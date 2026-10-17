using TerraKnight.World;

namespace TerraKnight.Items
{
    internal enum ItemType
    {
        None,
        CopperShortsword,
        CopperBroadsword,
        CopperPickaxe,
        DirtBlock,
        StoneBlock,
        Wood,
        Zenith,
        HermesBoots,
        CloudinaBottle,
        ClimbingClaws,
        ShieldOfCthulhu,
        GrapplingHook,

        // Melee.
        IronBroadsword, GoldBroadsword, PlatinumBroadsword, LightsBane, Muramasa, BladeofGrass,
        FieryGreatsword, NightsEdge, Excalibur, TerraBlade, EnchantedBoomerang,
        Starfury, EnchantedSword, IceBlade, BeeKeeper, DarkLance, Trident, WoodenBoomerang, Flamarang, ThornChakram,
        Musket, Boomstick, Shuriken, ThrowingKnife, MagicMissile, Vilethorn, AquaScepter, Flamelash,
        // Hardmode melee (ItemsHardmode.cs).
        CobaltSword, PalladiumSword, MythrilSword, OrichalcumSword, AdamantiteSword, TitaniumSword,
        CobaltNaginata, PalladiumPike, MythrilHalberd, OrichalcumHalberd, AdamantiteGlaive, TitaniumTrident, Gungnir, MushroomSpear, ChlorophytePartisan, NorthPole,
        LightDisc, Bananarang, PossessedHatchet, PaladinsHammer,
        // Hardmode ranged (ItemsHardmode.cs).
        Megashark, ClockworkAssaultRifle, Uzi, VenusMagnum, SniperRifle, TacticalShotgun, Gatligator, ChainGun, SDMG,
        CobaltRepeater, PalladiumRepeater, MythrilRepeater, OrichalcumRepeater, AdamantiteRepeater, TitaniumRepeater, HallowedRepeater,
        DaedalusStormbow, ChlorophyteShotbow, Tsunami, Marrow, IceBow, PulseBow, ShadowFlameBow,
        // Hardmode magic.
        CrystalStorm, CursedFlames, GoldenShower, FrostStaff, PoisonStaff, VenomStaff, UnholyTrident, MeteorStaff, SpectreStaff, InfernoFork,
        ShadowbeamStaff, HeatRay, BubbleGun, RazorbladeTyphoon, BlizzardStaff, SkyFracture, LeafBlower,
        LaserRifle, BatScepter, CrystalVileShard, MagnetSphere, RainbowRod, NimbusRod,
        MagicalHarp, ClingerStaff, ToxicFlask, BookofSkulls, SpiritFlame,
        NebulaBlaze, LunarFlareBook, NebulaArcanum, LastPrism, ChargedBlasterCannon, MedusaHead, IceRod,
        // Pickaxes.
        IronPickaxe, GoldPickaxe, NightmarePickaxe, MoltenPickaxe, CobaltPickaxe, MythrilPickaxe, TitaniumPickaxe, Picksaw,
        // Ranged and ammo.
        WoodenBow, IronBow, GoldBow, DemonBow, MoltenFury, FlintlockPistol, Handgun, Minishark, PhoenixBlaster, Grenade,
        WoodenArrow, FlamingArrow, UnholyArrow, JestersArrow, MusketBall, MeteorShot,
        HellfireArrow, HolyArrow, CursedArrow, FrostburnArrow, ChlorophyteArrow, IchorArrow, VenomArrow, BoneArrow, EndlessQuiver, LuminiteArrow, ShimmerArrow,
        SilverBullet, TungstenBullet, CrystalBullet, CursedBullet, IchorBullet, ChlorophyteBullet, HighVelocityBullet, VenomBullet, PartyBullet, NanoBullet,
        ExplodingBullet, GoldenBullet, EndlessMusketPouch, LuminiteBullet,
        // Magic.
        WandofSparking, AmethystStaff, RubyStaff, DiamondStaff, WaterBolt, SpaceGun, DemonScythe, FlowerofFire,
        // Explosives and building.
        Bomb, Dynamite, Torch, SandBlock, WoodPlatform, Rope,
        // Armor.
        MiningHelmet,
        CopperHelmet, CopperChainmail, CopperGreaves, IronHelmet, IronChainmail, IronGreaves, GoldHelmet, GoldChainmail, GoldGreaves,
        ShadowHelmet, ShadowScalemail, ShadowGreaves, NecroHelmet, NecroBreastplate, NecroGreaves,
        JungleHat, JungleShirt, JunglePants, MoltenHelmet, MoltenBreastplate, MoltenGreaves,
        // Accessories.
        RocketBoots, FlyingCarpet, ShinyRedBalloon, LuckyHorseshoe, Aglet, AnkletoftheWind, FeralClaws, BandofRegeneration, BandofStarpower,
        ManaFlower, CobaltShield, CrossNecklace, MagicQuiver, WarriorEmblem, RangerEmblem, SorcererEmblem, Flipper,
        FrogLeg, SpectreBoots, LightningBoots, BlizzardinaBottle, SandstorminaBottle, FartinaJar, CloudinaBalloon, BundleofBalloons, ShoeSpikes,
        FledglingWings, AngelWings, DemonWings, ObsidianShield, MagmaStone, SharkToothNecklace, PowerGlove, AvengerEmblem, WormScarf,
        StarCloak, HoneyComb, PanicNecklace, MagicCuffs, PhilosophersStone, Radar,
        // Summoner.
        SlimeStaff, FlinxStaff, VampireFrogStaff, SpiderStaff, PirateStaff, FinchStaff, HornetStaff, ImpStaff, AbigailsFlower, OpticStaff, SanguineStaff, StardustDragonStaff,
        DeadlySphereStaff, TempestStaff, XenoStaff, StardustCellStaff, RavenStaff, Terraprisma, DesertTigerStaff, BladeStaff, PygmyStaff,
        FlameburstCane, FlameburstRod, FlameburstStaff, BallistaCane, BallistaRod, BallistaStaff,
        ExplosiveTrapCane, ExplosiveTrapRod, ExplosiveTrapStaff, LightningAuraCane, LightningAuraRod, LightningAuraStaff,
        QueenSpiderStaff, StaffoftheFrostHydra, HoundiusShootius, LunarPortalStaff, RainbowCrystalStaff, BarnacleStaff,
        CorruptWhip, CrimsonWhip, MeteorWhip, FlowerWhip, EelWhip, ConstellationWhip, MoonLordWhip,
        MechanicalGlove, FireGauntlet, BerserkerGlove, FleshKnuckles, PutridScent, RifleScope, SniperScope, ReconScope, MoltenQuiver, StalkersQuiver,
        ManaRegenerationBand, CelestialMagnet, CelestialEmblem, CelestialCuffs, ArcaneFlower, ManaCloak, DestroyerEmblem, EyeoftheGolem,
        ApprenticeScarf, SquireShield, HuntressBuckler, MonkBelt, WickedClaws, RoyalGuardHarness,
        AdhesiveBandage, ArmorPolish, Bezoar, Blindfold, FastClock, Megaphone, Nazar, Vitamins, TrifoldMap, PocketMirror,
        ArmorBracing, MedicatedBandage, ThePlan, CountercurseMantra, ReflectiveShades, AnkhCharm, AnkhShield,
        PaladinsShield, FrozenTurtleShell, FrozenShield, HeroShield, SilverShield, RestorationShield,
        LavaCharm, ObsidianRose, ObsidianSkullRose, LavaSkull, MoltenSkullRose,
        CharmofMyths, MoonCharm, NeptunesShell, MoonShell, CelestialShell, SunStone, MoonStone, CelestialStone, ShinyStone, BrainOfConfusion, SporeSac,
        BeeCloak, StarVeil, SweetheartNecklace, StingerNecklace, CrossedHeartNecklace, SeraphNecklace, DruidicSerpentCloak,
        RoyalGel, VolatileGelatin, BoneGlove, HiveBackpack, BoneHelm, HandWarmer, StressBall, DeadCellsRamRune,
        DivingGear, JellyfishNecklace, JellyfishDivingGear, ArcticDivingGear,
        DiscountCard, LuckyCoin, GoldRing, CoinRing, GreedyRing, TreasureMagnet, AncientChisel, SpectreGoggles,
        SilverBracer, SnakeBand, MobiusStrip, WickedArmlet, OuroborosRing, TwilightGrasp, ArmletOfRuin,
        HarpyCharm, PhoenixQuiver, PoisonBarb, SweetBarb, SnappingStone, Pyroclast, ChaosCylinder, ChaosCylinderFull, ChaosCylinderRandom, ChaosCylinderSimple,
        CatalystBand, MysticArtsSash, HeavySling, ScoutSling, TemplarSling,
        RedsWings, Jetpack, ButterflyWings, FairyWings, HarpyWings, BoneWings, FlameWings, FrozenWings, GhostWings, SteampunkWings, LeafWings, BatWings, BeeWings,
        DTownsWings, WillsWings, CrownosWings, CenxsWings, TatteredFairyWings, SpookyWings, Hoverboard, FestiveWings, BeetleWings, FinWings, FishronWings, MothronWings,
        BejeweledValkyrieWing, WingsSolar, WingsVortex, WingsNebula, WingsStardust, Yoraiz0rWings, JimsWings, SkiphsWings, LokisWings, BetsyWings, ArkhalisWings,
        LeinforsWings, GhostarsWings, SafemanWings, FoodBarbarianWings, GroxTheGreatWings, RainbowWings, LongRainbowTrailWings, ChickenBonesWings, ChippysWings,
        HeroicisWings, KazzymodusWings, LunasWings, EmpressFlightBooster,
        WaterWalkingBoots, ObsidianWaterWalkingBoots, MoltenCharm, LavaWaders, IceSkates, FlurryBoots, SailfishBoots, SandBoots, FairyBoots, FlowerBoots,
        AmphibianBoots, FrogFlipper, FrogGear, FrogWebbing, FrostsparkBoots, TerrasparkBoots, HellfireTreads,
        TigerClimbingGear, Tabi, BlackBelt, MasterNinjaGear, HoneyBalloon, BlueHorseshoeBalloon, WhiteHorseshoeBalloon, YellowHorseshoeBalloon, FartInABalloon,
        BalloonPufferfish, SharkronBalloon, BalloonHorseshoeFart, BalloonHorseshoeHoney, BalloonHorseshoeSharkron, TsunamiInABottle, HorseshoeBundle,
        ObsidianHorseshoe, FloatingTube, PortableStool, ShimmerCloak, Magiluminescence,
        CobaltHat, CobaltHelmet, CobaltMask, CobaltBreastplate, CobaltLeggings, MythrilHood, MythrilHelmet, MythrilHat, MythrilChainmail, MythrilGreaves,
        AdamantiteHeadgear, AdamantiteHelmet, AdamantiteMask, AdamantiteBreastplate, AdamantiteLeggings, PalladiumMask, PalladiumHelmet, PalladiumHeadgear,
        PalladiumBreastplate, PalladiumLeggings, OrichalcumMask, OrichalcumHelmet, OrichalcumHeadgear, OrichalcumBreastplate, OrichalcumLeggings, TitaniumMask,
        TitaniumHelmet, TitaniumHeadgear, TitaniumBreastplate, TitaniumLeggings, FrostHelmet, FrostBreastplate, FrostLeggings, AncientBattleArmorHat,
        AncientBattleArmorShirt, AncientBattleArmorPants, HallowedMask, HallowedHelmet, HallowedHeadgear, HallowedHood, HallowedPlateMail, HallowedGreaves,
        AncientHallowedMask, AncientHallowedHelmet, AncientHallowedHeadgear, AncientHallowedHood, AncientHallowedPlateMail, AncientHallowedGreaves,
        CrystalNinjaHelmet, CrystalNinjaChestplate, CrystalNinjaLeggings, ChlorophyteMask, ChlorophyteHelmet, ChlorophyteHeadgear, ChlorophyteVisor,
        ChlorophytePlateMail, ChlorophyteGreaves, TurtleHelmet, TurtleScaleMail, TurtleLeggings, TikiMask, TikiShirt, TikiPants, SpookyHelmet,
        SpookyBreastplate, SpookyLeggings, ShroomiteHeadgear, ShroomiteMask, ShroomiteHelmet, ShroomiteBreastplate, ShroomiteLeggings, SpectreHood,
        SpectreMask, SpectreRobe, SpectrePants, BeetleHelmet, BeetleScaleMail, BeetleShell, BeetleLeggings, SolarFlareHelmet, SolarFlareBreastplate,
        SolarFlareLeggings, VortexHelmet, VortexBreastplate, VortexLeggings, NebulaHelmet, NebulaBreastplate, NebulaLeggings, StardustHelmet,
        StardustBreastplate, StardustLeggings,
        WoodYoyo, Rally, CorruptYoyo, CrimsonYoyo, JungleYoyo, Code1, HiveFive, Valor, Cascade, Chik, Amarok, HelFire, Gradient, FormatC, Code2, Yelets, RedsYoyo,
        ValkyrieYoyo, TheEyeOfCthulhu, Kraken, Terrarian,
        RedString, OrangeString, YellowString, LimeString, GreenString, TealString, CyanString, SkyBlueString, BlueString, PurpleString, VioletString, PinkString,
        BrownString, WhiteString, RainbowString, BlackString, BlackCounterweight, BlueCounterweight, GreenCounterweight, PurpleCounterweight, RedCounterweight,
        YellowCounterweight, YoYoGlove, YoyoBag, MagicString, MagicYoyoBag, StrungCounterweight,
        Mace, FlamingMace, BallOHurt, TheMeatball, BlueMoon, Sunfury, DaoofPow, DripplerFlail, FlowerPow, FlaironFlail,
        LeatherWhip, Snapthorn, SpinalTap, Firecracker, CoolWhip, Durendal, MorningStar, SlimeWhip, CobWhip, DarkHarvest, Kaleidoscope,
        FlinxFurCoat, BeeHeadgear, BeeBreastplate, BeeGreaves, ObsidianOutlawHat, ObsidianLongcoat, ObsidianPants, SpiderMask, SpiderBreastplate, SpiderGreaves,
        PygmyNecklace, PapyrusScarab, SummonerEmblem, NecromanticScroll, HerculesBeetle,
        WoodenSword, EbonwoodSword, RichMahoganySword, PearlwoodSword, ShadewoodSword, PalmWoodSword, BorealWoodSword, AshWoodSword, TungstenBroadsword,
        LeadBroadsword, TinBroadsword, SilverBroadsword, CactusSword, BoneSword, CandyCaneSword, Katana, FalconBlade, Cutlass, DyeTradersScimitar,
        PurpleClubberfish, Bladetongue, BreakerBlade, BloodButcherer, PsychoKnife, DD2SquireDemonSword, ZombieArm, TentacleSpike, HamBat, BatBat, SlapHand,
        TaxCollectorsStickOfDoom, BladedGlove, FetidBaghnakhs, BreathingReed, StaffofRegrowth, Sickle, Umbrella, TragicUmbrella, GravediggerShovel, LucyTheAxe,
        BluePhaseblade, RedPhaseblade, GreenPhaseblade, PurplePhaseblade, WhitePhaseblade, YellowPhaseblade, OrangePhaseblade, PinkPhaseblade, RainbowPhaseblade,
        BluePhasesaber, RedPhasesaber, GreenPhasesaber, PurplePhasesaber, WhitePhasesaber, YellowPhasesaber, OrangePhasesaber, PinkPhasesaber, RainbowPhasesaber,
        Keybrand, AntlionClaw, StylistKilLaKillScissorsIWish, WaffleIron, ChristmasTreeSword,
        Frostbrand, BeamSword, IceSickle, DeathSickle, ChlorophyteClaymore, DD2SquireBetsySword,
        IronShortsword, TinShortsword, LeadShortsword, SilverShortsword, TungstenShortsword, GoldShortsword, PlatinumShortsword, Gladius, Ruler, ChlorophyteSaber,
        Arkhalis, Terragrim, PiercingStarlight, TrueCopperShortsword,
        Spear, TheRottedFork, Swordfish, ObsidianSwordfish, SlimeSpear, ScourgeoftheCorruptor, DayBreak,
        MonkStaffT1, MonkStaffT2, MonkStaffT3,
        IceBoomerang, FruitcakeChakram, BloodyMachete, VampireKnives, ShadowFlameKnife, FlyingKnife, Shroomerang, CombatWrench, Trimarang, Axearang,
        BouncingShield, DeadCellsFlint,
        GolemFist, KOCannon, ChainKnife, ChainGuillotines, Anchor, SolarEruption,
        // Sweep R1: bows, hammers, axes, hamaxes, pickaxes.
        CopperBow, TinBow, LeadBow, SilverBow, TungstenBow, PlatinumBow, EbonwoodBow, ShadewoodBow, RichMahoganyBow, PalmWoodBow, BorealWoodBow, AshWoodBow,
        PearlwoodBow, TendonBow,
        WoodenHammer, CopperHammer, TinHammer, IronHammer, LeadHammer, SilverHammer, TungstenHammer, GoldHammer, PlatinumHammer, EbonwoodHammer, ShadewoodHammer,
        RichMahoganyHammer, PalmWoodHammer, BorealWoodHammer, AshWoodHammer, PearlwoodHammer, TheBreaker, FleshGrinder, Pwnhammer, Hammush, ChlorophyteWarhammer,
        CopperAxe, TinAxe, IronAxe, LeadAxe, SilverAxe, TungstenAxe, GoldAxe, PlatinumAxe, WarAxeoftheNight, BloodLustCluster, CobaltWaraxe, PalladiumWaraxe,
        MythrilWaraxe, OrichalcumWaraxe, AdamantiteWaraxe, TitaniumWaraxe, ChlorophyteGreataxe, AcornAxe, TheAxe,
        MeteorHamaxe, MoltenHamaxe, SpectreHamaxe, LunarHamaxeSolar, LunarHamaxeVortex, LunarHamaxeNebula, LunarHamaxeStardust,
        VortexPickaxe, NebulaPickaxe, SolarFlarePickaxe, StardustPickaxe,
        TinPickaxe, CactusPickaxe, LeadPickaxe, SilverPickaxe, TungstenPickaxe, PlatinumPickaxe, BonePickaxe, CandyCanePickaxe, ReaverShark, DeathbringerPickaxe,
        PalladiumPickaxe, OrichalcumPickaxe, AdamantitePickaxe, PickaxeAxe, ChlorophytePickaxe, SpectrePickaxe, ShroomiteDiggingClaw,
        // Sweep R2: drills and chainsaws.
        CobaltChainsaw, MythrilChainsaw, AdamantiteChainsaw, CobaltDrill, MythrilDrill, AdamantiteDrill, Drax, PalladiumDrill, PalladiumChainsaw, OrichalcumDrill,
        OrichalcumChainsaw, TitaniumDrill, TitaniumChainsaw, ChlorophyteDrill, ChlorophyteChainsaw, ChlorophyteJackhammer, SawtoothShark, VortexDrill, NebulaDrill,
        SolarFlareDrill, StardustDrill, LaserDrill, ButchersChainsaw,
        // Sweep R3: thrown consumables.
        SpikyBall, PoisonedKnife, HolyWater, UnholyWater, BloodWater, Beenade, StickyGrenade, BouncyGrenade, MolotovCocktail, RottenEgg, StarAnise, Javelin,
        BoneJavelin, BoneDagger, FrostDaggerfish, PartyGirlGrenade, MagicDagger, MudBall,
        // Sweep R4a: guns and bows on the ammo there is.
        Shotgun, RedRyder, Revolver, OnyxBlaster, QuadBarrelShotgun, Xenopopper, AerialBane, BeesKnees, HellwingBow, Toxikarp, PewMaticHorn, StarCannon,
        SuperStarCannon, Harpoon, VortexBeater, Phantasm, PhantomPhoenix, Eventide, PiranhaGun,
        // Sweep R4b: the rocket launchers and their ammo.
        GrenadeLauncher, RocketLauncher, ProximityMineLauncher, SnowmanCannon, RocketI, RocketII, RocketIII, RocketIV, ClusterRocketI, ClusterRocketII, MiniNukeI, MiniNukeII,
        Celebration, CelebrationMk2, ElectrosphereLauncher,
        // Sweep R4b3: darts and the Sandgun.
        Seed, PoisonDart, CrystalDart, CursedDart, IchorDart, Blowpipe, Blowgun, DartPistol, DartRifle, Sandgun,
        Flamethrower, ElfMelter, FlareGun, Flare, BlueFlare, SpelunkerFlare, CursedFlare,
        SnowballCannon, Snowball, Stynger, StyngerBolt, CandyCornRifle, CandyCorn, JackOLanternLauncher, ExplosiveJackOLantern, StakeLauncher, Stake, NailGun, Nail,
        AleTosser, AcornSlingshot,
        // Sweep R5: magic.
        TopazStaff, SapphireStaff, EmeraldStaff, AmberStaff,
        CrimsonRod, FlowerofFrost, BeeGun, WaspGun, NettleBurst, RainbowGun, StaffofEarth, Razorpine,
        CrystalSerpent, SoulDrain, ZapinatorGray, ZapinatorOrange, BetsysWrath, SharpTears,
        ShadowFlameHexDoll, BookStaff, LaserMachinegun, SparkleGuitar, Nightglow,
        WeatherPain, WandofFrosting, PrettyMirror, DaybloomStaff, GlacierFang, Petalstorm,
        MushroomBoiStaff, CattivaStaff, FoxsparksStaff, TrustyCattivaStaff, TrustyFoxsparksStaff, ClayPotStaff, ForbiddenStaff,
        // Sweep R6: the deferred ranged weapons and ammo, the Flairon's other form.
        PaintballGun, BarrelLauncher, KillingDeck, RainbowFlare, ShimmerFlare, WetRocket, LavaRocket, HoneyRocket, DryRocket, Flairon, GravityGlobe,
        // Sweep R7: the grappling hooks, the permanent boosters, the potions and flasks, the chest.
        IvyWhip, DualHook, WebSlinger, AmethystHook, TopazHook, SapphireHook, EmeraldHook, RubyHook, DiamondHook, SkeletronHand, BatHook, SpookyHook, CandyCaneHook,
        ChristmasHook, FishHook, SlimeHook, AntiGravityHook, TendonHook, ThornHook, IlluminantHook, WormHook, LunarHook, StaticHook, AmberHook, SquirrelHook, QueenSlimeHook,
        LifeFruit, DemonHeart, AegisCrystal, AegisFruit, ArcaneCrystal, GalaxyPearl, GummyWorm, Ambrosia, ArtisanLoaf,
        Mushroom, RestorationPotion, ObsidianSkinPotion, RegenerationPotion, SwiftnessPotion, GillsPotion, IronskinPotion, ManaRegenerationPotion, MagicPowerPotion,
        FeatherfallPotion, SpelunkerPotion, InvisibilityPotion, ShinePotion, NightOwlPotion, BattlePotion, ThornsPotion, WaterWalkingPotion, ArcheryPotion, HunterPotion,
        GravitationPotion, GreaterHealingPotion, GreaterManaPotion, BottledHoney, FlaskofVenom, FlaskofCursedFlames, FlaskofFire, FlaskofGold, FlaskofIchor, FlaskofNanites,
        FlaskofParty, FlaskofPoison, SuperManaPotion, MiningPotion, HeartreachPotion, CalmingPotion, BuilderPotion, TitanPotion, FlipperPotion, SummoningPotion,
        TrapsightPotion, AmmoReservationPotion, LifeforcePotion, EndurancePotion, RagePotion, InfernoPotion, WrathPotion, FishingPotion, SonarPotion, CratePotion,
        WarmthPotion, RecallPotion, TeleportationPotion, StrangeBrew, SuperHealingPotion, LuckPotionLesser, LuckPotion, LuckPotionGreater, BiomeSightPotion,
        LifeFruitHealingPotion, Chest,
        // <generated> (scratchpad/tkgen/gen.py) Materials and equipment intermediates for the vanilla recipes.
        CopperBar, IronBar, Lens, FallenStar, BlackLens, GoldBar, DemoniteBar, MeteoriteBar, Cobweb, Obsidian, Ruby, Amethyst, Diamond, Silk, Feather, PlatinumBar, IceTorch, FlinxFur, ShadowScale, Bone, Stinger, Vine, JungleSpores, Confetti, BeeWax, HellstoneBar, CobaltBar, MythrilBar, PixieDust, CrystalShard, SoulofLight, SoulofNight, CursedFlame, UnicornHorn, SoulofFright, SoulofMight, SoulofSight, SoulofFlight, ChlorophyteBar, TitaniumBar, PalladiumBar, OrichalcumBar, AdamantiteBar, HallowedBar, Ichor, Cog, ExplosivePowder, GoldDust, EmptyBullet, FrostCore, SpiderFang, VialofVenom, Nanites, BrokenHeroSword, FragmentStardust, LunarBar, ButterflyDust, GiantHarpyFeather, BoneFeather, FireFeather, IceFeather, BrokenBatWing, TatteredBeeWing, BlackFairyDust, SpookyTwig, BeetleHusk, SpectreBar, FragmentSolar, FragmentVortex, FragmentNebula, Topaz, AncientBattleArmorMaterial, TurtleShell, ShroomiteBar, SpookyWood, Ebonwood, RichMahogany, Pearlwood, Shadewood, PalmWood, BorealWood, AshWood, TinBar, LeadBar, TungstenBar, SilverBar, Cactus, Sapphire, Emerald, Amber, PinkPearl, Daybloom, Moonglow, RainbowBrick, GlowingMushroom, MushroomGrassSeeds, ClayBlock, Blinkroot, LightShard, PalworldKinshipPeach, Deathweed, Waterleaf, Fireblossom, Shiverthorn, Bottle, Glass, Coral, SharkFin, IronOre, GoldOre, RottenChunk, Hellstone, Ectoplasm, ArmoredCavefish, Damselfish, CrimsonTigerfish, FrostMinnow, SpecularFish, Prismite, VariegatedLardfish, FlarefinKoi, DoubleCod, Obsidifish, ChaosFish, Ebonkoi, Hemopiranha, CrispyHoneyBlock, LadyBug, WhitePearl, BlackPearl, GrassSeeds, Gel, PinkGel, BottledWater, VilePowder, ViciousPowder, Ale, SturdyFossil, MudBlock, HallowedSeeds, EbonsandBlock, CorruptSeeds, CrimsandBlock, CrimsonSeeds, DarkShard, PearlsandBlock, IllegalGunParts, AntlionMandible, Acorn,
        Shackle, ObsidianSkull, WhoopieCushion, SandstorminaBalloon, BlizzardinaBalloon, NaturesGift, TitanGlove, TrueExcalibur, TrueNightsEdge, Seedler, TheHorsemansBlade, InfluxWaver, Meowmere, StarWrath,
        // </generated>
        // Consumables.
        LesserHealingPotion, HealingPotion, LesserManaPotion, ManaPotion, LifeCrystal, ManaCrystal,
        // Crafting stations set down, and the wood gear made at the first of them (ItemsStations.cs).
        WorkBench, Furnace, IronAnvil, LeadAnvil, MythrilAnvil, OrichalcumAnvil, Sawmill, Loom, TinkerersWorkshop, Chain,
        WoodHelmet, WoodBreastplate, WoodGreaves,
        // The Knight's own (DreamNailItem.cs, NailItem.cs).
        DreamNail, KnightNail,
        // Ores and the metal sets made of their bars (ItemsOres.cs).
        CopperOre, TinOre, LeadOre, SilverOre, TungstenOre, PlatinumOre, DemoniteOre, Meteorite,
        CobaltOre, PalladiumOre, MythrilOre, OrichalcumOre, AdamantiteOre, TitaniumOre, ChlorophyteOre,
        TinHelmet, TinChainmail, TinGreaves, LeadHelmet, LeadChainmail, LeadGreaves, SilverHelmet, SilverChainmail, SilverGreaves,
        TungstenHelmet, TungstenChainmail, TungstenGreaves, PlatinumHelmet, PlatinumChainmail, PlatinumGreaves,
        // Money as an item and the bank it is kept in (ItemsMoney.cs).
        Geo, PiggyBank,
        // The materials of the craft chain (ItemsCraftChain.cs), crimtane and luminite ore among the ores (ItemsOres.cs).
        CrimtaneOre, CrimtaneBar, TissueSample, SpellTome, Harp, DjinnLamp, DivingHelmet, FlameWakerBoots, LunarOre,
        // Append only: existing numeric save IDs must never move.
        BossSigilFalseKnight, BossSigilGruzMother, BossSigilMawlek, BossSigilHornet, BossSigilMossCharger,
        BossSigilGorb, BossSigilNoEyes, BossSigilDungDefender, BossSigilTraitorLord, BossSigilMantisLords,
        BossSigilSoulMaster, BossSigilUumuu, BossSigilFlukemarm, BossSigilCrystalGuardian, BossSigilElderHu,
        BossSigilMarmu, BossSigilWatcherKnights, BossSigilHiveKnight, BossSigilCollector, BossSigilNosk,
        BossSigilGalien, BossSigilXero, BossSigilHornetSentinel, BossSigilMarkoth, BossSigilBrokenVessel,
        BossSigilGrimm, BossSigilHollowKnight, BossSigilFailedChampion, BossSigilSoulTyrant, BossSigilLostKin,
        BossSigilWhiteDefender, BossSigilGreyPrince, BossSigilNightmareGrimm, BossSigilRadiance,
        BossSigilPureVessel, BossSigilOroMato, BossSigilSheo, BossSigilSly, BossSigilOblobbles, BossSigilGodTamer,
        // The information accessories, the mirrors and the teleport rods (ItemsInfo.cs).
        CopperWatch, TinWatch, SilverWatch, TungstenWatch, GoldWatch, PlatinumWatch, DepthMeter, Compass, GPS,
        FishermansGuide, WeatherRadio, Sextant, FishFinder, MetalDetector, LifeformAnalyzer, DPSMeter, Stopwatch, TallyCounter,
        GoblinTech, REK, PDA, CellPhone, MagicMirror, IceMirror, RodofDiscord, RodOfHarmony, MagnetFlower,
        // The rest of the crafting stations and the buff ones (ItemsStations2.cs).
        Hellforge, AdamantiteForge, TitaniumForge, CrystalBall, Bookcase, ImbuingStation, Autohammer, LunarCraftingStation, Solidifier,
        Keg, CookingPot, AlchemyTable, HeavyWorkBench, BoneWelder, GlassKiln, LivingLoom, SkyMill, IceMachine, LihzahrdFurnace,
        SteampunkBoiler, FleshCloningVaat, HoneyDispenser, BlendOMatic, MeatGrinder, AmmoBox, BewitchingTable, SharpeningStation,
        WarTable, SliceOfCake, Book,
        // The rest of Terraria's armor (ItemsArmor2.cs), and the Robe, the Pink Thread and the Pumpkin.
        Goggles, MeteorHelmet, MeteorSuit, MeteorLeggings, EmptyBucket, WizardHat, NinjaHood, NinjaShirt, NinjaPants, MiningShirt, MiningPants,
        EbonwoodHelmet, EbonwoodBreastplate, EbonwoodGreaves, RichMahoganyHelmet, RichMahoganyBreastplate, RichMahoganyGreaves, PearlwoodHelmet,
        PearlwoodBreastplate, PearlwoodGreaves, CrimsonHelmet, CrimsonScalemail, CrimsonGreaves, EskimoHood, EskimoCoat, EskimoPants, GreenCap,
        VikingHelmet, CactusHelmet, CactusBreastplate, CactusLeggings, ShadewoodHelmet, ShadewoodBreastplate, ShadewoodGreaves, AncientIronHelmet,
        AncientGoldHelmet, AncientShadowHelmet, AncientShadowScalemail, AncientShadowGreaves, AncientNecroHelmet, AncientCobaltHelmet,
        AncientCobaltBreastplate, AncientCobaltLeggings, PinkEskimoHood, PinkEskimoCoat, PinkEskimoPants, RainHat, RainCoat, AmethystRobe, TopazRobe,
        SapphireRobe, EmeraldRobe, RubyRobe, DiamondRobe, PumpkinHelmet, PumpkinBreastplate, PumpkinLeggings, MagicHat, Gi, GypsyRobe, AnglerHat,
        AnglerVest, AnglerPants, BorealWoodHelmet, BorealWoodBreastplate, BorealWoodGreaves, PalmWoodHelmet, PalmWoodBreastplate, PalmWoodGreaves,
        NightVisionHelmet, GladiatorHelmet, GladiatorBreastplate, GladiatorLeggings, FossilHelm, FossilShirt, FossilPants, DjinnsCurse,
        ApprenticeHat, ApprenticeRobe, ApprenticeTrousers, SquireGreatHelm, SquirePlating, SquireGreaves, HuntressWig, HuntressJerkin, HuntressPants,
        MonkBrows, MonkShirt, MonkPants, SquireAltHead, SquireAltShirt, SquireAltPants, ApprenticeAltHead, ApprenticeAltShirt, ApprenticeAltPants,
        HuntressAltHead, HuntressAltShirt, HuntressAltPants, MonkAltHead, MonkAltShirt, MonkAltPants, UltrabrightHelmet, AmberRobe, MoonLordLegs,
        DeadMansSweater, AshWoodHelmet, AshWoodBreastplate, AshWoodGreaves, UpgradedMiningHead, UpgradedMiningBody, UpgradedMiningLegs,
        UpgradedFishingHead, UpgradedFishingBody, UpgradedFishingLegs, Robe, PinkThread, Pumpkin,
        // The bosses' hearts (BossPotions).
        Heart,
        // Master Mode relics (Relics.cs).
        RelicAbsoluteRadiance, RelicBigBuzzer, RelicBigFly, RelicBlackKnight, RelicDungDefender, RelicEnragedGuardian,
        RelicFalseKnight, RelicFalseKnightAt5, RelicFinalBoss, RelicFlukeMother, RelicGhostAladar, RelicGhostGalien,
        RelicGhostHu, RelicGhostMarkoth, RelicGhostMarmu, RelicGhostNoEyes, RelicGhostXero, RelicGreyPrince,
        RelicGrimm, RelicHiveKnight, RelicHollowKnight, RelicHollowKnightPrime, RelicHornet, RelicHornetAt4,
        RelicInfectedKnight, RelicInfectedKnightAt5, RelicJarCollector, RelicLobsterLancer, RelicMageKnight, RelicMageLord,
        RelicMageLordAt5, RelicMantisLord, RelicMawlek, RelicMegaBeamMiner, RelicMegaJellyfish, RelicMegaMossCharger,
        RelicMimicSpider, RelicNailBros, RelicNailsage, RelicNightmareGrimm, RelicOblobble, RelicPaintmaster,
        RelicPaleLurker, RelicSistersOfBattle, RelicTraitorLord, RelicWhiteDefender, RelicWingedNosk, RelicZote,
        // Generated wall items BEGIN
        StoneWall, DirtWall, WoodWall, GrayBrickWall, RedBrickWall, BlueBrickWall, GreenBrickWall, PinkBrickWall, GoldBrickWall, SilverBrickWall, CopperBrickWall, ObsidianBrickWall, GlassWall, PearlstoneBrickWall, IridescentBrickWall, MudstoneBrickWall, CobaltBrickWall, MythrilBrickWall, PlankedWall, CandyCaneWall, GreenCandyCaneWall, SnowBrickWall, AdamantiteBeamWall, DemoniteBrickWall, SandstoneBrickWall, EbonstoneBrickWall, RedStuccoWall, YellowStuccoWall, GreenStuccoWall, GrayStuccoWall, EbonwoodWall, RichMahoganyWall, PearlwoodWall, RainbowBrickWall, TinBrickWall, TungstenBrickWall, PlatinumBrickWall, GrassWall, JungleWall, FlowerWall, CactusWall, CloudWall, MushroomWall, BoneBlockWall, SlimeBlockWall, FleshBlockWall, DiscWall, IceBrickWall, ShadewoodWall, LihzahrdBrickWall, HiveWall, PurpleStainedGlass, YellowStainedGlass, BlueStainedGlass, GreenStainedGlass, RedStainedGlass, MulticoloredStainedGlass, BlueSlabWall, BlueTiledWall, PinkSlabWall, PinkTiledWall, GreenSlabWall, GreenTiledWall, WoodenFence, LeadFence, PalladiumColumnWall, BubblegumBlockWall, TitanstoneBlockWall, LivingWoodWall, PumpkinWall, HayWall, SpookyWoodWall, ChristmasTreeWallpaper, OrnamentWallpaper, CandyCaneWallpaper, FestiveWallpaper, StarsWallpaper, SquigglesWallpaper, SnowflakeWallpaper, KrampusHornWallpaper, BluegreenWallpaper, GrinchFingerWallpaper, FancyGreyWallpaper, IceFloeWallpaper, MusicWallpaper, PurpleRainWallpaper, RainbowWallpaper, SparkleStoneWallpaper, StarlitHeavenWallpaper, BubbleWallpaper, CopperPipeWallpaper, DuckyWallpaper, WaterfallWall, LavafallWall, EbonwoodFence, RichMahoganyFence, PearlwoodFence, ShadewoodFence, WhiteDynastyWall, BlueDynastyWall, ArcaneRuneWall, IronFence, CopperPlatingWall, StoneSlabWall, Sail, BorealWoodWall, PalmWoodWall, BorealWoodFence, PalmWoodFence, AmberGemsparkWall, AmberGemsparkWallOff, AmethystGemsparkWall, AmethystGemsparkWallOff, DiamondGemsparkWall, DiamondGemsparkWallOff, EmeraldGemsparkWall, EmeraldGemsparkWallOff, RubyGemsparkWall, RubyGemsparkWallOff, SapphireGemsparkWall, SapphireGemsparkWallOff, TopazGemsparkWall, TopazGemsparkWallOff, TinPlatingWall, ConfettiWall, ConfettiWallBlack, HoneyfallWall, ChlorophyteBrickWall, CrimtaneBrickWall, ShroomitePlatingWall, MartianConduitWall, HellstoneBrickWall, MarbleWall, MarbleBlockWall, GraniteWall, GraniteBlockWall, MeteoriteBrickWall, CrystalBlockWall, SandstoneWall, HardenedSandWall, CorruptHardenedSandWall, CrimsonHardenedSandWall, HallowHardenedSandWall, CorruptSandstoneWall, CrimsonSandstoneWall, HallowSandstoneWall, DesertFossilWall, LunarBrickWall, LivingLeafWall, CogWall, SandFallWall, SnowFallWall, SillyBalloonPinkWall, SillyBalloonPurpleWall, SillyBalloonGreenWall, IronBrickWall, LeadBrickWall, LesionBlockWall, CrimstoneBrickWall, SmoothSandstoneWall, SpiderWall, SolarBrickWall, VortexBrickWall, NebulaBrickWall, StardustBrickWall, OrangeStainedGlass, GoldStarryGlassWall, BlueStarryGlassWall, WroughtIronFence, EbonstoneEcho, MudWallEcho, PearlstoneEcho, SnowWallEcho, AmethystEcho, TopazEcho, SapphireEcho, EmeraldEcho, RubyEcho, DiamondEcho, Cave1Echo, Cave2Echo, Cave3Echo, Cave4Echo, Cave5Echo, Cave6Echo, Cave7Echo, SpiderEcho, CorruptGrassEcho, HallowedGrassEcho, IceEcho, ObsidianBackEcho, CrimsonGrassEcho, CrimstoneEcho, CaveWall1Echo, CaveWall2Echo, Cave8Echo, Corruption1Echo, Corruption2Echo, Corruption3Echo, Corruption4Echo, Crimson1Echo, Crimson2Echo, Crimson3Echo, Crimson4Echo, Dirt1Echo, Dirt2Echo, Dirt3Echo, Dirt4Echo, Hallow1Echo, Hallow2Echo, Hallow3Echo, Hallow4Echo, Jungle1Echo, Jungle2Echo, Jungle3Echo, Jungle4Echo, Lava1Echo, Lava2Echo, Lava3Echo, Lava4Echo, Rocks1Echo, Rocks2Echo, Rocks3Echo, Rocks4Echo, LargeBambooBlockWall, BambooBlockWall, AmberStoneWallEcho, BambooFence, AshWoodWall, AshWoodFence, EchoWall, ReefWall, SpiderWallUnsafe, BlueBrickWallUnsafe, BlueSlabWallUnsafe, BlueTiledWallUnsafe, PinkBrickWallUnsafe, PinkSlabWallUnsafe, PinkTiledWallUnsafe, GreenBrickWallUnsafe, GreenSlabWallUnsafe, GreenTiledWallUnsafe, SandstoneWallUnsafe, HardenedSandWallUnsafe, LihzahrdWallUnsafe, PoopWall, ShimmerWall, ShimmerBrickWall, LunarRustBrickWall, DarkCelestialBrickWall, AstraBrickWall, CosmicEmberBrickWall, CryocoreBrickWall, MercuryBrickWall, StarRoyaleBrickWall, HeavenforgeBrickWall, AncientBlueDungeonBrickWall, AncientGreenDungeonBrickWall, AncientPinkDungeonBrickWall, AncientGoldBrickWall, AncientSilverBrickWall, AncientCopperBrickWall, AncientCobaltBrickWall, AncientMythrilBrickWall, AncientObsidianBrickWall, AncientHellstoneBrickWall, LavaMossBlockWall, ArgonMossBlockWall, KryptonMossBlockWall, XenonMossBlockWall, VioletMossBlockWall, RainbowMossBlockWall, ShimmerFallWall, LivingWoodWallUnsafe, DirtWallUnsafe, FallenStarWall, FeywoodWall, HallowedBrickWall, PineTreeBlockWall, EasterBlockWall, GothicBrickWall, FlinxFurBlockWall, JellyfishBlockWall, PineWoodBlockWall, HarpyBlockWall, MoonplateBlockWall, LibrarianBlockWall, SpikeBlockWall, OfficeBlockWall, ForbiddenBlockWall, WaterBlockWall, BoulderBlockWall,
        // Generated wall items END

        // Audited crafting supplement (append only).
        BonePlatform, RedDye, OrangeDye, YellowDye, LimeDye, GreenDye, TealDye, CyanDye, SkyBlueDye, BlueDye, PurpleDye, VioletDye, PinkDye, BlackDye, RainbowDye, BrickLayer, ExtendoGrip, PaintSprayer, PortableCementMixer, WoodFishingPole, ReinforcedFishingPole, FisherofSouls, HighTestFishingLine, AnglerEarring, TackleBox, Fleshcatcher, BrownDye, ArchitectGizmoPack, AnglerTackleBag, FossilPickaxe, ThunderStaff, LavaMoss, KryptonMoss, XenonMoss, ArgonMoss, HallowJoustingLance, LavaFishingHook, LavaproofTackleBag, HandOfCreation, VioletMoss, RainbowMoss, Flymeal, Stinkbug, FishingBobber, FishingBobberGlowingStar, FishingBobberGlowingLava, FishingBobberGlowingKrypton, FishingBobberGlowingXenon, FishingBobberGlowingArgon, FishingBobberGlowingViolet, FishingBobberGlowingRainbow, HeroicisWingsInactive, ChippysWingsInactive, LightningStrike,

        // Audited crafting supplement (append only).
        DyeVat, Worm, Bass,

        // Fishing (gen_fishing.py)
        BloodFishingRod, FiberglassFishingPole, ScarabFishingRod, MechanicsRod, SittingDucksFishingRod, HotlineFishingHook, GoldenFishingRod, MonarchButterfly, SulphurButterfly, Snail, Scorpion, Grasshopper, ZebraSwallowtailButterfly, GlowingSnail, BlackScorpion, ApprenticeBait, Grubby, HellButterfly, WaterStrider, Firefly, UlyssesButterfly, BlackDragonfly, BlueDragonfly, GreenDragonfly, OrangeDragonfly, RedDragonfly, YellowDragonfly, Maggot, JuliaButterfly, Sluggy, Lavafly, RedAdmiralButterfly, JourneymanBait, PurpleEmperorButterfly, LightningBug, EnchantedNightcrawler, MagmaSnail, Buggy, TreeNymphButterfly, MasterBait, GoldButterfly, GoldGrasshopper, GoldWorm, GoldDragonfly, GoldLadyBug, GoldWaterStrider, BlueJellyfish, GreenJellyfish, PinkJellyfish, Batfish, Catfish, Cloudfish, Cursedfish, Dirtfish, DynamiteFish, EaterofPlankton, FallenStarfish, TheFishofCthulu, Fishotron, Harpyfish, Hungerfish, Ichorfish, Jewelfish, Pengfish, Pixiefish, Spiderfish, TundraTrout, UnicornFish, GuideVoodooFish, Wyverntail, ZombieFish, AmanitaFungifin, Angelfish, BloodyManowar, Bonefish, Bunnyfish, CapnTunabeard, Clownfish, DemonicHellfish, Derpfish, InfectedScabbardfish, Mudfish, Slimefish, TropicalBarracuda, ScarabFish, ScorpioFish, WoodenCrate, WoodenCrateHard, IronCrate, IronCrateHard, GoldenCrate, GoldenCrateHard, DungeonFishingCrate, DungeonFishingCrateHard, OceanCrate, OceanCrateHard, CorruptFishingCrate, CorruptFishingCrateHard, CrimsonFishingCrate, CrimsonFishingCrateHard, HallowedFishingCrate, HallowedFishingCrateHard, JungleFishingCrate, JungleFishingCrateHard, FrozenCrate, FrozenCrateHard, OasisCrate, OasisCrateHard, FloatingIslandFishingCrate, FloatingIslandFishingCrateHard, LavaCrate, LavaCrateHard, Trout, Salmon, AtlanticCod, Tuna, RedSnapper, NeonTetra, PrincessFish, GoldenCarp, Honeyfin, Shrimp, Rockfish, Stinkfish, OldShoe, FishingSeaweed, TinCan, BombFish, Flounder, RockLobster, Oyster, JojaCola, ShuckedOyster, BumblebeeTuna, MirageFish, MutantFlinxfin, Fishron,

        // Hallownest's key items (HkItems.cs)
        HkLumaflyLantern, HkCityCrest, HkSimpleKey, HkElegantKey, HkLoveKey, HkShopkeepersKey, HkTramPass, HkKingsBrand, HkDelicateFlower, HkRancidEgg, HkPaleOre, HkWanderersJournal, HkHallownestSeal, HkKingsIdol, HkArcaneEgg, HkKingsoul, HkVoidHeart,

        // Food (gen_food.py)
        PumpkinPie, CookedFish, CookedShrimp, Sashimi, GrubSoup, Escargot, GoldenDelight, SeafoodDinner, LobsterTail, MonsterLasagna, BlackenedFish,

        // Treasure bags (gen_bags.py)
        BagAbsoluteRadiance, BagBigBuzzer, BagBigFly, BagBlackKnight, BagDungDefender, BagEnragedGuardian, BagFalseKnight, BagFalseKnightAt5, BagFinalBoss, BagFlukeMother, BagGhostAladar, BagGhostGalien, BagGhostHu, BagGhostMarkoth, BagGhostMarmu, BagGhostNoEyes, BagGhostXero, BagGreyPrince, BagGrimm, BagHiveKnight, BagHollowKnight, BagHollowKnightPrime, BagHornet, BagHornetAt4, BagInfectedKnight, BagInfectedKnightAt5, BagJarCollector, BagLobsterLancer, BagMageKnight, BagMageLord, BagMageLordAt5, BagMantisLord, BagMawlek, BagMegaBeamMiner, BagMegaJellyfish, BagMegaMossCharger, BagMimicSpider, BagNailBros, BagNailsage, BagNightmareGrimm, BagOblobble, BagPaintmaster, BagPaleLurker, BagSistersOfBattle, BagTraitorLord, BagWhiteDefender, BagWingedNosk, BagZote,

        // Pets (gen_pets.py)
        ShadowOrb, FairyBell, Carrot, Fish, Seaweed, EatersBone, BoneKey, Nectar, TikiTotem, LizardEgg, ParrotCracker, StrangeGlowingMushroom, Seedling, WispinaBottle, AmberMosquito, EyeSpring, ToySled, SpiderEgg, MagicalPumpkinSeed, UnluckyYarn, CursedSapling, DogWhistle, BabyGrinchMischiefWhistle, ZephyrFish, TartarSauce, MagicLantern, BoneRattle, CrimsonHeart, SuspiciousLookingTentacle, CompanionCube, DD2PetGato, DD2PetGhost, DD2PetDragon, CelestialWand, EucaluptusSap, SharkBait, BambooLeaf, HellCake, BirdieRattle, ExoticEasternChewToy, BedazzledNectar, MudBud, BallOfFuseWire, FullMoonSqueakyToy, OrnateShadowKey, LightningCarrot, KingSlimePetItem, EyeOfCthulhuPetItem, EaterOfWorldsPetItem, BrainOfCthulhuPetItem, SkeletronPetItem, QueenBeePetItem, DestroyerPetItem, TwinsPetItem, SkeletronPrimePetItem, PlanteraPetItem, GolemPetItem, DukeFishronPetItem, LunaticCultistPetItem, MoonLordPetItem, FairyQueenPetItem, PumpkingPetItem, EverscreamPetItem, IceQueenPetItem, MartianPetItem, DD2OgrePetItem, DD2BetsyPetItem, QueenSlimePetItem, BerniePetItem, GlommerPetItem, DeerclopsPetItem, PigPetItem, ChesterPetItem, ResplendentDessert, JunimoPetItem, BlueEgg, SpiffoPlush, GlowTulip, DirtiestBlock, DeadCellsSwarmGrenade, PufferfishPet, AxeFairyPetItem, BoulderPet, RainbowBoulderPet,

        // Append only: vanilla crafting station.
        Campfire,

        // Append only: the tombstones a death leaves (Items/Tombstones.cs).
        Tombstone, GraveMarker, CrossGraveMarker, Headstone, Gravestone, Obelisk,
        RichGravestone1, RichGravestone2, RichGravestone3, RichGravestone4, RichGravestone5,

        // Append only: the Knight's dashes as accessories (Items/AbilityAccessories.cs).
        ShadeCloak, CrystalHeart, HallownestShield,

        // Append only: buff furniture carried, and the Water Candle (Items/CarriedFurniture.cs).
        HeartLantern, StarinaBottle, CatBast, Sunflower, GardenGnome, WaterCandle,

        // Append only: the star a kill leaves for mana (Items/ManaStars.cs).
        Star,

    }

    /// <summary>What an equipped accessory does. The movement ones stand in for the Knight's abilities.</summary>
    internal enum AccessoryEffect
    {
        None,
        /// <summary>Runs up to Terraria's boot speed (accRunSpeed 6).</summary>
        HermesBoots,
        /// <summary>Double jump.</summary>
        CloudInABottle,
        /// <summary>Wall slide and wall jump.</summary>
        ClimbingClaws,
        /// <summary>Dash on a double tap of a direction.</summary>
        ShieldDash,
        /// <summary>Only the numbers in <see cref="ItemDef.Bonus"/>.</summary>
        Passive,
    }

    internal enum DamageClass : byte { None, Melee, Ranged, Magic, Summon }

    /// <summary>
    /// How an item is obtained, the vanilla way (spec-recipes 0.3): blocks and the like bought at
    /// a bench, what a Terraria town NPC sells from that NPC's shop, what Terraria crafts by its
    /// recipe, and what comes from chests or drops as some Hallownest enemy's fixed drop.
    /// </summary>
    internal enum ItemSource : byte { Basic, Shop, Craft, Drop }

    /// <summary>
    /// Which class an item serves: its weapons, armor and accessories, or General (blocks, tools,
    /// potions, ammo, movement and defence gear). Every loadout's crafting column shows them all.
    /// </summary>
    internal enum Kit : byte { General, Melee, Ranged, Magic, Summon }
    internal enum AmmoKind : byte { None, Arrow, Bullet, FallenStar, Rocket, Dart, Sand, Gel, Flare, Snowball, StyngerBolt, CandyCorn, JackOLantern, Stake, Nail, Ale, Acorn }
    internal enum ArmorSlot : byte { None, Head, Body, Legs }
    /// <summary>Armor sets: three pieces of one set give its bonus.</summary>
    /// <summary>
    /// An armor family (ItemDef.Set: all three pieces must share it) or a set bonus a piece picks
    /// (ItemDef.SetBonus: the head's, or the Beetle body's, ArmorSetBonuses' PartType).
    /// </summary>
    internal enum ArmorSet : byte
    {
        None, Copper, Iron, Gold, Shadow, Necro, Jungle, Molten, Bee, Obsidian, Spider,
        // Families.
        Cobalt, Mythril, Adamantite, Palladium, Orichalcum, Titanium, Frost, Forbidden, Hallowed, CrystalAssassin, Chlorophyte, Turtle, Tiki, Spooky,
        Shroomite, Spectre, Beetle, Solar, Vortex, Nebula, Stardust,
        // Bonuses picked by one piece.
        CobaltCaster, CobaltMelee, CobaltRanged, MythrilCaster, MythrilMelee, MythrilRanged, AdamantiteCaster, AdamantiteMelee, AdamantiteRanged,
        HallowedSummoner, ChlorophyteMelee, ChlorophyteSummon, SpectreHealing, SpectreDamage, BeetleDamage, BeetleDefense,
        Wood,
        Tin, Lead, Silver, Tungsten, Platinum,
        // ItemsArmor2.cs: each wood its own set (Set(730, 731, 732)...); the robes count with the Wizard Hat and the Magic Hat.
        Meteor, Ninja, Mining, Ebonwood, RichMahogany, Pearlwood, Shadewood, BorealWood, PalmWood, AshWood, Crimson, Snow, Cactus,
        Wizard, MagicHat, Robe, Pumpkin, Angler, Gladiator, Fossil,
        ApprenticeT2, SquireT2, HuntressT2, MonkT2, ApprenticeT3, SquireT3, HuntressT3, MonkT3,
    }

    /// <summary>The Old One's Army sets' sentry bonuses (Player.setSquireT2 ... setMonkT3; a tier 3 set has its tier 2's too).</summary>
    [System.Flags]
    internal enum OoaSet : byte
    {
        None = 0, SquireT2 = 1, SquireT3 = 2, ApprenticeT2 = 4, ApprenticeT3 = 8, HuntressT2 = 16, HuntressT3 = 32, MonkT2 = 64, MonkT3 = 128,
    }

    /// <summary>What a summon staff calls (Terraria projectile ids noted).</summary>
    internal enum MinionKind : byte
    {
        None,
        BabySlime,      // 266
        Flinx,          // 951
        VampireFrog,    // 758
        Spider,         // 390-392, in turn
        Pirate,         // 393-395, at random
        BabyBird,       // 759
        Hornet,         // 373
        Imp,            // 375
        Abigail,        // 970 + 963
        Twins,          // 387 + 388
        Sanguine,       // 755
        StardustDragon, // 625-628
        DeadlySphere,   // 533 (MinionsHardmode.cs)
        Sharknado,      // 407
        UFO,            // 423
        StardustCell,   // 613
        Raven,          // 317
        Terraprisma,    // 946
        DesertTiger,    // 831 gems, 833-835 the tiger
        BladeStaff,     // 864
        Pygmy,          // 191-194, at random
        // The Old One's Army sentries (MinionsSentries.cs), by tier.
        Flameburst1, Flameburst2, Flameburst3,  // 663 / 665 / 667
        Ballista1, Ballista2, Ballista3,        // 677-679
        Trap1, Trap2, Trap3,                    // 691-693
        Aura1, Aura2, Aura3,                    // 688-690
        // The other sentries (MinionsSentries2.cs).
        QueenSpider,    // 377 (eggs 378, babies 379)
        FrostHydra,     // 308
        Houndius,       // 966
        LunarPortal,    // 641 (laser 642)
        RainbowCrystal, // 643 (blasts 644)
        Barnacle,       // 1025
        // The 1.4.5 summons (MinionsPal.cs).
        MushroomBoi,    // 1022 (burst 1044)
        Cattiva,        // 1093
        TrustyCattiva,  // 1112
        Foxsparks,      // 1094 (fireballs 1097, breath 1106)
        TrustyFoxsparks,// 1113
        ClayPot,        // 1118
        ForbiddenRelic, // 1119 (gems 1120)
    }

    /// <summary>The whips (Terraria projectile ids noted).</summary>
    internal enum WhipKind : byte
    {
        None,
        Leather,     // 841
        Snapthorn,   // 914
        SpinalTap,   // 952
        Firecracker, // 913
        CoolWhip,    // 912
        Durendal,    // 847
        MorningStar, // 848
        DarkHarvest, // 849
        Kaleidoscope, // 915
        SlimeWhip,   // 1104
        CobWhip,     // 1028
        CorruptWhip, // 1029
        CrimsonWhip, // 1030
        MeteorWhip,  // 1031
        FlowerWhip,  // 1032
        EelWhip,     // 1033
        ConstellationWhip, // 1034
        MoonLordWhip, // 1035
    }

    /// <summary>Projectiles items shoot or throw (Terraria projectile types noted).</summary>
    internal enum ProjKind : ushort
    {
        None,
        Bomb,          // 28
        Dynamite,      // 29
        Grenade,       // 30
        WoodenArrow,   // 1
        FlamingArrow,  // 2
        UnholyArrow,   // 4
        JesterArrow,   // 5
        Bullet,        // 14
        MeteorShot,    // 36
        AmethystBolt,  // 121
        RubyBolt,      // 125
        DiamondBolt,   // 126
        TopazBolt, SapphireBolt, EmeraldBolt, AmberBolt, // 122 123 124 597
        Spark,         // 954
        WaterBolt,     // 27
        Laser,         // 20
        DemonScythe,   // 45
        BallofFire,    // 15
        TerraBeam,     // 132
        Boomerang,     // 6
        HellfireArrow,      // 41
        HolyArrow,          // 91
        HallowStar,         // 92
        CursedArrow,        // 103
        FrostburnArrow,     // 172
        ChlorophyteArrow,   // 225
        IchorArrow,         // 278
        VenomArrow,         // 282
        BoneArrow,          // 474
        LuminiteArrow,      // 639
        LuminiteTrail,      // 640
        ShimmerArrow,       // 1006
        SilverBullet,       // 981
        CrystalBullet,      // 89
        CrystalShard,       // 90
        CursedBullet,       // 104
        ChlorophyteBullet,  // 207
        HighVelocityBullet, // 242
        IchorBullet,        // 279
        VenomBullet,        // 283
        PartyBullet,        // 284
        NanoBullet,         // 285
        ExplodingBullet,    // 286
        GoldenBullet,       // 287
        LuminiteBullet,     // 638
        StarfuryStar,       // 9
        EnchantedBeam,      // 173
        IceBolt,            // 118
        Bee,                // 181
        DarkLance,          // 46
        TridentSpear,       // 47
        WoodenBoomerang,    // 52
        Flamarang,          // 19
        ThornChakram,       // 33
        Shuriken,           // 3
        ThrowingKnife,      // 48
        MagicMissile,       // 16
        Flamelash,          // 34
        VilethornBase,      // 7
        VilethornTip,       // 8
        WaterStream,        // 22
        StarCloakStar,      // 726
        // Minion shots (MinionShot: whip tags add to them).
        HornetStinger,      // 374
        ImpFireball,        // 376
        MiniRetinaLaser,    // 389
        // The hardmode swords' shots (TerraProjectilesSwords.cs).
        FlamingJack,        // 321, the Horseman's Blade's jack-o'-lantern
        SeedlerSeed,        // 483
        SeedlerSeedlet,     // 484
        InfluxWaver,        // 451
        Meowmere,           // 502
        StarWrath,          // 503
        // The hardmode spears and what they leave behind (TerraProjectilesSpears.cs).
        CobaltNaginata,     // 97
        PalladiumPike,      // 212
        MythrilHalberd,     // 64
        OrichalcumHalberd,  // 215
        AdamantiteGlaive,   // 66
        TitaniumTrident,    // 218
        Gungnir,            // 105
        MushroomSpear,      // 130
        ChlorophytePartisan,// 222
        NorthPole,          // 342
        SpearMushroom,      // 131, the Mushroom Spear's glowing mushrooms
        SporeCloud,         // 228, the Chlorophyte Partisan's cloud
        NorthPoleSpear,     // 343, the North Pole's ice spear
        NorthPoleFlake,     // 344, its falling snowflakes
        // The hardmode boomerangs (TerraProjectilesBoomerangs.cs).
        LightDisc,          // 106
        Bananarang,         // 272
        PossessedHatchet,   // 182
        PaladinsHammer,     // 301
        // The hardmode bows' own arrows (TerraProjectilesRanged.cs).
        MarrowArrow,        // 117
        BoneShard,          // 1124, what it breaks into
        FrostArrow,         // 120
        PulseBolt,          // 357
        ShadowflameArrow,   // 495
        // The first hardmode spells (TerraProjectilesMagic.cs).
        CrystalStormShard,  // 94
        CursedFlame,        // 95
        GoldenShowerStream, // 280
        FrostBolt,          // 359
        PoisonFang,         // 265
        VenomFang,          // 355
        UnholyTrident,      // 114
        Meteor,             // 424-426
        SpectreWisp,        // 297
        InfernoBolt,        // 295
        InfernoBlast,       // 296
        Shadowbeam,         // 294 (TerraProjectilesMagic2.cs)
        HeatRay,            // 260
        Bubble,             // 410
        RazorbladeTyphoon,  // 409
        Icicle,             // 337, the Blizzard Staff's
        SkyFractureSword,   // 660
        Leaf,               // 206, the Leaf Blower's
        PurpleLaser,        // 88, the Laser Rifle's (TerraProjectilesMagic3.cs)
        Bat,                // 316, the Bat Scepter's
        CrystalVileShard,   // 494
        CrystalVileTip,     // 493
        MagnetSphere,       // 254
        MagnetBolt,         // 255
        RainbowRod,         // 79
        NimbusCloudMoving,  // 237
        NimbusCloud,        // 238
        NimbusRain,         // 239
        HarpNote1,          // 76 (TerraProjectilesMagic4.cs)
        HarpNote2,          // 77
        HarpNote3,          // 78
        ClingerWall,        // 482
        ToxicFlask,         // 510
        ToxicCloud,         // 511-513
        Skull,              // 837, the Book of Skulls'
        SpiritFlame,        // 659
        NebulaBlaze,        // 634 (TerraProjectilesLunar.cs)
        NebulaBlazeBig,     // 635
        LunarFlare,         // 645
        NebulaArcanum,      // 617
        NebulaArcanumSub,   // 618
        NebulaArcanumShard, // 620
        LastPrism,          // 633 (TerraProjectilesHeld.cs)
        LastPrismBeam,      // 632
        ChargedBlaster,     // 460
        ChargedBlasterOrb,  // 459
        ChargedBlasterBeam, // 461
        MedusaHead,         // 535
        MedusaRay,          // 536
        IceRodBolt,         // 80 (TerraProjectilesIce.cs)
        TigerPounce,        // 818, the Desert Tiger's (TerraProjectilesMinionShots.cs)
        PygmySpear,         // 195, the pygmies'
        FlameburstShot1,    // 664 (TerraProjectilesSentryShots.cs)
        FlameburstShot2,    // 666
        FlameburstShot3,    // 668
        BallistaBolt,       // 680
        FrostBlast,         // 309
        HoundiusFireball,   // 967
        BarnacleShot,       // 1026
        WhipMeteor,         // 1037, Starfall's (TerraProjectilesWhip2.cs)
        FlowerPetal,        // 1038
        ConstellationStar,  // 1039
        PhantomSlash,       // 1045, the Moon Lord whip's
        ScytheLightning,    // 916, Dark Harvest's (TerraProjectilesWhip.cs)
        SharknadoShark,     // 408 (TerraProjectilesMinionShots.cs)
        UfoLaser,           // 433
        StardustCellShot,   // 614
        StarVeilStar,       // 725, the accessories' (TerraProjectilesAcc.cs)
        BeeCloakStar,       // 724
        ManaCloakStar,      // 723
        SnakeCloakStar,     // 1133
        GiantBee,           // 566
        BrainIllusion,      // 565
        Spore,              // 567/568
        SacCloud,           // 569-571
        GelBall,            // 937
        BoneGloveBone,      // 532
        ShadowHand,         // 964
        ManaCloakPickup,    // item 4143
        // The armor sets' (accarmor-hm sec-H2).
        OrichalcumPetal,    // 221
        CrystalLeaf,        // 226
        CrystalLeafShot,    // 227
        TitaniumShard,      // 908
        SpiritHeal,         // 298
        SpectreWrath,       // 356
        SandnadoFriendly,   // 656
        StardustGuardian,   // 623
        NebulaBooster,      // items 3453-3455
        // The yoyos (aiStyle 99) and theirs.
        WoodenYoyo, RallyYoyo, MalaiseYoyo, ArteryYoyo, AmazonYoyo, Code1Yoyo, HiveFiveYoyo, ValorYoyo, CascadeYoyo, ChikYoyo, AmarokYoyo, HelFireYoyo,
        GradientYoyo, FormatCYoyo, Code2Yoyo, YeletsYoyo, RedsThrowYoyo, ValkyrieYoyo, EyeYoyo, KrakenYoyo, TerrarianYoyo,
        CounterweightBlack, CounterweightBlue, CounterweightGreen, CounterweightPurple, CounterweightRed, CounterweightYellow, CounterweightStrung,
        CascadeBurst,       // 1125
        ChikCrystal,        // 1134
        KrakenWave,         // 1135
        EyeLunge,           // 1130
        TerrarianOrb,       // 604
        // The flails (aiStyle 15) and theirs.
        MaceFlail, FlamingMaceFlail, BallOHurtFlail, MeatballFlail, BlueMoonFlail, SunfuryFlail, DaoOfPowFlail, DripplerFlail, FlowerPowFlail, FlaironFlail,
        FlowerPowPetal,     // 248
        DripplerBall,       // 928
        FlaironBubble,      // 405
        // The plain swords' own (sweep S1.A).
        BoneSwordBone,      // 1111
        IchorSplash,        // 524
        TentacleSpikeStuck, // 971
        BloodButchererStuck,// 975
        // The 1.4.5 thrown phaseblades (1059-1064, 1071, 1075, 1101), phasesabers (1065-1070, 1072, 1076, 1102) and the Keybrand (1074).
        PhaseBlue, PhaseRed, PhaseGreen, PhasePurple, PhaseWhite, PhaseYellow, PhaseOrange, PhasePink, PhaseRainbow,
        SaberBlue, SaberRed, SaberGreen, SaberPurple, SaberWhite, SaberYellow, SaberOrange, SaberPink, SaberRainbow,
        KeybrandThrown,
        MandibleBlade,      // 1082
        StylishScissors,    // 1083
        Waffle,             // 1012
        ChristmasOrnament,  // 907
        Ornament,           // 335
        // The swords that shoot (sweep S1.C).
        FrostbrandBolt,     // 119
        BeamSwordBeam,      // 116
        IceSickleShot,      // 263
        DeathSickleShot,    // 274
        SonicBoom,          // 684
        ClaymoreSpore,      // 1128
        ClaymoreSlam,       // 1129
        // The held blades (sweep S1.D).
        ArkhalisSlash,      // 595
        TerragrimSlash,     // 735
        PiercingStarlight,  // 927
        // The plain spears (sweep S1.E).
        SpearThrust,        // 49
        RottedForkThrust,   // 153
        SwordfishThrust,    // 368
        ObsidianSwordfishThrust, // 367
        SlimeSpearThrust,   // 1103
        // The thrown spears (sweep S1.E).
        ScourgeJavelin,     // 306
        TinyEater,          // 307
        Daybreak,           // 636
        DaybreakBlast,      // 953
        // The monk staffs (sweep S1.E).
        MonkStaffT1Spin,    // 697
        MonkStaffT1Slam,    // 698
        MonkStaffT2,        // 699
        MonkStaffT2Ghast,   // 700
        MonkStaffT3Spin,    // 707
        MonkStaffT3Thrust,  // 708
        SkyDragonBolt,      // 709
        SkyDragonField,     // 1110
        // The boomerangs and knives (sweep S1.F).
        IceBoomerangProj,   // 113
        FruitcakeChakramProj, // 333
        BloodyMacheteProj,  // 320
        ShroomerangProj,    // 867
        TrimarangProj,      // 1000
        AxearangProj,       // 1052
        CombatWrenchProj,   // 902
        BouncingShieldProj, // 866
        VampireKnife,       // 304
        VampireHeal,        // 305
        ShadowFlameKnifeProj, // 497
        FlyingKnifeProj,    // 491
        FlintHold,          // 1040
        FlintSpike,         // 1041
        // The chained weapons (sweep S1.G).
        GolemFistProj,      // 262
        BoxingGlove,        // 271
        ChainKnifeProj,     // 273
        ChainGuillotine,    // 481
        AnchorProj,         // 383
        SolarEruptionProj,  // 611
        SolarEruptionBlast, // 612
        // The drills and chainsaws (sweep R2).
        CobaltChainsawProj, MythrilChainsawProj, CobaltDrillProj, MythrilDrillProj, AdamantiteChainsawProj, AdamantiteDrillProj, DraxProj, // 57-62, 107
        PalladiumDrillProj, PalladiumChainsawProj, OrichalcumDrillProj, OrichalcumChainsawProj, TitaniumDrillProj, TitaniumChainsawProj, // 213-220
        ChlorophyteDrillProj, ChlorophyteChainsawProj, ChlorophyteJackhammerProj, SawtoothSharkProj, // 223, 224, 252, 369
        VortexDrillProj, NebulaDrillProj, SolarFlareDrillProj, StardustDrillProj, ButchersChainsawProj, LaserDrillProj, ButcherSpark, // 428, 430, 432, 609, 509, 445, 504
        // The thrown consumables (sweep R3).
        PoisonedKnifeProj, HolyWaterProj, UnholyWaterProj, BloodWaterProj, RottenEggProj, StarAniseProj, BoneDaggerProj, FrostDaggerfishProj, MagicDaggerProj, // 54 69 70 621 318 330 599 520 93
        JavelinProj, BoneJavelinProj, SpikyBallProj, BeenadeProj, MolotovProj, MolotovFire1, MolotovFire2, MolotovFire3, MudBallProj, // 507 598 24 183 399 400-402 1081
        StickyGrenadeProj, BouncyGrenadeProj, PartyGrenadeProj, // 397 517 588
        // The sweep R4a guns' and bows' shots.
        OnyxBlast, XenoBubble, BetsyArrow, BeeArrow, HellwingBat, ToxicBubble, PewMaticNote, StarCannonStar, SuperStar, HarpoonProj, // 661 444 710 469 485 523 968 955 728 23
        VortexBeaterHeld, VortexRocket, PhantasmHeld, PhantasmArrow, PhoenixBowHeld, Phoenix, // 615 616 630 631 705 706
        EventideArrow, PiranhaProj, // 932 190
        // The rocket launchers' shots: grenades 133 136 139 142 777 781 794 797, rockets 134 137 140 143 776 780 793 796,
        // mines 135 138 141 144 778 782 795 798, snowmen 338-341 803 804 808 809, fragments 779 783 862 863.
        LauncherGrenade1, LauncherGrenade2, LauncherGrenade3, LauncherGrenade4, LauncherGrenadeCluster1, LauncherGrenadeCluster2, LauncherGrenadeNuke1, LauncherGrenadeNuke2,
        Rocket1, Rocket2, Rocket3, Rocket4, RocketCluster1, RocketCluster2, RocketNuke1, RocketNuke2,
        Mine1, Mine2, Mine3, Mine4, MineCluster1, MineCluster2, MineNuke1, MineNuke2,
        SnowmanRocket1, SnowmanRocket2, SnowmanRocket3, SnowmanRocket4, SnowmanCluster1, SnowmanCluster2, SnowmanNuke1, SnowmanNuke2,
        ClusterFragment1, ClusterFragment2, SnowmanFragment1, SnowmanFragment2,
        Firework1, Firework2, Firework3, Firework4, Celeb2Held, Celeb2Rocket1, Celeb2Rocket2, Celeb2Rocket3, Celeb2Rocket4, ElectrosphereMissile, Electrosphere, // 167-170 714 715-718 442 443
        SeedProj, PoisonDartProj, CrystalDartProj, CursedDartProj, IchorDartProj, CursedDartFlame, SandBall, EbonsandBall, PearlsandBall, CrimsandBall, // 51 267 477-480 42 65 68 354
        FlamethrowerFlame, FlareProj, BlueFlareProj, SpelunkerFlareProj, CursedFlareProj, // 85 163 310 1008 1009
        SnowballProj, StyngerBoltProj, StyngerShrapnel, CandyCornProj, JackOLanternProj, StakeProj, NailProj, AleMug, AcornProj, // 166 246 249 311 312 323 514 669 1088
        CrimsonCloudMoving, CrimsonCloud, BloodRain, FrostBall, Wasp, NettleStem, NettleStem2, NettleTip, // 243 244 245 253 189 150 151 152
        RainbowHead, RainbowPiece, EarthBoulder, PineNeedle, // 250 251 261 336
        CrystalCharge, CrystalChargeShard, SoulDrainBox, ZapinatorLaser, BetsysWrathBolt, SharpTear, // 521 522 476 876 711 756
        ShadowflameTentacle, BookStaffShot, BookStaffStorm, LaserMachinegunHeld, MachinegunLaser, StellarTuneStar, NightglowStar, // 496 712 704 439 440 856 931
        WeatherPainCloud, FrostSpark, PrettyMirrorProj, DaybloomMote, GlacierFangShot, PetalstormPetal, // 969 979 1085 1114 1115 1116
        MushroomBlast, FoxFireball, FoxFlame, ForbiddenGem, // 1044 1097 1106 1120
        Paintball, DeadCellsBarrel, KillingDeckCard, RainbowFlareProj, ShimmerFlareProj, // 587 1023 1024 1010 1011
        RocketWet, RocketLava, RocketHoney, RocketDry, LauncherGrenadeWet, LauncherGrenadeLava, LauncherGrenadeHoney, LauncherGrenadeDry, // 784 787 790 799 785 788 791 800
        MineWet, MineLava, MineHoney, MineDry, SnowmanWet, SnowmanLava, SnowmanHoney, SnowmanDry, // 786 789 792 801 805 806 807 810
        SpilledLiquid, FlaironHead, // (a pool where a liquid rocket burst) 404
        FallingStar, // 12 (the night sky's)
        HallowLance, ThunderBolt, LightningStrikeBolt,
    }

    /// <summary>
    /// What equipment adds to the player. Percentages are fractions (0.1 = 10%), as Terraria
    /// keeps them.
    /// </summary>
    internal struct Bonuses
    {
        public int Defense, LifeRegen, ManaMax;
        public float MoveSpeed, MeleeSpeed, MeleeDamage, RangedDamage, MagicDamage, ArrowDamage;
        public float MeleeCrit, RangedCrit, MagicCrit, AllCrit;
        public float ManaCost;     // reduction
        public float AmmoSave;     // chance not to use ammo
        public bool NoFallDamage, NoKnockback, LongImmunity, JumpBoost, RocketBoots, Swim, Light, ManaFlower, FastArrows, FlyingCarpet;
        /// <summary>Player.autoReuseGlove (Feral Claws): melee weapons swing again while the button is held.</summary>
        public bool AutoReuseGlove;
        /// <summary>Player.gravControl2 (the Gravity Globe): up turns gravity over.</summary>
        public bool GravityControl;
        /// <summary>The potions' (Player.UpdateBuffs): lavaImmune, slowFall, kbBuff, manaRegenBuff, lifeForce, inferno, archery, resistCold,
        /// findTreasure, detectCreature, dangerSense, calmed, enemySpawns, invis, Shine's light, nightVision, lifeMagnet, ammoPotion.</summary>
        public bool LavaImmune, SlowFall, KbBuff, ManaRegenBuff, LifeForce, Inferno, Archery, ResistCold, FindTreasure, DetectCreature, DangerSense,
            Calm, Battle, Invisible, Shine, NightVision, LifeMagnet, AmmoPotion;
        /// <summary>thorns (share of a contact hit sent back), luck, tileSpeed (placing faster), blockRange (reach), meleeEnchant (the flask, 1-8).</summary>
        public float Thorns, Luck, TileSpeed, WallSpeed, BlockSpeed;
        public int FishingSkill, BobberKind;
        public bool FishingLine, TackleBox, AcidFishing;
        public int BlockRange, MeleeEnchant;
        /// <summary>The boots' top speed (accRunSpeed, the strongest boots'); 0 = none.</summary>
        public float AccRunSpeed;
        /// <summary>rocketBoots: 1 Rocket Boots, 2 Spectre and Lightning Boots (their sound and smoke).</summary>
        public int RocketKind;
        /// <summary>Frog Leg: jumpSpeedBoost, auto-jump, extraFall (tiles).</summary>
        public float JumpSpeedBoost;
        public bool AutoJump;
        public int ExtraFall;
        /// <summary>The extra jumps (hasJumpOption_*).</summary>
        public bool JumpCloud, JumpSandstorm, JumpBlizzard, JumpFart, JumpSail;
        /// <summary>dashType, assigned (the last slot wins): 1 the ninja dash (Tabi, Master Ninja Gear), 2 the Shield of Cthulhu's.</summary>
        public int DashKind;
        /// <summary>
        /// blackBelt (the 1/10 ninja dodge), the Portable Stool, Magiluminescence; inert in
        /// Hallownest: canFloatInWater (no Terraria water) and shimmerImmune (no shimmer).
        /// </summary>
        public bool BlackBelt, PortableStool, Magiluminescence, FloatInLiquid, ShimmerImmune;
        /// <summary>
        /// The yoyo accessories: yoyoString (range and life), yoyoGlove (a second yoyo), magicString
        /// (loose copies); counterWeight (1-6 the coloured ones, 7 the Strung, 8 the bags' random);
        /// stringColor (the last worn: 1-12 paint, 13 black, 14 white, 27 rainbow, 28 brown, 29 magic, 30 strung).
        /// </summary>
        public bool YoyoString, YoyoGlove, MagicString;
        public int CounterWeight, StringColor;
        /// <summary>Summoning: minionDamage and minionKB bonuses, more minion slots (maxMinions starts at 1).</summary>
        public float MinionDamage, MinionKB;
        public int MaxMinions;
        /// <summary>maxTurrets bonus (sentries; one to start with).</summary>
        public int MaxTurrets;
        /// <summary>whipRangeMultiplier bonus, and the whips' use-time cut (Obsidian set: ×1/1.15 as 0.15).</summary>
        public float WhipRange, WhipFaster;
        /// <summary>spikedBoots: the Climbing Claws and Shoe Spikes each add one; two cling to walls.</summary>
        public int SpikedBoots;
        /// <summary>Wings: their wingSlot (Wings_N; TerraWings has their numbers); the last pair worn counts.</summary>
        public int WingSlot;
        /// <summary>The Soaring Insignia (empressBrooch): endless flight and rocket time, +1.8 jump speed, ×1.75 acceleration.</summary>
        public bool EmpressBrooch;
        /// <summary>The boots' run trail (UpdateBootVisualEffects: the last pair worn decides); the Fairy Boots' rocket dust.</summary>
        public RunDustKind RunDust;
        public bool FairyDust;
        /// <summary>desertBoots (×1.75 run on sand), flowerBoots (flowers on grass: none here), the Hellfire Treads' flame trail.</summary>
        public bool DesertBoots, FlowerBoots, FlameTrail;
        /// <summary>waterWalk2 (1: water and honey) / waterWalk (2: lava too): Hallownest's acid is walked on (Player.AcidLiquid); it has no lava.</summary>
        public int WaterWalk;
        public bool MagmaStone, KbGlove, MeleeScaleGlove, StarCloak, HoneyComb, Panic, MagicCuffs, PStone;
        public int ArmorPen;
        /// <summary>Player.endurance: the share of damage taken away.</summary>
        public float Endurance;
        /// <summary>Player.aggro (Flesh Knuckles +400, Putrid Scent −400...): inert in Hallownest, whose enemies don't pick targets by it.</summary>
        public int Aggro;
        /// <summary>arrowDamage, bulletDamage, rocketDamage ×(1 + x) (the Shroomite heads' ×1.12), multiplying together.</summary>
        public float ArrowMult, BulletMult, SpecialistMult;
        /// <summary>manaRegenBonus (the Mana Regeneration Band's 60), in UpdateManaRegen.</summary>
        public int ManaRegenBonus;
        /// <summary>
        /// Effects set by items, several of them shared and counted once (UpdateEquips' post-pass,
        /// Pl:13053-13106): the scopes' zoom, the mana-star magnet, the Mana Cloak, the quivers'
        /// fire, the Harpy Charm, the DD2 accessories, the sentry slings, the whip-tag bracelets,
        /// the snapping stones, the Enchanted Timer, the barbs, the Catalyst Band, the sash's dodge,
        /// the Chaos Cylinder's 8%, the Bone Helm.
        /// </summary>
        public bool Scope, ManaMagnet, ManaCloak, MoltenQuiver, PhoenixQuiver, HarpyCharm, SeraphNecklace, DD2Accessory, SentryBackpack, GolemSentryBackpack,
            SilverBracer, MobiusStrip, WickedArmlet, SnakeBand, SnappingStone, Pyroclast, ArmletOfRuin, TimerCrit, SharpBarb, PoisonBarb, HoneyedBarb, CatalystBand,
            MysticSashDodge, AmmoCyclerDamage, BoneHelm;
        /// <summary>The Chaos Cylinder's ammo cycling mode (the last one worn wins).</summary>
        public AmmoCycleMode AmmoCycling;
        /// <summary>buffImmune: the player debuffs the gear keeps off (inert while nothing puts them on the Knight).</summary>
        public DebuffImmunity DebuffImmune;
        /// <summary>fireWalk (hot blocks), lavaRose (lava hurts less), lavaMax (ticks of lava immunity, summed): Hallownest has no hot blocks or lava.</summary>
        public bool FireWalk, LavaRose;
        public int LavaImmuneTicks;
        /// <summary>The Frozen Turtle Shell's ice barrier (buff 62): −25% damage at half life or less.</summary>
        public bool IceBarrier;
        /// <summary>wolfAcc (the werewolf at night, buff 28) and accMerman (the merfolk in liquid, buff 34).</summary>
        public bool Werewolf, Merfolk;
        /// <summary>skyStoneEffects, by when the stone works (applied once).</summary>
        public SkyStoneTime SkyStone;
        /// <summary>pickSpeed: the share taken off a pickaxe's use time (Ancient Chisel −0.25, sky stones −0.15).</summary>
        public float PickSpeed;
        /// <summary>
        /// The Shiny Stone, the Brain of Confusion, the Spore Sac, the Volatile Gelatin, the Bone
        /// Glove, the Hive Pack's strong bees, the Stress Ball, the Ram Rune's down-dash; the money
        /// accessories (Lucky Coin, Gold Ring, Discount Card, Treasure Magnet).
        /// </summary>
        public bool ShinyStone, BrainOfConfusion, SporeSac, VolatileGelatin, BoneGlove, StrongBees, StressBall, DownDash, LuckyCoin, GoldRing, Discount, TreasureMagnet;
        /// <summary>Inert in Hallownest: the Paladin's absorption (teammates), the Royal Gel (slimes), the diving helm (breath), ice skates, echo sight.</summary>
        public bool Paladin, RoyalGel, DivingHelm, IceSkate, SeeInvisible;
        /// <summary>Which star the Star Cloak family drops (starCloakItem_*OverrideItem: the strongest override wins).</summary>
        public CloakStar StarKind;
        /// <summary>The jellyfish necklaces' faint light.</summary>
        public AccLightKind AccLight;
        /// <summary>maxTagEffects and tagEffectDuration above their base 1 (set once in Equipment's post-pass).</summary>
        public int MaxTagEffects;
        public float TagDuration;
        /// <summary>
        /// The sets' own effects (ArmorSetBonuses.Benefits): the Meteor set's free Space Gun (spaceGun), the Crimson set's
        /// regeneration (crimsonRegen), the Cactus set's thorns (cactusThorns), the Ash Wood set's lava (ashWoodBonus), the
        /// Angler set's fewer enemies (anglerSetSpawnReduction); the Dead Man's Sweater (deadMansSweater: traps hit and
        /// debuff for half).
        /// </summary>
        public bool SpaceGunFree, CrimsonRegen, CactusThorns, AshWood, AnglerSet, DeadMansSweater;
        /// <summary>The Old One's Army sets' sentry bonuses.</summary>
        public OoaSet Ooa;

        public void Add(in Bonuses b)
        {
            BlockSpeed = System.Math.Max(BlockSpeed, b.BlockSpeed);
            WallSpeed = System.Math.Max(WallSpeed, b.WallSpeed);
            FishingSkill += b.FishingSkill;
            if (b.BobberKind > 0) BobberKind = b.BobberKind;
            FishingLine |= b.FishingLine; TackleBox |= b.TackleBox; AcidFishing |= b.AcidFishing;
            Defense += b.Defense; LifeRegen += b.LifeRegen; ManaMax += b.ManaMax;
            MoveSpeed += b.MoveSpeed; MeleeSpeed += b.MeleeSpeed;
            MeleeDamage += b.MeleeDamage; RangedDamage += b.RangedDamage; MagicDamage += b.MagicDamage; ArrowDamage += b.ArrowDamage;
            MeleeCrit += b.MeleeCrit; RangedCrit += b.RangedCrit; MagicCrit += b.MagicCrit; AllCrit += b.AllCrit;
            ManaCost += b.ManaCost;
            AmmoSave = 1f - (1f - AmmoSave) * (1f - b.AmmoSave);
            NoFallDamage |= b.NoFallDamage; NoKnockback |= b.NoKnockback; LongImmunity |= b.LongImmunity; GravityControl |= b.GravityControl;
            JumpBoost |= b.JumpBoost; RocketBoots |= b.RocketBoots; Swim |= b.Swim; Light |= b.Light; FlyingCarpet |= b.FlyingCarpet;
            ManaFlower |= b.ManaFlower; FastArrows |= b.FastArrows; AutoReuseGlove |= b.AutoReuseGlove;
            AccRunSpeed = System.Math.Max(AccRunSpeed, b.AccRunSpeed); RocketKind = System.Math.Max(RocketKind, b.RocketKind);
            JumpSpeedBoost += b.JumpSpeedBoost; AutoJump |= b.AutoJump; ExtraFall += b.ExtraFall;
            JumpCloud |= b.JumpCloud; JumpSandstorm |= b.JumpSandstorm; JumpBlizzard |= b.JumpBlizzard; JumpFart |= b.JumpFart; JumpSail |= b.JumpSail;
            if (b.DashKind != 0) DashKind = b.DashKind;
            BlackBelt |= b.BlackBelt; PortableStool |= b.PortableStool; Magiluminescence |= b.Magiluminescence;
            YoyoString |= b.YoyoString; YoyoGlove |= b.YoyoGlove; MagicString |= b.MagicString;
            if (b.CounterWeight != 0) CounterWeight = b.CounterWeight;
            if (b.StringColor != 0) StringColor = b.StringColor;
            FloatInLiquid |= b.FloatInLiquid; ShimmerImmune |= b.ShimmerImmune;
            SpikedBoots += b.SpikedBoots;
            if (b.WingSlot > 0) WingSlot = b.WingSlot;
            EmpressBrooch |= b.EmpressBrooch;
            if (b.RunDust != RunDustKind.Hermes) RunDust = b.RunDust;
            FairyDust |= b.FairyDust; DesertBoots |= b.DesertBoots; FlowerBoots |= b.FlowerBoots; FlameTrail |= b.FlameTrail;
            WaterWalk = System.Math.Max(WaterWalk, b.WaterWalk);
            MagmaStone |= b.MagmaStone; KbGlove |= b.KbGlove; MeleeScaleGlove |= b.MeleeScaleGlove; StarCloak |= b.StarCloak; HoneyComb |= b.HoneyComb;
            Panic |= b.Panic; MagicCuffs |= b.MagicCuffs; PStone |= b.PStone;
            ArmorPen += b.ArmorPen; Endurance += b.Endurance;
            MinionDamage += b.MinionDamage; MinionKB += b.MinionKB; MaxMinions += b.MaxMinions; MaxTurrets += b.MaxTurrets; WhipRange += b.WhipRange; WhipFaster += b.WhipFaster;
            Aggro += b.Aggro; ManaRegenBonus += b.ManaRegenBonus;
            ArrowMult = (1f + ArrowMult) * (1f + b.ArrowMult) - 1f;
            BulletMult = (1f + BulletMult) * (1f + b.BulletMult) - 1f;
            SpecialistMult = (1f + SpecialistMult) * (1f + b.SpecialistMult) - 1f;
            Scope |= b.Scope; ManaMagnet |= b.ManaMagnet; ManaCloak |= b.ManaCloak; MoltenQuiver |= b.MoltenQuiver; PhoenixQuiver |= b.PhoenixQuiver;
            HarpyCharm |= b.HarpyCharm; SeraphNecklace |= b.SeraphNecklace; DD2Accessory |= b.DD2Accessory; SentryBackpack |= b.SentryBackpack;
            GolemSentryBackpack |= b.GolemSentryBackpack; SilverBracer |= b.SilverBracer; MobiusStrip |= b.MobiusStrip; WickedArmlet |= b.WickedArmlet;
            SnakeBand |= b.SnakeBand; SnappingStone |= b.SnappingStone; Pyroclast |= b.Pyroclast; ArmletOfRuin |= b.ArmletOfRuin; TimerCrit |= b.TimerCrit;
            SharpBarb |= b.SharpBarb; PoisonBarb |= b.PoisonBarb; HoneyedBarb |= b.HoneyedBarb; CatalystBand |= b.CatalystBand;
            MysticSashDodge |= b.MysticSashDodge; AmmoCyclerDamage |= b.AmmoCyclerDamage; BoneHelm |= b.BoneHelm;
            if (b.AmmoCycling != AmmoCycleMode.Off) AmmoCycling = b.AmmoCycling;
            DebuffImmune |= b.DebuffImmune; FireWalk |= b.FireWalk; LavaRose |= b.LavaRose; LavaImmuneTicks += b.LavaImmuneTicks; IceBarrier |= b.IceBarrier;
            Werewolf |= b.Werewolf; Merfolk |= b.Merfolk; SkyStone |= b.SkyStone; PickSpeed += b.PickSpeed;
            ShinyStone |= b.ShinyStone; BrainOfConfusion |= b.BrainOfConfusion; SporeSac |= b.SporeSac; VolatileGelatin |= b.VolatileGelatin; BoneGlove |= b.BoneGlove;
            StrongBees |= b.StrongBees; StressBall |= b.StressBall; DownDash |= b.DownDash;
            LuckyCoin |= b.LuckyCoin; GoldRing |= b.GoldRing; Discount |= b.Discount; TreasureMagnet |= b.TreasureMagnet;
            Paladin |= b.Paladin; RoyalGel |= b.RoyalGel; DivingHelm |= b.DivingHelm; IceSkate |= b.IceSkate; SeeInvisible |= b.SeeInvisible;
            if (b.StarKind > StarKind) StarKind = b.StarKind;
            if (b.AccLight > AccLight) AccLight = b.AccLight;
            MaxTagEffects += b.MaxTagEffects; TagDuration += b.TagDuration;
            LavaImmune |= b.LavaImmune; SlowFall |= b.SlowFall; KbBuff |= b.KbBuff; ManaRegenBuff |= b.ManaRegenBuff; LifeForce |= b.LifeForce; Inferno |= b.Inferno;
            Archery |= b.Archery; ResistCold |= b.ResistCold; FindTreasure |= b.FindTreasure; DetectCreature |= b.DetectCreature; DangerSense |= b.DangerSense;
            Calm |= b.Calm; Battle |= b.Battle; Invisible |= b.Invisible; Shine |= b.Shine; NightVision |= b.NightVision; LifeMagnet |= b.LifeMagnet; AmmoPotion |= b.AmmoPotion;
            Thorns = System.Math.Max(Thorns, b.Thorns); Luck += b.Luck; TileSpeed += b.TileSpeed; BlockRange += b.BlockRange;
            if (b.MeleeEnchant != 0) MeleeEnchant = b.MeleeEnchant;
            SpaceGunFree |= b.SpaceGunFree; CrimsonRegen |= b.CrimsonRegen; CactusThorns |= b.CactusThorns; AshWood |= b.AshWood;
            AnglerSet |= b.AnglerSet; DeadMansSweater |= b.DeadMansSweater; Ooa |= b.Ooa;
        }
    }

    /// <summary>The permanent boosters (Player: ConsumedLifeFruit, extraAccessory, usedAegisCrystal...).</summary>
    internal enum PermanentBoost : byte { None, LifeFruit, DemonHeart, AegisCrystal, AegisFruit, ArcaneCrystal, GalaxyPearl, GummyWorm, Ambrosia, ArtisanLoaf }

    /// <summary>SpawnFastRunParticles' trail (Pl:20349-20436): Hermes's dust 16, the sailfish's spray, desert sand, frost, fairy dust, fire.</summary>
    internal enum RunDustKind : byte { Hermes, Sail, Desert, Cold, Fairy, Hellfire }

    /// <summary>The player debuffs Item.buffImmune can block (Poisoned 20, Darkness 22, Cursed 23, Bleeding 30, Confused 31, Slow 32, Weak 33, Silenced 35, Broken Armor 36, Chilled 46, Frozen 47, Stoned 156).</summary>
    [System.Flags]
    internal enum DebuffImmunity : uint
    {
        None = 0, Poisoned = 1, Darkness = 2, Cursed = 4, Bleeding = 8, Confused = 16, Slow = 32, Weak = 64, Silenced = 128, BrokenArmor = 256, Chilled = 512, Frozen = 1024, Stoned = 2048, OnFire = 4096,
    }

    /// <summary>When the sky stone works: the Sun Stone by day, the Moon Stone at night, the Celestial Stone always.</summary>
    [System.Flags]
    internal enum SkyStoneTime : byte { None = 0, Day = 1, Night = 2, Always = 3 }

    /// <summary>The Star Cloak family's star, in the order the overrides are applied (the last wins): 726, 725, 724, 723, 1133.</summary>
    internal enum CloakStar : byte { Star, Veil, Bee, Mana, Snake }

    /// <summary>The Jellyfish Necklace's pink light and the Arctic Diving Gear's blue one.</summary>
    internal enum AccLightKind : byte { None, Jellyfish, Arctic }

    /// <summary>Player.ammoCyclingMode (the Chaos Cylinders): slots 50-57 in turn, the whole inventory, at random, or plain.</summary>
    internal enum AmmoCycleMode : byte { Off, AmmoSlots, FullInventory, Random, Simple }

    /// <summary>Item stats, taken from Terraria's own Item.SetDefaults values (times in ticks of 1/60 s).</summary>
    internal sealed class ItemDef
    {
        public ItemType Type;
        public string Name;
        /// <summary>Terraria texture name, e.g. Item_3509.</summary>
        public string Texture;
        public int Damage;
        public float Knockback;
        /// <summary>Extra crit chance in percent on top of the base 4%.</summary>
        public int Crit;
        /// <summary>Pickaxe power in Terraria's percent units; 0 for non-pickaxes.</summary>
        public int PickPower;
        /// <summary>Item.fishingPole (fishing power) and Item.bait (bait power).</summary>
        public int FishingPower, BaitPower;
        /// <summary>A pole's bobber (Projectile_N); ItemID.Sets.CanFishInLava for a pole (the Acidline), IsLavaBait for bait: acid here.</summary>
        public int Bobber;
        public bool AcidPole, AcidBait;
        /// <summary>Item.questItem: one of the Angler's fish (rare -11, never worth money, one carried at a time).</summary>
        public bool QuestFish;
        /// <summary>Opened by a right click in the bag (a fishing crate, an Oyster).</summary>
        public bool Opens;
        /// <summary>One of Hallownest's key items (HkItems): in the hotbar or the Hallownest bag only, working from the hotbar.</summary>
        public bool HkItem;
        /// <summary>A prefixed copy's prefix and the plain definition it came from (Prefixes.Apply); a summon weapon's prefix tag damage and armour penetration, its minions'.</summary>
        public byte Prefix;
        public ItemDef Unprefixed;
        /// <summary>A forged copy's level (Forging), and the definition it was forged from.</summary>
        public byte Forge;
        public ItemDef Unforged;
        public int PrefixTagDamage, PrefixArmorPen;

        public ItemDef Clone() => (ItemDef)MemberwiseClone();
        /// <summary>A pet's buff (Item.DefaultToVanitypet's buffType): used it calls the pet, worn in the misc equipment it stays; a light pet's (Main.lightPet).</summary>
        public int PetBuff;
        public bool LightPet;
        /// <summary>Item.hammer shapes tiles and removes walls; Item.axe is shown five times over.</summary>
        public int HammerPower, AxePower;
        /// <summary>Its glowmask (Glow_N), drawn added over it in hand (PlayerDrawLayers: (250,250,250,alpha)).</summary>
        public string Glow;
        /// <summary>Length of one swing, in seconds (Terraria useAnimation).</summary>
        public float UseAnimation;
        /// <summary>Time between hits while held, in seconds (Terraria useTime).</summary>
        public float UseTime;
        /// <summary>Drawing scale of the held item.</summary>
        public float Scale = 1f;
        public CellKind Places = CellKind.Empty;
        public int PlacesWall;
        public int MaxStack = 1;
        public AccessoryEffect Accessory;
        public int Defense;
        /// <summary>Extra (or, negative, less) reach in tiles, Terraria's tileBoost.</summary>
        public int TileBoost;
        /// <summary>Tooltip line under the name, as in Terraria.</summary>
        public string Tooltip;
        /// <summary>The name and tooltip as the mod writes them (Simplified Chinese), kept while another language is shown (ItemDb.Relocalize).</summary>
        public string ZhName, ZhTooltip;
        /// <summary>Sound when used (Terraria UseSound); null for none.</summary>
        public string UseSound = "Item_1";
        /// <summary>The swing itself hits nothing (Terraria noMelee) and the item isn't drawn in hand (noUseGraphic).</summary>
        public bool NoMelee, NoUseGraphic;
        /// <summary>Throws the Zenith's swords: three a use, at useTime intervals.</summary>
        public bool ShootsFinalFractal;
        /// <summary>The sword's own projectiles and swing effects (<see cref="SwordFx"/>).</summary>
        public MeleeFx Fx;

        public DamageClass Class;
        /// <summary>Held out towards the cursor (Terraria useStyle 5) rather than swung.</summary>
        public bool HoldOut;
        /// <summary>useStyle 3: thrust out level in front after the use's first third (the umbrellas), not swung.</summary>
        public bool Thrust;
        /// <summary>Item.holdStyle while not in use: 1 held low in front (the Zombie Arm), 2 held up (the umbrellas over the head, the Breathing Reed); 0 none.</summary>
        public byte HoldStyle;
        /// <summary>
        /// Item.autoReuse: used again while the button stays down. Without it every use needs a
        /// fresh click (Player.ItemCheck: controlUseItem && releaseUseItem).
        /// </summary>
        public bool AutoReuse = true;
        /// <summary>Item.channel: what it casts follows the cursor while the button stays held (Magic Missile, Flamelash).</summary>
        public bool Channel;
        /// <summary>A right click uses it another way (altFunctionUse 2, ItemsThatAllowRepeatedRightClick: held to repeat): the Sky Dragon's Fury's thrust.</summary>
        public bool AltUse;
        /// <summary>Shots fired at once from one ammo (the Boomstick's 3 or 4 pellets); 0 = one.</summary>
        public int Pellets;
        /// <summary>Mana paid once a use that fires twice (the Aqua Scepter's 7 for two streams).</summary>
        public bool ManaEveryOtherShot;
        /// <summary>No mana as the use starts: what it holds out pays as it goes (ItemCheck_PayMana_ShouldSkipManaUse: the Medusa Head).</summary>
        public bool NoUseMana;
        /// <summary>With NoUseMana: enough mana is still needed to start (ItemCheck_PayMana_ShouldPayManaAfterCheck: the Soul Drain, the Laser Machinegun).</summary>
        public bool PayManaAfterCheck;
        /// <summary>useStyle 4: held up over the head through the use (the Blood Thorn).</summary>
        public bool HoldUp;
        /// <summary>Shots a use at most, the rest of the use firing nothing (Pl:43822-43840: the Tome of Infinite Wisdom's 5, the Nightglow's 5).</summary>
        public int ShotsPerUse;
        /// <summary>Item.staff (Main.cs:6933): held out like a staff, turned 45 degrees so the diagonal art points along the aim.</summary>
        public bool StaffHold;
        /// <summary>The use sound's own volume (LegacySoundStyle.WithVolume: the Leaf Blower's 0.13).</summary>
        public float UseSoundVolume = 1f;
        /// <summary>
        /// A sword whose useTime outlasts its swing (Starfury, Enchanted Sword, Ice Blade): it
        /// swings every useAnimation and shoots only on a swing that starts once useTime has passed.
        /// </summary>
        public bool ShootsOnSwingStart => Class == DamageClass.Melee && Shoot != ProjKind.None && !NoMelee && UseTime > UseAnimation;
        /// <summary>Main.DrawPlayerItemPos for held-out items: the back edge's distance in front of the hand, and how much lower (Terraria px).</summary>
        public UnityEngine.Vector2 HoldOffset = new UnityEngine.Vector2(10f, 0f);
        public ProjKind Shoot;
        public float ShootSpeed;
        /// <summary>Ammo this weapon fires, and the ammo this item is.</summary>
        public AmmoKind UseAmmo, Ammo;
        public int Mana;
        /// <summary>Used up on use (thrown explosives, potions, crystals).</summary>
        public bool Consumable;
        /// <summary>A crafted boss rematch seal, distinct from minion staves.</summary>
        public string BossSummonKey;
        public int HealLife, HealMana;
        public bool LifeCrystal, ManaCrystal;
        /// <summary>Kept for good: never sold, thrown, dropped or trashed (the Dream Nail).</summary>
        public bool Permanent;
        /// <summary>Used by the game's own controls, not the Terraria item use (the Dream Nail's FSM).</summary>
        public bool GameTool;
        /// <summary>Item.buffType / buffTime (ticks): the buff a drink gives; potion (potion sickness on drinking).</summary>
        public int BuffType, BuffTime;
        public bool PotionSick;
        /// <summary>The permanent booster it is, used up for good (Player.UpdatePermanentBoosters).</summary>
        public PermanentBoost Boost;
        /// <summary>Placed in the world as a chest that holds things (Chest).</summary>
        public bool PlacesChest;
        /// <summary>Placed in the world as a crafting station (World.Stations).</summary>
        public World.StationKind PlacesStation;
        public ArmorSlot Armor;
        public ArmorSet Set;
        /// <summary>The set bonus this piece picks when the family is complete (a head's, the Beetle body's); None = the family's.</summary>
        public ArmorSet SetBonus;
        /// <summary>Terraria's head/body/leg armor sheet number (Armor_Head_N, Armor/Armor_N, Armor_Legs_N).</summary>
        public int ArmorSheet;
        public Bonuses Bonus;
        /// <summary>Price at the shop in geo; 0 = not sold.</summary>
        public int Price;
        /// <summary>Item.value: the vanilla buy price in copper (a geo is a silver, 100 copper); selling gives a fifth.</summary>
        public int Value;
        /// <summary>When Hallownest first offers it, 0-5 (World.Areas levels), and how.</summary>
        public int Stage;
        public ItemSource Source;
        /// <summary>Item.material: a crafting ingredient with no use of its own.</summary>
        public bool Material;
        /// <summary>How deep into Hallownest the player must have been before the shop sells it (0-3).</summary>
        public int Tier;
        /// <summary>Terraria rarity, for the name colour (-12 the expert rainbow).</summary>
        public int Rare;
        /// <summary>The sheets a worn accessory draws on the player (shoeSlot, waistSlot, ...); 0 for none.</summary>
        public int Shoe, Waist, Balloon, Wing, Shield, Neck, Back, HandOn, HandOff;
        /// <summary>faceSlot (Acc_Face_N, on the head) and frontSlot (Acc_Front_N, the cape's front halves).</summary>
        public int Face, Front;
        /// <summary>The class it serves (<see cref="Items.Kit"/>).</summary>
        public Kit Kit;
        /// <summary>A summon staff: the minion it calls and the buff (Buff_N) that keeps its minions.</summary>
        public MinionKind Minion;
        public int MinionBuff;
        /// <summary>A sentry staff (Item.sentry): no buff and no minion slot; the sentry stands on the ground under the cursor.</summary>
        public bool Sentry;
        /// <summary>A whip: which.</summary>
        public WhipKind Whip;

        public bool IsWeapon => Damage > 0 && Ammo == AmmoKind.None;
        public bool IsAccessory => Accessory != AccessoryEffect.None;
        public bool IsArmor => Armor != ArmorSlot.None;
        public bool IsAmmo => Ammo != AmmoKind.None;
        public bool IsSummon => Minion != MinionKind.None;
        public bool IsWhip => Whip != WhipKind.None;
        public bool IsUsable => !IsPlainAmmo && !GameTool && (IsWeapon || FishingPower > 0 || PickPower > 0 || HammerPower > 0 || PlacesWall > 0 || Places != CellKind.Empty || Shoot != ProjKind.None || HealLife > 0 || HealMana > 0 || LifeCrystal || ManaCrystal
            || BuffType > 0 || PetBuff > 0 || Boost != PermanentBoost.None || PlacesChest || PlacesStation != World.StationKind.None || BossSummonKey != null || Type == ItemType.RecallPotion || Type == ItemType.TeleportationPotion || ItemDb.IsMirror(Type) || ItemDb.IsTeleportRod(Type) || Type == ItemType.WaterCandle);

        /// <summary>
        /// Ammo with no use of its own (Item.useStyle 0: Explosive Jack 'O Lanterns, rockets, darts,
        /// stakes...): its projectile is for the weapon that fires it. Held and clicked it did
        /// nothing in Terraria; here it fired every frame. Ammo that is also thrown (snowballs) has a
        /// use time, and blocks that are ammo (sand) are placed.
        /// </summary>
        public bool IsPlainAmmo => Ammo != AmmoKind.None && UseAnimation <= 0f && Places == CellKind.Empty;
    }

    internal static partial class ItemDb
    {
        const float Tick = 1f / 60f;

        static readonly ItemDef[] defs = Build();

        static ItemDef[] Build()
        {
            var list = new System.Collections.Generic.List<ItemDef>
            {
                new ItemDef { Type = ItemType.None, Name = "" },
                // 1.4.4's shortswords stab (useStyle 13): the blade is a projectile thrust at the cursor.
                new ItemDef { Type = ItemType.CopperShortsword, Name = "铜短剑", Texture = "Item_3507", Class = DamageClass.Melee, Damage = 5, Knockback = 4f, UseAnimation = 13 * Tick, UseTime = 13 * Tick, Scale = 0.8f, Price = 20,
                    NoMelee = true, NoUseGraphic = true, Fx = MeleeFx.Shortsword },
                new ItemDef { Type = ItemType.CopperBroadsword, Name = "铜阔剑", Texture = "Item_3508", Class = DamageClass.Melee, Damage = 9, Knockback = 5f, UseAnimation = 21 * Tick, UseTime = 20 * Tick, Price = 50 },
                new ItemDef { Type = ItemType.CopperPickaxe, Name = "铜镐", Texture = "Item_3509", Class = DamageClass.Melee, Damage = 4, Knockback = 2f, PickPower = 35, UseAnimation = 23 * Tick, UseTime = 15 * Tick, Scale = 0.9f, TileBoost = -1, Price = 40 },
                new ItemDef { Type = ItemType.DirtBlock, Name = "土块", Texture = "Item_2", UseAnimation = 15 * Tick, UseTime = 10 * Tick, Places = CellKind.Dirt, MaxStack = 999, Tooltip = "可放置" },
                new ItemDef { Type = ItemType.StoneBlock, Name = "石块", Texture = "Item_3", UseAnimation = 15 * Tick, UseTime = 10 * Tick, Places = CellKind.Stone, MaxStack = 999, Tooltip = "可放置" },
                new ItemDef { Type = ItemType.Wood, Name = "木材", Texture = "Item_9", UseAnimation = 15 * Tick, UseTime = 10 * Tick, Places = CellKind.Wood, MaxStack = 999, Tooltip = "可放置" },
                new ItemDef { Type = ItemType.Zenith, Name = "天顶剑", Texture = "Item_4956", Class = DamageClass.Melee, Damage = 190, Knockback = 6.5f, Crit = 10, UseAnimation = 30 * Tick, UseTime = 10 * Tick, Tooltip = "\"那是一把剑\"",
                    UseSound = null, NoMelee = true, NoUseGraphic = true, ShootsFinalFractal = true, Rare = 10 },
                new ItemDef { Type = ItemType.HermesBoots, Name = "赫尔墨斯靴", Texture = "Item_54", Accessory = AccessoryEffect.HermesBoots, Tooltip = "穿戴者可飞速奔跑", Rare = 1 },
                new ItemDef { Type = ItemType.CloudinaBottle, Name = "云朵瓶", Texture = "Item_53", Accessory = AccessoryEffect.CloudInABottle, Tooltip = "可让持有者二连跳", Rare = 1 },
                new ItemDef { Type = ItemType.ClimbingClaws, Name = "攀爬爪", Texture = "Item_953", Accessory = AccessoryEffect.ClimbingClaws, Tooltip = "可沿墙滑下，并能蹬墙跳", Rare = 1 },
                new ItemDef { Type = ItemType.ShieldOfCthulhu, Name = "克苏鲁护盾", Texture = "Item_3097", Accessory = AccessoryEffect.ShieldDash, Bonus = new Bonuses { DashKind = 2 }, Defense = 2, Tooltip = "可冲刺撞击敌人\n双击方向键可冲刺", Rare = 2 },
                new ItemDef { Type = ItemType.GrapplingHook, Name = "抓钩", Texture = "Item_84", Tooltip = "\"抓过来！\"\n按 E 朝光标抛出", Rare = 1 },
            };

            // --- Melee. ---
            list.Add(Sword(ItemType.IronBroadsword, "铁阔剑", 4, 12, 5.5f, 20, price: 100, tier: 0));
            list.Add(Sword(ItemType.GoldBroadsword, "金阔剑", 3520, 15, 6.5f, 18, price: 350, tier: 1));
            list.Add(Sword(ItemType.PlatinumBroadsword, "铂金宽剑", 3484, 16, 6.5f, 17, price: 450, tier: 1));
            list.Add(Sword(ItemType.LightsBane, "魔光剑", 46, 16, 5f, 20, price: 700, tier: 1, rare: 1, fx: MeleeFx.LightsBane));
            list.Add(Sword(ItemType.Muramasa, "村正", 155, 24, 3f, 18, price: 1500, tier: 2, rare: 2, fx: MeleeFx.Muramasa));
            list.Add(Sword(ItemType.BladeofGrass, "草剑", 190, 18, 4.5f, 20, price: 1300, tier: 2, rare: 3, tip: "击中时有几率使敌人中毒", fx: MeleeFx.BladeOfGrass));
            list.Add(Sword(ItemType.FieryGreatsword, "火山", 121, 40, 6.5f, 40, price: 2200, tier: 2, rare: 3, tip: "“它是火焰做的！”", fx: MeleeFx.Volcano));
            // Night's Edge, Excalibur and the Terra Blade hit only with their slashes (noMelee).
            list.Add(Sword(ItemType.NightsEdge, "永夜刃", 273, 40, 4.5f, 25, price: 4000, tier: 2, rare: 3, fx: MeleeFx.NightsEdge, noMelee: true));
            list.Add(Sword(ItemType.Excalibur, "断钢剑", 368, 72, 4.5f, 20, price: 8000, tier: 3, rare: 5, fx: MeleeFx.Excalibur, noMelee: true));
            list.Add(Sword(ItemType.TerraBlade, "泰拉刃", 757, 85, 6.5f, 18, price: 15000, tier: 3, rare: 8, fx: MeleeFx.TerraBlade, noMelee: true));
            list.Add(new ItemDef
            {
                Type = ItemType.EnchantedBoomerang, Name = "附魔回旋镖", Texture = "Item_55", Class = DamageClass.Melee, Damage = 17, Knockback = 8f,
                UseAnimation = 20 * Tick, UseTime = 20 * Tick, NoMelee = true, NoUseGraphic = true, Shoot = ProjKind.Boomerang, ShootSpeed = 10f,
                Price = 600, Tier = 1, Rare = 1, Tooltip = "掷出后会飞回",
            });

            // --- More weapons (spec-weapons2; Item.SetDefaults, zh-Hans names and tooltips). ---
            list.Add(Shooter(Sword(ItemType.Starfury, "星怒", 65, 25, 5f, 20, price: 2500, tier: 1, rare: 2, tip: "使星星如雨般从天而降\n“用天堂怒火锻造而成”"), 40, ProjKind.StarfuryStar, 25f));
            list.Add(Shooter(Sword(ItemType.EnchantedSword, "附魔剑", 989, 23, 4.25f, 21, price: 3000, tier: 1, rare: 2, tip: "射出附魔剑光束"), 45, ProjKind.EnchantedBeam, 9.5f, 1.1f));
            var iceBlade = Shooter(Sword(ItemType.IceBlade, "冰雪刃", 724, 17, 4.75f, 20, price: 800, tier: 1, rare: 1, tip: "射出冰矢"), 55, ProjKind.IceBolt, 9.5f);
            iceBlade.Crit = 2;
            list.Add(iceBlade);
            list.Add(Sword(ItemType.BeeKeeper, "养蜂人", 1123, 30, 5.3f, 20, price: 4000, tier: 2, rare: 3, tip: "攻击敌人后召唤杀人蜂\n造成困惑效果"));
            list.Add(Spear(ItemType.DarkLance, "暗黑长枪", 274, 34, 5f, 22, ProjKind.DarkLance, 6f, price: 5000, tier: 2, rare: 3));
            list.Add(Spear(ItemType.Trident, "三叉戟", 277, 14, 6f, 31, ProjKind.TridentSpear, 4f, price: 500, tier: 0, rare: 1, tip: "手持时可提高在水中的移动能力\n按住向上键可以减缓下降速度"));
            list.Add(Thrown(ItemType.WoodenBoomerang, "木回旋镖", 284, DamageClass.Melee, 10, 5f, 20, ProjKind.WoodenBoomerang, 6.5f, price: 300, tier: 0, rare: 1));
            list.Add(Thrown(ItemType.Flamarang, "烈焰回旋镖", 119, DamageClass.Melee, 49, 8f, 20, ProjKind.Flamarang, 14f, price: 5000, tier: 2, rare: 3));
            list.Add(Thrown(ItemType.ThornChakram, "荆棘旋刃", 191, DamageClass.Melee, 25, 8f, 15, ProjKind.ThornChakram, 14f, price: 2500, tier: 1, rare: 3));
            list.Add(Held(Gun(ItemType.Musket, "火枪", 96, 31, 5.25f, 32, 9f, "Item_183", price: 3000, tier: 1, crit: 8, rare: 1, tip: "“民兵说，我必须做好准备”"), -8f, 2f));
            var boomstick = Held(Gun(ItemType.Boomstick, "三发猎枪", 964, 14, 5.75f, 40, 5.35f, "Item_36", price: 4000, tier: 2, rare: 2, tip: "射出一片子弹"), 0f, 0f);
            boomstick.Pellets = 3;
            list.Add(boomstick);
            var shuriken = Thrown(ItemType.Shuriken, "手里剑", 42, DamageClass.Ranged, 10, 0f, 15, ProjKind.Shuriken, 9f, price: 1, tier: 0);
            shuriken.Consumable = true;
            shuriken.MaxStack = 9999;
            list.Add(shuriken);
            var knife = Thrown(ItemType.ThrowingKnife, "投刀", 279, DamageClass.Ranged, 12, 2f, 15, ProjKind.ThrowingKnife, 10f, price: 1, tier: 0);
            knife.Consumable = true;
            knife.MaxStack = 9999;
            list.Add(knife);
            var missile = Staff(ItemType.MagicMissile, "魔法飞弹", 113, 35, 7.5f, 22, 6f, 14, ProjKind.MagicMissile, "Item_9", price: 3500, tier: 2, hold: false, rare: 2, tip: "发射一枚可控的飞弹");
            missile.Channel = true;
            list.Add(missile);
            list.Add(Staff(ItemType.Vilethorn, "魔刺", 64, 10, 1f, 28, 32f, 10, ProjKind.VilethornBase, null, price: 2500, tier: 1, hold: false, rare: 1, tip: "召唤魔刺"));
            var aqua = Held(Staff(ItemType.AquaScepter, "海蓝权杖", 157, 27, 7f, 8, 12.5f, 7, ProjKind.WaterStream, "Item_13", price: 3500, tier: 2, rare: 2, tip: "喷射一阵水"), 6f, 2f);
            aqua.UseAnimation = 16 * Tick;
            aqua.ManaEveryOtherShot = true;
            list.Add(aqua);
            var flamelash = Staff(ItemType.Flamelash, "烈焰火鞭", 218, 32, 6.5f, 30, 6f, 21, ProjKind.Flamelash, "Item_20", price: 5000, tier: 2, hold: false, rare: 3, tip: "召唤可控的火球");
            flamelash.Channel = true;
            list.Add(flamelash);

            // --- Summoner (spec-summoner, spec-minions-*): staves, whips, armor and accessories. 1.4.5 staves cost no mana. ---
            list.Add(Staff2(ItemType.FinchStaff, "雀杖", 4281, 7, 4f, 36, MinionKind.BabyBird, 216, "Item_44", 400, 0, 1, "召唤雀宝宝来为你战斗"));
            list.Add(Staff2(ItemType.FlinxStaff, "小雪怪法杖", 5069, 8, 2f, 36, MinionKind.Flinx, 325, "Item_44", 600, 0, 3, "召唤小雪怪来为你战斗"));
            list.Add(Staff2(ItemType.SlimeStaff, "史莱姆法杖", 1309, 8, 2f, 28, MinionKind.BabySlime, 64, "Item_44", 800, 0, 4, "召唤史莱姆宝宝来为你战斗"));
            list.Add(Staff2(ItemType.VampireFrogStaff, "吸血鬼青蛙法杖", 4273, 11, 5f, 36, MinionKind.VampireFrog, 214, "Item_44", 1200, 1, 3, "召唤吸血鬼青蛙来为你战斗"));
            list.Add(Staff2(ItemType.AbigailsFlower, "阿比盖尔的花", 5114, 6, 2f, 36, MinionKind.Abigail, 335, "Custom/abigail_summon", 1000, 1, 3, "召唤友好鬼魂来为你战斗\n“它美得摄人心魄。”"));
            list.Add(Staff2(ItemType.HornetStaff, "黄蜂法杖", 2364, 12, 2f, 22, MinionKind.Hornet, 125, "Item_76", 1500, 1, 3, "召唤黄蜂来为你战斗"));
            list.Add(Staff2(ItemType.ImpStaff, "小鬼法杖", 2365, 17, 2f, 36, MinionKind.Imp, 126, "Item_77", 2500, 2, 3, "召唤小鬼来为你战斗"));
            list.Add(Staff2(ItemType.SpiderStaff, "蜘蛛法杖", 2551, 26, 3f, 36, MinionKind.Spider, 133, "Item_83", 4000, 2, 4, "召唤蜘蛛来为你战斗"));
            list.Add(Staff2(ItemType.OpticStaff, "魔眼法杖", 2535, 24, 2f, 36, MinionKind.Twins, 134, "Item_82", 6000, 3, 5, "召唤双子魔眼来为你战斗"));
            list.Add(Staff2(ItemType.PirateStaff, "海盗法杖", 2584, 40, 6f, 36, MinionKind.Pirate, 135, "Item_44", 6000, 3, 5, "召唤海盗来为你战斗"));
            list.Add(Staff2(ItemType.SanguineStaff, "血红法杖", 4269, 35, 3f, 36, MinionKind.Sanguine, 213, "Item_83", 7000, 3, 4, "召唤血红蝙蝠来为你战斗"));
            list.Add(Staff2(ItemType.StardustDragonStaff, "星尘之龙法杖", 3531, 40, 2f, 36, MinionKind.StardustDragon, 188, "Item_44", 12000, 3, 10, "召唤星尘之龙来为你战斗\n“有了一条巨龙后，谁还需要一群仆从呢？”"));
            list.Add(Staff2(ItemType.DeadlySphereStaff, "致命球法杖", 3249, 55, 2f, 36, MinionKind.DeadlySphere, 161, "Item_113", 100000, 3, 8, "召唤致命球来为你战斗"));
            list.Add(Staff2(ItemType.BladeStaff, "刃杖", 4758, 6, 0f, 36, MinionKind.BladeStaff, 271, "Item_44", 10000, 3, 5, "召唤附魔匕首为你而战\n无视敌人25点防御力\n标记伤害降低33%，但攻击速度极快\n“别被它们的小个头骗了”"));
            list.Add(Staff2(ItemType.DesertTigerStaff, "沙漠虎杖", 4607, 41, 2.5f, 36, MinionKind.DesertTiger, 263, "Item_44", 200000, 3, 8, "召唤白虎来为你战斗"));
            list.Add(Staff2(ItemType.Terraprisma, "泰拉棱镜", 5005, 90, 4f, 36, MinionKind.Terraprisma, 322, "Item_82", 200000, 3, 8, "召唤附魔剑来为你战斗"));
            const string flameTip = "召唤哨兵\n速度一般的防御塔，可以射出爆炸火球", ballistaTip = "召唤哨兵\n速度缓慢但伤害力极高的防御塔，可以射出穿透性箭矢";
            const string trapTip = "召唤哨兵\n可在敌人来到附近时爆炸的机关", auraTip = "召唤哨兵\n生成一道光环，反复电击进入其中的敌人\n光环伤害可穿透敌人的防御";
            list.Add(SentryStaff(ItemType.FlameburstCane, "爆炸烈焰魔杖", 3818, 17, 3f, MinionKind.Flameburst1, 3, flameTip));
            list.Add(SentryStaff(ItemType.FlameburstRod, "爆炸烈焰手杖", 3819, 42, 3f, MinionKind.Flameburst2, 5, flameTip));
            list.Add(SentryStaff(ItemType.FlameburstStaff, "爆炸烈焰法杖", 3820, 88, 3f, MinionKind.Flameburst3, 8, flameTip));
            list.Add(SentryStaff(ItemType.BallistaCane, "弩车魔杖", 3824, 30, 4.7f, MinionKind.Ballista1, 3, ballistaTip));
            list.Add(SentryStaff(ItemType.BallistaRod, "弩车手杖", 3825, 74, 4.7f, MinionKind.Ballista2, 5, ballistaTip));
            list.Add(SentryStaff(ItemType.BallistaStaff, "弩车法杖", 3826, 156, 4.7f, MinionKind.Ballista3, 8, ballistaTip));
            list.Add(SentryStaff(ItemType.ExplosiveTrapCane, "爆炸机关魔杖", 3832, 24, 0.5f, MinionKind.Trap1, 3, trapTip));
            list.Add(SentryStaff(ItemType.ExplosiveTrapRod, "爆炸机关手杖", 3833, 59, 0.5f, MinionKind.Trap2, 5, trapTip));
            list.Add(SentryStaff(ItemType.ExplosiveTrapStaff, "爆炸机关法杖", 3834, 126, 0.5f, MinionKind.Trap3, 8, trapTip));
            list.Add(SentryStaff(ItemType.LightningAuraCane, "闪电光环魔杖", 3829, 4, 0.25f, MinionKind.Aura1, 3, auraTip));
            list.Add(SentryStaff(ItemType.LightningAuraRod, "闪电光环手杖", 3830, 11, 0.25f, MinionKind.Aura2, 5, auraTip));
            list.Add(SentryStaff(ItemType.LightningAuraStaff, "闪电光环法杖", 3831, 34, 0.25f, MinionKind.Aura3, 8, auraTip));
            list.Add(SentryStaff(ItemType.QueenSpiderStaff, "蜘蛛女王法杖", 2366, 26, 7.5f, MinionKind.QueenSpider, 4, "召唤哨兵\n召唤蜘蛛女王来朝敌人吐蜘蛛卵", "Item_78"));
            list.Add(SentryStaff(ItemType.StaffoftheFrostHydra, "寒霜九头蛇法杖", 1572, 100, 7.5f, MinionKind.FrostHydra, 8, "召唤哨兵\n召唤强大的寒霜九头蛇来朝敌人喷吐冰雪", "Item_1"));
            list.Add(SentryStaff(ItemType.HoundiusShootius, "眼球激光塔", 5119, 24, 7.5f, MinionKind.Houndius, 2, "召唤哨兵\n召唤奥术构造体，向敌人发射光爆\n“我的天哪，快看那只眼睛！”", "Item_1"));
            list.Add(SentryStaff(ItemType.LunarPortalStaff, "月亮传送门法杖", 3569, 100, 7.5f, MinionKind.LunarPortal, 10, "召唤哨兵\n召唤月亮传送门，向敌人发射激光", "Item_78"));
            list.Add(SentryStaff(ItemType.RainbowCrystalStaff, "七彩水晶法杖", 3571, 130, 7.5f, MinionKind.RainbowCrystal, 10, "召唤哨兵\n召唤用来驱逐敌人的放射水晶\n“公爵，多么缤纷的颜色！”", "Item_78"));
            list.Add(SentryStaff(ItemType.BarnacleStaff, "藤壶法杖", 5463, 33, 7.5f, MinionKind.Barnacle, 4, "召唤哨兵\n召唤一颗悬挂藤壶以毒害敌人", "Item_78"));
            list.Add(Staff2(ItemType.PygmyStaff, "矮人法杖", 1157, 40, 3f, 28, MinionKind.Pygmy, 49, "Item_44", 350000, 3, 7, "召唤矮人来为你战斗"));
            list.Add(Staff2(ItemType.RavenStaff, "乌鸦法杖", 1802, 55, 3f, 28, MinionKind.Raven, 83, "Item_44", 100000, 3, 8, "召唤乌鸦来为你战斗"));
            list.Add(Staff2(ItemType.TempestStaff, "暴风雨法杖", 2621, 50, 2f, 36, MinionKind.Sharknado, 139, "Item_44", 50000, 3, 8, "召唤鲨鱼旋风来为你战斗"));
            list.Add(Staff2(ItemType.XenoStaff, "外星法杖", 2749, 36, 2f, 36, MinionKind.UFO, 140, "Item_44", 100000, 3, 8, "召唤UFO来为你战斗"));
            list.Add(Staff2(ItemType.StardustCellStaff, "星尘细胞法杖", 3474, 60, 2f, 36, MinionKind.StardustCell, 182, "Item_44", 100000, 3, 10, "召唤星尘细胞来为你战斗\n“培养最美丽的细胞感染”"));
            const string whipFocus = "你的召唤物将集中攻击被打中的敌人";
            list.Add(Whip(ItemType.LeatherWhip, "皮鞭", 4672, 14, 1f, 4f, 30, WhipKind.Leather, 500, 0, 1, "4召唤标记伤害\n" + whipFocus + "\n“该死的怪物！”"));
            list.Add(Whip(ItemType.Snapthorn, "荆鞭", 4913, 18, 1.5f, 4f, 30, WhipKind.Snapthorn, 1500, 1, 3, "6召唤标记伤害\n" + whipFocus + "\n打击敌人以增加鞭子攻击速度"));
            list.Add(Whip(ItemType.SpinalTap, "脊柱骨鞭", 5074, 29, 2f, 5f, 30, WhipKind.SpinalTap, 2500, 2, 2, "7召唤标记伤害\n" + whipFocus + "\n对抗多个目标时，比大多数鞭子表现更好\n“到十一了”"));
            list.Add(Whip(ItemType.Firecracker, "鞭炮", 4912, 37, 2f, 4f, 30, WhipKind.Firecracker, 4000, 2, 4, whipFocus + "\n用烈焰能量打击敌人\n被召唤物击中的敌人会爆出烈焰能量"));
            list.Add(Whip(ItemType.CoolWhip, "冷鞭", 4911, 45, 1.5f, 4f, 30, WhipKind.CoolWhip, 5000, 3, 4, "6召唤标记伤害\n" + whipFocus + "\n打击敌人以召唤友好的雪花\n“让我来点冷酷的”"));
            list.Add(Whip(ItemType.Durendal, "迪朗达尔", 4678, 55, 2f, 4f, 28, WhipKind.Durendal, 7000, 3, 5, "9点召唤标记伤害\n" + whipFocus + "\n用魔能打击敌人以提升鞭子攻击速度\n魔能会惩戒被召唤物击中的敌人"));
            list.Add(Whip(ItemType.MorningStar, "晨星", 4679, 160, 11f, 4f, 35, WhipKind.MorningStar, 15000, 3, 8, "8点召唤标记伤害\n10%的召唤标记暴击率\n" + whipFocus));
            list.Add(Whip(ItemType.SlimeWhip, "史莱姆鞭子", 5688, 15, 1f, 4f, 30, WhipKind.SlimeWhip, 250, 0, 1, "3点召唤标记伤害\n鞭击可点燃敌人"));
            list.Add(Whip(ItemType.CobWhip, "锥鞭", 5473, 9, 0.7f, 3f, 35, WhipKind.CobWhip, 750, 0, 1, "3点召唤标记伤害\n" + whipFocus + "\n打击敌人以召唤一只微型蜘蛛"));
            list.Add(Whip(ItemType.DarkHarvest, "暗黑收割", 4680, 110, 3f, 4f, 27, WhipKind.DarkHarvest, 100000, 3, 8, whipFocus + "\n用黑暗能量打击敌人以增加鞭子攻击速度\n被召唤物击中的敌人身上会跳出黑暗能量"));
            list.Add(Whip(ItemType.Kaleidoscope, "万花筒", 4914, 170, 4f, 4f, 30, WhipKind.Kaleidoscope, 50000, 3, 8, "20召唤标记伤害\n10%召唤标记暴击率\n" + whipFocus));
            list.Add(Whip(ItemType.CorruptWhip, "魂笞", 5474, 17, 1.35f, 4f, 30, WhipKind.CorruptWhip, 5000, 1, 1, "5点召唤标记伤害\n" + whipFocus + "\n“用邪恶化身的残余鞭打你的敌人”"));
            list.Add(Whip(ItemType.CrimsonWhip, "脉鞭", 5475, 19, 1.25f, 4f, 30, WhipKind.CrimsonWhip, 5000, 1, 1, "5点召唤标记伤害\n" + whipFocus + "\n“动脉、静脉和肌腱！”"));
            list.Add(Whip(ItemType.MeteorWhip, "星坠", 5476, 18, 1.5f, 4f, 30, WhipKind.MeteorWhip, 12500, 1, 2, "2点召唤标记伤害\n" + whipFocus + "\n用预置能量打击敌人\n陨石落在被召唤物击中的预置敌人身上"));
            list.Add(Whip(ItemType.FlowerWhip, "花之粗鄙展示", 5477, 50, 2f, 4f, 30, WhipKind.FlowerWhip, 30000, 3, 7, "9点召唤标记伤害\n" + whipFocus + "\n用挥发性能量打击敌人\n被召唤物击中的敌人会绽放出猛烈的花瓣\n“从丛林女王身上扯下来一根带刺鞭子？太酷了！”"));
            list.Add(Whip(ItemType.EelWhip, "电鳗", 5478, 150, 3f, 4f, 30, WhipKind.EelWhip, 25000, 3, 8, "12点召唤标记伤害\n5%召唤标记暴击率\n" + whipFocus + "\n用电能打击敌人\n电能会在被召唤物击中的敌人之间共振"));
            list.Add(Whip(ItemType.ConstellationWhip, "星座", 5479, 130, 4f, 4f, 30, WhipKind.ConstellationWhip, 50000, 3, 10, "15点召唤标记伤害\n15%召唤标记暴击率\n" + whipFocus + "\n用宇宙能量打击敌人\n宇宙能量会从被召唤物击中的敌人身上滋长\n再次打击可散落一片星宿"));
            list.Add(Whip(ItemType.MoonLordWhip, "私有物", 5480, 110, 4f, 4f, 30, WhipKind.MoonLordWhip, 50000, 3, 10, "25点召唤标记伤害\n10%召唤标记暴击率\n" + whipFocus + "\n用先知能量打击敌人\n幻象打击被召唤物击中的敌人"));
            list.Add(Armor(ItemType.FlinxFurCoat, "小雪怪皮毛外套", 5068, ArmorSlot.Body, 241, 1, ArmorSet.None, price: 600, tier: 0, rare: 2, tip: "仆从数量上限增加1\n召唤伤害提高5%", bonus: new Bonuses { MaxMinions = 1, MinionDamage = 0.05f }));
            list.Add(Armor(ItemType.BeeHeadgear, "蜜蜂头饰", 2361, ArmorSlot.Head, 160, 4, ArmorSet.Bee, price: 1200, tier: 1, rare: 3, tip: "召唤伤害提高4%\n仆从数量上限增加1", bonus: new Bonuses { MaxMinions = 1, MinionDamage = 0.04f }));
            list.Add(Armor(ItemType.BeeBreastplate, "蜜蜂胸甲", 2362, ArmorSlot.Body, 168, 5, ArmorSet.Bee, price: 1500, tier: 1, rare: 3, tip: "召唤伤害提高4%\n仆从数量上限增加1", bonus: new Bonuses { MaxMinions = 1, MinionDamage = 0.04f }));
            list.Add(Armor(ItemType.BeeGreaves, "蜜蜂护胫", 2363, ArmorSlot.Legs, 103, 4, ArmorSet.Bee, price: 1300, tier: 1, rare: 3, tip: "召唤伤害提高5%", bonus: new Bonuses { MinionDamage = 0.05f }));
            list.Add(Armor(ItemType.ObsidianOutlawHat, "黑曜石逃犯帽", 3266, ArmorSlot.Head, 185, 4, ArmorSet.Obsidian, price: 900, tier: 1, rare: 1, tip: "召唤伤害提高8%", bonus: new Bonuses { MinionDamage = 0.08f }));
            list.Add(Armor(ItemType.ObsidianLongcoat, "黑曜石风衣", 3267, ArmorSlot.Body, 187, 6, ArmorSet.Obsidian, price: 900, tier: 1, rare: 1, tip: "仆从数量上限增加1", bonus: new Bonuses { MaxMinions = 1 }));
            list.Add(Armor(ItemType.ObsidianPants, "黑曜石裤", 3268, ArmorSlot.Legs, 127, 5, ArmorSet.Obsidian, price: 900, tier: 1, rare: 1, tip: "召唤伤害提高8%", bonus: new Bonuses { MinionDamage = 0.08f }));
            list.Add(Armor(ItemType.SpiderMask, "蜘蛛面具", 2370, ArmorSlot.Head, 162, 5, ArmorSet.Spider, price: 2500, tier: 2, rare: 4, tip: "仆从数量上限增加1\n召唤伤害提高5%", bonus: new Bonuses { MaxMinions = 1, MinionDamage = 0.05f }));
            list.Add(Armor(ItemType.SpiderBreastplate, "蜘蛛胸甲", 2371, ArmorSlot.Body, 170, 8, ArmorSet.Spider, price: 2500, tier: 2, rare: 4, tip: "仆从数量上限增加1\n召唤伤害提高5%", bonus: new Bonuses { MaxMinions = 1, MinionDamage = 0.05f }));
            list.Add(Armor(ItemType.SpiderGreaves, "蜘蛛护胫", 2372, ArmorSlot.Legs, 105, 7, ArmorSet.Spider, price: 2500, tier: 2, rare: 4, tip: "仆从数量上限增加1\n召唤伤害提高6%", bonus: new Bonuses { MaxMinions = 1, MinionDamage = 0.06f }));
            list.Add(Acc(ItemType.PygmyNecklace, "矮人项链", 1158, price: 3000, tier: 2, rare: 7, tip: "仆从数量上限增加1", bonus: new Bonuses { MaxMinions = 1 }));
            list.Add(Acc(ItemType.SummonerEmblem, "召唤师徽章", 2998, price: 3000, tier: 2, rare: 4, tip: "召唤伤害提高15%", bonus: new Bonuses { MinionDamage = 0.15f }));
            list.Add(Acc(ItemType.NecromanticScroll, "死灵卷轴", 1845, price: 5000, tier: 3, rare: 8, tip: "仆从数量上限增加1\n召唤伤害提高10%", bonus: new Bonuses { MaxMinions = 1, MinionDamage = 0.1f }));
            list.Add(Acc(ItemType.PapyrusScarab, "甲虫莎草纸", 1864, price: 6000, tier: 3, rare: 8, tip: "仆从数量上限增加1\n召唤伤害提高15%、提高仆从的击退力", bonus: new Bonuses { MaxMinions = 1, MinionDamage = 0.15f, MinionKB = 2f }));
            list.Add(Acc(ItemType.HerculesBeetle, "大力士甲虫", 1167, price: 6000, tier: 3, rare: 7, tip: "召唤伤害提高15%\n提高仆从的击退力", bonus: new Bonuses { MinionDamage = 0.15f, MinionKB = 2f }));

            // --- Pickaxes: the deeper parts of Hallownest need stronger ones (see Areas). ---
            list.Add(Pick(ItemType.IronPickaxe, "铁镐", 1, 40, 5, 20, 13, 1f, price: 120, tier: 0));
            list.Add(Pick(ItemType.GoldPickaxe, "金镐", 3521, 55, 6, 20, 17, 1.05f, price: 450, tier: 1));
            list.Add(Pick(ItemType.NightmarePickaxe, "梦魇镐", 103, 65, 9, 20, 15, 1f, price: 1500, tier: 2, rare: 1));
            list.Add(Pick(ItemType.MoltenPickaxe, "熔岩镐", 122, 100, 12, 23, 18, 1f, price: 3000, tier: 2, rare: 3));
            list.Add(Pick(ItemType.CobaltPickaxe, "钴镐", 776, 110, 10, 25, 13, 1f, price: 5000, tier: 3, rare: 4));
            list.Add(Pick(ItemType.MythrilPickaxe, "秘银镐", 777, 150, 15, 25, 10, 1f, price: 7000, tier: 3, rare: 4));
            list.Add(Pick(ItemType.TitaniumPickaxe, "钛金镐", 1202, 190, 27, 25, 7, 1f, price: 10000, tier: 3, rare: 4));
            var picksaw = Pick(ItemType.Picksaw, "锯刃镐", 1294, 210, 34, 16, 6, 1.15f, price: 14000, tier: 3, rare: 7);
            picksaw.TileBoost = 1;
            list.Add(picksaw);

            // --- Ranged. ---
            list.Add(Bow(ItemType.WoodenBow, "木弓", 39, 4, 0f, 30, 6.1f, price: 30, tier: 0));
            list.Add(Bow(ItemType.IronBow, "铁弓", 99, 8, 0f, 28, 6.6f, price: 110, tier: 0));
            list.Add(Bow(ItemType.GoldBow, "金弓", 3516, 11, 0f, 26, 6.6f, price: 400, tier: 1));
            list.Add(Bow(ItemType.DemonBow, "恶魔弓", 44, 14, 1f, 25, 6.7f, price: 1200, tier: 2, rare: 1));
            var fury = Bow(ItemType.MoltenFury, "熔火之怒", 120, 31, 2f, 22, 8f, price: 2600, tier: 2, rare: 3, tip: "点燃木箭，火光熊熊");
            fury.Scale = 1.1f;
            list.Add(fury);
            list.Add(Held(Gun(ItemType.FlintlockPistol, "燧发枪", 95, 13, 1f, 16, 6f, "Item_184", price: 450, tier: 1, crit: 6, rare: 1, scale: 0.9f), 6f, 2f));
            list.Add(Held(Gun(ItemType.Handgun, "手枪", 164, 26, 3f, 15, 10f, "Item_41", price: 2000, tier: 2, rare: 2, scale: 0.85f), 0f, 2f));
            var shark = Gun(ItemType.Minishark, "迷你鲨", 98, 6, 0f, 8, 7f, "Item_11", price: 2500, tier: 2, rare: 2, tip: "33%几率省下弹药\n“半鲨，半枪，帅呆了。”");
            shark.Bonus.AmmoSave = 0.33f;
            list.Add(Held(shark, -5f, -2f));
            list.Add(Held(Gun(ItemType.PhoenixBlaster, "凤凰爆破枪", 219, 30, 2f, 14, 13f, "Item_41", price: 3500, tier: 2, rare: 3, scale: 0.85f), 0f, 2f));
            list.Add(new ItemDef
            {
                Type = ItemType.Grenade, Name = "手榴弹", Texture = "Item_168", Class = DamageClass.Ranged, Damage = 60, Knockback = 8f,
                UseAnimation = 45 * Tick, UseTime = 45 * Tick, NoMelee = true, NoUseGraphic = true, Shoot = ProjKind.Grenade, ShootSpeed = 5.5f,
                Consumable = true, MaxStack = 999, Price = 8, Tier = 0, Tooltip = "造成不会摧毁图格的小爆炸",
            });
            list.Add(Ammo(ItemType.WoodenArrow, "木箭", 40, AmmoKind.Arrow, ProjKind.WoodenArrow, 5, 2f, 3f, price: 1, tier: 0));
            list.Add(Ammo(ItemType.FlamingArrow, "烈焰箭", 41, AmmoKind.Arrow, ProjKind.FlamingArrow, 7, 2f, 3.5f, price: 2, tier: 0));
            list.Add(Ammo(ItemType.UnholyArrow, "邪箭", 47, AmmoKind.Arrow, ProjKind.UnholyArrow, 12, 3f, 3.4f, price: 4, tier: 1, tip: "可穿透多个敌人"));
            list.Add(Ammo(ItemType.JestersArrow, "小丑之箭", 51, AmmoKind.Arrow, ProjKind.JesterArrow, 10, 4f, 0.5f, price: 5, tier: 1, tip: "可穿透敌人和障碍"));
            list.Add(Ammo(ItemType.MusketBall, "火枪子弹", 97, AmmoKind.Bullet, ProjKind.Bullet, 7, 2f, 4f, price: 1, tier: 0));
            list.Add(Ammo(ItemType.MeteorShot, "流星弹", 234, AmmoKind.Bullet, ProjKind.MeteorShot, 8, 1f, 3f, price: 3, tier: 1, tip: "可从墙上弹开"));
            // Every other arrow and bullet (Item.SetDefaults; names and tooltips from the zh-Hans text).
            list.Add(Ammo(ItemType.HellfireArrow, "狱炎箭", 265, AmmoKind.Arrow, ProjKind.HellfireArrow, 13, 8f, 6.5f, price: 6, tier: 2, rare: 2));
            list.Add(Ammo(ItemType.HolyArrow, "圣箭", 516, AmmoKind.Arrow, ProjKind.HolyArrow, 13, 2f, 3.5f, price: 6, tier: 2, rare: 3, tip: "在撞击时召唤陨星"));
            list.Add(Ammo(ItemType.CursedArrow, "诅咒箭", 545, AmmoKind.Arrow, ProjKind.CursedArrow, 17, 3f, 4f, price: 5, tier: 2, rare: 3));
            list.Add(Ammo(ItemType.FrostburnArrow, "霜冻箭", 988, AmmoKind.Arrow, ProjKind.FrostburnArrow, 9, 2.2f, 3.75f, price: 2, tier: 1));
            list.Add(Ammo(ItemType.ChlorophyteArrow, "叶绿箭", 1235, AmmoKind.Arrow, ProjKind.ChlorophyteArrow, 16, 3.5f, 4.5f, price: 8, tier: 3, rare: 7, tip: "撞墙后弹回"));
            list.Add(Ammo(ItemType.IchorArrow, "灵液箭", 1334, AmmoKind.Arrow, ProjKind.IchorArrow, 16, 3f, 4.25f, price: 5, tier: 2, rare: 3, tip: "降低目标的防御力"));
            list.Add(Ammo(ItemType.VenomArrow, "毒液箭", 1341, AmmoKind.Arrow, ProjKind.VenomArrow, 19, 4.2f, 4.3f, price: 7, tier: 2, rare: 3, tip: "使目标染上强效毒液"));
            list.Add(Ammo(ItemType.BoneArrow, "骨箭", 3003, AmmoKind.Arrow, ProjKind.BoneArrow, 8, 2.5f, 3.5f, price: 2, tier: 1));
            list.Add(Ammo(ItemType.EndlessQuiver, "无尽箭袋", 3103, AmmoKind.Arrow, ProjKind.WoodenArrow, 5, 2f, 3f, price: 2500, tier: 3, rare: 2, endless: true));
            list.Add(Ammo(ItemType.LuminiteArrow, "夜明箭", 3568, AmmoKind.Arrow, ProjKind.LuminiteArrow, 15, 3.5f, 3f, price: 10, tier: 3, rare: 9, tip: "“以音速射下它们！”"));
            list.Add(Ammo(ItemType.ShimmerArrow, "微光箭", 5348, AmmoKind.Arrow, ProjKind.ShimmerArrow, 12, 2f, 3f, price: 3, tier: 1, rare: 2, tip: "向上坠落"));
            list.Add(Ammo(ItemType.SilverBullet, "银子弹", 278, AmmoKind.Bullet, ProjKind.SilverBullet, 9, 3f, 4.5f, price: 2, tier: 0));
            list.Add(Ammo(ItemType.TungstenBullet, "钨子弹", 4915, AmmoKind.Bullet, ProjKind.Bullet, 9, 4f, 4.5f, price: 2, tier: 0));
            list.Add(Ammo(ItemType.CrystalBullet, "水晶子弹", 515, AmmoKind.Bullet, ProjKind.CrystalBullet, 9, 1f, 5f, price: 4, tier: 2, rare: 3, tip: "在撞击时产生水晶碎块"));
            list.Add(Ammo(ItemType.CursedBullet, "诅咒弹", 546, AmmoKind.Bullet, ProjKind.CursedBullet, 15, 4f, 5f, price: 5, tier: 2, rare: 3));
            list.Add(Ammo(ItemType.IchorBullet, "灵液弹", 1335, AmmoKind.Bullet, ProjKind.IchorBullet, 13, 4f, 5.25f, price: 5, tier: 2, rare: 3, tip: "降低目标的防御力"));
            list.Add(Ammo(ItemType.ChlorophyteBullet, "叶绿弹", 1179, AmmoKind.Bullet, ProjKind.ChlorophyteBullet, 9, 4.5f, 5f, price: 7, tier: 3, rare: 7, tip: "追杀敌人"));
            list.Add(Ammo(ItemType.HighVelocityBullet, "高速子弹", 1302, AmmoKind.Bullet, ProjKind.HighVelocityBullet, 11, 4f, 4f, price: 5, tier: 2, rare: 3));
            list.Add(Ammo(ItemType.VenomBullet, "毒液弹", 1342, AmmoKind.Bullet, ProjKind.VenomBullet, 15, 4.1f, 5.3f, price: 6, tier: 2, rare: 3, tip: "在一定范围内施放强效毒液"));
            list.Add(Ammo(ItemType.PartyBullet, "派对弹", 1349, AmmoKind.Bullet, ProjKind.PartyBullet, 10, 5f, 5.1f, price: 4, tier: 2, rare: 3, tip: "碰撞时爆炸成彩纸"));
            list.Add(Ammo(ItemType.NanoBullet, "纳米弹", 1350, AmmoKind.Bullet, ProjKind.NanoBullet, 10, 3.6f, 4.6f, price: 6, tier: 2, rare: 3, tip: "造成困惑，击中墙壁后会反弹\n额外造成25%的伤害"));
            list.Add(Ammo(ItemType.ExplodingBullet, "爆破弹", 1351, AmmoKind.Bullet, ProjKind.ExplodingBullet, 10, 6.6f, 4.7f, price: 6, tier: 2, rare: 3, tip: "碰撞时爆炸"));
            list.Add(Ammo(ItemType.GoldenBullet, "金子弹", 1352, AmmoKind.Bullet, ProjKind.GoldenBullet, 10, 3.6f, 4.6f, price: 6, tier: 2, rare: 3, tip: "杀死的敌人会掉落更多的钱"));
            list.Add(Ammo(ItemType.EndlessMusketPouch, "无尽火枪袋", 3104, AmmoKind.Bullet, ProjKind.Bullet, 7, 2f, 4f, price: 2500, tier: 3, rare: 2, endless: true));
            list.Add(Ammo(ItemType.LuminiteBullet, "夜明弹", 3567, AmmoKind.Bullet, ProjKind.LuminiteBullet, 20, 3f, 2f, price: 10, tier: 3, rare: 9, tip: "“鬼怪排成行，一下全扫荡……”"));

            // --- Magic. ---
            list.Add(Staff(ItemType.WandofSparking, "火花魔棒", 3069, 14, 0f, 26, 7f, 2, ProjKind.Spark, "Item_8", hold: false, price: 250, tier: 0, crit: 10, rare: 1, tip: "射出小火花"));
            list.Add(Staff(ItemType.AmethystStaff, "紫晶法杖", 739, 15, 3.25f, 37, 6f, 7, ProjKind.AmethystBolt, "Item_43", price: 150, tier: 0));
            list.Add(Staff(ItemType.RubyStaff, "红玉法杖", 743, 18, 4.75f, 28, 9f, 9, ProjKind.RubyBolt, "Item_43", price: 600, tier: 1, rare: 1));
            list.Add(Staff(ItemType.DiamondStaff, "钻石法杖", 744, 23, 5.5f, 26, 9.5f, 9, ProjKind.DiamondBolt, "Item_43", price: 1100, tier: 2, rare: 2));
            // I:9994-10042, 30861-30875: the other gem staves and their 1.4.5 gem effects (Topaz bursts, Sapphire twins, Emerald fast-then-slow, Amber bounces).
            var topaz = Staff(ItemType.TopazStaff, "黄玉法杖", 740, 16, 3.5f, 36, 6.5f, 7, ProjKind.TopazBolt, "Item_43", price: 0, tier: 0, tip: "黄玉绚烂绽放");
            topaz.AutoReuse = false;
            list.Add(topaz);
            list.Add(Staff(ItemType.SapphireStaff, "蓝玉法杖", 741, 18, 4f, 34, 7.5f, 8, ProjKind.SapphireBolt, "Item_43", price: 0, tier: 0, rare: 1, tip: "蓝玉成对旋舞"));
            list.Add(Staff(ItemType.EmeraldStaff, "翡翠法杖", 742, 19, 4.25f, 32, 8f, 8, ProjKind.EmeraldBolt, "Item_43", price: 0, tier: 1, rare: 1, tip: "翡翠照亮黑夜"));
            list.Add(Staff(ItemType.AmberStaff, "琥珀法杖", 3377, 21, 4.75f, 28, 9f, 9, ProjKind.AmberBolt, "Item_43", price: 0, tier: 1, rare: 1, tip: "琥珀开辟前路"));
            // Main.cs:6880-6972: held out like staves (Item.staff).
            foreach (var gem in list)
                if (gem.Type == ItemType.TopazStaff || gem.Type == ItemType.SapphireStaff || gem.Type == ItemType.EmeraldStaff || gem.Type == ItemType.AmberStaff) gem.StaffHold = true;
            // I:15559-15571: the Crimson Rod, swung; its cloud heads for the cursor like the Nimbus Rod's (Pl:48743).
            var crimsonRod = Staff(ItemType.CrimsonRod, "猩红魔杖", 1256, 12, 0f, 24, 12f, 30, ProjKind.CrimsonCloudMoving, "Item_8", price: 0, tier: 1, hold: false, rare: 1,
                tip: "召唤云朵来向敌人降下血雨");
            crimsonRod.AutoReuse = false;
            list.Add(crimsonRod);
            list.Add(Held(Staff(ItemType.WaterBolt, "水矢", 165, 19, 5f, 17, 4.5f, 10, ProjKind.WaterBolt, "Item_21", price: 1000, tier: 1, rare: 2, tip: "发射移动缓慢的水矢\n会在墙上反弹", scale: 0.9f), 4f, 4f));
            list.Add(Held(Staff(ItemType.SpaceGun, "太空枪", 127, 20, 0.75f, 17, 10f, 6, ProjKind.Laser, "Item_157", price: 1200, tier: 1, rare: 1, scale: 0.8f), 4f, 2f));
            list.Add(Held(Staff(ItemType.DemonScythe, "恶魔之镰", 272, 35, 5f, 20, 0.2f, 14, ProjKind.DemonScythe, "Item_8", price: 2500, tier: 2, rare: 3, tip: "投掷恶魔之镰", scale: 0.9f), 4f, 4f));
            list.Add(Staff(ItemType.FlowerofFire, "火之花", 112, 48, 5.5f, 16, 7.5f, 9, ProjKind.BallofFire, "Item_20", hold: false, price: 3000, tier: 2, rare: 3, tip: "投掷会弹跳的火球"));

            // --- Explosives and building. ---
            list.Add(new ItemDef
            {
                Type = ItemType.Bomb, Name = "炸弹", Texture = "Item_166", UseAnimation = 25 * Tick, UseTime = 25 * Tick, NoMelee = true, NoUseGraphic = true,
                Shoot = ProjKind.Bomb, ShootSpeed = 5f, Consumable = true, MaxStack = 999, Price = 15, Tier = 0,
                Tooltip = "造成会摧毁大部分图格的小爆炸\n能炸开需要下砸的地板",
            });
            list.Add(new ItemDef
            {
                Type = ItemType.Dynamite, Name = "雷管", Texture = "Item_167", UseAnimation = 40 * Tick, UseTime = 40 * Tick, NoMelee = true, NoUseGraphic = true,
                Shoot = ProjKind.Dynamite, ShootSpeed = 4f, Consumable = true, MaxStack = 999, Price = 60, Tier = 0, Rare = 1,
                Tooltip = "造成会摧毁大部分图格的大爆炸\n能炸开需要下砸的地板",
            });
            list.Add(new ItemDef { Type = ItemType.Torch, Name = "火把", Texture = "Item_8", UseAnimation = 15 * Tick, UseTime = 10 * Tick, Places = CellKind.Torch, MaxStack = 999, Price = 1, Tier = 0, Tooltip = "提供照明", HoldStyle = 1 });
            list.Add(new ItemDef { Type = ItemType.SandBlock, Name = "沙块", Texture = "Item_169", UseAnimation = 15 * Tick, UseTime = 10 * Tick, Places = CellKind.Sand, MaxStack = 999, Price = 1, Tier = 0, Tooltip = "下方悬空时会落下" });
            list.Add(new ItemDef { Type = ItemType.WoodPlatform, Name = "木平台", Texture = "Item_94", UseAnimation = 15 * Tick, UseTime = 10 * Tick, Places = CellKind.Platform, MaxStack = 999, Price = 1, Tier = 0, Tooltip = "可从下方跳上，按住下可穿过" });
            list.Add(new ItemDef { Type = ItemType.Rope, Name = "绳", Texture = "Item_965", UseAnimation = 15 * Tick, UseTime = 8 * Tick, Places = CellKind.Rope, MaxStack = 999, TileBoost = 3, Price = 1, Tier = 0, Tooltip = "可供攀爬\n按住上下攀爬" });

            // --- Armor. ---
            list.Add(Armor(ItemType.MiningHelmet, "挖矿头盔", 88, ArmorSlot.Head, 11, 2, ArmorSet.Mining, price: 400, tier: 0, rare: 1, tip: "穿戴时可提供照明\n照亮黑暗的区域", bonus: new Bonuses { Light = true }));
            list.Add(Armor(ItemType.CopperHelmet, "铜头盔", 89, ArmorSlot.Head, 1, 1, ArmorSet.Copper, price: 40, tier: 0));
            list.Add(Armor(ItemType.CopperChainmail, "铜链甲", 80, ArmorSlot.Body, 1, 2, ArmorSet.Copper, price: 60, tier: 0));
            list.Add(Armor(ItemType.CopperGreaves, "铜护胫", 76, ArmorSlot.Legs, 1, 1, ArmorSet.Copper, price: 50, tier: 0));
            list.Add(Armor(ItemType.IronHelmet, "铁头盔", 90, ArmorSlot.Head, 2, 2, ArmorSet.Iron, price: 120, tier: 0));
            list.Add(Armor(ItemType.IronChainmail, "铁链甲", 81, ArmorSlot.Body, 2, 3, ArmorSet.Iron, price: 200, tier: 0));
            list.Add(Armor(ItemType.IronGreaves, "铁护胫", 77, ArmorSlot.Legs, 2, 2, ArmorSet.Iron, price: 160, tier: 0));
            list.Add(Armor(ItemType.GoldHelmet, "金头盔", 92, ArmorSlot.Head, 4, 4, ArmorSet.Gold, price: 450, tier: 1));
            list.Add(Armor(ItemType.GoldChainmail, "金链甲", 83, ArmorSlot.Body, 4, 5, ArmorSet.Gold, price: 650, tier: 1));
            list.Add(Armor(ItemType.GoldGreaves, "金护胫", 79, ArmorSlot.Legs, 4, 4, ArmorSet.Gold, price: 550, tier: 1));
            list.Add(Armor(ItemType.ShadowHelmet, "暗影头盔", 102, ArmorSlot.Head, 5, 6, ArmorSet.Shadow, price: 1500, tier: 2, rare: 1, tip: "暴击率提高5%", bonus: new Bonuses { AllCrit = 0.05f }));
            list.Add(Armor(ItemType.ShadowScalemail, "暗影鳞甲", 101, ArmorSlot.Body, 5, 7, ArmorSet.Shadow, price: 1500, tier: 2, rare: 1, tip: "暴击率提高5%", bonus: new Bonuses { AllCrit = 0.05f }));
            list.Add(Armor(ItemType.ShadowGreaves, "暗影护胫", 100, ArmorSlot.Legs, 5, 6, ArmorSet.Shadow, price: 1500, tier: 2, rare: 1, tip: "暴击率提高5%", bonus: new Bonuses { AllCrit = 0.05f }));
            list.Add(Armor(ItemType.NecroHelmet, "死灵头盔", 151, ArmorSlot.Head, 7, 6, ArmorSet.Necro, price: 1500, tier: 2, rare: 2, tip: "远程伤害提高5%", bonus: new Bonuses { RangedDamage = 0.05f }));
            list.Add(Armor(ItemType.NecroBreastplate, "死灵胸甲", 152, ArmorSlot.Body, 7, 7, ArmorSet.Necro, price: 1500, tier: 2, rare: 2, tip: "远程伤害提高5%", bonus: new Bonuses { RangedDamage = 0.05f }));
            list.Add(Armor(ItemType.NecroGreaves, "死灵护胫", 153, ArmorSlot.Legs, 7, 6, ArmorSet.Necro, price: 1500, tier: 2, rare: 2, tip: "远程伤害提高5%", bonus: new Bonuses { RangedDamage = 0.05f }));
            list.Add(Armor(ItemType.JungleHat, "丛林帽", 228, ArmorSlot.Head, 8, 5, ArmorSet.Jungle, price: 1500, tier: 2, rare: 3, tip: "最大魔力增加40\n魔法暴击率提高6%", bonus: new Bonuses { ManaMax = 40, MagicCrit = 0.06f }));
            list.Add(Armor(ItemType.JungleShirt, "丛林衣", 229, ArmorSlot.Body, 8, 6, ArmorSet.Jungle, price: 1500, tier: 2, rare: 3, tip: "最大魔力增加20\n魔法伤害提高6%", bonus: new Bonuses { ManaMax = 20, MagicDamage = 0.06f }));
            list.Add(Armor(ItemType.JunglePants, "丛林裤", 230, ArmorSlot.Legs, 8, 6, ArmorSet.Jungle, price: 1500, tier: 2, rare: 3, tip: "最大魔力增加20\n魔法暴击率提高6%", bonus: new Bonuses { ManaMax = 20, MagicCrit = 0.06f }));
            list.Add(Armor(ItemType.MoltenHelmet, "熔岩头盔", 231, ArmorSlot.Head, 9, 8, ArmorSet.Molten, price: 2500, tier: 3, rare: 3, tip: "近战暴击率提高7%", bonus: new Bonuses { MeleeCrit = 0.07f }));
            list.Add(Armor(ItemType.MoltenBreastplate, "熔岩胸甲", 232, ArmorSlot.Body, 9, 9, ArmorSet.Molten, price: 2500, tier: 3, rare: 3, tip: "近战伤害提高7%", bonus: new Bonuses { MeleeDamage = 0.07f }));
            list.Add(Armor(ItemType.MoltenGreaves, "熔岩护胫", 233, ArmorSlot.Legs, 9, 8, ArmorSet.Molten, price: 2500, tier: 3, rare: 3, tip: "近战速度提高7%", bonus: new Bonuses { MeleeSpeed = 0.07f }));

            // --- Accessories. ---
            list.Add(Acc(ItemType.RocketBoots, "火箭靴", 128, price: 2500, tier: 2, rare: 3, tip: "可飞行\n在空中按住跳跃", bonus: new Bonuses { RocketBoots = true }));
            list.Add(Acc(ItemType.FlyingCarpet, "飞毯", 934, price: 2500, tier: 1, rare: 2, tip: "可让持有者漂浮几秒钟", bonus: new Bonuses { FlyingCarpet = true }));
            list.Add(Acc(ItemType.ShinyRedBalloon, "闪亮红气球", 159, price: 1200, tier: 1, rare: 1, tip: "增加跳跃高度", bonus: new Bonuses { JumpBoost = true }));
            list.Add(Acc(ItemType.LuckyHorseshoe, "幸运马掌", 158, price: 600, tier: 0, rare: 1, tip: "消除掉落伤害", bonus: new Bonuses { NoFallDamage = true }));
            list.Add(Acc(ItemType.Aglet, "鞋带束头", 285, price: 150, tier: 0, rare: 1, tip: "移动速度提高5%", bonus: new Bonuses { MoveSpeed = 0.05f }));
            list.Add(Acc(ItemType.AnkletoftheWind, "疾风脚镯", 212, price: 800, tier: 1, rare: 3, tip: "移动速度提高10%", bonus: new Bonuses { MoveSpeed = 0.1f }));
            list.Add(Acc(ItemType.FeralClaws, "猛爪手套", 211, price: 800, tier: 1, rare: 3, tip: "近战速度提高12%", bonus: new Bonuses { MeleeSpeed = 0.12f, AutoReuseGlove = true }));
            list.Add(Acc(ItemType.BandofRegeneration, "再生手环", 49, price: 500, tier: 0, rare: 1, tip: "缓慢再生生命", bonus: new Bonuses { LifeRegen = 2 }));
            list.Add(Acc(ItemType.BandofStarpower, "星力手环", 111, price: 600, tier: 1, rare: 1, tip: "最大魔力值增加40", bonus: new Bonuses { ManaMax = 40 }));
            list.Add(Acc(ItemType.ManaFlower, "魔力花", 555, price: 3000, tier: 2, rare: 4, tip: "魔力消耗降低8%\n需要时自动使用魔力药水", bonus: new Bonuses { ManaFlower = true }));
            list.Add(Acc(ItemType.CobaltShield, "钴护盾", 156, price: 1200, tier: 1, rare: 2, tip: "对击退免疫", defense: 1, bonus: new Bonuses { NoKnockback = true }));
            list.Add(Acc(ItemType.CrossNecklace, "十字项链", 554, price: 2000, tier: 2, rare: 4, tip: "延长受伤后的无敌状态时间", bonus: new Bonuses { LongImmunity = true }));
            list.Add(Acc(ItemType.MagicQuiver, "魔法箭袋", 1321, price: 2500, tier: 2, rare: 4, tip: "箭的伤害提高10%并大大提高箭的速度\n20%几率不消耗箭", bonus: new Bonuses { ArrowDamage = 0.1f, FastArrows = true }));
            list.Add(Acc(ItemType.WarriorEmblem, "战士徽章", 490, price: 3000, tier: 2, rare: 4, tip: "近战伤害提高15%", bonus: new Bonuses { MeleeDamage = 0.15f }));
            list.Add(Acc(ItemType.RangerEmblem, "游侠徽章", 491, price: 3000, tier: 2, rare: 4, tip: "远程伤害提高15%", bonus: new Bonuses { RangedDamage = 0.15f }));
            list.Add(Acc(ItemType.SorcererEmblem, "巫士徽章", 489, price: 3000, tier: 2, rare: 4, tip: "魔法伤害提高15%", bonus: new Bonuses { MagicDamage = 0.15f }));
            list.Add(Acc(ItemType.Flipper, "脚蹼", 187, price: 0, tier: 0, rare: 1, tip: "给予游泳能力\n可在酸水中游泳", bonus: new Bonuses { Swim = true }));
            // More accessories (spec-acc2; Item.SetDefaults and Player.ApplyEquipFunctional, zh-Hans names and tooltips).
            list.Add(Acc(ItemType.FrogLeg, "蛙腿", 2423, price: 1200, tier: 1, rare: 1, tip: "提高跳跃速度并可自动跳跃\n提高掉落抗性", bonus: new Bonuses { JumpSpeedBoost = 1.6f, AutoJump = true, ExtraFall = 10 }));
            list.Add(Acc(ItemType.SpectreBoots, "幽灵靴", 405, price: 3500, tier: 2, rare: 4, tip: "可飞行\n穿戴者可飞速奔跑", bonus: new Bonuses { AccRunSpeed = 6f, RocketKind = 2 }));
            list.Add(Acc(ItemType.LightningBoots, "闪电靴", 898, price: 6000, tier: 3, rare: 5, tip: "可飞行、可飞速奔跑\n移动速度提高8%", bonus: new Bonuses { AccRunSpeed = 6.75f, RocketKind = 2, MoveSpeed = 0.08f }));
            list.Add(Acc(ItemType.BlizzardinaBottle, "暴雪瓶", 987, price: 1200, tier: 1, rare: 1, tip: "可让持有者二连跳", bonus: new Bonuses { JumpBlizzard = true }));
            list.Add(Acc(ItemType.SandstorminaBottle, "沙暴瓶", 857, price: 1500, tier: 1, rare: 2, tip: "可让持有者更好地二连跳", bonus: new Bonuses { JumpSandstorm = true }));
            list.Add(Acc(ItemType.FartinaJar, "罐中臭屁", 1724, price: 1200, tier: 1, rare: 2, tip: "可让持有者二连跳", bonus: new Bonuses { JumpFart = true }));
            list.Add(Acc(ItemType.CloudinaBalloon, "云朵气球", 399, price: 3000, tier: 2, rare: 4, tip: "可让持有者二连跳\n增加跳跃高度", bonus: new Bonuses { JumpBoost = true, JumpCloud = true }));
            list.Add(Acc(ItemType.BundleofBalloons, "气球束", 1164, price: 6000, tier: 3, rare: 8, tip: "可让持有者四连跳\n增加跳跃高度",
                bonus: new Bonuses { JumpBoost = true, JumpCloud = true, JumpSandstorm = true, JumpBlizzard = true }));
            list.Add(Acc(ItemType.ShoeSpikes, "鞋钉", 975, price: 800, tier: 1, rare: 1, tip: "可沿墙滑下\n结合攀爬爪使用时能力还会有所提升", bonus: new Bonuses { SpikedBoots = 1 }));
            list.Add(Acc(ItemType.FledglingWings, "雏翼", 4978, price: 300, tier: 0, rare: 0, tip: "可飞行和缓慢坠落", bonus: new Bonuses { WingSlot = 46 }));
            list.Add(Acc(ItemType.AngelWings, "天使之翼", 493, price: 8000, tier: 3, rare: 5, tip: "可飞行和缓慢坠落", bonus: new Bonuses { WingSlot = 2 }));
            list.Add(Acc(ItemType.DemonWings, "恶魔之翼", 492, price: 8000, tier: 3, rare: 5, tip: "可飞行和缓慢坠落", bonus: new Bonuses { WingSlot = 1 }));
            list.Add(Acc(ItemType.ObsidianShield, "黑曜石护盾", 397, price: 3000, tier: 2, rare: 4, tip: "对击退免疫\n对火块免疫", defense: 2, bonus: new Bonuses { NoKnockback = true, FireWalk = true }));
            list.Add(Acc(ItemType.MagmaStone, "岩浆石", 1322, price: 2500, tier: 2, rare: 3, tip: "近战攻击造成火焰伤害", bonus: new Bonuses { MagmaStone = true }));
            list.Add(Acc(ItemType.SharkToothNecklace, "鲨牙项链", 3212, price: 1500, tier: 1, rare: 1, tip: "盔甲穿透力增加5", bonus: new Bonuses { ArmorPen = 5 }));
            list.Add(Acc(ItemType.PowerGlove, "强力手套", 897, price: 5000, tier: 3, rare: 5, tip: "提高近战击退力\n近战速度提高12%\n近战武器和鞭子可自动挥舞\n增大近战武器的尺寸",
                bonus: new Bonuses { KbGlove = true, AutoReuseGlove = true, MeleeScaleGlove = true, MeleeSpeed = 0.12f }));
            list.Add(Acc(ItemType.AvengerEmblem, "复仇者徽章", 935, price: 6000, tier: 3, rare: 5, tip: "伤害提高12%", bonus: new Bonuses { MeleeDamage = 0.12f, RangedDamage = 0.12f, MagicDamage = 0.12f, MinionDamage = 0.12f }));
            list.Add(Acc(ItemType.WormScarf, "蠕虫围巾", 3224, price: 3000, tier: 2, rare: -12, tip: "所受伤害降低17%", bonus: new Bonuses { Endurance = 0.17f }));
            list.Add(Acc(ItemType.StarCloak, "星星斗篷", 532, price: 3000, tier: 2, rare: 4, tip: "受到伤害后会使星星坠落", bonus: new Bonuses { StarCloak = true }));
            list.Add(Acc(ItemType.HoneyComb, "蜂窝", 1132, price: 2500, tier: 2, rare: 2, tip: "受到伤害后释放蜜蜂并将使用者浸入蜂蜜中", bonus: new Bonuses { HoneyComb = true }));
            list.Add(Acc(ItemType.PanicNecklace, "恐慌项链", 1290, price: 1500, tier: 1, rare: 1, tip: "受到伤害后提高移动速度", bonus: new Bonuses { Panic = true }));
            // The hardmode class accessories (accarmor-hm sec-G3).
            list.Add(Acc(ItemType.MechanicalGlove, "机械手套", 936, price: 25000, tier: 3, rare: 6, tip: "提高近战击退力\n近战伤害和速度各提高12%\n近战武器和鞭子可自动挥舞\n增大近战武器的尺寸",
                bonus: new Bonuses { KbGlove = true, MeleeScaleGlove = true, AutoReuseGlove = true, MeleeSpeed = 0.12f, MeleeDamage = 0.12f }));
            list.Add(Acc(ItemType.FireGauntlet, "烈火手套", 1343, price: 30000, tier: 3, rare: 7, tip: "提高近战击退力，近战攻击可造成火焰伤害\n近战伤害和速度各提高12%\n近战武器和鞭子可自动挥舞\n增大近战武器的尺寸",
                bonus: new Bonuses { KbGlove = true, MeleeScaleGlove = true, AutoReuseGlove = true, MeleeSpeed = 0.12f, MeleeDamage = 0.12f, MagmaStone = true }));
            list.Add(Acc(ItemType.BerserkerGlove, "狂战士手套", 3992, price: 50000, tier: 3, rare: 5, tip: "提高近战击退力\n近战速度提高12%\n近战武器和鞭子可自动挥舞\n增大近战武器的尺寸\n敌人更容易将你作为攻击目标", defense: 8,
                bonus: new Bonuses { KbGlove = true, MeleeScaleGlove = true, AutoReuseGlove = true, MeleeSpeed = 0.12f, Aggro = 400 }));
            list.Add(Acc(ItemType.FleshKnuckles, "血肉指虎", 3016, price: 40000, tier: 3, rare: 5, tip: "敌人更有可能以你为目标", defense: 8, bonus: new Bonuses { Aggro = 400 }));
            list.Add(Acc(ItemType.PutridScent, "腐香囊", 3015, price: 40000, tier: 3, rare: 6, tip: "敌人不太可能以你为目标\n伤害和暴击率各提高5%",
                bonus: new Bonuses { Aggro = -400, MeleeCrit = 0.05f, RangedCrit = 0.05f, MagicCrit = 0.05f, MeleeDamage = 0.05f, RangedDamage = 0.05f, MagicDamage = 0.05f, MinionDamage = 0.05f }));
            list.Add(Acc(ItemType.RifleScope, "步枪瞄准镜", 1300, price: 15000, tier: 3, rare: 4, tip: "扩大枪的视野范围\n右键可拉远视野", bonus: new Bonuses { Scope = true }));
            list.Add(Acc(ItemType.SniperScope, "狙击镜", 1858, price: 30000, tier: 3, rare: 7, tip: "扩大枪的视野范围（右键可拉远视野）\n远程伤害和远程暴击率各提高10%",
                bonus: new Bonuses { Scope = true, RangedDamage = 0.1f, RangedCrit = 0.1f }));
            list.Add(Acc(ItemType.ReconScope, "侦察镜", 4005, price: 50000, tier: 3, rare: 5, tip: "扩大枪的视野范围（右键可拉远视野）\n远程伤害和远程暴击率各提高10%\n敌人不太可能以你为目标\n“发现敌人”",
                bonus: new Bonuses { Scope = true, RangedDamage = 0.1f, RangedCrit = 0.1f, Aggro = -400 }));
            list.Add(Acc(ItemType.MoltenQuiver, "熔火箭袋", 4002, price: 37500, tier: 3, rare: 5, tip: "箭的伤害提高10%，箭的速度大大提高\n20%几率不消耗箭\n点燃木箭，火光熊熊\n“在恐惧中颤抖吧！”",
                bonus: new Bonuses { ArrowDamage = 0.1f, FastArrows = true, MoltenQuiver = true }));
            list.Add(Acc(ItemType.StalkersQuiver, "潜行者箭袋", 4006, price: 50000, tier: 3, rare: 5, tip: "箭的伤害提高10%，箭的速度大大提高\n20%几率不消耗箭\n敌人不太可能以你为目标",
                bonus: new Bonuses { ArrowDamage = 0.1f, FastArrows = true, Aggro = -400 }));
            list.Add(Acc(ItemType.ManaRegenerationBand, "魔力再生手环", 982, price: 5000, tier: 1, rare: 1, tip: "缓慢再生生命\n最大魔力值增加40\n提高魔力再生速度",
                bonus: new Bonuses { LifeRegen = 2, ManaMax = 40, ManaRegenBonus = 60 }));
            list.Add(Acc(ItemType.CelestialMagnet, "天界磁石", 2219, price: 15000, tier: 1, rare: 4, tip: "扩大魔力星的拾取范围", bonus: new Bonuses { ManaMagnet = true }));
            list.Add(Acc(ItemType.CelestialEmblem, "天界徽章", 2220, price: 16000, tier: 3, rare: 5, tip: "扩大魔力星的拾取范围\n魔法伤害提高15%", bonus: new Bonuses { ManaMagnet = true, MagicDamage = 0.15f }));
            list.Add(Acc(ItemType.CelestialCuffs, "天界手铐", 2221, price: 16000, tier: 2, rare: 5, tip: "扩大魔力星的拾取范围\n最大魔力值增加40\n受伤时恢复魔力", defense: 2,
                bonus: new Bonuses { ManaMagnet = true, ManaMax = 40, MagicCuffs = true }));
            list.Add(Acc(ItemType.ArcaneFlower, "奥术花", 3991, price: 50000, tier: 3, rare: 5, tip: "魔力消耗降低8%\n魔法伤害和暴击率各提高5%\n需要时自动使用魔力药水\n敌人较少将你作为攻击目标",
                bonus: new Bonuses { ManaFlower = true, MagicCrit = 0.05f, MagicDamage = 0.05f, Aggro = -400 }));
            list.Add(Acc(ItemType.ManaCloak, "魔力斗篷", 4001, price: 15000, tier: 3, rare: 5, tip: "魔力消耗降低8%\n需要时自动使用魔力药水\n受到伤害后会使星星坠落\n收集星星会恢复魔力",
                bonus: new Bonuses { ManaFlower = true, StarCloak = true, ManaCloak = true }));
            list.Add(Acc(ItemType.DestroyerEmblem, "毁灭者徽章", 1301, price: 30000, tier: 3, rare: 7, tip: "伤害提高10%\n暴击率提高8%",
                bonus: new Bonuses { MeleeCrit = 0.08f, RangedCrit = 0.08f, MagicCrit = 0.08f, MeleeDamage = 0.1f, RangedDamage = 0.1f, MagicDamage = 0.1f, MinionDamage = 0.1f }));
            list.Add(Acc(ItemType.EyeoftheGolem, "石巨人之眼", 1248, price: 25000, tier: 3, rare: 7, tip: "暴击率提高10%", bonus: new Bonuses { MeleeCrit = 0.1f, RangedCrit = 0.1f, MagicCrit = 0.1f }));
            const string dd2Tip = "哨兵数量上限增加1\n召唤伤害提高10%";
            list.Add(Acc(ItemType.ApprenticeScarf, "学徒围巾", 3809, price: 15000, tier: 3, rare: 5, tip: dd2Tip, bonus: new Bonuses { DD2Accessory = true }));
            list.Add(Acc(ItemType.SquireShield, "侍卫护盾", 3810, price: 15000, tier: 3, rare: 5, tip: dd2Tip, bonus: new Bonuses { DD2Accessory = true }));
            list.Add(Acc(ItemType.HuntressBuckler, "女猎人圆盾", 3811, price: 15000, tier: 3, rare: 5, tip: dd2Tip, bonus: new Bonuses { DD2Accessory = true }));
            list.Add(Acc(ItemType.MonkBelt, "武僧腰带", 3812, price: 15000, tier: 3, rare: 5, tip: dd2Tip, bonus: new Bonuses { DD2Accessory = true }));
            list.Add(Acc(ItemType.WickedClaws, "邪恶之爪", 6182, price: 7500, tier: 3, rare: 4, tip: "召唤标记效果上限增加1个\n提高鞭子的击退力\n近战速度提高12%\n近战武器和鞭子可自动挥舞",
                bonus: new Bonuses { WickedArmlet = true, AutoReuseGlove = true, MeleeSpeed = 0.12f }));
            list.Add(Acc(ItemType.RoyalGuardHarness, "皇家卫队护具", 6177, price: 7500, tier: 2, rare: 6, tip: "经过哨兵时可将其带走\n对击退免疫", defense: 1,
                bonus: new Bonuses { NoKnockback = true, SentryBackpack = true }));
            list.Add(Acc(ItemType.MagicCuffs, "魔法手铐", 1595, price: 2500, tier: 2, rare: 2, tip: "最大魔力值增加40\n受伤时恢复魔力", defense: 2, bonus: new Bonuses { ManaMax = 40, MagicCuffs = true }));
            // An information accessory: works anywhere in the inventory (UI.InfoAccessories).
            list.Add(Acc(ItemType.Radar, "雷达", 3084, price: 250, tier: 0, rare: 1, tip: "探测你周围的敌人"));
            list.Add(Acc(ItemType.PhilosophersStone, "点金石", 535, price: 3500, tier: 2, rare: 4, tip: "治疗药水的冷却时间缩短25%", bonus: new Bonuses { PStone = true }));

            // --- Consumables. ---
            list.Add(Potion(ItemType.LesserHealingPotion, "弱效治疗药水", 28, 50, 0, price: 30, tier: 0));
            list.Add(Potion(ItemType.HealingPotion, "治疗药水", 188, 100, 0, price: 80, tier: 1, rare: 1));
            list.Add(Potion(ItemType.LesserManaPotion, "弱效魔力药水", 110, 0, 50, price: 15, tier: 0));
            list.Add(Potion(ItemType.ManaPotion, "魔力药水", 189, 0, 100, price: 40, tier: 1, rare: 1));
            list.Add(new ItemDef { Type = ItemType.LifeCrystal, Name = "生命水晶", Texture = "Item_29", UseAnimation = 30 * Tick, UseTime = 30 * Tick, Consumable = true, LifeCrystal = true, MaxStack = 99, UseSound = "Item_4", Price = 500, Tier = 0, Rare = 2, Tooltip = "最大生命永久增加20" });
            list.Add(new ItemDef { Type = ItemType.ManaCrystal, Name = "魔力水晶", Texture = "Item_109", UseAnimation = 30 * Tick, UseTime = 30 * Tick, Consumable = true, ManaCrystal = true, MaxStack = 99, UseSound = "Item_29", Price = 200, Tier = 0, Rare = 2, Tooltip = "最大魔力永久增加20" });

            AddHardmode(list);
            AddAccessories2(list);
            AddAccessories3(list);
            AddArmorHM(list);
            AddYoyos(list);
            AddFlails(list);
            AddMelee2(list);
            AddTools(list);
            AddGenerated(list);
            AddGuns(list);
            AddR7(list);
            AddStations(list);
            list.Add(DreamNailItem.Def());
            list.Add(NailItem.Def());
            AddOres(list);
            MeteoriteBlock(list);
            AddMoney(list);
            AddCraftChain(list);
            AddCraftingSupplement(list);
            AddFishing(list);
            AddFood(list);
            AddPets(list);
            HkItems.AddItems(list);
            AddInfo(list);
            AddStations2(list);
            AddTombstones(list);
            AddAbilityAccessories(list);
            AddCarriedFurniture(list);
            AddArmor2(list);
            AddHeart(list);
            if (Relics.Enabled) AddRelics(list);
            TreasureBags.AddItems(list);
            AddWalls(list);
            BossSummons.AddItems(list);
            var arr = new ItemDef[System.Enum.GetValues(typeof(ItemType)).Length];
            foreach (var d in list) arr[(int)d.Type] = d;
            // Held-out items not given one by hand: Terraria's own hold (Main.DrawPlayerItemPos).
            foreach (var d in list)
                if (d.HoldOut && d.HoldOffset == new UnityEngine.Vector2(10f, 0f) && d.Texture != null && d.Texture.StartsWith("Item_")
                    && int.TryParse(d.Texture.Substring(5), out int id) && HoldOffsets.TryGetValue(id, out var hold))
                    d.HoldOffset = hold;
            foreach (var d in list)
                if (d.HoldOut && d.Texture != null && d.Texture.StartsWith("Item_") && int.TryParse(d.Texture.Substring(5), out int id) && StaffIds.Contains(id))
                    d.StaffHold = true;
            // Item.SetDefaults leaves autoReuse off for these: a click each.
            foreach (var t in new[]
            {
                ItemType.CopperShortsword, ItemType.CopperBroadsword, ItemType.IronBroadsword, ItemType.GoldBroadsword, ItemType.PlatinumBroadsword,
                ItemType.LightsBane, ItemType.BladeofGrass, ItemType.FieryGreatsword, ItemType.EnchantedBoomerang,
                ItemType.WoodenBow, ItemType.IronBow, ItemType.GoldBow, ItemType.DemonBow, ItemType.MoltenFury,
                ItemType.FlintlockPistol, ItemType.Handgun, ItemType.PhoenixBlaster, ItemType.AmethystStaff, ItemType.DemonScythe, ItemType.FlowerofFire,
                ItemType.Bomb, ItemType.Dynamite, ItemType.Grenade,
                ItemType.Starfury, ItemType.DarkLance, ItemType.Trident, ItemType.WoodenBoomerang, ItemType.Flamarang, ItemType.ThornChakram,
                ItemType.Musket, ItemType.Boomstick, ItemType.Shuriken, ItemType.ThrowingKnife, ItemType.MagicMissile, ItemType.Vilethorn, ItemType.Flamelash,
            })
                if (arr[(int)t] != null) arr[(int)t].AutoReuse = false;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] == null) arr[i] = new ItemDef { Type = (ItemType)i, Name = ((ItemType)i).ToString(), Texture = "Item_1" };
            // Stage, source and value (gen.py). The geo price follows the value: a geo per silver.
            foreach (var (t, stage, source, value) in System.Linq.Enumerable.Concat(System.Linq.Enumerable.Concat(System.Linq.Enumerable.Concat(System.Linq.Enumerable.Concat(Sources(), OreSources()), MoneySources()), ChainSources()), System.Linq.Enumerable.Concat(System.Linq.Enumerable.Concat(InfoSources(), System.Linq.Enumerable.Concat(Station2Sources(), System.Linq.Enumerable.Concat(TombstoneSources(), System.Linq.Enumerable.Concat(AbilityAccessorySources(), CarriedFurnitureSources())))), System.Linq.Enumerable.Concat(Armor2Sources(), System.Linq.Enumerable.Concat(System.Linq.Enumerable.Concat(HeartSources(), System.Linq.Enumerable.Concat(Relics.Enabled ? RelicSources() : System.Linq.Enumerable.Empty<(ItemType, int, ItemSource, int)>(), TreasureBags.Sources())), System.Linq.Enumerable.Concat(System.Linq.Enumerable.Concat(FishingSources(), FoodSources()), PetSources()))))))
            {
                var d = arr[(int)t];
                d.Stage = stage;
                d.Source = source;
                d.Value = value;
                d.Price = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.CeilToInt(value / 100f));
            }
            // 1.4.4: everything that stacks at all stacks to 9999 (Item.CommonMaxStack): blocks, torches, bombs, potions, crystals.
            foreach (var d in arr) if (d != null && d.MaxStack > 1) d.MaxStack = 9999;
            // Money stacks as far as a count goes.
            arr[(int)ItemType.Geo].MaxStack = int.MaxValue;
            // The Chaos Cylinder's other modes come only from toggling it: its stage, source and value.
            foreach (var t in new[] { ItemType.ChaosCylinderFull, ItemType.ChaosCylinderRandom, ItemType.ChaosCylinderSimple })
            {
                var c = arr[(int)ItemType.ChaosCylinder];
                arr[(int)t].Stage = c.Stage;
                arr[(int)t].Source = c.Source;
                arr[(int)t].Value = c.Value;
                arr[(int)t].Price = c.Price;
            }
            // What each accessory shows on the player (Item.SetDefaults: shoeSlot, waistSlot, balloonSlot, wingSlot, ...).
            void Show(ItemType t, int shoe = 0, int waist = 0, int balloon = 0, int wing = 0, int shield = 0, int neck = 0, int back = 0, int handOn = 0, int handOff = 0,
                int face = 0, int front = 0)
            {
                var d = arr[(int)t];
                d.Shoe = shoe; d.Waist = waist; d.Balloon = balloon; d.Wing = wing; d.Shield = shield; d.Neck = neck; d.Back = back; d.HandOn = handOn; d.HandOff = handOff;
                d.Face = face; d.Front = front;
            }
            Show(ItemType.HermesBoots, shoe: 6);
            Show(ItemType.CloudinaBottle, waist: 1);
            Show(ItemType.ClimbingClaws, handOn: 11, handOff: 6);
            Show(ItemType.ShieldOfCthulhu, shield: 5);
            Show(ItemType.RocketBoots, shoe: 12);
            Show(ItemType.ShinyRedBalloon, balloon: 8);
            Show(ItemType.FeralClaws, handOn: 5, handOff: 9);
            Show(ItemType.BandofStarpower, handOn: 3);
            Show(ItemType.ManaFlower, waist: 6);
            Show(ItemType.CobaltShield, shield: 1);
            Show(ItemType.CrossNecklace, neck: 2);
            Show(ItemType.MagicQuiver, back: 7);
            Show(ItemType.Flipper, shoe: 1);
            Show(ItemType.FrogLeg, shoe: 15);
            Show(ItemType.SpectreBoots, shoe: 13);
            Show(ItemType.LightningBoots, shoe: 10);
            Show(ItemType.BlizzardinaBottle, waist: 13);
            Show(ItemType.SandstorminaBottle, waist: 15);
            Show(ItemType.FartinaJar, waist: 14);
            Show(ItemType.CloudinaBalloon, balloon: 4);
            Show(ItemType.BundleofBalloons, balloon: 3);
            Show(ItemType.ShoeSpikes, shoe: 4);
            Show(ItemType.FledglingWings, wing: 46);
            Show(ItemType.AngelWings, wing: 2);
            Show(ItemType.DemonWings, wing: 1);
            Show(ItemType.ObsidianShield, shield: 3);
            Show(ItemType.SharkToothNecklace, neck: 7);
            Show(ItemType.PowerGlove, handOn: 10, handOff: 5);
            Show(ItemType.WormScarf, neck: 8);
            Show(ItemType.StarCloak, back: 2);
            Show(ItemType.PanicNecklace, neck: 3);
            Show(ItemType.MagicCuffs, handOn: 8, handOff: 3);
            Show(ItemType.MechanicalGlove, handOn: 9, handOff: 4);
            Show(ItemType.FireGauntlet, handOn: 6, handOff: 1);
            Show(ItemType.BerserkerGlove, handOn: 20, handOff: 12);
            Show(ItemType.MoltenQuiver, back: 15);
            Show(ItemType.StalkersQuiver, back: 16);
            Show(ItemType.ManaRegenerationBand, handOn: 1);
            Show(ItemType.CelestialCuffs, handOn: 17, handOff: 10);
            Show(ItemType.ManaCloak, back: 14, front: 5);
            Show(ItemType.ArcaneFlower, face: 9);
            Show(ItemType.ApprenticeScarf, neck: 9);
            Show(ItemType.SquireShield, shield: 6);
            Show(ItemType.HuntressBuckler, handOn: 19);
            Show(ItemType.MonkBelt, waist: 12);
            Show(ItemType.WickedClaws, handOn: 25, handOff: 16);
            Show(ItemType.RoyalGuardHarness, shield: 11);
            Show(ItemType.PygmyNecklace, neck: 4);
            Show(ItemType.Shackle, handOn: 12, handOff: 7);
            Show(ItemType.SandstorminaBalloon, balloon: 6);
            Show(ItemType.BlizzardinaBalloon, balloon: 1);
            Show(ItemType.TitanGlove, handOn: 15, handOff: 8);
            Show(ItemType.Blindfold, face: 5);
            Show(ItemType.ReflectiveShades, face: 20);
            Show(ItemType.AnkhShield, shield: 4);
            Show(ItemType.PaladinsShield, shield: 2);
            Show(ItemType.FrozenShield, shield: 7);
            Show(ItemType.HeroShield, shield: 8);
            Show(ItemType.SilverShield, shield: 10);
            Show(ItemType.RestorationShield, shield: 12);
            Show(ItemType.LavaCharm, handOn: 24);
            Show(ItemType.ObsidianRose, face: 6);
            Show(ItemType.ObsidianSkullRose, face: 13);
            Show(ItemType.LavaSkull, face: 10);
            Show(ItemType.MoltenSkullRose, face: 11);
            Show(ItemType.CharmofMyths, handOn: 4);
            Show(ItemType.SunStone, handOn: 13);
            Show(ItemType.MoonStone, handOn: 14);
            Show(ItemType.BeeCloak, back: 1);
            Show(ItemType.StarVeil, neck: 5);
            Show(ItemType.SweetheartNecklace, neck: 6);
            Show(ItemType.StingerNecklace, neck: 10);
            Show(ItemType.CrossedHeartNecklace, neck: 13);
            Show(ItemType.SeraphNecklace, neck: 14);
            Show(ItemType.DruidicSerpentCloak, back: 41, front: 17);
            Show(ItemType.BoneGlove, handOn: 22, handOff: 14);
            Show(ItemType.HiveBackpack, back: 9);
            Show(ItemType.BoneHelm, face: 19);
            Show(ItemType.HandWarmer, handOn: 7, handOff: 2);
            Show(ItemType.DivingGear, face: 4);
            Show(ItemType.JellyfishNecklace, neck: 1);
            Show(ItemType.JellyfishDivingGear, face: 3);
            Show(ItemType.ArcticDivingGear, face: 2);
            Show(ItemType.TreasureMagnet, waist: 16);
            Show(ItemType.SpectreGoggles, face: 14);
            Show(ItemType.SilverBracer, handOn: 27, handOff: 17);
            Show(ItemType.SnakeBand, handOn: 28);
            Show(ItemType.WickedArmlet, handOn: 29);
            Show(ItemType.OuroborosRing, handOn: 30);
            Show(ItemType.TwilightGrasp, handOn: 31, handOff: 18);
            Show(ItemType.ArmletOfRuin, handOn: 32);
            Show(ItemType.PhoenixQuiver, back: 40);
            Show(ItemType.CatalystBand, handOn: 26);
            Show(ItemType.MysticArtsSash, waist: 17);
            Show(ItemType.WaterWalkingBoots, shoe: 2);
            Show(ItemType.ObsidianWaterWalkingBoots, shoe: 11);
            Show(ItemType.MoltenCharm, neck: 12);
            Show(ItemType.LavaWaders, shoe: 8);
            Show(ItemType.IceSkates, shoe: 7);
            Show(ItemType.FlurryBoots, shoe: 5);
            Show(ItemType.SailfishBoots, shoe: 17);
            Show(ItemType.SandBoots, shoe: 21);
            Show(ItemType.FairyBoots, shoe: 19);
            Show(ItemType.FlowerBoots, shoe: 16);
            Show(ItemType.AmphibianBoots, shoe: 18);
            Show(ItemType.FrogFlipper, shoe: 20);
            Show(ItemType.FrogGear, shoe: 20, handOn: 21, handOff: 13);
            Show(ItemType.FrogWebbing, handOn: 21, handOff: 13);
            Show(ItemType.FrostsparkBoots, shoe: 9);
            Show(ItemType.TerrasparkBoots, shoe: 24);
            Show(ItemType.HellfireTreads, shoe: 23);
            Show(ItemType.FlameWakerBoots, shoe: 22);
            Show(ItemType.TigerClimbingGear, shoe: 4, handOn: 11, handOff: 6);
            Show(ItemType.Tabi, shoe: 3);
            Show(ItemType.BlackBelt, waist: 10);
            Show(ItemType.MasterNinjaGear, shoe: 14, waist: 10, handOn: 11, handOff: 6);
            Show(ItemType.HoneyBalloon, balloon: 7);
            Show(ItemType.BlueHorseshoeBalloon, balloon: 2);
            Show(ItemType.WhiteHorseshoeBalloon, balloon: 9);
            Show(ItemType.YellowHorseshoeBalloon, balloon: 10);
            Show(ItemType.FartInABalloon, balloon: 5);
            Show(ItemType.BalloonPufferfish, balloon: 11);
            Show(ItemType.SharkronBalloon, balloon: 12);
            Show(ItemType.BalloonHorseshoeFart, balloon: 13);
            Show(ItemType.BalloonHorseshoeHoney, balloon: 14);
            Show(ItemType.BalloonHorseshoeSharkron, balloon: 15);
            Show(ItemType.TsunamiInABottle, waist: 11);
            Show(ItemType.HorseshoeBundle, balloon: 19);
            Show(ItemType.ShimmerCloak, back: 36, front: 12);
            Show(ItemType.Magiluminescence, neck: 11);
            Show(ItemType.YoYoGlove, handOn: 18, handOff: 11);
            // Every pair of wings: its slot (one pair at a time; they're drawn by TerraWings).
            foreach (var w in arr)
                if (w.Bonus.WingSlot > 0 && w.Wing == 0) w.Wing = w.Bonus.WingSlot;

            // Crafting classes: weapons by their damage class; armor and accessories that only help
            // one class go with it; blocks, tools, potions, ammo and everything else are General.
            foreach (var d in arr)
            {
                bool tool = d.PickPower > 0 || d.Places != CellKind.Empty || d.HealLife > 0 || d.HealMana > 0 || d.LifeCrystal || d.ManaCrystal || d.IsAmmo;
                d.Kit = tool || !d.IsWeapon ? Kit.General
                      : d.Class == DamageClass.Melee ? Kit.Melee : d.Class == DamageClass.Ranged ? Kit.Ranged
                      : d.Class == DamageClass.Magic ? Kit.Magic : d.Class == DamageClass.Summon ? Kit.Summon : Kit.General;
            }
            void Class(Kit kit, params ItemType[] types)
            {
                foreach (var t in types) arr[(int)t].Kit = kit;
            }
            Class(Kit.Melee, ItemType.ShadowHelmet, ItemType.ShadowScalemail, ItemType.ShadowGreaves, ItemType.MoltenHelmet, ItemType.MoltenBreastplate, ItemType.MoltenGreaves,
                ItemType.FeralClaws, ItemType.PowerGlove, ItemType.WarriorEmblem, ItemType.MagmaStone, ItemType.TitanGlove,
                ItemType.MechanicalGlove, ItemType.FireGauntlet, ItemType.BerserkerGlove, ItemType.FleshKnuckles);
            Class(Kit.Ranged, ItemType.NecroHelmet, ItemType.NecroBreastplate, ItemType.NecroGreaves, ItemType.MagicQuiver, ItemType.RangerEmblem,
                ItemType.RifleScope, ItemType.SniperScope, ItemType.ReconScope, ItemType.MoltenQuiver, ItemType.StalkersQuiver,
                ItemType.HarpyCharm, ItemType.PhoenixQuiver, ItemType.PoisonBarb, ItemType.SweetBarb, ItemType.SnappingStone, ItemType.SeraphNecklace,
                ItemType.ChaosCylinder, ItemType.ChaosCylinderFull, ItemType.ChaosCylinderRandom, ItemType.ChaosCylinderSimple);
            Class(Kit.Magic, ItemType.JungleHat, ItemType.JungleShirt, ItemType.JunglePants, ItemType.BandofStarpower, ItemType.ManaFlower, ItemType.SorcererEmblem, ItemType.MagicCuffs, ItemType.NaturesGift,
                ItemType.ManaRegenerationBand, ItemType.CelestialMagnet, ItemType.CelestialEmblem, ItemType.CelestialCuffs, ItemType.ArcaneFlower, ItemType.ManaCloak,
                ItemType.MysticArtsSash);
            Class(Kit.Summon, ItemType.FlinxFurCoat, ItemType.BeeHeadgear, ItemType.BeeBreastplate, ItemType.BeeGreaves, ItemType.ObsidianOutlawHat, ItemType.ObsidianLongcoat,
                ItemType.ObsidianPants, ItemType.SpiderMask, ItemType.SpiderBreastplate, ItemType.SpiderGreaves, ItemType.PygmyNecklace, ItemType.PapyrusScarab, ItemType.SummonerEmblem,
                ItemType.NecromanticScroll, ItemType.HerculesBeetle,
                ItemType.ApprenticeScarf, ItemType.SquireShield, ItemType.HuntressBuckler, ItemType.MonkBelt, ItemType.WickedClaws, ItemType.RoyalGuardHarness,
                ItemType.SilverBracer, ItemType.SnakeBand, ItemType.MobiusStrip, ItemType.WickedArmlet, ItemType.OuroborosRing, ItemType.TwilightGrasp, ItemType.ArmletOfRuin,
                ItemType.SilverShield, ItemType.DruidicSerpentCloak, ItemType.HeavySling, ItemType.ScoutSling, ItemType.TemplarSling);
            foreach (var (t, kit) in ArmorKits()) arr[(int)t].Kit = kit;
            ProgressionBalance.ApplyItems(arr);
            ProgressionBalance.NormalizeRecipeStages(arr, Recipes.BaseRecipes());
            ProgressionBalance.ApplyWeaponBalance(arr);
            return arr;
        }

        static ItemDef Sword(ItemType t, string name, int id, int dmg, float kb, int use, int price, int tier, int rare = 0, string tip = null, MeleeFx fx = MeleeFx.None, bool noMelee = false) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Melee, Damage = dmg, Knockback = kb,
            UseAnimation = use * Tick, UseTime = use * Tick, Price = price, Tier = tier, Rare = rare, Tooltip = tip, Fx = fx, NoMelee = noMelee,
        };

        /// <summary>A sword that also shoots: its useTime (ticks) longer than its swing.</summary>
        static ItemDef Shooter(ItemDef d, int useTicks, ProjKind proj, float speed, float scale = 1f)
        {
            d.UseTime = useTicks * Tick;
            d.Shoot = proj;
            d.ShootSpeed = speed;
            d.Scale = scale;
            return d;
        }

        /// <summary>useStyle 5 with a spear projectile: the item itself isn't drawn, the arm holds out along the thrust.</summary>
        static ItemDef Spear(ItemType t, string name, int id, int dmg, float kb, int use, ProjKind proj, float speed, int price, int tier, int rare = 0, string tip = null) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Melee, Damage = dmg, Knockback = kb, UseAnimation = use * Tick, UseTime = use * Tick,
            HoldOut = true, NoMelee = true, NoUseGraphic = true, Shoot = proj, ShootSpeed = speed, Scale = 1.1f, Price = price, Tier = tier, Rare = rare, Tooltip = tip,
        };

        /// <summary>Thrown from a swing with nothing in the hand (boomerangs, shuriken, knives).</summary>
        static ItemDef Thrown(ItemType t, string name, int id, DamageClass cls, int dmg, float kb, int use, ProjKind proj, float speed, int price, int tier, int rare = 0, string tip = null) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, Class = cls, Damage = dmg, Knockback = kb, UseAnimation = use * Tick, UseTime = use * Tick,
            NoMelee = true, NoUseGraphic = true, Shoot = proj, ShootSpeed = speed, Price = price, Tier = tier, Rare = rare, Tooltip = tip,
        };

        /// <summary>A summon staff: swung (useStyle 1, no hitbox), autoReuse, no mana in 1.4.5.</summary>
        static ItemDef Staff2(ItemType t, string name, int id, int dmg, float kb, int use, MinionKind minion, int buff, string sound, int price, int tier, int rare, string tip) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Summon, Damage = dmg, Knockback = kb, UseAnimation = use * Tick, UseTime = use * Tick,
            NoMelee = true, Minion = minion, MinionBuff = buff, UseSound = sound, Price = price, Tier = tier, Rare = rare, Tooltip = tip,
        };

        /// <summary>A whip (DefaultToWhip): swung with only the whip drawn, no autoReuse.</summary>
        /// <summary>An Old One's Army sentry staff: swung (no hitbox), one sentry a click (no autoReuse), no mana or buff.</summary>
        static ItemDef SentryStaff(ItemType t, string name, int id, int dmg, float kb, MinionKind minion, int rare, string tip, string sound = "Custom/dd2_defense_tower_spawn") => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Summon, Damage = dmg, Knockback = kb, UseAnimation = 30 * Tick, UseTime = 30 * Tick,
            NoMelee = true, Minion = minion, Sentry = true, AutoReuse = false, UseSound = sound, Tier = 3, Rare = rare, Tooltip = tip,
        };

        static ItemDef Whip(ItemType t, string name, int id, int dmg, float kb, float speed, int anim, WhipKind whip, int price, int tier, int rare, string tip) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Summon, Damage = dmg, Knockback = kb, UseAnimation = anim * Tick, UseTime = anim * Tick,
            NoMelee = true, NoUseGraphic = true, Whip = whip, ShootSpeed = speed, UseSound = "Item_152", AutoReuse = false, Price = price, Tier = tier, Rare = rare, Tooltip = tip,
        };

        static ItemDef Pick(ItemType t, string name, int id, int power, int dmg, int anim, int use, float scale, int price, int tier, int rare = 0) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Melee, Damage = dmg, Knockback = 2f, PickPower = power,
            UseAnimation = anim * Tick, UseTime = use * Tick, Scale = scale, Price = price, Tier = tier, Rare = rare,
        };

        static ItemDef Bow(ItemType t, string name, int id, int dmg, float kb, int use, float speed, int price, int tier, int rare = 0, string tip = null) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Ranged, Damage = dmg, Knockback = kb, UseAnimation = use * Tick, UseTime = use * Tick,
            HoldOut = true, NoMelee = true, Shoot = ProjKind.WoodenArrow, ShootSpeed = speed, UseAmmo = AmmoKind.Arrow, UseSound = "Item_5",
            Price = price, Tier = tier, Rare = rare, Tooltip = tip,
        };

        static ItemDef Held(ItemDef d, float x, float y)
        {
            d.HoldOffset = new UnityEngine.Vector2(x, y);
            return d;
        }

        static ItemDef Gun(ItemType t, string name, int id, int dmg, float kb, int use, float speed, string sound, int price, int tier, int crit = 0, int rare = 0, string tip = null, float scale = 1f) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Ranged, Damage = dmg, Knockback = kb, Crit = crit, UseAnimation = use * Tick, UseTime = use * Tick,
            HoldOut = true, NoMelee = true, Shoot = ProjKind.Bullet, ShootSpeed = speed, UseAmmo = AmmoKind.Bullet, UseSound = sound,
            Price = price, Tier = tier, Rare = rare, Tooltip = tip, Scale = scale,
        };

        /// <summary>An arrow or bullet: 9999 a stack (Item.CommonMaxStack); the Endless Quiver and Musket Pouch are one, never used up.</summary>
        static ItemDef Ammo(ItemType t, string name, int id, AmmoKind kind, ProjKind proj, int dmg, float kb, float speed, int price, int tier, string tip = null,
            int rare = 0, bool endless = false) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Ranged, Damage = dmg, Knockback = kb, Ammo = kind, Shoot = proj, ShootSpeed = speed,
            MaxStack = endless ? 1 : 9999, Consumable = !endless, Price = price, Tier = tier, Rare = rare, Tooltip = tip ?? (endless ? "弹药，永不耗尽" : "弹药"),
        };

        static ItemDef Staff(ItemType t, string name, int id, int dmg, float kb, int use, float speed, int mana, ProjKind proj, string sound, int price, int tier,
            bool hold = true, int crit = 0, int rare = 0, string tip = null, float scale = 1f) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Magic, Damage = dmg, Knockback = kb, Crit = crit, UseAnimation = use * Tick, UseTime = use * Tick,
            HoldOut = hold, NoMelee = true, Shoot = proj, ShootSpeed = speed, Mana = mana, UseSound = sound,
            Price = price, Tier = tier, Rare = rare, Tooltip = tip, Scale = scale,
        };

        static ItemDef Armor(ItemType t, string name, int id, ArmorSlot slot, int sheet, int defense, ArmorSet set, int price, int tier, int rare = 0, string tip = null, Bonuses bonus = default) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, Armor = slot, ArmorSheet = sheet, Defense = defense, Set = set, Bonus = bonus,
            Price = price, Tier = tier, Rare = rare, Tooltip = tip,
        };

        static ItemDef Acc(ItemType t, string name, int id, int price, int tier, int rare = 0, string tip = null, int defense = 0, Bonuses bonus = default) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, Accessory = AccessoryEffect.Passive, Defense = defense, Bonus = bonus,
            Price = price, Tier = tier, Rare = rare, Tooltip = tip,
        };

        static ItemDef Potion(ItemType t, string name, int id, int life, int mana, int price, int tier, int rare = 0) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, HealLife = life, HealMana = mana, Consumable = true, MaxStack = 30,
            UseAnimation = 17 * Tick, UseTime = 17 * Tick, UseSound = "Item_3", Price = price, Tier = tier, Rare = rare, PotionSick = life > 0,
            Tooltip = life > 0 ? $"恢复 {life} 生命值\n会造成药水病（60 秒内不能再喝）" : $"恢复 {mana} 魔力值",
        };

        public static ItemDef Get(ItemType type) => defs[(int)type];

        /// <summary>A known item (one from another player's game is checked before it is looked up).</summary>
        public static bool Has(ItemType type) => (int)type > 0 && (int)type < defs.Length && defs[(int)type] != null;

        public static System.Collections.Generic.IEnumerable<ItemDef> All
        {
            get { foreach (var d in defs) if (d.Type != ItemType.None) yield return d; }
        }

        /// <summary>The item a mined cell drops. The scene's own terrain comes out as stone.</summary>
        public static ItemType DropFor(CellKind kind)
        {
            switch (kind)
            {
                case CellKind.Original:
                case CellKind.Stone: return ItemType.StoneBlock;
                case CellKind.Dirt: return ItemType.DirtBlock;
                case CellKind.Wood: return ItemType.Wood;
                case CellKind.Sand: return ItemType.SandBlock;
                case CellKind.Platform: return ItemType.WoodPlatform;
                case CellKind.Torch: return ItemType.Torch;
                case CellKind.Rope: return ItemType.Rope;
                case CellKind.Meteorite: return ItemType.Meteorite;
                default: return ItemType.None;
            }
        }

        /// <summary>
        /// Player.GetPickaxeDamage: dirt and sand take the pick's power twice; torches (tileNoFail)
        /// and rope (tileRope) break at once; wood and platforms are ordinary tiles (a copper pick
        /// three hits, a gold one two).
        /// </summary>
        public static int Hardness(CellKind kind)
        {
            switch (kind)
            {
                case CellKind.Dirt:
                case CellKind.Sand: return 2;
                case CellKind.Torch:
                case CellKind.Rope: return 100;
                default: return 1;
            }
        }

        /// <summary>Terraria's rarity colours for item names.</summary>
        public static UnityEngine.Color RarityColor(int rare)
        {
            switch (rare)
            {
                case 1: return new UnityEngine.Color(150 / 255f, 150 / 255f, 1f);
                case 2: return new UnityEngine.Color(150 / 255f, 1f, 150 / 255f);
                case 3: return new UnityEngine.Color(1f, 200 / 255f, 150 / 255f);
                case 4: return new UnityEngine.Color(1f, 150 / 255f, 150 / 255f);
                case 5: return new UnityEngine.Color(1f, 150 / 255f, 1f);
                case 6: return new UnityEngine.Color(210 / 255f, 160 / 255f, 1f);
                case 7: return new UnityEngine.Color(150 / 255f, 1f, 10 / 255f);
                case 8: return new UnityEngine.Color(1f, 1f, 10 / 255f);
                case 9: return new UnityEngine.Color(5 / 255f, 200 / 255f, 1f);
                case 10: return new UnityEngine.Color(1f, 40 / 255f, 100 / 255f);
                // Quest items (rare -11): Colors.RarityAmber.
                case -11: return new UnityEngine.Color(1f, 175 / 255f, 0f);
                // Expert (rare -12): Main.DiscoColor, cycling through the rainbow.
                case -12: return UnityEngine.Color.HSVToRGB(UnityEngine.Time.unscaledTime * 0.2f % 1f, 1f, 1f);
                // Master (rare -13): Main.masterColor, red to gold and back (±0.05 a tick, green 200 at most).
                case -13: return new UnityEngine.Color(1f, UnityEngine.Mathf.PingPong(UnityEngine.Time.unscaledTime * 3f, 1f) * 200f / 255f, 0f);
                default: return UnityEngine.Color.white;
            }
        }
    }

    internal struct ItemStack
    {
        public ItemType Type;
        public int Count;
        /// <summary>Terraria's favourite mark (Alt+click): kept out of the trash and the shop.</summary>
        public bool Favorite;
        /// <summary>Its prefix (PrefixID, a reforge's: Prefixes), 0 none; its definition is then the prefixed copy.</summary>
        public byte Prefix;
        /// <summary>Its forge level (the Goblin Tinkerer's forging: Forging), 0 none; its definition is then the forged copy.</summary>
        public byte Forge;
        public bool IsEmpty => Type == ItemType.None || Count <= 0;
        public ItemDef Def => IsEmpty ? ItemDb.Get(ItemType.None) : Forging.Apply(Prefix == 0 ? ItemDb.Get(Type) : Prefixes.Apply(ItemDb.Get(Type), Prefix), Forge);

        public static ItemStack Of(ItemType type, int count = 1) => new ItemStack { Type = type, Count = count };
    }

    /// <summary>
    /// Terraria's inventory: 50 slots whose first row is the hotbar, three armor slots, five
    /// accessory slots, the trash slot, and the item held on the mouse cursor while rearranging.
    /// With the inventory open, an item held on the cursor is the one used when clicking in the
    /// world (Terraria's selectedItem 58).
    /// </summary>
    internal sealed class Inventory
    {
        public const int Columns = 10;
        public const int Rows = 5;
        public const int Size = Columns * Rows;
        public const int HotbarSize = Columns;
        /// <summary>
        /// Terraria's four ammo slots (inventory 54-57, beside the coins'), kept after the bag in
        /// <see cref="Slots"/>: Size to Total - 1, top to bottom.
        /// </summary>
        public const int AmmoSlots = 4;
        public const int AmmoEnd = Size + AmmoSlots;
        /// <summary>Terraria's coin slots (inventory 50-53), one of them: the Geo, the only money, is one stack. After the ammo slots.</summary>
        public const int CoinSlots = 1;
        public const int CoinSlot = AmmoEnd;
        public const int Total = AmmoEnd + CoinSlots;
        public static bool IsAmmoSlot(int i) => i >= Size && i < AmmoEnd;
        public static bool IsCoinSlot(int i) => i == CoinSlot;
        /// <summary>ItemSlot's InventoryCoin context: money only.</summary>
        public static bool FitsCoinSlot(ItemDef d) => d != null && d.Type == ItemType.Geo;

        /// <summary>Item.FitsAmmoSlot: what an ammo slot takes (ammo of any kind, the Ale too).</summary>
        public static bool FitsAmmoSlot(ItemDef d) => d != null && (d.Ammo != AmmoKind.None || d.BaitPower > 0);

        /// <summary>
        /// Item.CanFillEmptyAmmoSlot: a pickup goes into an empty ammo slot by itself when it is ammo
        /// first of all; sand, Fallen Stars, gel and Ale (mostly something else) only top up a stack
        /// already there.
        /// </summary>
        public static bool FillsEmptyAmmoSlot(ItemDef d) => FitsAmmoSlot(d) && d.BaitPower <= 0 && d.Type != ItemType.SandBlock && d.Type != ItemType.FallenStar &&
            d.Type != ItemType.Gel && d.Type != ItemType.PearlsandBlock && d.Type != ItemType.EbonsandBlock && d.Type != ItemType.CrimsandBlock && d.Type != ItemType.Ale;
        /// <summary>
        /// Five accessory slots, Master Mode's own one more (Player.IsItemSlotUnlockedAndUsable: slot 9,
        /// Main.masterMode) and the Demon Heart's (Player.extraAccessory; locked until it is eaten).
        /// </summary>
        public const int AccessorySlots = 7;
        /// <summary>The slots in use: the sixth once the Demon Heart is eaten (Player.IsItemSlotUnlockedAndUsable).</summary>
        public static int OpenAccessories =>
            5 + (World.GameDifficulty.Current == World.DifficultyLevel.Master ? 1 : 0)
              + (TerraMod.Instance != null && TerraMod.Instance.Stats.Boosts.Contains(PermanentBoost.DemonHeart) ? 1 : 0);
        public const int ArmorSlots = 3;

        /// <summary>The bag (0 to Size - 1, the hotbar first) and the ammo slots after it.</summary>
        public readonly ItemStack[] Slots = new ItemStack[Total];
        /// <summary>The worn set (Player.armor): what the equipment column shows and changes.</summary>
        public readonly ItemStack[] Accessories = new ItemStack[AccessorySlots];
        /// <summary>Head, body, legs.</summary>
        public readonly ItemStack[] Armor = new ItemStack[ArmorSlots];

        /// <summary>Player.miscEquips: pet, light pet, minecart, mount, hook (the equipment's second page). Shared by every loadout.</summary>
        public const int MiscSlots = 5, HookSlot = 4;
        public readonly ItemStack[] Misc = new ItemStack[MiscSlots];

        /// <summary>Player.bank: the Piggy Bank's 40 slots, the same in every one set down.</summary>
        public const int BankSlots = 40;
        public readonly ItemStack[] Bank = new ItemStack[BankSlots];

        /// <summary>The Hallownest bag: Hallownest's key items not in the hotbar (HkItems).</summary>
        public readonly ItemStack[] HkBag = new ItemStack[HkItems.BagSlots];

        /// <summary>ItemSlot.AccCheck for the misc slots (contexts 19, 20, 18, 17, 16): a pet, a light pet, a hook (no minecarts or mounts yet).</summary>
        public static bool FitsMisc(ItemDef d, int slot) => d != null && (slot == HookSlot ? Player.GrapplingHook.Hooks.ContainsKey(d.Type)
            : slot == Pets.PetSlot ? d.PetBuff > 0 && !d.LightPet : slot == Pets.LightSlot && d.PetBuff > 0 && d.LightPet);

        /// <summary>The misc slot an item is worn in (a right click puts it there), -1 none.</summary>
        public static int MiscSlotOf(ItemDef d)
        {
            for (int k = 0; k < MiscSlots; k++) if (FitsMisc(d, k)) return k;
            return -1;
        }

        /// <summary>Four loadouts, one per class (Melee, Ranged, Magic, Summon); vanilla has three.</summary>
        public const int LoadoutCount = 4;

        /// <summary>A set not worn now (EquipmentLoadout). The worn one's own entry holds empties.</summary>
        public sealed class Loadout
        {
            public readonly ItemStack[] Armor = new ItemStack[ArmorSlots];
            public readonly ItemStack[] Accessories = new ItemStack[AccessorySlots];
            /// <summary>Creative (BagPerLoadout): the loadout's own bag and hotbar pick, kept here while another is worn.</summary>
            public readonly ItemStack[] Bag = new ItemStack[Total];
            public int Selected;
        }

        /// <summary>
        /// Creative: every loadout has a bag of its own, switched along with its armor and accessories
        /// (the bags share nothing). A normal save has the one bag for all four.
        /// </summary>
        public bool BagPerLoadout;

        public readonly Loadout[] Loadouts = { new Loadout(), new Loadout(), new Loadout(), new Loadout() };
        public int CurrentLoadout;
        /// <summary>The crafting class of the worn loadout.</summary>
        public Kit LoadoutKit => (Kit)(CurrentLoadout + 1);

        /// <summary>
        /// What is in effect (Player.GetEffectiveArmor): the worn item, or in an empty worn slot an
        /// item a stored loadout shares into it (its favourite mark), and which loadout (-1 none).
        /// </summary>
        public readonly ItemStack[] EffectiveArmor = new ItemStack[ArmorSlots], EffectiveAccessories = new ItemStack[AccessorySlots];
        public readonly int[] ArmorSharedFrom = new int[ArmorSlots], AccessorySharedFrom = new int[AccessorySlots];

        /// <summary>
        /// Player.TrySwitchingLoadout: the worn set goes into its loadout's storage and the other
        /// comes out (two swaps). False for the loadout already worn or one that doesn't exist.
        /// </summary>
        public bool SwitchLoadout(int index)
        {
            if (index == CurrentLoadout || index < 0 || index >= LoadoutCount) return false;
            Swap(Loadouts[CurrentLoadout]);
            Swap(Loadouts[index]);
            CurrentLoadout = index;
            if (BagPerLoadout) Override = -1;
            RefreshEffective();
            return true;
        }

        void Swap(Loadout l)
        {
            for (int i = 0; i < ArmorSlots; i++) (Armor[i], l.Armor[i]) = (l.Armor[i], Armor[i]);
            for (int i = 0; i < AccessorySlots; i++) (Accessories[i], l.Accessories[i]) = (l.Accessories[i], Accessories[i]);
            if (!BagPerLoadout) return;
            // The money stays the player's whatever the loadout.
            for (int i = 0; i < Total; i++) if (i != CoinSlot) (Slots[i], l.Bag[i]) = (l.Bag[i], Slots[i]);
            (Selected, l.Selected) = (l.Selected, Selected);
        }

        /// <summary>
        /// Loadout Share (1.4.5): an empty worn slot takes the favourited item in the same slot of
        /// the first stored loadout that has one, when it could be worn beside the rest
        /// (CanShareArmor: no second of the same accessory, never two pairs of wings; lower slots
        /// win). A favourited worn item is one being shared out.
        /// </summary>
        public void RefreshEffective()
        {
            for (int i = 0; i < ArmorSlots; i++)
            {
                EffectiveArmor[i] = Armor[i];
                ArmorSharedFrom[i] = -1;
                if (!Armor[i].IsEmpty) continue;
                for (int l = 0; l < LoadoutCount; l++)
                {
                    var s = Loadouts[l].Armor[i];
                    if (s.IsEmpty || !s.Favorite) continue;
                    EffectiveArmor[i] = s;
                    ArmorSharedFrom[i] = l;
                    break;
                }
            }
            int open = OpenAccessories;
            for (int i = 0; i < AccessorySlots; i++)
            {
                EffectiveAccessories[i] = i < open ? Accessories[i] : default;
                AccessorySharedFrom[i] = -1;
                if (!Accessories[i].IsEmpty || i >= open) continue;
                for (int l = 0; l < LoadoutCount; l++)
                {
                    var s = Loadouts[l].Accessories[i];
                    if (s.IsEmpty || !s.Favorite) continue;
                    if (CanShareAccessory(s, i))
                    {
                        EffectiveAccessories[i] = s;
                        AccessorySharedFrom[i] = l;
                    }
                    break;
                }
            }
        }

        bool CanShareAccessory(ItemStack item, int slot)
        {
            for (int k = 0; k < AccessorySlots; k++)
            {
                if (k == slot) continue;
                // Against everything worn, and against what lower empty slots already take in.
                var other = !Accessories[k].IsEmpty ? Accessories[k] : k < slot && AccessorySharedFrom[k] >= 0 ? EffectiveAccessories[k] : default;
                if (other.IsEmpty) continue;
                if (other.Type == item.Type || (other.Def.Wing > 0 && item.Def.Wing > 0)) return false;
            }
            return true;
        }
        public ItemStack Mouse;
        /// <summary>The last item thrown away; the next one replaces it.</summary>
        public ItemStack Trash;
        public int Selected;
        /// <summary>Terraria's selection override (Auto Select, Shift held): the slot in hand instead of the selected one while it lasts, -1 for none.</summary>
        public int Override = -1;
        /// <summary>The slot in hand: the override's while there is one, else the selected one.</summary>
        public int Held => Override >= 0 && Override < Size && !Slots[Override].IsEmpty ? Override : Selected;
        public bool Infinite;
        /// <summary>The inventory is open and an item is on the cursor: that item is the one in use.</summary>
        public bool UseMouse;

        internal bool UsingMouse => UseMouse && !Mouse.IsEmpty;

        public ItemStack Current => UsingMouse ? Mouse : Slots[Held];

        public void Clear()
        {
            for (int i = 0; i < Total; i++) Slots[i] = default;
            for (int i = 0; i < AccessorySlots; i++) Accessories[i] = default;
            for (int i = 0; i < ArmorSlots; i++) Armor[i] = default;
            for (int i = 0; i < MiscSlots; i++) Misc[i] = default;
            for (int i = 0; i < BankSlots; i++) Bank[i] = default;
            for (int i = 0; i < HkBag.Length; i++) HkBag[i] = default;
            foreach (var l in Loadouts)
            {
                for (int i = 0; i < ArmorSlots; i++) l.Armor[i] = default;
                for (int i = 0; i < AccessorySlots; i++) l.Accessories[i] = default;
                for (int i = 0; i < Total; i++) l.Bag[i] = default;
                l.Selected = 0;
            }
            CurrentLoadout = 0;
            Mouse = default;
            Trash = default;
            Selected = 0;
            Override = -1;
            RefreshEffective();
        }

        /// <summary>
        /// Adds to existing stacks, then the first empty slot (hotbar first). All or nothing:
        /// false, with nothing moved, when they don't all fit.
        /// </summary>
        public bool Add(ItemType type, int count = 1)
        {
            if (Room(type) < count) return false;
            return AddSome(type, count) == 0;
        }

        /// <summary>As much as fits, the same way; returns how many were left over.</summary>
        public int AddSome(ItemType type, int count)
        {
            var def = ItemDb.Get(type);
            count = FillAmmo(def, count);
            for (int i = 0; i < Size && count > 0; i++)
            {
                if (Slots[i].Type != type || Slots[i].Count >= def.MaxStack) continue;
                int moved = System.Math.Min(count, def.MaxStack - Slots[i].Count);
                Slots[i].Count += moved;
                count -= moved;
            }
            for (int i = 0; i < Size && count > 0; i++)
            {
                if (!Slots[i].IsEmpty) continue;
                int moved = System.Math.Min(count, def.MaxStack);
                Slots[i] = ItemStack.Of(type, moved);
                count -= moved;
            }
            return count;
        }

        /// <summary>A stack picked up whole when it has a prefix (into the first empty bag slot, the hotbar first), else as its type is. Returns how many didn't fit.</summary>
        public int Pickup(ItemStack s, bool favorite = false)
        {
            if (s.IsEmpty) return 0;
            if (s.Prefix == 0 && s.Forge == 0) return Pickup(s.Type, s.Count, favorite);
            s.Favorite = favorite;
            for (int i = 0; i < Size; i++)
                if (Slots[i].IsEmpty)
                {
                    Slots[i] = s;
                    return 0;
                }
            return s.Count;
        }

        /// <summary>
        /// Player.GetItem, for an item picked up or coming back from the cursor: it tops up stacks
        /// of the same item first, then takes an empty slot — the first free hotbar slot for
        /// things that are used (weapons, tools, blocks, potions), else going up from the first
        /// slot for a favourite, else going down from the last slot (materials, accessories and
        /// ammo end up at the bottom right). Returns how many didn't fit.
        /// </summary>
        public int Pickup(ItemType type, int count, bool favorite = false)
        {
            var def = ItemDb.Get(type);
            count = FillCoins(def, count);
            count = FillAmmo(def, count);
            for (int i = 0; i < Size && count > 0; i++)
            {
                if (Slots[i].IsEmpty || Slots[i].Type != type || Slots[i].Count >= def.MaxStack) continue;
                int moved = System.Math.Min(count, def.MaxStack - Slots[i].Count);
                Slots[i].Count += moved;
                count -= moved;
            }
            while (count > 0)
            {
                int slot = -1;
                if (def.IsUsable)
                    for (int i = 0; i < HotbarSize && slot < 0; i++) if (Slots[i].IsEmpty) slot = i;
                if (slot < 0 && favorite)
                    for (int i = 0; i < Size && slot < 0; i++) if (Slots[i].IsEmpty) slot = i;
                if (slot < 0)
                    for (int i = Size - 1; i >= 0 && slot < 0; i--) if (Slots[i].IsEmpty) slot = i;
                if (slot < 0) break;
                int moved = System.Math.Min(count, def.MaxStack);
                Slots[slot] = ItemStack.Of(type, moved);
                Slots[slot].Favorite = favorite;
                count -= moved;
            }
            return count;
        }

        /// <summary>Player.GetItem's coins: money goes to the coin slot first. Returns how many are left.</summary>
        int FillCoins(ItemDef def, int count)
        {
            if (!FitsCoinSlot(def) || count <= 0) return count;
            var s = Slots[CoinSlot];
            if (!s.IsEmpty && s.Type != def.Type) return count;
            int have = s.IsEmpty ? 0 : s.Count;
            int moved = System.Math.Min(count, def.MaxStack - have);
            if (moved <= 0) return count;
            if (s.IsEmpty) Slots[CoinSlot] = ItemStack.Of(def.Type, moved);
            else Slots[CoinSlot].Count += moved;
            return count - moved;
        }

        /// <summary>The Geo the bag carries elsewhere gathered into the coin slot (a save from before it had one).</summary>
        public void GatherCoins()
        {
            var geo = ItemDb.Get(ItemType.Geo);
            for (int i = 0; i < Size; i++)
            {
                if (Slots[i].IsEmpty || !FitsCoinSlot(Slots[i].Def)) continue;
                int left = FillCoins(geo, Slots[i].Count);
                if (left <= 0) Slots[i] = default;
                else Slots[i].Count = left;
            }
        }

        /// <summary>
        /// Player.FillAmmo: ammo picked up tops up the ammo slots' stacks of it, top to bottom, then
        /// takes an empty one (<see cref="FillsEmptyAmmoSlot"/>). Returns how many are left.
        /// </summary>
        int FillAmmo(ItemDef def, int count)
        {
            if (!FitsAmmoSlot(def)) return count;
            for (int i = Size; i < AmmoEnd && count > 0; i++)
            {
                if (Slots[i].IsEmpty || Slots[i].Type != def.Type || Slots[i].Count >= def.MaxStack) continue;
                int moved = System.Math.Min(count, def.MaxStack - Slots[i].Count);
                Slots[i].Count += moved;
                count -= moved;
            }
            if (!FillsEmptyAmmoSlot(def)) return count;
            for (int i = Size; i < AmmoEnd && count > 0; i++)
            {
                if (!Slots[i].IsEmpty) continue;
                int moved = System.Math.Min(count, def.MaxStack);
                Slots[i] = ItemStack.Of(def.Type, moved);
                count -= moved;
            }
            return count;
        }

        /// <summary>Room for this many more of an item: the bag, and the ammo slots as far as they take it.</summary>
        public int Room(ItemType type)
        {
            var def = ItemDb.Get(type);
            int room = 0;
            for (int i = 0; i < Total; i++)
            {
                var s = Slots[i];
                bool ammoSlot = IsAmmoSlot(i);
                if (IsCoinSlot(i) && !FitsCoinSlot(def)) continue;
                if (s.IsEmpty) { if (!ammoSlot || FillsEmptyAmmoSlot(def)) room += def.MaxStack; }
                else if (s.Type == type && (!ammoSlot || FitsAmmoSlot(def))) room += def.MaxStack - s.Count;
            }
            return room;
        }

        public void ConsumeSelected()
        {
            if (Infinite) return;
            if (UsingMouse)
            {
                Mouse.Count--;
                if (Mouse.Count <= 0) Mouse = default;
                return;
            }
            int held = Held;
            Slots[held].Count--;
            if (Slots[held].Count <= 0) Slots[held] = default;
        }

        /// <summary>
        /// Terraria's ammo pick (Player.AmmoSlotOrder_Default, PickAmmo_IterateRange): the ammo slots
        /// top to bottom, then the inventory from its first slot on. -1 when there is none.
        /// </summary>
        public int FindAmmo(AmmoKind kind)
        {
            for (int i = Size; i < AmmoEnd; i++)
                if (!Slots[i].IsEmpty && Slots[i].Def.Ammo == kind) return i;
            for (int i = 0; i < Size; i++)
                if (!Slots[i].IsEmpty && Slots[i].Def.Ammo == kind) return i;
            return -1;
        }

        static readonly System.Collections.Generic.List<int> cycle = new System.Collections.Generic.List<int>();

        /// <summary>
        /// The Chaos Cylinders' pick (PickAmmo_PickAmmoItem, Pl:54329-54393): the offset-th stack
        /// of the ammo slots (the plain pick when they hold none: AmmoSlotOrder_AmmoOnly), or of every
        /// stack in the plain order (full inventory, random); the plain pick for the simple one or none worn.
        /// </summary>
        public int FindAmmo(AmmoKind kind, AmmoCycleMode mode, int offset)
        {
            if (mode == AmmoCycleMode.Off || mode == AmmoCycleMode.Simple) return FindAmmo(kind);
            cycle.Clear();
            for (int i = Size; i < AmmoEnd; i++)
                if (!Slots[i].IsEmpty && Slots[i].Def.Ammo == kind) cycle.Add(i);
            if (mode == AmmoCycleMode.AmmoSlots)
            {
                if (cycle.Count == 0) return FindAmmo(kind);
            }
            else
                for (int i = 0; i < Size; i++)
                    if (!Slots[i].IsEmpty && Slots[i].Def.Ammo == kind) cycle.Add(i);
            return cycle.Count == 0 ? -1 : cycle[(int)((uint)offset % (uint)cycle.Count)];
        }

        public void ConsumeAt(int index)
        {
            if (Infinite || index < 0) return;
            Slots[index].Count--;
            if (Slots[index].Count <= 0) Slots[index] = default;
        }

        /// <summary>The first stack of an item in the inventory matching a test, -1 if none.</summary>
        public int Find(System.Func<ItemDef, bool> test)
        {
            for (int i = 0; i < Size; i++)
                if (!Slots[i].IsEmpty && test(Slots[i].Def)) return i;
            return -1;
        }

        /// <summary>An accessory with this effect is in effect (worn, or shared in).</summary>
        public bool HasEquipped(AccessoryEffect effect)
        {
            foreach (var a in EffectiveAccessories)
                if (!a.IsEmpty && a.Def.Accessory == effect) return true;
            return false;
        }

        /// <summary>Anywhere the player has it: the inventory, the cursor, the worn set or a stored loadout.</summary>
        public bool Owns(ItemType type)
        {
            if (Mouse.Type == type && !Mouse.IsEmpty) return true;
            foreach (var s in Slots) if (s.Type == type && !s.IsEmpty) return true;
            foreach (var a in Accessories) if (a.Type == type && !a.IsEmpty) return true;
            foreach (var a in Armor) if (a.Type == type && !a.IsEmpty) return true;
            foreach (var a in Misc) if (a.Type == type && !a.IsEmpty) return true;
            foreach (var l in Loadouts)
            {
                foreach (var a in l.Accessories) if (a.Type == type && !a.IsEmpty) return true;
                foreach (var a in l.Armor) if (a.Type == type && !a.IsEmpty) return true;
            }
            return false;
        }

        /// <summary>Puts whatever is on the cursor back into the inventory (closing the inventory).</summary>
        public void ReturnMouseItem()
        {
            if (Mouse.IsEmpty) return;
            if (Add(Mouse.Type, Mouse.Count)) Mouse = default;
        }
    }
}
