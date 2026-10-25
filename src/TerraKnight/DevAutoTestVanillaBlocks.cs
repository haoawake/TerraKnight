using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator VanillaBlocksTest()
        {
            charmPass = charmAll = 0; charmTag = "vanillablocks";
            PlayerData.instance.isInvincible = true;
            yield return CharmPlace();
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            var constraints = rb.constraints; rb.constraints = RigidbodyConstraints2D.FreezeAll;
            var inv = mod.Inventory;
            var kept = (ItemStack[])inv.Slots.Clone();
            bool infinite = inv.Infinite, smart = mod.SmartCursor;
            inv.Infinite = false; mod.DevSetSmartCursor(false);
            inv.Selected = 0; inv.Override = -1;
            WorldItems.DevNoGrab = true;
            var box = hero.GetComponent<BoxCollider2D>().bounds;
            var cell = CellGrid.WorldToCell(new Vector2(box.center.x + 1.6f, box.min.y + .3f));
            var at = CellGrid.CellCenter(cell.x, cell.y);
            var types = new List<ItemType> { ItemType.Wood, ItemType.DirtBlock, ItemType.StoneBlock, ItemType.Obsidian };
            foreach (var e in VanillaBlocks.All) types.Add(e.Item);
            var pick = ItemDb.Get(ItemType.SpectrePickaxe);
            foreach (var type in types)
            {
                var def = ItemDb.Get(type);
                TerraWorld.Set(cell.x, cell.y, CellKind.Empty);
                TerraWorld.Set(cell.x, cell.y - 1, CellKind.Stone);
                TerraWorld.SetWall(cell.x, cell.y, 1);
                WorldItems.DevClear();
                inv.Slots[0] = ItemStack.Of(type, 3);
                yield return new WaitForSeconds(.35f);
                CharmCheck(type + " 可使用", mod.DevCanUse(type, at, out var target, out _) && target == cell, target.ToString());
                // Starter rewards can arrive while the fresh save is entering Town.
                // Reset the probe stack after waiting, and sample the use before later rewards.
                inv.Slots[0] = ItemStack.Of(type, 3);
                mod.DevUseSelected(at, target: true);
                int afterUse = inv.Slots[0].Count;
                yield return new WaitForSeconds(.2f);
                CharmCheck(type + " 放置和扣除", TerraWorld.Get(cell.x, cell.y) == def.Places && def.Places != CellKind.Empty && afterUse == 2,
                    $"kind={TerraWorld.Get(cell.x, cell.y)} stack={afterUse}");
                CharmCheck(type + " 实体碰撞", Cells.Blocks(def.Places) && TerraWorld.IsSolid(cell.x, cell.y), "");
                string sheet = VanillaBlocks.Sheet(def.Places) ?? (type == ItemType.Wood ? "Tiles_30" : type == ItemType.DirtBlock ? "Tiles_0" : type == ItemType.StoneBlock ? "Tiles_1" : "Tiles_56");
                CharmCheck(type + " 原版贴图", Terraria.TerrariaAssets.Texture(sheet) != null, sheet);
                WorldEdits.Unpack(WorldEdits.Pack(def.Places, true, 2), out var unpacked, out bool dug, out int slope);
                CharmCheck(type + " 存储编码", unpacked == def.Places && dug && slope == 2, "");
                if (type == ItemType.Glass || type == ItemType.Wood) CharmShot(type.ToString());
                for (int n = 0; n < 100 && TerraWorld.Get(cell.x, cell.y) != CellKind.Empty; n++) mod.Mine(pick, cell);
                CharmCheck(type + " 挖回原物品", TerraWorld.Get(cell.x, cell.y) == CellKind.Empty && WorldItems.DevCount(type) == 1, WorldItems.DevDescribe());
            }
            WorldItems.DevClear(); WorldItems.DevNoGrab = false;
            for (int i = 0; i < kept.Length; i++) inv.Slots[i] = kept[i];
            inv.Infinite = infinite; mod.DevSetSmartCursor(smart); rb.constraints = constraints;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 结果 {charmPass}/{charmAll}");
        }
    }
}
