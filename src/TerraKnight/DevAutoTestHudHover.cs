using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "hudhover", the user's 10-26 "光标在魔力星上方不显示魔力值"): the cursor put over the mana stars, then the
    /// hearts, then away: the numbers shown beside it (UI.ResourceHud). Lines "[AutoTest] 资源悬停 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HudHoverTest()
        {
            yield return new WaitForSeconds(1f);
            var stats = mod.Stats;
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 资源悬停 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            UI.ResourceHud.DevMouse = UI.ResourceHud.DevStarCenter;
            yield return null;
            yield return null;
            Check("魔力星上显示魔力", UI.ResourceHud.DevHover == stats.Mana + "/" + stats.ManaMax, $"{UI.ResourceHud.DevHover}（魔力 {stats.Mana}/{stats.ManaMax}）");
            UI.ResourceHud.DevMouse = UI.ResourceHud.DevHeartCenter;
            yield return null;
            yield return null;
            Check("生命心上显示生命", UI.ResourceHud.DevHover == stats.Life + "/" + stats.LifeMax, $"{UI.ResourceHud.DevHover}（生命 {stats.Life}/{stats.LifeMax}）");
            UI.ResourceHud.DevMouse = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            yield return null;
            yield return null;
            Check("离开后不显示", UI.ResourceHud.DevHover == null, UI.ResourceHud.DevHover ?? "无");
            UI.ResourceHud.DevMouse = null;
            Plugin.Log.LogInfo($"[AutoTest] 资源悬停 合计 {ok}/{all}");
        }
    }
}
