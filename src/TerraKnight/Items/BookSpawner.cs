using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.Items
{
    /// <summary>
    /// Creative: the monster book's spawn button. A copy of the enemy is put ten tiles in front of
    /// the player. Enemies live in their scenes, so one is copied from a scene that has it: one
    /// already loaded if there is one, else its scene (the roster's) loaded beside the one played
    /// and switched off the moment it loads, before anything in it starts (no music, colours or
    /// lights of its own), the enemy copied out, the scene let go. Bosses may need their arenas and
    /// not do much outside them.
    /// </summary>
    internal static class BookSpawner
    {
        static bool busy;
        static string switchOff;
        public static string DevLast = "";
        public static int DevSpawned;

        public static void Spawn(string key)
        {
            var mod = TerraMod.Instance;
            if (busy || mod == null || string.IsNullOrEmpty(key) || HeroController.SilentInstance == null) return;
            string record = key.Split('@')[0];
            mod.StartCoroutine(Run(record, EnemyDrops.SceneOf(record)));
        }

        static HealthManager Find(string record, Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var hm in root.GetComponentsInChildren<HealthManager>(true))
                    if (hm != null && UI.EnemyHover.RecordOf(hm) == record) return hm;
            return null;
        }

        static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != switchOff) return;
            // Before any Start in it: nothing of it may run beside the scene played.
            foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
        }

        static IEnumerator Run(string record, string sceneName)
        {
            busy = true;
            HealthManager source = null;
            for (int i = 0; i < USM.sceneCount && source == null; i++)
            {
                var s = USM.GetSceneAt(i);
                if (World.TerrainSystem.Find(s.name) != null) source = Find(record, s);
            }
            Scene borrowed = default;
            if (source == null && !string.IsNullOrEmpty(sceneName) && !USM.GetSceneByName(sceneName).isLoaded)
            {
                switchOff = sceneName;
                USM.sceneLoaded += OnLoaded;
                var load = USM.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (load != null) while (!load.isDone) yield return null;
                USM.sceneLoaded -= OnLoaded;
                switchOff = null;
                borrowed = USM.GetSceneByName(sceneName);
                source = Find(record, borrowed);
            }
            var hero = HeroController.SilentInstance;
            var gm = GameManager.UnsafeInstance;
            if (source != null && hero != null && gm != null)
            {
                var src = source.gameObject;
                bool was = src.activeSelf;
                src.SetActive(false);
                var copy = Object.Instantiate(src);
                src.SetActive(was);
                copy.name = src.name + "(图鉴)";
                var here = USM.GetSceneByName(gm.sceneName);
                if (here.IsValid()) USM.MoveGameObjectToScene(copy, here);
                copy.transform.position = Spot(hero, src.transform.position.z);
                copy.SetActive(true);
                DevSpawned++;
                DevLast = $"{record}：从 {source.gameObject.scene.name} 复制，放在 {copy.transform.position:F1}";
                Plugin.Log.LogInfo($"[图鉴] 生成 {DevLast}");
            }
            else
            {
                DevLast = $"{record}：在 {sceneName ?? "（没有场景）"} 里没找到";
                Plugin.Log.LogWarning($"[图鉴] 生成失败 {DevLast}");
                TerraMod.Instance?.PlaySound("Menu_Cancel", 0.7f);
            }
            if (borrowed.IsValid() && borrowed.isLoaded)
            {
                var unload = USM.UnloadSceneAsync(borrowed);
                while (unload != null && !unload.isDone) yield return null;
            }
            busy = false;
        }

        /// <summary>Ten tiles in front of the player, up out of anything solid there.</summary>
        static Vector3 Spot(HeroController hero, float z)
        {
            float facing = hero.cState.facingRight ? 1f : -1f;
            Vector2 at = (Vector2)hero.transform.position + new Vector2(facing * 10f * World.CellGrid.CellSize, 0.5f);
            for (int k = 0; k < 20 && World.TerraWorld.IsSolidAt(at); k++) at.y += World.CellGrid.CellSize;
            return new Vector3(at.x, at.y, z);
        }
    }
}
