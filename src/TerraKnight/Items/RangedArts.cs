using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The ranged weapons remade (the user's 10-27 "现在升级所有射手武器…要全新的动作特效，如果可以的话不同的武器都不同，并且可以有新的
    /// 左键效果，比如连发等等，反正要创意+正常强度…右键自然也可以加"; the thrown ones too): how each is moved through a use, what comes off
    /// it as it fires (SwordFxRanged.cs), its left button's way of firing (RangedFire) and its right-click art
    /// (BladeSkillsRanged.cs). A bow snaps forward as it looses and is drawn again for the next; a crossbow kicks and is
    /// cranked; a pistol jumps up and settles; a rifle kicks back into the shoulder and its bolt is worked; a machine gun
    /// shudders; a shotgun kicks high and is pumped; a launcher shoves back and down, blasting smoke behind; a sprayer sways;
    /// a blowpipe is puffed; a thrown thing is flung overhand, the next one drawn back ready. Their colours and motes are the
    /// weapon's own (Terraria's dusts). On a world with the new weapons (BladeArts.Enabled); the four Hallownest spell guns
    /// keep their own ways.
    /// </summary>
    internal static class RangedArts
    {
        internal enum Kind : byte { Bow, Repeater, Pistol, Rifle, Auto, Shotgun, Launcher, Cannon, Sprayer, Blowpipe, Thrown, Lob, Held }

        /// <summary>
        /// The left button's way of firing (RangedFire): the game's own; every Nth shot a full-power one; two at once; N in a
        /// quick burst; quickening as it is held; a copy sent on to the next enemy (N times); shards through the struck (N);
        /// a burst round the struck; turning after enemies; through more; harder close up, softer far off.
        /// </summary>
        internal enum Left : byte { Plain, Power, Twin, Burst, SpinUp, Ricochet, Split, Blast, Home, Pierce, PointBlank }

        internal readonly struct Look
        {
            public readonly Kind Kind;
            public readonly Color A, B;
            public readonly int Dust;
            public readonly Left Left;
            public readonly int N;
            public Look(Kind kind, Color a, Color b, int dust, Left left = Left.Plain, int n = 0)
            {
                Kind = kind; A = a; B = b; Dust = dust; Left = left; N = n;
            }
        }

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static readonly Color Wood1 = C(0.75f, 0.55f, 0.3f), Wood2 = C(0.95f, 0.85f, 0.6f), Fire1 = C(1f, 0.55f, 0.15f), Fire2 = C(1f, 0.9f, 0.4f),
            Ice1 = C(0.5f, 0.85f, 1f), Ice2 = Color.white, Shade1 = C(0.55f, 0.25f, 0.8f), Shade2 = C(0.85f, 0.6f, 1f),
            Blood1 = C(0.85f, 0.1f, 0.15f), Blood2 = C(1f, 0.45f, 0.4f), Leaf1 = C(0.35f, 0.75f, 0.25f), Leaf2 = C(0.75f, 1f, 0.45f),
            Water1 = C(0.25f, 0.55f, 1f), Water2 = C(0.65f, 0.9f, 1f), Cosmic1 = C(0.85f, 0.35f, 1f), Cosmic2 = C(0.45f, 0.8f, 1f),
            Gold1 = C(1f, 0.82f, 0.3f), Gold2 = C(1f, 0.97f, 0.75f), Steel1 = C(0.6f, 0.62f, 0.68f), Steel2 = C(1f, 0.92f, 0.7f),
            Volt1 = C(0.6f, 0.9f, 1f), Volt2 = C(1f, 1f, 0.7f), Bone1 = C(0.9f, 0.88f, 0.8f), Bone2 = Color.white,
            Venom1 = C(0.55f, 0.8f, 0.3f), Venom2 = C(0.9f, 1f, 0.6f), Holy1 = C(1f, 0.9f, 0.45f), Holy2 = C(1f, 1f, 0.85f);

        static readonly Dictionary<ItemType, Look> looks = new Dictionary<ItemType, Look>
        {
            // Bows: each wood's and each metal's colours; the later ones their arrows'.
            [ItemType.WoodenBow] = new Look(Kind.Bow, Wood1, Wood2, 7, Left.Power, 3),
            [ItemType.CopperBow] = new Look(Kind.Bow, C(0.9f, 0.5f, 0.25f), C(1f, 0.75f, 0.5f), 278, Left.Power, 3),
            [ItemType.TinBow] = new Look(Kind.Bow, C(0.75f, 0.72f, 0.6f), C(0.95f, 0.95f, 0.85f), 278, Left.Twin),
            [ItemType.RichMahoganyBow] = new Look(Kind.Bow, C(0.7f, 0.3f, 0.25f), C(0.5f, 0.85f, 0.35f), 278, Left.Home),
            [ItemType.PalmWoodBow] = new Look(Kind.Bow, C(0.85f, 0.7f, 0.45f), C(1f, 0.95f, 0.7f), 278, Left.Ricochet, 1),
            [ItemType.BorealWoodBow] = new Look(Kind.Bow, C(0.6f, 0.5f, 0.45f), C(0.85f, 0.95f, 1f), 76, Left.Power, 3),
            [ItemType.LeadBow] = new Look(Kind.Bow, C(0.4f, 0.45f, 0.55f), C(0.75f, 0.8f, 0.9f), 278, Left.Split, 2),
            [ItemType.SilverBow] = new Look(Kind.Bow, C(0.8f, 0.85f, 0.9f), Color.white, 278, Left.Ricochet, 1),
            [ItemType.EbonwoodBow] = new Look(Kind.Bow, C(0.45f, 0.3f, 0.6f), C(0.75f, 0.6f, 0.9f), 278, Left.Split, 3),
            [ItemType.ShadewoodBow] = new Look(Kind.Bow, C(0.7f, 0.2f, 0.2f), C(1f, 0.55f, 0.5f), 278, Left.Home),
            [ItemType.TungstenBow] = new Look(Kind.Bow, C(0.5f, 0.75f, 0.55f), C(0.8f, 1f, 0.85f), 278, Left.Twin),
            [ItemType.PlatinumBow] = new Look(Kind.Bow, C(0.75f, 0.8f, 0.95f), C(0.95f, 0.97f, 1f), 278, Left.Power, 3),
            [ItemType.IronBow] = new Look(Kind.Bow, C(0.6f, 0.6f, 0.62f), C(0.9f, 0.9f, 0.92f), 278, Left.Ricochet, 1),
            [ItemType.GoldBow] = new Look(Kind.Bow, Gold1, Gold2, 278, Left.Twin),
            [ItemType.AshWoodBow] = new Look(Kind.Bow, Fire1, Fire2, 6, Left.Blast),
            [ItemType.PearlwoodBow] = new Look(Kind.Bow, C(1f, 0.75f, 0.9f), C(0.75f, 0.9f, 1f), 278, Left.Home),
            [ItemType.TendonBow] = new Look(Kind.Bow, Blood1, Blood2, 5, Left.Split, 3),
            [ItemType.DemonBow] = new Look(Kind.Bow, Shade1, Shade2, 27, Left.Twin),
            [ItemType.BloodRainBow] = new Look(Kind.Bow, Blood1, Blood2, 5),
            [ItemType.HellwingBow] = new Look(Kind.Bow, Fire1, Fire2, 6, Left.Twin),
            [ItemType.BeesKnees] = new Look(Kind.Bow, C(1f, 0.8f, 0.2f), C(1f, 0.95f, 0.6f), 278, Left.Power, 3),
            [ItemType.MoltenFury] = new Look(Kind.Bow, Fire1, Fire2, 6, Left.Blast),
            [ItemType.IceBow] = new Look(Kind.Bow, Ice1, Ice2, 135, Left.Split, 3),
            [ItemType.Marrow] = new Look(Kind.Bow, Bone1, Bone2, 26, Left.Ricochet, 2),
            [ItemType.ShadowFlameBow] = new Look(Kind.Bow, Shade1, Shade2, 27, Left.Home),
            // The bows raining from the sky: their own rain (and their touch, signs), its hits too random to ration.
            [ItemType.DaedalusStormbow] = new Look(Kind.Bow, C(0.6f, 0.8f, 1f), C(1f, 0.9f, 1f), 15),
            [ItemType.AerialBane] = new Look(Kind.Bow, C(0.55f, 0.75f, 1f), C(0.9f, 0.95f, 1f), 15, Left.Power, 3),
            [ItemType.Tsunami] = new Look(Kind.Bow, Water1, Water2, 33, Left.Pierce),
            [ItemType.Eventide] = new Look(Kind.Bow, Cosmic1, Cosmic2, 278),
            [ItemType.PulseBow] = new Look(Kind.Bow, Volt1, Volt2, 226, Left.Power, 3),
            [ItemType.PhantomPhoenix] = new Look(Kind.Held, Fire1, Fire2, 6),
            [ItemType.Phantasm] = new Look(Kind.Held, C(0.4f, 0.95f, 0.9f), C(0.85f, 1f, 1f), 229),
            [ItemType.AcornSlingshot] = new Look(Kind.Bow, Wood1, Wood2, 7, Left.Ricochet, 1),
            // Repeaters: their metals'.
            [ItemType.CobaltRepeater] = new Look(Kind.Repeater, C(0.25f, 0.45f, 0.9f), C(0.6f, 0.8f, 1f), 278, Left.Power, 4),
            [ItemType.PalladiumRepeater] = new Look(Kind.Repeater, C(1f, 0.55f, 0.3f), C(1f, 0.85f, 0.6f), 278, Left.Twin),
            [ItemType.MythrilRepeater] = new Look(Kind.Repeater, C(0.4f, 0.85f, 0.7f), C(0.75f, 1f, 0.9f), 278, Left.Ricochet, 1),
            [ItemType.OrichalcumRepeater] = new Look(Kind.Repeater, C(1f, 0.45f, 0.8f), C(1f, 0.75f, 0.95f), 278, Left.Split, 2),
            [ItemType.AdamantiteRepeater] = new Look(Kind.Repeater, C(0.9f, 0.2f, 0.3f), C(1f, 0.6f, 0.6f), 278, Left.Pierce),
            [ItemType.TitaniumRepeater] = new Look(Kind.Repeater, C(0.75f, 0.75f, 0.8f), C(0.95f, 0.95f, 1f), 278, Left.Split, 3),
            [ItemType.HallowedRepeater] = new Look(Kind.Repeater, Holy1, Holy2, 57, Left.Home),
            [ItemType.ChlorophyteShotbow] = new Look(Kind.Repeater, Leaf1, Leaf2, 44, Left.Home),
            // Pistols and the rest held in one hand.
            [ItemType.FlintlockPistol] = new Look(Kind.Pistol, C(0.7f, 0.55f, 0.4f), Fire2, 6, Left.Power, 3),
            [ItemType.TheUndertaker] = new Look(Kind.Pistol, Blood1, Blood2, 5, Left.Burst, 2),
            [ItemType.Handgun] = new Look(Kind.Pistol, Steel1, Steel2, 6, Left.Burst, 3),
            [ItemType.PhoenixBlaster] = new Look(Kind.Pistol, Fire1, Fire2, 6, Left.Blast),
            [ItemType.Revolver] = new Look(Kind.Pistol, Steel1, Steel2, 6, Left.Power, 6),
            [ItemType.VenusMagnum] = new Look(Kind.Pistol, Leaf1, Leaf2, 44, Left.Home),
            [ItemType.FlareGun] = new Look(Kind.Pistol, C(1f, 0.4f, 0.2f), Fire2, 6, Left.Twin),
            [ItemType.PewMaticHorn] = new Look(Kind.Pistol, Gold1, Gold2, 278),
            [ItemType.PaintballGun] = new Look(Kind.Pistol, C(1f, 0.4f, 0.7f), C(0.4f, 0.9f, 1f), 278, Left.Twin),
            [ItemType.DartPistol] = new Look(Kind.Pistol, Venom1, Venom2, 46, Left.Ricochet, 1),
            [ItemType.NailGun] = new Look(Kind.Pistol, Steel1, Steel2, 6),
            // Rifles.
            [ItemType.RedRyder] = new Look(Kind.Rifle, C(0.75f, 0.25f, 0.2f), C(1f, 0.85f, 0.6f), 6, Left.Power, 3),
            [ItemType.Musket] = new Look(Kind.Rifle, C(0.6f, 0.45f, 0.3f), C(1f, 0.85f, 0.55f), 6, Left.Pierce),
            [ItemType.ClockworkAssaultRifle] = new Look(Kind.Rifle, Steel1, Steel2, 6),
            [ItemType.DartRifle] = new Look(Kind.Rifle, Venom1, Venom2, 46, Left.Pierce),
            [ItemType.SniperRifle] = new Look(Kind.Rifle, Steel1, Steel2, 6, Left.Pierce),
            [ItemType.CandyCornRifle] = new Look(Kind.Auto, C(1f, 0.65f, 0.2f), C(1f, 1f, 0.8f), 278, Left.Ricochet, 1),
            [ItemType.Harpoon] = new Look(Kind.Rifle, Steel1, Water2, 33),
            [ItemType.Sandgun] = new Look(Kind.Cannon, C(0.85f, 0.75f, 0.45f), C(1f, 0.95f, 0.75f), 32),
            // Machine guns: quickening as they're held.
            [ItemType.Minishark] = new Look(Kind.Auto, C(0.55f, 0.65f, 0.75f), Steel2, 6, Left.SpinUp),
            [ItemType.Gatligator] = new Look(Kind.Auto, C(0.4f, 0.6f, 0.3f), C(0.85f, 0.95f, 0.6f), 6, Left.SpinUp),
            [ItemType.Megashark] = new Look(Kind.Auto, C(0.45f, 0.55f, 0.7f), Steel2, 6, Left.SpinUp),
            [ItemType.Uzi] = new Look(Kind.Auto, Steel1, Steel2, 6, Left.SpinUp),
            [ItemType.ChainGun] = new Look(Kind.Auto, Blood1, Fire2, 6, Left.SpinUp),
            [ItemType.SDMG] = new Look(Kind.Auto, Cosmic1, Cosmic2, 229, Left.SpinUp),
            [ItemType.VortexBeater] = new Look(Kind.Held, C(0.4f, 0.95f, 0.9f), C(0.85f, 1f, 1f), 229),
            // Shotguns: harder close up.
            [ItemType.Boomstick] = new Look(Kind.Shotgun, C(0.6f, 0.45f, 0.3f), Fire2, 6, Left.PointBlank),
            [ItemType.QuadBarrelShotgun] = new Look(Kind.Shotgun, Steel1, Fire2, 6, Left.PointBlank),
            [ItemType.Shotgun] = new Look(Kind.Shotgun, Steel1, Fire2, 6, Left.PointBlank),
            [ItemType.OnyxBlaster] = new Look(Kind.Shotgun, Shade1, Shade2, 27, Left.PointBlank),
            [ItemType.TacticalShotgun] = new Look(Kind.Shotgun, C(0.4f, 0.45f, 0.4f), Fire2, 6, Left.PointBlank),
            [ItemType.Xenopopper] = new Look(Kind.Shotgun, C(0.4f, 0.9f, 0.85f), C(0.85f, 1f, 1f), 278, Left.PointBlank),
            // Launchers and cannons.
            [ItemType.GrenadeLauncher] = new Look(Kind.Launcher, Steel1, Fire2, 6),
            [ItemType.RocketLauncher] = new Look(Kind.Launcher, C(0.55f, 0.6f, 0.4f), Fire2, 6),
            [ItemType.ProximityMineLauncher] = new Look(Kind.Launcher, C(0.45f, 0.5f, 0.4f), C(1f, 0.4f, 0.3f), 6),
            [ItemType.SnowmanCannon] = new Look(Kind.Launcher, Ice1, Ice2, 76),
            [ItemType.Celebration] = new Look(Kind.Launcher, C(1f, 0.45f, 0.7f), C(0.5f, 0.9f, 1f), 278),
            [ItemType.CelebrationMk2] = new Look(Kind.Held, C(1f, 0.45f, 0.7f), C(0.5f, 0.9f, 1f), 278),
            [ItemType.ElectrosphereLauncher] = new Look(Kind.Launcher, Volt1, Volt2, 226),
            [ItemType.StakeLauncher] = new Look(Kind.Launcher, Wood1, Wood2, 7, Left.Pierce),
            [ItemType.JackOLanternLauncher] = new Look(Kind.Launcher, C(1f, 0.55f, 0.1f), C(1f, 0.85f, 0.3f), 6),
            [ItemType.BarrelLauncher] = new Look(Kind.Launcher, Wood1, Fire2, 6),
            [ItemType.Stynger] = new Look(Kind.Launcher, C(0.55f, 0.5f, 0.35f), C(1f, 0.85f, 0.4f), 6),
            [ItemType.StarCannon] = new Look(Kind.Cannon, C(1f, 0.85f, 0.35f), C(1f, 1f, 0.8f), 15, Left.Twin),
            [ItemType.SuperStarCannon] = new Look(Kind.Cannon, C(1f, 0.6f, 0.9f), C(1f, 1f, 0.8f), 15),
            [ItemType.SnowballCannon] = new Look(Kind.Cannon, Ice1, Ice2, 76, Left.Twin),
            // Sprayers and pipes.
            [ItemType.Flamethrower] = new Look(Kind.Sprayer, Fire1, Fire2, 6),
            [ItemType.ElfMelter] = new Look(Kind.Sprayer, C(1f, 0.3f, 0.25f), C(0.5f, 1f, 0.5f), 6),
            [ItemType.Toxikarp] = new Look(Kind.Sprayer, Venom1, Venom2, 46),
            [ItemType.PiranhaGun] = new Look(Kind.Held, Water1, Water2, 33),
            [ItemType.Blowpipe] = new Look(Kind.Blowpipe, Wood1, Venom2, 7, Left.Power, 3),
            [ItemType.Blowgun] = new Look(Kind.Blowpipe, Venom1, Venom2, 46, Left.Pierce),
            // Thrown.
            [ItemType.MudBall] = new Look(Kind.Thrown, C(0.45f, 0.35f, 0.25f), C(0.7f, 0.6f, 0.45f), 0, Left.Twin),
            [ItemType.Shuriken] = new Look(Kind.Thrown, Steel1, Color.white, 278, Left.Ricochet, 2),
            [ItemType.ThrowingKnife] = new Look(Kind.Thrown, Steel1, Color.white, 278, Left.Twin),
            [ItemType.PoisonedKnife] = new Look(Kind.Thrown, Venom1, Venom2, 46, Left.Power, 3),
            [ItemType.RottenEgg] = new Look(Kind.Thrown, C(0.85f, 0.85f, 0.55f), C(0.6f, 0.8f, 0.3f), 278, Left.Blast),
            [ItemType.BoneDagger] = new Look(Kind.Thrown, Bone1, Bone2, 26, Left.Split, 2),
            [ItemType.BoneJavelin] = new Look(Kind.Thrown, Bone1, Bone2, 26, Left.Pierce),
            [ItemType.StarAnise] = new Look(Kind.Thrown, C(0.85f, 0.55f, 0.3f), Gold2, 278, Left.Home),
            [ItemType.FrostDaggerfish] = new Look(Kind.Thrown, Ice1, Ice2, 135, Left.Ricochet, 1),
            [ItemType.Javelin] = new Look(Kind.Thrown, Wood1, Steel2, 7, Left.Pierce),
            [ItemType.SpikyBall] = new Look(Kind.Thrown, Steel1, Color.white, 278, Left.Twin),
            [ItemType.PrettyMirror] = new Look(Kind.Thrown, C(0.85f, 0.9f, 1f), Color.white, 278, Left.Split, 3),
            [ItemType.BloodWater] = new Look(Kind.Lob, Blood1, Blood2, 5, Left.Blast),
            [ItemType.UnholyWater] = new Look(Kind.Lob, Shade1, Shade2, 27, Left.Blast),
            [ItemType.HolyWater] = new Look(Kind.Lob, Holy1, C(0.7f, 0.9f, 1f), 57, Left.Blast),
            [ItemType.AleTosser] = new Look(Kind.Lob, C(0.9f, 0.7f, 0.3f), C(1f, 0.95f, 0.75f), 278, Left.Blast),
            [ItemType.Grenade] = new Look(Kind.Lob, Steel1, Fire2, 6),
            [ItemType.BouncyGrenade] = new Look(Kind.Lob, C(1f, 0.5f, 0.75f), Fire2, 6),
            [ItemType.StickyGrenade] = new Look(Kind.Lob, C(0.5f, 0.8f, 0.4f), Fire2, 6),
            [ItemType.Beenade] = new Look(Kind.Lob, C(1f, 0.8f, 0.2f), C(1f, 0.95f, 0.6f), 278),
            [ItemType.PartyGirlGrenade] = new Look(Kind.Lob, C(1f, 0.45f, 0.75f), C(0.5f, 0.9f, 1f), 278),
            [ItemType.MolotovCocktail] = new Look(Kind.Lob, Fire1, Fire2, 6),
        };

        static TerraProjectiles.Offshoot Off(ProjKind kind, int count, TerraProjectiles.SpawnWay way, float damage, float chance = 1f, float speed = 0f) =>
            new TerraProjectiles.Offshoot { Kind = kind, Count = count, Way = way, Damage = damage, Chance = chance, Speed = speed };

        /// <summary>
        /// What sets a later weapon's left shot apart beyond its way of firing (the user's 10-27 "尤其是后期的装备，左键的射击都应该
        /// 单独特殊化"): its element (fire, frost, venom, shadowflame, ichor, bleeding), what comes off it (bees off Bee's Knees'
        /// arrows, stars falling where the Pearlwood Bow's strike, spores off the Chlorophyte Shotbow's, petals off the
        /// Orichalcum Repeater's, titanium shards off the Titanium Repeater's), lightning off the Pulse Bow's, the S.D.M.G.'s
        /// rounds weaving, the Chain Gun's hot barrel setting its rounds alight, the Sniper Rifle's tracer. With the heat a
        /// machine gun has built (RangedFire.Heat).
        /// </summary>
        static readonly Dictionary<ItemType, System.Action<TerraProjectiles.RangedShot, float>> signs = new Dictionary<ItemType, System.Action<TerraProjectiles.RangedShot, float>>
        {
            [ItemType.AshWoodBow] = (r, h) => { r.Debuff = EnemyDebuffs.OnFire; r.DebuffTicks = 240; },
            [ItemType.HellwingBow] = (r, h) => { r.Debuff = EnemyDebuffs.OnFire; r.DebuffTicks = 300; },
            [ItemType.BeesKnees] = (r, h) => r.OnHit = Off(ProjKind.Bee, 1, TerraProjectiles.SpawnWay.Ring, 0.3f, 0.35f, 4f),
            [ItemType.MoltenFury] = (r, h) => { r.Debuff = EnemyDebuffs.Hellfire; r.DebuffTicks = 240; },
            [ItemType.TendonBow] = (r, h) => { r.Debuff = EnemyDebuffs.Ichor; r.DebuffTicks = 180; r.DebuffChance = 0.35f; },
            [ItemType.BloodRainBow] = (r, h) => { r.Debuff = EnemyDebuffs.Bleeding; r.DebuffTicks = 240; r.Trail = 0.4f; },
            [ItemType.DemonBow] = (r, h) => { r.Debuff = EnemyDebuffs.Shadowflame; r.DebuffTicks = 180; r.DebuffChance = 0.35f; },
            [ItemType.PearlwoodBow] = (r, h) => r.OnHit = Off(ProjKind.HallowStar, 1, TerraProjectiles.SpawnWay.Fall, 0.35f, 0.33f),
            [ItemType.DaedalusStormbow] = (r, h) => r.OnHit = Off(ProjKind.None, 1, TerraProjectiles.SpawnWay.Fall, 0.4f, 0.2f),
            [ItemType.OrichalcumRepeater] = (r, h) => r.OnHit = Off(ProjKind.OrichalcumPetal, 2, TerraProjectiles.SpawnWay.Ahead, 0.25f, 0.3f, 6f),
            [ItemType.AdamantiteRepeater] = (r, h) => { r.Debuff = EnemyDebuffs.Ichor; r.DebuffTicks = 180; r.DebuffChance = 0.3f; },
            [ItemType.TitaniumRepeater] = (r, h) => r.ShardKind = ProjKind.TitaniumShard,
            [ItemType.HallowedRepeater] = (r, h) => r.OnHit = Off(ProjKind.HallowStar, 1, TerraProjectiles.SpawnWay.Fall, 0.35f, 0.25f),
            [ItemType.IceBow] = (r, h) => { r.Debuff = EnemyDebuffs.Frostburn; r.DebuffTicks = 240; },
            [ItemType.Marrow] = (r, h) => { r.Debuff = EnemyDebuffs.Bleeding; r.DebuffTicks = 300; },
            [ItemType.ShadowFlameBow] = (r, h) => { r.Debuff = EnemyDebuffs.Shadowflame; r.DebuffTicks = 240; },
            [ItemType.ChlorophyteShotbow] = (r, h) => r.OnHit = Off(ProjKind.SporeCloud, 1, TerraProjectiles.SpawnWay.Ring, 0.25f, 0.2f, 0.3f),
            [ItemType.AerialBane] = (r, h) => r.Tracer = true,
            [ItemType.Eventide] = (r, h) => { r.Home = Mathf.Max(r.Home, 2.5f); r.Trail = Mathf.Max(r.Trail, 0.4f); },
            [ItemType.PulseBow] = (r, h) => { r.Chain = 1; r.ChainDamage = 0.4f; },
            [ItemType.Tsunami] = (r, h) => { r.TrailDust = 33; r.Trail = Mathf.Max(r.Trail, 0.3f); },
            [ItemType.PhoenixBlaster] = (r, h) => { r.Debuff = EnemyDebuffs.OnFire; r.DebuffTicks = 180; },
            [ItemType.DartPistol] = (r, h) => { r.Debuff = EnemyDebuffs.Venom; r.DebuffTicks = 180; },
            [ItemType.DartRifle] = (r, h) => { r.Debuff = EnemyDebuffs.Poisoned; r.DebuffTicks = 300; },
            [ItemType.Blowgun] = (r, h) => { r.Debuff = EnemyDebuffs.Poisoned; r.DebuffTicks = 300; },
            [ItemType.Harpoon] = (r, h) => { r.Debuff = EnemyDebuffs.Bleeding; r.DebuffTicks = 240; },
            [ItemType.Gatligator] = (r, h) => { r.Debuff = EnemyDebuffs.Bleeding; r.DebuffTicks = 180; r.DebuffChance = 0.3f; },
            [ItemType.ChainGun] = (r, h) =>
            {
                // A hot barrel (most of its run up) lights its rounds.
                if (h < 0.75f) return;
                r.Debuff = EnemyDebuffs.OnFire;
                r.DebuffTicks = 180;
                r.Trail = Mathf.Max(r.Trail, 0.4f);
                r.TrailDust = 6;
            },
            [ItemType.SDMG] = (r, h) => { r.Weave = Random.value < 0.5f ? 4f : -4f; r.Trail = Mathf.Max(r.Trail, 0.25f); r.TrailDust = 229; },
            [ItemType.VenusMagnum] = (r, h) => { r.Debuff = EnemyDebuffs.Poisoned; r.DebuffTicks = 240; },
            [ItemType.NailGun] = (r, h) => { r.Debuff = EnemyDebuffs.Bleeding; r.DebuffTicks = 240; },
            [ItemType.PaintballGun] = (r, h) => { r.Debuff = EnemyDebuffs.Confused; r.DebuffTicks = 90; r.DebuffChance = 0.15f; },
            [ItemType.Xenopopper] = (r, h) => { r.Debuff = EnemyDebuffs.Slimed; r.DebuffTicks = 180; },
            [ItemType.OnyxBlaster] = (r, h) => { r.Debuff = EnemyDebuffs.Shadowflame; r.DebuffTicks = 180; r.DebuffChance = 0.5f; },
            [ItemType.SniperRifle] = (r, h) => r.Tracer = true,
            [ItemType.SnowmanCannon] = (r, h) => { r.Debuff = EnemyDebuffs.Frostburn; r.DebuffTicks = 240; },
            [ItemType.JackOLanternLauncher] = (r, h) => { r.Debuff = EnemyDebuffs.OnFire; r.DebuffTicks = 300; },
            [ItemType.ElectrosphereLauncher] = (r, h) => { r.Chain = 1; r.ChainDamage = 0.35f; },
            [ItemType.Toxikarp] = (r, h) => { r.Debuff = EnemyDebuffs.Venom; r.DebuffTicks = 180; },
            [ItemType.SuperStarCannon] = (r, h) => { r.Trail = Mathf.Max(r.Trail, 0.4f); r.TrailDust = 15; },
            [ItemType.StarCannon] = (r, h) => { r.Trail = Mathf.Max(r.Trail, 0.3f); r.TrailDust = 15; },
            [ItemType.FrostDaggerfish] = (r, h) => { r.Debuff = EnemyDebuffs.Frostburn; r.DebuffTicks = 180; },
            [ItemType.Beenade] = (r, h) => r.OnDeath = Off(ProjKind.Bee, 2, TerraProjectiles.SpawnWay.Ring, 0.25f, 1f, 4f),
            [ItemType.StarAnise] = (r, h) => { r.Trail = Mathf.Max(r.Trail, 0.3f); },
        };

        /// <summary>The weapon's own touches on a left shot (signs), with its machine gun's heat.</summary>
        public static void Sign(ItemDef d, TerraProjectiles.RangedShot r, float heat)
        {
            if (d != null && signs.TryGetValue(d.Type, out var s)) s(r, heat);
        }

        /// <summary>The four Hallownest spell guns: their own ways (SoulWave, Howl, Shade, Abyss).</summary>
        internal static bool OwnWay(ItemType t) => t == ItemType.RevenantPulseRifle || t == ItemType.GhostPipeOrgan || t == ItemType.ShadeRiftRifle || t == ItemType.VoidSniper;

        /// <summary>The weapon's look: the table's, else by its ammo and its shape (its colours steel and warm).</summary>
        public static Look Of(ItemDef d)
        {
            if (d != null && looks.TryGetValue(d.Type, out var l)) return l;
            Kind kind;
            if (d == null) kind = Kind.Pistol;
            else if (d.Channel) kind = Kind.Held;
            else if (d.NoUseGraphic || !d.HoldOut) kind = Kind.Thrown;
            else if (d.UseAmmo == AmmoKind.Arrow) kind = Wide(d) ? Kind.Repeater : Kind.Bow;
            else if (d.UseAmmo == AmmoKind.Rocket) kind = Kind.Launcher;
            else if (d.UseAmmo == AmmoKind.Dart) kind = Kind.Blowpipe;
            else if (d.UseAmmo == AmmoKind.Gel) kind = Kind.Sprayer;
            else if (d.UseAmmo == AmmoKind.Bullet) kind = d.UseTime * 60f <= 10f ? Kind.Auto : Wide(d) ? Kind.Rifle : Kind.Pistol;
            else kind = Kind.Cannon;
            return new Look(kind, Steel1, Steel2, 6);
        }

        static bool Wide(ItemDef d)
        {
            var s = TerraMod.ItemSprite(d.Type);
            return s != null && s.rect.width >= 48f;
        }

        /// <summary>A ranged weapon used its new way: a world with the new weapons, a ranged weapon not of the four spell guns.</summary>
        public static bool Active(ItemDef d) => d != null && WeaponRefitting.EnabledFor(d) && d.IsWeapon && d.Class == DamageClass.Ranged && !OwnWay(d.Type) && !Player.GravityGlobe.Flipped;

        /// <summary>Thrown from the hand (the arm flings it overhand, the next one drawn back ready) rather than held out.</summary>
        public static bool Flung(ItemDef d) => Active(d) && (Of(d).Kind == Kind.Thrown || Of(d).Kind == Kind.Lob);

        // ------------------------------------------------------------------ use time

        /// <summary>
        /// The ranged use's time multiplier (Equipment.UseScale): a burst's use long enough for its shots (90% of theirs), a
        /// machine gun's from 1.25 down to 0.85 over 2.5 s of firing, Overdrive's 0.65 on top.
        /// </summary>
        public static float UseScale(ItemDef d)
        {
            if (!Active(d)) return 1f;
            var look = Of(d);
            float k = 1f;
            if (look.Left == Left.Burst) k = Mathf.Max(1, look.N) * 0.9f;
            else if (look.Left == Left.SpinUp) k = Mathf.Lerp(1.25f, 0.85f, RangedFire.Heat(d));
            // A gun's stance (BladeSkillsGuns.cs): the Minishark's overdrive, the Chain Gun's spun-up barrels.
            k *= BladeSkills.GunUseScale(d);
            return k;
        }

        // ------------------------------------------------------------------ the hold through a use

        /// <summary>A sprung thing let go: from 1, swinging past 0 and settling.</summary>
        static float Spring(float x, float rate, float swings) => Mathf.Exp(-rate * x) * Mathf.Cos(swings * Mathf.PI * x);

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>A bump from 0 up to 1 and back over [a, b].</summary>
        static float Bump(float p, float a, float b) => p <= a || p >= b ? 0f : Mathf.Sin(Mathf.PI * (p - a) / (b - a));

        /// <summary>
        /// The held-out weapon through its use (p 0..1, since: seconds since its last shot left, t the clock): its turn from the
        /// aim (degrees, + up), how far it is pushed along the aim (px, − back), its rise (px, + up).
        /// </summary>
        public static void Motion(ItemDef d, float p, float since, float t, out float turn, out float push, out float rise)
        {
            turn = push = rise = 0f;
            var look = Of(d);
            bool quick = d.UseTime * 60f <= 12f;
            switch (look.Kind)
            {
                case Kind.Bow:
                {
                    // Loosed: snapped forward, quivering; drawn again for the next, the bow eased back and tipped up.
                    float k = Mathf.Exp(-since * 16f);
                    float draw = Smooth((p - 0.45f) / 0.5f);
                    push = 2.5f * k * Mathf.Cos(since * 45f) - (quick ? 1.5f : 3f) * draw;
                    turn = 4f * Spring(since * 3f, 3f, 3f) * k + 2f * draw;
                    break;
                }
                case Kind.Repeater:
                {
                    // The kick, then the crank as it is wound for the next bolt.
                    float k = Mathf.Exp(-since * 12f);
                    turn = (quick ? 4f : 7f) * k - 2f * Bump(p, 0.45f, 0.85f);
                    push = -(quick ? 3f : 4.5f) * k - 1.5f * Bump(p, 0.45f, 0.85f);
                    break;
                }
                case Kind.Pistol:
                {
                    // Jumped up and back, settling.
                    float k = Mathf.Exp(-since * 9f);
                    turn = (quick ? 10f : 18f) * k * Mathf.Cos(Mathf.Min(since * 9f, Mathf.PI / 2f));
                    push = -3f * k;
                    break;
                }
                case Kind.Rifle:
                {
                    // Kicked into the shoulder, eased home; the bolt worked mid-use (a dip and a tug back).
                    float k = Mathf.Exp(-since * 6f);
                    float bolt = Bump(p, 0.4f, 0.75f);
                    turn = 9f * k - 3f * bolt;
                    push = -6f * k - 2f * bolt;
                    rise = -1f * bolt;
                    break;
                }
                case Kind.Auto:
                {
                    // Shuddering as it fires, a little kick each round.
                    float k = Mathf.Exp(-since * 20f);
                    turn = 2.5f * k + 1.2f * Mathf.Sin(t * 53f);
                    push = -2.5f * k - 0.6f * Mathf.Sin(t * 37f);
                    break;
                }
                case Kind.Shotgun:
                {
                    // Kicked high, then pumped (pulled back and forward with the muzzle dipped).
                    float k = Mathf.Exp(-since * 6.5f);
                    float pump = Bump(p, 0.45f, 0.8f);
                    turn = 22f * k - 5f * pump;
                    push = -7f * k - 3f * pump;
                    break;
                }
                case Kind.Launcher:
                {
                    // Shoved back and down on the shoulder, slowly up again.
                    float k = Mathf.Exp(-since * 4.5f);
                    turn = 12f * k;
                    push = -8f * k;
                    rise = -2f * k;
                    break;
                }
                case Kind.Cannon:
                {
                    float k = Mathf.Exp(-since * 7f);
                    turn = 8f * Spring(since * 2f, 2.5f, 2f) * k + (quick ? 0f : 2f) * k;
                    push = -5f * k;
                    break;
                }
                case Kind.Sprayer:
                    // Swayed as it pours, shaking with it.
                    turn = 2.5f * Mathf.Sin(t * 6f) + 0.8f * Mathf.Sin(t * 41f);
                    push = -1f - 0.8f * Mathf.Sin(t * 29f);
                    break;
                case Kind.Blowpipe:
                {
                    // Puffed: pushed out from the lips, drawn back for breath.
                    float k = Mathf.Exp(-since * 10f);
                    push = 2.5f * k - 1.5f * Bump(p, 0.5f, 0.95f);
                    turn = -2f * k + 1.5f * Bump(p, 0.5f, 0.95f);
                    break;
                }
            }
        }

        /// <summary>
        /// A thrown weapon's arm (degrees from level for the way he faces, + up) at p of its use toward the aim: flung down
        /// through the aim as the use starts, following through low, then drawn back over the shoulder with the next one.
        /// A lobbed one is thrown higher and drawn back lower.
        /// </summary>
        public static float ThrowAngle(float p, float aimDeg, bool lob)
        {
            float release = aimDeg + (lob ? 25f : 12f), low = aimDeg - (lob ? 30f : 55f), back = aimDeg + (lob ? 115f : 145f);
            if (p < 0.22f) return Mathf.Lerp(release, low, 1f - (1f - p / 0.22f) * (1f - p / 0.22f));
            if (p < 0.5f) return Mathf.Lerp(low, aimDeg, Smooth((p - 0.22f) / 0.28f));
            return Mathf.Lerp(aimDeg, back, Smooth((p - 0.5f) / 0.45f));
        }
    }
}
