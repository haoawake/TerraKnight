using System.Collections;
using System.Text;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "showcase"): a new creative save's showcase kit (Items/ShowcaseKit): each loadout's
    /// armour and accessories and the bag, listed, the bag open on each loadout for a picture; then
    /// ammo held in the hand (Explosive Jack 'O Lanterns) fires nothing, the launcher fires at its pace.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static string Names(ItemStack[] stacks)
        {
            var sb = new StringBuilder();
            foreach (var s in stacks) sb.Append(s.IsEmpty ? "空" : s.Def.Name).Append('、');
            return sb.ToString().TrimEnd('、');
        }

        IEnumerator ShowcaseTest(HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            ShowcaseKit.Give(mod);
            var inv = mod.Inventory;
            yield return new WaitForSeconds(0.5f);
            string[] kits = { "近战", "远程", "魔法", "召唤" };
            for (int l = 0; l < Inventory.LoadoutCount; l++)
            {
                inv.SwitchLoadout(l);
                yield return new WaitForSeconds(0.3f);
                Plugin.Log.LogInfo($"[AutoTest] 展示 {kits[l]}配装：盔甲 {Names(inv.Armor)}；饰品 {Names(inv.Accessories)}；防御 {mod.Stats.Defense}");
                InventoryGui.Open = true;
                yield return new WaitForSeconds(0.4f);
                Shot(GameManager.instance.sceneName, $"showcase_{l + 1}");
                yield return null;
                InventoryGui.Open = false;
            }
            inv.SwitchLoadout(0);
            var rows = new StringBuilder();
            for (int r = 0; r < Inventory.Rows; r++)
            {
                var row = new ItemStack[Inventory.Columns];
                System.Array.Copy(inv.Slots, r * Inventory.Columns, row, 0, Inventory.Columns);
                rows.Append($"\n    第{r + 1}行：{Names(row)}");
            }
            Plugin.Log.LogInfo($"[AutoTest] 展示 背包：生命 {mod.Stats.LifeMax} 魔力 {mod.Stats.ManaMax}，饰品格 {Inventory.OpenAccessories}，缺 {ShowcaseKit.DevMissing} 件{rows}");

            // Ammo in the hand isn't a weapon of its own.
            // A click goes through IsUsable before anything is used: plain ammo never passes.
            inv.Slots[9] = ItemStack.Of(ItemType.ExplosiveJackOLantern, 999);
            inv.Selected = 9;
            yield return null;
            bool ammoUsable = inv.Current.Def.IsUsable;
            inv.Slots[9] = ItemStack.Of(ItemType.JackOLanternLauncher);
            inv.Slots[Inventory.Size - 5] = ItemStack.Of(ItemType.ExplosiveJackOLantern, 999);
            yield return null;
            int before = TerraProjectiles.DevMade(ProjKind.JackOLanternProj);
            var aim = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center + Vector2.right * 6f;
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aim);
                yield return null;
            }
            int launcher = TerraProjectiles.DevMade(ProjKind.JackOLanternProj) - before;
            Plugin.Log.LogInfo($"[AutoTest] 展示 弹药：爆炸杰克南瓜灯能直接用 {ammoUsable}（应为 False），杰克南瓜灯发射器连按 1 秒发出 {launcher} 个（用时 25 帧，约 2–3 个）");
            inv.Slots[9] = ItemStack.Of(ItemType.LaserDrill);
            inv.Slots[Inventory.Size - 5] = default;
            inv.Selected = 0;
        }
    }
}
