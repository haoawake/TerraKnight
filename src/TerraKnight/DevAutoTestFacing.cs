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
    /// Dev (suite "facing", the user's 10-25 Husk Guard "反着跑步反着砸地板、卡住不动"): each kind of TK_FACE_KINDS
    /// (ZombieGuard) in its own room with TK_FACE_AI's enemy AI (new), the player held TK_FACE_DIST (7) units to one side and
    /// moved to the other every 5 s for TK_FACE_SECONDS (20): the frames it ran with its back to the way it went, the slams
    /// struck away from the player (its own Impact with Facing Right the other way), its longest stand while the player
    /// was in sight and near, and its states. Its picture's way is its own turns' (scale x and Facing Right together).
    /// One line a kind: [FACING].
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FacingTest()
        {
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            bool newAi = Environment.GetEnvironmentVariable("TK_FACE_AI") != "old";
            World.EnemyAiMode.Current = newAi ? World.EnemyAiKind.New : World.EnemyAiKind.Vanilla;
            EnemyAi.Brain.DevOldFacing = Environment.GetEnvironmentVariable("TK_FACE_BEFORE") == "1";
            float dist = float.TryParse(Environment.GetEnvironmentVariable("TK_FACE_DIST"), out var dd) ? dd : 7f;
            float seconds = float.TryParse(Environment.GetEnvironmentVariable("TK_FACE_SECONDS"), out var ss) ? ss : 20f;
            var kinds = (Environment.GetEnvironmentVariable("TK_FACE_KINDS") ?? "ZombieGuard").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var table = (IDictionary)AccessTools.Field(typeof(EnemyDrops), "table").GetValue(null);
            Plugin.Log.LogInfo($"[FACING] start ai={(newAi ? "new" : "old")} dist={dist} seconds={seconds} kinds={string.Join(";", kinds)}");
            int good = 0;
            foreach (string kind in kinds)
            {
                if (!table.Contains(kind)) { Plugin.Log.LogInfo($"[FACING] {kind} no-entry"); continue; }
                string scene = ((EnemyDrops.Entry)table[kind]).Scene;
                yield return GoTo(scene, EntryGateOf(scene));
                yield return new WaitForSecondsRealtime(2.5f);
                var hero = HeroController.instance;
                var rb = hero.GetComponent<Rigidbody2D>();
                var hm = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                    .Where(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && UI.EnemyHover.RecordOf(h) == kind)
                    .OrderBy(h => h.name).FirstOrDefault();
                if (hm == null) { Plugin.Log.LogInfo($"[FACING] {kind} none-in {scene}"); continue; }
                var col = hm.GetComponent<Collider2D>();
                Vector2 Pos() => hm != null && col != null ? (Vector2)col.bounds.center : hm != null ? (Vector2)hm.transform.position : Vector2.zero;
                var fsm = hm.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmVariables.FindFsmBool("Facing Right") != null);
                var facingVar = fsm != null ? fsm.FsmVariables.FindFsmBool("Facing Right") : null;
                // Its picture's way, read from its own turns: the scale its script gives with Facing Right true.
                bool? drawnRight = null;
                if (fsm != null)
                    foreach (var st in fsm.FsmStates)
                    {
                        float x = 0f;
                        bool? says = null;
                        foreach (var a in st.Actions)
                        {
                            if (a is HutongGames.PlayMaker.Actions.SetScale sc && sc.gameObject.OwnerOption == HutongGames.PlayMaker.OwnerDefaultOption.UseOwner && !sc.x.UseVariable && !sc.x.IsNone) x = sc.x.Value;
                            else if (a is HutongGames.PlayMaker.Actions.SetBoolValue sb && sb.boolVariable.Name == "Facing Right" && !sb.boolValue.UseVariable) says = sb.boolValue.Value;
                        }
                        if (says != null && x != 0f) { drawnRight = says.Value == x > 0f; break; }
                    }
                bool FacesRightNow() => hm.transform.localScale.x > 0f == (drawnRight ?? false);
                var brain = hm.GetComponent<EnemyAi.Brain>();
                var p0 = Pos();
                float side = Physics2D.Raycast(p0, Vector2.right, dist, 1 << 8).collider == null ? 1f : -1f;
                void PutHero()
                {
                    rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    var at = Pos();
                    float s = Physics2D.Raycast(at, Vector2.right * side, dist, 1 << 8).collider == null ? side : -side;
                    hero.transform.position = Clear(hero, at + new Vector2(s * dist, 0.5f));
                    rb.linearVelocity = Vector2.zero;
                    rb.constraints = RigidbodyConstraints2D.FreezeAll;
                }
                PutHero();
                int frames = 0, moving = 0, backward = 0, slams = 0, slamsAway = 0, flagWrong = 0;
                float stillFor = 0f, longestStill = 0f, nextSwap = Time.realtimeSinceStartup + 5f;
                Vector2 last = Pos();
                string lastState = null, stillAt = "";
                var trail = new List<string>();
                for (float end = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < end && hm != null && !hm.isDead;)
                {
                    yield return null;
                    if (hm == null || hm.isDead) break;
                    if (Time.realtimeSinceStartup >= nextSwap) { side = -side; PutHero(); nextSwap += 5f; stillFor = 0f; }
                    var p = Pos();
                    Vector2 heroAt = hero.GetComponent<Collider2D>().bounds.center;
                    float vx = (p.x - last.x) / Mathf.Max(Time.deltaTime, 0.001f);
                    last = p;
                    frames++;
                    bool right = FacesRightNow();
                    // Going on its own way: its script's walk and run, the brain's charge or leap (its club's recoil hops
                    // it back on purpose).
                    string fsmNow = fsm != null ? fsm.ActiveStateName : "";
                    string brainNow = brain != null ? brain.DevState : "";
                    bool goes = fsmNow == "Walk" || fsmNow == "Run" || brainNow.EndsWith(":Act") && (brainNow.StartsWith("Lunge") || brainNow.StartsWith("Leap"));
                    if (goes && Mathf.Abs(vx) > 2f)
                    {
                        moving++;
                        if (vx > 0f != right) backward++;
                    }
                    if (facingVar != null && facingVar.Value != right) flagWrong++;
                    string state = fsm != null ? fsm.ActiveStateName : "-";
                    if (state != lastState)
                    {
                        bool heroRight = heroAt.x > p.x;
                        // A slam away from where the player was when it began (he is moved every 5 s).
                        if ((state == "Impact" || state == "Land") && Time.realtimeSinceStartup < nextSwap - 3.5f)
                        {
                            slams++;
                            if (right != heroRight) slamsAway++;
                        }
                        if (trail.Count < 40) trail.Add($"{state}{(right ? "→" : "←")}{(heroRight ? "R" : "L")}" + (brain != null ? "/" + brain.DevState : ""));
                        lastState = state;
                    }
                    bool near = Vector2.Distance(p, heroAt) < 12f;
                    if (near && Mathf.Abs(vx) < 0.3f)
                    {
                        stillFor += Time.deltaTime;
                        if (stillFor > longestStill) { longestStill = stillFor; stillAt = $"{fsmNow}/{brainNow}@{p.x:F1},{p.y:F1} hero {heroAt.x:F1},{heroAt.y:F1}"; }
                    }
                    else stillFor = 0f;
                }
                bool ok = moving > 0 && backward <= moving / 20 && slamsAway == 0 && longestStill < 4f;
                if (ok) good++;
                Plugin.Log.LogInfo($"[FACING] {kind} {(ok ? "OK" : "BAD")} scene={scene} drawnRight={drawnRight} brain={(brain != null ? "yes" : "no")} frames={frames} moving={moving} backward={backward} " +
                    $"slams={slams} slamsAway={slamsAway} flagWrong={flagWrong} longestStill={longestStill:F1}s({stillAt}) trail={string.Join(" > ", trail)}");
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
            Plugin.Log.LogInfo($"[FACING] done {good}/{kinds.Length} ok");
        }
    }
}
