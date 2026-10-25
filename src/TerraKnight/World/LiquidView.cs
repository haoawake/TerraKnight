using System.Collections.Generic;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.World
{
    /// <summary>
    /// How a region's poured liquid looks and feels. It is drawn as Terraria draws liquid (LiquidRenderer): each cell a
    /// 16-px frame of the water sheet (Images/Misc/water_0 for water, the jungle's green water_3 for acid, the shimmer's
    /// water_14 tinted and sparkling as LiquidRenderer.DrawShimmer has it) picked by which
    /// sides are open — the surface frame on top, the edge frames at the sides, the inside elsewhere — a cell partly full
    /// cut to its level with the surface frame at the top, the frames stepping through the sheet's 16 animation frames; more
    /// see-through than Terraria's (Hallownest's water is clear), a mesh per 16×16 cells. Every stretch of surface is given
    /// a bounded volume for water-walking and gear, and for acid an Acid Box that hurts as Hallownest's acid does.
    /// PouredSwimming applies physical buoyancy from the actual cells without the native surface FSM taking control.
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
        const string WaterSheet = "Misc/water_0", AcidSheet = "Misc/water_3", ShimmerSheet = "Misc/water_14";
        /// <summary>The shimmer's opacity (Terraria's 0.75 for it in front, a little clearer as the water here).</summary>
        const float ShimmerAlpha = 0.6f;

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
            /// <summary>Each vertex's step on its sheet for one animation frame (80 px of that sheet's height).</summary>
            public readonly List<float> FrameStep = new List<float>();
            /// <summary>The colours as built; the shimmer's quads are coloured again each frame (Step).</summary>
            public readonly List<Color> Colors = new List<Color>();
            public readonly List<Color> Shown = new List<Color>();
            public readonly List<ShimmerQuad> Shimmer = new List<ShimmerQuad>();
        }

        /// <summary>A quad of shimmer: its first vertex, its cell, and whether it is the sparkle laid over the body (edge: not an inside frame).</summary>
        struct ShimmerQuad
        {
            public int V, X, Y;
            public bool Sparkle, Edge;
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
        static readonly List<int> waterTris = new List<int>(), acidTris = new List<int>(), shimmerTris = new List<int>(), sparkleTris = new List<int>();
        static Material waterMaterial, acidMaterial, shimmerMaterial, sparkleMaterial;

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
            ch.FrameStep.Clear();
            ch.Shimmer.Clear();
            waterTris.Clear();
            acidTris.Clear();
            shimmerTris.Clear();
            sparkleTris.Clear();
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
                ch.Go.AddComponent<MeshRenderer>().sharedMaterials = new[] { Material(LiquidKind.Water), Material(LiquidKind.Acid), Material(LiquidKind.Shimmer), SparkleMaterial() };
            }
            ch.Mesh.Clear();
            ch.Mesh.SetVertices(verts);
            ch.Colors.Clear();
            ch.Colors.AddRange(colors);
            ch.Mesh.SetColors(colors);
            ch.Mesh.subMeshCount = 4;
            Step(ch);
            ch.Mesh.SetTriangles(waterTris, 0);
            ch.Mesh.SetTriangles(acidTris, 1);
            ch.Mesh.SetTriangles(shimmerTris, 2);
            ch.Mesh.SetTriangles(sparkleTris, 3);
            ch.Mesh.RecalculateBounds();
            if (!ch.Go.activeSelf) ch.Go.SetActive(true);
        }

        /// <summary>
        /// The texture coordinates on the frame shown now: each sheet frame 80 px under the last. The shimmer's sparkles go
        /// by their own frame (GetShimmerFrame), and its quads take its colours of this moment (SetShimmerVertexColors, the
        /// sparkles' GetShimmerGlitterColor).
        /// </summary>
        void Step(Chunk ch)
        {
            if (ch.Mesh == null) return;
            ch.Uv.Clear();
            for (int i = 0; i < ch.BaseUv.Count; i++)
                ch.Uv.Add(ch.BaseUv[i] - new Vector2(0f, (ch.Slow[i] ? slowFrame : frame) * ch.FrameStep[i]));
            if (ch.Shimmer.Count > 0)
            {
                float t = Time.time * 60f;
                ch.Shown.Clear();
                ch.Shown.AddRange(ch.Colors);
                foreach (var q in ch.Shimmer)
                {
                    int wx = q.X + region.CellOffset.x, wy = -(q.Y + region.CellOffset.y);
                    if (q.Sparkle)
                    {
                        float off = ShimmerFrame(q.Edge, wx, wy, t) * ch.FrameStep[q.V];
                        for (int k = 0; k < 4; k++) ch.Uv[q.V + k] = ch.BaseUv[q.V + k] - new Vector2(0f, off);
                        ch.Shown[q.V] = Glitter(q.Edge, wx, wy + 1, t);
                        ch.Shown[q.V + 1] = Glitter(q.Edge, wx, wy, t);
                        ch.Shown[q.V + 2] = Glitter(q.Edge, wx + 1, wy, t);
                        ch.Shown[q.V + 3] = Glitter(q.Edge, wx + 1, wy + 1, t);
                    }
                    else
                    {
                        ch.Shown[q.V] = ShimmerBase(wx, wy + 1, t);
                        ch.Shown[q.V + 1] = ShimmerBase(wx, wy, t);
                        ch.Shown[q.V + 2] = ShimmerBase(wx + 1, wy, t);
                        ch.Shown[q.V + 3] = ShimmerBase(wx + 1, wy + 1, t);
                    }
                }
                ch.Mesh.SetColors(ch.Shown);
            }
            ch.Mesh.SetUVs(0, ch.Uv);
        }

        // ------------------------------------------------------------------ the shimmer's colours (LiquidRenderer)

        static float ShimmerWave(float x, float y, float t) => Mathf.Sin(((x + y / 6f) / 10f - t / 360f) * 2f * Mathf.PI);

        /// <summary>GetShimmerBaseColor: lavender to pale blue in slow waves, at the shimmer's opacity.</summary>
        static Color ShimmerBase(float x, float y, float t)
        {
            var c = Vector4.Lerp(new Vector4(0.64705884f, 26f / 51f, 14f / 15f, 1f), new Vector4(41f / 51f, 41f / 51f, 1f, 1f), 0.1f + ShimmerWave(x, y, t) * 0.4f);
            return new Color(c.x, c.y, c.z, ShimmerAlpha);
        }

        /// <summary>GetShimmerGlitterColor: a rainbow hue running along it, as bright as GetShimmerGlitterOpacity (added light: its strength in alpha).</summary>
        static Color Glitter(bool top, float x, float y, float t)
        {
            var c = Color.HSVToRGB(Mathf.Repeat(((x + y / 6f) + t / 30f) / 6f, 1f), 1f, 1f);
            float a;
            if (top) a = 0.5f;
            else
            {
                float wave = Mathf.Clamp01(Mathf.InverseLerp(-0.5f, 1f, ShimmerWave(x, y, t))) * 0.35f;
                float noise = Mathf.Sin(Noise((uint)x, (uint)y) / 10f + t / 180f);
                a = Mathf.Clamp01(Mathf.InverseLerp(0f, 0.5f, wave * noise));
            }
            c.a = a * ShimmerAlpha;
            return c;
        }

        static uint Noise(uint x, uint y)
        {
            x = 36469 * (x & 0xFFFF) + (x >> 16);
            y = 18012 * (y & 0xFFFF) + (y >> 16);
            return (x << 16) + y;
        }

        /// <summary>GetShimmerFrame: the sparkle's frame of the sheet, drifting along it (an inside one by its cell too).</summary>
        static int ShimmerFrame(bool top, float x, float y, float t)
        {
            x += 0.5f;
            y += 0.5f;
            double n = (x + y / 6f) / 10f - t / 360f;
            if (!top) n += x + y;
            return ((int)n % 16 + 16) % 16;
        }

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
            var kind = g.GetLiquidKind(x, y);
            bool acid = kind == LiquidKind.Acid, shimmer = kind == LiquidKind.Shimmer;
            var tex = Material(kind).mainTexture;
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
            var col = acid ? AcidTint : shimmer ? new Color(1f, 1f, 1f, ShimmerAlpha) : WaterTint;
            for (int k = 0; k < 4; k++)
            {
                ch.Slow.Add(slow);
                ch.FrameStep.Add(80f / H);
                colors.Add(col);
            }
            var tris = acid ? acidTris : shimmer ? shimmerTris : waterTris;
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            if (!shimmer) return;
            ch.Shimmer.Add(new ShimmerQuad { V = b, X = x, Y = y });
            // DrawShimmer's sparkle: the sheet's right half (48 px over), over every frame but the inside one, and over that
            // one on every other cell.
            bool edge = fx != 16 || fy % 80 != 48;
            if (!edge && (x + y) % 2 != 0) return;
            int s = verts.Count;
            verts.Add(new Vector3(x0, y0));
            verts.Add(new Vector3(x0, y1));
            verts.Add(new Vector3(x0 + c, y1));
            verts.Add(new Vector3(x0 + c, y0));
            float su0 = (fx + 48) / W, su1 = (fx + 64) / W;
            ch.BaseUv.Add(new Vector2(su0, vBottom));
            ch.BaseUv.Add(new Vector2(su0, vTop));
            ch.BaseUv.Add(new Vector2(su1, vTop));
            ch.BaseUv.Add(new Vector2(su1, vBottom));
            for (int k = 0; k < 4; k++)
            {
                ch.Slow.Add(false);
                ch.FrameStep.Add(80f / H);
                colors.Add(Color.clear);
            }
            ch.Shimmer.Add(new ShimmerQuad { V = s, X = x, Y = y, Sparkle = true, Edge = edge });
            sparkleTris.Add(s); sparkleTris.Add(s + 1); sparkleTris.Add(s + 2);
            sparkleTris.Add(s); sparkleTris.Add(s + 2); sparkleTris.Add(s + 3);
        }

        static Material Material(LiquidKind kind)
        {
            var known = kind == LiquidKind.Acid ? acidMaterial : kind == LiquidKind.Shimmer ? shimmerMaterial : waterMaterial;
            if (known != null) return known;
            var m = new Material(Shader.Find("Sprites/Default")) { name = kind == LiquidKind.Acid ? "Terra Acid" : kind == LiquidKind.Shimmer ? "Terra Shimmer" : "Terra Water" };
            m.mainTexture = Terraria.TerrariaAssets.Texture(kind == LiquidKind.Acid ? AcidSheet : kind == LiquidKind.Shimmer ? ShimmerSheet : WaterSheet);
            if (m.mainTexture != null) m.mainTexture.wrapMode = TextureWrapMode.Clamp;
            if (kind == LiquidKind.Acid) acidMaterial = m;
            else if (kind == LiquidKind.Shimmer) shimmerMaterial = m;
            else waterMaterial = m;
            return m;
        }

        /// <summary>The shimmer's sparkles: its sheet, added as light (their colours carry no alpha in Terraria's draw).</summary>
        static Material SparkleMaterial()
        {
            if (sparkleMaterial != null) return sparkleMaterial;
            var shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Particles/Additive") ?? Shader.Find("Sprites/Default");
            sparkleMaterial = new Material(shader) { name = "Terra Shimmer Sparkle", mainTexture = Material(LiquidKind.Shimmer).mainTexture };
            return sparkleMaterial;
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
                        while (b - 1 >= 0 && g.GetLiquid(x, b - 1) > 0 && g.GetLiquidKind(x, b - 1) == g.GetLiquidKind(x, y) && !Cells.Blocks(g.Get(x, b - 1))) b--;
                        // Each column must stop at its own floor. A single deepest
                        // column must not extend the swim region below adjacent blocks.
                        if (x > r.X0 && b != r.Bottom) break;
                        r.Bottom = b;
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
                // Native Surface Water Region takes control and snaps the hero to
                // its surface. Poured liquid can fill doorways and have solid roofs;
                // its buoyancy uses actual grid cells instead (PouredSwimming).
                body.Region = new GameObject("Terra Poured Swim Region");
                body.Region.AddComponent<TerraOwned>();
                body.Region.AddComponent<PouredLiquidRegion>();
                body.Region.transform.SetParent(region.Root, true);
                body.Region.transform.position = new Vector3((left + right) / 2f, (top + bottom) / 2f, 0f);
                var volume = body.Region.AddComponent<BoxCollider2D>();
                volume.isTrigger = true;
                volume.size = new Vector2(right - left, top - bottom);
                volume.enabled = false;
                DevSwimRegions++;
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

    internal sealed class PouredLiquidRegion : MonoBehaviour { }

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
            // Hallownest's pools kill what falls in (a DamageEnemies of the water's or the acid's, RuinsWater or Acid, that
            // only enemies made to swim ignore): poured liquid hurts no enemy, a boss least of all (Terraria's water hurts no
            // one; its lava spares the bosses).
            var removed = new List<string>();
            foreach (var de in copy.GetComponentsInChildren<DamageEnemies>(true))
            {
                removed.Add($"{de.gameObject.name}:{de.attackType}×{de.damageDealt}");
                Object.DestroyImmediate(de);
            }
            foreach (var f in copy.GetComponentsInChildren<PlayMakerFSM>(true))
                if (f.FsmName == "damages_enemy")
                {
                    removed.Add(f.gameObject.name + ":damages_enemy");
                    Object.DestroyImmediate(f);
                }
            Plugin.Log.LogInfo($"[液体] 记下原版{(go.GetComponents<PlayMakerFSM>().Length > 1 ? "酸池" : "水池")}的游泳区（{go.scene.name} 的 {go.name}）"
                + (removed.Count > 0 ? $"，去掉伤敌的部分 {string.Join("、", removed)}" : "，里面没有伤敌的部分"));
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
