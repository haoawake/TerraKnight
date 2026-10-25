using System.Collections;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Nightmare King Grimm (the dream's, Godhome's; 空洞骑士首领新版AI设计.md §2.7): the Troupe Master's kit
    /// (<see cref="GrimmFamilyKit"/>: led firebats, drawn dives and dashes, a spike on the player, leaning fireballs, the
    /// uppercut against the air, a quicker pace), and
    /// <list type="bullet">
    /// <item>each flame pillar that comes up under the player (where he is as it comes, the game's own way) marked on the
    /// ground first;</item>
    /// <item>his dive leaves a trail of his own fireballs (his Down's) falling from him.</item>
    /// </list>
    /// </summary>
    [Kit("NightmareGrimm")]
    internal sealed class NightmareGrimmKit : GrimmFamilyKit
    {
        protected override string[] Bats => new[] { "Firebat 1", "Firebat 2", "Firebat 3", "Firebat 4", "Extra Bat" };

        /// <summary>His own fireball (the ones his Down throws).</summary>
        GameObject fireShot;
        GameObject FireShot => fireShot != null ? fireShot : fireShot = OwnShots.Of(State("Down"));

        protected override void SetupMore()
        {
            OnEnter("Pillar", () => MarkGround(TargetFeet, 3f, Warn(0.5f)));
            OnEnter("AD Fire", () => Run(Trail()), first: false);
        }

        /// <summary>Fire dropped from him as he dives: pools where it lands (the floor, or a block the player built).</summary>
        IEnumerator Trail()
        {
            for (int i = 0; i < 6 && StateNow == "AD Fire"; i++)
            {
                OwnShots.Throw(FireShot, Me, new Vector2(Random.Range(-1f, 1f), -6f));
                yield return Batch5.Drip;
            }
        }
    }
}
