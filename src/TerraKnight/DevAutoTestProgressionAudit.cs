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
        IEnumerator ProgressionAuditTest(bool combat, bool stageMilestones = false)
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int oldStage = Shop.DevStage;
            var oldDifficulty = GameDifficulty.Current;
            var rng = UnityEngine.Random.state;
            var oldClears = VanillaBossProgression.DevClears;
            var export = new Dictionary<string, object>();
            var stages = new List<object>();
            var available = new HashSet<ItemType>[6];
            try
            {
                UnityEngine.Random.InitState(10162026);
                var records = EnemyDrops.DevRecords.ToArray();
                export["enemies"]=records.ToDictionary(r=>r,r=>new {Boss=EnemyDrops.IsBoss(r),Scene=EnemyDrops.SceneOf(r),Kind=EnemyDrops.KindOf(r)});
                export["basicSources"]=Shop.Basics.Select(d=>d.Type.ToString()).ToArray();
                // Export the real station compatibility table, not a second copy of
                // the recipe schedule. A crafted station must itself have a route.
                var stationSources=new Dictionary<string,HashSet<string>>();
                var stationSpecs=(System.Collections.IDictionary)typeof(Stations).GetField("specs",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null);
                foreach(var spec in stationSpecs.Values) {
                    var specType=spec.GetType();
                    var item=(ItemType)specType.GetField("Item").GetValue(spec);
                    foreach(var name in (string[])specType.GetField("Counts").GetValue(spec)) {
                        if(!stationSources.TryGetValue(name,out var set))stationSources[name]=set=new HashSet<string>();
                        if(item!=ItemType.None)set.Add(item.ToString());
                    }
                }
                export["stationSources"]=stationSources.ToDictionary(p=>p.Key,p=>p.Value.ToArray());
                // FixedStations.Ensure places both original altars at Dirtmouth.
                export["worldStationStages"]=new Dictionary<string,int>{{"恶魔祭坛 / 猩红祭坛",0}};
                foreach (var pair in ProgressionBalance.OriginalAccessories)
                {
                    var d = ItemDb.Get(pair.Key);
                    if (!d.Bonus.Equals(pair.Value.Bonus) || d.Defense != pair.Value.Defense || d.Damage != pair.Value.Damage || (d.Tooltip ?? "") != (pair.Value.Tooltip ?? ""))
                        Plugin.Log.LogError($"[AutoTest] 饰品原值 {pair.Key} 不通过");
                }
                export["accessoriesChecked"] = ProgressionBalance.OriginalAccessories.Count;
                export["milestoneMode"] = stageMilestones ? "legal-clears-within-stage-band" : "current-save";
                for (int s = 0; s < 6; s++)
                {
                    Shop.DevStage = s;
                    if (stageMilestones) VanillaBossProgression.DevClears = new HashSet<string>(VanillaBossProgression.All.Where(p => p.Value.Band <= s).Select(p => p.Key));
                    var drops = records.Where(r =>
                    {
                        if (ProgressionBalance.BossRewards.TryGetValue(r, out var boss) && ProgressionBalance.FormalBoss(r)) return boss.Stage <= s;
                        return Areas.LevelOf(EnemyDrops.SceneOf(r)) <= s;
                    }).ToDictionary(r => r, r => EnemyDrops.At(r, s, false, s >= 4, s >= 5).Concat(EnemyDrops.At(r, s, true, s >= 4, s >= 5))
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
                    var sources = new HashSet<ItemType>(Shop.Basics.Where(d=>d.Stage<=s).Select(d=>d.Type));
                    sources.UnionWith(Town.TownNpcs.DevSourcesAt(s).Select(x=>x.Type));
                    foreach (var group in drops.Values)
                        foreach (var row in group)
                            if (Enum.TryParse(row.Type, out ItemType type) && ItemDb.Get(type).Stage<=s) sources.Add(type);
                    sources.UnionWith(fish); sources.UnionWith(crateContents); sources.UnionWith(bait);
                    var worldSources=new HashSet<ItemType>();
                    var habitatSources=new HashSet<ItemType>();
                    // A species' canonical scene is not its only home. Regional materials
                    // must be audited through the same scene override real kills supply.
                    if(stageMilestones)
                        foreach(var record in records)
                            foreach(var home in BestiaryHabitats.Of(record))
                            {
                                if(Areas.LevelOf(home.Scene)>s)continue;
                                foreach(bool first in new[]{false,true})
                                    foreach(var row in EnemyDrops.At(record,s,first,s>=4,s>=5,home.Scene))
                                        if(!row.Master && row.Chance>0 && ItemDb.Get(row.Type).Stage<=s)habitatSources.Add(row.Type);
                            }
                    for(int i=0;i<AreaBlocks.All.Length;i++)
                        if(AreaBlocks.AcquisitionStage(i)<=s)worldSources.Add(AreaBlocks.BlockItem(i));
                    // These two crafting inputs are granted by the original ability pickups,
                    // rather than by ordinary enemy rolls. Do not invent duplicate kill drops.
                    if(s>=3)worldSources.Add(ItemType.CrystalHeart);
                    if(s>=5)worldSources.Add(ItemType.ShadeCloak);
                    // These are real independent kill producers, not ordinary EnemyDrops.At rows.
                    foreach (var pet in Pets.Eggs) if (!CraftOnlyLoot.Blocked(pet) && ItemDb.Get(pet).Stage <= s) worldSources.Add(pet);
                    sources.UnionWith(worldSources);
                    sources.UnionWith(habitatSources);
                    if (s>=2) sources.UnionWith(Town.Angler.DevRewardSources());
                    sources.RemoveWhere(t=>ItemDb.Get(t).Stage>s);
                    bool more = true;
                    while (more)
                    {
                        more = false;
                        foreach (var r in Recipes.All)
                            if (r.Ingredients.All(i=>sources.Contains(i.Type))) more |= sources.Add(r.Result);
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
                        Quests=s>=2 ? Town.Angler.DevRewardSources().Select(t=>t.ToString()).ToArray() : new string[0], Crates=crateContents.Select(t=>t.ToString()).ToArray(),
                        WorldSources=worldSources.Select(t=>t.ToString()).ToArray(),HabitatSources=habitatSources.Select(t=>t.ToString()).ToArray(),Health=health });
                    yield return null;
                }
                export["stages"] = stages;
                var materialChecks = new List<object>();
                GameDifficulty.Current = DifficultyLevel.Normal;
                // Each late boss pays its own materials (VanillaBossProgression: the mechanical bosses Hallowed Bars and their
                // souls, the pillars' bosses their fragments, the Golem's Beetle Husks, the Moon Lord's Luminite), each listed
                // one or one of its group at least its least amount a kill.
                foreach (var boss in ProgressionBalance.BossRewards.Where(p=>p.Value.Stage>=4 && ProgressionBalance.FormalBoss(p.Key)))
                {
                    if (!VanillaBossProgression.All.TryGetValue(boss.Key, out var profile) || profile.Materials == null || profile.Materials.Length == 0) continue;
                    Shop.DevStage = boss.Value.Stage;
                    var got = new Dictionary<ItemType,int>();
                    var oldSink = EnemyDrops.DevSink;
                    try
                    {
                        EnemyDrops.DevSink = (t,n) => got[t] = (got.TryGetValue(t, out int had) ? had : 0) + n;
                        for (int n=0;n<100;n++) EnemyDrops.Roll(boss.Key,Vector2.zero,true,false);
                    }
                    finally { EnemyDrops.DevSink=oldSink; }
                    bool pass = true;
                    var seen = new List<string>();
                    // The Ancient Manipulator is one a save (EssentialDrops), not a kill's.
                    foreach (var group in profile.Materials.Where(m => m.Type != ItemType.LunarCraftingStation).GroupBy(m => m.Group == 0 ? -(int)m.Type : m.Group))
                    {
                        int total = group.Sum(m => got.TryGetValue(m.Type, out int c) ? c : 0), least = group.Min(m => m.Min) * 100;
                        if (total < least) pass = false;
                        seen.Add($"{string.Join("/", group.Select(m => ItemDb.Get(m.Type).Name))} {total}（至少 {least}）");
                    }
                    materialChecks.Add(new {Boss=boss.Key,Trials=100,Materials=seen,Pass=pass});
                    Plugin.Log.LogInfo($"[AutoTest] Boss重复击杀材料 {boss.Key} 100次：{string.Join("，", seen)} {(pass ? "通过" : "不通过")}");
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
                export["items"] = ItemDb.All.Where(d=>d!=null).ToDictionary(d=>d.Type.ToString(), d=>new {d.Name,d.Stage,Source=d.Source.ToString(),d.Texture,d.Damage,d.Defense,d.Bonus,d.Tooltip,
                    d.IsWeapon,d.IsArmor,d.IsAccessory,d.MaxStack,d.Placeholder,Material=Recipes.IsMaterial(d.Type),Station=d.PlacesStation.ToString()});
                export["recipes"] = Recipes.All.Select(r=>new {Result=r.Result.ToString(),r.Count,r.Station,r.StationStage,Environment=r.Environment.ToString(),Ingredients=r.Ingredients.Select(i=>new {Type=i.Type.ToString(),i.Count}).ToArray()}).ToArray();
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"progression-audit.json"),JsonConvert.SerializeObject(export,Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] 阶段来源、四难度血量与 {ProgressionBalance.OriginalAccessories.Count} 个饰品原值导出通过");
            }
            finally { Shop.DevStage=oldStage; GameDifficulty.Current=oldDifficulty; UnityEngine.Random.state=rng; VanillaBossProgression.DevClears=oldClears; }
        }
    }
}
