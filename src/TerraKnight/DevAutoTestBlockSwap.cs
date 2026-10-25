using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "blockswap"): Terraria's block swap (Items.BlockSwap, the builder toggle "方块替换"). A dirt block used on
    /// stone mines it with the best pickaxe carried and goes down in its place, the stone dropped; nothing without a pickaxe
    /// or with the toggle off; not what drops the held item; not a platform made solid round the player; a wall over a wall
    /// of another kind.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BlockSwapTest()
        {
            charmPass = charmAll = 0;
            charmTag = "blockswap";
            PlayerData.instance.isInvincible = true;
            yield return CharmPlace();
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            bool endless = inv.Infinite;
            inv.Infinite = false;
            CharmCheck("方块替换默认开着", UI.BuilderToggles.BlockSwapOn, "");
            var box = hero.GetComponent<BoxCollider2D>().bounds;
            var c = CellGrid.WorldToCell(new Vector2(box.center.x + 1.6f, box.min.y + 0.3f));
            Vector2 at = CellGrid.CellCenter(c.x, c.y);
            // A stone set there by hand.
            TerraWorld.Set(c.x, c.y, CellKind.Empty);
            TerraWorld.Set(c.x, c.y, CellKind.Stone);
            yield return null;
            inv.Slots[0] = ItemStack.Of(ItemType.DirtBlock, 10);
            inv.Slots[5] = ItemStack.Of(ItemType.CopperPickaxe);
            inv.Selected = 0;
            inv.Override = -1;
            int swapped0 = BlockSwap.DevSwapped, blows0 = BlockSwap.DevBlows;
            IEnumerator UseFor(float seconds)
            {
                for (float t = Time.time + seconds; Time.time < t;)
                {
                    if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(at, target: true);
                    yield return null;
                }
            }
            yield return UseFor(3f);
            CharmShot("swapped");
            CharmCheck("拿泥土块点石块：铜镐挖几下后换成泥土", TerraWorld.Get(c.x, c.y) == CellKind.Dirt && BlockSwap.DevSwapped == swapped0 + 1,
                $"现在 {TerraWorld.Get(c.x, c.y)}，挖了 {BlockSwap.DevBlows - blows0} 下");
            CharmCheck("用掉一块泥土", inv.Slots[0].Count == 9, $"{inv.Slots[0].Count}");
            CharmCheck("换上之后不再挖（泥土上点泥土）", !BlockSwap.CanSwap(ItemDb.Get(ItemType.DirtBlock), c), "");
            // No pickaxe anywhere in the bag: nothing (the save's own pickaxes put aside meanwhile).
            var kept = (ItemStack[])inv.Slots.Clone();
            for (int i = 0; i < Inventory.Size; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Def.PickPower > 0) inv.Slots[i] = default;
            TerraWorld.Set(c.x, c.y, CellKind.Stone);
            yield return null;
            yield return UseFor(1.2f);
            CharmCheck("背包里没有镐子就不替换", TerraWorld.Get(c.x, c.y) == CellKind.Stone, $"{TerraWorld.Get(c.x, c.y)}");
            for (int i = 0; i < Inventory.Size; i++) if (!kept[i].IsEmpty && kept[i].Def.PickPower > 0) inv.Slots[i] = kept[i];
            inv.Slots[5] = ItemStack.Of(ItemType.CopperPickaxe);
            // The toggle off: nothing.
            TerraWorld.Set(c.x, c.y, CellKind.Stone);
            yield return null;
            UI.BuilderToggles.Toggle(UI.BuilderToggles.BlockSwapIndex);
            yield return UseFor(1.2f);
            CharmCheck("关掉方块替换就不替换", !UI.BuilderToggles.BlockSwapOn && TerraWorld.Get(c.x, c.y) == CellKind.Stone, $"{TerraWorld.Get(c.x, c.y)}");
            UI.BuilderToggles.Toggle(UI.BuilderToggles.BlockSwapIndex);
            CharmCheck("再点一下又开了", UI.BuilderToggles.BlockSwapOn, "");
            // What drops the held item: the same stone.
            TerraWorld.Set(c.x, c.y, CellKind.Stone);
            yield return null;
            CharmCheck("拿石块点石块不替换", TerraWorld.Get(c.x, c.y) == CellKind.Stone && !BlockSwap.CanSwap(ItemDb.Get(ItemType.StoneBlock), c), "");
            // The Hallownest ground under the feet: its area's own block drops from it.
            var ground = CellGrid.WorldToCell(new Vector2(box.center.x - 1.6f, box.min.y));
            for (int k = 0; k < 8 && TerraWorld.Get(ground.x, ground.y) == CellKind.Empty; k++) ground.y--;
            if (TerraWorld.Get(ground.x, ground.y) == CellKind.Original)
            {
                var own = AreaBlocks.DropFor(CellKind.Original, ground);
                CharmCheck("原版地面上拿它自己的地区方块不替换", !BlockSwap.CanSwap(ItemDb.Get(own), ground), $"{own}");
                CharmCheck("原版地面上拿泥土可以替换（镐力够）", BlockSwap.CanSwap(ItemDb.Get(ItemType.DirtBlock), ground), "");
            }
            else CharmCheck("脚下是原版地面", false, $"{TerraWorld.Get(ground.x, ground.y)}");
            // A platform round the player: not made solid.
            var feetCell = CellGrid.WorldToCell(new Vector2(box.center.x, box.center.y));
            var keep = TerraWorld.Get(feetCell.x, feetCell.y);
            TerraWorld.Set(feetCell.x, feetCell.y, CellKind.Platform);
            yield return null;
            CharmCheck("人站在平台里时不把它换成实心方块", !BlockSwap.CanSwap(ItemDb.Get(ItemType.DirtBlock), feetCell), "");
            TerraWorld.Set(feetCell.x, feetCell.y, keep);
            // A wall over a wall of another kind.
            TerraWorld.Set(c.x, c.y, CellKind.Empty);
            int wood = ItemDb.Get(ItemType.WoodWall).PlacesWall, stone = ItemDb.Get(ItemType.StoneWall).PlacesWall;
            TerraWorld.SetWall(c.x, c.y, wood);
            yield return null;
            inv.Slots[0] = ItemStack.Of(ItemType.StoneWall, 10);
            int walls0 = BlockSwap.DevWallsSwapped;
            yield return UseFor(0.6f);
            CharmCheck("拿石墙点木墙：直接换成石墙", TerraWorld.GetWall(c.x, c.y) == stone && BlockSwap.DevWallsSwapped > walls0, $"墙 {TerraWorld.GetWall(c.x, c.y)}（木 {wood}，石 {stone}）");
            CharmCheck("同种墙不替换", !BlockSwap.CanSwapWall(ItemDb.Get(ItemType.StoneWall), c), "");
            TerraWorld.SetWall(c.x, c.y, 0);
            // The toggle in the bag.
            for (float w = Time.time + 0.6f; Time.time < w;) { InventoryGui.Open = true; yield return null; }
            HudShot("toggle");
            yield return null;
            InventoryGui.Open = false;
            inv.Slots[0] = default;
            inv.Slots[5] = default;
            inv.Infinite = endless;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 结果 {charmPass}/{charmAll}");
        }
    }
}
