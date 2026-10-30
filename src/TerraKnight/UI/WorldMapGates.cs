using System;
using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// The way on where two rooms don't fit together on the map. Hollow Knight's rooms don't make one exact picture, so
    /// some gates lie away from the gate they lead into (WorldMapLayout): at each such gate, once its cells are explored
    /// (or its area's map bought), a little arrow in Terraria's map style points out of the room the way the passage goes,
    /// in the colour of the area it leads to. On the full map the mouse over an arrow names where it leads ("Leads to:
    /// Queen's Gardens"), a dotted line runs to the other end and Terraria's map ping marks it; a click there centres the
    /// map on the other end (on its layer). The corner maps show the arrows alone. Gates that meet their partner exactly
    /// carry no arrow: the passage simply goes on.
    /// </summary>
    internal static partial class WorldMap
    {
        sealed class Gate
        {
            public string Scene, Name, To, Entry;
            /// <summary>l r t b, d a door (a partner only: doors are things of their own on the map).</summary>
            public char Side;
            /// <summary>Where it is in its scene, and the point its partner's meets when the two join (cells).</summary>
            public Vector2Int At, Fit;
            public Gate Partner;
            public bool PartnerKnown;
        }

        static Dictionary<string, List<Gate>> gates;

        /// <summary>The gate table (WorldMapGateTable) read, each gate's partner found.</summary>
        static Dictionary<string, List<Gate>> Gates
        {
            get
            {
                if (gates != null) return gates;
                var all = new Dictionary<string, List<Gate>>();
                foreach (var row in AllGateRows())
                {
                    var f = row.Split('|');
                    if (f.Length < 7) continue;
                    var g = new Gate { Scene = f[0], Name = f[1], Side = f[2][0], At = Pair(f[3]), Fit = Pair(f[4]), To = f[5], Entry = f[6] };
                    if (!all.TryGetValue(g.Scene, out var list)) all[g.Scene] = list = new List<Gate>();
                    list.Add(g);
                }
                foreach (var list in all.Values)
                    foreach (var g in list)
                        if (!string.IsNullOrEmpty(g.To) && all.TryGetValue(g.To, out var there))
                            g.Partner = there.Find(p => p.Name == g.Entry);
                return gates = all;
            }
        }

        static IEnumerable<string> AllGateRows()
        {
            foreach (var row in gateRows) yield return row;
            foreach (var row in godhomeGateRows) yield return row;
        }

        static Vector2Int Pair(string s)
        {
            int k = s.IndexOf(',');
            return new Vector2Int(int.Parse(s.Substring(0, k)), int.Parse(s.Substring(k + 1)));
        }

        static bool GateDeep(Gate g) => IsDeepAt(g.Scene, new Vector2(
            (g.At.x + (g.Side == 'r' ? -1f : g.Side == 'l' ? 1f : 0f)) * C,
            (g.At.y + (g.Side == 't' ? -1f : g.Side == 'b' ? 1f : 0f)) * C));

        /// <summary>The gate leads somewhere the map can show and its two sides don't meet there (or are on different layers).</summary>
        static bool NeedsMarker(Gate g)
        {
            if (g.Side == 'd' || string.IsNullOrEmpty(g.To) || !layout.TryGetValue(g.Scene, out var at)) return false;
            if (!layout.TryGetValue(g.To, out var there) || g.Partner == null) return true;
            if (GateDeep(g) != (g.Partner != null ? GateDeep(g.Partner) : IsDeep(g.To))) return true;
            var a = OnMap(g.Scene, at, g.Fit);
            var b = OnMap(g.To, there, g.Partner.Fit);
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) > 2;
        }

        /// <summary>A gate's spot on the map (global cells, the middle of its cell).</summary>
        static Vector2 GateCell(Gate g, Vector2Int at) => OnMap(g.Scene, at, new Vector2(g.At.x + 0.5f, g.At.y + 0.5f));

        /// <summary>The other end on the map, when there is one.</summary>
        static bool OtherEnd(Gate g, out Vector2 cell, out bool deep)
        {
            cell = default;
            deep = false;
            if (g.Partner == null || !layout.TryGetValue(g.To, out var there)) return false;
            cell = GateCell(g.Partner, there);
            deep = GateDeep(g.Partner);
            return true;
        }

        /// <summary>The cell just inside the room by a gate: an arrow shows once the map shows it.</summary>
        static bool GateShown(Room room, Gate g) => GateNear(room, g, false);

        /// <summary>The cells just inside the room by a gate shown on the map (seen: explored, not only charted).</summary>
        static bool GateNear(Room room, Gate g, bool seen)
        {
            if (GateDeep(g) != ViewDeep) return false;
            int x = Mathf.Clamp(g.At.x + (g.Side == 'r' ? -2 : g.Side == 'l' ? 1 : 0), 0, room.W - 1);
            int y = Mathf.Clamp(g.At.y + (g.Side == 't' ? -2 : g.Side == 'b' ? 1 : 0), 0, room.H - 1);
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int cx = x + dx, cy = y + dy;
                    if (cx >= 0 && cy >= 0 && cx < room.W && cy < room.H && (seen ? room.IsSeen(cx, cy) : room.Shows(cx, cy))) return true;
                }
            return false;
        }

        /// <summary>Where a scene leads, as the map names it: a named room's name, else its area's.</summary>
        static string PlaceName(string scene) =>
            RoomNames.TryGetValue(scene ?? "", out var rn) ? Lang.T(rn) : Areas.Display(Areas.AreaName(scene));

        // ---------------------------------------------------------------- drawing

        static Texture2D[] arrows;

        /// <summary>
        /// Terraria-style map arrows (an 11-pixel arrow, white to be tinted, a dark rim), pointing right, left, up, down.
        /// </summary>
        static Texture2D Arrow(char side)
        {
            if (arrows == null)
            {
                arrows = new Texture2D[4];
                for (int k = 0; k < 4; k++) arrows[k] = MakeArrow(k);
            }
            return arrows[side == 'r' ? 0 : side == 'l' ? 1 : side == 't' ? 2 : 3];
        }

        static Texture2D MakeArrow(int dir)
        {
            const int S = 11;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Map gate arrow " + dir };
            // In the frame of an arrow pointing right (u along it, v across; the middle row 5): a head from u 5 to its tip at 9, a shaft behind.
            bool Right(int u, int v)
            {
                int dv = Math.Abs(v - 5);
                if (u >= 5 && u <= 9) return dv <= 9 - u;
                return u >= 1 && u < 5 && dv <= 1;
            }
            bool In(int x, int y)
            {
                if (x < 0 || y < 0 || x >= S || y >= S) return false;
                switch (dir)
                {
                    case 0: return Right(x, y);
                    case 1: return Right(S - 1 - x, y);
                    case 2: return Right(y, x);
                    default: return Right(S - 1 - y, x);
                }
            }
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    if (In(x, y)) px[y * S + x] = new Color32(255, 255, 255, 255);
                    else if (In(x - 1, y) || In(x + 1, y) || In(x, y - 1) || In(x, y + 1)) px[y * S + x] = new Color32(10, 10, 16, 235);
                    else px[y * S + x] = new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        /// <summary>The colour of an arrow: the area it leads to, lifted towards white so it stands out on the dark map.</summary>
        static Color ArrowColor(Gate g) => Color.Lerp(Player.OpeningPortal.AreaColor(Areas.AreaName(g.To)), Color.white, 0.35f);

        /// <summary>Where an arrow is drawn on screen: its tip on the gate, the arrow inside the room.</summary>
        static Rect ArrowRect(Gate g, Vector2 screen, float size)
        {
            var c = screen;
            switch (g.Side)
            {
                case 'r': c.x -= size / 2f; break;
                case 'l': c.x += size / 2f; break;
                case 't': c.y += size / 2f; break;
                case 'b': c.y -= size / 2f; break;
            }
            return new Rect(c.x - size / 2f, c.y - size / 2f, size, size);
        }

        static readonly List<(Gate gate, Rect rect)> drawnGates = new List<(Gate, Rect)>();

        /// <summary>
        /// The arrows of the rooms drawn (the full map's, with the one under the mouse bigger, its line to the other end and
        /// the ping there); returns the one under the mouse.
        /// </summary>
        static Gate DrawGateMarkers(List<(Room room, Rect rect)> visible, Func<Vector2, Vector2> cellToScreen, float size, Rect? clip, Vector2? mouse)
        {
            // Only the full map's arrows (it passes the mouse) are kept for its clicks.
            bool record = mouse.HasValue;
            if (record) drawnGates.Clear();
            Gate hovered = null;
            Rect hoveredRect = default;
            var prev = GUI.color;
            foreach (var (room, _) in visible)
            {
                if (!Gates.TryGetValue(room.Name, out var list)) continue;
                foreach (var g in list)
                {
                    // A gate joined to its partner by a drawn passage (WorldMapPassages) needs no arrow.
                    if (!NeedsMarker(g) || !GateShown(room, g) || Joined(g)) continue;
                    var s = cellToScreen(GateCell(g, room.At));
                    if (clip.HasValue && !clip.Value.Contains(s)) continue;
                    var r = ArrowRect(g, s, size);
                    if (record) drawnGates.Add((g, r));
                    if (mouse.HasValue && hovered == null && r.Contains(mouse.Value)) { hovered = g; hoveredRect = r; continue; }
                    GUI.color = new Color(1f, 1f, 1f, prev.a) * ArrowColor(g);
                    GUI.DrawTexture(r, Arrow(g.Side));
                }
            }
            if (hovered != null)
            {
                // The way to the other end, Terraria's ping on it, then the arrow itself on top, bigger (as a hovered pylon).
                if (OtherEnd(hovered, out var end, out bool deep) && deep == ViewDeep)
                {
                    var from = hoveredRect.center;
                    var to = cellToScreen(end);
                    DottedLine(from, to, new Color(1f, 0.9f, 0.4f, 0.9f), size * 0.22f);
                    Ping(to, size);
                }
                var big = new Rect(hoveredRect.center.x - hoveredRect.width * 0.65f, hoveredRect.center.y - hoveredRect.height * 0.65f, hoveredRect.width * 1.3f, hoveredRect.height * 1.3f);
                GUI.color = ArrowColor(hovered);
                GUI.DrawTexture(big, Arrow(hovered.Side));
            }
            // A jump just made: the place arrived at pinged a moment.
            if (record && jumpedAt.HasValue && Time.unscaledTime - jumpTime < 1.6f) Ping(cellToScreen(jumpedAt.Value), size);
            GUI.color = prev;
            return hovered;
        }

        /// <summary>Squares every few pixels from one point to another, a dark rim under each.</summary>
        static void DottedLine(Vector2 a, Vector2 b, Color color, float dot)
        {
            float len = Vector2.Distance(a, b);
            if (len < 1f) return;
            float step = Mathf.Max(4f, dot * 2.6f);
            int n = Mathf.Min(2000, Mathf.CeilToInt(len / step));
            var prev = GUI.color;
            for (int pass = 0; pass < 2; pass++)
            {
                GUI.color = pass == 0 ? new Color(0f, 0f, 0f, 0.7f) : color;
                float d = pass == 0 ? dot + 2f : dot;
                for (int i = 0; i <= n; i++)
                {
                    var p = Vector2.Lerp(a, b, i / (float)n);
                    GUI.DrawTexture(new Rect(p.x - d / 2f, p.y - d / 2f, d, d), Texture2D.whiteTexture);
                }
            }
            GUI.color = prev;
        }

        /// <summary>Terraria's map ping (Images/UI/MapPing, five frames, ten a second; PingMapLayer) round a point.</summary>
        static void Ping(Vector2 at, float size)
        {
            var tex = TerrariaAssets.Texture("UI/MapPing");
            float ui = GameView.Scale;
            var prev = GUI.color;
            GUI.color = Color.white;
            if (tex != null)
            {
                int frames = 5, frame = (int)(Time.unscaledTime * 10f) % frames;
                float fh = tex.height / (float)frames;
                var r = new Rect(at.x - tex.width * ui / 2f, at.y - fh * ui / 2f, tex.width * ui, fh * ui);
                GUI.DrawTextureWithTexCoords(r, tex, new Rect(0f, 1f - (frame + 1) / (float)frames, 1f, 1f / frames));
            }
            else
            {
                float k = size * (0.6f + 0.4f * Mathf.PingPong(Time.unscaledTime * 2f, 1f));
                GUI.color = new Color(1f, 0.9f, 0.4f, 0.8f);
                GUI.DrawTexture(new Rect(at.x - k / 2f, at.y - k / 2f, k, k), Texture2D.whiteTexture);
            }
            GUI.color = prev;
        }

        static Vector2? jumpedAt;
        static float jumpTime;

        /// <summary>The full map's arrow under a point (as last drawn).</summary>
        static Gate GateAt(Vector2 gui)
        {
            for (int i = drawnGates.Count - 1; i >= 0; i--)
                if (drawnGates[i].rect.Contains(gui)) return drawnGates[i].gate;
            return null;
        }

        /// <summary>A click on an arrow: the map centred on the other end, on its layer, pinged there.</summary>
        static bool JumpThrough(Gate g)
        {
            if (!OtherEnd(g, out var end, out bool deep)) return false;
            pan = end;
            layerChoice = deep == PlayerDeep ? MapLayer.Current : deep ? MapLayer.Deep : MapLayer.Shallow;
            jumpedAt = end;
            jumpTime = Time.unscaledTime;
            TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
            return true;
        }

        /// <summary>The hover line for an arrow: where it leads, and that a click shows the other end.</summary>
        static string GateLabel(Gate g)
        {
            string text = Lang.F("通往：{0}", PlaceName(g.To));
            if (OtherEnd(g, out _, out bool deep))
                text += "　" + (deep != ViewDeep ? Lang.F("点击看那一头（{0}）", Lang.T(deep ? "深层" : "浅层")) : Lang.T("点击看那一头"));
            return text;
        }

        // ---------------------------------------------------------------- dev

        /// <summary>Dev: the gates of a scene the map marks (name, where it leads, the cells between the two ends).</summary>
        public static bool DevGateDeep(string scene, string gate) => Gates.TryGetValue(scene, out var list) && list.Find(g => g.Name == gate) is Gate g && GateDeep(g);

        public static string DevGateMarkers(string scene)
        {
            if (!Gates.TryGetValue(scene ?? "", out var list)) return "没有门";
            var sb = new System.Text.StringBuilder();
            foreach (var g in list)
            {
                if (g.Side == 'd') continue;
                bool mark = NeedsMarker(g);
                string apart = "";
                if (g.Partner != null && layout.TryGetValue(g.Scene, out var a) && layout.TryGetValue(g.To, out var b))
                {
                    var d = OnMap(g.To, b, g.Partner.Fit) - OnMap(g.Scene, a, g.Fit);
                    apart = $" 相差 {d.x:F0},{d.y:F0}";
                }
                sb.Append($" {g.Name}→{g.To}{(mark ? "[标记]" : "[接上]")}{apart}");
            }
            return sb.ToString();
        }

        /// <summary>Dev: how many gates the map marks in all, and how many seamless gate pairs meet.</summary>
        public static string DevGateCounts(out int marked, out int met)
        {
            marked = met = 0;
            int total = 0;
            foreach (var list in Gates.Values)
                foreach (var g in list)
                {
                    if (g.Side == 'd' || string.IsNullOrEmpty(g.To)) continue;
                    total++;
                    if (NeedsMarker(g)) marked++;
                    else met++;
                }
            return $"门 {total} 个：接上 {met}，标出 {marked}";
        }

        /// <summary>Dev: the arrows drawn in the last frame of the full map (count), and where the first of a scene's is (GUI point).</summary>
        public static int DevDrawnGates => drawnGates.Count;

        public static Vector2? DevGateScreen(string scene, string gate)
        {
            foreach (var (g, r) in drawnGates)
                if (g.Scene == scene && g.Name == gate) return r.center;
            return null;
        }

        /// <summary>Dev: centre the open map on a scene's gate.</summary>
        public static bool DevCentreGate(string scene, string gate)
        {
            if (!Gates.TryGetValue(scene ?? "", out var list) || !layout.TryGetValue(scene, out var at)) return false;
            var g = list.Find(x => x.Name == gate);
            if (g == null) return false;
            pan = GateCell(g, at);
            return true;
        }

        /// <summary>Dev: a click on a scene's arrow (as the mouse would): where the map went.</summary>
        public static string DevJumpGate(string scene, string gate)
        {
            if (!Gates.TryGetValue(scene ?? "", out var list)) return "没有门";
            var g = list.Find(x => x.Name == gate);
            if (g == null) return "没有这个门";
            var before = pan;
            bool went = JumpThrough(g);
            return went ? $"从 {before:F0} 到 {pan:F0}（{g.To} 的 {g.Entry}），层 {layerChoice}" : "那一头不在地图上";
        }

        /// <summary>Dev: every gate the map marks: its scene, its name, where it leads, whether that is on the same layer.</summary>
        public static List<(string Scene, string Gate, string To, bool SameLayer)> DevMarkedGates()
        {
            var list = new List<(string, string, string, bool)>();
            foreach (var kv in Gates)
                foreach (var g in kv.Value)
                    if (NeedsMarker(g)) list.Add((g.Scene, g.Name, g.To, g.Partner != null && layout.ContainsKey(g.To) && GateDeep(g.Partner) == GateDeep(g)));
            list.Sort((a, b) => string.CompareOrdinal(a.Item1 + a.Item2, b.Item1 + b.Item2));
            return list;
        }

        /// <summary>Dev: the hover text of a scene's arrow.</summary>
        public static string DevGateLabel(string scene, string gate)
        {
            if (!Gates.TryGetValue(scene ?? "", out var list)) return null;
            var g = list.Find(x => x.Name == gate);
            return g == null ? null : GateLabel(g);
        }
    }
}
