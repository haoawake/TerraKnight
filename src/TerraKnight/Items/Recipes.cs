using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>
    /// One Terraria recipe (Recipe.SetupRecipes, 1.4.5.8): the result and how many, the
    /// ingredients, and the station it's made at. Here every station is a bench or Dirtmouth; a
    /// station only exists from its stage on (an anvil at once, the Tinkerer's Workshop and the
    /// Mythril Anvil with the stages Terraria gets them in). A basic item (blocks, torches) is an
    /// entry with no ingredients and a geo price instead.
    /// </summary>
    internal sealed class Recipe
    {
        public readonly ItemType Result;
        public readonly int Count;
        public readonly string Station;
        public readonly int StationStage;
        public readonly (ItemType Type, int Count)[] Ingredients;
        /// <summary>A basic item's price in copper (a geo is 100); 0 for a real recipe.</summary>
        public readonly int Cost;

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
            Ingredients = new (ItemType, int)[0];
            Cost = cost;
        }

        /// <summary>A basic item bought for its value (or, while testing, anything for free).</summary>
        public static Recipe Bought(ItemDef d, bool free) => new Recipe(d.Type, free ? 0 : (int)Shop.PriceOf(d));

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
                every = new List<Recipe>(all);
                every.AddRange(StationRecipes());
                every.AddRange(OreRecipes());
                every.AddRange(ChainRecipes());
                every.AddRange(BossSummons.Recipes());
                return every;
            }
        }

        static HashSet<ItemType> worthless;

        /// <summary>
        /// Worth nothing when sold: no value of its own, or made of nothing but such things (wood
        /// into a Work Bench, the Work Bench's wood into a Loom...), up through every recipe, so
        /// free materials can't be crafted into money.
        /// </summary>
        public static bool Worthless(ItemType t)
        {
            if (worthless == null)
            {
                worthless = new HashSet<ItemType>();
                foreach (var d in ItemDb.All) if (d != null && d.Type != ItemType.None && d.Value <= 0) worthless.Add(d.Type);
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
            foreach (var s in inv.Slots) if (!s.IsEmpty && s.Type == t) n += s.Count;
            return n;
        }

        /// <summary>How many of an item the inventory and the chests near the player hold together.</summary>
        public static int Count(Inventory inv, ItemType t, List<ItemStack[]> chests)
        {
            int n = Count(inv, t);
            if (chests != null)
                foreach (var slots in chests)
                    foreach (var s in slots) if (!s.IsEmpty && s.Type == t) n += s.Count;
            return n;
        }

        /// <summary>Whether the inventory, with the chests near the player, holds every ingredient (Recipe.FindRecipes: only those are offered).</summary>
        public static bool HasIngredients(Recipe r, Inventory inv, List<ItemStack[]> chests = null)
        {
            if (inv.Infinite) return true;
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
            if (inv.Infinite) return;
            foreach (var (t, need) in r.Ingredients)
            {
                int left = Take(inv.Slots, t, need);
                if (chests != null)
                    foreach (var slots in chests)
                    {
                        if (left <= 0) break;
                        left = Take(slots, t, left);
                    }
            }
        }

        /// <summary>Up to that many of an item taken out of the slots, last first; how many are still wanted.</summary>
        static int Take(ItemStack[] slots, ItemType t, int left)
        {
            for (int i = slots.Length - 1; i >= 0 && left > 0; i--)
            {
                ref var s = ref slots[i];
                if (s.IsEmpty || s.Type != t) continue;
                int take = UnityEngine.Mathf.Min(left, s.Count);
                s.Count -= take;
                left -= take;
                if (s.Count <= 0) s = default;
            }
            return left;
        }
    }
}
