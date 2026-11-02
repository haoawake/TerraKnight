using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "dotdeath", the user's 11-01 "怪物死亡的时候应该把中毒等负面buff清除，不然像格林亲族这种着火后会一直不死直到负面效果解除"):
    /// an enemy (TK_DOT_KINDS, the Grimmkin Novice and a Crawlid by default) brought by the book beside the Knight, set on fire and
    /// poisoned for 10 s, its life brought down to one point; then how long it takes, from its life at nothing, to be gone (its
    /// object off or destroyed, or marked dead), how often its script's state went back to an earlier one, and whether its debuffs
    /// are still on it. As now, then as before 11-01 (EnemyDebuffs.DevKeepOnDeath). Lines "[AutoTest] 死亡清负面 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DotDeathTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 死亡清负面 {what} {(pass ? "通过" : "不通过")}");
            }
            var kinds = (Environment.GetEnvironmentVariable("TK_DOT_KINDS") ?? "FlameBearerSmall,Crawler").Split(',');
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            var specialDeath = AccessTools.Field(typeof(HealthManager), "hasSpecialDeath");
            const string room = "Crossroads_01";
            try
            {
                foreach (var key in kinds)
                {
                    foreach (bool old in new[] { false, true })
                    {
                        EnemyDebuffs.DevKeepOnDeath = old;
                        yield return GoTo(room, EntryGateOf(room));
                        yield return new WaitForSeconds(1f);
                        PlayerData.instance.isInvincible = true;
                        BookSpawner.Spawn(key);
                        HealthManager hm = null;
                        for (float until = Time.time + 16f; Time.time < until && hm == null;)
                        {
                            hm = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                                .FirstOrDefault(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && h.name.Contains("图鉴") && UI.EnemyHover.RecordOf(h) == key);
                            yield return null;
                        }
                        string what = (old ? "旧" : "新") + " " + key;
                        if (hm == null) { Check(false, $"{what} 没生成（{BookSpawner.DevLast}）"); continue; }
                        yield return new WaitForSeconds(1.5f);
                        if (hm == null || hm.isDead) { Check(false, $"{what} 生成后就没了"); continue; }
                        var go = hm.gameObject;
                        var fsm = go.GetComponents<PlayMakerFSM>().OrderByDescending(f => f.FsmStates != null ? f.FsmStates.Length : 0).FirstOrDefault();
                        bool special = specialDeath != null && (bool)specialDeath.GetValue(hm);
                        EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 600);
                        EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, 600);
                        // Six seconds of Potent Acid (90 a second) to take its last point (it hides off the map between its
                        // appearances, where nothing burns it) while the fire still has seconds to go.
                        EnemyDebuffs.Add(hm, EnemyDebuffs.PotentAcid, 360);
                        int dies0 = EnemyDebuffs.DevDieCalls, diesAtZero = -1;
                        hm.hp = 1;
                        int cleared0 = EnemyDebuffs.DevClearedOnDeath;
                        float burnt = Time.time, zeroAt = -1f, goneAt = -1f;
                        var trail = new List<string>();
                        var seen = new HashSet<string>();
                        int backs = 0;
                        string last = fsm != null ? fsm.ActiveStateName : null;
                        bool fireAfterDeath = false;
                        int refused0 = EnemyDebuffs.DevRefused, held0 = EnemyDebuffs.DevHeldOff;
                        var life = new List<string>();
                        float nextLife = Time.time;
                        for (float until = Time.time + 14f; Time.time < until;)
                        {
                            PlayerData.instance.isInvincible = true;
                            mod.Stats.FullHeal();
                            if (go == null || !go.activeInHierarchy || hm == null || hm.isDead)
                            {
                                goneAt = Time.time;
                                if (zeroAt < 0f) zeroAt = goneAt;
                                break;
                            }
                            if (Time.time >= nextLife && life.Count < 16)
                            {
                                nextLife = Time.time + 1f;
                                life.Add($"{Time.time - burnt:F0}s 血{hm.hp} 火{EnemyDebuffs.Has(hm, EnemyDebuffs.OnFire)} 无敌{hm.IsInvincible} 在{(Vector2)hm.transform.position:F1}"
                                    + $" 图外{World.ArenaGuard.OffMap(hm)} 场外{World.ArenaGuard.Outside(hm)} 睡{BossWake.Asleep(hm)} 碰不到{EnemyStats.Untouchable(hm)}");
                            }
                            if (zeroAt < 0f && hm.hp <= 0) { zeroAt = Time.time; diesAtZero = EnemyDebuffs.DevDieCalls; }
                            if (zeroAt >= 0f && Time.time - zeroAt > 0.5f && EnemyDebuffs.Has(hm, EnemyDebuffs.OnFire)) fireAfterDeath = true;
                            string s = fsm != null ? fsm.ActiveStateName : null;
                            if (s != last)
                            {
                                if (zeroAt >= 0f && seen.Contains(s)) backs++;
                                if (zeroAt >= 0f) seen.Add(s);
                                if (trail.Count < 24) trail.Add($"{Time.time - burnt:F1}s {s}");
                                last = s;
                            }
                            yield return null;
                        }
                        float tookAfterZero = zeroAt >= 0f ? (goneAt >= 0f ? goneAt : Time.time) - zeroAt : -1f;
                        // Die calls once its life was at nothing (the first is its death; each more began it again).
                        int redies = diesAtZero >= 0 ? EnemyDebuffs.DevDieCalls - diesAtZero : 0;
                        Plugin.Log.LogInfo($"[AutoTest] 死亡清负面 {what}：特殊死亡 {special}，点火后 {(zeroAt >= 0f ? $"{zeroAt - burnt:F1}" : "-")} 秒血到 0，" +
                            $"再过 {tookAfterZero:F1} 秒{(goneAt >= 0f ? "消失" : "仍在")}，之后又死 {redies} 次（共 {EnemyDebuffs.DevDieCalls - dies0}），死亡状态重来 {backs} 次，死后半秒仍着火 {fireAfterDeath}，死亡时清掉 {EnemyDebuffs.DevClearedOnDeath - cleared0} 个，拒加 {EnemyDebuffs.DevRefused - refused0}、扣血被挡 {EnemyDebuffs.DevHeldOff - held0}；血量 [{string.Join("；", life)}]；状态 [{string.Join("、", trail)}]");
                        if (!old) Check(zeroAt >= 0f && goneAt >= 0f && !fireAfterDeath && redies == 0,
                            $"{what} 血到 0 后 {tookAfterZero:F1} 秒走完死亡，没再被负面效果打死，负面效果清除");
                        else Plugin.Log.LogInfo($"[AutoTest] 死亡清负面 {what} {(redies > 0 || fireAfterDeath ? $"死后仍着火、又被打死 {redies} 次（复现）" : "照样死掉")}");
                        if (go != null && go.activeInHierarchy) Object.Destroy(go);
                        yield return new WaitForSeconds(0.5f);
                    }
                }
            }
            finally
            {
                EnemyDebuffs.DevKeepOnDeath = false;
                Player.TerraStats.DevImmortal = false;
                Plugin.Log.LogInfo($"[AutoTest] 死亡清负面 合计 {ok}/{all}");
            }
        }
    }
}
