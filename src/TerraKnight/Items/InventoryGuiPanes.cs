using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.Items
{
    /// <summary>
    /// The inventory's windows move (the user's 10-29 "哪都是家会挡住合成表，让所有界面改为可拖动的"): the left button pressed on a
    /// window's empty part (its frame, title, background, the gaps between its slots) and dragged carries the window; a double
    /// click there puts it back. Each window is drawn against the screen moved by its own offset, in UI units so it keeps its
    /// place at any resolution, and the offsets are kept in the config.
    /// </summary>
    internal static partial class InventoryGui
    {
        enum Pane { None, Home, Chest, Sell, Shop, Reforge, Forge, Refit, Craft, Stats, Bestiary, HkBag, Blacklist, Tracking, Chat }

        static ConfigEntry<string> paneSetting;
        static readonly Dictionary<Pane, Vector2> paneOffsets = new Dictionary<Pane, Vector2>();
        /// <summary>Last frame's interface rectangles by the window they belong to, in drawing order.</summary>
        static readonly List<(Pane Pane, Rect Rect)> paneAreas = new List<(Pane, Rect)>();
        static int paneFrom;
        static Pane dragPane, lastGrabPane;
        static Vector2 dragFrom, dragStart, lastGrabAt;
        static Rect dragBounds;
        static float lastGrabTime;
        /// <summary>Where the left button last went down (GUI px): a drag is measured from there, the press handled on the UI's tick a frame or two later.</summary>
        static Vector2 pressAt;
        /// <summary>Dev: what the last press on a window did.</summary>
        internal static string DevLastGrab = "";

        /// <summary>The left button went down this frame: where.</summary>
        static void NotePress(Mouse mouse)
        {
            var m = UI.TerraCursor.ScreenMouse(mouse);
            pressAt = new Vector2(m.x, Screen.height - m.y);
        }

        public static void BindPanes(ConfigFile cfg)
        {
            paneSetting = cfg.Bind("UI", "PanelOffsets", "",
                "物品栏各个窗口拖动后的位置（相对默认位置的偏移，界面单位）。清空即全部回到默认位置；也可以在游戏里双击窗口的空白处还原单个窗口。\nWhere the inventory's windows were dragged to (offsets from their default places, in UI units). Empty puts them all back; double-clicking a window's empty part puts that one back.");
            paneOffsets.Clear();
            foreach (var part in paneSetting.Value.Split(';'))
            {
                var kv = part.Split('=');
                if (kv.Length != 2 || !System.Enum.TryParse(kv[0].Trim(), out Pane pane)) continue;
                var xy = kv[1].Split(',');
                if (xy.Length == 2 && float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                    && float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                    paneOffsets[pane] = new Vector2(x, y);
            }
        }

        static void SavePanes()
        {
            if (paneSetting == null) return;
            var parts = new List<string>();
            foreach (var kv in paneOffsets)
                if (kv.Value != Vector2.zero)
                    parts.Add(kv.Key + "=" + kv.Value.x.ToString("0.#", CultureInfo.InvariantCulture) + "," + kv.Value.y.ToString("0.#", CultureInfo.InvariantCulture));
            paneSetting.Value = string.Join(";", parts);
        }

        internal static Rect DevHomePanelRect => homePanelRect;
        internal static string DevPaneSetting => paneSetting != null ? paneSetting.Value : null;
        internal static void DevResetPanes() { paneOffsets.Clear(); dragPane = Pane.None; SavePanes(); }

        /// <summary>The screen as a window is drawn against: moved by the window's offset. What it draws from here on is that window's.</summary>
        static Rect BeginPane(Pane pane, Rect view, float ui)
        {
            paneFrom = panels.Count;
            return paneOffsets.TryGetValue(pane, out var o) ? new Rect(view.x + o.x * ui, view.y + o.y * ui, view.width, view.height) : view;
        }

        static void EndPane(Pane pane)
        {
            for (int i = paneFrom; i < panels.Count; i++) paneAreas.Add((pane, panels[i]));
        }

        /// <summary>A click region that does nothing itself: a window's background.</summary>
        static bool Passive(Kind kind) => kind == Kind.BestiaryPanel || kind == Kind.HomePanel || kind == Kind.RecipeTracePanel;

        static Pane PaneAt(Vector2 at)
        {
            for (int i = paneAreas.Count - 1; i >= 0; i--)
                if (paneAreas[i].Rect.Contains(at)) return paneAreas[i].Pane;
            return Pane.None;
        }

        /// <summary>The left button pressed where nothing on top answers it: on a window's empty part a drag starts (a double click resets it).</summary>
        static void GrabPane(Vector2 at, bool onAction)
        {
            if (onAction || dragPane != Pane.None) { DevLastGrab = onAction ? "按在按钮或格子上" : "已在拖动 " + dragPane; return; }
            at = pressAt;
            var pane = PaneAt(at);
            if (pane == Pane.None) { DevLastGrab = "不在窗口上 " + at; return; }
            if (pane == lastGrabPane && Time.unscaledTime - lastGrabTime < 0.35f && (at - lastGrabAt).sqrMagnitude < 64f)
            {
                lastGrabPane = Pane.None;
                if (paneOffsets.Remove(pane)) { SavePanes(); Sound("Menu_Tick"); }
                DevLastGrab = "双击还原 " + pane;
                return;
            }
            lastGrabPane = pane;
            lastGrabTime = Time.unscaledTime;
            lastGrabAt = at;
            dragPane = pane;
            dragFrom = at;
            DevLastGrab = "开始拖动 " + pane;
            dragStart = paneOffsets.TryGetValue(pane, out var o) ? o : Vector2.zero;
            bool any = false;
            foreach (var (p, r) in paneAreas)
            {
                if (p != pane) continue;
                dragBounds = any ? Rect.MinMaxRect(Mathf.Min(dragBounds.xMin, r.xMin), Mathf.Min(dragBounds.yMin, r.yMin), Mathf.Max(dragBounds.xMax, r.xMax), Mathf.Max(dragBounds.yMax, r.yMax)) : r;
                any = true;
            }
        }

        /// <summary>Every frame while a window is held: it follows the mouse, its top and a strip of it kept on the screen.</summary>
        static void DragPane(Mouse mouse)
        {
            if (dragPane == Pane.None) return;
            var view = UI.GameView.Rect;
            float ui = UI.GameView.Scale;
            var m = UI.TerraCursor.ScreenMouse(mouse);
            var delta = new Vector2(m.x, Screen.height - m.y) - dragFrom;
            float edge = 80f * ui;
            delta.x = Mathf.Clamp(delta.x, view.x + edge - dragBounds.xMax, view.xMax - edge - dragBounds.xMin);
            delta.y = Mathf.Clamp(delta.y, view.y - dragBounds.yMin, view.yMax - 40f * ui - dragBounds.yMin);
            paneOffsets[dragPane] = dragStart + delta / ui;
            // Let go: where it was let go is where it stays.
            if (!UI.TerraCursor.LeftHeld(mouse))
            {
                dragPane = Pane.None;
                SavePanes();
            }
        }
    }
}
