using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The geo economy and where crafting happens. A geo is 100 copper internally; shop purchases
    /// use whole geo and stage budgets, while dropped items retain the older liquidation curve.
    /// Fractional income is kept as spare copper. Recipes are made as in Terraria, by hand or at
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
                foreach (var d in ItemDb.All) if (!string.IsNullOrEmpty(d.Name) && d.Name != d.Type.ToString() && !d.Placeholder && !d.Permanent) all.Add(d);
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
                // The Knight's own nails (the Nail, the Dream Nail) are no one's to make; nor is a place left in the item list
                // with nothing made of it (an item taken out, such as the God Tamer's seal: named by its enum, Item_1's
                // pickaxe for a picture, a basic by default at no price), as Everything leaves it out.
                foreach (var d in ItemDb.All) if (d.Source == ItemSource.Basic && !d.Permanent && !d.Placeholder) basics.Add(d);
                return basics;
            }
        }

        // Order in the testing column, roughly as Terraria's recipe list goes.
        static int Category(ItemDef d) =>
            d.PickPower > 0 ? 0 : d.Class == DamageClass.Melee ? 1 : d.Class == DamageClass.Ranged && !d.IsAmmo ? 2 : d.IsAmmo ? 3
            : d.Class == DamageClass.Magic ? 4 : d.Class == DamageClass.Summon ? 5 : d.Shoot != ProjKind.None ? 6 : d.Places != World.CellKind.Empty ? 7 : d.IsArmor ? 8
            : d.IsAccessory ? 9 : d.Material ? 11 : 10;

        /// <summary>Free and fully stocked in a creative save (and while testing with infinite items).</summary>
        public static bool Free => TerraMod.Instance != null && TerraMod.Instance.Inventory.FreeMaking;

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
        /// The number of distinct native movement abilities obtained, in any order:
        /// Mothwing Cloak, Mantis Claw, Crystal Heart, Monarch Wings and Shade Cloak.
        /// One new ability advances one stage. Who moves into Dirtmouth, what the townsfolk sell and
        /// which recipes open go by it; the enemies' strength and drops by where they live
        /// (World.Areas.LevelOf, laid out along the same road).
        /// </summary>
        public static int UnlockedStage()
        {
            if (DevStage >= 0) return DevStage;
            // Free (a creative save): the stage it has chosen in the inventory (CreativeStage), else all of them.
            if (Free) { int chosen = CreativeStage.Chosen; return chosen >= 0 ? chosen : World.Areas.MaxLevel; }
            var pd = PlayerData.instance;
            if (pd == null) return 0;
            return ProgressionBalance.AbilityStage(Player.PlayerHooks.LearnedAbility);
        }

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

        /// <summary>
        /// What a stack sells for, in copper: a fifth of the value apiece (Player.GetItemExpectedPrice); a forged weapon a
        /// fifth of its levels' prices more (Forging.SellBonus). Forged equipment's total return is capped at the
        /// minimum cost of reaching that level, so forging and selling cannot turn a profit, even with a discount.
        /// </summary>
        public static long SellValue(ItemStack s)
        {
            if (s.IsEmpty) return 0;
            if (HallownestRecovery.RelicValue(s.Type) > 0) return HallownestRecovery.RelicValue(s.Type) * s.Count;
            if (HallownestRecovery.IsUnique(s.Type)) return System.Math.Max(1L, HallownestRecovery.Price(s.Def) / 5) * s.Count;
            if (s.Def.Value <= 0 || Recipes.Worthless(s.Type)) return 0;
            long value = System.Math.Max(1L, (ShopEquipmentResale(s.Def) ? PriceOf(s.Def) : ResalePriceOf(s.Def)) / 5) + Forging.SellBonus(s);
            if (s.Forge > 0 && Forging.Forgeable(ItemDb.Get(s.Type)))
                value = System.Math.Min(value, Forging.PricesTo(s.Type, s.Forge, minimumPaid: true));
            return value * s.Count;
        }

        /// <summary>
        /// A price paid with the Discount Card worn: four fifths (Pl:35648-35657 for a shop, Main.DrawInventory for the
        /// Goblin's reforging); here the geo bought at a bench, the forging and the reforging too. Never under a copper.
        /// </summary>
        public static long Discounted(long price) => Discounted(price, Equipment.Current.Discount);
        internal static long Discounted(long price, bool discount) => price > 0 && discount ? System.Math.Max(1L, (long)(price * 0.8f)) : price;

        // Geo purchase budgets, independent from drop liquidation. Prices are in copper (100 = one geo).
        static readonly int[] WeaponPrices={1000,2500,6000,12000,20000,35000};
        static readonly int[] AccessoryPrices={500,1200,3000,6000,12000,20000};
        static readonly int[] ArmorPrices={200,500,1200,2500,5000,9000};
        public static long PriceOf(ItemDef d)
        {
            if(d==null)return 0;
            if(d.Type==ItemType.HomeEverywhere)return 500000;
            long previous=ResalePriceOf(d);
            // Native item 2203 is worth 100000 copper; the generated shop entry had zero.
            // Correct the purchase without minting resale income from that former zero-value item.
            if(d.Type==ItemType.SteampunkBoiler)previous=CompressedPrice(100000);
            // Explicitly requested prices, and Hallownest's separate fivefold native shop policy.
            if(d.Type==ItemType.CasinoKit||d.Type==ItemType.RandomPrefabHouse||d.Type==ItemType.SimplePrefabHouse||d.Type==ItemType.WaterCandle||d.HkItem)return previous;
            if(previous<=0)return 0;
            int stage=System.Math.Max(0,System.Math.Min(5,d.Stage));
            long price=previous*5;
            // Thrown by the piece and bought by the stack (shurikens, throwing knives, grenades): priced as ammunition is,
            // not under a weapon's floor (a shuriken of 15 copper cost 300 geo apiece).
            if(d.IsAmmo||SpentByThePiece(d))price=System.Math.Max(price,100L*(stage+1));
            // Stage alone must not price a transitional item like a final weapon. Retain its value curve.
            else if(ProgressionBalance.BalancedWeapon(d))price=System.Math.Max(price,
                ProgressionBalance.TimberWeapon(d.Type)?15000L:System.Math.Min(100L*WeaponPrices[stage],System.Math.Max(30000L,previous*3)));
            else if(d.IsAccessory)price=System.Math.Max(price,System.Math.Min(100L*AccessoryPrices[stage],System.Math.Max(20000L,previous*3)));
            else if(d.IsArmor)price=System.Math.Max(price,System.Math.Min(100L*ArmorPrices[stage],System.Math.Max(10000L,previous*3)));
            // Shop transactions use whole geo; bulk ammunition must not become a nearly free currency fraction.
            return System.Math.Max(100L,((price+99)/100)*100);
        }

        // A 20% trade-in only for original shop equipment without a crafting recipe. Materials,
        // consumables, drops and crafted equipment keep the previous liquidation curve.
        internal static bool ShopEquipmentResale(ItemDef d)
        {
            if(d.Source!=ItemSource.Shop || SpentByThePiece(d) || !(ProgressionBalance.BalancedWeapon(d)||d.IsArmor||d.IsAccessory))return false;
            foreach(var r in Recipes.All)if(!r.IsBought&&r.Result==d.Type)return false;
            return true;
        }

        /// <summary>A weapon used up a piece at a time and kept in stacks (thrown knives, shurikens, grenades, spiky balls).</summary>
        internal static bool SpentByThePiece(ItemDef d) => d != null && d.Consumable && d.MaxStack > 1 && d.Damage > 0;

        /// <summary>The previous liquidation curve for materials, crafted equipment and drops.</summary>
        internal static long ResalePriceOf(ItemDef d)
        {
            if(d==null)return 0;
            if(d.Type==ItemType.CasinoKit)return Casino.HousePrice;
            if(d.Type==ItemType.RandomPrefabHouse)return World.PrefabHouses.Price;
            if(d.Type==ItemType.SimplePrefabHouse)return World.PrefabHouses.SimplePrice;
            if(d.Type==ItemType.WaterCandle)return 300000;
            return CompressedPrice(d.Value);
        }
        static long CompressedPrice(long value)
        {
            if(value<=0)return 0;
            if(value<=100)return value;
            return (long)System.Math.Round(100.0*System.Math.Pow(value/100.0,0.8));
        }
    }
}
