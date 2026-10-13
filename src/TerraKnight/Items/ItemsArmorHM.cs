namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        // <generated> by scratchpad/tkgen/gen_armor.py: edit the table there.

        static ItemDef HmArmor(ItemType t, string name, int id, ArmorSlot slot, int sheet, int defense, ArmorSet set, int rare, string tip,
            Bonuses bonus = default, ArmorSet setBonus = ArmorSet.None)
        {
            var d = Armor(t, name, id, slot, sheet, defense, set, price: 0, tier: 0, rare: rare, tip: tip, bonus: bonus);
            d.SetBonus = setBonus;
            return d;
        }

        /// <summary>
        /// Every hardmode and endgame armor piece (accarmor-hm sec-H2): Item.SetDefaults,
        /// Player.GrantArmorBenefits, zh-Hans names and tooltips; the set bonus is the family's, or
        /// the one the piece picks (a head, the Beetle body).
        /// </summary>
        static void AddArmorHM(System.Collections.Generic.List<ItemDef> list)
        {
            // --- Cobalt
            list.Add(HmArmor(ItemType.CobaltHat, "钴帽", 371, ArmorSlot.Head, 29, 3, ArmorSet.Cobalt, 4, "最大魔力增加40\n魔法伤害提高10%\n魔法暴击率提高9%", new Bonuses { ManaMax = 40, MagicDamage = 0.1f, MagicCrit = 0.09f }, ArmorSet.CobaltCaster));
            list.Add(HmArmor(ItemType.CobaltHelmet, "钴头盔", 372, ArmorSlot.Head, 30, 14, ArmorSet.Cobalt, 4, "移动速度提高10%\n近战伤害提高15%", new Bonuses { MoveSpeed = 0.1f, MeleeDamage = 0.15f }, ArmorSet.CobaltMelee));
            list.Add(HmArmor(ItemType.CobaltMask, "钴面具", 373, ArmorSlot.Head, 31, 5, ArmorSet.Cobalt, 4, "远程伤害提高10%\n远程暴击率提高10%", new Bonuses { RangedDamage = 0.1f, RangedCrit = 0.1f }, ArmorSet.CobaltRanged));
            list.Add(HmArmor(ItemType.CobaltBreastplate, "钴胸甲", 374, ArmorSlot.Body, 17, 10, ArmorSet.Cobalt, 4, "暴击率提高5%", new Bonuses { AllCrit = 0.05f }));
            list.Add(HmArmor(ItemType.CobaltLeggings, "钴护腿", 375, ArmorSlot.Legs, 16, 8, ArmorSet.Cobalt, 4, "移动速度提高10%，伤害提高3%", new Bonuses { MeleeDamage = 0.03f, RangedDamage = 0.03f, MagicDamage = 0.03f, MinionDamage = 0.03f, MoveSpeed = 0.1f }));
            // --- Mythril
            list.Add(HmArmor(ItemType.MythrilHood, "秘银兜帽", 376, ArmorSlot.Head, 32, 3, ArmorSet.Mythril, 4, "最大魔力增加60\n魔法伤害提高15%", new Bonuses { ManaMax = 60, MagicDamage = 0.15f }, ArmorSet.MythrilCaster));
            list.Add(HmArmor(ItemType.MythrilHelmet, "秘银头盔", 377, ArmorSlot.Head, 33, 16, ArmorSet.Mythril, 4, "近战暴击率提高8%\n近战伤害提高10%", new Bonuses { MeleeCrit = 0.08f, MeleeDamage = 0.1f }, ArmorSet.MythrilMelee));
            list.Add(HmArmor(ItemType.MythrilHat, "秘银帽", 378, ArmorSlot.Head, 34, 6, ArmorSet.Mythril, 4, "远程伤害提高12%\n远程暴击率提高7%", new Bonuses { RangedDamage = 0.12f, RangedCrit = 0.07f }, ArmorSet.MythrilRanged));
            list.Add(HmArmor(ItemType.MythrilChainmail, "秘银链甲", 379, ArmorSlot.Body, 18, 12, ArmorSet.Mythril, 4, "伤害提高7%", new Bonuses { MeleeDamage = 0.07f, RangedDamage = 0.07f, MagicDamage = 0.07f, MinionDamage = 0.07f }));
            list.Add(HmArmor(ItemType.MythrilGreaves, "秘银护胫", 380, ArmorSlot.Legs, 17, 9, ArmorSet.Mythril, 4, "暴击率提高10%", new Bonuses { AllCrit = 0.1f }));
            // --- Adamantite
            list.Add(HmArmor(ItemType.AdamantiteHeadgear, "精金头饰", 400, ArmorSlot.Head, 35, 4, ArmorSet.Adamantite, 4, "最大魔力增加80\n魔法伤害和魔法暴击率各提高12%", new Bonuses { ManaMax = 80, MagicDamage = 0.12f, MagicCrit = 0.12f }, ArmorSet.AdamantiteCaster));
            list.Add(HmArmor(ItemType.AdamantiteHelmet, "精金头盔", 401, ArmorSlot.Head, 36, 22, ArmorSet.Adamantite, 4, "近战暴击率提高7%\n近战伤害提高14%", new Bonuses { MeleeCrit = 0.07f, MeleeDamage = 0.14f }, ArmorSet.AdamantiteMelee));
            list.Add(HmArmor(ItemType.AdamantiteMask, "精金面具", 402, ArmorSlot.Head, 37, 8, ArmorSet.Adamantite, 4, "远程伤害提高14%\n远程暴击率提高10%", new Bonuses { RangedDamage = 0.14f, RangedCrit = 0.1f }, ArmorSet.AdamantiteRanged));
            list.Add(HmArmor(ItemType.AdamantiteBreastplate, "精金胸甲", 403, ArmorSlot.Body, 19, 16, ArmorSet.Adamantite, 4, "伤害提高8%", new Bonuses { MeleeDamage = 0.08f, RangedDamage = 0.08f, MagicDamage = 0.08f, MinionDamage = 0.08f }));
            list.Add(HmArmor(ItemType.AdamantiteLeggings, "精金护腿", 404, ArmorSlot.Legs, 18, 12, ArmorSet.Adamantite, 4, "暴击率提高7%\n移动速度提高5%", new Bonuses { AllCrit = 0.07f, MoveSpeed = 0.05f }));
            // --- Palladium
            list.Add(HmArmor(ItemType.PalladiumMask, "钯金面具", 1205, ArmorSlot.Head, 83, 14, ArmorSet.Palladium, 4, "近战伤害提高12%\n近战速度提高12%", new Bonuses { MeleeDamage = 0.12f, MeleeSpeed = 0.12f }));
            list.Add(HmArmor(ItemType.PalladiumHelmet, "钯金头盔", 1206, ArmorSlot.Head, 84, 5, ArmorSet.Palladium, 4, "远程伤害提高9%\n远程暴击率提高9%", new Bonuses { RangedDamage = 0.09f, RangedCrit = 0.09f }));
            list.Add(HmArmor(ItemType.PalladiumHeadgear, "钯金头饰", 1207, ArmorSlot.Head, 85, 3, ArmorSet.Palladium, 4, "魔法伤害和魔法暴击率各提高9%\n最大魔力增加60", new Bonuses { MagicDamage = 0.09f, MagicCrit = 0.09f, ManaMax = 60 }));
            list.Add(HmArmor(ItemType.PalladiumBreastplate, "钯金胸甲", 1208, ArmorSlot.Body, 54, 10, ArmorSet.Palladium, 4, "伤害提高3%\n暴击率提高2%", new Bonuses { MeleeDamage = 0.03f, RangedDamage = 0.03f, MagicDamage = 0.03f, MinionDamage = 0.03f, AllCrit = 0.02f }));
            list.Add(HmArmor(ItemType.PalladiumLeggings, "钯金护腿", 1209, ArmorSlot.Legs, 49, 8, ArmorSet.Palladium, 4, "伤害提高2%\n暴击率提高1%", new Bonuses { MeleeDamage = 0.02f, RangedDamage = 0.02f, MagicDamage = 0.02f, MinionDamage = 0.02f, AllCrit = 0.01f }));
            // --- Orichalcum
            list.Add(HmArmor(ItemType.OrichalcumMask, "山铜面具", 1210, ArmorSlot.Head, 86, 19, ArmorSet.Orichalcum, 4, "近战伤害和近战速度各提高11%\n移动速度提高7%", new Bonuses { MeleeDamage = 0.11f, MeleeSpeed = 0.11f, MoveSpeed = 0.07f }));
            list.Add(HmArmor(ItemType.OrichalcumHelmet, "山铜头盔", 1211, ArmorSlot.Head, 87, 7, ArmorSet.Orichalcum, 4, "远程暴击率提高15%\n移动速度提高8%", new Bonuses { RangedCrit = 0.15f, MoveSpeed = 0.08f }));
            list.Add(HmArmor(ItemType.OrichalcumHeadgear, "山铜头饰", 1212, ArmorSlot.Head, 88, 4, ArmorSet.Orichalcum, 4, "魔法暴击率提高18%\n最大魔力增加80", new Bonuses { MagicCrit = 0.18f, ManaMax = 80 }));
            list.Add(HmArmor(ItemType.OrichalcumBreastplate, "山铜胸甲", 1213, ArmorSlot.Body, 55, 13, ArmorSet.Orichalcum, 4, "暴击率提高6%", new Bonuses { AllCrit = 0.06f }));
            list.Add(HmArmor(ItemType.OrichalcumLeggings, "山铜护腿", 1214, ArmorSlot.Legs, 50, 10, ArmorSet.Orichalcum, 4, "伤害提高8%、移动速度提高11%", new Bonuses { MoveSpeed = 0.11f, MeleeDamage = 0.08f, RangedDamage = 0.08f, MagicDamage = 0.08f, MinionDamage = 0.08f }));
            // --- Titanium
            list.Add(HmArmor(ItemType.TitaniumMask, "钛金面具", 1215, ArmorSlot.Head, 89, 23, ArmorSet.Titanium, 4, "近战伤害和近战暴击率各提高9%\n近战速度提高9%", new Bonuses { MeleeDamage = 0.09f, MeleeCrit = 0.09f, MeleeSpeed = 0.09f }));
            list.Add(HmArmor(ItemType.TitaniumHelmet, "钛金头盔", 1216, ArmorSlot.Head, 90, 8, ArmorSet.Titanium, 4, "远程伤害提高16%\n远程暴击率提高7%", new Bonuses { RangedDamage = 0.16f, RangedCrit = 0.07f }));
            list.Add(HmArmor(ItemType.TitaniumHeadgear, "钛金头饰", 1217, ArmorSlot.Head, 91, 4, ArmorSet.Titanium, 4, "魔法伤害提高16%、魔法暴击率提高7%\n最大魔力增加100", new Bonuses { MagicDamage = 0.16f, MagicCrit = 0.07f, ManaMax = 100 }));
            list.Add(HmArmor(ItemType.TitaniumBreastplate, "钛金胸甲", 1218, ArmorSlot.Body, 56, 15, ArmorSet.Titanium, 4, "伤害提高4%\n暴击率提高3%", new Bonuses { MeleeDamage = 0.04f, RangedDamage = 0.04f, MagicDamage = 0.04f, MinionDamage = 0.04f, AllCrit = 0.03f }));
            list.Add(HmArmor(ItemType.TitaniumLeggings, "钛金护腿", 1219, ArmorSlot.Legs, 51, 11, ArmorSet.Titanium, 4, "伤害和暴击率各提高3%\n移动速度提高6%", new Bonuses { MeleeDamage = 0.03f, RangedDamage = 0.03f, MagicDamage = 0.03f, MinionDamage = 0.03f, AllCrit = 0.03f, MoveSpeed = 0.06f }));
            // --- Frost
            list.Add(HmArmor(ItemType.FrostHelmet, "寒霜头盔", 684, ArmorSlot.Head, 46, 10, ArmorSet.Frost, 5, "近战伤害和远程伤害各提高16%", new Bonuses { RangedDamage = 0.16f, MeleeDamage = 0.16f }));
            list.Add(HmArmor(ItemType.FrostBreastplate, "寒霜胸甲", 685, ArmorSlot.Body, 27, 20, ArmorSet.Frost, 5, "近战暴击率和远程暴击率各提高11%", new Bonuses { MeleeCrit = 0.11f, RangedCrit = 0.11f }));
            list.Add(HmArmor(ItemType.FrostLeggings, "寒霜护腿", 686, ArmorSlot.Legs, 26, 13, ArmorSet.Frost, 5, "移动速度提高8%\n近战速度提高10%", new Bonuses { MoveSpeed = 0.08f, MeleeSpeed = 0.1f }));
            // --- Forbidden
            list.Add(HmArmor(ItemType.AncientBattleArmorHat, "禁戒面具", 3776, ArmorSlot.Head, 200, 6, ArmorSet.Forbidden, 5, "魔法伤害和召唤伤害各提高15%", new Bonuses { MagicDamage = 0.15f, MinionDamage = 0.15f }));
            list.Add(HmArmor(ItemType.AncientBattleArmorShirt, "禁戒长袍", 3777, ArmorSlot.Body, 198, 12, ArmorSet.Forbidden, 5, "最大魔力增加40\n召唤伤害提高10%\n仆从数量上限增加1", new Bonuses { ManaMax = 40, MinionDamage = 0.1f, MaxMinions = 1 }));
            list.Add(HmArmor(ItemType.AncientBattleArmorPants, "禁戒裤", 3778, ArmorSlot.Legs, 142, 8, ArmorSet.Forbidden, 5, "最大魔力增加40\n魔法伤害提高10%\n仆从数量上限增加1", new Bonuses { ManaMax = 40, MagicDamage = 0.1f, MaxMinions = 1 }));
            // --- Hallowed
            list.Add(HmArmor(ItemType.HallowedMask, "神圣面具", 559, ArmorSlot.Head, 43, 24, ArmorSet.Hallowed, 5, "近战伤害和近战暴击率各提高10%\n近战速度提高10%", new Bonuses { MeleeCrit = 0.1f, MeleeDamage = 0.1f, MeleeSpeed = 0.1f }));
            list.Add(HmArmor(ItemType.HallowedHelmet, "神圣头盔", 553, ArmorSlot.Head, 41, 9, ArmorSet.Hallowed, 5, "远程伤害提高15%\n远程暴击率提高8%", new Bonuses { RangedDamage = 0.15f, RangedCrit = 0.08f }));
            list.Add(HmArmor(ItemType.HallowedHeadgear, "神圣头饰", 558, ArmorSlot.Head, 42, 5, ArmorSet.Hallowed, 5, "最大魔力增加100\n魔法伤害和魔法暴击率各提高12%", new Bonuses { MagicDamage = 0.12f, MagicCrit = 0.12f, ManaMax = 100 }));
            list.Add(HmArmor(ItemType.HallowedHood, "神圣兜帽", 4873, ArmorSlot.Head, 254, 1, ArmorSet.Hallowed, 5, "仆从数量上限增加1\n召唤伤害提高10%", new Bonuses { MinionDamage = 0.1f, MaxMinions = 1 }, ArmorSet.HallowedSummoner));
            list.Add(HmArmor(ItemType.HallowedPlateMail, "神圣板甲", 551, ArmorSlot.Body, 24, 15, ArmorSet.Hallowed, 5, "暴击率提高7%", new Bonuses { AllCrit = 0.07f }));
            list.Add(HmArmor(ItemType.HallowedGreaves, "神圣护胫", 552, ArmorSlot.Legs, 23, 11, ArmorSet.Hallowed, 5, "伤害提高7%\n移动速度提高8%", new Bonuses { MeleeDamage = 0.07f, RangedDamage = 0.07f, MagicDamage = 0.07f, MinionDamage = 0.07f, MoveSpeed = 0.08f }));
            list.Add(HmArmor(ItemType.AncientHallowedMask, "远古神圣面具", 4896, ArmorSlot.Head, 255, 24, ArmorSet.Hallowed, 5, "近战伤害和近战暴击率各提高10%\n近战速度提高10%", new Bonuses { MeleeCrit = 0.1f, MeleeDamage = 0.1f, MeleeSpeed = 0.1f }));
            list.Add(HmArmor(ItemType.AncientHallowedHelmet, "远古神圣头盔", 4897, ArmorSlot.Head, 256, 9, ArmorSet.Hallowed, 5, "远程伤害提高15%\n远程暴击率提高8%", new Bonuses { RangedDamage = 0.15f, RangedCrit = 0.08f }));
            list.Add(HmArmor(ItemType.AncientHallowedHeadgear, "远古神圣头饰", 4898, ArmorSlot.Head, 257, 5, ArmorSet.Hallowed, 5, "最大魔力增加100\n魔法伤害和魔法暴击率各提高12%", new Bonuses { MagicDamage = 0.12f, MagicCrit = 0.12f, ManaMax = 100 }));
            list.Add(HmArmor(ItemType.AncientHallowedHood, "远古神圣兜帽", 4899, ArmorSlot.Head, 258, 1, ArmorSet.Hallowed, 5, "仆从数量上限增加1\n召唤伤害提高10%", new Bonuses { MinionDamage = 0.1f, MaxMinions = 1 }, ArmorSet.HallowedSummoner));
            list.Add(HmArmor(ItemType.AncientHallowedPlateMail, "远古神圣板甲", 4900, ArmorSlot.Body, 229, 15, ArmorSet.Hallowed, 5, "暴击率提高7%", new Bonuses { AllCrit = 0.07f }));
            list.Add(HmArmor(ItemType.AncientHallowedGreaves, "远古神圣护胫", 4901, ArmorSlot.Legs, 212, 11, ArmorSet.Hallowed, 5, "伤害提高7%\n移动速度提高8%", new Bonuses { MeleeDamage = 0.07f, RangedDamage = 0.07f, MagicDamage = 0.07f, MinionDamage = 0.07f, MoveSpeed = 0.08f }));
            // --- CrystalAssassin
            list.Add(HmArmor(ItemType.CrystalNinjaHelmet, "水晶刺客兜帽", 4982, ArmorSlot.Head, 261, 12, ArmorSet.CrystalAssassin, 5, "暴击率提高5%\n魔力消耗降低10%", new Bonuses { AllCrit = 0.05f, ManaCost = 0.1f }));
            list.Add(HmArmor(ItemType.CrystalNinjaChestplate, "水晶刺客上衣", 4983, ArmorSlot.Body, 230, 14, ArmorSet.CrystalAssassin, 5, "伤害提高5%\n10%几率省下弹药", new Bonuses { MeleeDamage = 0.05f, RangedDamage = 0.05f, MagicDamage = 0.05f, MinionDamage = 0.05f, AmmoSave = 0.1f }));
            list.Add(HmArmor(ItemType.CrystalNinjaLeggings, "水晶刺客裤", 4984, ArmorSlot.Legs, 213, 10, ArmorSet.CrystalAssassin, 5, "移动速度提高20%\n近战速度提高10%", new Bonuses { MeleeSpeed = 0.1f, MoveSpeed = 0.2f }));
            // --- Chlorophyte
            list.Add(HmArmor(ItemType.ChlorophyteMask, "叶绿面具", 1001, ArmorSlot.Head, 78, 20, ArmorSet.Chlorophyte, 7, "近战伤害提高16%\n近战暴击率提高6%", new Bonuses { MeleeDamage = 0.16f, MeleeCrit = 0.06f }, ArmorSet.ChlorophyteMelee));
            list.Add(HmArmor(ItemType.ChlorophyteHelmet, "叶绿头盔", 1002, ArmorSlot.Head, 79, 13, ArmorSet.Chlorophyte, 7, "远程伤害提高16%\n20%几率省下弹药", new Bonuses { RangedDamage = 0.16f, AmmoSave = 0.2f }));
            list.Add(HmArmor(ItemType.ChlorophyteHeadgear, "叶绿头饰", 1003, ArmorSlot.Head, 80, 7, ArmorSet.Chlorophyte, 7, "最大魔力增加80、魔力消耗降低17%\n魔法伤害提高16%", new Bonuses { MagicDamage = 0.16f, ManaMax = 80, ManaCost = 0.17f }));
            list.Add(HmArmor(ItemType.ChlorophyteVisor, "叶绿面罩", 5524, ArmorSlot.Head, 283, 2, ArmorSet.Chlorophyte, 7, "仆从数量上限增加1个\n召唤伤害增加16%", new Bonuses { MinionDamage = 0.16f, MaxMinions = 1 }, ArmorSet.ChlorophyteSummon));
            list.Add(HmArmor(ItemType.ChlorophytePlateMail, "叶绿板甲", 1004, ArmorSlot.Body, 51, 18, ArmorSet.Chlorophyte, 7, "伤害提高5%\n暴击率提高7%", new Bonuses { MeleeDamage = 0.05f, RangedDamage = 0.05f, MagicDamage = 0.05f, MinionDamage = 0.05f, AllCrit = 0.07f }));
            list.Add(HmArmor(ItemType.ChlorophyteGreaves, "叶绿护胫", 1005, ArmorSlot.Legs, 47, 13, ArmorSet.Chlorophyte, 7, "暴击率提高8%\n移动速度提高5%", new Bonuses { AllCrit = 0.08f, MoveSpeed = 0.05f }));
            // --- Turtle
            list.Add(HmArmor(ItemType.TurtleHelmet, "海龟头盔", 1316, ArmorSlot.Head, 99, 21, ArmorSet.Turtle, 8, "近战伤害提高6%\n敌人更有可能以你为目标", new Bonuses { MeleeDamage = 0.06f, Aggro = 250 }));
            list.Add(HmArmor(ItemType.TurtleScaleMail, "海龟铠甲", 1317, ArmorSlot.Body, 65, 27, ArmorSet.Turtle, 8, "近战伤害和近战暴击率各提高8%\n敌人更有可能以你为目标", new Bonuses { MeleeDamage = 0.08f, MeleeCrit = 0.08f, Aggro = 250 }));
            list.Add(HmArmor(ItemType.TurtleLeggings, "海龟护腿", 1318, ArmorSlot.Legs, 54, 17, ArmorSet.Turtle, 8, "近战暴击率提高4%\n敌人更有可能以你为目标", new Bonuses { MeleeCrit = 0.04f, Aggro = 250 }));
            // --- Tiki
            list.Add(HmArmor(ItemType.TikiMask, "提基面具", 1159, ArmorSlot.Head, 82, 6, ArmorSet.Tiki, 7, "仆从数量上限增加1\n召唤伤害提高10%\n鞭子攻击范围扩大10%", new Bonuses { MaxMinions = 1, MinionDamage = 0.1f, WhipRange = 0.1f }));
            list.Add(HmArmor(ItemType.TikiShirt, "提基衣", 1160, ArmorSlot.Body, 53, 17, ArmorSet.Tiki, 7, "仆从数量上限增加1\n召唤伤害提高10%", new Bonuses { MaxMinions = 1, MinionDamage = 0.1f }));
            list.Add(HmArmor(ItemType.TikiPants, "提基裤", 1161, ArmorSlot.Legs, 48, 12, ArmorSet.Tiki, 7, "仆从数量上限增加1\n召唤伤害提高10%", new Bonuses { MaxMinions = 1, MinionDamage = 0.1f }));
            // --- Spooky
            list.Add(HmArmor(ItemType.SpookyHelmet, "阴森头盔", 1832, ArmorSlot.Head, 134, 9, ArmorSet.Spooky, 8, "仆从数量上限增加1\n召唤伤害提高11%", new Bonuses { MaxMinions = 1, MinionDamage = 0.11f }));
            list.Add(HmArmor(ItemType.SpookyBreastplate, "阴森胸甲", 1833, ArmorSlot.Body, 95, 11, ArmorSet.Spooky, 8, "仆从数量上限增加2\n召唤伤害提高11%", new Bonuses { MaxMinions = 2, MinionDamage = 0.11f }));
            list.Add(HmArmor(ItemType.SpookyLeggings, "阴森护腿", 1834, ArmorSlot.Legs, 79, 10, ArmorSet.Spooky, 8, "仆从数量上限增加1\n召唤伤害提高11%\n移动速度提高20%", new Bonuses { MoveSpeed = 0.2f, MaxMinions = 1, MinionDamage = 0.11f }));
            // --- Shroomite
            list.Add(HmArmor(ItemType.ShroomiteHeadgear, "蘑菇矿头饰", 1546, ArmorSlot.Head, 103, 11, ArmorSet.Shroomite, 8, "弓的伤害提高12%\n远程暴击率提高5%", new Bonuses { RangedCrit = 0.05f, ArrowMult = 0.12f }));
            list.Add(HmArmor(ItemType.ShroomiteMask, "蘑菇矿面具", 1547, ArmorSlot.Head, 104, 11, ArmorSet.Shroomite, 8, "枪的伤害提高12%\n远程暴击率提高5%", new Bonuses { RangedCrit = 0.05f, BulletMult = 0.12f }));
            list.Add(HmArmor(ItemType.ShroomiteHelmet, "蘑菇矿头盔", 1548, ArmorSlot.Head, 105, 11, ArmorSet.Shroomite, 8, "特定远程伤害提高12%\n具体指发射器、飞镖枪或其他不发射射箭/子弹的武器\n远程暴击率提高5%", new Bonuses { RangedCrit = 0.05f, SpecialistMult = 0.12f }));
            list.Add(HmArmor(ItemType.ShroomiteBreastplate, "蘑菇矿胸甲", 1549, ArmorSlot.Body, 67, 24, ArmorSet.Shroomite, 8, "远程伤害和远程暴击率各提高13%\n20%几率省下弹药", new Bonuses { RangedCrit = 0.13f, RangedDamage = 0.13f, AmmoSave = 0.2f }));
            list.Add(HmArmor(ItemType.ShroomiteLeggings, "蘑菇矿护腿", 1550, ArmorSlot.Legs, 56, 16, ArmorSet.Shroomite, 8, "远程暴击率提高7%\n移动速度提高12%", new Bonuses { RangedCrit = 0.07f, MoveSpeed = 0.12f }));
            // --- Spectre
            list.Add(HmArmor(ItemType.SpectreHood, "幽灵兜帽", 1503, ArmorSlot.Head, 101, 6, ArmorSet.Spectre, 8, "", default, ArmorSet.SpectreHealing));
            list.Add(HmArmor(ItemType.SpectreMask, "幽灵面具", 2189, ArmorSlot.Head, 156, 18, ArmorSet.Spectre, 8, "最大魔力增加60、魔力消耗降低13%\n魔法伤害和魔法暴击率各提高10%", new Bonuses { ManaMax = 60, ManaCost = 0.13f, MagicDamage = 0.1f, MagicCrit = 0.1f }, ArmorSet.SpectreDamage));
            list.Add(HmArmor(ItemType.SpectreRobe, "幽灵长袍", 1504, ArmorSlot.Body, 66, 14, ArmorSet.Spectre, 8, "魔法伤害和魔法暴击率各提高7%", new Bonuses { MagicDamage = 0.07f, MagicCrit = 0.07f }));
            list.Add(HmArmor(ItemType.SpectrePants, "幽灵裤", 1505, ArmorSlot.Legs, 55, 10, ArmorSet.Spectre, 8, "魔法伤害提高8%\n移动速度提高8%", new Bonuses { MagicDamage = 0.08f, MoveSpeed = 0.08f }));
            // --- Beetle
            list.Add(HmArmor(ItemType.BeetleHelmet, "甲虫头盔", 2199, ArmorSlot.Head, 157, 23, ArmorSet.Beetle, 8, "近战伤害提高6%\n敌人更有可能以你为目标", new Bonuses { MeleeDamage = 0.06f, Aggro = 250 }));
            list.Add(HmArmor(ItemType.BeetleScaleMail, "甲虫铠甲", 2200, ArmorSlot.Body, 105, 20, ArmorSet.Beetle, 8, "近战伤害和近战暴击率各提高8%\n移动速度和近战速度各提高6%", new Bonuses { MeleeDamage = 0.08f, MeleeCrit = 0.08f, MeleeSpeed = 0.06f, MoveSpeed = 0.06f }, ArmorSet.BeetleDamage));
            list.Add(HmArmor(ItemType.BeetleShell, "甲虫壳", 2201, ArmorSlot.Body, 106, 32, ArmorSet.Beetle, 8, "近战伤害和近战暴击率各提高5%\n敌人更有可能以你为目标", new Bonuses { MeleeDamage = 0.05f, MeleeCrit = 0.05f, Aggro = 400 }, ArmorSet.BeetleDefense));
            list.Add(HmArmor(ItemType.BeetleLeggings, "甲虫护腿", 2202, ArmorSlot.Legs, 98, 18, ArmorSet.Beetle, 8, "移动速度和近战速度各提高6%\n敌人更有可能以你为目标", new Bonuses { MeleeSpeed = 0.06f, MoveSpeed = 0.06f, Aggro = 250 }));
            // --- Solar
            list.Add(HmArmor(ItemType.SolarFlareHelmet, "耀斑头盔", 2763, ArmorSlot.Head, 171, 24, ArmorSet.Solar, 10, "近战暴击率提高26%\n可小幅提高生命再生速度\n敌人更有可能以你为目标", new Bonuses { MeleeCrit = 0.26f, LifeRegen = 2, Aggro = 300 }));
            list.Add(HmArmor(ItemType.SolarFlareBreastplate, "耀斑胸甲", 2764, ArmorSlot.Body, 177, 34, ArmorSet.Solar, 10, "近战伤害提高29%\n可小幅提高生命再生速度\n敌人更有可能以你为目标", new Bonuses { MeleeDamage = 0.29f, LifeRegen = 2, Aggro = 300 }));
            list.Add(HmArmor(ItemType.SolarFlareLeggings, "耀斑护腿", 2765, ArmorSlot.Legs, 112, 20, ArmorSet.Solar, 10, "移动速度和近战速度各提高15%\n可小幅提高生命再生速度\n敌人更有可能以你为目标", new Bonuses { MeleeSpeed = 0.15f, MoveSpeed = 0.15f, LifeRegen = 2, Aggro = 300 }));
            // --- Vortex
            list.Add(HmArmor(ItemType.VortexHelmet, "星旋头盔", 2757, ArmorSlot.Head, 169, 14, ArmorSet.Vortex, 10, "远程伤害提高16%\n远程暴击率提高7%", new Bonuses { RangedCrit = 0.07f, RangedDamage = 0.16f }));
            list.Add(HmArmor(ItemType.VortexBreastplate, "星旋胸甲", 2758, ArmorSlot.Body, 175, 28, ArmorSet.Vortex, 10, "远程伤害和远程暴击率各提高12%\n25%几率省下弹药", new Bonuses { AmmoSave = 0.25f, RangedCrit = 0.12f, RangedDamage = 0.12f }));
            list.Add(HmArmor(ItemType.VortexLeggings, "星旋护腿", 2759, ArmorSlot.Legs, 110, 20, ArmorSet.Vortex, 10, "远程伤害和远程暴击率各提高8%\n移动速度提高10%", new Bonuses { RangedCrit = 0.08f, RangedDamage = 0.08f, MoveSpeed = 0.1f }));
            // --- Nebula
            list.Add(HmArmor(ItemType.NebulaHelmet, "星云头盔", 2760, ArmorSlot.Head, 170, 14, ArmorSet.Nebula, 10, "最大魔力增加60、魔力消耗降低15%\n魔法伤害和魔法暴击率各提高7%", new Bonuses { ManaMax = 60, ManaCost = 0.15f, MagicCrit = 0.07f, MagicDamage = 0.07f }));
            list.Add(HmArmor(ItemType.NebulaBreastplate, "星云胸甲", 2761, ArmorSlot.Body, 176, 18, ArmorSet.Nebula, 10, "魔法伤害和魔法暴击率各提高9%", new Bonuses { MagicDamage = 0.09f, MagicCrit = 0.09f }));
            list.Add(HmArmor(ItemType.NebulaLeggings, "星云护腿", 2762, ArmorSlot.Legs, 111, 14, ArmorSet.Nebula, 10, "魔法伤害提高10%\n移动速度提高10%", new Bonuses { MoveSpeed = 0.1f, MagicDamage = 0.1f }));
            // --- Stardust
            list.Add(HmArmor(ItemType.StardustHelmet, "星尘头盔", 3381, ArmorSlot.Head, 189, 10, ArmorSet.Stardust, 10, "仆从数量上限增加1\n哨兵数量上限增加1\n召唤伤害提高22%", new Bonuses { MaxMinions = 1, MaxTurrets = 1, MinionDamage = 0.22f }));
            list.Add(HmArmor(ItemType.StardustBreastplate, "星尘板甲", 3382, ArmorSlot.Body, 190, 16, ArmorSet.Stardust, 10, "仆从数量上限增加2\n召唤伤害提高22%\n鞭子攻击范围扩大15%", new Bonuses { MaxMinions = 2, WhipRange = 0.15f, MinionDamage = 0.22f }));
            list.Add(HmArmor(ItemType.StardustLeggings, "星尘护腿", 3383, ArmorSlot.Legs, 130, 12, ArmorSet.Stardust, 10, "仆从数量上限增加2\n召唤伤害提高22%\n鞭子攻击范围扩大15%", new Bonuses { MaxMinions = 2, WhipRange = 0.15f, MinionDamage = 0.22f }));
        }

        /// <summary>
        /// The pieces that only help one class craft with it (the rest are General). A method, not
        /// a field: Build runs from the static initializer, before this file's fields are set.
        /// </summary>
        static (ItemType, Kit)[] ArmorKits() => new[]
        {
            (ItemType.CobaltHat, Kit.Magic),
            (ItemType.CobaltHelmet, Kit.Melee),
            (ItemType.CobaltMask, Kit.Ranged),
            (ItemType.MythrilHood, Kit.Magic),
            (ItemType.MythrilHelmet, Kit.Melee),
            (ItemType.MythrilHat, Kit.Ranged),
            (ItemType.AdamantiteHeadgear, Kit.Magic),
            (ItemType.AdamantiteHelmet, Kit.Melee),
            (ItemType.AdamantiteMask, Kit.Ranged),
            (ItemType.PalladiumMask, Kit.Melee),
            (ItemType.PalladiumHelmet, Kit.Ranged),
            (ItemType.PalladiumHeadgear, Kit.Magic),
            (ItemType.OrichalcumMask, Kit.Melee),
            (ItemType.OrichalcumHelmet, Kit.Ranged),
            (ItemType.OrichalcumHeadgear, Kit.Magic),
            (ItemType.TitaniumMask, Kit.Melee),
            (ItemType.TitaniumHelmet, Kit.Ranged),
            (ItemType.TitaniumHeadgear, Kit.Magic),
            (ItemType.HallowedMask, Kit.Melee),
            (ItemType.HallowedHelmet, Kit.Ranged),
            (ItemType.HallowedHeadgear, Kit.Magic),
            (ItemType.HallowedHood, Kit.Summon),
            (ItemType.AncientHallowedMask, Kit.Melee),
            (ItemType.AncientHallowedHelmet, Kit.Ranged),
            (ItemType.AncientHallowedHeadgear, Kit.Magic),
            (ItemType.AncientHallowedHood, Kit.Summon),
            (ItemType.ChlorophyteMask, Kit.Melee),
            (ItemType.ChlorophyteHelmet, Kit.Ranged),
            (ItemType.ChlorophyteHeadgear, Kit.Magic),
            (ItemType.ChlorophyteVisor, Kit.Summon),
            (ItemType.TurtleHelmet, Kit.Melee),
            (ItemType.TurtleScaleMail, Kit.Melee),
            (ItemType.TurtleLeggings, Kit.Melee),
            (ItemType.TikiMask, Kit.Summon),
            (ItemType.TikiShirt, Kit.Summon),
            (ItemType.TikiPants, Kit.Summon),
            (ItemType.SpookyHelmet, Kit.Summon),
            (ItemType.SpookyBreastplate, Kit.Summon),
            (ItemType.SpookyLeggings, Kit.Summon),
            (ItemType.ShroomiteHeadgear, Kit.Ranged),
            (ItemType.ShroomiteMask, Kit.Ranged),
            (ItemType.ShroomiteHelmet, Kit.Ranged),
            (ItemType.ShroomiteBreastplate, Kit.Ranged),
            (ItemType.ShroomiteLeggings, Kit.Ranged),
            (ItemType.SpectreHood, Kit.Magic),
            (ItemType.SpectreMask, Kit.Magic),
            (ItemType.SpectreRobe, Kit.Magic),
            (ItemType.SpectrePants, Kit.Magic),
            (ItemType.BeetleHelmet, Kit.Melee),
            (ItemType.BeetleScaleMail, Kit.Melee),
            (ItemType.BeetleShell, Kit.Melee),
            (ItemType.BeetleLeggings, Kit.Melee),
            (ItemType.SolarFlareHelmet, Kit.Melee),
            (ItemType.SolarFlareBreastplate, Kit.Melee),
            (ItemType.SolarFlareLeggings, Kit.Melee),
            (ItemType.VortexHelmet, Kit.Ranged),
            (ItemType.VortexBreastplate, Kit.Ranged),
            (ItemType.VortexLeggings, Kit.Ranged),
            (ItemType.NebulaHelmet, Kit.Magic),
            (ItemType.NebulaBreastplate, Kit.Magic),
            (ItemType.NebulaLeggings, Kit.Magic),
            (ItemType.StardustHelmet, Kit.Summon),
            (ItemType.StardustBreastplate, Kit.Summon),
            (ItemType.StardustLeggings, Kit.Summon),
        };
    }
}
