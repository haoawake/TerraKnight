using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Newtonsoft.Json.Linq;

namespace TerraKnight.Terraria
{
    /// <summary>
    /// Terraria's own text in every language it has (Terraria.Localization.Content.&lt;culture&gt;.*.json,
    /// embedded in the local Terraria.exe, read with Mono.Cecil without loading the game): item
    /// names and tooltips, buffs, NPCs, the rest. "{$Category.Key}" references are filled in, as
    /// LanguageManager does. The ids' internal names (ItemID, BuffID, NPCID constants) come from
    /// the same file's metadata. Nothing of Terraria's is shipped with the mod.
    /// </summary>
    internal static class TerrariaText
    {
        static ModuleDefinition module;
        static readonly Dictionary<string, Dictionary<string, string>> cultures = new Dictionary<string, Dictionary<string, string>>();
        static readonly Dictionary<string, Dictionary<int, string>> ids = new Dictionary<string, Dictionary<int, string>>();
        static readonly Regex reference = new Regex(@"\{\$([A-Za-z0-9_.]+)\}");
        static string exePath;

        public static bool Available => module != null;

        /// <summary>Opens the game's Terraria.exe beside its Content folder (the images' parent).</summary>
        public static bool Init(string terrariaDir)
        {
            try
            {
                exePath = Path.Combine(terrariaDir, "Terraria.exe");
                if (!File.Exists(exePath)) return false;
                module = ModuleDefinition.ReadModule(exePath, new ReaderParameters { ReadSymbols = false, InMemory = true });
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[语言] 读不了 Terraria.exe 的语言文件：{e.Message}");
                module = null;
                return false;
            }
        }

        /// <summary>Every "Category.Key" of a culture (en-US, zh-Hans, ja-JP...), loaded once.</summary>
        static Dictionary<string, string> Table(string culture)
        {
            if (cultures.TryGetValue(culture, out var t)) return t;
            t = new Dictionary<string, string>();
            cultures[culture] = t;
            if (module == null) return t;
            string prefix = "Terraria.Localization.Content." + culture + ".";
            string whole = "Terraria.Localization.Content." + culture + ".json";
            foreach (var res in module.Resources)
            {
                if (!(res is EmbeddedResource er)) continue;
                if (er.Name != whole && !er.Name.StartsWith(prefix)) continue;
                try
                {
                    string json = Encoding.UTF8.GetString(er.GetResourceData());
                    var root = JObject.Parse(json.TrimStart('﻿'));
                    foreach (var cat in root.Properties())
                    {
                        if (!(cat.Value is JObject obj)) continue;
                        foreach (var kv in obj.Properties())
                            if (kv.Value.Type == JTokenType.String) t[cat.Name + "." + kv.Name] = (string)kv.Value;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[语言] {er.Name} 读不了：{e.Message}");
                }
            }
            return t;
        }

        /// <summary>A key's text in a culture, references filled in; null when the culture hasn't it.</summary>
        public static string Get(string culture, string key)
        {
            var t = Table(culture);
            if (!t.TryGetValue(key, out var s) || s == null) return null;
            for (int depth = 0; depth < 4 && s.Contains("{$"); depth++)
                s = reference.Replace(s, m => t.TryGetValue(m.Groups[1].Value, out var r) ? r : m.Value);
            return s;
        }

        static Dictionary<string, (string Key, int Line)> zhIndex;

        /// <summary>
        /// The same line in another language, for a Chinese line that is Terraria's own Chinese word for
        /// word (a townsperson's chatter, an interface word): its key looked up by its zh-Hans text.
        /// </summary>
        public static string FromChinese(string zh, string culture)
        {
            if (module == null || string.IsNullOrEmpty(zh)) return null;
            if (zhIndex == null)
            {
                // Whole lines first, then each line of a longer one (its place in it).
                zhIndex = new Dictionary<string, (string, int)>();
                foreach (var kv in Table("zh-Hans"))
                    if (!string.IsNullOrEmpty(kv.Value) && !kv.Value.Contains("{$") && !zhIndex.ContainsKey(kv.Value)) zhIndex[kv.Value] = (kv.Key, -1);
                foreach (var kv in Table("zh-Hans"))
                {
                    if (string.IsNullOrEmpty(kv.Value) || !kv.Value.Contains("\n")) continue;
                    var parts = Get("zh-Hans", kv.Key).Split('\n');
                    for (int i = 0; i < parts.Length; i++)
                        if (parts[i].Length > 1 && !zhIndex.ContainsKey(parts[i])) zhIndex[parts[i]] = (kv.Key, i);
                }
            }
            if (!zhIndex.TryGetValue(zh, out var at)) return null;
            var text = Get(culture, at.Key);
            if (text == null || at.Line < 0) return text;
            var local = text.Split('\n');
            var source = Get("zh-Hans", at.Key).Split('\n');
            return local.Length == source.Length ? local[at.Line] : null;
        }

        /// <summary>The internal names of a Terraria.ID class's constants (ItemID, BuffID, NPCID) by value.</summary>
        static Dictionary<int, string> Ids(string typeName)
        {
            if (ids.TryGetValue(typeName, out var map)) return map;
            map = new Dictionary<int, string>();
            ids[typeName] = map;
            var type = module?.GetType("Terraria.ID." + typeName);
            if (type == null) return map;
            foreach (var f in type.Fields)
            {
                if (!f.HasConstant || f.Constant == null) continue;
                int v;
                try { v = Convert.ToInt32(f.Constant); }
                catch { continue; }
                if (!map.ContainsKey(v)) map[v] = f.Name;
            }
            return map;
        }

        public static string ItemKey(int id) => Ids("ItemID").TryGetValue(id, out var n) ? n : null;
        public static string BuffKey(int id) => Ids("BuffID").TryGetValue(id, out var n) ? n : null;
        public static string NpcKey(int id) => Ids("NPCID").TryGetValue(id, out var n) ? n : null;

        /// <summary>
        /// The housing menu's head sprite for a townsperson's internal name: Terraria's NPCHeadID
        /// constants, whose names are the townsfolk's (Guide, Merchant, Nurse...), read as the
        /// Images/NPC_Head_&lt;n&gt; file. -1 when it has none.
        /// </summary>
        public static int HeadIndex(string internalName)
        {
            if (string.IsNullOrEmpty(internalName)) return -1;
            if (headAliases.TryGetValue(internalName, out var alias)) internalName = alias;
            if (headIndexes == null)
            {
                headIndexes = new Dictionary<string, int>();
                foreach (var kv in Ids("NPCHeadID")) headIndexes[kv.Value] = kv.Key;
            }
            return headIndexes.TryGetValue(internalName, out var index) ? index : -1;
        }

        static Dictionary<string, int> headIndexes;

        /// <summary>
        /// The town pets and the town slimes: Terraria's NPCHeadID spells them differently from NPCID
        /// (the cats, dogs and bunnies by their coat, the slimes without the "Town" NPCID prefixes them
        /// with). Without these the housing menu has no head to draw for them and falls back to a crop of
        /// their walking sheet, which reads as a blob rather than a face.
        /// </summary>
        static readonly Dictionary<string, string> headAliases = new Dictionary<string, string>
        {
            ["DD2Bartender"] = "Tavernkeep",
            ["TownCat"] = "CatSiamese",
            ["TownDog"] = "DogLabrador",
            ["TownBunny"] = "BunnyWhite",
            ["TownSlimeBlue"] = "SlimeBlue",
            ["TownSlimeGreen"] = "SlimeGreen",
            ["TownSlimeOld"] = "SlimeOld",
            ["TownSlimePurple"] = "SlimePurple",
            ["TownSlimeRainbow"] = "SlimeRainbow",
            ["TownSlimeRed"] = "SlimeRed",
            ["TownSlimeYellow"] = "SlimeYellow",
            ["TownSlimeCopper"] = "SlimeCopper",
        };

        public static string ItemName(string culture, int id) { var k = ItemKey(id); return k == null ? null : Get(culture, "ItemName." + k); }
        public static string ItemTooltip(string culture, int id) { var k = ItemKey(id); return k == null ? null : Get(culture, "ItemTooltip." + k); }
        public static string BuffName(string culture, int id) { var k = BuffKey(id); return k == null ? null : Get(culture, "BuffName." + k); }
        public static string BuffDescription(string culture, int id) { var k = BuffKey(id); return k == null ? null : Get(culture, "BuffDescription." + k); }
        public static string NpcName(string culture, int id) { var k = NpcKey(id); return k == null ? null : Get(culture, "NPCName." + k); }

        /// <summary>Dev: how much was read.</summary>
        public static string DevReport(string culture) => $"{culture} {Table(culture).Count} 条，物品编号 {Ids("ItemID").Count} 个，Buff {Ids("BuffID").Count} 个";
    }
}
