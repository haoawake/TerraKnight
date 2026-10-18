namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The plain swung melee items (sweep S1.A; Item.SetDefaults, 1.4.5.8; zh-Hans names and
        /// tooltips): the wood and alternate-ore swords, the odd blades and clubs, the tools that
        /// swing like swords and the umbrellas (useStyle 3). Their own effects are
        /// <see cref="MeleeSpecials"/>'s; price and stage come from Sources().
        /// </summary>
        static void AddMelee2(System.Collections.Generic.List<ItemDef> list)
        {
            ItemDef M(ItemType t, string name, int id, int dmg, float kb, int anim, int use = 0, float scale = 1f, bool auto = false, int crit = 0, int rare = 0,
                string tip = null) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Melee, Damage = dmg, Knockback = kb, Crit = crit,
                UseAnimation = anim * Tick, UseTime = (use > 0 ? use : anim) * Tick, Scale = scale, AutoReuse = auto, Rare = rare, Tooltip = tip,
            };
            // The wood swords (I:1663-1673, 9000-9102, 12021-12031, 24414-24424, 26209-26219, 41947-41963).
            list.Add(M(ItemType.WoodenSword, "木剑", 24, 7, 5f, 20));
            list.Add(M(ItemType.EbonwoodSword, "乌木剑", 653, 11, 6f, 19));
            list.Add(M(ItemType.RichMahoganySword, "红木剑", 656, 8, 6f, 19));
            list.Add(M(ItemType.PearlwoodSword, "珍珠木剑", 659, 30, 7f, 15, auto: true));
            list.Add(M(ItemType.ShadewoodSword, "暗影木剑", 921, 11, 6f, 19));
            list.Add(M(ItemType.PalmWoodSword, "棕榈木剑", 2517, 8, 6f, 19));
            list.Add(M(ItemType.BorealWoodSword, "针叶木剑", 2745, 8, 6f, 20));
            list.Add(M(ItemType.AshWoodSword, "灰烬木剑", 5284, 8, 5f, 23));
            // The alternate ores' broadswords (I:31342-31580) and the cactus one (I:11629-11639).
            list.Add(M(ItemType.TinBroadsword, "锡宽剑", 3502, 10, 5.5f, 20));
            list.Add(M(ItemType.LeadBroadsword, "铅宽剑", 3496, 13, 5.5f, 20));
            list.Add(M(ItemType.SilverBroadsword, "银阔剑", 3514, 14, 6f, 20));
            list.Add(M(ItemType.TungstenBroadsword, "钨宽剑", 3490, 14, 6f, 19));
            list.Add(M(ItemType.CactusSword, "仙人掌剑", 881, 10, 4.5f, 30));
            // The odd blades.
            const string rapid = "快速攻击造成更多伤害";
            list.Add(M(ItemType.BoneSword, "骨剑", 1166, 19, 5.5f, 22, rare: 3));
            list.Add(M(ItemType.CandyCaneSword, "糖棒剑", 1909, 19, 5.3f, 27, scale: 1.1f, rare: 1));
            list.Add(M(ItemType.Katana, "武士刀", 2273, 18, 3.5f, 20, auto: true, crit: 15, rare: 1));
            list.Add(M(ItemType.FalconBlade, "猎鹰刃", 2608, 25, 6f, 20, auto: true, rare: 4, tip: rapid));
            list.Add(M(ItemType.Cutlass, "短弯刀", 672, 53, 4f, 16, scale: 1.1f, auto: true, rare: 4, tip: rapid));
            list.Add(M(ItemType.DyeTradersScimitar, "异域弯刀", 3349, 20, 4.25f, 18, rare: 2, tip: rapid));
            list.Add(M(ItemType.PurpleClubberfish, "紫挥棒鱼", 2330, 35, 8f, 35, scale: 1.15f, auto: true, rare: 1));
            list.Add(M(ItemType.Bladetongue, "舌锋剑", 3211, 55, 5.75f, 28, scale: 1.125f, auto: true, rare: 5, tip: "接触时喷出一股灵液"));
            list.Add(M(ItemType.BreakerBlade, "毁灭刃", 426, 70, 8f, 35, rare: 4, tip: "对未受伤的敌人造成更多伤害"));
            list.Add(M(ItemType.BloodButcherer, "血腥屠刀", 795, 22, 5f, 25, rare: 1));
            list.Add(M(ItemType.PsychoKnife, "变态人的刀", 3106, 85, 3.5f, 8, scale: 1.1f, auto: true, rare: 8, tip: "可进入隐形模式"));
            // {InputTrigger_InteractWithTile} is the right mouse button.
            list.Add(M(ItemType.DD2SquireDemonSword, "地狱之剑", 3823, 95, 6.5f, 20, scale: 1.1f, auto: true, rare: 5, tip: "按住右键来用护盾防护"));
            var arm = M(ItemType.ZombieArm, "僵尸臂", 1304, 15, 5.5f, 22, auto: true);
            arm.HoldStyle = 1;
            list.Add(arm);
            list.Add(M(ItemType.TentacleSpike, "触手钉锤", 5094, 20, 5.5f, 21, scale: 1.2f, rare: 2, tip: "将破坏性尖刺插入敌人\n“不要抓尖的一头。”"));
            list.Add(M(ItemType.HamBat, "火腿棍", 5096, 57, 6.5f, 20, scale: 1.2f, auto: true, rare: 4, tip: "你吃得越好，它的威力就越强大\n击败敌人后暂时提高治疗效果\n“这是用猪屁股做的武器！”"));
            list.Add(M(ItemType.BatBat, "蝙蝠棍", 5097, 36, 5.5f, 40, scale: 1.15f, rare: 2, tip: "击中时有可能治愈使用者\n“也许我可以用这个打些蝙蝠。”"));
            list.Add(M(ItemType.SlapHand, "拍拍手", 3258, 55, 20f, 20, scale: 1.1f, auto: true, crit: 15, rare: 4));
            list.Add(M(ItemType.TaxCollectorsStickOfDoom, "精致手杖", 3351, 40, 3.5f, 15, rare: 3, tip: "实现快速征税"));
            list.Add(M(ItemType.BladedGlove, "利刃手套", 1827, 14, 4f, 8, auto: true, rare: 2));
            list.Add(M(ItemType.FetidBaghnakhs, "臭虎爪", 3013, 60, 6f, 8, auto: true, rare: 5));
            // The tools that swing as weapons (their tile work isn't in yet).
            var reed = M(ItemType.BreathingReed, "芦苇呼吸管", 186, 10, 4f, 27, use: 19, rare: 1, tip: "延长呼吸时间并可在水中呼吸");
            reed.HoldStyle = 2;
            list.Add(reed);
            list.Add(M(ItemType.StaffofRegrowth, "再生法杖", 213, 7, 3f, 25, use: 13, auto: true, rare: 3, tip: "让泥土上长出草\n用它进行采集可提高炼金植物的采集量"));
            list.Add(M(ItemType.Sickle, "镰刀", 1786, 9, 2.25f, 24, auto: true, tip: "可从草地收集干草"));
            list.Add(M(ItemType.GravediggerShovel, "掘墓者铲子", 4711, 12, 3.5f, 22, use: 14, auto: true, rare: 1, tip: "挖掘区域要比镐大\n只能挖掘软图格\n“明白了吗？”"));
            list.Add(M(ItemType.LucyTheAxe, "露西斧", 5095, 27, 5f, 15, scale: 1.2f, auto: true, crit: 10, rare: 2, tip: "“我爱露西！”"));
            // The umbrellas (useStyle 3, holdStyle 2): held over the head they slow the fall (I:12322-12332, 38685-38695).
            foreach (var (t, name, id, dmg, rare) in new[] { (ItemType.Umbrella, "伞", 946, 10, 1), (ItemType.TragicUmbrella, "悲剧雨伞", 4707, 15, 2) })
            {
                var u = M(t, name, id, dmg, 5f, 22, rare: rare, tip: "持有此物可减缓掉落速度");
                u.Thrust = true;
                u.HoldStyle = 2;
                list.Add(u);
            }
            AddPhaseblades(list);
        }

        /// <summary>
        /// The phaseblades, phasesabers and the Keybrand (sweep S1.B; I:3834-3849, 33349-33364,
        /// 36592-36607, 43623-43638, 44569-44603, 9235-9250): swung, and since 1.4.5 each use also
        /// throws the blade (<see cref="TerraProjectiles"/>' phaseblade AI); the swords that shoot
        /// something else (the Mandible Blade, the Stylish Scissors, the Waffle Iron, the Christmas
        /// Tree Sword).
        /// </summary>
        static void AddPhaseblades(System.Collections.Generic.List<ItemDef> list)
        {
            ItemDef S(ItemType t, string name, int id, int dmg, float kb, int use, ProjKind proj, float speed, int rare, int crit = 0, float scale = 1f, string sound = "Item_15",
                string tip = null) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Melee, Damage = dmg, Knockback = kb, Crit = crit, UseAnimation = use * Tick, UseTime = use * Tick,
                Scale = scale, AutoReuse = true, Shoot = proj, ShootSpeed = speed, UseSound = sound, Rare = rare, Tooltip = tip,
            };
            foreach (var (t, name, id, proj) in new[]
            {
                (ItemType.BluePhaseblade, "蓝陨石光剑", 198, ProjKind.PhaseBlue), (ItemType.RedPhaseblade, "红陨石光剑", 199, ProjKind.PhaseRed),
                (ItemType.GreenPhaseblade, "绿陨石光剑", 200, ProjKind.PhaseGreen), (ItemType.PurplePhaseblade, "紫陨石光剑", 201, ProjKind.PhasePurple),
                (ItemType.WhitePhaseblade, "白陨石光剑", 202, ProjKind.PhaseWhite), (ItemType.YellowPhaseblade, "黄陨石光剑", 203, ProjKind.PhaseYellow),
                (ItemType.OrangePhaseblade, "橙陨石光剑", 4258, ProjKind.PhaseOrange), (ItemType.PinkPhaseblade, "粉陨石光剑", 5535, ProjKind.PhasePink),
                (ItemType.RainbowPhaseblade, "彩虹相位剑", 5670, ProjKind.PhaseRainbow),
            })
                list.Add(S(t, name, id, 26, 3f, 18, proj, 14f, 1));
            foreach (var (t, name, id, proj) in new[]
            {
                (ItemType.BluePhasesaber, "蓝晶光刃", 3764, ProjKind.SaberBlue), (ItemType.RedPhasesaber, "红晶光刃", 3765, ProjKind.SaberRed),
                (ItemType.GreenPhasesaber, "绿晶光刃", 3766, ProjKind.SaberGreen), (ItemType.PurplePhasesaber, "紫晶光刃", 3767, ProjKind.SaberPurple),
                (ItemType.WhitePhasesaber, "白晶光刃", 3768, ProjKind.SaberWhite), (ItemType.YellowPhasesaber, "黄晶光刃", 3769, ProjKind.SaberYellow),
                (ItemType.OrangePhasesaber, "橙晶光刃", 4259, ProjKind.SaberOrange), (ItemType.PinkPhasesaber, "粉晶光刃", 5536, ProjKind.SaberPink),
                (ItemType.RainbowPhasesaber, "彩虹相位刃", 5671, ProjKind.SaberRainbow),
            })
                list.Add(S(t, name, id, 50, 3f, 16, proj, 14f, 4));
            list.Add(S(ItemType.Keybrand, "钥匙剑", 671, 105, 6.5f, 20, ProjKind.KeybrandThrown, 15.9f, 8, crit: 16, sound: "Item_1", tip: "对受伤的敌人造成更多伤害"));
            list.Add(S(ItemType.AntlionClaw, "颌骨剑", 3772, 16, 4.5f, 15, ProjKind.MandibleBlade, 6.5f, 2, crit: 7, scale: 1.1f, sound: "Item_1"));
            list.Add(S(ItemType.StylistKilLaKillScissorsIWish, "时尚剪刀", 3352, 14, 5f, 13, ProjKind.StylishScissors, 7.5f, 2, sound: "Item_1"));
            list.Add(S(ItemType.WaffleIron, "华夫饼烘烤模", 5382, 50, 4.75f, 23, ProjKind.Waffle, 11f, 5, sound: "Item_1", tip: "“现在是华夫饼时间！”"));
            list.Add(S(ItemType.ChristmasTreeSword, "圣诞树剑", 1928, 86, 7f, 23, ProjKind.ChristmasOrnament, 5f, 8, scale: 1.1f, sound: "Item_1", tip: "射出圣诞装饰"));
            AddSwordShots(list);
        }

        /// <summary>
        /// The swords that shoot (sweep S1.C; I:9320-9333, 9826-9840, 15123-15138, 16148-16160,
        /// 16451-16464, 34070-34085): the Frostbrand and the Beam Sword swing more often than they
        /// shoot (useTime past useAnimation), the sickles fling their scythes, the Chlorophyte
        /// Claymore cuts only with its slash, the Flying Dragon sends a sonic boom.
        /// </summary>
        static void AddSwordShots(System.Collections.Generic.List<ItemDef> list)
        {
            list.Add(Shooter(Sword(ItemType.Frostbrand, "寒霜剑", 676, 49, 4.5f, 23, price: 0, tier: 0, rare: 5, tip: "射出冰矢"), 30, ProjKind.FrostbrandBolt, 12f));
            list.Add(Shooter(Sword(ItemType.BeamSword, "光束剑", 723, 52, 6.5f, 20, price: 0, tier: 0, rare: 4, tip: "射出一束光"), 35, ProjKind.BeamSwordBeam, 11f));
            var ice = Sword(ItemType.IceSickle, "冰雪镰刀", 1306, 50, 5.5f, 25, price: 0, tier: 0, rare: 5, tip: "射出冰镰刀");
            ice.Shoot = ProjKind.IceSickleShot;
            ice.ShootSpeed = 12f;
            ice.Scale = 1.15f;
            ice.AutoReuse = false;
            list.Add(ice);
            var death = Sword(ItemType.DeathSickle, "死神镰刀", 1327, 57, 5f, 25, price: 0, tier: 0, rare: 6, tip: "射出致命镰刀");
            death.Shoot = ProjKind.DeathSickleShot;
            death.ShootSpeed = 9f;
            death.UseSound = "Item_71";
            list.Add(death);
            list.Add(Sword(ItemType.ChlorophyteClaymore, "叶绿双刃刀", 1226, 95, 6f, 32, price: 0, tier: 0, rare: 7, tip: "挥舞时释放孢子云\n猛击地面时释放大量孢子云",
                fx: MeleeFx.ChlorophyteClaymore, noMelee: true));
            var dragon = Sword(ItemType.DD2SquireBetsySword, "飞龙", 3827, 180, 5.5f, 20, price: 0, tier: 0, rare: 8, tip: "向前释放心的能量");
            dragon.Shoot = ProjKind.SonicBoom;
            dragon.ShootSpeed = 17f;
            dragon.UseSound = "Custom/dd2_sonic_boom_blade_slash_*";
            list.Add(dragon);
            AddShortswords(list);
        }

        /// <summary>
        /// The other shortswords (sweep S1.D; I:1457-1471 and their overrides, 7168-7182,
        /// 15141-15156, 37700-37714): stabbed at the cursor (useStyle 13, <see cref="SwordFx"/>'s
        /// AI_161); the Gladius, the Ruler and the Chlorophyte Saber stab again every useTime of
        /// the use, a little scattered.
        /// </summary>
        static void AddShortswords(System.Collections.Generic.List<ItemDef> list)
        {
            ItemDef Stab(ItemType t, string name, int id, int dmg, float kb, int anim, int use = 0, float scale = 1f, bool auto = false, int rare = 0, string tip = null) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Melee, Damage = dmg, Knockback = kb, UseAnimation = anim * Tick, UseTime = (use > 0 ? use : anim) * Tick,
                Scale = scale, NoMelee = true, NoUseGraphic = true, Fx = MeleeFx.Shortsword, AutoReuse = auto, Rare = rare, Tooltip = tip,
            };
            list.Add(Stab(ItemType.IronShortsword, "铁短剑", 6, 8, 4f, 12));
            list.Add(Stab(ItemType.TinShortsword, "锡短剑", 3501, 7, 4f, 12, scale: 0.85f));
            list.Add(Stab(ItemType.LeadShortsword, "铅短剑", 3495, 9, 4f, 12, scale: 0.925f));
            list.Add(Stab(ItemType.SilverShortsword, "银短剑", 3513, 9, 4f, 12, scale: 0.95f));
            list.Add(Stab(ItemType.TungstenShortsword, "钨短剑", 3489, 10, 4f, 11, scale: 0.95f));
            list.Add(Stab(ItemType.GoldShortsword, "金短剑", 3519, 12, 5f, 11, scale: 0.95f));
            list.Add(Stab(ItemType.PlatinumShortsword, "铂金短剑", 3483, 13, 5f, 10, scale: 0.975f));
            list.Add(Stab(ItemType.Gladius, "罗马短剑", 4463, 15, 3f, 18, use: 6, tip: "“你不觉得刺激吗？！”"));
            list.Add(Stab(ItemType.Ruler, "标尺", 486, 12, 0.5f, 20, use: 5, auto: true));
            list.Add(Stab(ItemType.ChlorophyteSaber, "叶绿军刀", 1227, 80, 5f, 30, use: 6, auto: true, rare: 7, tip: "附魔有发光孢子"));
            // The held blades (I:30785-30801, 35967-35983, 39891-39908): channelled, a whirl or a flurry out along the aim while held.
            ItemDef Held(ItemType t, string name, int id, int dmg, float kb, int anim, int use, ProjKind proj, int rare, int crit = 0, string tip = null) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Melee, Damage = dmg, Knockback = kb, Crit = crit, UseAnimation = anim * Tick, UseTime = use * Tick,
                NoMelee = true, NoUseGraphic = true, HoldOut = true, Channel = true, AutoReuse = false, Shoot = proj, ShootSpeed = 15f, UseSound = null, Rare = rare, Tooltip = tip,
            };
            list.Add(Held(ItemType.Arkhalis, "Arkhalis剑", 3368, 25, 4f, 25, 15, ProjKind.ArkhalisSlash, 9, tip: "“我不是从虚身上打出这个的”"));
            list.Add(Held(ItemType.Terragrim, "泰拉魔刃", 4144, 17, 3f, 25, 15, ProjKind.TerragrimSlash, 2, tip: "释放切割疾风"));
            list.Add(Held(ItemType.PiercingStarlight, "星光", 4923, 80, 4f, 18, 6, ProjKind.PiercingStarlight, 8, crit: 10, tip: "以无与伦比的速度刺击"));
            // The True Copper Shortsword (I:44548-44565): the Zenith's three thrown swords, every one a copper shortsword (Pl:49188-49201).
            // The plain spears (sweep S1.E; I:4688-4701, 10795-10809, 23404-23435, 44672-44685): thrust out and back over the use.
            list.Add(Spear(ItemType.Spear, "长矛", 280, 8, 6.5f, 31, ProjKind.SpearThrust, 3.7f, price: 0, tier: 0));
            list.Add(Spear(ItemType.TheRottedFork, "腐叉", 802, 17, 5f, 31, ProjKind.RottedForkThrust, 4f, price: 0, tier: 0, rare: 1));
            list.Add(Spear(ItemType.Swordfish, "剑鱼", 2332, 19, 4.25f, 20, ProjKind.SwordfishThrust, 4f, price: 0, tier: 0, rare: 2));
            var obsidian = Spear(ItemType.ObsidianSwordfish, "黑曜石剑鱼", 2331, 70, 6.5f, 20, ProjKind.ObsidianSwordfishThrust, 4f, price: 0, tier: 0, rare: 7);
            obsidian.Crit = 20;
            list.Add(obsidian);
            list.Add(Spear(ItemType.SlimeSpear, "史莱姆长矛", 5687, 9, 6.5f, 24, ProjKind.SlimeSpearThrust, 5.5f, price: 0, tier: 0));
            // The Scourge of the Corruptor (I:18840-18857): held out, a javelin that bursts into tiny eaters.
            list.Add(new ItemDef
            {
                Type = ItemType.ScourgeoftheCorruptor, Name = "腐化者之戟", Texture = "Item_1571", Class = DamageClass.Melee, Damage = 70, Knockback = 5f,
                UseAnimation = 20 * Tick, UseTime = 20 * Tick, HoldOut = true, NoMelee = true, NoUseGraphic = true, Shoot = ProjKind.ScourgeJavelin, ShootSpeed = 14f,
                UseSound = "Item_39", Rare = 8, Tooltip = "能释放小吞噬怪的强大标枪",
            });
            // The boomerangs and knives (sweep S1.F; I:9217-9231, 18817-18832, 20199-20212, 20831-20844, 28056-28071, 28257-28273, 39143-39204,
            // 39443-39456, 42039-42053, 43082-43096, 43448-43459): thrown, not drawn in hand; only the Trimarang, the knives and the Flint repeat.
            ItemDef Throw(ItemType t, string name, int id, int dmg, float kb, int use, ProjKind proj, float speed, int rare, bool auto, int crit = 0, string tip = null, string sound = "Item_1") => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Melee, Damage = dmg, Knockback = kb, Crit = crit, UseAnimation = use * Tick, UseTime = use * Tick,
                NoMelee = true, NoUseGraphic = true, AutoReuse = auto, Shoot = proj, ShootSpeed = speed, Rare = rare, Tooltip = tip, UseSound = sound,
            };
            list.Add(Throw(ItemType.IceBoomerang, "冰雪回旋镖", 670, 21, 8.5f, 20, ProjKind.IceBoomerangProj, 11.5f, 1, false, crit: 2));
            list.Add(Throw(ItemType.FruitcakeChakram, "水果蛋糕旋刃", 1918, 19, 8f, 15, ProjKind.FruitcakeChakramProj, 11f, 1, false));
            list.Add(Throw(ItemType.BloodyMachete, "血腥砍刀", 1825, 20, 5f, 15, ProjKind.BloodyMacheteProj, 15f, 2, false));
            list.Add(Throw(ItemType.Shroomerang, "蘑菇回旋镖", 4764, 23, 7f, 20, ProjKind.ShroomerangProj, 10.5f, 1, false));
            list.Add(Throw(ItemType.CombatWrench, "战斗扳手", 4818, 25, 3.5f, 15, ProjKind.CombatWrenchProj, 8.5f, 2, false, tip: "“‘修理’它们”"));
            list.Add(Throw(ItemType.Trimarang, "三尖回旋镖", 5298, 21, 4.5f, 20, ProjKind.TrimarangProj, 11.5f, 0, true, tip: "“三个回旋镖总比一个好”"));
            list.Add(Throw(ItemType.Axearang, "斧头回旋镖", 5519, 20, 5f, 20, ProjKind.AxearangProj, 10.5f, 0, false, tip: "“伸手砍人”"));
            list.Add(Throw(ItemType.BouncingShield, "中士联盾", 4760, 80, 2f, 36, ProjKind.BouncingShieldProj, 10f, 5, false, tip: "“这个我可以玩一整天”"));
            list.Add(Throw(ItemType.VampireKnives, "吸血鬼刀", 1569, 29, 2.75f, 16, ProjKind.VampireKnife, 15f, 8, true, tip: "快速投掷出吸血飞刀", sound: "Item_39"));
            list.Add(Throw(ItemType.ShadowFlameKnife, "暗影焰刀", 3054, 43, 5.75f, 12, ProjKind.ShadowFlameKnifeProj, 13f, 5, true, crit: 3, tip: "击中时造成暗影焰"));
            var flyingKnife = Throw(ItemType.FlyingKnife, "飞刀", 3030, 40, 4.5f, 15, ProjKind.FlyingKnifeProj, 17f, 6, false, tip: "投掷可控的飞刀");
            flyingKnife.Channel = true;
            list.Add(flyingKnife);
            // The Flint (I:43082-43096): swung in hand (not thrown), charged while held.
            list.Add(new ItemDef
            {
                Type = ItemType.DeadCellsFlint, Name = "燧石", Texture = "Item_5462", Class = DamageClass.Melee, Damage = 70, Knockback = 5f, UseAnimation = 15 * Tick, UseTime = 15 * Tick,
                NoMelee = true, Channel = true, Shoot = ProjKind.FlintHold, ShootSpeed = 4f, Rare = 0, Tooltip = "“一位被抑郁球侵蚀的前监狱长伸出援手”",
            });
            // The chained weapons (sweep S1.G; I:16043-16058, 16268-16291, 16414-16436, 24020-24033, 27842-27858, 31124-31139).
            ItemDef Chain(ItemType t, string name, int id, int dmg, float kb, int use, ProjKind proj, float speed, int rare, bool auto, bool held, bool shown, string sound, float scale = 1f, string tip = null) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Melee, Damage = dmg, Knockback = kb, UseAnimation = use * Tick, UseTime = use * Tick, Scale = scale,
                NoMelee = true, NoUseGraphic = !shown, HoldOut = held, AutoReuse = auto, Shoot = proj, ShootSpeed = speed, Rare = rare, UseSound = sound, Tooltip = tip,
            };
            list.Add(Chain(ItemType.GolemFist, "石巨人之拳", 1297, 90, 12f, 24, ProjKind.GolemFistProj, 14f, 7, true, true, true, "Item_10", 0.9f, "以石巨人之力出拳"));
            list.Add(Chain(ItemType.KOCannon, "致胜炮", 1314, 18, 5f, 25, ProjKind.BoxingGlove, 15f, 4, false, true, true, "Item_174", 0.9f, "射出拳击手套"));
            // The plain Chain Knife (12, 20 ticks, not autoReuse); 60/15 is the Remix seed's StrongerVariant.
            list.Add(Chain(ItemType.ChainKnife, "链刀", 1325, 12, 3.5f, 20, ProjKind.ChainKnifeProj, 12f, 1, false, true, false, "Item_1"));
            list.Add(Chain(ItemType.ChainGuillotines, "铁链血滴子", 3012, 59, 3.25f, 14, ProjKind.ChainGuillotine, 9f, 5, true, false, false, "Item_1"));
            list.Add(Chain(ItemType.Anchor, "锚", 2424, 70, 8f, 20, ProjKind.AnchorProj, 20f, 3, false, false, false, "Item_1"));
            var eruption = Chain(ItemType.SolarEruption, "日耀喷发剑", 3473, 105, 2f, 20, ProjKind.SolarEruptionProj, 24f, 10, true, true, false, null, 1f, "“带着太阳之怒猛击”");
            eruption.Channel = true;
            list.Add(eruption);
            // The monk staffs (I:33959-33996, 34105-34124): whirled while held, or thrust; the Fury thrusts on a right click.
            ItemDef Monk(ItemType t, string name, int id, int dmg, float kb, int use, float speed, ProjKind proj, string sound, int rare, bool auto, string tip) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Melee, Damage = dmg, Knockback = kb, UseAnimation = use * Tick, UseTime = use * Tick,
                HoldOut = true, NoMelee = true, NoUseGraphic = true, Channel = true, AutoReuse = auto, Shoot = proj, ShootSpeed = speed, UseSound = sound, Rare = rare, Tooltip = tip,
            };
            list.Add(Monk(ItemType.MonkStaffT1, "瞌睡章鱼", 3835, 50, 7f, 30, 24f, ProjKind.MonkStaffT1Spin, "Custom/dd2_monk_staff_swing_*4", 5, true, "在挥舞着粉碎敌人时能充能"));
            list.Add(Monk(ItemType.MonkStaffT2, "恐怖关刀", 3836, 45, 7f, 27, 42f, ProjKind.MonkStaffT2, "Custom/dd2_ghastly_glaive_pierce_*", 5, false, "在击中敌人时召唤鬼魂"));
            var fury = Monk(ItemType.MonkStaffT3, "天龙之怒", 3858, 140, 5f, 30, 24f, ProjKind.MonkStaffT3Spin, "Custom/dd2_sky_dragons_fury_swing_*4", 8, true, "手持时右键可施展另一种攻击！");
            fury.AltUse = true;
            list.Add(fury);
            // Daybreak (I:31872-31890): thrown overhand; it sticks in, burns and blows up.
            list.Add(new ItemDef
            {
                Type = ItemType.DayBreak, Name = "破晓之光", Texture = "Item_3543", Class = DamageClass.Melee, Damage = 150, Knockback = 5f,
                UseAnimation = 16 * Tick, UseTime = 16 * Tick, NoMelee = true, NoUseGraphic = true, Shoot = ProjKind.Daybreak, ShootSpeed = 10f,
                Rare = 10, Tooltip = "“用光明之矛将敌人四分五裂！”",
            });
            list.Add(new ItemDef
            {
                Type = ItemType.TrueCopperShortsword, Name = "真铜短剑", Texture = "Item_5669", Class = DamageClass.Melee, Damage = 190, Knockback = 6.5f, Crit = 10,
                UseAnimation = 30 * Tick, UseTime = 10 * Tick, UseSound = null, NoMelee = true, NoUseGraphic = true, ShootsFinalFractal = true, Rare = 10,
            });
        }
    }
}
