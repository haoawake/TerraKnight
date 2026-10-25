using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "hurtcurve"): what the player takes at each stage (the user's "测试一下每个阶段的伤害平衡性"). On Master:
    /// every world boss's hit at its reward stage (a mask's damage there, times the bosses' factor and its tune) and the
    /// common enemies' and elites' hit per stage; written to DevCopy.SaveDir's hurt-curve.json for the stage report beside
    /// the masterattack suite's (what the player deals).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HurtCurveTest()
        {
            yield return new WaitForSeconds(1f);
            var was = World.GameDifficulty.Current;
            World.GameDifficulty.Current = World.DifficultyLevel.Master;
            try
            {
                var bosses = BossArena.DevKeys()
                    .Where(k => BossArena.SceneOf(k) != null && !BossArena.SceneOf(k).StartsWith("GG_"))
                    .Select(k =>
                    {
                        string record = k.Split('@')[0], scene = BossArena.SceneOf(k);
                        int stage = ProgressionBalance.BossRewards.TryGetValue(k, out var r) ? r.Stage : World.Areas.LevelOf(scene);
                        var tune = BossBalance.Of(record, EnemyDrops.VariantLevel(scene));
                        int hit = Mathf.RoundToInt(World.Areas.MasterDamagePerMask(stage) * World.Areas.BossDamage * tune.Damage);
                        return new { Key = k, Stage = stage, Hit = hit, Tune = tune.Damage };
                    }).OrderBy(b => b.Stage).ToList();
                var stages = Enumerable.Range(0, 6).Select(s => new
                {
                    Stage = s, Mob = World.Areas.MasterDamagePerMask(s),
                    Elite = Mathf.RoundToInt(World.Areas.MasterDamagePerMask(s) * World.Areas.EliteDamage),
                    MobDefense = World.Areas.Defense(s),
                }).ToList();
                File.WriteAllText(Path.Combine(DevCopy.SaveDir, "hurt-curve.json"), JsonConvert.SerializeObject(new { Bosses = bosses, Stages = stages }, Formatting.Indented));
                foreach (var b in bosses) Plugin.Log.LogInfo($"[AutoTest] 受伤曲线 {b.Key} 阶段 {b.Stage} 一击 {b.Hit}（调整 {b.Tune:F2}）");
                foreach (var s in stages) Plugin.Log.LogInfo($"[AutoTest] 受伤曲线 阶段 {s.Stage} 小怪一击 {s.Mob} 精英 {s.Elite}");
            }
            finally { World.GameDifficulty.Current = was; }
            Plugin.Log.LogInfo("[AutoTest] 受伤曲线 完成 通过");
        }
    }
}
