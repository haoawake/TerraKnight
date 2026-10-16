using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TerraKnight.Items;
using TerraKnight.Town;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "reforge" and "quicksell"). Reforge: which items may have prefixes (the Nail and the Dream Nail too),
    /// a Legendary sword's and a Warding accessory's numbers, the Goblin Tinkerer's reforging (paid, always a prefix of
    /// the item's list), the item back on closing, the prefix kept by a save, the Nail reforged and swinging with it.
    /// Quick sell: a stack sold at the shop's price onto the sale row, a favourite refused, bought back for the same,
    /// its prefix kept; the row drawn.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ReforgeTest()
        {
            mod.DevNoFallDamage = true;
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 重铸 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            int stageWas = Shop.DevStage;
            Shop.DevStage = 5;
            PlayerData.instance.isInvincible = true;
            PlayerData.instance.hasDreamNail = true;
            var inv = mod.Inventory;
            ItemDef sword = null, acc = null, staff = null;
            foreach (var d in ItemDb.All)
            {
                if (d == null || !Prefixes.CanHave(d)) continue;
                var list = Prefixes.DevRollable(d);
                if (sword == null && d.IsWeapon && d.Class == DamageClass.Melee && list.Any(p => p.Id == 81) && d.Damage >= 20) sword = d;
                if (acc == null && d.IsAccessory && list.Any(p => p.Id == 65)) acc = d;
                if (staff == null && d.IsSummon && list.Any(p => p.Id == 85) && d.Damage >= 10) staff = d;
            }
            Check("找到剑、饰品、法杖", sword != null && acc != null && staff != null, $"{sword?.Name}、{acc?.Name}、{staff?.Name}");
            if (sword == null || acc == null) yield break;
            Check("骨钉梦之钉能重铸，药水和圣巢物品不能", Prefixes.CanHave(ItemDb.Get(ItemType.KnightNail)) && Prefixes.CanHave(ItemDb.Get(ItemType.DreamNail))
                && !Prefixes.CanHave(ItemDb.Get(ItemType.HealingPotion)) && !Prefixes.CanHave(ItemDb.Get(ItemType.HkKingsoul)),
                $"骨钉 {Prefixes.DevRollable(ItemDb.Get(ItemType.KnightNail)).Count} 种，梦之钉 {Prefixes.DevRollable(ItemDb.Get(ItemType.DreamNail)).Count} 种");

            // Legendary: damage ×1.15, crit +5, speed ×0.9, size ×1.1, knockback ×1.15.
            var leg = new ItemStack { Type = sword.Type, Count = 1, Prefix = 81 }.Def;
            int ticks = Mathf.RoundToInt(sword.UseAnimation * 60f);
            Check("传奇的数值", leg.Damage == (int)System.Math.Round(sword.Damage * 1.15f) && leg.Crit == sword.Crit + 5 && Mathf.Abs(leg.Scale - sword.Scale * 1.1f) < 1e-4f
                && Mathf.RoundToInt(leg.UseAnimation * 60f) == (int)System.Math.Round(ticks * 0.9f) && leg.Value > sword.Value && leg.Name.Contains(Prefixes.Name(81)),
                $"{leg.Name}：伤害 {sword.Damage}→{leg.Damage}，暴击 {sword.Crit}→{leg.Crit}，大小 {sword.Scale:F2}→{leg.Scale:F2}，用时 {ticks}→{Mathf.RoundToInt(leg.UseAnimation * 60f)}，价值 {sword.Value}→{leg.Value}，稀有度 {sword.Rare}→{leg.Rare}");
            var lines = Prefixes.TooltipLines(leg);
            Check("传奇的提示行", lines.Count >= 4 && lines.All(l => l.Good), string.Join(" | ", lines.Select(l => l.Text)));
            var bad = Prefixes.TooltipLines(new ItemStack { Type = sword.Type, Count = 1, Prefix = 39 }.Def);
            Check("破损的提示行是红的", bad.Count >= 1 && bad.All(l => !l.Good), string.Join(" | ", bad.Select(l => l.Text)));

            // Warding on: +4 defense while worn.
            var savedAcc = inv.Accessories[0];
            inv.Accessories[0] = ItemStack.Of(acc.Type);
            Equipment.Update(inv);
            int def0 = Equipment.Current.Defense;
            inv.Accessories[0].Prefix = 65;
            Equipment.Update(inv);
            int def1 = Equipment.Current.Defense;
            Check("护佑饰品 +4 防御", def1 == def0 + 4, $"{inv.Accessories[0].Def.Name}：{def0}→{def1}；{string.Join(" | ", Prefixes.TooltipLines(inv.Accessories[0].Def).Select(l => l.Text))}");
            inv.Accessories[0] = savedAcc;
            Equipment.Update(inv);
            if (staff != null)
            {
                var fab = new ItemStack { Type = staff.Type, Count = 1, Prefix = 85 }.Def;
                Check("传说法杖的穿透和标记伤害", fab.PrefixArmorPen == 10 && fab.PrefixTagDamage == 3 && fab.Damage == (int)System.Math.Round(staff.Damage * 1.15f),
                    $"{fab.Name}：{string.Join(" | ", Prefixes.TooltipLines(fab).Select(l => l.Text))}");
            }

            // The Goblin Tinkerer.
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(4f);
            var hero = HeroController.instance;
            var at = TownNpcs.DevPos("GoblinTinkerer");
            Check("哥布林在镇上", at.HasValue);
            if (!at.HasValue) yield break;
            hero.transform.position = Clear(hero, at.Value + Vector2.left * 0.8f);
            yield return null;
            TownNpcs.Close(false);
            bool talked = TownNpcs.TryRightClick(TownNpcs.DevPos("GoblinTinkerer").Value);
            Check("和哥布林说话有重铸按钮", talked && TownNpcs.TalkingIsGoblin, TownNpcs.ChatText);
            TownNpcs.OpenReforge();
            yield return null;
            Check("打开重铸", TownNpcs.ReforgeOpen && InventoryGui.Open);
            Check("不能放药水", !InventoryGui.DevPutReforge(ItemStack.Of(ItemType.HealingPotion, 3)) && TownNpcs.ReforgeItem.IsEmpty);
            inv.Mouse = default;
            Check("放剑", InventoryGui.DevPutReforge(ItemStack.Of(sword.Type)) && TownNpcs.ReforgeItem.Type == sword.Type);
            Shop.Earn(100000000);
            var seen = new HashSet<int>();
            bool paidRight = true, allValid = true;
            for (int k = 0; k < 40; k++)
            {
                long cost = Prefixes.ReforgeCost(TownNpcs.ReforgeItem);
                long purse = Shop.Purse;
                if (!InventoryGui.DevReforge()) { paidRight = false; break; }
                if (Shop.Purse != purse - cost) paidRight = false;
                int p = TownNpcs.ReforgeItem.Prefix;
                seen.Add(p);
                if (p <= 0 || !Prefixes.DevRollable(sword).Any(r => r.Id == p)) allValid = false;
            }
            Check("重铸 40 次都扣对钱、都有词缀", paidRight && allValid && seen.Count >= 12, $"出现 {seen.Count} 种：{string.Join("、", seen.Select(Prefixes.Name))}；现在 {TownNpcs.ReforgeItem.Def.Name}，花费 {Shop.Money(Prefixes.ReforgeCost(TownNpcs.ReforgeItem))}");
            InventoryGui.Open = true;
            yield return null;
            yield return null;
            Shot("Town", "reforge_panel");
            yield return null;
            int prefix = TownNpcs.ReforgeItem.Prefix;
            // Leave capacity for the returned item; overflow intentionally drops at the feet.
            inv.Slots[20]=default;inv.Slots[21]=default;
            TownNpcs.Close(false);
            int slot = System.Array.FindIndex(inv.Slots, s => s.Type == sword.Type && s.Prefix == prefix);
            Check("关掉对话剑回到背包、词缀还在", TownNpcs.ReforgeItem.IsEmpty && slot >= 0, $"第 {slot} 格 {(slot >= 0 ? inv.Slots[slot].Def.Name : "")}");

            // A save keeps it.
            if (slot >= 0)
            {
                var pack = AccessTools.Method(typeof(Save.ModSave), "Pack");
                var unpack = AccessTools.Method(typeof(Save.ModSave), "Unpack");
                var back = (ItemStack)unpack.Invoke(null, new[] { pack.Invoke(null, new object[] { inv.Slots[slot] }) });
                Check("存档保存词缀", back.Type == sword.Type && back.Prefix == prefix, $"读回 {back.Def.Name}");
            }

            // The Nail: into the slot (not given again meanwhile), reforged, back, swinging with it.
            TownNpcs.TryRightClick(TownNpcs.DevPos("GoblinTinkerer").Value);
            TownNpcs.OpenReforge();
            int nailSlot = System.Array.FindIndex(inv.Slots, s => s.Type == ItemType.KnightNail);
            Check("背包里有骨钉", nailSlot >= 0);
            if (nailSlot >= 0)
            {
                inv.Mouse = inv.Slots[nailSlot];
                inv.Slots[nailSlot] = default;
                InventoryGui.DevPutReforge(inv.Mouse);
                yield return new WaitForSeconds(2.5f);
                int nails = inv.Slots.Count(s => s.Type == ItemType.KnightNail);
                Check("骨钉在重铸栏时不会再补一把", nails == 0 && TownNpcs.ReforgeItem.Type == ItemType.KnightNail, $"背包里 {nails} 把");
                for (int k = 0; k < 60 && TownNpcs.ReforgeItem.Prefix != 81; k++) InventoryGui.DevReforge();
                var nail = TownNpcs.ReforgeItem;
                Check("骨钉重铸", nail.Prefix > 0, $"{nail.Def.Name}，伤害 {ItemDb.Get(ItemType.KnightNail).Damage}→{nail.Def.Damage}，花费 {Shop.Money(Prefixes.ReforgeCost(nail))}");
                TownNpcs.Close(false);
                yield return new WaitForSeconds(1.5f);
                int back = System.Array.FindIndex(inv.Slots, s => s.Type == ItemType.KnightNail);
                Check("骨钉回到背包只有一把", back >= 0 && inv.Slots.Count(s => s.Type == ItemType.KnightNail) == 1 && inv.Slots[back].Prefix == nail.Prefix);
                if (back >= 0 && back != 0) (inv.Slots[0], inv.Slots[back]) = (inv.Slots[back], inv.Slots[0]);
                inv.Selected = 0;inv.Override=-1;inv.UseMouse=false;inv.Mouse=default;
                InventoryGui.Open = false;
                yield return new WaitForSeconds(0.5f);
                int s0 = NailItem.DevSwings;
                NailItem.DevHold = true;
                yield return new WaitForSeconds(1f);
                NailItem.DevHold = null;
                Check("拿着重铸过的骨钉挥砍", NailItem.DevSwings > s0, $"挥了 {NailItem.DevSwings - s0} 下，刀光大小 ×{NailItem.DevSlashScale:F2}（{inv.Current.Def.Name}）");
            }
            int dream = System.Array.FindIndex(inv.Slots, s => s.Type == ItemType.DreamNail);
            if (dream >= 0)
            {
                int p = Prefixes.Roll(ItemDb.Get(ItemType.DreamNail), out _);
                Check("梦之钉能抽到通用词缀", p >= 36 && p <= 61, Prefixes.Name(p));
            }
            Shop.DevStage = stageWas;
            Plugin.Log.LogInfo($"[AutoTest] 重铸 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }

        IEnumerator QuickSellTest()
        {
            mod.DevNoFallDamage = true;
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 快捷出售 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            PlayerData.instance.isInvincible = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(3f);
            var inv = mod.Inventory;
            ItemDef sword = ItemDb.All.FirstOrDefault(d => d != null && Prefixes.CanHave(d) && d.IsWeapon && d.Value > 1000 && d.Class == DamageClass.Melee);
            Check("找到一把剑", sword != null, sword?.Name);
            if (sword == null) yield break;
            inv.Slots[20] = new ItemStack { Type = sword.Type, Count = 1, Prefix = 81 };
            inv.Slots[21] = ItemStack.Of(ItemType.Gel, 30);
            inv.Slots[22] = ItemStack.Of(ItemType.Gel, 5);
            inv.Slots[22].Favorite = true;
            long value = Shop.SellValue(inv.Slots[20]);
            long purse = Shop.Purse;
            Check("卖剑", InventoryGui.DevSell(20) && inv.Slots[20].IsEmpty && Shop.Purse == purse + value && InventoryGui.DevSold.Count > 0 && InventoryGui.DevSold[0].Price == value,
                $"{Shop.Money(value)}，钱包 {Shop.Money(purse)}→{Shop.Money(Shop.Purse)}");
            long gel = Shop.SellValue(inv.Slots[21]);
            InventoryGui.DevSell(21);
            Check("卖凝胶整组", inv.Slots[21].IsEmpty && InventoryGui.DevSold[0].Item.Count == 30, Shop.Money(gel));
            Check("收藏的不卖", !InventoryGui.DevSell(22) || !inv.Slots[22].IsEmpty, $"第 22 格 {inv.Slots[22].Count}");
            InventoryGui.Open = true;
            InventoryGui.DevToggleSell();
            yield return null;
            yield return null;
            Shot("Town", "quicksell_row");
            yield return null;
            Check("出售模式开着", InventoryGui.SellMode);
            purse = Shop.Purse;
            inv.Mouse = default;
            InventoryGui.DevBuyBack(1);
            Check("原价买回剑、词缀还在", inv.Mouse.Type == sword.Type && inv.Mouse.Prefix == 81 && Shop.Purse == purse - value, $"{inv.Mouse.Def.Name}，钱包 {Shop.Money(purse)}→{Shop.Money(Shop.Purse)}");
            inv.Pickup(inv.Mouse);
            inv.Mouse = default;
            InventoryGui.Open = false;
            yield return null;
            yield return null;
            Check("关背包退出出售模式", !InventoryGui.SellMode);
            Plugin.Log.LogInfo($"[AutoTest] 快捷出售 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
