using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>The hardmode swords' own ways of shooting (Player.ItemCheck_Shoot).</summary>
    internal static partial class ShootPatterns
    {
        static readonly bool swords =
            Add(ItemType.StarWrath, StarWrath) &
            Add(ItemType.Meowmere, Meowmere) &
            Add(ItemType.InfluxWaver, Influx);

        /// <summary>
        /// Player.cs:48186: three stars from 600, 700 and 800 px over the player, up to 400 px behind
        /// it, down at the cursor (never upward, at least 20 px down for each of its length), a
        /// little apart in fall; each passes through blocks until below the cursor, which counts as
        /// no lower than 200 px over the player.
        /// </summary>
        static void StarWrath(in Shot s)
        {
            const float Px = TerraParticles.Px;
            float dir = s.Mouse.x < s.From.x ? -1f : 1f;
            float floor = Mathf.Max(s.Mouse.y, s.From.y + 200f * Px);
            for (int k = 0; k < 3; k++)
            {
                var from = s.From + new Vector2(-Next(0, 401) * dir, 600f + 100f * k) * Px;
                // In Terraria's px, y down.
                var v = new Vector2(s.Mouse.x - from.x, from.y - s.Mouse.y) / Px;
                if (v.y < 0f) v.y = -v.y;
                if (v.y < 20f) v.y = 20f;
                v = v.normalized * s.Speed;
                v.y += Next(-40, 41) * 0.02f;
                TerraProjectiles.Instance?.ShootStarWrath(from, new Vector2(v.x, -v.y), s.Damage, s.Knockback, s.Crit, floor);
            }
        }

        /// <summary>Player.cs:47540: the Meowmere's cat hits a quarter harder than the sword.</summary>
        static void Meowmere(in Shot s) => One(s, ProjKind.Meowmere, s.From, s.Vel, (int)(s.Damage * 1.25f), s.Knockback);

        /// <summary>Player.cs:49923: the Influx Waver's blade starts at ai[0] = -1.</summary>
        static void Influx(in Shot s) => TerraProjectiles.Instance?.ShootInflux(s.From, s.Vel, s.Damage, s.Knockback, s.Crit);
    }
}
