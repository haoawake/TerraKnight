using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// What the whips' tags spawn (Terraria's space, px y down, per update): Dark Harvest's
    /// black lightning (916, AI_176): it leaps from the enemy a minion struck to the nearest other
    /// one within 400 px, homing at 10 px an update, three enemies, each once.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterWhip()
        {
            specs[ProjKind.ScytheLightning] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_916", W = 18, H = 18, Time = 3600, Penetrate = 3, Extra = 2, TileCollide = false, OncePerNpc = true, ArmorPen = 50,
                TrailLength = 20, Update = (t, p) => t.StepScytheLightning(p), Draw = (t, fx, p, px) => DrawScytheLightning(fx, p, px),
                OnHit = (t, p, hm) => FxParticles.BlackLightningHit(EnemyCenterPx(hm)), OnKill = (t, p) => { },
            };
        }

        /// <summary>WhipTagEffect_DarkHarvest.OnTaggedHit: the lightning at the struck enemy, never hitting it.</summary>
        public void ShootScytheLightning(HealthManager from, int damage)
        {
            if (from == null) return;
            var at = EnemyCenterPx(from);
            var p = Spawn(ProjKind.ScytheLightning, TerraParticles.ToWorld(at), Vector2.zero, damage, 0f, 0f);
            if (p == null) return;
            p.Immune[from.GetInstanceID()] = int.MaxValue;
            FxParticles.BlackLightningHit(at);
        }

        /// <summary>
        /// AI_176: it keeps its quarry while that can be chased and hasn't been struck; else takes
        /// the nearest unstruck enemy within 400 px (no sight needed); none: gone. Straight at it,
        /// turned along; a random frame every 3 updates.
        /// </summary>
        void StepScytheLightning(Proj p)
        {
            var c = CenterPx(p);
            if (p.Target != null && (!Chaseable(p.Target) || p.Immune.ContainsKey(p.Target.GetInstanceID()))) p.Target = null;
            if (p.Target == null)
            {
                float best = 400f;
                foreach (var hm in EnemiesNear(c, 400f))
                {
                    if (p.Immune.ContainsKey(hm.GetInstanceID())) continue;
                    float d = Vector2.Distance(c, EnemyCenterPx(hm));
                    if (d < best)
                    {
                        best = d;
                        p.Target = hm;
                    }
                }
            }
            if (p.Target == null)
            {
                Kill(p);
                return;
            }
            var v = (EnemyCenterPx(p.Target) - c).normalized * 10f;
            p.Vel = Flip(v);
            p.TRot = Mathf.Atan2(v.y, v.x);
            p.Dir = v.x < 0f ? -1f : 1f;
            if (++p.FrameCounter >= 3)
            {
                p.FrameCounter = 0;
                p.Frame = Random.Range(0, 6);
            }
        }

        /// <summary>
        /// Main.DrawProj for 916: its last 19 places as jittered black or indigo copies, growing
        /// towards the tail and fading out over its oldest four, frames running on; the head black.
        /// </summary>
        static void DrawScytheLightning(FxBatch fx, Proj p, Vector2 px)
        {
            var rnd = new System.Random(p.TimeLeft);
            int frame = p.Frame + 1;
            for (int i = Mathf.Min(19, p.Trail.Count - 1); i >= 1; i--)
            {
                var at = TerraParticles.ToPx(p.Trail[i]);
                var prev = TerraParticles.ToPx(p.Trail[i - 1]);
                frame = (frame + 1) % 6;
                Color col = Color.black;
                if (rnd.Next(3) != 0) col = Color.Lerp(FxBatch.C(106, 90, 205, 127), Color.black, 0.2f + 0.8f * (float)rnd.NextDouble());
                col *= Mathf.Clamp01((19 - i) / 15f);
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rnd.NextDouble()) * 8f;
                var d = prev - at;
                float rot = d.sqrMagnitude > 1e-4f ? Mathf.Atan2(d.y, d.x) : p.TRot;
                fx.Draw(FxBatch.Frame("Projectile_916", 0, frame * 22, 22, 22, 11f, 11f), at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, col, rot,
                    Mathf.Lerp(1f, 1.4f, i / 15f), flipX: p.Dir < 0f);
            }
            fx.Draw(FxBatch.Frame("Projectile_916", 0, p.Frame * 22, 22, 22, 11f, 11f), px, Color.black, p.TRot, 1f, flipX: p.Dir < 0f);
        }
    }
}
