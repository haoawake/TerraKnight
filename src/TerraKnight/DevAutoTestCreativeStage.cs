using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "creativestage"): items free (a creative save's way), the stage first at 5 (none chosen), the picker drawn
    /// in the open inventory, each stage chosen read back by Shop.UnlockedStage, and the choice kept in the save's flags.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CreativeStageTest(string scene, HeroController hero)
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 创造阶段 {what} {(pass ? "通过" : "不通过")}");
            }
            bool wasFree = Shop.Free;
            mod.SetInfiniteItems(true);
            Shop.DevStage = -1;
            try
            {
                foreach (var f in new System.Collections.Generic.List<string>(mod.GrantedAbilities)) if (f.StartsWith("creative.stage.")) mod.GrantedAbilities.Remove(f);
                Check(Shop.UnlockedStage() == World.Areas.MaxLevel, $"没选过时是阶段 {Shop.UnlockedStage()}（应为 {World.Areas.MaxLevel}）");
                InventoryGui.Open = true;
                for (int k = 0; k < 4; k++) yield return null;
                int buttons = 0;
                for (int s = 0; s <= World.Areas.MaxLevel; s++) if (InventoryGui.DevRectOf("CreativeStage", s) != null) buttons++;
                Check(buttons == 6, $"背包里有阶段按钮 {buttons}/6");
                Shot(scene, "creative_stage_full");
                for (int k = 0; k < 4; k++) yield return null;
                bool each = true;
                foreach (int s in new[] { 0, 2, 4, 1 })
                {
                    CreativeStage.Set(s);
                    if (Shop.UnlockedStage() != s) each = false;
                }
                Check(each && mod.GrantedAbilities.Contains("creative.stage.1") && !mod.GrantedAbilities.Contains("creative.stage.4"),
                    $"切换阶段后按所选的算（最后 {Shop.UnlockedStage()}），存档里只记一个");
            }
            finally
            {
                InventoryGui.Open = false;
                if (!wasFree) mod.SetInfiniteItems(false);
            }
            Plugin.Log.LogInfo($"[AutoTest] 创造阶段 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
