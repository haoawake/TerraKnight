using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "shimmerbucket"): the Bottomless Shimmer Bucket. By the hot spring its recipe (an Empty Bucket, the
    /// shimmer near) may be made; in Dirtmouth it fills a dug pit with shimmer, which changes Tin Ore dropped in it as the
    /// spring does, which an Empty Bucket can't scoop, which the Knight stands in without sinking through the ground, and
    /// which no fishing finds.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ShimmerBucketTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 无底微光桶 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            var inv = mod.Inventory;

            // By the spring: the recipe and its place.
            var recipe = Recipes.All.FirstOrDefault(r => r.Result == ItemType.BottomlessShimmerBucket);
            Check("配方：一个空桶，附近有微光", recipe != null && recipe.Environment == CraftEnvironment.Shimmer
                && recipe.Ingredients.Length == 1 && recipe.Ingredients[0].Type == ItemType.EmptyBucket, recipe != null ? recipe.Station : "没有");
            yield return GoTo(Shimmer.Scene, EntryGateOf(Shimmer.Scene));
            yield return new WaitForSeconds(1f);
            var hero = HeroController.instance;
            var pool = Shimmer.Pool;
            if (pool.HasValue)
            {
                hero.transform.position = new Vector3(pool.Value.center.x, pool.Value.yMax + 1f, hero.transform.position.z);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.5f);
            }
            Check("温泉微光旁可以合成", CraftEnvironments.Available(CraftEnvironment.Shimmer), $"温泉 {pool}");
            Check("微光不能拆解无底微光桶", Shimmer.DecraftRecipe(ItemType.BottomlessShimmerBucket, 5) == null);

            // Dirtmouth: away from it, then a pit poured full.
            yield return GoTo("Town", EntryGateOf("Town"));
            yield return new WaitForSeconds(1.5f);
            hero = HeroController.instance;
            Check("德特茅斯远离微光时不能合成", !CraftEnvironments.Available(CraftEnvironment.Shimmer));
            var feet = CellGrid.WorldToCell((Vector2)hero.transform.position + Vector2.down * 0.6f);
            int ground = feet.y;
            for (int k = 0; k < 20 && !TerraWorld.IsSolid(feet.x + 6, ground); k++) ground--;
            int px0 = feet.x + 4, px1 = feet.x + 9, top = ground, bottom = ground - 2;
            for (int x = px0; x <= px1; x++)
                for (int y = bottom; y <= top; y++)
                    TerraWorld.Set(x, y, CellKind.Empty);
            yield return null;
            var bucket = ItemDb.Get(ItemType.BottomlessShimmerBucket);
            for (int pass = 0; pass < 3; pass++)
            {
                for (int x = px0; x <= px1; x++)
                    for (int y = bottom; y <= top; y++)
                        Buckets.Use(bucket, inv, hero, CellGrid.CellCenter(x, y));
                yield return new WaitForSeconds(0.6f);
            }
            int shimmerCells = 0, total = 0;
            for (int x = px0; x <= px1; x++)
                for (int y = bottom; y <= top; y++)
                    if (TerraWorld.LiquidAt(x, y) > 0 && TerraWorld.LiquidKindAt(x, y) == LiquidKind.Shimmer) { shimmerCells++; total += TerraWorld.LiquidAt(x, y); }
            Check("倒出微光填满坑", shimmerCells >= 12, $"坑 {px1 - px0 + 1}×{top - bottom + 1}，微光格 {shimmerCells}，共 {total}");
            Check("倒出的微光旁可以合成", CraftEnvironments.Available(CraftEnvironment.Shimmer));
            hero.transform.position = new Vector3(CellGrid.CellCenter(px0 - 3, top + 2).x, CellGrid.CellCenter(px0 - 3, top + 2).y, hero.transform.position.z);
            hero.FaceRight();
            yield return new WaitForSeconds(0.8f);
            Shot("Town", "shimmer_pool");

            // An Empty Bucket can't take it.
            var empty = ItemDb.Get(ItemType.EmptyBucket);
            int before = TerraWorld.LiquidAt(px0 + 2, bottom + 1);
            Buckets.Use(empty, inv, hero, CellGrid.CellCenter(px0 + 2, bottom + 1));
            yield return null;
            Check("空桶舀不起微光", TerraWorld.LiquidAt(px0 + 2, bottom + 1) == before && before > 0, $"舀之前 {before}，之后 {TerraWorld.LiquidAt(px0 + 2, bottom + 1)}");

            // What lies in it changes, as in the spring.
            var region = TerraWorld.RegionAt(px0, bottom);
            var drop = CellGrid.CellCenter((px0 + px1) / 2, bottom);
            Check("坑里算在微光里", Shimmer.In(drop) && !Shimmer.In(CellGrid.CellCenter(px0 - 3, top + 1)), $"{drop}");
            WorldItems.DevClear();
            WorldItems.DevNoGrab = true;
            int changed = WorldItems.DevShimmered;
            WorldItems.DropIn(ItemStack.Of(ItemType.TinOre, 9), region.SceneName, drop - (Vector2)region.Root.position);
            yield return new WaitForSeconds(3.5f);
            var tinTo = Shimmer.TransformOf(ItemType.TinOre, out _);
            Check("锡矿掉进倒出的微光后转化", WorldItems.DevCount(ItemType.TinOre) == 0 && WorldItems.DevCount(tinTo) == 9 && WorldItems.DevShimmered == changed + 1, WorldItems.DevDescribe());
            var item = WorldItems.DevItems.FirstOrDefault(i => i.Stack.Type == tinTo);
            Check("转化后浮起", item.Stack.Count == 9 && item.At.y > drop.y, item.At.ToString());
            WorldItems.SweepPickup(drop, 20f, region.SceneName);
            WorldItems.DevNoGrab = false;

            // The Knight in it: not through the ground.
            hero.transform.position = new Vector3(drop.x, CellGrid.CellCenter(px0, top + 3).y, hero.transform.position.z);
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            float lowest = hero.transform.position.y;
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                lowest = Mathf.Min(lowest, hero.transform.position.y);
                yield return null;
            }
            float floor = bottom * CellGrid.CellSize;
            Check("主角进微光不会穿地掉下去", lowest > floor - 0.3f && GameManager.instance.sceneName == "Town", $"最低 {lowest:F2}，坑底 {floor:F2}");
            Plugin.Log.LogInfo($"[AutoTest] 无底微光桶 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
