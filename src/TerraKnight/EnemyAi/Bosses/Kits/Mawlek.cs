using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Brooding Mawlek (Mawlek Control, its head's Mawlek Head; 空洞骑士首领新版AI设计.md §2.1):
    /// <list type="bullet">
    /// <item>the head's spit: the game's own spit (its splash where it lands, its harm), lobbed to land where the player is
    /// (from Master on, where he is going, now and then); more often (pace ×0.8);</item>
    /// <item>the big vomit: five of the game's own spit, lobbed evenly over the player's side to the wall;</item>
    /// <item>a player kept up in the air gets a high lob instead of the vomit: five lobs straight up, falling on the spots
    /// round where he stands.</item>
    /// </list>
    /// </summary>
    [Kit("Mawlek")]
    internal sealed class MawlekKit : BossKit
    {
        protected override string ControlName => "Mawlek Control";
        protected override float Pace => 0.8f;
        protected override Look Theme => Look.Infection;

        PlayMakerFSM head;
        bool headOwn, vomitOwn, highLob;
        /// <summary>The game's own spit, as its head's and its body's flings throw it.</summary>
        GameObject headShot, vomitShot;
        /// <summary>The vomit's spots, taken as it winds up.</summary>
        readonly List<Vector2> spots = new List<Vector2>(24);

        protected override void Setup()
        {
            Tempo("Idle", "Super Cooldown");
            head = FindFsm("Mawlek Head");
            if (head != null)
            {
                TempoIn(head, "Idle");
                headShot = OwnShots.Of(State("Shoot", head));
                headOwn = headShot != null && Batch1Helpers.Disable<FlingObjectsFromGlobalPool>(State("Shoot", head));
                if (headOwn) OnEnter("Shoot", HeadSpit, true, head);
            }
            vomitShot = OwnShots.Of(State("Shoot"));
            vomitOwn = vomitShot != null && Batch1Helpers.Disable<FlingObjectsFromGlobalPool>(State("Shoot"));
            if (vomitOwn)
            {
                OnEnter("Super Spit", PlanVomit);
                OnEnter("Shoot", Vomit);
                // A player up in the air: the spit's way (not the jump), and a high lob at its throw.
                Branch("Super Select", "Detect Hero Pos 2", () => highLob = WantAntiAir());
                OnEnter("Super Jump", () => highLob = false);
            }
        }

        /// <summary>The head's spit: a lob to land on the player, from the head.</summary>
        void HeadSpit()
        {
            var from = (Vector2)head.transform.position + Vector2.up * 0.5f;
            var to = BossAi.MasterRules && RollLead() ? Lead(0.8f) : Lock();
            OwnShots.Lob(headShot, from, to, Batch1Helpers.LobTime(from, to));
        }

        /// <summary>The vomit's globs: five, however far the wall (the user's, 2026-10-23: ten could not be got out of).</summary>
        const int VomitGlobs = 5;

        /// <summary>Where the vomit will fall: five globs spread evenly over the player's side to the wall, room between them.</summary>
        void PlanVomit()
        {
            spots.Clear();
            if (highLob)
            {
                var c = TargetFeet;
                for (int i = -2; i <= 2; i++) spots.Add(c + new Vector2(i * 1.4f, 0.1f));
                return;
            }
            float side = TargetCenter.x >= Me.x ? 1f : -1f;
            float floor = GroundBelow(Me);
            float start = Me.x + side * 2f, end = WallTowards(new Vector2(Me.x, floor + 0.6f), side, 40f) - side * 0.6f;
            float length = Mathf.Abs(end - start);
            if (length < 2f) return;
            float step = length / (VomitGlobs - 1);
            for (int i = 0; i < VomitGlobs; i++)
            {
                float d = Mathf.Clamp(i * step + Random.Range(-0.25f, 0.25f) * step, 0f, length);
                spots.Add(new Vector2(start + side * d, floor));
            }
        }

        /// <summary>The vomit (or the high lob) thrown at the spots taken.</summary>
        void Vomit()
        {
            var from = (head != null ? (Vector2)head.transform.position : Me) + Vector2.up * 0.5f;
            for (int i = 0; i < spots.Count; i++)
            {
                var to = spots[i];
                float t = highLob ? 1.05f + i * 0.04f : 0.65f + Mathf.Abs(to.x - from.x) * 0.03f;
                OwnShots.Lob(vomitShot, from, to, t);
            }
            highLob = false;
        }
    }
}
