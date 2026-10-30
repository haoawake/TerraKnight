using System;
using System.Collections;
using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "pylonbench", the user's 10-30 "使用晶塔之后要等很久这件事还是没优化"): teleports to bench pylons
    /// (TK_PB_SCENES, ';'), each from Dirtmouth, with a timeline from the click: the camera fade's state, the scene, the
    /// hero's transitioning and control, the portal's phase, each change with its time; then from the click to moving
    /// again. The camera fade's script is dumped once. Lines "[AutoTest] 晶塔时间线 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static bool pylonControlTrace;
        static float pylonTraceStart;

        /// <summary>While a traced teleport runs: who takes the hero's control (the script running, the callers).</summary>
        [HarmonyLib.HarmonyPatch(typeof(HeroController), nameof(HeroController.RelinquishControl))]
        static class PylonBenchControlTrace
        {
            static void Prefix()
            {
                if (!pylonControlTrace) return;
                var fsm = HutongGames.PlayMaker.FsmExecutionStack.ExecutingFsm;
                string by = fsm != null ? $"{fsm.GameObjectName}/{fsm.Name}@{fsm.ActiveStateName}" : "无状态机";
                var frames = new System.Diagnostics.StackTrace(1, false).GetFrames() ?? new System.Diagnostics.StackFrame[0];
                var callers = new List<string>();
                foreach (var f in frames)
                {
                    var m = f.GetMethod();
                    if (m == null || m.DeclaringType == null) continue;
                    callers.Add(m.DeclaringType.Name + "." + m.Name);
                    if (callers.Count >= 6) break;
                }
                Plugin.Log.LogInfo($"[AutoTest] 晶塔时间线 {Time.realtimeSinceStartup - pylonTraceStart:F2}s 交出控制 ← {by}{(fsm != null ? $"（{fsm.OwnerName} 场景 {(fsm.GameObject != null ? fsm.GameObject.scene.name : "?")}）" : "")}：{string.Join(" < ", callers)}");
            }
        }

        /// <summary>While a traced teleport runs: the respawn type HeroController.Respawn starts with.</summary>
        [HarmonyLib.HarmonyPatch(typeof(HeroController), nameof(HeroController.Respawn))]
        static class PylonBenchRespawnTrace
        {
            static void Prefix()
            {
                if (!pylonControlTrace) return;
                var pd = PlayerData.instance;
                Plugin.Log.LogInfo($"[AutoTest] 晶塔时间线 {Time.realtimeSinceStartup - pylonTraceStart:F2}s 原版复活开始：类型 {pd.respawnType}，{pd.respawnScene}/{pd.respawnMarkerName}，坐着 {pd.atBench}");
            }
        }

        IEnumerator PylonBenchTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            PlayerData.instance.isInvincible = true; Player.TerraStats.DevImmortal = true; mod.DevNoFallDamage = true;
            mod.ApplyCreative(true);
            mod.ApplyCreativeWorld();
            Player.TerraControls.DevNoRealKeys = true;
            yield return new WaitForSeconds(1f);
            var fade = GameCameras.instance.cameraFadeFSM;
            if (Environment.GetEnvironmentVariable("TK_PB_DUMP") == "1") DumpFsm(fade);
            var scenes = (Environment.GetEnvironmentVariable("TK_PB_SCENES") ?? "Deepnest_30;Crossroads_47;Ruins1_29;Fungus2_13;Mines_29").Split(';');
            var took = new List<float>();
            try
            {
                foreach (string scene in scenes)
                {
                    Pylons.Pylon p = null;
                    foreach (var q in Pylons.Unlocked) if (q.Scene == scene && !q.Placed) p = q;
                    if (p == null) { Plugin.Log.LogInfo($"[AutoTest] 晶塔时间线 {scene} 没有长椅晶塔"); continue; }
                    yield return GoTo("Town", "left1");
                    for (float w = Time.time + 8f; (GameManager.instance.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.time < w;) yield return null;
                    yield return new WaitForSeconds(0.5f);
                    Pylons.DevTravelTook = -1f;
                    int kicks = Pylons.DevFadeKicks;
                    float t0 = Time.realtimeSinceStartup;
                    pylonTraceStart = t0;
                    pylonControlTrace = true;
                    Pylons.Teleport(p);
                    string last = null;
                    var hero = HeroController.instance;
                    for (float until = t0 + 15f; Time.realtimeSinceStartup < until;)
                    {
                        hero = HeroController.instance;
                        var gm = GameManager.instance;
                        string now = $"镜头 {fade.ActiveStateName}，场景 {gm.sceneName}{(gm.IsInSceneTransition ? "（切换中）" : "")}，进场 {hero.cState.transitioning}，交出控制 {hero.controlReqlinquished}，传送门 {Player.OpeningPortal.DevPhase}，到达中 {Pylons.Arriving}，复活点 {PlayerData.instance.respawnScene}/{PlayerData.instance.respawnMarkerName} 类型 {PlayerData.instance.respawnType}";
                        if (now != last) Plugin.Log.LogInfo($"[AutoTest] 晶塔时间线 {scene} {Time.realtimeSinceStartup - t0:F2}s {now}");
                        last = now;
                        if (Pylons.DevTravelTook >= 0f && Time.realtimeSinceStartup - t0 > Pylons.DevTravelTook + 0.3f) break;
                        yield return null;
                    }
                    pylonControlTrace = false;
                    took.Add(Pylons.DevTravelTook);
                    Plugin.Log.LogInfo($"[AutoTest] 晶塔时间线 {scene}：点下到能动 {Pylons.DevTravelTook:F2}s，到长椅旁 {Pylons.DevPlacedTook:F2}s，补发淡入 {Pylons.DevFadeKicks - kicks} 次");
                    yield return new WaitForSeconds(0.5f);
                }
            }
            finally { Player.TerraControls.DevNoRealKeys = false; pylonControlTrace = false; }
            if (took.Count > 0) Plugin.Log.LogInfo($"[AutoTest] 晶塔时间线 汇总：{took.Count} 次，点下到能动 {string.Join(" ", took.ConvertAll(t => t.ToString("F2")))}");
        }
    }
}
