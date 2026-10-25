using System.Collections;
using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "tileanim"): Terraria's placed things animated as Terraria animates them. Every animated station's frames
    /// read against its sheet (inside it, none empty, not all alike). In Dirtmouth a campfire, a Furnace, a Glass Kiln, an
    /// Ancient Manipulator and a Void Vault set down on the street and four plain torches beside them: the campfire steps
    /// through its 8 frames 4 ticks apart with Flame_15's same frame over it and smoke rising off it, the Furnace through
    /// its 12 six ticks apart, the kiln through its 4 with Flame_10 over it, the manipulator's glow fades from colour to
    /// colour, the Void Vault's lens turns over its pedestal, each torch's seven Flame_0 copies move every fifth tick and
    /// throw off dust; looked away from, a station holds its frame. Watched and photographed through a camera of the test's
    /// own (close-ups at two moments), as the hidden test window draws nothing.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator TileAnimTest(string scene, HeroController hero)
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 物块动画 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }

            string audit = Stations.DevAnimAudit(out bool auditOk);
            Check("各动画工作站的帧在贴图内、没有空帧、帧间有变化", auditOk, audit);

            yield return GoTo("Town", EntryGateOf("Town"));
            scene = "Town";
            hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            yield return new WaitForSeconds(1.5f);
            QuietAround(hero.transform.position, 40f);
            var col = hero.GetComponent<BoxCollider2D>();
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            var feet = new Vector2(col.bounds.center.x, col.bounds.min.y);

            // The stations at the nearest even spots round the hero, the torches on the free floor cells beside them.
            int before = Stations.Count;
            var kinds = new[] { StationKind.Campfire, StationKind.Furnace, StationKind.GlassKiln, StationKind.AncientManipulator, StationKind.VoidVault };
            var boxes = new Dictionary<StationKind, Rect>();
            var offsets = new List<float>();
            for (float d = 0.75f; d <= 10f; d += 0.25f) { offsets.Add(d); offsets.Add(-d); }
            foreach (var k in kinds)
            {
                foreach (float d in offsets)
                    if (Stations.TryPlaceNatural(k, feet + new Vector2(d, 0.3f), out _)) break;
                var box = Stations.DevBoxOf(k);
                if (box.HasValue) boxes[k] = box.Value;
                else Plugin.Log.LogInfo($"[AutoTest] 物块动画 放不下 {k}：{Stations.DevWhy}");
            }
            var torches = new List<Vector2Int>();
            string torchWhy = "";
            foreach (float d in offsets)
            {
                if (torches.Count >= 4) break;
                var hit = Physics2D.Raycast(feet + new Vector2(d, 1f), Vector2.down, 2.5f, terrain);
                if (hit.collider == null) continue;
                var c = CellGrid.WorldToCell(new Vector2(feet.x + d, hit.point.y + 0.1f));
                bool near = false;
                foreach (var t in torches) near |= Mathf.Abs(t.x - c.x) < 2;
                if (near) continue;
                if (TerraWorld.Get(c.x, c.y) != CellKind.Empty || !TerraWorld.IsSolid(c.x, c.y - 1) || Stations.Covers(c))
                {
                    if (torchWhy.Length < 200) torchWhy += $" {c}:{TerraWorld.Get(c.x, c.y)}/{TerraWorld.Get(c.x, c.y - 1)}/{Stations.Covers(c)}";
                    continue;
                }
                TerraWorld.Set(c.x, c.y, CellKind.Torch);
                RegionalTorches.Placed(c, Items.ItemType.Torch);
                if (TerraWorld.Get(c.x, c.y) == CellKind.Torch) torches.Add(c);
            }
            // The camera on the middle of what was set down.
            var area = new Rect(feet, Vector2.zero);
            foreach (var box in boxes.Values) area = Rect.MinMaxRect(Mathf.Min(area.xMin, box.xMin), Mathf.Min(area.yMin, box.yMin), Mathf.Max(area.xMax, box.xMax), Mathf.Max(area.yMax, box.yMax));
            foreach (var c in torches)
            {
                var t = TerraWorld.RegionAt(c.x, c.y);
                Vector2 at = (Vector2)t.Root.position + CellGrid.CellCenter(c.x - t.CellOffset.x, c.y - t.CellOffset.y);
                area = Rect.MinMaxRect(Mathf.Min(area.xMin, at.x), Mathf.Min(area.yMin, at.y), Mathf.Max(area.xMax, at.x), Mathf.Max(area.yMax, at.y));
            }
            Check("摆好篝火、熔炉、玻璃窑、远古操纵机、虚空保险库和 4 支火把", boxes.Count == kinds.Length && torches.Count == 4,
                $"工作站 {boxes.Count}/{kinds.Length}，火把 {torches.Count}/4，范围 {area}{(torchWhy.Length > 0 ? "，跳过的格" + torchWhy : "")}");
            var mid = area.center + new Vector2(0f, 1f);
            Player.TerraCamera.DevLook = mid;

            // A camera of the test's own, drawn by hand every frame: the test window is hidden and its own camera draws
            // nothing there, while what is drawn by any camera counts as on screen.
            var probeObject = new GameObject("Terra Tile Animation Camera");
            var probe = probeObject.AddComponent<Camera>();
            probe.enabled = false;
            probe.orthographic = true;
            // Only the depths the game is played at (the scene's foreground and far backdrops left out).
            probe.nearClipPlane = 0.5f;
            probe.farClipPlane = 3f;
            probe.clearFlags = CameraClearFlags.SolidColor;
            probe.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            var target = new RenderTexture(1280, 720, 24);
            probe.targetTexture = target;
            void Look(Vector2 at, float halfHeight)
            {
                probe.transform.position = new Vector3(at.x, at.y, -1f);
                probe.orthographicSize = halfHeight;
            }
            void Save(string name)
            {
                probe.Render();
                var was = RenderTexture.active;
                RenderTexture.active = target;
                var tex = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                tex.Apply();
                RenderTexture.active = was;
                string path = System.IO.Path.Combine(outDir, $"{scene}_{name}.png");
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.Destroy(tex);
                Plugin.Log.LogInfo($"[AutoTest] 截图 {path}");
            }
            IEnumerator Watch(float seconds)
            {
                for (float t = 0f; t < seconds; t += Time.deltaTime)
                {
                    yield return null;
                    probe.Render();
                }
            }
            // What was set down moved to a layer of its own and the camera drawing only that (the whole scene drawn by it
            // comes out black), its first frames made first: the stations' overlays, the torches' cells and flames, the dust.
            const int probeLayer = 30;
            var moved = new List<(GameObject Go, int Layer)>();
            void Move(GameObject go)
            {
                if (go == null) return;
                foreach (var tr in go.GetComponentsInChildren<Transform>(true))
                {
                    moved.Add((tr.gameObject, tr.gameObject.layer));
                    tr.gameObject.layer = probeLayer;
                }
            }
            for (int k = 0; k < 3; k++) yield return null;
            foreach (var k in kinds)
            {
                var sr = Stations.DevRenderer(k, out _);
                if (sr != null) Move(sr.gameObject);
            }
            var allRenderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            foreach (var c in torches)
            {
                var r = TerraWorld.RegionAt(c.x, c.y);
                Vector2 at = (Vector2)r.Root.position + CellGrid.CellCenter(c.x - r.CellOffset.x, c.y - r.CellOffset.y);
                foreach (var sr in allRenderers)
                    if (sr != null && ((Vector2)sr.transform.position - at).sqrMagnitude < 0.02f && (sr.name == "Terra Cell" || sr.GetComponentInParent<RegionalTorchFlames.Flame>() != null))
                        Move(sr.gameObject);
            }
            if (Effects.TerraParticles.Instance != null) Move(Effects.TerraParticles.Instance.gameObject);
            probe.cullingMask = 1 << probeLayer;
            Look(mid, area.width * 0.3f + 1.5f);
            yield return Watch(0.8f);
            Save("tileanim_0_row");

            RegionalTorchFlames.Flame FlameAt(Vector2Int c)
            {
                var at = TerraWorld.RegionAt(c.x, c.y);
                if (at == null) return null;
                Vector2 centre = (Vector2)at.Root.position + CellGrid.CellCenter(c.x - at.CellOffset.x, c.y - at.CellOffset.y);
                foreach (var f in Object.FindObjectsByType<RegionalTorchFlames.Flame>(FindObjectsSortMode.None))
                    if (f != null && ((Vector2)f.transform.position - centre).sqrMagnitude < 0.01f) return f;
                return null;
            }
            var flame = torches.Count > 0 ? FlameAt(torches[0]) : null;
            var flameLayers = flame != null ? flame.GetComponentsInChildren<SpriteRenderer>() : new SpriteRenderer[0];
            string flameTex = flameLayers.Length > 0 && flameLayers[0].sprite != null ? flameLayers[0].sprite.texture.name : "无";
            Check("普通火把有 Flame_0 的 7 层火焰", flameLayers.Length == RegionalTorchFlames.Layers && flameTex.Contains("Flame_0"), $"{flameLayers.Length} 层，贴图 {flameTex}");

            // Frame by frame for a while: what each picture shows against the frame its kind is at.
            int Shown(SpriteRenderer sr, int frames)
            {
                if (sr == null || sr.sprite == null) return -1;
                var r = sr.sprite.rect;
                return frames - 1 - Mathf.RoundToInt(r.y / r.height);
            }
            var frameCount = new Dictionary<StationKind, int> { [StationKind.Campfire] = 8, [StationKind.Furnace] = 12, [StationKind.GlassKiln] = 4 };
            var tickEach = new Dictionary<StationKind, int> { [StationKind.Campfire] = 4, [StationKind.Furnace] = 6, [StationKind.GlassKiln] = 5 };
            var seen = new Dictionary<StationKind, HashSet<int>>();
            var steps = new Dictionary<StationKind, int>();
            var last = new Dictionary<StationKind, int>();
            var outOfStep = new Dictionary<StationKind, int>();
            var overlayOff = new Dictionary<StationKind, int>();
            foreach (var k in frameCount.Keys) { seen[k] = new HashSet<int>(); steps[k] = 0; last[k] = -1; outOfStep[k] = 0; overlayOff[k] = 0; }
            var lensSeen = new HashSet<Sprite>();
            float glowMin = 1f, glowMax = 0f, glowSumOff = 0f;
            int flameMoves = 0, flameOut = 0;
            var lastOffsets = flame != null ? (Vector2[])flame.DevOffsets.Clone() : null;
            int smoke0 = Stations.DevSmoke, dust0 = RegionalTorchFlames.Flame.DevDust;
            float t0 = Time.time;
            while (Time.time - t0 < 1.5f)
            {
                yield return null;
                probe.Render();
                foreach (var k in frameCount.Keys)
                {
                    var sr = Stations.DevRenderer(k, out var fx);
                    int f = Shown(sr, frameCount[k]);
                    if (f < 0) continue;
                    seen[k].Add(f);
                    if (f != Stations.DevFrame(k)) outOfStep[k]++;
                    if (last[k] >= 0 && f != last[k]) steps[k]++;
                    last[k] = f;
                    if (k != StationKind.Furnace && (fx == null || fx[0] == null || Shown(fx[0], frameCount[k]) != f)) overlayOff[k]++;
                }
                Stations.DevRenderer(StationKind.VoidVault, out var lens);
                if (lens != null && lens[0] != null && lens[0].sprite != null) lensSeen.Add(lens[0].sprite);
                Stations.DevRenderer(StationKind.AncientManipulator, out var glow);
                if (glow != null && glow[0] != null)
                {
                    float a = glow[0].color.a;
                    glowMin = Mathf.Min(glowMin, a);
                    glowMax = Mathf.Max(glowMax, a);
                    glowSumOff = Mathf.Max(glowSumOff, Mathf.Abs(a + glow[1].color.a - 1f));
                }
                if (flame != null)
                {
                    bool jumped = false;
                    for (int k = 0; k < RegionalTorchFlames.Layers; k++)
                    {
                        var o = flame.DevOffsets[k];
                        if (o != lastOffsets[k]) jumped = true;
                        if (o.x < -1.5f || o.x > 1.5f || o.y < -3.5f || o.y > 0f) flameOut++;
                        lastOffsets[k] = o;
                    }
                    if (jumped) flameMoves++;
                }
            }
            float span = Time.time - t0;
            foreach (var k in frameCount.Keys)
            {
                float want = span * 60f / tickEach[k];
                Check($"{k} 依次换帧", seen[k].Count == frameCount[k] && outOfStep[k] <= 1 && steps[k] >= want * 0.75f && steps[k] <= want * 1.25f + 1f,
                    $"见到 {seen[k].Count}/{frameCount[k]} 帧，{span:F2} 秒换了 {steps[k]} 次（每 {tickEach[k]} tick 一帧应约 {want:F1} 次），与全局帧不一致 {outOfStep[k]} 次");
                if (k != StationKind.Furnace)
                {
                    Stations.DevRenderer(k, out var fx);
                    string tex = fx != null && fx[0] != null && fx[0].sprite != null ? fx[0].sprite.texture.name : "无";
                    string want2 = k == StationKind.Campfire ? "Flame_15" : "Flame_10";
                    Check($"{k} 火焰层同帧", tex.Contains(want2) && overlayOff[k] == 0, $"火焰层贴图 {tex}，与底图不同帧 {overlayOff[k]} 次");
                }
            }
            {
                var sr = Stations.DevRenderer(StationKind.VoidVault, out var lens);
                string tex = sr != null && sr.sprite != null ? sr.sprite.texture.name : "无";
                Check("虚空保险库画底座、透镜转动", tex.Contains("#station54") && lensSeen.Count >= 6 && lens != null && lens.Length == 4,
                    $"底图 {tex}，透镜见到 {lensSeen.Count}/8 帧，{(lens != null ? lens.Length : 0)} 个子图");
            }
            Check("远古操纵机的辉光渐变", glowMax - glowMin > 0.015f && glowSumOff < 0.01f, $"前一色透明度 {glowMin:F3}~{glowMax:F3}，两色之和偏离 1 至多 {glowSumOff:F3}");
            float flameWant = span * 60f / RegionalTorchFlames.SeedTicks;
            Check("火把火焰每 5 tick 跳一次", flameMoves >= flameWant * 0.75f && flameMoves <= flameWant * 1.25f + 1f && flameOut == 0,
                $"{span:F2} 秒跳了 {flameMoves} 次（应约 {flameWant:F1} 次），越界 {flameOut} 次");
            Check("篝火冒烟", Stations.DevSmoke - smoke0 > 0, $"{span:F2} 秒 {Stations.DevSmoke - smoke0} 团烟");

            // Two moments a few ticks apart, close up.
            if (boxes.TryGetValue(StationKind.Campfire, out var fire))
            {
                Look(fire.center + new Vector2(0f, 0.3f), 1.2f);
                yield return Watch(0.3f);
                Save("tileanim_1_campfire_a");
                yield return Watch(0.1f);
                Save("tileanim_2_campfire_b");
            }
            if (torches.Count > 0)
            {
                var t = TerraWorld.RegionAt(torches[0].x, torches[0].y);
                Vector2 at = (Vector2)t.Root.position + CellGrid.CellCenter(torches[0].x - t.CellOffset.x, torches[0].y - t.CellOffset.y);
                Look(at, 0.5f);
                yield return Watch(0.2f);
                Save("tileanim_3_torch_a");
                yield return Watch(0.1f);
                Save("tileanim_4_torch_b");
            }
            if (boxes.TryGetValue(StationKind.AncientManipulator, out var mani) && boxes.TryGetValue(StationKind.VoidVault, out var vault))
            {
                Look((mani.center + vault.center) / 2f, 1.6f);
                yield return Watch(0.2f);
                Save("tileanim_5_glow_lens_a");
                yield return Watch(0.5f);
                Save("tileanim_5_glow_lens_b");
            }
            if (boxes.TryGetValue(StationKind.Furnace, out var furnace) && boxes.TryGetValue(StationKind.GlassKiln, out var kiln))
            {
                Look((furnace.center + kiln.center) / 2f, 1.6f);
                yield return Watch(0.2f);
                Save("tileanim_6_furnace_kiln");
            }

            // The torches' dust over a few seconds (1 in 40 every fourth tick each).
            Look(mid, area.width * 0.3f + 1.5f);
            yield return Watch(4f);
            int dust = RegionalTorchFlames.Flame.DevDust - dust0;
            Check("火把冒火星", dust > 0, $"4 支火把约 {Time.time - t0:F1} 秒 {dust} 粒");
            Save("tileanim_7_row_later");

            // Looked away from: the pictures hold their frames.
            var campfire = Stations.DevRenderer(StationKind.Campfire, out _);
            Player.TerraCamera.DevLook = mid + new Vector2(80f, 40f);
            Look(mid + new Vector2(80f, 40f), 3f);
            yield return Watch(0.5f);
            int held = Shown(campfire, 8), heldNow = Stations.DevFrame(StationKind.Campfire);
            bool away = campfire != null && !campfire.isVisible;
            int changed = 0;
            for (int k = 0; k < 30; k++)
            {
                yield return null;
                probe.Render();
                if (Shown(campfire, 8) != held) changed++;
            }
            Check("看不见时不换帧", away && changed == 0, $"看不见 {away}，停在第 {held} 帧（全局 {heldNow}→{Stations.DevFrame(StationKind.Campfire)}），30 帧里变了 {changed} 次");
            Player.TerraCamera.DevLook = mid;
            Look(mid, area.width * 0.3f + 1.5f);
            yield return Watch(0.3f);
            Check("回来后接着换帧", Shown(campfire, 8) == Stations.DevFrame(StationKind.Campfire), $"画 {Shown(campfire, 8)}，全局 {Stations.DevFrame(StationKind.Campfire)}");

            Player.TerraCamera.DevLook = null;
            foreach (var (go, layer) in moved) if (go != null) go.layer = layer;
            probe.targetTexture = null;
            Object.Destroy(target);
            Object.Destroy(probeObject);
            foreach (var c in torches) TerraWorld.Set(c.x, c.y, CellKind.Empty);
            Stations.DevTrim(before);
            yield return new WaitForSeconds(0.3f);
            Plugin.Log.LogInfo($"[AutoTest] 物块动画 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
