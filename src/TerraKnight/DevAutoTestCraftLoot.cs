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
        IEnumerator CraftLootTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            yield return GoTo("Town", "left1");
            int stageWas = Shop.DevStage, checks = 0, failed = 0, sampled = 0;
            var difficultyWas = GameDifficulty.Current;
            var rng = UnityEngine.Random.state;
            var sink = EnemyDrops.DevSink;
            bool manipulatorWas = EssentialDrops.ManipulatorGiven;
            var bosses = new List<object>();
            var tables = new List<object>();
            var shopEquipment=new HashSet<ItemType>(Enumerable.Range(0,6).SelectMany(s=>Town.TownNpcs.DevSourcesAt(s))
                .Select(s=>s.Type).Where(t=> {
                    var d=ItemDb.Get(t);return d.IsWeapon || d.IsArmor || d.IsAccessory || d.GameTool
                        || d.PickPower>0 || d.AxePower>0 || d.HammerPower>0 || d.FishingPower>0;
                }));
            var equipmentOwners=new Dictionary<ItemType,string>();
            void Check(bool ok, string message)
            {
                checks++;
                if (!ok) { failed++; Plugin.Log.LogError("[AutoTest] CraftLoot " + message + " 不通过"); }
            }
            try
            {
                Check(shopEquipment.Contains(ItemType.FinchStaff),"merchant FinchStaff fixture");
                UnityEngine.Random.InitState(102122);
                GameDifficulty.Current = DifficultyLevel.Normal;
                for (int stage = 0; stage <= 5; stage++)
                {
                    Shop.DevStage = stage;
                    for(int sample=0;sample<30;sample++)
                    {
                        var gift=ProgressionBalance.ExplorationMaterialGift(stage);
                        Check(!CraftOnlyLoot.OnlyCrafted(gift.Type) && ItemDb.Get(gift.Type).Stage<=stage
                            && gift.Type!=ItemType.LunarOre,"exploration gift "+gift.Type);
                    }
                    foreach (var rec in EnemyDrops.DevRecords)
                    {
                        bool boss = ProgressionBalance.FormalBoss(rec);
                        foreach (bool godhome in boss ? new[] { false, true } : new[] { false })
                        foreach (bool first in new[] { false, true })
                        {
                            string scene = godhome ? "GG_Workshop" : BestiaryHabitats.WorldScene(rec) ?? EnemyDrops.SceneOf(rec);
                            var rows = EnemyDrops.At(rec, stage, first, true, true, scene);
                            bool inGodhome=ProgressionLoot.GodhomeScene(scene);
                            int tier = boss && !inGodhome ? ProgressionBalance.BossRewards[rec].Stage : stage;
                            foreach (var row in rows)
                            {
                                Check(!CraftOnlyLoot.OnlyCrafted(row.Type), $"crafted table {rec}/{stage}/{godhome}/{row.Type}");
                                Check(!shopEquipment.Contains(row.Type),$"shop equipment table {rec}/{stage}/{row.Type}");
                                var d = ItemDb.Get(row.Type);
                                // Developer items are every hardmode boss's (Terraria's hardmode treasure bags), not one boss's own.
                                bool developer = System.Array.IndexOf(BossDropPlan.DeveloperItems, row.Type) >= 0;
                                if(boss && first && !godhome && !developer && stage==ProgressionBalance.BossRewards[rec].Stage
                                    && (ProgressionLoot.CombatWeapon(d) || d.IsArmor || d.IsAccessory))
                                {
                                    Check(!equipmentOwners.TryGetValue(row.Type,out var owner) || owner==rec,
                                        $"duplicate boss equipment {row.Type}/{rec}/{owner}");
                                    equipmentOwners[row.Type]=rec;
                                }
                                if(inGodhome && !row.Master)
                                    Check(d.Stage<=stage, $"Godhome item tier {rec}/{stage}/{row.Type}/{d.Stage}");
                                if (boss && !developer && (ProgressionLoot.CombatWeapon(d) || d.IsArmor || d.IsAccessory))
                                    Check(d.Stage == tier, $"boss equipment tier {rec}/{stage}/{godhome}/{row.Type}/{d.Stage}");
                            }
                            if (boss && first) tables.Add(new { Record = rec, Stage = stage, Godhome = inGodhome, Scene=scene, Items = rows.Select(r => r.Type.ToString()).ToArray() });
                        }
                        if (boss)
                        {
                            var selected = BossDropPlan.Weapons(rec, stage);
                            if (ProgressionBalance.BossRewards[rec].Stage == stage)
                            {
                                // A boss's own weapons are of its own band; a boss need not give every class one (the mechanical
                                // bosses and the pillars pay in their materials), but every boss pays something of its own.
                                foreach (var t in selected.Where(t => t != ItemType.None))
                                    Check(BossDropPlan.NaturalEquipment(t) && ItemDb.Get(t).Stage == stage, $"plan {rec}/{stage}/{t}");
                                var p = VanillaBossProgression.All[rec];
                                Check((p.Gear?.Length ?? 0) > 0 || p.Signature != ItemType.None || (p.Materials?.Length ?? 0) > 0, "boss pays something of its own " + rec);
                                bosses.Add(new { Record = rec, Stage = stage, Weapons = selected.Select(t => t.ToString()).ToArray(), Signature = BossDropPlan.Signature(rec).ToString() });
                            }
                        }
                    }
                    // A boss's own gear only from that boss (Terraria's way: fight it again), never from a common enemy.
                    foreach (var rec in EnemyDrops.DevRecords)
                    {
                        if (EnemyDrops.EntryFor(rec).Boss) continue;
                        var leaked = EnemyDrops.At(rec, stage, false, true, true).Where(r => BossDropPlan.BossGear(r.Type)).Select(r => r.Type.ToString()).ToArray();
                        Check(leaked.Length == 0, $"no boss gear from {rec}/{stage}: {string.Join(",", leaked)}");
                    }
                    // Actual crate generation, including special pieces and area extras.
                    foreach (var d in ItemDb.All.Where(d => Fishing.IsCrate(d.Type)))
                        for (int sample = 0; sample < 30; sample++)
                            foreach (var stack in Fishing.DevContents(d.Type))
                                Check(!CraftOnlyLoot.Blocked(stack.Type) && !shopEquipment.Contains(stack.Type) && !BossDropPlan.BossOnly(stack.Type), "crate " + d.Type + "/" + stack.Type);
                    // Each area's chests (weapons, accessories, supplies): no boss's own item either.
                    foreach (var a in World.AreaBlocks.All)
                        for (int part = 0; part < 3; part++)
                            for (int sample = 0; sample < 20; sample++)
                                foreach (var stack in World.RegionalChests.Contents(a.Name, part, stage))
                                    if (!stack.IsEmpty) Check(!BossDropPlan.BossOnly(stack.Type), "area chest " + a.Name + "/" + part + "/" + stage + "/" + stack.Type);
                    for(int sample=0;sample<100;sample++)
                    {
                        var chest=new List<ItemStack>();
                        FishingChest.Contents(chest);
                        foreach(var stack in chest)Check(!CraftOnlyLoot.Blocked(stack.Type) && !shopEquipment.Contains(stack.Type) && !BossDropPlan.BossOnly(stack.Type),"fishing chest "+stack.Type);
                        for(int door=1;door<=5;door++)
                            foreach(var stack in PantheonRewards.Contents(door,stage))
                                Check(!CraftOnlyLoot.Blocked(stack.Type) && !shopEquipment.Contains(stack.Type) && !BossDropPlan.BossOnly(stack.Type) && ItemDb.Get(stack.Type).Stage<=stage,"pantheon "+door+"/"+stage+"/"+stack.Type);
                    }
                    // Exercise the runtime payout and bag-opening entry with the real item DB.
                    foreach (var pair in ProgressionBalance.BossRewards.Where(p => ProgressionBalance.FormalBoss(p.Key)))
                    {
                        EnemyDrops.DevSink = (type, count) => { sampled++; Check(!CraftOnlyLoot.Blocked(type) && !shopEquipment.Contains(type), "paid " + pair.Key + "/" + type); };
                        foreach (var level in new[] { DifficultyLevel.Normal, DifficultyLevel.Expert, DifficultyLevel.Master })
                        {
                            GameDifficulty.Current = level;
                            EnemyDrops.Roll(pair.Key, Vector2.zero, direct: true, firstKill: true, scene: "GG_Workshop");
                            EnemyDrops.Roll(pair.Key, Vector2.zero, direct: true, firstKill: true, fromBag: true, scene: "GG_Workshop");
                            BossPotions.AtPhaseEnd(new EnemyStats.Info { Record=pair.Key, Scene="GG_Workshop",
                                Ref=BossStats.Of(pair.Key,pair.Value.Stage), Boss=true },Vector2.zero);
                        }
                        foreach (var bonus in Enumerable.Range(1, 8).Select(i => GodhomeRewards.OfIndex(i)))
                            for (int sample = 0; sample < 30; sample++)
                            {
                                var t = GodhomeRewards.RollReward(bonus,pair.Key);
                                Check(t == ItemType.None || !CraftOnlyLoot.Blocked(t) && !shopEquipment.Contains(t)
                                    && ItemDb.Get(t).Stage == stage && BossDropPlan.Owns(pair.Key,t,stage), "Godhome bonus " + t);
                            }
                    }
                    EnemyDrops.DevSink = sink;
                    GameDifficulty.Current = DifficultyLevel.Normal;
                    yield return null;
                }
                Check(!equipmentOwners.ContainsKey(ItemType.FinchStaff),"no FinchStaff boss owner");
                var report = new { Checks = checks, Failed = failed, SampledPayouts = sampled,
                    ShopEquipment=shopEquipment.Select(t=>t.ToString()).OrderBy(t=>t).ToArray(),
                    EquipmentOwners=equipmentOwners.ToDictionary(p=>p.Key.ToString(),p=>p.Value), Bosses = bosses, Tables = tables };
                File.WriteAllText(Path.Combine(DevCopy.SaveDir, "craft-loot-report.json"), JsonConvert.SerializeObject(report, Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] CraftLoot {checks-failed}/{checks}; paid={sampled} {(failed == 0 ? "通过" : "不通过")}");
            }
            finally
            {
                Shop.DevStage = stageWas;
                GameDifficulty.Current = difficultyWas;
                UnityEngine.Random.state = rng;
                EnemyDrops.DevSink = sink;
                GodhomeRewards.Active = null;
                if(!manipulatorWas)TerraMod.Instance.GrantedAbilities.Remove(EssentialDrops.ManipulatorKey);
            }
        }
    }
}
