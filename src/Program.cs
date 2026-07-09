using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace StitchPDF
{
    // Explorer launches one process per selected file for classic context-menu verbs.
    // The first process to grab the mutex becomes the "collector": it listens on a named
    // pipe and gathers file paths from its sibling processes until things go quiet, then
    // shows a single dialog and merges everything into one PDF.
    static class Program
    {
        const string MutexName = "Local\\StitchPDF_SingleInstance";
        const string PipeName = "StitchPDF_FileAggregator";
        const int QuietWindowMs = 700; // stop collecting after this long with no new sibling

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern int StrCmpLogicalW(string x, string y);

        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Silent mode for scripting/testing:
            //   StitchPDF.exe [--delete] --out <output.pdf> <file> <file> ...
            var argList = new List<string>(args);
            bool cliDelete = argList.Remove("--delete") | argList.Remove("--delete-originals");
            if (argList.Count > 1 && argList[0] == "--out")
                return RunCli(argList, cliDelete);

            try
            {
                return RunShell(args);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unexpected error:\r\n\r\n" + ex.Message, "Stitch into PDF",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        static int RunShell(string[] args)
        {
            var files = NormalizeExisting(args);
            if (files.Count == 0)
            {
                MessageBox.Show(
                    "Right-click one or more image/PDF files in Explorer and choose \"Stitch into PDF\".\r\n\r\n" +
                    "Command line: StitchPDF.exe [--delete] --out <output.pdf> <file1> <file2> ...",
                    "Stitch into PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }

            bool isCollector;
            var mutex = new Mutex(true, MutexName, out isCollector);
            using (mutex)
            {
                if (!isCollector)
                {
                    // A sibling is already collecting: hand our file over and exit.
                    if (SendToCollector(files))
                        return 0;

                    // The collector already stopped listening (or died). Try to take over.
                    try { isCollector = mutex.WaitOne(3000); }
                    catch (AbandonedMutexException) { isCollector = true; }

                    if (!isCollector && SendToCollector(files))
                        return 0;
                    // Last resort: proceed standalone with what we have.
                }

                try { CollectFromSiblings(files); }
                finally { if (isCollector) { try { mutex.ReleaseMutex(); } catch { } } }
            }

            // Explorer gives no selection order, so default to natural sort like Explorer's name column.
            files.Sort((a, b) => StrCmpLogicalW(a, b));

            if (Environment.GetEnvironmentVariable("STITCHPDF_SILENT") == "1")
            {
                var autoOut = UniquePath(Path.Combine(Path.GetDirectoryName(files[0]), "Stitched.pdf"));
                var autoSkipped = new List<string>();
                Merge(files, autoOut, autoSkipped, null);
                return autoSkipped.Count == 0 ? 0 : 3;
            }

            using (var form = new StitchForm(files))
            {
                if (form.ShowDialog() != DialogResult.OK)
                    return 0;

                var skipped = new List<string>();
                var merged = new List<string>();
                try
                {
                    Merge(form.OrderedFiles, form.OutputPath, skipped, merged);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not create the PDF:\r\n\r\n" + ex.Message,
                        "Stitch into PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }

                if (skipped.Count > 0)
                    MessageBox.Show("PDF created, but these files could not be added:\r\n\r\n" +
                        string.Join("\r\n", skipped),
                        "Stitch into PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                if (form.DeleteOriginals)
                {
                    // Only files that actually made it into the PDF; skipped files stay put.
                    var notRecycled = RecycleFiles(merged, form.OutputPath);
                    if (notRecycled.Count > 0)
                        MessageBox.Show("These files could not be moved to the Recycle Bin:\r\n\r\n" +
                            string.Join("\r\n", notRecycled),
                            "Stitch into PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                try { Process.Start(form.OutputPath); } catch { }
            }
            return 0;
        }

        static int RunCli(List<string> args, bool deleteOriginals)
        {
            try
            {
                var output = Path.GetFullPath(args[1]);
                var files = NormalizeExisting(args.Skip(2));
                if (files.Count == 0)
                    return 2;
                var skipped = new List<string>();
                var merged = new List<string>();
                Merge(files, output, skipped, merged);
                if (deleteOriginals)
                    RecycleFiles(merged, output);
                return skipped.Count == 0 ? 0 : 3;
            }
            catch
            {
                return 1;
            }
        }

        // Sends files to the Recycle Bin (restorable). Returns the ones that could not be recycled.
        static List<string> RecycleFiles(List<string> files, string outputPath)
        {
            var failed = new List<string>();
            foreach (var file in files)
            {
                // Never recycle the PDF we just created (e.g. an input overwritten as the output).
                if (string.Equals(file, outputPath, StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    FileSystem.DeleteFile(file, UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin, UICancelOption.DoNothing);
                }
                catch (Exception ex)
                {
                    failed.Add(Path.GetFileName(file) + "  (" + ex.Message + ")");
                }
            }
            return failed;
        }

        static void CollectFromSiblings(List<string> files)
        {
            var seen = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                NamedPipeServerStream server = null;
                try
                {
                    server = new NamedPipeServerStream(PipeName, PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                    IAsyncResult ar = server.BeginWaitForConnection(null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(QuietWindowMs))
                        return; // quiet long enough — every sibling has reported in

                    server.EndWaitForConnection(ar);
                    using (var reader = new StreamReader(server, Encoding.UTF8))
                    {
                        server = null; // reader now owns the stream
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            line = line.Trim();
                            if (line.Length > 0 && File.Exists(line) && seen.Add(line))
                                files.Add(line);
                        }
                    }
                }
                catch (IOException)
                {
                    // client vanished mid-handshake; keep listening
                }
                finally
                {
                    if (server != null) server.Dispose();
                }
            }
        }

        static bool SendToCollector(List<string> files)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    client.Connect(2000);
                    using (var writer = new StreamWriter(client, new UTF8Encoding(false)))
                    {
                        foreach (var f in files)
                            writer.WriteLine(f);
                        writer.Flush();
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static void Merge(List<string> files, string outputPath, List<string> skipped, List<string> merged)
        {
            using (var doc = new PdfDocument())
            {
                foreach (var file in files)
                {
                    try
                    {
                        if (string.Equals(Path.GetExtension(file), ".pdf", StringComparison.OrdinalIgnoreCase))
                            AppendPdf(doc, file);
                        else
                            AppendImage(doc, file);
                        if (merged != null)
                            merged.Add(file);
                    }
                    catch (Exception ex)
                    {
                        skipped.Add(Path.GetFileName(file) + "  (" + ex.Message + ")");
                    }
                }

                if (doc.PageCount == 0)
                    throw new InvalidOperationException("None of the selected files could be added to a PDF.");

                doc.Save(outputPath);
            }
        }

        static void AppendPdf(PdfDocument doc, string path)
        {
            using (var src = PdfReader.Open(path, PdfDocumentOpenMode.Import))
            {
                foreach (PdfPage page in src.Pages)
                    doc.AddPage(page);
            }
        }

        static void AppendImage(PdfDocument doc, string path)
        {
            using (var img = XImage.FromFile(path))
            {
                var page = doc.AddPage();
                page.Width = XUnit.FromPoint(img.PointWidth);
                page.Height = XUnit.FromPoint(img.PointHeight);
                using (var gfx = XGraphics.FromPdfPage(page))
                    gfx.DrawImage(img, 0, 0, img.PointWidth, img.PointHeight);
            }
        }

        static List<string> NormalizeExisting(IEnumerable<string> args)
        {
            var list = new List<string>();
            foreach (var a in args)
            {
                if (string.IsNullOrWhiteSpace(a))
                    continue;
                try
                {
                    var p = Path.GetFullPath(a);
                    if (File.Exists(p))
                        list.Add(p);
                }
                catch { }
            }
            return list;
        }

        internal static string UniquePath(string path)
        {
            if (!File.Exists(path))
                return path;
            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            for (int i = 2; ; i++)
            {
                var candidate = Path.Combine(dir, name + " (" + i + ")" + ext);
                if (!File.Exists(candidate))
                    return candidate;
            }
        }
    }
}
