using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// Great Nailsage Sly (Godhome; "Control"; 空洞骑士首领新版AI设计.md §2.8):
    /// <list type="bullet">
    /// <item>his dash slash (a nail art): now and then it ends just past where the player is going rather than at its fixed
    /// place, its way drawn while he charges;</item>
    /// <item>his jump aims at where the player is going and goes up to the player kept in the air, its strike down marked;</item>
    /// <item>his cyclone follows the player up off the ground (no fall while the player is in the air); against the air,
    /// the cyclone;</item>
    /// <item>the last stage's close spin: where the player was as it winds up is where he flies (drawn), not where the
    /// player has got to; each of his launches at the player drawn.</item>
    /// </list>
    /// </summary>
    [Kit("Nailsage")]
    internal sealed class NailsageKit : BossKit
    {
        protected override Look Theme => Look.Blade;
        protected override bool Roams => true;

        float ownTop = 15.7f, ownJumpY = 60f;
        Vector2 spinAt;
        float? cycloneGravity;

        protected override void Setup()
        {
            var t = Float("Topslash Y");
            if (t != null) ownTop = t.Value;
            var jy = Float("Jump Y Dstab");
            if (jy != null) ownJumpY = jy.Value;
            Tempo("Idle");
            // His jump up to a raised top (a player up off the floor) as fast as it takes; his strike where it tops out.
            if (Batch2.Has(Control, "Jump"))
            {
                OnEnter("Jump", () => Batch5.HighJump(this, rb, ownTop, ownJumpY));
                OnEnter("Jump", () => Run(Batch5.TopOut(this, rb, ownTop)), first: false);
            }
            // Aim Jump: its own position, the hero's, then the jump's speed from them; the hero's x replaced in between.
            At("Aim Jump", 3, () =>
            {
                var hero = Float("Hero X");
                if (hero != null) hero.Value = MaybeLead(0.8f).x;
                Batch5.RaiseTop(this, Float("Topslash Y"), ownTop);
            });
            OnEnter("Idle", () => { var top = Float("Topslash Y"); if (top != null) top.Value = ownTop; });
            OnEnter("Stomp Antic", () => MarkGround(Me, 4f, Warn(0.4f)), first: false);
            OnEnter("Dash Charged", PlanDash, first: false);
            OnEnter("Cycloning", HoldCyclone, first: false);
            OnExit("Cycloning", RestoreCyclone);
            Branch("NA Choice", "Cyc Jump Antic", WantAntiAir);
            Branch("Distance", "Cyc Jump Antic", () => AirForced && WantAntiAir());
            // At Hero?: its bounce counted (its second action), then whether he launches at the player.
            At("At Hero?", 2, () =>
            {
                var bounces = Int("Bounces");
                if (bounces != null && bounces.Value >= 3) MarkLine(Me, TargetCenter + Vector2.down * 0.5f, 0.35f);
            });
            OnEnter("Rage Slash Antic", () =>
            {
                spinAt = Lock();
                MarkLine(Me, spinAt, Antic(0.4f));
            }, first: false);
            OnEnter("Rage Slash", () =>
            {
                var d = spinAt - Me;
                if (rb != null && d.sqrMagnitude > 0.01f) rb.linearVelocity = d.normalized * 35f;
            }, first: false);
        }

        void HoldCyclone()
        {
            if (rb == null || TargetHeight <= 2f) return;
            if (!cycloneGravity.HasValue) cycloneGravity = rb.gravityScale;
            rb.gravityScale = 0f;
        }

        void RestoreCyclone()
        {
            if (rb != null && cycloneGravity.HasValue) rb.gravityScale = cycloneGravity.Value;
            cycloneGravity = null;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RestoreCyclone();
        }

        /// <summary>The dash slash's end: now and then just past where the player is going; its way drawn.</summary>
        void PlanDash()
        {
            var end = Float("End X");
            if (end == null) return;
            float dir = Mathf.Sign(end.Value - Me.x);
            if (RollLead())
            {
                float x = Lead(0.5f).x;
                float wall = WallTowards(Me, dir, 40f) - dir * 1.5f;
                if (Mathf.Sign(x - Me.x) == dir)
                {
                    float near = Me.x + dir * 3f;
                    end.Value = Mathf.Clamp(x + dir * 2.5f, Mathf.Min(near, wall), Mathf.Max(near, wall));
                }
            }
            MarkLine(Me, new Vector2(end.Value, Me.y), Warn(0.35f));
        }
    }
}
