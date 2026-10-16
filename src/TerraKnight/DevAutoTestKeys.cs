using System.Collections;
using HarmonyLib;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "keys"): Terraria 1.4.5's keys, pressed as the keyboard would. The Dash key with the Shield of Cthulhu
    /// (the way faced; held, no second dash; pressed again, one); Double Tap Dashing off; Quick Buff; Next Loadout; a key
    /// pressed again while listening unbinds it; the minimap's zoom keys; the keybinding list drawn.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator KeysTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 键位 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(3f);
            var inv = mod.Inventory;
            var hero = HeroController.instance;
            var saved = inv.Accessories[0];
            inv.Accessories[0] = ItemStack.Of(ItemType.ShieldOfCthulhu);
            yield return new WaitForSeconds(0.5f);
            Check("克苏鲁护盾的冲刺", TerraMovement.DashKind == 2, $"DashKind {TerraMovement.DashKind}");

            // The Dash key (bound to X for the test), facing right.
            TerraKeys.Set(TerraKeys.Action.Dash, Key.X);
            hero.FaceRight();
            yield return new WaitForSeconds(0.3f);
            int dashes = 0;
            bool was = false;
            float x0 = hero.transform.position.x;
            KeysDown(Key.X);
            float t0 = Time.time;
            while (Time.time - t0 < 2f)
            {
                yield return null;
                if (TerraMovement.IsDashing && !was) dashes++;
                was = TerraMovement.IsDashing;
            }
            float x1 = hero.transform.position.x;
            Check("按住冲刺键只冲一次、朝面朝方向", dashes == 1 && x1 > x0 + 1f, $"冲了 {dashes} 次，x {x0:F1}→{x1:F1}");
            KeysDown();
            yield return new WaitForSeconds(0.3f);
            hero.FaceLeft();
            yield return null;
            x0 = hero.transform.position.x;
            yield return Tap(Key.X, 0.2f);
            yield return new WaitForSeconds(0.5f);
            Check("松开再按又冲一次、朝左", hero.transform.position.x < x0 - 1f, $"x {x0:F1}→{hero.transform.position.x:F1}");

            // Double Tap Dashing off: tapping right twice doesn't dash.
            yield return new WaitForSeconds(1f);
            TerraKeys.DoubleTapDash = false;
            int before = 0;
            bool dashedTap = false;
            for (int k = 0; k < 2; k++) yield return Tap(TerraKeys.Get(TerraKeys.Action.Right), 0.05f);
            float tt = Time.time;
            while (Time.time - tt < 0.4f) { yield return null; if (TerraMovement.IsDashing) dashedTap = true; }
            Check("双击冲刺关掉后双击不冲", !dashedTap, $"{before}");
            TerraKeys.DoubleTapDash = true;
            yield return new WaitForSeconds(1f);
            dashedTap = false;
            for (int k = 0; k < 2; k++) yield return Tap(TerraKeys.Get(TerraKeys.Action.Right), 0.05f);
            tt = Time.time;
            while (Time.time - tt < 0.4f) { yield return null; if (TerraMovement.IsDashing) dashedTap = true; }
            Check("双击冲刺打开时双击能冲", dashedTap);
            inv.Accessories[0] = saved;

            // Quick Buff (B): two buff potions drunk.
            Player.PlayerBuffs.Clear();
            bool infiniteWas = TerraMod.DevAllInfinite;
            TerraMod.DevAllInfinite = false;
            inv.Infinite = false;
            inv.Slots[30] = ItemStack.Of(ItemType.IronskinPotion, 3);
            inv.Slots[31] = ItemStack.Of(ItemType.SwiftnessPotion, 2);
            yield return Tap(TerraKeys.Get(TerraKeys.Action.QuickBuff));
            var iron = ItemDb.Get(ItemType.IronskinPotion).BuffType;
            var swift = ItemDb.Get(ItemType.SwiftnessPotion).BuffType;
            Check("快速增益", Player.PlayerBuffs.Has(iron) && Player.PlayerBuffs.Has(swift) && inv.Slots[30].Count == 2 && inv.Slots[31].Count == 1, TerraMod.DevLastQuickUse);
            TerraMod.DevAllInfinite = infiniteWas;

            // Next Loadout (bound to Period).
            TerraKeys.Set(TerraKeys.Action.NextLoadout, Key.Period);
            int lo = inv.CurrentLoadout;
            yield return Tap(Key.Period);
            yield return new WaitForSeconds(0.3f);
            Check("下一配装", inv.CurrentLoadout == (lo + 1) % Inventory.LoadoutCount, $"{lo}→{inv.CurrentLoadout}");
            TerraKeys.Set(TerraKeys.Action.NextLoadout, Key.None);

            // Listening: the same key again unbinds.
            TerraKeys.Listen(TerraKeys.Action.QuickBuff);
            yield return null;
            yield return Tap(Key.B);
            Check("再按原键解除绑定", TerraKeys.Get(TerraKeys.Action.QuickBuff) == Key.None && TerraKeys.KeyName(Key.None).Length > 0, TerraKeys.KeyName(TerraKeys.Get(TerraKeys.Action.QuickBuff)));
            TerraKeys.Set(TerraKeys.Action.QuickBuff, Key.B);

            // The minimap's zoom keys.
            int styleWas = UI.WorldMap.Style;
            if (UI.WorldMap.StyleSetting != null) UI.WorldMap.StyleSetting.Value = 1;
            var scale = AccessTools.Field(typeof(UI.WorldMap), "miniScale");
            float s0 = (float)scale.GetValue(null);
            KeysDown(Key.NumpadPlus);
            yield return new WaitForSeconds(0.5f);
            KeysDown();
            float s1 = (float)scale.GetValue(null);
            Check("小地图放大键", s1 > s0 * 1.05f, $"{s0:F2}→{s1:F2}");
            if (UI.WorldMap.StyleSetting != null) UI.WorldMap.StyleSetting.Value = styleWas;

            // The list drawn.
            TerraKeys.Set(TerraKeys.Action.Dash, Key.None);
            UI.IngameOptions.Show();
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("Town", "keys_settings");
            yield return null;
            yield return null;
            UI.IngameOptions.Hide();
            Check("改键列表有 " + TerraKeys.Ordered.Length + " 项", TerraKeys.Ordered.Length == TerraKeys.All.Length, string.Join("、", System.Linq.Enumerable.Select(TerraKeys.Ordered, a => TerraKeys.Name(a))));
            Plugin.Log.LogInfo($"[AutoTest] 键位 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
