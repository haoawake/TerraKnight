using System;
using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "homestack", the user's 10-27 "检查用法以及背包管理是否可行，并能够一键快速堆叠到多个颜色背包内"): two Home
    /// Everywheres of two colours, one here (two chests and a Piggy Bank in it) and one in another scene (one chest), a chest
    /// outside both. The bag (a hotbar stack, stacks the homes' chests hold, a favourite, one nobody holds, geo) quick-stacked
    /// by the bag's new button: each stack onto the home chest that holds it, the hotbar, the favourite and the unheld stay,
    /// the geo into the Piggy Bank, the outside chest untouched. From another scene, by the home panel's "一键堆叠": onto the
    /// homes' chests with their scenes unloaded. The host's side of a LAN quick stack (Chests.NetQuickStack) from a player in
    /// no scene of theirs: a home's chest takes it, the outside one doesn't. Lines "[AutoTest] 家园堆叠 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HomeStackTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Private copy required");
            int ok = 0, all = 0;
            void Check(string name, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 家园堆叠 {name}：{detail} {(pass ? "通过" : "不通过")}");
            }
            int stageWas = Shop.DevStage;
            bool infiniteWas = TerraMod.DevAllInfinite;
            TerraMod.DevAllInfinite = false;
            mod.ApplyCreative(false);
            Shop.DevStage = 5;
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            try
            {
                yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
                yield return new WaitForSeconds(2f);
                var hero = HeroController.instance;
                var inv = mod.Inventory;
                inv.Infinite = false;
                Stations.Clear();
                Chests.Clear();
                float cs = CellGrid.CellSize;
                var cell = CellGrid.WorldToCell(new Vector2(30f, 12f));
                int gx = cell.x + 3, gy = cell.y - 1;
                for (int x = gx - 4; x <= gx + 40; x++) for (int y = gy; y <= gy + 9; y++) TerraWorld.Set(x, y, y == gy ? CellKind.Stone : CellKind.Empty);
                yield return new WaitForSeconds(.5f);
                hero.transform.position = Clear(hero, CellGrid.CellCenter(gx - 2, gy + 3));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1f);
                string here = GameManager.instance.sceneName;
                // Home A here, placed as the player places it; home B in another scene (its chest there, that scene not loaded).
                inv.Selected = 0;
                inv.Override = -1;
                inv.Slots[0] = ItemStack.Of(ItemType.HomeEverywhere, 1);
                mod.DevUseSelected(new Vector2((gx + 1.5f) * cs, (gy + 1) * cs + .2f));
                yield return new WaitForSeconds(.5f);
                var homeA = Stations.Homes().FirstOrDefault();
                if (homeA == null) { Check("放下家园", false, Stations.DevWhy ?? ""); yield break; }
                var aLocal = homeA.Local;
                const string Far = "Crossroads_02";
                var bLocal = new Vector2(20f, 6f);
                Stations.PlaceIn(StationKind.HomeEverywhere, Far, bLocal);
                var homeB = Stations.Homes().Last();
                Check("两个家园两种颜色", homeA != null && homeB != null && homeA.Key != homeB.Key && homeA.Color != homeB.Color, $"{homeA?.Color} / {homeB?.Color}，乙在 {homeB?.Scene}");
                var a1 = new ItemStack[40]; a1[0] = ItemStack.Of(ItemType.Wood, 5);
                var a2 = new ItemStack[40]; a2[3] = ItemStack.Of(ItemType.Gel, 3);
                var b1 = new ItemStack[40]; b1[0] = ItemStack.Of(ItemType.Wood, 2); b1[1] = ItemStack.Of(ItemType.Torch, 1);
                var outside = new ItemStack[40]; outside[0] = ItemStack.Of(ItemType.Gel, 10);
                Chests.AddWorld(here, aLocal + Vector2.left * (4 * cs), 0, a1);
                Chests.AddWorld(here, aLocal + Vector2.right * (4 * cs), 0, a2);
                Stations.PlaceIn(StationKind.PiggyBank, here, aLocal + Vector2.right * (7 * cs));
                Chests.AddWorld(here, aLocal + Vector2.right * (16 * cs), 0, outside);
                Chests.AddWorld(Far, bLocal + Vector2.left * (4 * cs), 0, b1);
                // The chests' own slots (AddWorld copies the arrays given).
                a1 = Chests.DevItemsAt(here, aLocal + Vector2.left * (4 * cs));
                a2 = Chests.DevItemsAt(here, aLocal + Vector2.right * (4 * cs));
                outside = Chests.DevItemsAt(here, aLocal + Vector2.right * (16 * cs));
                b1 = Chests.DevItemsAt(Far, bLocal + Vector2.left * (4 * cs));
                yield return new WaitForSeconds(.3f);
                Check("仓储接到各自家园", Chests.HomeStorages(homeA.Key).Count == 3 && Chests.HomeStorages(homeB.Key).Count == 1,
                    $"甲 {Chests.HomeStorages(homeA.Key).Count}（两个箱子和储钱罐），乙 {Chests.HomeStorages(homeB.Key).Count}");

                Array.Clear(inv.Slots, 0, inv.Slots.Length);
                inv.Mouse = default;
                for (int k = 0; k < 4; k++) Array.Clear(inv.BankOf(k), 0, inv.BankOf(k).Length);
                inv.Slots[0] = ItemStack.Of(ItemType.Wood, 50);
                inv.Slots[10] = ItemStack.Of(ItemType.Wood, 30);
                inv.Slots[11] = ItemStack.Of(ItemType.Gel, 20);
                var torch = ItemStack.Of(ItemType.Torch, 5);
                torch.Favorite = true;
                inv.Slots[12] = torch;
                inv.Slots[13] = ItemStack.Of(ItemType.DirtBlock, 40);
                inv.Slots[Inventory.AmmoEnd] = ItemStack.Of(ItemType.Geo, 120);
                long geo0 = Currencies.Held(inv, ItemType.Geo);
                InventoryGui.Open = true;
                yield return new WaitForSeconds(.5f);
                Check("背包旁出现「快速堆叠至全部家园」按钮", InventoryGui.DevRectOf("InvButton", 2).HasValue);
                Shot(here, "homestack_button");
                bool clicked = InventoryGui.DevHomeClick("InvButton", 2);
                yield return new WaitForSeconds(.3f);
                int Count(ItemStack[] items, ItemType t) => items.Where(s => !s.IsEmpty && s.Type == t).Sum(s => s.Count);
                Check("一键堆叠：木材进甲区已有木材的箱子", clicked && Count(a1, ItemType.Wood) == 35 && inv.Slots[10].IsEmpty, $"甲1 木材 {Count(a1, ItemType.Wood)}，乙1 木材 {Count(b1, ItemType.Wood)}");
                Check("凝胶进甲区已有凝胶的箱子，区外箱子不动", Count(a2, ItemType.Gel) == 23 && Count(outside, ItemType.Gel) == 10 && inv.Slots[11].IsEmpty, $"甲2 {Count(a2, ItemType.Gel)}，区外 {Count(outside, ItemType.Gel)}");
                Check("快捷栏、收藏、没人放的物品留在背包", inv.Slots[0].Count == 50 && inv.Slots[12].Count == 5 && inv.Slots[13].Count == 40 && Count(b1, ItemType.Torch) == 1);
                Check("吉欧进家园里的储钱罐", Currencies.Held(inv, ItemType.Geo) == 0 && Currencies.Banked(inv, ItemType.Geo) == geo0, $"背包 {Currencies.Held(inv, ItemType.Geo)}，储钱罐 {Currencies.Banked(inv, ItemType.Geo)}");
                Check("一次走了两个家园的仓储", Chests.DevHomeStackTargets == 4, $"{Chests.DevHomeStackTargets} 个（储钱罐、甲两个箱子、乙一个箱子）");

                // From another scene, by the home panel: the homes' scenes unloaded.
                InventoryGui.Open = false;
                yield return GoTo("Town", EntryGateOf("Town"));
                yield return new WaitForSeconds(1.5f);
                inv.Slots[10] = ItemStack.Of(ItemType.Gel, 15);
                inv.Slots[14] = ItemStack.Of(ItemType.Torch, 4);
                InventoryGui.Open = true;
                yield return new WaitForSeconds(.3f);
                InventoryGui.DevHomeClick("HomeButton", 0);
                yield return new WaitForSeconds(.3f);
                bool panelButton = InventoryGui.DevRectOf("HomeAction", 7).HasValue;
                Shot("Town", "homestack_panel");
                // Every stack of the bag below the hotbar goes, whatever came into it meanwhile.
                int BagGel() => inv.Slots.Skip(Inventory.HotbarSize).Take(Inventory.Size - Inventory.HotbarSize).Where(x => !x.IsEmpty && !x.Favorite && x.Type == ItemType.Gel).Sum(x => x.Count);
                int gelBag = BagGel(), gelA2 = Count(a2, ItemType.Gel);
                InventoryGui.DevHomeClick("HomeAction", 7);
                yield return new WaitForSeconds(.3f);
                Check("家园面板「一键堆叠」：在别处也堆进两个家园", panelButton && Count(a2, ItemType.Gel) == gelA2 + gelBag && BagGel() == 0 && Count(b1, ItemType.Torch) == 5 && inv.Slots[14].IsEmpty,
                    $"按钮 {panelButton}，甲2 凝胶 {gelA2}+{gelBag}→{Count(a2, ItemType.Gel)}，乙1 火把 {Count(b1, ItemType.Torch)}");

                // The host's side: a player in none of these scenes.
                string keyB = Chests.HomeStorages(homeB.Key)[0].Key;
                string keyOut = Net.WorldSync.Key("c", here, aLocal + Vector2.right * (16 * cs));
                var ask = new Net.WorldSync.Action { Keys = new[] { keyB, keyOut }, Slots = new[] { Net.WorldSync.Stack.From(ItemStack.Of(ItemType.Wood, 6)), Net.WorldSync.Stack.From(ItemStack.Of(ItemType.Gel, 4)) }, Targets = new[] { -1, -1 } };
                int woodB = Count(b1, ItemType.Wood), gelOut = Count(outside, ItemType.Gel);
                var left = Chests.NetQuickStack(ask, 7, k => false, out int movedNet);
                Check("联机房主：家园里的箱子不在同一场景也收", left != null && Count(b1, ItemType.Wood) == woodB + 6 && (left[0] == null || left[0].Item().IsEmpty), $"乙1 木材 {woodB}→{Count(b1, ItemType.Wood)}");
                Check("联机房主：家园外的远处箱子照旧不收", left != null && Count(outside, ItemType.Gel) == gelOut && left[1] != null && left[1].Item().Count == 4, $"区外凝胶 {gelOut}→{Count(outside, ItemType.Gel)}");
                InventoryGui.Open = false;
            }
            finally
            {
                Shop.DevStage = stageWas;
                TerraMod.DevAllInfinite = infiniteWas;
                Player.TerraStats.DevImmortal = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 家园堆叠 合计 {ok}/{all}");
        }
    }
}
