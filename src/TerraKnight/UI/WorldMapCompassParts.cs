using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// The compass's way follows what the save has opened (the user's 10-27 "应该根据当前的时期进行指引…毛里克的指引…指到了科尼法旁边"):
    /// a room whose gates the game's shutters part (WorldMapCompassData, from the compasssurvey suite) is walked only between
    /// gates whose parts are joined now. A shutter the player breaks (a breakable wall or floor) joins its parts both ways; a
    /// lever's gate or a toll gate joins them from the lever's or the machine's side until it is open; one opened from
    /// elsewhere only (the far side of a one-way wall, the Mantis gate) once the save has it open. Each fight is pointed to at
    /// its arena (its reviewed floor, else the boss or the dream warrior). With no fight reachable that way the way is found
    /// over every gate as before.
    /// </summary>
    internal static partial class WorldMap
    {
        sealed class RoomParts
        {
            public readonly Dictionary<string, int> GatePart = new Dictionary<string, int>();
            public readonly List<PartShutter> Shutters = new List<PartShutter>();
            public int Parts;
            /// <summary>[from, to]: walkable now (worked out again when the save opens or shuts something).</summary>
            public bool[,] Reach;
        }

        sealed class PartShutter
        {
            public string Kind, Cond;
            public RectInt Cells;
            public int[] Parts;
            public int Opener;
        }

        static Dictionary<string, RoomParts> roomParts;
        static Dictionary<ItemType, (string Scene, Vector2Int Cell, int Part)> fightSpots;
        static string shutterState;
        /// <summary>No fight reachable through what the save has opened: every gate walks to every other, as before.</summary>
        static bool ignoreShutters;

        static void EnsureParts()
        {
            if (roomParts != null) return;
            roomParts = new Dictionary<string, RoomParts>();
            foreach (var row in compassRoomRows)
            {
                var f = row.Split('|');
                var rp = new RoomParts { Parts = int.Parse(f[2]) };
                foreach (var gp in f[1].Split(','))
                {
                    int k = gp.LastIndexOf(':');
                    if (k > 0) rp.GatePart[gp.Substring(0, k)] = int.Parse(gp.Substring(k + 1));
                }
                roomParts[f[0]] = rp;
            }
            foreach (var row in compassShutterRows)
            {
                var f = row.Split('|');
                if (!roomParts.TryGetValue(f[0], out var rp)) continue;
                var r = f[3].Split(',');
                var parts = System.Array.ConvertAll<string, int>(f[4].Split(','), int.Parse);
                rp.Shutters.Add(new PartShutter
                {
                    Kind = f[1], Cond = f[2], Cells = new RectInt(int.Parse(r[0]), int.Parse(r[1]), int.Parse(r[2]), int.Parse(r[3])),
                    Parts = parts, Opener = int.Parse(f[5]),
                });
            }
            fightSpots = new Dictionary<ItemType, (string, Vector2Int, int)>();
            foreach (var row in compassSpotRows)
            {
                var f = row.Split('|');
                if (!System.Enum.TryParse(f[0], out ItemType fight)) continue;
                var c = f[2].Split(',');
                fightSpots[fight] = (f[1], new Vector2Int(int.Parse(c[0]), int.Parse(c[1])), int.Parse(f[3]));
            }
        }

        /// <summary>Whether the save has a shutter's condition open (every part of an "&amp;" one).</summary>
        static bool CondOpen(string cond)
        {
            var pd = PlayerData.instance;
            foreach (var c in cond.Split('&'))
            {
                if (c.StartsWith("pd:")) { if (pd == null || !pd.GetBool(c.Substring(3))) return false; }
                else if (c.StartsWith("pdnot:")) { if (pd == null || pd.GetBool(c.Substring(6))) return false; }
                else if (c.StartsWith("pb:"))
                {
                    int k = c.IndexOf(':', 3);
                    if (k < 0 || SceneData.instance == null) return false;
                    var data = SceneData.instance.FindMyState(new PersistentBoolData { sceneName = c.Substring(3, k - 3), id = c.Substring(k + 1) });
                    if (data == null || !data.activated) return false;
                }
            }
            return true;
        }

        /// <summary>The shutters read again (every pick of the fight): when the save has opened or shut one, the ways are worked out anew.</summary>
        static void RefreshShutters()
        {
            EnsureParts();
            var sb = new System.Text.StringBuilder();
            foreach (var rp in roomParts.Values)
                foreach (var s in rp.Shutters) sb.Append(CondOpen(s.Cond) ? '1' : '0');
            string now = sb.ToString();
            if (now == shutterState) return;
            shutterState = now;
            foreach (var rp in roomParts.Values) rp.Reach = null;
            ways.Clear();
            playerPartsScene = null;
        }

        static bool[,] ReachOf(RoomParts rp)
        {
            if (rp.Reach != null) return rp.Reach;
            int n = rp.Parts;
            var r = new bool[n, n];
            for (int i = 0; i < n; i++) r[i, i] = true;
            foreach (var s in rp.Shutters)
            {
                bool open = CondOpen(s.Cond);
                foreach (int a in s.Parts)
                    foreach (int b in s.Parts)
                    {
                        if (a == b || a >= n || b >= n) continue;
                        if (open || s.Kind == "break" || (s.Kind == "lever" || s.Kind == "toll" || s.Kind == "oneway") && s.Opener == a) r[a, b] = true;
                    }
            }
            for (int k = 0; k < n; k++)
                for (int i = 0; i < n; i++)
                    if (r[i, k])
                        for (int j = 0; j < n; j++)
                            if (r[k, j]) r[i, j] = true;
            return rp.Reach = r;
        }

        /// <summary>Whether one part of a room walks to another now (any part of a room the shutters don't part).</summary>
        static bool Walks(string scene, int from, int to)
        {
            if (ignoreShutters || from < 0 || to < 0 || from == to) return true;
            EnsureParts();
            if (!roomParts.TryGetValue(scene, out var rp) || from >= rp.Parts || to >= rp.Parts) return true;
            return ReachOf(rp)[from, to];
        }

        static int PartOf(Gate g)
        {
            EnsureParts();
            return roomParts.TryGetValue(g.Scene, out var rp) && rp.GatePart.TryGetValue(g.Name, out int p) ? p : -1;
        }

        /// <summary>The spot a fight is pointed to (its room's cells), when one was surveyed for it in this room.</summary>
        static bool SpotOf(ItemType? fight, string scene, out Vector2Int cell, out int part)
        {
            cell = default;
            part = -1;
            EnsureParts();
            if (!fight.HasValue || !fightSpots.TryGetValue(fight.Value, out var s) || s.Scene != scene) return false;
            cell = s.Cell;
            part = s.Part;
            return true;
        }

        // ---------------------------------------------------------------- the part the player stands in

        static readonly List<int> playerParts = new List<int>();
        static string playerPartsScene;
        static Vector2Int playerPartsCell;
        static float playerPartsAt;
        static int[] partSeen = new int[0], partQueue = new int[0];
        static int partStamp;

        /// <summary>
        /// The parts of the room being played the player can walk to a gate of (empty: every part — a room the shutters don't
        /// part, or the player shut in where no gate is reached): the room's cells walked from the player's, through what the
        /// save has open and the walls the player breaks, worked out again on a new cell at most twice a second.
        /// </summary>
        static List<int> PlayerParts(string scene, Vector2 head)
        {
            if (ignoreShutters) { playerParts.Clear(); return playerParts; }
            EnsureParts();
            if (!roomParts.TryGetValue(scene, out var rp) || !layout.TryGetValue(scene, out var at)) { playerParts.Clear(); return playerParts; }
            var start = Vector2Int.FloorToInt(InScene(scene, at, head));
            if (playerPartsScene == scene && (start == playerPartsCell || Time.unscaledTime < playerPartsAt + 0.5f)) return playerParts;
            playerPartsScene = scene;
            playerPartsCell = start;
            playerPartsAt = Time.unscaledTime;
            playerParts.Clear();
            var live = TerraWorld.Live;
            var grid = live != null && live.SceneName == scene ? live.Grid : null;
            if (grid == null) return playerParts;
            int w = grid.Width, h = grid.Height;
            if (partSeen.Length < w * h) { partSeen = new int[w * h]; partQueue = new int[w * h]; partStamp = 0; }
            partStamp++;
            // Shut: what the save keeps closed and the player can't break (a lever's gate counts from neither side here).
            bool Shut(int x, int y)
            {
                foreach (var s in rp.Shutters)
                    if (s.Kind != "break" && s.Cells.Contains(new Vector2Int(x, y)) && !CondOpen(s.Cond)) return true;
                return false;
            }
            bool Open(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && !grid.IsSolid(x, y) && !Shut(x, y);
            // From the player's cell, or the nearest open one round it.
            int head0 = -1;
            for (int r = 0; r <= 3 && head0 < 0; r++)
                for (int dy = -r; dy <= r && head0 < 0; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (Open(start.x + dx, start.y + dy)) { head0 = (start.y + dy) * w + start.x + dx; break; }
            if (head0 < 0) return playerParts;
            int qh = 0, qt = 0;
            partQueue[qt++] = head0;
            partSeen[head0] = partStamp;
            while (qh < qt)
            {
                int i = partQueue[qh++], x = i % w, y = i / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (!Open(nx, ny)) continue;
                    int n = ny * w + nx;
                    if (partSeen[n] == partStamp) continue;
                    partSeen[n] = partStamp;
                    partQueue[qt++] = n;
                }
            }
            // A gate is reached where its way in first comes to an open cell (not through a shut shutter).
            if (Gates.TryGetValue(scene, out var gates))
                foreach (var g in gates)
                {
                    if (!rp.GatePart.TryGetValue(g.Name, out int part) || playerParts.Contains(part)) continue;
                    var dir = g.Side == 'l' ? new Vector2Int(1, 0) : g.Side == 'r' ? new Vector2Int(-1, 0) : g.Side == 't' ? new Vector2Int(0, -1) : g.Side == 'b' ? new Vector2Int(0, 1) : Vector2Int.zero;
                    var across = dir.x != 0 ? new Vector2Int(0, 1) : new Vector2Int(1, 0);
                    bool reached = false, blocked = false;
                    for (int k = 0; k < 20 && !reached && !blocked; k++)
                    {
                        if (dir == Vector2Int.zero && k > 0) break;
                        for (int lat = -3; lat <= 3; lat++)
                        {
                            var c = g.At + dir * k + across * lat;
                            if (c.x < 0 || c.y < 0 || c.x >= w || c.y >= h) continue;
                            if (lat == 0 && Shut(c.x, c.y)) { blocked = true; break; }
                            if (partSeen[c.y * w + c.x] == partStamp) { reached = true; break; }
                        }
                    }
                    if (reached) playerParts.Add(part);
                }
            return playerParts;
        }

        /// <summary>Whether the player, standing where the parts were worked out, walks to a part of the room being played.</summary>
        static bool PlayerWalks(string scene, List<int> mine, int part)
        {
            if (mine.Count == 0 || part < 0) return true;
            foreach (int q in mine) if (Walks(scene, q, part)) return true;
            return false;
        }

        /// <summary>Dev: the room's parts as the save has them (gates by part, shutters open or shut) and the player's parts.</summary>
        internal static string DevParts(string scene)
        {
            EnsureParts();
            if (!roomParts.TryGetValue(scene, out var rp)) return $"{scene} 不分区";
            var sb = new System.Text.StringBuilder($"{scene}：");
            foreach (var kv in rp.GatePart) sb.Append($" {kv.Key}→{kv.Value}");
            foreach (var s in rp.Shutters) sb.Append($"；{s.Kind} {s.Cond} {(CondOpen(s.Cond) ? "开" : "关")} 连 {string.Join(",", s.Parts)}");
            var head = HeadCell(out bool found);
            if (found && TerraWorld.Live?.SceneName == scene) sb.Append($"；玩家在 {string.Join(",", PlayerParts(scene, head))}");
            return sb.ToString();
        }

        internal static bool DevIgnoringShutters => ignoreShutters;
    }
}
