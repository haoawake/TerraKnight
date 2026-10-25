using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's block swap (Player.PlaceThing_TryReplacingTiles / PlaceThing_TryReplacingWalls, WorldGen.ReplaceTile /
    /// ReplaceWall), on while the builder toggle "方块替换" is (builderAccStatus[10], on unless switched off; UI.BuilderToggles).
    /// A block, platform or torch used on a cell that holds another kind: the best pickaxe carried mines it there, a blow at
    /// the pickaxe's speed (TerraMod.Mine: its power against the block, the Hallownest ground's tiers, the dust and sounds),
    /// and the blow that breaks it drops it and sets the held one in its place, the use taking the pickaxe's time and the
    /// block's. A wall used on a wall of another kind the player set: the old one dropped, the new one there at once.
    /// Not a rope; not what drops the held item itself (an area's block on its own ground); not a platform into a block
    /// over the player; not sand holding sand up without a 110% pickaxe; nowhere a block couldn't go (boss rooms, under
    /// furniture, a chest or a pylon).
    /// </summary>
    internal static class BlockSwap
    {
        public static int DevSwapped, DevWallsSwapped, DevBlows;

        /// <summary>The best pickaxe in the inventory (Player.GetBestPickaxe: the 50 slots, the highest pick power), null if none.</summary>
        public static ItemDef BestPickaxe(Inventory inv)
        {
            ItemDef best = null;
            if (inv == null) return null;
            for (int i = 0; i < Inventory.Size; i++)
            {
                var s = inv.Slots[i];
                if (s.IsEmpty || s.Def.PickPower <= 0) continue;
                if (best == null || s.Def.PickPower > best.PickPower) best = s.Def;
            }
            return best;
        }

        /// <summary>The pick power a cell's block needs (the Hallownest ground its area's tier, meteorite 50, obsidian 55).</summary>
        static int PowerNeeded(CellKind kind, Vector2Int cell)
        {
            if (kind == CellKind.Original)
            {
                var region = TerraWorld.RegionAt(cell.x, cell.y);
                return Areas.PickRequired(Areas.TierOf(region?.SceneName));
            }
            return VanillaBlocks.PickRequired(kind);
        }

        /// <summary>Whether the held block, platform or torch swaps the one on this cell (PlaceThing_ValidTileForReplacement).</summary>
        public static bool CanSwap(ItemDef def, Vector2Int cell)
        {
            if (def == null || def.Places == CellKind.Empty || !UI.BuilderToggles.BlockSwapOn) return false;
            var mod = TerraMod.Instance;
            if (mod == null) return false;
            int x = cell.x, y = cell.y;
            var kind = TerraWorld.Get(x, y);
            if (kind == CellKind.Empty || kind == CellKind.Rope || def.Places == CellKind.Rope) return false;
            // The same block again: nothing to swap (a torch of another style is).
            bool torches = kind == CellKind.Torch && def.Places == CellKind.Torch;
            if (kind == def.Places && !(torches && RegionalTorches.ItemAt(cell) != def.Type)) return false;
            // A torch goes for a torch, a block or a platform for a block or a platform (WouldTileReplacementWork).
            if ((kind == CellKind.Torch) != (def.Places == CellKind.Torch)) return false;
            // KillTile_GetItemDrops == HeldItem.type: what it would drop is what is held.
            if (!torches && AreaBlocks.DropFor(kind, cell) == def.Type) return false;
            if (!TerraWorld.CanDig(x, y)) return false;
            if (Stations.Covers(cell) || Chests.Covers(cell) || Pylons.OccupiesCell(cell)) return false;
            var pick = BestPickaxe(mod.Inventory);
            if (pick == null || pick.PickPower < PowerNeeded(kind, cell)) return false;
            // PlaceThing_CheckPlatformSolidityForBlockSwap: a platform made solid round the player would shut him in.
            if (Cells.IsPlatform(kind) && !Cells.IsPlatform(def.Places))
            {
                var hero = HeroController.SilentInstance;
                if (hero != null)
                {
                    var b = hero.GetComponent<BoxCollider2D>().bounds;
                    var c = CellGrid.CellCenter(x, y);
                    var tile = new Rect(c.x - CellGrid.CellSize / 2f, c.y - CellGrid.CellSize / 2f, CellGrid.CellSize, CellGrid.CellSize);
                    if (tile.Overlaps(new Rect(b.min, b.size))) return false;
                }
            }
            // PlaceThing_CheckSpecificValidtyCaseForBlockSwap: sand that holds sand up stays, short of a 110% pickaxe.
            if (VanillaBlocks.IsSand(kind) && !VanillaBlocks.IsSand(def.Places) && VanillaBlocks.IsSand(TerraWorld.Get(x, y + 1)) && pick.PickPower < 110) return false;
            return true;
        }

        /// <summary>Whether the held wall swaps the one on this cell (PlaceThing_ValidWallForReplacement): a wall the player set, of another kind.</summary>
        public static bool CanSwapWall(ItemDef def, Vector2Int cell)
        {
            if (def == null || def.PlacesWall <= 0 || !UI.BuilderToggles.BlockSwapOn) return false;
            int wall = TerraWorld.GetWall(cell.x, cell.y);
            return wall > 0 && wall != def.PlacesWall && BuildingTools.CanHammerWall(cell);
        }

        /// <summary>
        /// A use of a block on a cell it swaps: one blow of the best pickaxe (its speed the use's). Broken, the held one goes
        /// down there (true: the caller sets it as it sets a placed one).
        /// </summary>
        public static bool Blow(TerraMod mod, ItemDef def, Vector2Int cell, ref float useCooldown)
        {
            var pick = BestPickaxe(mod.Inventory);
            if (pick == null) return false;
            // ApplyItemTime(bestPickaxe, pickSpeed) while it holds; pickaxe time + block time when it breaks.
            useCooldown = pick.UseTime * Equipment.UseScale(pick) * Equipment.PickTime;
            DevBlows++;
            mod.Mine(pick, cell);
            if (TerraWorld.Get(cell.x, cell.y) != CellKind.Empty) return false;
            useCooldown += def.UseTime / (1f + Equipment.Current.TileSpeed + Equipment.Current.BlockSpeed);
            DevSwapped++;
            Achievements.AchievementTracker.BlockSwapped();
            return true;
        }

        /// <summary>The held wall over the one there: the old one dropped as its item (WorldGen.ReplaceWall's KillWall_DropItems).</summary>
        public static void SwapWall(TerraMod mod, ItemDef def, Vector2Int cell)
        {
            int old = TerraWorld.GetWall(cell.x, cell.y);
            if (old > 0 && !mod.Inventory.Infinite)
                foreach (var item in ItemDb.All)
                    if (item != null && item.PlacesWall == old) { WorldItems.Drop(ItemStack.Of(item.Type), CellGrid.CellCenter(cell.x, cell.y)); break; }
            TerraMod.MineDust(cell, CellKind.Stone, 10);
            DevWallsSwapped++;
            Achievements.AchievementTracker.BlockSwapped();
        }
    }
}
