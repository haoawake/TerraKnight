using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;
using RangedShot = TerraKnight.Items.TerraProjectiles.RangedShot;

namespace TerraKnight.Items
{
    /// <summary>The remade magic weapons' mechanisms (MageKits): stage 3.</summary>
    internal static partial class MageKits
    {
        static readonly Color Cursed = new Color(0.55f, 1f, 0.2f), Ichor = new Color(1f, 0.85f, 0.2f), Soul = new Color(0.6f, 0.9f, 1f), Stone = new Color(0.6f, 0.6f, 0.6f);

        static void Lefts3()
        {
            // ---- Lightning Strike: each leap leaves a live wire between the two enemies.
            MageLefts.Custom(ItemType.LightningStrike, "闪电在敌人之间来回跳跃，每跳一次在两个敌人之间留下 1 秒的电线，碰到电线的敌人受伤（0.2 倍）",
                "its lightning leaps from enemy to enemy, each leap leaving a live wire between the two for a second that hurts what touches it (0.2×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    Vector2? last = null;
                    r.OnStrike = (hm, at) =>
                    {
                        var c = Centre(hm);
                        if (last.HasValue && Vector2.Distance(last.Value, c) > 0.5f * B) Wire(def, last.Value, c, 60, 0.2f);
                        last = c;
                    };
                    Cast(s, r);
                });

            // ---- Clinger Staff: a cursed flame crawls on along the ground.
            MageLefts.Custom(ItemType.ClingerStaff, "每道诅咒墙放出一团诅咒火，沿地面朝鼠标方向爬 8 格，烧到的敌人着诅咒火（0.4 倍）",
                "each cursed wall lets out a cursed flame that crawls 8 blocks along the ground toward the cursor, setting what it burns on cursed fire (0.4×)",
                (s, r, run) =>
                {
                    Cast(s, r);
                    var hero = HeroController.SilentInstance;
                    if (hero == null) return;
                    float dir = Mathf.Sign(s.Mouse.x - HeroCentre(hero).x);
                    var at = Ground(s.Mouse + Vector2.up * B, 6f) + Vector2.up * 0.5f * B;
                    MageMech.Add(new Crawler { Def = s.Def, Pos = at, Vel = new Vector2(dir * 0.12f * B, 0f), Key = "cursed_crawler", Damage = MageMech.DamageOf(s.Def, 0.4f) }, 6);
                });

            // ---- Golden Shower: ichor pools where it lands.
            MageLefts.Custom(ItemType.GoldenShower, "灵液落地积成水洼（2 秒，最多 6 个），水洼里的敌人沾上灵液（防御降低）",
                "its ichor pools where it lands (2 s, 6 at most); enemies in a pool are ichored (their defense lowered)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnEnd = (at, vel) => MageMech.Add(new Pool { Def = def, Pos = Ground(at, 1.5f) + Vector2.up * 0.2f, Key = "ichor_pool", DustType = 170, Debuff = EnemyDebuffs.Ichor }, 6);
                    Cast(s, r);
                });

            // ---- Soul Drain: each drained enemy gives a soul that circles you.
            MageLefts.Custom(ItemType.SoulDrain, "每吸取一个敌人，身边多一颗灵魂球环绕（最多 6 颗，8 秒）",
                "each enemy drained gives a soul that circles you (6 at most, 8 s)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) =>
                    {
                        if (MageMech.MarkOf(hm, "drained") > 0) return;
                        MageMech.AddMark(hm, "drained", 1, 1, 1.5f, Soul, 59);
                        MageMech.Add(new SoulOrb { Def = def, Pos = Centre(hm), Key = "soul_orb", Angle = Random.value * 360f }, 6);
                    };
                    Cast(s, r);
                });

            // ---- Meteor Staff: burning craters.
            MageLefts.Custom(ItemType.MeteorStaff, "流星落地砸出陨坑，燃烧 2 秒，坑里的敌人着火（0.2 倍）",
                "its meteor leaves a crater burning for 2 s where it lands, setting what is in it on fire (0.2×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnEnd = (at, vel) => MageMech.Add(new Burning { Def = def, Pos = Ground(at, 1.5f) + Vector2.up * 0.2f, Key = "crater", Life = 120, Damage = MageMech.DamageOf(def, 0.2f) }, 4);
                    Cast(s, r);
                });

            // ---- Crystal Vile Shard: cursed fire along its line.
            MageLefts.Custom(ItemType.CrystalVileShard, "晶刺沿线长出，被扎到的敌人有一半几率着诅咒火",
                "its crystals grow along the line; what they reach is set on cursed fire half the time",
                (s, r, run) =>
                {
                    Cast(s, r);
                    foreach (var e in AlongLine(s.From, s.Vel, 14f, 1.2f))
                        if (Random.value < 0.5f) EnemyDebuffs.Add(e, EnemyDebuffs.CursedInferno, 180);
                });

            // ---- Crystal Storm: each crystal splits off a wall once.
            MageLefts.Custom(ItemType.CrystalStorm, "水晶撞墙反弹，同时分出一颗小水晶（0.5 倍）",
                "a crystal glancing off a wall throws out a smaller one too (0.5×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    Vector2 lastVel = Vector2.zero;
                    bool split = false;
                    r.OnFly = (pos, vel) =>
                    {
                        if (!split && lastVel.sqrMagnitude > 0.1f && Vector2.Dot(lastVel.normalized, vel.normalized) < 0.3f)
                        {
                            split = true;
                            var side = Quaternion.Euler(0f, 0f, Random.Range(-40f, 40f)) * vel;
                            Bolt(def, s.Kind, pos, side, 0.5f, new RangedShot { Size = 0.7f });
                        }
                        lastVel = vel;
                        return true;
                    };
                    Cast(s, r);
                });

            // ---- Cursed Flames: the fire is catching.
            MageLefts.Custom(ItemType.CursedFlames, "诅咒火会传染：着火的敌人每半秒把诅咒火传给身边 3 格内的其他敌人（0.15 倍）",
                "its cursed fire is catching: a burning enemy passes it on to the others within 3 blocks every half second (0.15×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) => Contagion.On(def, hm, 4f);
                    Cast(s, r);
                });

            // ---- Laser Rifle: focus while standing still.
            MageLefts.Custom(ItemType.LaserRifle, "站着不动时聚焦：每发更细更强，连续站定射击最多强化到 1.5 倍；一动就重新聚焦",
                "standing still it focuses: each shot thinner and stronger, up to 1.5× while you keep still; moving starts the focus again",
                (s, r, run) =>
                {
                    float focus = Focus.Add(s.Hero, 0.2f);
                    var t = s;
                    t.Damage = Mathf.RoundToInt(s.Damage * (0.85f + 0.65f * focus));
                    r.Size = 1.1f - 0.4f * focus;
                    r.Trail = focus;
                    r.TrailDust = 62;
                    r.Tracer = focus > 0.8f;
                    Cast(t, r);
                });

            // ---- Sky Fracture: the swords stay in, then go off together.
            MageLefts.Custom(ItemType.SkyFracture, "剑插在敌人身上（最多 4 把），插满 4 把时一齐引爆（每把 0.6 倍）",
                "its swords stay in what they strike (4 at most); at four they all go off together (0.6× each)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) =>
                    {
                        if (MageMech.AddMark(hm, "sky_sword", 1, 4, 5f, new Color(0.7f, 0.9f, 1f), 15) < 4) return;
                        MageMech.ClearMark(hm, "sky_sword");
                        MageMech.Strike(def, hm, MageMech.DamageOf(def, 2.4f));
                        MageMech.Ring(Centre(hm), 0.3f, 15, 24, 6f, 1.5f);
                        Sound("Item_14", 0.5f, 1.3f);
                    };
                    Cast(s, r);
                });

            // ---- Medusa Head: the gaze turns to stone, bit by bit.
            MageLefts.Pulse(ItemType.MedusaHead, 0.3f, MedusaStacks, "凝视时每 0.3 秒给视线内每个敌人叠石化印记，叠满 5 层石化 1.5 秒（几乎不能动）",
                "while it gazes, every 0.3 s each enemy in its sight gains stone; at five it is turned to stone for 1.5 s (all but still)");

            // ---- Spirit Flame: the flames wait by you for an enemy.
            MageLefts.Custom(ItemType.SpiritFlame, "灵焰先停在你身边等待（最多 6 团，6 秒），敌人进入 12 格内时自动射向它",
                "its flames wait by you (6 at most, 6 s) and fly at an enemy as soon as one comes within 12 blocks",
                (s, r, run) =>
                {
                    var hero = s.Hero != null ? s.Hero : HeroController.SilentInstance;
                    if (hero == null) { Cast(s, r); return; }
                    MageMech.Add(new WaitingFlame { Def = s.Def, Pos = HeroCentre(hero), Angle = run * 60f, Key = "waiting_flame", Damage = MageMech.DamageOf(s.Def, 1f) }, 6);
                });
        }

        /// <summary>The enemies within `width` blocks of a line from a point along a velocity, up to `blocks` along it.</summary>
        static List<HealthManager> AlongLine(Vector2 from, Vector2 vel, float blocks, float width)
        {
            var list = new List<HealthManager>();
            var dir = vel.sqrMagnitude > 1e-6f ? vel.normalized : Vector2.right;
            foreach (var e in Near(from, blocks + 1f))
            {
                var c = Centre(e);
                float along = Vector2.Dot(c - from, dir);
                if (along < 0f || along > blocks * B) continue;
                if (Vector2.Distance(c, from + dir * along) <= width * B) list.Add(e);
            }
            return list;
        }

        /// <summary>A live wire between two points (world) for `ticks`: links of it along the line, each hurting what touches it.</summary>
        static void Wire(ItemDef def, Vector2 a, Vector2 b, int ticks, float k)
        {
            float len = Vector2.Distance(a, b);
            int n = Mathf.Clamp(Mathf.CeilToInt(len / (1.2f * MageMech.Block)), 1, 14);
            for (int i = 0; i <= n; i++)
                MageMech.Add(new Ribbon { Def = def, Pos = Vector2.Lerp(a, b, i / (float)n), Key = "wire", DustType = 226, Life = ticks, Damage = MageMech.DamageOf(def, k) }, 60);
            Arc(a, b, def);
        }

        /// <summary>The Medusa Head's gaze: stone on each enemy in its sight; at five, turned to stone.</summary>
        static void MedusaStacks(HeroController hero, ItemDef def)
        {
            var c = HeroCentre(hero);
            foreach (var e in Near(c, 20f))
            {
                if (MageMech.AddMark(e, "stone", 1, 5, 3f, Stone, 31) < 5) continue;
                MageMech.ClearMark(e, "stone");
                MageMech.Slow(e, 0.05f, 1.5f, Stone);
                MageMech.Strike(def, e, MageMech.DamageOf(def, 0.8f));
                Burst(Centre(e), 31, 16, 3f, 1.4f);
            }
        }

        /// <summary>The Laser Rifle's focus: built while the player keeps still, lost when he moves.</summary>
        static class Focus
        {
            static float focus, at;
            public static float Add(HeroController hero, float add)
            {
                bool still = hero != null && hero.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude < 0.5f;
                if (!still || Time.time - at > 1.5f) focus = 0f;
                at = Time.time;
                focus = Mathf.Min(1f, focus + add);
                return focus;
            }
        }

        static void Arts3()
        {
            // ---- Lightning Strike: judgement on every enemy.
            Art(ItemType.LightningStrike).Press("雷霆审判", 7f, 3f, "头顶开出雷云，3 秒内每半秒对 25 格内的每一个敌人落一道雷（各 0.5 倍）",
                "a thundercloud opens overhead and for 3 s, every half second, lightning strikes every enemy within 25 blocks (0.5× each)",
                (h, d) =>
                {
                    MageMech.Add(new Judgement { Def = d, Pos = HeroCentre(h) + Vector2.up * 8f * B, Key = "judgement" }, 1);
                    BladeSkills.MageArtCast(d, null, 1.3f);
                    return true;
                });

            // ---- Clinger Staff: cursed fire spreading both ways.
            Art(ItemType.ClingerStaff).Press("诅咒蔓延", 6f, 3f, "鼠标处的地面燃起诅咒火，1.5 秒内向两侧各蔓延 12 格，形成一片 3 秒的火海（2 倍）",
                "cursed fire catches on the ground at the cursor and spreads 12 blocks either way over 1.5 s into a sea of fire for 3 s (2×)",
                (h, d) =>
                {
                    var at = Ground(BladeSkills.MageAim() + Vector2.up * B, 6f) + Vector2.up * 0.5f * B;
                    foreach (int dir in new[] { -1, 1 })
                        MageMech.Add(new Crawler { Def = d, Pos = at, Vel = new Vector2(dir * 0.2f * B, 0f), Key = "cursed_sea", Life = 90, Leaves = true, Damage = MageMech.DamageOf(d, 2f) }, 2);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- Golden Shower: a fountain of ichor on every enemy near.
            Art(ItemType.GoldenShower).Press("灵液喷泉", 6f, 3f, "鼠标处喷出灵液喷泉，3 秒内灵液不断落在 12 格内的每个敌人身上（每股 0.3 倍）",
                "a fountain of ichor springs up on the cursor and for 3 s keeps raining ichor on every enemy within 12 blocks (0.3× a stream)",
                (h, d) =>
                {
                    var at = Ground(BladeSkills.MageAim(), 6f);
                    MageMech.Add(new Fountain { Def = d, Pos = at, Key = "ichor_fountain" }, 1);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- Soul Drain: the souls go out.
            Art(ItemType.SoulDrain).Press("魂之爆发", 5f, 2f, "所有环绕的灵魂球射向不同的敌人（各 4.5 倍），每颗命中回复 5 点魔力；没有灵魂球时召出三颗",
                "every soul circling you flies at a different enemy (4.5× each), each that hits restoring 5 mana; with none, three are called up",
                (h, d) =>
                {
                    var orbs = MageMech.Things("soul_orb");
                    if (orbs.Count == 0)
                        for (int i = 0; i < 3; i++) MageMech.Add(new SoulOrb { Def = d, Pos = HeroCentre(h), Key = "soul_orb", Angle = i * 120f }, 6);
                    var near = Near(HeroCentre(h), 20f);
                    int k = 0;
                    foreach (var o in MageMech.Things("soul_orb")) ((SoulOrb)o).Loose(near.Count > 0 ? near[k++ % near.Count] : null);
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });

            // ---- Meteor Staff: a star gate's meteor shower, each meteor at its own enemy.
            Art(ItemType.MeteorStaff).Press("流星雨", 7f, 3f, "鼠标上方开出星门，3 秒内流星接连落下，每颗瞄准不同的敌人（各 1 倍）",
                "a star gate opens over the cursor and for 3 s meteors come down one after another, each aimed at a different enemy (1× each)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim() + Vector2.up * 12f * B;
                    MageMech.Add(new StarGate { Def = d, Pos = at, Key = "star_gate" }, 1);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- Crystal Vile Shard: a forest of crystals from your feet to the cursor.
            Art(ItemType.CrystalVileShard).Press("晶刺林", 6f, 3f, "从脚下到鼠标处长出一片晶刺林，随后逐根碎裂迸出碎片（每段 0.6 倍），着诅咒火",
                "a forest of crystals grows from your feet to the cursor, then breaks piece by piece into shards (0.6× each), cursing what it reaches",
                (h, d) =>
                {
                    var from = HeroCentre(h);
                    var to = BladeSkills.MageAim();
                    int n = 6;
                    var tp = TerraProjectiles.Instance;
                    for (int i = 1; i <= n; i++)
                    {
                        var at = Vector2.Lerp(from, to, i / (float)n);
                        int delay = i * 6;
                        tp?.Later(delay, (f, v) =>
                        {
                            Bolt(d, ProjKind.CrystalVileShard, at, Vector2.up * 6f, 0.6f, new RangedShot { Debuff = EnemyDebuffs.CursedInferno, DebuffTicks = 180 });
                            foreach (var e in Near(at, 2f)) { MageMech.Strike(d, e, MageMech.DamageOf(d, 0.6f)); EnemyDebuffs.Add(e, EnemyDebuffs.CursedInferno, 180); }
                            Burst(at, 75, 10, 3f, 1.3f);
                        });
                    }
                    BladeSkills.MageArtCast(d, to);
                    return true;
                });

            // ---- Crystal Storm: a whirlpool of crystals.
            Art(ItemType.CrystalStorm).Press("水晶漩涡", 6f, 3f, "鼠标处卷起水晶漩涡，1.5 秒内越聚越多，最后所有水晶一齐射向附近的敌人（各 1.5 倍）",
                "a whirlpool of crystals gathers on the cursor, more and more for 1.5 s, then every crystal shoots at the enemies near (1.5× each)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    MageMech.Add(new CrystalVortex { Def = d, Pos = at, Key = "crystal_vortex" }, 1);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- Cursed Flames: a ring of cursed fire widening out.
            Art(ItemType.CursedFlames).Press("诅咒火环", 6f, 3f, "一圈诅咒火球绕着你旋转扩大，2 秒内烧过 10 格内的所有敌人（各 2.2 倍），着诅咒火",
                "a ring of cursed fireballs spins round you, widening, and in 2 s burns through every enemy within 10 blocks (2.2× each), cursing them",
                (h, d) =>
                {
                    for (int i = 0; i < 10; i++)
                        MageMech.Add(new FireRing { Def = d, Angle = i * 36f, Key = "cursed_ring", Damage = MageMech.DamageOf(d, 2.2f) });
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });

            // ---- Sky Fracture: swords rain on every enemy.
            Art(ItemType.SkyFracture).Press("天裂", 7f, 3f, "天空裂开，剑雨落向 20 格内的每一个敌人，每个敌人八把（各 2 倍），插在身上的剑一齐引爆",
                "the sky cracks open and swords rain on every enemy within 20 blocks, eight each (2× each); the swords in them all go off",
                (h, d) =>
                {
                    var tp = TerraProjectiles.Instance;
                    int k = 0;
                    foreach (var e in Near(HeroCentre(h), 20f, false))
                    {
                        var target = e;
                        for (int i = 0; i < 8; i++)
                        {
                            int delay = (k++) * 3;
                            tp?.Later(delay, (f, v) =>
                            {
                                if (target == null || target.isDead) return;
                                var c = Centre(target);
                                var from = c + new Vector2(Random.Range(-3f, 3f), 12f) * B;
                                Bolt(d, ProjKind.SkyFractureSword, from, At(from, c, 12f), 2f, new RangedShot { Trail = 0.6f });
                            });
                        }
                        int stuck = MageMech.MarkOf(e, "sky_sword");
                        if (stuck > 0) { MageMech.ClearMark(e, "sky_sword"); MageMech.Strike(d, e, MageMech.DamageOf(d, 0.6f * stuck)); }
                    }
                    BladeSkills.MageArtCast(d, null, 1.2f);
                    return true;
                });

            // ---- Medusa Head: the stare.
            Art(ItemType.MedusaHead).Press("石化之眼", 7f, 3f, "视线内 20 格的所有敌人石化 2 秒（几乎不能动），石化结束时碎裂受伤（9 倍）",
                "every enemy in sight within 20 blocks turns to stone for 2 s (all but still) and takes 9× as the stone cracks",
                (h, d) =>
                {
                    foreach (var e in Near(HeroCentre(h), 20f))
                    {
                        MageMech.Slow(e, 0.05f, 2f, Stone);
                        MageMech.ClearMark(e, "stone");
                        Petrify.On(d, e, 2f, 9f);
                    }
                    BladeSkills.MageArtCast(d, null, 1.2f);
                    Sound("NPC_Hit_41", 0.7f, 0.6f);
                    return true;
                });

            // ---- Spirit Flame: a ring of flames, each after its own enemy.
            Art(ItemType.SpiritFlame).Press("灵焰群", 6f, 3f, "身边燃起八团灵焰，依次锁定不同的敌人射出（各 1 倍）",
                "eight spirit flames rise round you and fly one after another at different enemies (1× each)",
                (h, d) =>
                {
                    var near = Near(HeroCentre(h), 20f);
                    for (int i = 0; i < 8; i++)
                        MageMech.Add(new WaitingFlame { Def = d, Pos = HeroCentre(h), Angle = i * 45f, Key = "flame_ring", Wait = 10 + i * 6, Prey = near.Count > 0 ? near[i % near.Count] : null, Damage = MageMech.DamageOf(d, 1f) });
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });
        }

        // ================================================================== things (stage 3)

        /// <summary>A flame crawling along the ground (cursed), burning what it reaches; leaving fire behind when asked.</summary>
        sealed class Crawler : MageThing
        {
            public bool Leaves;
            public Crawler() { Life = 60; Hurts = true; HitEvery = 20; Size = new Vector2(1.2f, 1.2f) * MageMech.Block; }
            public override void Tick()
            {
                var floor = Ground(Pos + Vector2.up * MageMech.Block, 3f);
                Pos.y = floor.y + 0.5f * MageMech.Block;
                if (Physics2D.Raycast(Pos, new Vector2(Mathf.Sign(Vel.x), 0f), 0.6f * MageMech.Block, TerrainMask).collider != null) Done = true;
                if (Leaves && Age % 8 == 0) MageMech.Add(new Pool { Def = Def, Pos = Pos, Key = "cursed_sea_fire", DustType = 75, Debuff = EnemyDebuffs.CursedInferno, Life = 180, Damage = Damage / 2, Hurts = true }, 30);
                for (int i = 0; i < 2; i++) MageMech.Dust(Pos + Random.insideUnitCircle * 0.4f, 75, Vector2.up * Random.Range(1f, 3f), Random.Range(1.2f, 1.7f));
            }
            public override void Hit(HealthManager hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.CursedInferno, 180);
        }

        /// <summary>A pool on the ground: what stands in it takes its debuff (and, if it hurts, its damage).</summary>
        sealed class Pool : MageThing
        {
            public int DustType, Debuff;
            public Pool() { Life = 120; Hurts = false; HitEvery = 30; Size = new Vector2(2.4f, 0.8f) * MageMech.Block; }
            public override void Tick()
            {
                if (Age % 15 != 0) return;
                foreach (var e in Near(Pos, 1.4f, false)) EnemyDebuffs.Add(e, Debuff, 240);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                if (Age % 3 == 0) MageMech.Dust(Pos + new Vector2(Random.Range(-1.2f, 1.2f), 0f) * MageMech.Block, DustType, Vector2.up * Random.Range(0.2f, 1f), 1.2f);
            }
        }

        /// <summary>A soul circling the player; let loose, it flies at its enemy and gives mana when it hits.</summary>
        sealed class SoulOrb : MageThing
        {
            public float Angle;
            HealthManager prey;
            bool loose;
            public SoulOrb() { Life = 480; Size = new Vector2(0.8f, 0.8f) * MageMech.Block; HitEvery = 60; }
            public void Loose(HealthManager hm)
            {
                if (hm == null) return;
                prey = hm;
                loose = true;
                Hurts = true;
                Damage = MageMech.DamageOf(Def, 4.5f);
                Life = Age + 90;
            }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) { Done = true; return; }
                if (loose)
                {
                    if (prey == null || prey.isDead) { Done = true; return; }
                    Pos = Vector2.MoveTowards(Pos, Centre(prey), 0.7f * MageMech.Block);
                }
                else
                {
                    Angle += 3f;
                    float a = Angle * Mathf.Deg2Rad;
                    Pos = Vector2.MoveTowards(Pos, HeroCentre(hero) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2f * MageMech.Block, 0.5f * MageMech.Block);
                }
                Vel = Vector2.zero;
                if (Age % 3 == 0) MageMech.Dust(Pos, 59, Vector2.zero, 1.1f);
            }
            public override void Hit(HealthManager hm)
            {
                TerraMod.Instance?.Stats.RestoreMana(5);
                Burst(Pos, 59, 12, 3f, 1.3f);
                Done = true;
            }
            public override void Draw(FxBatch fx, Vector2 px) => FxBatch.Instance?.Light(Pos, Soul * 0.6f, 2f);
        }

        /// <summary>The Lightning Strike's judgement: lightning on every enemy within 25 blocks every half second for 3 s.</summary>
        sealed class Judgement : MageThing
        {
            public Judgement() { Life = 180; }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero != null) Pos = HeroCentre(hero) + Vector2.up * 8f * MageMech.Block;
                if (Age % 30 != 10) return;
                int n = 0;
                foreach (var e in Near(Pos, 26f, false))
                {
                    var c = Centre(e);
                    MageMech.Strike(Def, e, MageMech.DamageOf(Def, 0.5f));
                    Arc(new Vector2(c.x + Random.Range(-1f, 1f) * MageMech.Block, Pos.y), c, Def);
                    Burst(c, 226, 8, 3f);
                    n++;
                }
                if (n > 0) Sound("Item_122", 0.55f, 0.9f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 4; i++)
                    MageMech.Dust(Pos + new Vector2(Random.Range(-6f, 6f), Random.Range(-1f, 1f)) * MageMech.Block, 31, Vector2.zero, Random.Range(1.6f, 2.4f), 140);
                if (Age % 6 == 0) MageMech.Dust(Pos + new Vector2(Random.Range(-5f, 5f), -0.6f) * MageMech.Block, 226, Vector2.down * 3f, 1.1f);
                FxBatch.Instance?.Light(Pos, new Color(0.5f, 0.6f, 1f) * 0.6f, 8f);
            }
        }

        /// <summary>A fountain of ichor raining on every enemy near.</summary>
        sealed class Fountain : MageThing
        {
            int k;
            public Fountain() { Life = 180; }
            public override void Tick()
            {
                if (Age % 10 != 0) return;
                var near = Near(Pos + Vector2.up * 2f * MageMech.Block, 12f, false);
                if (near.Count == 0) return;
                var e = near[k++ % near.Count];
                var c = Centre(e);
                var from = Pos + Vector2.up * 1.5f * MageMech.Block;
                // A lob landing on it.
                var v = Brainless.Lob(from, c, 9f, 0.4f);
                Bolt(Def, ProjKind.GoldenShowerStream, from, v, 0.3f, new RangedShot { Debuff = EnemyDebuffs.Ichor, DebuffTicks = 300 });
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 3; i++) MageMech.Dust(Pos + new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0f, 2f)) * MageMech.Block, 170, Vector2.up * Random.Range(3f, 6f), 1.3f);
            }
        }

        /// <summary>A lob that lands on a point: the flatter arc (px an update, y up; gravity in px an update squared).</summary>
        static class Brainless
        {
            public static Vector2 Lob(Vector2 from, Vector2 to, float speed, float g)
            {
                var d = (to - from) / TerraParticles.Px;
                float t = Mathf.Max(8f, Mathf.Abs(d.x) / Mathf.Max(1f, speed));
                return new Vector2(d.x / t, d.y / t + 0.5f * g * t);
            }
        }

        /// <summary>A star gate dropping meteors, each at a different enemy.</summary>
        sealed class StarGate : MageThing
        {
            int k;
            public StarGate() { Life = 180; }
            public override void Tick()
            {
                if (Age % 12 != 0) return;
                var near = Near(Pos + Vector2.down * 10f * MageMech.Block, 16f, false);
                var to = near.Count > 0 ? Centre(near[k++ % near.Count]) : Pos + new Vector2(Random.Range(-6f, 6f), -12f) * MageMech.Block;
                var from = Pos + new Vector2(Random.Range(-3f, 3f), 0f) * MageMech.Block;
                Bolt(Def, ProjKind.Meteor, from, At(from, to, 11f), 1f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float open = Mathf.Clamp01(Age / 15f) * Mathf.Clamp01((Life - Age) / 15f);
                MageMech.Ring(Pos, 2.5f * MageMech.Block * open, 6, 3, 0f, 1.4f);
                MageMech.Ring(Pos, 1.6f * MageMech.Block * open, 15, 2, 0f, 1.2f);
                FxBatch.Instance?.Light(Pos, Fire * open, 6f);
            }
        }

        /// <summary>The Cursed Flames' catching fire: the enemy passes cursed fire to the others near every half second.</summary>
        sealed class Contagion : MageThing
        {
            HealthManager on;
            public static void On(ItemDef def, HealthManager hm, float seconds)
            {
                EnemyDebuffs.Add(hm, EnemyDebuffs.CursedInferno, 240);
                if (MageMech.MarkOf(hm, "contagion") > 0) return;
                MageMech.AddMark(hm, "contagion", 1, 1, seconds, Cursed, 75);
                MageMech.Add(new Contagion { Def = def, on = hm, Life = Mathf.RoundToInt(seconds * 60f), Pos = Centre(hm), Key = "contagion" }, 16);
            }
            public override void Tick()
            {
                if (on == null || on.isDead) { Done = true; return; }
                Pos = Centre(on);
                if (Age % 30 != 15) return;
                foreach (var e in Near(Pos, 3f, false))
                {
                    if (e == on) continue;
                    MageMech.Strike(Def, e, MageMech.DamageOf(Def, 0.15f));
                    Contagion.On(Def, e, 3f);
                    MageMech.Dust(Vector2.Lerp(Pos, Centre(e), 0.5f), 75, Vector2.zero, 1.4f);
                }
            }
        }

        /// <summary>A crystal whirlpool: crystals gather in it, then all shoot at the enemies near.</summary>
        sealed class CrystalVortex : MageThing
        {
            public CrystalVortex() { Life = 95; }
            public override void Tick()
            {
                if (Age != 90) return;
                var near = Near(Pos, 18f);
                int n = 14;
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    var from = Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.5f * MageMech.Block;
                    var to = near.Count > 0 ? Centre(near[i % near.Count]) : from + new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Bolt(Def, ProjKind.CrystalStormShard, from, At(from, to, 10f), 1.5f);
                }
                Sound("Item_109", 0.6f, 1.1f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                int n = 2 + Age / 8;
                for (int i = 0; i < n; i++)
                {
                    float a = Age * 0.2f + i * Mathf.PI * 2f / n;
                    float r = 1.5f * MageMech.Block;
                    Sprite(fx, "Projectile_94", TerraParticles.ToPx(Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r), Color.white, -a, 1f);
                }
            }
        }

        /// <summary>One of a ring of cursed fireballs spinning round the player, widening out.</summary>
        sealed class FireRing : MageThing
        {
            public float Angle;
            public FireRing() { Life = 120; Hurts = true; HitEvery = 30; Size = new Vector2(1.2f, 1.2f) * MageMech.Block; }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) { Done = true; return; }
                Angle += 6f;
                float r = Mathf.Lerp(1.5f, 10f, Age / (float)Life) * MageMech.Block, a = Angle * Mathf.Deg2Rad;
                Pos = HeroCentre(hero) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                Vel = Vector2.zero;
                MageMech.Dust(Pos, 75, Vector2.zero, 1.5f);
            }
            public override void Hit(HealthManager hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.CursedInferno, 240);
            public override void Draw(FxBatch fx, Vector2 px) => Sprite(fx, "Projectile_95", px, Color.white, Angle * Mathf.Deg2Rad, 1f);
        }

        /// <summary>Stone on an enemy: when it cracks, it hurts.</summary>
        sealed class Petrify : MageThing
        {
            HealthManager on;
            float k;
            public static void On(ItemDef def, HealthManager hm, float seconds, float k) =>
                MageMech.Add(new Petrify { Def = def, on = hm, k = k, Life = Mathf.RoundToInt(seconds * 60f), Pos = Centre(hm), Key = "petrify" }, 24);
            public override void Tick()
            {
                if (on == null || on.isDead) { Done = true; return; }
                Pos = Centre(on);
                if (Age % 6 == 0) MageMech.Dust(Pos + Random.insideUnitCircle * 0.5f, 31, Vector2.zero, 1.2f);
            }
            public override void End()
            {
                if (on == null || on.isDead) return;
                MageMech.Strike(Def, on, MageMech.DamageOf(Def, k));
                Burst(Pos, 31, 20, 4f, 1.5f);
            }
        }

        /// <summary>A spirit flame waiting by the player until an enemy comes within 12 blocks (or its prey is set), then flying at it.</summary>
        sealed class WaitingFlame : MageThing
        {
            public float Angle;
            public int Wait = 0;
            public HealthManager Prey;
            bool going;
            public WaitingFlame() { Life = 360; Size = new Vector2(0.8f, 0.8f) * MageMech.Block; HitEvery = 60; }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) { Done = true; return; }
                if (!going && Age >= Wait)
                {
                    if (Prey == null || Prey.isDead) Prey = Nearest(HeroCentre(hero), 12f);
                    if (Prey != null) { going = true; Hurts = true; Life = Age + 120; }
                }
                if (going)
                {
                    if (Prey == null || Prey.isDead) { Done = true; return; }
                    Pos = Vector2.MoveTowards(Pos, Centre(Prey), 0.6f * MageMech.Block);
                }
                else
                {
                    Angle += 2.5f;
                    float a = Angle * Mathf.Deg2Rad;
                    Pos = Vector2.MoveTowards(Pos, HeroCentre(hero) + new Vector2(Mathf.Cos(a) * 2.2f, 1.2f + Mathf.Sin(a) * 0.6f) * MageMech.Block, 0.4f * MageMech.Block);
                }
                Vel = Vector2.zero;
                MageMech.Dust(Pos, 59, Vector2.up * 0.6f, 1.2f);
            }
            public override void Hit(HealthManager hm) { Burst(Pos, 59, 12, 3f, 1.3f); Done = true; }
            public override void Draw(FxBatch fx, Vector2 px) => FxBatch.Instance?.Light(Pos, Soul * 0.5f, 2f);
        }
    }
}
