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
        static readonly Dictionary<string, (long ticks, int calls, long worst)> totals = new Dictionary<string, (long, int, long)>();
        static readonly Dictionary<MethodBase, string> keys = new Dictionary<MethodBase, string>();
        /// <summary>Sounds started while recording: clip, the object that played it, where.</summary>
        public static readonly List<(string clip, string by, Vector2 at, float time)> Sounds = new List<(string, string, Vector2, float)>();

        static readonly HashSet<string> modNames = new HashSet<string>
        {
            "Update", "LateUpdate", "FixedUpdate", "Tick", "LateTick", "Draw", "OnGUI", "Step", "Sample", "Rebuild", "Apply", "Refresh", "Scan",
            "Flush", "Build", "UpdateAll", "DrawAll", "TickAll", "Simulate", "Light", "Recompute", "Sync", "Pump",
        };

        public static void Install(Harmony harmony)
        {
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
                        if (!callback && !modTick) continue;
                        try
                        {
                            harmony.Patch(m, prefix, postfix);
                            keys[m] = $"{t.Name}.{m.Name}";
                            patched++;
                        }
                        catch { }
                    }
                }
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

        static void Before(out long __state) => __state = Recording ? Stopwatch.GetTimestamp() : 0;

        static void After(long __state, MethodBase __originalMethod, object __instance)
        {
            Calls++;
            if (__state == 0 || !Recording) return;
            long dt = Stopwatch.GetTimestamp() - __state;
            if (!keys.TryGetValue(__originalMethod, out var key)) key = __originalMethod.Name;
            if (__instance is PlayMakerFSM fsm && fsm != null) key = $"FSM {Base(fsm.gameObject.name)}/{fsm.FsmName}";
            totals.TryGetValue(key, out var v);
            totals[key] = (v.ticks + dt, v.calls + 1, Math.Max(v.worst, dt));
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
            return $"[剖析] {label}（{frames} 帧，计时回调 {Calls} 次）：{sb}\n  放音 {Sounds.Count} 次：{ss}";
        }
    }
}
