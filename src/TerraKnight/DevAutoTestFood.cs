using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "food"): Terraria's food. Each food made here has its recipe; eaten, it gives its fed state (Well Fed,
    /// Plenty Satisfied, Exquisitely Stuffed) in place of any other, with that state's bonuses; the Ale makes the player
    /// Tipsy; the Shucked Oyster is food. The Angler's quests are Terraria's 41 fish.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FoodTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 食物 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            Vector2 aim = (Vector2)hero.transform.position + Vector2.right * 2f;
            var foods = new[] { ItemType.CookedFish, ItemType.CookedShrimp, ItemType.Sashimi, ItemType.SeafoodDinner, ItemType.LobsterTail, ItemType.BlackenedFish,
                ItemType.GoldenDelight, ItemType.GrubSoup, ItemType.Escargot, ItemType.MonsterLasagna, ItemType.PumpkinPie };
            var missing = foods.Where(f => !Recipes.All.Any(r => r.Result == f)).ToList();
            Check("每种都能做", missing.Count == 0, missing.Count == 0 ? $"{foods.Length} 种，配方 {Recipes.All.Count(r => foods.Contains(r.Result))} 条" : "缺 " + string.Join(",", missing));
            var dinner = Recipes.All.FirstOrDefault(r => r.Result == ItemType.SeafoodDinner);
            Check("海鲜大餐在烹饪锅用两条鱼", dinner != null && dinner.Station == "烹饪锅" && dinner.Ingredients[0].Count == 2, dinner != null ? $"{dinner.Station} {dinner.Ingredients[0].Type}×{dinner.Ingredients[0].Count}" : "无");

            foreach (var b in new[] { PotionBuffs.WellFed, PotionBuffs.PlentySatisfied, PotionBuffs.ExquisitelyStuffed, PotionBuffs.Tipsy }) PlayerBuffs.Remove(b);
            yield return null;
            var none = PotionBuffs.Bonuses();
            IEnumerator Eat(ItemType t)
            {
                mod.DevUse(t, aim);
                yield return new WaitForSeconds(0.6f);
            }
            string Fed() => $"吃得好 {PlayerBuffs.Has(PotionBuffs.WellFed)} 很满意 {PlayerBuffs.Has(PotionBuffs.PlentySatisfied)} 酒足饭饱 {PlayerBuffs.Has(PotionBuffs.ExquisitelyStuffed)}";

            yield return Eat(ItemType.CookedFish);
            var wf = PotionBuffs.Bonuses();
            Check("熟鱼：吃得好", PlayerBuffs.Has(PotionBuffs.WellFed) && wf.Defense - none.Defense == 2 && Mathf.Abs(wf.MoveSpeed - none.MoveSpeed - 0.2f) < 0.001f
                && Mathf.Abs(wf.MeleeDamage - none.MeleeDamage - 0.05f) < 0.001f && PlayerBuffs.TimeLeft(PotionBuffs.WellFed) > 8 * 3600 - 120,
                $"{Fed()}，防御 +{wf.Defense - none.Defense}，移速 +{wf.MoveSpeed - none.MoveSpeed:F2}，伤害 +{wf.MeleeDamage - none.MeleeDamage:F3}，剩 {PlayerBuffs.TimeLeft(PotionBuffs.WellFed) / 60} 秒");
            yield return Eat(ItemType.SeafoodDinner);
            var ps = PotionBuffs.Bonuses();
            Check("海鲜大餐：很满意，换掉吃得好", PlayerBuffs.Has(PotionBuffs.PlentySatisfied) && !PlayerBuffs.Has(PotionBuffs.WellFed) && ps.Defense - none.Defense == 3,
                $"{Fed()}，防御 +{ps.Defense - none.Defense}");
            yield return Eat(ItemType.GoldenDelight);
            var es = PotionBuffs.Bonuses();
            Check("金美味：酒足饭饱", PlayerBuffs.Has(PotionBuffs.ExquisitelyStuffed) && !PlayerBuffs.Has(PotionBuffs.PlentySatisfied) && es.Defense - none.Defense == 4
                && Mathf.Abs(es.PickSpeed - none.PickSpeed + 0.15f) < 0.001f, $"{Fed()}，防御 +{es.Defense - none.Defense}，挖矿 {es.PickSpeed - none.PickSpeed:F2}");
            yield return Eat(ItemType.CookedFish);
            Check("再吃熟鱼：低一档的也换上", PlayerBuffs.Has(PotionBuffs.WellFed) && !PlayerBuffs.Has(PotionBuffs.ExquisitelyStuffed), Fed());
            yield return Eat(ItemType.ShuckedOyster);
            Check("去壳牡蛎能吃", PlayerBuffs.Has(PotionBuffs.WellFed) && PlayerBuffs.TimeLeft(PotionBuffs.WellFed) > 9 * 3600, $"剩 {PlayerBuffs.TimeLeft(PotionBuffs.ExquisitelyStuffed) / 60} 秒");
            var before = PotionBuffs.Bonuses();
            yield return Eat(ItemType.Ale);
            var tipsy = PotionBuffs.Bonuses();
            Check("麦芽酒：醉醺醺", PlayerBuffs.Has(PotionBuffs.Tipsy) && tipsy.Defense - before.Defense == -4 && Mathf.Abs(tipsy.MeleeDamage - before.MeleeDamage - 0.1f) < 0.001f,
                $"防御 {tipsy.Defense - before.Defense}，近战 +{tipsy.MeleeDamage - before.MeleeDamage:F2}");
            foreach (var b in new[] { PotionBuffs.WellFed, PotionBuffs.PlentySatisfied, PotionBuffs.ExquisitelyStuffed, PotionBuffs.Tipsy }) PlayerBuffs.Remove(b);

            var quests = Fishing.QuestList(5).ToList();
            bool regional = QuestFishCatalog.All.All(q => quests.Contains(q.Fish));
            Check("渔夫任务36条地区鱼", quests.Count == 36 && regional, $"{quests.Count} 种，全部地区鱼 {regional}");
            Plugin.Log.LogInfo($"[AutoTest] 食物 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }

        static string FishingQuestTip(ItemType t) => Fishing.QuestTooltip(t);
    }
}
