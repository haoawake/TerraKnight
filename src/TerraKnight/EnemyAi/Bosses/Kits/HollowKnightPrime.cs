using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Pure Vessel (Godhome, HK Prime's Control; 空洞骑士首领新版AI设计.md §2.8):
    /// <list type="bullet">
    /// <item>its focus's blasts (the HK Prime Blast's own scripts): every other one comes up where the player is as its
    /// turn comes, one after another (a ring there, then the blast's own swelling);</item>
    /// <item>its own answer to a player kept up in the air (its leap at him and the stomp down) comes sooner, and the leap
    /// sometimes goes where he is going; against a player up high the leap goes as high as he is (its own leap's speed is
    /// set for a fixed height), across to under him, its body hurting on the way up;</item>
    /// <item>its stands between moves at the kit's pace.</item>
    /// </list>
    /// </summary>
    [Kit("HollowKnightPrime")]
    internal sealed class HollowKnightPrimeKit : BossKit
    {
        protected override Look Theme => Look.Void;

        float nextLeap;

        protected override void Setup()
        {
            At("Set Idle Time", 2, () => { var t = Float("Idle Time"); if (t != null) t.Value *= TempoNow; });
            At("Dstab Aim", 2, () => { var x = Float("Hero X"); if (x != null && RollLead()) x.Value = Lead(0.6f).x; });
            Branch("Phase?", "Dstab Antic", LeapAtAir);
            // Dstab Jump: its leap's speed (Jump Y, Jump X) is set just before the velocity; raised there for a player up high.
            int vi = Batch4.IndexOf(State("Dstab Jump"), "SetVelocity2d");
            if (vi >= 0) At("Dstab Jump", vi, LeapHigh);
            // The blasts' scripts once they are up (their objects start the fight still).
            OnEnter("Focus Charge", Blasts);
        }

        /// <summary>The leap as fast as it takes to reach the player's height (under the ceiling), arriving under him at its top.</summary>
        void LeapHigh()
        {
            if (rb == null || !HasTarget || TargetHeight < 3f) return;
            var jy = Float("Jump Y");
            var jx = Float("Jump X");
            if (jy == null) return;
            float g = Batch2.GravityOf(rb);
            float half = body != null ? body.bounds.extents.y : 1.2f;
            float top = Mathf.Min(TargetCenter.y, CeilingAbove(new Vector2(TargetCenter.x, Me.y), 40f) - half - 0.3f);
            float h = top - Me.y;
            if (h <= 0f) return;
            float vy = Mathf.Sqrt(2f * g * h);
            if (vy <= jy.Value) return;
            jy.Value = vy;
            float t = vy / g;
            if (jx != null) jx.Value = Mathf.Clamp((TargetCenter.x - Me.x) / t, -30f, 30f);
            // Its body hurts all the way up (its own leap's pictures show it: the hitbox unseen).
            var m = NewMove(2, Look.Void);
            m.Width = 5f;
            m.Height = 6f;
            var z = Strike(Hostiles.Zone.Shape.Box, Me, 0.02f, t + 0.15f, m);
            if (z != null) { z.Follow = transform; z.FollowOffset = Me - (Vector2)transform.position; z.Unseen = true; }
        }

        bool blastsDone, blastsMissed;

        /// <summary>A player kept up in the air: its leap at him, sooner than its own choice would come to it.</summary>
        bool LeapAtAir()
        {
            if (Time.time < nextLeap || Grace || !WantAntiAir()) return false;
            nextLeap = Time.time + 3.5f;
            return true;
        }

        /// <summary>The focus's blasts: every other one, as it picks its place, takes the player's (a ring there first).</summary>
        void Blasts()
        {
            if (blastsDone) return;
            var found = new List<PlayMakerFSM>();
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(false))
                    if (f.FsmName == "Control" && f.gameObject.name.StartsWith("HK Prime Blast")) found.Add(f);
            found.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));
            for (int i = 0; i < found.Count; i += 2)
            {
                var blast = found[i];
                System.Action place = () => PlaceBlast(blast);
                OnEnter("Pos Low", place, first: false, f: blast);
                OnEnter("Pos High", place, first: false, f: blast);
            }
            // None up yet: looked for again at the next focus.
            blastsDone = found.Count > 0;
            if (!blastsDone && !blastsMissed)
            {
                blastsMissed = true;
                Plugin.Log.LogWarning("[首领AI] 纯粹容器：还找不到聚焦爆破，下次聚焦再找");
            }
        }

        void PlaceBlast(PlayMakerFSM blast)
        {
            if (blast == null || Grace) return;
            Vector2 at = ClampToArena(Lock(), 1.5f);
            blast.transform.position = new Vector3(at.x, at.y, blast.transform.position.z);
            MarkCircle(at, 3f, 0.8f);
        }
    }
}
