using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// A LAN game's boss room with its battle gates shut and nobody in it: the fight is over. A player who dies in the
    /// arena leaves it for his respawn, and in a game where the room stays loaded (the other player's, waiting outside;
    /// the seamless world keeps neighbours in) its gates could stay shut for good, the fight they closed for never ending
    /// there. Twice a second, for each boss room loaded here: gates shut (a "BG Control" with its collider on) and no
    /// living player inside its arena (this one, or any other by where his game says he is) for three seconds, and its
    /// gates are sent BG OPEN.
    /// </summary>
    internal static class BossRoomVacancy
    {
        const float Grace = 3f;
        static float next;
        static readonly Dictionary<string, float> emptySince = new Dictionary<string, float>();
        static readonly List<PlayMakerFSM> shut = new List<PlayMakerFSM>();
        public static int DevOpened;
        public static string DevLast = "";
        /// <summary>Dev: the rule in a game of one player too (the test's).</summary>
        public static bool DevAlone;

        static string RoomOf(string scene) => scene != null && scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;

        public static void Tick()
        {
            if (!Net.NetSession.Active && !DevAlone) { emptySince.Clear(); return; }
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            var gm = GameManager.UnsafeInstance;
            if (gm == null || gm.IsInSceneTransition) return;
            // The shut gates of the boss rooms loaded here, by room.
            var rooms = new Dictionary<string, List<PlayMakerFSM>>();
            var all = PlayMakerFSM.FsmList;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == null || f.FsmName != "BG Control" || !f.gameObject.activeInHierarchy) continue;
                string room = RoomOf(f.gameObject.scene.name);
                if (!BossRooms.IsRoom(room) && !BossRooms.IsRoom(f.gameObject.scene.name)) continue;
                var c = f.GetComponent<Collider2D>();
                if (c == null || !c.enabled) continue;
                if (!rooms.TryGetValue(room, out var list)) rooms[room] = list = new List<PlayMakerFSM>();
                list.Add(f);
            }
            var stale = new List<string>();
            foreach (var k in emptySince.Keys) if (!rooms.ContainsKey(k)) stale.Add(k);
            foreach (var k in stale) emptySince.Remove(k);
            foreach (var kv in rooms)
            {
                if (Occupied(kv.Key)) { emptySince.Remove(kv.Key); continue; }
                if (!emptySince.TryGetValue(kv.Key, out float since)) { emptySince[kv.Key] = Time.unscaledTime; continue; }
                if (Time.unscaledTime - since < Grace) continue;
                emptySince.Remove(kv.Key);
                foreach (var gate in kv.Value) gate.SendEvent("BG OPEN");
                DevOpened++;
                DevLast = $"{kv.Key}：{kv.Value.Count} 扇门";
                Plugin.Log.LogInfo($"[联机] {kv.Key} 的战斗门关着、房里没有玩家 {Grace} 秒，打开 {kv.Value.Count} 扇门");
            }
        }

        /// <summary>A living player within the room's arena (its own ground; the whole room where it has none).</summary>
        static bool Occupied(string room)
        {
            var region = TerrainSystem.Find(room) ?? TerrainSystem.Find(room + "_boss");
            Rect? zone = region?.OwnZone;
            bool Inside(Vector2 p)
            {
                if (zone.HasValue) return zone.Value.Contains(p);
                var cell = CellGrid.WorldToCell(p);
                return RoomOf(TerraWorld.RegionAt(cell.x, cell.y)?.SceneName) == room;
            }
            var hero = HeroController.SilentInstance;
            if (hero != null && !hero.cState.dead && Inside(hero.transform.position)) return true;
            foreach (var p in Net.NetSession.Players.Values)
            {
                if (p == null || !p.HasState || !p.Visible || p.Latest.Dead) continue;
                if (RoomOf(p.Latest.Scene) != room && p.Latest.Scene != room + "_boss") continue;
                if (Inside(p.Center)) return true;
            }
            return false;
        }
    }
}
