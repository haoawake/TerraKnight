using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The hardmode guns' and bows' own ways of shooting (Player.ItemCheck_Shoot, Pl:47512-48920):
    /// their spray, the Musket Ball turned into a High Velocity Bullet, the bows that turn every
    /// arrow into their own, the shotguns' pellets, the Clockwork Assault Rifle's burst, the
    /// Tsunami's fan, the Chlorophyte Shotbow's spread and the Daedalus Stormbow's rain.
    /// Velocities in Terraria px per update (the spreads are symmetric, so y up or down alike).
    /// </summary>
    internal static partial class ShootPatterns
    {
        static readonly bool ranged =
            Add(ItemType.Megashark, Megashark) & Add(ItemType.Uzi, Uzi) & Add(ItemType.VenusMagnum, HighVelocity) & Add(ItemType.SniperRifle, HighVelocity) &
            Add(ItemType.SDMG, Sdmg) & Add(ItemType.ChainGun, ChainGun) & Add(ItemType.Gatligator, Gatligator) & Add(ItemType.TacticalShotgun, TacticalShotgun) &
            Add(ItemType.ClockworkAssaultRifle, Clockwork) & Add(ItemType.ChlorophyteShotbow, Shotbow) & Add(ItemType.Tsunami, Tsunami) &
            Add(ItemType.DaedalusStormbow, Stormbow) & Add(ItemType.Marrow, Marrow) & Add(ItemType.IceBow, IceBow) & Add(ItemType.PulseBow, PulseBow) &
            Add(ItemType.ShadowFlameBow, ShadowflameBow);

        /// <summary>Main.rand.Next(-n, n + 1) * k on each axis.</summary>
        static Vector2 Jitter(int n, float k) => new Vector2(Next(-n, n + 1) * k, Next(-n, n + 1) * k);

        static void Spray(in Shot s, ProjKind kind, Vector2 vel) => One(s, kind, s.From, vel, s.Damage, s.Knockback);

        /// <summary>Pl:47512-47523: the Sniper Rifle, Venus Magnum and Uzi make a Musket Ball a High Velocity Bullet.</summary>
        static ProjKind Hv(in Shot s) => s.Kind == ProjKind.Bullet ? ProjKind.HighVelocityBullet : s.Kind;

        static void Megashark(in Shot s) => Spray(s, s.Kind, s.Vel + Jitter(40, 0.01f));
        static void Uzi(in Shot s) => Spray(s, Hv(s), s.Vel + Jitter(30, 0.03f));
        static void HighVelocity(in Shot s) => Spray(s, Hv(s), s.Vel);
        static void Sdmg(in Shot s) => Spray(s, s.Kind, s.Vel + Jitter(40, 0.005f));

        /// <summary>Pl:47709 and 48244: up to 1.5 px off before the speed, then up to 1.2 more.</summary>
        static void ChainGun(in Shot s) => Spray(s, s.Kind, s.Vel + Jitter(50, 0.03f) + Jitter(40, 0.03f));

        /// <summary>Pl:47709 and 48046: up to 1.5 and then 2 px off, and one in three 60% faster or slower on each axis.</summary>
        static void Gatligator(in Shot s)
        {
            var v = s.Vel + Jitter(50, 0.03f) + Jitter(40, 0.05f);
            if (Next(0, 3) == 0)
            {
                v.x *= 1f + Next(-30, 31) * 0.02f;
                v.y *= 1f + Next(-30, 31) * 0.02f;
            }
            Spray(s, s.Kind, v);
        }

        /// <summary>Pl:48833: six pellets, each up to 2 px off, for one round.</summary>
        static void TacticalShotgun(in Shot s)
        {
            for (int k = 0; k < 6; k++) Spray(s, s.Kind, s.Vel + Jitter(40, 0.05f));
        }

        /// <summary>
        /// Pl:48901: three rounds, 4 ticks apart, aimed where the player aims as each goes; the
        /// second wanders up to 0.2 px and is 5% faster, the third 0.4 px and 10% (itemAnimation
        /// under 10, then under 5). Only the first costs ammo.
        /// </summary>
        static void Clockwork(in Shot s)
        {
            Spray(s, s.Kind, s.Vel);
            var pr = TerraProjectiles.Instance;
            if (pr == null) return;
            var copy = s;
            pr.Later(4, (from, dir) => One(copy, copy.Kind, from, (dir * copy.Speed + Jitter(20, 0.01f)) * 1.05f, copy.Damage, copy.Knockback));
            pr.Later(8, (from, dir) => One(copy, copy.Kind, from, (dir * copy.Speed + Jitter(40, 0.01f)) * 1.1f, copy.Damage, copy.Knockback));
        }

        /// <summary>Pl:48750: two arrows (three, one time in three), each after the first wandering up to 1.4 px more.</summary>
        static void Shotbow(in Shot s)
        {
            int n = 2 + (Next(0, 3) == 0 ? 1 : 0);
            for (int k = 0; k < n; k++)
            {
                var v = s.Vel;
                for (int j = 0; j < k; j++) v += Jitter(35, 0.04f);
                Spray(s, s.Kind, v);
            }
        }

        /// <summary>
        /// Pl:48217: five arrows side by side along a fan 40 px out from the player, a tenth of a
        /// half turn apart, all flying straight at the cursor; pulled back to the player when the
        /// fan would start in a wall.
        /// </summary>
        static void Tsunami(in Shot s)
        {
            const float Px = TerraParticles.Px;
            var dir = s.Vel.sqrMagnitude > 1e-6f ? s.Vel.normalized : Vector2.right;
            var arm = dir * 40f;
            bool open = !Blocked(s.From, s.From + arm * Px);
            for (int k = 0; k < 5; k++)
            {
                float a = Mathf.PI / 10f * (k - 2f);
                var off = new Vector2(arm.x * Mathf.Cos(a) - arm.y * Mathf.Sin(a), arm.x * Mathf.Sin(a) + arm.y * Mathf.Cos(a));
                if (!open) off -= arm;
                var at = s.From + off * Px;
                if (Blocked(s.From, at - s.Vel * Px)) at -= (at - s.From).normalized * 15f * Px;
                One(s, s.Kind, at, s.Vel, s.Damage, s.Knockback);
            }
        }

        /// <summary>
        /// Pl:47835: three arrows (one less, one time in three, for the holy, unholy, jester's and
        /// hellfire arrows; else one more, one time in three) from 600 px over the player and 150
        /// apart, between the cursor and the player, down at the cursor and never upward, each a
        /// little off and up to half again as fast across.
        /// </summary>
        static void Stormbow(in Shot s)
        {
            const float Px = TerraParticles.Px;
            int n = 3;
            bool few = s.Kind == ProjKind.HolyArrow || s.Kind == ProjKind.UnholyArrow || s.Kind == ProjKind.JesterArrow || s.Kind == ProjKind.HellfireArrow;
            if (Next(0, 3) == 0) n += few ? -1 : 1;
            float dir = s.Mouse.x < s.From.x ? -1f : 1f;
            for (int k = 0; k < n; k++)
            {
                // Terraria's px, y down, measured from the player's centre.
                float x = (s.Mouse.x - s.From.x) / Px + 10f + Next(0, 201) * -dir;
                x = x * 10f / 11f + Next(-100, 101);
                float y = -600f - 150f * k;
                var to = new Vector2((s.Mouse.x - s.From.x) / Px - x, (s.From.y - s.Mouse.y) / Px - y);
                if (to.y < 0f) to.y = -to.y;
                if (to.y < 20f) to.y = 20f;
                var v = to.normalized * s.Speed + Jitter(40, 0.03f);
                v.x *= Next(75, 150) * 0.01f;
                x += Next(-50, 51);
                One(s, s.Kind, s.From + new Vector2(x, -y) * Px, new Vector2(v.x, -v.y), s.Damage, s.Knockback);
            }
        }

        // Pl:47566-47581, 54140: the bows that shoot only their own arrows.
        static void Marrow(in Shot s) => Spray(s, ProjKind.MarrowArrow, s.Vel);
        static void IceBow(in Shot s) => Spray(s, ProjKind.FrostArrow, s.Vel);
        static void PulseBow(in Shot s) => Spray(s, ProjKind.PulseBolt, s.Vel);
        static void ShadowflameBow(in Shot s) => Spray(s, ProjKind.ShadowflameArrow, s.Vel);

        /// <summary>Collision.CanHit's opposite: a block between two points (world units).</summary>
        static bool Blocked(Vector2 a, Vector2 b) => Physics2D.Linecast(a, b, 1 << ChunkColliders.TerrainLayer);
    }
}
