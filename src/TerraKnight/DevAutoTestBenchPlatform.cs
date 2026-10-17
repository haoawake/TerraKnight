using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_benchplat"): platforms set down over a bench and the player sitting on it. Before and after the
    /// rest the same three things are tried on a row of platforms away from the bench: standing on it from above,
    /// jumping up through it from below (and landing on top), placing another platform. Logged meanwhile: the hero's
    /// collision against each platform composite (ignored or not), the composites' state, the hero's body.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static string PlatformState(HeroController hero)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var b = col.bounds;
            var sb = new System.Text.StringBuilder();
            int ignored = 0, off = 0, near = 0;
            foreach (var p in ChunkColliders.AllPlatforms)
            {
                if (p == null) continue;
                if (Vector2.Distance(p.bounds.center, b.center) > 30f) continue;
                near++;
                if (Physics2D.GetIgnoreCollision(col, p)) ignored++;
                if (!p.enabled || !p.gameObject.activeInHierarchy) off++;
            }
            sb.Append($"附近平台组 {near}，忽略碰撞 {ignored}，关着 {off}；主角刚体 {rb.bodyType}/模拟 {rb.simulated}/重力 {rb.gravityScale:F2}/{rb.collisionDetectionMode}，碰撞体 开 {col.enabled} 触发 {col.isTrigger} 层 {hero.gameObject.layer}，层间忽略 {Physics2D.GetIgnoreLayerCollision(hero.gameObject.layer, ChunkColliders.TerrainLayer)}，");
            sb.Append($"地上 {hero.cState.onGround} 可操作 {GameHooks.PlayerHasControl()} 坐 {PlayerData.instance.atBench} 台阶 {Player.StairCollision.DevState}");
            return sb.ToString();
        }

        /// <summary>Standing, jumping up through, placing: on a row of platforms at cell row y, columns x0..x1.</summary>
        IEnumerator PlatformTrials(string when, HeroController hero, int x0, int x1, int y, float dropFrom = 2.5f)
        {
            var rb = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            float top = (y + 1) * CellGrid.CellSize;
            float mid = (x0 + x1 + 1) * 0.5f * CellGrid.CellSize;
            float offset = hero.transform.position.y - col.bounds.min.y;
            // 1. Dropped from above: lands and stays on it.
            hero.transform.position = new Vector3(mid, top + dropFrom + offset, hero.transform.position.z);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);
            float feet = col.bounds.min.y;
            bool stands = hero.cState.onGround && Mathf.Abs(feet - top) < 0.25f;
            FxCheck($"{when}：从上面落到平台上站住", stands, $"脚 {feet:F2}，平台顶 {top:F2}，{PlatformState(hero)}");
            // 2. Up from below through it: passes and ends on top (not stuck in it, not held under it).
            hero.transform.position = new Vector3(mid, top - CellGrid.CellSize - 2.2f + offset, hero.transform.position.z);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            float startFeet = col.bounds.min.y;
            rb.linearVelocity = new Vector2(0f, 22f);
            yield return new WaitForSeconds(1.5f);
            feet = col.bounds.min.y;
            bool through = Mathf.Abs(feet - top) < 0.25f && hero.cState.onGround;
            FxCheck($"{when}：从下面跳穿平台站到上面", through, $"起跳脚 {startFeet:F2} -> 脚 {feet:F2}，平台顶 {top:F2}，{PlatformState(hero)}");
            // 3. Placing one more at the row's end.
            var cell = new Vector2Int(x1 + 1, y);
            TerraWorld.Set(cell.x, cell.y, CellKind.Empty);
            hero.transform.position = new Vector3(mid, top + 0.05f + offset, hero.transform.position.z);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.5f);
            mod.DevUse(ItemType.WoodPlatform, CellGrid.CellCenter(cell.x, cell.y));
            yield return new WaitForSeconds(0.3f);
            FxCheck($"{when}：放得下平台", TerraWorld.IsPlatform(cell.x, cell.y), $"格 {cell} 是 {TerraWorld.Get(cell.x, cell.y)}");
            TerraWorld.Set(cell.x, cell.y, CellKind.Empty);
        }

        IEnumerator BenchPlatformTest()
        {
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            PlayMakerFSM bench = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.FsmName == "Bench Control" && f.gameObject.scene.name == "Town") { bench = f; break; }
            if (bench == null) { FxCheck("长椅", false, "德特茅斯没有长椅"); yield break; }
            var bp = (Vector2)bench.transform.position;
            // A row of platforms of our own 10 units off the bench, in the open, four cells over the ground there.
            var groundCell = CellGrid.WorldToCell(bp + new Vector2(10f, 0f));
            int gy = groundCell.y;
            for (int k = 0; k < 20 && !TerraWorld.IsSolid(groundCell.x, gy - 1); k++) gy--;
            int rowY = gy + 3, rx0 = groundCell.x - 2, rx1 = groundCell.x + 2;
            var placed = new List<Vector2Int>();
            for (int x = rx0; x <= rx1; x++) { TerraWorld.Set(x, rowY, CellKind.Platform); placed.Add(new Vector2Int(x, rowY)); }
            yield return new WaitForSeconds(0.5f);
            yield return PlatformTrials("坐长椅之前", hero, rx0, rx1, rowY);

            // Platforms over the bench: across its seat and where the sitting Knight's body and head are.
            var benchCell = CellGrid.WorldToCell(bp);
            for (int dy = 0; dy <= 3; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    var c = new Vector2Int(benchCell.x + dx, benchCell.y + dy);
                    if (TerraWorld.Get(c.x, c.y) != CellKind.Empty) continue;
                    TerraWorld.Set(c.x, c.y, CellKind.Platform);
                    placed.Add(c);
                }
            hero.transform.position = new Vector3(bp.x + 0.3f, hero.transform.position.y, hero.transform.position.z);
            var col = hero.GetComponent<BoxCollider2D>();
            float offset = hero.transform.position.y - col.bounds.min.y;
            hero.transform.position = new Vector3(bp.x + 0.3f, (benchCell.y + 4) * CellGrid.CellSize + offset + 0.1f, hero.transform.position.z);
            yield return new WaitForSeconds(1.5f);
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 长椅平台 长椅 {bp:F1}，长椅状态 {bench.ActiveStateName}，主角 {(Vector2)hero.transform.position:F1}：{PlatformState(hero)}");
            // Down through the platforms onto the bench's seat if they hold him over it.
            for (int k = 0; k < 3 && bench.ActiveStateName != "In Range"; k++)
            {
                Player.TerraMovement.DevDown = true;
                yield return new WaitForSeconds(0.6f);
                Player.TerraMovement.DevDown = false;
                yield return new WaitForSeconds(0.4f);
            }
            yield return Tap(Key.W, 0.3f);
            yield return Tap(Key.UpArrow, 0.3f);
            yield return new WaitForSeconds(2.5f);
            bool sat = PlayerData.instance.atBench;
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 长椅平台 坐下 {sat}，长椅 {bench.ActiveStateName}：{PlatformState(hero)}");
            Shot("Town", "fx_benchplat_sit");
            yield return null;
            yield return Tap(Key.Space, 0.2f);
            yield return new WaitForSeconds(2f);
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 长椅平台 起身 坐着 {PlayerData.instance.atBench}，长椅 {bench.ActiveStateName}，主角 {(Vector2)hero.transform.position:F1}：{PlatformState(hero)}");
            Shot("Town", "fx_benchplat_up");
            yield return null;
            FxCheck("坐上了长椅（上面有平台）", sat, $"长椅 {bench.ActiveStateName}");
            yield return PlatformTrials("坐过长椅之后", hero, rx0, rx1, rowY);
            foreach (var c in placed) TerraWorld.Set(c.x, c.y, CellKind.Empty);
        }
    }
}
