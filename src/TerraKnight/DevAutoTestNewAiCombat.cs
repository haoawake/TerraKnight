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
        /// Every new-AI enemy let loose on a player who stands still (immortal, put back after each hit): what it does in
        /// its own time and whether any of it lands. The audit drives only each move's lifecycle; this watches the fight.
        /// One line an enemy, [NEWAI_COMBAT], for the report. TK_NEWAI_PROFILES picks some (a;b;c), TK_NEWAI_SECONDS the
        /// watch (8 s), TK_NEWAI_DIFFICULTY the difficulty (Master by default: every tier's moves), TK_NEWAI_STRIKE=1 a
        /// blow on it every 3 s (one point of its life), as the player's wake a kind that sees nothing.
        /// </summary>
        IEnumerator NewAiCombat()
        {
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            var difficultyWas = World.GameDifficulty.Current;
            World.GameDifficulty.Current = Enum.TryParse(Environment.GetEnvironmentVariable("TK_NEWAI_DIFFICULTY"), out World.DifficultyLevel level) ? level : World.DifficultyLevel.Master;
            var hero = HeroController.instance;
            Vector3 home = hero.transform.position;
            _ = Profiles.Count;
            var profiles = (Dictionary<string, Profile>)AccessTools.Field(typeof(Profiles), "all").GetValue(null);
            var selected = (Environment.GetEnvironmentVariable("TK_NEWAI_PROFILES") ?? "").Split(';');
            var entries = profiles.Where(p => selected.Length == 1 && selected[0] == "" || selected.Contains(p.Key)).ToArray();
            float watch = float.TryParse(Environment.GetEnvironmentVariable("TK_NEWAI_SECONDS"), out var w) ? w : 8f;
            bool strike = Environment.GetEnvironmentVariable("TK_NEWAI_STRIKE") == "1";
            Plugin.Log.LogInfo($"[NEWAI_COMBAT] start {entries.Length} enemies, {watch} s each, player at {home}, {World.GameDifficulty.Current}");
            foreach (var pair in entries)
            {
                var poolsBefore = AiSpawnedPoolRoots();
                hero.transform.position = home;
                Physics2D.SyncTransforms();
                hero.FaceRight();
                mod.Stats.FullHeal();
                yield return new WaitForSeconds(0.3f);
                BookSpawner.Spawn(pair.Key);
                Brain brain = null;
                float end = Time.realtimeSinceStartup + 16f;
                while (Time.realtimeSinceStartup < end)
                {
                    brain = Object.FindObjectsByType<Brain>(FindObjectsSortMode.None).FirstOrDefault(b => b.Record == pair.Key && b.name.Contains("图鉴"));
                    if (brain != null) break;
                    yield return null;
                }
                if (brain == null)
                {
                    Plugin.Log.LogInfo($"[NEWAI_COMBAT] {pair.Key} unavailable: {BookSpawner.DevLast}");
                    continue;
                }
                var go = brain.gameObject;
                var hm = go.GetComponent<HealthManager>();
                Vector2 start = brain.Position;
                int moves0 = Brain.DevMoves, fails0 = Brain.DevFails, shots0 = EnemyAi.EnemyAi.DevShots, areas0 = EnemyAi.EnemyAi.DevAreas;
                int hits0 = EnemyAi.EnemyAi.DevHits, debuffs0 = PlayerDebuffs.DevApplied, seize0 = Brain.DevSeizes;
                int hurts = 0;
                float lastHurt = Player.TerraHurt.LastHurtAt, minY = start.y, maxY = start.y, drift = 0f, nearest = 999f, putBack = -1f;
                var states = new List<string>();
                var own = new List<string>();
                var ownFrames = new Dictionary<string, int>();
                // Invincible all round: what its scripts were doing then.
                var invFrames = new Dictionary<string, int>();
                var controls = AiGet<PlayMakerFSM[]>(brain, "controls");
                // Why no move was taken, frame by frame while it stood alert: the choice's reason, else each move's.
                var why = new Dictionary<string, int>();
                bool died = false, vanished = false;
                var motion = new AiMotion();
                var ebody = go.GetComponent<Collider2D>();
                float t0 = Time.time, nextStrike = Time.time + 0.5f;
                while (Time.time - t0 < watch)
                {
                    if (brain == null || go == null) { vanished = true; break; }
                    if (hm != null && hm.isDead) { died = true; break; }
                    if (strike && hm != null && Time.time >= nextStrike && hm.hp > 1)
                    {
                        hm.hp -= 1;
                        nextStrike = Time.time + 3f;
                    }
                    var p = brain.Position;
                    minY = Mathf.Min(minY, p.y);
                    maxY = Mathf.Max(maxY, p.y);
                    drift = Mathf.Max(drift, Mathf.Abs(p.x - start.x));
                    nearest = Mathf.Min(nearest, Vector2.Distance(p, hero.transform.position));
                    string st = brain.DevState;
                    if ((states.Count == 0 || states[states.Count - 1] != st) && states.Count < 30) states.Add(st);
                    if (st == "alert")
                    {
                        string c = AiGet<string>(brain, "chooseWhy");
                        var each = AiGet<string[]>(brain, "whyNot");
                        if (c != null) why[c] = why.TryGetValue(c, out int k) ? k + 1 : 1;
                        else if (each != null)
                            for (int i = 0; i < each.Length; i++)
                                if (each[i] != null) { string key = i + ":" + each[i]; why[key] = why.TryGetValue(key, out int k2) ? k2 + 1 : 1; }
                    }
                    if (controls != null && own.Count < 40)
                        foreach (var f in controls)
                        {
                            if (f == null || !f.enabled) continue;
                            string s = f.FsmName + "." + f.ActiveStateName;
                            if (!own.Contains(s)) own.Add(s);
                            ownFrames[s] = ownFrames.TryGetValue(s, out int c) ? c + 1 : 1;
                            if (hm != null && hm.IsInvincible && hm.InvincibleFromDirection == 0 && f.ActiveStateName != "")
                                invFrames[s] = invFrames.TryGetValue(s, out int c2) ? c2 + 1 : 1;
                        }
                    bool vanishes = AiGet<bool>(brain, "hidden") || st.StartsWith("Blink") || st.StartsWith("Burrow");
                    motion.Step(p, ebody, st, vanishes, 60f, st.Contains(":"));
                    if (Player.TerraHurt.LastHurtAt != lastHurt)
                    {
                        lastHurt = Player.TerraHurt.LastHurtAt;
                        hurts++;
                        putBack = Time.time + 0.5f;
                    }
                    if (putBack > 0f && Time.time >= putBack)
                    {
                        hero.transform.position = home;
                        Physics2D.SyncTransforms();
                        putBack = -1f;
                    }
                    if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                    yield return null;
                }
                bool shown = go != null && go.GetComponentsInChildren<Renderer>().Any(r => r != null && r.enabled && r.gameObject.activeInHierarchy);
                Plugin.Log.LogInfo($"[NEWAI_COMBAT] {pair.Key} paced={Brain.DevPaced} moves={Brain.DevMoves - moves0} seizes={Brain.DevSeizes - seize0} shots={EnemyAi.EnemyAi.DevShots - shots0} areas={EnemyAi.EnemyAi.DevAreas - areas0} " +
                    $"aiHits={EnemyAi.EnemyAi.DevHits - hits0} hurts={hurts} debuffs={PlayerDebuffs.DevApplied - debuffs0} fails={Brain.DevFails - fails0} " +
                    $"fell={start.y - minY:F1} rose={maxY - start.y:F1} drift={drift:F1} nearest={nearest:F1} died={died} vanished={vanished} shown={shown} {motion.Report} states={string.Join(">", states)} why={string.Join(",", why.OrderByDescending(kv => kv.Value).Take(8).Select(kv => kv.Key + "×" + kv.Value))} own={string.Join(",", own)} ownFrames={string.Join(",", ownFrames.OrderByDescending(kv => kv.Value).Take(8).Select(kv => kv.Key + "×" + kv.Value))} inv={(hm != null && hm.IsInvincible ? hm.InvincibleFromDirection.ToString() : "-")} hp={(hm != null ? hm.hp : -1)} body={(go != null && go.GetComponent<Collider2D>() is Collider2D bc ? bc.enabled.ToString() : "none")} boss={EnemyAi.EnemyAi.DevBossNear} invFrames={string.Join(",", invFrames.OrderByDescending(kv => kv.Value).Take(6).Select(kv => kv.Key + "×" + kv.Value))}");
                if (go != null) Object.Destroy(go);
                foreach (var a in Hostiles.Attack.All.ToArray()) if (a != null) Object.Destroy(a.gameObject);
                foreach (var root in AiSpawnedPoolRoots())
                    if (root != null && !poolsBefore.Contains(root)) ObjectPool.Recycle(root);
                yield return new WaitForSeconds(0.2f);
            }
            World.GameDifficulty.Current = difficultyWas;
            Plugin.Log.LogInfo("[NEWAI_DONE]");
        }
    }
}
