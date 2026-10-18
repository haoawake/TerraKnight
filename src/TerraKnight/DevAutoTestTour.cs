using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "tour1".."tour6"): every scene on the world map, a sixth at a time. Each entered
    /// by one of its gates as the game would: whether the player gets there and in control, the
    /// screen's brightness (a black screen), the player stuck inside terrain or fallen out of the
    /// scene, a walk each way (stuck in place), and the errors logged while there.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        public const int TourParts = 6;

        static readonly string[] tourSkip = { "GG_", "Dream_", "Cinematic", "Menu", "Opening", "End", "PermaDeath", "Room_Final_Boss", "Room_Tram" };

        IEnumerator TourTest(HeroController hero, int part)
        {
            var scenes = new List<string>();
            foreach (var s in UI.WorldMap.DevSceneNames())
            {
                bool skip = false;
                foreach (var p in tourSkip) if (s.StartsWith(p)) skip = true;
                if (!skip) scenes.Add(s);
            }
            scenes.Sort(System.StringComparer.Ordinal);
            int from = scenes.Count * (part - 1) / TourParts, to = scenes.Count * part / TourParts;
            Plugin.Log.LogInfo($"[AutoTest] 巡游 第{part}段：{from}..{to - 1}（共 {scenes.Count} 个场景）");

            var errors = new List<string>();
            void OnLog(string msg, string stack, LogType type)
            {
                if (type == LogType.Exception || (type == LogType.Error && msg.Contains("Exception")) || msg.StartsWith("[Error"))
                    if (errors.Count < 20) errors.Add(msg.Length > 160 ? msg.Substring(0, 160) : msg);
            }
            Application.logMessageReceived += OnLog;
            var bad = new StringBuilder();
            int good = 0;
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            try
            {
                for (int i = from; i < to; i++)
                {
                    string scene = scenes[i];
                    var gm = GameManager.instance;
                    PlayerData.instance.isInvincible = true;
                    float t0 = Time.time;
                    while (gm.IsInSceneTransition && Time.time - t0 < 10f) yield return null;
                    errors.Clear();
                    gm.BeginSceneTransition(new GameManager.SceneLoadInfo
                    {
                        SceneName = scene, EntryGateName = EntryGateOf(scene), HeroLeaveDirection = GlobalEnums.GatePosition.unknown,
                        EntryDelay = 0f, WaitForSceneTransitionCameraFade = true, Visualization = GameManager.SceneLoadVisualizations.Default,
                    });
                    t0 = Time.time;
                    yield return new WaitForSeconds(0.5f);
                    while ((gm.sceneName != scene || gm.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.time - t0 < 20f) yield return null;
                    float took = Time.time - t0;
                    PlayerData.instance.isInvincible = true;
                    // The fade in over (some scenes' takes a while).
                    yield return new WaitForSeconds(3f);
                    var h = HeroController.instance;
                    var col = h.GetComponent<BoxCollider2D>();
                    bool arrived = gm.sceneName == scene;
                    bool control = GameHooks.PlayerHasControl();
                    float luma = -1f;
                    yield return Luma(v => luma = v);
                    var b = col.bounds;
                    bool inside = Physics2D.OverlapBox(b.center, (Vector2)b.size * 0.7f, 0f, terrain) != null;
                    var tcam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
                    bool covered = tcam != null && World.TerraWorld.CoveredByScenery(b.center, tcam.transform.position);
                    // A walk each way.
                    float x0 = h.transform.position.x;
                    Player.TerraMovement.DevMove = 1;
                    yield return new WaitForSeconds(0.7f);
                    float x1 = h.transform.position.x;
                    Player.TerraMovement.DevMove = -1;
                    yield return new WaitForSeconds(0.7f);
                    float x2 = h.transform.position.x;
                    Player.TerraMovement.DevMove = 0;
                    bool stuck = control && Mathf.Abs(x1 - x0) < 0.15f && Mathf.Abs(x2 - x1) < 0.15f;
                    var region = World.TerrainSystem.Find(gm.sceneName);
                    bool fellOut = false;
                    if (region != null)
                    {
                        float bottom = region.Root.TransformPoint(Vector3.zero).y;
                        fellOut = h.transform.position.y < bottom - 3f;
                    }
                    bool dark = luma >= 0f && luma < 0.035f;
                    bool ok = arrived && control && !dark && !inside && !stuck && !fellOut && errors.Count == 0;
                    if (ok) good++;
                    else bad.Append($" {scene}");
                    string errs = errors.Count > 0 ? $"，报错 {errors.Count} 条：{string.Join(" | ", errors.GetRange(0, Mathf.Min(3, errors.Count)))}" : "";
                    Plugin.Log.LogInfo($"[AutoTest] 巡游 {scene}：{(ok ? "好" : "坏")}，到达 {arrived}（{gm.sceneName}），可操作 {control}，用时 {took:F1}s，亮度 {luma:F3}{(dark ? "（黑屏）" : "")}，被前景挡住 {covered}，卡在地形里 {inside}，走不动 {stuck}（{x1 - x0:F2}/{x2 - x1:F2}），掉出场景 {fellOut}，主角 {(Vector2)h.transform.position:F1}{errs}");
                    if (!ok)
                    {
                        Shot(gm.sceneName, $"tour_{scene}");
                        yield return null;
                    }
                }
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                Player.TerraMovement.DevMove = 0;
            }
            Plugin.Log.LogInfo($"[AutoTest] 巡游 第{part}段：{good}/{to - from} 个场景正常{(bad.Length > 0 ? "，有问题的：" + bad : "")}");
        }
    }
}
