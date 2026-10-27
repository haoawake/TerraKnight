using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;

namespace TerraKnight
{
    /// <summary>
    /// Dev (the players' 10-26 "联机的时候…一个玩家没声音，另一个玩家双倍甚至n倍的音效"): every sound this game starts while
    /// <see cref="On"/> — each PlayOneShot, each source that starts playing (Play, play on awake: seen frame by frame, as a
    /// source's Play may be inlined past a patch) — counted by clip, with how often the same clip started twice in one
    /// frame and which objects played it; and the audio snapshots the game moved to. <see cref="Report"/> is one line.
    /// </summary>
    [HarmonyPatch]
    internal static class DevAudioWatch
    {
        public static bool On;
        static float since;
        static int total, frame = -1;
        static readonly Dictionary<string, int> plays = new Dictionary<string, int>();
        static readonly Dictionary<string, int> twice = new Dictionary<string, int>();
        static readonly Dictionary<string, HashSet<string>> who = new Dictionary<string, HashSet<string>>();
        static readonly HashSet<string> thisFrame = new HashSet<string>();
        static readonly Dictionary<int, (bool playing, float time)> seen = new Dictionary<int, (bool, float)>();
        static readonly List<string> snapshots = new List<string>();

        public static void Begin()
        {
            plays.Clear(); twice.Clear(); who.Clear(); thisFrame.Clear(); seen.Clear(); snapshots.Clear(); stacks.Clear();
            total = 0;
            since = Time.time;
            On = true;
            // Sources already playing aren't new.
            foreach (var src in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                seen[src.GetInstanceID()] = (src.isPlaying, src.time);
        }

        static string Who(AudioSource src)
        {
            if (src == null) return "?";
            var t = src.transform;
            string root = t.root != t ? t.root.name + "/" : "";
            return (root + t.name).Replace("(Clone)", "").Replace(" (TK copy)", "†");
        }

        static void Count(AudioClip clip, AudioSource src)
        {
            if (!On) return;
            if (Time.frameCount != frame) { frame = Time.frameCount; thisFrame.Clear(); }
            string name = clip != null ? clip.name : "(无)";
            total++;
            plays[name] = plays.TryGetValue(name, out int n) ? n + 1 : 1;
            if (!thisFrame.Add(name)) twice[name] = twice.TryGetValue(name, out int k) ? k + 1 : 1;
            if (!who.TryGetValue(name, out var set)) who[name] = set = new HashSet<string>();
            if (set.Count < 4)
            {
                // The FSM running when it was started (its object, name and state), else the code's own frames.
                var fsm = HutongGames.PlayMaker.FsmExecutionStack.ExecutingFsm;
                string by = fsm != null ? $"{Who(src)}←{fsm.GameObjectName}:{fsm.Name}:{fsm.ActiveStateName}" : Who(src);
                if (set.Add(by) && fsm == null && stacks.Count < 12 && !stacks.ContainsKey(name))
                    stacks[name] = string.Join(" < ", new System.Diagnostics.StackTrace(2, false).GetFrames()?.Take(7)
                        .Select(f => f.GetMethod()).Where(m => m != null).Select(m => m.DeclaringType?.Name + "." + m.Name) ?? new string[0]);
            }
        }

        static readonly Dictionary<string, string> stacks = new Dictionary<string, string>();

        /// <summary>The code that started each sound no FSM was running for (the first of each clip).</summary>
        public static string Stacks() => string.Join(" ‖ ", stacks.Select(kv => $"{kv.Key}: {kv.Value}"));

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayOneShot), typeof(AudioClip), typeof(float))]
        static void OneShot(AudioSource __instance, AudioClip clip) => Count(clip, __instance);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioMixerSnapshot), nameof(AudioMixerSnapshot.TransitionTo))]
        static void Snapshot(AudioMixerSnapshot __instance)
        {
            if (On && __instance != null && snapshots.Count < 40) snapshots.Add($"{Time.time - since:F0}s {__instance.audioMixer?.name}:{__instance.name}");
        }

        /// <summary>Each frame while on: sources that started playing since the last.</summary>
        public static void Tick()
        {
            if (!On) return;
            foreach (var src in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                int id = src.GetInstanceID();
                bool playing = src.isPlaying;
                float time = playing && src.clip != null ? src.time : 0f;
                bool started = playing && (!seen.TryGetValue(id, out var was) || !was.playing || time + 0.05f < was.time);
                seen[id] = (playing, time);
                if (started) Count(src.clip, src);
            }
        }

        /// <summary>One line: the sounds started since <see cref="Begin"/>, the most first.</summary>
        public static string Report(int top = 22)
        {
            float secs = Mathf.Max(0.1f, Time.time - since);
            var lines = plays.OrderByDescending(p => p.Value).Take(top)
                .Select(p => $"{p.Key}×{p.Value}{(twice.TryGetValue(p.Key, out int k) ? $"(同帧+{k})" : "")}[{string.Join(",", who[p.Key])}]");
            int doubled = twice.Values.Sum();
            string snaps = snapshots.Count == 0 ? "无" : string.Join("、", snapshots);
            return $"{secs:F0}s 共 {total} 次（每秒 {total / secs:F1}），同帧重复 {doubled} 次；混音切换 {snaps}；{string.Join(" ", lines)}{(stacks.Count > 0 ? " ‖ 调用 " + Stacks() : "")}";
        }
    }
}
