using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator AbilityRecoveryTest()
        {
            mod.ApplyCreative(false);
            Shop.DevStage = 5;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(3f);
            var inv = mod.Inventory;
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            inv.Infinite = false;
            int passed = 0, total = 0;
            void Check(string name, bool ok, string detail = "")
            {
                total++; if (ok) passed++;
                Plugin.Log.LogInfo($"[AutoTest] ABILITY_RECOVERY {name}: {detail} {(ok ? "通过" : "不通过")}");
            }
            void ClearItems()
            {
                Array.Clear(inv.Slots, 0, inv.Slots.Length);
                Array.Clear(inv.HkBag, 0, inv.HkBag.Length);
                inv.Mouse = default;
            }
            void Wear(ItemType t)
            {
                Array.Clear(inv.Accessories, 0, inv.Accessories.Length);
                foreach (var l in inv.Loadouts) Array.Clear(l.Accessories, 0, l.Accessories.Length);
                inv.Accessories[0] = ItemStack.Of(t);
                Equipment.Update(inv);
            }
            int Count(ItemType t) => inv.Slots.Concat(inv.HkBag).Concat(new[] { inv.Mouse }).Where(s => !s.IsEmpty && s.Type == t).Sum(s => s.Count);
            ClearItems();
            inv.HallownestObtained.Clear();
            Check("unseen not unlocked", !HallownestRecovery.CanBuy(inv, ItemType.VoidHallownestShield));
            Check("character records isolated", !HallownestRecovery.CanBuy(new Inventory(), ItemType.VoidHallownestShield));
            foreach (var d in AbilityShields.All)
            {
                Wear(d.Type);
                var tex = Terraria.TerrariaAssets.Texture(ItemDb.Get(d.Type).Texture);
                Check("equipped " + d.Type,
                    Equipment.Current.DashKind == 2 && Equipment.Current.DownDash == (d.Level > 0) && Equipment.Current.DiveLevel == d.Level
                    && AbilityAccessories.CrystalWorn == ((d.Mask & AbilityShields.Crystal) != 0)
                    && AbilityAccessories.ShadeWorn == ((d.Mask & AbilityShields.Shade) != 0)
                    && tex != null && tex.width == 36 && tex.height == 36
                    && Prefixes.CanHave(ItemDb.Get(d.Type)));
                Check("protected but sellable " + d.Type, ItemDb.Get(d.Type).CannotDiscard && !ItemDb.Get(d.Type).CannotSell);
            }
            Check("legacy shield remains two abilities", AbilityShields.Get(ItemType.HallownestShield).Level == 0
                && ItemDb.Get(ItemType.HallownestShield).Name == "晶影之盾");
            Wear(ItemType.DescendingDarkRune);
            Check("dark accessory equipped", Equipment.Current.DownDash && Equipment.Current.DiveLevel == 2 && !AbilityAccessories.CrystalWorn && !AbilityAccessories.ShadeWorn);

            var hb = hero.GetComponent<BoxCollider2D>().bounds;
            bool placed = Stations.TryPlace(StationKind.TinkerersWorkshop, hero, new Vector2(hb.center.x + 1.5f, hb.min.y + 0.3f))
                || Stations.TryPlace(StationKind.TinkerersWorkshop, hero, new Vector2(hb.center.x - 1.5f, hb.min.y + 0.3f));
            Check("workshop available", placed || Stations.Near("工匠作坊", hero));
            var recipes = AbilityShields.Recipes().ToArray();
            Check("24 upgrade recipes", recipes.Length == 24);
            foreach (var r in recipes)
            {
                ClearItems();
                for (int k = 0; k < r.Ingredients.Length; k++) inv.Slots[30 + k] = ItemStack.Of(r.Ingredients[k].Item1);
                bool crafted = InventoryGui.DevCraft(r.Result);
                Check("craft " + r.Result + " from " + r.Ingredients[0].Item1 + "/" + r.Ingredients[1].Item1,
                    crafted && inv.Mouse.Type == r.Result && inv.Slots[30].IsEmpty && inv.Slots[31].IsEmpty
                    && inv.HallownestObtained.Contains(r.Result));
            }
            ClearItems();
            HkItems.TryStoreEquipment(inv, ItemStack.Of(ItemType.HallownestDiveShield));
            HkItems.TryStoreAbility(inv, ItemStack.Of(ItemType.DescendingDarkRune));
            Check("late black upgrade directly from Hallownest bag", InventoryGui.DevCraft(ItemType.VoidHallownestShield)
                && inv.Mouse.Type == ItemType.VoidHallownestShield && inv.HkBag[HkItems.HallownestShieldSlot].IsEmpty && inv.HkBag[HkItems.DarkRuneSlot].IsEmpty);
            Check("consumed ingredients remain unlocked", inv.HallownestObtained.Contains(ItemType.HallownestDiveShield)
                && inv.HallownestObtained.Contains(ItemType.DescendingDarkRune));

            ClearItems();
            var pd = PlayerData.instance;
            Wear(ItemType.None);
            pd.quakeLevel = 2;
            mod.GrantedAbilities.Remove("quakeLevel.dark.v1");
            inv.HkBag[HkItems.DarkRuneSlot] = ItemStack.Of(ItemType.StoneBlock);
            Check("blocked reward retries", !mod.GrantDarkDiveReward(pd) && !mod.GrantedAbilities.Contains("quakeLevel.dark.v1"));
            inv.HkBag[HkItems.DarkRuneSlot] = default;
            Check("dark reward delivered once", mod.GrantDarkDiveReward(pd) && inv.HkBag[HkItems.DarkRuneSlot].Type == ItemType.DescendingDarkRune
                && !mod.GrantDarkDiveReward(pd));

            // Actual movement and landing on the Town floor, with separate ordinary/upgraded variants.
            InventoryGui.Open = false;
            var stand = hero.transform.position;
            foreach (var type in new[] { ItemType.HallownestDiveShield, ItemType.VoidHallownestShield })
            {
                Wear(type);
                yield return new WaitForSeconds(0.3f);
                hero.transform.position = stand + Vector3.up * 3f;
                hero.cState.onGround = false;
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.15f);
                int starts = TerraMovement.DevDownDashes, pounds = TerraMovement.DevPounds;
                TerraMovement.DevDown = true; TerraMovement.DevDownDash = true;
                for (float t = 0; t < 4f && TerraMovement.DevPounds == pounds; t += Time.deltaTime) yield return null;
                TerraMovement.DevDown = false;
                Check("slam starts and lands " + type, TerraMovement.DevDownDashes > starts && TerraMovement.DevPounds > pounds);
                hero.transform.position = stand;
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.3f);
            }
            Check("dark hit damage upgrade", TerraMovement.DownDamage(30, 1) == 30 && TerraMovement.DownDamage(30, 2) == 60
                && TerraMovement.DownDamage(40, 2) == 80);

            if (TargetDummy.DevAt.HasValue)
            {
                hero.transform.position = new Vector3(TargetDummy.DevAt.Value.x - 1.3f, TargetDummy.DevAt.Value.y + 2.5f, hero.transform.position.z);
                yield return new WaitForSeconds(0.7f);
                var pound = typeof(TerraMovement).GetMethod("GroundPound", BindingFlags.NonPublic | BindingFlags.Static);
                var level = typeof(TerraMovement).GetField("downDiveLevel", BindingFlags.NonPublic | BindingFlags.Static);
                var time = typeof(TerraMovement).GetField("downDashTime", BindingFlags.NonPublic | BindingFlags.Static);
                int normal = 0, dark = 0;
                foreach (int l in new[] { 1, 2 })
                {
                    yield return new WaitForSeconds(0.5f);
                    var enemyInfo = EnemyStats.Of(TargetDummy.DevHm);
                    int life = EnemyStats.Life(TargetDummy.DevHm, enemyInfo);
                    level.SetValue(null, l); time.SetValue(null, 0f);
                    pound.Invoke(null, new object[] { hero });
                    // Terraria health includes fractional native-health carry; the wooden dummy's native hp need not drop on every hit.
                    int dealt = life - EnemyStats.Life(TargetDummy.DevHm, enemyInfo);
                    if (l == 1) normal = dealt; else dark = dealt;
                    Check("ground pound actually damages dummy level " + l, dealt > 0, "damage=" + dealt);
                    if (l == 2) Check("dark landing grants immunity", hero.cState.invulnerable);
                }
                Check("dark deals more damage to dummy", dark > normal && normal > 0, normal + " -> " + dark);
            }
            else Check("dummy available for real hit checks", false);

            var goblin = TownNpcs.DevPos("GoblinTinkerer");
            Check("goblin present", goblin.HasValue);
            if (!goblin.HasValue) { Plugin.Log.LogInfo("[AutoTest] [ABILITY_RECOVERY_FAIL] no goblin"); yield break; }
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            hero.transform.position = Clear(hero, new Vector2(goblin.Value.x - 1.5f, goblin.Value.y - 20f / 32f - bottom + 0.05f));
            yield return new WaitForSeconds(0.6f);
            TownNpcs.TryRightClick(TownNpcs.DevPos("GoblinTinkerer").Value);
            TownNpcs.OpenRecoveryShop();
            Check("goblin recovery shop open", TownNpcs.ShopOpen && TownNpcs.RecoveryShop);
            ClearItems();
            Shop.Earn(5000000);
            pd.hasLantern = true;
            inv.HallownestTradeOffsets.Remove(ItemType.HkLumaflyLantern);
            HkItems.Tick(mod);
            var original = inv.HkBag.FirstOrDefault(s => s.Type == ItemType.HkLumaflyLantern);
            int loveSlot = Array.FindIndex(inv.HkBag, s => s.Type == ItemType.HkLumaflyLantern);
            Check("native acquisition recorded", loveSlot >= 0 && inv.HallownestObtained.Contains(ItemType.HkLumaflyLantern));
            if (loveSlot >= 0)
            {
                inv.Mouse = original; inv.HkBag[loveSlot] = default;
                long before = Shop.Purse;
                TownNpcs.Sell(ref inv.Mouse);
                HkItems.Tick(mod);
                Check("sold native item stays sold", inv.Mouse.IsEmpty && Count(ItemType.HkLumaflyLantern) == 0 && Shop.Purse > before && pd.hasLantern);
                TownNpcs.OpenRecoveryShop();
                int at = Array.FindIndex(TownNpcs.ShopItems, s => s.Type == ItemType.HkLumaflyLantern);
                long price = at >= 0 ? TownNpcs.BuyPrice(at) : -1;
                before = Shop.Purse;
                bool bought = at >= 0 && TownNpcs.Buy(at, 1, inv);
                HkItems.Tick(mod);
                Check("known native item repurchased", bought && Count(ItemType.HkLumaflyLantern) == 1 && Shop.Purse == before - price && price > 0);
                // Quick sell follows the same native synchronization as an NPC sale.
                var quick = typeof(InventoryGui).GetMethod("SellStack", BindingFlags.NonPublic | BindingFlags.Static);
                var args = new object[] { inv.Mouse };
                bool sold = (bool)quick.Invoke(null, args); inv.Mouse = (ItemStack)args[0];
                HkItems.Tick(mod);
                Check("quick selling native item stays sold", sold && Count(ItemType.HkLumaflyLantern) == 0);
            }
            inv.Mouse = default;
            pd.simpleKeys = 2;
            HkItems.Tick(mod);
            int keys = Array.FindIndex(inv.HkBag, s => s.Type == ItemType.HkSimpleKey);
            if (keys >= 0)
            {
                inv.Mouse = inv.HkBag[keys]; inv.HkBag[keys] = default;
                TownNpcs.Sell(ref inv.Mouse); HkItems.Tick(mod);
                Check("counted keys sold once", pd.simpleKeys == 0 && Count(ItemType.HkSimpleKey) == 0);
                TownNpcs.OpenRecoveryShop();
                int at = Array.FindIndex(TownNpcs.ShopItems, s => s.Type == ItemType.HkSimpleKey);
                bool bought = at >= 0 && TownNpcs.Buy(at, 1, inv);
                HkItems.Tick(mod);
                Check("consumed keys cannot be repurchased", !bought && at < 0 && pd.simpleKeys == 0 && Count(ItemType.HkSimpleKey) == 0);
            }
            else Check("key stack available", false);
            inv.Mouse = default;
            pd.hasXunFlower = true; pd.xunFlowerBroken = false;
            HkItems.Tick(mod);
            int flower = Array.FindIndex(inv.HkBag, s => s.Type == ItemType.HkDelicateFlower);
            if (flower >= 0)
            {
                inv.Mouse = inv.HkBag[flower]; inv.HkBag[flower] = default;
                TownNpcs.Sell(ref inv.Mouse); HkItems.Tick(mod);
                Check("sold flower stays sold", Count(ItemType.HkDelicateFlower) == 0);
                pd.xunFlowerBroken = true; HkItems.Tick(mod);
                pd.xunFlowerBroken = false; HkItems.Tick(mod);
                Check("fresh native flower is delivered after a sale", Count(ItemType.HkDelicateFlower) == 1);
            }
            else Check("flower available", false);
            foreach (var tool in new[] { ItemType.KnightNail, ItemType.DreamNail })
            {
                pd.hasDreamNail = true;
                for (int i = 0; i < inv.Slots.Length; i++) if (inv.Slots[i].Type == tool) inv.Slots[i] = default;
                for (int i = 0; i < inv.HkBag.Length; i++) if (inv.HkBag[i].Type == tool) inv.HkBag[i] = default;
                inv.Mouse = ItemStack.Of(tool);
                TownNpcs.Sell(ref inv.Mouse);
                yield return new WaitForSeconds(1.2f);
                Check("sold quest tool stays sold " + tool, Count(tool) == 0 && HallownestRecovery.SoldTool(inv, tool));
                TownNpcs.OpenRecoveryShop();
                int at = Array.FindIndex(TownNpcs.ShopItems, s => s.Type == tool);
                bool bought = at >= 0 && TownNpcs.Buy(at, 1, inv);
                Check("quest tool can be repurchased " + tool, bought && Count(tool) == 1 && !HallownestRecovery.SoldTool(inv, tool));
                TownNpcs.Sell(ref inv.Mouse);
            }
            inv.HallownestObtained.Remove(ItemType.HkVoidHeart);
            TownNpcs.OpenRecoveryShop();
            Check("unknown item hidden", !TownNpcs.ShopItems.Any(s => !s.IsEmpty && s.Type == ItemType.HkVoidHeart));
            TownNpcs.ShopItems[0] = ItemStack.Of(ItemType.HkVoidHeart);
            Check("unknown spoofed purchase rejected", !TownNpcs.Buy(0, 1, inv));
            TownNpcs.OpenRecoveryShop();
            var known = HallownestRecovery.Stock(inv).ToArray();
            Check("only known items in recovery stock", TownNpcs.ShopItems.Where(s => !s.IsEmpty).All(s => inv.HallownestObtained.Contains(s.Type)));
            Check("personal catalog fits shop", ItemDb.All.Count(d => HallownestRecovery.IsUnique(d.Type)) <= TownNpcs.ShopItems.Length);
            yield return new WaitForSeconds(3f);
            Shot("Town", "ability_recovery_shop");
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.3f);

            inv.Mouse = ItemStack.Of(ItemType.VoidHallownestShield);
            var trash = typeof(InventoryGui).GetMethod("ClickTrash", BindingFlags.NonPublic | BindingFlags.Static);
            trash.Invoke(null, null);
            Check("trash rejects unique", inv.Mouse.Type == ItemType.VoidHallownestShield);
            string drops = WorldItems.DevDescribe();
            WorldItems.Throw(inv.Mouse, hero);
            Check("throw rejects unique", WorldItems.DevDescribe() == drops);
            var drop = new Net.WorldSync.Action { Kind = (int)ItemType.VoidHallownestShield, Count = 1, Scene = "Town", X = 1, Y = 1 };
            Check("host rejects unique drop packet", !WorldItems.NetSpawn(drop, 1));
            inv.Mouse = default;
            TownNpcs.Close(false);
            var chestAt = hero.GetComponent<BoxCollider2D>().bounds;
            bool chestPlaced = false;
            foreach (float dx in new[] { 2.5f, -2.5f, 4f, -4f })
                if (Chests.TryPlace(hero, new Vector2(chestAt.center.x + dx, chestAt.min.y + 0.3f))) { chestPlaced = true; break; }
            Chests.DevOpenNear(hero.transform.position);
            var chestRecord = Chests.NetRecords().FirstOrDefault(c => c.Key == Chests.DevOpenKey);
            Check("shared chest available", chestPlaced || chestRecord != null);
            if (chestRecord != null)
            {
                var slots = Chests.NetContents(chestRecord.Key);
                slots[0] = Net.WorldSync.Stack.From(ItemStack.Of(ItemType.VoidHallownestShield));
                Check("host accepts unique shared chest contents", Chests.NetContents(chestRecord.Key, slots));
                Check("shared chest contains unique item", Chests.NetContents(chestRecord.Key).Any(s => s != null && s.Type == (int)ItemType.VoidHallownestShield));
                if (Chests.IsOpen)
                {
                    inv.Mouse = default;
                    InventoryGui.DevChestClick(0);
                    Check("unique shared chest item freely withdrawable", inv.Mouse.Type == ItemType.VoidHallownestShield);
                    inv.Mouse.Prefix = 65;
                    InventoryGui.DevChestClick(0);
                    Check("unique shared chest item can be redeposited", inv.Mouse.IsEmpty && Chests.OpenItems[0].Type == ItemType.VoidHallownestShield);
                    Check("stored shield keeps reforge prefix", Chests.OpenItems[0].Prefix == 65);
                    // Counted native keys must move entitlement with the actual stack.
                    for (int i = 0; i < inv.Slots.Length; i++) if (inv.Slots[i].Type == ItemType.HkSimpleKey) inv.Slots[i] = default;
                    for (int i = 0; i < inv.HkBag.Length; i++) if (inv.HkBag[i].Type == ItemType.HkSimpleKey) inv.HkBag[i] = default;
                    pd.simpleKeys = 2;
                    inv.Mouse = ItemStack.Of(ItemType.HkSimpleKey, 2);
                    Chests.Put(ref inv.Mouse);
                    HkItems.Tick(mod);
                    Check("stored keys are not duplicated into inventory", inv.Mouse.IsEmpty && pd.simpleKeys == 0 && Count(ItemType.HkSimpleKey) == 0);
                    int keyAt = Array.FindIndex(Chests.OpenItems, x => x.Type == ItemType.HkSimpleKey);
                    InventoryGui.DevChestClick(keyAt);
                    HkItems.Tick(mod);
                    Check("withdrawn keys restore native count", inv.Mouse.Type == ItemType.HkSimpleKey && inv.Mouse.Count == 2 && pd.simpleKeys == 2);
                    HkItems.Trade(inv, ItemType.HkSimpleKey, -2); inv.Mouse = default;
                    pd.simpleKeys = 3;
                    inv.Slots[30] = ItemStack.Of(ItemType.HkSimpleKey);
                    inv.Mouse = ItemStack.Of(ItemType.HkSimpleKey, 2);
                    Chests.Put(ref inv.Mouse);
                    Chests.Restock(inv);
                    Check("restock restores native key quantities", inv.Slots[30].Count == 3 && pd.simpleKeys == 3);
                    Chests.DepositAll(inv);
                    Check("deposit all keeps native quantities correct", inv.Slots[30].IsEmpty && pd.simpleKeys == 0);
                    Chests.LootAll(inv);
                    Check("loot all native keys works", Count(ItemType.HkSimpleKey) == 3 && pd.simpleKeys == 3);
                    int shieldAt = Array.FindIndex(inv.Slots, s => s.Type == ItemType.VoidHallownestShield);
                    if (shieldAt >= 0) Chests.Put(ref inv.Slots[shieldAt]);
                    int keySlot = Array.FindIndex(inv.Slots, s => s.Type == ItemType.HkSimpleKey);
                    if (keySlot >= 0)
                    {
                        inv.Slots[keySlot].Count--;
                        inv.Mouse = ItemStack.Of(ItemType.HkSimpleKey);
                        Chests.Put(ref inv.Mouse);
                        Chests.QuickStack(inv);
                        Check("quick stack keeps native quantities correct", Count(ItemType.HkSimpleKey) == 0 && pd.simpleKeys == 0);
                        HallownestStorage.Move(inv, () => Chests.LootAll(inv));
                        Check("nested storage batches count once", Count(ItemType.HkSimpleKey) == 3 && pd.simpleKeys == 3);
                        shieldAt = Array.FindIndex(inv.Slots, s => s.Type == ItemType.VoidHallownestShield);
                        if (shieldAt >= 0) Chests.Put(ref inv.Slots[shieldAt]);
                        HkItems.Trade(inv, ItemType.HkSimpleKey, -3);
                        for (int i = 0; i < inv.Slots.Length; i++) if (inv.Slots[i].Type == ItemType.HkSimpleKey) inv.Slots[i] = default;
                    }
                    else Check("native key slot for quick stack", false);
                    Chests.Close(false);
                }
                else Check("open shared chest for deposit", false);
            }

            // All four personal storages use the same native transaction path.
            var bankItems = new[] { ItemType.PiggyBank, ItemType.Safe, ItemType.DefendersForge, ItemType.VoidBag };
            for (int bank = 0; bank < 4; bank++)
            {
                Array.Clear(inv.BankOf(bank), 0, inv.BankOf(bank).Length);
                Chests.TogglePortable(bank, bankItems[bank]);
                Check("personal storage opens " + bank, Chests.BankOpen);
                inv.Mouse = ItemStack.Of(ItemType.HkLumaflyLantern);
                HkItems.Trade(inv, ItemType.HkLumaflyLantern, 1);
                InventoryGui.DevChestClick(0);
                HkItems.Tick(mod);
                Check("native item stays stored without duplication " + bank, inv.Mouse.IsEmpty && Count(ItemType.HkLumaflyLantern) == 0
                    && Chests.OpenItems[0].Type == ItemType.HkLumaflyLantern);
                InventoryGui.DevChestClick(0);
                HkItems.Tick(mod);
                Check("native item withdraws from storage " + bank, inv.Mouse.Type == ItemType.HkLumaflyLantern && Count(ItemType.HkLumaflyLantern) == 1);
                HkItems.Trade(inv, ItemType.HkLumaflyLantern, -1); inv.Mouse = default;
                Chests.Close(false);
            }
            Chests.TogglePortable(1, ItemType.Safe);
            inv.Mouse = ItemStack.Of(ItemType.DreamNail);
            HkItems.Trade(inv, ItemType.DreamNail, 1);
            InventoryGui.DevChestClick(0);
            yield return new WaitForSeconds(1.2f);
            Check("stored dream nail is not automatically replaced", Count(ItemType.DreamNail) == 0 && inv.Safe[0].Type == ItemType.DreamNail);
            Chests.LootAll(inv);
            Check("loot all restores dream nail", Count(ItemType.DreamNail) == 1 && inv.Safe[0].IsEmpty && !HallownestRecovery.SoldTool(inv, ItemType.DreamNail));
            for (int i = 0; i < inv.Slots.Length; i++) if (inv.Slots[i].Type == ItemType.DreamNail) inv.Slots[i] = default;
            for (int i = 0; i < inv.HkBag.Length; i++) if (inv.HkBag[i].Type == ItemType.DreamNail) inv.HkBag[i] = default;
            HkItems.Trade(inv, ItemType.DreamNail, -1);
            Chests.Close(false);
            // Per-character acquisition and sold-item offsets survive the real serializer.
            Save.ModSave.Write(1, mod);
            int obtained = inv.HallownestObtained.Count;
            inv.HallownestObtained.Clear(); inv.HallownestTradeOffsets.Clear();
            Save.ModSave.Read(1, mod);
            Check("acquisition history reloads", inv.HallownestObtained.Count >= obtained && inv.HallownestObtained.Contains(ItemType.HkLumaflyLantern));
            HkItems.Tick(mod);
            Check("sold story item stays sold after reload", Count(ItemType.HkLumaflyLantern) == 0);
            Check("stored shield survives save reload with prefix", Chests.NetRecords().Any(c =>
                c.Slots.Any(x => x != null && x.Type == (int)ItemType.VoidHallownestShield && x.Prefix == 65)));
            yield return new WaitForSeconds(1.2f);
            Check("sold nail stays sold after reload", Count(ItemType.KnightNail) == 0 && HallownestRecovery.SoldTool(inv, ItemType.KnightNail));
            Check("sold dream nail stays sold after reload", Count(ItemType.DreamNail) == 0 && HallownestRecovery.SoldTool(inv, ItemType.DreamNail));
            Plugin.Log.LogInfo($"[AutoTest] ABILITY_RECOVERY_TOTAL {passed}/{total} {(passed == total ? "[ABILITY_RECOVERY_DONE]" : "[ABILITY_RECOVERY_FAIL]")}");
            Shop.DevStage = -1;
        }
    }
}

