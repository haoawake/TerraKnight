using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "shimmertip", the user's 10-27 "能够微光嬗变的东西鼠标悬浮上去的时候显示“可在微光中嬗变为[贴图]”"): at stage 3,
    /// the bag open, items the shimmer transmutes (three of them, one that waits for stage 5) and one it doesn't, each
    /// hovered: the line "可在微光中嬗变为 [picture] name" (the picture drawn: InventoryGui.DevTipIcons) for the first,
    /// the stage-5 note on the late one, no line on the last. How many items carry the line. Shots Town_shimmertip_*.
    /// Lines "[AutoTest] 微光提示 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ShimmerTipTest()
        {
            int ok = 0, all = 0;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(3f);
            PlayerData.instance.isInvincible = true;
            int stage0 = Shop.DevStage;
            Shop.DevStage = 3;
            var picks = new List<ItemType>();
            ItemType late = ItemType.None;
            int carry = 0;
            foreach (var d in ItemDb.All)
            {
                if (d == null || ItemDb.Get(d.Type) != d) continue;
                var to = Shimmer.TransformOf(d.Type, out bool l);
                if (to == ItemType.None || ItemDb.Get(to) == null) continue;
                carry++;
                if (l && late == ItemType.None) late = d.Type;
                else if (!l && picks.Count < 3) picks.Add(d.Type);
            }
            if (late != ItemType.None) picks.Add(late);
            picks.Add(ItemType.IronBroadsword);
            var inv = mod.Inventory;
            var kept = new List<ItemStack>();
            for (int i = 0; i < picks.Count; i++) { kept.Add(inv.Slots[10 + i]); inv.Slots[10 + i] = ItemStack.Of(picks[i]); }
            try
            {
                InventoryGui.Open = true;
                // The interface answers to the test's cursor as to the mouse.
                UI.TerraCursor.DevDrives = true;
                yield return new WaitForSeconds(0.6f);
                for (int i = 0; i < picks.Count; i++)
                {
                    var r = InventoryGui.DevHitRect("Slot", 10 + i);
                    if (r == null) { all++; Plugin.Log.LogInfo($"[AutoTest] 微光提示 第 {10 + i} 格没画出来 不通过"); continue; }
                    UI.TerraCursor.DevAt = r.Value.center;
                    yield return new WaitForSeconds(0.4f);
                    string tip = InventoryGui.DevTooltip;
                    int icons = InventoryGui.DevTipIcons;
                    var to = Shimmer.TransformOf(picks[i], out bool l);
                    bool has = tip.Contains("可在微光中嬗变为") || tip.Contains("Transmutes into");
                    bool pass = to == ItemType.None ? !has : has && icons > 0 && tip.Contains(ItemDb.Get(to).Name) && (!l || tip.Contains("阶段 5"));
                    all++;
                    if (pass) ok++;
                    Plugin.Log.LogInfo($"[AutoTest] 微光提示 {ItemDb.Get(picks[i]).Name}{(l ? "（阶段 5 才嬗变）" : "")}：{(to == ItemType.None ? "不能嬗变" : "→ " + ItemDb.Get(to).Name)}，提示 {tip.Replace("\n", " | ")}，图标 {icons} {(pass ? "通过" : "不通过")}");
                    Shot("Town", $"shimmertip_{i}");
                    yield return new WaitForSeconds(0.2f);
                }
            }
            finally
            {
                UI.TerraCursor.DevAt = null;
                UI.TerraCursor.DevDrives = false;
                InventoryGui.Open = false;
                for (int i = 0; i < kept.Count; i++) inv.Slots[10 + i] = kept[i];
                Shop.DevStage = stage0;
            }
            Plugin.Log.LogInfo($"[AutoTest] 微光提示 能嬗变、提示里有这一行的物品 {carry} 种；合计 {ok}/{all}");
        }
    }
}
