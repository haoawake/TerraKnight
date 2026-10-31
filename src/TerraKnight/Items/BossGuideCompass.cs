using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>The way on as the Wayward Compass reads it (UI.WorldMap's compass): the fights the guide would point to next.</summary>
    internal static partial class BossGuide
    {
        /// <summary>
        /// The fights not won yet of the first stage, from the stage reached (Shop.UnlockedStage) on, that has any the save can
        /// begin now (<see cref="Ready"/>); with none from there on, those of the first stage left behind that has any; only
        /// when none can be begun anywhere, those of the first stage with any left, as before. A fight needing what the save
        /// hasn't got was pointed to all the same (a player's 10-31 "打完格鲁兹之母路标给我指天上": Gorb's dream on top of the
        /// Howling Cliffs, long before the Dream Nail). Empty once every fight is won. A fight that can't be had any more is
        /// passed over (<see cref="Passed"/>: with Zote dead, his Colosseum trial and Grey Prince Zote).
        /// </summary>
        internal static List<ItemType> NextFights(TerraMod mod)
        {
            var next = new List<ItemType>();
            if (mod == null) return next;
            var won = Passed(mod);
            int stage = Shop.UnlockedStage();
            bool ready = true;
            int pick = FirstStage(won, stage, true);
            if (pick < 0)
            {
                ready = false;
                pick = FirstStage(won, stage, false);
            }
            foreach (var e in Route)
                if (!won.Contains(e.Relic) && e.Stage == pick && (!ready || Ready(e.Relic, out _))) next.Add(e.Relic);
            return next;
        }

        /// <summary>The first stage from `stage` on with a fight left (one that can be begun, if `ready`); else the first left behind; -1 none.</summary>
        static int FirstStage(HashSet<ItemType> won, int stage, bool ready)
        {
            int pick = -1;
            foreach (var e in Route)
                if (!won.Contains(e.Relic) && e.Stage >= stage && (pick < 0 || e.Stage < pick) && (!ready || Ready(e.Relic, out _))) pick = e.Stage;
            if (pick < 0)
                foreach (var e in Route)
                    if (!won.Contains(e.Relic) && (pick < 0 || e.Stage < pick) && (!ready || Ready(e.Relic, out _))) pick = e.Stage;
            return pick;
        }

        /// <summary>
        /// Whether the save can begin the fight now, by what Hollow Knight asks before it starts: the Dream Nail for a dream, the
        /// Love Key for the Collector's tower, the Troupe come for Grimm, the three Dreamers asleep for the Hollow Knight, the
        /// Godtuner for Godhome's. What it lacks, else null (the compass's card says it).
        /// </summary>
        internal static bool Ready(ItemType fight, out string need)
        {
            need = null;
            var pd = PlayerData.instance;
            if (pd == null) return true;
            bool dream = pd.GetBool("hasDreamNail"), sealed3 = pd.GetInt("guardiansDefeated") >= 3;
            switch (fight)
            {
                case ItemType.RelicGhostNoEyes: case ItemType.RelicGhostAladar: case ItemType.RelicGhostHu: case ItemType.RelicGhostMarmu:
                case ItemType.RelicGhostGalien: case ItemType.RelicGhostXero: case ItemType.RelicGhostMarkoth:
                case ItemType.RelicFalseKnightAt5: case ItemType.RelicMageLordAt5: case ItemType.RelicInfectedKnightAt5:
                    if (!dream) need = Lang.T("需要梦之钉才能入梦挑战（在安息之地获得）");
                    break;
                case ItemType.RelicWhiteDefender:
                    if (!dream) need = Lang.T("需要梦之钉才能入梦挑战（在安息之地获得）");
                    else if (!pd.GetBool("killedDungDefender")) need = Lang.T("需要先击败粪虫防御者，再在他的坟前入梦");
                    break;
                case ItemType.RelicGreyPrince:
                    if (!dream) need = Lang.T("需要梦之钉才能入梦挑战（在安息之地获得）");
                    break;
                case ItemType.RelicJarCollector:
                    if (!pd.GetBool("hasLoveKey") && !pd.GetBool("openedLoveDoor")) need = Lang.T("需要爱之钥才能打开爱之塔");
                    break;
                case ItemType.RelicGrimm:
                    if (!pd.GetBool("troupeInTown")) need = Lang.T("需要先用梦之钉点亮嚎叫悬崖的梦魇之灯，格林剧团才会来到德特茅斯");
                    break;
                case ItemType.RelicNightmareGrimm:
                    if (!pd.GetBool("killedGrimm") || pd.GetInt("grimmChildLevel") < 3) need = Lang.T("需要先击败格林团长，并让格林之子收齐火焰");
                    else if (!dream) need = Lang.T("需要梦之钉才能入梦挑战（在安息之地获得）");
                    break;
                case ItemType.RelicHollowKnight:
                    if (!sealed3) need = Lang.T("需要先让三位守梦者长眠，黑卵神殿才会打开");
                    break;
                case ItemType.RelicFinalBoss:
                    if (!sealed3) need = Lang.T("需要先让三位守梦者长眠，黑卵神殿才会打开");
                    else if (!pd.GetBool("gotShadeCharm") || !dream) need = Lang.T("需要虚空之心和梦之钉，才能在空洞骑士的梦里见到辐光");
                    break;
                case ItemType.RelicNailBros: case ItemType.RelicPaintmaster: case ItemType.RelicNailsage: case ItemType.RelicHollowKnightPrime:
                case ItemType.RelicSistersOfBattle: case ItemType.RelicWingedNosk: case ItemType.RelicAbsoluteRadiance:
                    if (!pd.GetBool("hasGodfinder")) need = Lang.T("需要神明调谐器（在垃圾坑深处的寻神者那里获得）");
                    break;
            }
            return need == null;
        }

        /// <summary>Dev: the fight the voice would point to after first winning `won` (Compose's way on), none for the closing words.</summary>
        internal static ItemType? DevWayOn(TerraMod mod, ItemType won)
        {
            var fight = Find(won);
            if (mod == null || fight == null) return null;
            var passed = Passed(mod);
            passed.Add(won);
            return Next(passed, fight.Stage, out _, out _)?.Relic;
        }
    }
}
