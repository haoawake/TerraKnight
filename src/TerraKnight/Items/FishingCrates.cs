using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Opening a crate (Player.OpenFishingCrate, a right click in the bag), its contents shared out among Hallownest's
    /// areas: a piece of the area's equipment (an area's crate always one, the special one 1 time in 5: the Fiberglass Fishing
    /// Pole in the Greenpath crate, the Scarab Fishing Rod in Kingdom's Edge's, the Chum Caster in the Waterways', the acid
    /// hook in the acid crate's; Wooden 1 in 4, Iron 1 in 3, Golden 1 in 2, of their stages), and as Terraria's crates
    /// give: geo, ore, bars, potions, healing and mana potions, bait. A hardmode crate's ore and bars are hardmode's half
    /// the time and more. Now and then (1 in 50) a pet, a light pet or a minecart too. The Oyster: a pearl 1 time in 4 and
    /// the shucked oyster.
    /// </summary>
    internal static partial class Fishing
    {
        /// <summary>What a crate may hold besides (the pets, light pets and minecarts; filled where they are defined).</summary>
        internal static readonly List<ItemType> EasterEggs = new List<ItemType>();
        public static int DevOpened;

        public static bool IsCrate(ItemType t) => crates.ContainsKey(t);

        /// <summary>A right click on a crate in the bag: one opened, its contents into the bag (or at the player's feet).</summary>
        public static bool TryOpen(Inventory inv, int slot)
        {
            var stack = inv.Slots[slot];
            if (stack.IsEmpty || !crates.TryGetValue(stack.Type, out var def)) return false;
            var got = new List<ItemStack>();
            Contents(def, got);
            inv.ConsumeAt(slot);
            var hero = HeroController.SilentInstance;
            Vector2 at = hero != null ? (Vector2)hero.transform.position : Vector2.zero;
            foreach (var s in got)
            {
                if (s.Type == ItemType.Geo) { Shop.Earn(s.Count * 100L); continue; }
                int left = inv.Pickup(s.Type, s.Count);
                if (left < s.Count) UI.PopupText.Show(ItemDb.Get(s.Type), s.Count - left, at);
                if (left > 0) WorldItems.Drop(ItemStack.Of(s.Type, left), at);
            }
            TerraMod.Instance?.PlaySound("Grab");
            DevOpened++;
            Plugin.Log.LogInfo($"[钓鱼] 打开 {ItemDb.Get(stack.Type).Name}：{string.Join("，", got.ConvertAll(s => (s.Type == ItemType.Geo ? "吉欧" : ItemDb.Get(s.Type).Name) + "×" + s.Count))}");
            return true;
        }

        static bool Unlocked(ItemType t) => t != ItemType.None && ItemDb.Get(t).Stage <= Shop.UnlockedStage();

        static ItemType Pick(ItemType[] from, int lo = 0, int hi = 9)
        {
            var ok = new List<ItemType>();
            foreach (var t in from)
            {
                var d = ItemDb.Get(t);
                if (d.Stage >= lo && d.Stage <= hi && Unlocked(t)) ok.Add(t);
            }
            return ok.Count == 0 ? ItemType.None : ok[Random.Range(0, ok.Count)];
        }

        static void Put(List<ItemStack> got, ItemType t, int min, int max)
        {
            if (t == ItemType.None) return;
            got.Add(ItemStack.Of(t, Random.Range(min, max + 1)));
        }

        /// <summary>For test reports: what one crate gives.</summary>
        internal static List<ItemStack> DevContents(ItemType crate)
        {
            var got = new List<ItemStack>();
            if (crates.TryGetValue(crate, out var def)) Contents(def, got);
            return got;
        }

        static void Contents(CrateDef c, List<ItemStack> got)
        {
            if (c.Kind == CrateKind.Oyster)
            {
                // Player.OpenOyster: 1 in 4 a pearl (pink 1 in 10, else black 1 in 3, else white); always the shucked oyster.
                if (Random.Range(0, 4) == 0)
                    Put(got, Random.Range(0, 10) == 0 ? ItemType.PinkPearl : Random.Range(0, 3) == 0 ? ItemType.BlackPearl : ItemType.WhitePearl, 1, 1);
                Put(got, ItemType.ShuckedOyster, 1, 1);
                return;
            }
            bool hard = c.Hard;
            // The piece of equipment.
            if (Random.Range(0, c.Den) == 0)
            {
                ItemType piece = ItemType.None;
                if (c.Special != ItemType.None && Random.Range(0, 5) == 0 && Unlocked(c.Special)) piece = c.Special;
                if (piece == ItemType.None) piece = Pick(c.Pool, c.Lo, c.Hi);
                Put(got, piece, 1, 1);
            }
            bool biome = c.Kind != CrateKind.Tier;
            int level = biome ? 3 : c.Level;
            // Geo (Terraria's coins, a fifth: a gold coin is 20 geo here, a silver one 0.2).
            if (biome) { if (Random.Range(0, 4) == 0) got.Add(ItemStack.Of(ItemType.Geo, Random.Range(100, 241))); }
            else if (Random.Range(0, new[] { 7, 4, 3 }[level]) == 0)
                got.Add(ItemStack.Of(ItemType.Geo, level == 0 ? (Random.Range(0, 3) == 0 ? Random.Range(20, 101) : Random.Range(4, 19)) : level == 1 ? Random.Range(40, 201) : Random.Range(100, 301)));
            // Ore and bars, hardmode's in a hardmode crate half the time (bars two times in three).
            int oreDen = biome ? 7 : 7, oreMin = biome ? 20 : new[] { 4, 12, 20 }[level], oreMax = biome ? 35 : new[] { 15, 21, 35 }[level];
            if (Random.Range(0, oreDen) == 0)
                Put(got, hard && Random.Range(0, 2) == 0 && Pick(oresHard) != ItemType.None ? Pick(oresHard) : Pick(oresPre), oreMin, oreMax);
            int barDen = biome ? 4 : new[] { 8, 4, 3 }[level];
            if (Random.Range(0, barDen) == 0)
            {
                var bars = level == 0 ? barsLow : barsPre;
                Put(got, hard && Random.Range(0, 3) != 0 && Pick(barsHard) != ItemType.None ? Pick(barsHard) : Pick(bars), biome ? 6 : new[] { 2, 4, 6 }[level], biome ? 16 : new[] { 5, 10, 16 }[level]);
            }
            // Potions.
            if (Random.Range(0, biome ? 4 : 7) == 0) Put(got, Pick(biome ? potionsBiome : potionsTier), biome ? 2 : 1, biome ? 4 : 3);
            if (Random.Range(0, biome ? 2 : 3) == 0)
            {
                bool mana = Random.Range(0, 2) == 0;
                ItemType heal = level == 0 ? (mana ? ItemType.LesserManaPotion : ItemType.LesserHealingPotion) : (mana ? ItemType.ManaPotion : ItemType.HealingPotion);
                if (hard) heal = mana ? ItemType.GreaterManaPotion : ItemType.GreaterHealingPotion;
                Put(got, Unlocked(heal) ? heal : (mana ? ItemType.LesserManaPotion : ItemType.LesserHealingPotion), 5, biome ? 17 : 15);
            }
            // Bait: the Apprentice's and the Journeyman's in the Wooden crate, the Journeyman's and the Master's in the rest.
            if (Random.Range(0, biome ? 2 : 3) == 0)
            {
                ItemType bait = level == 0 ? (Random.Range(0, 3) != 0 ? ItemType.ApprenticeBait : ItemType.JourneymanBait) : (Random.Range(0, 2) != 0 ? ItemType.JourneymanBait : ItemType.MasterBait);
                Put(got, bait, 1, level == 0 ? 4 : 6);
            }
            // A pet, a light pet or a minecart, now and then.
            if (EasterEggs.Count > 0 && Random.Range(0, 50) == 0) Put(got, EasterEggs[Random.Range(0, EasterEggs.Count)], 1, 1);
            if (got.Count == 0) got.Add(ItemStack.Of(ItemType.Geo, Random.Range(5, 21)));
        }
    }
}

namespace TerraKnight.Items
{
    internal static partial class Fishing
    {
        /// <summary>The quest fish the Angler may ask for at a stage (Main.AnglerQuestSwap's rerolls: what Hallownest can't give, hardmode's).</summary>
        internal static IEnumerable<ItemType> QuestList(int stage)
        {
            foreach (var q in quests) if (q.Stage <= stage) yield return q.Fish;
        }

        /// <summary>Where a quest fish is caught, by Hallownest's areas in the language now.</summary>
        internal static string WhereText(ItemType fish)
        {
            foreach (var q in quests)
                if (q.Fish == fish)
                {
                    var names = new List<string>();
                    foreach (var a in q.Where) names.Add(World.Areas.Display(a));
                    return string.Join(Lang.T("、"), names);
                }
            return "";
        }

        /// <summary>A quest fish's tooltip: where it is caught.</summary>
        internal static string QuestTooltip(ItemType fish) => Lang.F("抓捕位置：{0}", WhereText(fish));
    }
}

namespace TerraKnight.Items
{
    internal static partial class Fishing
    {
        /// <summary>Dev: the ponds of the rooms loaded (area, acid, tiles, scene).</summary>
        internal static List<(Bounds Area, bool Acid, int Tiles, string Scene)> DevPonds()
        {
            Scan();
            var list = new List<(Bounds, bool, int, string)>();
            foreach (var p in ponds) list.Add((p.Area, p.Acid, p.Tiles, p.Scene));
            return list;
        }

        internal static int DevRules => rules.Length;
        internal static int DevCrates => crates.Count;
    }
}

namespace TerraKnight.Items
{
    internal static partial class Fishing
    {
        /// <summary>
        /// The bait Hallownest's enemies drop (Terraria's critters, caught with a bug net, have no Hallownest kin): an ordinary
        /// enemy 1 in 6 one of its area's, an elite 1 in 3, one or two; its area's moss 1 in 12; any of them a gold critter's
        /// 1 in 150. Bosses none. What is above the stage reached doesn't drop.
        /// </summary>
        internal static void KillDrops(string record, Vector2 at, bool direct, string scene = null)
        {
            if (record == null || EnemyDrops.IsBoss(record) || !EnemyDrops.Known(record)) return;
            // The area where it died (the room under the spot, else the one played), not the roster's.
            if (scene == null)
            {
                var cell = World.CellGrid.WorldToCell(at);
                scene = World.TerraWorld.RegionAt(cell.x, cell.y)?.SceneName ?? World.TerraWorld.Live?.SceneName ?? EnemyDrops.SceneOf(record);
            }
            string area = World.Areas.AreaName(scene ?? "");
            bool elite = EnemyDrops.IsEliteRecord(record);
            if (areaBait.TryGetValue(area, out var baits) && Random.Range(0, elite ? 3 : 6) == 0)
            {
                var t = Pick(baits);
                if (t != ItemType.None) EnemyDrops.Spill(ItemStack.Of(t, Random.Range(1, 3)), at, direct);
            }
            if (areaMoss.TryGetValue(area, out var moss) && Random.Range(0, 12) == 0)
            {
                var t = Pick(moss);
                if (t != ItemType.None) EnemyDrops.Spill(ItemStack.Of(t, 1), at, direct);
            }
            if (Random.Range(0, 150) == 0)
            {
                var t = Pick(goldBait);
                if (t != ItemType.None) EnemyDrops.Spill(ItemStack.Of(t, 1), at, direct);
            }
        }
    }
}
