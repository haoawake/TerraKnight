using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace TerraKnight.UI
{
    /// <summary>
    /// Pinyin search over Chinese names: Latin letters find a name by the full spellings of its syllables ("wupin"),
    /// by their initials ("wp") or by any mix of the two ("wpin", "zhguo", "wu品"). A character matches through
    /// every reading it has, so 重庆 is found by "chongqing" and "zhongqing" alike, and ü typed as u or v matches
    /// either way. Spaces between words are AND: every word must be found, in any order.
    /// </summary>
    internal static class PinyinSearch
    {
        // The table is lazy: 10 KB gzipped, read on the first search (the game never pays for it otherwise).
        static Dictionary<char, string[]> readings;

        // A query's answers, thrown away when the query changes (the same names are tested every frame).
        static string cachedQuery;
        static readonly Dictionary<string, bool> cache = new Dictionary<string, bool>();

        // Failure memo for one token: avoids walking a dead prefix again. 0 unknown, 1 no, 2 yes.
        static byte[,] memo = new byte[64, 64];

        /// <summary>Dev: the table is what a search uses (it loads on the first one).</summary>
        public static bool Ready => readings != null;

        /// <summary>Loads the table now: a search started from a cold frame costs this once.</summary>
        public static void Preload() { Table(); }

        /// <summary>Whether the query finds the text: literal text first (Chinese, Latin, digits), then pinyin words.</summary>
        public static bool Matches(string text, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            if (string.IsNullOrEmpty(text)) return false;
            if (query != cachedQuery) { cachedQuery = query; cache.Clear(); }
            if (cache.TryGetValue(text, out bool known)) return known;
            bool found = MatchesWords(text, query);
            cache[text] = found;
            return found;
        }

        static bool MatchesWords(string text, string query)
        {
            int i = 0;
            while (i < query.Length)
            {
                while (i < query.Length && IsSpace(query[i])) i++;
                int start = i;
                while (i < query.Length && !IsSpace(query[i])) i++;
                if (i == start) continue;
                string word = Lower(query, start, i - start);
                if (!MatchesWord(text, word)) return false;
            }
            return true;
        }

        static bool MatchesWord(string text, string word)
        {
            // The text as it is typed: Chinese names, drop names, Latin names, digits ("钉 12").
            if (text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            // No Latin letter in the word: only the text itself can match it (a punctuation mark, say).
            bool latin = false;
            for (int i = 0; i < word.Length && !latin; i++) latin = word[i] < 128 && char.IsLetterOrDigit(word[i]);
            if (!latin) return false;
            var table = Table();
            if (table.Count == 0) return false;
            if (memo.GetLength(0) <= text.Length || memo.GetLength(1) <= word.Length)
                memo = new byte[text.Length + 1, word.Length + 1];
            Array.Clear(memo, 0, memo.Length);
            for (int start = 0; start < text.Length; start++)
                if (MatchFrom(text, start, word, 0, table)) return true;
            return false;
        }

        /// <summary>The word matched from text[ti] on: a syllable takes a prefix of itself (an initial, or a whole
        /// spelling, or anything between) and the next character takes the rest.</summary>
        static bool MatchFrom(string text, int ti, string word, int wi, Dictionary<char, string[]> table)
        {
            if (wi == word.Length) return true;
            if (ti == text.Length) return false;
            byte known = memo[ti, wi];
            if (known != 0) return known == 2;
            bool found = false;
            char c = text[ti];
            char w = word[wi];
            if (c < 128) found = char.ToLowerInvariant(c) == w && MatchFrom(text, ti + 1, word, wi + 1, table);
            else if (w >= 128) found = c == w && MatchFrom(text, ti + 1, word, wi + 1, table);
            else if (table.TryGetValue(c, out var rs))
            {
                foreach (string r in rs)
                {
                    int max = Math.Min(r.Length, word.Length - wi), k = 0;
                    while (k < max && SameLetter(r[k], word[wi + k])) k++;
                    for (int take = 1; take <= k && !found; take++)
                    {
                        if (wi + take == word.Length) { found = true; break; }
                        found = MatchFrom(text, ti + 1, word, wi + take, table);
                    }
                    if (found) break;
                }
            }
            memo[ti, wi] = found ? (byte)2 : (byte)1;
            return found;
        }

        /// <summary>ü is written v in the table: a u or a v matches the other, a letter matches its own case's pair.</summary>
        static bool SameLetter(char table, char word) =>
            table == word || table == 'v' && word == 'u' || table == 'u' && word == 'v';

        static bool IsSpace(char c) => c == ' ' || c == '\t' || c == '\u3000' || c == '\r' || c == '\n';

        static string Lower(string s, int start, int length)
        {
            var chars = new char[length];
            for (int i = 0; i < length; i++) chars[i] = char.ToLowerInvariant(s[start + i]);
            return new string(chars);
        }

        static Dictionary<char, string[]> Table()
        {
            if (readings != null) return readings;
            var table = new Dictionary<char, List<string>>(4096);
            try
            {
                var stream = typeof(PinyinSearch).Assembly.GetManifestResourceStream("TerraKnight.pinyin.dat");
                if (stream == null) Plugin.Log?.LogWarning("[Pinyin] TerraKnight.pinyin.dat is missing: search stays literal.");
                else
                {
                    using (stream)
                    using (var gz = new GZipStream(stream, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gz, new UTF8Encoding(false)))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            int tab = line.IndexOf('\t');
                            if (tab <= 0) continue;
                            string pinyin = line.Substring(0, tab);
                            for (int i = tab + 1; i < line.Length; i++)
                            {
                                char c = line[i];
                                if (!table.TryGetValue(c, out var list)) table[c] = list = new List<string>(2);
                                if (!list.Contains(pinyin)) list.Add(pinyin);
                            }
                        }
                    }
                }
            }
            catch (Exception e) { Plugin.Log?.LogError("[Pinyin] the table could not be read: " + e); }
            if (table.Count > 0)
            {
                readings = new Dictionary<char, string[]>(table.Count);
                foreach (var pair in table) readings[pair.Key] = pair.Value.ToArray();
            }
            else readings = new Dictionary<char, string[]>();
            return readings;
        }
    }
}
