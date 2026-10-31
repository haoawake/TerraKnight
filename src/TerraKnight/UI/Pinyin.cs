using System.Collections.Generic;
using System.IO;

namespace TerraKnight.UI
{
    /// <summary>
    /// Finding Chinese names by pinyin (the user's 10-31 "让图鉴搜索可以直接按照拼音来搜索，全拼，首字母"): typed in letters,
    /// a run of the name's characters is found by their syllables in full ("shuijing"), by their first letters ("sj"), or
    /// by both mixed ("shuij", "sjing"): each character by any start of any of its readings. The readings are
    /// Resources/pinyin.txt (tools/make_pinyin.py: pypinyin's toneless readings of the CJK block, ü as v; one syllable a
    /// line, then its characters), each ü also as u. What isn't a letter or a character with a reading in a name (a space,
    /// a dot, a digit) is passed over.
    /// </summary>
    internal static class Pinyin
    {
        static Dictionary<char, string[]> readings;

        /// <summary>Each name's characters' readings, found once (a name with no Chinese: <see cref="None"/>).</summary>
        static readonly Dictionary<string, string[][]> names = new Dictionary<string, string[][]>();
        static readonly string[][] None = new string[0][];
        static readonly string[] Skip = new string[0];

        static string lastQuery, lastLetters;

        /// <summary>The name has a run of characters the query spells in pinyin (a query of letters only, spaces and ' left out).</summary>
        public static bool Matches(string name, string query)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(query)) return false;
            string q = Letters(query);
            if (q == null) return false;
            var chars = Of(name);
            if (chars == None) return false;
            for (int start = 0; start < chars.Length; start++)
                if (chars[start] != Skip && From(chars, start, q)) return true;
            return false;
        }

        /// <summary>The query in lower-case letters (ü as v), or null when anything but letters, spaces and ' is in it (kept from the last call).</summary>
        static string Letters(string query)
        {
            if (query == lastQuery) return lastLetters;
            lastQuery = query;
            lastLetters = null;
            var sb = new System.Text.StringBuilder(query.Length);
            foreach (char c in query)
            {
                if (c == ' ' || c == '\'') continue;
                char l = c == 'ü' || c == 'Ü' ? 'v' : char.ToLowerInvariant(c);
                if (l < 'a' || l > 'z' || l > 127) return null;
                sb.Append(l);
            }
            // At most 64 letters: the steps below keep the places reached in the query as bits.
            if (sb.Length == 0 || sb.Length > 63) return null;
            return lastLetters = sb.ToString();
        }

        /// <summary>Whether the query is spelled out from that character on: the places in it each character can bring it to.</summary>
        static bool From(string[][] chars, int start, string q)
        {
            ulong at = 1UL;
            for (int i = start; i < chars.Length && at != 0UL; i++)
            {
                var opts = chars[i];
                if (opts == Skip) continue;
                ulong next = 0UL;
                for (int p = 0; p < q.Length; p++)
                {
                    if ((at & (1UL << p)) == 0UL) continue;
                    foreach (var s in opts)
                        for (int k = 0; k < s.Length && p + k < q.Length && s[k] == q[p + k]; k++)
                        {
                            if (p + k + 1 == q.Length) return true;
                            next |= 1UL << (p + k + 1);
                        }
                }
                at = next;
            }
            return false;
        }

        /// <summary>A name's characters' readings: a Chinese character's, a letter as itself; others passed over.</summary>
        static string[][] Of(string name)
        {
            if (names.TryGetValue(name, out var found)) return found;
            if (readings == null) Load();
            bool any = false;
            var chars = new string[name.Length][];
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (readings.TryGetValue(c, out var r)) { chars[i] = r; any = true; }
                else if (c < 128 && char.IsLetter(c)) chars[i] = Letter(char.ToLowerInvariant(c));
                else chars[i] = Skip;
            }
            return names[name] = any ? chars : None;
        }

        static readonly Dictionary<char, string[]> letters = new Dictionary<char, string[]>();

        static string[] Letter(char c)
        {
            if (!letters.TryGetValue(c, out var one)) letters[c] = one = new[] { c.ToString() };
            return one;
        }

        static void Load()
        {
            var list = new Dictionary<char, List<string>>();
            using (var s = typeof(Pinyin).Assembly.GetManifestResourceStream("TerraKnight.pinyin.txt"))
            using (var r = new StreamReader(s, System.Text.Encoding.UTF8))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    int sp = line.IndexOf(' ');
                    if (sp <= 0) continue;
                    string syl = line.Substring(0, sp);
                    // ü typed either way: lv, lu.
                    string alt = syl.IndexOf('v') >= 0 ? syl.Replace('v', 'u') : null;
                    for (int i = sp + 1; i < line.Length; i++)
                    {
                        if (!list.TryGetValue(line[i], out var l)) list[line[i]] = l = new List<string>(1);
                        if (!l.Contains(syl)) l.Add(syl);
                        if (alt != null && !l.Contains(alt)) l.Add(alt);
                    }
                }
            }
            readings = new Dictionary<char, string[]>(list.Count);
            foreach (var kv in list) readings[kv.Key] = kv.Value.ToArray();
        }

        /// <summary>Dev: how many characters have readings (0 before the first search).</summary>
        public static int DevCharacters => readings != null ? readings.Count : 0;
    }
}
