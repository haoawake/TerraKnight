using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator HomeUnifiedTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int pass = 0, total = 0;
            void Check(string name, bool ok, string detail = "")
            { total++; if (ok) pass++; Plugin.Log.LogInfo($"[AutoTest] HomeUnified {name}：{detail} {(ok ? "通过" : "不通过")}"); }
            Player.TerraStats.DevImmortal = true; PlayerData.instance.isInvincible = true; mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1"); yield return new WaitForSeconds(3f);
            var hero = HeroController.instance; var inv = mod.Inventory;
            var body = hero.GetComponent<Rigidbody2D>(); var constraints = body.constraints;
            var oldStations = Stations.Save(); var oldChests = Chests.Save(); var oldSlots = (ItemStack[])inv.Slots.Clone();
            int stage = Shop.DevStage; bool infinite = inv.Infinite;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            Player.TerraControls.DevNoRealKeys = true; WorldItems.DevNoGrab = true;
            try
            {
                Stations.Clear(); Chests.Clear(); inv.Clear(); inv.Mouse = default;
                inv.Infinite = false; TerraMod.DevAllInfinite = false; mod.ApplyCreative(false); Shop.DevStage = 5;
                for (int k = 0; k < 4; k++) Array.Clear(inv.BankOf(k), 0, inv.BankOf(k).Length);
                const float cs = CellGrid.CellSize;
                int gx = 60, gy = 22;
                for (int x = gx - 12; x <= gx + 18; x++) for (int y = gy; y <= gy + 9; y++) TerraWorld.Set(x, y, y == gy ? CellKind.Stone : CellKind.Empty);
                string here = TerraWorld.Live.SceneName;
                var origin = (Vector2)TerraWorld.Live.Root.position;
                var a = new Vector2((gx + 1.5f) * cs, (gy + 1) * cs) - origin;
                Stations.PlaceIn(StationKind.HomeEverywhere, here, a);
                var home = Stations.Homes().Single(); Stations.ChangeHome(home.Key, 22, "绿洲仓库"); home = Stations.Home(home.Key);
                const string far = "Crossroads_02"; var b = new Vector2(20, 6);
                Stations.PlaceIn(StationKind.HomeEverywhere, far, b);
                var other = Stations.Homes().Last(); Stations.ChangeHome(other.Key, 49, "矿工营地");
                var a1 = a + Vector2.left * 2f; var a2 = a + Vector2.right * 2f;
                Chests.AddWorld(here, a1, 0, new ItemStack[40]); Chests.AddWorld(here, a2, 0, new ItemStack[40]);
                Chests.AddWorld(far, b, 0, new ItemStack[40]);
                Stations.PlaceIn(StationKind.PiggyBank, here, a + Vector2.right * 4f);
                Stations.PlaceIn(StationKind.IronAnvil, here, a + Vector2.left * 4f);
                Stations.PlaceIn(StationKind.WorkBench, far, b + Vector2.right * 3f);
                var first = Chests.DevItemsAt(here, a1); var second = Chests.DevItemsAt(here, a2); var outside = Chests.DevItemsAt(far, b);
                first[0] = ItemStack.Of(ItemType.Wood, 40); first[1] = ItemStack.Of(ItemType.StoneBlock, 18);
                second[0] = ItemStack.Of(ItemType.Wood, 70); second[1] = ItemStack.Of(ItemType.Gel, 9);
                second[2] = ItemStack.Of(ItemType.CopperShortsword); second[2].Prefix = 1; second[2].Forge = 2;
                inv.Bank[0] = ItemStack.Of(ItemType.Torch, 11); outside[0] = ItemStack.Of(ItemType.Wood, 999);
                hero.transform.position = Clear(hero, origin + a + Vector2.left * 2f + Vector2.up * 2f);
                body.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(2f);
                inv.Clear(); inv.Mouse = default; InventoryGui.Open = true;
                yield return new WaitForSeconds(.4f);
                var icon = InventoryGui.DevRectOf("HomeButton", 0); var stats = InventoryGui.DevRectOf("StatsButton", 0);
                Check("入口与属性仪同尺寸且并排", icon.HasValue && stats.HasValue && Mathf.Abs(icon.Value.width - stats.Value.width) < .01f
                    && Mathf.Abs(icon.Value.height - stats.Value.height) < .01f && Mathf.Abs(icon.Value.y - stats.Value.y) < .01f && icon.Value.x > stats.Value.xMax);
                var texture = HomeEverywhereArt.Texture(false);
                Check("入口贴图透明边缘", texture.GetPixels32().Count(p => p.a == 0) > texture.width * texture.height / 10);
                Check("入口真实点击", InventoryGui.DevHomeClick("HomeButton", 0));
                yield return new WaitForSeconds(.2f);
                Check("色块真实点击", InventoryGui.DevHomeClick("HomeColor", 22));
                yield return new WaitForSeconds(.25f);
                Check("统一容量为三个真实储物空间", Chests.HomeStorages(home.Key).Sum(s => s.Items.Length) == 120);
                Check("只显示总格子，不再列出独立箱子", InventoryGui.HomeMergedVisible && InventoryGui.DevRectOf("HomeSlot", 0).HasValue && !InventoryGui.DevRectOf("HomeStorage", 0).HasValue && !Chests.IsOpen);
                Check("名称搜索汇集两个箱子的同种物品", Chests.UnifiedHomeSlots(home.Key, ItemDb.Get(ItemType.Wood).Name).Single().Item.Count == 110);
                Check("搜索不混入其他区域", Chests.UnifiedHomeSlots(home.Key, "Wood").All(s => s.Item.Count != 999));
                InventoryGui.DevHomeSearch("Wood"); yield return new WaitForSeconds(.15f);
                Check("同种物品搜索界面只显示一个合计格", InventoryGui.DevHomeSlotCount == 1 && !InventoryGui.DevRectOf("HomeSlot", 1).HasValue);
                InventoryGui.DevHomeSlotClick(0, true); yield return new WaitForSeconds(.15f);
                Check("右键从合计物品取一个", inv.Mouse.Type == ItemType.Wood && inv.Mouse.Count == 1 && first[0].Count == 39 && second[0].Count == 70);
                Chests.PutHome(); yield return new WaitForSeconds(.15f);
                InventoryGui.DevHomeSlotClick(0); yield return new WaitForSeconds(.15f);
                Check("左键自动跨两个箱子取出合计库存", inv.Mouse.Type == ItemType.Wood && inv.Mouse.Count == 110 && first[0].IsEmpty && second[0].IsEmpty);
                Check("合并存入手中物品", Chests.PutHome() && inv.Mouse.IsEmpty && Chests.UnifiedHomeSlots(home.Key, "Wood").Single().Item.Count == 110);
                second[0] = first[0]; first[0] = default;
                Check("未知名称没有结果", Chests.UnifiedHomeSlots(home.Key, "不存在的物品000").Count == 0);
                InventoryGui.DevHomeSearch(""); yield return new WaitForSeconds(.15f);
                Check("搜索框能聚焦", InventoryGui.DevHomeClick("HomeSearch", 0) && InventoryGui.TypingName);
                yield return null;
                InputSystem.QueueTextEvent(Keyboard.current, '木'); yield return new WaitForSeconds(.15f);
                Check("中文输入搜索", InventoryGui.DevHomeSearchText == "木" && InventoryGui.DevHomeSlotCount > 0 && InventoryGui.DevHomeSlotCount < 120);
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Backspace)); yield return null; yield return null;
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); yield return new WaitForSeconds(.1f);
                Check("搜索退格清空", InventoryGui.DevHomeSearchText.Length == 0);
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Enter)); yield return null; yield return null;
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); yield return new WaitForSeconds(.1f);
                Check("回车只结束搜索输入", !InventoryGui.TypingName && InventoryGui.HomeMergedVisible);
                var weapon = Chests.UnifiedHomeSlots(home.Key, "CopperShortsword").Single();
                Chests.MoveHomeSlot(weapon, 0);
                Check("武器前缀锻造取出保持", inv.Mouse.Prefix == 1 && inv.Mouse.Forge == 2 && second[2].IsEmpty);
                Chests.PutHome();
                Check("武器前缀锻造存入保持", Chests.HomeStorages(home.Key).Any(s => s.Items.Any(i => i.Type == ItemType.CopperShortsword && i.Prefix == 1 && i.Forge == 2)));
                var wood = Chests.UnifiedHomeSlots(home.Key, "Wood").Single();
                var expected = wood.Item;
                var request = new Net.WorldSync.Action { Op = "homeSlot", Home = other.Key, Key = wood.Storage.Key, Kind = wood.Index, Power = 0,
                    Slots = new[] { Net.WorldSync.Stack.From(default), Net.WorldSync.Stack.From(expected) } };
                Check("房主拒绝跨区域取物", Chests.NetHomeMove(request, k => false) == null && second[0].Count == 110);
                request.Home = home.Key;
                Check("房主拒绝正在被别人使用的箱子", Chests.NetHomeMove(request, k => true) == null && second[0].Count == 110);
                var wrong = expected; wrong.Count--;
                request.Slots[1] = Net.WorldSync.Stack.From(wrong);
                Check("拒绝过期格子避免搜索重排拿错", Chests.NetHomeMove(request, k => false) == null && second[0].Count == 110);
                request.Slots[1] = Net.WorldSync.Stack.From(expected);
                var taken = Chests.NetHomeMove(request, k => false);
                Check("房主原子取物", taken != null && taken[0].Count == 110 && second[0].IsEmpty);
                request.Power = 3; request.Slots = taken;
                var returned = Chests.NetHomeMove(request, k => false);
                Check("房主原子存回且不复制", returned != null && returned[0].Count == 0 && Chests.HomeStorages(home.Key).Sum(s => s.Items.Where(i => i.Type == ItemType.Wood).Sum(i => i.Count)) == 110);
                inv.Mouse = ItemStack.Of(ItemType.Gel, 7);
                var refund = new Net.WorldSync.Action { Power = 3, Slots = new[] { Net.WorldSync.Stack.From(inv.Mouse) } };
                inv.Mouse = default; Chests.CompleteHomeMove(refund, false, null);
                Check("拒绝或断线退回手中物品", inv.Mouse.Type == ItemType.Gel && inv.Mouse.Count == 7);
                inv.Mouse = default;
                Array.Clear(first, 0, first.Length); Array.Clear(second, 0, second.Length); Array.Clear(inv.Bank, 0, inv.Bank.Length);
                for (int i = 0; i < first.Length; i++) first[i] = ItemStack.Of(ItemType.StoneBlock, ItemDb.Get(ItemType.StoneBlock).MaxStack);
                inv.Mouse = ItemStack.Of(ItemType.Wood, 27); Chests.PutHome();
                Check("第一个箱子满后自动存入第二个", inv.Mouse.IsEmpty && second.Any(i => i.Type == ItemType.Wood && i.Count == 27));
                inv.Slots[10] = ItemStack.Of(ItemType.Gel, 88); Chests.PutHome(10);
                Check("背包整叠存入合并仓库", inv.Slots[10].IsEmpty && second.Any(i => i.Type == ItemType.Gel && i.Count == 88));
                Check("区域外库存保持", outside[0].Count == 999);
                Array.Clear(first, 0, first.Length); Array.Clear(second, 0, second.Length);
                var recipe = Recipes.All.First(r => !r.IsBought && r.Station == "砧" && r.Ingredients.Length == 1 && r.Def.MaxStack == 1 && r.Ingredients.All(g => ItemDb.Get(g.Type).MaxStack >= g.Count * 5));
                var ingredient = recipe.Ingredients[0]; first[0] = ItemStack.Of(ingredient.Type, ingredient.Count * 3);
                InventoryGui.DevHomeSelect(home.Key); yield return new WaitForSeconds(.15f);
                Check("统一仓库制作直接扣真实库存", InventoryGui.DevCraftRecipe(recipe) == null && first[0].Count == ingredient.Count * 2);
                inv.Mouse = default; InventoryGui.DevHomeSelect(other.Key);
                Check("其他区域不能借用铁砧", !InventoryGui.DevOffered().Contains(recipe.Result));
                InventoryGui.DevHomeSelect(home.Key);
                yield return new WaitForSeconds(1f);
                var boundaries = UnityEngine.Object.FindObjectsByType<SoftHomeBoundary>(FindObjectsSortMode.None);
                Check("范围边界为实际粒子系统", boundaries.Length >= 1 && boundaries.Any(p => p.GetComponent<ParticleSystem>().particleCount > 0));
                Check("没有刺眼的连续光框", !UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(l => l.name == "Home Everywhere Aura"));
                Check("泰拉瑞亚原生框素材存在", Terraria.TerrariaAssets.Texture("UI/PanelBorder").width > 8 && Terraria.TerrariaAssets.Texture("UI/PanelBackground").width > 8);
                Array.Clear(first, 0, first.Length); Array.Clear(second, 0, second.Length); Array.Clear(inv.Bank, 0, inv.Bank.Length);
                first[20] = ItemStack.Of(ItemType.Wood, 23); second[30] = ItemStack.Of(ItemType.Wood, 37);
                inv.Bank[25] = ItemStack.Of(ItemType.Wood, 9);
                first[35] = ItemStack.Of(ItemType.CopperShortsword); first[35].Prefix = 1; first[35].Forge = 2;
                second[35] = ItemStack.Of(ItemType.CopperShortsword); second[35].Prefix = 2; second[35].Forge = 3;
                InventoryGui.DevHomeSearch("铁锭"); yield return new WaitForSeconds(.15f);
                Check("原生排序按钮可点击", InventoryGui.DevHomeClick("HomeAction", 10));
                var sorted = Chests.UnifiedHomeSlots(home.Key, "").Where(s => !s.Item.IsEmpty).Select(s => s.Item).ToArray();
                Check("排序跨箱和银行合并整区库存", sorted.Count(s => s.Type == ItemType.Wood) == 1 && sorted.Single(s => s.Type == ItemType.Wood).Count == 69);
                Check("排序包含筛选之外的全部物品", InventoryGui.DevHomeSearchText == "铁锭" && sorted.Length == 3 && first[0].Type == ItemType.CopperShortsword && first[20].IsEmpty && second[30].IsEmpty && inv.Bank[25].IsEmpty);
                Check("排序保留不同词缀和锻造", sorted.Any(s => s.Prefix == 1 && s.Forge == 2) && sorted.Any(s => s.Prefix == 2 && s.Forge == 3));
                Check("排序不影响其他区域", outside[0].Type == ItemType.Wood && outside[0].Count == 999);
                Check("房主排序拒绝被占用的箱子", !Chests.NetSortHome(home.Key, key => true));
                Check("房主排序拒绝不存在的区域", !Chests.NetSortHome("missing", key => false));
                Check("房主可执行区域排序", Chests.NetSortHome(home.Key, key => false));
                Array.Clear(first, 0, first.Length); Array.Clear(second, 0, second.Length); Array.Clear(inv.Bank, 0, inv.Bank.Length);
                int stackMax = ItemDb.Get(ItemType.Wood).MaxStack;
                first[0] = ItemStack.Of(ItemType.Wood, stackMax); second[0] = ItemStack.Of(ItemType.Wood, 70); inv.Bank[0] = ItemStack.Of(ItemType.Wood, 4);
                var combined = Chests.UnifiedHomeSlots(home.Key, "Wood");
                Check("超过原版堆叠上限仍显示一堆总数", combined.Count == 1 && combined[0].Item.Count == stackMax + 74);
                Chests.MoveHomeSlot(combined[0], 0);
                Check("取出保持玩家单堆上限且余量保留", inv.Mouse.Count == stackMax && first[0].IsEmpty && Chests.UnifiedHomeSlots(home.Key, "Wood").Single().Item.Count == 74);
                Chests.PutHome();
                Check("大堆存回自动分配原箱格子", inv.Mouse.IsEmpty && Chests.UnifiedHomeSlots(home.Key, "Wood").Single().Item.Count == stackMax + 74 && Chests.HomeStorages(home.Key).All(s => s.Items.All(i => i.IsEmpty || i.Count <= i.Def.MaxStack)));
                Array.Clear(first, 0, first.Length); Array.Clear(second, 0, second.Length); Array.Clear(inv.Bank, 0, inv.Bank.Length);
                first[0] = ItemStack.Of(ItemType.Wood, 15); second[0] = ItemStack.Of(ItemType.Wood, 25); inv.Bank[0] = ItemStack.Of(ItemType.Wood, 4);
                var beforeBag = inv.Slots.Where(s => s.Type == ItemType.Wood).Sum(s => s.Count);
                Chests.MoveHomeSlot(Chests.UnifiedHomeSlots(home.Key, "Wood").Single(), 2);
                Check("Shift取出跨箱和银行且不复制", inv.Mouse.IsEmpty && inv.Slots.Where(s => s.Type == ItemType.Wood).Sum(s => s.Count) == beforeBag + 44 && Chests.UnifiedHomeSlots(home.Key, "Wood").Count == 0);
                first[0] = ItemStack.Of(ItemType.Wood, 15); second[0] = ItemStack.Of(ItemType.Wood, 25);
                var groupAction = new Net.WorldSync.Action { Home = home.Key, Power = 4, Kind = 0, Targets = new[] { stackMax }, Slots = new[] { Net.WorldSync.Stack.From(default), Net.WorldSync.Stack.From(ItemStack.Of(ItemType.Wood, 40)) } };
                var groupResult = Chests.NetHomeMove(groupAction, key => false);
                Check("房主按物品类型原子跨箱取用", groupResult != null && groupResult[0].Count == 40 && first[0].IsEmpty && second[0].IsEmpty);
                first[0] = ItemStack.Of(ItemType.Wood, 15); second[0] = ItemStack.Of(ItemType.Wood, 25);
                groupResult = Chests.NetHomeMove(groupAction, key => key == Chests.UnifiedHomeSlots(home.Key, "Wood").Single().Storage.Key);
                Check("房主合堆取用跳过占用箱子", groupResult != null && groupResult[0].Count == 25 && first[0].Count == 15 && second[0].IsEmpty);
                Array.Clear(first, 0, first.Length); Array.Clear(second, 0, second.Length); Array.Clear(inv.Bank, 0, inv.Bank.Length);
                var samples = new[] { ItemType.Wood, ItemType.StoneBlock, ItemType.Gel, ItemType.IronBar, ItemType.GoldBar, ItemType.SilverBar, ItemType.Torch, ItemType.Meteorite, ItemType.LunarOre, ItemType.Hellstone, ItemType.CopperShortsword, ItemType.SDMG, ItemType.Tsunami, ItemType.Phantasm, ItemType.HealingPotion, ItemType.ManaPotion };
                for (int i = 0; i < 28; i++) { var type = samples[i % samples.Length]; first[i] = ItemStack.Of(type, Mathf.Min(23 + i * 13, ItemDb.Get(type).MaxStack)); }
                second[0] = ItemStack.Of(ItemType.IronBar, 330); inv.Bank[0] = ItemStack.Of(ItemType.Torch, 55);
                InventoryGui.DevHomeSearch(""); yield return new WaitForSeconds(.4f); Shot(here, "home_unified_native_frame"); yield return new WaitForSeconds(.15f);
                InventoryGui.DevHomeSearch(ItemDb.Get(ItemType.IronBar).Name); yield return new WaitForSeconds(.3f); Shot(here, "home_unified_search"); yield return new WaitForSeconds(.15f);
                InventoryGui.DevHomeSearch("");
                Screen.SetResolution(1024, 768, false); yield return new WaitForSeconds(.7f);
                var panel = InventoryGui.DevRectOf("HomePanel", 0);
                Check("小分辨率窗口不越界", panel.HasValue && panel.Value.xMin >= 0 && panel.Value.yMin >= 0 && panel.Value.xMax <= Screen.width && panel.Value.yMax <= Screen.height);
                Shot(here, "home_unified_1024"); yield return new WaitForSeconds(.15f);
                InventoryGui.Open = false; yield return new WaitForSeconds(.4f); Shot(here, "home_soft_boundary"); yield return new WaitForSeconds(.15f);
                Check("关闭背包结束区域连接", !Stations.HomeActive && !InventoryGui.TypingName);
                var save = Chests.Save(); Chests.Load(save);
                Check("合并视图没有替换真实箱子保存结构", Chests.DevItemsAt(here, a2)[0].Type == ItemType.IronBar && Chests.DevItemsAt(here, a2)[0].Count == 330);
            }
            finally
            {
                InventoryGui.Open = false; InventoryGui.DevHomeSearch("");
                Stations.Load(oldStations); Chests.Load(oldChests);
                for (int i = 0; i < oldSlots.Length; i++) inv.Slots[i] = oldSlots[i];
                inv.Mouse = default; inv.Infinite = infinite; Shop.DevStage = stage;
                body.constraints = constraints; Player.TerraControls.DevNoRealKeys = false; WorldItems.DevNoGrab = false;
                Player.TerraStats.DevImmortal = false; mod.DevNoFallDamage = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] HomeUnified 合计 {pass}/{total}");
        }
    }
}
