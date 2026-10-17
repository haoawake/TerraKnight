using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_listenscan"): every state machine that waits for one of the Knight's buttons the mod switches
    /// off (TerraControls: attack, cast, quick cast, super dash, dream nail, dash). Each scene with such an action
    /// (found in the level files) is loaded beside the one played, its objects switched off at once so nothing in it
    /// runs, and each listening state is logged with its events, its machine's other states that take or give back
    /// the player's control, and the scene unloaded again.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly string[] listenScanScenes = (
            "Abyss_02,Abyss_04,Abyss_08,Abyss_10,Abyss_12,Abyss_15,Abyss_17,Abyss_18,Abyss_19,Abyss_20,Abyss_22,Cliffs_01,Cliffs_05,Crossroads_01," +
            "Crossroads_04,Crossroads_10,Crossroads_22,Crossroads_30,Crossroads_33,Crossroads_38,Crossroads_47,Crossroads_50,Crossroads_ShamanTemple," +
            "Deepnest_01b,Deepnest_14,Deepnest_16,Deepnest_26b,Deepnest_30,Deepnest_32,Deepnest_33,Deepnest_44,Deepnest_45_v02,Deepnest_East_03," +
            "Deepnest_East_06,Deepnest_East_07,Deepnest_East_08,Deepnest_East_12,Deepnest_East_13,Deepnest_East_14b,Deepnest_East_18,Deepnest_Spider_Town," +
            "Dream_03_Infected_Knight,Dream_Abyss,Dream_Final_Boss,Fungus1_01b,Fungus1_04,Fungus1_06,Fungus1_08,Fungus1_10,Fungus1_11,Fungus1_14," +
            "Fungus1_15,Fungus1_16_alt,Fungus1_22,Fungus1_24,Fungus1_28,Fungus1_31,Fungus1_37,Fungus1_Slug,Fungus2_02,Fungus2_03,Fungus2_04,Fungus2_05," +
            "Fungus2_13,Fungus2_14,Fungus2_17,Fungus2_18,Fungus2_20,Fungus2_23,Fungus2_25,Fungus2_26,Fungus2_29,Fungus2_31,Fungus2_34,Fungus3_25," +
            "Fungus3_26,Fungus3_28,Fungus3_30,Fungus3_39,Fungus3_40,Fungus3_48,Fungus3_50,Fungus3_archive,GG_Atrium,GG_Atrium_Roof,GG_False_Knight," +
            "GG_Lurker,GG_Radiance,GG_Spa,GG_Workshop,Grimm_Divine,Grimm_Main_Tent,Hive_01,Hive_05,Mines_11,Mines_18,Mines_20,Mines_29,Mines_30," +
            "Mines_34,Mines_36,RestingGrounds_04,RestingGrounds_08,RestingGrounds_09,RestingGrounds_10,RestingGrounds_12,RestingGrounds_17," +
            "Room_Colosseum_02,Room_Colosseum_Bronze,Room_Colosseum_Gold,Room_Colosseum_Silver,Room_Final_Boss_Atrium,Room_Queen,Room_Slug_Shrine," +
            "Room_Tram,Room_Tram_RG,Room_Wyrm,Room_nailmaster,Ruins1_02,Ruins1_03,Ruins1_05c,Ruins1_17,Ruins1_18,Ruins1_27,Ruins1_28,Ruins1_29," +
            "Ruins1_30,Ruins1_31,Ruins1_32,Ruins2_03,Ruins2_05,Ruins2_08,Ruins2_Watcher_Room,Ruins_Bathhouse,Ruins_Elevator,Town,Tutorial_01," +
            "Waterways_02,Waterways_03,Waterways_04b,Waterways_05,Waterways_09,Waterways_12,Waterways_15,White_Palace_01,White_Palace_03_hub," +
            "White_Palace_06,White_Palace_09").Split(',');

        static readonly string[] listenActions = { "ListenForAttack", "ListenForCast", "ListenForQuickCast", "ListenForSuperDash", "ListenForDreamNail", "ListenForDash" };

        static string loadingForScan;

        static void FreezeScanned(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != loadingForScan) return;
            foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
        }

        static string EventsOf(HutongGames.PlayMaker.FsmStateAction a)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var field in a.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                if (field.GetValue(a) is HutongGames.PlayMaker.FsmEvent ev && ev != null && !string.IsNullOrEmpty(ev.Name)) sb.Append(field.Name).Append('=').Append(ev.Name).Append(' ');
            return sb.ToString().Trim();
        }

        static bool Mentions(HutongGames.PlayMaker.FsmStateAction a, string word)
        {
            if (a.GetType().Name.Contains(word)) return true;
            foreach (var field in a.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                object v = field.GetValue(a);
                string s = v is HutongGames.PlayMaker.FsmString fs ? fs.Value : v as string;
                if (s != null && s.Contains(word)) return true;
            }
            return false;
        }

        void ScanFsm(string sceneName, PlayMakerFSM f, ref int found)
        {
            HutongGames.PlayMaker.FsmState[] states;
            try { states = f.FsmStates; } catch (System.Exception) { return; }
            var takes = new List<string>();
            var gives = new List<string>();
            foreach (var s in states)
                foreach (var a in s.Actions)
                {
                    if (Mentions(a, "RelinquishControl")) takes.Add(s.Name);
                    if (Mentions(a, "RegainControl")) gives.Add(s.Name);
                }
            foreach (var s in states)
                foreach (var a in s.Actions)
                {
                    string type = a.GetType().Name;
                    if (System.Array.IndexOf(listenActions, type) < 0) continue;
                    found++;
                    var tr = new System.Text.StringBuilder();
                    foreach (var t in s.Transitions) tr.Append($"{t.EventName}->{t.ToState} ");
                    Plugin.Log.LogInfo($"[AutoTest] 按键扫描 {sceneName} | {PathOf(f.transform)} | '{f.FsmName}' | 状态 {s.Name} | {type}({EventsOf(a)}) | 跳转 {tr}| 收控制 [{string.Join(",", takes)}] 还控制 [{string.Join(",", gives)}]");
                }
        }

        /// <summary>Dev (suite "fx_fsm_Scene~Name"): every FSM in a scene on an object whose path contains Name, dumped (the scene frozen as above).</summary>
        IEnumerator SceneFsmDump(string arg)
        {
            int cut = arg.IndexOf('~');
            string name = cut > 0 ? arg.Substring(0, cut) : arg, part = cut > 0 ? arg.Substring(cut + 1) : "";
            USM.sceneLoaded += FreezeScanned;
            try
            {
                var scene = USM.GetSceneByName(name);
                bool loadedHere = false;
                if (!scene.isLoaded)
                {
                    loadingForScan = name;
                    var load = USM.LoadSceneAsync(name, LoadSceneMode.Additive);
                    if (load == null) { Plugin.Log.LogInfo($"[AutoTest] 状态机导出 {name} 载入不了"); yield break; }
                    while (!load.isDone) yield return null;
                    scene = USM.GetSceneByName(name);
                    loadedHere = true;
                }
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
                        if (PathOf(f.transform).Contains(part))
                        {
                            Logged(() => DumpFsm(f));
                            foreach (var st in f.FsmStates)
                                foreach (var a in st.Actions)
                                    if (a is HutongGames.PlayMaker.Actions.SendMessage sm && sm.functionCall != null)
                                        Plugin.Log.LogInfo($"[AutoTest] 状态机导出 {PathOf(f.transform)} '{f.FsmName}' 状态 {st.Name} SendMessage {sm.functionCall.FunctionName}({sm.functionCall.ParameterType})");
                        }
                if (loadedHere)
                {
                    var unload = USM.UnloadSceneAsync(scene);
                    while (unload != null && !unload.isDone) yield return null;
                }
            }
            finally
            {
                USM.sceneLoaded -= FreezeScanned;
                loadingForScan = null;
            }
        }

        IEnumerator ListenScan()
        {
            USM.sceneLoaded += FreezeScanned;
            int total = 0;
            try
            {
                foreach (var name in listenScanScenes)
                {
                    var scene = USM.GetSceneByName(name);
                    bool loadedHere = false;
                    if (!scene.isLoaded)
                    {
                        loadingForScan = name;
                        var load = USM.LoadSceneAsync(name, LoadSceneMode.Additive);
                        if (load == null) { Plugin.Log.LogInfo($"[AutoTest] 按键扫描 {name} 载入不了"); continue; }
                        while (!load.isDone) yield return null;
                        scene = USM.GetSceneByName(name);
                        loadedHere = true;
                    }
                    int found = 0;
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
                            ScanFsm(name, f, ref found);
                    total += found;
                    if (loadedHere)
                    {
                        var unload = USM.UnloadSceneAsync(scene);
                        while (unload != null && !unload.isDone) yield return null;
                    }
                    loadingForScan = null;
                    PlayerData.instance.isInvincible = true;
                }
            }
            finally
            {
                USM.sceneLoaded -= FreezeScanned;
            }
            Plugin.Log.LogInfo($"[AutoTest] 按键扫描 共 {listenScanScenes.Length} 个场景，{total} 处");
        }
    }
}
