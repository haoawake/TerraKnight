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
        /// <summary>
        /// The progress reached, as Terraria's comes with its bosses: each stage opens with the last
        /// stage's final boss beaten, not on walking into its areas: the False Knight (King Slime)
        /// for 1, Greenpath's Hornet (the Eye of Cthulhu) for 2, the Soul Master (the Brain of
        /// Cthulhu) for 3, the Watcher Knights (the Wall of Flesh: hardmode) for 4, the Broken
        /// Vessel (Plantera) for 5. Who moves into Dirtmouth, what the townsfolk sell and which
        /// stations' recipes are open go by it; the enemies' strength and drops still go by where
        /// they live.
        /// </summary>
        public static int UnlockedStage()
        {
            if (Free) return World.Areas.MaxLevel;
            var pd = PlayerData.instance;
            if (pd == null) return 0;
            if (pd.killedInfectedKnight) return 5;
            if (WatcherKnightsBeaten()) return 4;
            if (pd.mageLordDefeated) return 3;
            if (pd.hornet1Defeated) return 2;
            if (pd.falseKnightDefeated) return 1;
            return 0;
        }

        /// <summary>The Watcher Knights' fight is won: its battle's own record in the scene's data (the game keeps no flag of it).</summary>
        static bool WatcherKnightsBeaten()
        {
            var sd = SceneData.instance;
            if (sd == null) return false;
            var state = sd.FindMyState(new PersistentBoolData { id = "Battle Control", sceneName = "Ruins2_03" });
            return state != null && state.activated;
        }

        /// <summary>The boss whose fall opens a stage, for the townsfolk's word on it.</summary>
        public static string GateBoss(int stage) =>
            stage == 1 ? "假骑士" : stage == 2 ? "大黄蜂（苍绿之径）" : stage == 3 ? "灵魂大师" : stage == 4 ? "守望者骑士" : stage == 5 ? "残破容器" : "";

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

        /// <summary>A creative save's geo: never short of it.</summary>
        public const int CreativeGeo = 99999999;

        /// <summary>
        /// A creative save keeps its geo at 99,999,999: whatever takes some (a toll, a bench) is made up
        /// again at once, and the shade left at a death carries none (its geo would come back on top).
        /// Set as it is, past the game's own cap (PlayerData.AddGeo stops at 9,999,999), and the
        /// counter in the corner shown it without counting up.
        /// </summary>
        public static void KeepCreativeGeo(HeroController hero)
        {
            var pd = PlayerData.instance;
            if (pd == null || hero == null) return;
            if (pd.geoPool != 0) pd.geoPool = 0;
            if (pd.geo == CreativeGeo) return;
            pd.geo = CreativeGeo;
            if (hero.geoCounter != null) hero.geoCounter.NewSceneRefresh();
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
