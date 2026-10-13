using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The accessories' projectiles (accarmor-hm sec-G2, sec-G3; Terraria's space, px y down):
    /// the Star Cloak family's other stars (725, 724, 723, 1133), the Hive Pack's giant bee
    /// (566), the Brain of Confusion's illusion (565), the Spore Sac's spores and clouds
    /// (567-571), the Volatile Gelatin's ball (937), the Bone Glove's bone (532), the Bone
    /// Helm's shadow hand (964) and the Mana Cloak's star pickup (item 4143); the Harpy Charm's
    /// arrow redirect; and the accessories' part in a projectile's hit (the snapping stones, the
    /// barbs, the Catalyst Band, the Lucky Coin, the Mana Cloak's star, the Templar's Sling).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterAccessories()
        {
            // P:7648-7659: every star of the family is the Star Cloak's (24×24, AI_005, through two, AP 25).
            foreach (var (kind, tex) in new[] { (ProjKind.StarVeilStar, 725), (ProjKind.BeeCloakStar, 724), (ProjKind.ManaCloakStar, 723), (ProjKind.SnakeCloakStar, 1133) })
                specs[kind] = new Spec
                {
                    Ai = Ai.CloakStar, Texture = "Projectile_" + tex, W = 24, H = 24, Time = 3600, Penetrate = 2, LocalCooldown = 10, AlphaStart = 50f / 255f, TileCollide = false, ArmorPen = 25,
                    OnKill = (t, p) => t.KillCloakStar(p),
                };
            // P:6174-6186: the giant bee, through four, sharing the small bee's immunity (UseGroupStaticImmunity(181)).
            specs[ProjKind.GiantBee] = new Spec
            {
                Ai = Ai.Bee, Texture = "Projectile_566", W = 16, H = 16, Time = 660, Penetrate = 4, Extra = 3, AlphaStart = 1f, TypeCooldown = 10, TypeGroup = ProjKind.Bee,
                Frames = 4, Origin = new Vector2(14f, 12f), OnKill = (t, p) => KillBee(p),
            };
            // P:6163-6173: the illusion (aiStyle 104), harmless; 4 frames of 36.
            specs[ProjKind.BrainIllusion] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_565", W = 28, H = 28, Scale = 0.8f, Time = 3600, Penetrate = -1, TileCollide = false, AlphaStart = 1f, OwnMove = true,
                CanDamage = p => false, Update = (t, p) => StepBrainIllusion(p), Draw = (t, fx, p, px) => DrawFramed(fx, p, px, 4, FxBatch.C(255, 255, 255) * (1f - p.Alpha)), OnKill = (t, p) => { },
            };
            // P:6187-6220: the spores (14 or 16 px, AI_105) and their clouds (32 px, AI_106), each with its group's immunity.
            specs[ProjKind.Spore] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_567", W = 14, H = 14, Time = 3600, AlphaStart = 1f, TileCollide = false, TypeCooldown = 10,
                Update = (t, p) => StepSpore(p), Draw = (t, fx, p, px) => DrawSpore(fx, p, px), OnKill = (t, p) => t.KillSpore(p),
                OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, 60 * Random.Range(5, 11)),
            };
            specs[ProjKind.SacCloud] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_569", W = 32, H = 32, Time = 3600, Penetrate = -1, AlphaStart = 1f, TileCollide = false, TypeCooldown = 10,
                Update = (t, p) => StepSacCloud(p), Draw = (t, fx, p, px) => DrawSacCloud(fx, p, px), OnKill = (t, p) => { },
            };
            // P:8907-8918: the Volatile Gelatin's ball (AI_001), through three, 10 ticks of immunity to all of them.
            specs[ProjKind.GelBall] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_937", W = 10, H = 10, Time = 3600, Penetrate = 3, TypeCooldown = 10, AlphaStart = 1f,
                Update = (t, p) => StepGelBall(p), Draw = (t, fx, p, px) => DrawTurningLit(fx, p, px, 0.75f), TileHit = (t, p, x, y, v) => GelBallBounce(t, p, x, y, v), OnKill = (t, p) => KillGelBall(p),
            };
            // P:5986-5997: the Bone Glove's bone (AI_001), two updates a tick, through three, local immunity 5, AP 25.
            specs[ProjKind.BoneGloveBone] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_532", W = 16, H = 16, Time = 3600, Penetrate = 3, Extra = 1, LocalCooldown = 5, ArmorPen = 25,
                Update = (t, p) => StepBone(p), Draw = (t, fx, p, px) => DrawTurningLit(fx, p, px, 1f), TileHit = (t, p, x, y, v) => BoneBounce(t, p, x, y, v), OnKill = (t, p) => KillBone(p),
            };
            // P:9129-9145: the Bone Helm's shadow hand (AI_187), 40×40 at 0.7, through three, each enemy once, AP 20.
            specs[ProjKind.ShadowHand] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_964", W = 40, H = 40, Scale = 0.7f, Time = 3600, Penetrate = 3, Extra = 1, OncePerNpc = true, AlphaStart = 1f, TileCollide = false, ArmorPen = 20,
                TrailLength = 5, Update = (t, p) => StepShadowHand(p), Draw = (t, fx, p, px) => DrawShadowHand(fx, p, px), OnKill = (t, p) => { },
            };
            // Item 4143: the Mana Cloak's star, a pickup with no gravity (I:35962-35965).
            specs[ProjKind.ManaCloakPickup] = new Spec
            {
                Ai = Ai.Custom, Texture = "Item_4143", W = 12, H = 12, Time = 36000, Penetrate = -1, TileCollide = false, OwnMove = true,
                CanDamage = p => false, Update = (t, p) => StepManaPickup(p), Draw = (t, fx, p, px) => DrawManaPickup(fx, p, px), OnKill = (t, p) => { },
            };
        }

        // ------------------------------------------------------------------ which projectiles are what

        static readonly List<HealthManager> accNear = new List<HealthManager>();

        static HashSet<ProjKind> arrowKinds;

        /// <summary>Projectile.arrow: the arrows the bows shoot (every arrow ammo's projectile, the Molten Quiver's flaming one).</summary>
        static bool IsArrow(ProjKind kind)
        {
            if (arrowKinds == null)
            {
                // The arrows the bows make their own (arrow = true in SetDefaults).
                arrowKinds = new HashSet<ProjKind> { ProjKind.FlamingArrow, ProjKind.BetsyArrow, ProjKind.BeeArrow, ProjKind.HellwingBat, ProjKind.EventideArrow };
                foreach (var d in ItemDb.All) if (d.Ammo == AmmoKind.Arrow && d.Shoot != ProjKind.None) arrowKinds.Add(d.Shoot);
            }
            return arrowKinds.Contains(kind);
        }

        static readonly HashSet<ProjKind> sentryShots = new HashSet<ProjKind>
        {
            ProjKind.FlameburstShot1, ProjKind.FlameburstShot2, ProjKind.FlameburstShot3, ProjKind.BallistaBolt, ProjKind.FrostBlast, ProjKind.HoundiusFireball, ProjKind.BarnacleShot,
        };

        /// <summary>A projectile's damage class: a melee item's shot, a minion's, or what shot it.</summary>
        static DamageClass ClassOf(Proj p) => p.Melee ? DamageClass.Melee : p.MinionShot ? DamageClass.Summon : p.Class;

        // ------------------------------------------------------------------ the Star Cloak family (AI_005)

        /// <summary>A star of the Star Cloak family, through everything until below topY; the Mana Cloak's from a magic hit drops its star where it ends.</summary>
        public void ShootCloakStar(ProjKind kind, Vector2 from, Vector2 velocity, int damage, float topY, bool manaDrop = false)
        {
            var p = Spawn(kind, from, velocity, damage, 5f, 4f);
            if (p == null) return;
            p.Ai1 = topY;
            p.ManaDrop = manaDrop;
        }

        /// <summary>The star the worn cloak drops (the last override wins: veil, bee, mana, snake).</summary>
        public static ProjKind CloakStarKind(CloakStar star) => star switch
        {
            CloakStar.Veil => ProjKind.StarVeilStar,
            CloakStar.Bee => ProjKind.BeeCloakStar,
            CloakStar.Mana => ProjKind.ManaCloakStar,
            CloakStar.Snake => ProjKind.SnakeCloakStar,
            _ => ProjKind.StarCloakStar,
        };

        /// <summary>
        /// P:76693-76740: Item10; the Mana Cloak's star ten blue sparks (88) and three star gores
        /// flung twice as fast, and its pickup when it came from a magic hit; the others ten of
        /// their dust (58, the Druidic cloak's 57) and three star gores.
        /// </summary>
        void KillCloakStar(Proj p)
        {
            Sound("Item_10");
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            var v = TV(p);
            var topLeft = CenterPx(p) - new Vector2(12f, 12f);
            if (p.Kind == ProjKind.ManaCloakStar)
            {
                for (int i = 0; i < 10; i++)
                {
                    int d = BoxDust(topLeft, 24, 24, 88, v.x * 0.1f, v.y * 0.1f, 150, 1.2f);
                    if (d < 0) continue;
                    D(d).NoGravity = true;
                    D(d).Velocity.x *= 2f;
                }
                for (int i = 0; i < 3; i++) pp.NewGore(topLeft, v * 0.05f * 2f, Random.Range(0, 3) == 0 ? 16 : 17, 1f, 1f, default, true);
                if (p.ManaDrop) Spawn(ProjKind.ManaCloakPickup, p.Pos, Vector2.zero, 0, 0f, 0f);
                return;
            }
            int dust = p.Kind == ProjKind.SnakeCloakStar ? 57 : 58;
            for (int i = 0; i < 10; i++) BoxDust(topLeft, 24, 24, dust, v.x * 0.1f, v.y * 0.1f, 150, 1.2f);
            for (int i = 0; i < 3; i++) pp.NewGore(topLeft, v * 0.05f, Random.Range(16, 18));
        }

        /// <summary>P:24242-24285: the flight's gores and dust by kind (the Bee Cloak's star has none).</summary>
        void CloakStarFlight(Proj p)
        {
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            var topLeft = CenterPx(p) - new Vector2(12f, 12f);
            var v = TV(p);
            switch (p.Kind)
            {
                case ProjKind.BeeCloakStar:
                    return;
                case ProjKind.ManaCloakStar:
                {
                    if (Random.Range(0, 6) == 0) pp.NewGore(topLeft, v * 0.2f, 17);
                    var c = CenterPx(p);
                    for (int k = 0; k < 3; k++)
                    {
                        float a = topLeft.y / 200f + k * Mathf.PI * 2f + p.TRot;
                        var at = c + new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * 12f * p.Scale - v * 0.5f;
                        int d = PointDust(at, 267, v * 0.2f * k);
                        if (d >= 0)
                        {
                            D(d).NoGravity = true;
                            D(d).Color = Color.blue;
                        }
                        d = PointDust(at, 267, v * 0.2f * k, 0, 0.4f);
                        if (d >= 0)
                        {
                            D(d).NoGravity = true;
                            D(d).Color = Color.white;
                        }
                    }
                    return;
                }
                default:
                {
                    if (Random.Range(0, 24) == 0) pp.NewGore(topLeft, v * 0.2f, Random.Range(16, 18));
                    int dust = 86;
                    if (p.Kind == ProjKind.SnakeCloakStar)
                    {
                        dust = 87;
                        int s = Random.Range(0, 6) == 0 ? BoxDust(topLeft, 24, 24, 89, 0f, 0f, 127) : -1;
                        if (s >= 0) D(s).NoGravity = true;
                    }
                    int d;
                    if (Random.Range(0, 3) == 0 && (d = BoxDust(topLeft, 24, 24, dust, 0f, 0f, 127)) >= 0)
                    {
                        D(d).Velocity *= 0.5f;
                        D(d).NoGravity = true;
                    }
                    return;
                }
            }
        }

        /// <summary>M:31862-32037: the family's streak colours and sizes (three turning Extra_91 streaks, two pulsing glows behind).</summary>
        static void CloakStarLook(ProjKind kind, out Color streak, out Color glow, out float[] scales, out float back)
        {
            back = 0f;
            float s = 0.5f;
            switch (kind)
            {
                case ProjKind.StarCloakStar:
                {
                    var c = FxBatch.C(180, 20, 255) * 0.3f;
                    streak = c;
                    glow = c;
                    streak.a = 60f / 255f;
                    s = 0.6f;
                    break;
                }
                case ProjKind.StarVeilStar:
                {
                    Color a = FxBatch.C(255, 80, 255), b = FxBatch.C(255, 255, 0);
                    streak = Color.Lerp(a, b, 0.2f) * 0.3f;
                    glow = Color.Lerp(a, b, 0.8f) * 0.4f;
                    streak.a = 50f / 255f;
                    break;
                }
                case ProjKind.BeeCloakStar:
                {
                    Color a = FxBatch.C(255, 165, 0) * 0.75f, b = FxBatch.C(255, 255, 0) * 0.5f;
                    streak = Color.Lerp(a, b, 0.2f) * 0.3f;
                    glow = Color.Lerp(a, b, 0.8f) * 0.4f;
                    streak.a = 0f;
                    break;
                }
                case ProjKind.SnakeCloakStar:
                {
                    Color a = FxBatch.C(255, 215, 60), b = FxBatch.C(70, 255, 40);
                    streak = Color.Lerp(a, b, 0.2f) * 0.32f;
                    glow = Color.Lerp(a, b, 0.8f) * 0.32f;
                    streak.a = 50f / 255f;
                    back = 8f;
                    break;
                }
                default:
                    // The Mana Cloak's: the default blue and added white, full size.
                    streak = Color.blue * 0.1f;
                    streak.a = 0f;
                    glow = Color.white * 0.3f;
                    glow.a = 0f;
                    s = 0f;
                    break;
            }
            scales = new[] { 1.5f - s, 1.1f - s, 1.3f - s };
        }

        // ------------------------------------------------------------------ 565: the Brain of Confusion's illusion (AI_104)

        /// <summary>The illusion by the player after a real hit (Pl:38720): somewhere above him, drifting off with a third of his speed.</summary>
        public void BrainIllusion(Vector2 centreWorld, Vector2 velocityPx)
        {
            var at = centreWorld + new Vector2(Random.Range(-40, 40), Random.Range(20, 60)) * Px;
            var p = Spawn(ProjKind.BrainIllusion, at, velocityPx * 0.3f, 0, 0f, 0f);
            if (p != null) p.Frame = 0;
        }

        /// <summary>P:34099-34131: swells and fades in, then shrinks and fades out; rises, slowing sideways; a frame every 7 ticks.</summary>
        static void StepBrainIllusion(Proj p)
        {
            if (p.Ai0 == 0f)
            {
                p.Scale *= 1.01f;
                p.Alpha -= 7f / 255f;
                if (p.Alpha < 0f)
                {
                    p.Alpha = 0f;
                    p.Ai0 = 1f;
                }
            }
            else
            {
                p.Scale *= 0.995f;
                p.Alpha += 3f / 255f;
                if (p.Alpha >= 250f / 255f)
                {
                    p.Dead = true;
                    return;
                }
            }
            if (++p.FrameCounter > 6)
            {
                p.FrameCounter = 0;
                p.Frame = (p.Frame + 1) % 4;
            }
            p.Vel.y += 0.03f;
            p.Vel.x *= 0.97f;
            p.Pos += p.Vel * Px;
        }

        static void DrawFramed(FxBatch fx, Proj p, Vector2 px, int frames, Color c)
        {
            var whole = Sprite(p.Spec.Texture).rect;
            int fh = (int)whole.height / frames;
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, p.Frame * fh, (int)whole.width, fh, whole.width * 0.5f, fh * 0.5f), px, c, p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 566: the giant bee

        /// <summary>P:76693 (the bees' kill): six honey motes.</summary>
        static void KillBee(Proj p)
        {
            for (int i = 0; i < 6; i++)
            {
                int d = DustBox(p, 150, Flip(p.Vel), 50, 1f, true);
                if (d >= 0) D(d).Scale = 1f;
            }
        }

        // ------------------------------------------------------------------ 567/568, 569-571: the Spore Sac

        /// <summary>
        /// Player.SporeSac (Pl:42733-42815), each tick: one try in 15, fewer the more spores are out
        /// (none past 10); fifty places tried within 200-400 px, open, in sight of the player's
        /// middle or top, near a block, and not within 48 px of another spore. 70 damage, kb 1.5.
        /// </summary>
        void SporeSacTick(Vector2 centrePx, Vector2 topPx)
        {
            if (Random.Range(0, 15) != 0) return;
            int n = 0;
            foreach (var q in live) if (q.Kind == ProjKind.Spore && !q.Dead) n++;
            if (Random.Range(0, 15) < n || n >= 10) return;
            for (int j = 0; j < 50; j++)
            {
                int r = Random.Range(200 - 2 * j, 400 + 2 * j);
                var c = centrePx + new Vector2(Random.Range(-r, r), Random.Range(-r, r));
                if (SolidIn(c, 24f)) continue;
                c += new Vector2(12f, 12f);
                if (!(InSight(centrePx, c) && TerraWorld.LineClearPx(centrePx, c)) && !(InSight(topPx, c) && TerraWorld.LineClearPx(topPx, c))) continue;
                // Hallownest has no walls: a block within the 90×90 box, or a platform at the spot.
                if (!SolidIn(c, 90f)) continue;
                bool crowded = false;
                foreach (var q in live)
                    if (q.Kind == ProjKind.Spore && !q.Dead && Vector2.Distance(CenterPx(q), c) < 48f)
                    {
                        crowded = true;
                        break;
                    }
                if (crowded) continue;
                var p = Spawn(ProjKind.Spore, TerraParticles.ToWorld(c), Vector2.zero, 70, 1.5f, 0f);
                if (p == null) return;
                p.MinionShot = false;
                p.LocalAi1 = Random.Range(0, 2);
                p.HitSize = p.LocalAi1 > 0f ? new Vector2(16f, 16f) : Vector2.zero;
                p.Frame = live.Count % 6;
                return;
            }
        }

        /// <summary>Whether any block (or platform) is in a box (Terraria px) round a point.</summary>
        static bool SolidIn(Vector2 c, float size)
        {
            var w = TerraParticles.ToWorld(c);
            var half = size * 0.5f * Px;
            int x0 = CellGrid.WorldToCell(new Vector2(w.x - half, w.y)).x, x1 = CellGrid.WorldToCell(new Vector2(w.x + half, w.y)).x;
            int y0 = CellGrid.WorldToCell(new Vector2(w.x, w.y - half)).y, y1 = CellGrid.WorldToCell(new Vector2(w.x, w.y + half)).y;
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    if (TerraWorld.Get(x, y) != CellKind.Empty) return true;
            return Physics2D.OverlapBox(w, Vector2.one * size * Px, 0f, 1 << ChunkColliders.TerrainLayer) != null;
        }

        /// <summary>
        /// AI_105 (P:40769-40917): breathes and turns; drifts a little by its index; ages faster the
        /// farther from the player (a hundred times without the sac) and fades out after 5400; homes
        /// gently on the nearest enemy within 340 px (Manhattan).
        /// </summary>
        static void StepSpore(Proj p)
        {
            float o = (1f - p.Alpha) * p.Scale;
            FxBatch.Instance?.Light(CenterPx(p), new Color(0.2f * o, 0.275f * o, 0.075f * o), 2f);
            if (++p.LocalAi0 >= 90f) p.LocalAi0 *= -1f;
            p.Scale += p.LocalAi0 >= 0f ? 0.003f : -0.003f;
            p.TRot += 0.0025f * p.Scale;
            float sx = 1f, sy = 1f;
            switch (p.Frame % 6)
            {
                case 0: sy = -1f; break;
                case 1: sx = -1f; break;
                case 2: sx = sy = -1f; break;
                case 3: sy = 0f; break;
                case 4: sx = 0f; break;
            }
            p.Ai1 = p.Ai1 + 1f > 60f ? -180f : p.Ai1 + 1f;
            var v = TV(p);
            float k = p.Ai1 >= -60f ? 1f : -1f;
            v += new Vector2(0.002f * sy, 0.002f * sx) * k;
            var hero = HeroController.SilentInstance;
            if (p.Ai0 > 5400f)
            {
                p.Damage = 0;
                p.Bounces = 1;
                if (p.Alpha < 1f) p.Alpha = Mathf.Min(1f, p.Alpha + 5f / 255f);
                else
                {
                    p.Dead = true;
                    return;
                }
            }
            else
            {
                float d = hero != null ? Vector2.Distance(CenterPx(p), TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center)) / 100f : 100f;
                float[] steps = { 1.1f, 1.2f, 1.3f, 1.4f, 1.5f, 1.6f, 1.7f };
                for (int i = 0; i < steps.Length; i++) if (d > 4f + i) d *= steps[i];
                if (!Equipment.Current.SporeSac) d += 100f;
                p.Ai0 += d;
                if (p.Alpha > 50f / 255f) p.Alpha = Mathf.Max(50f / 255f, p.Alpha - 10f / 255f);
            }
            float best = 340f;
            Vector2? to = null;
            foreach (var hm in AccessoryEffects.Enemies(p.Pos, 340f, accNear))
            {
                if (!Chaseable(hm)) continue;
                var e = EnemyCenterPx(hm);
                float m = Mathf.Abs(CenterPx(p).x - e.x) + Mathf.Abs(CenterPx(p).y - e.y);
                if (m < best)
                {
                    best = m;
                    to = e;
                }
            }
            if (to.HasValue) v = (v * 40f + (to.Value - CenterPx(p)).normalized * 4f) / 41f;
            else if (v.magnitude > 0.2f) v *= 0.98f;
            p.Vel = Flip(v);
            p.Pos += p.Vel * Px;
        }

        /// <summary>GetAlpha (P:80863): (255-α) grey with that alpha.</summary>
        static void DrawSpore(FxBatch fx, Proj p, Vector2 px)
        {
            float o = 1f - p.Alpha;
            fx.Draw(Sprite(p.LocalAi1 > 0f ? "Projectile_568" : "Projectile_567"), px, new Color(o, o, o, o), p.TRot, p.Scale);
        }

        /// <summary>P:74318-74324: a spore still armed leaves a cloud (569-571) drifting off at 0.3 px a tick, as strong.</summary>
        void KillSpore(Proj p)
        {
            if (p.Bounces != 0) return;
            var v = Random.insideUnitCircle.normalized * 0.3f;
            var c = Spawn(ProjKind.SacCloud, p.Pos, v, p.Damage, 0f, 0f);
            if (c != null) c.LocalAi1 = Random.Range(0, 3);
        }

        /// <summary>AI_106 (P:34137-34167): turns with its drift, slows; fades in to 80 and after 60 ticks away.</summary>
        static void StepSacCloud(Proj p)
        {
            var v = TV(p);
            p.TRot += v.x * 0.02f + (v.x < 0f ? -1f : 1f) * Mathf.Abs(v.y) * 0.02f;
            p.Vel *= 0.98f;
            if (++p.Ai0 >= 60f)
            {
                if (p.Alpha < 1f) p.Alpha = Mathf.Min(1f, p.Alpha + 5f / 255f);
                else p.Dead = true;
            }
            else if (p.Alpha > 80f / 255f) p.Alpha = Mathf.Max(80f / 255f, p.Alpha - 30f / 255f);
        }

        static void DrawSacCloud(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Draw(Sprite("Projectile_" + (569 + (int)p.LocalAi1)), px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 937: the Volatile Gelatin's ball

        /// <summary>
        /// Player.VolatileGelatin (Pl:42817-42850): every 41 ticks, at the nearest enemy in sight
        /// within 640 px, 12 px a tick and 1.3 up; 65 damage, kb 7.
        /// </summary>
        void VolatileGelatinShot(Vector2 centrePx)
        {
            HealthManager best = null;
            float bestD = 640f;
            foreach (var hm in AccessoryEffects.Enemies(TerraParticles.ToWorld(centrePx), 640f, accNear))
            {
                if (!Chaseable(hm)) continue;
                var e = EnemyCenterPx(hm);
                float d = Vector2.Distance(e, centrePx);
                if (d >= bestD || !InSight(centrePx, e) || !TerraWorld.LineClearPx(centrePx, e)) continue;
                bestD = d;
                best = hm;
            }
            if (best == null) return;
            var v = (EnemyCenterPx(best) - centrePx).normalized * 12f;
            v.y -= 1.3f;
            Spawn(ProjKind.GelBall, TerraParticles.ToWorld(centrePx), Flip(v), 65, 7f, 0f);
        }

        /// <summary>AI_001 for 937 (P:56019-56060, 57886-57892, 58565): fades in; Queen Slime motes; falls after 5 updates; spins.</summary>
        static void StepGelBall(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            if (p.Alpha == 0f && Random.Range(0, 3) == 0)
            {
                var v = TV(p);
                int d = BoxDust(CenterPx(p) - new Vector2(5f, 5f) + v, 10, 10, 4, 0f, 0f, 50, 1.2f);
                if (d >= 0)
                {
                    D(d).Color = QueenSlimeColor(150);
                    D(d).Velocity = D(d).Velocity * 0.3f + v * 0.3f;
                    D(d).NoGravity = true;
                }
            }
            if (++p.Ai0 >= 5f)
            {
                p.Ai0 = 5f;
                p.Vel.y -= 0.07f;
            }
            p.Vel.y = Mathf.Max(p.Vel.y, -16f);
            p.TRot += (Mathf.Abs(p.Vel.x) + Mathf.Abs(p.Vel.y)) * 0.05f;
        }

        /// <summary>NPC.AI_121_QueenSlime_GetDustColor: a pink-to-blue gel colour at random.</summary>
        static Color QueenSlimeColor(int alpha)
        {
            var c = Color.Lerp(FxBatch.C(255, 160, 255), FxBatch.C(80, 210, 255), Random.value);
            c.a = alpha / 255f;
            return c;
        }

        /// <summary>P:18603-18613: off a wall at 0.4, off a floor at 0.95 (when going faster than 1), one hit fewer.</summary>
        static void GelBallBounce(TerraProjectiles t, Proj p, bool hitX, bool hitY, Vector2 oldVel)
        {
            if (hitX && Mathf.Abs(oldVel.x) > 1f) p.Vel.x = oldVel.x * -0.4f;
            if (hitY && Mathf.Abs(oldVel.y) > 1f) p.Vel.y = oldVel.y * -0.95f;
            if (--p.Penetrate <= 0) t.Kill(p);
        }

        /// <summary>P:72784-72792: twelve gel splashes in a ring.</summary>
        static void KillGelBall(Proj p)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f + Random.value * Mathf.PI;
                var v = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * (3f + Random.value * 0.5f);
                int d = BoxDust(CenterPx(p) - new Vector2(13f, 13f), 26, 26, 31, v.x, v.y, 40, 1.5f);
                if (d < 0) continue;
                D(d).Color = QueenSlimeColor(255);
                D(d).NoGravity = true;
            }
        }

        static void DrawTurningLit(FxBatch fx, Proj p, Vector2 px, float alphaMul)
        {
            var c = new Color(L, L, L, 1f) * (1f - p.Alpha);
            c.a *= alphaMul;
            fx.Draw(Sprite(p.Spec.Texture), px, c, p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 532: the Bone Glove's bone

        /// <summary>
        /// The Bone Glove (Pl:43959-43965): with any weapon in use, a bone every 60 ticks at the
        /// cursor, aimed over far targets (ApplyRangeCompensation 0.2), 10 px a tick; 25, kb 5.
        /// </summary>
        public void BoneGloveShot(Vector2 centreWorld, Vector2 mouseWorld)
        {
            Vector2 start = TerraParticles.ToPx(centreWorld), target = TerraParticles.ToPx(mouseWorld);
            float d = Vector2.Distance(start, target);
            var n = (target - start).normalized;
            n.y -= 1f;
            float k = d / 700f;
            k = k * k * 700f;
            target += new Vector2(-n.x * k * 0.2f, n.y * k * 0.2f);
            var v = (target - start).normalized * 10f;
            Spawn(ProjKind.BoneGloveBone, centreWorld, Flip(v), 25, 5f, 0f);
        }

        /// <summary>AI_001 for 532: its counter twice an update, falling past 15; spinning with its speed.</summary>
        static void StepBone(Proj p)
        {
            p.Ai0 += 2f;
            if (p.Ai0 >= 15f)
            {
                p.Ai0 = 15f;
                p.Vel.y -= 0.1f;
            }
            p.Vel.y = Mathf.Max(p.Vel.y, -16f);
            p.TRot += (0.2f + Mathf.Abs(p.Vel.x) * 0.1f) * p.Dir;
        }

        /// <summary>P:18260-18283: back off what it meets at three quarters, a hit fewer.</summary>
        static void BoneBounce(TerraProjectiles t, Proj p, bool hitX, bool hitY, Vector2 oldVel)
        {
            if (hitX) p.Vel.x = oldVel.x * -0.75f;
            if (hitY && (Mathf.Abs(oldVel.y) > 2f || p.Vel.y == 0f)) p.Vel.y = oldVel.y * -0.75f;
            if (--p.Penetrate <= 0) t.Kill(p);
        }

        /// <summary>P:77889-77896: the dig sound and ten bone dust.</summary>
        static void KillBone(Proj p)
        {
            Sound("Dig_" + Random.Range(0, 3));
            for (int i = 0; i < 10; i++) BoxDust(CenterPx(p) - new Vector2(8f, 8f), 16, 16, 26, 0f, 0f, 0, 0.8f);
        }

        // ------------------------------------------------------------------ 964: the Bone Helm's shadow hand

        int insanityCooldown;

        /// <summary>
        /// Player.SpawnHallucination (Pl:14148-14176), each tick: every 20-100 ticks a shadow hand
        /// at a random enemy within 500 px in sight, by one of four paths (RandomizeInsanityShadowFor,
        /// friendly); 18 damage.
        /// </summary>
        void BoneHelmTick(Vector2 centrePx)
        {
            insanityCooldown = Mathf.Clamp(insanityCooldown - 1, 0, 100);
            if (insanityCooldown > 0) return;
            insanityCooldown = Random.Range(20, 101);
            var near = new List<HealthManager>();
            foreach (var hm in AccessoryEffects.Enemies(TerraParticles.ToWorld(centrePx), 500f, accNear))
            {
                if (!Chaseable(hm)) continue;
                var e = EnemyCenterPx(hm);
                if (Vector2.Distance(e, centrePx) <= 500f && InSight(centrePx, e) && TerraWorld.LineClearPx(centrePx, e)) near.Add(hm);
            }
            if (near.Count == 0) return;
            var target = near[Random.Range(0, near.Count)];
            var c = EnemyCenterPx(target);
            int side = Random.Range(0, 2) * 2 - 1;
            Vector2 at, v;
            float ai0, ai1 = 0f;
            switch (Random.Range(0, 4))
            {
                default:
                {
                    float a = Random.Range(-1f, 1f) * Mathf.PI * 0.125f;
                    at = c + RotateBy(new Vector2(-side * 100f, 0f), a);
                    v = RotateBy(new Vector2(side * 100f / 20f, 0f), a);
                    ai0 = 0f;
                    break;
                }
                case 1:
                {
                    float a = Random.value * Mathf.PI * 2f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    at = c - dir * 50f;
                    ai0 = 180f;
                    ai1 = a - Mathf.PI / 2f;
                    v = dir * 2f;
                    break;
                }
                case 2:
                {
                    float a = Random.value * Mathf.PI * 2f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    at = c - dir * 100f;
                    ai0 = 300f;
                    ai1 = a;
                    v = dir * 2f;
                    break;
                }
                case 3:
                {
                    float a = Random.value * Mathf.PI * 2f, turn = Mathf.PI / 2f / 30f * Random.Range(-1f, 1f);
                    at = c;
                    var step = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3f;
                    for (int i = 0; i < 30; i++)
                    {
                        at -= step;
                        step = RotateBy(step, -turn);
                    }
                    v = step;
                    ai0 = 390f;
                    ai1 = turn;
                    break;
                }
            }
            var p = Spawn(ProjKind.ShadowHand, TerraParticles.ToWorld(at), Flip(v), 18, 0f, 0f);
            if (p == null) return;
            p.Ai0 = ai0;
            p.Ai1 = ai1;
            // Variation 1 swings round a point 70 px off along its rotation, which starts at 0.
            p.TRot = ai0 == 180f ? 0f : Mathf.Atan2(v.y, v.x);
        }

        /// <summary>AI_187_ShadowHand (P:43274-43420): the ghostly sound once; in to 50, out over its last 15; four paths.</summary>
        static void StepShadowHand(Proj p)
        {
            float counter = p.Ai0, max;
            int variation;
            if (counter < 180f) { variation = 0; max = 180f; }
            else if (counter < 300f) { variation = 1; counter -= 180f; max = 120f; }
            else if (counter < 390f) { variation = 2; counter -= 300f; max = 90f; }
            else { variation = 3; counter -= 390f; max = 90f; }
            float t = counter / max;
            if (counter == 0f) Sound("Custom/dd2_ghastly_glaive_impact_ghost_" + Random.Range(0, 3));
            if (counter > max - 15f) p.Alpha = Mathf.Min(1f, p.Alpha + 25f / 255f);
            else p.Alpha = Mathf.Max(50f / 255f, p.Alpha - 25f / 255f);
            if (counter >= max - 1f)
            {
                p.Dead = true;
                return;
            }
            var v = TV(p);
            switch (variation)
            {
                case 0:
                    v *= 0.98f;
                    p.Dir = v.x > 0f ? 1f : -1f;
                    p.TRot = Mathf.Atan2(v.y, v.x);
                    break;
                case 1:
                {
                    p.Dir = v.x > 0f ? 1f : -1f;
                    if (v.magnitude > 0.1f) v *= 0.95f;
                    float r = 70f * p.Dir;
                    var c = CenterPx(p);
                    var pivot = c - new Vector2(Mathf.Cos(p.TRot), Mathf.Sin(p.TRot)) * r;
                    float a = Remap(t, 0.3f, 0.5f, 0f, 1f) * Remap(t, 0.45f, 0.5f, 1f, 0f), b = Remap(t, 0.5f, 0.55f, 0f, 1f) * Remap(t, 0.5f, 1f, 1f, 0f);
                    float spin = a * Mathf.PI / 60f - b * Mathf.PI * 8f / 60f;
                    p.TRot = Mathf.Repeat(p.TRot + spin * -p.Dir + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
                    p.Pos = TerraParticles.ToWorld(pivot + new Vector2(Mathf.Cos(p.TRot), Mathf.Sin(p.TRot)) * r);
                    break;
                }
                case 2:
                {
                    float a = Remap(t, 0f, 0.4f, 1f, 0f), b = Remap(t, 0.3f, 0.4f, 0f, 1f) * Remap(t, 0.4f, 1f, 1f, 0f);
                    v = new Vector2(Mathf.Cos(p.Ai1), Mathf.Sin(p.Ai1)) * (a * 2f + b * 8f + 0.01f);
                    p.Dir = v.x > 0f ? 1f : -1f;
                    p.TRot = Mathf.Atan2(v.y, v.x);
                    break;
                }
                case 3:
                    v = RotateBy(v, p.Ai1);
                    p.Dir = v.x > 0f ? 1f : -1f;
                    p.TRot = Mathf.Atan2(v.y, v.x);
                    break;
            }
            p.Vel = Flip(v);
            p.Ai0++;
        }

        /// <summary>
        /// M:30719, 31362, 31560: four violet copies turning round it, a black trail of its last
        /// five places, the hand black by its opacity (held 20 px from its fingers' end).
        /// </summary>
        static void DrawShadowHand(FxBatch fx, Proj p, Vector2 px)
        {
            var tex = Sprite(p.Spec.Texture).rect;
            bool right = p.Dir > 0f;
            var frame = FxBatch.Frame(p.Spec.Texture, 0, 0, (int)tex.width, (int)tex.height, right ? tex.width - 20f : 20f, tex.height * 0.5f);
            float rot = p.TRot + (right ? 0f : Mathf.PI);
            float o = 1f - p.Alpha;
            for (int i = 0; i < p.Trail.Count; i++)
            {
                float k = (5f - i) / (5f * 1.5f);
                fx.Draw(frame, TerraParticles.ToPx(p.Trail[i]), new Color(0f, 0f, 0f, o * k), rot, p.Scale, flipX: !right);
            }
            var violet = FxBatch.C(238, 130, 238) * 0.5f * o;
            for (int k = 0; k < 4; k++)
            {
                float a = p.TRot + p.Ai0 / 60f * Mathf.PI * 2f + Mathf.PI / 2f * k;
                fx.Draw(frame, px + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f, violet, rot, p.Scale, flipX: !right);
            }
            fx.Draw(frame, px, new Color(0f, 0f, 0f, o), rot, p.Scale, flipX: !right);
        }

        // ------------------------------------------------------------------ item 4143: the Mana Cloak's star

        /// <summary>
        /// The pickup (Pl:35246-35249, 35357-35374): pulled in from 42 + 300 px around the player
        /// (PullItem_Common), and on touching him 50 mana and Mana Heat (magic damage up to 20%
        /// more, fading over 2 s); the grab sound.
        /// </summary>
        static void StepManaPickup(Proj p)
        {
            var hero = HeroController.SilentInstance;
            var mod = TerraMod.Instance;
            if (hero == null || mod == null) return;
            var body = hero.GetComponent<BoxCollider2D>().bounds;
            var c = p.Pos;
            if (new Bounds(new Vector3(c.x, c.y, body.center.z), new Vector3(12f * Px, 12f * Px, 1f)).Intersects(body))
            {
                int got = mod.Stats.RestoreMana(50);
                UI.CombatText.Show(body.center + Vector3.up * 0.9f, "50", UI.CombatText.Mana);
                Player.PlayerBuffs.Add(Player.PlayerBuffs.ManaHeat, 120);
                mod.PlaySound("Grab", 0.7f);
                DevManaStars++;
                p.Dead = true;
                return;
            }
            float reach = (42f + 300f) * Px;
            var range = new Bounds(body.center, body.size + Vector3.one * reach * 2f);
            if (range.Contains(new Vector3(c.x, c.y, body.center.z)))
            {
                Vector2 heroVel = hero.GetComponent<Rigidbody2D>().linearVelocity / Player.TerraMovement.PxT;
                var v = p.Vel;
                if (body.center.x > c.x)
                {
                    if (v.x < 4f + heroVel.x) v.x += 0.45f;
                    if (v.x < 0f) v.x += 0.3375f;
                }
                else
                {
                    if (v.x > -4f + heroVel.x) v.x -= 0.45f;
                    if (v.x > 0f) v.x -= 0.3375f;
                }
                if (body.center.y < c.y)
                {
                    if (v.y > -4f) v.y -= 0.45f;
                    if (v.y > 0f) v.y -= 0.3375f;
                }
                else
                {
                    if (v.y < 4f) v.y += 0.45f;
                    if (v.y < 0f) v.y += 0.3375f;
                }
                p.Vel = v;
            }
            else p.Vel *= 0.95f;
            p.Pos += p.Vel * Px;
        }

        /// <summary>Dev: Mana Cloak stars picked up so far.</summary>
        public static int DevManaStars;

        /// <summary>M:39419-39437: three dark blue copies circling 8 px out, three 4 px out, breathing over 5 s; the star half added.</summary>
        static void DrawManaPickup(FxBatch fx, Proj p, Vector2 px)
        {
            var tex = Sprite(p.Spec.Texture);
            float spin = p.Ticks / 240f + Time.time * 0.04f, b = Time.time % 5f / 2.5f;
            if (b >= 1f) b = 2f - b;
            b = b * 0.5f + 0.5f;
            for (float k = 0f; k < 1f; k += 0.34f)
            {
                float a = (k + spin) * Mathf.PI * 2f;
                fx.Draw(tex, px + new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * 8f * b, FxBatch.C(30, 30, 155, 60), 0f, 1f);
            }
            for (float k = 0f; k < 1f; k += 0.34f)
            {
                float a = (k + spin) * Mathf.PI * 2f;
                fx.Draw(tex, px + new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * 4f * b, FxBatch.C(60, 60, 127, 57), 0f, 1f);
            }
            fx.Draw(tex, px, FxBatch.C(255, 255, 255, 128), 0f, 1f);
            fx.Light(px, new Color(0.1f, 0.1f, 0.5f), 2f);
        }

        // ------------------------------------------------------------------ the Harpy Charm (Pl:28646-28702, P:58753-58812)

        int harpyCooldown;

        /// <summary>
        /// HarpyCharmEffect: every 90 ticks (72 with the Seraph Necklace) the nearest own arrow
        /// 400-1200 px from the player that sees an enemy within 1600 px turns at it, burning
        /// with the Phoenix Quiver; it then homes until it hits.
        /// </summary>
        void HarpyCharmTick(Vector2 centrePx)
        {
            var b = Equipment.Current;
            if (!b.HarpyCharm) return;
            if (harpyCooldown > 0)
            {
                harpyCooldown--;
                return;
            }
            Proj best = null;
            HealthManager bestTarget = null;
            float bestD = float.MaxValue;
            foreach (var q in live)
            {
                if (q.Dead || !q.Arrow || q.Spec.Ai != Ai.Arrow || (q.Harpy & 1) != 0) continue;
                float d = Vector2.Distance(centrePx, CenterPx(q));
                if (d < 400f || d > 1200f || d >= bestD) continue;
                var t = ArrowSight(q, 1600f);
                if (t == null) continue;
                best = q;
                bestTarget = t;
                bestD = d;
            }
            if (best == null) return;
            byte kind = (byte)(b.PhoenixQuiver ? 4 : b.SeraphNecklace ? 8 : 0);
            best.Harpy = (byte)(1 | kind);
            var v = TV(best);
            best.Vel = Flip((EnemyCenterPx(bestTarget) - CenterPx(best)).normalized * v.magnitude);
            best.TimeLeft = Mathf.Min(best.TimeLeft, 300);
            HarpyParticles(CenterPx(best), TV(best), kind);
            harpyCooldown = b.SeraphNecklace ? (int)(90 * 0.8f) : 90;
            DevHarpy++;
        }

        public static int DevHarpy;

        /// <summary>Projectile.FindTargetWithLineOfSight: the nearest chaseable enemy within range the arrow can see.</summary>
        static HealthManager ArrowSight(Proj q, float range)
        {
            HealthManager best = null;
            float bestD = range;
            var c = CenterPx(q);
            foreach (var hm in AccessoryEffects.Enemies(q.Pos, range, accNear))
            {
                if (!Chaseable(hm) || q.Immune.ContainsKey(hm.GetInstanceID())) continue;
                var e = EnemyCenterPx(hm);
                float d = Vector2.Distance(c, e);
                if (d >= bestD || !InSight(c, e) || !TerraWorld.LineClearPx(c, e)) continue;
                best = hm;
                bestD = d;
            }
            return best;
        }

        /// <summary>AI_001's tail for a charmed arrow (P:58753-58777): re-aimed at what it sees each update, speeding up to 12; lost sight ends it.</summary>
        static void HarpyArrowUpdate(Proj p)
        {
            if ((p.Harpy & 1) == 0 || (p.Harpy & 2) != 0) return;
            var t = ArrowSight(p, 1600f);
            if (t == null)
            {
                p.Harpy |= 2;
                return;
            }
            var v = TV(p);
            v = (EnemyCenterPx(t) - CenterPx(p)).normalized * v.magnitude;
            if (v.magnitude < 1f) v = v.normalized;
            if (v.magnitude < 12f) v *= 1.05f;
            p.Vel = Flip(v);
            if ((p.Harpy & 4) != 0 && Random.Range(0, 3) == 0)
            {
                int d = DustBox(p, 6, Vector2.zero, 0, Random.Range(1.3f, 1.7f), true);
                if (d >= 0) D(d).Velocity = Random.insideUnitCircle * 3f + v * 0.1f;
            }
        }

        static void HarpyParticles(Vector2 at, Vector2 v, byte kind) => FxParticles.HarpyCharm(at, v, kind == 4 ? 1 : kind == 8 ? 2 : 0);

        /// <summary>
        /// M:30580-30595: behind a Phoenix-quivered arrow two flame streaks (Extra_91) a little
        /// ahead of its centre along its flight, a red one and an orange-gold one, breathing.
        /// </summary>
        static void DrawPhoenixFlames(FxBatch fx, Proj p, Vector2 px)
        {
            var v = TV(p);
            if (v.sqrMagnitude < 1e-6f) return;
            var dir = v.normalized;
            float pulse = 0.5f + 0.05f * Mathf.Sin(Time.time * Mathf.PI * 2f);
            var tex = FxBatch.Frame("Extra_91", 0, 0, 34, 90, 17f, 10f);
            float rot = Mathf.Atan2(v.y, v.x) + Mathf.PI / 2f;
            var at = px + dir * 2f;
            var red = FxBatch.C(255, 40, 0, 180) * 0.75f;
            fx.Draw(tex, at, red, rot, new Vector2(0.6f, 0.8f) * 1.5f * (0.8f + 0.1f * pulse));
            var gold = Color.Lerp(FxBatch.C(255, 165, 0), FxBatch.C(255, 231, 69), 0.5f);
            gold.a = 127f / 255f;
            fx.Draw(tex, at, gold, rot, new Vector2(0.6f, 0.8f) * 1.5f * pulse);
        }

        // ------------------------------------------------------------------ once a tick

        int volatileCounter;

        /// <summary>The accessories' projectiles' own tick: the Spore Sac, the Volatile Gelatin, the Bone Helm, the Harpy Charm.</summary>
        void AccTick()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || hero.cState.dead || TerraMod.Instance == null) return;
            var b = Equipment.Current;
            var box = hero.GetComponent<BoxCollider2D>().bounds;
            var centre = TerraParticles.ToPx(box.center);
            if (b.SporeSac) SporeSacTick(centre, TerraParticles.ToPx(new Vector2(box.center.x, box.max.y)));
            if (b.VolatileGelatin && ++volatileCounter > 40)
            {
                volatileCounter = 0;
                VolatileGelatinShot(centre);
            }
            if (b.BoneHelm) BoneHelmTick(centre);
            HarpyCharmTick(centre);
        }

        // ------------------------------------------------------------------ the accessories in a projectile's hit

        /// <summary>
        /// Before a projectile's damage (StatusNPC and the snapping stones, P:11176-11195,
        /// 13439-13450): the barbs' poison on arrows, the Catalyst Band's mark, the Phoenix
        /// Quiver's fire; a charged stone's strike for a ranged (Snapping Stone), melee or ranged
        /// (Pyroclastic Stone) or tagged minion (Armlet of Ruin) hit; the Templar's Sling's crit
        /// for a sentry's shot. Returns damage to add after the hit's variation.
        /// </summary>
        int AccBeforeHit(Proj p, HealthManager hm, ref float critChance, bool tagged)
        {
            var b = Equipment.Current;
            var cls = ClassOf(p);
            bool enchantable = p.Damage > 0;
            if (b.PoisonBarb && p.Arrow) EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, 90);
            if (b.CatalystBand && p.Damage > 0) EnemyDebuffs.Add(hm, EnemyDebuffs.AcceleratePoisons, 300);
            if ((p.Harpy & 4) != 0) EnemyDebuffs.Add(hm, EnemyDebuffs.Hellfire, 300);
            // (The Templar's Sling's crit and the Armlet of Ruin's strike are in SummonTags.Modify, for every minion and sentry hit.)
            if (!enchantable) return 0;
            if (b.Pyroclast && (cls == DamageClass.Melee || cls == DamageClass.Ranged)) return AccessoryEffects.SnapStrike(CenterPx(p), hm, 1);
            if (b.SnappingStone && cls == DamageClass.Ranged) return AccessoryEffects.SnapStrike(CenterPx(p), hm, 0);
            return 0;
        }

        /// <summary>
        /// After a projectile's hit: the charmed arrow is spent (P:13714-13721); the Sweet Barb's
        /// bees off an arrow (P:13944-13967); the Lucky Coin's coin (stack 1, P:13739-13752); the
        /// Mana Cloak's star off a magic hit (P:13892-13915).
        /// </summary>
        void AccAfterHit(Proj p, HealthManager hm, int dealt)
        {
            var b = Equipment.Current;
            if (p.Spec.Ai == Ai.Arrow && (p.Harpy & 1) != 0) p.Harpy |= 2;
            if (p.Arrow && p.Kind != ProjKind.PhantasmArrow && PhantasmTime > 0) PhantasmArrows(p, hm);
            if (b.HoneyedBarb && p.Arrow) HoneyedBarbBees(p);
            if (b.LuckyCoin) AccessoryEffects.LuckyCoin(hm, p.Vel.x >= 0f ? 1 : -1, false);
            if (b.ManaCloak && ClassOf(p) == DamageClass.Magic && AccessoryEffects.ManaCloakCooldown == 0 && dealt > 0 && Chaseable(hm))
            {
                AccessoryEffects.ManaCloakCooldown = 300;
                var c = CenterPx(p);
                var from = c + new Vector2(Random.Range(-400, 400), -Random.Range(500, 800));
                var at = c + new Vector2(Random.Range(-100, 101), 0f);
                var v = (at - from).normalized * 23f;
                ShootCloakStar(ProjKind.ManaCloakStar, TerraParticles.ToWorld(from), Flip(v), 100, p.Pos.y, true);
            }
        }

        /// <summary>The Sweet Barb (P:13944-13967): one chance in 5, less for each bee out; three to six bees at 5% of the arrow and 4.</summary>
        void HoneyedBarbBees(Proj p)
        {
            int bees = 0;
            foreach (var q in live) if (!q.Dead && (q.Kind == ProjKind.Bee || q.Kind == ProjKind.GiantBee)) bees++;
            if (Random.value >= 0.2f - 0.02f * bees) return;
            int n = 3;
            for (int i = 0; i < 6; i++) if (i % 2 == 1 && Random.Range(0, 3) == 0) n++;
            n = Mathf.Min(n, 6);
            bool strong = Equipment.Current.StrongBees;
            for (int i = 0; i < n; i++)
            {
                var kind = strong && Random.Range(0, 2) == 0 ? ProjKind.GiantBee : ProjKind.Bee;
                int dmg = (int)(p.Damage * 0.05f) + 4;
                dmg += strong ? Random.Range(1, 4) : Random.Range(0, 2);
                var bee = Spawn(kind, p.Pos + (Vector2)(Random.insideUnitCircle * 8f * Px), Flip(Random.insideUnitCircle * 8f), dmg, strong ? 0.5f : 0f, 0f);
                if (bee == null) continue;
                bee.Class = DamageClass.Ranged;
                bee.Penetrate = 2;
            }
        }
    }
}
