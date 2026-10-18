using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        /// <summary>
        /// A hail of hits (ten pushes in half a second, as the Zenith's swords and the Star Wrath's stars land them) carries
        /// an enemy no further than one push: once it flies it isn't lifted again before it comes down.
        /// </summary>
        IEnumerator KnockJuggle17()
        {
            yield return GoTo("Crossroads_15", EntryGateOf("Crossroads_15"));
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1f);
            HealthManager target = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm != null && !hm.isDead && hm.gameObject.activeInHierarchy && !TerraKnockback.Immune(hm) && EnemyStats.Of(hm) != null
                    && hm.GetComponent<Rigidbody2D>() is Rigidbody2D rb && rb.bodyType == RigidbodyType2D.Dynamic) { target = hm; break; }
            if (target == null) { FxCheck("连击不把怪越推越远", false, "这里没有能击飞的敌人"); yield break; }
            target.hp = Mathf.Max(target.hp, 2000);
            float farthest = 0f;
            IEnumerator Watch(float x0, float seconds)
            {
                farthest = 0f;
                for (float end = Time.time + seconds; Time.time < end && target != null;)
                {
                    farthest = Mathf.Max(farthest, Mathf.Abs(target.transform.position.x - x0));
                    yield return null;
                }
            }
            // One push, as far as it goes.
            float x0 = target.transform.position.x;
            TerraKnockback.Push(target, 6f, 1f);
            yield return Watch(x0, 1.2f);
            float one = farthest;
            yield return new WaitForSeconds(TerraKnockback.SteadyWindow + 0.5f);
            // Ten within half a second, the same way.
            int held0 = TerraKnockback.DevHeldInFlight, steady0 = TerraKnockback.DevHeldBySteady;
            x0 = target.transform.position.x;
            for (int i = 0; i < 10; i++)
            {
                TerraKnockback.Push(target, 6f, 1f);
                yield return new WaitForSeconds(0.05f);
            }
            yield return Watch(x0, 1.2f);
            float ten = farthest;
            int held = TerraKnockback.DevHeldInFlight - held0, steadied = TerraKnockback.DevHeldBySteady - steady0;
            FxCheck("半秒连击 10 次不比打一下飞得远", one > 0.3f && ten <= one * 1.4f + 0.4f && held > 0,
                $"{target.name}：打一下退 {one:F2}，连击 10 次退 {ten:F2}；空中挡下 {held} 次，站稳挡下 {steadied} 次");
        }
    }
}
