using UnityEngine;
using static TerraKnight.Items.ItemType;

namespace TerraKnight.Items
{
    /// <summary>
    /// A new creative save starts kitted out for showing the mod off. Each of the four loadouts
    /// (F1-F4: melee, ranged, magic, summon) wears its class's lunar armour, its class's wings and
    /// the accessories that suit it best, with the Terraspark Boots in every one, and has a bag of
    /// its own (Inventory.BagPerLoadout): the class's best weapon first, its lunar pickaxe second,
    /// then the class's other weapons to show and the Lunar Hook; the ranged bag ends with the ammo
    /// they fire (luminite arrows and bullets, Rocket III: big blasts that leave the ground be,
    /// Fallen Stars), where ammo goes. Life, mana and every permanent upgrade are at their most,
    /// the Demon Heart's sixth slot open.
    /// </summary>
    internal static class ShowcaseKit
    {
        static readonly ItemType[][] armor =
        {
            new[] { SolarFlareHelmet, SolarFlareBreastplate, SolarFlareLeggings },
            new[] { VortexHelmet, VortexBreastplate, VortexLeggings },
            new[] { NebulaHelmet, NebulaBreastplate, NebulaLeggings },
            new[] { StardustHelmet, StardustBreastplate, StardustLeggings },
        };

        static readonly ItemType[][] accessories =
        {
            new[] { WingsSolar, TerrasparkBoots, FireGauntlet, BerserkerGlove, CelestialShell, WarriorEmblem },
            new[] { WingsVortex, TerrasparkBoots, ReconScope, StalkersQuiver, RangerEmblem, DestroyerEmblem },
            new[] { WingsNebula, TerrasparkBoots, CelestialEmblem, ManaCloak, SorcererEmblem, DestroyerEmblem },
            new[] { WingsStardust, TerrasparkBoots, PapyrusScarab, NecromanticScroll, PygmyNecklace, SummonerEmblem },
        };

        /// <summary>Each loadout's bag from its first slot: the best weapon, the pickaxe, the rest, the hook.</summary>
        static readonly ItemType[][] bags =
        {
            new[]
            {
                Zenith, SolarFlarePickaxe, StarWrath, Meowmere, Terrarian, DayBreak, SolarEruption, TerraBlade, InfluxWaver, PiercingStarlight,
                NorthPole, Flairon, TheHorsemansBlade, TrueExcalibur, TrueNightsEdge, Kraken, Keybrand, ChristmasTreeSword, ScourgeoftheCorruptor, VampireKnives,
                Gungnir, Seedler, BeeKeeper, LunarHook,
            },
            new[]
            {
                SDMG, VortexPickaxe, CelebrationMk2, Phantasm, VortexBeater, Tsunami, Xenopopper, ChainGun, ElectrosphereLauncher, SnowmanCannon,
                SuperStarCannon, Eventide, SniperRifle, Megashark, TacticalShotgun, Gatligator, ChlorophyteShotbow, DaedalusStormbow, PulseBow, LunarHook,
            },
            new[]
            {
                LastPrism, NebulaPickaxe, LunarFlareBook, NebulaBlaze, NebulaArcanum, RazorbladeTyphoon, ChargedBlasterCannon, LaserMachinegun, BetsysWrath, SparkleGuitar,
                Nightglow, MagnetSphere, SpectreStaff, InfernoFork, ShadowbeamStaff, BlizzardStaff, Razorpine, BatScepter, StaffofEarth, HeatRay,
                LeafBlower, LunarHook,
            },
            new[]
            {
                Terraprisma, StardustPickaxe, StardustDragonStaff, Kaleidoscope, MoonLordWhip, StardustCellStaff, RainbowCrystalStaff, LunarPortalStaff, XenoStaff, TempestStaff,
                DeadlySphereStaff, RavenStaff, DarkHarvest, MorningStar, PygmyStaff, BladeStaff, DesertTigerStaff, LunarHook,
            },
        };

        /// <summary>The ranged bag's ammo, from its last slot back.</summary>
        static readonly (ItemType Type, int Count)[] ammo =
        {
            (LuminiteBullet, 999), (LuminiteArrow, 999), (RocketIII, 999), (FallenStar, 999),
        };

        /// <summary>A loadout's showcase bag (0 melee ... 3 summon) into the given slots, emptied first.</summary>
        public static void FillBag(ItemStack[] slots, int loadout)
        {
            for (int i = 0; i < slots.Length; i++) slots[i] = default;
            var items = bags[loadout];
            for (int i = 0; i < items.Length && i < slots.Length; i++) slots[i] = Stack(items[i]);
            if (loadout == 1)
                for (int i = 0; i < ammo.Length; i++)
                    slots[i < Inventory.AmmoSlots ? Inventory.Size + i : Inventory.Size - 1 - (i - Inventory.AmmoSlots)] = Stack(ammo[i].Type, ammo[i].Count);
        }

        public static int DevMissing;

        static ItemStack Stack(ItemType t, int count = 1)
        {
            var def = ItemDb.Get(t);
            if (def == null || def.Type != t)
            {
                DevMissing++;
                Plugin.Log.LogWarning($"[创造] 展示装备里的 {t} 不存在，跳过");
                return default;
            }
            return ItemStack.Of(t, Mathf.Min(count, Mathf.Max(1, def.MaxStack)));
        }

        public static void Give(TerraMod mod)
        {
            DevMissing = 0;
            var inv = mod.Inventory;
            var stats = mod.Stats;
            // Every permanent upgrade first: the Demon Heart's slot decides what can be worn.
            stats.LifeCrystals = Player.TerraStats.MaxLifeCrystals;
            stats.ManaCrystals = Player.TerraStats.MaxManaCrystals;
            stats.LifeFruits = Player.TerraStats.MaxLifeFruits;
            foreach (PermanentBoost b in System.Enum.GetValues(typeof(PermanentBoost)))
                if (b != PermanentBoost.None && b != PermanentBoost.LifeFruit) stats.Boosts.Add(b);

            inv.Clear();
            // The melee set is worn; the other three wait in their loadouts.
            inv.CurrentLoadout = 0;
            for (int l = 0; l < Inventory.LoadoutCount; l++)
            {
                var wornArmor = l == 0 ? inv.Armor : inv.Loadouts[l].Armor;
                var wornAcc = l == 0 ? inv.Accessories : inv.Loadouts[l].Accessories;
                // Seven accessory slots (the Demon Heart's included): the kit's six, the last left empty.
                for (int i = 0; i < Inventory.ArmorSlots; i++) wornArmor[i] = i < armor[l].Length ? Stack(armor[l][i]) : default;
                for (int i = 0; i < Inventory.AccessorySlots; i++) wornAcc[i] = i < accessories[l].Length ? Stack(accessories[l][i]) : default;
            }
            // The melee bag is out; the other three wait with their loadouts.
            if (!inv.BagPerLoadout) Plugin.Log.LogWarning("[创造] 展示装备：这个存档只有一个背包，只放近战的");
            FillBag(inv.Slots, 0);
            if (inv.BagPerLoadout)
                for (int l = 1; l < Inventory.LoadoutCount; l++)
                {
                    FillBag(inv.Loadouts[l].Bag, l);
                    inv.Loadouts[l].Selected = 0;
                }
            inv.Selected = 0;
            inv.RefreshEffective();
            stats.UpdateMaximums(PlayerData.instance, 0);
            stats.FullHeal();
            Plugin.Log.LogInfo($"[创造] 发了展示装备：四套配装和四个背包（{bags[0].Length}/{bags[1].Length}/{bags[2].Length}/{bags[3].Length} 件，远程另有 {ammo.Length} 种弹药），生命 {stats.LifeMax} 魔力 {stats.ManaMax}{(DevMissing > 0 ? $"，缺 {DevMissing} 件" : "")}");
        }
    }
}
