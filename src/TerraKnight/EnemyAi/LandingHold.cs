using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi
{
    /// <summary>
    /// An enemy's own leap landing (its script's state after a "bottom hit" in the air that stops it dead: the Leaping
    /// Husk's Cooldown, the Heavy Sentry's and the Mantis Traitors' Land, the Mosskin Charger's, the Hoppers'...) stops its
    /// sideways speed once, on the frame it enters; on the grid's slopes and with the bodies' frictionless material it then
    /// slid on through the landing. For the first <see cref="HoldFor"/> of such a state the body is held to no sideways
    /// speed, whatever AI the save has. Only those landings (a state with a "velocity x = 0" of its own: not the Baldurs'
    /// or the Mushroom Rollers' landings that roll or jump on, not the Gruzzers' bounces), only falling bodies (gravity on)
    /// with no Walker running, never the bosses (their kits own their fights), never under Terraria's knockback or the new
    /// AI's own hold (its moves land themselves), never a network puppet (the owner's game moves it).
    /// </summary>
    internal sealed class LandingHold : MonoBehaviour
    {
        const float HoldFor = 0.6f;
        public static int DevHeld;

        HealthManager hm;
        Rigidbody2D rb;
        Walker walker;
        PlayMakerFSM[] fsms;
        HashSet<string>[] lands;
        string[] was;
        float[] since;

        static readonly HashSet<string> none = new HashSet<string>();

        internal static void Attach(HealthManager hm)
        {
            if (hm == null || hm.GetComponent<LandingHold>() != null) return;
            var rb = hm.GetComponent<Rigidbody2D>();
            if (rb == null) return;
            var info = Items.EnemyStats.Of(hm);
            if (info == null || info.Boss || info.Dummy) return;
            var list = new List<PlayMakerFSM>();
            var sets = new List<HashSet<string>>();
            foreach (var f in hm.GetComponents<PlayMakerFSM>())
            {
                var set = LandingStates(f);
                if (set.Count == 0) continue;
                list.Add(f);
                sets.Add(set);
            }
            if (list.Count == 0) return;
            var h = hm.gameObject.AddComponent<LandingHold>();
            h.hm = hm;
            h.rb = rb;
            h.walker = hm.GetComponent<Walker>();
            h.fsms = list.ToArray();
            h.lands = sets.ToArray();
            h.was = new string[list.Count];
            h.since = new float[list.Count];
        }

        /// <summary>The states a script lands in: those its "bottom hit" in the air leads to that stop it dead.</summary>
        static HashSet<string> LandingStates(PlayMakerFSM f)
        {
            if (f == null || f.Fsm == null) return none;
            HashSet<string> set = null;
            foreach (var s in f.FsmStates)
            {
                FsmEvent bottom = null;
                foreach (var a in s.Actions)
                {
                    if (a is CheckCollisionSide c && c.bottomHitEvent != null) bottom = c.bottomHitEvent;
                    else if (a is CheckCollisionSideEnter e && e.bottomHitEvent != null) bottom = e.bottomHitEvent;
                }
                if (bottom == null) continue;
                foreach (var t in s.Transitions)
                    if (t.FsmEvent == bottom && t.ToFsmState != null && Stops(t.ToFsmState)) (set ?? (set = new HashSet<string>())).Add(t.ToState);
            }
            return set ?? none;
        }

        /// <summary>Its own "velocity x = 0" (a set number, not a variable, on itself).</summary>
        static bool Stops(FsmState s)
        {
            foreach (var a in s.Actions)
                if (a is SetVelocity2d v && v.Enabled && v.gameObject != null && v.gameObject.OwnerOption == OwnerDefaultOption.UseOwner
                    && v.x != null && !v.x.UseVariable && v.x.Value == 0f)
                    return true;
            return false;
        }

        void FixedUpdate()
        {
            if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic || rb.gravityScale <= 0f || walker != null && walker.enabled) return;
            float now = Time.time;
            for (int i = 0; i < fsms.Length; i++)
            {
                var f = fsms[i];
                if (f == null || !f.enabled) continue;
                string state = f.ActiveStateName;
                if (!ReferenceEquals(state, was[i]))
                {
                    was[i] = state;
                    since[i] = now;
                }
                if (state == null || !lands[i].Contains(state) || now - since[i] > HoldFor) continue;
                var v = rb.linearVelocity;
                if (Mathf.Abs(v.x) < 0.01f || EnemyAi.Paused.Contains(f.Fsm) || Net.Enemies.IsPuppet(hm)) continue;
                var flight = GetComponent<Items.KnockFlight>();
                if (flight != null && flight.Active) continue;
                var slide = GetComponent<Items.KnockSlide>();
                if (slide != null && slide.Active) continue;
                rb.linearVelocity = new Vector2(0f, v.y);
                DevHeld++;
            }
        }
    }
}
