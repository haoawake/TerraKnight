using HarmonyLib;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.World
{
    internal static partial class Pylons
    {
        static string prevRespawnScene, prevRespawnMarker;
        static int prevRespawnType;

        /// <summary>Where the arriving teleport's pylon stands in the scene now loaded (a little above its base).</summary>
        static bool ArrivingSpot(out Vector3 spot)
        {
            spot = default;
            var p = arrivingAt;
            var gm = GameManager.UnsafeInstance;
            if (p == null || gm == null || gm.sceneName != p.Scene) return false;
            foreach (var o in spawned)
                if (o != null && o.Pylon == p)
                {
                    spot = o.transform.position + new Vector3(WidthCells * CellGrid.CellSize * 0.5f, 1f, 0f);
                    return true;
                }
            var region = TerrainSystem.Find(p.Scene);
            if (region == null) return false;
            spot = region.Root.TransformPoint(p.Local) + new Vector3(WidthCells * CellGrid.CellSize * 0.5f, 1f, 0f);
            return true;
        }

        /// <summary>The bench is there to rest on: active, tagged as a respawn point, its Bench Control running.</summary>
        static bool BenchUsable(string marker)
        {
            foreach (var go in GameObject.FindGameObjectsWithTag("RespawnPoint"))
                if (go.name == marker && go.activeInHierarchy && FSMUtility.LocateFSM(go, "Bench Control") != null) return true;
            return false;
        }

        /// <summary>
        /// Creative: the three toll benches (City of Tears, Queen's Gardens, Ancient Basin) as if paid,
        /// so their benches are there for their pylons. The bench shows by its toll machine's own
        /// saved state (a PersistentBoolItem "Toll Machine Bench" in the scene's data), the PlayerData
        /// flags beside it.
        /// </summary>
        internal static void UnlockTollBenches(PlayerData pd)
        {
            var sd = SceneData.instance;
            if (pd == null || sd == null) return;
            int paid = 0;
            foreach (var scene in new[] { "Ruins1_31", "Fungus3_50", "Abyss_18" })
            {
                var key = new PersistentBoolData { id = "Toll Machine Bench", sceneName = scene };
                var have = sd.FindMyState(key);
                if (have != null && have.activated) continue;
                sd.SaveMyState(new PersistentBoolData { id = "Toll Machine Bench", sceneName = scene, activated = true, semiPersistent = false });
                paid++;
            }
            pd.tollBenchCity = pd.tollBenchQueensGardens = pd.tollBenchAbyss = true;
            if (paid > 0) Plugin.Log.LogInfo($"[晶塔] 创造模式：{paid} 张收费长椅当作已付费");
        }

        public static int DevFadeKicks;

        /// <summary>
        /// The respawn's fade back in (HeroController.Respawn sends RESPAWN to the camera fade) is lost
        /// when the scene comes in before the fade out has got to waiting for it (a quick teleport):
        /// the screen stays black. For a moment after the player is put in, a fade still waiting is
        /// sent it again.
        /// </summary>
        internal static void WatchRespawnFade()
        {
            var gm = GameManager.UnsafeInstance;
            if (gm != null) gm.StartCoroutine(KickFade());
        }

        static System.Collections.IEnumerator KickFade()
        {
            // As long as either clock is in its 1.5 s: a game slowed down in real time (recorded frame by frame) still gets it.
            float until = Time.unscaledTime + 1.5f, untilGame = Time.time + 1.5f;
            while (Time.unscaledTime < until || Time.time < untilGame)
            {
                yield return null;
                var fade = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
                var hero = HeroController.SilentInstance;
                if (fade == null || hero == null || hero.cState.dead) continue;
                if (fade.ActiveStateName != "WaitForRespawn") continue;
                fade.SendEvent("RESPAWN");
                DevFadeKicks++;
                Plugin.Log.LogInfo("[复活点] 镜头还停在等复活的黑屏，补发一次淡入");
                yield break;
            }
        }

        /// <summary>Dev: why a respawn marker is inactive: its inactive ancestors and what is on them.</summary>
        static string WhyInactive(Transform t)
        {
            var sb = new System.Text.StringBuilder();
            for (var p = t; p != null; p = p.parent)
            {
                if (p.gameObject.activeSelf) continue;
                sb.Append($" [{p.name}:");
                foreach (var c in p.GetComponents<Component>()) if (c != null && !(c is Transform)) sb.Append(' ').Append(c.GetType().Name);
                sb.Append(']');
            }
            foreach (var pb in Object.FindObjectsByType<PersistentBoolItem>(FindObjectsSortMode.None))
                if (pb.name.ToLowerInvariant().Contains("toll")) sb.Append($" 收费机 {pb.name}");
            return sb.ToString();
        }

        static Transform spare;

        static Transform Spare(Vector3 at)
        {
            if (spare == null) spare = new GameObject("TerraKnight Respawn Fallback").transform;
            spare.position = at;
            return spare;
        }

        public static int DevSpawnFallbacks;
        public static string DevLastFallback = "";

        /// <summary>
        /// HeroController.LocateSpawnPoint finds the respawn marker among active objects only. When
        /// it isn't there (Hive_01's bench, inactive as the scene comes in) the game puts the player at
        /// (-20000, 20000), and Respawn dies on the missing marker before it gives back control or
        /// pause: an endless fall with the inventory and the menu shut. The marker found inactive, or
        /// the arriving pylon's spot, or any gate of the scene is used instead, and a bench that can't
        /// be sat on brings the player in standing (the bench road would wait on its FSM for ever).
        /// </summary>
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.LocateSpawnPoint))]
        static class SpawnPointFallback
        {
            static void Postfix(ref Transform __result)
            {
                if (__result != null) return;
                var pd = PlayerData.instance;
                var scene = USM.GetActiveScene();
                string marker = pd != null ? pd.respawnMarkerName : null;
                Transform found = null;
                string how = null;
                if (!string.IsNullOrEmpty(marker) && scene.IsValid())
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var t in root.GetComponentsInChildren<Transform>(true))
                            if (t.name == marker && (t.CompareTag("RespawnPoint") || t.GetComponent<RespawnMarker>() != null))
                            {
                                found = t;
                                how = "同名但没激活的复活点（" + WhyInactive(t) + "）";
                                break;
                            }
                        if (found != null) break;
                    }
                if (found == null && ArrivingSpot(out var spot))
                {
                    found = Spare(spot);
                    how = "晶塔的位置";
                }
                if (found == null && scene.IsValid())
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        var gate = root.GetComponentInChildren<TransitionPoint>();
                        if (gate == null) continue;
                        found = Spare(gate.transform.position);
                        how = "门 " + gate.name;
                        break;
                    }
                if (found == null) return;
                if (pd != null && pd.respawnType == 1 && (!found.gameObject.activeInHierarchy || FSMUtility.LocateFSM(found.gameObject, "Bench Control") == null))
                    pd.respawnType = 0;
                __result = found;
                DevSpawnFallbacks++;
                DevLastFallback = $"{scene.name} 的 {marker} 找不到，用{how} {found.position:F1}";
                Plugin.Log.LogWarning($"[复活点] {DevLastFallback}");
            }
        }
    }
}
