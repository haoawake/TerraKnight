using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossLoadoutTest(bool endgame=false, bool masterAudit=false, bool forgeAudit=false, bool variantAudit=false)
        {
            if(!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            // A fresh isolated save has no world victories. Endgame equipment must be tested
            // with its acquisition milestones, rather than failing the availability checks.
            var savedEndgameClears=VanillaBossProgression.DevClears;
            if(endgame)VanillaBossProgression.DevClears=new HashSet<string>(VanillaBossProgression.All.Keys);
            int checks=0,failures=0;
            void Check(string label,bool pass,string detail="")
            {checks++;if(!pass)failures++;Plugin.Log.LogInfo($"[AutoTest] Boss配装 {label} {detail} {(pass ? "通过" : "不通过")}");}
            // Protect the trip as well as the later samples. Otherwise an early death
            // respawns in Tutorial_01 before the pinned Crossroads target is selected.
            Player.TerraStats.DevImmortal=true;PlayerData.instance.isInvincible=true;mod.DevNoFallDamage=true;
            mod.Stats.Life=mod.Stats.LifeMax;
            yield return GoTo("Crossroads_01",EntryGateOf("Crossroads_01"));
            var hero=HeroController.instance;var inv=mod.Inventory;
            var oldSlots=(ItemStack[])inv.Slots.Clone();var oldArmor=(ItemStack[])inv.Armor.Clone();var oldAcc=(ItemStack[])inv.Accessories.Clone();
            var oldDifficulty=GameDifficulty.Current;int oldStage=Shop.DevStage,oldCrystals=mod.Stats.ManaCrystals;
            bool oldInfinite=inv.Infinite,oldInvincible=PlayerData.instance.isInvincible;
            var rng=UnityEngine.Random.state;
            var target=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .FirstOrDefault(h=>!h.isDead && h.gameObject.activeInHierarchy && h.gameObject.scene.name=="Crossroads_01" && !EnemyStats.Of(h).Boss);
            Check("固定目标存在",target!=null);if(target==null)yield break;
            var info=EnemyStats.Of(target);
            int oldHp=target.hp,oldMax=info.MaxHp,oldLevel=info.Level;bool oldBoss=info.Boss;
            string oldRecord=info.Record,oldScene=info.Scene;var oldRef=info.Ref;
            var others=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .Where(h=>h!=target && h.gameObject.activeSelf && h.gameObject.scene==target.gameObject.scene
                    && !h.transform.IsChildOf(target.transform) && !target.transform.IsChildOf(h.transform)).Select(h=>h.gameObject).Distinct().ToArray();
            foreach(var go in others)go.SetActive(false);
            var fsms=target.GetComponents<PlayMakerFSM>();var fsmWas=fsms.Select(f=>f.enabled).ToArray();foreach(var f in fsms)f.enabled=false;
            var heroBody=hero.GetComponent<Rigidbody2D>();var body=target.GetComponent<Rigidbody2D>();
            var col=hero.GetComponent<BoxCollider2D>();var tc=target.GetComponent<Collider2D>();
            Vector2 offset=tc!=null ? (Vector2)tc.bounds.center-(Vector2)target.transform.position : Vector2.zero;
            hero.transform.position=Clear(hero,hero.transform.position);yield return new WaitForSeconds(.5f);Vector3 home=hero.transform.position;
            var rows=new List<object>();
            var bosses=new List<object>();
            var sequences=new List<object>();
            var endgameGodhome=new List<object>();
            if(endgame)foreach(var sequence in Resources.LoadAll<BossSequence>("GG"))
            {
                sequences.Add(new {Name=sequence.name,sequence.Count,sequence.achievementKey});
                Plugin.Log.LogInfo($"[AutoTest] 神居序列资源 {sequence.name} count={sequence.Count} achievement={sequence.achievementKey}");
            }
            string[] representative=masterAudit ? new[]{"FalseKnight","Hornet","MantisLord","BlackKnight","Hornet@4","FinalBoss"}
                : new[]{"FalseKnight","Hornet","MageLord","MimicSpider","Hornet@4","FinalBoss"};
            string[] entryBosses={"FalseKnight","Hornet","MantisLord","MegaBeamMiner","InfectedKnight","Hornet@4"};
            string WorldScene(string rec) => rec=="Hornet" ? "Fungus1_04_boss" : rec=="Grimm" ? "Grimm_Main_Tent_boss"
                : rec=="NightmareGrimm" ? "Grimm_Nightmare" : EnemyDrops.SceneOf(rec);
            var cases=new List<(int Stage,int Class,bool Entry)>();
            var variants=new List<(int Stage,int Class,bool Entry,ItemType Weapon,ItemType Whip)>();
            for(int s=0;s<6;s++)for(int c=0;c<4;c++)cases.Add((s,c,false));
            if(masterAudit) { for(int s=1;s<6;s++)for(int c=0;c<4;c++)cases.Add((s,c,true)); }
            else for(int c=0;c<4;c++)cases.Add((4,c,true));
            if(endgame){cases.Clear();for(int c=0;c<4;c++)cases.Add((5,c,false));}
            if(forgeAudit || variantAudit)
            {
                var selected=new HashSet<string>((Environment.GetEnvironmentVariable("TK_MASTER_ATTACK_CASES")??"").Split(';'));
                cases=cases.Where(r=>selected.Contains($"{r.Stage}/{r.Class}/{(r.Entry?1:0)}")).ToList();
                if(cases.Count==0)throw new InvalidOperationException("Select explicit master attack follow-up cases");
            }
            if((forgeAudit || variantAudit) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TK_MASTER_ATTACK_VARIANT_LIST")))
            {
                foreach(string value in Environment.GetEnvironmentVariable("TK_MASTER_ATTACK_VARIANT_LIST").Split(';'))
                {
                    var p=value.Split('/');
                    if(p.Length!=5)throw new InvalidOperationException("Invalid attack variant");
                    variants.Add((int.Parse(p[0]),int.Parse(p[1]),p[2]=="1",(ItemType)Enum.Parse(typeof(ItemType),p[3]),(ItemType)Enum.Parse(typeof(ItemType),p[4])));
                }
                cases=variants.Select(v=>(v.Stage,v.Class,v.Entry)).ToList();
            }
            string auditFile=forgeAudit ? "master-attack-forged" : variantAudit ? "master-attack-variants" : "master-attack";
            try
            {
                if(masterAudit)mod.ApplyCreative(false);
                // isInvincible doesn't stop Terraria damage: the pinned target's touch on Master killed the tester mid-case.
                Player.TerraStats.DevImmortal=true;
                GameDifficulty.Current=masterAudit ? DifficultyLevel.Master : DifficultyLevel.Normal;PlayerData.instance.isInvincible=true;mod.DevNoFallDamage=true;
                inv.Infinite=!masterAudit;InventoryGui.Open=false;
                foreach(var run in cases)
                {
                    int s=run.Stage,c=run.Class,gearStage=run.Entry ? (masterAudit ? s-1 : 3) : s;Shop.DevStage=masterAudit ? gearStage : s;
                    string rec=run.Entry ? (masterAudit ? entryBosses[s] : "InfectedKnight") : representative[s];
                    var excluded=new HashSet<ItemType>();
                    if(masterAudit && run.Entry && ProgressionBalance.BossRewards.TryGetValue(rec,out var targetReward))
                    { foreach(var t in targetReward.Weapons)excluded.Add(t);excluded.Add(targetReward.Accessory); }
                    Minions.Instance.Clear();PlayerBuffs.Clear();SummonTags.Clear();EnemyDebuffs.Clear();ArmorEffects.Clear();
                    TerraProjectiles.Instance.DevClear();Whips.Instance.Clear();SwordFx.Instance?.Clear();ZenithSwords.Instance?.DevClear();
                    Array.Clear(inv.Slots,0,inv.Slots.Length);inv.Selected=0;inv.Override=-1;inv.Mouse=default;inv.UseMouse=false;
                    bool Usable(ItemType t)=>masterAudit
                        ? !excluded.Contains(t) && ProgressionBalance.Unlocked(t,gearStage,gearStage>=4,false)
                        : ProgressionBalance.Unlocked(t,gearStage,!run.Entry && s>=4,endgame);
                    float ArmorScore(ProgressionBalance.ArmorPath p)
                    {
                        var b=new Bonuses();foreach(var t in p.Items)b.Add(ItemDb.Get(t).Bonus);
                        var h=ItemDb.Get(p.Items[0]);
                        if(h.Set!=ArmorSet.None && p.Items.All(t=>ItemDb.Get(t).Set==h.Set))
                            b.Add(Equipment.SetBonus(h.Set==ArmorSet.Beetle ? ItemDb.Get(p.Items[1]).SetBonus : h.SetBonus!=ArmorSet.None ? h.SetBonus : h.Set));
                        if(c==3)return (1+b.MinionDamage)*(1+b.MaxMinions);
                        float damage=c==0 ? b.MeleeDamage : c==1 ? b.RangedDamage : b.MagicDamage;
                        float crit=b.AllCrit+(c==0 ? b.MeleeCrit : c==1 ? b.RangedCrit : b.MagicCrit);
                        return (1+damage)*(1.04f+crit)*(c==0 ? 1+b.MeleeSpeed : 1);
                    }
                    var armor=ProgressionBalance.ArmorPaths.Where(p=>p.Stage<=gearStage && ProgressionBalance.ClassIndex(p.Class)==c && p.Items.All(Usable))
                        .OrderByDescending(ArmorScore).ThenByDescending(p=>p.Stage).First().Items;
                    if(endgame)armor=new[] {
                        new[]{ItemType.SolarFlareHelmet,ItemType.SolarFlareBreastplate,ItemType.SolarFlareLeggings},
                        new[]{ItemType.VortexHelmet,ItemType.VortexBreastplate,ItemType.VortexLeggings},
                        new[]{ItemType.NebulaHelmet,ItemType.NebulaBreastplate,ItemType.NebulaLeggings},
                        new[]{ItemType.StardustHelmet,ItemType.StardustBreastplate,ItemType.StardustLeggings}}[c];
                    var candidates=ProgressionBalance.AccessoryPaths.Take(gearStage+1).Reverse().SelectMany(r=>r[c]).Where(Usable).Distinct().ToArray();
                    var all=candidates.Select(t=>ItemStack.Of(t)).ToArray();
                    var accessories=candidates.Where(t=>!ProgressionBalance.Superseded(t,all)).Take(5).ToArray();
                    Equip(armor,accessories);Equipment.Update(inv);
                    Check($"原版配装阶段{s}/{c}/{run.Entry}",inv.EffectiveArmor.All(a=>a.IsEmpty || Usable(a.Type)) && inv.EffectiveAccessories.Count(a=>!a.IsEmpty)<=5);
                    int defensive=Areas.Defense(s);
                    var pool=ProgressionBalance.Core.Take(gearStage+1).SelectMany(r=>r[c]).Where(Usable).Select(ItemDb.Get);
                    var weapon=pool.Where(d=>c!=3 || d.IsSummon && !d.Sentry).OrderByDescending(d=>ProgressionBalance.WeaponDps(d,defensive)).First();
                    var variant=variants.Count>0 ? variants[rows.Count] : default;
                    if(variant.Weapon!=ItemType.None)
                    {
                        if(!Usable(variant.Weapon) || ProgressionBalance.ClassIndex(ItemDb.Get(variant.Weapon).Class)!=c)throw new InvalidOperationException("Unavailable variant weapon");
                        weapon=ItemDb.Get(variant.Weapon);
                    }
                    if(endgame)weapon=ItemDb.Get(new[]{ItemType.Zenith,ItemType.CelebrationMk2,ItemType.LastPrism,ItemType.StardustDragonStaff}[c]);
                    var summon=weapon;
                    var whip=c==3 ? pool.Where(d=>d.IsWhip).OrderByDescending(d=>d.Stage).ThenByDescending(d=>d.Damage).FirstOrDefault() : null;
                    if(variant.Whip!=ItemType.None)
                    {
                        if(!Usable(variant.Whip) || !ItemDb.Get(variant.Whip).IsWhip)throw new InvalidOperationException("Unavailable variant whip");
                        whip=ItemDb.Get(variant.Whip);
                    }
                    if((forgeAudit || variantAudit) && c==3 && Enum.TryParse<ItemType>(Environment.GetEnvironmentVariable("TK_MASTER_ATTACK_WHIP"),out var alternate)
                        && ItemDb.Get(alternate).IsWhip && Usable(alternate))whip=ItemDb.Get(alternate);
                    var ammo=weapon.UseAmmo==AmmoKind.None ? null : ItemDb.All.Where(d=>d.Ammo==weapon.UseAmmo && Usable(d.Type)).OrderByDescending(d=>d.Damage).FirstOrDefault();
                    inv.Slots[0]=ItemStack.Of(weapon.Type,weapon.MaxStack>1 ? 999 : 1);
                    if(forgeAudit && Forging.CanForge(weapon))inv.Slots[0].Forge=(byte)Forging.MaxLevelOf(weapon.Type);
                    int summonForge=inv.Slots[0].Forge;
                    if(ammo!=null)inv.Slots[Inventory.Size-1]=ItemStack.Of(ammo.Type,9999);
                    // Opt-in resource-complete follow-up: mana is consumed normally,
                    // including potion sickness, rather than silently refilled.
                    if(variantAudit && Environment.GetEnvironmentVariable("TK_MASTER_ATTACK_MANA_POTIONS")=="1")
                        inv.Slots[Inventory.Size-2]=ItemStack.Of(gearStage>=4 ? ItemType.GreaterManaPotion : ItemType.ManaPotion,30);
                    mod.Stats.ManaCrystals=new[]{1,2,3,5,7,9}[gearStage];mod.Stats.UpdateMaximums(PlayerData.instance,Equipment.Current.ManaMax);mod.Stats.Mana=mod.Stats.ManaMax;
                    int at=rec.IndexOf('@');string key=at<0 ? rec : rec.Substring(0,at);
                    info.Record=key;info.Scene=masterAudit ? WorldScene(rec) : EnemyDrops.SceneOf(rec);info.Ref=BossStats.Of(key,at<0 ? -1 : int.Parse(rec.Substring(at+1)));info.Boss=true;info.Level=s;info.MaxHp=50000;info.Carry=0;target.hp=50000;target.IsInvincible=false;
                    Vector2 aim=(Vector2)col.bounds.center+Vector2.right*(c==0 ? 1f : 3f);
                    if(c==2 && s==5)aim+=Vector2.up*1.5f;
                    Pin(target,body,aim-offset);mod.DevAim=aim;mod.DevHold=weapon.Channel ? true : (bool?)null;
                    yield return null;
                    if(endgame && c==1){ArmorEffects.KeyDoubleTapDown();yield return new WaitForSeconds(.6f);}
                    if(c==3)
                    {
                        for(int n=0;n<Equipment.MaxMinions;n++)
                        {while(!mod.DevReady){Pin(target,body,aim-offset);yield return null;}mod.DevUseSelected(col.bounds.center);yield return null;}
                        mod.DevAimMinions(summon.Type,aim);
                        if(whip!=null){weapon=whip;inv.Slots[0]=ItemStack.Of(whip.Type);if(forgeAudit && Forging.CanForge(whip))inv.Slots[0].Forge=(byte)Forging.MaxLevelOf(whip.Type);aim=(Vector2)col.bounds.center+Vector2.right*1.2f;}
                    }
                    float duration=masterAudit ? (c==2 ? 20f : 12f) : (c==2 ? 24f : 16f),burst=masterAudit ? 6f : 8f,start=Time.time;
                    double firstDamage=0,lastDamage=0;int casts=0,manaBefore=mod.Stats.Mana;
                    UnityEngine.Random.InitState(73100+s*11+c+(run.Entry?100:0));
                    while(Time.time-start<duration)
                    {
                        hero.transform.position=home;heroBody.linearVelocity=Vector2.zero;Pin(target,body,aim-offset);mod.DevAim=aim;
                        target.hp=50000;float scale=info.Scale;float carry=info.Carry;
                        if(mod.DevReady){mod.DevUseSelected(aim);casts++;}
                        yield return null;
                        // Refilling native HP avoids target death; carry keeps sub-HP hits exact.
                        double damage=Math.Max(0,(50000-target.hp+info.Carry-carry)*scale);
                        if(Time.time-start<=burst)firstDamage+=damage;else lastDamage+=damage;
                    }
                    float elapsed=Time.time-start;
                    // Why a blow took nothing off (the guards a world boss's hit goes through), when none did.
                    if(firstDamage<=0 && target!=null)Plugin.Log.LogInfo($"[AutoTest] Boss配装 没打掉血：阶段{s}/{c} 场地外 {World.ArenaGuard.Outside(target)} 地图外 {World.ArenaGuard.OffMap(target)} 唤醒中 {BossWake.Asleep(target)} 可操作 {GameHooks.PlayerHasControl()} 无敌 {target.IsInvincible} 碰不到 {EnemyStats.Untouchable(target)} 打倒 {EnemyStats.Defeated(target)} 出招 {casts} 场景 {info.Scene}/{target.gameObject.scene.name}");
                    float first=(float)firstDamage/burst,last=(float)lastDamage/(elapsed-burst);
                    Check($"实际输出阶段{s}/{c}/{run.Entry}",first>0 && last>=0 && !float.IsNaN(first),$"{weapon.Type} burst={first:F1} sustained={last:F1} mana={manaBefore}->{mod.Stats.Mana}");
                    int budget=EnemyStats.LifeWhole(info);
                    rows.Add(new {Stage=s,GearStage=gearStage,Class=c,Entry=run.Entry,Representative=rec,Difficulty=GameDifficulty.Current.ToString(),Defense=info.Defense,WholeBossLife=budget,
                        IdealBurstBudgetSeconds=first>0 ? budget/first : (float?)null,IdealSustainedBudgetSeconds=last>0 ? budget/last : (float?)null,
                        Weapon=weapon.Type.ToString(),WeaponBaseDamage=weapon.Damage,WeaponGearDamage=Equipment.Damage(inv.Slots[0].Def,ammo),WeaponForge=inv.Slots[0].Forge,SummonForge=c==3?summonForge:0,Summon=c==3 ? summon.Type.ToString() : null,
                        Ammo=ammo?.Type.ToString(),Armor=armor.Select(t=>t.ToString()).ToArray(),Accessories=accessories.Select(t=>t.ToString()).ToArray(),Equipment=Equipment.Current,
                        Minions=Equipment.MaxMinions,Seconds=elapsed,BurstDps=first,SustainedDps=last,ManaBefore=manaBefore,ManaAfter=mod.Stats.Mana,Casts=casts});
                    if(masterAudit)Plugin.Log.LogInfo($"[AutoTest] 大师输出进度 {rows.Count}/{cases.Count} stage={s} gear={gearStage} class={c} entry={run.Entry} boss={rec} hp={budget} defense={info.Defense} burst={first:F1} sustained={last:F1}");
                    if(masterAudit)File.WriteAllText(Path.Combine(DevCopy.SaveDir,auditFile+"-progress.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failures,Profiles=rows},Formatting.Indented));
                    mod.DevHold=null;mod.DevAim=null;yield return new WaitForSeconds(.5f);
                }
                foreach(var pair in BossStats.DevAll)
                {
                    string rec=pair.Key;int at=rec.IndexOf('@');string key=at<0 ? rec : rec.Substring(0,at);
                    string scene=masterAudit ? WorldScene(rec) : EnemyDrops.SceneOf(rec);int s=pair.Value.Stage;
                    var tune=BossBalance.Of(key,at<0?s:int.Parse(rec.Substring(at+1)));
                    foreach(DifficultyLevel difficulty in Enum.GetValues(typeof(DifficultyLevel)))
                    {
                        GameDifficulty.Current=difficulty;
                        int life=Mathf.CeilToInt(BossStats.LifeOf(pair.Value,scene,difficulty)*tune.Life*BossPhases.WholeFactor(key,scene));
                        bosses.Add(new {Record=rec,Name=Bestiary.NameOf(key),Stage=s,Difficulty=difficulty.ToString(),Life=life,Defense=EnemyStats.BossDefense(pair.Value,tune),
                            PhaseFactor=BossPhases.WholeFactor(key,scene),CombatShare=BossStats.CombatLifeShare(s)});
                        if(endgame)
                        {
                            int godhome=Mathf.CeilToInt(BossStats.LifeOf(pair.Value,"GG_",difficulty)*tune.Life*BossPhases.WholeFactor(key,"GG_"));
                            bosses.Add(new {Record=rec,Name=Bestiary.NameOf(key),Stage=s,Location="GodhomeAttuned",ProfileStage=5,Difficulty=difficulty.ToString(),Life=godhome,
                                Defense=EnemyStats.BossDefense(pair.Value,tune),PhaseFactor=BossPhases.WholeFactor(key,"GG_"),CombatShare=BossStats.CombatLifeShare(5)});
                        }
                    }
                }
                if(endgame)
                {
                    Shop.DevStage=5;
                    foreach(var pair in BossStats.DevAll)
                    {
                        var r=pair.Value;
                        string scene=pair.Key=="Hornet@4"?"GG_Hornet_2":BestiaryHabitats.Of(pair.Key).FirstOrDefault(h=>Areas.GodhomeRoom(h.Scene)).Scene;
                        if(scene==null)continue;
                        var tune=BossBalance.Of(r.Record,EnemyDrops.VariantLevel(scene));
                        foreach(var difficulty in new[]{DifficultyLevel.Normal,DifficultyLevel.Master})
                        {
                            GameDifficulty.Current=difficulty;
                            int baseDefense=EnemyStats.BossDefense(r,tune,scene);
                            foreach(var p in GodhomeBalance.Hall.Concat(GodhomeBalance.Pantheons))
                                endgameGodhome.Add(new {Record=pair.Key,Scene=scene,Difficulty=difficulty.ToString(),Profile=p.Key,
                                    Life=Mathf.RoundToInt(BossStats.GodhomeLifeOf(r,scene,difficulty,p)*tune.Life*BossPhases.WholeFactor(r.Record,scene)),
                                    Defense=GodhomeBalance.Defense(baseDefense,p),
                                    RawHit=Mathf.RoundToInt(Areas.MasterDamagePerMask(5)*GameDifficulty.FactorOf(difficulty)*Areas.BossDamage*tune.Damage*p.Damage)});
                        }
                    }
                }
            }
            finally
            {
                VanillaBossProgression.DevClears=savedEndgameClears;
                Player.TerraStats.DevImmortal=false;
                mod.DevHold=null;mod.DevAim=null;mod.DevNoFallDamage=false;Minions.Instance.Clear();TerraProjectiles.Instance.DevClear();Whips.Instance.Clear();SwordFx.Instance?.Clear();ZenithSwords.Instance?.DevClear();PlayerBuffs.Clear();ArmorEffects.Clear();
                if(target!=null){target.hp=oldHp;info.MaxHp=oldMax;info.Level=oldLevel;info.Boss=oldBoss;info.Record=oldRecord;info.Scene=oldScene;info.Ref=oldRef;info.Carry=0;for(int i=0;i<fsms.Length;i++)if(fsms[i]!=null)fsms[i].enabled=fsmWas[i];}
                foreach(var go in others)if(go!=null)go.SetActive(true);
                Array.Copy(oldSlots,inv.Slots,oldSlots.Length);Array.Copy(oldArmor,inv.Armor,oldArmor.Length);Array.Copy(oldAcc,inv.Accessories,oldAcc.Length);
                inv.Infinite=oldInfinite;GameDifficulty.Current=oldDifficulty;Shop.DevStage=oldStage;mod.Stats.ManaCrystals=oldCrystals;PlayerData.instance.isInvincible=oldInvincible;
                Equipment.Update(inv);mod.Stats.UpdateMaximums(PlayerData.instance,Equipment.Current.ManaMax);UnityEngine.Random.state=rng;
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,masterAudit ? auditFile+"-audit.json" : "boss-loadouts.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failures,Profiles=rows,Bosses=bosses,EndgameGodhome=endgameGodhome,Sequences=sequences,Endgame=endgame,MasterAudit=masterAudit,Variants=variantAudit,FixedTarget=true,InfiniteAmmo=!masterAudit,RefilledMana=false,AccessorySlots=5,Potions=false,ManaPotionSupply=variantAudit && Environment.GetEnvironmentVariable("TK_MASTER_ATTACK_MANA_POTIONS")=="1" ? 30 : 0,Prefixes=false,Forging=forgeAudit},Formatting.Indented));
            Check("汇总",failures==0 && rows.Count==((forgeAudit || variantAudit)?cases.Count:endgame?4:masterAudit?44:28),$"profiles={rows.Count} bosses={bosses.Count}");
        }
    }
}
