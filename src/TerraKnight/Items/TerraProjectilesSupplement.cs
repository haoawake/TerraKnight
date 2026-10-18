using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    internal sealed partial class TerraProjectiles
    {
        static void RegisterCraftingSupplement()
        {
            specs[ProjKind.HallowLance] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_879", W = 24, H = 24, Time = 3600, Penetrate = -1,
                OwnMove = true, TileCollide = false, OwnerHitCheck = true, TypeCooldown = 24,
                Update = (t, p) => t.StepHallowLance(p), OnKill = (t, p) => { },
            };
            specs[ProjKind.ThunderBolt] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_731", W = 10, H = 10, Time = 90, Extra = 1,
                Penetrate = 1, AlphaStart = 1, Frames = 4, Light = new Color(.3f,.6f,1f), TrailLength = 8,
                Update = (t, p) => { p.Alpha = Mathf.Max(0, p.Alpha - 15f / 255); p.Vel = p.StartVel + new Vector2(0, Mathf.Sin(p.Updates * .55f) * 5); p.Frame = p.Updates / 4 % 4; },
            };
            specs[ProjKind.LightningStrikeBolt] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_731", W = 12, H = 12, Time = 30, Extra = 4, Penetrate = 1,
                Light = new Color(.5f,.7f,1f), TrailLength = 12, Frames = 4,
                Update = (t, p) =>
                {
                    // The original upgraded lightning acquires nearby enemies and follows them.
                    if (p.Target == null) p.Target = Nearest(p, 240f);
                    if (p.Target != null) p.Vel = ((Vector2)p.Target.transform.position - p.Pos).normalized * 12f;
                    p.Frame = p.Updates / 4 % 4;
                },
                OnHit = (t, p, hm) =>
                {
                    if (p.Ai0 >= 3) return;
                    p.LastHit = hm.GetInstanceID();
                    p.Immune[hm.GetInstanceID()] = 9999;
                    var next = Nearest(p, 480f);
                    if (next == null || next == hm) return;
                    var child = t.Spawn(ProjKind.LightningStrikeBolt, p.Pos, ((Vector2)next.transform.position - p.Pos).normalized * 12f, p.Damage, p.Knockback, p.Crit);
                    if (child != null) { child.Ai0 = p.Ai0 + 1; child.Target = next; child.LastHit = hm.GetInstanceID(); }
                },
            };
        }

        public static float LanceMultiplier(Vector2 velocity, Vector2 aim)
        {
            // Forward movement drives a jousting lance; a stationary lance retains its base damage.
            float forward = Mathf.Max(0f, Vector2.Dot(velocity, aim.normalized));
            return 1f + forward / (TerraParticles.Px * 60f) / 6f;
        }

        void StepHallowLance(Proj p)
        {
            var hero = HeroController.SilentInstance;
            var mod = TerraMod.Instance;
            if (hero == null || mod == null || !mod.UseHeld || mod.Inventory.Current.Type != ItemType.HallowJoustingLance) { Kill(p); return; }
            Vector2 center = hero.GetComponent<BoxCollider2D>().bounds.center;
            Vector2 aim = (mod.DevAim ?? mod.AimPoint) - center;
            if (aim.sqrMagnitude < .01f) aim = Vector2.right;
            aim.Normalize();
            p.Pos = center + aim * (100f * TerraParticles.Px);
            p.Rot = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg - 45f;
            if (p.LocalAi0 == 0) p.LocalAi0 = p.Damage;
            p.Damage = Mathf.RoundToInt(p.LocalAi0 * LanceMultiplier(hero.GetComponent<Rigidbody2D>().linearVelocity, aim));
            p.TimeLeft = 2;
        }
    }
}
