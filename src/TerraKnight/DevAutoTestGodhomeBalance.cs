using System.Collections;
using System.Linq;
using System.Text;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "godhomebalance"): Godhome's bosses at the player's stage (the user's, 2026-10-20). For a few bosses, at
    /// stages 2–5 and four challenges (Attuned, Radiant, the first and the fifth pantheon): the whole fight's life on
    /// Master, a hit's Terraria damage before defense, the defense; beside each, the boss's world fight at its own stage.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GodhomeBalanceTest()
        {
            yield return new WaitForSeconds(1f);
            var d = World.DifficultyLevel.Master;
            var profiles = new[] { GodhomeBalance.Hall[0], GodhomeBalance.Hall[2], GodhomeBalance.Pantheons[0], GodhomeBalance.Pantheons[4] };
            try
            {
                foreach (var (record, gg) in new[] { ("FalseKnight", "GG_False_Knight"), ("BigFly", "GG_Gruz_Mother"), ("Hornet", "GG_Hornet_1"),
                    ("MantisLord", "GG_Mantis_Lords"), ("HiveKnight", "GG_Hive_Knight"), ("NailBros", "GG_Brothers"), ("HollowKnightPrime", "GG_Hollow_Knight"), ("FinalBoss", "GG_Radiance") })
                {
                    var sb = new StringBuilder();
                    var world = BossStats.Of(record, 0);
                    if (world == null) { Plugin.Log.LogInfo($"[AutoTest] 神居平衡 {record} 没有对照 不通过"); continue; }
                    string arena = BossArena.SceneOf(record) ?? gg;
                    Shop.DevStage = world.Stage;
                    var tw = BossBalance.Of(record, EnemyDrops.VariantLevel(arena));
                    long wLife = Mathf.RoundToInt(BossStats.LifeOf(world, arena, d) * tw.Life * BossPhases.WholeFactor(record, arena));
                    int wHit = Mathf.RoundToInt(World.Areas.MasterDamagePerMask(World.Areas.LevelOf(arena)) * World.Areas.BossDamage * tw.Damage);
                    sb.Append($"地图（阶段 {world.Stage}）{wLife} 血 一击 {wHit}");
                    var r = BossStats.Of(record, EnemyDrops.VariantLevel(gg)) ?? world;
                    var t = BossBalance.Of(record, EnemyDrops.VariantLevel(gg));
                    for (int stage = 2; stage <= 5; stage++)
                    {
                        Shop.DevStage = stage;
                        sb.Append($"\n    阶段 {stage}：");
                        foreach (var p in profiles)
                        {
                            long life = Mathf.RoundToInt(BossStats.GodhomeLifeOf(r, gg, d, p) * t.Life * BossPhases.WholeFactor(record, gg));
                            int hit = Mathf.RoundToInt(World.Areas.MasterDamagePerMask(World.Areas.FightLevel(gg)) * World.Areas.BossDamage * t.Damage * p.Damage);
                            int def = GodhomeBalance.Defense(Mathf.RoundToInt(BossStats.StageDefense(stage) * Mathf.Min(1f, t.Defense) * 0.5f), p);
                            sb.Append($"{p.Name} {life} 血/一击 {hit}/防 {def}；");
                        }
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 神居平衡 {record}：{sb}");
                }
                Plugin.Log.LogInfo($"[AutoTest] 神居平衡 各阶段血量比例 {string.Join(" ", new[] { 0, 1, 2, 3, 4, 5 }.Select(s => $"{s}:{BossHealthBudgets.StageShare(s):F3}"))}，各阶段首领防御 {string.Join(" ", new[] { 0, 1, 2, 3, 4, 5 }.Select(s => $"{s}:{BossStats.StageDefense(s)}"))} 通过");
            }
            finally { Shop.DevStage = -1; }
        }
    }
}
