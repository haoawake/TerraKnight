using System.Collections;
using System.Collections.Generic;
using System.IO;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_17mapfix"): the map's fixes of 2026-10-17. The layout: rooms' insides lying on each other (the
    /// shallow and deep layers, from the atlas where the layout puts each scene), gates that meet; the reveal round the
    /// player (18 cells to the sides, 23 up and down); a cell dug and a block placed on the corner map and the full map in
    /// the next frames; a spike cut away gone from the map; the arrows at gates that don't meet (hovered, followed); the
    /// townsfolk's heads on the corner map and the full map (named under the mouse, kept when Dirtmouth is left); the rooms
    /// round the False Knight (their map cells against the game's colliders, their pictures and photographs); the full map
    /// at Deepnest's and Queen's Gardens' joins, close and far.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly string[] fkRooms = { "Crossroads_10", "Crossroads_06", "Crossroads_21", "Crossroads_05", "Crossroads_09", "Crossroads_08", "Crossroads_03", "Crossroads_47", "Crossroads_13", "Crossroads_30", "Crossroads_37" };

        static readonly string[] mapFixViews =
        {
            "Deepnest_East_06", "Deepnest_East_03", "Hive_04", "Deepnest_26b", "Deepnest_42", "Deepnest_03", "Deepnest_34", "Deepnest_35", "Deepnest_10",
            "Deepnest_01", "Fungus3_49", "Fungus3_13", "Fungus3_10", "Fungus3_08", "Fungus3_50", "Fungus3_40", "Fungus3_34", "Fungus2_18", "Crossroads_10",
        };

        IEnumerator MapFix17()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;

            // ---- the layout
            string ov = UI.WorldMap.DevOverlap(out int doubled, out int pairs, out int big, fkRooms);
            FxCheck("地图布局：浅层两间房里面叠在一起的格子少（原来 27161 格、55 对、超过 200 格的 26 对）", doubled < 6000 && big <= 8, $"{doubled} 格，{pairs} 对，超过 200 格 {big} 对；{ov}");
            string gates = UI.WorldMap.DevGateCounts(out int marked, out int met);
            FxCheck("门：大部分接上，没接上的都标出来", met > 300 && marked > 0, gates);

            // ---- the reach round the player: 18 to the sides, 23 up and down, on an open grid
            {
                const int w = 96, h = 96;
                var seen = new bool[w * h];
                UI.MapExploration.Reveal(w, h, 48, 48, (x, y) => false, (x, y) => { if (seen[y * w + x]) return false; seen[y * w + x] = true; return true; });
                bool up = seen[(48 + 23) * w + 48], upOut = seen[(48 + 24) * w + 48], down = seen[(48 - 23) * w + 48];
                bool side = seen[48 * w + 48 + 18], sideOut = seen[48 * w + 48 + 19], left = seen[48 * w + 48 - 18];
                FxCheck("照亮范围：左右 18 格、上下 23 格（各多 10 格）", up && down && !upOut && side && left && !sideOut,
                    $"上23 {up} 下23 {down} 上24 {upOut}；右18 {side} 左18 {left} 右19 {sideOut}；常量 {UI.MapExploration.Radius}/{UI.MapExploration.RadiusY}");
                string checks = UI.WorldMap.DevExplorationChecks(out var rep) ? "通过" : "不通过";
                FxCheck("地图探索的原有检查", checks == "通过", rep);
            }

            yield return LiveEdits17();
            yield return SpikeCut17();
            yield return GateMarkers17();
            yield return NpcHeads17();
            yield return FalseKnightRooms17();
            yield return MapViews17();
            UI.WorldMap.DevMouse(null);
            if (UI.WorldMap.Open) UI.WorldMap.DevSetOpen(false);
            UI.WorldMap.DevStyle(1);
        }

        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        /// <summary>A floor cell of the scene's own ground some cells beside the Knight (global cells).</summary>
        static Vector2Int? FloorNear(HeroController hero, int dx)
        {
            var feet = CellGrid.WorldToCell(hero.GetComponent<Collider2D>().bounds.center) + new Vector2Int(dx, 0);
            for (int dy = 0; dy > -12; dy--)
            {
                var c = feet + new Vector2Int(0, dy);
                if (TerraWorld.Get(c.x, c.y) == CellKind.Original && TerraWorld.Get(c.x, c.y + 1) == CellKind.Empty) return c;
            }
            return null;
        }

        /// <summary>A cell dug out and a block put in: on the corner map in the next frames, then on the open full map.</summary>
        IEnumerator LiveEdits17()
        {
            if (GameManager.instance.sceneName != "Crossroads_01") yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(1f);
            var hero = HeroController.instance;
            var region = TerraWorld.Live;
            string scene = region.SceneName;
            UI.WorldMap.DevStyle(1);
            if (UI.WorldMap.Open) UI.WorldMap.DevSetOpen(false);
            UI.WorldMap.DevSeeAll(scene);
            yield return new WaitForSecondsRealtime(0.8f);
            var cell = FloorNear(hero, 3) ?? FloorNear(hero, -3) ?? FloorNear(hero, 5);
            if (!cell.HasValue) { FxCheck("挖开/填上实时显示", false, "Knight 旁边找不到地面格"); yield break; }
            var c = cell.Value;
            int lx = c.x - region.CellOffset.x, ly = c.y - region.CellOffset.y;
            var dug = UI.WorldMap.DevColor(scene, "dug");
            var dirt = UI.WorldMap.DevColor(scene, "dirt");
            int patched0 = UI.WorldMap.DevPatched;

            TerraWorld.Set(c.x, c.y, CellKind.Empty);
            yield return null;
            yield return null;
            string s1 = UI.WorldMap.DevCellPixels(scene, lx, ly, out var solid1, out var open1);
            FxCheck("挖掉地面：小地图下一帧就是挖开的颜色", Same(open1, dug) && solid1.a == 0 && UI.WorldMap.DevPatched > patched0, $"格子 {c}（房间里 {lx},{ly}）：{s1}，补画 {UI.WorldMap.DevPatched - patched0} 次");
            Shot(scene, "mapfix_dug_minimap");
            yield return null;

            TerraWorld.Set(c.x, c.y, CellKind.Dirt);
            yield return null;
            yield return null;
            string s2 = UI.WorldMap.DevCellPixels(scene, lx, ly, out var solid2, out var open2);
            FxCheck("填上土块：小地图下一帧就是土块的颜色", Same(solid2, dirt) && open2.a == 0, s2);

            // The full map open: dug again, then filled, each in the next frame.
            UI.WorldMap.DevSetOpen(true);
            UI.WorldMap.DevCentre(CellGrid.CellCenter(c.x, c.y));
            UI.WorldMap.DevZoom(12f);
            yield return new WaitForSecondsRealtime(0.3f);
            TerraWorld.Set(c.x, c.y, CellKind.Empty);
            yield return null;
            yield return null;
            string s3 = UI.WorldMap.DevCellPixels(scene, lx, ly, out var solid3, out var open3);
            bool fullDug = Same(open3, dug) && solid3.a == 0;
            Shot(scene, "mapfix_dug_fullmap");
            yield return null;
            TerraWorld.Set(c.x, c.y, CellKind.Dirt);
            yield return null;
            yield return null;
            string s4 = UI.WorldMap.DevCellPixels(scene, lx, ly, out var solid4, out _);
            FxCheck("大地图开着：挖开、填上都在下一帧画出", fullDug && Same(solid4, dirt), $"挖开后 {s3}；填上后 {s4}");
            UI.WorldMap.DevSetOpen(false);
            yield return null;
        }

        /// <summary>A spike cut away (as a pickaxe does, DugHazards.CutSpike): its cell on the map no longer spikes, at once.</summary>
        IEnumerator SpikeCut17()
        {
            yield return GoTo("Tutorial_01", EntryGateOf("Tutorial_01"));
            var region = TerraWorld.Live;
            string scene = region?.SceneName;
            float wait = Time.time + 4f;
            while (UI.WorldMap.DevSpikeCells(scene).Count == 0 && Time.time < wait) yield return null;
            UI.WorldMap.DevSeeAll(scene);
            UI.WorldMap.DevStyle(1);
            yield return new WaitForSecondsRealtime(0.8f);
            var spikes = UI.WorldMap.DevSpikeCells(scene);
            Vector2Int? pick = null;
            foreach (var l in spikes)
            {
                var g = l + region.CellOffset;
                if (DugHazards.IsSpikeCell(g)) { pick = l; break; }
            }
            if (!pick.HasValue) { FxCheck("拆掉的尖刺从地图上消失", false, $"{scene} 地图上的尖刺格 {spikes.Count} 个，没有一个是能挖的尖刺"); yield break; }
            var p = pick.Value;
            var gc = p + region.CellOffset;
            string before = UI.WorldMap.DevCellPixels(scene, p.x, p.y, out _, out var open0);
            string nameBefore = UI.WorldMap.DevNameAtWorld(CellGrid.CellCenter(gc.x, gc.y));
            var spikeColor = UI.WorldMap.DevColor(scene, "spikes");
            DugHazards.CutSpike(gc);
            yield return null;
            yield return null;
            string after = UI.WorldMap.DevCellPixels(scene, p.x, p.y, out _, out var open1);
            string nameAfter = UI.WorldMap.DevNameAtWorld(CellGrid.CellCenter(gc.x, gc.y));
            // Every spike the cut took with it (its neighbours cut by the dig too) gone the same way.
            int left = 0;
            foreach (var l in UI.WorldMap.DevSpikeCells(scene)) if (l == p) left++;
            FxCheck("拆掉的尖刺：地图上那格不再是尖刺（下一帧），名字也不是尖刺", Same(open0, spikeColor) && !Same(open1, spikeColor) && Same(open1, UI.WorldMap.DevColor(scene, "dug")) && nameAfter != "尖刺" && left == 0,
                $"格子 {p}：拆前 {before}（{nameBefore}）；拆后 {after}（{nameAfter}）");
            UI.WorldMap.DevSetOpen(true);
            UI.WorldMap.DevCentre(CellGrid.CellCenter(gc.x, gc.y));
            UI.WorldMap.DevZoom(10f);
            yield return new WaitForSecondsRealtime(0.4f);
            Shot(scene, "mapfix_spike_cut");
            yield return null;
            UI.WorldMap.DevSetOpen(false);
        }

        /// <summary>The arrows at gates that don't meet: one in Deepnest or Queen's Gardens hovered (its name, the line), then clicked.</summary>
        IEnumerator GateMarkers17()
        {
            UI.WorldMap.RevealWorld();
            var marked = UI.WorldMap.DevMarkedGates();
            var sb = new System.Text.StringBuilder();
            foreach (var m in marked) sb.Append($" {m.Scene}.{m.Gate}→{m.To}{(m.SameLayer ? "" : "(别层)")}");
            var ping = Terraria.TerrariaAssets.Texture("UI/MapPing");
            Plugin.Log.LogInfo($"[AutoTest] 地图修复 标出的门 {marked.Count} 个（地图提示圈 {(ping != null ? ping.width + "x" + ping.height : "没有")}）：{sb}");
            UI.WorldMap.DevSetOpen(true);
            UI.WorldMap.DevPickLayer(1);
            int tried = 0, hovered = 0, jumped = 0;
            var picks = new List<(string Scene, string Gate, string To, bool SameLayer)>();
            foreach (var m in marked)
                if (m.SameLayer && (m.Scene.StartsWith("Deepnest") || m.Scene.StartsWith("Fungus3") || m.Scene.StartsWith("Crossroads")) && picks.Count < 4) picks.Add(m);
            foreach (var m in picks)
            {
                tried++;
                UI.WorldMap.DevPickLayer(1);
                UI.WorldMap.DevCentreGate(m.Scene, m.Gate);
                UI.WorldMap.DevZoom(3f);
                UI.WorldMap.DevMouse(null);
                yield return new WaitForSecondsRealtime(0.3f);
                var at = UI.WorldMap.DevGateScreen(m.Scene, m.Gate);
                if (!at.HasValue) { Plugin.Log.LogInfo($"[AutoTest] 地图修复 {m.Scene}.{m.Gate} 的箭头没画出来（画了 {UI.WorldMap.DevDrawnGates} 个）"); continue; }
                UI.WorldMap.DevMouse(at);
                yield return new WaitForSecondsRealtime(0.4f);
                string label = UI.WorldMap.DevLabel;
                if (!string.IsNullOrEmpty(label) && label.Contains("通往")) hovered++;
                Shot("Town", $"mapfix_gate_{m.Scene}_{m.Gate}_hover");
                yield return null;
                string jump = UI.WorldMap.DevJumpGate(m.Scene, m.Gate);
                if (jump.StartsWith("从")) jumped++;
                UI.WorldMap.DevMouse(null);
                yield return new WaitForSecondsRealtime(0.4f);
                Shot("Town", $"mapfix_gate_{m.Scene}_{m.Gate}_other_end");
                yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 地图修复 门 {m.Scene}.{m.Gate}→{m.To}：鼠标在 {at.Value:F0}，提示「{label}」，点了 {jump}；{UI.WorldMap.DevGateMarkers(m.Scene)}");
            }
            FxCheck("没接上的门：箭头画出来，指着说通往哪里，点了跳到另一头", marked.Count > 0 && tried > 0 && hovered == tried && jumped == tried, $"标出 {marked.Count} 个，试了 {tried}，提示对 {hovered}，跳过去 {jumped}");
            UI.WorldMap.DevSetOpen(false);
            yield return null;
        }

        /// <summary>The townsfolk on the corner map and the full map in Dirtmouth (a name under the mouse), and still there from elsewhere.</summary>
        IEnumerator NpcHeads17()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(4f);
            var hero = HeroController.instance;
            int live = Town.TownNpcs.Count;
            UI.WorldMap.DevStyle(1);
            UI.WorldMap.DevMiniZoom(0.6f);
            yield return null;
            yield return null;
            int mini = UI.WorldMap.DevNpcHeadCount;
            string miniList = UI.WorldMap.DevNpcHeads();
            Shot("Town", "mapfix_npc_minimap");
            yield return null;
            UI.WorldMap.DevMiniZoom(1.05f);
            UI.WorldMap.DevSetOpen(true);
            UI.WorldMap.DevPickLayer(0);
            UI.WorldMap.DevCentre(hero.transform.position);
            UI.WorldMap.DevZoom(3f);
            yield return new WaitForSecondsRealtime(0.4f);
            int full = UI.WorldMap.DevNpcHeadCount;
            var first = UI.WorldMap.DevNpcHeadScreen(0);
            string label = null;
            if (first.HasValue)
            {
                UI.WorldMap.DevMouse(first);
                yield return new WaitForSecondsRealtime(0.4f);
                label = UI.WorldMap.DevLabel;
            }
            Shot("Town", "mapfix_npc_fullmap");
            yield return null;
            UI.WorldMap.DevMouse(null);
            UI.WorldMap.DevSetOpen(false);
            FxCheck("镇民头像：小地图和大地图上都画出，指着显示名字", live > 0 && mini > 0 && full >= live && !string.IsNullOrEmpty(label),
                $"镇上 {live} 个；小地图画了 {mini}（{miniList}）；大地图 {full}，指着第一个显示「{label}」；{UI.WorldMap.DevNpcPlaces()}");

            // Away from Dirtmouth: the townsfolk still on the map where they were.
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            UI.WorldMap.DevSetOpen(true);
            UI.WorldMap.DevPickLayer(0);
            UI.WorldMap.DevCentreScene("Town");
            UI.WorldMap.DevZoom(2f);
            yield return new WaitForSecondsRealtime(0.4f);
            int away = UI.WorldMap.DevNpcHeadCount;
            Shot("Crossroads_01", "mapfix_npc_from_crossroads");
            yield return null;
            UI.WorldMap.DevSetOpen(false);
            FxCheck("离开德特茅斯后大地图上仍有镇民头像", away >= live, $"{away} 个；{UI.WorldMap.DevNpcPlaces()}");
        }

        /// <summary>
        /// The rooms round the False Knight (beaten, as the player's save is): each one's open map cells against the game's own
        /// solid colliders there, its cells as a picture beside a photograph of the whole scene, the full map round it.
        /// </summary>
        IEnumerator FalseKnightRooms17()
        {
            var pd = PlayerData.instance;
            bool fk = pd.GetBool("falseKnightDefeated");
            pd.SetBool("falseKnightDefeated", true);
            SceneData.instance.SaveMyState(new PersistentBoolData { sceneName = "Crossroads_10", id = "Battle Scene", activated = true });
            int total = 0;
            var sb = new System.Text.StringBuilder();
            foreach (var scene in fkRooms)
            {
                yield return GoTo(scene, EntryGateOf(scene));
                pd.isInvincible = true;
                float wait = Time.time + 4f;
                while (UI.WorldMap.DevCellPixels(scene, 0, 0, out _, out _) == "没有这个房间" && Time.time < wait) yield return null;
                yield return new WaitForSeconds(0.5f);
                string ph = UI.WorldMap.DevPhantoms(scene, out int n);
                total += n;
                sb.Append(" ").Append(ph);
                Plugin.Log.LogInfo($"[AutoTest] 地图修复 {ph}");
                Plugin.Log.LogInfo($"[AutoTest] 地图修复 {UI.WorldMap.DevRoomPicture(scene, Path.Combine(outDir, scene + "_mapfix_cells.png"))}");
                RoomPhoto(scene, Path.Combine(outDir, scene + "_mapfix_photo.png"));
                UI.WorldMap.DevSeeAll(scene);
                UI.WorldMap.DevSetOpen(true);
                UI.WorldMap.DevPickLayer(0);
                UI.WorldMap.DevCentreScene(scene);
                UI.WorldMap.DevZoom(3f);
                yield return new WaitForSecondsRealtime(0.4f);
                Shot(scene, "mapfix_fk_fullmap");
                yield return null;
                UI.WorldMap.DevSetOpen(false);
                yield return null;
            }
            string ov = UI.WorldMap.DevOverlap(out _, out _, out _, fkRooms);
            FxCheck("假骑士附近的房间：地图上空着的格子在游戏里没有实心碰撞", total == 0, sb.ToString());
            FxCheck("假骑士附近的房间：和别的房间里面不叠在一起（看「关注」）", !ov.Contains("[关注"), ov);
            pd.SetBool("falseKnightDefeated", fk);
        }

        /// <summary>
        /// A photograph of a whole loaded scene: a camera like the game's, pulled back to take in the scene's grid, rendered
        /// once into a picture (the terrain at its true place; the parallax layers drift).
        /// </summary>
        void RoomPhoto(string scene, string path)
        {
            var region = TerrainSystem.Find(scene);
            var main = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (region == null || main == null) return;
            const float c = CellGrid.CellSize;
            var min = (Vector2)region.CellOffset * c;
            var size = new Vector2(region.Grid.Width, region.Grid.Height) * c;
            var go = new GameObject("Map Fix Photo");
            var rt = new RenderTexture(2048, Mathf.Clamp(Mathf.RoundToInt(2048 * size.y / size.x), 64, 4096), 24);
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.CopyFrom(main);
                cam.enabled = false;
                cam.targetTexture = rt;
                cam.aspect = size.x / size.y;
                float dist = 40f;
                cam.fieldOfView = 2f * Mathf.Atan(size.y / 2f / dist) * Mathf.Rad2Deg;
                cam.transform.position = new Vector3(min.x + size.x / 2f, min.y + size.y / 2f, -dist);
                cam.transform.rotation = Quaternion.identity;
                cam.farClipPlane = 1000f;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply(false);
                RenderTexture.active = prev;
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.Destroy(tex);
                Plugin.Log.LogInfo($"[AutoTest] 地图修复 {scene} 照片 {rt.width}x{rt.height}（{size.x:F0}x{size.y:F0} 单位）→ {path}");
            }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[AutoTest] 地图修复 {scene} 照片失败：{e.Message}"); }
            finally
            {
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(go);
            }
        }

        /// <summary>The full map (the whole world shown) at Deepnest's and Queen's Gardens' rooms, close and far.</summary>
        IEnumerator MapViews17()
        {
            UI.WorldMap.RevealWorld();
            UI.WorldMap.DevSetOpen(true);
            UI.WorldMap.DevPickLayer(1);
            foreach (var scene in mapFixViews)
                foreach (float z in new[] { 1.4f, 3.5f })
                {
                    UI.WorldMap.DevCentreScene(scene);
                    UI.WorldMap.DevZoom(z);
                    yield return new WaitForSecondsRealtime(0.35f);
                    Shot("Town", $"mapfix_view_{scene}_z{z:0.#}");
                    yield return null;
                }
            UI.WorldMap.DevSetOpen(false);
        }
    }
}
