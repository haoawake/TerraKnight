using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "dreamfk"): the Failed Champion's dream reached by its seal and won (every part in play struck down with the
    /// Terraria blow, again and again: armour, head, armour); after the win, a timeline of the scene, the transition, the
    /// camera's fade, the player's control and any exception, until the player is back with control or a minute is up.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DreamFkTest()
        {
            var errors = new List<string>();
            void OnLog(string msg, string stack, LogType type)
            {
                if ((type == LogType.Exception || type == LogType.Error) && errors.Count < 20)
                    errors.Add((msg.Length > 200 ? msg.Substring(0, 200) : msg) + " @ " + (stack != null && stack.Length > 200 ? stack.Substring(0, 200) : stack));
            }
            Application.logMessageReceived += OnLog;
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            try
            {
                yield return SealTravel("Town", "left1");
                PlayerData.instance.isInvincible = true;
                PlayerData.instance.hasDreamNail = true;
                var s = BossSummons.Find(ItemType.BossSigilFailedChampion);
                Bestiary.Kills[s.Key] = 1;
                int stage = Shop.DevStage;
                Shop.DevStage = s.Stage;
                inv.Slots[0] = ItemStack.Of(s.Type, 2);
                inv.Selected = 0;
                inv.Override = -1;
                mod.DevUseSelected(HeroController.instance.transform.position);
                float t0 = Time.realtimeSinceStartup;
                while (!BossArena.Arrived(s.Key) && Time.realtimeSinceStartup - t0 < 30f) yield return null;
                var gm = GameManager.instance;
                string dream = gm.sceneName;
                Plugin.Log.LogInfo($"[AutoTest] 梦冠军 进入 {dream}（{Time.realtimeSinceStartup - t0:F1} 秒）");
                int kills0 = Bestiary.KillsOf(s.Key);
                var timeline = new List<string>();
                string last = null;
                float nextHit = 0f, wonAt = -1f, shotAt = -1f;
                int shots = 0;
                while (Time.realtimeSinceStartup - t0 < 120f)
                {
                    var hero = HeroController.SilentInstance;
                    var fade = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
                    string st = $"{gm.sceneName} 转场={gm.IsInSceneTransition} 淡入淡出={(fade != null ? fade.ActiveStateName : "?")} 可操作={GameHooks.PlayerHasControl()} 状态={gm.gameState} 时间={Time.timeScale:F1} 击杀={Bestiary.KillsOf(s.Key) - kills0}";
                    if (st != last) { timeline.Add($"{Time.realtimeSinceStartup - t0:F1}s {st}"); last = st; }
                    mod.Stats.FullHeal();
                    if (wonAt < 0f && Bestiary.KillsOf(s.Key) > kills0) { wonAt = Time.realtimeSinceStartup; timeline.Add($"{wonAt - t0:F1}s 首领倒下"); }
                    if (wonAt < 0f && gm.sceneName == dream && Time.realtimeSinceStartup >= nextHit)
                    {
                        nextHit = Time.realtimeSinceStartup + 0.4f;
                        if (Mathf.Repeat(Time.realtimeSinceStartup - t0, 3f) < 0.4f)
                        {
                            var parts = new System.Text.StringBuilder();
                            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                                if (hm != null && hm.gameObject.scene.name.StartsWith(dream)) parts.Append($" [{hm.name} hp{hm.hp} 死{hm.isDead} 无敌{hm.IsInvincible} {(Vector2)hm.transform.position:F1}]");
                            timeline.Add($"{Time.realtimeSinceStartup - t0:F1}s 部位{parts}");
                        }
                        foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                            if (hm != null && !hm.isDead && hm.hp > 0 && hm.gameObject.scene.name.StartsWith(dream)) Kill(hm);
                    }
                    // After the win: photographs of what the player sees, and done once back with control.
                    if (wonAt > 0f)
                    {
                        if (shots < 4 && Time.realtimeSinceStartup - (shotAt < 0f ? wonAt : shotAt) > 4f)
                        {
                            shotAt = Time.realtimeSinceStartup;
                            Shot(gm.sceneName, "dreamfk_after_" + (++shots));
                        }
                        if (gm.sceneName != dream && !gm.IsInSceneTransition && GameHooks.PlayerHasControl() && Time.realtimeSinceStartup - wonAt > 3f) break;
                        if (Time.realtimeSinceStartup - wonAt > 60f) break;
                    }
                    yield return null;
                }
                Plugin.Log.LogInfo("[AutoTest] 梦冠军 时间线：" + string.Join(" | ", timeline));
                Plugin.Log.LogInfo($"[AutoTest] 梦冠军 报错 {errors.Count} 条：" + string.Join(" || ", errors));
                bool back = gm.sceneName != dream && GameHooks.PlayerHasControl();
                Plugin.Log.LogInfo($"[AutoTest] 梦冠军 打完回到 {gm.sceneName} 可操作 {GameHooks.PlayerHasControl()} {(wonAt > 0f && back ? "通过" : "不通过")}");
                Shop.DevStage = stage;
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                System.Array.Copy(saved, inv.Slots, saved.Length);
            }
        }
    }
}
