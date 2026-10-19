using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (trailer 4.0, recorder D): Dirtmouth's shops and the Goblin Tinkerer, on an ordinary (not creative) save at
    /// stage 0. The Merchant's shop with the later stages' stock dimmed under a lock, hovered (the stage and what opens
    /// it), then a buy into the bag; the Goblin's forge: a sword forged up to its cap (the name popping over the hero, the
    /// cap in cycling colours), a breastplate to +5 ("防御 X → Y"); his reforge rolled until the best prefix comes up.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>The hero beside a townsperson, a step to its left, standing on the street.</summary>
        IEnumerator D4BesideNpc(HeroController hero, string npc, float side = 2.4f, float? standAt = null)
        {
            // Stood a while at a spot of the street first (under the lamps by the bench), if asked.
            if (standAt != null)
            {
                TownNpcs.DevSend(npc, standAt.Value, 0);
                yield return null;
            }
            var at = TownNpcs.DevPos(npc);
            if (at == null) yield break;
            var col = hero.GetComponent<BoxCollider2D>();
            float bottom = col.bounds.min.y - hero.transform.position.y;
            float feet = at.Value.y - 20f / 32f;
            hero.transform.position = Clear(hero, new Vector2(at.Value.x + side, feet - bottom + 0.05f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.5f);
        }

        /// <summary>The pointer onto a townsperson and a right click: its chat.</summary>
        IEnumerator D4Talk(string npc)
        {
            var at = TownNpcs.DevPos(npc);
            if (at == null) yield break;
            yield return Glide(GuiOf(at.Value + new Vector2(0f, 0.6f)), 0.6f);
            yield return new WaitForSeconds(0.25f);
            at = TownNpcs.DevPos(npc);
            bool ok = TownNpcs.TryRightClick(at.Value + new Vector2(0f, 0.6f));
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 对话 {npc}：{ok}");
            yield return new WaitForSeconds(1.1f);
        }

        /// <summary>The Goblin Tinkerer's clips alone (suite p4_forge_zh).</summary>
        bool d4SkipShop;

        IEnumerator P4_Dforge(HeroController hero)
        {
            d4SkipShop = true;
            yield return P4_Dshop(hero);
        }

        IEnumerator P4_Dshop(HeroController hero)
        {
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            Player.TerraStats.DevImmortal = true;
            Shop.DevStage = 0;
            yield return EnterRoom("Town");
            hero = HeroController.instance;
            pd.isInvincible = true;
            D4NoAreaTitle();
            yield return new WaitForSeconds(3f);
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 商店：创造 {mod.Creative}，阶段 {Shop.UnlockedStage()}，居民 {TownNpcs.DevDescribe()}");
            Shop.Earn(90000L * 100L);
            var sword = ItemStack.Of(ItemType.PlatinumBroadsword);
            sword.Forge = 2;
            var mail = ItemStack.Of(ItemType.PlatinumChainmail);
            mail.Forge = 2;
            inv.Slots[12] = sword;
            inv.Slots[13] = mail;
            for (int i = 14; i < 20; i++) inv.Slots[i] = default;
            var col = hero.GetComponent<BoxCollider2D>();

            if (!d4SkipShop)
            {
            // --- The Merchant: his shop, the later stages' stock locked.
            string seller = TownNpcs.DevPos("Merchant") != null ? "Merchant" : "ArmsDealer";
            yield return D4BesideNpc(hero, seller, 2.4f, TownNpcs.DevStreet.x + 5f);
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(-0.6f, 1.6f)));
            yield return D4Rec("shop_locked");
            yield return new WaitForSeconds(0.3f);
            yield return D4Talk(seller);
            yield return ClickHit("ChatButton", 0, 0.5f);
            yield return new WaitForSeconds(0.6f);
            var locked = new List<int>();
            var open = new List<int>();
            for (int i = 0; i < TownNpcs.ShopItems.Length; i++)
            {
                if (TownNpcs.ShopItems[i].IsEmpty) continue;
                if (TownNpcs.Locked(i)) locked.Add(i);
                else open.Add(i);
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 商店：在卖 {open.Count}，锁着 {locked.Count}：{TownNpcs.DevStock()}");
            // One on sale, then a few of the locked ones of different stages.
            if (open.Count > 2)
            {
                var r = InventoryGui.DevHitRect("ShopSlot", open[2]);
                if (r != null) { yield return Glide(r.Value.center, 0.45f); yield return new WaitForSeconds(0.6f); }
            }
            var shownStages = new HashSet<int>();
            int hovers = 0;
            foreach (int i in locked)
            {
                if (hovers >= 3) break;
                if (!shownStages.Add(TownNpcs.ShopLock[i])) continue;
                var r = InventoryGui.DevHitRect("ShopSlot", i);
                if (r == null) continue;
                yield return Glide(r.Value.center, 0.5f);
                yield return new WaitForSeconds(1.3f);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 商店：悬停锁着的 {i}（阶段 {TownNpcs.ShopLock[i]}）：{InventoryGui.DevTooltip.Replace("\n", " | ")}");
                hovers++;
            }
            StopRec();

            // --- A buy: onto the cursor, into the bag.
            yield return D4Rec("shop_buy");
            int buy = open.Count > 4 ? open[4] : open.Count > 0 ? open[0] : -1;
            if (buy >= 0)
            {
                var r = InventoryGui.DevHitRect("ShopSlot", buy);
                if (r != null) { yield return Glide(r.Value.center, 0.55f); yield return new WaitForSeconds(0.7f); }
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 商店：买 {TownNpcs.ShopItems[buy].Def?.Name}：{InventoryGui.DevTooltip.Replace("\n", " | ")}");
                long before = Shop.Purse;
                yield return ClickHit("ShopSlot", buy, 0.15f);
                yield return new WaitForSeconds(0.45f);
                int empty = -1;
                for (int i = 14; i < Inventory.Size; i++) if (inv.Slots[i].IsEmpty) { empty = i; break; }
                if (empty >= 0) yield return ClickHit("Slot", empty, 0.55f);
                yield return new WaitForSeconds(0.8f);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 商店：花了 {Shop.Money(before - Shop.Purse)}，光标 {(inv.Mouse.IsEmpty ? "空" : inv.Mouse.Def.Name)}");
            }
            StopRec();
            TownNpcs.Close(true);
            InventoryGui.Open = false;
            mod.ReturnOrDropMouseItem();
            yield return new WaitForSeconds(0.5f);

            }

            // --- The Goblin Tinkerer's forge: the sword to its cap.
            if (TownNpcs.DevPos("GoblinTinkerer") == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 录制 锻造：哥布林不在");
                PointerOff();
                yield break;
            }
            yield return D4BesideNpc(hero, "GoblinTinkerer", 2.4f, TownNpcs.DevStreet.x + 10f);
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(-0.6f, 1.6f)));
            yield return D4Rec("forge_weapon");
            yield return new WaitForSeconds(0.3f);
            yield return D4Talk("GoblinTinkerer");
            yield return ClickHit("ChatButton", 7, 0.5f);
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 锻造：锻造格 {TownNpcs.ForgeMode}");
            yield return ClickHit("Slot", 12, 0.6f);
            yield return new WaitForSeconds(0.2f);
            yield return ClickHit("ReforgeSlot", 0, 0.55f);
            yield return new WaitForSeconds(0.7f);
            // Three hammer blows: one fails (half of them do), the next two take it to the cap.
            foreach (bool take in new[] { true, false, true, true })
            {
                if (Forging.AtMax(TownNpcs.ReforgeItem)) break;
                var hr = InventoryGui.DevHitRect("ForgeButton", 0);
                if (hr == null) break;
                yield return Glide(hr.Value.center, 0.35f);
                yield return new WaitForSeconds(0.25f);
                Forging.DevOutcome = take;
                InventoryGui.DevReleaseHammer();
                UI.TerraCursor.DevClick();
                yield return null;
                yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 锻造：{(take ? "成功" : "失败")} → {TownNpcs.ReforgeItem.Def?.Name}");
                yield return new WaitForSeconds(0.85f);
            }
            Forging.DevOutcome = null;
            yield return Glide(GuiOf((Vector2)col.bounds.center + new Vector2(2.5f, 2.5f)), 0.6f);
            yield return new WaitForSeconds(1.0f);
            StopRec();

            // --- The breastplate: into the slot (the sword back into the bag), forged to +5.
            yield return D4Rec("forge_armor");
            yield return new WaitForSeconds(0.2f);
            yield return ClickHit("Slot", 13, 0.55f);
            yield return new WaitForSeconds(0.15f);
            yield return ClickHit("ReforgeSlot", 0, 0.5f);
            yield return new WaitForSeconds(0.15f);
            yield return ClickHit("Slot", 13, 0.5f);
            yield return new WaitForSeconds(0.6f);
            var slotRect = InventoryGui.DevHitRect("ReforgeSlot", 0);
            if (slotRect != null) { yield return Glide(slotRect.Value.center + new Vector2(150f, -10f), 0.5f); yield return new WaitForSeconds(0.9f); }
            for (int k = 0; k < 4; k++)
            {
                if (Forging.AtMax(TownNpcs.ReforgeItem)) break;
                var hr = InventoryGui.DevHitRect("ForgeButton", 0);
                if (hr == null) break;
                yield return Glide(hr.Value.center, 0.35f);
                yield return new WaitForSeconds(0.25f);
                Forging.DevOutcome = true;
                InventoryGui.DevReleaseHammer();
                UI.TerraCursor.DevClick();
                yield return null;
                yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 锻造盔甲 → {TownNpcs.ReforgeItem.Def?.Name}，防御 {TownNpcs.ReforgeItem.Def?.Defense}");
                yield return new WaitForSeconds(0.8f);
            }
            Forging.DevOutcome = null;
            if (slotRect != null) yield return Glide(slotRect.Value.center, 0.5f);
            yield return new WaitForSeconds(1.4f);
            StopRec();
            TownNpcs.Close(true);
            InventoryGui.Open = false;
            mod.ReturnOrDropMouseItem();
            yield return new WaitForSeconds(0.6f);

            // --- His reforge: rolled until the best prefix (the dice for each roll chosen ahead, the clicks real).
            int swordSlot = -1;
            for (int i = 0; i < Inventory.Size; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == ItemType.PlatinumBroadsword) swordSlot = i;
            if (swordSlot < 0) { PointerOff(); yield break; }
            var def = ItemDb.Get(ItemType.PlatinumBroadsword);
            var saved = Random.state;
            var seeds = new List<int>();
            var used = new HashSet<int>();
            int bestSeed = -1;
            for (int s = 1; s < 20000 && (seeds.Count < 3 || bestSeed < 0); s++)
            {
                Random.InitState(s);
                int p = Prefixes.Roll(def, out bool best);
                if (best) { if (bestSeed < 0) bestSeed = s; }
                else if (seeds.Count < 3 && p > 0 && used.Add(p)) seeds.Add(s);
            }
            Random.state = saved;
            if (bestSeed > 0) seeds.Add(bestSeed);
            yield return D4BesideNpc(hero, "GoblinTinkerer", 2.4f, TownNpcs.DevStreet.x + 10f);
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(-0.6f, 1.6f)));
            yield return D4Rec("reforge_best");
            yield return new WaitForSeconds(0.3f);
            yield return D4Talk("GoblinTinkerer");
            yield return ClickHit("ChatButton", 6, 0.5f);
            yield return new WaitForSeconds(0.5f);
            yield return ClickHit("Slot", swordSlot, 0.6f);
            yield return new WaitForSeconds(0.15f);
            yield return ClickHit("ReforgeSlot", 0, 0.5f);
            yield return new WaitForSeconds(0.6f);
            foreach (int s in seeds)
            {
                var hr = InventoryGui.DevHitRect("ReforgeButton", 0);
                if (hr == null) break;
                yield return Glide(hr.Value.center, 0.3f);
                yield return new WaitForSeconds(0.3f);
                Random.InitState(s);
                InventoryGui.DevReforge();
                Random.InitState(System.Environment.TickCount);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 重铸 → {TownNpcs.ReforgeItem.Def?.Name}，最佳 {InventoryGui.DevLastBest}");
                yield return new WaitForSeconds(s == bestSeed ? 1.6f : 0.75f);
            }
            var sr = InventoryGui.DevHitRect("ReforgeSlot", 0);
            if (sr != null) yield return Glide(sr.Value.center, 0.5f);
            yield return new WaitForSeconds(1.4f);
            StopRec();
            TownNpcs.Close(true);
            InventoryGui.Open = false;
            PointerOff();
            mod.ReturnOrDropMouseItem();
            Shop.DevStage = -1;
        }
    }
}
