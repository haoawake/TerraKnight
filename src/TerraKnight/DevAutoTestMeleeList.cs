using System.Collections;
using System.Linq;
using TerraKnight.Items;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "meleelist"): every melee item whose swing itself hits (not a tool, a spear, a yoyo, a flail, a whip, a
    /// thrust or a shortsword's stab), with its stage, rarity, damage, use time, size, effect and shot. Lines "[AutoTest] 真近战 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MeleeListTest()
        {
            yield return null;
            var all = ItemDb.All.Where(d => d != null && d.IsWeapon && d.Class == DamageClass.Melee).ToList();
            int n = 0;
            foreach (var d in all.OrderBy(d => d.Stage).ThenBy(d => d.Rare).ThenBy(d => d.Damage))
            {
                bool swing = !d.NoMelee && !d.Thrust && !d.HoldOut && !d.IsWhip && d.PickPower == 0 && d.AxePower == 0 && d.HammerPower == 0;
                if (!swing) continue;
                n++;
                var s = TerraMod.ItemSprite(d.Type);
                Plugin.Log.LogInfo($"[AutoTest] 真近战 {d.Type} {d.Name} 阶段{d.Stage} 稀有{d.Rare} 伤害{d.Damage} 用时{d.UseAnimation * 60f:F0}/{d.UseTime * 60f:F0} 大小{(s != null ? s.rect.width + "x" + s.rect.height : "?")}×{d.Scale:F2} 特效{d.Fx} 射{d.Shoot} 举起{d.HoldUp} 无图{d.NoUseGraphic} 声{d.UseSound}");
            }
            foreach (var d in all.Where(d => d.PickPower > 0 || d.AxePower > 0 || d.HammerPower > 0).Where(d => !d.NoMelee))
                Plugin.Log.LogInfo($"[AutoTest] 近战工具 {d.Type} {d.Name} 阶段{d.Stage} 伤害{d.Damage} 镐{d.PickPower} 斧{d.AxePower} 锤{d.HammerPower}");
            Plugin.Log.LogInfo($"[AutoTest] 真近战 合计 {n}");
        }
    }
}
