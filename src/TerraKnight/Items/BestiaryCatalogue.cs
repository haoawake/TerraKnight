using System;
using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>Explicit reference mode: potential sources, separate from the save's live drop table.</summary>
    internal static class BestiaryCatalogue
    {
        internal sealed class Entry
        {
            internal ItemType Type;
            internal int FirstStage;
            internal bool MasterOnly;
        }
        static readonly Dictionary<string, IReadOnlyList<Entry>> cache = new Dictionary<string, IReadOnlyList<Entry>>();

        internal static IReadOnlyList<Entry> Of(string record, string scene)
        {
            string key = record + "|" + scene;
            if (cache.TryGetValue(key, out var found)) return found;
            var rows = new Dictionary<ItemType, Entry>();
            var saved = VanillaBossProgression.DevClears;
            try
            {
                // A synchronous, scoped reference query. Restore before returning to the game loop.
                VanillaBossProgression.DevClears = new HashSet<string>(VanillaBossProgression.All.Keys);
                for (int stage = 0; stage <= World.Areas.MaxLevel; stage++)
                {
                    if (!ProgressionLoot.GodhomeScene(scene) && ProgressionBalance.FormalBoss(record) && ProgressionBalance.BossRewards.TryGetValue(record, out var boss) && stage < boss.Stage) continue;
                    var drops = EnemyDrops.At(record, stage, true, stage >= 4, stage >= 5, scene);
                    foreach (var d in drops)
                    {
                        if (d.Type == ItemType.None || d.Num <= 0 || d.LockStage > stage) continue;
                        if (!rows.TryGetValue(d.Type, out var entry))
                            rows[d.Type] = new Entry { Type = d.Type, FirstStage = stage, MasterOnly = d.Master };
                        else entry.MasterOnly &= d.Master;
                    }
                }
            }
            finally { VanillaBossProgression.DevClears = saved; }
            var result = new List<Entry>(rows.Values);
            result.Sort((a, b) => a.FirstStage != b.FirstStage ? a.FirstStage.CompareTo(b.FirstStage) : a.Type.CompareTo(b.Type));
            cache[key] = result;
            return result;
        }
    }
}
