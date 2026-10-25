using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// Oro and Mato (Godhome; "nailmaster", one kit each; 空洞骑士首领新版AI设计.md §2.8):
    /// <list type="bullet">
    /// <item>Oro's dash slash (his nail art): now and then it ends where the player is going rather than at its fixed
    /// place, its way drawn while he charges; his short dashes now and then turned to where the player is going;</item>
    /// <item>their jumps (to strike down) aim at where the player is going, and go as high as the player kept in the air;
    /// the strike down marked; Mato's cyclone rises to the player's height the same way; against the air, the jump;</item>
    /// <item>both up: as one sets off on his dash slash, the other (if idle) jumps at where the player is, half the time.</item>
    /// </list>
    /// </summary>
    [Kit("NailBros")]
    internal sealed class NailBrosKit : BossKit
    {
        protected override string ControlName => "nailmaster";
        protected override Look Theme => Look.Blade;
        /// <summary>Mato comes in falling from over the arena.</summary>
        protected override bool Roams => true;

        bool IsOro => Bool("Oro")?.Value ?? gameObject.name.StartsWith("Oro");
        float ownTop = 16.1f, ownJumpY = 60f;
        /// <summary>The next jump goes at where the player is (his brother's call), not where he is going.</summary>
        bool lockNext;
        /// <summary>The arena taken from the fight's camera lock once the entrance is over, and again now and then.</summary>
        bool arenaTaken;
        float arenaAgain;

        protected override void Setup()
        {
            var t = Float("Topslash Y");
            if (t != null) ownTop = t.Value;
            var jy = Float("Jump Y Dstab");
            if (jy != null) ownJumpY = jy.Value;
            Tempo("Idle");
            // His jump up to a raised top (a player up off the floor) as fast as it takes; his down stab where it tops out.
            OnEnter("Jump", () => Batch5.HighJump(this, rb, ownTop, ownJumpY));
            OnEnter("Jump", () => Run(Batch5.TopOut(this, rb, ownTop)), first: false);
            // Aim Jump: its own position, the hero's, then the jump's speed from them; the hero's x replaced in between.
            At("Aim Jump", 3, AimJump);
            OnEnter("Idle", () =>
            {
                var top = Float("Topslash Y");
                if (top != null) top.Value = ownTop;
                // On his feet between moves (the entrance's camera locks, Oro's intro and Mato's roar, switched off by then):
                // the arena from the fight's lock, wall to wall, now and then. Taken during the entrance it was the intro's
                // view, 3.6 units short of the left wall (a player by it was aimed at from there), or, for Mato, his waiting
                // place up in the rock left of the room; taken up in a jump over the lock's view, a view round him.
                if (Time.time >= arenaAgain)
                {
                    arenaAgain = Time.time + 4f;
                    Rearena();
                    if (!arenaTaken) Plugin.Log.LogInfo($"[首领AI] {name} 入场结束，场地按战斗镜头取 x {Arena.xMin:F1}~{Arena.xMax:F1} y {Arena.yMin:F1}~{Arena.yMax:F1}（在 {Me:F1}）");
                    arenaTaken = true;
                }
            });
            OnEnter("Cyclone Up", () => Batch5.RaiseTop(this, Float("Topslash Y"), ownTop));
            OnEnter("Dstab Antic", () => MarkGround(Me, 4f, Warn(0.6f)), first: false);
            OnEnter("Dash Antic", LeadDash, first: false);
            OnEnter("NA Charged", PlanDashSlash, first: false);
            OnEnter("Dash Forward", CallBrother);
            Branch("Choice", "Jump Antic", WantAntiAir);
            Branch("Oro Choice", "Jump Antic", WantAntiAir);
        }

        void AimJump()
        {
            var hero = Float("Hero X");
            if (hero != null) hero.Value = lockNext ? Lock().x : MaybeLead(0.9f).x;
            lockNext = false;
            Batch5.RaiseTop(this, Float("Topslash Y"), ownTop);
        }

        /// <summary>A short dash turned at where the player is going, now and then.</summary>
        void LeadDash()
        {
            if (RollLead()) Batch5.Face(transform, Lead(0.3f).x);
        }

        /// <summary>Oro's dash slash: now and then ending just past where the player is going; its way drawn.</summary>
        void PlanDashSlash()
        {
            if (!IsOro) return;
            var end = Float("End X");
            if (end == null) return;
            float dir = Mathf.Sign(end.Value - Me.x);
            if (RollLead())
            {
                float x = Lead(0.5f).x;
                if (Mathf.Sign(x - Me.x) == dir)
                {
                    float far = end.Value, near = Me.x + dir * 3f;
                    end.Value = Mathf.Clamp(x + dir * 2.5f, Mathf.Min(near, far), Mathf.Max(near, far));
                }
            }
            MarkLine(Me, new Vector2(end.Value, Me.y), Warn(0.5f));
        }

        /// <summary>Both up: the idle brother jumps at where the player is, half the time.</summary>
        void CallBrother()
        {
            var bro = Go("Brother")?.Value;
            if (bro == null || Random.value >= 0.5f) return;
            PlayMakerFSM other = null;
            foreach (var f in bro.GetComponents<PlayMakerFSM>()) if (f.FsmName == "nailmaster") other = f;
            var hm = bro.GetComponent<HealthManager>();
            if (other == null || hm == null || hm.isDead || other.ActiveStateName != "Idle") return;
            var kit = bro.GetComponent<NailBrosKit>();
            if (kit != null) kit.lockNext = true;
            other.SendEvent("DO STOMP");
        }
    }
}
