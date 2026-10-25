using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class ProgressionBalance
    {
        static readonly Dictionary<(AmmoKind Kind,int Stage),int> weaponAmmo = new Dictionary<(AmmoKind,int),int>();
        // Counts are expectations, not rounded pellet counts. A delayed burst belongs to
        // one firing cycle; Blizzard's two icicles belong to each 5-tick cast.
        internal readonly struct WeaponOutput
        {
            public readonly float Cycle, Volley, Units, Rate, HitLimit, Repeat;
            public readonly string Basis;
            public WeaponOutput(float cycle, float volley, float units, string basis, float fixedRate=0, float hitLimit=float.PositiveInfinity, float repeat=1)
            { Cycle=cycle; Volley=volley; Units=units; Rate=fixedRate>0 ? fixedRate : 1/Mathf.Max(1f/60,cycle); Basis=basis; HitLimit=hitLimit; Repeat=repeat; }
            public float Hits => Mathf.Min(HitLimit,Rate*Volley*Repeat);
            public float Power => Rate*Units*Repeat*Mathf.Min(1,HitLimit/(Rate*Volley*Repeat));
        }

        internal static bool BalancedWeapon(ItemDef d) => d!=null && d.IsWeapon && !d.IsAccessory && !d.GameTool && !d.IsAmmo
            && d.PickPower==0 && d.AxePower==0 && d.HammerPower==0 && !d.HkItem && d.Stage>=0 && d.Stage<=5;

        internal static WeaponOutput OutputOf(ItemDef d)
        {
            // Gladius, Ruler and Chlorophyte Saber repeat their stab every
            // useTime inside one animation; animation duration is not hit cadence.
            if(d.Fx==MeleeFx.Shortsword)
                return new WeaponOutput(d.UseTime,1,d.Type==ItemType.ChlorophyteSaber?.7f:1,
                    "短剑按使用间隔重复刺击；叶绿军刀特效保留原版0.7伤害倍率");
            float cycle=d.Class==DamageClass.Melee && (d.Shoot==ProjKind.None || d.ShootsOnSwingStart) || d.IsWhip ? d.UseAnimation : d.UseTime;
            float count=1, units=1;
            string basis="每次使用一击";
            switch(d.Type)
            {
                case ItemType.BookofSkulls:
                    return new WeaponOutput(cycle,1,1,"骷髅最多3次命中；3秒或累计640像素消失，20帧局部命中冷却",repeat:3);
                case ItemType.VampireKnives: count=units=4+1f/2+1f/4+1f/8+1f/16; break;
                case ItemType.Boomstick: count=units=3.5f; break;
                case ItemType.Shotgun: count=units=4.5f; break;
                case ItemType.QuadBarrelShotgun: count=units=8; break;
                case ItemType.TacticalShotgun: count=units=6; break;
                case ItemType.OnyxBlaster: count=5; units=6; break; // four pellets + double-damage blast
                case ItemType.Xenopopper: count=units=4.25f; break;
                case ItemType.Tsunami: count=units=5; break;
                case ItemType.AerialBane: count=units=6; break;
                case ItemType.ChlorophyteShotbow: count=units=2+1f/3; break;
                case ItemType.DaedalusStormbow: count=units=3+1f/3; break; // ordinary arrows; special arrows are 8/3
                case ItemType.ClockworkAssaultRifle: count=units=3; break; // already a 24-tick cycle in ItemDef
                case ItemType.PoisonStaff: count=units=3+1f/3; break;
                case ItemType.VenomStaff: count=units=4+1f/3+1f/4+1f/5; break;
                case ItemType.SkyFracture: count=units=3; break; // complete 28-tick cycle, not three casts per four ticks
                case ItemType.BlizzardStaff: count=units=2; break;
                case ItemType.BatScepter: count=units=2.5f; break;
                case ItemType.BubbleGun: count=units=3; break;
                case ItemType.Razorpine: count=units=3; break;
                case ItemType.LunarFlareBook: count=units=3; break;
                case ItemType.StarWrath: count=units=3; break;
                case ItemType.Eventide: count=5; units=6; break; // central arrow deals double damage
                case ItemType.Celebration: count=units=2; break;
                case ItemType.Flamethrower: case ItemType.ElfMelter: count=units=5; break;
                case ItemType.Zenith:
                    return new WeaponOutput(cycle,3,3,"三把剑独立11帧冷却；往返与残影共用各剑命中间隔（固定靶估计）",repeat:4.5f*7f/ZenithSwords.ZenithHitCooldownTicks);
                case ItemType.BetsysWrath: count=units=3; break;
                case ItemType.BeeGun:
                    return new WeaponOutput(cycle,2+1f/3,2+1f/3,"平均7/3只蜜蜂；同类共享10帧冷却",hitLimit:6);
                case ItemType.WaspGun:
                    return new WeaponOutput(cycle,3,3,"平均三只黄蜂；同类共享10帧冷却",hitLimit:6);
                case ItemType.SapphireStaff: count=2; units=1.2f; break;
                case ItemType.RubyStaff: count=2; units=1; break;
                case ItemType.AmberStaff: units=.8f; break;
                case ItemType.Meowmere: units=1.25f; break;
                case ItemType.NebulaBlaze: units=1.4f; break; // 20% triple damage
                case ItemType.IceSickle: units=.67f; break;
                case ItemType.InfluxWaver: count=units=3; break; // returning strikes
                case ItemType.BladeofGrass: count=2; units=1.25f; break;
                case ItemType.Muramasa: count=2; units=1.5f; break;
                case ItemType.FieryGreatsword: count=2; units=1.75f; break;
                case ItemType.BeeKeeper: count=3; units=2.4f; break;
                case ItemType.BeesKnees: count=3; units=2; break;
                case ItemType.Petalstorm: count=units=3; break;
                case ItemType.NebulaArcanum: count=units=4; break;
                case ItemType.TrueNightsEdge: count=2; units=1.5f; break;
                case ItemType.NimbusRod:
                    return new WeaponOutput(cycle,1,1,"最多三朵云，各每9帧一滴；持续副武器",20);
                case ItemType.CrimsonRod:
                    return new WeaponOutput(cycle,1,1,"一朵云每11帧一滴；持续副武器",60f/11);
                case ItemType.RainbowGun: case ItemType.ClingerStaff:
                    return new WeaponOutput(cycle,1,1,"持续区域共享10帧命中冷却；副武器",6);
                case ItemType.MagnetSphere:
                    return new WeaponOutput(cycle,1,1,"磁球每9帧发射；新球替换旧球",60f/9);
                case ItemType.FlaironFlail:
                    return new WeaponOutput(cycle,1,1,"链球接触及每3至5帧一颗追踪泡泡的合计估计",18);
                case ItemType.NorthPole: count=3; units=2.7f; basis="刺击、冰矛及一路落下的雪花的单目标估计"; break;
                case ItemType.RazorbladeTyphoon:
                    return new WeaponOutput(cycle,1,1,"刀轮持续命中；含存活重叠的单目标估计",6);
            }
            if((count!=1 || units!=1) && basis=="每次使用一击") basis="实际发射模式（含随机数量期望及子弹幕伤害）";
            if(d.IsSummon)
            {
                // Casting speed does not determine minion DPS. The staff's AI cadence
                // and slots do. These are sustained single-target estimates; live tests
                // separately cover ground/air pursuit and startup.
                if(d.Minion==MinionKind.StardustDragon)
                    return new WeaponOutput(cycle,1,1+.23f*Mathf.Min(50,2*Minions[d.Stage]+1),
                        "星尘龙各节共享7帧无敌；按本阶段长度计算伤害倍率",60f/7);
                if(d.Minion==MinionKind.Abigail)
                    return new WeaponOutput(cycle,1,1+.55f*(Minions[d.Stage]-1),"阿比盖尔共用本体，计入额外召唤增伤",3);
                float rate=MinionRate(d);
                return new WeaponOutput(cycle,1,1,d.Sentry ? "哨兵独立攻击周期" : "仆从独立攻击周期及本阶段数量",rate*(d.Sentry ? 1 : Minions[d.Stage]));
            }
            if(d.Channel)
            {
                float rate=TerraProjectiles.SustainedHitRate(d.Shoot);
                string why="持续接触：读取弹幕自身局部/同类/所有者命中冷却";
                switch(d.Type)
                {
                    case ItemType.LastPrism:
                        return new WeaponOutput(cycle,1,3,"蓄满三倍伤害；六束共享5帧命中冷却，不能按六倍叠加",12);
                    case ItemType.LaserMachinegun: rate=20; why="预热后每6帧两束激光"; break;
                    case ItemType.MedusaHead: rate=4; why="每15帧凝视"; break;
                    case ItemType.ChargedBlasterCannon:
                        return new WeaponOutput(8,40,61,"完整8秒周期：蓄力小球、大球及5秒1.5倍光束的合计估计");
                    case ItemType.Flamelash:
                        return new WeaponOutput(cycle,2,2,"操纵后重新施放；每颗火鞭可命中两次（12帧局部冷却）",1/Mathf.Max(.5f,cycle));
                    case ItemType.MagicMissile: case ItemType.RainbowRod:
                        rate=1/Mathf.Max(.5f,cycle); why="操纵后命中的重新施放周期估计"; break;
                    case ItemType.Phantasm: rate=24; why="满速四箭及追击箭的持续输出估计"; break;
                    case ItemType.VortexBeater: rate=12; why="持续子弹及周期火箭"; break;
                    case ItemType.CelebrationMk2: rate=24; why="持续火箭及不同火箭子弹幕估计"; break;
                    case ItemType.PiranhaGun: rate=6; why="三食人鱼共享命中冷却"; break;
                    case ItemType.SoulDrain: rate=6; why="持续吸取的共享命中周期"; break;
                }
                return new WeaponOutput(cycle,1,1,why,rate);
            }
            if(d.ShotsPerUse>0)
            {
                float shots=Mathf.Min(d.ShotsPerUse,Mathf.Ceil(d.UseAnimation/Mathf.Max(d.UseTime,1f/60)));
                return new WeaponOutput(Mathf.Max(cycle,d.UseAnimation),count*shots,units*shots,"每次动画发射上限及动画剩余时间");
            }
            return new WeaponOutput(cycle,count,units,basis);
        }

        static float MinionRate(ItemDef d)
        {
            switch(d.Minion)
            {
                case MinionKind.BabySlime: return 1.5f;
                case MinionKind.BabyBird: return 2;
                case MinionKind.VampireFrog: return 3;
                case MinionKind.Flinx: case MinionKind.Spider: case MinionKind.Pirate: return 2;
                case MinionKind.Hornet: return 1.2f;
                case MinionKind.Imp: return .75f;
                case MinionKind.Sanguine: return 1.5f;
                case MinionKind.UFO: case MinionKind.Terraprisma: return 3;
                case MinionKind.BladeStaff: return 4;
                case MinionKind.Flameburst1: return 60f/80;
                case MinionKind.Flameburst2: return 60f/70;
                case MinionKind.Flameburst3: return 1;
                case MinionKind.Ballista1: case MinionKind.Ballista2: case MinionKind.Ballista3: return 1f/3;
                case MinionKind.Trap1: case MinionKind.Trap2: case MinionKind.Trap3: return 2f/3;
                case MinionKind.Aura1: case MinionKind.Aura2: case MinionKind.Aura3: return 3;
                case MinionKind.FrostHydra: return 1f/1.5f;
                case MinionKind.QueenSpider: return 1.5f;
                default: return 2;
            }
        }

        internal static bool OriginalWeapon(ItemDef d,out int damage,out int rare)
        {
            damage=0; rare=0;
            if(d.Texture==null || !d.Texture.StartsWith("Item_") || !int.TryParse(d.Texture.Substring(5),out int id)
                || !VanillaWeaponDamage.Values.TryGetValue(id,out var v)) return false;
            damage=v.Damage; rare=v.Rare; return true;
        }
        internal static float OriginalCycle(ItemDef d)
        {
            var p=OutputOf(d);
            if(d.IsSummon || d.Channel || d.Texture==null || !d.Texture.StartsWith("Item_")
                || !int.TryParse(d.Texture.Substring(5),out int id) || !VanillaWeaponDamage.Values.TryGetValue(id,out var v)) return p.Cycle;
            bool complete=d.ShotsPerUse>0 || d.ShootsFinalFractal || d.Class==DamageClass.Melee && d.Shoot==ProjKind.None && d.Fx!=MeleeFx.Shortsword;
            switch(d.Type)
            {
                case ItemType.ClockworkAssaultRifle: case ItemType.SkyFracture:
                case ItemType.Flamethrower: case ItemType.ElfMelter: case ItemType.Eventide: complete=true; break;
            }
            return Mathf.Max(1,complete ? Mathf.Max(v.UseTime,v.Animation)+Mathf.Max(0,v.ReuseDelay-2) : v.UseTime)/60f;
        }
        internal static float WeaponTarget(ItemDef d)
        {
            OriginalWeapon(d,out int original,out int rare);
            if(original<=0) original=d.Damage;
            var p=OutputOf(d);
            float[] reference={35,65,100,170,250,400,550,700,900,1100,1400,1700};
            float role=d.Sentry ? .25f : d.IsSummon ? .65f : d.IsWhip ? .35f : PersistentWeapon(d.Type) ? .3f : 1;
            float vanilla=(original+AmmoBudget(d))*p.Power;
            if(!d.IsSummon && !d.Channel) vanilla*=p.Cycle/OriginalCycle(d);
            // Terra Blade can hit with its slash AND travelling wave; the damage
            // budget is divided by the ranged wave alone so its melee range is useful.
            if(d.Type==ItemType.TerraBlade) vanilla*=2;
            // Lunar and final crafted weapons keep a wider power range, so the
            // Zenith's many returning hits aren't flattened into a normal late drop.
            // Compare weapons available in one region against the same reference.
            // Dividing each by its rarity's reference made a crafted upgrade worse
            // merely because crafting raised its rarity (Handgun -> Phoenix Blaster).
            float relative=Mathf.Clamp(Mathf.Sqrt(vanilla/(reference[Mathf.Clamp(d.Stage,0,5)]*role)),.55f,rare>=10 ? 2.5f : 1.55f);
            float utility=d.Type==ItemType.VampireKnives ? .78f : d.Type==ItemType.BookofSkulls ? .8f : 1; // utility is retained, but budgeted
            float endgame=rare>=10 ? 1.25f : 1;
            float target=HitBudget[d.Stage]*2*role*relative*utility*endgame;
            // Late wood is a building material, not a full-strength weapon tier.
            // Normalizing rarity 0 against rarity 4-7 inverted their quality:
            // Pearlwood Sword reached ~329 DPS while Chlorophyte Saber was ~117.
            // Keep acquisition/recipes untouched and cap these cheap fallback
            // weapons below the tier's metal weapons instead of promoting them.
            if(ConstructionWeapon(d.Type))target=Mathf.Min(target,HitBudget[d.Stage]*.9f);
            return target;
        }
        internal static bool ConstructionWeapon(ItemType type)=>type==ItemType.AshWoodSword || type==ItemType.AshWoodBow
            || type==ItemType.PearlwoodSword || type==ItemType.PearlwoodBow;
        static float AmmoBudget(ItemDef d) => weaponAmmo.TryGetValue((d.UseAmmo,d.Stage),out int damage) ? damage : 0;
        static int WeaponArmorPen(ItemDef d)
        {
            switch(d.Type)
            {
                case ItemType.BladeStaff: return 25;
                case ItemType.Flamethrower: case ItemType.ElfMelter: return 15;
                case ItemType.WaspGun: case ItemType.WeatherPain: case ItemType.NettleBurst: return 10;
                case ItemType.RubyStaff: return 4;
                default: return TerraProjectiles.BaseArmorPen(d.Shoot);
            }
        }
        static bool PersistentWeapon(ItemType t) => t==ItemType.NimbusRod || t==ItemType.CrimsonRod || t==ItemType.RainbowGun || t==ItemType.ClingerStaff || t==ItemType.MagnetSphere;

        internal static float WeaponDps(ItemDef d,int defense,int ammo=0)
        {
            var p=OutputOf(d);
            float total=Mathf.Max(p.Hits,(d.Damage+ammo)*p.Power-Mathf.Max(0,defense-WeaponArmorPen(d))*.5f*p.Hits);
            return total*(d.IsSummon || d.IsWhip ? 1 : 1+Mathf.Clamp01((d.Crit+4)/100f));
        }

        internal static void ApplyWeaponBalance(ItemDef[] items)
        {
            // Match a real, early obtainable ammo of each kind. A rocket's 40 damage,
            // gel's zero, and a musket ball's 7 cannot share a flat stage allowance.
            foreach(AmmoKind kind in Enum.GetValues(typeof(AmmoKind))) for(int stage=0;stage<6;stage++)
            {
                ItemDef first=null;
                foreach(var a in items)
                    if(a!=null && kind!=AmmoKind.None && a.Ammo==kind && a.Stage<=stage
                        && (first==null || a.Stage<first.Stage || a.Stage==first.Stage && a.Damage<first.Damage)) first=a;
                weaponAmmo[(kind,stage)]=first?.Damage ?? 0;
            }
            foreach(var d in items) if(BalancedWeapon(d))
            {
                var p=OutputOf(d);
                float target=(WeaponTarget(d)+Mathf.Max(0,World.Areas.Defense(d.Stage)-WeaponArmorPen(d))*.5f*p.Hits)/p.Power-AmmoBudget(d);
                d.Damage=Mathf.Max(3,Mathf.RoundToInt(target));
                // Damage progression must not replace Terraria's per-use mana cost.
                if(d.Class==DamageClass.Magic && d.Texture!=null && d.Texture.StartsWith("Item_")
                    && int.TryParse(d.Texture.Substring(5),out int itemId) && VanillaWeaponDamage.Values.TryGetValue(itemId,out var original))
                    d.Mana=original.Mana;
            }
        }

        // A native recipe which consumes a weapon must preserve that upgrade's
        // output after all model/calibration passes, not only before them.
        internal static void PreserveRecipeUpgrades(ItemDef[] items,IEnumerable<Recipe> recipes)
        {
            var upgrades=new List<Recipe>(recipes);
            for(int pass=0;pass<8;pass++)
            {
                bool changed=false;
                foreach(var recipe in upgrades)
                {
                    var result=items[(int)recipe.Result];
                    // A weapon measured in play keeps its measured damage (WeaponCalibration): the model undercounts
                    // some of their hits (the Night's Edge's, the Terra Blade's beams), so it would raise them twice.
                    if(recipe.IsBought || !BalancedWeapon(result) || result.Source!=ItemSource.Craft || WeaponCalibration.Measured(result.Type))continue;
                    foreach(var ingredient in recipe.Ingredients)
                    {
                        var prior=items[(int)ingredient.Type];
                        if(!BalancedWeapon(prior) || prior.Class!=result.Class || prior.Stage>result.Stage || prior.Type==result.Type)continue;
                        OriginalWeapon(prior,out int oldDamage,out _);OriginalWeapon(result,out int newDamage,out _);
                        float nativeRatio=oldDamage>0 && newDamage>0
                            ? newDamage/OriginalCycle(result)/(oldDamage/OriginalCycle(prior)):1.2f;
                        float ratio=Mathf.Clamp(nativeRatio,1.2f,1.5f),defense=World.Areas.Defense(result.Stage);
                        int ammo=Mathf.RoundToInt(AmmoBudget(result));
                        float target=WeaponDps(prior,(int)defense,ammo)*ratio;
                        if(WeaponDps(result,(int)defense,ammo)>=target)continue;
                        var output=OutputOf(result);
                        float crit=result.IsSummon||result.IsWhip?1:1+Mathf.Clamp01((result.Crit+4)/100f);
                        int damage=Mathf.CeilToInt((target/crit+Mathf.Max(0,defense-WeaponArmorPen(result))*.5f*output.Hits)/output.Power-ammo);
                        if(damage>result.Damage){result.Damage=damage;changed=true;}
                    }
                }
                if(!changed)break;
            }
        }
    }
}
