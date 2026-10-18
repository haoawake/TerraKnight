using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_17areablocks[_Scene.Scene]"): the areas' own blocks and walls (World/AreaBlocks.cs). In a few areas: the
    /// scene's terrain mined with a pickaxe drops that area's block; a patch of its blocks and walls is laid in open air and
    /// photographed beside a hole dug in the terrain; the items in the bag pictured; once: the recipes both ways at a work
    /// bench, the walls counted as house walls, and placed blocks and walls kept across leaving the room and coming back.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator AreaBlocks17(string[] scenes)
        {
            mod.DevNoFallDamage = true;
            Player.TerraStats.DevImmortal = true;
            WorldItems.DevNoGrab = true;
            // Terraria's journey/infinite bag drops nothing mined; the test plays a normal one.
            bool infinite = mod.Inventory.Infinite;
            mod.Inventory.Infinite = false;
            var pick = ItemDb.Get(ItemType.TitaniumPickaxe);
            Vector2Int keep = default;
            int keepWall = 0;
            CellKind keepKind = CellKind.Empty;
            string keepScene = null;
            int keepIndex = -1;
            foreach (var scene in scenes ?? new[] { "Crossroads_01", "Fungus1_01", "Mines_02", "Ruins1_01", "Hive_01", "White_Palace_02" })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                PlayerData.instance.isInvincible = true;
                QuietAround(HeroController.instance.transform.position, 60f);
                yield return new WaitForSeconds(1.5f);
                int area = AreaBlocks.IndexOfScene(scene);
                if (area < 0) { FxCheck($"地区方块 {scene} 属于某个地区", false, Areas.AreaName(scene)); continue; }
                var a = AreaBlocks.All[area];
                var hero = HeroController.instance;
                hero.transform.position = Clear(hero, (Vector2)hero.transform.position + new Vector2(10f, 1f));
                for (float t = Time.time + 3f; !hero.cState.onGround && Time.time < t;) yield return null;
                yield return new WaitForSeconds(0.4f);
                var b = hero.GetComponent<BoxCollider2D>().bounds;
                var feet = CellGrid.WorldToCell(new Vector2(b.center.x, b.min.y - 0.1f));

                // The ground beside him, mined.
                Vector2Int? target = null;
                for (int dx = 3; dx <= 10 && target == null; dx++)
                    for (int dy = 0; dy >= -3 && target == null; dy--)
                    {
                        var c = new Vector2Int(feet.x + dx, feet.y + dy);
                        if (TerraWorld.Get(c.x, c.y) == CellKind.Original && TerraWorld.CanDig(c.x, c.y)) target = c;
                    }
                if (target == null) { FxCheck($"地区方块 {scene} 找到能挖的地形", false, $"脚下 {feet}"); continue; }
                WorldItems.DevClear();
                var cell = target.Value;
                for (int k = 0; k < 40 && TerraWorld.Get(cell.x, cell.y) != CellKind.Empty; k++)
                {
                    mod.Mine(pick, cell);
                    yield return null;
                }
                int got = WorldItems.DevCount(AreaBlocks.BlockItem(area));
                FxCheck($"地区方块 {a.Name} 挖原生地形掉 {a.Block}", got == 1, $"格 {cell} 现在 {TerraWorld.Get(cell.x, cell.y)}，掉了 {got} 个，场上 {WorldItems.DevDescribe()}");

                // A pocket dug beside it (the area's wall behind), and a patch of its blocks and walls in the air above.
                for (int dx = 1; dx <= 6; dx++)
                    for (int dy = -1; dy >= -3; dy--)
                        if (TerraWorld.Get(cell.x + dx, cell.y + dy) == CellKind.Original && TerraWorld.CanDig(cell.x + dx, cell.y + dy))
                            TerraWorld.Set(cell.x + dx, cell.y + dy, CellKind.Empty);
                int laid = 0;
                var corner = new Vector2Int(feet.x + 2, feet.y + 4);
                for (int x = 0; x < 10; x++)
                    for (int y = 0; y < 6; y++)
                    {
                        int gx = corner.x + x, gy = corner.y + y;
                        if (TerraWorld.Get(gx, gy) != CellKind.Empty) continue;
                        TerraWorld.SetWall(gx, gy, AreaBlocks.WallId(area));
                        bool ring = x == 0 || y == 0 || x == 9 || y == 5 || (x >= 3 && x <= 5 && y == 2);
                        if (ring) { TerraWorld.Set(gx, gy, AreaBlocks.Kind(area)); laid++; }
                    }
                Player.TerraCamera.DevLook = CellGrid.CellCenter(corner.x + 5, corner.y + 1);
                yield return new WaitForSeconds(0.8f);
                Shot(scene, "areablocks_" + a.Key);
                Player.TerraCamera.DevLook = null;
                FxCheck($"地区方块 {a.Name} 放下方块和墙", laid > 10 && TerraWorld.Get(corner.x, corner.y) == AreaBlocks.Kind(area),
                    $"方块 {laid} 个，墙 {TerraWorld.GetWall(corner.x + 1, corner.y + 1)}");
                if (keepScene == null)
                {
                    keepScene = scene;
                    keep = corner;
                    keepKind = TerraWorld.Get(corner.x, corner.y);
                    keepWall = TerraWorld.GetWall(corner.x + 1, corner.y + 1);
                    var reg = TerraWorld.RegionAt(corner.x, corner.y);
                    keepIndex = (corner.y - reg.CellOffset.y) * reg.Grid.Width + (corner.x - reg.CellOffset.x);
                }
            }
            WorldItems.DevClear();
            WorldItems.DevNoGrab = false;

            // The items in the bag.
            var inv = mod.Inventory;
            for (int i = 0; i < 10; i++)
            {
                inv.Slots[i] = ItemStack.Of(AreaBlocks.BlockItem(i * 2), 99);
                inv.Slots[Inventory.HotbarSize + i] = ItemStack.Of(AreaBlocks.WallItem(i * 2), 99);
            }
            InventoryGui.Open = true;
            yield return new WaitForSeconds(0.6f);
            Shot(GameManager.instance.sceneName, "areablocks_bag");
            InventoryGui.Open = false;

            bool recipes = true;
            for (int i = 0; i < AreaBlocks.All.Length; i++)
            {
                bool toWall = Recipes.All.Any(r => r.Result == AreaBlocks.WallItem(i) && r.Count == 4 && r.Station == "工作台" && r.Ingredients.Length == 1 && r.Ingredients[0] == (AreaBlocks.BlockItem(i), 1));
                bool back = Recipes.All.Any(r => r.Result == AreaBlocks.BlockItem(i) && r.Count == 1 && r.Station == "工作台" && r.Ingredients.Length == 1 && r.Ingredients[0] == (AreaBlocks.WallItem(i), 4));
                recipes &= toWall && back;
            }
            FxCheck("地区方块 1 块做 4 墙、4 墙做回 1 块（工作台）", recipes, "");
            FxCheck("地区墙算房屋墙", Enumerable.Range(0, AreaBlocks.All.Length).All(i => Housing.WallHouse(AreaBlocks.WallId(i))) && !Housing.WallHouse(Housing.NaturalWall), "");
            bool named = Enumerable.Range(0, AreaBlocks.All.Length).All(i => ItemDb.Get(AreaBlocks.BlockItem(i)).Name == AreaBlocks.All[i].Block && ItemDb.Get(AreaBlocks.WallItem(i)).Name == AreaBlocks.All[i].Wall);
            FxCheck("地区方块和墙都有物品", named, "");

            // Written to the save file and read back: the kind's high bits in ScenesHigh.
            if (keepScene != null)
            {
                int slot = Save.ModSave.DevStateSlot;
                Save.ModSave.Write(slot, mod);
                var disk = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{slot}.json")));
                long want = ((long)keepIndex << 8) | ((byte)keepKind >> 4);
                bool onDisk = disk["ScenesHigh"]?[keepScene] is Newtonsoft.Json.Linq.JArray high && high.Any(v => (long)v == want);
                FxCheck("地区方块写进存档文件（ScenesHigh）", onDisk, $"{keepScene} 格 {keepIndex} 应有 {want}");
                Save.ModSave.Read(slot, mod);
                bool back = WorldEdits.For(keepScene).TryGetValue(keepIndex, out ushort packed);
                WorldEdits.Unpack(packed, out var kind, out _);
                FxCheck("地区方块从存档文件读回", back && kind == keepKind, $"读回 {back}，{kind}（应为 {keepKind}）");
            }

            // Kept across leaving the room.
            if (keepScene != null)
            {
                string away = keepScene == "Crossroads_01" ? "Crossroads_02" : "Crossroads_01";
                yield return GoTo(away, EntryGateOf(away));
                yield return new WaitForSeconds(1f);
                yield return GoTo(keepScene, EntryGateOf(keepScene));
                yield return new WaitForSeconds(1.5f);
                FxCheck("地区方块和墙离开房间再回来还在", TerraWorld.Get(keep.x, keep.y) == keepKind && TerraWorld.GetWall(keep.x + 1, keep.y + 1) == keepWall,
                    $"{keepScene} 格 {keep}：{TerraWorld.Get(keep.x, keep.y)}（应为 {keepKind}），墙 {TerraWorld.GetWall(keep.x + 1, keep.y + 1)}（应为 {keepWall}）");
            }
            mod.Inventory.Infinite = infinite;
            Player.TerraStats.DevImmortal = false;
        }
    }
}
