using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The information accessories (Item.SetDefaults: DefaultToInfoAccessory; what each shows is
        /// UI.InfoAccessories'), working from anywhere in the bag as in Terraria; the mirrors (useStyle 6,
        /// 90 ticks: home, the bench last rested on, not used up); the Rod of Discord and the Rod of
        /// Harmony (to the cursor); the Magnet Flower (the Mana Flower with the Celestial Magnet's reach).
        /// Names and tooltips are zh-Hans.
        /// </summary>
        static void AddInfo(List<ItemDef> list)
        {
            list.Add(Acc2(ItemType.CopperWatch, "铜表", 15, 0, "报时"));
            list.Add(Acc2(ItemType.TinWatch, "锡表", 707, 0, "报时"));
            list.Add(Acc2(ItemType.SilverWatch, "银表", 16, 0, "报时"));
            list.Add(Acc2(ItemType.TungstenWatch, "钨表", 708, 0, "报时"));
            list.Add(Acc2(ItemType.GoldWatch, "金表", 17, 1, "报时"));
            list.Add(Acc2(ItemType.PlatinumWatch, "铂金表", 709, 1, "报时"));
            list.Add(Acc2(ItemType.DepthMeter, "深度计", 18, 1, "显示深度"));
            list.Add(Acc2(ItemType.Compass, "罗盘", 393, 1, "显示水平位置"));
            list.Add(Acc2(ItemType.GPS, "全球定位系统", 395, 3, "显示位置\n报时"));
            list.Add(Acc2(ItemType.FishermansGuide, "渔民袖珍宝典", 3120, 1, "显示钓鱼信息"));
            list.Add(Acc2(ItemType.WeatherRadio, "天气收音机", 3037, 1, "显示天气"));
            list.Add(Acc2(ItemType.Sextant, "六分仪", 3096, 1, "显示月相"));
            list.Add(Acc2(ItemType.FishFinder, "探鱼器", 3036, 3, "显示天气、月相和钓鱼信息"));
            list.Add(Acc2(ItemType.MetalDetector, "金属探测器", 3102, 1, "显示你周围最贵重的矿石"));
            list.Add(Acc2(ItemType.LifeformAnalyzer, "生命体分析机", 3118, 1, "显示你周围稀有生物的名称"));
            list.Add(Acc2(ItemType.DPSMeter, "每秒伤害计数器", 3119, 1, "显示你的每秒伤害"));
            list.Add(Acc2(ItemType.Stopwatch, "秒表", 3099, 1, "显示玩家移动的速度"));
            list.Add(Acc2(ItemType.TallyCounter, "杀怪计数器", 3095, 1, "显示击杀的怪物数量"));
            list.Add(Acc2(ItemType.GoblinTech, "哥布林数据仪", 3121, 3, "显示移动速度、每秒伤害和贵重矿石"));
            list.Add(Acc2(ItemType.REK, "R.E.K.3000", 3122, 3, "显示怪物数量、杀敌数和稀有生物"));
            list.Add(Acc2(ItemType.PDA, "个人数字助手", 3123, 5, "显示所有信息"));
            list.Add(Usable(Acc2(ItemType.CellPhone, "手机", 3124, 7, "显示所有信息\n可随意回家"), 90, "Item_6"));
            list.Add(Usable(new ItemDef { Type = ItemType.MagicMirror, Name = "魔镜", Texture = "Item_50", Rare = 1, Tooltip = "盯着镜子便可回家" }, 90, "Item_6"));
            list.Add(Usable(new ItemDef { Type = ItemType.IceMirror, Name = "冰雪镜", Texture = "Item_3199", Rare = 1, Tooltip = "盯着镜子便可回家" }, 90, "Item_6"));
            list.Add(Usable(new ItemDef { Type = ItemType.RodofDiscord, Name = "混沌传送杖", Texture = "Item_1326", Rare = 7, Tooltip = "将你传送至光标所在位置\n导致混沌状态" }, 20, "Item_8"));
            list.Add(Usable(new ItemDef { Type = ItemType.RodOfHarmony, Name = "和谐传送杖", Texture = "Item_5335", Rare = 10, Tooltip = "将你传送到光标所在位置" }, 20, "Item_8"));
            list.Add(Acc2(ItemType.MagnetFlower, "磁花", 4000, 4, "魔力消耗降低8%\n需要时自动使用魔力药水\n扩大魔力星的拾取范围", bonus: new Bonuses { ManaFlower = true, ManaMagnet = true }));
        }

        /// <summary>A thing used and kept: its use time (ticks) and sound.</summary>
        static ItemDef Usable(ItemDef d, int ticks, string sound)
        {
            d.UseAnimation = ticks * Tick;
            d.UseTime = ticks * Tick;
            d.UseSound = sound;
            return d;
        }

        /// <summary>Their stage, source and value (Item.value).</summary>
        static IEnumerable<(ItemType, int, ItemSource, int)> InfoSources()
        {
            yield return (ItemType.CopperWatch, 0, ItemSource.Craft, 1000);
            yield return (ItemType.TinWatch, 0, ItemSource.Craft, 1500);
            yield return (ItemType.SilverWatch, 0, ItemSource.Craft, 5000);
            yield return (ItemType.TungstenWatch, 1, ItemSource.Craft, 7500);
            yield return (ItemType.GoldWatch, 1, ItemSource.Craft, 10000);
            yield return (ItemType.PlatinumWatch, 1, ItemSource.Craft, 15000);
            yield return (ItemType.DepthMeter, 0, ItemSource.Drop, 12500);
            yield return (ItemType.Compass, 0, ItemSource.Drop, 12500);
            yield return (ItemType.GPS, 1, ItemSource.Craft, 150000);
            yield return (ItemType.FishermansGuide, 0, ItemSource.Drop, 50000);
            yield return (ItemType.WeatherRadio, 0, ItemSource.Drop, 50000);
            yield return (ItemType.Sextant, 0, ItemSource.Drop, 50000);
            yield return (ItemType.FishFinder, 1, ItemSource.Craft, 150000);
            yield return (ItemType.MetalDetector, 0, ItemSource.Drop, 50000);
            yield return (ItemType.LifeformAnalyzer, 0, ItemSource.Drop, 50000);
            yield return (ItemType.DPSMeter, 0, ItemSource.Drop, 50000);
            yield return (ItemType.Stopwatch, 0, ItemSource.Drop, 50000);
            yield return (ItemType.TallyCounter, 2, ItemSource.Drop, 50000);
            yield return (ItemType.GoblinTech, 1, ItemSource.Craft, 150000);
            yield return (ItemType.REK, 2, ItemSource.Craft, 150000);
            yield return (ItemType.PDA, 2, ItemSource.Craft, 250000);
            yield return (ItemType.CellPhone, 2, ItemSource.Craft, 400000);
            yield return (ItemType.MagicMirror, 1, ItemSource.Craft, 50000);
            yield return (ItemType.IceMirror, 0, ItemSource.Drop, 50000);
            yield return (ItemType.RodofDiscord, 4, ItemSource.Drop, 500000);
            yield return (ItemType.RodOfHarmony, 5, ItemSource.Craft, 500000);
            yield return (ItemType.MagnetFlower, 2, ItemSource.Craft, 200000);
        }

        /// <summary>The mirrors: home as the Recall Potion goes, kept.</summary>
        public static bool IsMirror(ItemType t) => t == ItemType.MagicMirror || t == ItemType.IceMirror || t == ItemType.CellPhone;

        /// <summary>The rods that take the player to the cursor.</summary>
        public static bool IsTeleportRod(ItemType t) => t == ItemType.RodofDiscord || t == ItemType.RodOfHarmony;
    }
}
