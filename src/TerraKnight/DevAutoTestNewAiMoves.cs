using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TerraKnight.EnemyAi;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        /// <summary>
        /// Every move of every new-AI enemy made once at a player who stands in its way: the enemy put at the move's own
        /// reach (inside a pulse's or a cloud's ring), the move begun as its brain would, watched to its end and a moment
        /// after; whether it hurt him (its own shots and areas, or its body). One [NEWAI_MOVE] line a move.
        /// TK_NEWAI_PROFILES picks some enemies (a;b).
        /// </summary>
        IEnumerator NewAiMoves()
        {
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            var hero = HeroController.instance;
            var hbody = hero.GetComponent<Rigidbody2D>();
            var hcol = hero.GetComponent<Collider2D>();
            Vector3 home = hero.transform.position;
            _ = Profiles.Count;
            var profiles = (Dictionary<string, Profile>)AccessTools.Field(typeof(Profiles), "all").GetValue(null);
            var selected = (Environment.GetEnvironmentVariable("TK_NEWAI_PROFILES") ?? "").Split(';');
            var entries = profiles.Where(p => selected.Length == 1 && selected[0] == "" || selected.Contains(p.Key)).ToArray();
            Plugin.Log.LogInfo($"[NEWAI_MOVE] start {entries.Length} enemies, player at {home}");
            Vector3 spot = home;
            void Home()
            {
                hero.transform.position = spot;
                if (hbody != null) hbody.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
                hero.FaceRight();
            }
            foreach (var pair in entries)
            {
                var poolsBefore = AiSpawnedPoolRoots();
                Home();
                mod.Stats.FullHeal();
                yield return new WaitForSeconds(0.3f);
                BookSpawner.Spawn(pair.Key);
                Brain brain = null;
                float found = Time.realtimeSinceStartup + 16f;
                while (Time.realtimeSinceStartup < found)
                {
                    brain = Object.FindObjectsByType<Brain>(FindObjectsSortMode.None).FirstOrDefault(b => b.Record == pair.Key && b.name.Contains("图鉴"));
                    if (brain != null) break;
                    yield return null;
                }
                if (brain == null)
                {
                    Plugin.Log.LogInfo($"[NEWAI_MOVE] {pair.Key} unavailable: {BookSpawner.DevLast}");
                    continue;
                }
                var go = brain.gameObject;
                var erb = go.GetComponent<Rigidbody2D>();
                var p = pair.Value;
                yield return new WaitForSeconds(0.6f);
                for (int i = 0; i < p.Moves.Count; i++)
                {
                    if (brain == null) break;
                    var m = p.Moves[i];
                    string head = $"{pair.Key} #{i} {m.Kind} tier={m.Tier} when={m.When} rel={m.Rel} look={m.Look}";

                    float idle = Time.time + 6f;
                    while (brain != null && brain.DevState.Contains(":") && Time.time < idle) yield return null;
                    if (brain == null) break;
                    if (brain.DevState.Contains(":")) AiCall(brain, "End", false);
                    // The move's own reach: the middle of its range (no farther than 8 units), inside a ring's radius.
                    bool ring = m.Kind == Kind.Pulse || m.Kind == Kind.Cloud;
                    float tiles = ring ? Mathf.Clamp(m.Radius * 0.6f, 1f, Mathf.Max(1f, m.Max)) : Mathf.Clamp((m.Min + Mathf.Min(m.Max, 16f)) * 0.5f, m.Min + 1f, Mathf.Max(m.Min + 1f, m.Max - 1f));
                    float dist = Mathf.Max(1.2f, tiles * 0.5f);
                    spot = home;
                    Home();
                    if (erb != null)
                    {
                        var at = new Vector2(home.x + dist, p.Flyer ? home.y + 1.5f : Mathf.Max(erb.position.y, home.y - 0.5f));
                        if (m.Rel == Rel.Below) at.y = home.y + 3f;
                        // A drop falls straight, so over him; a crawler drops from a ceiling, so upside down.
                        if (m.Kind == Kind.Drop) at.x = home.x;
                        if (m.Kind == Kind.Drop && go.GetComponent<Climber>() == null) go.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
                        erb.position = at;
                        go.transform.position = new Vector3(at.x, at.y, go.transform.position.z);
                        erb.linearVelocity = Vector2.zero;
                        Physics2D.SyncTransforms();
                    }
                    mod.Stats.FullHeal();
                    yield return new WaitForSeconds(0.25f);
                    if (brain == null) break;
                    // Its body there (one still coming out of the ground, a Mantis Traitor's spawn, has it off a while).
                    var own = go.GetComponent<Collider2D>();
                    for (float present = Time.time + 4f; own != null && !own.enabled && Time.time < present;) yield return null;
                    if (brain == null) break;
                    // Every move meets a player who can be hurt (the last one's immunity over).
                    for (float vulnerable = Time.time + 3f; hero.cState.invulnerable && Time.time < vulnerable;) yield return null;
                    if (brain == null) break;
                    // Then he goes where the move wants him, from its body as it is now (one its own script holds in place, a
                    // plant in the ground or a jelly on its path, isn't where it was put): under it for a move at what's below,
                    // over it for one at what's above, inside a ring, else beside it on an open side.
                    {
                        Vector2 heroOff = hcol != null ? (Vector2)hcol.bounds.center - (Vector2)hero.transform.position : new Vector2(0f, 0.6f);
                        Vector2 e = brain.Position;
                        float want = ring ? Mathf.Max(0.4f, m.Radius * 0.3f) : dist;
                        float up = Mathf.Max(1.8f, want);
                        var tries = m.Rel == Rel.Below || m.Kind == Kind.Drop
                            ? new[] { new Vector2(0f, -up), new Vector2(0.5f, -up), new Vector2(-0.5f, -up) }
                            : m.Rel == Rel.Above
                                ? new[] { new Vector2(0.3f, up), new Vector2(-0.3f, up), new Vector2(1f, up) }
                                : new[] { new Vector2(-want, 0f), new Vector2(want, 0f), new Vector2(-want, 0.8f), new Vector2(want, 0.8f) };
                        foreach (var off in tries)
                        {
                            var c = e + off;
                            if (Hostiles.NativeSolidAt(c) || Hostiles.NativeSolidAt(c + Vector2.up * 0.5f) || Hostiles.NativeSolidAt(c - Vector2.up * 0.5f)) continue;
                            spot = new Vector3(c.x - heroOff.x, c.y - heroOff.y, home.z);
                            Home();
                            break;
                        }
                    }
                    int hits0 = EnemyAi.EnemyAi.DevHits, shots0 = EnemyAi.EnemyAi.DevShots, areas0 = EnemyAi.EnemyAi.DevAreas, debuffs0 = PlayerDebuffs.DevApplied;
                    float lastHurt = Player.TerraHurt.LastHurtAt;
                    int hurts = 0;
                    bool begun = true;
                    float real = Vector2.Distance(brain.Position, hcol != null ? (Vector2)hcol.bounds.center : (Vector2)hero.transform.position);
                    int touched = 0, open = 0;
                    float closest = 99f;
                    // How it turned as the move began: its pivot, its body and its picture across (a turn about an off-centre pivot jumps).
                    var pic = go.GetComponent<Renderer>();
                    float tx0 = go.transform.position.x, bx0 = brain.Position.x, px0 = pic != null ? pic.bounds.center.x : 0f, turnAt = Time.time + 0.15f;
                    string turn = "";
                    try { AiCall(brain, "Begin", i, Time.time); }
                    catch (Exception e) { begun = false; Plugin.Log.LogWarning($"[NEWAI_MOVE] {head} begin failed: {e.InnerException ?? e}"); }
                    float end = Time.time + 7f;
                    bool finished = false;
                    string last = "";
                    var motion = new AiMotion();
                    var ebody = go.GetComponent<Collider2D>();
                    while (begun && Time.time < end)
                    {
                        if (brain == null) break;
                        if (Player.TerraHurt.LastHurtAt != lastHurt) { lastHurt = Player.TerraHurt.LastHurtAt; hurts++; }
                        // He stands in what it makes: no knockback carries him out of a shot's way or an area.
                        if (m.Kind > Kind.Drop && m.Kind != Kind.Pull && ((Vector2)hero.transform.position - (Vector2)spot).sqrMagnitude > 0.04f) Home();
                        if (turn == "" && Time.time >= turnAt && go != null)
                            turn = $"{go.transform.position.x - tx0:F2}/{brain.Position.x - bx0:F2}/{(pic != null ? pic.bounds.center.x - px0 : 0f):F2}";
                        // Its body's nearest to his (below 0: overlapping).
                        if (hcol != null && ebody != null && ebody.enabled) closest = Mathf.Min(closest, ebody.Distance(hcol).distance);
                        // A live shot or area on the player (its hurt may wait on his immunity).
                        if (hcol != null && Hostiles.Attack.All.Any(a => a != null && a.Hurts && a.Col.Distance(hcol).isOverlapped))
                        {
                            touched++;
                            if (!hero.cState.invulnerable) open++;
                        }
                        string st = brain.DevState;
                        bool vanishes = AiGet<bool>(brain, "hidden") || st.StartsWith("Blink") || st.StartsWith("Burrow");
                        motion.Step(brain.Position, ebody, st, vanishes, 60f, st.Contains(":"));
                        if (st.Contains(":")) last = st;
                        else if (!finished) { finished = true; end = Mathf.Min(end, Time.time + 1.5f); }
                        if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                        yield return null;
                    }
                    Plugin.Log.LogInfo($"[NEWAI_MOVE] {head} dist={dist:F1} real={real:F1} closest={closest:F2} turn={turn} touched={touched} open={open} hurts={hurts} aiHits={EnemyAi.EnemyAi.DevHits - hits0} shots={EnemyAi.EnemyAi.DevShots - shots0} " +
                        $"areas={EnemyAi.EnemyAi.DevAreas - areas0} debuffs={PlayerDebuffs.DevApplied - debuffs0} finished={finished} last={last} now={(brain != null ? brain.DevState : "gone")} {motion.Report}");
                    if (brain != null && brain.DevState.Contains(":")) AiCall(brain, "End", false);
                    foreach (var a in Hostiles.Attack.All.ToArray()) if (a != null) Object.Destroy(a.gameObject);
                    yield return new WaitForSeconds(0.3f);
                }
                if (go != null) Object.Destroy(go);
                foreach (var a in Hostiles.Attack.All.ToArray()) if (a != null) Object.Destroy(a.gameObject);
                foreach (var root in AiSpawnedPoolRoots())
                    if (root != null && !poolsBefore.Contains(root)) ObjectPool.Recycle(root);
                yield return new WaitForSeconds(0.2f);
            }
            Plugin.Log.LogInfo("[NEWAI_DONE]");
        }
    }
}
