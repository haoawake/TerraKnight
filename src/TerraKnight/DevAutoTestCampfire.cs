using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "campfire"): one Campfire alone in the bag (below the hotbar): its Cozy Fire on, and the life it gives back
    /// over six seconds against the same six seconds without it.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CampfireTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 篝火 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", EntryGateOf("Town"));
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            var inv = mod.Inventory;
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            var saved = (ItemStack[])inv.Slots.Clone();
            for (int i = Inventory.HotbarSize; i < Inventory.Size; i++) inv.Slots[i] = default;
            yield return new WaitForSeconds(0.6f);

            // Six seconds from half life, standing still: the life given back.
            IEnumerator Regen(System.Action<int> got)
            {
                var stats = mod.Stats;
                stats.Life = stats.LifeMax / 2;
                stats.DevResetRegen();
                int from = stats.Life;
                yield return new WaitForSeconds(6f);
                got(stats.Life - from);
            }
            int without = 0, with = 0;
            yield return Regen(n => without = n);
            int regenOff = Items.Equipment.Current.LifeRegen;

            inv.Slots[25] = ItemStack.Of(ItemType.Campfire);
            yield return new WaitForSeconds(0.6f);
            bool buff = Player.PlayerBuffs.Has(Player.PlayerBuffs.CozyFire);
            int regenOn = Items.Equipment.Current.LifeRegen;
            yield return Regen(n => with = n);
            Check("背包里的篝火给温馨之火", buff && CarriedFurniture.Campfire, $"增益 {buff}，认到篝火 {CarriedFurniture.Campfire}，生命再生 {regenOff}→{regenOn}");
            Check("6 秒回血比没有篝火时多", with > without, $"没有篝火 {without}，有篝火 {with}");
            Shot("Town", "campfire_bag");
            yield return null;

            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = saved[i];
            inv.Infinite = infinite;
            mod.Stats.FullHeal();
            yield return new WaitForSeconds(0.3f);
            Plugin.Log.LogInfo($"[AutoTest] 篝火 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
