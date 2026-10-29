using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bgmvoices", the user's 10-27 "战术霰弹枪蓄力攻击会导致bgm声音变小…好像别的枪都会"): in Dirtmouth, the guns' arts
    /// that make the most at once (the Tactical Shotgun's fragmenting rounds, the Quad Barrel's sixteen pellets, the Rocket
    /// Launcher's barrage, the Clockwork's full auto) fired at the Target Dummy, what the listener hears watched frame by frame:
    /// its peak, and how many frames it sat at the game's mixer's ceiling (1.0: its limiter turning the whole mix down, the
    /// music with it); the sounds started (DevAudioWatch: how many, the same one twice in a frame, mixer snapshots); and the
    /// music's sources Unity made virtual. Then the same with the mod's voices muted, for the music and the world alone.
    /// Lines "[AutoTest] 背景音乐 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BgmVoicesTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(3f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            BladeArts.Set(true);
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt ?? (Vector2)hero.transform.position + Vector2.right * 4f;
            var aim = at + new Vector2(0f, 0.8f);
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            var inv = mod.Inventory;
            bool savedInfinite = inv.Infinite;
            inv.Infinite = true;
            var music = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                .Where(s => s != null && s.isPlaying && s.outputAudioMixerGroup != null && s.outputAudioMixerGroup.audioMixer != null && s.outputAudioMixerGroup.audioMixer.name.Contains("Music")).ToArray();
            Plugin.Log.LogInfo($"[AutoTest] 背景音乐 音乐声源 {music.Length} 个，模组声音走 {(mod.SoundGroup != null ? mod.SoundGroup.audioMixer.name + "/" + mod.SoundGroup.name : "未定")}");
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

            // One sound at a time at a quarter volume, the music quiet: what reaches the listener against the clip's own peak.
            var musicVolume = music.Select(m => m.volume).ToArray();
            foreach (var m in music) m.volume = 0f;
            yield return new WaitForSeconds(1f);
            var gainParts = new System.Collections.Generic.List<string>();
            float quiet = 0f;
            for (int i = 0; i < 30; i++) { quiet = Mathf.Max(quiet, Peak()); yield return null; }
            foreach (var name in new[] { "Item_11", "Item_14", "Item_36", "Item_10", "Item_38", "NPC_Hit_1" })
            {
                float keep = TerraMod.SoundGain;
                TerraMod.SoundGain = 1f;
                mod.PlaySound(name, 0.25f);
                TerraMod.SoundGain = keep;
                float most = 0f;
                for (float end = Time.time + 0.8f; Time.time < end;) { most = Mathf.Max(most, Peak()); yield return null; }
                float own = TerraMod.ClipPeak(Terraria.TerrariaAssets.Sound(name)) * 0.25f;
                gainParts.Add($"{name} 自身 {own:0.00} → 听到 {most:0.00}（×{most / Mathf.Max(0.001f, own):0.0}）");
                yield return new WaitForSeconds(0.4f);
            }
            for (int i = 0; i < music.Length; i++) music[i].volume = musicVolume[i];
            Plugin.Log.LogInfo($"[AutoTest] 背景音乐 单个音效（音乐静音，底噪 {quiet:0.00}）：{string.Join("；", gainParts)}");

            var guns = new[] { ItemType.TacticalShotgun, ItemType.QuadBarrelShotgun, ItemType.RocketLauncher, ItemType.ClockworkAssaultRifle };
            // The mod's sounds at their gain as set, then lower, then muted (the music and the world alone).
            float set = TerraMod.SoundGain;
            var gains = new[] { set, 0.75f, 0f };
            int capped = 0;
            for (int pass = 0; pass < gains.Length; pass++)
            {
                TerraMod.SoundGain = Mathf.Max(0.01f, gains[pass]);
                var parts = new System.Collections.Generic.List<string>();
                foreach (var type in guns)
                {
                    var def = ItemDb.Get(type);
                    for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
                    inv.Slots[0] = ItemStack.Of(type);
                    var ammo = ItemDb.All.Where(a => a != null && ItemDb.Get(a.Type) == a && a.Ammo == def.UseAmmo).OrderBy(a => a.Damage).FirstOrDefault();
                    if (ammo != null) inv.Slots[1] = ItemStack.Of(ammo.Type, ammo.MaxStack);
                    inv.Selected = 0;
                    hero.transform.position = Clear(hero, new Vector2(at.x - 3f, at.y - bottom + 0.05f));
                    hero.FaceRight();
                    mod.DevAim = aim;
                    yield return new WaitForSeconds(0.5f);
                    void Mute() { foreach (var v in mod.DevVoices) v.mute = gains[pass] <= 0f; }
                    Mute();
                    DevAudioWatch.Begin();
                    mod.DevDuckLow = 1f;
                    BladeSkills.DevPress = true;
                    yield return new WaitForSeconds(0.3f);
                    float peak = 0f;
                    int frames = 0, top = 0, virt = 0;
                    for (float end = Time.time + 2.5f; Time.time < end;)
                    {
                        if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aim);
                        Mute();
                        DevAudioWatch.Tick();
                        float pk = Peak();
                        peak = Mathf.Max(peak, pk);
                        frames++;
                        if (pk >= 0.99f) top++;
                        virt = Mathf.Max(virt, music.Count(s => s != null && s.isVirtual));
                        yield return null;
                    }
                    mod.DevAim = null;
                    string heard = DevAudioWatch.Report(6);
                    DevAudioWatch.On = false;
                    if (pass == 0) capped = Mathf.Max(capped, top);
                    parts.Add($"{def.Name} 峰值 {peak:0.00}，顶到上限 {top}/{frames} 帧，模组声音最低压到 ×{mod.DevDuckLow:0.00}，音乐被静音 {virt} 个" + (pass == 0 ? $"（{heard}）" : ""));
                    yield return new WaitForSeconds(2f);
                    TerraProjectiles.Instance?.Clear();
                }
                Plugin.Log.LogInfo($"[AutoTest] 背景音乐 {(gains[pass] <= 0f ? "模组静音（对照）" : $"开枪，模组音量 ×{gains[pass]:0.##}")}：{string.Join("；", parts)}");
            }
            foreach (var v in mod.DevVoices) v.mute = false;
            TerraMod.SoundGain = set;
            inv.Infinite = savedInfinite;
            Plugin.Log.LogInfo($"[AutoTest] 背景音乐 开枪时最多 {capped} 帧顶到上限：{(capped <= 2 ? "通过" : "不通过")}");
        }
    }
}
