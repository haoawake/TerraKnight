using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HutongGames.PlayMaker;
using TerraKnight.EnemyAi;
using TerraKnight.EnemyAi.Bosses;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        static readonly string[] AllBossKits =
        {
            "FalseKnight", "BigFly", "Mawlek", "Hornet", "MageKnight", "BigBuzzer", "MegaMossCharger", "MantisLord", "MageLord",
            "DungDefender", "FlukeMother", "MegaBeamMiner", "MimicSpider", "BlackKnight", "InfectedKnight", "JarCollector",
            "Oblobble", "HiveKnight", "LobsterLancer", "TraitorLord", "NailBros", "Nailsage", "Paintmaster", "Grimm",
            "NightmareGrimm", "GreyPrince", "WhiteDefender", "MegaJellyfish", "HollowKnight", "HollowKnightPrime", "FinalBoss",
            "GhostXero", "GhostAladar", "GhostHu", "GhostMarmu", "GhostNoEyes", "GhostGalien", "GhostMarkoth",
        };

        /// <summary>
        /// Each boss with its new kit fought in its own arena by a player who only stands (immortal at one life, not
        /// invincible: every hit counts), put back by the boss after each hit: the kit's moves, the hits on the player (all of
        /// them, and the new AI's own shots' and areas'), the kit's failures, rescues and jumps. One [NEWAI_BOSS_COMBAT] line
        /// a boss. TK_NEWAI_BOSSES picks some (a;b), TK_NEWAI_SECONDS the watch (30 s).
        /// </summary>
        static readonly System.Reflection.FieldInfo dormant = AccessTools.Field(typeof(BossKit), "Dormant");

        /// <summary>A body still waiting for its turn (a Watcher Knight at rest, a Nailmaster at the door).</summary>
        static readonly System.Text.RegularExpressions.Regex bossWaits = new System.Text.RegularExpressions.Regex("Rest|Sleep|Dormant|Entry|Hanging|Inert");

        /// <summary>The kit of a record to watch: one already fighting before one still waiting its turn.</summary>
        BossKit KitOf(string record, BossKit not = null)
        {
            var all = Object.FindObjectsByType<BossKit>(FindObjectsSortMode.None)
                .Where(k => k != not && k.Record == record && k.gameObject.activeInHierarchy && !(bool)dormant.GetValue(k)).ToArray();
            return all.FirstOrDefault(k => !bossWaits.IsMatch(k.StateNow ?? "")) ?? (not == null ? all.FirstOrDefault() : null);
        }

        static Fsm probeFsm;
        static readonly Dictionary<string, int> probed = new Dictionary<string, int>();
        static Harmony prober;

        static void ProbeEvent(Fsm __instance, FsmEvent fsmEvent)
        {
            if (__instance != probeFsm || fsmEvent == null) return;
            string n = fsmEvent.Name + "@" + (__instance.ActiveStateName ?? "");
            probed[n] = probed.TryGetValue(n, out int c) ? c + 1 : 1;
        }

        IEnumerator ProbeEvents(string key, Fsm fsm, float seconds)
        {
            if (prober == null)
            {
                prober = new Harmony("tk.dev.eventprobe");
                prober.Patch(AccessTools.Method(typeof(Fsm), nameof(Fsm.Event), new[] { typeof(FsmEvent) }), prefix: new HarmonyMethod(typeof(DevAutoTest), nameof(ProbeEvent)));
            }
            probed.Clear();
            probeFsm = fsm;
            yield return new WaitForSeconds(seconds);
            probeFsm = null;
            Plugin.Log.LogInfo($"[NEWAI_BOSS_COMBAT] {key} events in {seconds} s: {string.Join(", ", probed.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + "×" + kv.Value))}");
        }

        IEnumerator NewAiBossCombat()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            string pick = Environment.GetEnvironmentVariable("TK_NEWAI_BOSSES");
            var keys = string.IsNullOrWhiteSpace(pick) ? AllBossKits : pick.Split(';');
            float watch = float.TryParse(Environment.GetEnvironmentVariable("TK_NEWAI_SECONDS"), out var w) ? w : 45f;
            // TK_NEWAI_HANG=1: the player held up under the room's ceiling over the arena the whole fight (a hook hanging
            // from the top), moved left, middle, right every 6 s: what of each boss reaches him there.
            bool hang = Environment.GetEnvironmentVariable("TK_NEWAI_HANG") == "1";
            // How high each boss gets against where the hung player is and the ceiling there (an invisible wall shows as a top short of both).
            float bossTop = float.MinValue, heldY = 0f, roofY = 0f;
            foreach (string key in keys.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                bossTop = float.MinValue;
                string record = key.Split('@')[0];
                PlayerData.instance.isInvincible = false;
                if (BossArena.Has(key)) BossArena.Fight(key, true);
                else
                {
                    string scene = EnemyDrops.SceneOf(record);
                    if (string.IsNullOrEmpty(scene)) { Plugin.Log.LogInfo($"[NEWAI_BOSS_COMBAT] {key} unavailable: no scene"); continue; }
                    yield return GoTo(scene, EntryGateOf(scene));
                }
                BossKit kit = null;
                float started = Time.realtimeSinceStartup, until = started + 70f, clearAt = started + 8f;
                int cleared = 0;
                while (Time.realtimeSinceStartup < until)
                {
                    kit = KitOf(record);
                    if (kit != null && !GameManager.instance.IsInSceneTransition) break;
                    // An arena's waves before its boss (the Traitor Lord's Mantis Traitors): the player who only stands has them
                    // put down, as a fighter would.
                    if (Time.realtimeSinceStartup >= clearAt && !GameManager.instance.IsInSceneTransition)
                    {
                        clearAt = Time.realtimeSinceStartup + 2f;
                        foreach (var h in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                        {
                            var info = Items.EnemyStats.Of(h);
                            if (h == null || h.isDead || !h.gameObject.activeInHierarchy || info == null || info.Boss || info.Record == record) continue;
                            if (!h.gameObject.scene.name.StartsWith(GameManager.instance.sceneName)) continue;
                            h.Die(null, AttackTypes.Generic, true);
                            cleared++;
                        }
                    }
                    yield return null;
                }
                if (cleared > 0) Plugin.Log.LogInfo($"[NEWAI_BOSS_COMBAT] {key} cleared {cleared} wave enemies before the boss");
                if (kit == null)
                {
                    // Who is there instead: each enemy's record, whether it is up, and the kit it has.
                    var seen = Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Select(h => $"{h.name}@{h.gameObject.scene.name}:{Items.EnemyStats.Of(h)?.Record ?? "-"}{(h.gameObject.activeInHierarchy ? "" : "(off)")}{(h.GetComponent<BossKit>() is BossKit k ? "+kit" + ((bool)dormant.GetValue(k) ? "(dormant)" : "") : "")}");
                    Plugin.Log.LogInfo($"[NEWAI_BOSS_COMBAT] {key} unavailable: no kit in {GameManager.instance.sceneName}; {string.Join(", ", seen.Take(20))}");
                    // And where the room's own scripts that start it stand.
                    var scripts = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Where(f => f.gameObject.scene.name.StartsWith(GameManager.instance.sceneName)
                            && System.Text.RegularExpressions.Regex.IsMatch(f.gameObject.name + "|" + f.FsmName, "Ghost|Battle|Boss|Appear|Dream|Conversation|Warrior"))
                        .Select(f => $"{f.gameObject.name}/{f.FsmName}:{f.ActiveStateName}{(f.gameObject.activeInHierarchy && f.enabled ? "" : "(off)")}");
                    Plugin.Log.LogInfo($"[NEWAI_BOSS_COMBAT] {key} scripts: {string.Join(", ", scripts.Take(30))}");
                    var loaded = new List<string>();
                    for (int si = 0; si < UnityEngine.SceneManagement.SceneManager.sceneCount; si++) loaded.Add(UnityEngine.SceneManagement.SceneManager.GetSceneAt(si).name);
                    var warrior = GameObject.Find("Warrior");
                    Plugin.Log.LogInfo($"[NEWAI_BOSS_COMBAT] {key} scenes: {string.Join(", ", loaded)}; Warrior → {(warrior != null ? warrior.scene.name : "none")}");
                    continue;
                }
                var hero = HeroController.instance;
                var hbody = hero.GetComponent<Rigidbody2D>();
                Vector2 Stand()
                {
                    var at = kit.ClampToArena(kit.Me, 3f);
                    at.y = kit.GroundBelow(at) + 1.2f;
                    float side = at.x - 4f > kit.Arena.xMin + 1f ? -4f : 4f;
                    return new Vector2(at.x + side, at.y);
                }
                void PutBack()
                {
                    var at = Stand();
                    hero.transform.position = new Vector3(at.x, at.y, hero.transform.position.z);
                    if (hbody != null) hbody.linearVelocity = Vector2.zero;
                    Physics2D.SyncTransforms();
                }
                PutBack();
                mod.Stats.FullHeal();
                yield return new WaitForSeconds(2f);
                int moves0 = BossAi.DevMoves, fails0 = BossAi.DevFails, unstuck0 = BossAi.DevUnstuck, aiHits0 = EnemyAi.EnemyAi.DevHits;
                int shots0 = EnemyAi.EnemyAi.DevShots, areas0 = EnemyAi.EnemyAi.DevAreas;
                int hurts = 0, jumps = 0, frames = 0, invulnerable = 0, invincible = 0, touched = 0, open = 0;
                var hcol = hero.GetComponent<Collider2D>();
                float lastHurt = Player.TerraHurt.LastHurtAt, putBackAt = -1f, maxStep = 0f;
                Vector2 prev = kit.transform.position;
                var states = new HashSet<string>();
                bool died = false;
                var motion = new AiMotion();
                var kbody = kit.GetComponent<Collider2D>();
                float t0 = Time.time;
                // Nothing going on a while: another body of it that is fighting is watched instead; else it is struck once
                // (a boss waiting for the first blow: the Vengefly King hanging, the Flukemarm resting).
                string lastState = kit.StateNow;
                float stateSince = Time.time, lastNudge = Time.time;
                int switches = 0, wakeHits = 0;
                bool stallLogged = false;
                // Its life brought down a third of the way through and again at two thirds: the moves of its second and third
                // ranks (below two thirds, below one third of its life) come in, each counted.
                int rankNow = 0;
                var rankMoves = new int[3];
                var rankNew = new int[3];
                int rankFrom = BossAi.DevMoves, newFrom = BossAi.DevNewStates, hooks0 = BossAi.DevHooks, branches0 = BossAi.DevBranches, locks0 = BossAi.DevLocks, air0 = BossAi.DevAntiAir;
                while (Time.time - t0 < watch)
                {
                    if (kit == null || kit.Hm == null) break;
                    if (kit.Hm.isDead) { died = true; break; }
                    if (!string.IsNullOrEmpty(kit.StateNow)) states.Add(kit.StateNow);
                    int rankWanted = Mathf.Min(2, (int)((Time.time - t0) / (watch / 3f)));
                    if (rankWanted > rankNow && kit.Info != null)
                    {
                        rankMoves[rankNow] = BossAi.DevMoves - rankFrom;
                        rankFrom = BossAi.DevMoves;
                        rankNew[rankNow] = BossAi.DevNewStates - newFrom;
                        newFrom = BossAi.DevNewStates;
                        rankNow = rankWanted;
                        int max = Mathf.Max(1, kit.Info.MaxHp);
                        kit.Hm.hp = Mathf.Min(kit.Hm.hp, Mathf.RoundToInt(max * (rankNow == 1 ? 0.6f : 0.3f)));
                        // A blow with it, as its own scripts look at their life on a hit (Galien's summons, phase checks).
                        kit.Hm.Hit(new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 1, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                    }
                    if (kit.StateNow != lastState) { lastState = kit.StateNow; stateSince = Time.time; }
                    // Held in one state a long while: every script on it then, for what holds it.
                    if (!stallLogged && Time.time - stateSince > 6f)
                    {
                        stallLogged = true;
                        var all = kit.GetComponentsInChildren<PlayMakerFSM>(true).Select(f => $"{f.gameObject.name}/{f.FsmName}:{f.ActiveStateName}{(f.enabled && f.gameObject.activeInHierarchy ? "" : "(off)")}");
                        Plugin.Log.LogInfo($"[NEWAI_BOSS_COMBAT] {key} stalled in {kit.StateNow} {Time.time - stateSince:F1} s: {string.Join(", ", all.Take(20))}");
                        // Its state's actions: running or done, and a wait's clock.
                        var control = (PlayMakerFSM)AccessTools.Field(typeof(BossKit), "Control").GetValue(kit);
                        var st = control != null ? control.Fsm.ActiveState : null;
                        if (st != null)
                            Plugin.Log.LogInfo($"[NEWAI_BOSS_COMBAT] {key} {st.Name} actions: " + string.Join(", ", st.Actions.Select(a =>
                                $"{a.GetType().Name}{(a.Enabled ? "" : "(off)")}{(a.Finished ? "(done)" : "")}" +
                                (a is HutongGames.PlayMaker.Actions.WaitRandom wr ? $" {AccessTools.Field(typeof(HutongGames.PlayMaker.Actions.WaitRandom), "timer").GetValue(wr)}/{AccessTools.Field(typeof(HutongGames.PlayMaker.Actions.WaitRandom), "time").GetValue(wr)}" : ""))) +
                                $"; manual={control.Fsm.ManualUpdate} paused={EnemyAi.EnemyAi.Paused.Contains(control.Fsm)} enabled={control.enabled}; " +
                                string.Join(" ", st.Transitions.Select(t => $"{t.EventName}->{(string.IsNullOrEmpty(t.ToState) ? "∅" : t.ToState)}")));
                        // What it is told meanwhile (the events its script gets, a second and a half).
                        if (control != null) mod.StartCoroutine(ProbeEvents(key, control.Fsm, 1.5f));
                    }
                    if (Time.time - stateSince > 4f && Time.time - lastNudge > 4f)
                    {
                        lastNudge = Time.time;
                        var other = KitOf(record, kit);
                        if (other != null && !bossWaits.IsMatch(other.StateNow ?? ""))
                        {
                            kit = other;
                            kbody = kit.GetComponent<Collider2D>();
                            prev = kit.transform.position;
                            lastState = kit.StateNow;
                            stateSince = Time.time;
                            switches++;
                            continue;
                        }
                        if (wakeHits < 3)
                        {
                            wakeHits++;
                            // As a player's first blow wakes it (BossWake), else a plain one with the "TAKE DAMAGE" a swing
                            // sends (MeleeHits: the Vengefly King hanging waits for that).
                            if (!Items.BossWake.Rouse(kit.Hm))
                            {
                                kit.Hm.Hit(new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 1, Direction = kit.Me.x > hero.transform.position.x ? 0f : 180f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                                FSMUtility.SendEventToGameObject(kit.gameObject, "TAKE DAMAGE");
                            }
                        }
                    }
                    // The player able to be hurt at all (a fight's opening left his invincibility on once).
                    frames++;
                    if (hero.cState.invulnerable) invulnerable++;
                    if (PlayerData.instance.GetBool("isInvincible")) invincible++;
                    // The new AI's own shots and areas on him (and not while he is immune).
                    if (hcol != null && Hostiles.Attack.All.Any(a => a != null && a.Hurts && a.Col.Distance(hcol).isOverlapped))
                    {
                        touched++;
                        if (!hero.cState.invulnerable) open++;
                    }
                    bool shown = kit.GetComponentsInChildren<Renderer>().Any(r => r != null && r.enabled);
                    motion.Step(kit.Me, kbody, kit.StateNow ?? "", !shown, 80f, true);
                    Vector2 pos = kit.transform.position;
                    float step = Vector2.Distance(pos, prev);
                    maxStep = Mathf.Max(maxStep, step);
                    if (step > 100f * Mathf.Max(Time.deltaTime, Time.fixedDeltaTime) + 0.5f) jumps++;
                    prev = pos;
                    if (Player.TerraHurt.LastHurtAt != lastHurt)
                    {
                        lastHurt = Player.TerraHurt.LastHurtAt;
                        hurts++;
                        putBackAt = Time.time + 0.8f;
                    }
                    // The second half of each rank up in the air, well over the floor, hit or not (its moves against the
                    // air want him kept up there 2.5 s and more).
                    float seg = (Time.time - t0) % (watch / 3f);
                    bool airPhase = seg > 7f && seg < watch / 3f - 0.5f;
                    if (kit != null && kit.Hm != null && kit.Hm.GetComponent<Collider2D>() is Collider2D bossBody && bossBody.enabled)
                        bossTop = Mathf.Max(bossTop, bossBody.bounds.max.y);
                    // Held up only once the fight is on (a boss waiting buried takes its arena from under the floor first).
                    if (hang && kit.Engaged)
                    {
                        putBackAt = -1f;
                        int spot = (int)((Time.time - t0) / 6f) % 3;
                        float hx = Mathf.Lerp(kit.Arena.xMin + 3f, kit.Arena.xMax - 3f, spot * 0.5f);
                        float top = kit.CeilingAbove(new Vector2(hx, kit.GroundBelow(new Vector2(hx, kit.Arena.center.y)) + 1f), 40f);
                        var held = new Vector2(hx, Mathf.Min(top, kit.Arena.yMax) - 1.6f);
                        heldY = held.y;
                        roofY = top;
                        hero.transform.position = new Vector3(held.x, held.y, hero.transform.position.z);
                        if (hbody != null) hbody.linearVelocity = Vector2.zero;
                        if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                        yield return null;
                        continue;
                    }
                    if (!airPhase && putBackAt > 0f && Time.time >= putBackAt) { PutBack(); putBackAt = -1f; }
                    if (airPhase)
                    {
                        putBackAt = -1f;
                        var up = Stand() + Vector2.up * 3.5f;
                        if (((Vector2)hero.transform.position - up).sqrMagnitude > 0.04f)
                        {
                            hero.transform.position = new Vector3(up.x, up.y, hero.transform.position.z);
                            if (hbody != null) hbody.linearVelocity = Vector2.zero;
                        }
                    }
                    if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                    yield return null;
                }
                rankMoves[rankNow] = BossAi.DevMoves - rankFrom;
                rankNew[rankNow] = BossAi.DevNewStates - newFrom;
                Plugin.Log.LogInfo($"[NEWAI_BOSS_COMBAT] {key} scene={GameManager.instance.sceneName} kitMoves={BossAi.DevMoves - moves0} byRank={rankMoves[0]}/{rankMoves[1]}/{rankMoves[2]} newStates={rankNew[0]}/{rankNew[1]}/{rankNew[2]} hooks={BossAi.DevHooks - hooks0} branches={BossAi.DevBranches - branches0} locks={BossAi.DevLocks - locks0} antiAir={BossAi.DevAntiAir - air0} rank={(kit != null ? kit.Rank : -1)} hurts={hurts} aiShots={EnemyAi.EnemyAi.DevShots - shots0} " +
                    $"aiAreas={EnemyAi.EnemyAi.DevAreas - areas0} aiHits={EnemyAi.EnemyAi.DevHits - aiHits0} touched={touched} open={open} fails={BossAi.DevFails - fails0} unstuck={BossAi.DevUnstuck - unstuck0} " +
                    $"bossTop={bossTop:F1} heldY={heldY:F1} roof={roofY:F1} arenaTop={(kit != null ? kit.Arena.yMax : 0f):F1} hornetRoof={HornetKit.DevRoofRaised} " +
                    $"jumps={jumps} maxStep={maxStep:F2} died={died} switches={switches} wakeHits={wakeHits} invulnerable={invulnerable * 100 / Mathf.Max(1, frames)}% invincible={invincible * 100 / Mathf.Max(1, frames)}% {motion.Report} states={states.Count}: {string.Join(",", states.Take(40))}");
                foreach (var a in Hostiles.Attack.All.ToArray()) if (a != null) Object.Destroy(a.gameObject);
            }
            Plugin.Log.LogInfo("[NEWAI_DONE]");
        }
    }
}
