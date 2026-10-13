using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "wall"): the plank-propped rock wall on the way to King's Pass's first crawlid
    /// (left of the start). The hero walks up to it from the right and digs it pressed against
    /// it, then a step back: what the wall is made of (grid cells, hand-placed pieces), what stops
    /// the hero, whether the pickaxe reaches and digs, whether he gets through.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator WallTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            Transform first = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "First Crawler" && t.gameObject.scene.name == scene) first = t;
            if (first == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 墙 没找到 First Crawler");
                yield break;
            }
            var crawler = first.GetComponentInChildren<HealthManager>(true);
            Vector2 cp = crawler != null ? (Vector2)crawler.transform.position : (Vector2)first.position;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            // The floor under the crawlid, then the wall: the first solid column to its right a little above the floor.
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            var down = Physics2D.Raycast(cp + Vector2.up * 0.5f, Vector2.down, 6f, terrain);
            float floorY = down.collider != null ? down.point.y : cp.y - 0.5f;
            var row = CellGrid.WorldToCell(new Vector2(cp.x, floorY + 0.75f));
            int wallX = row.x;
            while (wallX < row.x + 60 && !TerraWorld.IsSolid(wallX, row.y)) wallX++;
            int wallEnd = wallX;
            while (wallEnd < wallX + 30 && TerraWorld.IsSolid(wallEnd + 1, row.y)) wallEnd++;
            var map = new System.Text.StringBuilder();
            for (int y = row.y + 6; y >= row.y - 2; y--)
            {
                map.Append($"\n     {y,4} ");
                for (int x = wallX - 4; x <= wallEnd + 6; x++)
                {
                    var r = TerraWorld.RegionAt(x, y);
                    var k = TerraWorld.Get(x, y);
                    map.Append(r == null ? '?' : k == CellKind.Empty ? '.' : r.Grid.IsArt(x - r.CellOffset.x, y - r.CellOffset.y) ? 'a' : k == CellKind.Original ? '#' : 'b');
                }
            }
            Plugin.Log.LogInfo($"[AutoTest] 墙 第一只小爬虫在 {cp:F2}，地面 y={floorY:F2}；它右边的墙从格 x={wallX} 到 {wallEnd}（第 {row.y} 行）。附近的格子（x 从 {wallX - 4} 起，# 原地形，a 手放碰撞体，b 放的方块，. 空）：{map}");

            // From the right: walk up to it.
            float rightEdge = (wallEnd + 1) * CellGrid.CellSize;
            hero.transform.position = Clear(hero, new Vector2(rightEdge + 2f, floorY + 0.1f));
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            yield return WalkOn(hero, -1, 1.2f, "从右边走到墙跟前贴住");
            var stoppers = new System.Text.StringBuilder();
            foreach (var hit in Physics2D.OverlapBoxAll(new Vector2(col.bounds.min.x - 0.06f, col.bounds.center.y), new Vector2(0.1f, col.bounds.size.y * 0.8f), 0f, terrain))
                stoppers.Append($"{PathOf(hit.transform)}({hit.GetType().Name}{(hit.isTrigger ? " 触发器" : "")} {hit.bounds.min.x:F2}..{hit.bounds.max.x:F2}) ");
            Plugin.Log.LogInfo($"[AutoTest] 墙 贴住：主角左边 {col.bounds.min.x:F2}（墙的格子右边是 {rightEdge:F2}），挡住主角的：{stoppers}");
            Shot(scene, "wall_1_pressed");
            yield return null;

            // The tutorial door that stops him: what it is made of, then hit pressed against it and a step back.
            Transform door = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.StartsWith("Tute Door") && t.gameObject.scene.name == scene && Mathf.Abs(t.position.x - col.bounds.min.x) < 3f) door = t;
            if (door == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 墙 主角旁边没有 Tute Door");
                yield break;
            }
            var parts = new System.Text.StringBuilder();
            foreach (var c in door.GetComponentsInChildren<Collider2D>(true))
                parts.Append($"\n     {PathOf(c.transform)} {c.GetType().Name} 层{c.gameObject.layer} 触发器{c.isTrigger} 启用{c.enabled}/{c.gameObject.activeInHierarchy} {c.bounds.min:F2}..{c.bounds.max:F2}");
            Plugin.Log.LogInfo($"[AutoTest] 墙 {PathOf(door)} 在 {door.position:F2}，碰撞体：{parts}");
            DumpComponents(door);
            DumpFsms(door);
            string State()
            {
                var sb = new System.Text.StringBuilder();
                if (door == null) return "门已不在";
                foreach (var f in door.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    sb.Append($"{f.FsmName}:{f.ActiveStateName}");
                    foreach (var v in f.FsmVariables.IntVariables) sb.Append($" {v.Name}={v.Value}");
                    sb.Append("；");
                }
                return sb.ToString();
            }
            foreach (var (label, back) in new[] { ("贴住", 0f), ("退一步", 1f), ("退两步", 2f) })
            {
                if (door == null || !door.gameObject.activeInHierarchy) break;
                hero.transform.position = Clear(hero, new Vector2(rightEdge + 2f, floorY + 0.1f));
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.4f);
                yield return WalkOn(hero, -1, 1.2f, "走到门前");
                if (back > 0f) hero.transform.position += Vector3.right * back;
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.4f);
                string s0 = State();
                var seen = new System.Collections.Generic.HashSet<string>();
                MeleeHits.DebugLog = x => seen.Add(x);
                var aim = new Vector2(door.position.x, col.bounds.center.y);
                for (int i = 0; i < 3 && door != null && door.gameObject.activeInHierarchy; i++)
                {
                    mod.DevUse(ItemType.CopperPickaxe, aim);
                    yield return new WaitForSeconds(0.5f);
                }
                MeleeHits.DebugLog = null;
                Plugin.Log.LogInfo($"[AutoTest] 墙 {label}（主角左边 {col.bounds.min.x:F2}，门 {door.position.x:F2}）镐子打 3 下：{s0} -> {State()}；挥砍盖到：{string.Join("；", seen)}");
                if (back == 0f) Shot(scene, "wall_2_hit_pressed");
            }
            mod.DevNoFallDamage = false;
        }

        /// <summary>
        /// Dev (suite "onewall"): Howling Cliffs' one-way wall left of King's Pass (Cliffs_02, its
        /// break_floor FSM, the hit box right of the solid, a Hero Range trigger from just right of
        /// it). The hero pressed against it and a step back, the pickaxe aimed at the wall; each
        /// without the Hero Range fix and with it, its hit count put back to 0 between.
        /// </summary>
        IEnumerator OneWayWallTest(HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            string gate = null;
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                if (tp.targetScene == "Cliffs_02") gate = tp.entryPoint;
            if (gate == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 单向墙 国王山道里没有去 Cliffs_02 的门");
                yield break;
            }
            yield return GoTo("Cliffs_02", gate);
            hero = HeroController.instance;
            // A scene change resets the game's invincibility: on again, and the place cleared of what bites.
            PlayerData.instance.isInvincible = true;
            PlayMakerFSM wall = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.FsmName == "break_floor" && f.gameObject.name == "One Way Wall" && f.gameObject.scene.name == "Cliffs_02") wall = f;
            if (wall == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 单向墙 Cliffs_02 里没找到 One Way Wall");
                yield break;
            }
            Collider2D solid = null, range = null;
            foreach (var c in wall.GetComponentsInChildren<Collider2D>(true))
            {
                if (c.name == "Solid") solid = c;
                if (c.name == "Hero Range") range = c;
            }
            var sb = solid.bounds;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            Plugin.Log.LogInfo($"[AutoTest] 单向墙 实心 {sb.min:F2}..{sb.max:F2}，受击区 {wall.GetComponent<Collider2D>().bounds.min:F2}..{wall.GetComponent<Collider2D>().bounds.max:F2}，主角范围 {(range != null ? range.bounds.min.ToString("F2") + ".." + range.bounds.max.ToString("F2") : "无")}");
            foreach (var (label, back, fix) in new[] { ("贴住", 0f, false), ("退一步", 1f, false), ("贴住", 0f, true), ("退一步", 1f, true) })
            {
                if (wall == null || wall.ActiveStateName == "Break" || wall.ActiveStateName == "Activated") break;
                wall.FsmVariables.GetFsmInt("Hits").Value = 0;
                MeleeHits.HeroRangeFix = fix;
                // Where the player stood (his F8 report): the floor at 12.02, pressed against the solid's right side.
                float lift = hero.transform.position.y - col.bounds.min.y, half = hero.transform.position.x - col.bounds.center.x;
                hero.transform.position = new Vector3(sb.max.x + col.bounds.extents.x + 0.03f + back + half, 12.05f + lift, hero.transform.position.z);
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.4f);
                PlayerData.instance.isInvincible = true;
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.4f);
                if (!fix) Shot("Cliffs_02", "onewall_" + label);
                var seen = new System.Collections.Generic.HashSet<string>();
                MeleeHits.DebugLog = m => seen.Add(m);
                var ranges = new System.Text.StringBuilder();
                for (int i = 0; i < 2 && wall != null && wall.ActiveStateName != "Break"; i++)
                {
                    mod.DevUse(ItemType.CopperPickaxe, new Vector2(sb.center.x, col.bounds.center.y));
                    yield return new WaitForSeconds(0.5f);
                    ranges.Append(wall.FsmVariables.GetFsmBool("Hero Range").Value ? "在" : "不在").Append(' ');
                }
                MeleeHits.DebugLog = null;
                Plugin.Log.LogInfo($"[AutoTest] 单向墙 补发受击框 {MeleeHits.DevBoxTouches} 次；{(fix ? "修复后" : "修复前")} {label}（主角左边 {col.bounds.min.x:F2}，实心右边 {sb.max.x:F2}）镐子打 2 下：计数 {wall.FsmVariables.GetFsmInt("Hits").Value}（应为 2），状态 {wall.ActiveStateName}，Hero Range {ranges}；挥砍盖到：{string.Join("；", seen)}");
            }
            MeleeHits.HeroRangeFix = true;
            // A tiktik (the game's Climber) and a crawlid on a wall or roof: hit, they stay put.
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || !TerraKnockback.Immune(hm)) continue;
                var hc = hm.GetComponent<Collider2D>();
                Vector2 p0 = hm.transform.position;
                int immune0 = TerraKnockback.DevImmune;
                hm.hp = 9999;
                mod.Melee.HitWith(hc, 5, 12f, 0f, 1f, p0 + Vector2.left * 2f);
                yield return new WaitForSeconds(0.4f);
                Plugin.Log.LogInfo($"[AutoTest] 击退 {hm.gameObject.name}（Climber {hm.GetComponent<Climber>() != null}）击退 12 打一下：不推 {TerraKnockback.DevImmune - immune0} 次，移动 {Vector2.Distance(p0, hm.transform.position):F2}（它自己爬也会动一点）");
            }

            // The Fallen Star: one frame of its sheet in the bag and on the ground.
            var inv = mod.Inventory;
            inv.Slots[5] = ItemStack.Of(ItemType.FallenStar, 7);
            WorldItems.Drop(ItemStack.Of(ItemType.FallenStar, 3), (Vector2)col.bounds.center + Vector2.right * 1.5f);
            InventoryGui.Open = true;
            yield return new WaitForSeconds(0.8f);
            Shot(GameManager.instance.sceneName, "onewall_star");
            yield return null;
            // The book folded each time the bag opens.
            InventoryGui.DevBestiary(true);
            yield return null;
            bool openNow = InventoryGui.BestiaryOpen;
            InventoryGui.Open = false;
            yield return null;
            InventoryGui.Open = true;
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 图鉴 打开后 {openNow}，关掉背包再开 {InventoryGui.BestiaryOpen}（应为 False）；坠落之星帧数 {ItemFrames.FramesOf(ItemDb.Get(ItemType.FallenStar).Texture)}");
            InventoryGui.Open = false;

            // The map here, then after walking on into Cliffs_03 as the player did (a seamless crossing).
            Plugin.Log.LogInfo($"[AutoTest] 地图 {UI.FullMap.DevReport()}");
            // Just inside the room's left gate, as the player walked on.
            Bounds door = default;
            string doorTo = null;
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
            {
                var c = tp.GetComponent<Collider2D>();
                if (c == null || tp.gameObject.scene.name != "Cliffs_02" || tp.name != "left1") continue;
                door = c.bounds;
                doorTo = tp.targetScene;
            }
            Plugin.Log.LogInfo($"[AutoTest] 地图 Cliffs_02 左门通往 {doorTo ?? "无"}，门 {door.min:F1}..{door.max:F1}");
            hero.transform.position = Clear(hero, new Vector2(door.max.x + 2.5f, door.min.y + 1.2f));
            rb.linearVelocity = Vector2.zero;
            // The neighbours load behind the player first.
            yield return new WaitForSeconds(4f);
            PlayerData.instance.isInvincible = true;
            yield return WalkOn(hero, -1, 3f, $"往左走进 {doorTo}");
            yield return new WaitForSeconds(1.5f);
            Plugin.Log.LogInfo($"[AutoTest] 地图 走完：{GameManager.instance.sceneName} 主角 {hero.transform.position:F1}");
            Plugin.Log.LogInfo($"[AutoTest] 地图 走过去以后：{UI.FullMap.DevReport()}");
            UI.FullMap.Toggle();
            yield return new WaitForSeconds(0.6f);
            Plugin.Log.LogInfo($"[AutoTest] 地图 打开时：{UI.FullMap.DevView()}；悬崖的房间：{UI.FullMap.DevRooms("Cliffs")}");
            Shot(GameManager.instance.sceneName, "onewall_map");
            yield return null;
            UI.FullMap.Close();

            // A room the map has none for (the Stag Nest, Cliffs_03): the head from the room beside it.
            yield return GoTo("Cliffs_02", "left2");
            hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(4f);
            PlayerData.instance.isInvincible = true;
            yield return WalkOn(hero, -1, 2f, "往左走进 Cliffs_03");
            yield return new WaitForSeconds(1.5f);
            Plugin.Log.LogInfo($"[AutoTest] 地图 鹿角虫巢：{UI.FullMap.DevReport()}");
            UI.FullMap.Toggle();
            yield return new WaitForSeconds(0.6f);
            Plugin.Log.LogInfo($"[AutoTest] 地图 鹿角虫巢打开时：{UI.FullMap.DevView()}；悬崖的房间：{UI.FullMap.DevRooms("Cliffs")}");
            Shot(GameManager.instance.sceneName, "onewall_map_nest");
            yield return null;
            UI.FullMap.Close();
            mod.DevNoFallDamage = false;
        }
    }
}
