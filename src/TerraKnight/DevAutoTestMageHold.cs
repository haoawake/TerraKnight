using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "magehold", MageMech.Slow's hold, the user's 10-28 "蛇发女妖头的石化根本没有用"): the Forgotten
    /// Crossroads' own enemies turned to stone for 1.5 s, each kept where it was with its animation stopped and its FSMs
    /// off; let go, they are as they were and move again; one killed while held dies as the game kills it. Lines
    /// "[AutoTest] 定身 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MageHoldTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 定身 {what} {(pass ? "通过" : "不通过")}");
            }
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(2f);
            var foes = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .Where(hm => !hm.isDead && hm.gameObject.activeInHierarchy && hm.gameObject.scene.name == "Crossroads_01" && !EnemyStats.Unstoppable(EnemyStats.Of(hm)))
                .ToList();
            Check(foes.Count >= 2, $"十字路的敌人 {foes.Count} 只：{string.Join("、", foes.Select(f => f.name))}");
            if (foes.Count == 0) yield break;
            foreach (var f in foes) f.hp = Mathf.Max(f.hp, 9000);

            var before = foes.ToDictionary(f => f, f => (Vector2)f.transform.position);
            yield return new WaitForSeconds(1.5f);
            int movedBefore = foes.Count(f => f != null && !f.isDead && Vector2.Distance(before[f], f.transform.position) > 0.2f);
            var fsmOn = foes.ToDictionary(f => f, f => f.GetComponentsInChildren<PlayMakerFSM>(true).Select(m => m.enabled).ToArray());
            string States(HealthManager f) => f == null ? "无" : string.Join(" ", f.GetComponents<PlayMakerFSM>().Select(m => $"{m.FsmName}:{m.ActiveStateName}"));
            var statesBefore = foes.ToDictionary(f => f, f => States(f));

            // Knocked back to the right just before (a blow of the player's in flight as the stone comes), and again while held:
            // let go, none may slide on (the user's 10-28 "石化结束后敌人疯狂往右边平移").
            var walked = foes.ToDictionary(f => f, f => (Vector2)f.transform.position);
            foreach (var f in foes) TerraKnockback.Push(f, 10f, 1f);
            yield return new WaitForSeconds(0.1f);
            foreach (var f in foes) MageMech.Slow(f, 0.05f, 1.5f, new Color(0.6f, 0.6f, 0.6f));
            var held = foes.ToDictionary(f => f, f => (Vector2)f.transform.position);
            yield return new WaitForSeconds(0.5f);
            foreach (var f in foes) if (f != null && !f.isDead) TerraKnockback.Push(f, 10f, 1f);
            yield return new WaitForSeconds(0.7f);
            foreach (var f in foes)
            {
                if (f == null || f.isDead) continue;
                float drift = Vector2.Distance(held[f], f.transform.position);
                var anim = f.GetComponent<tk2dSpriteAnimator>();
                bool fsmOff = f.GetComponentsInChildren<PlayMakerFSM>(true).All(m => !m.enabled);
                Check(MageMech.IsHeld(f) && drift < 0.05f && fsmOff && (anim == null || anim.Paused),
                    $"{f.name} 石化中：挪动 {drift:0.000}，动画暂停 {(anim == null ? "无动画" : anim.Paused.ToString())}，状态机全关 {fsmOff}");
            }

            yield return new WaitForSeconds(0.6f);
            var released = foes.ToDictionary(f => f, f => (Vector2)f.transform.position);
            var statesReleased = foes.ToDictionary(f => f, f => States(f));
            foreach (var f in foes)
            {
                if (f == null || f.isDead) continue;
                var anim = f.GetComponent<tk2dSpriteAnimator>();
                bool same = f.GetComponentsInChildren<PlayMakerFSM>(true).Select(m => m.enabled).SequenceEqual(fsmOn[f]);
                Check(!MageMech.IsHeld(f) && same && (anim == null || !anim.Paused), $"{f.name} 解除后：状态机照旧 {same}，动画暂停 {(anim == null ? "无动画" : anim.Paused.ToString())}");
            }
            // Each moving again on its own within 3 s, as many as moved before (a crawler or climber held at a standstill stayed
            // there; a husk may stop and look round a while of its own accord).
            var furthest = foes.ToDictionary(f => f, f => 0f);
            for (float until = Time.time + 3f; Time.time < until; )
            {
                yield return null;
                foreach (var f in foes)
                    if (f != null && !f.isDead) furthest[f] = Mathf.Max(furthest[f], Vector2.Distance(released[f], f.transform.position));
            }
            int movedAfter = 0;
            var stuck = new System.Collections.Generic.List<string>();
            var slid = new System.Collections.Generic.List<string>();
            foreach (var f in foes)
            {
                if (f == null || f.isDead) continue;
                // Before: 1.5 s walked. After: 3 s at its own pace is about twice that walk (a climber that turned a corner before
                // goes further); a knockback's slide goes tens of units.
                float went = furthest[f], wentBefore = Vector2.Distance(before[f], walked[f]);
                if (went > 0.2f) movedAfter++;
                else if (wentBefore > 0.2f) stuck.Add(f.name);
                if (went > 3.5f * wentBefore + 3f) slid.Add($"{f.name} {went:0.0}");
                Plugin.Log.LogInfo($"[AutoTest] 定身 {f.name}：石化前 1.5 秒走了 {wentBefore:0.00}，解除后 3 秒最远 {went:0.00}；状态 石化前 [{statesBefore[f]}] 解除时 [{statesReleased[f]}] 3 秒后 [{States(f)}]");
            }
            Check(stuck.Count == 0, $"解除后自己动起来的 {movedAfter} 只（石化前 {movedBefore} 只），走着被石化、解除后不动的：{(stuck.Count == 0 ? "无" : string.Join("、", stuck))}");
            Check(slid.Count == 0, $"石化前后被击退，解除后滑出去的：{(slid.Count == 0 ? "无" : string.Join("、", slid))}");

            // Killed while held: let go first, so it dies as the game kills it.
            var victim = foes.FirstOrDefault(f => f != null && !f.isDead);
            if (victim != null)
            {
                MageMech.Slow(victim, 0.05f, 2f);
                yield return null;
                bool wasHeld = MageMech.IsHeld(victim);
                victim.Die(null, AttackTypes.Generic, true);
                yield return new WaitForSeconds(1.5f);
                bool gone = victim == null || victim.isDead;
                bool fsmBack = victim == null || victim.GetComponentsInChildren<PlayMakerFSM>(true).Select(m => m.enabled).SequenceEqual(fsmOn[victim]) || !victim.gameObject.activeInHierarchy;
                Check(wasHeld && gone && !MageMech.IsHeld(victim), $"石化中被打死：先放开 {!MageMech.IsHeld(victim)}，死了 {gone}，状态机放回 {fsmBack}，还在场 {(victim != null && victim.gameObject.activeInHierarchy)}");
            }
            Plugin.Log.LogInfo($"[AutoTest] 定身 合计 {ok}/{all}");
        }
    }
}
