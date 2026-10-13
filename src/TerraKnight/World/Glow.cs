using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Light in Hallownest's painted dark: a soft radial glow drawn just behind the player, over
    /// the background and the blocks, added to what is behind it where the game has an additive
    /// shader (it falls back to a plain translucent wash). Torches and the mining helmet use it.
    /// </summary>
    internal static class Glow
    {
        /// <summary>Behind the hero (0.0039), in front of the Terraria blocks (0.006).</summary>
        public const float Z = 0.0048f;

        static Sprite sprite;
        static Material material;
        public static bool Additive { get; private set; }

        public static SpriteRenderer Create(Transform parent, Color color, float radius)
        {
            Init();
            var go = new GameObject("Terra Glow");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            sr.color = Additive ? new Color(color.r * color.a, color.g * color.a, color.b * color.a, 1f) : new Color(color.r, color.g, color.b, color.a * 0.45f);
            go.transform.localScale = Vector3.one * (radius * 2f / (sprite.rect.width / sprite.pixelsPerUnit));
            return sr;
        }

        static void Init()
        {
            if (sprite != null) return;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Terra Glow" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // Terraria's light falls off roughly linearly per tile; soften the rim.
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    byte b = (byte)(a * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, b);
                }
            tex.SetPixels32(px);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            foreach (var name in new[] { "Legacy Shaders/Particles/Additive", "Particles/Additive", "Mobile/Particles/Additive", "Legacy Shaders/Particles/Additive (Soft)" })
            {
                var sh = Shader.Find(name);
                if (sh == null) continue;
                material = new Material(sh) { name = "Terra Glow (" + name + ")" };
                if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
                Additive = true;
                break;
            }
            if (material == null) material = new Material(Shader.Find("Sprites/Default")) { name = "Terra Glow (alpha)" };
            Plugin.Log.LogInfo($"[光] 光晕材质 {material.name}");
        }

        static Sprite lightSprite;

        /// <summary>Tiles out to where a light is drawn: there it is 0.91^32, 5% of what it is at its source.</summary>
        public const float LightReachTiles = 32f;

        /// <summary>
        /// A Terraria light (a torch's, the mining helmet's): it falls off by 0.91 a tile through
        /// the air (LightMap.LightDecayThroughAir), so it is soft beside its source and reaches far,
        /// drawn out to <see cref="LightReachTiles"/> tiles. The colour's alpha is its strength.
        /// </summary>
        public static SpriteRenderer CreateLight(Transform parent, Color color)
        {
            Init();
            if (lightSprite == null)
            {
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Terra Light" };
                var px = new Color32[size * size];
                float edge = Mathf.Pow(0.91f, LightReachTiles);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = d >= 1f ? 0f : (Mathf.Pow(0.91f, d * LightReachTiles) - edge) / (1f - edge);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                lightSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            var sr = Create(parent, color, LightReachTiles * CellGrid.CellSize);
            sr.gameObject.name = "Terra Light";
            sr.sprite = lightSprite;
            sr.transform.localScale = Vector3.one * (LightReachTiles * CellGrid.CellSize * 2f / (lightSprite.rect.width / lightSprite.pixelsPerUnit));
            return sr;
        }

        /// <summary>Torch light wavers a little (Terraria's torches flicker).</summary>
        public sealed class Flicker : MonoBehaviour
        {
            Vector3 baseScale;
            float seed;

            void Start()
            {
                baseScale = transform.localScale;
                seed = Random.value * 10f;
            }

            void Update()
            {
                float t = Time.time * 7f + seed;
                float k = 1f + 0.04f * Mathf.Sin(t) + 0.03f * Mathf.Sin(t * 2.3f + 1f);
                transform.localScale = baseScale * k;
            }
        }
    }
}
