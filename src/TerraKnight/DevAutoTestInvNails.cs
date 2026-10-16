using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TerraKnight.Items;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "invnails"): the Nail and the Dream Nail used with the inventory closed and open (held, nothing on the
    /// cursor): the Nail swings, the Dream Nail's FSM charges and slashes; with an item on the cursor the Nail doesn't. The
    /// hero's place on the screen is no interface.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator InvNailsTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 背包开着用骨钉 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            pd.hasDreamNail = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(2f);
            foreach (var hm in World.EnemyRespawn.Instance.DevLive("Crossroads_01")) Kill(hm);
            var inv = mod.Inventory;
            var hero = HeroController.instance;
            int nail = SlotOf(ItemType.KnightNail), dream = SlotOf(ItemType.DreamNail);
            Check("两把钉子都在", nail >= 0 && dream >= 0, $"骨钉第 {nail} 格，梦之钉第 {dream} 格");
            if (nail < 0 || dream < 0) yield break;
            if (nail != 0) { (inv.Slots[0], inv.Slots[nail]) = (inv.Slots[nail], inv.Slots[0]); if (dream == 0) dream = nail; nail = 0; }
            if (dream != 1) { (inv.Slots[1], inv.Slots[dream]) = (inv.Slots[dream], inv.Slots[1]); dream = 1; }
            PlayMakerFSM fsm = null;
            foreach (var f in hero.GetComponentsInChildren<PlayMakerFSM>(true)) if (f.FsmName == "Dream Nail") fsm = f;

            foreach (bool open in new[] { false, true })
            {
                string label = open ? "背包开着" : "背包关着";
                InventoryGui.Open = open;
                inv.Selected = nail;
                yield return new WaitForSeconds(0.6f);
                var cam = Camera.main;
                var sp = cam != null ? cam.WorldToScreenPoint(hero.transform.position) : Vector3.zero;
                bool overUi = InventoryGui.DevOverUi(new Vector2(sp.x, Screen.height - sp.y));
                Check(label + " 主角位置不是界面", !overUi, $"屏幕 {(Vector2)sp:F0}");
                int s0 = NailItem.DevSwings;
                NailItem.DevHold = true;
                yield return new WaitForSeconds(0.8f);
                NailItem.DevHold = null;
                Check(label + " 骨钉挥砍", NailItem.DevSwings > s0, $"挥了 {NailItem.DevSwings - s0} 下");
                yield return new WaitForSeconds(0.5f);

                inv.Selected = dream;
                yield return new WaitForSeconds(0.4f);
                var states = new List<string>();
                string last = null;
                DreamNailItem.DevHold = true;
                float t0 = Time.time;
                while (Time.time - t0 < 2.6f)
                {
                    if (Time.time - t0 > 1.6f) DreamNailItem.DevHold = false;
                    string now = fsm != null ? fsm.ActiveStateName : "?";
                    if (now != last) { states.Add(now); last = now; }
                    yield return null;
                }
                DreamNailItem.DevHold = null;
                Check(label + " 梦之钉蓄力挥砍", states.Contains("Charge") && states.Contains("Slash"), string.Join(" → ", states) + $"；拿着 {DreamNailItem.Holding()}，CanDreamNail {hero.CanDreamNail()}");
                yield return new WaitForSeconds(1f);
            }

            // An item on the cursor: that one is used, not the Nail.
            inv.Selected = nail;
            inv.Mouse = ItemStack.Of(ItemType.DirtBlock, 3);
            yield return new WaitForSeconds(0.3f);
            int c0 = NailItem.DevSwings;
            NailItem.DevHold = true;
            yield return new WaitForSeconds(0.6f);
            NailItem.DevHold = null;
            Check("光标上拿着东西时骨钉不挥", NailItem.DevSwings == c0, $"挥了 {NailItem.DevSwings - c0} 下");
            inv.Mouse = default;
            InventoryGui.Open = false;
            Plugin.Log.LogInfo($"[AutoTest] 背包开着用骨钉 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
