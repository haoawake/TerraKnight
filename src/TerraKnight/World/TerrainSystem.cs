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
            foreach (var root in map.gameObject.scene.GetRootGameObjects())
                foreach (var col in root.GetComponentsInChildren<Collider2D>(false))
                {
                    if (!col.enabled || col.gameObject.layer != ChunkColliders.TerrainLayer) continue;
                    if (map.renderData != null && col.transform.IsChildOf(map.renderData.transform)) continue;
                    if (col.GetComponentInParent<TerraOwned>() != null) continue;
                    string why = null;
                    var rb = col.attachedRigidbody;
                    // One-way platforms (effectors) stay the game's.
                    if (col.isTrigger) why = "触发器";
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
            foreach (var col in originals) col.enabled = false;
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
                col.enabled = false;
            }
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
                if (mr.gameObject.layer != ChunkColliders.TerrainLayer || !mr.enabled) continue;
                mr.enabled = false;
                hiddenMeshes++;
            }
            All.Add(this);
            Pylons.OnRegionReady(Region);

            Plugin.Log.LogInfo($"[{sceneName}] 地形格子化：tilemap {map.width}x{map.height} 位置 {map.transform.position}，" +
                               $"{originals.Count} 条原版轮廓{(prebuilt != null ? "（格子沿用试放时的）" : $" / {segments} 段")} + {extra.Count} 个手放碰撞体 -> {grid.Width}x{grid.Height} 格，" +
                               $"{(prebuilt != null ? "" : $"实心 {filled}，")}碰撞盒 {Region.BoxCount}，替换 {hiddenMeshes} 块地图网格，{Holes.ArtCount} 个可抠前景精灵，" +
                               $"{sw.ElapsedMilliseconds} ms（格子 {gridMs} ms）");
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
