using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;
using RangedShot = TerraKnight.Items.TerraProjectiles.RangedShot;

namespace TerraKnight.Items
{
    /// <summary>The remade magic weapons' mechanisms (MageKits): stage 4.</summary>
    internal static partial class MageKits
    {
        static readonly Color Wet = new Color(0.3f, 0.5f, 1f), Venom = new Color(0.6f, 0.2f, 0.9f), Shadow = new Color(0.6f, 0.2f, 0.8f);

        static void Lefts4()
        {
            // ---- Magic Dagger: the daggers stay in, every fourth throw calls them all back.
            MageLefts.Custom(ItemType.MagicDagger, "飞刀插在敌人身上（每个最多 6 把）；每第四次投掷，所有插着的飞刀飞回你手里，回程每把再切一次（0.35 倍）",
                "its daggers stay in what they strike (6 an enemy at most); every fourth throw, every dagger in an enemy comes back to your hand, cutting again (0.35× each)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) => MageMech.AddMark(hm, "dagger", 1, 6, 8f, new Color(0.7f, 0.8f, 1f), 15);
                    Cast(s, r);
                    if (run % 4 != 0) return;
                    var hero = s.Hero != null ? s.Hero : HeroController.SilentInstance;
                    if (hero == null) return;
                    foreach (var (e, n) in MageMech.Marked("dagger"))
                    {
                        MageMech.ClearMark(e, "dagger");
                        MageMech.Strike(def, e, MageMech.DamageOf(def, 0.35f * n));
                        var c = Centre(e);
                        var home = HeroCentre(hero);
                        for (int i = 0; i < n; i++) MageMech.Dust(Vector2.Lerp(c, home, i / (float)Mathf.Max(1, n)), 15, At(c, home, 8f), 1.2f);
                    }
                });

            // ---- Nimbus Rod: its rain wets; lightning finds the wet.
            MageLefts.Custom(ItemType.NimbusRod, "雨水打湿敌人（最多 3 层），湿透的敌人被云里落下的雷击中（0.6 倍），随后晾干",
                "its rain wets enemies (3 at most); one soaked through is struck by lightning from the cloud (0.6×), and dries",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) =>
                    {
                        if (MageMech.AddMark(hm, "wet", 1, 3, 5f, Wet, 33) < 3) return;
                        MageMech.ClearMark(hm, "wet");
                        var c = Centre(hm);
                        MageMech.Strike(def, hm, MageMech.DamageOf(def, 0.6f));
                        // Down out of the cloud it was rained on from.
                        var cloud = TerraProjectiles.Instance?.CloudOver(at) ?? c + Vector2.up * 6f * B;
                        Arc(cloud, c, def);
                        Arc(cloud + Vector2.right * 0.3f * B, c, def);
                        Burst(c, 226, 10, 3f);
                        Burst(cloud, 226, 6, 2f);
                        Sound("Item_122", 0.35f, 1.2f);
                    };
                    Cast(s, r);
                });

            // ---- Tome of Infinite Wisdom: its pages hang a moment in a row, then fly.
            MageLefts.Custom(ItemType.BookStaff, "书页先在身前悬停排成一排，再加速射出（0.95 倍）",
                "its pages hang in a row before you a moment, then fly off faster (0.95×)",
                (s, r, run) =>
                {
                    r.Hover = 14 + run % 4 * 4;
                    r.LaunchK = 1.5f;
                    r.LaunchSpeed = Mathf.Max(8f, s.Vel.magnitude) * 1.5f;
                    var t = s;
                    t.Damage = Mathf.RoundToInt(s.Damage * 0.95f);
                    Cast(t, r);
                });

            // ---- Shadowflame Hex Doll: its left as the game has it, its art only (the user's 10-28 "左键牵引去掉，只留右键").
            MageLefts.None(ItemType.ShadowFlameHexDoll);

            // ---- Ice Rod: its ice breaks into frost.
            MageLefts.Custom(ItemType.IceRod, "冰块碎裂时向四周迸出冰屑（各 0.08 倍），冻伤敌人",
                "its ice breaks into frost sparks all round (0.08× each), frostburning",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnEnd = (at, vel) =>
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                            Bolt(def, ProjKind.FrostSpark, at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5f, 0.08f, new RangedShot { Debuff = EnemyDebuffs.Frostburn, DebuffTicks = 180 });
                        }
                    };
                    Cast(s, r);
                });

            // ---- Venom Staff, Poison Staff: the toxin spreads at five.
            foreach (var (type, key, debuff, dust) in new[] { (ItemType.VenomStaff, "venom", EnemyDebuffs.Venom, 171), (ItemType.PoisonStaff, "poison", EnemyDebuffs.Poisoned, 46) })
                MageLefts.Custom(type, "毒牙在敌人身上叠毒（最多 5 层），叠满时毒素炸开（0.6 倍），扩散到 4 格内的敌人身上（各叠 2 层）",
                    "its fangs stack toxin on an enemy (5 at most); at five the toxin bursts (0.6×) and spreads to the enemies within 4 blocks (2 stacks each)",
                    (s, r, run) =>
                    {
                        var def = s.Def;
                        r.OnStrike = (hm, at) =>
                        {
                            EnemyDebuffs.Add(hm, debuff, 300);
                            if (MageMech.AddMark(hm, key, 1, 5, 6f, Venom, dust) < 5) return;
                            MageMech.ClearMark(hm, key);
                            var c = Centre(hm);
                            MageMech.Strike(def, hm, MageMech.DamageOf(def, 0.6f));
                            foreach (var e in Near(c, 4f, false))
                            {
                                if (e == hm) continue;
                                MageMech.AddMark(e, key, 2, 5, 6f, Venom, dust);
                                EnemyDebuffs.Add(e, debuff, 300);
                                MageMech.Dust(Vector2.Lerp(c, Centre(e), 0.5f), dust, Vector2.zero, 1.4f);
                            }
                            Burst(c, dust, 16, 3.5f, 1.4f);
                        };
                        Cast(s, r);
                    });

            // ---- Sharp Tears: the thorns stay in the ground a while.
            MageLefts.Custom(ItemType.SharpTears, "血荆棘刺出后在地上留 1.5 秒，走过的敌人被扎（0.25 倍）、流血",
                "its blood thorns stay in the ground for 1.5 s, pricking what goes over them (0.25×) and making it bleed",
                (s, r, run) =>
                {
                    Cast(s, r);
                    var at = Ground(s.Mouse + Vector2.up * B, 6f) + Vector2.up * 0.6f * B;
                    MageMech.Add(new Stub { Def = s.Def, Pos = at, Key = "blood_stub", Damage = MageMech.DamageOf(s.Def, 0.25f) }, 8);
                });

            // ---- Magical Harp: the beat.
            MageLefts.Custom(ItemType.MagicalHarp, "按节拍（约半秒一下）连续弹奏，音符越来越大越来越强（最多 1.5 倍），连到第 5 下奏出一组和弦（三个音符）；断拍从头来",
                "played on the beat (about every half second), its notes grow bigger and stronger (up to 1.5×); the fifth in a row is a chord of three; off the beat it starts again",
                (s, r, run) =>
                {
                    int combo = Beat.Next(s.Def, 0.35f, 0.75f);
                    var t = s;
                    t.Damage = Mathf.RoundToInt(s.Damage * (0.85f + 0.13f * combo));
                    r.Size = 1f + 0.12f * combo;
                    r.Trail = combo * 0.2f;
                    Cast(t, r);
                    if (combo < 5) return;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var v = Quaternion.Euler(0f, 0f, side * 15f) * s.Vel;
                        Bolt(s.Def, s.Kind, s.From, v, 0.8f, new RangedShot { Size = 1.4f, Trail = 1f });
                    }
                    Burst(s.From, 15, 12, 3f);
                });

            // ---- Killing Deck: suits.
            MageLefts.Custom(ItemType.KillingDeck, "每张牌随机一种花色：红心命中回 1 点生命、方块必定暴击、梅花命中处炸开（0.3 倍）、黑桃多穿透两个；连出三张同花色，第三张威力翻倍",
                "each card a suit at random: hearts heal 1 on a hit, diamonds always crit, clubs burst where they strike (0.3×), spades go through two more; three of a suit in a row, the third hits twice as hard",
                (s, r, run) =>
                {
                    var def = s.Def;
                    int suit = Random.Range(0, 4);
                    bool flush = Deck.Deal(suit);
                    switch (suit)
                    {
                        case 0: r.OnStrike = (hm, at) => TerraMod.Instance?.Stats.Heal(1); r.TrailDust = 5; break;
                        case 1: r.SureCrit = true; r.TrailDust = 91; break;
                        case 2: r.OnStrike = (hm, at) => { foreach (var e in Near(at, 2f)) MageMech.Strike(def, e, MageMech.DamageOf(def, 0.3f)); Burst(at, 6, 10, 3f); }; r.TrailDust = 6; break;
                        default: r.Pierce = 2; r.TrailDust = 27; break;
                    }
                    r.Trail = 0.6f;
                    var t = s;
                    if (flush) { t.Damage = s.Damage * 2; r.Size = 1.6f; r.Trail = 1f; }
                    Cast(t, r);
                });

            // ---- Frost Staff: frostbite, then shattering what is frozen.
            MageLefts.Custom(ItemType.FrostStaff, "冰霜弹叠冻伤（最多 5 层），叠满冻住 1 秒；打中冻住的敌人时炸出四片冰屑（各 0.1 倍）",
                "its frost bolts stack frostbite (5 at most); at five the enemy freezes for a second; striking a frozen one bursts four frost sparks (0.1× each)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) =>
                    {
                        if (MageMech.IsSlowed(hm) && MageMech.MarkOf(hm, "frozen") > 0)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                                Bolt(def, ProjKind.FrostSpark, at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f, 0.1f);
                            }
                            return;
                        }
                        if (MageMech.AddMark(hm, "frostbite", 1, 5, 5f, Ice, 135) < 5) return;
                        MageMech.ClearMark(hm, "frostbite");
                        MageMech.AddMark(hm, "frozen", 1, 1, 1f, Ice, 135);
                        MageMech.Slow(hm, 0.08f, 1f);
                        Burst(Centre(hm), 135, 14, 3f, 1.4f);
                    };
                    Cast(s, r);
                });

            // ---- Bejeweled Staff: each gem in turn, with its own staff's way.
            MageLefts.Custom(ItemType.BejeweledStaff, "七色宝石轮流射出，每一颗都带着它那根宝石法杖的左键效果",
                "the seven gems in turn, each with its own gem staff's left",
                (s, r, run) =>
                {
                    var gems = new[] { ItemType.AmethystStaff, ItemType.TopazStaff, ItemType.SapphireStaff, ItemType.EmeraldStaff, ItemType.RubyStaff, ItemType.AmberStaff, ItemType.DiamondStaff };
                    var gem = gems[(run - 1) % gems.Length];
                    var spec = MageLefts.Of(ItemDb.Get(gem));
                    var t = s;
                    t.Kind = MageLefts.Gems[System.Array.IndexOf(new[] { ItemType.AmethystStaff, ItemType.TopazStaff, ItemType.SapphireStaff, ItemType.EmeraldStaff, ItemType.RubyStaff, ItemType.DiamondStaff, ItemType.AmberStaff }, gem)];
                    if (spec?.Cast != null) spec.Cast(t, r, run);
                    else Cast(t, r);
                });

            // ---- Flower of Frost: ice where it bounces.
            MageLefts.Custom(ItemType.FlowerofFrost, "冰球每次落地弹起，在落点结出一片冰面 2 秒，冰面上的敌人减速",
                "each time its frost ball bounces it leaves a patch of ice for 2 s where it lands, slowing the enemies on it",
                (s, r, run) =>
                {
                    var def = s.Def;
                    float lastY = 0f;
                    r.OnFly = (pos, vel) =>
                    {
                        if (lastY < -0.5f && vel.y > 0.5f) MageMech.Add(new IcePatch { Def = def, Pos = Ground(pos, 1.5f) + Vector2.up * 0.15f, Key = "ice_patch" }, 8);
                        lastY = vel.y;
                        return true;
                    };
                    Cast(s, r);
                });

            // ---- Crystal Serpent: it follows the cursor and forks in what it strikes.
            MageLefts.Custom(ItemType.CrystalSerpent, "水晶蛇追着鼠标转向游动，穿过敌人时分出两条小蛇（各 0.4 倍）",
                "the serpent winds after the cursor, and forks into two smaller ones as it goes through an enemy (0.4× each)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.Weave = 12f;
                    r.Steer = (pos, vel) => BladeSkills.MageAim();
                    r.SteerTurn = 4f;
                    bool forked = false;
                    r.OnStrike = (hm, at) =>
                    {
                        if (forked) return;
                        forked = true;
                        foreach (var e in Near(at, 12f))
                        {
                            if (e == hm) continue;
                            Bolt(def, s.Kind, at, At(at, Centre(e), 8f), 0.4f, new RangedShot { Size = 0.7f });
                        }
                    };
                    Cast(s, r);
                });

            // ---- Rainbow Rod: its rainbow stays a while.
            MageLefts.Custom(ItemType.RainbowRod, "彩虹的轨迹在场上停留 1 秒，碰到的敌人受伤（0.08 倍）",
                "its rainbow's trail stays a second, hurting what touches it (0.08×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    int t = 0;
                    r.OnFly = (pos, vel) =>
                    {
                        if (++t % 3 == 0) MageMech.Add(new Ribbon { Def = def, Pos = pos, Key = "rainbow_ribbon", DustType = 66, Life = 60, Damage = MageMech.DamageOf(def, 0.08f) }, 60);
                        return true;
                    };
                    Cast(s, r);
                });

            // ---- The Orange Zapinator: as the gray, harder.
            MageLefts.Custom(ItemType.ZapinatorOrange, "每发（0.8 倍）随机失控成一种：巨大、分裂、粘在敌人身上 1 秒后爆炸、或飞到尽头折返",
                "every shot (0.8×) goes wrong one way at random: huge, splitting, sticking to an enemy and blowing up a second later, or turning back at the end",
                (s, r, run) =>
                {
                    var t = s;
                    t.Damage = Mathf.RoundToInt(s.Damage * 0.8f);
                    Cast(t, Zap(t, r, 0.7f));
                });
        }

        /// <summary>The Magical Harp's beat: casts in a row within the window add to it (to 5), off it it starts again.</summary>
        static class Beat
        {
            static float last = -10f;
            static int combo;
            static ItemType of;
            public static int Next(ItemDef def, float min, float max)
            {
                float gap = Time.time - last;
                combo = of == def.Type && gap >= min && gap <= max ? Mathf.Min(5, combo + 1) : 1;
                last = Time.time;
                of = def.Type;
                if (combo >= 5)
                {
                    int was = combo;
                    combo = 0;
                    return was;
                }
                return combo;
            }
        }

        /// <summary>The Killing Deck's suits dealt: three of one in a row is a flush.</summary>
        static class Deck
        {
            static readonly List<int> dealt = new List<int>();
            public static int Most()
            {
                var count = new int[4];
                foreach (var s in dealt) count[s]++;
                int best = 0;
                for (int i = 1; i < 4; i++) if (count[i] > count[best]) best = i;
                return best;
            }
            public static bool Deal(int suit)
            {
                dealt.Add(suit);
                while (dealt.Count > 5) dealt.RemoveAt(0);
                int n = dealt.Count;
                bool flush = n >= 3 && dealt[n - 1] == dealt[n - 2] && dealt[n - 2] == dealt[n - 3];
                if (flush) dealt.Clear();
                return flush;
            }
        }

        static void Arts4()
        {
            // ---- Magic Dagger: a dance of blades from enemy to enemy.
            Art(ItemType.MagicDagger).Press("飞刀之舞", 6f, 3f, "六把飞刀绕身一圈后飞出，每把在三个敌人之间来回切割，最后飞回你手里（每刀 1.5 倍）",
                "six daggers go round you once, then each cuts its way through three enemies in turn before coming back to your hand (1.5× a cut)",
                (h, d) =>
                {
                    for (int i = 0; i < 6; i++) MageMech.Add(new DancingBlade { Def = d, Pos = HeroCentre(h), Angle = i * 60f, Wait = 20 + i * 5, Key = "dancing_blade", Damage = MageMech.DamageOf(d, 1.5f) }, 6);
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });

            // ---- Nimbus Rod: a thunderstorm over the wet.
            Art(ItemType.NimbusRod).Press("雷雨", 7f, 3f, "鼠标上方的雨云化作雷云，4 秒内每半秒对每个被打湿的敌人和云下的敌人落雷（各 1.6 倍）",
                "a thundercloud over the cursor: for 4 s, every half second, lightning on every wet enemy and those under it (1.6× each)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim() + Vector2.up * 6f * B;
                    MageMech.Add(new StormCloud { Def = d, Pos = at, Key = "rainstorm", MarkKey = "wet", K = 1.6f }, 1);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- Tome of Infinite Wisdom: books round you, each with its own enemy.
            Art(ItemType.BookStaff).Press("智慧书阵", 7f, 3f, "身边浮起三本书 5 秒，各自朝不同的敌人连续射出书页（各 0.22 倍）",
                "three books float round you for 5 s, each firing pages at its own enemy (0.22× each)",
                (h, d) =>
                {
                    for (int i = 0; i < 3; i++) MageMech.Add(new FloatingBook { Def = d, Angle = i * 120f, Key = "floating_book", Index = i }, 3);
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });

            // ---- Shadowflame Hex Doll: tentacles out of the ground drawing everything in.
            Art(ItemType.ShadowFlameHexDoll).Press("暗影触手阵", 7f, 3f, "鼠标处地面伸出一圈暗影触手 3 秒，抓住 8 格内的所有敌人拉向中心（首领和精英不受牵引），不断受伤（0.5 倍）",
                "a ring of shadow tentacles reaches out of the ground on the cursor for 3 s, catching every enemy within 8 blocks and drawing it to the middle (not bosses or elites), hurting it (0.5×)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    MageMech.Add(new TentacleNest { Def = d, Pos = at, Key = "tentacle_nest", Damage = MageMech.DamageOf(d, 0.5f) }, 1);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- Ice Rod: a tide of ice.
            Art(ItemType.IceRod).Press("冰潮", 6f, 3f, "一排冰墙从脚下推向面前 16 格，把碰到的敌人推开、冻伤（9 倍），推到头时碎裂，被它推过的敌人再受一次冰裂（7 倍）",
                "a wall of ice pushes out 16 blocks ahead from your feet, shoving what it meets along and frostburning it (9×); where it stops it shatters, and every enemy it shoved takes the crack (7×)",
                (h, d) =>
                {
                    float face = BladeSkills.MageTurn(h);
                    var at = Ground(HeroCentre(h), 3f) + Vector2.up * B;
                    MageMech.Add(new Wave { Def = d, Pos = at, Vel = new Vector2(face * 0.3f * B, 0f), Key = "ice_tide", Damage = MageMech.DamageOf(d, 9f), Ice = true }, 2);
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });

            // ---- Venom Staff: a poison rain leaving pools; Poison Staff: a fog following the cursor.
            Art(ItemType.VenomStaff).Press("毒牙雨", 6f, 3f, "一阵毒牙雨轮流落向鼠标附近的敌人（各 1.4 倍），落地积成三摊毒池 3 秒",
                "a rain of fangs on the enemies by the cursor in turn (1.4× each) that pools into three venom puddles for 3 s",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    var tp = TerraProjectiles.Instance;
                    // The fangs come down on the enemies by the cursor in turn (on the cursor with none).
                    var near = Near(at, 8f, false);
                    for (int i = 0; i < 10; i++)
                    {
                        int delay = i * 5;
                        var e = near.Count > 0 ? near[i % near.Count] : null;
                        tp?.Later(delay, (f, v) =>
                        {
                            var to = e != null && !e.isDead ? Centre(e) : at;
                            var from = to + new Vector2(Random.Range(-2f, 2f), 10f) * B;
                            Bolt(d, ProjKind.VenomFang, from, At(from, to, 10f), 1.4f, new RangedShot { Debuff = EnemyDebuffs.Venom, DebuffTicks = 300 });
                        });
                    }
                    var floor = Ground(at, 8f) + Vector2.up * 0.2f;
                    for (int i = -1; i <= 1; i++) MageMech.Add(new Pool { Def = d, Pos = floor + Vector2.right * i * 2.4f * B, Key = "venom_pool", DustType = 171, Debuff = EnemyDebuffs.Venom, Life = 180, Hurts = true, Damage = MageMech.DamageOf(d, 0.6f) }, 3);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });
            Art(ItemType.PoisonStaff).Press("毒雾", 6f, 3f, "放出一团毒雾，4 秒内跟着鼠标飘动，雾里的敌人中毒、不断受伤（1.2 倍）",
                "a poison fog that drifts after the cursor for 4 s, poisoning and hurting what is in it (1.2×)",
                (h, d) =>
                {
                    MageMech.Add(new Fog { Def = d, Pos = BladeSkills.MageAim(), Key = "poison_fog", Damage = MageMech.DamageOf(d, 1.2f) }, 1);
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });

            // ---- Sharp Tears: thorns all along the way.
            Art(ItemType.SharpTears).Press("血荆棘林", 6f, 3f, "从脚下到鼠标处一路刺出血荆棘（各 2.6 倍），每根在地上留 2 秒，走过的敌人流血",
                "blood thorns burst out all the way from your feet to the cursor (2.6× each), each staying 2 s in the ground and making what goes over it bleed",
                (h, d) =>
                {
                    var from = HeroCentre(h);
                    var to = BladeSkills.MageAim();
                    var tp = TerraProjectiles.Instance;
                    for (int i = 1; i <= 7; i++)
                    {
                        var at = Ground(Vector2.Lerp(from, to, i / 7f) + Vector2.up * B, 6f);
                        int delay = i * 4;
                        tp?.Later(delay, (f, v) =>
                        {
                            Bolt(d, ProjKind.SharpTear, at, Vector2.up * 8f, 2.6f, new RangedShot { Debuff = EnemyDebuffs.Bleeding, DebuffTicks = 300 });
                            MageMech.Add(new Stub { Def = d, Pos = at + Vector2.up * 0.6f * B, Key = "blood_stub_art", Life = 120, Damage = MageMech.DamageOf(d, 0.9f) }, 7);
                        });
                    }
                    BladeSkills.MageArtCast(d, to);
                    return true;
                });

            // ---- Magical Harp: a melody leaping from enemy to enemy on the beat.
            Art(ItemType.MagicalHarp).Press("乐章", 6f, 3f, "奏出一段 3 秒的旋律：音符踩着节拍在附近的敌人之间跳动，每一拍打一个（各 1.75 倍）",
                "a melody of 3 s: its notes leap from enemy to enemy near on the beat, striking one each beat (1.75× each)",
                (h, d) =>
                {
                    MageMech.Add(new Melody { Def = d, Pos = HeroCentre(h), Key = "melody" }, 1);
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });

            // ---- Killing Deck: the ace of your best suit.
            Art(ItemType.KillingDeck).Press("王牌", 5f, 3f, "按最近五张牌里最多的花色打出一张王牌：红心回 15 点生命，方块连射五张必暴击的牌，梅花在附近每个敌人身上炸开（各 7 倍），黑桃一张贯穿全场的大牌（15 倍）",
                "an ace of the suit you dealt most in the last five: hearts heal 15, diamonds five sure-crit cards, clubs a burst on every enemy near (7× each), spades one great card through everything (15×)",
                (h, d) =>
                {
                    var from = HeroCentre(h);
                    var aim = BladeSkills.MageAim();
                    switch (Deck.Most())
                    {
                        case 0:
                            TerraMod.Instance?.Stats.Heal(15);
                            Burst(from, 5, 20, 3f, 1.4f);
                            for (int i = 0; i < 3; i++) Bolt(d, ProjKind.KillingDeckCard, from, Quaternion.Euler(0f, 0f, (i - 1) * 12f) * At(from, aim, 10f), 3f);
                            break;
                        case 1:
                            for (int i = 0; i < 5; i++) Bolt(d, ProjKind.KillingDeckCard, from, Quaternion.Euler(0f, 0f, (i - 2) * 6f) * At(from, aim, 11f), 2f, new RangedShot { SureCrit = true, Trail = 0.6f, TrailDust = 91 });
                            break;
                        case 2:
                            foreach (var e in Near(from, 18f)) { MageMech.Strike(d, e, MageMech.DamageOf(d, 7f)); Burst(Centre(e), 6, 16, 4f, 1.4f); }
                            break;
                        default:
                        {
                            // One great card: a blow on everything in its line (a piercing card struck one enemy many times), and the card flying.
                            var way = aim - from;
                            var dir = way.sqrMagnitude > 1e-6f ? way.normalized : Vector2.right;
                            foreach (var e in Along(from, Reach(from, dir, 50f), 2f)) MageMech.Strike(d, e, MageMech.DamageOf(d, 15f));
                            Bolt(d, ProjKind.KillingDeckCard, from, dir * 12f, 0.1f, new RangedShot { Size = 2.2f, Trail = 1f, Tracer = true, TrailDust = 27 });
                        }
                            break;
                    }
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });

            // ---- Frost Staff: a nova that freezes.
            Art(ItemType.FrostStaff).Press("冰霜新星", 6f, 3f, "鼠标处炸开冰霜新星，冻住 7 格内的所有敌人 1.5 秒（7 倍），解冻时碎裂（3.5 倍）",
                "a frost nova on the cursor freezes every enemy within 7 blocks for 1.5 s (7×), shattering as it thaws (3.5×)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    var frozen = Near(at, 7f);
                    TerraProjectiles.Instance?.Later(90, (f, v) =>
                    {
                        foreach (var e in frozen)
                        {
                            if (e == null || e.isDead) continue;
                            MageMech.Strike(d, e, MageMech.DamageOf(d, 3.5f));
                            Burst(Centre(e), 135, 16, 4f, 1.4f);
                        }
                    });
                    foreach (var e in frozen)
                    {
                        MageMech.Strike(d, e, MageMech.DamageOf(d, 7f));
                        MageMech.Slow(e, 0.06f, 1.5f);
                        MageMech.AddMark(e, "frozen", 1, 1, 1.5f, Ice, 135);
                    }
                    MageMech.Ring(at, 0.4f, 135, 36, 8f, 1.6f);
                    MageMech.Ring(at, 0.3f, 15, 20, 5f, 1.3f);
                    BladeSkills.MageArtCast(d, at);
                    Sound("Item_27", 0.8f, 0.7f);
                    return true;
                });

            // ---- Bejeweled Staff: each gem at its own enemy, with its staff's way.
            Art(ItemType.BejeweledStaff).Press("宝石风暴", 6f, 3f, "七色宝石各自射向一个敌人，每颗都带着它那根宝石法杖的左键效果（各 1 倍）",
                "the seven gems each fly at an enemy of their own, each with its own gem staff's left (1× each)",
                (h, d) =>
                {
                    var from = HeroCentre(h);
                    var near = Near(from, 20f);
                    var gems = new[] { ItemType.AmethystStaff, ItemType.TopazStaff, ItemType.SapphireStaff, ItemType.EmeraldStaff, ItemType.RubyStaff, ItemType.AmberStaff, ItemType.DiamondStaff };
                    var kinds = new[] { ProjKind.AmethystBolt, ProjKind.TopazBolt, ProjKind.SapphireBolt, ProjKind.EmeraldBolt, ProjKind.RubyBolt, ProjKind.AmberBolt, ProjKind.DiamondBolt };
                    for (int i = 0; i < 7; i++)
                    {
                        var to = near.Count > 0 ? Centre(near[i % near.Count]) : BladeSkills.MageAim();
                        float a = i * Mathf.PI * 2f / 7f;
                        var at = from + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.5f * B;
                        var s = new ShootPatterns.Shot { Def = d, Kind = kinds[i], From = at, Mouse = to, Vel = At(at, to, 9f), Damage = MageMech.DamageOf(d, 1f), Hero = h };
                        var spec = MageLefts.Of(ItemDb.Get(gems[i]));
                        if (spec?.Cast != null) spec.Cast(s, new RangedShot { Item = d }, 3);
                        else Bolt(d, kinds[i], at, s.Vel, 1f);
                    }
                    BladeSkills.MageArtCast(d, null, 1.1f);
                    return true;
                });

            // ---- Flower of Frost: frost petals out and back.
            Art(ItemType.FlowerofFrost).Press("冰晶之花", 5f, 3f, "脚下绽放一朵冰晶花，六片冰球花瓣朝鼠标方向扇形飞出，再飞回你身边（各 2 倍），沿途冻伤",
                "a frost flower opens at your feet: six frost-ball petals fan out toward the cursor and come back to you (2× each), frostburning on the way",
                (h, d) =>
                {
                    var c = HeroCentre(h);
                    // A fan of petals opening toward the cursor.
                    var aimWay = BladeSkills.MageAim() - c;
                    float aim0 = aimWay.sqrMagnitude > 1e-6f ? Mathf.Atan2(aimWay.y, aimWay.x) : 0f;
                    for (int i = 0; i < 6; i++)
                    {
                        float a = aim0 + (i - 2.5f) * 0.35f;
                        var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        var r = new RangedShot { Pierce = 3, Debuff = EnemyDebuffs.Frostburn, DebuffTicks = 240 };
                        OutAndBack(r, c, way, 9f, 8f, false);
                        Bolt(d, ProjKind.FrostBall, c, way * 9f, 2f, r);
                    }
                    Burst(c, 135, 24, 5f, 1.5f);
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });

            // ---- Crystal Serpent: serpents coiled round the enemies.
            Art(ItemType.CrystalSerpent).Press("晶蛇缠绕", 7f, 3f, "鼠标附近的每个敌人（最多 5 个）被一条水晶蛇缠住 2 秒：动弹不得（首领和精英只是变慢），越勒越紧，每 1/4 秒受伤（0.5 倍）；最后蛇身碎成八片晶片射向四周（各 0.4 倍）",
                "every enemy by the cursor (5 at most) is coiled by a crystal serpent for 2 s: held still (bosses and elites only slowed), squeezed tighter and hurt every quarter second (0.5×); then the serpent shatters into eight crystal shards all round (0.4× each)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    int n = 0;
                    foreach (var e in Near(at, 10f, false))
                    {
                        MageMech.Add(new CrystalCoil { Def = d, On_ = e, Pos = Centre(e), Key = "crystal_coil", Damage = MageMech.DamageOf(d, 0.5f) }, 5);
                        if (++n >= 5) break;
                    }
                    // None near: a serpent coils on the cursor all the same, and shatters there.
                    if (n == 0) MageMech.Add(new CrystalCoil { Def = d, Pos = at, Key = "crystal_coil", Damage = MageMech.DamageOf(d, 0.5f) }, 5);
                    BladeSkills.MageArtCast(d, at);
                    Sound("Item_101", 0.6f, 0.8f);
                    return true;
                });

            // ---- Rainbow Rod: a rainbow bridge.
            Art(ItemType.RainbowRod).Press("虹桥", 6f, 3f, "从你到鼠标处架起一道彩虹桥 4 秒，碰到桥的敌人受伤（1.4 倍）",
                "a rainbow bridge from you to the cursor for 4 s that hurts what touches it (1.4×)",
                (h, d) =>
                {
                    var from = HeroCentre(h);
                    var to = BladeSkills.MageAim();
                    int n = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(from, to) / (1f * B)), 2, 30);
                    for (int i = 0; i <= n; i++)
                    {
                        float k = i / (float)n;
                        var at = Vector2.Lerp(from, to, k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 2.5f * B;
                        MageMech.Add(new Ribbon { Def = d, Pos = at, Key = "rainbow_bridge", DustType = 66, Life = 240, Damage = MageMech.DamageOf(d, 1.4f) }, 31);
                    }
                    BladeSkills.MageArtCast(d, to);
                    return true;
                });

            // ---- The Orange Zapinator: overload, and a beam through everything.
            Art(ItemType.ZapinatorOrange).Press("超载", 6f, 3f, "2 秒内连射十发，越射越快，每发随机失控；最后枪身过热喷出冲击波（1 倍），再射出一道贯穿全场的光束（2.5 倍）",
                "ten shots over 2 s, faster and faster, each going wrong; then it overheats in a shockwave (1×) and fires a beam through everything (2.5×)",
                (h, d) =>
                {
                    Overload(h, d, 10, 1f);
                    TerraProjectiles.Instance?.Later(80, (f, v) =>
                    {
                        var hh = HeroController.SilentInstance;
                        if (hh == null) return;
                        var from = HeroCentre(hh);
                        // One beam: a blow on everything in its line, drawn as light (a piercing shot struck one enemy many times).
                        var way = BladeSkills.MageAim() - from;
                        var dir = way.sqrMagnitude > 1e-6f ? way.normalized : Vector2.right;
                        var end = Reach(from, dir, 50f);
                        foreach (var e in Along(from, end, 2f)) MageMech.Strike(d, e, MageMech.DamageOf(d, 2.5f));
                        for (int i = 0; i < 2; i++) DustLine(from, end, 226, 0.5f, 1.6f, 0.3f);
                        Sound("Item_12", 0.7f, 0.7f);
                    });
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });
        }

        // ================================================================== things (stage 4)

        /// <summary>An enemy dragged toward the player a moment (not bosses or elites).</summary>
        sealed class Drag : MageThing
        {
            public HealthManager On_;
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (On_ == null || On_.isDead || hero == null) { Done = true; return; }
                Pos = Centre(On_);
                MageMech.Pull(On_, HeroCentre(hero), 7f);
                if (Age % 4 == 0) MageMech.Dust(Pos, 27, Vector2.zero, 1.1f);
            }
        }

        /// <summary>A thorn stub left in the ground: pricking and bleeding what goes over it.</summary>
        sealed class Stub : MageThing
        {
            readonly int look = Random.Range(0, 6);
            public Stub() { Life = 90; Hurts = true; HitEvery = 30; Size = new Vector2(1f, 1.4f) * MageMech.Block; }
            public override void Hit(HealthManager hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Bleeding, 240);
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float fade = Mathf.Clamp01((Life - Age) / 20f);
                // The tip of one of the thorns (six 200 px frames).
                fx.Draw(FxBatch.Frame("Projectile_756", 136, look * 32, 64, 32, 32f, 16f), px, new Color(1f, 1f, 1f, fade), -Mathf.PI * 0.5f, 0.7f);
            }
        }

        /// <summary>A patch of ice: the enemies on it are slowed.</summary>
        sealed class IcePatch : MageThing
        {
            public IcePatch() { Life = 120; }
            public override void Tick()
            {
                if (Age % 10 != 0) return;
                foreach (var e in Near(Pos, 1.5f, false)) MageMech.Slow(e, 0.45f, 0.4f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                if (Age % 4 == 0) MageMech.Dust(Pos + new Vector2(Random.Range(-1.4f, 1.4f), 0f) * MageMech.Block, 135, Vector2.zero, 1.1f);
            }
        }

        /// <summary>A blade dancing from enemy to enemy (three cuts), then back to the player's hand.</summary>
        sealed class DancingBlade : MageThing
        {
            public float Angle;
            public int Wait;
            readonly List<HealthManager> cut = new List<HealthManager>();
            HealthManager prey;
            public DancingBlade() { Life = 300; Size = new Vector2(0.8f, 0.8f) * MageMech.Block; HitEvery = 20; }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) { Done = true; return; }
                var home = HeroCentre(hero);
                if (Age < Wait)
                {
                    Angle += 8f;
                    float a = Angle * Mathf.Deg2Rad;
                    Pos = home + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2f * MageMech.Block;
                    Vel = Vector2.zero;
                    return;
                }
                Hurts = true;
                if (cut.Count >= 3 || (prey == null && cut.Count > 0 && Next() == null))
                {
                    Pos = Vector2.MoveTowards(Pos, home, 0.8f * MageMech.Block);
                    if (Vector2.Distance(Pos, home) < 0.5f * MageMech.Block) Done = true;
                }
                else
                {
                    if (prey == null || prey.isDead) prey = Next();
                    if (prey == null) { Pos = Vector2.MoveTowards(Pos, home, 0.8f * MageMech.Block); if (Vector2.Distance(Pos, home) < 0.5f * MageMech.Block) Done = true; }
                    else Pos = Vector2.MoveTowards(Pos, Centre(prey), 0.8f * MageMech.Block);
                }
                Vel = Vector2.zero;
                MageMech.Dust(Pos, 15, Vector2.zero, 1f);
            }
            HealthManager Next()
            {
                foreach (var e in Near(Pos, 16f)) if (!cut.Contains(e)) return e;
                return null;
            }
            public override void Hit(HealthManager hm)
            {
                if (!cut.Contains(hm)) cut.Add(hm);
                prey = null;
            }
            public override void Draw(FxBatch fx, Vector2 px) => Sprite(fx, "Projectile_93", px, Color.white, Age * 0.5f, 1f);
        }

        /// <summary>
        /// The Crystal Serpent's art: a serpent of crystal coiled round an enemy for 2 s, holding it still (MageMech.Slow's
        /// hold) and squeezing tighter, a bite every quarter second; then it shatters into crystal shards all round.
        /// </summary>
        sealed class CrystalCoil : MageThing
        {
            public HealthManager On_;
            float ring = -1f;
            public CrystalCoil() { Life = 120; }
            public override void Tick()
            {
                bool on = On_ != null && !On_.isDead && On_.gameObject.activeInHierarchy;
                if (ring < 0f)
                {
                    // Round the enemy's body (px), not through it.
                    var col = on ? On_.GetComponent<Collider2D>() : null;
                    float half = col != null ? Mathf.Max(col.bounds.extents.x, col.bounds.extents.y) / TerraParticles.Px : 8f;
                    ring = Mathf.Clamp(half + 10f, 18f, 64f);
                }
                if (!on) return;
                Pos = Centre(On_);
                MageMech.Slow(On_, 0.05f, 0.1f, new Color(0.8f, 0.65f, 1f));
                if (Age % 15 != 14) return;
                MageMech.Strike(Def, On_, Damage);
                Burst(Pos, 70, 6, 2f, 1.1f);
            }
            public override void End()
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Bolt(Def, ProjKind.CrystalChargeShard, Pos + way * 0.8f * MageMech.Block, way * 8f, 0.4f);
                }
                Burst(Pos, 70, 24, 4f, 1.5f);
                Sound("Item_27", 0.55f, 1.1f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                if (ring < 0f) return;
                // Twelve crystal links round it, the tail thin and the head big, a gap between them; tightening as it goes.
                float r = ring * Mathf.Lerp(1.15f, 0.8f, Age / (float)Life);
                const int n = 12;
                for (int i = 0; i < n; i++)
                {
                    float a = Age * 0.12f + i * (Mathf.PI * 2f / n) * 0.88f;
                    var at = px + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.75f);
                    Sprite(fx, "Projectile_94", at, Color.white, a + Mathf.PI * 0.5f, Mathf.Lerp(1.2f, 2.4f, i / (float)(n - 1)));
                }
                fx.Light(px, new Color(0.7f, 0.5f, 1f) * 0.6f, 3f);
            }
        }

        /// <summary>A book floating round the player, firing pages at its own enemy.</summary>
        sealed class FloatingBook : MageThing
        {
            public float Angle;
            public int Index;
            public FloatingBook() { Life = 300; }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) { Done = true; return; }
                Angle += 2f;
                float a = Angle * Mathf.Deg2Rad;
                Pos = HeroCentre(hero) + new Vector2(Mathf.Cos(a) * 2.6f, 1.4f + Mathf.Sin(a) * 0.8f) * MageMech.Block;
                Vel = Vector2.zero;
                if (Age % 18 != Index * 6) return;
                var near = Near(Pos, 18f);
                if (near.Count == 0) return;
                var e = near[Index % near.Count];
                Bolt(Def, ProjKind.BookStaffShot, Pos, At(Pos, Centre(e), 9f), 0.22f);
            }
            /// <summary>The tome itself (its item picture), rocking as it floats; its pages (712) are what it fires.</summary>
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float fade = Mathf.Clamp01((Life - Age) / 20f);
                Sprite(fx, "Item_3852", px + new Vector2(0f, Mathf.Sin(Age * 0.08f + Index * 2f) * 3f), new Color(1f, 1f, 1f, fade), Mathf.Sin(Age * 0.1f) * 0.15f, 0.8f);
                fx.Light(px, new Color(1f, 0.8f, 0.4f) * 0.5f * fade, 2f);
            }
        }

        /// <summary>Shadow tentacles out of the ground: every enemy near drawn to the middle and hurt.</summary>
        sealed class TentacleNest : MageThing
        {
            public TentacleNest() { Life = 180; Hurts = true; HitEvery = 20; Size = new Vector2(3f, 3f) * MageMech.Block; }
            public override void Tick()
            {
                foreach (var e in Near(Pos, 8f, false))
                {
                    MageMech.Pull(e, Pos, 7f);
                    if (Age % 30 == 0)
                    {
                        MageMech.Strike(Def, e, MageMech.DamageOf(Def, 0.25f));
                        EnemyDebuffs.Add(e, EnemyDebuffs.Shadowflame, 180);
                        var c = Centre(e);
                        for (int i = 0; i < 6; i++) MageMech.Dust(Vector2.Lerp(Pos, c, i / 6f), 27, Vector2.zero, 1.3f);
                    }
                }
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 3; i++)
                {
                    float a = Age * 0.1f + i * Mathf.PI * 2f / 3f;
                    MageMech.Dust(Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(0.5f, 3f) * MageMech.Block, 27, Vector2.zero, 1.5f);
                }
                FxBatch.Instance?.Light(Pos, Shadow * 0.5f, 4f);
            }
        }

        /// <summary>A poison fog drifting after the cursor.</summary>
        sealed class Fog : MageThing
        {
            public Fog() { Life = 240; Hurts = true; HitEvery = 20; Size = new Vector2(4f, 3f) * MageMech.Block; }
            public override void Tick()
            {
                Vel = Vector2.ClampMagnitude(BladeSkills.MageAim() - Pos, 0.15f * MageMech.Block);
            }
            public override void Hit(HealthManager hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, 240);
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 3; i++) MageMech.Dust(Pos + new Vector2(Random.Range(-2f, 2f), Random.Range(-1.5f, 1.5f)) * MageMech.Block, 46, Random.insideUnitCircle * 0.5f, Random.Range(1.4f, 2f), 100);
            }
        }

        /// <summary>A melody: its notes leap from enemy to enemy on the beat.</summary>
        sealed class Melody : MageThing
        {
            int k;
            public Melody() { Life = 180; }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero != null) Pos = HeroCentre(hero);
                if (Age % 15 != 0) return;
                var near = Near(Pos, 18f);
                if (near.Count == 0) return;
                var e = near[k++ % near.Count];
                var c = Centre(e);
                MageMech.Strike(Def, e, MageMech.DamageOf(Def, 1.75f));
                var from = Pos + Vector2.up * B;
                for (int i = 0; i <= 6; i++)
                {
                    float t = i / 6f;
                    MageMech.Dust(Vector2.Lerp(from, c, t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * 2f * B, 15 + (k % 3), Vector2.zero, 1.3f);
                }
                Sound("Item_26", 0.4f, 0.8f + 0.1f * (k % 5));
            }
        }
    }
}
