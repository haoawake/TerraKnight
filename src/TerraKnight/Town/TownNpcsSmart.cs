using UnityEngine;

namespace TerraKnight.Town
{
    /// <summary>
    /// The townsfolk in SmartInteract (Terraria's NPCSmartInteractCandidateProvider): every townsperson within reach is
    /// weighed by its hitbox's distance from the mouse (0 with the mouse on it); the picked one is treated as hovered
    /// (Main.HoverOverNPCs: its chat bubble and name) and a right click anywhere talks to it.
    /// </summary>
    internal static partial class TownNpcs
    {
        static Npc smartPick, smartBest;
        public static int DevSmartTalks;

        /// <summary>NPC.Hitbox: a townsperson's 18 × 40 px body standing on its feet.</summary>
        static Rect Hitbox(Npc n) => new Rect(n.X - 9f * Px, n.FeetY, 18f * Px, 40f * Px);

        public static bool SmartCandidate(HeroController hero, Vector2 mouse, out float distance)
        {
            smartBest = null;
            distance = float.MaxValue;
            if (hero == null) return false;
            foreach (var n in npcs)
            {
                if (!InReach(n, hero)) continue;
                float d = World.SmartInteract.BoxDistance(Hitbox(n), mouse);
                if (d < distance) { distance = d; smartBest = n; }
                if (d <= 0f) break;
            }
            return smartBest != null;
        }

        public static void SmartWin(bool win) => smartPick = win ? smartBest : null;

        /// <summary>The townsperson SmartInteract picked, drawn as hovered.</summary>
        static bool SmartPicked(Npc n) => n == smartPick;

        /// <summary>A right click anywhere with a townsperson picked: the talk with it opens.</summary>
        public static bool TrySmartTalk()
        {
            var hero = HeroController.SilentInstance;
            var n = smartPick;
            if (hero == null || n == null || !npcs.Contains(n) || !InReach(n, hero)) return false;
            Talk(n);
            DevSmartTalks++;
            return true;
        }

        /// <summary>Dev: the picked townsperson's internal name, or null.</summary>
        public static string DevSmartPicked => smartPick?.Def.Internal;
    }
}
