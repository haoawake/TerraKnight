using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "meleecheck"): the save suite meleesave made, loaded again (the run's save folder seeded with it): where
    /// the Knight stands, the world's name, the true melee weapons in the bag, the piggy bank and the void bag, and a
    /// swing of the first one drawing its cuts. Lines "[AutoTest] 近战存档检查 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MeleeCheckTest()
        {
            yield return new WaitForSeconds(2f);
            var inv = mod.Inventory;
            int bag = inv.Slots.Take(Inventory.Size).Count(s => !s.IsEmpty && BladeArts.Is(s.Def));
            int bank = inv.BankOf(0).Count(s => !s.IsEmpty && BladeArts.Is(s.Def));
            int voidBag = inv.BankOf(3).Count(s => !s.IsEmpty && BladeArts.Is(s.Def));
            var hero = HeroController.instance;
            Plugin.Log.LogInfo($"[AutoTest] 近战存档检查 场景 {GameManager.instance.sceneName} {(Vector2)hero.transform.position:F1}，世界名 {Save.ModSave.WorldName}，背包 {bag} 把、存钱罐 {bank}、虚空袋 {voidBag}，第 49/50 格 {inv.Slots[Inventory.Size - 2].Def?.Name}/{inv.Slots[Inventory.Size - 1].Def?.Name}");
            int cuts0 = SwordFx.DevArtCuts;
            inv.Selected = 9;
            yield return new WaitForSeconds(0.3f);
            var aim = (Vector2)hero.transform.position + Vector2.right * 3f;
            mod.DevUseSelected(aim);
            for (float t = Time.time + 3f; mod.IsUsingItem && Time.time < t;) yield return null;
            yield return new WaitForSeconds(0.3f);
            Shot(GameManager.instance.sceneName, "meleecheck");
            bool ok = bag == 48 && bank + voidBag == 46 && SwordFx.DevArtCuts > cuts0 && Save.ModSave.WorldName == "真近战测试";
            Plugin.Log.LogInfo($"[AutoTest] 近战存档检查 {inv.Slots[9].Def?.Name} 挥了 {SwordFx.DevArtCuts - cuts0} 段 {(ok ? "通过" : "不通过")}");
        }
    }
}
