using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator RestorationGunTest()
        {
            if (!DevCopy.Verified) throw new System.InvalidOperationException("Isolated saves required");
            int passed = 0, total = 0;
            void Check(string name, bool pass, string detail = "")
            {
                total++; if (pass) passed++;
                Plugin.Log.LogInfo($"[AutoTest] 环境改造枪 {name}：{detail} {(pass ? "通过" : "不通过")}");
            }
            mod.DevNoFallDamage = true; Player.TerraStats.DevImmortal = true; PlayerData.instance.isInvincible = true;
            yield return GoTo("Town", "left1"); yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>(); var constraints = rb.constraints;
            var inv = mod.Inventory; var slots = (ItemStack[])inv.Slots.Clone();
            bool infinite = inv.Infinite; int stage = Shop.DevStage;
            var stationSave = Stations.Save(); var chestSave = Chests.Save();
            bool altarTaken = FixedStations.Taken(StationKind.DemonAltar);
            var root = new GameObject("Restoration fixture"); root.AddComponent<TerraOwned>();
            var holder = new GameObject("Restoration terrain");
            var fixture = holder.AddComponent<TerrainSystem>();
            const string scene = "RestorationFixture";
            var grid = new CellGrid(48, 32);
            for (int x = 0; x < grid.Width; x++) for (int y = 0; y < 8; y++) grid.Init(x, y, CellKind.Original);
            grid.CaptureNetworkBase();
            var region = new TerrainRegion(scene, grid, root.transform, false, TerraWorld.Live.Material, new SpriteRenderer[0]);
            region.SetOffset(new Vector2Int(2000, 2000));
            AccessTools.Property(typeof(TerrainSystem), "Region").SetValue(fixture, region, null);
            AccessTools.Property(typeof(TerrainSystem), "SceneName").SetValue(fixture, scene, null);
            TerrainSystem.All.Add(fixture);
            Vector2 At(int x, int y) => (Vector2)region.Root.position + CellGrid.CellCenter(x, y);
            void Spray(int x, int y) { RestorationGun.Apply(At(x, y)); region.Tick(); }
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            WorldItems.DevNoGrab = true; Player.TerraControls.DevNoRealKeys = true;
            try
            {
                inv.Clear(); TerraMod.DevAllInfinite = false; mod.ApplyCreative(false); inv.Infinite = false;
                inv.UseMouse = false; inv.Override = -1; inv.Selected = 0; InventoryGui.Open = false;
                Stations.Clear(); Chests.Clear(); WorldItems.DevClear();
                var gun = ItemDb.Get(ItemType.Clentaminator);
                Check("原版贴图与免弹药定义", gun.Texture == "Item_779" && Terraria.TerrariaAssets.Texture(gun.Texture) != null
                    && gun.UseAmmo == AmmoKind.None && gun.Mana == 0 && gun.Damage == 0 && !gun.Placeholder);
                Check("蒸汽朋克人阶段4出售", Town.TownNpcs.DevSourcesAt(4).Any(x => x.Item1 == "Steampunker" && x.Item2 == gun.Type)
                    && !Town.TownNpcs.DevSourcesAt(3).Any(x => x.Item1 == "Steampunker" && x.Item2 == gun.Type));

                grid.Set(12, 5, CellKind.Empty);
                grid.ApplyNetwork(5 * grid.Width + 13, CellKind.Meteorite, true, 0);
                grid.ApplyNetwork(5 * grid.Width + 14, CellKind.Empty, true, 0);
                region.Tick();
                Check("坑洞确实切开原地形", grid.IsDug(12, 5) && region.Holes.HasMask(5 * grid.Width + 12));
                Spray(13, 5);
                Check("陨石和空洞恢复为原地形", new[] { 12, 13, 14 }.All(x => grid.Get(x, 5) == CellKind.Original && !grid.IsDug(x, 5)));
                Check("原地形恢复实体碰撞", TerraWorld.IsSolid(2013, 2005));
                Check("原地形遮罩闭合", !region.Holes.HasMask(5 * grid.Width + 13));
                Check("陨石生成对应掉落物", WorldItems.DevCount(ItemType.Meteorite) == 1);
                int stone = WorldItems.DevCount(ItemType.StoneBlock), meteor = WorldItems.DevCount(ItemType.Meteorite);
                for (int i = 0; i < 12; i++) Spray(13, 5);
                Check("反复喷不重复掉矿或原地形", WorldItems.DevCount(ItemType.Meteorite) == meteor && WorldItems.DevCount(ItemType.StoneBlock) == stone);

                grid.Set(15, 13, CellKind.Stone); grid.SetSlope(15, 13, 2); grid.SetWall(15, 13, 21);
                region.Liquid.Set(16, 13, 255, LiquidKind.Water);
                var wallItem = ItemDb.All.First(d => d != null && d.PlacesWall == 21).Type;
                int wallBefore = WorldItems.DevCount(wallItem);
                Spray(15, 13);
                Check("原本空气不会填成岩石", grid.Get(15, 13) == CellKind.Empty && grid.GetSlope(15, 13) == 0 && !grid.IsDug(15, 13));
                Check("拆除方块与墙掉物", WorldItems.DevCount(ItemType.StoneBlock) == stone + 1 && WorldItems.DevCount(wallItem) == wallBefore + 1);
                Check("清除墙和倒入的液体", grid.GetWall(15, 13) == -1 && grid.GetLiquid(16, 13) == 0);

                for (int x = 9; x <= 10; x++) grid.Set(x, 7, CellKind.Wood);
                grid.Set(12, 7, CellKind.Stone);
                var contents = new ItemStack[Chests.Capacity]; contents[0] = ItemStack.Of(ItemType.Gel, 37);
                contents[1] = ItemStack.Of(ItemType.CopperShortsword); contents[1].Prefix = 1; contents[1].Forge = 2;
                var chestAt = new Vector2(5f, 4f);
                Chests.Load(new[] { new Chests.Saved { Scene = scene, Local = chestAt, Style = 0, Items = contents, Name = "不动的箱子" } });
                for (int y = 7; y <= 10; y++) for (int x = 8; x <= 12; x++) Spray(x, y);
                var chest = Chests.Save().Single(c => c.Scene == scene);
                Check("箱子和名字保持", chest.Name == "不动的箱子" && chest.Local == chestAt);
                Check("箱子内容与词缀锻造保持", chest.Items[0].Type == ItemType.Gel && chest.Items[0].Count == 37
                    && chest.Items[1].Prefix == 1 && chest.Items[1].Forge == 2);
                Check("容器底部两格保持", grid.Get(9, 7) == CellKind.Wood && grid.Get(10, 7) == CellKind.Wood);
                Check("容器旁普通方块仍生效", grid.Get(12, 7) == CellKind.Original);
                foreach (var bank in new[] { StationKind.PiggyBank, StationKind.Safe, StationKind.DefendersForge, StationKind.VoidVault })
                {
                    Stations.Clear();
                    for (int x = 16; x <= 20; x++) grid.Set(x, 7, CellKind.Wood);
                    Stations.PlaceIn(bank, scene, new Vector2(9.25f, 4f));
                    for (int y = 7; y <= 12; y++) for (int x = 16; x <= 20; x++) Spray(x, y);
                    Check(bank + " 容器保留", Stations.Save().Any(s => s.Scene == scene && s.Kind == bank));
                    Check(bank + " 底座保留", new[] { 17, 18, 19 }.All(x => grid.Get(x, 7) == CellKind.Wood));
                }
                Stations.Clear();
                Stations.PlaceIn(StationKind.WorkBench, scene, new Vector2(9.25f, 4f));
                Stations.PlaceIn(StationKind.PiggyBank, scene, new Vector2(9.25f, 4.5f));
                Spray(18, 8); Spray(18, 7);
                Check("储蓄罐下的工作台及底座保留", Stations.Save().Count(s => s.Scene == scene) == 2 && grid.Get(18, 7) == CellKind.Wood);
                Stations.Clear();
                Stations.PlaceIn(StationKind.WorkBench, scene, new Vector2(12.25f, 4f));
                int benches = WorldItems.DevCount(ItemType.WorkBench);
                Spray(24, 8);
                Check("普通工作站拆除并掉物", !Stations.Save().Any(s => s.Scene == scene) && WorldItems.DevCount(ItemType.WorkBench) == benches + 1);
                Spray(24, 8);
                Check("家具不重复掉物", WorldItems.DevCount(ItemType.WorkBench) == benches + 1);
                Stations.PlaceFixed(StationKind.DemonAltar, scene, new Vector2(12.25f, 4f));
                int altars = WorldItems.DevCount(ItemType.DemonAltar);
                Spray(24, 8);
                Check("祭坛回收并写入移除记录", !Stations.DevAltarBox(StationKind.DemonAltar, true).HasValue
                    && FixedStations.Taken(StationKind.DemonAltar) && WorldItems.DevCount(ItemType.DemonAltar) == altars + 1);
                Spray(24, 8);
                Check("祭坛不重复掉物", WorldItems.DevCount(ItemType.DemonAltar) == altars + 1);

                grid.Set(35, 15, CellKind.Meteorite); inv.Slots[0] = ItemStack.Of(ItemType.Clentaminator);
                hero.transform.position = At(25, 15) - (Vector2)hero.GetComponent<BoxCollider2D>().offset;
                rb.linearVelocity = Vector2.zero; hero.FaceRight();
                yield return new WaitForSeconds(.4f);
                // Fresh-save starter rewards can arrive during the first rendered frame.
                inv.Clear(); inv.Slots[0] = ItemStack.Of(ItemType.Clentaminator); inv.Selected = 0;
                bool noAmmoAtFire = !inv.Slots.Any(s => !s.IsEmpty && s.Def.IsAmmo);
                int made = TerraProjectiles.DevMade(ProjKind.RestorationSpray);
                mod.DevUseSelected(At(40, 15));
                bool keptTool = inv.Slots[0].Type == gun.Type && inv.Slots[0].Count == 1;
                yield return new WaitForSeconds(.6f);
                Check("真实使用无需弹药能喷射", TerraProjectiles.DevMade(ProjKind.RestorationSpray) > made && mod.HasAmmo(gun));
                Check("真实喷流恢复命中格", grid.Get(35, 15) == CellKind.Empty);
                Check("工具不消耗且无弹药槽", keptTool && noAmmoAtFire);
                TerraProjectiles.Instance.DevClear();

                var replay = new CellGrid(grid.Width, grid.Height);
                for (int x = 0; x < replay.Width; x++) for (int y = 0; y < 8; y++) replay.Init(x, y, CellKind.Original);
                region.CaptureLiquids(); TerrainRegion.ReplayEdits(replay, scene);
                Check("重读保存不再出现陨石坑", replay.Get(13, 5) == CellKind.Original && !replay.IsDug(13, 5) && replay.Get(35, 15) == CellKind.Empty);
                Check("重读保留容器底座", replay.Get(9, 7) == CellKind.Wood && replay.Get(10, 7) == CellKind.Wood);
                Check("重读保持墙和液体清除", replay.GetWall(15, 13) == -1 && replay.GetLiquid(16, 13) == 0);
                Check("拒绝非有限联机坐标", !RestorationGun.NetApply(new Net.WorldSync.Action { Scene = scene, X = float.NaN, Y = 1 }, -1));
            }
            finally
            {
                TerraProjectiles.Instance.DevClear();
                TerrainSystem.All.Remove(fixture); region.Destroy(); Object.Destroy(holder); Object.Destroy(root);
                WorldEdits.For(scene).Clear(); WorldWalls.For(scene).Clear(); WorldLiquids.For(scene).Clear();
                Stations.Load(stationSave); Chests.Load(chestSave);
                if (!altarTaken) mod.GrantedAbilities.Remove("altar.taken." + StationKind.DemonAltar);
                for (int i = 0; i < slots.Length; i++) inv.Slots[i] = slots[i];
                inv.Infinite = infinite; Shop.DevStage = stage;
                rb.constraints = constraints; WorldItems.DevNoGrab = false; Player.TerraControls.DevNoRealKeys = false;
            }

            // The real meteor algorithm, in a real room, rather than only a hand-written tile fixture.
            yield return GoTo("Crossroads_01", "top1"); yield return new WaitForSeconds(2f);
            var live = TerraWorld.Live; var g = live.Grid;
            var impact = new Vector2Int(-1, -1);
            for (int x = 20; x < g.Width - 20 && impact.x < 0; x++)
            for (int y = 12; y < g.Height - 6; y++)
            {
                bool floor = true;
                for (int dy = 0; dy < 9; dy++) floor &= g.Get(x, y - dy) == CellKind.Original;
                if (floor && g.Get(x, y + 1) == CellKind.Empty) { impact = new Vector2Int(x, y); break; }
            }
            Check("找到真实场景陨石测试地形", impact.x >= 0, live.SceneName);
            if (impact.x >= 0)
            {
                AccessTools.Method(typeof(Meteorites), "Smash").Invoke(null, new object[] { live.SceneName, impact.x, impact.y });
                var edits = WorldEdits.For(live.SceneName).ToArray();
                foreach (var e in edits) { WorldEdits.Unpack(e.Value, out var kind, out bool dug, out int slope); g.ApplyNetwork(e.Key, kind, dug, slope); }
                live.Tick(); yield return null;
                int nativeHoles = edits.Count(e => g.IsDugAt(e.Key) && g.GetAt(e.Key) == CellKind.Empty && g.OriginalAt(e.Key, out var k) && k == CellKind.Original);
                Check("真实陨石形成坑和矿石", nativeHoles > 0 && edits.Any(e => g.GetAt(e.Key) == CellKind.Meteorite), Meteorites.DevLast);
                var at = (Vector2)live.Root.position + CellGrid.CellCenter(impact.x, impact.y);
                hero.transform.position = Clear(hero, at + Vector2.up * 3f);
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(.4f); Shot(live.SceneName, "restoration_meteor_before");
                inv.Clear(); inv.Selected = 0; inv.Override = -1; inv.UseMouse = false;
                inv.Slots[0] = ItemStack.Of(ItemType.Clentaminator);
                mod.DevUseSelected(at + Vector2.right * 12f + Vector2.up * 3f);
                yield return new WaitForSeconds(.05f); Shot(live.SceneName, "restoration_spray");
                yield return new WaitForSeconds(.3f);
                TerraProjectiles.Instance.DevClear();
                int n = 0;
                foreach (var e in edits)
                {
                    RestorationGun.Apply((Vector2)live.Root.position + CellGrid.CellCenter(e.Key % g.Width, e.Key / g.Width));
                    if (++n % 24 == 0) { live.Tick(); yield return null; }
                }
                live.Tick(); yield return new WaitForSeconds(.4f);
                Check("整片真实陨石坑恢复初始方格", edits.All(e => g.OriginalAt(e.Key, out var k) && g.GetAt(e.Key) == k && !g.IsDugAt(e.Key)), $"恢复 {edits.Length} 格，原生坑洞 {nativeHoles} 格");
                Shot(live.SceneName, "restoration_meteor_after");
                yield return GoTo("Town", "left1"); yield return GoTo("Crossroads_01", "top1");
                Check("真实切换场景后陨石坑保持恢复", edits.All(e => TerraWorld.Live.Grid.OriginalAt(e.Key, out var k) && TerraWorld.Live.Grid.GetAt(e.Key) == k && !TerraWorld.Live.Grid.IsDugAt(e.Key)));
            }
            Player.TerraStats.DevImmortal = false; mod.DevNoFallDamage = false;
            Plugin.Log.LogInfo($"[AutoTest] 环境改造枪 合计 {passed}/{total}");
        }
    }
}
