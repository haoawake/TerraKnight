using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "superkey", the user's 10-26 "超级冲刺再给一个键位"): the Hallownest Shield worn in Dirtmouth. The Super Dash
    /// on the Dash key: a tap dashes, a long press Super Dashes. On a key of its own: the Dash key dashes while still held
    /// (no wait), held long it is no Super Dash, and the Super Dash key held then let go Super Dashes with no dash.
    /// Lines "[AutoTest] 超冲键 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SuperKeyTest()
        {
            Player.TerraStats.DevImmortal = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 超冲键 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var inv = mod.Inventory;
            for (int k = 0; k < inv.Accessories.Length; k++) inv.Accessories[k] = default;
            inv.Accessories[0] = ItemStack.Of(ItemType.HallownestShield);
            inv.RefreshEffective();
            PlayerData.instance.hasSuperDash = true;
            int dashes0 = 0, supers0 = 0;
            IEnumerator InControl()
            {
                for (float t = 0f; t < 6f && (!GameHooks.PlayerHasControl() || hero.cState.superDashing || Player.TerraMovement.IsDashing); t += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(0.3f);
            }
            Vector3 start = hero.transform.position;
            IEnumerator Ready()
            {
                AbilityAccessories.DevDashKey = false;
                AbilityAccessories.DevSuperKey = false;
                // A Super Dash still flying across the open town: stopped, and back to the start.
                var sd = HeroFsm(hero, "Superdash");
                if (sd != null && sd.ActiveStateName != "Inactive")
                {
                    sd.SendEvent("FSM CANCEL");
                    if (hero.controlReqlinquished) hero.RegainControl();
                    hero.StartAnimationControl();
                }
                yield return new WaitForSeconds(0.3f);
                hero.transform.position = start;
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return InControl();
                yield return new WaitForSeconds(0.6f);
                AbilityAccessories.DevReset();
                dashes0 = Player.TerraMovement.DevDashes;
                supers0 = AbilityAccessories.DevSuperDashes;
            }

            // The same key.
            AbilityAccessories.DevSuperApart = false;
            yield return Ready();
            AbilityAccessories.DevDashKey = true;
            yield return null;
            yield return null;
            int whileHeld = Player.TerraMovement.DevDashes - dashes0;
            yield return new WaitForSeconds(0.1f);
            AbilityAccessories.DevDashKey = false;
            yield return new WaitForSeconds(0.5f);
            Check("同一个键：轻点在松开时冲刺", whileHeld == 0 && Player.TerraMovement.DevDashes > dashes0 && AbilityAccessories.DevSuperDashes == supers0,
                $"按住时冲刺 {whileHeld}，松开后冲刺 {Player.TerraMovement.DevDashes - dashes0}，超冲 {AbilityAccessories.DevSuperDashes - supers0}");
            yield return Ready();
            AbilityAccessories.DevDashKey = true;
            yield return new WaitForSeconds(1.3f);
            AbilityAccessories.DevDashKey = false;
            yield return new WaitForSeconds(0.8f);
            Check("同一个键：长按超级冲刺", AbilityAccessories.DevSuperDashes > supers0 && Player.TerraMovement.DevDashes == dashes0,
                $"超冲 {AbilityAccessories.DevSuperDashes - supers0}，冲刺 {Player.TerraMovement.DevDashes - dashes0}");

            // Keys apart.
            AbilityAccessories.DevSuperApart = true;
            yield return Ready();
            AbilityAccessories.DevDashKey = true;
            yield return null;
            string atPress = $"按下时 超冲忙 {AbilityAccessories.SuperBusy} 等长按 {AbilityAccessories.HoldsDashKey} 状态机 {HeroFsm(hero, "Superdash")?.ActiveStateName} 超冲中 {hero.cState.superDashing} 冲刺中 {Player.TerraMovement.IsDashing} 冲刺种类 {Player.TerraMovement.DashKind} 可控 {GameHooks.PlayerHasControl()}";
            yield return new WaitForSeconds(0.1f);
            whileHeld = Player.TerraMovement.DevDashes - dashes0;
            Plugin.Log.LogInfo("[AutoTest] 超冲键 " + atPress);
            yield return new WaitForSeconds(1.3f);
            AbilityAccessories.DevDashKey = false;
            yield return new WaitForSeconds(0.8f);
            Check("分开时：冲刺键按下立刻冲刺（还按着就冲了），长按也不超冲", whileHeld > 0 && AbilityAccessories.DevSuperDashes == supers0,
                $"按下两帧内冲刺 {whileHeld}，共冲刺 {Player.TerraMovement.DevDashes - dashes0}，超冲 {AbilityAccessories.DevSuperDashes - supers0}");
            yield return Ready();
            AbilityAccessories.DevSuperKey = true;
            yield return new WaitForSeconds(1.0f);
            string charge = HeroFsm(hero, "Superdash")?.ActiveStateName;
            AbilityAccessories.DevSuperKey = false;
            yield return new WaitForSeconds(0.8f);
            Check("分开时：超冲键按住蓄力、松开超级冲刺", AbilityAccessories.DevSuperDashes > supers0 && Player.TerraMovement.DevDashes == dashes0,
                $"松手时 {charge}，超冲 {AbilityAccessories.DevSuperDashes - supers0}，冲刺 {Player.TerraMovement.DevDashes - dashes0}");
            yield return Ready();
            AbilityAccessories.DevSuperKey = true;
            yield return new WaitForSeconds(0.4f);
            AbilityAccessories.DevDashKey = true;
            yield return new WaitForSeconds(0.1f);
            AbilityAccessories.DevDashKey = false;
            int dashCharging = Player.TerraMovement.DevDashes - dashes0;
            yield return new WaitForSeconds(0.7f);
            AbilityAccessories.DevSuperKey = false;
            yield return new WaitForSeconds(0.8f);
            Check("分开时：蓄力中按冲刺键不打断成普通冲刺，松开照样超冲", dashCharging == 0 && AbilityAccessories.DevSuperDashes > supers0, $"蓄力中冲刺 {dashCharging}，超冲 {AbilityAccessories.DevSuperDashes - supers0}");

            AbilityAccessories.DevDashKey = null;
            AbilityAccessories.DevSuperKey = null;
            AbilityAccessories.DevSuperApart = null;
            Plugin.Log.LogInfo($"[AutoTest] 超冲键 合计 {ok}/{all}");
        }
    }
}
