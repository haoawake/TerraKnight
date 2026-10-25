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
        bool noviceTimberBaseline;
        IEnumerator NoviceTimberComparison()
        {
            yield return WeaponBalanceTest(true,true);
            string current=Path.Combine(DevCopy.SaveDir,"weapon-balance.json"),after=Path.Combine(DevCopy.SaveDir,"weapon-balance-after.json");
            File.Copy(current,after,true);
            var old=new Dictionary<ItemType,int>{{ItemType.AshWoodSword,66},{ItemType.AshWoodBow,68},{ItemType.PearlwoodSword,89},{ItemType.PearlwoodBow,87}};
            var saved=old.Keys.ToDictionary(t=>t,t=>ItemDb.Get(t).Damage);
            try
            {
                // Controlled before/after fixture only. These are the prior verified
                // runtime values from resume-lootquality-20261017-131503; no source
                // or save is changed, and no old budget is accepted as balanced.
                noviceTimberBaseline=true;
                foreach(var pair in old)ItemDb.Get(pair.Key).Damage=pair.Value;
                yield return WeaponBalanceTest(true,true);
                File.Copy(current,Path.Combine(DevCopy.SaveDir,"weapon-balance-timber-before.json"),true);
            }
            finally
            {
                foreach(var pair in saved)ItemDb.Get(pair.Key).Damage=pair.Value;
                noviceTimberBaseline=false;
                File.Copy(after,current,true);
            }
        }

        IEnumerator WeaponBalanceTest(bool integrationOnly=false,bool shopsOnly=false)
        {
            if(!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int checks=0,failures=0;
            void Check(string name,bool pass,string detail="")
            { checks++; if(!pass) failures++; Plugin.Log.LogInfo($"[AutoTest] 武器输出 {name} {detail} {(pass ? "通过" : "不通过")}"); }
            var weapons=ItemDb.All.Where(ProgressionBalance.BalancedWeapon).ToArray();
            var rows=new List<object>();
            foreach(var d in weapons)
            {
                var p=ProgressionBalance.OutputOf(d);
                bool original=ProgressionBalance.OriginalWeapon(d,out int vanilla,out int rare);
                Check("原版数据 "+d.Type,d.Texture==null||!d.Texture.StartsWith("Item_",StringComparison.Ordinal)||original,$"原版={vanilla} 当前={d.Damage} 阶段={d.Stage}");
                Check("输出参数 "+d.Type,p.Power>0 && p.Hits>0 && !float.IsNaN(p.Power) && d.Damage>=3);
                rows.Add(new {Type=d.Type.ToString(),d.Name,d.Stage,d.Damage,VanillaDamage=vanilla,VanillaRare=rare,d.UseTime,d.UseAnimation,
                    OriginalCycle=ProgressionBalance.OriginalCycle(d),p.Cycle,p.Volley,p.Units,p.Rate,p.Repeat,p.Hits,p.Power,p.Basis,Target=ProgressionBalance.WeaponTarget(d),
                    EstimatedNoAmmoDps=ProgressionBalance.WeaponDps(d,Areas.Defense(d.Stage))});
            }
            Check("飞刀随机数量期望",Mathf.Abs(ProgressionBalance.OutputOf(ItemDb.Get(ItemType.VampireKnives)).Volley-4.9375f)<.00001f);
            if(!noviceTimberBaseline)foreach(var pair in new[]{(ItemType.AshWoodSword,ItemType.CobaltSword),(ItemType.AshWoodBow,ItemType.CobaltRepeater),
                (ItemType.PearlwoodSword,ItemType.ChlorophyteSaber),(ItemType.PearlwoodBow,ItemType.TitaniumRepeater)})
            {
                var wood=ItemDb.Get(pair.Item1);var metal=ItemDb.Get(pair.Item2);
                int ammo=wood.UseAmmo==AmmoKind.None?0:ItemDb.Get(ItemType.WoodenArrow).Damage;
                Check("建材武器不压过同阶金属/"+wood.Type,ProgressionBalance.WeaponDps(wood,Areas.Defense(wood.Stage),ammo)
                    <ProgressionBalance.WeaponDps(metal,Areas.Defense(wood.Stage),ammo));
            }
            Check("暴雪不是双算每动画数量",ProgressionBalance.OutputOf(ItemDb.Get(ItemType.BlizzardStaff)).Volley==2);
            foreach(var stab in new[]{ItemType.Gladius,ItemType.Ruler,ItemType.ChlorophyteSaber})
            {
                Check("连续短刺使用实际频率/"+stab,Mathf.Abs(ProgressionBalance.OutputOf(ItemDb.Get(stab)).Cycle-ItemDb.Get(stab).UseTime)<.00001f);
                Check("原版连续短刺频率/"+stab,Mathf.Abs(ProgressionBalance.OriginalCycle(ItemDb.Get(stab))-ItemDb.Get(stab).UseTime)<.00001f);
            }
            Check("毒液法杖随机数量",Mathf.Abs(ProgressionBalance.OutputOf(ItemDb.Get(ItemType.VenomStaff)).Volley-(4+1f/3+1f/4+1f/5))<.00001f);
            Check("太阳花不双算每动画数量",ProgressionBalance.OutputOf(ItemDb.Get(ItemType.DaybloomStaff)).Volley==5);
            Check("泰拉刃单发高于飞刀",ItemDb.Get(ItemType.TerraBlade).Damage>ItemDb.Get(ItemType.VampireKnives).Damage*2);
            Check("哨兵分配独立预算",ProgressionBalance.WeaponTarget(ItemDb.Get(ItemType.QueenSpiderStaff))<ProgressionBalance.WeaponTarget(ItemDb.Get(ItemType.SpiderStaff)));

            bool immortalWas=TerraStats.DevImmortal;TerraStats.DevImmortal=true;
            bool invincibleWas=PlayerData.instance.isInvincible;PlayerData.instance.isInvincible=true;
            mod.Stats.FullHeal();mod.DevNoFallDamage=true;
            yield return GoTo("Crossroads_01",EntryGateOf("Crossroads_01"));
            var hero=HeroController.instance;
            var inv=mod.Inventory;
            var difficultyWas=GameDifficulty.Current;
            int stageWas=Shop.DevStage;
            bool infiniteWas=inv.Infinite;
            var randomWas=UnityEngine.Random.state;
            var saved=(ItemStack[])inv.Slots.Clone();
            var savedArmor=(ItemStack[])inv.Armor.Clone();
            var savedAccessories=(ItemStack[])inv.Accessories.Clone();
            int crystalsWas=mod.Stats.ManaCrystals;
            GameDifficulty.Current=DifficultyLevel.Normal;
            PlayerData.instance.isInvincible=true; mod.DevNoFallDamage=true;
            inv.Infinite=true; InventoryGui.Open=false;
            hero.transform.position=Clear(hero,hero.transform.position);
            yield return new WaitForSeconds(.6f);
            var target=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .FirstOrDefault(h=>!h.isDead && h.gameObject.activeInHierarchy && h.gameObject.scene.name=="Crossroads_01" && !EnemyStats.Of(h).Boss);
            Check("实测目标",target!=null);
            if(target==null) yield break;
            var info=EnemyStats.Of(target);
            int hpWas=target.hp,maxWas=info.MaxHp,levelWas=info.Level;
            var others=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .Where(h=>h!=target && h.gameObject!=target.gameObject && h.gameObject.activeSelf && h.gameObject.scene==target.gameObject.scene
                    && !h.transform.IsChildOf(target.transform) && !target.transform.IsChildOf(h.transform)).Select(h=>h.gameObject).Distinct().ToArray();
            foreach(var go in others) go.SetActive(false);
            Vector3 heroAt=hero.transform.position;
            var body=target.GetComponent<Rigidbody2D>(); var heroBody=hero.GetComponent<Rigidbody2D>();
            var col=hero.GetComponent<BoxCollider2D>(); var tc=target.GetComponent<Collider2D>();
            Vector2 offset=tc!=null ? (Vector2)tc.bounds.center-(Vector2)target.transform.position : Vector2.zero;
            var live=new List<WeaponLiveRow>();
            // Every class/stage, the changed volley families, sustained beams, sentries,
            // and the reported pair at BOTH melee range and medium range.
            var cases=new List<(ItemType Type,float Distance,float Seconds)>();
            for(int s=0;s<6;s++) for(int c=0;c<4;c++)
                cases.Add((ProgressionBalance.Core[s][c][0],c==0 ? 1 : 3,8));
            foreach(var t in new[] {ItemType.VampireKnives,ItemType.TerraBlade})
            { cases.Add((t,1,12)); cases.Add((t,3,12)); }
            foreach(var t in new[] {ItemType.Shotgun,ItemType.QuadBarrelShotgun,ItemType.TacticalShotgun,ItemType.OnyxBlaster,ItemType.Xenopopper,
                ItemType.PoisonStaff,ItemType.VenomStaff,ItemType.SkyFracture,ItemType.BatScepter,ItemType.LastPrism,ItemType.LaserMachinegun,
                ItemType.MedusaHead,ItemType.ChargedBlasterCannon,ItemType.Phantasm,ItemType.VortexBeater,ItemType.StardustDragonStaff,
                ItemType.BladeStaff,ItemType.QueenSpiderStaff,ItemType.BallistaCane,ItemType.LightningAuraCane,
                ItemType.FlaironFlail,ItemType.NorthPole,ItemType.BookStaff,ItemType.Nightglow,ItemType.Flamethrower,ItemType.Zenith,
                ItemType.DaybloomStaff,ItemType.SapphireStaff,ItemType.AmberStaff,ItemType.RocketLauncher,ItemType.StarCannon})
                cases.Add((t,3,10));
            cases.Add((ItemType.Arkhalis,1,10));
            cases.Add((ItemType.Terragrim,1,10));
            cases.Add((ItemType.PiercingStarlight,3,10));
            if(integrationOnly)
            {
                cases.Clear();
                foreach(var t in new[] {ItemType.VampireKnives,ItemType.TerraBlade})
                {cases.Add((t,1,12));cases.Add((t,3,12));}
                foreach(var t in new[] {ItemType.Flamelash,ItemType.ChargedBlasterCannon,ItemType.Zenith,ItemType.PiercingStarlight}) cases.Add((t,3,10));
                cases.Add((ItemType.BookofSkulls,1,10));
                cases.Add((ItemType.BookofSkulls,3,10));
            }
            if(shopsOnly)
            {
                cases.Clear();
                foreach(var t in ProgressionBalance.ShopOnlyWeapons.Concat(new[]{ItemType.Minishark,ItemType.Shotgun,
                    ItemType.GoldBroadsword,ItemType.PlatinumBroadsword,ItemType.TungstenBroadsword,
                    ItemType.GoldShortsword,ItemType.PlatinumShortsword,ItemType.TungstenShortsword,
                    ItemType.GoldBow,ItemType.PlatinumBow,ItemType.TungstenBow,
                    ItemType.AshWoodSword,ItemType.AshWoodBow,ItemType.PearlwoodSword,ItemType.PearlwoodBow,
                    ItemType.CobaltSword,ItemType.CobaltRepeater,ItemType.ChlorophyteSaber,ItemType.TitaniumRepeater,ItemType.Gladius,ItemType.Ruler}))
                    cases.Add((t,ItemDb.Get(t).Class==DamageClass.Melee ? 1 : 3,10));
            }
            if(noviceTimberBaseline)
            {
                cases.Clear();
                foreach(var t in new[]{ItemType.AshWoodSword,ItemType.AshWoodBow,ItemType.PearlwoodSword,ItemType.PearlwoodBow})
                    cases.Add((t,ItemDb.Get(t).Class==DamageClass.Melee?1:3,10));
            }
            try
            {
                Check("骷髅追踪范围内保留",TerraProjectiles.Instance.DevSkullLeash(target,false));
                Check("骷髅追踪越界释放",TerraProjectiles.Instance.DevSkullLeash(target,true));
                foreach(var run in cases)
                {
                    var d=ItemDb.Get(run.Type); Shop.DevStage=d.Stage;
                    hero.transform.position=heroAt; heroBody.linearVelocity=Vector2.zero;
                    Minions.Instance.Clear(); PlayerBuffs.Clear(); SummonTags.Clear(); EnemyDebuffs.Clear();
                    TerraProjectiles.Instance.DevClear(); Whips.Instance.Clear(); SwordFx.Instance?.Clear(); ZenithSwords.Instance?.DevClear();
                    for(int i=0;i<inv.Slots.Length;i++) inv.Slots[i]=default;
                    Equip(Array.Empty<ItemType>(),Array.Empty<ItemType>());
                    Equipment.Update(inv);
                    inv.Mouse=default; inv.Override=-1; inv.UseMouse=false;
                    inv.Selected=0; inv.Slots[0]=ItemStack.Of(run.Type,d.MaxStack>1 ? Mathf.Min(999,d.MaxStack) : 1);
                    var ammo=ItemDb.All.FirstOrDefault(a=>a.Ammo==d.UseAmmo && a.Ammo!=AmmoKind.None && a.Stage<=d.Stage);
                    if(ammo!=null) inv.Slots[Inventory.Size-1]=ItemStack.Of(ammo.Type,9999);
                    mod.Stats.ManaCrystals=Player.TerraStats.MaxManaCrystals;
                    mod.Stats.UpdateMaximums(PlayerData.instance,Equipment.Current.ManaMax);
                    Vector2 aim=(Vector2)col.bounds.center+Vector2.right*run.Distance;
                    // A crossbow cannot target its downward blind cone; keep its test
                    // target above the small floor rise beside this fixture.
                    if(run.Type==ItemType.BallistaCane) aim+=Vector2.up*1.5f;
                    target.hp=50000; target.IsInvincible=false; info.Level=d.Stage; info.Carry=0;
                    Pin(target,body,aim-offset);
                    mod.DevHold=d.Channel ? true : (bool?)null; mod.DevAim=aim;
                    yield return null;
                    if(d.IsSummon)
                    {
                        int n=d.Sentry ? 1 : Equipment.MaxMinions;
                        for(int i=0;i<n;i++)
                        {
                            while(!mod.DevReady) {Pin(target,body,aim-offset);yield return null;}
                            mod.DevUseSelected((Vector2)col.bounds.center+Vector2.right*1.5f);
                            yield return null;
                        }
                        mod.DevAimMinions(run.Type,aim);
                    }
                    int before=EnemyStats.Life(target,info),made=TerraProjectiles.DevMade(d.Shoot),hits=TerraProjectiles.DevHit(d.Shoot),casts=0;
                    Check("裸装输出基线 "+run.Type,Equipment.Current.MeleeDamage==0 && Equipment.Current.RangedDamage==0
                        && Equipment.Current.MagicDamage==0 && Equipment.Current.MinionDamage==0);
                    var hitBefore=Enum.GetValues(typeof(ProjKind)).Cast<ProjKind>().ToDictionary(k=>k,k=>TerraProjectiles.DevHit(k));
                    int orbs=TerraProjectiles.DevVampireOrbs,zapBigBefore=TerraProjectiles.DevZapBigHits;
                    float start=Time.time;
                    UnityEngine.Random.InitState(41016+(int)run.Type);
                    while(Time.time-start<run.Seconds)
                    {
                        hero.transform.position=heroAt; heroBody.linearVelocity=Vector2.zero;
                        Pin(target,body,aim-offset); mod.DevAim=aim; mod.Stats.Mana=mod.Stats.ManaMax;
                        if(!d.IsSummon && mod.DevReady) {mod.DevUseSelected(aim);casts++;}
                        yield return null;
                    }
                    float seconds=Time.time-start;
                    int damage=before-EnemyStats.Life(target,info);
                    var row=new WeaponLiveRow {Type=run.Type.ToString(),Stage=d.Stage,Damage=d.Damage,Distance=run.Distance,Seconds=seconds,
                        Dps=damage/seconds,TotalDamage=damage,Casts=casts,Made=TerraProjectiles.DevMade(d.Shoot)-made,Hits=TerraProjectiles.DevHit(d.Shoot)-hits,
                        HealingOrbs=TerraProjectiles.DevVampireOrbs-orbs,ZapBigHits=TerraProjectiles.DevZapBigHits-zapBigBefore,Minions=Equipment.MaxMinions,
                        EnemyDefense=info.Defense,ProjectileDamage=Equipment.Damage(d,ammo)};
                    row.ProjectileHits=hitBefore.Where(pair=>TerraProjectiles.DevHit(pair.Key)>pair.Value)
                        .ToDictionary(pair=>pair.Key.ToString(),pair=>TerraProjectiles.DevHit(pair.Key)-pair.Value);
                    live.Add(row);
                    Check("实际命中 "+run.Type+" 距离"+run.Distance,damage>0,JsonConvert.SerializeObject(row));
                    if(d.Sentry) Plugin.Log.LogInfo("[AutoTest] 哨兵实测 "+Minions.Instance.DevDescribe()+" 弩箭生成="+TerraProjectiles.DevMade(ProjKind.BallistaBolt));
                    if(d.Shoot==ProjKind.ZapinatorLaser) {
                        // Vanilla's 1/50 tenfold hit is intentionally high variance.
                        // A short run may exceed average DPS; require an observed
                        // lottery hit to explain it, and keep the eight-hit projectile limit.
                        Check("随机枪穿透次数有界 "+run.Type,row.Hits<=row.Casts*8 && row.Made==row.Casts);
                        Check("随机枪高输出有实际十倍触发 "+run.Type,row.Dps<=ProgressionBalance.WeaponTarget(d)*2.2f || row.ZapBigHits>0,
                            $"实测={row.Dps:F1} 十倍命中={row.ZapBigHits}");
                    } else if(!d.IsSummon && !noviceTimberBaseline)
                    {
                        float budget=ProgressionBalance.WeaponTarget(d);
                        // Native endgame hit floors deliberately exceed the former flattened DPS target.
                        // Validate their actual hit cadence against the output model at the new damage.
                        if(ProgressionBalance.DamageBeforeProgression.TryGetValue(d.Type,out int prior)&&d.Damage>prior)
                            budget=Mathf.Max(budget,ProgressionBalance.WeaponDps(d,info.Defense,ammo?.Damage??0));
                        Check("实际输出未异常叠加 "+run.Type,row.Dps<=budget*2.2f,
                            $"实测={row.Dps:F1} 预算={budget:F1}；允许近战双命中、暴击及范围命中波动");
                    }
                    if(run.Type==ItemType.VampireKnives)
                        Check("飞刀实发数量 "+run.Distance,casts>=10 && row.Made>=casts*4 && row.Made<=casts*8,$"使用{casts} 弹幕{row.Made}");
                    if(new[] {ItemType.Tsunami,ItemType.QuadBarrelShotgun,ItemType.TacticalShotgun,ItemType.SkyFracture}.Contains(run.Type))
                    {
                        float volley=ProgressionBalance.OutputOf(d).Volley;
                        Check("实际每轮弹幕 "+run.Type,Mathf.Abs(row.Made-casts*volley)<=volley,$"使用{casts} 弹幕{row.Made} 每轮{volley}");
                    }
                    mod.DevHold=null; mod.DevAim=null;
                    // Allow the prior swing to finish before another item starts.
                    yield return new WaitForSeconds(.6f);
                }
                if(!shopsOnly)foreach(float distance in new[] {1f,3f})
                {
                    float knife=live.Last(r=>r.Type=="VampireKnives" && r.Distance==distance).Dps;
                    float terra=live.Last(r=>r.Type=="TerraBlade" && r.Distance==distance).Dps;
                    Check("泰拉刃输出优势 距离"+distance,terra>knife*1.1f,$"泰拉刃={terra:F1} 飞刀={knife:F1}");
                }
            }
            finally
            {
                Minions.Instance.Clear(); TerraProjectiles.Instance.DevClear(); Whips.Instance.Clear(); SwordFx.Instance?.Clear(); ZenithSwords.Instance?.DevClear();
                mod.DevHold=null; mod.DevAim=null; mod.DevNoFallDamage=false;
                foreach(var go in others) if(go!=null) go.SetActive(true);
                if(target!=null) {target.hp=hpWas;info.MaxHp=maxWas;info.Level=levelWas;info.Carry=0;}
                Array.Copy(saved,inv.Slots,saved.Length); Array.Copy(savedArmor,inv.Armor,savedArmor.Length);
                Array.Copy(savedAccessories,inv.Accessories,savedAccessories.Length);
                mod.Stats.ManaCrystals=crystalsWas; PlayerData.instance.isInvincible=invincibleWas;TerraStats.DevImmortal=immortalWas;
                Equipment.Update(inv); mod.Stats.UpdateMaximums(PlayerData.instance,Equipment.Current.ManaMax);
                inv.Infinite=infiniteWas; GameDifficulty.Current=difficultyWas; Shop.DevStage=stageWas; UnityEngine.Random.state=randomWas;
            }
            string path=Path.Combine(DevCopy.SaveDir,"weapon-balance.json");
            File.WriteAllText(path,JsonConvert.SerializeObject(new {Checks=checks,Failures=failures,CounterfactualTimberBaseline=noviceTimberBaseline,Weapons=rows,Live=live},Formatting.Indented));
            Check("汇总",failures==0,$"{checks-failures}/{checks}；{path}");
        }

        sealed class WeaponLiveRow
        {
            public string Type;
            public int Stage,Damage,TotalDamage,Casts,Made,Hits,HealingOrbs,ZapBigHits,Minions,EnemyDefense,ProjectileDamage;
            public float Distance,Seconds,Dps;
            public Dictionary<string,int> ProjectileHits;
        }
    }
}
