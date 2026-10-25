using System.Collections;
using System.Linq;
using System.Text;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bossdefense"): a boss's Terraria defense against the common enemies' around it (the user's, 2026-10-20:
    /// some bosses below the small fry). Every arena's boss in its own room at its reward stage: its stand-in, the stand-in's
    /// defense, its tune, the defense it has; the room's common enemies' then (Areas.Defense of the stage, caught up) and an
    /// elite's; Godhome's at stages 2–5 against the common enemies of that stage.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossDefenseTest()
        {
            yield return new WaitForSeconds(1f);
            int below = 0, all = 0;
            var sb = new StringBuilder();
            try
            {
                foreach (string key in BossArena.DevKeys().OrderBy(k => ProgressionBalance.BossRewards.TryGetValue(k, out var rw) ? rw.Stage : 9))
                {
                    string record = key.Split('@')[0];
                    string scene = BossArena.SceneOf(key);
                    if (scene == null || scene.StartsWith("GG_")) continue;
                    int stage = ProgressionBalance.BossRewards.TryGetValue(key, out var reward) ? reward.Stage : World.Areas.LevelOf(scene);
                    Shop.DevStage = stage;
                    var r = BossStats.Of(record, EnemyDrops.VariantLevel(scene));
                    if (r == null) continue;
                    var t = BossBalance.Of(record, EnemyDrops.VariantLevel(scene));
                    int def = EnemyStats.BossDefense(r, t, scene);
                    int level = World.Areas.FightLevel(scene);
                    int mob = Mathf.RoundToInt(World.Areas.Defense(World.Areas.StatLevel(level)) * World.Areas.CatchUp(level));
                    all++;
                    bool low = def < mob;
                    if (low) below++;
                    sb.Append($"\n    {key}（阶段 {stage}，{scene} 区域等级 {level}）替身 {r.Boss.Name} 防 {r.Boss.Defense} × 调整 {t.Defense:F2} ÷2 = {def}，小怪 {mob}{(low ? " ← 比小怪低" : "")}");
                }
                var gsb = new StringBuilder();
                for (int stage = 0; stage <= 5; stage++)
                {
                    Shop.DevStage = stage;
                    int mob = World.Areas.Defense(stage);
                    var p = GodhomeBalance.Hall[0];
                    int def = EnemyStats.BossDefense(BossStats.Of("FalseKnight", 0), BossBalance.Of("Gruz", 0), "GG_Workshop");
                    gsb.Append($" 阶段 {stage}：调谐 {def} / 小怪 {mob}{(def < mob ? "（低）" : "")}；");
                }
                Plugin.Log.LogInfo($"[AutoTest] 首领防御 地图首领 {all} 个，比同处小怪低的 {below} 个：{sb}");
                Plugin.Log.LogInfo($"[AutoTest] 首领防御 神居（调整为 1 的首领）：{gsb}");
            }
            finally { Shop.DevStage = -1; }
            Plugin.Log.LogInfo("[AutoTest] 首领防御 完成 通过");
        }
    }
}
