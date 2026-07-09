using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace StitchPDF
{
    // Lets the user reorder/remove the collected files and pick the output name.
    // The PDF is saved next to the first file in the list.
    class StitchForm : Form
    {
        readonly Label lblHeader = new Label();
        readonly ListBox lstFiles = new ListBox();
        readonly Button btnUp = new Button();
        readonly Button btnDown = new Button();
        readonly Button btnRemove = new Button();
        readonly Label lblName = new Label();
        readonly TextBox txtName = new TextBox();
        readonly Label lblExt = new Label();
        readonly CheckBox chkDelete = new CheckBox();
        readonly Label lblFolder = new Label();
        readonly Button btnOk = new Button();
        readonly Button btnCancel = new Button();

        readonly List<string> files;

        public string OutputPath { get; private set; }
        public List<string> OrderedFiles { get { return new List<string>(files); } }
        public bool DeleteOriginals { get { return chkDelete.Checked; } }

        public StitchForm(List<string> initialFiles)
        {
            files = new List<string>(initialFiles);

            Text = "Stitch into PDF";
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(500, 468);
            MinimumSize = new Size(440, 408);
            MaximizeBox = false;
            ShowInTaskbar = true;
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            lblHeader.SetBounds(12, 12, 476, 20);
            lblHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            lstFiles.SetBounds(12, 36, 384, 252);
            lstFiles.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lstFiles.IntegralHeight = false;
            lstFiles.HorizontalScrollbar = true;

            btnUp.Text = "Move Up";
            btnUp.SetBounds(404, 36, 84, 28);
            btnUp.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnUp.Click += delegate { MoveSelected(-1); };

            btnDown.Text = "Move Down";
            btnDown.SetBounds(404, 70, 84, 28);
            btnDown.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnDown.Click += delegate { MoveSelected(+1); };

            btnRemove.Text = "Remove";
            btnRemove.SetBounds(404, 116, 84, 28);
            btnRemove.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRemove.Click += delegate { RemoveSelected(); };

            lblName.Text = "Output file name:";
            lblName.SetBounds(12, 300, 200, 18);
            lblName.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            txtName.Text = "Stitched";
            txtName.SetBounds(12, 321, 340, 25);
            txtName.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            lblExt.Text = ".pdf";
            lblExt.SetBounds(356, 324, 40, 18);
            lblExt.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            chkDelete.Text = "Delete merged files (move originals to Recycle Bin)";
            chkDelete.Checked = false;
            chkDelete.SetBounds(12, 352, 400, 22);
            chkDelete.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            lblFolder.SetBounds(12, 380, 476, 18);
            lblFolder.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lblFolder.ForeColor = SystemColors.GrayText;
            lblFolder.AutoEllipsis = true;

            btnOk.Text = "Stitch";
            btnOk.SetBounds(312, 416, 84, 30);
            btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnOk.Click += OnOk;

            btnCancel.Text = "Cancel";
            btnCancel.SetBounds(404, 416, 84, 30);
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.DialogResult = DialogResult.Cancel;

            Controls.AddRange(new Control[] {
                lblHeader, lstFiles, btnUp, btnDown, btnRemove,
                lblName, txtName, lblExt, chkDelete, lblFolder, btnOk, btnCancel
            });

            RefreshList();
            if (lstFiles.Items.Count > 0)
                lstFiles.SelectedIndex = 0;

            Shown += delegate
            {
                txtName.Focus();
                txtName.SelectAll();
                Activate(); // pull the window in front of Explorer
            };
        }

        void RefreshList()
        {
            lstFiles.BeginUpdate();
            lstFiles.Items.Clear();
            for (int i = 0; i < files.Count; i++)
                lstFiles.Items.Add((i + 1) + ".  " + Path.GetFileName(files[i]));
            lstFiles.EndUpdate();

            lblHeader.Text = "Pages will be added in this order (" + files.Count +
                (files.Count == 1 ? " file):" : " files):");
            lblFolder.Text = files.Count > 0
                ? "Saves to: " + Path.GetDirectoryName(files[0])
                : "";
        }

        void MoveSelected(int delta)
        {
            int i = lstFiles.SelectedIndex;
            int j = i + delta;
            if (i < 0 || j < 0 || j >= files.Count)
                return;
            var tmp = files[i];
            files[i] = files[j];
            files[j] = tmp;
            RefreshList();
            lstFiles.SelectedIndex = j;
        }

        void RemoveSelected()
        {
            int i = lstFiles.SelectedIndex;
            if (i < 0)
                return;
            files.RemoveAt(i);
            RefreshList();
            if (files.Count > 0)
                lstFiles.SelectedIndex = Math.Min(i, files.Count - 1);
        }

        void OnOk(object sender, EventArgs e)
        {
            if (files.Count == 0)
            {
                MessageBox.Show(this, "There are no files left to stitch.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var name = txtName.Text.Trim();
            if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show(this, "Please enter a valid file name (no \\ / : * ? \" < > | characters).",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
                txtName.SelectAll();
                return;
            }

            if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                name += ".pdf";

            var path = Path.Combine(Path.GetDirectoryName(files[0]), name);
            if (File.Exists(path))
            {
                var answer = MessageBox.Show(this, "\"" + name + "\" already exists. Overwrite it?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes)
                    return;
            }

            OutputPath = path;
            DialogResult = DialogResult.OK;
        }
    }
}
