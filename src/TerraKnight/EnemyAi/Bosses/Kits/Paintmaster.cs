using System.Collections;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// Paintmaster Sheo (Godhome; "nailmaster_sheo"; 空洞骑士首领新版AI设计.md §2.8):
    /// <list type="bullet">
    /// <item>with the jump slash's splash, the stab, the sweep and the great slash's rain, one more glob of his own paint
    /// lobbed to land where the player is, or thrown up through him when he is up off the floor (a platform, the
    /// ceiling);</item>
    /// <item>the red jump aims at where the player is going and goes up to the player up off the floor (as fast a leap as
    /// that takes, his slashes at the top of it); its strike down marked; against the air, and half the time against a
    /// player up off the floor, the jump;</item>
    /// <item>a quicker pace between his moves (×0.85).</item>
    /// </list>
    /// </summary>
    [Kit("Paintmaster")]
    internal sealed class PaintmasterKit : BossKit
    {
        protected override string ControlName => "nailmaster_sheo";
        protected override float Pace => 0.85f;
        protected override Look Theme => Look.Fire;

        float ownTop = 16.1f, ownJumpY = 60f;

        protected override void Setup()
        {
            var t = Float("Topslash Y");
            if (t != null) ownTop = t.Value;
            var jy = Float("Jump Y Dstab");
            if (jy != null) ownJumpY = jy.Value;
            Tempo("Idle", "Stab Recover", "Dstab Recover", "GSlash Recover");
            At("Aim Jump", 3, () =>
            {
                var hero = Float("Hero X");
                if (hero != null) hero.Value = MaybeLead(0.8f).x;
                Batch5.RaiseTop(this, Float("Topslash Y"), ownTop);
            });
            OnEnter("Idle", () => { var top = Float("Topslash Y"); if (top != null) top.Value = ownTop; });
            OnEnter("Jump", () => Batch5.HighJump(this, rb, ownTop, ownJumpY));
            OnEnter("Jump", () => Run(Batch5.TopOut(this, rb, ownTop)), first: false);
            // Down from a jump that never got to its slash (his Next Move unset): on to his next attack, not stood there.
            OnEnter("After Evade", NextMoveSet);
            OnEnter("Dstab Antic", () => MarkGround(Me, 4f, Warn(0.4f)), first: false);
            // His own paint (the jump slash's splash, the first to throw any), one more glob each time.
            foreach (var s in new[] { "RedShot L", "RedShot R", "Stab2", "S 3", "Drops" })
                if (Batch2.Has(Control, s)) OnEnter(s, PaintAt, first: false);
            Branch("Choice", "Jump Antic", () => WantAntiAir() || !Grace && TargetHeight > 3f && Random.value < 0.5f);
        }

        void NextMoveSet()
        {
            var next = Control != null ? Control.FsmVariables.FindFsmString("Next Move") : null;
            if (next == null) return;
            string v = next.Value;
            if (v != "STAB" && v != "SLASH" && v != "GREAT SLASH" && v != "ATTACK") next.Value = "ATTACK";
        }

        GameObject paint;
        GameObject Paint => paint != null ? paint : paint = OwnShots.Of(State("RedShot L")) ?? OwnShots.Of(State("RedShot R"));

        /// <summary>A glob of his own paint from him to land where the player is now, or up through him off the floor.</summary>
        void PaintAt()
        {
            if (Grace || !RoomForStrike || Paint == null) return;
            Vector2 from = Me + Vector2.up, to = Lock();
            if (TargetHeight > 3f && to.y - from.y > 1f)
            {
                // Up to him at the top of its arc.
                var prb = Paint.GetComponent<Rigidbody2D>();
                float g = Mathf.Abs(Physics2D.gravity.y) * (prb != null ? prb.gravityScale : 1f);
                if (g < 0.5f) { OwnShots.Throw(Paint, from, (to - from).normalized * 18f); return; }
                float vy = Mathf.Sqrt(2f * g * (to.y - from.y));
                OwnShots.Throw(Paint, from, new Vector2(Mathf.Clamp((to.x - from.x) * g / vy, -30f, 30f), vy));
                return;
            }
            OwnShots.Lob(Paint, from, OnGround(to), 0.9f);
        }
    }
}
