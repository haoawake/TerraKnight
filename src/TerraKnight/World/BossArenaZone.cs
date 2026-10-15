using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TerraKnight.World
{
    /// <summary>
    /// The part of a room a boss fights in, what it can reach (World.BossRooms keeps only that as the game made
    /// it). What the camera shows while a camera lock with the boss in it holds: CameraLockArea's bounds for the
    /// camera's centre and half a view round them (14.6 × 8.3, CameraController.LockToArea; −1 is the room's
    /// edge), for a boss in view and for the middle between the room's battle gates the smallest lock holding it (the
    /// arena's own, not one over the whole room); without such a lock, the
    /// span of the gates from their ground (found under them: an open gate stands raised) to a view over it; without
    /// gates, a view round the boss. A margin round all of it for what goes a little past the view.
    /// </summary>
    internal static class BossArenaZone
    {
        const float HalfW = 14.6f, HalfH = 8.3f, Margin = 2f;

        /// <summary>The fight's part of a boss room at its load (null: nothing to go by, the whole room counts).</summary>
        public static Rect? OfRoom(Scene scene, Rect room, out string how)
        {
            var bosses = Bosses(scene);
            var gates = Gates(scene);
            how = "";
            // Where the fight is: the bosses, and the middle between the gates on their ground.
            var marks = new List<Vector2>(bosses);
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var (x, ground, top) in gates)
            {
                x0 = Mathf.Min(x0, x);
                x1 = Mathf.Max(x1, x);
                y0 = Mathf.Min(y0, ground);
                y1 = Mathf.Max(y1, top);
            }
            if (gates.Count >= 2) marks.Add(new Vector2((x0 + x1) / 2f, y0 + 2f));
            // For each mark the smallest lock it is in (the arena's own, not a lock over the whole room that also holds it).
            Rect? zone = null;
            int locks = 0;
            var views = LockViews(scene, room);
            foreach (var b in marks)
            {
                Rect? least = null;
                foreach (var view in views)
                    if (view.Contains(b) && (least == null || view.width * view.height < least.Value.width * least.Value.height)) least = view;
                if (least == null) continue;
                zone = Union(zone, least.Value);
                locks++;
            }
            if (zone != null) how = $"镜头锁定 {locks} 处";
            if (zone == null && gates.Count >= 2)
            {
                // From the gates' ground to a view over it.
                zone = Rect.MinMaxRect(x0, y0 - 2f, x1, Mathf.Max(y1, y0 + 2f * HalfH));
                how = $"战斗门 {gates.Count} 道";
            }
            if (zone == null && bosses.Count > 0)
            {
                foreach (var b in bosses) zone = Union(zone, new Rect(b.x - HalfW, b.y - HalfH, 2f * HalfW, 2f * HalfH));
                how = $"首领周围（{bosses.Count} 个）";
            }
            if (zone == null)
            {
                how = "找不到首领、镜头锁定和战斗门";
                return null;
            }
            var z = Grow(zone.Value, Margin);
            // Within the room.
            return Rect.MinMaxRect(Mathf.Max(z.xMin, room.xMin), Mathf.Max(z.yMin, room.yMin), Mathf.Min(z.xMax, room.xMax), Mathf.Min(z.yMax, room.yMax));
        }

        /// <summary>Round a boss up (a ghost dreamt up, a boss come in, one out of its arena): the view of the smallest lock it is in, else a view round it.</summary>
        public static Rect Around(Scene scene, Rect room, Vector2 at)
        {
            // The smallest lock it is in (the arena's own, not one over the whole room).
            Rect? zone = null;
            foreach (var view in LockViews(scene, room))
                if (view.Contains(at) && (zone == null || view.width * view.height < zone.Value.width * zone.Value.height)) zone = view;
            return Grow(zone ?? new Rect(at.x - HalfW, at.y - HalfH, 2f * HalfW, 2f * HalfH), Margin);
        }

        /// <summary>The bosses of the scene, up or still waiting (inactive), by the monster book's kinds.</summary>
        static List<Vector2> Bosses(Scene scene)
        {
            var list = new List<Vector2>();
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hm == null || hm.gameObject.scene != scene) continue;
                if (!Items.EnemyStats.IsBossKind(UI.EnemyHover.RecordOf(hm), hm.hp)) continue;
                var c = hm.GetComponent<Collider2D>();
                list.Add(c != null && c.enabled && c.gameObject.activeInHierarchy ? (Vector2)c.bounds.center : (Vector2)hm.transform.position);
            }
            return list;
        }

        /// <summary>What the camera shows under each of the scene's lock areas.</summary>
        static List<Rect> LockViews(Scene scene, Rect room)
        {
            var list = new List<Rect>();
            foreach (var la in Object.FindObjectsByType<CameraLockArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (la == null || la.gameObject.scene != scene) continue;
                float xMin = la.cameraXMin == -1f ? room.xMin + HalfW : la.cameraXMin, xMax = la.cameraXMax == -1f ? room.xMax - HalfW : la.cameraXMax;
                float yMin = la.cameraYMin == -1f ? room.yMin + HalfH : la.cameraYMin, yMax = la.cameraYMax == -1f ? room.yMax - HalfH : la.cameraYMax;
                if (xMin == 0f && xMax == 0f && yMin == 0f && yMax == 0f)
                {
                    // No bounds given: the camera follows within the trigger.
                    var col = la.GetComponent<BoxCollider2D>();
                    if (col == null) continue;
                    Vector2 c = la.transform.TransformPoint(col.offset), s = Vector2.Scale(col.size, la.transform.lossyScale);
                    list.Add(new Rect(c.x - Mathf.Abs(s.x) / 2f - HalfW, c.y - Mathf.Abs(s.y) / 2f - HalfH, Mathf.Abs(s.x) + 2f * HalfW, Mathf.Abs(s.y) + 2f * HalfH));
                    continue;
                }
                list.Add(Rect.MinMaxRect(Mathf.Min(xMin, xMax) - HalfW, Mathf.Min(yMin, yMax) - HalfH, Mathf.Max(xMin, xMax) + HalfW, Mathf.Max(yMin, yMax) + HalfH));
            }
            return list;
        }

        /// <summary>The scene's battle gates (their FSM "BG Control", raised while open): where each stands, the ground under it, its top.</summary>
        static List<(float X, float Ground, float Top)> Gates(Scene scene)
        {
            var list = new List<(float, float, float)>();
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (f == null || f.FsmName != "BG Control" || f.gameObject.scene != scene) continue;
                var ren = f.GetComponent<Renderer>();
                Bounds b = ren != null ? ren.bounds : default;
                Vector2 p = f.transform.position;
                float top = b.size.y > 0.5f ? b.max.y : p.y + 3f;
                var down = Physics2D.Raycast(new Vector2(p.x, top), Vector2.down, 40f, terrain);
                list.Add((p.x, down.collider != null ? down.point.y : (b.size.y > 0.5f ? b.min.y : p.y - 3f), top));
            }
            return list;
        }

        static Rect Union(Rect? a, Rect b) => a == null ? b : Rect.MinMaxRect(Mathf.Min(a.Value.xMin, b.xMin), Mathf.Min(a.Value.yMin, b.yMin), Mathf.Max(a.Value.xMax, b.xMax), Mathf.Max(a.Value.yMax, b.yMax));

        static Rect Grow(Rect r, float m) => new Rect(r.x - m, r.y - m, r.width + 2f * m, r.height + 2f * m);
    }
}
