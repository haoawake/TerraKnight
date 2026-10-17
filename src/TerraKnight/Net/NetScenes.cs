using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;
using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.Net
{
    /// <summary>
    /// Where things are, the same for everyone. Each player's continuous world puts the scenes
    /// beside the one they entered by a door wherever that world needs them, so world positions
    /// differ from player to player; a scene's own coordinates (where it is as it loads, unmoved)
    /// don't. Everything sent is a scene and a point in that scene's own frame, put back where
    /// that scene is in the reader's world, if they have it loaded.
    /// </summary>
    internal static class NetScenes
    {
        /// <summary>The scene the player is in.</summary>
        public static string Current
        {
            get
            {
                var live = LiveWorld.Instance?.CurrentName;
                if (!string.IsNullOrEmpty(live)) return live;
                return GameManager.UnsafeInstance?.sceneName;
            }
        }

        public static UnityEngine.SceneManagement.Scene FindScene(string name) => Find(name);

        static UnityEngine.SceneManagement.Scene Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return default;
            var s = LiveWorld.Instance != null ? LiveWorld.Instance.SceneOf(name) : default;
            if (s.IsValid() && s.isLoaded) return s;
            s = USceneManager.GetSceneByName(name);
            return s.IsValid() && s.isLoaded ? s : default;
        }

        /// <summary>How far a loaded scene is moved from its own coordinates in this world; false when it isn't loaded.</summary>
        public static bool Shift(string scene, out Vector2 shift)
        {
            shift = default;
            var s = Find(scene);
            if (!s.IsValid()) return false;
            shift = SceneFrames.OfScene(s);
            return true;
        }

        public static bool IsLoaded(string scene) => Find(scene).IsValid();

        /// <summary>A world point as a point of a scene's own frame.</summary>
        public static Vector2 ToLocal(string scene, Vector2 world) => Shift(scene, out var s) ? world - s : world;

        /// <summary>A point of a scene's own frame in this world; false when that scene isn't loaded here.</summary>
        public static bool ToWorld(string scene, Vector2 local, out Vector2 world)
        {
            world = local;
            if (!Shift(scene, out var s)) return false;
            world = local + s;
            return true;
        }

        /// <summary>
        /// The scenes the player is in (the current one and any loaded with it, as a boss room is,
        /// that aren't neighbours of the continuous world) and every other gameplay scene loaded
        /// (the neighbours, kept running beside them).
        /// </summary>
        public static void Collect(List<string> inScenes, List<string> loaded)
        {
            inScenes.Clear();
            loaded.Clear();
            var cur = Current;
            if (!string.IsNullOrEmpty(cur)) inScenes.Add(cur);
            var live = LiveWorld.Instance;
            for (int i = 0; i < USceneManager.sceneCount; i++)
            {
                var s = USceneManager.GetSceneAt(i);
                if (!s.isLoaded || s.name == cur || s.name == "DontDestroyOnLoad" || s.name.StartsWith("Menu_") || s.name == "Quit_To_Menu") continue;
                if (!Gameplay(s)) continue;
                if (live != null && live.IsNeighbour(s.name)) loaded.Add(s.name);
                else inScenes.Add(s.name);
            }
            // Loading a boss room (including its additive boss scene) is not participation.
            // Otherwise a player waiting outside the arena can retain enemy authority forever,
            // and the boss's local intro tries to stage that waiting player for somebody else's fight.
            // But the one whose game runs a fight that is on keeps it while still in the room: the boss's state is in that
            // game alone (a puppet's scripts stand still), and handed over mid-fight it would start again from the other
            // game's stale copy (the False Knight's head left out at 1 hp, his armour back on).
            var hero = HeroController.SilentInstance;
            if (hero != null)
                for (int i = inScenes.Count - 1; i >= 0; i--)
                {
                    string scene = inScenes[i];
                    if (!BossRooms.IsRoom(scene)) continue;
                    string room = scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;
                    var region = TerrainSystem.Find(room) ?? TerrainSystem.Find(scene);
                    if (region?.OwnZone == null || region.OwnZone.Value.Contains(hero.transform.position)) continue;
                    if (Enemies.Owns(scene) && InRoom(hero, room) && FightOn(room)) continue;
                    inScenes.RemoveAt(i);
                    if (!loaded.Contains(scene)) loaded.Add(scene);
                }
        }

        static string RoomOf(string scene) => scene != null && scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;

        /// <summary>The hero within the room's own ground (anywhere in it, the arena or its way in).</summary>
        static bool InRoom(HeroController hero, string room)
        {
            var cell = CellGrid.WorldToCell(hero.transform.position);
            return RoomOf(TerraWorld.RegionAt(cell.x, cell.y)?.SceneName) == room;
        }

        /// <summary>A boss room's fight on in this game: a battle gate of it shut, or one of its bosses alive and hit in the last minute.</summary>
        static bool FightOn(string room)
        {
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (f.FsmName != "BG Control" || RoomOf(f.gameObject.scene.name) != room) continue;
                var c = f.GetComponent<Collider2D>();
                if (c != null && c.enabled) return true;
            }
            foreach (var info in Items.EnemyStats.All)
            {
                var hm = info.Hm;
                if (!info.Boss || hm == null || hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy) continue;
                if (RoomOf(hm.gameObject.scene.name) == room && Time.time - info.LastHit < 60f) return true;
            }
            return false;
        }

        static readonly Dictionary<int, bool> gameplay = new Dictionary<int, bool>();

        static bool Gameplay(UnityEngine.SceneManagement.Scene s)
        {
            if (gameplay.TryGetValue(s.handle, out bool yes)) return yes;
            if (gameplay.Count > 64) gameplay.Clear();
            // A scene of the game's (its SceneManager, or enemies in it): not an empty helper scene.
            foreach (var root in s.GetRootGameObjects())
                if (root.GetComponentInChildren<global::SceneManager>(true) != null || root.GetComponentInChildren<HealthManager>(true) != null)
                {
                    yes = true;
                    break;
                }
            gameplay[s.handle] = yes;
            return yes;
        }
    }
}
