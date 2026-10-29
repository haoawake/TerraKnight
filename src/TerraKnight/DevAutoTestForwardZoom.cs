using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "forwardzoom", the user's 10-28 "侦查镜在泰拉瑞亚设置里面可以选择禁用吗？装在饰品里想要暴击率不想要视野"):
    /// the Recon Scope worn and a Handgun held, the view's push toward a point (TerraCamera.DevPan) under each Forward Zoom
    /// mode with the right button up and held (TerraCamera.DevRight); the scope's ranged crit the same in all; and a sword
    /// held (no gun the scope serves): no push even held. The setting is put back. Lines "[AutoTest] 向前缩放 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ForwardZoomTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var inv = mod.Inventory;
            var savedSlots = (ItemStack[])inv.Slots.Clone();
            int savedSel = inv.Selected;
            var savedMode = TerraCamera.Panning;
            int ok = 0, all = 0;
            void Check(bool pass, string what) { all++; if (pass) ok++; Plugin.Log.LogInfo($"[AutoTest] 向前缩放 {what} {(pass ? "通过" : "不通过")}"); }

            Wear(ItemType.ReconScope);
            inv.Slots[0] = ItemStack.Of(ItemType.Handgun);
            inv.Selected = 0;
            TerraCamera.DevPan = new Vector2(4f, 1f);
            // Pushed after `seconds` with the right button up or held.
            IEnumerator Pushed(TerraCamera.PanMode mode, bool held, System.Action<float> got)
            {
                TerraCamera.PanSetting.Value = mode;
                TerraCamera.DevRight = false;
                yield return new WaitForSeconds(0.6f);
                TerraCamera.DevRight = held;
                yield return new WaitForSeconds(0.6f);
                got(TerraCamera.Pan.magnitude);
            }
            var modes = new[] { TerraCamera.PanMode.Hold, TerraCamera.PanMode.Click, TerraCamera.PanMode.OnAlways, TerraCamera.PanMode.OffAlways };
            foreach (var mode in modes)
            {
                float up = 0f, held = 0f;
                yield return Pushed(mode, false, v => up = v);
                yield return Pushed(mode, true, v => held = v);
                float crit = Equipment.Current.RangedCrit;
                bool want = mode == TerraCamera.PanMode.OnAlways ? up > 2f && held > 2f
                    : mode == TerraCamera.PanMode.OffAlways ? up < 0.05f && held < 0.05f
                    : up < 0.05f && held > 2f;
                Check(want && crit >= 0.1f, $"{mode}：不按右键推开 {up:0.00}，按住/点了右键推开 {held:0.00}，远程暴击 +{crit * 100f:0}%");
            }

            // Click: a second press puts it back.
            TerraCamera.PanSetting.Value = TerraCamera.PanMode.Click;
            TerraCamera.DevRight = false;
            yield return new WaitForSeconds(0.3f);
            TerraCamera.DevRight = true;
            yield return null;
            yield return null;
            TerraCamera.DevRight = false;
            yield return new WaitForSeconds(0.6f);
            float once = TerraCamera.Pan.magnitude;
            TerraCamera.DevRight = true;
            yield return null;
            yield return null;
            TerraCamera.DevRight = false;
            yield return new WaitForSeconds(0.8f);
            float twice = TerraCamera.Pan.magnitude;
            Check(once > 2f && twice < 0.05f, $"点击切换：点一下推开 {once:0.00}，再点一下收回 {twice:0.00}");

            // A sword: the scope serves only guns firing bullets, candy corn, stakes or gel.
            TerraCamera.PanSetting.Value = TerraCamera.PanMode.Hold;
            inv.Slots[0] = ItemStack.Of(ItemType.IronBroadsword);
            TerraCamera.DevRight = true;
            yield return new WaitForSeconds(0.8f);
            Check(TerraCamera.Pan.magnitude < 0.05f, $"手持铁阔剑按住右键：推开 {TerraCamera.Pan.magnitude:0.00}");

            TerraCamera.DevRight = null;
            TerraCamera.DevPan = null;
            TerraCamera.PanSetting.Value = savedMode;
            // The options panel with the new row (its layout).
            UI.IngameOptions.DevPage(0);
            UI.IngameOptions.Show();
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("Town", "fz_options");
            yield return new WaitForSecondsRealtime(0.3f);
            UI.IngameOptions.Hide();
            for (int i = 0; i < inv.Slots.Length && i < savedSlots.Length; i++) inv.Slots[i] = savedSlots[i];
            inv.Selected = savedSel;
            Plugin.Log.LogInfo($"[AutoTest] 向前缩放 合计 {ok}/{all}");
        }
    }
}
