using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Weapons that don't just fire one projectile at the cursor (Player.ItemCheck_Shoot's
    /// per-item branches: bursts, spreads, shots from the sky, several kinds at once): each
    /// registers its own way of shooting here, and <see cref="TerraMod"/>'s Fire hands it the
    /// shot it worked out (ammo, damage, speed, from the player's centre at the cursor).
    /// </summary>
    internal static partial class ShootPatterns
    {
        internal struct Shot
        {
            public ItemDef Def, Ammo;
            /// <summary>What it would shoot: the ammo's projectile, else the weapon's.</summary>
            public ProjKind Kind;
            /// <summary>The player's centre, the cursor (world units); velocity in Terraria px per update, y up.</summary>
            public Vector2 From, Mouse, Vel;
            public float Speed, Knockback, Crit;
            public int Damage, UseTicks;
            /// <summary>Extra updates a tick (the Magic Quiver's arrows).</summary>
            public int Extra;
            public bool Melee;
            public HeroController Hero;
        }

        internal delegate void Handler(in Shot shot);

        static Dictionary<ItemType, Handler> handlers;
        // Lazily: the partial files register from their own field initializers, in no set order.
        static Dictionary<ItemType, Handler> Handlers => handlers ??= new Dictionary<ItemType, Handler>();

        static bool Add(ItemType type, Handler h)
        {
            Handlers[type] = h;
            return true;
        }

        public static bool Try(in Shot shot)
        {
            if (!Handlers.TryGetValue(shot.Def.Type, out var h)) return false;
            h(in shot);
            return true;
        }

        /// <summary>One projectile, the way Fire would shoot it.</summary>
        static void One(in Shot s, ProjKind kind, Vector2 from, Vector2 vel, int damage, float knockback)
        {
            TerraProjectiles.Instance?.Shoot(kind, from, vel, damage, knockback, s.Crit, s.Extra, s.UseTicks, s.Melee);
        }

        /// <summary>Main.rand.Next(a, b): an integer in [a, b).</summary>
        static int Next(int a, int b) => Random.Range(a, b);
    }
}
