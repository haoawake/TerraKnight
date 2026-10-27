using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Massive Moss Charger (Mossy Control; 空洞骑士首领新版AI设计.md §2.1):
    /// <list type="bullet">
    /// <item>in the fight he comes up for the player wherever he is in the room, not only in his alert range;</item>
    /// <item>where he will come up is taken a moment before every emergence (nothing shows it);</item>
    /// <item>his leap goes 60% higher and lands where the player will be (led, the landing marked);</item>
    /// <item>from the second rank half his leaps come up right under where the player is (locked, a longer warning);</item>
    /// <item>from the second rank, half his landings go straight on into another leap at the player from where he came
    /// down (his own leap again);</item>
    /// <item>a player kept up in the air gets the leap.</item>
    /// </list>
    /// </summary>
    [Kit("MegaMossCharger")]
    internal sealed class MegaMossChargerKit : BossKit
    {
        protected override string ControlName => "Mossy Control";
        protected override bool Burrows => true;
        protected override Look Theme => Look.Spore;

        bool under, landed;

        protected override void Setup()
        {
            Tempo("Emerge Pause", "Submerge CD");
            if (AddState("TK Emerge C", () => Surface(false), "Emerge") != null) Divert("Attack Choice", "CHARGE", "TK Emerge C");
            if (AddState("TK Emerge L", () => Surface(true), "Leap Start") != null)
            {
                Divert("Attack Choice", "LEAP", "TK Emerge L");
                // His leap is his only move against the air: a player kept up there (three seconds and more) gets it every
                // time, a charge along the floor under him being no move at all.
                Branch("Attack Choice", "TK Emerge L", () => WantAntiAir() || !Grace && AirForced && TargetHeight > 3f);
            }
            OnEnter("Leap Launch", Launch, first: false);
            // In the air until he is back under his ground's height: lodged somewhere over it, he comes down after a while.
            OnEnter("In Air", () => Run(LandAfter(4f)));
            // His charge ends only on running into the room's ground (layer 8): held up by anything else (a battle gate),
            // or pressed on rock it already touches, he goes under after a moment.
            OnEnter("Charge", () => Run(ChargeLimit()));
            // Hidden, he waits for the player to come into his alert range (a trigger round his ground); in the fight he
            // comes up for him wherever he is in the room (on a platform over him, up a wall).
            Branch("Hidden", "Emerge Pause", () => Engaged && HasTarget);
            // A player past his ground's ends (a ledge at the room's side) sends him back under to wait: in the fight he
            // comes up all the same, at the end of his ground nearest the player.
            Branch("Hero Beyond?", "Left or Right?", () => Engaged && HasTarget);
            OnEnter("Land", () => landed = true);
            OnEnter("Emerge", () => landed = false);
            // Down from a leap and going under: up again at once from there instead, now and then.
            Branch("Submerge 1", "Leap Start", Releap);
        }

        /// <summary>Another leap from where he came down (his own leap starts at Appear X): from his second rank, half the time, never a third.</summary>
        bool Releap()
        {
            bool was = landed;
            landed = false;
            if (!was || Rank < 1 || Grace || Random.value >= 0.5f) return false;
            var ax = Float("Appear X");
            if (ax != null) ax.Value = Me.x;
            return true;
        }

        /// <summary>Where he will come up (the game's Appear X, or under the player), a moment before he does.</summary>
        IEnumerator Surface(bool leap)
        {
            var ax = Float("Appear X");
            under = leap && Rank >= 1 && Random.value < 0.5f && ax != null;
            if (under)
            {
                float min = Float("X Min")?.Value ?? Arena.xMin, max = Float("X Max")?.Value ?? Arena.xMax;
                ax.Value = Mathf.Clamp(Lock().x, Mathf.Min(min, max), Mathf.Max(min, max));
            }
            if (ax != null)
            {
                float lo = Float("X Min")?.Value ?? Arena.xMin, hi = Float("X Max")?.Value ?? Arena.xMax;
                ax.Value = Mathf.Clamp(ax.Value, Mathf.Min(lo, hi), Mathf.Max(lo, hi));
            }
            float x = ax != null ? ax.Value : Me.x;
            float startY = Float("Start Y")?.Value ?? Me.y;
            var ground = new Vector2(x, GroundBelow(new Vector2(x, startY + 2f)));
            float warn = Warn(under ? 0.7f : 0.5f);
            MarkGround(ground, under ? 5f : 7f, warn);
            yield return new WaitForSeconds(warn);
        }

        System.Collections.IEnumerator ChargeLimit()
        {
            float until = Time.time + 4f, still = 0f;
            yield return null;
            while (Active && StateNow == "Charge")
            {
                still = rb != null && Mathf.Abs(rb.linearVelocity.x) < 1f ? still + Time.deltaTime : 0f;
                if (still > 0.5f || Time.time > until)
                {
                    Control.SendEvent("SUBMERGE");
                    yield break;
                }
                yield return null;
            }
        }

        /// <summary>
        /// His own landing is coming back down to his ground's height (Start Y). Come down on something over it (a ledge, a
        /// platform the player stood on: the user's 10-25 "跳到左边的平台上会卡好几秒"), he hops off it towards his
        /// ground's middle at once; still up there after three hops, or in the air the whole while, he lands as his script
        /// does (back down at his ground's height).
        /// </summary>
        System.Collections.IEnumerator LandAfter(float seconds)
        {
            float until = Time.time + seconds, restingSince = -1f, nextHop = 0f;
            float startY = Float("Start Y")?.Value ?? float.NegativeInfinity;
            int hops = 0;
            while (Time.time < until)
            {
                if (!Active || StateNow != "In Air") yield break;
                bool resting = rb != null && Mathf.Abs(rb.linearVelocity.y) < 0.2f && transform.position.y > startY + 0.5f;
                restingSince = resting ? (restingSince < 0f ? Time.time : restingSince) : -1f;
                if (resting && !DevNoHop && Time.time - restingSince > 0.15f && Time.time >= nextHop)
                {
                    if (++hops > 3) break;
                    float lo = Float("X Min")?.Value ?? Arena.xMin, hi = Float("X Max")?.Value ?? Arena.xMax;
                    float dir = Mathf.Sign((lo + hi) / 2f - Me.x);
                    rb.linearVelocity = new Vector2(dir * 10f, 8f);
                    var s = transform.localScale;
                    transform.localScale = new Vector3(dir * Mathf.Abs(s.x), s.y, s.z);
                    nextHop = Time.time + 0.5f;
                    DevHops++;
                }
                yield return null;
            }
            if (Active && StateNow == "In Air") Control.SendEvent("LAND");
        }

        /// <summary>Dev: hops off something over his ground; none (the suite's comparison).</summary>
        public static int DevHops;
        public static bool DevNoHop;

        /// <summary>
        /// The leap: 60% higher (×1.265 up), its sideways speed to come down where the player will be; against a player up
        /// high, as high as it takes to pass his middle (under the ceiling), its top over him; the landing marked.
        /// </summary>
        void Launch()
        {
            if (rb == null) return;
            float g = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0.1f, rb.gravityScale);
            // As high as the room lets him: his top short of the ceiling (a low room's ceiling would take him in).
            float half = body != null ? body.bounds.extents.y : 1.5f;
            // Under the lower of the ceilings over where he starts and over where he goes to.
            float roofHere = CeilingAbove(Me, 40f), roofThere = CeilingAbove(new Vector2(TargetCenter.x, Me.y), 40f);
            float room = Mathf.Min(roofHere, roofThere) - Me.y - half - 0.6f;
            float roofVy = Mathf.Sqrt(2f * g * Mathf.Max(1f, room));
            float vy = Mathf.Min(30f * 1.265f, roofVy);
            // A player up high (a platform, under the ceiling, clinging to a wall): the leap goes as high as it takes for
            // his body's middle to pass the player's middle at its top (the user's 10-25: a player on a wall over eight tiles
            // was out of his reach), under the ceiling; the top over him, not the landing.
            bool high = TargetHeight > 3f;
            if (high)
            {
                float need = TargetCenter.y + 0.6f - Me.y;
                if (need > 0f) vy = Mathf.Clamp(Mathf.Sqrt(2f * g * need), vy, roofVy);
            }
            float t = 2f * vy / g;
            var p = Lead(t + 0.1f);
            if (high) p = Lead(vy / g);
            float vx = Mathf.Clamp((p.x - Me.x) / (high ? vy / g : t), -30f, 30f);
            rb.linearVelocity = new Vector2(vx, vy);            if (Mathf.Abs(vx) > 0.5f)
            {
                var s = transform.localScale;
                transform.localScale = new Vector3(Mathf.Sign(vx) * Mathf.Abs(s.x), s.y, s.z);
            }
            float startY = Float("Start Y")?.Value ?? Me.y;
            MarkGround(new Vector2(Me.x + vx * t, startY + 1f), 6f, t);
        }
    }
}
