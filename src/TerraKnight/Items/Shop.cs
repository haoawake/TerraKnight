using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The geo economy and where crafting happens. Prices are Terraria's own item values with a
    /// geo worth a silver coin (100 copper); what cheap things leave over is kept as spare copper,
    /// so a 7-copper musket ball costs 0.07 geo. Crafting (recipes, and basic items bought for
    /// geo) happens at a bench or anywhere in Dirtmouth. While testing (infinite items) the
    /// crafting column offers everything, free.
    /// </summary>
    internal static class Shop
    {
        static string benchScene;
        static readonly List<Transform> benches = new List<Transform>();
        static float nextBenchScan;
        static List<ItemDef> all, basics;

        /// <summary>Everything, in kind order as Terraria's recipe list goes (the testing column).</summary>
        public static IReadOnlyList<ItemDef> Everything
        {
            get
            {
                if (all != null) return all;
                all = new List<ItemDef>();
                foreach (var d in ItemDb.All) if (!string.IsNullOrEmpty(d.Name) && d.Name != d.Type.ToString()) all.Add(d);
                all.Sort((a, b) => Category(a) != Category(b) ? Category(a).CompareTo(Category(b)) : a.Stage != b.Stage ? a.Stage.CompareTo(b.Stage) : a.Type.CompareTo(b.Type));
                return all;
            }
        }

        /// <summary>The basic items (blocks, wood, torches, platforms, rope) bought at a bench for their value.</summary>
        public static IReadOnlyList<ItemDef> Basics
        {
            get
            {
                if (basics != null) return basics;
                basics = new List<ItemDef>();
                foreach (var d in ItemDb.All) if (d.Source == ItemSource.Basic) basics.Add(d);
                return basics;
            }
        }

        // Order in the testing column, roughly as Terraria's recipe list goes.
        static int Category(ItemDef d) =>
            d.PickPower > 0 ? 0 : d.Class == DamageClass.Melee ? 1 : d.Class == DamageClass.Ranged && !d.IsAmmo ? 2 : d.IsAmmo ? 3
            : d.Class == DamageClass.Magic ? 4 : d.Class == DamageClass.Summon ? 5 : d.Shoot != ProjKind.None ? 6 : d.Places != World.CellKind.Empty ? 7 : d.IsArmor ? 8
            : d.IsAccessory ? 9 : d.Material ? 11 : 10;

        /// <summary>Free and fully stocked while testing with infinite items.</summary>
        public static bool Free => TerraMod.Instance != null && TerraMod.Instance.Inventory.Infinite;

        /// <summary>Whether crafting is possible here: near a bench, or in Dirtmouth.</summary>
        public static bool Available(HeroController hero)
        {
            var gm = GameManager.UnsafeInstance;
            if (gm == null || hero == null) return false;
            if (gm.sceneName == "Town") return true;
            if (gm.sceneName != benchScene || Time.unscaledTime > nextBenchScan)
            {
                benchScene = gm.sceneName;
                nextBenchScan = Time.unscaledTime + 3f;
                benches.Clear();
                foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                    if (fsm.FsmName == "Bench Control") benches.Add(fsm.transform);
            }
            // The Artisan Loaf (usedArtisanLoaf): crafting stations reach 4 tiles further (here two units).
            float reach = TerraMod.Instance != null && TerraMod.Instance.Stats.Boosts.Contains(PermanentBoost.ArtisanLoaf) ? 10f : 8f;
            foreach (var b in benches)
                if (b != null && Vector2.Distance(b.position, hero.transform.position) < reach) return true;
            return false;
        }

        /// <summary>The deepest stage among the scenes the player has been to (World.Areas levels, 0-5).</summary>
        public static int UnlockedStage()
        {
            if (Free) return World.Areas.MaxLevel;
            var pd = PlayerData.instance;
            int stage = 0;
            if (pd?.scenesVisited != null)
                foreach (var s in pd.scenesVisited) stage = Mathf.Max(stage, World.Areas.LevelOf(s));
            return stage;
        }

        public static int Geo => PlayerData.instance != null ? PlayerData.instance.geo : 0;

        /// <summary>Copper held beyond whole geo (0-99).</summary>
        public static int SpareCopper;

        /// <summary>All the money, in copper.</summary>
        public static long Purse => Geo * 100L + SpareCopper;

        /// <summary>Pays a price in copper out of the geo (and spare copper); false when short.</summary>
        public static bool Spend(long copper)
        {
            if (copper <= 0) return true;
            if (Purse < copper) return false;
            SetPurse(Purse - copper);
            return true;
        }

        /// <summary>Takes money in, in copper.</summary>
        public static void Earn(long copper)
        {
            if (copper > 0) SetPurse(Purse + copper);
        }

        static void SetPurse(long copper)
        {
            int geo = (int)(copper / 100), had = Geo;
            SpareCopper = (int)(copper % 100);
            var hero = HeroController.instance;
            if (hero == null)
            {
                if (PlayerData.instance != null) PlayerData.instance.geo = geo;
                return;
            }
            if (geo > had) hero.AddGeo(geo - had);
            else if (geo < had) hero.TakeGeo(had - geo);
        }

        /// <summary>A price in copper as geo: "12 吉欧", "0.07 吉欧", "12.5 吉欧".</summary>
        public static string Money(long copper)
        {
            if (copper % 100 == 0) return Lang.F("{0} 吉欧", copper / 100);
            return Lang.F("{0} 吉欧", (copper / 100f).ToString(copper % 10 == 0 ? "0.0" : "0.00"));
        }

        /// <summary>What a stack sells for, in copper: a fifth of the value apiece (Player.GetItemExpectedPrice).</summary>
        public static long SellValue(ItemStack s) => s.IsEmpty || s.Def.Value <= 0 ? 0 : Mathf.Max(1, s.Def.Value / 5) * (long)s.Count;
    }
}
