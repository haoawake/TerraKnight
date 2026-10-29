using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "meleetips", Items.BladeTips): a remade weapon's tooltip gets one line, its right-click art, and none
    /// without one (the user's 10-26 "只留右键说明，没有的就直接不加"), every line in the language table for English; each weapon's whole tooltip
    /// written to the log for a read. Lines "[AutoTest] 武器描述 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MeleeTipsTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 武器描述 {what} {(pass ? "通过" : "不通过")}");
            }
            yield return new WaitForSeconds(0.5f);
            BladeArts.Set(true);
            var weapons = ItemDb.All.Where(d => d != null && BladeArts.Is(d)).OrderBy(d => d.Stage).ThenBy(d => d.Rare).ToList();
            int short_ = 0, rights = 0;
            foreach (var d in weapons)
            {
                var lines = BladeTips.Lines(d);
                bool right = BladeTips.RightClick(d) != null;
                if (right) rights++;
                if (lines.Count != (right ? 1 : 0)) short_++;
                Plugin.Log.LogInfo($"[AutoTest] 武器描述 {d.Type} {d.Name}｜{(d.Tooltip ?? "").Replace("\n", " / ")}｜{string.Join(" / ", lines)}");
            }
            Check(weapons.Count >= 90 && short_ == 0, $"{weapons.Count} 把都有统一的说明（缺行的 {short_} 把），{rights} 把有右键技");
            var missing = BladeTips.AllChinese().Distinct().Where(zh => Lang.Lookup(zh, "en-US") == null).ToList();
            Check(missing.Count == 0, $"说明的每一句都有英文（缺 {missing.Count}：{string.Join("；", missing.Take(5))}）");
            Plugin.Log.LogInfo($"[AutoTest] 武器描述 合计 {ok}/{all}");
        }
    }
}
