using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator ProgressionNoticeTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            yield return GoTo("Town", "left1");
            int savedStage = Shop.DevStage;
            int checks = 0, failures = 0;
            var messages = new List<string>();
            void Check(string label, bool pass)
            {
                checks++;
                if (!pass) { failures++; Plugin.Log.LogError($"[AutoTest] 阶段提示 {label} 不通过"); }
            }
            try
            {
                ProgressionNotice.Reset();
                Shop.DevStage = 0;
                int baseline = ProgressionNotice.DevNotes;
                ProgressionNotice.Tick(true);
                Check("初次载入不提示", ProgressionNotice.DevNotes == baseline);
                for (int stage = 1; stage <= 5; stage++)
                {
                    Shop.DevStage = stage;
                    // Let TerraMod.Update detect the change, using the actual gameplay hook.
                    yield return new WaitForSecondsRealtime(.25f);
                    string expected = Lang.F("圣巢探索进入新阶段：{0} / {1}。", stage, World.Areas.MaxLevel);
                    Check("实际更新发送阶段" + stage, ProgressionNotice.DevNotes == baseline + stage && Net.NetChat.DevHas(expected));
                    messages.Add(expected);
                    int count = ProgressionNotice.DevNotes;
                    yield return new WaitForSecondsRealtime(.15f);
                    Check("相同阶段不重复" + stage, ProgressionNotice.DevNotes == count);
                }
                int before = ProgressionNotice.DevNotes;
                Shop.DevStage = 2;
                ProgressionNotice.Tick(false);
                Check("非游玩期间不发送", ProgressionNotice.DevNotes == before);
                ProgressionNotice.Tick(true);
                Check("向下切换也提示", ProgressionNotice.DevNotes == before + 1);
                before = ProgressionNotice.DevNotes;
                ProgressionNotice.Reset();
                Shop.DevStage = 4;
                ProgressionNotice.Tick(true);
                Check("读档只重建基线", ProgressionNotice.DevNotes == before);
                ProgressionNotice.Tick(false);
                ProgressionNotice.Tick(true);
                Check("场景切换不重复", ProgressionNotice.DevNotes == before);
                // A book preview is independent of the player's actual stage.
                InventoryGui.DevBestiary(true);
                InventoryGui.DevBestiarySelect(-12);
                ProgressionNotice.Tick(true);
                Check("图鉴预览不触发", ProgressionNotice.DevNotes == before && Shop.UnlockedStage() == 4);
                Shot("Town", "progression_notice");
                yield return new WaitForSecondsRealtime(.3f);
                File.WriteAllText(Path.Combine(DevCopy.SaveDir, "progression-notice-result.json"),
                    JsonConvert.SerializeObject(new { Checks = checks, Failures = failures, Messages = messages }, Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] 阶段提示检查 {checks}，失败 {failures}");
            }
            finally
            {
                Shop.DevStage = savedStage;
                ProgressionNotice.Reset();
                InventoryGui.DevBestiary(false);
            }
        }
    }
}
