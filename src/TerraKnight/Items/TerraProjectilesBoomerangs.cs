using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The hardmode boomerangs (AI_003, Projectile.cs:39573), in Terraria's space where it says so:
    /// the Light Disc (106: up to six out, back after 45 updates, off walls with sparks, a blue
    /// streak behind and a glow over it), the Bananarang (272: up to ten, back fast), the Possessed
    /// Hatchet (182: thrown at will, it hunts enemies in sight within 800 px for its first 360
    /// updates, ten hits) and Paladin's Hammer (301: wound up behind the player for 30 updates,
    /// then out for 30, through everything it hits, back with a trail of ghost hammers).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterBoomerangs()
        {
            specs[ProjKind.LightDisc] = new Spec
            {
                Ai = Ai.Boomerang, Texture = "Projectile_106", W = 32, H = 32, Time = 3600, Penetrate = -1, MaxOut = 6, OutTicks = 45, SpinRate = 0.3f,
                ReturnSpeed = 16f, ReturnAccel = 1.2f, WallBounce = true, TrailLength = 15, Light = new Color(0f, 0.2f, 0.4f),
                Draw = (t, fx, p, px) => DrawLightDisc(fx, p, px),
            };
            specs[ProjKind.Bananarang] = new Spec
            {
                Ai = Ai.Boomerang, Texture = "Projectile_272", W = 32, H = 32, Scale = 0.9f, Time = 3600, Penetrate = -1, MaxOut = 10,
                ReturnSpeed = 20f, ReturnAccel = 1.5f, Draw = (t, fx, p, px) => DrawBoomerangLit(fx, p, px, 22, 34, 11f),
            };
            specs[ProjKind.PossessedHatchet] = new Spec
            {
                Ai = Ai.Boomerang, Texture = "Projectile_182", W = 30, H = 30, Time = 3600, Penetrate = 10, Extra = 1, MaxOut = int.MaxValue,
                ReturnSpeed = 16f, ReturnAccel = 1.2f, Light = new Color(0.15f, 0.15f, 0.15f), Draw = (t, fx, p, px) => DrawHatchet(fx, p, px),
            };
            specs[ProjKind.PaladinsHammer] = new Spec
            {
                Ai = Ai.Boomerang, Texture = "Projectile_301", W = 38, H = 38, Time = 3600, Penetrate = 1000, Extra = 2, LocalCooldown = 10,
                ReturnSpeed = 15f, ReturnAccel = 3f, Draw = (t, fx, p, px) => DrawPaladinsHammer(fx, p, px),
            };
        }

        /// <summary>
        /// The hardmode boomerangs' own flight out (ai[1] already counted): true when it decides
        /// for itself when to turn back.
        /// </summary>
        bool BoomerangOut(Proj p, HeroController hero)
        {
            switch (p.Kind)
            {
                case ProjKind.PossessedHatchet:
                    StepHatchet(p);
                    return true;
                case ProjKind.PaladinsHammer:
                    StepPaladinsHammer(p, hero);
                    return true;
                // The Machete and the Anchor have no 30-update turn (P:39818: their branch replaces it): home off a block, an enemy, or 800 px.
                case ProjKind.BloodyMacheteProj:
                    MacheteDrop(p);
                    return true;
                case ProjKind.AnchorProj:
                    StepAnchor(p);
                    return true;
                case ProjKind.BouncingShieldProj:
                    StepBouncingShield(p);
                    return true;
            }
            return false;
        }

        /// <summary>
        /// AI_003 for 182 (P:39839): it heads at 12 px an update for the nearest enemy it can see
        /// within 800 px (from its 10th update to its 360th), turning 0.25 a step (three times as
        /// hard across zero); with none it flies on and turns back after 30. Gold sparks.
        /// </summary>
        void StepHatchet(Proj p)
        {
            var c = CenterPx(p);
            if (Random.Range(0, 2) == 0)
            {
                int i = BoxDust(c - new Vector2(15f, 15f), 30, 30, 57, 0f, 0f, 255, 0.75f);
                if (i >= 0)
                {
                    D(i).Velocity *= 0.1f;
                    D(i).NoGravity = true;
                }
            }
            var v = TV(p);
            if (v.x != 0f) p.Dir = Mathf.Sign(v.x);
            Vector2 aim = default;
            bool found = false;
            if (p.Ai1 > 10f && p.Ai1 < 360f)
            {
                float best = 800f;
                foreach (var hm in EnemiesNear(c, 800f))
                {
                    var e = EnemyCenterPx(hm);
                    float d = Vector2.Distance(c, e);
                    if (d < best && InSight(c, e))
                    {
                        best = d;
                        aim = e;
                        found = true;
                    }
                }
            }
            if (!found)
            {
                aim = c + v * 100f;
                if (p.Ai1 >= 30f)
                {
                    p.Returning = true;
                    p.Ai1 = 0f;
                }
            }
            var want = aim - c;
            want = want.sqrMagnitude > 1e-6f ? want.normalized * 12f : Vector2.zero;
            v.x = Approach(v.x, want.x, 0.25f, 2f);
            v.y = Approach(v.y, want.y, 0.25f, 2f);
            p.Vel = Flip(v);
        }

        /// <summary>
        /// AI_003 for 301 (P:40003, 40303): drawn 1.75 times as big; for its first 30 updates it
        /// is held at the player's centre, turning a sixtieth of a turn each (a swing sound at 20),
        /// then flies along its throw pointing ahead, with gold dust off its flanks, and after 30
        /// more stops dead and comes back.
        /// </summary>
        void StepPaladinsHammer(Proj p, HeroController hero)
        {
            p.Scale = 1.75f;
            var v = TV(p);
            if (hero != null)
            {
                if (p.Ai1 == 1f) p.TRot = Mathf.Atan2(v.y, v.x) - Mathf.PI / 4f - Mathf.PI / 2f;
                if (p.Ai1 == 20f) Sound("Item_1");
                if (p.Ai1 < 30f)
                {
                    p.TRot += p.Dir * (Mathf.PI / 30f);
                    p.Pos = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center - p.Vel * Px;
                }
                else p.TRot = Mathf.Atan2(v.y, v.x) + Mathf.PI / 4f;
                if (p.Ai1 >= 60f)
                {
                    p.Returning = true;
                    p.Ai1 = 0f;
                    p.Vel = Vector2.zero;
                    return;
                }
            }
            if (p.Ai1 < 30f) return;
            // Ten tries an update: gold motes (1 in 2) and faint sparks (1 in 3) along both flanks.
            var c = CenterPx(p);
            var side = (v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.right);
            side = new Vector2(-side.y, side.x);
            float reach = 16f * p.Scale;
            for (int k = 0; k < 10; k++)
            {
                if (Random.Range(0, 2) == 0)
                {
                    int i = BoxDust(c - new Vector2(19f, 19f), 38, 38, 57, v.x * 0.2f, v.y * 0.2f, 200, 1.2f);
                    if (i >= 0)
                    {
                        D(i).Velocity += v * 1.76f * Random.value;
                        D(i).Velocity *= 0.2f;
                        D(i).NoGravity = true;
                        float r = Random.Range(0, 3) == 0 ? reach * Random.value : reach;
                        D(i).Position = c + side * r * (Random.Range(0, 2) * 2 - 1);
                    }
                }
                if (Random.Range(0, 3) == 0)
                {
                    int i = BoxDust(c - new Vector2(19f, 19f), 38, 38, 43, 0f, 0f, 254, 0.3f);
                    if (i >= 0)
                    {
                        D(i).Velocity += v * 1.76f * Random.value;
                        D(i).Velocity *= 0.5f;
                        D(i).NoGravity = true;
                        float r = Random.Range(0, 3) == 0 ? reach * Random.value : reach;
                        D(i).Position = c + side * r * (Random.Range(0, 2) * 2 - 1);
                    }
                }
            }
        }

        /// <summary>LightDisc_Bounce (P:20434): four lime-to-cyan sparks off what it struck, each with a smaller white twin.</summary>
        void LightDiscBounce(Proj p, Vector2 at, Vector2 normal)
        {
            var v = TV(p);
            var r = v - 2f * Vector2.Dot(v, normal) * normal;
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            for (int k = 0; k < 4; k++)
            {
                int i = PointDust(at, 306, RotateBy(r, Mathf.PI / 4f * Random.Range(-1f, 1f)) * 0.6f * Random.value, 200, 1.6f);
                if (i < 0) continue;
                D(i).Color = Color.Lerp(FxBatch.C(219, 253, 0), Color.cyan, Random.value);
                int j = pp.Clone(i);
                if (j < 0) continue;
                D(j).Color = Color.white;
                D(j).Scale = 1f;
                D(j).Alpha = 50;
            }
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>A lit boomerang at Main.DrawProj's generic pivot (half the sprite across, half the hitbox down).</summary>
        static void DrawBoomerangLit(FxBatch fx, Proj p, Vector2 px, int tw, int th, float oy)
        {
            int w = (int)(p.Spec.W * p.Spec.Scale);
            float ox = tw * 0.5f;
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, 0, tw, th, ox, oy), px + new Vector2((tw - w) * 0.5f, 0f), new Color(L, L, L, 1f), p.TRot, p.Scale);
        }

        /// <summary>
        /// LightDiscDrawer: a soft blue streak 16 px wide along its last 15 places, fading fast
        /// (the "LightDisc" shader on Extra_195, approximated with soft strokes); the disc, lit; and
        /// Light_Disc over it, added.
        /// </summary>
        static void DrawLightDisc(FxBatch fx, Proj p, Vector2 px)
        {
            var dot = FxBatch.Whole("Extra_98");
            for (int i = 1; i < p.Trail.Count; i++)
            {
                float t = (i - 0.5f) / p.Spec.TrailLength, k = 1f - t;
                var col = FxBatch.C(48, 63, 150) * (k * k * k * k) * 0.5f;
                col.a = 0f;
                var a = TerraParticles.ToPx(p.Trail[i - 1]);
                var b = TerraParticles.ToPx(p.Trail[i]);
                var seg = b - a;
                if (seg.sqrMagnitude < 0.01f) continue;
                fx.Draw(dot, (a + b) / 2f, col * 2f, Mathf.Atan2(seg.y, seg.x) + Mathf.PI / 2f, new Vector2(16f / 72f * 3f, Mathf.Max(seg.magnitude, 1f) / 72f * 1.6f));
            }
            fx.Draw(FxBatch.Frame("Projectile_106", 0, 0, 32, 32, 16f, 16f), px, new Color(L, L, L, 1f), p.TRot, p.Scale);
            fx.Draw(FxBatch.Frame("Light_Disc", 0, 0, 32, 32, 16f, 16f), px, FxBatch.C(200, 200, 200, 0), p.TRot, p.Scale);
        }

        /// <summary>182: GetAlpha (255, 255, 255, 200), facing its flight.</summary>
        static void DrawHatchet(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Draw(FxBatch.Frame("Projectile_182", 0, 0, 34, 30, 17f, 15f), px + new Vector2(2f, 0f), FxBatch.C(255, 255, 255, 200), p.TRot, p.Scale, flipX: p.Dir < 0f);
        }

        /// <summary>
        /// Main.DrawProj for 301 (M:35238): drawn 16 px (times its scale) ahead along its edge; on
        /// the way back five fading ghosts trail it, each turned a tenth of a turn further, with
        /// faint mirror images; a dim reversed copy behind it; while it is wound up, a white glow.
        /// GetAlpha: near white and partly added on the way out, lit on the way back.
        /// </summary>
        static void DrawPaladinsHammer(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Frame("Projectile_301", 0, 0, 38, 38, 19f, 19f);
            var alpha = p.Returning ? new Color(L, L, L, 1f) : FxBatch.C(250, 250, 250, 50);
            var ahead = new Vector2(Mathf.Cos(p.TRot - Mathf.PI / 4f), Mathf.Sin(p.TRot - Mathf.PI / 4f)) * 16f * p.Scale;
            var v = TV(p);
            if (p.Returning)
            {
                for (int k = 9; k >= 0; k -= 2)
                {
                    var c = alpha * ((9 - k) / 9f) * 0.5f;
                    float r = p.TRot + p.Dir * (-Mathf.PI / 10f) * (k + 1);
                    var off = new Vector2(Mathf.Cos(r - Mathf.PI / 4f), Mathf.Sin(r - Mathf.PI / 4f)) * 4f * p.Scale;
                    fx.Draw(spr, px + off - v * k * 0.45f, c * 0.5f, r, p.Scale);
                    fx.Draw(spr, px - off, c * 0.05f, r + Mathf.PI, p.Scale * 0.8f);
                }
            }
            fx.Draw(spr, px - ahead, alpha * 0.1f, p.TRot + Mathf.PI, p.Scale * 0.8f);
            fx.Draw(spr, px + ahead, alpha, p.TRot, p.Scale);
            if (!p.Returning && p.Ai1 < 30f) fx.Draw(spr, px + ahead, FxBatch.C(255, 255, 255, 64), p.TRot, p.Scale);
        }
    }
}
