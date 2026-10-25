using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Town;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "townall"): every townsperson at the last stage in Dirtmouth (the Angler woken; the Traveling Merchant
    /// only on his days, Santa only in December), their words free of name tags, the Nurse's healing and the Tax
    /// Collector's taxes, and the street drawn.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator TownAllTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 城镇居民 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            int stageWas = Shop.DevStage;
            Shop.DevStage = 5;
            Angler.Saved = true;
            PlayerData.instance.isInvincible = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(4f);
            bool xmas = System.DateTime.Now.Month == 12 && System.DateTime.Now.Day >= 15;
            // The townsfolk with nothing to sell or do wait in the housing panel (TownNpcsManage) until put down by hand.
            int waited = 0, early = 0;
            foreach (var d in TownNpcs.AllDefs)
            {
                if (TownNpcs.AutoArrives(d) || d.Kind == NpcKind.Santa || d.Kind == NpcKind.Angler) continue;
                waited++;
                if (TownNpcs.Present(d.Internal)) early++;
                else TownNpcs.DevPlace(d.Internal);
            }
            Check("只卖东西、有用处的居民自己入住，其余等玩家安置", waited > 0 && early == 0, $"等安置 {waited} 位，自己来了 {early} 位");
            // The Princess is evaluated on the next three-second town reconciliation,
            // after the last manually placed resident (e.g. Golfer/Painter) is present.
            yield return new WaitForSeconds(3.5f);
            string who = TownNpcs.DevDescribe();
            int count = TownNpcs.Count;
            Check("都住进来了", count >= 37, $"{count} 位：{who}");
            foreach (var id in new[] { "Nurse", "Dryad", "Wizard", "Mechanic", "Truffle", "TaxCollector", "DD2Bartender", "Golfer", "Princess", "TownCat", "TownSlimeBlue", "TownSlimeRainbow", "Angler" })
                Check("在 " + id, TownNpcs.DevPos(id).HasValue);
            Check("圣诞老人只在十二月", TownNpcs.DevPos("SantaClaus").HasValue == xmas, $"十二月 {xmas}");
            var h0 = HeroController.instance;
            foreach (var id in new[] { "TownCat", "TownSlimeBlue", "Princess" })
            {
                var at = TownNpcs.DevPos(id);
                if (!at.HasValue) continue;
                h0.transform.position = Clear(h0, at.Value + Vector2.left * 2f);
                yield return new WaitForSeconds(1.2f);
                Shot("Town", "town_" + id);
                yield return null;
            }

            var hero = HeroController.instance;
            string Talk(string id)
            {
                var at = TownNpcs.DevPos(id);
                if (!at.HasValue) return null;
                hero.transform.position = Clear(hero, at.Value + Vector2.left * 0.8f);
                TownNpcs.Close(false);
                return TownNpcs.TryRightClick(TownNpcs.DevPos(id).Value) ? TownNpcs.ChatText : null;
            }
            foreach (var id in new[] { "Nurse", "Wizard", "Princess", "TownCat", "TownSlimeBlue", "DD2Bartender" })
            {
                string text = Talk(id);
                yield return null;
                Check("对话 " + id, !string.IsNullOrEmpty(text) && !text.Contains("{"), text);
            }
            // The Nurse.
            var stats = mod.Stats;
            stats.Life = Mathf.Max(1, stats.LifeMax - 40);
            Talk("Nurse");
            long price = TownNpcs.NursePrice();
            Shop.Earn(1000000);
            long purse = Shop.Purse;
            TownNpcs.NurseHeal();
            Check("护士治疗", price > 0 && stats.Life == stats.LifeMax && Shop.Purse == purse - price, $"价 {Shop.Money(price)}，{TownNpcs.ChatText}");
            // The Tax Collector.
            Talk("TaxCollector");
            TownNpcs.Taxes = 5000;
            purse = Shop.Purse;
            TownNpcs.TaxCollect();
            Check("税收官领税", Shop.Purse == purse + 5000 && TownNpcs.Taxes == 0, TownNpcs.ChatText);
            TownNpcs.Close(false);
            Shop.DevStage = stageWas;
            Plugin.Log.LogInfo($"[AutoTest] 城镇居民 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
