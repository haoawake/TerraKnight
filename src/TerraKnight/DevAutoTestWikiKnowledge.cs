using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TeamCherry.Localization;
using TerraKnight.Items;
using TerraKnight.UI;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator WikiKnowledgeExport(string folder)
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Private export required");
            var sheets = (Dictionary<string, Dictionary<string, string>>)AccessTools.Field(typeof(Language), "_currentEntrySheets").GetValue(null);
            var dialogue = new List<object>();
            foreach (var sheet in sheets.OrderBy(p => p.Key))
                foreach (var row in sheet.Value.OrderBy(p => p.Key))
                {
                    string id = sheet.Key + "/" + row.Key;
                    TerrarianDialogue.All.TryGetValue(id, out var replacement);
                    dialogue.Add(new { Id = id, Sheet = sheet.Key, Key = row.Key,
                        Text = Language.Get(row.Key, sheet.Key), Changed = replacement != null,
                        English = replacement?.En, Reason = replacement?.Reason });
                }
            var profiles = new Dictionary<string, EnemyAi.Profile>();
            foreach (string record in EnemyDrops.DevRecords)
            {
                var profile = EnemyAi.Profiles.For(record);
                if (profile != null) profiles[record] = profile;
            }
            var slots = new SortedDictionary<long, int>();
            for (int a = 0; a < 5; a++) for (int b = 0; b < 5; b++) for (int c = 0; c < 5; c++)
            {
                long payout = Casino.SlotPayout(10000, a, b, c);
                slots[payout] = slots.TryGetValue(payout, out int n) ? n + 1 : 1;
            }
            var roulette = new List<object>();
            foreach (Casino.Colour colour in Enum.GetValues(typeof(Casino.Colour)))
                roulette.Add(new { Colour = colour.ToString(), Outcomes = Enumerable.Range(0, Casino.WheelSectors).Select(n => Casino.WheelPayout(10000, colour, n)).ToArray() });
            var pinball = new List<object>();
            // Clock and launch force are inputs, not a universal probability distribution.
            // Independent families use a reproducible uniform clock over 60 seconds.
            const int samples = 2000;
            foreach (double force in new[] { .4, .7, 1.0 })
            {
                var payouts = new SortedDictionary<int, int>();
                for (int i = 0; i < samples; i++)
                {
                    uint seed = unchecked((uint)(i + 1) * 2246822519u + 3);
                    double clock = (i * .6180339887498949 % 1) * 60;
                    int result = PinSim.Replay(new List<(double, uint, double)> { (force, seed, clock) })[0];
                    payouts[result] = payouts.TryGetValue(result, out int n) ? n + 1 : 1;
                    if (i % 25 == 0) yield return null;
                }
                pinball.Add(new { Force = force, Samples = samples, PayoutTenths = payouts });
            }
            var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
            settings.Converters.Add(new StringEnumConverter());
            File.WriteAllText(Path.Combine(folder, "knowledge-data.json"), JsonConvert.SerializeObject(new {
                schema = 1, language = Language.CurrentLanguage().ToString(), dialogue, profiles,
                casino = new { stakeCopper = 10000, slots, roulette, wheelSectors = Casino.WheelSectors,
                    channelTenths = PinSim.ChannelTenths, goldTenths = PinSim.GoldTenths,
                    freeEach = PinSim.FreeEach, freeMost = PinSim.FreeMost, pinball,
                    pinballProtocol = "Independent single families; clock frac(i*0.6180339887498949)*60 seconds; seed (i+1)*2246822519+3; Replay horizon 180s. Returned launch is -1; Replay substitutes 1x for unresolved families." }
            }, settings));
            Plugin.Log.LogInfo($"[AutoTest] Wiki 知识导出：{dialogue.Count} 文本，{profiles.Count} AI，125 槽轮结果，6000 弹珠家庭");
        }
    }
}
