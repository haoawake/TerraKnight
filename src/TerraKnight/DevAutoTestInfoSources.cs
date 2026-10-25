using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator InfoSourcesAudit()
        {
            var types = new HashSet<ItemType> { ItemType.Radar, ItemType.DepthMeter, ItemType.Compass, ItemType.MetalDetector, ItemType.LifeformAnalyzer, ItemType.DPSMeter, ItemType.Stopwatch, ItemType.TallyCounter, ItemType.FishermansGuide, ItemType.WeatherRadio, ItemType.Sextant };
            var rows = new List<object>();
            var was = World.GameDifficulty.Current;
            World.GameDifficulty.Current = World.DifficultyLevel.Normal;
            try
            {
                for (int stage = 0; stage <= 5; stage++)
                {
                    foreach (string record in EnemyDrops.DevRecords)
                    {
                        foreach (var home in BestiaryHabitats.Of(record))
                        {
                            var drops = EnemyDrops.At(record, stage, false, stage >= 3, stage >= 5, home.Scene);
                            var rates = new EnemyDrops.ChanceSummary(drops, stage);
                            foreach (var item in rates.Items)
                                if (types.Contains(item.Key) && item.Value > 0f)
                                    rows.Add(new { stage, record, enemy = EnemyDrops.EnglishName(record), home.Area, home.Scene, item = item.Key.ToString(), name = ItemDb.Get(item.Key).Name, chance = item.Value, boss = EnemyDrops.IsBoss(record) });
                        }
                    }
                    yield return null;
                }
                var definitions = types.Select(t => new { item = t.ToString(), name = ItemDb.Get(t).Name, stage = ItemDb.Get(t).Stage, source = ItemDb.Get(t).Source.ToString() }).ToArray();
                string dir = DevCopy.SaveDir;
                File.WriteAllText(Path.Combine(dir, "info-sources.json"), JsonConvert.SerializeObject(new { difficulty = "Normal", definitions, rows }, Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] InfoSources exported rows={rows.Count} file={Path.Combine(dir, "info-sources.json")}");
            }
            finally { World.GameDifficulty.Current = was; }
        }
    }
}
