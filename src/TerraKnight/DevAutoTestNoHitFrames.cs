using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        /// <summary>
        /// Suite hitframes: independent impacts in the same frame all deal damage, including
        /// fractional HK health carried between hits. Exercise each combat classification on
        /// an ordinary enemy so scripted boss phases cannot hide a damage admission failure.
        /// </summary>
        IEnumerator NoHitFramesTest(string scene)
        {
            HealthManager target = null;
            Collider2D collider = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene || EnemyStats.Untouchable(hm)) continue;
                var info = EnemyStats.Of(hm);
                if (info.Boss || info.Elite || info.Ref != null) continue;
                collider = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                if (collider == null) continue;
                target = hm;
                break;
            }
            if (target == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 同帧命中：没有可受伤的普通敌人，不通过");
                yield break;
            }
            var saved = EnemyStats.Of(target);
            int oldHp = target.hp, oldMax = saved.MaxHp;
            bool oldBoss = saved.Boss, oldElite = saved.Elite;
            float oldCarry = saved.Carry, oldLastHit = saved.LastHit;
            try
            {
                foreach (var kind in new[] { "普通怪", "精英", "首领" })
                {
                    target.hp = 100000;
                    saved.MaxHp = target.hp;
                    saved.Carry = 0f;
                    saved.Boss = kind == "首领";
                    saved.Elite = kind == "精英";
                    int admitted = 0, damaged = 0;
                    // No yield: six pellets followed by six minion impacts at the exact same Time.time.
                    for (int hit = 0; hit < 12; hit++)
                    {
                        int hp = target.hp;
                        float carry = saved.Carry;
                        if (mod.Melee.HitWith(collider, 100, 0f, 0f, 1f, collider.bounds.center)) admitted++;
                        if (target.hp < hp || (target.hp == hp && saved.Carry > carry)) damaged++;
                    }
                    bool pass = admitted == 12 && damaged == 12;
                    Plugin.Log.LogInfo($"[AutoTest] 同帧命中 {kind}：12 次独立冲击，接受 {admitted}，实际扣血/累计余数 {damaged}，{(pass ? "通过" : "不通过")}");
                }
            }
            finally
            {
                if (target != null) target.hp = oldHp;
                saved.MaxHp = oldMax;
                saved.Boss = oldBoss;
                saved.Elite = oldElite;
                saved.Carry = oldCarry;
                saved.LastHit = oldLastHit;
            }
        }
    }
}
