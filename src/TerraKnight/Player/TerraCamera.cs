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

        /// <summary>
        /// The player's middle as drawn this frame: the box's centre carried by his transform, which his rigidbody
        /// interpolates between physics steps. The box's own bounds stand still between steps (50 a second, the screen
        /// drawn at 144 or more): a camera on them moved in jerks under a smoothly drawn player, and the scenery nearest
        /// the camera, the foreground, shook worst.
        /// </summary>
        static Vector2 HeroCenter(HeroController hero)
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
                Snap(__instance, HeroCenter(___hero_ctrl) + ScopePan(__instance) + BossPan(__instance, HeroCenter(___hero_ctrl)));
            ref float timer = ref startLockedTimer(__instance);
            if (timer > 0f) timer -= Time.deltaTime;
            return false;
        }

        static Vector2 bossPan;
        public static Vector2 DevBossPan => bossPan;

        /// <summary>
        /// Hollow Knight's view is about half of Terraria's at the same screen, and its arenas are made to be seen
        /// whole under their camera locks: a boss leaping off the screen (the False Knight, Hornet...) leaves only its
        /// health bar. While a boss's bar is up the view eases toward it as far as it is off screen (its whole body in
        /// view, a margin inside the edge), the player kept within six tenths of the view's middle; back on the
        /// player when the boss is in view again or the fight is over.
        /// </summary>
        static Vector2 BossPan(CameraController cc, Vector2 player)
        {
            var target = Vector2.zero;
            var cam = cc.cam != null ? cc.cam : Camera.main;
            var info = UI.EnemyHealthBars.Fought;
            var hm = info?.Hm;
            if (cam != null && hm != null && !hm.isDead && hm.hp > 0 && hm.gameObject.activeInHierarchy && !Items.EnemyStats.Defeated(hm))
            {
                Bounds b = default;
                bool any = false;
                foreach (var r in hm.GetComponentsInChildren<Renderer>())
                {
                    if (r == null || !r.enabled || r is ParticleSystemRenderer || r.bounds.size.x > 40f) continue;
                    if (!any) { b = r.bounds; any = true; }
                    else b.Encapsulate(r.bounds);
                }
                if (!any) { var col = hm.GetComponent<Collider2D>(); if (col != null) { b = col.bounds; any = true; } }
                float dist = Mathf.Abs(cc.transform.position.z);
                float halfH = cam.orthographic ? cam.orthographicSize : dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), halfW = halfH * cam.aspect;
                if (any && Vector2.Distance(b.center, player) < 40f)
                {
                    float mx = halfW * 0.9f, my = halfH * 0.85f;
                    if (b.max.x > player.x + mx) target.x = b.max.x - (player.x + mx);
                    else if (b.min.x < player.x - mx) target.x = b.min.x - (player.x - mx);
                    if (b.max.y > player.y + my) target.y = b.max.y - (player.y + my);
                    else if (b.min.y < player.y - my) target.y = b.min.y - (player.y - my);
                    target.x = Mathf.Clamp(target.x, -halfW * 0.6f, halfW * 0.6f);
                    target.y = Mathf.Clamp(target.y, -halfH * 0.6f, halfH * 0.6f);
                }
            }
            // Glided there and back, a little faster than a scope (the boss doesn't wait).
            bossPan = Vector2.MoveTowards(bossPan, target, 48f * 60f * Effects.TerraParticles.Px * Time.unscaledDeltaTime);
            return bossPan;
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
