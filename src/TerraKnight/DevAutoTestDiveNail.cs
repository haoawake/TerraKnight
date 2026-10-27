using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "divenail", the user's 10-26 list): (1) with the Descending Dark rune worn, in the air: one Down press does
    /// nothing, two in quick succession dive; landed, two again within two seconds don't (the dark dive's cooldown), after them
    /// they do. (2) The Nail with its arts learned: it swings on the press itself; a full charge let go with up or down tapped
    /// a moment before is the Cyclone Slash, with neither the Great Slash. Lines "[AutoTest] 下砸骨钉 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DiveNailTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 下砸骨钉 {what} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            var inv = mod.Inventory;
            for (int a = 0; a < inv.Accessories.Length; a++) inv.Accessories[a] = default;
            inv.Accessories[0] = ItemStack.Of(ItemType.DescendingDarkRune);
            inv.RefreshEffective();
            yield return new WaitForSeconds(0.4f);
            var home = hero.transform.position;
            IEnumerator Up()
            {
                hero.transform.position = Clear(hero, (Vector2)home + Vector2.up * 6f);
                rb.velocity = Vector2.zero;
                yield return new WaitForSeconds(0.15f);
            }
            IEnumerator Land()
            {
                for (float t = Time.time + 4f; Time.time < t && !hero.cState.onGround;) yield return null;
                yield return new WaitForSeconds(0.2f);
            }
            // One press: nothing.
            yield return Up();
            int d0 = Player.TerraMovement.DevDownDashes;
            Player.TerraMovement.DevDownTap = true;
            yield return new WaitForSeconds(0.5f);
            Check(Player.TerraMovement.DevDownDashes == d0, $"空中按一下下键不下砸（{Player.TerraMovement.DevDownDashes - d0}）");
            // Two quick ones: the dive.
            yield return Up();
            d0 = Player.TerraMovement.DevDownDashes;
            Player.TerraMovement.DevDownTap = true;
            yield return new WaitForSeconds(0.1f);
            Player.TerraMovement.DevDownTap = true;
            yield return new WaitForSeconds(0.1f);
            Check(Player.TerraMovement.DevDownDashes == d0 + 1, $"空中连按两下下键下砸（{Player.TerraMovement.DevDownDashes - d0}）");
            yield return Land();
            // Again at once: the dark dive cools down.
            yield return Up();
            d0 = Player.TerraMovement.DevDownDashes;
            int held0 = Player.TerraMovement.DevDarkHeld;
            Player.TerraMovement.DevDownTap = true;
            yield return new WaitForSeconds(0.1f);
            Player.TerraMovement.DevDownTap = true;
            yield return new WaitForSeconds(0.1f);
            Check(Player.TerraMovement.DevDownDashes == d0 && Player.TerraMovement.DevDarkHeld > held0, $"黑砸 2 秒冷却内连按不下砸（下砸 {Player.TerraMovement.DevDownDashes - d0}，被冷却挡下 {Player.TerraMovement.DevDarkHeld - held0}）");
            yield return Land();
            yield return new WaitForSeconds(2.2f);
            yield return Up();
            d0 = Player.TerraMovement.DevDownDashes;
            Player.TerraMovement.DevDownTap = true;
            yield return new WaitForSeconds(0.1f);
            Player.TerraMovement.DevDownTap = true;
            yield return new WaitForSeconds(0.1f);
            Check(Player.TerraMovement.DevDownDashes == d0 + 1, $"冷却过后又能黑砸（{Player.TerraMovement.DevDownDashes - d0}）");
            yield return Land();
            for (int a = 0; a < inv.Accessories.Length; a++) inv.Accessories[a] = default;
            inv.RefreshEffective();

            // The Nail.
            var pd = PlayerData.instance;
            pd.hasNailArt = true;
            pd.hasCyclone = true;
            pd.hasDashSlash = true;
            inv.Slots[0] = ItemStack.Of(ItemType.KnightNail);
            inv.Selected = 0;
            yield return new WaitForSeconds(1f);
            int s0 = NailItem.DevSwings;
            NailItem.DevHold = true;
            NailItem.DevPress = true;
            yield return null;
            yield return null;
            Check(NailItem.DevSwings == s0 + 1, $"学了剑技后按下立刻出刀（{NailItem.DevSwings - s0}）");
            yield return new WaitForSeconds(0.3f);
            Check(NailItem.DevSwings == s0 + 1, $"按住不连砍（{NailItem.DevSwings - s0}）");
            NailItem.DevHold = null;
            yield return new WaitForSeconds(0.6f);
            PlayMakerFSM arts = null;
            foreach (var f in hero.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Nail Arts") arts = f;
            var timer = HarmonyLib.AccessTools.Field(typeof(HeroController), "nailChargeTimer");
            string Release(bool upDown)
            {
                NailArts.DevUpDown = upDown;
                return null;
            }
            foreach (bool upDown in new[] { true, false })
            {
                for (float t = Time.time + 3f; Time.time < t && arts.ActiveStateName != "Inactive";) yield return null;
                yield return new WaitForSeconds(0.3f);
                Release(upDown);
                yield return null;
                yield return null;
                NailArts.DevUpDown = false;
                timer.SetValue(hero, 5f);
                arts.SendEvent("BUTTON UP");
                string seen = arts.ActiveStateName;
                for (int k = 0; k < 40; k++)
                {
                    yield return null;
                    string s = arts.ActiveStateName;
                    if (s.StartsWith("Cyclone") || s.StartsWith("G Slash") || s == "Flash" || s == "Flash 2" || s == "Facing?") { seen = s; break; }
                    seen = s;
                }
                bool cyclone = seen.StartsWith("Cyclone") || seen == "Flash" || seen == "Activate Slash";
                Check(upDown ? cyclone : !cyclone, $"蓄满松开，{(upDown ? "刚按过上" : "没按上下")}：{seen}（缓冲改旋风 {NailArts.DevCycloneGrace} 次，{NailArts.DevGraceWhy}）");
                for (float t = Time.time + 4f; Time.time < t && arts.ActiveStateName != "Inactive";) yield return null;
                if (!GameHooks.PlayerHasControl()) { hero.RegainControl(); hero.StartAnimationControl(); }
            }
            Plugin.Log.LogInfo($"[AutoTest] 下砸骨钉 合计 {ok}/{all}");
        }
    }
}
