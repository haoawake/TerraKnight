using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TerraKnight.UI;

namespace TerraKnight
{
    // PinyinSearch 只在资源缺失/损坏时写日志；测试程序里给一个不依赖 BepInEx 的替身。
    internal static class Plugin
    {
        public static readonly StubLog Log = new StubLog();
    }

    internal sealed class StubLog
    {
        public void LogWarning(string message) => Console.Error.WriteLine("WARN " + message);
        public void LogError(string message) => Console.Error.WriteLine("ERROR " + message);
    }
}

namespace PinyinTest
{
    internal static class Program
    {
        static int failures;

        static void Check(string label, bool expected, bool actual)
        {
            if (expected == actual) return;
            failures++;
            Console.WriteLine($"FAIL {label}: expected {expected}, got {actual}");
        }

        static int Main(string[] args)
        {
            string casesPath = args.Length > 0 ? args[0] : "cases.json";
            var watch = Stopwatch.StartNew();
            PinyinSearch.Preload();
            Console.WriteLine($"table loaded in {watch.ElapsedMilliseconds} ms, ready={PinyinSearch.Ready}");

            // 手工用例：全拼、首字母、混合、多音字、ü/v、空格 AND、拉丁名。
            Check("full 物品/wupin", true, PinyinSearch.Matches("物品", "wupin"));
            Check("initials 物品/wp", true, PinyinSearch.Matches("物品", "wp"));
            Check("mixed 物品/wpin", true, PinyinSearch.Matches("物品", "wpin"));
            Check("substring 天顶剑/dingjian", true, PinyinSearch.Matches("天顶剑", "dingjian"));
            Check("substring 天顶剑/dj", true, PinyinSearch.Matches("天顶剑", "dj"));
            Check("polyphone 重庆/chongqing", true, PinyinSearch.Matches("重庆", "chongqing"));
            Check("polyphone 重庆/zhongqing", true, PinyinSearch.Matches("重庆", "zhongqing"));
            Check("umlaut 女/nv", true, PinyinSearch.Matches("女", "nv"));
            Check("umlaut 女/nu", true, PinyinSearch.Matches("女", "nu"));
            Check("umlaut 绿/lv", true, PinyinSearch.Matches("绿", "lv"));
            Check("words 铁剑/tie jian", true, PinyinSearch.Matches("铁剑", "tie jian"));
            Check("words order 铁剑/jian tie", true, PinyinSearch.Matches("铁剑", "jian tie"));
            Check("latin Zenith/zen", true, PinyinSearch.Matches("Zenith", "zen"));
            Check("latin+cn 杜兰达 · Duranda/duranda", true, PinyinSearch.Matches("杜兰达 · Duranda", "dulanda"));
            Check("negative 物品/wuping", false, PinyinSearch.Matches("物品", "wuping"));
            Check("negative 物品/xyz", false, PinyinSearch.Matches("物品", "xyz"));
            Check("chinese query 物品/物", true, PinyinSearch.Matches("物品", "物"));
            Check("empty query", true, PinyinSearch.Matches("物品", ""));

            if (!File.Exists(casesPath))
            {
                Console.WriteLine($"no cases file at {casesPath}");
            }
            else
            {
                var cases = JsonSerializer.Deserialize<List<Case>>(File.ReadAllText(casesPath));
                foreach (var c in cases)
                {
                    Check("full " + c.Name + " / " + c.Full, true, PinyinSearch.Matches(c.Name, c.Full));
                    Check("initials " + c.Name + " / " + c.Initials, true, PinyinSearch.Matches(c.Name, c.Initials));
                }
                Console.WriteLine($"cases: {cases.Count}");
            }

            // 性能：拿全部名字跑一遍最常见的「首字母」查询，看看每帧要多久。
            if (File.Exists(casesPath))
            {
                var cases = JsonSerializer.Deserialize<List<Case>>(File.ReadAllText(casesPath));
                var sw = Stopwatch.StartNew();
                int hits = 0;
                foreach (var c in cases) if (PinyinSearch.Matches(c.Name, c.Initials)) hits++;
                Console.WriteLine($"all initial queries: {hits}/{cases.Count} in {sw.ElapsedMilliseconds} ms");
                sw.Restart();
                int hits2 = 0;
                foreach (var c in cases) if (PinyinSearch.Matches(c.Name, c.Full)) hits2++;
                Console.WriteLine($"all full queries: {hits2}/{cases.Count} in {sw.ElapsedMilliseconds} ms");
            }

            Console.WriteLine(failures == 0 ? "OK" : $"{failures} failure(s)");
            return failures == 0 ? 0 : 1;
        }

        sealed class Case
        {
            public string Name { get; set; }
            public string Full { get; set; }
            public string Initials { get; set; }
        }
    }
}
