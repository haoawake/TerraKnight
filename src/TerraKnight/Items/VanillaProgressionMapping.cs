using System.Collections.Generic;
namespace TerraKnight.Items {
    internal static partial class ProgressionBalance {
        // Per-boss identity rewards. The original four-class pool remains additional loot.
        internal static readonly Dictionary<string,ItemType> BossSignatures=new Dictionary<string,ItemType> {
            ["FalseKnight"]=ItemType.RoyalGel,
            ["BigFly"]=ItemType.SlimeWhip,
            ["Mawlek"]=ItemType.BloodyMachete,
            ["BigBuzzer"]=ItemType.SlimeStaff,
            ["Hornet"]=ItemType.ShieldOfCthulhu,
            ["MegaMossCharger"]=ItemType.StingerNecklace,
            ["GhostAladar"]=ItemType.SweetheartNecklace,
            ["GhostNoEyes"]=ItemType.Terragrim,
            ["MantisLord"]=ItemType.BoneGlove,
            ["MageKnight"]=ItemType.CelestialCuffs,
            ["MageLord"]=ItemType.ManaFlower,
            ["MegaBeamMiner"]=ItemType.LavaSkull,
            ["DungDefender"]=ItemType.WormScarf,
            ["FlukeMother"]=ItemType.BrainOfConfusion,
            ["MegaJellyfish"]=ItemType.ObsidianShield,
            ["GhostHu"]=ItemType.HiveFive,
            ["GhostMarmu"]=ItemType.StarVeil,
            ["TraitorLord"]=ItemType.SporeSac,
            ["FalseKnight@5"]=ItemType.BoneHelm,
            ["BlackKnight"]=ItemType.WarriorEmblem,
            ["JarCollector"]=ItemType.ManaCloak,
            ["MimicSpider"]=ItemType.StarCloak,
            ["GhostGalien"]=ItemType.FrozenTurtleShell,
            ["GhostXero"]=ItemType.PowerGlove,
            ["MageLord@5"]=ItemType.SorcererEmblem,
            ["HiveKnight"]=ItemType.HiveBackpack,
            ["Hornet@4"]=ItemType.BlackBelt,
            ["InfectedKnight"]=ItemType.SummonerEmblem,
            ["InfectedKnight@5"]=ItemType.VolatileGelatin,
            ["GhostMarkoth"]=ItemType.StalkersQuiver,
            ["Oblobble"]=ItemType.RangerEmblem,
            ["LobsterLancer"]=ItemType.ShinyStone,
            ["Zote"]=ItemType.StressBall,
            ["PaleLurker"]=ItemType.AnkhShield,
            ["Grimm"]=ItemType.FireGauntlet,
            ["NightmareGrimm"]=ItemType.EmpressFlightBooster,
            ["WhiteDefender"]=ItemType.FrozenShield,
            ["GreyPrince"]=ItemType.PapyrusScarab,
            ["HollowKnight"]=ItemType.DestroyerEmblem,
            ["NailBros"]=ItemType.HeroShield,
            ["Paintmaster"]=ItemType.CelestialEmblem,
            ["Nailsage"]=ItemType.MasterNinjaGear,
            ["HollowKnightPrime"]=ItemType.CelestialShell,
            ["FinalBoss"]=ItemType.GravityGlobe,
        };
        internal static readonly HashSet<ItemType> EarlyExplorationItems=new HashSet<ItemType> {
            ItemType.FlyingCarpet,ItemType.CloudinaBottle,ItemType.ShinyRedBalloon,
            ItemType.HermesBoots,ItemType.ClimbingClaws,ItemType.FledglingWings,
            ItemType.BandofRegeneration,ItemType.BandofStarpower,ItemType.FlowerBoots,
            ItemType.JellyfishNecklace,ItemType.TreasureMagnet
        };
        internal static readonly Dictionary<string,ItemType[]> ExplorationSources=new Dictionary<string,ItemType[]> {
            ["Crawler"]=new[]{ItemType.FlyingCarpet,ItemType.ClimbingClaws},
            ["ZombieRunner"]=new[]{ItemType.FlyingCarpet,ItemType.HermesBoots},
            ["Buzzer"]=new[]{ItemType.CloudinaBottle,ItemType.ShinyRedBalloon},
            ["ZombieHornhead"]=new[]{ItemType.FledglingWings,ItemType.BandofRegeneration},
            ["ZombieBarger"]=new[]{ItemType.BandofStarpower,ItemType.FlowerBoots},
            ["Spitter"]=new[]{ItemType.JellyfishNecklace,ItemType.TreasureMagnet}
        };
        internal static readonly HashSet<ItemType> BasicBossExcluded=new HashSet<ItemType>(EarlyExplorationItems) {
            ItemType.FeralClaws,ItemType.HoneyComb,ItemType.SharkToothNecklace,ItemType.PanicNecklace,
            ItemType.CobaltShield,ItemType.MagmaStone,ItemType.RifleScope,ItemType.MoonCharm
        };
        internal static readonly ItemType[] LunarWeapons={ItemType.Meowmere,ItemType.SDMG,ItemType.LastPrism,ItemType.LunarPortalStaff};
        internal static readonly ItemType[][] LunarArmor={
            new[]{ItemType.SolarFlareHelmet,ItemType.SolarFlareBreastplate,ItemType.SolarFlareLeggings},
            new[]{ItemType.VortexHelmet,ItemType.VortexBreastplate,ItemType.VortexLeggings},
            new[]{ItemType.NebulaHelmet,ItemType.NebulaBreastplate,ItemType.NebulaLeggings},
            new[]{ItemType.StardustHelmet,ItemType.StardustBreastplate,ItemType.StardustLeggings}
        };
        internal static readonly HashSet<ItemType> ExclusiveBossItems=new HashSet<ItemType> {
            ItemType.ShieldOfCthulhu,ItemType.RoyalGel,ItemType.WormScarf,ItemType.BrainOfConfusion,
            ItemType.BoneGlove,ItemType.BoneHelm,ItemType.HiveBackpack,ItemType.SporeSac,ItemType.ShinyStone,
            ItemType.EmpressFlightBooster,ItemType.GravityGlobe
        };
    }
    internal static partial class ProgressionLoot {
        static void AddVanillaBossMaterials(string key,List<EnemyDrops.Drop> rows,Dictionary<ItemType,float> ores,Dictionary<ItemType,float> mats) {
            void Bundle(ItemType t,int min,int max,int group=0) {
                ores.Remove(t);mats.Remove(t);rows.RemoveAll(d=>d.Type==t);
                rows.Add(Row(t,group,1,min,max));
            }
            switch(key) {
                case "FalseKnight":Bundle(ItemType.Gel,20,40);break;
                case "Hornet":
                    Bundle(ItemType.DemoniteOre,30,90,209);Bundle(ItemType.CrimtaneOre,30,90,209);break;
                case "DungDefender":Bundle(ItemType.DemoniteOre,80,110);Bundle(ItemType.ShadowScale,20,40);break;
                case "FlukeMother":Bundle(ItemType.CrimtaneOre,80,110);Bundle(ItemType.TissueSample,20,40);break;
                case "HiveKnight":
                    Bundle(ItemType.BeeWax,17,29);
                    foreach(var t in new[]{ItemType.BeeKeeper,ItemType.BeesKnees,ItemType.BeeGun})rows.Add(Row(t,209,1));
                    break;
            }
        }
    }

}
