using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "nailarts"): the Nail item's nail arts with real key presses. In Dirtmouth, the three arts learned, the Nail
    /// in hand, Use Item put on K: K held 2.2 s (a swing, then the game's charge), let go; the game's attack action, its
    /// press, the charge timer, the Nail Arts FSM's states and the arts dealt logged; again with up held (the Cyclone).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator NailArtsTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 骨钉技艺 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            var pd = PlayerData.instance;
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            foreach (var f in new[] { "hasNailArt", "hasCyclone", "hasDashSlash", "hasUpwardSlash", "hasAllNailArts" }) pd.SetBool(f, true);
            pd.SetInt("nailSmithUpgrades", 4);
            inv.Slots[0] = ItemStack.Of(ItemType.KnightNail);
            inv.Selected = 0;
            inv.Override = -1;
            var useWas = Player.TerraKeys.Get(Player.TerraKeys.Action.UseItem);
            bool mouse = suite == "nailartsmouse";
            if (!mouse) Player.TerraKeys.Set(Player.TerraKeys.Action.UseItem, Key.K);
            TerraMod.DevNoRealMouse = false;
            yield return null;
            yield return null;
            var timer = AccessTools.Field(typeof(HeroController), "nailChargeTimer");
            var attack = GameHooks.Input.inputActions.attack;
            PlayMakerFSM arts = null;
            foreach (var f in hero.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Nail Arts") arts = f;
            string binds = string.Join(",", System.Linq.Enumerable.Select(attack.Bindings, b => b.Name));
            Check("拿着骨钉、学会技艺：Ready，原版攻击键打开并绑在 K 上", NailArts.Ready && attack.Enabled,
                $"Ready {NailArts.Ready}，手里 {inv.Current.Def?.Name}，attack.Enabled {attack.Enabled}，绑定 {binds}，Nail Arts FSM {(arts != null ? arts.ActiveStateName : "没有")}");

            foreach (var (label, up) in new[] { ("强力斩", false), ("旋风斩", true) })
            {
                int artsBefore = NailArts.DevArts, swingsBefore = NailItem.DevSwings;
                var states = new List<string>();
                float maxTimer = 0f;
                bool pressedSeen = false;
                float until = Time.time + 2.2f;
                while (Time.time < until)
                {
                    if (mouse) { MouseDown(UnityEngine.InputSystem.LowLevel.MouseButton.Left); KeysDown(up ? new[] { Key.W } : new Key[0]); }
                    else KeysDown(up ? new[] { Key.K, Key.W } : new[] { Key.K });
                    yield return null;
                    pressedSeen |= attack.IsPressed;
                    maxTimer = Mathf.Max(maxTimer, (float)timer.GetValue(hero));
                    if (arts != null && (states.Count == 0 || states[states.Count - 1] != arts.ActiveStateName)) states.Add(arts.ActiveStateName);
                }
                if (mouse) MouseDown();
                KeysDown(up ? new[] { Key.W } : new Key[0]);
                float after = Time.time + 1.6f;
                while (Time.time < after)
                {
                    yield return null;
                    if (Time.time > after - 1.2f) KeysDown();
                    if (arts != null && (states.Count == 0 || states[states.Count - 1] != arts.ActiveStateName)) states.Add(arts.ActiveStateName);
                }
                KeysDown();
                Check($"{label}：按住 K 蓄力、松开放出", pressedSeen && maxTimer >= 1.3f && states.Count > 2,
                    $"原版攻击键按下 {pressedSeen}，蓄力最多 {maxTimer:F2} 秒，挥砍 {NailItem.DevSwings - swingsBefore} 次，技艺命中 {NailArts.DevArts - artsBefore}，状态 {string.Join(" → ", states)}");
                yield return new WaitForSeconds(0.8f);
            }
            if (!mouse) Player.TerraKeys.Set(Player.TerraKeys.Action.UseItem, useWas);
            Plugin.Log.LogInfo($"[AutoTest] 骨钉技艺 结果 {ok}/{all}");
        }
    }
}
