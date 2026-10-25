using System.Collections;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Soul Master and the Soul Tyrant ("Mage Lord", the second phase's body "Mage Lord 2"; 空洞骑士首领新版AI设计.md §2.2):
    /// <list type="bullet">
    /// <item>the quake's ground waves 1.4 times as fast;</item>
    /// <item>after a teleport, a third of the time a homing soul orb (two from a third of his life);</item>
    /// <item>the quake sometimes falls where the player is going; its spot marked; the second phase's quakes hold their
    /// line (no creeping after him) and are each marked in turn;</item>
    /// <item>the four orbs' sweep at the player's height (a line across first);</item>
    /// <item>his idles a little shorter.</item>
    /// </list>
    /// Against the air: the homing orbs, the sweep at the player's height; the second phase, with the player up out of its
    /// quakes' reach (hanging under the ceiling, on a high platform), goes for its fireball volley (his own: away, then the
    /// fireballs after him) every few seconds and sends its own fireballs at him in between.
    /// </summary>
    [Kit("MageLord")]
    internal sealed class MageLordKit : BossKit
    {
        protected override string ControlName
        {
            get
            {
                foreach (var f in GetComponents<PlayMakerFSM>()) if (f.FsmName == "Mage Lord 2") return "Mage Lord 2";
                return "Mage Lord";
            }
        }

        protected override Look Theme => Look.Soul;
        // He teleports out of the room and hides under it between moves.
        protected override bool Roams => true;
        protected override bool Burrows => true;

        bool Phase2 => Control != null && Control.FsmName == "Mage Lord 2";
        /// <summary>His own soul orb (his Shot's; the second phase's fireball, its Spawn Fireball's), homing as it does.</summary>
        GameObject orbShot;
        GameObject OrbShot => orbShot != null ? orbShot : orbShot = OwnShots.Of(State(Phase2 ? "Spawn Fireball" : "Shot"));
        float nextAirOrbs, nextVolley;

        protected override void Setup()
        {
            if (!Batch2.Claim(Control)) { Dormant = true; return; }
            At("Set Idle Timer", 1, () =>
            {
                var t = Float("Idle Timer");
                if (t != null) t.Value *= TempoNow;
            });
            if (Batch2.Has(Control, "Shot CD")) Tempo("Shot CD");
            if (!Phase2) Tempo("Quake Waves", "HS Stop");
            if (!Phase2 && Float("Shockwave Speed") != null) ScaleFloat("Shockwave Speed", 1.4f);
            LeadAfterHero("Tele Quake");
            if (Batch2.Has(Control, "Tele Quake 2")) LeadAfterHero("Tele Quake 2");
            OnEnter("TeleportQ", MarkQuake);
            if (Phase2) Batch2.Disable<ChaseObjectGround>(State("Quake Antic"));
            OnEnter("After Tele", () =>
            {
                if (Random.value < 0.35f) Run(Orbs(Rank >= 2 ? 2 : 1, 0.35f));
            });
            if (Batch2.Has(Control, "Shot"))
                OnEnter("Shot", () =>
                {
                    if (Rank >= 2) Run(Orbs(1, 0.2f));
                }, first: false);
            if (!Phase2)
            {
                OnEnter("Tele Top Y", SpinnerHeight, first: false);
                Branch("Attack Choice", "Tele Spinner X", WantAntiAir);
            }
            else
            {
                // The player kept up out of the quakes' reach (its Tele Quake cancels to Wait Frame, a frame at a time): its
                // fireball volley every few seconds, and fireballs at him every two seconds or so in between.
                if (Batch2.Has(Control, "Shoot Start"))
                    Branch("Wait Frame", "Shoot Start", () =>
                    {
                        if (!HasTarget || Grace || Time.time < nextVolley) return false;
                        nextVolley = Time.time + 5f;
                        BossAi.DevAntiAir++;
                        return true;
                    });
                OnEnter("Wait Frame", () =>
                {
                    if (Time.time < nextAirOrbs) return;
                    nextAirOrbs = Time.time + 2.2f;
                    Run(Orbs(2, 0.4f));
                });
                // A quake that could not reach him (up over its fall, or off past its side): fireballs at him from the landing.
                OnEnter("Quake Land", () =>
                {
                    if (!HasTarget || !AirCamping || Mathf.Abs(TargetCenter.x - Me.x) < 2.5f || Time.time < nextAirOrbs) return;
                    nextAirOrbs = Time.time + 2.2f;
                    BossAi.DevAntiAir++;
                    Run(Orbs(2, 0.3f));
                }, first: false);
            }
        }

        /// <summary>The quake's x, read from the hero, led sometimes: right after his position is taken.</summary>
        void LeadAfterHero(string state)
        {
            var s = State(state);
            if (s == null) return;
            int i = 0;
            for (; i < s.Actions.Length; i++) if (s.Actions[i] is GetPosition) break;
            if (i >= s.Actions.Length) return;
            At(state, i + 1, () =>
            {
                var x = Float("Tele X");
                if (x != null && !Grace && RollLead()) x.Value = Lead(0.7f).x;
            });
        }

        void MarkQuake()
        {
            var x = Float("Tele X");
            if (x == null) return;
            MarkGround(new Vector2(x.Value, Me.y), 6f, Warn(0.75f));
        }

        /// <summary>The high spinner's sweep at the player's height (within the arena), a line across first.</summary>
        void SpinnerHeight()
        {
            var y = Float("Tele Y");
            if (y == null) return;
            float bot = Float("Bot Y")?.Value ?? y.Value - 4f;
            float h = Mathf.Clamp(Lock().y, bot, Arena.yMax - 2f);
            y.Value = h;
            float l = Float("Left X")?.Value ?? Arena.xMin, r = Float("Right X")?.Value ?? Arena.xMax;
            MarkLine(new Vector2(l, h), new Vector2(r, h), Warn(0.9f) + 0.4f);
        }

        /// <summary>Homing soul orbs after a short charge.</summary>
        IEnumerator Orbs(int count, float charge)
        {
            yield return new WaitForSeconds(Antic(charge));
            for (int i = 0; i < count; i++)
            {
                Vector2 from = Me + Vector2.up * 0.8f;
                Vector2 dir = (TargetCenter - from).normalized;
                if (count > 1) dir = Quaternion.Euler(0f, 0f, (i - (count - 1) * 0.5f) * 40f) * dir;
                OwnShots.Throw(OrbShot, from, dir * 9f, fire: true);
                yield return new WaitForSeconds(0.15f);
            }
        }
    }
}
