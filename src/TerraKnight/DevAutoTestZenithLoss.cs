using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "zenithloss", the user's 10-29 "新的天顶剑是不是有丢失伤害的情况"): the remade Zenith at Dirtmouth's Target
    /// Dummy, each of its five patterns alone (ZenithSwords.DevForce) for 5 s from 2, 6 and 12 units off: its swords'
    /// flights, those whose blade lay on the dummy and struck nothing, the blows sent against the hits the dummy took, and
    /// the damage a second. Lines "[AutoTest] ZenithLoss …"; passes when no flight that touched it was lost and every blow
    /// sent was taken.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ZenithLossTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(6f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            BladeArts.Set(true);
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt;
            if (!at.HasValue) { Plugin.Log.LogInfo("[AutoTest] ZenithLoss 没有训练假人"); yield break; }
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            var aim = at.Value + new Vector2(0f, 0.8f);
            var inv = mod.Inventory;
            var saved = inv.Slots[0];
            int savedSel = inv.Selected;
            inv.Slots[0] = ItemStack.Of(ItemType.Zenith);
            inv.Selected = 0;
            int lostAll = 0, missingAll = 0;
            var patterns = new[] { ZenithSwords.Pattern.Loop, ZenithSwords.Pattern.Cross, ZenithSwords.Pattern.Spiral, ZenithSwords.Pattern.Rain, ZenithSwords.Pattern.Wheel };
            try
            {
                foreach (var pattern in patterns)
                {
                    var parts = new List<string>();
                    foreach (float dx in new[] { 2f, 6f, 12f })
                    {
                        ZenithSwords.DevForce = pattern;
                        hero.transform.position = Clear(hero, new Vector2(at.Value.x - dx, at.Value.y - bottom + 0.05f));
                        hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                        hero.FaceRight();
                        yield return new WaitForSeconds(0.8f);
                        int flights = ZenithSwords.DevFlights, lost = ZenithSwords.DevLostFlights, calls = ZenithSwords.DevHitCalls;
                        int hits = TargetDummy.DevHits, dmg = TargetDummy.DevDamage;
                        mod.DevAim = aim;
                        mod.DevHold = true;
                        float start = Time.time;
                        while (Time.time - start < 5f)
                        {
                            if (!mod.IsUsingItem && mod.DevReady) mod.DevUse(ItemType.Zenith, aim);
                            yield return null;
                        }
                        mod.DevHold = null;
                        mod.DevAim = null;
                        // The last swords home.
                        yield return new WaitForSeconds(2.5f);
                        float secs = Time.time - start;
                        int f = ZenithSwords.DevFlights - flights, l = ZenithSwords.DevLostFlights - lost, c = ZenithSwords.DevHitCalls - calls, h = TargetDummy.DevHits - hits;
                        lostAll += l;
                        missingAll += Mathf.Max(0, c - h);
                        parts.Add($"距离 {dx:F0}：{f} 次飞行，碰到却没打中 {l} 次，打出 {c} 下、假人记到 {h} 下，每秒 {(TargetDummy.DevDamage - dmg) / secs:F0}");
                    }
                    Plugin.Log.LogInfo($"[AutoTest] ZenithLoss {pattern}：{string.Join("；", parts)}");
                }
            }
            finally
            {
                ZenithSwords.DevForce = null;
                mod.DevHold = null;
                mod.DevAim = null;
                inv.Slots[0] = saved;
                inv.Selected = savedSel;
            }
            Plugin.Log.LogInfo($"[AutoTest] ZenithLoss 合计 碰到没打中 {lostAll} 次，打出没记到 {missingAll} 下：{(lostAll == 0 && missingAll == 0 ? "通过" : "不通过")}");
        }
    }
}
