using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bossdropstage"): a player's report — a boss whose loot doesn't fit the stage drops nothing. Every boss
    /// record of the table (its stage variants "Record@n" too), at each unlocked stage 0–5 (Shop.DevStage), first kill and
    /// a later one, rolled 200 times through EnemyDrops.Roll in its own scene and in Godhome's (GG_Workshop) (EnemyDrops.DevSink counting what comes out,
    /// the Life Crystal and geo left aside), and once more as a treasure bag's opening; the kills that came out empty
    /// counted, the list's rows and the locked ones logged for any boss and stage where an empty kill happens.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossDropStageTest()
        {
            yield return new WaitForSeconds(1f);
            const int Rolls = 200;
            var got = new List<(ItemType type, int n)>();
            EnemyDrops.DevSink = (t, n) => got.Add((t, n));
            int checkedCases = 0, emptyCases = 0, neverCases = 0;
            var lines = new List<string>();
            try
            {
                var records = EnemyDrops.DevRecords.Where(EnemyDrops.IsBoss).OrderBy(r => r).ToList();
                Plugin.Log.LogInfo($"[AutoTest] 首领掉落阶段 首领记录 {records.Count} 个：{string.Join("、", records)}");
                foreach (string record in records)
                {
                    var entry = EnemyDrops.EntryFor(record);
                    string scene = entry.Scene;
                    for (int stage = 0; stage <= 5; stage++)
                        foreach (bool first in new[] { true, false })
                            foreach (var (bag, at) in new[] { (false, scene), (false, "GG_Workshop"), (true, (string)null) })
                            {
                                Shop.DevStage = stage;
                                int empty = 0, items = 0;
                                for (int i = 0; i < Rolls; i++)
                                {
                                    got.Clear();
                                    EnemyDrops.Roll(record, Vector2.zero, direct: true, firstKill: first, fromBag: bag, scene: at);
                                    int n = got.Count(g => g.type != ItemType.LifeCrystal && !IsCoin(g.type));
                                    if (n == 0) empty++;
                                    items += n;
                                }
                                checkedCases++;
                                if (empty == 0) continue;
                                emptyCases++;
                                if (empty == Rolls) neverCases++;
                                var rows = EnemyDrops.In(record, at ?? scene);
                                int locked = rows.Count(d => EnemyDrops.Locked(d, stage));
                                lines.Add($"{record}（{at ?? scene}）阶段 {stage}{(first ? " 首杀" : "")}{(bag ? " 宝藏袋" : "")}：{Rolls} 次里 {empty} 次什么都没掉，平均 {items / (float)Rolls:F2} 件；表里 {rows.Count} 行，锁着 {locked} 行");
                            }
                }
            }
            finally
            {
                EnemyDrops.DevSink = null;
                Shop.DevStage = -1;
            }
            foreach (var l in lines.Take(120)) Plugin.Log.LogInfo("[AutoTest] 首领掉落阶段 " + l);
            Plugin.Log.LogInfo($"[AutoTest] 首领掉落阶段 共 {checkedCases} 种情况，有空掉落的 {emptyCases} 种，从来不掉的 {neverCases} 种 {(neverCases == 0 ? "通过" : "不通过")}");
        }

        static bool IsCoin(ItemType t) => t == ItemType.Geo || t == ItemType.DreamEssence;
    }
}
