using BepInEx.Configuration;
using TerraKnight.Player;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// "泰拉瑞亚设置", the inventory's second button beside the game's Settings: Terraria's in-game
    /// settings (IngameOptions) for what the mod adds. General: the "Autoswing" switch
    /// (UI.AutoReuseAllOn / Off, Main.SettingsEnabled_AutoReuseAllItems: every weapon goes on being
    /// used while the button is held) and the enemy health bars; in a LAN game where it is, who is
    /// in it and a way out. Keybindings (Terraria's Controls menu): every Terraria action with its
    /// key, a click and a key press to change one (Escape keeps it), "Reset to Default". Choices are
    /// saved in the mod's config. The mod's version stands in gold at the top right. Escape or a
    /// click outside closes it.
    /// </summary>
    internal static class IngameOptions
    {
        public static bool Open { get; private set; }

        static ConfigEntry<bool> autoReuseSetting, barsSetting, cameraSetting;
        static GUIStyle row, head, keyStyle, section, version;
        static int openedFrame;
        static float openedAt;

        public static void Bind(ConfigEntry<bool> autoReuse, ConfigEntry<bool> bars, ConfigEntry<bool> camera)
        {
            autoReuseSetting = autoReuse;
            barsSetting = bars;
            cameraSetting = camera;
            Items.Equipment.AutoReuseAll = autoReuse.Value;
            EnemyHealthBars.Always = bars.Value;
            TerraCamera.Enabled = camera.Value;
        }

        /// <summary>The camera lock: Terraria's view (the player held in the middle of the screen, TerraCamera), or the game's own.</summary>
        public static void SetCameraLock(bool on)
        {
            TerraCamera.Enabled = on;
            if (cameraSetting != null) cameraSetting.Value = on;
            Plugin.Log.LogInfo($"[设置] 镜头锁：{(on ? "开" : "关")}");
        }

        public static void Show()
        {
            Open = true;
            openedFrame = Time.frameCount;
            openedAt = Time.unscaledTime;
            TerraMod.Instance?.PlaySound("Menu_Open", 0.7f);
        }

        public static void Hide()
        {
            if (!Open) return;
            Open = false;
            if (TerraKeys.Listening.HasValue) TerraKeys.StopListening();
            TerraMod.Instance?.PlaySound("Menu_Close", 0.7f);
        }

        /// <summary>Dev: flips the autoswing switch as a click on it would.</summary>
        public static void DevToggleAutoReuse() => SetAutoReuse(!Items.Equipment.AutoReuseAll);

        static void SetAutoReuse(bool on)
        {
            Items.Equipment.AutoReuseAll = on;
            if (autoReuseSetting != null) autoReuseSetting.Value = on;
            Plugin.Log.LogInfo($"[设置] 自动开火：{(on ? "开" : "关")}");
        }

        static string TT(string key, string zh) => TerrariaText.Get(Lang.Culture, key) ?? Lang.T(zh);

        /// <summary>Dev: where the panel is (GUI rect).</summary>
        public static Rect DevPanel => Panel(GameView.Scale);

        static Rect Panel(float ui)
        {
            var view = GameView.Rect;
            float w = Mathf.Min(1180f * ui, view.width - 40f * ui), h = Mathf.Min(800f * ui, view.height - 40f * ui);
            return new Rect(view.center.x - w / 2f, view.center.y - h / 2f, w, h);
        }

        static readonly Color Gold = new Color(1f, 0.86f, 0.3f);

        public static void Draw()
        {
            if (!Open) return;
            var gm = GameManager.UnsafeInstance;
            if (gm == null || !GameHooks.IsGameplay(gm) || gm.isPaused)
            {
                Hide();
                return;
            }
            var e = Event.current;
            float ui = GameView.Scale;
            CreativeMenu.Styles(ui);
            if (row == null)
            {
                row = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                head = new GUIStyle(row);
                section = new GUIStyle(row) { alignment = TextAnchor.MiddleLeft };
                keyStyle = new GUIStyle(row) { alignment = TextAnchor.MiddleLeft };
                version = new GUIStyle(row) { alignment = TextAnchor.MiddleRight };
            }
            row.fontSize = Mathf.RoundToInt(24 * ui);
            head.fontSize = Mathf.RoundToInt(32 * ui);
            section.fontSize = Mathf.RoundToInt(26 * ui);
            keyStyle.fontSize = Mathf.RoundToInt(20 * ui);
            version.fontSize = Mathf.RoundToInt(28 * ui);
            var p = Panel(ui);
            bool choosing = TerraKeys.Listening.HasValue || TerraKeys.Busy;
            // The click (or key) that opened it may reach the GUI a frame or two later: none counts for 0.3 s.
            bool settled = Time.frameCount != openedFrame && Time.unscaledTime - openedAt > 0.3f;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && !choosing && settled)
            {
                e.Use();
                Hide();
                return;
            }
            if (e.type == EventType.MouseDown && !p.Contains(TerraCursor.GuiMouse()) && settled)
            {
                e.Use();
                Hide();
                return;
            }
            CreativeMenu.Fill(p, new Color(0.13f, 0.17f, 0.42f, 0.9f));
            CreativeMenu.Rim(p, new Color(0.05f, 0.05f, 0.12f, 0.95f), 3f * ui);
            CreativeMenu.Shadowed(new Rect(p.x, p.y + 12f * ui, p.width, 44f * ui), Lang.T("泰拉瑞亚设置"), head, Color.white);
            // The mod's version, in gold at the top right, so it is seen at a glance.
            CreativeMenu.Shadowed(new Rect(p.xMax - 430f * ui, p.y + 12f * ui, 400f * ui, 44f * ui), Lang.F("空洞泰拉 v{0}", Plugin.Version), version, Gold);

            // General, down the left.
            float lx = p.x + 30f * ui, lw = 380f * ui, y = p.y + 70f * ui;
            // LegacyMenu.114: "General"
            CreativeMenu.Shadowed(new Rect(lx, y, lw, 34f * ui), TT("LegacyMenu.114", "常规"), section, new Color(0.75f, 0.85f, 1f));
            y += 44f * ui;
            if (Toggle(new Rect(lx, y, lw, 44f * ui), TT(Items.Equipment.AutoReuseAll ? "UI.AutoReuseAllOn" : "UI.AutoReuseAllOff", Items.Equipment.AutoReuseAll ? "自动开火: 开" : "自动开火: 关"), ui))
                SetAutoReuse(!Items.Equipment.AutoReuseAll);
            CreativeMenu.Shadowed(new Rect(lx, y + 42f * ui, lw, 46f * ui), Lang.T("所有武器按住鼠标就一直挥舞、射击（泰拉瑞亚的自动挥舞）"), CreativeMenu.text, new Color(0.85f, 0.85f, 0.95f, 0.85f));
            y += 96f * ui;
            if (Toggle(new Rect(lx, y, lw, 44f * ui), EnemyHealthBars.Always ? Lang.T("敌人血条：一直显示") : Lang.T("敌人血条：受伤后显示"), ui))
            {
                EnemyHealthBars.Always = !EnemyHealthBars.Always;
                if (barsSetting != null) barsSetting.Value = EnemyHealthBars.Always;
            }
            y += 64f * ui;
            if (Toggle(new Rect(lx, y, lw, 44f * ui), TerraCamera.Enabled ? Lang.T("镜头锁：开") : Lang.T("镜头锁：关"), ui))
                SetCameraLock(!TerraCamera.Enabled);
            CreativeMenu.Shadowed(new Rect(lx, y + 42f * ui, lw, 46f * ui), Lang.T("开：泰拉瑞亚的视角，人物始终在画面正中；关：空洞骑士原版镜头"), CreativeMenu.text, new Color(0.85f, 0.85f, 0.95f, 0.85f));
            y += 96f * ui;
            if (Net.NetSession.Active)
            {
                CreativeMenu.Shadowed(new Rect(lx, y, lw, 34f * ui), TT("LegacyMenu.13", "多人模式"), section, new Color(0.75f, 0.85f, 1f));
                y += 40f * ui;
                string where = Net.NetSession.Hosting
                    ? Lang.F("你是主机（端口 {0}），{1} 人在线", Net.NetSession.Port, Net.NetSession.Players.Count + 1)
                    : Lang.F("已连接到 {0}:{1}，{2} 人在线，延迟 {3} 毫秒", Net.NetSession.Address, Net.NetSession.Port, Net.NetSession.Players.Count + 1, Mathf.RoundToInt(Net.NetSession.PingMs));
                CreativeMenu.Shadowed(new Rect(lx, y, lw, 52f * ui), where, CreativeMenu.text, new Color(0.8f, 0.95f, 1f));
                y += 56f * ui;
                // LegacyMenu.2: "Disconnect"
                if (Toggle(new Rect(lx, y, lw, 44f * ui), TT("LegacyMenu.2", "断开连接"), ui))
                {
                    Net.NetSession.Leave(Net.NetText.Mod("你离开了多人游戏"));
                    Net.NetChat.Add(Lang.T("你离开了多人游戏"), new Color(1f, 0.94f, 0.08f));
                }
            }

            // Keybindings, on the right: two columns of Terraria's actions.
            float rx = lx + lw + 30f * ui, rw = p.xMax - 30f * ui - rx, ry = p.y + 70f * ui;
            CreativeMenu.Fill(new Rect(rx - 15f * ui, ry, 2f * ui, p.yMax - 100f * ui - ry), new Color(0.05f, 0.05f, 0.12f, 0.7f));
            // UI.Keybindings: "Keybindings"
            CreativeMenu.Shadowed(new Rect(rx, ry, rw, 34f * ui), TT("UI.Keybindings", "按键绑定"), section, new Color(0.75f, 0.85f, 1f));
            ry += 44f * ui;
            var all = TerraKeys.Ordered;
            int columns = all.Length > 30 ? 3 : 2;
            // Three columns: a size smaller, each on one line.
            keyStyle.fontSize = Mathf.RoundToInt((columns == 3 ? 17 : 20) * ui);
            keyStyle.wordWrap = false;
            int perColumn = (all.Length + columns - 1) / columns;
            float colW = (rw - 20f * (columns - 1) * ui) / columns, rh = Mathf.Min(38f * ui, (p.yMax - 200f * ui - ry) / perColumn);
            var mouse = TerraCursor.GuiMouse();
            for (int i = 0; i < all.Length; i++)
            {
                var a = all[i];
                var r = new Rect(rx + (i / perColumn) * (colW + 20f * ui), ry + (i % perColumn) * rh, colW, rh - 2f * ui);
                bool over = r.Contains(mouse);
                bool listening = TerraKeys.Listening == a;
                CreativeMenu.Fill(r, listening ? new Color(1f, 0.85f, 0.3f, 0.25f) : over ? new Color(1f, 1f, 1f, 0.1f) : new Color(0f, 0f, 0f, 0.25f));
                CreativeMenu.Shadowed(new Rect(r.x + 8f * ui, r.y, r.width * 0.58f, r.height), TerraKeys.Name(a), keyStyle, over ? Gold : Color.white);
                string key = listening ? Lang.T("按下新按键……") : TerraKeys.KeyName(TerraKeys.Get(a));
                var keyColor = listening ? Gold : TerraKeys.Clashes(a) ? new Color(1f, 0.45f, 0.4f) : new Color(0.8f, 0.9f, 1f);
                CreativeMenu.Shadowed(new Rect(r.x + r.width * 0.6f, r.y, r.width * 0.4f - 6f * ui, r.height), key, keyStyle, keyColor);
                if (e.type == EventType.MouseDown && e.button == 0 && over)
                {
                    e.Use();
                    TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
                    TerraKeys.Listen(a);
                }
            }
            // UIManageControls sp20: Double Tap Dashing, on or off.
            if (CreativeMenu.Button(new Rect(rx, p.yMax - 190f * ui, 300f * ui, 44f * ui), Lang.T("双击冲刺") + "：" + Lang.T(TerraKeys.DoubleTapDash ? "开" : "关"), ui))
            {
                TerraKeys.DoubleTapDash = !TerraKeys.DoubleTapDash;
                TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
            }
            // LegacyMenu.86: "Reset to Default"
            if (CreativeMenu.Button(new Rect(rx, p.yMax - 140f * ui, 300f * ui, 44f * ui), TT("LegacyMenu.86", "重置为默认"), ui))
            {
                TerraKeys.ResetAll();
                if (TerraKeys.Listening.HasValue) TerraKeys.StopListening();
            }
            CreativeMenu.Shadowed(new Rect(rx + 320f * ui, p.yMax - 140f * ui, rw - 320f * ui, 44f * ui),
                Lang.T("点一个动作，再按新的键（Esc 取消，再按一次原来的键是解除绑定）。红色表示有几个动作用了同一个键。"), CreativeMenu.text, new Color(0.85f, 0.85f, 0.95f, 0.85f));

            // LegacyMenu.5: "Back"
            if (CreativeMenu.Button(new Rect(p.center.x - 130f * ui, p.yMax - 76f * ui, 260f * ui, 50f * ui), TT("LegacyMenu.5", "返回"), ui)) Hide();
        }

        /// <summary>A line to click, Terraria's way: bigger and yellow under the mouse.</summary>
        static bool Toggle(Rect r, string text, float ui)
        {
            bool over = r.Contains(TerraCursor.GuiMouse());
            row.fontSize = Mathf.RoundToInt((over ? 27 : 24) * ui);
            CreativeMenu.Shadowed(r, text, row, over ? Gold : Color.white);
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || !over) return false;
            e.Use();
            TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
            return true;
        }

        /// <summary>The panel is up: clicks are its own, not a use.</summary>
        public static bool MouseOver => Open;
    }
}
