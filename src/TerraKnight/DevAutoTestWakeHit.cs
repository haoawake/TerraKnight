using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "wakehit", the user's 10-25 "新版敌人被攻击不进入战斗"): every common enemy kind in its own room, the
    /// player put well away from it (TK_WAKE_DIST, 14 units) and held there, struck once for TK_WAKE_DMG (30) (TK_WAKE_HIT player: the
    /// player's blow; minion: a minion's quiet one); for 4 s after, whether it came for him: its brain's alert, how much
    /// nearer it got, how far it moved, the states its own machine went through. TK_WAKE_AI new/old picks the enemy AI,
    /// TK_WAKE_KINDS some kinds (a;b). One line a kind: [WAKEHIT].
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator WakeHitTest()
        {
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            bool newAi = Environment.GetEnvironmentVariable("TK_WAKE_AI") != "old";
            World.EnemyAiMode.Current = newAi ? World.EnemyAiKind.New : World.EnemyAiKind.Vanilla;
            bool minion = Environment.GetEnvironmentVariable("TK_WAKE_HIT") == "minion";
            float dist = float.TryParse(Environment.GetEnvironmentVariable("TK_WAKE_DIST"), out var dd) ? dd : 14f;
            int damage = int.TryParse(Environment.GetEnvironmentVariable("TK_WAKE_DMG"), out var dm) ? dm : 30;
            var pick = (Environment.GetEnvironmentVariable("TK_WAKE_KINDS") ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var table = (IDictionary)AccessTools.Field(typeof(EnemyDrops), "table").GetValue(null);
            var byScene = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (DictionaryEntry kv in table)
            {
                string key = (string)kv.Key;
                var e = (EnemyDrops.Entry)kv.Value;
                if (key.Contains("@") || e.Boss || e.Hp <= 0 || string.IsNullOrEmpty(e.Scene)) continue;
                if (e.Kind != "normal" && e.Kind != "conditional" && e.Kind != "pogo" && e.Kind != "unique") continue;
                if (pick.Length > 0 && !pick.Contains(key)) continue;
                if (!byScene.TryGetValue(e.Scene, out var list)) byScene[e.Scene] = list = new List<string>();
                list.Add(key);
            }
            Plugin.Log.LogInfo($"[WAKEHIT] start ai={(newAi ? "new" : "old")} hit={(minion ? "minion" : "player")} dist={dist} dmg={damage} scenes={byScene.Count}");
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            int woke = 0, all = 0;
            // TK_WAKE_FROM: the rooms from that one on (a run cut short); a room whose scene stops the clock is left out.
            string from = Environment.GetEnvironmentVariable("TK_WAKE_FROM");
            foreach (var pair in byScene)
            {
                if (!string.IsNullOrEmpty(from) && string.CompareOrdinal(pair.Key, from) < 0) continue;
                if (pair.Key == "Crossroads_ShamanTemple") continue;
                yield return GoTo(pair.Key, EntryGateOf(pair.Key));
                yield return new WaitForSecondsRealtime(2.5f);
                hero = HeroController.instance;
                rb = hero.GetComponent<Rigidbody2D>();
                foreach (string kind in pair.Value)
                {
                    var hm = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                        .Where(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && UI.EnemyHover.RecordOf(h) == kind)
                        .OrderBy(h => h.name).FirstOrDefault();
                    if (hm == null) { Plugin.Log.LogInfo($"[WAKEHIT] {kind} none-in {pair.Key}"); continue; }
                    var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                    Vector2 Pos() => hm != null && col != null ? (Vector2)col.bounds.center : hm != null ? (Vector2)hm.transform.position : Vector2.zero;
                    var p0 = Pos();
                    // The side with more room, a clear place, held there.
                    rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    float side = Physics2D.Raycast(p0, Vector2.right, dist, 1 << 8).collider == null ? 1f
                        : Physics2D.Raycast(p0, Vector2.left, dist, 1 << 8).collider == null ? -1f : 1f;
                    hero.transform.position = Clear(hero, p0 + new Vector2(side * dist, 0.5f));
                    rb.linearVelocity = Vector2.zero;
                    rb.constraints = RigidbodyConstraints2D.FreezeAll;
                    yield return new WaitForSecondsRealtime(1f);
                    if (hm == null || hm.isDead) continue;
                    var brain = hm.GetComponent<EnemyAi.Brain>();
                    bool alertBefore = brain != null && brain.Alert;
                    var before = Pos();
                    var fsms = hm.GetComponents<PlayMakerFSM>();
                    var states = new List<string>();
                    string Flags(PlayMakerFSM f) { var a = f.FsmVariables.FindFsmBool("Friendly"); var b = f.FsmVariables.FindFsmBool("Hero in range"); return a == null && b == null ? "" : $"[友好{a?.Value} 范围{b?.Value}]"; }
                    string Now() => string.Join("|", fsms.Where(f => f != null && f.enabled).Select(f => f.FsmName + ":" + f.ActiveStateName + Flags(f))) + (brain != null ? " brain:" + brain.DevState + " why:" + AccessTools.Field(typeof(EnemyAi.Brain), "chooseWhy").GetValue(brain) + "/" + string.Join(",", ((string[])AccessTools.Field(typeof(EnemyAi.Brain), "whyNot").GetValue(brain) ?? new string[0]).Select(w => w ?? "ok")) : "");
                    states.Add(Now());
                    Vector2 heroAt = hero.GetComponent<Collider2D>().bounds.center;
                    float d0 = Vector2.Distance(before, heroAt);
                    int hp0 = hm.hp;
                    mod.Melee.HitWith(col, damage, 0f, 0f, side < 0 ? 1f : -1f, heroAt, autonomous: minion);
                    float nearest = d0, moved = 0f;
                    bool alert = false;
                    for (float t = Time.realtimeSinceStartup + 4f; Time.realtimeSinceStartup < t && hm != null && !hm.isDead;)
                    {
                        var p = Pos();
                        nearest = Mathf.Min(nearest, Vector2.Distance(p, heroAt));
                        moved = Mathf.Max(moved, Vector2.Distance(p, before));
                        alert |= brain != null && brain.Alert;
                        string s = Now();
                        if (states[states.Count - 1] != s && states.Count < 12) states.Add(s);
                        yield return null;
                    }
                    bool came = d0 - nearest > 1.5f || alert && moved > 1f;
                    all++;
                    if (came) woke++;
                    Plugin.Log.LogInfo($"[WAKEHIT] {kind} {(came ? "WOKE" : "STILL")} scene={pair.Key} obj={hm?.name} hp {hp0}->{hm?.hp} brain={(brain != null ? "yes" : "no")} alertBefore={alertBefore} alert={alert} " +
                        $"d0={d0:F1} nearer={d0 - nearest:F1} moved={moved:F1} states={string.Join(" > ", states)}");
                    rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    if (hm != null && !hm.isDead) hm.Die(null, AttackTypes.Generic, true);
                    yield return new WaitForSecondsRealtime(0.5f);
                }
            }
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            Plugin.Log.LogInfo($"[WAKEHIT] done {woke}/{all} came");
        }
    }
}
