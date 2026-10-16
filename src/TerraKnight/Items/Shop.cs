using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The geo economy and where crafting happens. Prices are Terraria's own item values with a
    /// geo worth a silver coin (100 copper); what cheap things leave over is kept as spare copper,
    /// so a 7-copper musket ball costs 0.07 geo. Recipes are made as in Terraria, by hand or at
    /// their station set down nearby (InventoryGui.CanMakeHere); the basic items bought for geo
    /// at a bench or anywhere in Dirtmouth. While testing (infinite items) the crafting column
    /// offers everything, free.
    /// </summary>
    internal static class Shop
    {
        static string benchScene;
        static int benchScenes;
        static readonly List<Transform> benches = new List<Transform>();
        static float nextBenchScan;

        /// <summary>Which scenes are loaded (their handles together).</summary>
        static int LoadedScenes()
        {
            int sig = 17;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                sig = sig * 31 + UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).handle;
            return sig;
        }

        static List<ItemDef> all, basics;

        /// <summary>Everything, in kind order as Terraria's recipe list goes (the testing column).</summary>
        public static IReadOnlyList<ItemDef> Everything
        {
            get
            {
                if (all != null) return all;
                all = new List<ItemDef>();
                foreach (var d in ItemDb.All) if (!string.IsNullOrEmpty(d.Name) && d.Name != d.Type.ToString() && !d.Permanent) all.Add(d);
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
                // The Knight's own nails (the Nail, the Dream Nail) are no one's to make.
                foreach (var d in ItemDb.All) if (d.Source == ItemSource.Basic && !d.Permanent) basics.Add(d);
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
            // Not from a scene still coming in (its bench not up yet).
            if (gm.IsInSceneTransition) return false;
            // A search of every state machine is a frame's worth of work, and the inventory asks every frame it is open: the
            // benches of the scenes loaded now, those not up yet too, looked for again when the loaded scenes change.
            int scenes = LoadedScenes();
            if (gm.sceneName != benchScene || scenes != benchScenes || Time.unscaledTime > nextBenchScan)
            {
                benchScene = gm.sceneName;
                benchScenes = scenes;
                benches.Clear();
                foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (fsm.FsmName == "Bench Control" && fsm.gameObject.scene.isLoaded) benches.Add(fsm.transform);
                nextBenchScan = Time.unscaledTime + 10f;
            }
            // The Artisan Loaf (usedArtisanLoaf): crafting stations reach 4 tiles further (here two units).
            float reach = TerraMod.Instance != null && TerraMod.Instance.Stats.Boosts.Contains(PermanentBoost.ArtisanLoaf) ? 10f : 8f;
            foreach (var b in benches)
                if (b != null && b.gameObject.activeInHierarchy && Vector2.Distance(b.position, hero.transform.position) < reach) return true;
            return false;
        }

        /// <summary>Dev: the progress as a test sets it (-1: the save's own).</summary>
        public static int DevStage = -1;

        /// <summary>
        /// The progress reached, by the Knight's own abilities, each opening the next stretch of
        /// Hallownest as Terraria's bosses open its next stretch: the Mothwing Cloak (Hornet's, the
        /// Eye of Cthulhu's shield) for 1, the Mantis Claw for 2, the Crystal Heart for 3, the
        /// Monarch Wings (the Broken Vessel's: the Wall of Flesh, hardmode) for 4, the Shade Cloak for
        /// 5; one stage for each of them got. Who moves into Dirtmouth, what the townsfolk sell and
        /// which recipes open go by it; the enemies' strength and drops by where they live
        /// (World.Areas.LevelOf, laid out along the same road).
        /// </summary>
        public static int UnlockedStage()
        {
            if (DevStage >= 0) return DevStage;
            if (Free) return World.Areas.MaxLevel;
            var pd = PlayerData.instance;
            if (pd == null) return 0;
            return ProgressionBalance.AbilityStage(pd.GetBool);
        }

        /// <summary>The abilities that open the stages 1 to 5, in the order the Knight gets them.</summary>
        static readonly string[] StageAbilities = { "hasDash", "hasWalljump", "hasSuperDash", "hasDoubleJump", "hasShadowDash" };

        /// <summary>The ability that opens a stage, for the townsfolk's word on it.</summary>
        public static string GateAbility(int stage) =>
            stage == 1 ? "蛾翼披风" : stage == 2 ? "螳螂爪" : stage == 3 ? "水晶之心" : stage == 4 ? "帝王之翼" : stage == 5 ? "暗影披风" : "";

        public static int Geo => PlayerData.instance != null ? PlayerData.instance.geo : 0;

        /// <summary>Copper held beyond whole geo (0-99).</summary>
        public static int SpareCopper;

        /// <summary>The money items stand for: a creative save keeps the game's geo as it always did.</summary>
        static bool GeoItems => TerraMod.Instance != null && !TerraMod.Instance.Creative;

        /// <summary>All the money, in copper: the Geo carried and in the Piggy Bank (GeoWallet), and the spare copper.</summary>
        public static long Purse => GeoItems
            ? (GeoWallet.Held(TerraMod.Instance.Inventory) + GeoWallet.Banked(TerraMod.Instance.Inventory)) * 100L + SpareCopper
            : Geo * 100L + SpareCopper;

        /// <summary>Pays a price in copper out of the geo (and spare copper); false when short.</summary>
        public static bool Spend(long copper)
        {
            if (copper <= 0) return true;
            if (Purse < copper) return false;
            if (!GeoItems)
            {
                SetPurse(Purse - copper);
                return true;
            }
            var inv = TerraMod.Instance.Inventory;
            long after = Purse - copper;
            GeoWallet.Pay(inv, GeoWallet.Held(inv) + GeoWallet.Banked(inv) - after / 100);
            SpareCopper = (int)(after % 100);
            return true;
        }

        /// <summary>Takes money in, in copper: whole geo into the bag as the item.</summary>
        public static void Earn(long copper)
        {
            if (copper <= 0) return;
            if (!GeoItems)
            {
                SetPurse(Purse + copper);
                return;
            }
            long total = SpareCopper + copper;
            GeoWallet.Give(TerraMod.Instance, total / 100);
            SpareCopper = (int)(total % 100);
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
        public static long SellValue(ItemStack s) => s.IsEmpty || s.Def.Value <= 0 || Recipes.Worthless(s.Type) ? 0 : System.Math.Max(1L, PriceOf(s.Def) / 5) * s.Count;

        /// <summary>
        /// What an item costs at the townsfolk's, in copper (a geo is 100): Terraria's value up to a
        /// silver, past it growing as the value's 0.8th power, so the dear end of Terraria's money
        /// (gold and platinum) stays within reach of Hallownest's geo and selling the drops of
        /// strong enemies doesn't pay out fortunes. A gold's worth costs 40 geo, ten gold 250, a
        /// platinum about 1600. Selling gives a fifth of it, as in Terraria.
        /// </summary>
        public static long PriceOf(ItemDef d)
        {
            if (d == null) return 0;
            // The Water Candle at the Merchant's: 3000 geo (the user's price).
            if (d.Type == ItemType.WaterCandle) return 300000;
            if (d.Value <= 0) return 0;
            if (d.Value <= 100) return d.Value;
            return (long)System.Math.Round(100.0 * System.Math.Pow(d.Value / 100.0, 0.8));
        }
    }
}
