using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "mossidle"): Greenpath's Fungus1_10 left alone for four minutes, the player standing at the entrance
    /// out of every enemy's way: which enemies fall out of the room (below it, or dead with no blow) with nobody touching
    /// them. Run on a build to compare with another.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MossIdleTest()
        {
            if (!DevCopy.Verified) yield break;
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            yield return GoTo("Fungus1_10", EntryGateOf("Fungus1_10"));
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            var at = hero.transform.position;
            var watched = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).Where(h => !h.isDead).ToList();
            var startY = watched.ToDictionary(h => h, h => h.transform.position.y);
            var fell = new List<string>();
            var gone = new HashSet<HealthManager>();
            // The ledge Moss Walker (3) rests on: its body, and what solid lies round it.
            foreach (var h in watched.Where(x => x.name.StartsWith("Moss Walker (3)")))
            {
                var c = h.GetComponent<Collider2D>();
                var rb = h.GetComponent<Rigidbody2D>();
                Plugin.Log.LogInfo($"[AutoTest] 苔藓闲置 {h.name} 身体 {c?.GetType().Name} {c?.bounds.min:F2}..{c?.bounds.max:F2} 材质 {(c?.sharedMaterial != null ? c.sharedMaterial.friction.ToString("F2") : "无")}/{(rb?.sharedMaterial != null ? rb.sharedMaterial.friction.ToString("F2") : "无")} {rb?.bodyType} 重力 {rb?.gravityScale}");
                var p = h.transform.position;
                for (float dx = -2f; dx <= 2.01f; dx += 0.5f)
                {
                    var hit = Physics2D.Raycast(new Vector2(p.x + dx, p.y + 0.5f), Vector2.down, 3f, 1 << 8);
                    var cell = World.CellGrid.WorldToCell(new Vector2(p.x + dx, p.y - 0.8f));
                    Plugin.Log.LogInfo($"[AutoTest] 苔藓闲置 x{p.x + dx:F1}：地面 {(hit.collider != null ? $"{hit.collider.name}({hit.collider.GetType().Name},摩擦 {(hit.collider.sharedMaterial != null ? hit.collider.sharedMaterial.friction.ToString("F2") : "无")}) y{hit.point.y:F2}" : "无")}，格子实心 {World.TerraWorld.IsSolid(cell.x, cell.y)}");
                }
            }
            // TK_MOSS_WAKE: the Moss Walkers woken and roaming, as when the player has passed them.
            bool wake = System.Environment.GetEnvironmentVariable("TK_MOSS_WAKE") == "1";
            if (wake)
                foreach (var h in watched)
                    foreach (var f in h.GetComponents<PlayMakerFSM>())
                        if (f.FsmName == "Moss Walker") f.SendEvent("WAKE");
            Plugin.Log.LogInfo($"[AutoTest] 苔藓闲置 开始{(wake ? "（叫醒苔藓行者）" : "")}：{watched.Count} 只敌人 {string.Join("、", watched.Select(h => h.name))}");
            // Each walker's last few seconds: where it was, its state, its speed and what its rays saw.
            var trail = new Dictionary<HealthManager, List<string>>();
            string Sample(HealthManager h)
            {
                var rb = h.GetComponent<Rigidbody2D>();
                var f = h.GetComponents<PlayMakerFSM>().FirstOrDefault(x => x.FsmName == "Moss Walker");
                string rays = f == null ? "" : $" 边{(f.FsmVariables.FindFsmBool("Edge Range")?.Value == true ? 1 : 0)}墙{(f.FsmVariables.FindFsmBool("Wall Range")?.Value == true ? 1 : 0)}";
                var p = h.transform.position;
                return $"({p.x:F1},{p.y:F1}) {f?.ActiveStateName} v({rb?.linearVelocity.x:F1},{rb?.linearVelocity.y:F1}) g{rb?.gravityScale}{(rb != null && rb.bodyType != RigidbodyType2D.Dynamic ? " " + rb.bodyType : "")}{rays}";
            }
            for (float t = 0f; t < 240f; t += 0.2f)
            {
                hero.transform.position = at;
                foreach (var h in watched)
                {
                    if (gone.Contains(h)) continue;
                    if (h == null || h.isDead) { gone.Add(h); fell.Add((h != null ? h.name : "?") + " 死了"); continue; }
                    if (!trail.TryGetValue(h, out var list)) trail[h] = list = new List<string>();
                    list.Add($"{t:F1}s {Sample(h)}");
                    if (list.Count > 40) list.RemoveAt(0);
                    if (h.transform.position.y < startY[h] - 8f && h.transform.position.y < -5f)
                    {
                        gone.Add(h);
                        var rb = h.GetComponent<Rigidbody2D>();
                        fell.Add($"{h.name} 掉到 {h.transform.position.y:F0}（起点 {startY[h]:F1}，重力 {rb?.gravityScale}）");
                        Plugin.Log.LogInfo($"[AutoTest] 苔藓闲置 {h.name} 掉落前：{string.Join(" | ", list)}");
                        // What lies under the last place it stood: the colliders a ray down from there meets.
                        var hits = Physics2D.RaycastAll(new Vector2(h.transform.position.x, startY[h] + 1f), Vector2.down, 40f);
                        Plugin.Log.LogInfo($"[AutoTest] 苔藓闲置 {h.name} 起点正下方：{string.Join("；", hits.Select(x => $"{x.collider.name}({LayerMask.LayerToName(x.collider.gameObject.layer)}{(x.collider.isTrigger ? " 触发" : "")}) y{x.point.y:F1}"))}");
                    }
                }
                yield return new WaitForSeconds(0.2f);
            }
            Plugin.Log.LogInfo($"[AutoTest] 苔藓闲置 结果：掉出房间 {fell.Count} 只 {string.Join("；", fell)}");
            pd.isInvincible = false;
            Player.TerraStats.DevImmortal = false;
        }
    }
}
