using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "aimcharge", the user's 10-27 "蓄力的时候别什么东西都举起来，那些炮类枪类的就应该蓄力的时候炮口枪口朝前"): in
    /// Dirtmouth, the launchers' right-click charge (the KO Cannon, the Golem Fist) aimed level, up and down to the right and
    /// once to the left: held out at the cursor (Items.SwordFx.DevHeldGunDeg against the aim's way), the player facing it;
    /// let go, the art goes off. The Mace's charge stays raised (its set pose). Close-ups Town_aimcharge_*. Lines "[AutoTest] 端平蓄力 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator AimChargeTest()
        {
            int ok = 0, all = 0;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(4f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            BladeArts.Set(true);
            var inv = mod.Inventory;
            var stand = (Vector2)hero.transform.position;
            var aims = new[] { new Vector2(6f, 0f), new Vector2(5f, 4f), new Vector2(5f, -3f), new Vector2(-6f, 1.5f) };
            try
            {
                foreach (var type in new[] { ItemType.KOCannon, ItemType.GolemFist, ItemType.Mace })
                {
                    var def = ItemDb.Get(type);
                    inv.Slots[0] = ItemStack.Of(type);
                    inv.Selected = 0;
                    for (int i = 0; i < aims.Length; i++)
                    {
                        hero.transform.position = Clear(hero, stand);
                        hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                        yield return new WaitForSeconds(0.4f);
                        var shoulder = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center + new Vector2(0f, 0.3f);
                        var aim = shoulder + aims[i];
                        mod.DevAim = aim;
                        SwordFx.DevHeldGunDeg = float.NaN;
                        string last = BladeSkills.DevLastArt;
                        int uses = BladeSkills.DevUses;
                        BladeSkills.DevHeld = true;
                        BladeSkills.DevPress = true;
                        yield return new WaitForSeconds(0.7f);
                        bool aimed = BladeSkills.ChargeAimed;
                        float facing = hero.cState.facingRight ? 1f : -1f;
                        float want = Mathf.Atan2(aims[i].y, aims[i].x * facing) * Mathf.Rad2Deg;
                        float drawn = SwordFx.DevHeldGunDeg;
                        bool facesAim = Mathf.Sign(aims[i].x) == facing;
                        yield return CloseUpAt("Town", $"aimcharge_{type}_{i}", (Vector2)hero.transform.position + aims[i] * 0.35f, 110, 3);
                        BladeSkills.DevHeld = false;
                        yield return new WaitForSeconds(0.5f);
                        bool fired = BladeSkills.DevUses > uses;
                        BladeSkills.DevHeld = null;
                        mod.DevAim = null;
                        bool launcher = type != ItemType.Mace;
                        bool pass = launcher ? aimed && facesAim && !float.IsNaN(drawn) && Mathf.Abs(Mathf.DeltaAngle(drawn, want)) < 6f && fired
                                             : !aimed && fired;
                        all++;
                        if (pass) ok++;
                        Plugin.Log.LogInfo($"[AutoTest] 端平蓄力 {def.Name} 瞄 {aims[i]}：{(launcher ? $"端平 {aimed}，画出 {drawn:F0}°，瞄准 {want:F0}°，面向光标 {facesAim}" : $"蓄力姿势 {BladeSkills.ChargePose:F0}°（举起）")}，松开放出 {fired}（{BladeSkills.DevLastArt}）{(pass ? " 通过" : " 不通过")}");
                        yield return new WaitForSeconds(1.2f);
                        if (!launcher) break;
                    }
                }
            }
            finally
            {
                BladeSkills.DevHeld = null;
                mod.DevAim = null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 端平蓄力 合计 {ok}/{all}");
        }
    }
}
