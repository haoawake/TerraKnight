using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The yoyos (aiStyle 99, AI_099_2_Yoyos, accarmor yoyo spec-yoyo.md Y.1; Terraria's space,
    /// px y down): out on their string to the cursor within their range, back when let go or when
    /// their time is up; their bounces, procs and children (Cascade's flame jet, Chik's crystals,
    /// the Kraken's waves, the Eye's lunge, the Terrarian's orbs, Hive-Five's bees); the Yoyo
    /// Glove's second yoyo, the counterweights (AI_099_1) and the Magic String's loose copies.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        /// <summary>A yoyo's numbers (ProjectileID.Sets.Yoyos*, P:6015-6072): life in seconds (0 = for ever), range px, top speed px/update, scale; its look.</summary>
        sealed class YoyoInfo
        {
            public ItemType Item;
            public int Tex;
            public float Life, Range, Speed, Scale = 1f;
            /// <summary>GetAlpha's colour (unlit); default: lit.</summary>
            public Color? Look;
        }

        static readonly Dictionary<ProjKind, YoyoInfo> yoyos = new Dictionary<ProjKind, YoyoInfo>
        {
            [ProjKind.WoodenYoyo] = new YoyoInfo { Item = ItemType.WoodYoyo, Tex = 541, Life = 3, Range = 130, Speed = 9 },
            [ProjKind.RallyYoyo] = new YoyoInfo { Item = ItemType.Rally, Tex = 548, Life = 5, Range = 170, Speed = 11 },
            [ProjKind.MalaiseYoyo] = new YoyoInfo { Item = ItemType.CorruptYoyo, Tex = 542, Life = 7, Range = 195, Speed = 12.5f },
            [ProjKind.ArteryYoyo] = new YoyoInfo { Item = ItemType.CrimsonYoyo, Tex = 543, Life = 6, Range = 207, Speed = 12 },
            [ProjKind.AmazonYoyo] = new YoyoInfo { Item = ItemType.JungleYoyo, Tex = 544, Life = 8, Range = 215, Speed = 13 },
            [ProjKind.Code1Yoyo] = new YoyoInfo { Item = ItemType.Code1, Tex = 534, Life = 9, Range = 220, Speed = 13 },
            [ProjKind.HiveFiveYoyo] = new YoyoInfo { Item = ItemType.HiveFive, Tex = 999, Life = 11, Range = 225, Speed = 14 },
            [ProjKind.ValorYoyo] = new YoyoInfo { Item = ItemType.Valor, Tex = 564, Life = 11, Range = 225, Speed = 14, Scale = 1.075f },
            [ProjKind.CascadeYoyo] = new YoyoInfo { Item = ItemType.Cascade, Tex = 545, Life = 13, Range = 235, Speed = 14 },
            [ProjKind.ChikYoyo] = new YoyoInfo { Item = ItemType.Chik, Tex = 546, Life = 16, Range = 275, Speed = 15, Look = FxBatch.C(255, 200, 255, 200) },
            [ProjKind.AmarokYoyo] = new YoyoInfo { Item = ItemType.Amarok, Tex = 552, Life = 15, Range = 270, Speed = 14 },
            [ProjKind.HelFireYoyo] = new YoyoInfo { Item = ItemType.HelFire, Tex = 553, Life = 12, Range = 330, Speed = 15, Look = FxBatch.C(255, 255, 200, 200) },
            [ProjKind.GradientYoyo] = new YoyoInfo { Item = ItemType.Gradient, Tex = 563, Life = 30, Range = 250, Speed = 16, Scale = 1.05f },
            [ProjKind.FormatCYoyo] = new YoyoInfo { Item = ItemType.FormatC, Tex = 562, Life = 16, Range = 320, Speed = 17 },
            [ProjKind.Code2Yoyo] = new YoyoInfo { Item = ItemType.Code2, Tex = 547, Life = 0, Range = 280, Speed = 17, Scale = 1.1f },
            [ProjKind.YeletsYoyo] = new YoyoInfo { Item = ItemType.Yelets, Tex = 549, Life = 14, Range = 290, Speed = 16 },
            [ProjKind.RedsThrowYoyo] = new YoyoInfo { Item = ItemType.RedsYoyo, Tex = 550, Life = 0, Range = 370, Speed = 16, Scale = 1.1f },
            [ProjKind.ValkyrieYoyo] = new YoyoInfo { Item = ItemType.ValkyrieYoyo, Tex = 551, Life = 0, Range = 370, Speed = 16, Scale = 1.1f },
            [ProjKind.EyeYoyo] = new YoyoInfo { Item = ItemType.TheEyeOfCthulhu, Tex = 555, Life = 0, Range = 360, Speed = 16.5f, Scale = 1.15f },
            [ProjKind.KrakenYoyo] = new YoyoInfo { Item = ItemType.Kraken, Tex = 554, Life = 0, Range = 380, Speed = 22, Scale = 1.2f, Look = FxBatch.C(200, 200, 200, 200) },
            [ProjKind.TerrarianYoyo] = new YoyoInfo { Item = ItemType.Terrarian, Tex = 603, Life = 0, Range = 400, Speed = 17.5f, Scale = 1.15f, Look = FxBatch.C(255, 255, 255, 200) },
        };

        static readonly ProjKind[] counterweights =
        {
            ProjKind.CounterweightBlack, ProjKind.CounterweightBlue, ProjKind.CounterweightGreen, ProjKind.CounterweightPurple, ProjKind.CounterweightRed,
            ProjKind.CounterweightYellow, ProjKind.CounterweightStrung,
        };

        static void RegisterYoyos()
        {
            // DefaultToYoyo (P:10766-10773): through everything, melee, the NPC's own 10 ticks; 16×16 × scale; the timer held at 6.
            foreach (var kv in yoyos)
            {
                var y = kv.Value;
                specs[kv.Key] = new Spec
                {
                    Ai = Ai.Custom, Texture = "Projectile_" + y.Tex, W = 16, H = 16, Scale = y.Scale, Time = 6, Penetrate = -1, PushFromPlayer = true, Held = true, TrailLength = 10,
                    TypeCooldown = kv.Key == ProjKind.TerrarianYoyo ? 10 : 0,
                    HoldsWhile = p => p.Ai0 != -2f, CanDamage = p => p.Ai0 != -3f,
                    Update = (t, p) => t.StepYoyo(p), Draw = (t, fx, p, px) => t.DrawYoyo(fx, p, px), OnKill = (t, p) => { },
                    TileHit = (t, p, x, yy, v) => YoyoTile(t, p, x, yy, v), ModifyHit = (t, p, hm, d, c) => t.YoyoBeforeHit(p, hm, d, c), OnHit = (t, p, hm) => t.YoyoAfterHit(p, hm),
                    LandsOnPlatforms = p => p.Ai0 == -2f,
                };
            }
            // P:6086-6095: the counterweights, 10×10, two updates a tick.
            for (int i = 0; i < counterweights.Length; i++)
                specs[counterweights[i]] = new Spec
                {
                    Ai = Ai.Custom, Texture = "Projectile_" + (i < 6 ? 556 + i : 1079), W = 10, H = 10, Time = 6, Penetrate = -1, Extra = 1, PushFromPlayer = true,
                    CanDamage = p => p.Ai0 != -3f, Update = (t, p) => t.StepCounterweight(p), Draw = (t, fx, p, px) => t.DrawCounterweight(fx, p, px), OnKill = (t, p) => { },
                    TileHit = (t, p, x, yy, v) => CounterweightTile(p, x, yy, v), ModifyHit = (t, p, hm, d, c) => t.YoyoBeforeHit(p, hm, d, c),
                    LandsOnPlatforms = p => p.Ai0 == -2f,
                };
            // P:10530-10544: Cascade's flame jet, unseen, a 200·scale px line, four enemies once each.
            specs[ProjKind.CascadeBurst] = new Spec
            {
                Ai = Ai.Custom, W = 32, H = 32, Time = 600, Penetrate = 4, OncePerNpc = true, TileCollide = false, PushFromPlayer = true, Look = Look.Invisible,
                Beam = p => (CenterPx(p), CenterPx(p) + (TV(p).sqrMagnitude > 1e-6f ? TV(p).normalized : new Vector2(0f, -1f)) * 200f * p.Ai1, 22f * p.Ai1),
                CanDamage = p => p.Ai0 >= 0f, Update = (t, p) => t.StepCascadeBurst(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => { },
                OnHit = (t, p, hm) => { if (Random.Range(0, 3) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, Random.Range(60, 240)); },
            };
            // P:10638-10653: Chik's crystal (aiStyle 24), shrinking away; each enemy once in 20 ticks by any of them.
            specs[ProjKind.ChikCrystal] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1134", W = 6, H = 6, Scale = 1.2f, Time = 600, Penetrate = 1, TypeCooldown = 20, AlphaStart = 50f / 255f, TileCollide = false,
                Update = (t, p) => StepChikCrystal(p), Draw = (t, fx, p, px) => DrawChikCrystal(fx, p, px), OnKill = (t, p) => { },
            };
            // P:10655-10668: the Kraken's wave (aiStyle 210), 100×100, 10 ticks for all of them.
            specs[ProjKind.KrakenWave] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1135", W = 100, H = 100, Time = 600, Penetrate = -1, TypeCooldown = 10, TileCollide = false, AlphaStart = 1f,
                Update = (t, p) => t.StepWave(p), Draw = (t, fx, p, px) => DrawWave(fx, p, px, false), OnKill = (t, p) => { },
            };
            // P:10599-10612: the Eye's lunge (aiStyle 210), 100×100, each enemy once, never a crit.
            specs[ProjKind.EyeLunge] = new Spec
            {
                Ai = Ai.Custom, Texture = "NPC_4", W = 100, H = 100, Time = 600, Penetrate = -1, OncePerNpc = true, TileCollide = false, AlphaStart = 1f, PushFromPlayer = true,
                ModifyHit = (t, p, hm, d, c) => (d, 0f), Update = (t, p) => t.StepWave(p), Draw = (t, fx, p, px) => DrawWave(fx, p, px, true), OnKill = (t, p) => { },
            };
            // P:6073-6085: the Terrarian's orb (aiStyle 115), one hit and it fades; all of them share 10 ticks.
            specs[ProjKind.TerrarianOrb] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_604", W = 14, H = 14, Scale = 1.2f, Time = 600, Penetrate = -1, TypeCooldown = 10, PushFromPlayer = true,
                CanDamage = p => p.Ai1 < 1000f, Update = (t, p) => StepTerrarianOrb(p), Draw = (t, fx, p, px) => DrawTerrarianOrb(fx, p, px), OnKill = (t, p) => { },
                TileHit = (t, p, x, yy, v) => { if (x) p.Vel.x = -v.x; if (yy) p.Vel.y = -v.y; },
                OnHit = (t, p, hm) => { t.Counterweight(EnemyCenterPx(hm), p.Damage, p.Knockback); p.Ai1 = 1000f; },
            };
        }

        /// <summary>Dev: the farthest live one of a kind from a point (Terraria px), -1 when none; how many are loose copies.</summary>
        public float DevFarthest(ProjKind kind, Vector2 fromWorld)
        {
            float best = -1f;
            var from = TerraParticles.ToPx(fromWorld);
            foreach (var q in live) if (q.Kind == kind && !q.Dead) best = Mathf.Max(best, Vector2.Distance(CenterPx(q), from));
            return best;
        }

        public int DevLoose(ProjKind kind)
        {
            int n = 0;
            foreach (var q in live) if (q.Kind == kind && !q.Dead && q.Ai0 == -2f) n++;
            return n;
        }

        public int DevReturning(ProjKind kind)
        {
            int n = 0;
            foreach (var q in live) if (q.Kind == kind && !q.Dead && (q.Ai0 == -1f || q.Ai0 == -3f)) n++;
            return n;
        }

        static bool IsYoyo(ProjKind k) => yoyos.ContainsKey(k);
        static bool IsCounterweight(ProjKind k) => System.Array.IndexOf(counterweights, k) >= 0;

        // ------------------------------------------------------------------ the hit counter (Player.yoyoHitCounter, never reset)

        static int yoyoHitCounter = -1;

        /// <summary>Player.EverySomeYoyoHits (Pl:4293-4301).</summary>
        static bool EverySomeYoyoHits(int n, bool countUp)
        {
            bool r = yoyoHitCounter % n == 0;
            if (countUp) yoyoHitCounter++;
            return r;
        }

        /// <summary>meleeSpeed (the use-time multiplier) of the yoyo's item.</summary>
        static float YoyoMeleeSpeed(Proj p) => Equipment.UseScale(ItemDb.Get(p.Item));

        // ------------------------------------------------------------------ AI_099_2_Yoyos (P:68786-69162)

        /// <summary>A yoyo of the same kind further up the list still out or coming back: this one is the glove's second (P:68788-68795).</summary>
        bool SecondYoyo(Proj p)
        {
            foreach (var q in live)
            {
                if (q == p) return false;
                if (q.Kind == p.Kind && !q.Dead && q.Ai0 >= -1f) return true;
            }
            return false;
        }

        void StepYoyo(Proj p)
        {
            var mod = TerraMod.Instance;
            var info = yoyos[p.Kind];
            if (mod == null || !Holder(out var owner))
            {
                Kill(p);
                return;
            }
            // Thrown by the item (StartChanneling): its item, the button latched.
            if (p.Item == ItemType.None)
            {
                p.Item = info.Item;
                p.Channel = true;
                p.Melee = true;
            }
            var b = Equipment.Current;
            bool secondary = SecondYoyo(p);
            float ms = YoyoMeleeSpeed(p);
            // The clock: a second yoyo ages about three times as fast; strings make it last half as long again.
            p.LocalAi0 += 1f;
            if (secondary) p.LocalAi0 += Random.Range(10, 31) * 0.1f;
            float seconds = p.LocalAi0 / 60f / ((1f + ms) / 2f);
            float life = info.Life > 0f ? info.Life : -1f;
            if (b.YoyoString && life > 0f) life *= 1.5f;
            if (p.Ai0 <= -2f) life = -1f;
            if (life > 0f && seconds > life)
            {
                foreach (var q in live)
                    if (!q.Dead && (IsYoyo(q.Kind) || IsCounterweight(q.Kind)) && q.Ai0 >= 0f) q.Ai0 = -1f;
                p.Ai0 = -1f;
            }
            if (p.Kind == ProjKind.TerrarianYoyo) TerrarianOrbs(p);
            p.Channel &= Channel(p.Item);
            var c = CenterPx(p);
            // The primary yoyo keeps the item in use and the player facing it (P:68884-68903).
            if (!secondary && p.Ai0 != -2f)
            {
                float dx = c.x - owner.x;
                var toward = c - owner;
                if (Mathf.Abs(dx) <= 2f) toward.x = 0f;
                if (p.Ai0 != -3f) mod.KeepUsing(ItemDb.Get(p.Item), Flip(toward));
                else mod.KeepUsing(ItemDb.Get(p.Item), Vector2.zero);
            }
            p.TimeLeft = 6;
            float R = info.Range, V = info.Speed;
            if (p.Kind == ProjKind.KrakenYoyo && secondary) V *= 0.75f;
            if (p.Kind == ProjKind.CascadeYoyo && Random.Range(0, 6) == 0)
            {
                int d = DustBox(p, 6, Vector2.zero, 0, 1f, true);
            }
            if (p.Kind == ProjKind.HelFireYoyo && Random.Range(0, 2) == 0)
            {
                int d = DustBox(p, 6, Vector2.zero, 0, 1.6f, true);
            }
            if (b.YoyoString) R = R * 1.25f + 30f;
            R /= (1f + ms * 3f) / 4f;
            V /= (1f + ms * 3f) / 4f;
            float inertia = Mathf.Max(1f, 14f - V / 2f);
            float arrive = 5f + V / 2f + (secondary ? 20f : 0f);
            var v = TV(p);
            if (p.Ai0 >= 0f)
            {
                if (v.magnitude > V) v *= 0.98f;
                var d = owner - c;
                bool far = d.magnitude > R, tooFar = d.magnitude > R * 1.3f;
                if (!p.Channel || !GameHooks.PlayerHasControl())
                {
                    p.Ai0 = -1f;
                }
                else
                {
                    var m = AimPx();
                    if ((m - owner).magnitude > R) m = owner + (m - owner).normalized * R;
                    if (m != p.Point)
                    {
                        if ((m - owner).magnitude > R - 1f) m = owner + (m - owner).normalized * (R - 1f);
                        p.Point = m;
                    }
                }
                if (tooFar) p.Ai0 = -1f;
                if (p.Ai0 >= 0f)
                {
                    if (far)
                    {
                        inertia /= 2f;
                        V *= 2f;
                        if ((c.x > owner.x && v.x > 0f) || (c.x < owner.x && v.x < 0f)) v.x *= 0.5f;
                        if ((c.y > owner.y && v.y > 0f) || (c.y < owner.y && v.y < 0f)) v.y *= 0.5f;
                        inertia = 1f;
                    }
                    var to = p.Point - c;
                    float dist = to.magnitude;
                    if (dist > arrive)
                    {
                        float s = Mathf.Min(dist / 2f, V);
                        if (far) s = Mathf.Min(s, V / 2f);
                        v = (v * (inertia - 1f) + to.normalized * s) / inertia;
                    }
                    else if (secondary)
                    {
                        if (v.magnitude < V * 0.6f) v = (v * (inertia - 1f) + Norm(v) * V * 0.6f) / inertia;
                    }
                    else v *= 0.8f;
                    if (secondary && !far && v.magnitude < V * 0.6f) v = Norm(v) * V * 0.6f;
                }
            }
            // The Magic String: coming back, the yoyo leaves a loose copy behind (P:69080-69088).
            if (p.Ai0 == -1f && b.MagicString)
            {
                p.Ai0 = -3f;
                var keep = SpawnClass;
                SpawnClass = DamageClass.Melee;
                var copy = Spawn(p.Kind, p.Pos, Flip(v), (int)(p.Damage * 0.75f), (int)(p.Knockback * 0.75f), p.Crit);
                SpawnClass = keep;
                if (copy != null)
                {
                    copy.Ai0 = -2f;
                    copy.Item = p.Item;
                    copy.Melee = true;
                }
            }
            if (p.Ai0 == -3f) p.Alpha = 1f;
            if (p.Ai0 == -2f)
            {
                // The loose copy rolls about for 3-6 hits.
                if (p.Penetrate < 0) p.Penetrate = Random.Range(3, 7);
                v.y += 0.3f;
                if (v.magnitude > 16f) v = v.normalized * 16f;
                else if (v.y <= 3f)
                {
                    V = Mathf.Min(V, 16f);
                    if (Mathf.Abs(v.x) < 0.1f)
                    {
                        float side = c.x < owner.x ? -1f : 1f;
                        v.x = 0.1f * side;
                        p.Dir = side;
                    }
                    else if (v.x > 0f && v.x < V) v.x += 0.1f;
                    else if (v.x < 0f && v.x > -V) v.x -= 0.1f;
                }
                p.Collide = true;
            }
            else if (p.Ai0 < 0f)
            {
                inertia *= 0.8f;
                V *= 1.5f;
                p.Collide = false;
                var d = owner - c;
                if (d.magnitude < V + 10f || d.magnitude == 0f || d.magnitude > 2000f)
                {
                    Kill(p);
                    return;
                }
                v = (v * (inertia - 1f) + d.normalized * V) / inertia;
            }
            else p.Collide = true;
            p.TRot += p.Ai0 == -2f || p.Kind == ProjKind.KrakenYoyo ? 0.45f * p.Dir : 0.45f;
            p.Vel = Flip(v);
        }

        static Vector2 Norm(Vector2 v) => v.sqrMagnitude > 1e-8f ? v.normalized : Vector2.right;

        /// <summary>
        /// Out, a yoyo meeting a block just stops along that axis (Collision.TileCollision); the loose
        /// copy breaks against a wall and bounces off the floor (P:18512-18521).
        /// </summary>
        static void YoyoTile(TerraProjectiles t, Proj p, bool hitX, bool hitY, Vector2 oldVel)
        {
            if (p.Ai0 == -2f)
            {
                if (hitX)
                {
                    Sound("Dig_0", 0.6f);
                    for (int i = 0; i < 5; i++) DustBox(p, 1, Vector2.zero, 0, 1f, false);
                    t.Kill(p);
                    return;
                }
                // Landing faster than 4 px a tick it bounces back up at 0.6 (y up here).
                if (hitY) p.Vel.y = oldVel.y < -4f ? -oldVel.y * 0.6f : 0f;
                return;
            }
            if (hitX) p.Vel.x = 0f;
            if (hitY) p.Vel.y = 0f;
        }

        // ------------------------------------------------------------------ the hit (P:13290-13335, 13425-13438, 12994-13004)

        (int, float) YoyoBeforeHit(Proj p, HealthManager hm, int dmg, float crit)
        {
            var c = CenterPx(p);
            var e = EnemyCenterPx(hm);
            var v = TV(p);
            if (p.Ai0 == -2f)
            {
                // The loose copy hops up off what it hits.
                float s = v.magnitude;
                v = Norm(v) * s / 5f;
                v.y -= Random.Range(50, 81) * 0.1f;
                v.y = Mathf.Max(v.y, -16f);
                p.Vel = Flip(v);
            }
            else
            {
                Counterweight(e, p.Damage, p.Knockback);
                if (p.Ai0 >= 0f && IsYoyo(p.Kind))
                {
                    var bounce = Norm(c - e) * (p.Kind == ProjKind.KrakenYoyo ? 4f : 16f);
                    p.Vel = Flip(v * -0.5f + bounce);
                    p.LocalAi0 += 20f;
                    // No line back to the player: 40 ticks more off its time, a quarter off the damage.
                    if (Holder(out var owner) && !TerraWorld.LineClearPx(c, owner))
                    {
                        p.LocalAi0 += 40f;
                        dmg = (int)(dmg * 0.75f);
                    }
                }
            }
            if (p.Kind == ProjKind.EyeYoyo && EverySomeYoyoHits(7, false)) dmg *= 2;
            if (p.Kind == ProjKind.ChikYoyo && EverySomeYoyoHits(3, false)) dmg *= 2;
            if (p.Kind == ProjKind.FormatCYoyo)
            {
                // Format:C rolls the crit itself: a crit lands a quarter harder, with its gold flash.
                bool isCrit = Random.value * 100f < crit;
                if (isCrit)
                {
                    dmg += (int)(dmg * 0.25f);
                    FormatCFlash(e + Norm(c - e) * -2f, v);
                }
                crit = isCrit ? 100f : 0f;
            }
            return (dmg, crit);
        }

        /// <summary>The yoyos' debuffs (StatusNPC, P:11444-11464) and their counted procs (P:13727-13869).</summary>
        void YoyoAfterHit(Proj p, HealthManager hm)
        {
            switch (p.Kind)
            {
                case ProjKind.CascadeYoyo: if (Random.Range(0, 3) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, Random.Range(60, 240)); break;
                case ProjKind.HelFireYoyo: EnemyDebuffs.Add(hm, EnemyDebuffs.Hellfire, Random.Range(180, 480)); break;
                case ProjKind.YeletsYoyo: EnemyDebuffs.Add(hm, EnemyDebuffs.Venom, 120); break;
                case ProjKind.AmarokYoyo: if (Random.Range(0, 3) != 0) EnemyDebuffs.Add(hm, EnemyDebuffs.Frostbite, Random.Range(120, 320)); break;
            }
            if (hm.isDead) return;
            var c = CenterPx(p);
            var e = EnemyCenterPx(hm);
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            if (p.Kind == ProjKind.HiveFiveYoyo && Random.Range(0, 3) == 0)
            {
                // Hive-Five's bee (beeType / beeDamage / beeKB, Pl:12504-12531), half the damage.
                var v = Norm(c - e) * 0.25f;
                bool strong = Equipment.Current.StrongBees && Random.Range(0, 2) == 0;
                int d = p.Damage / 2 + (strong ? Random.Range(1, 4) : Random.Range(0, 2));
                var bee = Spawn(strong ? ProjKind.GiantBee : ProjKind.Bee, p.Pos, Flip(v), d, strong ? 0.5f + p.Knockback * 1.1f : p.Knockback, p.Crit);
                if (bee != null) bee.Melee = true;
            }
            if (p.Kind == ProjKind.CascadeYoyo && p.Ai0 != -2f && EverySomeYoyoHits(2, true)) CascadeBurst(p, hm);
            if (p.Kind == ProjKind.KrakenYoyo && EverySomeYoyoHits(4, true))
            {
                var s = e - c;
                foreach (int l in new[] { -2, 0, 2 })
                {
                    var wv = RotateBy(s, l * Mathf.PI * 2f * 0.05f);
                    wv = (wv.sqrMagnitude > 1e-6f ? wv.normalized : Vector2.right) * (18f + 8f * Random.value);
                    var w = Spawn(ProjKind.KrakenWave, p.Pos, Flip(wv), (int)(p.Damage * 0.5f), p.Knockback, p.Crit);
                    if (w != null) w.Melee = true;
                }
            }
            if (p.Kind == ProjKind.ChikYoyo && EverySomeYoyoHits(3, true))
            {
                for (int m = 0; m < 5; m++)
                {
                    var cv = RotateBy(Random.insideUnitCircle * 8f, Random.value * Mathf.PI * 2f * 0.025f);
                    var cr = Spawn(ProjKind.ChikCrystal, p.Pos, Flip(cv), p.Damage, p.Knockback, p.Crit);
                    if (cr == null) continue;
                    cr.Ai0 = m != 0 ? 1f : 0f;
                    cr.Melee = true;
                }
                typeImmune[(ProjKind.ChikCrystal, hm.GetInstanceID())] = 20;
            }
            if (p.Kind == ProjKind.EyeYoyo && EverySomeYoyoHits(7, true)) EyeLunge(p, hm);
            SpawnClass = keep;
        }

        /// <summary>The nearest other chaseable enemy within range of a point (to the closest point of its box), and how far.</summary>
        HealthManager NearestOther(Vector2 fromPx, HealthManager not, float range, out float dist, out Vector2 at)
        {
            HealthManager best = null;
            dist = 0f;
            at = default;
            float bestD = range;
            foreach (var hm in AccessoryEffects.Enemies(TerraParticles.ToWorld(fromPx), range, accNear))
            {
                if (hm == not || !Chaseable(hm)) continue;
                var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                var close = col != null ? TerraParticles.ToPx(col.ClosestPoint(TerraParticles.ToWorld(fromPx))) : EnemyCenterPx(hm);
                float d = Vector2.Distance(close, fromPx);
                if (d >= bestD || !TerraWorld.LineClearPx(fromPx, close)) continue;
                bestD = d;
                best = hm;
                at = close;
            }
            if (best != null) dist = bestD;
            return best;
        }

        /// <summary>P:13762-13803: a flame jet at the nearest other enemy within 208 px, longer the further it is, twice the damage.</summary>
        void CascadeBurst(Proj p, HealthManager victim)
        {
            var c = CenterPx(p);
            var other = NearestOther(c, victim, 208f, out float dist, out var at);
            float a = other != null ? Mathf.Atan2(at.y - c.y, at.x - c.x) : Random.value * Mathf.PI * 2f;
            var v = RotateBy(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.1f, Random.Range(-1f, 1f) * Mathf.PI * 2f * 0.025f);
            var burst = Spawn(ProjKind.CascadeBurst, p.Pos, Flip(v), p.Damage * 2, p.Knockback, p.Crit);
            if (burst == null) return;
            burst.Ai1 = Mathf.Min(1.5f, Mathf.Max(0.22f + 0.22f * Random.value, dist / 200f));
            burst.Immune[victim.GetInstanceID()] = int.MaxValue;
            burst.Melee = true;
        }

        /// <summary>P:13826-13869: the Eye's servant lunging at the nearest other enemy within 80 px, twice the damage.</summary>
        void EyeLunge(Proj p, HealthManager victim)
        {
            var c = CenterPx(p);
            var other = NearestOther(c, victim, 80f, out _, out var at);
            float a = other != null ? Mathf.Atan2(at.y - c.y, at.x - c.x) : Random.value * Mathf.PI * 2f;
            var v = RotateBy(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 14f, Random.value * Mathf.PI * 2f * 0.025f);
            var lunge = Spawn(ProjKind.EyeLunge, p.Pos, Flip(v), p.Damage * 2, p.Knockback, 0f);
            if (lunge == null) return;
            lunge.Immune[victim.GetInstanceID()] = int.MaxValue;
            lunge.Melee = true;
        }

        /// <summary>PO:3703-3760 FormatC: a gold flash and a white one where the crit landed, and twelve sparks.</summary>
        static void FormatCFlash(Vector2 at, Vector2 v)
        {
            FxParticles.FormatCHit(at, v);
            for (int i = 0; i < 12; i++)
            {
                int d = PointDust(at, 213, Random.insideUnitCircle * 3f, 0, 1f);
                if (d < 0) continue;
                D(d).FadeIn = 1f;
                D(d).NoGravity = Random.Range(0, 3) == 0;
            }
        }

        // ------------------------------------------------------------------ the Yoyo Glove and the counterweights (Player.Counterweight, Pl:12444-12502)

        static ProjKind CounterweightKind(int code) =>
            code >= 1 && code <= 7 ? counterweights[code - 1] : code == 8 ? (Random.Range(0, 7) == 0 ? ProjKind.CounterweightStrung : counterweights[Random.Range(0, 6)]) : ProjKind.None;

        /// <summary>After a yoyo's hit: the glove's second yoyo first; else a counterweight, one per yoyo out.</summary>
        void Counterweight(Vector2 hitPx, int damage, float knockback)
        {
            var b = Equipment.Current;
            if (!b.YoyoGlove && b.CounterWeight <= 0) return;
            if (!Holder(out var owner)) return;
            int yoyosOut = 0, weights = 0;
            Proj last = null;
            foreach (var q in live)
            {
                if (q.Dead || q.Ai0 == -2f) continue;
                if (IsYoyo(q.Kind)) { yoyosOut++; last = q; }
                else if (IsCounterweight(q.Kind)) weights++;
            }
            var v = Norm(hitPx - owner) * 16f;
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            if (b.YoyoGlove && yoyosOut < 2)
            {
                if (last != null)
                {
                    var second = Spawn(last.Kind, TerraParticles.ToWorld(owner), Flip(v), last.Damage, last.Knockback, last.Crit);
                    if (second != null)
                    {
                        second.Item = last.Item;
                        second.Channel = true;
                        second.Melee = true;
                        second.Point = hitPx;
                    }
                }
            }
            else if (b.CounterWeight > 0 && weights < yoyosOut)
            {
                var kind = CounterweightKind(b.CounterWeight);
                var w = Spawn(kind, TerraParticles.ToWorld(owner), Flip(v), damage, (knockback + 6f) / 2f, 4f);
                if (w != null)
                {
                    w.Ai0 = weights > 0 ? 1f : 0f;
                    w.Melee = true;
                    w.Channel = true;
                }
            }
            SpawnClass = keep;
        }

        static Vector2 lastAimPx;
        static int lastAimTick = -1;
        static Vector2 aimDelta;

        /// <summary>
        /// AI_099_1_Counterweights (P:68432-68784): on a leash of 125 px round the player (the
        /// second closer), pushed against the mouse's motion, steering at an enemy near the player,
        /// 9-12 px an update; back when nothing of its yoyo is out.
        /// </summary>
        void StepCounterweight(Proj p)
        {
            if (!Holder(out var owner))
            {
                Kill(p);
                return;
            }
            var b = Equipment.Current;
            p.TimeLeft = 6;
            if (lastAimTick != armorTicks)
            {
                var aim = AimPx();
                aimDelta = lastAimTick < 0 ? Vector2.zero : aim - lastAimPx;
                lastAimPx = aim;
                lastAimTick = armorTicks;
            }
            float R = 125f;
            float vmin = 18f, vmax = 12f;
            if (p.Ai0 >= 0f)
            {
                bool any = false;
                foreach (var q in live) if (!q.Dead && IsYoyo(q.Kind) && q.Ai0 >= -1f) { any = true; break; }
                if (!any) p.Ai0 = -1f;
            }
            if (b.YoyoString) R += R * 0.25f + 10f;
            p.TRot += 0.5f;
            var c = CenterPx(p);
            var v = TV(p);
            if (p.Ai0 == 0f || p.Ai0 == 1f)
            {
                if (p.Ai0 == 1f) R *= 0.75f;
                vmin *= 0.5f;
                var d = owner - c;
                bool nearEdge = d.magnitude > R * 0.9f;
                if (d.magnitude > R)
                {
                    float over = d.magnitude - R;
                    c = owner - d.normalized * R;
                    float s = v.magnitude;
                    v = Norm(v) * (s - Mathf.Min(over, s - 1f));
                    var tang = new Vector2(Mathf.Abs(d.y) * Mathf.Sign(d.x), Mathf.Abs(d.x) * Mathf.Sign(d.y));
                    tang = Norm(tang) * v.magnitude;
                    if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
                    {
                        var w = Norm(v + new Vector2(0f, tang.y)) * v.magnitude;
                        v = Mathf.Abs(tang.x) < 0.1f || Mathf.Abs(tang.y) < 0.1f ? w : (w + v * 2f) / 3f;
                    }
                    else
                    {
                        var w = Norm(v + new Vector2(tang.x, 0f)) * v.magnitude;
                        v = Mathf.Abs(tang.x) < 0.2f || Mathf.Abs(tang.y) < 0.2f ? w : (w + v * 2f) / 3f;
                    }
                }
                if (p.Channel && UseHeldAny())
                {
                    var md = -aimDelta * 0.5f;
                    if (v != Vector2.zero)
                    {
                        if (nearEdge)
                        {
                            if (Mathf.Sign(md.x) == Mathf.Sign(c.x - owner.x)) md.x = 0f;
                            if (Mathf.Sign(md.y) == Mathf.Sign(c.y - owner.y)) md.y = 0f;
                        }
                        v += md * 0.1f;
                    }
                }
                else p.Ai0 = -1f;
                // The generic homing: the nearest enemy within 800 px it can see, near the player.
                HealthManager best = null;
                float bestD = 800f;
                foreach (var hm in AccessoryEffects.Enemies(TerraParticles.ToWorld(c), 800f, accNear))
                {
                    if (!Chaseable(hm)) continue;
                    var e = EnemyCenterPx(hm);
                    float dd = Mathf.Abs(e.x - c.x) + Mathf.Abs(e.y - c.y);
                    if (dd >= bestD || (e - owner).magnitude >= R * 0.9f || !TerraWorld.LineClearPx(c, e)) continue;
                    bestD = dd;
                    best = hm;
                }
                if (best != null) v = (v * 7f + Norm(EnemyCenterPx(best) - c) * 6f) / 8f;
                float sp = Mathf.Clamp(v.magnitude, vmin, vmax);
                v = Norm(v) * sp;
            }
            if (p.Ai0 == -1f && b.MagicString)
            {
                p.Ai0 = -3f;
                var copy = Spawn(p.Kind, TerraParticles.ToWorld(c), Flip(v), (int)(p.Damage * 0.75f), p.Knockback * 0.75f, p.Crit);
                if (copy != null) copy.Ai0 = -2f;
            }
            if (p.Ai0 == -3f) p.Alpha = 1f;
            if (p.Ai0 == -2f)
            {
                p.Extra = 0;
                if (p.Penetrate < 0) p.Penetrate = 1;
                v.y += 0.3f;
                if (v.magnitude > 16f) v = v.normalized * 16f;
                p.Collide = true;
            }
            else if (p.Ai0 < 0f)
            {
                p.Collide = false;
                var d = owner - c;
                if (d.magnitude < 40f || d.magnitude > 2000f)
                {
                    Kill(p);
                    return;
                }
                v = (v * 11f + d.normalized * 18f) / 12f;
            }
            else p.Collide = true;
            p.Pos = TerraParticles.ToWorld(c);
            p.Vel = Flip(v);
        }

        static bool UseHeldAny() => TerraMod.Instance != null && TerraMod.Instance.UseHeld;

        /// <summary>P:18527-18549: a counterweight bounces off blocks and turns a little back to the player.</summary>
        static void CounterweightTile(Proj p, bool hitX, bool hitY, Vector2 oldVel)
        {
            var v = Flip(oldVel);
            if (hitX) v.x = -v.x;
            if (hitY) v.y = -v.y;
            if (Holder(out var owner)) v = v * 0.75f + Norm(owner - CenterPx(p)) * v.magnitude * 0.25f;
            if (v.magnitude > 6f) v *= 0.5f;
            p.Vel = Flip(v);
        }

        // ------------------------------------------------------------------ the children

        /// <summary>AI_189 for 1125 (P:43035-43142): ten updates of fire and smoke along its jet.</summary>
        void StepCascadeBurst(Proj p)
        {
            var v = TV(p);
            var c = CenterPx(p);
            if (p.Ai0 == 0f) Sound("Item_14", 0.5f);
            p.Ai0++;
            if (++p.FrameCounter >= 2)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 5)
                {
                    Kill(p);
                    return;
                }
            }
            if (p.Ai0 == 1f)
            {
                for (int i = 0; i < 20; i++)
                {
                    var dv = RotateBy(v, Random.Range(-1f, 1f) * Mathf.PI / 4f * 0.3f) * (2f + 5f * Random.value);
                    PointDust(c + Random.insideUnitCircle * 8f, 6, dv, 200, 1f + Random.value);
                }
                int n = Random.Range(3, 7) / 3;
                for (int i = 0; i < n * 5; i++)
                {
                    var dv = RotateBy(v, Random.Range(-1f, 1f) * Mathf.PI / 4f * 0.3f) * (2f + 5f * Random.value) * 3f;
                    int d = PointDust(c + Random.insideUnitCircle * 8f, 31, dv, 50, 1f + Random.value);
                    if (d >= 0) D(d).NoGravity = true;
                }
                var pp = TerraParticles.Instance;
                for (int i = 0; i < n && pp != null; i++)
                    pp.NewGore(c + Random.insideUnitCircle * 8f - new Vector2(16f, 16f), RotateBy(v, Random.Range(-1f, 1f) * 0.24f) * 4f * Random.value * 1.7f, Random.Range(61, 64), exact: true);
            }
            if (Random.Range(0, 3) != 0)
            {
                int d = PointDust(c + Norm(v) * 200f * p.Ai1 * Random.value, 6, Vector2.zero, 90, 2.5f);
                if (d >= 0)
                {
                    D(d).NoGravity = true;
                    D(d).FadeIn = 1f;
                    if (Random.Range(0, 2) == 0)
                    {
                        D(d).NoGravity = false;
                        D(d).FadeIn = 0.4f;
                        D(d).Scale *= 0.3f;
                    }
                    else D(d).Velocity = Norm(v) * v.magnitude * 0.25f;
                }
            }
            for (int k = 0; k < 2; k++)
                if (Random.Range(0, 2) == 0)
                {
                    var dv = RotateBy(v, Random.Range(-1f, 1f) * Mathf.PI / 4f * 0.5f) * (1f + 2f * Random.value);
                    int d = PointDust(c, 6, new Vector2(dv.x * 0.6f, dv.y * 1.5f), 200, 1f + Random.value);
                }
        }

        /// <summary>aiStyle 24 for 1134 (P:26274-26355): the shatter (the first of the five only), crystal dust, slowing, shrinking away.</summary>
        static void StepChikCrystal(Proj p)
        {
            var v = TV(p);
            if (p.LocalAi0 == 0f)
            {
                p.LocalAi0 = 1f;
                if (p.Ai0 == 0f) Sound("Shatter", 0.35f);
                for (int i = 0; i < 6; i++)
                {
                    int d = DustBox(p, 70, Vector2.zero, 0, 1f, true);
                    if (d < 0) continue;
                    D(d).Velocity *= 2.5f;
                    D(d).Scale *= 0.9f;
                }
            }
            float l = p.Scale * 0.5f;
            FxBatch.Instance?.Light(CenterPx(p), new Color(0.5f * l, 0.2f * l, 0.9f * l), 2f);
            p.TRot += v.x * 0.2f;
            p.Ai1++;
            v *= 0.96f;
            p.Vel = Flip(v);
            if (p.Ai1 > 15f)
            {
                p.Scale -= 0.05f;
                if (p.Scale <= 0.2f) p.Dead = true;
            }
        }

        static void DrawChikCrystal(FxBatch fx, Proj p, Vector2 px)
        {
            float k = 1f - p.Alpha;
            fx.Draw(Sprite(p.Spec.Texture), px, new Color(k, k, k, 0f), p.TRot, p.Scale);
        }

        /// <summary>AI_210 for 1135 and 1130 (P:37483-37571): slowing ×0.92, in and out over 20 updates; the waves' spray, the Eye's roar.</summary>
        void StepWave(Proj p)
        {
            var v = TV(p);
            bool eye = p.Kind == ProjKind.EyeLunge;
            p.TRot = Mathf.Atan2(v.y, v.x) - (eye ? Mathf.PI / 2f : 0f);
            if (p.LocalAi0 == 0f)
            {
                if (eye) Sound(Random.Range(0, 100) == 0 ? "Roar_0" : "Item_20", 0.08f);
            }
            if (eye && ++p.FrameCounter >= 6)
            {
                p.FrameCounter = 0;
                p.Frame++;
            }
            if (eye && (p.Frame < 3 || p.Frame >= 6)) p.Frame = 3;
            v *= 0.92f;
            p.LocalAi0++;
            float t = p.LocalAi0 / 20f;
            p.Alpha = 1f - (t < 1f / 6f ? t * 6f : t < 2f / 3f ? 1f : Mathf.Lerp(1f, 0f, (t - 2f / 3f) * 3f));
            if (p.LocalAi0 >= 20f)
            {
                Kill(p);
                return;
            }
            if (!eye && p.LocalAi0 < 20 / 3)
            {
                var c = CenterPx(p);
                for (int i = 0; i < 8; i++)
                {
                    bool foam = Random.Range(0, 3) == 0;
                    var dv = RotateBy(v, Random.Range(-1f, 1f) * Mathf.PI * 2f * 0.15f) * 0.7f;
                    int d = PointDust(c + Random.insideUnitCircle * 8f, foam ? 302 : 33, dv, 0, 1f + 1.5f * Random.value);
                    if (d < 0) continue;
                    if (foam)
                    {
                        D(d).Scale = 1.2f;
                        D(d).Velocity *= 0.63f;
                        D(d).NoGravity = true;
                    }
                    else
                    {
                        D(d).Velocity *= (0.5f + 0.5f * Random.value) * 0.65f;
                        if (Random.Range(0, 4) != 0)
                        {
                            D(d).NoGravity = true;
                            D(d).Velocity *= 2f;
                        }
                    }
                }
            }
            p.Vel = Flip(v);
        }

        /// <summary>The wave (Projectile_1135) and the Eye (NPC_4's mouth frames 3-5), turning from added to plain as they live (P:80598-80605).</summary>
        static void DrawWave(FxBatch fx, Proj p, Vector2 px, bool eye)
        {
            float o = 1f - p.Alpha, k = Mathf.Clamp01(p.LocalAi0 / 19f);
            var col = new Color(o, o, o, k * o);
            if (eye) fx.Draw(FxBatch.Frame("NPC_4", 0, p.Frame * 166, 110, 166, 55f, 83f + 30f), px, col, p.TRot, 1f);
            else fx.Draw(Sprite(p.Spec.Texture), px, col, p.TRot, 1f);
        }

        /// <summary>P:68830-68873: every 6 ticks the Terrarian throws an orb, at the nearest enemy within 400 px when it sees one.</summary>
        void TerrarianOrbs(Proj p)
        {
            if (++p.LocalAi1 < 6f) return;
            p.LocalAi1 = 0f;
            var c = CenterPx(p);
            var rnd = Random.insideUnitCircle.normalized * Random.Range(10, 41) * 0.1f;
            if (Random.Range(0, 3) == 0) rnd *= 2f;
            var v = TV(p) * 0.25f + rnd;
            float best = 400f;
            foreach (var hm in AccessoryEffects.Enemies(p.Pos, 400f, accNear))
            {
                if (!Chaseable(hm)) continue;
                var e = EnemyCenterPx(hm);
                float d = Mathf.Abs(e.x - c.x) + Mathf.Abs(e.y - c.y);
                if (d >= best || !TerraWorld.LineClearPx(c, e)) continue;
                best = d;
                v = Norm(e - c) * 8f;
            }
            v *= 0.8f;
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            var orb = Spawn(ProjKind.TerrarianOrb, TerraParticles.ToWorld(c - v), Flip(v), p.Damage, p.Knockback, p.Crit);
            SpawnClass = keep;
            if (orb != null) orb.Melee = true;
        }

        /// <summary>AI_115 (P:35048-35069): green light, slowing, spinning, fading after 30 updates.</summary>
        static void StepTerrarianOrb(Proj p)
        {
            var v = TV(p);
            FxBatch.Instance?.Light(CenterPx(p), new Color(0.075f, 0.3f, 0.15f), 2f);
            v *= 0.985f;
            p.TRot += v.x * 0.2f + (v.x >= 0f ? 0.08f : -0.08f);
            p.Ai1++;
            if (p.Ai1 > 30f)
            {
                p.Alpha += 10f / 255f;
                if (p.Alpha >= 1f) p.Dead = true;
            }
            p.Vel = Flip(v);
        }

        /// <summary>Added (255-a), with up to six fading copies trailing back (M:35617-35656).</summary>
        static void DrawTerrarianOrb(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = Sprite(p.Spec.Texture);
            float o = 1f - Mathf.Clamp01(p.Alpha);
            var v = TV(p);
            int n = Mathf.Min((int)Mathf.Min(p.Ai1, 999f) + 1, 7);
            for (int k = n - 1; k >= 1; k--)
            {
                float f = (0.4f - 0.06f * k) * o;
                fx.Draw(spr, px - v * k * 1.5f, new Color(o * f, o * f, o * f, 0f), p.TRot, p.Scale - 0.1f * k);
            }
            fx.Draw(spr, px, new Color(o, o, o, 0f), p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ the draw (M:28373-28525, 35310-35688)

        /// <summary>
        /// The string from the player's centre (DrawProj's segment loop: FishingLine 12 px at a
        /// time, sagging when slow, aimed a tenth down from the yoyo's top), then the yoyo turning;
        /// the held one over the player (drawLayer 7). The Kraken's swirl and the Gradient's
        /// rainbow trail behind it, afterimages after it.
        /// </summary>
        void DrawYoyo(FxBatch fx, Proj p, Vector2 px)
        {
            var info = yoyos[p.Kind];
            bool held = !SecondYoyo(p) && p.Ai0 != -2f;
            if (p.Ai0 != -2f) DrawString(fx, p, px, held);
            if (p.Ai0 == -3f) return;
            var spr = Sprite(p.Spec.Texture);
            var v = TV(p);
            var col = Equipment.Current.StringColor == 29 ? Color.white : info.Look ?? new Color(L, L, L, 1f);
            bool flip = p.Dir < 0f;
            if (p.Kind == ProjKind.KrakenYoyo)
            {
                var swirl = Sprite("Extra_307");
                for (int k = 0; k < 5; k++)
                {
                    float f = 0.5f - 0.1f * k;
                    var a = FxBatch.C(40, 40, 127, 0) * 0.5f * 1.12f * f;
                    var b = FxBatch.C(120, 120, 255, 0) * 1.12f * f;
                    fx.Draw(swirl, px - v * k * 0.5f, a, p.TRot, p.Scale * (1f - 0.05f * k), flipX: flip, overPlayer: held);
                    fx.Draw(swirl, px - v * k * 0.5f, b, -p.TRot, p.Scale * 0.75f * (1f - 0.05f * k), flipX: !flip, overPlayer: held);
                }
            }
            if (p.Kind == ProjKind.GradientYoyo && p.Trail.Count > 1)
            {
                for (int i = Mathf.Min(9, p.Trail.Count - 1); i >= 1; i--)
                {
                    float f = i / 10f;
                    var hue = FxParticles.HslToRgb((1f - f) * (1f - f) / 5f % 1f, 1f, 0.5f) * L;
                    hue.a = 127f / 255f;
                    hue *= (1f - f) * (1f - f) * 0.65f;
                    var at = TerraParticles.ToPx(p.Trail[i]);
                    var prev = TerraParticles.ToPx(p.Trail[i - 1]);
                    fx.Draw(spr, at, hue, p.TRot, p.Scale, overPlayer: held);
                    fx.Draw(spr, (at + prev) * 0.5f, hue, p.TRot, p.Scale, overPlayer: held);
                }
            }
            fx.Draw(spr, px, col * (1f - p.Alpha), p.TRot, p.Scale, flipX: flip && p.Kind == ProjKind.KrakenYoyo, overPlayer: held);
            if (p.Kind == ProjKind.KrakenYoyo || p.Kind == ProjKind.TerrarianYoyo || p.Kind == ProjKind.HelFireYoyo)
            {
                float step = p.Kind == ProjKind.HelFireYoyo ? 0.4f : 0.5f;
                for (int k = 1; k <= 4; k++) fx.Draw(spr, px - v * k * step, col * (0.5f - 0.1f * k), p.TRot, p.Scale, overPlayer: held);
            }
        }

        void DrawCounterweight(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Ai0 != -2f) DrawString(fx, p, px, false);
            if (p.Ai0 == -3f) return;
            var col = Equipment.Current.StringColor == 29 ? Color.white : new Color(L, L, L, 1f);
            fx.Draw(Sprite(p.Spec.Texture), px, col, p.TRot, p.Scale);
        }

        /// <summary>The string's colour (TryApplyingPlayerStringColor, M:36109-36161; WorldGen.paintColor), before the halving.</summary>
        static Color StringColour(int code, out bool additive)
        {
            additive = false;
            Color c;
            switch (code)
            {
                case 0: return new Color(L, L, L, 102f / 255f);
                case 13: c = FxBatch.C(20, 20, 20); break;
                case 14: c = FxBatch.C(200, 200, 200); break;
                case 27: c = FxParticles.HslToRgb(Time.time * 0.25f % 1f, 1f, 0.5f); break;
                case 28: c = FxBatch.C(163, 116, 91); break;
                case 30: c = FxBatch.C(226, 228, 142); break;
                case 29:
                {
                    additive = true;
                    float t = Mathf.Clamp01((190f + 65f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.9f)) - 190f) / 65f + Random.Range(-100, 101) * 0.005f);
                    return new Color((184f * t + 255f * (1f - t) + Random.Range(-40, 41)) / 255f, (116f * t + 184f * (1f - t) + Random.Range(-40, 41)) / 255f,
                        (255f * t + 116f * (1f - t) + Random.Range(-40, 41)) / 255f, 0f);
                }
                default:
                {
                    var paint = new[] { (255, 0, 0), (255, 127, 0), (255, 255, 0), (127, 255, 0), (0, 255, 0), (0, 255, 127), (0, 255, 255), (0, 127, 255), (0, 0, 255), (127, 0, 255), (255, 0, 255), (255, 0, 127) };
                    var (r, g, bb) = code >= 1 && code <= 12 ? paint[code - 1] : (255, 255, 255);
                    c = FxBatch.C(Mathf.Max(75, r), Mathf.Max(75, g), Mathf.Max(75, bb));
                    break;
                }
            }
            c *= L;
            c.a = 102f / 255f;
            return c;
        }

        /// <summary>M:28408-28520: the yoyo's string in 12 px links from the player, sagging by the yoyo's slowness and how far it is.</summary>
        void DrawString(FxBatch fx, Proj p, Vector2 px, bool over)
        {
            if (!Holder(out var owner)) return;
            var line = FxBatch.Frame("FishingLine", 0, 0, 12, 12, 6f, 0f);
            var pos = owner;
            float height = p.Spec.H * p.Scale;
            var d = px - pos;
            if (d.sqrMagnitude < 1e-6f) return;
            pos -= d.normalized * 1.2f;
            var v = TV(p);
            var baseCol = StringColour(Equipment.Current.StringColor, out bool additive);
            bool first = true, last = false;
            for (int guard = 0; guard < 200 && !last; guard++)
            {
                float seg = 12f, len = d.magnitude;
                if (float.IsNaN(len)) break;
                if (len < 20f)
                {
                    seg = len - 8f;
                    last = true;
                }
                var step = d * 12f / len;
                if (first) first = false;
                else pos += step;
                d = new Vector2(px.x - pos.x, px.y - height * 0.5f + height * 0.1f - pos.y);
                if (len > 12f)
                {
                    float k = 0.3f * (1f - Mathf.Min(Mathf.Abs(v.x) + Mathf.Abs(v.y), 16f) / 16f);
                    float f = Mathf.Min(len / 80f, 1f);
                    k *= f;
                    k = Mathf.Max(k, 0f);
                    k *= f;
                    k *= 0.5f;
                    if (d.y > 0f)
                    {
                        d.y *= 1f + k;
                        d.x *= 1f - k;
                    }
                    else
                    {
                        float g = Mathf.Min(Mathf.Abs(v.x) / 3f, 1f) - 0.5f;
                        k *= g;
                        if (k > 0f) k *= 2f;
                        d.y *= 1f + k;
                        d.x *= 1f - k;
                    }
                }
                float rot = Mathf.Atan2(d.y, d.x) - 1.57f;
                var c = baseCol * 0.5f;
                if (seg <= 0f) break;
                fx.Draw(seg >= 12f ? line : FxBatch.Frame("FishingLine", 0, 0, 12, Mathf.Max(1, (int)seg), 6f, 0f), pos - new Vector2(6f, 0f) + new Vector2(6f, 0f), c, rot, 1f, overPlayer: over);
            }
        }
    }
}
