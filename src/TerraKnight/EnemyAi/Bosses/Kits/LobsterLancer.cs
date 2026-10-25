using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The God Tamer: the beast (Lobster, Control) and the tamer (Lancer, Control) under one parent; the beast's kit works
    /// on both (the fight ends with the beast), the tamer's only when the beast has none (空洞骑士首领新版AI设计.md §2.5):
    /// <list type="bullet">
    /// <item>the beast's spit a fourth spray, lobbed on where the player was (marked while it winds up), its globs leaving
    /// infection on the ground and on the player's blocks for a while;</item>
    /// <item>its roll's launch off the wall at where the player is going (marked), as high as a player kept up in the air,
    /// and off a ceiling it meets back down at him;</item>
    /// <item>the tamer's leap at where the player stood (marked), as high as a player kept up in the air.</item>
    /// </list>
    /// </summary>
    [Kit("LobsterLancer")]
    internal sealed class LobsterLancerKit : BossKit
    {
        protected override Look Theme => Look.Infection;

        /// <summary>The pairs already worked on (by their beast's and tamer's scripts).</summary>
        static readonly HashSet<int> done = new HashSet<int>();

        PlayMakerFSM beast, tamer;
        /// <summary>The beast's own globs (its Fire Globs').</summary>
        GameObject globShot;
        Rigidbody2D beastRb, tamerRb;
        Collider2D beastBody;
        Vector2 spitAt;
        FsmFloat launchY, leapY;

        protected override void Setup()
        {
            var parent = transform.parent;
            if (parent != null)
                foreach (var f in parent.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (f.FsmName != "Control") continue;
                    if (beast == null && f.Fsm.GetState("Fire Globs") != null) beast = f;
                    else if (tamer == null && f.Fsm.GetState("Slash 1") != null) tamer = f;
                }
            if (beast == null && Control != null && Control.Fsm.GetState("Fire Globs") != null) beast = Control;
            if (tamer == null && Control != null && Control.Fsm.GetState("Slash 1") != null) tamer = Control;
            if (beast != null) globShot = OwnShots.Of(State("Fire Globs", beast));
            bool onBeast = beast != null && beast.gameObject == gameObject;
            if (onBeast) Claim();
            else Run(ClaimIfAlone());
        }

        /// <summary>On the tamer: the beast's own kit does it all, if it has one by the next frame.</summary>
        IEnumerator ClaimIfAlone()
        {
            yield return null;
            if (beast != null && beast.GetComponent<BossKit>() != null) { Dormant = true; yield break; }
            Claim();
        }

        void Claim()
        {
            if (beast != null && done.Add(beast.GetInstanceID())) SetUpBeast();
            if (tamer != null && done.Add(tamer.GetInstanceID())) SetUpTamer();
        }

        // ------------------------------------------------------------------ the beast

        void SetUpBeast()
        {
            beastRb = beast.GetComponent<Rigidbody2D>();
            beastBody = beast.GetComponent<Collider2D>();
            OnEnter("Spit Antic", () =>
            {
                spitAt = Lock();
                MarkGround(spitAt, 6f, Warn(0.75f));
            }, true, beast);
            OnEnter("Fire Globs", SpitMore, false, beast);
            var launch = State("RC Launch", beast);
            launchY = Batch4.FloatField(Batch4.Find(launch, "SetVelocity2d"), "y");
            int vi = Batch4.IndexOf(launch, "SetVelocity2d");
            if (vi >= 0) At("RC Launch", vi, AimLaunch, beast);
            OnEnter("RC Air", () => Run(Ceiling()), true, beast);
        }

        Vector2 BeastAt => beastBody != null && beastBody.enabled ? (Vector2)beastBody.bounds.center : (Vector2)beast.transform.position;

        /// <summary>A fourth spray of four globs, lobbed round the marked point; they leave infection where they land.</summary>
        void SpitMore()
        {
            var sp = Go("Spit Point", beast)?.Value;
            Vector2 from = sp != null ? (Vector2)sp.transform.position : BeastAt + Vector2.up;
            for (int i = 0; i < 4; i++)
            {
                var to = spitAt + new Vector2((i - 1.5f) * 1.2f, 0f);
                OwnShots.Lob(globShot, from, to, Random.Range(0.75f, 0.95f));
            }
        }

        /// <summary>The roll's launch off the wall: at where the player is going, as high as he is if he keeps to the air.</summary>
        void AimLaunch()
        {
            var jumpX = Float("Jump X", beast);
            if (jumpX == null) return;
            var at = Lead(0.8f);
            jumpX.Value = (at.x - beast.transform.position.x) * 1.25f;
            if (launchY != null)
            {
                float vy = 48f;
                float g = beastRb != null ? Mathf.Abs(Physics2D.gravity.y * beastRb.gravityScale) : 0f;
                float up = TargetCenter.y - BeastAt.y + 1f;
                if ((TargetHeight > 3f || AirCamping) && g > 0f && up > 0f) vy = Mathf.Clamp(Mathf.Sqrt(2f * g * up), 48f, 110f);
                launchY.Value = vy;
            }
            MarkGround(at, 5f, Warn(0.6f));
        }

        /// <summary>Off a ceiling the rolled-up beast meets on its way up: back down at where the player is.</summary>
        IEnumerator Ceiling()
        {
            if (beastRb == null) yield break;
            float until = Time.time + 3f;
            while (Time.time < until && beast != null && beast.ActiveStateName == "RC Air")
            {
                var v = beastRb.linearVelocity;
                float top = beastBody != null ? beastBody.bounds.max.y : BeastAt.y + 1f;
                if (v.y > 4f && Hostiles.NativeRay(new Vector2(BeastAt.x, top), Vector2.up, 0.4f).collider != null)
                {
                    float dx = Lock().x - BeastAt.x;
                    beastRb.linearVelocity = new Vector2(Mathf.Clamp(dx * 1.5f, -25f, 25f), -Mathf.Abs(v.y) * 0.8f);
                    yield break;
                }
                yield return Batch4.Fixed;
            }
        }

        // ------------------------------------------------------------------ the tamer

        void SetUpTamer()
        {
            tamerRb = tamer.GetComponent<Rigidbody2D>();
            OnEnter("Aim", () => MarkGround(Lock(), 4f, Warn(0.6f)), false, tamer);
            var launch = State("Launch", tamer);
            leapY = Batch4.FloatField(Batch4.Find(launch, "SetVelocity2d"), "y");
            int vi = Batch4.IndexOf(launch, "SetVelocity2d");
            if (vi >= 0) At("Launch", vi, Leap, tamer);
        }

        /// <summary>
        /// The tamer's leap at a player up off the floor (a platform, a wall he clings to, wings): up through him, his body
        /// (its Damage Box) over the player's middle at the top of the leap, and across to him by then. The game's Jump X is
        /// half the way across a second, made for its own low leap: a high leap's short rise took him straight up beside his
        /// beast, nowhere near a player clinging up a wall (the user's, 10-25: "跳得还是不够高，玩家挂墙比8格高").
        /// </summary>
        void Leap()
        {
            if (leapY == null) return;
            float vy = 30f;
            float g = tamerRb != null ? Mathf.Abs(Physics2D.gravity.y * tamerRb.gravityScale) : 0f;
            var tamerBody = tamer.GetComponent<Collider2D>();
            Vector2 from = tamerBody != null && tamerBody.enabled ? (Vector2)tamerBody.bounds.center : (Vector2)tamer.transform.position;
            float half = tamerBody != null ? tamerBody.bounds.extents.y : 1f;
            if ((TargetHeight > 3f || WantAntiAir()) && g > 0f)
            {
                // Under the ceiling over him: his top a little short of it.
                float roof = CeilingAbove(new Vector2(TargetCenter.x, from.y), 40f) - half - 0.3f;
                float top = Mathf.Min(TargetCenter.y + half * 0.6f, roof);
                vy = Mathf.Clamp(Mathf.Sqrt(2f * g * Mathf.Max(top - from.y, 4f)), 30f, 110f);
                var jumpX = Float("Jump X", tamer);
                if (jumpX != null) jumpX.Value = Mathf.Clamp((TargetCenter.x - from.x) / Mathf.Max(0.15f, vy / g), -30f, 30f);
            }
            leapY.Value = vy;
        }
    }
}
