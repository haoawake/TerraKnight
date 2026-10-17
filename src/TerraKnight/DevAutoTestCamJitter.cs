using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_camjitter"): the Terraria camera (camera lock on) while the Knight runs along Dirtmouth for three
    /// seconds. Each rendered frame: how far the camera moved, how far the Knight's drawn body (his transform, which the
    /// rigidbody interpolates) moved, and how far his collider's bounds (the physics step's) moved. A camera following a
    /// target that moves only on physics steps jumps on some frames and stands on others while the Knight is drawn
    /// moving smoothly: the scenery nearest the camera (the foreground) shakes most.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CamJitterTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            PlayerData.instance.isInvincible = true;
            bool lockWas = Player.TerraCamera.Enabled;
            Player.TerraCamera.Enabled = true;
            yield return new WaitForSeconds(0.5f);
            var hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var cam = GameCameras.instance.mainCamera.transform;
            var camSteps = new List<float>();
            var drawnSteps = new List<float>();
            var boundsSteps = new List<float>();
            var offDrawn = new List<float>();
            float lastCam = cam.position.x, lastDrawn = hero.transform.TransformPoint(col.offset).x, lastBounds = col.bounds.center.x;
            Player.TerraMovement.DevMove = 1;
            int frames = 0;
            for (float t = Time.realtimeSinceStartup + 3f; Time.realtimeSinceStartup < t;)
            {
                yield return new WaitForEndOfFrame();
                float c = cam.position.x, d = hero.transform.TransformPoint(col.offset).x, b = col.bounds.center.x;
                if (frames > 10)
                {
                    camSteps.Add(c - lastCam);
                    drawnSteps.Add(d - lastDrawn);
                    boundsSteps.Add(b - lastBounds);
                    offDrawn.Add(c - d);
                }
                lastCam = c; lastDrawn = d; lastBounds = b;
                frames++;
            }
            Player.TerraMovement.DevMove = 0;
            string Stat(List<float> v)
            {
                if (v.Count == 0) return "无";
                float mean = 0f; foreach (var x in v) mean += x; mean /= v.Count;
                float var = 0f; foreach (var x in v) var += (x - mean) * (x - mean); var /= v.Count;
                int still = 0; foreach (var x in v) if (Mathf.Abs(x) < 1e-4f) still++;
                return $"平均 {mean:F4} 波动 {Mathf.Sqrt(var):F4} 不动的帧 {still}/{v.Count}";
            }
            float spread = 0f;
            if (offDrawn.Count > 0) { float lo = float.MaxValue, hi = float.MinValue; foreach (var x in offDrawn) { lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x); } spread = hi - lo; }
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 镜头抖 {frames} 帧 3 秒（{frames / 3f:F0} 帧/秒，物理 {1f / Time.fixedDeltaTime:F0} 次/秒），刚体插值 {rb.interpolation}，镜头锁 {Player.TerraCamera.Enabled}\n" +
                $"  镜头每帧：{Stat(camSteps)}\n  画出来的身子每帧：{Stat(drawnSteps)}\n  碰撞盒每帧：{Stat(boundsSteps)}\n  镜头减画出的身子：变化幅度 {spread:F3}");
            FxCheck("镜头跟着画出来的小骑士，不随物理步跳", spread < 0.05f, $"镜头相对画出的身子来回差 {spread:F3}");
            Player.TerraCamera.Enabled = lockWas;
        }
    }
}
