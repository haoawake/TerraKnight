namespace TerraKnight.Items
{
    /// <summary>
    /// The rest of the bows, and the hammers, axes, hamaxes and pickaxes (sweep R1), with
    /// Terraria's own numbers (Item.SetDefaults). In Hallownest hammers and axes have no walls or
    /// trees to work on: they swing as the weapons they also are; their power shows on the tooltip.
    /// </summary>
    internal static partial class ItemDb
    {
        static void AddTools(System.Collections.Generic.List<ItemDef> list)
        {
            // --- Bows (useStyle 5, wooden arrows, Item_5): the woods' and the ores' (I:9030-9132, 10691-10705, 12051-12062, 24445-24456, 26239-26250, 31384-31678, 41905-41922).
            list.Add(Bow(ItemType.CopperBow, "铜弓", 3504, 6, 0f, 29, 6.6f, price: 0, tier: 0));
            list.Add(Bow(ItemType.TinBow, "锡弓", 3498, 7, 0f, 28, 6.6f, price: 0, tier: 0));
            list.Add(Bow(ItemType.LeadBow, "铅弓", 3492, 9, 0f, 27, 6.6f, price: 0, tier: 0));
            list.Add(Bow(ItemType.SilverBow, "银弓", 3510, 9, 0f, 27, 6.6f, price: 0, tier: 0));
            list.Add(Bow(ItemType.TungstenBow, "钨弓", 3486, 10, 0f, 26, 6.6f, price: 0, tier: 0));
            list.Add(Bow(ItemType.PlatinumBow, "铂金弓", 3480, 13, 0f, 25, 6.6f, price: 0, tier: 0));
            list.Add(Bow(ItemType.EbonwoodBow, "乌木弓", 655, 8, 0f, 28, 6.6f, price: 0, tier: 0));
            list.Add(Bow(ItemType.ShadewoodBow, "暗影木弓", 923, 8, 0f, 28, 6.6f, price: 0, tier: 0));
            list.Add(Bow(ItemType.RichMahoganyBow, "红木弓", 658, 6, 0f, 29, 6.6f, price: 0, tier: 0));
            list.Add(Bow(ItemType.PalmWoodBow, "棕榈木弓", 2515, 6, 0f, 29, 6.6f, price: 0, tier: 0));
            list.Add(Bow(ItemType.BorealWoodBow, "针叶木弓", 2747, 6, 0f, 29, 6.6f, price: 0, tier: 0));
            list.Add(Bow(ItemType.AshWoodBow, "灰烬木弓", 5282, 6, 0f, 29, 6.6f, price: 0, tier: 0));
            list.Add(Bow(ItemType.PearlwoodBow, "珍珠木弓", 661, 12, 0f, 20, 7f, price: 0, tier: 0));
            list.Add(Bow(ItemType.TendonBow, "肌腱弓", 796, 19, 1f, 30, 6.7f, price: 0, tier: 0, rare: 1));

            // --- Hammers, axes and hamaxes (useStyle 1, autoReuse, useTurn): swung for useAnimation, a tool's stroke every useTime.
            ItemDef Tool(ItemType t, string name, int id, int dmg, float kb, int anim, int use, float scale = 1f, int rare = 0, int hammer = 0, int axe = 0, int pick = 0,
                int boost = 0, string glow = null, string tip = null) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Melee, Damage = dmg, Knockback = kb, UseAnimation = anim * Tick, UseTime = use * Tick,
                Scale = scale, Rare = rare, HammerPower = hammer, AxePower = axe, PickPower = pick, TileBoost = boost, Glow = glow, Tooltip = tip,
            };
            // I:3794-3807, 9014-9117, 12035-12047, 24429-24440, 26223-26235, 31304-31660, 41926-41938.
            list.Add(Tool(ItemType.WoodenHammer, "木锤", 196, 2, 5.5f, 37, 25, hammer: 25, boost: -1));
            list.Add(Tool(ItemType.CopperHammer, "铜锤", 3505, 4, 5.5f, 33, 23, hammer: 35, boost: -1));
            list.Add(Tool(ItemType.TinHammer, "锡锤", 3499, 6, 5.5f, 31, 21, hammer: 38));
            list.Add(Tool(ItemType.IronHammer, "铁锤", 7, 7, 5.5f, 30, 20, hammer: 40));
            list.Add(Tool(ItemType.LeadHammer, "铅锤", 3493, 8, 5.5f, 29, 19, hammer: 43));
            list.Add(Tool(ItemType.SilverHammer, "银锤", 3511, 9, 5.5f, 29, 19, hammer: 45));
            list.Add(Tool(ItemType.TungstenHammer, "钨锤", 3487, 9, 5.5f, 28, 25, hammer: 50));
            list.Add(Tool(ItemType.GoldHammer, "金锤", 3517, 9, 5.5f, 28, 23, hammer: 55));
            list.Add(Tool(ItemType.PlatinumHammer, "铂金锤", 3481, 10, 5.5f, 27, 21, hammer: 59));
            list.Add(Tool(ItemType.EbonwoodHammer, "乌木锤", 654, 7, 5.5f, 30, 20, hammer: 40));
            list.Add(Tool(ItemType.ShadewoodHammer, "暗影木锤", 922, 7, 5.5f, 30, 20, hammer: 40));
            list.Add(Tool(ItemType.RichMahoganyHammer, "红木锤", 657, 4, 5.5f, 33, 23, hammer: 35));
            list.Add(Tool(ItemType.PalmWoodHammer, "棕榈木锤", 2516, 4, 5.5f, 33, 23, hammer: 35));
            list.Add(Tool(ItemType.BorealWoodHammer, "针叶木锤", 2746, 4, 5.5f, 33, 23, hammer: 35));
            list.Add(Tool(ItemType.AshWoodHammer, "灰烬木锤", 5283, 4, 5.5f, 30, 20, hammer: 35));
            list.Add(Tool(ItemType.PearlwoodHammer, "珍珠木锤", 660, 10, 5.5f, 29, 19, hammer: 55));
            list.Add(Tool(ItemType.TheBreaker, "魔锤", 104, 24, 6f, 45, 19, rare: 1, hammer: 55));
            list.Add(Tool(ItemType.FleshGrinder, "血肉锤", 797, 23, 6f, 40, 19, rare: 1, hammer: 55));
            list.Add(Tool(ItemType.Pwnhammer, "神锤", 367, 26, 7.5f, 27, 14, rare: 4, hammer: 80, tip: "强大到足以摧毁恶魔祭坛"));
            list.Add(Tool(ItemType.Hammush, "蘑菇锤", 787, 26, 7.5f, 27, 14, 1.1f, rare: 7, hammer: 85, tip: "强大到足以摧毁恶魔祭坛"));
            list.Add(Tool(ItemType.ChlorophyteWarhammer, "叶绿战锤", 1234, 80, 8f, 35, 14, rare: 7, hammer: 90));
            // Axes (I:1914-1926, 10742-10754, 12788-12836, 15058-15106, 15254-15268, 16131-16144, 31314-31670, 42128-42137).
            list.Add(Tool(ItemType.CopperAxe, "铜斧", 3506, 3, 4.5f, 30, 21, axe: 7, boost: -1));
            list.Add(Tool(ItemType.TinAxe, "锡斧", 3500, 4, 4.5f, 28, 20, axe: 8));
            list.Add(Tool(ItemType.IronAxe, "铁斧", 10, 5, 4.5f, 27, 19, axe: 9));
            list.Add(Tool(ItemType.LeadAxe, "铅斧", 3494, 6, 4.5f, 28, 19, axe: 10));
            list.Add(Tool(ItemType.SilverAxe, "银斧", 3512, 6, 4.5f, 26, 18, axe: 10));
            list.Add(Tool(ItemType.TungstenAxe, "钨斧", 3488, 7, 4.5f, 26, 18, axe: 11));
            list.Add(Tool(ItemType.GoldAxe, "金斧", 3518, 7, 4.5f, 26, 18, axe: 11));
            list.Add(Tool(ItemType.PlatinumAxe, "铂金斧", 3482, 8, 4.5f, 25, 17, axe: 12));
            list.Add(Tool(ItemType.WarAxeoftheNight, "暗夜战斧", 45, 20, 6f, 30, 15, rare: 1, axe: 15));
            list.Add(Tool(ItemType.BloodLustCluster, "嗜血狂斧", 799, 22, 6f, 32, 15, rare: 1, axe: 15));
            list.Add(Tool(ItemType.CobaltWaraxe, "钴战斧", 991, 33, 5f, 35, 13, 1.1f, rare: 4, axe: 14));
            list.Add(Tool(ItemType.PalladiumWaraxe, "钯金战斧", 1222, 36, 5.5f, 35, 12, 1.1f, rare: 4, axe: 15));
            list.Add(Tool(ItemType.MythrilWaraxe, "秘银战斧", 992, 39, 6f, 35, 10, 1.1f, rare: 4, axe: 17));
            list.Add(Tool(ItemType.OrichalcumWaraxe, "山铜战斧", 1223, 41, 6.5f, 35, 9, 1.1f, rare: 4, axe: 18));
            list.Add(Tool(ItemType.AdamantiteWaraxe, "精金战斧", 993, 43, 7f, 35, 8, 1.1f, rare: 4, axe: 20));
            list.Add(Tool(ItemType.TitaniumWaraxe, "钛金战斧", 1224, 44, 7.5f, 35, 7, 1.1f, rare: 4, axe: 21));
            list.Add(Tool(ItemType.ChlorophyteGreataxe, "叶绿巨斧", 1233, 70, 7f, 30, 7, 1.15f, rare: 7, axe: 23));
            list.Add(Tool(ItemType.AcornAxe, "再生之斧", 5295, 20, 5f, 24, 12, axe: 25, tip: "让泥土上长出草\n用它进行采集可提高炼金植物的采集量\n砍树时会顺便种下橡实"));
            // The Axe (I:16131-16144): a guitar as well; a click strums a note by how far off the cursor is (Pl:46946-46971).
            var guitar = Tool(ItemType.TheAxe, "吉他斧", 1305, 72, 7.25f, 23, 7, 1.15f, rare: 8, hammer: 100, axe: 35, boost: 1);
            guitar.UseSound = null;
            list.Add(guitar);
            // Hamaxes (I:3942-3956, 4073-4087, 18414-18429, 31236-31266).
            list.Add(Tool(ItemType.MeteorHamaxe, "流星锤斧", 204, 20, 7f, 30, 16, rare: 1, hammer: 60, axe: 20));
            list.Add(Tool(ItemType.MoltenHamaxe, "熔岩锤斧", 217, 20, 7f, 27, 14, rare: 3, hammer: 70, axe: 30));
            list.Add(Tool(ItemType.SpectreHamaxe, "幽灵锤斧", 1507, 60, 7f, 28, 8, 1.05f, rare: 8, hammer: 90, axe: 30));
            list.Add(Tool(ItemType.LunarHamaxeSolar, "耀斑锤斧", 3522, 60, 7f, 28, 7, rare: 10, hammer: 100, axe: 30, boost: 4));
            list.Add(Tool(ItemType.LunarHamaxeVortex, "星旋锤斧", 3523, 60, 7f, 28, 7, rare: 10, hammer: 100, axe: 30, boost: 4, glow: "Glow_196"));
            list.Add(Tool(ItemType.LunarHamaxeNebula, "星云锤斧", 3524, 60, 7f, 28, 7, rare: 10, hammer: 100, axe: 30, boost: 4, glow: "Glow_197"));
            list.Add(Tool(ItemType.LunarHamaxeStardust, "星尘锤斧", 3525, 60, 7f, 28, 7, rare: 10, hammer: 100, axe: 30, boost: 4, glow: "Glow_198"));
            // The lunar fragment pickaxes (I:26529-26546, 26567-26625, 31078-31082): the Vortex one's numbers for all four, their own glowmasks.
            // (Their axes and hammers, 2772-2785 and 3465, were taken out of the game in favour of the hamaxes: unobtainable.)
            list.Add(Tool(ItemType.VortexPickaxe, "星旋镐", 2776, 80, 5.5f, 12, 6, rare: 10, pick: 225, boost: 4, glow: "Glow_5"));
            list.Add(Tool(ItemType.NebulaPickaxe, "星云镐", 2781, 80, 5.5f, 12, 6, rare: 10, pick: 225, boost: 4, glow: "Glow_10"));
            list.Add(Tool(ItemType.SolarFlarePickaxe, "耀斑镐", 2786, 80, 5.5f, 12, 6, rare: 10, pick: 225, boost: 4));
            list.Add(Tool(ItemType.StardustPickaxe, "星尘镐", 3466, 80, 5.5f, 12, 6, rare: 10, pick: 225, boost: 4, glow: "Glow_178"));

            // --- Pickaxes (I:1393-1403, 10486-10738, 11643-11654, 12770-12785, 14615-14749, 15196-15210, 16355-16368, 18396-18410, 20816-20827, 22197-22210, 23518-23531, 31324-31629).
            list.Add(Tool(ItemType.TinPickaxe, "锡镐", 3503, 5, 2f, 21, 14, 0.95f, pick: 35));
            list.Add(Tool(ItemType.CactusPickaxe, "仙人掌镐", 882, 4, 2f, 25, 16, pick: 35));
            list.Add(Tool(ItemType.LeadPickaxe, "铅镐", 3497, 6, 2f, 19, 12, 1.025f, pick: 43));
            list.Add(Tool(ItemType.SilverPickaxe, "银镐", 3515, 6, 2f, 19, 11, 1.05f, pick: 45));
            list.Add(Tool(ItemType.TungstenPickaxe, "钨镐", 3491, 6, 2f, 21, 19, 1.05f, pick: 50, tip: "可开采陨石"));
            list.Add(Tool(ItemType.PlatinumPickaxe, "铂金镐", 3485, 7, 2f, 19, 15, 1.05f, pick: 59, tip: "可开采陨石"));
            list.Add(Tool(ItemType.BonePickaxe, "骨镐", 1320, 8, 3f, 19, 11, rare: 1, pick: 55, tip: "“我得跟你挑个刺”"));
            list.Add(Tool(ItemType.CandyCanePickaxe, "糖棒镐", 1917, 7, 2.5f, 20, 16, pick: 55, tip: "可开采陨石"));
            list.Add(Tool(ItemType.ReaverShark, "掠夺鲨", 2341, 16, 3f, 22, 13, 1.15f, rare: 3, pick: 59));
            list.Add(Tool(ItemType.DeathbringerPickaxe, "死亡使者镐", 798, 12, 3.5f, 22, 14, rare: 1, pick: 70, tip: "能够开采狱石"));
            list.Add(Tool(ItemType.PalladiumPickaxe, "钯金镐", 1188, 12, 5f, 25, 12, rare: 4, pick: 130, tip: "可开采秘银和山铜"));
            list.Add(Tool(ItemType.OrichalcumPickaxe, "山铜镐", 1195, 17, 5f, 25, 9, rare: 4, pick: 165, tip: "可开采精金和钛金"));
            list.Add(Tool(ItemType.AdamantitePickaxe, "精金镐", 778, 20, 5f, 25, 8, rare: 4, pick: 180));
            list.Add(Tool(ItemType.PickaxeAxe, "镐斧", 990, 35, 4.75f, 25, 7, 1.1f, rare: 4, axe: 22, pick: 200, tip: "“不要与锤钻混淆”"));
            list.Add(Tool(ItemType.ChlorophytePickaxe, "叶绿镐", 1230, 40, 5f, 25, 7, rare: 7, pick: 200));
            list.Add(Tool(ItemType.SpectrePickaxe, "幽灵镐", 1506, 32, 5.25f, 24, 8, rare: 8, pick: 200));
            list.Add(Tool(ItemType.ShroomiteDiggingClaw, "蘑菇矿挖爪", 2176, 45, 6f, 12, 4, rare: 8, axe: 25, pick: 200));

            // --- Drills and chainsaws (sweep R2; useStyle 5, channel, Item_23): held out, the head buzzing at the cursor (TerraProjectilesDrills.cs).
            ItemDef Saw(ItemType t, string name, int id, int dmg, float kb, int use, ProjKind proj, float speed, int rare, int pick = 0, int axe = 0, int hammer = 0, int boost = 0,
                string tip = null, string sound = "Item_23") => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Melee, Damage = dmg, Knockback = kb, UseAnimation = 25 * Tick, UseTime = use * Tick,
                HoldOut = true, NoMelee = true, NoUseGraphic = true, Channel = true, Shoot = proj, ShootSpeed = speed, Rare = rare, PickPower = pick, AxePower = axe,
                HammerPower = hammer, TileBoost = boost, Tooltip = tip, UseSound = sound,
            };
            // I:5833-5944, 8138-8155, 14632-14907, 15214-15250, 15656-15672, 23535-23551, 26509-26525, 26580, 26607, 26728-26744, 28651-28667, 31070.
            list.Add(Saw(ItemType.CobaltChainsaw, "钴链锯", 383, 23, 2.75f, 13, ProjKind.CobaltChainsawProj, 40f, 4, axe: 14));
            list.Add(Saw(ItemType.PalladiumChainsaw, "钯金链锯", 1190, 26, 2.9f, 12, ProjKind.PalladiumChainsawProj, 40f, 4, axe: 15));
            list.Add(Saw(ItemType.MythrilChainsaw, "秘银链锯", 384, 29, 3f, 10, ProjKind.MythrilChainsawProj, 40f, 4, axe: 17));
            list.Add(Saw(ItemType.OrichalcumChainsaw, "山铜链锯", 1197, 31, 3.75f, 9, ProjKind.OrichalcumChainsawProj, 40f, 4, axe: 18));
            list.Add(Saw(ItemType.AdamantiteChainsaw, "精金链锯", 387, 33, 4.5f, 8, ProjKind.AdamantiteChainsawProj, 40f, 4, axe: 20));
            list.Add(Saw(ItemType.TitaniumChainsaw, "钛金链锯", 1204, 34, 4.6f, 7, ProjKind.TitaniumChainsawProj, 40f, 4, axe: 21));
            list.Add(Saw(ItemType.ChlorophyteChainsaw, "叶绿链锯", 1232, 50, 4.6f, 7, ProjKind.ChlorophyteChainsawProj, 46f, 7, axe: 23));
            list.Add(Saw(ItemType.SawtoothShark, "锯齿鲨", 2342, 13, 2.25f, 8, ProjKind.SawtoothSharkProj, 48f, 3, axe: 14));
            list.Add(Saw(ItemType.ButchersChainsaw, "屠夫链锯", 3098, 120, 8f, 8, ProjKind.ButchersChainsawProj, 48f, 8, axe: 30, tip: "被击中的敌人发出火花"));
            list.Add(Saw(ItemType.CobaltDrill, "钴钻头", 385, 10, 0.5f, 13, ProjKind.CobaltDrillProj, 32f, 4, pick: 110, tip: "可开采秘银和山铜"));
            list.Add(Saw(ItemType.PalladiumDrill, "钯金钻头", 1189, 12, 0.5f, 12, ProjKind.PalladiumDrillProj, 32f, 4, pick: 130, tip: "可开采秘银和山铜"));
            list.Add(Saw(ItemType.MythrilDrill, "秘银钻头", 386, 15, 0.5f, 10, ProjKind.MythrilDrillProj, 32f, 4, pick: 150, tip: "可开采精金和钛金"));
            list.Add(Saw(ItemType.OrichalcumDrill, "山铜钻头", 1196, 17, 0.5f, 9, ProjKind.OrichalcumDrillProj, 32f, 4, pick: 165, tip: "可开采精金和钛金"));
            list.Add(Saw(ItemType.AdamantiteDrill, "精金钻头", 388, 20, 0.5f, 8, ProjKind.AdamantiteDrillProj, 32f, 4, pick: 180));
            list.Add(Saw(ItemType.TitaniumDrill, "钛金钻头", 1203, 27, 0.5f, 7, ProjKind.TitaniumDrillProj, 32f, 4, pick: 190));
            list.Add(Saw(ItemType.Drax, "斧钻", 579, 35, 4.75f, 7, ProjKind.DraxProj, 36f, 4, pick: 200, axe: 22, tip: "“不要跟锯刃镐混淆”"));
            list.Add(Saw(ItemType.ChlorophyteDrill, "叶绿钻头", 1231, 35, 1f, 7, ProjKind.ChlorophyteDrillProj, 40f, 7, pick: 200));
            list.Add(Saw(ItemType.ChlorophyteJackhammer, "叶绿手提钻", 1262, 45, 5.2f, 7, ProjKind.ChlorophyteJackhammerProj, 46f, 7, hammer: 90));
            list.Add(Saw(ItemType.VortexDrill, "星旋钻头", 2774, 50, 0.5f, 4, ProjKind.VortexDrillProj, 32f, 10, pick: 225));
            list.Add(Saw(ItemType.NebulaDrill, "星云钻头", 2779, 50, 0.5f, 4, ProjKind.NebulaDrillProj, 32f, 10, pick: 225));
            list.Add(Saw(ItemType.SolarFlareDrill, "耀斑钻头", 2784, 50, 0.5f, 4, ProjKind.SolarFlareDrillProj, 32f, 10, pick: 225));
            list.Add(Saw(ItemType.StardustDrill, "星尘钻头", 3464, 50, 0.5f, 4, ProjKind.StardustDrillProj, 32f, 10, pick: 225));
            // --- The thrown consumables (sweep R3; I:3314-3328, 4785-4800, 6287-6318, 7543-7568, 13902-13915, 20035-20048, 20756-20768,
            // 25374-25442, 28616-28629, 28868-28881, 29638-29652, 30878-30913, 31202-31216, 32007-32021, 43882-43895): ranged, used up (the Magic Dagger magic, kept).
            ItemDef Toss(ItemType t, string name, int id, int dmg, float kb, int use, ProjKind proj, float speed, int rare = 0, bool auto = false, int crit = 0, bool held = false,
                string tip = null) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, Class = DamageClass.Ranged, Damage = dmg, Knockback = kb, Crit = crit, UseAnimation = use * Tick, UseTime = use * Tick,
                NoMelee = true, NoUseGraphic = true, HoldOut = held, AutoReuse = auto, Shoot = proj, ShootSpeed = speed, Rare = rare, Consumable = true, MaxStack = 9999, Tooltip = tip,
            };
            list.Add(Toss(ItemType.SpikyBall, "尖球", 161, 16, 1f, 15, ProjKind.SpikyBallProj, 5f, auto: true));
            list.Add(Toss(ItemType.PoisonedKnife, "毒刀", 287, 14, 2.4f, 15, ProjKind.PoisonedKnifeProj, 12f, auto: true, crit: 4));
            list.Add(Toss(ItemType.HolyWater, "圣水", 422, 20, 3f, 15, ProjKind.HolyWaterProj, 9f, 3, tip: "让神圣之地蔓延到一些物块上"));
            list.Add(Toss(ItemType.UnholyWater, "邪水", 423, 20, 3f, 15, ProjKind.UnholyWaterProj, 9f, 3, tip: "让腐化之地蔓延到一些物块上"));
            list.Add(Toss(ItemType.BloodWater, "血水", 3477, 20, 3f, 15, ProjKind.BloodWaterProj, 9f, 3, tip: "让猩红之地蔓延到一些物块上"));
            list.Add(Toss(ItemType.Beenade, "蜜蜂手榴弹", 1130, 12, 1f, 15, ProjKind.BeenadeProj, 6f, tip: "爆炸成一大群蜜蜂"));
            list.Add(Toss(ItemType.StickyGrenade, "粘性手榴弹", 2586, 60, 8f, 45, ProjKind.StickyGrenadeProj, 5.5f, held: true, tip: "不会摧毁图格的小爆炸\n会粘在物体上"));
            list.Add(Toss(ItemType.BouncyGrenade, "弹力手榴弹", 3116, 65, 8f, 40, ProjKind.BouncyGrenadeProj, 6.5f, held: true, tip: "造成不会摧毁图格的小爆炸\n非常有弹性"));
            list.Add(Toss(ItemType.PartyGirlGrenade, "快乐手榴弹", 3548, 30, 6f, 20, ProjKind.PartyGrenadeProj, 6f, 2, held: true, tip: "造成不会摧毁图格的小爆炸"));
            list.Add(Toss(ItemType.MolotovCocktail, "莫洛托夫鸡尾酒", 2590, 23, 7f, 40, ProjKind.MolotovProj, 6.5f, 1, held: true, tip: "让敌人着火的小爆炸\n火烧附近区域一会儿"));
            list.Add(Toss(ItemType.RottenEgg, "臭蛋", 1809, 13, 6.5f, 19, ProjKind.RottenEggProj, 9f, tip: "最适合用来整蛊城镇居民"));
            list.Add(Toss(ItemType.StarAnise, "星形茴香", 1913, 14, 0f, 15, ProjKind.StarAniseProj, 12f));
            list.Add(Toss(ItemType.Javelin, "标枪", 3094, 17, 4.75f, 24, ProjKind.JavelinProj, 11.5f));
            list.Add(Toss(ItemType.BoneJavelin, "骨头标枪", 3378, 20, 5f, 25, ProjKind.BoneJavelinProj, 10f, 1, auto: true));
            list.Add(Toss(ItemType.BoneDagger, "骨投刀", 3379, 14, 1.5f, 14, ProjKind.BoneDaggerProj, 10f, 1, auto: true));
            list.Add(Toss(ItemType.FrostDaggerfish, "寒霜飞鱼", 3197, 17, 3.5f, 13, ProjKind.FrostDaggerfishProj, 12.5f, 1));
            list.Add(Toss(ItemType.MudBall, "泥球", 5572, 8, 5.75f, 10, ProjKind.MudBallProj, 7f, auto: true));
            // I:43928-43942: the Pretty Mirror (1.4.5), thrown to break.
            list.Add(Toss(ItemType.PrettyMirror, "漂亮镜子", 5577, 13, 3f, 25, ProjKind.PrettyMirrorProj, 8f, 1, tip: "“弄坏了就得买，你和你的朋友一个也跑不了”"));
            var dagger = Toss(ItemType.MagicDagger, "魔法飞刀", 517, 14, 3.75f, 15, ProjKind.MagicDaggerProj, 12f, 1, tip: "有魔力的回旋飞刀");
            dagger.Class = DamageClass.Magic;
            dagger.Mana = 5;
            dagger.Consumable = false;
            dagger.MaxStack = 1;
            list.Add(dagger);
            // The Laser Drill (I:26728-26744): no use sound (the beam whines), 11 more tiles of reach.
            list.Add(Saw(ItemType.LaserDrill, "激光钻头", 2798, 35, 4.75f, 6, ProjKind.LaserDrillProj, 36f, 8, pick: 230, boost: 11, sound: null));
        }
    }
}
