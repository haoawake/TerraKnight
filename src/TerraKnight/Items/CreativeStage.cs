namespace TerraKnight.Items
{
    /// <summary>
    /// The stage a creative save plays at (the user's, 2026-10-20: "创造模式要能够自己更改当前阶段"): chosen in the inventory
    /// (InventoryGui.DrawCreativeStage), kept in the save (TerraMod.GrantedAbilities, "creative.stage.N"); none chosen, all
    /// of them (World.Areas.MaxLevel), as before. Shop.UnlockedStage reads it while items are free.
    /// </summary>
    internal static class CreativeStage
    {
        const string Prefix = "creative.stage.";

        /// <summary>The stage chosen, or -1 for none.</summary>
        public static int Chosen
        {
            get
            {
                var mod = TerraMod.Instance;
                if (mod == null) return -1;
                for (int s = 0; s <= World.Areas.MaxLevel; s++)
                    if (mod.GrantedAbilities.Contains(Prefix + s)) return s;
                return -1;
            }
        }

        public static void Set(int stage)
        {
            var mod = TerraMod.Instance;
            if (mod == null || stage < 0 || stage > World.Areas.MaxLevel) return;
            for (int s = 0; s <= World.Areas.MaxLevel; s++) mod.GrantedAbilities.Remove(Prefix + s);
            mod.GrantedAbilities.Add(Prefix + stage);
            Plugin.Log.LogInfo($"[创造模式] 当前阶段改为 {stage}");
        }
    }
}
