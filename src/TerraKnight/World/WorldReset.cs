namespace TerraKnight.World
{
    /// <summary>
    /// Every save's world is its own (the user's 10-26 "为什么其他存档建造的方块会留到新的存档里面？每个存档的建筑都应该是新的"):
    /// before a new game starts and before a save is read, everything built or changed in the last world goes. The blocks, the
    /// walls (a new game left them: a house built in one save stood in the next), the torches' styles, the liquids, the broken
    /// pots; the live rooms' grids back to the ground they were made from (they keep the edit tables they were given, so those
    /// are emptied in place first); the rooms kept loaded; and the player's own world set aside while he was a guest in another's
    /// (Net.WorldSync), which a save would otherwise write instead of the world he is in.
    /// </summary>
    internal static class WorldReset
    {
        public static int DevResets;

        public static void All()
        {
            Net.WorldSync.DropPersonal();
            foreach (var t in TerrainSystem.All)
            {
                var grid = t?.Region?.Grid;
                if (grid == null) continue;
                grid.ResetNetworkEdits();
                grid.ResetWalls();
                grid.ResetTorchStyles();
            }
            WorldEdits.ClearInPlace();
            WorldWalls.ClearInPlace();
            RegionalTorches.ClearInPlace();
            WorldEdits.Clear();
            WorldWalls.Clear();
            RegionalTorches.Clear();
            WorldLiquids.Clear();
            Items.BreakableDrops.Load(null);
            LiveWorld.Instance?.ClearAll();
            DevResets++;
        }
    }
}
