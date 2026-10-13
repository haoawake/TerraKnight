using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>Terraria's floating combat numbers: pop up over whoever was hit, drift up and fade.</summary>
    internal static class CombatText
    {
        public static readonly Color PlayerHurt = new Color(1f, 0.31f, 0.31f);
        public static readonly Color EnemyHurt = new Color(1f, 0.63f, 0.31f);
        public static readonly Color EnemyCrit = new Color(1f, 0.39f, 0.12f);
        public static readonly Color Heal = new Color(0.39f, 1f, 0.39f);
        public static readonly Color Pickup = new Color(0.6f, 0.85f, 1f);
        /// <summary>Terraria's CombatText.HealMana.</summary>
        public static readonly Color Mana = new Color(0.39f, 0.39f, 1f);
        public static readonly Color Notice = new Color(1f, 0.85f, 0.45f);

        const float Lifetime = 1.1f;

        sealed class Entry
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Age;
            public float Scale;
            public float DriftX;
        }

        static readonly List<Entry> entries = new List<Entry>();
        static GUIStyle style;

        public static void Show(Vector3 world, string text, Color color, bool big = false)
        {
            entries.Add(new Entry
            {
                World = world,
                Text = text,
                Color = color,
                Scale = big ? 1.4f : 1f,
                DriftX = Random.Range(-0.4f, 0.4f),
            });
        }

        public static void Tick(float dt)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                e.Age += dt;
                e.World += new Vector3(e.DriftX * dt, Mathf.Lerp(2.2f, 0.2f, e.Age / Lifetime) * dt, 0f);
                if (e.Age >= Lifetime) entries.RemoveAt(i);
            }
        }

        public static void Draw()
        {
            if (Event.current.type != EventType.Repaint || entries.Count == 0) return;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (cam == null) return;
            float ui = GameView.Scale;
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

            foreach (var e in entries)
            {
                var sp = cam.WorldToScreenPoint(e.World);
                if (sp.z < 0) continue;
                float pop = e.Age < 0.12f ? Mathf.Lerp(1.6f, 1f, e.Age / 0.12f) : 1f;
                style.fontSize = Mathf.RoundToInt(28 * ui * e.Scale * pop);
                float alpha = Mathf.Clamp01((Lifetime - e.Age) / 0.4f);
                var rect = new Rect(sp.x - 150, Screen.height - sp.y - 30, 300, 60);
                // Terraria draws its numbers with a dark outline.
                style.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.8f);
                for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                    if (ox != 0 || oy != 0) GUI.Label(new Rect(rect.x + ox * 2 * ui, rect.y + oy * 2 * ui, rect.width, rect.height), e.Text, style);
                var c = e.Color;
                c.a = alpha;
                style.normal.textColor = c;
                GUI.Label(rect, e.Text, style);
            }
        }
    }
}
