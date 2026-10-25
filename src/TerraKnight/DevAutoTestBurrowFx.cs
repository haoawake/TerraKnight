using System;
using System.Collections;
using System.Linq;
using TerraKnight.EnemyAi.Bosses;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "burrowfx"): the burrowing bosses with the new AI (the Dung Defender, the Massive Moss Charger, the White
    /// Defender; TK_BURROW_KEYS picks others), the player standing on the floor near them: every 0.6 s the boss's state,
    /// where it is, the ground under it, and each of its effect objects shown (a burrow trail, dirt), with a screenshot, to
    /// see whether the burrow's effects sit on the ground.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BurrowFxTest()
        {
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = Environment.GetEnvironmentVariable("TK_BURROW_VANILLA") == "1" ? World.EnemyAiKind.Vanilla : World.EnemyAiKind.New;
            string pick = Environment.GetEnvironmentVariable("TK_BURROW_KEYS");
            var keys = string.IsNullOrWhiteSpace(pick) ? new[] { "DungDefender", "MegaMossCharger", "WhiteDefender" } : pick.Split(';');
            foreach (var key in keys)
            {
                int started = BossSummonStart.DevStarted;
                // As the room loads (before its scripts start): each burrow effect, whether it is up, where it sits on its boss.
                UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene, UnityEngine.SceneManagement.LoadSceneMode> loaded = (sc, mode) =>
                {
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (f.FsmName != "Keep Y" || f.gameObject.scene != sc) continue;
                        var par = f.transform.parent;
                        var boss = f.GetComponentInParent<HealthManager>(true);
                        Plugin.Log.LogInfo($"[AutoTest] 钻地特效 {key} 载入时：{f.gameObject.name} 自身开着 {f.gameObject.activeSelf}、场景里显示 {f.gameObject.activeInHierarchy}，位置 ({f.transform.position.x:F2},{f.transform.position.y:F2}) 相对父物体 {f.transform.localPosition}；"
                            + $"父物体 {par?.name} ({par?.position.x:F2},{par?.position.y:F2})，首领 {boss?.name} 开着 {boss?.gameObject.activeInHierarchy} ({boss?.transform.position.x:F2},{boss?.transform.position.y:F2})");
                    }
                };
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += loaded;
                BossArena.Fight(key, true);
                float until = Time.realtimeSinceStartup + 40f;
                // Each burrow effect's height is fixed the frame it lets go of the boss (Keep Y: Pause, then Init): where the
                // boss stood then, how fast it was going, and the ground under it, against where the effect stays.
                var holders = new System.Collections.Generic.Dictionary<PlayMakerFSM, (Vector3 boss, Vector2 vel, string state)>();
                var fixedOnes = new System.Collections.Generic.HashSet<PlayMakerFSM>();
                float watchUntil = float.MaxValue;
                while (Time.realtimeSinceStartup < until && Time.realtimeSinceStartup < watchUntil)
                {
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                    {
                        if (f.FsmName != "Keep Y" || fixedOnes.Contains(f)) continue;
                        var parent = f.transform.parent;
                        if (f.ActiveStateName == "Pause" || parent != null && f.ActiveStateName != "Init")
                        {
                            if (parent != null)
                            {
                                var prb = parent.GetComponentInParent<Rigidbody2D>();
                                var pfsm = parent.GetComponentInParent<HealthManager>()?.GetComponents<PlayMakerFSM>().FirstOrDefault(p => p.FsmName != "Keep Y" && p.FsmName != "hp_scaler" && p.FsmName != "Stun");
                                holders[f] = (parent.position, prb != null ? prb.linearVelocity : Vector2.zero, pfsm?.ActiveStateName);
                            }
                            continue;
                        }
                        fixedOnes.Add(f);
                        holders.TryGetValue(f, out var h);
                        var floor = EnemyAi.Hostiles.NativeRay(new Vector2(f.transform.position.x, h.boss.y + 1f), Vector2.down, 30f);
                        Plugin.Log.LogInfo($"[AutoTest] 钻地特效 {key} 定高：{f.gameObject.name} 停在 y {f.transform.position.y:F2}（{GameManager.instance.sceneName}，{Time.realtimeSinceStartup - (until - 40f):F1}s）；"
                            + $"那一刻首领 ({h.boss.x:F2},{h.boss.y:F2}) 速度 {h.vel} 状态 {h.state}，它脚下地面 {(floor.collider != null ? floor.point.y : float.NaN):F2}（{floor.collider?.name}）");
                    }
                    if (BossArena.Arrived(key) && BossSummonStart.DevStarted != started && watchUntil == float.MaxValue) watchUntil = Time.realtimeSinceStartup + 2f;
                    yield return null;
                }
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= loaded;
                BossKit kit = null;
                for (float t = 0f; t < 10f && kit == null; t += 0.5f)
                {
                    kit = KitOf(key.Split('@')[0]);
                    if (kit == null) yield return new WaitForSeconds(0.5f);
                }
                var hero = HeroController.instance;
                var scene = GameManager.instance.sceneName;
                if (kit == null)
                {
                    // The game's own AI (no kit): the boss's body and the burrow effects set loose from it, logged as they go.
                    var hm = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(h => EnemyStats.Of(h)?.Record == key.Split('@')[0]);
                    for (int k = 0; k < 30 && hm != null; k++)
                    {
                        var ctl = hm.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Dung Defender" || f.FsmName == "Mossy Control");
                        var hit = EnemyAi.Hostiles.NativeRay((Vector2)hm.transform.position + Vector2.up * 0.5f, Vector2.down, 30f);
                        var loose0 = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)
                            .Where(f => f.FsmName == "Keep Y" && f.gameObject.activeInHierarchy)
                            .Select(f => $"{f.gameObject.name}@({f.transform.position.x:F1},{f.transform.position.y:F1})");
                        Plugin.Log.LogInfo($"[AutoTest] 钻地特效 {key}（原版AI）{k * 0.6f:F1}s 状态 {ctl?.ActiveStateName} 首领 ({hm.transform.position.x:F1},{hm.transform.position.y:F1}) 脚下地面 {(hit.collider != null ? hit.point.y : float.NaN):F1} 跟随特效 [{string.Join(" ", loose0)}]");
                        if (k % 2 == 0) Shot(scene, $"burrow_vanilla_{key}_{k:00}");
                        yield return new WaitForSeconds(0.6f);
                    }
                    continue;
                }
                for (int k = 0; k < 30; k++)
                {
                    // The player on the floor a few tiles from the boss, as one fighting it.
                    if (k % 5 == 0 && kit != null && kit.Hm != null)
                    {
                        var at = kit.ClampToArena(kit.Me, 3f);
                        at.x += at.x - 5f > kit.Arena.xMin + 1f ? -5f : 5f;
                        at.y = kit.GroundBelow(new Vector2(at.x, kit.Arena.center.y)) + 1.2f;
                        hero.transform.position = new Vector3(at.x, at.y, hero.transform.position.z);
                    }
                    if (kit == null || kit.Hm == null) break;
                    float ground = kit.GroundBelow(new Vector2(kit.Me.x, kit.Me.y + 0.5f));
                    var shown = kit.GetComponentsInChildren<Transform>(true)
                        .Where(t => t != kit.transform && t.gameObject.activeInHierarchy && (t.GetComponent<ParticleSystem>() != null || t.GetComponent<tk2dSprite>() != null && t.GetComponent<tk2dSprite>().color.a > 0.05f))
                        .Select(t => $"{t.name}@{t.position.y:F1}").Take(8);
                    // The effects the burrow sets loose from the boss (Keep Y: they leave it at the scene's start).
                    var loose = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)
                        .Where(f => f.FsmName == "Keep Y" && f.gameObject.activeInHierarchy)
                        .Select(f => $"{f.gameObject.name}@({f.transform.position.x:F1},{f.transform.position.y:F1})");
                    Plugin.Log.LogInfo($"[AutoTest] 钻地特效 {key} {k * 0.6f:F1}s 状态 {kit.StateNow} 首领 ({kit.Me.x:F1},{kit.Me.y:F1}) 脚下地面 {ground:F1} 跟随特效 [{string.Join(" ", loose)}] 自带 [{string.Join(" ", shown)}]");
                    if (k % 2 == 0) Shot(scene, $"burrow_{key}_{k:00}");
                    if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                    yield return new WaitForSeconds(0.6f);
                }
            }
            Plugin.Log.LogInfo("[AutoTest] 钻地特效 完成");
        }
    }
}
