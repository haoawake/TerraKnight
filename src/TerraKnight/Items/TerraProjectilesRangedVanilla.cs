using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The arrows the hardmode bows turn every arrow into (AI_001 in Terraria's space, px y down,
    /// per update): Marrow's bone arrow (117: fast, breaking into two bone shards that hurt only
    /// after 15 updates), the Ice Bow's frost arrow (120), the Pulse Bow's charged bolt (357: unseen,
    /// a blue line of light, off six walls or enemies, losing a little each time) and the
    /// Shadowflame Bow's arrow (495: through three, setting them alight with Shadowflame).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterRanged()
        {
            specs[ProjKind.MarrowArrow] = new Spec
            {
                Ai = Ai.Arrow, Texture = "Projectile_117", Time = 1200, Extra = 2, PivotBack = 11f, GravityDelay = 35, Gravity = 0.06f,
                OnKill = (t, p) => t.KillMarrowArrow(p),
            };
            specs[ProjKind.BoneShard] = new Spec
            {
                Ai = Ai.Arrow, Texture = "Projectile_1124", W = 6, H = 6, Scale = 0.95f, Time = 600, Extra = 1,
                Update = (t, p) => StepBoneShard(p), CanDamage = p => p.Ai0 >= 15f, Draw = (t, fx, p, px) => DrawBoneShard(fx, p, px),
                OnKill = (t, p) => KillBoneShard(p),
            };
            specs[ProjKind.FrostArrow] = new Spec
            {
                Ai = Ai.Arrow, Texture = "Projectile_120", Time = 1200, Extra = 1, PivotBack = 11f, GravityDelay = 30, Gravity = 0.05f,
                Update = (t, p) => ArrowDust(p, 67), OnKill = (t, p) => KillFrostArrow(p),
            };
            specs[ProjKind.PulseBolt] = new Spec
            {
                Ai = Ai.Arrow, W = 4, H = 4, Scale = 1.2f, Time = 600, Extra = 2, Penetrate = 6, NoGravity = true, AlphaStart = 1f, AlphaFade = 25f / 255f,
                Falloff = 0.8f, Look = Look.Invisible, Update = (t, p) => StepPulseBolt(p), TileHit = (t, p, hx, hy, ov) => t.BouncePulseBolt(p, hx, hy, ov),
                OnKill = (t, p) => Sound("Item_10"),
            };
            specs[ProjKind.ShadowflameArrow] = new Spec
            {
                Ai = Ai.Arrow, Texture = "Projectile_495", Time = 1200, Penetrate = 3, PivotBack = 11f, GravityDelay = 30, Gravity = 0.04f,
                Update = (t, p) => ArrowDust(p, 27), OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Shadowflame, Random.Range(120, 300)),
                OnKill = (t, p) => KillShadowflameArrow(p),
            };
        }

        /// <summary>The frost and shadowflame arrows' trail: one dust an update just behind it, slowed (P:58231, 58462).</summary>
        static void ArrowDust(Proj p, int type)
        {
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(5f, 5f) - v;
            int i = BoxDust(tl, 10, 10, type, v.x, v.y, 100, 1.2f);
            if (i < 0) return;
            D(i).NoGravity = true;
            D(i).Velocity *= 0.3f;
        }

        /// <summary>Projectile.Kill for 117 (P:76242): a crack, bone dust, and two shards flung back the way it came at a quarter of its damage.</summary>
        void KillMarrowArrow(Proj p)
        {
            Sound("Dig_" + Random.Range(0, 3));
            var c = CenterPx(p);
            for (int k = 0; k < 10; k++) BoxDust(c - new Vector2(5f, 5f), 10, 10, 26);
            var back = -TV(p);
            back = back.sqrMagnitude > 1e-6f ? back.normalized : Vector2.left;
            for (int k = 0; k < 2; k++)
            {
                var d = RotateBy(back, Random.Range(-0.5235988f, 0.5235988f)) * (1f + Random.value * 2f);
                var s = Spawn(ProjKind.BoneShard, TerraParticles.ToWorld(c + d * 5f), Flip(d), (int)(p.Damage * 0.25f), 0f, p.Crit);
                if (s != null) s.Frame = Random.Range(0, 2);
            }
        }

        /// <summary>A shard tumbles by how fast it goes (P:58565).</summary>
        static void StepBoneShard(Proj p)
        {
            var v = TV(p);
            p.TRot += (Mathf.Abs(v.x) + Mathf.Abs(v.y)) * 0.05f;
        }

        static void DrawBoneShard(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Draw(FxBatch.Frame("Projectile_1124", 0, p.Frame * 12, 12, 12, 6f, 2.5f), px + new Vector2(3.5f, 0f), new Color(L, L, L, 1f), p.TRot, p.Scale);
        }

        static void KillBoneShard(Proj p)
        {
            Sound("Dig_" + Random.Range(0, 3));
            var c = CenterPx(p);
            for (int k = 0; k < 5; k++) BoxDust(c - new Vector2(3f, 3f), 5, 5, 26);
        }

        static void KillFrostArrow(Proj p)
        {
            Sound("Dig_" + Random.Range(0, 3));
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(5f, 5f) - v;
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(tl, 10, 10, 67, v.x, v.y, 100);
                if (i < 0) continue;
                if (k < 5) D(i).NoGravity = true;
                D(i).Velocity *= 0.2f;
            }
        }

        static void KillShadowflameArrow(Proj p)
        {
            var v = TV(p);
            var c = CenterPx(p);
            for (int k = 0; k < 15; k++)
            {
                int i = BoxDust(c, 10, 10, 27);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity -= v * 0.3f;
            }
        }

        /// <summary>P:57734: once it has faded in past 170, a line of ten still sparks along its last step, each as faded as it is.</summary>
        static void StepPulseBolt(Proj p)
        {
            int alpha = Mathf.RoundToInt(p.Alpha * 255f);
            if (alpha >= 170) return;
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(2.4f, 2.4f);
            for (int k = 0; k < 10; k++)
            {
                var at = tl - v / 10f * k;
                int i = BoxDust(at, 1, 1, 206, 0f, 0f, alpha);
                if (i < 0) continue;
                D(i).Position = at;
                D(i).Velocity = Vector2.zero;
                D(i).NoGravity = true;
            }
        }

        /// <summary>P:19026: off a wall it loses a tenth of its damage and one of its six; with none left it ends there.</summary>
        void BouncePulseBolt(Proj p, bool hitX, bool hitY, Vector2 oldVel)
        {
            if (p.Penetrate <= 0)
            {
                p.Pos += p.Vel * Px;
                Kill(p);
                return;
            }
            p.Damage = (int)(p.Damage * 0.9f);
            p.Penetrate--;
            if (hitX) p.Vel.x = -oldVel.x;
            if (hitY) p.Vel.y = -oldVel.y;
        }
    }
}
