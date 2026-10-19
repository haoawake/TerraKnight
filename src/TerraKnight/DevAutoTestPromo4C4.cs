using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Recorder C: dig sites chosen for the camera. A wall to dig through: a standing spot on a floor, a wall of the room's
    /// own rock a few cells ahead (3 to 12 cells thick, four rows high, on a floor that carries on under it) and open
    /// room beyond it to walk out into; a ceiling with plants: a floor under a low ceiling. Both kept well inside the
    /// room (the camera stops at its edges). The candidates of a room in order of score, so a clip can name one.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        struct C4Site
        {
            public Vector2Int Stand;
            public int Dir, Thick, Score, Plants;
            public string Kind;
        }

        static bool C4Inside(TerrainRegion region, int x, int y, int mx = 34, int my = 18)
        {
            var cr = region.CellRect;
            return x >= cr.x + mx && x < cr.xMax - mx && y >= cr.y + my && y < cr.yMax - my;
        }

        List<C4Site> C4WallSites(string scene)
        {
            var list = new List<C4Site>();
            var region = TerraWorld.Live;
            if (region == null) return list;
            var decor = C4Decor(scene);
            var cr = region.CellRect;
            for (int x = cr.x + 4; x < cr.xMax - 4; x++)
                for (int y = cr.y + 4; y < cr.yMax - 8; y++)
                {
                    if (!C4Inside(region, x, y) || !C4Stand(x, y)) continue;
                    foreach (int d in new[] { -1, 1 })
                    {
                        // A run-up of at least two free cells on a floor, the wall after it.
                        int fx = x + d, k = 0;
                        bool flat = true;
                        while (k < 6 && !TerraWorld.IsSolid(fx, y) && flat) { flat = TerraWorld.IsSolid(fx, y - 1); fx += d; k++; }
                        if (!flat || k < 2 || k >= 6) continue;
                        // The wall: rows y..y+3 diggable; floor under it.
                        int t = 0;
                        for (; t < 14; t++)
                        {
                            int cx = fx + d * t;
                            bool rock = true;
                            for (int r = 0; r < 4 && rock; r++) rock = C4Dig(cx, y + r);
                            if (!rock) break;
                            if (!TerraWorld.IsSolid(cx, y - 1)) { t = -1; break; }
                        }
                        if (t < 3 || t > 12) continue;
                        // Beyond it: open cells to walk out onto (three free rows over a floor, four columns).
                        int bx = fx + d * t, open = 0;
                        for (int i = 0; i < 6; i++)
                        {
                            int cx = bx + d * i;
                            bool free = !TerraWorld.IsSolid(cx, y) && !TerraWorld.IsSolid(cx, y + 1) && !TerraWorld.IsSolid(cx, y + 2);
                            if (!free) break;
                            open++;
                        }
                        if (open < 3) continue;
                        var seen = new HashSet<SpriteRenderer>();
                        int plants = 0, edge = 0;
                        for (int i = 0; i < t; i++)
                            for (int r = -1; r < 5; r++)
                            {
                                int cx = fx + d * i;
                                plants += C4DecorOn(decor, cx, y + r, seen);
                                if (TerraWorld.Get(cx, y + r) == CellKind.Original && (!TerraWorld.IsSolid(cx, y + r + 1) || !TerraWorld.IsSolid(cx, y + r - 1))) edge++;
                            }
                        // Thin walls with plants and lit edges (open air over or under them) first.
                        int score = plants * 6 + edge * 2 + open + (12 - t);
                        list.Add(new C4Site { Stand = new Vector2Int(x, y), Dir = d, Thick = t, Score = score, Plants = plants, Kind = "wall" });
                    }
                }
            list.Sort((a, b) => b.Score.CompareTo(a.Score));
            // One a place: a site within a few cells of a better one dropped.
            var kept = new List<C4Site>();
            foreach (var s in list)
            {
                bool near = false;
                foreach (var o in kept) if (Mathf.Abs(o.Stand.x - s.Stand.x) < 16 && Mathf.Abs(o.Stand.y - s.Stand.y) < 10) near = true;
                if (!near) kept.Add(s);
                if (kept.Count >= 6) break;
            }
            return kept;
        }

        List<C4Site> C4CeilSites(string scene)
        {
            var list = new List<C4Site>();
            var region = TerraWorld.Live;
            if (region == null) return list;
            var decor = C4Decor(scene);
            var cr = region.CellRect;
            for (int x = cr.x + 4; x < cr.xMax - 4; x++)
                for (int y = cr.y + 4; y < cr.yMax - 10; y++)
                {
                    if (!C4Inside(region, x, y) || !C4Stand(x, y)) continue;
                    bool floor = true;
                    for (int dx = -6; dx <= 6 && floor; dx++) floor = TerraWorld.IsSolid(x + dx, y - 1) && !TerraWorld.IsSolid(x + dx, y) && !TerraWorld.IsSolid(x + dx, y + 1) && !TerraWorld.IsSolid(x + dx, y + 2);
                    if (!floor) continue;
                    var seen = new HashSet<SpriteRenderer>();
                    int score = 0, cols = 0;
                    for (int dx = -5; dx <= 5; dx++)
                    {
                        int top = -1;
                        for (int dy = 4; dy <= 8; dy++) if (TerraWorld.IsSolid(x + dx, y + dy)) { top = y + dy; break; }
                        if (top < 0 || !C4Dig(x + dx, top)) continue;
                        cols++;
                        for (int k = 0; k < 3; k++) if (C4Dig(x + dx, top + k)) score += C4DecorOn(decor, x + dx, top + k, seen);
                    }
                    if (cols < 8 || score == 0) continue;
                    list.Add(new C4Site { Stand = new Vector2Int(x, y), Score = score, Plants = score, Kind = "ceil" });
                }
            list.Sort((a, b) => b.Score.CompareTo(a.Score));
            var kept = new List<C4Site>();
            foreach (var s in list)
            {
                bool near = false;
                foreach (var o in kept) if (Mathf.Abs(o.Stand.x - s.Stand.x) < 16 && Mathf.Abs(o.Stand.y - s.Stand.y) < 10) near = true;
                if (!near) kept.Add(s);
                if (kept.Count >= 4) break;
            }
            return kept;
        }

        /// <summary>Not a clip: the dig sites of a few rooms, each photographed with the hero on it, logged by number.</summary>
        IEnumerator P4_Cscout2(HeroController hero)
        {
            yield return P4Kit();
            mod.TrySwitchLoadout(1);
            string[] scenes =
            {
                "Fungus1_01", "Fungus1_02", "Fungus1_03", "Fungus1_05", "Fungus1_06", "Fungus1_10", "Fungus1_13", "Fungus1_15", "Fungus1_21", "Fungus1_22", "Fungus1_31",
                "Mines_02", "Mines_03", "Mines_05", "Mines_06", "Mines_10", "Mines_16", "Mines_25", "Mines_31", "Mines_37",
                "Ruins1_01", "Ruins1_03", "Ruins1_17", "Ruins1_28", "Ruins2_01", "Ruins2_04",
                "Deepnest_01b", "Deepnest_02", "Deepnest_03", "Deepnest_16", "Deepnest_35",
                "Fungus3_10", "Fungus3_22", "Fungus3_34", "Fungus3_48", "Waterways_13", "Fungus2_01", "Crossroads_01", "Crossroads_21", "Hive_01", "Deepnest_East_06",
            };
            foreach (var scene in scenes)
            {
                if (!entryGates.ContainsKey(scene)) continue;
                yield return EnterRoom(scene);
                hero = HeroController.instance;
                if (GameManager.instance.sceneName != scene) continue;
                PlayerData.instance.isInvincible = true;
                QuietAround(hero.transform.position, 400f);
                var walls = C4WallSites(scene);
                var ceils = C4CeilSites(scene);
                var all = new List<C4Site>();
                all.AddRange(walls.GetRange(0, Mathf.Min(3, walls.Count)));
                all.AddRange(ceils.GetRange(0, Mathf.Min(2, ceils.Count)));
                for (int i = 0; i < all.Count; i++)
                {
                    var s = all[i];
                    C4PutOn(hero, s.Stand);
                    if (s.Dir > 0 && !hero.cState.facingRight) hero.FaceRight();
                    if (s.Dir < 0 && hero.cState.facingRight) hero.FaceLeft();
                    yield return new WaitForSeconds(0.6f);
                    float light = 0f;
                    yield return C4Brightness(v => light = v);
                    yield return C4Snap($"{scene}_{s.Kind}{(s.Kind == "wall" ? walls.IndexOf(s) : ceils.IndexOf(s))}");
                    Plugin.Log.LogInfo($"[AutoTest] C4侦察2 {scene} {s.Kind}{(s.Kind == "wall" ? walls.IndexOf(s) : ceils.IndexOf(s))}：站 {s.Stand} 往{(s.Dir < 0 ? "左" : "右")} 厚 {s.Thick} 分 {s.Score} 植物 {s.Plants} 亮度 {light:F3}");
                }
            }
        }
    }
}
