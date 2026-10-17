using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Bosses are fought where the game made them: nothing is dug, placed, blown up or frozen where a
    /// boss fights. A boss room is the room of every boss of the monster book's table (its "_boss" scene
    /// belongs to the room it is loaded into), the Greenpath Hornet's, the Grimm Troupe's tent, the
    /// Colosseum and every fight of Godhome (its statues' and pantheons' rooms); what stays as it is there is the part the boss reaches (BossArenaZone: what the camera
    /// shows while the fight's lock holds), the rest of the room is changed like any other. A boss up in
    /// any room (a warrior's ghost dreamt up, a Godhome fight) keeps what it reaches the same way, while
    /// it lives. A try there says so over the cursor.
    /// </summary>
    internal static class BossRooms
    {
        static HashSet<string> rooms;
        static readonly string[] Extra = { "Fungus1_04", "Grimm_Main_Tent", "Grimm_Nightmare", "Room_Colosseum_Bronze", "Room_Colosseum_Silver", "Room_Colosseum_Gold" };

        static string Room(string scene) => scene != null && scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;

        static HashSet<string> Rooms()
        {
            if (rooms != null) return rooms;
            rooms = new HashSet<string>(Extra);
            // The warriors' ghosts haunt ordinary rooms: those only while one is up.
            foreach (var (scene, kind) in Items.EnemyDrops.BossScenes())
                if (kind != "warrior_dream" && !string.IsNullOrEmpty(scene)) rooms.Add(Room(scene));
            return rooms;
        }

        static readonly Dictionary<string, (float At, List<Rect> Reach)> alive = new Dictionary<string, (float, List<Rect>)>();

        /// <summary>What each boss alive and out in the room reaches (BossArenaZone.Around; checked twice a second).</summary>
        static List<Rect> BossReach(TerrainRegion region, string room)
        {
            if (alive.TryGetValue(room, out var c) && Time.unscaledTime - c.At < 0.5f) return c.Reach;
            var reach = new List<Rect>();
            var area = new Rect(region.Root.position, new Vector2(region.Grid.Width, region.Grid.Height) * CellGrid.CellSize);
            foreach (var info in Items.EnemyStats.All)
            {
                var hm = info.Hm;
                if (!info.Boss || hm == null || hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy || Items.EnemyStats.Defeated(hm)) continue;
                if (Room(hm.gameObject.scene.name) != room) continue;
                var col = hm.GetComponent<Collider2D>();
                reach.Add(BossArenaZone.Around(hm.gameObject.scene, area, col != null && col.enabled ? (Vector2)col.bounds.center : (Vector2)hm.transform.position));
            }
            alive[room] = (Time.unscaledTime, reach);
            return reach;
        }

        /// <summary>A boss room for good (not one only while a boss is up in it): its ground is never changed.</summary>
        public static bool IsRoom(string scene) => !string.IsNullOrEmpty(scene) && (Rooms().Contains(Room(scene)) || GodhomeFight(scene));

        static readonly Dictionary<string, bool> godhome = new Dictionary<string, bool>();
        /// <summary>Dev: Godhome's fights not counted (as before they were), to compare.</summary>
        internal static bool DevNoGodhome;

        /// <summary>
        /// A room of Godhome's with a fight in it (its boss scene controller): the Hall of Gods' statues and the
        /// pantheons bring their bosses there, the book's own scenes being the world's. Their ground is the game's,
        /// as the bosses' runs, leaps and dives were made for (on the grid's they ended up in a wall or up in the air).
        /// Known once loaded.
        /// </summary>
        static bool GodhomeFight(string scene)
        {
            if (DevNoGodhome || !scene.StartsWith("GG_")) return false;
            if (godhome.TryGetValue(scene, out bool yes)) return yes;
            var s = UnityEngine.SceneManagement.SceneManager.GetSceneByName(scene);
            if (!s.IsValid() || !s.isLoaded) return false;
            foreach (var root in s.GetRootGameObjects())
                if (root.GetComponentInChildren<BossSceneController>(true) != null) { yes = true; break; }
            godhome[scene] = yes;
            return yes;
        }

        /// <summary>Whether this cell may not be changed: in a boss room's fight, or within reach of a boss up.</summary>
        public static bool Locked(int gx, int gy)
        {
            var r = TerraWorld.RegionAt(gx, gy);
            if (r == null) return false;
            string room = Room(r.SceneName);
            if (Rooms().Contains(room) && (r.OwnZone == null || r.IsOwnCell(gx - r.CellOffset.x, gy - r.CellOffset.y))) return true;
            var at = new Vector2((gx + 0.5f) * CellGrid.CellSize, (gy + 0.5f) * CellGrid.CellSize);
            foreach (var reach in BossReach(r, room)) if (reach.Contains(at)) return true;
            return false;
        }

        public static bool Locked(Vector2 world)
        {
            var c = CellGrid.WorldToCell(world);
            return Locked(c.x, c.y);
        }

        static float nextNotice;

        /// <summary>The refusal over the point tried, at most every two seconds.</summary>
        public static void Notice(Vector2 at)
        {
            if (Time.unscaledTime < nextNotice) return;
            nextNotice = Time.unscaledTime + 2f;
            UI.CombatText.Show(new Vector3(at.x, at.y + 0.4f, 0f), Lang.T("首领的战斗范围内不能改动地形"), UI.CombatText.Notice);
        }

        public static string DevRooms() => string.Join(" ", Rooms());
    }
}
