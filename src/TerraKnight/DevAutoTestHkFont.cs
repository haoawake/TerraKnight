using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "hkfont", the user's 10-26 "一段写给古代生灵的训*最后一个字变成方框了"): the mod's own lines in Hollow Knight's
    /// dialogue (UI.TerrarianDialogue) are drawn in the game's TextMeshPro fonts, which hold only the characters its own text
    /// uses. Every Chinese font asset loaded (with its fallbacks) checked against every character of those lines: the
    /// characters none of them has, and the lines they are in, written to hkfont-missing.txt in the run's save folder.
    /// Lines "[AutoTest] 字库 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HkFontTest()
        {
            yield return new WaitForSeconds(2f);
            // The game's own TextMeshPro (TMProOld, static atlases): the Chinese fonts its text objects switch to.
            var cjk = new List<TMProOld.TMP_FontAsset>();
            foreach (var c in Resources.FindObjectsOfTypeAll<ChangeFontByLanguage>())
                if (c.fontZH != null && !cjk.Contains(c.fontZH)) cjk.Add(c.fontZH);
            foreach (var f in Resources.FindObjectsOfTypeAll<TMProOld.TMP_FontAsset>())
            {
                int n = f.characterDictionary != null ? f.characterDictionary.Keys.Count(k => k >= 0x4E00 && k <= 0x9FFF) : 0;
                Plugin.Log.LogInfo($"[AutoTest] 字库 {f.name}：{f.characterDictionary?.Count ?? 0} 字，汉字 {n}，后备 {f.fallbackFontAssets?.Count ?? 0}{(cjk.Contains(f) ? "，中文用" : "")}");
            }
            bool Has(TMProOld.TMP_FontAsset f, char ch) => f.HasCharacter(ch, true);
            // The Chinese body font's characters, for choosing words it can draw.
            foreach (var f in cjk)
                if (f.characterDictionary != null)
                    File.WriteAllText(Path.Combine(TerraMod.SaveDir, $"hkfont-chars-{f.name}.txt"),
                        new string(f.characterDictionary.Keys.Where(k => k < 0x10000).OrderBy(k => k).Select(k => (char)k).ToArray()), new System.Text.UTF8Encoding(false));
            var missing = new SortedDictionary<char, List<string>>();
            var sb = new System.Text.StringBuilder();
            foreach (var kv in UI.TerrarianDialogue.All)
            {
                string zh = System.Text.RegularExpressions.Regex.Replace(kv.Value.Zh ?? "", "<[^>]*>", "");
                foreach (char ch in zh)
                {
                    if (ch < 0x80 || char.IsWhiteSpace(ch)) continue;
                    if (cjk.Count > 0 && cjk.All(f => Has(f, ch))) continue;
                    if (!missing.TryGetValue(ch, out var keys)) missing[ch] = keys = new List<string>();
                    if (!keys.Contains(kv.Key)) keys.Add(kv.Key);
                }
            }
            foreach (var kv in missing)
            {
                var lacking = cjk.Where(f => !Has(f, kv.Key)).Select(f => f.name);
                sb.AppendLine($"{kv.Key}\tU+{(int)kv.Key:X4}\t缺于 {string.Join(",", lacking)}\t{string.Join(" ", kv.Value)}");
            }
            File.WriteAllText(Path.Combine(TerraMod.SaveDir, "hkfont-missing.txt"), sb.ToString(), new System.Text.UTF8Encoding(false));
            Plugin.Log.LogInfo($"[AutoTest] 字库 汉字字库 {cjk.Count} 个（{string.Join(",", cjk.Select(f => f.name))}），台词 {UI.TerrarianDialogue.All.Count} 条，缺的字 {missing.Count} 个：{string.Join("", missing.Keys)}");
            Plugin.Log.LogInfo($"[AutoTest] 字库 {(missing.Count == 0 && cjk.Count > 0 ? "通过" : "不通过")}");
        }
    }
}
