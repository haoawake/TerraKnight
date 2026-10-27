using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "lockjumps", the user's 10-26 "四锁的饰品锁还是开放多段跳吧，只禁翅膀就好了"): the Pantheon's accessory binding
    /// on (PlayerChallenges.DevBindings), the Bundle of Balloons, the Cloud in a Bottle, Angel Wings and Tabi worn: the extra
    /// jumps and the dash kept, the wings, the balloons' jump boost and their defense off. Lines "[AutoTest] 饰品锁 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator LockJumpsTest()
        {
            yield return GoTo("Town", "left1");
            var inv = mod.Inventory;
            for (int a = 0; a < inv.Accessories.Length; a++) inv.Accessories[a] = default;
            inv.Accessories[0] = ItemStack.Of(ItemType.BundleofBalloons);
            inv.Accessories[1] = ItemStack.Of(ItemType.CloudinaBottle);
            inv.Accessories[2] = ItemStack.Of(ItemType.AngelWings);
            inv.Accessories[3] = ItemStack.Of(ItemType.Tabi);
            inv.RefreshEffective();
            string Line()
            {
                var b = Equipment.Current;
                return $"云 {Player.TerraMovement.JumpCloud} 沙暴 {Player.TerraMovement.JumpSandstorm} 暴雪 {Player.TerraMovement.JumpBlizzard} " +
                       $"二段跳 {Player.PlayerHooks.Abilities.DoubleJump} 冲刺 {Player.TerraMovement.DashKind} 翅膀 {b.WingSlot} 跳高 {b.JumpBoost}";
            }
            yield return new WaitForSeconds(0.5f);
            string free = Line();
            PlayerChallenges.DevBindings = BossSequenceController.ChallengeBindings.Charms;
            yield return new WaitForSeconds(0.5f);
            string locked = Line();
            var e = Equipment.Current;
            bool ok = Player.TerraMovement.JumpCloud && Player.TerraMovement.JumpSandstorm && Player.TerraMovement.JumpBlizzard
                      && Player.PlayerHooks.Abilities.DoubleJump && Player.TerraMovement.DashKind == 1 && e.WingSlot == 0 && !e.JumpBoost;
            PlayerChallenges.DevBindings = null;
            for (int a = 0; a < inv.Accessories.Length; a++) inv.Accessories[a] = default;
            inv.RefreshEffective();
            Plugin.Log.LogInfo($"[AutoTest] 饰品锁 不锁：{free}；锁上：{locked} {(ok ? "通过" : "不通过")}");
        }
    }
}
