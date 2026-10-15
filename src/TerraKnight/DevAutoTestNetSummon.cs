using System.Collections;
using TerraKnight.Items;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "netsummon"): boss seals in a LAN game, with the in-process bot (DevAutoTestNet) as the other player.
    /// The player's own seal: the summon goes out on the wire (the bot hears it, and the server's line in the chat), the
    /// player in the False Knight's arena, one seal spent. The bot's seal (Hornet): this player taken to Greenpath's
    /// arena too, nothing spent, Hornet up there.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator NetSummonTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 联机召唤 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1f);
            bool infinite = inv.Infinite;
            inv.Infinite = false;

            // 1. The player's own seal.
            int heard0 = bot.Count(Msg.BossSummon), lines0 = bot.Lines.Count;
            inv.Selected = 0;
            inv.Slots[0] = ItemStack.Of(ItemType.BossSigilFalseKnight, 3);
            Bestiary.Kills["FalseKnight"] = 1;
            Check("联机可以使用", BossSummons.Refusal(BossSummons.All[0]) == null, BossSummons.Refusal(BossSummons.All[0]) ?? "无拒绝");
            mod.DevUseSelected(hero.transform.position);
            float until = Time.time + 30f;
            while (Time.time < until && !BossArena.Arrived("FalseKnight")) yield return null;
            yield return new WaitForSeconds(3f);
            string said = bot.Lines.Count > lines0 ? bot.Lines[bot.Lines.Count - 1] : "";
            Check("召唤发给别人", bot.Count(Msg.BossSummon) > heard0 && said.Contains("召唤物"), $"机器人收到 {bot.Count(Msg.BossSummon) - heard0} 条，聊天「{said}」");
            Check("自己进了假骑士的竞技场", BossArena.Arrived("FalseKnight") && inv.Slots[0].Count == 2, $"场景 {GameManager.instance.sceneName}，召唤物剩 {inv.Slots[0].Count}");
            Shot(GameManager.instance.sceneName, "netsummon_1_own");

            // 2. The bot's seal: this player follows.
            int followed0 = BossSummons.DevFollowed, count0 = inv.Slots[0].Count;
            var p = bot.Mine(Msg.BossSummon);
            p.W.Write("Hornet");
            bot.Send(p);
            until = Time.time + 40f;
            while (Time.time < until && !(BossArena.Arrived("Hornet") && GameHooks.PlayerHasControl())) yield return null;
            yield return new WaitForSeconds(3f);
            bool hornet = false;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm != null && !hm.isDead && hm.hp > 0 && UI.EnemyHover.RecordOf(hm) == "Hornet") hornet = true;
            Check("跟着别人的召唤去了大黄蜂的竞技场", BossSummons.DevFollowed == followed0 + 1 && GameManager.instance.sceneName == "Fungus1_04" && BossArena.Arrived("Hornet"),
                $"跟随 {BossSummons.DevFollowed - followed0} 次，场景 {GameManager.instance.sceneName}，{BossSummons.DevLast}");
            Check("那边有大黄蜂、自己的召唤物没少", hornet && inv.Slots[0].Count == count0, $"大黄蜂 {hornet}，召唤物 {count0}→{inv.Slots[0].Count}");
            Shot("Fungus1_04", "netsummon_2_follow");

            inv.Infinite = infinite;
            Plugin.Log.LogInfo($"[AutoTest] 联机召唤 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
