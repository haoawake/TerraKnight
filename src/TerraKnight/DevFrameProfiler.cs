using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Development only (the "lag" autotest suites): times every script's Update, LateUpdate and
    /// FixedUpdate, the mod's own per-frame methods, and counts the sounds started, to find what makes
    /// a room's frames long. PlayMaker FSMs are timed one by one (object and FSM name).
    /// </summary>
    internal static class DevFrameProfiler
    {
        public static bool Recording;
        /// <summary>Each method's managed allocation counted too (the heap's growth across the call, what it calls included).</summary>
        public static bool Allocations;
        static readonly Dictionary<string, long> allocated = new Dictionary<string, long>();
        /// <summary>What each method allocated itself, the timed methods it called taken off.</summary>
        static readonly Dictionary<string, long> allocatedOwn = new Dictionary<string, long>();
        /// <summary>The timed calls under way, each one's callees' growth so far.</summary>
        static readonly List<long> childGrowth = new List<long>();
        static readonly Dictionary<string, (long ticks, int calls, long worst)> totals = new Dictionary<string, (long, int, long)>();
        static readonly Dictionary<MethodBase, string> keys = new Dictionary<MethodBase, string>();
        /// <summary>Sounds started while recording: clip, the object that played it, where.</summary>
        public static readonly List<(string clip, string by, Vector2 at, float time)> Sounds = new List<(string, string, Vector2, float)>();

        static readonly HashSet<string> modNames = new HashSet<string>
        {
            "Update", "LateUpdate", "FixedUpdate", "Tick", "LateTick", "Draw", "OnGUI", "Step", "Sample", "Rebuild", "Apply", "Refresh", "Scan",
            "Flush", "Build", "UpdateAll", "DrawAll", "TickAll", "Simulate", "Light", "Recompute", "Sync", "Pump",
        };

        /// <summary>
        /// With <paramref name="everyMethod"/> (the "buildlag" suite), every method of the mod outside the World namespace
        /// is timed too (bar the very short ones), so that an allocation is pinned to the method making it.
        /// </summary>
        public static bool Installed;

        public static void Install(Harmony harmony, bool everyMethod = false)
        {
            Installed = true;
            var sw = Stopwatch.StartNew();
            var prefix = new HarmonyMethod(typeof(DevFrameProfiler).GetMethod(nameof(Before), BindingFlags.Static | BindingFlags.NonPublic));
            var postfix = new HarmonyMethod(typeof(DevFrameProfiler).GetMethod(nameof(After), BindingFlags.Static | BindingFlags.NonPublic));
            int patched = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string n = asm.GetName().Name;
                bool ours = n == "TerraKnight";
                if (!ours && n != "Assembly-CSharp" && n != "PlayMaker" && n != "Assembly-CSharp-firstpass") continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = Array.FindAll(e.Types, t => t != null); }
                foreach (var t in types)
                {
                    if (t.IsGenericTypeDefinition || t.ContainsGenericParameters) continue;
                    if (ours && (t.Name.StartsWith("Dev") || t.FullName.Contains("DevAutoTest"))) continue;
                    bool mono = typeof(MonoBehaviour).IsAssignableFrom(t);
                    if (!ours && !mono) continue;
                    foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (m.IsAbstract || m.IsGenericMethodDefinition || m.ContainsGenericParameters || m.GetMethodBody() == null) continue;
                        bool callback = mono && !m.IsStatic && m.GetParameters().Length == 0 && (m.Name == "Update" || m.Name == "LateUpdate" || m.Name == "FixedUpdate" || m.Name == "OnGUI");
                        bool modTick = ours && modNames.Contains(m.Name);
                        bool every = everyMethod && ours && !m.IsConstructor && !(t.Namespace ?? "").StartsWith("TerraKnight.World")
                            && t != typeof(PerfProbe) && m.GetMethodBody().GetILAsByteArray().Length >= 24;
                        if (!callback && !modTick && !every) continue;
                        try
                        {
                            harmony.Patch(m, prefix, postfix);
                            var owner = t.IsNested && t.Name.StartsWith("<") ? t.DeclaringType : t;
                            keys[m] = $"{owner.Name}.{m.Name}";
                            patched++;
                        }
                        catch { }
                    }
                }
            }
            if (everyMethod)
                foreach (var t in new[] { typeof(GUI), typeof(GUIStyle) })
                    foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    {
                        if (m.Name != "Label" && m.Name != "DrawTexture" && m.Name != "DrawTextureWithTexCoords" && m.Name != "Box" && m.Name != "CalcSize" && m.Name != "CalcHeight") continue;
                        if (m.IsGenericMethodDefinition || m.GetMethodBody() == null) continue;
                        try
                        {
                            harmony.Patch(m, prefix, postfix);
                            keys[m] = $"{t.Name}.{m.Name}";
                            patched++;
                        }
                        catch { }
                    }
            // Sounds: the plain ways a sound starts.
            var sound = new HarmonyMethod(typeof(DevFrameProfiler).GetMethod(nameof(SoundStarted), BindingFlags.Static | BindingFlags.NonPublic));
            int sounds = 0;
            foreach (var m in typeof(AudioSource).GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (m.Name != "Play" && m.Name != "PlayOneShot" && m.Name != "PlayDelayed" && m.Name != "PlayScheduled") continue;
                try { harmony.Patch(m, sound); sounds++; }
                catch { }
            }
            Plugin.Log.LogInfo($"[剖析] 给 {patched} 个每帧方法加了计时、{sounds} 个放音方法加了计数，{sw.ElapsedMilliseconds} ms");
        }

        public static int Calls;

        static void Before(out (long at, long heap, int depth) __state)
        {
            if (!Recording)
            {
                __state = (0, 0, 0);
                return;
            }
            long heap = 0;
            int depth = 0;
            if (Allocations)
            {
                depth = childGrowth.Count;
                childGrowth.Add(0);
                heap = GC.GetTotalMemory(false);
            }
            __state = (Stopwatch.GetTimestamp(), heap, depth);
        }

        static void After((long at, long heap, int depth) __state, MethodBase __originalMethod, object __instance)
        {
            long now = __state.heap != 0 ? GC.GetTotalMemory(false) : 0;
            Calls++;
            if (__state.at == 0 || !Recording) return;
            long dt = Stopwatch.GetTimestamp() - __state.at;
            if (!keys.TryGetValue(__originalMethod, out var key)) key = __originalMethod.Name;
            if (__instance is PlayMakerFSM fsm && fsm != null) key = $"FSM {Base(fsm.gameObject.name)}/{fsm.FsmName}";
            totals.TryGetValue(key, out var v);
            totals[key] = (v.ticks + dt, v.calls + 1, Math.Max(v.worst, dt));
            if (__state.heap != 0)
            {
                long grew = now - __state.heap, children = 0;
                int d = __state.depth;
                // A call that threw left its entries: they go with it.
                if (childGrowth.Count > d)
                {
                    children = childGrowth[d];
                    childGrowth.RemoveRange(d, childGrowth.Count - d);
                }
                // A collection in between makes it negative: left out.
                if (grew > 0)
                {
                    allocated[key] = (allocated.TryGetValue(key, out long a) ? a : 0) + grew;
                    allocatedOwn[key] = (allocatedOwn.TryGetValue(key, out long o) ? o : 0) + Math.Max(0, grew - children);
                    if (d > 0 && d - 1 < childGrowth.Count) childGrowth[d - 1] += grew;
                }
            }
        }

        static void SoundStarted(AudioSource __instance, object[] __args)
        {
            if (!Recording || __instance == null) return;
            AudioClip clip = __args.Length > 0 && __args[0] is AudioClip c ? c : __instance.clip;
            Sounds.Add((clip != null ? clip.name : "?", PathOf(__instance.transform), __instance.transform.position, Time.time));
        }

        static string Base(string n)
        {
            int cut = n.IndexOf(" (");
            return cut > 0 ? n.Substring(0, cut) : n;
        }

        static string PathOf(Transform t)
        {
            string p = t.name;
            int depth = 0;
            for (var u = t.parent; u != null && depth < 3; u = u.parent, depth++) p = u.name + "/" + p;
            return p;
        }

        public static void Begin()
        {
            totals.Clear();
            allocated.Clear();
            allocatedOwn.Clear();
            childGrowth.Clear();
            Sounds.Clear();
            Recording = true;
        }

        /// <summary>The slowest methods since <see cref="Begin"/>, by total time (each one's own call, what it calls included).</summary>
        public static string Report(string label, int frames, int top = 25)
        {
            Recording = false;
            var list = new List<KeyValuePair<string, (long ticks, int calls, long worst)>>(totals);
            list.Sort((a, b) => b.Value.ticks.CompareTo(a.Value.ticks));
            double ms = 1000.0 / Stopwatch.Frequency;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count && i < top; i++)
            {
                var kv = list[i];
                sb.Append($"\n    {kv.Key} 共 {kv.Value.ticks * ms:F1} ms（每帧 {kv.Value.ticks * ms / Math.Max(1, frames):F2} ms，{kv.Value.calls} 次，最慢一次 {kv.Value.worst * ms:F1} ms）");
            }
            var bySound = new Dictionary<string, int>();
            foreach (var s in Sounds)
            {
                string k = s.clip + " ← " + s.by;
                bySound[k] = bySound.TryGetValue(k, out int n) ? n + 1 : 1;
            }
            var sounds = new List<KeyValuePair<string, int>>(bySound);
            sounds.Sort((a, b) => b.Value.CompareTo(a.Value));
            var ss = new System.Text.StringBuilder();
            for (int i = 0; i < sounds.Count && i < 12; i++) ss.Append($"\n    {sounds[i].Key} ×{sounds[i].Value}");
            var al = new System.Text.StringBuilder();
            if (Allocations)
            {
                var byBytes = new List<KeyValuePair<string, long>>(allocated);
                byBytes.Sort((a, b) => b.Value.CompareTo(a.Value));
                al.Append("\n  分配最多（连带调用的）：");
                for (int i = 0; i < byBytes.Count && i < 15; i++)
                    al.Append($"\n    {byBytes[i].Key} 共 {byBytes[i].Value / 1024f:F0} KB（每帧 {byBytes[i].Value / 1024f / Math.Max(1, frames):F1} KB）");
                var byOwn = new List<KeyValuePair<string, long>>(allocatedOwn);
                byOwn.Sort((a, b) => b.Value.CompareTo(a.Value));
                al.Append("\n  自己分配最多：");
                for (int i = 0; i < byOwn.Count && i < 40; i++)
                    al.Append($"\n    {byOwn[i].Key} 共 {byOwn[i].Value / 1024f:F0} KB（每帧 {byOwn[i].Value / 1024f / Math.Max(1, frames):F2} KB）");
            }
            return $"[剖析] {label}（{frames} 帧，计时回调 {Calls} 次）：{sb}\n  放音 {Sounds.Count} 次：{ss}{al}";
        }
    }
}
