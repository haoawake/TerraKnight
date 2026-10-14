using System.Collections;
using System.IO;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Suite "slots": the fifty save slots. The profiles screen on its first, second and last page
    /// (the buttons' numbers, the last page's two slots), then a new game in the test slot from its
    /// page (a slot past four), saved: the game's user N.dat and the mod's TerraKnight_slotN.json.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SlotsBoot()
        {
            UIManager.instance.UIGoToProfileMenu();
            yield return new WaitForSeconds(3f);
            Shot("Menu_Title", "slots_1_page1");
            yield return null;
            yield return null;
            foreach (int page in new[] { 1, UI.SaveSlots.Pages - 1 })
            {
                UI.SaveSlots.DevSetPage(page);
                yield return new WaitForSeconds(3f);
                var ui = UIManager.instance;
                var sb = new System.Text.StringBuilder();
                foreach (var b in new[] { ui.slotOne, ui.slotTwo, ui.slotThree, ui.slotFour })
                    sb.Append($" {UI.SaveSlots.IndexOf(b)}:{b.saveFileState}/{(b.interactable ? "可点" : "不可点")}");
                Plugin.Log.LogInfo($"[AutoTest] 存档 第 {page + 1} 页：{sb}");
                Shot("Menu_Title", $"slots_{page + 1}_page");
                yield return null;
                yield return null;
            }
            UI.SaveSlots.DevSetPage((slot - 1) / UI.SaveSlots.PerPage);
            yield return new WaitForSeconds(3f);
            Shot("Menu_Title", "slots_back");
            yield return null;
            yield return null;
            var btn = new[] { UIManager.instance.slotOne, UIManager.instance.slotTwo, UIManager.instance.slotThree, UIManager.instance.slotFour }[(slot - 1) % UI.SaveSlots.PerPage];
            Plugin.Log.LogInfo($"[AutoTest] 存档 测试存档位 {slot} 在第 {UI.SaveSlots.Page + 1} 页的按钮 {UI.SaveSlots.IndexOf(btn)}（{btn.saveFileState}）");
        }

        IEnumerator SlotsTest(string scene)
        {
            var gm = GameManager.instance;
            Plugin.Log.LogInfo($"[AutoTest] 存档 进了游戏：profileID {gm.profileID}");
            bool done = false, ok = false;
            gm.SaveGame(r =>
            {
                done = true;
                ok = r;
            });
            for (float t = 0f; t < 10f && !done; t += Time.unscaledDeltaTime) yield return null;
            yield return new WaitForSeconds(0.5f);
            bool game = File.Exists(Path.Combine(TerraMod.SaveDir, $"user{slot}.dat"));
            bool modSave = File.Exists(Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{slot}.json"));
            Plugin.Log.LogInfo($"[AutoTest] 存档 {(gm.profileID == slot && ok && game && modSave ? "通过" : "失败")}：存档位 {slot} 保存 {ok}，user{slot}.dat {game}，TerraKnight_slot{slot}.json {modSave}");
        }
    }
}
