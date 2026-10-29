using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "zenithrain", the user's 10-27 "天顶剑左手的攻击从天而降的剑有时候插不准，应该严格插在鼠标位置"): in Dirtmouth by
    /// its Target Dummy, the Zenith's rain thrown at a spot beside the dummy, three times standing and three times walking
    /// away while the swords fall; where each sword's point strikes against the cursor's spot, as remade and as before
    /// (ZenithSwords.DevOldRain). Lines "[AutoTest] 剑雨 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ZenithRainTest()
        {
            int ok = 0, all = 0;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(4f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            BladeArts.Set(true);
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt;
            if (!at.HasValue) { Plugin.Log.LogInfo("[AutoTest] 剑雨 没有训练假人 不通过"); yield break; }
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            var inv = mod.Inventory;
            inv.Slots[0] = ItemStack.Of(ItemType.Zenith);
            inv.Selected = 0;
            ZenithSwords.DevForce = ZenithSwords.Pattern.Rain;
            try
            {
                foreach (bool old in new[] { true, false })
                    foreach (bool walk in new[] { false, true })
                    {
                        ZenithSwords.DevOldRain = old;
                        float worst = 0f, sum = 0f;
                        int n = 0;
                        for (int round = 0; round < 3; round++)
                        {
                            var stand = new Vector2(at.Value.x - 7f, at.Value.y - bottom + 0.05f);
                            hero.transform.position = Clear(hero, stand);
                            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                            yield return new WaitForSeconds(0.4f);
                            // The spot: on the ground a little short of the dummy, a different one each round.
                            var spot = new Vector2(at.Value.x - 2.5f + round * 0.8f, at.Value.y + 0.3f);
                            ZenithSwords.DevRainTips.Clear();
                            for (float t = Time.time + 2f; !mod.DevReady && Time.time < t;) yield return null;
                            // The cursor held on the spot through the use (its three swords are thrown over a few ticks).
                            mod.DevAim = spot;
                            mod.DevUse(ItemType.Zenith, spot);
                            // Walking away as the swords fall: 6 units to the left over the flight.
                            for (float t = 0f; t < 2.2f && ZenithSwords.DevRainTips.Count < 3; t += Time.deltaTime)
                            {
                                if (walk && t < 1.2f) hero.transform.position += new Vector3(-5f * Time.deltaTime, 0f);
                                yield return null;
                            }
                            mod.DevAim = null;
                            foreach (var tip in ZenithSwords.DevRainTips)
                            {
                                float d = Vector2.Distance(tip, spot);
                                worst = Mathf.Max(worst, d);
                                sum += d;
                                n++;
                            }
                            Plugin.Log.LogInfo($"[AutoTest] 剑雨 {(old ? "以前" : "现在")}{(walk ? " 边扔边走" : " 站着")} 第 {round + 1} 次：光标 {spot:F2}，剑尖 {string.Join(" ", ZenithSwords.DevRainTips.Select(p => p.ToString("F2")))}");
                            yield return new WaitForSeconds(0.8f);
                        }
                        bool pass = old || n >= 9 && worst < 0.2f;
                        if (!old) { all++; if (pass) ok++; }
                        Plugin.Log.LogInfo($"[AutoTest] 剑雨 {(old ? "以前" : "现在")}{(walk ? " 边扔边走" : " 站着")}：{n} 把，剑尖离光标平均 {(n > 0 ? sum / n : 0f):F2}、最远 {worst:F2} 格（1 格 = 1 单位）{(old ? "" : pass ? " 通过" : " 不通过")}");
                    }
            }
            finally
            {
                ZenithSwords.DevForce = null;
                ZenithSwords.DevOldRain = false;
                mod.DevAim = null;
            }
            Shot("Town", "zenithrain");
            Plugin.Log.LogInfo($"[AutoTest] 剑雨 合计 {ok}/{all}");
        }
    }
}
