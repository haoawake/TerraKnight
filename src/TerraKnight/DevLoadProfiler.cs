using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Development only (the "profile" autotest suite): times every script's Awake, OnEnable and
    /// Start while a neighbouring scene loads in the background, to find what makes that frame long.
    /// </summary>
    internal static class DevLoadProfiler
    {
        public static bool Recording;
        static readonly Dictionary<MethodBase, (long ticks, int calls)> totals = new Dictionary<MethodBase, (long, int)>();
        static int depth;

        public static void Install(Harmony harmony)
        {
            var sw = Stopwatch.StartNew();
            var prefix = new HarmonyMethod(typeof(DevLoadProfiler).GetMethod(nameof(Before), BindingFlags.Static | BindingFlags.NonPublic));
            var postfix = new HarmonyMethod(typeof(DevLoadProfiler).GetMethod(nameof(After), BindingFlags.Static | BindingFlags.NonPublic));
            int patched = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string n = asm.GetName().Name;
                if (n != "Assembly-CSharp" && n != "PlayMaker" && n != "Assembly-CSharp-firstpass") continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = Array.FindAll(e.Types, t => t != null); }
                foreach (var t in types)
                {
                    if (t.IsGenericTypeDefinition || !typeof(MonoBehaviour).IsAssignableFrom(t) || t.Namespace?.StartsWith("TerraKnight") == true) continue;
                    foreach (var name in new[] { "Awake", "OnEnable", "Start" })
                    {
                        var m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                        if (m == null || m.ReturnType != typeof(void) || m.IsAbstract) continue;
                        try
                        {
                            harmony.Patch(m, prefix, postfix);
                            patched++;
                        }
                        catch { }
                    }
                }
            }
            Plugin.Log.LogInfo($"[剖析] 给 {patched} 个 Awake/OnEnable/Start 加了计时，{sw.ElapsedMilliseconds} ms");
        }

        static void Before(out long __state)
        {
            // Only the outermost call is timed, so nested Awake calls aren't counted twice.
            __state = Recording && depth++ == 0 ? Stopwatch.GetTimestamp() : 0;
        }

        static void After(long __state, MethodBase __originalMethod)
        {
            if (!Recording) { depth = 0; return; }
            depth = Mathf.Max(0, depth - 1);
            if (__state == 0) return;
            long dt = Stopwatch.GetTimestamp() - __state;
            totals.TryGetValue(__originalMethod, out var v);
            totals[__originalMethod] = (v.ticks + dt, v.calls + 1);
        }

        /// <summary>The slowest methods since the last report, by total time.</summary>
        public static void Report(string label)
        {
            var list = new List<KeyValuePair<MethodBase, (long ticks, int calls)>>(totals);
            list.Sort((a, b) => b.Value.ticks.CompareTo(a.Value.ticks));
            double all = 0;
            foreach (var kv in list) all += kv.Value.ticks;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count && i < 15; i++)
            {
                var kv = list[i];
                sb.Append($"\n    {kv.Key.DeclaringType.Name}.{kv.Key.Name} {kv.Value.ticks * 1000.0 / Stopwatch.Frequency:F1} ms / {kv.Value.calls} 次");
            }
            Plugin.Log.LogInfo($"[剖析] {label}：脚本初始化共 {all * 1000.0 / Stopwatch.Frequency:F1} ms：{sb}");
            totals.Clear();
        }
    }
}
