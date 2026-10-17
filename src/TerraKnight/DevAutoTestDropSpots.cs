using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_fkdrops"): where the False Knight's drops come out and come to rest. The False Knight is fought as
    /// in "fkhead" with nothing picked up; every drop is logged where it is made (the boss's body and the player beside
    /// it), and a few seconds after the fight where each lies.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FalseKnightDropsTest()
        {
            DropTrace.On = true;
            WorldItems.DevNoGrab = true;
            SpecialDeaths.DevTrace = true;
            HitTrace.On = true;
            bool beating = true;
            IEnumerator Beat()
            {
                while (beating)
                {
                    var fk = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None);
                    var sb = new System.Text.StringBuilder();
                    foreach (var hm in fk) if (hm != null && hm.name.StartsWith("False Knight")) sb.Append($" {hm.name} 血{hm.hp} 无敌{hm.IsInvincible}/{hm.InvincibleFromDirection}");
                    Plugin.Log.LogInfo($"[AutoTest] 掉落位置 心跳 时速 {Time.timeScale} 游戏时间 {Time.time:F0} 可操作 {GameHooks.PlayerHasControl()} 背包 {InventoryGui.Open} 设置 {UI.IngameOptions.Open} 暂停 {GameManager.instance.isPaused}{sb}");
                    yield return new WaitForSecondsRealtime(5f);
                }
            }
            mod.StartCoroutine(Beat());
            IEnumerator Alive()
            {
                while (beating)
                {
                    mod.Stats.FullHeal();
                    if (PlayerData.instance != null) PlayerData.instance.isInvincible = true;
                    yield return null;
                }
            }
            mod.StartCoroutine(Alive());
            int held0 = SpecialDeaths.DevHeldAtOne;
            try
            {
                yield return FalseKnightHeadTest();
                for (float t = Time.time + 6f; Time.time < t;) { PlayerData.instance.isInvincible = true; yield return null; }
                var hero = HeroController.instance;
                Plugin.Log.LogInfo($"[AutoTest] 掉落位置 打完 {GameManager.instance.sceneName}，主角 {(Vector2)hero.transform.position:F1}，地上的东西：{WorldItems.DevDescribe()}");
                FxCheck("假骑士暴怒时照样掉血、打得完", PlayerData.instance.falseKnightDefeated, $"打完 {PlayerData.instance.falseKnightDefeated}，暴怒中留一点血 {SpecialDeaths.DevHeldAtOne - held0} 次");
                Shot(GameManager.instance.sceneName, "fx_fkdrops_end");
                yield return null;
            }
            finally
            {
                beating = false;
                DropTrace.On = false;
                WorldItems.DevNoGrab = false;
                SpecialDeaths.DevTrace = false;
                HitTrace.On = false;
            }
        }
    }

    /// <summary>Dev: every WorldItems.Drop logged with the bosses and the player near it.</summary>
    [HarmonyLib.HarmonyPatch]
    internal static class DropTrace
    {
        public static bool On;

        [HarmonyLib.HarmonyPostfix]
        [HarmonyLib.HarmonyPatch(typeof(WorldItems), nameof(WorldItems.Drop))]
        static void Dropped(ItemStack stack, Vector2 centre)
        {
            if (!On) return;
            var hero = HeroController.SilentInstance;
            var sb = new System.Text.StringBuilder();
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm == null || !hm.name.StartsWith("False Knight")) continue;
                var c = hm.GetComponent<Collider2D>();
                sb.Append($" [{hm.name} 原点 {(Vector2)hm.transform.position:F1} 碰撞 {(c != null ? ((Vector2)c.bounds.center).ToString("F1") : "无")} 血 {hm.hp} 死 {hm.isDead}]");
            }
            Plugin.Log.LogInfo($"[AutoTest] 掉落位置 生成 {stack.Type}×{stack.Count} 在 {centre:F1}，主角 {(hero != null ? ((Vector2)hero.transform.position).ToString("F1") : "无")}，{sb}\n    {System.Environment.StackTrace.Split('\n')[3].Trim()} <- {System.Environment.StackTrace.Split('\n')[4].Trim()}");
        }
    }
}
