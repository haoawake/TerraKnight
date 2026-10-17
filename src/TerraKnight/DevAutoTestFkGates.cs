using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_fkgates"): the False Knight's first fight to its end as fkhead fights it, then his room's battle
    /// gates and battle scene watched for a minute: each gate's state and whether it still stands solid, where he lies,
    /// the Knight's place. A gate left shut after the win is the way out walled off.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FkGatesTest()
        {
            // Kept alive throughout: entering the room clears isInvincible, and one of his slams is more than a new save's life.
            bool done = false;
            mod.StartCoroutine(NetKeepAlive(() => done));
            try { yield return FalseKnightHeadTest(); }
            finally { done = true; }
            done = false;
            mod.StartCoroutine(NetKeepAlive(() => done));
            var pd = PlayerData.instance;
            FxCheck("假骑士第一场打完", pd.falseKnightDefeated, "");
            string last = null;
            for (float t = 0f; t < 60f; t += 5f)
            {
                int shut = GatesShut(out string gates);
                var battle = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).FirstOrDefault(f => f.gameObject.name == "Battle Scene" && f.gameObject.scene.name.StartsWith(FkArena));
                var fk = Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(h => h.name == "False Knight New");
                var control = fk != null ? fk.GetComponentsInChildren<PlayMakerFSM>(true).FirstOrDefault(f => f.FsmName == "FalseyControl") : null;
                string now = $"门{gates}；战斗 {battle?.FsmName}:{battle?.ActiveStateName}；假骑士 {(fk != null ? $"{(Vector2)fk.transform.position:F1} 死{(fk.isDead ? 1 : 0)} 血{fk.hp} {control?.ActiveStateName}" : "无")}";
                if (now != last) Plugin.Log.LogInfo($"[AutoTest] 修复1016 假骑士门 {t:F0}s 关着 {shut}：{now}；主角 {(Vector2)HeroController.instance.transform.position:F1}");
                last = now;
                if (shut == 0) break;
                // As a player finishes him: down to where he lies, the blows on whatever of him still takes them.
                for (float strike = Time.time + 5f; Time.time < strike;)
                {
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    {
                        if (hm == null || hm.isDead || hm.hp <= 0 || !hm.gameObject.scene.name.StartsWith(FkArena)) continue;
                        if (hm.name != "False Knight New" && hm.name != "Head") continue;
                        var hc = hm.GetComponent<Collider2D>();
                        if (hc == null || !hc.enabled || !hm.gameObject.activeInHierarchy) continue;
                        var hero = HeroController.instance;
                        hero.transform.position = Clear(hero, new Vector2(hc.bounds.min.x - 1.4f, hc.bounds.min.y + 0.3f));
                        mod.Melee.HitWith(hc, 120, 12f, 0f, 1f, hero.GetComponent<BoxCollider2D>().bounds.center);
                    }
                    yield return new WaitForSeconds(0.35f);
                }
            }
            {
                var fk = Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(h => h.name == "False Knight New");
                var head = fk != null ? fk.GetComponentsInChildren<HealthManager>(true).FirstOrDefault(h => h.name == "Head") : null;
                var hc = head != null ? head.GetComponent<Collider2D>() : null;
                Plugin.Log.LogInfo($"[AutoTest] 修复1016 假骑士门 头：{(head != null ? $"开{(head.gameObject.activeInHierarchy ? 1 : 0)} 碰撞{(hc != null && hc.enabled ? 1 : 0)} 血{head.hp} 死{(head.isDead ? 1 : 0)} 无敌{head.IsInvincible} 打倒{EnemyStats.Defeated(head)} {(Vector2)head.transform.position:F1}" : "无")}；身子 打倒{EnemyStats.Defeated(fk)} 无敌{fk?.IsInvincible}");
                if (fk != null) foreach (var f in fk.GetComponentsInChildren<PlayMakerFSM>(true)) if (f.FsmName == "FalseyControl") DumpFsm(f);
            }
            int left = GatesShut(out string end);
            Shot(FkArena, "fx_fkgates_end");
            yield return null;
            FxCheck("假骑士第一场打完后战斗门都开了", left == 0, end);
            done = true;
        }
    }
}
