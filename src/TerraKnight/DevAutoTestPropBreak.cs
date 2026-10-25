using System.Collections;
using System.Linq;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "propbreak"): a lifeblood cocoon and the geo rocks broken by Terraria weapons (the user's, 2026-10-20). In
    /// King's Pass: each cocoon and geo rock struck ten times by a weapon's hit (MeleeHits.HitWith, as a shot or a swing
    /// lands), a third of a second apart, the Knight beside it; the cocoon's opening and the rock's hits left logged.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator PropBreakTest()
        {
            mod.DevNoFallDamage = true;
            const string room = "Tutorial_01";
            yield return GoTo(room, EntryGateOf(room));
            yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            var hero = HeroController.instance;
            int ok = 0, all = 0;
            try
            {
                var cocoons = Object.FindObjectsByType<HealthCocoon>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(c => c.gameObject.scene.name == room).ToList();
                var rocks = Object.FindObjectsByType<GeoRock>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(c => c.gameObject.scene.name == room).Take(2).ToList();
                Plugin.Log.LogInfo($"[AutoTest] 打机关 {room}：生命血茧 {cocoons.Count} 个，吉欧石 {rocks.Count} 个（试前两个）");
                foreach (var target in cocoons.Cast<Component>().Concat(rocks))
                {
                    if (target == null) continue;
                    var col = target.GetComponent<Collider2D>() ?? target.GetComponentInChildren<Collider2D>();
                    if (col == null) { Plugin.Log.LogInfo($"[AutoTest] 打机关 {target.name} 没有碰撞体 不通过"); continue; }
                    string Before()
                    {
                        if (target == null) return "没了";
                        if (target is HealthCocoon hc) return (bool)HarmonyLib.AccessTools.Field(typeof(HealthCocoon), "activated").GetValue(hc) ? "破了" : "还在";
                        var fsm = target.GetComponent<PlayMakerFSM>();
                        var hits = fsm != null ? fsm.FsmVariables.GetFsmInt("Hits") : null;
                        return $"还剩 {(hits != null ? hits.Value : -1)} 下（{(fsm != null ? fsm.ActiveStateName : "-")}）";
                    }
                    string b0 = Before();
                    int pulses = Items.PropHits.DevPulses, tinks = Items.PropHits.DevTinkEvents, touches = Items.NailAttackProxy.DevReceivedTouches;
                    var cols = target.GetComponentsInChildren<Collider2D>(true);
                    Plugin.Log.LogInfo($"[AutoTest] 打机关 {target.name} 的碰撞体：{string.Join("；", cols.Select(c => c.name + " " + Items.PropHits.DevWhy(c)))}");
                    foreach (var f in target.GetComponents<PlayMakerFSM>())
                    {
                        var fsb = new System.Text.StringBuilder();
                        foreach (var st in f.FsmStates)
                        {
                            fsb.Append($" [{st.Name}:");
                            foreach (var act in st.Actions)
                            {
                                fsb.Append(" " + act.GetType().Name);
                                if (act is HutongGames.PlayMaker.Actions.Trigger2dEvent te) fsb.Append($"({te.trigger}/{te.collideTag?.Value})");
                            }
                            foreach (var tr in st.Transitions) fsb.Append($" {tr.EventName}→{tr.ToState}");
                            fsb.Append("]");
                        }
                        Plugin.Log.LogInfo($"[AutoTest] 打机关 {target.name} 状态机 {f.FsmName}：{fsb}");
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 打机关 {target.name} 组件：{string.Join("、", target.GetComponents<Component>().Select(c => c.GetType().Name))}");
                    hero.transform.position = Clear(hero, (Vector2)col.bounds.center + Vector2.left * 1.5f);
                    yield return new WaitForSeconds(0.5f);
                    for (int i = 0; i < 10 && target != null; i++)
                    {
                        mod.Melee.HitWith(col, 20, 2f, 0f, 1f, (Vector2)col.bounds.center + Vector2.left);
                        if (i == 0)
                        {
                            yield return null;
                            var gf = target != null ? target.GetComponent<PlayMakerFSM>() : null;
                            if (gf != null) Plugin.Log.LogInfo($"[AutoTest] 打机关 {target.name} 第一刀后一帧：状态 {gf.ActiveStateName}，层碰撞 17-{col.gameObject.layer} {!Physics2D.GetIgnoreLayerCollision(17, col.gameObject.layer)}");
                        }
                        yield return new WaitForSeconds(0.35f);
                    }
                    yield return new WaitForSeconds(1f);
                    string b1 = Before();
                    Plugin.Log.LogInfo($"[AutoTest] 打机关 {target?.name} 补刀 {Items.PropHits.DevPulses - pulses} 次，ReceivedDamage 收到 {Items.NailAttackProxy.DevReceivedTouches - touches} 次，叮 {Items.PropHits.DevTinkEvents - tinks} 次，最后一刀 {Items.PropHits.DevLast}");
                    all++;
                    bool moved = b1 != b0;
                    if (moved) ok++;
                    Plugin.Log.LogInfo($"[AutoTest] 打机关 {(target is HealthCocoon ? "生命血茧" : "吉欧石")} {target?.name}：{b0} → {b1} {(moved ? "通过" : "不通过")}");
                }
            }
            finally { Player.TerraStats.DevImmortal = false; }
            Plugin.Log.LogInfo($"[AutoTest] 打机关 合计 {ok}/{all} {(ok == all && all > 0 ? "通过" : "不通过")}");
        }
    }
}
