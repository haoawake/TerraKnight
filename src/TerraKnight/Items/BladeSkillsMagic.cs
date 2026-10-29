using UnityEngine;
using Offshoot = TerraKnight.Items.TerraProjectiles.Offshoot;
using RangedShot = TerraKnight.Items.TerraProjectiles.RangedShot;
using SpawnWay = TerraKnight.Items.TerraProjectiles.SpawnWay;

namespace TerraKnight.Items
{
    /// <summary>
    /// The remade magic weapons' right button (the user's 10-28 "现在开始做新版本的法师武器吧…绚丽…创意…尽量用原版的贴图，要用额外的
    /// 特效就用粒子效果…越后期的武器我希望越伟大"): an art each, paid in mana (a few of the weapon's casts), with a cooldown. The
    /// weapon's own projectiles (Terraria's own pictures), shaped anew: rings, orbits let go one by one, fans, rain on the
    /// cursor, rows along the ground, converging storms. Each opens its circles (SwordFx.MageArt), grander the later the weapon.
    /// The same stage's reskins share one (the gem staffs).
    /// </summary>
    internal static partial class BladeSkills
    {
        static partial void RegisterMagicArts()
        {
            KindArt Press(ItemType t, string name, float cd, string tip, string tipEn, System.Func<HeroController, ItemDef, bool> press)
            {
                var a = new KindArt { Name = name, Cooldown = cd, Tip = tip, TipEn = tipEn, Press = press };
                RegisterArt(t, a);
                return a;
            }
            KindArt Charge(ItemType t, string name, float full, string tip, string tipEn, System.Action<HeroController, ItemDef, float> release)
            {
                var a = new KindArt { Name = name, Charged = true, Full = full, Aimed = true, ChargeSound = "tk_gun_charge", Tip = tip, TipEn = tipEn, Release = release };
                RegisterArt(t, a);
                return a;
            }
            string Cost(float m) => $"（消耗 {m:0.#} 倍魔力）";
            string CostEn(float m) => $" (costs {m:0.#}× the mana)";

            // ---- stage 0-1
            foreach (var gem in new[] { ItemType.AmethystStaff, ItemType.TopazStaff, ItemType.SapphireStaff, ItemType.EmeraldStaff, ItemType.RubyStaff, ItemType.AmberStaff, ItemType.DiamondStaff })
                Press(gem, "宝石星阵", 5f, "右键：身边环起六颗宝石光，绕身一圈后依次射向鼠标处" + Cost(3f), "Right click: six gem bolts ring you, go round once and fly at the cursor one by one" + CostEn(3f),
                    (h, d) => Pay(d, 3f) && OrbitThen(h, d, d.Shoot, 6, 1.2f, new RangedShot()));
            Press(ItemType.DaybloomStaff, "向日葵绽放", 4f, "右键：花粉向四周绽开一圈，再汇聚到鼠标处" + Cost(3f), "Right click: motes open out round you in a ring, then gather on the cursor" + CostEn(3f),
                (h, d) => Pay(d, 3f) && Ring(h, d, d.Shoot, 12, 4f, 1.2f, () => new RangedShot { Point = Aim(), Seek = 3f, SeekFrom = 18 }));
            Press(ItemType.WandofSparking, "火花喷泉", 4f, "右键：魔棒一挥，朝前上方喷出十颗火花" + Cost(4f), "Right click: a flick throws ten sparks up and ahead" + CostEn(4f),
                (h, d) => Pay(d, 4f) && FanUp(h, d, ProjKind.Spark, 10, 40f, 15f, 1.2f, 0.9f, () => new RangedShot { Debuff = EnemyDebuffs.OnFire, DebuffTicks = 180 }));
            Press(ItemType.WandofFrosting, "霜花喷泉", 4f, "右键：魔棒一挥，朝前上方喷出十颗霜屑，冻伤敌人" + Cost(4f), "Right click: a flick throws ten frost sparks up and ahead, frostburning" + CostEn(4f),
                (h, d) => Pay(d, 4f) && FanUp(h, d, ProjKind.FrostSpark, 10, 40f, 15f, 1.2f, 0.9f, () => new RangedShot { Debuff = EnemyDebuffs.Frostburn, DebuffTicks = 180 }));
            Press(ItemType.ThunderStaff, "落雷", 5f, "右键：鼠标处接连落下三道雷电，各自跳向附近的敌人" + Cost(4f), "Right click: three bolts come down on the cursor one after another, each arcing on" + CostEn(4f),
                (h, d) => Pay(d, 4f) && Sky(h, d, d.Shoot, 3, 0.35f, 2f, 1f, () => new RangedShot { Chain = 1, ChainDamage = 0.35f, Size = 1.3f, Trail = 1f, TrailDust = 226 }));
            foreach (var zap in new[] { ItemType.ZapinatorGray, ItemType.ZapinatorOrange })
                Press(zap, "过载冲击", 5f, "右键：一次扇形射出五道激光，每道带一种随机效果" + Cost(3f), "Right click: five lasers at once in a fan, each with a change of its own at random" + CostEn(3f),
                    (h, d) => Pay(d, 3f) && Fan(h, d, d.Shoot, 5, 10f, 1f, 1.2f, ChaosShot));
            // ---- stage 2
            Press(ItemType.CrimsonRod, "血雨倾泻", 6f, "右键：鼠标处倾泻一阵血雨，敌人流血" + Cost(2f), "Right click: a downpour of blood on the cursor that makes enemies bleed" + CostEn(2f),
                (h, d) => Pay(d, 2f) && Sky(h, d, ProjKind.BloodRain, 16, 1.2f, 1.2f, 3f, () => new RangedShot { Debuff = EnemyDebuffs.Bleeding, DebuffTicks = 300 }));
            Press(ItemType.GlacierFang, "冰牙环", 5f, "右键：身边迸出一圈冰晶，冻伤敌人" + Cost(3f), "Right click: a ring of ice bolts bursts out round you, frostburning" + CostEn(3f),
                (h, d) => Pay(d, 3f) && Ring(h, d, ProjKind.IceBolt, 10, 9f, 1.2f, () => new RangedShot { Debuff = EnemyDebuffs.Frostburn, DebuffTicks = 240, Trail = 0.6f, TrailDust = 135 }));
            Press(ItemType.WaterBolt, "海潮", 5f, "右键：脚下涌起一排水矢，沿地面朝面向推进" + Cost(3f), "Right click: a row of water bolts wells up and sweeps on along the ground the way you face" + CostEn(3f),
                (h, d) => Pay(d, 3f) && Tide(h, d, d.Shoot, 6, 1.4f, new RangedShot { Pierce = 2 }));
            Press(ItemType.Vilethorn, "荆棘绽放", 5f, "右键：朝鼠标方向扇形长出五道魔刺，敌人中毒" + Cost(3f), "Right click: five vilethorns grow out in a fan at the cursor, poisoning" + CostEn(3f),
                (h, d) => Pay(d, 3f) && CastFan(h, d, 5, 30f, 1.2f, () => new RangedShot { Debuff = EnemyDebuffs.Poisoned, DebuffTicks = 300 }));
            Charge(ItemType.SpaceGun, "等离子炮", 1f, "右键：按住蓄力，松开射出一道巨大的等离子光束，贯穿一切（伤害最多 6 倍）" + Cost(4f),
                "Right click: hold to charge, let go for a great plasma beam through everything (up to 6× damage)" + CostEn(4f),
                (h, d, c) => { if (Pay(d, 4f)) Great(h, d, d.Shoot, 2f + 4f * c, new RangedShot { Size = 1.6f + 0.6f * c, Pierce = 10, Speed = 1.3f, Trail = 1f, Tracer = true }); });
            Press(ItemType.BeeGun, "蜂群", 5f, "右键：身边一下子飞出一圈蜜蜂" + Cost(4f), "Right click: a ring of bees bursts out round you" + CostEn(4f),
                (h, d) => Pay(d, 4f) && Ring(h, d, ProjKind.Bee, 14, 5f, 1f, () => new RangedShot()));
            Press(ItemType.AquaScepter, "水龙卷", 5f, "右键：鼠标处从天而降一道道水流" + Cost(4f), "Right click: streams of water pour down on the cursor" + CostEn(4f),
                (h, d) => Pay(d, 4f) && Sky(h, d, d.Shoot, 12, 0.8f, 1f, 2f, () => new RangedShot { Weave = 4f }));
            Press(ItemType.Petalstorm, "落英", 5f, "右键：鼠标处落下一阵花瓣雨" + Cost(2f), "Right click: a rain of petals on the cursor" + CostEn(2f),
                (h, d) => Pay(d, 2f) && Sky(h, d, d.Shoot, 14, 1f, 0.6f, 3f, () => new RangedShot()));
            Press(ItemType.Flamelash, "三重火鞭", 5f, "右键：放出三颗火鞭火球，自己追向附近的敌人" + Cost(2f), "Right click: three fireballs go off by themselves after the enemies near" + CostEn(2f),
                (h, d) => Pay(d, 2f) && OrbitThen(h, d, d.Shoot, 3, 1.6f, new RangedShot { Burst = 64f, BurstDamage = 0.3f }, 10));
            Press(ItemType.FlowerofFire, "火球瀑布", 5f, "右键：朝前上方抛出七颗火球" + Cost(3f), "Right click: seven fireballs thrown up and ahead" + CostEn(3f),
                (h, d) => Pay(d, 3f) && FanUp(h, d, d.Shoot, 7, 30f, 8f, 1f, 1f, () => new RangedShot()));
            Press(ItemType.WeatherPain, "双生风暴", 7f, "右键：两团风暴从左右两侧汇向鼠标处" + Cost(1.5f), "Right click: two storms from either side close in on the cursor" + CostEn(1.5f),
                (h, d) => Pay(d, 1.5f) && Converge(h, d, d.Shoot, 2, 10f, 0.8f, () => new RangedShot()));
            Press(ItemType.MagicMissile, "飞弹群", 5f, "右键：身边环起五枚飞弹，各自追向附近的敌人" + Cost(3f), "Right click: five missiles ring you and go off after the enemies near" + CostEn(3f),
                (h, d) => Pay(d, 3f) && OrbitThen(h, d, d.Shoot, 5, 1.1f, new RangedShot(), 8));
            Press(ItemType.DemonScythe, "镰刃风暴", 5f, "右键：朝鼠标方向扇形旋飞出五把镰刀" + Cost(3f), "Right click: five scythes spin out in a fan at the cursor" + CostEn(3f),
                (h, d) => Pay(d, 3f) && Fan(h, d, d.Shoot, 5, 20f, 1f, 0.8f, () => new RangedShot()));
            Press(ItemType.BookofSkulls, "骷髅洪流", 5f, "右键：朝鼠标方向扇形放出六个骷髅头，各自追向敌人" + Cost(3f), "Right click: six skulls let out in a fan at the cursor, each going after an enemy" + CostEn(3f),
                (h, d) => Pay(d, 3f) && CastFan(h, d, 6, 30f, 0.6f, () => new RangedShot()));
            // ---- stage 3
            Press(ItemType.LightningStrike, "雷霆审判", 6f, "右键：鼠标两侧接连劈下五道闪电，各自跳向附近的敌人" + Cost(2.5f), "Right click: five strikes come down one after another across the cursor, each arcing on" + CostEn(2.5f),
                (h, d) => Pay(d, 2.5f) && Across(h, d, 5, 2f, 7, 0.7f, () => new RangedShot { Chain = 1, ChainDamage = 0.35f }));
            Press(ItemType.ClingerStaff, "诅咒火雨", 6f, "右键：鼠标处落下一阵诅咒火" + Cost(1f), "Right click: a rain of cursed flame on the cursor" + CostEn(1f),
                (h, d) => Pay(d, 1f) && Sky(h, d, ProjKind.CursedFlame, 12, 1f, 1f, 2.5f, () => new RangedShot { Debuff = EnemyDebuffs.CursedInferno, DebuffTicks = 240 }));
            Press(ItemType.GoldenShower, "灵液洪流", 5f, "右键：朝鼠标方向扇形喷出五道灵液，降低敌人防御" + Cost(5f), "Right click: five streams of ichor in a fan at the cursor, lowering defense" + CostEn(5f),
                (h, d) => Pay(d, 5f) && CastFan(h, d, 5, 10f, 1.2f, () => new RangedShot { Debuff = EnemyDebuffs.Ichor, DebuffTicks = 300 }));
            Press(ItemType.SoulDrain, "魂之爆发", 5f, "右键：鼠标处炸开一圈灵魂之焰" + Cost(4f), "Right click: a ring of soul flames bursts out on the cursor" + CostEn(4f),
                (h, d) => Pay(d, 4f) && NovaAt(h, d, ProjKind.SpiritFlame, Aim(), 10, 3f, 0.8f, () => new RangedShot()));
            Press(ItemType.MeteorStaff, "流星雨", 6f, "右键：鼠标两侧接连落下八颗流星" + Cost(4f), "Right click: eight meteors come down one after another across the cursor" + CostEn(4f),
                (h, d) => Pay(d, 4f) && Across(h, d, 8, 1.2f, 5, 1.1f, () => new RangedShot()));
            Press(ItemType.CrystalVileShard, "晶刺爆发", 5f, "右键：朝鼠标方向扇形刺出三道晶刺" + Cost(3f), "Right click: three lines of crystal thrust out in a fan at the cursor" + CostEn(3f),
                (h, d) => Pay(d, 3f) && CastFan(h, d, 3, 25f, 1.2f, () => new RangedShot()));
            Press(ItemType.CrystalStorm, "水晶漩涡", 5f, "右键：十枚水晶绕身旋转，接连射向鼠标处" + Cost(8f), "Right click: ten crystals whirl round you and fly at the cursor one after another" + CostEn(8f),
                (h, d) => Pay(d, 8f) && OrbitThen(h, d, d.Shoot, 10, 0.9f, new RangedShot(), 3));
            Press(ItemType.CursedFlames, "诅咒火环", 5f, "右键：身边炸开一圈诅咒火球" + Cost(4f), "Right click: a ring of cursed fireballs bursts out round you" + CostEn(4f),
                (h, d) => Pay(d, 4f) && Ring(h, d, d.Shoot, 10, 6f, 0.9f, () => new RangedShot { Debuff = EnemyDebuffs.CursedInferno, DebuffTicks = 240 }));
            Charge(ItemType.LaserRifle, "聚能激光", 1f, "右键：按住蓄力，松开射出一道贯穿一切的粗激光（伤害最多 6 倍）" + Cost(5f), "Right click: hold to charge, let go for a thick laser through everything (up to 6× damage)" + CostEn(5f),
                (h, d, c) => { if (Pay(d, 5f)) Great(h, d, d.Shoot, 2f + 4f * c, new RangedShot { Size = 1.5f + 0.5f * c, Pierce = 10, Tracer = true, Trail = 1f }); });
            Press(ItemType.SkyFracture, "天裂", 5f, "右键：鼠标处接连劈下八把剑" + Cost(3f), "Right click: eight swords strike down on the cursor one after another" + CostEn(3f),
                (h, d) => Pay(d, 3f) && Across(h, d, 8, 0.8f, 4, 0.5f, () => new RangedShot()));
            Press(ItemType.MedusaHead, "石化之眼", 6f, "右键：视线内的敌人都被凝视，受伤并陷入混乱" + Cost(3f), "Right click: every enemy in sight is gazed upon, struck and confused" + CostEn(3f),
                (h, d) => Pay(d, 3f) && StrikeEach(h, d, 6, 3f, EnemyDebuffs.Confused, 120));
            Press(ItemType.SpiritFlame, "灵焰群", 5f, "右键：六团灵焰绕身一圈，接连扑向鼠标处" + Cost(3f), "Right click: six spirit flames go round you once and fly at the cursor one after another" + CostEn(3f),
                (h, d) => Pay(d, 3f) && OrbitThen(h, d, d.Shoot, 6, 0.9f, new RangedShot { Home = 3f }));
            // ---- stage 4
            Press(ItemType.MagicDagger, "飞刀之舞", 5f, "右键：八把飞刀绕身旋转，接连射向鼠标处" + Cost(6f), "Right click: eight daggers whirl round you and fly at the cursor one after another" + CostEn(6f),
                (h, d) => Pay(d, 6f) && OrbitThen(h, d, d.Shoot, 8, 1.2f, new RangedShot(), 4));
            Press(ItemType.NimbusRod, "雷雨", 6f, "右键：鼠标处倾泻一阵雷雨，雨滴放出电弧" + Cost(2f), "Right click: a thundering downpour on the cursor, its drops arcing" + CostEn(2f),
                (h, d) => Pay(d, 2f) && Sky(h, d, ProjKind.NimbusRain, 18, 1.2f, 1f, 3f, () => new RangedShot { Chain = 1, ChainDamage = 0.3f }));
            Press(ItemType.BookStaff, "智慧书阵", 5f, "右键：八道书页之光绕身一圈，接连射向鼠标处" + Cost(2f), "Right click: eight lights of the pages go round you once and fly at the cursor one after another" + CostEn(2f),
                (h, d) => Pay(d, 2f) && OrbitThen(h, d, d.Shoot, 8, 0.9f, new RangedShot { Home = 2f }, 4));
            Press(ItemType.ShadowFlameHexDoll, "暗影触手阵", 5f, "右键：身边向四周伸出八根暗影触手" + Cost(6f), "Right click: eight shadowflame tentacles reach out all round you" + CostEn(6f),
                (h, d) => Pay(d, 6f) && Ring(h, d, d.Shoot, 8, 6f, 1f, () => new RangedShot { Debuff = EnemyDebuffs.Shadowflame, DebuffTicks = 240 }));
            Press(ItemType.IceRod, "冰潮", 5f, "右键：脚下涌起一排冰晶，沿地面推进，冻伤敌人" + Cost(6f), "Right click: a row of ice wells up and sweeps on along the ground, frostburning" + CostEn(6f),
                (h, d) => Pay(d, 6f) && Tide(h, d, ProjKind.IceBolt, 6, 1.5f, new RangedShot { Debuff = EnemyDebuffs.Frostburn, DebuffTicks = 240, Pierce = 2 }));
            Press(ItemType.VenomStaff, "毒牙雨", 6f, "右键：鼠标处落下一阵毒牙，敌人中剧毒" + Cost(2f), "Right click: a rain of fangs on the cursor, envenoming" + CostEn(2f),
                (h, d) => Pay(d, 2f) && Sky(h, d, d.Shoot, 12, 0.9f, 0.5f, 3f, () => new RangedShot { Debuff = EnemyDebuffs.Venom, DebuffTicks = 300 }));
            Press(ItemType.SharpTears, "血荆棘林", 6f, "右键：从脚下到鼠标处一路长出血荆棘" + Cost(2f), "Right click: blood thorns grow up all the way from your feet to the cursor" + CostEn(2f),
                (h, d) => Pay(d, 2f) && Toward(h, d, 6, 5, 1f, () => new RangedShot()));
            Press(ItemType.PoisonStaff, "毒牙扇", 5f, "右键：朝鼠标方向扇形射出七枚毒牙" + Cost(2f), "Right click: seven fangs in a fan at the cursor" + CostEn(2f),
                (h, d) => Pay(d, 2f) && Fan(h, d, d.Shoot, 7, 20f, 1f, 0.6f, () => new RangedShot { Debuff = EnemyDebuffs.Poisoned, DebuffTicks = 300 }));
            Press(ItemType.MagicalHarp, "乐章", 5f, "右键：朝鼠标方向奏出一片扇形的音符" + Cost(6f), "Right click: a fan of notes rings out at the cursor" + CostEn(6f),
                (h, d) => Pay(d, 6f) && Fan(h, d, d.Shoot, 7, 10f, 1f, 1.3f, () => new RangedShot()));
            Press(ItemType.KillingDeck, "王牌", 5f, "右键：朝鼠标方向扇出七张牌" + Cost(4f), "Right click: seven cards fanned out at the cursor" + CostEn(4f),
                (h, d) => Pay(d, 4f) && Fan(h, d, d.Shoot, 7, 12f, 1f, 0.9f, () => new RangedShot()));
            Press(ItemType.FrostStaff, "冰霜新星", 5f, "右键：鼠标处炸开一圈冰霜弹，冻伤敌人" + Cost(3f), "Right click: a ring of frost bolts bursts out on the cursor, frostburning" + CostEn(3f),
                (h, d) => Pay(d, 3f) && NovaAt(h, d, d.Shoot, Aim(), 10, 6f, 1.05f, () => new RangedShot { Debuff = EnemyDebuffs.Frostburn, DebuffTicks = 240 }));
            Press(ItemType.BejeweledStaff, "宝石风暴", 5f, "右键：七色宝石光绕身一圈，依次射向鼠标处" + Cost(3f), "Right click: gem bolts of all seven colours go round you once and fly at the cursor one by one" + CostEn(3f),
                (h, d) => Pay(d, 3f) && OrbitKinds(h, d, i => MageLefts.Gems[i % MageLefts.Gems.Length], 7, 0.7f, new RangedShot(), 5));
            Press(ItemType.FlowerofFrost, "冰球瀑布", 5f, "右键：朝前上方抛出七颗冰球" + Cost(3f), "Right click: seven frost balls thrown up and ahead" + CostEn(3f),
                (h, d) => Pay(d, 3f) && FanUp(h, d, d.Shoot, 7, 30f, 8f, 1f, 1f, () => new RangedShot { Debuff = EnemyDebuffs.Frostburn, DebuffTicks = 240 }));
            Press(ItemType.CrystalSerpent, "水晶巨蛇", 5f, "右键：射出一条巨大的水晶蛇，蛇行前进，命中后碎成六片（2.5 倍）" + Cost(4f), "Right click: a great crystal serpent winds out and shatters in six where it strikes (2.5×)" + CostEn(4f),
                (h, d) => { if (!Pay(d, 4f)) return false; Great(h, d, d.Shoot, 2.5f, new RangedShot { Size = 1.8f, Weave = 10f, Shards = 6, ShardDamage = 0.3f, Trail = 1f }); return true; });
            Press(ItemType.RainbowRod, "虹桥", 5f, "右键：放出三道彩虹，自己追向附近的敌人" + Cost(2f), "Right click: three rainbows go off by themselves after the enemies near" + CostEn(2f),
                (h, d) => Pay(d, 2f) && OrbitThen(h, d, d.Shoot, 3, 0.6f, new RangedShot(), 10));
            // ---- stage 5
            Press(ItemType.BlizzardStaff, "暴风雪", 6f, "右键：鼠标处卷起一场暴风雪，冰锥倾泻而下" + Cost(6f), "Right click: a blizzard on the cursor, icicles pouring down" + CostEn(6f),
                (h, d) => Pay(d, 6f) && Sky(h, d, d.Shoot, 30, 1.5f, 0.7f, 4f, () => new RangedShot { Debuff = EnemyDebuffs.Frostburn, DebuffTicks = 240 }));
            Press(ItemType.Razorpine, "针叶风暴", 5f, "右键：十六枚松针绕身疾旋，接连射向鼠标处" + Cost(8f), "Right click: sixteen pine needles whirl round you and shoot at the cursor one after another" + CostEn(8f),
                (h, d) => Pay(d, 8f) && OrbitThen(h, d, d.Shoot, 16, 1.5f, new RangedShot(), 2));
            Press(ItemType.MagnetSphere, "磁暴", 6f, "右键：身边放出三颗磁球，缓缓飘开，电击附近的敌人" + Cost(3f), "Right click: three magnet spheres drift off round you, zapping enemies near" + CostEn(3f),
                (h, d) => Pay(d, 3f) && Ring(h, d, d.Shoot, 3, 1.5f, 0.5f, () => new RangedShot()));
            Press(ItemType.BubbleGun, "泡泡海", 5f, "右键：朝前上方喷出十六个泡泡" + Cost(6f), "Right click: sixteen bubbles up and ahead" + CostEn(6f),
                (h, d) => Pay(d, 6f) && FanUp(h, d, d.Shoot, 16, 40f, 8f, 0.9f, 0.8f, () => new RangedShot()));
            Press(ItemType.RainbowGun, "双虹", 5f, "右键：同时射出两道彩虹" + Cost(2f), "Right click: two rainbows at once" + CostEn(2f),
                (h, d) => Pay(d, 2f) && CastFan(h, d, 2, 10f, 0.8f, () => new RangedShot()));
            Press(ItemType.LaserMachinegun, "激光扇", 5f, "右键：一次扇形射出十二道激光" + Cost(5f), "Right click: twelve lasers at once in a fan" + CostEn(5f),
                (h, d) => Pay(d, 5f) && Fan(h, d, ProjKind.MachinegunLaser, 12, 12f, 1f, 1.5f, () => new RangedShot { Trail = 0.3f }));
            Press(ItemType.BatScepter, "蝙蝠风暴", 5f, "右键：十只蝙蝠绕身盘旋，接连扑向鼠标处" + Cost(6f), "Right click: ten bats circle you and swoop at the cursor one after another" + CostEn(6f),
                (h, d) => Pay(d, 6f) && OrbitThen(h, d, d.Shoot, 10, 1.2f, new RangedShot(), 3));
            Press(ItemType.WaspGun, "胡蜂巢", 5f, "右键：身边飞出一圈胡蜂，还有两只大黄蜂" + Cost(4f), "Right click: a ring of wasps bursts out round you, and two giant bees" + CostEn(4f),
                (h, d) => { if (!Pay(d, 4f)) return false; Ring(h, d, d.Shoot, 12, 0f, 0.5f, () => new RangedShot()); Fan(h, d, ProjKind.GiantBee, 2, 20f, 1f, 0.8f, () => new RangedShot()); return true; });
            Press(ItemType.LeafBlower, "落叶风暴", 5f, "右键：鼠标处卷起一阵落叶" + Cost(8f), "Right click: a storm of leaves on the cursor" + CostEn(8f),
                (h, d) => Pay(d, 8f) && Sky(h, d, d.Shoot, 18, 1f, 0.4f, 3f, () => new RangedShot { Weave = 4f }));
            Press(ItemType.Nightglow, "夜之光华", 5f, "右键：十道星光绕身一圈，接连射向鼠标处" + Cost(2f), "Right click: ten lights go round you once and fly at the cursor one after another" + CostEn(2f),
                (h, d) => Pay(d, 2f) && OrbitThen(h, d, d.Shoot, 10, 0.3f, new RangedShot(), 3));
            Press(ItemType.SparkleGuitar, "星之独奏", 5f, "右键：鼠标四周奏出一圈星星，一齐汇向鼠标处" + Cost(4f), "Right click: a ring of stars rings out round the cursor and closes in on it" + CostEn(4f),
                (h, d) => Pay(d, 4f) && Converge(h, d, d.Shoot, 12, 7f, 1.55f, () => new RangedShot()));
            Charge(ItemType.ChargedBlasterCannon, "满能炮击", 1.2f, "右键：按住蓄力，松开射出一发巨大的电浆炮，贯穿一切（伤害最多 8 倍）" + Cost(5f), "Right click: hold to charge, let go for a great plasma shot through everything (up to 8× damage)" + CostEn(5f),
                (h, d, c) => { if (Pay(d, 5f)) Great(h, d, ProjKind.PulseBolt, 3f + 5f * c, new RangedShot { Size = 2f + c, Pierce = 20, Tracer = true, Trail = 1f, TrailDust = 226 }); });
            Press(ItemType.BetsysWrath, "龙息", 5f, "右键：朝鼠标方向喷出五发龙怒火球，身边炸开一圈火焰" + Cost(3f), "Right click: five dragon's wrath bolts at the cursor, and a ring of fire round you" + CostEn(3f),
                (h, d) => { if (!Pay(d, 3f)) return false; Fan(h, d, d.Shoot, 5, 12f, 1f, 1.2f, () => new RangedShot()); Ring(h, d, ProjKind.BallofFire, 8, 6f, 0.4f, () => new RangedShot()); return true; });
            Press(ItemType.NebulaArcanum, "星云之心", 6f, "右键：射出一颗巨大的星云核，炸开时迸出一圈星云碎片（3 倍）" + Cost(3f), "Right click: a great nebula core that bursts into a ring of shards (3×)" + CostEn(3f),
                (h, d) => { if (!Pay(d, 3f)) return false; Great(h, d, d.Shoot, 3f, new RangedShot { Size = 2f, OnDeath = Off(ProjKind.NebulaArcanumShard, 8, SpawnWay.Ring, 0.3f, 6f), Trail = 1f }); return true; });
            Press(ItemType.HeatRay, "熔光", 5f, "右键：一次扇形射出五道高温射线，点燃敌人" + Cost(6f), "Right click: five heat rays at once in a fan, setting enemies alight" + CostEn(6f),
                (h, d) => Pay(d, 6f) && Fan(h, d, d.Shoot, 5, 10f, 1f, 1f, () => new RangedShot { Debuff = EnemyDebuffs.OnFire, DebuffTicks = 240 }));
            Press(ItemType.NettleBurst, "藤蔓之海", 5f, "右键：朝鼠标方向扇形长出五道荆棘藤蔓" + Cost(3f), "Right click: five thorny vines grow out in a fan at the cursor" + CostEn(3f),
                (h, d) => Pay(d, 3f) && CastFan(h, d, 5, 30f, 0.8f, () => new RangedShot { Debuff = EnemyDebuffs.Poisoned, DebuffTicks = 300 }));
            Press(ItemType.RazorbladeTyphoon, "刃之风暴", 5f, "右键：六团利刃台风绕身一圈，接连卷向鼠标处" + Cost(2.5f), "Right click: six typhoons go round you once and sweep at the cursor one after another" + CostEn(2.5f),
                (h, d) => Pay(d, 2.5f) && OrbitThen(h, d, d.Shoot, 6, 0.3f, new RangedShot(), 5));
            Press(ItemType.ShadowbeamStaff, "暗影棱镜", 5f, "右键：朝鼠标方向扇形射出六道暗影光束，在墙间来回折射" + Cost(6f), "Right click: six shadow beams in a fan at the cursor, bouncing from wall to wall" + CostEn(6f),
                (h, d) => Pay(d, 6f) && Fan(h, d, d.Shoot, 6, 15f, 1f, 0.8f, () => new RangedShot()));
            Press(ItemType.LunarFlareBook, "月陨", 7f, "右键：鼠标处倾泻一场月耀之雨" + Cost(6f), "Right click: a downpour of lunar flares on the cursor" + CostEn(6f),
                (h, d) => Pay(d, 6f) && Sky(h, d, d.Shoot, 20, 1.2f, 0.6f, 5f, () => new RangedShot { Trail = 1f }));
            Press(ItemType.LastPrism, "七彩星陨", 7f, "右键：鼠标处落下一阵神圣之星" + Cost(4f), "Right click: a fall of hallowed stars on the cursor" + CostEn(4f),
                (h, d) => Pay(d, 4f) && Sky(h, d, ProjKind.HallowStar, 21, 1.2f, 1.2f, 4f, () => new RangedShot { Trail = 1f }));
            Press(ItemType.SpectreStaff, "幽魂军团", 5f, "右键：十只幽魂绕身一圈，接连追向敌人" + Cost(3f), "Right click: ten wisps go round you once and go after the enemies one after another" + CostEn(3f),
                (h, d) => Pay(d, 3f) && OrbitThen(h, d, d.Shoot, 10, 0.5f, new RangedShot(), 3));
            Press(ItemType.InfernoFork, "地狱火柱", 6f, "右键：鼠标两侧接连落下六团狱火" + Cost(3f), "Right click: six hellfire bolts come down one after another across the cursor" + CostEn(3f),
                (h, d) => Pay(d, 3f) && Sky(h, d, d.Shoot, 6, 0.6f, 0.8f, 3f, () => new RangedShot()));
            Press(ItemType.NebulaBlaze, "星云爆发", 5f, "右键：射出一团巨大的星云烈焰，身边炸开一圈星云火（2.5 倍）" + Cost(3f), "Right click: a great nebula blaze, and a ring of nebula fire round you (2.5×)" + CostEn(3f),
                (h, d) => { if (!Pay(d, 3f)) return false; Great(h, d, ProjKind.NebulaBlazeBig, 2.5f, new RangedShot { Size = 1.5f, Trail = 1f }); Ring(h, d, d.Shoot, 8, 0f, 0.35f, () => new RangedShot()); return true; });
            Press(ItemType.StaffofEarth, "地裂", 6f, "右键：鼠标两侧接连砸下六块巨石" + Cost(3f), "Right click: six boulders come down one after another across the cursor" + CostEn(3f),
                (h, d) => Pay(d, 3f) && Sky(h, d, d.Shoot, 6, 0.6f, 0.5f, 3f, () => new RangedShot()));
            Press(ItemType.UnholyTrident, "邪恶三叉", 5f, "右键：一次扇形射出九道三叉戟" + Cost(3f), "Right click: nine tridents at once in a fan" + CostEn(3f),
                (h, d) => Pay(d, 3f) && Fan(h, d, d.Shoot, 9, 20f, 1f, 0.7f, () => new RangedShot()));
            Press(ItemType.ToxicFlask, "毒云弥漫", 6f, "右键：鼠标两侧接连落下六个毒瓶" + Cost(2f), "Right click: six toxic flasks come down one after another across the cursor" + CostEn(2f),
                (h, d) => Pay(d, 2f) && Sky(h, d, d.Shoot, 6, 0.6f, 0.6f, 3f, () => new RangedShot()));
            Press(ItemType.PrincessWeapon, "共鸣之环", 5f, "右键：朝鼠标方向扇形放出四道共鸣" + Cost(3f), "Right click: four resonances let out in a fan at the cursor" + CostEn(3f),
                (h, d) => Pay(d, 3f) && CastFan(h, d, 4, 15f, 0.9f, () => new RangedShot()));
            Press(ItemType.ArcSurge, "雷霆万钧", 6f, "右键：鼠标两侧接连涌起六道电弧，各自跳向两个敌人" + Cost(3f), "Right click: six surges of arcs one after another across the cursor, each leaping on to two more enemies" + CostEn(3f),
                (h, d) => Pay(d, 3f) && Across(h, d, 6, 2f, 5, 0.5f, () => new RangedShot { Chain = 2, ChainDamage = 0.3f }));
            // The remade weapons' own arts over the ones above (MageKits, 法师武器创意重做设计.md).
            MageKits.Arts();
        }

        // ------------------------------------------------------------------ paying and shooting

        /// <summary>Dev: magic arts cast, their projectiles made, refused for want of mana.</summary>
        public static int DevMageArts, DevMageArtShots, DevMageArtPoor;

        /// <summary>The art's mana (its weapon's cost × times, at least 1); false (a word over the player) when it is short.</summary>
        /// <summary>For the remade weapons' own arts (MageKits): the mana paid, the cast's circles and sound, the aim.</summary>
        internal static bool MagePay(ItemDef def, float times) => Pay(def, times);
        internal static void MageArtCast(ItemDef def, Vector2? at, float size = 1f) => MageCast(def, at, size);
        internal static Vector2 MageAim() => Aim();
        internal static float MageTurn(HeroController hero) => TurnToAim(hero);

        static bool Pay(ItemDef def, float times)
        {
            var mod = TerraMod.Instance;
            var hero = HeroController.SilentInstance;
            if (mod == null || hero == null) return false;
            var held = mod.Inventory.Current;
            if (!held.IsEmpty && held.Type == def.Type) def = held.Def;
            int cost = Mathf.Max(1, Mathf.RoundToInt(Equipment.ManaCost(def) * times));
            if (EnemyAi.PlayerDebuffs.IsSilenced) return false;
            if (mod.Stats.Mana < cost && Equipment.Current.ManaFlower) mod.QuickUse(d => d.HealMana > 0);
            if (mod.Stats.SpendMana(cost)) return true;
            UI.CombatText.Show(hero.transform.position + Vector3.up * 1.2f, Lang.T("魔力不足"), new Color(0.55f, 0.6f, 1f));
            DevMageArtPoor++;
            return false;
        }

        /// <summary>Each art's damage × this (suite magearts: about 2.5 s of the weapon's own fire, 3 s at stage 5).</summary>
        static readonly System.Collections.Generic.Dictionary<ItemType, float> ArtScale = new System.Collections.Generic.Dictionary<ItemType, float>
        {
            [ItemType.DaybloomStaff] = 0.82f,
            [ItemType.AmethystStaff] = 0.73f,
            [ItemType.WandofSparking] = 1.75f,
            [ItemType.SapphireStaff] = 1.21f,
            [ItemType.DiamondStaff] = 1.4f,
            [ItemType.WandofFrosting] = 1.37f,
            [ItemType.ThunderStaff] = 1.58f,
            [ItemType.ZapinatorGray] = 1.99f,
            [ItemType.CrimsonRod] = 1.59f,
            [ItemType.GlacierFang] = 6.48f,
            [ItemType.Vilethorn] = 2.5f,
            [ItemType.SpaceGun] = 1.76f,
            [ItemType.BeeGun] = 1.49f,
            [ItemType.Petalstorm] = 1.25f,
            [ItemType.BookofSkulls] = 1.62f,
            [ItemType.WaterBolt] = 3.2f,
            [ItemType.MagicMissile] = 1.5f,
            [ItemType.Flamelash] = 3.46f,
            [ItemType.FlowerofFire] = 2.02f,
            [ItemType.DemonScythe] = 2.2f,
            [ItemType.GoldenShower] = 1.97f,
            [ItemType.CrystalStorm] = 2.36f,
            [ItemType.CursedFlames] = 2.26f,
            [ItemType.LaserRifle] = 1.79f,
            [ItemType.SkyFracture] = 1.53f,
            [ItemType.MedusaHead] = 2.88f,
            [ItemType.SpiritFlame] = 1.35f,
            [ItemType.ClingerStaff] = 1.95f,
            [ItemType.SoulDrain] = 1.48f,
            [ItemType.MeteorStaff] = 1.88f,
            [ItemType.CrystalVileShard] = 1.35f,
            [ItemType.LightningStrike] = 0.75f,
            [ItemType.MagicDagger] = 2.4f,
            [ItemType.IceRod] = 3.63f,
            [ItemType.SharpTears] = 1.61f,
            [ItemType.KillingDeck] = 1.87f,
            [ItemType.BookStaff] = 1.33f,
            [ItemType.ShadowFlameHexDoll] = 5.36f,
            [ItemType.MagicalHarp] = 8.0f,
            [ItemType.BejeweledStaff] = 8.0f,
            [ItemType.FlowerofFrost] = 2.1f,
            [ItemType.CrystalSerpent] = 2.42f,
            [ItemType.RainbowRod] = 5.36f,
            [ItemType.ZapinatorOrange] = 1.59f,
            [ItemType.NimbusRod] = 3.22f,
            [ItemType.PoisonStaff] = 7.67f,
            [ItemType.VenomStaff] = 4.56f,
            [ItemType.UnholyTrident] = 1.73f,
            [ItemType.LeafBlower] = 2.11f,
            [ItemType.HeatRay] = 4.45f,
            [ItemType.NettleBurst] = 2.75f,
            [ItemType.StaffofEarth] = 1.42f,
            [ItemType.BlizzardStaff] = 2.39f,
            [ItemType.Razorpine] = 8.0f,
            [ItemType.MagnetSphere] = 1.19f,
            [ItemType.BubbleGun] = 7.01f,
            [ItemType.RainbowGun] = 1.35f,
            [ItemType.LaserMachinegun] = 4.46f,
            [ItemType.BatScepter] = 4.56f,
            [ItemType.WaspGun] = 1.57f,
            [ItemType.ChargedBlasterCannon] = 2.76f,
            [ItemType.BetsysWrath] = 6.96f,
            [ItemType.RazorbladeTyphoon] = 1.21f,
            [ItemType.ShadowbeamStaff] = 2.51f,
            [ItemType.SpectreStaff] = 2.75f,
            [ItemType.InfernoFork] = 1.73f,
            [ItemType.ToxicFlask] = 2.04f,
            [ItemType.NebulaArcanum] = 2.04f,
            [ItemType.LunarFlareBook] = 5.15f,
            [ItemType.LastPrism] = 8.0f,
            [ItemType.NebulaBlaze] = 5.01f,
            [ItemType.ArcSurge] = 7.06f,
        };

        static int MageDamage(ItemDef def, float k) => Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(def, null) * k * (ArtScale.TryGetValue(def.Type, out float s) ? s : 1f)));

        /// <summary>One projectile of a kind for an art, from a point (world) at a velocity (px a tick), damage × k, carrying r.</summary>
        static void Bolt(ItemDef def, ProjKind kind, Vector2 from, Vector2 vel, float k, RangedShot r, float? floor = null)
        {
            var tp = TerraProjectiles.Instance;
            if (tp == null || kind == ProjKind.None) return;
            r.Item = def;
            r.Local = true;
            TerraProjectiles.SpawnClass = def.Class;
            var keep = TerraProjectiles.SpawnRanged;
            TerraProjectiles.SpawnRanged = r;
            try
            {
                if (floor.HasValue) tp.ShootFalling(kind, from, vel, MageDamage(def, k), Equipment.Knockback(def), Equipment.Crit(def), floor);
                else tp.Shoot(kind, from, vel, MageDamage(def, k), Equipment.Knockback(def), Equipment.Crit(def));
            }
            finally
            {
                TerraProjectiles.SpawnRanged = keep;
                TerraProjectiles.SpawnClass = DamageClass.None;
            }
            DevMageArtShots++;
        }

        /// <summary>The art's cast: its circles (at the caster, and where it lands), its sound, the weapon held out.</summary>
        static void MageCast(ItemDef def, Vector2? at, float size = 1f)
        {
            SwordFx.Instance?.MageArt(def, at, size);
            if (def.UseSound != null) TerraMod.Instance?.PlaySound(def.UseSound, 0.8f * def.UseSoundVolume);
            TerraMod.Instance?.PlaySound("Item_8", 0.35f, 0.8f);
            ShowGun(def, null);
            DevMageArts++;
        }

        static float Speed(ItemDef def, float k = 1f) => Mathf.Max(4f, def.ShootSpeed * k);

        // ------------------------------------------------------------------ the shapes

        /// <summary>n in a ring round the player, flying out at speed (0: the weapon's), damage × k each.</summary>
        static bool Ring(HeroController hero, ItemDef def, ProjKind kind, int n, float speed, float k, System.Func<RangedShot> make)
        {
            var c = Centre(hero);
            float s = speed > 0f ? speed : Speed(def);
            var aimWay = AimWay(hero);
            float spin = Mathf.Atan2(aimWay.y, aimWay.x);
            for (int i = 0; i < n; i++)
            {
                float a = spin + i * Mathf.PI * 2f / n;
                var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Bolt(def, kind, c + way * 0.6f, way * s, k, make());
            }
            MageCast(def, null);
            return true;
        }

        /// <summary>n round the player in an orbit, let go one by one at the cursor (each gap ticks after the last), damage × k each.</summary>
        static bool OrbitThen(HeroController hero, ItemDef def, ProjKind kind, int n, float k, RangedShot r, int gap = 6)
        {
            var c = Centre(hero);
            for (int i = 0; i < n; i++)
            {
                var o = r.Copy();
                o.Orbit = 16 + i * gap;
                o.OrbitTurn = 12f;
                o.OrbitRadius = 38f;
                o.OrbitAngle = i * 360f / n;
                o.LaunchK = 1.2f;
                o.Trail = Mathf.Max(o.Trail, 0.6f);
                float a = o.OrbitAngle * Mathf.Deg2Rad;
                Bolt(def, kind, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Speed(def), k, o);
            }
            MageCast(def, null);
            return true;
        }

        /// <summary>n in a fan `spread` degrees either side of the aim turned up by `lift`, at the weapon's speed × speedK, damage × k each.</summary>
        static bool FanUp(HeroController hero, ItemDef def, ProjKind kind, int n, float spread, float lift, float speedK, float k, System.Func<RangedShot> make)
        {
            TurnToAim(hero);
            var c = Centre(hero);
            var way = AimWay(hero);
            float up = way.x < 0f ? -lift : lift;
            for (int i = 0; i < n; i++)
            {
                float off = up + Mathf.Lerp(-spread, spread, n == 1 ? 0.5f : i / (n - 1f)) * 0.5f + Random.Range(-2f, 2f);
                Bolt(def, kind, c, Rot(way, off) * Speed(def, speedK) * Random.Range(0.85f, 1.1f), k, make());
            }
            MageCast(def, null);
            return true;
        }

        /// <summary>n in a fan `spread` degrees either side of the aim, each carrying its own of make().</summary>
        static bool Fan(HeroController hero, ItemDef def, ProjKind kind, int n, float spread, float speedK, float k, System.Func<RangedShot> make)
        {
            TurnToAim(hero);
            var c = Centre(hero);
            var way = AimWay(hero);
            for (int i = 0; i < n; i++)
            {
                float off = n == 1 ? 0f : Mathf.Lerp(-spread, spread, i / (n - 1f));
                Bolt(def, kind, c, Rot(way, off) * Speed(def, speedK), k, make());
            }
            MageCast(def, null);
            return true;
        }

        /// <summary>n down from the sky onto the cursor over `seconds`, spread `width` blocks either side, damage × k each.</summary>
        static bool Sky(HeroController hero, ItemDef def, ProjKind kind, int n, float seconds, float k, float width, System.Func<RangedShot> make)
        {
            TurnToAim(hero);
            const float Block = 16f * Effects.TerraParticles.Px;
            var target = Aim();
            float high = 18f * Block;
            var hit = Physics2D.Raycast(target, Vector2.up, high, 1 << World.ChunkColliders.TerrainLayer);
            if (hit.collider != null) high = Mathf.Max(3f * Block, hit.distance - Block);
            var tp = TerraProjectiles.Instance;
            for (int i = 0; i < n && tp != null; i++)
            {
                void Drop()
                {
                    var spot = target + new Vector2(Random.Range(-width, width) * Block, 0f);
                    var from = spot + new Vector2(Random.Range(-1.5f, 1.5f) * Block, high);
                    Bolt(def, kind, from, (spot - from).normalized * Mathf.Max(10f, Speed(def)), k, make(), spot.y + 0.5f);
                }
                int ticks = Mathf.RoundToInt(seconds * 60f * i / Mathf.Max(1, n));
                if (ticks <= 0) Drop();
                else tp.Later(ticks, (f, d) => Drop());
            }
            MageCast(def, target, 0.9f);
            return true;
        }

        /// <summary>A row of n welling up from the ground before the player, sweeping on along it the way he faces.</summary>
        static bool Tide(HeroController hero, ItemDef def, ProjKind kind, int n, float k, RangedShot r)
        {
            TurnToAim(hero);
            const float Block = 16f * Effects.TerraParticles.Px;
            float facing = Facing(hero);
            var feet = (Vector2)hero.transform.position + new Vector2(0f, -0.9f);
            for (int i = 0; i < n; i++)
            {
                var from = feet + new Vector2(facing * 1.5f * Block, (0.4f + i * 0.9f) * Block);
                Bolt(def, kind, from, new Vector2(facing * Speed(def) * (1.2f - i * 0.04f), 0f), k, r.Copy());
            }
            MageCast(def, null);
            return true;
        }

        /// <summary>n from places round the cursor `blocks` away, closing in on it (each turned toward it), damage × k each.</summary>
        static bool Converge(HeroController hero, ItemDef def, ProjKind kind, int n, float blocks, float k, System.Func<RangedShot> make)
        {
            const float Block = 16f * Effects.TerraParticles.Px;
            var target = Aim();
            for (int i = 0; i < n; i++)
            {
                float a = n == 2 ? (i == 0 ? 0f : Mathf.PI) : i * Mathf.PI * 2f / n;
                var from = target + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.4f) * blocks * Block;
                var r = make();
                r.Point = target;
                r.Seek = 3f;
                Bolt(def, kind, from, (target - from).normalized * Speed(def), k, r);
                SwordFx.Instance?.MageRune(def, from);
            }
            MageCast(def, target);
            return true;
        }

        /// <summary>A great one at the aim (a charged art), damage × k, carrying r.</summary>
        static void Great(HeroController hero, ItemDef def, ProjKind kind, float k, RangedShot r)
        {
            TurnToAim(hero);
            var c = Centre(hero);
            Bolt(def, kind, c, AimWay(hero) * Speed(def), k, r);
            SwordFx.Instance?.MageGreat(def);
            MageCast(def, null, 1.2f);
        }

        /// <summary>The weapon's own cast (its pattern, as its left would), from the player at a point (world), damage × k, carrying r.</summary>
        static void CastAt(HeroController hero, ItemDef def, Vector2 mouse, float k, RangedShot r, float turn = 0f)
        {
            var c = Centre(hero);
            var way = mouse - c;
            way = way.sqrMagnitude < 1e-4f ? new Vector2(Facing(hero), 0f) : way.normalized;
            way = Rot(way, turn);
            var shot = new ShootPatterns.Shot
            {
                Def = def, Kind = def.Shoot, From = c, Mouse = mouse, Vel = way * def.ShootSpeed, Speed = def.ShootSpeed,
                Knockback = Equipment.Knockback(def), Crit = Equipment.Crit(def), Damage = MageDamage(def, k), UseTicks = Mathf.RoundToInt(def.UseAnimation * 60f),
                Hero = hero,
            };
            r.Item = def;
            r.Local = true;
            TerraProjectiles.SpawnClass = def.Class;
            var keep = TerraProjectiles.SpawnRanged;
            TerraProjectiles.SpawnRanged = r;
            try { RangedFire.Vanilla(shot); }
            finally
            {
                TerraProjectiles.SpawnRanged = keep;
                TerraProjectiles.SpawnClass = DamageClass.None;
            }
            DevMageArtShots++;
        }

        /// <summary>The weapon's own cast n times at points across the cursor (`step` blocks apart), gap ticks one after another.</summary>
        static bool Across(HeroController hero, ItemDef def, int n, float step, int gap, float k, System.Func<RangedShot> make)
        {
            const float Block = 16f * Effects.TerraParticles.Px;
            var target = Aim();
            var tp = TerraProjectiles.Instance;
            for (int i = 0; i < n && tp != null; i++)
            {
                var spot = target + new Vector2((i - (n - 1) * 0.5f) * step * Block, 0f);
                if (i == 0) CastAt(hero, def, spot, k, make());
                else tp.Later(gap * i, (f, d) => { var h = HeroController.SilentInstance; if (h != null) CastAt(h, def, spot, k, make()); });
            }
            MageCast(def, target);
            return true;
        }

        /// <summary>The weapon's own cast n times along the way from the player to the cursor, gap ticks one after another.</summary>
        static bool Toward(HeroController hero, ItemDef def, int n, int gap, float k, System.Func<RangedShot> make)
        {
            var from = Centre(hero);
            var target = Aim();
            var tp = TerraProjectiles.Instance;
            for (int i = 0; i < n && tp != null; i++)
            {
                var spot = Vector2.Lerp(from, target, (i + 1f) / n);
                if (i == 0) CastAt(hero, def, spot, k, make());
                else tp.Later(gap * i, (f, d) => { var h = HeroController.SilentInstance; if (h != null) CastAt(h, def, spot, k, make()); });
            }
            MageCast(def, target);
            return true;
        }

        /// <summary>The weapon's own cast n times in a fan `spread` degrees either side of the aim.</summary>
        static bool CastFan(HeroController hero, ItemDef def, int n, float spread, float k, System.Func<RangedShot> make)
        {
            TurnToAim(hero);
            var target = Aim();
            var c = Centre(hero);
            for (int i = 0; i < n; i++)
            {
                float off = n == 1 ? 0f : Mathf.Lerp(-spread, spread, i / (n - 1f));
                CastAt(hero, def, c + Rot(target - c, off), k, make());
            }
            MageCast(def, null);
            return true;
        }

        /// <summary>A ring of n flying out from a point (world), damage × k each.</summary>
        static bool NovaAt(HeroController hero, ItemDef def, ProjKind kind, Vector2 at, int n, float speed, float k, System.Func<RangedShot> make)
        {
            float spin = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < n; i++)
            {
                float a = spin + i * Mathf.PI * 2f / n;
                var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Bolt(def, kind, at + way * 0.3f, way * speed, k, make());
            }
            SwordFx.Instance?.MageNova(def, at);
            MageCast(def, at);
            return true;
        }

        /// <summary>Up to n enemies in sight within 40 blocks each struck where it stands (an unseen blow of the weapon's, × k), and given a debuff.</summary>
        static bool StrikeEach(HeroController hero, ItemDef def, int n, float k, int debuff, int ticks)
        {
            var c = Centre(hero);
            int struck = 0;
            foreach (var col in Physics2D.OverlapCircleAll(c, 40f * 16f * Effects.TerraParticles.Px, 1 << 11))
            {
                if (struck >= n) break;
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy) continue;
                var at = TerraProjectiles.EnemyCentre(hm);
                if (Physics2D.Linecast(c, at, 1 << World.ChunkColliders.TerrainLayer)) continue;
                Bolt(def, ProjKind.SoulDrainBox, at, Vector2.zero, k, new RangedShot { Debuff = debuff, DebuffTicks = ticks });
                SwordFx.Instance?.MageRune(def, at);
                struck++;
            }
            MageCast(def, null);
            return true;
        }

        /// <summary>OrbitThen with each one's kind its own (the Bejeweled Staff's seven gems).</summary>
        static bool OrbitKinds(HeroController hero, ItemDef def, System.Func<int, ProjKind> kindOf, int n, float k, RangedShot r, int gap = 6)
        {
            var c = Centre(hero);
            for (int i = 0; i < n; i++)
            {
                var o = r.Copy();
                o.Orbit = 16 + i * gap;
                o.OrbitTurn = 12f;
                o.OrbitRadius = 38f;
                o.OrbitAngle = i * 360f / n;
                o.LaunchK = 1.2f;
                o.Trail = Mathf.Max(o.Trail, 0.6f);
                float a = o.OrbitAngle * Mathf.Deg2Rad;
                Bolt(def, kindOf(i), c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Speed(def), k, o);
            }
            MageCast(def, null);
            return true;
        }

        /// <summary>The Zapinators' overload: a change at random for each.</summary>
        static RangedShot ChaosShot()
        {
            var r = new RangedShot { Trail = 0.6f };
            switch (Random.Range(0, 5))
            {
                case 0: r.Shards = 3; break;
                case 1: r.Chain = 2; r.ChainDamage = 0.35f; break;
                case 2: r.Burst = 72f; r.BurstDamage = 0.45f; break;
                case 3: r.Bounces = 2; r.BounceDamage = 0.6f; break;
                default: r.Home = 4f; break;
            }
            return r;
        }
    }
}
