using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "pylonall"): creative's pylons, every one of them. A teleport to each: whether the
    /// player gets there and back in control, standing (not falling for ever), the pause menu free,
    /// and whether the respawn point had to be made up (its bench not found).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>The screen's mean brightness (0-1) at the end of this frame, from a grid of samples.</summary>
        static IEnumerator Luma(System.Action<float> result)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            float sum = 0f;
            int n = 0;
            for (int y = 0; y < 48; y++)
                for (int x = 0; x < 64; x++)
                {
                    var c = tex.GetPixel(x * tex.width / 64, y * tex.height / 48);
                    sum += 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                    n++;
                }
            Object.Destroy(tex);
            result(sum / n);
        }

        IEnumerator PylonAllTest(HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            mod.ApplyCreativeWorld();
            yield return new WaitForSeconds(1f);
            var all = new List<World.Pylons.Pylon>(World.Pylons.Unlocked);
            int good = 0;
            var bad = new System.Text.StringBuilder();
            foreach (var p in all)
            {
                PlayerData.instance.isInvincible = true;
                float t0 = Time.time;
                while ((GameManager.instance.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.time - t0 < 10f) yield return null;
                int fallbacks = World.Pylons.DevSpawnFallbacks;
                // The first from a bench, sitting (as the map is opened from one).
                bool sat = p == all[0];
                if (sat) PlayerData.instance.atBench = true;
                World.Pylons.Teleport(p);
                t0 = Time.time;
                yield return new WaitForSeconds(1f);
                while ((GameManager.instance.sceneName != p.Scene || GameManager.instance.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.time - t0 < 20f) yield return null;
                float took = Time.time - t0;
                var h = HeroController.instance;
                yield return new WaitForSeconds(1f);
                float y0 = h.transform.position.y;
                yield return new WaitForSeconds(1f);
                Vector2 at = h.transform.position;
                float luma = -1f;
                yield return Luma(v => luma = v);
                bool arrived = GameManager.instance.sceneName == p.Scene;
                bool control = GameHooks.PlayerHasControl();
                bool falling = at.y < y0 - 2f || Mathf.Abs(at.x) > 5000f || Mathf.Abs(at.y) > 5000f;
                bool pauseLocked = PlayerData.instance.disablePause;
                bool dark = luma >= 0f && luma < 0.035f;
                var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
                var fade = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
                string view = cam != null ? $"镜头 {(Vector2)cam.transform.position:F1}，淡入 {(fade != null ? fade.ActiveStateName : "无")}，被前景挡住 {World.TerraWorld.CoveredByScenery(h.GetComponent<BoxCollider2D>().bounds.center, cam.transform.position)}" : "无镜头";
                bool ok = arrived && control && !falling && !pauseLocked && !dark && !(sat && PlayerData.instance.atBench);
                if (ok) good++;
                else bad.Append($" {p.Scene}");
                string fb = World.Pylons.DevSpawnFallbacks > fallbacks ? $"，复活点兜底：{World.Pylons.DevLastFallback}" : "";
                Plugin.Log.LogInfo($"[AutoTest] 全晶塔 {p.Scene}/{p.Bench}：{(ok ? "好" : "坏")}，到达 {arrived}，可操作 {control}，下坠 {falling}，暂停锁住 {pauseLocked}，用时 {took:F1}s，主角 {at:F1} 着地 {h.cState.onGround}，点下到能动 {World.Pylons.DevTravelTook:F2}s，亮度 {luma:F3}{(dark ? "（黑屏）" : "")}，{view}{(sat ? $"，坐着出发，到了还坐着 {PlayerData.instance.atBench}" : "")}，复活点 {PlayerData.instance.respawnScene}/{PlayerData.instance.respawnMarkerName} 类型 {PlayerData.instance.respawnType}{fb}");
                if (!ok)
                {
                    Shot(GameManager.instance.sceneName, $"pylonall_{p.Scene}");
                    yield return null;
                }
            }
            Plugin.Log.LogInfo($"[AutoTest] 全晶塔：{good}/{all.Count} 座传送正常{(bad.Length > 0 ? "，有问题的：" + bad : "")}");
        }
    }
}
