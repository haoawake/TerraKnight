using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>
    /// One Terraria recipe (Recipe.SetupRecipes, 1.4.5.8): the result and how many, the
    /// ingredients, and the nearby station it's made at. Only acquisition of materials and
    /// stations follows progression; possession permits crafting at any stage. A basic item
    /// is an entry with no ingredients and a geo price instead.
    /// </summary>
    internal sealed class Recipe
    {
        public readonly ItemType Result;
        public readonly int Count;
        public readonly string Station;
        /// <summary>Estimated acquisition stage for documentation and loot planning; never a crafting gate.</summary>
        public readonly int StationStage;
        public readonly (ItemType Type, int Count)[] Ingredients;
        /// <summary>A basic item's price in copper (a geo is 100), less with the Discount Card; 0 for a real recipe.</summary>
        public int Cost => (int)Shop.Discounted(listCost);
        readonly int listCost;
        public CraftEnvironment Environment;
        /// <summary>An expanded ingredient variant must not replace an existing recipe's shimmer fallback.</summary>
        internal bool IngredientAlternative;

        public Recipe(ItemType result, int count, string station, int stationStage, params (ItemType, int)[] ingredients)
        {
            Result = result;
            Count = count;
            Station = station;
            StationStage = stationStage;
            Ingredients = ingredients;
        }

        Recipe(ItemType result, int cost)
        {
            Result = result;
            Count = 1;
            Station = "";
            Ingredients = NoIngredients;
            listCost = cost;
        }

        static readonly (ItemType, int)[] NoIngredients = new (ItemType, int)[0];
        static readonly Dictionary<ItemType, Recipe> bought = new Dictionary<ItemType, Recipe>();

        /// <summary>
        /// A basic item bought for its value (or, while testing and in a creative game, anything for free): one entry kept
        /// for each item at its price, as the crafting column's list is made again every frame it shows.
        /// </summary>
        public static Recipe Bought(ItemDef d, bool free)
        {
            int cost = free ? 0 : (int)Shop.PriceOf(d);
            if (bought.TryGetValue(d.Type, out var r) && r.listCost == cost) return r;
            return bought[d.Type] = new Recipe(d.Type, cost);
        }

        public ItemDef Def => ItemDb.Get(Result);
        public bool IsBought => Ingredients.Length == 0;
    }

    internal static partial class Recipes
    {
        public static IReadOnlyList<Recipe> All => Every;

        static HashSet<ItemType> materials;

        /// <summary>Item.material: an ingredient of some recipe (what the Guide's slot takes).</summary>
        public static bool IsMaterial(ItemType type)
        {
            if (materials == null)
            {
                materials = new HashSet<ItemType>();
                foreach (var r in Every)
                    foreach (var (t, _) in r.Ingredients) materials.Add(t);
            }
            return materials.Contains(type);
        }

        static List<Recipe> every;

        /// <summary>The generated recipes and the stations' (ItemsStations.cs).</summary>
        static List<Recipe> Every
        {
            get
            {
                if (every != null) return every;
                every = BaseRecipes();
                ProgressionBalance.ApplyRecipes(every);
                return every;
            }
        }

        // No ItemDb lookups: usable while the item table establishes acquisition stages.
        internal static List<Recipe> BaseRecipes()
        {
            var result = new List<Recipe>(all);
            result.AddRange(StationRecipes());
            result.AddRange(OreRecipes());
            result.AddRange(ChainRecipes());
            result.AddRange(SupplementRecipes());
            result.AddRange(WallRecipes());
            result.AddRange(World.AreaBlocks.Recipes());
            result.AddRange(ItemDb.RegionVanityRecipes());
            result.AddRange(Buckets.Recipes());
            result.AddRange(World.RegionalFurniture.Recipes());
            result.AddRange(World.RegionalChests.Recipes());
            result.AddRange(FoodRecipes());
            result.AddRange(BossSummons.Recipes());
            result.AddRange(AbilityAccessories.Recipes());
            result.AddRange(CarriedFurniture.Recipes());
            result.AddRange(ItemDb.Batch1017Recipes());
            result.AddRange(ItemDb.ContainerRecipes());
            // Keep every supported vanilla ingredient alternative, without duplicating
            // recipes already restored by another registration source.
            foreach (var alternative in VanillaAlternativeRecipes())
                if (!System.Linq.Enumerable.Any(result, r => SameRecipe(r, alternative)))
                {
                    alternative.IngredientAlternative = System.Linq.Enumerable.Any(result, r => r.Result == alternative.Result);
                    result.Add(alternative);
                }
            return result;
        }

        static bool SameRecipe(Recipe a, Recipe b)
        {
            if (a.Result != b.Result || a.Count != b.Count || a.Station != b.Station
                || a.Environment != b.Environment || a.Ingredients.Length != b.Ingredients.Length) return false;
            foreach (var ingredient in a.Ingredients)
                if (!System.Linq.Enumerable.Contains(b.Ingredients, ingredient)) return false;
            return true;
        }

        static HashSet<ItemType> worthless;

        /// <summary>
        /// Worth nothing when sold: no value of its own, or made of nothing but such things (wood
        /// into a Work Bench, the Work Bench's wood into a Loom...), up through every recipe, so
        /// free materials can't be crafted into money. Free to begin with is what has no value and is
        /// had without crafting it; one with no value that is only ever crafted (an Iron Fence, four
        /// from an Iron Bar) costs what it is made of, and its recipe back (four fences into a bar)
        /// doesn't make the bar free.
        /// </summary>
        public static bool Worthless(ItemType t)
        {
            if (worthless == null)
            {
                worthless = new HashSet<ItemType>();
                var crafted = new HashSet<ItemType>();
                foreach (var r in Every) if (!r.IsBought) crafted.Add(r.Result);
                foreach (var d in ItemDb.All)
                    if (d != null && d.Type != ItemType.None && d.Value <= 0 && !(d.Source == ItemSource.Craft && crafted.Contains(d.Type))) worthless.Add(d.Type);
                for (bool more = true; more;)
                {
                    more = false;
                    foreach (var r in Every)
                    {
                        if (r.IsBought || worthless.Contains(r.Result)) continue;
                        bool free = true;
                        foreach (var (ing, _) in r.Ingredients) if (!worthless.Contains(ing)) { free = false; break; }
                        if (free) { worthless.Add(r.Result); more = true; }
                    }
                }
            }
            return worthless.Contains(t);
        }

        static Dictionary<ItemType, Recipe> byResult;

        /// <summary>The recipe that makes an item, if any.</summary>
        public static Recipe For(ItemType t)
        {
            if (byResult == null)
            {
                byResult = new Dictionary<ItemType, Recipe>();
                foreach (var r in Every) if (!byResult.ContainsKey(r.Result)) byResult[r.Result] = r;
            }
            return byResult.TryGetValue(t, out var rec) ? rec : null;
        }

        /// <summary>Recipes that use an item.</summary>
        public static IEnumerable<Recipe> Using(ItemType t)
        {
            foreach (var r in Every)
                foreach (var (ing, _) in r.Ingredients)
                    if (ing == t)
                    {
                        yield return r;
                        break;
                    }
        }

        /// <summary>How many of an item the inventory holds (every slot, as Recipe.FindRecipes counts).</summary>
        public static int Count(Inventory inv, ItemType t)
        {
            int n = 0;
            foreach (var s in inv.Slots) if (!s.IsEmpty && !s.WardrobeCopy && s.Type == t) n += s.Count;
            foreach (var s in inv.HkBag) if (!s.IsEmpty && !s.WardrobeCopy && s.Type == t) n += s.Count;
            return n;
        }

        /// <summary>How many of an item the inventory and the chests near the player hold together.</summary>
        public static int Count(Inventory inv, ItemType t, List<ItemStack[]> chests)
        {
            int n = Count(inv, t);
            if (chests != null)
                foreach (var slots in chests)
                    foreach (var s in slots) if (!s.IsEmpty && !s.WardrobeCopy && s.Type == t) n += s.Count;
            return n;
        }

        /// <summary>Whether the inventory, with the chests near the player, holds every ingredient (Recipe.FindRecipes: only those are offered).</summary>
        public static bool HasIngredients(Recipe r, Inventory inv, List<ItemStack[]> chests = null)
        {
            if (inv.FreeMaking) return true;
            if (r.IsBought && !ProgressionBalance.Unlocked(r.Result)) return false;
            foreach (var (t, n) in r.Ingredients)
                if (Count(inv, t, chests) < n) return false;
            return true;
        }

        /// <summary>
        /// Recipe.Create: the ingredients are taken out of the inventory, last slots first for the ones
        /// in the hotbar; what it lacks comes out of the chests near the player.
        /// </summary>
        public static void Consume(Recipe r, Inventory inv, List<ItemStack[]> chests = null)
        {
            HallownestRecovery.Observe(inv);
            if (inv.FreeMaking) return;
            List<(ItemStack[] Slots, ItemType Type, int Count)> fromChests = null;
            foreach (var (t, need) in r.Ingredients)
            {
                int left = Take(inv.Slots, t, need);
                if (left > 0) left = Take(inv.HkBag, t, left);
                if (chests != null)
                    foreach (var slots in chests)
                    {
                        if (left <= 0) break;
                        int before = left;
                        left = Take(slots, t, left);
                        if (left < before) (fromChests ?? (fromChests = new List<(ItemStack[], ItemType, int)>())).Add((slots, t, before - left));
                    }
            }
            // A LAN game: the host's chests give what was taken from the copies here.
            if (fromChests != null) World.Chests.NetCraftTook(fromChests, ItemStack.Of(r.Result, r.Count));
        }

        /// <summary>Up to that many of an item taken out of the slots, last first; how many are still wanted.</summary>
        static int Take(ItemStack[] slots, ItemType t, int left)
        {
            for (int i = slots.Length - 1; i >= 0 && left > 0; i--)
            {
                ref var s = ref slots[i];
                if (s.IsEmpty || s.WardrobeCopy || s.Type != t) continue;
                int take = UnityEngine.Mathf.Min(left, s.Count);
                s.Count -= take;
                left -= take;
                if (s.Count <= 0) s = default;
            }
            return left;
        }
    }
}
