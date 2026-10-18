using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_17mapoverlap"): the full map, the whole world revealed, centred on the places where two rooms' insides
    /// lie on each other (found offline from the atlas and the layout), each shot close and far.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly (string Scene, string Other, int X, int Y, int Layer)[] mapOverlapSpots =
        {
            ("Ruins1_05", "Ruins1_05c", 63, 188, 1),
            ("Ruins2_11", "Ruins2_11_b", 113, 182, 1),
            ("Deepnest_East_07", "Room_Colosseum_02", 101, 318, 1),
            ("Room_GG_Shortcut", "Waterways_04b", 77, 114, 1),
            ("Deepnest_East_06", "Hive_04", 93, 23, 1),
            ("Fungus2_18", "Fungus2_29", 110, 13, 1),
            ("Deepnest_East_03", "Hive_03", 56, 32, 1),
            ("White_Palace_19", "White_Palace_20", 221, 143, 2),
        };

        IEnumerator MapOverlap17()
        {
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            UI.WorldMap.RevealWorld();
            if (!UI.WorldMap.Open) UI.WorldMap.Toggle();
            yield return null;
            foreach (var s in mapOverlapSpots)
            {
                UI.WorldMap.DevPickLayer(s.Layer);
                foreach (float z in new[] { 4f, 1.6f })
                {
                    UI.WorldMap.DevCentreCell(s.Scene, new Vector2Int(s.X, s.Y));
                    UI.WorldMap.DevZoom(z);
                    yield return new WaitForSecondsRealtime(0.6f);
                    Shot("Town", $"mapov_{s.Scene}_z{z:0.#}");
                    yield return new WaitForSecondsRealtime(0.3f);
                }
            }
            if (UI.WorldMap.Open) UI.WorldMap.Toggle();
            FxCheck("地图重叠处截图", true, $"{mapOverlapSpots.Length} 处");
        }
    }
}
