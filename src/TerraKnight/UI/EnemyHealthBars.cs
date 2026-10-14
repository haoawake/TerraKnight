using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Terraria's health bars over Hallownest's enemies. Every enemy that has lost life gets the
    /// small bar under it (Main.DrawHealthBar: HealthBar1 filling over HealthBar2, green at full
    /// through yellow to red), at 1.5 times the size for bosses; the nearest boss also gets the
    /// big bar at the bottom of the screen (BigProgressBarHelper.DrawFancyBar on UI_BossBar) with its
    /// life written on it. Life is in Terraria points (<see cref="EnemyStats"/>).
    /// </summary>
    internal static class EnemyHealthBars
    {
        const float Px = Effects.TerraParticles.Px;
        /// <summary>Show every enemy's bar, not only the hurt ones (config).</summary>
        public static bool Always;

        static GUIStyle text;
        static float nextPrune;

        public static void Draw()
        {
            if (Event.current.type != EventType.Repaint) return;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            var hero = HeroController.SilentInstance;
            if (cam == null || hero == null) return;
            if (Time.unscaledTime > nextPrune)
            {
                nextPrune = Time.unscaledTime + 5f;
                EnemyStats.Prune();
            }
            Vector2 heroPos = hero.transform.position;
            EnemyStats.Info boss = null;
            float bossDist = float.MaxValue;
            foreach (var info in EnemyStats.All)
            {
                var hm = info.Hm;
                if (hm == null || hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy) continue;
                int life = EnemyStats.Life(hm, info), max = EnemyStats.LifeMax(info);
                if (info.Boss)
                {
                    float d = Vector2.Distance(hm.transform.position, heroPos);
                    if (d < 45f && d < bossDist && Visible(hm))
                    {
                        boss = info;
                        bossDist = d;
                    }
                }
                if (life >= max && !Always) continue;
                var bounds = Bounds(hm);
                if (bounds.size == Vector3.zero) continue;
                // Under the NPC: 10 px below its hitbox, centred.
                var at = new Vector3(bounds.center.x, bounds.min.y - 10f * Px, bounds.center.z);
                SmallBar(cam, at, life, max, info.Boss ? 1.5f : 1f);
            }
            if (boss != null) BossBar(boss);
        }

        static bool Visible(HealthManager hm)
        {
            foreach (var r in hm.GetComponentsInChildren<Renderer>())
                if (r.enabled && r.isVisible) return true;
            return false;
        }

        static Bounds Bounds(HealthManager hm)
        {
            var col = hm.GetComponent<Collider2D>();
            if (col != null && col.enabled) return col.bounds;
            var r = hm.GetComponent<Renderer>();
            return r != null ? r.bounds : default;
        }

        /// <summary>Main.DrawHealthBar: x is the centre, y the top, in world space.</summary>
        internal static void SmallBar(Camera cam, Vector3 world, int life, int max, float scale)
        {
            if (life <= 0) return;
            var hb1 = TerrariaAssets.Texture("HealthBar1");
            var hb2 = TerrariaAssets.Texture("HealthBar2");
            float f = Mathf.Min(1f, life / (float)Mathf.Max(1, max));
            int w = Mathf.Max(3, (int)(36f * f));
            float g = f - 0.1f;
            float R, G;
            if (g > 0.5f) { G = 255f; R = 255f * (1f - g) * 2f; }
            else { G = 255f * g * 2f; R = 255f; }
            R = Mathf.Clamp(R * 0.95f, 0f, 255f);
            G = Mathf.Clamp(G * 0.95f, 0f, 255f);
            var color = new Color(R / 255f, G / 255f, 0f, 0.95f);
            // World pixels to screen: project the bar's corners at the enemy's depth.
            var left = cam.WorldToScreenPoint(world + Vector3.left * (18f * scale * Px));
            var right = cam.WorldToScreenPoint(world + Vector3.right * (18f * scale * Px));
            if (left.z < 0f) return;
            float px = (right.x - left.x) / 36f; // screen pixels per bar pixel
            float x0 = left.x, y0 = Screen.height - left.y, h = hb1.height * px;
            var prev = GUI.color;
            GUI.color = color;
            if (w < 34)
            {
                Part(hb2, 2, 2, x0 + w * px, y0, px, h);
                Part(hb2, w + 2, 36 - w - 2, x0 + (w + 2) * px, y0, px, h);
                if (w > 2) Part(hb1, 0, w - 2, x0, y0, px, h);
                Part(hb1, 32, 2, x0 + (w - 2) * px, y0, px, h);
            }
            else
            {
                if (w < 36) Part(hb2, w, 36 - w, x0 + w * px, y0, px, h);
                Part(hb1, 0, w, x0, y0, px, h);
            }
            GUI.color = prev;
        }

        /// <summary>Columns [sx, sx+sw) of a bar texture, drawn at a screen position.</summary>
        static void Part(Texture2D tex, int sx, int sw, float x, float y, float px, float h)
        {
            if (sw <= 0) return;
            GUI.DrawTextureWithTexCoords(new Rect(x, y, sw * px, h), tex, new Rect(sx / (float)tex.width, 0f, sw / (float)tex.width, 1f));
        }

        /// <summary>
        /// BigProgressBarHelper.DrawFancyBar: UI_BossBar's six frames (border, fill cap, fill,
        /// background, shield cap, shield fill), a 456x22 bar centred 50 px above the bottom.
        /// </summary>
        static void BossBar(EnemyStats.Info info)
        {
            var hm = info.Hm;
            int life = EnemyStats.Life(hm, info), max = EnemyStats.LifeMax(info);
            var tex = TerrariaAssets.Texture("UI/UI_BossBar");
            var view = GameView.Rect;
            float ui = GameView.Scale;
            float frameW = tex.width, frameH = tex.height / 6f;
            var T = new Vector2(view.center.x - 228f * ui, view.yMax - 61f * ui);
            var O = T - new Vector2(32f, 24f) * ui;
            Rect Frame(int k) => new Rect(0f, 1f - (k + 1) / 6f, 1f, 1f / 6f);
            // Background at 20%.
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            GUI.DrawTextureWithTexCoords(new Rect(O.x, O.y, frameW * ui, frameH * ui), tex, Frame(3));
            GUI.color = prev;
            int w = (int)(456f * Mathf.Clamp01(life / (float)Mathf.Max(1, max)));
            w -= w % 2;
            if (w > 0)
            {
                // The fill: a 2 px column of frame 2 stretched, then the end cap from frame 1.
                GUI.DrawTextureWithTexCoords(new Rect(T.x, T.y, w * ui, 22f * ui), tex, Column(2, frameH, tex));
                GUI.DrawTextureWithTexCoords(new Rect(T.x + (w - 2) * ui, T.y, 2f * ui, 22f * ui), tex, Column(1, frameH, tex));
            }
            GUI.DrawTextureWithTexCoords(new Rect(O.x, O.y, frameW * ui, frameH * ui), tex, Frame(0));

            if (text == null) text = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            text.fontSize = Mathf.RoundToInt(16 * ui);
            var c = new Vector2(T.x + 228f * ui, T.y + 12f * ui);
            Outlined(new Rect(c.x - 10f * ui, c.y - 12f * ui, 20f * ui, 24f * ui), "/", TextAnchor.MiddleCenter);
            Outlined(new Rect(c.x - 205f * ui, c.y - 12f * ui, 200f * ui, 24f * ui), life.ToString(), TextAnchor.MiddleRight);
            Outlined(new Rect(c.x + 5f * ui, c.y - 12f * ui, 200f * ui, 24f * ui), max.ToString(), TextAnchor.MiddleLeft);
        }

        /// <summary>The bar's 2 px source column inside frame k (the interior starts at (32, 24) in each frame).</summary>
        static Rect Column(int k, float frameH, Texture2D tex)
        {
            float x = 32f / tex.width, w = 2f / tex.width;
            float top = k * frameH + 24f;
            return new Rect(x, 1f - (top + 22f) / tex.height, w, 22f / tex.height);
        }

        static void Outlined(Rect r, string s, TextAnchor anchor)
        {
            float ui = GameView.Scale;
            text.alignment = anchor;
            text.normal.textColor = Color.black;
            for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                    if (ox != 0 || oy != 0) GUI.Label(new Rect(r.x + ox * 1.5f * ui, r.y + oy * 1.5f * ui, r.width, r.height), s, text);
            text.normal.textColor = Color.white;
            GUI.Label(r, s, text);
        }
    }
}
