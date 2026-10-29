using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "optscroll", the user's 10-29 "泰拉瑞亚设置用多人模式的时候左边的多人模式设置就超出屏幕了，改为滚动制"): 泰拉瑞亚设置
    /// with the multiplayer rows shown (UI.IngameOptions.DevShowNet) at 1280×720: the left column is longer than its window on
    /// the panel and scrolls; at the top the Disconnect row lies below the window, scrolled to the end it lies inside it and
    /// inside the screen. Pictures of both. Lines "[AutoTest] OptScroll …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator OptScrollTest()
        {
            int pass = 0, total = 0;
            void Check(string name, bool ok, string detail = "")
            { total++; if (ok) pass++; Plugin.Log.LogInfo($"[AutoTest] OptScroll {name}：{detail} {(ok ? "通过" : "不通过")}"); }
            yield return GoTo("Town", "left1"); yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            UI.IngameOptions.DevShowNet = true;
            // The panel pauses the game: real-time waits from here.
            try
            {
                UI.IngameOptions.Show();
                yield return new WaitForSecondsRealtime(0.6f);
                var clip = UI.IngameOptions.DevLeftClip;
                float content = UI.IngameOptions.DevLeftContent;
                var view = UI.GameView.Rect;
                Check("左栏比窗口长时可以滚动", content > clip.height, $"内容 {content:F0}，窗口 {clip.height:F0}（屏幕 {view.height:F0}）");
                Check("窗口在屏幕里", clip.yMax <= view.yMax + 1f, $"窗口底 {clip.yMax:F0}，屏幕底 {view.yMax:F0}");
                var row = UI.IngameOptions.DevDisconnectRect;
                Check("没滚动时断开连接在窗口外", row.yMax > clip.yMax, $"断开连接 {row.y:F0}–{row.yMax:F0}");
                Shot("Town", "optscroll_top"); yield return new WaitForSecondsRealtime(0.2f);
                UI.IngameOptions.DevLeftScroll = 1e6f;
                yield return new WaitForSecondsRealtime(0.3f);
                row = UI.IngameOptions.DevDisconnectRect;
                Check("滚到底断开连接在窗口里", row.y >= clip.y && row.yMax <= clip.yMax && row.yMax <= view.yMax,
                    $"滚动 {UI.IngameOptions.DevLeftScroll:F0}，断开连接 {row.y:F0}–{row.yMax:F0}，窗口 {clip.y:F0}–{clip.yMax:F0}");
                Shot("Town", "optscroll_end"); yield return new WaitForSecondsRealtime(0.2f);
            }
            finally
            {
                UI.IngameOptions.DevShowNet = false;
                UI.IngameOptions.DevLeftScroll = 0f;
                UI.IngameOptions.Hide();
            }
            Plugin.Log.LogInfo($"[AutoTest] OptScroll 合计 {pass}/{total}");
        }
    }
}
