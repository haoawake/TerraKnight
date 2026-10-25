using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "dummy"): the Target Dummy as furniture (World/TargetDummy.cs). Dirtmouth's set down once (its flag), struck
    /// (its hits counted), mined with a pickaxe into its item and not set down again, the item set down again beside the
    /// Knight and struck there; the Merchant selling it from stage 0, his stock's lines at stages 0 and 5. Close-ups dummy_*.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DummyTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 训练假人 {what} {(pass ? "通过" : "不通过")}");
            }
            var inv = mod.Inventory;
            int Held(ItemType ty) { int n = 0; foreach (var s in inv.Slots) if (!s.IsEmpty && s.Type == ty) n += s.Count; return n; }
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            IEnumerator StandBy(Vector2 feet)
            {
                hero.transform.position = Clear(hero, new Vector2(feet.x - 1.4f, feet.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.5f);
            }
            IEnumerator Strike(Vector2 feet, System.Action<int> done)
            {
                int hits0 = TargetDummy.DevHits;
                for (int k = 0; k < 4; k++)
                {
                    mod.DevUse(ItemType.WoodenSword, feet + new Vector2(0f, 0.7f));
                    yield return new WaitForSeconds(0.4f);
                }
                done(TargetDummy.DevHits - hits0);
            }

            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt;
            Check(at.HasValue && TargetDummy.DevCount == 1 && mod.GrantedAbilities.Contains("dummy.town.v1"),
                $"德特茅斯有一个训练假人 {TargetDummy.DevCount}，在 {(at.HasValue ? at.Value.ToString("F1") : "无")}，记为已放 {mod.GrantedAbilities.Contains("dummy.town.v1")}");
            if (!at.HasValue) yield break;
            yield return StandBy(at.Value);
            int hits = 0;
            yield return Strike(at.Value, n => hits = n);
            Shot("Town", "dummy_struck");
            Check(hits > 0, $"木剑砍中 {hits} 下");

            // Mined.
            var pick = ItemDb.Get(ItemType.CopperPickaxe);
            int lying0 = WorldItems.DevCount(ItemType.TargetDummy), have0 = Held(ItemType.TargetDummy);
            var mouse = at.Value + new Vector2(0f, 0.7f);
            for (int k = 0; k < 10 && TargetDummy.DevCount > 0; k++)
            {
                Stations.TryMine(pick, mouse, hero);
                yield return null;
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);
            int got = WorldItems.DevCount(ItemType.TargetDummy) - lying0 + Held(ItemType.TargetDummy) - have0;
            Check(TargetDummy.DevCount == 0 && got == 1, $"用铜镐挖掉：假人剩 {TargetDummy.DevCount}，得到训练假人物品 {got}");
            yield return new WaitForSeconds(2.5f);
            Check(TargetDummy.DevCount == 0, $"挖掉后不再自己长出来 {TargetDummy.DevCount}");

            // Set down again beside the Knight.
            var spot = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center + new Vector2(2f, 0f);
            bool placed = Stations.TryPlace(StationKind.TargetDummy, hero, spot);
            for (float t = Time.realtimeSinceStartup + 3f; TargetDummy.DevCount == 0 && Time.realtimeSinceStartup < t;) yield return null;
            var again = TargetDummy.DevAt;
            Check(placed && again.HasValue, $"重新放下 {placed}，在 {(again.HasValue ? again.Value.ToString("F1") : "无")}");
            if (again.HasValue)
            {
                yield return StandBy(again.Value);
                yield return Strike(again.Value, n => hits = n);
                Shot("Town", "dummy_placed");
                Check(hits > 0, $"放下的假人也砍得中 {hits} 下");
            }

            // The Merchant's.
            int n0 = Town.TownNpcs.DevStockAt("Merchant", 0, ItemType.TargetDummy, out bool at0);
            int n5 = Town.TownNpcs.DevStockAt("Merchant", 5, ItemType.TargetDummy, out bool at5);
            Check(at0 && at5 && n5 <= Town.TownNpcs.ShopItems.Length, $"商人阶段 0 就卖训练假人 {at0}（阶段 0 共 {n0} 种，阶段 5 共 {n5} 种，商店最多 {Town.TownNpcs.ShopItems.Length} 格）");
            Plugin.Log.LogInfo($"[AutoTest] 训练假人 价格 {Shop.Money(Shop.PriceOf(ItemDb.Get(ItemType.TargetDummy)))}");
            Plugin.Log.LogInfo($"[AutoTest] 训练假人 结果 {ok}/{all}");
        }
    }
}
