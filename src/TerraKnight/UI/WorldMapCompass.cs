﻿using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// The Wayward Compass (charm 2) worn, with the charm bag on: the nearest of the fights the guide would point to next
    /// (<see cref="BossGuide.NextFights"/>: the stage's fights not won yet), by the way there over the gate table, to the
    /// room the fight is held in (a dream's: the room its dreamer lies in; the Colosseum's trials: the Colosseum; Godhome's:
    /// the Junk Pit from the world, the Hall of Gods from inside). The full map points to it: its relic's picture and its
    /// name where it is, an arrow by the player's head turned to it, and one at the map's edge while it is off the screen.
    /// The minimap (and the overlay map) draws the way there: dots from the player through the gates to it, flowing on.
    /// </summary>
    internal static partial class WorldMap
    {
        const int CompassCharm = 2;
        const string GodhomeOutside = "GG_Waterways", GodhomeInside = "GG_Workshop";
        /// <summary>Going through a gate, in cells walked: the two sides of a gate are one step apart.</summary>
        const float CrossCost = 2f;

        /// <summary>Each fight's room, as one comes to it from the world (none: Godhome's, which depends on where one is).</summary>
        static readonly Dictionary<ItemType, string> compassRooms = new Dictionary<ItemType, string>
        {
            [ItemType.RelicFalseKnight] = "Crossroads_10", [ItemType.RelicBigFly] = "Crossroads_04", [ItemType.RelicMawlek] = "Crossroads_09",
            [ItemType.RelicHornet] = "Fungus1_04", [ItemType.RelicMegaMossCharger] = "Fungus1_29", [ItemType.RelicBigBuzzer] = "Fungus1_20_v02",
            [ItemType.RelicGhostNoEyes] = "Fungus1_35", [ItemType.RelicGhostAladar] = "Cliffs_02",
            [ItemType.RelicMantisLord] = "Fungus2_15", [ItemType.RelicGhostHu] = "Fungus2_32", [ItemType.RelicMageKnight] = "Ruins1_23",
            [ItemType.RelicMageLord] = "Ruins1_24", [ItemType.RelicMegaBeamMiner] = "Mines_18", [ItemType.RelicDungDefender] = "Waterways_05",
            [ItemType.RelicFlukeMother] = "Waterways_12", [ItemType.RelicMegaJellyfish] = "Fungus3_archive_02", [ItemType.RelicGhostMarmu] = "Fungus3_40",
            [ItemType.RelicTraitorLord] = "Fungus3_23", [ItemType.RelicFalseKnightAt5] = "Crossroads_10",
            [ItemType.RelicBlackKnight] = "Ruins2_03", [ItemType.RelicJarCollector] = "Ruins2_11", [ItemType.RelicMageLordAt5] = "Ruins1_24",
            [ItemType.RelicEnragedGuardian] = "Mines_32", [ItemType.RelicMimicSpider] = "Deepnest_32", [ItemType.RelicGhostGalien] = "Deepnest_40",
            [ItemType.RelicHiveKnight] = "Hive_05", [ItemType.RelicGhostXero] = "RestingGrounds_02",
            [ItemType.RelicInfectedKnight] = "Abyss_19", [ItemType.RelicHornetAt4] = "Deepnest_East_Hornet", [ItemType.RelicGhostMarkoth] = "Deepnest_East_10",
            [ItemType.RelicInfectedKnightAt5] = "Abyss_19", [ItemType.RelicZote] = "Room_Colosseum_01", [ItemType.RelicOblobble] = "Room_Colosseum_01",
            [ItemType.RelicLobsterLancer] = "Room_Colosseum_01", [ItemType.RelicPaleLurker] = "GG_Lurker",
            [ItemType.RelicGrimm] = "Grimm_Main_Tent", [ItemType.RelicNightmareGrimm] = "Grimm_Main_Tent", [ItemType.RelicWhiteDefender] = "Waterways_15",
            [ItemType.RelicGreyPrince] = "Room_Bretta_Basement", [ItemType.RelicHollowKnight] = "Room_temple", [ItemType.RelicFinalBoss] = "Room_temple",
        };

        /// <summary>The cost from each gate on (standing by it) to the room's middle, the gate to go through next and where it comes out.</summary>
        sealed class Way
        {
            public string Scene;
            /// <summary>Where the way ends (map cells) and the room's part it is in (-1: any).</summary>
            public Vector2 End;
            public int EndPart = -1;
            public readonly Dictionary<Gate, float> Cost = new Dictionary<Gate, float>();
            public readonly Dictionary<Gate, Gate> Exit = new Dictionary<Gate, Gate>();
            public readonly Dictionary<Gate, Gate> Land = new Dictionary<Gate, Gate>();
        }

        struct WayPoint
        {
            public Vector2 Cell;
            public bool Deep;
            /// <summary>The scene it is in (Hollow Knight's map places it by its scene, WorldMapHollow).</summary>
            public string Scene;
            /// <summary>Come to through a gate whose two sides don't meet on the map: no line to it.</summary>
            public bool Jump;
        }

        static readonly Dictionary<string, Way> ways = new Dictionary<string, Way>();
        /// <summary>Where each gate comes out: its partner, and every gate whose partner it is (a door's row names nowhere, the room behind
        /// it names the door; Dirtmouth's tent door leads to whichever tent is up).</summary>
        static Dictionary<Gate, List<Gate>> across;
        static readonly List<WayPoint> wayPoints = new List<WayPoint>();
        static readonly List<string> wayScenes = new List<string>();
        static ItemType? compassFight;
        static string compassRoom, compassLabel, compassAt, compassHint;
        static float compassCost, compassNext;
        static Texture2D compassIcon, dotTex, arrowTex;
        static readonly Color CompassGold = new Color(1f, 0.82f, 0.32f);

        static bool CompassOn => Charms.Enabled && Charms.Worn(CompassCharm) && TerraMod.Instance != null;

        static bool InGodhome(string scene) => scene != null && scene.StartsWith("GG_") && !scene.StartsWith("GG_Pipeway") && !scene.StartsWith("GG_Waterways") && !scene.StartsWith("GG_Lurker");

        static string CompassRoomOf(ItemType fight, string from)
        {
            if (fight == ItemType.RelicGreyPrince) return GreyPrinceDestination(out _);
            return compassRooms.TryGetValue(fight, out var room) ? room : InGodhome(from) ? GodhomeInside : GodhomeOutside;
        }

        static string GreyPrinceDestination(out string hint)
        {
            hint = Lang.T("目标是布蕾塔家地下室的左特雕像，用梦之钉攻击雕像入梦。若入口未开放，请确认已两次救下左特、救出布蕾塔，并在勇士的试炼击败左特。");
            return "Room_Bretta_Basement";
        }

        /// <summary>Where a fight is pointed to in its room: its surveyed spot (WorldMapCompassData), else the room's middle.</summary>
        static Vector2 CompassEnd(string scene, ItemType? fight)
        {
            // Native Dream Enter object in Room_Bretta_Basement, surveyed from the game's scene.
            if (scene == "Room_Bretta_Basement" && layout.TryGetValue(scene, out var at))
                return OnMap(scene, at, new Vector2(16.29f, 7.03f) / C);
            if (SpotOf(fight, scene, out var cell, out _) && layout.TryGetValue(scene, out at))
                return OnMap(scene, at, new Vector2(cell.x + 0.5f, cell.y + 0.5f));
            return RoomMiddle(scene);
        }

        static bool CellOf(Gate g, out Vector2 cell)
        {
            cell = default;
            if (!layout.TryGetValue(g.Scene, out var at)) return false;
            cell = GateCell(g, at);
            return true;
        }

        /// <summary>Where a room's fight is drawn: its middle (the room's picture's, else its gates').</summary>
        static Vector2 RoomMiddle(string scene)
        {
            if (rooms.TryGetValue(scene, out var room) && room.W > 0) return new Vector2(room.At.x + room.DrawnW / 2f, room.At.y + room.DrawnH / 2f);
            Vector2 sum = Vector2.zero;
            int n = 0;
            if (Gates.TryGetValue(scene, out var list))
                foreach (var g in list)
                    if (CellOf(g, out var c)) { sum += c; n++; }
            if (n > 0) return sum / n;
            return layout.TryGetValue(scene, out var at) ? (Vector2)at : Vector2.zero;
        }

        /// <summary>
        /// The gates' costs to a fight's spot in its room, worked out once for what the save has open (Dijkstra from the spot
        /// back over the gate table, walking in a room only between parts joined now: WorldMapCompassParts).
        /// </summary>
        static Way WayTo(string scene, ItemType? fight)
        {
            string key = fight.HasValue ? scene + "#" + fight.Value : scene;
            if (ways.TryGetValue(key, out var w)) return w;
            w = new Way { Scene = scene, End = CompassEnd(scene, fight) };
            if (SpotOf(fight, scene, out _, out int endPart)) w.EndPart = endPart;
            ways[key] = w;
            if (!Gates.TryGetValue(scene, out var inRoom) || !layout.ContainsKey(scene)) return w;
            EnsureAcross();
            var end = w.End;
            var heap = new List<(float Cost, Gate Gate)>();
            foreach (var g in inRoom)
                if (Walks(scene, PartOf(g), w.EndPart) && CellOf(g, out var c))
                {
                    float d = Vector2.Distance(c, end);
                    w.Cost[g] = d;
                    HeapPush(heap, d, g);
                }
            while (heap.Count > 0)
            {
                var (d, b) = HeapPop(heap);
                if (!w.Cost.TryGetValue(b, out var best) || d > best + 0.001f || !across.TryGetValue(b, out var into)) continue;
                // Through any gate a that comes out at b: from every gate y of a's room, walk to a and go through.
                foreach (var a in into)
                {
                    if (!CellOf(a, out var ac) || !Gates.TryGetValue(a.Scene, out var here)) continue;
                    float through = d + CrossCost;
                    int aPart = PartOf(a);
                    foreach (var y in here)
                    {
                        // Come in by y, walk to a: only where the room's parts join now.
                        if (!Walks(a.Scene, PartOf(y), aPart) || !CellOf(y, out var yc)) continue;
                        float cost = Vector2.Distance(yc, ac) + through;
                        if (w.Cost.TryGetValue(y, out var had) && had <= cost) continue;
                        w.Cost[y] = cost;
                        w.Exit[y] = a;
                        w.Land[y] = b;
                        HeapPush(heap, cost, y);
                    }
                }
            }
            return w;
        }

        static void EnsureAcross()
        {
            if (across != null) return;
            across = new Dictionary<Gate, List<Gate>>();
            void Link(Gate from, Gate to)
            {
                if (!across.TryGetValue(from, out var list)) across[from] = list = new List<Gate>();
                if (!list.Contains(to)) list.Add(to);
            }
            foreach (var list in Gates.Values)
                foreach (var g in list)
                    if (g.Partner != null) { Link(g, g.Partner); Link(g.Partner, g); }
        }

        static void HeapPush(List<(float Cost, Gate Gate)> heap, float cost, Gate g)
        {
            heap.Add((cost, g));
            int i = heap.Count - 1;
            while (i > 0)
            {
                int up = (i - 1) / 2;
                if (heap[up].Cost <= heap[i].Cost) break;
                (heap[up], heap[i]) = (heap[i], heap[up]);
                i = up;
            }
        }

        static (float Cost, Gate Gate) HeapPop(List<(float Cost, Gate Gate)> heap)
        {
            var top = heap[0];
            heap[0] = heap[heap.Count - 1];
            heap.RemoveAt(heap.Count - 1);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, least = i;
                if (l < heap.Count && heap[l].Cost < heap[least].Cost) least = l;
                if (r < heap.Count && heap[r].Cost < heap[least].Cost) least = r;
                if (least == i) break;
                (heap[least], heap[i]) = (heap[i], heap[least]);
                i = least;
            }
            return top;
        }

        /// <summary>From a point of a room: the cost to the way's room, the gate to go through first and where it comes out (none: in it already, or no way).</summary>
        static float StartCost(Way w, string scene, Vector2 at, out Gate exit) => StartCost(w, scene, at, out exit, out _);

        static float StartCost(Way w, string scene, Vector2 at, out Gate exit, out Gate land)
        {
            exit = land = null;
            // The parts of this room the player walks to (empty: any).
            var mine = PlayerParts(scene, at);
            if (scene == w.Scene && PlayerWalks(scene, mine, w.EndPart)) return Vector2.Distance(at, w.End);
            float best = float.MaxValue;
            if (!Gates.TryGetValue(scene, out var here)) return best;
            EnsureAcross();
            foreach (var a in here)
            {
                if (!PlayerWalks(scene, mine, PartOf(a)) || !across.TryGetValue(a, out var outs) || !CellOf(a, out var ac)) continue;
                foreach (var b in outs)
                {
                    if (!w.Cost.TryGetValue(b, out var rest)) continue;
                    float cost = Vector2.Distance(at, ac) + CrossCost + rest;
                    if (cost < best) { best = cost; exit = a; land = b; }
                }
            }
            return best;
        }

        /// <summary>
        /// The fight pointed to, chosen again every two seconds: the nearest of the next fights by the way there (one with no way
        /// found only when none has one). The one pointed to is kept unless another is clearly nearer, so it doesn't flicker.
        /// </summary>
        static void PickFight(string scene, Vector2 at)
        {
            if (Time.unscaledTime < compassNext && compassAt == scene && compassFight.HasValue) return;
            compassNext = Time.unscaledTime + 2f;
            // Each pick tries the ways through what is open now first.
            if (ignoreShutters) { ignoreShutters = false; ways.Clear(); playerPartsScene = null; }
            RefreshShutters();
            compassAt = scene;
            ItemType? pick = null;
            string room = null;
            float best = float.MaxValue, kept = float.MaxValue;
            var fights = DevCompassOnly.HasValue ? new List<ItemType> { DevCompassOnly.Value } : BossGuide.NextFights(TerraMod.Instance);
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var f in fights)
                {
                    string r = CompassRoomOf(f, scene);
                    if (!layout.ContainsKey(r)) continue;
                    float cost = StartCost(WayTo(r, f), scene, at, out _);
                    if (compassFight == f && compassRoom == r) kept = cost;
                    if (pick == null || cost < best) { best = cost; pick = f; room = r; }
                }
                // None reachable through what is open: the way over every gate, as one that may need a wall or door opened.
                if (best < float.MaxValue || ignoreShutters || fights.Count == 0) break;
                ignoreShutters = true;
                ways.Clear();
                playerPartsScene = null;
                pick = null; room = null; best = kept = float.MaxValue;
            }
            if (pick.HasValue && compassFight.HasValue && kept < float.MaxValue && best > kept * 0.85f && (pick != compassFight || room != compassRoom))
            {
                pick = compassFight;
                room = compassRoom;
                best = kept;
            }
            compassCost = best;
            compassHint = null;
            if (pick == ItemType.RelicGreyPrince) GreyPrinceDestination(out compassHint);
            if (pick == compassFight && room == compassRoom) return;
            compassFight = pick;
            compassRoom = room;
            compassLabel = null;
            compassIcon = null;
            if (!pick.HasValue) return;
            var def = ItemDb.Get(pick.Value);
            string name = def != null ? def.Name.Replace("圣物", "").Replace(" Relic", "").Trim() : pick.Value.ToString();
            compassLabel = Lang.F("下一个首领：{0}", name);
            if (def != null && def.Texture != null) compassIcon = Relics.NativeArt(def.Texture) ? Relics.SculptureTexture(def.Texture) : Relics.Texture(def.Texture);
            Plugin.Log.LogInfo($"[指南针] 指向 {pick.Value}（{room}），从 {scene} 走 {(best < float.MaxValue ? best.ToString("F0") + " 格" : "找不到路")}{(ignoreShutters ? "（按已打开的路走不到，不管没开的墙和门）" : "")}");
        }

        /// <summary>The way from the player to the fight pointed to, as map points (wayPoints); false: no fight, or no way from here.</summary>
        static bool BuildWay()
        {
            wayPoints.Clear();
            wayScenes.Clear();
            if (!CompassOn) return false;
            string scene = TerraWorld.Live?.SceneName;
            var head = HeadCell(out bool found);
            if (!found || scene == null || !layout.ContainsKey(scene)) return false;
            PickFight(scene, head);
            if (!compassFight.HasValue || compassRoom == null) return false;
            var w = WayTo(compassRoom, compassFight);
            wayPoints.Add(new WayPoint { Cell = head, Deep = PlayerDeep, Scene = scene });
            wayScenes.Add(scene);
            // Straight to the spot when it is in the player's part of this room; else out through the gates.
            if (StartCost(w, scene, head, out var g, out var land) >= float.MaxValue) return false;
            if (g != null)
            {
                for (int guard = 0; g != null && land != null && guard < 400; guard++)
                {
                    if (!CellOf(g, out var gc) || !CellOf(land, out var bc)) return false;
                    bool gDeep = GateDeep(g), bDeep = GateDeep(land);
                    wayPoints.Add(new WayPoint { Cell = gc, Deep = gDeep, Scene = g.Scene });
                    wayPoints.Add(new WayPoint { Cell = bc, Deep = bDeep, Jump = gDeep != bDeep || (gc - bc).sqrMagnitude > 9f, Scene = land.Scene });
                    wayScenes.Add(land.Scene);
                    var from = land;
                    w.Exit.TryGetValue(from, out g);
                    w.Land.TryGetValue(from, out land);
                }
            }
            wayPoints.Add(new WayPoint { Cell = w.End, Deep = IsDeep(w.Scene), Scene = w.Scene });
            return true;
        }

        static void EnsureCompassTextures()
        {
            if (dotTex == null)
            {
                dotTex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "TerraKnight/Compass dot", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f)) / 16f;
                        dotTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 3f)));
                    }
                dotTex.Apply();
            }
            if (arrowTex == null)
            {
                // A triangle pointing right: its tip at the right edge, its base upright a little in from the left.
                arrowTex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "TerraKnight/Compass arrow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float u = (x + 0.5f) / 32f, v = Mathf.Abs((y + 0.5f) / 16f - 1f);
                        float edge = Mathf.Min((1f - u) * 0.85f - v, u - 0.15f);
                        arrowTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(edge * 20f)));
                    }
                arrowTex.Apply();
            }
        }

        static void Dot(Vector2 at, float size, Color color)
        {
            GUI.color = new Color(0f, 0f, 0f, color.a * 0.7f);
            GUI.DrawTexture(new Rect(at.x - size * 0.75f, at.y - size * 0.75f, size * 1.5f, size * 1.5f), dotTex);
            GUI.color = color;
            GUI.DrawTexture(new Rect(at.x - size / 2f, at.y - size / 2f, size, size), dotTex);
        }

        static void Arrow(Vector2 at, Vector2 dir, float size, Color color)
        {
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, at);
            GUI.color = new Color(0f, 0f, 0f, color.a * 0.7f);
            GUI.DrawTexture(new Rect(at.x - size * 0.6f, at.y - size * 0.6f, size * 1.2f, size * 1.2f), arrowTex);
            GUI.color = color;
            GUI.DrawTexture(new Rect(at.x - size / 2f, at.y - size / 2f, size, size), arrowTex);
            GUI.matrix = m;
        }

        static void CompassIcon(Vector2 at, float size, float alpha)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            GUI.color = new Color(CompassGold.r, CompassGold.g, CompassGold.b, alpha * (0.35f + 0.3f * pulse));
            float glow = size * (1.5f + 0.25f * pulse);
            GUI.DrawTexture(new Rect(at.x - glow / 2f, at.y - glow / 2f, glow, glow), dotTex);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            if (compassIcon != null)
            {
                float h = size, wdt = size * compassIcon.width / Mathf.Max(1f, compassIcon.height);
                GUI.DrawTexture(new Rect(at.x - wdt / 2f, at.y - h / 2f, wdt, h), compassIcon);
            }
        }

        /// <summary>Cuts the segment a-b to a rectangle: the part's ends as fractions of it; false: none of it inside.</summary>
        static bool ClipSegment(Vector2 a, Vector2 b, Rect r, out float t0, out float t1)
        {
            t0 = 0f;
            t1 = 1f;
            var d = b - a;
            float[] p = clipP, q = clipQ;
            p[0] = -d.x; q[0] = a.x - r.xMin;
            p[1] = d.x; q[1] = r.xMax - a.x;
            p[2] = -d.y; q[2] = a.y - r.yMin;
            p[3] = d.y; q[3] = r.yMax - a.y;
            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Abs(p[i]) < 1e-6f) { if (q[i] < 0f) return false; continue; }
                float t = q[i] / p[i];
                if (p[i] < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
            }
            return t0 <= t1;
        }
        static readonly float[] clipP = new float[4], clipQ = new float[4];

        /// <summary>The minimap's (or the overlay's) way there: the dots along it, flowing on to the fight, and the fight's picture.</summary>
        static void DrawCompassWay(System.Func<Vector2, Vector2> toScreen, Rect clip, float ui, float alpha)
        {
            compassDots = 0;
            compassRouteBuilt = BuildWay();
            if (!compassRouteBuilt || wayPoints.Count < 2) return;
            EnsureCompassTextures();
            var prev = GUI.color;
            float spacing = 7.5f * ui, size = 3.8f * ui;
            float phase = Time.unscaledTime * 14f * ui % spacing;
            var bounds = new Rect(clip.x - size, clip.y - size, clip.width + size * 2f, clip.height + size * 2f);
            var color = new Color(CompassGold.r, CompassGold.g, CompassGold.b, alpha);
            float run = 0f;
            int dots = 0;
            for (int i = 1; i < wayPoints.Count && dots < 800; i++)
            {
                var from = wayPoints[i - 1];
                var to = wayPoints[i];
                if (to.Jump)
                {
                    var door = toScreen(from.Cell);
                    if (from.Deep == ViewDeep && clip.Contains(door))
                    {
                        Dot(door, 8f * ui, color);
                        if (Open) FullMap.Outlined(new Rect(door.x + 9f * ui, door.y - 10f * ui, 240f * ui, 24f * ui), Lang.T(from.Deep != to.Deep ? "从此处换层" : "从此处进入下一房间"), color);
                    }
                    continue;
                }
                Vector2 a = toScreen(from.Cell), b = toScreen(to.Cell);
                float len = Vector2.Distance(a, b);
                if (len < 0.01f) continue;
                if (from.Deep == ViewDeep && to.Deep == ViewDeep && ClipSegment(a, b, bounds, out float t0, out float t1))
                {
                    float s = Mathf.Ceil((run + t0 * len - phase) / spacing) * spacing + phase;
                    for (; s <= run + t1 * len && dots < 800; s += spacing, dots++)
                        Dot(Vector2.Lerp(a, b, (s - run) / len), size, color);
                }
                run += len;
            }
            var end = wayPoints[wayPoints.Count - 1];
            compassDots = dots;
            var endAt = toScreen(end.Cell);
            if (end.Deep == ViewDeep && clip.Contains(endAt)) CompassIcon(endAt, 20f * ui, alpha);
            GUI.color = prev;
        }

        static GUIStyle compassHintStyle;
        static int compassDots, compassFullDots;
        static bool compassRouteBuilt;

        /// <summary>The fullscreen map uses the same gate route as the minimap, with an actionable destination card.</summary>
        static void DrawCompassRoute(float ui)
        {
            compassFullDots = 0;
            if (!CompassOn) return;
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            DrawCompassWay(ToScreen, screen, ui, 1f);
            compassFullDots = compassDots;
            DrawCompassCard(ui);
        }

        /// <summary>The full map's card for the fight pointed to: its name and what to do.</summary>
        static void DrawCompassCard(float ui)
        {
            if (!compassFight.HasValue && string.IsNullOrEmpty(compassHint)) return;
            var prev = GUI.color;
            if (compassHintStyle == null) compassHintStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            compassHintStyle.fontSize = Mathf.RoundToInt(16f * ui);
            float width = Mathf.Min(900f * ui, Screen.width - 32f * ui);
            string note = compassHint ?? Lang.T("沿金色路线前进，光点标出房间出口；路线按房间连接，不代表可以穿过墙壁。");
            if (!compassRouteBuilt && compassFight.HasValue) note = Lang.T("暂未找到连通路线，请先探索连接区域。") + "\n" + note;
            if (compassFight.HasValue && elsewhere == null && IsDeep(compassRoom) != ViewDeep)
                note += "\n" + Lang.T("目标位于另一地图层；从路线标出的门进入，或切换浅层/深层查看后续路线。");
            float bodyHeight = compassHintStyle.CalcHeight(new GUIContent(note), width - 24f * ui);
            float height = bodyHeight + 46f * ui;
            var card = new Rect(16f * ui, Screen.height - 96f * ui - height, width, height);
            GUI.color = new Color(.06f, .08f, .13f, .94f);
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = prev;
            FullMap.Outlined(new Rect(card.x + 12f * ui, card.y + 6f * ui, width - 24f * ui, 26f * ui), compassLabel ?? Lang.T("任性指南针"), CompassGold);
            GUI.color = Color.white;
            GUI.Label(new Rect(card.x + 12f * ui, card.y + 34f * ui, width - 24f * ui, bodyHeight), note, compassHintStyle);
            GUI.color = prev;
        }

        /// <summary>Dev: the fight pointed to, its room, the way's cost and the rooms it goes through.</summary>
        internal static string DevCompass()
        {
            bool built = BuildWay();
            return $"{(compassFight.HasValue ? compassFight.Value.ToString() : "无")} 房间 {compassRoom ?? "无"} 路程 {(compassCost < float.MaxValue ? compassCost.ToString("F0") : "无路")} 路线 {(built ? string.Join(" → ", wayScenes) : "没画")}（{wayPoints.Count} 点）";
        }

        /// <summary>Dev: the way's last point is the fight's room middle, and each step of it goes on from a gate to its partner.</summary>
        internal static int DevCompassPoints => wayPoints.Count;
        internal static string DevCompassRoom => compassRoom;
        internal static ItemType? DevCompassFight => compassFight;
        internal static int DevFullCompassDots => compassFullDots;
        internal static string DevCompassHintOf(ItemType fight) { if (fight == ItemType.RelicGreyPrince) { GreyPrinceDestination(out var hint); return hint; } return null; }
        internal static Vector2 DevCompassEnd => compassRoom != null ? CompassEnd(compassRoom, compassFight) : Vector2.zero;
        internal static void DevCompassForget() { compassFight = null; compassRoom = null; compassNext = 0f; }
        /// <summary>Dev: the compass points to this fight only.</summary>
        internal static ItemType? DevCompassOnly;
        /// <summary>Dev: a point of the room being played (world units) on the map, as the player's head is placed.</summary>
        internal static Vector2 DevLiveCell(Vector2 world)
        {
            var live = TerraWorld.Live;
            return live != null && live.SceneName != null && layout.TryGetValue(live.SceneName, out var at)
                ? OnMap(live.SceneName, at, new Vector2(world.x / C - live.CellOffset.x, world.y / C - live.CellOffset.y)) : Vector2.zero;
        }
        /// <summary>Dev (suite compasssurvey): every scene with gates; a scene's gates (name, side, cell); each fight's room.</summary>
        internal static List<string> DevGateScenes => new List<string>(Gates.Keys);
        internal static List<(string Name, char Side, Vector2Int At)> DevGatesOf(string scene) =>
            Gates.TryGetValue(scene, out var list) ? list.ConvertAll(g => (g.Name, g.Side, g.At)) : new List<(string, char, Vector2Int)>();
        internal static Dictionary<ItemType, string> DevCompassRooms => new Dictionary<ItemType, string>(compassRooms);
        /// <summary>Dev: a point of a scene (its own units, as it loads unmoved) on the map, by its laid-out place and terrain offset when loaded.</summary>
        internal static Vector2 DevSceneCell(string scene, Vector2 local)
        {
            var live = TerraWorld.Live;
            var off = live != null && live.SceneName == scene ? (Vector2)live.CellOffset : Vector2.zero;
            return layout.TryGetValue(scene, out var at) ? OnMap(scene, at, new Vector2(local.x / C - off.x, local.y / C - off.y)) : Vector2.zero;
        }
        /// <summary>Dev: the full map centred on a global cell.</summary>
        internal static void DevPanTo(Vector2 cell) => pan = cell;
        /// <summary>Dev: a room's place and size on the map.</summary>
        internal static string DevRoomRect(string scene) => rooms.TryGetValue(scene, out var r) ? $"{r.At} {r.W}x{r.H}" : layout.TryGetValue(scene, out var at) ? $"{at} 无图" : "不在地图上";
        internal static List<string> DevCompassScenes => wayScenes;
        internal static string DevCompassRoomOf(ItemType fight, string from) => CompassRoomOf(fight, from);
        internal static bool DevLaidOut(string scene) => layout.ContainsKey(scene) && Gates.ContainsKey(scene);
        /// <summary>Dev: the way's cost to a room from where the player is.</summary>
        internal static float DevCostFromHere(string room)
        {
            var head = HeadCell(out bool found);
            string scene = TerraWorld.Live?.SceneName;
            return found && scene != null && layout.ContainsKey(room) ? StartCost(WayTo(room, compassRoom == room ? compassFight : null), scene, head, out _) : float.MaxValue;
        }
    }
}
