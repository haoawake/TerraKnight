using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "agletpool"): the Aglet in the enemies' drop pools (ProgressionLoot.PoolExtras, the user's): at each
    /// stage 0–5, every enemy whose drops hold it, at what chance a kill; the book's areas for them.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator AgletPoolTest()
        {
            yield return new WaitForSeconds(1f);
            int found = 0;
            try
            {
                for (int stage = 0; stage <= 5; stage++)
                {
                    Shop.DevStage = stage;
                    var lines = new List<string>();
                    foreach (string record in EnemyDrops.DevRecords.OrderBy(r => r))
                    {
                        var drops = EnemyDrops.In(record, EnemyDrops.EntryFor(record).Scene);
                        foreach (var d in drops)
                            if (d.Type == ItemType.Aglet && !EnemyDrops.Locked(d, stage))
                            {
                                lines.Add($"{record}（{World.Areas.AreaName(EnemyDrops.EntryFor(record).Scene)}）{EnemyDrops.MarginalChance(drops, d, stage) * 100f:F2}%");
                                break;
                            }
                    }
                    found += lines.Count;
                    Plugin.Log.LogInfo($"[AutoTest] 鞋带束头 阶段 {stage}：{lines.Count} 种敌人掉 {string.Join("，", lines)}");
                }
            }
            finally { Shop.DevStage = -1; }
            Plugin.Log.LogInfo($"[AutoTest] 鞋带束头 诊断：{ProgressionLoot.DevExtraWhy(ItemType.Aglet)}");
            Plugin.Log.LogInfo($"[AutoTest] 鞋带束头 {(found > 0 ? "通过" : "不通过")}");
        }
    }
}
