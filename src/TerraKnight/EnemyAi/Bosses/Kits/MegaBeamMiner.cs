using System.Collections;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Crystal Guardian and the Enraged Guardian (Beam Miner; 空洞骑士首领新版AI设计.md §2.3):
    /// <list type="bullet">
    /// <item>his laser aims at the player wherever he is in the room (also out of its usual range, up under the ceiling);</item>
    /// <item>its wind-up a fifth shorter, and the thin beam follows the player through it, held only for its last
    /// moment; his waits a fifth shorter;</item>
    /// <item>from his second rank, half his lasers are fired again straight after (his own laser, aimed at the player
    /// anew);</item>
    /// <item>a player kept up in the air gets the laser.</item>
    /// </list>
    /// </summary>
    [Kit("MegaBeamMiner")]
    internal sealed class MegaBeamMinerKit : BossKit
    {
        protected override string ControlName => "Beam Miner";
        protected override float Pace => 0.8f;
        protected override Look Theme => Look.Crystal;

        bool enraged, aimHero;
        string Sfx => enraged ? " 2" : "";

        protected override void Setup()
        {
            enraged = Control != null && Control.Fsm.GetState("Facing R? 2") != null;
            Tempo("Idle", "First Idle");
            // Out of his laser's range (the game would fire it flat to the side), at the player all the same.
            OnEnter("Aim" + Sfx, () => aimHero = true);
            OnEnter("Aim Left" + Sfx, () => aimHero = HasTarget);
            OnEnter("Aim Right" + Sfx, () => aimHero = HasTarget);
            At("Facing R?" + Sfx, 0, ReAim);
            var antic = State("Beam Antic" + Sfx);
            var wait = antic != null ? Batch1Helpers.First<HutongGames.PlayMaker.Actions.Wait>(antic) : null;
            if (wait != null)
            {
                float span = wait.time.Value;
                OnEnter("Beam Antic" + Sfx, () => { if (aimHero) Run(Follow(span)); }, first: false);
            }
            Branch("Choice", "Range Check" + Sfx, WantAntiAir);
            // His laser's cleanup done (its CD): fired again at once, aimed anew, now and then.
        }


        Vector2 Origin
        {
            get
            {
                var o = Go("Beam Origin")?.Value;
                return o != null ? (Vector2)o.transform.position : Me + Vector2.up * 0.5f;
            }
        }

        /// <summary>The thin beam kept on the player through its wind-up, held where he will be for its last moment.</summary>
        IEnumerator Follow(float span)
        {
            string state = StateNow;
            float until = Time.time + Mathf.Max(0f, span - Antic(0.35f));
            var beam = Go("Beam")?.Value;
            var ball = Go("Beam Ball")?.Value;
            var impact = Go("Beam Impact")?.Value;
            while (Time.time < until && Active && StateNow == state)
            {
                Vector2 o = Origin, d = Lead(until - Time.time + 0.1f) - o;
                if (d.sqrMagnitude > 0.01f)
                {
                    Aim(o, d);
                    float a = Float("Angle")?.Value ?? 0f, dist = Float("Ray Distance")?.Value ?? 0f;
                    if (beam != null)
                    {
                        beam.transform.rotation = Quaternion.Euler(0f, 0f, a);
                        var sc = beam.transform.localScale;
                        beam.transform.localScale = new Vector3(dist / 1.75f, sc.y, sc.z);
                    }
                    if (ball != null) ball.transform.rotation = Quaternion.Euler(0f, 0f, a);
                    if (impact != null)
                    {
                        var hit = o + d.normalized * dist;
                        impact.transform.position = new Vector3(hit.x, hit.y, -0.004f);
                        impact.transform.rotation = Quaternion.Euler(0f, 0f, a);
                    }
                }
                yield return null;
            }
        }

        /// <summary>The laser aimed at the player now, as its warning begins (the game had aimed before the wind-up).</summary>
        void ReAim()
        {
            if (!aimHero) return;
            Vector2 o = Origin, d = Lock() - o;
            if (d.sqrMagnitude < 0.01f) return;
            Aim(o, d);
        }

        /// <summary>The game's laser variables set for a line from o along d (its angle, length, end and vector).</summary>
        void Aim(Vector2 o, Vector2 d)
        {
            var angle = Float("Angle");
            if (angle != null) angle.Value = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var hit = Hostiles.NativeRay(o, d.normalized, 100f);
            float dist = hit.collider != null ? hit.distance : 100f;
            var point = Vec2("Ray Hit Point");
            if (point != null) point.Value = o + d.normalized * dist;
            var len = Float("Ray Distance");
            if (len != null) len.Value = dist;
            var ray = Vec2("Ray Vector");
            if (ray != null) ray.Value = d;
        }
    }
}
