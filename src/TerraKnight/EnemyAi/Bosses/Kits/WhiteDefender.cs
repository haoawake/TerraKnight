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
            OnEnter("G Slam Antic", PillarUnderPlayer);
        }

        void PillarUnderPlayer()
        {
            if (Grace || !RoomForStrike) return;
            var at = OnGround(Lock());
            var pillar = OwnShots.Of(State("Pillar"));
            if (pillar != null) Run(OwnPillar(pillar, at));
        }

        /// <summary>His own pillar (his Pillar's), at the height his own are set, its harm as his.</summary>
        IEnumerator OwnPillar(GameObject prefab, Vector2 at)
        {
            yield return new WaitForSeconds(Warn(0.6f));
            var set = Batch3.Find<SetPosition>(State("Pillar"));
            float y = set != null && set.y != null && !set.y.IsNone ? set.y.Value : at.y;
            var go = ObjectPool.Spawn(prefab, new Vector3(at.x, y, 0.003f), Quaternion.identity);
            if (go == null) yield break;
            var s = go.transform.localScale;
            go.transform.localScale = new Vector3(Mathf.Abs(s.x) * (Random.value < 0.5f ? -1f : 1f), s.y, s.z);
            var harm = go.GetComponent<DamageHero>();
            var amount = Int("Damage");
            if (harm != null && amount != null && amount.Value > 0) harm.damageDealt = amount.Value;
            BossAi.DevMoves++;
        }
    }
}
