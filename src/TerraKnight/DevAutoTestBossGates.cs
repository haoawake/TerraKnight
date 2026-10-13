using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bossgates"): what decides whether each boss's fight happens, for the creative
    /// book's "fight it" button. Every boss's arena (and the scene it adds for the fight) loaded
    /// beside the one played: its conditional scene loads (what they test), its persistent bools,
    /// every state machine action that reads or writes the player's data, its gates.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly string[] bossGateScenes =
        {
            "Crossroads_10", "Crossroads_10_boss", "Crossroads_04", "Crossroads_09", "Fungus1_04", "Fungus1_04_boss", "Fungus1_29",
            "Cliffs_02", "Cliffs_02_boss", "Fungus1_35", "Waterways_05", "Waterways_05_boss", "Fungus3_23", "Fungus3_23_boss",
            "Fungus2_15", "Fungus2_15_boss", "Ruins1_24", "Ruins1_24_boss", "Fungus3_archive_02", "Fungus3_archive_02_boss",
            "Waterways_12", "Waterways_12_boss", "Mines_18", "Mines_18_boss", "Mines_32", "Fungus2_32", "Fungus3_40", "Fungus3_40_boss",
            "Ruins2_03", "Ruins2_03_boss", "Hive_05", "Ruins2_11", "Ruins2_11_boss", "Deepnest_32", "Deepnest_40",
            "RestingGrounds_02", "RestingGrounds_02_boss", "Deepnest_East_Hornet", "Deepnest_East_Hornet_boss", "Deepnest_East_10",
            "Abyss_19", "Grimm_Main_Tent", "Grimm_Main_Tent_boss", "Grimm_Nightmare",
            "Dream_01_False_Knight", "Dream_02_Mage_Lord", "Dream_03_Infected_Knight", "Dream_04_White_Defender", "Dream_Mighty_Zote",
            "Room_Final_Boss_Core", "Dream_Final_Boss", "GG_Hollow_Knight", "GG_Nailmasters", "GG_Painter", "GG_Sly",
        };

        static string Val(object o)
        {
            if (o == null) return "null";
            var t = o.GetType();
            var vp = t.GetProperty("Value");
            if (t.Namespace == "HutongGames.PlayMaker" && vp != null)
            {
                var name = t.GetProperty("Name")?.GetValue(o) as string;
                var v = vp.GetValue(o);
                return (string.IsNullOrEmpty(name) ? "" : "$" + name + "=") + (v is Object uo ? (uo != null ? uo.name : "null") : v?.ToString() ?? "null");
            }
            if (o is HutongGames.PlayMaker.FsmEvent ev) return "ev:" + ev.Name;
            return o.ToString();
        }

        IEnumerator BossGatesTest()
        {
            foreach (var name in bossGateScenes)
            {
                var scene = USM.GetSceneByName(name);
                bool loadedHere = false;
                if (!scene.isLoaded)
                {
                    var load = USM.LoadSceneAsync(name, LoadSceneMode.Additive);
                    if (load == null) { Plugin.Log.LogInfo($"[AutoTest] 首领开关 {name} 载入不了"); continue; }
                    while (!load.isDone) yield return null;
                    scene = USM.GetSceneByName(name);
                    loadedHere = true;
                }
                yield return null;
                PlayerData.instance.isInvincible = true;
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var c in root.GetComponentsInChildren<SceneAdditiveLoadConditional>(true))
                        {
                            var sb = new StringBuilder();
                            sb.Append($"载入 {c.sceneNameToLoad}（另一个 {c.altSceneNameToLoad}）：bool {c.needsPlayerDataBool}={c.playerDataBoolValue}，int {c.needsPlayerDataInt}={c.playerDataIntValue}（{c.isIntValue}）");
                            if (c.extraBoolTests != null) foreach (var b in c.extraBoolTests) sb.Append($"，额外 bool {b.playerDataBool}={b.value}");
                            if (c.extraIntTests != null) foreach (var i in c.extraIntTests) sb.Append($"，额外 int {i.playerDataInt} {i.testType} {i.value}/{i.otherPlayerDataInt}");
                            if (c.usePersistentBoolItem && c.persistentBoolData != null) sb.Append($"，看场景状态 {c.persistentBoolData.sceneName}/{c.persistentBoolData.id}");
                            if (!string.IsNullOrEmpty(c.doorTrigger)) sb.Append($"，门 {c.doorTrigger}");
                            Plugin.Log.LogInfo($"[AutoTest] 首领开关 {name} {PathOf(c.transform)}：{sb}");
                        }
                        foreach (var p in root.GetComponentsInChildren<PersistentBoolItem>(true))
                        {
                            var d = p.persistentBoolData;
                            Plugin.Log.LogInfo($"[AutoTest] 首领开关 {name} 场景状态 {PathOf(p.transform)}：{d?.sceneName}/{d?.id} 半持久 {p.semiPersistent}");
                        }
                        foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
                            foreach (var s in f.FsmStates)
                                foreach (var a in s.Actions)
                                {
                                    string type = a.GetType().Name;
                                    if (type.IndexOf("PlayerData", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                                    var sb = new StringBuilder();
                                    foreach (var fi in a.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                                    {
                                        if (fi.DeclaringType == typeof(HutongGames.PlayMaker.FsmStateAction)) continue;
                                        sb.Append($" {fi.Name}={Val(fi.GetValue(a))}");
                                    }
                                    Plugin.Log.LogInfo($"[AutoTest] 首领开关 {name} 状态机 {PathOf(f.transform)} '{f.FsmName}' [{s.Name}] {type}:{sb}");
                                }
                        // The game's own components that switch things by the player's data (ActivateIfPlayerdataTrue...).
                        foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                        {
                            if (mb == null || mb is PlayMakerFSM || mb is SceneAdditiveLoadConditional) continue;
                            var sb = new StringBuilder();
                            foreach (var fi in mb.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                            {
                                if (fi.FieldType != typeof(string)) continue;
                                string fn = fi.Name.ToLowerInvariant();
                                if (!fn.Contains("bool") && !fn.Contains("playerdata") && !fn.Contains("intname")) continue;
                                var v = fi.GetValue(mb) as string;
                                if (!string.IsNullOrEmpty(v)) sb.Append($" {fi.Name}={v}");
                            }
                            if (sb.Length > 0) Plugin.Log.LogInfo($"[AutoTest] 首领开关 {name} 组件 {PathOf(mb.transform)} {mb.GetType().Name}:{sb}");
                        }
                        foreach (var tp in root.GetComponentsInChildren<TransitionPoint>(true))
                            Plugin.Log.LogInfo($"[AutoTest] 首领开关 {name} 门 {tp.name} → {tp.targetScene}/{tp.entryPoint}，门洞 {tp.isADoor}，启用 {tp.gameObject.activeInHierarchy}");
                    }
                }
                catch (System.Exception e) { Plugin.Log.LogInfo($"[AutoTest] 首领开关 {name} 出错：{e.Message}"); }
                if (loadedHere)
                {
                    foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
                    var unload = USM.UnloadSceneAsync(scene);
                    while (unload != null && !unload.isDone) yield return null;
                }
                yield return new WaitForSeconds(0.2f);
            }
        }
    }
}
