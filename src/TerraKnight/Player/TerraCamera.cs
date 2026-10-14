using System.Collections;
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

        static Vector2 HeroCenter(HeroController hero)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            return col != null ? (Vector2)col.bounds.center : (Vector2)hero.transform.position;
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

        /// <summary>CameraController.LateUpdate without the look offsets, lock zones, scene clamp and smoothing.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CameraController), "LateUpdate")]
        static bool LateUpdate(CameraController __instance, bool ___isGameplayScene, HeroController ___hero_ctrl)
        {
            if (!Active || !___isGameplayScene || ___hero_ctrl == null) return true;
            if (DevLook.HasValue)
            {
                Snap(__instance, DevLook.Value);
                return false;
            }
            if (__instance.mode != CameraController.CameraMode.FROZEN)
                Snap(__instance, HeroCenter(___hero_ctrl) + ScopePan(__instance));
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
        /// that would throw the view aside for a frame.
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
