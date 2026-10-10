using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using dnlib.Load;
using EmbedLibrary.Core;
using dnlib.DotNet;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reflection;

namespace EmbedLibrary
{
    public partial class Form1 : Form
    {
        private string _file = string.Empty;
        private List<string> _librarys = new List<string>();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog open = new OpenFileDialog())
            {
                open.Title = "Choose your file";
                open.Filter = "All File(*.exe, *.dll)|*.exe;*.dll|Execute File(*.exe)|*.exe|Library File(*.dll)|*.dll";
                if (open.ShowDialog() == DialogResult.OK)
                {
                    _file = open.FileName;
                    txtName.Text = Path.GetFileName(open.FileName);
                }
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog open = new OpenFileDialog())
            {
                open.Title = "Choose your file";
                open.Filter = "Library File(*.dll)|*.dll";
                open.Multiselect = true;
                if (open.ShowDialog() == DialogResult.OK)
                {
                    var files = open.FileNames;
                    foreach (var file in files)
                    {
                        _librarys.Add(file);
                        lbLibrarys.Items.Add(Path.GetFileName(file));
                    }
                }
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            Clear();
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_file))
            {
                Task.Factory.StartNew(() => { Execute(chkAutomatically.Checked, chkWritePdb.Checked); });
            }
            else
                MessageBox.Show("Please select the desired program!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void chkAutomatically_CheckedChanged(object sender, EventArgs e)
        {
            Clear();
            btnAdd.Enabled = !chkAutomatically.Checked;
            btnClear.Enabled = !chkAutomatically.Checked;
        }

        private void menuLatestRelease_Click(object sender, EventArgs e)
        {
            OpenUrl("https://github.com/soheil-js/EmbedLibrary/releases");
        }

        private void menuSourceCode_Click(object sender, EventArgs e)
        {
            OpenUrl("https://github.com/soheil-js/EmbedLibrary");
        }

        private void menuAbout_Click(object sender, EventArgs e)
        {
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);
            string version = $"{versionInfo.ProductMajorPart}.{versionInfo.ProductMinorPart}";
            string copyright = versionInfo.LegalCopyright;
            MessageBox.Show($"Developed by 'Soheil Jashnsaz'\n\nVersion {version}\n{copyright}", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void txtName_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                Array array = (Array)e.Data.GetData(DataFormats.FileDrop);
                if (array != null)
                {
                    string path = array.GetValue(0).ToString();
                    int num = path.LastIndexOf(".");
                    if (num != -1)
                    {
                        string a = path.Substring(num).ToLower();
                        if (a == ".exe" || a == ".dll")
                        {
                            base.Activate();
                            _file = path;
                            txtName.Text = Path.GetFileName(path);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private void txtName_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                return;
            }
            e.Effect = DragDropEffects.None;
        }

        private void lbLibrarys_DragDrop(object sender, DragEventArgs e)
        {
            if (!chkAutomatically.Checked)
            {
                try
                {
                    Array array = (Array)e.Data.GetData(DataFormats.FileDrop);
                    if (array != null)
                    {
                        foreach (var file in array)
                        {
                            string path = file.ToString();
                            int num = path.LastIndexOf(".");
                            if (num != -1)
                            {
                                string a = path.Substring(num).ToLower();
                                if (a == ".dll")
                                {
                                    base.Activate();
                                    Add(path);
                                }
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private void lbLibrarys_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && !chkAutomatically.Checked)
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private string Address(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!dir.EndsWith("\\"))
                dir += "\\";
            if (!Directory.Exists($"{dir}Embedded"))
                Directory.CreateDirectory($"{dir}Embedded");
            string file = $"{dir}Embedded\\{Path.GetFileNameWithoutExtension(path) + Path.GetExtension(path)}";
            if (File.Exists(file))
                File.Delete(file);
            return file;
        }

        private void Clear()
        {
            this.Invoke((MethodInvoker)delegate {
                _librarys.Clear();
                lbLibrarys.Items.Clear();
            });
        }

        private void Add(string path)
        {
            this.Invoke((MethodInvoker)delegate {
                _librarys.Add(path);
                lbLibrarys.Items.Add(Path.GetFileName(path));
            });
        }

        private void GetRefs(string file)
        {
            using (AssemblyLoader assembly = new AssemblyLoader(file))
            {
                string dir = Path.GetDirectoryName(file);
                var refs = assembly.ModuleDefMD.GetAssemblyRefs();
                foreach (AssemblyRef asmRef in refs)
                {
                    if (!string.IsNullOrEmpty(dir))
                    {
                        string path = $"{Path.Combine(dir, asmRef.Name)}.dll";
                        if (File.Exists(path) && !_librarys.Contains(path))
                        {
                            Add(path);
                            GetRefs(path);
                        }
                    }
                }
            }
        }

        private void Execute(bool automatically, bool writePdb)
        {
            if (automatically)
            {
                Clear();
                GetRefs(_file);
            }

            if (_librarys.Count > 0)
            {
                using (AssemblyLoader assembly = new AssemblyLoader(_file))
                {
                    AssemblyContext context = assembly.GetAssemblyContext();
                    Embedder.Execute(context, _librarys.ToArray());
                    string address = Address(_file);
                    assembly.Write(address, writePdb);
                }

                if (Path.GetExtension(_file) == ".dll")
                {
                    string name = Path.GetFileNameWithoutExtension(_file);
                    string dir = Path.GetDirectoryName(_file);
                    if (File.Exists($"{dir}\\{name}.runtimeconfig.json"))
                    {
                        try
                        {
                            File.Copy($"{dir}\\{name}.exe", Address($"{dir}\\{name}.exe"));
                            File.Copy($"{dir}\\{name}.runtimeconfig.json", Address($"{dir}\\{name}.runtimeconfig.json"));
                        }
                        catch
                        {
                        }
                    }
                }

                MessageBox.Show("Completed!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
                MessageBox.Show("Please select the desired libraries!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static bool OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            Uri uri;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out uri))
                return false;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;

            try
            {
                using (Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
                {
                    UseShellExecute = true
                }))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
