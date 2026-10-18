using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "platform"): with the smart cursor on, a platform row is extended and a stair
    /// built down to the floor, as clicks would do it; the slopes and frames are logged (the row's
    /// end turns into the stair's top, the stair's foot flattens when the row continues). Then the
    /// hero walks down the stair and back up, the feet checked against the stair surface every
    /// step, and drops through it holding Down.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator PlatformTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            var feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
            int fx = feet.x, floor = feet.y;
            // Room: 14 columns ahead, 6 rows up, open; the floor under them solid.
            bool room = true;
            for (int x = fx; x <= fx + 14 && room; x++)
            {
                if (!TerraWorld.IsSolid(x, floor - 1)) room = false;
                for (int y = floor; y <= floor + 6; y++) if (TerraWorld.Get(x, y) != CellKind.Empty) room = false;
            }
            if (!room)
            {
                Plugin.Log.LogInfo($"[AutoTest] 平台：({fx},{floor}) 前面没有平地，跳过");
                yield break;
            }
            int rowY = floor + 4;
            int bombs = 0, dynamites = 0;
            var placed = new List<Vector2Int>();
            // The row to build from, laid by hand.
            for (int x = fx + 2; x <= fx + 5; x++)
            {
                TerraWorld.Set(x, rowY, CellKind.Platform);
                placed.Add(new Vector2Int(x, rowY));
            }
            // The hero where every cell is in reach.
            hero.transform.position = Clear(hero, CellGrid.CellCenter(fx + 7, floor) + Vector2.up * 0.7f);
            yield return new WaitForSeconds(0.4f);
            mod.DevSetSmartCursor(true);
            // Down-right from the row's end, a cell at a time, the mouse on the next cell (45° from the last).
            var targets = new List<Vector2Int>();
            for (int k = 1; k <= 4; k++) targets.Add(new Vector2Int(fx + 5 + k, rowY - k));
            // Then on along the floor from the stair's foot.
            targets.Add(new Vector2Int(fx + 10, floor));
            targets.Add(new Vector2Int(fx + 11, floor));
            var log = new System.Text.StringBuilder();
            foreach (var t in targets)
            {
                // Standing on the floor within reach but out of the cell (a platform never goes inside the player).
                int stand = t.x <= fx + 8 ? t.x + 4 : t.x - 4;
                hero.transform.position = Clear(hero, CellGrid.CellCenter(stand, floor) + Vector2.up * 0.7f);
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.25f);
                mod.DevUse(ItemType.WoodPlatform, CellGrid.CellCenter(t.x, t.y));
                yield return null;
                yield return null;
                log.Append($"({t.x - fx},{t.y - floor})={(TerraWorld.IsPlatform(t.x, t.y) ? "有" : "没放上")} ");
                if (TerraWorld.IsPlatform(t.x, t.y)) placed.Add(t);
            }
            Plugin.Log.LogInfo($"[AutoTest] 平台：智能光标逐格放（相对主角脚下）：{log}");
            yield return new WaitForSeconds(0.3f);
            var shape = new System.Text.StringBuilder();
            foreach (var p in placed)
            {
                var r = TerraWorld.RegionAt(p.x, p.y);
                int frame = r != null ? PlatformStairs.FrameX(r.Grid, p.x - r.CellOffset.x, p.y - r.CellOffset.y) : -1;
                int brace = r != null ? PlatformStairs.SupportFrameX(r.Grid, p.x - r.CellOffset.x, p.y - r.CellOffset.y) : -1;
                int s = TerraWorld.GetSlope(p.x, p.y);
                shape.Append($"({p.x - fx},{p.y - floor}){(s == 1 ? "\\" : s == 2 ? "/" : "_")}{frame}{(brace >= 0 ? "+" + brace : "")} ");
            }
            Plugin.Log.LogInfo($"[AutoTest] 平台：坡度和帧（_ 平，\\ 左高右低，+ 下方支撑件）：{shape}");
            Shot(scene, "p1_stairs");
            yield return new WaitForSeconds(0.2f);

            // Down the stair: from the row, walking right.
            hero.transform.position = Clear(hero, CellGrid.CellCenter(fx + 3, rowY + 1) + Vector2.up * 0.7f);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            yield return Walk(hero, col, 1, 2.2f, "往下走");
            Shot(scene, "p2_walked_down");
            yield return new WaitForSeconds(0.2f);
            // And back up, from the top of the row along the floor.
            hero.transform.position = Clear(hero, CellGrid.CellCenter(fx + 11, floor + 1) + Vector2.up * 0.7f);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            yield return Walk(hero, col, -1, 2.2f, "往上走");
            Shot(scene, "p3_walked_up");
            yield return new WaitForSeconds(0.2f);

            // Holding Down halfway down the stair: through it, onto the floor.
            hero.transform.position = Clear(hero, CellGrid.CellCenter(fx + 7, rowY - 1) + Vector2.up * 1.2f);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            float before = col.bounds.min.y;
            bool wasOnStair = Player.StairCollision.OnStair;
            Player.TerraMovement.DevDown = true;
            yield return new WaitForSeconds(0.8f);
            Player.TerraMovement.DevDown = false;
            yield return new WaitForSeconds(0.3f);
            Plugin.Log.LogInfo($"[AutoTest] 平台：站在楼梯上={wasOnStair}，脚 {before - floor * CellGrid.CellSize:F2}；按住下 0.8s 后脚 {col.bounds.min.y - floor * CellGrid.CellSize:F2}（地面 0），着地={hero.cState.onGround}");

            // A jump from under the stair goes up through it and lands on it.
            hero.transform.position = Clear(hero, CellGrid.CellCenter(fx + 7, floor) + Vector2.up * 0.7f);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.5f);
            rb.linearVelocity = new Vector2(0f, 9f * Player.TerraMovement.PxT);
            float peak = col.bounds.min.y;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                peak = Mathf.Max(peak, col.bounds.min.y);
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 平台：从楼梯下面起跳，最高脚 {peak - floor * CellGrid.CellSize:F2}，落下后脚 {col.bounds.min.y - floor * CellGrid.CellSize:F2}，站在楼梯上={Player.StairCollision.OnStair}");

            // Bombs and dynamite onto a platform row, dropped and thrown: as in Terraria they fall through it to the ground.
            // A fresh row each time (the last blast takes the old one), the hero out of the way.
            int bx = fx + 30;
            foreach (var kind in new[] { ProjKind.Bomb, ProjKind.Dynamite, ProjKind.Bomb, ProjKind.Dynamite })
            {
                bool thrown = placed.Count > 0 && (kind == ProjKind.Bomb ? bombs++ : dynamites++) > 0;
                for (int x = bx; x <= bx + 6; x++)
                    if (TerraWorld.Get(x, rowY) == CellKind.Empty) { TerraWorld.Set(x, rowY, CellKind.Platform); placed.Add(new Vector2Int(x, rowY)); }
                float top = (rowY + 1) * CellGrid.CellSize;
                if (thrown)
                {
                    // Standing on the row's left end, throwing at its right end.
                    hero.transform.position = Clear(hero, CellGrid.CellCenter(bx, rowY + 1) + Vector2.up * 0.7f);
                    rb.linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.6f);
                    mod.DevUse(kind == ProjKind.Bomb ? ItemType.Bomb : ItemType.Dynamite, CellGrid.CellCenter(bx + 5, rowY + 1));
                    // Off the row before it goes off.
                    hero.transform.position = Clear(hero, CellGrid.CellCenter(bx - 14, floor) + Vector2.up * 0.7f);
                }
                else TerraProjectiles.Instance.Shoot(kind, CellGrid.CellCenter(bx + 3, rowY + 3), new Vector2(0.5f, 0f), 0, 0f, 0f);
                yield return new WaitForSeconds(1.5f);
                var at = TerraProjectiles.Instance.DevNewest(kind);
                Plugin.Log.LogInfo($"[AutoTest] 平台：{kind}{(thrown ? " 从手里扔到平台上" : " 从平台上方落下")} 1.5s 后 {(at is Vector2 a ? $"中心 y {a.y:F2}，比平台顶高 {(a.y - top) * 32f:F1} px" : "已经没了")}（平台顶 {top:F2}，地面 {floor * CellGrid.CellSize:F2}；原版穿过平台落到地面）");
                yield return new WaitForSeconds(kind == ProjKind.Bomb ? 2.5f : 4.5f);
            }

            mod.DevSetSmartCursor(false);
            foreach (var p in placed) TerraWorld.Set(p.x, p.y, CellKind.Empty);
            mod.DevNoFallDamage = false;
        }

        /// <summary>Walks one way for a while, checking the feet against the stair surface each physics step.</summary>
        IEnumerator Walk(HeroController hero, BoxCollider2D col, int dir, float seconds, string what)
        {
            float worst = 0f, startY = col.bounds.min.y, startX = col.bounds.center.x;
            int steps = 0, onStair = 0, airborne = 0;
            var trace = new System.Text.StringBuilder();
            Player.TerraMovement.DevMove = dir;
            for (float t = 0f; t < seconds; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                steps++;
                if (Player.StairCollision.DevSurface is float s)
                {
                    onStair++;
                    worst = Mathf.Max(worst, Mathf.Abs(col.bounds.min.y - s));
                    if (trace.Length < 600)
                    {
                        var hit = Physics2D.Raycast(col.bounds.center, Vector2.down, col.bounds.extents.y + 0.16f, 256);
                        trace.Append($"[着地{(hero.cState.onGround ? 1 : 0)} 落{(hero.cState.falling ? 1 : 0)} vy{hero.GetComponent<Rigidbody2D>().linearVelocity.y:F2} 脚-坡{(col.bounds.min.y - s) * 32f:F1} 射线{(hit.collider != null ? hit.collider.name : "无")}{(hit.collider != null ? (hit.point.y - col.bounds.min.y).ToString("F2") : "")} {Player.StairCollision.DevLedge}]");
                    }
                }
                if (!hero.cState.onGround) airborne++;
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.3f);
            Plugin.Log.LogInfo($"[AutoTest] 平台：{what} {seconds}s：x {startX:F2} -> {col.bounds.center.x:F2}，脚 {startY:F2} -> {col.bounds.min.y:F2}；" +
                               $"{steps} 步里 {onStair} 步在楼梯上，脚和坡面最大差 {worst * 32f:F1} px，不着地 {airborne} 步；逐步：{trace}");
        }
    }
}
