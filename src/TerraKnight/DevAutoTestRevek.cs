using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "revek2"): Revek in the Spirits' Glade, woken as a ghost's dreamnailing wakes him,
    /// struck while he winds up and slashes, with a sword, a gun and a bow in turn: whether each
    /// disperses him (his Control FSM's "Hit") as the nail's parry does.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator RevekTest()
        {
            var pd = PlayerData.instance;
            pd.SetBool("hasDreamNail", true);
            pd.isInvincible = true;
            yield return GoTo("RestingGrounds_08", EntryGateOf("RestingGrounds_08"));
            yield return new WaitForSeconds(2f);
            PlayMakerFSM control = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.gameObject.name == "Ghost Battle Revek" && f.FsmName == "Control") control = f;
            if (control == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 瑞维克 找不到 不通过");
                yield break;
            }
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            inv.Infinite = true;
            foreach (var (weapon, ammo) in new[] { (ItemType.KnightNail, ItemType.None), (ItemType.IronBroadsword, ItemType.None), (ItemType.Minishark, ItemType.MusketBall), (ItemType.WoodenBow, ItemType.WoodenArrow) })
            {
                inv.Slots[0] = ItemStack.Of(weapon);
                if (ammo != ItemType.None) inv.Slots[Inventory.Size] = ItemStack.Of(ammo, 999);
                inv.Selected = 0;
                if (control.ActiveStateName == "Dormant") control.SendEvent("GHOST NPC KILLED");
                var seen = new System.Text.StringBuilder();
                string last = "";
                bool hit = false;
                int swings0 = NailItem.DevSwings;
                for (float until = Time.time + 25f; Time.time < until && !hit;)
                {
                    PlayerData.instance.isInvincible = true;
                    string st = control.ActiveStateName;
                    if (st != last) { seen.Append($" {st}"); last = st; }
                    if (st == "Hit" || st == "Damaged Pause") hit = true;
                    if (!(st == "Slash Antic" || st == "Slash Idle" || st == "Slash" || st == "L or R?")) NailItem.DevHold = null;
                    if (st == "Slash Antic" || st == "Slash Idle" || st == "Slash" || st == "L or R?")
                    {
                        Vector2 at = control.transform.position;
                        mod.DevAim = at;
                        // The nail's swing is the game's own: held, facing him.
                        if (weapon == ItemType.KnightNail)
                        {
                            NailItem.DevHold = true;
                            if ((at.x > hero.transform.position.x) != hero.cState.facingRight) { if (at.x > hero.transform.position.x) hero.FaceRight(); else hero.FaceLeft(); }
                        }
                        else if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(at);
                    }
                    yield return null;
                }
                mod.DevAim = null;
                NailItem.DevHold = null;
                Plugin.Log.LogInfo($"[AutoTest] 瑞维克 {ItemDb.Get(weapon).Name}：{(hit ? "打散了" : "没打散")}，骨钉挥了 {NailItem.DevSwings - swings0} 下，状态{seen} {(hit ? "通过" : "不通过")}");
                // His pause after a hit, then the next weapon.
                yield return new WaitForSeconds(5f);
            }
        }
    }
}
