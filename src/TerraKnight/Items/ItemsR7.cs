using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>
    /// Sweep R7 (Item.SetDefaults, zh-Hans names and tooltips): the grappling hooks (thrown by
    /// Player/GrapplingHook.cs), the permanent boosters, the potions and flasks (their buffs in
    /// Player/PotionBuffs.cs) and the chest bought to keep things in (World/Chests.cs).
    /// </summary>
    internal static partial class ItemDb
    {
        static void AddR7(List<ItemDef> list)
        {
            // --- The hooks (I:2372-40107): E throws the first one in the inventory.
            const string grapple = "按 E 朝光标抛出";
            ItemDef H(ItemType t, string name, int id, int rare, string flavour = null) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Rare = rare, Tooltip = flavour != null ? flavour + "\n" + grapple : grapple,
            };
            list.Add(H(ItemType.IvyWhip, "常春藤鞭", 185, 3));
            list.Add(H(ItemType.DualHook, "双钩", 437, 4));
            list.Add(H(ItemType.WebSlinger, "蛛丝吊索", 939, 2));
            list.Add(H(ItemType.AmethystHook, "紫晶钩", 1236, 1));
            list.Add(H(ItemType.TopazHook, "黄玉钩", 1237, 1));
            list.Add(H(ItemType.SapphireHook, "蓝玉钩", 1238, 1));
            list.Add(H(ItemType.EmeraldHook, "翡翠钩", 1239, 1));
            list.Add(H(ItemType.RubyHook, "红玉钩", 1240, 1));
            list.Add(H(ItemType.DiamondHook, "钻石钩", 1241, 1));
            list.Add(H(ItemType.SkeletronHand, "骷髅王之手", 1273, 2));
            list.Add(H(ItemType.BatHook, "蝙蝠钩", 1800, 3));
            list.Add(H(ItemType.SpookyHook, "阴森钩", 1829, 7));
            list.Add(H(ItemType.CandyCaneHook, "糖棒钩", 1915, 7));
            list.Add(H(ItemType.ChristmasHook, "圣诞钩", 1916, 7));
            list.Add(H(ItemType.FishHook, "鱼钩", 2360, 3));
            list.Add(H(ItemType.SlimeHook, "史莱姆钩", 2585, 3));
            list.Add(H(ItemType.AntiGravityHook, "反重力钩", 2800, 7));
            list.Add(H(ItemType.TendonHook, "肌腱钩", 3020, 6));
            list.Add(H(ItemType.ThornHook, "刺钩", 3021, 6));
            list.Add(H(ItemType.IlluminantHook, "荧光钩", 3022, 6));
            list.Add(H(ItemType.WormHook, "蠕虫钩", 3023, 6));
            list.Add(H(ItemType.LunarHook, "月钩", 3572, 10, "“想要月亮？勾住它然后拉下来！”"));
            list.Add(H(ItemType.StaticHook, "静止钩", 3623, 10));
            list.Add(H(ItemType.AmberHook, "琥珀钩", 4257, 1));
            list.Add(H(ItemType.SquirrelHook, "松鼠钩", 4759, 1, "像真正的松鼠一样抓住树！\n“藏身于树，化身为树”"));
            list.Add(H(ItemType.QueenSlimeHook, "失谐钩爪", 4980, 5, "将你传送到钩爪的位置"));

            // --- The permanent boosters (Pl:45577-45807, 12945-12960): used once each (the Life Fruit twenty times).
            ItemDef B(ItemType t, string name, int id, PermanentBoost boost, int rare, string tip, string sound = "Item_4", int stack = 9999) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Boost = boost, Consumable = true, MaxStack = stack, UseAnimation = 30 * Tick, UseTime = 30 * Tick,
                UseSound = sound, Rare = rare, Tooltip = tip,
            };
            list.Add(B(ItemType.LifeFruit, "生命果", 1291, PermanentBoost.LifeFruit, 7, "最大生命永久增加5\n（生命水晶用满后才能使用，最多 20 个）"));
            list.Add(B(ItemType.DemonHeart, "恶魔之心", 3335, PermanentBoost.DemonHeart, 4, "永久增加配饰栏数量", "Item_4", 1));
            list.Add(B(ItemType.AegisCrystal, "活力水晶", 5337, PermanentBoost.AegisCrystal, 6, "永久加速生命再生"));
            list.Add(B(ItemType.AegisFruit, "神盾果", 5338, PermanentBoost.AegisFruit, 6, "永久提高防御力", "Item_2"));
            list.Add(B(ItemType.ArcaneCrystal, "奥术水晶", 5339, PermanentBoost.ArcaneCrystal, 6, "永久提高魔力再生"));
            list.Add(B(ItemType.GalaxyPearl, "星系珍珠", 5340, PermanentBoost.GalaxyPearl, 6, "永久提高运气"));
            list.Add(B(ItemType.GummyWorm, "黏性蠕虫", 5341, PermanentBoost.GummyWorm, 6, "永久提高钓鱼技能", "Item_2"));
            list.Add(B(ItemType.Ambrosia, "仙馔密酒", 5342, PermanentBoost.Ambrosia, 6, "永久提高挖矿速度和建造速度", "Item_3"));
            list.Add(B(ItemType.ArtisanLoaf, "工匠面包", 5326, PermanentBoost.ArtisanLoaf, 3, "使用后可永久扩大制作站范围\n“曾经让泰迪想家的传奇面包”", "Item_2"));

            // --- The potions and flasks (I:1387-43842; buffs Pl:9962-10450). Drinking is Item3 (the Mushroom and food Item2).
            ItemDef P2(ItemType t, string name, int id, int buff, int ticks, int life, int mana, int rare, int value, bool potion, string tip)
            {
                var d = new ItemDef
                {
                    Type = t, Name = name, Texture = "Item_" + id, Consumable = true, MaxStack = 9999, UseAnimation = 17 * Tick, UseTime = 17 * Tick,
                    UseSound = t == ItemType.Mushroom ? "Item_2" : "Item_3", Rare = rare, HealLife = life, HealMana = mana, BuffType = buff, BuffTime = ticks, PotionSick = potion,
                };
                var lines = new List<string>();
                if (life > 0) lines.Add($"恢复 {life} 生命值");
                if (mana > 0) lines.Add($"恢复 {mana} 魔力值");
                if (!string.IsNullOrEmpty(tip)) lines.Add(tip);
                if (ticks > 0) lines.Add(ticks >= 3600 ? $"持续 {ticks / 3600} 分钟" : $"持续 {ticks / 60} 秒");
                d.Tooltip = string.Join("\n", lines);
                return d;
            }
            list.Add(P2(ItemType.Mushroom, "蘑菇", 5, 0, 0, 15, 0, 0, 1250, true, ""));
            list.Add(P2(ItemType.RestorationPotion, "恢复药水", 227, 0, 0, 90, 0, 1, 1500, true, "缩短药水冷却时间"));
            list.Add(P2(ItemType.ObsidianSkinPotion, "黑曜石皮药水", 288, 1, 21600, 0, 0, 1, 1000, false, "对熔岩免疫"));
            list.Add(P2(ItemType.RegenerationPotion, "再生药水", 289, 2, 28800, 0, 0, 1, 1000, false, "提供生命再生"));
            list.Add(P2(ItemType.SwiftnessPotion, "敏捷药水", 290, 3, 28800, 0, 0, 1, 1000, false, "移动速度提高25%"));
            list.Add(P2(ItemType.GillsPotion, "鱼鳃药水", 291, 4, 14400, 0, 0, 1, 1000, false, "当前世界无需水下呼吸，此药水暂无额外作用，使用不会消耗"));
            list.Add(P2(ItemType.IronskinPotion, "铁皮药水", 292, 5, 28800, 0, 0, 1, 1000, false, "防御力增加8"));
            list.Add(P2(ItemType.ManaRegenerationPotion, "魔力再生药水", 293, 6, 28800, 0, 0, 1, 1000, false, "提高魔力再生速度"));
            list.Add(P2(ItemType.MagicPowerPotion, "魔能药水", 294, 7, 14400, 0, 0, 1, 1000, false, "魔法伤害提高20%"));
            list.Add(P2(ItemType.FeatherfallPotion, "羽落药水", 295, 8, 36000, 0, 0, 1, 1000, false, "减缓坠落速度"));
            list.Add(P2(ItemType.SpelunkerPotion, "洞穴探险药水", 296, 9, 18000, 0, 0, 1, 1000, false, "显示宝藏和矿石的位置"));
            list.Add(P2(ItemType.InvisibilityPotion, "隐身药水", 297, 10, 10800, 0, 0, 1, 1000, false, "隐身并降低敌人的生成速度"));
            list.Add(P2(ItemType.ShinePotion, "光芒药水", 298, 11, 36000, 0, 0, 1, 1000, false, "发出光环"));
            list.Add(P2(ItemType.NightOwlPotion, "夜猫子药水", 299, 12, 36000, 0, 0, 1, 1000, false, "夜视力提升"));
            list.Add(P2(ItemType.BattlePotion, "战斗药水", 300, 13, 25200, 0, 0, 1, 1000, false, "提高敌人生成速度"));
            list.Add(P2(ItemType.ThornsPotion, "荆棘药水", 301, 14, 28800, 0, 0, 1, 1000, false, "攻击者也会受到伤害"));
            list.Add(P2(ItemType.WaterWalkingPotion, "水上漂药水", 302, 15, 36000, 0, 0, 1, 1000, false, "可在水上行走"));
            list.Add(P2(ItemType.ArcheryPotion, "箭术药水", 303, 16, 28800, 0, 0, 1, 1000, false, "弓伤害提高10%，箭速度提高20%"));
            list.Add(P2(ItemType.HunterPotion, "狩猎药水", 304, 17, 28800, 0, 0, 1, 1000, false, "显示敌人位置"));
            list.Add(P2(ItemType.GravitationPotion, "重力药水", 305, 18, 10800, 0, 0, 1, 1000, false, "可控制重力"));
            list.Add(P2(ItemType.GreaterHealingPotion, "强效治疗药水", 499, 0, 0, 150, 0, 3, 5000, true, ""));
            list.Add(P2(ItemType.GreaterManaPotion, "强效魔力药水", 500, 0, 0, 0, 200, 3, 500, false, ""));
            list.Add(P2(ItemType.BottledHoney, "瓶装蜂蜜", 1134, 48, 900, 80, 0, 0, 40, true, "短时间内提高自然恢复"));
            list.Add(P2(ItemType.FlaskofVenom, "毒液药剂", 1340, 71, 72000, 0, 0, 4, 2500, false, "近战和鞭子攻击会对敌人施放强效毒液"));
            list.Add(P2(ItemType.FlaskofCursedFlames, "诅咒焰药剂", 1353, 73, 72000, 0, 0, 4, 2500, false, "近战攻击和鞭子攻击会对敌人施放诅咒焰"));
            list.Add(P2(ItemType.FlaskofFire, "烈火药剂", 1354, 74, 72000, 0, 0, 4, 2500, false, "近战攻击和鞭子攻击会使敌人着火"));
            list.Add(P2(ItemType.FlaskofGold, "金药剂", 1355, 75, 72000, 0, 0, 4, 2500, false, "近战攻击和鞭子攻击会使敌人掉落更多金子"));
            list.Add(P2(ItemType.FlaskofIchor, "灵液药剂", 1356, 76, 72000, 0, 0, 4, 2500, false, "近战攻击和鞭子攻击会降低敌人防御力"));
            list.Add(P2(ItemType.FlaskofNanites, "纳米机器人药剂", 1357, 77, 72000, 0, 0, 4, 2500, false, "近战和鞭子攻击会使敌人困惑，额外造成5%的伤害"));
            list.Add(P2(ItemType.FlaskofParty, "派对药剂", 1358, 78, 72000, 0, 0, 4, 1500, false, "近战攻击和鞭子攻击会喷撒彩纸"));
            list.Add(P2(ItemType.FlaskofPoison, "毒药剂", 1359, 79, 72000, 0, 0, 4, 2500, false, "近战攻击和鞭子攻击会使敌人中毒"));
            list.Add(P2(ItemType.SuperManaPotion, "超级魔力药水", 2209, 0, 0, 0, 400, 4, 1500, false, ""));
            list.Add(P2(ItemType.MiningPotion, "挖矿药水", 2322, 104, 36000, 0, 0, 1, 1000, false, "挖矿速度提高25%"));
            list.Add(P2(ItemType.HeartreachPotion, "拾心药水", 2323, 105, 28800, 0, 0, 1, 1000, false, "扩大生命心的拾取范围"));
            list.Add(P2(ItemType.CalmingPotion, "镇静药水", 2324, 106, 43200, 0, 0, 1, 1000, false, "降低敌人生成速度"));
            list.Add(P2(ItemType.BuilderPotion, "建筑工药水", 2325, 107, 162000, 0, 0, 1, 1000, false, "提高放置速度、扩大放置范围"));
            list.Add(P2(ItemType.TitanPotion, "泰坦药水", 2326, 108, 28800, 0, 0, 1, 1000, false, "提高击退力"));
            list.Add(P2(ItemType.FlipperPotion, "脚蹼药水", 2327, 109, 28800, 0, 0, 1, 1000, false, "让你在液体中快速移动"));
            list.Add(P2(ItemType.SummoningPotion, "召唤药水", 2328, 110, 28800, 0, 0, 1, 1000, false, "仆从数量上限增加1"));
            list.Add(P2(ItemType.TrapsightPotion, "危险感药水", 2329, 111, 36000, 0, 0, 1, 1000, false, "让你看到附近的危险源"));
            list.Add(P2(ItemType.AmmoReservationPotion, "弹药储备药水", 2344, 112, 28800, 0, 0, 1, 1000, false, "20%几率省下弹药"));
            list.Add(P2(ItemType.LifeforcePotion, "生命力药水", 2345, 113, 28800, 0, 0, 1, 1000, false, "最大生命提高20%"));
            list.Add(P2(ItemType.EndurancePotion, "耐力药水", 2346, 114, 14400, 0, 0, 1, 1000, false, "所受伤害降低10%"));
            list.Add(P2(ItemType.RagePotion, "暴怒药水", 2347, 115, 14400, 0, 0, 1, 1000, false, "暴击率提高10%"));
            list.Add(P2(ItemType.InfernoPotion, "狱火药水", 2348, 116, 14400, 0, 0, 1, 1000, false, "点燃附近的敌人"));
            list.Add(P2(ItemType.WrathPotion, "怒气药水", 2349, 117, 14400, 0, 0, 1, 1000, false, "伤害提高10%"));
            list.Add(P2(ItemType.FishingPotion, "钓鱼药水", 2354, 121, 28800, 0, 0, 1, 1000, false, "渔力增加15"));
            list.Add(P2(ItemType.SonarPotion, "声呐药水", 2355, 122, 28800, 0, 0, 1, 1000, false, "查明上钩的鱼"));
            list.Add(P2(ItemType.CratePotion, "宝匣药水", 2356, 123, 14400, 0, 0, 1, 1000, false, "提高获得宝匣的几率"));
            list.Add(P2(ItemType.WarmthPotion, "保暖药水", 2359, 124, 54000, 0, 0, 1, 1000, false, "当前世界暂无寒冷伤害来源，此药水暂无额外减伤，使用不会消耗"));
            list.Add(P2(ItemType.RecallPotion, "回忆药水", 2350, 0, 0, 0, 0, 1, 1000, false, "将你传送回家（最后休息过的长椅）"));
            list.Add(P2(ItemType.TeleportationPotion, "传送药水", 2351, 0, 0, 0, 0, 1, 1000, false, "将你传送至随机位置"));
            list.Add(P2(ItemType.StrangeBrew, "诡药", 3001, 0, 0, 70, 0, 1, 500, true, "副作用可能包括：\n-不可预测的治疗\n-不确定的耐药性\n-短暂且莫名其妙的伤害免疫\n“难看且难闻”"));
            list.Add(P2(ItemType.SuperHealingPotion, "超级治疗药水", 3544, 0, 0, 200, 0, 7, 15000, true, ""));
            list.Add(P2(ItemType.LuckPotionLesser, "弱效幸运药水", 4477, 257, 18000, 0, 0, 0, 50000, false, "提高使用者的运气"));
            list.Add(P2(ItemType.LuckPotion, "幸运药水", 4478, 257, 36000, 0, 0, 0, 250000, false, "提高使用者的运气"));
            list.Add(P2(ItemType.LuckPotionGreater, "强效幸运药水", 4479, 257, 54000, 0, 0, 0, 1250000, false, "提高使用者的运气"));
            list.Add(P2(ItemType.BiomeSightPotion, "生物群系视觉药水", 5211, 343, 18000, 0, 0, 0, 1000, false, "当前世界没有腐化、猩红或神圣感染物块，此药水暂无显示目标，使用不会消耗"));
            list.Add(P2(ItemType.LifeFruitHealingPotion, "丛林果汁", 5496, 0, 0, 180, 0, 0, 105000, true, "“一种带有生命果和肉豆蔻香气的热带混合饮品”"));

            // --- The chest (I:48): bought from the Merchant, set down on the ground to keep 40 stacks in.
            list.Add(new ItemDef
            {
                Type = ItemType.Chest, Name = "宝箱", Texture = "Item_48", PlacesChest = true, Consumable = true, MaxStack = 9999, UseAnimation = 15 * Tick, UseTime = 10 * Tick,
                UseSound = null, Tooltip = "放在地面上，可存放 40 格物品\n右键打开；空箱子可用镐挖回",
            });
        }
    }
}
