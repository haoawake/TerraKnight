using System.Collections;
using System.Linq;
using TerraKnight.Items;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "magelist"): every magic weapon with what its remake is planned from — stage, tier, damage, use, mana, its
    /// shot and speed, channelled or not, its cast kind — one line each, by stage. Lines "[AutoTest] 法师清单 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MageListTest()
        {
            yield return null;
            var all = ItemDb.All.Where(d => d != null && ItemDb.Get(d.Type) == d && d.IsWeapon && d.Class == DamageClass.Magic)
                .OrderBy(d => d.Stage).ThenBy(d => d.Tier).ThenBy(d => d.Damage).ToList();
            foreach (var d in all)
                Plugin.Log.LogInfo($"[AutoTest] 法师清单 {d.Type}|{d.Name}|阶段{d.Stage}|档{d.Tier}|伤{d.Damage}|用{d.UseTime * 60f:0}/{d.UseAnimation * 60f:0}|魔{d.Mana}|{d.Shoot}|速{d.ShootSpeed:0.#}|{(d.Channel ? "持续" : "")}|{MageArts.Of(d).Kind}|稀{d.Rare}");
            Plugin.Log.LogInfo($"[AutoTest] 法师清单 合计 {all.Count}");
        }
    }
}
