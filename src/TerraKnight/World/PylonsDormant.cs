using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// A bench's pylon stands by it from the first time its scene is in the world, before the bench is rested on: half clear,
    /// giving no light, not used, mined or on the map, but its place taken all the same (no block, chest, piece of furniture
    /// or prefab house goes there: <see cref="Occupies"/>), so nothing the player builds round a bench can come where its pylon
    /// will light up. The first rest on the bench lights it where it stands. Its place is kept in the save (PylonSave.Dormant).
    /// </summary>
    internal static partial class Pylons
    {
        /// <summary>The benches' pylons not lit yet, each at its place.</summary>
        public static readonly List<Pylon> Dormant = new List<Pylon>();
        public static int DevDormantMade, DevDormantLit;

        public static Pylon FindDormant(string scene, string bench)
        {
            foreach (var p in Dormant) if (p.Scene == scene && p.Bench == bench) return p;
            return null;
        }

        /// <summary>A scene's grid came into the world: every bench of it without a pylon gets its dormant one (placed once), standing.</summary>
        static void ReadyDormant(TerrainRegion region)
        {
            var origin = (Vector2)region.CellOffset * CellGrid.CellSize;
            foreach (var b in Benches)
            {
                if (b.Scene != region.SceneName || Find(b.Scene, b.Bench) != null || Taken(b.Scene, b.Bench) || FindDormant(b.Scene, b.Bench) != null) continue;
                var bench = BenchTransform(b.Scene, b.Bench);
                Vector2 at = bench != null ? (Vector2)bench.position : origin + new Vector2(b.X, b.Y);
                if (!PlaceBeside(region, at, out var cell, out _)) continue;
                var p = new Pylon
                {
                    Scene = b.Scene,
                    Bench = b.Bench,
                    Local = CellGrid.CellCenter(cell.x, cell.y) - Vector2.one * (CellGrid.CellSize / 2f) - origin,
                    SceneSize = new Vector2(region.Grid.Width, region.Grid.Height) * CellGrid.CellSize,
                    Style = StyleFor(b.Scene),
                    Dormant = true,
                };
                Dormant.Add(p);
                DevDormantMade++;
                Plugin.Log.LogInfo($"[晶塔] {b.Scene} 的 {b.Bench} 旁立起未激活的{p.Name}（{p.Local}），坐一下长椅点亮");
            }
            foreach (var p in Dormant)
                if (p.Scene == region.SceneName) Spawn(p, region);
        }

        static float recheckAt;
        public static int DevDormantMoved;

        /// <summary>
        /// Once a second in play: a dormant pylon of the scene that something has come to stand on since its place was
        /// chosen (the Dirtmouth altars are set down after the scene's grid is ready) moves to the nearest free place by
        /// its bench. A lit one stays.
        /// </summary>
        static void RecheckDormant()
        {
            if (Time.unscaledTime < recheckAt) return;
            recheckAt = Time.unscaledTime + 1f;
            var live = TerraWorld.Live;
            if (live == null) return;
            var origin = (Vector2)live.CellOffset * CellGrid.CellSize;
            foreach (var p in Dormant)
            {
                if (p.Scene != live.SceneName) continue;
                var box = new Rect(origin + p.Local, new Vector2(WidthCells, HeightCells) * CellGrid.CellSize);
                var inner = new Rect(box.x + 0.02f, box.y + 0.02f, box.width - 0.04f, box.height - 0.04f);
                bool blocked = Stations.Overlaps(box);
                foreach (var c in Chests.Boxes()) if (c.Overlaps(inner)) blocked = true;
                if (!blocked) continue;
                PylonObject obj = null;
                foreach (var o in spawned) if (o != null && o.Pylon == p) obj = o;
                // Its own picture out of the way while a place is looked for.
                if (obj != null) obj.gameObject.SetActive(false);
                var bench = BenchTransform(p.Scene, p.Bench);
                var cell = default(Vector2Int);
                bool moved = bench != null && PlaceBeside(live, bench.position, out cell, out _);
                if (moved)
                {
                    p.Local = CellGrid.CellCenter(cell.x, cell.y) - Vector2.one * (CellGrid.CellSize / 2f) - origin;
                    if (obj != null) obj.transform.localPosition = new Vector3(p.Local.x, p.Local.y, 0f);
                    DevDormantMoved++;
                    Plugin.Log.LogInfo($"[晶塔] {p.Scene} 的未激活{p.Name}被家具或箱子占了位置，挪到 {p.Local}");
                }
                if (obj != null) obj.gameObject.SetActive(true);
            }
        }

        /// <summary>The first rest on its bench: the dormant pylon lit where it stands. False when the bench has none.</summary>
        static bool LightDormant(string scene, string bench, TerrainRegion live)
        {
            var p = FindDormant(scene, bench);
            if (p == null) return false;
            Dormant.Remove(p);
            p.Dormant = false;
            Unlocked.Add(p);
            PylonObject obj = null;
            foreach (var o in spawned) if (o != null && o.Pylon == p) obj = o;
            if (obj == null && live != null) obj = Spawn(p, live);
            if (obj != null) obj.Appear();
            DevDormantLit++;
            return true;
        }

        /// <summary>A pylon (lit or not) stands in part of this box (world units; touching its sides doesn't count).</summary>
        public static bool Occupies(Rect box)
        {
            var inner = new Rect(box.x + 0.02f, box.y + 0.02f, box.width - 0.04f, box.height - 0.04f);
            foreach (var o in spawned)
                if (o != null && o.isActiveAndEnabled && o.Bounds.Overlaps(inner)) return true;
            return false;
        }

        /// <summary>A pylon (lit or not) stands on this cell.</summary>
        public static bool OccupiesCell(Vector2Int cell)
        {
            var c = CellGrid.CellCenter(cell.x, cell.y);
            foreach (var o in spawned)
                if (o != null && o.isActiveAndEnabled && o.Bounds.Contains(c)) return true;
            return false;
        }

        /// <summary>A pylon (lit or not) within this many units of the box.</summary>
        public static bool Near(Rect box, float margin)
        {
            var grown = new Rect(box.x - margin, box.y - margin, box.width + margin * 2f, box.height + margin * 2f);
            foreach (var o in spawned)
                if (o != null && o.isActiveAndEnabled && o.Bounds.Overlaps(grown)) return true;
            return false;
        }
    }
}
