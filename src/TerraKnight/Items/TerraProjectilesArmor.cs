using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The armor sets' projectiles (accarmor-hm sec-H2; Terraria's space, px y down): the
    /// Orichalcum petal (221), the Chlorophyte leaf crystal and its shot (226, 227), the Titanium
    /// shards (908), the Spectre spirits (298, 356), the Solar flare (608), the Forbidden storm
    /// (656), the Stardust Guardian (623) and the Nebula boosters (items 3453-3455).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterArmor()
        {
            // P:3068-3080: the petal (aiStyle 41), 20×20, through everything, two more updates a tick, 120 updates.
            specs[ProjKind.OrichalcumPetal] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_221", W = 20, H = 20, Time = 120, Penetrate = -1, Extra = 2, TileCollide = false, NoSetOnHit = true, TrailLength = 10,
                Update = (t, p) => StepPetal(p), Draw = (t, fx, p, px) => DrawPetal(fx, p, px), OnKill = (t, p) => { },
            };
            // P:3113-3125: the leaf crystal over the player (aiStyle 42), never hurting itself.
            specs[ProjKind.CrystalLeaf] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_226", W = 22, H = 42, Time = 18000, Penetrate = -1, TileCollide = false, OwnMove = true, CanDamage = p => false,
                Update = (t, p) => t.StepCrystalLeaf(p), Draw = (t, fx, p, px) => DrawCrystalLeaf(fx, p, px), OnKill = (t, p) => { },
            };
            // P:3126-3138: its shot (aiStyle 43), hidden, 180 more updates a tick: a near-instant bolt.
            specs[ProjKind.CrystalLeafShot] = new Spec
            {
                Ai = Ai.Custom, W = 14, H = 14, Time = 180, Extra = 180, NoSetOnHit = true, Look = Look.Invisible,
                Update = (t, p) => StepLeafShot(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => { },
            };
            // P:8628-8639: a Titanium shard (aiStyle 175), 14×14, gone on its first hit.
            specs[ProjKind.TitaniumShard] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_908", W = 14, H = 14, Time = 3600, Penetrate = 1, TileCollide = false, OwnMove = true, NoSetOnHit = true,
                Update = (t, p) => t.StepShard(p), Draw = (t, fx, p, px) => DrawShard(fx, p, px), OnKill = (t, p) => KillShard(p),
            };
            // P:3885-3894: the healing spirit (aiStyle 52), unseen, three more updates a tick.
            specs[ProjKind.SpiritHeal] = new Spec
            {
                Ai = Ai.Custom, W = 6, H = 6, Time = 600, Extra = 3, Penetrate = -1, TileCollide = false, OwnMove = true, CanDamage = p => false, Look = Look.Invisible,
                Update = (t, p) => StepSpiritHeal(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => { },
            };
            // P:4466-4475: the wrath (aiStyle 59), unseen, harmless for its first 60 updates.
            specs[ProjKind.SpectreWrath] = new Spec
            {
                Ai = Ai.Custom, W = 6, H = 6, Time = 600, Extra = 3, Penetrate = 1, TileCollide = false, OwnMove = true, NoGhost = true, Look = Look.Invisible,
                CanDamage = p => p.Ai1 >= 60f, Update = (t, p) => t.StepWrath(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => { },
            };
            // P:7050-7062: the Forbidden storm (aiStyle 127), magic, through everything, its own 8 tick immunity, 900 ticks.
            specs[ProjKind.SandnadoFriendly] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_656", W = 10, H = 10, Time = 1200, Penetrate = -1, TileCollide = false, OwnMove = true, LocalCooldown = 8,
                CanDamage = p => p.LocalAi0 < 30f && p.Ai0 < 900f, Update = (t, p) => t.StepSandnado(p), Draw = (t, fx, p, px) => DrawSandnado(fx, p, px), OnKill = (t, p) => { },
                OnHit = (t, p, hm) => p.LocalAi0++,
            };
            // P:6701-6718: the Stardust Guardian (aiStyle 120), a minion of no slot; its punches are its own.
            specs[ProjKind.StardustGuardian] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_623", W = 50, H = 80, Time = 2, Penetrate = -1, TileCollide = false, OwnMove = true, CanDamage = p => false,
                Update = (t, p) => t.StepGuardian(p), Draw = (t, fx, p, px) => DrawGuardian(fx, p, px), OnKill = (t, p) => { },
            };
            // Items 3453-3455: the Nebula boosters, pickups with no gravity (WorldItem.cs:781-794).
            specs[ProjKind.NebulaBooster] = new Spec
            {
                Ai = Ai.Custom, Texture = "Item_3453", W = 12, H = 12, Time = 36000, Penetrate = -1, TileCollide = false, OwnMove = true, CanDamage = p => false,
                Update = (t, p) => StepBooster(p), Draw = (t, fx, p, px) => DrawBooster(fx, p, px), OnKill = (t, p) => { },
            };
        }

        // ------------------------------------------------------------------ what ArmorEffects asks for

        /// <summary>A set's projectile at a point (Terraria px) with a velocity (px a tick, y down); no damage class.</summary>
        public void ArmorSpawn(ProjKind kind, Vector2 atPx, Vector2 velYDown, int damage, float knockback)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.None;
            Spawn(kind, TerraParticles.ToWorld(atPx), Flip(velYDown), damage, knockback, 0f);
            SpawnClass = keep;
        }

        public int ArmorCount(ProjKind kind)
        {
            int n = 0;
            foreach (var q in live) if (q.Kind == kind && !q.Dead) n++;
            return n;
        }

        /// <summary>The leaf crystal's centre (Terraria px), if there is one.</summary>
        public bool ArmorCrystal(out Vector2 centrePx)
        {
            foreach (var q in live)
                if (q.Kind == ProjKind.CrystalLeaf && !q.Dead)
                {
                    centrePx = CenterPx(q);
                    return true;
                }
            centrePx = default;
            return false;
        }

        /// <summary>A leaf shot from a point at an enemy, leading it (Utils.GetChaseResults, 12 px a tick, within 180 ticks).</summary>
        public void ArmorLeafShot(Vector2 fromPx, HealthManager hm)
        {
            var v = Lead(fromPx, hm, 12f, 180f);
            ArmorSpawn(ProjKind.CrystalLeafShot, fromPx, v, 100, 10f);
        }

        /// <summary>A healing spirit from where the enemy was hit, carrying heal to the player.</summary>
        public void ArmorSpiritHeal(Vector2 atPx, int heal)
        {
            var p = Spawn(ProjKind.SpiritHeal, TerraParticles.ToWorld(atPx), Vector2.zero, 0, 0f, 0f);
            if (p != null) p.Ai1 = heal;
        }

        /// <summary>A wrath bolt off the enemy hit, at another (magic, the shot's damage).</summary>
        public void ArmorWrath(Vector2 atPx, Vector2 velYDown, int damage, HealthManager from)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.Magic;
            var p = Spawn(ProjKind.SpectreWrath, TerraParticles.ToWorld(atPx), Flip(velYDown), damage, 0f, 0f);
            SpawnClass = keep;
            if (p != null) p.Target = PickWrathTarget(atPx, from);
        }

        /// <summary>A Nebula booster flung from an enemy: 0 damage, 1 life, 2 mana.</summary>
        public void ArmorNebulaBooster(Vector2 atPx, Vector2 velYDown, int kind)
        {
            var p = Spawn(ProjKind.NebulaBooster, TerraParticles.ToWorld(atPx), Flip(velYDown), 0, 0f, 0f);
            if (p != null) p.Ai0 = kind;
        }

        // ------------------------------------------------------------------ 221: the Orichalcum petal

        /// <summary>AI_041 (P:28179-28187): a random frame of three; spins with its speed.</summary>
        static void StepPetal(Proj p)
        {
            if (p.Updates == 0)
            {
                p.Frame = Random.Range(0, 3);
                p.Scale = 1f + Random.Range(0, 30) * 0.01f;
            }
            p.TRot += TV(p).x * 0.01f;
        }

        /// <summary>(255,255,255,200) with nine fading copies trailing back along its flight (M:30598-30648).</summary>
        static void DrawPetal(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Frame(p.Spec.Texture, 0, p.Frame * 20, 20, 20, 10f, 10f);
            var v = TV(p);
            for (int i = 9; i >= 0; i--)
            {
                float k = 1f - 0.1f * i;
                fx.Draw(spr, px - v * i * 0.5f, new Color(k, k, k, 200f / 255f * k), p.TRot, p.Scale);
            }
        }

        // ------------------------------------------------------------------ 226: the leaf crystal

        static float MouseTextPulse => 190f + 65f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.9f));

        /// <summary>
        /// AI_042 (P:28188-28256): 60 px over the player's centre; every 40 ticks a shot at the
        /// nearest enemy it can see within 700 px (by x + y), leading it.
        /// </summary>
        void StepCrystalLeaf(Proj p)
        {
            var hero = HeroController.SilentInstance;
            if (!ArmorEffects.Chlorophyte || hero == null || hero.cState.dead)
            {
                Kill(p);
                return;
            }
            var player = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var c = new Vector2(Mathf.Round(player.x), Mathf.Round(player.y - 60f));
            p.Pos = TerraParticles.ToWorld(c);
            p.TimeLeft = 18000;
            p.Scale = 0.95f + (MouseTextPulse / 200f - 0.35f) * 0.2f;
            FxBatch.Instance?.Light(c, new Color(0.1f, 0.4f, 0.2f), 3f);
            if (ArmorEffects.CrystalLeafCooldown > 0) return;
            HealthManager best = null;
            float bestD = 700f;
            foreach (var hm in AccessoryEffects.Enemies(p.Pos, 700f, accNear))
            {
                if (!Chaseable(hm)) continue;
                var e = EnemyCenterPx(hm);
                float d = Mathf.Abs(e.x - c.x) + Mathf.Abs(e.y - c.y);
                if (d >= bestD || !InSight(c, e) || !TerraWorld.LineClearPx(c, e)) continue;
                bestD = d;
                best = hm;
            }
            if (best == null) return;
            ArmorLeafShot(c - new Vector2(4f, 0f), best);
            ArmorEffects.CrystalLeafCooldown = 40;
        }

        static void DrawCrystalLeaf(FxBatch fx, Proj p, Vector2 px)
        {
            float k = MouseTextPulse / 255f;
            fx.Draw(Sprite(p.Spec.Texture), px, new Color(k, k, k, 1f), 0f, p.Scale);
        }

        /// <summary>The velocity that meets a moving enemy (Utils.GetChaseResults): its last speed held; else straight at it.</summary>
        static Vector2 Lead(Vector2 fromPx, HealthManager hm, float speed, float maxTicks)
        {
            var at = EnemyCenterPx(hm);
            var rb = hm.GetComponent<Rigidbody2D>();
            var vt = rb != null ? new Vector2(rb.linearVelocity.x, -rb.linearVelocity.y) / Player.TerraMovement.PxT : Vector2.zero;
            var d = at - fromPx;
            float a = Vector2.Dot(vt, vt) - speed * speed, b = 2f * Vector2.Dot(d, vt), c = Vector2.Dot(d, d);
            float t = -1f;
            if (Mathf.Abs(a) < 1e-4f) t = b < 0f ? -c / b : -1f;
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc >= 0f)
                {
                    float r = Mathf.Sqrt(disc), t1 = (-b - r) / (2f * a), t2 = (-b + r) / (2f * a);
                    t = t1 > 0f && (t1 < t2 || t2 <= 0f) ? t1 : t2;
                }
            }
            var aim = t > 0f && t <= maxTicks ? d + vt * t : d;
            return aim.sqrMagnitude < 1e-4f ? new Vector2(speed, 0f) : aim.normalized * speed;
        }

        // ------------------------------------------------------------------ 227: the leaf shot

        /// <summary>AI_043 (P:28257-28293): the grass sound and eight leaf dust at first; a lime streak every second update.</summary>
        static void StepLeafShot(Proj p)
        {
            var v = TV(p);
            p.TRot = Mathf.Atan2(v.y, v.x);
            if (p.Updates == 0)
            {
                Sound("Grass", 0.8f);
                for (int i = 0; i < 8; i++)
                {
                    int d = DustBox(p, 157, Vector2.zero, 0, 1.5f, true);
                    if (d >= 0) D(d).Velocity = D(d).Velocity * 3f + v * Random.value;
                }
            }
            if (p.Updates % 2 == 0) FxParticles.LeafCrystalShot(CenterPx(p), v, p.TimeLeft / 180f);
        }

        // ------------------------------------------------------------------ 908: the Titanium shards

        static Vector2 lastPlayerPx;
        static int lastPlayerTick = -1;

        /// <summary>
        /// AI_175 (P:46151-46176): a flat ring round the player turning once every 50 ticks, wider
        /// with more shards, carried along as the player moves; gone when the barrier ends.
        /// </summary>
        void StepShard(Proj p)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || hero.cState.dead || !PlayerBuffs.Has(ArmorEffects.TitaniumStorm))
            {
                Kill(p);
                return;
            }
            var player = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            if (lastPlayerTick != armorTicks)
            {
                // The player's move since the last tick, shared by every shard.
                shardCarry = lastPlayerPx == Vector2.zero ? Vector2.zero : player - lastPlayerPx;
                lastPlayerPx = player;
                lastPlayerTick = armorTicks;
            }
            if (p.Ticks <= 1)
            {
                p.Frame = Random.Range(0, 12);
                p.TRot = Random.value * Mathf.PI * 2f;
            }
            p.TRot += Mathf.PI / 200f;
            int index = 0, total = 0;
            foreach (var q in live)
            {
                if (q.Kind != ProjKind.TitaniumShard || q.Dead) continue;
                if (q == p) index = total;
                total++;
            }
            float f = ((float)index / Mathf.Max(1, total) + (ArmorTicks % 300) / 300f * 6f) * Mathf.PI * 2f;
            float r = 24f + 6f * total;
            var c = CenterPx(p) + shardCarry;
            var target = player + new Vector2(Mathf.Cos(f), Mathf.Sin(f) * 0.05f) * r;
            c = Vector2.Lerp(c, target, 0.3f);
            p.Pos = TerraParticles.ToWorld(c);
            p.Ai1 = Mathf.Sin(f);
        }

        static Vector2 shardCarry;

        /// <summary>
        /// Its frame of twelve side by side, wobbling; behind the player on the far side of the
        /// ring (M:33957-33968). The TitaniumStorm shader's gradient is a silver-blue shimmer here.
        /// </summary>
        static void DrawShard(FxBatch fx, Proj p, Vector2 px)
        {
            float w = Time.time * 8f + p.GetHashCode() % 97;
            var at = px + new Vector2(Mathf.Cos(w), Mathf.Sin(w)) * 4f * 0.25f;
            float s = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f + p.Frame);
            var tint = Color.Lerp(new Color(0.85f, 0.9f, 1f, 1f), new Color(0.6f, 0.75f, 1f, 1f), s);
            fx.Draw(FxBatch.Frame(p.Spec.Texture, p.Frame * 24, 0, 24, 28, 12f, 14f), at, tint, p.TRot, 1f, overPlayer: p.Ai1 > 0f);
        }

        static void KillShard(Proj p)
        {
            for (int i = 0; i < 6; i++)
            {
                int d = DustBox(p, 11, Vector2.zero, 0, 0.9f, true);
                if (d >= 0) D(d).Velocity *= 1.5f;
            }
        }

        // ------------------------------------------------------------------ 298 and 356: the Spectre spirits

        /// <summary>AI_052 (P:28930-28988): home on the player, healing on touch; a trail of five spirit dust.</summary>
        static void StepSpiritHeal(Proj p)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null)
            {
                p.Dead = true;
                return;
            }
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var player = TerraParticles.ToPx(b.center);
            var c = CenterPx(p);
            var to = player - c;
            if (to.magnitude < 50f && Mathf.Abs(to.x) < 13f && Mathf.Abs(to.y) < 24f)
            {
                int healed = TerraMod.Instance != null ? TerraMod.Instance.Stats.Heal((int)p.Ai1) : 0;
                UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, ((int)p.Ai1).ToString(), UI.CombatText.Heal);
                ArmorEffects.DevHeals += healed > 0 ? 1 : 0;
                p.Dead = true;
                return;
            }
            var v = TV(p);
            v = (v * 15f + to.normalized * 4f) / 16f;
            p.Vel = Flip(v);
            p.Pos += p.Vel * Px;
            SpiritTrail(c, v);
        }

        static void SpiritTrail(Vector2 c, Vector2 v)
        {
            for (int i = 0; i < 5; i++)
            {
                int d = PointDust(c - v * 0.2f * i, 175, Vector2.zero, 100, 1.3f);
                if (d >= 0) D(d).NoGravity = true;
            }
        }

        /// <summary>AI_059 (P:29369-29429): curve onto a chosen enemy (another if it's gone), harmless its first 60 updates.</summary>
        void StepWrath(Proj p)
        {
            p.Ai1++;
            var c = CenterPx(p);
            if (p.Target == null || !Chaseable(p.Target))
            {
                p.Target = PickWrathTarget(c, null);
                if (p.Target == null)
                {
                    Kill(p);
                    return;
                }
            }
            var v = TV(p);
            var to = (EnemyCenterPx(p.Target) - c).normalized;
            v = (v * 29f + to * 4f) / 30f;
            p.Vel = Flip(v);
            p.Pos += p.Vel * Px;
            SpiritTrail(c, v);
        }

        /// <summary>A random chaseable enemy within 800 px (by x + y), one in sight and more than 50 px off by preference.</summary>
        HealthManager PickWrathTarget(Vector2 atPx, HealthManager hit)
        {
            var good = new List<HealthManager>();
            var any = new List<HealthManager>();
            foreach (var hm in AccessoryEffects.Enemies(TerraParticles.ToWorld(atPx), 800f, accNear))
            {
                if (!Chaseable(hm)) continue;
                var e = EnemyCenterPx(hm);
                float d = Mathf.Abs(e.x - atPx.x) + Mathf.Abs(e.y - atPx.y);
                if (d >= 800f) continue;
                any.Add(hm);
                if (d > 50f && TerraWorld.LineClearPx(atPx, e) && hm != hit) good.Add(hm);
            }
            var from = good.Count > 0 ? good : any;
            return from.Count > 0 ? from[Random.Range(0, from.Count)] : null;
        }

        // ------------------------------------------------------------------ 608: the Solar flare

        /// <summary>
        /// The flare (P:6556-6568, its Kill P:73439-73492): one hit on everything in 160×160
        /// round the player (knockback 15), the Item_14 boom, fire and smoke.
        /// </summary>
        public void ArmorSolarBurst(int damage)
        {
            var hero = HeroController.SilentInstance;
            var melee = TerraMod.Instance?.Melee;
            if (hero == null || melee == null) return;
            Vector2 cw = hero.GetComponent<BoxCollider2D>().bounds.center;
            var c = TerraParticles.ToPx(cw);
            ArmorEffects.Context = new ArmorEffects.Hit { Suppress = false };
            foreach (var h in Physics2D.OverlapBoxAll(cw, new Vector2(160f, 160f) * Px, 0f, 1 << 11))
            {
                var hm = h.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead) continue;
                melee.HitWith(h, damage, 15f, 0f, h.bounds.center.x >= cw.x ? 1f : -1f, cw);
            }
            ArmorEffects.Context = default;
            Sound("Item_14");
            var tl = c - new Vector2(80f, 80f);
            for (int i = 0; i < 4; i++) BoxDust(tl, 160, 160, 31, 0f, 0f, 100, 1.5f);
            for (int i = 0; i < 30; i++)
            {
                int d = BoxDust(tl, 160, 160, 6, 0f, 0f, 200, 3.7f);
                if (d >= 0)
                {
                    D(d).NoGravity = true;
                    D(d).Velocity *= 3f;
                }
                d = BoxDust(tl, 160, 160, 6, 0f, 0f, 100, 1.5f);
                if (d >= 0)
                {
                    D(d).Velocity *= 2f;
                    D(d).FadeIn = 2.5f;
                }
            }
            for (int i = 0; i < 10; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                int d = PointDust(c + dir * 80f, 6, dir * 3f, 0, 2.7f);
                if (d >= 0) D(d).NoGravity = true;
                d = PointDust(c + dir * 80f, 31, dir * 3f, 0, 1.5f);
            }
            var pp = TerraParticles.Instance;
            for (int g = 0; g < 2 && pp != null; g++)
                pp.NewGore(c - new Vector2(24f, 24f), new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f)) * 0.3f, Random.Range(61, 64), exact: true);
        }

        // ------------------------------------------------------------------ 656: the Forbidden storm

        /// <summary>
        /// CommandForbiddenStorm (Pl:57477-57569) after MinionRestTargetAim (Pl:57612-57674): the
        /// cursor's ground (searched 20 tiles up and down, then down to a floor within 5), within
        /// 1000 px, room for a storm, in sight; 20 mana; an older storm starts to fade.
        /// </summary>
        public void ArmorForbiddenStorm()
        {
            var hero = HeroController.SilentInstance;
            var mod = TerraMod.Instance;
            if (hero == null || mod == null) return;
            if (ArmorCount(ProjKind.SandnadoFriendly) > 1) return; // UpdateForbiddenSetLock
            var player = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var mouse = TerraParticles.ToPx(mod.DevAim ?? mod.AimPoint);
            if (!RestPoint(mouse, out var rest) || Vector2.Distance(rest, player) > 1000f) return;
            if (!StormRoom(rest) || !TerraWorld.LineClearPx(player, rest + new Vector2(0f, -8f))) return;
            if (!mod.Stats.SpendMana(20)) return;
            foreach (var q in live)
                if (q.Kind == ProjKind.SandnadoFriendly && !q.Dead && q.Ai0 < 780f) q.Ai0 = 780f + q.Ai0 % 60f;
            var b = Equipment.Current;
            int dmg = (int)(20f * (1f + b.MagicDamage + b.MinionDamage));
            var keep = SpawnClass;
            SpawnClass = DamageClass.Magic;
            Spawn(ProjKind.SandnadoFriendly, TerraParticles.ToWorld(rest), Vector2.zero, dmg, 0f, 0f);
            SpawnClass = keep;
            ArmorEffects.DevStorms++;
        }

        static bool SolidTile(int tx, int ty) => TerraWorld.IsSolidAt(TerraParticles.ToWorld(new Vector2(tx * 16f + 8f, ty * 16f + 8f)));

        /// <summary>MinionRestTargetAim: the nearest air edge to the cursor's tile, then the floor under it; (x·16+8, floor y·16).</summary>
        static bool RestPoint(Vector2 mousePx, out Vector2 rest)
        {
            int tx = Mathf.FloorToInt(mousePx.x / 16f), ty = Mathf.FloorToInt(mousePx.y / 16f);
            rest = default;
            if (SolidTile(tx, ty))
            {
                bool found = false;
                for (int k = 1; k <= 20 && !found; k++)
                {
                    if (!SolidTile(tx, ty - k)) { ty -= k; found = true; }
                    else if (!SolidTile(tx, ty + k)) { ty += k; found = true; }
                }
                if (!found) return false;
            }
            for (int k = 0; k <= 5; k++)
                if (SolidTile(tx, ty + k + 1))
                {
                    rest = new Vector2(tx * 16f + 8f, (ty + k + 1) * 16f);
                    return true;
                }
            return false;
        }

        /// <summary>CanSpawnSandstormFriendly (StrayMethods.cs:65-81): three tile columns each with ten free tiles up from the point.</summary>
        static bool StormRoom(Vector2 restPx)
        {
            int tx = Mathf.FloorToInt(restPx.x / 16f), ty = Mathf.FloorToInt(restPx.y / 16f) - 1;
            for (int x = tx - 1; x <= tx + 1; x++)
                for (int k = 0; k < 10; k++)
                    if (SolidTile(x, ty - k)) return false;
            return true;
        }

        /// <summary>
        /// AI_127 (P:36662-36749): a column of sand over the free span round its tile (15 up, 15
        /// down), a fifth as wide as tall; spiralling sand; 900 ticks, fading for the last 120 or
        /// after 30 hits.
        /// </summary>
        void StepSandnado(Proj p)
        {
            if (p.Ai0 == 0f) Sound("Item_82");
            p.Ai0++;
            if (p.Ai0 >= 900f)
            {
                Kill(p);
                return;
            }
            p.TimeLeft = 1200;
            if (p.LocalAi0 >= 30f && p.Ai0 < 780f) p.Ai0 = 780f + p.Ai0 % 60f;
            var c = CenterPx(p);
            int tx = Mathf.FloorToInt(c.x / 16f), ty = Mathf.FloorToInt((p.StartPos == Vector2.zero ? c : TerraParticles.ToPx(p.StartPos)).y / 16f) - 1;
            int top = ty, bottom = ty;
            for (int k = 0; k < 15 && !SolidTile(tx, top - 1); k++) top--;
            for (int k = 0; k < 15 && !SolidTile(tx, bottom + 1); k++) bottom++;
            float h = (bottom - top + 1) * 16f, w = h * 0.2f * 0.65f;
            p.LocalAi1 = h;
            p.HitSize = new Vector2(w, h);
            p.Pos = TerraParticles.ToWorld(new Vector2(tx * 16f + 8f, top * 16f + h * 0.5f));
            if (p.Ai0 < 780f)
            {
                int d = BoxDust(new Vector2(tx * 16f + 8f - w * 0.5f, top * 16f), (int)w, (int)h, 269, 0f, 0f, 0, 0.3f);
                if (d >= 0)
                {
                    D(d).FadeIn = 1f;
                    D(d).NoGravity = true;
                    D(d).Velocity = new Vector2(Random.Range(-1f, 1f) * 2f, -Random.value * 2f);
                }
            }
        }

        /// <summary>M:33071-33125: Projectile_656 stacked every 5.1 px up the column, each turning, wider up top, sand-coloured at half.</summary>
        static void DrawSandnado(FxBatch fx, Proj p, Vector2 px)
        {
            float h = p.LocalAi1;
            if (h <= 0f) return;
            float fade = Mathf.Min(1f, p.Ai0 / 30f) * Mathf.Clamp01((900f - p.Ai0) / 120f);
            var spr = Sprite(p.Spec.Texture);
            var col = new Color(212f / 255f, 192f / 255f, 100f / 255f, 0.5f) * fade;
            int k = 0;
            for (float y = 0f; y < h; y += 5.1f, k++)
            {
                float rot = -Mathf.PI / 50f * p.Ai0 + k * Mathf.PI * 2f / -20f;
                float s = 1f + (y / h - 0.15f);
                fx.Draw(spr, px + new Vector2(0f, h * 0.5f - y), col, rot, s * 0.65f);
            }
        }

        // ------------------------------------------------------------------ 623: the Stardust Guardian

        readonly Dictionary<int, int> guardianImmune = new Dictionary<int, int>();

        /// <summary>
        /// AI_120 (P:45454-45784): rests 15 px behind and 25 up from the player; with an enemy
        /// within 500 px of the player, flies beside it (70 px off, up to 24 px a tick) and punches
        /// an 80×40 box ahead, each enemy every 5 ticks, for 30 × minion damage.
        /// </summary>
        void StepGuardian(Proj p)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || hero.cState.dead || !PlayerBuffs.Has(ArmorEffects.StardustGuardian))
            {
                Kill(p);
                return;
            }
            p.TimeLeft = 2;
            float dir = TerraMod.Instance.UseDirection < 0f ? -1f : 1f;
            var player = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var rest = player + new Vector2(-15f * dir, -25f);
            var c = CenterPx(p);
            var v = TV(p);
            FxBatch.Instance?.Light(c, new Color(0.9f, 0.9f, 0.7f), 3f);
            if (p.Ticks <= 1) GuardianBurst(c);
            var keys = new List<int>(guardianImmune.Keys);
            foreach (var k in keys) if (--guardianImmune[k] <= 0) guardianImmune.Remove(k);
            int state = (int)p.Ai0;
            if (state == 0)
            {
                c = Vector2.Lerp(c, rest, 0.05f);
                v *= 0.5f;
                p.Dir = dir;
                if (++p.FrameCounter >= 9)
                {
                    p.FrameCounter = 0;
                    p.Frame = (p.Frame + 1) % 8;
                }
                if (Mathf.Abs(c.x - rest.x) <= 170f)
                {
                    var t = NearestTo(player, 500f);
                    if (t != null && Mathf.Abs(EnemyCenterPx(t).x - c.x) < 500f)
                    {
                        p.Target = t;
                        p.Ai0 = 2f;
                        p.Frame = 12;
                        p.FrameCounter = 0;
                    }
                }
            }
            else
            {
                if (p.Ticks % 4 == 0)
                {
                    var nt = NearestTo(c, 500f);
                    if (nt != null) p.Target = nt;
                }
                var t = p.Target;
                if (t == null || !Chaseable(t) || Vector2.Distance(player, c) > 500f || Vector2.Distance(EnemyCenterPx(t), c) > 500f)
                {
                    p.Ai0 = 0f;
                    p.Target = null;
                    p.Frame = 0;
                }
                else
                {
                    var e = EnemyCenterPx(t);
                    float side = Mathf.Sign(e.x - player.x);
                    var tb = t.GetComponent<Collider2D>();
                    float half = tb != null ? tb.bounds.extents.x / Px : 16f;
                    var goal = e + new Vector2(-side * (half + 70f), -10f);
                    var to = goal - c;
                    float dist = to.magnitude;
                    float cap = 6f * Mathf.Lerp(1f, 4f, Mathf.InverseLerp(50f, 400f, dist));
                    v = Vector2.MoveTowards(v, Vector2.ClampMagnitude(to, cap), 32f);
                    c += v;
                    p.Dir = e.x >= c.x ? 1f : -1f;
                    if (++p.FrameCounter >= (p.Frame < 14 ? 8 : 3))
                    {
                        p.FrameCounter = 0;
                        p.Frame = p.Frame < 13 ? p.Frame + 1 : p.Frame >= 18 ? 14 : p.Frame + 1;
                    }
                    if (p.Frame >= 14) GuardianPunch(p, c + new Vector2(p.Dir * 40f, 0f));
                }
            }
            p.Vel = Flip(v);
            p.Pos = TerraParticles.ToWorld(c);
        }

        HealthManager NearestTo(Vector2 fromPx, float range)
        {
            HealthManager best = null;
            float bestD = range;
            foreach (var hm in AccessoryEffects.Enemies(TerraParticles.ToWorld(fromPx), range, accNear))
            {
                if (!Chaseable(hm)) continue;
                float d = Vector2.Distance(EnemyCenterPx(hm), fromPx);
                if (d < bestD)
                {
                    bestD = d;
                    best = hm;
                }
            }
            return best;
        }

        /// <summary>The punch's 80×40 box (P:15612-15622): 30 × minion damage, whip tags taken, each enemy every 5 ticks, a burst of stardust.</summary>
        void GuardianPunch(Proj p, Vector2 boxPx)
        {
            var melee = TerraMod.Instance?.Melee;
            if (melee == null) return;
            int dmg = (int)(30f * (1f + Equipment.Current.MinionDamage));
            foreach (var h in Physics2D.OverlapBoxAll(TerraParticles.ToWorld(boxPx), new Vector2(80f, 40f) * Px, 0f, 1 << 11))
            {
                var hm = h.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead) continue;
                int id = hm.GetInstanceID();
                if (guardianImmune.ContainsKey(id)) continue;
                int dealt = SummonTags.Modify(hm, dmg, out bool crit, 1f, false, boxPx);
                ArmorEffects.Context = new ArmorEffects.Hit { Class = DamageClass.Summon };
                melee.HitWith(h, dealt, 10f, crit ? 100f : 0f, p.Dir, TerraParticles.ToWorld(boxPx - new Vector2(p.Dir * 40f, 0f)));
                SummonTags.OnHit(hm, dmg, boxPx, 10f);
                guardianImmune[id] = 5;
                ArmorEffects.DevGuardianHits++;
                FxParticles.StardustPunch(EnemyCenterPx(hm), new Vector2(p.Dir * 4f, 0f));
            }
        }

        static void GuardianBurst(Vector2 c)
        {
            for (int i = 0; i < 30; i++)
            {
                int d = BoxDust(c - new Vector2(25f, 40f), 50, 80, 135, 0f, 0f, 200, 1.7f);
                if (d >= 0)
                {
                    D(d).NoGravity = true;
                    D(d).Velocity *= 3f;
                }
                d = BoxDust(c - new Vector2(25f, 40f), 50, 80, 135, 0f, 0f, 100, 1f);
                if (d >= 0)
                {
                    D(d).Velocity *= 2f;
                    D(d).FadeIn = 2.5f;
                }
            }
        }

        /// <summary>Projectile_623's nineteen 108×92 frames, half see-through, facing its way, shaking 2 px while it punches (M:34219-34225).</summary>
        static void DrawGuardian(FxBatch fx, Proj p, Vector2 px)
        {
            var at = px;
            if (p.Ai0 == 2f) at += new Vector2(Random.Range(-2, 3), Random.Range(-2, 3));
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, p.Frame * 92, 108, 92, 54f, 46f), at, new Color(1f, 1f, 1f, 0.5f), 0f, 1f, flipX: p.Dir < 0f);
        }

        // ------------------------------------------------------------------ the Nebula boosters

        static readonly string[] boosterTex = { "Item_3453", "Item_3454", "Item_3455" };
        static readonly Color[] boosterLight = { new Color(0.5f, 0.3f, 0.05f), new Color(0.5f, 0.1f, 0.25f), new Color(0.25f, 0.1f, 0.5f) };

        /// <summary>
        /// No gravity, slowing ×0.95 to a stop (WorldItem.cs:781-794); within 142 px of the player
        /// pulled in at 12 px a tick (smoothing 5); taken on touch for a level of its buff.
        /// </summary>
        static void StepBooster(Proj p)
        {
            var hero = HeroController.SilentInstance;
            var c = CenterPx(p);
            var v = TV(p);
            int kind = Mathf.Clamp((int)p.Ai0, 0, 2);
            FxBatch.Instance?.Light(c, boosterLight[kind] * Random.Range(0.9f, 1.1f), 2f);
            if (hero != null && !hero.cState.dead)
            {
                var b = hero.GetComponent<BoxCollider2D>().bounds;
                var player = TerraParticles.ToPx(b.center);
                var to = player - c;
                if (Mathf.Abs(to.x) < 10f + 6f && Mathf.Abs(to.y) < 21f + 6f)
                {
                    TerraMod.Instance?.PlaySound("Grab", 0.8f);
                    ArmorEffects.NebulaLevelUp(kind);
                    p.Dead = true;
                    return;
                }
                if (Mathf.Abs(to.x) < 10f + 142f && Mathf.Abs(to.y) < 21f + 142f)
                {
                    v = (v * 4f + to.normalized * 12f) / 5f;
                    p.Vel = Flip(v);
                    p.Pos += p.Vel * Px;
                    return;
                }
            }
            v *= 0.95f;
            if (v.magnitude < 0.1f) v = Vector2.zero;
            p.Vel = Flip(v);
            p.Pos += p.Vel * Px;
        }

        /// <summary>Its four 18×28 frames, 6 ticks each, (255,255,255,50) (M:3835-3837).</summary>
        static void DrawBooster(FxBatch fx, Proj p, Vector2 px)
        {
            int kind = Mathf.Clamp((int)p.Ai0, 0, 2);
            int frame = (p.Ticks / 6) % 4;
            fx.Draw(FxBatch.Frame(boosterTex[kind], 0, frame * 28, 18, 28, 9f, 14f), px, new Color(1f, 1f, 1f, 50f / 255f), 0f, 1f);
        }

        /// <summary>Ticks counted (Player.miscCounter for the shards' turn).</summary>
        static int armorTicks;
        static int ArmorTicks => armorTicks;
    }
}
