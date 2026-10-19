using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "p4_Cchest", "p4_Ctorch"): recorder C's area chests and area torches. A new save's chest of an area
    /// walked up to and opened (its lid, its contents), its loot taken; one made from 30 of the area's blocks at a Work
    /// Bench and set down. Area torches of eight looks set along a dark room's floor with the smart cursor; a row of pots
    /// and lamps broken for the area's torches and Geo; a geo rock mined for its Geo.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator P4_Cchest(HeroController hero)
        {
            yield return P4Kit();
            yield return C4Chests();
        }

        IEnumerator C4Chests()
        {
            mod.TrySwitchLoadout(1);
            yield return null;
            // A plain game's bag (no creative title, the recipes what the bag can make), its kit kept.
            var inv = mod.Inventory;
            var kept = (ItemStack[])inv.Slots.Clone();
            bool allInf = TerraMod.DevAllInfinite;
            TerraMod.DevAllInfinite = false;
            mod.ApplyCreative(false);
            for (int i = 0; i < kept.Length; i++) inv.Slots[i] = kept[i];
            Plugin.Log.LogInfo("[AutoTest] 录制 储物箱计划：" + RegionalChests.DevDescribe());
            yield return C4ChestClip("chest_greenpath", "苍绿之径", true);
            yield return C4ChestClip("chest_crystalpeak", "水晶山峰", false);
            yield return C4ChestClip("chest_city", "泪水之城", false);
            yield return C4ChestClip("chest_deepnest", "深巢", false);
            yield return C4ChestClip("chest_queensgardens", "王后花园", false);
            TerraMod.DevAllInfinite = allInf;
            mod.ApplyCreative(true);
            for (int i = 0; i < kept.Length; i++) inv.Slots[i] = kept[i];
        }

        /// <summary>Recorder C's clips keep the hero alive (no Terraria fall damage on the plain-bag takes).</summary>
        void C4Safe()
        {
            mod.DevNoFallDamage = true;
            Player.TerraStats.DevImmortal = true;
            if (PlayerData.instance != null) PlayerData.instance.isInvincible = true;
        }

        /// <summary>The house again, the fishing again, the chests and torches again, in one run.</summary>
        /// <summary>The last fixes: the Blue Lake's cast, the chests with the lid in sight, the pots.</summary>
        IEnumerator P4_Cbatch5(HeroController hero)
        {
            C4Safe();
            yield return P4Kit();
            mod.TrySwitchLoadout(1);
            yield return null;
            var inv = mod.Inventory;
            var kept = (ItemStack[])inv.Slots.Clone();
            mod.ApplyCreative(false);
            inv.Infinite = false;
            for (int i = 0; i < kept.Length; i++) inv.Slots[i] = kept[i];
            inv.Slots[9] = ItemStack.Of(ItemType.GoldenFishingRod, 1);
            inv.Slots[Inventory.Size] = ItemStack.Of(ItemType.MasterBait, 99);
            yield return C4FishAt("fish_bluelake", "Crossroads_50", false, ItemType.None, 4.5f);
            mod.ApplyCreative(true);
            for (int i = 0; i < kept.Length; i++) inv.Slots[i] = kept[i];
            yield return C4Chests();
            mod.TrySwitchLoadout(1);
            mod.Inventory.Infinite = false;
            yield return C4Pots("torch_pots", "Ruins1_28");
        }

        IEnumerator P4_Cbatch4(HeroController hero)
        {
            C4Safe();
            yield return P4Kit();
            yield return C4BuildAll();
            yield return C4Fishing();
            yield return C4Chests();
            yield return P4_Ctorch(HeroController.instance);
        }

        /// <summary>The digging clips, the chests, the torches and the shores' pictures, in one run.</summary>
        IEnumerator P4_Cbatch3(HeroController hero)
        {
            yield return P4Kit();
            yield return C4DigClips();
            yield return P4_Cchest(HeroController.instance);
            yield return P4_Ctorch(HeroController.instance);
            yield return P4_Cscout3(HeroController.instance);
        }

        IEnumerator P4_Ctorch(HeroController hero)
        {
            yield return P4Kit();
            mod.TrySwitchLoadout(1);
            mod.Inventory.Infinite = false;
            yield return C4TorchTunnel("torch_tunnel", new[] { "Deepnest_16", "Deepnest_14", "Deepnest_39", "Deepnest_34", "Deepnest_41", "Deepnest_01b" });
            yield return C4Pots("torch_pots", "Ruins1_28");
            yield return C4GeoRock("geo_rock", "Crossroads_07");
        }

        /// <summary>The scene of an area's chest in this save's plan: a weapon's or an accessory's first.</summary>
        string C4ChestScene(string area)
        {
            string best = null;
            int bestPart = 99;
            foreach (var tok in RegionalChests.DevDescribe().Split(' '))
            {
                int colon = tok.IndexOf(':'), hash = tok.IndexOf('#');
                if (colon <= 0 || hash <= colon) continue;
                if (tok.Substring(0, colon) != area) continue;
                string scene = tok.Substring(colon + 1, hash - colon - 1);
                if (!int.TryParse(tok.Substring(hash + 1).TrimEnd('✓'), out int part)) continue;
                if (!entryGates.ContainsKey(scene)) continue;
                int rank = part <= 1 ? part : 2;
                if (rank < bestPart) { bestPart = rank; best = scene; }
            }
            return best;
        }

        /// <summary>Waits a moment for a chest of the scene to be set down (RegionalChests.Tick) and returns its box.</summary>
        IEnumerator C4FindChest(string scene, System.Action<Rect?> done)
        {
            Rect? found = null;
            for (float t = 0f; t < 4f && found == null; t += Time.deltaTime)
            {
                foreach (var b in Chests.Boxes())
                {
                    var cell = CellGrid.WorldToCell(b.center);
                    var r = TerraWorld.RegionAt(cell.x, cell.y);
                    if (r != null && r.SceneName == scene) { found = b; break; }
                }
                if (found == null) yield return null;
            }
            done(found);
        }

        /// <summary>A standing cell on the chest's floor some way off it, the floor carrying on to it.</summary>
        static bool C4ApproachTo(Rect box, out Vector2Int stand, out int face)
        {
            stand = default; face = 1;
            var at = CellGrid.WorldToCell(new Vector2(box.center.x, box.yMin + 0.1f));
            foreach (int side in new[] { -1, 1 })
            {
                int edge = side < 0 ? CellGrid.WorldToCell(new Vector2(box.xMin - 0.1f, box.yMin + 0.1f)).x : CellGrid.WorldToCell(new Vector2(box.xMax + 0.1f, box.yMin + 0.1f)).x;
                int ok = 0;
                for (int i = 0; i < 14; i++)
                {
                    int x = edge + side * i;
                    if (!C4Stand(x, at.y)) break;
                    ok = i;
                }
                if (ok >= 6) { stand = new Vector2Int(edge + side * Mathf.Min(ok, 10), at.y); face = -side; return true; }
            }
            return false;
        }

        IEnumerator C4ChestClip(string clip, string area, bool craftAfter)
        {
            string scene = C4ChestScene(area);
            if (scene == null) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{area} 计划里没有能去的箱子场景"); yield break; }
            yield return EnterRoom(scene);
            float entered = Time.time;
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            if (GameManager.instance.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：没进 {scene}"); yield break; }
            QuietAround(hero.transform.position, 400f);
            Rect? boxOr = null;
            yield return C4FindChest(scene, b => boxOr = b);
            if (boxOr == null) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} 没有箱子（{Chests.DevReport()}）"); yield break; }
            var box = boxOr.Value;
            if (!C4ApproachTo(box, out var stand, out int face)) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} 箱子 {box.center:F1} 旁边没有平地"); yield break; }
            C4PutOn(hero, stand);
            C4Face(hero, face);
            var col = hero.GetComponent<BoxCollider2D>();
            QuietAround(hero.transform.position, 400f);
            while (Time.time - entered < 10f) yield return null;
            aimInit = false;
            SmoothAim(box.center + new Vector2(-face * 2.5f, 1.2f), 99f);
            int items0 = Chests.Save().Where(c => c.Scene == scene).Sum(c => c.Items.Count(s => !s.IsEmpty));
            StartRec(clip);
            yield return new WaitForSeconds(0.3f);
            // Stopped a step short of it, so the chest (its lid opening) stays in sight beside the hero.
            float stopX = face > 0 ? box.xMin - 1.45f : box.xMax + 1.45f;
            yield return C4WalkTo(hero, stopX, 3f, () => box.center + new Vector2(-face * 0.6f, 0.9f));
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { SmoothAim(box.center, 10f); yield return null; }
            bool opened = Chests.TryRightClick(box.center);
            if (!opened) opened = Chests.DevOpenNear(box.center);
            PointerOn(GuiOf(box.center));
            yield return new WaitForSeconds(1.3f);
            var loot = InventoryGui.DevHitRect("ChestButton", 0);
            var held = Chests.OpenItems != null ? string.Join("，", Chests.OpenItems.Where(s => !s.IsEmpty).Select(s => s.Def.Name + "×" + s.Count)) : "";
            if (loot != null)
            {
                // The Quick Sell button can lie over the start of "Loot All": the click goes where only Loot All is.
                var lr = loot.Value;
                var sell = InventoryGui.DevHitRect("SellButton", 0);
                Vector2 at = lr.center;
                if (sell != null && sell.Value.Overlaps(lr))
                {
                    float free0 = Mathf.Max(lr.xMin, sell.Value.xMax + 4f);
                    at = new Vector2((free0 + lr.xMax) / 2f, lr.center.y);
                    if (free0 >= lr.xMax - 4f) at = new Vector2(lr.center.x, sell.Value.yMax < lr.center.y ? lr.center.y : lr.yMax - 3f);
                }
                yield return Glide(at, 0.7f);
                yield return new WaitForSeconds(0.12f);
                string hit = UI.TerraCursor.DevAt.HasValue ? InventoryGui.DevHitAt(at) : "";
                Chests.LootAll(mod.Inventory);
                mod.PlaySound("Grab", 0.7f);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：全部拿取 {lr}，快捷出售 {sell}，点在 {at}（{hit}）");
            }
            yield return new WaitForSeconds(1.4f);
            Chests.Close(true);
            InventoryGui.Open = false;
            PointerOff();
            AimAt(box.center + new Vector2(0f, 1f));
            yield return new WaitForSeconds(0.5f);
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{area} {scene} 箱子 {box.center:F1}，打开 {opened}，里面 {held}");
            if (craftAfter) yield return C4CraftChest("chest_craft", area, box);
        }

        /// <summary>A chest of the area made at a Work Bench from 30 of its blocks (a plain bag: the recipe offered), then set down.</summary>
        IEnumerator C4CraftChest(string clip, string area, Rect beside)
        {
            var hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            var inv = mod.Inventory;
            int ai = AreaBlocks.All.ToList().FindIndex(a => a.Name == area);
            if (ai < 0) yield break;
            var chestItem = RegionalChests.ItemOf(ai + 1);
            var saved = (ItemStack[])inv.Slots.Clone();
            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
            inv.Slots[0] = saved[0];
            inv.Slots[1] = ItemStack.Of(ItemType.WorkBench, 1);
            inv.Slots[Inventory.HotbarSize + 3] = ItemStack.Of(AreaBlocks.BlockItem(ai), 64);
            inv.Selected = 1;
            yield return null;
            // Room on the floor beside the hero for the bench and the chest after it.
            int face = hero.cState.facingRight ? 1 : -1;
            Vector2 c0 = col.bounds.center;
            Vector2? benchAt = null;
            for (float dx = 1.2f; dx <= 3.5f && benchAt == null; dx += 0.25f)
                foreach (int s in new[] { -face, face })
                {
                    var aim = c0 + new Vector2(s * dx, -0.3f);
                    if (Stations.FindSpot(StationKind.WorkBench, hero, aim, out _)) { benchAt = aim; break; }
                }
            if (benchAt == null) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：放不下工作台"); for (int i = 0; i < saved.Length; i++) inv.Slots[i] = saved[i]; yield break; }
            aimInit = false;
            SmoothAim(c0 + new Vector2(0.5f, 0.6f), 99f);
            yield return null;
            yield return null;
            StartRec(clip);
            yield return new WaitForSeconds(0.3f);
            yield return C4PlaceAt(benchAt.Value, 0.35f, 9f);
            yield return new WaitForSeconds(0.4f);
            PointerOn(GuiOf(aimNow));
            InventoryGui.Open = true;
            mod.PlaySound("Menu_Open", 0.6f);
            InventoryGui.DevFocus(0);
            yield return new WaitForSeconds(0.5f);
            var offered = InventoryGui.DevOffered();
            int target = offered.IndexOf(chestItem);
            Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：配方 {offered.Count} 个：{string.Join(",", offered)}；{ItemDb.Get(chestItem).Name} 在第 {target}");
            if (target >= 0)
            {
                var r0 = InventoryGui.DevHitRect("Recipe", InventoryGui.Focus);
                if (r0 != null) yield return Glide(r0.Value.center, 0.5f);
                for (int guard = 0; guard < 40 && InventoryGui.Focus != target; guard++)
                {
                    InventoryGui.DevScrollRecipes(target > InventoryGui.Focus ? 1 : -1);
                    mod.PlaySound("Menu_Tick", 0.5f);
                    yield return new WaitForSeconds(0.13f);
                }
                yield return new WaitForSeconds(0.5f);
                yield return ClickHit("Recipe", target, 0.2f);
                yield return new WaitForSeconds(0.4f);
                yield return ClickHit("Slot", 2, 0.55f);
                yield return new WaitForSeconds(0.4f);
            }
            InventoryGui.Open = false;
            PointerOff();
            mod.ReturnOrDropMouseItem();
            yield return new WaitForSeconds(0.3f);
            if (!inv.Slots[2].IsEmpty && inv.Slots[2].Type == chestItem)
            {
                inv.Selected = 2;
                Vector2? chestAt = null;
                for (float dx = 1.2f; dx <= 4f && chestAt == null; dx += 0.25f)
                    foreach (int s in new[] { -face, face })
                    {
                        var aim = (Vector2)col.bounds.center + new Vector2(s * dx, -0.3f);
                        if (Chests.FindSpot(hero, aim, out _)) { chestAt = aim; break; }
                    }
                if (chestAt != null) yield return C4PlaceAt(chestAt.Value, 0.35f, 9f);
            }
            yield return new WaitForSeconds(1.0f);
            StopRec();
            mod.DevAim = null;
            for (int i = 0; i < saved.Length; i++) inv.Slots[i] = saved[i];
        }

        /// <summary>A long flat floor of a dark room, eight area torches of different looks set along it with the smart cursor as the hero walks.</summary>
        IEnumerator C4TorchRow(string clip, string scene)
        {
            yield return EnterRoom(scene);
            float entered = Time.time;
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            if (GameManager.instance.sceneName != scene) yield break;
            QuietAround(hero.transform.position, 400f);
            var region = TerraWorld.Live;
            // The longest flat run of floor well inside the room.
            Vector2Int start = default;
            int bestRun = 0, dir = 1;
            var cr = region.CellRect;
            for (int x = cr.x + 4; x < cr.xMax - 4; x++)
                for (int y = cr.y + 4; y < cr.yMax - 6; y++)
                {
                    if (!C4Inside(region, x, y, 34, 14) || !C4Stand(x, y) || C4Stand(x - 1, y)) continue;
                    int run = 0;
                    while (run < 60 && C4Stand(x + run, y)) run++;
                    if (run > bestRun) { bestRun = run; start = new Vector2Int(x, y); }
                }
            if (bestRun < 20) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} 最长平地 {bestRun} 格"); yield break; }
            var inv = mod.Inventory;
            int[] styles = { 5, 20, 12, 7, 2, 22, 3, 35 };
            var savedHot = new ItemStack[Inventory.HotbarSize];
            for (int i = 0; i < Inventory.HotbarSize; i++) savedHot[i] = inv.Slots[i];
            for (int i = 0; i < styles.Length; i++) inv.Slots[i] = ItemStack.Of(RegionalTorches.ItemOf(styles[i]), 20);
            inv.Selected = 0;
            var col = hero.GetComponent<BoxCollider2D>();
            C4PutOn(hero, new Vector2Int(start.x + 2, start.y));
            C4Face(hero, 1);
            mod.DevSetSmartCursor(true);
            while (Time.time - entered < 6.5f) yield return null;
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(2f, 0.8f), 99f);
            float light0 = 0f;
            yield return C4Brightness(v => light0 = v);
            StartRec(clip);
            yield return new WaitForSeconds(0.3f);
            int placed = 0;
            float endX = CellGrid.CellCenter(start.x + bestRun - 3, start.y).x;
            for (int k = 0; k < styles.Length; k++)
            {
                inv.Selected = k;
                var spot = (Vector2)col.bounds.center + new Vector2(1.4f, 0.5f);
                int before = C4TorchesNear(col.bounds.center, 6f);
                yield return C4PlaceAt(spot, 0.18f, 14f);
                if (C4TorchesNear(col.bounds.center, 6f) > before) placed++;
                float x0 = col.bounds.center.x;
                if (col.bounds.center.x + 2.6f > endX) break;
                yield return C4WalkTo(hero, x0 + 2.6f, 1.2f, () => (Vector2)col.bounds.center + new Vector2(2.2f, 0.6f));
            }
            yield return new WaitForSeconds(0.4f);
            // A look back over the lit floor.
            C4Face(hero, -1);
            for (float t = 0f; t < 1.2f; t += Time.deltaTime) { SmoothAim((Vector2)col.bounds.center + new Vector2(-4f, 1f), 3f); yield return null; }
            float light1 = 0f;
            yield return C4Brightness(v => light1 = v);
            StopRec();
            mod.DevSetSmartCursor(false);
            for (int i = 0; i < Inventory.HotbarSize; i++) inv.Slots[i] = savedHot[i];
            Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} 平地 {bestRun} 格，放下 {placed} 支，亮度 {light0:F3} → {light1:F3}");
        }

        /// <summary>
        /// A tunnel dug (before the take) into a dark room's rock, its mouth on the room's floor; the hero walks into the
        /// dark tunnel setting an area torch of a new look every two units, each lighting the dug walls in its colour.
        /// </summary>
        IEnumerator C4TorchTunnel(string clip, string[] scenes)
        {
            string scene = null;
            Vector2Int stand = default;
            int dir = 1, fx = 0, run = 0;
            foreach (var sc in scenes)
            {
                if (!entryGates.ContainsKey(sc)) continue;
                yield return EnterRoom(sc);
                if (GameManager.instance.sceneName != sc) continue;
                var region = TerraWorld.Live;
                var cr = region.CellRect;
                int best = 0;
                for (int x = cr.x + 4; x < cr.xMax - 4; x++)
                    for (int y = cr.y + 4; y < cr.yMax - 8; y++)
                    {
                        if (!C4Inside(region, x, y, 34, 14) || !C4Stand(x, y)) continue;
                        foreach (int d in new[] { -1, 1 })
                        {
                            int f = x + d, k = 0;
                            while (k < 4 && !TerraWorld.IsSolid(f, y) && TerraWorld.IsSolid(f, y - 1)) { f += d; k++; }
                            if (k < 2 || k >= 4) continue;
                            int n = 0;
                            for (; n < 34; n++)
                            {
                                int cx = f + d * n;
                                bool ok = TerraWorld.IsSolid(cx, y - 1) && C4Inside(region, cx, y, 34, 14);
                                for (int r = 0; r < 5 && ok; r++) ok = C4Dig(cx, y + r);
                                if (!ok) break;
                            }
                            if (n > best) { best = n; stand = new Vector2Int(x, y); dir = d; fx = f; }
                        }
                    }
                if (best >= 22) { scene = sc; run = best; break; }
                Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{sc} 最长能挖的岩层 {best} 格，不够");
            }
            if (scene == null) yield break;
            float entered = Time.time;
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            QuietAround(hero.transform.position, 400f);
            // The tunnel: four rows over the floor, the whole run.
            for (int n = 0; n < run; n++)
                for (int r = 0; r < 4; r++) TerraWorld.Set(fx + dir * n, stand.y + r, CellKind.Empty);
            var inv = mod.Inventory;
            int[] styles = { 5, 20, 12, 7, 2, 22, 3, 35, 27, 41 };
            var savedHot = new ItemStack[Inventory.HotbarSize];
            for (int i = 0; i < Inventory.HotbarSize; i++) savedHot[i] = inv.Slots[i];
            for (int i = 0; i < Inventory.HotbarSize; i++) inv.Slots[i] = ItemStack.Of(RegionalTorches.ItemOf(styles[i]), 20);
            inv.Selected = 0;
            mod.DevSetSmartCursor(false);
            var col = hero.GetComponent<BoxCollider2D>();
            C4PutOn(hero, stand);
            C4Face(hero, dir);
            while (Time.time - entered < 7f) { QuietAround(hero.transform.position, 400f); yield return null; }
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 2f, 0.3f), 99f);
            float light0 = 0f;
            yield return C4Brightness(v => light0 = v);
            StartRec(clip);
            yield return new WaitForSeconds(0.3f);
            int placed = 0;
            float endX = CellGrid.CellCenter(fx + dir * (run - 2), stand.y).x;
            // The first torch just inside the mouth, then one every two units as he goes in.
            float nextX = CellGrid.CellCenter(fx + dir, stand.y).x;
            for (int k = 0; k < styles.Length && (nextX - endX) * dir < 0f; k++)
            {
                yield return C4WalkTo(hero, nextX - dir * 1.2f, 2f, () => new Vector2(nextX, CellGrid.CellCenter(0, stand.y).y + 0.6f));
                inv.Selected = k;
                var cell = CellGrid.WorldToCell(new Vector2(nextX, CellGrid.CellCenter(0, stand.y).y));
                // A torch on the floor, or (every other one) high on the tunnel's side, as the light falls.
                var at = CellGrid.CellCenter(cell.x, stand.y);
                int before = C4TorchesNear(at, 1.5f);
                yield return C4PlaceAt(at, 0.15f, 14f);
                if (C4TorchesNear(at, 1.5f) > before) placed++;
                yield return new WaitForSeconds(0.12f);
                nextX += dir * 2.0f;
            }
            yield return new WaitForSeconds(0.3f);
            C4Face(hero, -dir);
            for (float t = 0f; t < 1.3f; t += Time.deltaTime) { SmoothAim((Vector2)col.bounds.center + new Vector2(-dir * 4f, 0.8f), 3f); yield return null; }
            float light1 = 0f;
            yield return C4Brightness(v => light1 = v);
            StopRec();
            for (int i = 0; i < Inventory.HotbarSize; i++) inv.Slots[i] = savedHot[i];
            Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} 隧道 {run} 格（站 {stand} 往{(dir < 0 ? "左" : "右")}），放下 {placed} 支，亮度 {light0:F3} → {light1:F3}");
        }

        static int C4TorchesNear(Vector2 at, float r)
        {
            var c = CellGrid.WorldToCell(at);
            int n = 0, rc = Mathf.CeilToInt(r / CellGrid.CellSize);
            for (int x = c.x - rc; x <= c.x + rc; x++)
                for (int y = c.y - rc; y <= c.y + rc; y++)
                    if (TerraWorld.Get(x, y) == CellKind.Torch) n++;
            return n;
        }

        /// <summary>The room's pots and lamps in a row on one floor, struck one after another: torches of the area, then Geo.</summary>
        IEnumerator C4Pots(string clip, string scene)
        {
            yield return EnterRoom(scene);
            float entered = Time.time;
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            if (GameManager.instance.sceneName != scene) yield break;
            QuietAround(hero.transform.position, 400f);
            var list = new List<Breakable>();
            foreach (var b in Object.FindObjectsByType<Breakable>(FindObjectsSortMode.None))
            {
                var c = b != null ? b.GetComponent<Collider2D>() : null;
                if (b == null || !b.isActiveAndEnabled || c == null || !c.enabled || BreakableDrops.DevIsBroken(b) || BreakableDrops.Looted(b.gameObject)) continue;
                if (b.gameObject.scene.name != scene || Mathf.Abs(b.transform.position.z) > 1f) continue;
                list.Add(b);
            }
            // The biggest group on one floor: within 12 units across and a unit and a half up or down.
            List<Breakable> row = null;
            foreach (var a in list)
            {
                var g = list.Where(o => Mathf.Abs(o.transform.position.y - a.transform.position.y) < 1.5f && o.transform.position.x >= a.transform.position.x && o.transform.position.x - a.transform.position.x < 12f)
                    .OrderBy(o => o.transform.position.x).ToList();
                if (row == null || g.Count > row.Count || (g.Count == row.Count && a.transform.position.y > row[0].transform.position.y + 0.5f)) row = g;
            }
            if (row == null || row.Count < 2) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} 可破坏物 {list.Count} 个，没有成排的"); yield break; }
            var inv = mod.Inventory;
            var savedSlot = inv.Slots[9];
            inv.Slots[9] = ItemStack.Of(ItemType.NightsEdge, 1);
            inv.Selected = 9;
            var col = hero.GetComponent<BoxCollider2D>();
            Vector2 first = row[0].GetComponent<Collider2D>().bounds.center;
            hero.transform.position = Clear(hero, first + new Vector2(-3f, 0.2f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            C4Face(hero, 1);
            while (Time.time - entered < 6.5f) yield return null;
            aimInit = false;
            SmoothAim(first, 99f);
            StartRec(clip);
            yield return new WaitForSeconds(0.3f);
            int k = 0, broke = 0;
            foreach (var b in row)
            {
                if (b == null || BreakableDrops.DevIsBroken(b)) continue;
                var bc = b.GetComponent<Collider2D>();
                if (bc == null) continue;
                Vector2 at = bc.bounds.center;
                yield return C4WalkTo(hero, at.x - 1.3f, 2f, () => at);
                C4Face(hero, 1);
                BreakableDrops.DevRoll = k % 2 == 0 ? 1 : 3;
                int dropped = BreakableDrops.DevDropped;
                for (int s = 0; s < 3 && BreakableDrops.DevDropped == dropped && b != null && !BreakableDrops.DevIsBroken(b); s++)
                {
                    for (float t = 0f; t < 0.15f; t += Time.deltaTime) { SmoothAim(at, 14f); yield return null; }
                    while (!mod.DevReady) yield return null;
                    mod.DevUseSelected(at);
                    yield return new WaitForSeconds(0.3f);
                }
                if (b != null && !BreakableDrops.DevIsBroken(b))
                    HitTaker.Hit(b.gameObject, new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Nail, DamageDealt = 21, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 1f });
                BreakableDrops.DevRoll = null;
                if (BreakableDrops.DevDropped > dropped) broke++;
                k++;
                yield return new WaitForSeconds(0.25f);
            }
            // The drops gathered.
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                Vector2? drop = null;
                float bd = 64f;
                foreach (var dp in WorldItems.Positions())
                {
                    float d2 = (dp - (Vector2)col.bounds.center).sqrMagnitude;
                    if (d2 < bd) { bd = d2; drop = dp; }
                }
                Player.TerraMovement.DevMove = drop == null || Mathf.Abs(drop.Value.x - col.bounds.center.x) < 0.3f ? 0 : drop.Value.x > col.bounds.center.x ? 1 : -1;
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.5f);
            StopRec();
            inv.Slots[9] = savedSlot;
            Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} 一排 {row.Count} 个，打碎掉落 {broke}，最后 {BreakableDrops.DevLast}");
        }

        /// <summary>A geo rock mined with a pickaxe: each blow knocks Geo out, the hero gathers it (the amounts popping up).</summary>
        IEnumerator C4GeoRock(string clip, string scene)
        {
            yield return EnterRoom(scene);
            float entered = Time.time;
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            if (GameManager.instance.sceneName != scene) yield break;
            QuietAround(hero.transform.position, 400f);
            GeoRock rock = null;
            PlayMakerFSM rf = null;
            Vector2 rockAt = default;
            foreach (var r in Object.FindObjectsByType<GeoRock>(FindObjectsSortMode.None))
            {
                var fsm = r.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Geo Rock");
                var rc = r.GetComponentInChildren<Collider2D>();
                if (fsm == null || rc == null || !r.gameObject.activeInHierarchy || fsm.FsmVariables.GetFsmInt("Hits").Value <= 0) continue;
                // One with a floor beside it.
                var cell = CellGrid.WorldToCell(new Vector2(rc.bounds.min.x - 1.2f, rc.bounds.min.y + 0.2f));
                bool floor = false;
                for (int y = cell.y - 2; y <= cell.y + 2 && !floor; y++) floor = C4Stand(cell.x, y);
                if (!floor) continue;
                rock = r; rf = fsm; rockAt = rc.bounds.center;
                break;
            }
            if (rock == null) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} 没有旁边有地面的吉欧石"); yield break; }
            var col = rock.GetComponentInChildren<Collider2D>();
            var inv = mod.Inventory;
            var savedSlot = inv.Slots[9];
            inv.Slots[9] = ItemStack.Of(ItemType.CopperPickaxe, 1);
            inv.Selected = 9;
            hero.transform.position = Clear(hero, new Vector2(col.bounds.min.x - 3.5f, col.bounds.min.y + 0.3f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            C4Face(hero, 1);
            while (Time.time - entered < 6.5f) yield return null;
            aimInit = false;
            SmoothAim(rockAt, 99f);
            int geo0 = PlayerData.instance.geo;
            StartRec(clip);
            yield return new WaitForSeconds(0.3f);
            var hcol = hero.GetComponent<BoxCollider2D>();
            yield return C4WalkTo(hero, col.bounds.min.x - 0.9f, 2f, () => rockAt);
            C4Face(hero, 1);
            int strikes = 0;
            for (; strikes < 14 && rf.FsmVariables.GetFsmInt("Hits").Value > 0; strikes++)
            {
                for (float t = 0f; t < 0.12f; t += Time.deltaTime) { SmoothAim(rockAt, 12f); yield return null; }
                mod.TestStrike(col, 1f);
                yield return new WaitForSeconds(0.32f);
            }
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                Vector2? g = null;
                float bd = 36f;
                foreach (var gc in Object.FindObjectsByType<GeoControl>(FindObjectsSortMode.None))
                {
                    if (gc == null || !gc.gameObject.activeInHierarchy) continue;
                    float d2 = ((Vector2)gc.transform.position - (Vector2)hcol.bounds.center).sqrMagnitude;
                    if (d2 < bd) { bd = d2; g = gc.transform.position; }
                }
                Player.TerraMovement.DevMove = g == null || Mathf.Abs(g.Value.x - hcol.bounds.center.x) < 0.3f ? 0 : g.Value.x > hcol.bounds.center.x ? 1 : -1;
                SmoothAim(rockAt + new Vector2(0f, 1f), 3f);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.4f);
            StopRec();
            inv.Slots[9] = savedSlot;
            Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} {rock.name} 敲了 {strikes} 下，状态 {rf.ActiveStateName}，吉欧 {geo0} → {PlayerData.instance.geo}");
        }
    }
}
