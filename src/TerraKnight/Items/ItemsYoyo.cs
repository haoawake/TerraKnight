namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The yoyos and their accessories (yoyo/spec-yoyo.md Y.1.0, Y.1.9; Item.SetDefaults
        /// I:30043-30265, 42005-42022, 43658-43721; zh-Hans names and tooltips): melee, held on the
        /// button (channel), no autoReuse, thrown at 16 px a tick (Hive-Five 10).
        /// </summary>
        static void AddYoyos(System.Collections.Generic.List<ItemDef> list)
        {
            ItemDef Yoyo(ItemType t, string name, int id, int dmg, float kb, int rare, ProjKind proj, int crit = 0, string tip = null, float speed = 16f) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Melee, Damage = dmg, Knockback = kb, Crit = crit, UseAnimation = 25 * Tick, UseTime = 25 * Tick,
                NoMelee = true, NoUseGraphic = true, HoldOut = true, Channel = true, AutoReuse = false, Shoot = proj, ShootSpeed = speed, UseSound = "Item_1", Rare = rare, Tooltip = tip,
            };
            const string dev = "“非常适合冒充开发者！”";
            list.Add(Yoyo(ItemType.WoodYoyo, "木悠悠球", 3278, 9, 2.5f, 0, ProjKind.WoodenYoyo));
            list.Add(Yoyo(ItemType.Rally, "对打球", 3285, 14, 3.5f, 1, ProjKind.RallyYoyo));
            list.Add(Yoyo(ItemType.CorruptYoyo, "抑郁球", 3279, 16, 4.5f, 1, ProjKind.MalaiseYoyo));
            list.Add(Yoyo(ItemType.CrimsonYoyo, "血脉球", 3280, 17, 4f, 1, ProjKind.ArteryYoyo));
            list.Add(Yoyo(ItemType.JungleYoyo, "亚马逊球", 3281, 18, 3.75f, 3, ProjKind.AmazonYoyo));
            list.Add(Yoyo(ItemType.Code1, "代码1球", 3262, 21, 3.25f, 2, ProjKind.Code1Yoyo));
            list.Add(Yoyo(ItemType.HiveFive, "蜂巢球", 5294, 24, 3.75f, 3, ProjKind.HiveFiveYoyo, tip: "攻击敌人后召唤杀人蜂", speed: 10f));
            list.Add(Yoyo(ItemType.Valor, "英勇球", 3317, 28, 3.85f, 3, ProjKind.ValorYoyo));
            list.Add(Yoyo(ItemType.Cascade, "喷流球", 3282, 27, 4.3f, 3, ProjKind.CascadeYoyo, tip: "命中时为烈焰爆炸充能"));
            list.Add(Yoyo(ItemType.Chik, "吉克球", 3283, 34, 3.3f, 4, ProjKind.ChikYoyo, tip: "命中时为水晶爆炸充能"));
            list.Add(Yoyo(ItemType.Amarok, "冰雪悠悠球", 3289, 47, 2.8f, 4, ProjKind.AmarokYoyo));
            list.Add(Yoyo(ItemType.HelFire, "狱火球", 3290, 43, 4.5f, 4, ProjKind.HelFireYoyo));
            list.Add(Yoyo(ItemType.Gradient, "渐变球", 3316, 53, 3.8f, 4, ProjKind.GradientYoyo));
            list.Add(Yoyo(ItemType.FormatC, "好胜球", 3315, 40, 3.25f, 5, ProjKind.FormatCYoyo, crit: 20, tip: "暴击造成额外伤害"));
            list.Add(Yoyo(ItemType.Code2, "代码2球", 3284, 54, 3.8f, 5, ProjKind.Code2Yoyo));
            list.Add(Yoyo(ItemType.Yelets, "叶列茨球", 3286, 60, 3.1f, 7, ProjKind.YeletsYoyo, tip: "命中造成酸性毒液"));
            list.Add(Yoyo(ItemType.RedsYoyo, "Red的抛球", 3287, 70, 4.5f, 9, ProjKind.RedsThrowYoyo, tip: dev));
            list.Add(Yoyo(ItemType.ValkyrieYoyo, "女武神悠悠球", 3288, 70, 4.5f, 9, ProjKind.ValkyrieYoyo, tip: dev));
            list.Add(Yoyo(ItemType.TheEyeOfCthulhu, "克苏鲁之眼", 3292, 115, 3.5f, 8, ProjKind.EyeYoyo, tip: "以克苏鲁之眼的力量攻击"));
            list.Add(Yoyo(ItemType.Kraken, "克拉肯球", 3291, 120, 4.3f, 8, ProjKind.KrakenYoyo, crit: 10, tip: "蕴含深海能量\n命中时为强力巨浪充能"));
            list.Add(Yoyo(ItemType.Terrarian, "泰拉悠悠球", 3389, 190, 6.5f, 10, ProjKind.TerrarianYoyo, crit: 10));

            // The strings: yoyoString (range ×1.25 + 30, life ×1.5) and the string's colour (stringColor).
            const string longer = "增加悠悠球射程和时长";
            foreach (var (t, name, id, colour) in new[]
            {
                (ItemType.RedString, "红绳", 3293, 1), (ItemType.OrangeString, "橙绳", 3294, 2), (ItemType.YellowString, "黄绳", 3295, 3), (ItemType.LimeString, "橙绿绳", 3296, 4),
                (ItemType.GreenString, "绿绳", 3297, 5), (ItemType.TealString, "青绿绳", 3298, 6), (ItemType.CyanString, "青绳", 3299, 7), (ItemType.SkyBlueString, "天蓝绳", 3300, 8),
                (ItemType.BlueString, "蓝绳", 3301, 9), (ItemType.PurpleString, "紫绳", 3302, 10), (ItemType.VioletString, "蓝紫绳", 3303, 11), (ItemType.PinkString, "粉绳", 3304, 12),
                (ItemType.BrownString, "棕绳", 3305, 28), (ItemType.WhiteString, "白绳", 3306, 14), (ItemType.RainbowString, "彩虹绳", 3307, 27), (ItemType.BlackString, "黑绳", 3308, 13),
            })
                list.Add(Acc2(t, name, id, 1, longer, bonus: new Bonuses { YoyoString = true, StringColor = colour }));
            const string weight = "在用悠悠球击中敌人后投掷平衡锤";
            foreach (var (t, name, id, code) in new[]
            {
                (ItemType.BlackCounterweight, "黑平衡锤", 3309, 1), (ItemType.BlueCounterweight, "蓝平衡锤", 3310, 2), (ItemType.GreenCounterweight, "绿平衡锤", 3311, 3),
                (ItemType.PurpleCounterweight, "紫平衡锤", 3312, 4), (ItemType.RedCounterweight, "红平衡锤", 3313, 5), (ItemType.YellowCounterweight, "黄平衡锤", 3314, 6),
            })
                list.Add(Acc2(t, name, id, 2, weight, bonus: new Bonuses { CounterWeight = code }));
            list.Add(Acc2(ItemType.YoYoGlove, "悠悠球手套", 3334, 4, "可同时使用两个悠悠球", bonus: new Bonuses { YoyoGlove = true }));
            // The bags throw a counterweight of a random colour each time (Pl:14819-14834: counterWeight is rolled every frame).
            list.Add(Acc2(ItemType.YoyoBag, "悠悠球袋", 3366, 4, "让使用者掌握悠悠球技巧", bonus: new Bonuses { CounterWeight = 8, YoyoGlove = true, YoyoString = true }));
            list.Add(Acc2(ItemType.MagicString, "魔法绳", 5540, 5, "允许离绳的悠悠球技巧", bonus: new Bonuses { MagicString = true, StringColor = 29 }));
            list.Add(Acc2(ItemType.MagicYoyoBag, "魔法悠悠球袋", 5541, 5, "赋予使用者魔法悠悠球技能",
                bonus: new Bonuses { CounterWeight = 8, YoyoGlove = true, YoyoString = true, MagicString = true, StringColor = 29 }));
            list.Add(Acc2(ItemType.StrungCounterweight, "串接平衡锤", 5547, 3, longer + "\n" + weight, bonus: new Bonuses { YoyoString = true, StringColor = 30, CounterWeight = 7 }));
        }

        /// <summary>
        /// The flails (yoyo/spec-yoyo.md Y.2.1; I:3331-5964, 10775-10793, 15606-15624, 36758-36776,
        /// 40334-40357, 43535-43551): melee, held on the button to spin, no autoReuse but Flairon's;
        /// a thrown hit deals twice (ToolTipDamageMultiplier 2).
        /// </summary>
        static void AddFlails(System.Collections.Generic.List<ItemDef> list)
        {
            ItemDef Flail(ItemType t, string name, int id, int dmg, float kb, int use, float speed, int rare, ProjKind proj, int crit = 0, string tip = null) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Melee, Damage = dmg, Knockback = kb, Crit = crit, UseAnimation = use * Tick, UseTime = use * Tick,
                NoMelee = true, NoUseGraphic = true, HoldOut = true, Channel = true, AutoReuse = false, Shoot = proj, ShootSpeed = speed, UseSound = "Item_1", Rare = rare, Tooltip = tip,
            };
            list.Add(Flail(ItemType.Mace, "链锤", 5011, 9, 4.6f, 45, 11f, 1, ProjKind.MaceFlail, tip: "可以用火把升级"));
            list.Add(Flail(ItemType.FlamingMace, "烈焰链锤", 5012, 9, 4.6f, 45, 11f, 1, ProjKind.FlamingMaceFlail, tip: "“愿火焰照亮你前行的路”"));
            list.Add(Flail(ItemType.BallOHurt, "链球", 162, 15, 5.5f, 45, 12f, 1, ProjKind.BallOHurtFlail));
            list.Add(Flail(ItemType.TheMeatball, "血肉之球", 801, 17, 5.5f, 45, 12f, 1, ProjKind.MeatballFlail));
            list.Add(Flail(ItemType.BlueMoon, "蓝月", 163, 27, 6f, 45, 12f, 2, ProjKind.BlueMoonFlail));
            list.Add(Flail(ItemType.Sunfury, "阳炎之怒", 220, 32, 6.75f, 45, 12f, 3, ProjKind.SunfuryFlail, crit: 7));
            list.Add(Flail(ItemType.DaoofPow, "太极连枷", 389, 50, 6f, 45, 15f, 5, ProjKind.DaoOfPowFlail, tip: "有机会造成困惑\n“找到你内心的碎片”"));
            list.Add(Flail(ItemType.DripplerFlail, "滴滴怪致残者", 4272, 55, 6.5f, 40, 15.9f, 4, ProjKind.DripplerFlail));
            list.Add(Flail(ItemType.FlowerPow, "花之力", 1259, 65, 6.5f, 40, 15.9f, 7, ProjKind.FlowerPowFlail, tip: "朝附近的敌人射出锋利的花瓣"));
            var flairon = Flail(ItemType.FlaironFlail, "猪鲨链球", 5526, 66, 4.5f, 20, 14f, 8, ProjKind.FlaironFlail, tip: "喷射追踪泡泡\n在物品栏中右键点击以切换模式");
            flairon.AutoReuse = true;
            list.Add(flairon);
            // I:25519-25536: its other form (Flairon, 2611), thrown and drawn back (AI_069, not held on the button); the two swap by a right
            // click in the inventory (ItemSlot.TryItemSwap, IS:1967-1970).
            var flaironOld = Flail(ItemType.Flairon, "猪龙鱼链球", 2611, 66, 4.5f, 20, 14f, 8, ProjKind.FlaironHead, tip: "喷出自动制导的泡泡\n在物品栏中右键点击以切换模式");
            flaironOld.Channel = false;
            flaironOld.AutoReuse = true;
            list.Add(flaironOld);
        }
    }
}
