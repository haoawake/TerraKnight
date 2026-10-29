using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator WatcherPylonTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int ok = 0, all = 0;
            void Check(string text, bool pass) { all++; if (pass) ok++; Plugin.Log.LogInfo($"[AutoTest] 守望晶塔 {text}：{(pass ? "通过" : "不通过")}"); }
            List<HealthManager> Knights() => UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(h => h != null && h.gameObject.scene.name == "Ruins2_03_boss" && h.name.StartsWith("Black Knight ")).ToList();
            int Kills() => Bestiary.Kills.TryGetValue("BlackKnight", out int n) ? n : 0;
            IEnumerator Travel(Pylons.Pylon p)
            {
                float until = Time.time + 15f;
                while ((GameManager.instance.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.time < until) yield return null;
                Pylons.Teleport(p); yield return new WaitForSeconds(1f); until = Time.time + 25f;
                while ((GameManager.instance.sceneName != p.Scene || GameManager.instance.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.time < until) yield return null;
                yield return new WaitForSeconds(2f);
            }
            PlayerData.instance.isInvincible = true; Player.TerraStats.DevImmortal = true; mod.DevNoFallDamage = true;
            Player.TerraControls.DevNoRealKeys = true; mod.ApplyCreative(false); Shop.DevStage = 5;
            Bestiary.Kills.Remove("BlackKnight"); PlayerData.instance.SetBool("killedBlackKnight", false);
            BossArena.DevUnbeaten("BlackKnight");
            Pylons.Pylon placed = null;
            try
            {
                TransitionPoint.lastEntered = "top1";
                yield return GoTo("Ruins2_03", "top1"); yield return new WaitForSeconds(2f);
                Check("正常入口加载六名首领", Knights().Count == 6);
                foreach (var loader in UnityEngine.Object.FindObjectsByType<SceneAdditiveLoadConditional>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (loader.gameObject.scene.name == "Ruins2_03") Plugin.Log.LogInfo($"[AutoTest] 守望晶塔 原生载入 {loader.sceneNameToLoad} 入口{loader.doorTrigger} 持久状态{loader.persistentBoolData?.id} bool{loader.needsPlayerDataBool}={loader.playerDataBoolValue}");
                var hero = HeroController.instance; var region = TerrainSystem.Find("Ruins2_03");
                var origin = (Vector2)region.Root.position;
                hero.transform.position = Clear(hero, origin + new Vector2(27f, 69.1f));
                yield return new WaitForSeconds(.5f);
                var before = new HashSet<Pylons.Pylon>(Pylons.Unlocked);
                foreach (float dx in new[] { 2.2f, -2.2f, 3.2f, -3.2f, 1.6f, -1.6f })
                {
                    var bounds = hero.GetComponent<BoxCollider2D>().bounds;
                    if (Pylons.TryPlace(ItemDb.Get(ItemType.PylonUniversal), hero, new Vector2(bounds.center.x + dx, bounds.min.y + .3f))) break;
                    yield return null;
                }
                placed = Pylons.Unlocked.FirstOrDefault(p => !before.Contains(p));
                Check("电梯与战斗区之间实际放置晶塔", placed != null && placed.Scene == "Ruins2_03");
                if (placed == null) yield break;
                Check("新晶塔有有效入口", !string.IsNullOrEmpty(placed.Entry));
                placed.Entry = "bot1"; // An existing save's arbitrary gate, unchanged by the repair.
                yield return GoTo("Town", "left1"); yield return new WaitForSeconds(1f);
                TransitionPoint.lastEntered = "left1";
                Pylons.DevLegacyWatcherEntry = true;
                yield return Travel(placed);
                Check("旧路径复现房间在而首领子场景缺失", GameManager.instance.sceneName == placed.Scene && !USM.GetSceneByName("Ruins2_03_boss").isLoaded && Knights().Count == 0);
                Check("消失没有算作击败", Kills() == 0 && !PlayerData.instance.GetBool("killedBlackKnight"));
                Shot("Ruins2_03", "watcher_pylon_legacy_empty"); yield return new WaitForSeconds(.2f);
                yield return GoTo("Town", "left1"); yield return new WaitForSeconds(1f);
                Pylons.DevLegacyWatcherEntry = false;
                int arrivals = Pylons.DevArrivals;
                yield return Travel(placed);
                Check("修复兼容旧晶塔并加载六名首领", Knights().Count == 6 && USM.GetSceneByName("Ruins2_03_boss").isLoaded);
                Check("落点仍是玩家晶塔而不是房间入口", Pylons.DevArrivals > arrivals && Vector2.Distance(HeroController.instance.transform.position, (Vector2)TerrainSystem.Find(placed.Scene).Root.TransformPoint(placed.Local)) < 8f);
                Check("恢复战斗没有赠送击败", Kills() == 0 && !PlayerData.instance.GetBool("killedBlackKnight"));
                Check("旧晶塔的保存入口无需改档", placed.Entry == "bot1");
                // Step into the native battle trigger; no fabricated win or replacement boss.
                var controller = UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(f => f.gameObject.scene.name == "Ruins2_03_boss" && f.FsmName == "Battle Control");
                var trigger = controller != null ? controller.GetComponent<Collider2D>() : null;
                Check("原生战斗控制和触发器存在", controller != null && trigger != null);
                if (trigger != null) hero.transform.position = Clear(hero, new Vector2(trigger.bounds.center.x, trigger.bounds.min.y + 1f));
                float awakeBy = Time.time + 12f;
                while (Time.time < awakeBy && !Knights().Any(h => h.gameObject.activeInHierarchy && h.hp > 0 && !h.IsInvincible)) yield return new WaitForSeconds(.2f);
                Check("能通过原生触发开始战斗", controller != null && controller.ActiveStateName != "Idle" && Knights().Any(h => h.gameObject.activeInHierarchy && h.hp > 0 && !h.IsInvincible));
                Shot("Ruins2_03", "watcher_pylon_restored_battle"); yield return new WaitForSeconds(.2f);
                float end = Time.time + 75f;
                while (Time.time < end && Kills() == 0)
                {
                    foreach (var knight in Knights())
                        if (knight.gameObject.activeInHierarchy && !knight.isDead && knight.hp > 0 && !knight.IsInvincible)
                            knight.Hit(new HitInstance { Source = hero.gameObject, DamageDealt = 1000000, AttackType = AttackTypes.Nail, Direction = 0f, Multiplier = 1f, IgnoreInvulnerable = false });
                    yield return new WaitForSeconds(.4f);
                }
                yield return new WaitForSeconds(3f);
                Check("六名首领实际死亡后图鉴结算一次", Kills() == 1);
                Check("原生胜利状态正常写入", PlayerData.instance.GetBool("killedBlackKnight"));
                yield return GoTo("Town", "left1"); yield return new WaitForSeconds(1f);
                yield return Travel(placed);
                Check("已击败后晶塔不会复活首领", Knights().Count == 0 && Kills() == 1);
                Check("已击败后传送仍可正常控制", GameHooks.PlayerHasControl() && !PlayerData.instance.disablePause);
            }
            finally { Pylons.DevLegacyWatcherEntry = false; Player.TerraControls.DevNoRealKeys = false; Shop.DevStage = -1; }
            Plugin.Log.LogInfo($"[AutoTest] 守望晶塔 合计 {ok}/{all}");
        }
    }
}
