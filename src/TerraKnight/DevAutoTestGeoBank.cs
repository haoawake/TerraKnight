using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "geobank"): the Piggy Bank's money as the game's own geo: the counter shows the
    /// bag's and the bank's together, a toll the game takes comes out of the bag and then the bank,
    /// and a death's shade carries only the bag's while the bank keeps its own.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GeoBankTest(HeroController hero)
        {
            var inv = mod.Inventory;
            var pd = PlayerData.instance;
            inv.Infinite = false;
            for (int i = 0; i < inv.Slots.Length; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == ItemType.Geo) inv.Slots[i] = default;
            for (int i = 0; i < inv.Bank.Length; i++) inv.Bank[i] = default;
            inv.Pickup(ItemType.Geo, 30);
            inv.Bank[0] = ItemStack.Of(ItemType.Geo, 100);
            yield return null;
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 存钱罐 开始：背包 {GeoWallet.Held(inv)}，存钱罐 {GeoWallet.Banked(inv)}，原版吉欧 {pd.geo}（应为 130） {(pd.geo == 130 ? "通过" : "不通过")}");
            // A toll of 80, as the game takes one (HeroController.TakeGeo).
            hero.TakeGeo(80);
            yield return null;
            yield return null;
            bool tollOk = GeoWallet.Held(inv) == 0 && GeoWallet.Banked(inv) == 50 && pd.geo == 50;
            Plugin.Log.LogInfo($"[AutoTest] 存钱罐 原版收 80：背包 {GeoWallet.Held(inv)}，存钱罐 {GeoWallet.Banked(inv)}，原版吉欧 {pd.geo}（应为 0、50、50） {(tollOk ? "通过" : "不通过")}");
            // A death with 40 in the bag.
            inv.Pickup(ItemType.Geo, 40);
            yield return null;
            yield return null;
            pd.isInvincible = false;
            hero.cState.invulnerable = false;
            mod.Stats.FullHeal();
            hero.TakeDamage(null, GlobalEnums.CollisionSide.right, 999, 1);
            float wait = Time.realtimeSinceStartup + 40f;
            while (!hero.cState.dead && Time.realtimeSinceStartup < wait) yield return null;
            while ((hero = HeroController.SilentInstance) == null || hero.cState.dead || !GameHooks.PlayerHasControl()) { if (Time.realtimeSinceStartup > wait + 20f) break; yield return null; }
            yield return new WaitForSeconds(1f);
            pd = PlayerData.instance;
            bool deathOk = GeoWallet.Held(inv) == 0 && GeoWallet.Banked(inv) == 50 && pd.geoPool == 40 && pd.geo == 50;
            Plugin.Log.LogInfo($"[AutoTest] 存钱罐 死后：背包 {GeoWallet.Held(inv)}，存钱罐 {GeoWallet.Banked(inv)}，亡魂 {pd.geoPool}，原版吉欧 {pd.geo}（应为 0、50、40、50） {(deathOk ? "通过" : "不通过")}");
            pd.isInvincible = true;
        }
    }
}
