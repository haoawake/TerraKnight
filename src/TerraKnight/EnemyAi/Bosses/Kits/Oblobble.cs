using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Oblobbles (each body its own: Fatty Fly Attack, fat fly bounce, Set Rage; 空洞骑士首领新版AI设计.md §2.5):
    /// <list type="bullet">
    /// <item>their bursts a quarter more often, their flight 1.3 times as fast;</item>
    /// <item>each round's first shot at where the player was as it winds up (drawn while it winds up);</item>
    /// <item>the one left after the other falls a fifth faster again, in flight and in its bursts;</item>
    /// <item>off a wall, the floor or the ceiling it now and then flies straight at the player (where he will be), most of
    /// the time with him kept up in the air (hanging under the ceiling, on a platform).</item>
    /// </list>
    /// </summary>
    [Kit("Oblobble")]
    internal sealed class OblobbleKit : BossKit
    {
        protected override string ControlName => "Fatty Fly Attack";
        protected override float Pace => raging ? 0.75f * 0.8f : 0.75f;
        protected override Look Theme => Look.Infection;
        protected override bool Roams => true;

        bool raging;
        float aimAngle;
        bool aimed;

        protected override void Setup()
        {
            Tempo("Wait", "CD", "Rage Wait");
            var bounce = FindFsm("fat fly bounce");
            if (bounce != null) ScaleFloat("Speed", 1.3f, bounce);
            var rage = FindFsm("Set Rage");
            // Its rage sets its flight to 12: faster still, as the one left.
            if (rage != null && bounce != null)
                OnEnter("Roar Antic", () =>
                {
                    raging = true;
                    SetFloat("Speed", 12f * 1.3f * 1.2f, bounce);
                }, true, rage);
            OnEnter("Attack Antic 2", Aim);
            // Its flight's way after each bounce (Left or Right? reads it to face): at the player, now and then.
            if (bounce != null) OnEnter("Left or Right?", () => AtPlayer(bounce), true, bounce);
            At("Shoot Anim", 1, () =>
            {
                var mod = Float("Angle Mod");
                // The first round's first shot goes out at 45° plus the round's turn.
                if (aimed && mod != null) mod.Value = aimAngle - 45f;
                aimed = false;
            });
        }

        void AtPlayer(PlayMakerFSM bounce)
        {
            if (!HasTarget || !Engaged || Grace || Random.value >= (AirCamping ? 0.7f : 0.3f)) return;
            var angle = Float("Angle", bounce);
            if (angle == null) return;
            var d = Lead(0.4f) - Me;
            if (d.sqrMagnitude < 0.01f) return;
            angle.Value = Mathf.Repeat(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 360f);
            if (AirCamping) BossAi.DevAntiAir++;
        }

        void Aim()
        {
            var at = Lock();
            aimAngle = Batch4.AngleTo(Me, at);
            aimed = true;
            MarkLine(Me, Me + (at - Me).normalized * 14f, Warn(0.4f));
        }
    }
}
