using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "roomsfx", the user's 10-29 "部分房间只有环境音效没有武器音效"): rooms whose own sounds are loud (rain, waterfalls,
    /// machinery, crowds of ambient sources) against quiet Dirtmouth. In each: what the listener hears with none of the mod's
    /// sounds playing, how many of the game's sources play and how many Unity made virtual; then a gunshot, a swing, an
    /// explosion and a repeating gun's shot, each alone: the voice's volume (its onset fitting × the duck), whether Unity made
    /// it virtual, and the peak heard while it plays. Lines "[AutoTest] 房间音效 …"; passes when no weapon sound goes virtual
    /// and every room's sounds play at least 60% as loud as in Dirtmouth.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator RoomSfxTest()
        {
            var rooms = new[]
            {
                ("Town", "left1"), ("Crossroads_50", "left1"), ("Ruins1_03", "left1"), ("Ruins1_05", "right1"), ("Fungus1_01", "left1"),
                ("Fungus2_14", "right1"), ("Fungus3_26", "left1"), ("Mines_02", "left1"), ("Mines_05", "left1"), ("Deepnest_01", "left1"),
                ("Deepnest_East_03", "left1"), ("Abyss_04", "left1"), ("Hive_01", "left1"), ("RestingGrounds_05", "left1"), ("Cliffs_02", "left1"),
            };
            var config = AudioSettings.GetConfiguration();
            Plugin.Log.LogInfo($"[AutoTest] 房间音效 真实声道 {config.numRealVoices}，虚拟声道 {config.numVirtualVoices}");
            var wave = new float[1024];
            float Peak()
            {
                float m = 0f;
                for (int ch = 0; ch < 2; ch++)
                {
                    AudioListener.GetOutputData(wave, ch);
                    for (int i = 0; i < wave.Length; i++) m = Mathf.Max(m, Mathf.Abs(wave[i]));
                }
                return m;
            }
            float town = -1f, worst = float.MaxValue;
            string worstRoom = "";
            int virtualShots = 0;
            foreach (var (scene, gate) in rooms)
            {
                yield return GoTo(scene, gate);
                if (GameManager.instance.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] 房间音效 {scene} 没进去，跳过"); continue; }
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(1.5f);
                float game = 0f;
                for (int i = 0; i < 60; i++) { game = Mathf.Max(game, Peak()); yield return null; }
                var sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(s => s != null && s.isPlaying && !mod.DevVoices.Contains(s)).ToArray();
                int gameVirtual = sources.Count(s => s.isVirtual);
                // The game's sources by mixer and priority.
                string kinds = string.Join("，", sources.GroupBy(s => (s.outputAudioMixerGroup != null ? s.outputAudioMixerGroup.audioMixer.name : "无") + " 优先级 " + s.priority)
                    .OrderBy(g => g.Key).Select(g => g.Key + " ×" + g.Count()));
                Plugin.Log.LogInfo($"[AutoTest] 房间音效 {scene} 游戏声源：{kinds}");
                // The mod's voices at the 5.2.0 priority (200), then as set now.
                int now = TerraMod.VoicePriority;
                foreach (int priority in new[] { 200, now })
                {
                    TerraMod.VoicePriority = priority;
                    foreach (var v in mod.DevVoices) v.priority = priority;
                    var parts = new List<string>();
                    float sum = 0f;
                    int musicGone = 0;
                    foreach (var name in new[] { "Item_11", "Item_1", "Item_14", "Item_40" })
                    {
                        mod.PlaySound(name);
                        var voice = mod.DevLastVoice;
                        if (voice != null) voice.priority = priority;
                        float volume = voice != null ? voice.volume : 0f;
                        float most = 0f;
                        // Silent: virtual through most of its first 0.4 s (Unity may start a voice virtual and make it real an update later).
                        int frames = 0, virtualFrames = 0;
                        for (float end = Time.time + 0.4f; Time.time < end;)
                        {
                            frames++;
                            if (voice != null && voice.isPlaying && voice.isVirtual) virtualFrames++;
                            most = Mathf.Max(most, Peak());
                            musicGone = Mathf.Max(musicGone, sources.Count(s => s != null && s.isVirtual && s.outputAudioMixerGroup != null && s.outputAudioMixerGroup.audioMixer.name.Contains("Music")));
                            yield return null;
                        }
                        bool gone = virtualFrames * 2 > frames;
                        if (gone && priority == now) virtualShots++;
                        sum += gone ? 0f : volume;
                        parts.Add($"{name} 音量 ×{volume:0.00}（拟合 ×{mod.DevLevel(voice):0.00}，压低 ×{mod.DevDuck:0.00}）{(gone ? "，被静音" : virtualFrames > 0 ? $"，静音 {virtualFrames}/{frames} 帧" : "")}，听到 {most:0.00}");
                        yield return new WaitForSeconds(0.6f);
                    }
                    float mean = sum / 4f;
                    if (priority == now)
                    {
                        if (scene == "Town") town = mean;
                        else if (mean < worst) { worst = mean; worstRoom = scene; }
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 房间音效 {scene}（模组优先级 {priority}）：环境 {game:0.00}（模组记的 {mod.DevGameLevel:0.00}），在放的声源 {sources.Length} 个（被静音 {gameVirtual}，音乐被静音 {musicGone}），武器声平均 ×{mean:0.00}；{string.Join("；", parts)}");
                }
                TerraMod.VoicePriority = now;
                foreach (var v in mod.DevVoices) v.priority = now;
            }
            bool pass = virtualShots == 0 && town > 0f && worst >= 0.6f * town;
            Plugin.Log.LogInfo($"[AutoTest] 房间音效 德特茅斯 ×{town:0.00}，最低 {worstRoom} ×{worst:0.00}，被静音 {virtualShots} 次：{(pass ? "通过" : "不通过")}");
        }
    }
}
