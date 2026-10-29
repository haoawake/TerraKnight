using System.Collections;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// Terraria's camera: the player stays in the middle of the screen, always (Main.screenPosition
    /// is the player's centre less half the screen, every frame). Hollow Knight follows a separate
    /// camera target with look-ahead, fall and look-up offsets, room lock zones and the scene's
    /// edges; this points the camera straight at the player instead, with nothing that pulls it
    /// off him: no lock zone's floor, no stopping at the edge of the scenes loaded, no lag. Past
    /// the edge of a scene there is the neighbouring scene (see LiveWorld) or darkness.
    /// </summary>
    [HarmonyPatch]
    internal static class TerraCamera
    {
        public static bool Enabled = true;
        /// <summary>Dev: hold the camera on this point instead of the player.</summary>
        public static Vector2? DevLook;

        static bool Active => Enabled && TerraMod.Instance != null && World.TerrainSystem.Current != null;

        static readonly AccessTools.FieldRef<CameraController, Vector3> velocityX = AccessTools.FieldRefAccess<CameraController, Vector3>("velocityX");
        static readonly AccessTools.FieldRef<CameraController, Vector3> velocityY = AccessTools.FieldRefAccess<CameraController, Vector3>("velocityY");
        static readonly AccessTools.FieldRef<CameraController, float> startLockedTimer = AccessTools.FieldRefAccess<CameraController, float>("startLockedTimer");
        static readonly System.Action<CameraController> getTilemapInfo =
            AccessTools.MethodDelegate<System.Action<CameraController>>(AccessTools.Method(typeof(CameraController), "GetTilemapInfo"));

        /// <summary>
        /// The player's middle as drawn this frame: the box's centre carried by his transform, which his rigidbody
        /// interpolates between physics steps. The box's own bounds stand still between steps (50 a second, the screen
        /// drawn at 144 or more): a camera on them moved in jerks under a smoothly drawn player, and the scenery nearest
        /// the camera, the foreground, shook worst.
        /// </summary>
        internal static Vector2 HeroCenter(HeroController hero)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            return col != null ? (Vector2)hero.transform.TransformPoint(col.offset) : (Vector2)hero.transform.position;
        }

        /// <summary>The player's centre now, as the camera puts it in the middle of the screen; dev: how far off it the camera is.</summary>
        public static Vector2 DevOffset
        {
            get
            {
                var hero = HeroController.SilentInstance;
                var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
                if (hero == null || cam == null) return Vector2.zero;
                return (Vector2)cam.transform.position - HeroCenter(hero);
            }
        }

        static ConfigEntry<float> zoomSetting;

        public static void Bind(ConfigFile cfg) =>
            zoomSetting = cfg.Bind("Gameplay", "Zoom", 2f,
                "视野缩放（泰拉瑞亚的缩放，1 到 2）：2 是空洞骑士自己的视野，1 看到的范围是它的两倍。游戏里按住 - / = 调。\n" +
                "View zoom (Terraria's zoom, 1 to 2): 2 is Hollow Knight's own view, 1 shows twice as far. Hold - / = in game.");

        /// <summary>
        /// Terraria's game zoom (Main.GameZoomTarget, 1 to 2): Zoom In / Zoom Out held change it by 0.02 a tick
        /// (Main.UpdateViewZoomKeys), and the view is drawn that much larger. Hollow Knight's view is about Terraria's at
        /// 2 (half its width at 1), so 2 is where it starts and 1 shows twice as far.
        /// </summary>
        public static float Zoom
        {
            get => zoomSetting != null ? Mathf.Clamp(zoomSetting.Value, 1f, 2f) : zoom;
            set
            {
                zoom = Mathf.Clamp(value, 1f, 2f);
                if (zoomSetting != null) zoomSetting.Value = zoom;
            }
        }
        static float zoom = 2f;
        static bool zoomHeld;
        /// <summary>Dev: frames the zoom was looked at, with the keys held, and the zoom factor put on the camera.</summary>
        public static int DevZoomFrames, DevZoomKeyFrames, DevZoomOursFrames;
        public static float DevZoomFactor = 1f;

        /// <summary>Dev: the view's height on the play plane now (world units).</summary>
        public static float DevViewHeight
        {
            get
            {
                var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
                return cam == null ? 0f : 2f * Mathf.Abs(cam.transform.position.z) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            }
        }

        /// <summary>
        /// The zoom keys held this frame, and the view they make. The camera's tk2dCamera sets its field of view before each
        /// frame is drawn (OnPreCull: its own angle over its ZoomFactor), so the zoom goes there: the factor that turns its
        /// angle into one showing 2/zoom as much. The game's own (1) back when the view isn't the Terraria camera's.
        /// </summary>
        static void ApplyZoom(Camera cam, bool ours)
        {
            if (cam == null || cam.orthographic) return;
            DevZoomFrames++;
            float z = zoomHeld ? zoom : Zoom;
            if (ours) DevZoomOursFrames++;
            if (ours && !Items.InventoryGui.TypingName && !Net.NetChat.BlocksKeys)
            {
                float step = 0.02f * 60f * Time.unscaledDeltaTime;
                bool zoomIn = TerraKeys.Held(TerraKeys.Action.ViewZoomIn), zoomOut = TerraKeys.Held(TerraKeys.Action.ViewZoomOut);
                if (zoomIn || zoomOut) DevZoomKeyFrames++;
                if (zoomIn) z = Mathf.Clamp(z + step, 1f, 2f);
                if (zoomOut) z = Mathf.Clamp(z - step, 1f, 2f);
                // Kept in memory while held, written to the config file once let go.
                if (zoomIn || zoomOut) { zoom = z; zoomHeld = true; }
                else if (zoomHeld) { zoomHeld = false; Zoom = zoom; }
            }
            var tk = cam.GetComponent<tk2dCamera>();
            if (tk == null) return;
            float own = tk.SettingsRoot != null ? tk.SettingsRoot.CameraSettings.fieldOfView : tk.CameraSettings.fieldOfView;
            float factor = 1f;
            if (ours && z < 1.999f)
            {
                float fov = 2f * Mathf.Atan(Mathf.Tan(own * 0.5f * Mathf.Deg2Rad) * 2f / z) * Mathf.Rad2Deg;
                factor = own / fov;
            }
            if (!Mathf.Approximately(tk.ZoomFactor, factor)) tk.ZoomFactor = factor;
            DevZoomFactor = factor;
        }

        /// <summary>CameraController.LateUpdate without the look offsets, lock zones, scene clamp and smoothing.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CameraController), "LateUpdate")]
        static bool LateUpdate(CameraController __instance, bool ___isGameplayScene, HeroController ___hero_ctrl)
        {
            // Last frame's scope pan on the game's own camera off again first (OnGameCamera).
            if (onGame != Vector2.zero)
            {
                __instance.transform.position -= (Vector3)onGame;
                onGame = Vector2.zero;
            }
            // The zoom works with the game's own camera too (the centred one is a setting, off by default).
            bool gameplay = TerraMod.Instance != null && World.TerrainSystem.Current != null && ___isGameplayScene && ___hero_ctrl != null;
            ApplyZoom(__instance.cam != null ? __instance.cam : GameCameras.instance != null ? GameCameras.instance.mainCamera : null, gameplay);
            bool ours = Active && gameplay;
            if (!ours) return true;
            if (DevLook.HasValue)
            {
                Snap(__instance, DevLook.Value);
                return false;
            }
            // A phase's native freeze/pan and our old boss framing must not move a locked view.
            bossPan = Vector2.zero;
            Snap(__instance, HeroCenter(___hero_ctrl) + ScopePan(__instance) + Shake());
            ref float timer = ref startLockedTimer(__instance);
            if (timer > 0f) timer -= Time.deltaTime;
            return false;
        }

        static float shakeUntil, shakeLength = 1f, shakeStrength;

        /// <summary>
        /// A short shake of the view (Terraria's PunchCameraModifier, for the charged soul wave): about strength world units at
        /// first, gone in seconds. The game's own shakes stay off with this camera (LockedShake).
        /// </summary>
        public static void Punch(float strength, float seconds)
        {
            float left = Mathf.Max(0f, shakeUntil - Time.unscaledTime) / shakeLength;
            shakeStrength = Mathf.Max(shakeStrength * left, strength);
            shakeLength = Mathf.Max(0.01f, seconds);
            shakeUntil = Time.unscaledTime + seconds;
        }

        static Vector2 Shake()
        {
            float left = shakeUntil - Time.unscaledTime;
            if (left <= 0f) return Vector2.zero;
            return Random.insideUnitCircle * (shakeStrength * left / shakeLength);
        }

        static Vector2 bossPan;
        public static Vector2 DevBossPan => bossPan;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ConfigManager), "get_CameraShakeMultiplier")]
        static void LockedShake(ref float __result)
        {
            // Native ShakePositionV2 reads this ONLY for IsCameraShake. Its timers,
            // looping rumbles and StopEvent continue normally with zero displacement.
            // The saved vanilla setting is never modified, and body shakes still play.
            if (Active) __result = 0f;
        }

        static Vector2 pan;
        /// <summary>Dev: the scope's pan target (world units) instead of the mouse's; the pan now.</summary>
        public static Vector2? DevPan;
        public static Vector2 Pan => pan;

        /// <summary>Terraria 1.4.5's Forward Zoom (PlayerInput.CurrentProfile.PanControl_Keyboard, its options' "向前缩放").</summary>
        internal enum PanMode { OnAlways, OffAlways, Click, Hold }

        public static ConfigEntry<PanMode> PanSetting;
        public static PanMode Panning => PanSetting != null ? PanSetting.Value : PanMode.Hold;

        public static void BindPan(ConfigFile cfg) =>
            PanSetting = cfg.Bind("Gameplay", "ForwardZoom", PanMode.Hold,
                "向前缩放（泰拉瑞亚的同名选项）：瞄准镜和狙击步枪把视野推向鼠标。Hold 按住右键，Click 点右键切换，OnAlways 始终开启，OffAlways 始终关闭（饰品的属性照常）。\nForward Zoom (Terraria's option): the scopes and the Sniper Rifle push the view toward the mouse. Hold: while the right button is held; Click: the right button toggles it; OnAlways; OffAlways (the accessories' stats still count).");

        /// <summary>Utils.CycleControlControl_OffOnClickHold's order: off, on, click, hold.</summary>
        public static void CyclePan()
        {
            if (PanSetting == null) return;
            PanSetting.Value = PanSetting.Value switch { PanMode.OffAlways => PanMode.OnAlways, PanMode.OnAlways => PanMode.Click, PanMode.Click => PanMode.Hold, _ => PanMode.OffAlways };
            panToggled = false;
            Plugin.Log.LogInfo($"[设置] 向前缩放：{PanSetting.Value}");
        }

        /// <summary>Click mode: the pan switched on by a right click, off by the next.</summary>
        static bool panToggled, rightWas;
        static int panToggledFrame = -1;

        /// <summary>Dev: the right button held (null: the mouse's).</summary>
        public static bool? DevRight;

        /// <summary>
        /// GetPanControlValues (Pl:6268-6291, 1.4.5): a scope worn, half the screen (panFactor 0.5); the Sniper Rifle's own
        /// scope two thirds, with a scope worn too 0.8. -1: no pan. A worn scope counts (player.scope, Pl:15001-15010) only
        /// with a gun that fires bullets, candy corn, stakes or gel in hand.
        /// </summary>
        static float PanFactor(TerraMod mod)
        {
            var held = mod.Inventory.Current.Def;
            var ammo = held != null ? held.UseAmmo : Items.AmmoKind.None;
            bool scope = Items.Equipment.Current.Scope && (ammo == Items.AmmoKind.Bullet || ammo == Items.AmmoKind.CandyCorn || ammo == Items.AmmoKind.Stake || ammo == Items.AmmoKind.Gel);
            float f = scope ? 0.5f : -1f;
            if (mod.Inventory.Current.Type == Items.ItemType.SniperRifle) f = scope ? 0.8f : 2f / 3f;
            return f;
        }

        /// <summary>
        /// The scopes (M:63684-63828): the right button held pushes the view toward the cursor, at most the pan factor of
        /// half the screen each way; it glides there and back at 36 px a frame. Not with the mouse over the interface.
        /// With the Terraria camera or the game's own (OnGameCamera) alike.
        /// </summary>
        static Vector2 ScopePan(CameraController cc)
        {
            var target = Vector2.zero;
            var mod = TerraMod.Instance;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var cam = cc.cam != null ? cc.cam : Camera.main;
            float factor = mod != null && cam != null ? PanFactor(mod) : -1f;
            var mode = Panning;
            bool real = mouse != null && !TerraMod.DevNoRealMouse;
            // The right button (a test's DevRight; DevPan alone counts as held), not over the interface.
            bool right = DevRight ?? (DevPan.HasValue || real && mouse.rightButton.isPressed && !Items.InventoryGui.MouseOverUi && !Items.InventoryGui.Open);
            if (panToggledFrame != Time.frameCount)
            {
                panToggledFrame = Time.frameCount;
                if (factor <= 0f || mode != PanMode.Click) panToggled = false;
                else if (right && !rightWas) panToggled = !panToggled;
                rightWas = right;
            }
            bool on = mode == PanMode.OnAlways || mode == PanMode.Hold && right || mode == PanMode.Click && panToggled;
            if (factor > 0f && on && DevPan.HasValue) target = DevPan.Value;
            else if (factor > 0f && on && real)
            {
                var m = mouse.position.ReadValue();
                var n = new Vector2(Mathf.Clamp01(m.x / Screen.width) * 2f - 1f, Mathf.Clamp01(m.y / Screen.height) * 2f - 1f);
                float dist = Mathf.Abs(cc.transform.position.z);
                float halfH = cam.orthographic ? cam.orthographicSize : dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                target = new Vector2(n.x * halfH * cam.aspect, n.y * halfH) * factor;
            }
            pan = Vector2.MoveTowards(pan, target, 36f * 60f * Effects.TerraParticles.Px * Time.unscaledDeltaTime);
            return pan;
        }

        /// <summary>The pan put on the game's own camera last frame, taken off again before it moves (its smoothing starts from where it was).</summary>
        static Vector2 onGame;

        /// <summary>
        /// The game's own camera (the Terraria camera off, its default): the scope's pan put on top of where it settles each
        /// frame (after its look offsets, lock areas and scene bounds), and taken off before its next move, so its smoothing
        /// never chases the pan.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CameraController), "LateUpdate")]
        static void OnGameCamera(CameraController __instance, bool ___isGameplayScene, HeroController ___hero_ctrl)
        {
            bool gameplay = TerraMod.Instance != null && World.TerrainSystem.Current != null && ___isGameplayScene && ___hero_ctrl != null;
            if (Active && gameplay) return;
            bool follows = __instance.mode == CameraController.CameraMode.FOLLOWING || __instance.mode == CameraController.CameraMode.LOCKED;
            var p = gameplay && follows ? ScopePan(__instance) : Vector2.zero;
            if (!gameplay || !follows) pan = Vector2.zero;
            if (p == Vector2.zero) return;
            __instance.transform.position += (Vector3)p;
            onGame = p;
            DevGamePanFrames++;
        }

        public static int DevGamePanFrames;

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
        /// that would throw the view aside for a frame.
        ///
        /// The game's own camera (the camera lock off) keeps them: a lock area holds the view within bounds drawn for
        /// where Hollow Knight lets the player stand. Digging reaches places it never meant, such as the shaft beside the
        /// Hunter's lair under his room (locked to the lair until his mark is had), and there the lock pulled the view
        /// away with the player off the screen. A lock that would leave him out of view isn't taken (one held is let go)
        /// until he stands where its view shows him; the game asks again every step he is inside it.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CameraController), nameof(CameraController.LockToArea))]
        static bool BeforeLockToArea(CameraController __instance, CameraLockArea lockArea)
        {
            if (Active)
            {
                startLockedTimer(__instance) = 0f;
                return true;
            }
            var hero = HeroController.SilentInstance;
            if (TerraMod.Instance == null || World.TerrainSystem.Current == null || lockArea == null || hero == null) return true;
            if (LockShows(__instance, lockArea, HeroCenter(hero))) return true;
            if (__instance.lockZoneList != null && __instance.lockZoneList.Contains(lockArea)) __instance.ReleaseLock(lockArea);
            DevLocksRefused++;
            return false;
        }

        /// <summary>Dev: lock areas not taken because their view would leave the player off the screen.</summary>
        public static int DevLocksRefused;

        /// <summary>
        /// Whether the view a lock area holds the camera in (CameraController.LockToArea's bounds for its centre) still
        /// shows a point, a little inside the screen's edge, at the view's size now (the zoom included).
        /// </summary>
        static bool LockShows(CameraController cc, CameraLockArea area, Vector2 p)
        {
            var cam = cc.cam != null ? cc.cam : GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (cam == null) return true;
            float halfH = cam.orthographic ? cam.orthographicSize : Mathf.Abs(cam.transform.position.z) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * cam.aspect;
            if (halfH <= 0f) return true;
            float xMin = area.cameraXMin < 0f ? 14.6f : area.cameraXMin, xMax = area.cameraXMax < 0f ? cc.xLimit : area.cameraXMax;
            float yMin = area.cameraYMin < 0f ? 8.3f : area.cameraYMin, yMax = area.cameraYMax < 0f ? cc.yLimit : area.cameraYMax;
            const float inside = 1.5f;
            return p.x >= xMin - halfW + inside && p.x <= xMax + halfW - inside && p.y >= yMin - halfH + inside && p.y <= yMax + halfH - inside;
        }

        static IEnumerator SnapToHero(CameraController cc)
        {
            yield return new WaitForFixedUpdate();
            var hero = HeroController.instance;
            getTilemapInfo(cc);
            cc.camTarget.PositionToStart();
            var previous = cc.mode;
            if (hero != null) Snap(cc, HeroCenter(hero));
            yield return new WaitForSeconds(0.1f);
            GameCameras.instance.cameraFadeFSM.Fsm.Event("LEVEL LOADED");
            if (previous == CameraController.CameraMode.FROZEN) cc.SetMode(CameraController.CameraMode.FOLLOWING);
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
