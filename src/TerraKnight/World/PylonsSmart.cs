using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The lit pylons in SmartInteract (tile 597 in TileSmartInteractCandidateProvider's list): within their use range,
    /// weighed by the cell nearest the mouse; picked, a right click anywhere opens the pylon map, as a click on one does.
    /// </summary>
    internal static partial class Pylons
    {
        static PylonObject smartPick, smartBest;
        public static int DevSmartUsed;

        public static bool SmartCandidate(HeroController hero, Vector2 mouse, out float centre, out float rect)
        {
            smartBest = null;
            centre = rect = float.MaxValue;
            if (hero == null) return false;
            Vector2 at = hero.transform.position;
            foreach (var o in spawned)
            {
                if (o == null || !o.isActiveAndEnabled || o.Pylon.Dormant || Vector2.Distance(o.Bounds.center, at) > UseRange) continue;
                SmartInteract.NearestCell(o.Bounds, mouse, out float c, out float r);
                if (c < centre) { centre = c; rect = r; smartBest = o; }
            }
            return smartBest != null;
        }

        /// <summary>The pick decided: it marked gold, the other lit pylons in range grey, none when the query didn't run.</summary>
        public static void SmartWin(bool win)
        {
            smartPick = win ? smartBest : null;
            var hero = HeroController.SilentInstance;
            Vector2 at = hero != null ? (Vector2)hero.transform.position : default;
            foreach (var o in spawned)
                if (o != null)
                    o.Highlight = !SmartInteract.Showing || hero == null || o.Pylon.Dormant || Vector2.Distance(o.Bounds.center, at) > UseRange ? 0 : o == smartPick ? 2 : 1;
        }

        /// <summary>A right click anywhere with a pylon picked: the pylon map (one under the mouse takes its own click, HandleUse).</summary>
        public static bool TrySmartUse()
        {
            if (smartPick == null || HoveringPylon || !GameHooks.PlayerHasControl() || UI.FullMap.Open) return false;
            UI.FullMap.OpenForPylons();
            DevSmartUsed++;
            return true;
        }
    }
}
