using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// One scene's editable grid placed in the world: the scene being played, at its own
    /// coordinates, or a snapshot of a neighbouring scene moved so the two line up at their
    /// shared edge. Owns the grid's colliders, black fill, cut-outs and Terraria visuals, all
    /// under one root whose position is the region's offset; children work in the scene's own
    /// coordinates. Every change is recorded in <see cref="WorldEdits"/> under the scene's name,
    /// so digging into a neighbour carries over when that scene is entered.
    /// </summary>
    internal sealed class TerrainRegion
    {
        public readonly string SceneName;
        public readonly CellGrid Grid;
        public readonly Transform Root;
        public readonly bool IsLive;
        public readonly PhysicsMaterial2D Material;
        /// <summary>A boss room's: the game's own colliders stand and this grid is solid to nothing (TerrainSystem); with
        /// <see cref="OwnZone"/>, only there.</summary>
        public bool SceneOwnGround;
        /// <summary>The boss's part of a boss room (world, BossArenaZone): the game's own ground there, the grid's elsewhere.</summary>
        public Rect? OwnZone { get; private set; }
        bool[] ownCollide, ownLock;

        /// <summary>
        /// Leaves a boss's arena to the game's own colliders: the grid stops colliding in the cells wholly inside it, and
        /// those within it or under the hand-placed ground kept for it (kept whole, so not to be dug) stay as they are.
        /// </summary>
        public void SetOwnZone(Rect zone, List<Bounds> kept)
        {
            OwnZone = zone;
            ownCollide = new bool[Grid.Width * Grid.Height];
            ownLock = new bool[Grid.Width * Grid.Height];
            const float c = CellGrid.CellSize;
            Vector2 origin = Root.position;
            for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
            {
                var cell = new Rect(origin.x + x * c, origin.y + y * c, c, c);
                int i = y * Grid.Width + x;
                ownCollide[i] = zone.Contains(cell.min) && zone.Contains(cell.max);
                bool locked = zone.Contains(cell.center);
                if (!locked)
                    foreach (var b in kept)
                        if (b.min.x <= cell.center.x && cell.center.x <= b.max.x && b.min.y <= cell.center.y && cell.center.y <= b.max.y) { locked = true; break; }
                ownLock[i] = locked;
            }
            colliders.Skip = (x, y) => ownCollide[y * Grid.Width + x];
            if (!pendingBuild) colliders.BuildAll();
        }

        /// <summary>Whether a cell (local) is the game's own ground, unchanged: all of a boss room without a zone, its zone's cells with one.</summary>
        public bool IsOwnCell(int lx, int ly) => SceneOwnGround && (ownLock == null || (Grid.InClip(lx, ly) && ownLock[ly * Grid.Width + lx]));
        public HoleCutter Holes { get; private set; }
        /// <summary>Global cell of this grid's (0, 0); world position is offset * CellSize.</summary>
        public Vector2Int CellOffset { get; private set; }

        readonly ChunkColliders colliders;
        readonly CellVisuals visuals;
        readonly FillMeshes fill;
        readonly Dictionary<int, byte> edits;
        HandPlacedArt handPlaced;

        public int BoxCount => colliders.BoxCount;

        /// <param name="buildNow">Build colliders and drawing straight away; otherwise on the first <see cref="SetCuts"/>
        /// (a neighbour gets its cuts at once, and building twice is most of its set-up time).</param>
        public TerrainRegion(string sceneName, CellGrid grid, Transform root, bool live, PhysicsMaterial2D material, IEnumerable<SpriteRenderer> art, bool buildNow = true)
        {
            SceneName = sceneName;
            Grid = grid;
            Root = root;
            IsLive = live;
            Material = material;
            edits = WorldEdits.For(sceneName);

            colliders = new ChunkColliders(grid, root, material);
            fill = new FillMeshes(grid, root, grid.IsCut);
            Holes = new HoleCutter(root, art);
            visuals = new CellVisuals(grid, root, Holes, fill) { DugWallSheet = Areas.WallFor(sceneName) };
            grid.CellChanged += OnCellChanged;
            grid.WallChanged += OnWallChanged;
            if (buildNow) Build();
            else pendingBuild = true;
        }

        bool pendingBuild;

        /// <summary>Dev: shows another wall behind this region's dug cells (Areas.WallFor gives the scene's own).</summary>
        public void DevSetDugWall(string sheet) => visuals.SetDugWall(sheet);

        void Build()
        {
            pendingBuild = false;
            colliders.BuildAll();
            fill.BuildAll();
            foreach (int i in edits.Keys) visuals.MarkDirty(i % Grid.Width, i / Grid.Width);
            foreach (int i in WorldWalls.For(SceneName).Keys) visuals.MarkDirty(i % Grid.Width, i / Grid.Width);
            visuals.RefreshDirty();
            fill.RebuildDirty();
        }

        /// <summary>Replays this scene's saved edits into a freshly rasterised grid, before construction.</summary>
        public static void ReplayEdits(CellGrid grid, string sceneName)
        {
            grid.CaptureNetworkBase();
            foreach (var w in WorldWalls.For(sceneName)) grid.RestoreWall(w.Key, w.Value);
            foreach (var e in WorldEdits.For(sceneName))
            {
                WorldEdits.Unpack(e.Value, out var kind, out bool wasDug, out int slope);
                grid.Restore(e.Key, kind, wasDug, slope);
            }
        }

        /// <summary>Ties the art of the scene's hand-placed terrain to its cells (call while those colliders are still on).</summary>
        public void TrackHandPlaced(IEnumerable<Collider2D> colliders, List<Renderer> sceneArt) =>
            handPlaced = new HandPlacedArt(Grid, colliders, (Vector2)CellOffset * CellGrid.CellSize, sceneArt, Holes);

        public void SetOffset(Vector2Int offset)
        {
            CellOffset = offset;
            Root.position = new Vector3(offset.x * CellGrid.CellSize, offset.y * CellGrid.CellSize, Root.position.z);
        }

        public bool Contains(int gx, int gy) => Grid.InClip(gx - CellOffset.x, gy - CellOffset.y);

        /// <summary>Gives up the given areas of the grid (local cells) to neighbours and rebuilds what that changes.</summary>
        public void SetCuts(IEnumerable<RectInt> areas)
        {
            if (!Grid.SetCuts(areas) && !pendingBuild) return;
            Build();
        }

        /// <summary>Drop through this region's platforms around a global cell rectangle for a while.</summary>
        public void SuspendPlatforms(RectInt globalCells, float seconds) =>
            colliders.SuspendPlatforms(new RectInt(globalCells.position - CellOffset, globalCells.size), seconds);

        /// <summary>World x of a column boundary of this grid.</summary>
        public float ColumnX(int localColumn) => (CellOffset.x + localColumn) * CellGrid.CellSize;

        /// <summary>The whole grid in global cells (cut areas included).</summary>
        public RectInt CellRect => new RectInt(CellOffset.x, CellOffset.y, Grid.Width, Grid.Height);

        /// <summary>Any region's cell changed and was recorded in the save's edits (local cells): what a LAN game passes on.</summary>
        public static event System.Action<TerrainRegion, int, int> Edited;

        public static event System.Action<TerrainRegion, int, int> WallEdited;
        void OnWallChanged(int x, int y)
        {
            int i = y * Grid.Width + x;
            WorldWalls.For(SceneName)[i] = Grid.WallAt(i);
            visuals.MarkDirty(x, y);
            WallEdited?.Invoke(this, x, y);
        }

        void OnCellChanged(int x, int y)
        {
            int i = y * Grid.Width + x;
            edits[i] = WorldEdits.Pack(Grid.GetAt(i), Grid.IsDugAt(i), Grid.SlopeAt(i));
            colliders.MarkDirty(x, y);
            visuals.MarkDirty(x, y);
            handPlaced?.CellChanged(x, y);
            TerraWorld.RaiseChanged(x + CellOffset.x, y + CellOffset.y, Grid.GetAt(i));
            Edited?.Invoke(this, x, y);
        }

        public void Tick()
        {
            if (pendingBuild) Build();
            colliders.RebuildDirty();
            visuals.RefreshDirty();
            fill.RebuildDirty();
        }

        public void Destroy()
        {
            Grid.CellChanged -= OnCellChanged;
            Grid.WallChanged -= OnWallChanged;
            fill.DestroyMeshes();
            if (Root != null) Object.Destroy(Root.gameObject);
        }
    }
}
