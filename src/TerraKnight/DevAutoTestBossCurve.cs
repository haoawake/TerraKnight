using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossCurveTest(bool baseline)
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            var difficultyWas = GameDifficulty.Current;
            bool invincibleWas = PlayerData.instance.isInvincible;
            bool noFallWas = mod.DevNoFallDamage;
            var rows = new List<object>();
            int checks = 0, failures = 0;
            void Check(string name, bool pass)
            {
                checks++; if (!pass) failures++;
                Plugin.Log.LogInfo("[AutoTest] Boss曲线/" + name + (pass ? " 通过" : " 不通过"));
            }
            int Whole(BossStats.Ref r, string scene, DifficultyLevel difficulty)
            {
                var tune = BossBalance.Of(r.Record, EnemyDrops.VariantLevel(scene));
                return Mathf.CeilToInt(BossStats.LifeOf(r, scene, difficulty) * tune.Life * BossPhases.WholeFactor(r.Record, scene));
            }
            try
            {
                foreach (var pair in BossStats.DevAll)
                {
                    var r = pair.Value;
                    string scene = EnemyDrops.SceneOf(pair.Key);
                    int stage = ProgressionBalance.BossRewards.TryGetValue(pair.Key, out var reward) ? reward.Stage : r.Stage;
                    int prev = 0;
                    foreach (DifficultyLevel difficulty in Enum.GetValues(typeof(DifficultyLevel)))
                    {
                        GameDifficulty.Current = difficulty;
                        int life = Whole(r, scene, difficulty);
                        var tune = BossBalance.Of(r.Record, EnemyDrops.VariantLevel(scene));
                        int defense = EnemyStats.BossDefense(r, tune, scene);
                        rows.Add(new { Key = pair.Key, Name = Bestiary.NameOf(pair.Key), Stage = stage, Scene = scene,
                            Difficulty = difficulty.ToString(), WholeLife = life, Defense = defense,
                            DamageFactor = Areas.BossDamage * tune.Damage,
                            WholePhaseFactor = BossPhases.WholeFactor(r.Record, scene) });
                        if (!baseline)
                        {
                            Check("难度递增/" + pair.Key + "/" + difficulty, life >= prev);
                            Check("整场预算覆盖/" + pair.Key, BossHealthBudgets.TryTotal(r, scene, difficulty, out float total));
                            if (!BossStats.InGodhome(scene))
                                Check("血池分配后回到整场预算/" + pair.Key, Math.Abs(life - total) <= 6);
                            prev = life;
                        }
                    }
                }
                if (!baseline)
                {
                    GameDifficulty.Current = DifficultyLevel.Master;
                    string[] route = { "FalseKnight", "Hornet", "MantisLord", "BlackKnight", "Hornet@4", "Grimm", "HollowKnight", "FinalBoss" };
                    int previous = 0;
                    float previousBase = 0, previousGain = 0;
                    foreach (string key in route)
                    {
                        int at = key.IndexOf('@');
                        var r = BossStats.Of(at < 0 ? key : key.Substring(0, at), at < 0 ? -1 : int.Parse(key.Substring(at + 1)));
                        // The bestiary's first habitat can be a Godhome rematch (Grimm).
                        // This route compares overworld progression, not challenge profiles.
                        string scene = key == "Grimm" ? "Grimm_Main_Tent_boss" : EnemyDrops.SceneOf(key);
                        int life = Whole(r, scene, DifficultyLevel.Master);
                        Check("主线平滑递增/" + key, life > previous && (previous == 0 || life <= previous * 2.6f));
                        if (previous > 0)
                        {
                            float gain = BossHealthBudgets.WorldCurveBudget(key) - previousBase;
                            Check("前缓后陡基础增量/" + key, gain > previousGain);
                            previousGain = gain;
                        }
                        previous = life;
                        previousBase = BossHealthBudgets.WorldCurveBudget(key);
                    }
                    foreach (var pair in BossStats.DevAll)
                    {
                        if (BossStats.InGodhome(EnemyDrops.SceneOf(pair.Key))) continue;
                        int life = Whole(pair.Value, EnemyDrops.SceneOf(pair.Key), DifficultyLevel.Master);
                        Check("大师流程符合曲线上限/" + pair.Key, life <= 200006);
                    }
                    foreach (string key in new[] { "FalseKnight@5", "MageLord@5", "InfectedKnight@5" })
                    {
                        int at = key.IndexOf('@'); var r = BossStats.Of(key.Substring(0, at), 5);
                        int life = Whole(r, EnemyDrops.SceneOf(key), DifficultyLevel.Master);
                        int max = r.Stage == 2 ? 9000 : r.Stage == 3 ? 18000 : 42000;
                        Check("梦境强化战符合所属阶段/" + key, life <= max);
                    }
                    // Linear increments, not compounding or a full new budget per body.
                    for (int phase = 1; phase <= 3; phase++)
                        Check("战内阶段线性增加/" + phase, Mathf.Abs((BossPhases.Mult(phase) - BossPhases.Mult(phase - 1)) - BossPhases.Share * BossPhases.Step) < .00001f);
                    Check("双体共享预算", Mathf.Abs(BossPhases.WholeFactor("MantisLord", "Fungus2_15") - (BossPhases.Mult(0) + 2 * BossPhases.Mult(1) / 2)) < .00001f);
                    Check("守望者多体共享预算", Mathf.Abs(BossPhases.WholeFactor("BlackKnight", "Ruins2_03") - (BossPhases.Mult(0) + BossPhases.Mult(1) + BossPhases.Mult(2) + .5f * BossPhases.Mult(3))) < .00001f);

                    foreach (string key in BossHealthBudgets.Gatekeepers)
                    {
                        int at = key.IndexOf('@');
                        var r = BossStats.Of(at < 0 ? key : key.Substring(0, at), at < 0 ? -1 : int.Parse(key.Substring(at + 1)));
                        int life = Whole(r, "world", DifficultyLevel.Master);
                        Check("跨阶段守门20%/" + key, Math.Abs(life - BossHealthBudgets.WorldCurveBudget(key) * 1.2f) <= 6);
                    }
                    Check("大师一战大黄蜂4000", Whole(BossStats.Of("Hornet", -1), "Fungus1_04", DifficultyLevel.Master) == 4000);
                    Check("大师普通辐光200000", Whole(BossStats.Of("FinalBoss", -1), "Dream_Final_Boss", DifficultyLevel.Master) == 200000);
                    GameDifficulty.Current = DifficultyLevel.Normal;
                    PlayerData.instance.isInvincible = true; mod.DevNoFallDamage = true;
                    yield return GoTo("Ruins1_24", "left1");
                    for (float until = Time.realtimeSinceStartup + 20f; Time.realtimeSinceStartup < until;)
                    {
                        if (UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                            .Any(h => h.gameObject.scene.name.StartsWith("Ruins1_24") && UI.EnemyHover.RecordOf(h) == "MageLord")) break;
                        yield return null;
                    }
                    var actual = UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Where(h => h.gameObject.scene.name.StartsWith("Ruins1_24") && UI.EnemyHover.RecordOf(h) == "MageLord").ToArray();
                    Check("实际灵魂大师组件存在", actual.Length > 0);
                    foreach (var hm in actual)
                    {
                        int hp = hm.hp;
                        var info = EnemyStats.Of(hm);
                        if (info.Ref == null || hm.GetComponent<HealthManager>() != hm) continue;
                        int whole = EnemyStats.LifeWhole(info);
                        var soul = BossStats.Of("MageLord", -1).Boss;
                        float expected = 7800 * (soul.LifeAt(0f) / (float)soul.LifeAt(2f));
                        Check("真实Boss总血量接入新预算/" + hm.name, Math.Abs(whole - expected) <= 6);
                        Check("真实Boss当前阶段不独占整场预算/" + hm.name, EnemyStats.LifeMax(info) < whole);
                        Check("读取预算不改写原生阶段HP/" + hm.name, hm.hp == hp);
                    }
                }
            }
            finally
            {
                GameDifficulty.Current = difficultyWas;
                PlayerData.instance.isInvincible = invincibleWas;
                mod.DevNoFallDamage = noFallWas;
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, baseline ? "boss-curve-before.json" : "boss-curve-after.json"),
                JsonConvert.SerializeObject(new { Checks = checks, Failures = failures, Rows = rows }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] Boss曲线合计 {checks - failures}/{checks} {(failures == 0 ? "通过" : "不通过")}");
        }
    }
}
