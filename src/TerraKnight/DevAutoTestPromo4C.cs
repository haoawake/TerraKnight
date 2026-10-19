using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "p4_C..."): recorder C's clips for the 4.0 trailer: the world (digging each area's own blocks, the
    /// area torches, the area chests), building a home (the housing menu, a townsperson moving in) and fishing (an area
    /// crate opened). This file: helpers shared by C's clips and the scouting run that finds their places.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        // ------------------------------------------------------------------ helpers

        static readonly string[] c4DecorWords = { "vine", "grass", "bush", "moss", "leaf", "leaves", "fern", "flower", "plant", "weed" };

        /// <summary>The scene's plants the hole cutter takes away with a dug cell (HoleCutter.IsDecor's rule).</summary>
        static List<SpriteRenderer> C4Decor(string scene)
        {
            var list = new List<SpriteRenderer>();
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy || sr.gameObject.scene.name != scene) continue;
                float z = sr.transform.position.z;
                if (z >= CellVisuals.WallZ || z <= -8f) continue;
                if (sr.GetComponentInParent<TerraOwned>() != null || sr.GetComponentInParent<HealthManager>() != null) continue;
                string n = sr.gameObject.name.ToLowerInvariant();
                bool named = false;
                foreach (var k in c4DecorWords) if (n.Contains(k)) { named = true; break; }
                if (!named) continue;
                var b = sr.bounds;
                if (b.size.x > 8f || b.size.y > 8f) continue;
                list.Add(sr);
            }
            return list;
        }

        /// <summary>The decor sprites a dug cell would take with it (HoleCutter.DropDecor's overlap).</summary>
        static int C4DecorOn(List<SpriteRenderer> decor, int gx, int gy, HashSet<SpriteRenderer> seen)
        {
            var c = CellGrid.CellCenter(gx, gy);
            const float half = CellGrid.CellSize * 0.5f + 0.05f;
            int n = 0;
            foreach (var sr in decor)
            {
                if (sr == null || seen.Contains(sr)) continue;
                var b = sr.bounds;
                if (b.max.x < c.x - half || b.min.x > c.x + half || b.max.y < c.y - half || b.min.y > c.y + half) continue;
                seen.Add(sr);
                n++;
            }
            return n;
        }

        static bool C4Dig(int gx, int gy) => TerraWorld.Get(gx, gy) == CellKind.Original && TerraWorld.CanDig(gx, gy);

        /// <summary>Room to stand at a cell: solid under it, four free cells over it.</summary>
        static bool C4Stand(int x, int y) =>
            TerraWorld.IsSolid(x, y - 1) && !TerraWorld.IsSolid(x, y) && !TerraWorld.IsSolid(x, y + 1) && !TerraWorld.IsSolid(x, y + 2) && !TerraWorld.IsSolid(x, y + 3);

        /// <summary>The hero put down standing on a cell (his feet on its bottom).</summary>
        void C4PutOn(HeroController hero, Vector2Int stand)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            var feet = CellGrid.CellCenter(stand.x, stand.y) - new Vector2(0f, CellGrid.CellSize / 2f);
            float bottom = col.bounds.min.y - hero.transform.position.y;
            hero.transform.position = Clear(hero, new Vector2(feet.x, feet.y - bottom + 0.05f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        }

        /// <summary>The average brightness of the screen now (0..1), from a 7x7 lattice of its pixels.</summary>
        IEnumerator C4Brightness(System.Action<float> done)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            float sum = 0f;
            for (int y = 1; y < 8; y++)
                for (int x = 1; x < 8; x++)
                {
                    var c = tex.GetPixel(tex.width * x / 8, tex.height * y / 8);
                    sum += (c.r + c.g + c.b) / 3f;
                }
            Object.Destroy(tex);
            done(sum / 49f);
        }

        /// <summary>A JPG of the screen into dev/&lt;copy&gt;/c4/ (scouting pictures).</summary>
        IEnumerator C4Snap(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            string dir = Path.Combine(outDir, "c4");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".jpg"), tex.EncodeToJPG(80));
            Object.Destroy(tex);
        }

        // ------------------------------------------------------------------ scouting

        /// <summary>
        /// Not a clip: a walk through many rooms logging where C's clips could go: per room its plants, the best ceiling
        /// to dig under (most plants on the cells right over a standing spot), the best rock face to tunnel into, its
        /// breakables and geo rocks, its waters, its chests, its torch and how dark it is; a picture at the best ceiling.
        /// </summary>
        IEnumerator P4_Cscout(HeroController hero)
        {
            yield return P4Kit();
            Plugin.Log.LogInfo("[AutoTest] C4侦察 箱子计划：" + RegionalChests.DevDescribe());
            string[] scenes =
            {
                "Fungus1_01", "Fungus1_02", "Fungus1_03", "Fungus1_04", "Fungus1_05", "Fungus1_06", "Fungus1_07", "Fungus1_09", "Fungus1_10", "Fungus1_11",
                "Fungus1_12", "Fungus1_13", "Fungus1_14", "Fungus1_15", "Fungus1_17", "Fungus1_19", "Fungus1_21", "Fungus1_22", "Fungus1_25", "Fungus1_26",
                "Fungus1_29", "Fungus1_30", "Fungus1_31", "Fungus1_32",
                "Mines_01", "Mines_02", "Mines_03", "Mines_04", "Mines_05", "Mines_06", "Mines_07", "Mines_10", "Mines_11", "Mines_13", "Mines_16", "Mines_17",
                "Mines_20", "Mines_23", "Mines_25", "Mines_28", "Mines_29", "Mines_30", "Mines_31", "Mines_37",
                "Ruins1_01", "Ruins1_02", "Ruins1_03", "Ruins1_04", "Ruins1_05", "Ruins1_17", "Ruins1_18", "Ruins1_24", "Ruins1_27", "Ruins1_28", "Ruins1_30",
                "Ruins2_01", "Ruins2_03", "Ruins2_04",
                "Deepnest_01", "Deepnest_01b", "Deepnest_02", "Deepnest_03", "Deepnest_14", "Deepnest_16", "Deepnest_17", "Deepnest_26", "Deepnest_30",
                "Deepnest_31", "Deepnest_33", "Deepnest_34", "Deepnest_35", "Deepnest_36", "Deepnest_38", "Deepnest_39", "Deepnest_41", "Deepnest_42",
                "Fungus2_01", "Fungus2_03", "Fungus2_04", "Fungus2_05", "Fungus2_06", "Fungus2_07", "Fungus2_08", "Fungus2_09", "Fungus2_11", "Fungus2_12",
                "Fungus3_04", "Fungus3_05", "Fungus3_08", "Fungus3_10", "Fungus3_11", "Fungus3_13", "Fungus3_21", "Fungus3_22", "Fungus3_34", "Fungus3_48",
                "Crossroads_01", "Crossroads_02", "Crossroads_03", "Crossroads_04", "Crossroads_07", "Crossroads_13", "Crossroads_16", "Crossroads_18",
                "Crossroads_19", "Crossroads_21", "Crossroads_33", "Crossroads_35", "Crossroads_50",
                "Waterways_01", "Waterways_02", "Waterways_04", "Waterways_06", "Waterways_08", "Waterways_13",
                "Abyss_01", "Abyss_02", "Abyss_03", "Abyss_04", "Abyss_05", "Abyss_17", "Abyss_18", "Abyss_19", "Abyss_20",
                "RestingGrounds_02", "RestingGrounds_05", "RestingGrounds_06", "Deepnest_East_01", "Deepnest_East_03", "Deepnest_East_06", "Hive_01", "Hive_02",
            };
            foreach (var scene in scenes)
            {
                if (!entryGates.ContainsKey(scene)) { Plugin.Log.LogInfo($"[AutoTest] C4侦察 {scene}：门表里没有"); continue; }
                yield return EnterRoom(scene);
                hero = HeroController.instance;
                if (GameManager.instance.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] C4侦察 {scene}：没进去（在 {GameManager.instance.sceneName}）"); continue; }
                PlayerData.instance.isInvincible = true;
                float light = 0f;
                yield return C4Brightness(v => light = v);
                C4ScanScene(scene, out var ceilStand, out int ceilScore, out var tunnelStand, out int tunnelScore);
                if (ceilScore > 0)
                {
                    C4PutOn(hero, ceilStand);
                    yield return new WaitForSeconds(0.7f);
                    yield return C4Snap(scene + "_ceil");
                }
                if (tunnelScore > 2)
                {
                    C4PutOn(hero, tunnelStand);
                    yield return new WaitForSeconds(0.7f);
                    yield return C4Snap(scene + "_tunnel");
                }
                Plugin.Log.LogInfo($"[AutoTest] C4侦察 {scene} 入口亮度 {light:F3}");
            }
        }

        void C4ScanScene(string scene, out Vector2Int ceilStand, out int ceilScore, out Vector2Int tunnelStand, out int tunnelScore) => C4ScanScene(scene, out ceilStand, out ceilScore, out tunnelStand, out tunnelScore, out _, out _);

        void C4ScanScene(string scene, out Vector2Int ceilStand, out int ceilScore, out Vector2Int tunnelStand, out int tunnelScore, out int tunDir, out int tunRun)
        {
            ceilStand = default; ceilScore = 0; tunnelStand = default; tunnelScore = 0; tunDir = 0; tunRun = 0;
            var region = TerraWorld.Live;
            if (region == null) { Plugin.Log.LogInfo($"[AutoTest] C4侦察 {scene}：没有区域"); return; }
            var decor = C4Decor(scene);
            var cr = region.CellRect;
            int ceilH = 0;
            for (int x = cr.x + 6; x < cr.xMax - 6; x++)
                for (int y = cr.y + 4; y < cr.yMax - 10; y++)
                {
                    if (!C4Stand(x, y)) continue;
                    // A floor to walk along: solid under the next three cells each way.
                    bool floor = true;
                    for (int dx = -3; dx <= 3 && floor; dx++) floor = TerraWorld.IsSolid(x + dx, y - 1) && !TerraWorld.IsSolid(x + dx, y) && !TerraWorld.IsSolid(x + dx, y + 1);
                    if (floor)
                    {
                        // The ceiling: the first solid cell over each column 4..8 cells up; plants on its first three cells.
                        var seen = new HashSet<SpriteRenderer>();
                        int score = 0, cols = 0, h0 = 0;
                        for (int dx = -4; dx <= 4; dx++)
                        {
                            int top = -1;
                            for (int dy = 3; dy <= 8; dy++) if (TerraWorld.IsSolid(x + dx, y + dy)) { top = y + dy; break; }
                            if (top < 0 || !C4Dig(x + dx, top)) continue;
                            cols++;
                            if (dx == 0) h0 = top - y;
                            for (int k = 0; k < 3; k++) if (C4Dig(x + dx, top + k)) score += C4DecorOn(decor, x + dx, top + k, seen);
                        }
                        if (cols >= 6 && score > ceilScore) { ceilScore = score; ceilStand = new Vector2Int(x, y); ceilH = h0; }
                    }
                    // A rock face within three cells, three rows high, diggable 12 cells on.
                    foreach (int d in new[] { -1, 1 })
                    {
                        int fx = x + d, k = 0;
                        while (k < 3 && !TerraWorld.IsSolid(fx, y)) { fx += d; k++; }
                        if (k >= 3) continue;
                        int run = 0;
                        for (int i = 0; i < 30; i++)
                        {
                            bool ok = true;
                            for (int r = 0; r < 3 && ok; r++) ok = C4Dig(fx + d * i, y + r);
                            if (!ok) break;
                            run++;
                        }
                        if (run < 12) continue;
                        var seen = new HashSet<SpriteRenderer>();
                        int score = 0;
                        for (int i = 0; i < Mathf.Min(run, 16); i++) for (int r = -1; r < 4; r++) score += C4DecorOn(decor, fx + d * i, y + r, seen);
                        int value = score * 10 + Mathf.Min(run, 20);
                        if (value > tunnelScore) { tunnelScore = value; tunnelStand = new Vector2Int(x, y); tunDir = d; tunRun = run; }
                    }
                }
            int breakables = 0;
            foreach (var b in Object.FindObjectsByType<Breakable>(FindObjectsSortMode.None))
                if (b != null && b.isActiveAndEnabled && b.gameObject.scene.name == scene && Mathf.Abs(b.transform.position.z) <= 1f) breakables++;
            int rocks = 0;
            foreach (var r in Object.FindObjectsByType<GeoRock>(FindObjectsSortMode.None)) if (r != null && r.gameObject.activeInHierarchy && r.gameObject.scene.name == scene) rocks++;
            var ponds = Fishing.DevPonds().Where(p => p.Scene == scene).Select(p => $"{(p.Acid ? "酸" : "水")}{p.Tiles}格@{p.Area.center.x:F0},{p.Area.max.y:F1}");
            int chests = 0;
            var chestAt = new List<string>();
            foreach (var p in Chests.Positions()) { chests++; chestAt.Add($"{p.x:F1},{p.y:F1}"); }
            int style = RegionalTorchAreas.StyleOf(scene);
            Plugin.Log.LogInfo($"[AutoTest] C4侦察 {scene}（{Areas.AreaName(scene)}，火把 {RegionalTorches.NameOf(style)}）：植物 {decor.Count}；顶上植物最多 {ceilScore} 处站点 {ceilStand}（世界 {CellGrid.CellCenter(ceilStand.x, ceilStand.y):F1}，顶高 {ceilH}）；"
                + $"隧道 {tunnelScore}（站点 {tunnelStand} 世界 {CellGrid.CellCenter(tunnelStand.x, tunnelStand.y):F1} 往{(tunDir < 0 ? "左" : "右")} {tunRun} 格）；可破坏物 {breakables}，吉欧石 {rocks}；水 {string.Join(" ", ponds)}；箱子 {chests} {string.Join(" ", chestAt)}");
        }
    }
}
