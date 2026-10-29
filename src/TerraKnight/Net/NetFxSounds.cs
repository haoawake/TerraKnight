using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;

namespace TerraKnight.Net
{
    /// <summary>
    /// What the game running a scene's enemies hears of them, heard in the others' games too (the players' 10-26 "联机的时候…
    /// 一个玩家没声音"): a puppet's FSMs are frozen, so the sounds its FSM plays (a boss's leaps, slashes and cries, an enemy's
    /// attacks) were the owner's alone. Each one an enemy's FSM plays in a scene this game runs is sent with its source's
    /// settings and played again where it was by whoever is in that room. In a boss room the music its fight's FSMs set
    /// (the boss's theme, the silence before it, the mixer's snapshots) is sent the same way.
    /// </summary>
    [HarmonyPatch]
    internal static partial class NetFx
    {
        const byte KSound = 5, KMusic = 6;
        const byte MCue = 1, MAtmos = 2, MMusicSnapshot = 3, MSnapshot = 4;
        const int MaxSoundsPerSecond = 40;

        public static int DevSoundsSent, DevSoundsPlayed, DevSoundsMissing, DevMusicSent, DevMusicApplied;

        static float soundSecond;
        static int soundsThisSecond;

        /// <summary>The scene of the FSM running now when this game runs its enemies and another player has it, else null.</summary>
        static string OwnedFsmScene(bool enemyOnly)
        {
            if (Applying || !NetSession.Active) return null;
            var go = HutongGames.PlayMaker.FsmExecutionStack.ExecutingFsm?.GameObject;
            if (go == null) return null;
            string scene = go.scene.name;
            if (!Enemies.Owns(scene) || !Watched(scene)) return null;
            if (enemyOnly && go.GetComponentInParent<HealthManager>() == null) return null;
            return scene;
        }

        // ------------------------------------------------------------------ the owner's side

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayOneShot), typeof(AudioClip), typeof(float))]
        static void OneShot(AudioSource __instance, AudioClip clip, float volumeScale)
        {
            if (clip == null || __instance == null) return;
            string scene = OwnedFsmScene(true);
            if (scene != null) SendSound(scene, __instance, clip, __instance.volume * volumeScale);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Play), new System.Type[0])]
        static void Play(AudioSource __instance)
        {
            // A loop goes on until its FSM stops it: not sent (a spawned shot's own loop is on its copy already).
            if (__instance == null || __instance.clip == null || __instance.loop) return;
            string scene = OwnedFsmScene(true);
            if (scene != null) SendSound(scene, __instance, __instance.clip, __instance.volume);
        }

        static void SendSound(string scene, AudioSource src, AudioClip clip, float volume)
        {
            if (Time.unscaledTime - soundSecond >= 1f) { soundSecond = Time.unscaledTime; soundsThisSecond = 0; }
            if (++soundsThisSecond > MaxSoundsPerSecond) return;
            Vector3 at = src.transform.position;
            var local = NetScenes.ToLocal(scene, at);
            var group = src.outputAudioMixerGroup;
            var p = NetSession.Mine(Msg.NetFx);
            p.W.Write(KSound);
            p.W.Write(scene);
            p.W.Write(local.x);
            p.W.Write(local.y);
            p.W.Write(at.z);
            p.W.Write(clip.name);
            p.W.Write(volume);
            p.W.Write(src.pitch);
            p.W.Write(src.spatialBlend);
            p.W.Write(src.minDistance);
            p.W.Write(src.maxDistance);
            p.W.Write((byte)src.rolloffMode);
            p.W.Write(group != null && group.audioMixer != null ? group.audioMixer.name : "");
            p.W.Write(group != null ? group.name : "");
            NetSession.Send(p.Done());
            DevSoundsSent++;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.ApplyMusicCue))]
        static void MusicCue(MusicCue musicCue, float delayTime, float transitionTime, bool applySnapshot)
        {
            string scene = musicCue != null ? OwnedFsmScene(false) : null;
            if (scene == null || !World.BossRooms.IsRoom(scene)) return;
            SendMusic(scene, MCue, "", musicCue.name, delayTime, transitionTime, applySnapshot);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.ApplyAtmosCue))]
        static void AtmosCue(AtmosCue atmosCue, float transitionTime)
        {
            string scene = atmosCue != null ? OwnedFsmScene(false) : null;
            if (scene == null || !World.BossRooms.IsRoom(scene)) return;
            SendMusic(scene, MAtmos, "", atmosCue.name, 0f, transitionTime, false);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.ApplyMusicSnapshot))]
        static void MusicSnapshot(AudioMixerSnapshot snapshot, float delayTime, float transitionTime)
        {
            string scene = snapshot != null && snapshot.audioMixer != null ? OwnedFsmScene(false) : null;
            if (scene == null || !World.BossRooms.IsRoom(scene)) return;
            SendMusic(scene, MMusicSnapshot, snapshot.audioMixer.name, snapshot.name, delayTime, transitionTime, false);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioMixerSnapshot), nameof(AudioMixerSnapshot.TransitionTo))]
        static void Snapshot(AudioMixerSnapshot __instance, float timeToReach)
        {
            string scene = __instance != null && __instance.audioMixer != null ? OwnedFsmScene(false) : null;
            if (scene == null || !World.BossRooms.IsRoom(scene)) return;
            SendMusic(scene, MSnapshot, __instance.audioMixer.name, __instance.name, 0f, timeToReach, false);
        }

        static void SendMusic(string scene, byte what, string mixer, string name, float delay, float transition, bool applySnapshot)
        {
            var p = NetSession.Mine(Msg.NetFx);
            p.W.Write(KMusic);
            p.W.Write(scene);
            p.W.Write(what);
            p.W.Write(mixer);
            p.W.Write(name);
            p.W.Write(delay);
            p.W.Write(transition);
            p.W.Write(applySnapshot);
            NetSession.Send(p.Done());
            DevMusicSent++;
        }

        // ------------------------------------------------------------------ the others'

        static string RoomOf(string scene) => scene != null && scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;

        /// <summary>The player is in that scene's room, or near the point (a world of several rooms laid side by side).</summary>
        static bool Hears(string scene, Vector2 at)
        {
            if (RoomOf(scene) == RoomOf(NetScenes.Current)) return true;
            var hero = HeroController.SilentInstance;
            return hero != null && ((Vector2)hero.transform.position - at).sqrMagnitude < 30f * 30f;
        }

        static void ReceiveSound(BinaryReader r, string scene, bool here, Vector2 shift)
        {
            var at = new Vector3(r.ReadSingle(), r.ReadSingle(), 0f);
            at.z = r.ReadSingle();
            string clipName = r.ReadString();
            float volume = r.ReadSingle(), pitch = r.ReadSingle(), blend = r.ReadSingle(), min = r.ReadSingle(), max = r.ReadSingle();
            var rolloff = (AudioRolloffMode)r.ReadByte();
            string mixer = r.ReadString(), groupName = r.ReadString();
            at += (Vector3)shift;
            if (!here || !Hears(scene, at)) return;
            var clip = Named(clips, clipName, ref clipsAt);
            if (clip == null) { DevSoundsMissing++; return; }
            var src = FreeSource(clip.length / Mathf.Max(0.1f, Mathf.Abs(pitch)));
            src.transform.position = at;
            src.volume = 1f;
            src.pitch = pitch;
            src.spatialBlend = blend;
            src.minDistance = min;
            src.maxDistance = max;
            src.rolloffMode = rolloff;
            src.outputAudioMixerGroup = Group(mixer, groupName);
            src.PlayOneShot(clip, volume);
            DevSoundsPlayed++;
        }

        static void ReceiveMusic(BinaryReader r, string scene)
        {
            byte what = r.ReadByte();
            string mixer = r.ReadString(), name = r.ReadString();
            float delay = r.ReadSingle(), transition = r.ReadSingle();
            bool applySnapshot = r.ReadBoolean();
            var audio = GameManager.UnsafeInstance?.AudioManager;
            if (audio == null || RoomOf(scene) != RoomOf(NetScenes.Current)) return;
            if (what == MCue)
            {
                var cue = Named(cues, name, ref cuesAt);
                if (cue == null) return;
                audio.ApplyMusicCue(cue, delay, transition, applySnapshot);
            }
            else if (what == MAtmos)
            {
                var cue = Named(atmos, name, ref atmosAt);
                if (cue == null) return;
                audio.ApplyAtmosCue(cue, transition);
            }
            else
            {
                var snapshot = SnapshotNamed(mixer, name);
                if (snapshot == null) return;
                if (what == MMusicSnapshot) audio.ApplyMusicSnapshot(snapshot, delay, transition);
                else snapshot.TransitionTo(transition);
            }
            DevMusicApplied++;
        }

        // ------------------------------------------------------------------ the loaded assets by name

        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, MusicCue> cues = new Dictionary<string, MusicCue>();
        static readonly Dictionary<string, AtmosCue> atmos = new Dictionary<string, AtmosCue>();
        static readonly Dictionary<string, AudioMixerGroup> groups = new Dictionary<string, AudioMixerGroup>();
        static float clipsAt = -10f, cuesAt = -10f, atmosAt = -10f, groupsAt = -10f;

        /// <summary>A loaded asset by its name; the table made again (two seconds apart at most) when it isn't in it.</summary>
        static T Named<T>(Dictionary<string, T> table, string name, ref float at) where T : Object
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (table.TryGetValue(name, out var found) && found != null) return found;
            if (Time.unscaledTime - at < 2f) return null;
            at = Time.unscaledTime;
            table.Clear();
            foreach (var a in Resources.FindObjectsOfTypeAll<T>())
                if (a != null && !table.ContainsKey(a.name)) table[a.name] = a;
            return table.TryGetValue(name, out found) ? found : null;
        }

        static AudioMixerGroup Group(string mixer, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string key = mixer + "/" + name;
            if (groups.TryGetValue(key, out var g) && g != null) return g;
            if (Time.unscaledTime - groupsAt < 2f) return null;
            groupsAt = Time.unscaledTime;
            groups.Clear();
            foreach (var a in Resources.FindObjectsOfTypeAll<AudioMixerGroup>())
                if (a != null && a.audioMixer != null) groups[a.audioMixer.name + "/" + a.name] = a;
            return groups.TryGetValue(key, out g) ? g : null;
        }

        static AudioMixerSnapshot SnapshotNamed(string mixer, string name)
        {
            foreach (var m in Resources.FindObjectsOfTypeAll<AudioMixer>())
                if (m != null && m.name == mixer) return m.FindSnapshot(name);
            return null;
        }

        // ------------------------------------------------------------------ the sources the others' sounds are played from

        static readonly List<(AudioSource Src, float Free)> sources = new List<(AudioSource, float)>();
        const int MaxSources = 24;

        static AudioSource FreeSource(float length)
        {
            int pick = -1;
            float earliest = float.MaxValue;
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                if (sources[i].Src == null) { sources.RemoveAt(i); continue; }
            }
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i].Free <= Time.unscaledTime) { pick = i; break; }
                if (sources[i].Free < earliest) { earliest = sources[i].Free; pick = i; }
            }
            if (pick < 0 || (sources[pick].Free > Time.unscaledTime && sources.Count < MaxSources))
            {
                var go = new GameObject("TK net sound");
                Object.DontDestroyOnLoad(go);
                var src = go.AddComponent<AudioSource>();
                src.priority = TerraMod.VoicePriority;
                src.playOnAwake = false;
                sources.Add((src, 0f));
                pick = sources.Count - 1;
            }
            var chosen = sources[pick].Src;
            sources[pick] = (chosen, Time.unscaledTime + length + 0.1f);
            return chosen;
        }
    }
}
