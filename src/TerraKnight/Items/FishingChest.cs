using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Fishing Chest (the user's art, Resources/fishing_chest.png): now and then on the line instead of the catch (1 in
    /// 25, 1 in 12 with the Crate potion; never in place of the Angler's fish), opened from the bag like a crate. Inside:
    /// bait — the Apprentice Bait always, the Journeyman's 1 time in 2, the Master's 1 in 4, and a few of the critters that
    /// are bait — and sometimes more: a fishing potion 1 in 3, a fishing accessory 1 in 12, a fish weapon or fishing tool
    /// 1 in 15. Only what the stage reached has unlocked.
    /// </summary>
    internal static class FishingChest
    {
        public const ItemType Type = ItemType.FishingChest;
        public static int DevOpened, DevCaught;

        public static ItemDef Def() => new ItemDef
        {
            Type = Type, Name = "钓鱼宝箱", Texture = Relics.TexturePrefix + "fishing_chest", Opens = true, MaxStack = 9999, Rare = 2,
            Value = 20000, Stage = 0, Source = ItemSource.Drop, UseSound = null,
            Tooltip = "右键打开\n装着各种鱼饵，偶尔有钓鱼饰品、鱼类武器、钓鱼工具和药水",
        };

        /// <summary>A catch about to bite: the chest instead, 1 in 25 (1 in 12 under the Crate potion).</summary>
        public static bool Caught(bool cratePotion)
        {
            if (Random.Range(0, cratePotion ? 12 : 25) != 0) return false;
            DevCaught++;
            return true;
        }

        static readonly string[] accessories =
        {
            "HighTestFishingLine", "AnglerEarring", "TackleBox", "FishingBobber", "FishingBobberGlowingStar", "FishingBobberGlowingRainbow",
            "LavaFishingHook", "AnglerTackleBag", "LavaproofTackleBag", "FishermansGuide", "WeatherRadio", "Sextant", "FishFinder",
        };

        /// <summary>The fish that are weapons or tools (Terraria's caught ones), and the rods.</summary>
        static readonly string[] fishGear =
        {
            "PurpleClubberfish", "Swordfish", "Bladetongue", "Toxikarp", "ObsidianSwordfish", "FrostDaggerfish", "ReaverShark", "SawtoothShark",
            "Rockfish", "WoodFishingPole", "ReinforcedFishingPole", "FiberglassFishingPole", "ScarabFishingRod", "MechanicsRod",
            "SittingDucksFishingRod", "HotlineFishingHook", "GoldenFishingRod", "BloodFishingRod", "FisherofSouls", "Fleshcatcher", "ChumCaster",
        };

        static readonly string[] potions = { "FishingPotion", "SonarPotion", "CratePotion" };

        static readonly HashSet<ItemType> specialBait = new HashSet<ItemType> { ItemType.ApprenticeBait, ItemType.JourneymanBait, ItemType.MasterBait };

        /// <summary>What the stage reached has of these (an item the mod lacks is left out).</summary>
        static List<ItemType> Available(IEnumerable<string> names)
        {
            var ok = new List<ItemType>();
            int stage = Shop.UnlockedStage();
            foreach (var n in names)
                if (System.Enum.TryParse(n, out ItemType t) && Usable(t, stage)) ok.Add(t);
            return ok;
        }

        static bool Usable(ItemType t, int stage)
        {
            var d = ItemDb.Get(t);
            return d != null && d.Name != t.ToString() && d.Stage <= stage && ProgressionBalance.Unlocked(t);
        }

        /// <summary>The critters that are bait, now: not the three made baits, not the golden ones or the Truffle Worm.</summary>
        static List<ItemType> Critters()
        {
            var ok = new List<ItemType>();
            int stage = Shop.UnlockedStage();
            foreach (var d in ItemDb.All)
            {
                if (d.BaitPower <= 0 || specialBait.Contains(d.Type)) continue;
                string n = d.Type.ToString();
                if (n.StartsWith("Gold") || n == "TruffleWorm") continue;
                if (Usable(d.Type, stage)) ok.Add(d.Type);
            }
            return ok;
        }

        static T Any<T>(List<T> from) => from[Random.Range(0, from.Count)];

        public static void Contents(List<ItemStack> got)
        {
            got.Add(ItemStack.Of(ItemType.ApprenticeBait, Random.Range(2, 6)));
            if (Random.Range(0, 2) == 0) got.Add(ItemStack.Of(ItemType.JourneymanBait, Random.Range(1, 4)));
            if (Random.Range(0, 4) == 0) got.Add(ItemStack.Of(ItemType.MasterBait, Random.Range(1, 3)));
            var critters = Critters();
            for (int i = 0; i < 3 && critters.Count > 0; i++)
            {
                var t = Any(critters);
                critters.Remove(t);
                got.Add(ItemStack.Of(t, Random.Range(2, 6)));
            }
            var pot = Available(potions);
            if (pot.Count > 0 && Random.Range(0, 3) == 0) got.Add(ItemStack.Of(Any(pot), Random.Range(1, 3)));
            var acc = Available(accessories);
            if (acc.Count > 0 && Random.Range(0, 12) == 0) got.Add(ItemStack.Of(Any(acc)));
            var gear = Available(fishGear);
            if (gear.Count > 0 && Random.Range(0, 15) == 0) got.Add(ItemStack.Of(Any(gear)));
        }

        /// <summary>A right click on a chest in the bag: one opened, its contents into the bag (or at the player's feet).</summary>
        public static bool TryOpen(Inventory inv, int slot)
        {
            var stack = inv.Slots[slot];
            if (stack.IsEmpty || stack.Type != Type) return false;
            var got = new List<ItemStack>();
            Contents(got);
            inv.ConsumeAt(slot);
            var hero = HeroController.SilentInstance;
            Vector2 at = hero != null ? (Vector2)hero.transform.position : Vector2.zero;
            foreach (var s in got)
            {
                int left = inv.Pickup(s.Type, s.Count);
                if (left < s.Count) UI.PopupText.Show(ItemDb.Get(s.Type), s.Count - left, at);
                if (left > 0) WorldItems.Drop(ItemStack.Of(s.Type, left), at);
            }
            TerraMod.Instance?.PlaySound("Grab");
            DevOpened++;
            Plugin.Log.LogInfo($"[钓鱼] 打开钓鱼宝箱：{string.Join("，", got.ConvertAll(s => ItemDb.Get(s.Type).Name + "×" + s.Count))}");
            return true;
        }
    }
}
