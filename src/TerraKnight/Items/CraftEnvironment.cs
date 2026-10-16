using TerraKnight.World;

namespace TerraKnight.Items
{
    internal enum CraftEnvironment { None, Graveyard, Honey, Water, Lava }

    internal static class CraftEnvironments
    {
        public static bool Available(CraftEnvironment environment)
        {
            string scene = GameManager.UnsafeInstance?.sceneName ?? "";
            switch (environment)
            {
                // The Resting Grounds, or seven tombstones about (Items.Tombstones: SceneMetrics' graveyard).
                case CraftEnvironment.Graveyard: return scene.StartsWith("RestingGrounds") || Tombstones.InGraveyard;
                case CraftEnvironment.Honey: return scene.StartsWith("Hive") || Stations.Near("蜂蜜分配机", HeroController.SilentInstance);
                case CraftEnvironment.Water: return Fishing.NearCraftingLiquid(false);
                case CraftEnvironment.Lava: return Fishing.NearCraftingLiquid(true);
                default: return true;
            }
        }

        public static string Description(CraftEnvironment environment) => environment == CraftEnvironment.Graveyard ? "环境：墓地（安息之地，或附近有 7 块墓碑）"
            : environment == CraftEnvironment.Honey ? "环境：蜂巢，或附近放置蜂蜜分配机"
            : environment == CraftEnvironment.Water ? "环境：附近有水"
            : environment == CraftEnvironment.Lava ? "环境：附近有酸液（熔岩）" : "";
    }
}
