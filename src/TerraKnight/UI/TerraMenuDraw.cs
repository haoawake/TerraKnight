using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Terraria's menu pieces in IMGUI, sized from Terraria's own pixels at the menu's scale (<see cref="S"/>):
    /// UIPanel's nine-slice (PanelBackground under PanelBorder), Utils.DrawSplicedPanel's ten-pixel edges (the
    /// creation screens' CategoryPanel and PanelGrayscale), UITextPanel's big buttons, UIImageButton, GroupOptionButton,
    /// UIScrollbar, and text with Utils.DrawBorderString's dark outline. A hover over something new ticks, as Terraria's
    /// buttons do (SoundEngine.PlaySound(12)).
    /// </summary>
    internal static class TerraUi
    {
        /// <summary>Terraria pixels to screen pixels: Terraria's menus at UI scale 1 on a 1080-line screen.</summary>
        public static float S => Screen.height / 1080f;

        /// <summary>Text sizes in Terraria pixels: MouseText at 0.82, at 1, DeathText at 0.7 (the big buttons) and 0.8 (the titles).</summary>
        public const float Small = 16f, Normal = 19f, Large = 30f, Title = 36f;

        // Terraria's colours. An XNA colour times an opacity (premultiplied) is the colour at that alpha here.
        public static readonly Color PanelBack = C(63, 82, 151, 0.7f), PanelHover = C(73, 94, 171), ListBack = C(33, 43, 79, 0.8f),
            RowBorder = C(89, 116, 213, 0.7f), RowBorderHover = C(89, 116, 213), FatButtonHover = C(255, 231, 69),
            Inventory = C(63, 65, 151), CreativeColor = Color.Lerp(C(255, 105, 180), Color.white, 0.1f),
            ExpertColor = new Color(1f, 0.6f, 0f), HardcoreColor = new Color(1f, 0.15f, 0.1f);

        public static Color C(int r, int g, int b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);

        /// <summary>Clicks and hovers count (not while a panel above takes them).</summary>
        public static bool Input = true;

        public static Texture2D Tex(string name)
        {
            try { return TerrariaAssets.Texture(name); }
            catch (System.Exception) { return null; }
        }

        // ---- Clipping: the list draws inside its panel; mouse tests follow the clip's origin.

        static Vector2 origin, mouseOutside;
        static Rect clip;
        static bool clipped;

        public static void BeginClip(Rect abs)
        {
            // Inside GUI.BeginClip the event's mouse is the clip's own: the screen's is taken before it.
            mouseOutside = TerraCursor.GuiMouse();
            GUI.BeginClip(abs);
            origin = abs.position;
            clip = abs;
            clipped = true;
        }

        public static void EndClip()
        {
            GUI.EndClip();
            origin = Vector2.zero;
            clipped = false;
        }

        /// <summary>Where the clip being drawn in starts (screen coordinates; zero outside one).</summary>
        public static Vector2 ClipOrigin => clipped ? origin : Vector2.zero;

        /// <summary>The mouse in the coordinates being drawn in.</summary>
        public static Vector2 Mouse => clipped ? mouseOutside - origin : TerraCursor.GuiMouse();

        static bool InClip => !clipped || clip.Contains(mouseOutside);

        public static bool Over(Rect r) => Input && InClip && r.Contains(Mouse);

        // ---- Hover ticks.

        static string hoverNow, hoverLast;

        /// <summary>The mouse is on it; something new under it ticks once.</summary>
        public static bool Hover(Rect r, string id)
        {
            bool over = Over(r);
            if (over && id != null && Event.current.type == EventType.Repaint) hoverNow = id;
            return over;
        }

        /// <summary>At the end of each OnGUI pass: a hover begun this frame ticks.</summary>
        public static void EndFrame()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (hoverNow != null && hoverNow != hoverLast) Sound("Menu_Tick");
            hoverLast = hoverNow;
            hoverNow = null;
        }

        public static void Sound(string name) => TerraMod.Instance?.PlaySound(name, 0.7f);

        /// <summary>A left press on it now (used up).</summary>
        public static bool Clicked(Rect r)
        {
            var e = Event.current;
            if (!Input || e.type != EventType.MouseDown || e.button != 0 || !InClip || !r.Contains(e.mousePosition)) return false;
            e.Use();
            return true;
        }

        /// <summary>A second press on it soon after the first (UIElement's double click).</summary>
        public static bool DoubleClicked(Rect r, string id)
        {
            var e = Event.current;
            if (!Input || e.type != EventType.MouseDown || e.button != 0 || !InClip || !r.Contains(e.mousePosition)) return false;
            bool twice = lastClickId == id && Time.unscaledTime - lastClickAt < 0.5f;
            lastClickId = id;
            lastClickAt = Time.unscaledTime;
            if (twice) e.Use();
            return twice;
        }

        static string lastClickId;
        static float lastClickAt = -10f;

        // ---- Textures.

        /// <summary>A part (Terraria pixels from its top left) of a texture into a rect.</summary>
        public static void Part(Rect dst, Texture2D t, float sx, float sy, float sw, float sh, Color c)
        {
            if (t == null || dst.width <= 0f || dst.height <= 0f || sw <= 0f || sh <= 0f || Event.current.type != EventType.Repaint) return;
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTextureWithTexCoords(dst, t, new Rect(sx / t.width, 1f - (sy + sh) / t.height, sw / t.width, sh / t.height));
            GUI.color = prev;
        }

        public static void Whole(Rect dst, Texture2D t, Color c)
        {
            if (t != null) Part(dst, t, 0f, 0f, t.width, t.height, c);
        }

        /// <summary>A texture at its own size (times the scale) with its top left at a point.</summary>
        public static Rect At(Texture2D t, float x, float y) => t == null ? new Rect(x, y, 0f, 0f) : new Rect(x, y, t.width * S, t.height * S);

        /// <summary>UIPanel.DrawPanel: corners of <paramref name="corner"/> pixels, edges and middle stretched from <paramref name="bar"/>.</summary>
        public static void Nine(Rect r, Texture2D t, int corner, int bar, Color c)
        {
            if (t == null) return;
            float k = Mathf.Min(corner * S, Mathf.Min(r.width, r.height) / 2f);
            float x1 = r.x + k, x2 = r.xMax - k, y1 = r.y + k, y2 = r.yMax - k;
            float w = Mathf.Max(0f, x2 - x1), h = Mathf.Max(0f, y2 - y1);
            int o = corner + bar;
            Part(new Rect(r.x, r.y, k, k), t, 0, 0, corner, corner, c);
            Part(new Rect(x2, r.y, k, k), t, o, 0, corner, corner, c);
            Part(new Rect(r.x, y2, k, k), t, 0, o, corner, corner, c);
            Part(new Rect(x2, y2, k, k), t, o, o, corner, corner, c);
            Part(new Rect(x1, r.y, w, k), t, corner, 0, bar, corner, c);
            Part(new Rect(x1, y2, w, k), t, corner, o, bar, corner, c);
            Part(new Rect(r.x, y1, k, h), t, 0, corner, corner, bar, c);
            Part(new Rect(x2, y1, k, h), t, o, corner, corner, bar, c);
            Part(new Rect(x1, y1, w, h), t, corner, corner, bar, bar, c);
        }

        /// <summary>Utils.DrawSplicedPanel: the texture's outer <paramref name="e"/> pixels kept, its middle stretched.</summary>
        public static void Spliced(Rect r, Texture2D t, int e, Color c)
        {
            if (t == null) return;
            float k = Mathf.Min(e * S, Mathf.Min(r.width, r.height) / 2f);
            int tw = t.width, th = t.height, mw = tw - 2 * e, mh = th - 2 * e;
            float x1 = r.x + k, x2 = r.xMax - k, y1 = r.y + k, y2 = r.yMax - k;
            float w = Mathf.Max(0f, x2 - x1), h = Mathf.Max(0f, y2 - y1);
            Part(new Rect(r.x, r.y, k, k), t, 0, 0, e, e, c);
            Part(new Rect(x2, r.y, k, k), t, tw - e, 0, e, e, c);
            Part(new Rect(r.x, y2, k, k), t, 0, th - e, e, e, c);
            Part(new Rect(x2, y2, k, k), t, tw - e, th - e, e, e, c);
            Part(new Rect(x1, r.y, w, k), t, e, 0, mw, e, c);
            Part(new Rect(x1, y2, w, k), t, e, th - e, mw, e, c);
            Part(new Rect(r.x, y1, k, h), t, 0, e, e, mh, c);
            Part(new Rect(x2, y1, k, h), t, tw - e, e, e, mh, c);
            Part(new Rect(x1, y1, w, h), t, e, e, mw, mh, c);
        }

        /// <summary>A strip stretched sideways between two kept edges (Utils.DrawPanel, the list items' InnerPanelBackground).</summary>
        public static void Strip(Rect r, Texture2D t, int edge, Color c)
        {
            if (t == null) return;
            float k = Mathf.Min(edge * S, r.width / 2f);
            Part(new Rect(r.x, r.y, k, r.height), t, 0, 0, edge, t.height, c);
            Part(new Rect(r.x + k, r.y, Mathf.Max(0f, r.width - 2f * k), r.height), t, edge, 0, t.width - 2 * edge, t.height, c);
            Part(new Rect(r.xMax - k, r.y, k, r.height), t, t.width - edge, 0, edge, t.height, c);
        }

        /// <summary>UIPanel: its background in one colour and its border in another.</summary>
        public static void Panel(Rect r, Color back, Color border)
        {
            Nine(r, Tex("UI/PanelBackground"), 12, 4, back);
            Nine(r, Tex("UI/PanelBorder"), 12, 4, border);
        }

        /// <summary>The list items' info boxes (UIWorldListItem.DrawPanel) with a line centred in them.</summary>
        public static void InfoBox(Rect r, string s, Color c)
        {
            Strip(r, Tex("UI/InnerPanelBackground"), 8, Color.white);
            Text(new Rect(r.x + 4f * S, r.y, r.width - 8f * S, r.height), s, Small * S, c, TextAnchor.MiddleCenter);
        }

        public static float InfoHeight
        {
            get
            {
                var t = Tex("UI/InnerPanelBackground");
                return (t != null ? t.height : 28) * S;
            }
        }

        /// <summary>UIHorizontalSeparator: Separator1 stretched between its two-pixel ends.</summary>
        public static void Separator(float x, float y, float w)
        {
            var t = Tex("UI/CharCreation/Separator1");
            if (t == null) return;
            var c = Color.Lerp(Color.white, Inventory, 0.85f);
            c.a = 0.9f;
            Strip(new Rect(x, y, w, t.height * S), t, 2, c);
        }

        // ---- Text.

        static readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();
        static readonly GUIContent content = new GUIContent();

        public static GUIStyle Style(float size, TextAnchor anchor, bool wrap = false)
        {
            int px = Mathf.Max(8, Mathf.RoundToInt(size));
            int key = (px * 16 + (int)anchor) * 2 + (wrap ? 1 : 0);
            if (!styles.TryGetValue(key, out var st))
            {
                st = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold, alignment = anchor, wordWrap = wrap, clipping = TextClipping.Overflow,
                    fontSize = px, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
                };
                styles[key] = st;
            }
            return st;
        }

        /// <summary>Utils.DrawBorderString: the text over a dark outline.</summary>
        public static void Text(Rect r, string s, float size, Color c, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
        {
            if (string.IsNullOrEmpty(s)) return;
            BorderText.Draw(r, s, Style(size, anchor, wrap), new Color(0f, 0f, 0f, c.a), c, Mathf.Max(1f, 1.5f * S));
        }

        public static Vector2 Measure(string s, float size)
        {
            content.text = s ?? "";
            return Style(size, TextAnchor.MiddleLeft).CalcSize(content);
        }

        // ---- Controls.

        /// <summary>UITextPanel with large text (Back, New, Create): blue, lighter with a gold border under the mouse.</summary>
        public static bool BigButton(Rect r, string label, string id, bool enabled = true)
        {
            bool over = enabled && Hover(r, id);
            Panel(r, over ? PanelHover : PanelBack, over ? FatButtonHover : Color.black);
            Text(r, label, Large * S, enabled ? Color.white : new Color(0.6f, 0.6f, 0.6f), TextAnchor.MiddleCenter);
            return enabled && Clicked(r);
        }

        /// <summary>A UITextPanel title (UI.SelectWorld): centred over <paramref name="centerX"/>, its top at <paramref name="top"/>.</summary>
        public static void Header(float centerX, float top, string label)
        {
            var size = Measure(label, Title * S);
            float pad = 15f * S;
            var r = new Rect(centerX - size.x / 2f - pad, top, size.x + pad * 2f, size.y + pad * 2f);
            Panel(r, PanelHover, Color.black);
            Text(r, label, Title * S, Color.white, TextAnchor.MiddleCenter);
        }

        /// <summary>UIImageButton: the picture, full when the mouse is on it, else at <paramref name="idle"/>.</summary>
        public static bool ImageButton(Rect r, Texture2D t, string id, float idle = 0.4f)
        {
            bool over = Hover(r, id);
            Whole(r, t, new Color(1f, 1f, 1f, over ? 1f : idle));
            return Clicked(r);
        }

        /// <summary>
        /// GroupOptionButton: a grey-blue PanelGrayscale, the highlight inset when chosen, a white border under the mouse,
        /// its icon at the top left and its title right-aligned (centred with no icon).
        /// </summary>
        public static bool Option(Rect r, Texture2D icon, string title, Color titleColor, bool selected, string id, bool showSelected = true, Color? iconTint = null)
        {
            bool over = Hover(r, id);
            const float opacity = 0.7f;
            var col = Inventory;
            Spliced(r, Tex("UI/CharCreation/PanelGrayscale"), 10, new Color(col.r, col.g, col.b, opacity));
            if (selected && showSelected)
            {
                float k = 7f * S;
                var hi = Color.Lerp(col, Color.white, 0.7f);
                hi.a = opacity;
                Spliced(new Rect(r.x + k, r.y + k, r.width - 2f * k, r.height - 2f * k), Tex("UI/CharCreation/CategoryPanelHighlight"), 10, hi);
            }
            if (over) Spliced(r, Tex("UI/CharCreation/CategoryPanelBorder"), 10, Color.white);
            if (icon != null)
            {
                var ic = iconTint ?? Color.white;
                if (!over && !selected)
                {
                    ic = Color.Lerp(col, ic, 0.7f);
                    ic.a = opacity;
                }
                Whole(At(icon, r.x + S, r.y + S), icon, ic);
            }
            if (!string.IsNullOrEmpty(title))
            {
                if (icon != null) Text(new Rect(r.x + 34f * S, r.y, r.width - 42f * S, r.height), title, Normal * S, titleColor, TextAnchor.MiddleRight);
                else Text(r, title, Normal * S, titleColor, TextAnchor.MiddleCenter);
            }
            return Clicked(r);
        }

        /// <summary>
        /// UIScrollbar beside a list: its bar (six-pixel caps) and the handle over the part in view, dragged or clicked to.
        /// Returns the new view position.
        /// </summary>
        public static float Scrollbar(Rect r, float view, float viewSize, float total, ref bool dragging, ref float grab)
        {
            var bar = Tex("UI/Scrollbar");
            var inner = Tex("UI/ScrollbarInner");
            float cap = 6f * S;
            var inn = new Rect(r.x, r.y + 5f * S, r.width, r.height - 10f * S);
            float max = Mathf.Max(1f, total);
            var handle = new Rect(inn.x, inn.y + inn.height * (view / max) - 3f * S, r.width, inn.height * (viewSize / max) + 7f * S);
            var e = Event.current;
            bool overHandle = Over(handle);
            if (e.type == EventType.MouseDown && e.button == 0 && Input && InClip && r.Contains(e.mousePosition))
            {
                if (!handle.Contains(e.mousePosition))
                {
                    // A click on the bar moves the view there.
                    view = (e.mousePosition.y - inn.y - handle.height / 2f) / Mathf.Max(1f, inn.height) * max;
                    grab = handle.height / 2f;
                }
                else grab = e.mousePosition.y - handle.y;
                dragging = true;
                e.Use();
            }
            else if (dragging && e.type == EventType.MouseDrag)
            {
                view = (e.mousePosition.y - grab - inn.y + 3f * S) / Mathf.Max(1f, inn.height) * max;
                e.Use();
            }
            else if (dragging && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp)) dragging = false;
            if (bar != null)
            {
                Part(new Rect(r.x, r.y - cap, r.width, cap), bar, 0, 0, bar.width, 6, Color.white);
                Part(new Rect(r.x, r.y, r.width, r.height), bar, 0, 6, bar.width, 4, Color.white);
                Part(new Rect(r.x, r.yMax, r.width, cap), bar, 0, bar.height - 6, bar.width, 6, Color.white);
            }
            if (inner != null)
            {
                var c = new Color(1f, 1f, 1f, dragging || overHandle ? 1f : 0.85f);
                Part(new Rect(handle.x, handle.y - cap, handle.width, cap), inner, 0, 0, inner.width, 6, c);
                Part(new Rect(handle.x, handle.y, handle.width, handle.height), inner, 0, 6, inner.width, 4, c);
                Part(new Rect(handle.x, handle.yMax, handle.width, cap), inner, 0, inner.height - 6, inner.width, 6, c);
            }
            return Mathf.Clamp(view, 0f, Mathf.Max(0f, total - viewSize));
        }
    }
}
