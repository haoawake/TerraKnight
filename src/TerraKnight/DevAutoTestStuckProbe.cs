using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "stuckprobe", the user's 10-26 "从天上掉下来或者走路的时候会突然卡住脚无法移动，要跳一下或者勾爪才能恢复"):
    /// in Dirtmouth, King's Pass and the Crossroads' first room, the Knight walks the floor end to end both ways, then is
    /// dropped from the air every 1.5 units along it and walks off each landing. A stall: held to one side on the ground (or
    /// barely off it) for a quarter second without getting on while no solid cell stands ahead at body height. Each stall
    /// logged with its cell, whether it is on a chunk's edge, the colliders at the front foot and the hero's states.
    /// TK_SP_SCENES=Scene:gate,...: other rooms. Lines "[AutoTest] 卡脚 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator StuckProbeTest()
        {
            var hero = HeroController.instance;
            Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            var places = new List<(string scene, string gate)> { ("Town", "left1"), ("Tutorial_01", "right1"), ("Crossroads_01", "top1") };
            var env = System.Environment.GetEnvironmentVariable("TK_SP_SCENES");
            if (!string.IsNullOrEmpty(env))
            {
                places.Clear();
                foreach (var s in env.Split(',')) { var p = s.Split(':'); if (p.Length == 2) places.Add((p[0], p[1])); }
            }
            int stalls = 0, landings = 0, stuckLandings = 0;
            var rb = hero.GetComponent<Rigidbody2D>();
            var box = hero.GetComponent<BoxCollider2D>();
            const float c = CellGrid.CellSize;
            bool WallAhead(int dir)
            {
                var b = box.bounds;
                float x = dir > 0 ? b.max.x + 0.08f : b.min.x - 0.08f;
                for (float y = b.min.y + c * 0.6f; y <= b.max.y; y += c * 0.5f)
                    if (TerraWorld.IsSolidAt(new Vector2(x, y))) return true;
                return false;
            }
            string Describe(int dir)
            {
                var b = box.bounds;
                var foot = new Vector2(dir > 0 ? b.max.x : b.min.x, b.min.y);
                var cell = CellGrid.WorldToCell(foot);
                var cols = new List<string>();
                foreach (var col in Physics2D.OverlapBoxAll(foot + new Vector2(dir * 0.1f, 0.05f), new Vector2(0.4f, 0.3f), 0f))
                    if (col != null && !col.isTrigger && col.GetComponentInParent<HeroController>() == null)
                        cols.Add($"{PathOf(col.transform)}[{col.GetType().Name} 层{col.gameObject.layer}]");
                var cs = hero.cState;
                return $"脚 {foot:F2} 格 ({cell.x},{cell.y}) 区块边 {(cell.x % ChunkColliders.ChunkCells == 0 || (cell.x + 1) % ChunkColliders.ChunkCells == 0 ? "是" : "否")}，速度 {rb.velocity:F2}，着地 {cs.onGround} 后坐 {cs.recoiling} 攻击 {cs.attacking} 冲刺 {Player.TerraMovement.IsDashing} 可操作 {GameHooks.PlayerHasControl()}，前脚碰到 {(cols.Count == 0 ? "无" : string.Join("、", cols.Distinct()))}";
            }
            foreach (var (scene, gate) in places)
            {
                yield return GoTo(scene, gate);
                yield return new WaitForSeconds(1.5f);
                for (float t = Time.realtimeSinceStartup + 5f; !hero.cState.onGround && Time.realtimeSinceStartup < t;) yield return null;
                // The walks, right then left, as far as they go.
                foreach (int dir in new[] { 1, -1 })
                {
                    Player.TerraMovement.DevMove = dir;
                    float lastX = hero.transform.position.x, since = Time.time, until = Time.time + 25f;
                    while (Time.time < until)
                    {
                        yield return null;
                        float x = hero.transform.position.x;
                        if (Mathf.Abs(x - lastX) > 0.03f) { lastX = x; since = Time.time; continue; }
                        if (Time.time - since < 0.25f) continue;
                        if (WallAhead(dir) || GameManager.instance.IsInSceneTransition || GameManager.instance.sceneName != scene) break;
                        stalls++;
                        Plugin.Log.LogInfo($"[AutoTest] 卡脚 走路 {scene} 往{(dir > 0 ? "右" : "左")}：{Describe(dir)}");
                        Shot(scene, $"stuck_walk_{stalls}");
                        // Over it (as a jump would): on along the floor.
                        hero.transform.position += new Vector3(dir * 0.3f, 0.4f, 0f);
                        since = Time.time;
                        lastX = hero.transform.position.x;
                        if (stalls > 30) break;
                    }
                    Player.TerraMovement.DevMove = 0;
                    if (GameManager.instance.sceneName != scene) { yield return GoTo(scene, gate); yield return new WaitForSeconds(1f); }
                }
                // The drops: from the air every 1.5 units along the floor, then a walk off the landing either way.
                var region = TerrainSystem.Find(scene);
                var zone = region?.OwnZone;
                float x0 = zone.HasValue ? zone.Value.xMin + 2f : hero.transform.position.x - 20f, x1 = zone.HasValue ? zone.Value.xMax - 2f : hero.transform.position.x + 20f;
                float floorY = hero.transform.position.y;
                for (float x = x0; x <= x1; x += 1.5f)
                {
                    // Open air three to six units over the floor there.
                    var hit = Physics2D.Raycast(new Vector2(x, floorY + 6f), Vector2.down, 12f, 1 << ChunkColliders.TerrainLayer);
                    if (hit.collider == null) continue;
                    var drop = new Vector2(x, hit.point.y + 4f);
                    if (TerraWorld.IsSolidAt(drop) || TerraWorld.IsSolidAt(drop + Vector2.up)) continue;
                    hero.transform.position = new Vector3(drop.x, drop.y, hero.transform.position.z);
                    rb.velocity = Vector2.zero;
                    yield return null;
                    for (float t = Time.time + 3f; Time.time < t && !hero.cState.onGround;) yield return null;
                    yield return new WaitForSeconds(0.15f);
                    landings++;
                    bool stuck = false;
                    foreach (int dir in new[] { 1, -1 })
                    {
                        if (WallAhead(dir)) continue;
                        float start = hero.transform.position.x;
                        Player.TerraMovement.DevMove = dir;
                        yield return new WaitForSeconds(0.35f);
                        Player.TerraMovement.DevMove = 0;
                        float moved = Mathf.Abs(hero.transform.position.x - start);
                        if (moved < 0.1f && !WallAhead(dir))
                        {
                            stuck = true;
                            Plugin.Log.LogInfo($"[AutoTest] 卡脚 落地 {scene} x={x:F1} 往{(dir > 0 ? "右" : "左")}走不动（{moved:F2}）：{Describe(dir)}");
                            Shot(scene, $"stuck_land_{landings}");
                        }
                        yield return new WaitForSeconds(0.1f);
                    }
                    if (stuck) stuckLandings++;
                    if (GameManager.instance.sceneName != scene) break;
                }
                Plugin.Log.LogInfo($"[AutoTest] 卡脚 {scene} 小结：走路卡住 {stalls} 次（累计），落地 {landings} 次（累计），落地后走不动 {stuckLandings} 次（累计）");
            }
            Player.TerraMovement.DevMove = 0;
            Plugin.Log.LogInfo($"[AutoTest] 卡脚 合计：走路卡住 {stalls} 次，落地 {landings} 次里走不动 {stuckLandings} 次 {(stalls == 0 && stuckLandings == 0 ? "通过" : "不通过")}");
        }
    }
}
