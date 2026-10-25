using System;
using System.Collections.Generic;

namespace TerraKnight.Items
{
    // The six movement/region bands are not Terraria progression milestones.
    // These profiles describe what a victory enables, using the original recipe ingredients.
    internal static class VanillaBossProgression
    {
        internal enum Step { PreBoss, Slime, Eye, Evil, Dungeon, Hardmode, MechOne, MechTwo, MechAll, Plantera, Golem, Cultist, Pillars, MoonLord }
        internal sealed class Material
        {
            internal ItemType Type;
            internal int Min, Max, Group;
            internal Material(ItemType t,int min,int max,int group=0) {Type=t;Min=min;Max=max;Group=group;}
        }
        internal sealed class Profile
        {
            internal string Role, Upgrade;
            internal Step Step;
            internal int Band;
            internal ItemType[] Gear;
            internal ItemType Signature;
            internal Material[] Materials;
        }
        static Material M(ItemType t,int min,int max,int group=0)=>new Material(t,min,max,group);
        static Profile P(int band,Step step,string role,string upgrade,ItemType signature,ItemType[] gear,params Material[] materials)
            =>new Profile {Band=band,Step=step,Role=role,Upgrade=upgrade,Signature=signature,Gear=gear,Materials=materials};
        internal static readonly Dictionary<string,Profile> All=new Dictionary<string,Profile> {
            ["FalseKnight"]=P(0,Step.Slime,"史莱姆王","忍者装备和皇家凝胶，准备克苏鲁之眼",ItemType.RoyalGel,new[]{ItemType.NinjaHood,ItemType.NinjaShirt,ItemType.NinjaPants},M(ItemType.Gel,20,40)),
            ["BigFly"]=P(0,Step.Slime,"肉前机动探索支线","钩爪和飞毯帮助早期 Boss 走位",ItemType.FlyingCarpet,new[]{ItemType.SlimeHook}),
            ["Mawlek"]=P(0,Step.Slime,"肉前稀有近战支线","血腥砍刀、蝙蝠棍替代开局近战武器",ItemType.None,new[]{ItemType.BloodyMachete,ItemType.BatBat}),
            ["BigBuzzer"]=P(0,Step.Slime,"肉前史莱姆召唤支线","史莱姆法杖与近战攻速材料",ItemType.FeralClaws,new[]{ItemType.SlimeStaff}),
            ["Hornet"]=P(1,Step.Eye,"克苏鲁之眼","邪恶矿制作魔光剑/血腥屠刀、恶魔弓/肌腱弓，准备邪恶 Boss",ItemType.ShieldOfCthulhu,Array.Empty<ItemType>(),M(ItemType.DemoniteOre,30,90,208),M(ItemType.CrimtaneOre,30,90,208)),
            ["MegaMossCharger"]=P(1,Step.Eye,"肉前丛林探索支线","三发猎枪及丛林原料制作草剑、荆棘旋刃和丛林套",ItemType.AnkletoftheWind,new[]{ItemType.Boomstick},M(ItemType.JungleSpores,8,16),M(ItemType.Stinger,8,16),M(ItemType.Vine,3,6)),
            ["GhostAladar"]=P(1,Step.Eye,"肉前血月支线","吸血鬼青蛙法杖与受击加速饰品",ItemType.PanicNecklace,new[]{ItemType.VampireFrogStaff}),
            ["GhostNoEyes"]=P(1,Step.Eye,"肉前附魔剑稀有支线","稀有剑类提供肉前近战升级",ItemType.None,new[]{ItemType.EnchantedSword,ItemType.Terragrim}),
            ["DungDefender"]=P(2,Step.Evil,"世界吞噬怪","暗影鳞片配魔矿制作暗影套、梦魇镐，进入狱石链",ItemType.WormScarf,Array.Empty<ItemType>(),M(ItemType.DemoniteOre,80,110),M(ItemType.ShadowScale,20,40)),
            ["FlukeMother"]=P(2,Step.Evil,"克苏鲁之脑","组织样本配猩红矿制作猩红套、死亡使者镐",ItemType.BrainOfConfusion,Array.Empty<ItemType>(),M(ItemType.CrimtaneOre,80,110),M(ItemType.TissueSample,20,40)),
            ["MageKnight"]=P(2,Step.Evil,"肉前暗影珠/猩红心支线","魔刺和猩红魔杖用于邪恶 Boss 与骷髅王",ItemType.BandofStarpower,new[]{ItemType.Vilethorn,ItemType.CrimsonRod}),
            ["GhostHu"]=P(2,Step.Evil,"蜂后","蜂类武器直接掉落；蜂蜡用于蜜蜂套、黄蜂法杖",ItemType.HiveBackpack,new[]{ItemType.BeeKeeper,ItemType.BeesKnees,ItemType.BeeGun},M(ItemType.BeeWax,17,30)),
            ["MantisLord"]=P(2,Step.Dungeon,"骷髅王/地牢入口","村正用于永夜刃，手枪用于凤凰爆破枪，准备肉山",ItemType.BoneGlove,new[]{ItemType.Muramasa,ItemType.Handgun},M(ItemType.Bone,20,40)),
            ["MageLord"]=P(2,Step.Dungeon,"骷髅王后的地牢魔法支线","地牢魔法武器用于肉前后段",ItemType.CobaltShield,new[]{ItemType.MagicMissile,ItemType.WaterBolt,ItemType.BookofSkulls}),
            ["GhostXero"]=P(2,Step.Dungeon,"地牢宝箱支线","地牢链球、悠悠球与水系法杖",ItemType.None,new[]{ItemType.BlueMoon,ItemType.Valor,ItemType.AquaScepter}),
            ["MegaBeamMiner"]=P(2,Step.Dungeon,"独眼巨鹿","原版鹿角怪武器和骸骨头盔",ItemType.BoneHelm,new[]{ItemType.PewMaticHorn,ItemType.WeatherPain,ItemType.HoundiusShootius,ItemType.LucyTheAxe}),
            ["MegaJellyfish"]=P(2,Step.Dungeon,"肉前地狱探索支线","狱石与黑曜石制作熔岩套、火山、熔火之怒、小鬼法杖",ItemType.LavaCharm,new[]{ItemType.Flamelash,ItemType.FlowerofFire},M(ItemType.Hellstone,51,81),M(ItemType.Obsidian,17,27)),
            ["FalseKnight@5"]=P(2,Step.Dungeon,"地狱宝箱/肉山前支线","暗黑长枪、阳炎之怒、地狱之翼弓用于血肉墙",ItemType.ObsidianRose,new[]{ItemType.DarkLance,ItemType.Sunfury,ItemType.HellwingBow,ItemType.Cascade}),
            ["BlackKnight"]=P(3,Step.Hardmode,"血肉墙·近战","毁灭刃、战士徽章；神锤与困难模式矿物开启全面升级",ItemType.WarriorEmblem,new[]{ItemType.BreakerBlade,ItemType.Pwnhammer}),
            ["JarCollector"]=P(3,Step.Hardmode,"血肉墙·远程","发条式突击步枪与游侠徽章用于肉后初期",ItemType.RangerEmblem,new[]{ItemType.ClockworkAssaultRifle}),
            ["MageLord@5"]=P(3,Step.Hardmode,"血肉墙·魔法","激光步枪与巫士徽章用于肉后初期",ItemType.SorcererEmblem,new[]{ItemType.LaserRifle}),
            ["InfectedKnight"]=P(3,Step.Hardmode,"血肉墙·召唤","鞭炮与召唤师徽章，蜘蛛套/蜘蛛法杖随后自己制作",ItemType.SummonerEmblem,new[]{ItemType.Firecracker}),
            ["MimicSpider"]=P(3,Step.Hardmode,"肉后腐化宝箱怪","镖步枪、飞镖链、爬藤怪法杖用于机械 Boss",ItemType.PutridScent,new[]{ItemType.DartRifle,ItemType.ChainGuillotines,ItemType.ClingerStaff}),
            ["GhostGalien"]=P(3,Step.Hardmode,"肉后猩红宝箱怪","镖手枪、臭虎爪、夺命杖用于机械 Boss",ItemType.FleshKnuckles,new[]{ItemType.DartPistol,ItemType.FetidBaghnakhs,ItemType.SoulDrain}),
            ["GhostMarmu"]=P(3,Step.Hardmode,"肉后神圣宝箱怪","代达罗斯风暴弓、水晶刺客法杖和飞刀用于三王前",ItemType.IlluminantHook,new[]{ItemType.DaedalusStormbow,ItemType.CrystalVileShard,ItemType.FlyingKnife}),
            ["HiveKnight"]=P(4,Step.Hardmode,"史莱姆皇后·护甲","水晶刺客套与挥发明胶；不再混发肉前蜂后装备或机械魂",ItemType.VolatileGelatin,new[]{ItemType.CrystalNinjaHelmet,ItemType.CrystalNinjaChestplate,ItemType.CrystalNinjaLeggings}),
            ["Oblobble"]=P(4,Step.Hardmode,"史莱姆皇后·召唤/机动","刃杖、失谐钩用于三王前",ItemType.None,new[]{ItemType.BladeStaff,ItemType.QueenSlimeHook}),
            ["Hornet@4"]=P(4,Step.MechOne,"毁灭者","力量之魂制作巨兽鲨；神圣锭制作神圣装备、断钢剑、神圣连弩",ItemType.None,Array.Empty<ItemType>(),M(ItemType.HallowedBar,15,30),M(ItemType.SoulofMight,25,40)),
            ["GhostMarkoth"]=P(4,Step.MechOne,"双子魔眼","视域之魂制作魔眼法杖；第二个机械 Boss 提供新的加工分支",ItemType.None,Array.Empty<ItemType>(),M(ItemType.HallowedBar,15,30),M(ItemType.SoulofSight,25,40)),
            ["InfectedKnight@5"]=P(4,Step.MechOne,"机械骷髅王","恐惧之魂制作火焰喷射器；集齐三王魂制作斧钻/镐斧，进入叶绿链",ItemType.None,Array.Empty<ItemType>(),M(ItemType.HallowedBar,15,30),M(ItemType.SoulofFright,25,40)),
            ["TraitorLord"]=P(5,Step.Plantera,"世纪之花","花系武器、矮人法杖；解锁灵气、幽灵/蘑菇矿加工与后期地牢",ItemType.SporeSac,new[]{ItemType.Seedler,ItemType.GrenadeLauncher,ItemType.VenusMagnum,ItemType.NettleBurst,ItemType.LeafBlower,ItemType.FlowerPow,ItemType.WaspGun,ItemType.PygmyStaff,ItemType.FlowerWhip}),
            ["LobsterLancer"]=P(5,Step.Golem,"石巨人","疯狂飞斧、高温射线、石巨人之拳/大地法杖；甲虫鞘翅制作甲虫套",ItemType.ShinyStone,new[]{ItemType.PossessedHatchet,ItemType.HeatRay,ItemType.GolemFist,ItemType.StaffofEarth,ItemType.Stynger,ItemType.Picksaw,ItemType.SunStone,ItemType.EyeoftheGolem},M(ItemType.BeetleHusk,18,23)),
            ["Grimm"]=P(5,Step.Plantera,"南瓜月","南瓜月独特武器与阴森木；不提前给四柱碎片",ItemType.SpookyTwig,new[]{ItemType.TheHorsemansBlade,ItemType.StakeLauncher,ItemType.CandyCornRifle,ItemType.JackOLanternLauncher,ItemType.BatScepter,ItemType.RavenStaff},M(ItemType.SpookyWood,50,100)),
            ["GreyPrince"]=P(5,Step.Plantera,"霜月","霜月独特武器；不混入天界制作成品",ItemType.None,new[]{ItemType.ChristmasTreeSword,ItemType.NorthPole,ItemType.ElfMelter,ItemType.ChainGun,ItemType.SnowmanCannon,ItemType.BlizzardStaff,ItemType.Razorpine}),
            ["WhiteDefender"]=P(5,Step.Hardmode,"猪龙鱼公爵（肉后可越序挑战）","猪鲨独特武器与翅膀；胜利不冒充三王/世纪之花已击败",ItemType.FishronWings,new[]{ItemType.FlaironFlail,ItemType.Tsunami,ItemType.BubbleGun,ItemType.RazorbladeTyphoon,ItemType.TempestStaff}),
            ["NightmareGrimm"]=P(5,Step.Plantera,"光之女皇","女皇独特武器与翱翔徽章",ItemType.EmpressFlightBooster,new[]{ItemType.PiercingStarlight,ItemType.Eventide,ItemType.Nightglow,ItemType.Kaleidoscope,ItemType.Terraprisma}),
            ["HollowKnight"]=P(5,Step.Cultist,"拜月教邪教徒","远古操纵机开启天界碎片武器制作",ItemType.None,Array.Empty<ItemType>(),M(ItemType.LunarCraftingStation,1,1)),
            ["NailBros"]=P(5,Step.Pillars,"日耀柱","日耀碎片制作日耀喷发剑、破晓",ItemType.None,Array.Empty<ItemType>(),M(ItemType.FragmentSolar,18,26)),
            ["Nailsage"]=P(5,Step.Pillars,"星旋柱","星旋碎片制作星旋机枪、幻影弓",ItemType.None,Array.Empty<ItemType>(),M(ItemType.FragmentVortex,18,26)),
            ["Paintmaster"]=P(5,Step.Pillars,"星云柱","星云碎片制作星云烈焰、星云奥秘",ItemType.None,Array.Empty<ItemType>(),M(ItemType.FragmentNebula,18,26)),
            ["HollowKnightPrime"]=P(5,Step.Pillars,"星尘柱","星尘碎片制作星尘细胞、星尘之龙法杖及星座鞭",ItemType.None,Array.Empty<ItemType>(),M(ItemType.FragmentStardust,18,26)),
            ["FinalBoss"]=P(5,Step.MoonLord,"月亮领主","原版月亮领主武器直接掉；夜明矿自己炼锭并制作四职业天界护甲",ItemType.GravityGlobe,new[]{ItemType.Meowmere,ItemType.StarWrath,ItemType.Terrarian,ItemType.SDMG,ItemType.CelebrationMk2,ItemType.LastPrism,ItemType.LunarFlareBook,ItemType.LunarPortalStaff,ItemType.RainbowCrystalStaff},M(ItemType.LunarOre,70,90,207)),
        };

        internal static readonly string[] Mechs={"Hornet@4","GhostMarkoth","InfectedKnight@5"};
        // Fixed single-player Master totals, effective defense and contact-damage multiplier.
        // Ordered within each region; never scale against the player's currently worn gear.
        // Terraria's separate rolls of the bosses these stand for (ItemDropDatabase: Common(item, 1 in Den), the Moon Lord's
        // from its treasure bag), the boss's own like its gear: pets, wings, the Honey Comb, Plantera's hook and axe.
        internal static readonly Dictionary<string,(ItemType Type,int Den)[]> Extras = new Dictionary<string,(ItemType,int)[]> {
            ["DungDefender"]=new[]{(ItemType.EatersBone,20)},
            ["FlukeMother"]=new[]{(ItemType.BoneRattle,20)},
            ["GhostHu"]=new[]{(ItemType.HoneyComb,3),(ItemType.Nectar,15)},
            ["TraitorLord"]=new[]{(ItemType.ThornHook,10),(ItemType.Seedling,20),(ItemType.TheAxe,50)},
            ["Grimm"]=new[]{(ItemType.BlackFairyDust,9),(ItemType.SpiderEgg,9)},
            ["GreyPrince"]=new[]{(ItemType.FestiveWings,15),(ItemType.BabyGrinchMischiefWhistle,15)},
            ["NightmareGrimm"]=new[]{(ItemType.RainbowWings,15)},
            ["FinalBoss"]=new[]{(ItemType.LongRainbowTrailWings,10),(ItemType.SuspiciousLookingTentacle,1)},
            ["NailBros"]=new[]{(ItemType.ArcSurge,50)},
        };
        internal static readonly Dictionary<string,(int Life,int Defense,float Damage)> Combat = new Dictionary<string,(int,int,float)> {
            ["BigBuzzer"]=(1800,2,0.8f),["BigFly"]=(2200,3,0.85f),["FalseKnight"]=(3000,4,1.0f),["Mawlek"]=(3500,5,1.01f),
            ["MegaMossCharger"]=(3400,5,0.9f),["GhostAladar"]=(3800,6,0.95f),["GhostNoEyes"]=(4400,7,1.0f),["Hornet"]=(4800,8,1.02f),
            ["MageKnight"]=(4600,7,0.9f),["FlukeMother"]=(5400,8,1.0f),["DungDefender"]=(6000,9,1.01f),["GhostHu"]=(6800,10,1.02f),
            ["MantisLord"]=(7800,12,1.03f),["GhostXero"]=(8200,12,1.02f),["MageLord"]=(9000,13,1.03f),
            ["MegaBeamMiner"]=(9600,14,1.04f),["MegaJellyfish"]=(10500,15,1.04f),["FalseKnight@5"]=(18000,16,1.05f),
            ["JarCollector"]=(14500,17,1.04f),["InfectedKnight"]=(15500,18,1.05f),["BlackKnight"]=(17000,19,1.06f),["MageLord@5"]=(30000,20,1.07f),
            ["MimicSpider"]=(21000,21,1.06f),["GhostGalien"]=(23000,22,1.07f),["GhostMarmu"]=(25000,23,1.08f),
            ["Oblobble"]=(26000,24,1.07f),["HiveKnight"]=(29000,25,1.08f),
            ["Hornet@4"]=(35000,28,1.1f),["GhostMarkoth"]=(42000,31,1.13f),["InfectedKnight@5"]=(75000,34,1.16f),
            ["TraitorLord"]=(65000,38,1.18f),["Grimm"]=(75000,40,1.2f),["LobsterLancer"]=(85000,42,1.21f),
            ["GreyPrince"]=(95000,44,1.22f),["WhiteDefender"]=(105000,46,1.24f),["NightmareGrimm"]=(120000,48,1.26f),
            ["HollowKnight"]=(130000,50,1.26f),["FinalBoss"]=(200000,60,1.36f),
        };
        // The four pillar stand-ins are fought only in Godhome: each one's whole fight at stage 5 (Master, the Hall's
        // Attuned), which Godhome's stage share scales down below stage 5 (BossHealthBudgets.TryTotal); their defense and
        // damage are Godhome's own (BossStats.StageDefense, GodhomeBalance). As vanilla's pillars beside the Lunatic
        // Cultist (about three fifths of its life, half its defense), they stand under the Hollow Knight.
        internal static readonly Dictionary<string,int> GodhomeFights = new Dictionary<string,int> {
            ["NailBros"]=78300,["Nailsage"]=83700,["Paintmaster"]=89100,["HollowKnightPrime"]=97200,
        };
        const string Prefix="progression.vanilla.world.";
        internal static HashSet<string> DevClears;
        internal static ulong StateKey { get { ulong bits=0;int i=0;foreach(var key in All.Keys){if(Has(key))bits|=1UL<<i;i++;}return bits;} }
        static bool Has(string key)
        {
            if(DevClears!=null)return DevClears.Contains(key);
            var mod=TerraMod.Instance;
            if(mod==null)return false;
            // Existing saves keep their recorded victories; new victories get a scene-aware mark.
            return mod.GrantedAbilities.Contains(Prefix+key)
                || !mod.GrantedAbilities.Contains(Prefix+"migrated") && Bestiary.KillsOf(key)>0;
        }
        internal static bool Cleared(string key)=>Has(key);
        internal static Step RewardCap(int stage)
        {
            var limit=new[]{Step.Slime,Step.Eye,Step.Dungeon,Step.Hardmode,Step.MechAll,Step.MoonLord}[Math.Max(0,Math.Min(5,stage))];
            return Current<limit?Current:limit;
        }
        internal static void Initialize()
        {
            if(DevClears!=null)return;
            var mod=TerraMod.Instance;
            if(mod==null || mod.GrantedAbilities.Contains(Prefix+"migrated"))return;
            foreach(var old in All.Keys)if(Bestiary.KillsOf(old)>0)mod.GrantedAbilities.Add(Prefix+old);
            mod.GrantedAbilities.Add(Prefix+"migrated");
        }
        internal static bool AllowedEquipment(ItemType t,ItemDef d)
        {
            if(!(d.IsWeapon||d.IsArmor||d.IsAccessory||d.IsMount)||SpellRewards.Is(t))return true;
            foreach(var pair in All)
                if(pair.Value.Signature==t || Array.IndexOf(pair.Value.Gear,t)>=0)return Has(pair.Key);
            return d.Stage<3 || Current>=(d.Stage>=5?Step.Plantera:Step.Hardmode);
        }
        internal static Step Current
        {
            get
            {
                Step step=Step.PreBoss;
                void Raise(Step s,params string[] keys) {foreach(var key in keys)if(Has(key)){if(s>step)step=s;break;}}
                Raise(Step.Slime,"FalseKnight","BigFly","Mawlek","BigBuzzer");
                Raise(Step.Eye,"Hornet");Raise(Step.Evil,"DungDefender","FlukeMother");Raise(Step.Dungeon,"MantisLord","FalseKnight@5");
                Raise(Step.Hardmode,"BlackKnight","JarCollector","MageLord@5","InfectedKnight");
                int mechs=0;foreach(var key in Mechs)if(Has(key))mechs++;
                if(mechs>0)step=(Step)Math.Max((int)step,(int)Step.MechOne+mechs-1);
                Raise(Step.Plantera,"TraitorLord");Raise(Step.Golem,"LobsterLancer");Raise(Step.Cultist,"HollowKnight");
                Raise(Step.Pillars,"NailBros","Nailsage","Paintmaster","HollowKnightPrime");Raise(Step.MoonLord,"FinalBoss");
                return step;
            }
        }
        internal static void Credit(string key,string scene)
        {
            if(!All.TryGetValue(key,out var p) || string.IsNullOrEmpty(scene))return;
            bool godhome=ProgressionLoot.GodhomeScene(scene);
            if(godhome && !(p.Step==Step.Pillars && Current>=Step.Cultist))return;
            if(DevClears!=null){DevClears.Add(key);return;}
            var mod=TerraMod.Instance;if(mod==null)return;
            Initialize();
            mod.GrantedAbilities.Add(Prefix+key);
        }
        internal static bool BossMaterial(ItemType t)
        {
            switch(t) {
                case ItemType.ShadowScale:case ItemType.TissueSample:case ItemType.BeeWax:case ItemType.HallowedBar:
                case ItemType.SoulofMight:case ItemType.SoulofSight:case ItemType.SoulofFright:case ItemType.BeetleHusk:
                case ItemType.FragmentSolar:case ItemType.FragmentVortex:case ItemType.FragmentNebula:case ItemType.FragmentStardust:
                case ItemType.LunarOre:case ItemType.LunarCraftingStation:return true;
                default:return false;
            }
        }

        internal static bool Allowed(ItemType t)
        {
            switch(t)
            {
                case ItemType.MountCosmicCarKey:case ItemType.MountBrainScrambler:return Current>=Step.Golem;
                case ItemType.MountReindeerBells:case ItemType.MountWitchBroom:case ItemType.MountSpookyWoodMountItem:
                case ItemType.MountSantankMountItem:return Current>=Step.Plantera;
                case ItemType.MountShrimpyTruffle:return Has("WhiteDefender");
                case ItemType.MountQueenSlimeMountSaddle:return Has("HiveKnight");
                case ItemType.MountHoneyedGoggles:return Has("GhostHu");
                case ItemType.Hellstone:case ItemType.HellstoneBar:case ItemType.Meteorite:case ItemType.MeteoriteBar:return Current>=Step.Evil;
                case ItemType.Bone:return Has("MantisLord");
                case ItemType.ShadowScale:return Has("DungDefender");
                case ItemType.TissueSample:return Has("FlukeMother");
                case ItemType.BeeWax:return Has("GhostHu");
                case ItemType.SoulofMight:return Has("Hornet@4");
                case ItemType.SoulofSight:return Has("GhostMarkoth");
                case ItemType.SoulofFright:return Has("InfectedKnight@5");
                case ItemType.HallowedBar:return Current>=Step.MechOne;
                case ItemType.ChlorophyteOre:case ItemType.ChlorophyteBar:return Current>=Step.MechAll;
                case ItemType.Ectoplasm:case ItemType.SpectreBar:case ItemType.ShroomiteBar:case ItemType.Autohammer:
                case ItemType.BrokenHeroSword:return Current>=Step.Plantera;
                case ItemType.BeetleHusk:return Has("LobsterLancer");
                case ItemType.LunarCraftingStation:return Current>=Step.Cultist;
                case ItemType.FragmentSolar:return Has("NailBros");
                case ItemType.FragmentVortex:return Has("Nailsage");
                case ItemType.FragmentNebula:return Has("Paintmaster");
                case ItemType.FragmentStardust:return Has("HollowKnightPrime");
                case ItemType.LunarOre:case ItemType.LunarBar:return Has("FinalBoss");
                case ItemType.CobaltOre:case ItemType.CobaltBar:case ItemType.PalladiumOre:case ItemType.PalladiumBar:
                case ItemType.MythrilOre:case ItemType.MythrilBar:case ItemType.OrichalcumOre:case ItemType.OrichalcumBar:
                case ItemType.AdamantiteOre:case ItemType.AdamantiteBar:case ItemType.TitaniumOre:case ItemType.TitaniumBar:
                case ItemType.SoulofLight:case ItemType.SoulofNight:case ItemType.SoulofFlight:case ItemType.CrystalShard:
                case ItemType.PixieDust:case ItemType.UnicornHorn:case ItemType.CursedFlame:case ItemType.Ichor:
                case ItemType.SpiderFang:case ItemType.MythrilAnvil:case ItemType.OrichalcumAnvil:
                case ItemType.AdamantiteForge:case ItemType.TitaniumForge:return Current>=Step.Hardmode;
                default:return true;
            }
        }

        internal static void ApplyBands(ItemDef[] items)
        {
            // A material only bosses give (BossMaterial) is no earlier than the first boss paying it, so what is made of it
            // follows it there (NormalizeRecipeStages): Bee Wax is the Hu's (band 2), and the Hornet Staff and the Bee set with it.
            var firstBand=new Dictionary<ItemType,int>();
            foreach(var p in All.Values)if(p.Materials!=null)foreach(var m in p.Materials)
                if(BossMaterial(m.Type))firstBand[m.Type]=firstBand.TryGetValue(m.Type,out int b)?Math.Min(b,p.Band):p.Band;
            foreach(var pair in firstBand)if(items[(int)pair.Key]!=null)items[(int)pair.Key].Stage=Math.Max(items[(int)pair.Key].Stage,pair.Value);
            foreach(var pair in All)
            {
                var p=pair.Value;
                ProgressionBalance.BossRewards[pair.Key]=new ProgressionBalance.BossReward(p.Band,p.Gear,p.Signature);
                ProgressionBalance.BossSignatures[pair.Key]=p.Signature;
                foreach(var t in p.Gear)if(items[(int)t]!=null)items[(int)t].Stage=p.Band;
                if(p.Signature!=ItemType.None && items[(int)p.Signature]!=null)items[(int)p.Signature].Stage=p.Band;
                // Its separate rolls are its own too (the Honey Comb is the Hu's, band 2, and what is made of it follows).
                if(Extras.TryGetValue(pair.Key,out var extras))foreach(var x in extras)if(items[(int)x.Type]!=null)items[(int)x.Type].Stage=p.Band;
            }
        }
    }
}
