using System.Collections;
using GlobalEnums;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "creative"): the creative mode button and its panel, then the difficulty picker
    /// (shot in Boot, the creative game started at Expert). In that game: creative is on, the
    /// enemies' life and damage at each difficulty, a real hit at Normal and at Master, the save
    /// keeping mode and difficulty. Then back in the menu: the slot's label, and an ordinary new
    /// game asking its difficulty and starting at Master, which leaves the test slot as the other
    /// suites expect it.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CreativeTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            yield return new WaitForSeconds(0.6f);
            Plugin.Log.LogInfo($"[AutoTest] 创造模式 开局：创造 {mod.Creative}，无限物品 {mod.Inventory.Infinite}，难度 {GameDifficulty.NameOf(GameDifficulty.Current)}（应为专家），开始次数 {UI.CreativeMenu.DevStarted}");
            // Endless money: spending it leaves it as it was.
            int geo0 = PlayerData.instance.geo;
            hero.TakeGeo(12345);
            yield return null;
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 创造模式 钱：{geo0}（应为 {Items.Shop.CreativeGeo}），花掉 12345 后 {PlayerData.instance.geo}");
            Items.InventoryGui.Open = true;
            yield return new WaitForSeconds(0.4f);
            Shot(scene, "creative_4_inventory");
            yield return null;
            Items.InventoryGui.Open = false;

            // An ordinary save's items run out (the autotests' own setting aside).
            bool all = TerraMod.DevAllInfinite;
            TerraMod.DevAllInfinite = false;
            mod.ApplyCreative(false);
            bool normalInfinite = mod.Inventory.Infinite;
            mod.ApplyCreative(true);
            Plugin.Log.LogInfo($"[AutoTest] 创造模式 切换：非创造存档无限物品 {normalInfinite}（应为 False），创造存档 {mod.Inventory.Infinite}（应为 True）");
            TerraMod.DevAllInfinite = all;
            mod.ApplyCreative(true);

            // The enemies' life and damage at each difficulty.
            HealthManager crawler = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (!hm.isDead && hm.gameObject.activeInHierarchy && Bestiary.KeyOf(hm) == "Crawler") { crawler = hm; break; }
            var chosen = GameDifficulty.Current;
            var line = new System.Text.StringBuilder();
            for (int i = 1; i <= 4; i++)
            {
                GameDifficulty.Current = (DifficultyLevel)i;
                int life = crawler != null ? EnemyStats.LifeMax(EnemyStats.Of(crawler)) : -1;
                line.Append($"{GameDifficulty.NameOf(GameDifficulty.Current)}：小爬虫血 {life}，每面具伤害 {Areas.DamagePerMask(0)}/{Areas.DamagePerMask(5)}（山道/白宫），每点原版血 {Areas.HealthScale:F2}；");
            }
            Plugin.Log.LogInfo($"[AutoTest] 难度 数值（应为 1/4、1/2、3/4、全额；大师小爬虫 150）：{line}");

            // A real hit from the crawlid at Normal and at Master.
            if (crawler != null)
            {
                foreach (var d in new[] { DifficultyLevel.Normal, DifficultyLevel.Master })
                {
                    GameDifficulty.Current = d;
                    mod.Stats.Life = mod.Stats.LifeMax;
                    int before = mod.Stats.Life;
                    hero.TakeDamage(crawler.gameObject, CollisionSide.left, 1, 1);
                    yield return new WaitForSeconds(0.2f);
                    Plugin.Log.LogInfo($"[AutoTest] 难度 挨打：{GameDifficulty.NameOf(d)} 被小爬虫碰一下掉血 {before - mod.Stats.Life}（防御 {mod.Stats.Defense}）");
                    yield return new WaitForSeconds(2.2f);
                }
                mod.Stats.Life = mod.Stats.LifeMax;
            }
            GameDifficulty.Current = chosen;

            // The save keeps both.
            Save.ModSave.Write(slot, mod);
            var mode = Save.ModSave.ModeOf(slot);
            Plugin.Log.LogInfo($"[AutoTest] 创造模式 存档：有 mod 存档 {mode.Known}，创造 {mode.Creative}，难度 {GameDifficulty.NameOf(mode.Difficulty)}");

            // Back in the menu, the slot says so.
            var gm = GameManager.instance;
            gm.StartCoroutine(gm.ReturnToMainMenu(GameManager.ReturnToMainMenuSaveModes.SaveAndContinueOnFail));
            // The menu has a GameManager of its own (the game's is gone with Quit_To_Menu).
            float wait = Time.realtimeSinceStartup + 60f;
            while (((gm = GameManager.UnsafeInstance) == null || gm.sceneName != "Menu_Title" || gm.gameState != GameState.MAIN_MENU || UIManager.instance == null) && Time.realtimeSinceStartup < wait) yield return null;
            yield return new WaitForSeconds(3f);
            gm = GameManager.instance;
            UIManager.instance.UIGoToProfileMenu();
            yield return new WaitForSeconds(3f);
            Shot("Menu_Title", "creative_5_label");
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 创造模式 回到存档界面：存档 {slot} 的标签“{SlotLabel(slot)}”");

            // An ordinary new game asks its difficulty, and starts at the one chosen (Master: the other suites' numbers).
            int asked = UI.CreativeMenu.DevAsked;
            gm.profileID = slot;
            UIManager.instance.StartNewGame();
            yield return new WaitForSeconds(0.6f);
            Shot("Menu_Title", "creative_6_difficulty");
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 难度 普通新游戏：问难度 {UI.CreativeMenu.DevAsked - asked} 次，面板 {UI.CreativeMenu.DevStep}");
            // "返回": the menu under it takes clicks again only after the button is up (its release would hit a save slot).
            bool blockedOpen = UI.CreativeMenu.DevMenuBlocked;
            UI.CreativeMenu.DevBack();
            bool blockedAtBack = UI.CreativeMenu.DevMenuBlocked;
            for (int f = 0; f < 5; f++) yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 难度 返回：面板开着时菜单挡住 {blockedOpen}（应为 True），点返回那一刻仍挡住 {blockedAtBack}（应为 True），松开几帧后 {UI.CreativeMenu.DevMenuBlocked}（应为 False），面板 {UI.CreativeMenu.DevStep}");
            UIManager.instance.StartNewGame();
            yield return new WaitForSeconds(0.6f);
            UI.CreativeMenu.DevPick(DifficultyLevel.Master);
            wait = Time.realtimeSinceStartup + 90f;
            while (((gm = GameManager.UnsafeInstance) == null || gm.sceneName != "Tutorial_01" || !GameHooks.PlayerHasControl()) && Time.realtimeSinceStartup < wait) yield return null;
            gm = GameManager.instance;
            yield return new WaitForSeconds(1f);
            Save.ModSave.Write(slot, mod);
            mode = Save.ModSave.ModeOf(slot);
            Plugin.Log.LogInfo($"[AutoTest] 难度 普通新游戏进场：场景 {gm.sceneName}，创造 {mod.Creative}（应为 False），难度 {GameDifficulty.NameOf(GameDifficulty.Current)}；存档 创造 {mode.Creative} 难度 {GameDifficulty.NameOf(mode.Difficulty)}");
            Shot("Tutorial_01", "creative_7_ordinary");
            yield return null;
            mod.DevNoFallDamage = false;
        }

        static string SlotLabel(int slot)
        {
            var ui = UIManager.instance;
            var b = slot switch { 1 => ui.slotOne, 2 => ui.slotTwo, 3 => ui.slotThree, _ => ui.slotFour };
            return b != null && b.locationText != null ? b.locationText.text : "?";
        }
    }
}
