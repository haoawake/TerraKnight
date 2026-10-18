using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_17crates"): the areas' fishing crates. Each of the 24 areas has its crate and coffer, drawn with the
    /// crate pack's pictures (the tier and acid crates too); each area's waters give its own crate before hardmode and its
    /// coffer after, no other area's and none of Terraria's biome crates, more often with the Crate potion; every crate
    /// opened gives a piece of equipment of the stage reached, the area's block and torch now and then; a right click in the
    /// bag opens one; the bag of them drawn.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly ItemType[] tierAndAcidCrates =
        {
            ItemType.WoodenCrate, ItemType.WoodenCrateHard, ItemType.IronCrate, ItemType.IronCrateHard, ItemType.GoldenCrate, ItemType.GoldenCrateHard,
            ItemType.LavaCrate, ItemType.LavaCrateHard,
        };

        IEnumerator Crates17()
        {
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            var inv = mod.Inventory;
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            int stageWas = Shop.DevStage;
            var areas = Fishing.DevAreaCrateAreas().ToList();

            // ---- the items and their pictures
            var types = new HashSet<ItemType>();
            int opens = 0, drawn = 0;
            var badArt = new List<string>();
            foreach (var a in areas)
                foreach (bool hard in new[] { false, true })
                {
                    var t = Fishing.AreaCrateFor(a, hard);
                    types.Add(t);
                    var d = ItemDb.Get(t);
                    if (d != null && d.Opens && Fishing.IsCrate(t)) opens++;
                    var tex = d != null ? Terraria.TerrariaAssets.Texture(d.Texture) : null;
                    if (tex != null && tex.name.Contains("hk_") && tex.width == 34 && tex.height == 34) drawn++;
                    else badArt.Add($"{d?.Name}({d?.Texture} {tex?.name})");
                }
            FxCheck("24 个地区各有普通匣和秘匣", areas.Count == 24 && types.Count == 48 && opens == 48 && !types.Contains(ItemType.None),
                $"地区 {areas.Count}，宝匣 {types.Count} 种，能打开 {opens}；{string.Join("、", areas.Take(4).Select(a => ItemDb.Get(Fishing.AreaCrateFor(a, false)).Name + "/" + ItemDb.Get(Fishing.AreaCrateFor(a, true)).Name))}……");
            int tierDrawn = tierAndAcidCrates.Count(t => Terraria.TerrariaAssets.Texture(ItemDb.Get(t).Texture)?.name.Contains("hk_") == true);
            FxCheck("新宝匣和木/铁/金/酸蚀匣都用素材包的贴图", drawn == 48 && tierDrawn == tierAndAcidCrates.Length, $"地区匣 {drawn}/48，通用和酸蚀 {tierDrawn}/{tierAndAcidCrates.Length}；不对的 {string.Join("，", badArt.Take(6))}");
            FxCheck("宝匣总数（通用 6、酸蚀 2、地区 48、牡蛎）", Fishing.DevCrates == 57, $"{Fishing.DevCrates} 种");

            // ---- caught: each area its own (the crate before hardmode, the coffer after), never another's or a Terraria biome crate
            var others = new HashSet<ItemType>(types);
            foreach (var t in new[] { ItemType.DungeonFishingCrate, ItemType.OceanCrate, ItemType.CorruptFishingCrate, ItemType.CrimsonFishingCrate, ItemType.HallowedFishingCrate,
                         ItemType.JungleFishingCrate, ItemType.FrozenCrate, ItemType.OasisCrate, ItemType.FloatingIslandFishingCrate })
                others.Add(t);
            foreach (int stage in new[] { 2, 5 })
            {
                Shop.DevStage = stage;
                bool hard = stage >= 4;
                var miss = new List<string>();
                var stray = new List<string>();
                int caught = 0;
                foreach (var a in areas)
                {
                    var own = Fishing.AreaCrateFor(a, hard);
                    var got = Fishing.DevSimulate(a, false, 60, 2000, 6000, crateBuff: true);
                    if (got.TryGetValue(own, out int n) && n > 0) caught += n;
                    else miss.Add(a);
                    foreach (var kv in got)
                        if (kv.Key != own && others.Contains(kv.Key)) stray.Add($"{a}:{ItemDb.Get(kv.Key).Name}");
                }
                FxCheck($"第 {stage} 阶段每个地区钓到自己的{(hard ? "秘匣" : "匣")}", miss.Count == 0 && stray.Count == 0,
                    $"24 个地区共钓到 {caught} 个；没钓到的 {string.Join("、", miss)}；钓到别处的 {string.Join("、", stray.Take(6))}");
            }
            Shop.DevStage = 2;
            {
                var plain = Fishing.DevSimulate("遗忘十字路", false, 60, 2000, 20000);
                var buff = Fishing.DevSimulate("遗忘十字路", false, 60, 2000, 20000, crateBuff: true);
                int p = plain.TryGetValue(ItemType.CrateCrossroads, out int a1) ? a1 : 0, b = buff.TryGetValue(ItemType.CrateCrossroads, out int b1) ? b1 : 0;
                var weak = Fishing.DevSimulate("遗忘十字路", false, 15, 2000, 20000, crateBuff: true);
                int w = weak.TryGetValue(ItemType.CrateCrossroads, out int w1) ? w1 : 0;
                FxCheck("宝匣药水和渔力让地区匣更常见", b > p * 3 / 2 && b > w, $"渔力 60：没喝 {p} 个，喝了 {b} 个；渔力 15 喝了 {w} 个（各 20000 次）");
            }

            // ---- opened: a piece of equipment every time, of the stage reached; the area's block and torch now and then
            foreach (int stage in new[] { 1, 3, 5 })
            {
                Shop.DevStage = stage;
                bool hard = stage >= 4;
                var noPiece = new List<string>();
                var tooLate = new List<string>();
                int blocks = 0, torches = 0, areasWithBlock = 0, areasWithTorch = 0;
                var sample = new List<string>();
                var noPieceSeen = new List<string>();
                foreach (var a in areas)
                {
                    var crate = Fishing.AreaCrateFor(a, hard);
                    int pieces = 0;
                    bool block = false, torch = false;
                    for (int i = 0; i < 30; i++)
                    {
                        var got = Fishing.DevContents(crate);
                        int before = pieces;
                        foreach (var s in got)
                        {
                            var d = ItemDb.Get(s.Type);
                            if (d.MaxStack == 1 && s.Type != ItemType.Geo || s.Type == ItemType.CatBast)
                            {
                                pieces++;
                                if (d.Stage > stage) tooLate.Add($"{a}:{d.Name}(阶段{d.Stage})");
                            }
                            if (s.Type >= ItemType.AreaBlockKingsPass && s.Type <= ItemType.AreaBlockKingsPass + 23) { block = true; blocks++; }
                            if (World.RegionalTorches.StyleOf(s.Type) > 0) { torch = true; torches++; }
                        }
                        if (pieces == before && noPieceSeen.Count < 6) noPieceSeen.Add(ItemDb.Get(crate).Name + "：" + string.Join("，", got.Select(s => (s.Type == ItemType.Geo ? "吉欧" : ItemDb.Get(s.Type).Name) + "×" + s.Count + "(" + ItemDb.Get(s.Type).MaxStack + ")")));
                        if (i == 0 && sample.Count < 4) sample.Add(ItemDb.Get(crate).Name + "：" + string.Join("，", got.Select(s => (s.Type == ItemType.Geo ? "吉欧" : ItemDb.Get(s.Type).Name) + "×" + s.Count)));
                    }
                    if (pieces < 30) noPiece.Add($"{a} {pieces}/30");
                    if (block) areasWithBlock++;
                    if (torch) areasWithTorch++;
                }
                FxCheck($"第 {stage} 阶段打开地区{(hard ? "秘匣" : "匣")}：每次一件当前阶段的装备", noPiece.Count == 0 && tooLate.Count == 0,
                    $"缺装备的 {string.Join("、", noPiece)}；超出阶段的 {string.Join("、", tooLate.Take(5))}；例：{string.Join(" / ", sample)}；没装备的那次：{string.Join(" / ", noPieceSeen)}");
                FxCheck($"第 {stage} 阶段地区匣给当地方块和火把", areasWithBlock == 24 && areasWithTorch == 24,
                    $"给了方块的地区 {areasWithBlock}/24（{blocks} 次），给了火把的 {areasWithTorch}/24（{torches} 次）");
            }
            var report = new List<string>();
            Shop.DevStage = 3;
            foreach (var a in areas)
            {
                var r = Fishing.DevAreaCrate(a);
                report.Add($"{a}={r.Gear}+{r.Theme}件/{r.Stuff}材料");
            }
            Plugin.Log.LogInfo("[AutoTest] 地区匣 第 3 阶段的装备池：" + string.Join("，", report));

            // ---- a right click in the bag, the bag of them drawn
            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
            inv.Mouse = default;
            inv.Slots[10] = ItemStack.Of(ItemType.CrateHive, 2);
            InventoryGui.Open = true;
            yield return null;
            InventoryGui.DevRightClick(10);
            bool openedOne = inv.Slots[10].Count == 1 && inv.Slots.Any(s => !s.IsEmpty && s.Type != ItemType.CrateHive);
            FxCheck("背包里右键打开地区匣", openedOne, string.Join("，", inv.Slots.Where(s => !s.IsEmpty).Select(s => s.Def.Name + "×" + s.Count)));
            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
            int slot = 0;
            foreach (var a in areas)
                foreach (bool hard in new[] { false, true })
                    if (slot < 50) inv.Slots[slot++] = ItemStack.Of(Fishing.AreaCrateFor(a, hard), 1);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("Town", "crates_area");
            yield return new WaitForSecondsRealtime(0.3f);
            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
            for (int i = 0; i < tierAndAcidCrates.Length; i++) inv.Slots[i] = ItemStack.Of(tierAndAcidCrates[i], 1);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("Town", "crates_tier");
            yield return new WaitForSecondsRealtime(0.3f);
            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
            InventoryGui.Open = false;
            Shop.DevStage = stageWas;
            inv.Infinite = infinite;
        }
    }
}
