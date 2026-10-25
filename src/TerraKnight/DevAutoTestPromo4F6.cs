using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "p4_v5F_build_zh"): recorder F's building takes on Dirtmouth's west street. A wall of five areas' blocks
    /// laid two columns at a time; a stair of area platforms up to a balcony of another area's, walked up; a patch of dirt
    /// swapped block by block for Queen's Gardens stone by the best pickaxe carried, and a wood wall for stone wall;
    /// the smart cursor's interact outlining three area chests, the nearest in gold, opened by a right click.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator P4_v5F_build(HeroController hero)
        {
            F5Safe();
            StoryBeats.SetOn(false);
            yield return P4Kit();
            F5Safe();
            mod.TrySwitchLoadout(0);
            mod.DevSetSmartCursor(false);
            yield return F5TownReady();
            yield return F5AreaBlocks();
            yield return F5AreaPlatforms();
            yield return F5BlockSwap();
            yield return F5SmartInteract();
            F5CamOff();
        }

        /// <summary>The game's mouse put on a point of the world (a queued pointer state), so what follows the mouse (the smart cursor's box) is there.</summary>
        static void F5Mouse(Vector2 world)
        {
            var m = Mouse.current;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (m == null || cam == null) return;
            var sp = cam.WorldToScreenPoint(world);
            InputSystem.QueueStateEvent(m, new MouseState { position = new Vector2(sp.x, sp.y) });
        }

        /// <summary>The hotbar for building: these items in its first slots (the rest of the bag kept), each a full stack.</summary>
        ItemStack[] F5Hotbar(params ItemType[] items)
        {
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            for (int i = 0; i < Inventory.HotbarSize; i++) inv.Slots[i] = i < items.Length ? ItemStack.Of(items[i], Mathf.Max(1, Mathf.Min(250, ItemDb.Get(items[i]).MaxStack))) : saved[i];
            inv.Selected = 0;
            return saved;
        }

        IEnumerator F5AreaBlocks()
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            int[] areas = { 4, 10, 8, 7, 18 };
            int x0 = CellGrid.WorldToCell(new Vector2(66f, 10f)).x;
            int g = F5GroundRow(CellGrid.CellCenter(x0, 0).x, TownNpcs.DevStreet.y + 1f);
            F5Shoo(50f, 104f, 1, 12f);
            var items = areas.Select(a => AreaBlocks.BlockItem(a)).ToList();
            items.Add(RegionalTorches.ItemOf(RegionalTorchAreas.StyleOf("Town")));
            var saved = F5Hotbar(items.ToArray());
            int accSlot = Inventory.AccessorySlots - 1;
            var savedAcc = inv.Accessories[accSlot];
            inv.Accessories[accSlot] = ItemStack.Of(ItemType.ArchitectGizmoPack, 1);
            inv.RefreshEffective();
            Equipment.Update(inv);
            var boots = F5NoBoots();
            var col = hero.GetComponent<BoxCollider2D>();
            // Columns two wide, four high: areas 0 and 1 left of a two-cell gap (x0+4, x0+5), 2..4 right of it.
            int[] colX = { x0, x0 + 2, x0 + 6, x0 + 8, x0 + 10 };
            C4PutOn(hero, new Vector2Int(x0 - 4, g + 1));
            C4Face(hero, 1);
            F5CamOn(Vector2.zero);
            f5CamFix = new Vector2(CellGrid.CellCenter(x0 + 5, 0).x, CellGrid.CellCenter(0, g).y + 3.6f);
            yield return new WaitForSeconds(0.8f);
            F5CamSnap();
            aimInit = false;
            SmoothAim(CellGrid.CellCenter(x0 + 3, g + 2), 99f);
            int laid = 0;
            StartRec("v5F_area_blocks");
            yield return new WaitForSeconds(0.3f);
            yield return F5WalkTo(hero, (CellGrid.CellCenter(x0 + 4, 0).x + CellGrid.CellCenter(x0 + 5, 0).x) / 2f, 2f, () => CellGrid.CellCenter(x0 + 3, g + 2));
            foreach (int k in new[] { 1, 0, 2, 3, 4 })
            {
                inv.Selected = k;
                C4Face(hero, colX[k] > x0 + 4 ? 1 : -1);
                yield return new WaitForSeconds(0.1f);
                for (int y = g + 1; y <= g + 4; y++)
                    for (int i = 0; i < 2; i++)
                    {
                        // Nearest cell first, row by row from the bottom.
                        int x = colX[k] > x0 + 4 ? colX[k] + i : colX[k] + 1 - i;
                        yield return C4PlaceAt(CellGrid.CellCenter(x, y), 0.03f, 26f);
                        if (TerraWorld.IsSolid(x, y)) laid++;
                    }
            }
            // The town's own torches on top, lighting the colours.
            inv.Selected = 5;
            yield return new WaitForSeconds(0.15f);
            foreach (int tx in new[] { x0 + 2, x0 + 8 })
            {
                C4Face(hero, tx > x0 + 4 ? 1 : -1);
                yield return C4PlaceAt(CellGrid.CellCenter(tx, g + 5), 0.15f, 12f);
            }
            for (float t = 0f; t < 1.6f; t += Time.deltaTime) { SmoothAim(CellGrid.CellCenter(x0 + 5, g + 7), 2.5f); yield return null; }
            StopRec();
            F5Log($"地区物块：放下 {laid}/40");
            for (int i = 0; i < Inventory.HotbarSize; i++) inv.Slots[i] = saved[i];
            inv.Accessories[accSlot] = savedAcc;
            F5Accessories(boots);
            // Out of the gap for the next take.
            C4PutOn(hero, new Vector2Int(x0 + 14, g + 1));
        }

        IEnumerator F5AreaPlatforms()
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            // The stair rises right from the street to a balcony six cells up, past the block wall.
            int x0 = CellGrid.WorldToCell(new Vector2(66f, 10f)).x + 14;
            int g = F5GroundRow(CellGrid.CellCenter(x0, 0).x, TownNpcs.DevStreet.y + 1f);
            var saved = F5Hotbar(RegionalFurniture.Item(10, RegionalFurniture.PlatformIndex), RegionalFurniture.Item(18, RegionalFurniture.PlatformIndex));
            int accSlot = Inventory.AccessorySlots - 1;
            var savedAcc = inv.Accessories[accSlot];
            inv.Accessories[accSlot] = ItemStack.Of(ItemType.ArchitectGizmoPack, 1);
            inv.RefreshEffective();
            Equipment.Update(inv);
            var col = hero.GetComponent<BoxCollider2D>();
            C4PutOn(hero, new Vector2Int(x0 - 3, g + 1));
            C4Face(hero, 1);
            var bootsSaved = F5NoBoots();
            F5CamOn(new Vector2(1.5f, 3.4f));
            yield return new WaitForSeconds(0.8f);
            F5CamSnap();
            aimInit = false;
            SmoothAim(CellGrid.CellCenter(x0, g + 1), 99f);
            int laid = 0;
            StartRec("v5F_area_platforms");
            yield return new WaitForSeconds(0.3f);
            inv.Selected = 0;
            // The stair: four steps from the street, then up onto them, the last two from there.
            for (int i = 0; i < 6; i++)
            {
                if (i == 4)
                {
                    // Up onto the stair: a jump held a moment, drifting right, landing on the steps from above.
                    Player.TerraMovement.DevMove = 1;
                    Player.TerraMovement.DevJump = true;
                    Player.TerraMovement.DevGroundJump(hero);
                    for (float t = 0f; t < 0.42f; t += Time.deltaTime)
                    {
                        Player.TerraMovement.DevMove = col.bounds.center.x < CellGrid.CellCenter(x0 + 3, 0).x ? 1 : 0;
                        SmoothAim(CellGrid.CellCenter(x0 + 4, g + 5), 8f);
                        yield return null;
                    }
                    Player.TerraMovement.DevJump = false;
                    for (float t = 0f; t < 1.5f && !(hero.cState.onGround && col.bounds.min.y > CellGrid.CellCenter(0, g + 2).y); t += Time.deltaTime)
                    {
                        Player.TerraMovement.DevMove = col.bounds.center.x < CellGrid.CellCenter(x0 + 3, 0).x ? 1 : 0;
                        SmoothAim(CellGrid.CellCenter(x0 + 4, g + 5), 8f);
                        yield return null;
                    }
                    Player.TerraMovement.DevMove = 0;
                    F5Log($"地区平台：跳上后主角 {(Vector2)hero.transform.position:F1}，站着 {hero.cState.onGround}");
                    C4Face(hero, 1);
                }
                yield return C4PlaceAt(CellGrid.CellCenter(x0 + i, g + 1 + i), 0.06f, 16f);
                if (TerraWorld.IsPlatform(x0 + i, g + 1 + i)) laid++;
            }
            // The balcony of another area's platforms, laid ahead while walking out along it.
            inv.Selected = 1;
            yield return new WaitForSeconds(0.15f);
            for (int i = 6; i < 14; i++)
            {
                if (i == 8 || i == 11)
                {
                    yield return F5WalkTo(hero, CellGrid.CellCenter(x0 + i - 3, 0).x, 2f, () => CellGrid.CellCenter(x0 + i, g + 6));
                    C4Face(hero, 1);
                }
                yield return C4PlaceAt(CellGrid.CellCenter(x0 + i, g + 6), 0.05f, 16f);
                if (TerraWorld.IsPlatform(x0 + i, g + 6)) laid++;
            }
            yield return F5WalkTo(hero, CellGrid.CellCenter(x0 + 12, 0).x, 2f, () => CellGrid.CellCenter(x0 + 15, g + 7));
            yield return new WaitForSeconds(0.8f);
            StopRec();
            F5Accessories(bootsSaved);
            F5Log($"地区平台：放下 {laid}/14，主角 {(Vector2)hero.transform.position:F1}（阳台高 {CellGrid.CellCenter(0, g + 6).y:F1}）");
            for (int i = 0; i < Inventory.HotbarSize; i++) inv.Slots[i] = saved[i];
            inv.Accessories[accSlot] = savedAcc;
            inv.RefreshEffective();
            Equipment.Update(inv);
        }

        IEnumerator F5BlockSwap()
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            int x0 = CellGrid.WorldToCell(new Vector2(86f, 10f)).x;
            int g = F5GroundRow(CellGrid.CellCenter(x0, 0).x, TownNpcs.DevStreet.y + 1f);
            F5Shoo(70f, 112f, 1, 12f);
            // Outside the take: a patch of dirt right of the stand (x0+1 .. x0+5), a wooden back wall left of it (x0-6 .. x0-2).
            for (int x = x0 + 1; x <= x0 + 5; x++)
                for (int y = g + 1; y <= g + 4; y++) { TerraWorld.Set(x, y, CellKind.Dirt); TerraWorld.SetWall(x, y, 0); }
            int wood = ItemDb.Get(ItemType.WoodWall).PlacesWall;
            for (int x = x0 - 6; x <= x0 - 2; x++)
                for (int y = g + 1; y <= g + 4; y++) { TerraWorld.Set(x, y, CellKind.Empty); TerraWorld.SetWall(x, y, wood); }
            var saved = F5Hotbar(AreaBlocks.BlockItem(7), ItemType.StoneWall);
            inv.Slots[2] = ItemStack.Of(ItemType.SolarFlarePickaxe);
            int accSlot = Inventory.AccessorySlots - 1;
            var savedAcc = inv.Accessories[accSlot];
            inv.Accessories[accSlot] = ItemStack.Of(ItemType.ArchitectGizmoPack, 1);
            inv.RefreshEffective();
            Equipment.Update(inv);
            var col = hero.GetComponent<BoxCollider2D>();
            C4PutOn(hero, new Vector2Int(x0 - 1, g + 1));
            C4Face(hero, 1);
            F5CamOn(Vector2.zero);
            f5CamFix = new Vector2(CellGrid.CellCenter(x0, 0).x, CellGrid.CellCenter(0, g).y + 3.6f);
            yield return new WaitForSeconds(0.8f);
            F5CamSnap();
            aimInit = false;
            SmoothAim(CellGrid.CellCenter(x0 + 1, g + 1), 99f);
            int swapped0 = BlockSwap.DevSwapped, walls0 = BlockSwap.DevWallsSwapped;
            StartRec("v5F_blockswap");
            yield return new WaitForSeconds(0.4f);
            inv.Selected = 0;
            for (int y = g + 1; y <= g + 4; y++)
                for (int i = 0; i < 5; i++)
                {
                    int x = (y - g) % 2 == 1 ? x0 + 1 + i : x0 + 5 - i;
                    var c = CellGrid.CellCenter(x, y);
                    for (int k = 0; k < 40 && TerraWorld.Get(x, y) == CellKind.Dirt; k++)
                    {
                        SmoothAim(c, 24f);
                        if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aimNow, false, true);
                        yield return null;
                    }
                }
            yield return new WaitForSeconds(0.4f);
            // Round to the wooden wall: stone wall over it where the cursor passes.
            C4Face(hero, -1);
            inv.Selected = 1;
            yield return new WaitForSeconds(0.25f);
            for (int y = g + 4; y >= g + 1; y--)
                for (int i = 0; i < 5; i++)
                {
                    int x = (g + 4 - y) % 2 == 0 ? x0 - 2 - i : x0 - 6 + i;
                    var c = CellGrid.CellCenter(x, y);
                    int before = TerraWorld.GetWall(x, y);
                    for (int k = 0; k < 30 && TerraWorld.GetWall(x, y) == before; k++)
                    {
                        SmoothAim(c, 26f);
                        if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aimNow, false, true);
                        yield return null;
                    }
                }
            for (float t = 0f; t < 1.2f; t += Time.deltaTime) { SmoothAim(CellGrid.CellCenter(x0, g + 6), 2.5f); yield return null; }
            StopRec();
            int stone = ItemDb.Get(ItemType.StoneWall).PlacesWall, qg = 0, sw = 0;
            for (int x = x0 - 6; x <= x0 + 5; x++) for (int y = g + 1; y <= g + 4; y++) { if (TerraWorld.Get(x, y) == AreaBlocks.Kind(7)) qg++; if (TerraWorld.GetWall(x, y) == stone) sw++; }
            F5Log($"方块替换：换了 {BlockSwap.DevSwapped - swapped0} 块（王后花园石 {qg}/20），墙 {BlockSwap.DevWallsSwapped - walls0}（石墙 {sw}/20）");
            for (int i = 0; i < Inventory.HotbarSize; i++) inv.Slots[i] = saved[i];
            inv.Accessories[accSlot] = savedAcc;
            inv.RefreshEffective();
            Equipment.Update(inv);
            C4PutOn(hero, new Vector2Int(x0 + 8, g + 1));
        }

        IEnumerator F5SmartInteract()
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            int x0 = CellGrid.WorldToCell(new Vector2(96f, 10f)).x;
            int g = F5GroundRow(CellGrid.CellCenter(x0, 0).x, TownNpcs.DevStreet.y + 1f);
            F5Shoo(84f, 118f, 1, 10f);
            var col = hero.GetComponent<BoxCollider2D>();
            // Three area chests (Greenpath, Crystal Peak, the Hive) set down a few cells apart, outside the take.
            int[] styles = { 5, 11, 16 };
            var spots = new List<Vector2>();
            for (int k = 0; k < styles.Length; k++)
            {
                var at = CellGrid.CellCenter(x0 + k * 4, g + 1);
                C4PutOn(hero, new Vector2Int(x0 + k * 4 - 2, g + 1));
                yield return null;
                if (Chests.TryPlace(hero, at, styles[k])) spots.Add(at + new Vector2(-0.25f, 0.25f));
            }
            // Something in each (opened and filled outside the take).
            var items = new[] { ItemType.Starfury, ItemType.HermesBoots, ItemType.LifeCrystal, ItemType.GoldBar, ItemType.HealingPotion, ItemType.Torch, ItemType.MagicMirror, ItemType.BandofRegeneration };
            for (int k = 0; k < spots.Count; k++)
            {
                if (!Chests.TryRightClick(spots[k]) && !Chests.DevOpenNear(spots[k])) continue;
                var into = Chests.OpenItems;
                for (int i = 0; into != null && i < 4; i++) into[i] = ItemStack.Of(items[(k * 3 + i) % items.Length], items[(k * 3 + i) % items.Length] == ItemType.Torch || items[(k * 3 + i) % items.Length] == ItemType.GoldBar || items[(k * 3 + i) % items.Length] == ItemType.HealingPotion ? 15 : 1);
                Chests.Close(false);
                InventoryGui.Open = false;
                yield return null;
            }
            C4PutOn(hero, new Vector2Int(x0 + 4, g + 1));
            C4Face(hero, 1);
            mod.DevSetSmartCursor(true);
            inv.Selected = 0;
            F5CamOn(new Vector2(0.5f, 3.6f));
            yield return new WaitForSeconds(0.8f);
            F5CamSnap();
            var path = new[] { spots.Count > 0 ? spots[0] + new Vector2(0.4f, 1.2f) : (Vector2)col.bounds.center, spots.Count > 2 ? spots[2] + new Vector2(-0.4f, 1.0f) : (Vector2)col.bounds.center, spots.Count > 1 ? spots[1] + new Vector2(0.2f, 0.9f) : (Vector2)col.bounds.center };
            Vector2 p = (Vector2)col.bounds.center + new Vector2(-2.5f, 1.4f);
            AimAt(p);
            SmartInteract.DevMouse = p;
            int used0 = SmartInteract.DevUsed;
            StartRec("v5F_smart_interact");
            yield return new WaitForSeconds(0.4f);
            foreach (var target in path)
            {
                var from = p;
                for (float t = 0f; t < 0.9f; t += Time.deltaTime)
                {
                    p = Vector2.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / 0.9f));
                    AimAt(p);
                    SmartInteract.DevMouse = p;
                    yield return null;
                }
                for (float t = 0f; t < 0.45f; t += Time.deltaTime) { SmartInteract.DevMouse = p; yield return null; }
            }
            var picked = SmartInteract.Picked;
            bool opened = SmartInteract.TryUse();
            yield return new WaitForSeconds(2.2f);
            StopRec();
            Chests.Close(true);
            InventoryGui.Open = false;
            SmartInteract.DevMouse = null;
            mod.DevSetSmartCursor(false);
            mod.DevAim = null;
            F5Log($"智能交互：箱子 {spots.Count}，选中 {picked}，打开 {opened}（{SmartInteract.DevUsed - used0}）");
        }
    }
}
