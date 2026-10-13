using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using TeamCherry.Localization;

namespace TerraKnight
{
    /// <summary>
    /// The mod speaks the language Hollow Knight is set to, and Terraria's text follows it: its
    /// eleven languages (English, French, German, Simplified and Traditional Chinese, Spanish,
    /// Korean, Japanese, Italian, Portuguese, Russian) are all among Terraria's. Terraria's own items
    /// and buffs take Terraria's text in that language (Terraria.TerrariaText); the mod's own text is
    /// written in Simplified Chinese and looked up by it in the mod's table (Resources/lang.json,
    /// zh text -> culture -> text), the Chinese itself where a line has no entry yet. A change of
    /// language in the game's menu changes it all at once (<see cref="Changed"/>).
    /// </summary>
    internal static class Lang
    {
        /// <summary>The source language of the mod's text.</summary>
        public const string Source = "zh-Hans";

        public static string Culture { get; private set; } = Source;
        public static bool IsSource => Culture == Source;
        public static event Action Changed;

        static LanguageCode? last;
        static Dictionary<string, Dictionary<string, string>> table;

        static string forced;

        /// <summary>Dev: shows a language without touching the game's own setting (null: back to the game's).</summary>
        public static void DevForce(string culture)
        {
            forced = culture;
            last = null;
            if (culture == null) return;
            Culture = culture;
            Plugin.Log.LogInfo($"[语言] 测试用：强制 {culture}");
            Changed?.Invoke();
        }

        /// <summary>Each frame: the game's language, and a change of it passed on.</summary>
        public static void Tick()
        {
            if (forced != null) return;
            LanguageCode code;
            try { code = Language.CurrentLanguage(); }
            catch { return; }
            if (last == code) return;
            last = code;
            Culture = CultureOf(code);
            Plugin.Log.LogInfo($"[语言] 空洞骑士的语言 {code}，泰拉瑞亚和 mod 用 {Culture}");
            try { Changed?.Invoke(); }
            catch (Exception e) { Plugin.Log.LogError($"[语言] 换语言出错：{e}"); }
        }

        public static string CultureOf(LanguageCode code)
        {
            switch (code.ToString())
            {
                case "EN": return "en-US";
                case "FR": return "fr-FR";
                case "DE": return "de-DE";
                case "ZH": return "zh-Hans";
                case "ZH_TW": return "zh-Hant";
                case "ES": return "es-ES";
                case "KO": return "ko-KR";
                case "JA": return "ja-JP";
                case "IT": return "it-IT";
                case "PT": return "pt-BR";
                case "RU": return "ru-RU";
                default: return "en-US";
            }
        }

        static Dictionary<string, Dictionary<string, string>> Table()
        {
            if (table != null) return table;
            table = new Dictionary<string, Dictionary<string, string>>();
            try
            {
                using (var s = typeof(Lang).Assembly.GetManifestResourceStream("TerraKnight.lang.json"))
                {
                    if (s == null) return table;
                    using (var r = new StreamReader(s))
                    {
                        var root = JObject.Parse(r.ReadToEnd());
                        foreach (var line in root.Properties())
                        {
                            if (!(line.Value is JObject per)) continue;
                            var d = new Dictionary<string, string>();
                            foreach (var kv in per.Properties()) d[kv.Name] = (string)kv.Value;
                            table[line.Name] = d;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[语言] 翻译表读不了：{e.Message}");
            }
            return table;
        }

        /// <summary>The mod's own text (its Simplified Chinese) in the current language.</summary>
        public static string T(string zh)
        {
            if (string.IsNullOrEmpty(zh) || IsSource) return zh;
            return Lookup(zh, Culture) ?? Terraria.TerrariaText.FromChinese(zh, Culture) ?? zh;
        }

        static readonly System.Text.RegularExpressions.Regex number = new System.Text.RegularExpressions.Regex(@"\d+(?:\.\d+)?");

        /// <summary>
        /// One line of the mod's text: the table or Terraria's same line, else the line with its numbers
        /// as blanks ("持续 8 分钟" as "持续 {0} 分钟") looked up and filled; null when none has it.
        /// </summary>
        public static string Line(string zh, string culture)
        {
            if (string.IsNullOrEmpty(zh)) return zh;
            var s = Lookup(zh, culture) ?? Terraria.TerrariaText.FromChinese(zh, culture);
            if (s != null) return s;
            var nums = new List<string>();
            string pattern = number.Replace(zh, m => { nums.Add(m.Value); return "{" + (nums.Count - 1) + "}"; });
            if (nums.Count == 0) return null;
            var p = Lookup(pattern, culture);
            if (p == null) return null;
            try { return string.Format(p, nums.ToArray()); }
            catch (FormatException) { return null; }
        }

        /// <summary>A line with blanks ({0}, {1}...), in the current language, filled.</summary>
        public static string F(string zh, params object[] args)
        {
            string pattern = T(zh);
            try { return string.Format(pattern, args); }
            catch (FormatException) { return string.Format(zh, args); }
        }

        /// <summary>The table's entry for a line in a culture: the culture's own, else English for one it hasn't.</summary>
        public static string Lookup(string zh, string culture)
        {
            if (!Table().TryGetValue(zh, out var per)) return null;
            if (per.TryGetValue(culture, out var s) && !string.IsNullOrEmpty(s)) return Places(s);
            // Traditional Chinese from the Simplified when it has no line of its own is better than English.
            if (culture == "zh-Hant") return zh;
            return per.TryGetValue("en-US", out var en) && !string.IsNullOrEmpty(en) ? Places(en) : null;
        }

        /// <summary>Place names written as tokens in the table, filled with Hollow Knight's own name for them.</summary>
        static string Places(string s) => s.Contains("%TOWN%") ? s.Replace("%TOWN%", World.Areas.Display("德特茅斯")) : s;

        public static bool Has(string zh) => Table().ContainsKey(zh);

        /// <summary>
        /// The text for something Terraria also has: Terraria's own line in the current language when
        /// the mod's Chinese is Terraria's Chinese word for word (the mod didn't change it), else the
        /// mod's table, else Terraria's line, else the Chinese.
        /// </summary>
        public static string Pick(string zh, string vanillaZh, string vanillaLocal)
        {
            if (string.IsNullOrEmpty(zh) || IsSource) return zh;
            if (!string.IsNullOrEmpty(vanillaZh) && !string.IsNullOrEmpty(vanillaLocal) && Same(zh, vanillaZh)) return vanillaLocal;
            return Lookup(zh, Culture) ?? Terraria.TerrariaText.FromChinese(zh, Culture) ?? (string.IsNullOrEmpty(vanillaLocal) ? zh : vanillaLocal);
        }

        /// <summary>Two texts the same but for spacing, line breaks and the kind of quotation marks.</summary>
        public static bool Same(string a, string b) => Norm(a) == Norm(b);

        static string Norm(string s)
        {
            if (s == null) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char ch in s)
            {
                if (char.IsWhiteSpace(ch) || ch == '"' || ch == '“' || ch == '”' || ch == '\'' || ch == '‘' || ch == '’' || ch == '「' || ch == '」') continue;
                sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>Dev: the table's size.</summary>
        public static int DevEntries => Table().Count;
    }
}
