# 泰拉瑞亚 1.4.5 城镇 NPC：到达播报、生成条件、商店

数据来源：反编译的 Terraria 1.4.5（`D:/ClaudeTemp/claude/e------------github-----projects---/e332c8d1-bad4-4402-a7e3-1d6b4d5e6526/scratchpad/terraria-src`），`文件:行号` 相对该目录（mod 的路径相对 `强兼/`）。完整数据在同目录 `npcs.json`（生成脚本 `scripts/gen_npcs.py`，商店解析 `scripts/shops.py`）。

## 1. 播报文本、颜色和显示位置

| 场景 | 本地化键 | 英文 | 中文 | 颜色 | 代码 |
|---|---|---|---|---|---|
| 新 NPC 入住 / 旅商到来 | `Announcement.HasArrived` | {0} has arrived! | {0}已到达！ | NPCTravel (50,125,255) | Terraria/WorldGen.cs:5497 SpawnTownNPC 成功生成新 NPC 后; Terraria/WorldGen.cs:5327 SpawnTravelNPC（旅商） |
| 1.4.5 流浪 NPC 到来（没找到家） | `Announcement.HomelessArrived_0` | {0} has arrived, searching for a place to rest. | {0}已抵达，正在寻找一处歇脚的地方。 | NPCTravel (50,125,255) | WorldGen.cs:5121-5124 |
| 1.4.5 流浪 NPC 到来（没找到家） | `Announcement.HomelessArrived_1` | {0} has arrived, looking for a home! | {0}已抵达，正在寻找住处！ | NPCTravel (50,125,255) | WorldGen.cs:5121-5124 |
| 1.4.5 流浪 NPC 到来（没找到家） | `Announcement.HomelessArrived_2` | {0} has arrived, wishing for a house! | {0}已抵达，希望能有间房子住！ | NPCTravel (50,125,255) | WorldGen.cs:5121-5124 |
| 1.4.5 流浪 NPC 到来（没找到家） | `Announcement.HomelessArrived_3` | {0} has arrived, desiring shelter! | {0}已抵达，渴望得到一处庇护所！ | NPCTravel (50,125,255) | WorldGen.cs:5121-5124 |
| 1.4.5 流浪 NPC 到来（没找到家） | `Announcement.HomelessArrived_4` | {0} has arrived, hoping to settle down! | {0}已抵达，希望能在此安顿下来！ | NPCTravel (50,125,255) | WorldGen.cs:5121-5124 |
| 1.4.5 流浪 NPC 到来（没找到家） | `Announcement.HomelessArrived_5` | {0} has arrived, looking to settle nearby. | {0}已抵达，正打算在附近安家。 | NPCTravel (50,125,255) | WorldGen.cs:5121-5124 |
| 城镇 NPC 死亡 | `LegacyMisc.19` | {0} was slain... | {0}被杀死了…… | Death (255,25,25) | NPC.cs:78941-78973 |
| 渔夫/公主/城镇宠物死亡 | `LegacyMisc.36` | {0} has left! | {0}已离开！ | Death (255,25,25) | NPC.cs:78950-78955 |
| 旅商离开 / 流浪 NPC 离开 | `LegacyMisc.35` | {0} has departed! | {0}已离去！ | NPCTravel (50,125,255) | WorldGen.cs:4949, WorldGen.cs:5165 |
| 首次使用宠物许可证 | `Misc.LicenseCatUsed` 等 | — | — | World (50,255,130) | NPC.cs:81718-81741 |

- `{0}` = `NPC.GetFullNetName()`，格式键 `Game.NPCTitle`：英文 "{0} the {1}"（Andrew the Guide），中文 "{1}{0}"（职业在前：向导Andrew）。
- 颜色常量在 `Terraria.Chat/ChatColors.cs`：NPCTravel=(50,125,255)、Death=(255,25,25)、World=(50,255,130)、BossOrEvent=(175,75,255)。
- 显示位置：ChatHelper.BroadcastChatMessage (Terraria.Chat/ChatHelper.cs:46-65) → 单机/客户端 DisplayMessage → Main.NewTextMultiline (ChatHelper.cs:81-97) → RemadeChatMonitor：左下角聊天行，x=88，y=屏幕高-58-行号*22，最多显示 10 行（_showCount=10），每行未开聊天框时显示 600 tick=10 秒（ChatMessageContainer._timeLeft=600），字体 FontAssets.MouseText 带阴影；Main.NewText 同时播放 SoundID 12（菜单音）(Main.cs:65653-65657, RemadeChatMonitor.cs:25,84, ChatMessageContainer.cs:33)。服务器上走 NetTextModule 广播给所有客户端。
- 不播报的情况：NPC 只是搬家/流浪 NPC 入住已有空房（RelocatedHomeless, WorldGen.cs:5374-5384）不播报；老人 37、骷髅商人 453 不播报；被救出的 NPC（哥布林/巫师/机械师/发型师/渔夫/税收官/酒馆老板/高尔夫）在被救时只是 AI_000_TransformBoundNPC → Transform + 设 savedX（NPC.cs:45371-45375、53485-53512），不播报；之后若死亡再次入住才走 HasArrived。
- 死亡时若播报 LegacyMisc.19，还会按条件掉一块写着同一句话的墓碑（DropTombstoneTownNPC）；LegacyMisc.36 不掉。

## 2. 入住机制

- Main.UpdateTime_SpawnTownNPCs 每 7200/worldUpdateRate tick（默认 2 分钟）重算 townNPCCanSpawn[] 和 WorldGen.prioritizedTownNPCType（Main.cs:66569-67142）；优先级顺序见 priority_order。
- WorldGen.UpdateWorld 随机图格更新命中房屋墙（Main.wallHouse）时 TrySpawningTownNPC：无入侵/日食，每 npcSpawnPeriod(20*worldUpdateRate) 次一次 → SpawnTownNPC(x,y)（WorldGen.cs:75479-75491）；另有 CheckForHousesNearAPlayer 在玩家附近 300 次采样找房屋（只搬入流浪 NPC，WorldGen.cs:72193-72222）。
- SpawnTownNPC（WorldGen.cs:5330-5536）：房屋检查 StartRoomCheck/RoomNeeds/ScoreRoom（臭虫阻止）→ 若有同类型流浪 NPC 则直接给它分配家（不播报）→ 否则 IsThereASpawnablePrioritizedTownNPC 选可生成类型 → 在房屋外玩家视野外的地表找落脚点生成（找不到就在屋内）→ 播报 HasArrived → 设置 unlockedXSpawn 标志。
- 1.4.5 新增 SpawnHomelessNPC（WorldGen.cs:4956-5125，Main.cs:66068-66086 每天早上 1/(27000/dayRate) 概率调用）：白天、无入侵、prioritizedTownNPCType≠0、homelessSpawnTimeout=0（成功后设 54000）、至少 1 个有家 NPC、当前无流浪 NPC → 先尝试正常入住；不行就把该 NPC 以 homeless+homelessDespawn 生成在某户人家附近并播报 HomelessArrived_x；黄昏后玩家不在附近则 UnspawnHomelessNPC 播报 LegacyMisc.35。
- 优先级（`num42` 赋值顺序，Main.cs:66983-67140；前两项只在 infectedSeed / vampireSeed 世界生效）：树妖(20) → 动物学家(633) → 向导(22) → 商人(17) → 护士(18) → 军火商(19) → 哥布林工匠(107) → 巫师(108) → 树妖(20) → 爆破专家(38) → 巫医(228) → 蒸汽朋克人(178) → 机械师(124) → 渔夫(369) → 机器侠(209) → 海盗(229) → 服装商(54) → 发型师(353) → 染料商(207) → 油漆工(227) → 派对女孩(208) → 圣诞老人(142) → 酒馆老板(550) → 高尔夫球手(588) → 税收官(441) → 松露人(160) → 动物学家(633) → 公主(663) → 侍卫史莱姆(684) → 书呆子史莱姆(670) → 酷酷史莱姆(678) → 长者史莱姆(679) → 笨拙史莱姆(680) → 暴躁史莱姆(682) → 神秘史莱姆(683) → 天后史莱姆(681) → 兔兔(656) → 猫咪(637) → 狗狗(638)

## 3. 全部城镇 NPC

"mod" 一列：✔ = TerraKnight 已有（`src/TerraKnight/Items/TownShopData.cs` roster）。贴图：1.4 起城镇 NPC 实际用 `Images/TownNPCs/<名>_Default`（派对、微光各有变体），头像 `Images/NPC_Head_<N>`；旧版 `Images/NPC_<id>` 仍存在。

| ID | 内部名 | 中文 / 英文 | mod | 入住条件 | 死亡播报 | 贴图 / 头像 | 帧(总/额外/攻击) | 商店 |
|---|---|---|---|---|---|---|---|---|
| 22 | Guide | 向导 / Guide | ✔ | 无条件：世界里没有向导就可入住（世界生成时已放置）（Terraria/Main.cs:66827-66830） | LegacyMisc.19 | Images/TownNPCs/Guide_Default / Images/NPC_Head_1 | 26/10/5 | — |
| 17 | Merchant | 商人 / Merchant | ✔ | NPC.SpawnAllowed_Merchant()：所有在线玩家背包里的钱合计 ≥50 银币（5000 铜），或本世界商人曾经入住过（unlockedMerchantSpawn）（Terraria/NPC.cs:7202-7243） | LegacyMisc.19 | Images/TownNPCs/Merchant_Default / Images/NPC_Head_2 | 25/9/4 | #1 |
| 18 | Nurse | 护士 / Nurse |  | 商人已在世界中 且 SpawnAllowed_Nurse()：任一玩家最大生命 >100（statLifeMax/20>5），或护士曾入住过（Terraria/Main.cs:66835-66838; Terraria/NPC.cs:7185-7200） | LegacyMisc.19 | Images/TownNPCs/Nurse_Default / Images/NPC_Head_3 | 23/9/4 | — |
| 19 | ArmsDealer | 军火商 / Arms Dealer | ✔ | SpawnAllowed_ArmsDealer()：任一玩家背包有子弹或用子弹的武器（ammo/useAmmo==AmmoID.Bullet），或曾入住过（Terraria/NPC.cs:7119-7142） | LegacyMisc.19 | Images/TownNPCs/ArmsDealer_Default / Images/NPC_Head_6 | 25/9/4 | #2 |
| 20 | Dryad | 树妖 / Dryad |  | 击败克苏鲁之眼、世界吞噬怪/克苏鲁之脑、骷髅王任意一个（或 infectedSeed 世界，且在该种子世界中优先）（Terraria/Main.cs:66843-66846; Terraria/Main.cs:66995-66998） | LegacyMisc.19 | Images/TownNPCs/Dryad_Default / Images/NPC_Head_5 | 21/7/2 | #3 |
| 38 | Demolitionist | 爆破专家 / Demolitionist | ✔ | 商人已在世界中 且 SpawnAllowed_Demolitionist()：任一玩家背包有炸药类物品（ItemID.Sets.ItemsThatCountAsBombsForDemolitionistToSpawn），或曾入住过（Terraria/Main.cs:66847-66850; Terraria/NPC.cs:7094-7117） | LegacyMisc.19 | Images/TownNPCs/Demolitionist_Default / Images/NPC_Head_4 | 25/9/4 | #4 |
| 54 | Clothier | 服装商 / Clothier |  | 击败骷髅王（downedBoss3）（Terraria/Main.cs:66859-66862） | LegacyMisc.19 | Images/TownNPCs/Clothier_Default / Images/NPC_Head_7 | 23/7/2 | #5 |
| 107 | GoblinTinkerer | 哥布林工匠 / Goblin Tinkerer | ✔ | 救出被缚哥布林（savedGoblin：与地下的 BoundGoblin 105 对话即转化，AI_007_TownEntities_UpdateSavedStates NPC.cs:53485）（Terraria/Main.cs:66863-66866） | LegacyMisc.19 | Images/TownNPCs/GoblinTinkerer_Default / Images/NPC_Head_9 | 25/9/4 | #6 |
| 108 | Wizard | 巫师 / Wizard |  | 救出受缚巫师（savedWizard：困难模式洞穴层 BoundWizard 106）（Terraria/Main.cs:66871-66874） | LegacyMisc.19 | Images/TownNPCs/Wizard_Default / Images/NPC_Head_10 | 23/7/2 | #7 |
| 124 | Mechanic | 机械师 / Mechanic |  | 救出受缚机械师（savedMech：地牢 BoundMechanic 123）（Terraria/Main.cs:66875-66878） | LegacyMisc.19 | Images/TownNPCs/Mechanic_Default / Images/NPC_Head_8 | 23/9/4 | #8 |
| 142 | SantaClaus | 圣诞老人 / Santa Claus |  | 击败雪人军团（downedFrost）且处于圣诞节期间（Main.xMas）（Terraria/Main.cs:66879-66882） | LegacyMisc.19 | Images/TownNPCs/Santa_Default / Images/NPC_Head_11 | 25/9/4 | #9 |
| 160 | Truffle | 松露人 / Truffle |  | 困难模式；另外房屋必须满足 WorldGen.CheckSpecialTownNPCSpawningConditions：房屋（第一次入住时必须在地表以上，除非 unlockedTruffleSpawn 或 NoFunctionalSurface）内蘑菇图格（70/71/72/528）数 ≥ SceneMetrics.MushroomTileThreshold（Terraria/Main.cs:66899-66902; Terraria/WorldGen.cs:4890-4919） | LegacyMisc.19 | Images/TownNPCs/Truffle_Default / Images/NPC_Head_12 | 23/7/2 | #10 |
| 178 | Steampunker | 蒸汽朋克人 / Steampunker | ✔ | 击败任一机械 Boss（downedMechBossAny），或十周年（非 for the worthy）世界（Terraria/Main.cs:66883-66886） | LegacyMisc.19 | Images/TownNPCs/Steampunker_Default / Images/NPC_Head_13 | 23/9/4 | #11 |
| 207 | DyeTrader | 染料商 / Dye Trader |  | 城镇 NPC 总数 ≥4 且 SpawnAllowed_DyeTrader()：背包有染料或染料材料（item.dye>0、1107-1120、3385-3388），或装备栏/杂项栏有染料，或曾入住过（Terraria/Main.cs:66887-66890; Terraria/NPC.cs:7144-7183） | LegacyMisc.19 | Images/TownNPCs/DyeTrader_Default / Images/NPC_Head_14 | 25/9/4 | #12 |
| 208 | PartyGirl | 派对女孩 / Party Girl | ✔ | 每次检查有 1/40 概率且城镇 NPC 总数 ≥20；曾入住过（unlockedPartyGirlSpawn）则无条件（Terraria/Main.cs:66812-66821; Terraria/Main.cs:66911-66914） | LegacyMisc.19 | Images/TownNPCs/PartyGirl_Default / Images/NPC_Head_15 | 23/9/4 | #13 |
| 209 | Cyborg | 机器侠 / Cyborg | ✔ | 困难模式且击败世纪之花（Terraria/Main.cs:66903-66906） | LegacyMisc.19 | Images/TownNPCs/Cyborg_Default / Images/NPC_Head_16 | 26/10/5 | #14 |
| 227 | Painter | 油漆工 / Painter |  | 城镇 NPC 总数 ≥8（Terraria/Main.cs:66907-66910） | LegacyMisc.19 | Images/TownNPCs/Painter_Default / Images/NPC_Head_17 | 25/9/4 | #15 |
| 228 | WitchDoctor | 巫医 / Witch Doctor | ✔ | 击败蜂王（Terraria/Main.cs:66891-66894） | LegacyMisc.19 | Images/TownNPCs/WitchDoctor_Default / Images/NPC_Head_18 | 26/10/5 | #16 |
| 229 | Pirate | 海盗 / Pirate |  | 击败海盗入侵（downedPirates）（Terraria/Main.cs:66895-66898） | LegacyMisc.19 | Images/TownNPCs/Pirate_Default / Images/NPC_Head_19 | 26/10/5 | #17 |
| 353 | Stylist | 发型师 / Stylist |  | 救出被网住的发型师（savedStylist：蜘蛛洞 WebbedStylist 354）（Terraria/Main.cs:66851-66854） | LegacyMisc.19 | Images/TownNPCs/Stylist_Default / Images/NPC_Head_20 | 23/9/4 | #18 |
| 368 | TravellingMerchant | 旅商 / Traveling Merchant |  | 旅商：不走房屋入住流程。黎明后 time<27000（4:30-12:00）每 tick 有 1/(27000/dayRate*4) 概率，且城镇 NPC（不含老人/骷髅商人）≥2，非日食/入侵时 WorldGen.SpawnTravelNPC：先 SetupTravelShop 刷新商品，生成在某个有家 NPC 附近玩家视野外；time>48600（19:30）或夜晚且玩家不在附近时离开（Terraria/Main.cs:66033-66066; Terraria/WorldGen.cs:5172-5328; Terraria/WorldGen.cs:4920-4954 (UnspawnTravelNPC)） | LegacyMisc.19 | Images/TownNPCs/TravelingMerchant_Default / Images/NPC_Head_21 | 26/10/5 | #19 |
| 369 | Angler | 渔夫 / Angler |  | 救出沉睡渔夫（savedAngler：海洋 SleepingAngler 376）（Terraria/Main.cs:66855-66858） | LegacyMisc.36 | Images/TownNPCs/Angler_Default / Images/NPC_Head_22 | 23/9/4 | — |
| 441 | TaxCollector | 税收官 / Tax Collector |  | 救出税收官（savedTaxCollector：在地狱对痛苦亡魂 DemonTaxCollector 534 撒净化粉，Projectile.cs:14795-14800）（Terraria/Main.cs:66867-66870） | LegacyMisc.19 | Images/TownNPCs/TaxCollector_Default / Images/NPC_Head_23 | 25/9/4 | — |
| 453 | SkeletonMerchant | 骷髅商人 / Skeleton Merchant | ✔ | 骷髅商人：不入住。洞穴层刷怪时 1/2 * 1/35 概率（非暗影蜡烛、非水中、当前不存在）作为普通 NPC 刷出（Terraria/NPC.cs:5005-5011） | — | Images/TownNPCs/SkeletonMerchant_Default / None | 26/9/4 | #20 |
| 550 | DD2Bartender | 酒馆老板 / Tavernkeep |  | 救出酒馆老板（savedBartender：击败世吞/克脑后地表出现的 BartenderUnconscious 579）（Terraria/Main.cs:66915-66918） | LegacyMisc.19 | Images/TownNPCs/Tavernkeep_Default / Images/NPC_Head_24 | 25/9/4 | #21 |
| 588 | Golfer | 高尔夫球手 / Golfer |  | 救出高尔夫球手（savedGolfer：地下沙漠的 GolferRescue 589）（Terraria/Main.cs:66919-66922） | LegacyMisc.19 | Images/TownNPCs/Golfer_Default / Images/NPC_Head_25 | 25/9/4 | #22 |
| 633 | BestiaryGirl | 动物学家 / Zoologist | ✔ | 图鉴完成度 ≥10%（GetBestiaryProgressReport().CompletionPercent>=0.1），或 vampireSeed（非 infected）世界（Terraria/Main.cs:66923-66926） | LegacyMisc.19 | Images/TownNPCs/BestiaryGirl_Default / Images/NPC_Head_26 | 23/9/4 | #23 |
| 663 | Princess | 公主 / Princess |  | flag9：商人/护士/树妖/军火商/向导/爆破专家/服装商/巫师/哥布林/机械师/松露人/蒸汽朋克人/染料商/派对女孩/机器侠/油漆工/巫医/海盗/发型师/渔夫/税收官/酒馆老板/高尔夫球手/动物学家 全部在世界中；或十周年（非 ftw）世界；或公主曾入住过（Terraria/Main.cs:66974-66990） | LegacyMisc.36 | Images/TownNPCs/Princess_Default / Images/NPC_Head_45 | 23/7/2 | #24 |
| 637 | TownCat | 猫咪 / Cat |  | 使用猫咪许可证（boughtCat，MessageBuffer.cs:2830 UnlockOrExchangePet）（Terraria/Main.cs:66927-66930） | LegacyMisc.36 | Images/TownNPCs/Cat_Siamese 等6种 / Images/NPC_Head_27…32 | 28/18/0 | — |
| 638 | TownDog | 狗狗 / Dog |  | 使用狗狗许可证（boughtDog）（Terraria/Main.cs:66931-66934; Terraria/MessageBuffer.cs:2834） | LegacyMisc.36 | Images/TownNPCs/Dog_Labrador 等6种 / Images/NPC_Head_33…38 | 28/11/0 | — |
| 656 | TownBunny | 兔兔 / Bunny |  | 使用兔兔许可证（boughtBunny）（Terraria/Main.cs:66935-66938; Terraria/MessageBuffer.cs:2838） | LegacyMisc.36 | Images/TownNPCs/Bunny_White 等6种 / Images/NPC_Head_39…44 | 27/20/0 | — |
| 670 | TownSlimeBlue | 书呆子史莱姆 / Nerdy Slime |  | unlockedSlimeBlueSpawn：击败史莱姆王（NPC.cs:80221-80229），或使用史莱姆许可证（Misc.LicenseSlimeUsed，MessageBuffer.cs:2842）（Terraria/Main.cs:66939-66942） | LegacyMisc.36 | Images/TownNPCs/SlimeBlue_Default / Images/NPC_Head_46 | 14/6/0 | — |
| 678 | TownSlimeGreen | 酷酷史莱姆 / Cool Slime |  | 真正的派对（BirthdayParty.GenuineParty）期间，或曾入住过（unlockedSlimeGreenSpawn）（Terraria/Main.cs:66822-66826; Terraria/Main.cs:66943-66946） | LegacyMisc.36 | Images/TownNPCs/SlimeGreen_Default / Images/NPC_Head_47 | 14/6/0 | — |
| 679 | TownSlimeOld | 长者史莱姆 / Elder Slime |  | unlockedSlimeOldSpawn：带金钥匙右键"老旧摇摇箱"（BoundTownSlimeOld 685）（Terraria/Main.cs:66947-66950; Terraria/Main.cs:43800-43815; Terraria/NPC.cs:19172-19185） | LegacyMisc.36 | Images/TownNPCs/SlimeOld_Default / Images/NPC_Head_48 | 14/6/0 | — |
| 680 | TownSlimePurple | 笨拙史莱姆 / Clumsy Slime |  | unlockedSlimePurpleSpawn：击落"笨拙气球史莱姆"（BoundTownSlimePurple 686）（Terraria/Main.cs:66951-66954; Terraria/NPC.cs:82610-82622） | LegacyMisc.36 | Images/TownNPCs/SlimePurple_Default / Images/NPC_Head_49 | 14/6/0 | — |
| 681 | TownSlimeRainbow | 天后史莱姆 / Diva Slime |  | unlockedSlimeRainbowSpawn：向上投掷"闪耀史莱姆气球"（投射物 936 GelBalloon）（Terraria/Main.cs:66955-66958; Terraria/Projectile.cs:22675-22686） | LegacyMisc.36 | Images/TownNPCs/SlimeRainbow_Default / Images/NPC_Head_50 | 14/6/0 | — |
| 682 | TownSlimeRed | 暴躁史莱姆 / Surly Slime |  | unlockedSlimeRedSpawn：血月夜晚钓鱼钓起的敌怪 1/5 是暴躁史莱姆（未解锁前）（Terraria/Main.cs:66959-66962; Terraria/Projectile.cs:20927-20944） | LegacyMisc.36 | Images/TownNPCs/SlimeRed_Default / Images/NPC_Head_51 | 14/6/0 | — |
| 683 | TownSlimeYellow | 神秘史莱姆 / Mystic Slime |  | unlockedSlimeYellowSpawn：对"神秘青蛙"（BoundTownSlimeYellow 687）撒净化粉（Terraria/Main.cs:66963-66966; Terraria/Projectile.cs:14805-14821） | LegacyMisc.36 | Images/TownNPCs/SlimeYellow_Default / Images/NPC_Head_52 | 14/6/0 | — |
| 684 | TownSlimeCopper | 侍卫史莱姆 / Squire Slime |  | unlockedSlimeCopperSpawn：把铜头盔(89)或铜短剑(3507)丢到史莱姆身上（NPCID.Sets.CanConvertIntoCopperSlimeTownNPC）（Terraria/Main.cs:66967-66970; Terraria/WorldItem.cs:1091-1116; Terraria/NPC.cs:19197-19207） | LegacyMisc.36 | Images/TownNPCs/SlimeCopper_Default / Images/NPC_Head_53 | 14/6/0 | — |
| 37 | OldMan | 老人 / Old Man |  | 老人：骷髅王未击败时，若世界没有老人就在地牢入口生成（不播报）（Terraria/Main.cs:66805-66811） | — | Images/TownNPCs/OldMan_Default / None | 21/5/0 | — |

特殊：骷髅商人(453)：无到达播报；死亡分支排除 453，不播报；老人(37)：无到达/死亡播报（Old Man 生成不调用播报；死亡分支排除 37）。

## 4. TerraKnight 现状

- 已有 11 个：Guide(22)、Merchant(17)、ArmsDealer(19)、Demolitionist(38)、BestiaryGirl(633)、SkeletonMerchant(453)、GoblinTinkerer(107)、WitchDoctor(228)、PartyGirl(208)、Steampunker(178)、Cyborg(209)。
- 缺少 29 个：护士(18)·无商店、树妖(20)、服装商(54)、巫师(108)、机械师(124)、圣诞老人(142)、松露人(160)、染料商(207)、油漆工(227)、海盗(229)、发型师(353)、旅商(368)、渔夫(369)·无商店、税收官(441)·无商店、酒馆老板(550)、高尔夫球手(588)、公主(663)、猫咪(637)·无商店、狗狗(638)·无商店、兔兔(656)·无商店、书呆子史莱姆(670)·无商店、酷酷史莱姆(678)·无商店、长者史莱姆(679)·无商店、笨拙史莱姆(680)·无商店、天后史莱姆(681)·无商店、暴躁史莱姆(682)·无商店、神秘史莱姆(683)·无商店、侍卫史莱姆(684)·无商店、老人(37)·无商店。
- 名单：src/TerraKnight/Items/TownShopData.cs:6-19（gen.py 自动生成，namespace TerraKnight.Town partial class TownNpcs）
- 到达规则：TownNpcs.Place（TownNpcs.cs:204-267）：def.Stage <= 当前进度 或 Arrived 已含 → 放置；Arrived.Add 首次且非 fresh（非刚加载区域）时才提示；在 Dirtmouth 外解锁的 NPC 下次进入时静默出现。
- 到达提示（已提交版本）：TownNpcs.cs:260-264（HEAD 3cd5bc6）：UI.CombatText.Show(hero 头顶 1.6u, Lang.F("{0}已到达！", n.Name), Color(50,125,255), big:true) —— 世界空间飘字，1.1 秒
- 到达提示（工作区未提交版本）：工作区未提交改动（另一 agent 正在改，git diff 可见）：TownNpcs.cs:260-266 改为 Terraria.TerrariaText.Get(Lang.Culture,"Announcement.HasArrived") 替换 {0} 后 Net.NetChat.Add(text, Color(50,125,255))；NetChat.cs:126-129 Draw() 在单机时只要最后一行未超过 LineSeconds(10s) 也绘制 —— 即左下角聊天行，最多 10 行，每行 10 秒（NetChat.cs:25-26, 179-193）
- Terraria zh-Hans 的 Announcement.HasArrived 正是 "{0}已到达！"（与 mod 硬编码一致）。
- mod 的城镇 NPC 无生命值、不会死亡/离开，因此没有 LegacyMisc.19/35/36 的提示。
- 新增 NPC 需要改的位置：`TownShopData.cs` roster（gen.py 生成）、`Town/TownShopExtra.cs`（可选）、`TownNpcs.Dialog` / `GivenNames` switch、`FrameCount` / `ExtraFrames` / `AddHeight`（TownNpcs.cs:686-732）、贴图 `TownNPCs/<Profile>_Default`、家位上限 16（TownNpcs.cs:232）。

## 5. 缺失 NPC 的原版商店

来自 `Chest.SetupShop(int type)`（Chest.cs:1354-3537）。条件为原样 C# 表达式（多个条件为嵌套的 if，需同时满足）。`num < 39` 之类的格数限制已省略。除旅商(19)/骷髅商人(20)/酒馆老板(21)外，所有商店末尾：若附近有≥2 个 NPC 满足幸福度（TeleportPylonsSystem.DoesPositionHaveEnoughNPCs(2,…)）且不在腐化/猩红，则按所处环境追加对应晶塔（森林4876/雪原4920/沙漠4919/地狱5652/洞穴4917/海洋4918/丛林4875/神圣4916/蘑菇4921）（Terraria/Chest.cs:3466-3533）。

### 树妖 Dryad（NPC 20，商店 #3，Terraria/Chest.cs:1562-1739 (SetupShop case 3)）

| ItemID | 内部名 | 中文 / 英文 | 条件 | 价格 | 行 |
|---|---|---|---|---|---|
| 2886 | ViciousPowder | 毒粉 / Vicious Powder | `Main.bloodMoon` ∧ `WorldGen.crimson` ∧ `!Main.remixWorld \|\| (Main.tenthAnniversaryWorld && !Main.getGoodWorld)` |  | 1569 |
| 2171 | CrimsonSeeds | 猩红种子 / Crimson Seeds | `Main.bloodMoon` ∧ `WorldGen.crimson` |  | 1572 |
| 4508 | CrimsonGrassEcho | 猩红草墙 / Crimson Grass Wall | `Main.bloodMoon` ∧ `WorldGen.crimson` |  | 1574 |
| 67 | VilePowder | 魔粉 / Vile Powder | `Main.bloodMoon` ∧ `!(WorldGen.crimson)` ∧ `!Main.remixWorld \|\| (Main.tenthAnniversaryWorld && !Main.getGoodWorld)` |  | 1581 |
| 59 | CorruptSeeds | 腐化种子 / Corrupt Seeds | `Main.bloodMoon` ∧ `!(WorldGen.crimson)` |  | 1584 |
| 4504 | CorruptGrassEcho | 腐化草墙 / Corrupt Grass Wall | `Main.bloodMoon` ∧ `!(WorldGen.crimson)` |  | 1586 |
| 66 | PurificationPowder | 净化粉 / Purification Powder | `!(Main.bloodMoon)` ∧ `!Main.remixWorld \|\| Main.infectedSeed \|\| (Main.tenthAnniversaryWorld && !Main.getGoodWorld)` |  | 1594 |
| 62 | GrassSeeds | 草种子 / Grass Seeds | `!(Main.bloodMoon)` |  | 1597 |
| 63 | Sunflower | 向日葵 / Sunflower | `!(Main.bloodMoon)` |  | 1599 |
| 745 | GrassWall | 草墙 / Grass Wall | `!(Main.bloodMoon)` |  | 1601 |
| 59 | CorruptSeeds | 腐化种子 / Corrupt Seeds | `Main.hardMode && Main.player[Main.myPlayer].ZoneGraveyard` ∧ `WorldGen.crimson` |  | 1608 |
| 2171 | CrimsonSeeds | 猩红种子 / Crimson Seeds | `Main.hardMode && Main.player[Main.myPlayer].ZoneGraveyard` ∧ `!(WorldGen.crimson)` |  | 1612 |
| 27 | Acorn | 橡实 / Acorn |  |  | 1616 |
| 5309 | DontHurtNatureBook | 环境保护指南 / Guide to Environmental Preservation |  |  | 1618 |
| 114 | DirtRod | 土魔杖 / Dirt Rod |  |  | 1620 |
| 1828 | PumpkinSeed | 南瓜子 / Pumpkin Seed |  |  | 1622 |
| 747 | FlowerWall | 花墙 / Flower Wall |  |  | 1624 |
| 746 | JungleWall | 丛林墙 / Jungle Wall | `Main.hardMode` |  | 1628 |
| 369 | HallowedSeeds | 神圣种子 / Hallowed Seeds | `Main.hardMode` |  | 1633 |
| 4505 | HallowedGrassEcho | 神圣草墙 / Hallowed Grass Wall | `Main.hardMode` |  | 1638 |
| 5214 | AshGrassSeeds | 灰烬草种子 / Ash Grass Seeds | `Main.player[Main.myPlayer].ZoneUnderworldHeight` |  | 1643 |
| 194 | MushroomGrassSeeds | 蘑菇草种子 / Mushroom Grass Seeds | `!(Main.player[Main.myPlayer].ZoneUnderworldHeight) && (Main.player[Main.myPlayer].ZoneGlowshroom)` |  | 1647 |
| 1853 | DryadCoverings | 树妖遮体服 / Dryad Coverings | `Main.halloween` |  | 1651 |
| 1854 | DryadLoincloth | 树妖腰布 / Dryad Loincloth | `Main.halloween` |  | 1653 |
| 3215 | DayBloomPlanterBox | 太阳花种植盆 / Daybloom Planter Box |  |  | 1656 |
| 3216 | MoonglowPlanterBox | 月光草种植盆 / Moonglow Planter Box |  |  | 1657 |
| 3219 | BlinkrootPlanterBox | 闪耀根种植盆 / Blinkroot Planter Box |  |  | 1658 |
| 3218 | CrimsonPlanterBox | 死亡草种植盆 / Deathweed Planter Box | `WorldGen.crimson` |  | 1661 |
| 3217 | CorruptPlanterBox | 死亡草种植盆 / Deathweed Planter Box | `!(WorldGen.crimson)` |  | 1665 |
| 3220 | WaterleafPlanterBox | 水叶草种植盆 / Waterleaf Planter Box |  |  | 1667 |
| 3221 | ShiverthornPlanterBox | 寒颤棘种植盆 / Shiverthorn Planter Box |  |  | 1668 |
| 3222 | FireBlossomPlanterBox | 火焰花种植盆 / Fireblossom Planter Box |  |  | 1669 |
| 4047 | FlowerPacketWhite | 白花种子 / White Flower Seeds |  |  | 1670 |
| 4045 | FlowerPacketYellow | 黄花种子 / Yellow Flower Seeds |  |  | 1671 |
| 4044 | FlowerPacketRed | 红花种子 / Red Flower Seeds |  |  | 1672 |
| 4043 | FlowerPacketPink | 粉花种子 / Pink Flower Seeds |  |  | 1673 |
| 4042 | FlowerPacketMagenta | 洋红花种子 / Magenta Flower Seeds |  |  | 1674 |
| 4046 | FlowerPacketViolet | 蓝紫花种子 / Violet Flower Seeds |  |  | 1675 |
| 4041 | FlowerPacketBlue | 蓝花种子 / Blue Flower Seeds |  |  | 1676 |
| 4241 | FlowerPacketWild | 野花种子 / Wild Flower Seeds |  |  | 1677 |
| 4048 | FlowerPacketTallGrass | 高茎草种子 / Tall Grass Seeds |  |  | 1678 |
| 4430 | PottedForestCedar | 盆栽森林雪松 / Potted Forest Cedar | `Main.hardMode` ∧ `Main.moonPhase / 2 == 0` |  | 1684 |
| 4431 | PottedJungleCedar | 盆栽丛林雪松 / Potted Jungle Cedar | `Main.hardMode` ∧ `Main.moonPhase / 2 == 0` |  | 1685 |
| 4432 | PottedHallowCedar | 盆栽神圣雪松 / Potted Hallow Cedar | `Main.hardMode` ∧ `Main.moonPhase / 2 == 0` |  | 1686 |
| 4433 | PottedForestTree | 盆栽森林树 / Potted Forest Tree | `Main.hardMode` ∧ `Main.moonPhase / 2 == 1` |  | 1689 |
| 4434 | PottedJungleTree | 盆栽丛林树 / Potted Jungle Tree | `Main.hardMode` ∧ `Main.moonPhase / 2 == 1` |  | 1690 |
| 4435 | PottedHallowTree | 盆栽神圣树 / Potted Hallow Tree | `Main.hardMode` ∧ `Main.moonPhase / 2 == 1` |  | 1691 |
| 4436 | PottedForestPalm | 盆栽森林棕榈 / Potted Forest Palm | `Main.hardMode` ∧ `Main.moonPhase / 2 == 2` |  | 1694 |
| 4437 | PottedJunglePalm | 盆栽丛林棕榈 / Potted Jungle Palm | `Main.hardMode` ∧ `Main.moonPhase / 2 == 2` |  | 1695 |
| 4438 | PottedHallowPalm | 盆栽神圣棕榈 / Potted Hallow Palm | `Main.hardMode` ∧ `Main.moonPhase / 2 == 2` |  | 1696 |
| 4439 | PottedForestBamboo | 盆栽森林竹 / Potted Forest Bamboo | `Main.hardMode` ∧ `default` |  | 1699 |
| 4440 | PottedJungleBamboo | 盆栽丛林竹 / Potted Jungle Bamboo | `Main.hardMode` ∧ `default` |  | 1700 |
| 4441 | PottedHallowBamboo | 盆栽神圣竹 / Potted Hallow Bamboo | `Main.hardMode` ∧ `default` |  | 1701 |
| 4430 | PottedForestCedar | 盆栽森林雪松 / Potted Forest Cedar | `!(Main.hardMode)` ∧ `Main.moonPhase / 2 == 0` |  | 1710 |
| 4431 | PottedJungleCedar | 盆栽丛林雪松 / Potted Jungle Cedar | `!(Main.hardMode)` ∧ `Main.moonPhase / 2 == 0` |  | 1711 |
| 4433 | PottedForestTree | 盆栽森林树 / Potted Forest Tree | `!(Main.hardMode)` ∧ `Main.moonPhase / 2 == 1` |  | 1714 |
| 4434 | PottedJungleTree | 盆栽丛林树 / Potted Jungle Tree | `!(Main.hardMode)` ∧ `Main.moonPhase / 2 == 1` |  | 1715 |
| 4436 | PottedForestPalm | 盆栽森林棕榈 / Potted Forest Palm | `!(Main.hardMode)` ∧ `Main.moonPhase / 2 == 2` |  | 1718 |
| 4437 | PottedJunglePalm | 盆栽丛林棕榈 / Potted Jungle Palm | `!(Main.hardMode)` ∧ `Main.moonPhase / 2 == 2` |  | 1719 |
| 4439 | PottedForestBamboo | 盆栽森林竹 / Potted Forest Bamboo | `!(Main.hardMode)` ∧ `default` |  | 1722 |
| 4440 | PottedJungleBamboo | 盆栽丛林竹 / Potted Jungle Bamboo | `!(Main.hardMode)` ∧ `default` |  | 1723 |
| 8 | Torch | 火把 / Torch | `!Main.hardMode && Main.vampireSeed && Main.infectedSeed` |  | 1729 |
| 4386 | CrimsonTorch | 猩红火把 / Crimson Torch | `!Main.hardMode && Main.vampireSeed && Main.infectedSeed` ∧ `WorldGen.crimson` |  | 1732 |
| 4385 | CorruptTorch | 腐化火把 / Corrupt Torch | `!Main.hardMode && Main.vampireSeed && Main.infectedSeed` ∧ `!(WorldGen.crimson)` |  | 1736 |

### 服装商 Clothier（NPC 54，商店 #5，Terraria/Chest.cs:1808-1982 (SetupShop case 5)）

| ItemID | 内部名 | 中文 / 英文 | 条件 | 价格 | 行 |
|---|---|---|---|---|---|
| 254 | BlackThread | 黑线 / Black Thread |  |  | 1809 |
| 981 | PinkThread | 粉线 / Pink Thread |  |  | 1811 |
| 242 | SummerHat | 夏日草帽 / Summer Hat | `Main.dayTime` |  | 1815 |
| 245 | PlumbersShirt | 管道工衣 / Plumber's Shirt | `Main.moonPhase == 0` |  | 1820 |
| 246 | PlumbersPants | 管道工背带裤 / Plumber's Pants | `Main.moonPhase == 0` |  | 1822 |
| 1288 | WhiteTuxedoShirt | 白西装衣 / White Tuxedo Shirt | `Main.moonPhase == 0` ∧ `!Main.dayTime` |  | 1826 |
| 1289 | WhiteTuxedoPants | 白西装裤 / White Tuxedo Pants | `Main.moonPhase == 0` ∧ `!Main.dayTime` |  | 1827 |
| 325 | TheDoctorsShirt | 博士衣 / The Doctor's Shirt | `!(Main.moonPhase == 0) && (Main.moonPhase == 1)` |  | 1832 |
| 326 | TheDoctorsPants | 博士裤 / The Doctor's Pants | `!(Main.moonPhase == 0) && (Main.moonPhase == 1)` |  | 1834 |
| 269 | FamiliarShirt | 便装衣 / Familiar Shirt |  |  | 1837 |
| 270 | FamiliarPants | 便装裤 / Familiar Pants |  |  | 1839 |
| 271 | FamiliarWig | 便装假发 / Familiar Wig |  |  | 1841 |
| 503 | ClownHat | 小丑帽 / Clown Hat | `NPC.downedClown` |  | 1845 |
| 504 | ClownShirt | 小丑衣 / Clown Shirt | `NPC.downedClown` |  | 1847 |
| 505 | ClownPants | 小丑裤 / Clown Pants | `NPC.downedClown` |  | 1849 |
| 322 | MimeMask | 丑角面具 / Mime Mask | `Main.bloodMoon` |  | 1854 |
| 3362 | FallenTuxedoShirt | 堕落西装衣 / Fallen Tuxedo Shirt | `Main.bloodMoon` ∧ `!Main.dayTime` |  | 1858 |
| 3363 | FallenTuxedoPants | 堕落西装裤 / Fallen Tuxedo Pants | `Main.bloodMoon` ∧ `!Main.dayTime` |  | 1859 |
| 2856 | WhiteLunaticHood | 日耀邪教徒兜帽 / Solar Cultist Hood | `NPC.downedAncientCultist` ∧ `Main.dayTime` |  | 1866 |
| 2858 | WhiteLunaticRobe | 日耀邪教徒长袍 / Solar Cultist Robe | `NPC.downedAncientCultist` ∧ `Main.dayTime` |  | 1867 |
| 2857 | BlueLunaticHood | 月亮邪教徒兜帽 / Lunar Cultist Hood | `NPC.downedAncientCultist` ∧ `!(Main.dayTime)` |  | 1871 |
| 2859 | BlueLunaticRobe | 月亮邪教徒长袍 / Lunar Cultist Robe | `NPC.downedAncientCultist` ∧ `!(Main.dayTime)` |  | 1872 |
| 3242 | TaxCollectorHat | 税收官帽 / Tax Collector's Hat | `NPC.AnyNPCs(441)` |  | 1877 |
| 3243 | TaxCollectorSuit | 税收官衣服 / Tax Collector's Suit | `NPC.AnyNPCs(441)` |  | 1878 |
| 3244 | TaxCollectorPants | 税收官裤 / Tax Collector's Pants | `NPC.AnyNPCs(441)` |  | 1879 |
| 4685 | UndertakerHat | 掘墓者帽 / Gravedigger Hat | `Main.player[Main.myPlayer].ZoneGraveyard` |  | 1883 |
| 4686 | UndertakerCoat | 掘墓者外套 / Gravedigger Coat | `Main.player[Main.myPlayer].ZoneGraveyard` |  | 1884 |
| 4704 | FuneralHat | 葬礼帽 / Funeral Hat | `Main.player[Main.myPlayer].ZoneGraveyard` |  | 1885 |
| 4705 | FuneralCoat | 葬礼外套 / Funeral Coat | `Main.player[Main.myPlayer].ZoneGraveyard` |  | 1886 |
| 4706 | FuneralPants | 葬礼裤 / Funeral Pants | `Main.player[Main.myPlayer].ZoneGraveyard` |  | 1887 |
| 4707 | TragicUmbrella | 悲剧雨伞 / Tragic Umbrella | `Main.player[Main.myPlayer].ZoneGraveyard` |  | 1888 |
| 4708 | VictorianGothHat | 维多利亚哥特帽 / Victorian Goth Hat | `Main.player[Main.myPlayer].ZoneGraveyard` |  | 1889 |
| 4709 | VictorianGothDress | 维多利亚哥特裙 / Victorian Goth Dress | `Main.player[Main.myPlayer].ZoneGraveyard` |  | 1890 |
| 1429 | Beanie | 小便帽 / Beanie | `Main.player[Main.myPlayer].ZoneSnow` |  | 1894 |
| 1740 | GuyFawkesMask | 盖伊·福克斯面具 / Guy Fawkes Mask | `Main.halloween` |  | 1899 |
| 869 | TamOShanter | 苏格兰便帽 / Tam O' Shanter | `Main.hardMode` ∧ `Main.moonPhase == 2` |  | 1906 |
| 4994 | GraduationCapBlue | 蓝毕业帽 / Blue Graduation Cap | `Main.hardMode` ∧ `Main.moonPhase == 3` |  | 1911 |
| 4997 | GraduationGownBlue | 蓝毕业礼服 / Blue Graduation Gown | `Main.hardMode` ∧ `Main.moonPhase == 3` |  | 1913 |
| 864 | Tiara | 头冠 / Tiara | `Main.hardMode` ∧ `Main.moonPhase == 4` |  | 1918 |
| 865 | PrincessDress | 公主裙 / Princess Dress | `Main.hardMode` ∧ `Main.moonPhase == 4` |  | 1920 |
| 4995 | GraduationCapMaroon | 褐红毕业帽 / Maroon Graduation Cap | `Main.hardMode` ∧ `Main.moonPhase == 5` |  | 1925 |
| 4998 | GraduationGownMaroon | 褐红毕业礼服 / Maroon Graduation Gown | `Main.hardMode` ∧ `Main.moonPhase == 5` |  | 1927 |
| 873 | CowboyHat | 牛仔帽 / Cowboy Hat | `Main.hardMode` ∧ `Main.moonPhase == 6` |  | 1932 |
| 874 | CowboyJacket | 牛仔夹克 / Cowboy Jacket | `Main.hardMode` ∧ `Main.moonPhase == 6` |  | 1934 |
| 875 | CowboyPants | 牛仔裤 / Cowboy Pants | `Main.hardMode` ∧ `Main.moonPhase == 6` |  | 1936 |
| 4996 | GraduationCapBlack | 黑毕业帽 / Black Graduation Cap | `Main.hardMode` ∧ `Main.moonPhase == 7` |  | 1941 |
| 4999 | GraduationGownBlack | 黑毕业礼服 / Black Graduation Gown | `Main.hardMode` ∧ `Main.moonPhase == 7` |  | 1943 |
| 1275 | BallaHat | 巴拉帽 / Balla Hat | `NPC.downedFrost` ∧ `Main.dayTime` |  | 1951 |
| 1276 | GangstaHat | 黑帮帽 / Gangsta Hat | `NPC.downedFrost` ∧ `!(Main.dayTime)` |  | 1956 |
| 3246 | ClothierJacket | 服装商夹克 / Clothier's Jacket | `Main.halloween` |  | 1962 |
| 3247 | ClothierPants | 服装商裤 / Clothier's Pants | `Main.halloween` |  | 1963 |
| 3730 | PartyBundleOfBalloonsAccessory | 派对气球束 / Bundled Party Balloons | `BirthdayParty.PartyIsUp` |  | 1967 |
| 3731 | PartyBalloonAnimal | 气球兔兔 / Balloon Animal | `BirthdayParty.PartyIsUp` |  | 1968 |
| 3733 | FlowerBoyHat | 呆萌向日葵花瓣头盔 / Silly Sunflower Petals | `BirthdayParty.PartyIsUp` |  | 1969 |
| 3734 | FlowerBoyShirt | 呆萌向日葵上衣 / Silly Sunflower Tops | `BirthdayParty.PartyIsUp` |  | 1970 |
| 3735 | FlowerBoyPants | 呆萌向日葵裤装 / Silly Sunflower Bottoms | `BirthdayParty.PartyIsUp` |  | 1971 |
| 4744 | HunterCloak | 猎人斗篷 / Hunter Cloak | `num < 38 && Main.LocalPlayer.golferScoreAccumulated >= 2000` |  | 1975 |
| 5308 | PlacePainting | r/Terraria / r/Terraria |  |  | 1977 |
| 5630 | PaintingRPlace2023 | r/泰拉瑞亚2023 / r/Terraria 2023 | `num < 38` |  | 1980 |

### 巫师 Wizard（NPC 108，商店 #7，Terraria/Chest.cs:1998-2026 (SetupShop case 7)）

| ItemID | 内部名 | 中文 / 英文 | 条件 | 价格 | 行 |
|---|---|---|---|---|---|
| 487 | CrystalBall | 水晶球 / Crystal Ball |  |  | 1999 |
| 496 | IceRod | 冰雪魔杖 / Ice Rod |  |  | 2001 |
| 500 | GreaterManaPotion | 强效魔力药水 / Greater Mana Potion |  |  | 2003 |
| 507 | Bell | 铃铛 / Bell |  |  | 2005 |
| 508 | Harp | 竖琴 / Harp |  |  | 2007 |
| 531 | SpellTome | 魔法书 / Spell Tome |  |  | 2009 |
| 149 | Book | 书 / Book |  |  | 2011 |
| 576 | MusicBox | 八音盒 / Music Box |  |  | 2013 |
| 3186 | EmptyDropper | 空滴管 / Empty Dropper |  |  | 2015 |
| 5461 | DeadCellsKillingDeck | 击杀牌叠 / Killing Deck | `Main.hardMode && Main.bloodMoon` |  | 2019 |
| 1739 | WizardsHat | 巫师之帽 / Wizard's Hat | `Main.halloween` |  | 2023 |

### 机械师 Mechanic（NPC 124，商店 #8，Terraria/Chest.cs:2027-2084 (SetupShop case 8)）

| ItemID | 内部名 | 中文 / 英文 | 条件 | 价格 | 行 |
|---|---|---|---|---|---|
| 509 | Wrench | 红扳手 / Red Wrench |  |  | 2028 |
| 850 | BlueWrench | 蓝扳手 / Blue Wrench |  |  | 2030 |
| 851 | GreenWrench | 绿扳手 / Green Wrench |  |  | 2032 |
| 3612 | YellowWrench | 黄扳手 / Yellow Wrench |  |  | 2034 |
| 510 | WireCutter | 钢丝钳 / Wire Cutter |  |  | 2036 |
| 530 | Wire | 电线 / Wire |  |  | 2038 |
| 513 | Lever | 控制杆 / Lever |  |  | 2040 |
| 538 | Switch | 开关 / Switch |  |  | 2042 |
| 529 | RedPressurePlate | 红压力板 / Red Pressure Plate |  |  | 2044 |
| 541 | GreenPressurePlate | 绿压力板 / Green Pressure Plate |  |  | 2046 |
| 542 | GrayPressurePlate | 灰压力板 / Gray Pressure Plate |  |  | 2048 |
| 543 | BrownPressurePlate | 棕压力板 / Brown Pressure Plate |  |  | 2050 |
| 852 | BluePressurePlate | 蓝压力板 / Blue Pressure Plate |  |  | 2052 |
| 853 | YellowPressurePlate | 黄压力板 / Yellow Pressure Plate |  |  | 2054 |
| 4261 | OrangePressurePlate | 橙压力板 / Orange Pressure Plate |  |  | 2056 |
| 3707 | ProjectilePressurePad | 青绿压力垫板 / Teal Pressure Pad |  |  | 2057 |
| 2739 | BoosterTrack | 增速轨道 / Booster Track |  |  | 2058 |
| 849 | Actuator | 致动器 / Actuator |  |  | 2060 |
| 1263 | Teleporter | 传送机 / Teleporter |  |  | 2062 |
| 3616 | WirePipe | 分线盒 / Junction Box |  |  | 2063 |
| 3725 | PixelBox | 像素盒 / Pixel Box |  |  | 2064 |
| 2799 | LaserRuler | 机械标尺 / Mechanical Ruler |  |  | 2065 |
| 3619 | MechanicalLens | 机械透镜 / Mechanical Lens |  |  | 2066 |
| 3627 | EngineeringHelmet | 工程头盔 / Engineering Helmet |  |  | 2067 |
| 3629 | WireBulb | 彩线灯泡 / Wire Bulb |  |  | 2068 |
| 585 | Timer5Second | 5秒计时器 / 5 Second Timer |  |  | 2069 |
| 584 | Timer3Second | 3秒计时器 / 3 Second Timer |  |  | 2070 |
| 583 | Timer1Second | 1秒计时器 / 1 Second Timer |  |  | 2071 |
| 4484 | TimerOneHalfSecond | 1/2秒计时器 / 1/2 Second Timer |  |  | 2072 |
| 4485 | TimerOneFourthSecond | 1/4秒计时器 / 1/4 Second Timer |  |  | 2073 |
| 4409 | SpectreGoggles | 幽灵护目镜 / Spectre Goggles | `Main.player[Main.myPlayer].ZoneGraveyard` |  | 2076 |
| 2295 | MechanicsRod | 机械师钓竿 / Mechanic's Rod | `NPC.AnyNPCs(369) && (Main.moonPhase == 1 \|\| Main.moonPhase == 3 \|\| Main.moonPhase == 5 \|\| Main.moonPhase == 7)` |  | 2081 |

### 圣诞老人 Santa Claus（NPC 142，商店 #9，Terraria/Chest.cs:2085-2105 (SetupShop case 9)）

| ItemID | 内部名 | 中文 / 英文 | 条件 | 价格 | 行 |
|---|---|---|---|---|---|
| 588 | SantaHat | 圣诞帽 / Santa Hat |  |  | 2087 |
| 589 | SantaShirt | 圣诞衣 / Santa Shirt |  |  | 2089 |
| 590 | SantaPants | 圣诞裤 / Santa Pants |  |  | 2091 |
| 597 | RedLight | 红灯 / Red Light |  |  | 2093 |
| 598 | GreenLight | 绿灯 / Green Light |  |  | 2095 |
| 596 | BlueLight | 蓝灯 / Blue Light |  |  | 2097 |
| 1873 | ChristmasTree | 圣诞树 / Christmas Tree |  |  | 2101 |
| 1874 | StarTopper1 | 星星顶饰1 / Star Topper 1 |  |  | 2101 |
| 1875 | StarTopper2 | 星星顶饰2 / Star Topper 2 |  |  | 2101 |
| 1876 | StarTopper3 | 星星顶饰3 / Star Topper 3 |  |  | 2101 |
| 1877 | BowTopper | 蝴蝶结顶饰 / Bow Topper |  |  | 2101 |
| 1878 | WhiteGarland | 白花环 / White Garland |  |  | 2101 |
| 1879 | WhiteAndRedGarland | 白红花环 / White and Red Garland |  |  | 2101 |
| 1880 | RedGardland | 红花环 / Red Garland |  |  | 2101 |
| 1881 | RedAndGreenGardland | 红绿花环 / Red and Green Garland |  |  | 2101 |
| 1882 | GreenGardland | 绿花环 / Green Garland |  |  | 2101 |
| 1883 | GreenAndWhiteGarland | 绿白花环 / Green and White Garland |  |  | 2101 |
| 1884 | MulticoloredBulb | 五彩灯泡 / Multicolored Bulb |  |  | 2101 |
| 1885 | RedBulb | 红灯泡 / Red Bulb |  |  | 2101 |
| 1886 | YellowBulb | 黄灯泡 / Yellow Bulb |  |  | 2101 |
| 1887 | GreenBulb | 绿灯泡 / Green Bulb |  |  | 2101 |
| 1888 | RedAndGreenBulb | 红绿灯泡 / Red and Green Bulb |  |  | 2101 |
| 1889 | YellowAndGreenBulb | 黄绿灯泡 / Yellow and Green Bulb |  |  | 2101 |
| 1890 | RedAndYellowBulb | 红黄灯泡 / Red and Yellow Bulb |  |  | 2101 |
| 1891 | WhiteBulb | 白灯泡 / White Bulb |  |  | 2101 |
| 1892 | WhiteAndRedBulb | 白红灯泡 / White and Red Bulb |  |  | 2101 |
| 1893 | WhiteAndYellowBulb | 白黄灯泡 / White and Yellow Bulb |  |  | 2101 |
| 1894 | WhiteAndGreenBulb | 白绿灯泡 / White and Green Bulb |  |  | 2101 |
| 1895 | MulticoloredLights | 五彩串灯 / Multicolored Lights |  |  | 2101 |
| 1896 | RedLights | 红串灯 / Red Lights |  |  | 2101 |
| 1897 | GreenLights | 绿串灯 / Green Lights |  |  | 2101 |
| 1898 | BlueLights | 蓝串灯 / Blue Lights |  |  | 2101 |
| 1899 | YellowLights | 黄串灯 / Yellow Lights |  |  | 2101 |
| 1900 | RedAndYellowLights | 红黄串灯 / Red and Yellow Lights |  |  | 2101 |
| 1901 | RedAndGreenLights | 红绿串灯 / Red and Green Lights |  |  | 2101 |
| 1902 | YellowAndGreenLights | 黄绿串灯 / Yellow and Green Lights |  |  | 2101 |
| 1903 | BlueAndGreenLights | 蓝绿串灯 / Blue and Green Lights |  |  | 2101 |
| 1904 | RedAndBlueLights | 红蓝串灯 / Red and Blue Lights |  |  | 2101 |
| 1905 | BlueAndYellowLights | 蓝黄串灯 / Blue and Yellow Lights |  |  | 2101 |

### 松露人 Truffle（NPC 160，商店 #10，Terraria/Chest.cs:2106-2128 (SetupShop case 10)）

| ItemID | 内部名 | 中文 / 英文 | 条件 | 价格 | 行 |
|---|---|---|---|---|---|
| 756 | MushroomSpear | 蘑菇长矛 / Mushroom Spear | `NPC.downedMechBossAny` |  | 2109 |
| 787 | Hammush | 蘑菇锤 / Hammush | `NPC.downedMechBossAny` |  | 2111 |
| 868 | MushroomCap | 蘑菇帽 / Mushroom Cap |  |  | 2114 |
| 1551 | Autohammer | 自动锤炼机 / Autohammer | `NPC.downedPlantBoss` |  | 2118 |
| 1181 | StrangeGlowingMushroom | 奇异发光蘑菇 / Strange Glowing Mushroom |  |  | 2121 |
| 5231 | MySon | 我的儿子 / My Son |  |  | 2123 |
| 783 | DarkBlueSolution | 深蓝溶液 / Dark Blue Solution | `!Main.remixWorld \|\| (Main.tenthAnniversaryWorld && !Main.getGoodWorld)` |  | 2126 |

### 染料商 Dye Trader（NPC 207，商店 #12，Terraria/Chest.cs:2240-2267 (SetupShop case 12)）

| ItemID | 内部名 | 中文 / 英文 | 条件 | 价格 | 行 |
|---|---|---|---|---|---|
| 1120 | DyeVat | 染缸 / Dye Vat |  |  | 2241 |
| 5920 | EasterBlock | 复活节物块 / Easter Block |  |  | 2242 |
| 3248 | DyeTraderTurban | 染料商头巾 / Dye Trader's Turban | `Main.halloween` |  | 2245 |
| 1741 | DyeTraderRobe | 染料商长袍 / Dye Trader Robe | `Main.halloween` |  | 2246 |
| 1037 | SilverDye | 银染料 / Silver Dye |  |  | 2248 |
| 2874 | BrownDye | 棕染料 / Brown Dye |  |  | 2249 |
| 1969 | TeamDye | 团队染料 / Team Dye | `Main.netMode == 1` |  | 2252 |
| 2871 | ShadowDye | 暗影染料 / Shadow Dye | `Main.moonPhase == 0` |  | 2256 |
| 2872 | NegativeDye | 阴暗染料 / Negative Dye | `Main.moonPhase == 0` |  | 2257 |
| 4663 | BloodbathDye | 大屠杀染料 / Bloodbath Dye | `!Main.dayTime && Main.bloodMoon` |  | 2261 |
| 4662 | FogboundDye | 灰雾染料 / Fogbound Dye | `Main.player[Main.myPlayer].ZoneGraveyard` |  | 2265 |

### 油漆工 Painter（NPC 227，商店 #15，Terraria/Chest.cs:2437-2476 (SetupShop case 15)）

| ItemID | 内部名 | 中文 / 英文 | 条件 | 价格 | 行 |
|---|---|---|---|---|---|
| 1071 | Paintbrush | 漆刷 / Paintbrush |  |  | 2439 |
| 1072 | PaintRoller | 涂漆滚刷 / Paint Roller |  |  | 2441 |
| 1100 | PaintScraper | 漆铲 / Paint Scraper |  |  | 2443 |
| 1073 | RedPaint | 红漆 / Red Paint |  |  | 2447 |
| 1074 | OrangePaint | 橙漆 / Orange Paint |  |  | 2447 |
| 1075 | YellowPaint | 黄漆 / Yellow Paint |  |  | 2447 |
| 1076 | LimePaint | 橙绿漆 / Lime Paint |  |  | 2447 |
| 1077 | GreenPaint | 绿漆 / Green Paint |  |  | 2447 |
| 1078 | TealPaint | 青绿漆 / Teal Paint |  |  | 2447 |
| 1079 | CyanPaint | 青漆 / Cyan Paint |  |  | 2447 |
| 1080 | SkyBluePaint | 天蓝漆 / Sky Blue Paint |  |  | 2447 |
| 1081 | BluePaint | 蓝漆 / Blue Paint |  |  | 2447 |
| 1082 | PurplePaint | 紫漆 / Purple Paint |  |  | 2447 |
| 1083 | VioletPaint | 蓝紫漆 / Violet Paint |  |  | 2447 |
| 1084 | PinkPaint | 粉漆 / Pink Paint |  |  | 2447 |
| 1097 | BlackPaint | 黑漆 / Black Paint |  |  | 2450 |
| 1099 | GrayPaint | 灰漆 / Gray Paint |  |  | 2452 |
| 1098 | WhitePaint | 白漆 / White Paint |  |  | 2454 |
| 1966 | BrownPaint | 棕漆 / Brown Paint |  |  | 2456 |
| 1967 | ShadowPaint | 暗影漆 / Shadow Paint | `Main.hardMode` |  | 2460 |
| 1968 | NegativePaint | 反色漆 / Negative Paint | `Main.hardMode` |  | 2462 |
| 4668 | GlowPaint | 夜明涂料 / Illuminant Coating | `Main.player[Main.myPlayer].ZoneGraveyard` |  | 2467 |
| 5344 | EchoCoating | 回声涂料 / Echo Coating | `Main.player[Main.myPlayer].ZoneGraveyard` ∧ `NPC.downedPlantBoss \|\| NPC.AnyNPCs(124)` |  | 2471 |

油漆工第二个商店"装饰"（商店 #25，Terraria/Chest.cs:2477-2608 (SetupShop case 25, 按钮 GameUI.PainterDecor)）：

| ItemID | 内部名 | 中文 / 英文 | 条件 | 行 |
|---|---|---|---|---|
| 1948 | ChristmasTreeWallpaper | 圣诞树壁纸 / Christmas Tree Wallpaper | `Main.xMas` | 2484 |
| 1949 | OrnamentWallpaper | 装饰壁纸 / Ornament Wallpaper | `Main.xMas` | 2484 |
| 1950 | CandyCaneWallpaper | 糖棒壁纸 / Candy Cane Wallpaper | `Main.xMas` | 2484 |
| 1951 | FestiveWallpaper | 节日壁纸 / Festive Wallpaper | `Main.xMas` | 2484 |
| 1952 | StarsWallpaper | 星星壁纸 / Stars Wallpaper | `Main.xMas` | 2484 |
| 1953 | SquigglesWallpaper | 波浪条纹壁纸 / Squiggles Wallpaper | `Main.xMas` | 2484 |
| 1954 | SnowflakeWallpaper | 雪花壁纸 / Snowflake Wallpaper | `Main.xMas` | 2484 |
| 1955 | KrampusHornWallpaper | 坎卜斯犄角壁纸 / Krampus Horn Wallpaper | `Main.xMas` | 2484 |
| 1956 | BluegreenWallpaper | 蓝绿壁纸 / Bluegreen Wallpaper | `Main.xMas` | 2484 |
| 1957 | GrinchFingerWallpaper | 格林奇手指壁纸 / Grinch Finger Wallpaper | `Main.xMas` | 2484 |
| 2158 | BubbleWallpaper | 泡泡壁纸 / Bubble Wallpaper |  | 2492 |
| 2159 | CopperPipeWallpaper | 铜管壁纸 / Copper Pipe Wallpaper |  | 2492 |
| 2160 | DuckyWallpaper | 黄鸭壁纸 / Ducky Wallpaper |  | 2492 |
| 2008 | FancyGreyWallpaper | 别致灰壁纸 / Fancy Gray Wallpaper |  | 2499 |
| 2009 | IceFloeWallpaper | 浮冰壁纸 / Ice Floe Wallpaper |  | 2499 |
| 2010 | MusicWallpaper | 音符壁纸 / Music Wallpaper |  | 2499 |
| 2011 | PurpleRainWallpaper | 紫雨壁纸 / Purple Rain Wallpaper |  | 2499 |
| 2012 | RainbowWallpaper | 彩虹壁纸 / Rainbow Wallpaper |  | 2499 |
| 2013 | SparkleStoneWallpaper | 烁石壁纸 / Sparkle Stone Wallpaper |  | 2499 |
| 2014 | StarlitHeavenWallpaper | 星光天堂壁纸 / Starlit Heaven Wallpaper |  | 2499 |
| 1490 | Daylight | 天光 / Daylight | `!Main.player[Main.myPlayer].ZoneGraveyard` | 2505 |
| 1481 | FirstEncounter | 初次邂逅 / First Encounter | `!Main.player[Main.myPlayer].ZoneGraveyard` ∧ `Main.moonPhase <= 1` | 2509 |
| 1482 | GoodMorning | 早安 / Good Morning | `!Main.player[Main.myPlayer].ZoneGraveyard` ∧ `!(Main.moonPhase <= 1) && (Main.moonPhase <= 3)` | 2514 |
| 1483 | UndergroundReward | 地下馈赠 / Underground Reward | `!Main.player[Main.myPlayer].ZoneGraveyard` ∧ `!((Main.moonPhase <= 1) \|\| (Main.moonPhase <= 3)) && (Main.moonPhase <= 5)` | 2519 |
| 1484 | ThroughtheWindow | 窗外 / Through the Window | `!Main.player[Main.myPlayer].ZoneGraveyard` ∧ `!(((Main.moonPhase <= 1) \|\| (Main.moonPhase <= 3)) \|\| (Main.moonPhase <= 5))` | 2524 |
| 5245 | Purity | 纯净 / Purity | `Main.player[Main.myPlayer].ShoppingZone_Forest` | 2530 |
| 1492 | DeadlandComesAlive | 死地复生 / Deadland Comes Alive | `Main.player[Main.myPlayer].ZoneCrimson` | 2535 |
| 1488 | LightlessChasms | 阴暗幽谷 / Lightless Chasms | `Main.player[Main.myPlayer].ZoneCorrupt` | 2540 |
| 1489 | TheLandofDeceivingLooks | 浮华假象的大地 / The Land of Deceiving Looks | `Main.player[Main.myPlayer].ZoneHallow` | 2545 |
| 1486 | DoNotStepontheGrass | 请勿践踏草坪 / Do Not Step on the Grass | `Main.player[Main.myPlayer].ZoneJungle` | 2550 |
| 5491 | WinterAtVaringskollen | 瓦林斯科伦之冬 / Winter At Varingskollen | `Main.player[Main.myPlayer].ZoneSnow` | 2555 |
| 1487 | ColdWatersintheWhiteLand | 冰雪寒溪 / Cold Waters in the White Land | `Main.player[Main.myPlayer].ZoneSnow` | 2557 |
| 1491 | SecretoftheSands | 沙的秘密 / Secret of the Sands | `Main.player[Main.myPlayer].ZoneDesert` | 2562 |
| 1493 | EvilPresence | 恶灵现世 / Evil Presence | `Main.bloodMoon` | 2567 |
| 1485 | PlaceAbovetheClouds | 云之上 / Place Above the Clouds | `!Main.player[Main.myPlayer].ZoneGraveyard` ∧ `Main.player[Main.myPlayer].ZoneSkyHeight` | 2574 |
| 1494 | SkyGuardian | 天空守卫 / Sky Guardian | `!Main.player[Main.myPlayer].ZoneGraveyard` ∧ `Main.player[Main.myPlayer].ZoneSkyHeight && Main.hardMode` | 2579 |
| 5251 | Thunderbolt | 雷电 / Thunderbolt | `Main.IsItStorming` | 2585 |
| 4723 | Nevermore | 永不复焉 / Nevermore | `Main.player[Main.myPlayer].ZoneGraveyard` | 2590 |
| 4724 | Reborn | 重生 / Reborn | `Main.player[Main.myPlayer].ZoneGraveyard` | 2592 |
| 4725 | Graveyard | 墓地 / Graveyard | `Main.player[Main.myPlayer].ZoneGraveyard` | 2594 |
| 4726 | GhostManifestation | 鬼魂显现 / Ghost Manifestation | `Main.player[Main.myPlayer].ZoneGraveyard` | 2596 |
| 4727 | WickedUndead | 邪恶亡灵 / Wicked Undead | `Main.player[Main.myPlayer].ZoneGraveyard` | 2598 |
| 5257 | HailtotheKing | 国王万岁 / Hail to the King | `Main.player[Main.myPlayer].ZoneGraveyard` | 2600 |
| 4728 | BloodyGoblet | 血腥酒杯 / Bloody Goblet | `Main.player[Main.myPlayer].ZoneGraveyard` | 2602 |
| 4729 | StillLife | 静物 / Still Life | `Main.player[Main.myPlayer].ZoneGraveyard` | 2604 |

### 海盗 Pirate（NPC 229，商店 #17，Terraria/Chest.cs:2666-2691 (SetupShop case 17)）

| ItemID | 内部名 | 中文 / 英文 | 条件 | 价格 | 行 |
|---|---|---|---|---|---|
| 928 | Cannon | 大炮 / Cannon |  |  | 2667 |
| 929 | Cannonball | 炮弹 / Cannonball |  |  | 2669 |
| 876 | PirateHat | 海盗帽 / Pirate Hat |  |  | 2671 |
| 877 | PirateShirt | 海盗衣 / Pirate Shirt |  |  | 2673 |
| 878 | PiratePants | 海盗裤 / Pirate Pants |  |  | 2675 |
| 2434 | Sail | 船帆 / Sail |  |  | 2677 |
| 5926 | JellyfishBlock | 水母物块 / Jellyfish Block | `Main.player[Main.myPlayer].ZoneGraveyard` |  | 2680 |
| 1180 | ParrotCracker | 鹦鹉饼干 / Parrot Cracker | `Main.player[Main.myPlayer].ZoneBeach` |  | 2684 |
| 1337 | BunnyCannon | 兔兔炮 / Bunny Cannon | `Main.hardMode && NPC.downedMechBossAny && NPC.AnyNPCs(208)` |  | 2688 |

### 发型师 Stylist（NPC 353，商店 #18，Terraria/Chest.cs:2692-2784 (SetupShop case 18)）

| ItemID | 内部名 | 中文 / 英文 | 条件 | 价格 | 行 |
|---|---|---|---|---|---|
| 1990 | HairDyeRemover | 染发剂清除剂 / Hair Dye Remover |  |  | 2694 |
| 1979 | DepthHairDye | 深度染发剂 / Depth Hair Dye |  |  | 2696 |
| 1977 | LifeHairDye | 生命染发剂 / Life Hair Dye | `Main.player[Main.myPlayer].statLifeMax >= 400` |  | 2700 |
| 1978 | ManaHairDye | 魔力染发剂 / Mana Hair Dye | `Main.player[Main.myPlayer].statManaMax >= 200` |  | 2705 |
| 1980 | MoneyHairDye | 钱币染发剂 / Money Hair Dye | `num2 >= 1000000` 条件 num2>=1000000：背包里的钱合计 ≥1 铂金币 |  | 2739 |
| 1981 | TimeHairDye | 时间染发剂 / Time Hair Dye | `(Main.moonPhase % 2 == 0 && Main.dayTime) \|\| (Main.moonPhase % 2 == 1 && !Main.dayTime)` |  | 2743 |
| 1982 | TeamHairDye | 团队染发剂 / Team Hair Dye | `Main.player[Main.myPlayer].team != 0 && Main.netMode == 1` |  | 2748 |
| 1983 | BiomeHairDye | 生物群系染发剂 / Biome Hair Dye | `Main.hardMode` |  | 2753 |
| 1984 | PartyHairDye | 派对染发剂 / Party Hair Dye | `NPC.AnyNPCs(208)` |  | 2758 |
| 1985 | RainbowHairDye | 彩虹染发剂 / Rainbow Hair Dye | `Main.hardMode && NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3` |  | 2763 |
| 1986 | SpeedHairDye | 速度染发剂 / Speed Hair Dye | `Main.hardMode && NPC.downedMechBossAny` |  | 2768 |
| 2863 | MartianHairDye | 火星染发剂 / Martian Hair Dye | `Main.hardMode && NPC.downedMartians` |  | 2773 |
| 3259 | TwilightHairDye | 暮光染发剂 / Twilight Hair Dye | `Main.hardMode && NPC.downedMartians` |  | 2775 |
| 5104 | WilsonBeardShort | 绅士胡子 / Gentleman's Beard |  |  | 2778 |
| 5577 | PrettyMirror | 漂亮镜子 / Pretty Mirror | `Main.player[Main.myPlayer].ZoneGraveyard` |  | 2781 |

### 旅商 Traveling Merchant（NPC 368，商店 #19，Terraria/Chest.cs:2785-2804 (SetupShop case 19)）

Chest.SetupTravelShop (Chest.cs:1269-1352)：商品数 4-6 + 运气加成（1/4,1/8,1/16,1/32 各 +1、专家 1/2 +1、使用过小贩背包 +1、十周年 +1/+2）；困难模式先强制 1 个 rarity≥2 的；最后加 1 幅画。每件用 SetupTravelShop_GetItem(rarity={100,200,300,400,500,600}) 按"从低到高覆盖"的方式 roll（Chest.cs:978-1268），SetupTravelShop_GetPainting 选画（815-949）；成套物品由 SetupTravelShop_AddToShop 自动附带（702-760）。另外 shop 19 开头：持有任意帕鲁物品时追加 5735/5736（Chest.cs:2785-2796）。

商品池（`it = N` 的覆盖顺序即原版 roll 顺序，越靠后越常见；条件里 rarity[k] 越大越稀有）：

| ItemID | 内部名 | 中文 | 条件 | 行 |
|---|---|---|---|---|
| 3309 | BlackCounterweight | 黑平衡锤 | `minimumRarity <= 4 && playerWithHighestLuck.RollLuck(rarity[4]) == 0` | 982 |
| 3314 | YellowCounterweight | 黄平衡锤 | `minimumRarity <= 3 && playerWithHighestLuck.RollLuck(rarity[3]) == 0` | 986 |
| 1987 | AngelHalo | 天使光环 | `playerWithHighestLuck.RollLuck(rarity[5]) == 0` | 990 |
| 2270 | Gatligator | 鳄鱼机关枪 | `playerWithHighestLuck.RollLuck(rarity[4]) == 0 && Main.hardMode` | 998 |
| 4760 | BouncingShield | 中士联盾 | `playerWithHighestLuck.RollLuck(rarity[4]) == 0 && Main.hardMode` | 1002 |
| 2278 | Kimono | 和服 | `playerWithHighestLuck.RollLuck(rarity[4]) == 0` | 1006 |
| 2271 | ArcaneRuneWall | 奥术神符墙 | `playerWithHighestLuck.RollLuck(rarity[4]) == 0` | 1010 |
| 2223 | PulseBow | 脉冲弓 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0 && Main.hardMode && NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3` | 1018 |
| 2272 | WaterGun | 水枪 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0` | 1022 |
| 2276 | DiamondRing | 钻石戒指 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0` | 1026 |
| 2284 | CrimsonCloak | 猩红斗篷 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0` | 1030 |
| 2285 | MysteriousCape | 神秘披风 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0` | 1034 |
| 2286 | RedCape | 红披风 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0` | 1038 |
| 2287 | WinterCape | 冬季披风 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0` | 1042 |
| 4744 | HunterCloak | 猎人斗篷 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0` | 1046 |
| 2296 | SittingDucksFishingRod | 冤大头钓竿 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0 && NPC.downedBoss3` | 1050 |
| 3628 | CompanionCube | 同伴方块 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0` | 1054 |
| 4091 | AntiPortalBlock | 反传送门块 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0 && Main.hardMode` | 1058 |
| 4603 | BirdieRattle | 小鸟拨浪鼓 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0` | 1062 |
| 4604 | ExoticEasternChewToy | 奇异咀嚼玩具 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0` | 1066 |
| 5297 | BlueEgg | 蓝鸡蛋 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0` | 1070 |
| 4605 | BedazzledNectar | 眩晕花蜜 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0` | 1074 |
| 4550 | BambooLeaf | 竹叶 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0` | 1078 |
| 5680 | LilacDuskHead | 丁香薄暮发夹 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1086 |
| 2268 | Pho | 越南河粉 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1090 |
| 2269 | Revolver | 左轮手枪 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0 && WorldGen.shadowOrbSmashed` | 1094 |
| 1988 | Fez | 菲斯帽 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1098 |
| 2275 | MagicHat | 魔法帽 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1102 |
| 2279 | GypsyRobe | 神秘长袍 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1106 |
| 2277 | Gi | 稽古衣 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1110 |
| 4555 | ChefHat | 厨师帽 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1114 |
| 4321 | GameMasterShirt | 大师级玩家夹克 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1118 |
| 4323 | StarPrincessCrown | 星星公主王冠 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1122 |
| 5390 | LincolnsHood | Raynebro的兜帽 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1126 |
| 4549 | DemonHorns | 恶魔角 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1130 |
| 4561 | DevilHorns | 魔鬼角 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1134 |
| 4774 | PandaEars | 熊猫耳朵 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1138 |
| 5136 | VulkelfEar | 瓦尔克精灵耳朵 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1142 |
| 5305 | GoblorcEar | 哥布魔耳朵 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1146 |
| 4562 | Fedora | 软呢帽 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1150 |
| 4558 | StarHairpin | 星星发夹 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1154 |
| 4559 | HeartHairpin | 心形发夹 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1158 |
| 4563 | UnicornHornHat | 假独角兽角 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1162 |
| 4666 | PrettyPinkRibbon | 漂亮粉丝带 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 1166 |
| 4347 | ZapinatorGray | 灰冲击枪 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0 && (NPC.downedDeerclops \|\| NPC.downedSlimeKing \|\| NPC.downedBoss1 \|\| NPC.downedBoss2 \|\| NPC.downedBoss3 \|\| NPC.downedQueenBee \|\| Main.hardMode)` | 1170 |
| 4348 | ZapinatorOrange | 橙冲击枪 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0 && (NPC.downedDeerclops \|\| NPC.downedSlimeKing \|\| NPC.downedBoss1 \|\| NPC.downedBoss2 \|\| NPC.downedBoss3 \|\| NPC.downedQueenBee \|\| Main.hardMode)` ∧ `Main.hardMode` | 1173 |
| 3262 | Code1 | 代码1球 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0 && NPC.downedBoss1` | 1178 |
| 3284 | Code2 | 代码2球 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0 && NPC.downedMechBossAny` | 1182 |
| 5600 | RollerSkatesBlueMountItem | 蓝色溜冰鞋 | `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 1190 |
| 2267 | PadThai | 泰式炒面 | `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 1194 |
| 2214 | BrickLayer | 砌砖刀 | `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 1198 |
| 2215 | ExtendoGrip | 加长握爪 | `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 1202 |
| 2216 | PaintSprayer | 喷漆器 | `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 1206 |
| 2217 | PortableCementMixer | 便携式水泥搅拌机 | `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 1210 |
| 3624 | ActuationAccessory | 自动安放器 | `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 1214 |
| 2273 | Katana | 武士刀 | `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 1218 |
| 2274 | UltrabrightTorch | 超亮火把 | `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 1222 |
| 2266 | Sake | 清酒 | `minimumRarity <= 0` ∧ `playerWithHighestLuck.RollLuck(rarity[0]) == 0` | 1228 |
| 2258 | SteampunkCup | 圣餐杯 | `minimumRarity <= 0` ∧ `playerWithHighestLuck.RollLuck(rarity[0]) == 0` | 1236 |
| 2242 | FancyDishes | 精致餐具 | `minimumRarity <= 0` ∧ `playerWithHighestLuck.RollLuck(rarity[0]) == 0` | 1240 |
| 2260 | DynastyWood | 王朝木 | `minimumRarity <= 0` ∧ `playerWithHighestLuck.RollLuck(rarity[0]) == 0` | 1244 |
| 3637 | TeamBlockWhite | 白团队块 | `minimumRarity <= 0` ∧ `playerWithHighestLuck.RollLuck(rarity[0]) == 0` | 1248 |
| 4420 | LawnFlamingo | 草坪火烈鸟 | `minimumRarity <= 0` ∧ `playerWithHighestLuck.RollLuck(rarity[0]) == 0` | 1252 |
| 3119 | DPSMeter | 每秒伤害计数器 | `minimumRarity <= 0` ∧ `playerWithHighestLuck.RollLuck(rarity[0]) == 0` | 1256 |
| 3118 | LifeformAnalyzer | 生命体分析机 | `minimumRarity <= 0` ∧ `playerWithHighestLuck.RollLuck(rarity[0]) == 0` | 1260 |
| 3099 | Stopwatch | 秒表 | `minimumRarity <= 0` ∧ `playerWithHighestLuck.RollLuck(rarity[0]) == 0` | 1264 |
| 5121 | PaintingWilson | 绅士科学家 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0 && !Main.dontStarveWorld` | 819 |
| 5122 | PaintingWillow | 纵火者 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0 && !Main.dontStarveWorld` | 823 |
| 5124 | PaintingWolfgang | 大力士 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0 && !Main.dontStarveWorld` | 827 |
| 5123 | PaintingWendy | 丧亲者 | `playerWithHighestLuck.RollLuck(rarity[3]) == 0 && !Main.dontStarveWorld` | 831 |
| 3596 | MoonLordPainting | 不是小孩，也不是乌贼 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0 && Main.hardMode && NPC.downedMoonlord` | 839 |
| 2865 | PaintingCastleMarsberg | 火星贝格城堡 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0 && Main.hardMode && NPC.downedMartians` | 843 |
| 2866 | PaintingMartiaLisa | 火娜丽莎 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0 && Main.hardMode && NPC.downedMartians` | 847 |
| 2867 | PaintingTheTruthIsUpThere | 真理就在火星 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0 && Main.hardMode && NPC.downedMartians` | 851 |
| 3055 | PaintingAcorns | 橡实 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0 && NPC.downedFrost` | 855 |
| 3056 | PaintingColdSnap | 寒流 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0 && NPC.downedFrost` | 859 |
| 3057 | PaintingCursedSaint | 被诅咒的圣诞骷髅王 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0 && NPC.downedFrost` | 863 |
| 3058 | PaintingSnowfellas | 吉祥三雪宝 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0 && NPC.downedFrost` | 867 |
| 3059 | PaintingTheSeason | 圣诞雪季 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0 && NPC.downedFrost` | 871 |
| 5243 | MoonmanandCompany | 月亮人公司 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0 && Main.hardMode && NPC.downedMoonlord` | 875 |
| 5530 | StickmanVsTerrTerr | 火柴人vs泰泰 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 879 |
| 5633 | PaintingGermanZenith | 奥克托伯 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 883 |
| 5636 | PaintingGermanBeer | 恭祝 | `playerWithHighestLuck.RollLuck(rarity[2]) == 0` | 887 |
| 5121 | PaintingWilson | 绅士科学家 | `minimumRarity <= 1` ∧ `playerWithHighestLuck.RollLuck(rarity[1]) == 0 && Main.dontStarveWorld` | 893 |
| 5122 | PaintingWillow | 纵火者 | `minimumRarity <= 1` ∧ `playerWithHighestLuck.RollLuck(rarity[1]) == 0 && Main.dontStarveWorld` | 897 |
| 5124 | PaintingWolfgang | 大力士 | `minimumRarity <= 1` ∧ `playerWithHighestLuck.RollLuck(rarity[1]) == 0 && Main.dontStarveWorld` | 901 |
| 5123 | PaintingWendy | 丧亲者 | `minimumRarity <= 1` ∧ `playerWithHighestLuck.RollLuck(rarity[1]) == 0 && Main.dontStarveWorld` | 905 |
| 5225 | KargohsSummon | Kargoh的召唤 | `minimumRarity <= 1` ∧ `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 909 |
| 5229 | BennyWarhol | 本尼·沃霍尔 | `minimumRarity <= 1` ∧ `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 913 |
| 5232 | Duality | 二元性 | `minimumRarity <= 1` ∧ `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 917 |
| 5389 | HoplitePizza | 奶酪比萨海报 | `minimumRarity <= 1` ∧ `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 921 |
| 5233 | ParsecPals | 秒差距伙伴 | `minimumRarity <= 1` ∧ `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 925 |
| 5241 | DoNotEattheVileMushroom | 不要吃魔菇！ | `minimumRarity <= 1` ∧ `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 929 |
| 5244 | SunshineofIsrapony | Israpony的阳光 | `minimumRarity <= 1` ∧ `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 933 |
| 5487 | HeroesFromAnotherWorld | 异域英雄 | `minimumRarity <= 1` ∧ `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 937 |
| 5242 | YuumaTheBlueTiger | 蓝虎Yuuma | `minimumRarity <= 1` ∧ `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 941 |
| 5531 | CozyWindow | 舒适窗户 | `minimumRarity <= 1` ∧ `playerWithHighestLuck.RollLuck(rarity[1]) == 0` | 945 |

成套附带：`itemID == 2260`→2261 红王朝瓦；`itemID == 2260`→2262 蓝王朝瓦；`itemID == 5680`→5681 丁香薄暮连衣裙；`itemID == 5680`→5682 丁香薄暮短裙；`itemID == 4555`→4556 厨师服；`itemID == 4555`→4557 厨师裤；`itemID == 4321`→4322 大师级玩家裤；`itemID == 4323`→4324 星星公主裙；`itemID == 4323`→4365 天界魔棒；`itemID == 5390`→5386 Raynebro的连帽衫；`itemID == 5390`→5387 Raynebro的裤子；`itemID == 4666`→4664 漂亮粉连衣裙；`itemID == 4666`→4665 漂亮粉长袜

### 酒馆老板 Tavernkeep（NPC 550，商店 #21，Terraria/Chest.cs:2957-3149 (SetupShop case 21)）

局部变量：`flag` = `Main.hardMode && NPC.downedMechBossAny`（行 2959）；`num7` = `Main.hardMode && NPC.downedGolemBoss`（行 2960）

| ItemID | 内部名 | 中文 / 英文 | 条件 | 价格 | 行 |
|---|---|---|---|---|---|
| 353 | Ale | 麦芽酒 / Ale |  |  | 2961 |
| 3828 | DD2ElderCrystal | 永恒水晶 / Eternia Crystal |  | Item.buyPrice(0, 4) [num7] / Item.buyPrice(0, 1) [!(num7) && (flag)] / Item.buyPrice(0, 0, 25) [!((num7) \|\| (flag))] | 2963 |
| 3816 | DD2ElderCrystalStand | 永恒水晶座 / Eternia Crystal Stand |  |  | 2977 |
| 3813 | DefendersForge | 护卫熔炉 / Defender's Forge |  | 50 (CustomCurrencyID.DefenderMedals) | 2979 |
| 3818 | DD2FlameburstTowerT1Popper | 爆炸烈焰魔杖 / Flameburst Rod |  | 5 (CustomCurrencyID.DefenderMedals) | 2984 |
| 3824 | DD2BallistraTowerT1Popper | 弩车魔杖 / Ballista Rod |  | 5 (CustomCurrencyID.DefenderMedals) | 2988 |
| 3832 | DD2ExplosiveTrapT1Popper | 爆炸机关魔杖 / Explosive Trap Rod |  | 5 (CustomCurrencyID.DefenderMedals) | 2992 |
| 3829 | DD2LightningAuraT1Popper | 闪电光环魔杖 / Lightning Aura Rod |  | 5 (CustomCurrencyID.DefenderMedals) | 2996 |
| 3819 | DD2FlameburstTowerT2Popper | 爆炸烈焰手杖 / Flameburst Cane | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3002 |
| 3825 | DD2BallistraTowerT2Popper | 弩车手杖 / Ballista Cane | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3006 |
| 3833 | DD2ExplosiveTrapT2Popper | 爆炸机关手杖 / Explosive Trap Cane | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3010 |
| 3830 | DD2LightningAuraT2Popper | 闪电光环手杖 / Lightning Aura Cane | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3014 |
| 3820 | DD2FlameburstTowerT3Popper | 爆炸烈焰法杖 / Flameburst Staff | `num7` | 60 (CustomCurrencyID.DefenderMedals) | 3021 |
| 3826 | DD2BallistraTowerT3Popper | 弩车法杖 / Ballista Staff | `num7` | 60 (CustomCurrencyID.DefenderMedals) | 3025 |
| 3834 | DD2ExplosiveTrapT3Popper | 爆炸机关法杖 / Explosive Trap Staff | `num7` | 60 (CustomCurrencyID.DefenderMedals) | 3029 |
| 3831 | DD2LightningAuraT3Popper | 闪电光环法杖 / Lightning Aura Staff | `num7` | 60 (CustomCurrencyID.DefenderMedals) | 3033 |
| 3800 | SquireGreatHelm | 侍卫大头盔 / Squire's Great Helm | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3040 |
| 3801 | SquirePlating | 侍卫板甲 / Squire's Plating | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3044 |
| 3802 | SquireGreaves | 侍卫护胫 / Squire's Greaves | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3048 |
| 3797 | ApprenticeHat | 学徒帽 / Apprentice's Hat | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3053 |
| 3798 | ApprenticeRobe | 学徒长袍 / Apprentice's Robe | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3057 |
| 3799 | ApprenticeTrousers | 学徒裤 / Apprentice's Trousers | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3061 |
| 3803 | HuntressWig | 女猎人假发 / Huntress's Wig | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3066 |
| 3804 | HuntressJerkin | 女猎人上衣 / Huntress's Jerkin | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3070 |
| 3805 | HuntressPants | 女猎人裤 / Huntress's Pants | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3074 |
| 3806 | MonkBrows | 武僧浓眉秃头帽 / Monk's Bushy Brow Bald Cap | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3079 |
| 3807 | MonkShirt | 武僧衣 / Monk's Shirt | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3083 |
| 3808 | MonkPants | 武僧裤 / Monk's Pants | `flag` | 15 (CustomCurrencyID.DefenderMedals) | 3087 |
| 3871 | SquireAltHead | 英灵殿骑士头盔 / Valhalla Knight's Helm | `num7` | 50 (CustomCurrencyID.DefenderMedals) | 3095 |
| 3872 | SquireAltShirt | 英灵殿骑士胸甲 / Valhalla Knight's Breastplate | `num7` | 50 (CustomCurrencyID.DefenderMedals) | 3099 |
| 3873 | SquireAltPants | 英灵殿骑士护胫 / Valhalla Knight's Greaves | `num7` | 50 (CustomCurrencyID.DefenderMedals) | 3103 |
| 3874 | ApprenticeAltHead | 暗黑艺术家帽子 / Dark Artist's Hat | `num7` | 50 (CustomCurrencyID.DefenderMedals) | 3108 |
| 3875 | ApprenticeAltShirt | 暗黑艺术家长袍 / Dark Artist's Robes | `num7` | 50 (CustomCurrencyID.DefenderMedals) | 3112 |
| 3876 | ApprenticeAltPants | 暗黑艺术家护腿 / Dark Artist's Leggings | `num7` | 50 (CustomCurrencyID.DefenderMedals) | 3116 |
| 3877 | HuntressAltHead | 红色骑术兜帽 / Red Riding Hood | `num7` | 50 (CustomCurrencyID.DefenderMedals) | 3121 |
| 3878 | HuntressAltShirt | 红色骑术服 / Red Riding Dress | `num7` | 50 (CustomCurrencyID.DefenderMedals) | 3125 |
| 3879 | HuntressAltPants | 红色骑术护腿 / Red Riding Leggings | `num7` | 50 (CustomCurrencyID.DefenderMedals) | 3129 |
| 3880 | MonkAltHead | 渗透忍者头盔 / Shinobi Infiltrator's Helmet | `num7` | 50 (CustomCurrencyID.DefenderMedals) | 3134 |
| 3881 | MonkAltShirt | 渗透忍者上衣 / Shinobi Infiltrator's Torso | `num7` | 50 (CustomCurrencyID.DefenderMedals) | 3138 |
| 3882 | MonkAltPants | 渗透忍者裤装 / Shinobi Infiltrator's Pants | `num7` | 50 (CustomCurrencyID.DefenderMedals) | 3142 |

### 高尔夫球手 Golfer（NPC 588，商店 #22，Terraria/Chest.cs:3150-3256 (SetupShop case 22)）

| ItemID | 内部名 | 中文 / 英文 | 条件 | 价格 | 行 |
|---|---|---|---|---|---|
| 4587 | GolfClubStoneIron | 破旧高尔夫球杆（铁杆） / Worn Golf Club (Iron) |  |  | 3153 |
| 4590 | GolfClubWoodDriver | 破旧高尔夫球杆（木杆） / Worn Golf Club (Driver) |  |  | 3154 |
| 4589 | GolfClubBronzeWedge | 破旧高尔夫球杆（挖起杆） / Worn Golf Club (Wedge) |  |  | 3155 |
| 4588 | GolfClubRustyPutter | 破旧高尔夫球杆（推杆） / Worn Golf Club (Putter) |  |  | 3156 |
| 4083 | GolfCupFlagWhite | 白三角旗 / White Pin Flag |  |  | 3157 |
| 4084 | GolfCupFlagRed | 红三角旗 / Red Pin Flag |  |  | 3158 |
| 4085 | GolfCupFlagGreen | 绿三角旗 / Green Pin Flag |  |  | 3159 |
| 4086 | GolfCupFlagBlue | 蓝三角旗 / Blue Pin Flag |  |  | 3160 |
| 4087 | GolfCupFlagYellow | 黄三角旗 / Yellow Pin Flag |  |  | 3161 |
| 4088 | GolfCupFlagPurple | 紫三角旗 / Purple Pin Flag |  |  | 3162 |
| 4039 | GolfClubIron | 高尔夫球杆（铁杆） / Golf Club (Iron) | `golferScoreAccumulated >= 500` |  | 3165 |
| 4094 | GolfClubDriver | 高尔夫球杆（木杆） / Golf Club (Driver) | `golferScoreAccumulated >= 500` |  | 3167 |
| 4093 | GolfClubWedge | 高尔夫球杆（挖起杆） / Golf Club (Wedge) | `golferScoreAccumulated >= 500` |  | 3169 |
| 4092 | GolfClubPutter | 高尔夫球杆（推杆） / Golf Club (Putter) | `golferScoreAccumulated >= 500` |  | 3171 |
| 4089 | GolfTee | 高尔夫球座 / Golf Tee |  |  | 3174 |
| 3989 | GolfBall | 高尔夫球 / Golf Ball |  |  | 3175 |
| 4095 | GolfWhistle | 高尔夫口哨 / Golf Whistle |  |  | 3176 |
| 4040 | GolfCup | 高尔夫球洞 / Golf Cup |  |  | 3177 |
| 4319 | ArrowSign | 箭头标牌 / Arrow Sign |  |  | 3178 |
| 4320 | PaintedArrowSign | 彩色箭头标牌 / Painted Arrow Sign |  |  | 3179 |
| 4591 | GolfClubMythrilIron | 精致高尔夫球杆（铁杆） / Fancy Golf Club (Iron) | `golferScoreAccumulated > 1000` |  | 3182 |
| 4594 | GolfClubPearlwoodDriver | 精致高尔夫球杆（木杆） / Fancy Golf Club (Driver) | `golferScoreAccumulated > 1000` |  | 3184 |
| 4593 | GolfClubGoldWedge | 精致高尔夫球杆（挖起杆） / Fancy Golf Club (Wedge) | `golferScoreAccumulated > 1000` |  | 3186 |
| 4592 | GolfClubLeadPutter | 精致高尔夫球杆（推杆） / Fancy Golf Club (Putter) | `golferScoreAccumulated > 1000` |  | 3188 |
| 4135 | GolfHat | 乡村俱乐部帽 / Country Club Cap |  |  | 3191 |
| 4138 | GolfVisor | 乡村俱乐部帽舌 / Country Club Visor |  |  | 3192 |
| 4136 | GolfShirt | 乡村俱乐部背心 / Country Club Vest |  |  | 3193 |
| 4137 | GolfPants | 乡村俱乐部长裤 / Country Club Trousers |  |  | 3194 |
| 4049 | LawnMower | 割草机 / Lawn Mower |  |  | 3195 |
| 4265 | GolfChest | 高尔夫箱 / Golf Chest | `golferScoreAccumulated > 500` |  | 3198 |
| 4595 | GolfClubTitaniumIron | 高级高尔夫球杆（铁杆） / Premium Golf Club (Iron) | `golferScoreAccumulated > 2000` |  | 3203 |
| 4598 | GolfClubChlorophyteDriver | 高级高尔夫球杆（木杆） / Premium Golf Club (Driver) | `golferScoreAccumulated > 2000` |  | 3205 |
| 4597 | GolfClubDiamondWedge | 高级高尔夫球杆（挖起杆） / Premium Golf Club (Wedge) | `golferScoreAccumulated > 2000` |  | 3207 |
| 4596 | GolfClubShroomitePutter | 高级高尔夫球杆（推杆） / Premium Golf Club (Putter) | `golferScoreAccumulated > 2000` |  | 3209 |
| 4264 | GolfCart | 高尔夫球车钥匙 / Golf Cart Keys | `golferScoreAccumulated > 2000` ∧ `NPC.downedBoss3` |  | 3213 |
| 4599 | GolfTrophyBronze | 高尔夫铜奖杯 / Bronze Golf Trophy | `golferScoreAccumulated > 500` |  | 3219 |
| 4600 | GolfTrophySilver | 高尔夫银奖杯 / Silver Golf Trophy | `golferScoreAccumulated >= 1000` |  | 3224 |
| 4601 | GolfTrophyGold | 高尔夫金奖杯 / Gold Golf Trophy | `golferScoreAccumulated >= 2000` |  | 3229 |
| 4658 | GolfPainting1 | 起伏的果岭 / The Rolling Greens | `golferScoreAccumulated >= 2000` ∧ `Main.moonPhase == 0 \|\| Main.moonPhase == 1` |  | 3236 |
| 4659 | GolfPainting2 | 静物球习作 / Study of a Ball at Rest | `golferScoreAccumulated >= 2000` ∧ `!(Main.moonPhase == 0 \|\| Main.moonPhase == 1) && (Main.moonPhase == 2 \|\| Main.moonPhase == 3)` |  | 3241 |
| 4660 | GolfPainting3 | 球来了！ / Fore! | `golferScoreAccumulated >= 2000` ∧ `!((Main.moonPhase == 0 \|\| Main.moonPhase == 1) \|\| (Main.moonPhase == 2 \|\| Main.moonPhase == 3)) && (Main.moonPhase == 4 \|\| Main.moonPhase == 5)` |  | 3246 |
| 4661 | GolfPainting4 | 映像的二重性 / The Duplicity of Reflections | `golferScoreAccumulated >= 2000` ∧ `!(((Main.moonPhase == 0 \|\| Main.moonPhase == 1) \|\| (Main.moonPhase == 2 \|\| Main.moonPhase == 3)) \|\| (Main.moonPhase == 4 \|\| Main.moonPhase == 5)) && (Main.moonPhase == 6 \|\| Main.moonPhase == 7)` |  | 3251 |

### 公主 Princess（NPC 663，商店 #24，Terraria/Chest.cs:3403-3465 (SetupShop case 24)）

| ItemID | 内部名 | 中文 / 英文 | 条件 | 价格 | 行 |
|---|---|---|---|---|---|
| 5071 | RoyalTiara | 皇家头冠 / Royal Tiara |  |  | 3404 |
| 5072 | RoyalDressTop | 皇家女衫 / Royal Blouse |  |  | 3405 |
| 5073 | RoyalDressBottom | 皇家礼服裙 / Royal Dress |  |  | 3406 |
| 5076 | RoyalScepter | 皇家权杖 / Royal Scepter |  |  | 3407 |
| 5077 | GlassSlipper | 水晶鞋 / Glass Slipper |  |  | 3408 |
| 5078 | PrinceUniform | 王子制服 / Prince Uniform |  |  | 3409 |
| 5079 | PrincePants | 王子裤 / Prince Pants |  |  | 3410 |
| 5080 | PrinceCape | 王子披风 / Prince Cape |  |  | 3411 |
| 5081 | PottedCrystalPlantFern | 盆栽水晶蕨 / Potted Crystal Fern |  |  | 3412 |
| 5082 | PottedCrystalPlantSpiral | 盆栽水晶螺旋 / Potted Crystal Spiral |  |  | 3413 |
| 5083 | PottedCrystalPlantTeardrop | 盆栽水晶泪滴 / Potted Crystal Teardrop |  |  | 3414 |
| 5084 | PottedCrystalPlantTree | 盆栽水晶树 / Potted Crystal Tree |  |  | 3415 |
| 5085 | Princess64 | 公主64 / Princess 64 |  |  | 3416 |
| 5086 | PaintingOfALass | 少女画像 / Painting of a Lass |  |  | 3417 |
| 5087 | DarkSideHallow | 神圣之地的黑暗面 / Dark Side of the Hallow |  |  | 3418 |
| 5310 | PrincessStyle | 公主风 / Princess Style |  |  | 3419 |
| 5222 | SuspiciouslySparkly | 可疑闪光 / Suspiciously Sparkly |  |  | 3420 |
| 5228 | TerraBladeChronicles | 泰拉刃纪事 / Terra Blade Chronicles |  |  | 3421 |
| 5266 | RoyalRomance | 皇家浪漫 / Royal Romance | `NPC.downedSlimeKing && NPC.downedQueenSlime` |  | 3424 |
| 5044 | MusicBoxCredits | 八音盒（旅程结束） / Music Box (Journey's End) | `Main.hardMode && NPC.downedMoonlord` |  | 3428 |
| 1309 | SlimeStaff | 史莱姆法杖 / Slime Staff | `Main.tenthAnniversaryWorld` |  | 3432 |
| 1859 | HeartLantern | 红心灯笼 / Heart Lantern | `Main.tenthAnniversaryWorld` |  | 3433 |
| 1358 | FlaskofParty | 派对药剂 / Flask of Party | `Main.tenthAnniversaryWorld` |  | 3434 |
| 857 | SandstorminaBottle | 沙暴瓶 / Sandstorm in a Bottle | `Main.tenthAnniversaryWorld` ∧ `Main.player[Main.myPlayer].ZoneDesert` |  | 3437 |
| 4144 | Terragrim | 泰拉魔刃 / Terragrim | `Main.tenthAnniversaryWorld` ∧ `Main.bloodMoon` |  | 3441 |
| 2584 | PirateStaff | 海盗法杖 / Pirate Staff | `Main.tenthAnniversaryWorld` ∧ `Main.hardMode && NPC.downedPirates` ∧ `Main.moonPhase == 0 \|\| Main.moonPhase == 1` |  | 3447 |
| 854 | DiscountCard | 优惠卡 / Discount Card | `Main.tenthAnniversaryWorld` ∧ `Main.hardMode && NPC.downedPirates` ∧ `Main.moonPhase == 2 \|\| Main.moonPhase == 3` |  | 3451 |
| 855 | LuckyCoin | 幸运币 / Lucky Coin | `Main.tenthAnniversaryWorld` ∧ `Main.hardMode && NPC.downedPirates` ∧ `Main.moonPhase == 4 \|\| Main.moonPhase == 5` |  | 3455 |
| 905 | CoinGun | 钱币枪 / Coin Gun | `Main.tenthAnniversaryWorld` ∧ `Main.hardMode && NPC.downedPirates` ∧ `Main.moonPhase == 6 \|\| Main.moonPhase == 7` |  | 3459 |
| 5088 | BerniePetItem | 伯尼的钮扣 / Bernie's Button |  |  | 3463 |

## 6. 没有商店的缺失 NPC 的功能（NPCInteractions.cs）

- 护士 18：NurseHeal（NPCInteractions.cs:246）治疗收费。
- 渔夫 369：AnglerQuest（133）每日钓鱼任务。
- 税收官 441：TaxCollectorCollectTaxes（221）领税金；税金按 Player.taxRate 累积（Main.cs:66019-66026）。
- 城镇宠物（猫/狗/兔/史莱姆）：PetAnimal（153）可抚摸。
- 老人 37：OldManCurse（171）夜晚召唤骷髅王。
- 发型师 353 有商店 #18 外加 StylistHairWindow（426）理发；染料商 207 困难模式 DyeTraderRarePlant（444）；酒馆老板 550 TavernkeepAdvice（466）；树妖 20 DryadPurification（105）。
