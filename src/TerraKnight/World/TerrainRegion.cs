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

        /// <summary>
        /// Where what the mod lays in the room is drawn (drops): beside <see cref="Root"/>, at its place, in its scene, never
        /// switched off. A boss room that keeps the game's own ground whole switches the terrain root off (TerrainSystem),
        /// and all under it with it.
        /// </summary>
        public Transform Props
        {
            get
            {
                if (props == null && Root != null)
                {
                    var go = new GameObject("TerraKnight Props");
                    go.AddComponent<TerraOwned>();
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, Root.gameObject.scene);
                    props = go.transform;
                }
                if (props != null && Root != null && (props.position != Root.position || props.rotation != Root.rotation))
                    props.SetPositionAndRotation(Root.position, Root.rotation);
                return props;
            }
        }
        Transform props;

        public readonly bool IsLive;
        public readonly PhysicsMaterial2D Material;
        /// <summary>A boss room's: the game's own colliders stand and this grid is solid to nothing (TerrainSystem); with
        /// <see cref="OwnZone"/>, only there.</summary>
        public bool SceneOwnGround;
        /// <summary>The boss's part of a boss room (world, BossArenaZone): the game's own ground there, the grid's elsewhere.</summary>
        public Rect? OwnZone { get; private set; }
        bool[] ownCollide, ownLock;

        /// <summary>
        /// Leaves a boss's arena (<paramref name="zone"/>, and with the ground under it <paramref name="ground"/>) to the game's
        /// own colliders: the grid stops colliding in the cells wholly inside the ground and in those the hand-placed ground
        /// kept for it covers (that collider, solid, stands there: the grid's outline over the
        /// same ground is hollow, and a boss pressed into a wall (a pose's collider reaching back) would be drawn into it
        /// and fall through the floor); those within it or under that ground (kept whole, so not to be dug) stay as they are.
        /// </summary>
        public void SetOwnZone(Rect zone, Rect ground, List<Collider2D> kept)
        {
            OwnZone = zone;
            ownCollide = new bool[Grid.Width * Grid.Height];
            ownLock = new bool[Grid.Width * Grid.Height];
            const float c = CellGrid.CellSize;
            Vector2 origin = Root.position;
            var keptBounds = new List<Bounds>(kept.Count);
            foreach (var k in kept) keptBounds.Add(k.bounds);
            for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
            {
                var cell = new Rect(origin.x + x * c, origin.y + y * c, c, c);
                int i = y * Grid.Width + x;
                bool covered = false, underKept = false;
                for (int k = 0; k < kept.Count; k++)
                {
                    var b = keptBounds[k];
                    if (b.min.x > cell.center.x || cell.center.x > b.max.x || b.min.y > cell.center.y || cell.center.y > b.max.y) continue;
                    underKept = true;
                    if (kept[k].OverlapPoint(cell.center)) { covered = true; break; }
                }
                ownCollide[i] = ground.Contains(cell.min) && ground.Contains(cell.max) || covered;
                ownLock[i] = ground.Contains(cell.center) || underKept;
            }
            colliders.Skip = (x, y) => ownCollide[y * Grid.Width + x] && Grid.Get(x, y) == CellKind.Original;
            colliders.Built = BuiltCell;
            if (!pendingBuild) colliders.BuildAll();
        }

        /// <summary>A boss room's cell the player made (not the room's own ground): its own colliders (ChunkColliders.Built).</summary>
        bool BuiltCell(int x, int y) => Grid.Get(x, y) != CellKind.Original;

        /// <summary>Keep the native room, while the active grid draws and collides with player construction.</summary>
        public void KeepWholeRoomGround()
        {
            colliders.Skip = (x, y) => Grid.Get(x, y) == CellKind.Original;
            colliders.Built = BuiltCell;
            fill.Skip = (x, y) => true; // Native render meshes already provide the original ground's fill.
            if (!pendingBuild) { colliders.BuildAll(); fill.BuildAll(); }
        }

        /// <summary>Whether a cell (local) is the game's own ground, unchanged: all of a boss room without a zone, its zone's cells with one.</summary>
        public bool IsOwnCell(int lx, int ly) => SceneOwnGround && (ownLock == null || (Grid.InClip(lx, ly) && ownLock[ly * Grid.Width + lx]));
        public HoleCutter Holes { get; private set; }
        /// <summary>Global cell of this grid's (0, 0); world position is offset * CellSize.</summary>
        public Vector2Int CellOffset { get; private set; }

        readonly ChunkColliders colliders;
        readonly CellVisuals visuals;
        readonly FillMeshes fill;
        readonly Dictionary<int, ushort> edits;
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
            fill.Skip = visuals.FillElsewhere;
            grid.CellChanged += OnCellChanged;
            grid.WallChanged += OnWallChanged;
            Liquid = new LiquidSim(grid);
            Liquid.Merged += OnLiquidMerged;
            liquidView = new LiquidView(this);
            if (buildNow) Build();
            else pendingBuild = true;
            Liquid.WakeAll();
        }

        /// <summary>The grid's liquid, moving (World.LiquidSim), and how it is drawn and felt (World.LiquidView).</summary>
        public readonly LiquidSim Liquid;
        readonly LiquidView liquidView;

        /// <summary>
        /// Poured liquid inside one of the scene's own water or acid pools is none (pouring there is refused; a save from
        /// before may hold some): cleared once the scene's objects are in place (TerrainSystem).
        /// </summary>
        public void ClearInsideGamePools(UnityEngine.SceneManagement.Scene scene)
        {
            if (!Grid.HasLiquids) return;
            int cleared = 0;
            const float c = CellGrid.CellSize;
            var origin = (Vector2)Root.position;
            void Clear(BoxCollider2D box)
            {
                var t = box.transform;
                Vector2 center = t.TransformPoint(box.offset), size = Vector2.Scale(box.size, t.lossyScale);
                size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
                int x0 = Mathf.FloorToInt((center.x - size.x / 2f - origin.x) / c), x1 = Mathf.CeilToInt((center.x + size.x / 2f - origin.x) / c);
                int y0 = Mathf.FloorToInt((center.y - size.y / 2f - origin.y) / c) - 40, y1 = Mathf.CeilToInt((center.y + size.y / 2f - origin.y) / c);
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(Grid.Height, y1); y++)
                    for (int x = Mathf.Max(0, x0); x < Mathf.Min(Grid.Width, x1); x++)
                        if (Grid.GetLiquid(x, y) > 0) { Liquid.Set(x, y, 0, LiquidKind.Water); cleared++; }
            }
            foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (fsm != null && fsm.gameObject.scene == scene && fsm.FsmName == "Surface Water Region" && fsm.GetComponentInParent<TerraOwned>(true) == null
                    && fsm.GetComponent<BoxCollider2D>() is BoxCollider2D region) Clear(region);
            foreach (var dh in Object.FindObjectsByType<DamageHero>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (dh != null && dh.hazardType == 3 && dh.gameObject.scene == scene && dh.GetComponentInParent<TerraOwned>(true) == null
                    && dh.GetComponent<BoxCollider2D>() is BoxCollider2D acid) Clear(acid);
            if (cleared > 0) Plugin.Log.LogInfo($"[液体] {SceneName}：原版水池里留着的 {cleared} 格倒出来的液体清掉");
        }

        bool liquidFailed;

        /// <summary>This grid's liquid into the scene's record (WorldLiquids).</summary>
        public void CaptureLiquids() => WorldLiquids.Capture(SceneName, Grid);
        float liquidClock;

        /// <summary>Water met acid: obsidian there (Liquid.CreateLiquidMergeTile), with a hiss.</summary>
        void OnLiquidMerged(int x, int y)
        {
            if (Grid.Get(x, y) != CellKind.Empty) return;
            Grid.Set(x, y, CellKind.Obsidian);
            TerraMod.Instance?.PlaySound("Splash_1", 0.5f);
        }

        bool pendingBuild;

        /// <summary>Dev: shows another wall behind this region's dug cells (Areas.WallFor gives the scene's own).</summary>
        public void DevSetDugWall(string sheet) => visuals.SetDugWall(sheet);

        /// <summary>The holes' style switched (World.HoleShapes): every hole drawn again on the next tick.</summary>
        public void RestyleHoles() => visuals.MarkAll();

        internal void RestoreScenery() => Holes.RestoreDecor(Grid);

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
            RegionalTorches.Replay(grid, sceneName);
            WorldLiquids.Replay(grid, sceneName);
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
            RegionalTorches.Record(SceneName, Grid, i);
            colliders.MarkDirty(x, y);
            visuals.MarkDirty(x, y);
            handPlaced?.CellChanged(x, y);
            if (!Grid.IsDug(x, y)) Holes.RestoreDecor(Grid);
            Liquid?.CellChanged(x, y);
            TerraWorld.RaiseChanged(x + CellOffset.x, y + CellOffset.y, Grid.GetAt(i));
            Edited?.Invoke(this, x, y);
        }

        public void Tick()
        {
            if (pendingBuild) Build();
            colliders.RebuildDirty();
            visuals.RefreshDirty();
            fill.RebuildDirty();
            if (liquidFailed) return;
            try
            {
                // Terraria's liquid steps 60 times a second; a slow frame catches up at most four steps.
                liquidClock += Mathf.Min(Time.deltaTime, 0.1f);
                int steps = 0;
                while (liquidClock >= LiquidStep && steps < 4)
                {
                    liquidClock -= LiquidStep;
                    Liquid.Step();
                    steps++;
                }
                if (steps == 4) liquidClock = 0f;
                liquidView.Tick();
            }
            catch (System.Exception e)
            {
                // Once: the terrain goes on, this region's liquid stops.
                liquidFailed = true;
                Plugin.Log.LogError($"[液体] {SceneName} 的液体出错，本场景的液体停下：{e}");
            }
        }

        const float LiquidStep = 1f / 60f;

        public void Destroy()
        {
            CaptureLiquids();
            liquidView.Destroy();
            Grid.CellChanged -= OnCellChanged;
            Grid.WallChanged -= OnWallChanged;
            fill.DestroyMeshes();
            Holes?.DestroyMasks();
            if (Root != null) Object.Destroy(Root.gameObject);
        }
    }
}
