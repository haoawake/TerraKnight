using System;
using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "homeease"): the user's 10-29 "看看'哪都是家'你认为好用不？如果觉得反人类就优化一下". A home right-clicked in
    /// the world opens its storage; the bag closed and opened again, the home button goes straight back into the home opened
    /// last (another chosen meanwhile: that one); Deposit All puts the bag below the hotbar into the open home, the hotbar and
    /// favourites kept; the homes' chips carry their names (a picture of them, one name too long for its chip).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HomeEaseTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int pass = 0, total = 0;
            void Check(string name, bool ok, string detail = "")
            { total++; if (ok) pass++; Plugin.Log.LogInfo($"[AutoTest] HomeEase {name}：{detail} {(ok ? "通过" : "不通过")}"); }
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
                const float cs = CellGrid.CellSize;
                int gx = 60, gy = 22;
                for (int x = gx - 12; x <= gx + 18; x++) for (int y = gy; y <= gy + 9; y++) TerraWorld.Set(x, y, y == gy ? CellKind.Stone : CellKind.Empty);
                string here = TerraWorld.Live.SceneName;
                var origin = (Vector2)TerraWorld.Live.Root.position;
                var a = new Vector2((gx + 1.5f) * cs, (gy + 1) * cs) - origin;
                Stations.PlaceIn(StationKind.HomeEverywhere, here, a);
                var home = Stations.Homes().Single(); Stations.ChangeHome(home.Key, 22, "绿洲仓库");
                Stations.PlaceIn(StationKind.HomeEverywhere, "Crossroads_02", new Vector2(20, 6));
                var other = Stations.Homes().Last(); Stations.ChangeHome(other.Key, 49, "矿工营地");
                Stations.PlaceIn(StationKind.HomeEverywhere, "Crossroads_02", new Vector2(60, 6));
                var third = Stations.Homes().Last(); Stations.ChangeHome(third.Key, 5, "十字路口最深处的那个特别大的仓库");
                var a1 = a + Vector2.left * 2f; var a2 = a + Vector2.right * 2f;
                Chests.AddWorld(here, a1, 0, new ItemStack[40]); Chests.AddWorld(here, a2, 0, new ItemStack[40]);
                hero.transform.position = Clear(hero, origin + a + Vector2.left * 2f + Vector2.up * 2f);
                body.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(2f);

                // Right-clicked where it stands: its storage, not its colours.
                bool clicked = Stations.TryRightClick(origin + a + Vector2.up * 1.5f * cs);
                yield return new WaitForSeconds(.3f);
                Check("右键场景里的家园打开它的仓库", clicked && InventoryGui.Open && InventoryGui.HomeMergedVisible && Stations.SelectedHome == home.Key,
                    $"点中 {clicked}，背包 {InventoryGui.Open}，合并仓库 {InventoryGui.HomeMergedVisible}");

                // The bag closed and opened again: the home button goes straight back in.
                InventoryGui.Open = false; yield return new WaitForSeconds(.2f);
                Check("关背包断开区域但记得上次的", !Stations.HomeActive && Stations.LastHome == home.Key);
                InventoryGui.Open = true; yield return new WaitForSeconds(.3f);
                InventoryGui.DevHomeClick("HomeButton", 0); yield return new WaitForSeconds(.3f);
                Check("家园按钮直接回到上次的区域", InventoryGui.HomeMergedVisible && Stations.SelectedHome == home.Key);
                InventoryGui.DevHomeClick("HomeColor", 49); yield return new WaitForSeconds(.3f);
                InventoryGui.Open = false; yield return new WaitForSeconds(.2f);
                InventoryGui.Open = true; yield return new WaitForSeconds(.3f);
                InventoryGui.DevHomeClick("HomeButton", 0); yield return new WaitForSeconds(.3f);
                Check("换过区域后回到换过的那个", Stations.SelectedHome == other.Key);
                InventoryGui.DevHomeClick("HomeColor", 22); yield return new WaitForSeconds(.3f);

                // Deposit All: below the hotbar, favourites kept.
                inv.Slots[0] = ItemStack.Of(ItemType.Torch, 20);
                inv.Slots[12] = ItemStack.Of(ItemType.Wood, 50);
                inv.Slots[13] = ItemStack.Of(ItemType.Gel, 30); inv.Slots[13].Favorite = true;
                inv.Slots[20] = ItemStack.Of(ItemType.IronBar, 10);
                bool deposit = InventoryGui.DevHomeClick("HomeAction", 11);
                yield return new WaitForSeconds(.3f);
                int wood = Chests.UnifiedHomeSlots(home.Key, "Wood").Sum(s => s.Item.Count);
                int bars = Chests.UnifiedHomeSlots(home.Key, "IronBar").Sum(s => s.Item.Count);
                Check("全部存入只存快捷栏以下、没收藏的", deposit && inv.Slots[12].IsEmpty && inv.Slots[20].IsEmpty && wood == 50 && bars == 10
                    && inv.Slots[0].Type == ItemType.Torch && inv.Slots[13].Type == ItemType.Gel,
                    $"按钮 {deposit}，木材 {wood}，铁锭 {bars}，快捷栏 {inv.Slots[0].Type}，收藏 {inv.Slots[13].Type}");
                var chip = InventoryGui.DevRectOf("HomeColor", 22);
                Check("家园色块够放名字（一行四个）", chip.HasValue && chip.Value.width > 100f * UI.GameView.Scale, chip.HasValue ? $"宽 {chip.Value.width:F0}" : "没有");
                Shot(here, "home_ease_chips"); yield return new WaitForSeconds(.2f);
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
            Plugin.Log.LogInfo($"[AutoTest] HomeEase 合计 {pass}/{total}");
        }
    }
}
