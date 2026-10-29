using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// More hardmode spells (Terraria's space, px y down, per update): the Shadowbeam Staff's beam
    /// (294) and the Heat Ray (260) — unseen bolts making 101 updates a tick whose path shows as a
    /// line of dust when they turn or end (AI_048, ParticleOrchestrator.Spawn_Shadowbeam/HeatRay);
    /// the Bubble Gun's bubbles (410, AI_072), the Razorblade Typhoon's homing blades (409, AI_071),
    /// the Blizzard Staff's icicles (337, from the sky), the Sky Fracture's swords (660) and the Leaf
    /// Blower's leaves (206, AI_040: curling as they fly).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterMagic2()
        {
            specs[ProjKind.Shadowbeam] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 300, Extra = 100, Penetrate = -1, Look = Look.Invisible,
                Update = (t, p) => BeamStart(p, 9), TileHit = (t, p, hx, hy, ov) => BounceShadowbeam(p, hx, hy, ov), OnKill = (t, p) => BeamLine(p, 9),
            };
            specs[ProjKind.HeatRay] = new Spec
            {
                Ai = Ai.Custom, W = 8, H = 8, Time = 200, Extra = 100, Penetrate = 1, Look = Look.Invisible,
                Update = (t, p) => BeamStart(p, 3), OnKill = (t, p) => BeamLine(p, 3),
            };
            specs[ProjKind.Bubble] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_410", W = 14, H = 14, Time = 100, Penetrate = 1, AlphaStart = 1f,
                Update = (t, p) => StepBubble(p), Draw = (t, fx, p, px) => DrawBubble(fx, p, px), OnKill = (t, p) => KillBubble(p),
            };
            specs[ProjKind.RazorbladeTyphoon] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_409", W = 30, H = 30, Time = 300, Extra = 2, Penetrate = -1, AlphaStart = 1f, OwnerImmune = 6,
                Update = (t, p) => t.StepTyphoon(p), Draw = (t, fx, p, px) => DrawTyphoon(fx, p, px), TileHit = (t, p, hx, hy, ov) => Reflect(p, hx, hy, ov),
                OnKill = (t, p) => { },
            };
            specs[ProjKind.Icicle] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_337", W = 10, H = 10, Time = 3600, Penetrate = 1, Extra = 1, TileCollide = false,
                Update = (t, p) => StepIcicle(p), Draw = (t, fx, p, px) => DrawIcicle(fx, p, px), OnKill = (t, p) => KillIcicle(p),
            };
            specs[ProjKind.SkyFractureSword] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_660", W = 10, H = 10, Time = 600, Penetrate = 1, Extra = 1, AlphaStart = 1f, TrailLength = 3,
                Update = (t, p) => StepSkyFracture(p), Draw = (t, fx, p, px) => DrawSkyFracture(fx, p, px), OnKill = (t, p) => KillSkyFracture(p),
            };
            specs[ProjKind.Leaf] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_206", W = 14, H = 14, Time = 360, Penetrate = 1, AlphaStart = 1f,
                Update = (t, p) => t.StepLeaf(p), Draw = (t, fx, p, px) => DrawLeaf(fx, p, px), OnKill = (t, p) => KillLeaf(p),
            };
        }

        /// <summary>A Blizzard Staff icicle (world position, px per update y up), one of five shapes.</summary>
        public void ShootIcicle(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, int shape)
        {
            var p = Spawn(ProjKind.Icicle, from, velocity, damage, knockback, crit);
            if (p == null) return;
            p.Ai1 = shape;
            p.Frame = shape;
        }

        // ------------------------------------------------------------------ 294/260: the beams (AI_048)

        /// <summary>Where the line starts: the beam's place after a few updates (AI_048_GetStartPositionSettingDelay).</summary>
        static void BeamStart(Proj p, int delay)
        {
            if (DevBeamPath != null && p.Kind == ProjKind.Shadowbeam && DevBeamPath.Count < 4000) DevBeamPath.Add((p.Pos, p.Vel));
            if (p.LocalAi0 == delay)
            {
                p.StartPos = p.Pos;
                p.LocalAi1 = 1f;
            }
            p.LocalAi0++;
        }

        /// <summary>
        /// SpawnHelper_SpawnInLine: from where the line started to here, a step of the beam's speed
        /// (at least 4 px) at a time, the Shadowbeam's purple dust three to a step, the Heat Ray's
        /// orange four, each 0.9 to 1.4 big and nearly still.
        /// </summary>
        static void BeamLine(Proj p, int delay)
        {
            if (p.LocalAi1 == 0f) return;
            bool shadow = p.Kind == ProjKind.Shadowbeam;
            var from = TerraParticles.ToPx(p.StartPos);
            var to = CenterPx(p);
            float len = Vector2.Distance(from, to), speed = TV(p).magnitude;
            var step = (to - from).sqrMagnitude > 1e-6f ? (to - from).normalized * speed : Vector2.zero;
            float stride = Mathf.Max(4f, speed);
            // The dust pool holds 400 and a full pool overwrites its oldest: a beam's whole flight in open air (some 900 dust)
            // overwrote the part by the player with the part off screen, and nothing of it was seen (the user's 10-28 "暗影束
            // 法杖的弹幕依旧没修好"). Only the stretch within reach of the player is drawn, at most 80 steps of it.
            float d0 = 0f, d1 = len;
            var hero = HeroController.SilentInstance;
            if (hero != null && len > 1f)
            {
                var c = TerraParticles.ToPx(hero.transform.position);
                float reach = 18f / Px;
                var dir = (to - from) / len;
                float along = Vector2.Dot(c - from, dir);
                float across2 = (c - from).sqrMagnitude - along * along;
                if (across2 >= reach * reach) return;
                float half = Mathf.Sqrt(reach * reach - across2);
                d0 = Mathf.Max(0f, along - half);
                d1 = Mathf.Min(len, along + half);
                if (d1 <= d0) return;
            }
            stride = Mathf.Max(stride, (d1 - d0) / 80f);
            // Each step's dust spread over the step (a stretched step left gaps between its little clumps).
            int per = shadow ? 3 : 4;
            var unit = len > 0f ? (to - from) / len : Vector2.zero;
            for (float d = d0; d < d1; d += stride)
            {
                var at = Vector2.Lerp(from, to, d / len);
                for (int k = 0; k < per; k++) LineDust(shadow ? 173 : 162, at - unit * (stride * k / per) - step * 0.05f * k);
            }
        }

        static void LineDust(int type, Vector2 at)
        {
            int i = PointDust(at, type, Vector2.zero);
            if (i < 0) return;
            D(i).Scale = Random.Range(70, 110) * 0.013f;
            // The line stays where the beam went and fades there (it sank to the floor: "甚至还受到重力").
            D(i).NoGravity = true;
        }

        /// <summary>
        /// P:18976: off a wall the Shadowbeam shows its path so far and starts a new line there. Terraria moves it on by what
        /// TileCollision left of its speed (up to the wall); Move has already stopped it short of the wall, so it only turns:
        /// a whole step on put it inside the wall, where the game's hollow colliders let it wander.
        /// </summary>
        static void BounceShadowbeam(Proj p, bool hitX, bool hitY, Vector2 oldVel)
        {
            if (hitX) p.Vel.x = -oldVel.x;
            if (hitY) p.Vel.y = -oldVel.y;
            BeamLine(p, 9);
            if (p.LocalAi0 >= 9f)
            {
                p.StartPos = p.Pos;
                p.LocalAi1 = 1f;
            }
            DevBeamBounces?.Add((p.Pos, Blocked(p.Pos, TileBox(p) * 0.5f * Px, false, p)));
            p.Ranged?.OnBounce?.Invoke(p.Pos, p.Vel);
        }

        /// <summary>Dev: each Shadowbeam bounce, where it left the beam and whether that is inside a wall (null: not kept).</summary>
        public static System.Collections.Generic.List<(Vector2 at, bool inWall)> DevBeamBounces;

        /// <summary>Dev: a Shadowbeam's place and velocity at each of its updates (null: not kept).</summary>
        public static System.Collections.Generic.List<(Vector2 at, Vector2 vel)> DevBeamPath;

        /// <summary>Dev: the live projectiles of a kind now: where, their velocity (world, per update), their age in ticks.</summary>
        public System.Collections.Generic.List<(Vector2 pos, Vector2 vel, int ticks)> DevLive(ProjKind kind)
        {
            var list = new System.Collections.Generic.List<(Vector2, Vector2, int)>();
            foreach (var p in live) if (p != null && !p.Dead && p.Kind == kind) list.Add((p.Pos, p.Vel, p.Ticks));
            return list;
        }

        // ------------------------------------------------------------------ 410: Bubble Gun (AI_072)

        /// <summary>It fades in after 3 updates (to 50), slows 4% an update; its size (0.6-1.2) is chosen once.</summary>
        static void StepBubble(Proj p)
        {
            p.LocalAi0++;
            if (p.LocalAi0 > 3f) p.Alpha = Mathf.Max(50f / 255f, p.Alpha - 25f / 255f);
            p.Vel *= 0.96f;
            if (p.Ai1 == 0f) p.Ai1 = Random.Range(60, 121) / 100f;
            p.Scale = p.Ai1;
            p.HitSize = Vector2.one * (int)(14f * p.Ai1);
        }

        static void DrawBubble(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Draw(FxBatch.Frame("Projectile_410", 0, 0, 20, 20, 10f, 10f), px, new Color(L, L, L, 1f) * (1f - p.Alpha), 0f, p.Scale);
        }

        /// <summary>P:74257: a pop (Item54) and a ring of pale green-blue spray its own size.</summary>
        static void KillBubble(Proj p)
        {
            Sound("Item_54");
            var c = CenterPx(p);
            float r = 10f * p.Ai1;
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(c - Vector2.one * r, (int)(r * 2), (int)(r * 2), 212);
                if (i < 0) continue;
                var dir = (D(i).Position - c).sqrMagnitude > 1e-6f ? (D(i).Position - c).normalized : Vector2.up;
                D(i).Position = c + dir * r * p.Scale;
                D(i).Velocity = dir * D(i).Velocity.magnitude;
                D(i).Color = Color.Lerp(Color.HSVToRGB(0.4f + Random.value * 0.2f, 0.9f, 1f), Color.white, 0.3f);
                D(i).NoGravity = true;
                D(i).Scale = 0.7f;
            }
        }

        // ------------------------------------------------------------------ 409: Razorblade Typhoon (AI_071)

        // The knight's centre is lower than a Terraria player's firing point. A 30px disc
        // can start intersecting the floor and bounce in place forever. Move only the initial
        // spawn to nearby open air, with an unobstructed path; subsequent terrain bounces stay intact.
        static void ClearTyphoonLaunch(Proj p)
        {
            Vector2 half=TileBox(p)*Px*.5f;
            if(!Blocked(p.Pos,half,false,p)) return;
            Vector2 origin=p.Pos;
            var directions=new[] {p.Vel.sqrMagnitude>0 ? p.Vel.normalized : Vector2.right,
                Vector2.up,Vector2.right,new Vector2(1,1).normalized,
                Vector2.left,new Vector2(-1,1).normalized,Vector2.down};
            for(int step=1;step<=8;step++)
            foreach(var direction in directions)
            {
                Vector2 at=origin+direction*(step*4*Px);
                // Unity reports fraction zero when the firing point already overlaps the
                // floor. Permit leaving that overlap, but never cross another solid surface.
                bool crossed=false;
                foreach(var hit in Physics2D.LinecastAll(origin,at,1<<ChunkColliders.TerrainLayer))
                    if(hit.fraction>0 && !hit.collider.isTrigger && !hit.collider.usedByEffector) crossed=true;
                if(crossed) continue;
                if(Blocked(at,half,false,p)) continue;
                p.Pos=p.StartPos=at;
                return;
            }
        }

        /// <summary>
        /// AI_071: after 10 updates, one time in three, a fan of six sea-blue motes off its front,
        /// fading in (to 50), spinning, a new frame every 3 updates, a cold light. It locks on to the
        /// nearest enemy in sight within 500 px (looking again every 30 updates when there's none)
        /// and turns a tenth of the way towards it each update while it's within 1000 px (Manhattan)
        /// and not immune; it speeds up a little all the time.
        /// </summary>
        void StepTyphoon(Proj p)
        {
            p.LocalAi1++;
            var c = CenterPx(p);
            var v = TV(p);
            if (p.LocalAi1 > 10f && Random.Range(0, 3) == 0)
            {
                var front = (v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.right) * 15f;
                for (int k = 0; k < 6; k++)
                {
                    var at = RotateBy(front, (k - 2) * Mathf.PI / 6f) + c;
                    var out_ = new Vector2(Mathf.Cos(Random.value * Mathf.PI - Mathf.PI / 2f), Mathf.Sin(Random.value * Mathf.PI - Mathf.PI / 2f)) * Random.Range(3, 8);
                    int i = PointDust(at + out_, 217, out_ * 2f, 100, 1.4f);
                    if (i < 0) continue;
                    D(i).NoGravity = true;
                    D(i).Velocity = D(i).Velocity / 4f - v;
                }
                p.Alpha = Mathf.Max(50f / 255f, p.Alpha - 5f / 255f);
                p.TRot += v.x * 0.1f;
                p.Frame = (int)(p.LocalAi1 / 3f) % 3;
            }
            if (p.Ai0 > 0f) p.Ai0--;
            if (p.Target == null && p.Ai0 == 0f)
            {
                float best = 500f;
                foreach (var hm in EnemiesNear(c, 500f))
                {
                    var e = EnemyCenterPx(hm);
                    float d = Vector2.Distance(c, e);
                    if (d < best && InSight(c, e))
                    {
                        best = d;
                        p.Target = hm;
                    }
                }
                if (p.Target == null) p.Ai0 = 30f;
            }
            if (p.Target != null)
            {
                var hm = p.Target;
                if (Chaseable(hm) && !npcImmune.ContainsKey(hm.GetInstanceID()))
                {
                    var e = EnemyCenterPx(hm);
                    if (Mathf.Abs(c.x - e.x) + Mathf.Abs(c.y - e.y) < 1000f)
                    {
                        float a = Mathf.DeltaAngle(Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg, Mathf.Atan2(e.y - c.y, e.x - c.x) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                        v = RotateBy(v, a * 0.1f);
                    }
                    else p.Target = null;
                }
                else p.Target = null;
            }
            float speed = v.magnitude;
            if (speed > 1e-6f) v = v / speed * (speed + 0.0025f);
            p.Vel = Flip(v);
        }

        /// <summary>GetAlpha for 409: near white, mostly solid; a third of a turn's frames, spinning.</summary>
        static void DrawTyphoon(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Light(px, new Color(0.1f, 0.4f, 0.6f), 2.5f);
            fx.Draw(FxBatch.Frame("Projectile_409", 0, p.Frame * 68, 68, 68, 34f, 34f), px, FxBatch.C(250, 250, 250, 200), p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 337: Blizzard Staff

        /// <summary>P:57920: through blocks until within 300 px of the player's height; one of five shapes; frost motes.</summary>
        static void StepIcicle(Proj p)
        {
            var hero = HeroController.SilentInstance;
            if (hero != null && p.Pos.y - 5f * Px < hero.GetComponent<BoxCollider2D>().bounds.max.y + 300f * Px) p.Collide = true;
            p.Frame = (int)p.Ai1;
            var v = TV(p);
            p.TRot = Mathf.Atan2(v.y, v.x) + 1.57f;
            if (Random.Range(0, 2) == 0)
            {
                int i = BoxDust(CenterPx(p) - new Vector2(5f, 5f), 10, 10, 197);
                if (i >= 0)
                {
                    D(i).Velocity *= 0.5f;
                    D(i).NoGravity = true;
                }
            }
        }

        static void DrawIcicle(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Draw(FxBatch.Frame("Projectile_337", 0, p.Frame * 36, 14, 36, 7f, 5f), px + new Vector2(2f, 0f), FxBatch.C(250, 250, 250, 150), p.TRot, p.Scale);
        }

        static void KillIcicle(Proj p)
        {
            Sound("Item_27");
            var tl = CenterPx(p) - new Vector2(5f, 5f);
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(tl, 10, 10, 197);
                if (i >= 0) D(i).NoGravity = true;
            }
        }

        // ------------------------------------------------------------------ 660: Sky Fracture

        /// <summary>P:56639: as it appears, a chime (Item9) and an upright ring of cyan sparks round its flight; it points along it (pi/4 for the diagonal art).</summary>
        static void StepSkyFracture(Proj p)
        {
            var v = TV(p);
            if (p.Alpha > 0f)
            {
                Sound("Item_9");
                p.Alpha = 0f;
                p.Scale = 1.1f;
                p.Frame = Random.Range(0, 14);
                var c = CenterPx(p);
                float a = Mathf.Atan2(v.y, v.x);
                for (int k = 0; k < 16; k++)
                {
                    var s = RotateBy(new Vector2(0f, -1f), k * Mathf.PI * 2f / 16f);
                    s = RotateBy(new Vector2(s.x, s.y * 4f), a);
                    int i = PointDust(c + s, 180, s.sqrMagnitude > 1e-6f ? s.normalized : Vector2.up);
                    if (i < 0) continue;
                    D(i).Scale = 1.5f;
                    D(i).NoGravity = true;
                }
            }
            p.TRot = Mathf.Atan2(v.y, v.x) + Mathf.PI / 4f;
        }

        /// <summary>One of its 14 swords in pale cyan, added, with three fading copies behind (Main.DrawProj's trail list).</summary>
        static void DrawSkyFracture(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Light(px, new Color(0.12f, 0.2f, 0.2f), 2.5f);
            var spr = FxBatch.Frame("Projectile_660", 38 * p.Frame, 0, 38, 38, 19f, 19f);
            for (int k = Mathf.Min(2, p.Trail.Count - 1); k >= 1; k--)
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[k]), FxBatch.C(150, 255, 255, 0) * ((3f - k) / 15f), p.TRot, p.Scale);
            fx.Draw(spr, px, FxBatch.C(150, 255, 255, 0), p.TRot, p.Scale);
        }

        static void KillSkyFracture(Proj p)
        {
            Sound("Item_10");
            var v = TV(p);
            var c = CenterPx(p);
            int n = Random.Range(4, 10);
            for (int k = 0; k < n; k++)
            {
                int i = PointDust(c, 180, Vector2.zero, 100);
                if (i < 0) continue;
                D(i).Velocity = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * 1.6f + new Vector2(0f, -1f) - v * (Random.value * 2f - 1f) * 0.5f;
                D(i).Scale = 2f;
                D(i).FadeIn = 0.5f;
                D(i).NoGravity = true;
            }
        }

        // ------------------------------------------------------------------ 206: Leaf Blower (AI_040)

        /// <summary>
        /// AI_040_LeafBlower: fades in after 3 updates and out after 50; five frames; a slight random
        /// drift (ai[0], ai[1]) chosen on its first update; after 10 updates it curls away from the
        /// side it was thrown to, ever tighter, and its speed is kept between 12 and 16 (Manhattan).
        /// </summary>
        void StepLeaf(Proj p)
        {
            p.LocalAi0++;
            if (p.LocalAi0 > 50f)
            {
                p.Alpha += 2f / 255f;
                if (p.Alpha >= 1f)
                {
                    p.Alpha = 1f;
                    Kill(p);
                    return;
                }
            }
            else if (p.LocalAi0 > 3f) p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            if (++p.FrameCounter >= 3)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 5) p.Frame = 0;
            }
            var v = TV(p);
            v += new Vector2(p.Ai0, p.Ai1);
            if (p.LocalAi1 == 0f) p.Dir = v.x > 0f ? -1f : 1f;
            p.LocalAi1++;
            var c = CenterPx(p);
            if (p.LocalAi1 == 1f)
            {
                p.Ai0 = Random.Range(-100, 101) * 6E-05f;
                p.Ai1 = Random.Range(-100, 101) * 6E-05f;
                if (Random.Range(0, 3) != 0)
                {
                    int i = PointDust(c + v * 3f, 31, RotateBy(v * 0.35f, Random.Range(-0.15f, 0.15f)), 100, 1.2f);
                    if (i >= 0) D(i).FadeIn = 0f;
                }
            }
            if (p.LocalAi1 < 60f && Random.Range(0, 60) == 0)
            {
                int i = PointDust(c, 31, RotateBy(v * 0.35f, Random.Range(-0.15f, 0.15f)), 100, 1.2f);
                if (i >= 0) D(i).FadeIn = 0f;
            }
            if (p.LocalAi1 > 10f)
            {
                v = RotateBy(v, p.Dir * Mathf.Abs(p.Ai0) * p.LocalAi1 * 0.35f);
                v *= 0.97f;
            }
            if (Mathf.Abs(v.x) + Mathf.Abs(v.y) > 16f) v *= 0.95f;
            if (Mathf.Abs(v.x) + Mathf.Abs(v.y) < 12f) v *= 1.05f;
            p.TRot = Mathf.Atan2(v.y, v.x) + 3.14f;
            p.Vel = Flip(v);
        }

        static void DrawLeaf(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Draw(FxBatch.Frame("Projectile_206", 0, p.Frame * 16, 24, 16, 12f, 7f), px + new Vector2(5f, 0f), new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale);
        }

        /// <summary>P:76009: a rustle (Grass) and green bits, unless it had faded out already.</summary>
        static void KillLeaf(Proj p)
        {
            if (p.Alpha >= 1f) return;
            Sound("Grass");
            var tl = CenterPx(p) - new Vector2(7f, 7f);
            for (int k = 0; k < 5; k++) BoxDust(tl, 14, 14, 40);
        }
    }
}
