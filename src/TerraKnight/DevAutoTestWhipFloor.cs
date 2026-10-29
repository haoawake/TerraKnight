using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "whipfloor", the user's 10-27 "鞭子站在地上的时候还是会打到地上，跳起来打就能穿墙了，应该站着也能穿墙"): in
    /// Dirtmouth, three whips lashed standing on the ground at a spot ahead and below the floor, then from the air at the same
    /// spot: the rope goes into the ground (its lowest link well under his feet's line, Whips.DevLowest) standing as it does in
    /// the air. As before (Whips.DevFloor: the ground under his feet held it up), to compare. Lines "[AutoTest] 鞭子穿地 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator WhipFloorTest()
        {
            int ok = 0, all = 0;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(4f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            BladeArts.Set(true);
            Whips.DevCount = true;
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt ?? (Vector2)hero.transform.position;
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            var ground = new Vector2(at.x - 6f, at.y - bottom + 0.05f);
            try
            {
                foreach (var type in new[] { ItemType.LeatherWhip, ItemType.EelWhip, ItemType.MorningStar })
                {
                    var def = ItemDb.Get(type);
                    float seconds = Mathf.Max(1f, Mathf.Round(def.UseAnimation * 60f) * Equipment.WhipScale()) / 60f;
                    var lows = new float[2, 2];
                    for (int old = 0; old < 2; old++)
                        for (int air = 0; air < 2; air++)
                        {
                            Whips.DevFloor = old == 1;
                            float low = -999f;
                            for (int lash = 0; lash < 2; lash++)
                            {
                                hero.transform.position = Clear(hero, ground + (air == 1 ? Vector2.up * 2.5f : Vector2.zero));
                                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                                hero.FaceRight();
                                yield return new WaitForSeconds(0.25f);
                                while (mod.IsUsingItem || !mod.DevReady) yield return null;
                                // Ahead and a little under the floor he stands on (in the air: as far under his feet).
                                var feet = (Vector2)hero.transform.position + new Vector2(0f, bottom);
                                var aim = feet + new Vector2(2.5f, -1.2f);
                                Whips.DevLowest = -999f;
                                mod.DevAim = aim;
                                mod.DevUse(type, aim);
                                for (float t = 0f; t < seconds + 0.5f && (t < 0.1f || mod.IsUsingItem); t += Time.deltaTime)
                                {
                                    // Held where he was (in the air too): the lash's way only.
                                    if (air == 1) hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                                    yield return null;
                                }
                                if (lash == 0 && old == 0) Shot("Town", $"whipfloor_{type}_{(air == 1 ? "air" : "ground")}");
                                low = Mathf.Max(low, Whips.DevLowest);
                            }
                            lows[old, air] = low;
                            mod.DevAim = null;
                        }
                    // Standing, the rope now goes as far under his feet as in the air (within a third), and well under (not held at the floor).
                    bool pass = lows[0, 0] > 24f && lows[0, 0] >= lows[0, 1] * 0.66f;
                    all++;
                    if (pass) ok++;
                    Plugin.Log.LogInfo($"[AutoTest] 鞭子穿地 {def.Name}：鞭绳最低在脚下 现在 站着 {lows[0, 0]:F0} px、跳起 {lows[0, 1]:F0} px；以前 站着 {lows[1, 0]:F0} px、跳起 {lows[1, 1]:F0} px {(pass ? "通过" : "不通过")}");
                }
            }
            finally
            {
                Whips.DevFloor = false;
                Whips.DevCount = false;
                mod.DevAim = null;
                Player.TerraStats.DevImmortal = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 鞭子穿地 合计 {ok}/{all}");
        }
    }
}
