using System.Collections;
using System.IO;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "l10n"): the mod in other languages without touching the game's own setting
    /// (Lang.DevForce). Terraria's text read from Terraria.exe, item names and tooltips and buffs in
    /// English and Japanese, and the mod's own lines still without a translation written out
    /// (dev/l10n_untranslated.txt) for the table.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator L10nTest(HeroController hero)
        {
            Plugin.Log.LogInfo($"[AutoTest] 语言 泰拉瑞亚文本：{Terraria.TerrariaText.DevReport("en-US")}；{Terraria.TerrariaText.DevReport("zh-Hans")}；mod 翻译表 {Lang.DevEntries} 条");
            var sample = new[] { ItemType.CopperShortsword, ItemType.WorkBench, ItemType.Minishark, ItemType.TerraBlade, ItemType.Zenith, ItemType.WoodPlatform, ItemType.Chest, ItemType.HermesBoots };
            foreach (var culture in new[] { "en-US", "ja-JP", "ru-RU" })
            {
                Lang.DevForce(culture);
                yield return null;
                var sb = new System.Text.StringBuilder();
                foreach (var t in sample)
                {
                    var d = ItemDb.Get(t);
                    sb.Append($"\n    {t}：{d.Name} | {(d.Tooltip ?? "").Replace("\n", " / ")}");
                }
                Plugin.Log.LogInfo($"[AutoTest] 语言 {culture} 物品：{sb}");
                InventoryGui.Open = true;
                yield return new WaitForSeconds(0.4f);
                Shot(GameManager.instance.sceneName, "l10n_" + culture);
                yield return null;
                InventoryGui.Open = false;
                Plugin.Log.LogInfo($"[AutoTest] 语言 {culture} 界面：{Lang.T("物品栏")}、{Lang.T("制作")}、{World.GameDifficulty.NameOf(World.DifficultyLevel.Normal)}、{Shop.Money(1234)}、{World.Areas.Display("遗忘十字路")}、{Lang.F("工作站：{0}（放在附近，或在长椅旁、德特茅斯）", Lang.T("工作台"))}；说明里还缺翻译的行 {ItemDb.DevMissingLines.Count} 条");
                Plugin.Log.LogInfo($"[AutoTest] 语言 {culture} Buff：{Player.PlayerBuffs.Name(Player.PlayerBuffs.PotionSickness)}（{Player.PlayerBuffs.Desc(Player.PlayerBuffs.PotionSickness)}），{Player.PlayerBuffs.Name(Player.PlayerBuffs.Honey)}，{Player.PlayerBuffs.Name(Player.PlayerBuffs.Werewolf)}");
            }
            Lang.DevForce("en-US");
            yield return null;
            var items = new System.Collections.Generic.List<string>(ItemDb.DevMissingLines);
            var buffs = Player.PlayerBuffs.DevUntranslated();
            string path = Path.Combine(outDir, "l10n_untranslated.txt");
            using (var w = new StreamWriter(path, false, new System.Text.UTF8Encoding(false)))
            {
                w.WriteLine("### items");
                foreach (var s in items) w.WriteLine(s.Replace("\n", "\\n"));
                w.WriteLine("### buffs");
                foreach (var s in buffs) w.WriteLine(s.Replace("\n", "\\n"));
            }
            Plugin.Log.LogInfo($"[AutoTest] 语言 还没翻译：物品 {items.Count} 条，Buff {buffs.Count} 条，写在 {path}");
            Lang.DevForce(null);
            yield return null;
        }
    }
}
