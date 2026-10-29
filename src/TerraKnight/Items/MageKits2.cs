using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;
using RangedShot = TerraKnight.Items.TerraProjectiles.RangedShot;

namespace TerraKnight.Items
{
    /// <summary>The remade magic weapons' mechanisms (MageKits): stage 2.</summary>
    internal static partial class MageKits
    {
        static readonly Color Blood = new Color(0.9f, 0.1f, 0.15f), Ice = new Color(0.55f, 0.85f, 1f), Honey = new Color(1f, 0.75f, 0.2f), Poison = new Color(0.5f, 0.9f, 0.3f);

        static void Lefts2()
        {
            // ---- Crimson Rod: its rain marks with blood.
            MageLefts.Custom(ItemType.CrimsonRod, "血云的雨滴给敌人叠血印记（最多 5 层，6 秒）",
                "the cloud's rain marks enemies with blood (5 at most, 6 s)",
                (s, r, run) =>
                {
                    r.OnStrike = (hm, at) => MageMech.AddMark(hm, "blood", 1, 5, 6f, Blood, 5);
                    Cast(s, r);
                });

            // ---- Glacier Fang: its icicles stay in what they strike, slowing it the more there are.
            MageLefts.Custom(ItemType.GlacierFang, "冰枪钉在敌人身上（最多 5 根，6 秒），钉得越多敌人越慢；按住左键连续蓄发",
                "its icicles stay in what they strike (5 at most, 6 s), slowing it the more there are; hold the left button to go on charging",
                (s, r, run) =>
                {
                    r.OnStrike = (hm, at) =>
                    {
                        int n = MageMech.AddMark(hm, "icicle", 1, 5, 6f, Ice, 135);
                        MageMech.Slow(hm, 1f - 0.12f * n, 6f);
                    };
                    Cast(s, r);
                });

            // ---- Water Bolt: each bounce throws out a smaller bolt.
            MageLefts.Custom(ItemType.WaterBolt, "水矢每次反弹分出一道小水矢（0.4 倍），每道最多分两次",
                "each bounce of a water bolt throws out a smaller one (0.4×), twice at most",
                (s, r, run) => Cast(s, WaterSplits(s.Def, r, 2, s.Kind)));

            // ---- Vilethorn: its thorns stay in the enemies they reach.
            MageLefts.Custom(ItemType.Vilethorn, "魔刺扎中的敌人身上留下荆棘：4 秒内每秒扎一下（0.25 倍），使它中毒；最多 3 根",
                "the enemies its thorns reach keep a thorn: a prick each second for 4 s (0.25×), poisoning; 3 at most",
                (s, r, run) =>
                {
                    var def = s.Def;
                    Cast(s, r);
                    // Its thorns grow along the aim (about 12 blocks): what stands within a block of the line is caught.
                    var dir = s.Vel.sqrMagnitude > 1e-6f ? s.Vel.normalized : Vector2.right;
                    foreach (var e in Near(s.From, 13f))
                    {
                        var c = Centre(e);
                        float along = Vector2.Dot(c - s.From, dir);
                        if (along < 0f || along > 12f * B) continue;
                        if (Vector2.Distance(c, s.From + dir * along) > 1.1f * B) continue;
                        Thorns.On(def, e);
                    }
                });

            // ---- Space Gun: heat.
            MageLefts.Custom(ItemType.SpaceGun, "连射积热：越热激光越粗越强（最多 1.4 倍）；过热时朝四周排出八道激光（各 0.4 倍），随后冷却片刻",
                "firing builds heat: the hotter, the thicker and stronger the laser (up to 1.4×); overheated, it vents eight lasers all round (0.4× each) and cools a moment",
                (s, r, run) =>
                {
                    float heat = Heat.Add(s.Def, 9f);
                    r.Size = 1f + heat / 100f * 0.8f;
                    r.Trail = heat / 100f;
                    r.TrailDust = 6;
                    if (Heat.Cooling(s.Def)) { r.Size = 0.8f; r.Trail = 0f; }
                    var t = s;
                    t.Damage = Mathf.RoundToInt(s.Damage * (Heat.Cooling(s.Def) ? 0.6f : 0.85f + heat / 100f * 0.55f));
                    Cast(t, r);
                    if (heat >= 100f)
                    {
                        Heat.Vent(s.Def, 1.2f);
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * Mathf.PI / 4f;
                            Bolt(s.Def, ProjKind.Laser, s.From, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Max(8f, s.Def.ShootSpeed), 0.4f);
                        }
                        Burst(s.From, 6, 20, 5f, 1.4f);
                        Sound("Item_14", 0.5f, 1.4f);
                    }
                });

            // ---- Bee Gun: honey on what they sting; every bee goes for the honey.
            MageLefts.Custom(ItemType.BeeGun, "蜜蜂蜇过的敌人沾上蜂蜜（4 秒），所有蜜蜂都去追沾了蜂蜜的敌人",
                "what the bees sting is smeared with honey (4 s), and every bee goes after the honeyed",
                (s, r, run) =>
                {
                    r.OnStrike = (hm, at) => MageMech.AddMark(hm, "honey", 1, 1, 4f, Honey, 152);
                    r.Steer = (pos, vel) =>
                    {
                        HealthManager best = null;
                        float bestD = 15f * B;
                        foreach (var (e, n) in MageMech.Marked("honey"))
                        {
                            float d = Vector2.Distance(Centre(e), pos);
                            if (d < bestD) { bestD = d; best = e; }
                        }
                        return best != null ? Centre(best) : (Vector2?)null;
                    };
                    r.SteerTurn = 9f;
                    Cast(s, r);
                });

            // ---- Aqua Scepter: its stream leaves a ribbon of water that hurts.
            MageLefts.Custom(ItemType.AquaScepter, "水流蛇行前进，沿途留下停留半秒的水带，碰到的敌人受伤（0.2 倍）",
                "its stream winds as it goes and leaves a ribbon of water for half a second that hurts what it touches (0.2×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.Weave = 20f;
                    int t = 0;
                    r.OnFly = (pos, vel) =>
                    {
                        if (++t % 3 == 0) MageMech.Add(new Ribbon { Def = def, Pos = pos, Key = "aqua_ribbon", DustType = 33, Damage = MageMech.DamageOf(def, 0.2f) }, 40);
                        return true;
                    };
                    Cast(s, r);
                });

            // ---- Petalstorm: its petals wheel round the cursor a moment, then go for the enemies.
            MageLefts.Custom(ItemType.Petalstorm, "花瓣先绕着鼠标转半圈聚成花环，再各自追向敌人",
                "its petals wheel half way round the cursor into a garland, then each goes after an enemy",
                (s, r, run) =>
                {
                    var at = s.Mouse;
                    int age = 0;
                    float start = Random.value * Mathf.PI * 2f;
                    r.Steer = (pos, vel) =>
                    {
                        age++;
                        if (age < 40)
                        {
                            float a = start + age * 0.08f;
                            return at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.5f * B;
                        }
                        var e = Nearest(pos, 16f);
                        return e != null ? Centre(e) : (Vector2?)null;
                    };
                    r.SteerTurn = 14f;
                    r.SteerSpeed = 5f;
                    Cast(s, r);
                });

            // ---- Flamelash: it leaves fire where it goes.
            MageLefts.Custom(ItemType.Flamelash, "火球飞过的路线留下 1 秒的火焰，经过的敌人着火受伤（0.2 倍）",
                "its fireball leaves fire along its way for a second, burning what goes through (0.2×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    int t = 0;
                    r.OnFly = (pos, vel) =>
                    {
                        if (++t % 4 == 0) MageMech.Add(new Ribbon { Def = def, Pos = pos, Key = "flame_trail", DustType = 6, Life = 60, Burns = true, Damage = MageMech.DamageOf(def, 0.2f) }, 40);
                        return true;
                    };
                    Cast(s, r);
                });

            // ---- Flower of Fire: flames where it bounces.
            MageLefts.Custom(ItemType.FlowerofFire, "火球每次落地弹起，都在落点留下 2 秒的火苗（0.25 倍）",
                "each time its fireball bounces off the ground it leaves a flame there for 2 s (0.25×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    float lastY = 0f;
                    r.OnFly = (pos, vel) =>
                    {
                        // A bounce: falling one tick, rising the next.
                        if (lastY < -0.5f && vel.y > 0.5f)
                            MageMech.Add(new Ember { Def = def, Pos = Ground(pos, 1.5f) + Vector2.up * 0.15f, Key = "flower_flame", Damage = MageMech.DamageOf(def, 0.25f), Life = 120 }, 8);
                        lastY = vel.y;
                        return true;
                    };
                    Cast(s, r);
                });

            // ---- Weather Pain: the weather under the cloud.
            MageLefts.Custom(ItemType.WeatherPain, "风暴云下的敌人每半秒随机遭到一种天气：冰雹（减速）、闪电（0.2 倍）或热浪（着火）",
                "every half second an enemy under the storm meets weather at random: hail (slowed), lightning (0.2×) or a heatwave (on fire)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    int t = 0;
                    r.OnFly = (pos, vel) =>
                    {
                        if (++t % 30 != 0) return true;
                        foreach (var e in Near(pos + Vector2.down * 2f * B, 3f, false)) Weather(def, e, pos, Random.Range(0, 3), 0.2f);
                        return true;
                    };
                    Cast(s, r);
                });

            // ---- Magic Missile: a ribbon of light behind it.
            MageLefts.Custom(ItemType.MagicMissile, "飞弹拖着半秒的光带，碰到光带的敌人受伤（0.2 倍）",
                "its missile trails a ribbon of light for half a second that hurts what touches it (0.2×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    int t = 0;
                    r.OnFly = (pos, vel) =>
                    {
                        if (++t % 3 == 0) MageMech.Add(new Ribbon { Def = def, Pos = pos, Key = "missile_ribbon", DustType = 15, Damage = MageMech.DamageOf(def, 0.2f) }, 40);
                        return true;
                    };
                    Cast(s, r);
                });

            // ---- Demon Scythe: it comes back.
            MageLefts.Custom(ItemType.DemonScythe, "镰刀飞出 14 格或砍中敌人后掉头飞回你手里，回程再切一次",
                "its scythe flies out 14 blocks or until it cuts something, then turns and comes back to your hand, cutting again on the way",
                (s, r, run) =>
                {
                    r.Pierce = 2;
                    // Out at once at 8 px an update (not the scythe's own slow drift), back from 14 blocks or its first cut.
                    OutAndBack(r, s.From, s.Mouse - s.From, 14f, 8f, true);
                    Cast(s, r);
                });

            // ---- Book of Skulls: they bite and hang on.
            MageLefts.Custom(ItemType.BookofSkulls, "骷髅头咬住敌人，2 秒内每半秒再啃一口（0.1 倍）",
                "its skull bites an enemy and hangs on, gnawing every half second for 2 s (0.1×)",
                (s, r, run) =>
                {
                    var def = s.Def;
                    r.OnStrike = (hm, at) => Bite.On(def, hm, 2f, 0.1f);
                    Cast(s, r);
                });
        }

        /// <summary>A Water Bolt that throws out a smaller one at each bounce (generations left).</summary>
        static RangedShot WaterSplits(ItemDef def, RangedShot r, int left, ProjKind kind)
        {
            if (left <= 0) return r;
            Vector2 lastVel = Vector2.zero;
            int splits = 0;
            r.OnFly = (pos, vel) =>
            {
                // A bounce: its heading turned past a right angle in one tick.
                if (lastVel.sqrMagnitude > 0.1f && Vector2.Dot(lastVel.normalized, vel.normalized) < 0f && splits < 2)
                {
                    splits++;
                    var side = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f)) * vel;
                    Bolt(def, kind, pos, side * 0.9f, 0.4f, WaterSplits(def, new RangedShot { Size = 0.7f }, left - 1, kind));
                    Burst(pos, 33, 6, 2f);
                }
                lastVel = vel;
                return true;
            };
            return r;
        }

        /// <summary>One weather on an enemy: hail (slowed), lightning (k), heat (on fire).</summary>
        static void Weather(ItemDef def, HealthManager e, Vector2 from, int which, float k)
        {
            var c = Centre(e);
            switch (which)
            {
                case 0:
                    MageMech.Slow(e, 0.5f, 1.5f);
                    MageMech.Strike(def, e, MageMech.DamageOf(def, k * 0.5f));
                    Burst(c + Vector2.up * 0.6f, 135, 8, 2f);
                    break;
                case 1:
                    MageMech.Strike(def, e, MageMech.DamageOf(def, k));
                    Arc(from, c, def);
                    break;
                default:
                    EnemyDebuffs.Add(e, EnemyDebuffs.OnFire, 240);
                    MageMech.Strike(def, e, MageMech.DamageOf(def, k * 0.5f));
                    Burst(c, 6, 10, 2.5f);
                    break;
            }
        }

        static void Arts2()
        {
            // ---- Crimson Rod: blood rite.
            Art(ItemType.CrimsonRod).Press("血祭", 6f, 3f, "所有带血印记的敌人同时喷血：受到 4 倍伤害，每层印记再加 1.2 倍，每个敌人为你回复 2 点生命；没有时血雨落在鼠标 12 格内的敌人身上（5 倍）并叠满血印记，片刻后它们一齐喷血",
                "every enemy marked with blood bursts at once: 4×, and 1.2× more for each mark, each one healing you 2; with none, a blood rain falls on the enemies within 12 blocks of the cursor (5×) and marks them in full, and a moment later they burst",
                (h, d) =>
                {
                    int healed = 0;
                    foreach (var (e, n) in MageMech.Marked("blood"))
                    {
                        MageMech.ClearMark(e, "blood");
                        MageMech.Strike(d, e, MageMech.DamageOf(d, 4f + 1.2f * n));
                        Burst(Centre(e), 5, 14 + n * 3, 4f, 1.5f);
                        healed += 2;
                    }
                    if (healed > 0) TerraMod.Instance?.Stats.Heal(healed);
                    else
                    {
                        var rained = Near(BladeSkills.MageAim(), 12f, false);
                        foreach (var e in rained)
                        {
                            MageMech.AddMark(e, "blood", 5, 5, 6f, Blood, 5);
                            MageMech.Strike(d, e, MageMech.DamageOf(d, 5f));
                            Burst(Centre(e), 5, 14, 4f, 1.5f);
                        }
                        TerraProjectiles.Instance?.Later(40, (f, v) =>
                        {
                            int back = 0;
                            foreach (var e in rained)
                            {
                                int n = MageMech.MarkOf(e, "blood");
                                if (e == null || e.isDead || n == 0) continue;
                                MageMech.ClearMark(e, "blood");
                                MageMech.Strike(d, e, MageMech.DamageOf(d, 0.6f * n));
                                Burst(Centre(e), 5, 14 + n * 3, 4f, 1.5f);
                                back += 2;
                            }
                            if (back > 0) TerraMod.Instance?.Stats.Heal(back);
                        });
                    }
                    BladeSkills.MageArtCast(d, null);
                    Sound("NPC_Hit_13", 0.6f, 0.8f);
                    return true;
                });

            // ---- Glacier Fang: glacier fall.
            Art(ItemType.GlacierFang).Press("冰川崩落", 6f, 3f, "所有钉着冰枪的敌人身上的冰枪一齐炸开：每根 1.5 倍，并冻住片刻；没有钉着冰枪的敌人时，鼠标附近的敌人受到一次冰爆（5 倍）",
                "the icicles in every enemy burst at once: 1.5× each, and it freezes a moment; with none, the enemies by the cursor take an icy blast (5×)",
                (h, d) =>
                {
                    var marked = MageMech.Marked("icicle");
                    foreach (var (e, n) in marked)
                    {
                        MageMech.ClearMark(e, "icicle");
                        MageMech.Strike(d, e, MageMech.DamageOf(d, 1.5f * n));
                        MageMech.Slow(e, 0.1f, 0.6f + 0.2f * n);
                        Burst(Centre(e), 135, 10 + n * 5, 4f, 1.5f);
                    }
                    if (marked.Count == 0)
                        foreach (var e in Near(BladeSkills.MageAim(), 6f)) { MageMech.Strike(d, e, MageMech.DamageOf(d, 5f)); MageMech.Slow(e, 0.3f, 1.5f); }
                    BladeSkills.MageArtCast(d, null);
                    Sound("Item_27", 0.7f, 0.7f);
                    return true;
                });

            // ---- Water Bolt: tide.
            Art(ItemType.WaterBolt).Press("潮汐", 6f, 3f, "脚下涌起一道浪，沿地面朝面向推进 20 格，卷走碰到的敌人（6 倍）",
                "a wave wells up at your feet and rolls 20 blocks along the ground the way you face, sweeping what it meets along (6×)",
                (h, d) =>
                {
                    float face = BladeSkills.MageTurn(h);
                    var at = Ground(HeroCentre(h), 3f) + Vector2.up * 0.8f * B;
                    MageMech.Add(new Wave { Def = d, Pos = at, Vel = new Vector2(face * 0.35f * B, 0f), Key = "tide", Damage = MageMech.DamageOf(d, 6f) }, 2);
                    BladeSkills.MageArtCast(d, null);
                    Sound("Item_21", 0.7f, 0.7f);
                    return true;
                });

            // ---- Vilethorn: a thicket.
            Art(ItemType.Vilethorn).Press("荆棘丛", 6f, 3f, "鼠标处地面长出 8 格宽的荆棘丛，4 秒内扎伤经过的敌人（1 倍）、使其中毒并减速",
                "an 8-block thicket of thorns grows from the ground on the cursor, pricking what goes through for 4 s (1×), poisoning and slowing it",
                (h, d) =>
                {
                    var at = Ground(BladeSkills.MageAim(), 6f) + Vector2.up * 0.6f * B;
                    MageMech.Add(new Thicket { Def = d, Pos = at, Key = "thicket", Damage = MageMech.DamageOf(d, 1f) }, 2);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- Bee Gun: a hive.
            Art(ItemType.BeeGun).Press("蜂巢", 6f, 3f, "鼠标处落下一个蜂巢，5 秒内不断放出蜜蜂，附近的敌人被蜂蜜粘住减速",
                "a hive drops onto the cursor and for 5 s lets out bee after bee; enemies near are stuck in its honey, slowed",
                (h, d) =>
                {
                    var at = Ground(BladeSkills.MageAim(), 6f) + Vector2.up * 0.6f * B;
                    MageMech.Add(new Hive { Def = d, Pos = at, Key = "hive" }, 1);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- Aqua Scepter: a waterspout.
            Art(ItemType.AquaScepter).Press("水龙卷", 7f, 3f, "鼠标处卷起水龙卷 4 秒，把附近的敌人吸进去打转（首领和精英不受牵引），卷中的敌人不断受伤（0.9 倍）",
                "a waterspout whirls on the cursor for 4 s, drawing the enemies near into it (not bosses or elites) and hurting what spins in it (0.9×)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    MageMech.Add(new Whirl { Def = d, Pos = at, Key = "whirl", Damage = MageMech.DamageOf(d, 0.9f), DustType = 33 }, 1);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- Petalstorm: a great flower shedding petals.
            Art(ItemType.Petalstorm).Press("落英", 6f, 3f, "鼠标处开出一朵巨大的花，3 秒内花瓣一片片飘落，每片追向不同的敌人（各 0.55 倍）",
                "a great flower opens on the cursor and sheds its petals one by one for 3 s, each after a different enemy (0.55× each)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    MageMech.Add(new Blossom { Def = d, Pos = at, Key = "blossom" }, 1);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- Flamelash: a pillar of fire that follows the cursor.
            Art(ItemType.Flamelash).Press("火柱", 6f, 3f, "召出一道 5 格高的火柱，3 秒内跟着鼠标扫过地面，烧伤碰到的敌人（0.8 倍）",
                "a pillar of fire 5 blocks high rises and sweeps along after the cursor for 3 s, burning what it touches (0.8×)",
                (h, d) =>
                {
                    var at = Ground(BladeSkills.MageAim(), 6f) + Vector2.up * 2.5f * B;
                    MageMech.Add(new FirePillar { Def = d, Pos = at, Key = "fire_pillar", Damage = MageMech.DamageOf(d, 0.8f) }, 1);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- Flower of Fire: a flower of fireballs that come back.
            Art(ItemType.FlowerofFire).Press("火焰之花", 5f, 3f, "脚下绽放一朵火焰花，六片火球花瓣朝鼠标方向扇形飞出，再飞回你身边（各 2 倍）",
                "a flower of fire opens at your feet: six fireball petals fan out toward the cursor and come back to you (2× each)",
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
                        var r = new RangedShot { Pierce = 3 };
                        OutAndBack(r, c, way, 9f, 8f, false);
                        Bolt(d, ProjKind.BallofFire, c, way * 9f, 2f, r);
                    }
                    Burst(c, 6, 24, 5f, 1.5f);
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });

            // ---- Weather Pain: the weather turns.
            Art(ItemType.WeatherPain).Press("气象剧变", 7f, 3f, "鼠标附近的天气在雷暴、冰雹、热浪之间轮换 3 秒，每半秒落在范围内的每个敌人身上一次：雷击（0.9 倍）、冰雹（减速）或热浪（着火）",
                "for 3 s the weather by the cursor turns through thunder, hail and heat, every half second on every enemy there: lightning (0.9×), hail (slowed) or heat (on fire)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    MageMech.Add(new WeatherFront { Def = d, Pos = at, Key = "weather_front" }, 1);
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- Magic Missile: one missile from enemy to enemy, lines of light between.
            Art(ItemType.MagicMissile).Press("星链飞弹", 6f, 3f, "放出一枚大飞弹，在附近的敌人之间连续弹射 6 次（每次 0.8 倍），每两次命中之间留下一道停留 2 秒的光带，碰到光带的敌人受伤（0.3 倍）；只有一个敌人时绕一圈再撞回来",
                "one great missile bounces from enemy to enemy near, six strikes (0.8× each), leaving a line of light between each two for 2 s that hurts what touches it (0.3×); with one enemy alone it loops round and strikes again",
                (h, d) =>
                {
                    MageMech.Add(new ChainMissile { Def = d, Pos = HeroCentre(h), Aim = BladeSkills.MageAim(), Key = "chain_missile", Damage = MageMech.DamageOf(d, 0.8f) }, 2);
                    BladeSkills.MageArtCast(d, null);
                    Sound("Item_9", 0.6f, 0.8f);
                    return true;
                });

            // ---- Demon Scythe: a ring of scythes closing in.
            Art(ItemType.DemonScythe).Press("镰之环", 6f, 3f, "八把镰刀在鼠标处围成一圈，旋转着收拢，切碎中间的敌人（各 0.3 倍）",
                "eight scythes ring the cursor and spin in, cutting up what is in the middle (0.3× each)",
                (h, d) =>
                {
                    var at = BladeSkills.MageAim();
                    for (int i = 0; i < 8; i++)
                        MageMech.Add(new ScytheRing { Def = d, Centre_ = at, Angle = i * 45f, Key = "scythe_ring", Damage = MageMech.DamageOf(d, 0.3f) });
                    BladeSkills.MageArtCast(d, at);
                    return true;
                });

            // ---- Book of Skulls: a stampede of skulls along the ground.
            Art(ItemType.BookofSkulls).Press("骷髅洪流", 6f, 3f, "一串骷髅头沿地面朝面向奔涌而出，碰到的敌人被咬住 2 秒（每口 0.12 倍）",
                "a string of skulls rushes out along the ground the way you face; what they meet is bitten and held for 2 s (0.12× a bite)",
                (h, d) =>
                {
                    float face = BladeSkills.MageTurn(h);
                    var tp = TerraProjectiles.Instance;
                    for (int i = 0; i < 6; i++)
                    {
                        int at = i * 7;
                        tp?.Later(at, (f, v) =>
                        {
                            var hh = HeroController.SilentInstance;
                            if (hh == null) return;
                            var from = Ground(HeroCentre(hh), 3f) + Vector2.up * 0.7f * B;
                            Bolt(d, ProjKind.Skull, from, new Vector2(face * 8f, 0f), 0.4f, new RangedShot { Pierce = 3, OnStrike = (hm, p) => Bite.On(d, hm, 2f, 0.12f) });
                        });
                    }
                    BladeSkills.MageArtCast(d, null);
                    return true;
                });
        }

        // ================================================================== things (stage 2)

        /// <summary>A weapon's heat (Space Gun, Laser Machinegun...): built by firing, cooling when it stops; overheated, it vents.</summary>
        internal static class Heat
        {
            static readonly Dictionary<ItemType, (float heat, float at, float coolUntil)> heats = new Dictionary<ItemType, (float, float, float)>();

            /// <summary>Heat added for a shot (cooling 40 a second since the last); the heat now (0-100).</summary>
            public static float Add(ItemDef def, float add)
            {
                heats.TryGetValue(def.Type, out var h);
                float now = Mathf.Max(0f, h.heat - (Time.time - h.at) * 40f);
                if (Time.time < h.coolUntil) add = 0f;
                now = Mathf.Min(100f, now + add);
                heats[def.Type] = (now, Time.time, h.coolUntil);
                if (now > 30f && TerraMod.Instance != null && HeroController.SilentInstance != null)
                    MageMech.Dust(HeroCentre(HeroController.SilentInstance) + Random.insideUnitCircle * 0.5f, 6, Vector2.up * 1f, 0.6f + now / 100f);
                return now;
            }

            public static bool Cooling(ItemDef def) => heats.TryGetValue(def.Type, out var h) && Time.time < h.coolUntil;

            public static void Vent(ItemDef def, float seconds) => heats[def.Type] = (0f, Time.time, Time.time + seconds);
        }

        /// <summary>A ribbon left in the air (a stream's water, a missile's light, a fireball's flames): it hurts what touches it a moment.</summary>
        sealed class Ribbon : MageThing
        {
            public int DustType;
            public bool Burns;
            public Ribbon() { Life = 30; Hurts = true; HitEvery = 30; Size = new Vector2(0.8f, 0.8f) * MageMech.Block; }
            public override void Hit(HealthManager hm) { if (Burns) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 120); }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                if (Age % 3 == 0) MageMech.Dust(Pos + Random.insideUnitCircle * 0.2f, DustType, Burns ? Vector2.up * 0.6f : Vector2.zero, 1.1f * (1f - Age / (float)Life) + 0.4f);
            }
        }

        /// <summary>Thorns in an enemy (Vilethorn): a prick a second for 4 s, poisoning; three at most.</summary>
        sealed class Thorns : MageThing
        {
            HealthManager on;
            public static void On(ItemDef def, HealthManager hm)
            {
                if (MageMech.AddMark(hm, "thorns", 1, 3, 4f, Poison, 39) > 3) return;
                MageMech.Add(new Thorns { Def = def, on = hm, Life = 240, Pos = Centre(hm), Key = "thorns" }, 24);
            }
            public override void Tick()
            {
                if (on == null || on.isDead) { Done = true; return; }
                Pos = Centre(on);
                if (Age % 60 != 30) return;
                MageMech.Strike(Def, on, MageMech.DamageOf(Def, 0.25f));
                EnemyDebuffs.Add(on, EnemyDebuffs.Poisoned, 180);
                Burst(Pos, 39, 5, 1.5f);
            }
        }

        /// <summary>A thicket of thorns on the ground: pricking, poisoning and slowing what goes through.</summary>
        sealed class Thicket : MageThing
        {
            public Thicket() { Life = 240; Hurts = true; HitEvery = 20; Size = new Vector2(8f, 1.6f) * MageMech.Block; }
            public override void Hit(HealthManager hm)
            {
                EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, 240);
                MageMech.Slow(hm, 0.5f, 0.6f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float grow = Mathf.Clamp01(Age / 15f), fade = Mathf.Clamp01((Life - Age) / 20f);
                for (int i = -3; i <= 3; i++)
                    Sprite(fx, "Projectile_7", px + new Vector2(i * 18f, -6f * grow), new Color(1f, 1f, 1f, fade), (i % 2 == 0 ? -0.2f : 0.2f) + Mathf.PI * 1.5f, 0.9f * grow);
                if (Age % 6 == 0) MageMech.Dust(Pos + new Vector2(Random.Range(-4f, 4f), 0.3f) * MageMech.Block, 39, Vector2.up * 0.5f, 1f);
            }
        }

        /// <summary>A wave rolling along the ground, sweeping enemies along.</summary>
        sealed class Wave : MageThing
        {
            public bool Ice;
            public Wave() { Life = 60; Hurts = true; HitEvery = 20; Size = new Vector2(2f, 2.2f) * MageMech.Block; }
            public override void Tick()
            {
                foreach (var e in Near(Pos, 1.6f, false)) MageMech.Pull(e, Centre(e) + new Vector2(Mathf.Sign(Vel.x), 0f), 8f);
                for (int i = 0; i < 4; i++) MageMech.Dust(Pos + new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * MageMech.Block, Ice ? 135 : 33, new Vector2(Mathf.Sign(Vel.x) * 2f, Random.Range(0f, 2f)), Random.Range(1.2f, 1.8f));
            }
            public override void Hit(HealthManager hm)
            {
                if (!Ice) return;
                EnemyDebuffs.Add(hm, EnemyDebuffs.Frostburn, 240);
                MageMech.Slow(hm, 0.5f, 1f);
                MageMech.AddMark(hm, "tide_frost", 1, 1, 3f, MageKits.Ice, 135);
            }
            public override void End()
            {
                if (!Ice) return;
                foreach (var (e, n) in MageMech.Marked("tide_frost"))
                {
                    MageMech.ClearMark(e, "tide_frost");
                    MageMech.Strike(Def, e, MageMech.DamageOf(Def, 7f));
                    Burst(Centre(e), 135, 16, 4f, 1.4f);
                }
                Burst(Pos, 135, 24, 5f, 1.5f);
            }
        }

        /// <summary>A hive letting out bees at the enemies near; its honey slows what comes close.</summary>
        sealed class Hive : MageThing
        {
            public Hive() { Life = 300; }
            public override void Tick()
            {
                if (Age % 15 == 0)
                {
                    var e = Nearest(Pos, 14f);
                    var to = e != null ? Centre(e) : Pos + Random.insideUnitCircle * 4f;
                    Bolt(Def, Age % 60 == 0 ? ProjKind.GiantBee : ProjKind.Bee, Pos + Vector2.up * 0.4f, At(Pos, to, 5f), 1f);
                }
                if (Age % 20 == 0) foreach (var e in Near(Pos, 3.5f, false)) MageMech.Slow(e, 0.5f, 0.5f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float grow = Mathf.Clamp01(Age / 15f);
                Sprite(fx, "Item_1124", px, Color.white, 0f, 2f * grow);
                if (Age % 4 == 0) MageMech.Dust(Pos + Random.insideUnitCircle * 3f * MageMech.Block, 152, Vector2.down * 0.3f, 1.1f);
            }
        }

        /// <summary>A whirl drawing the enemies near into it (not bosses or elites), hurting what spins in it.</summary>
        sealed class Whirl : MageThing
        {
            public int DustType;
            public Whirl() { Life = 240; Hurts = true; HitEvery = 20; Size = new Vector2(3f, 5f) * MageMech.Block; }
            public override void Tick()
            {
                foreach (var e in Near(Pos, 9f, false)) MageMech.Pull(e, Pos, 6f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 4; i++)
                {
                    float h = Random.Range(-2.5f, 2.5f), a = Age * 0.25f + i * Mathf.PI * 0.5f, r = (1.6f + h * 0.3f) * MageMech.Block;
                    MageMech.Dust(Pos + new Vector2(Mathf.Cos(a) * r, h * MageMech.Block), DustType, new Vector2(-Mathf.Sin(a), 0.2f) * 2.5f, 1.4f);
                }
            }
        }

        /// <summary>A great flower shedding its petals one by one, each after a different enemy.</summary>
        sealed class Blossom : MageThing
        {
            int target;
            public Blossom() { Life = 180; }
            public override void Tick()
            {
                if (Age % 8 != 0) return;
                var near = Near(Pos, 18f);
                var to = near.Count > 0 ? Centre(near[target++ % near.Count]) : Pos + Random.insideUnitCircle * 5f;
                float a = Random.value * Mathf.PI * 2f;
                Bolt(Def, ProjKind.PetalstormPetal, Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.2f * MageMech.Block, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3f, 0.55f,
                    new RangedShot { Point = to, Seek = 10f, SeekFrom = 6, PointSpeed = 6f });
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                float grow = Mathf.Clamp01(Age / 20f), fade = Mathf.Clamp01((Life - Age) / 20f);
                // Two rings of the Petalstorm's own petals, narrow ends in (the petal lies thick end up-left), turning slowly.
                for (int ring = 0; ring < 2; ring++)
                {
                    int n = ring == 0 ? 8 : 6;
                    for (int i = 0; i < n; i++)
                    {
                        float a = Age * (ring == 0 ? 0.02f : -0.03f) + (i + ring * 0.5f) * Mathf.PI * 2f / n;
                        var at = px + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (ring == 0 ? 22f : 10f) * grow;
                        Anim(fx, "Projectile_1116", 8, Age / 8 + i, at, new Color(1f, 1f, 1f, fade), a + Mathf.PI * 0.75f, (ring == 0 ? 2.2f : 1.5f) * grow);
                    }
                }
                FxBatch.Instance?.Light(Pos, new Color(1f, 0.6f, 0.9f) * 0.6f, 4f);
            }
        }

        /// <summary>A pillar of fire that sweeps along after the cursor.</summary>
        sealed class FirePillar : MageThing
        {
            public FirePillar() { Life = 180; Hurts = true; HitEvery = 15; Size = new Vector2(1.6f, 5f) * MageMech.Block; }
            public override void Tick()
            {
                var aim = BladeSkills.MageAim();
                float dx = Mathf.Clamp(aim.x - Pos.x, -0.35f * MageMech.Block, 0.35f * MageMech.Block);
                Vel = new Vector2(dx, 0f);
                var floor = Ground(Pos + Vector2.up * 2f * MageMech.Block, 8f);
                Pos.y = floor.y + 2.5f * MageMech.Block;
            }
            public override void Hit(HealthManager hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 180);
            public override void Draw(FxBatch fx, Vector2 px)
            {
                for (int i = 0; i < 5; i++)
                    MageMech.Dust(Pos + new Vector2(Random.Range(-0.7f, 0.7f), Random.Range(-2.5f, 2.5f)) * MageMech.Block, 6, Vector2.up * Random.Range(2f, 5f), Random.Range(1.3f, 2f));
                FxBatch.Instance?.Light(Pos, Fire, 5f);
            }
        }

        /// <summary>A weather front: thunder, hail and heat in turn, once a second on every enemy near.</summary>
        sealed class WeatherFront : MageThing
        {
            public WeatherFront() { Life = 180; }
            public override void Tick()
            {
                if (Age % 30 != 1) return;
                int which = Age / 60 % 3 == 0 ? 1 : Age / 60 % 3 == 1 ? 0 : 2;
                foreach (var e in Near(Pos, 10f, false)) Weather(Def, e, Pos + Vector2.up * 6f * MageMech.Block, which, 0.9f);
                Sound(which == 1 ? "Item_122" : which == 0 ? "Item_27" : "Item_20", 0.5f);
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                int which = Age / 60 % 3;
                int dust = which == 0 ? 226 : which == 1 ? 135 : 6;
                for (int i = 0; i < 3; i++)
                    MageMech.Dust(Pos + new Vector2(Random.Range(-10f, 10f), Random.Range(4f, 7f)) * MageMech.Block, dust, Vector2.down * Random.Range(3f, 6f), 1.2f);
            }
        }

        /// <summary>
        /// The Magic Missile's art: a great missile going from enemy to enemy, striking each; after each strike it carries
        /// on, curving, a moment before it turns for the next (so with one enemy alone it loops round and comes back), and
        /// a line of light is left between each two strikes.
        /// </summary>
        sealed class ChainMissile : MageThing
        {
            public Vector2 Aim;
            HealthManager prey, last;
            Vector2 lastHit;
            int strikes, coast;
            bool struck;
            public ChainMissile() { Life = 420; }
            public override void Tick()
            {
                float speed = 0.5f * MageMech.Block;
                if (Vel.sqrMagnitude < 1e-6f) Vel = At(Pos, Aim, speed);
                if (coast > 0)
                {
                    // Flying on past what it struck, curving round.
                    coast--;
                    Vel = Rotate(Vel, 9f);
                }
                else
                {
                    if (prey == null || prey.isDead || !prey.gameObject.activeInHierarchy) prey = Next();
                    var to = prey != null ? Centre(prey) : Aim;
                    Vel = (Vector2)Vector3.RotateTowards(Vel, (to - Pos).normalized * speed, 12f * Mathf.Deg2Rad, 0f);
                    if (prey == null && Vector2.Distance(Pos, Aim) < 0.8f * MageMech.Block) { Done = true; return; }
                    if (prey != null && MageMech.EnemiesNear(Pos, 0.7f * MageMech.Block, false).Contains(prey)) Strike();
                }
                Vel = Vel.normalized * speed;
                if (Age % 2 == 0) MageMech.Dust(Pos, 15, Vector2.zero, 1.4f);
            }
            void Strike()
            {
                MageMech.Strike(Def, prey, Damage);
                Burst(Pos, 15, 14, 3.5f, 1.4f);
                Sound("Item_10", 0.45f, 1.2f);
                // The line of light from the strike before.
                if (struck)
                {
                    float len = Vector2.Distance(lastHit, Pos);
                    int n = Mathf.Clamp(Mathf.CeilToInt(len / MageMech.Block), 1, 24);
                    for (int i = 0; i <= n; i++)
                        MageMech.Add(new Ribbon { Def = Def, Pos = Vector2.Lerp(lastHit, Pos, i / (float)n), Key = "missile_line", DustType = 15, Life = 120, Damage = MageMech.DamageOf(Def, 0.3f) }, 90);
                    DustLine(lastHit, Pos, 15, 0.5f, 1.2f, 0.1f);
                }
                struck = true;
                lastHit = Pos;
                last = prey;
                prey = null;
                coast = 10;
                if (++strikes >= 6) Done = true;
            }
            /// <summary>Another enemy near (not the one just struck); none, the one just struck again.</summary>
            HealthManager Next()
            {
                foreach (var e in Near(Pos, 18f, false)) if (e != last) return e;
                return last != null && !last.isDead && last.gameObject.activeInHierarchy ? last : null;
            }
            public override void Draw(FxBatch fx, Vector2 px)
            {
                Sprite(fx, "Projectile_16", px, Color.white, Age * 0.25f, 1.5f);
                fx.Light(px, new Color(0.5f, 0.6f, 1f), 3f);
            }
        }

        /// <summary>One of a ring of scythes spinning in on a point.</summary>
        sealed class ScytheRing : MageThing
        {
            public Vector2 Centre_;
            public float Angle;
            public ScytheRing() { Life = 90; Hurts = true; HitEvery = 12; Size = new Vector2(1.2f, 1.2f) * MageMech.Block; }
            public override void Tick()
            {
                Angle += 7f;
                float r = Mathf.Lerp(6f, 0.6f, Age / (float)Life) * MageMech.Block;
                float a = Angle * Mathf.Deg2Rad;
                Pos = Centre_ + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                Vel = Vector2.zero;
                if (Age % 3 == 0) MageMech.Dust(Pos, 27, Vector2.zero, 1.2f);
            }
            public override void Draw(FxBatch fx, Vector2 px) => Sprite(fx, "Projectile_45", px, Color.white, Age * 0.4f, 1f);
        }

        /// <summary>A skull's bite held on an enemy: a gnaw every half second.</summary>
        sealed class Bite : MageThing
        {
            HealthManager on;
            float k;
            public static void On(ItemDef def, HealthManager hm, float seconds, float k)
            {
                if (hm == null || hm.isDead) return;
                foreach (var t in MageMech.Things("bite")) if (t is Bite b && b.on == hm) { b.Life = b.Age + Mathf.RoundToInt(seconds * 60f); return; }
                MageMech.Add(new Bite { Def = def, on = hm, k = k, Life = Mathf.RoundToInt(seconds * 60f), Pos = Centre(hm), Key = "bite" }, 12);
            }
            public override void Tick()
            {
                if (on == null || on.isDead) { Done = true; return; }
                Pos = Centre(on) + Vector2.up * 0.3f;
                if (Age % 30 != 29) return;
                MageMech.Strike(Def, on, MageMech.DamageOf(Def, k));
                Burst(Pos, 5, 4, 1.5f);
            }
            public override void Draw(FxBatch fx, Vector2 px) => Anim(fx, "Projectile_837", 3, Age / 6, px, new Color(1f, 1f, 1f, 0.8f), Mathf.Sin(Age * 0.5f) * 0.3f, 0.8f);
        }
    }
}
