using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The remade true melee swings (the user's 10-26 "重制一整套的真近战武器方案", then "全是问题": forward cuts only, aimed at
    /// the cursor, cuts that fit the weapon, the sword itself moving, a trail that follows it, the shots at the game's pace).
    /// Every sword, club, axe, sickle, claw and knife whose own blade hits (not a tool, a spear, a yoyo, a flail, a whip or a
    /// stab) swings a combo of its own, one cut a use: the game's swing time and shots, the next cut while the button is held
    /// or pressed again soon, the first again after a pause. Its cuts are of its kind: a sword chops (竖劈), rises (上撩),
    /// sweeps level (横劈, the blade foreshortened), cuts aslant (斜斩) and thrusts (突刺); a greatsword cleaves (重劈), sweeps
    /// heavily and smashes; a club, a mace or a hand smashes (砸) and swings across, never thrusts; an axe chops and cleaves; a
    /// sickle reaps; claws rake and flurry, a knife stabs in a flurry, scissors snip; a katana draws (拔刀斩); a phase blade
    /// flows. Some cuts take the body with them (the user's "多搞一种攻击姿态，除了横扫和竖劈再搞点创新"): a run in
    /// stabbing that stops at the enemy, then a cut up (突进斩); the katana's draw on the run (一闪); a spring up and the
    /// weapon brought down on the ground (跃斩); a cut up rising with it (升龙斩); the tip dragged along the ground in
    /// sparks, then flicked up (拖刀上撩); and in the air with the cursor well below, every weapon's thrust straight down,
    /// bouncing off what it hits as the nail's down slash does (空中下刺). All of them in front of the player, about the line
    /// to the cursor (a smash downward). Each cut hits an enemy once
    /// (a cut of several strokes shares its damage), 5-15% harder than the game's swing, 15-25% further, the last of a
    /// combo knocking back harder. Its look (SwordFx's art part) is the weapon's own picture laid along the blade and a
    /// trail traced behind its edge each frame in the colours of its sprite, grander stage by stage. The swords whose
    /// slash is a projectile (Night's Edge, Excalibur, the Terra Blade...) swing so too, their other shots as ever.
    /// The game's own swing (useStyle 1, MeleeHits.Tick) is all still there: a world made with Terraria's weapons
    /// (World.WeaponMode, every save from before the choice) swings it.
    /// </summary>
    internal static class BladeArts
    {
        /// <summary>The remade weapons are this save's (World.WeaponMode, chosen with its world; Terraria's own in a save from before).</summary>
        public static bool Enabled => WeaponRefitting.Enabled;

        /// <summary>Dev: the session's weapons set (the suites comparing the remade weapons with Terraria's); the save's own come back as it is read.</summary>
        public static void Set(bool on)
        {
            var k = on ? World.WeaponKind.New : World.WeaponKind.Vanilla;
            if (World.WeaponMode.Current == k) return;
            World.WeaponMode.Current = k;
            Plugin.Log.LogInfo($"[武器] 本局武器：{World.WeaponMode.NameOf(k)}");
        }

        /// <summary>The remade swing is this item's now (not while the Gravity Globe has the player upside down: the game's swing then).</summary>
        public static bool Active(ItemDef d) => d != null && WeaponRefitting.EnabledFor(d) && Is(d) && !Player.GravityGlobe.Flipped;

        /// <summary>
        /// The feet the player is drawn on (world): the pose's, which a mount's saddle and the Portable Stool raise, else
        /// the hero's box. The cuts turn about the shoulder 25 px above them.
        /// </summary>
        public static Vector2 Feet(HeroController hero)
        {
            var mod = TerraMod.Instance;
            if (mod != null && mod.NetPose.Shown) return mod.NetPose.Feet;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            return new Vector2(b.center.x, b.min.y + Player.TerraMovement.StepOffset);
        }

        /// <summary>The swords whose slash is Terraria's own projectile (AI_190): the remade cut is their slash now, their other shots as ever.</summary>
        public static bool SlashFinisher(ItemDef d) =>
            d.Fx == MeleeFx.NightsEdge || d.Fx == MeleeFx.Excalibur || d.Fx == MeleeFx.TerraBlade || d.Fx == MeleeFx.TrueNightsEdge
            || d.Fx == MeleeFx.TrueExcalibur || d.Fx == MeleeFx.HorsemansBlade || d.Fx == MeleeFx.ChlorophyteClaymore;

        /// <summary>A true melee weapon: a melee swing whose blade hits, tools, the utility staffs and the mounts' gear apart.</summary>
        public static bool Is(ItemDef d)
        {
            if (d == null || !d.IsWeapon || d.Class != DamageClass.Melee || d.Thrust || d.HoldOut || d.IsWhip || d.HoldUp || d.NoUseGraphic) return false;
            if (d.PickPower > 0 || d.AxePower > 0 || d.HammerPower > 0) return false;
            if (d.Type == ItemType.StaffofRegrowth || d.Type == ItemType.GravediggerShovel || d.Type == ItemType.BreathingReed) return false;
            return !d.NoMelee || SlashFinisher(d);
        }

        // ------------------------------------------------------------------ the cuts

        /// <summary>What the weapon is, which decides how it strikes.</summary>
        internal enum Kind : byte { Sword, Great, Blunt, Axe, Sickle, Claw, Knife, Scissors, Katana, Light }

        internal enum Shape : byte { Arc, Ellipse, Thrust, Cleave, Smash, Iai, Drag, Plunge }

        /// <summary>
        /// One stroke of a cut: the blade's way through it, degrees counterclockwise from the line to the cursor (Absolute:
        /// from level, a smash goes down whatever the aim). Arc: the blade at full length from From to To. Ellipse: its tip on
        /// an ellipse squashed across the aim, the blade foreshortened as it points toward or away from the eye (a level cut);
        /// From and To its angle on the ellipse. Thrust: along the aim, out Ext px and back. Cleave and Smash: raised slowly
        /// past From, brought down fast to To and held there (a smash strikes the ground). Iai: held drawn, then one fast level
        /// cut. Drag: the tip on the ground ahead pushed along it, then flicked up to From. Plunge: straight down from the
        /// hand (in the air), thrust out and held. Start and End: its share of the use.
        /// </summary>
        internal sealed class Stroke
        {
            public Shape Shape;
            public float From, To, Squash = 1f, Reach = 1f, Ext, Tilt;
            public float Start, End = 1f;
            public bool Absolute;
        }

        /// <summary>
        /// A cut: its strokes, and how the body goes with it. Dash: a run forward (px a tick at its start, easing off) from
        /// DashFrom to DashTo of the use, stopped by an enemy just ahead. Hop: a spring up (px a tick) at HopAt from the
        /// ground, HopForward with it. Dive: down that fast once it cuts; Pogo: a hit bounces the player up off it.
        /// </summary>
        internal sealed class Move
        {
            public string Name;
            public Stroke[] Strokes;
            public float Dash, DashFrom, DashTo, Hop, HopAt, HopForward, Dive;
            public bool Pogo;
        }

        internal sealed class Art
        {
            public ItemType Type;
            public Kind Kind;
            public Move[] Combo;
            public float Boost = 1.05f, Reach = 1.15f;
            /// <summary>0..5: how grand its trail and finish are.</summary>
            public int Level;
            public Color Dark, Main, Light;
            public bool Rainbow;
            /// <summary>The motes its edge sheds besides its colours' (Terraria dust: fire, ice, spores, shadow, glints, blood, stars), 0 none.</summary>
            public int Mote;
            public string Signature;
            /// <summary>What its cuts shed and how its trail looks (SwordFx), from what the weapon is.</summary>
            public Theme Theme;
            /// <summary>Its cuts may take the body with them (runs, springs, the plunge): a katana, a phase blade, claws, a knife, a greatsword, the great swords of the end.</summary>
            public bool Mobile;
        }

        /// <summary>
        /// What a weapon's cuts look like beyond its colours, from what it is and what its description says: a blade of fire
        /// trails flame and embers, an ice blade frost, the grass and seed swords leaves, the shadow blades smoke, the holy
        /// ones motes of light, the star swords stars, the butchers blood, the Bee Keeper honey and bees, the Bladetongue
        /// ichor, the Flymeal flies, the candy cane its stripes, the Tax Collector's cane coins, the dye scimitar and the
        /// scissors splashes of colour, the phase blades a white-hot core in a glow, the Influx Waver arcs, the Terra Blade
        /// its green, the Meowmere a rainbow, the mandible sand, the clubberfish water, the bone sword bone, the Christmas
        /// tree sword lights, the claymore spores, the katana petals, the Muramasa its blue flame, the sickle hay, Lucy hearts,
        /// the falcon feathers, the orichalcum petals, the slapping hand a gust, the zombie's arm rot; plain steel sparks.
        /// </summary>
        internal enum Theme : byte
        {
            Steel, Wood, Fire, Ice, Leaf, Shadow, Holy, Star, Blood, Honey, Ichor, Fly, Candy, Coin, Dye, Saber, Electric, Terra,
            Rainbow, Sand, Water, Bone, Festive, Spore, Sakura, Spirit, Hay, Heart, Feather, Petal, Wind, Snow, Rot,
        }

        static readonly Dictionary<ItemType, Theme> themes = new Dictionary<ItemType, Theme>
        {
            [ItemType.WoodenSword] = Theme.Wood, [ItemType.BorealWoodSword] = Theme.Snow, [ItemType.PalmWoodSword] = Theme.Sand,
            [ItemType.RichMahoganySword] = Theme.Leaf, [ItemType.EbonwoodSword] = Theme.Shadow, [ItemType.ShadewoodSword] = Theme.Blood,
            [ItemType.AshWoodSword] = Theme.Fire, [ItemType.PearlwoodSword] = Theme.Holy, [ItemType.CactusSword] = Theme.Leaf,
            [ItemType.ZombieArm] = Theme.Rot, [ItemType.Katana] = Theme.Sakura, [ItemType.Muramasa] = Theme.Spirit, [ItemType.RevenantEdge] = Theme.Spirit,
            [ItemType.BatBat] = Theme.Shadow, [ItemType.BladeofGrass] = Theme.Leaf, [ItemType.Sickle] = Theme.Hay,
            [ItemType.IceBlade] = Theme.Ice, [ItemType.Frostbrand] = Theme.Ice, [ItemType.IceSickle] = Theme.Ice,
            [ItemType.LightsBane] = Theme.Shadow, [ItemType.BloodButcherer] = Theme.Blood, [ItemType.TentacleSpike] = Theme.Blood,
            [ItemType.Starfury] = Theme.Star, [ItemType.StarWrath] = Theme.Star, [ItemType.EnchantedSword] = Theme.Star,
            [ItemType.CandyCaneSword] = Theme.Candy, [ItemType.PurpleClubberfish] = Theme.Water, [ItemType.StylistKilLaKillScissorsIWish] = Theme.Dye,
            [ItemType.AntlionClaw] = Theme.Sand, [ItemType.DyeTradersScimitar] = Theme.Dye, [ItemType.LucyTheAxe] = Theme.Heart,
            [ItemType.BeeKeeper] = Theme.Honey, [ItemType.BoneSword] = Theme.Bone, [ItemType.TaxCollectorsStickOfDoom] = Theme.Coin,
            [ItemType.FieryGreatsword] = Theme.Fire, [ItemType.FalconBlade] = Theme.Feather, [ItemType.NightsEdge] = Theme.Shadow,
            [ItemType.TrueNightsEdge] = Theme.Shadow, [ItemType.DeathSickle] = Theme.Shadow, [ItemType.OrichalcumSword] = Theme.Petal,
            [ItemType.Excalibur] = Theme.Holy, [ItemType.TrueExcalibur] = Theme.Holy, [ItemType.Keybrand] = Theme.Holy,
            [ItemType.Flymeal] = Theme.Fly, [ItemType.BeamSword] = Theme.Saber, [ItemType.SlapHand] = Theme.Wind,
            [ItemType.WaffleIron] = Theme.Honey, [ItemType.HamBat] = Theme.Honey, [ItemType.Bladetongue] = Theme.Ichor, [ItemType.TheHorsemansBlade] = Theme.Fire,
            [ItemType.ChlorophyteClaymore] = Theme.Spore, [ItemType.TerraBlade] = Theme.Terra, [ItemType.Seedler] = Theme.Leaf,
            [ItemType.InfluxWaver] = Theme.Electric, [ItemType.PsychoKnife] = Theme.Blood, [ItemType.ChristmasTreeSword] = Theme.Festive,
            [ItemType.DD2SquireDemonSword] = Theme.Fire, [ItemType.DD2SquireBetsySword] = Theme.Fire, [ItemType.Meowmere] = Theme.Rainbow,
            [ItemType.FetidBaghnakhs] = Theme.Rot,
            // The spears (their techniques, TerraProjectilesSpearArts).
            [ItemType.Trident] = Theme.Water, [ItemType.TheRottedFork] = Theme.Rot, [ItemType.Swordfish] = Theme.Water,
            [ItemType.ObsidianSwordfish] = Theme.Fire, [ItemType.SlimeSpear] = Theme.Water, [ItemType.DarkLance] = Theme.Shadow,
            [ItemType.ThunderSpear] = Theme.Electric, [ItemType.PalladiumPike] = Theme.Fire, [ItemType.OrichalcumHalberd] = Theme.Petal,
            [ItemType.AdamantiteGlaive] = Theme.Fire, [ItemType.Gungnir] = Theme.Holy, [ItemType.MushroomSpear] = Theme.Spirit,
            [ItemType.ChlorophytePartisan] = Theme.Spore, [ItemType.NorthPole] = Theme.Snow,
            // The held blades (TerraProjectilesHeldArts).
            [ItemType.Arkhalis] = Theme.Spirit, [ItemType.Terragrim] = Theme.Wind, [ItemType.PiercingStarlight] = Theme.Star,
        };

        /// <summary>The weapons whose cuts move the body: what darts and springs by nature, and the great swords of the end.</summary>
        static bool Moves(ItemDef d, Kind kind) =>
            kind == Kind.Katana || kind == Kind.Light || kind == Kind.Claw || kind == Kind.Knife || kind == Kind.Great
            || d.Type == ItemType.FalconBlade || d.Type == ItemType.DeathSickle || d.Type == ItemType.TerraBlade || d.Type == ItemType.TrueExcalibur
            || d.Type == ItemType.TrueNightsEdge || d.Type == ItemType.InfluxWaver || d.Type == ItemType.StarWrath || d.Type == ItemType.Meowmere;

        static readonly Dictionary<ItemType, Art> arts = new Dictionary<ItemType, Art>();

        public static Art Of(ItemDef d)
        {
            if (d == null) return null;
            if (arts.TryGetValue(d.Type, out var a)) return a;
            a = Build(d);
            arts[d.Type] = a;
            return a;
        }

        /// <summary>The weapons that aren't swords, by kind.</summary>
        static readonly Dictionary<ItemType, Kind> kinds = new Dictionary<ItemType, Kind>
        {
            [ItemType.BreakerBlade] = Kind.Great, [ItemType.FieryGreatsword] = Kind.Great, [ItemType.ChlorophyteClaymore] = Kind.Great,
            [ItemType.TheHorsemansBlade] = Kind.Great, [ItemType.Keybrand] = Kind.Great, [ItemType.DD2SquireBetsySword] = Kind.Great,
            [ItemType.BatBat] = Kind.Blunt, [ItemType.HamBat] = Kind.Blunt, [ItemType.PurpleClubberfish] = Kind.Blunt, [ItemType.TentacleSpike] = Kind.Blunt,
            [ItemType.ZombieArm] = Kind.Blunt, [ItemType.SlapHand] = Kind.Blunt, [ItemType.WaffleIron] = Kind.Blunt,
            [ItemType.TaxCollectorsStickOfDoom] = Kind.Blunt, [ItemType.CandyCaneSword] = Kind.Blunt,
            [ItemType.LucyTheAxe] = Kind.Axe, [ItemType.BloodButcherer] = Kind.Axe,
            [ItemType.Sickle] = Kind.Sickle, [ItemType.IceSickle] = Kind.Sickle, [ItemType.DeathSickle] = Kind.Sickle,
            [ItemType.BladedGlove] = Kind.Claw, [ItemType.FetidBaghnakhs] = Kind.Claw, [ItemType.AntlionClaw] = Kind.Claw,
            [ItemType.PsychoKnife] = Kind.Knife, [ItemType.StylistKilLaKillScissorsIWish] = Kind.Scissors,
            [ItemType.Katana] = Kind.Katana, [ItemType.Muramasa] = Kind.Katana,
            [ItemType.BluePhaseblade] = Kind.Light, [ItemType.RedPhaseblade] = Kind.Light, [ItemType.GreenPhaseblade] = Kind.Light,
            [ItemType.PurplePhaseblade] = Kind.Light, [ItemType.WhitePhaseblade] = Kind.Light, [ItemType.YellowPhaseblade] = Kind.Light,
            [ItemType.OrangePhaseblade] = Kind.Light, [ItemType.PinkPhaseblade] = Kind.Light, [ItemType.RainbowPhaseblade] = Kind.Light,
            [ItemType.BluePhasesaber] = Kind.Light, [ItemType.RedPhasesaber] = Kind.Light, [ItemType.GreenPhasesaber] = Kind.Light,
            [ItemType.PurplePhasesaber] = Kind.Light, [ItemType.WhitePhasesaber] = Kind.Light, [ItemType.YellowPhasesaber] = Kind.Light,
            [ItemType.OrangePhasesaber] = Kind.Light, [ItemType.PinkPhasesaber] = Kind.Light, [ItemType.RainbowPhasesaber] = Kind.Light,
        };

        /// <summary>Each weapon's combo, a cut a use: the moves' names in order (Move()).</summary>
        static readonly Dictionary<ItemType, string> combos = new Dictionary<ItemType, string>
        {
            // Stage 0: one or two cuts.
            [ItemType.WoodenSword] = "Chop", [ItemType.BorealWoodSword] = "Sweep", [ItemType.PalmWoodSword] = "Diagonal",
            [ItemType.RichMahoganySword] = "Rise", [ItemType.EbonwoodSword] = "Chop Overhead", [ItemType.ShadewoodSword] = "Diagonal Uppercut",
            [ItemType.CopperBroadsword] = "Chop Sweep", [ItemType.TinBroadsword] = "Sweep Chop", [ItemType.LeadBroadsword] = "Diagonal LowSweep",
            [ItemType.IronBroadsword] = "Crescent Chop", [ItemType.SilverBroadsword] = "Twin", [ItemType.TungstenBroadsword] = "Sweep LowSweep",
            [ItemType.GoldBroadsword] = "Chop Diagonal", [ItemType.PlatinumBroadsword] = "Cross XCut", [ItemType.CactusSword] = "Chop Backsweep",
            [ItemType.ZombieArm] = "Leap Chop", [ItemType.Katana] = "Iai Chop", [ItemType.RevenantEdge] = "LowSweep Cross",
            [ItemType.BatBat] = "Sweep Leap", [ItemType.BladeofGrass] = "Sweep Uppercut",
            // Stage 1: two.
            [ItemType.Sickle] = "Reap Uppercut", [ItemType.IceBlade] = "Flourish Rise", [ItemType.LightsBane] = "LowSweep Chop",
            [ItemType.BloodButcherer] = "Leap Sweep", [ItemType.Starfury] = "Uppercut Diagonal", [ItemType.EnchantedSword] = "Overhead Sweep",
            [ItemType.TentacleSpike] = "Chop Smash",
            // Stage 2: two or three.
            [ItemType.AshWoodSword] = "Uppercut Chop", [ItemType.BluePhaseblade] = "Crescent Sweep Rise", [ItemType.RedPhaseblade] = "Uppercut Backsweep",
            [ItemType.GreenPhaseblade] = "Rise Twin", [ItemType.PurplePhaseblade] = "XCut Cross", [ItemType.WhitePhaseblade] = "Twin Crescent",
            [ItemType.YellowPhaseblade] = "Backsweep Diagonal Rise", [ItemType.OrangePhaseblade] = "Sweep Twin", [ItemType.PinkPhaseblade] = "Rise Flourish Sweep",
            [ItemType.RainbowPhaseblade] = "Cross Uppercut", [ItemType.CandyCaneSword] = "Sweep Reap Leap", [ItemType.PurpleClubberfish] = "Backsweep Leap",
            [ItemType.StylistKilLaKillScissorsIWish] = "Snip Flurry", [ItemType.AntlionClaw] = "Flurry Overhead", [ItemType.Muramasa] = "Iai Cross Uppercut",
            [ItemType.DyeTradersScimitar] = "Diagonal LowSweep Rise", [ItemType.LucyTheAxe] = "Chop Cleave Leap", [ItemType.BeeKeeper] = "Sweep Crescent Rise",
            [ItemType.BoneSword] = "Chop LowSweep Crescent", [ItemType.TaxCollectorsStickOfDoom] = "Backsweep Chop", [ItemType.FieryGreatsword] = "LowSweep HeavySweep Leap",
            [ItemType.FalconBlade] = "Twin XCut Diagonal", [ItemType.NightsEdge] = "Diagonal Uppercut Flourish",
            // Stage 3: three.
            [ItemType.PearlwoodSword] = "Chop Uppercut Sweep", [ItemType.BladedGlove] = "Overhead Rake", [ItemType.BluePhasesaber] = "Sweep Crescent Uppercut",
            [ItemType.RedPhasesaber] = "Cross LowSweep Uppercut", [ItemType.GreenPhasesaber] = "Rise Sweep Twin", [ItemType.PurplePhasesaber] = "Diagonal Cross XCut",
            [ItemType.WhitePhasesaber] = "Twin Sweep Rise", [ItemType.YellowPhasesaber] = "Sweep Backsweep Uppercut", [ItemType.OrangePhasesaber] = "Rise Diagonal Cross",
            [ItemType.PinkPhasesaber] = "Flourish Twin Uppercut", [ItemType.RainbowPhasesaber] = "Cross Overhead Uppercut", [ItemType.CobaltSword] = "Chop Sweep Uppercut",
            [ItemType.PalladiumSword] = "Diagonal Chop Crescent", [ItemType.MythrilSword] = "LowSweep Diagonal Uppercut", [ItemType.OrichalcumSword] = "Rise Cross XCut",
            [ItemType.AdamantiteSword] = "Chop LowSweep Flourish", [ItemType.TitaniumSword] = "Overhead Diagonal Uppercut", [ItemType.BreakerBlade] = "Cleave LowSweep Leap",
            [ItemType.FetidBaghnakhs] = "Rake Flurry Uppercut", [ItemType.Excalibur] = "Sweep Uppercut Crescent",
            // Stage 4: three or four.
            [ItemType.Flymeal] = "Chop Uppercut Sweep XCut", [ItemType.BeamSword] = "Flourish Diagonal Uppercut Sweep", [ItemType.Cutlass] = "Diagonal LowSweep Overhead Cross",
            [ItemType.SlapHand] = "Sweep Backsweep Leap", [ItemType.HamBat] = "Backsweep Leap Smash", [ItemType.Frostbrand] = "Crescent Sweep Uppercut Chop",
            [ItemType.WaffleIron] = "Chop Leap Sweep", [ItemType.Bladetongue] = "LowSweep Diagonal Uppercut Chop", [ItemType.DD2SquireDemonSword] = "Diagonal XCut Uppercut Cross",
            [ItemType.IceSickle] = "Reap LowSweep Uppercut", [ItemType.DeathSickle] = "Sweep Reap Flourish Uppercut", [ItemType.TheHorsemansBlade] = "Cleave Sweep Uppercut Leap",
            [ItemType.ChlorophyteClaymore] = "HeavySweep LowSweep Cleave Leap", [ItemType.TrueNightsEdge] = "Diagonal Uppercut Cross Overhead", [ItemType.TrueExcalibur] = "Crescent Rise Diagonal Uppercut",
            [ItemType.TerraBlade] = "XCut Sweep Uppercut Cross",
            // Stage 5: four.
            [ItemType.Seedler] = "Sweep LowSweep Uppercut Flourish", [ItemType.InfluxWaver] = "Overhead Cross Uppercut Sweep", [ItemType.PsychoKnife] = "Lunge StabFlurry Flurry Diagonal",
            [ItemType.ChristmasTreeSword] = "Chop LowSweep Backsweep Uppercut", [ItemType.Keybrand] = "Cleave Uppercut HeavySweep Leap", [ItemType.DD2SquireBetsySword] = "LowSweep Cleave Uppercut Leap",
            [ItemType.StarWrath] = "Crescent Diagonal Cross Uppercut", [ItemType.Meowmere] = "Uppercut XCut Cross LowSweep",
        };

        /// <summary>The moves each kind may make, for a weapon not in the table.</summary>
        static string[] PoolOf(Kind k)
        {
            switch (k)
            {
                case Kind.Great: return new[] { "Cleave", "HeavySweep", "Chop", "Smash" };
                case Kind.Blunt: return new[] { "Smash", "Sweep", "Backsweep", "Chop" };
                case Kind.Axe: return new[] { "Chop", "Cleave", "Diagonal", "Sweep" };
                case Kind.Sickle: return new[] { "Reap", "Sweep", "Rise", "Backsweep" };
                case Kind.Claw: return new[] { "Rake", "Flurry", "Diagonal", "Rise" };
                case Kind.Knife: return new[] { "StabFlurry", "Flurry", "Thrust", "Diagonal" };
                case Kind.Scissors: return new[] { "Snip", "Flurry", "Thrust" };
                case Kind.Katana: return new[] { "Iai", "Chop", "Diagonal", "Cross" };
                case Kind.Light: return new[] { "Sweep", "Diagonal", "Rise", "Twin", "Cross" };
                default: return new[] { "Chop", "Sweep", "Diagonal", "Rise", "Thrust", "Cross" };
            }
        }

        static Art Build(ItemDef d)
        {
            var art = new Art { Type = d.Type, Kind = kinds.TryGetValue(d.Type, out var k) ? k : Kind.Sword };
            int stage = Mathf.Clamp(d.Stage, 0, 5);
            art.Level = Mathf.Clamp(stage + (d.Rare >= 8 && stage < 5 ? 1 : 0), 0, 5);
            art.Boost = 1.05f + 0.02f * art.Level;
            // The user's 10-26 "范围也提高": 30-50% further than the game's swing.
            art.Reach = 1.3f + 0.04f * art.Level;
            art.Theme = themes.TryGetValue(d.Type, out var theme) ? theme : art.Kind == Kind.Light ? Theme.Saber : Theme.Steel;
            art.Mobile = Moves(d, art.Kind);
            string combo;
            if (!combos.TryGetValue(d.Type, out combo))
            {
                var pool = PoolOf(art.Kind);
                int n = Mathf.Clamp(1 + art.Level / 2 + (art.Level >= 4 ? 1 : 0), 1, 4);
                var parts = new List<string>();
                for (int i = 0; i < n; i++) parts.Add(pool[((int)d.Type + i * 2) % pool.Length]);
                combo = string.Join(" ", parts);
            }
            var names = combo.Split(' ');
            art.Combo = new Move[names.Length];
            for (int i = 0; i < names.Length; i++) art.Combo[i] = MoveOf(names[i]);
            art.Signature = art.Kind + ":" + combo;
            Colours(d, art);
            art.Mote = MoteOf(d);
            return art;
        }

        static Stroke Arc(float from, float to, float start = 0f, float end = 1f, float reach = 1f) =>
            new Stroke { Shape = Shape.Arc, From = from, To = to, Start = start, End = end, Reach = reach };

        static Stroke Ellipse(float from, float to, float squash, float start = 0f, float end = 1f, float reach = 1f, float tilt = 0f) =>
            new Stroke { Shape = Shape.Ellipse, From = from, To = to, Squash = squash, Start = start, End = end, Reach = reach, Tilt = tilt };

        static Stroke Thrust(float tilt, float ext, float start = 0f, float end = 1f) =>
            new Stroke { Shape = Shape.Thrust, From = 0f, To = 0f, Tilt = tilt, Ext = ext, Start = start, End = end };

        /// <summary>A move by its name: its strokes (each a share of the use).</summary>
        static Move MoveOf(string name)
        {
            Stroke[] s;
            switch (name)
            {
                case "Chop": s = new[] { Arc(110f, -70f) }; break;
                case "Rise": s = new[] { Arc(-75f, 100f) }; break;
                case "Diagonal": s = new[] { Ellipse(100f, -80f, 0.6f) }; break;
                case "Sweep": s = new[] { Ellipse(85f, -85f, 0.3f) }; break;
                case "Backsweep": s = new[] { Ellipse(-85f, 85f, 0.3f) }; break;
                case "HeavySweep": s = new[] { Ellipse(95f, -95f, 0.35f, reach: 1.1f) }; break;
                case "Reap": s = new[] { Ellipse(50f, -100f, 0.45f, reach: 1.05f, tilt: -12f) }; break;
                case "Thrust": s = new[] { Thrust(0f, 20f) }; break;
                case "StabFlurry": s = new[] { Thrust(8f, 14f, 0f, 0.34f), Thrust(-6f, 14f, 0.34f, 0.67f), Thrust(0f, 18f, 0.67f, 1f) }; break;
                case "Cleave": s = new[] { new Stroke { Shape = Shape.Cleave, From = 120f, To = -65f, Reach = 1.1f } }; break;
                case "Smash": s = new[] { new Stroke { Shape = Shape.Smash, From = 125f, To = -80f, Reach = 1.05f, Absolute = true } }; break;
                case "Iai": s = new[] { new Stroke { Shape = Shape.Iai, From = 65f, To = -70f, Squash = 0.22f, Reach = 1.15f } }; break;
                case "Flurry": s = new[] { Arc(60f, -40f, 0f, 0.33f, 0.9f), Arc(-45f, 55f, 0.33f, 0.66f, 0.9f), Arc(70f, -55f, 0.66f, 1f, 0.95f) }; break;
                case "Rake": s = new[] { Ellipse(55f, -55f, 0.5f, 0f, 0.34f, 0.95f, 14f), Ellipse(55f, -55f, 0.5f, 0.33f, 0.67f, 0.95f, 0f), Ellipse(55f, -55f, 0.5f, 0.66f, 1f, 0.95f, -14f) }; break;
                case "Snip": s = new[] { Arc(45f, -45f, 0f, 0.5f, 0.85f), Arc(-45f, 45f, 0.5f, 1f, 0.85f) }; break;
                case "Twin": s = new[] { Arc(105f, -60f, 0f, 0.5f), Arc(-65f, 95f, 0.5f, 1f) }; break;
                case "Cross": s = new[] { Arc(110f, -70f, 0f, 0.5f), Ellipse(85f, -85f, 0.3f, 0.5f, 1f) }; break;
                // 大上段劈: raised high over the head, slowly, then brought down hard.
                case "Overhead": s = new[] { new Stroke { Shape = Shape.Cleave, From = 135f, To = -70f, Reach = 1.1f } }; break;
                // 下段横扫: low across in front, at the knees.
                case "LowSweep": s = new[] { Ellipse(70f, -70f, 0.25f, reach: 1.05f, tilt: -30f) }; break;
                // 月牙斩: a wide crescent from low ahead to over the head.
                case "Crescent": s = new[] { Ellipse(-75f, 125f, 0.8f, reach: 1.15f) }; break;
                // 挽剑花: two quick tilted sweeps.
                case "Flourish": s = new[] { Ellipse(60f, -60f, 0.5f, 0f, 0.5f, 0.95f, 20f), Ellipse(60f, -60f, 0.5f, 0.5f, 1f, 0.95f, -20f) }; break;
                // X 斩: a cut down aslant, then one up aslant across it.
                case "XCut": s = new[] { Ellipse(100f, -80f, 0.6f, 0f, 0.5f, 1f, 10f), Ellipse(-80f, 100f, 0.6f, 0.5f, 1f, 1f, -10f) }; break;
                // With the body: a run in stabbing, then a cut up as it stops (突进斩).
                case "Lunge": return new Move { Name = name, Strokes = new[] { Thrust(-6f, 24f, 0f, 0.5f), Arc(-30f, 80f, 0.5f, 1f) }, Dash = 4f, DashFrom = 0.04f, DashTo = 0.38f };
                // The katana's draw on the run (一闪).
                case "IaiDash": return new Move { Name = name, Strokes = new[] { new Stroke { Shape = Shape.Iai, From = 65f, To = -70f, Squash = 0.22f, Reach = 1.15f } }, Dash = 4.5f, DashFrom = 0.18f, DashTo = 0.46f };
                // A spring up, the weapon raised, brought down on the ground as he comes down (跃斩).
                case "Leap": return new Move { Name = name, Strokes = new[] { new Stroke { Shape = Shape.Smash, From = 125f, To = -80f, Reach = 1.1f, Absolute = true } }, Hop = 4.6f, HopAt = 0f, HopForward = 2f };
                // A cut from low ahead to high, rising with it (升龙斩).
                case "Uppercut": return new Move { Name = name, Strokes = new[] { Arc(-85f, 115f, 0f, 1f, 1.1f) }, Hop = 4.2f, HopAt = 0.15f };
                // The tip pushed along the ground, sparks off it, then flicked up (拖刀上撩).
                case "Drag": return new Move { Name = name, Strokes = new[] { new Stroke { Shape = Shape.Drag, From = 75f, Reach = 1.05f } }, Dash = 3f, DashFrom = 0f, DashTo = 0.45f };
                // In the air, aimed well down: straight down, falling faster, a hit bouncing him up (空中下刺).
                case "Plunge": return new Move { Name = name, Strokes = new[] { new Stroke { Shape = Shape.Plunge, Reach = 1.05f, Absolute = true } }, Dive = 9f, Pogo = true };
                default: name = "Chop"; s = new[] { Arc(110f, -70f) }; break;
            }
            return new Move { Name = name, Strokes = s };
        }

        /// <summary>A move by its name, for a right-click art's cut (BladeSkills).</summary>
        public static Move Named(string name) => MoveOf(name);

        /// <summary>A short jab, its way tilted (the claws' rush).</summary>
        public static Move Jab(float tilt) => new Move { Name = "Jab", Strokes = new[] { Thrust(tilt, 16f) } };

        static Move plunge;

        /// <summary>The cut in the air with the cursor well below: every remade weapon's own (it doesn't go on the combo).</summary>
        public static Move Plunge => plunge ?? (plunge = MoveOf("Plunge"));

        /// <summary>Aimed this far down (degrees) in the air, a use is the plunge.</summary>
        public const float PlungeAim = -45f;

        /// <summary>Dev: the body's moves off (the dummy's damage measured standing).</summary>
        internal static bool DevNoBody;

        /// <summary>Dev: runs, springs and bounces made.</summary>
        internal static int DevDashes, DevHops, DevPogos;

        /// <summary>
        /// The body with the cut, each frame of the use (u 0..1): the run's speed while it lasts (no faster than it goes
        /// already, ended by an enemy just ahead: the cut lands on it, the Knight doesn't run into it), the spring once from
        /// the ground, the dive. Not on a mount, a rope or the hook, sliding a wall, dashing, or without control.
        /// </summary>
        public static void Body(Art art, Move m, float u, HeroController hero, float facing, ref bool sprung, ref bool dashed)
        {
            if (DevNoBody || art == null || !art.Mobile || m == null || hero == null) return;
            var cs = hero.cState;
            if (!hero.acceptingInput || hero.controlReqlinquished || cs.dead || cs.transitioning || cs.wallSliding || Player.Mounts.OwnMovement
                || Player.GrapplingHook.IsLatched || Player.TerraMovement.OnRope || Player.TerraMovement.IsDashing) return;
            var rb = hero.GetComponent<Rigidbody2D>();
            if (rb == null) return;
            const float PxT = Player.TerraMovement.PxT;
            if (m.Dash > 0f && u >= m.DashFrom && u <= m.DashTo && !dashed)
            {
                if (EnemyAhead(hero, facing)) dashed = true;
                else
                {
                    float k = 1f - (u - m.DashFrom) / Mathf.Max(0.01f, m.DashTo - m.DashFrom);
                    float vx = facing * m.Dash * (0.35f + 0.65f * k) * PxT;
                    var v = rb.linearVelocity;
                    if (v.x * facing < vx * facing) rb.linearVelocity = new Vector2(vx, v.y);
                    if (u - m.DashFrom < 0.05f) DevDashes++;
                }
            }
            if (m.Dash > 0f && u > m.DashTo) dashed = true;
            if (m.Hop > 0f && !sprung && u >= m.HopAt)
            {
                sprung = true;
                if (cs.onGround)
                {
                    var v = rb.linearVelocity;
                    rb.linearVelocity = new Vector2(v.x + facing * m.HopForward * PxT, m.Hop * PxT);
                    DevHops++;
                }
            }
            if (m.Dive > 0f && u >= 0.2f && !cs.onGround)
            {
                var v = rb.linearVelocity;
                if (v.y > -m.Dive * PxT) rb.linearVelocity = new Vector2(v.x, -m.Dive * PxT);
            }
        }

        /// <summary>The plunge's hit: bounced up off it, as the nail's down slash bounces him.</summary>
        public static void Bounce(HeroController hero)
        {
            var rb = hero != null ? hero.GetComponent<Rigidbody2D>() : null;
            if (rb == null || hero.cState.dead) return;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 7f * Player.TerraMovement.PxT);
            DevPogos++;
        }

        /// <summary>A live enemy's body within a step and a half ahead, at the body's height.</summary>
        static bool EnemyAhead(HeroController hero, float facing)
        {
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var centre = new Vector2(facing > 0f ? b.max.x + 0.75f : b.min.x - 0.75f, b.center.y);
            foreach (var c in Physics2D.OverlapBoxAll(centre, new Vector2(1.5f, b.size.y), 0f))
            {
                if (c == null || c.isTrigger && c.GetComponent<DamageHero>() == null) continue;
                var hm = c.GetComponentInParent<HealthManager>();
                if (hm != null && !hm.isDead && hm.hp > 0 && UI.EnemyHover.RecordOf(hm) != null) return true;
            }
            return false;
        }

        /// <summary>The motes a blade of fire, ice, spores, shadow, light, blood or stars sheds besides its colours'.</summary>
        static int MoteOf(ItemDef d)
        {
            switch (d.Type)
            {
                case ItemType.FieryGreatsword: case ItemType.DD2SquireDemonSword: case ItemType.TheHorsemansBlade: case ItemType.DD2SquireBetsySword: return 6;
                case ItemType.IceBlade: case ItemType.Frostbrand: case ItemType.IceSickle: return 135;
                case ItemType.BladeofGrass: case ItemType.Seedler: case ItemType.ChlorophyteClaymore: case ItemType.CactusSword: return 157;
                case ItemType.LightsBane: case ItemType.NightsEdge: case ItemType.TrueNightsEdge: case ItemType.DeathSickle: return 27;
                case ItemType.Excalibur: case ItemType.TrueExcalibur: case ItemType.PearlwoodSword: case ItemType.Keybrand: return 43;
                case ItemType.BloodButcherer: case ItemType.TentacleSpike: return 5;
                case ItemType.Starfury: case ItemType.StarWrath: case ItemType.EnchantedSword: case ItemType.Meowmere: return 15;
                default: return 0;
            }
        }

        // ------------------------------------------------------------------ where the blade is

        /// <summary>The stroke under way at this point of the use (0..1), and how far through it (0..1).</summary>
        public static int StrokeAt(Move m, float u, out float su)
        {
            var s = m.Strokes;
            for (int i = 0; i < s.Length; i++)
                if (u < s[i].End || i == s.Length - 1)
                {
                    su = Mathf.Clamp01((u - s[i].Start) / Mathf.Max(1e-4f, s[i].End - s[i].Start));
                    return i;
                }
            su = 1f;
            return s.Length - 1;
        }

        /// <summary>
        /// The blade through a stroke (su 0..1): its way, degrees counterclockwise from level for a player facing right (the
        /// aim added unless Absolute), its length against the whole (a level cut's foreshortening), the hand thrust out (px),
        /// and whether it is cutting yet (not while raised, drawn back or held drawn).
        /// </summary>
        public static float Blade(Stroke s, float su, float aim, out float len, out float ext, out bool live, float floor = -90f)
        {
            len = 1f;
            ext = 0f;
            live = true;
            float local;
            switch (s.Shape)
            {
                case Shape.Thrust:
                    ext = su < 0.22f ? -5f * (su / 0.22f) : su < 0.5f ? Mathf.Lerp(-5f, s.Ext, EaseOut((su - 0.22f) / 0.28f)) : Mathf.Lerp(s.Ext, 0f, Mathf.SmoothStep(0f, 1f, (su - 0.5f) / 0.5f));
                    live = su >= 0.25f && su <= 0.7f;
                    local = s.Tilt;
                    break;
                case Shape.Cleave:
                case Shape.Smash:
                {
                    // Brought down no further than the ground ahead (floor: the way to it from the shoulder at full length).
                    float to = s.Absolute ? Mathf.Max(s.To, floor) : Mathf.Max(s.To, floor - aim);
                    if (su < 0.4f) { local = Mathf.Lerp(s.From - 55f, s.From, Mathf.SmoothStep(0f, 1f, su / 0.4f)); live = false; }
                    else if (su < 0.62f) local = Mathf.Lerp(s.From, to, EaseIn((su - 0.4f) / 0.22f));
                    else local = to;
                    break;
                }
                case Shape.Drag:
                {
                    // The ground ahead (floor: the way to it from the shoulder at full length), whatever the aim; then up.
                    float low = Mathf.Max(floor, -60f) - aim;
                    if (su < 0.55f)
                    {
                        local = low;
                        ext = Mathf.Lerp(-6f, 14f, EaseOut(su / 0.55f));
                        live = su >= 0.08f;
                    }
                    else
                    {
                        float e = Swing((su - 0.55f) / 0.45f);
                        local = Mathf.Lerp(low, s.From, e);
                        ext = Mathf.Lerp(14f, 0f, Mathf.Clamp01(e));
                    }
                    break;
                }
                case Shape.Plunge:
                    // Straight down, a little ahead; drawn up, then thrust and held.
                    local = -82f;
                    ext = su < 0.2f ? -4f * (su / 0.2f) : su < 0.45f ? Mathf.Lerp(-4f, 18f, EaseOut((su - 0.2f) / 0.25f)) : 18f;
                    live = su >= 0.2f;
                    break;
                case Shape.Iai:
                {
                    float e = su < 0.3f ? 0f : su < 0.55f ? EaseOut((su - 0.3f) / 0.25f) : 1f;
                    live = su >= 0.3f;
                    float phi = Mathf.Lerp(s.From, s.To, e) * Mathf.Deg2Rad;
                    float x = Mathf.Cos(phi), y = s.Squash * Mathf.Sin(phi);
                    local = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
                    len = Mathf.Sqrt(x * x + y * y);
                    break;
                }
                case Shape.Ellipse:
                {
                    float phi = Mathf.Lerp(s.From, s.To, Swing(su)) * Mathf.Deg2Rad;
                    float x = Mathf.Cos(phi), y = s.Squash * Mathf.Sin(phi);
                    local = Mathf.Atan2(y, x) * Mathf.Rad2Deg + s.Tilt;
                    len = Mathf.Sqrt(x * x + y * y);
                    live = su >= 0.1f;
                    break;
                }
                default:
                    local = Mathf.Lerp(s.From, s.To, Swing(su));
                    live = su >= 0.1f;
                    break;
            }
            len *= s.Reach;
            return s.Absolute ? local : aim + local;
        }

        /// <summary>A swing's pace: a breath drawn back, then out fast and easing to its end.</summary>
        static float Swing(float u)
        {
            const float wind = 0.12f;
            return u < wind ? -0.05f * Mathf.Sin(Mathf.PI * 0.5f * u / wind) : -0.05f + 1.05f * EaseOut((u - wind) / (1f - wind));
        }

        static float EaseOut(float x)
        {
            x = Mathf.Clamp01(x);
            float k = 1f - x;
            return 1f - k * k * k;
        }

        static float EaseIn(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * x;
        }

        /// <summary>The line to the cursor for a player facing it (degrees from level, up positive), held within 70 of level.</summary>
        public static float Aim(Vector2 from, Vector2 to)
        {
            var d = to - from;
            return Mathf.Clamp(Mathf.Atan2(d.y, Mathf.Abs(d.x) < 1e-4f ? 1e-4f : Mathf.Abs(d.x)) * Mathf.Rad2Deg, -70f, 70f);
        }

        /// <summary>The span (degrees, low to high) a stroke's blade has swept so far while cutting, false before it cuts.</summary>
        public static bool Swept(Stroke s, float su, float aim, out float lo, out float hi, out float reach, float floor = -90f)
        {
            lo = hi = reach = 0f;
            float start = s.Shape == Shape.Thrust ? 0.25f : s.Shape == Shape.Cleave || s.Shape == Shape.Smash ? 0.4f : s.Shape == Shape.Iai ? 0.3f
                : s.Shape == Shape.Plunge ? 0.2f : s.Shape == Shape.Drag ? 0.08f : 0.1f;
            if (su < start) return false;
            float a0 = Blade(s, start, aim, out float l0, out _, out _, floor);
            float a1 = Blade(s, Mathf.Min(su, s.Shape == Shape.Thrust ? 0.7f : 1f), aim, out float l1, out float e1, out _, floor);
            // A level cut's tip comes near the eye and goes on past the middle: the whole of it so far, its widest reach.
            float amid = Blade(s, (start + su) * 0.5f, aim, out float lm, out _, out _, floor);
            lo = Mathf.Min(a0, Mathf.Min(a1, amid));
            hi = Mathf.Max(a0, Mathf.Max(a1, amid));
            if (s.Shape == Shape.Thrust || s.Shape == Shape.Plunge) { lo -= 12f; hi += 12f; }
            reach = Mathf.Max(l0, Mathf.Max(l1, lm));
            return true;
        }

        /// <summary>The way from the shoulder (25 px over the feet) to the ground ahead for a blade reaching radius px (degrees from level).</summary>
        public static float FloorAngle(float radius) => -Mathf.Asin(Mathf.Clamp01(25f / Mathf.Max(1f, radius))) * Mathf.Rad2Deg;

        /// <summary>The body frame holding the blade that way: 1 the arm raised high, 2 up ahead, 3 ahead, 4 down.</summary>
        public static int ArmFrame(float deg)
        {
            deg = Mathf.Repeat(deg + 180f, 360f) - 180f;
            return deg > 100f || deg < -150f ? 1 : deg > 30f ? 2 : deg > -45f ? 3 : 4;
        }

        // ------------------------------------------------------------------ colours

        /// <summary>
        /// The weapon's colours from its own sprite: of the opaque pixels, the hue that is most vivid and most present gives
        /// the main colour (its brighter half) and the dark one (its darker half, deeper); the brightest few, whitened, the
        /// light. A grey blade (iron, silver) stays steel, a little cold. Night's Edge, Excalibur and the like keep their
        /// slash's own colours.
        /// </summary>
        static void Colours(ItemDef d, Art art)
        {
            art.Main = new Color(0.8f, 0.85f, 1f);
            art.Light = Color.white;
            art.Dark = new Color(0.25f, 0.28f, 0.4f);
            switch (d.Fx)
            {
                case MeleeFx.NightsEdge: case MeleeFx.TrueNightsEdge:
                    art.Dark = Rgb(40, 20, 60); art.Main = Rgb(120, 60, 220); art.Light = Rgb(200, 170, 255); return;
                case MeleeFx.Excalibur:
                    art.Dark = Rgb(180, 160, 60); art.Main = Rgb(255, 230, 90); art.Light = Rgb(255, 250, 200); return;
                case MeleeFx.TrueExcalibur:
                    art.Dark = Rgb(150, 60, 110); art.Main = Rgb(255, 105, 180); art.Light = Rgb(255, 220, 240); return;
                case MeleeFx.TerraBlade:
                    art.Dark = Rgb(45, 124, 205); art.Main = Rgb(64, 220, 96); art.Light = Rgb(200, 255, 180); return;
                case MeleeFx.HorsemansBlade:
                    art.Dark = Rgb(140, 50, 10); art.Main = Rgb(255, 140, 30); art.Light = Rgb(255, 230, 150); return;
                case MeleeFx.ChlorophyteClaymore:
                    art.Dark = Rgb(30, 100, 30); art.Main = Rgb(110, 230, 60); art.Light = Rgb(220, 255, 170); return;
            }
            if (d.Type == ItemType.RainbowPhaseblade || d.Type == ItemType.RainbowPhasesaber || d.Type == ItemType.Meowmere) art.Rainbow = true;
            if (!Pixels(d.Texture, out var px, out _, out _)) return;
            var hues = new float[12];
            var count = new int[12];
            int opaque = 0;
            foreach (var p in px)
            {
                if (p.a < 200) continue;
                Color.RGBToHSV(p, out float h, out float s, out float v);
                if (v < 0.2f) continue;
                opaque++;
                int b = Mathf.Clamp((int)(h * 12f), 0, 11);
                hues[b] += s * v * s;
                count[b]++;
            }
            if (opaque == 0) return;
            int best = 0;
            for (int b = 1; b < 12; b++) if (hues[b] > hues[best]) best = b;
            bool grey = hues[best] / Mathf.Max(1, count[best]) < 0.05f || count[best] < opaque * 0.08f;
            var vals = new List<float>();
            foreach (var p in px)
            {
                if (p.a < 200) continue;
                Color.RGBToHSV(p, out float h, out float s, out float v);
                if (v < 0.2f) continue;
                if (!grey && Mathf.Clamp((int)(h * 12f), 0, 11) != best) continue;
                vals.Add(v);
            }
            vals.Sort();
            float median = vals.Count > 0 ? vals[vals.Count / 2] : 0.5f;
            Color hi = Color.black, lo = Color.black, top = Color.black;
            int nh = 0, nl = 0, nt = 0;
            float topV = vals.Count > 0 ? vals[Mathf.Max(0, vals.Count - 1 - vals.Count / 8)] : 1f;
            foreach (var p in px)
            {
                if (p.a < 200) continue;
                Color.RGBToHSV(p, out float h, out float s, out float v);
                if (v < 0.2f) continue;
                if (v >= topV) { top += (Color)p; nt++; }
                if (!grey && Mathf.Clamp((int)(h * 12f), 0, 11) != best) continue;
                if (v >= median) { hi += (Color)p; nh++; }
                else { lo += (Color)p; nl++; }
            }
            if (nh > 0) art.Main = Bright(hi / nh, 0.7f);
            if (nl > 0) art.Dark = (lo / nl) * 0.55f;
            else art.Dark = art.Main * 0.4f;
            if (nt > 0) art.Light = Color.Lerp(Bright(top / nt, 0.85f), Color.white, 0.35f);
            if (grey)
            {
                // Steel: a cold white edge.
                art.Main = Color.Lerp(art.Main, new Color(0.78f, 0.86f, 1f), 0.35f);
                art.Dark = Color.Lerp(art.Dark, new Color(0.18f, 0.22f, 0.34f), 0.5f);
            }
            art.Main.a = art.Dark.a = art.Light.a = 1f;
        }

        static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        /// <summary>At least this bright (HSV value), the hue kept.</summary>
        static Color Bright(Color c, float minV)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return Color.HSVToRGB(h, Mathf.Min(1f, s * 1.1f), Mathf.Max(v, minV));
        }

        /// <summary>
        /// The texture's pixels, rows from the top: the mod's own picture when it has one, else Terraria's file read again
        /// (its loaded copy keeps none).
        /// </summary>
        public static bool Pixels(string name, out Color32[] px, out int w, out int h)
        {
            px = null;
            w = h = 0;
            if (string.IsNullOrEmpty(name)) return false;
            try
            {
                var tex = Terraria.TerrariaAssets.Texture(name);
                if (tex != null && tex.isReadable && tex.width > 2)
                {
                    var up = tex.GetPixels32();
                    w = tex.width;
                    h = tex.height;
                    px = new Color32[up.Length];
                    for (int y = 0; y < h; y++) System.Array.Copy(up, (h - 1 - y) * w, px, y * w, w);
                    return true;
                }
                string dir = Terraria.TerrariaAssets.ImagesDir;
                if (string.IsNullOrEmpty(dir)) return false;
                string path = Path.Combine(dir, name + ".xnb");
                if (!File.Exists(path)) return false;
                var img = Terraria.Xnb.XnbTexture.Load(path);
                w = img.Width;
                h = img.Height;
                px = new Color32[w * h];
                for (int i = 0; i < px.Length; i++)
                    px[i] = new Color32(img.Rgba[i * 4], img.Rgba[i * 4 + 1], img.Rgba[i * 4 + 2], img.Rgba[i * 4 + 3]);
                return true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[新挥砍] 取不到 {name} 的像素：{e.Message}");
                return false;
            }
        }

        /// <summary>The colour now: a rainbow blade's turns with time, offset along the trail.</summary>
        public static Color Tint(Art art, Color c, float offset)
        {
            if (!art.Rainbow) return c;
            Color.RGBToHSV(c, out _, out float s, out float v);
            return Color.HSVToRGB(Mathf.Repeat(Time.time * 0.6f + offset, 1f), Mathf.Max(0.55f, s), Mathf.Max(0.8f, v));
        }
    }
}
