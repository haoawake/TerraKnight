namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// Every pair of wings the mod lacked, the Soaring Insignia, and the boots and feet
        /// (accarmor-hm sec-G1): Item.SetDefaults, zh-Hans names and tooltips; each wing's numbers
        /// and draw are its slot's row in <see cref="Player.TerraWings"/>.
        /// </summary>
        static void AddAccessories3(System.Collections.Generic.List<ItemDef> list)
        {
            const string fly = "可飞行和缓慢坠落", dev = "“非常适合冒充开发者！”\n" + fly, hover = "\n按向下键可开关悬停状态\n按向上键可停用悬停", up = "\n按住向上键可以更快上升！";
            ItemDef Wing(ItemType t, string name, int id, int rare, int slot, string tip) => Acc2(t, name, id, rare, tip, bonus: new Bonuses { WingSlot = slot });
            list.Add(Wing(ItemType.RedsWings, "Red的翅膀", 665, 9, 3, dev));
            list.Add(Wing(ItemType.Jetpack, "喷气背包", 748, 5, 4, fly + up));
            list.Add(Wing(ItemType.ButterflyWings, "蝴蝶之翼", 749, 5, 5, fly));
            list.Add(Wing(ItemType.FairyWings, "仙灵之翼", 761, 5, 6, fly));
            list.Add(Wing(ItemType.HarpyWings, "鸟妖之翼", 785, 5, 7, fly));
            list.Add(Wing(ItemType.BoneWings, "骨之翼", 786, 5, 8, fly));
            list.Add(Wing(ItemType.FlameWings, "烈焰之翼", 821, 5, 9, fly));
            list.Add(Wing(ItemType.FrozenWings, "冰冻之翼", 822, 5, 10, fly));
            list.Add(Wing(ItemType.GhostWings, "幽灵之翼", 823, 8, 11, fly));
            list.Add(Wing(ItemType.SteampunkWings, "蒸汽朋克之翼", 948, 8, 12, fly));
            list.Add(Wing(ItemType.LeafWings, "叶之翼", 1162, 5, 13, fly));
            list.Add(Wing(ItemType.BatWings, "蝙蝠之翼", 1165, 5, 14, fly));
            list.Add(Wing(ItemType.BeeWings, "蜜蜂之翼", 1515, 5, 15, fly));
            list.Add(Wing(ItemType.DTownsWings, "D-Town的翅膀", 1583, 9, 16, dev));
            list.Add(Wing(ItemType.WillsWings, "Will的翅膀", 1584, 9, 17, dev));
            list.Add(Wing(ItemType.CrownosWings, "Crowno的翅膀", 1585, 9, 18, dev));
            list.Add(Wing(ItemType.CenxsWings, "Cenx的翅膀", 1586, 9, 19, dev));
            list.Add(Wing(ItemType.TatteredFairyWings, "褴褛仙灵之翼", 1797, 7, 20, fly));
            list.Add(Wing(ItemType.SpookyWings, "阴森之翼", 1830, 7, 21, fly));
            list.Add(Wing(ItemType.Hoverboard, "悬浮板", 1866, 5, 22, fly + hover));
            list.Add(Wing(ItemType.FestiveWings, "喜庆之翼", 1871, 5, 23, fly));
            list.Add(Wing(ItemType.BeetleWings, "甲虫之翼", 2280, 7, 24, fly));
            list.Add(Wing(ItemType.FinWings, "鳍翼", 2494, 4, 25, fly));
            list.Add(Wing(ItemType.FishronWings, "猪龙鱼之翼", 2609, 8, 26, fly + "\n允许在水中快速行进"));
            list.Add(Wing(ItemType.MothronWings, "蛾怪之翼", 2770, 8, 27, fly));
            list.Add(Wing(ItemType.BejeweledValkyrieWing, "Lazure的屏障台", 3228, 9, 28, dev + "\n女武神卫星屏障台是完全安全的。大多数时候是。"));
            list.Add(Wing(ItemType.WingsSolar, "日耀之翼", 3468, 10, 29, fly));
            list.Add(Wing(ItemType.WingsVortex, "星旋强化翼", 3469, 10, 30, fly + hover));
            list.Add(Wing(ItemType.WingsNebula, "星云斗篷", 3470, 10, 31, fly + hover));
            list.Add(Wing(ItemType.WingsStardust, "星尘之翼", 3471, 10, 32, fly));
            list.Add(Wing(ItemType.Yoraiz0rWings, "Yoraiz0r的魔法", 3580, 9, 33, dev + "\n无论此配饰对你产生什么作用都不是Bug！"));
            list.Add(Wing(ItemType.JimsWings, "Jim的翅膀", 3582, 9, 34, dev));
            list.Add(Wing(ItemType.SkiphsWings, "Skiphs的爪子", 3588, 9, 35, dev));
            list.Add(Wing(ItemType.LokisWings, "Loki的翅膀", 3592, 9, 36, dev + "\n难知如阴，动如雷震。"));
            list.Add(Wing(ItemType.BetsyWings, "双足翼龙之翼", 3883, 8, 37, fly + hover));
            list.Add(Wing(ItemType.ArkhalisWings, "Arkhalis的飞翼", 3924, 9, 38, "“非常适合冒充开发者！”\n“我不是从网格中拿到这个的”\n" + fly));
            list.Add(Wing(ItemType.LeinforsWings, "Leinfors的卷缠斗篷", 3928, 9, 39, dev + "\n“它已全开！啥意思啊？！”"));
            list.Add(Wing(ItemType.GhostarsWings, "Ghostar的无极翼", 4730, 9, 40, dev + "\n“我就在这里”"));
            list.Add(Wing(ItemType.SafemanWings, "Safeman的毛毯斗篷", 4746, 9, 41, dev + "\n“你能帮我把这个系上吗？”"));
            list.Add(Wing(ItemType.FoodBarbarianWings, "FoodBarbarian的褴褛龙之翼", 4750, 9, 42, dev + "\n“这些洞能减轻重量。”"));
            list.Add(Wing(ItemType.GroxTheGreatWings, "Grox The Great的翅膀", 4754, 9, 43, dev));
            list.Add(Wing(ItemType.RainbowWings, "女皇之翼", 4823, 9, 44, fly + up));
            list.Add(Wing(ItemType.LongRainbowTrailWings, "天界星盘", 4954, -12, 45, fly + hover + up + "\n“你懂得越多”"));
            list.Add(Wing(ItemType.ChickenBonesWings, "鸡骨翅膀", 5586, 9, 47, dev));
            list.Add(Wing(ItemType.ChippysWings, "花栗鼠斗篷", 5627, 9, 48, "“非常适合冒充泰拉瑞亚的热心人！”\n" + fly));
            list.Add(Wing(ItemType.HeroicisWings, "英雄翅膀", 5659, 9, 49, fly + "\n“每遇挫折，便重新开始...且变得更明智。”"));
            list.Add(Wing(ItemType.KazzymodusWings, "卡兹默德斯之翼", 5686, 9, 50, dev));
            list.Add(Wing(ItemType.LunasWings, "露娜的符文妖精翅膀", 6140, 9, 51, dev));
            list.Add(Acc2(ItemType.EmpressFlightBooster, "翱翔徽章", 4989, -12, "给予无限翅膀飞行时间和无限火箭靴飞行时间\n提高飞行和跳跃机动性",
                bonus: new Bonuses { EmpressBrooch = true, MoveSpeed = 0.075f }));

            // --- Boots and feet (G1.3.8). Water walking, ice, lava, hot blocks and flowers have nothing to act on in Hallownest. ---
            list.Add(Acc2(ItemType.WaterWalkingBoots, "水上漂靴", 863, 4, "可让人在水和蜂蜜上行走", bonus: new Bonuses { WaterWalk = 1 }));
            list.Add(Acc2(ItemType.ObsidianWaterWalkingBoots, "黑曜石水上漂靴", 907, 4, "可让人在水和蜂蜜上行走\n对火块免疫", bonus: new Bonuses { WaterWalk = 1, FireWalk = true }));
            list.Add(Acc2(ItemType.MoltenCharm, "熔火护身符", 4038, 5, "7秒内对熔岩免疫\n对火块免疫", bonus: new Bonuses { LavaImmuneTicks = 420, FireWalk = true }));
            list.Add(Acc2(ItemType.LavaWaders, "熔岩靴", 908, 7, "可让人在水、蜂蜜和熔岩上行走\n对火块免疫并在7秒内对熔岩免疫\n减少因触碰熔岩而受到的伤害",
                bonus: new Bonuses { WaterWalk = 2, FireWalk = true, LavaImmuneTicks = 420, LavaRose = true }));
            list.Add(Acc2(ItemType.IceSkates, "溜冰鞋", 950, 1, "提供额外冰面行动力\n落到冰上时冰不会碎", bonus: new Bonuses { IceSkate = true }));
            list.Add(Acc2(ItemType.FlurryBoots, "疾风雪靴", 1579, 1, "穿戴者可飞速奔跑", bonus: new Bonuses { AccRunSpeed = 6f, RunDust = RunDustKind.Cold }));
            list.Add(Acc2(ItemType.SailfishBoots, "旗鱼靴", 3200, 1, "穿戴者可飞速奔跑", bonus: new Bonuses { AccRunSpeed = 6f, RunDust = RunDustKind.Sail }));
            list.Add(Acc2(ItemType.SandBoots, "沙丘行者靴", 4055, 1, "穿戴者可飞速奔跑，在沙地上还能跑得更快\n“无节律行走就不会引来蠕虫”",
                bonus: new Bonuses { AccRunSpeed = 6f, DesertBoots = true, RunDust = RunDustKind.Desert }));
            list.Add(Acc2(ItemType.FairyBoots, "仙灵靴", 3993, 5, "可飞行\n穿戴者可飞速奔跑\n你走过的草地上会长出花朵",
                bonus: new Bonuses { AccRunSpeed = 6f, RocketKind = 2, FairyDust = true, FlowerBoots = true, RunDust = RunDustKind.Fairy }));
            list.Add(Acc2(ItemType.FlowerBoots, "花靴", 3017, 7, "你走过的草地上会长出花朵", bonus: new Bonuses { FlowerBoots = true }));
            list.Add(Acc2(ItemType.AmphibianBoots, "水陆两用靴", 3990, 5, "穿戴者可飞速奔跑\n提高跳跃速度，并可自动跳跃\n提高掉落抗性",
                bonus: new Bonuses { AccRunSpeed = 6f, JumpSpeedBoost = 1.6f, AutoJump = true, ExtraFall = 10, RunDust = RunDustKind.Sail }));
            list.Add(Acc2(ItemType.FrogFlipper, "青蛙脚蹼", 3994, 5, "给予游泳能力\n提高跳跃速度，并可自动跳跃\n提高掉落抗性",
                bonus: new Bonuses { JumpSpeedBoost = 1.6f, AutoJump = true, ExtraFall = 10, Swim = true }));
            list.Add(Acc2(ItemType.FrogGear, "青蛙装备", 3995, 5, "给予游泳能力\n可爬墙\n提高跳跃速度，并可自动跳跃\n提高掉落抗性\n“身为绿皮生物可太难了”",
                bonus: new Bonuses { JumpSpeedBoost = 1.6f, AutoJump = true, ExtraFall = 10, Swim = true, SpikedBoots = 2 }));
            list.Add(Acc2(ItemType.FrogWebbing, "青蛙蹼", 3996, 5, "可爬墙\n提高跳跃速度，并可自动跳跃\n提高掉落抗性",
                bonus: new Bonuses { JumpSpeedBoost = 1.6f, AutoJump = true, ExtraFall = 10, SpikedBoots = 2 }));
            list.Add(Acc2(ItemType.FrostsparkBoots, "霜花靴", 1862, 7, "可飞行、飞速奔跑、并提供额外冰面行动力\n移动速度提高8%",
                bonus: new Bonuses { AccRunSpeed = 6.75f, RocketKind = 3, MoveSpeed = 0.08f, IceSkate = true }));
            list.Add(Acc2(ItemType.TerrasparkBoots, "泰拉闪耀靴", 5000, 7, "可飞行、飞速奔跑，并提供额外冰面行动力\n移动速度提高8%\n可让人在水、蜂蜜和熔岩上行走\n对火块免疫并在7秒内对熔岩免疫\n减少因触碰熔岩而受到的伤害",
                bonus: new Bonuses { AccRunSpeed = 6.75f, RocketKind = 4, MoveSpeed = 0.08f, IceSkate = true, WaterWalk = 2, FireWalk = true, LavaImmuneTicks = 420, LavaRose = true }));
            list.Add(Acc2(ItemType.HellfireTreads, "狱炎战靴", 4874, 7, "可飞行\n穿戴者可飞速奔跑\n在你身后留下一道火舌痕迹",
                bonus: new Bonuses { AccRunSpeed = 6f, RocketKind = 5, RunDust = RunDustKind.Hellfire, FlameTrail = true }));

            // --- Climbing, the ninja's dash and dodge, the balloons and the Sail jump, the rest (sec-G1b). ---
            list.Add(Acc2(ItemType.TigerClimbingGear, "猛虎攀爬装备", 976, 2, "可爬墙", bonus: new Bonuses { SpikedBoots = 2 }));
            list.Add(Acc2(ItemType.Tabi, "分趾厚底袜", 977, 7, "可猛冲\n双击一个方向", bonus: new Bonuses { DashKind = 1 }));
            list.Add(Acc2(ItemType.BlackBelt, "黑腰带", 963, 7, "有机会闪避攻击", bonus: new Bonuses { BlackBelt = true }));
            list.Add(Acc2(ItemType.MasterNinjaGear, "忍者大师装备", 984, 8, "可爬墙和猛冲\n有机会闪避攻击", bonus: new Bonuses { SpikedBoots = 2, DashKind = 1, BlackBelt = true }));
            const string twice = "可让持有者二连跳", higher = "增加跳跃高度", horseshoe = "增加跳跃高度、消除掉落伤害", honey = "受到伤害后释放蜜蜂并将使用者浸入蜂蜜中";
            list.Add(Acc2(ItemType.HoneyBalloon, "蜂蜜气球", 1249, 2, higher + "\n" + honey, bonus: new Bonuses { JumpBoost = true, HoneyComb = true }));
            list.Add(Acc2(ItemType.BlueHorseshoeBalloon, "蓝马掌气球", 1250, 4, twice + "\n" + horseshoe, bonus: new Bonuses { JumpBoost = true, JumpCloud = true, NoFallDamage = true }));
            list.Add(Acc2(ItemType.WhiteHorseshoeBalloon, "白马掌气球", 1251, 4, twice + "\n" + horseshoe, bonus: new Bonuses { JumpBoost = true, JumpBlizzard = true, NoFallDamage = true }));
            list.Add(Acc2(ItemType.YellowHorseshoeBalloon, "黄马掌气球", 1252, 4, "可让持有者做出强化二连跳\n" + horseshoe,
                bonus: new Bonuses { JumpBoost = true, JumpSandstorm = true, NoFallDamage = true }));
            list.Add(Acc2(ItemType.FartInABalloon, "臭屁气球", 1863, 4, twice + "\n" + higher, bonus: new Bonuses { JumpBoost = true, JumpFart = true }));
            list.Add(Acc2(ItemType.BalloonPufferfish, "气球河豚鱼", 3225, 1, higher, bonus: new Bonuses { JumpBoost = true }));
            list.Add(Acc2(ItemType.SharkronBalloon, "鲨鱼龙气球", 3241, 1, higher + "\n" + twice, bonus: new Bonuses { JumpBoost = true, JumpSail = true }));
            list.Add(Acc2(ItemType.BalloonHorseshoeFart, "绿马掌气球", 3250, 4, twice + "\n" + horseshoe, bonus: new Bonuses { JumpBoost = true, JumpFart = true, NoFallDamage = true }));
            list.Add(Acc2(ItemType.BalloonHorseshoeHoney, "琥珀马掌气球", 3251, 4, honey + "\n增加跳跃高度，消除掉落伤害",
                bonus: new Bonuses { JumpBoost = true, HoneyComb = true, NoFallDamage = true }));
            list.Add(Acc2(ItemType.BalloonHorseshoeSharkron, "粉马掌气球", 3252, 4, twice + "\n" + horseshoe, bonus: new Bonuses { JumpBoost = true, JumpSail = true, NoFallDamage = true }));
            list.Add(Acc2(ItemType.TsunamiInABottle, "海啸瓶", 3201, 1, twice, bonus: new Bonuses { JumpSail = true }));
            list.Add(Acc2(ItemType.HorseshoeBundle, "马掌气球束", 5331, 8, "可让持有者四连跳\n增加跳跃高度，消除掉落伤害",
                bonus: new Bonuses { JumpBoost = true, JumpCloud = true, JumpSandstorm = true, JumpBlizzard = true, NoFallDamage = true }));
            list.Add(Acc2(ItemType.ObsidianHorseshoe, "黑曜石马掌", 396, 4, "消除掉落伤害\n对火块免疫", bonus: new Bonuses { NoFallDamage = true, FireWalk = true }));
            list.Add(Acc2(ItemType.FloatingTube, "浮游圈", 4404, 0, "可让人浮在水面", bonus: new Bonuses { FloatInLiquid = true }));
            list.Add(Acc2(ItemType.PortableStool, "梯凳", 4341, 1, "按住向上键可以够到更高的地方", bonus: new Bonuses { PortableStool = true }));
            list.Add(Acc2(ItemType.ShimmerCloak, "炫彩斗篷", 5355, 5, "对微光相位化免疫\n当淹没在微光中时按住向下键可以相位化", bonus: new Bonuses { ShimmerImmune = true }));
            // I:13917-13924, Pl:15180-15183: the Gravity Globe (expert, the Moon Lord's bag), Up turning gravity over (Player/GravityGlobe.cs).
            list.Add(Acc2(ItemType.GravityGlobe, "重力球", 1131, -12, "可让持有者反转重力\n按向上键可改变重力", bonus: new Bonuses { GravityControl = true }));
            list.Add(Acc2(ItemType.Magiluminescence, "魔光护符", 5107, 1, "提高移动速度和加速度\n穿戴时可提供照明\n“我黑暗生命中的一道短暂曙光。”",
                bonus: new Bonuses { Magiluminescence = true }));
        }
    }
}
