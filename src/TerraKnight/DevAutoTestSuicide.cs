using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TerraKnight.Items;
using TerraKnight.World;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "suicide"): enemies reported dying by themselves, perhaps as the player crosses
    /// into the next scene. The player stands still a while, then crosses a gate line back and
    /// forth; every enemy death not the player's is logged by EnemyDeathLog, and the living
    /// enemies of every loaded scene are counted before and after. Then the Life Crystals: where
    /// this save's lie, one knocked out with the pickaxe, and the crystal a new mask gives.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SuicideTest()
        {
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            var hero = HeroController.instance;
            var world = LiveWorld.Instance;
            foreach (var room in new[] { "Crossroads_01", "Crossroads_07", "Fungus1_02" })
            {
                yield return GoTo(room, EntryGateOf(room));
                yield return new WaitForSeconds(2f);
                int before = EnemyDeathLog.DevOthers;
                string alive0 = Alive();
                yield return new WaitForSeconds(15f);
                Plugin.Log.LogInfo($"[AutoTest] 自杀 {room} 站着不动 15 秒：不是玩家打死的 {EnemyDeathLog.DevOthers - before} 只；活着的敌人 {alive0} -> {Alive()}");

                var side = GlobalEnums.GatePosition.right;
                if (!world.TryGetNeighbour(side, out _, out _, out _, out _)) side = GlobalEnums.GatePosition.left;
                for (int trip = 1; trip <= 3; trip++)
                {
                    foreach (int way in new[] { 1, -1 })
                    {
                        var s = way == 1 ? side : Opposite(side);
                        float wait = Time.time + 20f;
                        while (!world.TryGetNeighbour(s, out _, out _, out _, out _) && Time.time < wait) yield return null;
                        if (!world.TryGetNeighbour(s, out var next, out _, out var gate, out _))
                        {
                            Plugin.Log.LogInfo($"[AutoTest] 自杀 {GameManager.instance.sceneName} 的 {s} 边没有接好的场景，跳过");
                            break;
                        }
                        int dir = s == GlobalEnums.GatePosition.right ? 1 : -1;
                        before = EnemyDeathLog.DevOthers;
                        string from = GameManager.instance.sceneName;
                        alive0 = Alive();
                        yield return StandInsideGate(hero, gate, dir);
                        yield return Cross(hero, from, dir, 0, $"suicide_{room}_{trip}_{(way == 1 ? "go" : "back")}");
                        yield return new WaitForSeconds(4f);
                        Plugin.Log.LogInfo($"[AutoTest] 自杀 越界 {from} -> {GameManager.instance.sceneName}（第 {trip} 趟）：不是玩家打死的 {EnemyDeathLog.DevOthers - before} 只{(EnemyDeathLog.DevOthers > before ? "，最后一只 " + EnemyDeathLog.DevLast : "")}；活着的敌人 {alive0} -> {Alive()}");
                    }
                }
            }
            Plugin.Log.LogInfo($"[AutoTest] 自杀 合计不是玩家打死的 {EnemyDeathLog.DevOthers} 只{(EnemyDeathLog.DevOthers == 0 ? "（没抓到）" : "")}");
            yield return CrystalTest();
        }

        static GlobalEnums.GatePosition Opposite(GlobalEnums.GatePosition s) =>
            s == GlobalEnums.GatePosition.right ? GlobalEnums.GatePosition.left : s == GlobalEnums.GatePosition.left ? GlobalEnums.GatePosition.right : s;

        /// <summary>Living enemies of each loaded scene.</summary>
        static string Alive()
        {
            var by = new SortedDictionary<string, int>();
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy) continue;
                string s = hm.gameObject.scene.name;
                by[s] = by.TryGetValue(s, out int n) ? n + 1 : 1;
            }
            var sb = new System.Text.StringBuilder();
            foreach (var kv in by) sb.Append($"{kv.Key}={kv.Value} ");
            return sb.ToString().TrimEnd();
        }

        /// <summary>How many of an item the bag holds.</summary>
        int Owned(ItemType type)
        {
            int n = 0;
            foreach (var s in mod.Inventory.Slots) if (!s.IsEmpty && s.Type == type) n += s.Count;
            return n;
        }

        IEnumerator CrystalTest()
        {
            var hero = HeroController.instance;
            Plugin.Log.LogInfo($"[AutoTest] 水晶 这个存档的生命水晶（种子 {LifeCrystals.Seed}）：{LifeCrystals.DevDescribe()}");
            // The nearest one: the first of the list in the Crossroads, else the first.
            string scene = null;
            foreach (var s in LifeCrystals.DevScenes()) if (s.StartsWith("Crossroads_") && EntryGateOf(s) != null) { scene = s; break; }
            if (scene == null) foreach (var s in LifeCrystals.DevScenes()) if (EntryGateOf(s) != null) { scene = s; break; }
            if (scene == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 水晶 没有能去的水晶场景，不通过");
                yield break;
            }
            yield return GoTo(scene, EntryGateOf(scene));
            float wait = Time.time + 5f;
            while (LifeCrystals.DevBox(scene) == null && Time.time < wait) yield return null;
            var box = LifeCrystals.DevBox(scene);
            Plugin.Log.LogInfo($"[AutoTest] 水晶 进了 {scene}：{LifeCrystals.DevDescribe()}");
            if (box == null)
            {
                Plugin.Log.LogInfo($"[AutoTest] 水晶 {scene} 里没放出水晶，不通过");
                yield break;
            }
            var b = box.Value;
            hero.transform.position = Clear(hero, new Vector2(b.xMax + 0.8f, b.yMin + 0.6f));
            yield return new WaitForSeconds(0.6f);
            Player.TerraCamera.DevLook = b.center;
            yield return new WaitForSeconds(0.3f);
            Shot(scene, "crystal_1_standing");
            yield return null;
            Player.TerraCamera.DevLook = null;

            // The pickaxe out, swung at it until it breaks.
            var inv = mod.Inventory;
            for (int i = 0; i < Inventory.HotbarSize; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Def.PickPower > 0) { inv.Selected = i; break; }
            int crystalsBefore = Owned(ItemType.LifeCrystal), broken0 = LifeCrystals.DevBroken;
            wait = Time.time + 8f;
            while (LifeCrystals.DevBroken == broken0 && Time.time < wait)
            {
                if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(b.center, false, true);
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);
            int got = Owned(ItemType.LifeCrystal) - crystalsBefore;
            Plugin.Log.LogInfo($"[AutoTest] 水晶 用镐子挖：{(LifeCrystals.DevBroken > broken0 ? "挖下来了" : "没挖下来")}，背包里多了 {got} 个生命水晶{(LifeCrystals.DevBroken > broken0 && got == 1 ? " 通过" : " 不通过")}");
            Shot(scene, "crystal_2_broken");
            yield return null;

            // A new mask: one more crystal.
            var pd = PlayerData.instance;
            crystalsBefore = Owned(ItemType.LifeCrystal);
            int masks = pd.maxHealthBase;
            pd.maxHealthBase = masks + 1;
            yield return new WaitForSeconds(1.5f);
            int fromMask = Owned(ItemType.LifeCrystal) - crystalsBefore;
            yield return new WaitForSeconds(1.2f);
            int again = Owned(ItemType.LifeCrystal) - crystalsBefore;
            Plugin.Log.LogInfo($"[AutoTest] 水晶 面具 {masks} -> {masks + 1}：给了 {fromMask} 个生命水晶，再等一会儿共 {again} 个{(fromMask == 1 && again == 1 ? " 通过" : " 不通过")}");
            pd.maxHealthBase = masks;
        }
    }
}
