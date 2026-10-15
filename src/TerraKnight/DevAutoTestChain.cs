using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "chain"): the craft chain. What Terraria only crafts (the Megashark, the tomes, the crimson
    /// weapons...) is in no drop table and has its recipe; every ingredient of those recipes comes from somewhere
    /// (a drop, a shop, another recipe); crimtane and luminite ore smelt into their bars; what goes into a recipe
    /// reads "Material".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ChainTest()
        {
            yield return null;
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 合成链 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var dropped = new HashSet<ItemType>();
            foreach (var key in EnemyDrops.DevRecords)
                foreach (var d in EnemyDrops.Of(key)) dropped.Add(d.Type);
            var crafted = new[]
            {
                ItemType.Megashark, ItemType.CrystalStorm, ItemType.CursedFlames, ItemType.SkyFracture, ItemType.MagicalHarp, ItemType.SpiritFlame,
                ItemType.NebulaBlaze, ItemType.NebulaArcanum, ItemType.SlimeWhip, ItemType.SlimeSpear, ItemType.CrimsonWhip, ItemType.MeteorWhip,
                ItemType.DivingGear, ItemType.HarpyCharm, ItemType.Hoverboard, ItemType.HellfireTreads, ItemType.CrimsonYoyo, ItemType.JungleYoyo,
                ItemType.TheMeatball, ItemType.DaoofPow, ItemType.BloodButcherer, ItemType.TendonBow, ItemType.FleshGrinder, ItemType.BloodLustCluster,
                ItemType.DeathbringerPickaxe, ItemType.Silk, ItemType.SpectreBar, ItemType.ShroomiteBar, ItemType.PalworldKinshipPeach,
                ItemType.VenomStaff, ItemType.Phantasm,
            };
            // Where an ingredient comes from: a drop (its ore for a bar), a shop or the basics, a recipe.
            var sold = new HashSet<ItemType>(Shop.Everything.Where(d => d.Source == ItemSource.Shop || d.Source == ItemSource.Basic).Select(d => d.Type));
            bool Sourced(ItemType t) => dropped.Contains(t) || (ItemDb.OreOf(t, out var o) && dropped.Contains(o.Ore)) || sold.Contains(t) || Recipes.For(t) != null;
            foreach (var t in crafted)
            {
                var r = Recipes.For(t);
                var unsourced = r == null ? new List<string>() : r.Ingredients.Where(g => !Sourced(g.Type)).Select(g => ItemDb.Get(g.Type)?.Name ?? g.Type.ToString()).ToList();
                Check(ItemDb.Get(t)?.Name ?? t.ToString(), !dropped.Contains(t) && r != null && unsourced.Count == 0,
                    $"掉落表里 {(dropped.Contains(t) ? "有" : "没有")}，配方 {(r == null ? "没有" : $"{r.Station}：{string.Join(" + ", r.Ingredients.Select(g => $"{ItemDb.Get(g.Type)?.Name}×{g.Count}"))}")}{(unsourced.Count > 0 ? "；没有来源的材料 " + string.Join("、", unsourced) : "")}");
            }
            foreach (var (ore, bar) in new[] { (ItemType.CrimtaneOre, ItemType.CrimtaneBar), (ItemType.LunarOre, ItemType.LunarBar) })
            {
                var r = Recipes.For(bar);
                Check($"{ItemDb.Get(ore)?.Name}炼成{ItemDb.Get(bar)?.Name}", r != null && r.Ingredients.Any(g => g.Type == ore) && (dropped.Contains(bar) || dropped.Contains(ore)),
                    r == null ? "没有配方" : $"{r.Station} {string.Join(" + ", r.Ingredients.Select(g => $"{ItemDb.Get(g.Type)?.Name}×{g.Count}"))}，掉落表里有它 {dropped.Contains(bar) || dropped.Contains(ore)}");
            }
            foreach (var (t, want) in new[] { (ItemType.Minishark, true), (ItemType.HermesBoots, true), (ItemType.CrimtaneBar, true), (ItemType.SpellTome, true), (ItemType.SDMG, false) })
                Check($"{ItemDb.Get(t)?.Name}显示“材料”", Recipes.IsMaterial(t) == want, $"是材料 {Recipes.IsMaterial(t)}，应为 {want}");
            Plugin.Log.LogInfo($"[AutoTest] 合成链 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
