using System.Collections.Generic;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.World
{
    /// <summary>
    /// How a region's poured liquid looks and feels. It is drawn as Terraria draws liquid (LiquidRenderer): each cell a
    /// 16-px frame of the water sheet (Images/Misc/water_0 for water, the jungle's green water_3 for acid) picked by which
    /// sides are open — the surface frame on top, the edge frames at the sides, the inside elsewhere — a cell partly full
    /// cut to its level with the surface frame at the top, the frames stepping through the sheet's 16 animation frames; more
    /// see-through than Terraria's (Hallownest's water is clear), a mesh per 16×16 cells. Every stretch of surface is given
    /// a pool's working parts: a copy of a Surface Water Region the Knight swims in (its pictures off), and for acid an Acid
    /// Box that hurts as Hallownest's acid does, followed by Player.AcidLiquid like the scene's own pools (Isma's Tear and
    /// the flippers swim in it).
    /// </summary>
    internal sealed class LiquidView
    {
        const float Z = 0.002f;
        /// <summary>A surface this deep (cells) or more is swum in; shallower liquid is walked through.</summary>
        const int SwimDepth = 3;
        /// <summary>The least liquid that makes a cell a surface (Liquid.LiquidCheck's 24).</summary>
        const int SurfaceMin = 24;
        const float LayoutEvery = 0.25f;
        /// <summary>Terraria's water at 0.6 and lava at 0.95 (LiquidRenderer.DEFAULT_OPACITY), clearer here.</summary>
        static readonly Color WaterTint = new Color(1f, 1f, 1f, 0.4f), AcidTint = new Color(0.95f, 1f, 0.8f, 0.5f);
        const string WaterSheet = "Misc/water_0", AcidSheet = "Misc/water_3";

        readonly TerrainRegion region;
        readonly CellGrid g;
        readonly Dictionary<int, Chunk> chunks = new Dictionary<int, Chunk>();
        readonly HashSet<int> wet = new HashSet<int>();
        readonly List<int> dirty = new List<int>();
        bool layoutDue;
        float nextLayout;

        sealed class Chunk
        {
            public GameObject Go;
            public Mesh Mesh;
            /// <summary>Each vertex's texture coordinates on animation frame 0, and whether it steps with the slow frame.</summary>
            public readonly List<Vector2> BaseUv = new List<Vector2>();
            public readonly List<bool> Slow = new List<bool>();
            public readonly List<Vector2> Uv = new List<Vector2>();
        }

        public LiquidView(TerrainRegion region)
        {
            this.region = region;
            g = region.Grid;
        }

        // ------------------------------------------------------------------ animation (LiquidRenderer.Update)

        static float frameState, slowState;
        static int frame, slowFrame, lastFrameTick = -1;

        /// <summary>Terraria's frames: 6 a second (no wind), the middle column's (a waterfall's) half a frame a second.</summary>
        static void Animate()
        {
            if (lastFrameTick == Time.frameCount) return;
            lastFrameTick = Time.frameCount;
            frameState = (frameState + 6f * Time.deltaTime) % 16f;
            slowState = (slowState + 0.5f * Time.deltaTime) % 16f;
            frame = (int)frameState;
            slowFrame = (int)slowState;
        }

        int shownFrame = -1, shownSlow = -1;

        public void Tick()
        {
            LiquidTemplates.Watch();
            Animate();
            dirty.Clear();
            region.Liquid.TakeDirty(dirty);
            if (dirty.Count > 0)
            {
                foreach (int c in dirty) Rebuild(c);
                layoutDue = true;
            }
            if (frame != shownFrame || slowFrame != shownSlow)
            {
                shownFrame = frame;
                shownSlow = slowFrame;
                foreach (int c in wet) if (chunks.TryGetValue(c, out var ch)) Step(ch);
            }
            if (layoutDue && Time.time >= nextLayout)
            {
                layoutDue = false;
                nextLayout = Time.time + LayoutEvery;
                Layout();
            }
        }

        public void Destroy()
        {
            foreach (var ch in chunks.Values) if (ch.Mesh != null) Object.Destroy(ch.Mesh);
            chunks.Clear();
            foreach (var b in bodies) b.Destroy();
            bodies.Clear();
        }

        // ------------------------------------------------------------------ the liquid drawn

        static readonly List<Vector3> verts = new List<Vector3>();
        static readonly List<Color> colors = new List<Color>();
        static readonly List<int> waterTris = new List<int>(), acidTris = new List<int>();
        static Material waterMaterial, acidMaterial;

        bool Open(int x, int y) => g.InClip(x, y) && !Cells.Blocks(g.Get(x, y));
        bool Wet(int x, int y) => g.GetLiquid(x, y) > 0 && Open(x, y);

        void Rebuild(int chunk)
        {
            int cols = region.Liquid.ChunkCols, n = LiquidSim.ChunkCells;
            int cx = chunk % cols, cy = chunk / cols;
            if (!chunks.TryGetValue(chunk, out var ch)) chunks[chunk] = ch = new Chunk();
            verts.Clear();
            colors.Clear();
            ch.BaseUv.Clear();
            ch.Slow.Clear();
            waterTris.Clear();
            acidTris.Clear();
            for (int y = cy * n; y < Mathf.Min(g.Height, (cy + 1) * n); y++)
                for (int x = cx * n; x < Mathf.Min(g.Width, (cx + 1) * n); x++)
                    if (Wet(x, y)) Cell(ch, x, y);
            if (verts.Count == 0)
            {
                wet.Remove(chunk);
                if (ch.Go != null) ch.Go.SetActive(false);
                return;
            }
            wet.Add(chunk);
            if (ch.Go == null)
            {
                ch.Mesh = new Mesh { name = "Terra Liquid" };
                ch.Mesh.MarkDynamic();
                ch.Go = new GameObject("Terra Liquid");
                ch.Go.AddComponent<TerraOwned>();
                ch.Go.transform.SetParent(region.Root, false);
                ch.Go.transform.localPosition = new Vector3(0f, 0f, Z);
                ch.Go.AddComponent<MeshFilter>().sharedMesh = ch.Mesh;
                ch.Go.AddComponent<MeshRenderer>().sharedMaterials = new[] { Material(false), Material(true) };
            }
            ch.Mesh.Clear();
            ch.Mesh.SetVertices(verts);
            ch.Mesh.SetColors(colors);
            ch.Mesh.subMeshCount = 2;
            Step(ch);
            ch.Mesh.SetTriangles(waterTris, 0);
            ch.Mesh.SetTriangles(acidTris, 1);
            ch.Mesh.RecalculateBounds();
            if (!ch.Go.activeSelf) ch.Go.SetActive(true);
        }

        /// <summary>The texture coordinates on the frame shown now: each sheet frame 80 px under the last.</summary>
        void Step(Chunk ch)
        {
            if (ch.Mesh == null) return;
            ch.Uv.Clear();
            float fast = frame * 80f / SheetHeight, slow = slowFrame * 80f / SheetHeight;
            for (int i = 0; i < ch.BaseUv.Count; i++)
                ch.Uv.Add(ch.BaseUv[i] - new Vector2(0f, ch.Slow[i] ? slow : fast));
            ch.Mesh.SetUVs(0, ch.Uv);
        }

        static float SheetHeight => Material(false).mainTexture != null ? Material(false).mainTexture.height : 1360f;

        /// <summary>
        /// One cell (LiquidRenderer.InternalPrepareDraw): its frame from which sides are open and dry — the surface (top) row,
        /// the left and right columns, both sides open one above the other — and, at the surface, only its level drawn (a
        /// quarter at least), the frame's top row at the liquid's top.
        /// </summary>
        void Cell(Chunk ch, int x, int y)
        {
            bool top = !Wet(x, y + 1) && Open(x, y + 1);
            bool left = !Wet(x - 1, y) && Open(x - 1, y);
            bool right = !Wet(x + 1, y) && Open(x + 1, y);
            int fx = 0, fy = 0;
            if (!left) fx += right ? 32 : 16;
            if (left && right)
            {
                fx = 16;
                fy = top ? 16 : 32;
            }
            else if (!top) fy += !left && !right ? 48 : 16;
            // Terraria's alternate side frame, row by row.
            if (fy == 16 && (left ^ right) && y % 2 == 0) fy += 16;
            float level = g.GetLiquid(x, y) / 255f;
            float cut = top ? Mathf.Min(0.75f, 1f - level) : 0f;
            const float c = CellGrid.CellSize;
            float x0 = x * c, y0 = y * c, y1 = y0 + (1f - cut) * c;
            int h = Mathf.CeilToInt((1f - cut) * 16f);
            bool acid = g.GetLiquidKind(x, y) == LiquidKind.Acid;
            var tex = Material(acid).mainTexture;
            float W = tex != null ? tex.width : 48f, H = tex != null ? tex.height : 1360f;
            // Unity's rows run up: Terraria's (fx, fy) top-left is (fx, H - fy) here.
            float u0 = fx / W, u1 = (fx + 16) / W, vTop = (H - fy) / H, vBottom = (H - fy - h) / H;
            int b = verts.Count;
            verts.Add(new Vector3(x0, y0));
            verts.Add(new Vector3(x0, y1));
            verts.Add(new Vector3(x0 + c, y1));
            verts.Add(new Vector3(x0 + c, y0));
            ch.BaseUv.Add(new Vector2(u0, vBottom));
            ch.BaseUv.Add(new Vector2(u0, vTop));
            ch.BaseUv.Add(new Vector2(u1, vTop));
            ch.BaseUv.Add(new Vector2(u1, vBottom));
            bool slow = fx == 16;
            var col = acid ? AcidTint : WaterTint;
            for (int k = 0; k < 4; k++)
            {
                ch.Slow.Add(slow);
                colors.Add(col);
            }
            var tris = acid ? acidTris : waterTris;
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
        }

        static Material Material(bool acid)
        {
            if (acid ? acidMaterial != null : waterMaterial != null) return acid ? acidMaterial : waterMaterial;
            var m = new Material(Shader.Find("Sprites/Default")) { name = acid ? "Terra Acid" : "Terra Water" };
            m.mainTexture = Terraria.TerrariaAssets.Texture(acid ? AcidSheet : WaterSheet);
            if (m.mainTexture != null) m.mainTexture.wrapMode = TextureWrapMode.Clamp;
            if (acid) acidMaterial = m; else waterMaterial = m;
            return m;
        }

        // ------------------------------------------------------------------ the surfaces: swimming, the acid's bite

        /// <summary>One stretch of surface: its cells (x0..x1 on row y), how far down the liquid under it goes, its top.</summary>
        struct Run
        {
            public int X0, X1, Y, Bottom;
            public float Top;
            public bool Acid;
            public long Key => ((long)X0 << 40) ^ ((long)X1 << 24) ^ ((long)Y << 12) ^ ((long)Bottom << 1) ^ (Acid ? 1L : 0L);
        }

        sealed class Body
        {
            public long Key;
            public GameObject Hazard, Region;
            public void Destroy()
            {
                if (Hazard != null) Object.Destroy(Hazard);
                if (Region != null) Object.Destroy(Region);
            }
        }

        readonly List<Body> bodies = new List<Body>();
        readonly List<Run> runs = new List<Run>();
        public static int DevBodies, DevSwimRegions;

        void Layout()
        {
            runs.Clear();
            if (wet.Count > 0) FindRuns();
            var keep = new HashSet<long>();
            foreach (var r in runs) keep.Add(r.Key);
            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                if (keep.Contains(bodies[i].Key)) { keep.Remove(bodies[i].Key); continue; }
                bodies[i].Destroy();
                bodies.RemoveAt(i);
            }
            // keep now holds the runs that have no body yet.
            foreach (var r in runs)
                if (keep.Contains(r.Key)) bodies.Add(Make(r));
            DevBodies = bodies.Count;
        }

        void FindRuns()
        {
            int n = LiquidSim.ChunkCells, cols = region.Liquid.ChunkCols;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            foreach (int c in wet)
            {
                int cx = c % cols, cy = c / cols;
                x0 = Mathf.Min(x0, cx * n);
                y0 = Mathf.Min(y0, cy * n);
                x1 = Mathf.Max(x1, Mathf.Min(g.Width, (cx + 1) * n) - 1);
                y1 = Mathf.Max(y1, Mathf.Min(g.Height, (cy + 1) * n) - 1);
            }
            for (int y = y0; y <= y1; y++)
            {
                int x = x0;
                while (x <= x1)
                {
                    if (!Surface(x, y, out bool acid)) { x++; continue; }
                    var r = new Run { X0 = x, Y = y, Acid = acid, Bottom = y, Top = 0f };
                    while (x <= x1 && Surface(x, y, out bool a2) && a2 == acid)
                    {
                        int b = y;
                        while (b - 1 >= 0 && g.GetLiquid(x, b - 1) > 0 && !Cells.Blocks(g.Get(x, b - 1))) b--;
                        r.Bottom = Mathf.Min(r.Bottom, b);
                        r.Top = Mathf.Max(r.Top, (y + g.GetLiquid(x, y) / 255f) * CellGrid.CellSize);
                        x++;
                    }
                    r.X1 = x - 1;
                    runs.Add(r);
                }
            }
        }

        /// <summary>A cell holding enough liquid with none (enough) over it.</summary>
        bool Surface(int x, int y, out bool acid)
        {
            acid = false;
            int a = g.GetLiquid(x, y);
            if (a < SurfaceMin || !Open(x, y)) return false;
            if (g.GetLiquid(x, y + 1) >= SurfaceMin && Open(x, y + 1)) return false;
            acid = g.GetLiquidKind(x, y) == LiquidKind.Acid;
            return true;
        }

        Body Make(Run r)
        {
            var body = new Body { Key = r.Key };
            const float c = CellGrid.CellSize;
            var origin = (Vector2)region.Root.position;
            float left = origin.x + r.X0 * c, right = origin.x + (r.X1 + 1) * c;
            float top = origin.y + r.Top, bottom = origin.y + r.Bottom * c;
            int depth = Mathf.CeilToInt((r.Top - r.Bottom * c) / c - 0.01f);
            if (depth >= SwimDepth)
            {
                LiquidTemplates.Region(r.Acid, out var swim, out var scale);
                if (swim != null)
                {
                    body.Region = LiquidTemplates.Place(swim, scale, region.Root, left, right, top);
                    DevSwimRegions++;
                }
            }
            if (r.Acid)
            {
                // Hallownest's Acid Box: a trigger on the hazard layer the hero's box hurts on (a hazard respawn).
                var go = new GameObject("Terra Acid Box") { layer = 17 };
                go.AddComponent<TerraOwned>();
                go.transform.SetParent(region.Root, true);
                go.transform.position = new Vector3((left + right) / 2f, (top + bottom) / 2f, 0f);
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(right - left, Mathf.Max(0.1f, top - bottom - 0.05f));
                var dh = go.AddComponent<DamageHero>();
                dh.damageDealt = 1;
                dh.hazardType = 3;
                body.Hazard = go;
                // The new box's bounds before the pool's water-walking top is laid over them.
                Physics2D.SyncTransforms();
                Player.AcidLiquid.Add(dh, body.Region != null ? body.Region.GetComponent<BoxCollider2D>() : null);
            }
            return body;
        }
    }

    /// <summary>
    /// Copies of Hollow Knight's swim regions to give poured liquid its swimming: the first water Surface Water Region and the
    /// first acid one met in the rooms loaded this session, kept switched off out of every scene.
    /// </summary>
    internal static class LiquidTemplates
    {
        static GameObject water, acid;
        static Vector3 waterScale, acidScale;
        static Transform holder;
        static int signature;

        /// <summary>When the rooms loaded change, the pools in them looked over for the kinds still missing.</summary>
        public static void Watch()
        {
            if (water != null && acid != null) return;
            int sig = 17;
            for (int i = 0; i < USM.sceneCount; i++) sig = sig * 31 + USM.GetSceneAt(i).handle;
            if (sig == signature) return;
            signature = sig;
            foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (fsm == null || fsm.FsmName != "Surface Water Region" || !fsm.gameObject.scene.isLoaded) continue;
                var go = fsm.gameObject;
                if (go.GetComponentInParent<TerraOwned>(true) != null || go.GetComponent<BoxCollider2D>() == null) continue;
                bool isAcid = false;
                foreach (var f in go.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Acid Armour Check") isAcid = true;
                if (isAcid && acid == null) { acid = Keep(go); acidScale = go.transform.lossyScale; }
                else if (!isAcid && water == null) { water = Keep(go); waterScale = go.transform.lossyScale; }
            }
        }

        static GameObject Keep(GameObject go)
        {
            if (holder == null)
            {
                var h = new GameObject("Terra Liquid Templates");
                Object.DontDestroyOnLoad(h);
                h.SetActive(false);
                holder = h.transform;
            }
            var copy = Object.Instantiate(go, holder);
            copy.name = go.name;
            // Off itself, so each copy made from it is set in place before it wakes.
            copy.SetActive(false);
            copy.AddComponent<TerraOwned>();
            Plugin.Log.LogInfo($"[液体] 记下原版{(go.GetComponents<PlayMakerFSM>().Length > 1 ? "酸池" : "水池")}的游泳区（{go.scene.name} 的 {go.name}）");
            return copy;
        }

        public static void Region(bool forAcid, out GameObject template, out Vector3 scale)
        {
            template = forAcid ? acid : water;
            scale = forAcid ? acidScale : waterScale;
        }

        /// <summary>
        /// A copy of a swim region (scale: the original's in the world) stretched over [left, right], its top at the surface,
        /// under a region's root: its colliders on, its pictures off (Terraria's water is drawn instead).
        /// </summary>
        public static GameObject Place(GameObject template, Vector3 scale, Transform parent, float left, float right, float top)
        {
            var copy = Object.Instantiate(template, parent, true);
            foreach (var col in copy.GetComponentsInChildren<Collider2D>(true)) col.enabled = true;
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            var box = copy.GetComponent<BoxCollider2D>();
            float sy = Mathf.Abs(scale.y);
            float boxW = Mathf.Max(0.01f, box.size.x), height = box.size.y * sy;
            float newSx = (right - left) / boxW;
            copy.transform.localScale = new Vector3(newSx, sy, 1f);
            var center = new Vector2((left + right) / 2f, top - height / 2f);
            copy.transform.position = new Vector3(center.x - box.offset.x * newSx, center.y - box.offset.y * sy, 0f);
            copy.transform.rotation = Quaternion.identity;
            copy.SetActive(true);
            return copy;
        }
    }
}
