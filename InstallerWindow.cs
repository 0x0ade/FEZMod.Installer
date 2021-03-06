﻿using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.IO;
using System.Drawing.Text;
using Mono.Cecil;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows.Forms.VisualStyles;

using ContentAlignment = System.Drawing.ContentAlignment;

namespace FezGame.Mod.Installer {
    public class InstallerWindow : Form {

        public static Version Version = Assembly.GetEntryAssembly().GetName().Version;

        public static InstallerWindow Instance;

        public OpenFileDialog OpenFileDialog;
        
        public RichTextBox LogBox;

        public TextBox ExePathBox;
        public Button ExePathButton;
        public Label ExeStatusLabel;
        public TabControl VersionTabs;
        public ListBox StableVersionList;
        public ListBox NightlyVersionList;
        public TextBox ManualPathBox;
        public Button ManualPathButton;
        public Button InstallButton;
        public Button UninstallButton;
        public CustomProgress Progress;
        
        public int AddIndex = 0;
        public int AddOffset = 0;
        
        public List<Tuple<string, string>> StableVersions;
        public List<Tuple<string, string>> NightlyVersions;
        
        public string FezVersion;
        public string FezModVersion;
        public MonoMod.MonoMod ExeMod;
        
        public InstallerWindow() {
            Text = "FEZMod Installer";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ResizeRedraw = false;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;

            //Setting the font doesn't change anything...
            /*PrivateFontCollection pfc = LoadAsset<PrivateFontCollection>("fonts.uni05_53");
            for (int i = 0; i < pfc.Families.Length; i++) {
                Console.WriteLine("Font " + i + ": " + pfc.Families[i]);
            }
            GlobalFont = new Font(pfc.Families[0], 8f);*/
            AllowDrop = true;
            DragDrop += onDragDrop;
            DragEnter += delegate(object sender, DragEventArgs e) {
                if (e.Data.GetDataPresent(DataFormats.FileDrop) && VersionTabs.Enabled) {
                    e.Effect = DragDropEffects.Copy;
                    VersionTabs.SelectedIndex = 2;
                }
            };
            BackgroundImage = LoadAsset<Image>("background");
            BackgroundImageLayout = ImageLayout.Center;
            Icon = LoadAsset<Icon>("icons.main");

            MinimumSize = Size = MaximumSize = BackgroundImage.Size + (Environment.OSVersion.Platform.ToString().ToLower().Contains("win") ? new Size(8, 8) : new Size());
            
            Controls.Add(new Label() {
                Bounds = new Rectangle(448, 338, 308, 16),
                //Font = GlobalFont,
                TextAlign = ContentAlignment.BottomRight,
                Text = "v" + Version,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(127, 0, 0, 0)
            });
            
            Controls.Add(LogBox = new RichTextBox() {
                Bounds = new Rectangle(0, 0, 448, 358),
                ReadOnly = true,
                Multiline = true,
                //ScrollBars = System.Windows.Forms.ScrollBars.Vertical,
                DetectUrls = true,
                ShortcutsEnabled = true,
                BackColor = Color.Black,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                WordWrap = true,
                Text = "FEZMod Installer v" + Version + "\n",
                Visible = false,
            });

            Controls.Add(Progress = new CustomProgress() {
                Bounds = new Rectangle(448, 313, 312, 24),
                //Font = GlobalFont,
                Text = "Idle."
            });
            
            Add(new Label() {
                //Font = GlobalFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Step 1: Select FEZ.exe",
                BackColor = Color.Transparent,
                ForeColor = Color.Black
            });
            
            Add(ExePathBox = new TextBox() {
                ReadOnly = true
            });
            ExePathBox.Width -= 32;
            Controls.Add(ExePathButton = new Button() {
                Bounds = new Rectangle(ExePathBox.Bounds.X + ExePathBox.Bounds.Width, ExePathBox.Bounds.Y, 32, ExePathBox.Bounds.Height),
                Image = LoadAsset<Image>("icons.open"),
                ImageAlign = ContentAlignment.MiddleCenter
            });
            ExePathButton.Click += delegate(object senderClick, EventArgs eClick) {
                if (OpenFileDialog == null) {
                    OpenFileDialog = new OpenFileDialog() {
                        Title = "Select FEZ.exe",
                        AutoUpgradeEnabled = true,
                        CheckFileExists = true,
                        CheckPathExists = true,
                        ValidateNames = true,
                        Multiselect = false,
                        ShowReadOnly = false,
                        Filter = "FEZ.exe|FEZ.exe|All files|*.*",
                        FilterIndex = 0
                    };
                    OpenFileDialog.FileOk +=
                        (object senderFileOk, CancelEventArgs eFileOk) => Task.Run(() => this.ExeSelected(OpenFileDialog.FileNames[0]));
                }
                
                OpenFileDialog.ShowDialog(this);
            };
            AddOffset += 2;
            
            Add(ExeStatusLabel = new Label() {
                //Font = GlobalFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "No FEZ.exe selected",
                BackColor = Color.FromArgb(127, 255, 63, 63),
                ForeColor = Color.Black
            });
            
            AddOffset += 2;
            
            Add(new Label() {
                //Font = GlobalFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Step 2: Choose FEZMod version",
                BackColor = Color.Transparent,
                ForeColor = Color.Black
            });
            
            Controls.Add(InstallButton = new Button() {
                Bounds = new Rectangle(448, 313 - 1 - ExePathButton.Size.Height, 312 - 32, ExePathButton.Size.Height),
                //Font = GlobalFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Step 3: Install FEZMod",
                Enabled = false
            });
            InstallButton.Click += (object senderClick, EventArgs eClick) => Task.Run((Action) this.Install);
            Controls.Add(UninstallButton = new Button() {
                Bounds = new Rectangle(InstallButton.Bounds.X + InstallButton.Bounds.Width, InstallButton.Bounds.Y, 32, InstallButton.Bounds.Height),
                Image = LoadAsset<Image>("icons.uninstall"),
                ImageAlign = ContentAlignment.MiddleCenter
            });
            UninstallButton.Click += (object senderClick, EventArgs eClick) => Task.Run(delegate() {
                this.Uninstall();
                this.ClearCache();
                this.ExeSelected(ExeMod.In.FullName, " [just uninstalled]");
                this.SetMainEnabled(true);
            });
            
            Controls.Add(VersionTabs = new TabControl() {
                Bounds = new Rectangle(448, 4 + 26 * AddIndex + AddOffset, 312, InstallButton.Location.Y - (4 + 26 * AddIndex + AddOffset)),
                BackColor = Color.Transparent
            });
            
            VersionTabs.TabPages.Add(new TabPage("Stable"));
            VersionTabs.TabPages[0].Controls.Add(StableVersionList = new ListBox() {
                Dock = DockStyle.Fill,
                MultiColumn = true
            });
            
            VersionTabs.TabPages.Add(new TabPage("Nightly"));
            VersionTabs.TabPages[1].Controls.Add(NightlyVersionList = new ListBox() {
                Dock = DockStyle.Fill,
                MultiColumn = true
            });
            
            VersionTabs.TabPages.Add(new TabPage("Manual"));
            Panel manualPanel;
            VersionTabs.TabPages[2].Controls.Add(manualPanel = new Panel() {
                Dock = DockStyle.Fill
            });
            manualPanel.Controls.Add(new Label() {
                Bounds = new Rectangle(0, 24, VersionTabs.Width - 8, 24),
                Text = "or drag-and-drop a folder / .zip here",
                TextAlign = ContentAlignment.MiddleCenter
            });
            manualPanel.Controls.Add(ManualPathBox = new TextBox() {
                Bounds = new Rectangle(0, 0, VersionTabs.Width - 32 - 8, 24),
                ReadOnly = true
            });
            manualPanel.Controls.Add(ManualPathButton = new Button() {
                Bounds = new Rectangle(ManualPathBox.Bounds.X + ManualPathBox.Bounds.Width, ManualPathBox.Bounds.Y, 32, ManualPathBox.Bounds.Height),
                Image = LoadAsset<Image>("icons.open"),
                ImageAlign = ContentAlignment.MiddleCenter
            });
            ManualPathButton.Click += delegate(object senderClick, EventArgs eClick) {
                if (OpenFileDialog == null) {
                    OpenFileDialog = new OpenFileDialog() {
                        Title = "Select FEZMod ZIP",
                        AutoUpgradeEnabled = true,
                        CheckFileExists = true,
                        CheckPathExists = true,
                        ValidateNames = true,
                        Multiselect = false,
                        ShowReadOnly = false,
                        Filter = "FEZMod ZIP|*.zip|All files|*.*",
                        FilterIndex = 0
                    };
                    OpenFileDialog.FileOk +=
                        (object senderFileOk, CancelEventArgs eFileOk) => ManualPathBox.Text = OpenFileDialog.FileNames[0];
                }

                OpenFileDialog.ShowDialog(this);
            };
        }
        
        public InstallerWindow SetMainEnabled(bool enabled) {
            return Invoke(delegate() {
                ExePathBox.Enabled = enabled;
                ExePathButton.Enabled = enabled;
                VersionTabs.Enabled = enabled;
                StableVersionList.Enabled = enabled;
                NightlyVersionList.Enabled = enabled;
                InstallButton.Enabled = enabled && FezVersion != null;
                UninstallButton.Enabled = enabled;
            });
        }
        
        public void DownloadStableVersionList() {
            Invoke(delegate() {
                StableVersionList.BeginUpdate();
                StableVersionList.Items.Add("Downloading list...");
                StableVersionList.EndUpdate();
            });
            
            try {
                StableVersions = VersionHelper.GetStableVersions();
            } catch (Exception e) {
                StableVersions = null;
                LogLine("Something went horribly wrong:");
                LogLine(e.ToString());
                Invoke(delegate() {
                    StableVersionList.BeginUpdate();
                    StableVersionList.Items.Clear();
                    StableVersionList.Items.Add("Something went wrong - see the log.");
                    StableVersionList.EndUpdate();
                });
                return;
            }
            
            Invoke(delegate() {
                StableVersionList.BeginUpdate();
                StableVersionList.Items.Clear();
                for (int i = 0; i < StableVersions.Count; i++) {
                    StableVersionList.Items.Add(StableVersions[i].Item1);
                }
                StableVersionList.SelectedIndex = 0;
                StableVersionList.EndUpdate();
            });
        }
        
        public void DownloadNightlyVersionList() {
            Invoke(delegate() {
                NightlyVersionList.BeginUpdate();
                NightlyVersionList.Items.Add("Downloading list...");
                NightlyVersionList.EndUpdate();
            });
            
            try {
                NightlyVersions = VersionHelper.GetNightlyVersions();
            } catch (Exception e) {
                NightlyVersions = null;
                LogLine("Something went horribly wrong:");
                LogLine(e.ToString());
                Invoke(delegate() {
                    NightlyVersionList.BeginUpdate();
                    NightlyVersionList.Items.Clear();
                    NightlyVersionList.Items.Add("Something went wrong - see the log.");
                    NightlyVersionList.EndUpdate();
                });
                return;
            }
            
            Invoke(delegate() {
                NightlyVersionList.BeginUpdate();
                NightlyVersionList.Items.Clear();
                for (int i = 0; i < NightlyVersions.Count; i++) {
                    NightlyVersionList.Items.Add(NightlyVersions[i].Item1);
                }
                NightlyVersionList.SelectedIndex = 0;
                NightlyVersionList.EndUpdate();
            });
        }
        
        public void Add(Control c) {
            c.Bounds = new Rectangle(448, 4 + 26 * AddIndex + AddOffset, 312, 24);
            Controls.Add(c);
            AddIndex++;
        }
        
        public InstallerWindow Invoke(Action d) {
            base.Invoke(d);
            return this;
        }
        
        public InstallerWindow InitProgress(string str, int max) {
            return Invoke(delegate() {
                Progress.Value = 0;
                Progress.Maximum = max;
                Progress.Text = str;
                Progress.Invalidate();
            });
        }
        public InstallerWindow SetProgress(int val) {
            return Invoke(delegate() {
                Progress.Value = val;
                Progress.Invalidate();
            });
        }
        public InstallerWindow SetProgress(string str, int val) {
            return Invoke(delegate() {
                Progress.Value = val;
                Progress.Text = str;
                Progress.Invalidate();
            });
        }
        public InstallerWindow EndProgress() {
            return Invoke(delegate() {
                Progress.Value = Progress.Maximum;
                Progress.Invalidate();
            });
        }
        public InstallerWindow EndProgress(string str) {
            return Invoke(delegate() {
                Progress.Value = Progress.Maximum;
                Progress.Text = str;
                Progress.Invalidate();
            });
        }
        
        private List<string> logScheduled = new List<string>();
        private Task logUpdateTask;
        public InstallerWindow Log(string s) {
            logScheduled.Add(s);
            if (logUpdateTask == null) {
                logUpdateTask = Task.Run((Action) LogFlush);
            }
            return this;
        }
        public InstallerWindow LogLine() {
            Log("\n");
            return this;
        }
        public InstallerWindow LogLine(string s) {
            Log(s);
            LogLine();
            return this;
        }
        public void LogFlush() {
            Thread.Sleep(100);
            string added = "";
            while (0 < logScheduled.Count) {
                added += logScheduled[0];
                logScheduled.RemoveAt(0);
            }
            Invoke(delegate() {
                LogBox.Visible = true;
                LogBox.Text += added;
                LogBox.SelectionStart = LogBox.Text.Length;
                LogBox.SelectionLength = 0;
                LogBox.ScrollToCaret();
            });
            logUpdateTask = null;
        }
        
        private void onHandleCreated(object sender, EventArgs e) {
            HandleCreated -= onHandleCreated;
            Task.Run((Action) FezFinder.FindFEZ);
            Task.Run((Action) DownloadStableVersionList);
            Task.Run((Action) DownloadNightlyVersionList);
        }

        private void onDragDrop(object sender, DragEventArgs e) {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && 0 < files.Length && Directory.Exists(files[0]) || files[0].ToLower().EndsWith(".zip")) {
                ManualPathBox.Text = files[0];
            }
        }

        [STAThread]
        public static void Main(string[] args) {
            Console.WriteLine("Entering the holy realm of FEZMod.");
            Application.EnableVisualStyles();

            Assembly assembly = Assembly.GetExecutingAssembly();
            string[] manifestResourceNames = assembly.GetManifestResourceNames();
            for (int i = 0; i < manifestResourceNames.Length; i++) {
                Console.WriteLine("Asset " + i + ": " + manifestResourceNames[i]);
            }

            Application.VisualStyleState = VisualStyleState.ClientAndNonClientAreasEnabled;
            Instance = new InstallerWindow();
            Instance.HandleCreated += Instance.onHandleCreated;
            
            ShowDialog:
            try {
                Instance.ShowDialog();
            } catch (Exception e) {
                //Gonna blame X11.
                Console.WriteLine(e.ToString());
                MessageBox.Show("Your window manager has left the building!\nThis simply means the installer crashed,\nbut your window manager caused it.", "FEZMod Installer");
                goto ShowDialog;
            }
        }

        public static T LoadAsset<T>(string name, bool fullPath = false) where T : class {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Type t = typeof(T);

            if (t == typeof(Image) || t == typeof(Bitmap)) {
                using (Stream s = assembly.GetManifestResourceStream(fullPath ? name : "FEZMod.Installer.Assets." + name + ".png")) {
                    return Image.FromStream(s) as T;
                }
            }

            if (t == typeof(Icon)) {
                return Icon.FromHandle(LoadAsset<Bitmap>(name, fullPath).GetHicon()) as T;
            }

            if (t == typeof(PrivateFontCollection)) {
                PrivateFontCollection pfc = new PrivateFontCollection();
                byte[] data;
                using (Stream s = assembly.GetManifestResourceStream(fullPath ? name : "FEZMod.Installer.Assets." + name + ".ttf")) {
                    data = new byte[s.Length];
                    s.Read(data, 0, (int) s.Length);
                }
                //yeeey, unsafe!
                unsafe {
                    fixed (byte* pData = data) {
                        pfc.AddMemoryFont((IntPtr) pData, data.Length);
                    }
                }
                return pfc as T;
            }

            return default(T);
        }

    }
}
