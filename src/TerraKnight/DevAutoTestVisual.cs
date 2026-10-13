using System.Collections;
using System.Collections.Generic;
using GlobalEnums;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "visual"): what crossing between scenes looks like. Each crossing is watched frame
    /// by frame (brightness of the whole screen, how much it changed since the frame before, the
    /// camera against the player) so a flash or a jump shows up as a spike; and the same stretch of
    /// world is photographed from the same camera position before entering a neighbour and after,
    /// so anything a scene only sets up when it is entered shows up as a difference.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator VisualTest(string scene, HeroController hero)
        {
            var world = LiveWorld.Instance;
            mod.DevNoFallDamage = true;

            // King's Pass -> Dirtmouth, sideways.
            yield return WaitNeighbour(world, GatePosition.right);
            if (world.TryGetNeighbour(GatePosition.right, out var town, out _, out var gate, out _))
            {
                yield return StandInsideGate(hero, gate, 1);
                yield return SameViewBothWays(hero, scene, town, 1, 0, "v1");
            }

            // Dirtmouth -> Crossroads, down the well.
            yield return WaitNeighbour(world, GatePosition.bottom);
            if (world.TryGetNeighbour(GatePosition.bottom, out var below, out _, out var well, out _))
            {
                hero.transform.position = new Vector3(well.x, well.y + 4f, Constants.HERO_Z);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.3f);
                yield return SameViewBothWays(hero, GameManager.instance.sceneName, below, 0, -1, "v2");
                yield return StandOnSeam(hero, "v2s");
            }

            // Greenpath's stacked rooms.
            foreach (var (room, entry) in new[] { ("Fungus1_31", "right1"), ("Fungus1_21", "right1") })
            {
                yield return GoTo(room, entry);
                hero = HeroController.instance;
                foreach (var side in new[] { GatePosition.top, GatePosition.bottom })
                {
                    yield return WaitNeighbour(world, side, 12f);
                    if (!world.TryGetNeighbour(side, out var other, out _, out var g, out _)) continue;
                    int dy = side == GatePosition.top ? 1 : -1;
                    hero.transform.position = Clear(hero, new Vector2(g.x, g.y - dy * 4f));
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.5f);
                    yield return SameViewBothWays(hero, GameManager.instance.sceneName, other, 0, dy, $"v3_{room}_{side}");
                    yield return StandOnSeam(hero, $"v3s_{room}_{side}");
                    yield return GoTo(room, entry);
                    hero = HeroController.instance;
                }
            }
            mod.DevNoFallDamage = false;
        }

        IEnumerator WaitNeighbour(LiveWorld world, GatePosition side, float seconds = 30f)
        {
            float wait = Time.time + seconds;
            while (!world.TryGetNeighbour(side, out _, out _, out _, out _) && Time.time < wait) yield return null;
        }

        IEnumerator GoTo(string sceneName, string gate)
        {
            var gm = GameManager.instance;
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                SceneName = sceneName, EntryGateName = gate, HeroLeaveDirection = GatePosition.unknown,
                EntryDelay = 0f, WaitForSceneTransitionCameraFade = true, Visualization = GameManager.SceneLoadVisualizations.Default,
            });
            float wait = Time.time + 40f;
            while ((gm.sceneName != sceneName || !GameHooks.PlayerHasControl()) && Time.time < wait) yield return null;
            yield return new WaitForSeconds(2f);
            Plugin.Log.LogInfo($"[AutoTest] 到了 {gm.sceneName}（{gate}），可操作={GameHooks.PlayerHasControl()}");
        }

        /// <summary>
        /// Photographs the view with the camera held where it is, crosses into the neighbour while
        /// watching every frame, then holds the camera on the same spot again and photographs it.
        /// </summary>
        IEnumerator SameViewBothWays(HeroController hero, string from, string to, int dirX, int dirY, string tag)
        {
            Vector2 look = hero.GetComponent<BoxCollider2D>().bounds.center + new Vector3(dirX * 6f, dirY * 4f, 0f);
            Player.TerraCamera.DevLook = look;
            yield return new WaitForSeconds(0.4f);
            Shot(from, tag + "a_before_entering_" + to);
            Player.TerraCamera.DevLook = null;
            yield return null;

            var frames = new List<string>();
            mod.StartCoroutine(WatchFrames(frames, 70));
            // Upwards the player would fly or climb; carry him up at a rocket's pace.
            var lift = dirY > 0 ? mod.StartCoroutine(Lift(hero, from)) : null;
            yield return Cross(hero, from, dirX, dirY, tag + "x");
            if (lift != null) mod.StopCoroutine(lift);
            yield return new WaitForSeconds(0.6f);
            Plugin.Log.LogInfo($"[AutoTest] {tag} 越线前后每帧（亮度 / 和上一帧的差 / 相机-主角 / 当前场景）：{string.Join(" | ", frames)}");

            Player.TerraCamera.DevLook = look;
            yield return new WaitForSeconds(0.4f);
            Shot(GameManager.instance.sceneName, tag + "b_after_entering_" + to);
            Player.TerraCamera.DevLook = null;
            yield return new WaitForSeconds(0.2f);
        }

        static IEnumerator Lift(HeroController hero, string from)
        {
            var rb = hero.GetComponent<Rigidbody2D>();
            float t0 = Time.time;
            while (GameManager.instance.sceneName == from && Time.time - t0 < 6f)
            {
                rb.linearVelocity = new Vector2(0f, 7.5f * Player.TerraMovement.PxT);
                yield return new WaitForFixedUpdate();
            }
        }

        /// <summary>Per frame: mean brightness, mean change from the frame before, the camera against the player, the scene.</summary>
        IEnumerator WatchFrames(List<string> into, int count)
        {
            float[] prev = null;
            var cam = GameCameras.instance.mainCamera.transform;
            for (int f = 0; f < count; f++)
            {
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                const int nx = 48, ny = 30;
                var lum = new float[nx * ny];
                float sum = 0f, diff = 0f;
                for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    var c = tex.GetPixel((i * 2 + 1) * tex.width / (nx * 2), (j * 2 + 1) * tex.height / (ny * 2));
                    float l = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                    lum[j * nx + i] = l;
                    sum += l;
                    if (prev != null) diff += Mathf.Abs(l - prev[j * nx + i]);
                }
                Object.Destroy(tex);
                prev = lum;
                var hero = HeroController.SilentInstance;
                Vector2 rel = hero != null ? (Vector2)(cam.position - hero.transform.position) : Vector2.zero;
                Vector2 hp = hero != null ? (Vector2)hero.transform.position : Vector2.zero;
                into.Add($"{sum / lum.Length:F3}/{diff / lum.Length:F3}/({rel.x:F2},{rel.y:F2})/{GameManager.instance.sceneName}/主角({hp.x:F2},{hp.y:F2})");
            }
        }

        /// <summary>
        /// Stands the player right on the line between two stacked scenes for a while: does the
        /// current scene flip back and forth, does the game keep pushing him out of the rock, does
        /// the camera wander?
        /// </summary>
        IEnumerator StandOnSeam(HeroController hero, string tag)
        {
            var world = LiveWorld.Instance;
            int crossings = world.CrossCount;
            var rb = hero.GetComponent<Rigidbody2D>();
            var cam = GameCameras.instance.mainCamera.transform;
            var trace = new System.Text.StringBuilder();
            float t0 = Time.time;
            Vector2 heroStart = hero.transform.position;
            float camMax = 0f;
            while (Time.time - t0 < 2.5f)
            {
                Vector2 rel = cam.position - hero.transform.position;
                camMax = Mathf.Max(camMax, rel.magnitude);
                if (trace.Length < 900) trace.Append($" {hero.transform.position.x:F1},{hero.transform.position.y:F1}/{rel.x:F1},{rel.y:F1}/{GameManager.instance.sceneName}");
                yield return new WaitForSeconds(0.1f);
            }
            Plugin.Log.LogInfo($"[AutoTest] {tag} 站在缝上 2.5s：越线 {world.CrossCount - crossings} 次，主角 {heroStart:F1} -> {(Vector2)hero.transform.position:F1}，" +
                               $"相机偏离主角最多 {camMax:F2}，速度 {rb.linearVelocity:F2}；轨迹（主角/相机-主角/场景）：{trace}");
            Shot(GameManager.instance.sceneName, tag + "_on_seam");
        }
    }
}
