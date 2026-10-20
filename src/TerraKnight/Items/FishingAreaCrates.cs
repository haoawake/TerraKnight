using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The areas' fishing crates: each of Hallownest's 24 fishing areas has its own crate, and its own coffer once the stage
    /// reached is hardmode's. They are caught where Terraria's biome crates are in its catch rules (the crate roll on a rare
    /// catch: the Crate potion, the fishing power and the luck all help) in that area's waters, and opened by a right click.
    /// The nine areas Terraria's biome crates stood for keep that item (so the saves' stacks stay), under the area's own name
    /// and picture; the other fifteen are new items. What one holds (CrateKind.Area, FishingCrates.Contents): always a piece
    /// of equipment, from what the area's enemies drop at the stage reached and the Terraria crate's own pieces for the area
    /// (the Jungle crate's in Greenpath, the Queen's Gardens and the Hive...), the area's fishing rod 1 time in 5 where it has
    /// one; the area's block (as Terraria's biome crates give their bricks), its torch, a material its enemies drop, its bait
    /// and its moss; then what every biome crate gives (geo, ore, bars, potions, bait). An area whose enemies drop few pieces
    /// (or none) shares its neighbours' and the Golden crate's of the stage.
    /// </summary>
    internal static partial class Fishing
    {
        sealed class AreaCrate
        {
            public string Area, Key, Name, CofferName;
            public ItemType Crate, Coffer, Special;
            public ItemType[] Theme;
            public string[] Near;
        }

        // Terraria's crates' own pieces of equipment (Player.OpenFishingCrate's first roll), for the areas they stood for.
        static readonly ItemType[] jungle = { ItemType.AnkletoftheWind, ItemType.Boomstick, ItemType.FeralClaws, ItemType.StaffofRegrowth, ItemType.FlowerBoots };
        static readonly ItemType[] ocean = { ItemType.WaterWalkingBoots, ItemType.BreathingReed, ItemType.FloatingTube, ItemType.Trident, ItemType.Flipper };
        static readonly ItemType[] corrupt = { ItemType.BallOHurt, ItemType.BandofStarpower, ItemType.Musket, ItemType.ShadowOrb, ItemType.Vilethorn };
        static readonly ItemType[] crimson = { ItemType.TheRottedFork, ItemType.CrimsonRod, ItemType.PanicNecklace, ItemType.CrimsonHeart };
        static readonly ItemType[] frozen = { ItemType.IceBoomerang, ItemType.IceBlade, ItemType.IceSkates, ItemType.SnowballCannon, ItemType.BlizzardinaBottle, ItemType.FlurryBoots, ItemType.GlacierFang };
        static readonly ItemType[] oasis = { ItemType.AncientChisel, ItemType.SandBoots, ItemType.ThunderStaff, ItemType.CatBast, ItemType.SandstorminaBottle };
        static readonly ItemType[] sky = { ItemType.LuckyHorseshoe, ItemType.Starfury, ItemType.ShinyRedBalloon, ItemType.CelestialMagnet };
        // The Dungeon crate's Golden Lock Box.
        static readonly ItemType[] dungeon = { ItemType.Muramasa, ItemType.CobaltShield, ItemType.AquaScepter, ItemType.BlueMoon, ItemType.Handgun, ItemType.MagicMissile, ItemType.Valor };
        static readonly ItemType[] noTheme = new ItemType[0];

        static AreaCrate A(string area, string key, string name, string coffer, ItemType crate, ItemType hard, ItemType[] theme = null, ItemType special = ItemType.None, params string[] near) =>
            new AreaCrate { Area = area, Key = key, Name = name, CofferName = coffer, Crate = crate, Coffer = hard, Theme = theme ?? noTheme, Special = special, Near = near };

        /// <summary>The 24 areas in FishingData's order (the crate pack's numbering: area n's crate is picture 2n-1, its coffer 2n).</summary>
        static readonly AreaCrate[] areaCrates =
        {
            A("国王山道", "KingsPass", "山道匣", "山道秘匣", ItemType.CrateKingsPass, ItemType.CofferKingsPass, null, ItemType.None, "遗忘十字路"),
            A("德特茅斯", "Dirtmouth", "镇灯匣", "镇灯秘匣", ItemType.CrateDirtmouth, ItemType.CofferDirtmouth, null, ItemType.None, "遗忘十字路", "国王山道"),
            A("遗忘十字路", "Crossroads", "旧巢匣", "旧巢秘匣", ItemType.CrateCrossroads, ItemType.CofferCrossroads),
            A("苍绿之径", "Greenpath", "苍绿匣", "苍绿秘匣", ItemType.JungleFishingCrate, ItemType.JungleFishingCrateHard, jungle, ItemType.FiberglassFishingPole),
            A("真菌荒地", "FungalWastes", "菌孢匣", "菌孢秘匣", ItemType.CrateFungalWastes, ItemType.CofferFungalWastes),
            A("雾之峡谷", "FogCanyon", "雾泡匣", "雾泡秘匣", ItemType.OceanCrate, ItemType.OceanCrateHard, ocean),
            A("呼啸悬崖", "HowlingCliffs", "啸风匣", "啸风秘匣", ItemType.CrateHowlingCliffs, ItemType.CofferHowlingCliffs, sky, ItemType.None, "苍绿之径"),
            A("泪水之城", "CityOfTears", "泪城匣", "泪城秘匣", ItemType.DungeonFishingCrate, ItemType.DungeonFishingCrateHard, dungeon),
            A("皇家水道", "RoyalWaterways", "锈渠匣", "锈渠秘匣", ItemType.CorruptFishingCrate, ItemType.CorruptFishingCrateHard, corrupt, ItemType.BloodFishingRod),
            A("水晶山峰", "CrystalPeak", "晶簇匣", "晶簇秘匣", ItemType.CrateCrystalPeak, ItemType.CofferCrystalPeak),
            A("安息之地", "RestingGrounds", "安息匣", "安息秘匣", ItemType.FrozenCrate, ItemType.FrozenCrateHard, frozen),
            A("深巢", "Deepnest", "蛛丝匣", "蛛丝秘匣", ItemType.CrimsonFishingCrate, ItemType.CrimsonFishingCrateHard, crimson),
            A("野兽巢穴", "BeastsDen", "兽巢匣", "兽巢秘匣", ItemType.CrateBeastsDen, ItemType.CofferBeastsDen, crimson, ItemType.None, "深巢"),
            A("远古盆地", "AncientBasin", "沉岩匣", "沉岩秘匣", ItemType.CrateAncientBasin, ItemType.CofferAncientBasin),
            A("王后花园", "QueensGardens", "荆花匣", "荆花秘匣", ItemType.CrateQueensGardens, ItemType.CofferQueensGardens, jungle, ItemType.FiberglassFishingPole, "苍绿之径"),
            A("王国边缘", "KingdomsEdge", "白灰匣", "白灰秘匣", ItemType.OasisCrate, ItemType.OasisCrateHard, oasis, ItemType.ScarabFishingRod, "皇家水道"),
            A("愚人斗兽场", "Colosseum", "斗兽匣", "斗兽秘匣", ItemType.CrateColosseum, ItemType.CofferColosseum, oasis, ItemType.ScarabFishingRod, "王国边缘", "皇家水道"),
            A("蜂巢", "Hive", "蜂蜡匣", "蜂蜡秘匣", ItemType.CrateHive, ItemType.CofferHive, jungle, ItemType.None, "王国边缘", "皇家水道"),
            A("深渊", "Abyss", "虚空匣", "虚空秘匣", ItemType.CrateAbyss, ItemType.CofferAbyss, null, ItemType.None, "远古盆地"),
            A("白色宫殿", "WhitePalace", "白宫匣", "白宫秘匣", ItemType.HallowedFishingCrate, ItemType.HallowedFishingCrateHard, null, ItemType.None, "远古盆地"),
            A("神居", "Godhome", "神辉匣", "神辉秘匣", ItemType.FloatingIslandFishingCrate, ItemType.FloatingIslandFishingCrateHard, sky, ItemType.None, "白色宫殿", "远古盆地"),
            A("黑卵神殿", "BlackEggTemple", "黑卵匣", "黑卵秘匣", ItemType.CrateBlackEggTemple, ItemType.CofferBlackEggTemple, null, ItemType.None, "遗忘十字路"),
            A("梦境", "Dream", "梦光匣", "梦光秘匣", ItemType.CrateDream, ItemType.CofferDream, null, ItemType.None, "安息之地"),
            A("格林剧团", "GrimmTroupe", "梦魇匣", "梦魇秘匣", ItemType.CrateGrimmTroupe, ItemType.CofferGrimmTroupe, null, ItemType.None, "德特茅斯", "遗忘十字路"),
        };

        static readonly Dictionary<string, AreaCrate> areaCrateByArea = new Dictionary<string, AreaCrate>();
        static readonly Dictionary<ItemType, AreaCrate> areaCrateByType = new Dictionary<ItemType, AreaCrate>();

        /// <summary>Terraria's biome crates' catch rules: in their place, the area's own crate.</summary>
        static readonly HashSet<ItemType> biomeCrates = new HashSet<ItemType>
        {
            ItemType.DungeonFishingCrate, ItemType.DungeonFishingCrateHard, ItemType.OceanCrate, ItemType.OceanCrateHard, ItemType.CorruptFishingCrate,
            ItemType.CorruptFishingCrateHard, ItemType.CrimsonFishingCrate, ItemType.CrimsonFishingCrateHard, ItemType.HallowedFishingCrate,
            ItemType.HallowedFishingCrateHard, ItemType.JungleFishingCrate, ItemType.JungleFishingCrateHard, ItemType.FrozenCrate, ItemType.FrozenCrateHard,
            ItemType.OasisCrate, ItemType.OasisCrateHard, ItemType.FloatingIslandFishingCrate, ItemType.FloatingIslandFishingCrateHard,
        };

        static bool areaCratesReady;

        /// <summary>The area crates into the crate table (in place of the biome crates' contents), once.</summary>
        static void EnsureAreaCrates()
        {
            if (areaCratesReady) return;
            areaCratesReady = true;
            foreach (var a in areaCrates)
            {
                areaCrateByArea[a.Area] = a;
                areaCrateByType[a.Crate] = a;
                areaCrateByType[a.Coffer] = a;
                crates[a.Crate] = new CrateDef(CrateKind.Area, 3, false, 1, 0, 5, a.Special, a.Theme) { Area = a.Area };
                crates[a.Coffer] = new CrateDef(CrateKind.Area, 3, true, 1, 0, 5, a.Special, a.Theme) { Area = a.Area };
            }
        }

        /// <summary>The crate an area's waters give at a stage (its coffer in hardmode), or none for a place not one of the 24.</summary>
        internal static ItemType AreaCrateFor(string area, bool hard)
        {
            EnsureAreaCrates();
            return area != null && areaCrateByArea.TryGetValue(area, out var a) ? (hard ? a.Coffer : a.Crate) : ItemType.None;
        }

        /// <summary>The area a crate is of (null for the tier and acid crates).</summary>
        internal static string AreaOfCrate(ItemType t)
        {
            EnsureAreaCrates();
            return areaCrateByType.TryGetValue(t, out var a) ? a.Area : null;
        }

        /// <summary>
        /// The items: the fifteen new ones, the nine biome crates under the areas' names. Their pictures: the reused crates'
        /// (and the tier and acid crates') as Resources/hk_Item_&lt;id&gt;.png over Terraria's, the new ones' HkCrate_/HkCoffer_&lt;key&gt;.
        /// </summary>
        internal static void AddAreaCrates(List<ItemDef> list)
        {
            var have = new Dictionary<ItemType, ItemDef>();
            foreach (var d in list) have[d.Type] = d;
            foreach (var a in areaCrates)
                foreach (bool hard in new[] { false, true })
                {
                    var t = hard ? a.Coffer : a.Crate;
                    string name = hard ? a.CofferName : a.Name;
                    if (have.TryGetValue(t, out var d)) { d.Name = name; continue; }
                    list.Add(new ItemDef
                    {
                        Type = t, Name = name, Texture = (hard ? "HkCoffer_" : "HkCrate_") + a.Key, Opens = true, MaxStack = 9999, Rare = 2, Tooltip = "右键单击打开", UseSound = null,
                        // Hardmode coffers follow the same stage-3 gate as fishing.
                        Source = ItemSource.Drop, Value = 50000, Price = 500, Stage = hard ? 3 : 0,
                    });
                }
        }

        // ------------------------------------------------------------------ what an area crate holds

        sealed class AreaLoot
        {
            public int Stamp = -1;
            public ItemType[] Gear = new ItemType[0], Stuff = new ItemType[0];
        }

        static readonly Dictionary<string, AreaLoot> areaLoot = new Dictionary<string, AreaLoot>();
        static readonly HashSet<ItemType> crateMetals = new HashSet<ItemType>();

        /// <summary>
        /// What the area's enemies (bosses not) drop at the stage reached: their pieces of equipment, and the materials (not
        /// ore or bars, which every crate gives anyway).
        /// </summary>
        static AreaLoot LootOf(string area)
        {
            int stage = Shop.UnlockedStage();
            bool entry = ProgressionBalance.EntryClear, final = ProgressionBalance.FinalClear;
            int stamp = stage * 4 + (entry ? 1 : 0) + (final ? 2 : 0);
            if (!areaLoot.TryGetValue(area, out var loot)) areaLoot[area] = loot = new AreaLoot();
            if (loot.Stamp == stamp) return loot;
            loot.Stamp = stamp;
            var (gear, stuff) = AreaLootAt(area, stage, entry, final);
            loot.Gear = gear;
            loot.Stuff = stuff;
            return loot;
        }

        /// <summary>An area's enemies' pieces of equipment and materials at a stage (World.RegionalChests' too).</summary>
        internal static (ItemType[] Gear, ItemType[] Stuff) AreaLootAt(string area, int stage, bool entry = false, bool final = false)
        {
            if (crateMetals.Count == 0)
                foreach (var set in new[] { oresPre, oresHard, barsPre, barsLow, barsHard }) crateMetals.UnionWith(set);
            var gear = new HashSet<ItemType>();
            var stuff = new HashSet<ItemType>();
            foreach (var record in EnemyDrops.DevRecords)
            {
                if (EnemyDrops.IsBoss(record) || World.Areas.AreaName(EnemyDrops.SceneOf(record) ?? "") != area) continue;
                foreach (var d in EnemyDrops.At(record, stage, false, entry, final))
                {
                    var def = ItemDb.Get(d.Type);
                    if (def == null || def.HkItem || d.Master) continue;
                    if (def.MaxStack == 1 && (def.IsWeapon && !def.IsAmmo && !def.GameTool || def.IsAccessory || def.IsArmor)) gear.Add(d.Type);
                    else if (def.MaxStack > 1 && Recipes.IsMaterial(d.Type) && !def.IsWeapon && !def.IsAmmo && def.BaitPower == 0 && def.HealLife == 0 && def.HealMana == 0
                             && !def.LifeCrystal && !def.ManaCrystal && def.Boost == PermanentBoost.None && d.Type != ItemType.Geo && d.Type != ItemType.Heart && !crateMetals.Contains(d.Type))
                        stuff.Add(d.Type);
                }
            }
            return (new List<ItemType>(gear).ToArray(), new List<ItemType>(stuff).ToArray());
        }

        /// <summary>The areas an area shares its crate's pieces with when its own enemies drop few (none for most).</summary>
        internal static string[] NearAreas(string area)
        {
            EnsureAreaCrates();
            return area != null && areaCrateByArea.TryGetValue(area, out var a) ? a.Near : new string[0];
        }

        /// <summary>A pick among items (Fishing's own filter: unlocked, not shop- or boss-only), none if none qualifies.</summary>
        internal static ItemType PickUnlocked(ItemType[] from, int lo = 0, int hi = 9) => Pick(from, lo, hi);

        /// <summary>Fewer of the area's own pieces than this: its neighbours' join them, then the Golden crate's of the stage.</summary>
        const int FewPieces = 8;

        /// <summary>
        /// The area crate's piece of equipment: its area's (and its Terraria crate's); an area whose enemies drop few (or
        /// none: Dirtmouth, the Black Egg Temple, the Troupe's tent) with its neighbours' and the Golden crate's of the stage.
        /// </summary>
        static ItemType AreaPiece(CrateDef c)
        {
            EnsureAreaCrates();
            if (!areaCrateByArea.TryGetValue(c.Area ?? "", out var a)) return ItemType.None;
            var own = LootOf(a.Area).Gear;
            var pool = new List<ItemType>(own);
            pool.AddRange(a.Theme);
            if (own.Length < FewPieces)
                foreach (var near in a.Near) pool.AddRange(LootOf(near).Gear);
            if (own.Length < FewPieces)
            {
                int stage = Shop.UnlockedStage();
                foreach (var t in crates[c.Hard ? ItemType.GoldenCrateHard : ItemType.GoldenCrate].Pool)
                {
                    var d = ItemDb.Get(t);
                    if (d.MaxStack == 1 && d.Stage >= stage - 1 && d.Stage <= stage) pool.Add(t);
                }
            }
            var piece = Pick(pool.ToArray());
            if (piece == ItemType.None) piece = Pick(crates[c.Hard ? ItemType.GoldenCrateHard : ItemType.GoldenCrate].Pool);
            return piece;
        }

        /// <summary>The area's own: its block and its torch (whatever the stage: they are of the waters fished), a material its enemies drop, its bait and moss.</summary>
        static void AreaExtras(CrateDef c, List<ItemStack> got)
        {
            if (!areaCrateByArea.TryGetValue(c.Area ?? "", out var a)) return;
            for (int i = 0; i < World.AreaBlocks.All.Length; i++)
                if (World.AreaBlocks.All[i].Key == a.Key && Random.Range(0, 2) == 0) got.Add(ItemStack.Of(World.AreaBlocks.BlockItem(i), Random.Range(25, 51)));
            for (int s = 1; s <= World.RegionalTorches.Count; s++)
                if (World.RegionalTorches.All[s - 1].Key == a.Key && Random.Range(0, 3) == 0) got.Add(ItemStack.Of(World.RegionalTorches.ItemOf(s), Random.Range(10, 21)));
            if (Random.Range(0, 2) == 0)
            {
                var stuff = LootOf(a.Area).Stuff;
                foreach (var near in a.Near) if (stuff.Length == 0) stuff = LootOf(near).Stuff;
                if (stuff.Length > 0) Put(got, Pick(stuff), c.Hard ? 8 : 5, c.Hard ? 20 : 15);
            }
            if (areaBait.TryGetValue(a.Area, out var baits) && Random.Range(0, 3) == 0) Put(got, Pick(baits), 1, 3);
            if (areaMoss.TryGetValue(a.Area, out var moss) && Random.Range(0, 4) == 0) Put(got, Pick(moss), 1, 2);
        }

        /// <summary>Dev: an area's crate pieces and materials at the stage reached (the report of the crate suite).</summary>
        internal static (string Crate, string Coffer, int Gear, int Theme, int Stuff) DevAreaCrate(string area)
        {
            EnsureAreaCrates();
            if (!areaCrateByArea.TryGetValue(area, out var a)) return (null, null, 0, 0, 0);
            var loot = LootOf(area);
            return (ItemDb.Get(a.Crate).Name, ItemDb.Get(a.Coffer).Name, loot.Gear.Length, a.Theme.Length, loot.Stuff.Length);
        }

        internal static IEnumerable<string> DevAreaCrateAreas()
        {
            foreach (var a in areaCrates) yield return a.Area;
        }
    }
}
