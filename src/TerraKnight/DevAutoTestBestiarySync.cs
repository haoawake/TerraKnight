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
        IEnumerator BestiarySyncTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int checks = 0, failures = 0, savedStage = Shop.DevStage;
            var difficulty = GameDifficulty.Current;
            var sink = EnemyDrops.DevSink;
            var random = UnityEngine.Random.state;
            var observations = new List<object>();
            void Check(bool ok, string label)
            {
                checks++;
                if (!ok) { failures++; Plugin.Log.LogError("[AutoTest] 图鉴同步 " + label + " 不通过"); }
            }
            try
            {
                foreach (DifficultyLevel d in Enum.GetValues(typeof(DifficultyLevel)))
                {
                    GameDifficulty.Current = d;
                    for (int actual = 0; actual <= 5; actual++)
                    {
                        Shop.DevStage = actual;
                        var early = Bestiary.StatsOf("Crawler", 0);
                        var late = Bestiary.StatsOf("Crawler", 5);
                        Check(late.Life > early.Life && late.Damage > early.Damage && late.Defense > early.Defense, "普通敌人六阶段成长/" + d);
                        var boss = Bestiary.StatsOf("FalseKnight", 0);
                        Check(Bestiary.StatsOf("FalseKnight", 5).Defense == boss.Defense, "首领固定预算");
                        Check(Bestiary.StatsOf("Hornet", 0).Defense == EnemyStats.BossDefense(BossStats.Of("Hornet", 1), BossBalance.Of("Hornet", 1), BossArena.SceneOf("Hornet")), "大黄蜂初战不读取二战防御");
                        bool had = Bestiary.Stats.TryGetValue("FalseKnight", out var old);
                        try
                        {
                            Bestiary.Stats["FalseKnight"] = new Bestiary.Seen { Life = 999999, Defense = 999999 };
                            Check(Bestiary.StatsOf("FalseKnight").Defense == boss.Defense, "旧首领防御刷新");
                        }
                        finally { if (had) Bestiary.Stats["FalseKnight"] = old; else Bestiary.Stats.Remove("FalseKnight"); }
                        Check(Shop.DevStage == actual, "预览不改变存档进度");
                    }
                }
                GameDifficulty.Current = DifficultyLevel.Normal;
                UnityEngine.Random.InitState(20261019);
                foreach (int stage in new[] { 0, 5 })
                {
                    Shop.DevStage = stage;
                    const string scene = "Crossroads_45";
                    string key = EnemyDrops.KeyFor("ZombieMiner", EnemyDrops.VariantLevel(scene));
                    Check(key == "ZombieMiner@0", "十字路矿工死亡记录");
                    var rows = EnemyDrops.At(key, stage, false, stage >= 4, stage >= 5, scene);
                    var depth = rows.Where(row => row.Type == ItemType.DepthMeter).ToArray();
                    Check(depth.Length > 0, "十字路实际池含深度计/" + stage);
                    float chance = depth.Sum(row => EnemyDrops.MarginalChance(rows, row, stage));
                    int count = 0;
                    EnemyDrops.DevSink = (type, amount) => { if (type == ItemType.DepthMeter) count += amount; };
                    const int rolls = 10000;
                    for (int i = 0; i < rolls; i++)
                    {
                        EnemyDrops.Roll(key, Vector2.zero, true, false, scene: scene);
                        if (i % 250 == 0) yield return null;
                    }
                    float expected = rolls * chance;
                    Check(count > 0 && Math.Abs(count - expected) < Math.Max(10, 6 * Math.Sqrt(expected)), "实际掉落深度计/" + stage);
                    observations.Add(new { stage, key, scene, chance, rolls, count });
                    Plugin.Log.LogInfo($"[AutoTest] 深度计 stage={stage} pool={chance} rolls={rolls} dropped={count}");
                }
            }
            finally
            {
                Shop.DevStage = savedStage; GameDifficulty.Current = difficulty;
                EnemyDrops.DevSink = sink; UnityEngine.Random.state = random;
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "bestiarysync-result.json"), JsonConvert.SerializeObject(new { checks, failures, observations }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 图鉴同步检查 {checks}，失败 {failures}");
        }
    }
}
