using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;
using RangedShot = TerraKnight.Items.TerraProjectiles.RangedShot;

namespace TerraKnight.Items
{
    /// <summary>The remade magic weapons' mechanisms (MageKits): stage 5, the grandest.</summary>
    internal static partial class MageKits
    {
        static readonly Color Snow = new Color(0.8f, 0.95f, 1f), PineGreen = new Color(0.4f, 0.85f, 0.3f), Sting = new Color(1f, 0.8f, 0.2f),
            Dragonfire = new Color(1f, 0.45f, 0.1f), NebulaPink = new Color(0.95f, 0.35f, 0.9f), TridentViolet = new Color(0.7f, 0.25f, 0.95f),
            Resonant = new Color(1f, 0.6f, 0.95f), Wired = new Color(0.45f, 0.9f, 1f), Moon = new Color(0.6f, 0.95f, 1f);

        static readonly Color[] Spectrum =
        {
            new Color(1f, 0.25f, 0.25f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.95f, 0.3f), new Color(0.35f, 1f, 0.4f),
            new Color(0.35f, 0.7f, 1f), new Color(0.45f, 0.4f, 1f), new Color(0.85f, 0.4f, 1f),
        };

        /// <summary>Torch dusts of the rainbow's colours, red to violet.</summary>
        static readonly int[] SpectrumDust = { 60, 6, 64, 61, 59, 62 };

        // ------------------------------------------------------------------ helpers (stage 5)

        /// <summary>The live enemies a band `width` blocks wide from one point to another (world) touches.</summary>
        static List<HealthManager> Along(Vector2 from, Vector2 to, float width)
        {
            var list = new List<HealthManager>();
            var way = to - from;
            float len = way.magnitude;
            if (len < 1e-4f) return list;
            foreach (var hit in Physics2D.CircleCastAll(from, width * B * 0.5f, way / len, len, 1 << 11))
            {
                var hm = hit.collider != null ? hit.collider.GetComponentInParent<HealthManager>() : null;
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || list.Contains(hm)) continue;
                list.Add(hm);
            }
            return list;
        }

        /// <summary>Where a line from a point stops at rock (world), `blocks` at most.</summary>
        static Vector2 Reach(Vector2 from, Vector2 dir, float blocks)
        {
            var hit = Physics2D.Raycast(from, dir, blocks * B, TerrainMask);
            return hit.collider != null ? hit.point : from + dir * blocks * B;
        }

        /// <summary>The first live enemy a line from a point meets before rock, within `blocks`.</summary>
        static HealthManager FirstAlong(Vector2 from, Vector2 dir, float blocks)
        {
            var end = Reach(from, dir, blocks);
            HealthManager best = null;
            float bestAt = float.MaxValue;
            foreach (var hm in Along(from, end, 1f))
            {
                float d = Vector2.Distance(from, Centre(hm));
                if (d < bestAt) { best = hm; bestAt = d; }
            }
            return best;
        }

        /// <summary>One of the stacked frames of a texture, drawn at px.</summary>
        static void Anim(FxBatch fx, string texture, int frames, int frame, Vector2 px, Color c, float rotation, float scale)
        {
            var tex = Terraria.TerrariaAssets.Texture(texture);
            if (tex == null) return;
            frames = Mathf.Max(1, frames);
            int h = tex.height / frames;
            fx.Draw(FxBatch.Frame(texture, 0, (frame % frames) * h, tex.width, h, tex.width / 2f, h / 2f), px, c, rotation, scale);
        }

        /// <summary>A line of still dust between two world points, a dust every `step` blocks, shaken by `jitter` blocks.</summary>
        static void DustLine(Vector2 a, Vector2 b, int dust, float step = 0.6f, float scale = 1f, float jitter = 0f)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a, b) / (step * B)), 1, 60);
            for (int i = 0; i <= n; i++) MageMech.Dust(Vector2.Lerp(a, b, i / (float)n) + Random.insideUnitCircle * jitter * B, dust, Vector2.zero, scale);
        }

        static float Facing(HeroController hero) => hero != null && hero.cState.facingRight ? 1f : -1f;

        static Vector2 Rotate(Vector2 v, float degrees) => Quaternion.Euler(0f, 0f, degrees) * v;

        /// <summary>The Rainbow Gun's colours on an enemy: red burns, orange scorches, yellow ichor, green poison, blue slows, violet confuses.</summary>
        static void Colour(ItemDef def, HealthManager e, int c, float scorch)
        {
            switch (c)
            {
                case 0: EnemyDebuffs.Add(e, EnemyDebuffs.OnFire, 180); break;
                case 1: MageMech.Strike(def, e, MageMech.DamageOf(def, scorch)); break;
                case 2: EnemyDebuffs.Add(e, EnemyDebuffs.Ichor, 240); break;
                case 3: EnemyDebuffs.Add(e, EnemyDebuffs.Poisoned, 240); break;
                case 4: MageMech.Slow(e, 0.55f, 0.8f); break;
                default: if (!EnemyStats.Unstoppable(EnemyStats.Of(e))) EnemyDebuffs.Add(e, EnemyDebuffs.Confused, 120); break;
            }
            Burst(Centre(e), SpectrumDust[Mathf.Clamp(c, 0, 5)], 8, 2.5f, 1.2f);
        }

        // ------------------------------------------------------------------ the Magnet Spheres in flight

        /// <summary>A Magnet Sphere in flight: where it was at its last tick (and when), and whether it has ended.</summary>
        sealed class SphereAt { public Vector2 Pos; public float At; public bool Gone; }
        static readonly List<SphereAt> spheres = new List<SphereAt>();

        /// <summary>The newest three spheres out (the oldest of four fades away: TerraProjectiles.StepMagnetSphere).</summary>
        static List<Vector2> LiveSpheres()
        {
            spheres.RemoveAll(x => x.Gone || Time.time - x.At > 0.25f);
            var list = new List<Vector2>();
            for (int i = Mathf.Max(0, spheres.Count - 3); i < spheres.Count; i++) list.Add(spheres[i].Pos);
            return list;
        }

        // ------------------------------------------------------------------ heat, beats, waits

        static int machineHeat, rayHeat;
        static float machineAt, rayAt, nightglowAt;

        /// <summary>The Sparkle Guitar's strums in a row: 1 to 4, then round again; a gap of more than `max` seconds starts again.</summary>
        static class Strum
        {
            static float last = -10f;
            static int combo;
            public static int Next(float max)
            {
                combo = Time.time - last <= max ? combo % 4 + 1 : 1;
                last = Time.time;
                return combo;
            }
        }

        static void PineHit(ItemDef def, HealthManager hm)
        {
            if (MageMech.AddMark(hm, "pine", 1, 8, 6f, PineGreen, 3) >= 8) PineBurst(def, hm, 1.6f);
        }

        /// <summary>The needles in an enemy burst: a blow, and six needles thrown all round.</summary>
        static void PineBurst(ItemDef def, HealthManager hm, float k)
        {
            MageMech.ClearMark(hm, "pine");
            var c = Centre(hm);
            MageMech.Strike(def, hm, MageMech.DamageOf(def, k));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + 0.3f;
                var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Bolt(def, ProjKind.PineNeedle, c + way * 1.2f * B, way * 9f, 0.25f);
            }
            Burst(c, 3, 16, 4f, 1.3f);
        }

        static void StingHit(ItemDef def, HealthManager hm)
        {
            int n = MageMech.MarkOf(hm, "sting");
            if (n > 0) MageMech.Strike(def, hm, MageMech.DamageOf(def, 0.04f * n));
            MageMech.AddMark(hm, "sting", 1, 10, 5f, Sting, 46);
            EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, 240);
        }

        static RangedShot WaspShot(ItemDef def) => new RangedShot { OnStrike = (hm, at) => StingHit(def, hm) };

        static void Lefts5()
        {
            // ---- Blizzard Staff: a field of icicles in the ground.
            MageLefts.Custom(ItemType.BlizzardStaff, "冰锥落地后插在地上 2 秒（最多 12 根），成片的冰刺扎伤走过的敌人（0.07 倍）并冻伤",
                "its icicles stick in the ground where they land for 2 s (12 at most), a field of spikes pricking what goes over them (0.07×) and frostburning",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnEnd = (at, vel) =>
                    {
                        var floor = Ground(at + Vector2.up * 0.3f * B, 1.5f);
                        if (Vector2.Distance(floor, at) > 1.4f * B) return;
                        MageMech.Add(new IceSpike { Def = def, Pos = floor + Vector2.up * 0.5f * B, Key = "ice_spike", Damage = MageMech.DamageOf(def, 0.07f) }, 12);
                    };
                    Cast(s, r);
                });

            // ---- Razorpine: needles that stay in, bursting at eight.
            MageLefts.Custom(ItemType.Razorpine, "松针扎进敌人后留在身上（最多 8 根），扎满时松针炸开（1.6 倍），向四周迸出六根松针（各 0.25 倍）",
                "its needles stay in what they strike (8 at most); at eight they burst (1.6×), throwing six needles all round (0.25× each)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) => PineHit(def, hm);
                    Cast(s, r);
                });

            // ---- Magnet Sphere: three at most, wired together.
            MageLefts.Custom(ItemType.MagnetSphere, "场上同时留三颗磁球（各 0.4 倍伤害），放出第四颗时最早那颗淡去；磁球之间拉出电线，碰到电线的敌人受到电击（0.35 倍）",
                "three spheres stay out (0.4× damage each), the oldest fading as a fourth goes out; they are wired to each other, and an enemy touching a wire is shocked (0.35×)",
                (s, r, run) =>
                {
                    TerraProjectiles.Instance?.SendAwaySpheres(2);
                    var sp = new SphereAt { Pos = s.From, At = Time.time };
                    spheres.Add(sp);
                    r.OnFly = (pos, vel) =>
                    {
                        sp.Pos = pos;
                        sp.At = Time.time;
                        return true;
                    };
                    r.OnEnd = (pos, vel) => sp.Gone = true;
                    // Three out at once: each sphere zaps at 0.4 of the one the game keeps.
                    var t = s;
                    t.Damage = Mathf.RoundToInt(s.Damage * 0.4f);
                    Cast(t, r);
                    if (MageMech.Count("magnet_web") == 0) MageMech.Add(new MagnetWeb { Def = s.Def, Pos = s.From, Key = "magnet_web", Damage = MageMech.DamageOf(s.Def, 0.35f) }, 1);
                });

            // ---- Bubble Gun: bubbles wrap enemies.
            MageLefts.Custom(ItemType.BubbleGun, "泡泡把打中的敌人包住 1 秒（小敌人被托起浮在泡泡里），泡泡破时受伤（0.4 倍）；一个敌人同时只被一个泡泡包住",
                "a bubble wraps what it strikes for a second (small enemies float up in it); when it pops, the enemy is hurt (0.4×); one bubble on an enemy at a time",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) => Bubbled.On(def, hm, 1f, 0.4f, false);
                    Cast(s, r);
                });

            // ---- Rainbow Gun: arches whose colours fall on what is under them.
            MageLefts.Custom(ItemType.RainbowGun, "彩虹落地处立起一道彩虹拱 3 秒（最多两道），拱下的敌人每半秒随机受到一种颜色：赤·着火、橙·灼伤（0.3 倍）、黄·灵液、绿·中毒、蓝·减速、紫·混乱",
                "where the rainbow lands an arch stands for 3 s (two at most); every half second each enemy under it takes a colour at random: red burns, orange scorches (0.3×), yellow ichor, green poison, blue slows, violet confuses",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnEnd = (at, vel) => MageMech.Add(new RainbowArch { Def = def, Pos = Ground(at + Vector2.up * B, 8f), Key = "rainbow_arch" }, 2);
                    Cast(s, r);
                });

            // ---- Laser Machinegun: it heats up, and vents.
            MageLefts.Pulse(ItemType.LaserMachinegun, 0.3f, MachineHeat,
                "扫射时机枪积热：每 0.3 秒向鼠标附近的不同敌人多射出激光，越热越多（1～4 道，各 0.3 倍）；热到顶时向四周排出一圈激光（16 道，各 0.35 倍）后冷却",
                "firing builds heat: every 0.3 s more lasers go out at different enemies by the cursor, more the hotter it runs (1-4, 0.3× each); at full heat it vents a ring of lasers (16, 0.35× each) and cools");

            // ---- Bat Scepter: the bats roost first.
            MageLefts.Custom(ItemType.BatScepter, "蝙蝠先倒挂在你身后（最多 8 只），等 14 格内有敌人时扑出（1.9 倍），各扑向不同的敌人；1 秒内没有敌人就飞向鼠标",
                "its bats hang behind you first (8 at most) and swoop out when an enemy comes within 14 blocks (1.9×), each at a different one; with none for a second they fly at the cursor",
                (s, r, run) =>
                {
                    var roost = MageMech.Things("roost_bat");
                    if (roost.Count >= 8 && roost[0] is RoostBat old) old.Go(BladeSkills.MageAim());
                    MageMech.Add(new RoostBat { Def = s.Def, Pos = s.From, Key = "roost_bat", Slot = run % 8 }, 9);
                });

            // ---- Wasp Gun: stings that stay.
            MageLefts.Custom(ItemType.WaspGun, "胡蜂的毒刺留在被叮的敌人身上（最多 10 根），每根让之后的叮咬多疼一点（每根 +0.04 倍），并让它中毒",
                "the wasps' stings stay in what they sting (10 at most), each making later stings hurt more (+0.04× a sting), and poison",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) => StingHit(def, hm);
                    Cast(s, r);
                });

            // ---- Leaf Blower: whirlwinds of leaves on the cursor.
            MageLefts.Custom(ItemType.LeafBlower, "叶片在鼠标处打旋，聚成一阵小旋风 2 秒（最多 3 阵），把附近的小敌人卷向中心，旋风里的敌人不断被叶片割伤（0.25 倍）",
                "its leaves gather into a little whirlwind on the cursor for 2 s (3 at most), drawing small enemies near into it and cutting what is inside (0.25×)",
                (s, r, run) =>
                {
                    r.Weave = 14f;
                    Cast(s, r);
                    if (run % 4 != 1) return;
                    var at = s.Mouse;
                    foreach (var t in MageMech.Things("leaf_whirl"))
                        if (t is LeafWhirl w && Vector2.Distance(w.Pos, at) < 3f * B) { w.Life = w.Age + 120; w.Leaves++; return; }
                    MageMech.Add(new LeafWhirl { Def = s.Def, Pos = at, Key = "leaf_whirl", Damage = MageMech.DamageOf(s.Def, 0.25f) }, 3);
                });

            // ---- Nightglow: the lights wait for the button to be let go.
            MageLefts.Custom(ItemType.Nightglow, "星光先悬停在你身后，等你松开左键才射出，悬停越久越强（0.45～0.8 倍）；悬停满 1.5 秒或超过八颗时自己射出",
                "its lights hang behind you until you let go of the button, then fly, stronger the longer they waited (0.45-0.8×); one that has waited 1.5 s, or a ninth, goes on its own",
                (s, r, run) =>
                {
                    nightglowAt = Time.time;
                    var waiting = MageMech.Things("glow_star");
                    if (waiting.Count >= 8 && waiting[0] is GlowStar old) old.Go();
                    MageMech.Add(new GlowStar { Def = s.Def, Pos = s.From, Key = "glow_star", Slot = run % 8, Wait = s.Def.UseAnimation + 0.2f }, 9);
                });

            // ---- Sparkle Guitar: strums building to a chord.
            MageLefts.Custom(ItemType.SparkleGuitar, "扫弦：连续弹奏时星星越来越亮越来越强（0.7～0.94 倍），每连到第 4 下奏出和弦：鼠标处向外绽开一圈八颗星星（各 0.2 倍）；停顿超过 0.7 秒从头来",
                "strummed in a row, its stars grow brighter and stronger (0.7× to 0.94×); every fourth strum is a chord, a ring of eight stars opening on the cursor (0.2× each); a pause of 0.7 s starts again",
                (s, r, run) =>
                {
                    int combo = Strum.Next(0.7f);
                    var t = s;
                    t.Damage = Mathf.RoundToInt(s.Damage * (0.62f + 0.08f * combo));
                    r.Size = 0.9f + 0.15f * combo;
                    r.Trail = combo * 0.2f;
                    Cast(t, r);
                    if (combo < 4) return;
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Bolt(s.Def, ProjKind.StellarTuneStar, s.Mouse + way * 0.6f * B, way * 5f, 0.2f, new RangedShot { Trail = 0.6f });
                    }
                    Burst(s.Mouse, 15, 20, 4f, 1.4f);
                    Sound("Item_9", 0.5f, 1.2f);
                });

            // ---- Charged Blaster Cannon: arcs round you while it charges.
            MageLefts.Pulse(ItemType.ChargedBlasterCannon, 0.5f, BlasterArcs, "蓄能和开火时身边浮起电弧，每 0.5 秒电击 12 格内的每一个敌人（各 0.3 倍）",
                "while it charges and fires, arcs float round you, shocking every enemy within 12 blocks every half second (0.3× each)");

            // ---- Betsy's Wrath: the dragon's mark.
            MageLefts.Custom(ItemType.BetsysWrath, "龙怒火球在命中的敌人身上留下龙焰印记（最多 5 层），每层让它的防御更低、着火更久",
                "its fireballs leave a dragonfire mark on what they strike (5 at most), each lowering its defence and burning it longer",
                (s, r, run) =>
                {
                    r.OnStrike = (hm, at) =>
                    {
                        int n = MageMech.AddMark(hm, "dragon", 1, 5, 8f, Dragonfire, 6);
                        EnemyDebuffs.Add(hm, EnemyDebuffs.BetsysCurse, 120 + 60 * n);
                        EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 120 + 60 * n);
                    };
                    Cast(s, r);
                });

            // ---- Nebula Arcanum: the core grows as it flies.
            MageLefts.Custom(ItemType.NebulaArcanum, "星云核飞行中把周围的星尘吸进来，越飞越大：命中时按飞了多久额外受伤（每秒 0.35 倍，最多 1 倍），并把 4 格内的小敌人吸过去",
                "its core draws the stardust round it in as it flies, growing: what it strikes takes more the longer it flew (0.35× a second, 1× at most), and small enemies within 4 blocks are drawn to it",
                (s, r, run) =>
                {
                    var def = s.Def;
                    float born = Time.time;
                    r.OnFly = (pos, vel) =>
                    {
                        float age = Time.time - born;
                        if (Random.value < 0.4f + age * 0.3f)
                        {
                            var way = Random.insideUnitCircle.normalized;
                            float far = (1.5f + Mathf.Min(2f, age)) * B;
                            MageMech.Dust(pos + way * far, Random.value < 0.5f ? 86 : 90, -way * 3f, 1f + Mathf.Min(1f, age * 0.5f));
                        }
                        return true;
                    };
                    r.OnStrike = (hm, at) =>
                    {
                        float age = Time.time - born;
                        if (age > 0.25f) MageMech.Strike(def, hm, MageMech.DamageOf(def, Mathf.Min(1f, 0.35f * age)));
                        MageMech.Add(new Well { Def = def, Pos = at, Key = "nebula_well", Radius = 4f, DustType = 86 }, 6);
                    };
                    Cast(s, r);
                });

            // ---- Heat Ray: it runs hotter.
            MageLefts.Custom(ItemType.HeatRay, "连续射击时射线越来越热：每发热度 +1（最多 20），伤害从 0.8 倍升到 1.2 倍；热到顶时射线变粗一倍、多穿透三个敌人并点燃；停火 1 秒冷却",
                "firing in a row it runs hotter: +1 heat a shot (20 at most), its damage rising from 0.8× to 1.2×; at full heat the ray is twice as thick, goes through three more enemies and sets them alight; a second's pause cools it",
                (s, r, run) =>
                {
                    if (Time.time - rayAt > 1f) rayHeat = 0;
                    rayAt = Time.time;
                    rayHeat = Mathf.Min(20, rayHeat + 1);
                    var t = s;
                    t.Damage = Mathf.RoundToInt(s.Damage * (0.8f + 0.02f * rayHeat));
                    if (rayHeat >= 20)
                    {
                        r.Size = 2f;
                        r.Pierce = 3;
                        r.Debuff = EnemyDebuffs.OnFire;
                        r.DebuffTicks = 180;
                        r.Trail = 0.6f;
                        r.TrailDust = 6;
                    }
                    Cast(t, r);
                    for (int i = 0; i < 1 + rayHeat / 5; i++) MageMech.Dust(s.From + Random.insideUnitCircle * 0.3f * B, 6, Vector2.up * Random.Range(1f, 2.5f), 0.9f + rayHeat * 0.04f);
                });

            // ---- Nettle Burst: the vine roots what it meets first.
            MageLefts.Custom(ItemType.NettleBurst, "藤蔓缠住伸出方向上碰到的第一个敌人，把它定住 1 秒（首领和精英只减速）并中毒，松开时勒一下（0.5 倍）；同一个敌人 2 秒内只缠一次",
                "its vine roots the first enemy in its way, holding it a second (bosses and elites only slowed) and poisoning it, squeezing as it lets go (0.5×); an enemy is rooted once in 2 s",
                (s, r, run) =>
                {
                    Cast(s, r);
                    var dir = s.Vel.sqrMagnitude > 1e-6f ? s.Vel.normalized : (Vector2)(s.Mouse - s.From).normalized;
                    var e = FirstAlong(s.From, dir, 16f);
                    if (e == null || MageMech.MarkOf(e, "rooted") > 0) return;
                    Root.On(s.Def, e, 1f, 0.5f);
                });

            // ---- Razorblade Typhoon: it gathers blades as it cuts, then splits.
            MageLefts.Custom(ItemType.RazorbladeTyphoon, "台风（0.8 倍）每切中一次就卷起一片刀刃，越切越重（每次 +0.15 倍）；切到第四次时分出两股小台风，各自追向别的敌人（各 0.5 倍）",
                "the typhoon (0.8×) picks up a blade with each cut, cutting harder (+0.15× a cut); at the fourth it throws off two small typhoons after other enemies (0.5× each)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    int cuts = 0;
                    r.OnStrike = (hm, at) =>
                    {
                        cuts++;
                        if (cuts > 1) MageMech.Strike(def, hm, MageMech.DamageOf(def, 0.15f * Mathf.Min(6, cuts - 1)));
                        Burst(at, 33, 6, 3f);
                        if (cuts != 4) return;
                        int n = 0;
                        foreach (var e in Near(at, 16f))
                        {
                            if (e == hm) continue;
                            Bolt(def, ProjKind.RazorbladeTyphoon, at, At(at, Centre(e), 6f), 0.5f, new RangedShot { Size = 0.7f });
                            if (++n >= 2) break;
                        }
                    };
                    var t = s;
                    t.Damage = Mathf.RoundToInt(s.Damage * 0.8f);
                    Cast(t, r);
                });

            // ---- Shadowbeam Staff: each bend off a wall splits a beam off; the prism splits it more.
            MageLefts.Custom(ItemType.ShadowbeamStaff, "光束每在墙上折射一次，就分出一道射向附近敌人的折射光（0.5 倍，每束最多三次）；穿过暗影棱镜时分成射向棱镜附近每个敌人的光束（各 0.6 倍）",
                "each time the beam bends off a wall it splits off a beam at an enemy near (0.5×, three times a beam at most); through the shadow prism it splits into beams at every enemy near the prism (0.6× each)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    int splits = 0;
                    bool prismed = false;
                    r.OnBounce = (pos, vel) =>
                    {
                        if (splits++ >= 3) return;
                        var e = Nearest(pos, 20f);
                        if (e == null) return;
                        Bolt(def, ProjKind.Shadowbeam, pos, At(pos, Centre(e), Mathf.Max(4f, vel.magnitude)), 0.5f);
                        Burst(pos, 27, 6, 2f);
                    };
                    r.OnFly = (pos, vel) =>
                    {
                        if (prismed) return true;
                        foreach (var t in MageMech.Things("shadow_prism"))
                            if (t is ShadowPrism p && Vector2.Distance(pos, p.Pos) < 1.6f * B) { prismed = true; p.Refract(0.6f); break; }
                        return true;
                    };
                    Cast(s, r);
                });

            // ---- Lunar Flare: moonlight where the flares land.
            MageLefts.Custom(ItemType.LunarFlareBook, "月耀落地处留下一片月光 2 秒（最多 6 片），站在月光里的敌人每半秒受伤（0.2 倍）",
                "where a flare lands it leaves a pool of moonlight for 2 s (6 at most); an enemy standing in it is hurt every half second (0.2×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnEnd = (at, vel) => MageMech.Add(new Moonlight { Def = def, Pos = Ground(at + Vector2.up * B, 3f) + Vector2.up * 0.2f, Key = "moonlight", Damage = MageMech.DamageOf(def, 0.2f) }, 6);
                    Cast(s, r);
                });

            // ---- Last Prism: its beam marks every enemy it sweeps over.
            MageLefts.Pulse(ItemType.LastPrism, 0.25f, PrismMarks, "光束扫过的每一个敌人都被打上棱镜印记（最多 6 层），叠满时那个敌人头顶落下一颗神圣之星（1.5 倍）",
                "every enemy the beam sweeps over takes a prism mark (6 at most); at six a hallowed star falls on that enemy (1.5×)");

            // ---- Spectre Staff: the wisps come back to circle you.
            MageLefts.Custom(ItemType.SpectreStaff, "幽魂命中后回到你身边环绕 10 秒（最多 6 只），环绕的幽魂撞伤靠近你的敌人（0.4 倍）",
                "a wisp that strikes comes back to circle you for 10 s (6 at most); the circling wisps hurt the enemies that come close (0.4×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    bool back = false;
                    r.OnStrike = (hm, at) =>
                    {
                        if (back) return;
                        back = true;
                        MageMech.Add(new OrbitWisp { Def = def, Pos = at, Key = "orbit_wisp", Slot = MageMech.Count("orbit_wisp"), Damage = MageMech.DamageOf(def, 0.4f) }, 6);
                    };
                    Cast(s, r);
                });

            // ---- Inferno Fork: a pillar of hellfire where it bursts.
            MageLefts.Custom(ItemType.InfernoFork, "狱火炸开处升起一道狱火柱 2 秒（最多 4 道），碰到的敌人受伤（0.3 倍）并着狱火",
                "where the hellfire bursts a pillar of it rises for 2 s (4 at most), hurting what touches it (0.3×) with hellfire",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnEnd = (at, vel) => MageMech.Add(new HellPillar { Def = def, Pos = Ground(at + Vector2.up * B, 4f) + Vector2.up * 2.5f * B, Key = "hell_pillar", Damage = MageMech.DamageOf(def, 0.3f) }, 4);
                    Cast(s, r);
                });

            // ---- Nebula Blaze: shards after the others; the nebula mark.
            MageLefts.Custom(ItemType.NebulaBlaze, "星云火在命中的敌人身上叠星云印记（最多 5 层，每层让星云火对它多 4% 伤害），并分出三颗星云碎片追向附近别的敌人（各 0.35 倍）",
                "its blaze stacks a nebula mark on what it strikes (5 at most, each making the blaze hurt it 4% more) and throws three nebula shards after the other enemies near (0.35× each)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    bool split = false;
                    r.OnStrike = (hm, at) =>
                    {
                        int n = MageMech.MarkOf(hm, "nebula");
                        if (n > 0) MageMech.Strike(def, hm, MageMech.DamageOf(def, 0.04f * n));
                        MageMech.AddMark(hm, "nebula", 1, 5, 6f, NebulaPink, 86);
                        if (split) return;
                        split = true;
                        int k = 0;
                        foreach (var e in Near(at, 14f))
                        {
                            if (e == hm) continue;
                            MageMech.Add(new Comet { Def = def, Target = e, Pos = at, Texture = "Projectile_634", Frames = 4, DustType = 86, Speed = 0.6f, Scale = 0.6f, Key = "nebula_shard", Damage = MageMech.DamageOf(def, 0.35f) }, 24);
                            if (++k >= 3) break;
                        }
                    };
                    Cast(s, r);
                });

            // ---- Staff of Earth: the longer it rolls, the harder it hits.
            MageLefts.Custom(ItemType.StaffofEarth, "巨石滚得越久砸得越重：命中时每滚 1 秒多 0.5 倍（最多 0.75 倍），沿路碾起碎石",
                "its boulder hits harder the longer it has rolled: 0.5× more a second (0.75× at most), throwing up gravel as it goes",
                (s, r, run) =>
                {
                    var def = s.Def;
                    float born = Time.time;
                    int tick = 0;
                    r.OnFly = (pos, vel) =>
                    {
                        if (++tick % 4 == 0) MageMech.Dust(pos + Vector2.down * 0.5f * B, 1, new Vector2(-Mathf.Sign(vel.x) * 1.5f, 1.5f), 1.1f);
                        return true;
                    };
                    r.OnStrike = (hm, at) =>
                    {
                        float t = Time.time - born;
                        if (t > 0.2f) MageMech.Strike(def, hm, MageMech.DamageOf(def, Mathf.Min(0.75f, 0.5f * t)));
                        Burst(at, 1, 8, 3f);
                    };
                    Cast(s, r);
                });

            // ---- Unholy Trident: three marks and the enemy is dragged to you.
            MageLefts.Custom(ItemType.UnholyTrident, "三叉戟在敌人身上留下三叉印记，叠到三层时印记爆开（0.8 倍），把它拖向你（首领和精英不受牵引）并点燃暗影焰",
                "its tridents leave a trident mark; at three the marks burst (0.8×), dragging the enemy toward you (not bosses or elites) and setting it alight with shadowflame",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) =>
                    {
                        if (MageMech.AddMark(hm, "trident", 1, 3, 6f, TridentViolet, 27) < 3) return;
                        MageMech.ClearMark(hm, "trident");
                        MageMech.Strike(def, hm, MageMech.DamageOf(def, 0.8f));
                        EnemyDebuffs.Add(hm, EnemyDebuffs.Shadowflame, 240);
                        MageMech.Add(new Drag { Def = def, On_ = hm, Life = 30, Key = "drag" }, 12);
                        Burst(Centre(hm), 27, 16, 3.5f, 1.4f);
                    };
                    Cast(s, r);
                });

            // ---- Toxic Flask: the clouds drift on the way you face.
            MageLefts.Custom(ItemType.ToxicFlask, "毒瓶碎裂处多冒出一团毒云，顺着你的朝向慢慢飘 4 秒（最多 4 团），云里的敌人中毒受伤（0.2 倍）",
                "where the flask breaks one more cloud rises and drifts slowly the way you face for 4 s (4 at most), poisoning and hurting what is in it (0.2×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    float face = s.Mouse.x >= s.From.x ? 1f : -1f;
                    r.OnEnd = (at, vel) => MageMech.Add(new DriftCloud { Def = def, Pos = at + Vector2.up * 0.5f * B, Vel = new Vector2(face * 0.03f * B, 0f), Key = "drift_cloud", Damage = MageMech.DamageOf(def, 0.2f) }, 4);
                    Cast(s, r);
                });

            // ---- Resonance Scepter: what is struck resonates with the others.
            MageLefts.Custom(ItemType.PrincessWeapon, "音波环让碰到的敌人共鸣（最多 5 层）；打中一个共鸣的敌人时，其他每个共鸣的敌人都跟着受到回响（每层 0.08 倍）",
                "its rings set what they touch resonating (5 at most); striking one that resonates, every other one resonating takes an echo (0.08× a stack)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) =>
                    {
                        MageMech.AddMark(hm, "resonance", 1, 5, 5f, Resonant, 267);
                        var c = Centre(hm);
                        foreach (var (e, n) in MageMech.Marked("resonance"))
                        {
                            if (e == hm || Vector2.Distance(Centre(e), c) > 25f * B) continue;
                            MageMech.Strike(def, e, MageMech.DamageOf(def, 0.08f * n));
                            DustLine(c, Centre(e), 267, 1f, 0.9f);
                        }
                    };
                    Cast(s, r);
                });

            // ---- Arc Surge: the arcs leave wires between enemies.
            MageLefts.Custom(ItemType.ArcSurge, "电弧命中后跳向附近另外两个敌人（各 0.3 倍），被连过的敌人之间留下 3 秒电线，碰到电线的敌人触电（0.2 倍）",
                "its arc leaps on from what it strikes to two more enemies (0.3× each), leaving wires between them for 3 s that shock what touches them (0.2×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    bool leapt = false;
                    r.OnStrike = (hm, at) =>
                    {
                        MageMech.AddMark(hm, "wired", 1, 1, 6f, Wired, 226);
                        if (leapt) return;
                        leapt = true;
                        int n = 0;
                        foreach (var e in Near(Centre(hm), 10f))
                        {
                            if (e == hm) continue;
                            MageMech.Strike(def, e, MageMech.DamageOf(def, 0.3f));
                            MageMech.AddMark(e, "wired", 1, 1, 6f, Wired, 226);
                            Arc(Centre(hm), Centre(e), def);
                            MageMech.Add(new WireLink { Def = def, A = hm, Z = e, Pos = Centre(hm), Key = "arc_wire", Damage = MageMech.DamageOf(def, 0.2f) }, 12);
                            if (++n >= 2) break;
                        }
                    };
                    Cast(s, r);
                });
        }

        // ------------------------------------------------------------------ the channelled weapons' pulses

        static void MachineHeat(HeroController hero, ItemDef def)
        {
            if (Time.time - machineAt > 1f) machineHeat = 0;
            machineAt = Time.time;
            machineHeat++;
            var from = HeroCentre(hero);
            var aim = BladeSkills.MageAim();
            var near = Near(aim, 12f);
            int n = 1 + machineHeat / 4;
            for (int i = 0; i < n; i++)
            {
                var to = near.Count > 0 ? Centre(near[i % near.Count]) : aim + Random.insideUnitCircle * 2f * B;
                Bolt(def, ProjKind.MachinegunLaser, from, At(from, to, 16f), 0.3f, new RangedShot { Trail = 0.4f });
            }
            for (int i = 0; i < machineHeat; i++) MageMech.Dust(from + Random.insideUnitCircle * 0.4f * B, 6, Vector2.up * Random.Range(1f, 2f), 0.8f + machineHeat * 0.06f);
            if (machineHeat < 12) return;
            machineHeat = 0;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Bolt(def, ProjKind.MachinegunLaser, from + way * 0.8f * B, way * 14f, 0.35f, new RangedShot { Trail = 0.6f });
            }
            Burst(from, 31, 30, 5f, 1.6f);
            Sound("Item_14", 0.5f, 1.4f);
        }

        static void BlasterArcs(HeroController hero, ItemDef def)
        {
            var c = HeroCentre(hero);
            for (int i = 0; i < 6; i++) MageMech.Dust(c + Random.insideUnitCircle * 1.6f * B, 226, Random.insideUnitCircle, 1.1f);
            foreach (var e in Near(c, 12f))
            {
                MageMech.Strike(def, e, MageMech.DamageOf(def, 0.3f));
                Arc(c + Random.insideUnitCircle * B, Centre(e), def);
            }
        }

        static void PrismMarks(HeroController hero, ItemDef def)
        {
            var from = HeroCentre(hero);
            var way = BladeSkills.MageAim() - from;
            if (way.sqrMagnitude < 1e-6f) return;
            var end = Reach(from, way.normalized, 50f);
            foreach (var e in Along(from, end, 2f))
            {
                if (MageMech.AddMark(e, "prism", 1, 6, 4f, Color.white, 66) < 6) continue;
                MageMech.ClearMark(e, "prism");
                var c = Centre(e);
                MageMech.Add(new Comet { Def = def, Target = e, Pos = c + new Vector2(Random.Range(-4f, 4f), 16f) * B, Texture = "Projectile_92", DustType = 58,
                    Tint = Spectrum[Random.Range(0, Spectrum.Length)], Key = "prism_star", Damage = MageMech.DamageOf(def, 1.5f) }, 40);
            }
        }

        static void Arts5()
        {
            // ---- Blizzard Staff: a blizzard.
            Art(ItemType.BlizzardStaff).Press("暴风雪", 7f, 3f, "鼠标处刮起 3 秒暴风雪：9 格内的所有敌人减速、冻伤，每半秒受寒（0.4 倍），冰锥轮流砸向其中的每一个敌人（各 0.5 倍）",
                "a blizzard on the cursor for 3 s: every enemy within 9 blocks is slowed and frostburnt and chilled every half second (0.4×), and icicles fall on each of them in turn (0.5× each)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    MageMech.Add(new Blizzard { Def = d, Pos = at, Key = "blizzard" }, 1);
                    BladeSkills.MageArtCast(d, at);
                    Sound("Item_27", 0.6f, 0.7f);
                    return true;
                });

            // ---- Razorpine: a storm of needles round you, then every needle bursts.
            Art(ItemType.Razorpine).Press("针叶风暴", 7f, 3f, "身边转起一圈松针 3 秒，松针从旋转的针圈甩出、转向附近的敌人（各 0.6 倍）；结束时所有扎着松针的敌人一齐炸开（1.6 倍）",
                "a ring of needles spins round you for 3 s, flinging needles off the spinning ring that turn onto the enemies near (0.6× each); as it ends, every enemy with needles in it bursts at once (1.6×)",
                (h, d) =>
                {
                    MageMech.Add(new PineSpin { Def = d, Pos = HeroCentre(h), Key = "pine_spin" }, 1);
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });

            // ---- Magnet Sphere: every sphere discharges at every enemy.
            Art(ItemType.MagnetSphere).Press("磁暴", 6f, 3f, "场上的磁球（不足三颗时在你身边补上）在 1.3 秒内六次放电，打 20 格内的每一个敌人（每颗每次 1.4 倍），最后磁球炸开",
                "the spheres out, made up to three by spheres round you, discharge six times over 1.3 s at every enemy within 20 blocks (1.4× a sphere a time), then burst",
                (h, d) =>
                {
                    MageMech.Add(new MagnetStorm { Def = d, Pos = HeroCentre(h), Key = "magnet_storm" }, 1);
                    BladeSkills.MageArtCast(d, null);
                    Sound("Item_93", 0.6f, 0.9f);
                    return true;
                });

            // ---- Bubble Gun: a sea of bubbles.
            Art(ItemType.BubbleGun).Press("泡泡海", 6f, 3f, "鼠标处冒出一大片泡泡 2 秒：9 格内的所有敌人被泡泡包住浮起，泡泡不停从下面撞上它们（每秒七次，各 1.5 倍），最后一齐破裂（8 倍）",
                "a sea of bubbles on the cursor for 2 s: every enemy within 9 blocks is wrapped and floats up, bubbles keep rising into them (seven times a second, 1.5× each), and all pop at the end (8×)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    MageMech.Add(new BubbleSea { Def = d, Pos = at, Key = "bubble_sea" }, 1);
                    BladeSkills.MageArtCast(d, at);
                    Sound("Item_85", 0.7f, 0.8f);
                    return true;
                });

            // ---- Rainbow Gun: two rainbows crossing.
            Art(ItemType.RainbowGun).Press("双虹", 6f, 3f, "两道彩虹从鼠标两侧 14 格外升起，划过碰到的敌人（各 2 倍），在鼠标处交汇炸开：7 格内的每个敌人受到所有颜色（8 倍），交汇处留下一道大彩虹拱 3 秒（每半秒 0.8 倍和一种颜色）",
                "two rainbows rise from 14 blocks either side of the cursor, cutting what they touch (2× each), and cross on it in a burst: every enemy within 7 blocks takes every colour (8×), and a great arch stands where they met for 3 s (0.8× and a colour every half second)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    for (int side = -1; side <= 1; side += 2)
                        MageMech.Add(new RainbowBeam { Def = d, From = at + new Vector2(side * 14f, -1f) * B, To = at, Pos = at + new Vector2(side * 14f, -1f) * B, Crosses = side > 0, Key = "double_rainbow", Damage = MageMech.DamageOf(d, 2f) }, 2);
                    BladeSkills.MageArtCast(d, at);
                    Sound("Item_67", 0.6f, 0.9f);
                    return true;
                });

            // ---- Laser Machinegun: a fan of lasers swept there and back.
            Art(ItemType.LaserMachinegun).Press("激光扇", 6f, 3f, "激光机枪向前展开成扇形来回扫射，扇面越扫越窄、最后收拢到准星上（每道 0.9 倍，穿透三个），扫中的每个敌人按被扫中的次数再受一次灼烧（每次 0.8 倍）",
                "the gun fans out and sweeps its lasers to and fro, the fan narrowing until it closes on the aim (0.9× each, through three); every enemy swept then burns once more for each time it was struck (0.8× a time)",
                (h, d) =>
                {
                    BladeSkills.MageTurn(h);
                    MageMech.Add(new LaserFan { Def = d, Pos = HeroCentre(h), Key = "laser_fan" }, 1);
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });

            // ---- Bat Scepter: a storm of bats round the cursor.
            Art(ItemType.BatScepter).Press("蝙蝠风暴", 7f, 3f, "蝙蝠群绕着鼠标盘旋成风暴 3 秒，跟着鼠标移动，每 1/6 秒咬一次风暴里的每个敌人（1.5 倍），每咬一口为你回 1 点生命（最多 12 点）；风暴散开时八只蝙蝠扑向附近的敌人（各 1 倍）",
                "a storm of bats circles the cursor for 3 s, following it and biting every enemy inside every sixth of a second (1.5×), each bite healing you 1 (12 at most); as it breaks up, eight bats swoop at the enemies near (1× each)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    MageMech.Add(new BatStorm { Def = d, Pos = at, Key = "bat_storm" }, 1);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- Wasp Gun: a nest that swarms out when it is crowded.
            Art(ItemType.WaspGun).Press("胡蜂巢", 7f, 3f, "鼠标处落下一个胡蜂巢 4 秒，不停放出胡蜂（各 0.8 倍）；巢边 6 格内有三个以上敌人或时间到时，胡蜂一齐扑出（十只，各 0.8 倍）",
                "a wasps' nest drops on the cursor for 4 s, letting out wasps (0.8× each); when three or more enemies come within 6 blocks of it, or its time is up, the whole swarm bursts out (ten, 0.8× each)",
                (h, d) =>
                {
                    var at = Ground(BladeSkills.MageAim(), 12f) + Vector2.up * 0.8f * B;
                    MageMech.Add(new WaspNest { Def = d, Pos = at, Key = "wasp_nest" }, 1);
                    BladeSkills.MageArtCast(d, at);
                    Sound("Item_97", 0.6f, 0.8f);
                    return true;
                });

            // ---- Leaf Blower: every whirl of leaves gathered into a storm.
            Art(ItemType.LeafBlower).Press("落叶风暴", 7f, 3f, "把场上的小旋风全部卷起，合成一股大旋风从你面前慢慢向前推进，碰到敌人时停下来绞，卷住的小敌人不断被割伤（每秒七次 0.8 倍，每卷入一阵小旋风 +25%）",
                "every whirl of leaves out is gathered into one great whirlwind that drives slowly ahead of you, lingering where there are enemies, carrying small ones along and cutting them (0.8× seven times a second, +25% for each whirl gathered)",
                (h, d) =>
                {
                    float face = BladeSkills.MageTurn(h);
                    var from = HeroCentre(h);
                    int gathered = 0;
                    foreach (var t in MageMech.Things("leaf_whirl"))
                    {
                        gathered += 1 + (t is LeafWhirl w ? w.Leaves / 2 : 0);
                        DustLine(t.Pos, from, 3, 0.8f, 1.2f, 0.3f);
                        t.Done = true;
                    }
                    MageMech.Add(new LeafStorm { Def = d, Pos = from + new Vector2(face * 2f, 0f) * B, Dir = face, Key = "leaf_storm",
                        Damage = MageMech.DamageOf(d, 0.8f * (1f + 0.25f * Mathf.Min(6, gathered))) }, 1);
                    BladeSkills.MageArtCast(d, null);
                    Sound("Item_43", 0.6f, 0.7f);
                    return true;
                });

            // ---- Nightglow: ten lights round you, each at its own enemy.
            Art(ItemType.Nightglow).Press("夜之光华", 6f, 3f, "十道星光绕你转一圈，再依次射向不同的敌人（各 0.35 倍）；身后悬停着的星光也以最强的力量一同射出",
                "ten lights go round you once, then fly one after another at different enemies (0.35× each); the lights hanging behind you go with them at their strongest",
                (h, d) =>
                {
                    foreach (var t in MageMech.Things("glow_star")) if (t is GlowStar g) g.Go(0.8f);
                    MageMech.Add(new NightRing { Def = d, Pos = HeroCentre(h), Key = "night_ring" }, 1);
                    BladeSkills.MageArtCast(d, null, 1.1f);
                    Sound("Item_9", 0.6f, 0.8f);
                    return true;
                });

            // ---- Sparkle Guitar: a solo on the beat over every enemy.
            Art(ItemType.SparkleGuitar).Press("星之独奏", 6f, 3f, "弹一段 3 秒的独奏：每一拍星星落在 18 格内的每一个敌人身上（0.9 倍），每第四拍是重音（1.8 倍）",
                "a solo of 3 s: on every beat stars fall on every enemy within 18 blocks (0.9×), every fourth beat a heavy one (1.8×)",
                (h, d) =>
                {
                    MageMech.Add(new Solo { Def = d, Pos = HeroCentre(h), Key = "solo" }, 1);
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });

            // ---- Charged Blaster Cannon: a full-charge shot.
            Art(ItemType.ChargedBlasterCannon).Charge("满能炮击", 1.5f, 3f, "按住蓄力，松开射出一道巨大的电浆炮，贯穿一条线上的所有敌人（最多 10 倍），落点炸开电浆（最多 2 倍）",
                "hold to charge; let go for a great plasma shot through everything in its line (up to 10×) that bursts where it ends (up to 2×)",
                (h, d, c) =>
                {
                    BladeSkills.MageTurn(h);
                    var from = HeroCentre(h);
                    var way = BladeSkills.MageAim() - from;
                    var dir = way.sqrMagnitude > 1e-6f ? way.normalized : Vector2.right * Facing(h);
                    var end = Reach(from, dir, 50f);
                    foreach (var e in Along(from, end, 2f + 2f * c)) MageMech.Strike(d, e, MageMech.DamageOf(d, 2f + 8f * c));
                    foreach (var e in Near(end, 4f, false)) MageMech.Strike(d, e, MageMech.DamageOf(d, 2f * c));
                    for (int i = 0; i < 3; i++) DustLine(from, end, 226, 0.5f, 1.3f + c, 0.25f * (1f + c));
                    Burst(end, 226, 30, 6f, 1.6f);
                    Bolt(d, ProjKind.ChargedBlasterOrb, from, dir * 14f, 0.3f, new RangedShot { Size = 1.5f + 2f * c, Pierce = 30, Trail = 1f });
                    BladeSkills.MageArtCast(d, null, 1f + c);
                    Sound("Item_92", 0.8f, 0.8f);
                });

            // ---- Betsy's Wrath: the dragon's breath.
            Art(ItemType.BetsysWrath).Press("龙息", 7f, 3f, "身后浮现龙的轮廓，向鼠标方向喷出 1.5 秒龙息（14 格内的锥形，每次 1.5 倍，并叠龙焰印记）；龙息将尽时所有带龙焰印记的敌人同时燃起龙火，按层数受伤（每层 1.2 倍）",
                "a dragon's shape rises behind you and breathes fire toward the cursor for 1.5 s (a cone 14 blocks long, 1.5× a time, marking); as the breath ends every enemy with the dragon's mark bursts into dragonfire, hurt by its marks (1.2× each)",
                (h, d) =>
                {
                    MageMech.Add(new DragonBreath { Def = d, Pos = HeroCentre(h), Key = "dragon_breath" }, 1);
                    BladeSkills.MageArtCast(d, null, 1.2f);
                    Sound("Item_34", 0.7f, 0.7f);
                    return true;
                });

            // ---- Nebula Arcanum: the heart of a nebula.
            Art(ItemType.NebulaArcanum).Press("星云之心", 7f, 3f, "鼠标处展开星云之心 3 秒：把 10 格内的所有小敌人吸向中心，每 1/6 秒伤害其中每个敌人（2 倍），最后坍缩（8 倍）",
                "the heart of a nebula opens on the cursor for 3 s, drawing every small enemy within 10 blocks to its middle and hurting each inside every sixth of a second (2×), then collapsing (8×)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    MageMech.Add(new NebulaHeart { Def = d, Pos = at, Key = "nebula_heart" }, 1);
                    BladeSkills.MageArtCast(d, at, 1.2f);
                    Sound("Item_117", 0.6f, 0.7f);
                    return true;
                });

            // ---- Heat Ray: a ray that melts the ground.
            Art(ItemType.HeatRay).Press("熔光", 6f, 3f, "射出一道粗射线（3 倍，穿透十个），同时在你和鼠标之间的地面熔出一条熔岩带 4 秒，走过的敌人着火受伤（0.8 倍）",
                "a thick ray (3×, through ten) that melts a strip of lava into the ground between you and the cursor for 4 s, burning what goes over it (0.8×)",
                (h, d) =>
                {
                    BladeSkills.MageTurn(h);
                    var from = HeroCentre(h);
                    var to = BladeSkills.MageAim();
                    Bolt(d, ProjKind.HeatRay, from, At(from, to, 15f), 3f, new RangedShot { Size = 2f, Pierce = 10, Trail = 1f, TrailDust = 6, Debuff = EnemyDebuffs.OnFire, DebuffTicks = 240 });
                    float dx = to.x - from.x;
                    int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(dx) / (1.2f * B)), 4, 24);
                    var tp = TerraProjectiles.Instance;
                    for (int i = 0; i <= n; i++)
                    {
                        var at = Ground(new Vector2(from.x + dx * i / n, Mathf.Max(from.y, to.y) + 2f * B), 12f) + Vector2.up * 0.4f * B;
                        tp?.Later(i * 2, (f, v) => MageMech.Add(new LavaStrip { Def = d, Pos = at, Key = "lava_strip", Damage = MageMech.DamageOf(d, 0.8f) }, 25));
                    }
                    BladeSkills.MageArtCast(d, null);
                    Sound("Item_20", 0.7f, 0.8f);
                    return true;
                });

            // ---- Nettle Burst: a sea of vines.
            Art(ItemType.NettleBurst).Press("藤蔓之海", 7f, 3f, "鼠标处地面伸出一片藤蔓：10 格内的所有敌人被缠住 2.5 秒（首领和精英只减速）并受伤（4 倍），被缠住时每 1/6 秒被荆棘扎一下（0.8 倍），藤蔓松开时再勒一下（4 倍）",
                "vines burst out of the ground on the cursor: every enemy within 10 blocks is rooted for 2.5 s (bosses and elites only slowed) and hurt (4×), pricked by thorns every sixth of a second while held (0.8×) and squeezed as the vines let go (4×)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    MageMech.Add(new VineSea { Def = d, Pos = at, Key = "vine_sea" }, 1);
                    BladeSkills.MageArtCast(d, at);
                    Sound("Item_8", 0.7f, 0.6f);
                    return true;
                });

            // ---- Razorblade Typhoon: one great typhoon.
            Art(ItemType.RazorbladeTyphoon).Press("刃之风暴", 7f, 3f, "台风在鼠标处汇成一股大台风 3 秒，把 8 格内的小敌人吸进来不停切割（每 1/6 秒 0.8 倍），最后甩出六股台风追向不同的敌人（各 0.8 倍）",
                "the typhoons gather into one great typhoon on the cursor for 3 s, drawing small enemies within 8 blocks in and cutting them on and on (0.8× every sixth of a second), then flinging six typhoons at different enemies (0.8× each)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    MageMech.Add(new BladeStorm { Def = d, Pos = at, Key = "blade_storm", Damage = MageMech.DamageOf(d, 0.8f) }, 1);
                    BladeSkills.MageArtCast(d, at, 1.2f);
                    Sound("Item_43", 0.6f, 0.6f);
                    return true;
                });

            // ---- Shadowbeam Staff: a shadow prism.
            Art(ItemType.ShadowbeamStaff).Press("暗影棱镜", 7f, 3f, "鼠标处立起暗影棱镜 5 秒：立起时向 20 格内的每个敌人射出光束（各 2 倍），此后每 1/3 秒再射一轮（各 1 倍）；你的光束穿过棱镜时分成射向每个敌人的光束",
                "a shadow prism stands on the cursor for 5 s: it fires a beam at every enemy within 20 blocks as it rises (2× each) and again every third of a second (1× each); your beams passing through it split at every enemy",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    var prism = new ShadowPrism { Def = d, Pos = at, Key = "shadow_prism" };
                    MageMech.Add(prism, 1);
                    prism.Refract(2f);
                    BladeSkills.MageArtCast(d, at);
                    Sound("Item_72", 0.7f, 0.7f);
                    return true;
                });

            // ---- Lunar Flare: the moon falls.
            Art(ItemType.LunarFlareBook).Press("月陨", 8f, 3f, "一轮月亮从天而降砸在鼠标处：落点 4 格内的敌人受到 20 倍伤害，14 格内的其他敌人被冲击波打到（10 倍）；场上每片月光同时炸开（各 3 倍），砸出的月坑发光 3 秒（每 0.25 秒 2 倍）",
                "a moon falls on the cursor: enemies within 4 blocks of where it lands take 20×, the others within 14 blocks are struck by its shockwave (10×), every pool of moonlight bursts at once (3× each), and the crater glows for 3 s (2× every quarter second)",
                (h, d) =>
                {
                    var at = Ground(BladeSkills.MageAim() + Vector2.up * B, 10f) + Vector2.up * 0.5f * B;
                    MageMech.Add(new MoonFall { Def = d, Target = at, Pos = at + new Vector2(-6f, 18f) * B, Key = "moon_fall" }, 1);
                    BladeSkills.MageArtCast(d, at, 1.3f);
                    Sound("Item_88", 0.7f, 0.6f);
                    return true;
                });

            // ---- Last Prism: a string of hallowed stars on every enemy.
            Art(ItemType.LastPrism).Press("七彩星陨", 8f, 3f, "30 格内的每一个敌人头顶各落下一串七彩神圣之星（每串十二颗，各 7 倍）",
                "a string of hallowed stars of the seven colours falls on every enemy within 30 blocks (twelve each, 7× each)",
                (h, d) =>
                {
                    var c = HeroCentre(h);
                    int k = 0;
                    foreach (var e in Near(c, 30f, false))
                    {
                        var top = Centre(e);
                        for (int i = 0; i < 12; i++)
                            MageMech.Add(new Comet { Def = d, Target = e, Pos = top + new Vector2((i % 7 - 3) * 1.2f, 16f + i * 0.5f) * B, Delay = i * 5 + k * 3, Texture = "Projectile_92", DustType = 58,
                                Tint = Spectrum[i % 7], Key = "prism_star", Damage = MageMech.DamageOf(d, 7f) }, 240);
                        k++;
                    }
                    BladeSkills.MageArtCast(d, null, 1.4f);
                    Sound("Item_9", 0.7f, 0.7f);
                    return true;
                });

            // ---- Spectre Staff: every wisp at its own enemy.
            Art(ItemType.SpectreStaff).Press("幽魂军团", 6f, 3f, "所有环绕你的幽魂，再加六只新的，一齐冲向各自锁定的敌人（各 3 倍），命中时各为你回 1 点生命",
                "every wisp circling you, and six new ones, charges at an enemy of its own (3× each), each healing you 1 as it strikes",
                (h, d) =>
                {
                    var c = HeroCentre(h);
                    var near = Near(c, 24f);
                    var wisps = new List<OrbitWisp>();
                    foreach (var t in MageMech.Things("orbit_wisp")) if (t is OrbitWisp w) wisps.Add(w);
                    for (int i = 0; i < 6; i++)
                    {
                        var w = new OrbitWisp { Def = d, Pos = c + Random.insideUnitCircle * B, Key = "orbit_wisp", Slot = i, Damage = MageMech.DamageOf(d, 0.4f) };
                        MageMech.Add(w, 9);
                        wisps.Add(w);
                    }
                    for (int i = 0; i < wisps.Count; i++)
                        if (near.Count > 0) wisps[i].Charge(near[i % near.Count]);
                        else wisps[i].Charge(null);
                    BladeSkills.MageArtCast(d, null);
                    Sound("Item_43", 0.6f, 1.1f);
                    return true;
                });

            // ---- Inferno Fork: a wall of hellfire pillars.
            Art(ItemType.InfernoFork).Press("地狱火柱", 7f, 3f, "鼠标两侧依次升起九道狱火柱，连成一道 16 格宽的火墙 3 秒，碰到的敌人每 0.25 秒受伤（1.5 倍）并着狱火",
                "nine pillars of hellfire rise one after another out to either side of the cursor, a wall 16 blocks wide for 3 s that hurts what touches it every quarter second (1.5×) with hellfire",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    var tp = TerraProjectiles.Instance;
                    for (int i = 0; i <= 4; i++)
                        for (int side = -1; side <= 1; side += 2)
                        {
                            if (i == 0 && side > 0) continue;
                            var p = Ground(at + new Vector2(side * i * 2f, 2f) * B, 12f) + Vector2.up * 3f * B;
                            tp?.Later(i * 5, (f, v) => MageMech.Add(new HellPillar { Def = d, Pos = p, Life = 180, HitEvery = 15, Size = new Vector2(1.6f, 6f) * B, Key = "hell_wall", Damage = MageMech.DamageOf(d, 1.5f) }, 9));
                        }
                    BladeSkills.MageArtCast(d, at, 1.2f);
                    Sound("Item_74", 0.7f, 0.7f);
                    return true;
                });

            // ---- Nebula Blaze: a nebula bursting on the cursor.
            Art(ItemType.NebulaBlaze).Press("星云爆发", 7f, 3f, "星云火在鼠标处炸开一片星云：14 格内的每个敌人受伤（6 倍）并叠满星云印记，星云停留 3 秒，每 0.25 秒灼烧带印记的敌人（1.2 倍）",
                "a nebula bursts on the cursor: every enemy within 14 blocks is hurt (6×) and takes the full nebula mark, and the nebula stays 3 s, scorching the marked every quarter second (1.2×)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    foreach (var e in Near(at, 14f, false))
                    {
                        MageMech.Strike(d, e, MageMech.DamageOf(d, 6f));
                        MageMech.AddMark(e, "nebula", 5, 5, 6f, NebulaPink, 86);
                        DustLine(at, Centre(e), 86, 0.8f, 1.2f, 0.2f);
                    }
                    MageMech.Add(new NebulaCloud { Def = d, Pos = at, Key = "nebula_cloud" }, 1);
                    MageMech.Ring(at, 0.4f, 86, 40, 9f, 1.8f);
                    MageMech.Ring(at, 0.3f, 90, 24, 5f, 1.4f);
                    BladeSkills.MageArtCast(d, at, 1.2f);
                    Sound("Item_117", 0.7f, 0.8f);
                    return true;
                });

            // ---- Staff of Earth: the ground splits.
            Art(ItemType.StaffofEarth).Press("地裂", 7f, 3f, "地面从你脚下向前裂开 20 格，裂缝里一路喷出岩石，把经过的敌人掀起（每个最多两次，各 2.8 倍），裂缝尽头再滚出三块巨石",
                "the ground splits 20 blocks ahead from your feet, rock bursting up all along it and throwing up what it passes (twice an enemy at most, 2.8× each); three boulders roll out where it ends",
                (h, d) =>
                {
                    float face = BladeSkills.MageTurn(h);
                    MageMech.Add(new Fissure { Def = d, Pos = Ground(HeroCentre(h), 4f) + Vector2.up * 0.2f, Dir = face, Key = "fissure" }, 1);
                    BladeSkills.MageArtCast(d, null);
                    Sound("Item_69", 0.8f, 0.7f);
                    return true;
                });

            // ---- Unholy Trident: a triangle of tridents.
            Art(ItemType.UnholyTrident).Press("邪恶三叉", 7f, 3f, "三支三叉戟插在鼠标周围，围成一个三角领域 4 秒，领域内的敌人每 1/3 秒受伤（0.7 倍）并着暗影焰；结束时三叉戟飞回你手里，切过沿路的敌人（1 倍）",
                "three tridents plunge into the ground round the cursor, a triangle for 4 s whose enemies are hurt every third of a second (0.7×) with shadowflame; then they fly back to your hand, cutting what is in the way (1×)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    MageMech.Add(new TridentField { Def = d, Pos = at, Key = "trident_field" }, 1);
                    BladeSkills.MageArtCast(d, at);
                    Sound("Item_72", 0.6f, 0.6f);
                    return true;
                });

            // ---- Toxic Flask: a great cloud spreading.
            Art(ItemType.ToxicFlask).Press("毒云弥漫", 7f, 3f, "毒气瓶在鼠标处碎成一大片毒云，4 秒内从 3 格扩散到 10 格，云里的敌人中毒、染上剧毒，每 1/6 秒受伤（1.4 倍）",
                "the flask breaks into a great cloud on the cursor, spreading from 3 to 10 blocks over 4 s; what is in it is poisoned and envenomed and hurt every sixth of a second (1.4×)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    MageMech.Add(new GreatCloud { Def = d, Pos = at, Key = "great_cloud" }, 1);
                    BladeSkills.MageArtCast(d, at);
                    Sound("Item_107", 0.7f, 0.8f);
                    return true;
                });

            // ---- Resonance Scepter: a field where every enemy rings with the others.
            Art(ItemType.PrincessWeapon).Press("共鸣之环", 7f, 3f, "身边展开 10 格的共鸣场 4 秒，每 1/3 秒场内的每个敌人受伤（1.6 倍），场内敌人越多每个受得越重（每多一个 +50%，最多 +150%），结束时一齐回响（5 倍）",
                "a field of resonance 10 blocks round you for 4 s: every third of a second each enemy in it is hurt (1.6×), the more there are the harder each is struck (+50% for each other, +150% at most), and all ring out together at the end (5×)",
                (h, d) =>
                {
                    MageMech.Add(new ResonanceField { Def = d, Pos = HeroCentre(h), Key = "resonance_field" }, 1);
                    BladeSkills.MageArtCast(d, null, 1.2f);
                    Sound("Item_26", 0.6f, 0.6f);
                    return true;
                });

            // ---- Arc Surge: a thunderbolt down the wires.
            Art(ItemType.ArcSurge).Press("雷霆万钧", 7f, 3f, "鼠标处劈下一道巨大的雷柱（4 格内 5 倍），再沿电线传给所有连过的敌人（各 4 倍）；没有电线时跳向最近的四个敌人（各 2.5 倍）",
                "a great thunderbolt comes down on the cursor (5× within 4 blocks) and runs down the wires to every enemy linked (4× each); with no wires it leaps to the four enemies nearest (2.5× each)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    foreach (var e in Near(at, 4f, false)) MageMech.Strike(d, e, MageMech.DamageOf(d, 5f));
                    var sky = at + Vector2.up * 18f * B;
                    for (int i = 0; i < 3; i++) Arc(sky + new Vector2(Random.Range(-1f, 1f), 0f) * B, at, d);
                    DustLine(sky, at, 226, 0.5f, 1.8f, 0.5f);
                    Burst(at, 226, 30, 6f, 1.6f);
                    TerraProjectiles.Instance?.Later(10, (f, v) =>
                    {
                        var wired = new List<HealthManager>();
                        foreach (var (e, n) in MageMech.Marked("wired")) { wired.Add(e); MageMech.ClearMark(e, "wired"); }
                        foreach (var t in MageMech.Things("arc_wire"))
                            if (t is WireLink w)
                            {
                                if (w.A != null && !wired.Contains(w.A)) wired.Add(w.A);
                                if (w.Z != null && !wired.Contains(w.Z)) wired.Add(w.Z);
                                w.Done = true;
                            }
                        float k = 4f;
                        if (wired.Count == 0)
                        {
                            k = 2.5f;
                            foreach (var e in Near(at, 20f)) { wired.Add(e); if (wired.Count >= 4) break; }
                        }
                        foreach (var e in wired)
                        {
                            if (e == null || e.isDead) continue;
                            MageMech.Strike(d, e, MageMech.DamageOf(d, k));
                            Arc(at, Centre(e), d);
                            Burst(Centre(e), 226, 12, 4f, 1.4f);
                        }
                    });
                    BladeSkills.MageArtCast(d, at, 1.3f);
                    Sound("Item_122", 0.8f, 0.6f);
                    return true;
                });
        }

        // ================================================================== things (stage 5)

        /// <summary>An icicle stuck in the ground: pricking and frostburning what goes over it.</summary>
        sealed class IceSpike : MageThing
        {
            readonly int look = Random.Range(0, 5);
            public IceSpike() { Life = 120; Hurts = true; HitEvery = 30; Size = new Vector2(0.9f, 1.2f) * MageMech.Block; }
            public override void Hit(HealthManager hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Frostburn, 180);
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float fade = Mathf.Clamp01((Life - Age) / 20f);
                Anim(fx, "Projectile_337", 5, look, px, new Color(1f, 1f, 1f, fade), Mathf.PI, 0.9f);
                if (Age % 20 == 0) MageMech.Dust(Pos, 76, Vector2.up * 0.5f, 0.9f);
            }
        }

        /// <summary>A blizzard: every enemy in it slowed and chilled, icicles falling on each in turn.</summary>
        sealed class Blizzard : MageThing
        {
            int k;
            public Blizzard() { Life = 180; }
            public override void Tick()
            {
                var near = Near(Pos, 9f, false);
                if (Age % 10 == 0) foreach (var e in near) { MageMech.Slow(e, 0.5f, 0.4f); EnemyDebuffs.Add(e, EnemyDebuffs.Frostburn, 120); }
                if (Age % 30 == 0) foreach (var e in near) MageMech.Strike(Def, e, MageMech.DamageOf(Def, 0.4f));
                if (Age % 6 != 0) return;
                var to = near.Count > 0 ? Centre(near[k++ % near.Count]) : Pos + new Vector2(Random.Range(-8f, 8f), -2f) * MageMech.Block;
                var from = to + new Vector2(Random.Range(-3f, 3f), 6f) * MageMech.Block;
                if (Physics2D.OverlapPoint(from, TerrainMask) != null) from = to + Vector2.up * 1.5f * MageMech.Block;
                Bolt(Def, ProjKind.Icicle, from, At(from, to, 12f), 0.5f, new RangedShot { Debuff = EnemyDebuffs.Frostburn, DebuffTicks = 180 });
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 4; i++)
                    MageMech.Dust(Pos + new Vector2(Random.Range(-9f, 9f), Random.Range(-4f, 5f)) * MageMech.Block, 76, new Vector2(-4f + Random.Range(-1f, 1f), -1.5f), Random.Range(1f, 1.6f));
                FxBatch.Instance?.Light(Pos, Snow * 0.4f, 6f);
            }
        }

        /// <summary>The Razorpine's storm: needles swept all round the player, then every needle in an enemy bursts.</summary>
        sealed class PineSpin : MageThing
        {
            public PineSpin() { Life = 180; }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) { Done = true; return; }
                Pos = HeroCentre(hero);
                if (Age % 3 != 0) return;
                var def = Def;
                var near = Near(Pos, 20f);
                for (int arm = 0; arm < 2; arm++)
                {
                    float a = Age * 0.12f + arm * Mathf.PI;
                    var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var from = Pos + way * 1.5f * MageMech.Block;
                    // Flung off the spinning ring, then turning onto an enemy near.
                    var vel = near.Count > 0 ? At(from, Centre(near[(Age / 3 + arm) % near.Count]), 7f) + way * 3f : way * 10f;
                    Bolt(def, ProjKind.PineNeedle, from, vel, 0.6f, new RangedShot { Home = 6f, OnStrike = (hm, at) => PineHit(def, hm) });
                }
            }
            public override void End()
            {
                foreach (var (e, n) in MageMech.Marked("pine")) PineBurst(Def, e, 1.6f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 3; i++)
                {
                    float a = Age * 0.12f + i * Mathf.PI * 2f / 3f;
                    MageMech.Dust(Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.5f * MageMech.Block, 3, Vector2.zero, 1.2f);
                }
            }
        }

        /// <summary>The wires between the Magnet Spheres in flight: what touches one is shocked.</summary>
        sealed class MagnetWeb : MageThing
        {
            int idle;
            public MagnetWeb() { Life = 36000; }
            public override void Tick()
            {
                var live = LiveSpheres();
                if (live.Count == 0) { if (++idle > 30) Done = true; return; }
                idle = 0;
                Pos = live[0];
                if (live.Count < 2) return;
                bool draw = Age % 3 == 0, strike = Age % 15 == 0;
                var hit = strike ? new HashSet<HealthManager>() : null;
                for (int i = 0; i < live.Count; i++)
                    for (int j = i + 1; j < live.Count; j++)
                    {
                        if (draw) DustLine(live[i], live[j], 226, 0.9f, 0.9f, 0.15f);
                        if (strike) foreach (var e in Along(live[i], live[j], 1.2f)) hit.Add(e);
                    }
                if (hit == null) return;
                foreach (var e in hit)
                {
                    MageMech.Strike(Def, e, Damage);
                    Burst(Centre(e), 226, 6, 2.5f);
                }
            }
        }

        /// <summary>The Magnet Sphere's art: every sphere (or the player) discharging at every enemy, then the spheres burst.</summary>
        sealed class MagnetStorm : MageThing
        {
            readonly List<Vector2> own = new List<Vector2>();
            public MagnetStorm() { Life = 80; }
            /// <summary>The spheres out, and spheres of its own round the player to make three.</summary>
            List<Vector2> Sources()
            {
                var src = LiveSpheres();
                own.Clear();
                var hero = HeroController.SilentInstance;
                for (int i = src.Count; i < 3 && hero != null; i++)
                {
                    float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 3f + Age * 0.05f;
                    own.Add(HeroCentre(hero) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.5f * MageMech.Block);
                }
                src.AddRange(own);
                return src;
            }
            public override void Tick()
            {
                var sources = Sources();
                if (Age % 15 != 1) return;
                foreach (var src in sources)
                {
                    foreach (var e in Near(src, 20f))
                    {
                        MageMech.Strike(Def, e, MageMech.DamageOf(Def, 1.4f));
                        Arc(src, Centre(e), Def);
                    }
                    Burst(src, 226, 10, 3f, 1.3f);
                }
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                foreach (var at in own) Anim(fx, "Projectile_254", 5, Age / 7, TerraParticles.ToPx(at), Color.white, Age * 0.2f, 1f);
            }
            public override void End()
            {
                foreach (var x in spheres) { Burst(x.Pos, 226, 20, 5f, 1.5f); x.Gone = true; }
                spheres.Clear();
            }
        }

        /// <summary>A bubble wrapped round an enemy: it floats (not bosses or elites), then pops.</summary>
        sealed class Bubbled : MageThing
        {
            HealthManager on;
            float k, size = 1f;
            public static void On(ItemDef def, HealthManager hm, float seconds, float k, bool renew)
            {
                if (hm == null || hm.isDead) return;
                int ticks = Mathf.RoundToInt(seconds * 60f);
                foreach (var t in MageMech.Things("bubbled"))
                    if (t is Bubbled b && b.on == hm)
                    {
                        if (!renew) return;
                        b.Life = Mathf.Max(b.Life, b.Age + ticks);
                        b.k = Mathf.Max(b.k, k);
                        return;
                    }
                var col = hm.GetComponent<Collider2D>();
                float s = col != null ? Mathf.Max(col.bounds.size.x, col.bounds.size.y) : 1f;
                MageMech.Add(new Bubbled { Def = def, on = hm, k = k, size = s, Life = ticks, Pos = Centre(hm), Key = "bubbled" }, 24);
            }
            public override void Tick()
            {
                if (on == null || on.isDead) { Done = true; return; }
                Pos = Centre(on);
                MageMech.Pull(on, Pos + Vector2.up, 1.2f);
                MageMech.Slow(on, 0.3f, 0.1f);
                if (Age % 8 == 0) MageMech.Dust(Pos + Random.insideUnitCircle * size * 0.5f, 33, Vector2.up * 0.8f, 0.9f);
            }
            public override void End()
            {
                if (on == null || on.isDead) return;
                MageMech.Strike(Def, on, MageMech.DamageOf(Def, k));
                Burst(Pos, 33, 14, 3f, 1.3f);
                Sound("Item_54", 0.35f, 1.2f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float wobble = 1f + Mathf.Sin(Age * 0.3f) * 0.05f;
                Sprite(fx, "Projectile_410", px, new Color(1f, 1f, 1f, 0.55f), 0f, Mathf.Clamp(size / (16f * TerraParticles.Px) * 1.1f, 1.5f, 8f) * wobble);
            }
        }

        /// <summary>The Bubble Gun's art: every enemy in it wrapped, bubbles rising into them, all popping at the end.</summary>
        sealed class BubbleSea : MageThing
        {
            int k;
            public BubbleSea() { Life = 120; }
            public override void Tick()
            {
                var near = Near(Pos, 9f, false);
                if (Age % 10 == 1) foreach (var e in near) Bubbled.On(Def, e, (Life - Age) / 60f + 0.05f, 8f, true);
                if (Age % 8 != 0) return;
                foreach (var e in near)
                {
                    var to = Centre(e);
                    MageMech.Strike(Def, e, MageMech.DamageOf(Def, 1.5f));
                    for (int i = 0; i < 4; i++) MageMech.Dust(to + new Vector2(Random.Range(-1f, 1f), -1.5f - i * 0.4f) * MageMech.Block, 33, Vector2.up * 3f, 1.3f);
                }
                if (++k % 3 == 0) Sound("Item_85", 0.3f, 1.3f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 4; i++)
                    MageMech.Dust(Pos + new Vector2(Random.Range(-9f, 9f), Random.Range(-4f, 2f)) * MageMech.Block, 33, Vector2.up * Random.Range(1f, 3f), Random.Range(1f, 1.6f));
                FxBatch.Instance?.Light(Pos, new Color(0.4f, 0.7f, 1f) * 0.4f, 6f);
            }
        }

        /// <summary>A rainbow arch on the ground: the enemies under it take a colour at random every half second.</summary>
        sealed class RainbowArch : MageThing
        {
            /// <summary>Its half width (blocks), and a blow of its own with each colour (× the weapon's damage).</summary>
            public float Span = 4.5f, K;
            public RainbowArch() { Life = 180; }
            public override void Tick()
            {
                if (Age % 30 != 0) return;
                foreach (var e in Near(Pos + Vector2.up * 2.5f * MageMech.Block, Span + 0.5f, false))
                {
                    if (Mathf.Abs(Centre(e).x - Pos.x) > Span * MageMech.Block) continue;
                    Colour(Def, e, Random.Range(0, 6), 0.3f);
                    if (K > 0f) MageMech.Strike(Def, e, MageMech.DamageOf(Def, K));
                }
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 6; i++)
                {
                    float a = Random.Range(0f, Mathf.PI);
                    float r = (Span - 0.5f - i * 0.25f) * MageMech.Block;
                    MageMech.Dust(Pos + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r), SpectrumDust[i], Vector2.zero, 1.1f);
                }
            }
        }

        /// <summary>A rainbow arcing from one side to the cursor; the one from the right bursts there in every colour.</summary>
        sealed class RainbowBeam : MageThing
        {
            public Vector2 From, To;
            public bool Crosses;
            public RainbowBeam() { Life = 40; Hurts = true; HitEvery = 40; Size = new Vector2(1.4f, 1.4f) * MageMech.Block; }
            public override void Tick()
            {
                float t = Mathf.Clamp01(Age / 40f);
                var next = Vector2.Lerp(From, To, t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * 6f * MageMech.Block;
                Vel = next - Pos;
                for (int i = 0; i < 6; i++) MageMech.Dust(Pos + Vector2.up * (i - 2.5f) * 0.25f * MageMech.Block, SpectrumDust[i], Vector2.zero, 1.3f);
            }
            public override void End()
            {
                if (!Crosses) return;
                foreach (var e in Near(To, 7f, false))
                {
                    for (int c = 0; c < 6; c++) if (c != 1) Colour(Def, e, c, 0f);
                    MageMech.Strike(Def, e, MageMech.DamageOf(Def, 8f));
                }
                MageMech.Add(new RainbowArch { Def = Def, Pos = Ground(To + Vector2.up * MageMech.Block, 8f), Key = "rainbow_arch_great", Span = 6f, K = 0.8f }, 1);
                for (int i = 0; i < 6; i++) MageMech.Ring(To, 0.3f + i * 0.1f, SpectrumDust[i], 16, 4f + i * 1.2f, 1.5f);
                Sound("Item_14", 0.5f, 1.3f);
            }
        }

        /// <summary>The Laser Machinegun's fan: lasers swept there and back; each enemy struck burns once more for each strike.</summary>
        sealed class LaserFan : MageThing
        {
            Vector2 aim;
            public LaserFan() { Life = 60; }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) { Done = true; return; }
                Pos = HeroCentre(hero);
                if (Age == 1) aim = (BladeSkills.MageAim() - Pos).sqrMagnitude > 1e-6f ? (BladeSkills.MageAim() - Pos).normalized : Vector2.right * Facing(hero);
                // To and fro, three times, the fan narrowing onto the aim.
                float t = Age / 60f;
                float angle = 50f * (1f - t) * Mathf.Sin(t * Mathf.PI * 3f);
                var def = Def;
                Bolt(def, ProjKind.MachinegunLaser, Pos, Rotate(aim, angle) * 16f, 0.9f,
                    new RangedShot { Pierce = 3, Trail = 0.5f, OnStrike = (hm, at) => MageMech.AddMark(hm, "fan", 1, 30, 3f, new Color(0.4f, 0.8f, 1f), 226) });
            }
            public override void End()
            {
                var def = Def;
                TerraProjectiles.Instance?.Later(20, (f, v) =>
                {
                    foreach (var (e, n) in MageMech.Marked("fan"))
                    {
                        MageMech.ClearMark(e, "fan");
                        MageMech.Strike(def, e, MageMech.DamageOf(def, 0.8f * n));
                        Burst(Centre(e), 226, 10 + n, 3.5f, 1.3f);
                    }
                });
            }
        }

        /// <summary>A bat roosting behind the player until there is an enemy to swoop at.</summary>
        sealed class RoostBat : MageThing
        {
            public int Slot;
            public RoostBat() { Life = 600; }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) { Done = true; return; }
                float face = Facing(hero);
                var home = HeroCentre(hero) + new Vector2(-face * (1.2f + Slot % 4 * 0.7f), 1.3f + Slot / 4 * 0.7f + Mathf.Sin((Age + Slot * 7) * 0.15f) * 0.2f) * MageMech.Block;
                Pos = Vector2.Lerp(Pos, home, 0.25f);
                Vel = Vector2.zero;
                if (Age < 8) return;
                var near = Near(HeroCentre(hero), 14f);
                if (near.Count > 0) Go(Centre(near[Slot % near.Count]));
                else if (Age > 60) Go(BladeSkills.MageAim());
            }
            public void Go(Vector2 to)
            {
                if (Done) return;
                Bolt(Def, ProjKind.Bat, Pos, At(Pos, to, 9f), 1.9f);
                Done = true;
            }
            public override void Draw(FxBatch fx, Vector2 px) => Anim(fx, "Projectile_316", 4, Age / 5, px, Color.white, 0f, 1f);
        }

        /// <summary>The Bat Scepter's art: bats circling the cursor, biting what is inside, the bites healing the player.</summary>
        sealed class BatStorm : MageThing
        {
            int healed;
            public BatStorm() { Life = 180; }
            public override void Tick()
            {
                Vel = Vector2.ClampMagnitude(BladeSkills.MageAim() - Pos, 0.15f * MageMech.Block);
                if (Age % 10 != 0) return;
                foreach (var e in Near(Pos, 3.5f, false))
                {
                    MageMech.Strike(Def, e, MageMech.DamageOf(Def, 1.5f));
                    Burst(Centre(e), 5, 6, 2.5f);
                    if (healed < 12) { healed++; TerraMod.Instance?.Stats.Heal(1); }
                }
            }
            public override void End()
            {
                var near = Near(Pos, 16f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    var to = near.Count > 0 ? Centre(near[i % near.Count]) : Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f * MageMech.Block;
                    Bolt(Def, ProjKind.Bat, Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2f * MageMech.Block, At(Pos, to, 9f), 1f);
                }
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = Age * 0.15f + i * Mathf.PI / 4f;
                    float r = (2f + Mathf.Sin(Age * 0.1f + i) * 0.6f) * MageMech.Block;
                    var at = Pos + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.7f);
                    Anim(fx, "Projectile_316", 4, Age / 4 + i, TerraParticles.ToPx(at), Color.white, 0f, 1f);
                }
                if (Age % 3 == 0) MageMech.Dust(Pos + Random.insideUnitCircle * 2.5f * MageMech.Block, 54, Vector2.zero, 1.2f, 100);
            }
        }

        /// <summary>A wasps' nest letting out wasps, bursting into a swarm when crowded or at its end.</summary>
        sealed class WaspNest : MageThing
        {
            bool swarmed;
            public WaspNest() { Life = 240; }
            public override void Tick()
            {
                if (Age % 12 == 0)
                {
                    var e = Nearest(Pos, 16f);
                    var to = e != null ? Centre(e) : Pos + Random.insideUnitCircle * 4f * MageMech.Block;
                    Bolt(Def, ProjKind.Wasp, Pos + Vector2.up * 0.5f * MageMech.Block, At(Pos, to, 6f), 0.8f, WaspShot(Def));
                }
                if (Age % 20 == 0 && Near(Pos, 6f, false).Count >= 3) { Swarm(); Done = true; }
            }
            public override void End() => Swarm();
            void Swarm()
            {
                if (swarmed) return;
                swarmed = true;
                var near = Near(Pos, 16f);
                for (int i = 0; i < 10; i++)
                {
                    var to = near.Count > 0 ? Centre(near[i % near.Count]) : Pos + Random.insideUnitCircle * 4f * MageMech.Block;
                    Bolt(Def, ProjKind.Wasp, Pos + Random.insideUnitCircle * 0.5f * MageMech.Block, At(Pos, to, 6f + i * 0.3f), 0.8f, WaspShot(Def));
                }
                Burst(Pos, 152, 20, 4f, 1.4f);
                Sound("Item_97", 0.6f, 1.2f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float grow = Mathf.Clamp01(Age / 15f);
                Sprite(fx, "Item_1124", px, Color.white, Mathf.Sin(Age * 0.2f) * 0.05f, 2f * grow);
                if (Age % 6 == 0) MageMech.Dust(Pos + Random.insideUnitCircle * 1.5f * MageMech.Block, 152, Vector2.down * 0.3f, 1.1f);
            }
        }

        /// <summary>A little whirlwind of leaves: small enemies drawn in, what is inside cut.</summary>
        sealed class LeafWhirl : MageThing
        {
            public int Leaves;
            public LeafWhirl() { Life = 120; Hurts = true; HitEvery = 20; Size = new Vector2(3f, 3f) * MageMech.Block; }
            public override void Tick()
            {
                foreach (var e in Near(Pos, 4f, false)) MageMech.Pull(e, Pos, 3f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 2; i++)
                {
                    float a = Age * 0.3f + i * Mathf.PI;
                    float r = (1f + Mathf.Sin(Age * 0.05f) * 0.3f) * MageMech.Block;
                    MageMech.Dust(Pos + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.6f + Random.Range(-1f, 1f) * MageMech.Block), 3, new Vector2(-Mathf.Sin(a), 0.3f) * 2f, 1.1f);
                }
            }
        }

        /// <summary>The Leaf Blower's art: a great whirlwind driving ahead, carrying and cutting.</summary>
        sealed class LeafStorm : MageThing
        {
            public float Dir = 1f;
            public LeafStorm() { Life = 150; Hurts = true; HitEvery = 8; Size = new Vector2(4f, 6f) * MageMech.Block; }
            public override void Tick()
            {
                // Slow, slower still where there are enemies to grind; stopped by rock.
                bool grinding = Near(Pos, 2.5f, false).Count > 0;
                Vel = new Vector2(Dir * (grinding ? 0.03f : 0.12f) * MageMech.Block, 0f);
                foreach (var e in Near(Pos, 6f, false)) MageMech.Pull(e, Pos + Vel * 10f, 7f);
                if (Physics2D.OverlapPoint(Pos + new Vector2(Dir * 1.5f * MageMech.Block, 0f), TerrainMask) != null) Vel = Vector2.zero;
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 5; i++)
                {
                    float h = Random.Range(-3f, 3f), a = Age * 0.35f + i * Mathf.PI * 0.4f, r = (1.6f + (h + 3f) * 0.25f) * MageMech.Block;
                    MageMech.Dust(Pos + new Vector2(Mathf.Cos(a) * r, h * MageMech.Block), i % 2 == 0 ? 3 : 40, new Vector2(-Mathf.Sin(a), 0.2f) * 3f, 1.4f);
                }
            }
        }

        /// <summary>A Nightglow light hanging behind the player: it goes when the button is let go, stronger the longer it waited.</summary>
        sealed class GlowStar : MageThing
        {
            public int Slot;
            public float Wait = 0.5f;
            public GlowStar() { Life = 600; }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) { Done = true; return; }
                float face = Facing(hero);
                float a = (Slot - 3.5f) * 0.3f;
                var home = HeroCentre(hero) + new Vector2(-face * (1.6f + Mathf.Cos(a) * 0.6f), 1.8f + Mathf.Sin(a) * 1.6f) * MageMech.Block;
                Pos = Vector2.Lerp(Pos, home, 0.2f);
                Vel = Vector2.zero;
                if (Age % 4 == 0) MageMech.Dust(Pos, 15, Vector2.zero, 0.8f + Mathf.Min(1f, Age / 90f) * 0.6f);
                if (Age >= 90 || Age > 6 && Time.time - nightglowAt > Wait) Go();
            }
            public void Go(float k = 0f)
            {
                if (Done) return;
                Done = true;
                if (k <= 0f) k = 0.45f + 0.35f * Mathf.Min(1f, Age / 90f);
                var near = Near(BladeSkills.MageAim(), 14f, false);
                var to = near.Count > 0 ? Centre(near[Slot % near.Count]) : BladeSkills.MageAim();
                Bolt(Def, ProjKind.NightglowStar, Pos, At(Pos, to, 11f), k, new RangedShot { Home = 4f, Trail = 0.6f });
            }
            public override void Draw(FxBatch fx, Vector2 px) => Sprite(fx, "Projectile_931", px, Color.white, Age * 0.05f, 0.8f + Mathf.Min(1f, Age / 90f) * 0.5f);
        }

        /// <summary>The Nightglow's art: ten lights round the player, flying one by one at different enemies.</summary>
        sealed class NightRing : MageThing
        {
            int sent;
            public NightRing() { Life = 110; }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) { Done = true; return; }
                Pos = HeroCentre(hero);
                if (Age < 40 || Age % 6 != 0 || sent >= 10) return;
                float a = Age * 0.15f + sent * Mathf.PI / 5f;
                var from = Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.5f * MageMech.Block;
                var near = Near(Pos, 24f);
                var to = near.Count > 0 ? Centre(near[sent % near.Count]) : BladeSkills.MageAim();
                Bolt(Def, ProjKind.NightglowStar, from, At(from, to, 12f), 0.35f, new RangedShot { Home = 5f, Trail = 1f });
                sent++;
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = sent; i < 10; i++)
                {
                    float a = Age * 0.15f + i * Mathf.PI / 5f;
                    var at = Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.5f * MageMech.Block;
                    Sprite(fx, "Projectile_931", TerraParticles.ToPx(at), Spectrum[i % Spectrum.Length], a, 1f);
                }
            }
        }

        /// <summary>The Sparkle Guitar's solo: on each beat stars fall on every enemy near.</summary>
        sealed class Solo : MageThing
        {
            int beat;
            public Solo() { Life = 180; }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) { Done = true; return; }
                Pos = HeroCentre(hero);
                if (Age % 15 != 0) return;
                beat++;
                bool strong = beat % 4 == 0;
                foreach (var e in Near(Pos, 18f))
                {
                    var c = Centre(e);
                    MageMech.Strike(Def, e, MageMech.DamageOf(Def, strong ? 1.8f : 0.9f));
                    for (int i = 0; i <= 5; i++) MageMech.Dust(c + new Vector2(0f, (5 - i) * 0.8f) * MageMech.Block, strong ? 57 : 15, Vector2.down * 2f, strong ? 1.5f : 1.1f);
                }
                if (strong) MageMech.Ring(Pos, 0.4f, 57, 24, 6f, 1.4f);
                Sound("Item_26", 0.4f, 0.8f + 0.1f * (beat % 4));
            }
        }

        /// <summary>Betsy's Wrath's art: a dragon's shape behind the player breathing a cone of fire; every dragon-marked enemy bursts.</summary>
        sealed class DragonBreath : MageThing
        {
            Vector2 dir = Vector2.right;
            public DragonBreath() { Life = 90; }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) { Done = true; return; }
                Pos = HeroCentre(hero);
                var way = BladeSkills.MageAim() - Pos;
                if (way.sqrMagnitude > 1e-6f) dir = way.normalized;
                if (Age % 2 == 0)
                    for (int i = 0; i < 5; i++)
                    {
                        var d = Rotate(dir, Random.Range(-25f, 25f));
                        MageMech.Dust(Pos + d * Random.Range(1f, 13f) * MageMech.Block, i % 2 == 0 ? 6 : 55, d * 5f, Random.Range(1.4f, 2.2f));
                    }
                if (Age % 8 == 0)
                    foreach (var e in Near(Pos, 14f))
                    {
                        if (Vector2.Angle(dir, Centre(e) - Pos) > 28f) continue;
                        MageMech.Strike(Def, e, MageMech.DamageOf(Def, 1.5f));
                        MageMech.AddMark(e, "dragon", 1, 5, 8f, Dragonfire, 6);
                        EnemyDebuffs.Add(e, EnemyDebuffs.OnFire, 240);
                    }
                if (Age != 82) return;
                foreach (var (e, n) in MageMech.Marked("dragon"))
                {
                    if (Vector2.Distance(Centre(e), Pos) > 30f * MageMech.Block) continue;
                    MageMech.ClearMark(e, "dragon");
                    MageMech.Strike(Def, e, MageMech.DamageOf(Def, 1.2f * n));
                    var c = Centre(e);
                    for (int i = 0; i < 8; i++) MageMech.Dust(c + new Vector2(Random.Range(-0.5f, 0.5f), i * 0.4f) * MageMech.Block, 6, Vector2.up * 3f, 2f);
                    Burst(c, 6, 20, 5f, 1.8f);
                }
                Sound("Item_45", 0.7f, 0.7f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                // The dragon: its wings spread over the player, its neck reaching the way it breathes.
                if (Age % 2 != 0) return;
                float side = dir.x >= 0f ? 1f : -1f;
                float flap = Mathf.Sin(Age * 0.2f) * 0.6f;
                for (int i = 0; i < 4; i++)
                {
                    float t = Random.value;
                    for (int w = -1; w <= 1; w += 2)
                    {
                        var wing = new Vector2(w * t * 5f, 2.5f + Mathf.Sin(t * Mathf.PI) * (2f + flap) - t * 1.5f);
                        MageMech.Dust(Pos + wing * MageMech.Block, 6, Vector2.zero, 1.3f, 60);
                    }
                    var neck = new Vector2(side * (0.5f + t * 2f), 1.5f + t * 1.5f);
                    MageMech.Dust(Pos + neck * MageMech.Block, 55, Vector2.zero, 1.4f, 40);
                }
                FxBatch.Instance?.Light(Pos, Dragonfire * 0.6f, 6f);
            }
        }

        /// <summary>A short pull of the enemies near a point to it (not bosses or elites).</summary>
        sealed class Well : MageThing
        {
            public float Radius = 4f, Speed = 6f;
            public int DustType = 27;
            public Well() { Life = 30; }
            public override void Tick()
            {
                foreach (var e in Near(Pos, Radius, false)) MageMech.Pull(e, Pos, Speed);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                var way = Random.insideUnitCircle.normalized;
                MageMech.Dust(Pos + way * Radius * 0.6f * MageMech.Block, DustType, -way * 4f, 1.1f);
            }
        }

        /// <summary>The Nebula Arcanum's art: a nebula's heart drawing every enemy in, hurting, then collapsing.</summary>
        sealed class NebulaHeart : MageThing
        {
            public NebulaHeart() { Life = 180; }
            public override void Tick()
            {
                var near = Near(Pos, 10f, false);
                foreach (var e in near) MageMech.Pull(e, Pos, 6f);
                if (Age % 10 == 0) foreach (var e in near) MageMech.Strike(Def, e, MageMech.DamageOf(Def, 2f));
            }
            public override void End()
            {
                foreach (var e in Near(Pos, 10f, false)) MageMech.Strike(Def, e, MageMech.DamageOf(Def, 8f));
                MageMech.Ring(Pos, 0.3f, 86, 36, 9f, 1.8f);
                MageMech.Ring(Pos, 0.2f, 90, 24, 5f, 1.5f);
                Sound("Item_14", 0.6f, 0.8f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 4; i++)
                {
                    float a = Age * 0.12f + i * Mathf.PI * 0.5f;
                    float r = Random.Range(1f, 9f) * MageMech.Block;
                    var at = Pos + new Vector2(Mathf.Cos(a + r), Mathf.Sin(a + r)) * r;
                    MageMech.Dust(at, i % 2 == 0 ? 86 : 90, (Pos - at).normalized * 3f + new Vector2(-(at - Pos).y, (at - Pos).x).normalized * 2f, Random.Range(1.2f, 1.8f));
                }
                Sprite(fx, "Projectile_617", px, new Color(1f, 1f, 1f, 0.8f), Age * 0.1f, 1.5f + Mathf.Sin(Age * 0.2f) * 0.2f);
                FxBatch.Instance?.Light(Pos, NebulaPink * 0.6f, 7f);
            }
        }

        /// <summary>The nebula left by the Nebula Blaze's art: the marked within it scorched.</summary>
        sealed class NebulaCloud : MageThing
        {
            public NebulaCloud() { Life = 180; }
            public override void Tick()
            {
                if (Age % 15 != 0) return;
                foreach (var (e, n) in MageMech.Marked("nebula"))
                    if (Vector2.Distance(Centre(e), Pos) < 16f * MageMech.Block) MageMech.Strike(Def, e, MageMech.DamageOf(Def, 1.2f));
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 3; i++)
                    MageMech.Dust(Pos + Random.insideUnitCircle * 8f * MageMech.Block, i % 2 == 0 ? 86 : 240, Random.insideUnitCircle * 0.5f, Random.Range(1.2f, 1.8f), 80);
            }
        }

        /// <summary>A strip of lava melted into the ground: what goes over it burns.</summary>
        sealed class LavaStrip : MageThing
        {
            public LavaStrip() { Life = 240; Hurts = true; HitEvery = 20; Size = new Vector2(1.3f, 1f) * MageMech.Block; }
            public override void Hit(HealthManager hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 240);
            public override void Draw(FxBatch fx, Vector2 px)
            {
                if (Age % 3 == 0) MageMech.Dust(Pos + new Vector2(Random.Range(-0.6f, 0.6f), -0.3f) * MageMech.Block, Age % 6 == 0 ? 35 : 6, Vector2.up * Random.Range(0.5f, 2f), 1.3f);
                if (Age % 10 == 0) FxBatch.Instance?.Light(Pos, Fire * 0.4f, 2f);
            }
        }

        /// <summary>Vines round an enemy: held (bosses and elites only slowed), squeezing as they let go.</summary>
        sealed class Root : MageThing
        {
            HealthManager on;
            float squeeze;
            public static void On(ItemDef def, HealthManager hm, float seconds, float squeeze)
            {
                if (hm == null || hm.isDead) return;
                MageMech.AddMark(hm, "rooted", 1, 1, seconds + 1f, PineGreen, 3);
                EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, 300);
                MageMech.Add(new Root { Def = def, on = hm, squeeze = squeeze, Life = Mathf.RoundToInt(seconds * 60f), Pos = Centre(hm), Key = "root" }, 24);
            }
            public bool Holds(HealthManager hm) => on == hm && !Done;
            public override void Tick()
            {
                if (on == null || on.isDead) { Done = true; return; }
                Pos = Centre(on);
                MageMech.Slow(on, 0.05f, 0.1f);
                if (Age % 5 == 0)
                {
                    float a = Age * 0.4f;
                    MageMech.Dust(Pos + new Vector2(Mathf.Cos(a) * 0.8f, Mathf.Sin(a * 0.5f) * 1f) * MageMech.Block, 3, Vector2.zero, 1.2f);
                }
            }
            public override void End()
            {
                if (on == null || on.isDead || squeeze <= 0f) return;
                MageMech.Strike(Def, on, MageMech.DamageOf(Def, squeeze));
                Burst(Pos, 3, 10, 3f);
            }
        }

        /// <summary>The Nettle Burst's art: vines out of the ground rooting every enemy, thorns pricking the held.</summary>
        sealed class VineSea : MageThing
        {
            readonly List<Vector2> stems = new List<Vector2>();
            public VineSea() { Life = 150; }
            public override void Tick()
            {
                if (Age == 1)
                {
                    for (int i = 0; i < 12; i++) stems.Add(Ground(Pos + new Vector2(Random.Range(-10f, 10f), 3f) * MageMech.Block, 12f));
                    foreach (var e in Near(Pos, 10f, false))
                    {
                        Root.On(Def, e, 2.5f, 4f);
                        MageMech.Strike(Def, e, MageMech.DamageOf(Def, 4f));
                        Burst(Centre(e), 3, 12, 3f, 1.3f);
                    }
                }
                if (Age % 10 != 0) return;
                foreach (var t in MageMech.Things("root"))
                    if (Vector2.Distance(t.Pos, Pos) < 12f * MageMech.Block)
                        foreach (var e in Near(t.Pos, 0.5f, false))
                            if (((Root)t).Holds(e)) MageMech.Strike(Def, e, MageMech.DamageOf(Def, 0.8f));
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float grow = Mathf.Clamp01(Age / 12f) * Mathf.Clamp01((Life - Age) / 20f);
                foreach (var s in stems)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        var at = s + Vector2.up * (k + 0.5f) * 0.9f * MageMech.Block * grow;
                        Sprite(fx, k == 2 ? "Projectile_152" : "Projectile_150", TerraParticles.ToPx(at), new Color(1f, 1f, 1f, grow), -Mathf.PI * 0.5f + Mathf.Sin(Age * 0.05f + s.x) * 0.15f, 1f);
                    }
                }
            }
        }

        /// <summary>The Razorblade Typhoon's art: one great typhoon drawing enemies in and cutting, then flinging six typhoons.</summary>
        sealed class BladeStorm : MageThing
        {
            public BladeStorm() { Life = 180; Hurts = true; HitEvery = 10; Size = new Vector2(5f, 5f) * MageMech.Block; }
            public override void Tick()
            {
                foreach (var e in Near(Pos, 8f, false)) MageMech.Pull(e, Pos, 7f);
            }
            public override void End()
            {
                var near = Near(Pos, 20f);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3f;
                    var to = near.Count > 0 ? Centre(near[i % near.Count]) : Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 8f * MageMech.Block;
                    Bolt(Def, ProjKind.RazorbladeTyphoon, Pos, At(Pos, to, 7f), 0.8f);
                }
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float grow = Mathf.Clamp01(Age / 20f), fade = Mathf.Clamp01((Life - Age) / 20f);
                // A funnel of the typhoon's own rings seen from the side (flattened), narrow at the foot, swaying as it turns.
                for (int k = 7; k >= 0; k--)
                {
                    float t = k / 7f, s = Mathf.Lerp(0.45f, 1.4f, t) * grow;
                    var at = px + new Vector2(Mathf.Sin(Age * 0.08f + k * 0.7f) * 7f * t, 40f - 80f * t) * grow;
                    int frame = (Age / 3 + k) % 3;
                    fx.Draw(FxBatch.Frame("Projectile_409", 0, frame * 68, 68, 68, 34f, 34f), at, new Color(1f, 1f, 1f, 0.8f * fade), 0f, new Vector2(s, s * 0.4f));
                }
                // The six typhoons it gathered, climbing round it.
                for (int i = 0; i < 6; i++)
                {
                    float a = Age * 0.15f + i * Mathf.PI / 3f, t = Mathf.Repeat(Age * 0.012f + i / 6f, 1f);
                    var at = px + new Vector2(Mathf.Cos(a) * Mathf.Lerp(18f, 58f, t), 40f - 80f * t) * grow;
                    Anim(fx, "Projectile_409", 3, Age / 3 + i, at, new Color(1f, 1f, 1f, 0.85f * fade * Mathf.Sin(t * Mathf.PI)), -Age * 0.4f, 0.55f * grow);
                }
                for (int i = 0; i < 3; i++)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(1f, 4f) * MageMech.Block;
                    MageMech.Dust(Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 33, new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * 4f, 1.4f);
                }
            }
        }

        /// <summary>The Shadowbeam Staff's prism: beams at every enemy as it rises and again in time; the player's beams split through it.</summary>
        sealed class ShadowPrism : MageThing
        {
            public ShadowPrism() { Life = 300; }
            public void Refract(float k)
            {
                foreach (var e in Near(Pos, 20f))
                    Bolt(Def, ProjKind.Shadowbeam, Pos, At(Pos, Centre(e), 6f), k);
                MageMech.Ring(Pos, 0.3f, 27, 16, 4f, 1.3f);
            }
            public override void Tick()
            {
                if (Age % 20 == 0) Refract(1f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                Sprite(fx, "Item_3541", px, new Color(0.65f, 0.35f, 1f), Mathf.Sin(Age * 0.05f) * 0.1f, 1.6f);
                if (Age % 3 == 0) MageMech.Dust(Pos + Random.insideUnitCircle * 1.2f * MageMech.Block, 27, Vector2.zero, 1.2f);
                FxBatch.Instance?.Light(Pos, Shadow * 0.6f, 4f);
            }
        }

        /// <summary>A pool of moonlight: what stands in it is hurt every half second.</summary>
        sealed class Moonlight : MageThing
        {
            public float Radius = 2f;
            public int Every = 30;
            public Moonlight() { Life = 120; }
            public override void Tick()
            {
                if (Age % Every != 0) return;
                foreach (var e in Near(Pos + Vector2.up * MageMech.Block, Radius, false)) MageMech.Strike(Def, e, Damage);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(Radius / 2f));
                if (Age % 3 == 0)
                    for (int i = 0; i < n; i++)
                        MageMech.Dust(Pos + new Vector2(Random.Range(-0.8f, 0.8f) * Radius, 0f) * MageMech.Block, 229, Vector2.up * Random.Range(0.5f, 1.5f), 1.1f);
                if (Age % 10 == 0) FxBatch.Instance?.Light(Pos, Moon * 0.4f, Radius + 0.5f);
            }
        }

        /// <summary>The Lunar Flare's art: a moon falling onto its spot, a shockwave, every moonlight pool bursting.</summary>
        sealed class MoonFall : MageThing
        {
            public Vector2 Target;
            public MoonFall() { Life = 120; }
            public override void Tick()
            {
                var way = Target - Pos;
                float step = Mathf.Min(1.1f, 0.2f + Age * 0.04f) * MageMech.Block;
                if (way.magnitude <= step) { Pos = Target; Vel = Vector2.zero; Crash(); return; }
                Vel = way.normalized * step;
                if (Age % 2 == 0) Burst(Pos, 229, 3, 1.5f, 1.6f);
            }
            void Crash()
            {
                Done = true;
                foreach (var e in Near(Target, 14f, false))
                    MageMech.Strike(Def, e, MageMech.DamageOf(Def, Vector2.Distance(Centre(e), Target) < 4f * MageMech.Block ? 20f : 10f));
                foreach (var t in MageMech.Things("moonlight"))
                {
                    foreach (var e in Near(t.Pos + Vector2.up * MageMech.Block, 3f, false)) MageMech.Strike(Def, e, MageMech.DamageOf(Def, 3f));
                    Burst(t.Pos, 229, 16, 4f, 1.5f);
                    t.Done = true;
                }
                MageMech.Add(new Moonlight { Def = Def, Pos = Target, Key = "moon_crater", Radius = 6f, Every = 15, Life = 180, Damage = MageMech.DamageOf(Def, 2f) }, 1);
                MageMech.Ring(Target, 0.3f, 229, 48, 10f, 1.8f);
                MageMech.Ring(Target, 0.2f, 15, 24, 6f, 1.4f);
                for (int i = 0; i < 24; i++) MageMech.Dust(Target + new Vector2(Random.Range(-14f, 14f), 0.2f) * MageMech.Block, 229, Vector2.up * Random.Range(2f, 6f), 1.6f);
                Sound("Item_14", 0.8f, 0.6f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                var tex = Terraria.TerrariaAssets.Texture("Moon_0");
                if (tex != null) fx.Draw(FxBatch.Frame("Moon_0", 0, 0, tex.width, tex.width, tex.width / 2f, tex.width / 2f), px, Color.white, Age * 0.01f, 1.6f);
                FxBatch.Instance?.Light(Pos, Moon * 0.8f, 8f);
            }
        }

        /// <summary>Something that flies to an enemy (passing through rock) and strikes it on arrival: a falling star, a shard.</summary>
        sealed class Comet : MageThing
        {
            public HealthManager Target;
            public Vector2 To;
            public string Texture = "Projectile_92";
            public int DustType = 58, Delay;
            /// <summary>Its texture's frames, stacked (the Nebula Blaze's four).</summary>
            public int Frames = 1;
            public Color Tint = Color.white;
            public float Speed = 0.7f, Scale = 1f;
            public Comet() { Life = 300; }
            public override void Tick()
            {
                if (Age < Delay) { Vel = Vector2.zero; return; }
                // An enemy gone on the way: the comet still lands where it last was.
                if (Target != null && (Target.isDead || !Target.gameObject.activeInHierarchy)) Target = null;
                else if (Target != null) To = Centre(Target);
                var to = To;
                var way = to - Pos;
                float step = Speed * MageMech.Block;
                if (way.magnitude <= step)
                {
                    Pos = to;
                    Vel = Vector2.zero;
                    if (Target != null) MageMech.Strike(Def, Target, Damage);
                    else foreach (var e in Near(Pos, 1.5f, false)) MageMech.Strike(Def, e, Damage);
                    Burst(Pos, DustType, 12, 3f, 1.3f);
                    Done = true;
                    return;
                }
                Vel = way.normalized * step;
                if (Age % 2 == 0) MageMech.Dust(Pos, DustType, Vector2.zero, 1.1f * Scale);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                if (Age < Delay) return;
                Anim(fx, Texture, Frames, Age / 4, px, Tint, -Mathf.Atan2(Vel.y, Vel.x), Scale);
            }
        }

        /// <summary>A Spectre wisp circling the player: it hurts what comes close; sent off, it charges its enemy.</summary>
        sealed class OrbitWisp : MageThing
        {
            public int Slot;
            HealthManager prey;
            bool charging;
            public OrbitWisp() { Life = 600; Hurts = true; HitEvery = 30; Size = new Vector2(1f, 1f) * MageMech.Block; }
            public void Charge(HealthManager e)
            {
                prey = e;
                charging = true;
                Hurts = false;
                Life = Age + 90;
            }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) { Done = true; return; }
                Vel = Vector2.zero;
                if (Age % 2 == 0) MageMech.Dust(Pos, 175, Vector2.zero, charging ? 1.6f : 1.1f, 100);
                if (charging)
                {
                    var to = prey != null && !prey.isDead ? Centre(prey) : BladeSkills.MageAim();
                    Pos = Vector2.MoveTowards(Pos, to, 0.9f * MageMech.Block);
                    if (Vector2.Distance(Pos, to) > 0.4f * MageMech.Block) return;
                    if (prey != null && !prey.isDead)
                    {
                        MageMech.Strike(Def, prey, MageMech.DamageOf(Def, 3f));
                        TerraMod.Instance?.Stats.Heal(1);
                    }
                    Burst(Pos, 175, 14, 3f, 1.5f);
                    Done = true;
                    return;
                }
                float a = Age * 0.06f + Slot * Mathf.PI / 3f;
                var home = HeroCentre(hero) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.5f * MageMech.Block;
                Pos = Vector2.MoveTowards(Pos, home, 0.5f * MageMech.Block);
            }
        }

        /// <summary>A pillar of hellfire standing where it rose.</summary>
        sealed class HellPillar : MageThing
        {
            public HellPillar() { Life = 120; Hurts = true; HitEvery = 20; Size = new Vector2(1.4f, 5f) * MageMech.Block; }
            public override void Hit(HealthManager hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Hellfire, 240);
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float h = Size.y / MageMech.Block * 0.5f, rise = Mathf.Clamp01(Age / 8f);
                for (int i = 0; i < 7; i++)
                    MageMech.Dust(Pos + new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(-h, -h + 2f * h * rise)) * MageMech.Block, i % 2 == 0 ? 6 : 259, Vector2.up * Random.Range(2f, 4f), Random.Range(1.3f, 2f));
                if (Age % 8 == 0) FxBatch.Instance?.Light(Pos, Fire * 0.6f, 3f);
            }
        }

        /// <summary>The Staff of Earth's art: a fissure running along the ground, rock bursting up.</summary>
        sealed class Fissure : MageThing
        {
            public float Dir;
            int step;
            readonly Dictionary<HealthManager, int> hits = new Dictionary<HealthManager, int>();
            public Fissure() { Life = 62; }
            public override void Tick()
            {
                if (Age % 3 != 0) return;
                step++;
                var at = Ground(Pos + new Vector2(Dir * step, 2f) * MageMech.Block, 6f);
                for (int i = 0; i < 4; i++) MageMech.Dust(at + new Vector2(Random.Range(-0.5f, 0.5f), 0f) * MageMech.Block, i % 2 == 0 ? 1 : 0, new Vector2(Random.Range(-1f, 1f), Random.Range(3f, 6f)), Random.Range(1.2f, 1.8f));
                foreach (var e in Near(at + Vector2.up * 1.5f * MageMech.Block, 2f, false))
                {
                    hits.TryGetValue(e, out int n);
                    if (n >= 2) continue;
                    hits[e] = n + 1;
                    MageMech.Strike(Def, e, MageMech.DamageOf(Def, 2.8f));
                    MageMech.Pull(e, Centre(e) + Vector2.up * 2f, 20f);
                }
                if (step % 5 == 0) Sound("Item_69", 0.3f, 1.2f);
                if (step != 20) return;
                for (int i = 0; i < 3; i++) Bolt(Def, ProjKind.EarthBoulder, at + Vector2.up * (1f + i) * MageMech.Block, new Vector2(Dir * (5f + i), 2f), 1f);
            }
        }

        /// <summary>The Unholy Trident's art: three tridents in the ground, a triangle that hurts, then back to the player's hand.</summary>
        sealed class TridentField : MageThing
        {
            public TridentField() { Life = 240; }
            Vector2 Corner(int i)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 3f;
                return Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5f * MageMech.Block;
            }
            public override void Tick()
            {
                if (Age % 4 == 0) for (int i = 0; i < 3; i++) DustLine(Corner(i), Corner((i + 1) % 3), 27, 1f, 1.1f, 0.1f);
                if (Age % 20 != 0) return;
                foreach (var e in Near(Pos, 5f, false))
                {
                    MageMech.Strike(Def, e, MageMech.DamageOf(Def, 0.7f));
                    EnemyDebuffs.Add(e, EnemyDebuffs.Shadowflame, 180);
                }
            }
            public override void End()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) return;
                var home = HeroCentre(hero);
                var hit = new HashSet<HealthManager>();
                for (int i = 0; i < 3; i++)
                {
                    foreach (var e in Along(Corner(i), home, 1.5f)) hit.Add(e);
                    DustLine(Corner(i), home, 27, 0.6f, 1.3f);
                }
                foreach (var e in hit) MageMech.Strike(Def, e, MageMech.DamageOf(Def, 1f));
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 3; i++) Sprite(fx, "Projectile_114", TerraParticles.ToPx(Corner(i)), Color.white, Mathf.PI * 0.75f, 1.2f);
                FxBatch.Instance?.Light(Pos, TridentViolet * 0.4f, 5f);
            }
        }

        /// <summary>A toxic cloud drifting on: what is in it poisoned and hurt.</summary>
        sealed class DriftCloud : MageThing
        {
            public DriftCloud() { Life = 240; Hurts = true; HitEvery = 20; Size = new Vector2(3f, 2.5f) * MageMech.Block; }
            public override void Hit(HealthManager hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, 240);
            public override void Draw(FxBatch fx, Vector2 px)
            {
                if (Age % 2 == 0) MageMech.Dust(Pos + new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(-1.2f, 1.2f)) * MageMech.Block, 46, Random.insideUnitCircle * 0.4f, Random.Range(1.3f, 1.8f), 100);
            }
        }

        /// <summary>The Toxic Flask's art: a cloud spreading wide, poisoning, envenoming and hurting.</summary>
        sealed class GreatCloud : MageThing
        {
            public GreatCloud() { Life = 240; }
            float Radius => Mathf.Lerp(3f, 10f, Mathf.Clamp01(Age / 150f));
            public override void Tick()
            {
                if (Age % 10 != 0) return;
                foreach (var e in Near(Pos, Radius, false))
                {
                    MageMech.Strike(Def, e, MageMech.DamageOf(Def, 1.4f));
                    EnemyDebuffs.Add(e, EnemyDebuffs.Poisoned, 240);
                    EnemyDebuffs.Add(e, EnemyDebuffs.Venom, 180);
                }
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 4; i++)
                    MageMech.Dust(Pos + Random.insideUnitCircle * Radius * MageMech.Block, i % 2 == 0 ? 46 : 171, Random.insideUnitCircle * 0.5f, Random.Range(1.4f, 2.2f), 100);
            }
        }

        /// <summary>The Resonance Scepter's art: a field round the player where every enemy rings with the others.</summary>
        sealed class ResonanceField : MageThing
        {
            public ResonanceField() { Life = 240; }
            public override void Tick()
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) { Done = true; return; }
                Pos = HeroCentre(hero);
                if (Age % 20 != 0) return;
                var near = Near(Pos, 10f, false);
                float k = 1.6f * (1f + 0.5f * Mathf.Min(3, near.Count - 1));
                for (int i = 0; i < near.Count; i++)
                {
                    MageMech.Strike(Def, near[i], MageMech.DamageOf(Def, k));
                    MageMech.AddMark(near[i], "resonance", 1, 5, 5f, Resonant, 267);
                    if (near.Count > 1) DustLine(Centre(near[i]), Centre(near[(i + 1) % near.Count]), 267, 0.8f, 1f);
                }
                MageMech.Ring(Pos, 0.5f, 267, 28, 7f, 1.3f);
                Sound("Item_26", 0.3f, 1.3f);
            }
            public override void End()
            {
                foreach (var e in Near(Pos, 10f, false))
                {
                    MageMech.Strike(Def, e, MageMech.DamageOf(Def, 5f));
                    Burst(Centre(e), 267, 14, 4f, 1.4f);
                }
                MageMech.Ring(Pos, 0.5f, 267, 48, 10f, 1.6f);
            }
        }

        /// <summary>A wire left between two enemies an arc linked: what touches it is shocked.</summary>
        sealed class WireLink : MageThing
        {
            public HealthManager A, Z;
            public WireLink() { Life = 180; }
            public override void Tick()
            {
                if (A == null || A.isDead || Z == null || Z.isDead) { Done = true; return; }
                Pos = Centre(A);
                var b = Centre(Z);
                if (Age % 4 == 0) DustLine(Pos, b, 226, 0.9f, 0.8f, 0.12f);
                if (Age % 20 != 0) return;
                foreach (var e in Along(Pos, b, 1f))
                    if (e != A && e != Z) MageMech.Strike(Def, e, Damage);
            }
        }
    }
}
