using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Watcher Knights ("Black Knight" on each of the six; 空洞骑士首领新版AI设计.md §2.2):
    /// <list type="bullet">
    /// <item>the bouncing roll sometimes aims where the player is going (the landing marked);</item>
    /// <item>the ground roll lasts 1.6 times as long, coming back off the walls (as it does);</item>
    /// <item>two up together share it out: while one rolls, the other bounces at the player;</item>
    /// <item>new, an upward slash: a leap up to the player over him, as high as the room lets;</item>
    /// <item>with the player up off the floor (a platform, hanging under the ceiling), the bouncing roll's leap goes up to
    /// his height and comes across to him there (its launch as fast as that height takes), half the knight's turns.</item>
    /// </list>
    /// Against the air: the upward slash (half the time he is up off the room's ground, always when he stays up there) and
    /// the bounce.
    /// </summary>
    [Kit("BlackKnight")]
    internal sealed class BlackKnightKit : BossKit
    {
        protected override string ControlName => "Black Knight";
        protected override Look Theme => Look.Blade;

        static readonly List<BlackKnightKit> knights = new List<BlackKnightKit>();
        bool rolling;

        protected override void Setup()
        {
            if (!Batch2.Claim(Control)) { Dormant = true; return; }
            knights.Add(this);
            var aim = State("Aim");
            if (aim != null)
            {
                int last = -1;
                for (int i = 0; i < aim.Actions.Length; i++) if (aim.Actions[i] is GetPosition) last = i;
                if (last >= 0) At("Aim", last + 1, LeadJump);
            }
            OnEnter("Jump Antic", MarkJump);
            OnEnter("Charge Antic", () =>
            {
                var t = Float("Charge Timer");
                if (t != null) t.Value *= 1.6f;
                rolling = true;
            }, first: false);
            OnEnter("Charge End", () => rolling = false);
            OnEnter("Set Facing", () => rolling = false);
            if (AddState("TK Uppercut", Uppercut, "Recover") != null) Branch("Idle", "TK Uppercut", WantUppercut);
            Branch("Idle", "Aim", PartnerRolling);
            // The player up there: the bouncing roll at him (its own "Jump Start" waits for him in reach on the floor).
            Branch("Idle", "Aim", () => !Grace && High && Random.value < 0.7f);
            OnEnter("Jump Launch", LaunchHigh, first: false);
        }

        float jumpX;

        /// <summary>The bounce's aim: where the player is, or sometimes where he is going.</summary>
        void LeadJump()
        {
            var x = Float("Hero X");
            if (x == null) return;
            if (!Grace && RollLead()) x.Value = Lead(1f).x;
            jumpX = x.Value;
        }

        void MarkJump() => MarkGround(new Vector2(jumpX, Me.y), 5f, Warn(0.6f));

        /// <summary>The player well up off the room's floor (a platform, a ledge, hanging under the ceiling).</summary>
        bool High => HasTarget && TargetHeight > 3f && TargetCenter.y - Me.y > 2.5f;

        /// <summary>
        /// The bouncing roll's leap (the game's 50 up) against a player up off the floor: as fast as it takes to reach his
        /// height (body at his), across to where he is by then; the game's own leap otherwise.
        /// </summary>
        void LaunchHigh()
        {
            if (rb == null || Grace || !High) return;
            float g = Batch2.GravityOf(rb);
            Vector2 at = Lock();
            float half = body != null && body.enabled ? body.bounds.extents.y : 1f;
            float ceil = CeilingAbove(at + Vector2.down * 0.5f, 40f);
            float h = Mathf.Min(at.y + 0.5f, ceil - half - 0.2f) - Me.y;
            if (h < 1f) return;
            float vy = Mathf.Max(rb.linearVelocity.y, Mathf.Sqrt(2f * g * h));
            float up = (vy - Mathf.Sqrt(Mathf.Max(0f, vy * vy - 2f * g * h))) / Mathf.Max(0.1f, g);
            float vx = Mathf.Clamp((at.x - Me.x) / Mathf.Max(0.15f, up), -40f, 40f);
            rb.linearVelocity = new Vector2(vx, vy);
            var jx = Float("Jump X");
            if (jx != null) jx.Value = vx;
            BossAi.DevMoves++;
        }

        /// <summary>Another knight of the room is rolling now: this one bounces at the player, half the time.</summary>
        bool PartnerRolling()
        {
            if (Vector2.Distance(Me, TargetCenter) > 14f || Random.value >= 0.5f) return false;
            for (int i = knights.Count - 1; i >= 0; i--)
            {
                var k = knights[i];
                if (k == null) { knights.RemoveAt(i); continue; }
                if (k != this && k.rolling && k.Active && k.Scene == Scene) return true;
            }
            return false;
        }

        /// <summary>
        /// The player over him: the upward slash. Close over him, often; up off the room's ground anywhere near (a ledge,
        /// his own platforms, wings), half the time; kept up there, always.
        /// </summary>
        bool WantUppercut()
        {
            if (Grace) return false;
            float dy = TargetCenter.y - Me.y, dx = Mathf.Abs(TargetCenter.x - Me.x);
            if (dy < 1.5f || dx > (High ? 14f : 10f)) return false;
            if (dy <= 3.5f && dx < 4f && Random.value < 0.6f) return true;
            return !TargetOnGround && Random.value < 0.5f || WantAntiAir();
        }

        /// <summary>A leap up to where the player is (as high as the room lets him), slashing all the way.</summary>
        IEnumerator Uppercut()
        {
            var go = gameObject;
            if (rb != null) rb.linearVelocity = Vector2.zero;
            Batch2.Play(go, "Slash Antic");
            float antic = Antic(0.35f);
            Vector2 target = Lock();
            Vector2 from = Me;
            float room = CeilingAbove(from) - from.y - 1f;
            float h = Mathf.Clamp(target.y - from.y + 1f, 2f, Mathf.Max(2f, room));
            MarkLine(from, from + Vector2.up * (h + 1f), antic + 0.05f);
            yield return new WaitForSeconds(antic);
            Batch2.Play(go, "Slash1");
            float g = Batch2.GravityOf(rb);
            float vy = Mathf.Sqrt(2f * g * h);
            float rise = vy / Mathf.Max(0.1f, g);
            float dx = Mathf.Clamp(target.x - from.x, -12f, 12f);
            if (rb != null) rb.linearVelocity = new Vector2(dx / Mathf.Max(0.2f, rise), vy);
            var z = Batch2.Escort(this, transform, Vector2.up * 1f, 3.5f, rise + 0.15f, Look.Blade);
            yield return new WaitForSeconds(rise + 0.15f);
            if (z != null) Destroy(z.gameObject);
            Batch2.Play(go, "Slash1 Recover");
        }
    }
}
