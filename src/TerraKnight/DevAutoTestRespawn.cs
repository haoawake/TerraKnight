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

        void Kill(HealthManager hm)
        {
            var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
            if (col != null) mod.Melee.HitWith(col, 999999, 0f, 0f, 1f, hm.transform.position);
        }
    }
}
