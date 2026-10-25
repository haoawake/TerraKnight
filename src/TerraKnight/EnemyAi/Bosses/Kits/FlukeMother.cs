using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Flukemarm ("Fluke Mother"; her flukeflies are the cage's, each with its own "Fluke Fly"; 空洞骑士首领新版AI设计.md
    /// §2.2):
    /// <list type="bullet">
    /// <item>her flies bolder and quicker: 1.3 times their chase's speed and pull, aimed a little ahead of the player;</item>
    /// <item>she lets them out more often (×0.7 on her wait between broods);</item>
    /// </list>
    /// Against the air: her flies fly.
    /// </summary>
    [Kit("FlukeMother")]
    internal sealed class FlukeMotherKit : BossKit
    {
        protected override string ControlName => "Fluke Mother";
        protected override float Pace => 0.7f;
        protected override Look Theme => Look.Spike;

        readonly List<ChaseObjectV2> chases = new List<ChaseObjectV2>();

        protected override void Setup()
        {
            if (!Batch2.Claim(Control)) { Dormant = true; return; }
            Tempo("Rage");
            Run(Flies());
        }

        /// <summary>The cage (her script finds it as it starts) and its flies' chases, quickened once.</summary>
        IEnumerator Flies()
        {
            GameObject cage = null;
            for (int i = 0; i < 1200 && cage == null; i++)
            {
                cage = Go("Cage")?.Value;
                if (cage == null) yield return null;
            }
            if (cage == null) yield break;
            foreach (var f in cage.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f.FsmName != "Fluke Fly") continue;
                var c = Batch2.Find<ChaseObjectV2>(f.Fsm.GetState("Chase"));
                if (c == null || chases.Contains(c)) continue;
                if (c.speedMax != null) c.speedMax.Value *= 1.3f;
                if (c.accelerationForce != null) c.accelerationForce.Value *= 1.3f;
                chases.Add(c);
            }
            if (chases.Count > 0) Plugin.Log.LogInfo($"[首领AI] 吸虫之母的 {chases.Count} 只飞吸虫加快");
        }

        protected override void Tick()
        {
            // The flies a little ahead of the player (as the common enemies' lead).
            float lead = 0.35f * BossAi.LeadWeight;
            for (int i = 0; i < chases.Count; i++)
            {
                var c = chases[i];
                if (c == null) continue;
                if (c.offsetX != null) c.offsetX.Value = TargetVelocity.x * lead;
                if (c.offsetY != null) c.offsetY.Value = TargetVelocity.y * lead;
            }
        }
    }
}
