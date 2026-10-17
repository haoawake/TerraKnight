using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_quirrelbench"): Quirrel's bench on the City of Tears balcony (Ruins1_02), as a player uses it. A
    /// row of platforms and a block set down beside the bench; the platform trials (stand on, jump up through, place one
    /// more) before; then Quirrel listened to (Up at his prompt, Space through his words), the bench sat on and left; the
    /// trials again after each, and a block placed and stood on. The hero's collision state is logged with each.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator QuirrelBenchTest()
        {
            const string scene = "Ruins1_02";
            mod.DevNoFallDamage = true;
            yield return GoTo(scene, EntryGateOf(scene));
            yield return new WaitForSeconds(1.5f);
            bool done = false;
            mod.StartCoroutine(NetKeepAlive(() => done));
            var hero = HeroController.instance;
            PlayMakerFSM bench = null, quirrel = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (f.gameObject.scene.name != scene) continue;
                if (f.FsmName == "Bench Control") bench = f;
                if (f.FsmName == "npc_control" && f.gameObject.name.Contains("Quirrel")) quirrel = f;
            }
            if (bench == null) { FxCheck("奎若的长椅", false, "没找到"); done = true; yield break; }
            Vector2 bp = bench.transform.position;
            // The balcony floor's top under the bench, and a row of platforms 3 units over it, left of the bench.
            var floor = Physics2D.Raycast(bp + new Vector2(-5f, 2f), Vector2.down, 10f, 1 << ChunkColliders.TerrainLayer);
            float floorTop = floor.collider != null ? floor.point.y : bp.y - 0.6f;
            var c0 = CellGrid.WorldToCell(new Vector2(bp.x - 7f, floorTop + 3.2f));
            int rowY = c0.y, rx0 = c0.x - 2, rx1 = c0.x + 2;
            var placed = new List<Vector2Int>();
            for (int x = rx0; x <= rx1; x++) { TerraWorld.Set(x, rowY, CellKind.Platform); placed.Add(new Vector2Int(x, rowY)); }
            yield return new WaitForSeconds(0.4f);
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 奎若长椅 长椅 {bp:F1}，地面顶 {floorTop:F2}，平台行 {rowY}（x 格 {rx0}..{rx1}），奎若 {(quirrel != null ? quirrel.ActiveStateName : "无")}");
            yield return PlatformTrials("奎若长椅 之前", hero, rx0, rx1, rowY);
            yield return BlockTrial("奎若长椅 之前", hero, bp + new Vector2(-3f, 0f), floorTop, placed);

            // Quirrel: Up at his prompt, then Space through what he says.
            if (quirrel != null && quirrel.gameObject.activeInHierarchy)
            {
                var col = hero.GetComponent<BoxCollider2D>();
                float offset = hero.transform.position.y - col.bounds.min.y;
                hero.transform.position = new Vector3(quirrel.transform.position.x + 1.2f, floorTop + offset + 0.05f, hero.transform.position.z);
                yield return new WaitForSeconds(0.8f);
                yield return Tap(Key.W, 0.25f);
                yield return Tap(Key.UpArrow, 0.25f);
                for (float t = Time.realtimeSinceStartup + 25f; Time.realtimeSinceStartup < t;)
                {
                    yield return new WaitForSeconds(0.9f);
                    if (GameHooks.PlayerHasControl() && !UI.HudDialogue.DevOpen && quirrel.ActiveStateName != "Talk") break;
                    yield return Tap(Key.Space, 0.15f);
                }
                Plugin.Log.LogInfo($"[AutoTest] 修复1016 奎若长椅 和奎若说完话：奎若 {quirrel.ActiveStateName}，可操作 {GameHooks.PlayerHasControl()}；{PlatformState(hero)}");
                yield return PlatformTrials("和奎若说话之后", hero, rx0, rx1, rowY);
            }

            // The bench: down onto it, Up to sit, a while, Space to get up.
            {
                var col = hero.GetComponent<BoxCollider2D>();
                float offset = hero.transform.position.y - col.bounds.min.y;
                hero.transform.position = new Vector3(bp.x + 0.2f, floorTop + offset + 0.05f, hero.transform.position.z);
                yield return new WaitForSeconds(1f);
                yield return Tap(Key.W, 0.3f);
                yield return Tap(Key.UpArrow, 0.3f);
                yield return new WaitForSeconds(3f);
                bool sat = PlayerData.instance.atBench;
                Plugin.Log.LogInfo($"[AutoTest] 修复1016 奎若长椅 坐下 {sat}，长椅 {bench.ActiveStateName}：{PlatformState(hero)}");
                Shot(scene, "fx_quirrelbench_sit");
                yield return null;
                yield return Tap(Key.Space, 0.2f);
                yield return new WaitForSeconds(2.5f);
                Plugin.Log.LogInfo($"[AutoTest] 修复1016 奎若长椅 起身 坐着 {PlayerData.instance.atBench}，可操作 {GameHooks.PlayerHasControl()}，长椅 {bench.ActiveStateName}：{PlatformState(hero)}");
                FxCheck("坐上了奎若的长椅", sat, $"长椅 {bench.ActiveStateName}");
            }
            yield return PlatformTrials("坐过奎若的长椅之后", hero, rx0, rx1, rowY);
            yield return BlockTrial("坐过奎若的长椅之后", hero, bp + new Vector2(-3f, 0f), floorTop, placed);
            Shot(scene, "fx_quirrelbench_after");
            yield return null;
            foreach (var c in placed) TerraWorld.Set(c.x, c.y, CellKind.Empty);
            done = true;
        }

        /// <summary>A block placed on the floor by the bench (as the player would), then the Knight stood on it.</summary>
        IEnumerator BlockTrial(string when, HeroController hero, Vector2 near, float floorTop, List<Vector2Int> placed)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            var cell = CellGrid.WorldToCell(new Vector2(near.x, floorTop + 0.25f));
            if (TerraWorld.Get(cell.x, cell.y) != CellKind.Empty) { FxCheck($"{when}：放方块", false, $"格 {cell} 不空：{TerraWorld.Get(cell.x, cell.y)}"); yield break; }
            float offset = hero.transform.position.y - col.bounds.min.y;
            hero.transform.position = new Vector3(near.x - 2f, floorTop + offset + 0.05f, hero.transform.position.z);
            yield return new WaitForSeconds(0.5f);
            mod.DevUse(ItemType.DirtBlock, CellGrid.CellCenter(cell.x, cell.y));
            yield return new WaitForSeconds(0.3f);
            bool put = TerraWorld.IsSolid(cell.x, cell.y);
            placed.Add(cell);
            FxCheck($"{when}：放得下方块", put, $"格 {cell} 是 {TerraWorld.Get(cell.x, cell.y)}");
            if (!put) yield break;
            float top = (cell.y + 1) * CellGrid.CellSize;
            hero.transform.position = new Vector3(CellGrid.CellCenter(cell.x, cell.y).x, top + 2f + offset, hero.transform.position.z);
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);
            float feet = col.bounds.min.y;
            FxCheck($"{when}：站得上方块", hero.cState.onGround && Mathf.Abs(feet - top) < 0.25f, $"脚 {feet:F2}，方块顶 {top:F2}；{PlatformState(hero)}");
        }
    }
}
