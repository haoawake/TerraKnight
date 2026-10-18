using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// The monster book's notes at the bottom of the screen: "发现了新敌人 - 名字" when an enemy is
    /// first seen, "已收集 名字 资料" when it is first defeated. A small panel in the book's colours
    /// with the enemy's journal portrait, rising in over a quarter second, held 2.6 s, fading
    /// over 0.4 s; one at a time, the rest waiting their turn.
    /// </summary>
    internal static class BestiaryToast
    {
        struct Note
        {
            public string Record;
            public bool Collected;
        }

        static readonly Queue<Note> waiting = new Queue<Note>();
        static Note current;
        static bool showing;
        static float age;
        static GUIStyle style, headStyle;
        const float In = 0.25f, Hold = 2.6f, Out = 0.4f;

        public static int DevShown;
        public static string DevLast;

        public static void Show(string record, bool collected)
        {
            if (string.IsNullOrEmpty(record)) return;
            waiting.Enqueue(new Note { Record = record, Collected = collected });
        }

        public static string TextOf(string record, bool collected) =>
            collected ? Lang.F("已收集 {0} 资料", Bestiary.NameOf(record)) : Lang.F("发现了新敌人 - {0}", Bestiary.NameOf(record));

        public static void Draw()
        {
            var e = Event.current;
            if (e.type != EventType.Repaint) return;
            if (!showing)
            {
                if (waiting.Count == 0) return;
                current = waiting.Dequeue();
                showing = true;
                age = 0f;
                DevShown++;
                DevLast = TextOf(current.Record, current.Collected);
                TerraMod.Instance?.PlaySound(current.Collected ? "Grab" : "Menu_Open", 0.6f);
            }
            age += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (age > In + Hold + Out)
            {
                showing = false;
                return;
            }
            float ui = GameView.Scale;
            var view = GameView.Rect;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
                headStyle = new GUIStyle(style);
            }
            style.fontSize = Mathf.RoundToInt(19 * ui);
            headStyle.fontSize = Mathf.RoundToInt(13 * ui);
            string text = TextOf(current.Record, current.Collected);
            string head = Lang.T(current.Collected ? "怪物图鉴 · 资料收集" : "怪物图鉴 · 新发现");
            float tw = style.CalcSize(new GUIContent(text)).x;
            float w = Mathf.Max(300f * ui, tw + 96f * ui), h = 62f * ui;
            float a = age < In ? age / In : age > In + Hold ? 1f - (age - In - Hold) / Out : 1f;
            float rise = (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / In))) * 18f * ui;
            var panel = new Rect(view.center.x - w / 2f, view.yMax - 150f * ui + rise, w, h);
            var prev = GUI.color;
            // The book's dark blue, a lighter rim, and a band of the note's colour along the left.
            var accent = current.Collected ? new Color(1f, 0.82f, 0.3f) : new Color(0.45f, 0.75f, 1f);
            GUI.color = new Color(0f, 0f, 0f, 0.35f * a);
            GUI.DrawTexture(new Rect(panel.x + 4f * ui, panel.y + 5f * ui, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(33 / 255f, 43 / 255f, 92 / 255f, 0.94f * a);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = new Color(89 / 255f, 116 / 255f, 213 / 255f, a);
            Frame(panel, 2f * ui);
            GUI.color = new Color(accent.r, accent.g, accent.b, a);
            GUI.DrawTexture(new Rect(panel.x, panel.y, 5f * ui, h), Texture2D.whiteTexture);
            // The portrait in a book slot.
            var slot = new Rect(panel.x + 14f * ui, panel.y + 7f * ui, 48f * ui, 48f * ui);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.DrawTexture(slot, TerrariaAssets.Texture("UI/Bestiary/Slot_Back"));
            var sprite = EnemyHover.PortraitOf(current.Record);
            if (sprite != null) DrawSprite(new Rect(slot.x + 4f * ui, slot.y + 4f * ui, slot.width - 8f * ui, slot.height - 8f * ui), sprite, new Color(1f, 1f, 1f, a));
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.DrawTexture(slot, TerrariaAssets.Texture("UI/Bestiary/Slot_Front"));
            GUI.color = prev;
            Shadowed(new Rect(slot.xMax + 12f * ui, panel.y + 6f * ui, w - 90f * ui, 18f * ui), head, headStyle, new Color(accent.r, accent.g, accent.b, a), a);
            Shadowed(new Rect(slot.xMax + 12f * ui, panel.y + 24f * ui, w - 90f * ui, 30f * ui), text, style, new Color(1f, 1f, 1f, a), a);
        }

        static void Frame(Rect r, float t)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
        }

        static void Shadowed(Rect r, string text, GUIStyle s, Color c, float a)
        {
            float ui = GameView.Scale;
            s.normal.textColor = new Color(0f, 0f, 0f, 0.8f * a);
            GUI.Label(new Rect(r.x + 1.5f * ui, r.y + 1.5f * ui, r.width, r.height), text, s);
            s.normal.textColor = c;
            GUI.Label(r, text, s);
        }

        /// <summary>A Unity sprite fitted into a box, keeping its shape (its part of the atlas).</summary>
        static readonly System.Collections.Generic.Dictionary<Sprite, (Rect Tr, Rect Uv)> spriteRects = new System.Collections.Generic.Dictionary<Sprite, (Rect, Rect)>();

        /// <summary>Dev: sprites whose rect had to come from their UVs (packed tight: textureRect throws).</summary>
        public static int DevTightSprites;

        /// <summary>A sprite's part of its texture (pixels) and as UVs: from textureRect, or for a tightly packed one the box of its UVs.</summary>
        static (Rect Tr, Rect Uv) RectsOf(Sprite sprite)
        {
            if (spriteRects.TryGetValue(sprite, out var r)) return r;
            var tex = sprite.texture;
            Rect tr;
            if (sprite.packed && sprite.packingMode == SpritePackingMode.Tight)
            {
                DevTightSprites++;
                var uvs = sprite.uv;
                Vector2 lo = uvs.Length > 0 ? uvs[0] : Vector2.zero, hi = lo;
                foreach (var u in uvs) { lo = Vector2.Min(lo, u); hi = Vector2.Max(hi, u); }
                tr = new Rect(lo.x * tex.width, lo.y * tex.height, (hi.x - lo.x) * tex.width, (hi.y - lo.y) * tex.height);
            }
            else
            {
                try { tr = sprite.textureRect; }
                catch { tr = sprite.rect; }
            }
            r = (tr, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
            if (spriteRects.Count > 2048) spriteRects.Clear();
            spriteRects[sprite] = r;
            return r;
        }

        public static void DrawSprite(Rect box, Sprite sprite, Color tint)
        {
            if (sprite == null || sprite.texture == null) return;
            var (tr, uv) = RectsOf(sprite);
            var tex = sprite.texture;
            float k = Mathf.Min(box.width / tr.width, box.height / tr.height);
            float w = tr.width * k, h = tr.height * k;
            var r = new Rect(box.center.x - w / 2f, box.center.y - h / 2f, w, h);
            var prev = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(r, tex, uv);
            GUI.color = prev;
        }

        public static void Clear()
        {
            waiting.Clear();
            showing = false;
        }
    }
}
