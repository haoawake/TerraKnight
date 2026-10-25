using System.Collections;
using HutongGames.PlayMaker;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// Grey Prince Zote (the dream's, Godhome's; "Control"; 空洞骑士首领新版AI设计.md §2.7):
    /// <list type="bullet">
    /// <item>the jump slash: from his stomp's hover he moves to where the player was as it began (marked on the ground)
    /// before he comes down, in place of the game's random shift;</item>
    /// <item>he jumps for it higher (up to the player on a platform or hanging under the ceiling, as high as his body
    /// fits under it), hovers there at the player's height and comes across through him before his stomp; against a player
    /// up off the floor he goes for it often (half his choices), and always against the air;</item>
    /// <item>his Zotelings quicker: the flying ones chase faster, the hopping ones wait less between hops.</item>
    /// </list>
    /// His land waves, his charge and the challenge's levels are the game's own.
    /// </summary>
    [Kit("GreyPrince")]
    internal sealed class GreyPrinceKit : BossKit
    {
        protected override Look Theme => Look.Rock;
        /// <summary>His charge goes out through the arena's wall and his fall through its floor, coming back the other way.</summary>
        protected override bool Roams => true;
        protected override bool Burrows => true;

        FsmFloat top;
        float ownTop = 17.5f, stompX;

        protected override void Setup()
        {
            top = Batch5.CompareTo(State("JS In Air"));
            if (top != null) ownTop = top.Value;
            OnEnter("JS Aim", RaiseTop);
            OnEnter("JS Jump", LaunchHigh, first: false);
            OnEnter("Stomp Antic", LockStomp);
            AddState("TK Stomp Aim", StompAim, "Stomp");
            Branch("Shift?", "TK Stomp Aim", () => true);
            foreach (var choice in new[] { "Move Choice", "Move Choice 1", "Move Choice 2", "Move Choice 3" })
                if (Control != null && Control.Fsm.GetState(choice) != null) Branch(choice, "JS Antic", WantHigh);
            Run(Zotelings());
        }

        /// <summary>The jump slash against a player up off the floor (a platform, the ceiling): half his choices, always against the air.</summary>
        bool WantHigh() => WantAntiAir() || !Grace && TargetHeight > 3f && Random.value < 0.5f;

        /// <summary>
        /// The height his jump slash turns into the stomp's hover at (the game's 17.5): his body at the player's height when
        /// the player is up off the floor, as high as it fits under the ceiling over him and over the player; else the game's.
        /// </summary>
        void RaiseTop()
        {
            if (top == null) return;
            if (TargetHeight < 3f) { top.Value = ownTop; return; }
            float y = transform.position.y;
            float above = body != null && body.enabled ? body.bounds.max.y - y : 2.2f;
            float centre = body != null && body.enabled ? body.bounds.center.y - y : 0f;
            float ceil = Mathf.Min(CeilingAbove(Me + Vector2.up * 0.5f, 40f), CeilingAbove(TargetCenter, 40f));
            float cap = ceil - Mathf.Max(0.5f, above) - 0.3f;
            top.Value = Mathf.Max(ownTop, Mathf.Min(TargetCenter.y - centre, cap));
        }

        /// <summary>His jump slash's leap (the game's 60 up) as fast as it takes to pass his raised top.</summary>
        void LaunchHigh()
        {
            if (top == null || rb == null || top.Value <= ownTop + 0.01f) return;
            float g = Batch2.GravityOf(rb);
            float h = top.Value + 1.2f - transform.position.y;
            if (h <= 0f) return;
            float vy = Mathf.Sqrt(2f * g * h);
            var v = rb.linearVelocity;
            if (vy <= v.y) return;
            // The same sideways travel by the time he is up there as the game's leap would make by its top.
            float x = Mathf.Clamp(Lock().x - transform.position.x, -20f, 20f);
            rb.linearVelocity = new Vector2(Mathf.Clamp(x * g / vy, -40f, 40f), vy);
        }

        /// <summary>Where the stomp will come down: where the player is now, between the arena's sides, marked.</summary>
        void LockStomp()
        {
            float left = Float("Left X")?.Value ?? Arena.xMin, right = Float("Right X")?.Value ?? Arena.xMax;
            stompX = Mathf.Clamp(Lock().x, Mathf.Min(left, right), Mathf.Max(left, right));
            MarkGround(new Vector2(stompX, Me.y), 6f, Warn(0.7f));
        }

        /// <summary>Over to the marked place in a fifth of a second (still in the air), then the stomp.</summary>
        IEnumerator StompAim()
        {
            float dx = stompX - Me.x;
            if (Mathf.Abs(dx) < 0.4f || rb == null) yield break;
            try
            {
                Batch5.Face(transform, stompX, facesRight: true);
                const float time = 0.2f;
                for (float t = 0f; t < time; t += Time.deltaTime)
                {
                    rb.linearVelocity = new Vector2(dx / time, 0f);
                    yield return null;
                }
                rb.linearVelocity = Vector2.zero;
            }
            finally { if (rb != null) rb.linearVelocity = Vector2.zero; }
        }

        /// <summary>The room's Zotelings (their own "Control"s), quicker once.</summary>
        IEnumerator Zotelings()
        {
            yield return null;
            var scene = gameObject.scene;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (f == null || f.gameObject.scene != scene || f.FsmName != "Control" || !f.gameObject.name.StartsWith("Zoteling")) continue;
                var chase = f.Fsm.GetState("Buzzer Chase");
                if (chase != null)
                    foreach (var a in chase.Actions)
                    {
                        Batch5.ScaleField(a, "speedMax", 1.3f);
                        Batch5.ScaleField(a, "acceleration", 1.3f);
                    }
                if (f.Fsm.GetState("Hopper Land") != null) TempoFixed(f, "Hopper Land", 0.65f);
                if (f.Fsm.GetState("Buzzer Start") != null) TempoFixed(f, "Buzzer Start", 0.6f);
            }
        }
    }
}
