using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>
    /// The damage of the weapons measured in play (suite "classdps", 2026-10-21): every stage's four classes' core weapons
    /// used for six seconds on Dirtmouth's Target Dummy at the median defense of the stage's bosses, Master, in the stage's reference kit.
    /// The damage model (ProgressionBalance.ApplyWeaponBalance) counts a spear's thrust, a sword's beam over its swing, a
    /// yoyo's bees as one hit and gives a summoner's fifth slot nothing; play doesn't. Each class's best weapon of a stage
    /// is set to the stage's figure (85, 115, 190, 340, 620, 1500 a second; a summoner's best minions with its best whip
    /// over them), the others towards it, keeping part of their spread. The values are the damage itself, the last word on
    /// these weapons after the model (PreserveRecipeUpgrades leaves them be), subject to the final timber cap and endgame
    /// native damage floor in WeaponProgression. Weapons that rain from the
    /// sky or stream past a small dummy (the Daedalus Stormbow, the Bee's Knees, the Golden Shower, the Blizzard Staff, the
    /// Aqua Scepter, the Crimson Rod) stay the model's: the dummy can't measure them. Written by scratchpad calib_abs.py
    /// from classdps.json and itemdump.json.
    /// </summary>
    internal static class WeaponCalibration
    {
        // Measured at stage 3 or 4 and since made stage-2 bosses' gear (VanillaBossProgression, 10-22), the model's stage-2
        // damage stands for these until classdps measures them there: the Dark Lance, Cascade, Sunfury and Hellwing Bow (the
        // Failed Champion's), the Flamelash and Flower of Fire (Uumuu's), the Bee Keeper and Bee Gun (the Hu's).
        static readonly Dictionary<ItemType, int> damage = new Dictionary<ItemType, int>
        {
            // The Arc Surge (suite r8): the model counts its three casts a use as ten hits a second; one enemy takes one in ten
            // ticks of them all (usesIDStaticNPCImmunity), about 1.5 a use: about 1000 a second on the dummy at stage 5.
            [ItemType.ArcSurge] = 302, // 电弧涌动：阶段5 法师，伤害 104 时实测 318/秒 → 目标约 1000/秒（大体型首领上更高）
            [ItemType.WoodenSword] = 23, // 木剑：阶段0 战士，伤害 21 时实测 65/秒 → 目标 71/秒
            [ItemType.IronBroadsword] = 23, // 铁阔剑：阶段0 战士，伤害 22 时实测 72/秒 → 目标 75/秒
            [ItemType.EnchantedBoomerang] = 25, // 附魔回旋镖：阶段0 战士，伤害 26 时实测 89/秒 → 目标 85/秒
            [ItemType.Spear] = 15, // 长矛：阶段0 战士，伤害 15 时实测 78/秒 → 目标 79/秒
            [ItemType.WoodenBow] = 23, // 木弓：阶段0 射手，伤害 21 时实测 60/秒 → 目标 67/秒
            [ItemType.Blowpipe] = 18, // 吹管：阶段0 射手，伤害 18 时实测 76/秒 → 目标 77/秒
            [ItemType.IronBow] = 26, // 铁弓：阶段0 射手，伤害 25 时实测 70/秒 → 目标 73/秒
            [ItemType.FlintlockPistol] = 13, // 燧发枪：阶段0 射手，伤害 14 时实测 90/秒 → 目标 85/秒
            [ItemType.WandofSparking] = 30, // 火花魔棒：阶段0 法师，伤害 29 时实测 79/秒 → 目标 82/秒
            [ItemType.AmethystStaff] = 47, // 紫晶法杖：阶段0 法师，伤害 46 时实测 84/秒 → 目标 85/秒
            [ItemType.FinchStaff] = 8, // 雀杖：阶段0 召唤，伤害 8 时实测 15/秒 → 目标 16/秒
            [ItemType.SlimeStaff] = 10, // 史莱姆法杖：阶段0 召唤，伤害 10 时实测 55/秒 → 目标 57/秒
            [ItemType.LeatherWhip] = 10, // 皮鞭：阶段0 召唤，伤害 10 时实测 16/秒 → 目标 17/秒
            [ItemType.AbigailsFlower] = 7, // 阿比盖尔的花：阶段0 召唤，伤害 7 时实测 33/秒 → 目标 35/秒
            [ItemType.BladeofGrass] = 24, // 草剑：阶段1 战士，伤害 22 时实测 98/秒 → 目标 111/秒
            [ItemType.ThornChakram] = 24, // 荆棘旋刃：阶段1 战士，伤害 22 时实测 97/秒 → 目标 111/秒
            [ItemType.Starfury] = 20, // 星怒：阶段1 战士，伤害 18 时实测 104/秒 → 目标 115/秒
            [ItemType.EnchantedSword] = 26, // 附魔剑：阶段1 战士，伤害 23 时实测 99/秒 → 目标 112/秒
            [ItemType.GoldBow] = 29, // 金弓：阶段1 射手，伤害 27 时实测 78/秒 → 目标 85/秒
            [ItemType.Musket] = 36, // 火枪：阶段1 射手，伤害 40 时实测 129/秒 → 目标 115/秒
            [ItemType.Boomstick] = 13, // 三发猎枪：阶段1 射手，伤害 14 时实测 111/秒 → 目标 105/秒
            [ItemType.DemonBow] = 30, // 恶魔弓：阶段1 射手，伤害 31 时实测 109/秒 → 目标 104/秒
            [ItemType.TopazStaff] = 56, // 黄玉法杖：阶段1 法师，伤害 47 时实测 81/秒 → 目标 98/秒
            [ItemType.SapphireStaff] = 48, // 蓝玉法杖：阶段1 法师，伤害 42 时实测 90/秒 → 目标 104/秒
            [ItemType.RubyStaff] = 44, // 红玉法杖：阶段1 法师，伤害 39 时实测 93/秒 → 目标 107/秒
            [ItemType.DiamondStaff] = 41, // 钻石法杖：阶段1 法师，伤害 38 时实测 105/秒 → 目标 115/秒
            [ItemType.FlinxStaff] = 11, // 小雪怪法杖：阶段1 召唤，伤害 11 时实测 62/秒 → 目标 64/秒
            [ItemType.Snapthorn] = 14, // 荆鞭：阶段1 召唤，伤害 14 时实测 29/秒 → 目标 30/秒
            [ItemType.VampireFrogStaff] = 12, // 吸血鬼青蛙法杖：阶段1 召唤，伤害 12 时实测 68/秒 → 目标 71/秒
            [ItemType.CrimsonWhip] = 19, // 脉鞭：阶段1 召唤，伤害 18 时实测 29/秒 → 目标 30/秒
            [ItemType.Muramasa] = 30, // 村正：阶段2 战士，伤害 30 时实测 170/秒 → 目标 169/秒
            [ItemType.Valor] = 28, // 英勇球：阶段2 战士，伤害 30 时实测 208/秒 → 目标 190/秒
            [ItemType.BlueMoon] = 32, // 蓝月：阶段2 战士，伤害 33 时实测 181/秒 → 目标 175/秒
            [ItemType.FieryGreatsword] = 47, // 火山：阶段2 战士，伤害 48 时实测 178/秒 → 目标 173/秒
            [ItemType.Handgun] = 31, // 手枪：阶段2 射手，伤害 29 时实测 166/秒 → 目标 177/秒
            [ItemType.MoltenFury] = 41, // 熔火之怒：阶段2 射手，伤害 41 时实测 188/秒 → 目标 190/秒
            [ItemType.PhoenixBlaster] = 32, // 凤凰爆破枪：阶段2 射手，伤害 31 时实测 177/秒 → 目标 183/秒
            [ItemType.Minishark] = 14, // 迷你鲨：阶段2 射手，伤害 13 时实测 147/秒 → 目标 164/秒
            [ItemType.SpaceGun] = 42, // 太空枪：阶段2 法师，伤害 44 时实测 202/秒 → 目标 190/秒
            [ItemType.BookofSkulls] = 30, // 骷髅头法书：阶段2 法师，伤害 32 时实测 202/秒 → 目标 190/秒
            [ItemType.DemonScythe] = 59, // 恶魔之镰：阶段2 法师，伤害 62 时实测 197/秒 → 目标 187/秒
            [ItemType.ImpStaff] = 27, // 小鬼法杖：阶段2 召唤，伤害 31 时实测 107/秒 → 目标 90/秒
            [ItemType.SpinalTap] = 29, // 脊柱骨鞭：阶段2 召唤，伤害 33 时实测 97/秒 → 目标 82/秒
            [ItemType.CorruptWhip] = 17, // 魂笞：阶段2 召唤，伤害 19 时实测 46/秒 → 目标 39/秒
            [ItemType.NightsEdge] = 42, // 永夜刃：阶段3 战士，伤害 42 时实测 342/秒 → 目标 340/秒
            [ItemType.ClockworkAssaultRifle] = 19, // 发条式突击步枪：阶段3 射手，伤害 18 时实测 318/秒 → 目标 340/秒
            [ItemType.Shotgun] = 29, // 霰弹枪：阶段3 射手，伤害 27 时实测 316/秒 → 目标 338/秒
            [ItemType.Harpoon] = 100, // 鱼叉枪：阶段3 射手，伤害 86 时实测 238/秒 → 目标 286/秒
            [ItemType.LaserRifle] = 54, // 激光步枪：阶段3 法师，伤害 53 时实测 303/秒 → 目标 310/秒
            [ItemType.MedusaHead] = 61, // 蛇发女妖头：阶段3 法师，伤害 63 时实测 354/秒 → 目标 340/秒
            [ItemType.SpiderStaff] = 15, // 蜘蛛法杖：阶段3 召唤，伤害 15 时实测 201/秒 → 目标 211/秒
            [ItemType.Firecracker] = 26, // 鞭炮：阶段3 召唤，伤害 25 时实测 72/秒 → 目标 76/秒
            [ItemType.HiveFive] = 28, // 蜂巢球：阶段4 战士，伤害 26 时实测 452/秒 → 目标 504/秒
            [ItemType.Excalibur] = 71, // 断钢剑：阶段4 战士，伤害 73 时实测 638/秒 → 目标 620/秒
            [ItemType.Amarok] = 43, // 冰雪悠悠球：阶段4 战士，伤害 43 时实测 581/秒 → 目标 586/秒
            [ItemType.HallowedRepeater] = 79, // 神圣连弩：阶段4 射手，伤害 80 时实测 633/秒 → 目标 620/秒
            [ItemType.Megashark] = 35, // 巨兽鲨：阶段4 射手，伤害 34 时实测 534/秒 → 目标 560/秒
            [ItemType.CrystalStorm] = 49, // 水晶风暴：阶段4 法师，伤害 51 时实测 657/秒 → 目标 620/秒
            [ItemType.SkyFracture] = 56, // 裂天剑：阶段4 法师，伤害 58 时实测 612/秒 → 目标 594/秒
            [ItemType.OpticStaff] = 32, // 魔眼法杖：阶段4 召唤，伤害 33 时实测 351/秒 → 目标 332/秒
            [ItemType.Durendal] = 56, // 迪朗达尔：阶段4 召唤，伤害 58 时实测 193/秒 → 目标 183/秒
            [ItemType.SanguineStaff] = 39, // 血红法杖：阶段4 召唤，伤害 41 时实测 186/秒 → 目标 176/秒
            [ItemType.CoolWhip] = 41, // 冷鞭：阶段4 召唤，伤害 42 时实测 158/秒 → 目标 150/秒
            [ItemType.TerraBlade] = 76, // 泰拉刃：阶段5 战士，伤害 77 时实测 1524/秒 → 目标 1500/秒
            [ItemType.PossessedHatchet] = 109, // 疯狂飞斧：阶段5 战士，伤害 102 时实测 1214/秒 → 目标 1308/秒
            [ItemType.FlaironFlail] = 46, // 猪鲨链球：阶段5 战士，伤害 42 时实测 1091/秒 → 目标 1227/秒
            [ItemType.NorthPole] = 161, // 北极：阶段5 战士，伤害 134 时实测 862/秒 → 目标 1065/秒
            [ItemType.Tsunami] = 40, // 海啸：阶段5 射手，伤害 39 时实测 1420/秒 → 目标 1446/秒
            [ItemType.TacticalShotgun] = 58, // 战术霰弹枪：阶段5 射手，伤害 54 时实测 1219/秒 → 目标 1320/秒
            [ItemType.ChainGun] = 43, // 链式机枪：阶段5 射手，伤害 40 时实测 1217/秒 → 目标 1318/秒
            [ItemType.AerialBane] = 38, // 空中祸害：阶段5 射手，伤害 38 时实测 1509/秒 → 目标 1500/秒
            [ItemType.RazorbladeTyphoon] = 88, // 利刃台风：阶段5 法师，伤害 83 时实测 1274/秒 → 目标 1364/秒
            [ItemType.Razorpine] = 38, // 剃刀松：阶段5 法师，伤害 36 时实测 1202/秒 → 目标 1317/秒
            [ItemType.NebulaBlaze] = 81, // 星云烈焰：阶段5 法师，伤害 81 时实测 1493/秒 → 目标 1500/秒
            [ItemType.RavenStaff] = 52, // 乌鸦法杖：阶段5 召唤，伤害 53 时实测 717/秒 → 目标 706/秒
            [ItemType.XenoStaff] = 41, // 外星法杖：阶段5 召唤，伤害 41 时实测 823/秒 → 目标 811/秒
            [ItemType.DarkHarvest] = 95, // 暗黑收割：阶段5 召唤，伤害 96 时实测 527/秒 → 目标 519/秒
            [ItemType.Kaleidoscope] = 117, // 万花筒：阶段5 召唤，伤害 119 时实测 463/秒 → 目标 456/秒
            [ItemType.WandofFrosting] = 32, // 结霜魔棒：阶段1 法师，伤害 25 时实测 76/秒 → 目标 103/秒
            [ItemType.ThunderStaff] = 32, // 霹雳法杖：阶段1 法师，伤害 26 时实测 91/秒 → 目标 115/秒
        };

        /// <summary>Whether a weapon's damage comes from play rather than the model.</summary>
        public static bool Measured(ItemType t) => damage.ContainsKey(t);

        /// <summary>After the model's passes: the measured weapons' damage.</summary>
        public static void Apply(ItemDef[] items)
        {
            foreach (var kv in damage)
            {
                var d = items[(int)kv.Key];
                if (d == null || d.Placeholder || !d.IsWeapon) continue;
                d.Damage = kv.Value;
            }
        }
    }
}
