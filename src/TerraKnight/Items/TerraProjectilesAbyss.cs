using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Abyss Shriek's weapons' shots (Items/Abyss.cs; Terraria's space, px y down), black with silver edges and a little
    /// lilac, as the Shade Soul's (Shade's palette; layered black over silver only as one picture, two sprites in one place
    /// being drawn in no set order):
    /// - the Abyss Yoyo (AbyssYoyo, on the yoyos' own code): its first six seconds, a black ripple off a blow at most every
    ///   Abyss.RippleCooldown (AbyssRipple, everything within Abyss.RipplePx); its last six, a shadow rend on every enemy it
    ///   strikes (AbyssRend: a black blade of light flying in from beyond that one, through it);
    /// - the Void Sniper's shot (VoidTracer as seen, VoidSnipe its blow on the first enemy on the line: through walls, or
    ///   stopped at the first wall when aimed), the aim's sight (VoidSight), the aimed shot's mark (VoidMark) and the strike
    ///   a marked enemy struck again calls, spending the mark (VoidBomb: dark bombs from the player through walls, bursting as ShadeBlast);
    /// - the Abyss Shriek Staff's three (AbyssTriad: at the cursor short of walls, three bursts, AbyssPulse, then little souls
    ///   trailing black threads, AbyssThread, flung out and then charging, through walls);
    /// - the dirge soul's laser (DirgeLaser as seen, DirgeLaserHit its blow on the first enemy, stopped at walls).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        public static int DevAbyssRipples, DevAbyssRends, DevSnipes, DevAimedSnipes, DevVoidMarks, DevVoidStrikes, DevVoidBombs;
        public static int DevAbyssCasts, DevAbyssPulses, DevAbyssThreads, DevDirgeLasers;

        static void RegisterAbyss()
        {
            specs[ProjKind.AbyssRipple] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 16, Penetrate = -1, TileCollide = false, OwnMove = true, OncePerNpc = true, PushFromCentre = true,
                CanDamage = p => p.Ticks < 3, HitArea = p => (p.Pos, Vector2.one * ((Abyss.RipplePx + 48f) * 2f * Px)), HitTest = (p, col) => Within(p, col, Abyss.RipplePx),
                Update = (t, p) => { }, Draw = (t, fx, p, px) => DrawAbyssRipple(fx, p, px, Abyss.RipplePx, 14f), OnHit = (t, p, hm) => AbyssSpark(p, hm, 4), OnKill = (t, p) => { },
            };
            // A rend (Target its enemy): a black blade of light flying in from out beyond it (the user's), through it and on,
            // one blow on that enemy alone as it passes.
            specs[ProjKind.AbyssRend] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 16, Penetrate = -1, TileCollide = false, OncePerNpc = true, TrailLength = 5,
                HitArea = p => (p.Pos, Vector2.one * (160f * Px)),
                HitTest = (p, col) => p.Target != null && col.GetComponentInParent<HealthManager>() == p.Target && Within(p, col, 16f),
                Update = (t, p) => StepAbyssRend(p), Draw = (t, fx, p, px) => DrawAbyssRend(fx, p, px), OnHit = (t, p, hm) => AbyssSpark(p, hm, 6), OnKill = (t, p) => { },
            };
            // The sniper's shriek where a shot lands: a small black ripple, seen only.
            specs[ProjKind.AbyssHitRing] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 12, Penetrate = -1, TileCollide = false, OwnMove = true, CanDamage = p => false,
                Update = (t, p) => { }, Draw = (t, fx, p, px) => DrawAbyssRipple(fx, p, px, 30f, 8f), OnKill = (t, p) => { },
            };
            specs[ProjKind.VoidTracer] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 10, Penetrate = -1, TileCollide = false, OwnMove = true, CanDamage = p => false,
                Light = new Color(0.2f, 0.14f, 0.3f), Update = (t, p) => { }, Draw = (t, fx, p, px) => DrawVoidTracer(fx, p, px), OnKill = (t, p) => { },
            };
            // The shot's blow (Ai1 1 aimed): along the line from the muzzle (Point) to where it stopped, on the first enemy there.
            specs[ProjKind.VoidSnipe] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 3, Penetrate = 1, TileCollide = false, OwnMove = true, Beam = p => (p.Point, CenterPx(p), 8f),
                Update = (t, p) => { }, Draw = (t, fx, p, px) => { }, OnHit = (t, p, hm) => t.VoidSnipeHit(p, hm), OnKill = (t, p) => { },
            };
            specs[ProjKind.VoidMark] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = Abyss.MarkTicks, Penetrate = -1, TileCollide = false, OwnMove = true, CanDamage = p => false,
                Update = (t, p) => StepVoidMark(p), Draw = (t, fx, p, px) => DrawVoidMark(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.VoidSight] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 3, Penetrate = -1, TileCollide = false, OwnMove = true, CanDamage = p => false,
                Update = (t, p) => { }, Draw = (t, fx, p, px) => DrawVoidSight(fx, p, px), OnKill = (t, p) => { },
            };
            // A bomb (Target its enemy, Ai0 its updates): up and out, then after its enemy through walls, bursting there.
            specs[ProjKind.VoidBomb] = new Spec
            {
                Ai = Ai.Custom, Texture = Abyss.Tex("shell"), W = 12, H = 12, Time = 150, Penetrate = -1, TileCollide = false, CanDamage = p => false, TrailLength = 6,
                Light = new Color(0.22f, 0.15f, 0.34f),
                Update = (t, p) => t.StepVoidBomb(p), Draw = (t, fx, p, px) => DrawVoidBomb(fx, p, px), OnKill = (t, p) => t.VoidBombBurst(p),
            };
            // The three (Ai0 their ticks, TRot their turn toward the cast's way).
            specs[ProjKind.AbyssTriad] = new Spec
            {
                Ai = Ai.Custom, Texture = Abyss.Tex("triad"), W = 4, H = 4, Time = 90, Penetrate = -1, TileCollide = false, OwnMove = true, CanDamage = p => false,
                Light = new Color(0.25f, 0.18f, 0.4f),
                Update = (t, p) => t.StepAbyssTriad(p), Draw = (t, fx, p, px) => DrawAbyssTriad(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.AbyssPulse] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 16, Penetrate = -1, TileCollide = false, OwnMove = true, OncePerNpc = true, PushFromCentre = true,
                CanDamage = p => p.Ticks < 2, HitArea = p => (p.Pos, Vector2.one * ((Abyss.PulsePx + 48f) * 2f * Px)), HitTest = (p, col) => Within(p, col, Abyss.PulsePx),
                Update = (t, p) => { }, Draw = (t, fx, p, px) => DrawAbyssRipple(fx, p, px, Abyss.PulsePx, 14f), OnHit = (t, p, hm) => AbyssSpark(p, hm, 5), OnKill = (t, p) => { },
            };
            // A little soul of the three (Ai0 its updates, Ai1 its weave): flung out harmless (the user's: out first, then the charge), then at an enemy.
            specs[ProjKind.AbyssThread] = new Spec
            {
                Ai = Ai.Custom, W = 10, H = 10, Time = 150, Penetrate = 1, TileCollide = false, OncePerNpc = true, TrailLength = 10,
                CanDamage = p => p.Ai0 >= Abyss.ThreadOut,
                Update = (t, p) => StepAbyssThread(p), Draw = (t, fx, p, px) => DrawAbyssThread(fx, p, px), OnHit = (t, p, hm) => AbyssSpark(p, hm, 3), OnKill = (t, p) => ThreadFades(p),
            };
            specs[ProjKind.DirgeLaser] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 2, Penetrate = -1, TileCollide = false, OwnMove = true, CanDamage = p => false,
                Light = new Color(0.2f, 0.14f, 0.3f), Update = (t, p) => { }, Draw = (t, fx, p, px) => DrawDirgeLaser(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.DirgeLaserHit] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 2, Penetrate = 1, TileCollide = false, OwnMove = true, Beam = p => (p.Point, CenterPx(p), 8f),
                Update = (t, p) => { }, Draw = (t, fx, p, px) => { }, OnHit = (t, p, hm) => AbyssSpark(p, hm, 3), OnKill = (t, p) => { },
            };
        }

        /// <summary>A blow landing: black motes off the enemy, a few lilac; the void's sound.</summary>
        static void AbyssSpark(Proj p, HealthManager hm, int count)
        {
            var at = hm != null ? TerraParticles.ToPx(Centre(hm)) : CenterPx(p);
            for (int k = 0; k < count; k++) ShadeDust(at + Random.insideUnitCircle * 6f, Random.insideUnitCircle * 3f, Random.Range(0.9f, 1.3f), k % 3 == 0 ? 240 : 109);
            Sound("tk_shade_hit_*", 0.35f);
        }

        /// <summary>
        /// A black ripple (the yoyo's, the staff's bursts): a black ring opening to its reach over its first ticks, a silver
        /// one just outside it (rings drawn apart, never one over the other), a lilac breath, a black heart fading.
        /// </summary>
        static void DrawAbyssRipple(FxBatch fx, Proj p, Vector2 px, float reach, float open)
        {
            float k = Mathf.Clamp01(p.Ticks / open), o = 1f - Mathf.Clamp01(p.Ticks / 16f);
            if (o <= 0.01f) return;
            float r = reach * (0.25f + 0.75f * Mathf.Sqrt(k));
            var ring = FxBatch.Whole(Shade.Tex("ring"));
            var disc = FxBatch.Whole(Shade.Tex("disc"));
            fx.Draw(disc, px, Shade.Dark(Shade.Void, 0.35f * o * o), 0f, r * 0.85f / 15.6f);
            fx.Draw(ring, px, SoulWave.Glow(Shade.Lilac, 0.3f * o), 0f, r * 1.12f / 31f, overPlayer: true);
            fx.Draw(ring, px, Shade.Dark(Shade.Void, 0.95f * o), 0f, r / 31f, overPlayer: true);
            fx.Draw(ring, px, Shade.Dark(Shade.Void, 0.8f * o), 0f, r * 0.95f / 31f, overPlayer: true);
            fx.Draw(ring, px, Shade.Dark(Shade.Silver, 0.85f * o), 0f, r * 1.06f / 31f, overPlayer: true);
            fx.Light(px, new Color(0.25f, 0.18f, 0.4f) * o, r / 14f);
        }

        // ------------------------------------------------------------------ the Abyss Yoyo

        /// <summary>
        /// The Abyss Yoyo's blow landed (YoyoAfterHit): in its first Abyss.YoyoPhase seconds a ripple off the yoyo, at most
        /// once in Abyss.RippleCooldown of its clock (Bounces the clock it may next ripple at); after them, a rend on the
        /// enemy struck, every blow.
        /// </summary>
        void AbyssYoyoHit(Proj p, HealthManager hm)
        {
            float seconds = p.LocalAi0 / 60f / ((1f + YoyoMeleeSpeed(p)) / 2f);
            if (seconds < Abyss.YoyoPhase)
            {
                if (p.LocalAi0 < p.Bounces) return;
                p.Bounces = (int)p.LocalAi0 + Abyss.RippleCooldown;
                var r = Spawn(ProjKind.AbyssRipple, p.Pos, Vector2.zero, Mathf.Max(1, Mathf.RoundToInt(p.Damage * Abyss.RippleShare)), p.Knockback * 0.5f, p.Crit);
                if (r == null) return;
                r.Melee = true;
                DevAbyssRipples++;
                Sound("tk_abyss_ripple", 0.5f);
                return;
            }
            // From a way at random, Abyss.RendFromPx out, in at Abyss.RendSpeed through its middle.
            float a = Random.value * Mathf.PI * 2f;
            var inward = -new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var from = TerraParticles.ToPx(Centre(hm)) - inward * Abyss.RendFromPx;
            var rend = Spawn(ProjKind.AbyssRend, TerraParticles.ToWorld(from), Flip(inward * Abyss.RendSpeed), Mathf.Max(1, Mathf.RoundToInt(p.Damage * Abyss.RendShare)), p.Knockback * 0.3f, p.Crit);
            if (rend == null) return;
            rend.Target = hm;
            rend.TRot = Mathf.Atan2(inward.y, inward.x);
            rend.Melee = true;
            DevAbyssRends++;
            Sound("tk_abyss_rend", 0.45f);
        }

        /// <summary>The yoyo's own look over the yoyos' drawing: a lilac breath, black afterimages along its trail; a stronger breath in its second half.</summary>
        static void DrawAbyssYoyoGlow(FxBatch fx, Proj p, Vector2 px, bool over)
        {
            var spr = FxBatch.Whole(Abyss.Tex("yoyo"));
            float seconds = p.LocalAi0 / 60f;
            bool rending = seconds >= Abyss.YoyoPhase && p.Ai0 >= 0f;
            int n = p.Trail.Count;
            for (int k = Mathf.Min(5, n - 1); k >= 1; k--)
            {
                float f = 1f - k / 6f;
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[k]), Shade.Dark(Shade.Ink, 0.35f * f), p.TRot, p.Scale * (0.85f + 0.15f * f));
            }
            fx.Draw(spr, px, SoulWave.Glow(Shade.Lilac, rending ? 0.4f : 0.2f), p.TRot, p.Scale * (rending ? 1.25f : 1.12f), overPlayer: over);
            if (rending && Random.value < 0.4f) ShadeDust(px + Random.insideUnitCircle * 10f, Random.insideUnitCircle * 1.2f, Random.Range(0.8f, 1.1f), Random.value < 0.3f ? 240 : 109);
        }

        /// <summary>A rend on its way in: turning after its enemy till it has struck (Hits), then straight on.</summary>
        static void StepAbyssRend(Proj p)
        {
            var t = p.Target;
            if (p.Hits == 0 && t != null && !t.isDead)
            {
                var to = TerraParticles.ToPx(Centre(t)) - CenterPx(p);
                if (to.sqrMagnitude > 4f) p.Vel = Vector2.Lerp(p.Vel, Flip(to.normalized * Abyss.RendSpeed), 0.3f);
            }
            if (p.Vel.sqrMagnitude > 0.01f) p.TRot = Mathf.Atan2(-p.Vel.y, p.Vel.x);
            if (Random.value < 0.5f) ShadeDust(CenterPx(p) - TV(p).normalized * 6f, -TV(p) * 0.08f, Random.Range(0.7f, 1f), Random.value < 0.25f ? 240 : 109);
        }

        /// <summary>A rend: the black crescent in its silver rim (one picture, abyss_crescent.png) bulging forward along its flight, its afterimages, a lilac breath; in fast, fading after it has gone through.</summary>
        static void DrawAbyssRend(FxBatch fx, Proj p, Vector2 px)
        {
            var blade = FxBatch.Frame(Abyss.Tex("crescent"), 0, 0, 20, 40, 13f, 20f);
            float o = Remap01(p.Ticks, 0f, 2f) * Remap01(p.TimeLeft, 0f, 5f);
            if (o <= 0.01f) return;
            int n = p.Trail.Count;
            for (int k = n - 1; k >= 1; k--)
            {
                float f = 1f - k / (float)(n + 1);
                fx.Draw(blade, TerraParticles.ToPx(p.Trail[k]), Shade.Dark(Shade.Ink, 0.4f * f * o), p.TRot, 0.9f * (0.8f + 0.2f * f));
            }
            fx.Draw(blade, px, SoulWave.Glow(Shade.Lilac, 0.3f * o), p.TRot, 1.05f, overPlayer: true);
            fx.Draw(blade, px, Shade.Dark(Color.white, o), p.TRot, 0.95f, overPlayer: true);
        }

        // ------------------------------------------------------------------ the Void Sniper

        /// <summary>
        /// The sniper's shot, at once from its muzzle (the player's middle if a wall is between) along the aim: plain, through
        /// walls to its reach; aimed, stopped at the first wall (the user's); the first enemy on the way takes it. Its tracer,
        /// its kick, a black flash at the muzzle.
        /// </summary>
        public void ShootVoidSniper(HeroController hero, Vector2 mouse, int damage, float knockback, float crit, bool aimed)
        {
            if (hero == null) return;
            var centre = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var dir = TerraParticles.ToPx(mouse) - centre;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            float facing = dir.x < 0f ? -1f : 1f;
            var muzzle = Abyss.RiflePoint(centre, dir, facing, Abyss.Muzzle);
            if (Physics2D.Linecast(TerraParticles.ToWorld(centre), TerraParticles.ToWorld(muzzle), 1 << ChunkColliders.TerrainLayer)) muzzle = centre;
            var from = TerraParticles.ToWorld(muzzle);
            var wdir = Flip(dir).normalized;
            float reach = Abyss.SnipeReachPx * Px;
            float wall = aimed ? BeamWall(from, wdir, reach) : reach;
            var struck = BeamTarget(from, wdir, wall, out var hitAt);
            var end = struck != null ? hitAt : from + wdir * wall;
            var tracer = Spawn(ProjKind.VoidTracer, end, Vector2.zero, 0, 0f, 0f);
            if (tracer != null)
            {
                tracer.Point = muzzle;
                tracer.Ai1 = aimed ? 1f : 0f;
            }
            var blow = Spawn(ProjKind.VoidSnipe, struck != null ? hitAt + wdir * (4f * Px) : end, wdir, damage, knockback, crit);
            if (blow != null)
            {
                blow.Point = muzzle;
                blow.Ai1 = aimed ? 1f : 0f;
            }
            if (struck == null && aimed && wall < reach - 0.01f)
            {
                var endPx = TerraParticles.ToPx(end);
                for (int k = 0; k < 4; k++) ShadeDust(endPx, -dir * Random.Range(1f, 3f) + Random.insideUnitCircle * 2f, Random.Range(0.7f, 1f), k == 0 ? 240 : 109);
            }
            DevSnipes++;
            if (aimed) DevAimedSnipes++;
            Abyss.Kick(aimed ? 5f : 7f, aimed ? 4f : 8f);
            Player.TerraCamera.Punch(0.03f, 0.12f);
            Sound("tk_void_snipe_*", 0.7f);
            for (int k = 0; k < 6; k++) ShadeDust(muzzle, dir * Random.Range(1.5f, 4.5f) + Random.insideUnitCircle, Random.Range(0.9f, 1.3f), k % 3 == 0 ? 240 : 109);
        }

        /// <summary>The shot struck: a mark already on it calls the strike and is spent; else an aimed shot marks it (so aimed shots go mark, bombs, mark...).</summary>
        void VoidSnipeHit(Proj p, HealthManager hm)
        {
            if (hm == null) return;
            var at = TerraParticles.ToPx(Centre(hm));
            for (int k = 0; k < 10; k++)
            {
                float a = k / 10f * Mathf.PI * 2f;
                ShadeDust(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1.5f, 3f), Random.Range(0.9f, 1.3f), k % 3 == 0 ? 240 : 109);
            }
            Spawn(ProjKind.AbyssHitRing, Centre(hm), Vector2.zero, 0, 0f, 0f);
            Sound("tk_void_hit", 0.55f);
            var mark = VoidMarkOn(hm);
            if (mark != null)
            {
                Kill(mark);
                VoidStrike(p, hm);
            }
            else if (p.Ai1 == 1f && !hm.isDead) MarkVoid(hm);
        }

        Proj VoidMarkOn(HealthManager hm)
        {
            foreach (var q in live)
                if (!q.Dead && q.Kind == ProjKind.VoidMark && q.Target == hm) return q;
            return null;
        }

        /// <summary>Dev: whether an enemy bears the sniper's mark.</summary>
        public bool DevVoidMarked(HealthManager hm) => VoidMarkOn(hm) != null;

        void MarkVoid(HealthManager hm)
        {
            var m = Spawn(ProjKind.VoidMark, Centre(hm), Vector2.zero, 0, 0f, 0f);
            if (m == null) return;
            m.Target = hm;
            m.LocalAi0 = 14f;
            DevVoidMarks++;
            Sound("tk_void_mark", 0.5f);
        }

        static void StepVoidMark(Proj p)
        {
            if (p.Target == null || p.Target.isDead || !p.Target.gameObject.activeInHierarchy)
            {
                p.TimeLeft = 1;
                return;
            }
            p.Pos = Centre(p.Target);
            if (p.LocalAi0 > 0f) p.LocalAi0--;
        }

        /// <summary>The mark: a black reticle round the enemy, four silver-edged ticks pointing in, turning slowly, a lilac breath; snapping in from wider as it lands.</summary>
        static void DrawVoidMark(FxBatch fx, Proj p, Vector2 px)
        {
            float r = 24f;
            var col = p.Target != null ? p.Target.GetComponent<Collider2D>() : null;
            if (col != null) r = Mathf.Clamp(Mathf.Max(col.bounds.extents.x, col.bounds.extents.y) / Px + 8f, 18f, 80f);
            float snap = 1f + 0.8f * (p.LocalAi0 / 14f);
            float o = Remap01(p.TimeLeft, 0f, 40f);
            r *= snap;
            var ring = FxBatch.Whole(Shade.Tex("ring"));
            fx.Draw(ring, px, SoulWave.Glow(Shade.Lilac, 0.3f * o), 0f, r * 1.1f / 31f, overPlayer: true);
            fx.Draw(ring, px, Shade.Dark(Shade.Void, 0.9f * o), 0f, r / 31f, overPlayer: true);
            var tick = FxBatch.Frame(Shade.Tex("raybar"), 0, 0, 4, 7, 0f, 3.5f);
            float turn = p.Ticks * 0.03f;
            for (int i = 0; i < 4; i++)
            {
                float a = turn + i * Mathf.PI / 2f;
                var outer = px + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r + 7f);
                fx.Draw(tick, outer, Shade.Dark(Color.white, o), a + Mathf.PI, new Vector2(11f / 4f, 5f / 7f), overPlayer: true);
            }
        }

        /// <summary>
        /// A marked enemy struck again: Abyss.Bombs dark bombs from the player, flung up and out in a fan, each a share of
        /// the shot, after that enemy through walls.
        /// </summary>
        void VoidStrike(Proj shot, HealthManager hm)
        {
            if (!Holder(out var owner)) return;
            int n = Abyss.Bombs;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(shot.Damage * Abyss.BombShare));
            for (int k = 0; k < n; k++)
            {
                float a = (-90f + (k - (n - 1) * 0.5f) * 30f + Random.Range(-6f, 6f)) * Mathf.Deg2Rad;
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(6f, 8f);
                var b = Spawn(ProjKind.VoidBomb, TerraParticles.ToWorld(owner + v.normalized * 12f), Flip(v), dmg, shot.Knockback * 0.6f, shot.Crit);
                if (b == null) continue;
                b.Target = hm;
                b.Class = shot.Class;
                b.Ai0 = -k * 2f;
                DevVoidBombs++;
            }
            DevVoidStrikes++;
            Sound("tk_void_missile", 0.65f);
        }

        /// <summary>A bomb: slowing as it rises a moment, then after its enemy, quickening, through everything; it bursts on reaching it (or at the end of its time).</summary>
        void StepVoidBomb(Proj p)
        {
            p.Ai0++;
            var me = CenterPx(p);
            var t = p.Target;
            if (p.Ai0 < 12f) p.Vel *= 0.94f;
            else if (t != null && !t.isDead && t.gameObject.activeInHierarchy)
            {
                var to = TerraParticles.ToPx(Centre(t)) - me;
                float speed = Mathf.Min(16f, 6f + (p.Ai0 - 12f) * 0.45f);
                if (to.magnitude <= speed + 8f)
                {
                    Kill(p);
                    return;
                }
                p.Vel = Vector2.Lerp(p.Vel, Flip(to.normalized * speed), 0.18f);
            }
            if (p.Vel.sqrMagnitude > 0.01f) p.TRot = Mathf.Atan2(-p.Vel.y, p.Vel.x);
            if (Random.value < 0.6f) ShadeDust(me - TV(p).normalized * 8f, -TV(p) * 0.1f + Random.insideUnitCircle * 0.4f, Random.Range(0.8f, 1.1f), Random.value < 0.25f ? 240 : 109);
        }

        /// <summary>A bomb's burst (ShadeBlast: its black core and silver rim), on everything within Abyss.BombBlastPx, through walls.</summary>
        void VoidBombBurst(Proj p)
        {
            var b = Spawn(ProjKind.ShadeBlast, p.Pos, Vector2.zero, p.Damage, p.Knockback, p.Crit);
            if (b != null)
            {
                b.Ai0 = Abyss.BombBlastPx;
                b.Ai1 = 2f;
                b.Class = p.Class;
            }
            var at = CenterPx(p);
            for (int k = 0; k < 10; k++)
            {
                float a = k / 10f * Mathf.PI * 2f;
                ShadeDust(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(2f, 3.5f), Random.Range(1f, 1.4f), k % 3 == 0 ? 240 : 109);
            }
            Sound("tk_void_blast", 0.45f);
        }

        /// <summary>A bomb: the pack's shell (24 px) along its flight, its black afterimages, a lilac breath.</summary>
        static void DrawVoidBomb(FxBatch fx, Proj p, Vector2 px)
        {
            var shell = FxBatch.Whole(Abyss.Tex("shell"));
            int n = p.Trail.Count;
            for (int k = n - 1; k >= 1; k--)
            {
                float f = 1f - k / (float)(n + 1);
                fx.Draw(shell, TerraParticles.ToPx(p.Trail[k]), Shade.Dark(Shade.Ink, 0.35f * f), p.TRot, 0.7f * (0.8f + 0.2f * f));
            }
            fx.Draw(shell, px, SoulWave.Glow(Shade.Lilac, 0.3f), p.TRot, 0.85f);
            fx.Draw(shell, px, Color.white, p.TRot, 0.75f, overPlayer: true);
        }

        /// <summary>The shot as seen: a thin black line in silver (one picture) from the muzzle to where it stopped, a lilac breath, fading fast; an aimed one a touch thicker.</summary>
        static void DrawVoidTracer(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Point == Vector2.zero) return;
            float k = Mathf.Clamp01(1f - p.Ticks / 10f);
            var d = px - p.Point;
            float len = d.magnitude;
            if (len < 1f || k <= 0f) return;
            float rot = Mathf.Atan2(d.y, d.x), w = (p.Ai1 == 1f ? 1.2f : 1f) * (0.35f + 0.65f * k);
            fx.Draw(ShadeBlock, p.Point, SoulWave.Glow(Shade.Lilac, 0.25f * k), rot, new Vector2(len / 4f, 9f * w / 4f));
            fx.Draw(FxBatch.Frame(Shade.Tex("raybar"), 0, 0, 4, 7, 0f, 3.5f), p.Point, Shade.Dark(Color.white, Mathf.Pow(k, 0.6f)), rot, new Vector2(len / 4f, 5f * w / 7f));
            for (float s = 0f; s <= len; s += 160f) fx.Light(p.Point + d * (s / len), new Color(0.2f, 0.14f, 0.3f) * k, 1.4f);
            if (p.Ticks < 4)
            {
                float m = 1f - p.Ticks / 4f;
                fx.Draw(FxBatch.Whole(Shade.Tex("disc")), p.Point, SoulWave.Glow(Shade.Lilac, 0.5f * m), 0f, (5f + 6f * m) / 15.6f, overPlayer: true);
                fx.Draw(FxBatch.Whole(Shade.Tex("orb_black")), p.Point, Shade.Dark(Color.white, m), 0f, 0.7f + 0.6f * m, overPlayer: true);
            }
        }

        /// <summary>The aim held: its sight kept up a tick more (one at a time).</summary>
        public void KeepVoidSight(HeroController hero)
        {
            foreach (var q in live)
                if (!q.Dead && q.Kind == ProjKind.VoidSight)
                {
                    q.TimeLeft = 3;
                    return;
                }
            var p = Spawn(ProjKind.VoidSight, hero.GetComponent<BoxCollider2D>().bounds.center, Vector2.zero, 0, 0f, 0f);
            if (p != null) p.TimeLeft = 3;
        }

        /// <summary>The aim's sight: a faint lilac line from the muzzle along the aim to the first wall, dashed, and a small black reticle where it ends.</summary>
        static void DrawVoidSight(FxBatch fx, Proj p, Vector2 px)
        {
            var mod = TerraMod.Instance;
            if (mod == null || !Holder(out var centre)) return;
            var dir = TerraParticles.ToPx(mod.DevAim ?? mod.AimPoint) - centre;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            float facing = dir.x < 0f ? -1f : 1f;
            var muzzle = Abyss.RiflePoint(centre, dir, facing, Abyss.Muzzle);
            float wall = BeamWall(TerraParticles.ToWorld(muzzle), Flip(dir).normalized, Abyss.SnipeReachPx * Px) / Px;
            float len = Mathf.Min(wall, 900f);
            float rot = Mathf.Atan2(dir.y, dir.x);
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 8f);
            for (float s = 6f; s < len; s += 14f)
                fx.Draw(ShadeBlock, muzzle + dir * s, SoulWave.Glow(Shade.Lilac, 0.28f * pulse), rot, new Vector2(7f / 4f, 1.4f / 4f));
            var end = muzzle + dir * len;
            var ring = FxBatch.Whole(Shade.Tex("ring"));
            fx.Draw(ring, end, Shade.Dark(Shade.Void, 0.85f), 0f, 7f / 31f, overPlayer: true);
            fx.Draw(ring, end, SoulWave.Glow(Shade.Lilac, 0.35f * pulse), 0f, 8.5f / 31f, overPlayer: true);
        }

        // ------------------------------------------------------------------ the Abyss Shriek Staff

        /// <summary>
        /// The three called up at the cursor, at most Abyss.CastReachPx off, short of the first wall on the way (the user's:
        /// the wall check); turned to the way from the player.
        /// </summary>
        public void CastAbyssTriad(HeroController hero, Vector2 mouse, int damage, float knockback, float crit)
        {
            if (hero == null) return;
            var centre = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var to = TerraParticles.ToPx(mouse) - centre;
            var dir = to.sqrMagnitude > 1e-6f ? to.normalized : Vector2.right;
            float dist = Mathf.Min(to.magnitude, Abyss.CastReachPx);
            float wall = BeamWall(TerraParticles.ToWorld(centre), Flip(dir).normalized, dist * Px + 0.01f) / Px;
            if (wall < dist) dist = Mathf.Max(0f, wall - 14f);
            var at = centre + dir * dist;
            var p = Spawn(ProjKind.AbyssTriad, TerraParticles.ToWorld(at), Vector2.zero, damage, knockback, crit);
            if (p == null) return;
            p.TRot = Mathf.Atan2(dir.y, dir.x) + Mathf.PI / 2f;
            p.Ai0 = 0f;
            DevAbyssCasts++;
            for (int k = 0; k < 12; k++)
            {
                float a = k / 12f * Mathf.PI * 2f;
                ShadeDust(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.4f, 1.1f, k % 4 == 0 ? 240 : 109);
            }
        }

        /// <summary>Dev: where the last three were called up (world), or null.</summary>
        public Vector2? DevTriadAt() => DevPos(ProjKind.AbyssTriad);

        /// <summary>
        /// The three (Ai0 their ticks): showing over Abyss.AppearTicks (frames 0-3); a burst every Abyss.PulseEvery, three
        /// (frames 4-5), each AbyssPulse round them; then drawing back (6-7) and coming apart into Abyss.Threads homing black
        /// threads all round.
        /// </summary>
        void StepAbyssTriad(Proj p)
        {
            p.Ai0++;
            int t = (int)p.Ai0;
            int bursts = Abyss.AppearTicks + Abyss.PulseEvery * Abyss.Pulses;
            p.Frame = t < Abyss.AppearTicks ? Mathf.Min(3, t * 4 / Abyss.AppearTicks) : t < bursts ? 4 + (t / 4) % 2 : Mathf.Min(7, 6 + (t - bursts) / 5);
            for (int k = 0; k < Abyss.Pulses; k++)
                if (t == Abyss.AppearTicks + k * Abyss.PulseEvery)
                {
                    var b = Spawn(ProjKind.AbyssPulse, p.Pos, Vector2.zero, Mathf.Max(1, Mathf.RoundToInt(p.Damage * Abyss.PulseShare)), p.Knockback, p.Crit);
                    if (b != null) b.Class = p.Class;
                    DevAbyssPulses++;
                    Sound("tk_abyss_pulse_" + k, 0.6f + 0.08f * k);
                    Player.TerraCamera.Punch(0.03f + 0.02f * k, 0.12f);
                    var c = CenterPx(p);
                    for (int j = 0; j < 10; j++)
                    {
                        float a = j / 10f * Mathf.PI * 2f;
                        ShadeDust(c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(2.5f, 4f), Random.Range(1f, 1.3f), j % 3 == 0 ? 240 : 109);
                    }
                }
            if (t == bursts + 4)
            {
                var c = CenterPx(p);
                float spin = p.TRot;
                int dmg = Mathf.Max(1, Mathf.RoundToInt(p.Damage * Abyss.ThreadShare));
                for (int k = 0; k < Abyss.Threads; k++)
                {
                    float a = spin + k / (float)Abyss.Threads * Mathf.PI * 2f;
                    var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Abyss.ThreadFling;
                    var th = Spawn(ProjKind.AbyssThread, TerraParticles.ToWorld(c + v.normalized * 14f), Flip(v), dmg, p.Knockback * 0.4f, p.Crit);
                    if (th == null) continue;
                    th.Class = p.Class;
                    th.Ai1 = Random.Range(-1f, 1f);
                    DevAbyssThreads++;
                }
                Sound("tk_abyss_threads", 0.5f);
            }
            if (t >= bursts + 14) Kill(p);
        }

        /// <summary>The three: the pack's frame (64×64, its art facing up), turned to the cast's way, a lilac breath growing to the bursts, its light.</summary>
        static void DrawAbyssTriad(FxBatch fx, Proj p, Vector2 px)
        {
            var frame = FxBatch.Frame(Abyss.Tex("triad"), 0, Mathf.Clamp(p.Frame, 0, 7) * 64, 64, 64, 32f, 32f);
            float shout = p.Frame >= 4 && p.Frame <= 5 ? 1f : p.Frame < 4 ? p.Frame / 4f : 0.4f;
            fx.Draw(frame, px, SoulWave.Glow(Shade.Lilac, 0.15f + 0.25f * shout), p.TRot, 1.1f, overPlayer: true);
            fx.Draw(frame, px, Color.white, p.TRot, 1f, overPlayer: true);
            fx.Light(px, new Color(0.28f, 0.2f, 0.45f) * (0.5f + 0.5f * shout), 3.5f);
        }

        /// <summary>
        /// A little soul (Ai0 its updates, Ai1 its weave): flung out and slowing for Abyss.ThreadOut, harmless; then the charge,
        /// after the nearest enemy within 40 tiles, through walls, weaving a little, quickening.
        /// </summary>
        static void StepAbyssThread(Proj p)
        {
            p.Ai0++;
            if (p.Ai0 < Abyss.ThreadOut)
            {
                p.Vel *= 0.88f;
                p.Frame = 2 + ((int)p.Ai0 / 5) % 2;
            }
            else
            {
                p.Frame = 4 + ((int)p.Ai0 / 4) % 2;
                if (p.Ai0 == Abyss.ThreadOut) Sound("tk_shade_dash", 0.25f);
                var target = p.Target;
                if (target == null || target.isDead || !target.gameObject.activeInHierarchy || p.Immune.ContainsKey(target.GetInstanceID())) target = p.Target = NearestThrough(p, 640f);
                if (target != null)
                {
                    var to = TerraParticles.ToPx(Centre(target)) - CenterPx(p);
                    var side = new Vector2(-to.y, to.x).normalized * (Mathf.Sin(p.Ai0 * 0.35f) * 2.5f * p.Ai1);
                    float speed = Mathf.Min(14f, 8f + (p.Ai0 - Abyss.ThreadOut) * 0.35f);
                    p.Vel = (p.Vel * 5f + Flip(to.normalized * speed + side)) / 6f;
                }
                else p.Vel *= 0.97f;
            }
            p.TRot = Mathf.Atan2(-p.Vel.y, p.Vel.x);
        }

        static void ThreadFades(Proj p)
        {
            var at = CenterPx(p);
            for (int k = 0; k < 3; k++) ShadeDust(at, Random.insideUnitCircle * 1.5f, Random.Range(0.7f, 1f), k == 0 ? 240 : 109);
        }

        /// <summary>
        /// A little soul: the dirge soul's frame, small, turned along its charge (upright while it is flung out), a lilac
        /// breath; its trail a black thread in silver (one picture, abyss_thread.png), thinning back along it.
        /// </summary>
        static void DrawAbyssThread(FxBatch fx, Proj p, Vector2 px)
        {
            var seg = FxBatch.Frame(Abyss.Tex("thread"), 0, 0, 8, 3, 0f, 1.5f);
            float o = Remap01(p.Ticks, 0f, 3f) * Remap01(p.TimeLeft, 0f, 10f);
            var prev = px;
            int n = p.Trail.Count;
            for (int k = 1; k < n; k++)
            {
                var at = TerraParticles.ToPx(p.Trail[k]);
                var d = prev - at;
                float len = d.magnitude;
                if (len > 0.3f && len < 60f)
                {
                    float f = 1f - k / (float)(n + 1);
                    fx.Draw(seg, at, Shade.Dark(Color.white, o * f), Mathf.Atan2(d.y, d.x), new Vector2((len + 0.8f) / 8f, (0.5f + 0.7f * f)));
                }
                prev = at;
            }
            var frame = FxBatch.Frame(Abyss.Tex("dirge"), 0, (p.Frame & 7) * 40, 40, 40, 20f, 20f);
            bool charging = p.Ai0 >= Abyss.ThreadOut;
            float rot = charging ? p.TRot : 0f;
            bool left = TV(p).x < 0f;
            fx.Draw(frame, px, SoulWave.Glow(Shade.Lilac, 0.25f * o), rot, 0.52f, flipY: charging && left, flipX: !charging && left, overPlayer: true);
            fx.Draw(frame, px, Shade.Dark(Color.white, o), rot, 0.46f, flipY: charging && left, flipX: !charging && left, overPlayer: true);
        }

        // ------------------------------------------------------------------ the dirge soul's laser

        /// <summary>
        /// A tick of a dirge soul's laser (Items/MinionsAbyss.cs) from its mouth (px) along a way (Terraria radians): seen to
        /// the first enemy or wall; when it strikes, one blow on that first enemy (the user's: it doesn't go through).
        /// </summary>
        public void DirgeLaser(Vector2 mouthPx, float rot, int damage, float knockback, bool strike)
        {
            var dir = new Vector2(Mathf.Cos(rot), Mathf.Sin(rot));
            var from = TerraParticles.ToWorld(mouthPx);
            var wdir = Flip(dir).normalized;
            float wall = BeamWall(from, wdir, 520f * Px);
            var struck = BeamTarget(from, wdir, wall, out var hitAt);
            var end = struck != null ? hitAt : from + wdir * wall;
            var seen = Spawn(ProjKind.DirgeLaser, end, Vector2.zero, 0, 0f, 0f);
            if (seen != null)
            {
                seen.Point = mouthPx;
                seen.Ai1 = struck != null ? 1f : 0f;
            }
            if (!strike || struck == null) return;
            var blow = SpawnMinionShot(ProjKind.DirgeLaserHit, TerraParticles.ToPx(hitAt + wdir * (4f * Px)), dir, damage, knockback);
            if (blow != null)
            {
                blow.Point = mouthPx;
                DevDirgeLasers++;
            }
        }

        /// <summary>The laser: a broad black core in silver (one picture) with a lilac breath, a little shiver in its width (the user's: thicker, nothing at its end).</summary>
        static void DrawDirgeLaser(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Point == Vector2.zero) return;
            var d = px - p.Point;
            float len = d.magnitude;
            if (len < 1f) return;
            float rot = Mathf.Atan2(d.y, d.x), w = 0.85f + 0.15f * Mathf.Sin(Time.time * 40f);
            fx.Draw(ShadeBlock, p.Point, SoulWave.Glow(Shade.Lilac, 0.28f), rot, new Vector2(len / 4f, 18f * w / 4f));
            fx.Draw(FxBatch.Frame(Shade.Tex("raybar"), 0, 0, 4, 7, 0f, 3.5f), p.Point, Color.white, rot, new Vector2(len / 4f, 10f * w / 7f));
            for (float s = 0f; s <= len; s += 96f) fx.Light(p.Point + d * (s / len), new Color(0.2f, 0.14f, 0.3f), 1.5f);
        }
    }
}
