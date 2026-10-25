using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.Achievements
{
    /// <summary>
    /// What is looked at once a second besides the journey: the rooms and areas been to, the stag stations and Cornifer's
    /// maps, Zote's precepts, the bench sat on, the Grubfather's rewards, the mounts ridden, the things ever carried (the
    /// Zenith, pets, the spells' weapons), the bag; and the ground covered, every frame.
    /// </summary>
    internal static partial class AchievementTracker
    {
        static string lastScene;
        static readonly HashSet<ItemType> bagSeen = new HashSet<ItemType>();
        static float benchSince = -1f;

        /// <summary>The sixteen weapons the four spells give (four classes each).</summary>
        static readonly ItemType[] SpellWeapons =
        {
            ItemType.RevenantEdge, ItemType.RevenantPulseRifle, ItemType.VengefulSpiritFocus, ItemType.WanderingSoulStaff,
            ItemType.WailingDeathbell, ItemType.GhostPipeOrgan, ItemType.HowlingWraithsFocus, ItemType.ElegyMask,
            ItemType.ShadeEdge, ItemType.ShadeRiftRifle, ItemType.ShadeSoulFocus, ItemType.ShadeSoulStaff,
            ItemType.AbyssYoyo, ItemType.VoidSniper, ItemType.AbyssStaff, ItemType.DirgeMask,
        };

        static void PollWorld()
        {
            var pd = PlayerData.instance;
            var gm = GameManager.UnsafeInstance;
            var mod = TerraMod.Instance;
            if (pd == null || gm == null || mod == null) return;

            // Rooms: the areas been to, a few rooms of their own.
            string scene = gm.sceneName;
            if (scene != lastScene && !string.IsNullOrEmpty(scene))
            {
                lastScene = scene;
                string area = World.Areas.AreaName(scene);
                if (!string.IsNullOrEmpty(area) && Achievements.Count("area:" + area) == 0)
                {
                    Achievements.Set("area:" + area, 1);
                    Achievements.AddTo("E01", "areas", 24);
                }
                if (scene == "Abyss_06_Core") Achievements.Unlock("E07");
                if (scene == "Crossroads_30") Achievements.Unlock("E08");
                if (scene == "GG_Waterways") Achievements.Unlock("E09");
            }

            Check("E06", pd.openedTown && pd.openedCrossroads && pd.openedGreenpath && pd.openedRuins1 && pd.openedRuins2 && pd.openedFungalWastes
                         && pd.openedRoyalGardens && pd.openedRestingGrounds && pd.openedDeepnest && pd.openedStagNest && pd.openedHiddenStation);
            Check("E18", pd.mapCrossroads && pd.mapGreenpath && pd.mapFogCanyon && pd.mapRoyalGardens && pd.mapFungalWastes && pd.mapCity
                         && pd.mapWaterways && pd.mapMines && pd.mapDeepnest && pd.mapCliffs && pd.mapOutskirts && pd.mapRestingGrounds && pd.mapAbyss);
            Check("X01", pd.zotePrecept >= 57);
            Check("X03", pd.bankerTheft >= 1);
            Check("X04", pd.grubRewards >= 7);
            Check("X11", pd.elderbugGaveFlower);
            Check("I05", Charms.Enabled && pd.charmsOwned >= 40);

            // Ten minutes on a bench at a time.
            if (pd.atBench)
            {
                if (benchSince < 0f) benchSince = Time.unscaledTime;
                if (Time.unscaledTime - benchSince >= 600f) Achievements.Unlock("X02");
            }
            else benchSince = -1f;

            // Mounts ridden (by their kind).
            if (Player.Mounts.Riding)
            {
                int kind = Player.Mounts.Current.Native;
                if (Achievements.Count("mount:" + kind) == 0)
                {
                    Achievements.Set("mount:" + kind, 1);
                    long n = Achievements.Add("mounts");
                    if (n >= 10) Achievements.Unlock("I10");
                    if (n >= 38) Achievements.Unlock("I11");
                }
            }

            // What has ever been carried (the character's collection), and the bag now.
            var inv = mod.Inventory;
            inv.ObserveCollections();
            var got = inv.CollectedItems;
            Check("I06", got.Contains(ItemType.Zenith));
            int pets = 0;
            foreach (var t in got)
            {
                var def = ItemDb.Get(t);
                if (def != null && def.PetBuff > 0) pets++;
            }
            Achievements.Max("pets", pets);
            Check("I08", pets >= 1);
            Check("I09", pets >= 10);
            int spells = 0;
            foreach (var t in SpellWeapons) if (got.Contains(t)) spells++;
            Achievements.Max("spell weapons", spells);
            Check("I17", spells >= SpellWeapons.Length);
            bool fullDifferent = true, luminite = false;
            var seen = bagSeen;
            seen.Clear();
            foreach (var s in inv.Slots)
            {
                if (s.IsEmpty || !seen.Add(s.Type)) fullDifferent = false;
                if (!s.IsEmpty && s.Type == ItemType.LunarBar) luminite = true;
            }
            Check("I22", fullDifferent);
            Check("I24", luminite);
        }

        // ------------------------------------------------------------------ the ground covered

        static Vector2 lastAt;
        static bool haveAt;
        static float walked;

        /// <summary>Every frame: how far the player went (a jump of more than 3 at once is a warp, not counted).</summary>
        static void Odometer()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) { haveAt = false; return; }
            Vector2 at = hero.transform.position;
            if (haveAt)
            {
                float d = Vector2.Distance(at, lastAt);
                if (d < 3f) walked += d;
            }
            lastAt = at;
            haveAt = true;
            if (walked >= 10f)
            {
                long metres = Achievements.Add("metres", (long)walked);
                walked -= (long)walked;
                if (metres >= 100000) Achievements.Unlock("E12");
            }
        }
    }
}
