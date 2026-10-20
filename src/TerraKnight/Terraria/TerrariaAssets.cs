using System.Collections.Generic;
using System.IO;
using TerraKnight.Terraria.Xnb;
using UnityEngine;

namespace TerraKnight.Terraria
{
    /// <summary>
    /// Textures read at runtime from the player's own Terraria install. The mod ships no
    /// Terraria art, so it only works when both games are installed.
    /// </summary>
    internal static class TerrariaAssets
    {
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();

        public static string ImagesDir { get; private set; }
        public static string SoundsDir { get; private set; }

        public static bool Init(string terrariaDir)
        {
            ImagesDir = Path.Combine(terrariaDir, "Content", "Images");
            SoundsDir = Path.Combine(terrariaDir, "Content", "Sounds");
            return File.Exists(Path.Combine(ImagesDir, "Tiles_0.xnb"));
        }

        public static AudioClip Sound(string name)
        {
            if (sounds.TryGetValue(name, out var clip)) return clip;

            var audio = XnbSound.Load(Path.Combine(SoundsDir, name + ".xnb"));
            clip = AudioClip.Create("Terraria/" + name, audio.Samples.Length / audio.Channels, audio.Channels, audio.SampleRate, false);
            clip.SetData(audio.Samples, 0);
            sounds[name] = clip;
            return clip;
        }

        public static Texture2D Texture(string name)
        {
            // Generated from this player's Hollow Knight portraits, used everywhere an item draws.
            if (name != null && name.StartsWith(Items.BossSummons.IconPrefix)) return Items.BossSummons.Icon(name);
            // A relic's picture: the mod's own (Resources/relic_*.png).
            if (name != null && name.StartsWith(Items.Relics.TexturePrefix)) return Items.Relics.Texture(name);
            // Hallownest's key items: the game's own inventory pictures.
            if (name != null && name.StartsWith(Items.HkItems.TexturePrefix)) return Items.HkItems.Texture(name);
            if (name == "Buff_" + Items.Pets.LanternBuff) return Items.HkItems.Texture(Items.HkItems.TexturePrefix + nameof(Items.ItemType.HkLumaflyLantern));
            if (name == "Buff_" + Items.Pets.GrimmBuff) return Items.HkItems.Texture(Items.HkItems.TexturePrefix + nameof(Items.ItemType.HkGrimmchild));
            if (name == "Buff_" + Items.Charms.OverchargeBuff) return Items.Charms.OverchargeIcon;
            if (Items.CharmSkills.IsCooldownBuff(name)) return Items.CharmSkills.CooldownIcon(name) ?? Items.Charms.OverchargeIcon;
            if (textures.TryGetValue(name, out var tex)) return tex;
            // The mod's own picture for a Terraria one (Resources/hk_<name>.png), drawn in its place.
            tex = Override(name);
            if (tex != null)
            {
                textures[name] = tex;
                return tex;
            }

            var img = XnbTexture.Load(Path.Combine(ImagesDir, name + ".xnb"));
            tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false)
            {
                name = "Terraria/" + name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.LoadRawTextureData(FlipRows(img));
            tex.Apply(false, true);
            textures[name] = tex;
            return tex;
        }

        /// <summary>The mod's replacement for a Terraria texture: the embedded Resources/hk_&lt;name&gt;.png (point-filtered pixel art), or null.</summary>
        static Texture2D Override(string name)
        {
            using (var s = typeof(TerrariaAssets).Assembly.GetManifestResourceStream("TerraKnight.hk_" + name + ".png"))
            {
                if (s == null) return null;
                var bytes = new byte[s.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int n = s.Read(bytes, read, bytes.Length - read);
                    if (n <= 0) break;
                    read += n;
                }
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "TerraKnight/hk_" + name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                ImageConversion.LoadImage(tex, bytes, false);
                return tex;
            }
        }

        /// <summary>A texture made here (a recoloured copy, say) kept under a name, so it is drawn like any of Terraria's.</summary>
        public static void Put(string name, Texture2D tex) => textures[name] = tex;

        public static bool Has(string name) => textures.ContainsKey(name);

        /// <summary>A texture's pixels (bottom row first, as Unity keeps them), to build another texture from.</summary>
        public static Color32[] Pixels(string name, out int width, out int height)
        {
            var own = Override(name);
            if (own != null)
            {
                width = own.width;
                height = own.height;
                var copy = own.GetPixels32();
                Object.Destroy(own);
                return copy;
            }
            var img = XnbTexture.Load(Path.Combine(ImagesDir, name + ".xnb"));
            var raw = FlipRows(img);
            width = img.Width;
            height = img.Height;
            var px = new Color32[width * height];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(raw[i * 4], raw[i * 4 + 1], raw[i * 4 + 2], raw[i * 4 + 3]);
            return px;
        }

        /// <summary>A point-filtered texture made from pixels (bottom row first).</summary>
        public static Texture2D Make(string name, Color32[] pixels, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Terraria/" + name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        static readonly Dictionary<string, Texture2D> silhouettes = new Dictionary<string, Texture2D>();

        /// <summary>A texture's shape in plain white (alpha kept), to tint into one solid colour.</summary>
        public static Texture2D Silhouette(string name)
        {
            if (silhouettes.TryGetValue(name, out var tex)) return tex;
            var img = XnbTexture.Load(Path.Combine(ImagesDir, name + ".xnb"));
            var raw = FlipRows(img);
            for (int k = 0; k < raw.Length; k += 4) raw[k] = raw[k + 1] = raw[k + 2] = 255;
            tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false)
            {
                name = "Terraria/" + name + "#silhouette",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.LoadRawTextureData(raw);
            tex.Apply(false, true);
            silhouettes[name] = tex;
            return tex;
        }

        static readonly Dictionary<Sprite, Sprite> silhouetteSprites = new Dictionary<Sprite, Sprite>();

        /// <summary>The same sprite cut from the white silhouette of its Terraria texture; null for other textures.</summary>
        public static Sprite Silhouette(Sprite sprite)
        {
            if (sprite == null) return null;
            if (silhouetteSprites.TryGetValue(sprite, out var s)) return s;
            const string prefix = "Terraria/";
            string texName = sprite.texture != null ? sprite.texture.name : "";
            if (texName.StartsWith(prefix) && !texName.EndsWith("#silhouette"))
            {
                var white = Silhouette(texName.Substring(prefix.Length));
                var rect = sprite.rect;
                s = UnityEngine.Sprite.Create(white, rect, new Vector2(sprite.pivot.x / rect.width, sprite.pivot.y / rect.height),
                    sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            silhouetteSprites[sprite] = s;
            return s;
        }

        /// <summary>A sprite cut from a texture, with the rectangle in Terraria's top-left pixel coordinates.</summary>
        public static Sprite Sprite(string name, int x, int y, int w, int h, float pixelsPerUnit, Vector2 pivot)
        {
            var tex = Texture(name);
            var rect = new Rect(x, tex.height - y - h, w, h);
            return UnityEngine.Sprite.Create(tex, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        public static Sprite Sprite(string name, float pixelsPerUnit)
        {
            var tex = Texture(name);
            return Sprite(name, 0, 0, tex.width, tex.height, pixelsPerUnit, new Vector2(0.5f, 0.5f));
        }

        // XNA stores rows top to bottom, Unity textures bottom to top.
        static byte[] FlipRows(XnbImage img)
        {
            int stride = img.Width * 4;
            var flipped = new byte[img.Rgba.Length];
            for (int y = 0; y < img.Height; y++)
                System.Buffer.BlockCopy(img.Rgba, y * stride, flipped, (img.Height - 1 - y) * stride, stride);
            return flipped;
        }
    }
}
