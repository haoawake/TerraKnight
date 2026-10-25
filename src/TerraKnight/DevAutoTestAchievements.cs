using System.Collections;
using System.IO;
using TerraKnight.Achievements;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        /// <summary>
        /// Suite achievements: a journey achievement got from the game's own record within its once-a-second look (J01, the
        /// Mothwing Cloak), its popup shown, the file written; then the achievements window with some got, at all kinds,
        /// at the journey's, at the deaths' not got (pictures of each, the popup's too). [ACH] lines.
        /// </summary>
        IEnumerator AchievementsTest()
        {
            void Check(string what, bool ok, string detail = "") => Plugin.Log.LogInfo($"[ACH] {what}：{detail}{(ok ? " 通过" : " 不通过")}");
            var pd = PlayerData.instance;
            Achievements.Achievements.DevSetAll(false);
            yield return new WaitForSeconds(0.5f);
            Check("计数中（非创造存档、正在游戏）", Achievements.Achievements.Counting);
            Check("图标图集读得到", Achievements.Achievements.Atlas != null, Achievements.Achievements.Atlas != null ? $"{Achievements.Achievements.Atlas.width}x{Achievements.Achievements.Atlas.height}" : "无");
            int drawn = 0;
            foreach (var d in AchievementDefs.All) if (Achievements.Achievements.IconDrawn(d)) drawn++;
            Check("已画好的图标", drawn > 100, $"{drawn}/{AchievementDefs.All.Length}");

            bool had = pd.hasDash;
            pd.hasDash = true;
            int shown = Achievements.AchievementPopup.DevShown;
            float until = Time.unscaledTime + 3f;
            while (Time.unscaledTime < until && !Achievements.Achievements.Has("J01")) yield return null;
            Check("拿到蛾翼披风后达成 J01", Achievements.Achievements.Has("J01"));
            Check("弹出成就窗口", Achievements.AchievementPopup.DevShown > shown && Achievements.AchievementPopup.DevLast == "J01", Achievements.AchievementPopup.DevLast ?? "无");
            yield return new WaitForSeconds(0.7f);
            Shot("Ach", "popup");
            string file = Path.Combine(BepInEx.Paths.BepInExRootPath, "TerraKnight", "achievements.txt");
            Check("写进存档文件", File.Exists(file) && File.ReadAllText(file).Contains("J01"), file);

            foreach (var id in new[] { "J02", "J03", "J07", "J10", "J12", "J14", "J17", "J25", "B01", "B02", "B05", "B08", "C01", "G02", "G08", "D04", "D11", "X01" })
                Achievements.Achievements.DevSet(id, true);
            Achievements.AchievementPanel.DevShow(-1, 0);
            yield return new WaitForSecondsRealtime(0.6f);
            Check("成就面板打开", Achievements.AchievementPanel.Open && UI.IngameOptions.AnyOpen);
            Shot("Ach", "panel_all");
            Achievements.AchievementPanel.DevShow((int)Cat.Journey, 0);
            yield return new WaitForSecondsRealtime(0.4f);
            Shot("Ach", "panel_journey");
            Achievements.AchievementPanel.DevShow((int)Cat.Death, 2);
            yield return new WaitForSecondsRealtime(0.4f);
            Shot("Ach", "panel_death_notgot");
            Achievements.AchievementPanel.DevShow((int)Cat.Boss, 1);
            yield return new WaitForSecondsRealtime(0.4f);
            Shot("Ach", "panel_boss_got");
            Achievements.AchievementPanel.Hide();
            yield return new WaitForSecondsRealtime(0.3f);
            Check("面板关上", !Achievements.AchievementPanel.Open);
            pd.hasDash = had;
            Plugin.Log.LogInfo($"[ACH] 共达成 {Achievements.Achievements.GotCount} 个");
        }
    }
}
