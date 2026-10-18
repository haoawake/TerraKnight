using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "torch" and "door"). torch: the starting kit, torches placed in the dark and one
    /// held (their light), and Auto Select (Shift) picking the torch at the air, the pickaxe at a
    /// block in reach, the last torch far away, nothing when Shift is let go. door: King's Pass's
    /// great door with the hero pressed against it, hit with the pickaxe aimed at it.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator TorchTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            var inv = mod.Inventory;
            var kit = new System.Text.StringBuilder();
            for (int i = 0; i < Inventory.Size; i++)
                if (!inv.Slots[i].IsEmpty) kit.Append($"[{i}] {inv.Slots[i].Def.Name}×{inv.Slots[i].Count} ");
            Plugin.Log.LogInfo($"[AutoTest] 火把 开局物品：{kit}");
            // The kit as a new game has it, whatever the autotests' endless items say.
            bool wasInfinite = inv.Infinite;
            inv.Infinite = false;

            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            // A cave dug under the floor (dark, as a Terraria cave), the hero in it.
            var top = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y - 0.1f));
            for (int y = top.y - 14; y <= top.y - 4; y++)
                for (int x = top.x - 22; x <= top.x + 22; x++)
                    TerraWorld.Set(x, y, CellKind.Empty);
            hero.transform.position = Clear(hero, CellGrid.CellCenter(top.x, top.y - 13));
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);
            Shot(scene, "torch_0_dark");
            yield return null;
            // Torches on its floor, 12 tiles apart, as Terraria players space them.
            var feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
            int placed = 0;
            foreach (int dx in new[] { -18, -6, 6, 18 })
            {
                var c = new Vector2Int(feet.x + dx, top.y - 14);
                if (TerraWorld.Get(c.x, c.y) != CellKind.Empty || !TerraWorld.IsSolid(c.x, c.y - 1)) continue;
                TerraWorld.Set(c.x, c.y, CellKind.Torch);
                placed++;
            }
            yield return new WaitForSeconds(0.5f);
            Shot(scene, "torch_1_placed");
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 火把 洞里放了 {placed} 支（相隔 12 格）");

            // One in hand.
            inv.Selected = 3;
            yield return new WaitForSeconds(0.5f);
            Shot(scene, "torch_2_held");
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 火把 手持：{inv.Current.Def.Name}");
            inv.Selected = 0;

            // Auto Select.
            string Held() => inv.Current.IsEmpty ? "空" : $"{inv.Current.Def.Name}（第 {inv.Held} 格，覆盖 {inv.Override}）";
            TerraMod.DevAutoSelect = true;
            var centre = (Vector2)col.bounds.center;
            mod.DevAim = centre + new Vector2(1.5f, 1.5f);
            yield return null;
            yield return null;
            string air = Held();
            var ground = CellGrid.CellCenter(feet.x + 2, feet.y - 1);
            mod.DevAim = ground;
            yield return null;
            yield return null;
            string block = $"{Held()}，那格 {TerraWorld.Get(feet.x + 2, feet.y - 1)}";
            mod.DevAim = centre + new Vector2(9f, 2f);
            yield return null;
            yield return null;
            string far = Held();
            inv.Selected = 3;
            yield return null;
            yield return null;
            string farHeld = Held();
            inv.Selected = 0;
            mod.DevAim = centre + new Vector2(1.5f, 1.5f);
            yield return null;
            yield return null;
            Shot(scene, "torch_3_autoselect");
            yield return null;
            TerraMod.DevAutoSelect = false;
            yield return null;
            yield return null;
            string released = Held();
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 智能切换：瞄空气 {air}；瞄脚下方块 {block}；瞄远处空气 {far}；手里本来就是火把时瞄远处 {farHeld}；松开 Shift {released}");
            inv.Infinite = wasInfinite;
            mod.DevNoFallDamage = false;
        }

        /// <summary>
        /// Dev (suite "pylon"): a pylon unlocked on Dirtmouth's bench, then a teleport there from
        /// King's Pass: the player arrives standing beside it out of a portal in Dirtmouth's colours
        /// (not sat on the bench), the bench is the respawn point again, control comes back.
        /// </summary>
        IEnumerator PylonTest(HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            yield return GoTo("Town", "left1");
            hero = HeroController.instance;
            PlayMakerFSM bench = null;
            foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (fsm.FsmName == "Bench Control" && fsm.gameObject.scene.name == "Town") bench = fsm;
            if (bench == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 晶塔 德特茅斯没找到长椅");
                yield break;
            }
            var pd = PlayerData.instance;
            hero.transform.position = Clear(hero, (Vector2)bench.transform.position + Vector2.left * 3f);
            yield return new WaitForSeconds(0.5f);
            pd.atBench = true;
            yield return new WaitForSeconds(0.5f);
            pd.SetBenchRespawn(bench.gameObject.name, "Town", 1, true);
            yield return new WaitForSeconds(0.5f);
            pd.atBench = false;
            yield return new WaitForSeconds(1f);
            if (Pylons.Unlocked.Count == 0)
            {
                Plugin.Log.LogInfo("[AutoTest] 晶塔 没解锁");
                yield break;
            }
            yield return GoTo("Tutorial_01", "right1");
            int runs = Player.OpeningPortal.DevRuns, summons = Player.OpeningPortal.DevSummons;
            Pylons.Teleport(Pylons.Unlocked[0]);
            float wait = Time.time + 30f;
            var gm = GameManager.instance;
            while ((gm.sceneName != "Town" || gm.IsInSceneTransition) && Time.time < wait) yield return null;
            hero = HeroController.instance;
            // The arrival, frame by frame of the portal.
            for (int i = 0; i < 12; i++)
            {
                Shot("Town", $"pylon_{i:D2}");
                if (i == 3) LogRenderersAt(new Vector2(132.4f, 10.8f), "长椅上的人影", 6f);
                Plugin.Log.LogInfo($"[AutoTest] 晶塔 第 {i} 张：主角 {hero.transform.position:F1}，传送门 {Player.OpeningPortal.DevPhase}，人物 {mod.DevPlayerDrawn}，长椅上 {pd.atBench}，knight 渲染 {hero.GetComponent<MeshRenderer>().enabled}");
                yield return null;
                yield return new WaitForSeconds(0.3f);
            }
            yield return new WaitForSeconds(1f);
            Plugin.Log.LogInfo($"[AutoTest] 晶塔 到达：场景 {gm.sceneName}，主角 {hero.transform.position:F1}，晶塔 {Pylons.Unlocked[0].Local}，坐在长椅上={pd.atBench}，重生点 {pd.respawnScene}/{pd.respawnMarkerName} 类型 {pd.respawnType}（应为 1），" +
                               $"传送门 {Player.OpeningPortal.DevSummons - summons} 次、走完 {Player.OpeningPortal.DevRuns - runs} 次，配色 {Player.OpeningPortal.DevPalette}，可操作 {GameHooks.PlayerHasControl()}");
            // Other areas' portals, opened here for their colours.
            var col = hero.GetComponent<BoxCollider2D>();
            foreach (var area in new[] { "苍绿之径", "水晶山峰", "格林剧团" })
            {
                Player.OpeningPortal.Summon(hero, new Vector2(col.bounds.center.x - 2.5f, col.bounds.min.y), area, 1);
                yield return new WaitForSeconds(1.4f);
                Shot("Town", "pylon_area_" + area);
                yield return null;
                while (Player.OpeningPortal.Active) yield return null;
                yield return new WaitForSeconds(0.3f);
            }
            mod.DevNoFallDamage = false;
            PlayerData.instance.isInvincible = false;
        }

        IEnumerator DoorTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            // What can be broken in this scene, by FSM.
            var list = new System.Text.StringBuilder();
            PlayMakerFSM door = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                var c = f.GetComponent<Collider2D>();
                if (c == null || !f.gameObject.activeInHierarchy) continue;
                string n = f.FsmName + " " + f.gameObject.name;
                if (n.IndexOf("Break", System.StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Door", System.StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                list.Append($"\n    {f.gameObject.name} [{f.FsmName}] 层{f.gameObject.layer} 触发器{c.isTrigger} 启用{c.enabled} 范围 {c.bounds.min:F1}..{c.bounds.max:F1}");
                if (f.FsmName == "Great Door") door = f;
            }
            Plugin.Log.LogInfo($"[AutoTest] 门 {scene} 里能打碎的：{list}");
            if (door == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 门 没找到国王山道的大门");
                yield break;
            }
            var dc = door.GetComponent<Collider2D>();
            var b = dc.bounds;
            var lo = CellGrid.WorldToCell(b.min);
            var hi = CellGrid.WorldToCell(b.max);
            int solid = 0, art = 0, cells = 0;
            for (int y = lo.y; y <= hi.y; y++)
                for (int x = lo.x; x <= hi.x; x++)
                {
                    cells++;
                    if (TerraWorld.IsSolid(x, y)) solid++;
                    var r = TerraWorld.RegionAt(x, y);
                    if (r != null && r.Grid.IsArt(x - r.CellOffset.x, y - r.CellOffset.y)) art++;
                }
            Plugin.Log.LogInfo($"[AutoTest] 门 大门的格子：{cells} 格里实心 {solid}、手放碰撞体 {art}");

            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            foreach (int dir in new[] { 1, -1 })
            {
                // Pressed against one side of it, then the pickaxe aimed at its middle.
                float x = dir > 0 ? b.min.x - col.bounds.extents.x - 0.6f : b.max.x + col.bounds.extents.x + 0.6f;
                hero.transform.position = Clear(hero, new Vector2(x, b.min.y + 0.2f));
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.4f);
                yield return WalkOn(hero, dir, 0.8f, $"走到门{(dir > 0 ? "左" : "右")}边贴住");
                float gap = dir > 0 ? b.min.x - col.bounds.max.x : col.bounds.min.x - b.max.x;
                Shot(scene, $"door_{(dir > 0 ? "left" : "right")}_pressed");
                yield return null;
                int hits0 = door.FsmVariables.GetFsmInt("Hits").Value;
                var seenBy = new System.Collections.Generic.HashSet<string>();
                MeleeHits.DebugLog = s => seenBy.Add(s);
                for (int i = 0; i < 4 && door != null && dc.enabled; i++)
                {
                    mod.DevUse(ItemType.CopperPickaxe, b.center);
                    yield return new WaitForSeconds(0.45f);
                }
                MeleeHits.DebugLog = null;
                if (door == null) break;
                Plugin.Log.LogInfo($"[AutoTest] 门 {(dir > 0 ? "左" : "右")}边贴住（离门 {gap:F2}）用镐子打 4 下：Hits {hits0} -> {door.FsmVariables.GetFsmInt("Hits").Value}，状态 {door.ActiveStateName}；挥砍盖到：{string.Join("；", seenBy)}");
            }

            // The breakable floor: stood on, hit with the pickaxe aimed down at it, then swords either way.
            PlayMakerFSM floor = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.FsmName == "break_floor" && f.gameObject.activeInHierarchy) floor = f;
            if (floor != null)
            {
                DumpFsms(floor.transform);
                var fc = floor.GetComponent<Collider2D>();
                var fb = fc.bounds;
                var cols = new System.Text.StringBuilder();
                foreach (var c in floor.GetComponentsInChildren<Collider2D>(true))
                    cols.Append($"{c.name}({c.GetType().Name} 触发器{c.isTrigger} 层{c.gameObject.layer} {c.bounds.min:F1}..{c.bounds.max:F1}) ");
                Plugin.Log.LogInfo($"[AutoTest] 门 木板的碰撞体：{cols}");
                // Under it (it is a floor broken from below, its Hero Range under it): the head against
                // it as at the top of a jump, then a step lower; without the fix, then with it.
                Collider2D boardSolid = null;
                foreach (var c in floor.GetComponentsInChildren<Collider2D>(true)) if (!c.isTrigger) boardSolid = c;
                var sb2 = boardSolid != null ? boardSolid.bounds : fb;
                var hitsBy = new System.Collections.Generic.HashSet<string>();
                float gravity = rb.gravityScale;
                // One hit a round, its count put back to 0 between rounds: on top of it, under it with the
                // head against it, a cell lower; without the fix, then with it.
                foreach (var (where, fix) in new[] { ("上面", false), ("上面", true), ("下面贴住", false), ("下面贴住", true), ("下面低一格", false), ("下面低一格", true) })
                {
                    if (floor == null || floor.ActiveStateName == "Break" || floor.ActiveStateName == "Activated") break;
                    floor.FsmVariables.GetFsmInt("Hits").Value = 0;
                    MeleeHits.HeroRangeFix = fix;
                    rb.gravityScale = 0f;
                    float down = hero.transform.position.y - col.bounds.min.y, up = hero.transform.position.y - col.bounds.max.y;
                    float y = where == "上面" ? sb2.max.y + 0.03f + down : sb2.min.y - 0.03f - (where == "下面低一格" ? 1f : 0f) + up;
                    hero.transform.position = new Vector3(sb2.center.x - 0.6f, y, hero.transform.position.z);
                    rb.linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.3f);
                    if (!fix) Shot(scene, "door_board_" + where);
                    hitsBy.Clear();
                    MeleeHits.DebugLog = s => hitsBy.Add(s);
                    rb.linearVelocity = Vector2.zero;
                    mod.DevUse(ItemType.CopperPickaxe, new Vector2(sb2.center.x, sb2.center.y));
                    yield return new WaitForSeconds(0.45f);
                    bool inRange = floor.FsmVariables.GetFsmBool("Hero Range").Value;
                    rb.gravityScale = gravity;
                    MeleeHits.DebugLog = null;
                    Plugin.Log.LogInfo($"[AutoTest] 门 木板 {where} {(fix ? "修复后" : "修复前")}：打一下计数 {floor.FsmVariables.GetFsmInt("Hits").Value}（应为 1），Hero Range {(inRange ? "在" : "不在")}，挥砍盖到 {hitsBy.Count} 个：{string.Join("；", hitsBy)}");
                }
                if (floor != null) floor.FsmVariables.GetFsmInt("Hits").Value = 0;
                MeleeHits.HeroRangeFix = true;
            }

            // A rock wall pressed against: the pickaxe on the cell beside the hero.
            var top = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y - 0.1f));
            for (int y = top.y - 10; y <= top.y - 4; y++)
                for (int x = top.x - 6; x <= top.x + 6; x++)
                    TerraWorld.Set(x, y, CellKind.Empty);
            hero.transform.position = Clear(hero, CellGrid.CellCenter(top.x, top.y - 9));
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            yield return WalkOn(hero, -1, 0.8f, "走到洞的左壁贴住");
            var mid = CellGrid.WorldToCell(col.bounds.center);
            int wallX = mid.x;
            while (wallX > mid.x - 6 && !TerraWorld.IsSolid(wallX, mid.y)) wallX--;
            var blockers = new System.Text.StringBuilder();
            foreach (var hit in Physics2D.OverlapBoxAll(new Vector2(col.bounds.min.x - 0.1f, col.bounds.center.y), new Vector2(0.18f, col.bounds.size.y * 0.8f), 0f, 1 << ChunkColliders.TerrainLayer))
                blockers.Append($"{hit.name}({hit.GetType().Name} 半径 {(hit is BoxCollider2D bx ? bx.edgeRadius : 0f):F2}) ");
            Plugin.Log.LogInfo($"[AutoTest] 门 贴墙：主角碰撞盒 边缘半径 {col.edgeRadius:F2}，左边 {col.bounds.min.x:F2}，第一格实心 x={wallX}（{wallX * CellGrid.CellSize + CellGrid.CellSize:F2} 处是它的右边），左边挨着的碰撞体：{blockers}");
            var report = new System.Text.StringBuilder();
            foreach (int dy in new[] { 0, 1, -1, 2 })
            {
                var target = new Vector2Int(wallX, mid.y + dy);
                if (!TerraWorld.IsSolid(target.x, target.y)) target.x = wallX;
                var before = TerraWorld.Get(target.x, target.y);
                bool dig = TerraWorld.CanDig(target.x, target.y);
                for (int i = 0; i < 8 && TerraWorld.Get(target.x, target.y) != CellKind.Empty; i++)
                {
                    mod.DevUse(ItemType.CopperPickaxe, CellGrid.CellCenter(target.x, target.y));
                    yield return new WaitForSeconds(0.4f);
                }
                report.Append($"格 ({target.x},{target.y}) {before} 能挖 {dig} -> {TerraWorld.Get(target.x, target.y)}；");
            }
            Shot(scene, "door_wall_dug");
            yield return null;
            float before2 = col.bounds.min.x;
            yield return WalkOn(hero, -1, 0.8f, "挖开以后接着往左走");
            report.Append($"挖完往左走：{before2:F2} -> {col.bounds.min.x:F2}");
            Plugin.Log.LogInfo($"[AutoTest] 门 贴着岩壁挖旁边的格子（主角左边 {col.bounds.min.x:F2}）：{report}");
            mod.DevNoFallDamage = false;
        }
    }
}
