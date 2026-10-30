using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.EnemyAi.Bosses;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "dungstuck", the user's 10-29 "神居的粪虫防御者会卡在地里"): the Dung Defender in Godhome's hall, the new AI
    /// then the game's (TK_DUNG_SECONDS each), the player put somewhere new every 3 s (on the floor across his tunnels' span,
    /// or held up high, which raises his eruption). Every frame his own script's state: under ground (dived, tunnelling) over
    /// 6 s, falling after an eruption over 3 s, or his body's bottom well under the floor outside those, each logged with the
    /// frames before it. Lines "[AutoTest] DungStuck …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly HashSet<string> dungUnder = new HashSet<string> { "Dive In 1", "Dive In 2", "Underground", "Erupt Tunneling", "Tunneling L", "Tunneling R", "Set Min X", "Set Max X", "Erupt Antic", "Erupt Antic R", "Wake", "Rage In", "Sleep", "GG Pause", "Rage HP", "Init" };

        IEnumerator DungStuckTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            Player.TerraStats.DevImmortal = true; mod.DevNoFallDamage = true;
            float seconds = float.TryParse(Environment.GetEnvironmentVariable("TK_DUNG_SECONDS"), out var s) ? s : 180f;
            int problems = 0;
            foreach (var kind in new[] { World.EnemyAiKind.New, World.EnemyAiKind.Vanilla })
            {
                World.EnemyAiMode.Current = kind;
                string ai = kind == World.EnemyAiKind.New ? "新版AI" : "原版AI";
                yield return GoTo("GG_Workshop", "left1");
                yield return GoTo("GG_Dung_Defender", "door_dreamEnter");
                PlayerData.instance.isInvincible = true;
                HealthManager hm = null;
                float until = Time.realtimeSinceStartup + 30f;
                while (hm == null && Time.realtimeSinceStartup < until)
                {
                    hm = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(h => h != null && !h.isDead && EnemyStats.Of(h)?.Record == "DungDefender");
                    if (hm == null) yield return new WaitForSeconds(0.2f);
                }
                if (hm == null) { Plugin.Log.LogInfo($"[AutoTest] DungStuck {ai} 没有粪虫防御者"); continue; }
                var fsm = hm.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Dung Defender");
                var hero = HeroController.instance;
                var hb = hero.GetComponent<Rigidbody2D>();
                float minX = fsm?.FsmVariables.FindFsmFloat("Min X")?.Value ?? 62f, maxX = fsm?.FsmVariables.FindFsmFloat("Max X")?.Value ?? 90f;
                float centre = fsm?.FsmVariables.FindFsmFloat("Centre X")?.Value ?? 76f;
                var down = EnemyAi.Hostiles.NativeRay(new Vector2(centre, 20f), Vector2.down, 40f);
                float floor = down.collider != null ? down.point.y : 7.5f;
                var kit = Object.FindObjectsByType<BossKit>(FindObjectsSortMode.None).FirstOrDefault(k => k.Record == "DungDefender");
                Plugin.Log.LogInfo($"[AutoTest] DungStuck {ai}：地面 y {floor:F2}（{down.collider?.name}），隧道 x {minX:F1}~{maxX:F1}，新版AI场地 {(kit != null ? kit.Arena.ToString() : "无")}");
                // What stands under the hall's floor: every ground collider straight down at a few places, and whether his
                // body meets it.
                var mine = hm.GetComponents<Collider2D>().Where(c => !c.isTrigger).ToArray();
                Plugin.Log.LogInfo($"[AutoTest] DungStuck 碰撞检测 {hm.GetComponent<Rigidbody2D>()?.collisionDetectionMode}");
                Plugin.Log.LogInfo($"[AutoTest] DungStuck 他的碰撞体：{string.Join("，", mine.Select(c => $"{c.GetType().Name} 层{c.gameObject.layer} {c.bounds.min:F2}~{c.bounds.max:F2} 开 {c.enabled}"))}，刚体 {hm.GetComponent<Rigidbody2D>()?.bodyType}，层碰地形 {!Physics2D.GetIgnoreLayerCollision(hm.gameObject.layer, 8)}");
                foreach (float px in new[] { minX + 2f, centre, maxX - 2f })
                {
                    var hits = Physics2D.RaycastAll(new Vector2(px, 14f), Vector2.down, 24f, 1 << 8);
                    Plugin.Log.LogInfo($"[AutoTest] DungStuck x {px:F1} 往下：{string.Join("；", hits.Select(h => $"{h.collider.name}/{h.collider.GetType().Name.Replace("Collider2D", "")}{(h.collider.isTrigger ? "(触发)" : "")}{(h.collider.usedByEffector ? "(单向)" : "")}{(World.ChunkColliders.IsBuilt(h.collider) ? "(建)" : "")} y {h.point.y:F2} 他忽略 {(mine.Length > 0 && Physics2D.GetIgnoreCollision(mine[0], h.collider))}"))}");
                    var cells = new System.Text.StringBuilder();
                    for (float y = 8f; y >= 3f; y -= 0.5f)
                    {
                        var c = World.CellGrid.WorldToCell(new Vector2(px, y + 0.01f));
                        cells.Append($" {y:F1}:{World.TerraWorld.Get(c.x, c.y)}");
                    }
                    Plugin.Log.LogInfo($"[AutoTest] DungStuck x {px:F1} 格子：{cells}");
                }
                var trail = new List<string>();
                string state = null, flagged = null;
                float since = Time.time, t0 = Time.time, nextSpot = 0f;
                int spot = 0;
                Vector2 held = Vector2.zero;
                bool hold = false;
                var counts = new Dictionary<string, int>();
                float longestUnder = 0f, longestFall = 0f;
                while (Time.time - t0 < seconds && hm != null && !hm.isDead)
                {
                    yield return null;
                    if (EnemyStats.Of(hm) is EnemyStats.Info info && hm.hp < info.MaxHp * 0.3f) hm.hp = Mathf.RoundToInt(info.MaxHp * 0.6f);
                    mod.Stats.FullHeal();
                    PlayerData.instance.isInvincible = true;
                    if (Time.time >= nextSpot)
                    {
                        nextSpot = Time.time + 3f;
                        float x = Mathf.Lerp(minX + 1f, maxX - 1f, ((spot * 37) % 100) / 100f);
                        hold = spot % 3 == 2;
                        held = new Vector2(x, floor + (hold ? 6f : 1.3f));
                        spot++;
                        hero.transform.position = new Vector3(held.x, held.y, hero.transform.position.z);
                        if (hb != null) hb.linearVelocity = Vector2.zero;
                    }
                    else if (hold)
                    {
                        hero.transform.position = new Vector3(held.x, held.y, hero.transform.position.z);
                        if (hb != null) hb.linearVelocity = Vector2.zero;
                    }
                    string now = fsm != null ? fsm.ActiveStateName : "?";
                    var body = hm.GetComponents<Collider2D>().Where(c => c.enabled && !c.isTrigger).OrderByDescending(c => c.bounds.size.y).FirstOrDefault()
                        ?? hm.GetComponent<Collider2D>();
                    float bottom = body != null ? body.bounds.min.y : hm.transform.position.y;
                    trail.Add($"{Time.time - t0:F2}s {now} 位置 ({hm.transform.position.x:F2},{hm.transform.position.y:F2}) 脚底 {bottom:F2} 速度 {hm.GetComponent<Rigidbody2D>()?.linearVelocity:F1}");
                    if (trail.Count > 16) trail.RemoveAt(0);
                    if (now != state)
                    {
                        if (state != null)
                        {
                            float d = Time.time - since;
                            if (dungUnder.Contains(state)) longestUnder = Mathf.Max(longestUnder, d);
                            if (state == "Erupt Fall") longestFall = Mathf.Max(longestFall, d);
                        }
                        state = now;
                        since = Time.time;
                        // Standing (landed, idle, unrolled): where his picture's bottom is against the floor.
                        if (now == "Idle" || now == "Erupt Land" || now == "RJ Unroll" || now == "Roar?")
                        {
                            var mr = hm.GetComponent<MeshRenderer>();
                            Plugin.Log.LogInfo($"[AutoTest] DungStuck {ai} 站立 {now}：位置 y {hm.transform.position.y:F2}，图底 {(mr != null ? mr.bounds.min.y : float.NaN):F2}，碰撞盒底 {bottom:F2}，地面 {floor:F2}，刚体 {hm.GetComponent<Rigidbody2D>()?.bodyType}");
                        }
                        counts[now] = (counts.TryGetValue(now, out int c) ? c : 0) + 1;
                        flagged = null;
                    }
                    float took = Time.time - since;
                    string why = dungUnder.Contains(now) && now != "Sleep" && took > 6f ? $"在地下 {took:F1} 秒"
                        : now == "Erupt Fall" && took > 3f ? $"喷发后下落 {took:F1} 秒没落地"
                        : !dungUnder.Contains(now) && bottom < floor - 1f ? $"脚底 {bottom:F2} 在地面 {floor:F2} 以下"
                        : null;
                    if (why != null && flagged != why.Substring(0, 2))
                    {
                        flagged = why.Substring(0, 2);
                        problems++;
                        Plugin.Log.LogInfo($"[AutoTest] DungStuck {ai} 卡住：{why}（状态 {now}，玩家 ({hero.transform.position.x:F1},{hero.transform.position.y:F1})），此前几帧：\n    {string.Join("\n    ", trail)}");
                        if (problems <= 4) Shot("GG_Dung_Defender", $"dungstuck_{problems}");
                    }
                }
                Plugin.Log.LogInfo($"[AutoTest] DungStuck {ai}：{seconds:F0} 秒，最长在地下 {longestUnder:F1} 秒，最长喷发下落 {longestFall:F1} 秒，钻地 {(counts.TryGetValue("Underground", out int u) ? u : 0)} 次，喷发 {(counts.TryGetValue("Erupt Land", out int l) ? l : 0)} 次落地，脱困 {BossAi.DevUnstuck}");
                if (hm != null) hm.hp = 1;
                yield return new WaitForSeconds(1f);
            }
            Plugin.Log.LogInfo($"[AutoTest] DungStuck 合计卡住 {problems} 次：{(problems == 0 ? "通过" : "不通过")}");
        }
    }
}
