using System.Collections;
using System.Linq;
using System.Text;
using TerraKnight.EnemyAi.Bosses;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "ggfkprobe"): Godhome's False Knight hall as the new AI sees it: the arena it takes, the room's rock by
    /// its grid and its colliders down a few columns, and the knight's place and state from his entrance on (the player
    /// left standing at the door). TK_GGFK_SCENE another hall.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GgFkProbe()
        {
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            string hall = System.Environment.GetEnvironmentVariable("TK_GGFK_SCENE") ?? "GG_False_Knight";
            string record = System.Environment.GetEnvironmentVariable("TK_GGFK_RECORD") ?? "FalseKnight";
            yield return GoTo(hall, "door_dreamEnter");
            BossKit kit = null;
            for (float until = Time.realtimeSinceStartup + 20f; Time.realtimeSinceStartup < until && kit == null;)
            {
                kit = Object.FindObjectsByType<BossKit>(FindObjectsSortMode.None).FirstOrDefault(k => k.Record == record);
                yield return null;
            }
            var hero = HeroController.instance;
            // The first three seconds: where the boss's bodies are and their states (its entrance).
            for (float t0 = 0f; t0 < 3f; t0 += 0.1f)
            {
                var sb0 = new StringBuilder();
                foreach (var k in Object.FindObjectsByType<BossKit>(FindObjectsSortMode.None).Where(k => k.Record == record))
                {
                    var f0 = k.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "FalseyControl") ?? k.GetComponent<PlayMakerFSM>();
                    var c0 = k.GetComponent<Collider2D>();
                    var r0 = k.GetComponent<Rigidbody2D>();
                    sb0.Append($" {k.name}:{(Vector2)k.transform.position:F1} {f0?.ActiveStateName} 碰撞{(c0 != null ? (c0.enabled ? (c0.isTrigger ? "触发" : "开") : "关") : "-")} 运动学{(r0 != null && r0.isKinematic)} 层{k.gameObject.layer}");
                }
                Plugin.Log.LogInfo($"[GGFK] 入场 t={t0:F1}{sb0}");
                yield return new WaitForSeconds(0.1f);
            }
            // The room's colliders over its open middle (the roof): what they are and whose.
            var area = new Rect(10f, 38f, 50f, 25f);
            foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            {
                if (c == null || !c.enabled || c.gameObject.layer != 8 || !c.bounds.Intersects(new Bounds(area.center, area.size))) continue;
                string kind = c is EdgeCollider2D ? "边" : c is PolygonCollider2D ? "多边形" : c is CompositeCollider2D cc ? "复合(" + cc.geometryType + ")" : c is BoxCollider2D ? "盒" : c.GetType().Name;
                bool mine = World.ChunkColliders.Mirrors.Contains(c) || World.ChunkColliders.IsBuilt(c);
                Plugin.Log.LogInfo($"[GGFK] 碰撞体 {c.name}（{kind}{(c.isTrigger ? " 触发" : "")}{(c.usedByEffector ? " 单向" : "")}{(mine ? " mod" : " 原版")}）{c.bounds.min:F1}~{c.bounds.max:F1}");
            }
            foreach (var k in Object.FindObjectsByType<BossKit>(FindObjectsSortMode.None).Where(k => k.Record == record))
                Plugin.Log.LogInfo($"[GGFK] 套件 {k.name} 在 {(Vector2)k.transform.position:F1} 交战 {k.Engaged} 场地 {k.Arena.xMin:F1}~{k.Arena.xMax:F1}×{k.Arena.yMin:F1}~{k.Arena.yMax:F1}");
            kit = Object.FindObjectsByType<BossKit>(FindObjectsSortMode.None).Where(k => k.Record == record).OrderByDescending(k => k.Engaged).ThenBy(k => k.transform.position.y).FirstOrDefault() ?? kit;
            float x0 = float.TryParse(System.Environment.GetEnvironmentVariable("TK_GGFK_X0"), out var gx0) ? gx0 : 14f, x1 = float.TryParse(System.Environment.GetEnvironmentVariable("TK_GGFK_X1"), out var gx1) ? gx1 : 56f;
            float y1 = float.TryParse(System.Environment.GetEnvironmentVariable("TK_GGFK_Y1"), out var gy1) ? gy1 : 60f, y0 = y1 - 42f;
            Plugin.Log.LogInfo($"[GGFK] 主角 {(Vector2)hero.transform.position:F1}，首领 {(kit != null ? ((Vector2)kit.transform.position).ToString("F1") : "无")}");
            for (float x = x0; x <= x1; x += (x1 - x0) / 10f)
            {
                var sb = new StringBuilder();
                for (float y = y1; y >= y0; y -= 0.5f)
                {
                    var c = World.CellGrid.WorldToCell(new Vector2(x, y));
                    var k = World.TerraWorld.Get(c.x, c.y);
                    bool native = EnemyAi.Hostiles.NativeSolidAt(new Vector2(x, y));
                    sb.Append(native ? '#' : k == World.CellKind.Original ? 'X' : k == World.CellKind.Empty ? '.' : 'o');
                }
                Plugin.Log.LogInfo($"[GGFK] 列 x={x:F0} y{y1:F0}→{y0:F0}: {sb}");
            }
            var rays = new StringBuilder();
            for (float x = x0; x <= x1; x += (x1 - x0) / 20f)
            {
                var up = EnemyAi.Hostiles.NativeRay(new Vector2(x, y0 + 12f), Vector2.up, 40f);
                var down = EnemyAi.Hostiles.NativeRay(new Vector2(x, y1 - 10f), Vector2.down, 40f);
                rays.Append($" x{x:F0}:↑{(up.collider != null ? up.point.y.ToString("F1") : "-")}↓{(down.collider != null ? down.point.y.ToString("F1") : "-")}");
            }
            Plugin.Log.LogInfo($"[GGFK] 射线（自 y{y0 + 12f:F0} 向上 / 自 y{y1 - 10f:F0} 向下）:{rays}");
            if (kit == null) yield break;
            var fsm = kit.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "FalseyControl") ?? kit.GetComponent<PlayMakerFSM>();
            for (float t = 0f; t < 12f && kit != null; t += 0.5f)
            {
                var col = kit.GetComponent<Collider2D>();
                var rb = kit.GetComponent<Rigidbody2D>();
                Plugin.Log.LogInfo($"[GGFK] t={t:F2} 场地 {kit.Arena.xMin:F1}~{kit.Arena.xMax:F1}×{kit.Arena.yMin:F1}~{kit.Arena.yMax:F1} 交战 {kit.Engaged} 状态 {fsm?.ActiveStateName} " +
                    $"位置 {(col != null ? ((Vector2)col.bounds.center).ToString("F1") : "-")} 碰撞 {(col != null && col.enabled)} 速度 {(rb != null ? rb.linearVelocity.ToString("F1") : "-")} 重力 {(rb != null ? rb.gravityScale : 0f):F2}");
                yield return new WaitForSeconds(0.25f);
            }
        }
    }
}
