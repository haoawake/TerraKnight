using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>
    /// Late original weapons, measured without stationary stealth (2026-10-27).
    /// Apply after the native damage floor: native hit damage alone does not account
    /// for this game's melee speed stacking and overlapping projectile hits.
    /// Changes only base damage; cadence, volleys, ammunition and immunity stay intact. A weapon in the class balance's
    /// table (WeaponCalibration, from Terraria's own damages, 2026-10-28) keeps that table's.
    /// </summary>
    internal static class LateWeaponCalibration
    {
        static readonly Dictionary<ItemType, int> damage = new Dictionary<ItemType, int>
        {
            // Mechanical-boss equipment, before the late-game reference set.
            [ItemType.HallowedRepeater] = 96,
            [ItemType.Megashark] = 46,
            [ItemType.ChlorophyteShotbow] = 50,
            // Post-Plantera, events and Fishron: mobile output rather than Shroomite stealth.
            [ItemType.Tsunami] = 85,
            [ItemType.TacticalShotgun] = 125,
            [ItemType.ChainGun] = 95,
            [ItemType.AerialBane] = 80,
            [ItemType.Eventide] = 85,
            [ItemType.VenusMagnum] = 110,
            [ItemType.SniperRifle] = 350,
            [ItemType.PulseBow] = 210,
            [ItemType.Xenopopper] = 80,
            [ItemType.PiranhaGun] = 90,
            [ItemType.ElfMelter] = 100,
            [ItemType.CandyCornRifle] = 160,
            [ItemType.StakeLauncher] = 150,
            // Pillars, followed by the Moon Lord gun. Retain their acquisition hierarchy.
            [ItemType.VortexBeater] = 110,
            [ItemType.Phantasm] = 100,
            [ItemType.SDMG] = 185,
            // Measured multi-hit outliers, without changing native attack behavior.
            [ItemType.SolarEruption] = 50,
            [ItemType.Meowmere] = 150,
            [ItemType.StarWrath] = 105,
        };

        internal static void Apply(ItemDef[] items)
        {
            foreach (var kv in damage)
            {
                var d = items[(int)kv.Key];
                if (d == null || d.Placeholder || !d.IsWeapon || d.Stage < 4 || WeaponCalibration.Measured(kv.Key)) continue;
                d.Damage = kv.Value;
            }
        }
    }
}
