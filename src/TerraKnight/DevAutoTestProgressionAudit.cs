using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator ProgressionAuditTest(bool combat)
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int oldStage = Shop.DevStage;
            var oldDifficulty = GameDifficulty.Current;
            var rng = UnityEngine.Random.state;
            var export = new Dictionary<string, object>();
            var stages = new List<object>();
            var available = new HashSet<ItemType>[6];
            try
            {
                UnityEngine.Random.InitState(10162026);
                var records = EnemyDrops.DevRecords.ToArray();
                foreach (var pair in ProgressionBalance.OriginalAccessories)
                {
                    var d = ItemDb.Get(pair.Key);
                    if (!d.Bonus.Equals(pair.Value.Bonus) || d.Defense != pair.Value.Defense || d.Damage != pair.Value.Damage || (d.Tooltip ?? "") != (pair.Value.Tooltip ?? ""))
                        Plugin.Log.LogError($"[AutoTest] 饰品原值 {pair.Key} 不通过");
                }
                export["accessoriesChecked"] = ProgressionBalance.OriginalAccessories.Count;
                for (int s = 0; s < 6; s++)
                {
                    Shop.DevStage = s;
                    var drops = records.Where(r =>
                    {
                        if (ProgressionBalance.BossRewards.TryGetValue(r, out var boss) && ProgressionBalance.FormalBoss(r)) return boss.Stage <= s;
                        return Areas.LevelOf(EnemyDrops.SceneOf(r)) <= s;
                    }).ToDictionary(r => r, r => EnemyDrops.At(r, s, false, s >= 4, s >= 5)
                        .Where(d => !d.Master && d.Chance > 0).Select(d => new { Type = d.Type.ToString(), Chance = EnemyDrops.MarginalChance(EnemyDrops.At(r,s,false,s>=4,s>=5), d), d.Min, d.Max }).ToArray());
                    var fish = new HashSet<ItemType>();
                    foreach (var rec in records.Select(EnemyDrops.SceneOf).Distinct().Where(scene => Areas.LevelOf(scene) <= s))
                    {
                        string area = Areas.AreaName(rec);
                        foreach (var t in Fishing.DevSimulate(area, false, 100, 1664, 2000).Keys) fish.Add(t);
                        if (s >= 1) foreach (var t in Fishing.DevSimulate(area, true, 100, 1664, 2000).Keys) fish.Add(t);
                    }
                    var crateContents = new HashSet<ItemType>();
                    foreach (var t in fish.Where(Fishing.IsCrate).ToArray())
                        for (int n = 0; n < 500; n++) foreach (var stack in Fishing.DevContents(t)) crateContents.Add(stack.Type);
                    var bait = new HashSet<ItemType>();
                    var oldSink = EnemyDrops.DevSink;
                    try
                    {
                        EnemyDrops.DevSink = (t,n) => bait.Add(t);
                        foreach (var rec in drops.Keys)
                            for (int n = 0; n < 300; n++) Fishing.KillDrops(rec, Vector2.zero, true, EnemyDrops.SceneOf(rec));
                    }
                    finally { EnemyDrops.DevSink = oldSink; }
                    var sources = new HashSet<ItemType>(ItemDb.All.Where(d=>d!=null && d.Stage<=s && d.Source==ItemSource.Basic).Select(d=>d.Type));
                    sources.UnionWith(Town.TownNpcs.DevSourcesAt(s).Select(x=>x.Type));
                    foreach (var rec in drops.Keys)
                        foreach (var row in EnemyDrops.At(rec,s,false,s>=4,s>=5))
                            if (!row.Master && ItemDb.Get(row.Type).Stage<=s) sources.Add(row.Type);
                    sources.UnionWith(fish); sources.UnionWith(crateContents); sources.UnionWith(bait);
                    if (s>=2) sources.UnionWith(Town.Angler.DevRewardSources());
                    sources.RemoveWhere(t=>ItemDb.Get(t).Stage>s);
                    bool more = true;
                    while (more)
                    {
                        more = false;
                        foreach (var r in Recipes.All)
                            if (r.StationStage<=s && r.Ingredients.All(i=>sources.Contains(i.Type))) more |= sources.Add(r.Result);
                    }
                    available[s] = sources;
                    var health = new List<object>();
                    foreach (var difficulty in (DifficultyLevel[])Enum.GetValues(typeof(DifficultyLevel)))
                    {
                        GameDifficulty.Current = difficulty;
                        foreach (var rec in records)
                        {
                            int raw = EnemyDrops.HpOf(rec);
                            if (raw <= 0 || !ProgressionLoot.Eligible(EnemyDrops.EntryFor(rec))) continue;
                            int at = rec.IndexOf('@');
                            string key = at < 0 ? rec : rec.Substring(0, at);
                            string scene = EnemyDrops.SceneOf(rec);
                            var reference = EnemyStats.IsBossKind(key, raw) ? BossStats.Of(key, EnemyDrops.VariantLevel(scene)) : null;
                            var info = new EnemyStats.Info { Record=key, MaxHp=raw, Scene=scene, Level=Areas.LevelOf(scene), Ref=reference, Boss=reference!=null };
                            health.Add(new { Record=rec, Difficulty=difficulty.ToString(), Stage=reference?.Stage ?? info.Level,
                                Boss=info.Boss, Elite=EnemyStats.EliteKind(key,raw), Life=EnemyStats.LifeWhole(info), Defense=info.Defense });
                        }
                    }
                    stages.Add(new { Stage=s, Drops=drops, Shops=Town.TownNpcs.DevSourcesAt(s).Select(x=>new {x.Npc,Type=x.Type.ToString()}).ToArray(),
                        Fishing=fish.Select(t=>t.ToString()).ToArray(), Bait=bait.Select(t=>t.ToString()).ToArray(),
                        Quests=s>=2 ? Town.Angler.DevRewardSources().Select(t=>t.ToString()).ToArray() : new string[0], Crates=crateContents.Select(t=>t.ToString()).ToArray(), Health=health });
                    yield return null;
                }
                export["stages"] = stages;
                var materialChecks = new List<object>();
                GameDifficulty.Current = DifficultyLevel.Normal;
                foreach (var boss in ProgressionBalance.BossRewards.Where(p=>p.Value.Stage>=4 && ProgressionBalance.FormalBoss(p.Key)))
                {
                    Shop.DevStage = boss.Value.Stage;
                    int bars=0,souls=0,fragments=0,husks=0;
                    var oldSink = EnemyDrops.DevSink;
                    try
                    {
                        EnemyDrops.DevSink = (t,n) =>
                        {
                            if (t==ItemType.HallowedBar) bars+=n;
                            if (t==ItemType.SoulofMight || t==ItemType.SoulofSight || t==ItemType.SoulofFright) souls+=n;
                            if (t==ItemType.BeetleHusk) husks+=n;
                            if (t==ItemType.FragmentSolar || t==ItemType.FragmentVortex || t==ItemType.FragmentNebula || t==ItemType.FragmentStardust) fragments+=n;
                        };
                        for (int n=0;n<100;n++) EnemyDrops.Roll(boss.Key,Vector2.zero,true,false);
                    }
                    finally { EnemyDrops.DevSink=oldSink; }
                    bool pass=bars>=1500 && souls>=2000 && (boss.Value.Stage<5 || husks>=400 && fragments>=1800);
                    materialChecks.Add(new {Boss=boss.Key,Trials=100,Bars=bars,Souls=souls,Fragments=fragments,Husks=husks,Pass=pass});
                    Plugin.Log.LogInfo($"[AutoTest] Boss重复击杀材料 {boss.Key} 100次：锭{bars} 魂{souls} 碎片{fragments} 外壳{husks} {(pass ? "通过" : "不通过")}");
                }
                export["bossMaterialChecks"] = materialChecks;
                var output = new List<object>();
                if (combat)
                {
                    GameDifficulty.Current = DifficultyLevel.Normal;
                    yield return LootCombatTest((label,pass,detail)=>Plugin.Log.LogInfo($"[AutoTest] {label} {detail} {(pass ? "通过" : "不通过")}"), output, available:available);
                    yield return BalanceTest("Crossroads_01",HeroController.instance);
                }
                export["combat"] = output;
                export["items"] = ItemDb.All.Where(d=>d!=null).ToDictionary(d=>d.Type.ToString(), d=>new {d.Stage,Source=d.Source.ToString(),d.Texture,d.Damage,d.Defense,d.Bonus,d.Tooltip});
                export["recipes"] = Recipes.All.Select(r=>new {Result=r.Result.ToString(),r.Count,r.Station,r.StationStage,Environment=r.Environment.ToString(),Ingredients=r.Ingredients.Select(i=>new {Type=i.Type.ToString(),i.Count}).ToArray()}).ToArray();
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"progression-audit.json"),JsonConvert.SerializeObject(export,Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] 阶段来源、四难度血量与 {ProgressionBalance.OriginalAccessories.Count} 个饰品原值导出通过");
            }
            finally { Shop.DevStage=oldStage; GameDifficulty.Current=oldDifficulty; UnityEngine.Random.state=rng; }
        }
    }
}
