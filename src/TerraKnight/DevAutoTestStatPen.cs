using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "statpen"): the Amethyst Stat Viewer's weapon page (PlayerStatReport, tab 1) with a summon weapon in hand
    /// (the user: "手持召唤师武器的时候看紫晶属性仪怎么没看到5点护甲穿透"): a summon staff shows the minions' own 5
    /// (MeleeHits.MinionArmorPen) on top of the accessories', the Blade Staff 25 more, a whip only the accessories'.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator StatPenTest()
        {
            yield return new WaitForSeconds(1f);
            var inv = mod.Inventory;
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 属性仪穿透 {what} {(pass ? "通过" : "不通过")}");
            }
            var saved = inv.Slots[0];
            int sel = inv.Selected;
            int acc = Equipment.Current.ArmorPen;
            try
            {
                foreach (var (t, want) in new[] { (ItemType.FinchStaff, acc + 5), (ItemType.BladeStaff, acc + 30), (ItemType.LeatherWhip, acc) })
                {
                    inv.Slots[0] = ItemStack.Of(t);
                    inv.Selected = 0;
                    yield return null;
                    var rows = PlayerStatReport.Capture(1);
                    var pen = rows.FirstOrDefault(r => r.Label.StartsWith("护甲穿透"));
                    string all2 = string.Join("；", rows.Where(r => r.Label.Contains("穿透") || r.Label.Contains("自带")).Select(r => r.Label + " " + r.Value));
                    Check(pen.Value == want.ToString(), $"{ItemDb.Get(t).Name}：{all2}（饰品 {acc}，应为 {want}）");
                }
            }
            finally
            {
                inv.Slots[0] = saved;
                inv.Selected = sel;
                Plugin.Log.LogInfo($"[AutoTest] 属性仪穿透 结果 {ok}/{all}");
            }
        }
    }
}
