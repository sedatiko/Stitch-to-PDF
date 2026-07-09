# Stitch into PDF

A tiny Windows context-menu tool: select any mix of **images and PDFs** in Explorer,
right-click, choose **"Stitch into PDF"**, and they are merged into a single PDF.
A small dialog lets you reorder the pages and type the output file name
(default: `Stitched.pdf`). The result is saved next to the first file and opened
when done.

- Images (JPG, PNG, GIF, BMP, TIFF) become full-size pages.
- PDFs are appended page by page.
- Optional **"Delete merged files"** checkbox (off by default) moves the original
  files to the **Recycle Bin** after a successful merge — restorable, and files that
  were skipped are never touched.
- No admin rights, no installer framework, no runtime to download — it compiles
  against the .NET Framework 4.x already built into Windows 10/11.

## Installation guide

### Requirements

- Windows 10 or 11 (64-bit). Nothing else — the app targets the .NET Framework 4.x
  that is already part of Windows, and it is compiled with the C# compiler that
  ships in `C:\Windows\Microsoft.NET\Framework64\v4.0.30319`.
- Internet access during the build only (to fetch the PDFsharp library from nuget.org).
- No admin rights: everything installs per-user (`%LOCALAPPDATA%` + `HKCU` registry).

### Option A — Download a release (no build needed)

1. Grab the latest `StitchPDF-x.y.z-win64.zip` from the
   [Releases page](https://github.com/sedatiko/Stitch-to-PDF/releases).
2. Extract it anywhere.
3. Run the installer from the extracted folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

The script unblocks the downloaded files (Mark of the Web) automatically. Then skip
straight to **Step 4 — Use it** below.

### Option B — Build from source

#### Step 1 — Get the code

```powershell
git clone https://github.com/sedatiko/Stitch-to-PDF.git
cd Stitch-to-PDF
```

#### Step 2 — Build

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

This downloads PDFsharp 1.50 from nuget.org, generates the app icon, and compiles
`dist\StitchPDF.exe` (~19 KB) next to `dist\PdfSharp.dll`.

#### Step 3 — Install

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

This does exactly two things:

1. Copies `StitchPDF.exe` and `PdfSharp.dll` to `%LOCALAPPDATA%\Programs\StitchPDF`.
2. Registers the **"Stitch into PDF"** verb for all image types and `.pdf` files under
   `HKCU\Software\Classes\SystemFileAssociations` (current user only).

No Explorer restart is needed — the menu is read live from the registry.

Optional: Windows hides context-menu verbs when more than 15 items are selected.
`.\install.ps1 -RaiseMultiSelectLimit` raises that Explorer-wide limit to 100.

### Step 4 — Use it

Select any mix of images and PDFs in Explorer, right-click, and on **Windows 11**
choose **"Show more options" → "Stitch into PDF"** (or press **Shift + right-click**
to open the classic menu directly). Reorder the files if needed, type a name
(default `Stitched`), optionally tick **"Delete merged files"** to send the originals
to the Recycle Bin, and click **Stitch**. Putting a verb in the new top-level
Win11 menu requires a packaged (MSIX) `IExplorerCommand` shell extension, which is
deliberately out of scope for this little tool.

### Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

Removes the registry entries and the installed files. Add `-ResetMultiSelectLimit`
if you used `-RaiseMultiSelectLimit` during install and want the Windows default back.

## How multi-select works (the fun part)

Classic context-menu verbs give you **one process per selected file** — Explorer
offers no way to hand a classic verb the whole selection at once. StitchPDF solves
this with a single-instance rendezvous:

1. Every launched instance races for a named mutex.
2. The winner becomes the *collector*: it listens on a named pipe.
3. Every loser connects to the pipe, sends its file path, and exits.
4. When the pipe has been quiet for ~700 ms, the collector sorts the files
   (natural sort, like Explorer's name column), shows the dialog, and merges.

## Command line

There is also a silent scripting mode:

```powershell
StitchPDF.exe --out C:\out\combined.pdf scan1.jpg scan2.png report.pdf
```

Add `--delete` to move the successfully merged source files to the Recycle Bin.
Exit codes: `0` ok, `3` ok but some files skipped, `1`/`2` failure.

## Limitations

- Windows hides context-menu verbs when **more than 15 items** are selected.
  Run `.\install.ps1 -RaiseMultiSelectLimit` to raise that Explorer-wide limit to 100.
- Selection order is not available to context-menu apps, so files start in
  name order — reorder them in the dialog if needed.
- WEBP/HEIC images and password-protected PDFs are not supported; they are
  reported as skipped rather than failing the whole merge.
- Multi-frame TIFFs contribute only their first frame.

## Built with

- [PDFsharp 1.50](http://www.pdfsharp.net/) (MIT) for PDF writing/merging
- C# / WinForms, compiled with the `csc.exe` that ships in
  `C:\Windows\Microsoft.NET\Framework64\v4.0.30319` — no SDK required

## License & disclaimer

This project is released under the **MIT License** — see [LICENSE](LICENSE) for the
full text. In short: you may use, copy, modify, and redistribute it freely, provided
the copyright and permission notice are preserved.

**No warranty, no liability.** As stated in the license, the software is provided
**"AS IS", without warranty of any kind**, express or implied, including but not
limited to the warranties of merchantability, fitness for a particular purpose, and
noninfringement. In no event shall the authors or copyright holders be liable for
any claim, damages, or other liability arising from, out of, or in connection with
the software or its use — this includes (without limitation) any loss or corruption
of files, documents, or data. The tool modifies only per-user registry keys and
writes only the output PDF you ask for, but **always keep backups of important
documents** and use it at your own risk.

Bundled dependency: [PDFsharp](http://www.pdfsharp.net/) is used under its own
MIT license (Copyright © empira Software GmbH).
