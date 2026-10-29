using System.Collections;
using System.Linq;
using TerraKnight.Items;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "rangedlist"): every ranged weapon with how it is held and used (held out, held up, channelled, no use graphic,
    /// a swing), the ammo it takes, its stage, damage, use time, auto-reuse, sprite size, hold offset, shot, sound and tooltip.
    /// Lines "[AutoTest] 射手武器 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator RangedListTest()
        {
            yield return null;
            var all = ItemDb.All.Where(d => d != null && ItemDb.Get(d.Type) == d && d.IsWeapon && d.Class == DamageClass.Ranged).ToList();
            foreach (var d in all.OrderBy(d => d.Stage).ThenBy(d => d.Rare).ThenBy(d => d.Damage))
            {
                var s = TerraMod.ItemSprite(d.Type);
                string how = d.HoldOut ? "前举" : d.HoldUp ? "举起" : d.Thrust ? "刺" : d.NoUseGraphic ? "无图" : "挥";
                Plugin.Log.LogInfo($"[AutoTest] 射手武器 {d.Type}|{d.Name}|{how}|弹药{d.UseAmmo}|引导{d.Channel}|连发{Equipment.AutoReuses(d)}|阶段{d.Stage}|伤害{d.Damage}|用时{d.UseAnimation * 60f:F0}/{d.UseTime * 60f:F0}|大小{(s != null ? s.rect.width + "x" + s.rect.height : "?")}|握{d.HoldOffset}|射{d.Shoot}|声{d.UseSound}|{(d.Tooltip ?? "").Replace("\n", " / ")}");
            }
            Plugin.Log.LogInfo($"[AutoTest] 射手武器 合计 {all.Count}");
        }
    }
}
