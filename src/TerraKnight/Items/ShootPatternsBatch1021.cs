using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The 10-21 batch's ways of shooting (Player.ItemCheck_Shoot): the Arc Surge's bolts, the Blood Rain Bow's blood from the
    /// sky, the Resonance Scepter's ring as far along the line to the cursor as the player can see.
    /// </summary>
    internal static partial class ShootPatterns
    {
        static readonly bool batch1021 =
            Add(ItemType.ArcSurge, (in Shot s) => TerraProjectiles.Instance?.FireArcSurge(s.From, s.Mouse, s.Vel, s.Speed, s.Damage, s.Knockback, s.Crit)) &
            Add(ItemType.BloodRainBow, BloodRainBow) &
            Add(ItemType.PrincessWeapon, (in Shot s) => TerraProjectiles.Instance?.FirePrincessRing(s.From, s.Mouse, s.Vel, s.Damage, s.Knockback, s.Crit));

        /// <summary>
        /// Pl:47877-47909, 54148: one or two blood arrows (three one time in three) from 600-750 px over a point near the
        /// cursor, 0-60 px behind the player's facing and drawn a tenth of the way back to them, aimed down at the cursor,
        /// a little astray, slowed to 55-79% across.
        /// </summary>
        static void BloodRainBow(in Shot s)
        {
            const float Px = TerraParticles.Px;
            int n = Next(1, 3);
            if (Next(0, 3) == 0) n++;
            float dir = s.Mouse.x < s.From.x ? -1f : 1f;
            for (int k = 0; k < n; k++)
            {
                // Terraria's px, y down, measured from the player's centre.
                float x = (s.Mouse.x - s.From.x) / Px + 10f + Next(0, 61) * -dir;
                x = x * 10f / 11f + Next(-30, 31);
                float y = -600f - 150f * Random.value;
                var to = new Vector2((s.Mouse.x - s.From.x) / Px - x, (s.From.y - s.Mouse.y) / Px - y);
                if (to.y < 0f) to.y = -to.y;
                if (to.y < 20f) to.y = 20f;
                var v = to.normalized * s.Speed + new Vector2(Next(-20, 21) * 0.03f, Next(-40, 41) * 0.03f);
                v.x *= Next(55, 80) * 0.01f;
                x += Next(-50, 51);
                One(s, ProjKind.BloodRainArrow, s.From + new Vector2(x, -y) * Px, new Vector2(v.x, -v.y), s.Damage, s.Knockback);
            }
        }
    }
}
