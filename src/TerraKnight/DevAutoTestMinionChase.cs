using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "minionchase", the user's 10-28 "现在的召唤物有点蠢，碰到那些会飞的怪以及瞬移的怪物追都追不上，但又别太强"):
    /// each minion staff's full set against one flying or teleporting enemy in Dirtmouth (a bestiary copy, its life raised
    /// so it lasts, the Target Dummy put away meanwhile), the game's way and the remade: the damage it takes in the time,
    /// the first hit, the minions' hits, and how the enemy moved (speed, teleports). TK_CHASE_STAFFS (item names),
    /// TK_CHASE_FOES (records), TK_CHASE_MODES ("0,1": game's, remade), TK_CHASE_SECONDS. Lines "[AutoTest] 召唤追击 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MinionChaseTest()
        {
            List<string> Env(string name, string fallback) =>
                (Environment.GetEnvironmentVariable(name) ?? fallback).Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            var foes = Env("TK_CHASE_FOES", "Dummy,AngryBuzzer,Spitter,SuperSpitter,ColMosquito,FlyingSentrySword,Mage,FlameBearerSmall");
            var modes = Env("TK_CHASE_MODES", "0,1").Select(int.Parse).ToList();
            // TK_CHASE_REPEAT: each mode that many times over (the measure is noisy: one minion, ten seconds).
            int repeat = int.TryParse(Environment.GetEnvironmentVariable("TK_CHASE_REPEAT"), out var rp) ? Mathf.Max(1, rp) : 1;
            modes = modes.SelectMany(x => Enumerable.Repeat(x, repeat)).ToList();
            var onlyStaffs = Env("TK_CHASE_STAFFS", "");
            float seconds = float.TryParse(Environment.GetEnvironmentVariable("TK_CHASE_SECONDS"), out var s) ? s : 10f;
            // TK_HUNT_OFF ("lead,sprint,pounce"): those parts of the remade hunting switched off for this run.
            var off = Env("TK_HUNT_OFF", "");
            Minions.DevNoLead = off.Contains("lead");
            Minions.DevNoSprint = off.Contains("sprint");
            Minions.DevNoToss = off.Contains("toss");
            var staffs = ItemDb.All.Where(d => d != null && ItemDb.Get(d.Type) == d && d.IsSummon && !d.Sentry && !d.IsWhip)
                .Where(d => onlyStaffs.Count == 0 || onlyStaffs.Contains(d.Type.ToString()))
                .OrderBy(d => d.Stage).ThenBy(d => d.Damage).ToList();
            Plugin.Log.LogInfo($"[AutoTest] 召唤追击 召唤杖 {staffs.Count} 把，敌人 {string.Join("、", foes)}，每次 {seconds} 秒");

            Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            var body = hero.GetComponent<Rigidbody2D>();
            // On the ground (the minions on foot fly back to a player in the air and walk only once he stands): 5 left of the
            // Target Dummy, Dirtmouth's flat street.
            for (float until = Time.realtimeSinceStartup + 10f; World.TargetDummy.DevAt == null && Time.realtimeSinceStartup < until;) yield return null;
            var street = World.TargetDummy.DevAt ?? (Vector2)hero.transform.position + Vector2.right * 12f;
            hero.transform.position = Clear(hero, street + Vector2.left * 5f + Vector2.up * 0.5f);
            body.linearVelocity = Vector2.zero;
            for (float until = Time.time + 3f; Time.time < until && !hero.cState.onGround;) yield return null;
            yield return new WaitForSeconds(0.3f);
            var stand = hero.transform.position;
            Plugin.Log.LogInfo($"[AutoTest] 召唤追击 主角站在 {stand}，着地 {hero.cState.onGround}");
            // The Target Dummy put away: the minions go only for the enemy under test.
            var dummies = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).Where(World.TargetDummy.Is).Select(h => h.gameObject).Where(g => g.activeSelf).ToList();
            foreach (var g in dummies) g.SetActive(false);
            var inv = mod.Inventory;
            const int slot = Inventory.HotbarSize - 1;
            var saved = inv.Slots.ToArray();
            int savedSel = inv.Selected;

            void Reset()
            {
                Minions.Instance.Clear(); Player.PlayerBuffs.Clear(); SummonTags.Clear(); EnemyDebuffs.Clear();
                TerraProjectiles.Instance.DevClear();
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    if (hm != null && hm.name.Contains("图鉴")) Object.Destroy(hm.gameObject);
            }

            try
            {
                foreach (var d in staffs)
                    foreach (var foe in foes)
                        foreach (int mode in modes)
                        {
                            Reset();
                            BladeArts.Set(mode == 1);
                            // "Dummy": the Target Dummy itself, standing still (the remade hunting may not make that any stronger).
                            bool still = foe == "Dummy";
                            var here = stand;
                            if (still)
                            {
                                foreach (var g in dummies) g.SetActive(true);
                                yield return null;
                                if (World.TargetDummy.DevAt is Vector2 da) here = Clear(hero, da + Vector2.left * 5f + Vector2.up * 0.5f);
                            }
                            hero.transform.position = here;
                            body.linearVelocity = Vector2.zero;
                            hero.FaceRight();
                            yield return new WaitForSeconds(0.3f);
                            // The full set.
                            inv.Selected = slot;
                            inv.Slots[slot] = ItemStack.Of(d.Type);
                            for (int i = 0; i < Equipment.MaxMinions; i++)
                            {
                                for (float w = Time.time + 2f; (!mod.DevReady || mod.IsUsingItem) && Time.time < w;) yield return null;
                                mod.DevUseSelected((Vector2)col.bounds.center + Vector2.up);
                                yield return null;
                            }
                            inv.Slots[slot] = default;
                            // The enemy, raised so it lasts, and roused.
                            HealthManager enemy = still ? dummies.Select(g => g.GetComponent<HealthManager>()).FirstOrDefault(h => h != null) : null;
                            if (!still) BookSpawner.Spawn(foe);
                            for (float until = Time.time + 16f; Time.time < until && enemy == null;)
                            {
                                enemy = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                                    .FirstOrDefault(h => h != null && !h.isDead && h.name.Contains("图鉴") && UI.EnemyHover.RecordOf(h) == foe);
                                yield return null;
                            }
                            if (enemy == null)
                            {
                                Plugin.Log.LogInfo($"[AutoTest] 召唤追击 {foe} 没刷出来：{BookSpawner.DevLast}");
                                continue;
                            }
                            if (!still)
                            {
                                enemy.hp = 50000;
                                World.EnemyAggro.Struck(enemy, EnemyStats.Of(enemy));
                            }
                            int hits0 = Minions.DevHits, dmg0 = Minions.DevHitDamage, sprints0 = Minions.DevSprints, tosses0 = Minions.DevTosses, blinks0 = Minions.DevBlinks, follows0 = Minions.DevFollows;
                            float start = Time.time, first = -1f, travelled = 0f;
                            int jumps = 0;
                            Vector2 last = TerraProjectiles.EnemyCentre(enemy);
                            while (Time.time - start < seconds && enemy != null && !enemy.isDead)
                            {
                                if (((Vector2)hero.transform.position - (Vector2)here).sqrMagnitude > 1f) { hero.transform.position = here; body.linearVelocity = Vector2.zero; }
                                yield return null;
                                if (enemy == null) break;
                                var at = TerraProjectiles.EnemyCentre(enemy);
                                float step = Vector2.Distance(at, last);
                                if (step > 2.5f) jumps++;
                                else travelled += step;
                                last = at;
                                if (first < 0f && Minions.DevHits > hits0) first = Time.time - start;
                            }
                            float took = Mathf.Max(0.1f, Time.time - start);
                            // The minions' own count (Terraria damage before variance): the enemy's life is in Hallownest's units.
                            int dealt = Minions.DevHitDamage - dmg0;
                            Plugin.Log.LogInfo($"[AutoTest] 召唤追击 {d.Name} ×{Minions.Instance.DevMinionCount} 对 {(still ? "训练假人" : EnemyDrops.EnglishName(foe))}（{(mode == 1 ? "新版" : "原版")}）：" +
                                $"伤害 {dealt}（每秒 {dealt / took:0}），首中 {(first < 0f ? "无" : first.ToString("0.0") + " 秒")}，命中 {Minions.DevHits - hits0} 次，" +
                                $"敌人速度 {travelled / took:0.0}/秒，瞬移 {jumps} 次（认出 {Minions.DevBlinks - blinks0}），冲刺 {Minions.DevSprints - sprints0}，对空 {Minions.DevTosses - tosses0}，闪身 {Minions.DevFollows - follows0}");
                            if (Minions.DevTossWhy.Count > 0)
                            {
                                Plugin.Log.LogInfo("[AutoTest] 召唤追击   没扔：" + string.Join("，", Minions.DevTossWhy.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
                                Minions.DevTossWhy.Clear();
                            }
                            if (still) foreach (var g in dummies) g.SetActive(false);
                        }
            }
            finally
            {
                Reset();
                BladeArts.Set(true);
                foreach (var g in dummies) if (g != null) g.SetActive(true);
                for (int i = 0; i < inv.Slots.Length && i < saved.Length; i++) inv.Slots[i] = saved[i];
                inv.Selected = savedSel;
            }
            Plugin.Log.LogInfo("[AutoTest] 召唤追击 完");
        }
    }
}
