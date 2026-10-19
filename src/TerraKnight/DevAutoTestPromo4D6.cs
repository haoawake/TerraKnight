using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (trailer 4.0, recorder D): the inventory's systems. The four buttons at the bag's bottom right hovered (their
    /// names); an item put on the loot blacklist; the Hallownest bag's 29 slots; the Potion Box (pages, a 20-potion stack
    /// favourited: its buff for good); the Banner Case; the crafting window's tabs and "全部"; then creative: a stack
    /// showing ∞ taken from with right clicks (it stays ∞), and the window making anything for nothing.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static Rect D4Utility(int i) => InventoryGui.UtilityButtonRect(UI.GameView.Rect, UI.GameView.Scale, i);

        int D4EmptyBag(int from = 10)
        {
            var inv = mod.Inventory;
            for (int i = from; i < Inventory.Size; i++) if (inv.Slots[i].IsEmpty) return i;
            return -1;
        }

        /// <summary>The hero put by the lamps right of Dirtmouth's bench (the area's title off).</summary>
        IEnumerator D4ToTown(HeroController hero)
        {
            yield return EnterRoom("Town");
            hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            D4NoAreaTitle();
            var col = hero.GetComponent<BoxCollider2D>();
            var street = TownNpcs.DevStreet;
            float bottom = col.bounds.min.y - hero.transform.position.y;
            hero.transform.position = Clear(hero, new Vector2(street.x + 7f, street.y - bottom + 0.3f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(2f);
        }

        /// <summary>An ordinary (not creative) save's bag: the four buttons, the blacklist, the Potion Box, the Banner Case.</summary>
        IEnumerator P4_Dinv(HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            // Terraria's fall damage would take an ordinary save's 100 life on the drop into Dirtmouth.
            mod.DevNoFallDamage = true;
            Player.TerraStats.DevImmortal = true;
            yield return EnterRoom("Town");
            hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            D4NoAreaTitle();
            var col = hero.GetComponent<BoxCollider2D>();
            var street = TownNpcs.DevStreet;
            float bottom = col.bounds.min.y - hero.transform.position.y;
            hero.transform.position = Clear(hero, new Vector2(street.x + 7f, street.y - bottom + 0.3f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            var inv = mod.Inventory;
            mod.Stats.Life = mod.Stats.LifeMax;
            // The bag: a stack to blacklist, the Potion Box and the Banner Case, each in a slot of its own.
            int gelSlot = D4EmptyBag(20);
            if (gelSlot >= 0) inv.Slots[gelSlot] = ItemStack.Of(ItemType.Gel, 87);
            int boxSlot = D4EmptyBag(20);
            if (boxSlot >= 0) inv.Slots[boxSlot] = ItemStack.Of(ItemType.PotionBox);
            int caseSlot = D4EmptyBag(20);
            if (caseSlot >= 0) inv.Slots[caseSlot] = ItemStack.Of(ItemType.BannerCase);
            inv.DropBlacklist.Clear();
            // The box's first page full of potions (Ironskin twenty, to favourite); the case's of banners.
            foreach (var page in inv.PotionBox) for (int i = 0; i < page.Length; i++) page[i] = default;
            foreach (var page in inv.BannerCase) for (int i = 0; i < page.Length; i++) page[i] = default;
            var potions = new List<ItemType>();
            var banners = new List<ItemType>();
            foreach (ItemType t in System.Enum.GetValues(typeof(ItemType)))
            {
                var d = ItemDb.Get(t);
                if (d == null || string.IsNullOrEmpty(d.Texture)) continue;
                if (t != ItemType.IronskinPotion && Containers.IsPotion(d) && potions.Count < 60) potions.Add(t);
                if (t.ToString().StartsWith("Banner") && t != ItemType.BannerCase && banners.Count < 70) banners.Add(t);
            }
            inv.PotionBox[0][0] = ItemStack.Of(ItemType.IronskinPotion, 20);
            var rng = new System.Random(4);
            for (int i = 0; i < potions.Count && i < 79; i++)
            {
                int pg = (i + 1) / Inventory.ContainerPageSlots, at = (i + 1) % Inventory.ContainerPageSlots;
                if (i > 52) break;
                inv.PotionBox[pg][at] = ItemStack.Of(potions[i], 2 + rng.Next(14));
            }
            for (int i = 0; i < banners.Count && i < 70; i++)
                inv.BannerCase[i / Inventory.ContainerPageSlots][i % Inventory.ContainerPageSlots] = ItemStack.Of(banners[i], 1 + rng.Next(3));
            Player.PlayerBuffs.Remove(5);
            yield return new WaitForSeconds(2f);
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 背包：凝胶 {gelSlot}，药水箱 {boxSlot}，旗帜盒 {caseSlot}；药水 {potions.Count} 种，旗帜 {banners.Count} 种");

            // --- The four buttons.
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(1.5f, 1.5f)));
            yield return D4Rec("inv_buttons");
            yield return new WaitForSeconds(0.3f);
            InventoryGui.Open = true;
            mod.PlaySound("Menu_Open", 0.7f);
            yield return new WaitForSeconds(0.5f);
            for (int i = 0; i < 4; i++)
            {
                yield return Glide(D4Utility(i).center, i == 0 ? 0.7f : 0.4f);
                yield return new WaitForSeconds(0.9f);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 背包：按钮 {i} 提示 {InventoryGui.DevTooltip.Replace("\n", " | ")}");
            }
            StopRec();

            // --- The loot blacklist: the Gel onto it (the stack stays), its entry hovered.
            yield return D4Rec("inv_blacklist");
            yield return new WaitForSeconds(0.2f);
            yield return ClickAt(D4Utility(0), 0.4f);
            yield return new WaitForSeconds(0.7f);
            if (gelSlot >= 0)
            {
                yield return ClickHit("Slot", gelSlot, 0.6f);
                yield return new WaitForSeconds(0.3f);
                yield return ClickHit("BlacklistEntry", 0, 0.6f);
                yield return new WaitForSeconds(0.5f);
                yield return ClickHit("Slot", gelSlot, 0.6f);
                yield return new WaitForSeconds(0.3f);
                var e = InventoryGui.DevHitRect("BlacklistEntry", 0);
                if (e != null) yield return Glide(e.Value.center, 0.5f);
                yield return new WaitForSeconds(1.4f);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 背包：黑名单 {string.Join(",", inv.DropBlacklist)}，提示 {InventoryGui.DevTooltip.Replace("\n", " | ")}");
            }
            StopRec();
            // Its panel shut (one panel at a time; the Potion Box's opens in the same place).
            if (InventoryGui.DevBlacklistOpen) InventoryGui.DevUtilityClick(0);
            yield return null;

            // --- The Potion Box: opened, a page turned, the Ironskin favourited (twenty of it: its buff for good).
            if (boxSlot >= 0)
            {
                yield return D4Rec("inv_potionbox");
                yield return new WaitForSeconds(0.2f);
                var r = InventoryGui.DevHitRect("Slot", boxSlot);
                if (r != null) yield return Glide(r.Value.center, 0.6f);
                yield return new WaitForSeconds(0.35f);
                InventoryGui.DevRightClick(boxSlot);
                yield return new WaitForSeconds(0.8f);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 背包：药水箱开 {Chests.ContainerOpen}，{Chests.ContainerPage + 1}/{Chests.ContainerPages}");
                yield return ClickHit("ChestButton", 8, 0.6f);
                yield return new WaitForSeconds(0.8f);
                yield return ClickHit("ChestButton", 7, 0.4f);
                yield return new WaitForSeconds(0.6f);
                var c0 = InventoryGui.DevHitRect("ChestSlot", 0);
                if (c0 != null) yield return Glide(c0.Value.center, 0.5f);
                yield return new WaitForSeconds(0.9f);
                InventoryGui.DevAlt = true;
                UI.TerraCursor.DevClick();
                yield return null;
                yield return null;
                InventoryGui.DevAlt = false;
                yield return new WaitForSeconds(1.6f);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 背包：铁皮药水收藏 {inv.PotionBox[0][0].Favorite}，增益 {Player.PlayerBuffs.Has(5)}");
                StopRec();
                Containers.Toggle(ItemType.PotionBox);
                yield return null;
            }

            // --- The Banner Case.
            if (caseSlot >= 0)
            {
                yield return D4Rec("inv_bannerbox");
                yield return new WaitForSeconds(0.2f);
                var r = InventoryGui.DevHitRect("Slot", caseSlot);
                if (r != null) yield return Glide(r.Value.center, 0.6f);
                yield return new WaitForSeconds(0.35f);
                InventoryGui.DevRightClick(caseSlot);
                yield return new WaitForSeconds(0.8f);
                foreach (int k in new[] { 3, 14, 27 })
                {
                    var b = InventoryGui.DevHitRect("ChestSlot", k);
                    if (b != null) { yield return Glide(b.Value.center, 0.4f); yield return new WaitForSeconds(0.6f); }
                }
                yield return ClickHit("ChestButton", 8, 0.5f);
                yield return new WaitForSeconds(1.0f);
                StopRec();
                Containers.Toggle(ItemType.BannerCase);
                yield return null;
            }

            InventoryGui.Open = false;
            PointerOff();
            mod.ReturnOrDropMouseItem();
        }

        /// <summary>A creative save's bag: the Hallownest bag full, the crafting window, ∞ stacks.</summary>
        IEnumerator P4_Dinv2(HeroController hero)
        {
            yield return P4Kit();
            yield return D4ToTown(hero);
            hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            var inv = mod.Inventory;
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(1.5f, 1.5f)));
            InventoryGui.Open = true;
            yield return new WaitForSeconds(0.5f);
            // --- The Hallownest bag.
            yield return D4Rec("inv_hkbag");
            yield return new WaitForSeconds(0.2f);
            yield return ClickAt(D4Utility(2), 0.45f);
            yield return new WaitForSeconds(0.8f);
            var hk = InventoryGui.DevHitIndexes("HkBag");
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 背包：圣巢背包格 {hk.Count}");
            foreach (int k in new[] { 0, 4, 9, 13, 27, 28 })
            {
                var r = InventoryGui.DevHitRect("HkBag", k);
                if (r == null) continue;
                yield return Glide(r.Value.center, 0.4f);
                yield return new WaitForSeconds(0.65f);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 背包：圣巢格 {k} {InventoryGui.DevTooltip.Replace("\n", " | ")}");
            }
            StopRec();
            yield return ClickAt(D4Utility(2), 0.3f);
            InventoryGui.Open = false;
            yield return new WaitForSeconds(0.4f);

            // --- The crafting window: its tabs, then "全部".
            yield return D4Rec("inv_craft");
            yield return new WaitForSeconds(0.2f);
            InventoryGui.Open = true;
            mod.PlaySound("Menu_Open", 0.7f);
            yield return new WaitForSeconds(0.5f);
            yield return ClickHit("GridToggle", 0, 0.7f);
            yield return new WaitForSeconds(0.8f);
            foreach (int f in new[] { 0, 2, 1, 7, 3 })
            {
                yield return ClickHit("FilterEntry", f, 0.4f);
                yield return new WaitForSeconds(0.75f);
            }
            yield return ClickHit("FilterEntry", -1, 0.5f);
            yield return new WaitForSeconds(1.2f);
            StopRec();
            yield return ClickHit("GridToggle", 0, 0.3f);
            InventoryGui.Open = false;
            yield return new WaitForSeconds(0.4f);

            // --- Creative: ∞ stacks, and anything made for nothing.
            // A stack of potions that shows ∞.
            int inf = D4EmptyBag(20);
            if (inf >= 0) inv.Slots[inf] = ItemStack.Of(ItemType.SuperHealingPotion, 30);
            yield return D4Rec("creative_infinite");
            yield return new WaitForSeconds(0.2f);
            InventoryGui.Open = true;
            mod.PlaySound("Menu_Open", 0.7f);
            yield return new WaitForSeconds(0.5f);
            if (inf >= 0)
            {
                var r = InventoryGui.DevHitRect("Slot", inf);
                if (r != null) yield return Glide(r.Value.center, 0.6f);
                yield return new WaitForSeconds(0.5f);
                for (int k = 0; k < 6; k++)
                {
                    InventoryGui.DevRightClick(inf);
                    yield return new WaitForSeconds(0.18f);
                }
                yield return new WaitForSeconds(0.4f);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 创造：右键 ∞ {inv.Slots[inf].Def?.Name}，光标 {inv.Mouse.Count}，格里 {inv.Slots[inf].Count}");
                int empty = D4EmptyBag(10);
                if (empty >= 0) yield return ClickHit("Slot", empty, 0.6f);
                yield return new WaitForSeconds(0.5f);
            }
            // The window: every item, free.
            yield return ClickHit("GridToggle", 0, 0.6f);
            yield return new WaitForSeconds(0.9f);
            var recs = InventoryGui.DevHitIndexes("Recipe");
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 创造：制作窗口 {recs.Count} 格，配方 {InventoryGui.DevOffered().Count}");
            for (int k = 0; k < 4 && k < recs.Count; k++)
            {
                var rr = InventoryGui.DevHitRect("Recipe", recs[Mathf.Min(recs.Count - 1, 3 + k * 7)]);
                if (rr != null) { yield return Glide(rr.Value.center, 0.4f); yield return new WaitForSeconds(0.5f); }
            }
            yield return new WaitForSeconds(0.6f);
            StopRec();
            yield return ClickHit("GridToggle", 0, 0.3f);
            InventoryGui.Open = false;
            PointerOff();
            mod.ReturnOrDropMouseItem();
        }
    }
}
