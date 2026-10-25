using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.EnemyAi
{
    // A watchdog correction is movement too: never jump straight to the saved safe point.
    // Shared by world enemies and bosses; disabling recovery always restores the body.
    internal sealed class BodyRecovery : MonoBehaviour
    {
        Bosses.BossKit owner;
        HealthManager health;
        bool bossRecovery;
        Collider2D body;
        Rigidbody2D rb;
        Vector2 target;
        RigidbodyType2D bodyType;
        float gravity;
        readonly Dictionary<Behaviour, bool> scripts = new Dictionary<Behaviour, bool>();
        // Its PlayMaker scripts are held, not disabled: one disabled starts again from its first state once enabled (its
        // RestartOnEnable), and hears a DISABLE on the way (the False Knight, slid back mid-stun, went back to waiting for
        // his fight to start, his head never out). Holds are counted per script across every recovery (a boss's and the
        // world's unsticking can overlap on one body): the script's own setting comes back when the last lets go.
        readonly List<PlayMakerFSM> held = new List<PlayMakerFSM>();
        static readonly Dictionary<PlayMakerFSM, (int Count, bool Own)> holds = new Dictionary<PlayMakerFSM, (int, bool)>();

        static void Hold(PlayMakerFSM f)
        {
            if (holds.TryGetValue(f, out var h)) holds[f] = (h.Count + 1, h.Own);
            else holds[f] = (1, f.Fsm.ManualUpdate);
            f.Fsm.ManualUpdate = true;
        }

        static void Release(PlayMakerFSM f)
        {
            if (!holds.TryGetValue(f, out var h)) return;
            if (h.Count > 1) { holds[f] = (h.Count - 1, h.Own); return; }
            holds.Remove(f);
            if (f != null && f.Fsm != null) f.Fsm.ManualUpdate = h.Own;
        }
        internal bool Running { get; private set; }
        internal const float Speed = 8f;

        internal void Begin(Bosses.BossKit kit, Collider2D collider, Rigidbody2D rigidbody, PlayMakerFSM control, Vector2 to)
            => Begin(kit.Hm, kit, collider, rigidbody, control, to);

        internal void Begin(HealthManager hm, Bosses.BossKit kit, Collider2D collider, Rigidbody2D rigidbody, PlayMakerFSM control, Vector2 to)
        {
            if (Running) return;
            enabled = true;
            owner = kit; health = hm; body = collider; rb = rigidbody; target = to;
            bossRecovery = kit != null;
            foreach (var f in GetComponentsInChildren<PlayMakerFSM>(true)) if (f.Fsm != null) held.Add(f);
            if (control != null && control.Fsm != null && !held.Contains(control)) held.Add(control);
            foreach (var f in held) Hold(f);
            foreach (var b in GetComponents<Behaviour>())
                if (b is Brain || b is Walker || b is Climber || b is Crawler) scripts[b] = b.enabled;
            foreach (var f in scripts.Keys) if (f != null) f.enabled = false;
            if (rb != null)
            {
                bodyType = rb.bodyType; gravity = rb.gravityScale;
                rb.linearVelocity = Vector2.zero; rb.gravityScale = 0f; rb.bodyType = RigidbodyType2D.Kinematic;
            }
            Running = true;
        }

        void Update() => Step(Time.deltaTime);

        internal void Step(float dt)
        {
            if (!Running) return;
            if (health == null || health.isDead || Net.Enemies.IsPuppet(health) || bossRecovery && (owner == null || !owner.Active) || body == null || !body.enabled) { Restore(); return; }
            Vector2 center = body.bounds.center;
            Vector2 next = Vector2.MoveTowards(center, target, Speed * Mathf.Clamp(dt, 0f, 0.05f));
            transform.position += (Vector3)(next - center);
            Physics2D.SyncTransforms();
            if ((target - next).sqrMagnitude <= 0.0001f) Restore();
        }

        internal void Restore()
        {
            if (!Running) return;
            Running = false;
            if (rb != null) { rb.bodyType = bodyType; rb.gravityScale = gravity; rb.linearVelocity = Vector2.zero; }
            foreach (var entry in scripts) if (entry.Key != null) entry.Key.enabled = entry.Value;
            scripts.Clear();
            foreach (var f in held) Release(f);
            held.Clear();
        }

        void OnDisable() => Restore();
        void OnDestroy() => Restore();
    }
}
