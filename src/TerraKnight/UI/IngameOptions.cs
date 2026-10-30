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
    /// used while the button is held), the enemy health bars, the camera lock and Autopause (UI.AutoPause:
    /// the inventory stops a single-player game); in a LAN game where it is, who is
    /// in it and a way out. On the right, two tabs. Keybindings (Terraria's Controls menu): every Terraria action with its
    /// key, a click and a key press to change one (Escape keeps it), "Reset to Default". Cursor (Terraria's Cursor
    /// settings): Terraria's or Hollow Knight's picture, its size, colour, glow and pulse, with a preview. Choices are
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
            float w = Mathf.Min(1180f * ui, view.width - 40f * ui), h = Mathf.Min(910f * ui, view.height - 40f * ui);
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

            // General, down the left: a column of its own that scrolls with the wheel when it is longer than the panel (the
            // multiplayer rows under the others went past its foot).
            float lx0 = p.x + 30f * ui, lw = 380f * ui, top = p.y + 70f * ui;
            leftClip = new Rect(lx0 - 10f * ui, top, lw + 26f * ui, p.yMax - 20f * ui - top);
            float maxScroll = Mathf.Max(0f, leftContent - leftClip.height);
            if (e.type == EventType.Repaint && UnityEngine.InputSystem.Mouse.current != null && leftClip.Contains(TerraCursor.GuiMouse()))
            {
                float wheel = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y;
                if (wheel > 0.1f) leftScroll -= 60f * ui;
                else if (wheel < -0.1f) leftScroll += 60f * ui;
            }
            leftScroll = Mathf.Clamp(leftScroll, 0f, maxScroll);
            GUI.BeginGroup(leftClip);
            inLeft = true;
            float lx = 10f * ui, y = -leftScroll;
            // A toggle with a note under it, and one without: close enough that the column fits the panel.
            float noteRow = 88f * ui, plainRow = 56f * ui;
            // LegacyMenu.114: "General"
            CreativeMenu.Shadowed(new Rect(lx, y, lw, 34f * ui), TT("LegacyMenu.114", "常规"), section, new Color(0.75f, 0.85f, 1f));
            y += 44f * ui;
            if (Toggle(new Rect(lx, y, lw, 44f * ui), TT(Items.Equipment.AutoReuseAll ? "UI.AutoReuseAllOn" : "UI.AutoReuseAllOff", Items.Equipment.AutoReuseAll ? "自动开火: 开" : "自动开火: 关"), ui))
                SetAutoReuse(!Items.Equipment.AutoReuseAll);
            CreativeMenu.Shadowed(new Rect(lx, y + 42f * ui, lw, 46f * ui), Lang.T("所有武器按住鼠标就一直挥舞、射击（泰拉瑞亚的自动挥舞）"), CreativeMenu.text, new Color(0.85f, 0.85f, 0.95f, 0.85f));
            y += noteRow;
            if (Toggle(new Rect(lx, y, lw, 44f * ui), EnemyHealthBars.Always ? Lang.T("敌人血条：一直显示") : Lang.T("敌人血条：受伤后显示"), ui))
            {
                EnemyHealthBars.Always = !EnemyHealthBars.Always;
                if (barsSetting != null) barsSetting.Value = EnemyHealthBars.Always;
            }
            y += plainRow;
            if (Toggle(new Rect(lx, y, lw, 44f * ui), TerraCamera.Enabled ? Lang.T("镜头锁：开") : Lang.T("镜头锁：关"), ui))
                SetCameraLock(!TerraCamera.Enabled);
            CreativeMenu.Shadowed(new Rect(lx, y + 42f * ui, lw, 46f * ui), Lang.T("开：泰拉瑞亚的视角，人物始终在画面正中；关：空洞骑士原版镜头"), CreativeMenu.text, new Color(0.85f, 0.85f, 0.95f, 0.85f));
            y += noteRow;
            // LegacyMenu.69 / 70: "Autopause On" / "Autopause Off" (Main.autoPause).
            if (Toggle(new Rect(lx, y, lw, 44f * ui), TT(AutoPause.On ? "LegacyMenu.69" : "LegacyMenu.70", AutoPause.On ? "自动暂停: 开" : "自动暂停: 关"), ui))
                AutoPause.Set(!AutoPause.On);
            y += plainRow;
            // The nail and the Dream Nail swung at the mouse (Items.NailAim).
            if (Toggle(new Rect(lx, y, lw, 44f * ui), Items.NailAim.On ? Lang.T("骨钉朝鼠标劈砍：开") : Lang.T("骨钉朝鼠标劈砍：关"), ui))
                Items.NailAim.Set(!Items.NailAim.On);
            CreativeMenu.Shadowed(new Rect(lx, y + 42f * ui, lw, 46f * ui), Lang.T("开：骨钉和梦之钉朝鼠标方向挥砍（360 度），自身的后坐力仍按原版上下左右"), CreativeMenu.text, new Color(0.85f, 0.85f, 0.95f, 0.85f));
            y += noteRow;
            // This world's weapons (World.WeaponMode), chosen with it: shown, not switched.
            CreativeMenu.Shadowed(new Rect(lx, y, lw, 44f * ui), Lang.F("本世界的武器：{0}", World.WeaponMode.NameOf(World.WeaponMode.Current)), CreativeMenu.text, Color.white);
            CreativeMenu.Shadowed(new Rect(lx, y + 42f * ui, lw, 46f * ui), Lang.T("创建世界时选定，之后不能更改；以前的存档用原版武器"), CreativeMenu.text, new Color(0.85f, 0.85f, 0.95f, 0.85f));
            y += noteRow;
            // Hollow Knight's talk in the chat a line a second, or the game's own box (UI.DialogueBroadcast).
            if (Toggle(new Rect(lx, y, lw, 44f * ui), DialogueBroadcast.Enabled ? Lang.T("对话播报：开") : Lang.T("对话播报：关"), ui))
            {
                if (DialogueBroadcast.Setting != null) DialogueBroadcast.Setting.Value = !DialogueBroadcast.Enabled;
            }
            CreativeMenu.Shadowed(new Rect(lx, y + 42f * ui, lw, 46f * ui), Lang.T("开：角色对话和碑文每秒一句进左下角聊天框；关：原版对话框"), CreativeMenu.text, new Color(0.85f, 0.85f, 0.95f, 0.85f));
            y += noteRow;
            // Player.SmartCursorSettings.SmartBlocksEnabled: Terraria's own option, LegacyMenu.215 / 216.
            if (Toggle(new Rect(lx, y, lw, 44f * ui), Items.SmartCursor.SmartBlocks ? TT("LegacyMenu.215", "智能放置物块: 到光标处") : TT("LegacyMenu.216", "智能放置物块: 禁用"), ui))
                Items.SmartCursor.SetBlocks(!Items.SmartCursor.SmartBlocks);
            y += plainRow;
            // Terraria 1.4.5's Forward Zoom (UI.PanControlMode): the scopes' and the Sniper Rifle's push of the view.
            var pan = Player.TerraCamera.Panning;
            string panName = pan == Player.TerraCamera.PanMode.OnAlways ? TT("UI.PanControlMode_OnAlways", "始终开启")
                : pan == Player.TerraCamera.PanMode.OffAlways ? TT("UI.PanControlMode_OffAlways", "始终关闭")
                : pan == Player.TerraCamera.PanMode.Click ? TT("UI.PanControlMode_Click", "点击切换") : TT("UI.PanControlMode_Hold", "按住使用");
            if (Toggle(new Rect(lx, y, lw, 44f * ui), TT("UI.PanControlMode", "向前缩放") + ": " + panName, ui))
                Player.TerraCamera.CyclePan();
            CreativeMenu.Shadowed(new Rect(lx, y + 42f * ui, lw, 26f * ui), Lang.T("瞄准镜的右键视野；关掉不影响属性"), CreativeMenu.text, new Color(0.85f, 0.85f, 0.95f, 0.85f));
            y += 76f * ui;
            // How dug holes look (World.HoleShapes).
            if (Toggle(new Rect(lx, y, lw, 44f * ui), World.HoleShapes.Hollow ? Lang.T("挖洞样式：融合") : Lang.T("挖洞样式：泰拉瑞亚"), ui))
                World.HoleShapes.Set(!World.HoleShapes.Hollow);
            CreativeMenu.Shadowed(new Rect(lx, y + 42f * ui, lw, 46f * ui), Lang.T("融合：平滑洞口、墨线描边、洞内阴影，背后是调暗的泰拉瑞亚墙；泰拉瑞亚：方格洞口和原样的墙"), CreativeMenu.text, new Color(0.85f, 0.85f, 0.95f, 0.85f));
            y += noteRow;
            // Which map M opens (FullMap.Hollow): Hollow Knight's own, or Terraria's drawn from the world.
            if (WorldMap.Enabled)
            {
                if (Toggle(new Rect(lx, y, lw, 44f * ui), FullMap.Hollow ? Lang.T("全屏地图：空洞骑士") : Lang.T("全屏地图：泰拉瑞亚"), ui))
                    FullMap.SetHollow(!FullMap.Hollow);
                CreativeMenu.Shadowed(new Rect(lx, y + 42f * ui, lw, 46f * ui), Lang.T("空洞骑士：原版地图；泰拉瑞亚：按实际地形一格一格画。两种都标出晶塔、居民、幼虫和长椅"), CreativeMenu.text, new Color(0.85f, 0.85f, 0.95f, 0.85f));
                y += noteRow;
            }
            if (Net.NetSession.Active || DevShowNet)
            {
                CreativeMenu.Shadowed(new Rect(lx, y, lw, 34f * ui), TT("LegacyMenu.13", "多人模式"), section, new Color(0.75f, 0.85f, 1f));
                y += 40f * ui;
                string where = Net.NetSession.Hosting
                    ? Lang.F("你是主机（端口 {0}），{1} 人在线", Net.NetSession.Port, Net.NetSession.Players.Count + 1)
                    : Lang.F("已连接到 {0}:{1}，{2} 人在线，延迟 {3} 毫秒", Net.NetSession.Address, Net.NetSession.Port, Net.NetSession.Players.Count + 1, Mathf.RoundToInt(Net.NetSession.PingMs));
                CreativeMenu.Shadowed(new Rect(lx, y, lw, 52f * ui), where, CreativeMenu.text, new Color(0.8f, 0.95f, 1f));
                y += 56f * ui;
                // LegacyMenu.2: "Disconnect"
                DevDisconnectRect = new Rect(leftClip.x + lx, leftClip.y + y, lw, 44f * ui);
                if (Toggle(new Rect(lx, y, lw, 44f * ui), TT("LegacyMenu.2", "断开连接"), ui) && !DevShowNet)
                {
                    Net.NetSession.Leave(Net.NetText.Mod("你离开了多人游戏"));
                    Net.NetChat.Add(Lang.T("你离开了多人游戏"), new Color(1f, 0.94f, 0.08f));
                }
                y += 50f * ui;
            }
            leftContent = y + leftScroll;
            inLeft = false;
            GUI.EndGroup();
            if (maxScroll > 0f)
            {
                // Where the column is scrolled to: a thin bar down its right edge.
                var track = new Rect(leftClip.xMax - 5f * ui, leftClip.y, 4f * ui, leftClip.height);
                CreativeMenu.Fill(track, new Color(0f, 0f, 0f, 0.3f));
                float h = track.height * leftClip.height / leftContent;
                CreativeMenu.Fill(new Rect(track.x, track.y + (track.height - h) * leftScroll / maxScroll, track.width, h), new Color(0.75f, 0.85f, 1f, 0.8f));
            }
            float lxRight = lx0;

            // On the right, two tabs: the keybindings (two or three columns of Terraria's actions), or the cursor's look.
            float rx = lxRight + lw + 30f * ui, rw = p.xMax - 30f * ui - rx, ry = p.y + 70f * ui;
            CreativeMenu.Fill(new Rect(rx - 15f * ui, ry, 2f * ui, p.yMax - 100f * ui - ry), new Color(0.05f, 0.05f, 0.12f, 0.7f));
            // UI.Keybindings: "Keybindings"
            if (Tab(new Rect(rx, ry, 190f * ui, 34f * ui), TT("UI.Keybindings", "按键绑定"), rightPage == 0, ui)) rightPage = 0;
            if (Tab(new Rect(rx + 200f * ui, ry, 110f * ui, 34f * ui), Lang.T("光标"), rightPage == 1, ui))
            {
                rightPage = 1;
                if (TerraKeys.Listening.HasValue) TerraKeys.StopListening();
            }
            // The Voice Beyond the Door's guidance, this save's (Items.BossGuide).
            if (Tab(new Rect(rx + 320f * ui, ry, 170f * ui, 34f * ui), Items.BossGuide.Title, rightPage == 2, ui))
            {
                rightPage = 2;
                if (TerraKeys.Listening.HasValue) TerraKeys.StopListening();
            }
            // The interface's size (Terraria's Interface options: UI Scale).
            if (Tab(new Rect(rx + 500f * ui, ry, 120f * ui, 34f * ui), Lang.T("界面"), rightPage == 3, ui))
            {
                rightPage = 3;
                if (TerraKeys.Listening.HasValue) TerraKeys.StopListening();
            }
            ry += 44f * ui;
            if (rightPage == 3)
            {
                DrawInterfacePage(new Rect(rx, ry + 10f * ui, rw, p.yMax - 110f * ui - ry), ui);
                // LegacyMenu.5: "Back"
                if (CreativeMenu.Button(new Rect(p.center.x - 130f * ui, p.yMax - 76f * ui, 260f * ui, 50f * ui), TT("LegacyMenu.5", "返回"), ui)) Hide();
                return;
            }
            if (rightPage == 2)
            {
                Items.BossGuide.DrawSettings(new Rect(rx, ry + 10f * ui, rw, p.yMax - 110f * ui - ry), ui);
                // LegacyMenu.5: "Back"
                if (CreativeMenu.Button(new Rect(p.center.x - 130f * ui, p.yMax - 76f * ui, 260f * ui, 50f * ui), TT("LegacyMenu.5", "返回"), ui)) Hide();
                return;
            }
            if (rightPage == 1)
            {
                DrawCursorPage(new Rect(rx, ry, rw, p.yMax - 100f * ui - ry), ui);
                // LegacyMenu.5: "Back"
                if (CreativeMenu.Button(new Rect(p.center.x - 130f * ui, p.yMax - 76f * ui, 260f * ui, 50f * ui), TT("LegacyMenu.5", "返回"), ui)) Hide();
                return;
            }
            // The charm skill key only with the charm page (still being made, off by default).
            var all = Items.Charms.Enabled ? TerraKeys.Ordered : System.Array.FindAll(TerraKeys.Ordered, k => k != TerraKeys.Action.CharmSkill);
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

        /// <summary>The right column's page: 0 the keybindings, 1 the cursor.</summary>
        static int rightPage;

        /// <summary>Dev: shows a page of the right column (0 keybindings, 1 cursor).</summary>
        public static void DevPage(int page) => rightPage = page;

        /// <summary>A tab over the right column: the chosen one bright with a line under it, the other dim (gold under the mouse).</summary>
        static bool Tab(Rect r, string text, bool chosen, float ui)
        {
            bool over = r.Contains(TerraCursor.GuiMouse());
            CreativeMenu.Shadowed(r, text, section, chosen ? new Color(0.75f, 0.85f, 1f) : over ? Gold : new Color(0.6f, 0.62f, 0.75f));
            if (chosen) CreativeMenu.Fill(new Rect(r.x, r.yMax + 2f * ui, Mathf.Min(r.width, section.CalcSize(new GUIContent(text)).x), 3f * ui), new Color(0.75f, 0.85f, 1f, 0.9f));
            return !chosen && CreativeMenu.Clicked(r);
        }

        // ------------------------------------------------------------------ the cursor page

        /// <summary>The slider being dragged (-1 none): 0 size, 1 glow, 2 hue, 3 saturation, 4 lightness.</summary>
        static int dragging = -1;
        static float hue, saturation, lightness;
        static Color? hslOf;
        static Texture2D hueBar, saturationBar, lightnessBar;
        static Vector3 barsFor = new Vector3(-1f, -1f, -1f);

        /// <summary>
        /// The cursor's look (TerraCursor): its picture (Terraria's or Hollow Knight's), size, colour by Terraria's hue,
        /// saturation and lightness bars, glow and pulse, with a live preview on a dark and a light ground. A slider follows
        /// the mouse while held and the choice is written to the config when it is let go.
        /// </summary>
        static void DrawCursorPage(Rect a, float ui)
        {
            var e = Event.current;
            if (dragging >= 0 && (e.type == EventType.MouseUp || !TerraCursor.LeftHeld(UnityEngine.InputSystem.Mouse.current)))
            {
                dragging = -1;
                TerraCursor.Save();
            }
            var note = new Color(0.85f, 0.85f, 0.95f, 0.85f);
            float previewW = Mathf.Min(320f * ui, a.width * 0.36f);
            float cx = a.x, cw = a.width - previewW - 30f * ui, y = a.y;
            float labelW = 210f * ui, barX = cx + labelW, barW = cw - labelW - 10f * ui, barH = 16f * ui;

            // The picture.
            CreativeMenu.Shadowed(new Rect(cx, y, labelW, 40f * ui), Lang.T("样式"), keyStyle, Color.white);
            float bw = (cw - labelW - 20f * ui) / 2f;
            for (int i = 0; i < 2; i++)
            {
                var st = (TerraCursor.Style)i;
                bool chosen = TerraCursor.CurrentStyle == st;
                var r = new Rect(barX + i * (bw + 10f * ui), y, bw, 40f * ui);
                if (CreativeMenu.Button(r, Lang.T(i == 0 ? "泰拉瑞亚" : "空洞骑士"), ui, chosen ? Gold : (Color?)null) && !chosen)
                {
                    TerraCursor.CurrentStyle = st;
                    TerraCursor.Save();
                }
                if (chosen) CreativeMenu.Rim(r, Gold, 2.5f * ui);
            }
            y += 46f * ui;
            CreativeMenu.Shadowed(new Rect(cx, y, cw, 26f * ui), Lang.T("泰拉瑞亚：原版指针，开启智能光标时换成智能光标的样子；空洞骑士：游戏自己的花纹指针"), CreativeMenu.text, note);
            y += 44f * ui;

            // Size.
            CreativeMenu.Shadowed(new Rect(cx, y, labelW, 30f * ui), Lang.F("大小：{0}%", Mathf.RoundToInt(TerraCursor.Size * 100f)), keyStyle, Color.white);
            float size01 = Slider(0, new Rect(barX, y + 7f * ui, barW, barH), Mathf.InverseLerp(TerraCursor.MinSize, TerraCursor.MaxSize, TerraCursor.Size), null, ui);
            TerraCursor.Size = Mathf.Round(Mathf.Lerp(TerraCursor.MinSize, TerraCursor.MaxSize, size01) * 20f) / 20f;
            y += 50f * ui;

            // Colour: Terraria's three bars, the swatch, the style's own colour back.
            SyncHsl();
            CreativeMenu.Shadowed(new Rect(cx, y, labelW, 30f * ui), Lang.T("颜色"), keyStyle, Color.white);
            var swatch = new Rect(barX, y + 2f * ui, 60f * ui, 26f * ui);
            CreativeMenu.Fill(swatch, TerraCursor.BaseColor);
            CreativeMenu.Rim(swatch, new Color(0f, 0f, 0f, 0.8f), 2f * ui);
            if (CreativeMenu.Button(new Rect(swatch.xMax + 14f * ui, y, 170f * ui, 30f * ui), Lang.T("默认颜色"), ui) && TerraCursor.CustomColor.HasValue)
            {
                TerraCursor.CustomColor = null;
                TerraCursor.Save();
            }
            y += 40f * ui;
            RebuildBars();
            string[] names = { "色相", "饱和度", "亮度" };
            var bars = new[] { hueBar, saturationBar, lightnessBar };
            float[] values = { hue, saturation, lightness };
            bool colourChanged = false;
            for (int i = 0; i < 3; i++)
            {
                CreativeMenu.Shadowed(new Rect(cx + 20f * ui, y, labelW - 20f * ui, 30f * ui), Lang.T(names[i]), keyStyle, new Color(0.85f, 0.9f, 1f));
                float v = Slider(2 + i, new Rect(barX, y + 7f * ui, barW, barH), values[i], bars[i], ui);
                if (!Mathf.Approximately(v, values[i])) { values[i] = v; colourChanged = true; }
                y += 38f * ui;
            }
            if (colourChanged)
            {
                hue = values[0];
                saturation = values[1];
                lightness = values[2];
                var c = Effects.FxParticles.HslToRgb(hue, saturation, lightness);
                TerraCursor.CustomColor = new Color(c.r, c.g, c.b, 1f);
                hslOf = TerraCursor.CustomColor;
            }
            y += 8f * ui;

            // Glow.
            CreativeMenu.Shadowed(new Rect(cx, y, labelW, 30f * ui), Lang.F("发光强度：{0}%", Mathf.RoundToInt(TerraCursor.Glow * 100f)), keyStyle, Color.white);
            TerraCursor.Glow = Mathf.Round(Slider(1, new Rect(barX, y + 7f * ui, barW, barH), TerraCursor.Glow, null, ui) * 100f) / 100f;
            y += 34f * ui;
            CreativeMenu.Shadowed(new Rect(cx, y, cw, 26f * ui), Lang.T("0% 无描边，25% 是泰拉瑞亚原样的浅色描边，更高会加上同色光晕"), CreativeMenu.text, note);
            y += 40f * ui;

            // Pulse, and every choice back to Terraria's.
            if (CreativeMenu.Button(new Rect(cx, y, 260f * ui, 40f * ui), Lang.T(TerraCursor.Pulse ? "呼吸闪烁：开" : "呼吸闪烁：关"), ui))
            {
                TerraCursor.Pulse = !TerraCursor.Pulse;
                TerraCursor.Save();
            }
            if (CreativeMenu.Button(new Rect(cx + 280f * ui, y, 220f * ui, 40f * ui), Lang.T("恢复默认"), ui))
            {
                TerraCursor.ResetLook();
                hslOf = null;
            }

            // The preview: the cursor as chosen, pulsing as it will, over a dark and a light ground.
            var box = new Rect(a.xMax - previewW, a.y, previewW, Mathf.Min(360f * ui, a.height));
            CreativeMenu.Shadowed(new Rect(box.x, box.y, box.width, 30f * ui), Lang.T("预览"), keyStyle, new Color(0.75f, 0.85f, 1f));
            var dark = new Rect(box.x, box.y + 36f * ui, box.width, (box.height - 36f * ui) / 2f);
            var light = new Rect(box.x, dark.yMax, box.width, dark.height);
            CreativeMenu.Fill(dark, new Color(0.04f, 0.05f, 0.09f, 1f));
            CreativeMenu.Fill(light, new Color(0.82f, 0.84f, 0.88f, 1f));
            CreativeMenu.Rim(new Rect(dark.x, dark.y, dark.width, dark.height * 2f), new Color(0.05f, 0.05f, 0.12f, 0.95f), 2f * ui);
            if (e.type == EventType.Repaint)
            {
                TerraCursor.DrawPreview(dark.center - new Vector2(10f, 14f) * ui, ui);
                TerraCursor.DrawPreview(light.center - new Vector2(10f, 14f) * ui, ui);
            }
        }

        /// <summary>The bars' hue, saturation and lightness from the colour the cursor has, when that changed outside them.</summary>
        static void SyncHsl()
        {
            var c = TerraCursor.BaseColor;
            if (dragging >= 2 || hslOf.HasValue && hslOf.Value == c) return;
            RgbToHsl(c, out float h, out float s, out float l);
            // A grey has no hue of its own: the bar keeps where it was.
            if (s > 0.01f && l > 0.01f && l < 0.99f) hue = h;
            saturation = s;
            lightness = l;
            hslOf = c;
        }

        /// <summary>Main.rgbToHsl.</summary>
        static void RgbToHsl(Color c, out float h, out float s, out float l)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            l = (max + min) / 2f;
            if (max - min < 1e-5f) { h = 0f; s = 0f; return; }
            float d = max - min;
            s = l > 0.5f ? d / (2f - max - min) : d / (max + min);
            if (max == c.r) h = (c.g - c.b) / d + (c.g < c.b ? 6f : 0f);
            else if (max == c.g) h = (c.b - c.r) / d + 2f;
            else h = (c.r - c.g) / d + 4f;
            h /= 6f;
        }

        /// <summary>Terraria's colour bars: every hue; grey to the hue; black through the colour to white.</summary>
        static void RebuildBars()
        {
            if (hueBar == null) hueBar = Bar(t => Effects.FxParticles.HslToRgb(t, 1f, 0.5f));
            var now = new Vector3(hue, saturation, lightness);
            if (saturationBar != null && now == barsFor) return;
            barsFor = now;
            saturationBar = Bar(t => Effects.FxParticles.HslToRgb(hue, t, lightness), saturationBar);
            lightnessBar = Bar(t => Effects.FxParticles.HslToRgb(hue, saturation, t), lightnessBar);
        }

        static Texture2D Bar(System.Func<float, Color> at, Texture2D reuse = null)
        {
            const int W = 128;
            var tex = reuse ?? new Texture2D(W, 1, TextureFormat.RGBA32, false) { name = "Cursor Colour Bar", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[W];
            for (int i = 0; i < W; i++)
            {
                var c = at(i / (W - 1f));
                px[i] = new Color(c.r, c.g, c.b, 1f);
            }
            tex.SetPixels(px);
            tex.Apply(false);
            return tex;
        }

        /// <summary>A bar to drag along (a gradient, or a fill to the value), its knob gold while held or under the mouse.</summary>
        /// <summary>The interface scale shown while its bar is dragged; applied on letting go, as Terraria's (temporaryGUIScaleSlider).</summary>
        static float draggedScale = -1f;
        const int ScaleSlider = 10;
        const int EffectSlider = 11;
        public static Rect DevEffectSlider { get; private set; }

        /// <summary>
        /// Terraria's UI Scale (IngameOptions: 50% to 200%, the bar's value taken on release, Main.UIScaleMax capping what is
        /// used): the whole of the mod's interface larger or smaller, the scale used beside the one asked for when the screen
        /// can't hold it.
        /// </summary>
        static void DrawInterfacePage(Rect a, float ui)
        {
            var e = Event.current;
            if(dragging==EffectSlider && (e.type==EventType.MouseUp || !TerraCursor.LeftHeld(UnityEngine.InputSystem.Mouse.current))) dragging=-1;
            if (dragging == ScaleSlider && (e.type == EventType.MouseUp || !TerraCursor.LeftHeld(UnityEngine.InputSystem.Mouse.current)))
            {
                dragging = -1;
                if (draggedScale > 0f) GameView.SetUiScale(draggedScale);
                draggedScale = -1f;
                return;
            }
            var note = new Color(0.85f, 0.85f, 0.95f, 0.85f);
            float labelW = 260f * ui, barX = a.x + labelW, barW = a.width - labelW - 20f * ui, barH = 16f * ui, y = a.y;
            float shown = draggedScale > 0f ? draggedScale : GameView.WantedScale;
            int wanted = Mathf.RoundToInt(shown * 100f), used = Mathf.RoundToInt(Mathf.Min(shown, GameView.MaxScale) * 100f);
            string label = used != wanted ? Lang.F("界面缩放：{0}%（实际 {1}%）", wanted, used) : Lang.F("界面缩放：{0}%", wanted);
            CreativeMenu.Shadowed(new Rect(a.x, y, labelW, 30f * ui), label, keyStyle, Color.white);
            float v = Slider(ScaleSlider, new Rect(barX, y + 7f * ui, barW, barH), Mathf.InverseLerp(GameView.MinUiScale, GameView.MaxUiScale, shown), null, ui);
            if (dragging == ScaleSlider) draggedScale = Mathf.Round(Mathf.Lerp(GameView.MinUiScale, GameView.MaxUiScale, v) * 100f) / 100f;
            y += 46f * ui;
            CreativeMenu.Shadowed(new Rect(a.x, y, a.width, 70f * ui), Lang.T("放大或缩小 mod 的全部界面：背包、生命和魔力、地图、聊天、提示和这个设置面板。和泰拉瑞亚的“界面缩放”一样，范围 50%～200%，松开鼠标后生效；屏幕放不下时按屏幕能容纳的最大值显示。"), CreativeMenu.text, note);
            y += 80f * ui;
            if (CreativeMenu.Button(new Rect(a.x, y, 220f * ui, 44f * ui), Lang.T("恢复 100%"), ui) && GameView.WantedScale != 1f)
                GameView.SetUiScale(1f);
            y += 72f * ui;
            CreativeMenu.Shadowed(new Rect(a.x,y,labelW,30f*ui),Lang.F("武器特效不透明度：{0}%",Mathf.RoundToInt(Effects.WeaponEffects.Opacity*100f)),keyStyle,Color.white);
            DevEffectSlider = new Rect(barX,y+7f*ui,barW,barH);
            float opacity = Slider(EffectSlider,DevEffectSlider,Effects.WeaponEffects.Opacity,null,ui);
            if (Mathf.Abs(opacity-Effects.WeaponEffects.Opacity)>.001f) Effects.WeaponEffects.Set(opacity);
            y += 46f*ui;
            CreativeMenu.Shadowed(new Rect(a.x,y,a.width,76f*ui),Lang.T("0% 隐藏，100% 正常显示。统一调整挥砍、弹幕、光束、拖尾、爆炸、粒子和召唤物等特效；立即生效并保存，不改变伤害或命中。"),CreativeMenu.text,note);
            y += 88f*ui;
            if(CreativeMenu.Button(new Rect(a.x,y,220f*ui,44f*ui),Lang.T("恢复特效 100%"),ui)) Effects.WeaponEffects.Set(1f);
        }

        static float Slider(int id, Rect bar, float v, Texture2D gradient, float ui)
        {
            var e = Event.current;
            var m = TerraCursor.GuiMouse();
            var hit = new Rect(bar.x - 8f * ui, bar.y - 10f * ui, bar.width + 16f * ui, bar.height + 20f * ui);
            if ((e.type == EventType.MouseDown && e.button == 0 || TerraCursor.DevDrives && e.type == EventType.Repaint && TerraCursor.LeftPressed(UnityEngine.InputSystem.Mouse.current)) && hit.Contains(m))
            {
                dragging = id;
                if(e.type==EventType.MouseDown) e.Use();
                TerraMod.Instance?.PlaySound("Menu_Tick", 0.5f);
            }
            if (dragging == id) v = Mathf.Clamp01((m.x - bar.x) / bar.width);
            if (e.type != EventType.Repaint) return v;
            CreativeMenu.Fill(new Rect(bar.x - 2f * ui, bar.y - 2f * ui, bar.width + 4f * ui, bar.height + 4f * ui), new Color(0f, 0f, 0f, 0.6f));
            if (gradient != null) GUI.DrawTexture(bar, gradient, ScaleMode.StretchToFill);
            else
            {
                CreativeMenu.Fill(bar, new Color(0.2f, 0.25f, 0.45f, 0.9f));
                CreativeMenu.Fill(new Rect(bar.x, bar.y, bar.width * v, bar.height), new Color(0.75f, 0.85f, 1f, 0.9f));
            }
            var knob = new Rect(bar.x + bar.width * v - 5f * ui, bar.y - 6f * ui, 10f * ui, bar.height + 12f * ui);
            CreativeMenu.Fill(knob, dragging == id || hit.Contains(m) ? Gold : Color.white);
            CreativeMenu.Rim(knob, new Color(0f, 0f, 0f, 0.8f), 1.5f * ui);
            return v;
        }

        /// <summary>The left column's window on the panel, its height of rows and how far it is scrolled; drawing inside it.</summary>
        static Rect leftClip;
        static float leftContent, leftScroll;
        static bool inLeft;
        internal static bool DevShowNet;
        internal static Rect DevDisconnectRect;
        internal static Rect DevLeftClip => leftClip;
        internal static float DevLeftContent => leftContent;
        internal static float DevLeftScroll { get => leftScroll; set => leftScroll = value; }

        /// <summary>A line to click, Terraria's way: bigger and yellow under the mouse.</summary>
        static bool Toggle(Rect r, string text, float ui)
        {
            var m = TerraCursor.GuiMouse();
            // In the scrolled column only a row in sight answers (one scrolled off still lies under the mouse's local point).
            bool over = r.Contains(m) && (!inLeft || new Rect(0f, 0f, leftClip.width, leftClip.height).Contains(m));
            row.fontSize = Mathf.RoundToInt((over ? 27 : 24) * ui);
            CreativeMenu.Shadowed(r, text, row, over ? Gold : Color.white);
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || !over) return false;
            e.Use();
            TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
            return true;
        }

        /// <summary>The panel is up: clicks are its own, not a use.</summary>
        public static bool MouseOver => AnyOpen;

        /// <summary>A window over the game that takes its input and pauses it: this one or the achievements.</summary>
        public static bool AnyOpen => Open || Achievements.AchievementPanel.Open;
    }
}
