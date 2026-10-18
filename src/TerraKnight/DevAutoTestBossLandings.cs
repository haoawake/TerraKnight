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
        IEnumerator BossLandingAudit(string only)
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            var results = new List<object>();
            var itemFeet = new Dictionary<string, Vector2>();
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            Shop.DevStage = 5;
            PlayerData.instance.hasDreamNail = true;
            int checks = 0, failures = 0;
            void Check(string label, bool pass)
            {
                checks++;
                if (!pass) { failures++; Plugin.Log.LogError($"[AutoTest] 首领落点 {label} 不通过"); }
            }
            var keys = only == null ? BossArena.DevKeys().ToArray() : only.Split('.');
            foreach (string key in keys)
            {
                Check("逐个策略覆盖/" + key, BossArrivalPoints.PolicyOf(key) != null);
                var seal = BossSummons.All.FirstOrDefault(s => s.Key == key);
                foreach (bool creative in seal == null ? new[] { true } : new[] { false, true })
                {
                    yield return SealTravel("Town", "left1");
                    if (!GameHooks.PlayerHasControl() && !GameManager.instance.IsInSceneTransition)
                    {
                        HeroController.instance.RegainControl();
                        HeroController.instance.StartAnimationControl();
                        yield return new WaitForSecondsRealtime(.2f);
                    }
                    mod.ApplyCreative(creative);
                    mod.Inventory.Infinite = creative;
                    Bestiary.Kills[key] = 1;
                    PlayerData.instance.isInvincible = true;
                    mod.Stats.FullHeal();
                    int placed = BossSummonStart.DevPlaced, started = BossSummonStart.DevStarted;
                    if (creative) BookSpawner.Spawn(key);
                    else
                    {
                        mod.Inventory.Mouse = default;
                        mod.Inventory.UseMouse = false;
                        mod.Inventory.Selected = 0;
                        mod.Inventory.Override = -1;
                        mod.Inventory.Slots[0] = ItemStack.Of(seal.Type, 3);
                        string reason = BossSummons.Refusal(seal);
                        Check("召唤可用/" + key, reason == null);
                        if (reason != null) continue;
                        mod.DevUseSelected(HeroController.instance.transform.position);
                    }
                    for (float until = Time.realtimeSinceStartup + 30f; !BossArena.Arrived(key) && Time.realtimeSinceStartup < until;) yield return null;
                    Check("到达/" + key + "/" + creative, BossArena.Arrived(key));
                    bool reviewed = BossArrivalPoints.TryFeet(key, out var expected);
                    if (reviewed)
                        for (float until = Time.realtimeSinceStartup + 15f; BossSummonStart.DevPlaced == placed && Time.realtimeSinceStartup < until;) yield return null;
                    else if (BossArrivalPoints.PolicyOf(key) == "native-challenge")
                        for (float until = Time.realtimeSinceStartup + 18f; BossSummonStart.DevStarted == started && Time.realtimeSinceStartup < until;) { mod.Stats.FullHeal(); yield return null; }
                    else yield return new WaitForSecondsRealtime(.3f);
                    var hero = HeroController.instance;
                    var bounds = hero.GetComponent<BoxCollider2D>().bounds;
                    Vector2 feet = new Vector2(bounds.center.x, bounds.min.y);
                    if (reviewed)
                    {
                        Check("执行定位/" + key + "/" + creative, BossSummonStart.DevPlaced > placed);
                        Check("正确战斗层/" + key + "/" + creative, Mathf.Abs(feet.y - expected.y) < 1f && Mathf.Abs(feet.x - expected.x) < 1f);
                    }
                    if (!creative) itemFeet[key] = feet;
                    else if (itemFeet.TryGetValue(key, out var item))
                        Check("图鉴与召唤物一致/" + key, Vector2.Distance(item, feet) < 1f);
                    if (!creative) Check("召唤物扣除一次/" + key, mod.Inventory.Slots[0].Count == 2);
                    results.Add(new { Key=key,Creative=creative,Policy=BossArrivalPoints.PolicyOf(key),Scene=GameManager.instance.sceneName,X=feet.x,Y=feet.y,ExpectedX=expected.x,ExpectedY=expected.y,Placed=BossSummonStart.DevPlaced-placed,Alive=!hero.cState.dead,Control=GameHooks.PlayerHasControl(),Start=BossSummonStart.DevLast });
                    Check("角色存活/" + key + "/" + creative,!hero.cState.dead);
                    File.WriteAllText(Path.Combine(DevCopy.SaveDir,"boss-landings-audit.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failures,Rows=results},Formatting.Indented));
                    Plugin.Log.LogInfo($"[AutoTest] 首领落点 {key} creative={creative} feet={feet:F2} policy={BossArrivalPoints.PolicyOf(key)}");
                }
            }
            yield return SealTravel("Town","left1");
            foreach (string scene in UI.WorldMap.DevSceneNames())
                if (scene.StartsWith("Room_")) Check("室内场景归层/"+scene, UI.WorldMap.DevDeep(scene));
            foreach (string scene in new[] {"Cliffs_01","Cliffs_02","Cliffs_03","Cliffs_04","Cliffs_05","Cliffs_06"})
            {
                yield return SealTravel(scene,EntryGateOf(scene));
                var centre = (Vector2)HeroController.instance.GetComponent<BoxCollider2D>().bounds.center - (Vector2)TerraWorld.Live.Root.position;
                bool interior=UI.WorldMap.DevDeepAt(scene,centre);
                Check("悬崖当前层/"+scene,UI.WorldMap.DevViewDeep==interior);
                Check("悬崖隔层隐藏/"+scene,UI.WorldMap.DevShown("Cliffs_03")==interior);
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"boss-landings-audit.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failures,Rows=results},Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 首领落点与地图合计 {checks}，失败 {failures}");
        }

        IEnumerator BossLandingSurvey(string only)
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            var results = new List<object>();
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var keys = only == null ? BossArena.DevKeys().ToArray() : only.Split('.');
            foreach (string key in keys)
            {
                yield return SealTravel("Town", "left1");
                if (!BossArena.Fight(key, false)) { Plugin.Log.LogError($"[AutoTest] 落点测绘 {key} 无法进入 不通过"); continue; }
                for (float until = Time.realtimeSinceStartup + 25f; !BossArena.Arrived(key) && Time.realtimeSinceStartup < until;) yield return null;
                yield return new WaitForSecondsRealtime(2f);
                string room = BossArena.SceneOf(key), record = key.Split('@')[0];
                bool InRoom(Component c) => c.gameObject.scene.name == room || c.gameObject.scene.name == room + "_boss";
                var parts = UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(h => InRoom(h) && UI.EnemyHover.RecordOf(h) == record).ToArray();
                var bodies = parts.Select(h => new { h.name, X=h.transform.position.x, Y=h.transform.position.y, Active=h.gameObject.activeInHierarchy }).ToArray();
                var landmarks = UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(f => InRoom(f) && (f.FsmName=="BG Control" || f.name.Contains("Battle") || f.name.Contains("Boss Control") || f.FsmName=="Challenge Start"))
                    .Select(f => {var c=f.GetComponent<Collider2D>(); return new { f.name, Fsm=f.FsmName, State=f.ActiveStateName, X=f.transform.position.x,Y=f.transform.position.y, Bounds=c==null ? "" : c.bounds.ToString() };}).ToArray();
                var locks = UnityEngine.Object.FindObjectsByType<CameraLockArea>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(InRoom).Select(c=>new {c.name,c.cameraXMin,c.cameraXMax,c.cameraYMin,c.cameraYMax}).ToArray();
                var floors = new List<object>();
                foreach (var h in parts.Take(3))
                    foreach (float dx in new[]{-9f,-7f,0f,7f,9f})
                    {
                        var start=(Vector2)h.transform.position + new Vector2(dx,20f);
                        var hits=Physics2D.RaycastAll(start,Vector2.down,250f,1<<ChunkColliders.TerrainLayer)
                            .Where(hit=>hit.collider!=null && !hit.collider.isTrigger && hit.normal.y>.5f && InRoom(hit.collider))
                            .Select(hit=>new {X=hit.point.x,Y=hit.point.y,Name=hit.collider.name}).ToArray();
                        floors.Add(new {X=start.x,Hits=hits});
                    }
                var hero=HeroController.instance;
                results.Add(new {Key=key,Room=room,Actual=GameManager.instance.sceneName,HeroX=hero.transform.position.x,HeroY=hero.transform.position.y,Bodies=bodies,Landmarks=landmarks,Locks=locks,Floors=floors});
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"boss-landings-survey.json"),JsonConvert.SerializeObject(results,Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] 落点测绘 {key} 场景 {GameManager.instance.sceneName} 首领 {parts.Length}");
            }
            yield return SealTravel("Town","left1");
        }
    }
}
