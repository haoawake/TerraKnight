using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>What a bucket or a sponge does to a cell's liquid.</summary>
    internal enum BucketUse : byte
    {
        None,
        /// <summary>Scoops a bucketful (Empty Bucket).</summary>
        Empty,
        Water,
        Acid,
        BottomlessWater,
        BottomlessAcid,
        /// <summary>Soaks up any amount of water (Super Absorbant Sponge).</summary>
        WaterSponge,
        /// <summary>Soaks up any amount of acid (Lava Absorbant Sponge).</summary>
        AcidSponge,
    }

    internal static partial class ItemDb
    {
        /// <summary>
        /// Terraria's buckets and sponges, acid in the place of lava: the Water and Acid Buckets (206, 207), their
        /// bottomless kinds (3031, 4820), the Super Absorbant Sponge (3032) and the acid one (4872). The Empty Bucket (205,
        /// worn as a helmet too) scoops; Obsidian (what water and acid make) is placed as a block.
        /// </summary>
        static void AddBuckets(List<ItemDef> list)
        {
            ItemDef B(ItemType t, string name, int id, BucketUse use, int stack, int rare, int value, int stage, ItemSource source, string tip) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Bucket = use, MaxStack = stack, Rare = rare, Tooltip = tip,
                UseAnimation = 15 * Tick, UseTime = 10 * Tick, AutoReuse = true, NoMelee = true, UseSound = null,
                Value = value, Price = Mathf.Max(1, Mathf.CeilToInt(value / 100f)), Stage = stage, Source = source,
            };
            list.Add(B(ItemType.WaterBucket, "水桶", 206, BucketUse.Water, 9999, 0, 0, 0, ItemSource.Craft, "包含少量水\n可被倒出"));
            list.Add(B(ItemType.AcidBucket, "酸水桶", 207, BucketUse.Acid, 9999, 0, 0, 0, ItemSource.Craft, "包含少量酸水\n可被倒出"));
            list.Add(B(ItemType.BottomlessWaterBucket, "无底水桶", 3031, BucketUse.BottomlessWater, 1, 7, 500000, 0, ItemSource.Craft, "包含无穷无尽的水\n可被倒出"));
            list.Add(B(ItemType.BottomlessAcidBucket, "无底酸水桶", 4820, BucketUse.BottomlessAcid, 1, 7, 500000, 0, ItemSource.Craft, "包含无穷无尽的酸水\n可被倒出"));
            list.Add(B(ItemType.SuperAbsorbantSponge, "超级吸水棉", 3032, BucketUse.WaterSponge, 1, 7, 500000, 2, ItemSource.Drop, "能吸收无穷无尽的水"));
            list.Add(B(ItemType.AcidAbsorbantSponge, "酸水吸收棉", 4872, BucketUse.AcidSponge, 1, 7, 500000, 1, ItemSource.Drop, "能吸收无穷无尽的酸水"));
            foreach (var d in list)
            {
                if (d.Type == ItemType.EmptyBucket)
                {
                    d.Bucket = BucketUse.Empty;
                    d.UseAnimation = 15 * Tick;
                    d.UseTime = 10 * Tick;
                    d.AutoReuse = true;
                    d.NoMelee = true;
                }
                else if (d.Type == ItemType.Obsidian && d.Places == CellKind.Empty)
                {
                    d.Places = CellKind.Obsidian;
                    d.UseAnimation = 15 * Tick;
                    d.UseTime = 10 * Tick;
                    d.AutoReuse = true;
                    d.Consumable = true;
                }
            }
        }
    }

    /// <summary>
    /// Player.ItemCheck_UseBuckets on the grid's liquid: within reach of the cell under the cursor, an Empty Bucket takes a
    /// bucketful where the 3×3 around holds more than 100 of one liquid (the cell's all and the rest from its neighbours, up
    /// to 255) and turns into a Water or Acid Bucket; a sponge does the same without the 100, its own liquid only, and
    /// stays; a full bucket pours 255 into a cell open to it (none of the other liquid, under 200) and gives the Empty
    /// Bucket back, the bottomless ones keep pouring. Hollow Knight's own pools fill an Empty Bucket too.
    /// </summary>
    internal static class Buckets
    {
        public static int DevScooped, DevPoured;

        /// <summary>The Bottomless Buckets made from an Empty Bucket by water and by acid (the game's pools or poured liquid).</summary>
        public static IEnumerable<Recipe> Recipes()
        {
            yield return new Recipe(ItemType.BottomlessWaterBucket, 1, "徒手", 0, (ItemType.EmptyBucket, 1)) { Environment = CraftEnvironment.Water };
            yield return new Recipe(ItemType.BottomlessAcidBucket, 1, "徒手", 0, (ItemType.EmptyBucket, 1)) { Environment = CraftEnvironment.Lava };
        }

        public static void Use(ItemDef def, Inventory inv, HeroController hero, Vector2 mouse)
        {
            if (def == null || inv == null || hero == null) return;
            var cell = CellGrid.WorldToCell(mouse);
            var bounds = hero.GetComponent<BoxCollider2D>().bounds;
            if (!TerraMod.InReach(bounds, def, cell)) return;
            var region = TerraWorld.RegionAt(cell.x, cell.y);
            if (region == null || BossRooms.Locked(cell.x, cell.y)) return;
            int x = cell.x - region.CellOffset.x, y = cell.y - region.CellOffset.y;
            switch (def.Bucket)
            {
                case BucketUse.Empty:
                case BucketUse.WaterSponge:
                case BucketUse.AcidSponge:
                    Take(def, inv, hero, region, x, y, mouse);
                    break;
                default:
                    Pour(def, inv, hero, region, x, y, mouse);
                    break;
            }
        }

        static void Take(ItemDef def, Inventory inv, HeroController hero, TerrainRegion region, int x, int y, Vector2 mouse)
        {
            var g = region.Grid;
            int here = g.GetLiquid(x, y);
            if (here <= 0)
            {
                // Hollow Knight's pools: a bucketful from them, the pool as it was.
                if (def.Bucket == BucketUse.Empty && Fishing.NativeLiquidAt(mouse, out bool nativeAcid))
                {
                    Swap(inv, hero, nativeAcid ? ItemType.AcidBucket : ItemType.WaterBucket);
                    TerraMod.Instance?.PlaySound("Splash_1");
                    DevScooped++;
                }
                return;
            }
            var kind = g.GetLiquidKind(x, y);
            if (def.Bucket == BucketUse.WaterSponge && kind != LiquidKind.Water) return;
            if (def.Bucket == BucketUse.AcidSponge && kind != LiquidKind.Acid) return;
            int around = 0;
            for (int i = x - 1; i <= x + 1; i++)
                for (int j = y - 1; j <= y + 1; j++)
                    if (g.GetLiquid(i, j) > 0 && g.GetLiquidKind(i, j) == kind) around += g.GetLiquid(i, j);
            if (def.Bucket == BucketUse.Empty)
            {
                if (around <= 100) return;
                Swap(inv, hero, kind == LiquidKind.Acid ? ItemType.AcidBucket : ItemType.WaterBucket);
            }
            TerraMod.Instance?.PlaySound("Splash_1");
            var sim = region.Liquid;
            int got = here;
            sim.Set(x, y, 0, kind);
            if (got < 255)
                for (int i = x - 1; i <= x + 1; i++)
                    for (int j = y - 1; j <= y + 1; j++)
                    {
                        if (i == x && j == y) continue;
                        int there = g.GetLiquid(i, j);
                        if (there <= 0 || g.GetLiquidKind(i, j) != kind) continue;
                        int take = Mathf.Min(there, 255 - got);
                        got += take;
                        sim.Set(i, j, there - take, kind);
                    }
            DevScooped++;
        }

        static void Pour(ItemDef def, Inventory inv, HeroController hero, TerrainRegion region, int x, int y, Vector2 mouse)
        {
            var g = region.Grid;
            if (!g.InClip(x, y) || Cells.Blocks(g.Get(x, y)) || g.GetLiquid(x, y) >= 200) return;
            var kind = def.Bucket == BucketUse.Acid || def.Bucket == BucketUse.BottomlessAcid ? LiquidKind.Acid : LiquidKind.Water;
            if (g.GetLiquid(x, y) > 0 && g.GetLiquidKind(x, y) != kind) return;
            // Hollow Knight's own pool already fills the place.
            if (Fishing.NativeLiquidAt(mouse, out _)) return;
            region.Liquid.Set(x, y, 255, kind);
            TerraMod.Instance?.PlaySound("Splash_1");
            if (def.Bucket == BucketUse.Water || def.Bucket == BucketUse.Acid) Swap(inv, hero, ItemType.EmptyBucket);
            DevPoured++;
        }

        /// <summary>Player.PutItemInInventoryFromItemUsage: one of the held item spent, the new one into its slot if that emptied, else the bag, else the ground.</summary>
        static void Swap(Inventory inv, HeroController hero, ItemType into)
        {
            if (inv.Infinite) return;
            bool mouse = inv.UsingMouse;
            int held = inv.Held;
            inv.ConsumeSelected();
            if (mouse && inv.Mouse.IsEmpty) { inv.Mouse = ItemStack.Of(into); return; }
            if (!mouse && inv.Slots[held].IsEmpty) { inv.Slots[held] = ItemStack.Of(into); return; }
            int left = inv.Pickup(into, 1);
            if (left > 0) WorldItems.Drop(ItemStack.Of(into, left), hero.transform.position);
        }
    }
}
