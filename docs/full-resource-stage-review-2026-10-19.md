# 全量物品获取阶段复核

后续更严格的材料、实际工位依赖及跨阶段持续来源检查见同目录 `crafting-material-continuity-2026-10-19.md`。该检查排除制作成品直接掉落作为依赖捷径。

运行时覆盖 3066 个物品、1496 条配方；逐项分类复核 158 个 Mat 材料定义与 231 个资源/工位/宝匣门槛。

本次实际改变 692 个物品的阶段（包括合成链自动传播与地区家具），没有重写原版配方材料、数量、工位和环境条件。

掉率：全部生产 Row() 调用与修改前逐项一致；独立掉落概率、池触发概率、数量、难度倍率均未调整。新增同期来源和提前解锁会改变部分共享池的候选占比，不能理解为每项最终边际概率完全不变。

小怪只制造物品过滤：按物品的 Craft 来源分类统一处理普通怪、精英、首杀及旧阶段保留表；不会仅凭“有配方”就误删暴雪瓶、星力手环等仍有原版世界来源的物品。Boss 奖励保持原范围。删除的组内份额保留为空结果，图鉴和随机抽签共用原总权重，因此不把被删物品的概率转移给剩余物品。鱼饵、苔藓和宠物旁路也执行同一限制。

运行时普通敌人阶段表中的 Craft 条目：0；修改前此类成品共有 104 种出现在小怪表内。

检查包含真实游戏中的掉落表、随机掉落样本、商店、钓鱼、宝匣与配方。采矿/原版能力拾取采用来源声明补充图模型，不能替代逐地区手动通关或采矿验证。钓鱼和宝匣来源为有限抽样，来源未命中也会记录为待核验。

## 配方闭合检查

| 阶段 | 可获取物品 | 尚未闭合的配方 |
| --- | ---: | ---: |
| 0 | 631 | 0 |
| 1 | 942 | 0 |
| 2 | 1332 | 0 |
| 3 | 1730 | 0 |
| 4 | 2103 | 0 |
| 5 | 2451 | 0 |

## 材料和工位的来源延迟或未覆盖项

下表区分标注阶段与采样到的最早来源；不会把仅修改标签称为同期可获取。

| 物品 | 标注阶段 | 最早来源阶段 | 说明 |
| --- | ---: | --- | --- |
| 神辉秘匣 `FloatingIslandFishingCrateHard` | 3 | 5 | Fishing；地区专属宝匣，困难模式开关阶段3，实际仍需到达对应阶段4/5地区，属有意保留的地区限制 |
| 白宫秘匣 `HallowedFishingCrateHard` | 3 | 5 | Fishing；地区专属宝匣，困难模式开关阶段3，实际仍需到达对应阶段4/5地区，属有意保留的地区限制 |
| 白灰秘匣 `OasisCrateHard` | 3 | 4 | Fishing；地区专属宝匣，困难模式开关阶段3，实际仍需到达对应阶段4/5地区，属有意保留的地区限制 |

## 全部材料与工位逐项结果

| 物品 | 原阶段 | 当前阶段 | 最早来源 | 判断依据 |
| --- | ---: | ---: | --- | --- |
| 橡实 `Acorn` | 0 | 0 | 0: drop:AngryBuzzer, drop:Blocker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 精金锭 `AdamantiteBar` | 3 | 3 | 3: Crates | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 精金熔炉 `AdamantiteForge` | 3 | 3 | 3: craft:秘银砧 | 困难模式初期冶炼、砧与魔法工位 |
| 精金矿 `AdamantiteOre` | 3 | 3 | 3: drop:BeamMiner, drop:BlackKnight | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 炼药桌 `AlchemyTable` | 2 | 2 | 2: drop:MageBlob, Fishing | 地牢和地狱工位 |
| 麦芽酒 `Ale` | 1 | 2 | 2: shop:DD2Bartender | 麦芽酒仍由现有酒馆老板供应，不额外创造免费或替代配方 |
| 琥珀 `Amber` | 1 | 0 | 0: drop:Mosquito, drop:ZombieMiner | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 紫晶 `Amethyst` | 0 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 禁戒碎片 `AncientBattleArmorMaterial` | 4 | 3 | 3: drop:LesserMawlek, Crates | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 渔夫耳环 `AnglerEarring` | 1 | 2 | 2: Quests | 钓鱼任务奖励从阶段2渔夫开放，同期配饰合成链随之对齐 |
| 蚁狮上颚 `AntlionMandible` | 2 | 1 | 1: drop:FungCrawler, drop:FungifiedZombie | 沿用实际可达的早期海岸、真菌或寒冷地区来源，而不是把标签标在来源之前 |
| 装甲洞穴鱼 `ArmoredCavefish` | 3 | 2 | 2: Fishing | 普通钓鱼、洞穴与酸水钓鱼材料；具体来源仍受鱼种群系限制 |
| 灰烬木 `AshWood` | 3 | 2 | 2: drop:BeamMiner, drop:CrystalCrawler | 地牢、狱石及对应Boss材料；保留原有地区和Boss路线 |
| 自动锤炼机 `Autohammer` | 5 | 5 | 5: drop:MushroomBaby, drop:MushroomRoller | 后期蘑菇矿、神庙与四柱工位 |
| 蜂蜡 `BeeWax` | 1 | 1 | 1: drop:BigBuzzer, drop:BlobFlyer | 早期邪恶矿及蜂系路线；鳞片与组织仍跟随阶段2对应Boss |
| 甲虫外壳 `BeetleHusk` | 5 | 5 | 5: drop:LobsterLancer, drop:ZotelingBalloon | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 黑色仙尘 `BlackFairyDust` | 5 | 5 | 5: drop:PalaceFly, drop:RoyalGuard | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 黑晶状体 `BlackLens` | 0 | 0 | 0: drop:AngryBuzzer, drop:BigFly | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 黑珍珠 `BlackPearl` | 2 | 1 | 1: drop:FungoonBaby, Crates | 沿用实际可达的早期海岸、真菌或寒冷地区来源，而不是把标签标在来源之前 |
| 搅拌机 `BlendOMatic` | 4 | 4 | 4: shop:Steampunker | 机械阶段蒸汽朋克工位 |
| 闪耀根 `Blinkroot` | 1 | 0 | 0: drop:ZombieGuard, drop:ZombieMiner | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 骨头 `Bone` | 2 | 2 | 2: drop:DungDefender, drop:FalseKnight@5 | 地牢、狱石及对应Boss材料；保留原有地区和Boss路线 |
| 骨之羽 `BoneFeather` | 5 | 5 | 5: drop:FlyingSentryJavelin, drop:MageKnight | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 骨头焊机 `BoneWelder` | 2 | 2 | 2: drop:Mage, drop:MageBalloon | 地牢和地狱工位 |
| 书 `Book` | 2 | 2 | 2: drop:Mage, drop:MageBalloon | 地牢、狱石及对应Boss材料；保留原有地区和Boss路线 |
| 书架 `Bookcase` | 2 | 2 | 2: craft:锯木机 | 地牢和地狱工位 |
| 针叶木 `BorealWood` | 1 | 0 | 0: drop:BlobFlyer, drop:MossKnight | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 玻璃瓶 `Bottle` | 0 | 0 | 0: shop:Merchant | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 瓶装水 `BottledWater` | 0 | 0 | 0: shop:Merchant | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 破蝙蝠之翼 `BrokenBatWing` | 4 | 3 | 3: drop:FlyingSentryJavelin, drop:FlyingSentrySword | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 断裂英雄剑 `BrokenHeroSword` | 5 | 5 | 5: drop:GreyPrince, drop:PalaceFly | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 蝴蝶尘 `ButterflyDust` | 4 | 4 | 4: drop:BlowFly, drop:ColMosquito | 机械阶段及其后续材料；保留机械三魂、叶绿与海龟材料区别 |
| 仙人掌 `Cactus` | 0 | 0 | 0: drop:Bouncer, drop:BurstingBouncer | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 链条 `Chain` | 0 | 0 | 0: craft:砧 | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 混沌鱼 `ChaosFish` | 2 | 3 | 3: Fishing | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 叶绿锭 `ChlorophyteBar` | 4 | 4 | 4: craft:精金熔炉 | 机械阶段及其后续材料；保留机械三魂、叶绿与海龟材料区别 |
| 叶绿矿 `ChlorophyteOre` | 4 | 4 | 4: drop:AbyssCrawler, drop:AcidFlyer | 机械阶段及其后续材料；保留机械三魂、叶绿与海龟材料区别 |
| 粘土块 `ClayBlock` | 0 | 0 | 0: drop:BurstingBouncer, drop:EggSac | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 钴锭 `CobaltBar` | 3 | 3 | 3: Crates | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 钴矿 `CobaltOre` | 3 | 3 | 3: drop:BeamMiner, drop:BeeHatchling | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 蛛网 `Cobweb` | 0 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 齿轮 `Cog` | 4 | 4 | 4: shop:Steampunker | 机械阶段及其后续材料；保留机械三魂、叶绿与海龟材料区别 |
| 彩纸 `Confetti` | 4 | 1 | 1: shop:PartyGirl | 派对女孩属于肉前居民；购买入口同步，不改价格 |
| 烹饪锅 `CookingPot` | 0 | 0 | 0: craft:砧 | 基础制作与建筑工位 |
| 铜锭 `CopperBar` | 0 | 0 | 0: Crates | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 铜矿 `CopperOre` | 0 | 0 | 0: drop:AngryBuzzer, drop:BigFly | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 珊瑚 `Coral` | 2 | 1 | 1: drop:FungoonBaby, drop:Jellyfish | 沿用实际可达的早期海岸、真菌或寒冷地区来源，而不是把标签标在来源之前 |
| 锈渠秘匣 `CorruptFishingCrateHard` | 4 | 3 | 3: Fishing | 困难宝匣与困难模式钓鱼开关同步，不改开箱概率 |
| 腐化种子 `CorruptSeeds` | 2 | 1 | 1: drop:FungoonBaby, Crates | 沿用实际可达的早期海岸、真菌或寒冷地区来源，而不是把标签标在来源之前 |
| 猩红沙块 `CrimsandBlock` | 1 | 0 | 0: drop:FlameBearerSmall | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 蛛丝秘匣 `CrimsonFishingCrateHard` | 4 | 3 | 3: Fishing | 困难宝匣与困难模式钓鱼开关同步，不改开箱概率 |
| 猩红种子 `CrimsonSeeds` | 1 | 0 | 0: drop:BlobFlyer, drop:FlameBearerSmall | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 猩红虎鱼 `CrimsonTigerfish` | 2 | 3 | 3: Fishing | 原有猩红深巢/深层丛林鱼种需要阶段3地区，不能只把标签标早 |
| 猩红矿锭 `CrimtaneBar` | 1 | 1 | 1: craft:熔炉 | 早期邪恶矿及蜂系路线；鳞片与组织仍跟随阶段2对应Boss |
| 猩红矿 `CrimtaneOre` | 1 | 1 | 1: drop:AcidFlyer, drop:AcidWalker | 早期邪恶矿及蜂系路线；鳞片与组织仍跟随阶段2对应Boss |
| 松脆蜂蜜块 `CrispyHoneyBlock` | 3 | 1 | 1: drop:AcidFlyer, drop:AcidWalker | 早期邪恶矿及蜂系路线；鳞片与组织仍跟随阶段2对应Boss |
| 水晶球 `CrystalBall` | 4 | 3 | 3: drop:GrubMimic, drop:MinesCrawler | 困难模式初期冶炼、砧与魔法工位 |
| 水晶碎块 `CrystalShard` | 3 | 3 | 3: drop:BeamMiner, drop:CrystalCrawler | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 诅咒焰 `CursedFlame` | 3 | 3 | 3: drop:AbyssCrawler, drop:CeilingDropper | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 雀鲷 `Damselfish` | 2 | 2 | 2: Fishing | 普通钓鱼、洞穴与酸水钓鱼材料；具体来源仍受鱼种群系限制 |
| 暗黑碎块 `DarkShard` | 4 | 3 | 3: drop:LesserMawlek, Crates | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 太阳花 `Daybloom` | 0 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 死亡草 `Deathweed` | 0 | 0 | 0: drop:ZombieGuard, drop:ZombieMiner | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 魔矿锭 `DemoniteBar` | 1 | 1 | 1: craft:熔炉 | 早期邪恶矿及蜂系路线；鳞片与组织仍跟随阶段2对应Boss |
| 魔矿 `DemoniteOre` | 1 | 1 | 1: drop:AcidFlyer, drop:AcidWalker | 早期邪恶矿及蜂系路线；鳞片与组织仍跟随阶段2对应Boss |
| 钻石 `Diamond` | 1 | 0 | 0: drop:MossmanShaker, drop:ZombieMiner | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 土块 `DirtBlock` | 0 | 0 | 0: basic | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 沙漠幽魂灯 `DjinnLamp` | 4 | 3 | 3: drop:Jellyfish, Crates | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 双鳍鳕鱼 `DoubleCod` | 2 | 2 | 2: Fishing | 普通钓鱼、洞穴与酸水钓鱼材料；具体来源仍受鱼种群系限制 |
| 泪城秘匣 `DungeonFishingCrateHard` | 4 | 3 | 3: Fishing | 困难宝匣与困难模式钓鱼开关同步，不改开箱概率 |
| 黑檀锦鲤 `Ebonkoi` | 2 | 2 | 2: Fishing | 普通钓鱼、洞穴与酸水钓鱼材料；具体来源仍受鱼种群系限制 |
| 黑檀沙块 `EbonsandBlock` | 2 | 2 | 2: drop:Flukeman | 黑檀沙仍通过现有皇家水道来源取得 |
| 乌木 `Ebonwood` | 1 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 灵气 `Ectoplasm` | 5 | 5 | 5: drop:FatFluke, drop:FinalBoss | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 翡翠 `Emerald` | 1 | 0 | 0: drop:AcidWalker, drop:MossmanRunner | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 空心子弹 `EmptyBullet` | 4 | 3 | 3: shop:ArmsDealer | 困难模式初期弹药材料，商店入口同步 |
| 爆炸粉 `ExplosivePowder` | 4 | 3 | 3: shop:Demolitionist | 困难模式初期弹药材料，商店入口同步 |
| 坠落之星 `FallenStar` | 0 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 羽毛 `Feather` | 0 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 火羽 `FireFeather` | 4 | 3 | 3: drop:FlameBearerLarge, drop:FlameBearerMed | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 火焰花 `Fireblossom` | 3 | 2 | 2: drop:Mummy, Crates | 地牢、狱石及对应Boss材料；保留原有地区和Boss路线 |
| 钓鱼浮标 `FishingBobber` | 1 | 2 | 2: Quests | 钓鱼任务奖励从阶段2渔夫开放，同期配饰合成链随之对齐 |
| 闪鳍锦鲤 `FlarefinKoi` | 2 | 2 | 2: Fishing | 普通钓鱼、洞穴与酸水钓鱼材料；具体来源仍受鱼种群系限制 |
| 血肉克隆台 `FleshCloningVaat` | 1 | 3 | 3: drop:MiniSpider, drop:SpiderFlyer | 当前模组血肉克隆台的既有可达来源在深巢 |
| 小雪怪皮毛 `FlinxFur` | 1 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 神辉秘匣 `FloatingIslandFishingCrateHard` | 5 | 3 | 5: Fishing | 困难宝匣与困难模式钓鱼开关同步，不改开箱概率 |
| 星云碎片 `FragmentNebula` | 5 | 5 | 5: drop:GreyPrince, drop:Paintmaster | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 日耀碎片 `FragmentSolar` | 5 | 5 | 5: drop:Grimm, drop:HollowKnightPrime | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 星尘碎片 `FragmentStardust` | 5 | 5 | 5: drop:FinalBoss, drop:HollowKnight | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 星旋碎片 `FragmentVortex` | 5 | 5 | 5: drop:Nailsage, drop:WhiteDefender | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 寒霜核 `FrostCore` | 4 | 3 | 3: drop:BeamMiner, drop:CrystalCrawler | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 寒霜鲦鱼 `FrostMinnow` | 2 | 2 | 2: Fishing | 普通钓鱼、洞穴与酸水钓鱼材料；具体来源仍受鱼种群系限制 |
| 安息秘匣 `FrozenCrateHard` | 4 | 3 | 3: Fishing | 困难宝匣与困难模式钓鱼开关同步，不改开箱概率 |
| 熔炉 `Furnace` | 0 | 0 | 0: craft:工作台 | 基础制作与建筑工位 |
| 凝胶 `Gel` | 0 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 巨型鸟妖之羽 `GiantHarpyFeather` | 4 | 3 | 3: drop:FlyingSentryJavelin, drop:FlyingSentrySword | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 玻璃 `Glass` | 0 | 0 | 0: craft:熔炉 | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 玻璃窑 `GlassKiln` | 0 | 0 | 0: craft:砧 | 基础制作与建筑工位 |
| 发光蘑菇 `GlowingMushroom` | 2 | 0 | 0: drop:ZombieMiner, Crates | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 金锭 `GoldBar` | 0 | 0 | 0: Crates | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 金蝴蝶 `GoldButterfly` | 1 | 3 | 3: drop:MossFlyer | 金蠕虫和金蝴蝶的固定来源为深巢苔藓怪；按实际来源阶段标注，其他金色小动物仍保留早期获取路线 |
| 金尘 `GoldDust` | 4 | 3 | 3: shop:Merchant | 困难模式初期弹药材料，商店入口同步 |
| 金矿 `GoldOre` | 0 | 0 | 0: drop:AngryBuzzer, drop:BigFly | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 金蠕虫 `GoldWorm` | 1 | 3 | 3: drop:MossWalker | 金蠕虫和金蝴蝶的固定来源为深巢苔藓怪；按实际来源阶段标注，其他金色小动物仍保留早期获取路线 |
| 钛金匣 `GoldenCrateHard` | 4 | 3 | 3: Fishing | 困难宝匣与困难模式钓鱼开关同步，不改开箱概率 |
| 草种子 `GrassSeeds` | 1 | 0 | 0: drop:BlobFlyer, drop:FlameBearerSmall | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 神圣锭 `HallowedBar` | 4 | 4 | 4: drop:GhostMarkoth, drop:HiveKnight | 机械阶段及其后续材料；保留机械三魂、叶绿与海龟材料区别 |
| 白宫秘匣 `HallowedFishingCrateHard` | 5 | 3 | 5: Fishing | 困难宝匣与困难模式钓鱼开关同步，不改开箱概率 |
| 神圣种子 `HallowedSeeds` | 4 | 3 | 3: shop:Dryad | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 竖琴 `Harp` | 4 | 3 | 3: shop:Wizard | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 重型组装机 `HeavyWorkBench` | 0 | 0 | 0: craft:砧 | 基础制作与建筑工位 |
| 地狱熔炉 `Hellforge` | 2 | 2 | 2: drop:BeamMiner, drop:MegaBeamMiner | 地牢和地狱工位 |
| 狱石 `Hellstone` | 2 | 2 | 2: drop:AcidFlyer, drop:AcidWalker | 地牢、狱石及对应Boss材料；保留原有地区和Boss路线 |
| 狱石锭 `HellstoneBar` | 2 | 2 | 2: craft:地狱熔炉 | 地牢、狱石及对应Boss材料；保留原有地区和Boss路线 |
| 血腥食人鱼 `Hemopiranha` | 1 | 3 | 3: Fishing | 原有猩红深巢/深层丛林鱼种需要阶段3地区，不能只把标签标早 |
| 优质钓鱼线 `HighTestFishingLine` | 1 | 2 | 2: Quests | 钓鱼任务奖励从阶段2渔夫开放，同期配饰合成链随之对齐 |
| 苍白矿石 `HkPaleOre` | 0 | 0 | 0: basic | 空洞原版骨钉升级资源，不作为泰拉矿或复制掉落 |
| 蜂蜜分配机 `HoneyDispenser` | 3 | 4 | 4: drop:BeeStinger, drop:ZombieHive | 蜂蜜分配机仍来自蜂巢原有敌人，不改变原始掉率 |
| 冰雪羽 `IceFeather` | 4 | 3 | 3: drop:BeamMiner, drop:CrystalCrawler | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 冰雪机 `IceMachine` | 1 | 2 | 2: drop:CrystalCrawler, drop:CrystalFlyer | 当前模组冰雪机的既有可达来源在水晶山峰 |
| 冰雪火把 `IceTorch` | 1 | 1 | 1: drop:AcidWalker, drop:MossCharger | 早期邪恶矿及蜂系路线；鳞片与组织仍跟随阶段2对应Boss |
| 灵液 `Ichor` | 3 | 3 | 3: drop:FatFluke, drop:FlipHopper | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 非法枪械部件 `IllegalGunParts` | 1 | 1 | 1: shop:ArmsDealer | 现有早期军火商购买路线 |
| 灌注站 `ImbuingStation` | 2 | 1 | 1: shop:WitchDoctor | 早期探索、蜂系与组合工位 |
| 铁砧 `IronAnvil` | 0 | 0 | 0: craft:工作台 | 基础制作与建筑工位 |
| 铁锭 `IronBar` | 0 | 0 | 0: Crates | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 秘银匣 `IronCrateHard` | 4 | 3 | 3: Fishing | 困难宝匣与困难模式钓鱼开关同步，不改开箱概率 |
| 铁矿 `IronOre` | 0 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 苍绿秘匣 `JungleFishingCrateHard` | 4 | 3 | 3: Fishing | 困难宝匣与困难模式钓鱼开关同步，不改开箱概率 |
| 丛林孢子 `JungleSpores` | 0 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 酒桶 `Keg` | 0 | 0 | 0: craft:锯木机 | 基础制作与建筑工位 |
| 瓢虫 `LadyBug` | 1 | 0 | 0: drop:AcidFlyer, drop:GrassHopper | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 酸蚀秘匣 `LavaCrateHard` | 4 | 3 | 3: Fishing | 困难宝匣与困难模式钓鱼开关同步，不改开箱概率 |
| 铅砧 `LeadAnvil` | 0 | 0 | 0: craft:工作台 | 基础制作与建筑工位 |
| 铅锭 `LeadBar` | 0 | 0 | 0: Crates | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 铅矿 `LeadOre` | 0 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 晶状体 `Lens` | 0 | 0 | 0: drop:AngryBuzzer, drop:BigFly | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 光明碎块 `LightShard` | 4 | 3 | 3: drop:Jellyfish, Crates | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 丛林蜥蜴熔炉 `LihzahrdFurnace` | 5 | 5 | 5: drop:GardenZombie, drop:MossKnightFat | 后期蘑菇矿、神庙与四柱工位 |
| 生命木织机 `LivingLoom` | 0 | 0 | 0: drop:AcidFlyer, drop:PlantShooter | 基础制作与建筑工位 |
| 织布机 `Loom` | 0 | 0 | 0: craft:锯木机 | 基础制作与建筑工位 |
| 夜明锭 `LunarBar` | 5 | 5 | 5: craft:远古操纵机 | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 远古操纵机 `LunarCraftingStation` | 5 | 5 | 5: drop:FinalBoss, drop:GreyPrince | 后期蘑菇矿、神庙与四柱工位 |
| 夜明矿 `LunarOre` | 5 | 5 | 5: drop:FinalBoss, drop:GreyPrince | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 绞肉机 `MeatGrinder` | 4 | 3 | 3: drop:CeilingDropper, drop:FatFluke | 困难模式初期冶炼、砧与魔法工位 |
| 陨石 `Meteorite` | 2 | 2 | 2: drop:AcidFlyer, drop:AcidWalker | 地牢、狱石及对应Boss材料；保留原有地区和Boss路线 |
| 陨石锭 `MeteoriteBar` | 2 | 2 | 2: craft:熔炉 | 地牢、狱石及对应Boss材料；保留原有地区和Boss路线 |
| 月光草 `Moonglow` | 1 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 泥块 `MudBlock` | 2 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 蘑菇草种子 `MushroomGrassSeeds` | 2 | 1 | 1: drop:FungifiedZombie, drop:FungusFlyer | 沿用实际可达的早期海岸、真菌或寒冷地区来源，而不是把标签标在来源之前 |
| 秘银砧 `MythrilAnvil` | 3 | 3 | 3: craft:砧 | 困难模式初期冶炼、砧与魔法工位 |
| 秘银锭 `MythrilBar` | 3 | 3 | 3: Crates | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 秘银矿 `MythrilOre` | 3 | 3 | 3: drop:AbyssCrawler, drop:BabyCentipede | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 纳米机器人 `Nanites` | 5 | 5 | 5: shop:Cyborg | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 白灰秘匣 `OasisCrateHard` | 4 | 3 | 4: Fishing | 困难宝匣与困难模式钓鱼开关同步，不改开箱概率 |
| 黑曜石 `Obsidian` | 2 | 2 | 2: drop:BeamMiner, drop:CrystalCrawler | 地牢、狱石及对应Boss材料；保留原有地区和Boss路线 |
| 黑曜石鱼 `Obsidifish` | 2 | 2 | 2: drop:DungDefender, Fishing | 普通钓鱼、洞穴与酸水钓鱼材料；具体来源仍受鱼种群系限制 |
| 雾泡秘匣 `OceanCrateHard` | 4 | 3 | 3: Fishing | 困难宝匣与困难模式钓鱼开关同步，不改开箱概率 |
| 山铜砧 `OrichalcumAnvil` | 3 | 3 | 3: craft:砧 | 困难模式初期冶炼、砧与魔法工位 |
| 山铜锭 `OrichalcumBar` | 3 | 3 | 3: Crates | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 山铜矿 `OrichalcumOre` | 3 | 3 | 3: drop:AbyssCrawler, drop:BabyCentipede | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 钯金锭 `PalladiumBar` | 3 | 3 | 3: Crates | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 钯金矿 `PalladiumOre` | 3 | 3 | 3: drop:BabyCentipede, drop:BeamMiner | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 棕榈木 `PalmWood` | 1 | 0 | 0: drop:AcidWalker, drop:FlameBearerSmall | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 亲情桃 `PalworldKinshipPeach` | 4 | 4 | 4: craft:水晶球 | 跨作品特殊加工材料，保留原有设计 |
| 珍珠沙块 `PearlsandBlock` | 4 | 3 | 3: drop:Jellyfish | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 珍珠木 `Pearlwood` | 4 | 3 | 3: drop:Jellyfish, Crates | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 粉凝胶 `PinkGel` | 4 | 0 | 0: drop:AngryBuzzer, drop:Blocker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 粉珍珠 `PinkPearl` | 2 | 1 | 1: drop:FungusFlyer, drop:Jellyfish | 沿用实际可达的早期海岸、真菌或寒冷地区来源，而不是把标签标在来源之前 |
| 粉线 `PinkThread` | 0 | 0 | 0: shop:Merchant | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 妖精尘 `PixieDust` | 3 | 3 | 3: drop:Jellyfish, Crates | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 铂金锭 `PlatinumBar` | 0 | 0 | 0: Crates | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 铂金矿 `PlatinumOre` | 0 | 0 | 0: drop:AngryBuzzer, drop:BigFly | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 七彩矿鱼 `Prismite` | 2 | 3 | 3: Fishing | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 南瓜 `Pumpkin` | 0 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 彩虹砖 `RainbowBrick` | 0 | 0 | 0: drop:Hatcher, drop:ZombieRunner | 当前模组已有开局彩虹砖来源；保留明确移植设计 |
| 红木 `RichMahogany` | 1 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 腐肉 `RottenChunk` | 0 | 0 | 0: drop:AngryBuzzer, drop:Blocker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 红玉 `Ruby` | 1 | 0 | 0: drop:MossmanRunner, drop:ZombieMiner | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 沙块 `SandBlock` | 0 | 0 | 0: basic | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 蓝玉 `Sapphire` | 1 | 0 | 0: drop:BlobFlyer, drop:PlantShooter | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 锯木机 `Sawmill` | 0 | 0 | 0: craft:工作台 | 基础制作与建筑工位 |
| 暗影木 `Shadewood` | 1 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 暗影鳞片 `ShadowScale` | 2 | 2 | 2: drop:DungDefender, drop:FatFluke | 地牢、狱石及对应Boss材料；保留原有地区和Boss路线 |
| 鲨鱼鳍 `SharkFin` | 2 | 1 | 1: drop:FungoonBaby, drop:Jellyfish | 沿用实际可达的早期海岸、真菌或寒冷地区来源，而不是把标签标在来源之前 |
| 寒颤棘 `Shiverthorn` | 1 | 1 | 1: drop:Hornet | 沿用实际可达的早期海岸、真菌或寒冷地区来源，而不是把标签标在来源之前 |
| 蘑菇矿锭 `ShroomiteBar` | 5 | 5 | 5: craft:自动锤炼机 | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 丝绸 `Silk` | 0 | 0 | 0: craft:织布机 | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 银锭 `SilverBar` | 0 | 0 | 0: Crates | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 银矿 `SilverOre` | 0 | 0 | 0: drop:AngryBuzzer, drop:BigFly | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 天磨 `SkyMill` | 1 | 1 | 1: drop:AngryBuzzer, drop:BigBuzzer | 早期探索、蜂系与组合工位 |
| 飞翔之魂 `SoulofFlight` | 3 | 3 | 3: drop:FlyingSentryJavelin, drop:FlyingSentrySword | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 恐惧之魂 `SoulofFright` | 4 | 4 | 4: drop:HiveKnight | 机械阶段及其后续材料；保留机械三魂、叶绿与海龟材料区别 |
| 光明之魂 `SoulofLight` | 3 | 3 | 3: drop:BeamMiner, drop:CrystalCrawler | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 力量之魂 `SoulofMight` | 4 | 4 | 4: drop:InfectedKnight@5 | 机械阶段及其后续材料；保留机械三魂、叶绿与海龟材料区别 |
| 暗影之魂 `SoulofNight` | 3 | 3 | 3: drop:AbyssCrawler, drop:BabyCentipede | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 视域之魂 `SoulofSight` | 4 | 4 | 4: drop:GhostMarkoth, drop:Hornet@4 | 机械阶段及其后续材料；保留机械三魂、叶绿与海龟材料区别 |
| 幽灵锭 `SpectreBar` | 5 | 5 | 5: craft:精金熔炉 | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 镜面鱼 `SpecularFish` | 2 | 2 | 2: Fishing | 普通钓鱼、洞穴与酸水钓鱼材料；具体来源仍受鱼种群系限制 |
| 魔法书 `SpellTome` | 4 | 3 | 3: drop:HeavyMantis, drop:LesserMawlek | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 蜘蛛牙 `SpiderFang` | 3 | 3 | 3: drop:BabyCentipede, drop:CentipedeHatcher | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 阴森嫩枝 `SpookyTwig` | 5 | 5 | 5: drop:PalaceFly, drop:RoyalGuard | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 阴森木 `SpookyWood` | 5 | 5 | 5: drop:FatFluke, drop:FinalBoss | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 蒸汽朋克锅炉 `SteampunkBoiler` | 4 | 4 | 4: shop:Steampunker | 机械阶段蒸汽朋克工位 |
| 毒刺 `Stinger` | 0 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 石块 `StoneBlock` | 0 | 0 | 0: basic, drop:AcidFlyer | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 坚固化石 `SturdyFossil` | 1 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 钓具箱 `TackleBox` | 1 | 2 | 2: Quests | 钓鱼任务奖励从阶段2渔夫开放，同期配饰合成链随之对齐 |
| 褴褛蜜蜂之翼 `TatteredBeeWing` | 4 | 4 | 4: drop:BeeStinger, drop:BigBee | 机械阶段及其后续材料；保留机械三魂、叶绿与海龟材料区别 |
| 锡锭 `TinBar` | 0 | 0 | 0: Crates | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 锡矿 `TinOre` | 0 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 工匠作坊 `TinkerersWorkshop` | 1 | 1 | 1: shop:Merchant, shop:GoblinTinkerer | 早期探索、蜂系与组合工位 |
| 组织样本 `TissueSample` | 2 | 2 | 2: drop:DungDefender, drop:FatFluke | 地牢、狱石及对应Boss材料；保留原有地区和Boss路线 |
| 钛金锭 `TitaniumBar` | 3 | 3 | 3: Crates | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 钛金熔炉 `TitaniumForge` | 3 | 3 | 3: craft:秘银砧 | 困难模式初期冶炼、砧与魔法工位 |
| 钛金矿 `TitaniumOre` | 3 | 3 | 3: drop:BeamMiner, drop:BlackKnight | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 黄玉 `Topaz` | 1 | 0 | 0: drop:AcidFlyer, drop:PlantShooter | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 钨锭 `TungstenBar` | 0 | 0 | 0: Crates | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 钨矿 `TungstenOre` | 0 | 0 | 0: drop:AngryBuzzer, drop:BigFly | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 海龟壳 `TurtleShell` | 4 | 4 | 4: drop:AcidFlyer, drop:AcidWalker | 机械阶段及其后续材料；保留机械三魂、叶绿与海龟材料区别 |
| 独角兽角 `UnicornHorn` | 3 | 3 | 3: drop:Jellyfish, Crates | 困难模式初期矿石、群系与羽翼材料，不要求机械Boss |
| 斑驳油鱼 `VariegatedLardfish` | 2 | 3 | 3: Fishing | 原有猩红深巢/深层丛林鱼种需要阶段3地区，不能只把标签标早 |
| 小瓶毒液 `VialofVenom` | 5 | 5 | 5: shop:WitchDoctor | 世纪之花及更后期事件、石巨人、四柱与月亮领主材料 |
| 毒粉 `ViciousPowder` | 1 | 0 | 0: drop:FlameBearerSmall | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 魔粉 `VilePowder` | 2 | 0 | 0: drop:AngryBuzzer, drop:Blocker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 藤蔓 `Vine` | 0 | 0 | 0: drop:AcidFlyer, drop:AcidWalker | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 水叶草 `Waterleaf` | 2 | 0 | 0: drop:GrassHopper, Crates | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 白珍珠 `WhitePearl` | 2 | 1 | 1: drop:FungoonBaby, Crates | 沿用实际可达的早期海岸、真菌或寒冷地区来源，而不是把标签标在来源之前 |
| 木材 `Wood` | 0 | 0 | 0: basic, drop:AcidFlyer | 开局自然材料、宝石、草药、丛林材料和基础加工品；仍按地区分布 |
| 珍珠木匣 `WoodenCrateHard` | 4 | 3 | 3: Fishing | 困难宝匣与困难模式钓鱼开关同步，不改开箱概率 |
| 工作台 `WorkBench` | 0 | 0 | 0: craft:徒手 | 基础制作与建筑工位 |

完整 JSON 另列全部物品、来源、阶段变化及每条未闭合配方的缺失材料。仅制造物品统一从小怪表过滤，其余装备、饰品、坐骑和特殊物品保留合法来源；材料链随资源门槛重新计算，不将所有物品强行放入同一个阶段。

## 验证证据

隔离游戏副本：`E:\皓子不困的妙妙工具\github项目管理\projects\强兼\artifacts\single-copy-materialcontinuity-20261019-205841`；Completed=True，TimedOut=False，Failures=0。

正式玩家存档修改 0，新增 0。运行器不承诺共享设置恢复，不能将这一结果写成共享设置恢复验证通过。

独立代码检查：75 项资源/地区检查；30 项过滤、图鉴概率和随机抽签检查（180000 次抽签）；资源表生成往返一致。构建使用 SkipPluginInstall=true，未安装到正式游戏目录。

运行日志汇总：

- [Info   :TerraKnight] [AutoTest] 材料阶段全表汇总 60241/60241 通过
