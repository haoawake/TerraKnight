using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight.Net
{
    /// <summary>
    /// Terraria's multiplayer menu on the save profiles screen ("多人模式" in its bottom left, the
    /// creative button's twin): the player's name and look, then "Host &amp; Play" (the password,
    /// blank for none, and the port) or "Join via IP" (the address, then the port), Terraria's
    /// status lines while it connects, the password when the server asks. Once in, a banner along
    /// the top says where the game is and the save profiles are free again: whichever save is
    /// chosen is the character this player brings, as a Terraria player is.
    /// </summary>
    internal static class NetMenu
    {
        enum Step { Closed, Main, Host, Join, Busy, Password }

        static Step step;
        static string nameField, portField, addressField, joinPortField, passwordField = "", hostPassword = "";
        static int maxPlayers;
        static string focusNext;
        static GUIStyle field, label;

        public static string DevStep => step.ToString();

        /// <summary>A panel of this menu is open (the save pager hides under it).</summary>
        public static bool IsOpen => step != Step.Closed;

        /// <summary>A step with text boxes is open: the keys type (the game's own menu doesn't hear them).</summary>
        public static bool TextFocused => step == Step.Main || step == Step.Host || step == Step.Join || step == Step.Password;

        /// <summary>A line in the language being played: Terraria's by key when it has one, else the mod's.</summary>
        static string TT(string key, string zh) => TerrariaText.Get(Lang.Culture, key) ?? Lang.T(zh);

        static void Open(Step s)
        {
            if (step == Step.Closed) CreativeMenu.Block(true);
            step = s;
            focusNext = s == Step.Main ? "tk_name" : s == Step.Host ? "tk_hostpw" : s == Step.Join ? "tk_addr" : s == Step.Password ? "tk_pw" : null;
        }

        static void Close()
        {
            if (step != Step.Closed) CreativeMenu.Block(false);
            step = Step.Closed;
        }

        static void Styles(float ui)
        {
            CreativeMenu.Styles(ui);
            if (field == null)
            {
                field = new GUIStyle(GUI.skin.textField) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                field.normal.background = field.focused.background = field.hover.background = field.active.background = null;
                field.normal.textColor = field.focused.textColor = field.hover.textColor = Color.white;
                label = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            }
            field.fontSize = Mathf.RoundToInt(22 * ui);
            label.fontSize = Mathf.RoundToInt(20 * ui);
        }

        /// <summary>From TerraMod.OnGUI with the creative menu (main menu only).</summary>
        public static void Draw()
        {
            if (!CreativeMenu.InMainMenu())
            {
                if (step != Step.Closed) Close();
                return;
            }
            float ui = Screen.height / 1080f;
            Styles(ui);
            // A step waiting on the connection follows it.
            if (step == Step.Busy || step == Step.Password)
            {
                if (NetSession.State == NetSession.Phase.NeedPassword && step != Step.Password)
                {
                    passwordField = "";
                    Open(Step.Password);
                }
                else if (NetSession.State == NetSession.Phase.Ready) Close();
                else if (NetSession.State == NetSession.Phase.Off) Open(Step.Main);
            }
            bool profiles = CreativeMenu.ProfilesShown();
            if (step == Step.Closed)
            {
                if (NetSession.State != NetSession.Phase.Off) DrawBanner(ui);
                else if (profiles)
                {
                    var r = new Rect(60f * ui, Screen.height - 120f * ui, 240f * ui, 58f * ui);
                    if (CreativeMenu.Button(r, TT("LegacyMenu.13", "多人模式"), ui, CreativeMenu.Gold)) OpenMain();
                }
                return;
            }
            if (!profiles && step == Step.Main)
            {
                Close();
                return;
            }
            CreativeMenu.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));
            float w = 760f * ui, h = 560f * ui;
            var p = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            CreativeMenu.Fill(p, CreativeMenu.Back);
            CreativeMenu.Rim(p, CreativeMenu.Line, 2f * ui);
            CreativeMenu.Rim(new Rect(p.x + 8f * ui, p.y + 8f * ui, w - 16f * ui, h - 16f * ui), new Color(CreativeMenu.Line.r, CreativeMenu.Line.g, CreativeMenu.Line.b, 0.35f), 1f * ui);
            string head = step == Step.Host ? TT("LegacyMenu.88", "开服并开始游戏") : step == Step.Join ? TT("LegacyMenu.146", "通过IP加入") : TT("LegacyMenu.13", "多人模式");
            CreativeMenu.Shadowed(new Rect(p.x, p.y + 24f * ui, w, 40f * ui), head, CreativeMenu.title, CreativeMenu.Gold);
            switch (step)
            {
                case Step.Main: DrawMain(p, ui); break;
                case Step.Host: DrawHost(p, ui); break;
                case Step.Join: DrawJoin(p, ui); break;
                case Step.Busy: DrawBusy(p, ui); break;
                case Step.Password: DrawPassword(p, ui); break;
            }
            if (focusNext != null && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl(focusNext);
                focusNext = null;
            }
        }

        static void OpenMain()
        {
            nameField = NetSettings.Name.Value;
            if (string.IsNullOrEmpty(nameField)) nameField = NetSettings.PlayerName;
            portField = NetSettings.Port.Value.ToString();
            joinPortField = portField;
            addressField = NetSettings.LastAddress.Value;
            maxPlayers = Mathf.Clamp(NetSettings.MaxPlayers.Value, 1, Server.MaxSlots);
            Open(Step.Main);
        }

        /// <summary>A labelled text box; Enter in it does what the step's main button does.</summary>
        static string Field(Rect row, string caption, string value, string control, int max, float ui, float labelWidth = 230f, bool masked = false)
        {
            CreativeMenu.Shadowed(new Rect(row.x, row.y, labelWidth * ui, row.height), caption, label, new Color(0.88f, 0.88f, 0.92f));
            var box = new Rect(row.x + labelWidth * ui, row.y, row.width - labelWidth * ui, row.height);
            bool focused = GUI.GetNameOfFocusedControl() == control;
            CreativeMenu.Fill(box, new Color(0f, 0f, 0f, 0.6f));
            CreativeMenu.Rim(box, focused ? CreativeMenu.Gold : CreativeMenu.Line, (focused ? 2f : 1.5f) * ui);
            GUI.SetNextControlName(control);
            var inner = new Rect(box.x + 10f * ui, box.y, box.width - 20f * ui, box.height);
            return masked ? GUI.PasswordField(inner, value ?? "", '*', max, field) : GUI.TextField(inner, value ?? "", max, field);
        }

        /// <summary>Enter pressed in a text box this frame (used up).</summary>
        static bool Submitted()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter)) return false;
            e.Use();
            return true;
        }

        static bool Escaped()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Escape) return false;
            e.Use();
            return true;
        }

        static void DrawEnded(Rect p, float ui)
        {
            if (NetSession.Ended == null) return;
            CreativeMenu.Shadowed(new Rect(p.x + 48f * ui, p.yMax - 130f * ui, p.width - 96f * ui, 44f * ui), NetSession.Ended.ToString(), CreativeMenu.text, new Color(1f, 0.6f, 0.55f));
        }

        static void DrawMain(Rect p, float ui)
        {
            Submitted();
            bool esc = Escaped();
            CreativeMenu.Shadowed(new Rect(p.x + 48f * ui, p.y + 78f * ui, p.width - 96f * ui, 60f * ui),
                Lang.T("和局域网里的朋友一起玩：一人开服，其他人用他的 IP 地址加入。每个人带着自己选的存档进入，在同一个场景里能看到彼此、并肩作战。"), CreativeMenu.text, new Color(0.9f, 0.9f, 0.9f));
            float x = p.x + 70f * ui, rw = p.width - 140f * ui;
            nameField = Field(new Rect(x, p.y + 150f * ui, rw, 48f * ui), TT("LegacyMenu.45", "输入人物名称：").TrimEnd(':', '：'), nameField, "tk_name", 20, ui);
            // The look: Terraria's default character, or one of the other colourings.
            var lookRow = new Rect(x, p.y + 212f * ui, rw, 48f * ui);
            CreativeMenu.Shadowed(new Rect(lookRow.x, lookRow.y, 230f * ui, lookRow.height), Lang.T("外观"), label, new Color(0.88f, 0.88f, 0.92f));
            int look = Mathf.Clamp(NetSettings.Look.Value, 0, Player.TerraPlayerSprite.Looks.Length - 1);
            if (CreativeMenu.Button(new Rect(lookRow.x + 230f * ui, lookRow.y, 48f * ui, 48f * ui), "<", ui)) look = (look + Player.TerraPlayerSprite.Looks.Length - 1) % Player.TerraPlayerSprite.Looks.Length;
            DrawSwatch(new Rect(lookRow.x + 290f * ui, lookRow.y, rw - 360f * ui, 48f * ui), Player.TerraPlayerSprite.Looks[look], look, ui);
            if (CreativeMenu.Button(new Rect(lookRow.xMax - 58f * ui, lookRow.y, 48f * ui, 48f * ui), ">", ui)) look = (look + 1) % Player.TerraPlayerSprite.Looks.Length;
            if (look != NetSettings.Look.Value) NetSettings.Look.Value = look;
            float bw = 300f * ui, by = p.y + 290f * ui;
            if (CreativeMenu.Button(new Rect(p.center.x - bw - 10f * ui, by, bw, 56f * ui), TT("LegacyMenu.88", "开服并开始游戏"), ui))
            {
                SaveName();
                hostPassword = "";
                Open(Step.Host);
            }
            if (CreativeMenu.Button(new Rect(p.center.x + 10f * ui, by, bw, 56f * ui), TT("LegacyMenu.146", "通过IP加入"), ui))
            {
                SaveName();
                Open(Step.Join);
            }
            DrawEnded(p, ui);
            if (CreativeMenu.Button(new Rect(p.center.x - 100f * ui, p.yMax - 78f * ui, 200f * ui, 50f * ui), TT("LegacyMenu.5", "返回"), ui) || esc) Close();
        }

        static void SaveName()
        {
            string n = (nameField ?? "").Trim();
            NetSettings.Name.Value = n;
        }

        static void DrawSwatch(Rect r, Player.TerraPlayerSprite.Palette pal, int look, float ui)
        {
            CreativeMenu.Fill(r, new Color(0f, 0f, 0f, 0.6f));
            CreativeMenu.Rim(r, CreativeMenu.Line, 1.5f * ui);
            var parts = new[] { pal.Hair, pal.Skin, pal.Shirt, pal.UnderShirt, pal.Pants, pal.Shoe };
            float s = r.height - 24f * ui, gap = 5f * ui, x = r.x + 10f * ui;
            foreach (var c in parts)
            {
                CreativeMenu.Fill(new Rect(x, r.y + 12f * ui, s, s), c);
                x += s + gap;
            }
            CreativeMenu.Shadowed(new Rect(x + 6f * ui, r.y, r.xMax - x - 6f * ui, r.height), look == 0 ? Lang.T("默认") : Lang.F("配色 {0}", look), CreativeMenu.small, Color.white);
        }

        static void DrawHost(Rect p, float ui)
        {
            bool enter = Submitted(), esc = Escaped();
            float x = p.x + 70f * ui, rw = p.width - 140f * ui;
            CreativeMenu.Shadowed(new Rect(x, p.y + 82f * ui, rw, 40f * ui), Lang.T("服务器就开在这台电脑上，朋友用你的 IP 地址和端口加入。留空密码则任何人都能进。"), CreativeMenu.text, new Color(0.9f, 0.9f, 0.9f));
            hostPassword = Field(new Rect(x, p.y + 150f * ui, rw, 48f * ui), TT("LegacyMenu.7", "输入服务器密码：").TrimEnd(':', '：'), hostPassword, "tk_hostpw", 40, ui, 260f);
            portField = Field(new Rect(x, p.y + 212f * ui, rw, 48f * ui), Lang.T("端口"), portField, "tk_port", 5, ui, 260f);
            var maxRow = new Rect(x, p.y + 274f * ui, rw, 48f * ui);
            CreativeMenu.Shadowed(new Rect(maxRow.x, maxRow.y, 260f * ui, maxRow.height), Lang.T("最多玩家"), label, new Color(0.88f, 0.88f, 0.92f));
            if (CreativeMenu.Button(new Rect(maxRow.x + 260f * ui, maxRow.y, 48f * ui, 48f * ui), "-", ui)) maxPlayers = Mathf.Max(2, maxPlayers - 1);
            CreativeMenu.Shadowed(new Rect(maxRow.x + 318f * ui, maxRow.y, 80f * ui, 48f * ui), maxPlayers.ToString(), CreativeMenu.button, Color.white);
            if (CreativeMenu.Button(new Rect(maxRow.x + 408f * ui, maxRow.y, 48f * ui, 48f * ui), "+", ui)) maxPlayers = Mathf.Min(Server.MaxSlots, maxPlayers + 1);
            var ips = Server.LocalAddresses();
            if (ips.Count > 0)
                CreativeMenu.Shadowed(new Rect(x, p.y + 336f * ui, rw, 30f * ui), Lang.F("本机局域网地址：{0}", string.Join("，", ips)), CreativeMenu.text, new Color(0.75f, 0.9f, 1f));
            DrawEnded(p, ui);
            float bw = 220f * ui;
            // LegacyMenu.144: "Start"
            if (CreativeMenu.Button(new Rect(p.center.x - bw - 10f * ui, p.yMax - 78f * ui, bw, 50f * ui), TT("LegacyMenu.144", "开始"), ui, CreativeMenu.Gold) || enter) StartHost();
            if (CreativeMenu.Button(new Rect(p.center.x + 10f * ui, p.yMax - 78f * ui, bw, 50f * ui), TT("LegacyMenu.5", "返回"), ui) || esc) Open(Step.Main);
        }

        static bool ParsePort(string s, out int port) => int.TryParse((s ?? "").Trim(), out port) && port > 0 && port < 65536;

        static void StartHost()
        {
            if (!ParsePort(portField, out int port)) port = Server.DefaultPort;
            NetSettings.Port.Value = port;
            NetSettings.MaxPlayers.Value = maxPlayers;
            if (NetSession.Host(port, hostPassword, maxPlayers, out string error)) Open(Step.Busy);
            else Plugin.Log.LogWarning($"[联机] {error}");
        }

        static void DrawJoin(Rect p, float ui)
        {
            bool enter = Submitted(), esc = Escaped();
            float x = p.x + 70f * ui, rw = p.width - 140f * ui;
            CreativeMenu.Shadowed(new Rect(x, p.y + 82f * ui, rw, 40f * ui), Lang.T("填开服那台电脑的局域网 IP 地址（开服的人在他的多人模式里能看到）。"), CreativeMenu.text, new Color(0.9f, 0.9f, 0.9f));
            addressField = Field(new Rect(x, p.y + 150f * ui, rw, 48f * ui), TT("LegacyMenu.89", "输入服务器IP地址:").TrimEnd(':', '：'), addressField, "tk_addr", 64, ui, 260f);
            joinPortField = Field(new Rect(x, p.y + 212f * ui, rw, 48f * ui), TT("LegacyMenu.90", "输入服务器端口:").TrimEnd(':', '：'), joinPortField, "tk_jport", 5, ui, 260f);
            DrawEnded(p, ui);
            float bw = 220f * ui;
            // LegacyMenu.87: "Join"
            if (CreativeMenu.Button(new Rect(p.center.x - bw - 10f * ui, p.yMax - 78f * ui, bw, 50f * ui), TT("LegacyMenu.87", "加入"), ui, CreativeMenu.Gold) || enter) StartJoin();
            if (CreativeMenu.Button(new Rect(p.center.x + 10f * ui, p.yMax - 78f * ui, bw, 50f * ui), TT("LegacyMenu.5", "返回"), ui) || esc) Open(Step.Main);
        }

        static void StartJoin()
        {
            string address = (addressField ?? "").Trim();
            if (address.Length == 0) return;
            if (!ParsePort(joinPortField, out int port)) port = Server.DefaultPort;
            NetSettings.LastAddress.Value = address;
            NetSettings.Port.Value = port;
            NetSession.Join(address, port);
            Open(Step.Busy);
        }

        static void DrawBusy(Rect p, float ui)
        {
            bool esc = Escaped();
            string status = NetSession.Status?.ToString() ?? "";
            CreativeMenu.Shadowed(new Rect(p.x, p.y + 220f * ui, p.width, 50f * ui), status, CreativeMenu.title, Color.white);
            // LegacyMenu.6: "Cancel"
            if (CreativeMenu.Button(new Rect(p.center.x - 100f * ui, p.yMax - 78f * ui, 200f * ui, 50f * ui), TT("LegacyMenu.6", "取消"), ui) || esc)
            {
                NetSession.Leave(null);
                Open(Step.Main);
            }
        }

        static void DrawPassword(Rect p, float ui)
        {
            bool enter = Submitted(), esc = Escaped();
            float x = p.x + 70f * ui, rw = p.width - 140f * ui;
            // LegacyMenu.3: "Server Requires Password:"
            CreativeMenu.Shadowed(new Rect(x, p.y + 110f * ui, rw, 40f * ui), TT("LegacyMenu.3", "服务器需要密码："), CreativeMenu.big, Color.white);
            passwordField = Field(new Rect(x, p.y + 180f * ui, rw, 48f * ui), Lang.T("密码"), passwordField, "tk_pw", 40, ui, 160f, masked: true);
            float bw = 220f * ui;
            // LegacyMenu.4: "Accept"
            if (CreativeMenu.Button(new Rect(p.center.x - bw - 10f * ui, p.yMax - 78f * ui, bw, 50f * ui), TT("LegacyMenu.4", "接受"), ui, CreativeMenu.Gold) || enter)
            {
                NetSession.SubmitPassword(passwordField);
                Open(Step.Busy);
            }
            if (CreativeMenu.Button(new Rect(p.center.x + 10f * ui, p.yMax - 78f * ui, bw, 50f * ui), TT("LegacyMenu.6", "取消"), ui) || esc)
            {
                NetSession.Leave(null);
                Open(Step.Main);
            }
        }

        /// <summary>In a game (or joining one) at the menu: where, and a way out.</summary>
        static void DrawBanner(float ui)
        {
            float w = 980f * ui, h = 64f * ui;
            var r = new Rect((Screen.width - w) / 2f, 18f * ui, w, h);
            CreativeMenu.Fill(r, CreativeMenu.Back);
            CreativeMenu.Rim(r, CreativeMenu.Line, 1.5f * ui);
            string where;
            if (NetSession.State != NetSession.Phase.Ready) where = NetSession.Status?.ToString() ?? "";
            else if (NetSession.Hosting)
            {
                var ips = Server.LocalAddresses();
                // LegacyMenu.1: "Running on port"
                where = Lang.F("服务器已开启，{0} {1}　本机地址：{2}　——　选择一个存档开始游戏", TT("LegacyMenu.1", "运行于端口"), NetSession.Port, ips.Count > 0 ? ips[0] : "127.0.0.1");
            }
            else where = Lang.F("已连接到 {0}:{1}　——　选择一个存档开始游戏", NetSession.Address, NetSession.Port);
            CreativeMenu.Shadowed(new Rect(r.x + 20f * ui, r.y, r.width - 240f * ui, r.height), where, CreativeMenu.small, Color.white);
            // LegacyMenu.2: "Disconnect"
            if (CreativeMenu.Button(new Rect(r.xMax - 210f * ui, r.y + 9f * ui, 196f * ui, 46f * ui), TT("LegacyMenu.2", "断开连接"), ui)) NetSession.Leave(null);
        }

        // ------------------------------------------------------------------ dev

        /// <summary>Dev: hosts or joins as the menu's buttons would (no typing).</summary>
        public static void DevHost(int port, string password) => NetSession.Host(port, password, Server.DefaultMaxPlayers, out _);
        public static void DevJoin(string address, int port) => NetSession.Join(address, port);
        public static void DevOpen() => OpenMain();
        public static void DevShowHost() { OpenMain(); Open(Step.Host); }
        public static void DevShowJoin() { OpenMain(); Open(Step.Join); }
        public static void DevClose() => Close();
    }
}
