using System.Collections.Generic;
using UnityEngine;
using Offshoot = TerraKnight.Items.TerraProjectiles.Offshoot;
using RangedShot = TerraKnight.Items.TerraProjectiles.RangedShot;
using SpawnWay = TerraKnight.Items.TerraProjectiles.SpawnWay;

namespace TerraKnight.Items
{
    /// <summary>
    /// The remade magic weapons' left button (the user's 10-28 "现在开始做新版本的法师武器吧…绚丽…创意", then "法师武器的左键也是可以
    /// 改动的，跟原版可以不一样"): each weapon's own change to how it casts, said in its tooltip as it is (no flourish of words:
    /// "不用专门写圣巢之力这几个字进去，正常写描述就行"). Kept near its own damage a second on one enemy (suite magearts):
    ///   Echo: every Nth cast an echo of it a moment later from a rune behind (×0.6K, seeking), the rest ×0.9;
    ///   Twin: two from either side curving in on the cursor (×0.55 each);
    ///   Resonance: every Nth a great one (×(0.8 + N/4), bigger, through 2 more), the rest ×0.8;
    ///   Fan: N close together, 5 degrees either side (×1.2K/N each);
    ///   Orbit: round the player once, then off at the aim (×1.1K);
    ///   Hover: held where made a moment, gathering, then off faster (×0.95K);
    ///   Volley: a second a moment after the first (×0.8, then ×0.35);
    ///   Rain: every Nth cast one more down from the sky on the cursor (×K);
    ///   Nova: every Nth a ring of eight round the player (×0.2 each), the rest ×0.9;
    ///   Extra: every Nth one of another kind as well (×K);
    ///   Chaos: each with one change at random (split, chain, burst, bounce or seeking);
    ///   Gather: turning in the air to the cursor;
    ///   Pulse: a channelled one's held thing left as it is, something more every so often while it is used.
    /// Sign: what each one's shots carry besides (debuffs, arcs, bursts, offshoots).
    /// </summary>
    internal static class MageLefts
    {
        internal enum Left : byte { Plain, Echo, Twin, Resonance, Fan, Orbit, Hover, Volley, Rain, Nova, Extra, Chaos, Gather, Pulse, Custom }

        internal sealed class Spec
        {
            public Left Left;
            public int N = 1;
            /// <summary>Echoes, rain, novas, extras: of this kind (None: the weapon's own); Rain's and Extra's damage share.</summary>
            public ProjKind Kind;
            public float K = 1f;
            public System.Action<RangedShot> Sign;
            /// <summary>Pulse: every so many seconds while it is used, this.</summary>
            public float Every;
            public System.Action<HeroController, ItemDef> OnPulse;
            /// <summary>Custom: the weapon's own cast (MageKits: 法师武器创意重做设计.md): the game's shot, what it carries, the cast's place in its run.</summary>
            public System.Action<ShootPatterns.Shot, RangedShot, int> Cast;
            public string Tip, TipEn;
        }

        static readonly Dictionary<ItemType, Spec> specs = new Dictionary<ItemType, Spec>();

        static Offshoot Off(ProjKind kind, int n, SpawnWay way, float damage, float speed = 0f, float chance = 1f) =>
            new Offshoot { Kind = kind, Count = n, Way = way, Damage = damage, Speed = speed, Chance = chance };

        static void S(ItemType t, Left left, int n, string zh, string en, System.Action<RangedShot> sign = null, ProjKind kind = ProjKind.None, float k = 1f) =>
            specs[t] = new Spec { Left = left, N = n, Kind = kind, K = k, Sign = sign, Tip = "左键：" + zh, TipEn = "Left click: " + en };

        static void P(ItemType t, float every, System.Action<HeroController, ItemDef> pulse, string zh, string en) =>
            specs[t] = new Spec { Left = Left.Pulse, Every = every, OnPulse = pulse, Tip = "左键：" + zh, TipEn = "Left click: " + en };

        /// <summary>A weapon's own left (the remade weapons' mechanisms, MageKits).</summary>
        internal static void Custom(ItemType t, string zh, string en, System.Action<ShootPatterns.Shot, RangedShot, int> cast) =>
            specs[t] = new Spec { Left = Left.Custom, Cast = cast, Tip = "左键：" + zh, TipEn = "Left click: " + en };

        /// <summary>A weapon whose left stays the game's (its art only).</summary>
        internal static void None(ItemType t) => specs.Remove(t);

        /// <summary>A channelled weapon's own pulse (MageKits): every so many seconds while it is used.</summary>
        internal static void Pulse(ItemType t, float every, System.Action<HeroController, ItemDef> pulse, string zh, string en) => P(t, every, pulse, zh, en);

        /// <summary>The weapon's left, or null (left the game's way: the Hallownest spells' weapons).</summary>
        public static Spec Of(ItemDef d)
        {
            if (specs.Count == 0) Make();
            return d != null && specs.TryGetValue(d.Type, out var s) ? s : null;
        }

        /// <summary>Its tooltip's line, in the game's language; null without one.</summary>
        public static string Tip(ItemDef d)
        {
            var s = Of(d);
            if (s == null || !MageArts.Active(d)) return null;
            return Lang.IsSource || Lang.Culture == "zh-Hant" ? s.Tip : s.TipEn;
        }

        /// <summary>The gem staffs' echoes, one at random for the Bejeweled Staff.</summary>
        internal static readonly ProjKind[] Gems = { ProjKind.AmethystBolt, ProjKind.TopazBolt, ProjKind.SapphireBolt, ProjKind.EmeraldBolt, ProjKind.RubyBolt, ProjKind.DiamondBolt, ProjKind.AmberBolt };

        static void Make()
        {
            const string gem = "每第三次施法，杖头的符文稍后再射出一道追踪的宝石光（0.6 倍）";
            const string gemEn = "every third cast, the rune at the staff's head lets out a seeking gem bolt a moment later (0.6×)";
            // ---- stage 0-1
            S(ItemType.DaybloomStaff, Left.Gather, 1, "飘出的花粉在空中转向，汇聚到鼠标处", "the motes turn in the air and gather on the cursor");
            S(ItemType.AmethystStaff, Left.Echo, 3, gem, gemEn);
            S(ItemType.TopazStaff, Left.Echo, 3, gem + "；黄玉光多穿透一个敌人", gemEn + "; the topaz goes through one more enemy", r => r.Pierce = 1);
            S(ItemType.SapphireStaff, Left.Echo, 3, gem + "；蓝玉光飞得更快", gemEn + "; the sapphire flies faster", r => r.Speed = 1.25f);
            S(ItemType.EmeraldStaff, Left.Echo, 3, gem + "；翡翠光命中后弹向下一个敌人（0.5 倍）", gemEn + "; the emerald glances on to the next enemy (0.5×)", r => { r.Bounces = 1; r.BounceDamage = 0.5f; });
            S(ItemType.RubyStaff, Left.Echo, 3, gem + "；红玉光命中处小范围炸开（0.3 倍）", gemEn + "; the ruby bursts a little where it strikes (0.3×)", r => { r.Burst = 40f; r.BurstDamage = 0.3f; });
            S(ItemType.AmberStaff, Left.Echo, 3, gem + "；琥珀光命中后碎成两片（0.3 倍）", gemEn + "; the amber shatters in two where it strikes (0.3×)", r => r.Shards = 2);
            S(ItemType.DiamondStaff, Left.Echo, 3, gem + "；钻石光多穿透一个敌人、飞得更快", gemEn + "; the diamond goes through one more enemy, faster", r => { r.Pierce = 1; r.Speed = 1.15f; });
            S(ItemType.WandofSparking, Left.Volley, 2, "每次挥动连出两颗火花，第二颗稍弱", "each flick throws two sparks, the second weaker");
            S(ItemType.WandofFrosting, Left.Volley, 2, "每次挥动连出两颗霜屑，第二颗稍弱", "each flick throws two frost sparks, the second weaker");
            S(ItemType.ThunderStaff, Left.Plain, 1, "雷电命中后跳到附近另一个敌人（0.35 倍）", "its bolt arcs on to another enemy near (0.35×)", r => { r.Chain = 1; r.ChainDamage = 0.35f; });
            S(ItemType.ZapinatorGray, Left.Chaos, 1, "每一发随机带上一种效果：分裂、连锁闪电、爆炸、弹射或追踪", "every shot takes one change at random: splitting, arcing, bursting, glancing on or seeking");
            // ---- stage 2
            S(ItemType.CrimsonRod, Left.Plain, 1, "血云落下的血雨让敌人流血", "the cloud's blood rain makes enemies bleed", r => { r.Debuff = EnemyDebuffs.Bleeding; r.DebuffTicks = 300; });
            P(ItemType.GlacierFang, 0.5f, MageFire.GlacierShards, "施放时每半秒朝附近的敌人迸出两枚冰晶（各 0.3 倍）", "while it is used, every half second two ice bolts fly at an enemy near (0.3× each)");
            S(ItemType.WaterBolt, Left.Fan, 2, "一次射出左右分开的两道水矢（各 0.45 倍）", "two water bolts at once, either way of the aim (0.45× each)", k: 0.75f);
            S(ItemType.Vilethorn, Left.Plain, 1, "魔刺让敌人中毒", "its thorns poison", r => { r.Debuff = EnemyDebuffs.Poisoned; r.DebuffTicks = 300; });
            S(ItemType.SpaceGun, Left.Resonance, 4, "每第四发是一道更粗的光束，穿透更多敌人（1.8 倍），其余 0.8 倍", "every fourth shot a thicker beam through more enemies (1.8×), the rest 0.8×");
            S(ItemType.BeeGun, Left.Extra, 5, "每第五发多放出一只大黄蜂", "every fifth shot lets out a giant bee as well", kind: ProjKind.GiantBee, k: 1f);
            S(ItemType.AquaScepter, Left.Plain, 1, "水流蛇行前进", "its stream winds as it goes", r => r.Weave = 20f);
            S(ItemType.Petalstorm, Left.Plain, 1, "花瓣追着敌人飞", "its petals chase enemies", r => r.Home = 2.5f);
            S(ItemType.Flamelash, Left.Plain, 1, "火球撞上敌人时炸开（0.4 倍）", "its fireball bursts where it strikes (0.4×)", r => { r.Burst = 64f; r.BurstDamage = 0.4f; r.Trail = 0.5f; });
            S(ItemType.FlowerofFire, Left.Plain, 1, "火球熄灭时迸出两颗火花（各 0.12 倍）", "its fireball throws out two sparks as it dies (0.12× each)", r => r.OnDeath = Off(ProjKind.Spark, 2, SpawnWay.Up, 0.12f, 4f));
            S(ItemType.WeatherPain, Left.Plain, 1, "风暴打中的敌人放出电弧，跳到另一个敌人（0.3 倍）", "what the storm strikes arcs on to another enemy (0.3×)", r => { r.Chain = 1; r.ChainDamage = 0.3f; });
            S(ItemType.MagicMissile, Left.Plain, 1, "飞弹撞上敌人时炸开（0.35 倍）", "its missile bursts where it strikes (0.35×)", r => { r.Burst = 48f; r.BurstDamage = 0.35f; r.Trail = 0.5f; });
            S(ItemType.DemonScythe, Left.Orbit, 1, "镰刀先绕身转一圈再飞出（1.1 倍）", "each scythe goes round you once before it flies off (1.1×)");
            S(ItemType.BookofSkulls, Left.Echo, 2, "每第二次施法，书旁的符文稍后再放出一个追踪的骷髅头（0.6 倍）", "every second cast, the rune by the book lets out a seeking skull a moment later (0.6×)");
            // ---- stage 3
            S(ItemType.LightningStrike, Left.Rain, 3, "每第三次施法，鼠标处多落下一道闪电（0.5 倍）", "every third cast another bolt comes down on the cursor (0.5×)", k: 0.5f);
            S(ItemType.ClingerStaff, Left.Plain, 1, "诅咒墙烧到敌人时溅起两点诅咒火（各 0.2 倍）", "the cursed wall throws up two cursed flames where it burns an enemy (0.2× each)", r => r.OnHit = Off(ProjKind.CursedFlame, 2, SpawnWay.Up, 0.2f, 5f));
            S(ItemType.GoldenShower, Left.Plain, 1, "灵液命中处再溅起两滴灵液（各 0.25 倍）", "two more drops of ichor splash up where it strikes (0.25× each)", r => r.OnHit = Off(ProjKind.GoldenShowerStream, 2, SpawnWay.Up, 0.25f, 6f));
            S(ItemType.SoulDrain, Left.Plain, 1, "吸取时在敌人身上炸出灵魂冲击（0.25 倍）", "the drain bursts in a soul blast on the enemy (0.25×)", r => { r.Burst = 40f; r.BurstDamage = 0.25f; });
            S(ItemType.MeteorStaff, Left.Plain, 1, "流星落地时有时迸出两块碎陨石（各 0.25 倍）", "a meteor sometimes throws up two pieces as it lands (0.25× each)", r => r.OnDeath = Off(ProjKind.None, 2, SpawnWay.Up, 0.25f, 5f, 0.34f));
            S(ItemType.CrystalVileShard, Left.Plain, 1, "晶刺有一半几率让敌人着诅咒火", "its crystals set enemies on cursed fire half the time", r => { r.Debuff = EnemyDebuffs.CursedInferno; r.DebuffTicks = 180; r.DebuffChance = 0.5f; });
            S(ItemType.CrystalStorm, Left.Plain, 1, "水晶命中后碎成两片（各 0.3 倍）", "its crystals shatter in two where they strike (0.3× each)", r => r.Shards = 2);
            S(ItemType.CursedFlames, Left.Plain, 1, "诅咒火球命中处炸开（0.45 倍）", "its cursed fireball bursts where it strikes (0.45×)", r => { r.Burst = 72f; r.BurstDamage = 0.45f; });
            S(ItemType.LaserRifle, Left.Resonance, 5, "每第五发是一道穿透光束（2 倍），其余 0.8 倍", "every fifth shot a piercing beam (2×), the rest 0.8×");
            S(ItemType.SkyFracture, Left.Twin, 1, "两侧各现出一把剑，交汇于鼠标处（各 0.55 倍）", "a sword from either side, meeting on the cursor (0.55× each)");
            P(ItemType.MedusaHead, 0.6f, MageFire.MedusaGaze, "凝视时每 0.6 秒让最近的敌人混乱片刻", "while it gazes, every 0.6 s the nearest enemy is confused a moment");
            S(ItemType.SpiritFlame, Left.Plain, 1, "灵焰追踪敌人", "its flames seek enemies out", r => r.Home = 3.5f);
            // ---- stage 4
            S(ItemType.MagicDagger, Left.Echo, 3, "每第三次掷出，身旁的符文稍后再掷出一把追踪的飞刀（0.6 倍）", "every third throw, a rune beside you throws a seeking dagger a moment later (0.6×)");
            S(ItemType.NimbusRod, Left.Plain, 1, "雨云的雨滴打中敌人时放出电弧（0.3 倍）", "the cloud's rain arcs on from what it strikes (0.3×)", r => { r.Chain = 1; r.ChainDamage = 0.3f; });
            S(ItemType.BookStaff, Left.Plain, 1, "书页追踪敌人", "its pages seek enemies out", r => { r.Home = 2f; r.Trail = 0.3f; });
            S(ItemType.ShadowFlameHexDoll, Left.Plain, 1, "触手打中敌人时有一半几率再长出一根（0.3 倍）", "a tentacle striking an enemy grows another half the time (0.3×)", r => r.OnHit = Off(ProjKind.ShadowflameTentacle, 1, SpawnWay.Ahead, 0.3f, 0f, 0.5f));
            S(ItemType.IceRod, Left.Plain, 1, "冰块让敌人冻伤", "its ice frostburns", r => { r.Debuff = EnemyDebuffs.Frostburn; r.DebuffTicks = 240; });
            S(ItemType.VenomStaff, Left.Fan, 3, "一次射出三枚毒牙（各 0.4 倍）", "three fangs at once (0.4× each)");
            S(ItemType.SharpTears, Left.Extra, 3, "每第三次施法多刺出一根血荆棘（0.6 倍）", "every third cast one more blood thorn (0.6×)", k: 0.6f);
            S(ItemType.PoisonStaff, Left.Fan, 3, "一次射出三枚毒牙（各 0.4 倍）", "three fangs at once (0.4× each)");
            S(ItemType.MagicalHarp, Left.Echo, 2, "每第二个音符之后响起一个回音音符（0.6 倍）", "every second note is followed by an echo of it (0.6×)");
            S(ItemType.KillingDeck, Left.Echo, 3, "每第三次掷出，身旁的符文稍后再掷出一张追踪的牌（0.6 倍）", "every third throw, a rune beside you throws a seeking card a moment later (0.6×)");
            S(ItemType.FrostStaff, Left.Plain, 1, "冰霜弹碎裂时向四周迸出冰屑（各 0.2 倍）", "its frost bolt bursts into frost sparks all round as it breaks (0.2× each)", r => r.OnDeath = Off(ProjKind.FrostSpark, 4, SpawnWay.Ring, 0.2f, 5f));
            S(ItemType.BejeweledStaff, Left.Echo, 3, "每第三次施法，杖头的符文稍后射出一道随机颜色的追踪宝石光（0.6 倍）", "every third cast, the rune at its head lets out a seeking gem bolt of any colour a moment later (0.6×)");
            S(ItemType.FlowerofFrost, Left.Plain, 1, "冰球熄灭时迸出两颗冰屑（各 0.12 倍）", "its frost ball throws out two frost sparks as it dies (0.12× each)", r => r.OnDeath = Off(ProjKind.FrostSpark, 2, SpawnWay.Up, 0.12f, 4f));
            S(ItemType.CrystalSerpent, Left.Plain, 1, "水晶蛇行前进", "its crystal winds as it goes", r => r.Weave = 20f);
            S(ItemType.RainbowRod, Left.Plain, 1, "彩虹撞上敌人时炸开彩光（0.35 倍）", "the rainbow bursts in light where it strikes (0.35×)", r => { r.Burst = 64f; r.BurstDamage = 0.35f; });
            S(ItemType.ZapinatorOrange, Left.Chaos, 1, "每一发随机带上一种效果：分裂、连锁闪电、爆炸、弹射或追踪", "every shot takes one change at random: splitting, arcing, bursting, glancing on or seeking");
            // ---- stage 5
            S(ItemType.BlizzardStaff, Left.Plain, 1, "冰锥命中后碎成两片（各 0.3 倍）", "its icicles shatter in two where they strike (0.3× each)", r => r.Shards = 2);
            S(ItemType.Razorpine, Left.Plain, 1, "松针微微追踪敌人", "its needles turn a little after enemies", r => r.Home = 2f);
            S(ItemType.MagnetSphere, Left.Resonance, 3, "每第三颗磁球更大更强（1.55 倍），其余 0.8 倍", "every third sphere bigger and stronger (1.55×), the rest 0.8×");
            S(ItemType.BubbleGun, Left.Plain, 1, "泡泡破裂时有时溅出两个小泡泡（各 0.2 倍）", "a bubble popping sometimes throws out two more (0.2× each)", r => r.OnDeath = Off(ProjKind.Bubble, 2, SpawnWay.Ring, 0.2f, 4f, 0.5f));
            S(ItemType.RainbowGun, Left.Plain, 1, "彩虹有时让敌人混乱", "the rainbow sometimes confuses", r => { r.Debuff = EnemyDebuffs.Confused; r.DebuffTicks = 120; r.DebuffChance = 0.25f; });
            P(ItemType.LaserMachinegun, 0.45f, MageFire.MachinegunSide, "扫射时每 0.45 秒多射出两道斜向激光（各 0.35 倍）", "while it fires, every 0.45 s two more lasers go out at an angle (0.35× each)");
            S(ItemType.BatScepter, Left.Orbit, 1, "蝙蝠先绕身一圈再扑出（1.1 倍）", "each bat goes round you once before it swoops off (1.1×)");
            S(ItemType.WaspGun, Left.Extra, 4, "每第四发多放出一只大黄蜂", "every fourth shot lets out a giant bee as well", kind: ProjKind.GiantBee, k: 1f);
            S(ItemType.LeafBlower, Left.Plain, 1, "叶片蜿蜒飞行", "its leaves wind as they fly", r => r.Weave = 14f);
            S(ItemType.Nightglow, Left.Hover, 1, "星光先在身后悬停片刻，再加速射出（0.8 倍）", "its lights hang behind you a moment, then shoot off faster (0.8×)", k: 0.85f);
            S(ItemType.SparkleGuitar, Left.Echo, 3, "每第三下和弦之后响起回音（0.6 倍）", "every third chord is followed by an echo of it (0.6×)");
            P(ItemType.ChargedBlasterCannon, 0.8f, MageFire.BlasterArc, "蓄能时每 0.8 秒向最近的敌人迸出电弧（0.4 倍）", "while it charges and fires, every 0.8 s an arc leaps to the nearest enemy (0.4×)");
            S(ItemType.BetsysWrath, Left.Nova, 4, "每第四次施法身边炸开一圈火焰（各 0.2 倍）", "every fourth cast a ring of fire bursts round you (0.2× each)", kind: ProjKind.BallofFire);
            S(ItemType.NebulaArcanum, Left.Resonance, 3, "每第三发是更大的星云核（1.55 倍），其余 0.8 倍", "every third a greater nebula core (1.55×), the rest 0.8×");
            S(ItemType.HeatRay, Left.Plain, 1, "射线点燃敌人，命中处溅起两颗火花（各 0.2 倍）", "the ray sets enemies alight and throws up two sparks where it strikes (0.2× each)",
                r => { r.Debuff = EnemyDebuffs.OnFire; r.DebuffTicks = 180; r.OnHit = Off(ProjKind.Spark, 2, SpawnWay.Up, 0.2f, 4f); });
            S(ItemType.NettleBurst, Left.Plain, 1, "荆棘让敌人中毒", "its thorns poison", r => { r.Debuff = EnemyDebuffs.Poisoned; r.DebuffTicks = 300; });
            S(ItemType.RazorbladeTyphoon, Left.Orbit, 1, "台风先绕身转一圈再飞出（0.5 倍，台风会追着敌人反复切割）", "each typhoon goes round you once before it flies off (0.5×; it chases and cuts again and again)", k: 0.45f);
            S(ItemType.ShadowbeamStaff, Left.Plain, 1, "光束命中时折射到另一个敌人（0.4 倍）", "the beam bends on from what it strikes to another enemy (0.4×)", r => { r.Chain = 1; r.ChainDamage = 0.4f; });
            S(ItemType.LunarFlareBook, Left.Rain, 4, "每第四次施法，鼠标处多落下一颗更大的月耀（1.2 倍）", "every fourth cast a greater flare comes down on the cursor (1.2×)", k: 1.2f);
            P(ItemType.LastPrism, 0.7f, MageFire.PrismStar, "施放时每 0.7 秒在最近的敌人头顶招来一颗星（0.35 倍）", "while it shines, every 0.7 s a star comes down on the nearest enemy (0.35×)");
            S(ItemType.SpectreStaff, Left.Echo, 3, "每第三次施法，杖头的符文稍后再放出一只幽魂（1.2 倍）", "every third cast, the rune at the staff's head lets out another wisp a moment later (1.2×)", k: 2f);
            S(ItemType.InfernoFork, Left.Plain, 1, "狱火炸开的范围更大（0.4 倍）", "its hellfire bursts wider (0.4×)", r => { r.Burst = 96f; r.BurstDamage = 0.4f; });
            S(ItemType.NebulaBlaze, Left.Hover, 1, "星云火先在掌心停留片刻，再加速射出", "its blaze waits in your hand a moment, then shoots off faster");
            S(ItemType.StaffofEarth, Left.Plain, 1, "巨石命中后碎成三块（各 0.3 倍）", "its boulder breaks in three where it strikes (0.3× each)", r => r.Shards = 3);
            S(ItemType.UnholyTrident, Left.Fan, 3, "一次射出三道三叉戟（各 0.4 倍）", "three tridents at once (0.4× each)");
            S(ItemType.ToxicFlask, Left.Plain, 1, "毒瓶碎裂时多冒出两团毒雾（各 0.3 倍）", "the flask breaks into two more toxic clouds (0.3× each)", r => r.OnDeath = Off(ProjKind.ToxicCloud, 2, SpawnWay.Ring, 0.3f, 0.6f));
            S(ItemType.PrincessWeapon, Left.Echo, 2, "每第二次施法之后响起一道共鸣（0.6 倍）", "every second cast is followed by a resonance of it (0.6×)");
            S(ItemType.ArcSurge, Left.Plain, 1, "电弧命中后再跳向两个敌人（各 0.3 倍）", "its arcs leap on to two more enemies (0.3× each)", r => { r.Chain = 2; r.ChainDamage = 0.3f; });
            // The remade weapons' own mechanisms, over the specs above (法师武器创意重做设计.md).
            MageKits.Lefts();
        }
    }
}
