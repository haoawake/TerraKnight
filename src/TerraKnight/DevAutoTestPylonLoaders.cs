using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        // Complete SceneAdditiveLoadConditional catalogue from the native scene data.
        static readonly string[] portalLoaderRooms = {
            "Crossroads_10", "Ruins1_24", "Ruins2_03", "Ruins2_11", "Fungus1_04", "Fungus2_15", "Fungus3_23", "Fungus3_40",
            "Fungus3_archive_02", "Cliffs_02", "RestingGrounds_02", "Mines_18", "Deepnest_East_Hornet", "Waterways_05", "Waterways_12", "Grimm_Main_Tent"
        };
        sealed class PortalLoaderSample
        {
            public string Room, Owner, Main, Alternate, Door, Expected;
            public bool Active, Restored;
        }
        static string NativePortalChoice(SceneAdditiveLoadConditional c)
        {
            var gm = GameManager.instance;
            bool blocked = !string.IsNullOrEmpty(c.needsPlayerDataBool) && gm.GetPlayerDataBool(c.needsPlayerDataBool) != c.playerDataBoolValue;
            bool skip = (bool)AccessTools.Field(typeof(SceneAdditiveLoadConditional), "skipExtraTests").GetValue(c);
            if (!blocked && !skip && c.extraBoolTests != null)
                foreach (var b in c.extraBoolTests) if (gm.GetPlayerDataBool(b.playerDataBool) != b.value) blocked = true;
            if (!blocked && !string.IsNullOrEmpty(c.needsPlayerDataInt)) blocked = (gm.GetPlayerDataInt(c.needsPlayerDataInt) == c.playerDataIntValue) != c.isIntValue;
            if (!blocked && !skip && c.extraIntTests != null && c.extraIntTests.Length > 1)
                foreach (var t in c.extraIntTests)
                {
                    int a = gm.GetPlayerDataInt(t.playerDataInt), b = string.IsNullOrEmpty(t.otherPlayerDataInt) ? t.value : gm.GetPlayerDataInt(t.otherPlayerDataInt);
                    bool pass = t.testType == SceneAdditiveLoadConditional.IntTest.TestType.Equal ? a == b
                        : t.testType == SceneAdditiveLoadConditional.IntTest.TestType.NotEqual ? a != b
                        : t.testType == SceneAdditiveLoadConditional.IntTest.TestType.Less ? a < b
                        : t.testType == SceneAdditiveLoadConditional.IntTest.TestType.More ? a > b
                        : t.testType == SceneAdditiveLoadConditional.IntTest.TestType.LessOrEqual ? a <= b : a >= b;
                    if (!pass) blocked = true;
                }
            if (!blocked && c.usePersistentBoolItem && c.persistentBoolData != null)
                blocked = SceneData.instance.FindMyState(c.persistentBoolData)?.activated == true;
            return blocked ? c.altSceneNameToLoad : c.sceneNameToLoad;
        }
        static IEnumerable<SceneAdditiveLoadConditional> SceneLoaders(Scene scene) => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SceneAdditiveLoadConditional>(true));

        IEnumerator PylonLoadersTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int passed = 0, total = 0, rooms = 0, guarded = 0, benchRoutes = 0;
            var evidence = new List<object>();
            void Check(string name, bool pass) { total++; if (pass) passed++; Plugin.Log.LogInfo($"[AutoTest] 全图晶塔 {name}：{(pass ? "通过" : "不通过")}"); }
            PlayerData.instance.isInvincible = true; Player.TerraStats.DevImmortal = true; mod.DevNoFallDamage = true;
            mod.ApplyCreative(false); Player.TerraControls.DevNoRealKeys = true;
            var samples = new List<PortalLoaderSample>(); string target = "";
            var originalDoors = new Dictionary<string, string>();
            void Loaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != target) return;
                foreach (var c in SceneLoaders(scene))
                {
                    string owner = PathOf(c.transform);
                    samples.Add(new PortalLoaderSample { Room = scene.name, Owner = owner, Main = c.sceneNameToLoad,
                        Alternate = c.altSceneNameToLoad, Door = c.doorTrigger, Active = c.isActiveAndEnabled,
                        Expected = c.isActiveAndEnabled ? NativePortalChoice(c) : "",
                        Restored = originalDoors.TryGetValue(owner, out string door) && door == c.doorTrigger });
                }
            }
            IEnumerator Arrival(Pylons.Pylon p, string tag)
            {
                target = p.Scene; samples.Clear();
                int before = Pylons.DevArrivals;
                TransitionPoint.lastEntered = "unrelated_previous_room";
                Pylons.Teleport(p);
                yield return new WaitForSeconds(.5f);
                float until = Time.time + 30f;
                while ((GameManager.instance.sceneName != p.Scene || GameManager.instance.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.time < until) yield return null;
                yield return new WaitForSeconds(2f);
                Check(p.Scene + tag + "/到达可操作", GameManager.instance.sceneName == p.Scene && GameHooks.PlayerHasControl() && Pylons.DevArrivals == before + 1);
                var destination = TerrainSystem.Find(p.Scene);
                Check(p.Scene + tag + "/落点保持在晶塔旁", destination != null && Vector2.Distance(HeroController.instance.transform.position, destination.Root.TransformPoint(p.Local)) < 8f);
                Check(p.Scene + tag + "/入口字段临时豁免后恢复", samples.Count > 0 && samples.All(s => s.Restored));
                foreach (var s in samples.Where(s => s.Active && !string.IsNullOrEmpty(s.Main)))
                {
                    bool main = USM.GetSceneByName(s.Main).isLoaded;
                    bool alt = !string.IsNullOrEmpty(s.Alternate) && USM.GetSceneByName(s.Alternate).isLoaded;
                    bool expected = s.Expected == s.Main ? main && !alt
                        : string.IsNullOrEmpty(s.Expected) ? !main && !alt : alt && !main;
                    Check(p.Scene + tag + "/原生条件选择 " + s.Main, expected);
                    if (!expected) Plugin.Log.LogInfo($"[AutoTest] 全图晶塔 条件详情 {s.Room}/{s.Owner} 预期={s.Expected} 主场景={main} 另一场景={alt}({s.Alternate}) 入口={s.Door}");
                    evidence.Add(new { Tag = tag, Sample = s, MainLoaded = main, AlternateLoaded = alt, Passed = expected });
                }
                target = "";
            }
            USM.sceneLoaded += Loaded;
            try
            {
                foreach (string room in portalLoaderRooms)
                {
                    target = ""; originalDoors.Clear();
                    yield return GoTo(room, EntryGateOf(room));
                    var scene = USM.GetSceneByName(room);
                    Check(room + "/房间实际载入", scene.isLoaded && GameManager.instance.sceneName == room);
                    if (!scene.isLoaded) continue;
                    var loaders = SceneLoaders(scene).ToList();
                    foreach (var c in loaders) originalDoors[PathOf(c.transform)] = c.doorTrigger;
                    var hero = HeroController.instance; var region = TerrainSystem.Find(room);
                    Check(room + "/条件载入器可检查", loaders.Count > 0 && region != null);
                    if (region == null) continue;
                    rooms++;
                    var bounds = hero.GetComponent<BoxCollider2D>().bounds;
                    var origin = (Vector2)region.Root.position;
                    string bench = GameObject.FindGameObjectsWithTag("RespawnPoint").FirstOrDefault(g => g.scene.name == room)?.name ?? "";
                    var pylon = new Pylons.Pylon { Scene = room, Placed = true, Bench = "", Entry = EntryGateOf(room), ArriveBench = bench,
                        Local = new Vector2(bounds.center.x - Pylons.WidthCells * CellGrid.CellSize / 2f, bounds.min.y) - origin, Style = 8 };
                    if (!string.IsNullOrEmpty(bench)) benchRoutes++;
                    var guard = loaders.FirstOrDefault(c => c.isActiveAndEnabled && !string.IsNullOrEmpty(c.sceneNameToLoad)
                        && (!string.IsNullOrEmpty(c.needsPlayerDataBool) || c.usePersistentBoolItem && c.persistentBoolData != null));
                    string flag = guard?.needsPlayerDataBool;
                    bool wrong = guard != null && !guard.playerDataBoolValue;
                    PersistentBoolData record = guard != null && guard.usePersistentBoolItem ? guard.persistentBoolData : null;
                    var guardField = !string.IsNullOrEmpty(flag) ? AccessTools.Field(typeof(PlayerData), flag) : null;
                    bool wasFlag = guardField != null && (bool)guardField.GetValue(PlayerData.instance);
                    bool recordWas = record != null && SceneData.instance.FindMyState(record)?.activated == true;
                    var recordCopy = record == null ? null : new PersistentBoolData { sceneName = record.sceneName, id = record.id, semiPersistent = record.semiPersistent, activated = true };
                    yield return GoTo("Town", "left1");
                    yield return Arrival(pylon, string.IsNullOrEmpty(bench) ? "门传送" : "长椅传送");
                    if (guardField == null && recordCopy == null) continue;
                    yield return GoTo("Town", "left1");
                    try
                    {
                        // Change isolated fixture conditions without invoking kill/quest callbacks.
                        if (guardField != null) guardField.SetValue(PlayerData.instance, wrong);
                        else SceneData.instance.SaveMyState(recordCopy);
                        guarded++;
                        yield return Arrival(pylon, "条件不满足");
                    }
                    finally
                    {
                        if (guardField != null) guardField.SetValue(PlayerData.instance, wasFlag);
                        else { recordCopy.activated = recordWas; SceneData.instance.SaveMyState(recordCopy); }
                    }
                }
                Check("目录内16张条件载入地图全部覆盖", rooms == portalLoaderRooms.Length);
                Check("包含入口和长椅两种真实传送路径", benchRoutes > 0 && benchRoutes < rooms);
                Check("原生剧情或胜利条件阻止路径已覆盖", guarded > 0);
                target = ""; int before = PylonSceneLoad.DevExemptions;
                TransitionPoint.lastEntered = "left1";
                yield return GoTo("Ruins2_03", "bot1");
                Check("普通进房不获得传送豁免", PylonSceneLoad.DevExemptions == before && !Pylons.Arriving);
            }
            finally { USM.sceneLoaded -= Loaded; Player.TerraControls.DevNoRealKeys = false; }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "pylon-loaders-result.json"), JsonConvert.SerializeObject(new { Passed = passed, Total = total, Rooms = rooms, Guarded = guarded, BenchRoutes = benchRoutes, Evidence = evidence }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 全图晶塔 合计 {passed}/{total}，地图{rooms}，条件阻止{guarded}，长椅路径{benchRoutes}");
        }
    }
}
