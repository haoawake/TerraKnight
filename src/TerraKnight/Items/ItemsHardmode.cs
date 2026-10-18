namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// Hardmode equipment beyond the first set (Item.SetDefaults, 1.4.5.8; zh-Hans names and
        /// tooltips): the ore swords (plain swings with autoReuse), the spears, the boomerangs, the guns
        /// and the bows.
        /// </summary>
        static void AddHardmode(System.Collections.Generic.List<ItemDef> list)
        {
            list.Add(Sword(ItemType.CobaltSword, "钴剑", 483, 40, 5f, 19, price: 0, tier: 3, rare: 4));
            list.Add(Sword(ItemType.PalladiumSword, "钯金剑", 1185, 49, 5.5f, 22, price: 0, tier: 3, rare: 4));
            list.Add(Sword(ItemType.MythrilSword, "秘银剑", 484, 50, 6f, 20, price: 0, tier: 3, rare: 4));
            list.Add(Sword(ItemType.OrichalcumSword, "山铜剑", 1192, 59, 6f, 22, price: 0, tier: 3, rare: 4));
            list.Add(Sword(ItemType.AdamantiteSword, "精金剑", 482, 61, 6f, 21, price: 0, tier: 3, rare: 4));
            list.Add(Sword(ItemType.TitaniumSword, "钛金剑", 1199, 61, 6f, 20, price: 0, tier: 3, rare: 4));
            // Spears (useStyle 5, no autoReuse): the thrust's projectile reaches out and back over the use.
            foreach (var d in new[]
            {
                Spear(ItemType.CobaltNaginata, "钴薙刀", 537, 44, 4f, 28, ProjKind.CobaltNaginata, 4.3f, price: 0, tier: 3, rare: 4),
                Spear(ItemType.PalladiumPike, "钯金刺矛", 1186, 44, 4.5f, 27, ProjKind.PalladiumPike, 4.4f, price: 0, tier: 3, rare: 4),
                Spear(ItemType.MythrilHalberd, "秘银长戟", 390, 45, 5f, 26, ProjKind.MythrilHalberd, 4.5f, price: 0, tier: 3, rare: 4),
                Spear(ItemType.OrichalcumHalberd, "山铜长戟", 1193, 46, 5.5f, 25, ProjKind.OrichalcumHalberd, 4.5f, price: 0, tier: 3, rare: 4),
                Spear(ItemType.AdamantiteGlaive, "精金关刀", 406, 49, 6f, 25, ProjKind.AdamantiteGlaive, 5f, price: 0, tier: 3, rare: 4),
                Spear(ItemType.TitaniumTrident, "钛金三叉戟", 1200, 48, 6.2f, 23, ProjKind.TitaniumTrident, 5f, price: 0, tier: 3, rare: 4),
                Spear(ItemType.Gungnir, "永恒之枪", 550, 61, 6.4f, 22, ProjKind.Gungnir, 5.6f, price: 0, tier: 3, rare: 5),
                Spear(ItemType.MushroomSpear, "蘑菇长矛", 756, 60, 6.2f, 40, ProjKind.MushroomSpear, 5.5f, price: 0, tier: 3, rare: 7),
                Spear(ItemType.ChlorophytePartisan, "叶绿镋", 1228, 49, 6.2f, 23, ProjKind.ChlorophytePartisan, 5f, price: 0, tier: 3, rare: 7, tip: "射出孢子云"),
                Spear(ItemType.NorthPole, "北极", 1947, 80, 6.7f, 30, ProjKind.NorthPole, 4.75f, price: 0, tier: 3, rare: 7, tip: "射出下雪花的冰矛"),
            })
            {
                d.AutoReuse = false;
                list.Add(d);
            }
            // Boomerangs (useStyle 1, autoReuse, not drawn in hand).
            ItemDef Boomerang(ItemType t, string name, int id, int dmg, float kb, int use, ProjKind proj, float speed, int rare, string tip = null) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Melee, Damage = dmg, Knockback = kb, UseAnimation = use * Tick, UseTime = use * Tick,
                NoMelee = true, NoUseGraphic = true, Shoot = proj, ShootSpeed = speed, Tier = 3, Rare = rare, Tooltip = tip,
            };
            list.Add(Boomerang(ItemType.LightDisc, "光辉飞盘", 561, 60, 8f, 14, ProjKind.LightDisc, 16f, 5, "“你好，程序！”"));
            list.Add(Boomerang(ItemType.Bananarang, "香蕉回旋镖", 1324, 45, 6.5f, 11, ProjKind.Bananarang, 16f, 5));
            list.Add(Boomerang(ItemType.PossessedHatchet, "疯狂飞斧", 1122, 80, 5f, 14, ProjKind.PossessedHatchet, 12f, 7, "追杀敌人"));
            list.Add(Boomerang(ItemType.PaladinsHammer, "圣骑士锤", 1513, 100, 9f, 15, ProjKind.PaladinsHammer, 14f, 8, "威力强大的回旋锤"));

            // Guns (held, useStyle 5; Main.DrawPlayerItemPos's offsets). How each shoots: ShootPatternsRanged.cs.
            ItemDef G(ItemType t, string name, int id, int dmg, float kb, int use, float speed, string sound, int rare, float hx, float hy, int crit = 0,
                float scale = 1f, float save = 0f, string tip = null)
            {
                var d = Held(Gun(t, name, id, dmg, kb, use, speed, sound, price: 0, tier: 3, crit: crit, rare: rare, tip: tip, scale: scale), hx, hy);
                d.Bonus.AmmoSave = save;
                return d;
            }
            list.Add(G(ItemType.Megashark, "巨兽鲨", 533, 25, 1f, 7, 10f, "Item_11", 5, -7f, -2f, save: 0.5f, tip: "50%几率省下弹药\n“迷你鲨的老哥”"));
            // Three-round bursts every 24 ticks (useAnimation 12, useTime 4, reuseDelay 14); only the first round costs ammo.
            list.Add(G(ItemType.ClockworkAssaultRifle, "发条式突击步枪", 434, 17, 0f, 24, 7.75f, "Item_31", 4, 0f, -2f, tip: "三发点射\n只有第一发消耗弹药"));
            list.Add(G(ItemType.Uzi, "乌兹冲锋枪", 1265, 30, 3.5f, 9, 13f, "Item_11", 7, -8f, 4f, scale: 0.75f, tip: "射出强大的高速子弹"));
            list.Add(G(ItemType.VenusMagnum, "维纳斯万能枪", 1255, 50, 5.5f, 9, 13.5f, "Item_41", 7, 6f, 0f, scale: 0.85f, tip: "射出强大的高速子弹"));
            var sniper = G(ItemType.SniperRifle, "狙击步枪", 1254, 200, 8f, 36, 16f, "Item_40", 8, -6f, 2f, crit: 25, tip: "射出强大的高速子弹");
            sniper.AutoReuse = false;
            list.Add(sniper);
            list.Add(G(ItemType.TacticalShotgun, "战术霰弹枪", 679, 36, 7f, 34, 6f, "Item_38", 8, 0f, 2f));
            list.Add(G(ItemType.Gatligator, "鳄鱼机关枪", 2270, 21, 1.5f, 7, 8f, "Item_41", 4, -4f, 0f, save: 0.5f, tip: "50%几率省下弹药\n非常不精准"));
            list.Add(G(ItemType.ChainGun, "链式机枪", 1929, 38, 1.75f, 4, 14f, "Item_41", 8, 0f, 2f, save: 2f / 3f, tip: "66%几率省下弹药"));
            list.Add(G(ItemType.SDMG, "太空海豚机枪", 1553, 85, 2.5f, 5, 12f, "Item_40", 10, -10f, -2f, crit: 10, save: 2f / 3f, tip: "66%几率省下弹药\n“它来自太空边缘”"));

            // Bows and repeaters.
            ItemDef B(ItemType t, string name, int id, int dmg, float kb, int use, float speed, int rare, float hx = 10f, float hy = 0f, int crit = 0,
                float scale = 1f, string sound = "Item_5", string tip = null)
            {
                var d = Held(Bow(t, name, id, dmg, kb, use, speed, price: 0, tier: 3, rare: rare, tip: tip), hx, hy);
                d.Crit = crit;
                d.Scale = scale;
                d.UseSound = sound;
                return d;
            }
            list.Add(B(ItemType.CobaltRepeater, "钴连弩", 435, 35, 1.5f, 23, 9f, 4, -2f, -2f));
            list.Add(B(ItemType.PalladiumRepeater, "钯金连弩", 1187, 37, 1.75f, 22, 9.25f, 4, -2f, -2f));
            list.Add(B(ItemType.MythrilRepeater, "秘银连弩", 436, 39, 2f, 20, 9.5f, 4, -2f, -2f));
            list.Add(B(ItemType.OrichalcumRepeater, "山铜连弩", 1194, 40, 2f, 19, 9.75f, 4, -2f, -2f));
            list.Add(B(ItemType.AdamantiteRepeater, "精金连弩", 481, 42, 2.5f, 18, 10f, 4, -2f, -2f));
            list.Add(B(ItemType.TitaniumRepeater, "钛金连弩", 1201, 43, 2.5f, 17, 10.5f, 4, -2f, -2f));
            list.Add(B(ItemType.HallowedRepeater, "神圣连弩", 578, 53, 2.5f, 16, 11f, 4, -2f, -2f));
            list.Add(B(ItemType.DaedalusStormbow, "代达罗斯风暴弓", 3029, 38, 2.25f, 19, 12.5f, 6, 4f, 0f, tip: "从天上射下箭来"));
            list.Add(B(ItemType.ChlorophyteShotbow, "叶绿连弩", 1229, 34, 2.75f, 19, 11.5f, 7, -2f, -2f));
            list.Add(B(ItemType.Tsunami, "海啸", 2624, 53, 2f, 24, 10f, 8, 4f, 0f, tip: "一次射出5支箭"));
            list.Add(B(ItemType.Marrow, "骸骨弓", 682, 40, 4.7f, 19, 11f, 5, 4f, 0f, crit: 5, scale: 1.1f, tip: "发射高速骨箭，命中后碎成骨片"));
            list.Add(B(ItemType.IceBow, "冰雪弓", 725, 39, 4.5f, 14, 10f, 5, tip: "射出寒霜箭"));
            list.Add(B(ItemType.PulseBow, "脉冲弓", 2223, 80, 3f, 20, 7.75f, 8, 2f, -2f, crit: 7, sound: "Item_75", tip: "射出带电的箭"));
            list.Add(B(ItemType.ShadowFlameBow, "暗影焰弓", 3052, 47, 4.5f, 20, 11f, 5, crit: 3, sound: "Item_102", tip: "射出暗影焰箭"));

            // Magic (useStyle 5): the spell tomes held out, the staffs (Item.staff) turned along the aim. How each casts: ShootPatternsMagic.cs.
            ItemDef M(ItemType t, string name, int id, int dmg, float kb, int use, float speed, int mana, ProjKind proj, string sound, int rare, bool staff,
                float scale = 1f, string tip = null)
            {
                var d = Staff(t, name, id, dmg, kb, use, speed, mana, proj, sound, price: 0, tier: 3, rare: rare, tip: tip, scale: scale);
                d.StaffHold = staff;
                return d;
            }
            list.Add(M(ItemType.CrystalStorm, "水晶风暴", 518, 35, 5f, 7, 16f, 5, ProjKind.CrystalStormShard, "Item_9", 4, false, 0.9f, "召唤快速发射的水晶碎块"));
            list.Add(M(ItemType.CursedFlames, "诅咒焰", 519, 55, 6.5f, 15, 10f, 9, ProjKind.CursedFlame, "Item_20", 4, false, 0.9f, "召唤邪恶的火球"));
            // Three streams a use (useAnimation 18, useTime 6), the mana paid once.
            var shower = M(ItemType.GoldenShower, "黄金雨", 1336, 30, 4f, 6, 10f, 7, ProjKind.GoldenShowerStream, "Item_13", 4, false, tip: "喷射一阵灵液\n降低目标的防御力");
            shower.UseAnimation = 18 * Tick;
            shower.ManaEveryOtherShot = true;
            list.Add(shower);
            list.Add(M(ItemType.FrostStaff, "寒霜法杖", 726, 46, 5f, 12, 16f, 12, ProjKind.FrostBolt, "Item_20", 5, true, tip: "射出寒霜流"));
            list.Add(M(ItemType.PoisonStaff, "剧毒法杖", 1308, 43, 5.6f, 36, 13.5f, 22, ProjKind.PoisonFang, "Item_43", 6, true, tip: "射出能刺穿多个敌人的毒牙"));
            list.Add(M(ItemType.VenomStaff, "毒液法杖", 2188, 44, 7f, 30, 14f, 25, ProjKind.VenomFang, "Item_43", 7, true, tip: "射出能刺穿多个敌人的毒液尖牙"));
            list.Add(M(ItemType.UnholyTrident, "邪恶三叉戟", 683, 150, 6.66f, 27, 13f, 19, ProjKind.UnholyTrident, "Item_20", 6, true, tip: "召唤恶魔的三叉戟"));
            list.Add(M(ItemType.MeteorStaff, "流星法杖", 2750, 50, 4.5f, 10, 10f, 9, ProjKind.Meteor, "Item_88", 5, true, tip: "降下流星雨"));
            list.Add(M(ItemType.SpectreStaff, "幽灵法杖", 1446, 65, 6f, 24, 6f, 15, ProjKind.SpectreWisp, "Item_43", 8, true, tip: "召唤亡魂来追杀敌人"));
            var fork = M(ItemType.InfernoFork, "狱火叉", 1445, 70, 5f, 30, 8f, 18, ProjKind.InfernoBolt, "Item_73", 8, true, tip: "发射爆炸成熊熊狱火的火球");
            fork.AutoReuse = false;
            list.Add(fork);
            list.Add(M(ItemType.ShadowbeamStaff, "暗影束法杖", 1444, 80, 6f, 15, 6f, 7, ProjKind.Shadowbeam, "Item_72", 8, true, tip: "产生会在墙上反弹的暗影光束"));
            list.Add(Held(M(ItemType.HeatRay, "高温射线枪", 1295, 90, 3f, 10, 15f, 8, ProjKind.HeatRay, "Item_12", 7, false, tip: "射出灼热的高温射线\n“喔啦！！”"), 4f, 0f));
            list.Add(Held(M(ItemType.BubbleGun, "泡泡枪", 2623, 70, 3f, 9, 15f, 5, ProjKind.Bubble, "Item_85", 8, false, tip: "快速射出强劲的泡泡"), -30f, -4f));
            list.Add(M(ItemType.RazorbladeTyphoon, "利刃台风", 2622, 85, 5f, 40, 6f, 20, ProjKind.RazorbladeTyphoon, "Item_84", 8, false, 0.9f, "发射快速移动的刀轮"));
            // Two casts a use (useAnimation 10, useTime 5), two icicles each, the mana paid once; no sound.
            var blizzard = M(ItemType.BlizzardStaff, "暴雪法杖", 1931, 58, 4.5f, 5, 10f, 9, ProjKind.Icicle, null, 8, true, tip: "在一片区域内洒下冰锥");
            blizzard.UseAnimation = 10 * Tick;
            blizzard.ManaEveryOtherShot = true;
            list.Add(blizzard);
            // Three swords a use, 4 ticks apart, every 28 ticks (useAnimation 12, useTime 4, reuseDelay 18).
            var fracture = M(ItemType.SkyFracture, "裂天剑", 3787, 38, 6f, 28, 8.75f, 17, ProjKind.SkyFractureSword, "Item_9", 4, true);
            fracture.Crit = 20;
            list.Add(fracture);
            // Three leaves a use (useAnimation 15, useTime 5), the mana paid once; SoundID.LeafBlower is Item34 at 13%.
            var blower = Held(M(ItemType.LeafBlower, "吹叶机", 1178, 48, 4f, 5, 15f, 5, ProjKind.Leaf, "Item_34", 7, false, tip: "快速射出锋利的树叶"), 4f, 0f);
            blower.UseAnimation = 15 * Tick;
            blower.ManaEveryOtherShot = true;
            blower.UseSoundVolume = 0.13f;
            list.Add(blower);
            list.Add(Held(M(ItemType.LaserRifle, "激光步枪", 514, 29, 2.5f, 12, 17f, 8, ProjKind.PurpleLaser, "Item_12", 4, false), 0f, 3f));
            list.Add(M(ItemType.BatScepter, "蝙蝠权杖", 1801, 45, 3f, 12, 10f, 6, ProjKind.Bat, "Item_32", 8, true, tip: "召唤蝙蝠来攻击敌人"));
            list.Add(M(ItemType.CrystalVileShard, "魔晶碎块", 3051, 25, 3f, 33, 32f, 13, ProjKind.CrystalVileShard, null, 5, true, tip: "召唤巨大的水晶尖刺\n忽略10点敌人防御"));
            var sphere = M(ItemType.MagnetSphere, "磁球", 1266, 48, 6f, 20, 1.2f, 14, ProjKind.MagnetSphere, "Item_20", 8, false);
            sphere.AutoReuse = false;
            list.Add(sphere);
            // Swung (useStyle 1), steered while the button is held.
            var rainbow = Staff(ItemType.RainbowRod, "彩虹魔杖", 495, 50, 6f, 25, 6f, 21, ProjKind.RainbowRod, "Item_28", price: 0, tier: 3, hold: false, rare: 5, tip: "发射可控的彩虹");
            rainbow.Channel = true;
            rainbow.AutoReuse = false;
            list.Add(rainbow);
            var nimbus = Staff(ItemType.NimbusRod, "雨云魔杖", 1244, 30, 0f, 22, 16f, 30, ProjKind.NimbusCloudMoving, "Item_66", price: 0, tier: 3, hold: false, rare: 6, tip: "召唤云朵来向敌人降下大雨");
            nimbus.AutoReuse = false;
            list.Add(nimbus);
            // Sweep R5a: the Flower of Frost (I:15690-15702), Bee Gun (I:13785-13801), Wasp Gun (I:14197-14211), Nettle Burst (I:10598-10610),
            // Rainbow Gun (I:15626-15638), Staff of Earth (I:16027-16039) and Razorpine (I:20955-20970); shots in TerraProjectilesMagic5.cs.
            var frostFlower = Staff(ItemType.FlowerofFrost, "寒霜之花", 1264, 60, 6.5f, 12, 9f, 11, ProjKind.FrostBall, "Item_20", price: 0, tier: 3, hold: false, rare: 5, tip: "射出寒霜球");
            frostFlower.AutoReuse = false;
            list.Add(frostFlower);
            var beeGun = M(ItemType.BeeGun, "蜜蜂枪", 1121, 9, 0.25f, 12, 8f, 5, ProjKind.Bee, "Item_11", 2, false, 0.8f, "射出会追杀敌人的蜜蜂");
            beeGun.Tier = 2;
            list.Add(beeGun);
            list.Add(M(ItemType.WaspGun, "胡蜂枪", 1155, 31, 0.25f, 18, 9f, 10, ProjKind.Wasp, "Item_11", 8, false, tip: "忽略10点敌人防御"));
            list.Add(M(ItemType.NettleBurst, "爆裂藤蔓", 788, 35, 1f, 25, 32f, 12, ProjKind.NettleStem, null, 7, true, tip: "忽略10点敌人防御"));
            var rainbowGun = M(ItemType.RainbowGun, "彩虹枪", 1260, 45, 2.5f, 40, 16f, 20, ProjKind.RainbowHead, "Item_67", 8, false, tip: "射出造成连续伤害的彩虹");
            rainbowGun.AutoReuse = false;
            list.Add(rainbowGun);
            var earthStaff = M(ItemType.StaffofEarth, "大地法杖", 1296, 125, 7.5f, 24, 12f, 18, ProjKind.EarthBoulder, "Item_69", 7, true, tip: "召唤强大的巨石");
            earthStaff.Crit = 20;
            earthStaff.AutoReuse = false;
            list.Add(earthStaff);
            list.Add(M(ItemType.Razorpine, "剃刀松", 1930, 48, 3.25f, 8, 12f, 5, ProjKind.PineNeedle, "Item_39", 8, true, tip: "射出锋利的松针"));
            // Sweep R5b: the Crystal Serpent (I:28923-28937), Soul Drain (I:27747-27759, its mana paid once the box finds someone),
            // the Zapinators (I:37142-37163), Betsy's Wrath (I:34218-34234) and the Blood Thorn (I:36734-36747, raised; three thorns a use,
            // the mana paid once); shots in TerraProjectilesMagic6.cs.
            list.Add(M(ItemType.CrystalSerpent, "水晶蛇", 3209, 40, 4.4f, 29, 8.5f, 9, ProjKind.CrystalCharge, "Item_109", 5, true, tip: "射出爆炸水晶电荷"));
            var soulDrain = M(ItemType.SoulDrain, "夺命杖", 3006, 35, 2.5f, 12, 10f, 10, ProjKind.SoulDrainBox, null, 5, true, tip: "吸取敌人的生命");
            soulDrain.NoUseMana = true;
            soulDrain.PayManaAfterCheck = true;
            list.Add(soulDrain);
            foreach (var (zapType, zapName, zapId, zapDamage, zapRare, zapTier) in new[] { (ItemType.ZapinatorGray, "灰冲击枪", 4347, 42, 2, 1), (ItemType.ZapinatorOrange, "橙冲击枪", 4348, 100, 5, 3) })
            {
                var zap = M(zapType, zapName, zapId, zapDamage, 6f, 36, 15f, 16, ProjKind.ZapinatorLaser, "Item_158", zapRare, false, 0.75f, "“它可能坏了”");
                zap.AutoReuse = false;
                zap.Tier = zapTier;
                list.Add(zap);
            }
            var wrath = M(ItemType.BetsysWrath, "双足翼龙怒气", 3870, 100, 7f, 20, 14f, 14, ProjKind.BetsysWrathBolt, "Custom/dd2_betsy_fireball_shot_*", 8, true, tip: "泼洒能降低防御的瘴气！");
            wrath.UseSoundVolume = 0.4f;
            list.Add(wrath);
            var thorn = M(ItemType.SharpTears, "血荆棘", 4270, 40, 1f, 11, 32f, 20, ProjKind.SharpTear, null, 4, false, tip: "从地面召出血荆棘");
            thorn.UseAnimation = 33 * Tick;
            thorn.HoldOut = false;
            thorn.HoldUp = true;
            thorn.ManaEveryOtherShot = true;
            list.Add(thorn);
            // Sweep R5c: the Shadowflame Hex Doll (I:28237-28254, three tentacles a use), the Tome of Infinite Wisdom (I:34088-34103: five pages
            // a use; the right button a twister for twice the mana), the Laser Machinegun (I:26672-26687), the Stellar Tune (I:38758-38767:
            // two stars a use) and the Nightglow (I:39955-39972: five streaks a use); the mana paid once a use. Shots in TerraProjectilesMagic7.cs.
            var hexDoll = M(ItemType.ShadowFlameHexDoll, "暗影焰妖娃", 3053, 32, 3.75f, 7, 9f, 6, ProjKind.ShadowflameTentacle, "Item_103", 5, false, tip: "召唤暗影焰触手来攻击敌人");
            hexDoll.UseAnimation = 21 * Tick;
            hexDoll.Crit = 3;
            hexDoll.NoUseGraphic = true;
            hexDoll.ManaEveryOtherShot = true;
            list.Add(hexDoll);
            var bookStaff = M(ItemType.BookStaff, "无限智慧巨著", 3852, 36, 9f, 3, 11f, 20, ProjKind.BookStaffShot, "Custom/dd2_book_staff_cast_*", 5, true,
                tip: "想知道是谁把一本无限智慧巨著戳在一根棍子上……\n右键施放强大的龙卷风");
            bookStaff.UseAnimation = 25 * Tick;
            bookStaff.AltUse = true;
            bookStaff.ShotsPerUse = 5;
            bookStaff.ManaEveryOtherShot = true;
            list.Add(bookStaff);
            var laserGun = Channelled(M(ItemType.LaserMachinegun, "激光机枪", 2795, 60, 2f, 20, 20f, 6, ProjKind.LaserMachinegunHeld, null, 8, false));
            laserGun.NoUseMana = true;
            laserGun.PayManaAfterCheck = true;
            list.Add(laserGun);
            var guitar = M(ItemType.SparkleGuitar, "星星吉他", 4715, 85, 1.5f, 6, 1f, 12, ProjKind.StellarTuneStar, null, 5, false, tip: "“这些和弦简直太棒了”");
            guitar.UseAnimation = 12 * Tick;
            guitar.Crit = 20;
            guitar.ManaEveryOtherShot = true;
            list.Add(guitar);
            var nightglow = M(ItemType.Nightglow, "夜光", 4952, 50, 2.5f, 2, 17f, 23, ProjKind.NightglowStar, "Item_82", 8, false, 0.7f);
            nightglow.UseAnimation = 36 * Tick;
            nightglow.ShotsPerUse = 5;
            nightglow.ManaEveryOtherShot = true;
            list.Add(nightglow);
            // Sweep R5d, the 1.4.5 spells: Weather Pain (I:41017-41028), the Wand of Frosting (I:41108-41130), the Daybloom Staff (I:47414-47421:
            // five motes a use), the Glacier Fang (I:47427-47433: charged while held) and the Petalstorm (I:47439-47445); TerraProjectilesMagic8.cs.
            var weather = M(ItemType.WeatherPain, "天候棒", 5118, 13, 1f, 45, 1f, 30, ProjKind.WeatherPainCloud, "Item_66", 1, false, tip: "忽略10点敌人防御\n“真棒……呵呵”");
            weather.AutoReuse = false;
            weather.Tier = 2;
            list.Add(weather);
            // The plain Wand of Frosting (15, 26 ticks, speed 7, 2 mana, rare 1); 42/10 is the Remix seed's StrongerVariant.
            var frosting = Staff(ItemType.WandofFrosting, "结霜魔棒", 5147, 15, 0f, 26, 7f, 2, ProjKind.FrostSpark, "Item_8", price: 0, tier: 0, hold: false, crit: 10, rare: 1, tip: "射出一点寒霜");
            list.Add(frosting);
            var daybloom = M(ItemType.DaybloomStaff, "太阳花法杖", 6152, 8, 3f, 4, 6f, 5, ProjKind.DaybloomMote, "Item_8", 0, true, tip: "发射小型魔法弹\n“装满了希望、梦想和黏糊糊的凝胶！”");
            daybloom.UseAnimation = 40 * Tick;
            daybloom.ShotsPerUse = 5;
            daybloom.ManaEveryOtherShot = true;
            daybloom.Tier = 0;
            list.Add(daybloom);
            var glacier = M(ItemType.GlacierFang, "冰川之牙", 6153, 11, 2f, 24, 4f, 10, ProjKind.GlacierFangShot, "Item_210", 1, true, tip: "发射蓄能冰锥长枪");
            glacier.Crit = 11;
            glacier.Channel = true;
            glacier.AutoReuse = false;
            glacier.Tier = 1;
            list.Add(glacier);
            var petal = M(ItemType.Petalstorm, "神秘绽放", 6154, 20, 3.5f, 42, 5f, 20, ProjKind.PetalstormPetal, "Item_60", 2, true, tip: "绽放出神秘花瓣");
            petal.UseSoundVolume = 0.65f;
            petal.Tier = 2;
            list.Add(petal);
            // Sweep R5e, the 1.4.5 summons (I:43007-43022, 44473-44508, 47346-47382, 47488-47522); MinionsPal.cs, shots in TerraProjectilesPal.cs.
            // The Pals' spheres are thrown unseen (noUseGraphic).
            var mushroom = Staff2(ItemType.MushroomBoiStaff, "蘑菇法杖", 5456, 8, 2f, 36, MinionKind.MushroomBoi, TerraKnight.Player.PlayerBuffs.MushroomBoiMinion, "Custom/deadcells_mushroom_summon", 0, 1, 2,
                "召唤一个蘑菇小伙伴为你而战\n“蘑菇小伙伴，出击！”");
            mushroom.UseSoundVolume = 0.35f;
            list.Add(mushroom);
            foreach (var (palType, palName, palId, palDmg, palKb, palKind, palBuff, palRare, palTier, palTip) in new[]
            {
                (ItemType.CattivaStaff, "捣蛋猫", 5663, 11, 2f, MinionKind.Cattiva, TerraKnight.Player.PlayerBuffs.CattivaMinion, 5, 1, "召唤捣蛋猫来为你而战\n一位可爱的伙伴"),
                (ItemType.FoxsparksStaff, "火绒狐", 5664, 20, 3f, MinionKind.Foxsparks, TerraKnight.Player.PlayerBuffs.FoxsparksMinion, 4, 1, "召唤火绒狐为你而战\n火绒狐在场时再次使用并按住，施展火绒狐的“拥抱烈焰”技能，喷射火焰！"),
                (ItemType.TrustyCattivaStaff, "可靠的捣蛋猫", 6148, 22, 2f, MinionKind.TrustyCattiva, TerraKnight.Player.PlayerBuffs.TrustyCattivaMinion, 5, 3, "召唤可靠的捣蛋猫为你而战\n它对你的信赖使其更加强大，并强化了“连击拳”！"),
                (ItemType.TrustyFoxsparksStaff, "可靠的火绒狐", 6149, 40, 3f, MinionKind.TrustyFoxsparks, TerraKnight.Player.PlayerBuffs.TrustyFoxsparksMinion, 4, 3,
                    "召唤可靠的火绒狐为你而战\n火绒狐在场时再次使用并按住，施展火绒狐的“拥抱烈焰”技能，喷射火焰！\n它对你的信赖使其更加强大，并学会了“灵魂之火”！"),
            })
            {
                var pal = Staff2(palType, palName, palId, palDmg, palKb, 15, palKind, palBuff, null, 0, palTier, palRare, palTip);
                pal.NoUseGraphic = true;
                list.Add(pal);
            }
            list.Add(Staff2(ItemType.ClayPotStaff, "黏土蓓蕾法杖", 6161, 5, 2f, 36, MinionKind.ClayPot, TerraKnight.Player.PlayerBuffs.ClayPotMinion, "Item_82", 0, 0, 1, "召唤黏土蓓蕾为你而战"));
            list.Add(Staff2(ItemType.ForbiddenStaff, "毁灭法杖", 6164, 40, 4f, 36, MinionKind.ForbiddenRelic, TerraKnight.Player.PlayerBuffs.ForbiddenMinion, "Item_113", 0, 3, 5, "召唤禁戒遗迹为你而战"));
            // Notes play themselves (Item26); the harp itself makes no use sound.
            list.Add(M(ItemType.MagicalHarp, "魔法竖琴", 494, 42, 1.5f, 12, 4.5f, 5, ProjKind.HarpNote1, null, 5, false));
            list.Add(Staff(ItemType.ClingerStaff, "爬藤怪法杖", 3014, 43, 8f, 24, 15f, 40, ProjKind.ClingerWall, "Item_100", price: 0, tier: 3, hold: false, rare: 5, tip: "召唤诅咒焰墙"));
            // Sweep R6: the Killing Deck (I:43062-43078: swung, unseen in hand; its four throws in turn in TerraProjectilesGuns3.cs).
            var deck = Staff(ItemType.KillingDeck, "击杀牌叠", 5461, 22, 5f, 20, 12f, 8, ProjKind.KillingDeckCard, "Item_7", price: 0, tier: 3, hold: false, rare: 4,
                tip: "“宝贝，给你的对手一副‘死人牌’”");
            deck.NoUseGraphic = true;
            deck.AutoReuse = true;
            list.Add(deck);
            var flask = Staff(ItemType.ToxicFlask, "毒气瓶", 3105, 52, 4f, 45, 14f, 30, ProjKind.ToxicFlask, "Item_106", price: 0, tier: 3, hold: false, rare: 8);
            flask.NoUseGraphic = true;
            flask.AutoReuse = false;
            list.Add(flask);
            list.Add(M(ItemType.BookofSkulls, "骷髅头法书", 1313, 29, 3.5f, 32, 4.5f, 18, ProjKind.Skull, "Item_8", 2, false, 0.9f, "射出骷髅头"));
            list.Add(M(ItemType.SpiritFlame, "神灯烈焰", 3779, 85, 5f, 22, 3f, 14, ProjKind.SpiritFlame, "Item_117", 4, false));
            // The lunar spells (TerraProjectilesLunar.cs): the Nebula Blaze cast from the hand, unseen.
            var blaze = M(ItemType.NebulaBlaze, "星云烈焰", 3542, 130, 3f, 12, 6f, 12, ProjKind.NebulaBlaze, "Item_20", 10, false, tip: "“从猎户座腰带收到掌中”");
            blaze.NoUseGraphic = true;
            list.Add(blaze);
            list.Add(M(ItemType.LunarFlareBook, "月耀", 3570, 100, 4.5f, 10, 10f, 9, ProjKind.LunarFlare, "Item_88", 10, false, tip: "倾泻月耀球"));
            list.Add(M(ItemType.NebulaArcanum, "星云奥秘", 3476, 70, 5f, 30, 7f, 30, ProjKind.NebulaArcanum, "Item_117", 10, true, tip: "“召唤大量星体能量来追杀敌人”"));
            // Held out while the button is held (TerraProjectilesHeld.cs): what they hold pays the mana as it runs.
            ItemDef Channelled(ItemDef d)
            {
                d.Channel = true;
                d.NoUseGraphic = true;
                d.AutoReuse = false;
                return d;
            }
            list.Add(Channelled(M(ItemType.LastPrism, "终极棱镜", 3541, 100, 0.25f, 10, 30f, 12, ProjKind.LastPrism, null, 10, false, tip: "“发射生命解离彩虹”")));
            list.Add(Channelled(M(ItemType.ChargedBlasterCannon, "充能爆破炮", 2882, 100, 2f, 20, 14f, 14, ProjKind.ChargedBlaster, "Item_75", 8, false)));
            // Raised (useStyle 4), its gaze paying for itself; held, it keeps looking (autoReuse).
            var medusa = Channelled(M(ItemType.MedusaHead, "蛇发女妖头", 3269, 40, 2f, 20, 1f, 15, ProjKind.MedusaHead, null, 4, false));
            medusa.HoldOut = false;
            medusa.AutoReuse = true;
            medusa.NoUseMana = true;
            list.Add(medusa);
            list.Add(Staff(ItemType.IceRod, "冰雪魔杖", 496, 28, 2f, 9, 12f, 6, ProjKind.IceRodBolt, "Item_28", price: 0, tier: 3, hold: false, rare: 4, tip: "召唤一块冰"));
        }
    }
}
