using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// Terraria's camera: the player stays in the middle of the screen, always. Hollow Knight
    /// follows a separate camera target with look-ahead, fall and look-up offsets, room lock zones
    /// and the scene's edges; this points the camera straight at the player instead. Past the edge
    /// of a scene there is the neighbouring scene (see LiveWorld) or darkness. One thing of the
    /// game's is kept: the lowest the room's camera lock area lets the camera go. The camera is a
    /// perspective one, and a room's foreground (nearer the camera than the play) was placed to sit
    /// on the ground as seen from no lower than that: below it the foreground rides up off the
    /// ground (Dirtmouth's fences and grass floating), and the empty ground under the room fills
    /// the screen. No ceiling: the view still follows the player up.
    /// </summary>
    [HarmonyPatch]
    internal static class TerraCamera
    {
        public static bool Enabled = true;
        /// <summary>Dev: hold the camera on this point instead of the player.</summary>
        public static Vector2? DevLook;
        /// <summary>Follow smoothing in seconds; Terraria barely smooths at all.</summary>
        const float DampTime = 0.04f;

        static bool Active => Enabled && TerraMod.Instance != null && World.TerrainSystem.Current != null;

        static readonly AccessTools.FieldRef<CameraController, Vector3> velocityX = AccessTools.FieldRefAccess<CameraController, Vector3>("velocityX");
        static readonly AccessTools.FieldRef<CameraController, Vector3> velocityY = AccessTools.FieldRefAccess<CameraController, Vector3>("velocityY");
        static readonly AccessTools.FieldRef<CameraController, float> startLockedTimer = AccessTools.FieldRefAccess<CameraController, float>("startLockedTimer");
        static readonly System.Action<CameraController> getTilemapInfo =
            AccessTools.MethodDelegate<System.Action<CameraController>>(AccessTools.Method(typeof(CameraController), "GetTilemapInfo"));

        static Vector2 HeroCenter(HeroController hero)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            return col != null ? (Vector2)col.bounds.center : (Vector2)hero.transform.position;
        }

        /// <summary>CameraController.LateUpdate without the look offsets, lock zones and scene clamp.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CameraController), "LateUpdate")]
        static bool LateUpdate(CameraController __instance, bool ___isGameplayScene, HeroController ___hero_ctrl)
        {
            if (!Active || !___isGameplayScene || ___hero_ctrl == null) return true;
            var t = __instance.transform;
            if (DevLook.HasValue)
            {
                Snap(__instance, DevLook.Value);
                return false;
            }
            if (__instance.mode != CameraController.CameraMode.FROZEN)
            {
                var target = WithinWorld(__instance, HeroCenter(___hero_ctrl) + ScopePan(__instance));
                target.y += LockFloorPush(__instance, ___hero_ctrl, target.y, false);
                __instance.destination = new Vector3(target.x, target.y, t.position.z);
                float x = Vector3.SmoothDamp(t.position, new Vector3(target.x, t.position.y, t.position.z), ref velocityX(__instance), DampTime).x;
                float y = Vector3.SmoothDamp(t.position, new Vector3(t.position.x, target.y, t.position.z), ref velocityY(__instance), DampTime).y;
                t.position = new Vector3(x, y, t.position.z);
            }
            ref float timer = ref startLockedTimer(__instance);
            if (timer > 0f) timer -= Time.deltaTime;
            return false;
        }

        static Vector2 pan;
        /// <summary>Dev: the scope's pan target (world units) instead of the mouse's; the pan now.</summary>
        public static Vector2? DevPan;
        public static Vector2 Pan => pan;

        /// <summary>
        /// The scopes (GetPanControlValues Pl:6268-6291, M:63684-63828): with one worn and a gun
        /// held, the right button held pushes the view toward the cursor, half the screen at most
        /// each way (panFactor 0.5); it glides there and back at 36 px a frame.
        /// </summary>
        static Vector2 ScopePan(CameraController cc)
        {
            var target = Vector2.zero;
            var mod = TerraMod.Instance;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var cam = cc.cam != null ? cc.cam : Camera.main;
            bool scoped = mod != null && cam != null && Items.Equipment.Current.Scope && mod.Inventory.Current.Def.UseAmmo == Items.AmmoKind.Bullet;
            if (scoped && DevPan.HasValue) target = DevPan.Value;
            else if (scoped && mouse != null && !TerraMod.DevNoRealMouse && mouse.rightButton.isPressed && !Items.InventoryGui.MouseOverUi)
            {
                var m = mouse.position.ReadValue();
                var n = new Vector2(Mathf.Clamp01(m.x / Screen.width) * 2f - 1f, Mathf.Clamp01(m.y / Screen.height) * 2f - 1f);
                float dist = Mathf.Abs(cc.transform.position.z);
                float halfH = cam.orthographic ? cam.orthographicSize : dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                target = new Vector2(n.x * halfH * cam.aspect, n.y * halfH) * 0.5f;
            }
            pan = Vector2.MoveTowards(pan, target, 36f * 60f * Effects.TerraParticles.Px * Time.unscaledDeltaTime);
            return pan;
        }

        /// <summary>
        /// Scene entry and respawn snap the camera to the hero. Snap straight onto the player (the
        /// vanilla version clamps to the scene and offsets towards the facing side), then tell the
        /// fade the level is in view as vanilla does.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CameraController), nameof(CameraController.PositionToHero))]
        static bool PositionToHero(CameraController __instance)
        {
            if (!Active) return true;
            // Walking over a gate line into a scene already laid out: no snap, no fade-in.
            if (World.LiveWorld.Promoting) return false;
            __instance.StartCoroutine(SnapToHero(__instance));
            return false;
        }

        /// <summary>
        /// For half a second after a scene starts, entering one of its camera lock areas puts the
        /// camera inside the area at once. This camera pays no heed to lock areas, and walking
        /// over a gate line starts the scene again without the camera ever leaving the player:
        /// that would throw the view aside for a frame. The lock still takes hold, without the jump.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CameraController), nameof(CameraController.LockToArea))]
        static void BeforeLockToArea(CameraController __instance)
        {
            if (Active) startLockedTimer(__instance) = 0f;
        }

        static IEnumerator SnapToHero(CameraController cc)
        {
            yield return new WaitForFixedUpdate();
            var hero = HeroController.instance;
            getTilemapInfo(cc);
            cc.camTarget.PositionToStart();
            var previous = cc.mode;
            if (hero != null)
            {
                var p = WithinWorld(cc, HeroCenter(hero), snap: true);
                p.y += LockFloorPush(cc, hero, p.y, true);
                Snap(cc, p);
            }
            yield return new WaitForSeconds(0.1f);
            GameCameras.instance.cameraFadeFSM.Fsm.Event("LEVEL LOADED");
            if (previous == CameraController.CameraMode.FROZEN) cc.SetMode(CameraController.CameraMode.FOLLOWING);
        }

        /// <summary>
        /// Terraria keeps the view inside the world, and Hollow Knight keeps it inside the scene:
        /// past the edge of every loaded scene there is only the backs of scenery never meant to be
        /// seen (above King's Pass, where a new game drops the player in, a wall of black). The view
        /// stops where the world stops. The world is all the scenes laid out together, and their
        /// edges don't line up, so where it stops is measured through the player: along his row,
        /// how far the scenes run either way without a break, and along his column, up and down.
        /// Only reaching the end of his own row or column stops the view, as at Terraria's world
        /// edge, so passing between stacked scenes of different widths doesn't push it sideways;
        /// moving from one scene into the next doesn't change the world, so the view doesn't lurch;
        /// and when a scene loads or goes, the view glides to where the new edges put it.
        /// </summary>
        static Vector2 WithinWorld(CameraController cc, Vector2 target, bool snap = false)
        {
            var cam = cc.cam != null ? cc.cam : Camera.main;
            if (World.TerraWorld.Live == null || cam == null) return target;
            const float c = World.CellGrid.CellSize;
            float dist = Mathf.Abs(cc.transform.position.z);
            float halfH = cam.orthographic ? cam.orthographicSize : dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * cam.aspect;
            rects.Clear();
            foreach (var t in World.TerrainSystem.All)
            {
                var r = t.Region;
                if (r == null) continue;
                var cr = r.CellRect;
                rects.Add(new Rect(cr.xMin * c, cr.yMin * c, cr.width * c, cr.height * c));
            }
            var limits = new Rect(float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity);
            if (Average(target, halfH, true, out float left, out float right)) { limits.xMin = left; limits.xMax = right; }
            if (Average(target, halfW, false, out float bottom, out float top)) { limits.yMin = bottom; limits.yMax = top; }
            // How far the world's edges push the view off the player. That changes in jumps when a
            // scene comes or goes (or a line across the view starts or stops finding one); the push
            // glides to its new size (about a tenth of a second), so the view never jumps.
            var push = new Vector2(Fit(target.x, limits.xMin, limits.xMax, halfW), Fit(target.y, limits.yMin, limits.yMax, halfH)) - target;
            if (snap || !havePush) smoothedPush = push;
            else smoothedPush = Vector2.Lerp(smoothedPush, push, 1f - Mathf.Exp(-Time.unscaledDeltaTime / LimitGlide));
            havePush = true;
            return target + smoothedPush;
        }

        const float LimitGlide = 0.12f;

        static readonly AccessTools.FieldRef<CameraController, CameraLockArea> currentLockArea = AccessTools.FieldRefAccess<CameraController, CameraLockArea>("currentLockArea");
        static float floorPush;
        public static float DevFloor;
        public static string DevLock = "无";

        /// <summary>
        /// How far up the room's lock area floor (CameraController.LockToArea's yLockMin: cameraYMin, or
        /// 8.3 when unset) pushes a view centred at y; never so far the player leaves the view. In a
        /// scene moved behind the player its numbers are in its own coordinates: moved with it. The
        /// push glides as it changes (entering, leaving the area), like the world's edges.
        /// </summary>
        static float LockFloorPush(CameraController cc, HeroController hero, float y, bool snap)
        {
            float want = 0f;
            var area = currentLockArea(cc);
            DevLock = "无";
            if (cc.mode == CameraController.CameraMode.LOCKED && area != null && area.isActiveAndEnabled)
            {
                var cam = cc.cam != null ? cc.cam : Camera.main;
                float halfH = cam == null ? 8.3f : cam.orthographic ? cam.orthographicSize : Mathf.Abs(cc.transform.position.z) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float floor = (area.cameraYMin < 0f ? 8.3f : area.cameraYMin) + World.SceneFrames.Of(area.gameObject).y;
                floor = Mathf.Min(floor, HeroCenter(hero).y + halfH * 0.7f);
                want = Mathf.Max(0f, floor - y);
                DevLock = $"{area.gameObject.scene.name}/{area.name} 最低 {floor:F2}";
            }
            floorPush = snap ? want : Mathf.Lerp(floorPush, want, 1f - Mathf.Exp(-Time.unscaledDeltaTime / LimitGlide));
            DevFloor = floorPush;
            return floorPush;
        }
        static readonly System.Collections.Generic.List<Rect> rects = new System.Collections.Generic.List<Rect>();
        static readonly System.Collections.Generic.List<Vector2> spans = new System.Collections.Generic.List<Vector2>();
        static Vector2 smoothedPush;
        static bool havePush;

        /// <summary>The view centred on p, kept between lo and hi (centred between them when it is wider than that).</summary>
        static float Fit(float p, float lo, float hi, float half)
        {
            if (float.IsInfinity(lo) || float.IsInfinity(hi)) return p;
            if (hi - lo <= half * 2f) return (lo + hi) / 2f;
            return Mathf.Clamp(p, lo + half, hi - half);
        }

        /// <summary>
        /// Along the player's row (for the left and right limits) or column (bottom and top), the
        /// unbroken stretch of scenes around him. False when there is none.
        /// </summary>
        static bool Average(Vector2 at, float halfAcross, bool horizontal, out float lo, out float hi)
        {
            const int Lines = 9;
            const float Gap = 0.6f;
            lo = hi = 0f;
            int n = 0;
            float p = horizontal ? at.x : at.y;
            for (int k = 0; k < Lines; k++)
            {
                float line = (horizontal ? at.y : at.x) - halfAcross + (k + 0.5f) / Lines * halfAcross * 2f;
                spans.Clear();
                foreach (var r in rects)
                {
                    if (horizontal ? (line < r.yMin || line > r.yMax) : (line < r.xMin || line > r.xMax)) continue;
                    spans.Add(horizontal ? new Vector2(r.xMin, r.xMax) : new Vector2(r.yMin, r.yMax));
                }
                if (spans.Count == 0) continue;
                spans.Sort((u, v) => u.x.CompareTo(v.x));
                // Merge into unbroken stretches and take the one around the player.
                float a = spans[0].x, b = spans[0].y;
                bool found = false;
                for (int i = 1; i <= spans.Count && !found; i++)
                {
                    if (i < spans.Count && spans[i].x <= b + Gap)
                    {
                        b = Mathf.Max(b, spans[i].y);
                        continue;
                    }
                    if (p >= a - Gap && p <= b + Gap) found = true;
                    else if (i < spans.Count) { a = spans[i].x; b = spans[i].y; }
                }
                if (!found) continue;
                lo += a;
                hi += b;
                n++;
            }
            if (n == 0) return false;
            lo /= n;
            hi /= n;
            return true;
        }

        /// <summary>Puts the camera on a point at once, with no smoothing carried over.</summary>
        public static void Snap(CameraController cc, Vector2 point)
        {
            var t = cc.transform;
            t.position = new Vector3(point.x, point.y, t.position.z);
            cc.destination = t.position;
            velocityX(cc) = Vector3.zero;
            velocityY(cc) = Vector3.zero;
        }
    }
}
