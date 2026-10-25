using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The 10-21 batch's projectiles (Terraria px, y down, as vanilla's code has them): the Arc Surge's bolt (1122: AI_203 and
    /// its Kill, LightningGenerator's path from the hand hit along at 16 px a point, Red Lightning 4-7 s, the strip drawn 16
    /// ticks), the Storm Spear (730, AI_019, its 732 bolt at the thrust's start), the jousting lances (877-879: AI_019 held
    /// out while channelled, damage and knockback by the player's speed, the 95 px shaft, knocked away when the player is
    /// hurt), the Blood Rain Bow's arrow (819, AI_001: a trail of blood) and the Resonance Scepter's ring (950, AI_186).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        /// <summary>An Arc Surge bolt: what is drawn (the bolt and its forks), what it hits along (the main path), their box.</summary>
        sealed class Arc
        {
            public List<LightningGenerator.Bolt> Look;
            public Vector2[] Hit;
            public Vector2 HitMin, HitMax;
            /// <summary>The hand the drawn bolt was last pulled to (StormLightningParticle.AnchorToPlayerHand).</summary>
            public Vector2 Anchor;
            public int Life;
        }

        static readonly Dictionary<Proj, Arc> arcs = new Dictionary<Proj, Arc>();
        static readonly Color ArcSurgeColor = new Color(1f, 60f / 255f, 60f / 255f);
        /// <summary>Spawn_ArcLightning's life (16 ticks).</summary>
        const int ArcSurgeLife = 16;

        static void RegisterBatch1021()
        {
            // P:10439-10450: 16×16, magic, through blocks, one tick (it hits as it dies), one hit in 10 ticks for all of them.
            specs[ProjKind.ArcSurgeBolt] = new Spec
            {
                Ai = Ai.Custom, W = 16, H = 16, Time = ArcSurgeLife + 2, Penetrate = -1, TileCollide = false, OwnMove = true, TypeCooldown = 10, Look = Look.Invisible,
                CanDamage = p => p.Updates == 1 && arcs.ContainsKey(p),
                HitArea = p => ArcArea(p), HitTest = (p, col) => ArcTouches(p, col),
                OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.RedLightning, 60 * Random.Range(4, 8)),
                Update = (t, p) => StepArcSurge(p), Draw = (t, fx, p, px) => DrawArcSurge(fx, p), OnKill = (t, p) => arcs.Remove(p),
            };
            // P:7690-7695 (DefaultToSpear, 18×18); AI_019's terms for 730 (P:46992-46996).
            specs[ProjKind.ThunderSpearThrust] = new Spec
            {
                Ai = Ai.Spear, Texture = "Projectile_730", W = 18, H = 18, Time = 3600, Penetrate = -1, TileCollide = false, OwnerHitCheck = true,
                SpearOffset = new Vector3(8f, 1.2f, 1.5f), Update = (t, p) => t.StepThunderSpear(p),
            };
            // P:7707-7716: 15×15, melee, once, meets blocks; aiStyle 44 (P:28306-28322) fades it out in 21 ticks.
            specs[ProjKind.ThunderSpearShot] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_732", W = 15, H = 15, Time = 3600, Penetrate = 1, Frames = 4, Light = new Color(0.3f, 0.6f, 0.8f),
                Update = (t, p) => StepThunderSpearShot(t, p), Draw = (t, fx, p, px) => DrawThunderSpearShot(fx, p, px), OnKill = (t, p) => KillThunderSpearShot(p),
            };
            Lance(ProjKind.JoustingLanceThrust, 877, ItemType.JoustingLance);
            Lance(ProjKind.ShadowJoustingLanceThrust, 878, ItemType.ShadowJoustingLance);
            Lance(ProjKind.HallowLance, 879, ItemType.HallowJoustingLance);
            // P:7995-8006: an arrow of 10×10, two updates a tick, unseen (hide): only its blood shows.
            specs[ProjKind.BloodRainArrow] = new Spec
            {
                Ai = Ai.Arrow, Texture = "Projectile_819", W = 10, H = 10, Time = 1200, Extra = 1, Look = Look.Invisible, AlphaStart = 1f, AlphaFade = 20f / 255f,
                Update = (t, p) => StepBloodRainArrow(p), OnKill = (t, p) => KillBloodRainArrow(p),
            };
            // P:8958-8969: 104×104, magic, through blocks and everything, 2% less with each hit (P:13575), pushing away from the player (P:13232).
            specs[ProjKind.PrincessRing] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_950", W = 104, H = 104, Time = 180, Penetrate = -1, TileCollide = false, OwnMove = true, AlphaStart = 1f,
                Falloff = 0.98f, PushFromPlayer = true,
                Update = (t, p) => t.StepPrincessRing(p), Draw = (t, fx, p, px) => DrawPrincessRing(fx, p, px),
            };
        }

        // ------------------------------------------------------------------ 1122: the Arc Surge

        /// <summary>
        /// Player.ItemCheck_Shoot for 6173 (Pl:49668-49701; Terraria px, y down, from the player's centre): a bolt to the cursor,
        /// kept inside the reachable 1920×1200 and at least 20 updates of its speed away, 20 px astray; then up to two more at
        /// enemies ahead of the aim in the 1000×800 round the player (GetSparkleGuitarTarget, picked at random), each ending
        /// where a line walked to it in 8 px steps first comes within 16 px.
        /// </summary>
        public void FireArcSurge(Vector2 from, Vector2 mouse, Vector2 velocity, float speed, int damage, float knockback, float crit)
        {
            var dir = velocity.sqrMagnitude > 1e-6f ? Flip(velocity).normalized : Vector2.right;
            var point = new Vector2((mouse.x - from.x) / Px, (from.y - mouse.y) / Px);
            float k = 1f;
            if (Mathf.Abs(point.x) > 960f) k = Mathf.Min(k, 960f / Mathf.Abs(point.x));
            if (Mathf.Abs(point.y) > 600f) k = Mathf.Min(k, 600f / Mathf.Abs(point.y));
            point *= k;
            float least = speed * 20f, now = point.magnitude;
            if (now < least) point += dir * (least - now);
            point += Random.insideUnitCircle * 20f;
            var aim = Flip(dir);
            ShootArcSurge(from + Flip(point) * Px, aim, damage, knockback, crit);
            var start = TerraParticles.ToPx(from);
            var targets = new List<Vector2>();
            foreach (var hm in EnemiesNear(start, 640f))
            {
                var c = TerraParticles.ToPx(Centre(hm));
                if (Mathf.Abs(c.x - start.x) <= 500f && Mathf.Abs(c.y - start.y) <= 400f) targets.Add(c);
            }
            for (int tries = 0, made = 0; made < 2 && tries < targets.Count; tries++)
            {
                var target = targets[Random.Range(0, targets.Count)];
                if (Vector2.Dot((target - start).normalized, dir) < 0.5f) continue;
                var at = start;
                var step = Vector2.MoveTowards(Vector2.zero, target - start, 8f);
                for (int n = 0; n < 400 && InSight(at, at + step) && Vector2.Distance(at, target) > 8f; n++) at += step;
                if (Vector2.Distance(at, target) > 16f) continue;
                made++;
                ShootArcSurge(TerraParticles.ToWorld(at), aim, damage, knockback, crit);
            }
        }

        /// <summary>One Arc Surge bolt ending at a point: AI_203's one tick, then Kill's bolt (the main path from the hand hit along, the drawn one with its forks).</summary>
        void ShootArcSurge(Vector2 at, Vector2 aim, int damage, float knockback, float crit)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var p = Spawn(ProjKind.ArcSurgeBolt, at, (aim.sqrMagnitude > 1e-6f ? aim.normalized : Vector2.right) * 0.001f, damage, knockback, crit);
            if (p == null) return;
            int life=TerraMod.Instance?.SlowMagicUse==true?ArcSurgeLife*3:ArcSurgeLife;
            p.TimeLeft=life+2;
            uint seed = (uint)Random.Range(0, int.MaxValue);
            Vector2 hand = ArcHand(hero, aim), end = TerraParticles.ToPx(at);
            var gen = LightningGenerator.ArcSurge(hand, end);
            var main = gen.MainPath(seed, end, end - hand);
            var look = new List<LightningGenerator.Bolt>();
            gen.Generate(look, seed, end, end - hand);
            var arc = new Arc { Look = look, Hit = main.Positions, HitMin = main.Positions[0], HitMax = main.Positions[0], Anchor = hand, Life = life };
            foreach (var q in main.Positions)
            {
                arc.HitMin = Vector2.Min(arc.HitMin, q);
                arc.HitMax = Vector2.Max(arc.HitMax, q);
            }
            arcs[p] = arc;
            // Spawn_ArcLightning: the sound at the hand (pitch -0.25..0.15), the light at the end, the electric motes along it.
            TerraMod.Instance?.PlaySound("Item_203", 0.6f, Random.Range(-1f, 1f) * 0.2f - 0.05f);
            ArcSurgeDust(look.Count > 0 ? look[look.Count - 1] : main, seed);
            FxParticles.ArcSurgeHand(hand, Mathf.Atan2(end.y - hand.y, end.x - hand.x) + Mathf.PI / 2f, life);
        }

        /// <summary>Where a staff's bolt leaves the hand (Player.HandPosition for useStyle 5): out along the aim from the shoulder.</summary>
        static Vector2 ArcHand(HeroController hero, Vector2 aim)
        {
            var centre = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var d = Flip(aim.sqrMagnitude > 1e-6f ? aim.normalized : Vector2.right);
            return centre + new Vector2(0f, -2f) + d * 12f;
        }

        /// <summary>StormLightningParticle.EmitSpawnDust with a hand to hold: electric motes (278's frame) flung off the main bolt.</summary>
        static void ArcSurgeDust(LightningGenerator.Bolt bolt, uint seed)
        {
            var pts = bolt.Positions;
            var pp = TerraParticles.Instance;
            if (pp == null || pts.Length < 12) return;
            var rot = LightningGenerator.CalcRotations(pts);
            float every = Mathf.Lerp(2f, 5f, Mathf.InverseLerp(100f, 500f, Vector2.Distance(pts[pts.Length - 1], pts[0])));
            int chance = Mathf.CeilToInt(pts.Length / every);
            var rng = new System.Random((int)(seed & 0x7fffffff));
            for (int i = 5; i < pts.Length - 5; i++)
            {
                if (rng.Next(chance) != 0) continue;
                var v = -new Vector2(Mathf.Cos(rot[i]), Mathf.Sin(rot[i])) * (3f + (float)rng.NextDouble() * 6.5f) * 1.2f;
                int d = pp.NewDust(pts[i], 0, 0, 278);
                if (d < 0) continue;
                pp[d].Position = pts[i] - v * 2f;
                pp[d].Velocity = v;
                pp[d].Color = ArcSurgeColor;
                pp[d].Scale = 0.7f + (float)rng.NextDouble() * 0.5f;
                pp[d].NoGravity = true;
                pp[d].FadeIn = 0f;
                var white = pp[d];
                int w = pp.NewDust(pts[i], 0, 0, 278);
                if (w < 0) continue;
                white.Scale *= 0.66f;
                white.Color = Color.white;
                pp[w] = white;
            }
        }

        /// <summary>AI_203's light while it lives: rising with the bolt's age.</summary>
        static void StepArcSurge(Proj p)
        {
            if (!arcs.TryGetValue(p, out var arc) || arc.Hit.Length == 0) return;
            var fx = FxBatch.Instance;
            float t = (p.Updates - 1) / (float)arc.Life;
            float light = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((1f - t) / 0.3f);
            if (fx != null && light > 0.01f)
                for (int i = 0; i < arc.Hit.Length; i += 20) fx.Light(arc.Hit[i], ArcSurgeColor * light, 3f, weaponEffect: true);
        }

        /// <summary>Its hit area: the main path's box, grown by the 16 px every point has (MultiPointHitbox).</summary>
        static (Vector2, Vector2) ArcArea(Proj p)
        {
            if (!arcs.TryGetValue(p, out var arc)) return (p.Pos, Vector2.zero);
            Vector2 min = TerraParticles.ToWorld(arc.HitMin), max = TerraParticles.ToWorld(arc.HitMax);
            var size = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y)) + Vector2.one * 16f * Px;
            return ((min + max) * 0.5f, size);
        }

        /// <summary>MultiPointHitbox.Intersects: a point of the path within 8 px of the box.</summary>
        static bool ArcTouches(Proj p, Collider2D col)
        {
            if (!arcs.TryGetValue(p, out var arc)) return false;
            var b = col.bounds;
            b.Expand(16f * Px);
            foreach (var q in arc.Hit)
            {
                var w = TerraParticles.ToWorld(q);
                if (w.x >= b.min.x && w.x <= b.max.x && w.y >= b.min.y && w.y <= b.max.y) return true;
            }
            return false;
        }

        /// <summary>
        /// StormLightningParticle.Draw: the bolt pulled after the hand (more at the hand, nothing at the end), drawn as
        /// StormLightningDrawer's ArcSurge strip, 12 px wide at three quarters' strength.
        /// </summary>
        static void DrawArcSurge(FxBatch fx, Proj p)
        {
            if (!arcs.TryGetValue(p, out var arc) || arc.Look.Count == 0) return;
            var hero = HeroController.SilentInstance;
            var main = arc.Look[arc.Look.Count - 1];
            if (hero != null && main.Positions.Length > 1)
            {
                var hand = ArcHand(hero, Flip(main.Positions[main.Positions.Length - 1] - main.Positions[0]));
                Vector2 first = main.Positions[0], last = main.Positions[main.Positions.Length - 1];
                float span = Mathf.Max(1f, (last - first).sqrMagnitude);
                var move = hand - arc.Anchor;
                if (move.sqrMagnitude > 0.01f)
                {
                    foreach (var b in arc.Look)
                        for (int i = 0; i < b.Positions.Length; i++)
                        {
                            float k = Mathf.Clamp01(1f - (b.Positions[i] - first).sqrMagnitude / span);
                            b.Positions[i] += k * k * move;
                        }
                    arc.Anchor = hand;
                }
            }
            float progress = Mathf.Clamp01((p.Updates - 1) / (float)arc.Life);
            LightningGenerator.Draw(fx, arc.Look, progress, ArcSurgeColor, 12f, 0.75f, LightningGenerator.ArcSurgeAnim);
        }

        // ------------------------------------------------------------------ 730 / 732: the Storm Spear

        /// <summary>AI_019_Spears for 730 (P:46744-46750, 46847-46858): its bolt at the thrust's start, 4× the spear's speed and 1.6× its damage; electric motes off it.</summary>
        void StepThunderSpear(Proj p)
        {
            if (p.Dead) return;
            if (p.LocalAi0 == 0f)
            {
                p.LocalAi0 = 1f;
                var shot = Spawn(ProjKind.ThunderSpearShot, p.Pos, p.Vel * 4f, (int)(p.Damage * 1.6f), p.Knockback * 0.5f, p.Crit);
                if (shot != null) shot.Melee = true;
            }
            var box = CenterPx(p) - new Vector2(9f, 9f);
            int i;
            if (Random.Range(0, 5) == 0 && (i = BoxDust(box, 18, 18, 226, 0f, 0f, 150, 0.7f)) >= 0)
            {
                D(i).NoGravity = true;
                D(i).Velocity *= 1.4f;
            }
            if (Random.Range(0, 5) == 0 && (i = BoxDust(box, 18, 18, 226, 0f, 0f, 150, 0.5f)) >= 0) D(i).Velocity.y -= 0.5f;
        }

        /// <summary>aiStyle 44 for 732: 12 more alpha a tick until it's gone, a frame every 3 ticks, a mote one tick in five, a pale blue light.</summary>
        static void StepThunderSpearShot(TerraProjectiles t, Proj p)
        {
            p.Alpha += 12f / 255f;
            if (p.Alpha > 1f)
            {
                t.Kill(p);
                return;
            }
            int i;
            if (Random.Range(0, 5) == 0 && (i = BoxDust(CenterPx(p) - new Vector2(7.5f, 7.5f), 15, 15, 226, 0f, 0f, 150, 0.8f)) >= 0)
            {
                D(i).NoGravity = true;
                D(i).Velocity *= 1.2f;
            }
            if (++p.FrameCounter >= 3)
            {
                p.FrameCounter = 0;
                p.Frame = (p.Frame + 1) % 4;
            }
            var v = TV(p);
            p.TRot = Mathf.Atan2(v.y, v.x);
        }

        /// <summary>Main.DrawProj's generic branch: its frame of four, turned along its flight, GetAlpha's near-white at 25 (P:81134-81138), fading.</summary>
        static void DrawThunderSpearShot(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = Sprite(p.Spec.Texture).rect;
            int w = (int)whole.width, h = (int)whole.height / 4;
            float o = 1f - Mathf.Clamp01(p.Alpha);
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, p.Frame * h, w, h, w * 0.5f, h * 0.5f), px, FxBatch.C(248, 248, 248, 25) * o, p.TRot, p.Scale);
        }

        /// <summary>P:74866-74880: five to nine motes thrown along its flight.</summary>
        static void KillThunderSpearShot(Proj p)
        {
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            var c = CenterPx(p);
            var v = TV(p);
            int n = Random.Range(5, 10);
            for (int k = 0; k < n; k++)
            {
                int i = pp.NewDust(c, 0, 0, 226, 0f, 0f, 100, 0.5f);
                if (i < 0) continue;
                pp[i].Velocity *= 1.6f;
                pp[i].Velocity.y -= 1f;
                pp[i].Velocity = pp[i].Velocity * 0.5f + v * (k / (float)n) * 0.7f;
                pp[i].Position = Vector2.Lerp(pp[i].Position, c, 0.5f);
                pp[i].NoGravity = true;
            }
        }

        // ------------------------------------------------------------------ 877-879: the jousting lances

        static void Lance(ProjKind kind, int texture, ItemType item) => specs[kind] = new Spec
        {
            // P:8432-8438: 25×25, DefaultToSpear, faded in from 255.
            Ai = Ai.Custom, Texture = "Projectile_" + texture, W = 25, H = 25, Time = 3600, Penetrate = -1, TileCollide = false, OwnMove = true, OwnerHitCheck = true,
            Held = true, AlphaStart = 1f,
            Update = (t, p) => t.StepLance(p, item), Draw = (t, fx, p, px) => DrawSpear(fx, p, px), OnKill = (t, p) => { },
            // Colliding (P:15679-15695): the shaft, 95 px back from its centre towards the player and 23 px across.
            Beam = p =>
            {
                var c = TerraParticles.ToPx(p.Pos);
                return (c, c - TV(p).normalized * 95f, 23f * p.Scale);
            },
            // Projectile.Damage (P:14536-14539, 12861-12865): its damage 0.1 + 0.9 of the player's speed over 7, its knockback that speed over 7.
            ModifyHit = (t, p, hm, damage, crit) => (Mathf.Max(1, Mathf.RoundToInt(damage * LanceMultiplier(HeroVelocity()))), crit),
            KnockbackScale = p => LanceSpeed() / 7f,
        };

        /// <summary>The player's speed in Terraria px a tick.</summary>
        static float LanceSpeed() => HeroVelocity().magnitude / Px / 60f;

        static Vector2 HeroVelocity()
        {
            var hero = HeroController.SilentInstance;
            return hero == null ? Vector2.zero : hero.GetComponent<Rigidbody2D>().linearVelocity;
        }

        public static bool IsLance(ProjKind kind) => kind == ProjKind.JoustingLanceThrust || kind == ProjKind.ShadowJoustingLanceThrust || kind == ProjKind.HallowLance;

        /// <summary>
        /// AI_019_Spears for a lance (P:46718-46728, 46797-46800, 46810-46845): out from 28 to 34.4 times its speed over the use's
        /// first two thirds, held there while the button is (SetDummyItemTime), back over the last third once it's let go; the
        /// Shadow and Hallowed lances shed their dust when the player charges along them. Player.Hurt takes it away (Pl:38554).
        /// </summary>
        void StepLance(Proj p, ItemType item)
        {
            var hero = HeroController.SilentInstance;
            var mod = TerraMod.Instance;
            if (hero == null || mod == null)
            {
                Kill(p);
                return;
            }
            if (p.Updates <= 1) p.Timer2 = Time.time;
            else if (TerraHurt.LastHurtAt > p.Timer2)
            {
                Kill(p);
                return;
            }
            if (p.Ai1 <= 0f) p.Ai1 = Mathf.Max(3, Mathf.RoundToInt(ItemDb.Get(item).UseAnimation * 60f));
            int N = (int)p.Ai1, T = N / 3;
            bool held = mod.UseHeld && mod.Inventory.Current.Type == item;
            int a = N - 1 - (int)p.Ai0;
            if (held && a < T)
            {
                a = T;
                p.Ai0 = N - 1 - T;
            }
            float f = 28f + 0.4f * ((N - T) - Mathf.Max(0, a - T)) - 0.4f * (T - Mathf.Min(a, T));
            Vector2 centre = hero.GetComponent<BoxCollider2D>().bounds.center;
            p.Pos = centre + p.Vel * f * Px;
            p.Anim = a;
            p.Alpha = Mathf.Max(0f, p.Alpha - 40f / 255f);
            if (p.Kind != ProjKind.JoustingLanceThrust)
            {
                var run = hero.GetComponent<Rigidbody2D>().linearVelocity;
                float speed = run.magnitude / Px / 60f;
                float along = Vector2.Dot(p.Vel.normalized, run.sqrMagnitude > 1e-6f ? run.normalized : Vector2.right * p.Dir);
                if (speed > 6f && along > 0.8f)
                {
                    int every = speed > 8f ? 2 : speed > 7f ? 5 : 8;
                    bool hallow = p.Kind == ProjKind.HallowLance;
                    int a1 = hallow ? 43 : 14, a2 = hallow ? 57 : 27;
                    var box = CenterPx(p) - new Vector2(4f, 4f);
                    var v = TV(p);
                    if (Random.Range(0, every + 3) == 0) BoxDust(box, 8, 8, a1, 0f, 0f, 150, 1.4f);
                    if (Random.Range(0, every) == 0)
                    {
                        int i = BoxDust(box, 8, 8, a2, v.x * 0.2f + p.Dir * 3f, v.y * 0.2f, 100, 1.2f);
                        if (i >= 0)
                        {
                            D(i).NoGravity = true;
                            D(i).Velocity *= 0.25f;
                        }
                        if ((i = BoxDust(box, 8, 8, a2, 0f, 0f, 150, 1.4f)) >= 0) D(i).Velocity *= 0.25f;
                    }
                }
            }
            if (++p.Ai0 >= N && !held) Kill(p);
        }

        // ------------------------------------------------------------------ 819: the Blood Rain Bow's arrow

        /// <summary>AI_001 for 819 (P:57369-57406): a burst of blood as it starts; five drops along its flight and a red glint.</summary>
        static void StepBloodRainArrow(Proj p)
        {
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            var c = CenterPx(p);
            var v = TV(p);
            var box = c - new Vector2(5f, 5f);
            int i;
            if (p.LocalAi0 == 0f)
            {
                p.LocalAi0 = 1f;
                for (int k = 0; k < 8; k++)
                {
                    if ((i = BoxDust(box, 10, 10, 5, v.x, v.y, 100)) < 0) continue;
                    float a = Random.Range(-1f, 1f) * Mathf.PI;
                    D(i).Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2f + (v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.zero) * 2f;
                    D(i).Scale = 0.9f;
                    D(i).FadeIn = 1.3f;
                    D(i).Position = c;
                }
            }
            for (int k = 1; k < 6; k++)
            {
                if ((i = BoxDust(box, 10, 10, 5, 0f, 0f, 100)) < 0) continue;
                D(i).Velocity = (D(i).Velocity * 0.5f + v * 0.5f) * 0.25f;
                D(i).Scale = 0.9f;
                D(i).Position = c - v * k / 5f;
            }
            if (Random.Range(0, 2) == 0 && (i = pp.NewDust(c - v * 3f, 0, 0, 267)) >= 0)
            {
                D(i).Position = c - v * 3f;
                D(i).Velocity = v * 0.5f;
                D(i).Color = Color.red * (Random.value * 0.3f + 0.1f);
                D(i).NoGravity = true;
                D(i).Scale = 0.7f;
            }
        }

        /// <summary>P:74030-74039: the dig sound and twenty drops of blood.</summary>
        static void KillBloodRainArrow(Proj p)
        {
            Sound("Dig_" + Random.Range(0, 3));
            var box = CenterPx(p) - new Vector2(5f, 5f);
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(box, 10, 10, 5);
                if (i < 0) continue;
                D(i).Scale = 1f + Random.value * 0.5f;
                D(i).Velocity *= 1.5f;
            }
        }

        // ------------------------------------------------------------------ 950: the Resonance Scepter's ring

        /// <summary>
        /// Player.ItemCheck_Shoot for 5065 (Pl:48179-48184) with GetFarthestSpawnPositionOnLine: the ring at the cursor (kept
        /// inside the reachable 1920×1200), or where the line out to it at the shot's speed a step first leaves the sight of a
        /// point 16 px ahead of the player.
        /// </summary>
        public void FirePrincessRing(Vector2 from, Vector2 mouse, Vector2 velocity, int damage, float knockback, float crit)
        {
            var start = TerraParticles.ToPx(from);
            var cursor = new Vector2((mouse.x - from.x) / Px, (from.y - mouse.y) / Px);
            float k = 1f;
            if (Mathf.Abs(cursor.x) > 960f) k = Mathf.Min(k, 960f / Mathf.Abs(cursor.x));
            if (Mathf.Abs(cursor.y) > 600f) k = Mathf.Min(k, 600f / Mathf.Abs(cursor.y));
            cursor = start + cursor * k;
            var step = Flip(velocity);
            float speed = step.magnitude, far = (cursor - start).magnitude;
            var eye = start + new Vector2(velocity.x < 0f ? -16f : 16f, 0f);
            var at = start;
            for (int n = 1; n <= 400 && InSight(eye, at); n++)
            {
                at += step;
                if ((at - cursor).magnitude < 20f + Mathf.Abs(step.x) + Mathf.Abs(step.y) || speed * n >= far)
                {
                    at = cursor;
                    break;
                }
            }
            Spawn(ProjKind.PrincessRing, TerraParticles.ToWorld(at), Vector2.zero, damage, knockback, crit);
        }

        /// <summary>
        /// AI_186_PrincessWeapon (P:43454-43575): a 60 tick ring swelling out and fading, its inner echo a step behind and
        /// the whole of it shrinking over the last 15; pink motes (267) streaming round its edge both ways and sparkles off it,
        /// and a burst of sparkles out four ways at tick 50.
        /// </summary>
        void StepPrincessRing(Proj p)
        {
            const float n = 60f;
            p.Ai0++;
            if (p.Ai0 >= n)
            {
                Kill(p);
                return;
            }
            float opacity = Remap(p.Ai0, 0f, n, 1f, 0f);
            p.Alpha = 1f - opacity;
            float t = p.Ai0 / n;
            float e1 = 1f - (1f - t) * (1f - t), e2 = 1f - (1f - e1) * (1f - e1);
            float shrink = Remap(p.Ai0, n - 15f, n, 0f, 1f);
            float keep = 1f - shrink * shrink;
            p.Scale = (0.4f + 0.6f * e2) * keep;
            float late = Remap(p.Ai0, 20f, n, 0f, 1f);
            float l1 = 1f - (1f - late) * (1f - late), l2 = 1f - (1f - l1) * (1f - l1);
            p.LocalAi0 = (0.4f + 0.6f * l2) * keep;
            var pp = TerraParticles.Instance;
            var c = CenterPx(p);
            float radius = 52f * p.Scale;
            var pink = FxParticles.HslToRgb(0.92f, 1f, 0.5f) * opacity;
            if (t < 0.9f && pp != null)
                for (int side = -1; side <= 1; side += 2)
                    for (int k = 0; k < 3; k++)
                    {
                        if (Random.Range(0, 2) != 0) continue;
                        float a = Random.value * Mathf.PI * 2f;
                        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        var turn = new Vector2(Mathf.Cos(a + Mathf.PI / 4f), Mathf.Sin(a + Mathf.PI / 4f));
                        var at = c + dir * radius + turn * 6f * side;
                        int i = pp.NewDust(at, 0, 0, 267, 0f, 0f, 0);
                        if (i < 0) continue;
                        pp[i].Position = at;
                        pp[i].Color = pink;
                        pp[i].NoGravity = true;
                        pp[i].Scale = 0.3f * p.Scale;
                        pp[i].FadeIn = Random.value * 1.2f * p.Scale;
                        pp[i].Velocity = turn * p.Scale * 2f * -side;
                        if (side < 0) pp[i].Position += pp[i].Velocity * -5f;
                        var twin = pp[i];
                        int j = pp.NewDust(at, 0, 0, 267);
                        if (j < 0) continue;
                        twin.Scale /= 2f;
                        twin.FadeIn *= 0.85f;
                        twin.Color = Color.white;
                        pp[j] = twin;
                    }
            if (t < 0.95f && Random.Range(0, 4) == 0)
            {
                float a = Random.value * Mathf.PI * 2f + Mathf.PI / 2f;
                FxParticles.PrincessWeapon(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, Vector2.zero);
            }
            if (Mathf.Approximately(p.Ai0, n - 10f))
                for (float k = 0f; k < 1f; k += 0.25f)
                {
                    var dir = new Vector2(Mathf.Cos(Mathf.PI * 2f * k), Mathf.Sin(Mathf.PI * 2f * k));
                    FxParticles.PrincessWeapon(c, dir);
                    FxParticles.PrincessWeapon(c, dir * 2f);
                }
        }

        /// <summary>Main.DrawProj for 950 (M:28777-28800): white and pink rings added, each twice (a little smaller), at the echo's and its own size.</summary>
        static void DrawPrincessRing(FxBatch fx, Proj p, Vector2 px)
        {
            var ring = FxBatch.Whole(p.Spec.Texture);
            float opacity = 1f - Mathf.Clamp01(p.Alpha);
            var white = new Color(1f, 1f, 1f, 0f) * opacity * 0.6f;
            var pink = FxParticles.HslToRgb(0.92f, 1f, 0.5f) * opacity;
            pink.a = 0f;
            pink *= 0.6f;
            fx.Draw(ring, px, white, 0f, p.LocalAi0);
            fx.Draw(ring, px, pink, 0f, p.LocalAi0 * 0.95f);
            fx.Draw(ring, px, white, 0f, p.Scale);
            fx.Draw(ring, px, pink, 0f, p.Scale * 0.95f);
        }
    }
}
