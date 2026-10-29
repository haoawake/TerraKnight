using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The magic weapons' casts, look only (the user's 10-26 "法师武器的挥舞动画，全改…不要生硬的贴图动画，要么就用原版的升级要么
    /// 就用粒子效果。每个挥舞都符合武器本身，但并不造成伤害"): how each is moved through a use and what comes off it; nothing here
    /// hits. A staff flicks up and snaps to the aim as it casts, settling as a sprung thing does, magic gathering at its head; the
    /// staffs that call from the sky are raised high and brought down; a book floats and nods as it casts, runes round it, a
    /// circle opening before it; a magic gun kicks up and back and eases home, its muzzle flashing; a wand is swept round in a
    /// flourish (SwordFx draws it) that ends on the aim, its tip trailing; a harp is plucked, a guitar strummed, held things
    /// hover. Each in its weapon's colours and motes (Terraria's own dusts). With the remade swing on (BladeArts.Enabled).
    /// </summary>
    internal static class MageArts
    {
        internal enum Kind : byte { Staff, SkyStaff, Tome, Gun, Wand, Harp, Guitar, Held, Hand, Raise }

        /// <summary>A weapon's cast: how it moves, its two colours and the dust its motes are (278: tinted by the colours).</summary>
        internal readonly struct Look
        {
            public readonly Kind Kind;
            public readonly Color A, B;
            public readonly int Dust;
            public Look(Kind kind, Color a, Color b, int dust = 278) { Kind = kind; A = a; B = b; Dust = dust; }
        }

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static readonly Color Fire1 = C(1f, 0.55f, 0.15f), Fire2 = C(1f, 0.9f, 0.4f), Ice1 = C(0.5f, 0.85f, 1f), Ice2 = Color.white,
            Volt1 = C(0.6f, 0.9f, 1f), Volt2 = C(1f, 1f, 0.7f), Shade1 = C(0.55f, 0.25f, 0.8f), Shade2 = C(0.85f, 0.6f, 1f),
            Blood1 = C(0.85f, 0.1f, 0.15f), Blood2 = C(1f, 0.45f, 0.4f), Soul1 = C(0.85f, 0.92f, 1f), Soul2 = Color.white,
            Leaf1 = C(0.35f, 0.75f, 0.25f), Leaf2 = C(0.75f, 1f, 0.45f), Water1 = C(0.25f, 0.55f, 1f), Water2 = C(0.65f, 0.9f, 1f),
            Cosmic1 = C(0.85f, 0.35f, 1f), Cosmic2 = C(0.45f, 0.8f, 1f), Gold1 = C(1f, 0.82f, 0.3f), Gold2 = C(1f, 0.97f, 0.75f);

        static readonly Dictionary<ItemType, Look> looks = new Dictionary<ItemType, Look>
        {
            // Staffs: their gems' and their heads' colours.
            [ItemType.DaybloomStaff] = new Look(Kind.Staff, C(1f, 0.9f, 0.35f), C(0.6f, 0.95f, 0.35f)),
            [ItemType.AmethystStaff] = new Look(Kind.Staff, C(0.75f, 0.35f, 1f), C(0.95f, 0.75f, 1f), 86),
            [ItemType.TopazStaff] = new Look(Kind.Staff, C(1f, 0.75f, 0.15f), C(1f, 0.95f, 0.6f), 87),
            [ItemType.SapphireStaff] = new Look(Kind.Staff, C(0.25f, 0.45f, 1f), C(0.65f, 0.8f, 1f), 88),
            [ItemType.EmeraldStaff] = new Look(Kind.Staff, C(0.2f, 0.9f, 0.4f), C(0.7f, 1f, 0.75f), 89),
            [ItemType.RubyStaff] = new Look(Kind.Staff, C(1f, 0.15f, 0.25f), C(1f, 0.6f, 0.65f), 90),
            [ItemType.DiamondStaff] = new Look(Kind.Staff, C(0.85f, 0.95f, 1f), Color.white, 91),
            [ItemType.AmberStaff] = new Look(Kind.Staff, C(1f, 0.55f, 0.1f), C(1f, 0.85f, 0.45f), 138),
            [ItemType.ThunderStaff] = new Look(Kind.Staff, Volt1, Volt2, 226),
            [ItemType.GlacierFang] = new Look(Kind.Staff, Ice1, Ice2, 135),
            [ItemType.Petalstorm] = new Look(Kind.Staff, C(1f, 0.55f, 0.75f), C(1f, 0.85f, 0.9f)),
            [ItemType.SkyFracture] = new Look(Kind.Staff, C(0.45f, 0.85f, 1f), Color.white, 15),
            [ItemType.SoulDrain] = new Look(Kind.Staff, Blood1, Blood2, 5),
            [ItemType.MeteorStaff] = new Look(Kind.SkyStaff, Fire1, Fire2, 6),
            [ItemType.CrystalVileShard] = new Look(Kind.Staff, C(0.6f, 0.35f, 0.85f), C(0.85f, 0.7f, 1f)),
            [ItemType.LightningStrike] = new Look(Kind.SkyStaff, Volt1, Volt2, 226),
            [ItemType.BookStaff] = new Look(Kind.Staff, C(0.95f, 0.9f, 0.75f), C(0.6f, 0.85f, 1f)),
            [ItemType.FrostStaff] = new Look(Kind.Staff, Ice1, Ice2, 135),
            [ItemType.BejeweledStaff] = new Look(Kind.Staff, C(1f, 0.4f, 0.8f), C(0.5f, 0.9f, 1f)),
            [ItemType.AbyssStaff] = new Look(Kind.Staff, C(0.25f, 0.15f, 0.4f), C(0.7f, 0.6f, 0.95f), 27),
            [ItemType.CrystalSerpent] = new Look(Kind.Staff, C(1f, 0.45f, 0.85f), C(0.6f, 0.95f, 1f)),
            [ItemType.PoisonStaff] = new Look(Kind.Staff, C(0.45f, 0.85f, 0.25f), C(0.8f, 1f, 0.5f)),
            [ItemType.VenomStaff] = new Look(Kind.Staff, C(0.6f, 0.25f, 0.85f), C(0.9f, 0.65f, 1f)),
            [ItemType.PrincessWeapon] = new Look(Kind.Staff, C(1f, 0.5f, 0.8f), C(1f, 0.9f, 0.95f)),
            [ItemType.UnholyTrident] = new Look(Kind.Staff, C(0.85f, 0.15f, 0.35f), C(1f, 0.55f, 0.6f), 27),
            [ItemType.NettleBurst] = new Look(Kind.Staff, Leaf1, Leaf2, 39),
            [ItemType.StaffofEarth] = new Look(Kind.SkyStaff, C(0.6f, 0.45f, 0.3f), C(0.85f, 0.75f, 0.55f), 1),
            [ItemType.BlizzardStaff] = new Look(Kind.SkyStaff, Ice1, Ice2, 76),
            [ItemType.Razorpine] = new Look(Kind.Staff, C(0.25f, 0.6f, 0.35f), C(0.85f, 1f, 0.9f)),
            [ItemType.BatScepter] = new Look(Kind.Staff, C(0.5f, 0.1f, 0.25f), C(0.85f, 0.4f, 0.5f)),
            [ItemType.BetsysWrath] = new Look(Kind.Staff, C(1f, 0.45f, 0.2f), C(0.8f, 0.4f, 1f), 6),
            [ItemType.ShadowbeamStaff] = new Look(Kind.Staff, Shade1, Shade2, 27),
            [ItemType.SpectreStaff] = new Look(Kind.Staff, C(0.45f, 0.85f, 1f), Color.white),
            [ItemType.InfernoFork] = new Look(Kind.Staff, Fire1, Fire2, 6),
            [ItemType.NebulaArcanum] = new Look(Kind.Staff, Cosmic1, Cosmic2),
            [ItemType.ArcSurge] = new Look(Kind.Staff, Volt1, Volt2, 226),
            // Books.
            [ItemType.WaterBolt] = new Look(Kind.Tome, Water1, Water2),
            [ItemType.BookofSkulls] = new Look(Kind.Tome, C(0.95f, 0.9f, 0.8f), C(1f, 0.6f, 0.3f), 26),
            [ItemType.DemonScythe] = new Look(Kind.Tome, Shade1, Shade2, 27),
            [ItemType.GoldenShower] = new Look(Kind.Tome, C(1f, 0.8f, 0.15f), C(1f, 0.95f, 0.5f)),
            [ItemType.CrystalStorm] = new Look(Kind.Tome, C(1f, 0.5f, 0.9f), C(0.55f, 0.95f, 1f)),
            [ItemType.CursedFlames] = new Look(Kind.Tome, C(0.55f, 1f, 0.15f), C(0.85f, 1f, 0.6f)),
            [ItemType.RazorbladeTyphoon] = new Look(Kind.Tome, C(0.3f, 0.75f, 1f), Color.white),
            [ItemType.LunarFlareBook] = new Look(Kind.Tome, C(0.55f, 0.95f, 1f), Color.white),
            [ItemType.MagnetSphere] = new Look(Kind.Tome, C(0.4f, 0.9f, 1f), Volt2, 226),
            // Magic guns.
            [ItemType.ZapinatorGray] = new Look(Kind.Gun, C(1f, 0.5f, 0.9f), C(0.5f, 1f, 0.9f), 66),
            [ItemType.ZapinatorOrange] = new Look(Kind.Gun, C(1f, 0.6f, 0.2f), C(0.5f, 1f, 0.9f), 66),
            [ItemType.SpaceGun] = new Look(Kind.Gun, C(0.35f, 1f, 0.4f), C(0.8f, 1f, 0.8f)),
            [ItemType.BeeGun] = new Look(Kind.Gun, C(1f, 0.75f, 0.15f), C(1f, 0.95f, 0.5f)),
            [ItemType.LaserRifle] = new Look(Kind.Gun, C(0.75f, 0.3f, 1f), C(1f, 0.75f, 1f)),
            [ItemType.HeatRay] = new Look(Kind.Gun, Fire1, Fire2, 6),
            [ItemType.LeafBlower] = new Look(Kind.Gun, Leaf1, Leaf2, 39),
            [ItemType.BubbleGun] = new Look(Kind.Gun, C(0.6f, 0.85f, 1f), Color.white),
            [ItemType.RainbowGun] = new Look(Kind.Gun, C(1f, 0.4f, 0.6f), C(0.5f, 0.9f, 1f), 66),
            [ItemType.LaserMachinegun] = new Look(Kind.Gun, C(0.3f, 0.9f, 1f), Color.white, 226),
            [ItemType.WaspGun] = new Look(Kind.Gun, C(1f, 0.7f, 0.1f), C(1f, 0.9f, 0.4f)),
            [ItemType.ChargedBlasterCannon] = new Look(Kind.Gun, C(0.35f, 0.65f, 1f), Color.white, 226),
            [ItemType.AquaScepter] = new Look(Kind.Gun, Water1, Water2),
            // Held things.
            [ItemType.VengefulSpiritFocus] = new Look(Kind.Held, Soul1, Soul2),
            [ItemType.HowlingWraithsFocus] = new Look(Kind.Held, Soul1, C(0.75f, 0.85f, 1f)),
            [ItemType.ShadeSoulFocus] = new Look(Kind.Held, C(0.2f, 0.15f, 0.3f), C(0.6f, 0.55f, 0.75f), 27),
            [ItemType.WeatherPain] = new Look(Kind.Held, C(0.75f, 0.8f, 0.9f), C(0.5f, 0.75f, 1f), 31),
            [ItemType.SpiritFlame] = new Look(Kind.Held, C(0.5f, 0.45f, 1f), C(0.8f, 0.85f, 1f)),
            [ItemType.ShadowFlameHexDoll] = new Look(Kind.Held, Shade1, Shade2, 27),
            [ItemType.Nightglow] = new Look(Kind.Held, C(0.5f, 0.75f, 1f), C(1f, 0.95f, 0.8f)),
            [ItemType.LastPrism] = new Look(Kind.Held, C(1f, 0.5f, 0.6f), C(0.5f, 0.9f, 1f), 66),
            [ItemType.NebulaBlaze] = new Look(Kind.Held, Cosmic1, Cosmic2),
            [ItemType.MagicalHarp] = new Look(Kind.Harp, C(1f, 0.6f, 0.85f), C(0.6f, 0.85f, 1f)),
            [ItemType.SparkleGuitar] = new Look(Kind.Guitar, Gold1, C(1f, 0.6f, 0.9f), 57),
            // Wands swept round.
            [ItemType.WandofSparking] = new Look(Kind.Wand, Fire1, Fire2, 6),
            [ItemType.WandofFrosting] = new Look(Kind.Wand, Ice1, Ice2, 135),
            [ItemType.CrimsonRod] = new Look(Kind.Wand, Blood1, Blood2, 5),
            [ItemType.Vilethorn] = new Look(Kind.Wand, C(0.55f, 0.35f, 0.75f), C(0.7f, 0.85f, 0.35f)),
            [ItemType.MagicMissile] = new Look(Kind.Wand, C(0.35f, 0.55f, 1f), C(0.8f, 0.9f, 1f), 15),
            [ItemType.Flamelash] = new Look(Kind.Wand, Fire1, Fire2, 6),
            [ItemType.FlowerofFire] = new Look(Kind.Wand, Fire1, Fire2, 6),
            [ItemType.ClingerStaff] = new Look(Kind.Wand, C(0.55f, 1f, 0.15f), C(0.85f, 1f, 0.6f)),
            [ItemType.IceRod] = new Look(Kind.Wand, Ice1, Ice2, 135),
            [ItemType.FlowerofFrost] = new Look(Kind.Wand, Ice1, Ice2, 135),
            [ItemType.RainbowRod] = new Look(Kind.Wand, C(1f, 0.45f, 0.6f), C(0.5f, 0.9f, 1f), 66),
            [ItemType.NimbusRod] = new Look(Kind.Wand, C(0.7f, 0.8f, 0.95f), Water2, 31),
            // Thrown from the hand, raised.
            [ItemType.MedusaHead] = new Look(Kind.Hand, C(0.75f, 0.95f, 0.45f), Gold2),
            [ItemType.MagicDagger] = new Look(Kind.Hand, C(0.45f, 0.65f, 1f), Color.white, 15),
            [ItemType.KillingDeck] = new Look(Kind.Hand, C(0.9f, 0.2f, 0.25f), Gold2),
            [ItemType.ToxicFlask] = new Look(Kind.Hand, C(0.45f, 0.95f, 0.3f), C(0.8f, 1f, 0.55f)),
            [ItemType.SharpTears] = new Look(Kind.Raise, Blood1, Blood2, 5),
        };

        /// <summary>The cast's look: the table's, else by how it is held (its colours a soft violet).</summary>
        public static Look Of(ItemDef d)
        {
            if (d != null && looks.TryGetValue(d.Type, out var l)) return l;
            var kind = d == null ? Kind.Staff : d.NoUseGraphic ? Kind.Hand : d.HoldUp ? Kind.Raise : !d.HoldOut ? Kind.Wand : d.StaffHold ? Kind.Staff : GunShaped(d) ? Kind.Gun : Kind.Tome;
            return new Look(kind, C(0.65f, 0.5f, 1f), C(0.9f, 0.85f, 1f), 15);
        }

        static bool GunShaped(ItemDef d)
        {
            var s = TerraMod.ItemSprite(d.Type);
            return s != null && s.rect.width > s.rect.height * 1.4f;
        }

        /// <summary>A magic weapon cast its new way: the remade swing on, a magic weapon.</summary>
        public static bool Active(ItemDef d) => d != null && WeaponRefitting.EnabledFor(d) && d.IsWeapon && d.Class == DamageClass.Magic && !Player.GravityGlobe.Flipped;

        /// <summary>
        /// How grand its casting is (the user's 10-28 "越后期的武器我希望越伟大"): 1 for the first stages, 2 for stages 2-3, 3 for
        /// stage 4, 4 for stage 5, 5 for the Moon Lord's and the like (rarity 10 and up).
        /// </summary>
        public static int Grand(ItemDef d) => d == null ? 1 : d.Rare >= 10 ? 5 : d.Stage >= 5 ? 4 : d.Stage >= 4 ? 3 : d.Stage >= 2 ? 2 : 1;

        /// <summary>A held one used with no use of its own to run through (channelled, its shot holding it out): moved by the clock.</summary>
        public static bool Continuous(ItemDef d) => d != null && d.Channel;

        // ------------------------------------------------------------------ the hold through a use

        /// <summary>A sprung thing let go: from 1 at the start, swinging past 0 and settling (rate, swings).</summary>
        static float Spring(float p, float rate, float swings) => Mathf.Exp(-rate * p) * Mathf.Cos(swings * Mathf.PI * p);

        /// <summary>
        /// The held-out weapon through its use (p 0..1; t the clock for those held on): its turn from the aim (degrees, + up)
        /// and how far it is pushed along the aim (px, − back), and its rise (px, + up).
        /// </summary>
        public static void Motion(ItemDef d, float p, float t, out float turn, out float push, out float rise)
        {
            turn = push = rise = 0f;
            var look = Of(d);
            bool quick = d.UseAnimation * 60f <= 14f;
            bool held = Continuous(d);
            switch (look.Kind)
            {
                case Kind.Staff:
                    if (held) { turn = 3f * Mathf.Sin(t * 5f); rise = Mathf.Sin(t * 3f); break; }
                    // Flicked up as the spell leaves, snapped onto the aim, settling.
                    turn = (quick ? 9f : 24f) * Spring(p, 6f, 3.2f);
                    push = (quick ? 2f : 4f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(p / 0.35f));
                    break;
                case Kind.SkyStaff:
                    // Raised high to call it down, then brought back to the aim.
                    float up = Mathf.Clamp01(p / 0.12f) * (1f - Mathf.Clamp01((p - 0.55f) / 0.35f));
                    turn = (quick ? 35f : 70f) * up * up * (3f - 2f * up);
                    rise = 3f * up;
                    break;
                case Kind.Tome:
                    // The book floats, nodding forward as it casts.
                    rise = 1.5f * Mathf.Sin(t * 6.5f) + 2f;
                    turn = -(quick ? 4f : 9f) * Spring(p, 7f, 2f);
                    push = 3f * Mathf.Exp(-6f * p);
                    break;
                case Kind.Gun:
                    if (held) { turn = 1.5f * Mathf.Sin(t * 40f); push = -1f - 0.7f * Mathf.Sin(t * 33f); break; }
                    // The kick: up and back, eased home.
                    turn = (quick ? 4f : 11f) * Mathf.Exp(-7f * p);
                    push = -(quick ? 2.5f : 6f) * Mathf.Exp(-7f * p);
                    break;
                case Kind.Harp:
                    turn = 5f * Mathf.Sin(p * Mathf.PI * 4f) * (1f - p);
                    rise = 1f;
                    break;
                case Kind.Guitar:
                    turn = 12f * Spring(p, 5f, 4f);
                    break;
                case Kind.Held:
                    rise = 2f * Mathf.Sin(t * 4.5f) + 1.5f;
                    turn = held ? 2f * Mathf.Sin(t * 3f) : 6f * Spring(p, 6f, 2.5f);
                    push = held ? 0f : 2f * Mathf.Exp(-6f * p);
                    break;
            }
        }

        /// <summary>
        /// A wand's flourish (degrees from level for the way he faces, + up) at p of its use, toward the aim at aimDeg: raised
        /// back over the head, swept down and round onto the aim (a little past it), settling there.
        /// </summary>
        public static float WandAngle(float p, float aimDeg, bool quick)
        {
            float from = aimDeg + (quick ? 85f : 125f);
            if (p < 0.42f)
            {
                float x = p / 0.42f;
                float e = x * x * (3f - 2f * x);
                return Mathf.Lerp(from, aimDeg - 14f, e);
            }
            float y = Mathf.Clamp01((p - 0.42f) / 0.3f);
            return Mathf.Lerp(aimDeg - 14f, aimDeg, y * y * (3f - 2f * y));
        }
    }
}
