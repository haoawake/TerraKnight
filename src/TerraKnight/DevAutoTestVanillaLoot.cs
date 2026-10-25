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
        IEnumerator VanillaAttackTest()
        {
            var old=VanillaBossProgression.DevClears;
            string oldCases=Environment.GetEnvironmentVariable("TK_MASTER_ATTACK_CASES"),oldVariants=Environment.GetEnvironmentVariable("TK_MASTER_ATTACK_VARIANT_LIST");
            try
            {
                VanillaBossProgression.DevClears=new HashSet<string>(VanillaBossProgression.All.Keys);
                Environment.SetEnvironmentVariable("TK_MASTER_ATTACK_CASES","0/0/0;2/1/0;4/1/0");
                Environment.SetEnvironmentVariable("TK_MASTER_ATTACK_VARIANT_LIST","0/0/0/WoodenSword/None;0/0/0/BatBat/None;2/1/0/Handgun/None;2/1/0/PhoenixBlaster/None;4/1/0/AdamantiteRepeater/None;4/1/0/HallowedRepeater/None;4/1/0/Megashark/None");
                yield return BossLoadoutTest(masterAudit:true,variantAudit:true);
                var audit=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(DevCopy.SaveDir,"master-attack-variants-audit.json")));
                var samples=audit["Profiles"].ToDictionary(r=>(string)r["Weapon"],r=>(float)r["SustainedDps"]);
                foreach(var upgrade in new[]{("WoodenSword","BatBat"),("Handgun","PhoenixBlaster"),("AdamantiteRepeater","HallowedRepeater"),("AdamantiteRepeater","Megashark")})
                {
                    bool pass=samples[upgrade.Item2]>samples[upgrade.Item1]*1.05f;
                    Plugin.Log.LogInfo($"[AutoTest] 实际升级 {upgrade.Item1} {samples[upgrade.Item1]:F1} -> {upgrade.Item2} {samples[upgrade.Item2]:F1} {(pass?"通过":"不通过")}");
                }
            }
            finally
            {
                VanillaBossProgression.DevClears=old;
                Environment.SetEnvironmentVariable("TK_MASTER_ATTACK_CASES",oldCases);Environment.SetEnvironmentVariable("TK_MASTER_ATTACK_VARIANT_LIST",oldVariants);
            }
        }
        IEnumerator VanillaLootTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated saves required");
            yield return GoTo("Town","left1");
            var old=VanillaBossProgression.DevClears;int oldStage=Shop.DevStage;
            var difficulty=GameDifficulty.Current;var sink=EnemyDrops.DevSink;var rng=UnityEngine.Random.state;
            int checks=0,failed=0;var rows=new List<object>();var owners=new Dictionary<ItemType,string>();var mountOwners=new Dictionary<ItemType,string>();
            void Check(bool pass,string text){checks++;if(!pass){failed++;Plugin.Log.LogError("[AutoTest] VanillaLoot "+text+" 不通过");}}
            try
            {
                VanillaBossProgression.DevClears=new HashSet<string>();Shop.DevStage=5;
                GameDifficulty.Current=DifficultyLevel.Normal;
                foreach(var pair in VanillaBossProgression.All)
                {
                    var key=pair.Key;var p=pair.Value;var entry=EnemyDrops.EntryFor(key);
                    Check(ProgressionBalance.FormalBoss(key),"registered "+key);
                    Check(VanillaBossProgression.Combat.ContainsKey(key)!=VanillaBossProgression.GodhomeFights.ContainsKey(key),"combat "+key);
                    foreach(var t in p.Gear.Concat(p.Signature==ItemType.None?Array.Empty<ItemType>():new[]{p.Signature}))
                    {
                        Check(BossDropPlan.NaturalEquipment(t),"legal authored gear "+key+"/"+t+" source="+ItemDb.Get(t).Source);
                        Check(!owners.ContainsKey(t),"unique owner "+t);owners[t]=key;
                    }
                    VanillaBossProgression.DevClears.Clear();
                    VanillaBossProgression.Credit(key,"GG_Workshop");
                    Check(VanillaBossProgression.StateKey==0,"early Godhome credit "+key);
                    var early=EnemyDrops.At(key,5,true,true,true,"GG_Workshop");
                    Check(!early.Any(d=>VanillaBossProgression.BossMaterial(d.Type)),"early Godhome material "+key);
                    Check(!early.Any(d=>ItemDb.Get(d.Type).IsMount),"early Godhome mount "+key);
                    string scene=BestiaryHabitats.WorldScene(key)??entry.Scene;
                    // Four pillar stand-ins are Godhome native. Credit only after the Cultist.
                    if(p.Step==VanillaBossProgression.Step.Pillars)VanillaBossProgression.DevClears.Add("HollowKnight");
                    VanillaBossProgression.Credit(key,scene);
                    var drops=EnemyDrops.At(key,p.Band,true,true,true,scene);
                    foreach(var t in drops.Select(d=>d.Type).Distinct().Where(t=>ItemDb.Get(t).IsMount))
                    {Check(!mountOwners.ContainsKey(t),"unique boss mount "+t);mountOwners[t]=key;}
                    Check(drops.Any(d=>p.Gear.Contains(d.Type)||p.Signature==d.Type||p.Materials.Any(m=>m.Type==d.Type)),"meaningful package "+key);
                    foreach(var m in p.Materials)Check(drops.Any(d=>d.Type==m.Type&&d.Min==m.Min&&d.Max==m.Max),"material quantities "+key+"/"+m.Type);
                    var paid=new Dictionary<ItemType,int>();EnemyDrops.DevSink=(t,n)=>paid[t]=paid.TryGetValue(t,out int v)?v+n:n;
                    for(int i=0;i<20;i++)EnemyDrops.Roll(key,Vector2.zero,true,true,scene:scene);
                    Check(paid.Count>0,"runtime payout "+key);
                    foreach(var t in paid.Keys)Check(!CraftOnlyLoot.Blocked(t),"runtime blocked "+key+"/"+t);
                    var r=BossStats.Of(key,p.Band);
                    // The Godhome-only gods: their stage-5 fight; defense and damage are Godhome's (no world values).
                    var c=VanillaBossProgression.GodhomeFights.TryGetValue(key,out int fight)?(Life:fight,Defense:0,Damage:0f):VanillaBossProgression.Combat[key];
                    float total=BossStats.LifeOf(r,scene,DifficultyLevel.Master)*BossBalance.Of(r.Record,EnemyDrops.VariantLevel(scene)).Life*BossPhases.WholeFactor(r.Record,scene);
                    // False Knight's later armour premium is intentionally outside
                    // the original normalized budget; include it in this assertion.
                    float phasePremium=BossPhases.WholeFactor(r.Record,scene)/BossPhases.BudgetFactor(r.Record,scene);
                    // Crediting a pillar advances Godhome to the 1.5x late-progress budget.
                    // The authored GodhomeFights values describe the baseline before that progression.
                    float progress=VanillaBossProgression.Current>=VanillaBossProgression.Step.MoonLord?2.25f
                        :VanillaBossProgression.Current>=VanillaBossProgression.Step.Pillars?1.5f
                        :VanillaBossProgression.Current>=VanillaBossProgression.Step.Cultist?1.2f:1f;
                    float expected=(BossStats.InGodhome(scene)?c.Life*GodhomeBalance.Current(scene).Life*progress:c.Life)*phasePremium;
                    Check(Math.Abs(total-expected)<30,"whole phase health "+key+"/"+total+" expected="+expected);
                    if(!BossStats.InGodhome(scene))Check(EnemyStats.BossDefense(r,BossBalance.Of(r.Record,p.Band),scene)==c.Defense,"effective defense "+key);
                    rows.Add(new {Boss=key,p.Role,Stage=p.Band,Milestone=p.Step.ToString(),MasterLife=c.Life,EffectiveMasterLife=total,Godhome=BossStats.InGodhome(scene),Defense=c.Defense,DamageMultiplier=c.Damage,p.Upgrade,
                        Gear=p.Gear.Select(t=>new{Item=t.ToString(),ItemDb.Get(t).Damage,ItemDb.Get(t).Defense}).ToArray(),Signature=p.Signature.ToString(),
                        Materials=p.Materials.Select(m=>new{Item=m.Type.ToString(),m.Min,m.Max}).ToArray(),Drops=drops.Select(d=>d.Type.ToString()).ToArray()});
                    yield return null;
                }
                string[] mechs=VanillaBossProgression.Mechs;ItemType[] souls={ItemType.SoulofMight,ItemType.SoulofSight,ItemType.SoulofFright};
                foreach(int a in new[]{0,1,2})foreach(int b in new[]{0,1,2}.Where(i=>i!=a))
                {
                    int third=3-a-b;VanillaBossProgression.DevClears.Clear();VanillaBossProgression.DevClears.Add("BlackKnight");
                    foreach(int index in new[]{a,b,third})
                    {
                        Check(!VanillaBossProgression.Allowed(souls[index]),"unearned soul "+index);
                        VanillaBossProgression.Credit(mechs[index],BestiaryHabitats.WorldScene(mechs[index])??"test_world");
                        int count=VanillaBossProgression.DevClears.Count-1;
                        Check(VanillaBossProgression.Current==(VanillaBossProgression.Step)((int)VanillaBossProgression.Step.MechOne+count-1),"mech milestone "+a+b+count);
                        for(int j=0;j<3;j++)Check(VanillaBossProgression.Allowed(souls[j])==VanillaBossProgression.Cleared(mechs[j]),"soul owner "+j);
                        Check(VanillaBossProgression.Allowed(ItemType.ChlorophyteOre)==(count==3),"chlorophyte requires three");
                    }
                }
                VanillaBossProgression.DevClears.Clear();VanillaBossProgression.DevClears.Add("BlackKnight");VanillaBossProgression.DevClears.Add("WhiteDefender");
                Check(VanillaBossProgression.Current==VanillaBossProgression.Step.Hardmode,"Fishron does not skip mechs");
                foreach(var t in new[]{ItemType.FragmentSolar,ItemType.FragmentVortex,ItemType.FragmentNebula,ItemType.FragmentStardust,ItemType.LunarOre})Check(!VanillaBossProgression.Allowed(t),"late material gate "+t);
                VanillaBossProgression.DevClears.UnionWith(VanillaBossProgression.All.Keys);
                foreach(var key in VanillaBossProgression.All.Keys)
                    for(int stage=0;stage<6;stage++)
                        foreach(var d in EnemyDrops.At(key,stage,true,true,true,"GG_Workshop"))
                            Check(d.Master||d.Type==ItemType.Heart||d.Type==ItemType.LifeCrystal||ItemDb.Get(d.Type).Stage<=stage,"Godhome ability cap "+key+"/"+stage+"/"+d.Type);
                foreach(bool progressed in new[]{false,true})
                {
                    VanillaBossProgression.DevClears.Clear();
                    if(progressed)VanillaBossProgression.DevClears.UnionWith(VanillaBossProgression.All.Keys);
                    for(int stage=0;stage<6;stage++)
                    {
                        Check(VanillaBossProgression.Allowed(ProgressionBalance.ExplorationMaterialGift(stage).Type),"exploration material gate "+stage);
                        for(int door=1;door<6;door++)
                        {
                            var contents=PantheonRewards.Contents(door,stage);
                            Check(contents.Count>0,"nonempty pantheon "+door+"/"+stage);
                            foreach(var item in contents)Check(ProgressionBalance.Unlocked(item.Type,stage,false,false)&&!CraftOnlyLoot.Blocked(item.Type),"pantheon material gate "+item.Type);
                        }
                    }
                }
                VanillaBossProgression.DevClears.UnionWith(VanillaBossProgression.All.Keys);
                foreach(var rec in EnemyDrops.DevRecords.Where(r=>!ProgressionBalance.FormalBoss(r)))
                    for(int stage=0;stage<6;stage++)
                        foreach(var d in EnemyDrops.At(rec,stage,false,true,true))
                        {
                            Check(!VanillaBossProgression.BossMaterial(d.Type),"mob boss material "+rec+"/"+d.Type);
                            Check(!CraftOnlyLoot.Blocked(d.Type),"mob crafted/shop equipment "+rec+"/"+stage+"/"+d.Type);
                        }
                foreach(var order in new[]{new[]{"BigBuzzer","BigFly","FalseKnight","Mawlek"},new[]{"MageKnight","FlukeMother","DungDefender","GhostHu","MantisLord","MageLord","MegaBeamMiner","MegaJellyfish","FalseKnight@5"},new[]{"Oblobble","HiveKnight","Hornet@4","GhostMarkoth","InfectedKnight@5"}})
                    for(int i=1;i<order.Length;i++)
                    {
                        var before=VanillaBossProgression.Combat[order[i-1]];var after=VanillaBossProgression.Combat[order[i]];
                        Check(after.Life>before.Life&&after.Defense>=before.Defense,"substage strength "+order[i]);
                    }
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"vanilla-loot-report.json"),JsonConvert.SerializeObject(new{Checks=checks,Failed=failed,Bosses=rows},Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] VanillaLoot {checks-failed}/{checks} {(failed==0?"通过":"不通过")}");
                EnemyDrops.DevSink=sink;
                yield return VanillaRecipesTest();
            }
            finally {VanillaBossProgression.DevClears=old;Shop.DevStage=oldStage;GameDifficulty.Current=difficulty;EnemyDrops.DevSink=sink;UnityEngine.Random.state=rng;}
        }
    }
}
