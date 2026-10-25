using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>The way on as the Wayward Compass reads it (UI.WorldMap's compass): the fights the guide would point to next.</summary>
    internal static partial class BossGuide
    {
        /// <summary>
        /// The fights not won yet of the first stage, from the stage reached (Shop.UnlockedStage) on, that has any; with none left
        /// from there on, those of the first stage that has any left behind. Empty once every fight is won.
        /// </summary>
        internal static List<ItemType> NextFights(TerraMod mod)
        {
            var next = new List<ItemType>();
            if (mod == null) return next;
            var won = Won(mod);
            int stage = Shop.UnlockedStage(), pick = -1;
            foreach (var e in Route)
                if (!won.Contains(e.Relic) && e.Stage >= stage && (pick < 0 || e.Stage < pick)) pick = e.Stage;
            if (pick < 0)
                foreach (var e in Route)
                    if (!won.Contains(e.Relic) && (pick < 0 || e.Stage < pick)) pick = e.Stage;
            foreach (var e in Route)
                if (!won.Contains(e.Relic) && e.Stage == pick) next.Add(e.Relic);
            return next;
        }
    }
}
