using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The ores and their bars, as in Terraria: enemies drop ore (EnemyDrops turns the bars of
        /// its tables into ore), smelted at a Furnace (Recipe.SetupRecipes: three of copper, tin,
        /// iron, lead, demonite, crimtane, meteorite, cobalt and palladium a bar; four of silver,
        /// tungsten, gold, platinum, mythril and orichalcum; hellstone with obsidian at the Hellforge;
        /// adamantite, titanium and chlorophyte at the Adamantite Forge; four of luminite at the
        /// Ancient Manipulator). The metal armor sets the
        /// mod lacked come with them: tin, lead, silver, tungsten and platinum (Item.SetDefaults
        /// 687-698, 91/82/78), made at an anvil.
        /// </summary>
        static void AddOres(List<ItemDef> list)
        {
            foreach (var o in Ores)
                if (o.Ore != ItemType.IronOre && o.Ore != ItemType.GoldOre && o.Ore != ItemType.Hellstone)
                    list.Add(Mat(o.Ore, o.Name, o.Id, o.Rare, "材料"));

            list.Add(Armor(ItemType.TinHelmet, "锡头盔", 687, ArmorSlot.Head, 47, 2, ArmorSet.Tin, price: 0, tier: 0));
            list.Add(Armor(ItemType.TinChainmail, "锡链甲", 688, ArmorSlot.Body, 28, 2, ArmorSet.Tin, price: 0, tier: 0));
            list.Add(Armor(ItemType.TinGreaves, "锡护胫", 689, ArmorSlot.Legs, 27, 1, ArmorSet.Tin, price: 0, tier: 0));
            list.Add(Armor(ItemType.LeadHelmet, "铅头盔", 690, ArmorSlot.Head, 48, 3, ArmorSet.Lead, price: 0, tier: 0));
            list.Add(Armor(ItemType.LeadChainmail, "铅链甲", 691, ArmorSlot.Body, 29, 3, ArmorSet.Lead, price: 0, tier: 0));
            list.Add(Armor(ItemType.LeadGreaves, "铅护胫", 692, ArmorSlot.Legs, 28, 2, ArmorSet.Lead, price: 0, tier: 0));
            list.Add(Armor(ItemType.SilverHelmet, "银头盔", 91, ArmorSlot.Head, 3, 3, ArmorSet.Silver, price: 0, tier: 0));
            list.Add(Armor(ItemType.SilverChainmail, "银链甲", 82, ArmorSlot.Body, 3, 4, ArmorSet.Silver, price: 0, tier: 0));
            list.Add(Armor(ItemType.SilverGreaves, "银护胫", 78, ArmorSlot.Legs, 3, 3, ArmorSet.Silver, price: 0, tier: 0));
            list.Add(Armor(ItemType.TungstenHelmet, "钨头盔", 693, ArmorSlot.Head, 49, 4, ArmorSet.Tungsten, price: 0, tier: 1));
            list.Add(Armor(ItemType.TungstenChainmail, "钨链甲", 694, ArmorSlot.Body, 30, 5, ArmorSet.Tungsten, price: 0, tier: 1));
            list.Add(Armor(ItemType.TungstenGreaves, "钨护胫", 695, ArmorSlot.Legs, 29, 3, ArmorSet.Tungsten, price: 0, tier: 1));
            list.Add(Armor(ItemType.PlatinumHelmet, "铂金头盔", 696, ArmorSlot.Head, 50, 5, ArmorSet.Platinum, price: 0, tier: 1));
            list.Add(Armor(ItemType.PlatinumChainmail, "铂金链甲", 697, ArmorSlot.Body, 31, 6, ArmorSet.Platinum, price: 0, tier: 1));
            list.Add(Armor(ItemType.PlatinumGreaves, "铂金护胫", 698, ArmorSlot.Legs, 30, 5, ArmorSet.Platinum, price: 0, tier: 1));
        }

        /// <summary>An ore: its item and Terraria id, the bar it smelts into and how many make one, where, and from which stage.</summary>
        internal readonly struct OreInfo
        {
            public readonly ItemType Ore, Bar;
            public readonly string Name, Station;
            public readonly int Id, PerBar, Rare, Stage, StationStage, Value;
            public OreInfo(ItemType ore, string name, int id, ItemType bar, int perBar, int rare, int stage, int value, string station = "熔炉", int stationStage = 0)
            {
                Ore = ore; Name = name; Id = id; Bar = bar; PerBar = perBar; Rare = rare; Stage = stage; Value = value; Station = station; StationStage = stationStage;
            }
        }

        // Built on first use: the item table (defs = Build(), in another file of this class) reads it while
        // this class's other static fields may not be set yet.
        static OreInfo[] ores;
        internal static OreInfo[] Ores => ores ?? (ores = new[]
        {
            new OreInfo(ItemType.CopperOre, "铜矿", 12, ItemType.CopperBar, 3, 0, 0, 250),
            new OreInfo(ItemType.TinOre, "锡矿", 699, ItemType.TinBar, 3, 0, 0, 375),
            new OreInfo(ItemType.IronOre, "铁矿", 11, ItemType.IronBar, 3, 0, 0, 500),
            new OreInfo(ItemType.LeadOre, "铅矿", 700, ItemType.LeadBar, 3, 0, 0, 750),
            new OreInfo(ItemType.SilverOre, "银矿", 14, ItemType.SilverBar, 4, 0, 0, 750),
            new OreInfo(ItemType.TungstenOre, "钨矿", 701, ItemType.TungstenBar, 4, 0, 1, 1125),
            new OreInfo(ItemType.GoldOre, "金矿", 13, ItemType.GoldBar, 4, 0, 1, 1500),
            new OreInfo(ItemType.PlatinumOre, "铂金矿", 702, ItemType.PlatinumBar, 4, 0, 1, 2250),
            new OreInfo(ItemType.DemoniteOre, "魔矿", 56, ItemType.DemoniteBar, 3, 1, 1, 5000),
            new OreInfo(ItemType.Meteorite, "陨石", 116, ItemType.MeteoriteBar, 3, 1, 1, 2333),
            new OreInfo(ItemType.CrimtaneOre, "猩红矿", 880, ItemType.CrimtaneBar, 3, 1, 1, 6500),
            // Hellstone bars take an obsidian each too (OreRecipes).
            new OreInfo(ItemType.Hellstone, "狱石", 174, ItemType.HellstoneBar, 3, 2, 2, 1250, "地狱熔炉", 2),
            new OreInfo(ItemType.CobaltOre, "钴矿", 364, ItemType.CobaltBar, 3, 3, 4, 3500),
            new OreInfo(ItemType.PalladiumOre, "钯金矿", 1104, ItemType.PalladiumBar, 3, 3, 4, 4500),
            new OreInfo(ItemType.MythrilOre, "秘银矿", 365, ItemType.MythrilBar, 4, 3, 3, 5500),
            new OreInfo(ItemType.OrichalcumOre, "山铜矿", 1105, ItemType.OrichalcumBar, 4, 3, 3, 6500),
            new OreInfo(ItemType.AdamantiteOre, "精金矿", 366, ItemType.AdamantiteBar, 4, 3, 4, 7500, "精金熔炉", 4),
            new OreInfo(ItemType.TitaniumOre, "钛金矿", 1106, ItemType.TitaniumBar, 4, 3, 4, 8500, "精金熔炉", 4),
            new OreInfo(ItemType.ChlorophyteOre, "叶绿矿", 947, ItemType.ChlorophyteBar, 5, 7, 4, 9000, "精金熔炉", 4),
            // Luminite into its bars at the Ancient Manipulator (Recipe.cs: 4 a bar, tile 412).
            new OreInfo(ItemType.LunarOre, "夜明矿", 3460, ItemType.LunarBar, 4, 10, 5, 15000, "远古操纵机", 5),
        });

        /// <summary>The ore a bar is smelted from, if any.</summary>
        internal static bool IsOre(ItemType t)
        {
            foreach (var o in Ores) if (o.Ore == t) return true;
            return false;
        }

        internal static bool OreOf(ItemType bar, out OreInfo ore)
        {
            foreach (var o in Ores) if (o.Bar == bar) { ore = o; return true; }
            ore = default;
            return false;
        }

        /// <summary>
        /// The new items' stage, source and value: an ore is worth its bar's share (Item.value: a
        /// copper ore 250, its bar 750), the armor Terraria's own. Applied after the generated table.
        /// </summary>
        static IEnumerable<(ItemType, int, ItemSource, int)> OreSources()
        {
            // Iron and gold ore were there already, at a later stage than the enemies who drop them now; hellstone stays as it was.
            foreach (var o in Ores)
                if (o.Ore != ItemType.Hellstone) yield return (o.Ore, o.Stage, ItemSource.Drop, o.Value);
            yield return (ItemType.TinHelmet, 0, ItemSource.Craft, 1125);
            yield return (ItemType.TinChainmail, 0, ItemSource.Craft, 1875);
            yield return (ItemType.TinGreaves, 0, ItemSource.Craft, 1500);
            yield return (ItemType.LeadHelmet, 0, ItemSource.Craft, 4500);
            yield return (ItemType.LeadChainmail, 0, ItemSource.Craft, 7500);
            yield return (ItemType.LeadGreaves, 0, ItemSource.Craft, 6000);
            yield return (ItemType.SilverHelmet, 0, ItemSource.Craft, 7500);
            yield return (ItemType.SilverChainmail, 0, ItemSource.Craft, 12500);
            yield return (ItemType.SilverGreaves, 0, ItemSource.Craft, 10000);
            yield return (ItemType.TungstenHelmet, 1, ItemSource.Craft, 11250);
            yield return (ItemType.TungstenChainmail, 1, ItemSource.Craft, 18750);
            yield return (ItemType.TungstenGreaves, 1, ItemSource.Craft, 15000);
            yield return (ItemType.PlatinumHelmet, 1, ItemSource.Craft, 22500);
            yield return (ItemType.PlatinumChainmail, 1, ItemSource.Craft, 37500);
            yield return (ItemType.PlatinumGreaves, 1, ItemSource.Craft, 30000);
        }
    }

    internal static partial class Recipes
    {
        /// <summary>Smelting every ore into its bar (Recipe.cs:9652-11988), and the five armor sets at an anvil (9788-10422).</summary>
        static IEnumerable<Recipe> OreRecipes()
        {
            foreach (var o in ItemDb.Ores)
                yield return o.Ore == ItemType.Hellstone
                    ? new Recipe(o.Bar, 1, o.Station, o.StationStage, (ItemType.Obsidian, 1), (o.Ore, o.PerBar))
                    : new Recipe(o.Bar, 1, o.Station, o.StationStage, (o.Ore, o.PerBar));
            yield return new Recipe(ItemType.TinHelmet, 1, "砧", 0, (ItemType.TinBar, 12));
            yield return new Recipe(ItemType.TinChainmail, 1, "砧", 0, (ItemType.TinBar, 20));
            yield return new Recipe(ItemType.TinGreaves, 1, "砧", 0, (ItemType.TinBar, 16));
            yield return new Recipe(ItemType.LeadHelmet, 1, "砧", 0, (ItemType.LeadBar, 15));
            yield return new Recipe(ItemType.LeadChainmail, 1, "砧", 0, (ItemType.LeadBar, 25));
            yield return new Recipe(ItemType.LeadGreaves, 1, "砧", 0, (ItemType.LeadBar, 20));
            yield return new Recipe(ItemType.SilverHelmet, 1, "砧", 0, (ItemType.SilverBar, 15));
            yield return new Recipe(ItemType.SilverChainmail, 1, "砧", 0, (ItemType.SilverBar, 25));
            yield return new Recipe(ItemType.SilverGreaves, 1, "砧", 0, (ItemType.SilverBar, 20));
            yield return new Recipe(ItemType.TungstenHelmet, 1, "砧", 0, (ItemType.TungstenBar, 15));
            yield return new Recipe(ItemType.TungstenChainmail, 1, "砧", 0, (ItemType.TungstenBar, 25));
            yield return new Recipe(ItemType.TungstenGreaves, 1, "砧", 0, (ItemType.TungstenBar, 20));
            yield return new Recipe(ItemType.PlatinumHelmet, 1, "砧", 0, (ItemType.PlatinumBar, 20));
            yield return new Recipe(ItemType.PlatinumChainmail, 1, "砧", 0, (ItemType.PlatinumBar, 30));
            yield return new Recipe(ItemType.PlatinumGreaves, 1, "砧", 0, (ItemType.PlatinumBar, 25));
        }
    }

    internal static partial class EnemyDrops
    {
        /// <summary>
        /// Enemies drop ore, not bars (Terraria's drop bars only from crates and bosses' bags): every
        /// smeltable bar in the tables becomes its ore, as many times over as a bar takes, at the
        /// same chance; a common enemy's twice that (<see cref="OreBoost"/>: the ore is all there is to
        /// mine, and smelting takes its time), a boss's its bars' worth (a boss's ore is Terraria's own measure).
        /// </summary>
        static EnemyDrops()
        {
            AddBossHealing();
            AddRelicRows();
            int changed = 0;
            foreach (var e in table.Values)
            {
                if (e.Drops == null) continue;
                int boost = e.Boss ? 1 : OreBoost;
                for (int i = 0; i < e.Drops.Length; i++)
                {
                    var d = e.Drops[i];
                    // The stage a new row is locked to is the bar's (ores carry none of their own).
                    if (d.LockStage < 0)
                    {
                        var def = ItemDb.Get(d.Type);
                        d.LockStage = def != null ? def.Stage : 0;
                        e.Drops[i] = d;
                    }
                    if (!ItemDb.OreOf(d.Type, out var ore))
                    {
                        // Ore dropped as it is (Terraria's own rows): a common enemy's as much more.
                        if (boost > 1 && ItemDb.IsOre(d.Type))
                        {
                            d.Min *= boost;
                            d.Max *= boost;
                            e.Drops[i] = d;
                        }
                        continue;
                    }
                    d.Type = ore.Ore;
                    d.Min *= ore.PerBar * boost;
                    d.Max *= ore.PerBar * boost;
                    e.Drops[i] = d;
                    changed++;
                }
            }
            DevBarsToOre = changed;
        }

        /// <summary>A common enemy's ore over its bars' worth.</summary>
        public const int OreBoost = 2;

        /// <summary>Dev: how many bar drops the tables had turned into ore.</summary>
        public static int DevBarsToOre;
    }
}
