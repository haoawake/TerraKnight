"""Generate the drop rebalance plan, frozen before-state and verified implementation appendix.

Run from any directory: python tools/build_drop_balance_doc.py
Source-validated runtime metadata is used only for the current-state appendix.
All proposed rules and numbers are explicitly labelled as design values.
"""
from pathlib import Path
from collections import Counter, defaultdict
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'src/TerraKnight'
OUT = ROOT / '掉落记录.md'
SNAP = json.loads((ROOT / 'tools/encyclopedia_runtime.json').read_text(encoding='utf-8'))
DATA = SNAP['data']
ITEMS = DATA['items']

def read(rel):
    return (SRC / rel).read_text(encoding='utf-8-sig')

def check_sources():
    # UI/reforge/dev-test changes elsewhere do not change these exported tables.
    relevant = [rel for rel in SNAP['source_hashes'] if
                rel.startswith('Items/Items') or rel in {
                    'Items/EnemyDropData.cs', 'Items/BossPotions.cs',
                    'Items/Relics.cs', 'Items/RelicsGenerated.cs', 'Items/RecipeData.cs',
                    'Items/RecipeDataChain.cs', 'Items/RecipeSupplementGenerated.cs',
                    'Items/ItemsCraftingSupplement.cs', 'Items/BossSummons.cs',
                    'Items/RecipeDataChain.cs', 'Items/FishingItems.cs', 'Items/PetsData.cs'}]
    # Keep the old snapshot as a comparison baseline after implementation. It must never
    # silently be represented as the current DLL's runtime data.
    assert hashlib.sha256((SRC / 'Items/EnemyDropData.cs').read_bytes()).hexdigest() == SNAP['source_hashes']['Items/EnemyDropData.cs']
    return [rel for rel in relevant if hashlib.sha256((SRC / rel).read_bytes()).hexdigest() == SNAP['source_hashes'][rel]]

def name(t):
    assert t in ITEMS, t
    return f'{ITEMS[t]["Name"]}（`{t}`）'

CLASSES = ['Melee', 'Ranged', 'Magic', 'Summon']
CN = dict(zip(CLASSES, ['近战', '远程', '魔法', '召唤']))

# Every stage has two exploration/crafting candidates and two boss candidates per class.
# These are target stages, not current ItemDef stages.
CORE = [
    ['WoodenSword IronBroadsword EnchantedBoomerang Spear', 'WoodenBow Blowpipe IronBow FlintlockPistol',
     'WandofSparking AmethystStaff CrimsonRod Vilethorn', 'FinchStaff SlimeStaff LeatherWhip AbigailsFlower'],
    ['BladeofGrass ThornChakram Starfury EnchantedSword', 'GoldBow Musket Boomstick DemonBow',
     'RubyStaff DiamondStaff WaterBolt MagicMissile', 'FlinxStaff Snapthorn VampireFrogStaff HornetStaff'],
    ['Muramasa Valor BlueMoon NightsEdge', 'Handgun MoltenFury PhoenixBlaster Minishark',
     'AquaScepter SpaceGun BookofSkulls DemonScythe', 'ImpStaff SpinalTap HoundiusShootius CorruptWhip'],
    ['FieryGreatsword DarkLance Cascade Sunfury', 'ClockworkAssaultRifle Shotgun HellwingBow Harpoon',
     'Flamelash FlowerofFire LaserRifle MedusaHead', 'SpiderStaff Firecracker QueenSpiderStaff FlameburstCane'],
    ['BeeKeeper HiveFive Excalibur Amarok', 'BeesKnees HallowedRepeater DaedalusStormbow Megashark',
     'BeeGun CrystalStorm GoldenShower SkyFracture', 'OpticStaff Durendal SanguineStaff CoolWhip'],
    ['TerraBlade PossessedHatchet FlaironFlail NorthPole', 'Tsunami TacticalShotgun ChainGun AerialBane',
     'RazorbladeTyphoon Razorpine BlizzardStaff NebulaBlaze', 'RavenStaff XenoStaff DarkHarvest Kaleidoscope'],
]
CORE = [[s.split() for s in row] for row in CORE]

# Each stage/class has both an offensive and a sustain/control accessory path.
ACCESSORIES = [[s.split() for s in row] for row in [
 ['FeralClaws BandofRegeneration','PoisonBarb BandofRegeneration','BandofStarpower BandofRegeneration','PygmyNecklace BandofRegeneration'],
 ['SharkToothNecklace HermesBoots','HarpyCharm PanicNecklace','ManaRegenerationBand CelestialMagnet','SilverBracer CobaltShield'],
 ['PowerGlove CobaltShield','MagicQuiver CobaltShield','MagicCuffs ManaFlower','SnakeBand ApprenticeScarf'],
 ['WarriorEmblem CrossNecklace','RangerEmblem CrossNecklace','SorcererEmblem CrossNecklace','SummonerEmblem CrossNecklace'],
 ['MechanicalGlove FrozenTurtleShell','MoltenQuiver RifleScope','CelestialCuffs ArcaneFlower','MobiusStrip WickedArmlet'],
 ['FireGauntlet FrozenShield','SniperScope CharmofMyths','CelestialEmblem ManaCloak','PapyrusScarab OuroborosRing'],
]]

# Explicit armour paths; mixed Flinx starter is intentional and gets no false set bonus.
ARMOR_PATHS = [
 (0,'Melee','IronHelmet IronChainmail IronGreaves','铁/铅与基础建材','10；通用入门甲，职业输出由基础武器承担'),
 (0,'Ranged','CopperHelmet CopperChainmail CopperGreaves','铜/锡与基础建材','9；通用入门甲，弹药保障独立提供'),
 (0,'Magic','WoodHelmet WoodBreastplate WoodGreaves','木材；通用金属甲由制作/商店补充','8；阶段0不强制专精全套，保证回蓝入口'),
 (0,'Summon','WoodHelmet WoodBreastplate WoodGreaves','木材；矮人项链前移阶段0给额外1仆从','8；初始1+项链1=2，不能再免费加第三只'),
 (1,'Melee','GoldHelmet GoldChainmail GoldGreaves','金/铂金；职业饰品补专精','18；金属甲侧重防护，不把12%进攻预算全额叠到其上'),
 (1,'Ranged','FossilHelm FossilShirt FossilPants','坚固化石前移阶段1；悬崖/真菌岩质源','16；总进攻参考12%，含暴击/省弹折算'),
 (1,'Magic','JungleHat JungleShirt JunglePants','孢子/藤蔓/毒刺；阶段1完整配方','14；总进攻参考12%，降耗和魔力属于同一预算'),
 (1,'Summon','WoodHelmet FlinxFurCoat WoodGreaves','雪怪皮毛+木材；皮毛真菌地区稳定供给','14；这是混搭，无三件套奖励；外套+1、项链+1，合计3仆从'),
 (2,'Melee','ShadowHelmet ShadowScalemail ShadowGreaves','魔矿/鳞片；城市/水道来源','28；总进攻参考16%，保留机动特色'),
 (2,'Ranged','NecroHelmet NecroBreastplate NecroGreaves','骨/蛛网；无需阶段3材料','25；总进攻参考16%，计入省弹后的资源优势'),
 (2,'Magic','MeteorHelmet MeteorSuit MeteorLeggings','陨石；水晶/施法怪','22；太空枪免费须降低枪面板/套装其他输出，不能双重满额'),
 (2,'Summon','ObsidianOutlawHat ObsidianLongcoat ObsidianPants','黑曜石前移阶段2少量源+蛛网/丝绸','22；鞭范围/速度计入总预算；额外1仆从+项链，合计3'),
 (3,'Melee','MoltenHelmet MoltenBreastplate MoltenGreaves','狱石/黑曜石；地狱熔炉3','40；总进攻参考20%'),
 (3,'Ranged','HuntressWig HuntressJerkin HuntressPants','女猎人T2前移3；拟配方骨/丝绸/狱石，移除阶段4购买限制','36；远程/哨兵混合套，两种输出共用预算，不免费加满哨兵DPS'),
 (3,'Magic','AncientCobaltHelmet AncientCobaltBreastplate AncientCobaltLeggings','改为阶段3花园古代甲；孢子/藤蔓/丝绸+本地精英材料','32；名称虽有钴，不要求阶段4钴矿；独立配方，禁止阶段2旧低率提前掉'),
 (3,'Summon','SpiderMask SpiderBreastplate SpiderGreaves','蜘蛛牙+蛛网；深巢','32；装备额外2、项链额外1，含初始1合计4仆从'),
 (4,'Melee','TitaniumMask TitaniumBreastplate TitaniumLeggings','钛金；先完成阶段4入口','56；钛金防御机制收益从纯进攻预算扣除'),
 (4,'Ranged','HallowedHelmet HallowedPlateMail HallowedGreaves','神圣锭阶段4正式Boss/完整试炼材料包','50；同家族身体/腿共享，必须按头盔分支结算'),
 (4,'Magic','HallowedHeadgear HallowedPlateMail HallowedGreaves','神圣锭阶段4正式Boss/完整试炼材料包','44；共享身体腿时，用头盔/套装防御差补齐，不能给同一件两个数值'),
 (4,'Summon','BeeHeadgear BeeBreastplate BeeGreaves','蜂蜡30；蜂巢主来源','44；明确数值见3.3；常规配置移除额外位数项链，保持4仆从预算'),
 (5,'Melee','BeetleHelmet BeetleScaleMail BeetleLeggings','甲虫外壳+阶段5制作材料','74；攻击胸与防御胸分别校准，不同时拿两者效果'),
 (5,'Ranged','ShroomiteHeadgear ShroomiteBreastplate ShroomiteLeggings','叶绿+阶段5主题材料；不同弹药头部公平可得','66；潜行/站定增益必须按可用输出时间折算'),
 (5,'Magic','SpectreMask SpectreRobe SpectrePants','灵气+阶段5材料；治疗头作为侧向选择','58；吸血与伤害头互斥，治疗收益折算'),
 (5,'Summon','SpookyHelmet SpookyBreastplate SpookyLeggings','阴森木+阶段5制作材料','58；套装额外3+莎草纸1+初始1=5；与鞭/仆从总DPS一同验收'),
]

# key, target reward stage, encounter/reward rationale, four class candidates,
# signature accessory, budget tier (normal boss / elite).
BOSSES = [
 ('FalseKnight',0,'重甲、跳压与落石；给破甲和抗击退，下一场能用', 'EnchantedBoomerang IronBow Vilethorn SlimeStaff','Shackle'),
 ('BigFly',0,'慢速飞行、撞墙与幼体；给对空和自动追击', 'Spear Blowpipe CrimsonRod FinchStaff','CloudinaBottle'),
 ('Mawlek',0,'近身爪击、酸液与跳压；给安全射程和机动', 'EnchantedBoomerang FlintlockPistol Vilethorn AbigailsFlower','BandofRegeneration'),
 ('BigBuzzer',0,'冲撞、召唤；属于早期精英，不用必掉一整包天空装备', 'Spear Blowpipe CrimsonRod FinchStaff','ShinyRedBalloon'),
 ('Hornet',1,'突进、跳跃、掷针；打赢披风门槛后领阶段1奖，不能先要求有披风', 'Starfury Boomstick MagicMissile HornetStaff','FeralClaws'),
 ('MegaMossCharger',1,'地面冲撞、跳压；给长距离迎击与地面控制', 'ThornChakram DemonBow WaterBolt VampireFrogStaff','HermesBoots'),
 ('GhostAladar',1,'弹跳身体；给能追打高低位置的武器', 'EnchantedSword Boomstick MagicMissile HornetStaff','CloudinaBottle'),
 ('GhostNoEyes',1,'移动平台、游魂与传送；给追踪、滞空操作便利', 'Starfury DemonBow MagicMissile HornetStaff','FlyingCarpet'),
 ('MantisLord',2,'轮流突刺、下落和飞刃；给精准、回旋和鞭标记', 'Muramasa Handgun BookofSkulls SpinalTap','FeralClaws'),
 ('MageKnight',2,'瞬移、冲刺与小怪；给短窗口爆发，按精英待遇', 'Valor PhoenixBlaster BookofSkulls ImpStaff','BandofStarpower'),
 ('MageLord',2,'瞬移、追踪弹和二阶段砸地；给法力循环与跨平台输出', 'BlueMoon PhoenixBlaster DemonScythe HoundiusShootius','MagicCuffs'),
 ('MegaBeamMiner',2,'激光扫射、跳跃与固定炮台；给光束和掩体外输出', 'NightsEdge Handgun SpaceGun HoundiusShootius','SharkToothNecklace'),
 ('DungDefender',2,'地滚球、钻地与跳压；给弹跳/穿透、击退抵抗', 'BlueMoon MoltenFury AquaScepter CorruptWhip','WormScarf'),
 ('FlukeMother',2,'固定本体、不断孵化；给清杂和召唤协同', 'NightsEdge PhoenixBlaster AquaScepter ImpStaff','BrainOfConfusion'),
 ('MegaJellyfish',2,'本体短窗口可伤、周边电击；给窗口爆发而非无限驻场白打', 'Valor Handgun DemonScythe HoundiusShootius','BandofStarpower'),
 ('GhostHu',1,'漂浮光环与横向落下；按真菌区早期支线重定级', 'ThornChakram DemonBow WaterBolt VampireFrogStaff','BandofRegeneration'),
 ('GhostMarmu',3,'弹跳、快速反弹；花园支线按阶段3奖励', 'Cascade Shotgun MedusaHead QueenSpiderStaff','CrossNecklace'),
 ('TraitorLord',3,'快速突进、地波；花园实际路线按阶段3，给主动进攻和生存取舍', 'DarkLance HellwingBow Flamelash Firecracker','PowerGlove'),
 ('FalseKnight@5',2,'高速重甲跳压；@5是变体键，实际阶段2，不能当终局奖', 'NightsEdge PhoenixBlaster BookofSkulls ImpStaff','BoneGlove'),
 ('BlackKnight',3,'重甲滚动、连续双体；给穿透、多目标与四职业等价徽章', 'Sunfury ClockworkAssaultRifle LaserRifle Firecracker','WarriorEmblem'),
 ('JarCollector',3,'跳跃、砸罐、召唤；给清杂和仆从协同', 'Cascade Shotgun MedusaHead SpiderStaff','SummonerEmblem'),
 ('MimicSpider',3,'奔跑、跳扑、喷酸；给追击和远距反击', 'DarkLance HellwingBow Flamelash QueenSpiderStaff','CrossNecklace'),
 ('GhostGalien',3,'本体与旋转镰刀；给回旋、轨道和持久输出', 'Sunfury Harpoon FlowerofFire QueenSpiderStaff','WarriorEmblem'),
 ('GhostXero',2,'漂浮本体、飞剑；安息之地按阶段2，给对空', 'Valor Handgun BookofSkulls ImpStaff','CobaltShield'),
 ('MageLord@5',3,'更快瞬移、追踪弹；实际阶段3', 'Cascade ClockworkAssaultRifle LaserRifle SpiderStaff','SorcererEmblem'),
 ('HiveKnight',4,'连刺、蜂群、蜂蜜刺球；蜂巢阶段4，给四职业蜂系与蜜蜂套材料', 'BeeKeeper BeesKnees BeeGun OpticStaff','HiveBackpack'),
 ('Hornet@4',4,'高速针线、尖刺布场；给精确射击、标记和闪避', 'Excalibur DaedalusStormbow SkyFracture Durendal','BlackBelt'),
 ('InfectedKnight',3,'突刺、跳压、感染气球；打赢帝王之翼门槛后领阶段4奖，战前按阶段3资源测', 'Sunfury ClockworkAssaultRifle LaserRifle SpiderStaff','AvengerEmblem'),
 ('InfectedKnight@5',4,'更快突刺与连续召唤；实际阶段4', 'Amarok Megashark CrystalStorm SanguineStaff','CrossNecklace'),
 ('GhostMarkoth',4,'漂浮、飞剑与旋转护盾；给追踪和可控的对空窗口输出', 'HiveFive HallowedRepeater SkyFracture OpticStaff','CobaltShield'),
 ('Oblobble',4,'双体弹幕，单体狂暴；给多目标与移动输出', 'HiveFive HallowedRepeater GoldenShower OpticStaff','RangerEmblem'),
 ('LobsterLancer',4,'驯兽师、野兽滚动；给切换目标和地面控制', 'Excalibur DaedalusStormbow SkyFracture CoolWhip','SummonerEmblem'),
 ('Zote',4,'普通左特几乎无威胁；只有纪念与少量材料，不作为核心保底来源', 'HiveFive HallowedRepeater CrystalStorm OpticStaff','StressBall'),
 ('PaleLurker',4,'逃跑、投掷尖刺；按精英支线，不因名字给首领整包奖', 'Amarok Megashark SkyFracture CoolWhip','BlackBelt'),
 ('Grimm',5,'冲刺、火柱、蝙蝠弹；给火焰与节奏爆发', 'NorthPole ChainGun Razorpine RavenStaff','FireGauntlet'),
 ('NightmareGrimm',5,'高速火柱、蝙蝠和弹幕；终局强支线，奖励上限高于普通格林', 'TerraBlade Tsunami RazorbladeTyphoon Kaleidoscope','CelestialShell'),
 ('WhiteDefender',5,'滚球、钻地、连跳；给弹跳、范围与防御方向', 'FlaironFlail TacticalShotgun BlizzardStaff XenoStaff','FrozenShield'),
 ('GreyPrince',5,'不可预测跳压、召唤、冲击波；给清杂与机动', 'PossessedHatchet ChainGun NebulaBlaze DarkHarvest','CharmofMyths'),
 ('HollowKnight',5,'突刺、连斩、感染弹幕；给终局稳定主力', 'TerraBlade Tsunami RazorbladeTyphoon XenoStaff','DestroyerEmblem'),
 ('NailBros',5,'双体突刺与旋风；给穿透和双目标收益', 'TerraBlade AerialBane BlizzardStaff DarkHarvest','MechanicalGlove'),
 ('Paintmaster',5,'滚动、涂料、落地挥击；给区域持续伤害', 'FlaironFlail TacticalShotgun Razorpine RavenStaff','CelestialEmblem'),
 ('Nailsage',5,'快速连斩、旋转追击；给短窗口反击与追踪', 'PossessedHatchet Tsunami NebulaBlaze Kaleidoscope','MasterNinjaGear'),
 ('HollowKnightPrime',5,'高密度突刺、剑雨、光柱；给高操作上限终局变体', 'NorthPole AerialBane RazorbladeTyphoon Kaleidoscope','CelestialShell'),
 ('FinalBoss',5,'浮空平台、光束、剑雨；给通关纪念与四职业终局可选奖，不要求刷它才能通关', 'TerraBlade ChainGun NebulaBlaze XenoStaff','CelestialShell'),
]

INTRO = r'''# 掉落记录

> 2026-10-16 平衡方案与实装记录。**第1～8节保留设计目标，第9节保留实现前基线，第11节列出当前游戏导出的实装数值与验证状态；具体差异以第11节为准。**
> 原版泰拉瑞亚定位用于解释素材与机制，不约束本模组的伤害、防御和获得阶段。预算区间和“开荒顺畅”的目标不等于已完成全部Boss实战验证。

## 1. 调整结论与阅读方法

1. 材料更稳定，成品装备更克制。取消普通怪约 25%～33.33% 的武器独立掉率，以及高血量普通怪每次必掉装备的旧规则。开荒保障通过低成本制作、常驻商店和首次探索奖励完成。
2. 四职业分别建设完整获取路径；每阶段每职业配置 2 把探索/制作武器、2 把 Boss 候选武器、1 套可制作主力甲、1 件进攻饰品、1 件续航/防御饰品。数量相同只是起点，获取耗时、消耗、对空能力和实际通关表现也要接近。
3. Boss 首次通关随机掉落一件当前阶段优质武器或套装部位，加一件有效功能奖励与材料。主力奖励先由系统按四职业各25%权重抽职业，再在对应合格装备池内抽一件；取消额外独立武器连掉。不提供选职业或领奖选择界面，不根据最后一击或当前手持武器改变概率。
4. 装备按实际路线重定级。蜂巢统一为阶段4，蜜蜂套从防御13的旧套装改为阶段4召唤甲；失败冠军的 `@5` 只是变体标识，仍按阶段2设计。
5. 矿物按“原版定位 → 新阶段 → 区域生态 → 获取/熔炼/制作门槛”一起重排。苍绿之径早期掉孢子、藤蔓，阶段4的丛林/花园才加入叶绿；不会让任何区域的小怪提前泄漏后期矿石。

本文件先给可执行的规则和阶段表，再给完整 Boss 奖励、材料、装备和敌人审计附录。附录保留逐项现状，便于实现时逐行核对；不得把附录旧掉率当作新版设计。

## 2. 实现前源码确认的问题

| 项目 | 实现前行为与证据 | 调整方向 |
|---|---|---|
| 职业偏置 | `EnemyDrops.Roll` 在已解锁的 Group 内等概率抽物品，不先选职业 | 四职业各25%随机，类内等权；通用饰品独立功能槽 |
| 硬塞保底 | 原文规定精英最多10件的混合池必掉；很多 HP≥70 的怪也有必掉组 | 普通精英成品总概率12%，只有首次有名精英保障；不可按 HP 自动升级奖励 |
| 高概率武器 | 巴德尔长者史莱姆杖1/3、闪电扭曲者恶魔剑1/3、巨型跳虫冰雪魔杖1/3等 | 普通怪成品武器总概率2%，精英6%；Boss武器并入唯一职业包 |
| 门槛绕过 | `C()` 的 LockStage 默认0；`D/G()` 才在静态构造时取物品Stage | 所有可成长掉落使用同一资格判断，原有行也不能豁免 |
| 阶段漂移 | `Areas.LevelOf` 蜂巢=4，HiveKnight掉落与BossStats=3；残破容器所在地=3，BossStats=4 | 区分“战前预算/战后奖励”，蜂巢统一4；不要机械复制区域数字到所有Boss |
| 材料泛滥区域 | 为满足每材料≥5种怪，非生态区域也塞了蜂蜡、硬模式矿物等 | 取消固定5种怪；保证至少两条可达获取途径，可以是同区两怪、采集/制作或首领 |
| 实际矿量 | 锭转矿：普通怪/精英乘每锭矿量再×2，Boss只乘每锭矿量；直接矿石也×2 | 新方案全部按最终矿石个数写，不再叠加隐藏翻倍 |
| 开荒资源不齐 | 火花魔棒、紫晶法杖元数据=阶段1；召唤第一根杖多依赖掉落；商店部分轮换 | 四职业阶段0都有稳定入口，并给法师法力、射手弹药、召唤鞭配套 |
| 旧平衡统计遗漏 | `DevAutoTestBalance` 排除了召唤、鞭、持续引导，且汇总全部武器中位数、混搭最高防御 | 验收按职业、整套装备、可获得路线，加入仆从/鞭与资源停火时间 |

阶段解锁现状：`Shop.UnlockedStage()` 计五项能力已获得数量，标准路线依次是披风、爪、心、翼、影披；它不验证取得顺序。拟实现仍可显示0～5，但关键资源同时检查对应能力/地区解锁，不能靠跳序凑数解锁全套后期资源。Boss结算先确认胜利与能力发放，再发战后奖励；不能在击杀帧用旧Stage把门槛Boss的奖过滤没。

## 3. 阶段与职业的共同预算（全部为拟定值）

| 阶段 | 标准路线与资源层 | 主力武器参考单次总伤害* | 普通怪实战击杀时间 | 首次主力替换目标 |
|---|---|---|---|---|
| 0 | 国王山道、十字路、苍绿前段；铜锡铁铅银、植物和基础宝石 | 12～22 | 2～4秒 | 首个长椅/制作点前有基础武器，15分钟内第二种打法 |
| 1 | 披风后，真菌区/悬崖/雾谷；金铂钨、丛林完整材料 | 22～36 | 2～4秒 | 新区20分钟内一把升级主力 |
| 2 | 螳螂爪后，城市/水道/水晶/安息；魔矿猩红、陨石、骨 | 36～56 | 2～4秒 | 不刷指定Boss也能造同阶段基础武器 |
| 3 | 水晶之心后，深巢/盆地/花园；狱石、黑曜石、蜘蛛系 | 56～82 | 2～4秒 | 每职业都有对空和清杂选择 |
| 4 | 帝王之翼后，边缘/蜂巢/斗兽场/深渊；硬模式矿与叶绿 | 82～122 | 2～5秒 | 首个阶段4支线后有可靠主力，蜂系不落后 |
| 5 | 暗影披风后，剧团/白宫/神居/终局；终局素材 | 122～180 | 2～5秒 | 常规主力足够通关，终极奖励用于挑战和专精 |

\* 这是约0.5秒一次、单体一次命中武器的**合计命中包预算**，不是把所有武器面板改成同一个数。弓枪要包含弹药；散弹包含预期命中的所有弹丸；高攻速、多段、穿透、追踪、鞭与召唤必须按下述模型反推。该区间是初始设计锚点，须与现有敌人HP共同验证；若常规小怪超过上表时长，优先校准整阶段预算，不能只提高某职业。

### 3.1 伤害和效果如何定值

`有效DPS = Σ(每种命中的防后伤害 × 实际每秒命中次数 × 暴击期望) + DoT/套装/标记贡献`。现状敌人防后伤害至少1，基础减伤按防御的一半；以实际实现中的取整、穿甲和局部伤害倍率为准。

- 近战：短手武器在同等可输出时间内可以比追踪远程高10%～15%；计入接近、闪避造成的停手，整场Boss表现仍须在四职业中位数±15%以内。剑气和刀刃同时命中按真实共享免疫核算。
- 远程：固定阶段基础弹药测试，另测特种弹药上限；枪自身伤害和弹药分别吃倍率/取整，不能只读枪面板。散弹、三连发、蜂弓衍生蜂都计入总量。基础弹药成本以连续战斗5分钟不超过该路线5分钟常规收入的20%为目标。
- 魔法：主力持续输出按60秒含自然回蓝/喝药/换位统计，爆发按5秒窗口；两者都要满足职业目标。降低早期耗魔比只加伤害更优先；阶段0基础法杖耗魔2～3，并提供基础魔力扩容/低价回蓝来源。
- 召唤：按该阶段实际仆从位数、仆从追敌/卡路、鞭标记、哨兵和同时攻击上限计算总伤害；鞭和仆从共用一个预算，不能各自达到完整职业预算再相加。对空Boss必须测试，不以地面木桩结果替代。
- 持续引导、悠悠球、回旋、贯穿、吸血、百分比伤害、追踪、自动攻击为特殊项：逐种读取发射器/免疫/仆从AI后定面板；禁止直接按使用速度套公式。低伤高频不得被阶段防御压成每击1。
- 核心升级：同职业同路线新阶段主力的实战稳定输出比上阶段主力提高约25%～40%，或以相近输出换一个明确的生存/操作收益。Boss奖比本阶段可制作基础主力约高10%～15%，不能成为下一阶段的免做通行证。
- 吸血和自动治疗按每秒治疗量与触发冷却限制，不能随攻速/弹丸数无限放大；护甲穿透按最终防御测试。固定伤害、鞭标记、饰品反击弹也跟阶段重算，不能留着原版固定值。

### 3.2 护甲预算：整套比较，不取三件最高防御拼接

下列防御是整套有效防御（包括套装防御加成）；伤害加成是三件与套装合计。暴击、攻速、仆从位数、穿甲、哨兵等收益均折算进套装预算，不是可额外全加。

| 阶段 | 近战防御 | 远程防御 | 魔法防御 | 召唤防御 | 普通主力套总进攻预算 | 含初始1只的常规仆从位上限 |
|---|---:|---:|---:|---:|---|---:|
| 0 | 10 | 9 | 8 | 8 | 对应职业伤害8%或等值效果 | 2 |
| 1 | 18 | 16 | 14 | 14 | 12% | 3 |
| 2 | 28 | 25 | 22 | 22 | 16% | 3 |
| 3 | 40 | 36 | 32 | 32 | 20% | 4 |
| 4 | 56 | 50 | 44 | 44 | 24% | 4 |
| 5 | 74 | 66 | 58 | 58 | 30% | 5 |

仆从数量带来的收益须包含在职业总DPS中；上表百分比是没有强机制时的参考，不能机械把旧套装百分比叠上去。阶段0使用通用基础甲也应可通关，各职业专精由低成本头部变体/饰品补足。阶段1起每职业保证完整主力套：近战丛林/暗影方向，远程化石/死灵方向，魔法丛林/宝石方向，召唤雪怪→黑曜石→蜘蛛→蜂系→提基/阴森方向。调整套装所在阶段后，配方材料与工作站一起迁移。

生存验收按同阶段同生命量和同难度的实际受击次数比较：非近战之间目标差≤1次，近战可多承受约1次作为贴身补偿。高减伤、闪避、护盾、吸血不能再免费叠加近战最高防御。控制/抗击退仍保留职业选择。

### 3.3 蜂巢整组重定级（阶段4）

| 内容 | 源码现状 | 拟定阶段4数值/行为 |
|---|---|---|
| 蜜蜂头饰 | 防御4、召唤伤害4%、+1仆从 | 防御13、召唤伤害6%、+1仆从 |
| 蜜蜂胸甲 | 防御5、召唤伤害4%、+1仆从 | 防御18、召唤伤害6%、+1仆从 |
| 蜜蜂护胫 | 防御4、召唤伤害5% | 防御13、召唤伤害6%、移动速度5% |
| 完整套装 | 总防御13、召唤伤害23%、额外2仆从 | 总防御44、总召唤伤害24%（单件18%+套装6%）、套装额外+1仆从，总计初始1+装备3=4；新增“鞭命中后仆从优先攻击目标”作为操作便利，不另加免费蜂群DPS |
| 配方 | 蜂蜡8/12/10，砧阶段0；物品Stage3 | 用量保留8/12/10，阶段4砧配方；蜂蜡掉落限定蜂巢并阶段4，禁止提前从深渊小蜜蜂/阴影爬虫供料 |
| 蜂巢骑士材料 | 蜂蜡及多件蜂系武器独立掉落 | 必掉蜂蜡12～18；三种本地蜂怪均有补充来源；胜利首次按4.1随机掉一件职业武器或套装部位，允许正常重复 |
| 养蜂人/蜂膝弓/蜜蜂枪 | 原版蜂后定位，部分独立1/3概率 | 全部阶段4武器；分别预算刀刃+蜂、箭+蜂、全部蜂命中总DPS；只进职业包，不再各自33% |
| 召唤蜂系候选 | 黄蜂法杖原版早期定位 | 阶段1黄蜂杖保留早期定位；阶段4用魔眼杖作为追击机制候选，若要蜂外观必须另做外观适配并保持伤害预算；不把同一根杖同时当阶段1和4升级品 |
| 蜂巢背包/蜂窝 | 原版效果及阶段沿用 | 背包提供蜂系操作/协同，不让四职业强制佩戴；蜂窝受击触发内置冷却1秒，反击DPS≤阶段4主力的8%；新增上限为拟实现机制 |

### 3.4 饰品预算（每件，拟定）

| 阶段 | 纯伤害型参考 | 纯防御型参考 | 续航/功能型处理 |
|---|---|---|---|
| 0 | 职业伤害4%～5% | 防御2或减伤3% | 回蓝/弹药/仆从控制优先；移动便利不抵消职业主力保底 |
| 1 | 6%～7% | 防御3或减伤4% | 简单恢复/抗击退，不同时给高输出和长无敌 |
| 2 | 8%～9% | 防御4或减伤5% | 魔力手铐、职业专精开始成型 |
| 3 | 10%～11% | 防御6或减伤6% | 四职业徽章等价；获得方式相当 |
| 4 | 12%～13% | 防御8或减伤7% | 组合饰品拆算所有子效果，数量上限收益不能免费叠加 |
| 5 | 14%～15% | 防御10或减伤8% | 毕业组合各分支独立校准；多功能型下调单项数值 |

同源饰品/升级链只取最高档效果；4件普通进攻饰品合计增幅参考上限阶段0～5分别为20/28/36/44/52/60%，再算攻速、暴击和套装的组合结果。该上限为验收预算，不意味着直接截断所有Build。信息饰品、时装、钓具、建材和工作站不占Boss有效装备槽；早期跑图奖励属于通用槽，不能冒充缺失的法师或召唤武器。已有功能保留其用途；任何新增冷却或同源排他都需实现并测试。

## 4. 掉率、数量和保底

所有概率为普通难度拟定基线，且是**整类总概率**。先检查地区、阶段、敌人资格，再掷类别，然后在类别内抽一件；不是每加一把武器就多掷一次。

| 来源 | 材料 | 武器 | 盔甲 | 饰品 | 总成品率 |
|---|---|---:|---:|---:|---:|
| 常规怪（可正常重生） | 基础木石/草药原概率×1.2，封顶80%；主材料×1.25，封顶75%，已必掉仍必掉 | 2% | 1% | 1% | 4% |
| 常规精英 | 主材料×1.2，封顶85%；至少一份地区主材料 | 6% | 3% | 3% | 12% |
| 有名、有限次数的精英首次 | 主材料必掉，首通规则按个人存档 | 从4职业各1候选中等概率随机掉1件 | 与武器共用随机主力槽 | 通用功能另按指定奖 | 首次一次保底，重战回精英率 |
| 正式Boss首次 | 必掉地区主材料+指定制作材料 | 1件四职业等权随机武器/甲部位 | 与武器共用随机主力槽 | 1件当前有效功能奖励 | 2个有效奖励槽，均自动随机结算 |
| 正式Boss重复 | 必掉地区主材料，数量为首次60%（向上取整） | 1件四职业等权随机武器/甲部位 | 与武器共用随机主力槽 | 功能奖20%，允许正常重复掉落 | 主力槽1件，功能槽另20% |

稀有成品槽的类别互斥，一次最多1件。例：先用一个随机数决定0～2%武器、2～3%盔甲、3～4%稀有饰品，其余不触发此槽。未解锁候选不能占用抽取权重，候选空属于配置错误，应在发布前补齐。

**职业基础饰品不受稀有饰品1%总率限制。** 每阶段每职业配置进攻与续航/功能两条路线；每个普通怪固定提供其中两件关键饰品，独立基础饰品槽总率16%，两件各8%；精英总率40%，两件各20%。同一关键饰品至少有3种当阶段可达的普通怪来源，并有本阶段材料直接制作，不要求先刷到另一件稀有成品。不同怪的特色名单固定，不会每次打开图鉴变换。稀有成品槽与基础饰品槽可各掉1件。

星力手环明确前移阶段0，新增坠落之星3+铁锭2、阶段0砧配方，取消必须先刷恐慌项链的唯一合成依赖。再生手环、野性爪、矮人项链，以及后续各阶段的关键饰品按同一可达性规则统一检查。具体48个路线位置与完整实际来源见第11节。

普通怪主材料有多行时并非全部独立上调：每只常规怪配置1个主材料池+1个副材料池+至多1个基础建材池，合并相同物品，矿石作为一个主材料池。材料每百只的总“制作等价量”目标较现状增加15%～25%，避免原有数十材料行各自+25%导致背包/经济崩溃。草药25%→30%，孢子50%→62.5%，藤蔓5%→6.25%为单行示例；区域主来源不足时按第6节改为主材料概率，不以低旧率维持饥荒。

### 4.1 Boss随机装备池与首通成就感

- 首通主力槽100%：系统随机抽近战/远程/魔法/召唤，各25%；再按武器75%、套装部位25%抽类别，最后在该职业、该类别的合格候选中等概率掉1件。每个职业都须有当前阶段值得用的候选；空池属于配置错误，发布前必须补齐，不转移权重给其他职业。玩家无需操作。
- 首通功能槽100%：若为职业饰品池，独立按四职业各25%抽职业，再在该职业的等价饰品中随机掉1件；不强制与主力槽职业相同。若该Boss配置的是通用有效饰品池，则在通用池内等概率抽1件。每个Boss固定使用其中一种池，不能把通用池当作第五职业稀释四职业权重。四职业徽章随机掉1枚。
- 重复挑战：按相同四职业等权规则独立随机结算主力槽；功能槽20%触发，触发后按上述固定池规则随机掉落。允许连续抽到同职业、同物品；不按已拥有装备重抽、不累计自选资格、不自动改成材料。首次记录按玩家保存，仅用于首通/重复奖励差异。
- 战后奖服务下一段路线：给对空、清杂、短窗口爆发、续航其中至少一项明确提升；不是声称掉落能帮助尚未击败的同一Boss。门槛Boss按“战前资源/战后奖励”拆开。
- 简单左特不发正式Boss主力包；苍白潜伏者、灵魂战士、反击蝇之王按有名精英处理。无敌单位、被动单位、玩家阴影、无限召唤幼体不提供可循环刷的核心材料/保底计数。
- 圣物/纪念品为额外奖励。药水、治疗心和吉欧沿用独立功能，不挤占装备奖励槽。掉入背包、场景传送、死亡离场等都要验证实际领取。

### 4.2 开荒不依赖刷武器

首次到达制作点，自动随机获得一件基础武器，四职业各25%；20次有效常规击杀内再自动随机结算一件当前区域探索武器（每阶段每玩家一次）。不提供选择界面，也不保证两次随机结果属于同职业或不同打法。自己想玩的职业靠下表稳定制作/常驻商店获取，不能把随机奖励当作唯一开荒保障。这是拟新增机制，不是现有代码已具备。

| 职业 | 阶段0稳定入口（拟新增或前移） | 低成本替代路线 | 弹药/法力/召唤配套 |
|---|---|---|---|
| 近战 | 木剑现有配方：木材7，工作台 | 铁阔剑改铁锭6，阶段0砧；首次探索可选回旋镖 | 短手主力外再给长矛/回旋用于对空 |
| 远程 | 木弓现有配方：木材10，工作台 | 吹管改木材8+藤蔓1，工作台；手里剑作为副武器 | 商人阶段0常驻木箭，首包木箭100/种子100；阶段0不依赖阶段1军火商 |
| 魔法 | 火花魔棒前移阶段0：木材8+坠落之星1，工作台 | 紫晶杖前移阶段0：铜/锡锭6+紫晶2，砧；同等价配方不要一起误扣材料 | 首包魔力水晶1；基础回蓝药常驻低价；紫晶有十字路地面怪来源，不能必须先到水晶山峰 |
| 召唤 | 雀杖前移稳定制作：木材8+羽毛2，工作台 | 史莱姆杖制作：木材8+凝胶12；皮鞭前移阶段0：藤蔓2+木材6，工作台 | 阶段0额外仆从位来自低成本头部/配件；首包与配方不要求先杀格鲁兹之母 |

上述材料来源：木石来自常规地面怪与既有采集，凝胶来自十字路常规怪，羽毛来自反击蝇/格鲁兹，坠落之星来自反击蝇/阿司匹德，紫晶新增小爬虫/提克提克的阶段0副材料池（20%，1个），藤蔓来自苍绿苔藓怪（50%，1个）和首次探索材料包。阶段0即使不愿提前进入苍绿，也可用基础随机奖励及该职业另一条十字路制作线完成开荒；不能把所有替代都挂到同一个Boss。

基础武器首包目标价值相等，初始材料组合分别按正常路线3～5分钟可取得配置，不按泰拉币价格硬比。商店常驻基础四职业武器价拟定30吉欧/件，替代升级60吉欧/件，阶段0箭/种子100发≤5吉欧、回蓝药5瓶≤5吉欧；要用当地实际收入验证，不能沿用火花魔棒当前2500吉欧标价。即使商人未入住，基础随机奖励和工作台配方也能成立。

矮人项链拟新增阶段0稳定配方：木材6+丝绸1+凝胶4，工作台，仍提供+1仆从；现有巫医阶段2售卖可以保留但不能作为唯一来源。阶段0蛛网/便携织布途径须与此同批实现。首次基础资源补助统一含魔力水晶1个与少量通用材料，不按随机武器职业区别发放，不提供资源选择项；每玩家一次，换武器不再次领取永久增益。

### 4.3 难度、经济与后缀

现状不必掉概率在困难/专家/大师乘1.1/1.2/1.3，数量上限增加15%/30%/50%，吉欧乘1.25/1.5/2。拟保留这套难度奖励差，但对成品率在**类别总率**上乘：普通怪总成品率4%/4.4%/4.8%/5.2%，不能让每个候选各自放大一次；Boss固定一个主力槽不因大师多掉一包。材料先在普通难度校准第6.4节期望，再加难度倍率，必掉资源仍必掉。材料经济验收比较同难度旧版/新版，不拿普通旧产量比大师新版。

当前掉落代码还有自然前缀与Boss掉落安全位置处理；本方案不会把前缀伤害混进基础面板。战斗基准先用无前缀物品，再测可获得前缀上限。材料增产与成品减少后，售卖净收入不应靠刷同一弱怪超过同阶段正常探索路线的125%；超出时调整材料售卖价/地区权重，避免把产量再砍回去。吉欧现状不在本轮统一重算，若与商店目标价不匹配，应优先修价和可达性。

## 5. 每阶段四职业获取矩阵（拟定）

每格前2件为探索/制作线、后2件为Boss候选线；同阶段每职业4种，不用全部物品数量平均。所有名单均为现有 ItemType，阶段与来源按方案重定。它们构成最低保障清单，附录中的额外武器是可选侧向流派，不挤占这4个位置。Boss可以提供另一位Boss的同阶段候选作为主题替代，不能因此削掉该职业探索路线。

阶段0材料线见4.2；阶段1在真菌区使用孢子/藤蔓、金铂与宝石，阶段2使用城市/水晶/水道材料，阶段3使用狱石/蜘蛛/黑曜石，阶段4用硬模式矿/叶绿/蜂蜡，阶段5用终局素材。每阶段探索线配方所需工作站及材料上限不高于当前阶段；阶段2不能用阶段3狱石强迫先去下一阶段。
'''

MINERALS = [
 # ids, vanilla role, target, location, rule, final count, station
 ('CopperOre TinOre','肉前入门矿',0,'山道/十字路地面怪','主矿池60%，铜锡二选一','3～6','3矿→1锭；熔炉0'),
 ('IronOre LeadOre','肉前入门功能矿',0,'十字路躯壳、守卫','主矿池45%，铁铅二选一','3～5','3矿→1锭；熔炉0'),
 ('SilverOre','肉前早期矿',0,'十字路较强躯壳；精英补给','主矿池30%','4～6','4矿→1锭；熔炉0'),
 ('TungstenOre GoldOre PlatinumOre','肉前较高档矿',1,'真菌区/悬崖岩质怪；水晶区继续补给','主矿池50%，三种等权','4～8','均4矿→1锭；熔炉1'),
 ('DemoniteOre CrimtaneOre','原版邪恶Boss矿',2,'城市/水道精英与粪虫/吸虫Boss','普通主来源35%；精英必给6～9；Boss18～30','3～6','均3矿→1锭；熔炉2；不是阶段1无限供应'),
 ('Meteorite','原版肉前事件矿',2,'水晶激光怪/灵魂施法怪','地区主矿池35%','3～6','3矿→1锭；熔炉2；陨石套/太空枪同阶段校准'),
 ('Hellstone','原版肉前末期矿',3,'深渊上层/格林亲族大师以上的明确火焰源','主矿池45%，禁止苍绿新手亲族提前给','3～6','3矿+黑曜石1→1锭；地狱熔炉3'),
 ('CobaltOre PalladiumOre','原版肉后第一档',4,'边缘岩质强怪、斗兽场完整波次','主矿池55%，钴钯二选一','3～6','均3矿→1锭；熔炉4'),
 ('MythrilOre OrichalcumOre','原版肉后第二档',4,'阶段4精英/完整试炼；击败任一阶段4正式Boss后开放','与第一档同池，池内权重30%','4～8','均4矿→1锭；熔炉4；解锁阶段4砧'),
 ('AdamantiteOre TitaniumOre','原版肉后第三档',4,'完成阶段4正式Boss后，边缘强敌/试炼','同池权重20%；第一档50%、第二档30%、第三档20%','4～8','均4矿→1锭；精金熔炉4；先给可达的熔炉制作线'),
 ('ChlorophyteOre','原版机械Boss后地下丛林矿',4,'苍绿/真菌/花园的成熟植物、苔藓和螳螂源','阶段4且击败任一阶段4正式Boss后，丛林主材料池25%；旧孢子/藤蔓仍保留','2～4','5矿→1锭；精金熔炉4；全套配方成本同时降至可接受周期'),
 ('LunarOre','原版月亮领主掉落矿',5,'辐光通关奖；通关后神居高阶完整挑战奖励','通关必掉68～88；重复挑战给同阶段等价素材','Boss68～88','4矿→1锭；远古操纵机5；月亮领主档装备为通关后侧向奖励'),
 ('Obsidian','原版水熔岩接触产物',2,'阶段2水道/水晶岩质怪少量；阶段3盆地/深渊大量','阶段2副材料30%1～2；阶段3副材料50%1～3、精英必掉2～4','见概率列','阶段2黑曜石召唤套；阶段3另需狱石才可冶炼狱石锭；旧阶段1来源删除'),
 ('JungleSpores','原版地下丛林肉前材料',0,'苍绿苔藓怪；真菌区真菌怪','主材料62.5%；阶段4后保留在基础材料子池','1～2','阶段0工作台/砧基础线；高级丛林配方阶段1，不能因孢子提前直接造高档装备'),
 ('Vine','原版食人怪肉前材料',0,'苍绿苔藓怪/苔藓骑士；花园补给','主来源50%；精英75%','1','皮鞭/吹管基础线；丛林套配方阶段1'),
 ('Stinger','原版丛林毒刺材料',1,'真菌/螳螂/有毒植物；蜂巢作为后续来源','主来源62.5%','1～2','阶段1荆棘/丛林路线；不是所有昆虫都掉'),
 ('BeeWax','原版蜂后肉前召唤材料',4,'蜂巢士兵/守卫/蜂巢幼蜂；蜂巢骑士','常规62.5%，1～3；守卫必掉3～5；Boss必掉12～18','见概率列','蜜蜂套砧4；移除盆地/深巢无关来源'),
 ('ShadowScale TissueSample','原版邪恶Boss组织',2,'对应城市/水道首领；常规感染精英补充','精英50%1～2；Boss必掉8～12','见概率列','同阶段盔甲/武器；不可只给矿不供组织'),
 ('Bone','原版地牢肉前材料',2,'城市哨兵/水道/失败冠军','主材料50%','2～4','死灵套/骨系；不要求神居'),
 ('SturdyFossil','原版地下沙漠提炼材料',1,'悬崖/真菌岩质怪与地区探索包','主来源40%；地区精英75%','1～2','化石远程套阶段1；不要求本模组未具备的完整沙漠提炼流程'),
 ('FlinxFur','原版雪怪肉前召唤材料',1,'真菌区跳跃怪/蘑菇战士；地区材料包','主来源50%；精英75%','1～2','雪怪杖/外套阶段1；主题移植须在图鉴说明，不宣称原版丛林会掉雪怪毛'),
 ('SpiderFang','原版肉后蜘蛛材料',3,'深巢蜘蛛系；诺斯克材料包','主来源50%；精英75%','1～2','蜘蛛甲/杖阶段3；上调或下调数值按新阶段而非原版肉后定位'),
 ('Cobweb','原版早期地下材料',0,'十字路少量、深巢大量；采集/探索包备选','十字路30%1～2；深巢75%2～4','见概率列','7蛛网→1丝绸；阶段0便携织布线，阶段1完整织布机'),
 ('Silk','原版蛛网加工材料',0,'蛛网加工为主；十字路探索包补给','不强行让5种怪直掉丝绸','加工','织布链必须前移，不只改物品Stage'),
 ('SoulofLight SoulofNight','原版肉后地下群系魂',4,'阶段4雾谷光系/深渊暗系正式敌人；正式Boss等价补给','符合地区普通怪30%，不得全世界任意怪给','1','阶段4配方；召唤幼体不给魂'),
 ('SoulofMight SoulofSight SoulofFright HallowedBar','原版机械Boss奖励',4,'阶段4首领职业材料包；完整斗兽试炼','每场从四种材料等权随机给一种，魂6～10或神圣锭8～12','Boss专用','阶段4砧；阶段4普通斗兽怪删独立机械魂'),
 ('CrystalShard','原版肉后神圣地下晶体',4,'阶段4开放的水晶山峰成熟晶体怪','主材料45%','1～3','水晶风暴/弹药；阶段2普通水晶材料可用宝石替代，不能提前给硬模式成品'),
 ('SpookyWood Ectoplasm BeetleHusk','原版世纪之花后/石巨人后材料',5,'剧团/梦境/神居正式挑战对应主题','精英40%1～2；Boss必掉8～12','见概率列','阶段5配方；不可在早期亲族掉终局木料'),
 ('FragmentSolar FragmentVortex FragmentNebula FragmentStardust','原版四柱终局材料',5,'阶段5高阶挑战随机职业碎片；辐光额外四系','每场四系各25%随机一种10～16；辐光每系10～20','Boss专用','远古操纵机5；月系终极配方另需通关矿，普通阶段5主力不依赖它'),
 ('HkPaleOre','空洞原版骨钉关键资源，非泰拉矿',0,'保持原版关键物品路线','不加入普通矿池，不复制、不转普通锭','原版','仅原有骨钉升级流程；不得误用名字Ore批量替换'),
]

RESOURCE_TEXT = r'''
## 6. 矿物与地区材料的动态进度（拟定）

### 6.1 资格与产出单位

`允许掉落 = 可获得奖励的敌人 × 已到达该地区 × 资源最低阶段 × 对应能力门槛 × 地区/事件解锁条件`。表中额外Boss条件是新增设计条件，当前代码没有统一实现。首次Boss奖励可使用本次战后新解锁资格；普通怪不能借此获得尚未完成的Boss资格。

每只怪只从当地有生态意义的主矿/主材料池取一项；所有表中矿量都是**最终掉到地上的矿石数**。取消普通矿石隐式×2。熔炼比沿用矿物表，不沿用原版世界生成/祭坛/采掘前提；本模组以敌人掉落供矿时，以这里的能力与Boss条件替代世界状态，不能要求本模组不存在的三机械Boss或祭坛。

高阶阶段4矿子池在阶段4且完成至少一次正式Boss胜利后开放，胜利来自帝王之翼门槛残破容器或其后的阶段4正式Boss；不能拿阶段0假骑士胜利抵数。残破容器战前用阶段3装备，胜利后取得翼，算阶段4入口完成；通过跳跃路线提前到蜂巢的玩家仍须满足阶段/能力门槛。保证不使用叶绿/高阶矿也有可完成入口的路线，不能要求叶绿装备去解锁叶绿。

以下材料表对所有来源生效；未列出的专属掉落材料采用其源码阶段、生态区域和同类产率，详见完整材料审计。已有必掉材料不因通用封顶被削成概率掉落。草药/建材不应把矿物池挤空，矿物也不能覆盖所有老配方材料。

数量列是**新增来源的基础最终量**，不是把所有旧矿石堆叠强制覆盖成小堆；既有合规来源必须按6.4做产量校准，取能实现旧版每击杀制作等价量增加15%～25%的数量。否则取消旧×2又把所有行改成3～6矿，会造成大幅减产，违背本次目标。
'''

RESOURCE_END = r'''
### 6.2 丛林/花园的完整例子

| 玩家进度 | 苍绿/真菌/花园的植物材料池 | 尚不允许的材料 | 老材料处理 |
|---|---|---|---|
| 阶段0 | 孢子、藤蔓、木材、低阶草药；紫晶少量副来源 | 毒刺的阶段1主供、蜂蜡、叶绿、生命果 | 基础配方可用，丛林套仍需阶段1工作站/配方门槛 |
| 阶段1 | 加入毒刺，完整丛林/荆棘路线；金铂钨由岩质怪供给 | 叶绿、蜂蜡、肉后魂 | 孢子藤蔓保持供给，不能升级后断货 |
| 阶段2 | 植物材料不自动变成魔矿；去城市/水道取邪恶矿与骨 | 叶绿、蜂蜡、硬模式矿 | 同区保留旧材料，增加稀有草药/对应副料 |
| 阶段3 | 花园成熟材料、藤蔓/孢子更大堆叠；黑曜石/狱石仍去深渊火焰源 | 叶绿、高阶硬模式矿 | 升级取堆叠，不把所有植物改掉矿石 |
| 阶段4未完成入口Boss | 保留基础植物池；进入蜂巢才可得蜂蜡 | 叶绿、第二/三档矿 | 第一档硬模式矿由边缘岩质怪供给 |
| 阶段4完成入口Boss后 | 主材料池75%旧植物子池、25%叶绿（2～4矿）；主池总触发75% | 夜明矿、终局碎片 | 叶绿实际每击杀18.75%，平均0.5625矿；另保留副材料池，旧材料不断供 |
| 阶段5 | 同一池继续供叶绿和旧植物；终局材料从终局活动供给 | 普通植物不能凭阶段自动产夜明矿 | 可降低满配后基础材料权重，提供材料筛选与自动存箱 |

叶绿估算：单块锭需5矿，按上述主要来源约8.89次击杀/锭；若全套仍需原版几十块锭，会过度刷怪。因此拟定叶绿套头/身/腿为6/10/8锭（总24锭=120矿），并提供每次阶段4主题Boss材料包12～18矿；常规怪只做沿途补充。它是阶段4后半的专精套，不作为阶段4入门必做。优先保证第一把升级武器在约20～35次当地有效击杀或一次Boss材料奖后可制作；若达不到，降低配方需求/提高目标材料供给，而非提高成品武器掉率。

### 6.3 熔炉与替代矿的闭环

- 铜锡/铁铅、金铂、钴钯等配方提供等价替代，但同一次制作只消费玩家选定的材料，不把两条并列配方合并成双倍需求。
- 阶段0可得工作台/熔炉/砧；阶段1丛林制作线无需阶段2城市工作站。阶段3地狱熔炉从地区探索或低价制作稳定提供；阶段4精金熔炉/砧按已取得矿与现有炉升级，不能再要求只有该炉才能炼的锭来造自己。
- 精金熔炉拟定升级：阶段4入口完成后，地狱熔炉1+精金矿或钛金矿12+石块20；材料均可在未有精金熔炉时从怪物取得。阶段4砧拟定铁/铅砧1+秘银/山铜锭6，先在普通熔炉炼第二档矿。
- 铜锡铁铅银阶段0、钨金铂阶段1、魔矿猩红陨石/少量黑曜石阶段2、狱石/大量黑曜石阶段3、硬模式矿和叶绿阶段4、夜明阶段5通关后；**物品Stage、掉落资格、商店、宝箱/钓鱼匣、熔炼配方、装备配方、图鉴显示必须一致**。只修敌人掉落仍会被宝箱/匣/商店绕过。
- 原版有地下丛林、邪恶地、神圣地等前提，这里采用圣巢对应生态区域，不把所有“绿色怪”都视为丛林矿源。蜂巢独立供应蜂蜡；王后花园供应植物/叶绿；深渊供应火焰/暗系；水晶山峰供应宝石/后期晶体。

### 6.4 矿物池合并后的产量校准（不能漏做）

对一个已有的、地区和阶段均合规的矿物族，先从当前**已经转矿和翻倍后的实际行**求每次击杀期望：`E旧 = Σ(行概率 × 平均最终矿数 / 每锭矿数)`，单位为锭等价。先合并同物品/替代矿族，再定池总概率 `p新`，新平均每次成功矿数满足 `p新 × Σ(矿种条件权重 × 平均矿数/每锭矿数) = 1.20 × E旧`；允许范围1.15～1.25。不能用原始C/D行的“锭数量”再算一次×2。

明确例子：小爬虫当前铜矿、锡矿各1/3概率，每次6～12矿。两者合计期望6矿/击杀=2锭等价。拟改一个60%铜锡池，铜锡各50%，成功给10～14矿，期望7.2矿/击杀=2.4锭等价，恰好增加20%；不是给3～6矿（那会减产55%）。熔炼仍3矿/锭，最终实际掉落不再隐式翻倍。困难/专家/大师另按4.3从同难度基线比较。

普通精英和Boss的既有合法矿物也做相同期望比较，不能把原本几十矿的保底改成数枚。因阶段或生态错误而删除的越阶来源不予原地补回同一种后期矿；用该地区当前材料代替，并在新指定的合法区域增加正确材料来源。对这些迁移项，比较“该职业完成一件武器/一套甲所需的有效探索时间”，而不是强行维持旧版提前量产后期矿的错误收益。

产率对齐后再校准配方成本；如果保留旧高矿量会导致每阶段制作过快，应在文档里明确重排材料用途/配方，而不能暗中减矿。所有现有来源的最终概率和堆叠需要在实现时从该公式展开输出一份新数据表，本次仍属于设计稿。

## 7. 全部Boss的职业奖励与攻击方式

表中四个职业候选各对应25%的随机职业权重，都是现有物品；首次与重复主力槽按4.1自动随机掉落。饰品是主题功能槽的候选，若为职业专用，则同槽必须配置另外三职业的等价饰品（同阶段进攻/续航预算），并按四职业等权随机；不再展示选职业或领奖菜单。

Boss前期战斗不能依赖自己的战后奖。螳螂领主战前按阶段1、战后按阶段2；首次大黄蜂战前阶段0、战后1；残破容器战前阶段3、战后4。它们的战斗数值也应按可用装备验证，但本次只记录拟调整方向，不改Boss生命/攻击实现。区域支线胡长老/泽若/马尔穆/叛徒领主按实际可达路线重分配，与现有源码阶段的差异是明确拟修改项。
'''

END = r'''
## 8. 实现与验收清单

1. 建立唯一的阶段/地区资源策略表，消除 `C/D/G` 门槛差异；完成矿→锭→工作站→武器/套装的依赖图，检测未来材料依赖、资源无来源、自造熔炉循环。
2. 先落地四职业阶段0稳定获取、弹药/法力/仆从配套，再降低成品率，避免先削掉率加重开荒。移除原表多余独立武器行、必掉混合组，材料按地区池合并。
3. 重做Boss随机装备池、首次/重复记录和多人个人状态，沿用击杀后自动掉落，不新增领取选择界面。先按四职业各25%抽取，再类内抽装备；不以当前手持武器、造成伤害最多者或已拥有装备改变概率。
4. 重定级武器/护甲/饰品，尤其蜂巢全组、早期宝石杖/鞭与配方；同步Tooltip、图鉴、商店和宝箱/钓鱼等旁路。使用同一表生成文档和游戏配置时，仍保留“现状/拟定”标签直到实装验证完成。
5. 数据验收：6阶段×4职业各2探索+2Boss候选，全部已定义且Class正确；每阶段每职业2条不共用同一个Boss的基础获取线；每材料至少2条可达来源；每个正式Boss四职业有效奖齐全，简易/精英例外单列；每条矿石来源的地区/阶段/额外门槛符合第6节。
6. 概率验收：固定种子独立统计每地区/阶段/难度10万次有效击杀，按职业检查约25%权重与类内等权，对比理论总率和每种材料期望，而不是只数全部武器；区分概率包、无候选、首通、重复、召唤物/无敌怪。置信区间核查极端偏差；成品独立行与组不能双重结算。
7. 四职业各从新隔离存档完整走阶段0～5；记录第一把/第二把武器时间、同阶段套装成本、5分钟资源净收入、Boss用时与死亡数。阶段0首20分钟四职业可用武器≥2种打法，至少1条不依赖Boss掉落；不允许借用别职业主力才打得过。
8. 战斗验证使用实际全套配装、同阶段生命、普通与大师等四难度；含地面/对空/移动/多目标/短窗口Boss。比较60秒含弹药/回蓝与召唤追敌的总输出。四职业同路线Boss用时目标距中位数≤15%，早期获取时间差≤20%；偏差出现时调整该职业预算和来源。
9. 矿物动态测试至少覆盖：阶段0苍绿只出孢子藤蔓；阶段1毒刺；阶段3无叶绿/蜂蜡泄漏；阶段4入口前后叶绿资格切换；回旧区仍出低阶材料；能力跳序、Boss胜利发能力帧、存档重载、背包满、多人不共享首通次数。改存档/库存的测试必须使用隔离状态。

**本次文档检查不代表上述游戏测试通过。** 召唤AI真实命中率、全武器特殊机制、最终逐武器面板和Boss通关时间仍需在实现阶段测定；本方案提供完整范围、初始预算与验收门槛，不能把预算区间当实测结论。原版阶段在此作为设计参照；未联网逐页复核原版Wiki，不把新增设计值描述成原版事实。

## 9. 实现前完整基线与逐项迁移审计

以下附录是实现前的冻结对照，不能当作当前游戏掉率；它逐条列出旧版所有敌人掉落。新版实际阶段、数值、概率和来源以第11节及其运行时JSON为准。原型、实验、圣物、永久增益等仍可出现在旧基线中，不因为名字带武器、Ore或Defense就自动进职业/矿物池。

### 9.1 附录的迁移规则

- 核心矩阵与Boss表中明确重定阶段的物品优先按前文处理；同一物品只有一个固定阶段，不在获得时动态缩放旧装备。同物品若用于低阶段和高阶段，必须改高阶段候选或新增独立变体，禁止静默升级已持有低阶装备。
- 额外武器按现有Stage作为暂定侧向阶段，以本阶段同职业总DPS/用途预算重算伤害和衍生效果；不能仅凭稀有度/原版肉前肉后判断强度。特殊武器逐一标记复核，不自动给一个伪精确的新面板数。
- 盔甲按整套家族、头盔分支和职业重排，按3.2的整套预算分配，推荐头/身/腿约30%/40%/30%，套装额外防御计入总额；通用甲作为入门防具，不能占四职业每阶段专精配额。单件比例取整后校正使总和一致。
- 饰品保留操作用途，按3.4控制数值、冷却、同源叠加，职业专属进该职业功能候选，通用信息/移动放通用池。炮台套、悠悠球/鞭专精等不按通用Class随意改伤害。
- 普通材料优先第6节指定的新阶段与区域；矿物之外材料以当前Stage为暂定门槛，逐来源核对生态，禁止为凑5个怪而跨区分发。永久生命/魔力/幸运等增益不使用“材料×1.25”，须独立限制；工具/站点不作为有效战斗装备。

'''

def build(runtime_path=None):
    verified = check_sources()
    source = read('Items/EnemyDropData.cs')
    enemies = []
    for m in re.finditer(r'\["([^"]+)"\]\s*= new Entry \{ (.*?)Drops = new\[\] \{', source):
        b = m[2]
        e = {'key':m[1], 'name':re.search(r'Zh = "([^"]+)"', b)[1],
             'stage':int(re.search(r'Stage = (\d+)',b)[1]), 'boss':'Boss = true' in b,
             'geo':int(re.search(r'Geo = (-?\d+)',b)[1]),
             'kind':re.search(r'Kind = "([^"]+)"',b)[1],
             'scene':re.search(r'Scene = "([^"]+)"',b)[1]}
        enemies.append(e)
    assert {e['key'] for e in enemies} == set(DATA['drops'])
    bykey = {e['key']:e for e in enemies}
    assert {b[0] for b in BOSSES} == {e['key'] for e in enemies if e['boss']}
    planned = {}
    for stage,row in enumerate(CORE):
        for cls,types in zip(CLASSES,row):
            assert len(types)==len(set(types))==4
            for t in types:
                assert ITEMS[t]['Class']==cls, (t,cls,ITEMS[t]['Class'])
                assert ITEMS[t]['MaxStack']==1 and not ITEMS[t]['PickPower'], t
                assert t not in planned, t
                planned[t]=stage
    assert len(ARMOR_PATHS)==24
    assert Counter((s,c) for s,c,*_ in ARMOR_PATHS)==Counter((s,c) for s in range(6) for c in CLASSES)
    armor_stages = {}
    for s,c,ts,*_ in ARMOR_PATHS:
        for t in ts.split():
            name(t)
            # Shared starter wood pieces stay stage 0 when used in the stage-1 mixed set.
            armor_stages[t]=min(s,armor_stages.get(t,s))
    # Entrance boss has an explicit post-victory reward stage.
    for key,stage,reason,types,acc in BOSSES:
        name(acc)
        for cls,t in zip(CLASSES,types.split()):
            assert ITEMS[t]['Class']==cls, (key,t,cls)
            if key=='InfectedKnight':
                continue  # its entry upgrade is an explicit stage-3 item reward, not a new stage for the same item
            assert planned[t]==stage, (key,t,planned[t],stage)
    out = [INTRO]
    def add(s=''): out.append(s)
    def table(headers,rows):
        add('| '+' | '.join(headers)+' |')
        add('|'+'|'.join(['---']*len(headers))+'|')
        for row in rows:
            add('| '+' | '.join(str(s).replace('|','&#124;').replace('\n','<br>') for s in row)+' |')
        add()
    table(['阶段','职业','探索/制作1','探索/制作2','Boss候选1','Boss候选2'],
          [(s,CN[c],*[name(t) for t in ts]) for s,row in enumerate(CORE) for c,ts in zip(CLASSES,row)])
    add('探索线拟定成本上限（主材料等价份数，不是已实现配方）：阶段0/1/2/3/4/5分别为6/8/10/12/14/16份，并附当地副材料1～4份。实际每种配方在实现时展开成明确物品ID、数量、工作站与阶段；本轮已明确阶段0和蜜蜂套、叶绿套、矿物熔炼链的具体配方。其余配方不得直接声称已调整。')
    add('### 5.1 四职业主力防具路线')
    add('下表是24组可达配装目标。阶段0通用甲与阶段1雪怪混搭不假装有不存在的职业全套奖励；同ID部件在各配装中数值固定。防御为整套有效目标；共享部件/头盔分支的差额用头盔或已匹配套装效果实现，不动态缩放背包旧物品。阶段0木甲同时用于法师/召唤，目标都为8；阶段1雪怪外套需把胸甲防御设为能与固定木头腿合计14的值。')
    table(['阶段','职业','主力配装（现有ID）','拟来源/配方迁移','整套防御与效果目标'],
          [(s,CN[c],'、'.join(name(t) for t in ts.split()),mat,stat) for s,c,ts,mat,stat in ARMOR_PATHS])
    add('### 5.2 会造成矿物循环依赖的武器配方，必须同时改')
    add('例如阶段2熔火之怒不能沿用狱石阶段3配方：拟改魔矿/猩红锭8+骨6+木材10，砧2；阶段2小鬼法杖拟改陨石锭8+坠落之星3+骨4，砧2；阶段2永夜刃不能要求阶段3炽焰巨剑作原料，拟改村正1+魔矿/猩红锭8+暗影鳞片/组织样本6，砧2。这里改变的是本模组配方和强度，物品原版名字保留；其余核心武器也需展开同类依赖检查。阶段0燧发枪移到常驻商人/首通随机池，并让火枪子弹100发≤5吉欧、阶段0可买；不能领到枪却必须等阶段1军火商才有子弹。')
    add('### 5.3 效果和饰品的职业配额如何落实')
    add('各阶段每职业保障1件进攻、1件续航/防御方向；同一件通用饰品可给多个职业使用，但不重复计作四件职业专精。近战用野性爪→猛爪/手套→战士徽章→机械/烈火手套方向；远程用淬毒刺→箭袋→游侠徽章→狙击/熔火箭袋方向；魔法用星力手环→魔力再生/魔力花→手铐/术士徽章→天界组合方向；召唤用前移阶段0矮人项链→阶段2银护腕→阶段3召唤师徽章→阶段4鞭标记专精→阶段5莎草纸方向。新增/前移效果按3.4分档；低阶饰品不因玩家阶段自动升级。功能槽同时提供护体/机动等价选项；同件职业效果只在自身分支生效。')
    add(RESOURCE_TEXT)
    table(['资源ID','原版定位','新最低阶段','地区/来源','拟定概率与额外门槛','新增来源基础最终量*','熔炼/配方闭环'],
          [( '、'.join(name(t) for t in ts.split()),van,s,loc,rule,count,station) for ts,van,s,loc,rule,count,station in MINERALS])
    add(RESOURCE_END)
    table(['Boss/记录','现状→拟奖励阶段','攻击方式与奖品用途','近战','远程','魔法','召唤','功能槽主题'],
          [(f'{bykey[k]["name"]} `{k}`',f'{bykey[k]["stage"]}→{s}' + ('（战后入4；当前列为阶段3过渡奖）' if k=='InfectedKnight' else ''),reason,
            *[('—（无主力包）' if k=='Zote' else name(t)) for t in ts.split()],name(acc)) for k,s,reason,ts,acc in BOSSES])
    add('残破容器例外的具体结算：武器类别随机给表中阶段3成熟武器；护甲类别随机给对应职业阶段4入门甲部位；功能槽按固定随机池结算，并额外给阶段4入口材料。不会把表中同ID武器改为阶段4，也不提供已有装备换材料的领奖选项。因此战后奖励包含入口进展，但不把阶段4后半装备无条件塞给门槛Boss。')
    add(END)
    # Exact counts and observed class distribution, without calling stackables weapons.
    dropped = {d['Type'] for ds in DATA['drops'].values() for d in ds}
    weapons = {t for t in dropped if ITEMS[t]['Damage']>0 and ITEMS[t]['MaxStack']==1 and not ITEMS[t]['PickPower'] and ITEMS[t]['Class'] in CLASSES}
    add('### 9.2 当前掉落武器数量：按职业分开统计')
    add('这里只统计掉落表中出现、可重复使用、非镐工具的有伤害物品；可堆叠投掷品/弹药单列于物品附录，不混进该统计。该口径不代表已经逐一验证所有武器机制。')
    table(['当前Stage',*[CN[c] for c in CLASSES]],[(s,*[sum(ITEMS[t]['Stage']==s and ITEMS[t]['Class']==c for t in weapons) for c in CLASSES]) for s in range(6)])
    add('### 9.3 高概率独立武器行专项清理')
    add('以下为现状独立掉率≥25%的非堆叠武器行；必掉行另列在完整敌人附录。Boss全部并入职业包；普通怪/精英按整类总率2%/6%，不保留每行这个概率。')
    table(['敌人','武器','现状独立概率','拟迁移'],
          [(f'{e["name"]} `{e["key"]}`',name(d['Type']),f'{d["Num"]/d["Den"]:.2%}',
            '并入唯一职业包' if e['boss'] else '删除独立高率行，进地区总率池')
           for e in enemies for d in DATA['drops'][e['key']] if d['Type'] in weapons and d['Group']==0 and 1<d['Den'] and d['Num']/d['Den']>=.25])
    add('### 9.4 所有掉落物品：当前阶段、属性与迁移')
    add('每件列出现状数值、所有当前来源、拟定阶段或统一迁移规则。伤害是当前面板，不是3.1中的实战DPS；装备新数值以第3节预算和实测决定。`锁N` 是当前这一行的阶段门槛，`组N` 是现状组号，组内单件概率须除以合格候选数；同组各行不是各自必掉。')
    uses=defaultdict(list)
    for e in enemies:
        for d in DATA['drops'][e['key']]: uses[d['Type']].append((e,d))
    resource_stages={t:s for ts,_,s,*_ in MINERALS for t in ts.split()}
    # A listed ore's corresponding bar shares the proposed processing stage.
    for m in re.finditer(r'new OreInfo\(ItemType\.(\w+), "[^"]+", \d+, ItemType\.(\w+)', read('Items/ItemsOres.cs')):
        if m[1] in resource_stages: resource_stages[m[2]]=resource_stages[m[1]]
    planned.update(armor_stages)
    for stage,row in enumerate(ACCESSORIES):
        for ts in row:
            for t in ts: planned[t]=min(stage,planned.get(t,stage))
    for t in ('BeeHeadgear','BeeBreastplate','BeeGreaves'): planned[t]=4
    planned['PygmyNecklace']=0
    for t in ('AmethystStaff','WandofSparking','LeatherWhip'): planned[t]=0
    def rowtext(d):
        qty=str(d['Min']) if d['Min']==d['Max'] else f'{d["Min"]}～{d["Max"]}'
        rate=f'组{d["Group"]}({d["Num"]/d["Den"]:.3g})' if d['Group'] else f'{d["Num"]/d["Den"]:.3%}'
        return f'{rate}×{qty}/锁{d["LockStage"]}'+('/大师' if d.get('Master') else '')
    def category(t):
        d=ITEMS[t]
        if t.startswith('Hk'): return '原版关键物品'
        if t.startswith('Relic'): return '圣物'
        if d['PickPower']>0: return '工具'
        if t in weapons: return CN[d['Class']]+'武器'
        if d['Damage']>0 and d['MaxStack']>1: return '弹药/投掷消耗'
        if d['Defense']>0 and d['Accessory']=='None': return '盔甲/防具'
        if d['Accessory']!='None': return '饰品'
        if d['Places']!='Empty' or d['PlacesStation']!='None' or d['PlacesWall']>0: return '材料/建造/站点'
        return '材料/消耗/其他'
    for cat in ['近战武器','远程武器','魔法武器','召唤武器','盔甲/防具','饰品','弹药/投掷消耗','材料/建造/站点','材料/消耗/其他','原版关键物品','圣物','工具']:
        ts=sorted([t for t in dropped if category(t)==cat],key=lambda t:(ITEMS[t]['Stage'],t))
        if not ts: continue
        add(f'#### {cat}（{len(ts)}件）')
        rows=[]
        for t in ts:
            d=ITEMS[t]
            target=planned.get(t,resource_stages.get(t,d['Stage']))
            if t in resource_stages: policy='按第6节地区/阶段/额外条件；材料池；移除跨区来源'
            elif t in planned: policy='核心/蜂系明确重定级；按第3～7节职业总预算'
            elif t in weapons: policy=f'阶段{target}{CN[d["Class"]]}侧向候选；防后DPS/机制重算；普通/精英总率2%/6%'
            elif cat=='盔甲/防具': policy='整套家族预算重分防御/效果；不按单件原版值保留'
            elif cat=='饰品': policy='第3.4节同阶段效果预算；通用/职业功能分池'
            elif cat=='圣物': policy='额外纪念奖；不计有效装备/材料增产'
            elif cat=='原版关键物品': policy='保留原版流程；不批量调率/复制'
            elif cat=='弹药/投掷消耗': policy='计入远程整场成本/DPS；不占常规耐用武器配额'
            elif cat=='工具': policy='工具获取/采掘门槛独立；不占战斗主力槽'
            else: policy='生态来源与材料池合并；永久增益/消耗品/站点独立审核'
            stats='、'.join(f'{label}{d[k]}' for k,label in [('Damage','伤害'),('Defense','防御'),('Mana','耗魔')] if d[k]) or '—'
            sources='；'.join(f'{e["name"]} `{e["key"]}` {rowtext(x)}' for e,x in uses[t])
            rows.append((name(t),f'{d["Stage"]}→{target}',stats,policy,sources))
        table(['物品','现状→拟阶段','当前主要数值','拟迁移/调值规则','完整当前来源（无省略）'],rows)
    add('### 9.5 全部敌人奖励迁移清单')
    add('每条记录都在本次核对范围。完整现状概率/数量在9.4按物品交叉查询。这里的来源类别为拟定分类：无限生成/被动/无敌禁止收益；正式Boss清单优先，其他不因HP单独判为精英。具体区域材料名单以第6节为准。')
    bosstarget={b[0]:b[1] for b in BOSSES}
    rows=[]
    for e in sorted(enemies,key=lambda e:(e['stage'],e['key'])):
        if e['key']=='Zote': policy='简单左特：纪念+少量材料，无主力包'
        elif e['key'] in ('BigBuzzer','MageKnight','PaleLurker'): policy='有名精英：首次四职业等权随机一件，重复普通精英12%总成品'
        elif e['boss']: policy='正式Boss：首通四职业等权随机主力+随机有效功能+材料，重复随机主力槽'
        elif e['kind'] in ('invulnerable','passive','non_enemy','player_shade'): policy='不供核心材料/成品；原版必要任务行为保留'
        elif e['kind']=='miniboss': policy='常规精英：6%武器+3%甲+3%饰品；地区主材料保底'
        elif e['kind']=='arena': policy='斗兽场波次普通怪：按普通总率4%；整场完成另发试炼奖，不能每波发Boss包'
        elif e['kind']=='conditional': policy='条件生成：仅正常世界独立重生个体进普通池；召唤幼体不给核心材料/首通计数'
        else: policy='普通总率4%；合并成1主材料/1副材料/至多1建材池，移除混合必掉组'
        target=bosstarget.get(e['key'],4 if e['scene'].startswith('Hive_') else e['stage'])
        rows.append((f'{e["name"]} `{e["key"]}`',e['stage'],e['scene'],e['kind'],e['geo'],target,policy))
    table(['敌人/变体','现状表Stage','场景','源码Kind','现状普通吉欧(-1沿用游戏)','拟Boss奖阶段/普通暂定表阶段','拟奖励规则'],rows)
    add('普通怪的“暂定表阶段”仅保留旧记录作迁移索引，不用于绕过第6节资源条件；同区怪的实际地图阶段应统一由 `Areas.LevelOf` 派生。蜂巢士兵/守卫等旧表Stage3一律按蜂巢地区4处理，苔藓等早期区域随玩家进度开放后期植物资源。')
    add('## 10. 本次文档核验与依据')
    add(f'实现前基线包含物品定义{len(ITEMS)}种、掉落记录{len(enemies)}条（Boss标记{len(BOSSES)}条）、旧运行时掉落行{sum(map(len,DATA["drops"].values()))}条、旧掉落物品{len(dropped)}种、耐用非镐武器{len(weapons)}种。核心矩阵6×4×4=96个位置，全部存在且职业一致；Boss表覆盖全部Boss标记记录（其中精英/简单例外单列）。')
    add(f'实装后仍有{len(verified)}个旧数据来源哈希与冻结基线相同，其余源码改动是本次改造或工作区既有改动，不能用旧快照证明当前DLL。当前实装数据与测试结果见第11节。ID存在、职业匹配、Boss记录覆盖和基线物品覆盖由 `python tools/build_drop_balance_doc.py` 自动检查。')
    add('运行时导出不是整份当前构建的运行证明：本轮核验期间，工作区的 `EnemyDrops.cs` 另有自然前缀/落点/旧区强度统计修改，其完整哈希与导出不同。掉落数据表、物品定义和静态转矿等32个相关数据来源仍一致；本轮另读当前Roll/概率/分组/锁阶段函数确认其上述现状规则，不据此声称当前全部代码已跑过。`Areas.StatLevel/CatchUp/LifeScale` 当前还会抬升旧区普通怪强度，回苍绿刷叶绿时必须按当前实际强度计获取时间，不能按开局弱怪HP估算；它不会自动赋予矿物资格。')
    for rel,why in [
        ('Items/EnemyDropData.cs','当前敌人、变体、原始掉落行'),('Items/EnemyDrops.cs','概率、分组、难度和领取'),
        ('Items/ItemsOres.cs','锭矿转换、隐式翻倍、矿物与熔炼链'),('Items/Items.cs','属性、职业Kit、蜜蜂套单件'),
        ('Items/ItemsGenerated.cs','阶段和来源覆盖'),('Items/Equipment.cs','套装效果、装备增益、弹药与暴击'),
        ('Items/ArmorEffects.cs','动态护甲效果'),('Items/AccessoryEffects.cs','饰品触发机制'),
        ('Items/RecipeData.cs','基础武器、蜜蜂套等配方'),('Items/RecipeSupplementGenerated.cs','新增合成装备旁路'),
        ('Items/TownShopData.cs','NPC售卖与阶段'),('Items/Shop.cs','能力计数与商店'),
        ('World/Areas.cs','实际区域阶段、防御与伤害'),('Items/BossStats.cs','Boss阶段/生命基准'),
        ('Items/BossBalance.cs','Boss攻击风格与数值差异'),('Items/BossPhases.cs','多阶段血池'),
        ('Player/TerraStats.cs','玩家生命、防御与受击'),('World/GameDifficulty.cs','难度倍率'),
        ('DevAutoTestBalance.cs','现有测试的覆盖范围与遗漏')]:
        add(f'- [{rel}](src/TerraKnight/{rel})：{why}。')
    add('- [本地运行时元数据导出](tools/encyclopedia_runtime.json)：只用与本次相关源码一致的字段作现状依据，未运行新平衡数值。')
    add('- [本文件生成器](tools/build_drop_balance_doc.py)：保存方案文本、核心矩阵、Boss与矿物映射，输出本文；不会修改游戏源码。')
    add('## 11. 当前实装数据与验证')
    if runtime_path is None:
        runtime_path=ROOT/'artifacts/drop-balance-validation/loot-balance-runtime.json'
    runtime_path=Path(runtime_path)
    if not runtime_path.exists():
        add('当前尚未提供完整通过的实装运行时导出；不能仅凭方案或构建判定平衡已完成。')
    else:
        live=json.loads(runtime_path.read_text(encoding='utf-8-sig'))
        summary=live['summary']
        liveitems={d['Type']:d for d in live['items']}
        enumids={d['TypeId']:d['Type'] for d in live['items']}
        typename=lambda d: d['Type'] if isinstance(d['Type'],str) else enumids[d['Type']]
        def marginal(rows,d):
            chance=d['Num']/d['Den']
            if d['Group']<=0: return chance
            weight=lambda r: r.get('Weight',0) or 1
            return chance*weight(d)/sum(weight(x) for x in rows if x['Group']==d['Group'])
        add(f'游戏内隔离测试：**{summary["Passed"]}/{summary["Checks"]}项通过**。该结论覆盖下面列出的数据/抽样/固定地面目标出手，不代表全Boss通关体验已经验证。')
        if summary['Failures']: add('未通过：'+'；'.join(summary['Failures']))
        add(f'实测DLL SHA-256：`{live.get("pluginSha256","未导出")}`。')
        try: relative=runtime_path.relative_to(ROOT).as_posix()
        except ValueError: relative=runtime_path.as_posix()
        add(f'[完整游戏运行时数据与结果]({relative})。测试使用隔离存档，环境与玩家存档核对结果见同批测试的 `result.json`。')
        add('### 11.1 已接入的规则')
        add('四职业随机Boss主力池；首次/重复奖励差异；稀有成品总率；关键饰品普通8%/精英20%及直接制作；所有原始行统一阶段资格；地区矿物进度与额外Boss门槛；最终矿量校准；前期四职业武器与商人常驻供应；24组防具与蜂套44防御/24%召唤增伤；按实际多段/弹丸/召唤与耗魔校准的初始属性；配方、附近工作站、商店、钓鱼匣的阶段判定；加权图鉴概率。')
        add('普通怪的装备与基础饰品随玩家已解锁阶段更新，材料仍受地区生态限制；旧Boss固定发自身阶段装备和材料，不能靠重刷早期Boss兑换终局资源。基础饰品两条来源加稳定配方用于降低随机卡职业的风险，Boss仍自动随机，不提供职业选择界面。')
        add('原有非核心流派装备已分散补回各阶段可选稀有池，候选增加不会提高稀有成品总率；升级饰品与对应低级件同时佩戴时抑制低级件，避免同一能力重复堆叠。材料按普通难度旧期望×1.2校准，并另加难度奖励；高难度的新旧数量分布不同，不能据此宣称每个材料在每种难度都恰好增加20%。图鉴显示实际难度下随机数量的上下限。')
        add('实机测试发现利刃台风的大弹丸在骑士身体中心生成时可能卡入邻近地面/墙边，现于首次生成时寻找邻近开放位置并检查路径；飞行后的反弹碰撞仍保留。此修复来自实际零命中失败及弹丸位置日志，未将失败测试改成通过。')
        add('### 11.2 四职业武器当前面板（游戏导出）')
        def weapon(t):
            d=liveitems[t]
            return f'{name(t)}：伤害{d["Damage"]}，耗魔{d["Mana"]}，使用{d["UseTime"]*60:.1f}帧'
        table(['阶段','职业','探索/制作1','探索/制作2','Boss候选1','Boss候选2'],
              [(s,CN[c],*[weapon(t) for t in ts]) for s,row in enumerate(CORE) for c,ts in zip(CLASSES,row)])
        add('### 11.3 关键饰品的完整普通怪来源与制作')
        actualrecipes=defaultdict(list)
        for r in live['recipes']: actualrecipes[r['Result']].append(r)
        for s,row in enumerate(ACCESSORIES):
            add(f'#### 阶段{s}')
            rows=[]
            for t in dict.fromkeys(t for ts in row for t in ts):
                sources=[]
                for key,ds in live['stage'+str(s)].items():
                    if live.get('sourceLevels',{}).get(key,6)>s: continue;
                    matches=[d for d in ds if typename(d)==t and d['Group']==103]
                    if matches:
                        rate=sum(marginal(ds,d) for d in matches)
                        sources.append(f'{bykey[key]["name"]} `{key}` {rate:.1%}')
                recipes=actualrecipes[t]
                description='；'.join(f'{r["Station"]}（阶段{r["StationStage"]}）：'+ '+'.join(name(i['Type'])+'×'+str(i['Count']) for i in r['Ingredients']) for r in recipes)
                rows.append((name(t),liveitems[t]['Stage'],len(sources),'；'.join(sources),description))
            table(['关键饰品','实际最低阶段','常规来源种数（含精英）','完整当前来源/普通难度单件率','稳定制作配方'],rows)
        add('### 11.4 实际出手结果与边界')
        table(['阶段','职业','测试武器','8秒左右实际DPS','魔力变化','配装防御','仆从上限'],
              [(d['Stage'],CN[CLASSES[d['Class']]],name(d['Weapon']),f'{d["Dps"]:.1f}',f'{d["ManaBefore"]}→{d["ManaAfter"]}',d.get('Defense','未导出'),d.get('MaxMinions','未导出')) for d in live.get('combat',[])])
        add('测试为普通难度、固定地面目标、每阶段各职业一条主力路线；召唤包含仆从与鞭，阶段1～3按配方配有矮人项链。短距离站桩有利于近战、持续追踪和多段武器，不能直接当作移动/对空Boss的最终DPS。已根据初测修正近战衍生命中、三连发预算、蜂弓/蜂剑、利刃台风、鞭标记和法师耗魔；仍需覆盖飞行Boss、完整阶段流程、60秒资源循环和多人实际领取。未完成的这些路径不标为已通过。')
        add('### 11.5 对应代码')
        for file in ['ProgressionBalance.cs','ProgressionBalanceData.cs','ProgressionLoot.cs','ProgressionRecipes.cs','ProgressionShops.cs','ProgressionGifts.cs','ProgressionAccessoryFamilies.cs']:
            add(f'- [{file}](src/TerraKnight/Items/{file})')
        add('- [游戏内数据/掉落验证](src/TerraKnight/DevAutoTestLootBalance.cs)；[实际出手验证](src/TerraKnight/DevAutoTestLootCombat.cs)。')
    text='\n'.join(out)+'\n'
    assert '\ufffd' not in text
    assert set(uses)==dropped
    assert len(planned)>=96
    OUT.write_text(text,encoding='utf-8')
    print(json.dumps({'output':str(OUT),'enemies':len(enemies),'boss_rows':len(BOSSES),'drop_rows':sum(map(len,DATA['drops'].values())),
                      'drop_items':len(dropped),'core_positions':96,'armor_paths':len(ARMOR_PATHS),'verified_source_hashes':len(verified),'lines':len(text.splitlines())},ensure_ascii=False))

if __name__=='__main__':
    import argparse
    parser=argparse.ArgumentParser()
    parser.add_argument('--runtime',type=Path)
    build(parser.parse_args().runtime)
