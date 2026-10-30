using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Passages drawn between gates that don't meet on the map (the user's 10-30 "地图要不就按照你说的试一试吧": the loops
    /// Hallownest can't lay flat, joined as Hollow Knight's own map joins them, by drawing). A tunnel in the map's own look,
    /// thin rock walls round dark open space as wide as the narrower doorway (its walls on the doorways' edges), out of
    /// each gate the way it faces and round a bend between, a faint dashed line down it in the colours of the two areas
    /// (Hollow Knight's tram lines). Drawn over its own two rooms' rock (where they lie on each other it is cut through
    /// them) and under every other room, the rooms' open space over it all. Only between gates of one layer at most
    /// <see cref="MaxPassage"/> cells apart, both ends shown (dim while either is only charted); the others keep their
    /// arrows (WorldMapGates).
    /// </summary>
    internal static partial class WorldMap
    {
        /// <summary>The farthest two gate ends a passage joins (map cells, across plus up).</summary>
        const int MaxPassage = 140;

        /// <summary>The narrowest and widest a passage is drawn (map cells), and its walls' thickness.</summary>
        const float PassageMin = 2f, PassageMax = 8f, PassageWall = 1f;

        sealed class Passage
        {
            public Gate A, B;
            public bool Deep;
            /// <summary>Each doorway along its room's edge (its own cells, low to high), measured once its room's cells are there.</summary>
            public Vector2 OpenA = new Vector2(-1f, -1f), OpenB = new Vector2(-1f, -1f);
            /// <summary>Its way as last made ready to draw (map cells), how many points, how wide; its two rooms.</summary>
            public readonly Vector2[] Way = new Vector2[8];
            public int Points;
            public float Width;
            public Room Ra, Rb;
        }

        static List<Passage> passages;
        static Dictionary<Gate, Passage> passageOf;

        /// <summary>Every gate pair a passage joins: both on the map, on one layer, apart there, not too far.</summary>
        static List<Passage> Passages
        {
            get
            {
                if (passages != null) return passages;
                passages = new List<Passage>();
                passageOf = new Dictionary<Gate, Passage>();
                foreach (var list in Gates.Values)
                    foreach (var g in list)
                    {
                        var p = g.Partner;
                        // A gate whose partner is joined already to a gate of its own leads there too (two doorways into one).
                        if (p == null || g.Side == 'd' || p.Side == 'd' || passageOf.ContainsKey(g) || passageOf.ContainsKey(p) && p.Partner == g || !NeedsMarker(g)) continue;
                        if (!layout.TryGetValue(g.Scene, out var a) || !layout.TryGetValue(g.To, out var b) || GateDeep(g) != GateDeep(p)) continue;
                        var d = GateCell(p, b) - GateCell(g, a);
                        if (Mathf.Abs(d.x) + Mathf.Abs(d.y) > MaxPassage) continue;
                        var pass = new Passage { A = g, B = p, Deep = GateDeep(g) };
                        passages.Add(pass);
                        passageOf[g] = passageOf[p] = pass;
                    }
                return passages;
            }
        }

        /// <summary>A passage drawn on the layer in view now: both its rooms there with their cells, both its gates shown.</summary>
        static bool PassageShown(Passage p, out Room ra, out Room rb)
        {
            rb = null;
            if (!rooms.TryGetValue(p.A.Scene, out ra) || p.Deep != ViewDeep || !rooms.TryGetValue(p.B.Scene, out rb)) return false;
            if (!ra.Captured || !rb.Captured || ra.Tiles == null || rb.Tiles == null || !Shown(ra) || !Shown(rb)) return false;
            return GateShown(ra, p.A) && GateShown(rb, p.B);
        }

        /// <summary>The gate is joined to its partner by a passage drawn now (it needs no arrow).</summary>
        static bool Joined(Gate g) => Passages.Count > 0 && passageOf.TryGetValue(g, out var p) && PassageShown(p, out _, out _);

        /// <summary>
        /// A doorway along its room's edge, in its own cells (low, high): the open cells in a line through the gate just
        /// inside the room, at most 12 each way; four cells round the gate where its own cell is rock.
        /// </summary>
        static Vector2 Doorway(Room room, Gate g)
        {
            bool upright = g.Side == 'l' || g.Side == 'r';
            int x = Mathf.Clamp(g.At.x + (g.Side == 'r' ? -1 : g.Side == 'l' ? 1 : 0), 0, room.W - 1);
            int y = Mathf.Clamp(g.At.y + (g.Side == 't' ? -1 : g.Side == 'b' ? 1 : 0), 0, room.H - 1);
            int along = upright ? y : x, n = upright ? room.H : room.W;
            if (!OpenAt(room, upright, x, y, along)) return new Vector2(along - 2f, along + 2f);
            int lo = along, hi = along;
            while (lo > 0 && along - lo < 12 && OpenAt(room, upright, x, y, lo - 1)) lo--;
            while (hi < n - 1 && hi - along < 12 && OpenAt(room, upright, x, y, hi + 1)) hi++;
            return new Vector2(lo, hi + 1f);
        }

        static bool OpenAt(Room room, bool upright, int x, int y, int k) =>
            !IsSolid((Tile)room.Tiles[(upright ? k : y) * room.W + (upright ? x : k)]);

        /// <summary>Where a passage leaves a gate on the map (global cells, the doorway's middle on the room's edge) and how wide it is there.</summary>
        static Vector2 PassageEnd(Room room, Gate g, ref Vector2 open, out float width)
        {
            if (open.x < 0f) open = Doorway(room, g);
            bool upright = g.Side == 'l' || g.Side == 'r';
            var s = StretchOf(g.Scene);
            float k = upright ? s.y : s.x;
            float w = Mathf.Min(open.y - open.x, PassageMax / k);
            float c = Mathf.Clamp((upright ? g.At.y : g.At.x) + 0.5f, open.x + w / 2f, open.y - w / 2f);
            var local = g.Side == 'r' ? new Vector2(Mathf.Min(g.At.x + 1, room.W), c)
                : g.Side == 'l' ? new Vector2(Mathf.Max(g.At.x, 0), c)
                : g.Side == 't' ? new Vector2(c, Mathf.Min(g.At.y + 1, room.H))
                : new Vector2(c, Mathf.Max(g.At.y, 0));
            width = w * k;
            return OnMap(g.Scene, room.At, local);
        }

        static Vector2 OutOf(char side) => side == 'r' ? Vector2.right : side == 'l' ? Vector2.left : side == 't' ? Vector2.up : Vector2.down;

        /// <summary>
        /// A passage's way (map cells, into its <see cref="Passage.Way"/>; returns its points): straight out of each gate,
        /// round one bend halfway where the two face each other, else out a little way from each, then round.
        /// </summary>
        static int Route(Passage p, Room ra, Room rb, out float width)
        {
            var route = p.Way;
            var pa = PassageEnd(ra, p.A, ref p.OpenA, out float wa);
            var pb = PassageEnd(rb, p.B, ref p.OpenB, out float wb);
            width = Mathf.Clamp(Mathf.Min(wa, wb), PassageMin, PassageMax);
            Vector2 da = OutOf(p.A.Side), db = OutOf(p.B.Side);
            bool ha = da.x != 0f, hb = db.x != 0f;
            int n = 0;
            route[n++] = pa;
            if (ha && hb && (pb.x - pa.x) * da.x > 0f && (pa.x - pb.x) * db.x > 0f)
            {
                float mx = (pa.x + pb.x) / 2f;
                route[n++] = new Vector2(mx, pa.y);
                route[n++] = new Vector2(mx, pb.y);
            }
            else if (!ha && !hb && (pb.y - pa.y) * da.y > 0f && (pa.y - pb.y) * db.y > 0f)
            {
                float my = (pa.y + pb.y) / 2f;
                route[n++] = new Vector2(pa.x, my);
                route[n++] = new Vector2(pb.x, my);
            }
            else
            {
                float stub = width + 2f;
                Vector2 qa = pa + da * stub, qb = pb + db * stub;
                route[n++] = qa;
                if (ha && hb)
                {
                    float my = (qa.y + qb.y) / 2f;
                    route[n++] = new Vector2(qa.x, my);
                    route[n++] = new Vector2(qb.x, my);
                }
                else if (!ha && !hb)
                {
                    float mx = (qa.x + qb.x) / 2f;
                    route[n++] = new Vector2(mx, qa.y);
                    route[n++] = new Vector2(mx, qb.y);
                }
                else route[n++] = ha ? new Vector2(qb.x, qa.y) : new Vector2(qa.x, qb.y);
                route[n++] = qb;
            }
            route[n++] = pb;
            return n;
        }

        // The view a passage is drawn into: a cell's place on screen is passageScreen + (cell - passageCell) × passageZoom, y down.
        static Vector2 passageScreen, passageCell;
        static float passageZoom;

        /// <summary>A rectangle round a straight piece of a passage (map cells, r beyond it on every side), filled in the colour set.</summary>
        static void PassageFill(Vector2 p, Vector2 q, float r)
        {
            float x0 = Mathf.Min(p.x, q.x) - r, x1 = Mathf.Max(p.x, q.x) + r, y0 = Mathf.Min(p.y, q.y) - r, y1 = Mathf.Max(p.y, q.y) + r;
            GUI.DrawTexture(new Rect(passageScreen.x + (x0 - passageCell.x) * passageZoom, passageScreen.y - (y1 - passageCell.y) * passageZoom,
                (x1 - x0) * passageZoom, (y1 - y0) * passageZoom), Texture2D.whiteTexture);
        }

        static Color Charted(Color c)
        {
            float k = MapExploration.ChartedAlpha / 255f;
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }

        static readonly List<Passage> drawable = new List<Passage>();
        static int passageStamp;

        /// <summary>A room is an end of a passage drawn now: its rock goes under the passage.</summary>
        static bool InPassage(Room room) => room.PassageStamp == passageStamp;

        /// <summary>
        /// The passages of the layer in view made ready to draw and their rooms marked (<see cref="InPassage"/>):
        /// <paramref name="screen"/> is where the cell <paramref name="centre"/> is drawn, <paramref name="z"/> pixels a
        /// cell, <paramref name="view"/> the part drawn.
        /// </summary>
        static void PreparePassages(Vector2 screen, Vector2 centre, float z, Rect view)
        {
            passageStamp++;
            drawable.Clear();
            passageScreen = screen;
            passageCell = centre;
            passageZoom = z;
            foreach (var p in Passages)
            {
                if (!PassageShown(p, out var ra, out var rb)) continue;
                int n = Route(p, ra, rb, out float width);
                float outer = width / 2f + PassageWall;
                // Off the part drawn: not drawn.
                float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
                for (int i = 0; i < n; i++)
                {
                    x0 = Mathf.Min(x0, p.Way[i].x); x1 = Mathf.Max(x1, p.Way[i].x);
                    y0 = Mathf.Min(y0, p.Way[i].y); y1 = Mathf.Max(y1, p.Way[i].y);
                }
                var box = Rect.MinMaxRect(screen.x + (x0 - outer - centre.x) * z, screen.y - (y1 + outer - centre.y) * z,
                    screen.x + (x1 + outer - centre.x) * z, screen.y - (y0 - outer - centre.y) * z);
                if (!box.Overlaps(view)) continue;
                p.Points = n;
                p.Width = width;
                p.Ra = ra;
                p.Rb = rb;
                ra.PassageStamp = rb.PassageStamp = passageStamp;
                drawable.Add(p);
            }
            DevPassagesDrawn = drawable.Count;
        }

        /// <summary>The passages made ready (<see cref="PreparePassages"/>): walls, open space, the dashed line.</summary>
        static void DrawGatePassages(float alpha)
        {
            if (drawable.Count == 0) return;
            var prev = GUI.color;
            foreach (var p in drawable)
            {
                var route = p.Way;
                int n = p.Points;
                float half = p.Width / 2f, outer = half + PassageWall;
                bool seen = GateNear(p.Ra, p.A, true) && GateNear(p.Rb, p.B, true);
                Color rock = Color.Lerp(TileColor(p.Ra, Tile.Terrain), TileColor(p.Rb, Tile.Terrain), 0.5f);
                Color air = Color.Lerp(TileColor(p.Ra, Tile.Air), TileColor(p.Rb, Tile.Air), 0.5f);
                Color dash = Color.Lerp(ArrowColor(p.B), ArrowColor(p.A), 0.5f);
                if (!seen) { rock = Charted(rock); air = Charted(air); dash = Charted(dash); }
                // Its walls, then its open space over them: the pieces' corners join.
                GUI.color = new Color(rock.r, rock.g, rock.b, alpha);
                for (int i = 0; i + 1 < n; i++) PassageFill(route[i], route[i + 1], outer);
                GUI.color = new Color(air.r, air.g, air.b, alpha);
                for (int i = 0; i + 1 < n; i++) PassageFill(route[i], route[i + 1], half);
                // The dashed line down its middle, on through the bends: a dash of 1.5 cells every 3.
                GUI.color = new Color(dash.r, dash.g, dash.b, 0.55f * alpha);
                float thick = Mathf.Max(0.35f, 1.5f / passageZoom) / 2f, run = 0f;
                for (int i = 0; i + 1 < n; i++)
                {
                    Vector2 a = route[i], b = route[i + 1];
                    float len = Vector2.Distance(a, b);
                    if (len < 0.01f) continue;
                    var dir = (b - a) / len;
                    for (float d = Mathf.Ceil(run / 3f) * 3f; d < run + len; d += 3f)
                    {
                        float s0 = d - run, s1 = Mathf.Min(len, s0 + 1.5f);
                        PassageFill(a + dir * s0, a + dir * s1, thick);
                    }
                    run += len;
                }
            }
            GUI.color = prev;
        }

        // ---------------------------------------------------------------- dev

        /// <summary>Dev: the passages drawn in the last frame of a map (full or corner).</summary>
        public static int DevPassagesDrawn;

        /// <summary>Dev: every passage: its two gates, how far apart, its layer.</summary>
        public static List<string> DevPassages()
        {
            var list = new List<string>();
            foreach (var p in Passages)
            {
                var d = layout.TryGetValue(p.A.Scene, out var a) && layout.TryGetValue(p.B.Scene, out var b) ? GateCell(p.B, b) - GateCell(p.A, a) : Vector2.zero;
                list.Add($"{p.A.Scene}:{p.A.Name} → {p.B.Scene}:{p.B.Name} 相差 {Mathf.Abs(d.x) + Mathf.Abs(d.y):F0}{(p.Deep ? " 深层" : "")}");
            }
            return list;
        }

        /// <summary>Dev: the open map centred halfway along the passage out of a scene's gate; false: no passage there.</summary>
        public static bool DevCentrePassage(string scene, string gate)
        {
            foreach (var p in Passages)
            {
                if (!(p.A.Scene == scene && p.A.Name == gate || p.B.Scene == scene && p.B.Name == gate)) continue;
                if (!layout.TryGetValue(p.A.Scene, out var a) || !layout.TryGetValue(p.B.Scene, out var b)) return false;
                pan = (GateCell(p.A, a) + GateCell(p.B, b)) / 2f;
                layerChoice = p.Deep ? MapLayer.Deep : MapLayer.Shallow;
                return true;
            }
            return false;
        }

        /// <summary>Dev: whether a scene's gate is joined by a passage drawn now.</summary>
        public static bool DevJoined(string scene, string gate) =>
            Gates.TryGetValue(scene ?? "", out var list) && list.Find(g => g.Name == gate) is Gate g && Joined(g);
    }
}
