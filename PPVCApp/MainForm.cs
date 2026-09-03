using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PPVCApp
{
    public class MainForm : Form
    {
        private const string DefaultServerPath = @"\\whpl.local\whvn\VIET\05 Prefab\00 REVIT tools\Addin";
        private static readonly string[] TargetYears = new string[] { "2024", "2025", "2026" };

        // UI Controls
        private Panel pnlHeader;
        private Label lblTitle;
        private Label lblSubtitle;

        private GroupBox grpServer;
        private Label lblServerStatus;
        private TextBox txtServerPath;

        private GroupBox grpVersions;
        private CheckBox chk2024;
        private CheckBox chk2025;
        private CheckBox chk2026;
        private CheckBox chkSelectAll;

        private Panel pnlButtons;
        private Button btnInstall;
        private Button btnUninstallAll;

        private ProgressBar prgProgress;
        private RichTextBox rtbLog;

        private string resolvedSourceDir = DefaultServerPath;

        public MainForm()
        {
            InitializeComponent();
            this.Load += MainForm_Load;
        }

        private void InitializeComponent()
        {
            // System Terminal High-Contrast Colors
            Color bgDark = Color.FromArgb(12, 12, 12);
            Color headerBg = Color.FromArgb(20, 20, 20);
            Color btnBg = Color.FromArgb(26, 26, 26);
            Color btnHoverBg = Color.FromArgb(45, 45, 45);
            Color borderColor = Color.FromArgb(80, 80, 80);
            Color textWhite = Color.FromArgb(255, 255, 255);
            Color textDim = Color.FromArgb(170, 170, 170);
            Color accentGreen = Color.FromArgb(0, 255, 102);  // Terminal Bright Green
            Color accentCyan = Color.FromArgb(0, 229, 255);   // Terminal Bright Cyan
            Color accentRed = Color.FromArgb(255, 85, 85);    // Terminal Bright Red

            Font monoFont = new Font("Consolas", 9.5F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
            Font monoBold = new Font("Consolas", 9.5F, FontStyle.Bold, GraphicsUnit.Point, ((byte)(0)));
            Font monoTitle = new Font("Consolas", 12F, FontStyle.Bold, GraphicsUnit.Point, ((byte)(0)));

            this.Text = "REVIT_ADDIN_MANAGER // TERMINAL_SYS";
            this.Size = new Size(750, 670);
            this.MinimumSize = new Size(680, 580);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = bgDark;
            this.ForeColor = textWhite;
            this.Font = monoFont;

            // Header Panel
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = headerBg,
                Padding = new Padding(15, 10, 15, 10)
            };

            lblTitle = new Label
            {
                Text = "> REVIT_ADDIN_MANAGER // INSTALLER_v1.0",
                Font = monoTitle,
                ForeColor = accentGreen,
                AutoSize = true,
                Location = new Point(15, 10)
            };

            lblSubtitle = new Label
            {
                Text = "[STATUS: ONLINE] [DESTINATION: %APPDATA%\\Autodesk\\Revit\\Addins]",
                Font = new Font("Consolas", 8.5F, FontStyle.Regular),
                ForeColor = textDim,
                AutoSize = true,
                Location = new Point(17, 36)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);

            // Server GroupBox
            grpServer = new GroupBox
            {
                Text = " [ 01 // SERVER SOURCE CONFIG ] ",
                ForeColor = accentCyan,
                Font = monoBold,
                Location = new Point(20, 75),
                Size = new Size(695, 90),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblServerStatus = new Label
            {
                Text = "[SYS_CHECK] Kiểm tra đường dẫn kết nối server...",
                Font = new Font("Consolas", 9F, FontStyle.Regular),
                ForeColor = accentGreen,
                Location = new Point(15, 23),
                AutoSize = true
            };

            txtServerPath = new TextBox
            {
                Location = new Point(15, 46),
                Size = new Size(665, 24),
                BackColor = Color.FromArgb(5, 5, 5),
                ForeColor = accentGreen,
                Font = monoBold,
                Text = DefaultServerPath,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            txtServerPath.TextChanged += TxtServerPath_TextChanged;

            grpServer.Controls.Add(lblServerStatus);
            grpServer.Controls.Add(txtServerPath);

            // Versions GroupBox
            grpVersions = new GroupBox
            {
                Text = " [ 02 // TARGET REVIT VERSIONS ] ",
                ForeColor = accentCyan,
                Font = monoBold,
                Location = new Point(20, 175),
                Size = new Size(695, 65),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            chk2024 = new CheckBox
            {
                Text = "Revit 2024",
                Checked = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = textWhite,
                Font = monoBold,
                Location = new Point(20, 25),
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            chk2025 = new CheckBox
            {
                Text = "Revit 2025",
                Checked = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = textWhite,
                Font = monoBold,
                Location = new Point(160, 25),
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            chk2026 = new CheckBox
            {
                Text = "Revit 2026",
                Checked = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = textWhite,
                Font = monoBold,
                Location = new Point(300, 25),
                AutoSize = true,
                Cursor = Cursors.Hand
            };

            chkSelectAll = new CheckBox
            {
                Text = "[Select All]",
                Checked = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = accentGreen,
                Font = monoBold,
                Location = new Point(540, 25),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            chkSelectAll.CheckedChanged += ChkSelectAll_CheckedChanged;

            grpVersions.Controls.Add(chk2024);
            grpVersions.Controls.Add(chk2025);
            grpVersions.Controls.Add(chk2026);
            grpVersions.Controls.Add(chkSelectAll);

            // Action Buttons Panel
            pnlButtons = new Panel
            {
                Location = new Point(20, 250),
                Size = new Size(695, 44),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            btnInstall = new Button
            {
                Text = "[ EXECUTE INSTALL / UPDATE ]",
                Size = new Size(335, 36),
                Location = new Point(0, 4),
                FlatStyle = FlatStyle.Flat,
                BackColor = btnBg,
                ForeColor = accentGreen,
                Font = new Font("Consolas", 10.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            btnInstall.FlatAppearance.BorderSize = 1;
            btnInstall.FlatAppearance.BorderColor = accentGreen;
            btnInstall.FlatAppearance.MouseOverBackColor = btnHoverBg;
            btnInstall.Click += BtnInstall_Click;

            btnUninstallAll = new Button
            {
                Text = "[ UNINSTALL ALL ADDINS ]",
                Size = new Size(345, 36),
                Location = new Point(350, 4),
                FlatStyle = FlatStyle.Flat,
                BackColor = btnBg,
                ForeColor = accentRed,
                Font = new Font("Consolas", 10.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnUninstallAll.FlatAppearance.BorderSize = 1;
            btnUninstallAll.FlatAppearance.BorderColor = accentRed;
            btnUninstallAll.FlatAppearance.MouseOverBackColor = btnHoverBg;
            btnUninstallAll.Click += BtnUninstallAll_Click;

            pnlButtons.Controls.Add(btnInstall);
            pnlButtons.Controls.Add(btnUninstallAll);

            // ProgressBar
            prgProgress = new ProgressBar
            {
                Location = new Point(20, 302),
                Size = new Size(695, 8),
                Style = ProgressBarStyle.Blocks,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Log Output Box (Terminal View)
            rtbLog = new RichTextBox
            {
                Location = new Point(20, 318),
                Size = new Size(695, 295),
                BackColor = Color.FromArgb(5, 5, 5),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 9.5F, FontStyle.Regular),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // Add all controls to Form
            this.Controls.Add(pnlHeader);
            this.Controls.Add(grpServer);
            this.Controls.Add(grpVersions);
            this.Controls.Add(pnlButtons);
            this.Controls.Add(prgProgress);
            this.Controls.Add(rtbLog);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            LogInfo("INIT: Revit Addin Installer System initialized.");
            CheckRevitRunning();
            txtServerPath.Text = DefaultServerPath;
            ValidateServerPath(DefaultServerPath);
        }

        private void ChkSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            bool isChecked = chkSelectAll.Checked;
            chk2024.Checked = isChecked;
            chk2025.Checked = isChecked;
            chk2026.Checked = isChecked;
        }

        private void TxtServerPath_TextChanged(object sender, EventArgs e)
        {
            string rawInput = txtServerPath.Text.Trim('"', '\'', ' ');
            ValidateServerPath(rawInput);
        }

        private void ValidateServerPath(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                resolvedSourceDir = path;
                lblServerStatus.Text = "[CONNECTED] Đã kết nối đường dẫn server.";
                lblServerStatus.ForeColor = Color.FromArgb(0, 255, 102);
            }
            else if (!string.IsNullOrEmpty(path))
            {
                resolvedSourceDir = path;
                lblServerStatus.Text = "[WARNING] Thư mục server chưa kết nối được hoặc không tồn tại.";
                lblServerStatus.ForeColor = Color.FromArgb(255, 85, 85);
            }
        }

        private List<string> GetSelectedYears()
        {
            List<string> years = new List<string>();
            if (chk2024.Checked) years.Add("2024");
            if (chk2025.Checked) years.Add("2025");
            if (chk2026.Checked) years.Add("2026");
            return years;
        }

        private void CheckRevitRunning()
        {
            try
            {
                var revitProcesses = Process.GetProcessesByName("Revit");
                if (revitProcesses.Length > 0)
                {
                    LogWarning("[WARN] Process 'Revit.exe' detected active. Please close Revit to prevent file lock issues.");
                }
            }
            catch { }
        }

        private async void BtnInstall_Click(object sender, EventArgs e)
        {
            List<string> selectedYears = GetSelectedYears();
            if (selectedYears.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 phiên bản Revit!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string currentInput = txtServerPath.Text.Trim('"', '\'', ' ');
            if (!string.IsNullOrEmpty(currentInput))
            {
                resolvedSourceDir = currentInput;
            }

            if (string.IsNullOrEmpty(resolvedSourceDir) || !Directory.Exists(resolvedSourceDir))
            {
                MessageBox.Show($"Không tìm thấy thư mục server nguồn tại:\n{resolvedSourceDir}\n\nVui lòng kiểm tra lại kết nối mạng server!", "Lỗi đường dẫn server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SetUIEnabled(false);
            CheckRevitRunning();

            await Task.Run(() => PerformInstall(resolvedSourceDir, selectedYears));

            SetUIEnabled(true);
        }

        private void PerformInstall(string sourceAddinDir, List<string> selectedYears)
        {
            LogHeader("\n>>> EXECUTE: STARTING INSTALL / SYNC TASK...");
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string revitAddinsBaseDir = Path.Combine(appData, "Autodesk", "Revit", "Addins");

            if (!Directory.Exists(revitAddinsBaseDir))
            {
                Directory.CreateDirectory(revitAddinsBaseDir);
            }

            LogInfo($"[DEST] Base directory: {revitAddinsBaseDir}");

            DirectoryInfo sourceDirInfo = new DirectoryInfo(sourceAddinDir);
            DirectoryInfo[] sourceSubDirs = sourceDirInfo.GetDirectories();

            int totalCopied = 0;
            int totalErrors = 0;

            foreach (string year in selectedYears)
            {
                LogInfo($"\n--- Processing Revit Year: {year} ---");

                DirectoryInfo matchingSourceDir = sourceSubDirs.FirstOrDefault(d => d.Name.Equals(year, StringComparison.OrdinalIgnoreCase))
                    ?? sourceSubDirs.FirstOrDefault(d => d.Name.Equals($"Revit {year}", StringComparison.OrdinalIgnoreCase))
                    ?? sourceSubDirs.FirstOrDefault(d => d.Name.Equals($"Revit{year}", StringComparison.OrdinalIgnoreCase))
                    ?? sourceSubDirs.FirstOrDefault(d => d.Name.Contains(year));

                if (matchingSourceDir == null || !matchingSourceDir.Exists)
                {
                    LogWarning($" [SKIP] No source folder found for Revit {year}.");
                    continue;
                }

                string targetYearDir = Path.Combine(revitAddinsBaseDir, year);
                if (!Directory.Exists(targetYearDir))
                {
                    Directory.CreateDirectory(targetYearDir);
                    LogSuccess($" + Target folder created: {targetYearDir}");
                }

                LogInfo($" Copying: [{matchingSourceDir.FullName}] -> [{targetYearDir}]");
                CopyDirectoryContentRecursive(matchingSourceDir.FullName, targetYearDir, ref totalCopied, ref totalErrors);
            }

            LogHeader("\n=== TASK SUMMARY ===");
            LogSuccess($" - Total items copied / synced : {totalCopied}");
            if (totalErrors > 0)
            {
                LogError($" - Total items failed (File locked) : {totalErrors}");
                LogWarning(" Recommendation: Close Revit.exe and re-run installation.");
            }
            else
            {
                LogSuccess(" STATUS: ALL SELECTED REVIT ADDINS INSTALLED SUCCESSFULLY!");
            }
        }

        private void CopyDirectoryContentRecursive(string sourceDir, string targetDir, ref int totalCopied, ref int totalErrors)
        {
            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(filePath);
                string destFilePath = Path.Combine(targetDir, fileName);

                try
                {
                    File.Copy(filePath, destFilePath, true);
                    totalCopied++;
                    LogSuccess($"   [OK] {fileName}");
                }
                catch (Exception ex)
                {
                    totalErrors++;
                    LogError($"   [FAIL] {fileName}: {ex.Message}");
                }
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(subDir);
                string destSubDir = Path.Combine(targetDir, dirName);

                try
                {
                    if (!Directory.Exists(destSubDir))
                    {
                        Directory.CreateDirectory(destSubDir);
                    }
                    CopyDirectoryContentRecursive(subDir, destSubDir, ref totalCopied, ref totalErrors);
                }
                catch (Exception ex)
                {
                    totalErrors++;
                    LogError($"   [FAIL DIR] {dirName}: {ex.Message}");
                }
            }
        }

        private async void BtnUninstallAll_Click(object sender, EventArgs e)
        {
            DialogResult confirm = MessageBox.Show(
                "Xác nhận gỡ bỏ các Tool Revit PPVC trong thư mục %APPDATA%?\n\nLưu ý: Chỉ các file và thư mục có tên chứa 'PPVC' mới bị xóa. Các tool khác của hãng khác sẽ được giữ nguyên.",
                "Xác nhận gỡ bỏ PPVC Tool",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            SetUIEnabled(false);
            CheckRevitRunning();

            List<string> selectedYears = GetSelectedYears();
            if (selectedYears.Count == 0) selectedYears = TargetYears.ToList();

            await Task.Run(() => PerformUninstallAll(selectedYears));

            SetUIEnabled(true);
        }

        private void PerformUninstallAll(List<string> yearsToUninstall)
        {
            LogHeader("\n>>> EXECUTE: UNINSTALLING PPVC REVIT ADDINS...");
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string revitAddinsBaseDir = Path.Combine(appData, "Autodesk", "Revit", "Addins");

            int totalDeletedFiles = 0;
            int totalDeletedDirs = 0;
            int totalSkipped = 0;
            int totalErrors = 0;

            foreach (string year in yearsToUninstall)
            {
                LogInfo($"\n--- Cleaning PPVC Addins for Revit {year} ---");
                string targetYearDir = Path.Combine(revitAddinsBaseDir, year);

                if (!Directory.Exists(targetYearDir))
                {
                    LogWarning($" [SKIP] No addin directory for Revit {year} at: {targetYearDir}");
                    continue;
                }

                DirectoryInfo yearDirInfo = new DirectoryInfo(targetYearDir);

                // Only delete files containing "PPVC" (case-insensitive)
                foreach (FileInfo file in yearDirInfo.GetFiles())
                {
                    if (file.Name.IndexOf("PPVC", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try
                        {
                            file.Delete();
                            totalDeletedFiles++;
                            LogSuccess($"   [DELETED PPVC FILE] {file.Name}");
                        }
                        catch (Exception ex)
                        {
                            totalErrors++;
                            LogError($"   [DELETE FAIL] {file.Name}: {ex.Message}");
                        }
                    }
                    else
                    {
                        totalSkipped++;
                    }
                }

                // Only delete folders containing "PPVC" (case-insensitive)
                foreach (DirectoryInfo subDir in yearDirInfo.GetDirectories())
                {
                    if (subDir.Name.IndexOf("PPVC", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try
                        {
                            subDir.Delete(true);
                            totalDeletedDirs++;
                            LogSuccess($"   [DELETED PPVC DIR] {subDir.Name}");
                        }
                        catch (Exception ex)
                        {
                            totalErrors++;
                            LogError($"   [DELETE DIR FAIL] {subDir.Name}: {ex.Message}");
                        }
                    }
                    else
                    {
                        totalSkipped++;
                    }
                }
            }

            LogHeader("\n=== UNINSTALL SUMMARY ===");
            LogSuccess($" - PPVC Items Removed: {totalDeletedFiles} files, {totalDeletedDirs} directories");
            LogInfo($" - Non-PPVC Tools Preserved: {totalSkipped} items");
            if (totalErrors > 0)
            {
                LogError($" - Deletion errors (File locked by Revit): {totalErrors}");
                LogWarning(" Recommendation: Close Revit.exe and re-run uninstall.");
            }
            else
            {
                LogSuccess(" STATUS: PPVC ADDINS UNINSTALLED CLEANLY!");
            }
        }

        private void SetUIEnabled(bool enabled)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetUIEnabled(enabled)));
                return;
            }

            btnInstall.Enabled = enabled;
            btnUninstallAll.Enabled = enabled;
            grpVersions.Enabled = enabled;
            prgProgress.Style = enabled ? ProgressBarStyle.Blocks : ProgressBarStyle.Marquee;
        }

        private void LogHeader(string text)
        {
            AppendLogText(text + "\n", Color.FromArgb(0, 229, 255), true);
        }

        private void LogInfo(string text)
        {
            AppendLogText(text + "\n", Color.FromArgb(220, 220, 220), false);
        }

        private void LogSuccess(string text)
        {
            AppendLogText(text + "\n", Color.FromArgb(0, 255, 102), false);
        }

        private void LogWarning(string text)
        {
            AppendLogText(text + "\n", Color.FromArgb(255, 200, 0), false);
        }

        private void LogError(string text)
        {
            AppendLogText(text + "\n", Color.FromArgb(255, 85, 85), false);
        }

        private void AppendLogText(string text, Color color, bool bold)
        {
            if (rtbLog.InvokeRequired)
            {
                rtbLog.Invoke(new Action(() => AppendLogText(text, color, bold)));
                return;
            }

            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor = color;
            rtbLog.SelectionFont = new Font(rtbLog.Font, bold ? FontStyle.Bold : FontStyle.Regular);
            rtbLog.AppendText(text);
            rtbLog.SelectionColor = rtbLog.ForeColor;
            rtbLog.ScrollToCaret();
        }
    }
}
