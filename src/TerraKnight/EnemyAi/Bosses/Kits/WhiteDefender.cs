using System.Collections;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The White Defender (Dream_04_White_Defender, GG_White_Defender; "Dung Defender"): the Defenders' common part
    /// (DungKitBase), and his ground slam raises one more of his own dung pillars under the player (where he is as the slam
    /// winds up), beside the room's own.
    /// </summary>
    [Kit("WhiteDefender")]
    internal sealed class WhiteDefenderKit : DungKitBase
    {
        protected override Look Theme => Look.Spike;

        protected override void More()
        {
        }


    }
}
