using System.Collections.Generic;
using TerraKnight.Player;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Terraria's classic life and mana display in the top-right corner, replacing Hollow Knight's
    /// masks and soul vessel, which are kept running but never drawn. The hearts as Terraria's
    /// (ClassicPlayerResourcesDisplaySet): 20 life each up to 400, past that the hearts' life over
    /// twenty hearts; one golden (Heart2) for each Life Fruit eaten, from the first. After them the
    /// Knight's Mask Shards, cyan hearts of 20 (two shards each), the last of the life to go.
    /// </summary>
    internal static class ResourceHud
    {
        const int HeartsPerRow = 10;
        const int LifePerHeart = 20;
        const int ManaPerStar = 20;
        const float HeartStep = 26f;
        const float StarStep = 28f;

        static GUIStyle textStyle;

        /// <summary>Rows of hearts beyond Terraria's two (the masks' cyan ones): what sits under them moves down as many rows.</summary>
        public static int ExtraHeartRows { get; private set; }

        /// <summary>A row of hearts, in unscaled pixels.</summary>
        public const float RowStep = HeartStep;
        static readonly List<Renderer> hiddenHud = new List<Renderer>();
        static GameObject hiddenFor;

        /// <summary>Keeps the game's HUD invisible. Its FSMs still run, only the renderers are off.</summary>
        public static void HideGameHud()
        {
            var cams = GameCameras.instance;
            if (cams == null || cams.hudCanvas == null) return;
            if (hiddenFor != cams.hudCanvas || Time.frameCount % 60 == 0)
            {
                hiddenFor = cams.hudCanvas;
                hiddenHud.Clear();
                cams.hudCanvas.GetComponentsInChildren(true, hiddenHud);
            }
            foreach (var r in hiddenHud)
                if (r != null && r.enabled) r.enabled = false;
        }

        public static void Draw(TerraStats stats)
        {
            if (Event.current.type != EventType.Repaint) return;
            var view = GameView.Rect;
            float ui = GameView.Scale;
            if (textStyle == null) textStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            textStyle.fontSize = Mathf.RoundToInt(20 * ui);

            var heart = TerrariaAssets.Texture("Heart");
            var golden = TerrariaAssets.Texture("Heart2") ?? heart;
            var cyan = Cyan(heart);
            var silver = Silver(heart);
            var star = TerrariaAssets.Texture("Mana");
            float top = view.y + 16f * ui;

            // Life: text, then rows of ten hearts to the left of the mana column: Terraria's, then the masks'.
            float right = view.xMax - 70f * ui;
            int heartLife = Mathf.Max(0, stats.LifeMax - stats.MaskLife);
            float perHeart = heartLife <= 400 ? LifePerHeart : heartLife / 20f;
            int red = Mathf.CeilToInt(heartLife / perHeart - 0.001f);
            int masks = Mathf.CeilToInt(stats.MaskLife / (float)LifePerHeart);
            int shields = Mathf.CeilToInt(stats.ShieldShown / (float)LifePerHeart);
            int heartCount = red + masks + shields;
            ExtraHeartRows = Mathf.Max(0, (heartCount + HeartsPerRow - 1) / HeartsPerRow - 2);
            float hearts = Mathf.Min(HeartsPerRow, heartCount);
            float rowWidth = Mathf.Max(hearts * HeartStep * ui, 200f * ui);
            float x0 = right - rowWidth;
            string lifeText = Lang.F("生命值：{0}/{1}", stats.Life, stats.LifeMax) + (stats.ShieldShown > 0 ? " +" + stats.ShieldShown : "");
            // As wide as the words need (the shield's "+40" on the same line), widening to the left.
            float textWidth = Mathf.Max(rowWidth, textStyle.CalcSize(new GUIContent(lifeText)).x + 12f * ui);
            Outlined(new Rect(right - textWidth, top, textWidth, 26f * ui), lifeText, Color.white);

            for (int i = 0; i < heartCount; i++)
            {
                float from, size;
                Texture2D tex;
                if (i < red)
                {
                    from = i * perHeart;
                    size = Mathf.Min(perHeart, heartLife - from);
                    tex = i < stats.LifeFruits ? golden : heart;
                }
                else if (i < red + masks)
                {
                    from = heartLife + (i - red) * LifePerHeart;
                    size = Mathf.Min(LifePerHeart, stats.LifeMax - from);
                    tex = cyan;
                }
                else
                {
                    // Lifeblood's silver hearts, after all of the life: the shield left in them.
                    from = (i - red - masks) * LifePerHeart;
                    size = Mathf.Min(LifePerHeart, stats.ShieldShown - from);
                    tex = silver;
                }
                float fill = size > 0f ? Mathf.Clamp01(((i < red + masks ? stats.Life : stats.ShieldShown) - from) / size) : 0f;
                if (fill <= 0f) continue;
                // A part-filled heart shrinks and fades like Terraria's.
                float s = fill >= 1f ? 1f : Mathf.Lerp(0.5f, 1f, fill);
                // The Fragile Heart worn: a slow wave swells through the masks' cyan hearts.
                if (i >= red && i < red + masks) s *= 1f + 0.16f * Items.CharmEffects.MaskWave(i - red, masks);
                float w = heart.width * ui * s, h = heart.height * ui * s;
                int col = i % HeartsPerRow, row = i / HeartsPerRow;
                float cx = right - (Mathf.Min(heartCount, HeartsPerRow) - col - 0.5f) * HeartStep * ui;
                float cy = top + 40f * ui + row * HeartStep * ui;
                var prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, fill >= 1f ? 1f : Mathf.Lerp(0.4f, 1f, fill));
                GUI.DrawTexture(new Rect(cx - w / 2f, cy - h / 2f, w, h), tex);
                GUI.color = prev;
            }

            // Mana: a vertical column of stars at the far right.
            float mx = view.xMax - 36f * ui;
            Outlined(new Rect(mx - 30f * ui, top, 60f * ui, 26f * ui), Lang.T("魔力"), Color.white);
            int stars = Mathf.CeilToInt(stats.ManaMax / (float)ManaPerStar);
            for (int i = 0; i < stars; i++)
            {
                float fill = Mathf.Clamp01((stats.Mana - i * ManaPerStar) / (float)ManaPerStar);
                float s = fill >= 1f ? 1f : Mathf.Lerp(0.5f, 1f, fill);
                float w = star.width * ui * s, h = star.height * ui * s;
                float cy = top + 40f * ui + i * StarStep * ui;
                var prev = GUI.color;
                // Lifeblood's stars, the last ones, pale: gone at death.
                bool temp = i >= stars - Mathf.CeilToInt(stats.TempMana / (float)ManaPerStar);
                GUI.color = new Color(1f, 1f, 1f, fill <= 0f ? 0.25f : Mathf.Lerp(0.4f, 1f, fill));
                GUI.DrawTexture(new Rect(mx - w / 2f, cy - h / 2f, w, h), temp ? Pale(star) : star);
                GUI.color = prev;
            }
            // The cursor over the hearts or the stars: their life or mana as numbers beside it (ClassicPlayerResourcesDisplaySet's
            // MouseTextHackZoom, "statLife/statLifeMax2", "statMana/statManaMax2").
            var mouse = DevMouse ?? Event.current.mousePosition;
            int heartRows = Mathf.Max(1, (heartCount + HeartsPerRow - 1) / HeartsPerRow);
            var heartArea = new Rect(right - Mathf.Min(heartCount, HeartsPerRow) * HeartStep * ui, top + 40f * ui - HeartStep * ui * 0.5f,
                Mathf.Min(heartCount, HeartsPerRow) * HeartStep * ui, heartRows * HeartStep * ui);
            var starArea = new Rect(mx - StarStep * ui * 0.5f, top + 40f * ui - StarStep * ui * 0.5f, StarStep * ui, Mathf.Max(1, stars) * StarStep * ui);
            DevHover = heartArea.Contains(mouse) ? stats.Life + "/" + stats.LifeMax : starArea.Contains(mouse) ? stats.Mana + "/" + stats.ManaMax : null;
            if (DevHover != null) EnemyHover.MouseText(DevHover, Color.white, mouse);
            DevStarCenter = starArea.center;
            DevHeartCenter = heartArea.center;
        }

        /// <summary>Dev: the cursor taken as here (GUI coordinates); what its hover showed; the areas' centres last drawn.</summary>
        public static Vector2? DevMouse;
        public static string DevHover;
        public static Vector2 DevStarCenter, DevHeartCenter;

        static Texture2D cyanHeart;
        static Texture2D cyanFrom;

        /// <summary>
        /// The heart recoloured for a Mask Shard's: its red as cyan, its white shine and dark rim kept (made once, read
        /// back through a render texture, the heart's own pixels not being readable).
        /// </summary>
        static Texture2D Cyan(Texture2D heart)
        {
            if (heart == null) return null;
            if (cyanHeart != null && cyanFrom == heart) return cyanHeart;
            var rt = RenderTexture.GetTemporary(heart.width, heart.height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(heart, rt);
            RenderTexture.active = rt;
            var t = new Texture2D(heart.width, heart.height, TextureFormat.RGBA32, false) { filterMode = heart.filterMode };
            t.ReadPixels(new Rect(0, 0, heart.width, heart.height), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = t.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                px[i] = new Color(c.g, c.r * 0.85f + c.g * 0.15f, Mathf.Max(c.r, c.b), c.a);
            }
            t.SetPixels(px);
            t.Apply();
            cyanHeart = t;
            cyanFrom = heart;
            return t;
        }

        static Texture2D paleStar;
        static Texture2D paleFrom;

        /// <summary>The mana star half-way to white, for lifeblood's stars.</summary>
        static Texture2D Pale(Texture2D star)
        {
            if (star == null) return null;
            if (paleStar != null && paleFrom == star) return paleStar;
            paleStar = Recolor(star, c => new Color(Mathf.Lerp(c.r, 1f, 0.55f), Mathf.Lerp(c.g, 1f, 0.55f), Mathf.Lerp(c.b, 1f, 0.35f), c.a));
            paleFrom = star;
            return paleStar;
        }

        static Texture2D silverHeart;
        static Texture2D silverFrom;

        /// <summary>The heart recoloured silver for lifeblood's shield (its red as a pale grey, the shine kept).</summary>
        static Texture2D Silver(Texture2D heart)
        {
            if (heart == null) return null;
            if (silverHeart != null && silverFrom == heart) return silverHeart;
            silverHeart = Recolor(heart, c =>
            {
                float v = Mathf.Max(c.r, c.g) * 0.8f + 0.12f * c.a;
                return new Color(v, v, Mathf.Min(1f, v * 1.06f), c.a);
            });
            silverFrom = heart;
            return silverHeart;
        }

        static Texture2D Recolor(Texture2D src, System.Func<Color, Color> map)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { filterMode = src.filterMode };
            t.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = t.GetPixels();
            for (int i = 0; i < px.Length; i++) px[i] = map(px[i]);
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static void Outlined(Rect r, string text, Color color) =>
            BorderText.Draw(r, text, textStyle, new Color(0f, 0f, 0f, 0.85f), color, 1.5f * GameView.Scale);
    }
}
