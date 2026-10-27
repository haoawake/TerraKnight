using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace TerraKnight.Player
{
    /// <summary>
    /// Terraria's keyboard controls, rebindable as in its Controls menu (Settings → Keybindings):
    /// movement and jump (added to the Knight's own input actions, <see cref="TerraControls"/>),
    /// throw, inventory, grapple, quick heal and mana, auto select, smart cursor, the maps, the
    /// chat, the ten hotbar slots and the four loadouts. A binding is changed by clicking it and
    /// pressing the new key (Escape keeps the old one); the choices are saved in the mod's config
    /// (section Keys). As in Terraria a binding may also be a mouse button: the middle one or a side
    /// one (Mouse3, Mouse4, Mouse5; the left and right stay the item's), stand-ins past the keyboard's
    /// keys in the same Key values.
    /// </summary>
    internal static class TerraKeys
    {
        public enum Action
        {
            Up, Down, Left, Right, Jump, Throw, Inventory, Grapple, QuickHeal, QuickMana, AutoSelect, SmartCursor, FullMap, MapStyle, Chat,
            Hotbar1, Hotbar2, Hotbar3, Hotbar4, Hotbar5, Hotbar6, Hotbar7, Hotbar8, Hotbar9, Hotbar10, Loadout1, Loadout2, Loadout3, Loadout4,
            // 1.4.4-1.4.5: the Dash key, Quick Buff, the map's zoom and opacity, cycling the loadouts, the armor set ability.
            Dash, QuickBuff, MapZoomIn, MapZoomOut, MapAlphaUp, MapAlphaDown, NextLoadout, PreviousLoadout, ArmorSetAbility,
            // Use Item and Interact (Terraria's MouseLeft and MouseRight triggers), the favourite key (config FavoriteKey).
            UseItem, Interact, Favorite,
            // The mod's own: the shadow dash (the Shade Cloak, the Hallownest Shield).
            ShadowDash,
            // Terraria's Zoom In and Zoom Out (Main.UpdateViewZoomKeys), held.
            ViewZoomIn, ViewZoomOut, Mount,
            // The mod's own: a worn charm's skill (Items.CharmSkills).
            CharmSkill,
            // The mod's own, unbound at first: home by a recall item carried, the Rod of Discord to the cursor (TerraMod.QuickRecall, QuickTeleport).
            Recall, DiscordTeleport,
            // The mod's own: the Crystal Heart's Super Dash (Items.AbilityAccessories), on the Dash key at first.
            SuperDash,
        }

        /// <summary>
        /// The list as Terraria's Keybindings menu orders it (UIManageControls.AssembleBindPanels): the gameplay controls,
        /// the map's, the hotbar's; the mod's own (Chat, the fourth loadout) among them.
        /// </summary>
        public static readonly Action[] Ordered =
        {
            Action.UseItem, Action.Interact, Action.Up, Action.Down, Action.Left, Action.Right, Action.Jump, Action.Grapple, Action.AutoSelect, Action.SmartCursor, Action.Favorite,
            Action.Mount, Action.QuickHeal, Action.QuickMana, Action.QuickBuff, Action.Throw, Action.Inventory, Action.Chat, Action.ViewZoomIn, Action.ViewZoomOut,
            Action.Loadout1, Action.Loadout2, Action.Loadout3, Action.Loadout4, Action.NextLoadout, Action.PreviousLoadout, Action.ArmorSetAbility, Action.Dash, Action.SuperDash, Action.ShadowDash, Action.CharmSkill,
            Action.Recall, Action.DiscordTeleport,
            Action.MapZoomIn, Action.MapZoomOut, Action.MapAlphaUp, Action.MapAlphaDown, Action.FullMap, Action.MapStyle,
            Action.Hotbar1, Action.Hotbar2, Action.Hotbar3, Action.Hotbar4, Action.Hotbar5, Action.Hotbar6, Action.Hotbar7, Action.Hotbar8, Action.Hotbar9, Action.Hotbar10,
        };

        public static readonly Action[] All = (Action[])Enum.GetValues(typeof(Action));

        /// <summary>The mouse buttons a binding may take, as Key values no keyboard key has.</summary>
        public const Key MouseMiddle = (Key)1001, MouseBack = (Key)1002, MouseForward = (Key)1003, MouseLeft = (Key)1004, MouseRight = (Key)1005;

        public static bool IsMouse(Key k) => k == MouseMiddle || k == MouseBack || k == MouseForward || k == MouseLeft || k == MouseRight;

        static ButtonControl MouseButton(Key k)
        {
            var m = Mouse.current;
            if (m == null) return null;
            return k == MouseMiddle ? m.middleButton : k == MouseBack ? m.backButton : k == MouseForward ? m.forwardButton
                : k == MouseLeft ? m.leftButton : k == MouseRight ? m.rightButton : null;
        }

        /// <summary>A binding as the config writes it: the key's name, or Terraria's Mouse3 / Mouse4 / Mouse5.</summary>
        static string Id(Key k) => k == MouseLeft ? "Mouse1" : k == MouseRight ? "Mouse2" : k == MouseMiddle ? "Mouse3" : k == MouseBack ? "Mouse4" : k == MouseForward ? "Mouse5" : k == Key.None ? "None" : k.ToString();

        static bool TryParse(string s, out Key k)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "mouse1": case "leftbutton": k = MouseLeft; return true;
                case "mouse2": case "rightbutton": k = MouseRight; return true;
                case "mouse3": case "middlebutton": k = MouseMiddle; return true;
                case "mouse4": case "backbutton": k = MouseBack; return true;
                case "mouse5": case "forwardbutton": k = MouseForward; return true;
                case "none": k = Key.None; return true;
            }
            return Enum.TryParse(s, true, out k) && k != Key.None && !IsMouse(k);
        }

        static readonly Dictionary<Action, Key> defaults = new Dictionary<Action, Key>
        {
            [Action.Mount] = Key.R, [Action.Up] = Key.W, [Action.Down] = Key.S, [Action.Left] = Key.A, [Action.Right] = Key.D, [Action.Jump] = Key.Space,
            [Action.Throw] = Key.T, [Action.Inventory] = Key.Escape, [Action.Grapple] = Key.E, [Action.QuickHeal] = Key.H, [Action.QuickMana] = Key.J,
            [Action.AutoSelect] = Key.LeftShift, [Action.SmartCursor] = Key.LeftCtrl, [Action.FullMap] = Key.M, [Action.MapStyle] = Key.Tab, [Action.Chat] = Key.Enter,
            [Action.Hotbar1] = Key.Digit1, [Action.Hotbar2] = Key.Digit2, [Action.Hotbar3] = Key.Digit3, [Action.Hotbar4] = Key.Digit4, [Action.Hotbar5] = Key.Digit5,
            [Action.Hotbar6] = Key.Digit6, [Action.Hotbar7] = Key.Digit7, [Action.Hotbar8] = Key.Digit8, [Action.Hotbar9] = Key.Digit9, [Action.Hotbar10] = Key.Digit0,
            [Action.Loadout1] = Key.F1, [Action.Loadout2] = Key.F2, [Action.Loadout3] = Key.F3, [Action.Loadout4] = Key.F4,
            // PlayerInput.Reset (Redigit's Pick): the loadout cycling and the armor set ability are left unbound; Dash on F
            // here (Terraria leaves it unbound), the Shadow Dash on X (the user's choice, neither taken by another action).
            [Action.Dash] = Key.F, [Action.QuickBuff] = Key.B, [Action.MapZoomIn] = Key.NumpadPlus, [Action.MapZoomOut] = Key.NumpadMinus,
            [Action.MapAlphaUp] = Key.PageUp, [Action.MapAlphaDown] = Key.PageDown, [Action.NextLoadout] = Key.None, [Action.PreviousLoadout] = Key.None,
            [Action.ArmorSetAbility] = Key.None, [Action.ShadowDash] = Key.X, [Action.CharmSkill] = Key.C,
            [Action.Recall] = Key.None, [Action.DiscordTeleport] = Key.None,
            // The Super Dash on the Dash key: a long press of it (a key of its own: held, it charges at once).
            [Action.SuperDash] = Key.F,
            // PlayerInput's defaults: OemPlus and OemMinus, the = and - keys by the backspace.
            [Action.ViewZoomIn] = Key.Equals, [Action.ViewZoomOut] = Key.Minus,
            [Action.UseItem] = MouseLeft, [Action.Interact] = MouseRight, [Action.Favorite] = Key.LeftAlt,
        };

        static readonly Dictionary<Action, Key> keys = new Dictionary<Action, Key>(defaults);
        static readonly Dictionary<Action, ConfigEntry<string>> settings = new Dictionary<Action, ConfigEntry<string>>();

        /// <summary>Bumped whenever a binding changes (the movement bindings are put on the game's actions again).</summary>
        public static int Version { get; private set; }

        static ConfigEntry<bool> doubleTapDash;

        /// <summary>Double Tap Dashing (Player.Settings.DashControl, on by default): a dash also on tapping a direction twice; off, only the Dash key dashes.</summary>
        public static bool DoubleTapDash
        {
            get => doubleTapDash == null || doubleTapDash.Value;
            set
            {
                if (doubleTapDash != null) doubleTapDash.Value = value;
            }
        }

        public static void Bind(ConfigFile cfg)
        {
            doubleTapDash = cfg.Bind("Keys", "DoubleTapDash", true, "双击方向键冲刺（泰拉瑞亚按键设置里的“双击冲刺”）；关掉后只能用冲刺键冲刺。");
            foreach (var a in All)
            {
                var e = cfg.Bind("Keys", a.ToString(), defaults[a].ToString(), $"{a} 的按键（Unity Input System 的键名，如 W、Space、LeftShift、Digit1、F1；鼠标中键 Mouse3，侧键 Mouse4 / Mouse5）。游戏里物品栏右下角“泰拉瑞亚设置”里可以直接改。");
                settings[a] = e;
                if (TryParse(e.Value, out var k)) keys[a] = k;
            }
            Version++;
        }

        public static Key Get(Action a) => keys.TryGetValue(a, out var k) ? k : defaults[a];

        public static Key Default(Action a) => defaults[a];

        /// <summary>Dev: what the config holds for an action.</summary>
        public static string DevSaved(Action a) => settings.TryGetValue(a, out var e) ? e.Value : null;

        public static void Set(Action a, Key k)
        {
            keys[a] = k;
            if (settings.TryGetValue(a, out var e)) e.Value = Id(k);
            Version++;
            Plugin.Log.LogInfo($"[按键] {a} = {k}");
        }

        public static void ResetAll()
        {
            foreach (var a in All) Set(a, defaults[a]);
            DoubleTapDash = true;
        }

        static ButtonControl Control(Action a)
        {
            var kb = Keyboard.current;
            var k = Get(a);
            if (IsMouse(k)) return MouseButton(k);
            if (kb == null || k == Key.None) return null;
            try { return kb[k]; }
            catch (ArgumentException) { return null; }
        }

        /// <summary>The action's key went down this frame (never while a key is being chosen or the chat is typed into).</summary>
        public static bool Pressed(Action a)
        {
            if (Blocked || (Items.Casino.IsOpen && a != Action.Inventory)) return false;
            var c = Control(a);
            return c != null && c.wasPressedThisFrame;
        }

        public static bool Held(Action a)
        {
            if (Blocked || (Items.Casino.IsOpen && a != Action.Inventory)) return false;
            var c = Control(a);
            return c != null && c.isPressed;
        }

        /// <summary>A key is being chosen (or was just now): the keys are the settings' only.</summary>
        static bool Blocked => Listening != null || Time.frameCount - capturedFrame <= 1 || Items.InventoryGui.TypingName;

        /// <summary>A key is being chosen or was chosen this frame or the last (its Escape or Enter isn't anyone else's).</summary>
        public static bool Busy => Blocked;

        public static Action? Listening { get; private set; }
        static int capturedFrame = -10, listenFrame;

        public static void Listen(Action a)
        {
            Listening = a;
            listenFrame = Time.frameCount;
        }

        public static void StopListening()
        {
            Listening = null;
            capturedFrame = Time.frameCount;
        }

        /// <summary>Each frame: while a binding waits for its key, the first key or mouse button (middle, side) pressed is it (Escape: no change).</summary>
        public static void Tick()
        {
            if (Listening == null || Time.frameCount == listenFrame) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame && Listening != Action.Inventory)
                {
                    StopListening();
                    return;
                }
                foreach (var c in kb.allKeys)
                {
                    if (c == null || !c.wasPressedThisFrame || c.keyCode == Key.None) continue;
                    // PlayerInput.ListenFor: the key it already has takes it off (unbound).
                    Set(Listening.Value, c.keyCode == Get(Listening.Value) ? Key.None : c.keyCode);
                    StopListening();
                    return;
                }
            }
            foreach (var k in new[] { MouseLeft, MouseRight, MouseMiddle, MouseBack, MouseForward })
            {
                var b = MouseButton(k);
                if (b == null || !b.wasPressedThisFrame) continue;
                Set(Listening.Value, k == Get(Listening.Value) ? Key.None : k);
                StopListening();
                return;
            }
        }

        /// <summary>Several actions on one key (shown in red in the list).</summary>
        public static bool Clashes(Action a)
        {
            var k = Get(a);
            if (k == Key.None) return false;
            foreach (var b in All)
                if (b != a && Get(b) == k) return true;
            return false;
        }

        static bool IsHotbar(Action a) => a >= Action.Hotbar1 && a <= Action.Hotbar10;

        /// <summary>The action's name in Terraria's Controls menu (LegacyMenu.148-185, UI.Loadout1-3), or the mod's.</summary>
        public static string Name(Action a)
        {
            string key = a switch
            {
                Action.Up => "LegacyMenu.148", Action.Down => "LegacyMenu.149", Action.Left => "LegacyMenu.150", Action.Right => "LegacyMenu.151",
                Action.Jump => "LegacyMenu.152", Action.Throw => "LegacyMenu.153", Action.Inventory => "LegacyMenu.154", Action.Grapple => "LegacyMenu.155",
                Action.QuickMana => "LegacyMenu.156", Action.QuickHeal => "LegacyMenu.159", Action.AutoSelect => "LegacyMenu.160", Action.SmartCursor => "LegacyMenu.161",
                Action.MapStyle => "LegacyMenu.172", Action.FullMap => "LegacyMenu.173",
                Action.Loadout1 => "UI.Loadout1", Action.Loadout2 => "UI.Loadout2", Action.Loadout3 => "UI.Loadout3",
                Action.Dash => "UI.Dash", Action.QuickBuff => "LegacyMenu.157", Action.MapZoomIn => "LegacyMenu.168", Action.MapZoomOut => "LegacyMenu.169",
                Action.MapAlphaUp => "LegacyMenu.171", Action.MapAlphaDown => "LegacyMenu.170", Action.NextLoadout => "UI.NextLoadout",
                Action.PreviousLoadout => "UI.PreviousLoadout", Action.ArmorSetAbility => "UI.ArmorSetAbility",
                Action.UseItem => "LegacyMenu.162", Action.Interact => "LegacyMenu.163",
                Action.ViewZoomIn => "UI.ZoomIn", Action.ViewZoomOut => "UI.ZoomOut",
                _ => null,
            };
            if (IsHotbar(a)) key = "LegacyMenu." + (176 + (a - Action.Hotbar1));
            string zh = a switch
            {
                Action.Mount => "上下坐骑", Action.Chat => "聊天", Action.Loadout4 => "配装4", Action.Jump => "跳", Action.Throw => "丢下", Action.Grapple => "抓钩", Action.FullMap => "切换完整地图",
                Action.MapStyle => "切换地图类型", Action.Dash => "冲刺", Action.QuickBuff => "快速增益", Action.MapZoomIn => "放大", Action.MapZoomOut => "缩小",
                Action.MapAlphaUp => "降低透明度", Action.MapAlphaDown => "提高透明度", Action.NextLoadout => "下一配装", Action.PreviousLoadout => "上一配装",
                Action.ArmorSetAbility => "盔甲套装能力", Action.ShadowDash => "暗影冲刺", Action.SuperDash => "超级冲刺", Action.CharmSkill => "护符技", Action.Recall => "回城", Action.DiscordTeleport => "混沌传送", Action.UseItem => "使用物品", Action.Interact => "交互", Action.Favorite => "收藏",
                Action.ViewZoomIn => "放大", Action.ViewZoomOut => "缩小", _ => a.ToString(),
            };
            return (key != null ? Terraria.TerrariaText.Get(Lang.Culture, key) : null) ?? Lang.T(zh);
        }

        /// <summary>A key as the keyboard shows it.</summary>
        public static string KeyName(Key k)
        {
            // LegacyMenu.195: "<Unbound>"
            if (k == Key.None) return Terraria.TerrariaText.Get(Lang.Culture, "LegacyMenu.195") ?? Lang.T("<未绑定>");
            // Controls.LeftClick / RightClick
            if (k == MouseLeft) return Terraria.TerrariaText.Get(Lang.Culture, "Controls.LeftClick") ?? Lang.T("鼠标左键");
            if (k == MouseRight) return Terraria.TerrariaText.Get(Lang.Culture, "Controls.RightClick") ?? Lang.T("鼠标右键");
            if (k == MouseMiddle) return Lang.T("鼠标中键");
            if (k == MouseBack) return Lang.T("鼠标侧键（后）");
            if (k == MouseForward) return Lang.T("鼠标侧键（前）");
            var kb = Keyboard.current;
            try
            {
                var c = kb?[k];
                if (c != null && !string.IsNullOrEmpty(c.displayName)) return c.displayName;
            }
            catch (ArgumentException) { }
            return k.ToString();
        }

        /// <summary>
        /// The binding of the Knight's own actions (movement and jump) for a key or mouse button:
        /// InControl's key of the same name, or its mouse button (its Button4 / Button5 are the side
        /// buttons, UnityMouseProvider's back and forward).
        /// </summary>
        public static InControl.BindingSource ToInControlSource(Key k)
        {
            if (k == MouseLeft) return new InControl.MouseBindingSource(InControl.Mouse.LeftButton);
            if (k == MouseRight) return new InControl.MouseBindingSource(InControl.Mouse.RightButton);
            if (k == MouseMiddle) return new InControl.MouseBindingSource(InControl.Mouse.MiddleButton);
            if (k == MouseBack) return new InControl.MouseBindingSource(InControl.Mouse.Button4);
            if (k == MouseForward) return new InControl.MouseBindingSource(InControl.Mouse.Button5);
            return ToInControl(k, out var ik) ? new InControl.KeyBindingSource(ik) : null;
        }

        /// <summary>The InControl key of the same name, for the Knight's own actions (movement and jump).</summary>
        public static bool ToInControl(Key k, out InControl.Key ik)
        {
            string n = k.ToString();
            if (n.StartsWith("Digit")) n = "Key" + n.Substring(5);
            else if (n.StartsWith("Numpad")) n = "Pad" + n.Substring(6);
            else if (n == "Enter") n = "Return";
            else if (n == "LeftCtrl") n = "LeftControl";
            else if (n == "RightCtrl") n = "RightControl";
            return Enum.TryParse(n, out ik) && ik != InControl.Key.None;
        }
    }
}
