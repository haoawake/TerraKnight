using System.Collections;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Radiance and the Absolute Radiance (Control, Attack Choices, Attack Commands, Teleport, Phase Control;
    /// 空洞骑士首领新版AI设计.md §2.10): faster (her waits ×0.75, the nail sweeps' long waits cut to just after their last
    /// wave), and from two thirds of her life down her own orbs beside her sweeps: a few of them out round her, hunting the
    /// player. The climb (her script's Ascend Cast: only her aimed beam, "三阶段只会无脑射激光", the user's 10-25) has her
    /// other attacks between its beams: her orbs, a comb of her swords brought over the climbing player falling down on
    /// him, her nail fan. Nothing of the mod's drawn (the user's, 2026-10-23: only her own pictures). Her phases, the
    /// spikes, the platforms and the ending go on as they did.
    /// </summary>
    [Kit("FinalBoss")]
    internal sealed class RadianceKit : BossKit
    {
        protected override float Pace => 0.75f;
        protected override Look Theme => Look.Soul;
        /// <summary>She goes all over her two arenas and the climb: the watchdog leaves her be.</summary>
        protected override bool Roams => true;

        PlayMakerFSM choices, commands, teleport, phases;
        bool climbOrbCast;

        public static int DevClimbMoves, DevClimbOrbs, DevClimbRain, DevClimbFans;

        /// <summary>Her own sword comb (her Comb Top's: a row of her swords with gaps that fly off together).</summary>
        GameObject combShot;
        GameObject CombShot => combShot != null ? combShot : combShot = commands != null ? OwnShots.Of(State("Comb Top", commands)) : null;

        /// <summary>When her climb's aimed beam last began (Attack Commands' Aim: its beam out and over within 1.15 s of 1.75).</summary>
        float aimAt = -10f;
        bool rainMissing;

        protected override void Setup()
        {
            choices = FindFsm("Attack Choices");
            commands = FindFsm("Attack Commands");
            teleport = FindFsm("Teleport");
            phases = FindFsm("Phase Control");
            // Her waits, at ×0.75 (and the difficulty's).
            Tempo("Arena 1 Idle", "Arena 2 Idle", "A1 Cast End", "A2 Cast End");
            if (choices != null) TempoIn(choices, "A1 Pause", "A2 Pause", "Eye Beam Recover", "Nail Fan Recover", "Orb Recover");
            if (commands != null) TempoIn(commands, "Spawn Fireball");
            if (commands != null) OnEnter("Orb Antic", () =>
            {
                if (climbOrbCast && Int("Shots", commands) is var shots && shots != null) shots.Value = 1;
            }, first: false, f: commands);
            // Her arenas move: where she arrives, and each phase's, is the fight's now (Lead and ClampToArena go by it).
            if (teleport != null) OnEnter("Arrive", Rearena, first: false, f: teleport);
            if (phases != null)
                foreach (var s in new[] { "Set Phase 2", "Set Phase 3", "Stun 1", "Set Ascend" }) OnEnter(s, Rearena, f: phases);
            // The climb: her other attacks between the aimed beams.
            if (commands != null) OnEnter("Aim", () => aimAt = Time.time, f: commands);
            OnEnter("Ascend Cast", () => Run(Climb()));
        }

        // ------------------------------------------------------------------ the climb: her orbs, her swords from above, her fan

        IEnumerator Climb()
        {
            yield return new WaitForSeconds(2.5f);
            // The three in a shuffled round each, never the same twice running.
            var bag = new System.Collections.Generic.List<int>();
            int last = -1;
            while (Control != null && Control.ActiveStateName == "Ascend Cast" && Hm != null)
            {
                yield return new WaitForSeconds(Mathf.Max(2.2f, 4f * TempoNow));
                if (Control == null || Control.ActiveStateName != "Ascend Cast") yield break;
                // Hurt just now, or up at her side to end it: left be.
                if (Grace || !HasTarget || Vector2.Distance(TargetCenter, Me) < 7f) continue;
                if (bag.Count == 0)
                {
                    bag.AddRange(new[] { 0, 1, 2 });
                    for (int i = bag.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (bag[i], bag[j]) = (bag[j], bag[i]); }
                    if (bag[0] == last) { bag.RemoveAt(0); bag.Add(last); }
                }
                int pick = bag[0];
                bag.RemoveAt(0);
                last = pick;
                yield return pick == 0 ? ClimbOrbs() : pick == 1 ? SwordRain() : Fan();
            }
        }

        IEnumerator ClimbOrbs()
        {
            if (commands == null) yield break;
            float ready = Time.time + 2.5f;
            while (Time.time < ready && !(commands.ActiveStateName == "Aim" && Time.time - aimAt >= 1.3f)) yield return null;
            if (commands.ActiveStateName != "Aim" || StateNow != "Ascend Cast") yield break;
            var bounds = new[] { Float("Orb Min X", commands), Float("Orb Max X", commands), Float("Orb Min Y", commands), Float("Orb Max Y", commands) };
            var distance = Batch3.Find<HutongGames.PlayMaker.Actions.DistanceBetweenPoints>(State("Orb Pos", commands));
            if (System.Array.Exists(bounds, v => v == null) || distance == null) yield break;
            var saved = System.Array.ConvertAll(bounds, v => v.Value);
            var oldReference = distance.point2;
            Vector2 locked = Lock();
            try
            {
                // The original lower arena is nowhere near the climb. Its native 3..11 unit
                // rejection test otherwise retries synchronously forever. Sample a guaranteed
                // valid ring near the locked player position, independently of later movement.
                bounds[0].Value = locked.x - .5f; bounds[1].Value = locked.x + .5f;
                bounds[2].Value = locked.y + 4f; bounds[3].Value = locked.y + 5f;
                distance.point2 = new HutongGames.PlayMaker.FsmVector3 { Value = locked };
                climbOrbCast = true;
                DevClimbOrbs++;
                DevClimbMoves++;
                commands.SendEvent("ORBS");
                float end = Time.time + 8f;
                while (Time.time < end && commands.ActiveStateName != "Orb End" && commands.ActiveStateName != "End"
                    && StateNow == "Ascend Cast") yield return null;
            }
            finally
            {
                for (int i = 0; i < bounds.Length; i++) bounds[i].Value = saved[i];
                distance.point2 = oldReference;
                climbOrbCast = false;
                if (commands != null && commands.isActiveAndEnabled && StateNow == "Ascend Cast")
                    commands.SendEvent("ASCEND BEAM");
            }
        }

        /// <summary>
        /// One of her combs as her Nail Top Sweep 2 makes it (Type 6: swords pointing down), brought from arena 2's top to
        /// over the climbing player once its swords are out and turning (its Antic, 0.4 s before they fly).
        /// </summary>
        IEnumerator SwordRain()
        {
            var prefab = CombShot;
            if (prefab == null)
            {
                if (!rainMissing) Plugin.Log.LogInfo("[首领AI] 辐光爬升：找不到她自己的剑阵（Comb Top），不出剑雨");
                rainMissing = true;
                yield break;
            }
            var comb = ObjectPool.Spawn(prefab, new Vector3(Me.x, Me.y, prefab.transform.position.z), Quaternion.identity);
            if (comb == null) yield break;
            var fsm = PlayMakerFSM.FindFsmOnGameObject(comb, "Control");
            var type = fsm != null ? fsm.FsmVariables.FindFsmInt("Type") : null;
            if (type == null)
            {
                if (!rainMissing) Plugin.Log.LogInfo("[首领AI] 辐光爬升：剑阵没有 Type，不出剑雨");
                rainMissing = true;
                comb.SetActive(false);
                yield break;
            }
            type.Value = 6;
            DevClimbRain++;
            DevClimbMoves++;
            BossAi.DevMoves++;
            float until = Time.time + 1.5f;
            while (Time.time < until && comb.activeInHierarchy && fsm.ActiveStateName != "Antic") yield return null;
            if (!comb.activeInHierarchy || fsm.ActiveStateName != "Antic") yield break;
            Vector2 over = TargetCenter + new Vector2(Random.Range(-1.5f, 1.5f), 11f);
            comb.transform.position = new Vector3(over.x, over.y, comb.transform.position.z);
        }

        /// <summary>Her nail fan (Attack Commands' NAIL FAN) between two aimed beams, then her beam again (ASCEND BEAM).</summary>
        IEnumerator Fan()
        {
            if (commands == null) yield break;
            float until = Time.time + 2.5f;
            while (Time.time < until && !(commands.ActiveStateName == "Aim" && Time.time - aimAt >= 1.3f)) yield return null;
            if (commands.ActiveStateName != "Aim" || Control == null || Control.ActiveStateName != "Ascend Cast") yield break;
            commands.SendEvent("NAIL FAN");
            DevClimbFans++;
            DevClimbMoves++;
            BossAi.DevMoves++;
            float end = Time.time + 5f;
            while (Time.time < end && commands.ActiveStateName != "End" && Control.ActiveStateName == "Ascend Cast") yield return null;
            if (Control.ActiveStateName == "Ascend Cast") commands.SendEvent("ASCEND BEAM");
        }

        // ------------------------------------------------------------------ her sweeps: shorter, her orbs beside them



    }
}
