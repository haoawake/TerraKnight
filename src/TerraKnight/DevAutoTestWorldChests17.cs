using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_17worldchests"): the areas' chests of a new save. The plan (five an area, a weapon's, an accessory's,
    /// three of supplies); a few areas' scenes visited: each chest set down on even ground, clear of hazards, in its area's
    /// look, holding what its part says; drawn, and on the full map once seen; kept through the save file; a save without a
    /// plan (an old one) gets none.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator WorldChests17()
        {
            var inv = mod.Inventory;
            // The test save's own (set down from its first plan) out of the way, then a plan of our own.
            Chests.Clear();
            RegionalChests.NewWorld(424242);
            var areas = AreaBlocks.All.Select(a => a.Name).ToList();
            var counts = areas.ToDictionary(a => a, a => RegionalChests.DevScenesOf(a).Count);
            string largest = areas.Where(a => a != "神居").OrderByDescending(RegionalChests.DevSize).First();
            bool rule = counts[largest] == RegionalChests.PerArea && counts["神居"] == 0 && areas.Where(a => a != "神居").All(a => counts[a] >= 1 && counts[a] <= RegionalChests.PerArea
                && counts[a] == RegionalChests.CountOf(a));
            FxCheck("箱子按地区面积分：最大的 5 个，其余按比例，神居没有", rule && RegionalChests.DevCount == counts.Values.Sum(),
                $"共 {RegionalChests.DevCount} 个，最大的是 {largest}；{string.Join("，", areas.Select(a => $"{a}{counts[a]}（{RegionalChests.DevSize(a)} 格）"))}");

            var visit = new List<string>();
            foreach (var area in new[] { "国王山道", "遗忘十字路", "苍绿之径", "泪水之城", "深巢", "水晶山峰" })
            {
                var s = RegionalChests.DevScenesOf(area).FirstOrDefault();
                if (s != null && !visit.Contains(s)) visit.Add(s);
            }
            int placed = 0, onGround = 0, clear = 0, styled = 0, rightParts = 0;
            var problems = new List<string>();
            var hero = HeroController.instance;
            string shotScene = null;
            Vector2 shotAt = default;
            foreach (var scene in visit)
            {
                yield return GoTo(scene, EntryGateOf(scene));
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(3f);
                var region = TerrainSystem.Find(scene);
                if (region == null) { problems.Add(scene + " 没加载"); continue; }
                foreach (var c in Chests.Save().Where(c => c.Scene == scene && c.Style > 0))
                {
                    placed++;
                    var w = (Vector2)region.Root.position + c.Local;
                    var below = CellGrid.WorldToCell(w + new Vector2(-0.25f, -0.1f));
                    if (TerraWorld.IsSolid(below.x, below.y) && TerraWorld.IsSolid(below.x + 1, below.y)) onGround++;
                    else problems.Add($"{scene} {c.Local:F1} 下面不是地面");
                    var box = new Rect(w.x - 0.5f, w.y, 1f, 1f);
                    bool hazard = false;
                    foreach (var dh in Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None))
                    {
                        var col = dh.GetComponent<Collider2D>();
                        if (col == null || dh.hazardType < 2 || dh.hazardType > 4 || dh.gameObject.scene.name != scene) continue;
                        var b = col.bounds;
                        if (Rect.MinMaxRect(b.min.x - 2f, b.min.y - 2f, b.max.x + 2f, b.max.y + 2f).Overlaps(box)) { hazard = true; break; }
                    }
                    if (!hazard) clear++;
                    else problems.Add($"{scene} {c.Local:F1} 旁边有尖刺/酸水");
                    if (c.Style == RegionalChests.StyleOf(Areas.AreaName(scene))) styled++;
                    var held = c.Items.Where(s => !s.IsEmpty).ToList();
                    bool weapon = held.Any(s => s.Def.MaxStack == 1 && s.Def.IsWeapon), accessory = held.Any(s => s.Def.MaxStack == 1 && s.Def.IsAccessory);
                    if (!(weapon && accessory)) rightParts++;
                    Plugin.Log.LogInfo($"[AutoTest] 地区储物箱 {scene} @{c.Local:F1} 样式 {c.Style}：{string.Join("，", held.Select(s => s.Def.Name + "×" + s.Count))}");
                    if (shotScene == null) { shotScene = scene; shotAt = w; }
                }
            }
            FxCheck("走过的场景放下了箱子", placed > 0, $"{visit.Count} 个场景放下 {placed} 个：{string.Join("、", visit)}；{string.Join("；", problems.Take(6))}");
            FxCheck("箱子放在平地上、离尖刺酸水远、用当地样式", placed > 0 && onGround == placed && clear == placed && styled == placed,
                $"平地 {onGround}/{placed}，远离危险 {clear}/{placed}，当地样式 {styled}/{placed}");
            FxCheck("一个箱子不会同时放武器和饰品", rightParts == placed, $"{rightParts}/{placed}");

            // What the parts hold, over many rolls of an area.
            int weaponOk = 0, accOk = 0, suppliesOk = 0, pets = 0;
            for (int i = 0; i < 60; i++)
            {
                var wpn = RegionalChests.Contents("遗忘十字路", RegionalChests.WeaponPart, 1).Where(s => !s.IsEmpty).ToList();
                var acc = RegionalChests.Contents("遗忘十字路", RegionalChests.AccessoryPart, 1).Where(s => !s.IsEmpty).ToList();
                var sup = RegionalChests.Contents("遗忘十字路", 3, 1).Where(s => !s.IsEmpty).ToList();
                if (wpn.Count(s => s.Def.MaxStack == 1 && s.Def.IsWeapon) == 1) weaponOk++;
                if (acc.Count(s => s.Def.MaxStack == 1 && s.Def.IsAccessory) == 1) accOk++;
                if (!sup.Any(s => s.Def.MaxStack == 1 && (s.Def.IsWeapon || s.Def.IsAccessory)) && sup.Any(s => s.Type == ItemType.Geo)) suppliesOk++;
                pets += sup.Count(s => Pets.Eggs.Contains(s.Type));
            }
            FxCheck("武器箱一件当地武器，饰品箱一件当地饰品，补给箱没有装备", weaponOk == 60 && accOk == 60 && suppliesOk >= 58,
                $"武器 {weaponOk}/60，饰品 {accOk}/60，补给 {suppliesOk}/60，宠物 {pets} 次；例：{string.Join("，", RegionalChests.Contents("遗忘十字路", 2, 1).Where(s => !s.IsEmpty).Select(s => s.Def.Name + "×" + s.Count))}");

            // Drawn, and on the map once seen.
            if (shotScene != null)
            {
                yield return GoTo(shotScene, EntryGateOf(shotScene));
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(2f);
                PlayerData.instance.isInvincible = true;
                QuietAround(hero.transform.position, 30f);
                hero.transform.position = Clear(hero, shotAt + new Vector2(-1.6f, 0.6f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1.5f);
                Shot(shotScene, "worldchest");
                yield return new WaitForSeconds(0.3f);
                var region = TerrainSystem.Find(shotScene);
                if (!UI.WorldMap.Open) UI.WorldMap.Toggle();
                UI.WorldMap.DevCentreCell(shotScene, CellGrid.WorldToCell(shotAt) - region.CellOffset);
                UI.WorldMap.DevZoom(4f);
                yield return new WaitForSecondsRealtime(0.6f);
                Shot(shotScene, "worldchest_map");
                yield return new WaitForSecondsRealtime(0.3f);
                UI.WorldMap.Toggle();
            }

            // Through the save file and back.
            int slot = Save.ModSave.DevStateSlot;
            int planned = RegionalChests.DevCount, down = RegionalChests.DevPlacedCount, styledChests = Chests.Save().Count(c => c.Style > 0);
            Save.ModSave.Write(slot, mod);
            var disk = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{slot}.json")));
            int onDisk = (disk["WorldChests"] as Newtonsoft.Json.Linq.JArray)?.Count ?? -1;
            Save.ModSave.Read(slot, mod);
            int styledBack = Chests.Save().Count(c => c.Style > 0);
            FxCheck("存档带上箱子计划和箱子样式", onDisk == planned && RegionalChests.DevCount == planned && RegionalChests.DevPlacedCount == down && styledBack == styledChests,
                $"计划 {planned}（文件里 {onDisk}，读回 {RegionalChests.DevCount}），已放 {down}→{RegionalChests.DevPlacedCount}，有样式的箱子 {styledChests}→{styledBack}");
            // An old save (no plan): none.
            RegionalChests.Load(0, null);
            FxCheck("老存档没有地区储物箱", RegionalChests.DevCount == 0 && !RegionalChests.Planned, $"{RegionalChests.DevCount}");
            // Mined when empty: its own chest item.
            FxCheck("地区储物箱物品都能放置", Enumerable.Range(1, AreaBlocks.All.Length).All(s => ItemDb.Get(RegionalChests.ItemOf(s)).PlacesChest && ItemDb.Get(RegionalChests.ItemOf(s)).ChestStyle == s && RegionalChests.FrameSprite(s, 2) != null),
                string.Join("，", Enumerable.Range(1, 4).Select(s => ItemDb.Get(RegionalChests.ItemOf(s)).Name)));
        }
    }
}
