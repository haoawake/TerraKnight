using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TerraKnight.Items;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fkhead"): the False Knight fought as a player fights him, with a sword's blows and their
    /// knockback whenever he is in reach, jumps and all, until he falls. Every frame: his body, his head (where
    /// it comes out, how far from the body, which side of the arena's gates), his control FSM's state; each
    /// head that comes out is reported with what he was doing, and any head outside the gates or far from him.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FalseKnightHeadTest()
        {
            var gm = GameManager.instance;
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            const string arena = "Crossroads_10";
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                SceneName = arena, EntryGateName = "left1", HeroLeaveDirection = GlobalEnums.GatePosition.right,
                EntryDelay = 0f, WaitForSceneTransitionCameraFade = true, Visualization = GameManager.SceneLoadVisualizations.Default,
            });
            float wait = Time.time + 30f;
            while ((gm.sceneName != arena || !GameHooks.PlayerHasControl()) && Time.time < wait) yield return null;
            wait = Time.time + 8f;
            while (!UnityEngine.SceneManagement.SceneManager.GetSceneByName(arena + "_boss").isLoaded && Time.time < wait) yield return null;
            yield return new WaitForSeconds(1f);
            var hero = HeroController.instance;
            hero.transform.position = Clear(hero, new Vector2(38f, 28f));
            HealthManager fk = null;
            wait = Time.time + 25f;
            while (Time.time < wait && fk == null)
            {
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    if (hm.name == "False Knight New" && hm.gameObject.activeInHierarchy) fk = hm;
                yield return null;
            }
            if (fk == null) { Plugin.Log.LogInfo("[AutoTest] 假骑士头 等不到假骑士出场 不通过"); yield break; }
            HealthManager head = null;
            foreach (var hm in fk.GetComponentsInChildren<HealthManager>(true)) if (hm.name == "Head") head = hm;
            if (head == null)
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (hm.name == "Head" && hm.gameObject.scene == fk.gameObject.scene) head = hm;
            var control = new List<PlayMakerFSM>(fk.GetComponentsInChildren<PlayMakerFSM>(true)).Find(f => f.FsmName == "FalseyControl");
            // The arena's gates: the x range the fight is held in.
            float left = float.MinValue, right = float.MaxValue;
            var gates = new StringBuilder();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.gameObject.scene != fk.gameObject.scene && t.gameObject.scene.name != arena) continue;
                if (!t.name.StartsWith("Battle Gate")) continue;
                gates.Append($" {t.name}@{(Vector2)t.position:F1}");
                if (t.position.x < fk.transform.position.x) left = Mathf.Max(left, t.position.x); else right = Mathf.Min(right, t.position.x);
            }
            var fkc = fk.GetComponent<Collider2D>();
            Plugin.Log.LogInfo($"[AutoTest] 假骑士头 出场：身体 {(Vector2)fk.transform.position:F1}（碰撞 {(Vector2)fkc.bounds.center:F1}），头 {(head != null ? PathOf(head.transform) + " " + ((Vector2)head.transform.position).ToString("F1") + " 激活 " + head.gameObject.activeInHierarchy : "无")}，门{gates}，场地 x {left:F1}..{right:F1}");
            if (head == null) yield break;

            var inv = mod.Inventory;
            inv.Slots[1] = ItemStack.Of(ItemType.IronBroadsword);
            inv.Selected = 1;
            var trail = new List<string>();
            string lastState = null;
            bool headWas = false;
            int outs = 0, bad = 0;
            float t0 = Time.time, nextHit = 0f;
            while (Time.time - t0 < 180f && !pd.falseKnightDefeated)
            {
                mod.Stats.FullHeal();
                string cs = control != null ? control.ActiveStateName : "?";
                Vector2 body = fkc != null ? (Vector2)fkc.bounds.center : (Vector2)fk.transform.position;
                if (cs != lastState) { lastState = cs; if (trail.Count < 300) trail.Add($"{Time.time - t0:F1}s {cs} 身体 {body:F1}"); }
                var hc = head.GetComponent<Collider2D>();
                Vector2 hp = hc != null ? (Vector2)hc.bounds.center : (Vector2)head.transform.position;
                // The game keeps the head parked high above the arena (y 240) and brings it to him when he falls stunned.
                bool headOut = head.gameObject.activeInHierarchy && hp.y < 150f;
                if (headOut && !headWas)
                {
                    outs++;
                    yield return null;
                    hp = hc != null ? (Vector2)hc.bounds.center : (Vector2)head.transform.position;
                    Plugin.Log.LogInfo($"[AutoTest] 假骑士头 第 {outs} 次露头：{Time.time - t0:F1}s，状态 {cs}，身体 {body:F1}，头 {hp:F1}，相距 {Vector2.Distance(hp, body):F1}，画出的头：{HeadsShown()}");
                    Shot(arena, $"fkhead_out{outs}");
                }
                if (headOut && (hp.x < left - 0.5f || hp.x > right + 0.5f || Vector2.Distance(hp, body) > 6f))
                {
                    bad++;
                    if (bad <= 3)
                    {
                        Plugin.Log.LogInfo($"[AutoTest] 假骑士头 头跑开了：{Time.time - t0:F1}s，状态 {cs}，身体 {body:F1}，头 {hp:F1}，场地 x {left:F1}..{right:F1}；画出的头：{HeadsShown()}");
                        Shot(arena, $"fkhead_away{bad}");
                    }
                }
                headWas = headOut;
                // Blows as a player lands them: on the head when it is out, else on the armour, from close by.
                bool raging = cs.StartsWith("Rage") || cs == "Particle Pause" || cs == "Anim End" || cs == "R Attack Antic";
                // Asleep up in the roof he is woken by a blow (BossWake), as the old fight test does.
                if (Time.time >= nextHit && GameHooks.PlayerHasControl() && !raging)
                {
                    nextHit = Time.time + 0.35f;
                    var target = headOut && hc != null && hc.enabled ? hc : fkc;
                    if (target != null && target.enabled && target.gameObject.activeInHierarchy)
                    {
                        var tb = target.bounds;
                        hero.transform.position = Clear(hero, new Vector2(tb.min.x - 1.4f, tb.min.y + 0.3f));
                        mod.Melee.HitWith(target, headOut ? 120 : 60, 12f, 0f, 1f, hero.GetComponent<BoxCollider2D>().bounds.center);
                    }
                }
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 假骑士头 状态：{string.Join(" | ", trail)}");
            Plugin.Log.LogInfo($"[AutoTest] 假骑士头 打完 {pd.falseKnightDefeated}，露头 {outs} 次，头跑开的帧 {bad} {(bad == 0 && outs > 0 ? "通过" : "不通过")}");
        }
    }
}
