using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight.World
{
    internal static class RestorationGun
    {
        internal const int RadiusCells = 2;
        internal static int DevCells, DevWalls, DevFurniture, DevProtected, DevUses;
        static readonly Dictionary<(string, int, int), float> requested = new Dictionary<(string, int, int), float>();

        internal static Rect ContainerProtection(Rect box) => new Rect(
            box.xMin + .001f, box.yMin - CellGrid.CellSize + .001f,
            box.width - .002f, box.height + CellGrid.CellSize - .002f);

        internal static bool Protected(Rect box, List<Rect> protection)
        {
            foreach (var p in protection) if (p.Overlaps(box)) return true;
            return false;
        }

        internal static bool Touches(Rect box, Vector2 at)
        {
            float dx = Mathf.Max(box.xMin - at.x, 0f, at.x - box.xMax);
            float dy = Mathf.Max(box.yMin - at.y, 0f, at.y - box.yMax);
            return dx * dx + dy * dy <= RadiusCells * RadiusCells * CellGrid.CellSize * CellGrid.CellSize;
        }

        internal static void Spray(Vector2 at)
        {
            if (!WorldSync.CanInteract) return;
            var cell = CellGrid.WorldToCell(at);
            var r = TerraWorld.RegionAt(cell.x, cell.y);
            if (r == null) return;
            if (WorldSync.Guest)
            {
                var key = (r.SceneName, cell.x, cell.y);
                if (requested.TryGetValue(key, out float until) && Time.unscaledTime < until) return;
                if (requested.Count > 512) requested.Clear();
                requested[key] = Time.unscaledTime + .5f;
                // Pylons belong to the local travel/save state, unlike shared terrain and furniture.
                var protection = new List<Rect>();
                Chests.RestorationProtection(protection); Stations.RestorationProtection(protection);
                Pylons.RestorationRemove(at, protection);
                // Only the host changes shared objects and awards drops. Projectile visuals stay local.
                WorldSync.Submit(new WorldSync.Action { Op = "restoreTerrain", Scene = r.SceneName,
                    X = cell.x - r.CellOffset.x, Y = cell.y - r.CellOffset.y });
                return;
            }
            Apply(CellGrid.CellCenter(cell.x, cell.y));
        }

        internal static bool NetApply(WorldSync.Action a, int slot)
        {
            if (float.IsNaN(a.X) || float.IsNaN(a.Y) || float.IsInfinity(a.X) || float.IsInfinity(a.Y)) return false;
            var r = TerrainSystem.Find(a.Scene);
            if (r == null || a.X < 0 || a.Y < 0 || a.X >= r.Grid.Width || a.Y >= r.Grid.Height) return false;
            var at = CellGrid.CellCenter(r.CellOffset.x + Mathf.RoundToInt(a.X), r.CellOffset.y + Mathf.RoundToInt(a.Y));
            if (slot != NetSession.MySlot)
            {
                var player = NetSession.Player(slot);
                if (player == null || !player.HasState || (player.Latest.Scene != a.Scene
                    && !player.Loaded.Contains(a.Scene) && !player.In.Contains(a.Scene))) return false;
            }
            Apply(at, slot == NetSession.MySlot);
            return true;
        }

        internal static void Apply(Vector2 at, bool personalObjects = true)
        {
            DevUses++;
            var protection = new List<Rect>();
            Chests.RestorationProtection(protection);
            Stations.RestorationProtection(protection);
            var drops = new List<(ItemStack Stack, Vector2 At)>();
            Stations.RestorationRemove(at, protection, drops);
            LifeCrystals.RestorationRemove(at, protection);
            if (personalObjects) Pylons.RestorationRemove(at, protection);
            var cell = CellGrid.WorldToCell(at);
            var refresh = new HashSet<TerrainRegion>();
            for (int y = cell.y - RadiusCells; y <= cell.y + RadiusCells; y++)
            for (int x = cell.x - RadiusCells; x <= cell.x + RadiusCells; x++)
            {
                if ((x - cell.x) * (x - cell.x) + (y - cell.y) * (y - cell.y) > RadiusCells * RadiusCells) continue;
                var r = TerraWorld.RegionAt(x, y);
                if (r == null) continue;
                var g = r.Grid;
                int lx = x - r.CellOffset.x, ly = y - r.CellOffset.y, i = ly * g.Width + lx;
                if (!g.OriginalAt(i, out var original)) continue;
                var box = new Rect(x * CellGrid.CellSize + .002f, y * CellGrid.CellSize + .002f,
                    CellGrid.CellSize - .004f, CellGrid.CellSize - .004f);
                if (Protected(box, protection)) { DevProtected++; continue; }
                var kind = g.Get(lx, ly);
                int wall = g.GetWall(lx, ly);
                bool changed = kind != original || g.IsDug(lx, ly) || g.GetSlope(lx, ly) != 0 || wall >= 0 || g.GetLiquid(lx, ly) > 0;
                if (!changed) continue;
                // Native terrain is restored, never harvested; a second spray cannot mint another block.
                if (kind != CellKind.Empty && kind != CellKind.Original && kind != CellKind.MagicIce)
                {
                    var item = AreaBlocks.DropFor(kind, new Vector2Int(x, y));
                    if (item != ItemType.None) drops.Add((ItemStack.Of(item), CellGrid.CellCenter(x, y)));
                }
                if (wall > 0)
                {
                    foreach (var d in ItemDb.All)
                        if (d != null && d.PlacesWall == wall) { drops.Add((ItemStack.Of(d.Type), CellGrid.CellCenter(x, y))); break; }
                    DevWalls++;
                }
                if (g.GetLiquid(lx, ly) > 0) r.Liquid.Set(lx, ly, 0, LiquidKind.Water);
                g.SetWall(lx, ly, -1);
                g.RestoreOriginal(lx, ly);
                DevCells++;
                refresh.Add(r);
            }
            foreach (var r in refresh) r.RestoreScenery();
            // Keep stacks at their hit positions; world-item merging uses the existing save/network path.
            foreach (var d in drops) WorldItems.Drop(d.Stack, d.At);
        }
    }
}
