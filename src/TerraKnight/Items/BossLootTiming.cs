using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// A boss's loot falls when its death is played out, not at its last point of health (the user's): the False Knight's
    /// maggot bursting out of the fallen armour, the Gruz Mother's body bursting, the Soul Master blowing apart. Each boss
    /// dies its own way, so its body is watched (its own object and the corpse its EnemyDeathEffects puts out): the loot
    /// comes where it was last seen once the body is gone or its bursting state and animation have finished.
    /// Persistent bodies (the Mantis Lords bowing) settle after their animation and state stop changing.
    /// Left before then (its room gone), the loot goes into the bag as a fight that leaves its
    /// scene by itself has it.
    /// </summary>
    [HarmonyPatch]
    internal static class BossLootTiming
    {
        public static bool Enabled = true;
        /// <summary>Stable idle time for a persistent corpse; never interrupts a playing death animation.</summary>
        public const float MaxWait = 6f;
        public static int DevDeferred, DevByGone, DevByBurst, DevByTime, DevByLeft;
        public static string DevLastWhy = "";

        sealed class Pending
        {
            public string Record, Scene, Name;
            public string LifeReward;
            public bool? FirstKill;
            public GameObject Body;
            public EnemyDeathEffects Effects;
            public GameObject Corpse;
            public Vector2 At;
            public float Since;
            public int GoneFrames;
            public bool Burst;
            public float StableSince;
            public string StateKey;
            public float NextCorpseScan;
            public readonly List<GameObject> Related = new List<GameObject>();
        }

        static readonly List<Pending> pending = new List<Pending>();
        internal static void Emitted(EnemyDeathEffects effects, GameObject body)
        {
            foreach (var p in pending) if (ReferenceEquals(p.Effects, effects)) p.Corpse = body;
        }

        // Death effects may be destroyed before the separately spawned corpse finishes animating.
        static GameObject Corpse(Pending p)
        {
            var current = BossCorpseTracking.Of(p.Effects);
            if (current != null) p.Corpse = current;
            return p.Corpse;
        }

        // Some boss FSMs spawn their corpse themselves, outside EnemyDeathEffects.corpse
        // (notably Mage Lord Phase2). Keep these animation owners even after the HM is disabled.
        static void FindRelatedCorpses(Pending p)
        {
            BossCorpseTracking.Related(p.Record, p.Scene, p.Related);
            if (Time.time < p.NextCorpseScan) return;
            p.NextCorpseScan = Time.time + 0.1f;
            string record = Compact(p.Record.Split('@')[0]);
            foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = fsm.gameObject;
                if (go.scene.name != p.Scene) continue;
                // Native controllers that outlive the health-bearing body.
                bool controller = (record == "hollowknight" && go.name == "Boss Corpse")
                    || (record == "nightmaregrimm" && go.name == "Grimm Control");
                bool owns = p.Body != null && go.transform.IsChildOf(p.Body.transform);
                if (!controller && !owns && !p.Related.Contains(go)) continue;
                if (controller && !p.Related.Contains(go)) p.Related.Add(go);
                // Native FSMs also create/activate corpses themselves (Oro, Mato,
                // Sly, Grimm). Follow their actual object variables, not record aliases.
                foreach (var variable in fsm.FsmVariables.GameObjectVariables)
                {
                    string name = Compact(variable.Name);
                    var value = variable.Value;
                    if ((!name.Contains("corpse") && !name.Contains("deathhead")) || value == null || !value.scene.isLoaded) continue;
                    if (!p.Related.Contains(value)) p.Related.Add(value);
                }
            }
            foreach (var anim in Object.FindObjectsByType<tk2dSpriteAnimator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var go = anim.gameObject;
                if (go.scene.name != p.Scene || p.Related.Contains(go)) continue;
                string name = Compact(go.name);
                if (name.Contains("corpse") && name.Contains(record)) p.Related.Add(go);
            }
        }

        static string Compact(string text)
        {
            var result = new System.Text.StringBuilder();
            foreach (char c in text) if (char.IsLetterOrDigit(c)) result.Append(char.ToLowerInvariant(c));
            return result.ToString();
        }

        static bool DeathFlow(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return false;
            foreach (var fsm in go.GetComponentsInChildren<PlayMakerFSM>())
            {
                if (!fsm.enabled) continue;
                var active = fsm.Fsm.ActiveState;
                if (active != null)
                    foreach (var action in active.Actions)
                    {
                        if (action == null || action.Finished) continue;
                        string type = action.GetType().Name;
                        // Invisible pauses also own time in a death sequence. They need
                        // not have a death-like state name (the ghosts start in Pause).
                        if (type == "Wait" || type == "RandomWait" || type == "WaitRandom"
                            || type == "NextFrameEvent" || type == "Tk2dPlayAnimationWithEvents"
                            || type.StartsWith("iTween")) return true;
                    }
                string state = Compact(fsm.ActiveStateName ?? "");
                if (state == "dead" || state == "deathend" || state == "end" || state == "idle" || state == "rest") continue;
                // A looping shake or an invisible pause in a death FSM is still part of dying.
                if (state.Contains("death") || state.Contains("dying") || state.Contains("shake") || state.Contains("explode")
                    || state.Contains("burst") || state.Contains("blow") || state.Contains("splat"))
                {
                    // A terminal state may be named Death forever (Nosk's corpse).
                    // Only a state with a next step blocks settling; animation is checked separately.
                    if (active != null && active.Transitions.Length > 0) return true;
                }
            }
            return false;
        }

        /// <summary>Automatic exits also wait, then deliver directly when the death finishes or the room unloads.</summary>
        public static bool Waits(string record, string scene) => Enabled && EnemyDrops.IsBoss(record);

        /// <summary>A boss down: its loot kept until its body is gone or bursts (Bestiary.Credit calls this instead of rolling).</summary>
        public static void Defer(HealthManager hm, string record, Vector2? at, bool? firstKill)
        {
            var col = hm.GetComponent<Collider2D>();
            pending.Add(new Pending
            {
                Record = record, Scene = hm.gameObject.scene.name, Name = hm.gameObject.name, FirstKill = firstKill, Body = hm.gameObject,
                Effects = hm.GetComponent<EnemyDeathEffects>(), Corpse = BossCorpseTracking.Of(hm.GetComponent<EnemyDeathEffects>()), Since = Time.time, StableSince = Time.time,
                LifeReward = Net.BossLifeRewards.Begin(record, hm.gameObject.scene.name),
                At = at ?? (col != null ? (Vector2)col.bounds.center : (Vector2)hm.transform.position),
            });
            DevDeferred++;
        }

        static bool Animating(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return false;
            foreach (var anim in go.GetComponentsInChildren<tk2dSpriteAnimator>())
            {
                if (!anim.enabled || !anim.Playing || anim.CurrentClip == null) continue;
                // Idle loops may persist forever on corpses; one-shot death clips must finish.
                var clip = anim.CurrentClip;
                if (clip.wrapMode == tk2dSpriteAnimationClip.WrapMode.Once) return true;
            }
            foreach (var anim in go.GetComponentsInChildren<Animator>())
            {
                if (!anim.enabled) continue;
                for (int layer = 0; layer < anim.layerCount; layer++)
                {
                    var state = anim.GetCurrentAnimatorStateInfo(layer);
                    if (anim.IsInTransition(layer) || (!state.loop && state.normalizedTime < 1f)) return true;
                }
            }
            return false;
        }

        static string States(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return "";
            string key = "";
            foreach (var fsm in go.GetComponentsInChildren<PlayMakerFSM>())
                if (fsm.enabled) key += fsm.GetInstanceID() + ":" + fsm.ActiveStateName + ";";
            return key;
        }

        /// <summary>The body's drawn part (its renderers on, nothing as big as a room), and where it is.</summary>
        static bool Seen(GameObject go, ref Vector2 at)
        {
            if (go == null || !go.activeInHierarchy) return false;
            bool any = false;
            Bounds b = default;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var rb = r.bounds;
                if (rb.size.x > 30f || rb.size.y > 30f || rb.size.sqrMagnitude < 1e-4f) continue;
                if (!any) { b = rb; any = true; }
                else b.Encapsulate(rb);
            }
            if (any) at = b.center;
            return any;
        }

        /// <summary>Each frame: a boss's loot let fall once its body is gone, has burst or has waited long enough.</summary>
        public static void Tick()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                bool loaded = UnityEngine.SceneManagement.SceneManager.GetSceneByName(p.Scene).isLoaded;
                if (!loaded)
                {
                    pending.RemoveAt(i);
                    DevByLeft++;
                    Fire(p, true, "离开了房间");
                    continue;
                }
                var corpse = Corpse(p);
                FindRelatedCorpses(p);
                bool seen = Seen(p.Body, ref p.At) | Seen(corpse, ref p.At);
                bool present = Active(p.Body) | Active(corpse);
                bool animating = Animating(p.Body) | Animating(corpse);
                bool deathFlow = DeathFlow(p.Body) | DeathFlow(corpse);
                string stateKey = States(p.Body) + "|" + States(corpse);
                foreach (var related in p.Related)
                {
                    seen |= Seen(related, ref p.At);
                    present |= Active(related);
                    animating |= Animating(related);
                    deathFlow |= DeathFlow(related);
                    stateKey += "|" + States(related);
                }
                if (animating || stateKey != p.StateKey)
                {
                    p.StableSince = Time.time;
                    p.StateKey = stateKey;
                }
                // Hidden renderers are common between death states. Only an inactive/destroyed
                // animation owner has gone; invisibility alone cannot finish the sequence.
                p.GoneFrames = present ? 0 : p.GoneFrames + 1;
                string why = null;
                if (p.Burst && !animating && !deathFlow) { why = "爆开动画结束"; DevByBurst++; }
                // Gone two frames running (not the frame its object hands over to its corpse).
                else if (p.GoneFrames >= 2 && !animating && !deathFlow) { why = "身体消失"; DevByGone++; }
                else if (!animating && !deathFlow && Time.time - p.StableSince >= MaxWait) { why = "死亡流程结束，遗体静止"; DevByTime++; }
                if (why == null) continue;
                pending.RemoveAt(i);
                Fire(p, EnemyDrops.LeavesOnItsOwn(p.Scene), why);
            }
        }

        static bool Active(GameObject go) => go != null && go.activeInHierarchy;

        static void Fire(Pending p, bool direct, string why)
        {
            DevLastWhy = $"{p.Record}：{why}，{Time.time - p.Since:F1} 秒后";
            Plugin.Log.LogInfo($"[首领掉落] {DevLastWhy}，在 {p.At.x:F1},{p.At.y:F1}{(direct ? "（直接进背包）" : "")}");
            Relics.ContextScene = p.Scene;
            Relics.ContextName = p.Name;
            try
            {
                EnemyDrops.Roll(p.Record, p.At, direct, p.FirstKill, scene: p.Scene);
                Net.BossLifeRewards.Complete(p.LifeReward, p.Record, p.Scene, p.At, direct);
            }
            finally { Relics.ContextScene = Relics.ContextName = null; }
        }

        // Grimm Control keeps running through the wake-up transition. Its native sequence is
        // Ash Away -> Fade -> Set PD -> Divine Leaves? -> Return. The death visuals and kill
        // records are complete at Divine Leaves?, while the dream is still loaded.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.FsmState), nameof(HutongGames.PlayMaker.FsmState.OnEnter))]
        static void NightmareSettled(HutongGames.PlayMaker.FsmState __instance)
        {
            if (pending.Count == 0 || __instance.Name != "Divine Leaves?") return;
            var go = __instance.Fsm?.GameObject;
            if (go == null || go.name != "Grimm Control" || go.scene.name != "Grimm_Nightmare") return;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (p.Scene != go.scene.name || p.Record.Split('@')[0] != "NightmareGrimm") continue;
                pending.RemoveAt(i);
                Fire(p, true, "格林王死亡演出结束，梦境内发放");
            }
        }

        /// <summary>Finish the bursting state, then wait for its one-shot sprite animation before paying.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.FsmState), nameof(HutongGames.PlayMaker.FsmState.OnExit))]
        static void Entered(HutongGames.PlayMaker.FsmState __instance)
        {
            if (pending.Count == 0) return;
            var go = __instance.Fsm?.GameObject;
            if (go == null || !Bursting(__instance.Name)) return;
            foreach (var p in pending)
            {
                if (p.Burst) continue;
                var corpse = Corpse(p);
                if (p.Body != null && go.transform.IsChildOf(p.Body.transform) || corpse != null && go.transform.IsChildOf(corpse.transform)) p.Burst = true;
                foreach (var related in p.Related)
                    if (related != null && go.transform.IsChildOf(related.transform)) p.Burst = true;
            }
        }

        static bool Bursting(string state)
        {
            if (string.IsNullOrEmpty(state)) return false;
            string s = state.ToLowerInvariant();
            return s.Contains("burst") || s.Contains("explo") || s.Contains("blow") || s.Contains("splat");
        }

        /// <summary>A save left: what was waiting goes with it.</summary>
        public static void Clear() { pending.Clear(); BossCorpseTracking.Clear(); }

        public static int DevPending => pending.Count;
    }
}
