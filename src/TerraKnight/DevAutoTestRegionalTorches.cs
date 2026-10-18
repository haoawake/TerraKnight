using System.Collections;
using System.Collections.Generic;
using System.IO;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "rtorch", or "rtorch_table" / "rtorch_place" / "rtorch_break"): Hallownest's regional torches.
    /// table: every surveyed scene's torch (DevAutoTestGates' scenes; those the names don't place are listed), each
    /// torch's own scene giving it back, the 48 items (pictures, placing torches, in the creative list), the pot roll's
    /// odds and its Geo. place: styled torches set down in a dug cave through the item's use, each drawn from its own row
    /// with its flame layer's seven copies flickering over it and its own light, a plain one as before; one mined giving
    /// its own item back; through a save written and read back and the scene entered again; styles from the network.
    /// break: the scene's breakables broken in several areas giving that area's torch and Geo, nothing from a copy the
    /// mod owns, nothing again from one broken before once the room is entered again.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        int rtChecks, rtFails;

        void RtCheck(string label, bool pass, string detail = "")
        {
            rtChecks++;
            if (!pass) rtFails++;
            string line = $"[AutoTest] 地区火把 {label}：{detail} {(pass ? "通过" : "不通过")}";
            if (pass) Plugin.Log.LogInfo(line);
            else Plugin.Log.LogError(line);
        }

        IEnumerator RegionalTorchTest(string which)
        {
            bool all = which == "all";
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            bool wasInfinite = mod.Inventory.Infinite;
            mod.SetInfiniteItems(false);
            try
            {
                if (all || which == "table") RtTable();
                if (all || which == "place") yield return RtPlace();
                if (all || which == "break") yield return RtBreak();
                if (which == "scan") yield return RtScan();
            }
            finally
            {
                BreakableDrops.DevRoll = null;
                Player.TerraCamera.DevLook = null;
                mod.SetInfiniteItems(wasInfinite);
                Player.TerraStats.DevImmortal = false;
                mod.DevNoFallDamage = false;
                if (PlayerData.instance != null) PlayerData.instance.isInvincible = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 地区火把汇总 {rtChecks - rtFails}/{rtChecks} {(rtFails == 0 ? "通过" : "不通过")}");
        }

        void RtInvincible()
        {
            Player.TerraStats.DevImmortal = true;
            if (PlayerData.instance != null) PlayerData.instance.isInvincible = true;
        }

        // --- The table, the items, the odds. ---

        void RtTable()
        {
            var unmapped = new List<string>();
            var perStyle = new int[RegionalTorches.Count + 1];
            foreach (var scene in entryGates.Keys)
            {
                int s = RegionalTorchAreas.ByName(scene);
                if (s == 0) s = RegionalTorchAreas.ByArea(scene);
                if (s == 0) unmapped.Add(scene);
                else perStyle[s]++;
            }
            RtCheck("场景表覆盖调查过的全部场景", unmapped.Count == 0, $"{entryGates.Count} 个场景，没对上 {unmapped.Count} 个{(unmapped.Count > 0 ? "：" + string.Join("、", unmapped) : "")}");
            var counts = new System.Text.StringBuilder();
            var none = new List<string>();
            for (int s = 1; s <= RegionalTorches.Count; s++)
            {
                counts.Append($"{s}.{RegionalTorches.All[s - 1].Area} {perStyle[s]}；");
                if (perStyle[s] == 0) none.Add(RegionalTorches.All[s - 1].Area);
            }
            Plugin.Log.LogInfo($"[AutoTest] 地区火把 各区场景数：{counts}");
            Plugin.Log.LogInfo($"[AutoTest] 地区火把 调查场景里没有的区（只在别的场景或运行时出现）：{(none.Count == 0 ? "无" : string.Join("、", none))}");
            var wrong = new List<string>();
            for (int s = 1; s <= RegionalTorches.Count; s++)
            {
                var t = RegionalTorches.All[s - 1];
                int expect = s == RegionalTorchAreas.InfectedCrossroads ? RegionalTorchAreas.Crossroads : s;
                int got = RegionalTorchAreas.ByName(t.Scene);
                if (got != expect) wrong.Add($"{t.Area}（{t.Scene}→{got}）");
            }
            RtCheck("每支火把的代表场景对回本区", wrong.Count == 0, wrong.Count == 0 ? "48 支" : string.Join("、", wrong));

            // The items.
            var bad = new List<string>();
            var everything = new HashSet<ItemDef>(Shop.Everything);
            for (int s = 1; s <= RegionalTorches.Count; s++)
            {
                var def = ItemDb.Get(RegionalTorches.ItemOf(s));
                var tex = Terraria.TerrariaAssets.Texture(def.Texture);
                string zh = def.ZhName ?? def.Name;
                bool ok = def.Places == CellKind.Torch && zh == RegionalTorches.All[s - 1].Name && RegionalTorches.StyleOf(def.Type) == s
                    && tex != null && tex.width == 14 && tex.height == 16 && everything.Contains(def) && def.MaxStack == 9999 && def.Value == 50;
                if (!ok) bad.Add($"{s}.{zh}（放置 {def.Places}，贴图 {(tex != null ? tex.width + "x" + tex.height : "无")}，创造列表 {everything.Contains(def)}，堆叠 {def.MaxStack}）");
            }
            RtCheck("48 种火把物品（名字、图标 14x16、放火把、创造列表）", bad.Count == 0, bad.Count == 0 ? $"{RegionalTorches.Count} 种" : string.Join("；", bad));
            var tiles = Terraria.TerrariaAssets.Texture(RegionalTorches.TileSheet);
            var flames = Terraria.TerrariaAssets.Texture(RegionalTorches.FlameSheet);
            RtCheck("放置图集与火焰图集", tiles.width == 132 && tiles.height == 1056 && flames.width == 132 && flames.height == 1056, $"{tiles.width}x{tiles.height} / {flames.width}x{flames.height}");
            RtCheck("普通火把不变", RegionalTorches.StyleOf(ItemType.Torch) == 0 && RegionalTorches.ItemOf(0) == ItemType.Torch && ItemDb.Get(ItemType.Torch).Texture == "Item_8", "");

            // The pot's odds (the save's difficulty) and its Geo by stage.
            const int n = 20000;
            int many = 0, few = 0, torchSum = 0;
            for (int k = 0; k < n; k++)
            {
                var (t, _) = BreakableDrops.Roll(5, 0, 30);
                if (t > 0) { many++; torchSum += t; }
                if (BreakableDrops.Roll(5, 0, 0).Torches > 0) few++;
            }
            float pMany = many / (float)n, pFew = few / (float)n;
            RtCheck("掉火把的几率（带够 20 支 1/7，不够 2/7）", Mathf.Abs(pMany - 1f / 7f) < 0.02f && Mathf.Abs(pFew - 2f / 7f) < 0.02f,
                $"难度 {GameDifficulty.Current}：{pMany:P1} / {pFew:P1}，每次 {torchSum / (float)Mathf.Max(1, many):F1} 支");
            var geo = new System.Text.StringBuilder();
            bool geoOk = true;
            foreach (int level in new[] { 0, 2, 3, 5 })
            {
                long sum = 0;
                int min = int.MaxValue, max = 0;
                for (int k = 0; k < n; k++)
                {
                    int g = BreakableDrops.Geo(level);
                    sum += g;
                    min = Mathf.Min(min, g);
                    max = Mathf.Max(max, g);
                }
                geo.Append($"阶段{level} 平均 {sum / (float)n:F2}（{min}~{max}）；");
                geoOk &= sum > 0;
            }
            RtCheck("罐子的钱（吉欧）", geoOk, $"难度 {GameDifficulty.Current}，已解锁阶段 {Shop.UnlockedStage()}：{geo}");
        }

        // --- Placing, drawing, mining, the save, the network. ---

        IEnumerator RtPlace()
        {
            const string scene = "Crossroads_01";
            if (GameManager.instance.sceneName != scene)
            {
                yield return GoTo(scene, EntryGateOf(scene));
                yield return new WaitForSeconds(1f);
            }
            RtInvincible();
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            // Away from the gate he came in by.
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.right * 12f);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1f);
            // A stone shelf in the room's open air over the hero (the first rows clear over its whole width), the hero on it.
            var top = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y - 0.1f));
            var home = TerraWorld.RegionAt(top.x, top.y);
            int floor = -1;
            for (int y = top.y + 3; y < top.y + 50 && floor < 0; y++)
            {
                bool clear = true;
                for (int x = top.x - 16; x <= top.x + 14 && clear; x++)
                    for (int dy = -1; dy <= 6 && clear; dy++)
                        clear = TerraWorld.InWorld(x, y + dy) && TerraWorld.Get(x, y + dy) == CellKind.Empty;
                if (clear) floor = y;
            }
            Plugin.Log.LogInfo($"[AutoTest] 地区火把 场地：{(home != null ? home.SceneName + " " + home.CellRect : "无区域")}，脚下 {top}，架子 {floor}");
            if (floor < 0)
            {
                RtCheck("找到放火把的地方", false, "上方没有够宽的空地");
                yield break;
            }
            for (int x = top.x - 16; x <= top.x + 14; x++) TerraWorld.Set(x, floor - 1, CellKind.Stone);
            // A pillar for the wall torches.
            int px = top.x + 11;
            for (int y = floor; y <= floor + 4; y++) TerraWorld.Set(px, y, CellKind.Stone);
            hero.transform.position = Clear(hero, CellGrid.CellCenter(top.x, floor));
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1f);
            RtInvincible();

            IEnumerator UseAt(Vector2Int cell)
            {
                var aim = CellGrid.CellCenter(cell.x, cell.y);
                hero.transform.position = Clear(hero, aim + Vector2.left * 1.5f);
                rb.linearVelocity = Vector2.zero;
                yield return null;
                mod.DevUseSelected(aim, target: true);
                yield return new WaitForSeconds(0.15f);
            }

            // Standing ones along the floor, two on the pillar's sides, a plain one among them.
            var placed = new List<(Vector2Int Cell, int Style, int Fx)>();
            int[] standing = { 1, 4, 5, 20, 43, 48, 0, 36 };
            for (int k = 0; k < standing.Length; k++) placed.Add((new Vector2Int(top.x - 14 + k * 3, floor), standing[k], 0));
            placed.Add((new Vector2Int(px + 1, floor + 3), 13, 22));
            placed.Add((new Vector2Int(px - 1, floor + 3), 41, 44));
            bool smart = mod.SmartCursor;
            mod.DevSetSmartCursor(false);
            foreach (var (cell, style, _) in placed)
            {
                inv.Slots[9] = ItemStack.Of(RegionalTorches.ItemOf(style), 5);
                inv.Selected = 9;
                yield return UseAt(cell);
            }
            inv.Selected = 0;
            var region = TerraWorld.RegionAt(top.x, floor) ?? home;
            Player.TerraCamera.DevLook = CellGrid.CellCenter(top.x, floor + 3);
            hero.transform.position = Clear(hero, CellGrid.CellCenter(top.x, floor));
            yield return new WaitForSeconds(0.8f);
            Shot(scene, "rtorch_1_placed");
            yield return null;

            // Each one: its cell, its picture's row and column, its flame copies, its light.
            var renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            var flameObjs = Object.FindObjectsByType<RegionalTorchFlames.Flame>(FindObjectsSortMode.None);
            var details = new List<string>();
            bool drawnOk = true;
            foreach (var (cell, style, fx) in placed)
            {
                Vector2 at = (Vector2)region.Root.position + CellGrid.CellCenter(cell.x - region.CellOffset.x, cell.y - region.CellOffset.y);
                SpriteRenderer block = null, light = null;
                foreach (var sr in renderers)
                {
                    if (sr == null || !sr.gameObject.activeInHierarchy) continue;
                    Vector2 p = sr.transform.position;
                    if (sr.name == "Terra Cell" && (p - at).sqrMagnitude < 0.01f && sr.sprite != null && sr.sprite.texture != null
                        && (sr.sprite.texture.name.Contains("HkTorchTiles") || sr.sprite.texture.name.Contains("Tiles_4"))) block = sr;
                    if (sr.name == "Terra Light" && (p - (at + Vector2.up * 0.1f)).sqrMagnitude < 0.01f) light = sr;
                }
                RegionalTorchFlames.Flame flame = null;
                foreach (var f in flameObjs) if (f != null && ((Vector2)f.transform.position - at).sqrMagnitude < 0.01f) flame = f;
                int gotStyle = RegionalTorches.StyleAt(cell);
                var rect = block != null ? block.sprite.rect : default;
                var tex = block != null ? block.sprite.texture : null;
                bool rowOk = style > 0
                    ? tex != null && tex.name.Contains("HkTorchTiles") && (int)rect.x == fx && (int)rect.y == tex.height - (style - 1) * 22 - 20
                    : tex != null && tex.name.Contains("Tiles_4") && (int)rect.x == fx && (int)rect.y == tex.height - 20;
                int layers = flame != null ? flame.GetComponentsInChildren<SpriteRenderer>().Length : 0;
                bool flameOk = style > 0 ? layers == RegionalTorchFlames.Layers : flame == null;
                Color want = Glow.Additive
                    ? new Color(RegionalTorches.GlowColor(style).r * RegionalTorches.GlowColor(style).a, RegionalTorches.GlowColor(style).g * RegionalTorches.GlowColor(style).a, RegionalTorches.GlowColor(style).b * RegionalTorches.GlowColor(style).a, 1f)
                    : new Color(RegionalTorches.GlowColor(style).r, RegionalTorches.GlowColor(style).g, RegionalTorches.GlowColor(style).b, RegionalTorches.GlowColor(style).a * 0.45f);
                bool lightOk = light != null && Mathf.Abs(light.color.r - want.r) < 0.01f && Mathf.Abs(light.color.g - want.g) < 0.01f && Mathf.Abs(light.color.b - want.b) < 0.01f;
                bool ok = TerraWorld.Get(cell.x, cell.y) == CellKind.Torch && gotStyle == style && rowOk && flameOk && lightOk;
                drawnOk &= ok;
                details.Add($"{RegionalTorches.NameOf(style)}@{cell}：格 {TerraWorld.Get(cell.x, cell.y)} 样式 {gotStyle} 帧 {rect.x},{rect.y}（{(rowOk ? "对" : "错")}）火焰 {layers} 层 光 {(light != null ? light.color.ToString() : "无")}{(ok ? "" : " ←")}");
            }
            RtCheck("放下的火把各画自己的那一行、火焰层与灯光", drawnOk, string.Join("；", details));

            // The flame flickers: its copies move from tick to tick.
            RegionalTorchFlames.Flame one = null;
            foreach (var f in Object.FindObjectsByType<RegionalTorchFlames.Flame>(FindObjectsSortMode.None)) if (f != null) one = f;
            if (one != null)
            {
                var a = (Vector2[])one.DevOffsets.Clone();
                yield return new WaitForSeconds(0.1f);
                var b = one.DevOffsets;
                int moved = 0;
                bool inRange = true;
                for (int k = 0; k < a.Length; k++)
                {
                    if (a[k] != b[k]) moved++;
                    inRange &= b[k].x >= -1.5f && b[k].x <= 1.5f && b[k].y >= -3.5f && b[k].y <= 0f;
                }
                RtCheck("火焰层跳动（7 层随机偏移，X ±1.5 px，Y 0~-3.5 px）", moved > 0 && inRange, $"0.1 秒内 {moved}/7 层换了位置，现在 {string.Join(" ", b)}");
            }
            else RtCheck("火焰层跳动", false, "没找到火焰");

            // A regional torch in hand.
            inv.Slots[9] = ItemStack.Of(ItemType.TorchFogCanyon, 3);
            inv.Selected = 9;
            yield return new WaitForSeconds(0.5f);
            Shot(scene, "rtorch_2_held");
            yield return null;
            inv.Selected = 0;

            // Mined: its own item back, the plain one's too.
            int ItemCount(ItemType t)
            {
                int c = WorldItems.DevCount(t);
                for (int i = 0; i < Inventory.Total; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == t) c += inv.Slots[i].Count;
                if (!inv.Mouse.IsEmpty && inv.Mouse.Type == t) c += inv.Mouse.Count;
                return c;
            }
            inv.Slots[8] = ItemStack.Of(ItemType.CopperPickaxe);
            foreach (int k in new[] { 3, 6 })
            {
                var (cell, style, _) = placed[k];
                var item = RegionalTorches.ItemOf(style);
                int before = ItemCount(item), plain = ItemCount(ItemType.Torch);
                inv.Selected = 8;
                for (int tries = 0; tries < 6 && TerraWorld.Get(cell.x, cell.y) == CellKind.Torch; tries++) yield return UseAt(cell);
                inv.Selected = 0;
                yield return new WaitForSeconds(0.5f);
                int after = ItemCount(item), plainAfter = ItemCount(ItemType.Torch);
                bool ok = TerraWorld.Get(cell.x, cell.y) == CellKind.Empty && after == before + 1 && (style == 0 || plainAfter == plain);
                RtCheck($"挖掉{RegionalTorches.NameOf(style)}还回同一种", ok, $"格 {TerraWorld.Get(cell.x, cell.y)}，{ItemDb.Get(item).Name} {before}→{after}，普通火把 {plain}→{plainAfter}");
            }
            // The plain torch set where a regional one stood is plain.
            {
                var (cell, _, _) = placed[3];
                inv.Slots[9] = ItemStack.Of(ItemType.Torch, 3);
                inv.Selected = 9;
                yield return UseAt(cell);
                inv.Selected = 0;
                yield return new WaitForSeconds(0.3f);
                RtCheck("原地改放普通火把就是普通火把", TerraWorld.Get(cell.x, cell.y) == CellKind.Torch && RegionalTorches.StyleAt(cell) == 0, $"样式 {RegionalTorches.StyleAt(cell)}");
                placed[3] = (cell, 0, 0);
            }

            // The rebuilt grid (a scene loaded again) keeps the styles.
            string sceneName = region.SceneName;
            var replay = new CellGrid(region.Grid.Width, region.Grid.Height);
            TerrainRegion.ReplayEdits(replay, sceneName);
            var lost = new List<string>();
            foreach (var (cell, style, _) in placed)
            {
                if (TerraWorld.Get(cell.x, cell.y) != CellKind.Torch) continue;
                int i = (cell.y - region.CellOffset.y) * region.Grid.Width + cell.x - region.CellOffset.x;
                if (replay.TorchStyleAt(i) != style || replay.GetAt(i) != CellKind.Torch) lost.Add($"{cell} {style}→{replay.TorchStyleAt(i)}");
            }
            RtCheck("场景重建保留样式", lost.Count == 0, lost.Count == 0 ? $"{RegionalTorches.For(sceneName).Count} 支有样式" : string.Join("、", lost));

            // Through the save file and back.
            int saveSlot = Save.ModSave.DevStateSlot;
            int styledBefore = RegionalTorches.For(sceneName).Count;
            Save.ModSave.Write(saveSlot, mod);
            string path = Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{saveSlot}.json");
            var disk = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
            var onDisk = disk["TorchStyles"]?[sceneName] as Newtonsoft.Json.Linq.JArray;
            RtCheck("存档文件写进火把样式", onDisk != null && onDisk.Count == styledBefore, $"{sceneName}：内存 {styledBefore}，文件 {(onDisk != null ? onDisk.Count : -1)}");
            Save.ModSave.Read(saveSlot, mod);
            int styledRead = RegionalTorches.For(sceneName).Count;
            RtCheck("读档读回火把样式", styledRead == styledBefore, $"{styledRead}/{styledBefore}");
            inv = mod.Inventory;
            mod.SetInfiniteItems(false);
            const string away = "Crossroads_02";
            yield return GoTo(away, EntryGateOf(away));
            RtInvincible();
            yield return GoTo(scene, EntryGateOf(scene));
            RtInvincible();
            hero = HeroController.instance;
            yield return new WaitForSeconds(1f);
            region = TerraWorld.RegionAt(top.x, floor);
            var back = new List<string>();
            int flamesBack = 0;
            foreach (var f in Object.FindObjectsByType<RegionalTorchFlames.Flame>(FindObjectsSortMode.None)) if (f != null && f.gameObject.activeInHierarchy) flamesBack++;
            int expectFlames = 0;
            foreach (var (cell, style, _) in placed)
            {
                if (TerraWorld.Get(cell.x, cell.y) != CellKind.Torch) continue;
                if (style > 0) expectFlames++;
                if (RegionalTorches.StyleAt(cell) != style) back.Add($"{cell} {style}→{RegionalTorches.StyleAt(cell)}");
            }
            RtCheck("出去再回来火把样式和火焰都在", back.Count == 0 && flamesBack >= expectFlames, $"{(back.Count == 0 ? "样式都对" : string.Join("、", back))}，火焰 {flamesBack}/{expectFlames}");
            Player.TerraCamera.DevLook = CellGrid.CellCenter(top.x, floor + 3);
            hero.transform.position = Clear(hero, CellGrid.CellCenter(top.x, floor));
            yield return new WaitForSeconds(0.8f);
            Shot(scene, "rtorch_3_reloaded");
            yield return null;
            Player.TerraCamera.DevLook = null;

            // From the network: a torch placed by another player with its style, then a world record's.
            {
                var cell = new Vector2Int(top.x - 16, floor);
                int i = (cell.y - region.CellOffset.y) * region.Grid.Width + cell.x - region.CellOffset.x;
                using (var ms = new MemoryStream())
                {
                    var w = new BinaryWriter(ms);
                    w.Write(sceneName);
                    w.Write((ushort)1);
                    w.Write(i);
                    w.Write(WorldEdits.Pack(CellKind.Torch, region.Grid.IsDugAt(i)));
                    w.Write(region.Grid.WallAt(i));
                    w.Write((ushort)1);
                    w.Write(i);
                    w.Write((byte)RegionalTorchAreas.Hive);
                    w.Flush();
                    ms.Position = 0;
                    Net.TileSync.Receive(new BinaryReader(ms));
                }
                bool tile = TerraWorld.Get(cell.x, cell.y) == CellKind.Torch && RegionalTorches.StyleAt(cell) == RegionalTorchAreas.Hive
                    && RegionalTorches.For(sceneName).TryGetValue(i, out byte kept) && kept == RegionalTorchAreas.Hive;
                string key = "r:" + sceneName + ":" + i / 512;
                RegionalTorches.NetApply(key, sceneName, new[] { ((long)i << 8) | RegionalTorchAreas.Dream });
                bool record = RegionalTorches.StyleAt(cell) == RegionalTorchAreas.Dream;
                RegionalTorches.NetRemove(key);
                bool removed = RegionalTorches.StyleAt(cell) == 0;
                RtCheck("联机：放火把带样式、世界记录、记录删除", tile && record && removed, $"图格消息 {tile}，记录 {record}，删除 {removed}");
                TerraWorld.Set(cell.x, cell.y, CellKind.Empty);
            }
            mod.DevSetSmartCursor(smart);
        }

        // --- Each torch's own scene: its torch at run time (the scene's map zone too), its breakables, one the game keeps broken. ---

        IEnumerator RtScan()
        {
            var seen = new HashSet<string>();
            int persistentTested = 0, visited = 0;
            var mismatch = new List<string>();
            foreach (var t in RegionalTorches.All)
            {
                string scene = t.Scene;
                if (!seen.Add(scene) || !entryGates.ContainsKey(scene)) continue;
                yield return GoTo(scene, EntryGateOf(scene));
                RtInvincible();
                if (GameManager.instance.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] 地区火把 扫描 进不去 {scene}"); continue; }
                visited++;
                var sm = GameManager.instance.sm;
                int runtime = RegionalTorchAreas.StyleOf(scene), byName = RegionalTorchAreas.ByName(scene);
                if (runtime != byName && !(byName == RegionalTorchAreas.Crossroads && runtime == RegionalTorchAreas.InfectedCrossroads)) mismatch.Add($"{scene}（名字 {byName}，运行时 {runtime}）");
                var list = new List<Breakable>();
                foreach (var b in Object.FindObjectsByType<Breakable>(FindObjectsSortMode.None))
                    if (b != null && b.isActiveAndEnabled && b.gameObject.scene.name == scene) list.Add(b);
                var kept = list.FindAll(b => b.GetComponent<PersistentBoolItem>() != null);
                Plugin.Log.LogInfo($"[AutoTest] 地区火把 扫描 {scene}：地图区 {(sm != null ? sm.mapZone.ToString() : "?")}，火把 {RegionalTorches.NameOf(runtime)}（{runtime}），Breakable {list.Count} 个（持久 {kept.Count}{(kept.Count > 0 ? "：" + string.Join("、", kept.ConvertAll(b => b.name)) : "")}），BreakableObject {Object.FindObjectsByType<BreakableObject>(FindObjectsSortMode.None).Length}，杆 {Object.FindObjectsByType<BreakablePole>(FindObjectsSortMode.None).Length + Object.FindObjectsByType<BreakablePoleSimple>(FindObjectsSortMode.None).Length}");
                var whole = kept.Find(b => !BreakableDrops.DevIsBroken(b) && b.GetComponent<Collider2D>() != null && b.GetComponent<Collider2D>().enabled && !BreakableDrops.Looted(b.gameObject));
                if (persistentTested < 2 && whole != null)
                {
                    var hero = HeroController.instance;
                    hero.transform.position = Clear(hero, (Vector2)whole.transform.position + Vector2.left * 1.5f);
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.3f);
                    int dropped = BreakableDrops.DevDropped;
                    BreakableDrops.DevRoll = 3;
                    HitTaker.Hit(whole.gameObject, new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Nail, DamageDealt = 21, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 1f });
                    BreakableDrops.DevRoll = null;
                    yield return new WaitForSeconds(0.4f);
                    string key = BreakableDrops.KeyOf(whole.gameObject), wholeName = whole.name;
                    bool gave = BreakableDrops.DevDropped == dropped + 1;
                    string away = scene == "Town" ? "Crossroads_01" : "Town";
                    yield return GoTo(away, EntryGateOf(away));
                    yield return GoTo(scene, EntryGateOf(scene));
                    RtInvincible();
                    Breakable again = null;
                    foreach (var b in Object.FindObjectsByType<Breakable>(FindObjectsSortMode.None)) if (b != null && BreakableDrops.KeyOf(b.gameObject) == key) again = b;
                    int before = BreakableDrops.DevDropped;
                    if (again != null && !BreakableDrops.DevIsBroken(again))
                        HitTaker.Hit(again.gameObject, new HitInstance { Source = HeroController.instance.gameObject, AttackType = AttackTypes.Nail, DamageDealt = 21, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 1f });
                    yield return new WaitForSeconds(0.3f);
                    RtCheck($"{scene} 持久的 {wholeName} 打碎掉一次，回来还碎着、不再掉", gave && (again == null || BreakableDrops.DevIsBroken(again)) && BreakableDrops.DevDropped == before,
                        $"第一次掉 {gave}，回来 {(again == null ? "找不到" : "已碎 " + BreakableDrops.DevIsBroken(again))}，再打掉落 {before}→{BreakableDrops.DevDropped}");
                    persistentTested++;
                }
            }
            RtCheck("运行时（含地图区）与按名字给的火把一致", mismatch.Count == 0, $"走过 {visited} 个场景{(mismatch.Count > 0 ? "，不一致：" + string.Join("、", mismatch) : "")}");
            if (persistentTested == 0) Plugin.Log.LogInfo("[AutoTest] 地区火把 扫描：走过的场景里没有持久的 Breakable");
        }

        // --- The breakables. ---

        IEnumerator RtBreak()
        {
            string[] scenes = { "Crossroads_01", "Crossroads_04", "Fungus1_01", "Fungus1_02", "Fungus2_03", "Ruins1_01", "Ruins1_03", "Waterways_01", "Mines_01", "Deepnest_01b", "RestingGrounds_02", "Town" };
            var areas = new HashSet<int>();
            bool poleDone = false;
            var broken = new List<(string Scene, string Key, bool Persistent)>();
            foreach (var scene in scenes)
            {
                if (areas.Count >= 4) break;
                if (GameManager.instance.sceneName != scene) yield return GoTo(scene, EntryGateOf(scene));
                RtInvincible();
                yield return new WaitForSeconds(0.5f);
                if (GameManager.instance.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] 地区火把 没进得去 {scene}"); continue; }
                var hero = HeroController.instance;
                var rb = hero.GetComponent<Rigidbody2D>();
                var sm = GameManager.instance.sm;
                int style = RegionalTorchAreas.StyleOf(scene);
                var list = new List<Breakable>();
                foreach (var b in Object.FindObjectsByType<Breakable>(FindObjectsSortMode.None))
                {
                    var c = b != null ? b.GetComponent<Collider2D>() : null;
                    if (b == null || !b.isActiveAndEnabled || c == null || !c.enabled || BreakableDrops.DevIsBroken(b) || BreakableDrops.Looted(b.gameObject)) continue;
                    if (b.gameObject.scene.name != scene || Mathf.Abs(b.transform.position.z) > 1f) continue;
                    list.Add(b);
                }
                int objects = Object.FindObjectsByType<BreakableObject>(FindObjectsSortMode.None).Length, poles = Object.FindObjectsByType<BreakablePole>(FindObjectsSortMode.None).Length
                    + Object.FindObjectsByType<BreakablePoleSimple>(FindObjectsSortMode.None).Length;
                Plugin.Log.LogInfo($"[AutoTest] 地区火把 {scene}（地图区 {(sm != null ? sm.mapZone.ToString() : "?")}，火把 {RegionalTorches.NameOf(style)}）：Breakable {list.Count} 个，BreakableObject {objects} 个，杆 {poles} 个");
                if (list.Count == 0) continue;
                list.Sort((a, b) => ((Vector2)a.transform.position - (Vector2)hero.transform.position).sqrMagnitude.CompareTo(((Vector2)b.transform.position - (Vector2)hero.transform.position).sqrMagnitude));
                // One the game keeps broken (PersistentBoolItem) second, where the room has one.
                int kept = list.FindIndex(x => x.GetComponent<PersistentBoolItem>() != null);
                Plugin.Log.LogInfo($"[AutoTest] 地区火把 {scene} 持久的 Breakable {list.FindAll(x => x.GetComponent<PersistentBoolItem>() != null).Count} 个");
                if (kept > 1) { var keptOne = list[kept]; list.RemoveAt(kept); list.Insert(1, keptOne); }
                bool gave = false;
                for (int k = 0; k < list.Count && k < 2; k++)
                {
                    var b = list[k];
                    bool persistent = b.GetComponent<PersistentBoolItem>() != null;
                    hero.transform.position = Clear(hero, (Vector2)b.transform.position + Vector2.left * 1.5f);
                    rb.linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.3f);
                    int dropped = BreakableDrops.DevDropped, logged = BreakableDrops.DevLog.Count;
                    BreakableDrops.DevRoll = k == 0 ? 1 : 3;
                    HitTaker.Hit(b.gameObject, new HitInstance
                    {
                        Source = hero.gameObject, AttackType = AttackTypes.Nail, DamageDealt = 21, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 1f,
                    });
                    BreakableDrops.DevRoll = null;
                    yield return new WaitForSeconds(0.4f);
                    var last = BreakableDrops.DevLog.Count > logged ? BreakableDrops.DevLog[BreakableDrops.DevLog.Count - 1] : default;
                    bool ok = BreakableDrops.DevDropped == dropped + 1 && last.Style == style
                        && (k == 0 ? last.Torches > 0 && last.Torch == RegionalTorches.ItemOf(style) : last.Geo > 0 || GameDifficulty.Current < DifficultyLevel.Expert);
                    RtCheck($"{scene} 打碎 {b.name}（{(persistent ? "持久" : "不持久")}）掉{(k == 0 ? "本区火把" : "吉欧")}", ok,
                        $"碎了 {BreakableDrops.DevIsBroken(b)}，{BreakableDrops.DevLast}");
                    if (k == 0) { Shot(scene, "rtorch_break_" + scene); yield return null; }
                    if (ok) gave = true;
                    broken.Add((scene, BreakableDrops.KeyOf(b.gameObject), persistent));
                }
                if (gave) areas.Add(style);

                // A pole of the scene's: BreakablePole on a side hit, BreakablePoleSimple on the nail's slash.
                if (!poleDone)
                {
                    BreakablePole pole = null;
                    foreach (var x in Object.FindObjectsByType<BreakablePole>(FindObjectsSortMode.None))
                        if (x != null && x.enabled && x.gameObject.scene.name == scene) { pole = x; break; }
                    BreakablePoleSimple simple = null;
                    if (pole == null)
                        foreach (var x in Object.FindObjectsByType<BreakablePoleSimple>(FindObjectsSortMode.None))
                            if (x != null && x.enabled && x.gameObject.scene.name == scene && x.GetComponent<Collider2D>() != null && x.GetComponent<Collider2D>().enabled) { simple = x; break; }
                    var target = pole != null ? pole.gameObject : simple != null ? simple.gameObject : null;
                    if (target != null)
                    {
                        hero.transform.position = Clear(hero, (Vector2)target.transform.position + Vector2.left * 1.5f);
                        rb.linearVelocity = Vector2.zero;
                        yield return new WaitForSeconds(0.3f);
                        int dropped = BreakableDrops.DevDropped;
                        BreakableDrops.DevRoll = 1;
                        if (pole != null)
                            HitTaker.Hit(target, new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Nail, DamageDealt = 21, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 1f });
                        else PropHits.Pulse(target.GetComponent<Collider2D>(), hero.transform.position, 1f, 21);
                        yield return new WaitForSeconds(0.5f);
                        BreakableDrops.DevRoll = null;
                        var last = BreakableDrops.DevLog.Count > 0 ? BreakableDrops.DevLog[BreakableDrops.DevLog.Count - 1] : default;
                        RtCheck($"{scene} 打断{(pole != null ? "杆子" : "细杆")} {target.name} 掉本区火把", BreakableDrops.DevDropped == dropped + 1 && last.Style == style && last.Torches > 0, BreakableDrops.DevLast);
                        poleDone = true;
                    }
                }

                // A copy the mod owns gives nothing.
                if (areas.Count == 1 && list.Count > 2)
                {
                    var holder = new GameObject("TK test holder");
                    holder.AddComponent<TerraOwned>();
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder, list[2].gameObject.scene);
                    var copy = Object.Instantiate(list[2].gameObject, list[2].transform.position + Vector3.right * 0.7f, list[2].transform.rotation, holder.transform);
                    yield return null;
                    yield return null;
                    var cb = copy.GetComponent<Breakable>();
                    int dropped = BreakableDrops.DevDropped, skipped = BreakableDrops.DevSkipped;
                    if (cb != null)
                    {
                        BreakableDrops.DevRoll = 1;
                        HitTaker.Hit(copy, new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Nail, DamageDealt = 21, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 1f });
                        BreakableDrops.DevRoll = null;
                    }
                    yield return new WaitForSeconds(0.3f);
                    RtCheck("mod 自己的物体不掉", cb != null && BreakableDrops.DevDropped == dropped && BreakableDrops.DevSkipped > skipped, $"复制品 {(cb != null ? "碎了 " + BreakableDrops.DevIsBroken(cb) : "没有 Breakable")}");
                    Object.Destroy(holder);
                }
            }
            RtCheck("至少 3 个不同地区打碎可破坏物掉本区火把", areas.Count >= 3, $"{areas.Count} 个区：{string.Join("、", new List<int>(areas).ConvertAll(s => RegionalTorches.NameOf(s)))}");

            // Into each room again: nothing given a second time, the kept ones still broken.
            var rooms = new List<string>();
            foreach (var b in broken) if (!rooms.Contains(b.Scene)) rooms.Add(b.Scene);
            foreach (var scene in rooms)
            {
                string away = scene == "Town" ? "Crossroads_01" : "Town";
                yield return GoTo(away, EntryGateOf(away));
                yield return GoTo(scene, EntryGateOf(scene));
                RtInvincible();
                yield return new WaitForSeconds(0.8f);
                var hero = HeroController.instance;
                var rb = hero.GetComponent<Rigidbody2D>();
                foreach (var (s, key, persistent) in broken)
                {
                    if (s != scene) continue;
                    Breakable b = null;
                    foreach (var x in Object.FindObjectsByType<Breakable>(FindObjectsSortMode.None)) if (x != null && BreakableDrops.KeyOf(x.gameObject) == key) b = x;
                    if (b == null)
                    {
                        RtCheck($"{scene} 回来后 {key}", persistent, "找不到它（持久的碎了就没了也算）");
                        continue;
                    }
                    bool stillBroken = BreakableDrops.DevIsBroken(b);
                    hero.transform.position = Clear(hero, (Vector2)b.transform.position + Vector2.left * 1.5f);
                    rb.linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.3f);
                    int dropped = BreakableDrops.DevDropped;
                    BreakableDrops.DevRoll = 1;
                    HitTaker.Hit(b.gameObject, new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Nail, DamageDealt = 21, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 1f });
                    BreakableDrops.DevRoll = null;
                    yield return new WaitForSeconds(0.3f);
                    bool ok = BreakableDrops.DevDropped == dropped && (!persistent || stillBroken);
                    RtCheck($"{scene} 回来后 {b.name}（{(persistent ? "持久" : "不持久")}）不再掉", ok, $"回来时已碎 {stillBroken}，再打后掉落次数 {dropped}→{BreakableDrops.DevDropped}，{BreakableDrops.DevLast}");
                }
            }

            // The breakables given are kept in the save.
            int saveSlot = Save.ModSave.DevStateSlot;
            Save.ModSave.Write(saveSlot, mod);
            var disk = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{saveSlot}.json")));
            var looted = disk["LootedBreakables"] as Newtonsoft.Json.Linq.JArray;
            RtCheck("掉过的可破坏物记在存档里", looted != null && looted.Count == BreakableDrops.LootedCount && looted.Count >= broken.Count, $"文件 {(looted != null ? looted.Count : -1)}，内存 {BreakableDrops.LootedCount}");
        }
    }
}
