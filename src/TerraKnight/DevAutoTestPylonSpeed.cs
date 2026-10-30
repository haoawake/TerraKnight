using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "pylonspeed", the user's 10-29 "传送动画变长了吗？有人说有点卡卡的"): a placed pylon in rooms with side, bottom
    /// and top gates, reached by each of those gates, the game's own way in (PylonDoorEntry.DevOff, as since 5.2.0) and the
    /// door's: from the click to the player put by the pylon and to his moving again, and how far the view had to go from
    /// where the room faded in to where he stands. Lines "[AutoTest] PylonSpeed …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>Where the view was as the room began fading in (GameManager.FadeSceneIn), and when.</summary>
        static Vector2 fadeInCamera;
        static float fadeInAt = -1f;

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.FadeSceneIn))]
        static class PylonSpeedFadeIn
        {
            static void Postfix()
            {
                var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
                if (cam == null) return;
                fadeInCamera = cam.transform.position;
                fadeInAt = Time.realtimeSinceStartup;
            }
        }

        IEnumerator PylonSpeedTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            PlayerData.instance.isInvincible = true; Player.TerraStats.DevImmortal = true; mod.DevNoFallDamage = true;
            mod.ApplyCreative(false); Player.TerraControls.DevNoRealKeys = true;
            var rooms = (Environment.GetEnvironmentVariable("TK_PSPEED_ROOMS") ?? "Crossroads_01;Crossroads_04;Fungus1_02;Ruins1_02;Cliffs_01").Split(';');
            var results = new List<(string mode, float took, float placed, float jump)>();
            try
            {
                foreach (string room in rooms)
                {
                    yield return GoTo(room, EntryGateOf(room));
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(room);
                    var region = TerrainSystem.Find(room);
                    if (!scene.isLoaded || region == null) { Plugin.Log.LogInfo($"[AutoTest] PylonSpeed {room} 进不去"); continue; }
                    var hero = HeroController.instance;
                    var b = hero.GetComponent<BoxCollider2D>().bounds;
                    var local = new Vector2(b.center.x - Pylons.WidthCells * CellGrid.CellSize / 2f, b.min.y) - (Vector2)region.Root.position;
                    // Every gate of the room that isn't a door, by its way in.
                    var gates = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TransitionPoint>(true))
                        .Where(t => t != null && !t.isADoor).Select(t => (t.name, pos: t.GetGatePosition()))
                        .Where(g => g.pos != GlobalEnums.GatePosition.door && g.pos != GlobalEnums.GatePosition.unknown)
                        .GroupBy(g => g.pos).Select(g => g.First()).ToList();
                    Plugin.Log.LogInfo($"[AutoTest] PylonSpeed {room} 入口：{string.Join("、", gates.Select(g => $"{g.name}({g.pos})"))}");
                    foreach (var (gate, pos) in gates.Select(g => (g.name, g.pos)))
                        foreach (bool off in new[] { true, false })
                        {
                            yield return GoTo("Town", "left1");
                            World.PylonDoorEntry.DevOff = off;
                            var p = new Pylons.Pylon { Scene = room, Placed = true, Bench = "", Entry = gate, ArriveBench = "", Local = local, Style = 8 };
                            fadeInAt = -1f;
                            Pylons.DevTravelTook = Pylons.DevPlacedTook = -1f;
                            Pylons.Teleport(p);
                            float until = Time.realtimeSinceStartup + 20f;
                            while (Pylons.DevTravelTook < 0f && Time.realtimeSinceStartup < until) yield return null;
                            yield return new WaitForSeconds(0.3f);
                            var cam = GameCameras.instance.mainCamera;
                            float jump = fadeInAt > 0f && cam != null ? Vector2.Distance(fadeInCamera, cam.transform.position) : -1f;
                            string mode = off ? "原生进场" : "门式进场";
                            results.Add((mode + "/" + pos, Pylons.DevTravelTook, Pylons.DevPlacedTook, jump));
                            // The room loaded again: its grid's root is a new one.
                            var here = TerrainSystem.Find(room);
                            var at = here != null && here.Root != null ? (Vector2)HeroController.instance.transform.position - ((Vector2)here.Root.position + local) : Vector2.positiveInfinity;
                            Plugin.Log.LogInfo($"[AutoTest] PylonSpeed {room} 经 {gate}({pos}) {mode}：点下到能动 {Pylons.DevTravelTook:F2}s，到晶塔旁 {Pylons.DevPlacedTook:F2}s，淡入时镜头离最终位置 {jump:F1}，落点离晶塔 {at.magnitude:F1}，门式 {World.PylonDoorEntry.DevDoorEntries}");
                        }
                }
            }
            finally { World.PylonDoorEntry.DevOff = false; Player.TerraControls.DevNoRealKeys = false; }
            foreach (var g in results.GroupBy(r => r.mode).OrderBy(g => g.Key))
                Plugin.Log.LogInfo($"[AutoTest] PylonSpeed 汇总 {g.Key}：{g.Count()} 次，点下到能动 平均 {g.Average(r => r.took):F2}s，到晶塔旁 {g.Average(r => r.placed):F2}s，镜头跳 平均 {g.Average(r => r.jump):F1} 最大 {g.Max(r => r.jump):F1}");
        }
    }
}
