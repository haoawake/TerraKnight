namespace TerraKnight.Items
{
    /// <summary>
    /// The monster book's areas in the order a first run usually reaches them, each with the
    /// enemies first met there in the order they are met: the same route as the encounter
    /// report (怪物遭遇顺序.md), both made by one script. An enemy not on it goes by the area of
    /// its roster scene, after these.
    /// </summary>
    internal static partial class BestiaryRoute
    {
        static readonly string[] areaNames =
        {
            "国王山道", "遗忘十字路", "苍绿之径", "雾之峡谷", "真菌荒地", "泪水之城", "水晶山峰", "安息之地", "皇家水道", "呼啸悬崖", "远古盆地", "王国边缘", "愚人斗兽场", "蜂巢", "深巢", "王后花园", "深渊", "白色宫殿", "黑卵神殿", "梦境", "格林剧团", "神居",
        };

        static readonly string[][] areaRecords =
        {
            new[] { "Crawler", "Buzzer", "HealthScuttler" },
            new[] { "ZombieRunner", "Climber", "ZombieHornhead", "ZombieLeaper", "ZombieBarger", "ZombieShield", "Bouncer", "Spitter", "Worm", "FalseKnight", "PrayerSlug", "Roller", "BigFly", "Hatcher", "Hatchling", "Blocker", "ZombieGuard", "MenderBug", "OrangeScuttler", "Mawlek", "AngryBuzzer", "BurstingBouncer", "BurstingZombie", "SpittingZombie", "EggSac", "ZombieMiner@0" },
            new[] { "MossWalker", "MossmanRunner", "MossmanShaker", "Mosquito", "BlobFlyer", "MossCharger", "SnapperTrap", "PlantShooter", "AcidWalker", "AcidFlyer", "Pigeon", "MossKnight", "LazyFlyer", "Hornet", "MegaMossCharger", "BigBuzzer", "GhostNoEyes" },
            new[] { "JellyCrawler", "Jellyfish", "ZapBug", "MegaJellyfish" },
            new[] { "FungCrawler", "FungoonBaby", "FungusFlyer", "FungifiedZombie", "MushroomTurret", "MushroomBaby", "MushroomRoller", "MushroomBrawler", "MantisFlyerChild", "Mantis", "MantisLord", "GhostHu" },
            new[] { "Sentry", "SentryFat", "FlyingSentrySword", "FlyingSentryJavelin", "CeilingDropper", "RoyalDandy", "RoyalCoward", "RoyalPlumper", "GreatShieldZombie", "GorgeousHusk", "Mage", "MageBlob", "MageBalloon", "MageKnight", "MageLord", "BlackKnight", "JarCollector" },
            new[] { "ZombieMiner", "BeamMiner", "LaserBug", "CrystalFlyer", "MinesCrawler", "CrystalCrawler", "GrubMimic", "MegaBeamMiner" },
            new[] { "Mummy", "GhostXero" },
            new[] { "Inflater", "FlipHopper", "Flukefly", "Flukeman", "FatFluke", "DungDefender", "FlukeMother" },
            new[] { "GhostAladar" },
            new[] { "AbyssCrawler", "LesserMawlek", "MawlekTurret", "OrangeBalloon", "InfectedKnight" },
            new[] { "SuperSpitter", "BlowFly", "Hopper", "GiantHopper", "Hornet@4", "GhostMarkoth", "PaleLurker" },
            new[] { "ColShield", "ColRoller", "Zote", "ColMosquito", "Blobble", "ColHopper", "ColMiner", "ColWorm", "ColFlyingSentry", "Oblobble", "ElectricMage", "LobsterLancer" },
            new[] { "BeeHatchling", "BeeStinger", "ZombieHive", "BigBee", "HiveKnight" },
            new[] { "BabyCentipede", "CentipedeHatcher", "BigCentipede", "SpiderCorpse", "ShootSpider", "MiniSpider", "SpiderFlyer", "SlashSpider", "MimicSpider", "GhostGalien" },
            new[] { "MossFlyer", "GrassHopper", "MantisHeavyFlyer", "GardenZombie", "MossKnightFat", "HeavyMantis", "TraitorLord", "GhostMarmu" },
            new[] { "AbyssTendril", "Sibling" },
            new[] { "PalaceFly", "RoyalGuard", "WhiteRoyal" },
            new[] { "HollowKnight" },
            new[] { "FalseKnight@5", "MageLord@5", "InfectedKnight@5", "WhiteDefender", "GreyPrince", "ZotelingBuzzer", "ZotelingBalloon", "FinalBoss" },
            new[] { "FlameBearerSmall", "FlameBearerMed", "Grimm", "FlameBearerLarge", "NightmareGrimm" },
            new[] { "NailBros", "Paintmaster", "Nailsage", "HollowKnightPrime", "ZotelingHopper" },
        };
    }
}
