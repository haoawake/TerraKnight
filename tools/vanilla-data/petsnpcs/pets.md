# 泰拉瑞亚 1.4.5 宠物 / 照明宠物 / 矿车 资料

数据来源：反编译的 Terraria 1.4.5（`D:/ClaudeTemp/claude/e------------github-----projects---/e332c8d1-bad4-4402-a7e3-1d6b4d5e6526/scratchpad/terraria-src`），所有 `文件:行号` 都相对该目录。完整机器可读数据在同目录 `pets.json`（生成脚本 `scripts/gen_pets.py`）。

- 召唤宠物的物品 **84** 个：普通宠物 74 个，照明宠物 10 个（仙灵铃铛一个物品对应 3 种仙灵）。
- 矿车物品 **26** 个，另有 1 种没有物品的默认木矿车（MountID 13）和 3 种轨道物品。
- 按 AI 分组后共 7 种 aiStyle：11、26（含 6 种纯飞行子变体）、67、90、124、144、174。每种实现一次即可覆盖全部宠物。

## 1. 宠物系统怎么运转（原版）

- **判定是不是宠物**：看 buff。`Main.vanityPet[buff]` 是普通宠物（Terraria/Main.cs:9395-9468），`Main.lightPet[buff]` 是照明宠物（Terraria/Main.cs:9469-9480）。投射物侧另有 ProjectileID.Sets.LightPet（Terraria.ID/ProjectileID.cs:389 = {18,500,72,87,86,211,492,650,702,891,896,895}） 和 Main.projPet（Terraria/Main.cs:8925-9032（同时包含仆从））。
- **物品模板**：Item.DefaultToVanitypet(projId, buffID) (Item.cs:47937-47951)：damage 0, useStyle 1(挥动), 16x30, UseSound Item2, useAnimation/useTime 20, rare 3, noMelee, value sellPrice(0,2), buffType, shoot。旧物品手写同样字段。
- **使用方式**：左键使用物品：添加 buff（Item.buffType, buffTime 默认 3600）；物品不消耗。也可放入宠物/照明宠物栏自动生效。
- **每帧维持**：Player.miscEquips[2] 是矿车栏；miscEquips[0] 宠物、[1] 照明宠物、[3] 坐骑、[4] 钩爪。宠物/照明宠物栏放入后由 Player.UpdatePet / UpdatePetLight 每帧检查，若 buff 不存在且未隐藏就 AddBuff(buff, 3600) 并播放物品 UseSound (Player.cs:17412-17455)；buff 每帧在 Player.UpdateBuffs 中被设回 18000 并在投射物不存在时生成（BuffHandle_SpawnPetIfNeededAndSetTime, Player.cs:12315-12345）；投射物 AI 每帧检查 player 的宠物标志，标志为真就 timeLeft=2（标志随 buff 消失而失效，宠物 2 tick 后消失）。
- **装备栏预览动画**：ProjectileID.Sets.CharacterPreviewAnimations (ProjectileID.cs:14-17)：装备栏/角色预览里宠物的循环帧 SimpleLoop(起始帧, 帧数, 每帧tick=4) .WhenNotSelected/.WhenSelected/.WithOffset/.WithSpriteDirection/.WithCode(Float/SlimePet/WormPet…)。可直接当作"待机动画"数据用。
- **仙灵铃铛**：物品 buffType=27，放入照明宠物栏时随机选 27/101/102 之一，对应投射物 72 蓝仙灵 / 86 粉仙灵 / 87 绿仙灵；穿全套仙灵套装时固定蓝仙灵（Player.cs:17440-17453、11355-11392）。
- **华美甜点**：一个 buff（341）同时生成史莱姆王宠物 881 和史莱姆皇后宠物 934（Player.cs:12330-12340）。
- **贴图命名**：物品 `Images/Item_<ItemID>`，投射物 `Images/Projectile_<ProjID>`（竖排帧，帧高 = 图高 / Main.projFrames），buff 图标 `Images/Buff_<BuffID>`。

## 2. 按 AI 分组

表头说明：帧 = `Main.projFrames`（未设置为 1，Main.cs:8622）；碰撞箱 = SetDefaults 的 width×height 乘 scale；`tile` = SetDefaults 里的 tileCollide（飞行变体/飞行模式会在 AI 里临时设为 false）；贴图都在 `Content/Images/`；代码列 = 物品定义 Item.cs 行 · 投射物 SetDefaults 行 · Player.UpdateBuffs 中生成宠物的行。获取方式的代码出处见 pets.json 的 `source_refs`。

### aiStyle 11 · 悬浮跟随光球（暗影珠/仙灵）（4 个）

- 代码：Projectile.AI() 内联, Projectile.cs:24651
- 行为：无重力、不碰撞图格。目标=玩家中心；距离>800px 瞬移到玩家；>停止半径（暗影珠 70px，仙灵 50px）时以固定速度（暗影珠 3，仙灵 6 px/tick）直线靠近，否则速度清零。暗影珠：按住上/下时目标改到玩家头顶/脚下 40px，停止半径 4。暗影珠 rotation += 0.02；仙灵按水平速度翻转并 rotation=vx*0.1，4 帧每 4 tick 循环，随机撒 dust 56/73/74。

| ItemID | 内部名 | 中文 / 英文 | Buff | Proj | 类型 | 帧 | 碰撞箱 | tile | 贴图 | 代码 | 获取方式 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 115 | ShadowOrb | 暗影珠 / Shadow Orb | 19 ShadowOrb | 18 ShadowOrb | 照明 | 1 | 25x25 | N | Item_115 / Projectile_18 / Buff_19 | Item.cs:2758 · Projectile.cs:995 · Player.cs:11092 | 打破腐化之地的暗影珠（CheckOrb 轮换掉落 5 选 1）；腐化匣/污损匣开出 |
| 425 | FairyBell | 仙灵铃铛 / Fairy Bell | 27 FairyBlue | 72 BlueFairy | 照明 | 4 | 14x14 | N | Item_425 / Projectile_72 / Buff_27 | Item.cs:6331 · Projectile.cs:1524 · Player.cs:11355 | 合成：铃铛 + 25 妖精尘 + 8 光明之魂 + 10 视域之魂 @ 秘银砧/山铜砧 |
| 425 | FairyBell | 仙灵铃铛 / Fairy Bell | 27 FairyBlue | 86 PinkFairy | 照明 | 4 | 14x14 | N | Item_425 / Projectile_86 / Buff_27 | Item.cs:6331 · Projectile.cs:1676 · Player.cs:11355 | 合成：铃铛 + 25 妖精尘 + 8 光明之魂 + 10 视域之魂 @ 秘银砧/山铜砧 |
| 425 | FairyBell | 仙灵铃铛 / Fairy Bell | 27 FairyBlue | 87 GreenFairy | 照明 | 4 | 14x14 | N | Item_425 / Projectile_87 / Buff_27 | Item.cs:6331 · Projectile.cs:1690 · Player.cs:11355 | 合成：铃铛 + 25 妖精尘 + 8 光明之魂 + 10 视域之魂 @ 秘银砧/山铜砧 |

### aiStyle 26 · 地面行走型（46 个）

- 代码：AI_026(), Projectile.cs:59086（派发 26765）
- 行为：见 variants。地面型：ai[0]==0 地面模式：重力 0.4/tick，最大下落 10，默认最大水平速度 6.5、加速度 0.08（1.4 新宠物 6 / 0.2，并跟随玩家速度），遇障碍按障碍高度给跳跃速度 -5.1/-7.1/-9.1/-10.1/-11.1（Projectile.cs:63063-63300）；与玩家水平距离超过停止距离 num（默认 85px，见 59097-59165）才走。ai[0]==1 飞行模式：距离>num92（默认 500px，部分 400/300/200）或垂直差>300px 时切入（61260-61345），无碰撞直线飞向玩家（速度 10，1.4 宠物 12 或玩家速度，加速 0.2-0.5），离玩家<num102（默认 200px）且玩家着地并且自身不在实心块里时落回地面模式（61390-61500）。距离>2000px 直接瞬移到玩家。飞行模式与地面模式各自使用不同帧段（每类型帧表在 61620-62470 飞行段、62565-66045 地面段）。松露人(209)/伯尼(956)/虫群咬咬(1027) 不飞：淡出后在玩家脚下重新出现（61346-61389）。

| ItemID | 内部名 | 中文 / 英文 | Buff | Proj | 类型 | 帧 | 碰撞箱 | tile | 贴图 | 代码 | 获取方式 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 603 | Carrot | 胡萝卜 / Carrot | 40 PetBunny | 111 Bunny | 普通 | 8 | 18x18 | Y | Item_603 / Projectile_111 / Buff_40 | Item.cs:8400 · Projectile.cs:1994 · Player.cs:11394 | 仅典藏版（Collector's Edition）新建角色时赠送；非典藏版放进宠物栏不会召唤 |
| 669 | Fish | 鱼 / Fish | 41 BabyPenguin | 112 Penguin | 普通 | 6 | 18x18 | Y | Item_669 / Projectile_112 / Buff_41 | Item.cs:9202 · Projectile.cs:2004 · Player.cs:11442 | 冰雪箱（地下冰雪）1/50 |
| 753 | Seaweed | 海草 / Seaweed | 42 PetTurtle | 127 Turtle | 普通 | 16 | 22x22 | Y | Item_753 / Projectile_127 / Buff_42 | Item.cs:10165 · Projectile.cs:2161 · Player.cs:11563 | 常春藤箱（丛林箱）1/50；丛林匣/荆棘匣 1/20 |
| 1172 | LizardEgg | 蜥蜴蛋 / Lizard Egg | 53 PetLizard | 200 PetLizard | 普通 | 10 | 28x28 | Y | Item_1172 / Projectile_200 / Buff_53 | Item.cs:14392 · Projectile.cs:2864 · Player.cs:11633 | 丛林蜥蜴/丛林蜥蜴（爬行）/飞蛇 1/1000 |
| 1180 | ParrotCracker | 鹦鹉饼干 / Parrot Cracker | 54 PetParrot | 208 Parrot | 普通 | 5 | 18x36 | Y | Item_1180 / Projectile_208 / Buff_54 | Item.cs:14492 · Projectile.cs:2964 · Player.cs:11647 | 海盗商店：玩家在海洋 |
| 1181 | StrangeGlowingMushroom | 奇异发光蘑菇 / Strange Glowing Mushroom | 55 BabyTruffle | 209 Truffle | 普通 | 12 | 12x32 | Y | Item_1181 / Projectile_209 / Buff_55 | Item.cs:14506 · Projectile.cs:2974 · Player.cs:11661 | 松露人商店（常驻） |
| 1182 | Seedling | 幼苗 / Seedling | 56 PetSapling | 210 Sapling | 普通 | 12 | 14x30 | Y | Item_1182 / Projectile_210 / Buff_56 | Item.cs:14520 · Projectile.cs:2984 · Player.cs:11675 | 世纪之花 1/20（非专家）；其宝藏袋 1/15 |
| 1242 | AmberMosquito | 蚊子琥珀 / Amber Mosquito | 61 BabyDinosaur | 236 BabyDino | 普通 | 13 | 34x34 | Y | Item_1242 / Projectile_236 / Buff_61 | Item.cs:15398 · Projectile.cs:3177 · Player.cs:11507 | 提炼机提炼泥沙/雪泥/沙漠化石 |
| 1311 | EyeSpring | 弹簧眼 / Eye Spring | 65 EyeballSpring | 268 EyeSpring | 普通 | 8 | 18x32 | Y | Item_1311 / Projectile_268 / Buff_65 | Item.cs:16219 · Projectile.cs:3540 · Player.cs:11535 | 日食的眼怪 1/15 |
| 1312 | ToySled | 玩具雪橇 / Toy Sled | 66 BabySnowman | 269 BabySnowman | 普通 | 7 | 20x26 | Y | Item_1312 / Projectile_269 / Buff_66 | Item.cs:16233 · Projectile.cs:3550 · Player.cs:11549 | 冰雪宝箱怪 1/20 |
| 1798 | SpiderEgg | 蜘蛛卵 / Spider Egg | 81 PetSpider | 313 Spider | 普通 | 12 | 30x30 | Y | Item_1798 / Projectile_313 / Buff_81 | Item.cs:19932 · Projectile.cs:4059 · Player.cs:11703 | 南瓜王（南瓜月）掉落池之一 |
| 1799 | MagicalPumpkinSeed | 魔法南瓜子 / Magical Pumpkin Seed | 82 Squashling | 314 Squashling | 普通 | 13 | 24x40 | Y | Item_1799 / Projectile_314 / Buff_82 | Item.cs:19946 · Projectile.cs:4069 · Player.cs:11717 | 万圣节期间砍下南瓜 1/200 |
| 1810 | UnluckyYarn | 霉运纱线 / Unlucky Yarn | 84 BlackCat | 319 BlackCat | 普通 | 11 | 36x30 | Y | Item_1810 / Projectile_319 / Buff_84 | Item.cs:20050 · Projectile.cs:4122 · Player.cs:11493 | 万圣节礼袋 1/150 |
| 1837 | CursedSapling | 诅咒树苗 / Cursed Sapling | 85 CursedSapling | 324 CursedSapling | 普通 | 10 | 26x38 | Y | Item_1837 / Projectile_324 / Buff_85 | Item.cs:20348 · Projectile.cs:4174 · Player.cs:11689 | 哀木（南瓜月）掉落池之一 |
| 1927 | DogWhistle | 狗哨 / Dog Whistle | 91 Puppy | 334 Puppy | 普通 | 11 | 28x28 | Y | Item_1927 / Projectile_334 / Buff_91 | Item.cs:20906 · Projectile.cs:4270 · Player.cs:11465 | 圣诞礼物 1/400 |
| 1959 | BabyGrinchMischiefWhistle | 格林奇宝宝的恶作剧口哨 / Baby Grinch's Mischief Whistle | 92 BabyGrinch | 353 BabyGrinch | 普通 | 14 | 18x28 | Y | Item_1959 / Projectile_353 / Buff_92 | Item.cs:21156 · Projectile.cs:4432 · Player.cs:11479 | 冰雪女王（霜月）1/15 |
| 2587 | TartarSauce | 魅惑里拉琴 / Beguiling Lyre | 136 MiniMinotaur | 398 MiniMinotaur | 普通 | 11 | 18x40 | Y | Item_2587 / Projectile_398 / Buff_136 | Item.cs:25390 · Projectile.cs:4793 · Player.cs:11780 | 铁匣/秘银匣 1/20 |
| 3060 | BoneRattle | 骨头拨浪鼓 / Bone Rattle | 154 BabyFaceMonster | 499 BabyFaceMonster | 普通 | 12 | 34x34 | Y | Item_3060 / Projectile_499 / Buff_154 | Item.cs:28295 · Projectile.cs:5427 · Player.cs:11521 | 克苏鲁之脑 1/20（非专家）；其宝藏袋 1/20 |
| 4366 | EucaluptusSap | 桉树汁 / Eucalyptus Sap | 218 SugarGlider | 765 SugarGlider | 普通 | 10 | 18x18 | Y | Item_4366 / Projectile_765 / Buff_218 | Item.cs:37233 · Projectile.cs:7881 · Player.cs:11337 | 摇森林树 1/1000 |
| 4550 | BambooLeaf | 竹叶 / Bamboo Leaf | 262 BabyRedPanda | 825 BabyRedPanda | 普通 | 26 | 26x32 | Y | Item_4550 / Projectile_825 / Buff_262 | Item.cs:38272 · Projectile.cs:8047 · Player.cs:11152 | 旅商随机商品（rarity[3]） |
| 4551 | HellCake | 地狱蛋糕块 / Slice of Hell Cake | 261 BabyImp | 821 BabyImp | 普通 | 23 | 20x36 | Y | Item_4551 / Projectile_821 / Buff_261 | Item.cs:38227 · Projectile.cs:8032 · Player.cs:11148 | 暗影箱（地狱）1/10；黑曜石匣/狱石匣 1/20 |
| 4604 | ExoticEasternChewToy | 奇异咀嚼玩具 / Exotic Chew Toy | 259 FennecFox | 816 FennecFox | 普通 | 17 | 26x32 | Y | Item_4604 / Projectile_816 / Buff_259 | Item.cs:37984 · Projectile.cs:7975 · Player.cs:11140 | 旅商随机商品（rarity[3]） |
| 4701 | MudBud | 泥芽 / Mud Bud | 264 Plantero | 854 Plantero | 普通 | 19 | 26x32 | Y | Item_4701 / Projectile_854 / Buff_264 | Item.cs:38641 · Projectile.cs:8189 · Player.cs:11156 | 动物学家商店：击败世纪之花后 |
| 4735 | BallOfFuseWire | 保险丝球 / Ball O' Fuse Wire | 266 DynamiteKitten | 858 DynamiteKitten | 普通 | 14 | 26x20 | Y | Item_4735 / Projectile_858 / Buff_266 | Item.cs:38938 · Projectile.cs:8239 · Player.cs:11160 | 动物学家商店：图鉴完成度 ≥70% |
| 4736 | FullMoonSqueakyToy | 满月尖叫玩具 / Full Moon Squeaky Toy | 267 BabyWerewolf | 859 BabyWerewolf | 普通 | 24 | 20x32 | Y | Item_4736 / Projectile_859 / Buff_267 | Item.cs:38942 · Projectile.cs:8249 · Player.cs:11164 | 动物学家商店：困难模式血月 |
| 4737 | OrnateShadowKey | 华丽暗影钥匙 / Ornate Shadow Key | 268 ShadowMimic | 860 ShadowMimic | 普通 | 14 | 20x20 | Y | Item_4737 / Projectile_860 / Buff_268 | Item.cs:38946 · Projectile.cs:8259 · Player.cs:11168 | 暗影箱（地狱）1/10；黑曜石匣/狱石匣 1/20 |
| 4777 | LightningCarrot | 闪电胡萝卜 / Lightning Carrot | 274 VoltBunny | 875 VoltBunny | 普通 | 11 | 20x20 | Y | Item_4777 / Projectile_875 / Buff_274 | Item.cs:39298 · Projectile.cs:8395 · Player.cs:11172 | 动物学家商店：图鉴完成度 ≥50% |
| 4797 | KingSlimePetItem | 皇家美味 / Royal Delight | 284 KingSlimePet | 881 KingSlimePet | 普通 | 12 | 20x20 | Y | Item_4797 / Projectile_881 / Buff_284 | Item.cs:39417 · Projectile.cs:8453 · Player.cs:11176 | 大师模式：击败史莱姆王后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4800 | BrainOfCthulhuPetItem | 罐中脑 / Brain in a Jar | 287 BrainOfCthulhuPet | 884 BrainOfCthulhuPet | 普通 | 14 | 20x20 | Y | Item_4800 / Projectile_884 / Buff_287 | Item.cs:39420 · Projectile.cs:8488 · Player.cs:11188 | 大师模式：击败克苏鲁之脑后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4806 | PlanteraPetItem | 世纪之花幼苗 / Plantera Seedling | 293 PlanteraPet | 890 PlanteraPet | 普通 | 12 | 20x20 | Y | Item_4806 / Projectile_890 / Buff_293 | Item.cs:39426 · Projectile.cs:8523 · Player.cs:11212 | 大师模式：击败世纪之花后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4807 | GolemPetItem | 石巨人守卫 / Guardian Golem | 294 GolemPet | 891 GolemPet | 照明 | 15 | 20x20 | Y | Item_4807 / Projectile_891 / Buff_294 | Item.cs:39427 · Projectile.cs:8523 · Player.cs:11216 | 大师模式：击败石巨人后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4813 | EverscreamPetItem | 灌木星星 / Shrub Star | 300 EverscreamPet | 897 EverscreamPet | 普通 | 11 | 20x20 | Y | Item_4813 / Projectile_897 / Buff_300 | Item.cs:39433 · Projectile.cs:8568 · Player.cs:11240 | 大师模式：击败常绿尖叫怪（霜月）后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4815 | MartianPetItem | 宇宙滑板 / Cosmic Skateboard | 302 MartianPet | 899 MartianPet | 普通 | 14 | 20x20 | Y | Item_4815 / Projectile_899 / Buff_302 | Item.cs:39435 · Projectile.cs:8591 · Player.cs:11248 | 大师模式：击败火星飞碟后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4816 | DD2OgrePetItem | 食人魔棍棒 / Ogre's Club | 303 DD2OgrePet | 900 DD2OgrePet | 普通 | 14 | 20x20 | Y | Item_4816 / Projectile_900 / Buff_303 | Item.cs:39436 · Projectile.cs:8591 · Player.cs:11252 | 大师模式：击败食人魔 T3（撒旦军队）后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4960 | QueenSlimePetItem | 王室美食 / Regal Delicacy | 317 QueenSlimePet | 934 QueenSlimePet | 普通 | 12 | 20x20 | Y | Item_4960 / Projectile_934 / Buff_317 | Item.cs:40050 · Projectile.cs:8876 · Player.cs:11260 | 大师模式：击败史莱姆皇后后每名玩家必得（MasterModeDropOnAllPlayers） |
| 5088 | BerniePetItem | 伯尼的钮扣 / Bernie's Button | 327 BerniePet | 956 BerniePet | 普通 | 11 | 20x28 | Y | Item_5088 / Projectile_956 / Buff_327 | Item.cs:40787 · Projectile.cs:9026 · Player.cs:11264 | 公主商店（常驻） |
| 5090 | DeerclopsPetItem | 独眼巨鹿眼球 / Deerclops Eyeball | 329 DeerclopsPet | 958 DeerclopsPet | 普通 | 17 | 20x20 | Y | Item_5090 / Projectile_958 / Buff_329 | Item.cs:40797 · Projectile.cs:9049 · Player.cs:11272 | 大师模式：击败独眼巨鹿后每名玩家必得（MasterModeDropOnAllPlayers） |
| 5091 | PigPetItem | 怪物肉 / Monster Meat | 330 PigPet | 959 PigPet | 普通 | 12 | 20x20 | Y | Item_5091 / Projectile_959 / Buff_330 | Item.cs:40802 · Projectile.cs:9059 · Player.cs:11276 | 多种地面/地下敌怪 1/1500（永恒领域种子世界 1/500） |
| 5098 | ChesterPetItem | 眼骨 / Eye Bone | 331 ChesterPet | 960 ChesterPet | 普通 | 20 | 20x20 | Y | Item_5098 / Projectile_960 / Buff_331 | Item.cs:40865 · Projectile.cs:9069 · Player.cs:11280 | 独眼巨鹿 1/3（非专家）；其宝藏袋 1/3 |
| 5131 | ResplendentDessert | 华美甜点 / Resplendent Dessert | 341 DualSlimePet | 881 KingSlimePet | 普通 | 12 | 20x20 | Y | Item_5131 / Projectile_881 / Buff_341 | Item.cs:41083 · Projectile.cs:8453 · Player.cs:11284 | 合成：皇家美味（史莱姆王宠物）+ 王室美食（史莱姆皇后宠物），无需制作站；同时召唤两只 |
| 5131 | ResplendentDessert | 华美甜点 / Resplendent Dessert | 341 DualSlimePet | 934 QueenSlimePet | 普通 | 12 | 20x20 | Y | Item_5131 / Projectile_934 / Buff_341 | Item.cs:41083 · Projectile.cs:8876 · Player.cs:11284 | 合成：皇家美味（史莱姆王宠物）+ 王室美食（史莱姆皇后宠物），无需制作站；同时召唤两只 |
| 5276 | JunimoPetItem | 星之果实 / Stardrop | 345 JunimoPet | 994 JunimoPet | 普通 | 16 | 20x20 | Y | Item_5276 / Projectile_994 / Buff_345 | Item.cs:41868 · Projectile.cs:9438 · Player.cs:11289 | 手持 Joja 可乐与树妖对话，点"给可乐"，Junimo 动画结束后赠送 |
| 5297 | BlueEgg | 蓝鸡蛋 / Blue Chicken Egg | 349 BlueChickenPet | 998 BlueChickenPet | 普通 | 10 | 20x20 | Y | Item_5297 / Projectile_998 / Buff_349 | Item.cs:42152 · Projectile.cs:9503 · Player.cs:11293 | 旅商随机商品（rarity[3]） |
| 5332 | SpiffoPlush | 毛绒Spiffo / Spiffo Plush | 351 Spiffo | 1003 Spiffo | 普通 | 16 | 26x26 | Y | Item_5332 / Projectile_1003 / Buff_351 | Item.cs:42293 · Projectile.cs:9540 · Player.cs:11297 | 僵尸类敌怪 1/1500 |
| 5333 | GlowTulip | 发光郁金香 / Glow Tulip | 352 CavelingGardener | 1004 CavelingGardener | 普通 | 15 | 26x26 | Y | Item_5333 / Projectile_1004 / Buff_352 | Item.cs:42299 · Projectile.cs:9550 · Player.cs:11301 | 采集野生发光郁金香图格（TileID 656） |
| 5466 | DeadCellsSwarmGrenade | 虫群手榴弹 / Swarm Grenade | 356 DeadCellsSwarmBiter | 1027 DeadCellsSwarmBiter | 普通 | 5 | 26x26 | Y | Item_5466 / Projectile_1027 / Buff_356 | Item.cs:43129 · Projectile.cs:9727 · Player.cs:11317 | 动物学家商店：派对进行中 |

### aiStyle 26 · 悬停飞行（黄蜂宝宝/西风鱼/鲨鱼宝宝/小鸟妖/蝴蝶/蜂王/猪鲨/月总/双足翼龙/河豚）（10 个）

- 代码：Projectile.cs:60399-60774（在 AI_026 开头的独立分支，执行完直接 return）
- 行为：悬停飞行（黄蜂宝宝/西风鱼/鲨鱼宝宝/小鸟妖/蝴蝶/蜂王/猪鲨/月总/双足翼龙/河豚）：加速度 0.4（西风鱼/鲨鱼/河豚 0.3），无碰撞，靠近玩家身侧

| ItemID | 内部名 | 中文 / 英文 | Buff | Proj | 类型 | 帧 | 碰撞箱 | tile | 贴图 | 代码 | 获取方式 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1170 | Nectar | 花蜜 / Nectar | 51 BabyHornet | 198 BabyHornet | 普通 | 4 | 26x26 | Y | Item_1170 / Projectile_198 / Buff_51 | Item.cs:14364 · Projectile.cs:2843 · Player.cs:11605 | 蜂王 1/15（非专家）；其宝藏袋 1/9 |
| 2420 | ZephyrFish | 和风鱼 / Zephyr Fish | 127 ZephyrFish | 380 ZephyrFish | 普通 | 4 | 26x26 | Y | Item_2420 / Projectile_380 / Buff_127 | Item.cs:23997 · Projectile.cs:4647 · Player.cs:11766 | 钓鱼：传奇（Legendary）稀有度 |
| 4425 | SharkBait | 鲨鱼鱼饵 / Shark Bait | 219 SharkPup | 774 SharkPup | 普通 | 8 | 26x26 | Y | Item_4425 / Projectile_774 / Buff_219 | Item.cs:37479 · Projectile.cs:7913 · Player.cs:11132 | 水中箱（海洋）；海洋匣/海边匣 1/10 |
| 4603 | BirdieRattle | 小鸟拨浪鼓 / Birdie Rattle | 258 LilHarpy | 815 LilHarpy | 普通 | 10 | 26x26 | Y | Item_4603 / Projectile_815 / Buff_258 | Item.cs:37980 · Projectile.cs:7965 · Player.cs:11136 | 旅商随机商品（rarity[3]） |
| 4605 | BedazzledNectar | 眩晕花蜜 / Bedazzled Nectar | 260 GlitteryButterfly | 817 GlitteryButterfly | 普通 | 18 | 26x26 | Y | Item_4605 / Projectile_817 / Buff_260 | Item.cs:37988 · Projectile.cs:7985 · Player.cs:11144 | 旅商随机商品（rarity[3]） |
| 4802 | QueenBeePetItem | 闪耀蜂蜜 / Sparkling Honey | 289 QueenBeePet | 886 QueenBeePet | 普通 | 8 | 20x20 | Y | Item_4802 / Projectile_886 / Buff_289 | Item.cs:39422 · Projectile.cs:8488 · Player.cs:11196 | 大师模式：击败蜂王后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4808 | DukeFishronPetItem | 海猪肉 / Pork of the Sea | 295 DukeFishronPet | 892 DukeFishronPet | 普通 | 6 | 20x20 | Y | Item_4808 / Projectile_892 / Buff_295 | Item.cs:39428 · Projectile.cs:8523 · Player.cs:11220 | 大师模式：击败猪龙鱼公爵后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4810 | MoonLordPetItem | 月亮乌贼 / Piece of Moon Squid | 297 MoonLordPet | 894 MoonLordPet | 普通 | 8 | 20x20 | Y | Item_4810 / Projectile_894 / Buff_297 | Item.cs:39430 · Projectile.cs:8545 · Player.cs:11228 | 大师模式：击败月亮领主后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4817 | DD2BetsyPetItem | 双足翼龙蛋 / Betsy's Egg | 304 DD2BetsyPet | 901 DD2BetsyPet | 普通 | 12 | 20x20 | Y | Item_4817 / Projectile_901 / Buff_304 | Item.cs:39437 · Projectile.cs:8591 · Player.cs:11256 | 大师模式：击败双足翼龙后每名玩家必得（MasterModeDropOnAllPlayers） |
| 5513 | PufferfishPet | 河豚虾 / Puffer Shrimp | 371 Pufferfish | 1046 Pufferfish | 普通 | 12 | 26x26 | Y | Item_5513 / Projectile_1046 / Buff_371 | Item.cs:43388 · Projectile.cs:9910 · Player.cs:11321 | 合成：5 河豚 + 10 虾（无需制作站） |

### aiStyle 26 · 吞噬怪宝宝（1 个）

- 代码：Projectile.cs:60105-60231（在 AI_026 开头的独立分支，执行完直接 return）
- 行为：吞噬怪宝宝：纯飞行，绕玩家

| ItemID | 内部名 | 中文 / 英文 | Buff | Proj | 类型 | 帧 | 碰撞箱 | tile | 贴图 | 代码 | 获取方式 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 994 | EatersBone | 吞噬怪骨头 / Eater's Bone | 45 BabyEater | 175 BabyEater | 普通 | 2 | 34x34 | Y | Item_994 / Projectile_175 / Buff_45 | Item.cs:12838 · Projectile.cs:2614 · Player.cs:11577 | 世界吞噬怪 1/20（非专家）；其宝藏袋 1/20 |

### aiStyle 26 · 骷髅王宝宝头（1 个）

- 代码：Projectile.cs:60232-60398（在 AI_026 开头的独立分支，执行完直接 return）
- 行为：骷髅王宝宝头：纯飞行，旋转

| ItemID | 内部名 | 中文 / 英文 | Buff | Proj | 类型 | 帧 | 碰撞箱 | tile | 贴图 | 代码 | 获取方式 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1169 | BoneKey | 骨头钥匙 / Bone Key | 50 BabySkeletronHead | 197 BabySkeletronHead | 普通 | 1 | 42x42 | Y | Item_1169 / Projectile_197 / Buff_50 | Item.cs:14350 · Projectile.cs:2833 · Player.cs:11591 | 地牢守卫 100% |

### aiStyle 26 · 妖灵（1 个）

- 代码：Projectile.cs:60775-60941（在 AI_026 开头的独立分支，执行完直接 return）
- 行为：妖灵：纯飞行，发光

| ItemID | 内部名 | 中文 / 英文 | Buff | Proj | 类型 | 帧 | 碰撞箱 | tile | 贴图 | 代码 | 获取方式 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1183 | WispinaBottle | 妖灵瓶 / Wisp in a Bottle | 57 Wisp | 211 Wisp | 照明 | 10 | 24x24 | Y | Item_1183 / Projectile_211 / Buff_57 | Item.cs:14534 · Projectile.cs:2994 · Player.cs:11731 | 困难模式地牢敌怪 1/400（专家有重掷） |

### aiStyle 26 · 提基之灵（1 个）

- 代码：Projectile.cs:60942-61088（在 AI_026 开头的独立分支，执行完直接 return）
- 行为：提基之灵：纯飞行

| ItemID | 内部名 | 中文 / 英文 | Buff | Proj | 类型 | 帧 | 碰撞箱 | tile | 贴图 | 代码 | 获取方式 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1171 | TikiTotem | 提基图腾 / Tiki Totem | 52 TikiSpirit | 199 TikiSpirit | 普通 | 8 | 28x28 | Y | Item_1171 / Projectile_199 / Buff_52 | Item.cs:14378 · Projectile.cs:2853 · Player.cs:11619 | 巫医商店：困难模式且玩家在丛林 |

### aiStyle 26 · 骷髅王/机械骷髅王宠物（2 个）

- 代码：Projectile.cs:61089-61255（在 AI_026 开头的独立分支，执行完直接 return）
- 行为：骷髅王/机械骷髅王宠物：纯飞行

| ItemID | 内部名 | 中文 / 英文 | Buff | Proj | 类型 | 帧 | 碰撞箱 | tile | 贴图 | 代码 | 获取方式 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 4801 | SkeletronPetItem | 疯狂骷髅头 / Possessed Skull | 288 SkeletronPet | 885 SkeletronPet | 普通 | 10 | 20x20 | Y | Item_4801 / Projectile_885 / Buff_288 | Item.cs:39421 · Projectile.cs:8488 · Player.cs:11192 | 大师模式：击败骷髅王后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4805 | SkeletronPrimePetItem | 机器人骷髅头 / Robotic Skull | 292 SkeletronPrimePet | 889 SkeletronPrimePet | 普通 | 11 | 20x20 | Y | Item_4805 / Projectile_889 / Buff_292 | Item.cs:39425 · Projectile.cs:8523 · Player.cs:11208 | 大师模式：击败机械骷髅王后每名玩家必得（MasterModeDropOnAllPlayers） |

### aiStyle 67 · AI_067 跟随型（与海盗仆从共用）：猩红之心/同伴方块/最土的块/巨石宠物（5 个）

- 代码：AI_067_FreakingPirates(), Projectile.cs:50015（派发 30651）
- 行为：地面跟随+远离时飞回，与 AI_026 类似但锚点：猩红之心在玩家身后 (15+w/2+40)px，同伴方块/最土的块在玩家正中，巨石宠物在身后 15+w/2；猩红之心飞行、自发光、旋转 rotation+=vx/20，帧 0/2 停 12 tick 其余 6 tick；同伴方块落入熔岩 20 tick 会惨叫并消失，在黑暗中会"刺"玩家 3 点伤害；巨石宠物 rotation+=vx/16 滚动（50222-50460）。

| ItemID | 内部名 | 中文 / 英文 | Buff | Proj | 类型 | 帧 | 碰撞箱 | tile | 贴图 | 代码 | 获取方式 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 3062 | CrimsonHeart | 猩红之心 / Crimson Heart | 155 CrimsonHeart | 500 CrimsonHeart | 照明 | 4 | 16x16 | Y | Item_3062 / Projectile_500 / Buff_155 | Item.cs:28309 · Projectile.cs:5415 · Player.cs:11106 | 打破猩红之地的猩红之心（CheckOrb 轮换掉落）；猩红匣/血匣开出 |
| 3628 | CompanionCube | 同伴方块 / Companion Cube | 191 CompanionCube | 653 CompanionCube | 普通 | 1 | 24x24 | Y | Item_3628 / Projectile_653 / Buff_191 | Item.cs:32704 · Projectile.cs:7014 · Player.cs:11120 | 旅商随机商品（稀有度槽 rarity[3]） |
| 5400 | DirtiestBlock | 最土的块 / The Dirtiest Block | 354 DirtiestBlock | 1018 DirtiestBlock | 普通 | 1 | 16x16 | Y | Item_5400 / Projectile_1018 / Buff_354 | Item.cs:42906 · Projectile.cs:9639 · Player.cs:11305 | 挖掉稀有的"最土的块"图格（TileID 668） |
| 5523 | BoulderPet | 友好巨石 / Friendly Boulder | 373 BoulderPet | 1056 BoulderPet | 普通 | 1 | 32x32 | Y | Item_5523 / Projectile_1056 / Buff_373 | Item.cs:43499 · Projectile.cs:9989 · Player.cs:11309 | 击碎世界中特殊巨石图格（TileID 716 BoulderThatSpawnsPet）→ 生成滚动的投射物 1057，其消失时掉落；可与友好彩虹巨石微光互转 |
| 5654 | RainbowBoulderPet | 友好彩虹巨石 / Friendly Rainbow Boulder | 382 RainbowBoulderPet | 1090 RainbowBoulderPet | 普通 | 1 | 16x16 | Y | Item_5654 / Projectile_1090 / Buff_382 | Item.cs:44409 · Projectile.cs:10165 · Player.cs:11313 | 微光转化友好巨石（5523 ⇄ 5654） |

### aiStyle 90 · 魔法灯笼（1 个）

- 代码：Projectile.AI() 内联, Projectile.cs:32969
- 行为：飞到玩家 (40*dir, -40) 处，距离>1000 瞬移；朝向=玩家朝向；每 10 tick 对 17 格半径内的宝藏图格（Main.IsTileSpelunkable）随机撒 dust 204 高亮（类似洞穴探险）。

| ItemID | 内部名 | 中文 / 英文 | Buff | Proj | 类型 | 帧 | 碰撞箱 | tile | 贴图 | 代码 | 获取方式 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 3043 | MagicLantern | 魔法灯笼 / Magic Lantern | 152 MagicLantern | 492 MagicLantern | 照明 | 8 | 18x32 | Y | Item_3043 / Projectile_492 / Buff_152 | Item.cs:28147 · Projectile.cs:5647 · Player.cs:11456 | 骷髅商人：满月（moonPhase==0）夜晚 |

### aiStyle 124 · 飞行锚点宠物（克眼/双子/可疑触手/女皇/南瓜王/冰雪女王/咕噜咪）（7 个）

- 代码：Projectile.AI() 内联, Projectile.cs:35619
- 行为：锚点=player.MountedCenter + (dir*30, -20)（各类型偏移：双子 (30,-50)、女皇 (-36,-50)、南瓜王 (30,-60)、冰后 (-30,-50)、咕噜咪 (-40,-40)，同时存在某些宠物时左右互换，35694-35740）；velocity = (锚点-中心)*0.1，距离<4 时 velocity*=0.25；距离>1000 瞬移；速度>6 时可疑触手按速度方向转向。

| ItemID | 内部名 | 中文 / 英文 | Buff | Proj | 类型 | 帧 | 碰撞箱 | tile | 贴图 | 代码 | 获取方式 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 3577 | SuspiciousLookingTentacle | 可疑触手 / Suspicious Looking Tentacle | 190 SuspiciousTentacle | 650 SuspiciousTentacle | 照明 | 4 | 20x20 | N | Item_3577 / Projectile_650 / Buff_190 | Item.cs:32258 · Projectile.cs:6979 · Player.cs:11341 | 月亮领主宝藏袋（专家/大师） |
| 4798 | EyeOfCthulhuPetItem | 可疑咧嘴眼球 / Suspicious Grinning Eye | 285 EyeOfCthulhuPet | 882 EyeOfCthulhuPet | 普通 | 20 | 20x20 | N | Item_4798 / Projectile_882 / Buff_285 | Item.cs:39418 · Projectile.cs:8463 · Player.cs:11180 | 大师模式：击败克苏鲁之眼后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4804 | TwinsPetItem | 微型双子魔眼 / Pair of Eyeballs | 291 TwinsPet | 888 TwinsPet | 普通 | 36 | 20x20 | N | Item_4804 / Projectile_888 / Buff_291 | Item.cs:39424 · Projectile.cs:8510 · Player.cs:11204 | 大师模式：击败双子魔眼后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4811 | FairyQueenPetItem | 光之珠宝 / Jewel of Light | 298 FairyQueenPet | 895 FairyQueenPet | 照明 | 6 | 20x20 | N | Item_4811 / Projectile_895 / Buff_298 | Item.cs:39431 · Projectile.cs:8555 · Player.cs:11232 | 大师模式：击败光之女皇后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4812 | PumpkingPetItem | 南瓜香薰蜡烛 / Pumpkin Scented Candle | 299 PumpkingPet | 896 PumpkingPet | 照明 | 16 | 20x20 | N | Item_4812 / Projectile_896 / Buff_299 | Item.cs:39432 · Projectile.cs:8555 · Player.cs:11236 | 大师模式：击败南瓜王（南瓜月）后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4814 | IceQueenPetItem | 冰冻王冠 / Frozen Crown | 301 IceQueenPet | 898 IceQueenPet | 普通 | 16 | 20x20 | N | Item_4814 / Projectile_898 / Buff_301 | Item.cs:39434 · Projectile.cs:8578 · Player.cs:11244 | 大师模式：击败冰雪女王（霜月）后每名玩家必得（MasterModeDropOnAllPlayers） |
| 5089 | GlommerPetItem | 咕噜咪的花 / Glommer's Flower | 328 GlommerPet | 957 GlommerPet | 普通 | 12 | 20x20 | N | Item_5089 / Projectile_957 / Buff_328 | Item.cs:40792 · Projectile.cs:9036 · Player.cs:11268 | 跳跳兽 1/100（永恒领域种子世界 1/40） |

### aiStyle 144 · AI_144 飞行宠物（撒旦军队三宠/欢快星/仙斧）（5 个）

- 代码：AI_144_DD2Pet(), Projectile.cs:71133（派发 37211）
- 行为：锚点 player.MountedCenter + (dir*30, -20) 加正弦上下浮动（cos(localAI0*π/60)*2），按类型改帧速/速度（702：帧 10 tick、速度 6；703：帧 4/2、速度 3；1050 仙斧在玩家另一侧（x 取反），离玩家<320px 时朝面向方向找 12 格内可摇动的树(TileID.Sets.IsShakeable)，飞过去做砍树动作：树木粒子+音效，偶尔调用 KillTile_DropItems 掉出该树的掉落物（不破坏树），之后冷却 300 tick（71258-71331））；距离>1000 瞬移；velocity 向锚点插值。

| ItemID | 内部名 | 中文 / 英文 | Buff | Proj | 类型 | 帧 | 碰撞箱 | tile | 贴图 | 代码 | 获取方式 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 3855 | DD2PetGato | Gato蛋 / Gato Egg | 200 PetDD2Gato | 703 DD2PetGato | 普通 | 8 | 20x20 | N | Item_3855 / Projectile_703 / Buff_200 | Item.cs:34022 · Projectile.cs:7435 · Player.cs:11329 | 撒旦军队：黑暗魔法师 T1 1/3（专家 1/2，与龙蛋二选一）；T3 1/6 |
| 3856 | DD2PetGhost | 飞眼怪蛋 / Creeper Egg | 201 PetDD2Ghost | 702 DD2PetGhost | 照明 | 4 | 20x20 | N | Item_3856 / Projectile_702 / Buff_201 | Item.cs:34023 · Projectile.cs:7435 · Player.cs:11333 | 撒旦军队：食人魔 T2 1/5（专家 1/4）；食人魔 T3 1/10 |
| 3857 | DD2PetDragon | 龙蛋 / Dragon Egg | 202 PetDD2Dragon | 701 DD2PetDragon | 普通 | 3 | 20x20 | N | Item_3857 / Projectile_701 / Buff_202 | Item.cs:34024 · Projectile.cs:7435 · Player.cs:11124 | 撒旦军队：黑暗魔法师 T1 1/3（专家 1/2，与 Gato 蛋二选一）；T3 1/6 |
| 4365 | CelestialWand | 天界魔棒 / Celestial Wand | 217 UpbeatStar | 764 UpbeatStar | 普通 | 1 | 20x20 | N | Item_4365 / Projectile_764 / Buff_217 | Item.cs:37229 · Projectile.cs:7868 · Player.cs:11128 | 旅商：抽到星星公主王冠时同时上架（与公主裙一起） |
| 5517 | AxeFairyPetItem | 仙橡果 / Faecorn | 372 AxeFairyPet | 1050 AxeFairyPet | 普通 | 16 | 20x20 | N | Item_5517 / Projectile_1050 / Buff_372 | Item.cs:43426 · Projectile.cs:9955 · Player.cs:11325 | 摇森林树 1/1000 |

### aiStyle 174 · 多节蠕虫宠物（吞噬怪/毁灭者/邪教徒龙）（3 个）

- 代码：AI_174_MultisegmentPet(), Projectile.cs:46178（派发 37331）；绘制 Main.DrawMultisegmentPet, Main.cs:37745
- 行为：速度=clamp(玩家速度, 4, 12)；距离≥120px 时每 tick 最多转 5° 朝向玩家；|vy|<1 时 vy-=0.1；rotation=速度角+π/2；>2000px 瞬移；身体节由 oldPos 拖尾绘制（TrailCacheLength）。

| ItemID | 内部名 | 中文 / 英文 | Buff | Proj | 类型 | 帧 | 碰撞箱 | tile | 贴图 | 代码 | 获取方式 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 4799 | EaterOfWorldsPetItem | 蠕动残骸 / Writhing Remains | 286 EaterOfWorldsPet | 883 EaterOfWorldsPet | 普通 | 3 | 20x20 | N | Item_4799 / Projectile_883 / Buff_286 | Item.cs:39419 · Projectile.cs:8476 · Player.cs:11184 | 大师模式：击败世界吞噬怪后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4803 | DestroyerPetItem | 失效探测器 / Deactivated Probe | 290 DestroyerPet | 887 DestroyerPet | 普通 | 3 | 20x20 | N | Item_4803 / Projectile_887 / Buff_290 | Item.cs:39423 · Projectile.cs:8498 · Player.cs:11200 | 大师模式：击败毁灭者后每名玩家必得（MasterModeDropOnAllPlayers） |
| 4809 | LunaticCultistPetItem | 碑牌碎片 / Tablet Fragment | 296 LunaticCultistPet | 893 LunaticCultistPet | 普通 | 4 | 20x20 | N | Item_4809 / Projectile_893 / Buff_296 | Item.cs:39429 · Projectile.cs:8533 · Player.cs:11224 | 大师模式：击败拜月教邪教徒后每名玩家必得（MasterModeDropOnAllPlayers） |

## 3. 照明宠物的光

原版有两种加光方式：①`Projectile.light` 字段 + `Projectile.ProjLight()`（Projectile.cs:16247-16757）按类型给 RGB 乘系数，最后 `Lighting.AddLight(中心格, r,g,b)`；②AI 里直接 `Lighting.AddLight` 或用 `DelegateMethods.v3_1` + `Utils.PlotTileLine(..., DelegateMethods.CastLightOpen)` 沿线段逐格加光（只照非实心格，DelegateMethods.cs:729-741）。RGB 数值 1.0 约等于火把亮度的一格满光。

| 物品 | Proj | RGB | 方式 | 代码 |
|---|---|---|---|---|
| 115 暗影珠 | 18 ShadowOrb | (0.54, 0.09, 0.9) | Projectile.light=0.9（SetDefaults Projectile.cs:995）经 ProjLight 着色：R*0.6 G*0.1 B*1 | Terraria/Projectile.cs:16456-16460; Terraria/Projectile.cs:16756 (Lighting.AddLight 中心点) |
| 425 仙灵铃铛 | 72 BlueFairy | (0.405, 0.675, 1) | light=0.9：R*0.45 G*0.75 B=1（蓝仙灵） | Terraria/Projectile.cs:16536-16541 |
| 425 仙灵铃铛 | 86 PinkFairy | (0.9, 0.405, 0.75) | light=0.9：R*1 G*0.45 B=0.75（粉仙灵） | Terraria/Projectile.cs:16542-16547 |
| 425 仙灵铃铛 | 87 GreenFairy | (0.405, 1, 0.675) | light=0.9：R*0.45 G=1 B*0.75（绿仙灵） | Terraria/Projectile.cs:16548-16553 |
| 1181 奇异发光蘑菇 | 209 Truffle | (0.15, 0.2, 0.875) | 虽是普通宠物但 light=0.5：ProjLight R*0.3 G*0.4 B*1.75，再乘 (255-alpha)/255 | Terraria/Projectile.cs:16711-16720 |
| 1183 妖灵瓶 | 211 Wisp | (0.5, 0.9, 1) | light=1：(0.5,0.9,1)*light；localAI[0]==0 时 light 设为 1.5，否则 1（所以亮度在 1~1.5 倍之间） | Terraria/Projectile.cs:16697-16710 |
| 1837 诅咒树苗 | 324 CursedSapling | (0.9, 0.6, 0.2) | 诅咒树苗飞行模式时 Lighting.AddLight(0.9,0.6,0.2) | Terraria/Projectile.cs:62241 |
| 3043 魔法灯笼 | 492 MagicLantern | (0.35, 0.35, 0.1) | AI 中 Lighting.AddLight(position, 0.35,0.35,0.1)；另有 17 格半径宝藏高亮 | Terraria/Projectile.cs:32992 |
| 3062 猩红之心 | 500 CrimsonHeart | (0.9, 0.1, 0.3) | AI_067 中 Lighting.AddLight(Center, 0.9,0.1,0.3) | Terraria/Projectile.cs:50356-50358 |
| 3577 可疑触手 | 650 SuspiciousTentacle | (1, 1.8, 2) | DelegateMethods.v3_1=(0.5,0.9,1)*2，用 Utils.PlotTileLine+CastLightOpen 沿"自身中心→速度*6"(宽20)、自身左右、玩家中心→玩家速度*6(宽40)、玩家左右 四条线逐格加光（只照非实心格） | Terraria/Projectile.cs:35657-35665; Terraria/DelegateMethods.cs:729-741 |
| 3856 飞眼怪蛋 | 702 DD2PetGhost | (0.3, 0.5, 1) | v3_1=(0.3,0.5,1)，同上四条 CastLightOpen 线 | Terraria/Projectile.cs:71182-71186 |
| 4807 石巨人守卫 | 891 GolemPet | (1.5, 0.915, 0.24) | v3_1=(1,0.61,0.16)*1.5，仅自身两条线（速度线宽20 + 左右） | Terraria/Projectile.cs:66046-66052 |
| 4811 光之珠宝 | 895 FairyQueenPet | (2.25, 1.35, 2.25) | base=(1,0.6,1)*1.5（复古光照模式再*0.8）；自身线用 base*1.5，玩家线用 base*0.75 | Terraria/Projectile.cs:35666-35679 |
| 4812 南瓜香薰蜡烛 | 896 PumpkingPet | (2.25, 1.575, 0.1125) | base=(1,0.7,0.05)*1.5（复古*0.8）；自身线 base*1.5，玩家线 base*0.75 | Terraria/Projectile.cs:35680-35693 |

注：松露人宝宝（209）和诅咒树苗（324）虽然是普通宠物，也会发光，已列在表中。魔法灯笼另有"宝藏高亮"效果，可疑触手/女皇/南瓜王/闪烁灯芯/石巨人宠物的光是沿线段铺开的，范围比单点光大。

## 4. 没有物品的宠物投射物

- Proj 1095 `PalworldPetChillet`：projPet=true、aiStyle 26、12 帧（Main.cs:8902），有 player.petFlagChillet/petFlagChilletIgnis 标志（Player.cs:2223-2225），但 1.4.5 代码中没有任何地方生成它；物品 5665/5666（帕鲁 冰电/冰火 Chillet）其实是坐骑 62/63（Item.cs:44510-44533, Mount.cs:2561-2566 SetAsChillet）。
- Proj 1096 `PalworldPetChilletIgnis`：projPet=true、aiStyle 26、12 帧（Main.cs:8902），有 player.petFlagChillet/petFlagChilletIgnis 标志（Player.cs:2223-2225），但 1.4.5 代码中没有任何地方生成它；物品 5665/5666（帕鲁 冰电/冰火 Chillet）其实是坐骑 62/63（Item.cs:44510-44533, Mount.cs:2561-2566 SetAsChillet）。

## 5. 矿车

### 5.1 轨道

- 图格：TileID 314 `MinecartTrack`，贴图 `Images/Tiles_314`。Minecart.cs 自带 36 种连接帧 + 装饰帧 36-39（左下/右下/弹性缓冲器/普通缓冲器）；每格 8 个 2px 采样点高度表 _tileHeight[frameX][0..7]（Minecart.cs:103 Initialize）
- 放置：Item.cartTrack=true、createTile=314；Minecart.PlaceTrack(tile, style) 设 frameX=-1/首个压力板帧/首个左增速帧/首个右增速帧 (Minecart.cs:1492-1512)，随后 Minecart.FrameTrack 依据左右相邻轨道自动选连接帧 (Minecart.cs:947)。轨道可悬空放置、不需要支撑；用锤子敲可在分叉处切换 (FlipSwitchTrack, Minecart.cs:1302)。拆除时按轨道类型掉 2340/2492/2739 (Minecart.cs:1514)。
- 轨道类型：0=普通 Type_Normal；1=压力板 Type_Pressure（压过触发 HitTrackSwitch/电路, Player.cs:28448）；2=增速 Type_Booster（BoosterSpeed=4f, Minecart.cs:60）

| ItemID | 内部名 | 中文 / 英文 | placeStyle | 说明 | 获取方式 |
|---|---|---|---|---|---|
| 2340 | MinecartTrack | 矿车轨道 / Minecart Track | 0 | 普通轨道 | 合成：1 铁/铅锭 + 1 木材 @ 砧 → 50 个；也可拆掉世界生成的废弃矿车轨道获得（Terraria/Recipe.cs:6581-6590; Terraria/Minecart.cs:1514-1523 (GetTrackItem)） |
| 2492 | PressureTrack | 压力板轨道 / Pressure Plate Track | 1 | 压力板轨道（mech=true，经过时触发电路） | 合成：1 矿车轨道 + 1 任意压力板 @ 砧（Terraria/Recipe.cs:6591-6598） |
| 2739 | BoosterTrack | 增速轨道 / Booster Track | 2 | 增速轨道（mech=true，BoosterSpeed=4，可用扳手/锤子切换方向） | 机械师商店（常驻）（Terraria/Chest.cs:2058） |

### 5.2 怎么骑上去（只能在轨道上）

- 右键点击（或智能光标/快捷坐骑键）轨道图格 314：Player.TileInteractionsUse → LaunchMinecartHook 在该格中心生成"轨道钩"投射物 403 TrackHook（Player.cs:33520-33540, 33815-33822）。
- 快捷坐骑键：若坐骑栏物品不是矿车，先 QuickMinecartSnap 在玩家周围 24px 找轨道，或 QuickMinecart 在可交互范围内找最近的可见轨道并发射钩子 (Player.cs:6070-6170)。
- 钩子拉到轨道时（Player.cs:22840-22870）：坐骑类型 = 矿车栏 miscEquips[2] 中的矿车（MountID.Sets.Cart 且非仅专家或专家模式），否则默认 13 木矿车；Minecart.GetOnTrack 把玩家吸附到轨道（用 MagnetOffset(25,26)、MinecartTextureWidth 50 计算），并检查上方 height+heightBoost-20 空间无实心块，然后 mount.SetMount。
- 所以：矿车只能在轨道上骑上去；物品本身放在矿车栏（宠物/照明宠物/矿车/坐骑/钩爪 五个杂项栏中的第 3 个）。

### 5.3 骑乘物理

- 骑乘时 Player.Update 走 Minecart.TrackCollision(…, followDown=controlDown, followUp=controlUp) 处理沿轨道移动、坡度、跳跃离轨、缓冲器、增速、开关 (Player.cs:28296-28330, Minecart.cs:562)。返回位：0 在轨道、1 弹性缓冲器、2 用了坡道、3 碰开关、4 左增速、5 右增速。
- 在轨道上：TrackRotation 计算车身旋转 fullRotation 与 gfxOffY (Minecart.cs:1261)；离开轨道/跳起时播 MinecartJumpingSound。
- 不在轨道且着地：fullRotation=0、onWrongGround=true、runSlowdown=0.2，此时按左/右（新按下）就自动下车 mount.TryDismount (Player.cs:26864-26875)。所以矿车离开轨道几乎不能开。
- 跳跃：jumpHeight 15 tick、jumpSpeed 5.15，blockExtraJumps=true（禁用二段跳/翅膀），flightTimeMax 0；坠落伤害倍率 1。
- 撞击敌怪：速度>4 时车头前方加长判定框伤害 NPC：伤害 25+55*速度比（木矿车 15+30*速度）×1.5(困难)×1.5(专家)，击退 10+40*速度比 (Player.cs:27746-27800, 29471-29510)。

### 5.4 超级矿车（矿车升级包 / 机械矿车的激光）

- "超级矿车"（1.4.4+ 的矿车升级包机制）：Player.unlockedSuperCart 由使用矿车升级包 5289 永久解锁（旧存档背包有机械矿车 3353 也视为已解锁，Player.cs:56368-56377）；enabledSuperCart 可在装备界面开关（Main.cs:41965-42040）；UsingSuperCart = 两者皆真 (Player.cs:3950-3963)。
- 对所有 MountID.Sets.Cart 矿车生效（Mount.cs:4851）：runSpeed/dashSpeed 20、acceleration 0.1、jumpHeight 15、jumpSpeed 5.15 (Mount.cs:373-381, 482-508, 2834-2860)
- 防御 +2*(1+|vx|/runSpeed*2.5) (Mount.cs:4855-4856)
- CastSuperCartLaser：行驶中（vx≠0）对车头前方 ±45°、300px 内、视线可达的可追踪敌怪各发一道 591 MinecartMechLaser，伤害 60 (Mount.cs:5260-5290)
- 撞击伤害改为 50+100*速度 (Player.cs:29510-29513)
- 机械矿车（MountID 11）本身只多了红色车灯和火花，以及更高的 heightBoost；1.4.5 里激光属于超级矿车系统，对所有矿车生效。

### 5.5 矿车物品

所有矿车（除下面列出的特例）共用 `Mount.SetAsMinecart`（Mount.cs:2752-2803）的数据：runSpeed/dashSpeed 13、acceleration 0.04、jumpHeight 15、jumpSpeed 5.15、heightBoost 10、fallDamage 1、flightTimeMax 0、blockExtraJumps、3 帧（行驶 3 帧每 12 tick，静止 1 帧）、playerYOffsets 8、xOffset 1、yOffset 13、bodyFrame 3、spawnDust 213、粒子 DelegateMethods.Minecart.Sparks。物品本身：`Item.DefaultToMinecart(mount)`（Item.cs:48398-48403）设 36x26 与 mountType。

| ItemID | 内部名 | 中文 / 英文 | MountID | Buff | 车贴图 | 速度/加速/跳 | 特殊 | 代码 | 获取方式 |
|---|---|---|---|---|---|---|---|---|---|
| 2343 | Minecart | 矿车 / Minecart | 6 Minecart | 118 | Images/Mount_Minecart | 13/0.04/15 | 手写数据，与 SetAsMinecart 默认值一致 | Item.cs:23553 · Mount.cs:918 | 合成：15 铁锭/铅锭 + 10 任意木材 @ 铁砧/铅砧（Terraria/Recipe.cs:12560） |
| 3353 | MinecartMech | 机械矿车 / Mechanical Cart | 11 MinecartMech | 166 | Images/Mount_MinecartMech + Images/Mount_MinecartMechGlow(发光层) | 13/0.04/15 | 粒子 SparksMech；骑乘时 Mount.UpdateEffects case 11 加红色光 (0.4,0.12,0.15)*(1+\|vx\|/runSpeed*2.5) 到玩家中心/上/下/左/右 (Mount.cs:5072-5090) 并喷火花。激光由"超级矿车"系统提供（见 super_cart）。物品带 expert=true (Item.cs:30670)。 | Item.cs:30666 · Mount.cs:1489 | 使用矿车升级包（5289）获得，同时永久解锁超级矿车；5289 = 机械车轮零件 + 机械货车零件 + 机械电池零件 @ 秘银砧/山铜砧（零件来自三个机械 Boss）（Terraria/Player.cs:45585-45596; Terraria/Recipe.cs:12644） |
| 4066 | DesertMinecart | 沙漠矿车 / Desert Minecart | 15 DesertMinecart | 208 | Images/Mount_MinecartDesert | 13/0.04/15 |  | Item.cs:35706 · Mount.cs:970 (SetAsMinecart Mount.cs:971) | 砂岩箱（地下沙漠）1/15（Terraria/WorldGen.cs:37204-37213） |
| 4067 | FishMinecart | 鲤鱼矿车 / Minecarp | 16 FishMinecart | 210 | Images/Mount_MinecartMineCarp | 13/0.04/15 | 手写数据，与默认值一致 | Item.cs:35710 · Mount.cs:1049 | 渔夫任务奖励 1/60（随任务次数提高概率）（Terraria/Player.cs:57070-57072） |
| 4426 | BeeMinecart | 蜜蜂矿车 / Bee Minecart | 18 BeeMinecart | 220 | Images/Extra_108 | 13/0.04/15 |  | Item.cs:37483 · Mount.cs:973 (SetAsMinecart Mount.cs:974) | 常春藤箱（丛林）1/10（Terraria/WorldGen.cs:37217-37227） |
| 4427 | LadybugMinecart | 瓢虫矿车 / Ladybug Minecart | 19 LadybugMinecart | 222 | Images/Extra_109 | 13/0.04/15 |  | Item.cs:37487 · Mount.cs:976 (SetAsMinecart Mount.cs:977) | 生命木箱 1/6（与向日葵矿车二选一）（Terraria/WorldGen.cs:36638-36647; Terraria/WorldGen.cs:36925-36934） |
| 4428 | PigronMinecart | 猪龙矿车 / Pigron Minecart | 20 PigronMinecart | 224 | Images/Extra_110 | 13/0.04/15 |  | Item.cs:37491 · Mount.cs:979 (SetAsMinecart Mount.cs:980) | 猪龙（三种）1/100（Terraria.GameContent.ItemDropRules/ItemDropDatabase.cs:969） |
| 4429 | SunflowerMinecart | 向日葵矿车 / Sunflower Minecart | 21 SunflowerMinecart | 226 | Images/Extra_111 | 13/0.04/15 |  | Item.cs:37495 · Mount.cs:982 (SetAsMinecart Mount.cs:983) | 生命木箱 1/6（与瓢虫矿车二选一）（Terraria/WorldGen.cs:36638-36647; Terraria/WorldGen.cs:36925-36934） |
| 4443 | HellMinecart | 恶魔地狱矿车 / Demonic Hellcart | 22 HellMinecart | 228 | Images/Extra_112 | 13/0.04/15 |  | Item.cs:37560 · Mount.cs:985 (SetAsMinecart Mount.cs:986) | 暗影箱（地狱）1/10；黑曜石匣/狱石匣 1/20（Terraria/WorldGen.cs:37564-37566; Terraria/Player.cs:9290-9292） |
| 4450 | ShroomMinecart | 蘑菇矿车 / Shroom Minecart | 24 ShroomMinecart | 231 | Images/Extra_115 | 13/0.04/15 | 蘑菇矿车：frontTextureGlow = Extra_116（发光层）(Mount.cs:990) | Item.cs:37628 · Mount.cs:988 (SetAsMinecart Mount.cs:989) | 蘑菇箱 1/2（Terraria/WorldGen.cs:37042-37044; Terraria/WorldGen.cs:37366-37370） |
| 4451 | AmethystMinecart | 紫晶矿车 / Amethyst Minecart | 25 AmethystMinecart | 233 | Images/Extra_117 | 13/0.04/15 |  | Item.cs:37632 · Mount.cs:992 (SetAsMinecart Mount.cs:993) | 合成：矿车 + 大紫晶 @ 砧（Terraria/Recipe.cs:12583） |
| 4452 | TopazMinecart | 黄玉矿车 / Topaz Minecart | 26 TopazMinecart | 235 | Images/Extra_118 | 13/0.04/15 |  | Item.cs:37636 · Mount.cs:995 (SetAsMinecart Mount.cs:996) | 合成：矿车 + 大黄玉 @ 砧（Terraria/Recipe.cs:12588） |
| 4453 | SapphireMinecart | 蓝玉矿车 / Sapphire Minecart | 27 SapphireMinecart | 237 | Images/Extra_119 | 13/0.04/15 |  | Item.cs:37640 · Mount.cs:998 (SetAsMinecart Mount.cs:999) | 合成：矿车 + 大蓝玉 @ 砧（Terraria/Recipe.cs:12593） |
| 4454 | EmeraldMinecart | 翡翠矿车 / Emerald Minecart | 28 EmeraldMinecart | 239 | Images/Extra_120 | 13/0.04/15 |  | Item.cs:37644 · Mount.cs:1001 (SetAsMinecart Mount.cs:1002) | 合成：矿车 + 大翡翠 @ 砧（Terraria/Recipe.cs:12598） |
| 4455 | RubyMinecart | 红玉矿车 / Ruby Minecart | 29 RubyMinecart | 241 | Images/Extra_121 | 13/0.04/15 |  | Item.cs:37648 · Mount.cs:1004 (SetAsMinecart Mount.cs:1005) | 合成：矿车 + 大红玉 @ 砧（Terraria/Recipe.cs:12603） |
| 4456 | DiamondMinecart | 钻石矿车 / Diamond Minecart | 30 DiamondMinecart | 243 | Images/Extra_122 | 13/0.04/15 |  | Item.cs:37652 · Mount.cs:1007 (SetAsMinecart Mount.cs:1008) | 合成：矿车 + 大钻石 @ 砧（Terraria/Recipe.cs:12608） |
| 4467 | AmberMinecart | 琥珀矿车 / Amber Minecart | 31 AmberMinecart | 245 | Images/Extra_123 | 13/0.04/15 |  | Item.cs:37736 · Mount.cs:1010 (SetAsMinecart Mount.cs:1011) | 合成：矿车 + 大琥珀 @ 砧（Terraria/Recipe.cs:12613） |
| 4468 | BeetleMinecart | 甲虫矿车 / Beetle Minecart | 32 BeetleMinecart | 247 | Images/Extra_124 | 13/0.04/15 |  | Item.cs:37754 · Mount.cs:1013 (SetAsMinecart Mount.cs:1014) | 合成：矿车 + 8 甲虫外壳 @ 砧（Terraria/Recipe.cs:12577） |
| 4469 | MeowmereMinecart | 彩虹猫矿车 / Meowmere Minecart | 33 MeowmereMinecart | 249 | Images/Extra_125 | 13/0.04/15 | 喵喵矿车：粒子 SparksMeow + 喵叫落地/缓冲器音效 (Mount.cs:1018-1020) | Item.cs:37758 · Mount.cs:1016 (SetAsMinecart Mount.cs:1017) | 月亮领主 1/10；其宝藏袋 1/10（Terraria.GameContent.ItemDropRules/ItemDropDatabase.cs:602; Terraria/Player.cs:7744-7746） |
| 4470 | PartyMinecart | 派对矿车 / Party Wagon | 34 PartyMinecart | 251 | Images/Extra_126 | 13/0.04/15 |  | Item.cs:37762 · Mount.cs:1022 (SetAsMinecart Mount.cs:1023) | 派对女孩商店（常驻）（Terraria/Chest.cs:2297） |
| 4471 | PirateMinecart | 荷兰人矿车 / The Dutchman | 35 PirateMinecart | 253 | Images/Extra_127 | 13/0.04/15 |  | Item.cs:37766 · Mount.cs:1025 (SetAsMinecart Mount.cs:1026) | 荷兰飞盗船 1/20（Terraria.GameContent.ItemDropRules/ItemDropDatabase.cs:861） |
| 4472 | SteampunkMinecart | 蒸汽朋克矿车 / Steampunk Minecart | 36 SteampunkMinecart | 255 | Images/Extra_128 | 13/0.04/15 |  | Item.cs:37770 · Mount.cs:1028 (SetAsMinecart Mount.cs:1029) | 蒸汽朋克人商店：困难模式（Terraria/Chest.cs:2229-2232） |
| 4745 | CoffinMinecart | 棺材矿车 / Coffin Minecart | 38 CoffinMinecart | 269 | Images/Extra_150 | 13/0.04/15 | 棺材矿车：backTexture = frontTexture（前后两层同图）(Mount.cs:1033-1036) | Item.cs:39011 · Mount.cs:1031 (SetAsMinecart Mount.cs:1032) | 合成（需墓地环境）：5 铁/铅锭 + 10 木材 + 10 腐肉（或 10 椎骨）@ 砧（Terraria/Recipe.cs:12618; Terraria/Recipe.cs:12631） |
| 4763 | DiggingMoleMinecart | 挖掘鼹鼠矿车 / Digging Molecart | 39 DiggingMoleMinecart | 272 | Images/Extra_155 | 6/0.02/15 | 挖掘鼹鼠矿车：yOffset-2，frontTextureExtra=Extra_165；在地表以下沿行驶方向自动挖 1x5 格并消耗背包里的矿车轨道铺轨 (MinecartDiggerHelper.cs:7-55，调用 Player.cs:20344) | Item.cs:39186 · Mount.cs:1038 (SetAsMinecart Mount.cs:1039) | 动物学家商店：图鉴完成度 ≥25%（Terraria/Chest.cs:3332） |
| 5125 | FartMinecart | 放屁车 / Fart Kart | 51 FartMinecart | 338 | Images/Extra_246 | 13/0.04/15 | 臭屁卡丁车：verticalOffset -10, playerVerticalOffset -8；spawnDust 211；放屁粒子/音效 (Mount.cs:1101-1107) | Item.cs:41072 · Mount.cs:1101 (SetAsMinecart Mount.cs:1102) | 合成：矿车 + 放屁垫 @ 砧（Terraria/Recipe.cs:12569） |
| 5288 | TerraFartMinecart | 泰拉放屁车 / Terra Fart Kart | 53 TerraFartMinecart | 346 | Images/Extra_251 | 13/0.04/15 | 泰拉臭屁卡丁车：同 51，粒子 SparksTerraFart (Mount.cs:1109-1115) | Item.cs:41983 · Mount.cs:1109 (SetAsMinecart Mount.cs:1110) | 合成：臭屁卡丁车 + 泰拉马桶 @ 砧（Terraria/Recipe.cs:12573） |
| — | （无物品） | 木矿车 | 13 MinecartWood | 184 | Images/Mount_MinecartWood | 10/0.03/12 | 矿车栏为空时的默认矿车（Player.cs:22856-22859），撞击伤害 15+30*速度 | Mount.cs:1597 | 无需获取 |

车贴图是横向 3 帧（totalFrames 3）的精灵表，`textureWidth/Height` 取整张图尺寸；`Images/Extra_N` 指 `TextureAssets.Extra[N]`。

## 6. 移植到 TerraKnight 时需要注意的现状（来自对 mod 源码的只读调查）

- 杂项装备栏 UI 已有 5 格：宠物 / 照明宠物 / 矿车 / 坐骑 / 钩爪（`Items/Items.cs:1924-1926`，标签与空格图标在 `Items/InventoryGuiChest.cs:44-48`），但 `Inventory.FitsMisc`（`Items/Items.cs:1932-1933`）目前只接受钩爪，需要按格子放开宠物/照明宠物/矿车。
- `ItemDef.BuffType > 0` 在 `TerraMod.cs:736-740` 被当成药水喝掉并 `ConsumeSelected()`（`Items/Items.cs:2190-2201`，不看 Consumable），所以宠物物品不能直接沿用 BuffType 字段，需要单独的使用路径（可参照仆从的 `Minion`/`MinionBuff`）。
- buff 系统（`Player/PlayerBuffs.cs`）直接用原版 BuffID，已有"只要投射物存在就把 buff 维持在 18000"的仆从模式（`PlayerBuffs.cs:267-281`），宠物可以照搬：buff 存在 → 生成/维持宠物，宠物 AI 每帧检查标志。
- 加光：`World.LightMap.Add(Vector2 world, Color rgb)`（`LightMap.cs:107-111`）相当于 Lighting.AddLight；CastLightOpen 式的线段光可用多次 Add 实现。
- 贴图：`TerrariaAssets.Texture("Projectile_N" / "Item_N" / "Buff_N" / "Extra_N" / "Mount_Minecart")` 直接读玩家安装的 Terraria Content。
- mod 目前没有轨道图格（TileID 314）也没有坐骑物理；矿车需要新增轨道格子/放置/连接帧与沿轨移动。
