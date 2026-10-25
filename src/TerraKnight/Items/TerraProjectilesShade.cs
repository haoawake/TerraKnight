using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Shade Soul's weapons' shots (Items/Shade.cs; Terraria's space, px y down), black with the pack's silver edges and a
    /// little lilac (black drawn over what is under it, the lilac added):
    /// - the Shade Edge's cuts (ShadeSwing: the sword itself, drawn from the flat picture along its blade, with only a smear
    ///   behind its tip; the chop over the head to the feet, the sweep a flat ellipse in front, foreshortened, the fling
    ///   over the head), the fling throwing a shadow of the sword (ShadeThrow: out a short way, hanging a moment spinning,
    ///   everything within Shade.SpinPx struck every Shade.SpinCooldown, walls or not, then fading; the sword swings on);
    /// - the Shade Rift Rifle's ray (ShadeRay as seen, ShadeRayHit its blow along the whole line to the first wall, through
    ///   every enemy), its marks (ShadeMark, one per enemy, up to Shade.MaxMarks, orbs circling it) and their bursts
    ///   (ShadeBlast, bigger and harder for more marks);
    /// - the Shade Wraith's ghost (ShadeGhost: to the cursor and kept there while the button is held, never through a wall;
    ///   back slowly when let go, through walls so it can't be stuck; the right click's blink to it after a short wait,
    ///   all the mana spent) and its shocks (ShadeShock: from a ring all round, closing on the ghost through walls).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        public static int DevShadeCuts, DevShadeThrows, DevShadeFades;
        public static int DevRays, DevMarks, DevShadeBlasts, DevShadeBlastHits, DevShadeGhosts, DevGhostCaught, DevGhostReturns, DevGhostNoMana, DevGhostPaid, DevShocks, DevMarkCalls, DevMarkSkipped;

        static void RegisterShade()
        {
            // A cut (Ai0 the way it faces, Ai1 which cut, LocalAi0 its updates, LocalAi1 its length in updates): Point the
            // shoulder, Pos the blade's tip; its blow along the blade.
            specs[ProjKind.ShadeSwing] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 3600, Penetrate = -1, Extra = 2, TileCollide = false, OwnMove = true, OncePerNpc = true,
                Held = true, HoldsWhile = q => false, CanDamage = p => CutLive(p) > 0.05f,
                Beam = p => (p.Point, CenterPx(p), 22f * p.Scale),
                Update = (t, p) => t.StepShadeSwing(p), Draw = (t, fx, p, px) => DrawShadeSwing(fx, p, px),
                OnHit = (t, p, hm) => ShadeSpark(p, hm, 8, 0.8f), OnKill = (t, p) => { },
            };
            // The thrown shadow of the sword (Ai0 0 out, 1 hanging, 2 fading; Ai1 its spin's way; LocalAi0 the phase's ticks; LocalAi1 its way out; Point where it left).
            specs[ProjKind.ShadeThrow] = new Spec
            {
                Ai = Ai.Custom, Texture = Shade.Tex("edge_flat"), W = 8, H = 8, Time = 600, Penetrate = -1, TileCollide = false, OwnMove = true,
                LocalCooldown = Shade.SpinCooldown, Light = new Color(0.22f, 0.16f, 0.32f), CanDamage = p => p.Ai0 < 2f,
                HitArea = p => (p.Pos, Vector2.one * ((Shade.SpinPx + 48f) * 2f * Px)), HitTest = (p, col) => Within(p, col, Shade.SpinPx * p.Scale),
                Update = (t, p) => t.StepShadeThrow(p), Draw = (t, fx, p, px) => DrawShadeThrow(fx, p, px),
                OnHit = (t, p, hm) => ShadeSpark(p, hm, 5, 0.6f), OnKill = (t, p) => { },
            };
            specs[ProjKind.ShadeRay] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = Shade.RayTicks, Penetrate = -1, TileCollide = false, OwnMove = true, CanDamage = p => false,
                Light = new Color(0.2f, 0.14f, 0.3f), Update = (t, p) => { }, Draw = (t, fx, p, px) => DrawShadeRay(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.ShadeRayHit] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 3, Penetrate = -1, TileCollide = false, OwnMove = true, OncePerNpc = true,
                Beam = p => (p.Point, CenterPx(p), 16f),
                Update = (t, p) => { }, Draw = (t, fx, p, px) => { }, OnHit = (t, p, hm) => t.MarkShade(p, hm), OnKill = (t, p) => { },
            };
            // A mark (Target its enemy, Ai0 its stacks, LocalAi0 a pulse when one is added, LocalAi1 the orbs' turn).
            specs[ProjKind.ShadeMark] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = Shade.MarkTicks, Penetrate = -1, TileCollide = false, OwnMove = true, CanDamage = p => false,
                Update = (t, p) => t.StepShadeMark(p), Draw = (t, fx, p, px) => DrawShadeMark(fx, p, px), OnKill = (t, p) => { },
            };
            // A burst of marks (Ai0 its reach in px, Ai1 its stacks): once, on everything within its reach, walls or not.
            specs[ProjKind.ShadeBlast] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 24, Penetrate = -1, TileCollide = false, OwnMove = true, OncePerNpc = true, PushFromCentre = true,
                CanDamage = p => p.Ticks < 2, HitArea = p => (p.Pos, Vector2.one * ((p.Ai0 + 48f) * 2f * Px)), HitTest = (p, col) => Within(p, col, p.Ai0),
                Update = (t, p) => { }, Draw = (t, fx, p, px) => DrawShadeBlast(fx, p, px), OnHit = (t, p, hm) => { DevShadeBlastHits++; ShadeSpark(p, hm, 6, 0.7f); }, OnKill = (t, p) => { },
            };
            // The ghost (Ai0 0 out, 1 coming back; LocalAi0 its ticks; LocalAi1 the mana's clock, then its ticks coming back).
            specs[ProjKind.ShadeGhost] = new Spec
            {
                Ai = Ai.Custom, Texture = Shade.Tex("flight"), W = 18, H = 18, TileBox = new Vector2(16f, 16f), Time = 600, Penetrate = -1, LocalCooldown = 30,
                Held = true, TrailLength = 6, Light = new Color(0.2f, 0.15f, 0.3f), CanDamage = p => p.Ai0 == 0f,
                Update = (t, p) => t.StepShadeGhost(p), Draw = (t, fx, p, px) => DrawShadeGhost(fx, p, px),
                OnHit = (t, p, hm) => ShadeSpark(p, hm, 4, 0.5f), OnKill = (t, p) => { },
                // A wall stops it on that side; it slides along it on the other.
                TileHit = (t, p, hx, hy, before) => { if (hx) p.Vel.x = 0f; if (hy) p.Vel.y = 0f; },
            };
            specs[ProjKind.ShadeShock] = new Spec
            {
                Ai = Ai.Custom, Texture = Shade.Tex("charged"), W = 14, H = 14, Time = 24, Penetrate = -1, TileCollide = false, OncePerNpc = true, TrailLength = 4,
                Update = (t, p) => t.StepShadeShock(p), Draw = (t, fx, p, px) => DrawShadeShock(fx, p, px),
                OnHit = (t, p, hm) => ShadeSpark(p, hm, 3, 0.4f), OnKill = (t, p) => ShockFades(p),
            };
        }

        /// <summary>A black mote (Terraria's dust 109, near black in the mod's light) that floats; 240 for the lilac ones, 54 the violet grey.</summary>
        static void ShadeDust(Vector2 at, Vector2 vel, float scale, int type = 109)
        {
            int i = PointDust(at, type, vel, 0, scale);
            if (i < 0) return;
            D(i).NoGravity = true;
            D(i).Velocity = vel;
        }

        /// <summary>Within a reach (px) of the shot's middle: the nearest point of the collider's box, walls or not.</summary>
        static bool Within(Proj p, Collider2D col, float reachPx)
        {
            var near = (Vector2)col.bounds.ClosestPoint(p.Pos);
            return Vector2.Distance(near, p.Pos) <= reachPx * Px;
        }

        /// <summary>A blow landing: black motes off the enemy along the blow, a few lilac ones, the cut's sound.</summary>
        static void ShadeSpark(Proj p, HealthManager hm, int count, float strength)
        {
            var at = hm != null ? TerraParticles.ToPx(Centre(hm)) : CenterPx(p);
            var dir = TV(p).sqrMagnitude > 1e-6f ? TV(p).normalized : (at - CenterPx(p)).normalized;
            for (int k = 0; k < count; k++)
                ShadeDust(at + Random.insideUnitCircle * 6f, (dir * Random.Range(1f, 4f) + Random.insideUnitCircle * 2.5f) * strength, Random.Range(0.9f, 1.4f));
            for (int k = 0; k < Mathf.Max(1, count / 3); k++) ShadeDust(at, Random.insideUnitCircle * 3f, 0.9f, 240);
            Sound("tk_shade_hit_*", 0.35f + 0.3f * strength);
        }

        // ------------------------------------------------------------------ the Shade Edge

        /// <summary>
        /// A cut of the Shade Edge: the sword swung over its use, its blow along the blade: the chop, the sweep, and the
        /// third, an overhand fling that throws a shadow of the sword toward the cursor partway through (ThrowShade; the
        /// user's: it strikes and fades, the swinging goes on). A new cut ends the last one's swing.
        /// </summary>
        public void ShadeCut(HeroController hero, Vector2 mouse, int cut, int damage, float knockback, float crit, int useTicks)
        {
            if (hero == null) return;
            var centre = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var aim = TerraParticles.ToPx(mouse) - centre;
            float d = aim.x < 0f ? -1f : 1f;
            var def = ItemDb.Get(ItemType.ShadeEdge);
            float scale = def.Scale * Equipment.ItemScale(def);
            foreach (var q in live)
                if (!q.Dead && q.Kind == ProjKind.ShadeSwing) Kill(q);
            DevShadeCuts++;
            var p = Spawn(ProjKind.ShadeSwing, TerraParticles.ToWorld(centre), new Vector2(d, 0f), damage, knockback, crit);
            if (p == null) return;
            p.Ai0 = d;
            p.Ai1 = cut;
            p.LocalAi0 = 0f;
            p.LocalAi1 = Mathf.Max(8f, useTicks) * 3f;
            p.Scale = scale * Shade.SwordScale;
            p.Point = centre;
            p.Melee = true;
            // The fling's throw is still to come (Bounces 1 once it is gone).
            p.Bounces = 0;
            Sound("tk_shade_swing_" + cut, 0.75f);
        }

        /// <summary>The fling's shadow of the sword, from the shoulder toward the cursor (the swing's own size; its spin a share of the blow).</summary>
        void ThrowShade(Proj swing)
        {
            var mod = TerraMod.Instance;
            var from = swing.Point;
            var aim = mod != null ? TerraParticles.ToPx(mod.DevAim ?? mod.AimPoint) - from : new Vector2(swing.Ai0, 0f);
            var dir = aim.sqrMagnitude > 1e-6f ? aim.normalized : new Vector2(swing.Ai0, 0f);
            var start = from + dir * 16f;
            var s = Spawn(ProjKind.ShadeThrow, TerraParticles.ToWorld(start), Flip(dir), Mathf.Max(1, Mathf.RoundToInt(swing.Damage * Shade.SpinShare)), swing.Knockback, swing.Crit);
            if (s == null) return;
            s.Point = start;
            s.Ai0 = 0f;
            s.Ai1 = swing.Ai0;
            s.LocalAi1 = Mathf.Atan2(dir.y, dir.x);
            s.TRot = s.LocalAi1;
            s.Scale = swing.Scale / Shade.SwordScale;
            s.Melee = true;
            s.Class = swing.Class;
            DevShadeThrows++;
            Sound("tk_shade_throw", 0.8f);
            for (int k = 0; k < 6; k++) ShadeDust(start, dir * Random.Range(2f, 4f) + Random.insideUnitCircle, Random.Range(1f, 1.4f));
        }

        /// <summary>How far into its blow a cut is (0 before and after its live part): the swing's middle 90%.</summary>
        static float CutLive(Proj p)
        {
            float t = p.LocalAi0 / Mathf.Max(1f, p.LocalAi1);
            return Remap01(t, 0.04f, 0.12f) * (1f - Remap01(t, 0.9f, 0.96f));
        }

        /// <summary>
        /// The blade at a point of a cut (0..1): its way (Terraria radians, y down) and its length against the whole (the
        /// sweep foreshortened). The chop from over the head and behind it down to the feet in front; the sweep, its tip on a
        /// flat ellipse round the player: from behind (short, up), out level at full reach, round in front (short, down); the
        /// fling from low behind the back over the head to the front. Eased in and out. Mirrored facing left.
        /// </summary>
        static (float rot, float len) ShadeBlade(int cut, float face, float t)
        {
            t = Mathf.Clamp01(t);
            float e = t * t * (3f - 2f * t);
            float a, len = 1f;
            if (cut == 0) a = Mathf.Lerp(-118f, 72f, e) * Mathf.Deg2Rad;
            else if (cut == 2) a = Mathf.Lerp(-160f, 40f, e) * Mathf.Deg2Rad;
            else
            {
                float phi = Mathf.Lerp(-105f, 80f, e) * Mathf.Deg2Rad;
                float x = Mathf.Cos(phi), y = 0.3f * Mathf.Sin(phi);
                a = Mathf.Atan2(y, x);
                len = Mathf.Sqrt(x * x + y * y);
            }
            if (face < 0f) a = Mathf.PI - a;
            return (a, len);
        }

        /// <summary>The shoulder the sword turns on: a little above the middle, toward the way the player faces.</summary>
        static Vector2 Shoulder(Vector2 centre, float face) => centre + new Vector2(face * 3f, -4f);

        void StepShadeSwing(Proj p)
        {
            if (!Holder(out var centre))
            {
                Kill(p);
                return;
            }
            p.LocalAi0++;
            if (p.LocalAi0 > p.LocalAi1)
            {
                Kill(p);
                return;
            }
            p.TimeLeft = 2;
            float t = p.LocalAi0 / p.LocalAi1;
            var (rot, len) = ShadeBlade((int)p.Ai1, p.Ai0, t);
            float reach = Shade.BladePx * p.Scale * len;
            p.Point = Shoulder(centre, p.Ai0);
            var dir = new Vector2(Mathf.Cos(rot), Mathf.Sin(rot));
            p.Pos = TerraParticles.ToWorld(p.Point + dir * reach);
            p.TRot = rot;
            if (p.Ai1 == 2f && p.Bounces == 0 && t >= Shade.ThrowAt)
            {
                p.Bounces = 1;
                ThrowShade(p);
            }
            if (CutLive(p) > 0.5f && Random.value < 0.25f)
            {
                float side = p.Ai1 == 0f ? p.Ai0 : -p.Ai0;
                var tangent = new Vector2(-dir.y, dir.x) * side;
                ShadeDust(CenterPx(p), tangent * Random.Range(1f, 2.5f), Random.Range(0.8f, 1.1f), Random.value < 0.2f ? 240 : 109);
            }
        }

        /// <summary>The flat sword (Resources/shade_edge_flat.png) from its grip at a point, along a way, its blade shortened to a share.</summary>
        static void DrawFlatSword(FxBatch fx, Vector2 grip, float rot, float len, float scale, bool flip, Color c)
        {
            var sword = FxBatch.Frame(Shade.Tex("edge_flat"), 0, 0, 61, 27, Shade.FlatGrip.x, Shade.FlatGrip.y);
            fx.Draw(sword, grip, c, rot, new Vector2(scale * Mathf.Max(0.2f, len), scale), flipY: flip, overPlayer: true);
        }

        /// <summary>A white block (Resources/shade_px.png) pivoting on its left middle: bars and spokes, tinted.</summary>
        static Sprite ShadeBlock => FxBatch.Frame(Shade.Tex("px"), 0, 0, 4, 4, 0f, 2f);

        /// <summary>
        /// A cut drawn: the smear behind the blade's tip (the user's: only that), a crescent of black spokes along the blade's
        /// last stretch of the swing, wide by the blade and narrowing to the tip's line further back, fading; a silver rim on
        /// its outer edge (the spoke's own picture, shade_spoke.png: two sprites in one place are drawn in no set order) and a
        /// lilac breath under it; then the sword.
        /// </summary>
        static void DrawShadeSwing(FxBatch fx, Proj p, Vector2 px)
        {
            float t = Mathf.Clamp01(p.LocalAi0 / Mathf.Max(1f, p.LocalAi1));
            int cut = (int)p.Ai1;
            float face = p.Ai0, live = CutLive(p);
            var shoulder = p.Point;
            var block = ShadeBlock;
            var spoke = FxBatch.Frame(Shade.Tex("spoke"), 0, 0, 16, 4, 0f, 2f);
            const int n = 26;
            const float span = 0.3f;
            if (live > 0.01f)
                for (int pass = 0; pass < 2; pass++)
                    for (int k = n; k >= 0; k--)
                    {
                        float tk = t - k * span / n;
                        if (tk < 0.04f) continue;
                        var (a, l) = ShadeBlade(cut, face, tk);
                        var (an, _) = ShadeBlade(cut, face, tk - span / n);
                        float r = Shade.BladePx * p.Scale * l;
                        float f = 1f - k / (float)(n + 1);
                        float inner = r * Mathf.Lerp(0.93f, 0.55f, f), len = r - inner;
                        // As wide as the arc to the spoke before it at the tip, and a little over.
                        float gap = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, an * Mathf.Rad2Deg)) * Mathf.Deg2Rad * r + 1.5f;
                        float o = live * f;
                        var at = shoulder + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * inner;
                        if (pass == 0) fx.Draw(block, at, SoulWave.Glow(Shade.Lilac, 0.16f * o), a, new Vector2((len + 4f) / 4f, (gap + 2f) / 4f));
                        else fx.Draw(spoke, at, Shade.Dark(Color.white, 0.92f * o), a, new Vector2((len + 1.6f) / 16f, gap / 4f));
                    }
            var (ar, lr) = ShadeBlade(cut, face, t);
            float fade = Remap01(t, 0f, 0.06f) * (1f - Remap01(t, 0.94f, 1f));
            DrawFlatSword(fx, shoulder, ar, lr, p.Scale, face < 0f, Color.white * fade);
        }

        /// <summary>
        /// The thrown shadow of the sword: out along its way, easing to a stop Shade.ThrowReach off; hanging there, spinning;
        /// then fading where it is (the user's: it strikes and is gone, nothing comes back), its blows only before the fade.
        /// It holds nothing back: the sword in hand swings on. Black motes off its edge; a burst of them as it goes.
        /// </summary>
        void StepShadeThrow(Proj p)
        {
            p.LocalAi0++;
            var me = CenterPx(p);
            var dir = new Vector2(Mathf.Cos(p.LocalAi1), Mathf.Sin(p.LocalAi1));
            if (p.Ai0 == 0f)
            {
                float k = Mathf.Clamp01(p.LocalAi0 / Shade.ThrowOut);
                var at = p.Point + dir * (Shade.ThrowReach * (1f - (1f - k) * (1f - k)));
                p.Vel = Flip(at - me);
                p.Pos = TerraParticles.ToWorld(at);
                if (k >= 1f)
                {
                    p.Ai0 = 1f;
                    p.LocalAi0 = 0f;
                }
            }
            else if (p.Ai0 == 1f)
            {
                p.Vel *= 0.5f;
                if (p.LocalAi0 >= Shade.ThrowHang)
                {
                    p.Ai0 = 2f;
                    p.LocalAi0 = 0f;
                }
            }
            else
            {
                p.Vel *= 0.5f;
                if (p.LocalAi0 >= Shade.ThrowFade)
                {
                    DevShadeFades++;
                    for (int k = 0; k < 14; k++)
                    {
                        float a = k / 14f * Mathf.PI * 2f;
                        ShadeDust(me, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1.5f, 3.5f), Random.Range(1f, 1.4f), k % 4 == 0 ? 240 : 109);
                    }
                    Sound("tk_shade_catch", 0.4f);
                    Kill(p);
                    return;
                }
            }
            p.TRot = Mathf.Repeat(p.TRot + 0.55f * p.Ai1 + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
            if (p.Ai0 < 2f && (int)p.LocalAi0 % 9 == 1) Sound("tk_shade_spin", 0.3f);
            if (Random.value < 0.7f)
            {
                float a = Random.value * Mathf.PI * 2f;
                var at = me + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(10f, Shade.SpinPx * 0.8f);
                ShadeDust(at, new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * p.Ai1 * Random.Range(1.5f, 3f), Random.Range(0.8f, 1.2f), Random.value < 0.25f ? 240 : 109);
            }
        }

        /// <summary>
        /// The spinning shadow of the sword: a thin dark ring as far as it strikes (a lilac breath on it), the black blur of
        /// its turning with a silver rim where its tips go, its afterimages turning behind it, the sword a little see-through
        /// with more lilac than the one in hand; all of it fading in its last moments.
        /// </summary>
        static void DrawShadeThrow(FxBatch fx, Proj p, Vector2 px)
        {
            var sword = FxBatch.Frame(Shade.Tex("edge_flat"), 0, 0, 61, 27, 31f, 14f);
            float s = p.Scale;
            var ring = FxBatch.Whole(Shade.Tex("ring"));
            var disc = FxBatch.Whole(Shade.Tex("disc"));
            float pulse = 1f + 0.04f * Mathf.Sin(p.Ticks * 0.5f), reach = Shade.SpinPx * s * pulse, big = s * Shade.SwordScale, half = 30f * big;
            float o = Remap01(p.Ticks, 0f, 2f) * (p.Ai0 == 2f ? 1f - Remap01(p.LocalAi0, 0f, Shade.ThrowFade) : 1f);
            if (o <= 0.01f) return;
            fx.Draw(ring, px, SoulWave.Glow(Shade.Lilac, 0.16f * o), 0f, reach / 31f * 1.03f);
            fx.Draw(ring, px, Shade.Dark(Shade.Plum, 0.6f * o), 0f, reach / 31f);
            fx.Draw(disc, px, Shade.Dark(Shade.Void, 0.28f * o), 0f, half / 15.6f);
            fx.Draw(ring, px, Shade.Dark(Shade.Silver, 0.5f * o), 0f, half / 31f);
            for (int j = 4; j >= 1; j--)
                fx.Draw(sword, px, Shade.Dark(Shade.Ink, 0.5f * (1f - j / 5f) * o), p.TRot - p.Ai1 * j * 0.32f, big * 1.05f);
            fx.Draw(sword, px, SoulWave.Glow(Shade.Lilac, 0.35f * o), p.TRot, big * 1.12f);
            fx.Draw(sword, px, Color.white * (0.85f * o), p.TRot, big, overPlayer: true);
            fx.Light(px, new Color(0.25f, 0.18f, 0.38f) * o, 3f);
        }

        // ------------------------------------------------------------------ the Shade Rift Rifle

        /// <summary>
        /// The rifle's ray, at once from its muzzle (the player's middle if a wall is between) along the aim to the first
        /// wall (platforms passed), through every enemy on the way, each marked once; its kick, motes at the muzzle.
        /// </summary>
        public void ShootShadeRay(HeroController hero, Vector2 mouse, int damage, float knockback, float crit)
        {
            if (hero == null) return;
            var centre = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var dir = TerraParticles.ToPx(mouse) - centre;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            float facing = dir.x < 0f ? -1f : 1f;
            var muzzle = Shade.RiflePoint(centre, dir, facing, Shade.Muzzle);
            if (Physics2D.Linecast(TerraParticles.ToWorld(centre), TerraParticles.ToWorld(muzzle), 1 << ChunkColliders.TerrainLayer)) muzzle = centre;
            var from = TerraParticles.ToWorld(muzzle);
            var wdir = Flip(dir).normalized;
            float wall = BeamWall(from, wdir, Shade.RayReachPx * Px);
            var end = from + wdir * wall;
            var ray = Spawn(ProjKind.ShadeRay, end, Vector2.zero, 0, 0f, 0f);
            if (ray == null) return;
            ray.Point = muzzle;
            ray.TRot = Mathf.Atan2(dir.y, dir.x);
            var blow = Spawn(ProjKind.ShadeRayHit, end, wdir, damage, knockback, crit);
            if (blow != null)
            {
                blow.Point = muzzle;
                blow.TRot = ray.TRot;
            }
            DevRays++;
            Shade.Kick(3f, 3.5f);
            Sound("tk_rift_ray_*", 0.6f);
            for (int k = 0; k < 4; k++) ShadeDust(muzzle, dir * Random.Range(1.5f, 4f) + Random.insideUnitCircle, Random.Range(0.8f, 1.1f));
            ShadeDust(muzzle, dir * 1.2f + Random.insideUnitCircle * 0.5f, 0.9f, 240);
            if (wall < Shade.RayReachPx * Px - 0.01f)
            {
                var endPx = TerraParticles.ToPx(end);
                for (int k = 0; k < 3; k++) ShadeDust(endPx, -dir * Random.Range(1f, 3f) + Random.insideUnitCircle * 2f, Random.Range(0.7f, 1f), k == 0 ? 240 : 109);
            }
        }

        /// <summary>
        /// The ray: a black core in a silver edge (one picture), a lilac breath round both, from the muzzle (Point) to its end, narrowing
        /// and fading; little black rifts flickering along it; a black star at the muzzle while it is young.
        /// </summary>
        static void DrawShadeRay(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Point == Vector2.zero) return;
            float k = Mathf.Clamp01(1f - p.Ticks / (float)Shade.RayTicks);
            var d = px - p.Point;
            float len = d.magnitude;
            if (len < 1f || k <= 0f) return;
            float rot = Mathf.Atan2(d.y, d.x);
            var block = ShadeBlock;
            float w = 0.4f + 0.6f * k, core = Mathf.Pow(k, 0.6f);
            fx.Draw(block, p.Point, SoulWave.Glow(Shade.Lilac, 0.22f * k), rot, new Vector2(len / 4f, 12f * w / 4f));
            // The black core in its silver edge, one picture (shade_raybar.png): two sprites in one place are drawn in no set order.
            fx.Draw(FxBatch.Frame(Shade.Tex("raybar"), 0, 0, 4, 7, 0f, 3.5f), p.Point, Shade.Dark(Color.white, core), rot, new Vector2(len / 4f, 7f * w / 7f));
            var dir = d / len;
            var normal = new Vector2(-dir.y, dir.x);
            var rift = FxBatch.Frame(Shade.Tex("px"), 0, 0, 4, 4, 2f, 2f);
            for (float s = 24f; s < len; s += 38f)
            {
                float jitter = Mathf.Sin(s * 0.37f + p.Ticks * 1.7f);
                var at = p.Point + dir * s + normal * (jitter * 5f);
                fx.Draw(rift, at, Shade.Dark(Shade.Void, 0.9f * core), rot + 0.785f + jitter * 0.4f, new Vector2(1.3f, 1.3f) * w);
            }
            for (float s = 0f; s <= len; s += 128f) fx.Light(p.Point + d * (s / len), new Color(0.2f, 0.14f, 0.3f) * k, 1.6f);
            if (p.Ticks < 5)
            {
                float m = 1f - p.Ticks / 5f;
                var orb = FxBatch.Whole(SoulWave.Tex("orb"));
                fx.Draw(orb, p.Point, SoulWave.Glow(Shade.Lilac, 0.45f * m), 0f, 0.9f + 0.6f * m, overPlayer: true);
                fx.Draw(orb, p.Point, Shade.Dark(Shade.Void, 0.95f * m), 0f, 0.45f + 0.4f * m, overPlayer: true);
                var star = FxBatch.Whole(SoulWave.Tex("flash"));
                fx.Draw(star, p.Point, Shade.Dark(Shade.Silver, 0.8f * m), p.TRot, 0.35f + 0.3f * m, overPlayer: true);
            }
        }

        /// <summary>A mark on what the ray struck: the enemy's own (one a mark, up to Shade.MaxMarks), its time started again; a pulse.</summary>
        void MarkShade(Proj ray, HealthManager hm)
        {
            DevMarkCalls++;
            if (hm == null || hm.isDead)
            {
                DevMarkSkipped++;
                return;
            }
            Proj mark = null;
            foreach (var q in live)
                if (!q.Dead && q.Kind == ProjKind.ShadeMark && q.Target == hm)
                {
                    mark = q;
                    break;
                }
            if (mark == null)
            {
                mark = Spawn(ProjKind.ShadeMark, Centre(hm), Vector2.zero, 0, 0f, 0f);
                if (mark == null) return;
                mark.Target = hm;
                mark.Ai0 = 0f;
                mark.LocalAi1 = Random.value * Mathf.PI * 2f;
            }
            mark.Ai0 = Mathf.Min(Shade.MaxMarks, mark.Ai0 + 1f);
            mark.TimeLeft = Shade.MarkTicks;
            mark.LocalAi0 = 12f;
            DevMarks++;
            var at = TerraParticles.ToPx(Centre(hm));
            for (int k = 0; k < 4; k++) ShadeDust(at + Random.insideUnitCircle * 5f, Random.insideUnitCircle * 2.5f, Random.Range(0.8f, 1.2f), k == 0 ? 240 : 109);
            Sound("tk_shade_hit_*", 0.3f);
        }

        /// <summary>A mark keeps to its enemy (gone with it); its orbs turn, its pulse fades.</summary>
        void StepShadeMark(Proj p)
        {
            var hm = p.Target;
            if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy)
            {
                Kill(p);
                return;
            }
            p.Pos = Centre(hm);
            p.LocalAi1 += 0.05f + 0.01f * p.Ai0;
            if (p.LocalAi0 > 0f) p.LocalAi0--;
        }

        /// <summary>The number of marks on an enemy (dev: the tests count them).</summary>
        public int DevMarksOn(HealthManager hm)
        {
            foreach (var q in live)
                if (!q.Dead && q.Kind == ProjKind.ShadeMark && q.Target == hm) return (int)q.Ai0;
            return 0;
        }

        /// <summary>
        /// A mark: five white orbs in a row over the enemy's head (its picture's top), as many black from the left as its stacks (the user's: the
        /// five turning black one by one; in a row, none hidden behind the enemy as a ring round it was), the black ones in
        /// silver rims with a white glint, the white ones in dark rims; the newest bigger for a moment; a lilac breath on all
        /// five at five; bobbing a little; fading in its last second.
        /// </summary>
        static void DrawShadeMark(FxBatch fx, Proj p, Vector2 px)
        {
            int n = Mathf.Clamp((int)p.Ai0, 0, Shade.MaxMarks);
            if (n <= 0) return;
            // Over the top of what is drawn (an enemy's picture often stands well above its box), else of its box.
            var col = p.Target != null ? p.Target.GetComponent<Collider2D>() : null;
            var art = p.Target != null ? p.Target.GetComponent<Renderer>() : null;
            float top = col != null ? col.bounds.max.y : float.NegativeInfinity;
            if (art != null && art.enabled && art.bounds.size.y < 12f) top = Mathf.Max(top, art.bounds.max.y);
            var head = !float.IsInfinity(top) ? TerraParticles.ToPx(new Vector2(col != null ? col.bounds.center.x : p.Pos.x, top)) + new Vector2(0f, -10f) : px + new Vector2(0f, -34f);
            head.y += Mathf.Sin(p.Ticks * 0.1f) * 1.2f;
            float o = Remap01(p.TimeLeft, 0f, 60f);
            float pop = 1f + 0.6f * (p.LocalAi0 / 12f);
            var disc = FxBatch.Whole(Shade.Tex("disc"));
            // Each orb one picture (shade_orb_black/white.png, 11 px): two sprites in one place are drawn in no set order, so a
            // black body laid over a silver disc could come out under it, the orb looking white (the user's report).
            var blackOrb = FxBatch.Whole(Shade.Tex("orb_black"));
            var whiteOrb = FxBatch.Whole(Shade.Tex("orb_white"));
            const float step = 12f;
            for (int i = 0; i < Shade.MaxMarks; i++)
            {
                var at = head + new Vector2((i - (Shade.MaxMarks - 1) * 0.5f) * step, 0f);
                bool black = i < n;
                float size = black && i == n - 1 ? pop : 1f;
                if (n >= Shade.MaxMarks) fx.Draw(disc, at, SoulWave.Glow(Shade.Lilac, 0.35f * o), 0f, 7.5f * size / 15.6f, overPlayer: true);
                fx.Draw(black ? blackOrb : whiteOrb, at, Shade.Dark(Color.white, o), 0f, size, overPlayer: true);
            }
        }

        /// <summary>
        /// The rifle's right button: every mark bursts on its enemy, its reach and damage by its stacks (Shade.BlastPx,
        /// BlastShare of the rifle's blow with the round's), on all within it; the marks are spent.
        /// </summary>
        public void DetonateShadeMarks(int damage, float knockback, float crit)
        {
            var marks = new System.Collections.Generic.List<Proj>();
            foreach (var q in live)
                if (!q.Dead && q.Kind == ProjKind.ShadeMark) marks.Add(q);
            if (marks.Count == 0)
            {
                Sound("tk_pulse_cancel", 0.4f);
                return;
            }
            int most = 0;
            foreach (var m in marks)
            {
                int stacks = Mathf.Clamp((int)m.Ai0, 1, Shade.MaxMarks);
                most = Mathf.Max(most, stacks);
                var at = m.Target != null ? Centre(m.Target) : m.Pos;
                var b = Spawn(ProjKind.ShadeBlast, at, Vector2.zero, Mathf.Max(1, Mathf.RoundToInt(damage * Shade.BlastShare(stacks))), knockback * (0.6f + 0.25f * stacks), crit);
                Kill(m);
                if (b == null) continue;
                b.Ai0 = Shade.BlastPx(stacks);
                b.Ai1 = stacks;
                DevShadeBlasts++;
                var c = TerraParticles.ToPx(at);
                int count = 8 + 4 * stacks;
                for (int k = 0; k < count; k++)
                {
                    float a = k / (float)count * Mathf.PI * 2f + Random.value * 0.2f;
                    var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(2f, 3.2f + 0.5f * stacks);
                    ShadeDust(c, v, Random.Range(1f, 1.5f), k % 4 == 0 ? 240 : 109);
                }
            }
            Sound("tk_rift_burst", 0.6f + 0.06f * most);
            Player.TerraCamera.Punch(0.04f + 0.02f * most, 0.16f);
        }

        /// <summary>A burst: a black core swelling and gone, a silver rim opening to its reach, a lilac breath outside it.</summary>
        static void DrawShadeBlast(FxBatch fx, Proj p, Vector2 px)
        {
            float k = Mathf.Clamp01(p.Ticks / 24f), o = 1f - k;
            float r = Mathf.Max(24f, p.Ai0);
            var ring = FxBatch.Whole(Shade.Tex("ring"));
            var disc = FxBatch.Whole(Shade.Tex("disc"));
            float open = 0.35f + 0.65f * Mathf.Sqrt(k);
            // The core under the rim and the flash (its own sorting order: two sprites in one place are drawn in no set order).
            fx.Draw(disc, px, Shade.Dark(Shade.Void, 0.88f * Mathf.Pow(o, 1.5f)), 0f, r * open * 0.94f / 15.6f);
            fx.Draw(ring, px, SoulWave.Glow(Shade.Lilac, 0.4f * o), 0f, r * open / 31f * 1.05f, overPlayer: true);
            fx.Draw(ring, px, Shade.Dark(Shade.Silver, 0.9f * o), 0f, r * open / 31f, overPlayer: true);
            fx.Draw(disc, px, Shade.Dark(Shade.Eye, 0.85f * Mathf.Max(0f, 1f - k * 4f)), 0f, (3f + p.Ai1) / 15.6f, overPlayer: true);
            fx.Light(px, new Color(0.3f, 0.2f, 0.45f) * o, r / 12f);
        }

        // ------------------------------------------------------------------ the Shade Wraith

        /// <summary>The ghost sent toward the cursor from the player (one at a time: the staff is held while it is out).</summary>
        public void SendShadeGhost(HeroController hero, Vector2 mouse, int damage, float knockback, float crit)
        {
            if (hero == null || Holds(ProjKind.ShadeGhost)) return;
            var centre = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var dir = TerraParticles.ToPx(mouse) - centre;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            var p = Spawn(ProjKind.ShadeGhost, TerraParticles.ToWorld(centre + dir * 8f), Flip(dir) * (Shade.GhostSpeed * 0.6f), damage, knockback, crit);
            if (p == null) return;
            p.Ai0 = 0f;
            p.TRot = Mathf.Atan2(dir.y, dir.x);
            DevShadeGhosts++;
            for (int k = 0; k < 8; k++) ShadeDust(centre + dir * 10f, dir * Random.Range(1f, 3f) + Random.insideUnitCircle * 1.5f, Random.Range(0.9f, 1.3f));
        }

        /// <summary>
        /// The ghost: while the button is held (the staff in hand, the mana paid every Shade.ManaEvery ticks), it rushes for
        /// the cursor and keeps there, the player holding the staff out to it, a shock closing on it every Shade.ShockEvery
        /// ticks; walls stop it. Let go (or out of mana), it comes back slowly, through walls, quickening a little, and is
        /// caught at the hand: only then can it be sent again.
        /// </summary>
        void StepShadeGhost(Proj p)
        {
            var mod = TerraMod.Instance;
            if (mod == null || !Holder(out var owner))
            {
                Kill(p);
                return;
            }
            p.TimeLeft = 600;
            p.LocalAi0++;
            if (++p.FrameCounter >= 5)
            {
                p.FrameCounter = 0;
                p.Frame = (p.Frame + 1) % 8;
            }
            var me = CenterPx(p);
            var def = ItemDb.Get(ItemType.ShadeSoulFocus);
            if (p.Bounces > 0)
            {
                StepBlink(p, owner, me, def);
                return;
            }
            if (p.Ai0 == 0f)
            {
                var held = mod.Inventory.Current;
                bool still = mod.UseHeld && !held.IsEmpty && held.Type == def.Type;
                if (still && ++p.LocalAi1 >= Shade.ManaEvery)
                {
                    p.LocalAi1 = 0f;
                    if (mod.PayMana(def)) DevGhostPaid++;
                    else
                    {
                        still = false;
                        DevGhostNoMana++;
                    }
                }
                if (!still)
                {
                    p.Ai0 = 1f;
                    p.LocalAi1 = 0f;
                    p.Collide = false;
                    DevGhostReturns++;
                    Sound("tk_shade_return", 0.45f);
                }
                else
                {
                    var goal = TerraParticles.ToPx(mod.DevAim ?? mod.AimPoint);
                    var to = goal - me;
                    float dist = to.magnitude;
                    float speed = Mathf.Min(Shade.GhostSpeed, dist * 0.25f + 0.5f);
                    var want = dist > 1f ? to / dist * speed : Vector2.zero;
                    p.Vel = Vector2.Lerp(p.Vel, Flip(want), dist > 60f ? 0.22f : 0.35f);
                    if (dist < 14f) p.Vel += new Vector2(Mathf.Sin(p.LocalAi0 * 0.15f) * 0.18f, Mathf.Cos(p.LocalAi0 * 0.11f) * 0.14f);
                    if ((int)p.LocalAi0 % Shade.ShockEvery == 0) ShadeShock(p);
                    mod.KeepUsing(def, Flip(me - owner));
                }
            }
            if (p.Ai0 == 1f)
            {
                p.LocalAi1++;
                p.Collide = false;
                var to = owner - me;
                float dist = to.magnitude;
                float speed = Mathf.Min(Shade.GhostBack, 1.5f + p.LocalAi1 * 0.1f);
                if (dist < Mathf.Max(22f, speed * 1.5f))
                {
                    GhostCaught(p);
                    return;
                }
                p.Vel = Vector2.Lerp(p.Vel, Flip(to / dist * speed), 0.2f);
            }
            if (p.Vel.sqrMagnitude > 0.25f) p.TRot = Mathf.Atan2(-p.Vel.y, p.Vel.x);
            if (Random.value < 0.4f) ShadeDust(me - TV(p).normalized * 10f + Random.insideUnitCircle * 4f, -TV(p) * 0.1f + Random.insideUnitCircle * 0.3f, Random.Range(0.7f, 1f));
        }

        void GhostCaught(Proj p)
        {
            DevGhostCaught++;
            var at = CenterPx(p);
            for (int k = 0; k < 8; k++) ShadeDust(at, Random.insideUnitCircle * 2.5f, Random.Range(0.8f, 1.2f));
            Sound("tk_shade_catch", 0.45f);
            Kill(p);
        }

        /// <summary>A shock from a ring round the ghost (Shade.ShockRingPx out, a way at random), closing on it over Shade.ShockTicks.</summary>
        void ShadeShock(Proj g)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Shade.ShockRingPx + Random.Range(0f, 24f);
            var c = CenterPx(g);
            var from = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            var v = (c - from) / Shade.ShockTicks;
            var s = Spawn(ProjKind.ShadeShock, TerraParticles.ToWorld(from), Flip(v), Mathf.Max(1, Mathf.RoundToInt(g.Damage * Shade.ShockShare)), g.Knockback * 0.4f, g.Crit);
            if (s == null) return;
            s.Parent = g;
            s.Class = g.Class;
            s.TimeLeft = Shade.ShockTicks + 8;
            s.TRot = Mathf.Atan2(v.y, v.x);
            DevShocks++;
            if (DevShocks % 3 == 0) Sound("tk_shade_shock", 0.3f);
        }

        /// <summary>A shock: after its ghost wherever it goes, through walls, gone when it gets there; straight on if the ghost is gone.</summary>
        void StepShadeShock(Proj p)
        {
            var g = p.Parent;
            if (g != null && !g.Dead && g.Kind == ProjKind.ShadeGhost)
            {
                var to = CenterPx(g) - CenterPx(p);
                float speed = Mathf.Max(p.Vel.magnitude, 8f);
                if (to.magnitude <= speed + 4f)
                {
                    Kill(p);
                    return;
                }
                p.Vel = Vector2.Lerp(p.Vel, Flip(to.normalized * speed), 0.35f);
            }
            p.TRot = Mathf.Atan2(-p.Vel.y, p.Vel.x);
        }

        static void ShockFades(Proj p)
        {
            var at = CenterPx(p);
            for (int k = 0; k < 2; k++) ShadeDust(at, Random.insideUnitCircle * 1.5f, Random.Range(0.7f, 1f), k == 0 && Random.value < 0.5f ? 240 : 109);
        }

        /// <summary>
        /// The right button with the staff and its ghost out: the blink begins (one at a time), the ghost held where it is
        /// for Shade.BlinkTicks (the user's: a short delay, with its sound), then the player goes to it and all the mana is
        /// spent (StepBlink), Shade.BlinkCooldown before the next. False with no ghost out, one already blinking, or the
        /// last blink too near (its seconds left shown over the player).
        /// </summary>
        public bool StartShadeBlink()
        {
            foreach (var q in live)
                if (!q.Dead && q.Kind == ProjKind.ShadeGhost)
                {
                    if (q.Bounces > 0) return false;
                    if (Time.time < Shade.BlinkReady)
                    {
                        DevShadeBlinkCooling++;
                        var hero = HeroController.SilentInstance;
                        if (hero != null) UI.CombatText.Show(hero.transform.position + Vector3.up * 1.2f, Lang.T("传送冷却") + " " + Mathf.CeilToInt(Shade.BlinkReady - Time.time), new Color(0.66f, 0.53f, 0.85f));
                        Sound("tk_pulse_cancel", 0.35f);
                        return false;
                    }
                    q.Bounces = Shade.BlinkTicks;
                    q.Vel = Vector2.zero;
                    Sound("tk_shade_blink_charge", 0.6f);
                    return true;
                }
            return false;
        }

        public static int DevShadeBlinked, DevShadeBlinkFailed, DevShadeBlinkCooling;

        /// <summary>
        /// The blink's wait (the ghost's Bounces the ticks left): the ghost still, the staff held out to it, shadow drawn in
        /// to the player and the ghost both, more as it ends; then the player to the ghost (a free place by it for the
        /// player's box), the mana all spent, the ghost caught. No room for the player there: nothing spent, the ghost
        /// carries on coming back.
        /// </summary>
        void StepBlink(Proj p, Vector2 owner, Vector2 me, ItemDef def)
        {
            var mod = TerraMod.Instance;
            var hero = HeroController.SilentInstance;
            p.Bounces--;
            p.Vel = Vector2.zero;
            float k = 1f - p.Bounces / (float)Shade.BlinkTicks;
            if (p.Ai0 == 0f) mod.KeepUsing(def, Flip(me - owner));
            foreach (var at in new[] { owner, me })
                if (Random.value < 0.4f + 0.5f * k)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    var from = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(18f, 30f);
                    ShadeDust(from, (at - from) * 0.16f, Random.Range(0.9f, 1.3f), Random.value < 0.25f ? 240 : 109);
                }
            if (p.Bounces > 0 || hero == null) return;
            Vector2 before = hero.GetComponent<BoxCollider2D>().bounds.center;
            if (!Player.TeleportRods.ToPoint(hero, p.Pos))
            {
                DevShadeBlinkFailed++;
                Sound("tk_pulse_cancel", 0.5f);
                p.Ai0 = 1f;
                p.Collide = false;
                return;
            }
            foreach (var at in new[] { TerraParticles.ToPx(before), me })
                for (int j = 0; j < 14; j++)
                {
                    float a = j / 14f * Mathf.PI * 2f;
                    ShadeDust(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1.5f, 3f), Random.Range(1f, 1.4f), j % 4 == 0 ? 240 : 109);
                }
            mod.Stats.Mana = 0;
            Sound("tk_shade_blink", 0.7f);
            DevShadeBlinked++;
            Shade.BlinkReady = Time.time + Shade.BlinkCooldown;
            DevGhostCaught++;
            Kill(p);
        }

        /// <summary>
        /// The ghost: the pack's frame (48×32, facing along its flight, kept upright going left), its afterimages, a lilac
        /// breath (no ring round it: the user's).
        /// </summary>
        static void DrawShadeGhost(FxBatch fx, Proj p, Vector2 px)
        {
            var frame = FxBatch.Frame(Shade.Tex("flight"), 0, (p.Frame & 7) * 32, 48, 32, 24f, 16f);
            float rot = p.TRot;
            bool left = Mathf.Cos(rot) < 0f;
            float o = Remap01(p.Ticks, 0f, 4f);
            int n = p.Trail.Count;
            for (int k = n - 1; k >= 1; k--)
            {
                float f = 1f - k / (float)(n + 1);
                fx.Draw(frame, TerraParticles.ToPx(p.Trail[k]), Shade.Dark(Shade.Ink, 0.35f * f * o), rot, 0.85f * (0.8f + 0.2f * f), flipY: left);
            }
            fx.Draw(frame, px, SoulWave.Glow(Shade.Lilac, 0.22f * o), rot, 0.95f, flipY: left);
            fx.Draw(frame, px, Color.white * o, rot, 0.85f, flipY: left);
            fx.Light(px, new Color(0.22f, 0.16f, 0.34f) * o, 2.5f);
        }

        /// <summary>A shock: the charged wave's picture, small, along its flight, a lilac breath round it, its afterimages; fading in.</summary>
        static void DrawShadeShock(FxBatch fx, Proj p, Vector2 px)
        {
            var wave = FxBatch.Frame(Shade.Tex("charged"), 0, 0, 80, 48, 40f, 24f);
            float rot = p.TRot;
            bool left = Mathf.Cos(rot) < 0f;
            float o = Remap01(p.Ticks, 0f, 3f);
            int n = p.Trail.Count;
            for (int k = n - 1; k >= 1; k--)
            {
                float f = 1f - k / (float)(n + 1);
                fx.Draw(wave, TerraParticles.ToPx(p.Trail[k]), Shade.Dark(Shade.Ink, 0.3f * f * o), rot, 0.3f * (0.8f + 0.2f * f), flipY: left);
            }
            fx.Draw(wave, px, SoulWave.Glow(Shade.Lilac, 0.18f * o), rot, 0.36f, flipY: left);
            fx.Draw(wave, px, Color.white * o, rot, 0.32f, flipY: left);
        }

        // ------------------------------------------------------------------ the little shade's wave

        /// <summary>The little shade's wave (Items/MinionsShade.cs), drawn where the minion is: the charged wave along its flight, its afterimages.</summary>
        public static void DrawShadeWave(FxBatch fx, Vector2 px, Vector2 velPx, Vector2[] trail, float o)
        {
            var wave = FxBatch.Frame(Shade.Tex("charged"), 0, 0, 80, 48, 40f, 24f);
            float rot = Mathf.Atan2(velPx.y, velPx.x);
            bool left = velPx.x < 0f;
            if (trail != null)
                for (int k = trail.Length - 1; k >= 1; k--)
                {
                    if ((trail[k] - px).sqrMagnitude > 160f * 160f) continue;
                    float f = 1f - k / (float)(trail.Length + 1);
                    fx.Draw(wave, trail[k], Shade.Dark(Shade.Ink, 0.35f * f * o), rot, 0.55f * (0.75f + 0.25f * f), flipY: left);
                }
            fx.Draw(wave, px, SoulWave.Glow(Shade.Lilac, 0.25f * o), rot, 0.66f, flipY: left);
            fx.Draw(wave, px, Color.white * o, rot, 0.6f, flipY: left);
            fx.Light(px, new Color(0.25f, 0.18f, 0.4f) * o, 3f);
        }
    }
}
