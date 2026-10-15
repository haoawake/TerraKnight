using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "mana"): 1.4.5's mana. The Mana Flower drinks a potion from the bag for a held spell (the Last Prism,
    /// paying as it goes) once the mana runs short; without mana a staff is still cast, 2.5 times slower (slowMagicUse);
    /// the Last Prism held without any mana goes on, slower.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ManaTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 魔力 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1f);
            var hero = HeroController.instance;
            hero.transform.position = new Vector2(110f, 12f);
            yield return new WaitForSeconds(0.8f);
            var inv = mod.Inventory;
            var stats = mod.Stats;
            var savedAcc = (ItemStack[])inv.Accessories.Clone();
            // Potions really drunk (a Creative save's are never used up).
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            int slot = 2, potSlot = Inventory.HotbarSize + 3;
            var aim = (Vector2)hero.transform.position + new Vector2(6f, 0.5f);
            mod.DevAim = aim;

            // 1. The Mana Flower and the Last Prism: out of mana, a potion from the bag.
            Wear(ItemType.ManaFlower);
            inv.Slots[potSlot] = ItemStack.Of(ItemType.ManaPotion, 5);
            inv.Slots[slot] = ItemStack.Of(ItemType.LastPrism, 1);
            inv.Selected = slot;
            yield return null;
            // Spent, as a cast spends it (its regeneration waits).
            stats.SpendMana(stats.Mana);
            int checks0 = TerraMod.DevManaChecks, tries0 = TerraMod.DevFlowerTries;
            mod.DevHold = true;
            int lowest = 999;
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                if (mod.DevReady) mod.DevUseSelected(aim);
                lowest = Mathf.Min(lowest, stats.Mana);
                yield return null;
            }
            mod.DevHold = false;
            yield return new WaitForSeconds(0.5f);
            int left = inv.Slots[potSlot].IsEmpty ? 0 : inv.Slots[potSlot].Count;
            Check("魔力花给持续施法喝药", left < 5, $"4 秒里喝了 {5 - left} 瓶魔力药水，最低魔力 {lowest}，检查魔力 {TerraMod.DevManaChecks - checks0} 次，" +
                  $"魔力花尝试 {TerraMod.DevFlowerTries - tries0} 次，魔力花生效 {Equipment.Current.ManaFlower}，光棱每次 {Equipment.ManaCost(ItemDb.Get(ItemType.LastPrism))} 点，" +
                  $"最后一次快捷使用：{TerraMod.DevLastQuickUse}，无限物品 {inv.Infinite}");

            // 2. No flower, no potions, no mana: the staff still casts, 2.5 times slower.
            Wear();
            inv.Slots[potSlot] = default;
            inv.Slots[slot] = ItemStack.Of(ItemType.WaterBolt, 1);
            inv.Selected = slot;
            yield return new WaitForSeconds(0.5f);
            int fullUses = 0, emptyUses = 0, slow0 = TerraMod.DevSlowCasts;
            mod.DevHold = true;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                stats.Mana = stats.ManaMax;
                if (mod.DevReady) { mod.DevUseSelected(aim); fullUses++; }
                yield return null;
            }
            yield return new WaitForSeconds(0.6f);
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                stats.Mana = 0;
                if (mod.DevReady) { mod.DevUseSelected(aim); emptyUses++; }
                yield return null;
            }
            mod.DevHold = false;
            int slowCasts = TerraMod.DevSlowCasts - slow0;
            float ratio = emptyUses / (float)Mathf.Max(1, fullUses);
            Check("没蓝也能施法，慢 2.5 倍", emptyUses > 0 && slowCasts > 0 && ratio > 0.3f && ratio < 0.52f,
                  $"满蓝 3 秒 {fullUses} 次，没蓝 3 秒 {emptyUses} 次（{ratio:P0}，应约 40%），缺魔施法 {slowCasts} 次");
            yield return new WaitForSeconds(0.6f);

            // 3. The Last Prism held without mana: on, slower.
            inv.Slots[slot] = ItemStack.Of(ItemType.LastPrism, 1);
            inv.Selected = slot;
            yield return null;
            mod.DevHold = true;
            bool alive = true;
            int held = 0;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                stats.Mana = 0;
                if (mod.DevReady) mod.DevUseSelected(aim);
                if (t > 1f) { held++; if (TerraProjectiles.Instance != null && !TerraProjectiles.Instance.Holds(ProjKind.LastPrism)) alive = false; }
                yield return null;
            }
            mod.DevHold = false;
            Check("没蓝按住光棱不断", alive && mod.SlowMagicUse, $"光棱一直在 {alive}，缺魔状态 {mod.SlowMagicUse}");
            Shot("Town", "mana_prism_empty");
            yield return null;

            for (int i = 0; i < savedAcc.Length; i++) inv.Accessories[i] = savedAcc[i];
            inv.Infinite = infinite;
            inv.Slots[slot] = default;
            mod.DevAim = null;
            stats.Mana = stats.ManaMax;
            Plugin.Log.LogInfo($"[AutoTest] 魔力 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
