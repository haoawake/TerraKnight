using System.Collections;
using UnityEngine;
using TerraKnight.World;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "respawn"): the enemies of the Forgotten Crossroads' first room come back each
    /// after its own wait: a kill away from the player shows its ghost on the spot before it is
    /// back, and one killed with the player standing on its spot waits until he has gone.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator RespawnTest()
        {
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            const string room = "Crossroads_01";
            yield return GoTo(room, EntryGateOf(room));
            yield return new WaitForSeconds(1.5f);
            var resp = EnemyRespawn.Instance;
            Plugin.Log.LogInfo($"[AutoTest] 刷新2 {room} 各刷新点的等待：{resp.DevDelays(room)}");
            var hero = HeroController.instance;
            Vector2 heroAt = hero.transform.position;
            HealthManager far = null, near = null;
            foreach (var hm in resp.DevLive(room))
            {
                float d = Vector2.Distance(hm.transform.position, heroAt);
                if (d > 8f && (far == null || d < Vector2.Distance(far.transform.position, heroAt))) far = hm;
                else if (d <= 8f || near == null) near = near ?? hm;
            }
            if (far == null || near == null || far == near)
            {
                Plugin.Log.LogInfo($"[AutoTest] 刷新2 找不到两只可以试的怪（远的 {far != null}，近的 {near != null}），不通过");
                yield break;
            }
            EnemyRespawn.DevFixedSeconds = 6f;

            // One far off: its ghost a few seconds before it is back, then it.
            Vector2 farAt = far.transform.position;
            string farName = far.name;
            int back0 = EnemyRespawn.DevRespawned;
            Kill(far);
            yield return new WaitForSeconds(4.2f);
            int ghosts = resp.DevGhosts;
            Player.TerraCamera.DevLook = farAt;
            yield return new WaitForSeconds(0.3f);
            Shot(room, "respawn_ghost");
            yield return null;
            Player.TerraCamera.DevLook = null;
            yield return new WaitForSeconds(2.5f);
            int back1 = EnemyRespawn.DevRespawned - back0;
            Plugin.Log.LogInfo($"[AutoTest] 刷新2 打死 {farName}（{farAt:F1}，离主角远）：4 秒时虚影 {ghosts} 个{(ghosts > 0 ? "（对）" : "（没有，不通过）")}，7 秒时刷出 {back1} 只，虚影剩 {resp.DevGhosts} 个{(back1 > 0 && resp.DevGhosts == 0 ? " 通过" : " 不通过")}");

            // One with the player standing on its spot: it waits until he goes.
            Vector2 nearAt = resp.DevHome(near) ?? (Vector2)near.transform.position;
            string nearName = near.name;
            back0 = EnemyRespawn.DevRespawned;
            Kill(near);
            // Its twin and anything else round the spot too: they would wear the player down while he stands there.
            foreach (var other in resp.DevLive(room)) if (Vector2.Distance(other.transform.position, nearAt) < 14f) Kill(other);
            yield return new WaitForSeconds(0.3f);
            hero.transform.position = Clear(hero, nearAt + Vector2.up * 0.5f);
            float until = Time.time + 8f;
            float logAt = Time.time + 4f;
            while (Time.time < until)
            {
                if (Time.time >= logAt) { logAt = float.MaxValue; Plugin.Log.LogInfo($"[AutoTest] 刷新2 站着 4 秒时那个刷新点：{resp.DevSlotAt(nearAt)}"); }
                // Held on the spot.
                if (Vector2.Distance(hero.transform.position, nearAt) > 1.5f) hero.transform.position = Clear(hero, nearAt + Vector2.up * 0.5f);
                yield return null;
            }
            bool backWhileThere = resp.DevSlotAt(nearAt).Contains("活着");
            hero.transform.position = Clear(hero, heroAt);
            Plugin.Log.LogInfo($"[AutoTest] 刷新2 出生点 {nearAt:F1}，主角走到 {(Vector2)hero.transform.position:F1}（相距 {Vector2.Distance(hero.transform.position, nearAt):F1}）");
            yield return new WaitForSeconds(1.5f);
            bool backAfter = resp.DevSlotAt(nearAt).Contains("活着");
            Plugin.Log.LogInfo($"[AutoTest] 刷新2 走开后那个刷新点：{resp.DevSlotAt(nearAt)}；场景里：{resp.DevReport(room, out _, out _, out _)}");
            Plugin.Log.LogInfo($"[AutoTest] 刷新2 打死 {nearName} 后站在它的出生点上 8 秒：{(backWhileThere ? "刷出来了（不该刷）" : "没刷（对）")}；走开后：{(backAfter ? "刷出来了" : "还没刷")}{(!backWhileThere && backAfter ? " 通过" : " 不通过")}");
            EnemyRespawn.DevFixedSeconds = -1f;
        }

        /// <summary>
        /// Dev (suite "respawnblock"): blocks and a platform the player put on enemies' spots in the
        /// Crossroads' first room: the ghost has no collider, and it and then the enemy come back at
        /// the nearest clear place, not in them; one dropped out of the world goes, and comes back.
        /// </summary>
        IEnumerator RespawnBlockTest()
        {
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            const string room = "Crossroads_01";
            yield return GoTo(room, EntryGateOf(room));
            yield return new WaitForSeconds(1.5f);
            // Entering a scene puts the game's damage mode back (HeroController.SetDamageMode clears isInvincible).
            PlayerData.instance.isInvincible = true;
            var resp = EnemyRespawn.Instance;
            var hero = HeroController.instance;
            Vector2 heroAt = hero.transform.position;
            var picks = new System.Collections.Generic.List<(HealthManager hm, Vector2 home, Vector2 size, Vector2 offset)>();
            HealthManager spare = null;
            foreach (var hm in resp.DevLive(room))
            {
                bool known = resp.DevBody(hm, out var home, out var size, out var offset, out bool walker);
                if (known && walker && picks.Count < 2 && Vector2.Distance(home, heroAt) > 10f && !picks.Exists(p => Vector2.Distance(p.home, home) < 5f))
                    picks.Add((hm, home, size, offset));
                else if (known && spare == null && Vector2.Distance(home, heroAt) > 10f) spare = hm;
            }
            Plugin.Log.LogInfo($"[AutoTest] 刷新堵 {room}：{string.Join("，", picks.ConvertAll(p => $"{p.hm.name} 出生点 {p.home:F1} 身子 {p.size:F1}"))}；掉出地图的那只 {(spare != null ? spare.name : "无")}");
            if (picks.Count < 2 || spare == null) { Plugin.Log.LogInfo("[AutoTest] 刷新堵 找不到可以试的怪 不通过"); yield break; }
            EnemyRespawn.DevFixedSeconds = 5f;
            int ok = 0, all = 0;
            void Check2(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 刷新堵 {what} {(pass ? "通过" : "不通过")}");
            }
            for (int round = 0; round < 2; round++)
            {
                var (hm, home, size, offset) = picks[round];
                string name = hm.name;
                Kill(hm);
                yield return new WaitForSeconds(0.5f);
                // First a mound of dirt over its whole body, then a platform through its middle.
                var placed = new System.Collections.Generic.List<Vector2Int>();
                var c = home + offset;
                var lo = CellGrid.WorldToCell(c - size * 0.5f - Vector2.one * 0.5f);
                var hi = CellGrid.WorldToCell(c + size * 0.5f + Vector2.one * 0.5f);
                int midY = CellGrid.WorldToCell(c).y;
                var kind = round == 0 ? CellKind.Dirt : CellKind.Platform;
                for (int y = lo.y; y <= hi.y; y++)
                    for (int x = lo.x; x <= hi.x; x++)
                    {
                        if (round == 1 && y != midY) continue;
                        if (TerraWorld.Get(x, y) != CellKind.Empty) continue;
                        TerraWorld.Set(x, y, kind);
                        placed.Add(new Vector2Int(x, y));
                    }
                int back0 = EnemyRespawn.DevRespawned, moved0 = EnemyRespawn.DevMoved;
                int ghostCols = -1;
                Vector2 ghostAt = Vector2.zero;
                float until = Time.time + 9f;
                while (Time.time < until && EnemyRespawn.DevRespawned == back0)
                {
                    if (ghostCols < 0 && resp.DevGhosts > 0)
                    {
                        // A moment for anything tk2d would add.
                        yield return null;
                        ghostCols = resp.DevGhostColliders;
                        float bd = float.MaxValue;
                        foreach (var g in resp.DevGhostsAt()) if (Vector2.Distance(g, c) < bd) { bd = Vector2.Distance(g, c); ghostAt = g; }
                        Player.TerraCamera.DevLook = c;
                        yield return new WaitForSeconds(0.3f);
                        Shot(room, $"respawnblock_ghost{round}");
                    }
                    yield return null;
                }
                bool back = EnemyRespawn.DevRespawned > back0, moved = EnemyRespawn.DevMoved > moved0;
                Vector2 at = EnemyRespawn.DevLastSpawn + offset;
                yield return new WaitForSeconds(0.2f);
                // In what was put there: any of those cells inside its body's upper three quarters.
                bool inBlock = false;
                var blo = CellGrid.WorldToCell(new Vector2(at.x - size.x * 0.4f, at.y - size.y * 0.25f));
                var bhi = CellGrid.WorldToCell(new Vector2(at.x + size.x * 0.4f, at.y + size.y * 0.5f));
                foreach (var cell in placed) if (cell.x >= blo.x && cell.x <= bhi.x && cell.y >= blo.y && cell.y <= bhi.y) inBlock = true;
                Shot(room, $"respawnblock_back{round}");
                yield return new WaitForSeconds(1.5f);
                string after = resp.DevSlotAt(at);
                Player.TerraCamera.DevLook = null;
                all++;
                bool pass = back && moved && !inBlock && ghostCols == 0 && Vector2.Distance(ghostAt, at) < 0.6f;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 刷新堵 {name} 出生点 {c:F1} 放了 {placed.Count} 格{(round == 0 ? "土块" : "平台")}：虚影 {ghostAt:F1}（碰撞体 {ghostCols} 个），{(back ? $"刷出在 {at:F1}（挪开 {Vector2.Distance(at, c):F1}{(moved ? "" : "，没挪")}），{(inBlock ? "在方块里" : "不在方块里")}；1.5 秒后 {after}" : "没刷出来")} {(pass ? "通过" : "不通过")}");
                foreach (var cell in placed) if (TerraWorld.Get(cell.x, cell.y) == kind) TerraWorld.Set(cell.x, cell.y, CellKind.Empty);
                yield return new WaitForSeconds(0.5f);
            }

            // One dropped out of the world: it goes after a while, and comes back on its spot.
            spare = null;
            foreach (var hm in resp.DevLive(room))
                if (resp.DevBody(hm, out var home, out _, out _, out _) && Vector2.Distance(home, hero.transform.position) > 10f && !picks.Exists(p => Vector2.Distance(p.home, home) < 3f)) { spare = hm; break; }
            if (spare == null) Check2(false, "找不到扔出地图的那只");
            else
            {
                string name = spare.name;
                resp.DevBody(spare, out var home, out _, out _, out _);
                int lost0 = EnemyRespawn.DevLost, back0 = EnemyRespawn.DevRespawned;
                // Far off past the room's side (no kill plane there, as under many rooms), held there.
                // Held there every frame: a wall crawler's own script would take it back to its wall.
                var rb = spare.GetComponent<Rigidbody2D>();
                var far = spare.transform.position + Vector3.right * 400f;
                float until = Time.time + 16f, logAt = Time.time + 1f;
                while (Time.time < until && EnemyRespawn.DevRespawned == back0)
                {
                    if (spare != null && EnemyRespawn.DevLost == lost0)
                    {
                        spare.transform.position = far;
                        if (rb != null) { rb.position = far; rb.linearVelocity = Vector2.zero; }
                    }
                    if (Time.time >= logAt && spare != null) { logAt = Time.time + 2f; Plugin.Log.LogInfo($"[AutoTest] 刷新堵 扔出去的 {name}：{resp.DevLostState(spare)}"); }
                    yield return null;
                }
                bool lost = EnemyRespawn.DevLost > lost0, back = EnemyRespawn.DevRespawned > back0;
                bool pass = lost && back && Vector2.Distance(EnemyRespawn.DevLastSpawn, home) < 0.6f;
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 刷新堵 {name} 扔到地图外面：{(lost ? "被收走了" : "没被收走")}，{(back ? $"在 {EnemyRespawn.DevLastSpawn:F1} 刷回来（出生点 {home:F1}）" : "没刷回来")} {(pass ? "通过" : "不通过")}");
            }
            EnemyRespawn.DevFixedSeconds = -1f;
            Plugin.Log.LogInfo($"[AutoTest] 刷新堵 合计 {ok}/{all}");
        }

        void Kill(HealthManager hm)
        {
            var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
            if (col != null) mod.Melee.HitWith(col, 999999, 0f, 0f, 1f, hm.transform.position);
        }
    }
}
