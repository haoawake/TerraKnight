using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// What the Mantis Lords', the Soul Master's, the Defenders', the Flukemarm's and the Watcher Knights' kits share:
    /// a script worked on once (two health managers on one body, Godhome's copies), an action of a state found or
    /// switched off, a clip played, a throw's flight worked out, a thrown thing carried along a path.
    /// </summary>
    internal static class Batch2
    {
        static readonly HashSet<int> claimed = new HashSet<int>();

        /// <summary>A script taken by a kit, once (false: another kit has it already, or there is none).</summary>
        internal static bool Claim(PlayMakerFSM f) => f != null && claimed.Add(f.GetInstanceID());

        /// <summary>Whether a script has a state (without the kit's warning in the log: a variant without it).</summary>
        internal static bool Has(PlayMakerFSM f, string state) => f != null && f.Fsm.GetState(state) != null;

        /// <summary>A state's first action of a kind, or null.</summary>
        internal static T Find<T>(FsmState s) where T : FsmStateAction
        {
            if (s == null) return null;
            foreach (var a in s.Actions) if (a is T t) return t;
            return null;
        }

        /// <summary>A state's actions of a kind switched off (a chase after the player: a lock holds still).</summary>
        internal static int Disable<T>(FsmState s) where T : FsmStateAction
        {
            if (s == null) return 0;
            int n = 0;
            foreach (var a in s.Actions)
                if (a is T) { a.Enabled = false; n++; }
            return n;
        }

        /// <summary>A clip of a body's own, if it has it.</summary>
        internal static void Play(GameObject go, string clip)
        {
            var anim = go != null ? go.GetComponent<tk2dSpriteAnimator>() : null;
            if (anim != null && anim.GetClipByName(clip) != null) anim.Play(clip);
        }

        /// <summary>A body shown or hidden, its collider with it (what the game's SetMeshRenderer and SetCollider do).</summary>
        internal static void Show(GameObject go, bool on)
        {
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = on;
            var c = go.GetComponent<Collider2D>();
            if (c != null) c.enabled = on;
        }

        /// <summary>The time a throw up at vy (gravity g, both up positive) takes to come down to a height dy over its start.</summary>
        internal static float FlightTime(float vy, float dy, float g)
        {
            g = Mathf.Max(0.01f, g);
            float disc = Mathf.Max(0f, vy * vy - 2f * g * dy);
            return (vy + Mathf.Sqrt(disc)) / g;
        }

        /// <summary>The gravity on a body (units a second squared, positive), from its rigidbody's scale.</summary>
        internal static float GravityOf(Rigidbody2D rb) => rb != null ? Mathf.Abs(Physics2D.gravity.y * rb.gravityScale) : Mathf.Abs(Physics2D.gravity.y);

        /// <summary>A body's dash ends where physics allowed it to reach, without a final coordinate jump.</summary>
        internal static IEnumerator Travel(BossKit kit, Rigidbody2D rb, Vector2 end, float speed, float seconds)
        {
            if (rb == null) yield break;
            float blocked = 0f;
            for (float t = 0f; t < seconds; t += Time.fixedDeltaTime)
            {
                Vector2 from = kit.transform.position;
                var delta = end - from;
                if (delta.sqrMagnitude < 0.0025f) break;
                rb.linearVelocity = delta.normalized * Mathf.Min(speed, delta.magnitude / Mathf.Max(0.001f, Time.fixedDeltaTime));
                yield return new WaitForFixedUpdate();
                blocked = Vector2.Distance(from, kit.transform.position) < 0.005f ? blocked + Time.fixedDeltaTime : 0f;
                if (blocked >= 0.15f) break;
            }
        }

        /// <summary>A strike carried with a body (its own hitbox while it slashes through), for some seconds: unseen, its own animation shows it.</summary>
        internal static Hostiles.Zone Escort(BossKit kit, Transform body, Vector2 offset, float radiusTiles, float seconds, Look look, int masks = 1)
        {
            var m = kit.NewMove(masks, look);
            m.Radius = radiusTiles;
            var z = kit.Strike(Hostiles.Zone.Shape.Circle, (Vector2)body.position + offset, 0.01f, seconds, m);
            z.Follow = body;
            z.FollowOffset = offset;
            z.Unseen = true;
            return z;
        }
    }
}
