using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// MasterTrophy presentation: a fixed three-tile pedestal and a separate floating sculpture.
    /// Terraria TileDrawing.DrawMasterTrophies uses a five-second, four-pixel bob and six
    /// offset copies with a two-second glow pulse. Keep the original artwork's aspect ratio.
    /// All visual children belong to the station, so mining/unloading removes the whole display.
    /// </summary>
    internal sealed class RelicDisplay : MonoBehaviour
    {
        const float Px = TerraParticles.Px;
        static readonly Dictionary<string, Sprite> sculptures = new Dictionary<string, Sprite>();
        static Sprite haloSprite, starSprite;
        SpriteRenderer pedestal, sculpture, halo;
        readonly SpriteRenderer[] echoes = new SpriteRenderer[6];
        readonly SpriteRenderer[] stars = new SpriteRenderer[4];
        Transform display;
        bool visible;

        public float FloatHeight => sculpture != null ? sculpture.transform.localPosition.y : 0f;
        public int GlowCopies => echoes.Length;

        public void Init(string texture, SpriteRenderer baseRenderer)
        {
            pedestal = baseRenderer;
            bool native = Relics.NativeArt(texture);
            var pic = native ? Relics.SculptureTexture(texture) : Relics.Texture(texture);
            if (!sculptures.TryGetValue(texture, out var sprite) || sprite == null)
            {
                // Native artwork is already a standalone frame; legacy artwork includes a pedestal.
                var rect = native ? new Rect(0, 0, pic.width, pic.height) : new Rect(0, 16, pic.width, pic.height - 16);
                sprite = Sprite.Create(pic, rect,
                    new Vector2(0.5f, 0f), (native ? 1f : 2f) / Px, 0, SpriteMeshType.FullRect);
                sculptures[texture] = sprite;
            }
            var go = new GameObject("Floating relic display");
            display = go.transform;
            display.SetParent(transform, false);
            EnsureEffects();
            halo = Make("Golden halo", haloSprite, -2, true);
            sculpture = Make("Relic sculpture", sprite, 1, false);
            sculpture.transform.localScale = Vector3.one * (native ? 1f : 1.1f);
            for (int i = 0; i < echoes.Length; i++)
            {
                echoes[i] = Make("Golden echo " + i, sprite, -1, true);
                echoes[i].transform.localScale = sculpture.transform.localScale;
            }
            for (int i = 0; i < stars.Length; i++) stars[i] = Make("Gold sparkle " + i, starSprite, 2, true);
            SetVisible(true);
            Animate();
        }

        SpriteRenderer Make(string name, Sprite sprite, int order, bool additive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(display, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = pedestal.sortingLayerID;
            sr.sortingOrder = pedestal.sortingOrder + order;
            if (additive && TerraParticles.AdditiveMaterial() != null)
                sr.sharedMaterial = TerraParticles.AdditiveMaterial();
            return sr;
        }

        /// <summary>Placed facing right (tile 617's PlaceRight alternate, frameY 72 on): the sculpture and its echoes drawn turned (SpriteEffects.FlipHorizontally).</summary>
        public void Face(bool right)
        {
            if (sculpture == null || sculpture.flipX == right) return;
            sculpture.flipX = right;
            foreach (var e in echoes) if (e != null) e.flipX = right;
        }

        public bool Facing => sculpture != null && sculpture.flipX;

        public void SetVisible(bool show)
        {
            visible = show;
            if (display != null && display.gameObject.activeSelf != show) display.gameObject.SetActive(show);
        }

        void Update()
        {
            if (!visible || pedestal == null || !pedestal.enabled) return;
            Animate();
            LightMap.Add(sculpture.bounds.center, new Color(0.85f, 0.57f, 0.16f));
        }

        void Animate()
        {
            float time = Time.time;
            float bob = Mathf.Sin(time * Mathf.PI * 2f / 5f);
            float pulse = Mathf.Sin(time * Mathf.PI) * 0.3f + 0.7f;
            // The base is 12 px tall; the statue floats 0..8 px above it, within 3x4 tiles.
            Vector3 position = new Vector3(0f, (16f + bob * 4f) * Px, 0f);
            sculpture.transform.localPosition = position;
            halo.transform.localPosition = position + new Vector3(0f, 22f * Px, 0f);
            halo.transform.localScale = Vector3.one * (1.05f + pulse * 0.12f);
            halo.color = new Color(1f, 0.65f, 0.17f, 0.20f * pulse);
            float radius = (6f + bob * 2f) * Px;
            for (int i = 0; i < echoes.Length; i++)
            {
                float angle = Mathf.PI * 2f * i / echoes.Length;
                echoes[i].transform.localPosition = position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                echoes[i].color = new Color(1f, 0.82f, 0.38f, 0.14f * pulse);
            }
            for (int i = 0; i < stars.Length; i++)
            {
                float phase = Mathf.Repeat(time / 2.4f + i * 0.25f, 1f);
                float shine = Mathf.Pow(Mathf.Sin(phase * Mathf.PI), 6f);
                float angle = i * 2.4f + Mathf.Floor(time / 2.4f + i * 0.25f) * 1.7f;
                stars[i].transform.localPosition = position + new Vector3(Mathf.Cos(angle) * 21f, 22f + Mathf.Sin(angle) * 21f + phase * 4f, 0f) * Px;
                stars[i].transform.localScale = Vector3.one * (0.55f + shine * 0.65f);
                stars[i].color = new Color(1f, 0.86f, 0.40f, shine * 0.9f);
            }
        }

        static void EnsureEffects()
        {
            if (haloSprite != null) return;
            const int size = 64;
            var halo = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Relic soft gold halo", wrapMode = TextureWrapMode.Clamp };
            var star = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Relic gold glint", wrapMode = TextureWrapMode.Clamp };
            var glowPixels = new Color[size * size];
            var starPixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - size / 2f) / (size / 2f), dy = (y + 0.5f - size / 2f) / (size / 2f);
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    glowPixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - radius), 2.5f));
                    float diamond = Mathf.Abs(dx) + Mathf.Abs(dy);
                    float ray = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(dx) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(dy)),
                        Mathf.Clamp01(1f - Mathf.Abs(dy) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(dx)));
                    starPixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Max(ray, Mathf.Clamp01(1f - diamond * 4f)));
                }
            halo.SetPixels(glowPixels); halo.Apply();
            star.SetPixels(starPixels); star.Apply();
            haloSprite = Sprite.Create(halo, new Rect(0, 0, size, size), Vector2.one * 0.5f, 1f / Px);
            starSprite = Sprite.Create(star, new Rect(0, 0, size, size), Vector2.one * 0.5f, 8f / Px);
        }
    }
}
