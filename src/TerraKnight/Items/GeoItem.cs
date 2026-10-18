using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>The Geo item's picture: the game's own geo (its coin sprite), made a small texture; Terraria's gold coin until it is found.</summary>
    internal static class GeoItem
    {
        public const string Icon = "HK_Geo";
        static bool fromGame, logged;
        static float nextLook;
        static int looks;

        public static void EnsureIcon()
        {
            if (fromGame) return;
            if (!TerrariaAssets.Has(Icon)) TerrariaAssets.Put(Icon, TerrariaAssets.Texture("Item_73"));
            // A look every five seconds for a minute; the stand-in stays if the game's isn't loaded by then.
            if (Time.unscaledTime < nextLook || looks >= 12) return;
            nextLook = Time.unscaledTime + 5f;
            looks++;
            // The geo counter's own coin, in the corner of the screen.
            var hero = HeroController.SilentInstance;
            var coin = hero != null && hero.geoCounter != null ? hero.geoCounter.geoSprite : null;
            if (coin != null)
            {
                Texture2D got = null;
                var sr = coin.GetComponentInChildren<SpriteRenderer>(true);
                if (sr != null && sr.sprite != null) got = DreamNailItem.Capture(sr.sprite, 26, Icon);
                var tk = got == null ? coin.GetComponentInChildren<tk2dBaseSprite>(true) : null;
                if (tk != null) got = CaptureTk2d(tk, 26);
                if (got != null)
                {
                    TerrariaAssets.Put(Icon, got);
                    fromGame = true;
                    Plugin.Log.LogInfo($"[吉欧] 图标用吉欧计数器的硬币（{got.width}x{got.height}）");
                    return;
                }
            }
            Sprite best = null;
            int bestScore = 0;
            var names = new System.Text.StringBuilder();
            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (s == null) continue;
                string n = s.name.ToLowerInvariant();
                if (!n.Contains("geo")) continue;
                if (names.Length < 600) names.Append(s.name).Append(' ');
                if (n.Contains("rock") || n.Contains("pin") || n.Contains("map") || n.Contains("deposit") || n.Contains("stack") || n.Contains("charm") || n.Contains("bestiary")) continue;
                int score = 1 + (n.Contains("hud") ? 4 : 0) + (n.Contains("coin") || n.Contains("icon") ? 3 : 0) + (n.Contains("small") ? 1 : 0);
                if (score > bestScore) { best = s; bestScore = score; }
            }
            if (names.Length > 0 && !logged) { logged = true; Plugin.Log.LogInfo($"[吉欧] 名字里带 geo 的图：{names}"); }
            if (best == null) return;
            var tex = DreamNailItem.Capture(best, 26, Icon);
            if (tex == null) return;
            TerrariaAssets.Put(Icon, tex);
            fromGame = true;
            Plugin.Log.LogInfo($"[吉欧] 图标用原版的 {best.name}（{best.rect.width}x{best.rect.height} → {tex.width}x{tex.height}）");
        }

        /// <summary>A tk2d sprite's current frame (its rectangle of the atlas, by its UVs) drawn into a texture of the given height.</summary>
        internal static Texture2D CaptureTk2d(tk2dBaseSprite tk, int height, string name = Icon) => CaptureTk2d(tk.GetCurrentSpriteDef(), height, name);

        /// <summary>A tk2d sprite definition (its rectangle of the atlas, by its UVs) drawn into a texture of the given height.</summary>
        internal static Texture2D CaptureTk2d(tk2dSpriteDefinition def, int height, string name = Icon)
        {
            var src = def?.material != null ? def.material.mainTexture : null;
            if (def == null || src == null || def.uvs == null || def.uvs.Length < 4) return null;
            float u0 = float.MaxValue, v0 = float.MaxValue, u1 = float.MinValue, v1 = float.MinValue;
            foreach (var uv in def.uvs) { u0 = Mathf.Min(u0, uv.x); v0 = Mathf.Min(v0, uv.y); u1 = Mathf.Max(u1, uv.x); v1 = Mathf.Max(v1, uv.y); }
            float pw = (u1 - u0) * src.width, ph = (v1 - v0) * src.height;
            if (pw < 1f || ph < 1f) return null;
            // A frame stored turned (FlipMode.Tk2d) is as tall as it is wide the other way: drawn as stored.
            int h = Mathf.Max(1, height), w = Mathf.Max(1, Mathf.RoundToInt(height * pw / ph));
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt, new Vector2(u1 - u0, v1 - v0), new Vector2(u0, v0));
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply(false, false);
                return tex;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
