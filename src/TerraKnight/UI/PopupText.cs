using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// PopupText for picked-up items: "name (count)" in the item's rarity colour pops out of the
    /// player's middle, rises about 34 px and fades after 90 ticks. Picking up more of the same
    /// while it shows adds to its count and makes it pop again; popups that overlap push the
    /// higher one up. Twenty at most. Ticks at 60 Hz (<see cref="Tick"/>).
    /// </summary>
    internal static class PopupText
    {
        const float Px = Effects.TerraParticles.Px;
        const int MaxPopups = 20;

        sealed class Popup
        {
            public bool Active;
            public string Name;
            public int Stack;
            public Color Color;
            /// <summary>Centre, world units.</summary>
            public Vector2 Pos;
            /// <summary>px per tick, y up.</summary>
            public Vector2 Vel;
            public int Life;
            public float Scale, Alpha;
            public int AlphaDir;
            public string Text => Stack > 1 ? $"{Name} ({Stack})" : Name;
            /// <summary>A rough size in Terraria px, for the overlap test.</summary>
            public Vector2 Size => new Vector2(Mathf.Max(20f, Text.Length * 10f), 22f);
        }

        static readonly Popup[] popups = new Popup[MaxPopups];
        static GUIStyle style;

        /// <summary>PopupText.NewText(ItemPickup, item, player.Center, added).</summary>
        public static void Show(ItemDef def, int added, Vector2 at)
        {
            if (def == null || added <= 0) return;
            var color = ItemDb.RarityColor(def.Rare);
            foreach (var p in popups)
            {
                if (p == null || !p.Active || p.Name != def.Name) continue;
                p.Stack += added;
                if (p.Life < 0) p.Scale = 1f;
                p.Scale = 0f;
                p.Life = Mathf.Max(p.Life, 60);
                p.Vel.y = 7f;
                p.Pos = at;
                p.Color = color;
                return;
            }
            int slot = -1;
            for (int i = 0; i < MaxPopups && slot < 0; i++) if (popups[i] == null || !popups[i].Active) slot = i;
            if (slot < 0)
            {
                // All in use: the highest on screen makes way.
                slot = 0;
                for (int i = 1; i < MaxPopups; i++) if (popups[i].Pos.y > popups[slot].Pos.y) slot = i;
            }
            popups[slot] = new Popup
            {
                Active = true, Name = def.Name, Stack = added, Color = color, Pos = at,
                Vel = new Vector2(0f, 7f), Life = 60, Scale = 0f, Alpha = 1f, AlphaDir = -1,
            };
        }

        public static void Clear()
        {
            for (int i = 0; i < MaxPopups; i++) popups[i] = null;
        }

        /// <summary>PopupText.Update, one tick.</summary>
        public static void Tick()
        {
            int active = 0;
            foreach (var p in popups) if (p != null && p.Active) active++;
            for (int i = 0; i < MaxPopups; i++)
            {
                var p = popups[i];
                if (p == null || !p.Active) continue;
                p.Alpha += p.AlphaDir * 0.01f;
                if (p.Alpha <= 0.7f) { p.Alpha = 0.7f; p.AlphaDir = 1; }
                if (p.Alpha >= 1f) { p.Alpha = 1f; p.AlphaDir = -1; }
                bool pushed = false;
                var me = new Rect(p.Pos / Px - p.Size / 2f, p.Size);
                for (int j = 0; j < MaxPopups; j++)
                {
                    var o = popups[j];
                    if (j == i || o == null || !o.Active) continue;
                    var other = new Rect(o.Pos / Px - o.Size / 2f, o.Size);
                    // The higher one (ties by index) makes way upwards.
                    if (!me.Overlaps(other) || p.Pos.y < o.Pos.y || (p.Pos.y == o.Pos.y && i > j)) continue;
                    pushed = true;
                    int life = 60 + 15 * Mathf.Min(active, 3);
                    p.Life = Mathf.Max(p.Life, life);
                    o.Life = Mathf.Max(o.Life, life);
                }
                if (pushed)
                {
                    if (p.Vel.y < 6f) p.Vel.y += 0.2f;
                    else p.Vel.y *= 0.86f;
                }
                else
                {
                    p.Vel.y *= 0.86f;
                    if (p.Scale >= 1f) p.Vel.y *= 0.4f;
                }
                p.Vel.x *= 0.93f;
                p.Pos += p.Vel * Px;
                p.Life--;
                if (p.Life > 0) p.Scale = Mathf.Min(1f, p.Scale + 0.1f);
                else
                {
                    p.Scale -= 0.03f;
                    if (p.Scale < 0.1f) p.Active = false;
                }
            }
        }

        /// <summary>Four black passes 2 px off each way, then the text, faded by scale and alpha.</summary>
        public static void Draw()
        {
            if (Event.current.type != EventType.Repaint) return;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (cam == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            float ui = GameView.Scale;
            foreach (var p in popups)
            {
                if (p == null || !p.Active || p.Scale <= 0f) continue;
                var s = cam.WorldToScreenPoint(new Vector3(p.Pos.x, p.Pos.y, 0f));
                if (s.z < 0f) continue;
                style.fontSize = Mathf.Max(1, Mathf.RoundToInt(18f * ui * p.Scale));
                string text = p.Text;
                var size = style.CalcSize(new GUIContent(text));
                var r = new Rect(s.x - size.x / 2f, Screen.height - s.y - size.y / 2f, size.x, size.y);
                float a = p.Scale * p.Alpha;
                style.normal.textColor = new Color(0f, 0f, 0f, a);
                float o = 2f * ui * p.Scale;
                GUI.Label(new Rect(r.x - o, r.y, r.width, r.height), text, style);
                GUI.Label(new Rect(r.x + o, r.y, r.width, r.height), text, style);
                GUI.Label(new Rect(r.x, r.y - o, r.width, r.height), text, style);
                GUI.Label(new Rect(r.x, r.y + o, r.width, r.height), text, style);
                var c = p.Color;
                style.normal.textColor = new Color(c.r, c.g, c.b, a);
                GUI.Label(r, text, style);
            }
        }
    }
}
