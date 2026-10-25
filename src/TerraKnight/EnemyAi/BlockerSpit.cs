using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi
{
    /// <summary>
    /// The Elder Baldur (Blocker Control) with the new AI. Its own goop spit (its Fire: a ball thrown at a random sideways
    /// speed, as often as not wide of the player) is thrown at where the player is each time, on the flatter arc that lands
    /// on him under the ball's own gravity; one time in three two more follow, each aimed again. Its short wave (the
    /// profile's Wave) is that same goop, a little smaller, popping up out of the ground ball after ball toward him, each
    /// falling back with the game's own splat.
    /// </summary>
    internal static class BlockerSpit
    {
        const float SpitSpeed = 22f, TripleChance = 0.35f, TripleGap = 0.22f, WaveScale = 0.78f, WaveStep = 0.75f, WaveEvery = 0.07f;

        public static int DevAimed, DevTriples, DevWaveBalls;

        /// <summary>A step of the kit's own put into the Baldur's script.</summary>
        sealed class Step : FsmStateAction
        {
            public System.Action Do;

            public override void OnEnter()
            {
                try { Do?.Invoke(); }
                catch (System.Exception e) { Plugin.Log.LogWarning("[敌人AI] 巴德尔长者的喷吐出错：" + e.Message); }
                Finish();
            }
        }

        public static PlayMakerFSM Control(PlayMakerFSM[] fsms)
        {
            if (fsms == null) return null;
            foreach (var f in fsms) if (f != null && f.FsmName == "Blocker Control") return f;
            return null;
        }

        /// <summary>Its Fire ends with the aim (once a script: a Baldur woken again keeps the step it has).</summary>
        public static void Hook(Brain brain, PlayMakerFSM control)
        {
            var fire = control != null ? control.Fsm.GetState("Fire") : null;
            if (fire == null) return;
            foreach (var a in fire.Actions)
                if (a is Step old)
                {
                    old.Do = () => Spat(brain, control);
                    return;
                }
            var step = new Step { Do = () => Spat(brain, control) };
            var list = new List<FsmStateAction>(fire.Actions) { step };
            fire.Actions = list.ToArray();
            step.Init(fire);
        }

        static bool Working(Brain brain) => brain != null && brain.isActiveAndEnabled && brain.HasTarget && World.EnemyAiMode.New;

        static void Spat(Brain brain, PlayMakerFSM control)
        {
            var ball = control.FsmVariables.FindFsmGameObject("Shot Instance")?.Value;
            var prefab = control.FsmVariables.FindFsmGameObject("Projectile")?.Value;
            if (ball == null) return;
            // Its own size again, whatever the AI (a pooled ball may have been one of the wave's smaller ones).
            if (prefab != null) ball.transform.localScale = prefab.transform.localScale;
            // Its rolling baldur, when it sends one, goes its own way.
            if (!Working(brain) || control.FsmVariables.FindFsmBool("Rollering")?.Value == true) return;
            Throw(ball, brain.TargetCenter);
            if (Random.value < TripleChance) brain.StartCoroutine(More(brain, control, prefab));
        }

        /// <summary>A ball thrown to land on a point: the flatter arc at the spit's speed, under the ball's own gravity.</summary>
        static void Throw(GameObject ball, Vector2 at)
        {
            var rb = ball.GetComponent<Rigidbody2D>();
            if (rb == null) return;
            float g = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0.01f, rb.gravityScale);
            rb.linearVelocity = Brain.Lob(ball.transform.position, at, SpitSpeed, g);
            DevAimed++;
        }

        static IEnumerator More(Brain brain, PlayMakerFSM control, GameObject prefab)
        {
            if (prefab == null) yield break;
            DevTriples++;
            var origin = control.FsmVariables.FindFsmVector3("Shot Origin");
            var effect = control.FsmVariables.FindFsmGameObject("Spit Effect")?.Value;
            for (int k = 0; k < 2; k++)
            {
                yield return new WaitForSeconds(TripleGap);
                if (!Working(brain) || control == null) yield break;
                var at = control.transform.position + (origin != null ? origin.Value : Vector3.zero);
                var ball = ObjectPool.Spawn(prefab, at, Quaternion.identity);
                ball.transform.localScale = prefab.transform.localScale;
                Throw(ball, brain.TargetCenter);
                if (effect != null) FSMUtility.SendEventToGameObject(effect, "PLAY");
                brain.Spit();
            }
        }

        /// <summary>Its goop (the Goop state's ball), for the wave.</summary>
        static GameObject Goop(PlayMakerFSM control)
        {
            var goop = control != null ? control.Fsm.GetState("Goop") : null;
            if (goop != null)
                foreach (var a in goop.Actions)
                    if (a is SetGameObject s && s.gameObject != null && s.gameObject.Value != null) return s.gameObject.Value;
            return null;
        }

        /// <summary>
        /// The short wave: from its side toward the player, a ball of its goop popping up out of the ground every
        /// <see cref="WaveStep"/> units, a little smaller than its spit, up to the reach or a wall or a drop.
        /// </summary>
        public static IEnumerator Wave(Brain brain, PlayMakerFSM control, Collider2D body, float dir, float reach)
        {
            var prefab = Goop(control);
            if (prefab == null || body == null) yield break;
            var b = body.bounds;
            float x = dir > 0f ? b.max.x + 0.35f : b.min.x - 0.35f, y = b.min.y + 0.6f;
            float z = control.transform.position.z;
            for (float travelled = 0f; travelled <= reach; travelled += WaveStep)
            {
                if (!Working(brain)) yield break;
                float ground = Ground(new Vector2(x, y + 1.2f), 3f);
                if (float.IsNaN(ground) || Blocked(new Vector2(x - dir * WaveStep, ground + 0.4f), dir, WaveStep)) yield break;
                var ball = ObjectPool.Spawn(prefab, new Vector3(x, ground + 0.6f, z), Quaternion.identity);
                ball.transform.localScale = prefab.transform.localScale * WaveScale;
                var col = ball.GetComponent<Collider2D>();
                if (col != null)
                {
                    // Just clear of the ground, so it splats only as it comes back down.
                    Physics2D.SyncTransforms();
                    ball.transform.position = new Vector3(x, ground + col.bounds.extents.y + 0.06f, z);
                }
                var rb = ball.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    float g = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0.01f, rb.gravityScale);
                    rb.linearVelocity = new Vector2(dir * 0.6f, Mathf.Sqrt(2f * g * Random.Range(0.9f, 1.3f)));
                }
                if (travelled == 0f) brain.Spit();
                DevWaveBalls++;
                x += dir * WaveStep;
                y = ground;
                yield return new WaitForSeconds(WaveEvery);
            }
        }

        static readonly RaycastHit2D[] hits = new RaycastHit2D[8];

        /// <summary>The nearest solid ground along a ray that isn't an enemy's own body (the Baldur's shell stands on the terrain layer).</summary>
        static RaycastHit2D Nearest(Vector2 from, Vector2 way, float length)
        {
            int n = Physics2D.RaycastNonAlloc(from, way, hits, length, Hostiles.TerrainMask);
            RaycastHit2D best = default;
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || c.isTrigger || c.GetComponentInParent<HealthManager>() != null) continue;
                if (hits[i].distance < bestD) { bestD = hits[i].distance; best = hits[i]; }
            }
            return best;
        }

        static float Ground(Vector2 from, float length)
        {
            var hit = Nearest(from, Vector2.down, length);
            return hit.collider != null ? hit.point.y : float.NaN;
        }

        static bool Blocked(Vector2 from, float dir, float length) => Nearest(from, new Vector2(Mathf.Sign(dir), 0f), length).collider != null;
    }
}
