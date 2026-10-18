using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The hardmode spears (AI_019_Spears, each with its own reach, Projectile.cs:46703) and what
    /// some leave behind, in Terraria's space (px y down, per update): the Mushroom Spear's glowing
    /// mushrooms along the thrust (131, aiStyle 30), the Chlorophyte Partisan's spore cloud as it
    /// draws back (228, aiStyle 44), the North Pole's ice spear (343, aiStyle 57) dropping snowflakes
    /// (344, AI_001) that drift apart as they fall; Gungnir's gold streaks and dust.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterSpears()
        {
            // SetDefaults (P:1444-4326): the hitbox, the scale; AI_019_Spears_GetSpearOffsetRelativeToPlayer's terms.
            void S(ProjKind k, int tex, int w, int h, float scale, float n6, float n7, float n8) =>
                specs[k] = new Spec
                {
                    Ai = Ai.Spear, Texture = "Projectile_" + tex, W = w, H = h, Scale = scale, Time = 3600, Penetrate = -1, TileCollide = false,
                    OwnerHitCheck = true, SpearOffset = new Vector3(n6, n7, n8),
                };
            S(ProjKind.CobaltNaginata, 97, 18, 18, 1.1f, 3f, 2.1f, 1.9f);
            S(ProjKind.PalladiumPike, 212, 18, 18, 1.12f, 3f, 2.1f, 1.9f);
            S(ProjKind.MythrilHalberd, 64, 18, 18, 1.25f, 3f, 1.9f, 1.7f);
            S(ProjKind.OrichalcumHalberd, 215, 18, 18, 1.27f, 3f, 1.9f, 1.7f);
            S(ProjKind.AdamantiteGlaive, 66, 18, 18, 1.27f, 3f, 2.1f, 1.9f);
            S(ProjKind.TitaniumTrident, 218, 18, 18, 1.28f, 3f, 2.1f, 1.9f);
            S(ProjKind.Gungnir, 105, 18, 18, 1.3f, 3f, 2.4f, 2.1f);
            S(ProjKind.MushroomSpear, 130, 22, 22, 1.2f, 3f, 1.3f, 1f);
            S(ProjKind.ChlorophytePartisan, 222, 18, 18, 1.3f, 3f, 2.4f, 2.1f);
            S(ProjKind.NorthPole, 342, 22, 2, 1.1f, 3f, 2.4f, 2.1f);
            // Gungnir reaches 50 px further at full stretch, 20 px wider, with gold streaks (P:47098, M:35899).
            var g = specs[ProjKind.Gungnir];
            g.SpearReach = 50f;
            g.SpearReachSize = 20f;
            g.SpearItem = ItemType.Gungnir;
            g.SpearGlint = FxBatch.C(255, 220, 80, 0);
            specs[ProjKind.NorthPole].Light = new Color(0.65f, 0.9f, 1f);

            specs[ProjKind.SpearMushroom] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_131", W = 22, H = 22, Time = 3600, Penetrate = 1, TileCollide = false,
                Light = new Color(0.05f, 0.2f, 0.5f), Update = (t, p) => t.StepSpearMushroom(p), Draw = (t, fx, p, px) => DrawSpearMushroom(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.SporeCloud] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_228", W = 30, H = 30, Scale = 1.1f, Time = 3600, Penetrate = -1, TileCollide = false, TypeCooldown = 10,
                Update = (t, p) => t.StepSporeCloud(p), Draw = (t, fx, p, px) => DrawSporeCloud(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.NorthPoleSpear] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_343", W = 10, H = 10, Scale = 1.1f, Time = 120, Penetrate = 3, AlphaStart = 1f, TileBox = new Vector2(10f, 10f),
                Update = (t, p) => t.StepNorthPoleSpear(p), Draw = (t, fx, p, px) => DrawNorthPoleSpear(fx, p, px), OnKill = (t, p) => KillNorthPoleSpear(p),
            };
            specs[ProjKind.NorthPoleFlake] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_344", W = 26, H = 26, Scale = 0.9f, Time = 240, Penetrate = 1, AlphaStart = 1f, TileCollide = false,
                Update = (t, p) => t.StepNorthPoleFlake(p), Draw = (t, fx, p, px) => DrawNorthPoleFlake(fx, p, px), OnKill = (t, p) => KillNorthPoleFlake(p),
            };
        }

        /// <summary>A projectile the spear leaves at its tip (the player's, melee).</summary>
        Proj Child(Proj p, ProjKind kind, Vector2 velocity, int damage, float knockback)
        {
            var c = Spawn(kind, p.Pos, velocity, damage, knockback, p.Crit);
            if (c != null) c.Melee = true;
            return c;
        }

        /// <summary>The spear's hitbox (Terraria px, top-left) and its size, scaled as SetDefaults scales it.</summary>
        static Vector2 SpearBox(Proj p, out int w, out int h)
        {
            w = (int)(p.Spec.W * p.Spec.Scale);
            h = (int)(p.Spec.H * p.Spec.Scale);
            return CenterPx(p) - new Vector2(w * 0.5f, h * 0.5f);
        }

        /// <summary>
        /// AI_019_Spears' per-spear part (P:46728-46900): the Mushroom Spear leaves a mushroom every 4
        /// updates of the first 25 at a third of its damage and no knockback; the Chlorophyte Partisan
        /// a spore cloud as it starts back, and the North Pole an ice spear (2.4 times as fast, 85% of
        /// the knockback) if the player can see where it is; Gungnir's and the North Pole's dust.
        /// </summary>
        void SpearExtras(Proj p, Vector2 heroCentre, bool drawingBack)
        {
            var v = TV(p);
            switch (p.Kind)
            {
                case ProjKind.MushroomSpear:
                    if (p.LocalAi0 % 4f == 0f && p.LocalAi0 <= 24f) Child(p, ProjKind.SpearMushroom, p.Vel, p.Damage / 3, 0f);
                    p.LocalAi0++;
                    break;
                case ProjKind.ChlorophytePartisan:
                    if (drawingBack && p.LocalAi0 == 0f)
                    {
                        p.LocalAi0 = 1f;
                        Child(p, ProjKind.SporeCloud, p.Vel, p.Damage, p.Knockback);
                    }
                    break;
                case ProjKind.NorthPole:
                {
                    if (drawingBack && p.LocalAi0 == 0f)
                    {
                        p.LocalAi0 = 1f;
                        if (InSight(TerraParticles.ToPx(heroCentre), CenterPx(p))) Child(p, ProjKind.NorthPoleSpear, p.Vel * 2.4f, p.Damage, p.Knockback * 0.85f);
                    }
                    if (p.TimeLeft % 2 == 0)
                    {
                        var tl = SpearBox(p, out int w, out int h);
                        int i = BoxDust(tl - v * 3f - new Vector2(10f, 10f), w + 20, h + 20, 135, v.x * 0.4f, v.y * 0.4f, 50, 1.2f);
                        if (i >= 0)
                        {
                            D(i).Velocity *= 0.5f;
                            D(i).Velocity += v * 0.25f;
                        }
                    }
                    break;
                }
                case ProjKind.Gungnir:
                {
                    var tl = SpearBox(p, out int w, out int h);
                    if (Random.Range(0, 3) == 0)
                    {
                        int i = BoxDust(tl, w, h, 57, v.x * 0.2f, v.y * 0.2f, 200, 1.2f);
                        if (i >= 0)
                        {
                            D(i).Velocity += v * 0.3f;
                            D(i).Velocity *= 0.2f;
                        }
                    }
                    if (Random.Range(0, 4) == 0)
                    {
                        int i = BoxDust(tl, w, h, 43, 0f, 0f, 254, 0.3f);
                        if (i >= 0)
                        {
                            D(i).Velocity += v * 0.5f;
                            D(i).Velocity *= 0.5f;
                        }
                    }
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ 131: the Mushroom Spear's mushroom (aiStyle 30)

        /// <summary>It slows to a stop, spinning, and fades out (4 alpha an update).</summary>
        void StepSpearMushroom(Proj p)
        {
            p.Vel *= 0.8f;
            p.TRot += 0.2f;
            p.Alpha += 4f / 255f;
            if (p.Alpha >= 1f) Kill(p);
        }

        /// <summary>GetAlpha for 131: unlit and added, by how far it has faded.</summary>
        static void DrawSpearMushroom(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Draw(FxBatch.Frame("Projectile_131", 0, 0, 22, 24, 11f, 11f), px, new Color(1f, 1f, 1f, 0f) * (1f - p.Alpha), p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 228: the Chlorophyte Partisan's spore cloud (aiStyle 44)

        /// <summary>It drifts to a stop and fades (2 alpha an update) until 200, five frames of 6 updates.</summary>
        void StepSporeCloud(Proj p)
        {
            p.Vel *= 0.96f;
            p.Alpha += 2f / 255f;
            if (p.Alpha > 200f / 255f)
            {
                Kill(p);
                return;
            }
            if (++p.FrameCounter >= 6)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 5) p.Frame = 0;
            }
        }

        static void DrawSporeCloud(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Draw(FxBatch.Frame("Projectile_228", 0, p.Frame * 32, 30, 32, 15f, 16.5f), px + new Vector2(-1.5f, 0f), new Color(L, L, L, 1f) * (1f - p.Alpha), 0f, p.Scale);
        }

        // ------------------------------------------------------------------ 343/344: the North Pole's ice spear and snowflakes

        /// <summary>
        /// aiStyle 57 (P:29298): straight for 30 updates, then falling (0.25 an update, to 16) with a
        /// little drag; it fades in at once and drops a snowflake (70% damage, 55% knockback, one of
        /// three) 8 to 14 updates after it is thrown, then every 8.
        /// </summary>
        void StepNorthPoleSpear(Proj p)
        {
            var v = TV(p);
            p.Ai0++;
            if (p.Ai0 > 30f)
            {
                p.Ai0 = 30f;
                v.y = Mathf.Min(v.y + 0.25f, 16f);
                v.x *= 0.995f;
            }
            p.TRot = Mathf.Atan2(v.y, v.x) + 1.57f;
            p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            if (p.LocalAi1 == 0f) p.LocalAi1 = Random.Range(0, 7);
            p.LocalAi0++;
            int every = 8 + (p.LocalAi1 > 0f ? (int)p.LocalAi1 : 0);
            if (p.LocalAi0 >= every)
            {
                p.LocalAi0 = 0f;
                p.LocalAi1 = -1f;
                var f = Child(p, ProjKind.NorthPoleFlake, Vector2.zero, (int)(p.Damage * 0.7f), p.Knockback * 0.55f);
                if (f != null)
                {
                    f.Ai1 = Random.Range(0, 3);
                    f.Frame = (int)f.Ai1;
                }
            }
            p.Vel = Flip(v);
        }

        /// <summary>Projectile.Kill for 343: a shatter (Item27) and a line of frost back along its flight.</summary>
        static void KillNorthPoleSpear(Proj p)
        {
            Sound("Item_27");
            var v = TV(p);
            var old = CenterPx(p) - v - new Vector2(5f, 5f);
            for (int k = 4; k < 31; k++)
            {
                int i = BoxDust(old - v * (30f / k), 8, 8, 197, v.x, v.y, 100, 1.2f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity *= 0.5f;
            }
        }

        /// <summary>GetAlpha for 343/344: near white, partly added, fading in.</summary>
        static Color FrostColor(Proj p)
        {
            float k = 1f - p.Alpha;
            return FxBatch.C((int)(250f * k), (int)(250f * k), (int)(250f * k), (int)(100f * k));
        }

        /// <summary>Main.DrawProj's generic pivot: half the hitbox's height down the sprite, a little right of its centre.</summary>
        static void DrawNorthPoleSpear(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Draw(FxBatch.Frame("Projectile_343", 0, 0, 18, 70, 9f, 5.5f), px + new Vector2(3.5f, 0f), FrostColor(p), p.TRot, p.Scale);
        }

        /// <summary>
        /// AI_001 for 344 (P:58044): gone on the ground (the block under it); fades in after 5
        /// updates, falls after 20 (0.15, with drag) and from then on every 5 updates edges away from
        /// the other snowflakes within 60 px.
        /// </summary>
        void StepNorthPoleFlake(Proj p)
        {
            var c = CenterPx(p);
            var v = TV(p);
            int w = (int)(p.Spec.W * p.Spec.Scale);
            var tl = c - new Vector2(w * 0.5f, w * 0.5f);
            if (TerraWorld.IsSolidAt(TerraParticles.ToWorld(new Vector2(tl.x, tl.y + v.y + 16f)))
                || TerraWorld.IsSolidAt(TerraParticles.ToWorld(new Vector2(tl.x + w, tl.y + v.y + 16f))))
            {
                Kill(p);
                return;
            }
            p.LocalAi1++;
            if (p.LocalAi1 > 5f) p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            p.Frame = (int)p.Ai1;
            if (p.LocalAi1 >= 20f)
            {
                v.y += 0.15f;
                v.x *= 0.96f;
            }
            if (p.LocalAi1 >= 25f)
            {
                p.LocalAi1 = 20f;
                float n = 0f, push = 0f;
                foreach (var q in live)
                {
                    if (q == p || q.Dead || q.Kind != ProjKind.NorthPoleFlake) continue;
                    var d = TerraParticles.ToPx(q.Pos) - c;
                    if (d.magnitude > 60f) continue;
                    n++;
                    push += -Mathf.Sign(d.x) * (d.x == 0f ? 0f : 1f) * Mathf.Clamp01(1f - d.magnitude / 60f);
                }
                if (n > 0f) v.x += (push == 0f ? 1f : push) * 0.25f;
            }
            v.y = Mathf.Min(v.y, 16f);
            p.Vel = Flip(v);
        }

        static void KillNorthPoleFlake(Proj p)
        {
            var v = TV(p);
            int w = (int)(p.Spec.W * p.Spec.Scale);
            var tl = CenterPx(p) - new Vector2(w * 0.5f, w * 0.5f);
            for (int k = 0; k < 8; k++)
            {
                int i = BoxDust(tl, w, w, 197, 0f, 0f, 50);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity *= 0.75f;
                D(i).Velocity -= v * 0.025f;
            }
        }

        static void DrawNorthPoleFlake(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Draw(FxBatch.Frame("Projectile_344", 0, p.Frame * 26, 26, 26, 13f, 11.5f), px + new Vector2(1.5f, 0f), FrostColor(p), p.TRot, p.Scale);
        }
    }
}
