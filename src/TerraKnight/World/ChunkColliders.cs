using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Collision for the grid, split into chunks the size of the game's own tilemap partitions
    /// (32 units) so seams fall where vanilla already has them. Each chunk merges greedy boxes
    /// into an outline CompositeCollider2D, which behaves like the hollow tk2d edge outlines the
    /// hero and enemies were written against. Platforms get a second, one-way composite per chunk
    /// (a PlatformEffector2D, as the game's own one-way platforms have).
    /// </summary>
    internal sealed class ChunkColliders
    {
        public const int ChunkCells = 64;
        public const int TerrainLayer = 8;

        sealed class Chunk
        {
            public GameObject Go;
            public CompositeCollider2D Composite;
            public readonly List<BoxCollider2D> Boxes = new List<BoxCollider2D>();
            public GameObject PlatformGo;
            public CompositeCollider2D Platforms;
            public readonly List<BoxCollider2D> PlatformBoxes = new List<BoxCollider2D>();
            public float SuspendedUntil;
        }

        readonly CellGrid grid;
        readonly Transform root;
        readonly PhysicsMaterial2D material;
        readonly int cols, rows;
        readonly Chunk[] chunks;
        readonly bool[] dirty;
        readonly GreedyRects greedy = new GreedyRects(ChunkCells);
        bool anySuspended;

        public ChunkColliders(CellGrid grid, Transform root, PhysicsMaterial2D material)
        {
            this.grid = grid;
            this.root = root;
            this.material = material;
            cols = (grid.Width + ChunkCells - 1) / ChunkCells;
            rows = (grid.Height + ChunkCells - 1) / ChunkCells;
            chunks = new Chunk[cols * rows];
            dirty = new bool[cols * rows];
        }

        public int BoxCount { get; private set; }

        public void BuildAll()
        {
            BoxCount = 0;
            for (int cy = 0; cy < rows; cy++)
            for (int cx = 0; cx < cols; cx++)
                Rebuild(cx, cy);
        }

        public void MarkDirty(int x, int y) => dirty[(y / ChunkCells) * cols + x / ChunkCells] = true;

        public void RebuildDirty()
        {
            for (int i = 0; i < dirty.Length; i++)
            {
                if (!dirty[i]) continue;
                dirty[i] = false;
                Rebuild(i % cols, i / cols);
            }
            if (!anySuspended) return;
            anySuspended = false;
            foreach (var c in chunks)
            {
                if (c == null || c.PlatformGo == null || c.PlatformGo.activeSelf) continue;
                if (Time.time >= c.SuspendedUntil) c.PlatformGo.SetActive(true);
                else anySuspended = true;
            }
        }

        /// <summary>Lets everything fall through the platforms of the chunks overlapping a local cell rectangle for a while.</summary>
        public void SuspendPlatforms(RectInt area, float seconds)
        {
            int cx0 = Mathf.Max(0, area.xMin / ChunkCells), cx1 = Mathf.Min(cols - 1, (area.xMax - 1) / ChunkCells);
            int cy0 = Mathf.Max(0, area.yMin / ChunkCells), cy1 = Mathf.Min(rows - 1, (area.yMax - 1) / ChunkCells);
            for (int cy = cy0; cy <= cy1; cy++)
            for (int cx = cx0; cx <= cx1; cx++)
            {
                var c = chunks[cy * cols + cx];
                if (c?.PlatformGo == null) continue;
                // The whole object: with only the composite off, its boxes would act as plain solid colliders.
                c.PlatformGo.SetActive(false);
                c.SuspendedUntil = Mathf.Max(c.SuspendedUntil, Time.time + seconds);
                anySuspended = true;
            }
        }

        bool Blocks(int x, int y) => grid.InClip(x, y) && Cells.Blocks(grid.Get(x, y));

        // Only flat platforms collide here; a stair is walked by StairCollision instead.
        bool IsPlatform(int x, int y) => grid.InClip(x, y) && grid.Get(x, y) == CellKind.Platform && grid.GetSlope(x, y) == 0;

        void Rebuild(int cx, int cy)
        {
            int x0 = cx * ChunkCells, y0 = cy * ChunkCells;
            int x1 = Mathf.Min(x0 + ChunkCells, grid.Width), y1 = Mathf.Min(y0 + ChunkCells, grid.Height);
            var chunk = chunks[cy * cols + cx];
            int boxes = 0, platforms = 0;
            const float c = CellGrid.CellSize;

            greedy.Run(x0, y0, x1, y1, Blocks, (x, y, w, h) =>
            {
                if (chunk == null) chunk = CreateChunk(cx, cy);
                var box = boxes < chunk.Boxes.Count ? chunk.Boxes[boxes] : AddBox(chunk.Go, chunk.Boxes);
                box.size = new Vector2(w * c, h * c);
                box.offset = new Vector2((x - x0 + w * 0.5f) * c, (y - y0 + h * 0.5f) * c);
                box.enabled = true;
                boxes++;
            });
            // Platforms: one box per horizontal run, a full cell tall (only the top surface collides).
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    if (!IsPlatform(x, y)) continue;
                    int start = x;
                    while (x + 1 < x1 && IsPlatform(x + 1, y)) x++;
                    if (chunk == null) chunk = CreateChunk(cx, cy);
                    if (chunk.PlatformGo == null) CreatePlatforms(chunk);
                    var box = platforms < chunk.PlatformBoxes.Count ? chunk.PlatformBoxes[platforms] : AddBox(chunk.PlatformGo, chunk.PlatformBoxes, platform: true);
                    int w = x - start + 1;
                    box.size = new Vector2(w * c, c);
                    box.offset = new Vector2((start - x0 + w * 0.5f) * c, (y - y0 + 0.5f) * c);
                    box.enabled = true;
                    platforms++;
                }

            if (chunk == null) return;
            for (int i = boxes; i < chunk.Boxes.Count; i++) chunk.Boxes[i].enabled = false;
            chunk.Composite.GenerateGeometry();
            if (chunk.Platforms != null)
            {
                for (int i = platforms; i < chunk.PlatformBoxes.Count; i++) chunk.PlatformBoxes[i].enabled = false;
                chunk.Platforms.GenerateGeometry();
            }
            BoxCount += boxes;
        }

        Chunk CreateChunk(int cx, int cy)
        {
            // The hero reads collision.gameObject, which is the Rigidbody2D's object, so it must be on
            // the terrain layer and carry no NonSlider/SteepSlope.
            var go = new GameObject($"Terra Chunk {cx} {cy}") { layer = TerrainLayer };
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(cx * ChunkCells * CellGrid.CellSize, cy * ChunkCells * CellGrid.CellSize, 0f);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
            composite.generationType = CompositeCollider2D.GenerationType.Manual;
            composite.sharedMaterial = material;
            var chunk = new Chunk { Go = go, Composite = composite };
            chunks[cy * cols + cx] = chunk;
            return chunk;
        }

        /// <summary>Every platform composite built, in every scene (for the bosses that go through them, BossPlatforms).</summary>
        internal static readonly List<CompositeCollider2D> AllPlatforms = new List<CompositeCollider2D>();

        void CreatePlatforms(Chunk chunk)
        {
            var go = new GameObject("Terra Platforms") { layer = TerrainLayer };
            go.transform.SetParent(chunk.Go.transform, false);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Manual;
            composite.sharedMaterial = material;
            composite.usedByEffector = true;
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
            effector.useSideFriction = false;
            effector.useSideBounce = false;
            chunk.PlatformGo = go;
            chunk.Platforms = composite;
            AllPlatforms.Add(composite);
        }

        static BoxCollider2D AddBox(GameObject go, List<BoxCollider2D> list, bool platform = false)
        {
            var box = go.AddComponent<BoxCollider2D>();
            box.compositeOperation = Collider2D.CompositeOperation.Merge;
            list.Add(box);
            return box;
        }
    }
}
