using BepInEx.Configuration;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using TerraKnight.Terraria;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.UI
{
    /// <summary>
    /// Terraria's other two map styles (Main.mapStyle, Tab cycles them: minimap, overlay, none):
    /// the minimap in its frame at the top right (240x240 at 52 from the right and 90 down, 1.05
    /// pixels a tile, the frame's zoom in / zoom out / reset buttons), and the overlay, the whole
    /// map over the screen at 2.5 pixels a tile and 35% opacity, centred on the player. Numpad + and
    /// - (or = and -) zoom whichever is shown, PageUp and PageDown change its opacity, while held
    /// (Player: 1.025 / 0.975 a tick for the minimap, 1.05 / 0.95 for the overlay, 0.015 opacity).
    /// Tab no longer opens Hollow Knight's quick map while this map is in use.
    /// </summary>
    internal static partial class WorldMap
    {
        /// <summary>0 none, 1 minimap, 2 overlay (config Gameplay.MapStyle, kept as it changes).</summary>
        public static ConfigEntry<int> StyleSetting;
        public static int Style => StyleSetting != null ? Mathf.Clamp(StyleSetting.Value, 0, 2) : 1;
        const float MinimapDefaultScale = 1.05f;
        static float miniScale = MinimapDefaultScale, miniAlpha = 1f, overlayScale = 2.5f, overlayAlpha = 0.35f;
        static float keyTicks;
        const string FrameDir = "UI/Minimap/Default/";

        /// <summary>Keys and the frame's buttons for the minimap and overlay; call every frame in gameplay.</summary>
        public static void UpdateMini()
        {
            if (!Enabled || Open) return;
            var kb = Keyboard.current;
            // Tests set the style and zoom themselves: keys and clicks in their window don't count.
            if (kb == null || TerraMod.Instance?.AutoTest != null) return;
            if (kb.tabKey.wasPressedThisFrame)
            {
                if (StyleSetting != null) StyleSetting.Value = (Style + 1) % 3;
                TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
            }
            keyTicks += Time.unscaledDeltaTime * 60f;
            int ticks = Mathf.Min(4, (int)keyTicks);
            keyTicks -= (int)keyTicks;
            if (ticks == 0 || Style == 0) return;
            bool zoomIn = kb.numpadPlusKey.isPressed || kb.equalsKey.isPressed;
            bool zoomOut = kb.numpadMinusKey.isPressed || kb.minusKey.isPressed;
            bool alphaUp = kb.pageUpKey.isPressed, alphaDown = kb.pageDownKey.isPressed;
            var mouse = Mouse.current;
            if (Style == 1 && mouse != null && mouse.leftButton.isPressed)
            {
                Vector2 m = mouse.position.ReadValue();
                var gui = new Vector2(m.x, Screen.height - m.y);
                if (MinimapButtonAt(gui, out int button))
                {
                    if (button == 0 && mouse.leftButton.wasPressedThisFrame) miniScale = MinimapDefaultScale;
                    if (button == 1) zoomIn = true;
                    if (button == 2) zoomOut = true;
                }
            }
            for (int i = 0; i < ticks; i++)
            {
                if (Style == 1)
                {
                    if (zoomIn) miniScale *= 1.025f;
                    if (zoomOut) miniScale *= 0.975f;
                    if (alphaUp) miniAlpha += 0.015f;
                    if (alphaDown) miniAlpha -= 0.015f;
                }
                else
                {
                    if (zoomIn) overlayScale *= 1.05f;
                    if (zoomOut) overlayScale *= 0.95f;
                    if (alphaUp) overlayAlpha += 0.015f;
                    if (alphaDown) overlayAlpha -= 0.015f;
                }
            }
            miniScale = Mathf.Clamp(miniScale, 0.2f, 3f);
            overlayScale = Mathf.Clamp(overlayScale, 0.5f, 16f);
            miniAlpha = Mathf.Clamp01(miniAlpha);
            overlayAlpha = Mathf.Clamp01(overlayAlpha);
        }

        /// <summary>Where the minimap's picture goes on screen.</summary>
        static Rect MinimapRect()
        {
            float ui = GameView.Scale;
            return new Rect(Screen.width - (52f + 240f) * ui, 90f * ui, 240f * ui, 240f * ui);
        }

        /// <summary>The frame's place (MinimapFrame: the picture's corner + 4, then the Default frame's offset -8, -15).</summary>
        static Vector2 FramePosition()
        {
            float ui = GameView.Scale;
            var r = MinimapRect();
            return new Vector2(r.x + (4f - 8f) * ui, r.y + (4f - 15f) * ui);
        }

        /// <summary>The Default frame's buttons: reset, zoom in, zoom out, by the frame's top left.</summary>
        static readonly Vector2[] buttonOffsets = { new Vector2(150f, 240f), new Vector2(202f, 240f), new Vector2(176f, 240f) };
        static readonly string[] buttonTextures = { "MinimapButton_Reset", "MinimapButton_ZoomIn", "MinimapButton_ZoomOut" };

        static bool MinimapButtonAt(Vector2 gui, out int button)
        {
            button = -1;
            if (Style != 1 || !MinimapShown()) return false;
            float ui = GameView.Scale;
            var frame = FramePosition();
            for (int i = 0; i < 3; i++)
            {
                var tex = TerrariaAssets.Texture(FrameDir + buttonTextures[i]);
                if (tex == null) continue;
                // Button.IsTouchingPoint: within half its width of its centre.
                var centre = frame + buttonOffsets[i] * ui + new Vector2(tex.width, tex.height) * 0.5f * ui;
                if (Vector2.Distance(gui, centre) < tex.width * 0.5f * ui) { button = i; return true; }
            }
            return false;
        }

        static bool MinimapShown() => Enabled && !Open && Style == 1 && !Items.InventoryGui.Open && GameHooks.IsGameplay(GameManager.UnsafeInstance);

        /// <summary>The mouse is on the minimap's frame (the bag's "over the UI": no item use there).</summary>
        public static bool MouseOnMinimap(Vector2 gui)
        {
            if (!MinimapShown()) return false;
            var r = MinimapRect();
            float ui = GameView.Scale;
            return Rect.MinMaxRect(r.xMin - 8f * ui, r.yMin - 15f * ui, r.xMax + 8f * ui, r.yMax + 30f * ui).Contains(gui);
        }

        /// <summary>The minimap or the overlay, before the rest of the interface; call from OnGUI.</summary>
        public static void DrawMini()
        {
            if (!Enabled || Open || Event.current.type != EventType.Repaint) return;
            if (Style == 1 && MinimapShown()) DrawMinimap();
            else if (Style == 2) DrawOverlay();
        }

        /// <summary>The rooms seen, drawn round a centre cell at a zoom into a rectangle (solids, then open cells, at an alpha).</summary>
        static void DrawRooms(Rect area, Vector2 centre, float z, float alpha, bool inGroup)
        {
            float cx = (inGroup ? 0f : area.x) + area.width / 2f, cy = (inGroup ? 0f : area.y) + area.height / 2f;
            float w = area.width, h = area.height;
            var visible = new System.Collections.Generic.List<(Room room, Rect rect)>();
            float now = Time.unscaledTime;
            foreach (var room in rooms.Values)
            {
                if (!room.Captured || !room.AnySeen || !Shown(room)) continue;
                var rect = new Rect(cx + (room.At.x - centre.x) * z, cy - (room.At.y + room.H - centre.y) * z, room.W * z, room.H * z);
                float left = inGroup ? 0f : area.x, top = inGroup ? 0f : area.y;
                if (rect.xMax < left || rect.yMax < top || rect.xMin > left + w || rect.yMin > top + h) continue;
                // The corner maps refresh a room's picture at most twice a second.
                if (room.Solid == null || room.Dirty && now - room.LastBuilt > 0.5f) Build(room);
                visible.Add((room, rect));
            }
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            foreach (var (room, rect) in visible) GUI.DrawTexture(rect, room.Solid);
            foreach (var (room, rect) in visible) GUI.DrawTexture(rect, room.Open);
            GUI.color = prev;
        }

        static void DrawMinimap()
        {
            float ui = GameView.Scale;
            var area = MinimapRect();
            var prev = GUI.color;
            // MinimapFrame.DrawBackground: black, 244 square round the picture, at the minimap's opacity.
            GUI.color = new Color(0f, 0f, 0f, miniAlpha);
            GUI.DrawTexture(new Rect(area.x - 2f * ui, area.y - 2f * ui, 244f * ui, 244f * ui), Texture2D.whiteTexture);
            GUI.color = prev;
            var centre = HeadCell(out _);
            GUI.BeginGroup(area);
            DrawRooms(area, centre, miniScale * ui, miniAlpha, true);
            FullMap.DrawHead(new Vector2(area.width / 2f, area.height / 2f), ui * 0.5f);
            GUI.EndGroup();
            var frameTex = TerrariaAssets.Texture(FrameDir + "MinimapFrame");
            var frame = FramePosition();
            if (frameTex != null) GUI.DrawTexture(new Rect(frame.x, frame.y, frameTex.width * ui, frameTex.height * ui), frameTex);
            // A button's picture shows only under the mouse.
            var mp = Event.current.mousePosition;
            if (MinimapButtonAt(mp, out int b))
            {
                var tex = TerrariaAssets.Texture(FrameDir + buttonTextures[b]);
                var at = frame + buttonOffsets[b] * ui;
                GUI.DrawTexture(new Rect(at.x, at.y, tex.width * ui, tex.height * ui), tex);
            }
        }

        static void DrawOverlay()
        {
            float ui = GameView.Scale;
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            var centre = HeadCell(out _);
            DrawRooms(screen, centre, overlayScale * ui, overlayAlpha, false);
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Max(overlayAlpha, 0.5f));
            FullMap.DrawHead(new Vector2(Screen.width / 2f, Screen.height / 2f), ui * 0.6f);
            GUI.color = prev;
        }

        public static string DevMini() => $"样式 {Style}，小地图缩放 {miniScale:F2} 不透明 {miniAlpha:F2}，覆盖缩放 {overlayScale:F2} 不透明 {overlayAlpha:F2}";

        public static void DevStyle(int style)
        {
            if (StyleSetting != null) StyleSetting.Value = style;
        }

        public static void DevMiniZoom(float scale) => miniScale = scale;

        /// <summary>Hollow Knight's quick map (Tab) stays shut while Terraria's map is in use: Tab is the map style key.</summary>
        [HarmonyPatch(typeof(ListenForQuickMap), nameof(ListenForQuickMap.OnUpdate))]
        static class NoQuickMap
        {
            static bool Prefix(ListenForQuickMap __instance)
            {
                if (!Enabled) return true;
                if (__instance.isNotPressed != null) __instance.Fsm.Event(__instance.isNotPressed);
                return false;
            }
        }
    }
}
