using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "p4_Cdig"): recorder C's digging clips. In a few areas the pickaxe cuts a tunnel into the room's own
    /// rock (the area's block dropping from each cell and flying to the hero, the plants on the dug cells going with
    /// them, the area's wall showing in the hole), or sweeps a ceiling over the hero (Greenpath's hanging vines).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator P4_Cdig(HeroController hero)
        {
            yield return P4Kit();
            yield return C4DigClips();
        }

        IEnumerator C4DigClips()
        {
            yield return C4Ceiling("dig_greenpath_ceiling", "Fungus1_10");
            yield return C4Through("dig_greenpath_wall", "Fungus1_21", 0);
            yield return C4Through("dig_crystalpeak", "Mines_31", 0);
            yield return C4Through("dig_crystalpeak2", "Mines_37", 0);
            yield return C4Through("dig_city", "Ruins2_01", 1);
            yield return C4Through("dig_kingdomsedge", "Deepnest_East_06", 1);
            yield return C4Through("dig_deepnest", "Deepnest_01b", 0);
        }

        /// <summary>
        /// A wall dug through (C4WallSites' site of that number): the hero walks up to it, cuts a passage four cells high
        /// through it, column by column, walks out into the room beyond and stops.
        /// </summary>
        IEnumerator C4Through(string clip, string scene, int index)
        {
            yield return C4DigSetup(scene);
            float entered = Time.time;
            var hero = HeroController.instance;
            if (GameManager.instance.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：没进 {scene}"); yield break; }
            var sites = C4WallSites(scene);
            if (sites.Count <= index) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} 只有 {sites.Count} 处墙"); yield break; }
            var site = sites[index];
            int dir = site.Dir;
            var col = hero.GetComponent<BoxCollider2D>();
            C4PutOn(hero, site.Stand);
            C4Face(hero, dir);
            // The wall's first column, its last, and where to stop beyond it.
            int fx = site.Stand.x + dir;
            while (!TerraWorld.IsSolid(fx, site.Stand.y)) fx += dir;
            int lx = fx + dir * (site.Thick - 1);
            float stopX = CellGrid.CellCenter(lx + dir * 3, site.Stand.y).x;
            while (Time.time - entered < 7.5f) { QuietAround(hero.transform.position, 400f); yield return null; }
            aimInit = false;
            SmoothAim(CellGrid.CellCenter(fx, site.Stand.y + 1), 99f);
            var decor = C4Decor(scene);
            StartRec(clip);
            yield return new WaitForSeconds(0.3f);
            float t0 = Time.time;
            int dug = 0;
            bool through = false;
            float throughAt = 0f;
            while (Time.time - t0 < 9f)
            {
                PlayerData.instance.isInvincible = true;
                var c = (Vector2)col.bounds.center;
                Vector2Int? next = null;
                // The nearest solid cell of the passage ahead (feet to head), within the wall.
                int frontX = CellGrid.WorldToCell(new Vector2(dir > 0 ? col.bounds.max.x + 0.05f : col.bounds.min.x - 0.05f, c.y)).x;
                for (int k = 0; k < 3 && next == null; k++)
                {
                    int x = frontX + dir * k;
                    if ((x - fx) * dir < 0 || (x - lx) * dir > 0) continue;
                    for (int r = 0; r < 4 && next == null; r++)
                        if (TerraWorld.IsSolid(x, site.Stand.y + r) && TerraWorld.CanDig(x, site.Stand.y + r)) next = new Vector2Int(x, site.Stand.y + r);
                }
                bool blocked = false;
                for (int r = 0; r < 3; r++) blocked |= TerraWorld.IsSolid(frontX, site.Stand.y + r);
                if (!through && (c.x - CellGrid.CellCenter(lx, site.Stand.y).x) * dir > 0.3f) { through = true; throughAt = Time.time; }
                bool arrived = (c.x - stopX) * dir > 0f;
                SmoothAim(next != null ? CellGrid.CellCenter(next.Value.x, next.Value.y) : c + new Vector2(2.4f * dir, 0.3f), 16f);
                Player.TerraMovement.DevMove = blocked || arrived ? 0 : dir;
                if (mod.DevReady && next != null)
                {
                    var target = CellGrid.CellCenter(next.Value.x, next.Value.y);
                    var before = TerraWorld.Get(next.Value.x, next.Value.y);
                    mod.DevUseSelected(target, false, true);
                    if (TerraWorld.Get(next.Value.x, next.Value.y) != before) dug++;
                }
                if (arrived && next == null && hero.cState.onGround) break;
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime) { SmoothAim((Vector2)col.bounds.center + new Vector2(-dir * 2.5f, 0.6f), 4f); yield return null; }
            StopRec();
            mod.DevAim = null;
            int gone = decor.FindAll(sr => sr != null && !sr.enabled).Count;
            Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} 墙 {index}（站 {site.Stand}，厚 {site.Thick}）往{(dir < 0 ? "左" : "右")}，挖掉 {dug} 格，{(through ? $"{throughAt - t0:F1}s 挖穿" : "没挖穿")}，用时 {Time.time - t0:F1}s，植物消失 {gone}，背包 {C4BagSummary()}");
        }

        /// <summary>The room entered for a dig: the melee loadout's pickaxe in hand, a bag that keeps what is mined, the room's enemies away.</summary>
        IEnumerator C4DigSetup(string scene)
        {
            yield return EnterRoom(scene);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.TrySwitchLoadout(1);
            yield return null;
            // Terraria's infinite bag drops nothing mined: a normal one here.
            mod.Inventory.Infinite = false;
            QuietAround(hero.transform.position, 400f);
            int pick = -1;
            for (int i = 0; i < Inventory.HotbarSize; i++) if (!mod.Inventory.Slots[i].IsEmpty && mod.Inventory.Slots[i].Def.PickPower > 0) { pick = i; break; }
            if (pick >= 0) mod.Inventory.Selected = pick;
            mod.DevSetSmartCursor(false);
        }

        /// <summary>
        /// A tunnel into the room's rock face (the scan's best: the most plants on it, the longest run), four cells high:
        /// the next solid cell of the column in front (feet to head), then of the one after it, aimed at and struck;
        /// the hero steps on as the way opens.
        /// </summary>
        IEnumerator C4Tunnel(string clip, string scene, float seconds)
        {
            yield return C4DigSetup(scene);
            var hero = HeroController.instance;
            if (GameManager.instance.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：没进 {scene}"); yield break; }
            C4ScanScene(scene, out _, out _, out var stand, out int score, out int dir, out int run);
            if (score <= 0) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} 没有能挖的岩壁"); yield break; }
            var col = hero.GetComponent<BoxCollider2D>();
            C4PutOn(hero, stand);
            if (dir > 0 && !hero.cState.facingRight) hero.FaceRight();
            if (dir < 0 && hero.cState.facingRight) hero.FaceLeft();
            yield return new WaitForSeconds(1.4f);
            QuietAround(hero.transform.position, 400f);
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(2.2f * dir, 0.2f), 99f);
            var decor = C4Decor(scene);
            int decor0 = decor.Count;
            int picked0 = mod.Inventory.Slots[Inventory.HotbarSize].Count;
            StartRec(clip);
            yield return new WaitForSeconds(0.35f);
            float t0 = Time.time, startX = col.bounds.center.x;
            int dug = 0;
            while (Time.time - t0 < seconds)
            {
                PlayerData.instance.isInvincible = true;
                var c = (Vector2)col.bounds.center;
                int feetY = CellGrid.WorldToCell(new Vector2(c.x, col.bounds.min.y + 0.1f)).y;
                int frontX = CellGrid.WorldToCell(new Vector2(dir > 0 ? col.bounds.max.x + 0.05f : col.bounds.min.x - 0.05f, c.y)).x;
                Vector2Int? next = null;
                for (int k = 0; k < 2 && next == null; k++)
                    for (int r = 0; r < 4 && next == null; r++)
                        if (TerraWorld.IsSolid(frontX + dir * k, feetY + r) && TerraWorld.CanDig(frontX + dir * k, feetY + r)) next = new Vector2Int(frontX + dir * k, feetY + r);
                bool blocked = false;
                for (int r = 0; r < 3; r++) blocked |= TerraWorld.IsSolid(frontX, feetY + r);
                SmoothAim(next != null ? CellGrid.CellCenter(next.Value.x, next.Value.y) : c + new Vector2(2.4f * dir, 0.2f), 16f);
                Player.TerraMovement.DevMove = blocked ? 0 : dir;
                if (mod.DevReady && next != null)
                {
                    var target = CellGrid.CellCenter(next.Value.x, next.Value.y);
                    var before = TerraWorld.Get(next.Value.x, next.Value.y);
                    mod.DevUseSelected(target, false, true);
                    if (TerraWorld.Get(next.Value.x, next.Value.y) != before) dug++;
                }
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.8f);
            StopRec();
            mod.DevAim = null;
            int gone = decor.FindAll(sr => sr != null && !sr.enabled).Count;
            Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} 往{(dir < 0 ? "左" : "右")}挖进 {(col.bounds.center.x - startX) * dir:F1}，挖掉 {dug} 格，植物随之消失 {gone}/{decor0}，背包 {C4BagSummary()}");
        }

        /// <summary>
        /// A ceiling over the hero swept with the pickaxe: the first rock cell over each of nine columns, left to right,
        /// then the cell over it, right to left, the plants hanging there going with them; a few steps on and once more.
        /// </summary>
        IEnumerator C4Ceiling(string clip, string scene)
        {
            yield return C4DigSetup(scene);
            var hero = HeroController.instance;
            if (GameManager.instance.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：没进 {scene}"); yield break; }
            float entered = Time.time;
            C4ScanScene(scene, out var stand, out int score, out _, out _, out _, out _);
            if (score <= 0) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} 没有长植物的顶"); yield break; }
            var col = hero.GetComponent<BoxCollider2D>();
            C4PutOn(hero, stand);
            while (Time.time - entered < 7.5f) yield return null;
            QuietAround(hero.transform.position, 400f);
            var decor = C4Decor(scene);
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(-1.5f, 2.5f), 99f);
            StartRec(clip);
            yield return new WaitForSeconds(0.4f);
            int dug = 0;
            IEnumerator Strike(Vector2Int cell)
            {
                var target = CellGrid.CellCenter(cell.x, cell.y);
                for (float t = 0f; t < 0.09f || Vector2.Distance(aimNow, target) > 0.12f; t += Time.deltaTime)
                {
                    SmoothAim(target, 18f);
                    if (t > 0.6f) break;
                    yield return null;
                }
                while (!mod.DevReady) { SmoothAim(target, 18f); yield return null; }
                var before = TerraWorld.Get(cell.x, cell.y);
                for (int k = 0; k < 4 && TerraWorld.Get(cell.x, cell.y) == before; k++)
                {
                    while (!mod.DevReady) { SmoothAim(target, 18f); yield return null; }
                    mod.DevUseSelected(target, false, true);
                    yield return null;
                }
                if (TerraWorld.Get(cell.x, cell.y) != before) dug++;
            }
            int Top(int x, int fromY)
            {
                for (int dy = 0; dy <= 8; dy++) if (TerraWorld.IsSolid(x, fromY + dy)) return C4Dig(x, fromY + dy) ? fromY + dy : -1;
                return -1;
            }
            for (int round = 0; round < 2; round++)
            {
                var at = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int i = 0; i <= 8; i++)
                    {
                        int dx = pass == 0 ? -4 + i : 4 - i;
                        int top = Top(at.x + dx, at.y + 3);
                        if (top < 0) continue;
                        yield return Strike(new Vector2Int(at.x + dx, top));
                    }
                }
                if (round == 0)
                {
                    // On a few steps under the opened ceiling.
                    float x0 = col.bounds.center.x;
                    int way = C4Stand(at.x + 6, at.y) ? 1 : -1;
                    Player.TerraMovement.DevMove = way;
                    for (float t = 0f; t < 1.2f && Mathf.Abs(col.bounds.center.x - x0) < 2.8f; t += Time.deltaTime)
                    {
                        SmoothAim((Vector2)col.bounds.center + new Vector2(way * 2.5f, 2.6f), 8f);
                        yield return null;
                    }
                    Player.TerraMovement.DevMove = 0;
                    yield return new WaitForSeconds(0.15f);
                }
            }
            yield return new WaitForSeconds(0.8f);
            StopRec();
            mod.DevAim = null;
            int gone = decor.FindAll(sr => sr != null && !sr.enabled).Count;
            Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} 顶上挖掉 {dug} 格，植物随之消失 {gone}/{decor.Count}，背包 {C4BagSummary()}");
        }

        string C4BagSummary()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Inventory.Size; i++)
            {
                var s = mod.Inventory.Slots[i];
                if (!s.IsEmpty && (Cells.IsAreaBlock(s.Def.Places) || s.Type >= ItemType.AreaBlockKingsPass && s.Type <= ItemType.AreaWallGodhome)) sb.Append($"{s.Def.Name}×{s.Count} ");
            }
            return sb.Length == 0 ? "没有地区方块" : sb.ToString();
        }
    }
}
