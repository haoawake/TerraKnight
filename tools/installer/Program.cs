using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TerraKnightSetup
{
    /// <summary>
    /// 空洞泰拉's installer: one program carrying everything (BepInEx 5, the mod, the launcher) in an embedded zip. It finds
    /// Hollow Knight and Terraria in the Steam libraries on any drive (or lets the player pick them), unpacks into the game
    /// folder, points the mod at Terraria, and leaves 空洞泰拉.bat (and a desktop shortcut) to start the modded game.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // For a check from the command line: what it finds, written to a file.
            int detect = Array.IndexOf(args, "--detect");
            if (detect >= 0 && detect + 1 < args.Length)
            {
                var libs = Steam.Libraries();
                File.WriteAllText(args[detect + 1], "libraries=" + string.Join(";", libs) + "\r\nhollow=" + Steam.Find(libs, "Hollow Knight", Steam.IsHollowKnight)
                    + "\r\nterraria=" + Steam.Find(libs, "Terraria", Steam.IsTerraria) + "\r\n", new UTF8Encoding(false));
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm(args));
        }
    }

    /// <summary>Steam's libraries on this computer and the games in them.</summary>
    internal static class Steam
    {
        /// <summary>Every Steam library: Steam's own list (libraryfolders.vdf) and the usual library places on each drive.</summary>
        public static List<string> Libraries()
        {
            var found = new List<string>();
            void Add(string p)
            {
                if (string.IsNullOrWhiteSpace(p)) return;
                try
                {
                    p = Path.GetFullPath(p.Replace('/', '\\').Replace("\\\\", "\\"));
                    if (Directory.Exists(Path.Combine(p, "steamapps")) && !found.Contains(p, StringComparer.OrdinalIgnoreCase)) found.Add(p);
                }
                catch (Exception) { }
            }
            foreach (var steam in SteamDirs())
            {
                Add(steam);
                var vdf = Path.Combine(steam.Replace('/', '\\'), "steamapps", "libraryfolders.vdf");
                try
                {
                    if (File.Exists(vdf))
                        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                            Add(m.Groups[1].Value);
                }
                catch (Exception) { }
            }
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                    foreach (var rel in new[] { "SteamLibrary", "Steam", @"Program Files (x86)\Steam", @"Program Files\Steam", @"Games\Steam", @"Games\SteamLibrary", @"Game\Steam", @"Game\SteamLibrary" })
                        Add(Path.Combine(d.RootDirectory.FullName, rel));
                }
                catch (Exception) { }
            }
            return found;
        }

        static IEnumerable<string> SteamDirs()
        {
            var keys = new[]
            {
                (@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath"),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath"),
            };
            foreach (var (key, value) in keys)
            {
                string v = null;
                try { v = Registry.GetValue(key, value, null) as string; } catch (Exception) { }
                if (!string.IsNullOrEmpty(v)) yield return v;
            }
        }

        /// <summary>A game's folder (steamapps\common\name) in the first library that has its program.</summary>
        public static string Find(IEnumerable<string> libraries, string name, Func<string, bool> valid)
        {
            foreach (var lib in libraries)
            {
                var dir = Path.Combine(lib, "steamapps", "common", name);
                if (valid(dir)) return dir;
            }
            return null;
        }

        public static bool IsHollowKnight(string dir) => !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "hollow_knight.exe"));

        /// <summary>A Terraria the mod can read: its Content\Images.</summary>
        public static bool IsTerraria(string dir) => !string.IsNullOrEmpty(dir) && Directory.Exists(Path.Combine(dir, "Content", "Images"));
    }

    internal sealed class SetupForm : Form
    {
        const string ConfigName = "com.haoawake.terraknight.cfg";
        readonly TextBox hollow = new TextBox(), terraria = new TextBox(), log = new TextBox();
        readonly CheckBox shortcut = new CheckBox();
        readonly Button install = new Button(), quit = new Button();
        readonly bool auto, quiet;
        readonly string version;

        public SetupForm(string[] args)
        {
            version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            Text = "空洞泰拉 TerraKnight v" + version + " 安装";
            Font = new Font("Microsoft YaHei UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(620, 430);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            var title = new Label { Text = "空洞泰拉 TerraKnight v" + version, Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold), AutoSize = true, Location = new Point(16, 12) };
            var note = new Label
            {
                Text = "把泰拉瑞亚装进空洞骑士。会一并装好 BepInEx 5 框架；泰拉瑞亚（Steam 版 1.4）需要已经装在这台电脑上。",
                AutoSize = false, Size = new Size(588, 36), Location = new Point(16, 46),
            };
            Controls.Add(title);
            Controls.Add(note);
            Row("空洞骑士的游戏目录（hollow_knight.exe 所在）", hollow, 88, "选择空洞骑士的游戏目录", Steam.IsHollowKnight);
            Row("泰拉瑞亚的游戏目录（Terraria.exe 所在）", terraria, 146, "选择泰拉瑞亚的游戏目录", Steam.IsTerraria);
            shortcut.Text = "在桌面创建“空洞泰拉”快捷方式";
            shortcut.Checked = true;
            shortcut.AutoSize = true;
            shortcut.Location = new Point(16, 204);
            Controls.Add(shortcut);
            log.Multiline = true;
            log.ReadOnly = true;
            log.ScrollBars = ScrollBars.Vertical;
            log.Location = new Point(16, 234);
            log.Size = new Size(588, 140);
            Controls.Add(log);
            install.Text = "安装";
            install.Size = new Size(110, 34);
            install.Location = new Point(374, 386);
            install.Click += (s, e) => Install();
            quit.Text = "退出";
            quit.Size = new Size(110, 34);
            quit.Location = new Point(494, 386);
            quit.Click += (s, e) => Close();
            Controls.Add(install);
            Controls.Add(quit);
            AcceptButton = install;

            // An elevated restart of this installer brings the folders chosen and installs at once.
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "--hollow") hollow.Text = args[i + 1];
                if (args[i] == "--terraria") terraria.Text = args[i + 1];
                if (args[i] == "--shortcut") shortcut.Checked = args[i + 1] == "1";
            }
            auto = args.Contains("--auto");
            quiet = args.Contains("--quiet");
            Shown += (s, e) =>
            {
                if (hollow.Text.Length == 0 || terraria.Text.Length == 0) Detect();
                if (auto) Install();
            };
        }

        void Row(string label, TextBox box, int y, string pick, Func<string, bool> valid)
        {
            Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(16, y) });
            box.Location = new Point(16, y + 22);
            box.Size = new Size(500, 26);
            Controls.Add(box);
            var browse = new Button { Text = "浏览…", Location = new Point(524, y + 20), Size = new Size(80, 28) };
            browse.Click += (s, e) =>
            {
                using (var dialog = new FolderBrowserDialog { Description = pick, ShowNewFolderButton = false })
                {
                    if (Directory.Exists(box.Text)) dialog.SelectedPath = box.Text;
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    box.Text = dialog.SelectedPath;
                    if (!valid(box.Text)) Say("这个文件夹看起来不对：" + box.Text);
                }
            };
            Controls.Add(browse);
        }

        void Tell(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            Say(text);
            if (!quiet) MessageBox.Show(this, text, caption, buttons, icon);
        }

        void Say(string line)
        {
            log.AppendText(line + Environment.NewLine);
            log.Update();
        }

        /// <summary>Hollow Knight: this program's own folder if it sits in the game's, else the Steam libraries; Terraria likewise.</summary>
        void Detect()
        {
            Say("正在查找游戏……");
            var libraries = Steam.Libraries();
            Say("找到 Steam 库 " + libraries.Count + " 个：" + string.Join("；", libraries));
            string here = Path.GetDirectoryName(Application.ExecutablePath);
            if (hollow.Text.Length == 0)
                hollow.Text = Steam.IsHollowKnight(here) ? here : Steam.Find(libraries, "Hollow Knight", Steam.IsHollowKnight) ?? "";
            if (terraria.Text.Length == 0)
            {
                string beside = Steam.IsHollowKnight(hollow.Text) ? Path.Combine(Path.GetDirectoryName(hollow.Text.TrimEnd('\\')) ?? "", "Terraria") : null;
                terraria.Text = Steam.IsTerraria(beside) ? beside : Steam.Find(libraries, "Terraria", Steam.IsTerraria) ?? "";
            }
            Say(hollow.Text.Length > 0 ? "空洞骑士：" + hollow.Text : "没找到空洞骑士，请点“浏览…”选择它的游戏目录。");
            Say(terraria.Text.Length > 0 ? "泰拉瑞亚：" + terraria.Text : "没找到泰拉瑞亚，请点“浏览…”选择它的游戏目录。");
        }

        static bool Elevated()
        {
            using (var id = WindowsIdentity.GetCurrent()) return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        static bool Writable(string dir)
        {
            try
            {
                var probe = Path.Combine(dir, ".terraknight-write-test");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch (UnauthorizedAccessException) { return false; }
            catch (IOException) { return false; }
        }

        void Install()
        {
            string hk = hollow.Text.Trim().Trim('"'), tr = terraria.Text.Trim().Trim('"');
            if (!Steam.IsHollowKnight(hk))
            {
                Tell("这里没有 hollow_knight.exe，请选择空洞骑士的游戏目录。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!Steam.IsTerraria(tr))
            {
                Tell("没找到泰拉瑞亚（选择的目录里要有 Content\\Images）。空洞泰拉读取本机泰拉瑞亚的贴图、音效和文字，请先在 Steam 安装泰拉瑞亚，或选择它的游戏目录。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            foreach (var p in Process.GetProcessesByName("hollow_knight"))
            {
                string path = null;
                try { path = p.MainModule?.FileName; } catch (Exception) { }
                if (path != null && path.StartsWith(hk, StringComparison.OrdinalIgnoreCase))
                {
                    Tell("空洞骑士正在运行，请先关闭游戏再安装。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            if (!Writable(hk))
            {
                if (Elevated())
                {
                    Tell("无法写入游戏目录：" + hk, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                // The game folder is under Program Files or otherwise protected: once more as administrator.
                try
                {
                    Process.Start(new ProcessStartInfo(Application.ExecutablePath,
                        $"--hollow \"{hk}\" --terraria \"{tr}\" --shortcut {(shortcut.Checked ? 1 : 0)} --auto") { Verb = "runas", UseShellExecute = true });
                    Close();
                }
                catch (Exception) { Say("需要管理员权限才能写入游戏目录，已取消。"); }
                return;
            }
            install.Enabled = false;
            try
            {
                Unpack(hk);
                PointAtTerraria(hk, tr);
                if (shortcut.Checked) MakeShortcut(hk);
                Conflicts(hk);
                Say("安装完成。");
                Tell("安装完成！\n\n以后双击游戏目录里的“空洞泰拉.bat”" + (shortcut.Checked ? "（或桌面上的“空洞泰拉”）" : "") + "开始游戏。\n从 Steam 直接启动的仍是原版空洞骑士。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception e)
            {
                Say("安装失败：" + e.Message);
                Tell("安装失败：" + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                install.Enabled = true;
            }
        }

        /// <summary>The release's files into the game folder (BepInEx, the mod, the launcher), replacing older ones.</summary>
        void Unpack(string hk)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            {
                if (stream == null) throw new InvalidOperationException("安装器里没有安装文件。");
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    int n = 0;
                    string root = Path.GetFullPath(hk).TrimEnd('\\') + "\\";
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.FullName.EndsWith("/")) continue;
                        string to = Path.GetFullPath(Path.Combine(hk, entry.FullName.Replace('/', '\\')));
                        if (!to.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(to));
                        entry.ExtractToFile(to, true);
                        n++;
                    }
                    Say("已写入 " + n + " 个文件（BepInEx 框架、空洞泰拉、启动器）。");
                    // An older install's launcher script beside the game: its new place is BepInEx\TerraKnight.
                    var old = Path.Combine(hk, "terraknight-launch.ps1");
                    if (File.Exists(old) && File.Exists(Path.Combine(hk, "BepInEx", "TerraKnight", "terraknight-launch.ps1"))) File.Delete(old);
                }
            }
        }

        /// <summary>The mod's setting for Terraria's folder (BepInEx's config file: written new or its line replaced).</summary>
        void PointAtTerraria(string hk, string tr)
        {
            var dir = Path.Combine(hk, "BepInEx", "config");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, ConfigName);
            string line = "TerrariaDir = " + tr;
            if (!File.Exists(file))
            {
                File.WriteAllText(file, "[Paths]\r\n\r\n" + line + "\r\n", new UTF8Encoding(false));
            }
            else
            {
                var text = File.ReadAllText(file);
                if (Regex.IsMatch(text, @"(?m)^TerrariaDir\s*=.*$")) text = Regex.Replace(text, @"(?m)^TerrariaDir\s*=.*$", line.Replace("$", "$$"));
                else if (Regex.IsMatch(text, @"(?m)^\[Paths\]\s*$")) text = Regex.Replace(text, @"(?m)^\[Paths\]\s*$", "[Paths]\r\n\r\n" + line.Replace("$", "$$"));
                else text = text.TrimEnd() + "\r\n\r\n[Paths]\r\n\r\n" + line + "\r\n";
                File.WriteAllText(file, text, new UTF8Encoding(false));
            }
            Say("泰拉瑞亚的位置已写入设置：" + tr);
        }

        void MakeShortcut(string hk)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { Path.Combine(desktop, "空洞泰拉.lnk") });
                link.TargetPath = Path.Combine(hk, "空洞泰拉.bat");
                link.WorkingDirectory = hk;
                link.IconLocation = Path.Combine(hk, "hollow_knight.exe") + ",0";
                link.Description = "空洞泰拉 TerraKnight";
                link.WindowStyle = 7;
                link.Save();
                Say("桌面快捷方式已创建。");
            }
            catch (Exception e) { Say("没能创建桌面快捷方式（" + e.Message + "），请直接双击游戏目录里的 空洞泰拉.bat。"); }
        }

        /// <summary>The Hollow Knight Modding API's MonoMod in the game's Managed folder stops BepInEx: said, with what to do.</summary>
        void Conflicts(string hk)
        {
            var managed = Path.Combine(hk, "hollow_knight_Data", "Managed");
            if (!Directory.Exists(managed)) return;
            var bad = new[] { "MonoMod*.dll", "Mono.Cecil*.dll", "MMHOOK_*.dll", "0Harmony*.dll" }.SelectMany(p => Directory.GetFiles(managed, p)).Select(Path.GetFileName).ToArray();
            if (bad.Length == 0) return;
            Say("注意：检测到空洞骑士的 Modding API（" + string.Join("、", bad) + "），它和 BepInEx 冲突。");
            Tell("检测到空洞骑士的 Modding API（Scarab、Lumafly 等模组管理器安装的）。它放进 hollow_knight_Data\\Managed 的这些文件会让 BepInEx 启动失败：\n\n" + string.Join("、", bad) +
                "\n\n请在模组管理器里关闭 Modding API；或者在 Steam 里对空洞骑士“验证游戏文件的完整性”，再删掉上面这些文件。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
