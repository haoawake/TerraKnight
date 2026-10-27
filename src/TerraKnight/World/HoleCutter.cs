using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Hollow Knight paints its terrain in front of the gameplay plane (z &lt; 0.004), so anything
    /// inside a dug hole would be hidden behind it. Every cut cell's cut is drawn into the mask of its
    /// block of cells (one SpriteMask over 16×16 cells), and the scene's foreground terrain art is set
    /// to draw only outside the masks, which opens the art exactly where the grid was dug.
    /// </summary>
    internal sealed class HoleCutter
    {
        readonly Transform root;
        readonly List<SpriteRenderer> art = new List<SpriteRenderer>();
        static Sprite maskSprite;

        /// <summary>
        /// Cells a block's mask covers along each side. Every SpriteMask costs each frame it is in view, whatever its size: one
        /// per cell let a few dozen dynamite blasts (1700 masks) take a frame from 8 ms to 43 ms (the user's 10-26 "用雷管炸地图越炸越卡").
        /// </summary>
        const int Block = 16;
        const int BlockPx = Block * HoleShapes.Res;

        sealed class MaskBlock
        {
            public GameObject Go;
            public Texture2D Tex;
            public byte[] Px;
            public int Cells;
        }

        readonly Dictionary<(int, int), MaskBlock> blocks = new Dictionary<(int, int), MaskBlock>();
        readonly HashSet<MaskBlock> dirtyBlocks = new HashSet<MaskBlock>();
        /// <summary>Each cut cell's cut (HoleShapes' alpha, Res×Res), by cell index.</summary>
        readonly Dictionary<int, byte[]> cellCuts = new Dictionary<int, byte[]>();

        readonly List<SpriteRenderer> cover = new List<SpriteRenderer>();

        public HoleCutter(Transform root, IEnumerable<SpriteRenderer> sceneArt)
        {
            this.root = root;
            List<Bounds> hazards = null;
            foreach (var sr in sceneArt)
            {
                if (sr == null) continue;
                float z = sr.transform.position.z;
                bool spike = IsSpikeArt(sr);
                if (!spike && IsThornArt(sr))
                {
                    hazards ??= HazardBounds(sr.gameObject.scene);
                    spike = OnHazard(sr, hazards);
                }
                // Only scenery in front of the Terraria wall layer matters to either list; a spike's or thorn's art behind
                // the gameplay plane too, as a mined spike's cell has no wall in front of it.
                if (z >= (spike ? HazardArtBehind : CellVisuals.WallZ) || z <= -30f) continue;
                if (sr.GetComponentInParent<TerraOwned>() != null || sr.GetComponentInParent<HealthManager>() != null) continue;
                if (spike ? z > HazardArtFront : sr.gameObject.layer == 0 && sr.sortingLayerID == 0 && z >= -8f) art.Add(sr);
                if (spike && z > HazardArtFront) spikeArt.Add(sr);
                if (spike) DevHazardArt++;
                if (z < 0f) cover.Add(sr);
                if (z >= -8f && IsDecor(sr)) decor.Add(sr);
            }
        }

        public int DevHazardArt;

        /// <summary>The spikes' and thorns' own sprites among the art (what is left of them over a cell, DugHazards.TidyArt).</summary>
        readonly List<SpriteRenderer> spikeArt = new List<SpriteRenderer>();
        public bool HasSpikeArt => spikeArt.Count > 0;

        /// <summary>Whether a spike's or thorn's picture (its mesh, not its box) is drawn over the cell: one still showing.</summary>
        public bool SpikeArtOver(int x, int y)
        {
            if (spikeArt.Count == 0) return false;
            var c = (Vector2)root.position + CellGrid.CellCenter(x, y);
            const float half = CellGrid.CellSize * 0.5f;
            foreach (var sr in spikeArt)
            {
                if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy) continue;
                var b = sr.bounds;
                if (b.max.x < c.x - half || b.min.x > c.x + half || b.max.y < c.y - half || b.min.y > c.y + half) continue;
                if (CrossesCell(sr, c, half)) return true;
            }
            return false;
        }

        /// <summary>The layer Hollow Knight keeps its hazards on (their colliders and the spikes' own sprites: 17).</summary>
        const int HazardLayer = 17;
        /// <summary>
        /// How far behind and in front of the gameplay plane a spike's or thorn's sprite still belongs to it: Crossroads' rows
        /// reach z 1.15; a thorn bush (Greenpath's, the Queen's Gardens') is dozens of sprites from z -13.9 to 6.0, and the
        /// spiky backdrops behind it start at z 10.
        /// </summary>
        const float HazardArtBehind = 6.5f, HazardArtFront = -14f;

        /// <summary>The game's hazard type for spikes and thorns (DamageHero.hazardType 2).</summary>
        const int SpikeHazard = 2;

        /// <summary>A sprite named for spikes or thorns, itself or up to three parents up (fungd_spikes_29/fungd_spikes_12_0001_a, thorns_wall).</summary>
        static bool IsThornArt(SpriteRenderer sr)
        {
            var t = sr.transform;
            for (int k = 0; k < 4 && t != null; k++, t = t.parent)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("lift")) return false;
                if (n.Contains("spike") || n.Contains("thorn")) return true;
            }
            return false;
        }

        /// <summary>The world boxes of a scene's spike and thorn hazards (their DamageHero's colliders).</summary>
        static List<Bounds> HazardBounds(UnityEngine.SceneManagement.Scene scene)
        {
            var list = new List<Bounds>();
            foreach (var dh in Object.FindObjectsByType<DamageHero>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (dh == null || dh.hazardType != SpikeHazard || dh.gameObject.scene != scene) continue;
                foreach (var col in dh.GetComponents<Collider2D>())
                    if (col != null && col.enabled) list.Add(col.bounds);
            }
            return list;
        }

        /// <summary>Its picture's box reaches a hazard's (within half a unit): the bush's art, not a spiky backdrop elsewhere in the room.</summary>
        static bool OnHazard(SpriteRenderer sr, List<Bounds> hazards)
        {
            var b = sr.bounds;
            const float near = 0.5f;
            foreach (var h in hazards)
                if (b.max.x >= h.min.x - near && b.min.x <= h.max.x + near && b.max.y >= h.min.y - near && b.min.y <= h.max.y + near)
                    return true;
            return false;
        }

        /// <summary>
        /// The art of a row of spikes (Cave Spikes, Deep Spikes, Enemy_spike, the White Palace's): sprites on the hazard layer,
        /// where the terrain art's list (layer 0) never looked, so a mined spike's cell left its picture standing. Spikes riding
        /// a lift move with it and are left whole.
        /// </summary>
        static bool IsSpikeArt(SpriteRenderer sr)
        {
            if (sr.gameObject.layer != HazardLayer) return false;
            string n = sr.gameObject.name.ToLowerInvariant();
            return n.Contains("spike") && !n.Contains("lift");
        }

        // ------------------------------------------------------------------ what grows on the ground

        /// <summary>The scene's plants in front of the gameplay plane: vines, grass, bushes, moss, leaves, ferns, flowers.</summary>
        readonly List<SpriteRenderer> decor = new List<SpriteRenderer>();
        static readonly string[] decorNames = { "vine", "grass", "bush", "moss", "leaf", "leaves", "fern", "flower", "plant", "weed" };
        public int DevDecorHidden;

        static bool IsDecor(SpriteRenderer sr)
        {
            string n = sr.gameObject.name.ToLowerInvariant();
            bool named = false;
            foreach (var k in decorNames) if (n.Contains(k)) { named = true; break; }
            if (!named) return false;
            // A sheet of the scene's art, not one plant (a whole floor's grass strip stays, cut where dug like the rest).
            var b = sr.bounds;
            return b.size.x <= 8f && b.size.y <= 8f;
        }

        /// <summary>
        /// A cell of the room's own ground dug out (CellVisuals calls it for dug ground only, not for the overhang beside it
        /// or a mined spike): the plants growing on it, hanging from it or in front of it go with it, whole, as they would
        /// fall off with the ground they grew on; the cut alone left the rest of a hanging vine in the air (Greenpath's roof
        /// vines). A plant counts where its picture itself crosses the cell, not its box with the sprite's transparent margin
        /// (that took every vine of a cluster at once). On a load, each dug cell takes them away again.
        /// </summary>
        public void DropDecor(int x, int y)
        {
            if (decor.Count == 0) return;
            var c = (Vector2)root.position + CellGrid.CellCenter(x, y);
            const float half = CellGrid.CellSize * 0.5f + 0.05f;
            for (int i = decor.Count - 1; i >= 0; i--)
            {
                var sr = decor[i];
                if (sr == null) { decor.RemoveAt(i); continue; }
                var b = sr.bounds;
                if (b.max.x < c.x - half || b.min.x > c.x + half || b.max.y < c.y - half || b.min.y > c.y + half) continue;
                if (!CrossesCell(sr, c, half)) continue;
                sr.enabled = false;
                decor.RemoveAt(i);
                DevDecorHidden++;
            }
        }

        /// <summary>Whether the sprite's picture (its mesh, at its own depth) covers any of a 4×4 spread of points over the cell.</summary>
        static bool CrossesCell(SpriteRenderer sr, Vector2 center, float half)
        {
            float z = sr.transform.position.z;
            for (int j = 0; j < 4; j++)
                for (int k = 0; k < 4; k++)
                {
                    var p = new Vector3(center.x - half + (k + 0.5f) * half * 0.5f, center.y - half + (j + 0.5f) * half * 0.5f, z);
                    if (OverSpriteShape(sr, p)) return true;
                }
            return false;
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
        /// <summary>Cells cut by a mask; the block masks drawing them.</summary>
        public int MaskCount => cellCuts.Count;
        public int MaskBlocks => blocks.Count;
        /// <summary>The cell's art is cut away (dug, or its overhang): Hollow Knight's smooth holes also mask a corner of standing terrain, which doesn't count.</summary>
        public bool HasMask(int index) => cutCells.Contains(index);
        readonly HashSet<int> cutCells = new HashSet<int>();

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
                if (sr == null || !sr.enabled || sr.forceRenderingOff || sr.color.a < 0.9f || !sr.gameObject.activeInHierarchy || Glow(sr)) continue;
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

        /// <summary>
        /// The foreground scenery that can be drawn over a part of the gameplay plane from where the camera goes near it:
        /// each sprite's own place widened by how far its depth carries it across the screen (the nearer the camera, the
        /// more it slides) over half a view's width. Fog, haze and the like, and sprites as big as a room, are left out.
        /// </summary>
        public List<SpriteRenderer> CoverOver(Rect area, float cameraZ, float reach = 16f)
        {
            var found = new List<SpriteRenderer>();
            foreach (var sr in cover)
            {
                if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy || Glow(sr)) continue;
                var b = sr.bounds;
                if (b.size.x > 40f || b.size.y > 40f) continue;
                float toPlane = -cameraZ, toSprite = b.center.z - cameraZ;
                if (toSprite <= 0.5f) continue;
                float slide = Mathf.Abs(toPlane / toSprite - 1f) * reach;
                var r = Rect.MinMaxRect(b.min.x - slide, b.min.y - slide, b.max.x + slide, b.max.y + slide);
                if (r.Overlaps(area)) found.Add(sr);
            }
            return found;
        }

        /// <summary>Each cover sprite's glow test, kept (it reads names, new strings each time), again when its sprite changes.</summary>
        readonly Dictionary<SpriteRenderer, (Sprite sprite, bool glow)> glowOf = new Dictionary<SpriteRenderer, (Sprite, bool)>();

        bool Glow(SpriteRenderer sr)
        {
            var sprite = sr.sprite;
            if (glowOf.TryGetValue(sr, out var g) && g.sprite == sprite) return g.glow;
            bool glow = IsGlow(sr);
            glowOf[sr] = (sprite, glow);
            return glow;
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
            if (!interactionOn) ApplyMaskInteraction();
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

        public void SetCut(int index, int x, int y, bool cut) => SetShape(index, x, y, cut ? HoleShapes.FullAlpha : null, cut);

        /// <summary>
        /// The cell's mask: the whole cell (HoleShapes.FullAlpha), a shape of it (World.HoleShapes: a smooth hole's outline
        /// through it), or none (null); whether the cell counts as cut (<see cref="HasMask"/>) apart from what its mask
        /// covers. Drawn into its block's mask at the next <see cref="Flush"/>.
        /// </summary>
        public void SetShape(int index, int x, int y, byte[] alpha, bool cut)
        {
            if (cut) cutCells.Add(index); else cutCells.Remove(index);
            cellCuts.TryGetValue(index, out var had);
            if (had == alpha) return;
            if (alpha == null) cellCuts.Remove(index);
            else
            {
                cellCuts[index] = alpha;
                if (!interactionOn) ApplyMaskInteraction();
            }
            var key = (x / Block, y / Block);
            if (!blocks.TryGetValue(key, out var b))
            {
                if (alpha == null) return;
                blocks[key] = b = new MaskBlock { Px = new byte[BlockPx * BlockPx] };
            }
            b.Cells += (alpha != null ? 1 : 0) - (had != null ? 1 : 0);
            int res = HoleShapes.Res, x0 = x % Block * res, y0 = y % Block * res;
            for (int r = 0; r < res; r++)
            {
                int to = (y0 + r) * BlockPx + x0;
                if (alpha != null) System.Buffer.BlockCopy(alpha, r * res, b.Px, to, res);
                else System.Array.Clear(b.Px, to, res);
            }
            dirtyBlocks.Add(b);
        }

        /// <summary>The region gone: its blocks' mask pictures with it.</summary>
        public void DestroyMasks()
        {
            foreach (var b in blocks.Values)
                if (b.Tex != null) Object.Destroy(b.Tex);
            blocks.Clear();
            dirtyBlocks.Clear();
        }

        /// <summary>The blocks' masks whose cells changed, drawn again (after a pass of the cells' pictures).</summary>
        public void Flush()
        {
            if (dirtyBlocks.Count == 0) return;
            foreach (var kv in new List<KeyValuePair<(int, int), MaskBlock>>(blocks))
            {
                var b = kv.Value;
                if (!dirtyBlocks.Contains(b)) continue;
                if (b.Cells <= 0)
                {
                    if (b.Go != null) Object.Destroy(b.Go);
                    if (b.Tex != null) Object.Destroy(b.Tex);
                    blocks.Remove(kv.Key);
                    continue;
                }
                if (b.Go == null)
                {
                    b.Tex = new Texture2D(BlockPx, BlockPx, TextureFormat.Alpha8, false)
                        { name = "TerraKnight/hole masks", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                    b.Go = new GameObject("Terra Holes");
                    b.Go.transform.SetParent(root, false);
                    b.Go.transform.localPosition = new Vector3(kv.Key.Item1 * Block * CellGrid.CellSize, kv.Key.Item2 * Block * CellGrid.CellSize, 0f);
                    var mask = b.Go.AddComponent<SpriteMask>();
                    mask.sprite = Sprite.Create(b.Tex, new Rect(0, 0, BlockPx, BlockPx), Vector2.zero, HoleShapes.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                    mask.alphaCutoff = 0.5f;
                }
                b.Tex.LoadRawTextureData(b.Px);
                b.Tex.Apply(false, false);
            }
            dirtyBlocks.Clear();
        }


        /// <summary>The highest sorting order of the cuttable art over a point (for what is drawn on its edge), 0 with none.</summary>
        public int ArtOrderAt(Vector2 world)
        {
            int best = int.MinValue;
            foreach (var sr in art)
            {
                if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy) continue;
                var b = sr.bounds;
                if (world.x < b.min.x || world.x > b.max.x || world.y < b.min.y || world.y > b.max.y) continue;
                if (sr.sortingOrder > best) best = sr.sortingOrder;
            }
            return best == int.MinValue ? 0 : best;
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
