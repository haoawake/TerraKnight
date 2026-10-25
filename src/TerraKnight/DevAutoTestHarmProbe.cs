using System;
using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "harmprobe"): the user's "白色防御者那个地刺……它扔出来的球能打我80，地刺只有1" (10-25). A boss's fight
    /// (TK_HARM_BOSSES, ';', WhiteDefender by default) entered as a rematch, and after a few seconds every thing in the loaded
    /// scenes and the pool that can hurt the player (DamageHero, the room's own and spawned): its masks, hazard type, where it
    /// sits, and the Terraria damage a touch of it comes to (PlayerHooks.RawContact, before defense).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HarmProbeTest()
        {
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            // The new AI, as the user plays (TK_HARM_AI=vanilla: the game's own).
            World.EnemyAiMode.Current = Environment.GetEnvironmentVariable("TK_HARM_AI") == "vanilla" ? World.EnemyAiKind.Vanilla : World.EnemyAiKind.New;
            var keys = (Environment.GetEnvironmentVariable("TK_HARM_BOSSES") ?? "WhiteDefender").Split(';');
            foreach (var key in keys.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                PlayerData.instance.isInvincible = true;
                if (!BossArena.Has(key)) { Plugin.Log.LogInfo($"[AutoTest] 伤害来源 {key}：没有场地"); continue; }
                BossArena.Fight(key, true);
                float until = Time.realtimeSinceStartup + 40f;
                yield return new WaitForSeconds(1f);
                while (GameManager.instance.IsInSceneTransition && Time.realtimeSinceStartup < until) yield return null;
                yield return new WaitForSeconds(8f);
                var harms = Object.FindObjectsByType<DamageHero>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Plugin.Log.LogInfo($"[AutoTest] 伤害来源 {key}（{GameManager.instance.sceneName}）：{harms.Length} 个");
                foreach (var g in harms.GroupBy(h => (Name: h.name.Replace("(Clone)", "").Trim(), h.damageDealt, h.hazardType)).OrderBy(g => g.Key.Name))
                {
                    var h = g.First();
                    int raw = Player.PlayerHooks.RawContact(Mathf.Max(1, h.damageDealt), h.gameObject);
                    var hm = h.GetComponentInParent<HealthManager>();
                    Plugin.Log.LogInfo($"[AutoTest] 伤害来源   {g.Key.Name} ×{g.Count()} 面具 {h.damageDealt} 类型 {h.hazardType} 场景 {h.gameObject.scene.name} 在 {TentPath(h.transform)} 上级血条 {(hm != null ? hm.name + "/" + (EnemyStats.Of(hm)?.Record ?? "无记录") : "无")} → 原始伤害 {raw}");
                }
                // TK_HARM_SECONDS: then that long standing in the fight in the user's gear (Solar Flare, their accessories), each hit logged ([受伤]).
                float seconds = float.TryParse(Environment.GetEnvironmentVariable("TK_HARM_SECONDS"), out var sec) ? sec : 0f;
                if (seconds <= 0f) continue;
                var inv = mod.Inventory;
                inv.Armor[0] = ItemStack.Of(ItemType.SolarFlareHelmet);
                inv.Armor[1] = ItemStack.Of(ItemType.SolarFlareBreastplate);
                inv.Armor[2] = ItemStack.Of(ItemType.SolarFlareLeggings);
                var gear = new[] { ItemType.FireGauntlet, ItemType.BerserkerGlove, ItemType.CelestialShell, ItemType.WarriorEmblem, ItemType.HeroShield, ItemType.TerrasparkBoots };
                for (int i = 0; i < gear.Length && i < inv.Accessories.Length; i++) inv.Accessories[i] = ItemStack.Of(gear[i]);
                yield return new WaitForSeconds(0.5f);
                var boss = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(x => x != null && !x.isDead && x.gameObject.activeInHierarchy && (EnemyStats.Of(x)?.Boss ?? false));
                Plugin.Log.LogInfo($"[AutoTest] 伤害来源 {key} 实战 {seconds} 秒：防御 {mod.Stats.Defense}，难度 {World.GameDifficulty.Current}，首领 {boss?.name}");
                if (boss != null && !BossWake.Rouse(boss)) FSMUtility.SendEventToGameObject(boss.gameObject, "TAKE DAMAGE");
                PlayerData.instance.isInvincible = false;
                // TK_HARM_SHOTS=1: a picture each second and a half, and what can hurt is up then (its name, where, its collider on).
                bool pictures = Environment.GetEnvironmentVariable("TK_HARM_SHOTS") == "1";
                float nextShot = 0f;
                int shot = 0;
                // TK_HARM_HP: its life held at that share of its own (its later parts), else kept from dying.
                float share = float.TryParse(Environment.GetEnvironmentVariable("TK_HARM_HP"), out var hs) ? hs : 0f;
                EnemyStats.Info bossInfo = null;
                bool lowered = false;
                float nextLook = 0f;
                for (float end = Time.time + seconds; Time.time < end;)
                {
                    // The boss as its fight brings it out (the Hollow Knight still chained when the room loads), its life then
                    // brought down to the share asked for, once.
                    if ((boss == null || !boss.gameObject.activeInHierarchy) && Time.time >= nextLook)
                    {
                        nextLook = Time.time + 0.5f;
                        boss = KitOf(key.Split('@')[0])?.Hm ?? boss;
                    }
                    if (!lowered && share > 0f && boss != null && boss.gameObject.activeInHierarchy && (bossInfo = EnemyStats.Of(boss)) != null)
                    {
                        lowered = true;
                        boss.hp = Mathf.RoundToInt(bossInfo.MaxHp * share);
                        boss.Hit(new HitInstance { Source = HeroController.instance.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 1, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                        Plugin.Log.LogInfo($"[AutoTest] 伤害来源 {key}：首领 {boss.name} 生命压到 {boss.hp}/{bossInfo.MaxHp}");
                    }
                    if (boss != null && !boss.isDead) boss.hp = lowered ? Mathf.Max(boss.hp, Mathf.RoundToInt(bossInfo.MaxHp * share) - 50) : Mathf.Max(boss.hp, 3000);
                    if (pictures && Time.time >= nextShot)
                    {
                        nextShot = Time.time + 1.5f;
                        var up = Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None)
                            .Where(h => h != null && h.gameObject.activeInHierarchy && h.transform.root.name != boss?.transform.root.name)
                            .Select(h => $"{h.name.Replace("(Clone)", "")}@({h.transform.position.x:F1},{h.transform.position.y:F1}){(h.GetComponent<Collider2D>() is Collider2D c && c.enabled ? "" : "无碰撞")}");
                        Plugin.Log.LogInfo($"[AutoTest] 伤害来源 画面 {shot:00} 首领 {boss?.GetComponent<EnemyAi.Bosses.BossKit>()?.StateNow} 在 ({boss?.transform.position.x:F1},{boss?.transform.position.y:F1})：{string.Join("、", up.Take(16))}");
                        // The music then: the cue the game's AudioManager is on, and each of its music channels.
                        var music = GameObject.Find("_GameManager/AudioManager/Music");
                        string channels = music == null ? "无" : string.Join("、", music.GetComponentsInChildren<AudioSource>(true)
                            .Select(a => $"{a.name}:{(a.clip != null ? a.clip.name : "-")}{(a.isPlaying ? "" : "(停)")} 音量 {a.volume:F2}"));
                        Plugin.Log.LogInfo($"[AutoTest] 伤害来源 音乐 {shot:00} 曲目 {GameManager.instance.AudioManager?.CurrentMusicCue?.name ?? "无"}：{channels}");
                        Shot(GameManager.instance.sceneName, $"harm_{key}_{shot++:00}");
                    }
                    yield return null;
                }
                PlayerData.instance.isInvincible = true;
            }
            PlayerData.instance.isInvincible = false;
        }
    }
}
