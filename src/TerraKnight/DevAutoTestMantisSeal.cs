using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "mantisseal"): a player's report — the Mantis Lords' seal sets the player outside, falling into the void.
    /// With the Mantis Lords beaten already (defeatedMantisLords: their arena's floor opened to Deepnest), the seal used
    /// from Dirtmouth: the Knight's place and scene every half second for twelve seconds, the room's floor under him.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MantisSealTest()
        {
            mod.DevNoFallDamage = true;
            Player.TerraStats.DevImmortal = true;
            var pd = PlayerData.instance;
            bool was = pd.defeatedMantisLords;
            try
            {
                yield return SealTravel("Town", "left1");
                pd.defeatedMantisLords = true;
                var inv = mod.Inventory;
                var seal = BossSummons.All.First(x => x.Key == "MantisLord");
                Bestiary.Kills["MantisLord"] = Mathf.Max(1, Bestiary.KillsOf("MantisLord"));
                Shop.DevStage = seal.Stage;
                inv.Slots[0] = ItemStack.Of(seal.Type, 3);
                inv.Selected = 0;
                inv.Override = -1;
                for (float t = Time.realtimeSinceStartup + 15f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) yield return null;
                mod.DevUseSelected(HeroController.instance.transform.position);
                var trace = new System.Text.StringBuilder();
                for (float t = Time.realtimeSinceStartup + 14f, next = 0f; Time.realtimeSinceStartup < t;)
                {
                    mod.Stats.FullHeal();
                    if (Time.realtimeSinceStartup >= next)
                    {
                        next = Time.realtimeSinceStartup + 0.5f;
                        var h = HeroController.SilentInstance;
                        if (h != null)
                        {
                            var c = World.CellGrid.WorldToCell(h.transform.position);
                            var region = World.TerraWorld.RegionAt(c.x, c.y);
                            trace.Append($"\n    {GameManager.instance.sceneName} {(Vector2)h.transform.position:F1} 在房间里 {region?.SceneName ?? "否"} 着地 {h.cState.onGround} 可操作 {GameHooks.PlayerHasControl()}");
                        }
                    }
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 三螳螂召唤 打赢过之后用召唤物：{BossSummonStart.DevLast}{trace}");
                var hero = HeroController.instance;
                var cell = World.CellGrid.WorldToCell(hero.transform.position);
                bool inside = World.TerraWorld.RegionAt(cell.x, cell.y) != null && GameManager.instance.sceneName == "Fungus2_15";
                Plugin.Log.LogInfo($"[AutoTest] 三螳螂召唤 结果：{GameManager.instance.sceneName} {(Vector2)hero.transform.position:F1} {(inside ? "通过" : "不通过")}");
            }
            finally
            {
                pd.defeatedMantisLords = was;
                Player.TerraStats.DevImmortal = false;
                Shop.DevStage = -1;
            }
        }
    }
}
