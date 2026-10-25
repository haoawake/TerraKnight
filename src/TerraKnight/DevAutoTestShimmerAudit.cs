using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator ShimmerAudit()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Independent save required");
            var checks = new List<object>();
            int passed = 0, total = 0;
            void Check(string name, bool ok, string detail = "")
            {
                total++; if (ok) passed++;
                checks.Add(new {Name=name, Passed=ok, Detail=detail});
                Plugin.Log.LogInfo($"[AutoTest] 微光 {name} {(ok ? "通过" : "不通过")} {detail}");
            }
            int oldStage = Shop.DevStage;
            bool oldGrab = WorldItems.DevNoGrab;
            var table = new List<object>();
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true; mod.DevNoFallDamage = true;
            try
            {
                for (int stage = 0; stage <= 5; stage++)
                {
                    Shop.DevStage = stage;
                    foreach (var d in ItemDb.All)
                    {
                        if (d.Type == ItemType.None) continue;
                        var to = Shimmer.TransformOf(d.Type, out bool late);
                        var r = Shimmer.DecraftRecipe(d.Type, stage);
                        bool valid = to == ItemType.None || ItemDb.Get(to) != null;
                        if (r != null) valid &= r.Count > 0 && Shimmer.Results(r).All(p => p.Type != ItemType.None && p.Count > 0 && ItemDb.Get(p.Type) != null);
                        if (r != null && stage < 1) valid &= !r.Ingredients.Any(p => p.Type == ItemType.Bone);
                        bool can = Shimmer.CanShimmer(ItemStack.Of(d.Type, Math.Max(1, d.MaxStack)));
                        if (to != ItemType.None) valid &= can == (!late || stage >= 5);
                        if (late && stage < 5) valid &= !can;
                        Check($"规则 {stage}/{d.Type}", valid);
                        if (to != ItemType.None || r != null) table.Add(new {Stage=stage, Item=d.Type.ToString(), To=to.ToString(), Late=late, Can=can, RecipeCount=r?.Count, Results=r == null ? null : Shimmer.Results(r).Select(p => new {Item=p.Type.ToString(), p.Count}).ToArray()});
                    }
                    yield return null;
                }
                Check("空物品不能转化", !Shimmer.CanShimmer(default));
                foreach (var type in new[] {ItemType.Torch, ItemType.WorkBench, ItemType.Furnace, ItemType.Chest})
                {
                    var canonical = Shimmer.DecraftRecipe(type, 5);
                    Check("替代木材配方不覆盖原拆解：" + type, canonical != null && canonical.Ingredients.Any(p => p.Type == ItemType.Wood) && !canonical.Ingredients.Any(p => p.Type == ItemType.AshWood));
                }
                Check("替代木材的合成配方仍然注册", Recipes.All.Any(r => r.Result == ItemType.Torch && r.Ingredients.Any(p => p.Type == ItemType.AshWood)) && Recipes.All.Any(r => r.Result == ItemType.Torch && r.Ingredients.Any(p => p.Type == ItemType.SpookyWood)));
                Check("阶段配方复制保留替代标记", Recipes.All.Where(r => r.Result == ItemType.Torch && r.Ingredients.Any(p => p.Type == ItemType.AshWood)).All(r => r.IngredientAlternative));
                yield return GoTo(Shimmer.Scene, EntryGateOf(Shimmer.Scene));
                yield return new WaitForSeconds(1f);
                hero = HeroController.instance;
                var pool = Shimmer.Pool;
                Check("温泉微光区域存在", GameManager.instance.sceneName == Shimmer.Scene && pool.HasValue, pool.ToString());
                if (!pool.HasValue) yield break;
                var region = TerrainSystem.Find(Shimmer.Scene);
                Vector2 drop = new Vector2(pool.Value.center.x, pool.Value.yMax - 0.3f);
                Vector2 local = drop - (Vector2)region.Root.position;
                void Put(ItemType type, int count)
                {
                    WorldItems.DevClear(); WorldItems.DevNoGrab = true;
                    WorldItems.DropIn(ItemStack.Of(type, count), Shimmer.Scene, local);
                }
                Check("微光内外判定", Shimmer.In(drop) && !Shimmer.In(drop + Vector2.right * (pool.Value.width + 2f)));
                Check("表面上方浮起范围", Shimmer.JustAbove(new Vector2(drop.x, pool.Value.yMax + 0.2f)) && !Shimmer.JustAbove(drop));
                Shop.DevStage = 0;
                WorldItems.DevClear(); WorldItems.DevNoGrab = true;
                hero.transform.position = new Vector3(drop.x - 2f, pool.Value.yMax + 0.2f, hero.transform.position.z);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero; hero.FaceRight();
                WorldItems.Throw(ItemStack.Of(ItemType.TinOre, 3), hero);
                yield return new WaitForSeconds(4.5f);
                Check("正常抛出物品可以落入微光并转化", WorldItems.DevCount(ItemType.CopperOre) == 3 && WorldItems.DevCount(ItemType.TinOre) == 0, WorldItems.DevDescribe());
                Put(ItemType.TinOre, 9);
                int changed = WorldItems.DevShimmered;
                yield return new WaitForSeconds(0.7f);
                Check("浸泡不足时间不立即转化", WorldItems.DevCount(ItemType.TinOre) == 9 && WorldItems.DevShimmered == changed, WorldItems.DevDescribe());
                yield return new WaitForSeconds(2f);
                var tinTo = Shimmer.TransformOf(ItemType.TinOre, out _);
                Check("实际矿石转化与数量守恒", WorldItems.DevCount(ItemType.TinOre) == 0 && WorldItems.DevCount(tinTo) == 9 && WorldItems.DevShimmered == changed + 1, WorldItems.DevDescribe());
                var item = WorldItems.DevItems.FirstOrDefault(i => i.Stack.Type == tinTo);
                Check("转化结果会上浮", item.Stack.Count == 9 && item.At.y > drop.y, item.At.ToString());
                yield return new WaitForSeconds(3f);
                Check("产物不在池内自动连续转化", WorldItems.DevCount(tinTo) == 9 && WorldItems.DevShimmered == changed + 1, WorldItems.DevDescribe());
                int before = Recipes.Count(mod.Inventory, tinTo);
                WorldItems.SweepPickup(drop, 20f, Shimmer.Scene);
                Check("转化结果可以拾取入包", Recipes.Count(mod.Inventory, tinTo) == before + 9 && WorldItems.DevCount(tinTo) == 0);
                Put(ItemType.ClimbingClaws, 1);
                changed = WorldItems.DevShimmered;
                yield return new WaitForSeconds(4f);
                var clawsTo = Shimmer.TransformOf(ItemType.ClimbingClaws, out _);
                Check("饰品互换只触发一次", WorldItems.DevCount(clawsTo) == 1 && WorldItems.DevShimmered == changed + 1, WorldItems.DevDescribe());
                Put(ItemType.Torch, 7);
                var torch = Shimmer.DecraftRecipe(ItemType.Torch, 0);
                yield return new WaitForSeconds(3f);
                bool torchOk = torch != null && WorldItems.DevCount(ItemType.Torch) == 7 % torch.Count;
                if (torch != null) foreach (var p in Shimmer.Results(torch)) torchOk &= WorldItems.DevCount(p.Type) == 7 / torch.Count * p.Count;
                Check("拆解整批并保留余数", torchOk, WorldItems.DevDescribe());
                Check("火把实际返回普通木材而非灰烬木", WorldItems.DevCount(ItemType.Wood) == 2 && WorldItems.DevCount(ItemType.AshWood) == 0);
                Put(ItemType.Torch, 1);
                changed = WorldItems.DevShimmered;
                yield return new WaitForSeconds(3f);
                Check("不足一批不吞物品", WorldItems.DevCount(ItemType.Torch) == 1 && WorldItems.DevShimmered == changed, WorldItems.DevDescribe());
                Shop.DevStage = 4;
                Put(ItemType.RodofDiscord, 1);
                changed = WorldItems.DevShimmered;
                yield return new WaitForSeconds(3f);
                Check("混沌传送杖阶段5前锁定", WorldItems.DevCount(ItemType.RodofDiscord) == 1 && WorldItems.DevShimmered == changed);
                Shop.DevStage = 5;
                yield return new WaitForSeconds(3f);
                Check("达到阶段5后转成和谐传送杖", WorldItems.DevCount(ItemType.RodOfHarmony) == 1 && WorldItems.DevCount(ItemType.RodofDiscord) == 0, WorldItems.DevDescribe());
                Put(ItemType.CopperPickaxe, 1);
                yield return new WaitForSeconds(3f);
                Check("铜镐使用专用拆解结果", WorldItems.DevCount(ItemType.CopperOre) == 1 && WorldItems.DevCount(ItemType.Wood) == 1 && WorldItems.DevCount(ItemType.CopperPickaxe) == 0, WorldItems.DevDescribe());
                var alchemy = Recipes.All.FirstOrDefault(r => Shimmer.Alchemy(r) && !r.IsBought && Shimmer.TransformOf(r.Result, out _) == ItemType.None && ReferenceEquals(Shimmer.DecraftRecipe(r.Result, 5), r));
                Check("存在可拆解的炼金配方", alchemy != null);
                if (alchemy != null)
                {
                    int batches = Math.Max(1, Math.Min(30, ItemDb.Get(alchemy.Result).MaxStack / alchemy.Count));
                    Put(alchemy.Result, alchemy.Count * batches);
                    yield return new WaitForSeconds(3f);
                    var outputs = Shimmer.Results(alchemy);
                    bool limited = WorldItems.DevCount(alchemy.Result) == 0 && WorldItems.DevItems.All(i => outputs.Any(p => p.Type == i.Stack.Type));
                    foreach (var p in outputs) limited &= WorldItems.DevCount(p.Type) <= p.Count * batches;
                    Check("炼金拆解产量不超过投入且没有整批残留", limited, WorldItems.DevDescribe());
                }
                Put(ItemType.DreamNail, 1);
                changed = WorldItems.DevShimmered;
                yield return new WaitForSeconds(3f);
                Check("不支持的任务物品不会被吞", WorldItems.DevCount(ItemType.DreamNail) == 1 && WorldItems.DevShimmered == changed);
                WorldItems.DevClear();
                WorldItems.NetApply(new Net.WorldSync.Record {Key="shimmer-audit-shared", Scene=Shimmer.Scene, X=local.x, Y=local.y, Kind=(int)ItemType.TinOre, Seed=9, Owner=-1});
                changed = WorldItems.DevShimmered;
                yield return new WaitForSeconds(3f);
                Check("共享物品转化被现有入口禁用（功能限制）", WorldItems.DevCount(ItemType.TinOre) == 9 && WorldItems.DevShimmered == changed, "NetId!=null is excluded; this is not a two-client test");
            }
            finally
            {
                WorldItems.DevClear(); WorldItems.DevNoGrab = oldGrab; Shop.DevStage = oldStage;
                File.WriteAllText(Path.Combine(DevCopy.SaveDir, "shimmer-audit.json"), JsonConvert.SerializeObject(new {Passed=passed, Total=total, RecipeCount=Recipes.All.Count, Checks=checks, Rules=table, SharedDropConversionSupported=false, TwoClientTestRun=false}, Formatting.Indented));
            }
            Plugin.Log.LogInfo($"[AutoTest] 微光结果 {passed}/{total}");
        }
    }
}
