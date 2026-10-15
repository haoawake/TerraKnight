using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Hollow Knight paints its terrain in front of the gameplay plane (z &lt; 0.004), so anything
    /// inside a dug hole would be hidden behind it. Every cut cell gets a SpriteMask, and the scene's
    /// foreground terrain art is set to draw only outside the masks, which opens the art exactly
    /// where the grid was dug.
    /// </summary>
    internal sealed class HoleCutter
    {
        readonly Transform root;
        readonly List<SpriteRenderer> art = new List<SpriteRenderer>();
        readonly Dictionary<int, GameObject> masks = new Dictionary<int, GameObject>();
        static Sprite maskSprite;

        readonly List<SpriteRenderer> cover = new List<SpriteRenderer>();

        public HoleCutter(Transform root, IEnumerable<SpriteRenderer> sceneArt)
        {
            this.root = root;
            foreach (var sr in sceneArt)
            {
                if (sr == null) continue;
                float z = sr.transform.position.z;
                // Only scenery in front of the Terraria wall layer matters to either list.
                if (z >= CellVisuals.WallZ || z <= -30f) continue;
                if (sr.GetComponentInParent<TerraOwned>() != null || sr.GetComponentInParent<HealthManager>() != null) continue;
                if (sr.gameObject.layer == 0 && sr.sortingLayerID == 0 && z >= -8f) art.Add(sr);
                if (z < 0f) cover.Add(sr);
            }
        }

        public int ArtCount => art.Count;

        /// <summary>More scenery for holes to cut (the art of hand-placed terrain, wherever it sits).</summary>
        public void AddArt(IEnumerable<SpriteRenderer> renderers)
        {
            foreach (var sr in renderers)
            {
                if (sr == null || art.Contains(sr)) continue;
                art.Add(sr);
                if (interactionOn && !exemptSet.Contains(sr)) sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
            }
        }
        public int MaskCount => masks.Count;
        public bool HasMask(int index) => masks.ContainsKey(index);

        /// <summary>
        /// Whether any of the scene's foreground scenery is drawn over a point of the gameplay plane.
        /// The camera is a perspective one, so the point is carried along the line of sight to each
        /// sprite's depth first. Sprite bounds include their transparent margins, so this errs
        /// towards covered; large faint overlays (fog, vignettes) don't count, and art opened up by
        /// a dug hole at the point doesn't either.
        /// </summary>
        public bool Covers(Vector3 point, Vector3 camera, bool holeAtPoint)
        {
            float depth = point.z - camera.z;
            if (Mathf.Abs(depth) < 1e-3f) return false;
            foreach (var sr in cover)
            {
                if (sr == null || !sr.enabled || sr.forceRenderingOff || sr.color.a < 0.9f || !sr.gameObject.activeInHierarchy || IsGlow(sr)) continue;
                if (holeAtPoint && sr.maskInteraction == SpriteMaskInteraction.VisibleOutsideMask) continue;
                var b = sr.bounds;
                if (b.size.x > 40f || b.size.y > 40f) continue;
                float t = (b.center.z - camera.z) / depth;
                var p = new Vector3(camera.x + (point.x - camera.x) * t, camera.y + (point.y - camera.y) * t, b.center.z);
                if (p.x < b.min.x || p.x > b.max.x || p.y < b.min.y || p.y > b.max.y) continue;
                if (OverSpriteShape(sr, p)) return true;
            }
            return false;
        }

        static readonly string[] glowWords = { "light", "glow", "haze", "fog", "mist", "beam", "shine", "blur", "shadow", "vignette", "dust", "particle", "ray" };

        /// <summary>Soft see-through dressing (light, haze, glows): drawn over the player but not hiding him.</summary>
        static bool IsGlow(SpriteRenderer sr)
        {
            string n = sr.sprite != null ? sr.sprite.name.ToLowerInvariant() : "";
            string o = sr.gameObject.name.ToLowerInvariant();
            foreach (var w in glowWords)
                if (n.Contains(w) || o.Contains(w)) return true;
            var shader = sr.sharedMaterial != null ? sr.sharedMaterial.shader.name : "";
            return shader.Contains("Additive") || shader.Contains("Particle");
        }

        static readonly Dictionary<Sprite, (Vector2[] verts, ushort[] tris)> shapes = new Dictionary<Sprite, (Vector2[], ushort[])>();

        /// <summary>
        /// Whether a world point (at the sprite's depth) falls on the sprite's mesh, which hugs its
        /// visible pixels far closer than its bounds do. Sliced and tiled sprites count by bounds.
        /// </summary>
        static bool OverSpriteShape(SpriteRenderer sr, Vector3 world)
        {
            var sprite = sr.sprite;
            if (sprite == null) return false;
            if (sr.drawMode != SpriteDrawMode.Simple) return true;
            if (!shapes.TryGetValue(sprite, out var shape))
            {
                shape = (sprite.vertices, sprite.triangles);
                shapes[sprite] = shape;
            }
            Vector2 p = sr.transform.InverseTransformPoint(world);
            if (sr.flipX) p.x = -p.x;
            if (sr.flipY) p.y = -p.y;
            var v = shape.verts;
            var tris = shape.tris;
            for (int i = 0; i + 2 < tris.Length; i += 3)
                if (InTriangle(p, v[tris[i]], v[tris[i + 1]], v[tris[i + 2]])) return true;
            return false;
        }

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        /// <summary>
        /// Every sprite renderer of a scene (inactive ones too). The constructor keeps the scenery
        /// in front of the Terraria wall layer: what holes cut (layer 0, the default sorting layer,
        /// no nearer than z -8) and what can hide the player (anything in front of the gameplay plane).
        /// </summary>
        public static IEnumerable<SpriteRenderer> SceneSprites(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
                foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
                    yield return sr;
        }

        readonly Dictionary<string, List<GameObject>> openings = new Dictionary<string, List<GameObject>>();

        /// <summary>
        /// Cuts the scene's art open over cells that aren't dug (a shaft into the scene below),
        /// under a key so it can be closed again. Null or empty cells closes it.
        /// </summary>
        public void SetOpening(string key, IEnumerable<Vector2Int> cells)
        {
            if (openings.TryGetValue(key, out var old))
            {
                foreach (var go in old) if (go != null) Object.Destroy(go);
                openings.Remove(key);
            }
            if (cells == null) return;
            var list = new List<GameObject>();
            foreach (var c in cells)
            {
                var go = new GameObject("Terra Opening");
                go.transform.SetParent(root, false);
                var center = CellGrid.CellCenter(c.x, c.y);
                go.transform.localPosition = new Vector3(center.x, center.y, 0f);
                go.AddComponent<SpriteMask>().sprite = MaskSprite();
                list.Add(go);
            }
            if (list.Count == 0) return;
            if (masks.Count == 0) ApplyMaskInteraction();
            openings[key] = list;
        }

        public IEnumerable<string> OpeningKeys => openings.Keys;

        readonly Dictionary<string, List<SpriteRenderer>> exempt = new Dictionary<string, List<SpriteRenderer>>();
        readonly HashSet<SpriteRenderer> exemptSet = new HashSet<SpriteRenderer>();
        bool interactionOn;

        /// <summary>
        /// Art that masks never cut, under a key (the scene's end of a shaft that another scene's
        /// opening looks into). Null releases it.
        /// </summary>
        public void SetExempt(string key, IEnumerable<SpriteRenderer> renderers)
        {
            if (exempt.TryGetValue(key, out var old))
            {
                foreach (var sr in old)
                {
                    exemptSet.Remove(sr);
                    if (sr != null && interactionOn && art.Contains(sr)) sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
                }
                exempt.Remove(key);
            }
            if (renderers == null) return;
            var list = new List<SpriteRenderer>(renderers);
            foreach (var sr in list)
            {
                exemptSet.Add(sr);
                sr.maskInteraction = SpriteMaskInteraction.None;
            }
            exempt[key] = list;
        }

        public IEnumerable<string> ExemptKeys => exempt.Keys;

        public void SetCut(int index, int x, int y, bool cut)
        {
            if (cut == masks.ContainsKey(index)) return;
            if (cut)
            {
                if (masks.Count == 0) ApplyMaskInteraction();
                var go = new GameObject("Terra Hole");
                go.transform.SetParent(root, false);
                var c = CellGrid.CellCenter(x, y);
                go.transform.localPosition = new Vector3(c.x, c.y, 0f);
                go.AddComponent<SpriteMask>().sprite = MaskSprite();
                masks[index] = go;
            }
            else
            {
                Object.Destroy(masks[index]);
                masks.Remove(index);
            }
        }

        void ApplyMaskInteraction()
        {
            interactionOn = true;
            foreach (var sr in art)
                if (sr != null && !exemptSet.Contains(sr)) sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
        }

        static Sprite MaskSprite()
        {
            if (maskSprite != null) return maskSprite;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[16];
            for (int k = 0; k < px.Length; k++) px[k] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            // 4px over 0.5 units: exactly one cell.
            maskSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 8f);
            return maskSprite;
        }
    }

    /// <summary>Marks objects the mod created inside a scene, so scans of the scene's own art skip them.</summary>
    internal sealed class TerraOwned : MonoBehaviour
    {
    }
}
