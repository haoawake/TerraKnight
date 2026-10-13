using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Sweep R4a2's other two (Terraria's space, px y down, per update): Eventide's rainbow arrows
    /// (932, AI_181, five fanned out about the player each use) and the Piranha Gun's piranhas
    /// (190, AI_039: three out at a time, biting on to what they hit and following it).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        public static int DevEventideRainbows;

        static void RegisterGuns2()
        {
            // P:8844, 15711: the rainbow arrow, through five (each once a second), 2 s, its blade 40 px either side of it.
            specs[ProjKind.EventideArrow] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_932", W = 10, H = 10, Time = 120, Penetrate = 5, LocalCooldown = 60, AlphaStart = 1f,
                Beam = p =>
                {
                    var along = new Vector2(Mathf.Cos(p.TRot), Mathf.Sin(p.TRot)) * 40f;
                    return (CenterPx(p) - along, CenterPx(p) + along, 8f);
                },
                Update = (t, p) => StepEventideArrow(p), Draw = (t, fx, p, px) => DrawEventideArrow(fx, p, px), OnKill = (t, p) => KillEventideArrow(p),
            };
            // P:2781: the piranha, AI_039, through everything, each enemy every 14 ticks, four frames.
            specs[ProjKind.PiranhaProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_190", W = 22, H = 22, Time = 3600, Penetrate = -1, LocalCooldown = 14, AlphaStart = 1f, Frames = 4,
                Update = (t, p) => t.StepPiranha(p), Draw = (t, fx, p, px) => DrawPiranha(fx, p, px), OnHit = (t, p, hm) => PiranhaBite(p, hm),
                TileHit = (t, p, hx, hy, before) =>
                {
                    HitTileDust(p);
                    p.Ai0 = 1f;
                },
                OnKill = (t, p) => { },
            };
        }

        /// <summary>Projectile.GetFairyQueenWeaponsColorFull: hsl((hue + 0.5) % 1, 1, 0.5), toward white, its alpha scaled.</summary>
        static Color RainbowOf(float hue, float alphaMult, float toWhite = 0f)
        {
            var c = Color.Lerp(FxParticles.HslToRgb((hue + 0.5f) % 1f, 1f, 0.5f), Color.white, toWhite);
            c.a = alphaMult;
            return c;
        }

        // ------------------------------------------------------------------ 932: Eventide's arrow

        /// <summary>
        /// Eventide (Pl:48352-48401): one of the use's five shots, the k-th: from 40 px out round the
        /// player, a tenth of a half turn apart across the aim (in toward the facing side first), or
        /// as far behind when that's in a wall; aimed at the cursor (straight on when it's within
        /// 40-100 px); the middle one a rainbow arrow at twice the damage, rainbow ones twice as fast.
        /// </summary>
        public void ShootEventide(ProjKind kind, Vector2 fromWorld, Vector2 aimWorld, Vector2 mouseWorld, float speed, int damage, float knockback, float crit, int k)
        {
            var centre = TerraParticles.ToPx(fromWorld);
            var straight = aimWorld.sqrMagnitude > 1e-6f ? Flip(aimWorld).normalized : new Vector2(0f, -1f);
            var arm = straight * 40f;
            bool open = !Physics2D.Linecast(fromWorld, TerraParticles.ToWorld(centre + arm), 1 << World.ChunkColliders.TerrainLayer);
            float facing = mouseWorld.x < fromWorld.x ? -1f : 1f;
            int slot = facing > 0f ? 4 - k : k;
            var off = RotateBy(arm, Mathf.PI / 10f * (slot - 2f));
            if (!open) off -= arm;
            var at = centre + off;
            var vel = straight * speed;
            if (Physics2D.Linecast(fromWorld, TerraParticles.ToWorld(at - vel), 1 << World.ChunkColliders.TerrainLayer))
                at -= (at - centre).sqrMagnitude > 1e-6f ? (at - centre).normalized * 15f : Vector2.zero;
            var mouse = TerraParticles.ToPx(mouseWorld);
            var dir = (mouse - at).sqrMagnitude > 1e-6f ? (mouse - at).normalized : new Vector2(0f, -1f);
            float near = Mathf.Clamp01(Mathf.InverseLerp(100f, 40f, Vector2.Distance(mouse, centre)));
            if (near > 0f)
            {
                var blend = Vector2.Lerp(dir, straight, near);
                dir = blend.sqrMagnitude > 1e-6f ? blend.normalized : straight;
            }
            if (kind == ProjKind.WoodenArrow) kind = ProjKind.EventideArrow;
            if (k == 2)
            {
                kind = ProjKind.EventideArrow;
                damage *= 2;
            }
            // The bowstring with every shot (UseSound each useTime); the first is the use's own.
            if (k > 0) Sound("Item_5");
            var keep = SpawnClass;
            SpawnClass = DamageClass.Ranged;
            if (kind == ProjKind.EventideArrow)
            {
                var p = Spawn(kind, TerraParticles.ToWorld(at), Flip(dir * speed * 2f), damage, knockback, crit);
                if (p != null)
                {
                    p.Ai1 = Time.time * 2.4f % 1f;
                    DevEventideRainbows++;
                }
            }
            else Spawn(kind, TerraParticles.ToWorld(at), Flip(dir * speed), damage, knockback, crit);
            SpawnClass = keep;
        }

        /// <summary>AI_181 (P:43980-43994): showing over 10 updates, turned along its flight, now and then a rainbow mote (267).</summary>
        static void StepEventideArrow(Proj p)
        {
            p.Ai0++;
            p.Alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(p.Ai0 / 10f));
            var v = TV(p);
            p.TRot = ToRot(v);
            if (Random.Range(0, 6) != 0) return;
            int i = PointDust(CenterPx(p), 267, Vector2.zero, 100);
            if (i < 0) return;
            D(i).FadeIn = 1f;
            D(i).NoGravity = true;
            D(i).Color = RainbowOf(p.Ai1, 1f, Random.value * 0.4f);
            D(i).Scale *= 1.5f;
        }

        /// <summary>
        /// DrawProj_FairyQueenRangedItemShot (M:36576-36600): six added rainbow copies trailing up to
        /// 120 px behind as it goes, a half-added halo of four 4 px round it, then itself in white
        /// (half added), growing from 0.7 over 5 updates.
        /// </summary>
        static void DrawEventideArrow(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Whole(p.Spec.Texture);
            if (spr == null) return;
            float o = 1f - p.Alpha, size = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(p.Ai0 / 5f));
            var along = new Vector2(Mathf.Cos(p.TRot), Mathf.Sin(p.TRot));
            if (o > 0f)
            {
                float speed = Mathf.Clamp01(TV(p).magnitude);
                var full = RainbowOf(p.Ai1, 0f);
                var half = RainbowOf(p.Ai1, 0.5f);
                for (float k = 0f; k < 1f; k += 1f / 6f) fx.Draw(spr, px - along * 120f * k * speed, full * o * (1f - k), p.TRot, size);
                for (float k = 0f; k < 1f; k += 0.25f) fx.Draw(spr, px + RotateBy(along, k * Mathf.PI * 2f) * 4f * size, half * o, p.TRot, size);
            }
            fx.Draw(spr, px, new Color(o, o, o, o * 0.5f), p.TRot, size);
        }

        /// <summary>P:72264-72300: a chime (Item10) and rainbow motes (267) strewn back along its path, fewer and smaller further back.</summary>
        static void KillEventideArrow(Proj p)
        {
            Sound("Item_10");
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(5f, 5f);
            var c = RainbowOf(p.Ai1, 1f);
            for (int k = 0; k < 10; k++)
            {
                var at = tl - v * k;
                int n = Random.Range(1, 3);
                float fade = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(10f, 0f, k));
                if (k >= 3) n--;
                if (k >= 7) n -= 2;
                for (int j = 0; j < n; j++)
                {
                    int i = BoxDust(at, 10, 10, 267);
                    if (i < 0) continue;
                    D(i).Color = c;
                    D(i).Velocity *= Random.value * 0.8f;
                    D(i).NoGravity = true;
                    D(i).Scale = (0.9f + Random.value * 1.2f) * fade;
                    D(i).FadeIn = Random.value * 1.2f * fade;
                }
            }
        }

        // ------------------------------------------------------------------ 190: the Piranha Gun's piranha

        /// <summary>Pl:48845-48862: piranhas to make up three, each up to 2 px off the aim.</summary>
        public void ShootPiranhas(Vector2 from, Vector2 velocity, int damage, float knockback, float crit)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.Ranged;
            for (int k = DevCount(ProjKind.PiranhaProj); k < 3; k++)
                Spawn(ProjKind.PiranhaProj, from, velocity + new Vector2(Random.Range(-40, 41) * 0.05f, Random.Range(-40, 41) * 0.05f), damage, knockback, crit);
            SpawnClass = keep;
        }

        /// <summary>P:13270-13283: a piranha that bites takes that one as its catch.</summary>
        static void PiranhaBite(Proj p, HealthManager hm)
        {
            if (p.Ai1 != 0f) return;
            p.Ai1 = 1f;
            p.Target = hm;
        }

        /// <summary>
        /// AI_039 for 190 (P:27908-28174): out at its speed until 700 px off; on its catch it follows
        /// it (on within 16 px, else 16 px an update to it), moving with it and kept 8 px from the
        /// other piranhas; with its catch gone it goes for the nearest enemy it can see; let go of,
        /// or 1500 px off a catch, it comes back at 20 px an update with the player's run, gone
        /// within 70. The gun stays pointed at it, and while it's still held tops up to three.
        /// </summary>
        void StepPiranha(Proj p)
        {
            var mod = TerraMod.Instance;
            p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            if (mod == null || !Holder(out var centre) || Vector2.Distance(centre, CenterPx(p)) > 2000f)
            {
                Kill(p);
                return;
            }
            var def = ItemDb.Get(ItemType.PiranhaGun);
            bool held = Channel(ItemType.PiranhaGun);
            var c = CenterPx(p);
            var toPlayer = centre - c;
            float dist = Mathf.Max(toPlayer.magnitude, 0.001f);
            var v = TV(p);
            if (!held && p.Alpha <= 0f)
            {
                p.Ai0 = 1f;
                p.Ai1 = -1f;
            }
            if (p.Ai1 > 0f && dist > 1500f)
            {
                p.Ai1 = 0f;
                p.Ai0 = 1f;
            }
            var hero = HeroController.SilentInstance;
            var playerRun = hero != null ? Flip(hero.GetComponent<Rigidbody2D>().linearVelocity / Px / 60f) : Vector2.zero;
            if (p.Ai1 > 0f)
            {
                p.Collide = false;
                var t = p.Target;
                if (t != null && !t.isDead && t.gameObject.activeInHierarchy)
                {
                    var to = EnemyCenterPx(t) - c;
                    float d = to.magnitude;
                    if (d < 16f)
                    {
                        v = to;
                        if (d > 16f / 3f)
                        {
                            FacePiranha(p, v);
                            v = Vector2.zero;
                        }
                    }
                    else
                    {
                        v = to / d * 16f;
                        FacePiranha(p, v);
                    }
                    var trb = t.GetComponent<Rigidbody2D>();
                    if (trb != null) p.Pos += trb.linearVelocity / 60f;
                    foreach (var q in live)
                    {
                        if (q == p || q.Dead || q.Kind != ProjKind.PiranhaProj || Vector2.Distance(c, CenterPx(q)) >= 8f) continue;
                        var qc = CenterPx(q);
                        v.x += c.x < qc.x ? -4f : 4f;
                        v.y += c.y < qc.y ? -4f : 4f;
                    }
                    p.Ai0 = 1f;
                }
                else
                {
                    p.Ai1 = 0f;
                    p.Target = null;
                    float best = 3000f;
                    HealthManager pick = null;
                    foreach (var hm in EnemiesNear(c, 3000f))
                    {
                        if (!Chaseable(hm)) continue;
                        var e = EnemyCenterPx(hm);
                        float d = Mathf.Abs(c.x - e.x) + Mathf.Abs(c.y - e.y);
                        if (d < best && InSight(c, e))
                        {
                            best = d;
                            pick = hm;
                        }
                    }
                    if (pick != null)
                    {
                        var to = EnemyCenterPx(pick) - c;
                        v = (to.sqrMagnitude > 1e-6f ? to.normalized : Vector2.right) * 16f;
                        p.Ai0 = 0f;
                        p.Ai1 = 1f;
                        p.Target = pick;
                    }
                }
            }
            else if (p.Ai0 == 0f)
            {
                if (dist > 700f) p.Ai0 = 1f;
                FacePiranha(p, v);
            }
            else
            {
                p.Collide = false;
                FacePiranha(p, v);
                if (dist < 70f)
                {
                    Kill(p);
                    return;
                }
                v = toPlayer / dist * 20f;
                p.Pos += Flip(playerRun) * Px;
            }
            if (++p.FrameCounter >= 4)
            {
                p.FrameCounter = 0;
                p.Frame++;
            }
            if (p.Frame >= 4) p.Frame = 0;
            p.Vel = Flip(v);
            // SetDummyItemTime(5) and the gun turned to it; the first of them tops the three up while the button is held.
            mod.KeepUsing(def, Flip(c - centre));
            if (!held || p.Ai1 == -1f) return;
            foreach (var q in live)
            {
                if (q.Dead || q.Kind != ProjKind.PiranhaProj) continue;
                if (q == p && DevCount(ProjKind.PiranhaProj) < 3)
                {
                    var aim = AimPx() - centre;
                    ShootPiranhas(TerraParticles.ToWorld(centre), Flip((aim.sqrMagnitude > 1e-6f ? aim.normalized : Vector2.right) * def.ShootSpeed), p.Damage, p.Knockback, p.Crit);
                }
                break;
            }
        }

        /// <summary>Turned along its way, mirrored going left.</summary>
        static void FacePiranha(Proj p, Vector2 v)
        {
            if (v.x < 0f)
            {
                p.Dir = -1f;
                p.TRot = Mathf.Atan2(-v.y, -v.x);
            }
            else
            {
                p.Dir = 1f;
                p.TRot = Mathf.Atan2(v.y, v.x);
            }
        }

        /// <summary>Its frame about its middle, lit, mirrored going left.</summary>
        static void DrawPiranha(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            int tw = (int)whole.rect.width, fh = (int)whole.rect.height / 4;
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, fh * p.Frame, tw, fh, tw / 2f, fh / 2f), px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale, flipX: p.Dir < 0f);
        }
    }
}
