using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Newtonsoft.Json;
using TeamCherry.Localization;

namespace TerraKnight.UI
{
    /// <summary>
    /// Adapt the identity of the person addressed, not the identity of historical
    /// characters. Keyed at Language.Get so normal, dream, yes/no and broadcast
    /// conversations all see the same text, independent of BroadcastDialogue.
    /// </summary>
    [HarmonyPatch]
    internal static class TerrarianDialogue
    {
        internal sealed class Entry
        {
            public string Zh { get; set; }
            public string En { get; set; }
            public string Reason { get; set; }
        }

        static readonly Lazy<Dictionary<string, Entry>> entries = new Lazy<Dictionary<string, Entry>>(Load);
        internal static IReadOnlyDictionary<string, Entry> All => entries.Value;

        static Dictionary<string, Entry> Load()
        {
            using var stream = typeof(TerrarianDialogue).Assembly.GetManifestResourceStream("TerraKnight.terrarian-dialogue.json");
            if (stream == null) throw new InvalidOperationException("Missing Terrarian dialogue resource");
            using var reader = new StreamReader(stream);
            return JsonConvert.DeserializeObject<Dictionary<string, Entry>>(reader.ReadToEnd());
        }

        internal static string Resolve(string key, string sheet, LanguageCode language, string original)
        {
            if (key == null || sheet == null || !entries.Value.TryGetValue(sheet + "/" + key, out var row)) return original;
            // Authored Chinese and English. Other languages use English rather
            // than silently restoring the original Vessel/player ancestry.
            return language == LanguageCode.ZH || language == LanguageCode.ZH_TW ? row.Zh : row.En;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Language), nameof(Language.Get), new[] { typeof(string), typeof(string) })]
        static void Get(string key, string sheetTitle, ref string __result) =>
            __result = Resolve(key, sheetTitle, Language.CurrentLanguage(), __result);
    }
}
