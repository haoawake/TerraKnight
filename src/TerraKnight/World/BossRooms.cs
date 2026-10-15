using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Boss rooms are fought as the game made them: nothing is dug, placed, blown up or frozen in
    /// them. A boss room is the room of every boss of the monster book's table (its "_boss" scene
    /// belongs to the room it is loaded into), the Greenpath Hornet's, the Grimm Troupe's tent and
    /// the Colosseum; and any room while a boss or an elite is alive in it (a warrior's ghost once
    /// dreamt up, a Godhome fight). A try there says so over the cursor.
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

        static readonly Dictionary<string, (float At, bool Alive)> alive = new Dictionary<string, (float, bool)>();

        /// <summary>A boss or elite alive and out in the room (checked twice a second).</summary>
        static bool BossAlive(string room)
        {
            if (alive.TryGetValue(room, out var c) && Time.unscaledTime - c.At < 0.5f) return c.Alive;
            bool any = false;
            foreach (var info in Items.EnemyStats.All)
            {
                var hm = info.Hm;
                if (!info.Boss || hm == null || hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy || Items.EnemyStats.Defeated(hm)) continue;
                if (Room(hm.gameObject.scene.name) == room) { any = true; break; }
            }
            alive[room] = (Time.unscaledTime, any);
            return any;
        }

        /// <summary>A boss room for good (not one only while a boss is up in it): its ground is never changed.</summary>
        public static bool IsRoom(string scene) => !string.IsNullOrEmpty(scene) && Rooms().Contains(Room(scene));

        /// <summary>Whether the room this cell lies in may not be changed.</summary>
        public static bool Locked(int gx, int gy)
        {
            var r = TerraWorld.RegionAt(gx, gy);
            if (r == null) return false;
            string room = Room(r.SceneName);
            return Rooms().Contains(room) || BossAlive(room);
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
            UI.CombatText.Show(new Vector3(at.x, at.y + 0.4f, 0f), Lang.T("首领房间里不能改动地形"), UI.CombatText.Notice);
        }

        public static string DevRooms() => string.Join(" ", Rooms());
    }
}
