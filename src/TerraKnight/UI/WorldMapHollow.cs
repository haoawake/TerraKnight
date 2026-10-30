using System;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Terraria's map marks on Hollow Knight's own map (FullMap, the user's 10-30 "原版的地图能不能应用回来，不过是按m打开，然后
    /// 晶塔，npc，虫子，长椅什么的依旧显示"): the townsfolk, the Grimm flames, the grub and bench pins, the compass's way and the other
    /// players, drawn by the same code as on Terraria's map, each scene's point put on the game's map instead of on the world's
    /// cells (elsewhere), with no layers there (the game's map lays every room flat).
    /// </summary>
    internal static partial class WorldMap
    {
        /// <summary>While Hollow Knight's map is drawn: where a point of a scene (its own units) goes on it, or null.</summary>
        static Func<string, Vector2, Vector2?> elsewhere;

        /// <summary>
        /// The townsfolk, the Grimm flames, the grub and bench pins and the compass's way on Hollow Knight's map, within a clip
        /// (the corner maps'), the compass's card on the full map only; returns the name under the mouse.
        /// </summary>
        internal static string DrawMarksOn(Func<string, Vector2, Vector2?> place, Func<Vector2, Vector2> toScreen, float ui, Vector2? mouse,
            Rect? clip = null, bool card = true, float alpha = 1f)
        {
            elsewhere = place;
            try
            {
                string npc = DrawNpcHeads(toScreen, ui, clip, mouse);
                string flame = DrawGrimmFlames(toScreen, 26f * ui, clip, mouse);
                string pin = DrawMapPins(toScreen, 24f * ui, clip, mouse);
                DrawCompassOn(toScreen, ui, card, alpha);
                return flame ?? pin ?? npc;
            }
            finally { elsewhere = null; }
        }

        /// <summary>The other players' heads on Hollow Knight's map, their names under them on the full map.</summary>
        internal static void DrawOthersOn(Func<string, Vector2, Vector2?> place, Func<Vector2, Vector2> toScreen, float ui, Rect? clip = null, bool names = true)
        {
            elsewhere = place;
            try { DrawOthers(toScreen, ui, names, clip); }
            finally { elsewhere = null; }
        }

        /// <summary>The teammate under the cursor on Hollow Knight's map whom a Wormhole Potion would go to.</summary>
        internal static Net.RemotePlayer MateOn(Func<string, Vector2, Vector2?> place, Func<Vector2, Vector2> toScreen, Vector2 gui, float radius)
        {
            if (Net.NetSession.State == Net.NetSession.Phase.Off) return null;
            foreach (var p in Net.NetSession.Players.Values)
            {
                if (!World.Wormhole.CanGo(p) || !p.Latest.Shown) continue;
                var at = place(p.Latest.Scene, p.Latest.BoxCenter);
                if (at.HasValue && Vector2.Distance(toScreen(at.Value), gui) <= radius) return p;
            }
            return null;
        }

        /// <summary>
        /// Dev (the user's 10-30 "能不能想办法让现在的泰拉瑞亚地图也做到"): every room's cells as Terraria's map draws them, each
        /// stretched into its room's place on Hollow Knight's map; returns how many were drawn. A look at Terraria's map laid out
        /// as the game's, its rooms bent to the game's drawings.
        /// </summary>
        internal static int DrawCellsOn(Func<string, Vector2, Vector2?> place, Func<Vector2, Vector2> toScreen)
        {
            int n = 0;
            foreach (var room in rooms.Values)
            {
                if (!room.Captured || !room.AnyShown) continue;
                var a = place(room.Name, Vector2.zero);
                var b = place(room.Name, new Vector2(room.W * C, room.H * C));
                if (!a.HasValue || !b.HasValue) continue;
                Vector2 sa = toScreen(a.Value), sb = toScreen(b.Value);
                var rect = Rect.MinMaxRect(Mathf.Min(sa.x, sb.x), Mathf.Min(sa.y, sb.y), Mathf.Max(sa.x, sb.x), Mathf.Max(sa.y, sb.y));
                if (rect.xMax < 0 || rect.yMax < 0 || rect.xMin > Screen.width || rect.yMin > Screen.height) continue;
                if (room.Solid == null || room.Dirty) Build(room, false);
                GUI.DrawTexture(rect, room.Solid);
                GUI.DrawTexture(rect, room.Open);
                n++;
            }
            return n;
        }

        /// <summary>A scene's size in its own units, from its cells on the map or the world atlas; false: neither has it.</summary>
        internal static bool SceneSize(string scene, out Vector2 size)
        {
            size = default;
            if (scene == null) return false;
            if (!rooms.TryGetValue(scene, out var room) || room.Tiles == null) atlas.TryGetValue(scene, out room);
            if (room == null || room.W <= 0 || room.H <= 0) return false;
            size = new Vector2(room.W * C, room.H * C);
            return true;
        }

        /// <summary>A point of the compass's way on Hollow Knight's map, by its scene; null where that map has no room for it.</summary>
        static Vector2? WayOn(WayPoint p) =>
            p.Scene != null && layout.TryGetValue(p.Scene, out var at) ? elsewhere(p.Scene, InScene(p.Scene, at, p.Cell) * C) : null;

        /// <summary>
        /// The compass's way on Hollow Knight's map: its dots flowing on to the fight, a door's dot where the way goes on in a room
        /// away from it (a gate whose sides lie apart on that map, or past a room it hasn't), the fight's picture, the card.
        /// </summary>
        static void DrawCompassOn(Func<Vector2, Vector2> toScreen, float ui, bool card, float alpha)
        {
            if (card) compassFullDots = 0;
            if (!CompassOn) return;
            compassRouteBuilt = BuildWay();
            if (compassRouteBuilt && wayPoints.Count >= 2)
            {
                EnsureCompassTextures();
                var prev = GUI.color;
                var gold = new Color(CompassGold.r, CompassGold.g, CompassGold.b, alpha);
                float spacing = 7.5f * ui, size = 3.8f * ui, run = 0f;
                float phase = Time.unscaledTime * 14f * ui % spacing;
                int dots = 0;
                Vector2? last = null, lastAt = null;
                for (int i = 0; i < wayPoints.Count && dots < 800; i++)
                {
                    var at = WayOn(wayPoints[i]);
                    if (!at.HasValue)
                    {
                        if (lastAt.HasValue) Dot(lastAt.Value, 8f * ui, gold);
                        last = lastAt = null;
                        continue;
                    }
                    var s = toScreen(at.Value);
                    if (last.HasValue)
                    {
                        // The way is the head, then each gate and the gate it comes out of, then the fight: a step onto an even
                        // point goes through a gate. About 20 of a scene's tiles apart on the game's map, its two sides don't meet there.
                        bool through = i % 2 == 0 && i < wayPoints.Count - 1;
                        if (through && Vector2.Distance(last.Value, at.Value) > 0.3f) Dot(lastAt.Value, 8f * ui, gold);
                        else
                        {
                            var a = lastAt.Value;
                            float len = Vector2.Distance(a, s);
                            if (len > 0.01f)
                            {
                                for (float d = Mathf.Ceil((run - phase) / spacing) * spacing + phase; d <= run + len && dots < 800; d += spacing, dots++)
                                    Dot(Vector2.Lerp(a, s, (d - run) / len), size, gold);
                                run += len;
                            }
                        }
                    }
                    last = at;
                    lastAt = s;
                }
                if (last.HasValue && WayOn(wayPoints[wayPoints.Count - 1]).HasValue) CompassIcon(lastAt.Value, 20f * ui, alpha);
                if (card) compassFullDots = dots;
                GUI.color = prev;
            }
            if (card) DrawCompassCard(ui);
        }
    }
}
