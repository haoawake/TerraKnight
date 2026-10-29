using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    internal sealed partial class TerraProjectiles
    {
        static void RegisterCraftingSupplement()
        {
            // The Hallowed Jousting Lance (879) is the other lances' (TerraProjectilesBatch1021).
            specs[ProjKind.ThunderBolt] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_731", W = 10, H = 10, Time = 90, Extra = 1,
                Penetrate = 1, AlphaStart = 1, Frames = 4, Light = new Color(.3f,.6f,1f), TrailLength = 8,
                Update = (t, p) => { p.Alpha = Mathf.Max(0, p.Alpha - 15f / 255); p.Vel = p.StartVel + new Vector2(0, Mathf.Sin(p.Updates * .55f) * 5); p.Frame = p.Updates / 4 % 4; },
            };
            specs[ProjKind.LightningStrikeBolt] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_731", W = 12, H = 12, Time = 90, Extra = 2, Penetrate = 1,
                Light = new Color(.5f,.7f,1f), TrailLength = 12, Frames = 4,
                Update = (t, p) =>
                {
                    // The original upgraded lightning acquires nearby enemies and follows them.
                    if (p.Target == null) p.Target = Nearest(p, 240f);
                    if (p.Target != null) p.Vel = (Centre(p.Target) - p.Pos).normalized * 12f;
                    p.Frame = p.Updates / 4 % 4;
                },
                OnHit = (t, p, hm) =>
                {
                    if (p.Ai0 >= 3) return;
                    p.LastHit = hm.GetInstanceID();
                    p.Immune[hm.GetInstanceID()] = 9999;
                    var next = Nearest(p, 480f);
                    if (next == null || next == hm) return;
                    var child = t.Spawn(ProjKind.LightningStrikeBolt, p.Pos, (Centre(next) - p.Pos).normalized * 12f, p.Damage, p.Knockback, p.Crit);
                    if (child != null) { child.Ai0 = p.Ai0 + 1; child.Target = next; child.LastHit = hm.GetInstanceID(); }
                },
            };
        }

        /// <summary>A jousting lance's damage at a player's speed (world units a second): 0.1 + 0.9 of it over 7 px a tick (P:14536-14539), whichever way.</summary>
        public static float LanceMultiplier(Vector2 velocity) => 0.1f + velocity.magnitude / (TerraParticles.Px * 60f) / 7f * 0.9f;
    }
}
