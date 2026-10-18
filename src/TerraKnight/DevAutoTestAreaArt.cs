using System.Collections;
using System.Collections.Generic;
using System.IO;
using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "areaart"): reference for drawing each Hallownest area's own block and wall. Per area, a few of its rooms:
    /// views of the room where its terrain's outline is busiest (no HUD, no Terraria interface, the Knight hidden), and the
    /// scene's own sprites (its ground layer, the foreground before it, the background behind) as they are, with their
    /// tints; once, Terraria's block and wall sheets and items to draw to, and which frame the mod draws for which
    /// neighbours. Written under the copy's dev folder, area-art/; tools turn it into the art pack.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>The areas (the mod's Areas.AreaName), each a file-name key and a few of its rooms.</summary>
        static readonly (string Key, string Name, string[] Scenes)[] artAreas =
        {
            ("KingsPass", "国王山道", new[] { "Tutorial_01" }),
            ("Dirtmouth", "德特茅斯", new[] { "Town" }),
            ("HowlingCliffs", "呼啸悬崖", new[] { "Cliffs_01", "Cliffs_02" }),
            ("Crossroads", "遗忘十字路", new[] { "Crossroads_01", "Crossroads_07", "Crossroads_13" }),
            ("Greenpath", "苍绿之径", new[] { "Fungus1_01", "Fungus1_03", "Fungus1_22" }),
            ("FungalWastes", "真菌荒地", new[] { "Fungus2_01", "Fungus2_06", "Fungus2_18" }),
            ("FogCanyon", "雾之峡谷", new[] { "Fungus3_26", "Fungus3_28", "Fungus3_02" }),
            ("QueensGardens", "王后花园", new[] { "Fungus3_04", "Fungus3_10", "Fungus3_22" }),
            ("CityOfTears", "泪水之城", new[] { "Ruins1_01", "Ruins1_03", "Ruins2_01" }),
            ("RoyalWaterways", "皇家水道", new[] { "Waterways_01", "Waterways_02", "Waterways_04" }),
            ("CrystalPeak", "水晶山峰", new[] { "Mines_02", "Mines_05", "Mines_10" }),
            ("RestingGrounds", "安息之地", new[] { "RestingGrounds_02", "RestingGrounds_05", "RestingGrounds_06" }),
            ("Deepnest", "深巢", new[] { "Deepnest_01", "Deepnest_02", "Deepnest_26" }),
            ("BeastsDen", "野兽巢穴", new[] { "Deepnest_Spider_Town" }),
            ("KingdomsEdge", "王国边缘", new[] { "Deepnest_East_01", "Deepnest_East_03", "Deepnest_East_06" }),
            ("Hive", "蜂巢", new[] { "Hive_01", "Hive_02" }),
            ("AncientBasin", "远古盆地", new[] { "Abyss_04", "Abyss_05", "Abyss_19" }),
            ("Abyss", "深渊", new[] { "Abyss_09", "Abyss_10", "Abyss_06_Core" }),
            ("WhitePalace", "白色宫殿", new[] { "White_Palace_02", "White_Palace_03_hub", "White_Palace_04" }),
            ("BlackEggTemple", "黑卵神殿", new[] { "Room_temple" }),
            ("Colosseum", "愚人斗兽场", new[] { "Room_Colosseum_01", "Room_Colosseum_02" }),
            ("GrimmTroupe", "格林剧团", new[] { "Grimm_Main_Tent" }),
            ("Dream", "梦境", new[] { "Dream_Nailcollection", "Dream_01_False_Knight" }),
            ("Godhome", "神居", new[] { "GG_Atrium", "GG_Workshop" }),
        };

        IEnumerator AreaArtExport(string only)
        {
            string root = Path.Combine(outDir, "area-art");
            Directory.CreateDirectory(root);
            ExportTerrariaTemplates(Path.Combine(root, "_terraria"));
            mod.DevNoFallDamage = true;
            Player.TerraStats.DevImmortal = true;
            var index = new List<string> { "key\tname\tscene\twall\tviews\tsprites" };
            foreach (var area in artAreas)
            {
                if (!string.IsNullOrEmpty(only) && only != area.Key) continue;
                string dir = Path.Combine(root, area.Key);
                Directory.CreateDirectory(dir);
                var seen = new HashSet<string>();
                var manifest = new List<string> { "file\tlayer\tscene\tz\tsortingOrder\ttint\tworldSize\tpixels" };
                foreach (var scene in area.Scenes)
                {
                    yield return GoTo(scene, EntryGateOf(scene));
                    if (GameManager.instance.sceneName != scene)
                    {
                        Plugin.Log.LogInfo($"[AutoTest] 地区素材 {area.Name} {scene}：没进去（在 {GameManager.instance.sceneName}），跳过");
                        index.Add($"{area.Key}\t{area.Name}\t{scene}\t-\t0\t0");
                        continue;
                    }
                    PlayerData.instance.isInvincible = true;
                    var hero = HeroController.instance;
                    QuietAround(hero.transform.position, 80f);
                    // The area title gone, the room settled.
                    yield return new WaitForSeconds(2.5f);
                    QuietAround(hero.transform.position, 80f);
                    int views = 0;
                    yield return AreaViews(scene, dir, area.Scenes.Length >= 3 ? 2 : 3, n => views = n);
                    int sprites = ExportSceneSprites(scene, dir, seen, manifest);
                    string wall = Areas.WallFor(scene);
                    index.Add($"{area.Key}\t{area.Name}\t{scene}\t{wall}\t{views}\t{sprites}");
                    Plugin.Log.LogInfo($"[AutoTest] 地区素材 {area.Name} {scene}：{views} 张画面，{sprites} 张原版精灵，现用墙 {wall}");
                }
                File.WriteAllLines(Path.Combine(dir, "sprites.tsv"), manifest);
            }
            File.WriteAllLines(Path.Combine(root, "areas.tsv"), index);
            Player.TerraStats.DevImmortal = false;
            Plugin.Log.LogInfo($"[AutoTest] 地区素材 导出到 {root}");
        }

        /// <summary>
        /// The room's views where its terrain's outline is busiest: the region's cells scored over a lattice of view-sized
        /// windows (each of the scene's own cells beside an open one counts), the best few that don't overlap much, each
        /// photographed with the camera held there, the HUD, the interface and the Knight hidden.
        /// </summary>
        IEnumerator AreaViews(string scene, string dir, int want, System.Action<int> done)
        {
            var region = TerrainSystem.Current?.Region;
            var cam = GameCameras.instance.mainCamera;
            if (region == null || cam == null) { done(0); yield break; }
            float depth = -cam.transform.position.z;
            Vector2 lo = cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth)), hi = cam.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
            Vector2 size = hi - lo;
            var grid = region.Grid;
            var off = region.CellOffset;
            var edge = new List<Vector2>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.Get(x, y) != CellKind.Original) continue;
                    bool open = false;
                    for (int k = 0; k < 4 && !open; k++)
                    {
                        int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        open = nx >= 0 && ny >= 0 && nx < grid.Width && ny < grid.Height && grid.Get(nx, ny) == CellKind.Empty;
                    }
                    if (open) edge.Add(CellGrid.CellCenter(x + off.x, y + off.y));
                }
            Vector2 min = CellGrid.CellCenter(off.x, off.y), max = CellGrid.CellCenter(off.x + grid.Width - 1, off.y + grid.Height - 1);
            var picks = new List<(Vector2 at, int score)>();
            var candidates = new List<(Vector2 at, int score)>();
            for (float cy = min.y + size.y * 0.5f; cy <= Mathf.Max(min.y + size.y * 0.5f, max.y - size.y * 0.5f) + 0.01f; cy += size.y * 0.25f)
                for (float cx = min.x + size.x * 0.5f; cx <= Mathf.Max(min.x + size.x * 0.5f, max.x - size.x * 0.5f) + 0.01f; cx += size.x * 0.25f)
                {
                    int score = 0;
                    foreach (var e in edge)
                        if (Mathf.Abs(e.x - cx) < size.x * 0.5f && Mathf.Abs(e.y - cy) < size.y * 0.5f) score++;
                    candidates.Add((new Vector2(cx, cy), score));
                }
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            foreach (var c in candidates)
            {
                if (picks.Count >= want || c.score == 0) break;
                bool far = true;
                foreach (var p in picks)
                    if (Mathf.Abs(p.at.x - c.at.x) < size.x * 0.6f && Mathf.Abs(p.at.y - c.at.y) < size.y * 0.6f) far = false;
                if (far) picks.Add(c);
            }
            var hero = HeroController.instance;
            var heroMesh = hero.GetComponent<MeshRenderer>();
            var look = hero.transform.Find("Terra Player")?.gameObject;
            var hud = GameCameras.instance.hudCamera;
            bool hudWas = hud != null && hud.gameObject.activeSelf;
            // The Knight's vignette darkens all but round him: off, the room as its art is.
            var vignettes = new List<GameObject>();
            foreach (var t in hero.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains("ignette") && t.gameObject.activeSelf) vignettes.Add(t.gameObject);
            foreach (var v in vignettes) v.SetActive(false);
            if (heroMesh != null) heroMesh.enabled = false;
            if (look != null) look.SetActive(false);
            if (hud != null) hud.gameObject.SetActive(false);
            TerraMod.DevNoInterface = true;
            // The room's camera locks follow the Knight: he is carried (hidden, held still) to each view as well.
            var rb = hero.GetComponent<Rigidbody2D>();
            float gravity = rb.gravityScale;
            rb.gravityScale = 0f;
            int n = 0;
            try
            {
                foreach (var p in picks)
                {
                    hero.transform.position = Clear(hero, p.at);
                    Player.TerraCamera.DevLook = p.at;
                    for (float t = Time.time + 0.9f; Time.time < t;)
                    {
                        rb.linearVelocity = Vector2.zero;
                        yield return null;
                    }
                    yield return new WaitForEndOfFrame();
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    n++;
                    File.WriteAllBytes(Path.Combine(dir, $"view_{scene}_{n}.jpg"), tex.EncodeToJPG(92));
                    Object.Destroy(tex);
                    Plugin.Log.LogInfo($"[AutoTest] 地区素材 {scene} 画面 {n}：中心 {p.at:F1}，轮廓格 {p.score}");
                }
            }
            finally
            {
                Player.TerraCamera.DevLook = null;
                rb.gravityScale = gravity;
                TerraMod.DevNoInterface = false;
                if (hud != null) hud.gameObject.SetActive(hudWas);
                if (look != null) look.SetActive(true);
                if (heroMesh != null) heroMesh.enabled = true;
                foreach (var v in vignettes) if (v != null) v.SetActive(true);
            }
            done(n);
        }

        /// <summary>
        /// The scene's own sprites (not the mod's), the largest of each layer: the ground layer (|z| within half a unit),
        /// the foreground before it, the background behind. Each written once per area, as the sheet holds it, untinted;
        /// the renderer's tint, depth and size in the manifest.
        /// </summary>
        int ExportSceneSprites(string scene, string dir, HashSet<string> seen, List<string> manifest)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var layers = new Dictionary<string, List<SpriteRenderer>> { ["ground"] = new List<SpriteRenderer>(), ["front"] = new List<SpriteRenderer>(), ["back"] = new List<SpriteRenderer>() };
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr == null || sr.sprite == null || !sr.enabled || !sr.gameObject.activeInHierarchy || sr.gameObject.scene != active) continue;
                var tex = sr.sprite.texture;
                if (tex == null || tex.name.StartsWith("Terraria/") || tex.name.StartsWith("hole ") || tex.name.StartsWith("TerraKnight")) continue;
                if (ModOwned(sr.transform) || Effect(sr.sprite.name)) continue;
                float z = sr.transform.position.z;
                layers[Mathf.Abs(z) <= 0.5f ? "ground" : z < 0f ? "front" : "back"].Add(sr);
            }
            var limits = new Dictionary<string, int> { ["ground"] = 30, ["front"] = 10, ["back"] = 15 };
            var sheets = new Dictionary<Texture, RenderTexture>();
            int written = 0;
            try
            {
                foreach (var layer in layers)
                {
                    var list = layer.Value;
                    list.Sort((a, b) => (b.bounds.size.x * b.bounds.size.y).CompareTo(a.bounds.size.x * a.bounds.size.y));
                    int taken = 0;
                    string sub = Path.Combine(dir, "sprites_" + layer.Key);
                    foreach (var sr in list)
                    {
                        if (taken >= limits[layer.Key]) break;
                        var s = sr.sprite;
                        string id = s.texture.name + "/" + s.name;
                        if (!seen.Add(id)) continue;
                        Rect r;
                        try { r = s.textureRect; }
                        catch (System.Exception) { continue; }
                        if (r.width < 24 || r.height < 24) continue;
                        if (!sheets.TryGetValue(s.texture, out var rt))
                        {
                            rt = RenderTexture.GetTemporary(s.texture.width, s.texture.height, 0, RenderTextureFormat.ARGB32);
                            Graphics.Blit(s.texture, rt);
                            sheets[s.texture] = rt;
                        }
                        var prev = RenderTexture.active;
                        RenderTexture.active = rt;
                        var outTex = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
                        outTex.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
                        outTex.Apply();
                        RenderTexture.active = prev;
                        Directory.CreateDirectory(sub);
                        string file = Safe(s.name) + "_" + (written + 1) + ".png";
                        File.WriteAllBytes(Path.Combine(sub, file), outTex.EncodeToPNG());
                        Object.Destroy(outTex);
                        var c = sr.color;
                        var b = sr.bounds.size;
                        manifest.Add($"sprites_{layer.Key}/{file}\t{layer.Key}\t{scene}\t{sr.transform.position.z:F2}\t{sr.sortingOrder}\t#{ColorUtility.ToHtmlStringRGBA(c)}\t{b.x:F1}x{b.y:F1}\t{(int)r.width}x{(int)r.height}");
                        taken++;
                        written++;
                    }
                }
            }
            finally
            {
                foreach (var rt in sheets.Values) RenderTexture.ReleaseTemporary(rt);
            }
            return written;
        }

        /// <summary>Light, fog and particles: not the area's rock.</summary>
        static bool Effect(string name)
        {
            string n = name.ToLowerInvariant();
            foreach (var w in new[] { "light", "glow", "fog", "haze", "beam", "blur", "particle", "dust", "level_tile", "black", "shadow", "vignette", "gradient", "fade", "mist", "ray" })
                if (n.Contains(w)) return true;
            return false;
        }

        static bool ModOwned(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name.StartsWith("Terra") || t.name.StartsWith("TK ")) return true;
            return false;
        }

        static string Safe(string name)
        {
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-') chars[i] = '_';
            return new string(chars);
        }

        /// <summary>
        /// Terraria's sheets to draw to (a block's, the dirt's, each area wall the mod uses and the stone wall; the block and
        /// wall items), as they are, and frames.tsv: the frame the mod draws for each set of neighbours and variant.
        /// </summary>
        static void ExportTerrariaTemplates(string dir)
        {
            Directory.CreateDirectory(dir);
            var names = new List<string> { "Tiles_1", "Tiles_0", "Wall_1", "Item_3", "Item_2", "Item_26", "Item_30" };
            foreach (var area in artAreas)
                foreach (var scene in area.Scenes)
                {
                    string w = Areas.WallFor(scene);
                    if (!names.Contains(w)) names.Add(w);
                }
            var sizes = new List<string> { "name\twidth\theight" };
            foreach (var name in names)
            {
                Color32[] px;
                int w, h;
                try { px = TerrariaAssets.Pixels(name, out w, out h); }
                catch (System.Exception) { sizes.Add($"{name}\t缺\t"); continue; }
                var image = new Texture2D(w, h, TextureFormat.RGBA32, false);
                image.SetPixels32(px);
                image.Apply();
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), image.EncodeToPNG());
                Object.Destroy(image);
                sizes.Add($"{name}\t{w}\t{h}");
            }
            File.WriteAllLines(Path.Combine(dir, "sizes.tsv"), sizes);
            var frames = new List<string> { "kind\tup\tdown\tleft\tright\tvariant\tx\ty\tsize" };
            for (int m = 0; m < 16; m++)
            {
                bool up = (m & 8) != 0, down = (m & 4) != 0, left = (m & 2) != 0, right = (m & 1) != 0;
                for (int v = 0; v < 3; v++)
                {
                    var f = TileFraming.Frame(up, down, left, right, v);
                    frames.Add($"block\t{B(up)}\t{B(down)}\t{B(left)}\t{B(right)}\t{v}\t{f.x}\t{f.y}\t{TileFraming.FrameSize}");
                }
                if (m == 15)
                {
                    // The wall's interior repeats a 3x3 pattern over the cells (Framing.centerWallFrameLookup).
                    for (int cx = 0; cx < 3; cx++)
                        for (int cy = 0; cy < 3; cy++)
                            for (int v = 0; v < 3; v++)
                            {
                                var f = TileFraming.WallFrame(true, true, true, true, v, cx, -cy - 1);
                                frames.Add($"wall\t1\t1\t1\t1\t{v}@{cx},{cy}\t{f.x}\t{f.y}\t{TileFraming.WallFrameSize}");
                            }
                }
                else
                    for (int v = 0; v < 3; v++)
                    {
                        var f = TileFraming.WallFrame(up, down, left, right, v);
                        frames.Add($"wall\t{B(up)}\t{B(down)}\t{B(left)}\t{B(right)}\t{v}\t{f.x}\t{f.y}\t{TileFraming.WallFrameSize}");
                    }
            }
            File.WriteAllLines(Path.Combine(dir, "frames.tsv"), frames);
        }

        static int B(bool b) => b ? 1 : 0;
    }
}
