using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's shimmer in Hallownest: the Forgotten Crossroads' hot spring (Crossroads_30) shimmers, and so does what
    /// the Bottomless Shimmer Bucket pours (World.LiquidKind.Shimmer: it changes items alone, as the spring). Items lying in it
    /// come out changed as WorldItem.GetShimmered has them (WorldItemsShimmer): ItemID.Sets.ShimmerTransformToItem
    /// (ShimmerData), else taken apart by their recipe here, so what goes in comes back out. The player, and the spring's
    /// own healing, are the game's; the spring takes the shimmer's colours (Tick). Hallownest has no coins, slime crowns or
    /// critters to free. The locks (ShimmerTransforms): the post-Moon Lord transforms at stage 5, a recipe with bones in it
    /// (post-Skeletron) at stage 1, with Lihzahrd bricks (post-Golem) at stage 4.
    /// </summary>
    internal static partial class Shimmer
    {
        public const string Scene = "Crossroads_30";
        const int MoonLordStage = 5, SkeletronStage = 1, GolemStage = 4;
        static readonly ItemType lihzahrdBrick = System.Enum.TryParse("LihzahrdBrick", out ItemType t) ? t : ItemType.None;

        static Dictionary<ItemType, (ItemType To, bool Late)> transformOf;
        static HashSet<ItemType> noDecraftSet;
        static Dictionary<ItemType, (ItemType Type, int Count)[]> customOf;
        static Dictionary<ItemType, ItemType[]> decraftBySet;
        static readonly Dictionary<ItemType, Recipe> recipeOf = new Dictionary<ItemType, Recipe>();

        static void Build()
        {
            if (transformOf != null) return;
            transformOf = new Dictionary<ItemType, (ItemType, bool)>();
            foreach (var (from, to, late) in transforms) transformOf[from] = (to, late);
            noDecraftSet = new HashSet<ItemType>(noDecraft);
            customOf = new Dictionary<ItemType, (ItemType, int)[]>();
            foreach (var (item, results) in customResults) customOf[item] = results;
            decraftBySet = new Dictionary<ItemType, ItemType[]>();
            foreach (var (item, ingredients) in decraftBy) decraftBySet[item] = ingredients;
        }

        /// <summary>ShimmerTransforms.GetTransformToItem: what it turns into (None: nothing), and whether that waits for stage 5.</summary>
        public static ItemType TransformOf(ItemType type, out bool late)
        {
            Build();
            late = false;
            if (!transformOf.TryGetValue(type, out var to)) return ItemType.None;
            late = to.Late;
            return to.To;
        }

        /// <summary>
        /// Item.CanShimmer: a locked transform stops it altogether (IsItemTransformLocked); else something to turn into, or
        /// a recipe to come apart by with at least one batch of it there.
        /// </summary>
        public static bool CanShimmer(ItemStack s)
        {
            if (s.IsEmpty) return false;
            int stage = Shop.UnlockedStage();
            if (TransformOf(s.Type, out bool late) != ItemType.None) return !late || stage >= MoonLordStage;
            var r = DecraftRecipe(s.Type, stage);
            return r != null && s.Count >= Mathf.Max(1, r.Count);
        }

        /// <summary>
        /// ShimmerTransforms.GetDecraftingRecipeIndex and IsRecipeIndexDecraftLocked: the recipe it comes apart by, or null.
        /// Of several, Terraria's own (the last that may be taken apart; ShimmerData.decraftBy names its ingredients), else
        /// the last canonical one here (expanded ingredient alternatives do not override it); never one Terraria marks
        /// notDecraftable, nor a basic item bought for geo.
        /// </summary>
        public static Recipe DecraftRecipe(ItemType type, int stage)
        {
            Build();
            if (!recipeOf.TryGetValue(type, out var pick))
            {
                pick = null;
                if (!noDecraftSet.Contains(type))
                {
                    decraftBySet.TryGetValue(type, out var want);
                    foreach (var r in Recipes.All)
                    {
                        if (r.Result != type || r.IsBought) continue;
                        if (want != null && SameIngredients(r, want))
                        {
                            pick = r;
                            break;
                        }
                        if (!r.IngredientAlternative) pick = r;
                    }
                }
                recipeOf[type] = pick;
            }
            if (pick == null) return null;
            foreach (var (t, _) in pick.Ingredients)
            {
                if (t == ItemType.Bone && stage < SkeletronStage) return null;
                if (t == lihzahrdBrick && lihzahrdBrick != ItemType.None && stage < GolemStage) return null;
            }
            return pick;
        }

        static bool SameIngredients(Recipe r, ItemType[] want)
        {
            if (r.Ingredients.Length != want.Length) return false;
            foreach (var (t, _) in r.Ingredients)
                if (System.Array.IndexOf(want, t) < 0) return false;
            return true;
        }

        /// <summary>What one batch comes apart into: Recipe.customShimmerResults, else its ingredients.</summary>
        public static (ItemType Type, int Count)[] Results(Recipe r)
        {
            Build();
            return customOf.TryGetValue(r.Result, out var c) ? c : r.Ingredients;
        }

        /// <summary>Recipe.alchemy (made at a placed bottle): each unit of what comes back is lost at one in three.</summary>
        public static bool Alchemy(Recipe r) => r.Station == "玻璃瓶";

        // ------------------------------------------------------------------ the spring

        static World.TerrainRegion poolOf;
        static BoxCollider2D poolBox;

        /// <summary>The spring's water in the world (its Spa Region's box, where it is now: the seamless world moves rooms), while its room is loaded.</summary>
        public static Rect? Pool
        {
            get
            {
                var region = World.TerrainSystem.Find(Scene);
                if (region == null)
                {
                    poolOf = null;
                    return null;
                }
                if (region != poolOf || poolBox == null)
                {
                    poolOf = region;
                    poolBox = FindPool(region);
                }
                if (poolBox == null) return null;
                var t = poolBox.transform;
                Vector2 a = t.TransformPoint(poolBox.offset - poolBox.size / 2f), b = t.TransformPoint(poolBox.offset + poolBox.size / 2f);
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }
        }

        static BoxCollider2D FindPool(World.TerrainRegion region)
        {
            foreach (var root in region.Root.gameObject.scene.GetRootGameObjects())
                foreach (var box in root.GetComponentsInChildren<BoxCollider2D>(true))
                    if (box.name.StartsWith("Spa Region")) return box;
            return null;
        }

        /// <summary>A point in the shimmer: the spring's box and the unit under it down to its floor, or poured shimmer under its level.</summary>
        public static bool In(Vector2 p)
        {
            var r = Pool;
            if (r != null && p.x >= r.Value.xMin && p.x <= r.Value.xMax && p.y >= r.Value.yMin - 1f && p.y <= r.Value.yMax) return true;
            return PouredSurface(p, 0, out float top) && p.y <= top;
        }

        /// <summary>Just over the shimmer (within two tiles of its top): a shimmered item still rises there (UpdateShimmer).</summary>
        public static bool JustAbove(Vector2 p)
        {
            var r = Pool;
            const float reach = 2f * World.CellGrid.CellSize;
            if (r != null && p.x >= r.Value.xMin && p.x <= r.Value.xMax && p.y > r.Value.yMax && p.y - r.Value.yMax < reach) return true;
            return PouredSurface(p, 2, out float top) && p.y > top && p.y - top < reach;
        }

        /// <summary>
        /// Poured shimmer at the point's cell or in the open cells under it (down to <paramref name="below"/> of them): the
        /// top of the liquid in the first such cell (world units).
        /// </summary>
        static bool PouredSurface(Vector2 p, int below, out float top)
        {
            top = 0f;
            var c = World.CellGrid.WorldToCell(p);
            for (int k = 0; k <= below; k++)
            {
                int y = c.y - k;
                int a = World.TerraWorld.LiquidAt(c.x, y);
                if (a > 0)
                {
                    if (World.TerraWorld.LiquidKindAt(c.x, y) != World.LiquidKind.Shimmer) return false;
                    top = (y + a / 255f) * World.CellGrid.CellSize;
                    return true;
                }
                if (World.TerraWorld.IsSolid(c.x, y)) return false;
            }
            return false;
        }

        /// <summary>The shimmer within reach of the hero (the spring, or poured shimmer, 10 tiles across and 8 up and down), for its crafting.</summary>
        public static bool Near(HeroController hero)
        {
            if (hero == null) return false;
            Vector2 at = hero.transform.position;
            var r = Pool;
            const float cs = World.CellGrid.CellSize;
            if (r != null && at.x >= r.Value.xMin - 10f * cs && at.x <= r.Value.xMax + 10f * cs && at.y >= r.Value.yMin - 8f * cs && at.y <= r.Value.yMax + 8f * cs) return true;
            var c0 = World.CellGrid.WorldToCell(at);
            for (int dy = -8; dy <= 8; dy++)
                for (int dx = -10; dx <= 10; dx++)
                    if (World.TerraWorld.LiquidAt(c0.x + dx, c0.y + dy) > 0 && World.TerraWorld.LiquidKindAt(c0.x + dx, c0.y + dy) == World.LiquidKind.Shimmer) return true;
            return false;
        }

        /// <summary>The shimmer's room keeps its own ground: a cell of it (not one the player put there) is never dug or blown away.</summary>
        public static bool Protects(int gx, int gy)
        {
            var r = World.TerraWorld.RegionAt(gx, gy);
            return r != null && r.SceneName == Scene && r.Grid.Get(gx - r.CellOffset.x, gy - r.CellOffset.y) == World.CellKind.Original;
        }

        static float nextNotice;

        /// <summary>Said over a pickaxe's mark there, every two seconds at most.</summary>
        public static void Notice(Vector2 at)
        {
            if (Time.unscaledTime < nextNotice) return;
            nextNotice = Time.unscaledTime + 2f;
            UI.CombatText.Show(new Vector3(at.x, at.y + 0.4f, 0f), Lang.T("微光里的岩石挖不动"), UI.CombatText.Notice);
        }

        /// <summary>WorldItem.ShimmerEffect's four sparkle colours.</summary>
        public static readonly Color[] Colors =
        {
            new Color(1f, 1f, 210 / 255f), new Color(190 / 255f, 245 / 255f, 1f), new Color(1f, 150 / 255f, 1f), new Color(190 / 255f, 175 / 255f, 1f),
        };

        /// <summary>One of them as Terraria picks it (Main.rand.Next(6): the last one three times in six).</summary>
        public static Color SparkleColor()
        {
            int k = Random.Range(0, 6);
            return Colors[k < 3 ? k : 3];
        }

        /// <summary>A shimmer sparkle (dust 309) in a box of Terraria pixels.</summary>
        public static void Sparkle(Vector2 topLeftPx, int w, int h, float scale = 1f, float speed = 1f)
        {
            var pp = Effects.TerraParticles.Instance;
            if (pp == null) return;
            int d = pp.NewDust(topLeftPx, w, h, 309);
            pp[d].Scale *= scale;
            pp[d].Velocity *= speed;
            pp[d].Color = SparkleColor();
            pp[d].NoGravity = true;
        }
    }
}
