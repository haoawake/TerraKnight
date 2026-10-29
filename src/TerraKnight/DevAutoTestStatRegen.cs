using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "statregen", the user's 10-27 "微光直接获取的永久增益似乎没生效，反正紫晶属性仪里面没看到魔力再生数字改变"): the Arcane
    /// Crystal used from the bag (as a shimmered one is) — the mana regenerated a second before and after at half a pool, the
    /// Amethyst Stat Viewer's rows for it and for the boosts used, and mana actually coming back faster over two seconds.
    /// The player's boosts and mana are put back. Lines "[AutoTest] 永久增益 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator StatRegenTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            var stats = mod.Stats;
            var inv = mod.Inventory;
            var savedBoosts = stats.Boosts.ToList();
            var savedSlot = inv.Slots[0];
            int savedSel = inv.Selected;
            stats.Boosts.Remove(PermanentBoost.ArcaneCrystal);
            // A full set of mana crystals (the crystal's share is manaMax / 50), the bag spending what is used.
            int savedCrystals = stats.ManaCrystals;
            bool savedInfinite = inv.Infinite;
            stats.ManaCrystals = Player.TerraStats.MaxManaCrystals;
            stats.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
            inv.Infinite = false;
            void Check(bool ok, string what) => Plugin.Log.LogInfo($"[AutoTest] {what} {(ok ? "通过" : "不通过")}");

            // Mana back over 1.5 s from a tenth of a pool (short of full), standing still, the wait over.
            IEnumerator Regained(System.Action<int> got)
            {
                stats.Mana = stats.ManaMax / 10;
                yield return new WaitForSeconds(3.5f);
                stats.Mana = stats.ManaMax / 10;
                yield return new WaitForSeconds(1.5f);
                got(stats.Mana - stats.ManaMax / 10);
            }

            stats.Mana = stats.ManaMax / 2;
            float before = stats.ManaRegenRate(false), beforeStill = stats.ManaRegenRate(true);
            int gainBefore = 0;
            yield return Regained(n => gainBefore = n);

            inv.Slots[0] = ItemStack.Of(ItemType.ArcaneCrystal);
            inv.Selected = 0;
            yield return null;
            mod.DevUseSelected((Vector2)hero.transform.position + Vector2.right);
            yield return new WaitForSeconds(0.5f);
            bool used = stats.Boosts.Contains(PermanentBoost.ArcaneCrystal) && inv.Slots[0].IsEmpty;
            stats.Mana = stats.ManaMax / 2;
            float after = stats.ManaRegenRate(false), afterStill = stats.ManaRegenRate(true);
            var rows = PlayerStatReport.Capture(0);
            string regenRow = rows.FirstOrDefault(r => r.Label.StartsWith("魔力再生（")).Value + " " + rows.FirstOrDefault(r => r.Label == "奥术水晶").Value;
            string boostRow = rows.FirstOrDefault(r => r.Label == "已用永久增益").Value;
            int gainAfter = 0;
            yield return Regained(n => gainAfter = n);

            Check(used, $"永久增益 奥术水晶 从背包使用：{(used ? "已记下" : "没有记下")}");
            Check(after > before && afterStill > beforeStill, $"永久增益 魔力再生（最大魔力 {stats.ManaMax}，半满）：移动 {before:0.##} → {after:0.##}，站立 {beforeStill:0.##} → {afterStill:0.##} 每秒");
            Check(regenRow != null && regenRow.Contains("回魔每秒多") && boostRow != null && boostRow.Contains("奥术水晶"), $"永久增益 属性仪：魔力再生「{regenRow}」，已用永久增益「{boostRow}」");
            Check(gainAfter > gainBefore, $"永久增益 1.5 秒回魔：{gainBefore} → {gainAfter}");

            stats.Boosts.Clear();
            foreach (var b in savedBoosts) stats.Boosts.Add(b);
            inv.Slots[0] = savedSlot;
            inv.Selected = savedSel;
            inv.Infinite = savedInfinite;
            stats.ManaCrystals = savedCrystals;
            stats.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
            stats.Mana = stats.ManaMax;
        }
    }
}
