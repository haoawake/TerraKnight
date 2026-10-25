using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Owns the editable grid of one loaded gameplay scene: the one being played, or a
    /// neighbouring scene loaded beside it. Lives on the scene's TileMap object, so it is created
    /// with the scene and destroyed with it. The tilemap's chunk edge colliders and the scene's
    /// plain hand-placed terrain colliders are switched off and replaced by the grid's own
    /// colliders, and the tilemap's black render meshes by the grid's fill. The grid works in the
    /// scene's own coordinates; a neighbour moved into place gets a matching region offset.
    /// </summary>
    internal sealed class TerrainSystem : MonoBehaviour
    {
        /// <summary>Cells this close to where the world stops can't be dug, so the hero can't leave it.</summary>
        public const int BorderCells = 2;

        /// <summary>The scene being played.</summary>
        public static TerrainSystem Current { get; set; }
        /// <summary>Every loaded scene's grid, the one being played included.</summary>
        public static readonly List<TerrainSystem> All = new List<TerrainSystem>();

        /// <summary>The grid of a loaded scene, by name.</summary>
        public static TerrainRegion Find(string scene)
        {
            foreach (var t in All) if (t != null && t.SceneName == scene) return t.Region;
            return null;
        }

        public TerrainRegion Region { get; private set; }
        public CellGrid Grid => Region.Grid;
        public string SceneName { get; private set; }
        public HoleCutter Holes => Region.Holes;
        public tk2dTileMap Map { get; private set; }
        /// <summary>Dev: the largest of the hand-placed colliders folded into the grid, and its bounds.</summary>
        public Collider2D FirstHandPlaced { get; private set; }
        public Bounds FirstHandPlacedBounds { get; private set; }
        /// <summary>Dev: every hand-placed collider folded into the grid, with its bounds (taken while it was on).</summary>
        public readonly List<(string Name, Bounds Bounds)> HandPlaced = new List<(string, Bounds)>();

        /// <param name="prebuilt">A grid already rasterised from this map (by <see cref="BuildGrid"/>), to use instead of building another.</param>
        public static TerrainSystem AttachTo(tk2dTileMap map, string sceneName, bool makeCurrent = true, CellGrid prebuilt = null)
        {
            // The game's map asks for the tilemap without naming a scene (GameMap: RefreshTilemapInfo(null)): the
            // map's own scene is the one, or the room would be built nameless (no boss room, no town) for good.
            if (string.IsNullOrEmpty(sceneName)) sceneName = map.gameObject.scene.name;
            var existing = map.GetComponent<TerrainSystem>();
            if (existing == null)
            {
                existing = map.gameObject.AddComponent<TerrainSystem>();
                if (!existing.Setup(map, sceneName, prebuilt, buildNow: makeCurrent))
                {
                    Destroy(existing);
                    return null;
                }
            }
            if (makeCurrent) Current = existing;
            return existing;
        }

        /// <summary>
        /// A loaded scene's tilemap, as the game finds it (GameManager.RefreshTilemapInfo: the object
        /// tagged TileMap): a root object usually, deeper in some rooms (Crossroads_11_alt, Greenpath's
        /// door from the Crossroads).
        /// </summary>
        public static tk2dTileMap SceneTileMap(UnityEngine.SceneManagement.Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            foreach (var go in roots)
                if (go.CompareTag("TileMap") && go.TryGetComponent<tk2dTileMap>(out var map)) return map;
            foreach (var go in roots)
                foreach (var map in go.GetComponentsInChildren<tk2dTileMap>(true))
                    if (map.CompareTag("TileMap")) return map;
            return null;
        }

        /// <summary>The scene's terrain outlines: the tilemap's chunk edge colliders on the terrain layer.</summary>
        public static List<EdgeCollider2D> TerrainOutlines(tk2dTileMap map)
        {
            var list = new List<EdgeCollider2D>();
            if (map.renderData == null) return list;
            foreach (var col in map.renderData.GetComponentsInChildren<EdgeCollider2D>(true))
                if (col.enabled && col.gameObject.layer == ChunkColliders.TerrainLayer && col.pointCount > 1)
                    list.Add(col);
            return list;
        }

        /// <summary>
        /// Plain terrain colliders placed by hand (roofs, walls, ledges): solid, on the terrain
        /// layer, static, and on objects without any script, so breakable walls, platforms, doors
        /// and hazards keep working the game's way.
        /// </summary>
        public static List<Collider2D> HandPlacedTerrain(tk2dTileMap map) => HandPlacedTerrain(map, null);

        static List<Collider2D> HandPlacedTerrain(tk2dTileMap map, Dictionary<string, List<string>> skipped)
        {
            var list = new List<Collider2D>();
            // The tilemap's own area, which the grid covers. Ground set down wholly outside it is kept
            // as the game made it: the off-map cages the pooled enemies wait in (Deepnest's tram rooms
            // keep their Carver Hatchers' spawners on such a floor; without it they fell for ever, dying
            // and coming back on a kill plane below, hundreds of bodies a minute).
            Vector3 tile = map.data != null ? map.data.tileSize : Vector3.one;
            var area = new Rect(map.transform.position.x, map.transform.position.y, map.width * tile.x, map.height * tile.y);
            foreach (var root in map.gameObject.scene.GetRootGameObjects())
                foreach (var col in root.GetComponentsInChildren<Collider2D>(false))
                {
                    if (!col.enabled || col.gameObject.layer != ChunkColliders.TerrainLayer) continue;
                    if (map.renderData != null && col.transform.IsChildOf(map.renderData.transform)) continue;
                    if (col.GetComponentInParent<TerraOwned>() != null) continue;
                    string why = null;
                    var rb = col.attachedRigidbody;
                    var b = col.bounds;
                    if (!area.Overlaps(Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y))) why = "在地图外";
                    // One-way platforms (effectors) stay the game's.
                    else if (col.isTrigger) why = "触发器";
                    else if (col.usedByEffector) why = "单向平台";
                    else if (col is CompositeCollider2D || col.compositeOperation != Collider2D.CompositeOperation.None) why = "复合";
                    else if (rb != null && rb.bodyType != RigidbodyType2D.Static) why = "会动";
                    else
                    {
                        var script = ScriptOn(col.transform);
                        if (script != null) why = "脚本 " + script.GetType().Name;
                    }
                    if (why == null) list.Add(col);
                    else if (skipped != null)
                    {
                        if (!skipped.TryGetValue(why, out var names)) skipped[why] = names = new List<string>();
                        names.Add(col.name);
                    }
                }
            return list;
        }

        static float Area(Bounds b) => b.size.x * b.size.y;

        /// <summary>
        /// The first script on the object or any parent that gives the collider behaviour of its
        /// own: breakable walls, platforms, lifts, doors, hazards. The terrain markers (no wall
        /// cling, no nail bounce, steep slope) are plain terrain and don't count, nor do PlayMaker's
        /// event relays, which do nothing without an FSM beside them.
        /// </summary>
        static MonoBehaviour ScriptOn(Transform t)
        {
            for (; t != null; t = t.parent)
                foreach (var mb in t.GetComponents<MonoBehaviour>())
                    if (mb != null && !(mb is NonSlider) && !(mb is NonThunker) && !(mb is SteepSlope) && !(mb is PlayMakerProxyBase) && !(mb is DustyPlatform)) return mb;
            return null;
        }

        /// <summary>The scene's origin in world space (moved for a neighbour), snapped to the grid.</summary>
        public static Vector2Int CellOffsetOf(tk2dTileMap map) =>
            new Vector2Int(Mathf.RoundToInt(map.transform.position.x / CellGrid.CellSize), Mathf.RoundToInt(map.transform.position.y / CellGrid.CellSize));

        /// <summary>A grid for a tilemap, rasterised from its colliders with the player's saved edits replayed.</summary>
        public static CellGrid BuildGrid(tk2dTileMap map, List<EdgeCollider2D> outlines, List<Collider2D> extra, string sceneName, out int filled, out int segments)
        {
            Vector3 tileSize = map.data.tileSize;
            int width = Mathf.CeilToInt(map.width * tileSize.x / CellGrid.CellSize);
            int height = Mathf.CeilToInt(map.height * tileSize.y / CellGrid.CellSize);
            var grid = new CellGrid(width, height);
            var origin = (Vector2)CellOffsetOf(map) * CellGrid.CellSize;
            filled = GridRasterizer.Fill(grid, outlines, origin, out segments);
            if (extra != null) filled += GridRasterizer.FillExtra(grid, extra, origin);
            TerrainRegion.ReplayEdits(grid, sceneName);
            return grid;
        }

        /// <summary>A boss room: the game's own colliders and tilemap stand, the grid's are off (in its fight's zone, when one was found).</summary>
        public bool SceneOwnGround { get; private set; }

        /// <summary>How the boss room's zone was found (or why the whole room is kept).</summary>
        public string OwnZoneHow { get; private set; }

        static bool Overlaps(Bounds b, Rect r) => b.max.x > r.xMin && b.min.x < r.xMax && b.max.y > r.yMin && b.min.y < r.yMax;

        /// <summary>
        /// How far outside a boss room's zone a hand-placed collider still stays whole: the walls that close the fight in
        /// stand just past its camera lock (the Pure Vessel's left Enemy Wall ends 1.3 short of it). Made cells instead, a
        /// boss pressed against one turns into its hollow outline (a pose's collider reaching back) and falls into the floor.
        /// </summary>
        const float ZoneWallReach = 2.5f;

        /// <summary>How far under a boss room's zone (its camera lock) the game's own ground is kept: its burrowers' depth (the
        /// Dung Defender tunnels about 11 under the lock's bottom).</summary>
        const float UnderArenaDepth = 12f;

        static bool NearZone(Bounds b, Rect r) =>
            Overlaps(b, new Rect(r.xMin - ZoneWallReach, r.yMin - ZoneWallReach, r.width + 2f * ZoneWallReach, r.height + 2f * ZoneWallReach));

        /// <summary>How far past a boss room's zone its floor may run on to the room's wall and the zone is taken on to it.</summary>
        const float WallSnap = 6f;

        /// <summary>
        /// A zone whose side ends over the open floor a little short of the room's wall (a camera lock narrower than the room:
        /// Godhome's Nailmasters' intro view stops 3.6 short of the arena's left wall) taken on to that wall, a unit into it.
        /// Cut on the floor, the game's floor edge ends in a free corner beside the grid's: a body sliding across the seam
        /// caught on it, as on a wall that isn't there (Mato shut in by it, dashing at it for ever; the user's, 10-25).
        /// </summary>
        static Rect ToWalls(Rect z)
        {
            int mask = 1 << ChunkColliders.TerrainLayer;
            var down = Physics2D.Raycast(z.center, Vector2.down, z.height, mask);
            if (down.collider == null) return z;
            float y = down.point.y + 0.75f, xMin = z.xMin, xMax = z.xMax;
            var l = Physics2D.Raycast(new Vector2(z.xMin + 0.5f, y), Vector2.left, WallSnap + 0.5f, mask);
            if (l.collider != null && l.point.x < z.xMin) xMin = l.point.x - 1f;
            var r = Physics2D.Raycast(new Vector2(z.xMax - 0.5f, y), Vector2.right, WallSnap + 0.5f, mask);
            if (r.collider != null && r.point.x > z.xMax) xMax = r.point.x + 1f;
            return Rect.MinMaxRect(xMin, z.yMin, xMax, z.yMax);
        }

        /// <summary>
        /// Keeps the part of one of the tilemap's outlines within the zone: new edge colliders on its object (its markers,
        /// layer and material) along the pieces inside, cut at the zone's edge (Liang-Barsky); the outline itself goes off.
        /// </summary>
        static void ClipToZone(EdgeCollider2D col, Rect zone)
        {
            var t = col.transform;
            var pts = col.points;
            var world = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) world[i] = t.TransformPoint(pts[i] + col.offset);
            var pieces = new List<List<Vector2>>();
            // Each piece's outline points just past its cut ends (none where it isn't cut): its neighbours there.
            var before = new List<Vector2?>();
            var after = new List<Vector2?>();
            List<Vector2> cur = null;
            for (int i = 0; i + 1 < world.Length; i++)
            {
                if (!ClipSegment(world[i], world[i + 1], zone, out var a, out var b))
                {
                    cur = null;
                    continue;
                }
                if (cur == null || (cur[cur.Count - 1] - a).sqrMagnitude > 1e-6f)
                {
                    cur = new List<Vector2> { a };
                    pieces.Add(cur);
                    before.Add((a - world[i]).sqrMagnitude > 1e-6f ? world[i] : i > 0 ? world[i - 1] : (Vector2?)null);
                    after.Add(null);
                }
                cur.Add(b);
                after[after.Count - 1] = (b - world[i + 1]).sqrMagnitude > 1e-6f ? world[i + 1] : i + 2 < world.Length ? world[i + 2] : (Vector2?)null;
                // Out of the zone at its end: the next piece starts afresh.
                if ((b - world[i + 1]).sqrMagnitude > 1e-6f) cur = null;
            }
            for (int p = 0; p < pieces.Count; p++)
            {
                var piece = pieces[p];
                if (piece.Count < 2) continue;
                var e = col.gameObject.AddComponent<EdgeCollider2D>();
                e.sharedMaterial = col.sharedMaterial;
                e.edgeRadius = col.edgeRadius;
                var local = new Vector2[piece.Count];
                for (int i = 0; i < piece.Count; i++) local[i] = t.InverseTransformPoint(piece[i]);
                e.points = local;
                // The outline going on past the cut as the physics' ghost vertex: a free end on the open floor is a corner a
                // body sliding onto the piece catches on, as on a wall (Godhome's Nailmasters' floor, cut by the zone at
                // 33.1: Mato shut in against it).
                if (before[p].HasValue) { e.useAdjacentStartPoint = true; e.adjacentStartPoint = t.InverseTransformPoint(before[p].Value); }
                if (after[p].HasValue) { e.useAdjacentEndPoint = true; e.adjacentEndPoint = t.InverseTransformPoint(after[p].Value); }
                DevClipped++;
            }
        }

        /// <summary>Dev: the outline pieces kept in boss rooms' zones.</summary>
        public static int DevClipped;

        static bool ClipSegment(Vector2 p0, Vector2 p1, Rect r, out Vector2 a, out Vector2 b)
        {
            float t0 = 0f, t1 = 1f;
            var d = p1 - p0;
            bool Edge(float p, float q)
            {
                if (p == 0f) return q >= 0f;
                float u = q / p;
                if (p < 0f)
                {
                    if (u > t1) return false;
                    if (u > t0) t0 = u;
                }
                else
                {
                    if (u < t0) return false;
                    if (u < t1) t1 = u;
                }
                return true;
            }
            a = b = default;
            if (!Edge(-d.x, p0.x - r.xMin) || !Edge(d.x, r.xMax - p0.x) || !Edge(-d.y, p0.y - r.yMin) || !Edge(d.y, r.yMax - p0.y)) return false;
            a = p0 + t0 * d;
            b = p0 + t1 * d;
            return (b - a).sqrMagnitude > 1e-8f;
        }

        bool Setup(tk2dTileMap map, string sceneName, CellGrid prebuilt, bool buildNow)
        {
            var sw = Stopwatch.StartNew();
            SceneName = sceneName;
            Map = map;
            if (map.renderData == null || map.data == null)
            {
                Plugin.Log.LogWarning($"[{sceneName}] tilemap has no render data, terrain left vanilla");
                return false;
            }
            var originals = TerrainOutlines(map);
            if (originals.Count == 0)
            {
                Plugin.Log.LogWarning($"[{sceneName}] no terrain edge colliders found, terrain left vanilla");
                return false;
            }
            var skipped = new Dictionary<string, List<string>>();
            var extra = HandPlacedTerrain(map, skipped);

            int filled = 0, segments = 0;
            var grid = prebuilt ?? BuildGrid(map, originals, extra, sceneName, out filled, out segments);
            long gridMs = sw.ElapsedMilliseconds;
            var root = new GameObject("TerraKnight Terrain");
            root.AddComponent<TerraOwned>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, map.gameObject.scene);
            Region = new TerrainRegion(sceneName, grid, root.transform, true, originals[0].sharedMaterial,
                HoleCutter.SceneSprites(map.gameObject.scene), buildNow);
            Region.SetOffset(CellOffsetOf(map));
            // While the hand-placed colliders still answer point queries: which cells and art are whose.
            Region.TrackHandPlaced(extra, SceneArt(map));
            // A boss room keeps the game's own ground, its colliders and its art: nothing is dug or built there
            // (World.BossRooms), and its bosses run, dash and land on the edges they were made for (the False
            // Knight stuck on a cell's corner, Hornet's dash into a wall). The grid stays, unseen and solid to
            // nothing, for what reads it (shots against the ground, the map).
            SceneOwnGround = BossRooms.IsRoom(sceneName);
            Region.SceneOwnGround = SceneOwnGround;
            try { Region.ClearInsideGamePools(map.gameObject.scene); }
            catch (System.Exception e) { Plugin.Log.LogError($"[液体] {sceneName} 清原版水池里的液体失败：{e}"); }
            // Only the part the boss fights in (BossArenaZone): there the game's outlines are kept, cut at its edge, and
            // the rest of the room is the grid's. A zone of most of the room, or none found: the whole room, as before.
            Rect? zone = null;
            // The game's own ground kept for it: the zone and the ground under it.
            Rect ground = default;
            OwnZoneHow = null;
            if (SceneOwnGround)
            {
                Vector3 tile = map.data.tileSize;
                var room = new Rect(map.transform.position.x, map.transform.position.y, map.width * tile.x, map.height * tile.y);
                zone = BossArenaZone.OfRoom(map.gameObject.scene, room, out string how);
                if (zone != null)
                {
                    var walled = ToWalls(zone.Value);
                    if (walled != zone.Value) how += $"，两边延到墙 x {walled.xMin:F1}~{walled.xMax:F1}";
                    zone = walled;
                }
                if (zone != null && zone.Value.width * zone.Value.height > 0.8f * room.width * room.height)
                {
                    how += "，占房间八成以上：整间保留";
                    zone = null;
                }
                // The ground under the arena is the boss's too: where it waits out of sight and burrows (the Dung Defender
                // starts under its floor, below the camera lock, and its burrow's dirt takes its height from where it is then;
                // the grid's cells there, solid, would push it up first and the dirt would float).
                if (zone != null)
                {
                    var z = zone.Value;
                    ground = Rect.MinMaxRect(z.xMin, Mathf.Max(room.yMin, z.yMin - UnderArenaDepth), z.xMax, z.yMax);
                }
                OwnZoneHow = how;
            }
            var kept = new List<Collider2D>();
            if (SceneOwnGround && zone == null) Region.KeepWholeRoomGround();
            else
                foreach (var col in originals)
                {
                    if (zone != null && Overlaps(col.bounds, ground)) ClipToZone(col, ground);
                    col.enabled = false;
                }
            float largest = 0f;
            foreach (var col in extra)
            {
                // Measured before switching off: a disabled collider has empty bounds.
                float area = Area(col.bounds);
                HandPlaced.Add((col.name, col.bounds));
                if (area > largest)
                {
                    largest = area;
                    FirstHandPlaced = col;
                    FirstHandPlacedBounds = col.bounds;
                }
                // In a zone's room, what reaches into the zone or closes it (a wall just past it) stays whole (and its cells unchanged).
                if (zone != null && NearZone(col.bounds, ground)) kept.Add(col);
                else if (!SceneOwnGround || zone != null) col.enabled = false;
            }
            if (zone != null) Region.SetOwnZone(zone.Value, ground, kept);
            if (skipped.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var kv in skipped)
                    sb.Append($" {kv.Key}×{kv.Value.Count}（{string.Join("、", kv.Value.GetRange(0, Mathf.Min(3, kv.Value.Count)))}）");
                Plugin.Log.LogInfo($"[{sceneName}] 没并进格子的地形碰撞体:{sb}");
            }

            // The tilemap's render meshes paint the inside of the terrain black; the grid draws that
            // fill itself so dug cells can be left open.
            int hiddenMeshes = 0;
            foreach (var mr in map.renderData.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (SceneOwnGround && zone == null) break;
                if (mr.gameObject.layer != ChunkColliders.TerrainLayer || !mr.enabled) continue;
                mr.enabled = false;
                hiddenMeshes++;
            }
            All.Add(this);
            Pylons.OnRegionReady(Region);

            Plugin.Log.LogInfo($"[{sceneName}] 地形格子化：tilemap {map.width}x{map.height} 位置 {map.transform.position}，" +
                               $"{originals.Count} 条原版轮廓{(prebuilt != null ? "（格子沿用试放时的）" : $" / {segments} 段")} + {extra.Count} 个手放碰撞体 -> {grid.Width}x{grid.Height} 格，" +
                               $"{(prebuilt != null ? "" : $"实心 {filled}，")}碰撞盒 {Region.BoxCount}，替换 {hiddenMeshes} 块地图网格，{Holes.ArtCount} 个可抠前景精灵，" +
                               $"{sw.ElapsedMilliseconds} ms（格子 {gridMs} ms）{(SceneOwnGround ? (zone != null ? $"；首领房间：战斗范围 {zone.Value.xMin:F0}~{zone.Value.xMax:F0} × {zone.Value.yMin:F0}~{zone.Value.yMax:F0}（{OwnZoneHow}）保留原版地形，其余可改动" : "；首领房间：保留原版地形碰撞和贴图（" + OwnZoneHow + "）") : "")}");
            return true;
        }

        public bool CanDig(int x, int y) => TerraWorld.CanDig(x, y);

        /// <summary>The scene's own sprites and meshes (not the tilemap's, not ours, not enemies').</summary>
        static List<Renderer> SceneArt(tk2dTileMap map)
        {
            var list = new List<Renderer>();
            foreach (var root in map.gameObject.scene.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(r is SpriteRenderer) && !(r is MeshRenderer)) continue;
                    if (map.renderData != null && r.transform.IsChildOf(map.renderData.transform)) continue;
                    if (r.GetComponentInParent<TerraOwned>() != null || r.GetComponentInParent<HealthManager>() != null) continue;
                    list.Add(r);
                }
            return list;
        }

        /// <summary>The scene is about to go (the game is changing scenes): stop counting it as part of the world.</summary>
        public void Detach()
        {
            All.Remove(this);
            if (Current == this) Current = null;
        }

        void LateUpdate()
        {
            Region?.Tick();
        }

        void OnDestroy()
        {
            Region?.Destroy();
            All.Remove(this);
            if (Current == this) Current = null;
        }
    }
}
