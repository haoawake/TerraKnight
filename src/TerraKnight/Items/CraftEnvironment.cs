using TerraKnight.World;

namespace TerraKnight.Items
{
    internal enum CraftEnvironment { None, Graveyard, Honey }

    internal static class CraftEnvironments
    {
        public static bool Available(CraftEnvironment environment)
        {
            string scene = GameManager.UnsafeInstance?.sceneName ?? "";
            switch (environment)
            {
                case CraftEnvironment.Graveyard: return scene.StartsWith("RestingGrounds");
                case CraftEnvironment.Honey: return scene.StartsWith("Hive") || Stations.Near("蜂蜜分配机", HeroController.SilentInstance);
                default: return true;
            }
        }

        public static string Description(CraftEnvironment environment) => environment == CraftEnvironment.Graveyard ? "环境：安息之地（墓地）"
            : environment == CraftEnvironment.Honey ? "环境：蜂巢，或附近放置蜂蜜分配机" : "";
    }
}
