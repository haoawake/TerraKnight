using System.Collections;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Dung Defender's and the White Defender's common part ("Dung Defender" on both; 空洞骑士首领新版AI设计.md §2.2):
    /// <list type="bullet">
    /// <item>his thrown dung balls land where the player is (sometimes where he is going), worked out along their arc, the
    /// spot marked while they fly;</item>
    /// <item>underground for 0.6–1.8 s, never the same; after coming up, a third of the time straight down again (three in a
    /// row at most; more often with the player up off the floor);</item>
    /// <item>he comes up where the player is as the ground bulges (the spot marked), and with the player up off the floor
    /// (a platform, hanging under the ceiling) his eruption carries him up to the player's height (as high as he fits under
    /// the ceiling), his rages' quick ones too; most of his choices then are the dive that ends in it;</item>
    /// <item>his thrown balls and his eruption's fan go up through a player up off the floor, at the top of their arcs;</item>
    /// <item>against the air, and three times in four against a player up off the floor, his eruption throws a fan of dung
    /// up at him too.</item>
    /// </list>
    /// </summary>
    internal abstract class DungKitBase : BossKit
    {
        protected override string ControlName => "Dung Defender";
        protected override Look Theme => Look.Rock;
        protected override bool Burrows => true;

        int chained;
        /// <summary>The game's eruption top; his body's top and middle over his position (measured as he lands).</summary>
        float ownPeak = 12f, bodyTop = 1.6f, bodyMiddle;
        bool arenaTaken;

        /// <summary>The player up off the floor (a platform, hanging under the ceiling), over what the game's eruption reaches.</summary>
        bool High => HasTarget && (TargetHeight > 3f || TargetCenter.y > ownPeak + 1f);
        /// <summary>His own dung balls (the ones his eruption throws), for the fan.</summary>
        GameObject dungShot;
        GameObject DungShot => dungShot != null ? dungShot : dungShot = OwnShots.Of(State("Erupt Out"));

        protected override void Setup()
        {
            if (!Batch2.Claim(Control)) { Dormant = true; return; }
            var peak = Float("Erupt Peak Y");
            if (peak != null) ownPeak = peak.Value;
            OnEnter("Erupt Land", () =>
            {
                RestorePeak();
                Measure();
                // He waits buried under the floor: his arena taken again once he stands on it.
                if (!arenaTaken) { arenaTaken = true; Rearena(); }
            });
            OnEnter("Idle", RestorePeak);
            Branch("Move Choice", "Timer", () => !Grace && High && Random.value < 0.7f);
            OnEnter("Throw 1", AimBall, first: false);
            OnEnter("Timer", RandomBurrow, first: false);
            OnEnter("TD Set", RandomBurrow, first: false);
            OnEnter("Idle", () => chained = 0);
            Branch("Roar?", "Timer", ChainBurrow);
            OnEnter("Erupt Antic", LockErupt);
            More();
        }

        /// <summary>A Defender's own additions (the White Defender's).</summary>
        protected virtual void More() { }

        float MinX => Float("Min X")?.Value ?? Arena.xMin + 2f;
        float MaxX => Float("Max X")?.Value ?? Arena.xMax - 2f;

        /// <summary>The thrown ball's flight bent to land on the player (sometimes where he is going), the spot marked.</summary>
        void AimBall()
        {
            var ball = Go("Dung Ball")?.Value;
            var brb = ball != null ? ball.GetComponent<Rigidbody2D>() : null;
            if (brb == null || Grace) return;
            Vector2 from = ball.transform.position;
            float vy = Mathf.Max(20f, brb.linearVelocity.y);
            float g = Batch2.GravityOf(brb);
            if (High && TargetCenter.y - from.y > 1f)
            {
                // Up through him at the top of its arc (a ball sent higher would break on the ceiling over him).
                brb.linearVelocity = UpTo(from, Lock(), g);
                return;
            }
            Vector2 at = TargetFeet;
            float t = Batch2.FlightTime(vy, at.y - from.y, g);
            if (RollLead()) at = new Vector2(Lead(t).x, at.y);
            t = Batch2.FlightTime(vy, at.y - from.y, g);
            float vx = Mathf.Clamp((at.x - from.x) / Mathf.Max(0.2f, t), -30f, 30f);
            brb.linearVelocity = new Vector2(vx, vy);
            MarkGround(at + Vector2.up * 0.3f, 3f, Mathf.Clamp(t, 0.4f, 2f));
        }

        void RandomBurrow()
        {
            var timer = Float("Timer");
            if (timer != null) timer.Value = Random.Range(0.6f, 1.8f);
        }

        /// <summary>Straight down again after coming up (not the first time, not while raging), a third of the time.</summary>
        bool ChainBurrow()
        {
            if (!(Bool("Intro Attack")?.Value ?? false) || (Int("Rages")?.Value ?? 0) > 0) return false;
            if (chained >= 2 || Random.value >= (High ? 0.6f : 0.35f)) return false;
            chained++;
            return true;
        }

        /// <summary>Where he comes up: under the player (his rages' quick ones aside), the ground there marked.</summary>
        void LockErupt()
        {
            RaisePeak();
            // His rages' quick eruptions come up where he is, but under a player up off the floor all the same.
            if ((Int("Rages")?.Value ?? 0) > 0 && !High || Grace) return;
            float x = Mathf.Clamp(Lock().x, MinX, MaxX);
            var p = transform.position;
            transform.position = new Vector3(x, p.y, p.z);
            MarkGround(new Vector2(x, TargetFeet.y + 0.3f), 5f, Warn(0.5f));
        }


        /// <summary>A throw from a point whose arc tops out at another (straight at it with no gravity on the thing).</summary>
        static Vector2 UpTo(Vector2 from, Vector2 to, float g)
        {
            float dy = to.y - from.y;
            if (g < 0.5f || dy < 0.5f) return (to - from).normalized * 24f;
            float vy = Mathf.Sqrt(2f * g * dy);
            return new Vector2(Mathf.Clamp((to.x - from.x) * g / vy, -30f, 30f), vy);
        }

        /// <summary>
        /// The eruption's top: the player's height when he is up off the floor (a platform, under the ceiling), as high as
        /// the Defender's body fits under the ceiling there; else the game's.
        /// </summary>
        void RaisePeak()
        {
            var peak = Float("Erupt Peak Y");
            if (peak == null) return;
            peak.Value = ownPeak;
            if (Grace || !High) return;
            float ceil = Mathf.Min(CeilingAbove(new Vector2(transform.position.x, TargetCenter.y), 40f), CeilingAbove(TargetCenter, 40f));
            peak.Value = Mathf.Max(ownPeak, Mathf.Min(TargetCenter.y - bodyMiddle, ceil - bodyTop - 0.3f));
        }

        void RestorePeak()
        {
            var peak = Float("Erupt Peak Y");
            if (peak != null) peak.Value = ownPeak;
        }

        void Measure()
        {
            if (body == null || !body.enabled) return;
            var b = body.bounds;
            if (b.size.y < 0.5f) return;
            float y = transform.position.y;
            bodyTop = Mathf.Max(0.5f, b.max.y - y);
            bodyMiddle = b.center.y - y;
        }
    }

    /// <summary>The Dung Defender (Waterways_05_boss, GG_Dung_Defender): his kit is the Defenders' common part.</summary>
    [Kit("DungDefender")]
    internal sealed class DungDefenderKit : DungKitBase
    {
    }
}
