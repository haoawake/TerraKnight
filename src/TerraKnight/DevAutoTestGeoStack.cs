using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "geostack"): the Geo in the coin slot (where picked-up Geo lands) onto the Piggy Bank's Geo stack: the
    /// inventory's "Quick Stack to Nearby Chests", the bank panel's "Deposit All" and "Quick Stack", and a Shift+click on the
    /// coin slot with the bank open; with a chest beside the bank holding Geo itself, both quick stacks (the chest's open
    /// panel's and the inventory's) still into the bank. Each ends with the bank's Geo in one stack and the game's geo unchanged.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GeoStackTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 吉欧存钱罐 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            float settle = Time.time + 3f;
            while (!hero.cState.onGround && Time.time < settle) yield return null;
            var inv = mod.Inventory;
            bool wasInfinite = inv.Infinite;
            mod.SetInfiniteItems(false);
            yield return null;
            var savedBag = (ItemStack[])inv.Slots.Clone();
            var savedBank = (ItemStack[])inv.Bank.Clone();
            int c = Inventory.CoinSlot;

            var feet = hero.GetComponent<BoxCollider2D>().bounds;
            bool placed = false;
            foreach (float dx in new[] { 1.5f, -1.5f, 2.5f, -2.5f, 3.5f, -3.5f })
                if (Stations.TryPlace(StationKind.PiggyBank, hero, new Vector2(feet.center.x + dx, feet.min.y + 0.2f))) { placed = true; break; }
            Check("放下存钱罐", placed, $"附近有存钱罐 {Stations.PiggyBankWithin(feet.center, 600f * Effects.TerraParticles.Px)}");

            int BankGeo() => inv.Bank.Where(s => !s.IsEmpty && s.Type == ItemType.Geo).Sum(s => s.Count);
            int BankStacks() => inv.Bank.Count(s => !s.IsEmpty && s.Type == ItemType.Geo);
            int game0 = 0;
            // A bank already holding Geo (100) and the coin slot holding 500, the rest of the bag empty.
            IEnumerator Reset()
            {
                for (int i = Inventory.HotbarSize; i < Inventory.Total; i++) inv.Slots[i] = default;
                for (int i = 0; i < inv.Bank.Length; i++) inv.Bank[i] = default;
                inv.Bank[3] = ItemStack.Of(ItemType.Geo, 100);
                inv.Slots[c] = ItemStack.Of(ItemType.Geo, 500);
                yield return new WaitForSeconds(0.3f);
                game0 = PlayerData.instance.geo;
            }
            string State() => $"钱币槽 {(inv.Slots[c].IsEmpty ? "空" : inv.Slots[c].Count.ToString())}，存钱罐吉欧 {BankGeo()}（{BankStacks()} 堆），游戏吉欧 {game0}→{PlayerData.instance.geo}";

            // 1. The inventory's Quick Stack button, the bank shut.
            yield return Reset();
            int moved = Chests.QuickStackNearby(inv, hero.GetComponent<BoxCollider2D>().bounds.center);
            yield return new WaitForSeconds(0.3f);
            Check("背包的快速堆叠：钱币槽的吉欧并进存钱罐", inv.Slots[c].IsEmpty && BankGeo() == 600 && BankStacks() == 1 && PlayerData.instance.geo == game0,
                $"移动 {moved} 组，{State()}");

            // 2. Geo in the bag (not the coin slot) too.
            yield return Reset();
            inv.Slots[Inventory.HotbarSize + 5] = ItemStack.Of(ItemType.Geo, 40);
            moved = Chests.QuickStackNearby(inv, hero.GetComponent<BoxCollider2D>().bounds.center);
            yield return new WaitForSeconds(0.3f);
            Check("背包的快速堆叠：背包格和钱币槽的吉欧都进去", inv.Slots[c].IsEmpty && inv.Slots[Inventory.HotbarSize + 5].IsEmpty && BankGeo() == 640 && BankStacks() == 1,
                $"移动 {moved} 组，{State()}");

            // 3. The bank open: its panel's Deposit All and Quick Stack.
            yield return Reset();
            InventoryGui.Open = true;
            bool opened = Stations.DevToggleBank(feet.center);
            yield return new WaitForSeconds(0.3f);
            Check("右键打开存钱罐", opened && Chests.BankOpen, $"打开 {opened}，存钱罐开着 {Chests.BankOpen}");
            Chests.QuickStack(inv);
            yield return new WaitForSeconds(0.3f);
            Check("存钱罐面板的快速堆叠", inv.Slots[c].IsEmpty && BankGeo() == 600 && BankStacks() == 1 && PlayerData.instance.geo == game0, State());

            yield return Reset();
            Chests.DepositAll(inv);
            yield return new WaitForSeconds(0.3f);
            Check("存钱罐面板的全部存入", inv.Slots[c].IsEmpty && BankGeo() == 600 && BankStacks() == 1 && PlayerData.instance.geo == game0, State());
            bool stayed = Chests.BankOpen;
            for (float t = 0f; t < 2f && stayed; t += Time.deltaTime) { yield return null; stayed = Chests.BankOpen; }
            Check("存钱罐开着不会自己关上", stayed, $"开着 {Chests.BankOpen}，背包开着 {InventoryGui.Open}");

            // 4. Shift+click on the coin slot.
            yield return Reset();
            bool openBefore = Chests.BankOpen;
            if (!Chests.BankOpen) Stations.DevToggleBank(feet.center);
            yield return null;
            bool openAtClick = Chests.BankOpen;
            InventoryGui.DevShift = true;
            string keys = InventoryGui.DevModifiers;
            InventoryGui.DevShift = false;
            InventoryGui.DevLeftClick(c, shift: true);
            string right = $"点完当帧：钱币槽 {(inv.Slots[c].IsEmpty ? "空" : inv.Slots[c].Count + (inv.Slots[c].Favorite ? "（收藏）" : ""))}，按键 {keys}";
            yield return new WaitForSeconds(0.3f);
            Check("Shift 点钱币槽", inv.Slots[c].IsEmpty && BankGeo() == 600 && BankStacks() == 1 && PlayerData.instance.geo == game0, $"之前开着 {openBefore}，点的时候开着 {openAtClick}，{right}，{State()}");

            // 5. A favourite coin slot stays.
            yield return Reset();
            inv.Slots[c].Favorite = true;
            Chests.QuickStack(inv);
            moved = Chests.QuickStackNearby(inv, hero.GetComponent<BoxCollider2D>().bounds.center);
            yield return new WaitForSeconds(0.3f);
            Check("收藏的吉欧不动", !inv.Slots[c].IsEmpty && inv.Slots[c].Count == 500 && BankGeo() == 100, State());
            inv.Slots[c].Favorite = false;

            // 6. A chest beside the bank, holding Geo itself: both quick stacks still send the Geo to the bank.
            Chests.Close(false);
            yield return null;
            var at = hero.GetComponent<BoxCollider2D>().bounds;
            bool chest = Chests.DevNearest(at.center) is Vector2 cn && Vector2.Distance(cn, at.center) < 6f;
            foreach (float dx in new[] { -1.5f, 1.5f, -2.5f, 2.5f, -3.5f, 3.5f, -4.5f, 4.5f })
            {
                if (chest) break;
                chest = Chests.TryPlace(hero, new Vector2(at.center.x + dx, at.min.y + 0.2f));
            }
            yield return new WaitForSeconds(0.3f);
            int ChestGeo() { var items = Chests.OpenItems; return items == null ? -1 : items.Where(s => !s.IsEmpty && s.Type == ItemType.Geo).Sum(s => s.Count); }
            bool chestOpen = chest && Chests.DevOpenNear(at.center);
            yield return null;
            if (chestOpen)
            {
                var items = Chests.OpenItems;
                for (int i = 0; i < items.Length; i++) items[i] = default;
                items[0] = ItemStack.Of(ItemType.Geo, 50);
            }
            yield return Reset();
            Chests.QuickStack(inv);
            yield return new WaitForSeconds(0.3f);
            Check("箱子开着、存钱罐在旁边：箱子的快速堆叠把吉欧存进存钱罐", chestOpen && inv.Slots[c].IsEmpty && BankGeo() == 600 && ChestGeo() == 50, $"箱子 {chest}/{chestOpen}，箱子吉欧 {ChestGeo()}，{State()}");
            yield return Reset();
            moved = Chests.QuickStackNearby(inv, hero.GetComponent<BoxCollider2D>().bounds.center);
            yield return new WaitForSeconds(0.3f);
            Check("箱子和存钱罐都在：快速堆叠到附近把吉欧存进存钱罐", chestOpen && inv.Slots[c].IsEmpty && BankGeo() == 600 && ChestGeo() == 50, $"移动 {moved} 组，箱子吉欧 {ChestGeo()}，{State()}");
            if (chestOpen)
            {
                var items = Chests.OpenItems;
                if (items != null) for (int i = 0; i < items.Length; i++) items[i] = default;
            }
            Chests.Close(false);

            Shot("Town", "geostack_bank");
            yield return null;
            Chests.Close(false);
            InventoryGui.Open = false;

            for (int i = 0; i < Inventory.Total; i++) inv.Slots[i] = savedBag[i];
            for (int i = 0; i < inv.Bank.Length; i++) inv.Bank[i] = savedBank[i];
            mod.SetInfiniteItems(wasInfinite);
            yield return new WaitForSeconds(0.3f);
            Plugin.Log.LogInfo($"[AutoTest] 吉欧存钱罐 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
