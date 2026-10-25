using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "tapshadow"): a direction tapped twice without the Dash key (AbilityAccessories.TapShadowDash, TerraMod's
    /// double tap): with the Shield of Cthulhu and the Shade Cloak worn, the shadow dash first, the Shield's dash while it
    /// cools down; the Shade Cloak alone, nothing while it cools down; the Dash key held, the double tap is left to its own way.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator TapShadowTest()
        {
            var inv = mod.Inventory;
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            void Wear(params ItemType[] items)
            {
                for (int k = 0; k < inv.Accessories.Length; k++) inv.Accessories[k] = default;
                for (int k = 0; k < items.Length; k++) inv.Accessories[k] = ItemStack.Of(items[k]);
                inv.RefreshEffective();
            }
            IEnumerator Settle()
            {
                for (float t = 0f; t < 4f && (!GameHooks.PlayerHasControl() || Player.TerraMovement.IsDashing || AbilityAccessories.ShadowDashing); t += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(0.3f);
            }
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 双击暗影冲刺 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }

            // The Shield and the Cloak: the shadow dash, then (cooling down) the Shield's.
            Wear(ItemType.ShieldOfCthulhu, ItemType.ShadeCloak);
            AbilityAccessories.DevDashKey = false;
            AbilityAccessories.DevReset();
            yield return Settle();
            int shadows0 = AbilityAccessories.DevShadowDashes, plain0 = Player.TerraMovement.DevDashes;
            bool first = AbilityAccessories.TapShadowDash(1);
            yield return null;
            bool shadowing = AbilityAccessories.ShadowDashing;
            yield return Settle();
            bool second = AbilityAccessories.TapShadowDash(-1);
            if (!second) Player.TerraMovement.RequestDash(-1);
            yield return new WaitForSeconds(0.3f);
            Check("克盾加暗影披风：第一次双击是暗影冲刺，冷却中再双击是普通冲刺",
                first && shadowing && !second && AbilityAccessories.DevShadowDashes == shadows0 + 1 && Player.TerraMovement.DevDashes > plain0,
                $"第一次 {first}（冲刺中是暗影 {shadowing}），第二次 {second}，暗影冲刺 {AbilityAccessories.DevShadowDashes - shadows0} 次，普通冲刺 {Player.TerraMovement.DevDashes - plain0} 次，冷却剩 {AbilityAccessories.ShadowCooldownLeft:F2} 秒");
            yield return Settle();

            // The Hallownest Shield (Shield, Cloak and Heart in one): the same.
            Wear(ItemType.HallownestShield);
            AbilityAccessories.DevReset();
            yield return Settle();
            shadows0 = AbilityAccessories.DevShadowDashes;
            bool hallow = AbilityAccessories.TapShadowDash(1);
            yield return Settle();
            Check("圣巢之盾：双击是暗影冲刺", hallow && AbilityAccessories.DevShadowDashes == shadows0 + 1, $"双击 {hallow}，暗影冲刺 {AbilityAccessories.DevShadowDashes - shadows0} 次");

            // The Cloak alone, cooling down: no dash of either kind.
            Wear(ItemType.ShadeCloak);
            yield return null;
            plain0 = Player.TerraMovement.DevDashes;
            shadows0 = AbilityAccessories.DevShadowDashes;
            bool cooling = AbilityAccessories.TapShadowDash(1);
            if (!cooling) Player.TerraMovement.RequestDash(1);
            yield return new WaitForSeconds(0.3f);
            Check("只有暗影披风、冷却中：双击什么都不出", !cooling && AbilityAccessories.DevShadowDashes == shadows0 && Player.TerraMovement.DevDashes == plain0,
                $"双击 {cooling}，暗影冲刺 {AbilityAccessories.DevShadowDashes - shadows0} 次，普通冲刺 {Player.TerraMovement.DevDashes - plain0} 次");

            // The Dash key held: the double tap is left to the held-Dash way (ShadowKeys), not taken here.
            Wear(ItemType.ShieldOfCthulhu, ItemType.ShadeCloak);
            AbilityAccessories.DevReset();
            AbilityAccessories.DevDashKey = true;
            yield return null;
            bool held = AbilityAccessories.TapShadowDash(1);
            AbilityAccessories.DevDashKey = false;
            Check("按住冲刺键时：这里不接双击", !held, $"双击 {held}");
            AbilityAccessories.DevDashKey = null;
            yield return Settle();
            Plugin.Log.LogInfo($"[AutoTest] 双击暗影冲刺 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
