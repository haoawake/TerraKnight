namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>A crafting material (Item.material): no use of its own, stacks to 9999.</summary>
        static ItemDef Mat(ItemType t, string name, int id, int rare, string tip) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, MaxStack = 9999, Material = true, Rare = rare, Tooltip = tip, UseSound = null,
        };

        /// <summary>
        /// The equipment Terraria's recipes go through on the way to the mod's items (spec-recipes
        /// 5.3), with their 1.4.5.8 stats; the seven swords with their own projectiles (True Night's
        /// Edge's blade, True Excalibur's slashes, the Seedler's seeds, the Horseman's pumpkins, the
        /// Influx Waver's blades, the Meowmere's cats, the Star Wrath's stars).
        /// </summary>
        static void AddIntermediates(System.Collections.Generic.List<ItemDef> list)
        {
            list.Add(Acc(ItemType.Shackle, "镣铐", 216, price: 0, tier: 0, rare: 1, defense: 1));
            list.Add(Acc(ItemType.ObsidianSkull, "黑曜石骷髅头", 193, price: 0, tier: 0, rare: 2, tip: "对火块免疫", defense: 1, bonus: new Bonuses { FireWalk = true }));
            list.Add(Acc(ItemType.WhoopieCushion, "整蛊坐垫", 215, price: 0, tier: 0, rare: 2, tip: "“可能会惹恼别人”"));
            list.Add(Acc(ItemType.SandstorminaBalloon, "沙暴气球", 983, price: 0, tier: 0, rare: 4, tip: "允许持有者做出强化的二段跳\n增加跳跃高度",
                bonus: new Bonuses { JumpBoost = true, JumpSandstorm = true }));
            list.Add(Acc(ItemType.BlizzardinaBalloon, "暴雪气球", 1163, price: 0, tier: 0, rare: 4, tip: "可让持有者二连跳\n增加跳跃高度",
                bonus: new Bonuses { JumpBoost = true, JumpBlizzard = true }));
            list.Add(Acc(ItemType.NaturesGift, "大自然的恩赐", 223, price: 0, tier: 0, rare: 3, tip: "魔力消耗降低6%", bonus: new Bonuses { ManaCost = 0.06f }));
            list.Add(Acc(ItemType.TitanGlove, "泰坦手套", 536, price: 0, tier: 0, rare: 4, tip: "提高近战击退力\n加大近战武器的尺寸",
                bonus: new Bonuses { KbGlove = true, MeleeScaleGlove = true }));
            // The swung blades (972/983/997) and their shots come from SwordFx; the rest shoot as they swing.
            list.Add(Sword(ItemType.TrueNightsEdge, "真永夜刃", 675, 70, 4.75f, 32, price: 0, tier: 0, rare: 8, fx: MeleeFx.TrueNightsEdge, noMelee: true));
            list.Add(Sword(ItemType.TrueExcalibur, "真断钢剑", 674, 72, 4.5f, 18, price: 0, tier: 0, rare: 8, fx: MeleeFx.TrueExcalibur, noMelee: true));
            list.Add(Shooter(Sword(ItemType.Seedler, "种子弯刀", 3018, 50, 6f, 23, price: 0, tier: 0, rare: 5), 23, ProjKind.SeedlerSeed, 12f));
            list.Add(Sword(ItemType.TheHorsemansBlade, "无头骑士剑", 1826, 150, 7.5f, 26, price: 0, tier: 0, rare: 8, tip: "召唤南瓜头来攻击敌人",
                fx: MeleeFx.HorsemansBlade, noMelee: true));
            list.Add(Shooter(Sword(ItemType.InfluxWaver, "波涌之刃", 2880, 100, 4.5f, 20, price: 0, tier: 0, rare: 8), 20, ProjKind.InfluxWaver, 11f, 1.05f));
            list.Add(Shooter(Sword(ItemType.Meowmere, "彩虹猫之刃", 3063, 200, 6.5f, 14, price: 0, tier: 0, rare: 10), 14, ProjKind.Meowmere, 12f, 1.1f));
            var wrath = Shooter(Sword(ItemType.StarWrath, "狂星之怒", 3065, 170, 6.5f, 16, price: 0, tier: 0, rare: 10, tip: "使星星如雨般从天而降"), 16, ProjKind.StarWrath, 8f, 1.1f);
            wrath.UseSound = "Item_105";
            list.Add(wrath);
        }
    }
}
