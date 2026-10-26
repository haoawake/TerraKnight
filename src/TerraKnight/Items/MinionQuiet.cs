using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// A summoner's minions and sentries take health and count for nothing more (the user's): they don't break off a boss,
    /// and they draw no enemy's aggression (a Moss Knight turned on the minion at its back, not on the player).
    /// HealthManager.TakeDamage tells the struck body's machines HIT and TOOK DAMAGE (and HIT to the body it forwards its
    /// hits to), on which some flinch, turn, block, dodge, counter or teleport, and a boss's Stun Control STUN DAMAGE,
    /// towards a stagger: while a minion's blow is dealt (MeleeHits.Deal), on any enemy, the first two are dropped here, and
    /// a boss's stun machine is held off there. A blow on a raised shield (HealthManager.Invincible) tells it BLOCKED HIT
    /// and the side's, on which the Moss Knight blocks and slashes back at the minion: dropped too. The player's own blows
    /// (whips too) are untouched.
    /// </summary>
    [HarmonyPatch]
    internal static class MinionQuiet
    {
        /// <summary>The body a minion's blow is being dealt to now, and the one it forwards its hits to (null between blows).</summary>
        public static GameObject Body, Forward;
        public static int DevQuieted, DevDropped;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FSMUtility), nameof(FSMUtility.SendEventToGameObject), typeof(GameObject), typeof(string), typeof(bool))]
        static bool Drop(GameObject go, string eventName)
        {
            if (Body == null || go == null || (go != Body && go != Forward)) return true;
            if (eventName != "HIT" && eventName != "TOOK DAMAGE" && eventName != "BLOCKED HIT" && eventName != "BLOCKED HIT R"
                && eventName != "BLOCKED HIT L" && eventName != "BLOCKED HIT U" && eventName != "BLOCKED DOWN") return true;
            DevDropped++;
            return false;
        }

        static readonly System.Collections.Generic.HashSet<string> told = new System.Collections.Generic.HashSet<string>();
        public static int DevPhaseTold;

        /// <summary>
        /// A boss's health checks go on as the player's blow makes them: its Phase Control (the Hollow Knight, the Radiance,
        /// the Collector), a Warrior Dream's Escalation or Rage Check, the Lost Kin's shake tokens, the Dung Defender's Rage?,
        /// the Hive Knight's Phase Check all wait for TOOK DAMAGE, pause, then compare GetHP with a threshold. After a minion's
        /// blow, a machine waiting so whose check the health now passes is told TOOK DAMAGE, once for each threshold; the
        /// rest hear nothing, so a hail of minion blows can't keep sending a fight's own machine back to its idle.
        /// </summary>
        public static void PhaseChecks(HealthManager hm)
        {
            if (hm == null || hm.isDead || Items.EnemyStats.Of(hm)?.Boss != true) return;
            foreach (var f in hm.GetComponents<PlayMakerFSM>())
            {
                if (!f.enabled || f.Fsm == null) continue;
                var at = f.Fsm.ActiveState;
                if (at == null) continue;
                var check = To(f, at, "TOOK DAMAGE", true);
                // Through its pauses to the state that reads the health.
                for (int hop = 0; hop < 4 && check != null && !ReadsHp(check); hop++) check = To(f, check, "FINISHED", false);
                if (check == null) continue;
                foreach (var a in check.Actions)
                {
                    if (!a.Enabled || !(a is HutongGames.PlayMaker.Actions.IntCompare c) || !IntoHp(check, c.integer1)) continue;
                    int line = c.integer2.Value;
                    if (!Passes(f, at, check, c, hm.hp)) continue;
                    if (told.Count > 4096) told.Clear();
                    if (!told.Add($"{f.GetInstanceID()}/{check.Name}/{line}")) continue;
                    f.SendEvent("TOOK DAMAGE");
                    DevPhaseTold++;
                    Plugin.Log.LogInfo($"[召唤物] {hm.name} 的 {f.FsmName} 在 {at.Name}：血 {hm.hp} 过了 {line}，照玩家的一击告诉它受了伤（{check.Name}）");
                    break;
                }
            }
        }

        /// <summary>Dev: the boss's machine now waiting on a health check that moves it on, and the health that passes it.</summary>
        public static (PlayMakerFSM fsm, int line) DevNextLine(HealthManager hm)
        {
            if (hm == null) return (null, 0);
            foreach (var f in hm.GetComponents<PlayMakerFSM>())
            {
                var at = f.Fsm?.ActiveState;
                if (at == null) continue;
                var check = To(f, at, "TOOK DAMAGE", true);
                for (int hop = 0; hop < 4 && check != null && !ReadsHp(check); hop++) check = To(f, check, "FINISHED", false);
                if (check == null) continue;
                foreach (var a in check.Actions)
                    if (a.Enabled && a is HutongGames.PlayMaker.Actions.IntCompare c && IntoHp(check, c.integer1) && Passes(f, at, check, c, c.integer2.Value - 1))
                        return (f, c.integer2.Value);
            }
            return (null, 0);
        }

        /// <summary>
        /// Whether health this low passes the check (a phase's line is passed going down to it): the comparison's event on
        /// the low side moves the machine on (the Phase Controls' NEXT on equal and less), or with only an event above the
        /// line (the Dung Defender's Rage?, which goes on to its rage when that one doesn't fire), the health is down to it.
        /// </summary>
        static bool Passes(PlayMakerFSM f, HutongGames.PlayMaker.FsmState at, HutongGames.PlayMaker.FsmState check, HutongGames.PlayMaker.Actions.IntCompare c, int hp)
        {
            int line = c.integer2.Value;
            bool Set(HutongGames.PlayMaker.FsmEvent e) => e != null && !string.IsNullOrEmpty(e.Name);
            if (Set(c.lessThan) || Set(c.equal))
            {
                var ev = hp < line ? c.lessThan : hp == line ? c.equal : null;
                if (!Set(ev)) return false;
                var next = To(f, check, ev.Name, false);
                return next != null && next != at;
            }
            return Set(c.greaterThan) && hp <= line;
        }

        /// <summary>Where the state goes on this event (its own transitions, else the machine's global ones when asked).</summary>
        static HutongGames.PlayMaker.FsmState To(PlayMakerFSM f, HutongGames.PlayMaker.FsmState st, string ev, bool global)
        {
            foreach (var t in st.Transitions)
                if (t.EventName == ev) return f.Fsm.GetState(t.ToState);
            if (global)
                foreach (var t in f.FsmGlobalTransitions)
                    if (t.EventName == ev) return f.Fsm.GetState(t.ToState);
            return null;
        }

        static bool ReadsHp(HutongGames.PlayMaker.FsmState st)
        {
            foreach (var a in st.Actions) if (a.Enabled && a is GetHP) return true;
            return false;
        }

        /// <summary>Whether the variable is where the state's GetHP puts the health.</summary>
        static bool IntoHp(HutongGames.PlayMaker.FsmState st, HutongGames.PlayMaker.FsmInt v)
        {
            if (v == null || !v.UseVariable) return false;
            foreach (var a in st.Actions)
                if (a.Enabled && a is GetHP g && g.storeValue != null && g.storeValue.Name == v.Name) return true;
            return false;
        }
    }
}
