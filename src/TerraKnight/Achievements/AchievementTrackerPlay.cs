using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.Achievements
{
    /// <summary>
    /// The casino's and the fishing's achievements: each round as the casino settles it (the slots' and the wheel's shown
    /// once their reels and ball have stopped, so nothing is told early), the pinball's channels and cups as its marbles
    /// meet them; each catch as it comes in, the Angler's quests, the crates opened. Money in copper (100 to a geo); a
    /// game day is Terraria's, 24 minutes.
    /// </summary>
    internal static partial class AchievementTracker
    {
        const float GameDay = 24f * 60f;

        static readonly List<(float At, System.Action Do)> later = new List<(float, System.Action)>();

        /// <summary>Something to check once a show is over (the reels stopped, the ball in its pocket).</summary>
        static void Later(float seconds, System.Action act) => later.Add((Time.unscaledTime + seconds, act));

        static void RunLater()
        {
            for (int i = later.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < later[i].At) continue;
                var act = later[i].Do;
                later.RemoveAt(i);
                if (Achievements.Counting) act();
            }
        }

        // ------------------------------------------------------------------ the casino

        /// <summary>A slot machine's spin: its stake and what it paid, the lines that came up three alike (of how many).</summary>
        public static void SlotsRound(long stake, long payout, bool allIn, int lines, int lineCount)
        {
            if (!Achievements.Counting) return;
            bool hooked = Player.GrapplingHook.IsLatched;
            Later(2.6f, () =>
            {
                if (lines > 0) Achievements.Unlock("G02");
                if (lines >= lineCount) Achievements.Unlock("G03");
                // The consolation: something back, never more than the stake, no line won.
                bool consolation = lines == 0 && payout > 0 && payout <= stake;
                if (consolation) Achievements.AddTo("G04", "slots consolation streak", 5);
                else Achievements.Set("slots consolation streak", 0);
                Round(0, stake, payout, allIn, hooked);
            });
        }

        /// <summary>A turn of the wheel: its stake, what it paid, whether green was the bet.</summary>
        public static void WheelRound(long stake, long payout, bool allIn, bool green)
        {
            if (!Achievements.Counting) return;
            bool hooked = Player.GrapplingHook.IsLatched;
            Later(3f, () =>
            {
                bool won = payout > 0;
                if (green && won) Achievements.Unlock("G05");
                if (green && won && allIn) Achievements.Unlock("G06");
                if (won) Achievements.AddTo("G07", "wheel streak", 8);
                else Achievements.Set("wheel streak", 0);
                Round(1, stake, payout, allIn, hooked);
            });
        }

        /// <summary>One of the player's marbles in a channel (its worth in tenths: 50 at the edges, 1 in the middle).</summary>
        public static void PinLanded(int tenths)
        {
            if (!Achievements.Counting) return;
            if (tenths >= 50) Achievements.Unlock("G08");
            if (tenths <= 1) Achievements.AddTo("G11", "house cut", 50);
        }

        /// <summary>One of the player's marbles in the cup, gold or rainbow.</summary>
        public static void PinCupped(bool gold)
        {
            if (Achievements.Counting && gold) Achievements.Unlock("G10");
        }

        /// <summary>A marble and all that came of it settled: its price, what they paid, the free marbles it gave.</summary>
        public static void PinDone(long stake, long payout, int free, bool allIn)
        {
            if (!Achievements.Counting) return;
            if (free >= 6) Achievements.Unlock("G09");
            Round(2, stake, payout, allIn, Player.GrapplingHook.IsLatched);
        }

        static readonly float[] lastWin = { -1e9f, -1e9f, -1e9f };
        static readonly List<float> allInLosses = new List<float>();

        /// <summary>Every round of every machine: the totals, the streak without a win, all in, the day's wins.</summary>
        static void Round(int game, long stake, long payout, bool allIn, bool hooked)
        {
            float now = Time.unscaledTime;
            long bet = Achievements.Add("casino bet copper", stake);
            if (bet >= 10000000L * 100) Achievements.Unlock("G19");
            long net = Achievements.Add("casino net copper", payout - stake);
            if (net >= 1000000L * 100) Achievements.Unlock("G16");
            if (net <= -1000000L * 100) Achievements.Unlock("G17");
            bool won = payout > stake;
            if (won)
            {
                Achievements.Set("casino dry streak", 0);
                lastWin[game] = now;
                if (now - lastWin[0] <= GameDay && now - lastWin[1] <= GameDay && now - lastWin[2] <= GameDay) Achievements.Unlock("G20");
                if (hooked) Achievements.Unlock("G21");
                if (allIn) Achievements.Unlock("G12");
            }
            else Achievements.AddTo("G18", "casino dry streak", 20);
            if (allIn && payout == 0)
            {
                Achievements.Unlock("G13");
                allInLosses.Add(now);
                allInLosses.RemoveAll(t => now - t > GameDay);
                if (allInLosses.Count >= 3) Achievements.Unlock("G14");
                // The comeback: what was lost all in, to be won back from here.
                Achievements.Set("comeback base", net);
                Achievements.Set("comeback need", stake);
            }
            long need = Achievements.Count("comeback need");
            if (need > 0 && net - Achievements.Count("comeback base") > need) Achievements.Unlock("G15");
        }

        // ------------------------------------------------------------------ fishing

        /// <summary>A catch landed: what it was, the water (poured or the room's own, its scene), the bait on the line.</summary>
        public static void Fished(ItemType type, bool poured, string scene, ItemType bait)
        {
            if (!Achievements.Counting) return;
            long catches = Achievements.Add("catches");
            if (catches >= 100) Achievements.Unlock("F01");
            if (catches >= 1000) Achievements.Unlock("F02");
            if (type == ItemType.OldShoe || type == ItemType.FishingSeaweed || type == ItemType.TinCan) Achievements.AddTo("F07", "junk", 10);
            if (poured) Achievements.Unlock("F09");
            if (!string.IsNullOrEmpty(scene))
            {
                string area = World.Areas.AreaName(scene);
                if (!string.IsNullOrEmpty(area) && Achievements.Count("fished in:" + area) == 0)
                {
                    Achievements.Set("fished in:" + area, 1);
                    Achievements.AddTo("F08", "fishing areas", 5);
                }
            }
            if (Current() != null) Achievements.Unlock("F10");
            if (bait == ItemType.MasterBait) Achievements.Unlock("F13");
        }

        /// <summary>An Angler's quest turned in: how many so far.</summary>
        public static void AnglerQuest(int done)
        {
            if (!Achievements.Counting) return;
            Achievements.Max("angler quests", done);
            if (done >= 10) Achievements.Unlock("F03");
            if (done >= 25) Achievements.Unlock("F04");
            if (done >= 50) Achievements.Unlock("F05");
        }

        /// <summary>A fishing crate opened.</summary>
        public static void CrateOpened() => Achievements.AddTo("F06", "crates", 100);

        /// <summary>The counts the list shows bars for.</summary>
        static void Goals()
        {
            AchievementDefs.Goal("W01", "kills", 1000);
            AchievementDefs.Goal("W02", "kills", 10000);
            AchievementDefs.Goal("W12", "primal aspids", 100);
            AchievementDefs.Goal("W13", "elites", 50);
            AchievementDefs.Goal("D06", "saw deaths", 50);
            AchievementDefs.Goal("D12", "aspid deaths", 10);
            AchievementDefs.Goal("D19", "deaths", 100);
            AchievementDefs.Goal("D20", "deaths", 1000);
            AchievementDefs.Goal("C13", "nohit bosses", 10);
            AchievementDefs.Goal("B39", "newai bosses", 10);
            AchievementDefs.Goal("G04", "slots consolation streak", 5);
            AchievementDefs.Goal("G07", "wheel streak", 8);
            AchievementDefs.Goal("G11", "house cut", 50);
            AchievementDefs.Goal("G18", "casino dry streak", 20);
            AchievementDefs.Goal("F01", "catches", 100);
            AchievementDefs.Goal("F02", "catches", 1000);
            AchievementDefs.Goal("F03", "angler quests", 10);
            AchievementDefs.Goal("F04", "angler quests", 25);
            AchievementDefs.Goal("F05", "angler quests", 50);
            AchievementDefs.Goal("F06", "crates", 100);
            AchievementDefs.Goal("F07", "junk", 10);
            AchievementDefs.Goal("F08", "fishing areas", 5);
            AchievementDefs.Goal("E01", "areas", 24);
            AchievementDefs.Goal("E12", "metres", 100000);
            AchievementDefs.Goal("I09", "pets", 10);
            AchievementDefs.Goal("I10", "mounts", 10);
            AchievementDefs.Goal("I11", "mounts", 38);
            AchievementDefs.Goal("I17", "spell weapons", 16);
            AchievementDefs.Goal("I03", "reforges", 200);
            AchievementDefs.Goal("I20", "bags", 25);
            AchievementDefs.Goal("E13", "tiles dug", 10000);
            AchievementDefs.Goal("E14", "tiles dug", 100000);
            AchievementDefs.Goal("E19", "pylon trips", 100);
            AchievementDefs.Goal("H03", "prefab houses", 10);
            AchievementDefs.Goal("H07", "shimmered", 50);
            AchievementDefs.Goal("H10", "chest renames", 20);
            AchievementDefs.Goal("H11", "block swaps", 1000);
            AchievementDefs.Goal("H14", "bucket cells", 1000);
            AchievementDefs.Goal("W09", "blocks", 100);
            AchievementDefs.Goal("W14", "art kills", 100);
            AchievementDefs.Goal("W15", "spell kills", 500);
            AchievementDefs.Goal("C25", "nail only bosses", 5);
            AchievementDefs.Goal("X07", "bag full", 50);
            AchievementDefs.Goal("X09", "dummy hits", 10000);
        }
    }
}
