using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "compasssurvey", the user's 10-27 "指南针…应该根据当前的时期进行指引…毛里克…指到了科尼法旁边"): every scene
    /// of the map's gate table loaded on its own (as the layout survey does), its terrain grid built, and the things that
    /// shut a way until the save opens them found: terrain-layer colliders over open cells whose removal the save keeps (a
    /// player-data bool or the scene's persistent record). With them all shut, the open cells joined into parts; each gate's
    /// part; each shutter's kind (break: the player breaks it from either side; lever / toll: opened from the side of its
    /// lever or toll machine; pd: opened elsewhere, by the save only), its condition and the parts it joins; and each fight's
    /// spot (its reviewed arena floor, else the boss's, the dream warrior's or the room's open middle) with its part.
    /// Writes compass-survey.tsv in the run's folder for tools/compass_links.py. TK_SURVEY_SCENES limits the scenes,
    /// TK_SURVEY_SECONDS the time (default 1700). Lines "[AutoTest] 指南针勘察 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        sealed class Shutter
        {
            public string Kind, Cond, Name, Parts;
            public RectInt Cells;
            public Vector2 Opener = new Vector2(float.NaN, float.NaN);
        }

        IEnumerator CompassSurveyTest()
        {
            PlayerData.instance.isInvincible = true;
            var rooms = UI.WorldMap.DevCompassRooms;
            var scenes = UI.WorldMap.DevGateScenes;
            foreach (var r in rooms.Values) if (!scenes.Contains(r)) scenes.Add(r);
            string only = System.Environment.GetEnvironmentVariable("TK_SURVEY_SCENES");
            if (!string.IsNullOrEmpty(only)) scenes = scenes.Where(s => only.Split(',').Contains(s)).ToList();
            float seconds = float.TryParse(System.Environment.GetEnvironmentVariable("TK_SURVEY_SECONDS"), out float sx) ? sx : 1700f;
            float until = Time.realtimeSinceStartup + seconds;
            var lines = new List<string>();
            var unknown = new Dictionary<string, int>();
            int done = 0, split = 0;
            foreach (var name in scenes)
            {
                if (Time.realtimeSinceStartup > until) { Plugin.Log.LogInfo($"[AutoTest] 指南针勘察 时间到，停在 {name}"); break; }
                if (name.StartsWith("GG_") && name != "GG_Waterways" && name != "GG_Workshop" && name != "GG_Lurker" && name != "GG_Pipeway") continue;
                var already = USM.GetSceneByName(name);
                bool loadedHere = false;
                if (!already.isLoaded)
                {
                    AsyncOperation load = null;
                    try { load = USM.LoadSceneAsync(name, LoadSceneMode.Additive); }
                    catch (System.Exception) { }
                    if (load == null) continue;
                    while (!load.isDone) yield return null;
                    loadedHere = true;
                }
                // A fight room's boss scene (Fungus2_15_boss), where its boss waits.
                bool bossHere = false;
                string bossName = name + "_boss";
                if (rooms.ContainsValue(name) && Application.CanStreamedLevelBeLoaded(bossName) && !USM.GetSceneByName(bossName).isLoaded)
                {
                    AsyncOperation bl = null;
                    try { bl = USM.LoadSceneAsync(bossName, LoadSceneMode.Additive); }
                    catch (System.Exception) { }
                    if (bl != null) { while (!bl.isDone) yield return null; bossHere = true; }
                }
                yield return null;
                yield return null;
                PlayerData.instance.isInvincible = true;
                var scene = USM.GetSceneByName(name);
                var extra = bossHere ? USM.GetSceneByName(bossName).GetRootGameObjects() : new GameObject[0];
                try { if (SurveyScene(scene, rooms, lines, unknown, extra)) split++; }
                catch (System.Exception e) { Plugin.Log.LogInfo($"[AutoTest] 指南针勘察 {name} 出错：{e}"); }
                done++;
                if (bossHere)
                {
                    var ub = USM.UnloadSceneAsync(USM.GetSceneByName(bossName));
                    while (ub != null && !ub.isDone) yield return null;
                }
                if (!loadedHere) continue;
                var unload = USM.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone) yield return null;
                if (done % 25 == 0)
                {
                    var gc = Resources.UnloadUnusedAssets();
                    while (!gc.isDone) yield return null;
                }
            }
            string path = Path.Combine(outDir, "compass-survey.tsv");
            File.WriteAllLines(path, lines, new System.Text.UTF8Encoding(false));
            Plugin.Log.LogInfo($"[AutoTest] 指南针勘察 看了 {done}/{scenes.Count} 个场景，门被隔开的 {split} 个，写到 {path}");
            foreach (var kv in unknown.OrderByDescending(k => k.Value).Take(60))
                Plugin.Log.LogInfo($"[AutoTest] 指南针勘察 没有存档条件的挡路物 {kv.Key} ×{kv.Value}");
        }

        /// <summary>One scene: its parts, gates, shutters and fight spots as tsv lines; true when its gates are split.</summary>
        bool SurveyScene(Scene scene, Dictionary<ItemType, string> rooms, List<string> lines, Dictionary<string, int> unknown, GameObject[] bossRoots)
        {
            var map = TerrainSystem.SceneTileMap(scene);
            if (map == null || map.data == null) { lines.Add($"N\t{scene.name}\t没有地形图"); return false; }
            var grid = TerrainSystem.BuildGrid(map, TerrainSystem.TerrainOutlines(map), TerrainSystem.HandPlacedTerrain(map), scene.name, out _, out _);
            const float c = CellGrid.CellSize;
            var origin = (Vector2)TerrainSystem.CellOffsetOf(map) * c;
            int w = grid.Width, h = grid.Height;
            Vector2Int CellOf(Vector2 p) => new Vector2Int(Mathf.FloorToInt((p.x - origin.x) / c), Mathf.FloorToInt((p.y - origin.y) / c));
            var roots = scene.GetRootGameObjects();

            // Levers and toll machines: where the shutters they open are opened from.
            var levers = new List<Vector2>();
            var tolls = new List<Vector2>();
            foreach (var root in roots)
                foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (f.FsmName == "Switch Control") levers.Add(f.transform.position);
                    else if (f.FsmName == "Toll Machine" || f.gameObject.name.StartsWith("Toll Machine")) tolls.Add(f.transform.position);
                }

            var shutters = new List<Shutter>();
            foreach (var root in roots)
                foreach (var col in root.GetComponentsInChildren<Collider2D>(false))
                {
                    if (col.isTrigger || !col.enabled || col.gameObject.layer != 8) continue;
                    var b = col.bounds;
                    var lo = CellOf(b.min);
                    var hi = CellOf(b.max);
                    int x0 = Mathf.Max(0, lo.x), y0 = Mathf.Max(0, lo.y), x1 = Mathf.Min(w - 1, hi.x), y1 = Mathf.Min(h - 1, hi.y);
                    if (x1 < x0 || y1 < y0) continue;
                    int all = (x1 - x0 + 1) * (y1 - y0 + 1);
                    if (all > 4000) continue;
                    int open = 0;
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                            if (!grid.IsSolid(x, y)) open++;
                    if (open * 2 < all) continue;
                    var s = ClassifyShutter(col.gameObject, scene.name);
                    if (s == null) continue;
                    if (s.Cond == null)
                    {
                        string key = $"{scene.name}/{System.Text.RegularExpressions.Regex.Replace(col.gameObject.name, @"\s*\(\d+\)$", "")}";
                        unknown[key] = unknown.TryGetValue(key, out int k) ? k + 1 : 1;
                        continue;
                    }
                    s.Cells = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                    // Its side found already: a one-way wall's range.
                    if (!float.IsNaN(s.Opener.x) && s.Kind != "toll") s.Kind = "oneway";
                    var near = s.Kind == "lever" ? levers : s.Kind == "toll" ? tolls : null;
                    if (near != null && near.Count > 0 && float.IsNaN(s.Opener.x))
                    {
                        var mid = (Vector2)b.center;
                        s.Opener = near.OrderBy(p => (p - mid).sqrMagnitude).First();
                    }
                    shutters.Add(s);
                }

            // The open cells, the shutters shut, joined into parts.
            var shut = new bool[w * h];
            foreach (var s in shutters)
                for (int y = s.Cells.yMin; y < s.Cells.yMax; y++)
                    for (int x = s.Cells.xMin; x < s.Cells.xMax; x++)
                        shut[y * w + x] = true;
            var part = new int[w * h];
            for (int i = 0; i < part.Length; i++) part[i] = -1;
            int parts = 0;
            var queue = new Queue<int>();
            for (int i = 0; i < w * h; i++)
            {
                if (part[i] >= 0 || shut[i] || grid.IsSolid(i % w, i / w)) continue;
                part[i] = parts;
                queue.Enqueue(i);
                while (queue.Count > 0)
                {
                    int j = queue.Dequeue(), x = j % w, y = j / w;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int n = ny * w + nx;
                        if (part[n] >= 0 || shut[n] || grid.IsSolid(nx, ny)) continue;
                        part[n] = parts;
                        queue.Enqueue(n);
                    }
                }
                parts++;
            }
            // A point's part: the nearest open cell within a few cells.
            int PartNear(Vector2Int at, int reach = 6)
            {
                for (int r = 0; r <= reach; r++)
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                            int x = at.x + dx, y = at.y + dy;
                            if (x >= 0 && y >= 0 && x < w && y < h && part[y * w + x] >= 0) return part[y * w + x];
                        }
                return -1;
            }

            // Shutters touching each other count as one.
            var groups = new List<List<Shutter>>();
            foreach (var s in shutters)
            {
                var grown = new RectInt(s.Cells.xMin - 1, s.Cells.yMin - 1, s.Cells.width + 2, s.Cells.height + 2);
                var into = groups.Where(g => g.Any(o => o.Cells.Overlaps(grown))).ToList();
                var merged = new List<Shutter> { s };
                foreach (var g in into) { merged.AddRange(g); groups.Remove(g); }
                groups.Add(merged);
            }
            var groupAt = new int[w * h];
            for (int i = 0; i < groupAt.Length; i++) groupAt[i] = -1;
            for (int gi = 0; gi < groups.Count; gi++)
                foreach (var s in groups[gi])
                    for (int y = s.Cells.yMin; y < s.Cells.yMax; y++)
                        for (int x = s.Cells.xMin; x < s.Cells.xMax; x++)
                            groupAt[y * w + x] = gi;
            var pockets = new Dictionary<int, HashSet<int>>();

            // Gates: their part just inside the room. One whose way in crosses a shutter first (a wall over the gate itself,
            // its far side off the room) is a pocket of its own, which that shutter joins to the room.
            var gateParts = new List<string>();
            var used = new HashSet<int>();
            foreach (var (gn, side, at) in UI.WorldMap.DevGatesOf(scene.name))
            {
                var dir = side == 'l' ? new Vector2Int(1, 0) : side == 'r' ? new Vector2Int(-1, 0) : side == 't' ? new Vector2Int(0, -1) : side == 'b' ? new Vector2Int(0, 1) : Vector2Int.zero;
                var across = dir.x != 0 ? new Vector2Int(0, 1) : new Vector2Int(1, 0);
                int p = -1, crossed = -1;
                if (dir != Vector2Int.zero)
                    for (int k = 0; k < 20 && p < 0; k++)
                        foreach (int lat in new[] { 0, 1, -1, 2, -2, 3, -3 })
                        {
                            var cl = at + dir * k + across * lat;
                            if (cl.x < 0 || cl.y < 0 || cl.x >= w || cl.y >= h) continue;
                            int i = cl.y * w + cl.x;
                            if (groupAt[i] >= 0 && crossed < 0 && lat == 0) crossed = groupAt[i];
                            if (part[i] >= 0) { p = part[i]; break; }
                        }
                if (p < 0) p = PartNear(at + dir * 2, 8);
                if (crossed >= 0)
                {
                    int pocket = parts++;
                    if (!pockets.TryGetValue(crossed, out var ps)) pockets[crossed] = ps = new HashSet<int>();
                    ps.Add(pocket);
                    p = pocket;
                }
                gateParts.Add($"{gn}:{p}");
                if (p >= 0) used.Add(p);
            }

            // Each shutter joins the parts round it.
            var shutterLines = new List<string>();
            for (int gi = 0; gi < groups.Count; gi++)
            {
                var g = groups[gi];
                var touched = pockets.TryGetValue(gi, out var pk) ? new HashSet<int>(pk) : new HashSet<int>();
                foreach (var s in g)
                {
                    for (int x = s.Cells.xMin - 1; x <= s.Cells.xMax; x++)
                        for (int y = s.Cells.yMin - 1; y <= s.Cells.yMax; y++)
                        {
                            if (x < 0 || y < 0 || x >= w || y >= h) continue;
                            int p = part[y * w + x];
                            if (p >= 0) touched.Add(p);
                        }
                }
                if (touched.Count < 2) continue;
                var kinds = g.Select(s => s.Kind).Distinct().ToList();
                string kind = kinds.Contains("pd") ? "pd" : kinds.Contains("oneway") ? "oneway" : kinds.Contains("lever") ? "lever" : kinds.Contains("toll") ? "toll" : "break";
                // Opened from: the part, of those it joins, nearest its lever, toll machine or range.
                var opener = g.FirstOrDefault(s => !float.IsNaN(s.Opener.x));
                int openerPart = -1;
                if (opener != null)
                {
                    var oc = CellOf(opener.Opener);
                    for (int r = 0; r <= 30 && openerPart < 0; r++)
                        for (int dy = -r; dy <= r && openerPart < 0; dy++)
                            for (int dx = -r; dx <= r; dx++)
                            {
                                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                                int x = oc.x + dx, y = oc.y + dy;
                                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                                int p = part[y * w + x];
                                if (p >= 0 && touched.Contains(p)) { openerPart = p; break; }
                            }
                }
                var all = g.SelectMany(s => new[] { s.Cells }).ToList();
                int ax = all.Min(r => r.xMin), ay = all.Min(r => r.yMin), bx = all.Max(r => r.xMax), by = all.Max(r => r.yMax);
                shutterLines.Add($"B\t{scene.name}\t{kind}\t{string.Join("&", g.Select(s => s.Cond).Distinct())}\t{ax},{ay},{bx - ax},{by - ay}\t{string.Join(",", touched.OrderBy(p => p))}\t{openerPart}\t{string.Join("/", g.Select(s => s.Name).Distinct())}\t{string.Join("/", g.Select(s => s.Parts).Distinct())}");
            }

            // The fights held here: their spot and its part.
            var fightLines = new List<string>();
            foreach (var kv in rooms)
            {
                if (kv.Value != scene.name) continue;
                string key = Relics.KeyOf(kv.Key);
                string how;
                Vector2? spot = null;
                // A reviewed floor of this very room (a dream's or a variant's is of another scene).
                if (key != null && BossArena.SceneOf(key) == scene.name && BossArrivalPoints.Feet.TryGetValue(key, out var feet)) { spot = feet + Vector2.up * 1f; how = "落点"; }
                else
                {
                    how = "首领";
                    GameObject best = null;
                    var searched = roots.Concat(bossRoots).ToArray();
                    foreach (var root in searched)
                        foreach (var t in root.GetComponentsInChildren<Transform>(true))
                            if (t.name.StartsWith("Ghost Warrior NPC") || t.name == "Ghost NPC") { best = t.gameObject; how = "战士之梦"; }
                    string arena = key != null ? BossArena.SceneOf(key) : null;
                    // A dream's: where the dream nail takes one in (the dreamer's Dream Enter).
                    if (best == null && arena?.StartsWith("Dream_") == true)
                    {
                        foreach (var root in searched)
                            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                                if (t.name.StartsWith("Dream Enter")) { best = t.gameObject; how = "入梦点"; }
                        // Else where the dream lets one out: beside the dreamer (the False Knight's and the Soul Master's corpses).
                        if (best == null)
                            foreach (var root in searched)
                                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                                    if (t.name == "door_dreamReturn") { best = t.gameObject; how = "出梦点"; }
                    }
                    // A fight held behind a door of this room (the Hollow Knight's in the Black Egg, its door's way set by its state machine): that door.
                    if (best == null && arena != null && arena != scene.name)
                        foreach (var root in searched)
                            foreach (var tp in root.GetComponentsInChildren<TransitionPoint>(true))
                                if (tp.isADoor && (tp.targetScene == arena || arena.StartsWith("Room_Final_Boss") || arena == "Dream_Final_Boss")) { best = tp.gameObject; how = "门"; }
                    // The boss itself: by the book's record, else the healthiest.
                    if (best == null)
                    {
                        HealthManager top = null;
                        bool topNamed = false;
                        foreach (var root in searched)
                            foreach (var hm in root.GetComponentsInChildren<HealthManager>(true))
                            {
                                bool named = key != null && UI.EnemyHover.RecordOf(hm) == key;
                                if (top == null || named && !topNamed || named == topNamed && hm.hp > top.hp) { top = hm; topNamed = named; }
                            }
                        if (top != null) { best = top.gameObject; how = topNamed ? "首领" : $"血最多的 {top.name}"; }
                    }
                    if (best != null) how += $"（{best.name}）";
                    if (best != null) spot = best.transform.position;
                }
                Vector2Int cell;
                if (spot.HasValue) cell = CellOf(spot.Value);
                else
                {
                    // The open cell of a gate's part nearest the room's middle.
                    how = "房间中部";
                    cell = new Vector2Int(w / 2, h / 2);
                    float bestD = float.MaxValue;
                    for (int y = 0; y < h; y += 2)
                        for (int x = 0; x < w; x += 2)
                        {
                            int p = part[y * w + x];
                            if (p < 0 || !used.Contains(p)) continue;
                            float d = (new Vector2(x, y) - new Vector2(w / 2f, h / 2f)).sqrMagnitude;
                            if (d < bestD) { bestD = d; cell = new Vector2Int(x, y); }
                        }
                }
                int fp = PartNear(cell, 10);
                fightLines.Add($"E\t{kv.Key}\t{scene.name}\t{cell.x},{cell.y}\t{fp}\t{how}");
            }

            bool splitGates = used.Count > 1;
            lines.Add($"S\t{scene.name}\t{w}x{h}\t{string.Join(",", gateParts)}");
            lines.AddRange(shutterLines);
            lines.AddRange(fightLines);
            if (splitGates || shutterLines.Count > 0)
                Plugin.Log.LogInfo($"[AutoTest] 指南针勘察 {scene.name}：门 {string.Join(" ", gateParts)}；挡路 {shutterLines.Count} 处 {string.Join(" | ", shutterLines.Select(l => string.Join(" ", l.Split('\t').Skip(2))))}");
            foreach (var l in fightLines) Plugin.Log.LogInfo($"[AutoTest] 指南针勘察 首领 {l.Replace('\t', ' ')}");
            return splitGates;
        }

        /// <summary>
        /// What kind of shutter a terrain collider belongs to and what opens it in the save (null: not one — a dive or
        /// collapsing floor, a battle gate, a lift; Cond null: one with no condition found, counted for the report).
        /// </summary>
        static Shutter ClassifyShutter(GameObject go, string sceneName)
        {
            var chain = new List<GameObject>();
            for (var t = go.transform; t != null; t = t.parent)
            {
                if (t.parent == null && t.name.StartsWith("_")) break;
                chain.Add(t.gameObject);
            }
            var fsms = chain.SelectMany(o => o.GetComponents<PlayMakerFSM>()).ToList();
            string names = string.Join(" ", chain.Select(o => o.name)).ToLowerInvariant();
            foreach (var f in fsms)
            {
                string fn = f.FsmName;
                if (fn == "quake_floor" || fn == "quake_floor_shake" || fn.StartsWith("collapse") || fn == "BG Control" || fn == "Loose Floor" || fn == "Break 1" || fn == "Rise" || fn == "Close")
                    return null;
            }
            if (names.Contains("battle gate") || names.Contains("lift") || names.Contains("elevator") || names.Contains("platform") || names.Contains("collapse") || names.Contains("quake")) return null;
            // What it is made of, for the report: its and its parents' behaviours and state machines.
            var s = new Shutter
            {
                Name = go.name,
                Parts = string.Join(",", chain.SelectMany(o => o.GetComponents<MonoBehaviour>()).Where(m => m != null)
                    .Select(m => m is PlayMakerFSM pf ? "fsm:" + pf.FsmName : m.GetType().Name).Distinct()),
            };
            bool breakable = fsms.Any(f => f.FsmName == "breakable_wall_v2" || f.FsmName == "break_floor" || f.FsmName == "Break Pole" || f.FsmName == "Pillar")
                || names.Contains("breakable") || names.Contains("break wall") || names.Contains("break floor")
                || chain.Any(o => o.GetComponent<Breakable>() != null);
            bool toll = names.Contains("toll");
            // A one-way wall (Hollow Knight's break_floor with "Hero Range"): a blow counts only with the hero inside its range
            // trigger, so it is broken from that side alone.
            var oneWay = fsms.FirstOrDefault(f => f.FsmName == "break_floor" && f.FsmVariables.BoolVariables.Any(v => v.Name == "Hero Range"));
            if (oneWay != null)
            {
                var range = oneWay.GetComponentsInChildren<Collider2D>(true).Where(c => c.isTrigger).OrderByDescending(c => c.bounds.size.x * c.bounds.size.y).FirstOrDefault();
                if (range != null)
                {
                    breakable = false;
                    s.Opener = range.bounds.center;
                }
            }
            // The condition: a player-data bool its state machines set or test, a deactivating component's bool, else the scene's record.
            foreach (var f in fsms)
            {
                // Hollow Knight's playerdata_activation: the object (in)activated (Activate) while the bool is "Check if Player Data is".
                if (f.FsmName == "playerdata_activation")
                {
                    string pdName = f.FsmVariables.GetFsmString("Player Data name")?.Value;
                    if (string.IsNullOrEmpty(pdName)) continue;
                    bool check = f.FsmVariables.GetFsmBool("Check if Player Data is")?.Value ?? true;
                    bool activate = f.FsmVariables.GetFsmBool("Activate")?.Value ?? false;
                    bool goneWhen = activate ? !check : check;
                    s.Kind = breakable ? "break" : "pd";
                    s.Cond = (goneWhen ? "pd:" : "pdnot:") + pdName;
                    return s;
                }
                foreach (var st in f.FsmStates)
                    foreach (var a in st.Actions)
                    {
                        if (a == null) continue;
                        string tn = a.GetType().Name;
                        if (tn != "SetPlayerDataBool" && tn != "PlayerDataBoolTest" && tn != "GetPlayerDataBool") continue;
                        var v = a.GetType().GetField("boolName")?.GetValue(a) as HutongGames.PlayMaker.FsmString;
                        if (v == null || string.IsNullOrEmpty(v.Value)) continue;
                        // A bool it sets is set as it opens (a wall broken, a gate raised); one it only reads opens it from elsewhere.
                        s.Kind = breakable ? "break" : toll ? "toll" : tn == "SetPlayerDataBool" ? "lever" : "pd";
                        s.Cond = "pd:" + v.Value;
                        return s;
                    }
            }
            foreach (var o in chain)
                foreach (var m in o.GetComponents<MonoBehaviour>())
                {
                    if (m == null || !m.GetType().Name.StartsWith("DeactivateIfPlayerdata")) continue;
                    var v = m.GetType().GetField("boolName")?.GetValue(m) as string;
                    if (string.IsNullOrEmpty(v)) continue;
                    s.Kind = breakable ? "break" : "pd";
                    s.Cond = (m.GetType().Name.EndsWith("False") ? "pdnot:" : "pd:") + v;
                    return s;
                }
            foreach (var o in chain)
            {
                var pb = o.GetComponent<PersistentBoolItem>();
                if (pb == null || pb.persistentBoolData == null) continue;
                string id = string.IsNullOrEmpty(pb.persistentBoolData.id) ? o.name : pb.persistentBoolData.id;
                string sc = string.IsNullOrEmpty(pb.persistentBoolData.sceneName) ? sceneName : pb.persistentBoolData.sceneName;
                s.Kind = breakable ? "break" : toll ? "toll" : "lever";
                s.Cond = $"pb:{sc}:{id}";
                return s;
            }
            s.Kind = breakable ? "break" : "pd";
            return s;
        }
    }
}
