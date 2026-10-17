using System.Collections;
using System.Text;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_quirrelcity"): Quirrel's spot in the City of Tears. The City's rooms looked through for him (his
    /// npc_control, active or not) and for a bench; where he is found: the colliders under him and the bench (layer,
    /// trigger, effector, on or off), the grid's cells (# solid, p platform, a hand-placed art collider), then the Knight
    /// dropped onto the spot from above: does he stand on it, and where.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator QuirrelCityTest()
        {
            mod.DevNoFallDamage = true;
            foreach (var scene in new[] { "Ruins1_02", "Ruins1_03", "Ruins1_01", "Ruins1_05", "Ruins1_17", "Ruins1_18", "Ruins1_23", "Ruins1_25", "Ruins1_27", "Ruins1_28", "Ruins1_29", "Ruins2_01", "Ruins2_03" })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                yield return new WaitForSeconds(1.5f);
                PlayerData.instance.isInvincible = true;
                if (GameManager.instance.sceneName != scene) continue;
                PlayMakerFSM q = null, bench = null;
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (f.gameObject.scene.name != scene) continue;
                    if (f.FsmName == "npc_control" && f.gameObject.name.Contains("Quirrel")) q = f;
                    if (f.FsmName == "Bench Control") bench = f;
                }
                Plugin.Log.LogInfo($"[AutoTest] 修复1016 奎若泪城 {scene}：奎若 {(q != null ? PathOf(q.transform) + " " + ((Vector2)q.transform.position).ToString("F1") + " 激活 " + q.gameObject.activeInHierarchy : "无")}，长椅 {(bench != null ? PathOf(bench.transform) + " " + ((Vector2)bench.transform.position).ToString("F1") : "无")}");
                if (q == null) continue;
                Vector2 at = q.transform.position;
                float best = 0f;
                foreach (var r in q.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    float a2 = r.bounds.size.x * r.bounds.size.y;
                    if (a2 > best && a2 < 60f) { best = a2; at = new Vector2(r.bounds.center.x, r.bounds.min.y); }
                }
                var sb = new StringBuilder();
                var near = new Rect(at.x - 6f, at.y - 4f, 12f, 7f);
                foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (c.gameObject.scene.name != scene) continue;
                    var b = c.bounds;
                    if (b.max.x < near.xMin || b.min.x > near.xMax || b.max.y < near.yMin || b.min.y > near.yMax || b.size.x > 60f) continue;
                    sb.Append($"\n    碰撞 {PathOf(c.transform)} {c.GetType().Name} 层{c.gameObject.layer} 触发{(c.isTrigger ? 1 : 0)} 单向{(c.usedByEffector ? 1 : 0)} 开{(c.enabled && c.gameObject.activeInHierarchy ? 1 : 0)} {(Vector2)b.min:F1}..{(Vector2)b.max:F1}{FsmStates(c.gameObject, false)}");
                }
                var grid = new StringBuilder();
                const float cs = CellGrid.CellSize;
                for (float y = near.yMax; y >= near.yMin; y -= cs)
                {
                    grid.Append($"\n    y {y,6:F1} ");
                    for (float x = near.xMin; x <= near.xMax; x += cs)
                    {
                        var cell = CellGrid.WorldToCell(new Vector2(x + cs / 2f, y - cs / 2f));
                        var rr = TerraWorld.RegionAt(cell.x, cell.y);
                        bool art = rr != null && rr.Grid.IsArt(cell.x - rr.CellOffset.x, cell.y - rr.CellOffset.y);
                        bool isQ = Mathf.Abs(x - at.x) < cs && Mathf.Abs(y - at.y) < cs;
                        grid.Append(isQ ? 'Q' : TerraWorld.IsPlatform(cell.x, cell.y) ? 'p' : art ? 'a' : TerraWorld.IsSolid(cell.x, cell.y) ? '#' : '.');
                    }
                }
                Plugin.Log.LogInfo($"[AutoTest] 修复1016 奎若泪城 {scene} 奎若脚下 {at:F1}：{sb}\n  网格（x {near.xMin:F0}..{near.xMax:F0}，# 实心，p 平台，a 手放碰撞体，Q 奎若）{grid}");
                // Dropped onto his spot and either side of it.
                var hero = HeroController.instance;
                var col = hero.GetComponent<BoxCollider2D>();
                foreach (float dx in new[] { -2f, 0f, 2f })
                {
                    float offset = hero.transform.position.y - col.bounds.min.y;
                    hero.transform.position = new Vector3(at.x + dx, at.y + 2.5f + offset, hero.transform.position.z);
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(1.2f);
                    float feet = col.bounds.min.y;
                    bool stands = hero.cState.onGround && Mathf.Abs(feet - at.y) < 0.6f;
                    FxCheck($"{scene} 奎若那里站得住（偏 {dx:+0;-0;0}）", stands, $"脚 {feet:F2}，奎若脚下 {at.y:F2}，着地 {hero.cState.onGround}；{PlatformState(hero)}");
                    if (!stands) { Shot(scene, $"fx_quirrelcity_{dx:+0;-0;0}"); yield return null; }
                }
                Player.TerraCamera.DevLook = at;
                yield return new WaitForSeconds(0.4f);
                Shot(scene, "fx_quirrelcity");
                yield return null;
                Player.TerraCamera.DevLook = null;
                break;
            }
        }
    }
}
