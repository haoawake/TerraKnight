using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>Announce actual growth-stage changes in the local bottom-left message log.</summary>
    internal static class ProgressionNotice
    {
        static int previous = -1;
        static ulong previousWorld;
        public static int DevNotes { get; private set; }
        public static int DevWorldNotes { get; private set; }

        // Loading a character establishes a baseline instead of announcing old progress again.
        public static void Reset() { previous = -1; previousWorld = 0; }

        public static void Tick(bool ready)
        {
            if (!ready) return;
            int stage = Items.Shop.UnlockedStage();
            int before = previous;
            ulong world = Items.VanillaBossProgression.StateKey;
            ulong added = world & ~previousWorld;
            previousWorld = world;
            previous = stage;
            if (before < 0) return;
            if (before != stage)
            {
                Net.NetChat.Add(Lang.F("圣巢探索进入新阶段：{0} / {1}。", stage, World.Areas.MaxLevel), new Color(1f, 0.85f, 0.35f));
                DevNotes++;
            }
            if (added != 0)
            {
                var names = new System.Collections.Generic.List<string>();
                int i = 0;
                foreach (var p in Items.VanillaBossProgression.All)
                {
                    if ((added & (1UL << i)) != 0) names.Add(Items.ProgressionInfo.BossName(p.Key));
                    i++;
                }
                Net.NetChat.Add(Lang.F("圣巢战况更新：已击败 {0}。相关掉落与制作条件已更新。", string.Join("、", names)), new Color(.65f, .91f, .88f));
                DevWorldNotes++;
            }
        }
    }
}
