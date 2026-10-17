using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "sealprobe_Key1.Key2"): each arena reached as a seal's rematch, nothing done in it; what starts its fight
    /// dumped for the log: the boss's own FSMs (inactive too) and the room's battle, boss, challenge, ghost and wake FSMs.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly Regex sealProbeNames = new Regex("Battle|Boss|Challenge|Ghost|Refight|Wake|Encounter|Appear|Start|Prompt|Dream|Throne|Lord", RegexOptions.IgnoreCase);

        /// <summary>One FSM in a line: its state now, what that state does and waits on (its Trigger2dEvent's tag and event), where it goes.</summary>
        static void Waits(string key, PlayMakerFSM f)
        {
            var st = f.Fsm?.ActiveState;
            var sb = new System.Text.StringBuilder();
            if (st != null)
            {
                foreach (var a in st.Actions)
                {
                    if (a is HutongGames.PlayMaker.Actions.Trigger2dEvent te) sb.Append($" 触发({te.trigger} {te.collideTag?.Value}→{te.sendEvent?.Name})");
                    else if (a is HutongGames.PlayMaker.Actions.SendEventByName se) sb.Append($" 发({se.sendEvent?.Value})");
                    else if (a is HutongGames.PlayMaker.Actions.BoolTest bt) sb.Append($" 判({bt.boolVariable?.Name}→{bt.isTrue?.Name}/{bt.isFalse?.Name})");
                    else sb.Append(' ').Append(a.GetType().Name);
                }
                sb.Append(" |");
                foreach (var t in st.Transitions) sb.Append($" {t.EventName}->{t.ToState}");
            }
            var col = f.GetComponent<Collider2D>();
            Plugin.Log.LogInfo($"[AutoTest] 召唤探查 {key} · {f.gameObject.scene.name}/{PathOf(f.transform)} '{f.FsmName}' 开{f.gameObject.activeInHierarchy} 碰撞{(col == null ? "无" : $"{col.enabled}/{col.isTrigger} {col.bounds.center:F1} {col.bounds.size:F1}")} 状态 {f.ActiveStateName}：{sb}");
        }

        IEnumerator SealProbe(string only)
        {
            bool full = only.StartsWith("full.");
            if (full) only = only.Substring(5);
            PlayerData.instance.isInvincible = true;
            PlayerData.instance.hasDreamNail = true;
            var list = new List<string>();
            if (only == "world") { foreach (var k in BossArena.DevKeys()) if (BossArena.DevWorldArena(k, out _, out _)) list.Add(k); }
            else list.AddRange(only.Split('.'));
            foreach (var key in list)
            {
                if (!BossArena.Has(key)) { Plugin.Log.LogInfo($"[AutoTest] 召唤探查 {key}：没有竞技场"); continue; }
                yield return SealTravel("Town", "left1");
                Bestiary.Kills[key] = 1;
                if (!BossArena.Fight(key, true)) { Plugin.Log.LogInfo($"[AutoTest] 召唤探查 {key}：进不去"); continue; }
                float until = Time.realtimeSinceStartup + 20f;
                while (!BossArena.Arrived(key) && Time.realtimeSinceStartup < until) yield return null;
                float ctl = Time.realtimeSinceStartup + 6f;
                while (!GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ctl) yield return null;
                yield return new WaitForSecondsRealtime(1f);
                string room = BossArena.SceneOf(key), record = key.Split('@')[0];
                var h = HeroController.instance;
                Plugin.Log.LogInfo($"[AutoTest] 召唤探查 {key}：在 {GameManager.instance.sceneName}，主角 {(Vector2)h.transform.position:F1} 可操作 {GameHooks.PlayerHasControl()}");
                var seen = new HashSet<PlayMakerFSM>();
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    string sn = hm.gameObject.scene.name;
                    if ((sn != room && sn != room + "_boss") || UI.EnemyHover.RecordOf(hm) != record) continue;
                    Plugin.Log.LogInfo($"[AutoTest] 召唤探查 {key} 首领 {PathOf(hm.transform)} 开{hm.gameObject.activeInHierarchy} hp{hm.hp} 无敌{hm.IsInvincible} 在 {(Vector2)hm.transform.position:F1}");
                    foreach (var f in hm.GetComponentsInChildren<PlayMakerFSM>(true)) if (seen.Add(f)) Logged(() => Waits(key, f));
                }
                int n = 0;
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    string sn = f.gameObject.scene.name;
                    if ((sn != room && sn != room + "_boss") || seen.Contains(f)) continue;
                    bool waitsTouch = f.Fsm?.ActiveState != null && System.Array.Exists(f.Fsm.ActiveState.Actions, a => a is HutongGames.PlayMaker.Actions.Trigger2dEvent);
                    if (!waitsTouch && !sealProbeNames.IsMatch(f.gameObject.name + " " + f.FsmName)) continue;
                    if (f.GetComponentInParent<HealthManager>() != null && !full) continue;
                    if (++n > (full ? 400 : 70)) break;
                    seen.Add(f);
                    Logged(() => { Waits(key, f); if (full && (waitsTouch || f.gameObject.name == "Boss Control")) DumpFsm(f); });
                }
            }
            yield return SealTravel("Town", "left1");
        }
    }
}
