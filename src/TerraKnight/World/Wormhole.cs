using System.Collections;
using HarmonyLib;
using TerraKnight.Items;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.World
{
    /// <summary>
    /// The Wormhole Potion (Main.DrawMap's head click: Player.UnityTeleport with the potion, one used) and the Potion of
    /// Return (Player.DoPotionOfReturnTeleportationAndSetTheComebackPoint / ...ReturnToOriginalUsePosition).
    /// A teammate's head clicked on the full map with a Wormhole Potion in the bag takes the player to him: in a room
    /// loaded here at once, else by the respawn road (the game's own scene change, as the Recall Potion goes) with the
    /// marker <see cref="MatePrefix"/> naming his place. The Potion of Return goes home as the Recall Potion does and
    /// leaves a portal there; a right click on it takes the player back where he drank it, and it is gone.
    /// </summary>
    [HarmonyPatch]
    internal static class Wormhole
    {
        /// <summary>A respawn marker no object has: "TerraKnight Wormhole@x,y" (scene coordinates), found by <see cref="AtTheMark"/>.</summary>
        public const string MatePrefix = "TerraKnight Wormhole@";

        public static int DevTeleports, DevReturns;
        public static string DevLast = "";

        static string Num(float v) => v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        static bool TryParse(string marker, out Vector2 local)
        {
            local = default;
            if (marker == null || !marker.StartsWith(MatePrefix)) return false;
            var xy = marker.Substring(MatePrefix.Length).Split(',');
            if (xy.Length != 2) return false;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (!float.TryParse(xy[0], System.Globalization.NumberStyles.Float, inv, out float x) || !float.TryParse(xy[1], System.Globalization.NumberStyles.Float, inv, out float y)) return false;
            local = new Vector2(x, y);
            return true;
        }

        /// <summary>A Wormhole Potion in the bag (its slot), -1 for none.</summary>
        static int PotionSlot(Inventory inv)
        {
            for (int i = 0; i < Inventory.Size; i++)
                if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == ItemType.WormholePotion) return i;
            return -1;
        }

        /// <summary>Whether a click on this player's head would take the local one to him (a teammate, a potion carried).</summary>
        public static bool CanGo(Net.RemotePlayer p)
        {
            var mod = TerraMod.Instance;
            if (mod == null || p == null || !p.HasState || p.Latest.Dead || string.IsNullOrEmpty(p.Latest.Scene)) return false;
            if (p.Team != Net.NetSession.Team) return false;
            return PotionSlot(mod.Inventory) >= 0;
        }

        /// <summary>To the teammate: one Wormhole Potion used.</summary>
        public static bool Go(Net.RemotePlayer p)
        {
            var mod = TerraMod.Instance;
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            if (!CanGo(p) || gm == null || hero == null || gm.IsInSceneTransition || hero.cState.dead || hero.cState.transitioning) return false;
            int slot = PotionSlot(mod.Inventory);
            mod.Inventory.ConsumeAt(slot);
            mod.PlaySound("Item_6");
            DevTeleports++;
            Player.PotionBuffs.Sparkle(hero.GetComponent<BoxCollider2D>().bounds.center);
            return TravelTo(p.Latest.Scene, p.Latest.Feet, $"传送到 {p.Name}");
        }

        /// <summary>To a place: here at once if its room is loaded, else by the respawn road.</summary>
        internal static bool TravelTo(string scene, Vector2 local, string why)
        {
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            if (gm == null || hero == null || string.IsNullOrEmpty(scene)) return false;
            Player.DelicateFlower.OnTeleport();
            var region = TerrainSystem.Find(scene);
            if (region != null && region.Root != null)
            {
                Vector2 at = region.Root.TransformPoint(local);
                hero.transform.position = new Vector3(at.x, at.y + 0.7f, hero.transform.position.z);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                Player.PotionBuffs.Sparkle(hero.GetComponent<BoxCollider2D>().bounds.center);
                DevLast = $"{why}（同一处已载入的场景 {scene} {(Vector2)hero.transform.position:F1}）";
                Plugin.Log.LogInfo($"[虫洞] {DevLast}");
                return true;
            }
            DevLast = $"{why}（换场景到 {scene} {local:F1}）";
            Plugin.Log.LogInfo($"[虫洞] {DevLast}");
            gm.StartCoroutine(Road(gm, PlayerData.instance, hero, scene, MatePrefix + Num(local.x) + "," + Num(local.y)));
            return true;
        }

        /// <summary>Pylons.RecallTravel's road to a marker: faded out, respawned there standing, the respawn point put back.</summary>
        static IEnumerator Road(GameManager gm, PlayerData pd, HeroController hero, string scene, string marker)
        {
            UI.HudDialogue.Close();
            hero.RelinquishControl();
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            gm.cameraCtrl.FadeOut(GlobalEnums.CameraFadeType.HERO_DEATH);
            yield return new WaitForSeconds(0.6f);
            string savedScene = pd.respawnScene, savedMarker = pd.respawnMarkerName;
            int savedType = pd.respawnType;
            bool savedBench = pd.atBench;
            pd.atBench = false;
            pd.respawnScene = scene;
            pd.respawnMarkerName = marker;
            pd.respawnType = 0;
            try
            {
                gm.ReadyForRespawn(false);
                float until = Time.realtimeSinceStartup + 40f;
                yield return null;
                while (Time.realtimeSinceStartup < until && (gm.IsInSceneTransition || gm.sceneName != scene || !GameHooks.PlayerHasControl()))
                    yield return null;
            }
            finally
            {
                pd.respawnScene = savedScene;
                pd.respawnMarkerName = savedMarker;
                pd.respawnType = savedType;
                pd.atBench = savedBench;
            }
            var h = HeroController.SilentInstance;
            if (h != null) Player.PotionBuffs.Sparkle(h.GetComponent<BoxCollider2D>().bounds.center);
        }

        static Transform spot;

        /// <summary>HeroController.LocateSpawnPoint for a wormhole's marker: the teammate's place, a little over his feet.</summary>
        [HarmonyPostfix, HarmonyPriority(Priority.High)]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.LocateSpawnPoint))]
        static void AtTheMark(ref Transform __result)
        {
            var pd = PlayerData.instance;
            if (__result != null || pd == null || !TryParse(pd.respawnMarkerName, out var local)) return;
            var scene = USM.GetActiveScene();
            var region = TerrainSystem.Find(scene.name);
            Vector2 at = region != null && region.Root != null ? (Vector2)region.Root.TransformPoint(local) : local;
            if (spot == null) spot = new GameObject("TerraKnight Wormhole Spot").transform;
            spot.position = new Vector3(at.x, at.y + 0.7f, 0f);
            __result = spot;
        }

        // ------------------------------------------------------------------ the Potion of Return

        static string returnScene;
        static Vector2 returnLocal;
        static string portalScene;
        static Vector2 portalLocal;
        static SpriteRenderer portal;

        /// <summary>The Potion of Return drunk: where the player stands remembered, home by the Recall's road, a portal there back.</summary>
        public static bool DrinkReturn()
        {
            var hero = HeroController.SilentInstance;
            var region = TerrainSystem.Current?.Region;
            if (hero == null || region == null) return false;
            var feet = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center + Vector2.down * hero.GetComponent<BoxCollider2D>().bounds.extents.y;
            string scene = region.SceneName;
            Vector2 local = region.Root.InverseTransformPoint(feet);
            if (!Pylons.Recall()) return false;
            returnScene = scene;
            returnLocal = local;
            portalScene = null;
            DevReturns++;
            GameManager.UnsafeInstance.StartCoroutine(PortalAtHome());
            return true;
        }

        /// <summary>
        /// Once home: the portal where the player came out (PotionOfReturnGateInHome). Home is wherever the Recall's road
        /// ends (the world's spawn too, for one who never rested on a bench): its scene change begun, then over, the
        /// player in control and the room built.
        /// </summary>
        static IEnumerator PortalAtHome()
        {
            float until = Time.realtimeSinceStartup + 45f;
            var gm = GameManager.UnsafeInstance;
            float began = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < began && (gm == null || !gm.IsInSceneTransition))
            {
                yield return null;
                gm = GameManager.UnsafeInstance;
            }
            yield return new WaitForSeconds(0.5f);
            while (Time.realtimeSinceStartup < until && (gm == null || gm.IsInSceneTransition || !GameHooks.PlayerHasControl() || TerrainSystem.Current?.Region == null))
            {
                yield return null;
                gm = GameManager.UnsafeInstance;
            }
            var hero = HeroController.SilentInstance;
            var region = TerrainSystem.Current?.Region;
            if (hero == null || region == null || returnScene == null) yield break;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            portalScene = region.SceneName;
            portalLocal = region.Root.InverseTransformPoint(new Vector2(b.center.x + 1.5f, b.min.y));
            ShowPortal();
            Plugin.Log.LogInfo($"[返回药水] 传送门在 {portalScene} {portalLocal:F1}，回到 {returnScene} {returnLocal:F1}");
        }

        static void ShowPortal()
        {
            var region = portalScene != null ? TerrainSystem.Find(portalScene) : null;
            if (region == null || region.Root == null) { if (portal != null) portal.gameObject.SetActive(false); return; }
            if (portal == null)
            {
                var go = new GameObject("Terra Return Portal");
                Object.DontDestroyOnLoad(go);
                portal = go.AddComponent<SpriteRenderer>();
                // The Portal Gun's gate (Projectile_602), purple, two tiles tall.
                var tex = Terraria.TerrariaAssets.Texture("Projectile_602");
                if (tex != null)
                {
                    portal.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), tex.height / 1.6f);
                    portal.color = new Color(0.8f, 0.5f, 1f, 1f);
                }
                var heroSr = HeroController.SilentInstance != null ? HeroController.SilentInstance.GetComponent<MeshRenderer>() : null;
                if (heroSr != null) { portal.sortingLayerID = heroSr.sortingLayerID; portal.sortingOrder = heroSr.sortingOrder - 1; }
            }
            portal.gameObject.SetActive(true);
            Vector2 at = region.Root.TransformPoint(portalLocal);
            portal.transform.position = new Vector3(at.x, at.y, 0.01f);
        }

        /// <summary>Each frame of play: the portal shown in its scene (hidden elsewhere), and pulsing.</summary>
        public static void Tick()
        {
            if (portal == null || portalScene == null) return;
            ShowPortal();
            if (portal.gameObject.activeSelf)
            {
                float a = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
                portal.color = new Color(0.8f, 0.5f, 1f, a);
                portal.transform.localScale = Vector3.one * (0.95f + 0.05f * Mathf.Sin(Time.unscaledTime * 3f));
            }
        }

        /// <summary>A right click on the portal within reach: back where the potion was drunk, the portal gone.</summary>
        public static bool TryRightClick(Vector2 mouse)
        {
            var hero = HeroController.SilentInstance;
            if (portal == null || !portal.gameObject.activeSelf || hero == null || returnScene == null) return false;
            var box = portal.bounds;
            var r = new Rect(box.min.x, box.min.y, box.size.x, box.size.y);
            if (!r.Contains(mouse) || Vector2.Distance(hero.transform.position, r.center) > 7f) return false;
            string scene = returnScene;
            var local = returnLocal;
            returnScene = null;
            portalScene = null;
            portal.gameObject.SetActive(false);
            TerraMod.Instance?.PlaySound("Item_6");
            TravelTo(scene, local, "返回药水回到原处");
            return true;
        }

        /// <summary>Dev: where the portal is (world), if it stands.</summary>
        public static Vector2? DevPortal => portal != null && portal.gameObject.activeSelf ? (Vector2?)portal.bounds.center : null;
    }
}
