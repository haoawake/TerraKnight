using System.Collections;
using System.IO;
using UnityEngine;
using TerraKnight.Items;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "quit"): the pause menu's quit to the main menu, then the slot loaded again from
    /// the save profiles screen, three times over in different places: the slot must still read
    /// (not "corrupted"), and the game and the mod's save must come back as they were left.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator QuitTest()
        {
            PlayerData.instance.isInvincible = true;
            string[] rooms = { null, "Town", "Crossroads_01" };
            for (int round = 1; round <= rooms.Length; round++)
            {
                if (rooms[round - 1] != null) yield return GoTo(rooms[round - 1], EntryGateOf(rooms[round - 1]));
                var gm = GameManager.instance;
                var pd = PlayerData.instance;
                mod.Inventory.Pickup(ItemType.Wood, 7 * round);
                pd.geo = 100 * round + 3;
                string before = State();
                string scene = gm.sceneName;
                Plugin.Log.LogInfo($"[AutoTest] 退出{round} 在 {scene} 退出到主菜单前：{before}");

                // As a player quits: Esc for the pause menu, then its "quit to menu" confirmed.
                float wait = Time.realtimeSinceStartup + 20f;
                while ((pd.disablePause || gm.gameState != GlobalEnums.GameState.PLAYING) && Time.realtimeSinceStartup < wait) yield return null;
                yield return Tap(UnityEngine.InputSystem.Key.Escape);
                wait = Time.realtimeSinceStartup + 5f;
                while (gm.gameState != GlobalEnums.GameState.PAUSED && Time.realtimeSinceStartup < wait) yield return null;
                yield return new WaitForSecondsRealtime(0.8f);
                Plugin.Log.LogInfo($"[AutoTest] 退出{round} 按 Esc 后：{gm.gameState}，禁止暂停={pd.disablePause}，界面 {UIManager.instance.uiState}/{UIManager.instance.menuState}");
                Shot(scene, $"quit_{round}_paused");
                yield return null;
                yield return null;
                UIManager.instance.UIReturnToMainMenu();
                wait = Time.realtimeSinceStartup + 60f;
                float note = Time.realtimeSinceStartup + 5f;
                while (Time.realtimeSinceStartup < wait && ((gm = GameManager.UnsafeInstance) == null || gm.sceneName != "Menu_Title" || UIManager.instance == null || UIManager.instance.menuState != GlobalEnums.MainMenuState.MAIN_MENU))
                {
                    if (Time.realtimeSinceStartup >= note)
                    {
                        note += 5f;
                        Plugin.Log.LogInfo($"[AutoTest] 退出{round} 等回主菜单：场景 {gm?.sceneName}，{gm?.gameState}，界面 {UIManager.instance?.uiState}/{UIManager.instance?.menuState}，存档文件：{Files()}");
                    }
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(4f);
                Plugin.Log.LogInfo($"[AutoTest] 退出{round} 到了 {gm?.sceneName}（菜单 {UIManager.instance?.menuState}），{Time.realtimeSinceStartup - (wait - 60f):F1} 秒");
                Plugin.Log.LogInfo($"[AutoTest] 退出{round} 存档文件：{Files()}");

                // The slot as the profiles screen reads it.
                SaveStats stats = null;
                bool read = false;
                gm.GetSaveStatsForSlot(slot, s => { stats = s; read = true; });
                wait = Time.realtimeSinceStartup + 10f;
                while (!read && Time.realtimeSinceStartup < wait) yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 退出{round} 存档 {slot} 读出：{(stats == null ? "读不出（损坏）" : $"geo {stats.geo} 面具 {stats.maxHealth} 区域 {stats.mapZone}")}");
                UIManager.instance.UIGoToProfileMenu();
                yield return new WaitForSecondsRealtime(3f);
                var ui = UIManager.instance;
                var sb = new System.Text.StringBuilder();
                foreach (var b in new[] { ui.slotOne, ui.slotTwo, ui.slotThree, ui.slotFour })
                    sb.Append($" {UI.SaveSlots.IndexOf(b)}:{b.saveFileState}");
                Plugin.Log.LogInfo($"[AutoTest] 退出{round} 存档界面（第 {UI.SaveSlots.Page + 1} 页）：{sb}");
                Shot("Menu_Title", $"quit_{round}_profiles");
                yield return null;
                yield return null;

                Platform.Current.LocalSharedData.SetInt("lastProfileIndex", slot);
                gm.LoadGameFromUI(slot);
                wait = Time.realtimeSinceStartup + 90f;
                while (Time.realtimeSinceStartup < wait && (HeroController.SilentInstance == null || GameManager.UnsafeInstance == null || GameManager.UnsafeInstance.gameState != GlobalEnums.GameState.PLAYING || !GameHooks.PlayerHasControl()))
                {
                    var g = GameManager.UnsafeInstance;
                    if (g != null && g.gameState == GlobalEnums.GameState.CUTSCENE && g.inputHandler != null && g.inputHandler.skipMode == GlobalEnums.SkipPromptMode.SKIP_PROMPT) g.SkipCutscene();
                    yield return null;
                }
                yield return new WaitForSeconds(2f);
                gm = GameManager.instance;
                string after = State();
                bool same = after == before;
                Plugin.Log.LogInfo($"[AutoTest] 退出{round} 读回存档：{gm.sceneName}，可操作={GameHooks.PlayerHasControl()}；{after}");
                Plugin.Log.LogInfo($"[AutoTest] 退出{round} {(stats != null && same ? "通过" : "不通过")}（存档{(stats != null ? "读得出" : "读不出")}，mod 内容{(same ? "一样" : "不一样")}）");
                Shot(gm.sceneName, $"quit_{round}_back");
                yield return null;
                PlayerData.instance.isInvincible = true;
            }
        }

        /// <summary>What a save should bring back: the bag, the geo and the mod's records.</summary>
        string State()
        {
            var inv = mod.Inventory;
            var sb = new System.Text.StringBuilder();
            int items = 0;
            foreach (var s in inv.Slots) if (!s.IsEmpty) { items++; sb.Append($"{s.Type}×{s.Count} "); }
            return $"geo {PlayerData.instance.geo}，背包 {items} 格：{sb}，生命上限 {mod.Stats.LifeMax}，改动场景 {System.Linq.Enumerable.Count(World.WorldEdits.All)} 个，图鉴 {Bestiary.Kills.Count} 种";
        }

        string Files()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var f in Directory.GetFiles(TerraMod.SaveDir))
            {
                string n = Path.GetFileName(f);
                if (n.StartsWith($"user{slot}") || n.StartsWith($"TerraKnight_slot{slot}"))
                    sb.Append($" {n}({new FileInfo(f).Length}B {File.GetLastWriteTime(f):HH:mm:ss})");
            }
            return sb.ToString();
        }
    }
}
