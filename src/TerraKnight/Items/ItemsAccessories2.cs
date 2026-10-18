namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>An accessory by its vanilla stats (the price and stage come from Sources()).</summary>
        static ItemDef Acc2(ItemType t, string name, int id, int rare, string tip, int defense = 0, Bonuses bonus = default) =>
            Acc(t, name, id, price: 0, tier: 0, rare: rare, tip: tip, defense: defense, bonus: bonus);

        /// <summary>
        /// The hardmode defensive, status and utility accessories (accarmor-hm sec-G2) and the rest of
        /// the class ones (sec-G3): Item.SetDefaults, Player.ApplyEquipFunctional, zh-Hans names and
        /// tooltips. Effects with nothing to act on in Hallownest keep their flags (inert).
        /// </summary>
        static void AddAccessories2(System.Collections.Generic.List<ItemDef> list)
        {
            // --- Debuff immunities (buffImmune): nothing puts these debuffs on the Knight yet. ---
            const DebuffImmunity ankh = DebuffImmunity.Weak | DebuffImmunity.BrokenArmor | DebuffImmunity.Bleeding | DebuffImmunity.Poisoned | DebuffImmunity.Slow
                | DebuffImmunity.Confused | DebuffImmunity.Silenced | DebuffImmunity.Cursed | DebuffImmunity.Darkness | DebuffImmunity.Stoned;
            list.Add(Acc2(ItemType.AdhesiveBandage, "粘性绷带", 885, 4, "免疫流血", bonus: new Bonuses { DebuffImmune = DebuffImmunity.Bleeding }));
            list.Add(Acc2(ItemType.ArmorPolish, "盔甲抛光剂", 886, 4, "免疫破损盔甲", bonus: new Bonuses { DebuffImmune = DebuffImmunity.BrokenArmor }));
            list.Add(Acc2(ItemType.Bezoar, "牛黄", 887, 4, "对中毒免疫", bonus: new Bonuses { DebuffImmune = DebuffImmunity.Poisoned }));
            list.Add(Acc2(ItemType.Blindfold, "蒙眼布", 888, 4, "免疫黑暗", bonus: new Bonuses { DebuffImmune = DebuffImmunity.Darkness }));
            list.Add(Acc2(ItemType.FastClock, "快走时钟", 889, 4, "免疫缓慢", bonus: new Bonuses { DebuffImmune = DebuffImmunity.Slow }));
            list.Add(Acc2(ItemType.Megaphone, "扩音器", 890, 4, "对沉默免疫", bonus: new Bonuses { DebuffImmune = DebuffImmunity.Silenced }));
            list.Add(Acc2(ItemType.Nazar, "邪眼", 891, 2, "免疫诅咒", bonus: new Bonuses { DebuffImmune = DebuffImmunity.Cursed }));
            list.Add(Acc2(ItemType.Vitamins, "维生素", 892, 4, "对虚弱免疫", bonus: new Bonuses { DebuffImmune = DebuffImmunity.Weak }));
            list.Add(Acc2(ItemType.TrifoldMap, "三折地图", 893, 4, "对困惑免疫", bonus: new Bonuses { DebuffImmune = DebuffImmunity.Confused }));
            list.Add(Acc2(ItemType.PocketMirror, "袖珍镜", 3781, 3, "对石化免疫", bonus: new Bonuses { DebuffImmune = DebuffImmunity.Stoned }));
            list.Add(Acc2(ItemType.ArmorBracing, "盔甲背带", 901, 5, "对虚弱和破损盔甲免疫", bonus: new Bonuses { DebuffImmune = DebuffImmunity.Weak | DebuffImmunity.BrokenArmor }));
            list.Add(Acc2(ItemType.MedicatedBandage, "药用绷带", 902, 5, "对中毒和流血免疫", bonus: new Bonuses { DebuffImmune = DebuffImmunity.Poisoned | DebuffImmunity.Bleeding }));
            list.Add(Acc2(ItemType.ThePlan, "计划书", 903, 5, "对缓慢和困惑免疫", bonus: new Bonuses { DebuffImmune = DebuffImmunity.Slow | DebuffImmunity.Confused }));
            list.Add(Acc2(ItemType.CountercurseMantra, "反诅咒咒语", 904, 5, "对沉默和诅咒免疫", bonus: new Bonuses { DebuffImmune = DebuffImmunity.Silenced | DebuffImmunity.Cursed }));
            list.Add(Acc2(ItemType.ReflectiveShades, "反光墨镜", 5354, 5, "对黑暗和石化免疫", bonus: new Bonuses { DebuffImmune = DebuffImmunity.Darkness | DebuffImmunity.Stoned }));
            list.Add(Acc2(ItemType.AnkhCharm, "十字章护身符", 1612, 6, "对大部分减益免疫", bonus: new Bonuses { DebuffImmune = ankh }));
            list.Add(Acc2(ItemType.AnkhShield, "十字章护盾", 1613, 7, "对击退和火块免疫\n对大部分减益免疫", defense: 5,
                bonus: new Bonuses { DebuffImmune = ankh | DebuffImmunity.Chilled, NoKnockback = true, FireWalk = true }));

            // --- Shields. The Paladin's absorption shares teammates' damage: inert alone. ---
            const string paladinTip = "当生命值高于25%时，吸收团队中其他玩家所受伤害的25%";
            list.Add(Acc2(ItemType.PaladinsShield, "圣骑士护盾", 938, 8, paladinTip + "\n对击退免疫", defense: 6, bonus: new Bonuses { NoKnockback = true, Paladin = true }));
            list.Add(Acc2(ItemType.FrozenTurtleShell, "冰冻海龟壳", 1253, 5, "生命值低于50%时，在持有者周围放置可降低25%伤害的护罩", bonus: new Bonuses { IceBarrier = true }));
            list.Add(Acc2(ItemType.FrozenShield, "冰冻护盾", 3997, 5, "对击退免疫\n生命值低于50%时，在所有者周围放置可减少25%伤害的护罩\n" + paladinTip, defense: 6,
                bonus: new Bonuses { NoKnockback = true, Paladin = true, IceBarrier = true }));
            list.Add(Acc2(ItemType.HeroShield, "英雄护盾", 3998, 5, "对击退免疫\n" + paladinTip + "\n敌人更有可能以你为目标", defense: 10,
                bonus: new Bonuses { NoKnockback = true, Paladin = true, Aggro = 400 }));
            list.Add(Acc2(ItemType.SilverShield, "银盾", 6183, 3, "对击退和火块免疫\n召唤标记效果上限增加1个\n大幅延长召唤标记效果的持续时间", defense: 2,
                bonus: new Bonuses { NoKnockback = true, FireWalk = true, SilverBracer = true }));
            list.Add(Acc2(ItemType.RestorationShield, "恢复之盾", 6188, 2, "对击退免疫\n缓慢再生生命\n最大魔力值增加40\n提高魔力再生速度", defense: 1,
                bonus: new Bonuses { NoKnockback = true, LifeRegen = 2, ManaMax = 40, ManaRegenBonus = 60 }));

            // --- Fire blocks, lava and roses: Hallownest has neither. ---
            list.Add(Acc2(ItemType.LavaCharm, "熔岩护身符", 906, 3, "7秒内对熔岩免疫", bonus: new Bonuses { LavaImmuneTicks = 420 }));
            list.Add(Acc2(ItemType.ObsidianRose, "黑曜石玫瑰", 1323, 3, "降低因触碰熔岩而受到的伤害", bonus: new Bonuses { LavaRose = true }));
            list.Add(Acc2(ItemType.ObsidianSkullRose, "黑曜石骷髅头玫瑰", 4004, 5, "对火块免疫\n减少因触碰熔岩而受到的伤害", bonus: new Bonuses { FireWalk = true, LavaRose = true }));
            list.Add(Acc2(ItemType.LavaSkull, "岩浆骷髅头", 3999, 5, "7秒内对熔岩免疫\n对火块免疫", bonus: new Bonuses { FireWalk = true, LavaImmuneTicks = 420 }));
            list.Add(Acc2(ItemType.MoltenSkullRose, "熔火骷髅头玫瑰", 4003, 6, "7秒内对熔岩免疫\n对火块免疫\n减少因触碰熔岩而受到的伤害",
                bonus: new Bonuses { FireWalk = true, LavaRose = true, LavaImmuneTicks = 420 }));

            // --- Charms, stones and shells. The werewolf and the sky stones follow TerraTime's day. ---
            const string skyTip = "小幅提高伤害、近战速度、暴击率、\n生命再生、防御力、挖矿速度和仆从击退力";
            list.Add(Acc2(ItemType.CharmofMyths, "神话护身符", 860, 6, "提供生命再生并使治疗药水的冷却时间缩短25%", bonus: new Bonuses { LifeRegen = 2, PStone = true }));
            list.Add(Acc2(ItemType.MoonCharm, "月光护身符", 485, 4, "在晚上将持有者变成狼人", bonus: new Bonuses { Werewolf = true }));
            list.Add(Acc2(ItemType.NeptunesShell, "海神贝壳", 497, 5, "入水时将持有者变成人鱼", bonus: new Bonuses { Merfolk = true, Swim = true }));
            list.Add(Acc2(ItemType.MoonShell, "月亮贝壳", 861, 6, "在晚上将持有者变成狼人，入水时将持有者变成人鱼", bonus: new Bonuses { Werewolf = true, Merfolk = true, Swim = true }));
            list.Add(Acc2(ItemType.CelestialShell, "天界贝壳", 3110, 8, "在晚上将持有者变成狼人，入水时将持有者变成人鱼\n" + skyTip,
                bonus: new Bonuses { Werewolf = true, Merfolk = true, Swim = true, SkyStone = SkyStoneTime.Always }));
            list.Add(Acc2(ItemType.SunStone, "太阳石", 899, 7, "白天时，" + skyTip, bonus: new Bonuses { SkyStone = SkyStoneTime.Day }));
            list.Add(Acc2(ItemType.MoonStone, "月亮石", 900, 5, "夜晚时，" + skyTip, bonus: new Bonuses { SkyStone = SkyStoneTime.Night }));
            list.Add(Acc2(ItemType.CelestialStone, "天界石", 1865, 7, skyTip, bonus: new Bonuses { SkyStone = SkyStoneTime.Always }));
            list.Add(Acc2(ItemType.ShinyStone, "闪亮石", 3337, -12, "不移动时，大大提高生命再生速度", bonus: new Bonuses { ShinyStone = true }));
            list.Add(Acc2(ItemType.BrainOfConfusion, "混乱之脑", 3223, -12, "有机会制造幻象并闪避攻击\n闪避后暂时提高暴击率和仆从伤害\n可使附近被击中的敌人陷入困惑",
                bonus: new Bonuses { BrainOfConfusion = true }));
            list.Add(Acc2(ItemType.SporeSac, "孢子囊", 3336, -12, "随着时间推移召唤可以伤害敌人的孢子", bonus: new Bonuses { SporeSac = true }));

            // --- The Star Cloak and Honey Comb family: one volley and one swarm per hit, whatever is worn. ---
            list.Add(Acc2(ItemType.BeeCloak, "蜜蜂斗篷", 1247, 4, "受到伤害后会使星星坠落、释放蜜蜂并将使用者浸入蜂蜜中",
                bonus: new Bonuses { StarCloak = true, StarKind = CloakStar.Bee, HoneyComb = true }));
            list.Add(Acc2(ItemType.StarVeil, "星星面纱", 862, 6, "受到伤害后会使星星坠落并延长受伤后的无敌状态时间",
                bonus: new Bonuses { StarCloak = true, StarKind = CloakStar.Veil, LongImmunity = true }));
            list.Add(Acc2(ItemType.SweetheartNecklace, "甜心项链", 1578, 3, "受到伤害后释放蜜蜂并将使用者浸入蜂蜜中、并提高移动速度", bonus: new Bonuses { HoneyComb = true, Panic = true }));
            list.Add(Acc2(ItemType.StingerNecklace, "毒刺项链", 4007, 5, "盔甲穿透力提高5点\n受到伤害后释放蜜蜂并将使用者浸入蜂蜜中", bonus: new Bonuses { HoneyComb = true, ArmorPen = 5 }));
            list.Add(Acc2(ItemType.CrossedHeartNecklace, "交错心形项链", 6187, 5, "受到伤害后提高移动速度\n延长受伤后的无敌状态时间", bonus: new Bonuses { Panic = true, LongImmunity = true }));
            list.Add(Acc2(ItemType.SeraphNecklace, "炽天使项链", 6180, 5, "定期将箭矢引向敌人\n延长受伤后的无敌状态时间",
                bonus: new Bonuses { HarpyCharm = true, SeraphNecklace = true, LongImmunity = true }));
            list.Add(Acc2(ItemType.DruidicSerpentCloak, "德鲁伊蛇纹斗篷", 6186, 5, "每挥动3次，第二条鞭子会协同攻击\n受到伤害后会使星星坠落",
                bonus: new Bonuses { SnakeBand = true, StarCloak = true, StarKind = CloakStar.Snake }));

            // --- The bosses' expert accessories and the rest. ---
            list.Add(Acc2(ItemType.RoyalGel, "皇家凝胶", 3090, -12, "史莱姆变得友好", bonus: new Bonuses { RoyalGel = true }));
            list.Add(Acc2(ItemType.VolatileGelatin, "挥发明胶", 4987, -12, "定期释放能伤害敌人的挥发明胶", bonus: new Bonuses { VolatileGelatin = true }));
            list.Add(Acc2(ItemType.BoneGlove, "骨头手套", 3245, -12, "在你攻击时向敌人射出交叉骨头", bonus: new Bonuses { BoneGlove = true }));
            list.Add(Acc2(ItemType.HiveBackpack, "蜂巢背包", 3333, -12, "增加友好蜜蜂的力量", bonus: new Bonuses { StrongBees = true }));
            list.Add(Acc2(ItemType.BoneHelm, "骸骨头盔", 5100, -12, "召唤暗影之手来攻击敌人\n“看到它我就头疼。”", bonus: new Bonuses { BoneHelm = true }));
            list.Add(Acc2(ItemType.HandWarmer, "暖手宝", 1921, 2, "对冷冻和冰冻效果免疫", bonus: new Bonuses { DebuffImmune = DebuffImmunity.Chilled | DebuffImmunity.Frozen }));
            list.Add(Acc2(ItemType.StressBall, "解压球", 5543, 1, "不移动时连续使用某些物品\n物品栏打开时不起作用", bonus: new Bonuses { StressBall = true }));
            list.Add(Acc2(ItemType.DeadCellsRamRune, "重锤符文", 5465, 2, "按住下并跳跃即可向下猛击\n猛击地面会对附近的敌人造成伤害\n猛击能砸开需要下砸的地板\n“像古代的英雄一样猛击地面”", bonus: new Bonuses { DownDash = true }));

            // --- Water: Hallownest's only liquid is acid; there is no drowning and no ice. ---
            list.Add(Acc2(ItemType.DivingGear, "潜水装备", 394, 4, "给予游泳能力\n大大延长水下呼吸时间", bonus: new Bonuses { Swim = true, DivingHelm = true }));
            list.Add(Acc2(ItemType.JellyfishNecklace, "水母项链", 1303, 2, "发出非常微弱的光芒，这种光芒在水下会变得更醒目", bonus: new Bonuses { AccLight = AccLightKind.Jellyfish }));
            list.Add(Acc2(ItemType.JellyfishDivingGear, "水母潜水装备", 1860, 5, "给予游泳能力并大大延长水下呼吸时间\n发出非常微弱的光芒，这种光芒在水下会变得更醒目",
                bonus: new Bonuses { Swim = true, DivingHelm = true, AccLight = AccLightKind.Jellyfish }));
            list.Add(Acc2(ItemType.ArcticDivingGear, "北极潜水装备", 1861, 6, "给予游泳能力并大大延长水下呼吸时间\n提供额外冰面行动力\n发出非常微弱的光芒，这种光芒在水下会变得更醒目",
                bonus: new Bonuses { Swim = true, DivingHelm = true, IceSkate = true, AccLight = AccLightKind.Arctic }));

            // --- Money: coins are geo. ---
            list.Add(Acc2(ItemType.DiscountCard, "优惠卡", 854, 5, "商店价格降低20%", bonus: new Bonuses { Discount = true }));
            list.Add(Acc2(ItemType.LuckyCoin, "幸运币", 855, 5, "击中敌人有时会掉落额外的钱币\n“我捡到一枚钱币！今天真幸运！”", bonus: new Bonuses { LuckyCoin = true }));
            list.Add(Acc2(ItemType.GoldRing, "金戒指", 3033, 5, "扩大钱币拾取范围", bonus: new Bonuses { GoldRing = true }));
            list.Add(Acc2(ItemType.CoinRing, "钱币戒指", 3034, 5, "扩大钱币拾取范围\n击中敌人有时会掉落额外的钱币", bonus: new Bonuses { GoldRing = true, LuckyCoin = true }));
            list.Add(Acc2(ItemType.GreedyRing, "贪婪戒指", 3035, 6, "扩大钱币拾取范围\n击中敌人有时会掉落额外钱币\n商店价格降低20%",
                bonus: new Bonuses { GoldRing = true, LuckyCoin = true, Discount = true }));
            list.Add(Acc2(ItemType.TreasureMagnet, "宝藏磁石", 5010, 1, "扩大物品的拾取范围\n吸引附近的吉欧", bonus: new Bonuses { TreasureMagnet = true }));
            list.Add(Acc2(ItemType.AncientChisel, "远古凿子", 4056, 1, "挖矿速度提高25%\n“古老的问题需要古老的解决方案”", bonus: new Bonuses { PickSpeed = -0.25f }));
            list.Add(Acc2(ItemType.SpectreGoggles, "幽灵护目镜", 4409, 5, "开启回声视觉，显示隐藏物块", bonus: new Bonuses { SeeInvisible = true }));

            // --- Whip tags (1.4.5): more tags at once, longer, harder whips, a second crack. ---
            list.Add(Acc2(ItemType.SilverBracer, "银护腕", 6156, 2, "召唤标记效果上限增加1个\n大幅延长召唤标记效果的持续时间\n“真是一线银光！”", bonus: new Bonuses { SilverBracer = true }));
            list.Add(Acc2(ItemType.SnakeBand, "蛇形手环", 6157, 4, "每挥动3次，第二条鞭子会协同攻击\n“麻烦找上门，就得抽它……”", bonus: new Bonuses { SnakeBand = true }));
            list.Add(Acc2(ItemType.MobiusStrip, "莫比乌斯带", 6158, 7, "召唤标记效果上限增加1个\n鞭子攻击范围扩大10%", bonus: new Bonuses { MobiusStrip = true }));
            list.Add(Acc2(ItemType.WickedArmlet, "邪恶臂环", 6159, 4, "召唤标记效果上限增加1个\n提高鞭子的击退力", bonus: new Bonuses { WickedArmlet = true }));
            list.Add(Acc2(ItemType.OuroborosRing, "衔尾蛇指环", 6162, 8, "召唤标记效果上限增加1个\n鞭子攻击范围扩大10%\n每挥动3次，第二条鞭子会协同攻击",
                bonus: new Bonuses { MobiusStrip = true, SnakeBand = true }));
            list.Add(Acc2(ItemType.TwilightGrasp, "暮光之握", 6163, 5, "召唤标记效果上限增加2个\n大幅延长召唤标记效果的持续时间\n提高鞭子的击退力",
                bonus: new Bonuses { SilverBracer = true, WickedArmlet = true }));
            list.Add(Acc2(ItemType.ArmletOfRuin, "毁灭臂环", 6179, 5, "召唤标记效果上限增加1个\n提高鞭子的击退力\n定期积蓄微光能量\n仆从标记命中时会释放远古力量",
                bonus: new Bonuses { WickedArmlet = true, ArmletOfRuin = true }));

            // --- Ranged. ---
            list.Add(Acc2(ItemType.HarpyCharm, "鹰身女妖护符", 6166, 2, "定期将箭矢引向敌人", bonus: new Bonuses { HarpyCharm = true, RangedCrit = 0.05f }));
            list.Add(Acc2(ItemType.PhoenixQuiver, "凤凰箭袋", 6181, 6, "定期将箭矢引向敌人\n箭矢伤害增加10%，箭矢速度大幅提升\n有20%概率不消耗箭矢\n点燃木箭\n“凤凰之焰”",
                bonus: new Bonuses { HarpyCharm = true, PhoenixQuiver = true, MoltenQuiver = true, FastArrows = true, ArrowDamage = 0.1f }));
            list.Add(Acc2(ItemType.PoisonBarb, "淬毒刺", 6165, 1, "箭矢附带中毒效果", bonus: new Bonuses { PoisonBarb = true }));
            list.Add(Acc2(ItemType.SweetBarb, "甜蜜倒刺", 6184, 3, "受到伤害后释放蜜蜂并将使用者浸入蜂蜜中\n箭矢附带中毒效果，命中时还可能释放蜜蜂！",
                bonus: new Bonuses { PoisonBarb = true, HoneyedBarb = true, HoneyComb = true }));
            list.Add(Acc2(ItemType.SnappingStone, "碎裂之石", 6167, 3, "定期积蓄微光能量\n远程攻击命中时释放伤害脉冲", bonus: new Bonuses { SnappingStone = true }));
            list.Add(Acc2(ItemType.Pyroclast, "火山碎屑石", 6178, 4, "近战攻击造成火焰伤害\n定期积蓄微光能量\n近战和远程攻击命中时释放爆裂斩击", bonus: new Bonuses { MagmaStone = true, Pyroclast = true }));
            const string cycleTip = "\n远程伤害增加8%\n在物品栏中右键点击可切换弹药使用模式";
            list.Add(Acc2(ItemType.ChaosCylinder, "混沌弹筒", 6168, 5, "武器会轮流使用弹药" + cycleTip,
                bonus: new Bonuses { AmmoCycling = AmmoCycleMode.AmmoSlots, AmmoCyclerDamage = true }));
            list.Add(Acc2(ItemType.ChaosCylinderFull, "混沌弹筒（满载）", 6169, 5, "武器会在整个物品栏中轮流使用弹药" + cycleTip,
                bonus: new Bonuses { AmmoCycling = AmmoCycleMode.FullInventory, AmmoCyclerDamage = true }));
            list.Add(Acc2(ItemType.ChaosCylinderRandom, "混沌弹筒（随机）", 6193, 5, "武器会随机使用弹药" + cycleTip,
                bonus: new Bonuses { AmmoCycling = AmmoCycleMode.Random, AmmoCyclerDamage = true }));
            list.Add(Acc2(ItemType.ChaosCylinderSimple, "混沌弹筒（简易）", 6194, 5, "武器正常使用弹药！" + cycleTip,
                bonus: new Bonuses { AmmoCycling = AmmoCycleMode.Simple, AmmoCyclerDamage = true }));

            // --- Generic and magic. ---
            list.Add(Acc2(ItemType.CatalystBand, "催化手环", 6185, 5, "治疗药水的冷却时间缩短25%\n箭矢附带中毒效果\n提高敌人身上负面效果造成的伤害。",
                bonus: new Bonuses { PoisonBarb = true, PStone = true, CatalystBand = true }));
            list.Add(Acc2(ItemType.MysticArtsSash, "秘术腰带", 6189, 8, "缓慢再生生命\n最大魔力值增加40\n提高魔力再生速度\n有机会闪避攻击",
                bonus: new Bonuses { LifeRegen = 2, ManaMax = 40, ManaRegenBonus = 60, MysticSashDodge = true }));

            // --- Sentry slings: walk through a sentry to carry it. ---
            list.Add(Acc2(ItemType.HeavySling, "重型投石索", 6172, 5, "经过哨兵时可将其带走", bonus: new Bonuses { SentryBackpack = true }));
            list.Add(Acc2(ItemType.ScoutSling, "斥候投石索", 6175, 7, "经过哨兵时可将其带走\n敌人更不容易以你为目标\n伤害增加5%",
                bonus: new Bonuses { SentryBackpack = true, Aggro = -400, MeleeDamage = 0.05f, RangedDamage = 0.05f, MagicDamage = 0.05f, MinionDamage = 0.05f }));
            list.Add(Acc2(ItemType.TemplarSling, "圣殿骑士投石索", 6176, 8, "经过哨兵时可将其带走\n哨兵暴击率提高10%", bonus: new Bonuses { SentryBackpack = true, GolemSentryBackpack = true }));
        }
    }
}
