# 泰拉瑞亚 1.4.5 钓鱼系统完整提取（供 TerraKnight 移植）

来源：`scratchpad/terraria-src`（1.4.5 反编译）+ 同版本 en-US / zh-Hans 本地化。所有行号均为该反编译树内的 `文件:行`。机器可读版本见同目录 `fishing.json`（规则、宝匣、奖励全部结构化，`items` 字典含全部 ID 的中英文名）。`fishing_reference.py` 是读取 `fishing.json` 的参考实现，可算出任意条件下的精确渔获分布，用来对拍移植代码。

约定：`Main.rand.Next(n)` 为 [0,n-1] 均匀整数；`Next(a,b)` 为 [a,b-1]；下文所有堆叠数量已换成闭区间。`FL` = 渔力（fishingLevel）。C# 整数除法向零取整。

**渔获判定入口**：渔获由数据驱动的有序规则表 `GameContentFishDropPopulator`（Terraria.GameContent.FishDropRules/）决定，Main.cs:6752-6754 填充，`Projectile.FishingCheck_RollItemDrop`（Projectile.cs:20960-20978）调用 `Main.FishDropsDB.TryGetItemDropType`。第 3 节的规则表就是生效逻辑。源码里另有一个没有任何调用者的 `FishingCheck_RollItemDropOld`（Projectile.cs:21033-21616），它和规则表有差异（它的地牢分支屏蔽全部群系分支、腐化/猩红/神圣用 else-if 互斥、地牢只有“极稀有 1/7 炼药桌”、地下兜底 #154 写法不同），移植以规则表为准，旧函数只作对照。

## 1. 依赖的游戏系统（需映射到空洞骑士）

| 系统 | 原版定义 | 引用 |
|---|---|---|
| Player.ZoneCorrupt / ZoneCrimson / ZoneHallow / ZoneJungle / ZoneSnow / ZoneDesert / ZoneGlowshroom / ZoneDungeon / ZoneBeach | SceneMetrics tile counts around the PLAYER (Corruption/Crimson 300, Hallow 125, Jungle 140 (not in underworld), Snow 1500, Desert 1500 sand excluding ocean sand, Mushroom 100, Dungeon 250 + dungeon wall behind player and below surface); ZoneBeach = WorldGen.oceanDepths(player tile) | SceneMetrics.cs:24-58, 668-698; Player.cs:16804-16827 |
| heightLevel from bobber tile Y vs Main.worldSurface, Main.rockLayer, Main.maxTilesY-300 |  | Projectile.cs:20807-20854 |
| Ocean fish: bobber X within 380 tiles of the world edge + >1000 liquid tiles + heightLevel<=1 |  | AFishDropRulePopulator.cs:257-264 |
| Liquid type per tile (lava / honey flags; any other liquid incl. shimmer counts as water; a bobber in shimmer also gives the owner buff 353 Projectile.cs:54688-54699), pond flood-fill |  | Projectile.cs:21699-21743 |
| Time of day (Main.dayTime, Main.time), Main.moonPhase, Main.bloodMoon, Main.raining, Main.cloudBGAlpha |  | Player.cs:42530-42574 |
| World progression: Main.hardMode, NPC.downedBoss3 (dungeon), NPC.combatBookWasUsed, NPC.unlockedSlimeRedSpawn, WorldGen.crimson (quest selection) |  | various |
| Player.luck (RecalculateLuck: ladybugs, torch luck, luck potion, kite, Galaxy Pearl, lantern night, garden gnome, coins, broken mirror; clamp -0.7..1) |  | Player.cs:3071-3073, 29367-29393 |
| Angler NPC presence (NPC.AnyNPCs(369)) for quest fish; Main.anglerQuest / anglerQuestFinished / anglerWhoFinishedToday / Player.anglerQuestsFinished |  | Main.cs:1857-1873 |
| Inventory: best pole, first bait (ammo slots first), HasItem(quest fish) |  | Player.cs:42576-42620 |
| Seeds: remixWorld, notTheBeesWorld, Skyblock.lowTiles, !isThereAWorldSurface |  | various |

要点：生物群系标志读的是**玩家**所在位置（SceneMetrics 扫描玩家周围图格），高度层和“海洋”读的是**浮标**图格坐标。移植时建议给每个 HK 场景配置：`zones`（corrupt/crimson/hallow/jungle/snow/desert/glowshroom/dungeon/beach 的布尔集合）、`heightLevel`（0 太空 … 4 地狱）、是否算“海洋”（替代 X<380 且水>1000），以及每个水池的液体类型与图格数。

## 2. 钓鱼流程

### 2.1 抛竿、浮标、咬钩、收杆

- 所有钓竿每次抛出 **1 个浮标**（原版没有多浮标钓竿；`Player.ItemCheck_Shoot` 中无钓竿特例，Player.cs:47461 起；装备钓鱼浮标配饰时只替换弹幕类型 Player.cs:47586-47589）。浮标存在时再次使用钓竿 = 收杆，不会再抛（`ItemCheck_PullFishingBobbers`，Player.cs:52671, 53006-53037）。
- 浮标 AI：Terraria/Projectile.cs:54599-54804 (AI_061_FishingBobber, aiStyle 61)。销毁条件：held item has fishingPole==0, or held item's shoot != this bobber type (custom bobbers 986-993 exempt), player CCed/noItems/pulley/dead (54605-54625); retracting and distance>3000px (54661-54664); retracting and touching the player (54671-54674)。distance to player > 900px -> ai[0]=1 (54682-54685)。漂浮：when wet: velocity.X*=0.9; buoyancy toward water line (AI_061_FishingBobber_GetWaterLine 54806-54837); 'settled' when |velocity.Y|<=0.01 (54737-54740). Shimmer: gives buff 353 to owner (54688-54699). In air: gravity 0.2, max fall 15.9 (54742-54754)。
- **咬钩计时**（Projectile.cs:54767-54785）：仅当浮标在液体中静止、`ai[1]==0`、且在浮标主人客户端上运行。每帧：`FL=player.GetFishingConditions().FinalFishingLevel; if Next(300)<FL: localAI[1]+=Next(1,3); localAI[1]+=FL/30 (int); localAI[1]+=Next(1,3); if Next(60)==0: localAI[1]+=60`。`localAI[1] > 660 -> localAI[1]=0; FishingCheck()`。期望帧数 ≈ `660 / (1.5 + floor(FL/30) + 1.5*min(FL,300)/300 + 1)`（例：FL=30 约 181 帧≈3 秒，FL=100 约 110 帧）。注意这里用的是 `GetFishingConditions().FinalFishingLevel`，不含鱼饵桶/水量/幸运修正。
- **上钩窗口**（Projectile.cs:20876-20880, 20890-20893, 54786-54803）：FishingCheck 判出渔获时 `ai[1] = Next(-240,-90) - fishingLevel (fishingLevel after all FishingCheck modifiers); localAI[1] = itemType (or -npcType); localAI[2] = bait type`；之后每帧 `ai[1] += Next(1,5) (1..4); while velocity.Y==0 the bobber is kicked: velocity.Y=Next(100,500)*0.015, velocity.X=Next(-100,101)*0.015 (visual bob)`；`when ai[1] >= 0: ai[1]=0, localAI[1]=0 (fish escaped, timer restarts)`。窗口长度 ≈ (91..240 + FL) / 2.5 帧。
- **熔岩空竿加速**：if nothing was hooked and the pond is lava: sources = [lava bait] + [Hotline pole] + [accLavaFishing]; 3 sources -> localAI[1]+=480, 2 sources -> +240 (head start on the next 660 timer)（Projectile.cs:20897-20920）。
- **收杆**（Player.cs:53006-53085 (ItemCheck_PullFishingBobbers / PullBobber)）：
  - Using the pole while any own bobber exists does not cast; it sets every bobber ai[0]=1 (retract) and if in water sets velocity.Y=min(velocity.Y,-10) (53021-53029).
  - If ai[1]<0 (bite window open) and localAI[1]!=0 and the bait stack (type localAI[2]) is still in inventory (ammo slots 54-57 first, then 0-49): ConsumeBait then PullBobber (53030-53033).
  - PullBobber: bait 2673 Truffle Worm -> spawn Duke Fishron (370) on player (53041-53052); localAI[1]<0 -> spawn that NPC at the bobber (Dreadnautilus 618 spawns 64px lower; Surly Slime 682 sets NPC.unlockedSlimeRedSpawn) (53053-53075); else Next(7)==0 && !accFishingLine -> line breaks, catch lost (53076-53079); else ai[1]=item type (53080-53083).
  - When the bobber dies (reaches the player) on the owner: if 0<ai[1]<ItemID.Count -> AI_061_FishingBobber_GiveItemToPlayer (Projectile.cs:73234-73242).
- **鱼饵消耗**（Player.cs:53087-53164）：`d = 1 + bait/6 (float, min 1); if accTackleBox d += 1; consume if NextFloat()*d < 1  =>  P = 1/d`。特例：localAI[1]==-1 -> consume; caught item with rare<0 (junk 2337-2339 rare -1, quest fish rare -11) -> never consume; Gold Worm (2895) -> P=1/20; Truffle Worm -> always; Ladybug/Gold Ladybug consumed -> NPC.LadyBugKilled (luck penalty)。
- **特殊堆叠**（Projectile.cs:54853-54906）：炸弹鱼 3196：`min=(FL/20+3)/2, max=(FL/10+6)/2 (+1 each for Next(50)<FL, Next(100)<FL, Next(150)<FL, Next(200)<FL); stack=Next(min,max+1)`；寒霜飞鱼 3197：`min=(FL/4+15)/2, max=(FL/2+40)/2 (+6 each for Next(50)<FL, Next(100)<FL, Next(150)<FL, Next(200)<FL); stack=Next(min,max+1)`。FL here = GetFishingConditions().FinalFishingLevel (no chum/water/luck); integer division。

### 2.2 FishingCheck（Projectile.cs:20704-20925）

1. Reject if the player is wet and the bobber center is above the player's mounted center（Projectile.cs:20720-20723）
2. X,Y = bobber center tile; GetFishingPondState -> inLava, inHoney, waterTilesCount, chumsInWater（Projectile.cs:20724-20730）
3. notTheBeesWorld: 50% clear inHoney（Projectile.cs:20731-20734）
4. waterTilesCount < 75 -> 'Not Enough Water!' and abort（Projectile.cs:20735-20739）
5. conditions = player.GetFishingConditions(); bait 2673 -> Truffle-worm path (ocean X<380 or >maxTilesX-380, water>1000, no Duke Fishron) then abort（Projectile.cs:20740-20753）
6. fishingLevel = FinalFishingLevel; ==0 -> abort（Projectile.cs:20754-20758）
7. CanFishInLava = Sets.CanFishInLava[pole] || Sets.IsLavaBait[bait] || player.accLavaFishing（Projectile.cs:20759）
8. chum: >0 +11, >1 +6, >2 +3（Projectile.cs:20760-20771）
9. waterNeededToFish = int(300*atmo); atmo = clamp((bobber.position.Y/16 - (60 + 10*(maxTilesX/4200)^2)) / (worldSurface/6), 0.25, 1)（Projectile.cs:20773-20785）
10. waterQuality = water/needed; if <1: fishingLevel = int(fishingLevel*waterQuality); shown as '(-x%)'（Projectile.cs:20786-20795）
11. luck<0: with chance -luck: fishingLevel = int(fishingLevel*(0.9 - 0.3*NextFloat())); luck>0: with chance luck: fishingLevel = int(fishingLevel*(1.1 + 0.3*NextFloat()))（Projectile.cs:20796-20806）
12. heightLevel (see height_levels)（Projectile.cs:20807-20854）
13. junk = Next(50)>FL && Next(50)>FL && waterTilesCount < waterNeededToFish（Projectile.cs:20855）
14. RollDropLevels(FL)（Projectile.cs:20856, 21635-21697）
15. ProbeForQuestFish（Projectile.cs:20857, 21618-21633）
16. PrepareFishingContext (biome rolls)（Projectile.cs:20858, 20980-21031）
17. Gate: Next(100) <= (FL+75)/2 (int)  =>  P = min(1, (floor((FL+75)/2)+1)/100); on success SetFishingCheckResults: RollEnemySpawns then rule list（Projectile.cs:20710-20714, 20862-20925）

> 注意：FishingCheck_RollDropLevels does 150/fishingLevel with no zero guard; fishingLevel can reach 0 after the water/luck multipliers for very low power (DivideByZero in C#). Guard FL>=1 in a port.

### 2.3 渔力公式

`FinalFishingLevel = int((BaitPower + PolePower + fishingSkill + situational) * multiplier)  [Player.cs:42524-42526]`

- 钓竿：held item's fishingPole if >0, else the highest fishingPole in inventory slots 0-57 (Player.cs:42604-42620)；鱼饵：first item with bait>0 in ammo slots 54-57, else in slots 0-49 (Player.cs:42576-42602); BaitPower 0 or PolePower 0 -> FinalFishingLevel 0。
- fishingSkill 来源：Fishing Potion buff 121 +15（Player.cs:10233-10236）；Angler Earring 2374 / Angler Tackle Bag 3721 / Lavaproof Tackle Bag 5064 +10（Player.cs:14760-14763, 14776-14788）；any Fishing Bobber 5139-5146 +10（Player.cs:13083-13086）；each Angler/Captain armor piece 2367-2369, 5591-5593 +5（Player.cs:13546-13549）；Gummy Worm permanent +3（Player.cs:12952-12955）。
- 情境加成（只在 GetFishingConditions 内）：Tipsy buff (25) +5（Player.cs:42512-42515）；canFloatInWater && wet (Inner Tube / Flamingo mount) +5（Player.cs:42516-42519）；sitting on furniture +5（Player.cs:42520-42523）。
- 乘数（Player.cs:42530-42574，全部相乘）：

| 条件 | 乘数 |
|---|---|
| Main.raining | ×1.2 |
| Main.cloudBGAlpha > 0 | ×1.1 |
| day && (time < 5400 || time > 48600)  [04:30-06:00, 18:00-19:30] | ×1.3 |
| day && 16200 < time < 37800  [09:00-15:00] | ×0.8 |
| night && 6480 < time < 25920  [21:18-02:42] | ×0.8 |
| moonPhase == 0 (full) | ×1.1 |
| moonPhase 1 or 7 | ×1.05 |
| moonPhase 3 or 5 | ×0.95 |
| moonPhase == 4 (new) | ×0.9 |
| Main.bloodMoon | ×1.1 |

  Day is 54000 ticks starting 04:30; night 32400 ticks starting 19:30. All factors multiply.
- FishingCheck 内再修正（顺序）：鱼饵桶 chum（池内 >0 个 +11，>1 再 +6，>2 再 +3）→ 水量惩罚 → 幸运。chum (+11/+6/+3), water penalty, luck: only inside FishingCheck; the bite timer and Bomb Fish/Frost Daggerfish stacks use FinalFishingLevel without them。
- 水量：`waterNeededToFish = int(300 × atmo)`，`atmo = clamp((浮标position.Y/16 − (60 + 10×(maxTilesX/4200)²)) / (worldSurface/6), 0.25, 1)`（高空所需水量最低 75）。`waterTilesCount < 75` 直接不能钓；`< waterNeededToFish` 时 `FL = int(FL × water/needed)`，并允许出垃圾。
- 幸运：luck<0 时以 −luck 概率 `FL = int(FL×(0.9−0.3×r))`；luck>0 时以 luck 概率 `FL = int(FL×(1.1+0.3×r))`（Projectile.cs:20796-20806）。

### 2.4 稀有度与宝匣/垃圾判定（Projectile.cs:21635-21697, 20855）

各标志独立掷骰，可同时为真：

| 标志 | 概率 |
|---|---|
| common | 1/max(2, 150/FL) |
| uncommon | 1/max(3, 300/FL) |
| rare | 1/max(4, 1050/FL) |
| veryrare | 1/max(5, 2250/FL) |
| legendary | 1/max(6, 4500/FL) |
| crate | Next(100) < 10 (+15 with Crate Potion => 25%) |
| junk | `Next(50)>FL && Next(50)>FL && 水 < 所需水量` |

例：FL=50 → common 1/3、uncommon 1/6、rare 1/21、veryrare 1/45、legendary 1/90；FL≥75 → common 1/2；FL≥750 时所有分母封底（2/3/4/5/6）。最终是否出货还要过门槛 `Next(100) <= (FL+75)/2`（FL≥123 必过）。

### 2.5 鱼池扫描、高度层、群系判定

- 鱼池（Projectile.cs:21699-21743）：Scan left/right along row Y from the bobber tile while tile has liquid>0 and is not solid (stops at x=10 / maxTilesX-10). For every column in [minX,maxX] count tiles downward from Y while liquid>0 && !solid && y<maxTilesY-10. Any lava tile -> inLava; any honey tile (non-lava) -> inHoney. chumsInWater sums chum counts at every counted tile. If honey: waterTilesCount = int(count*1.5). Liquid amount per tile is ignored (any liquid>0 counts as 1).
- 高度层（Projectile.cs:20807-20854，按浮标图格 Y）：0=sky / 太空（Y < worldSurface*0.5）；1=surface / 地表（Y < worldSurface）；2=underground / 地下（Y < rockLayer）；3=cavern / 洞穴（Y < maxTilesY-300）；4=underworld / 地狱（else）。颠倒世界：remixWorld: <0.5ws ->0, <ws ->1, <rockLayer ->3, <maxTilesY-300 ->2 (then 50% ->1), else 4。
- 群系判定（Projectile.cs:20980-21031）：
  - corruption=Player.ZoneCorrupt, crimson=ZoneCrimson, jungle=ZoneJungle, snow=ZoneSnow, dungeon=ZoneDungeon && NPC.downedBoss3
  - notTheBees && !remix: 50% jungle=false
  - remix && heightLevel==0: corruption=crimson=false; else if both corruption and crimson: 50/50 keep one
  - snow && jungle: 50% snow=false
  - desert = ZoneDesert && !dungeon; RolledInfectedDesert = desert && Next(2)==0 (used only with ZoneHallow)
  - RolledRemixOcean = remix && heightLevel==1 && Y>=rockLayer && Next(3)==0
  - Ocean (non-remix) = heightLevel<=1 && (X<380 || X>maxTilesX-380) && waterTilesCount>1000  (AFishDropRulePopulator.cs:257-264; X is the BOBBER tile, not ZoneBeach)
  - Hallow, Beach (ocean crates), Glowshroom, TrueDesert (oasis crates), TrueSnow (Scaly Truffle) read Player zone flags directly
- 任务鱼探测（Projectile.cs:21618-21633）：questFish = Main.anglerQuestItemNetIDs[Main.anglerQuest]; set -1 if the player already has that item (Player.HasItem), or no Angler NPC (369) exists, or Main.anglerQuestFinished。
- 血月敌怪（Projectile.cs:20927-20958）：需要 !inLava && !inHoney && Main.bloodMoon && !Main.dayTime，概率 1/6 (1/3 with Chum Caster 4325)；if !NPC.unlockedSlimeRedSpawn && Next(5)==0 -> Surly Slime 682; else if hardMode -> one of {620 Hemogoblin Shark, 621 Blood Eel, 586 Zombie Merman, 587 Wandering Eye Fish}, then Next(10)==0 overrides to 618 Dreadnautilus; else one of {586, 587}。an enemy hook blocks any item (stopper rule #0); reeling spawns the NPC。

### 2.6 规则表求值

for rule in rules (in order): if all conditions match AND Next(den) < num AND rarity flag matches: return random item from rule.items (uniform, duplicates weight) or 0 for a stopper (nothing caught). If no rule matches: 0.（Terraria.GameContent.FishDropRules/FishDropRule.cs:31-51, FishDropRuleList.cs:10-21; populated in Main.cs:6752-6754）。Rule 157 (Bass, Rarity Any, no conditions) means every non-lava/honey/ocean/junk/crate check that reaches the end yields Bass. “停止规则(stopper)”= 条件成立即返回 0（什么都钓不到），用于熔岩/蜂蜜/垃圾/宝匣/海洋/敌怪分支收尾。规则内部先判条件、再掷 `Next(den) < num`、再判稀有度标志（FishDropRule.cs:31-51），三者独立，概率相乘。

## 3. 渔获规则表（按求值顺序，GameContentFishDropPopulator.cs）

列：`#` 规则序号；稀有度标志；规则自身概率；物品（多个则均匀抽取，重复项=权重）；附加条件；源码行。


**入口**（Populate）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 0 | 任意 | 必定 | （停止：无渔获） | 已判出血月敌怪 | 12 |

**熔岩**（LavaDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 1 | 任意 | 1/6 | 黑曜石匣 / Obsidian Crate (4877) | 鱼池含熔岩，可熔岩钓鱼，宝匣判定成立，肉前 | 242 |
| 2 | 任意 | 1/6 | 狱石匣 / Hellstone Crate (4878) | 鱼池含熔岩，可熔岩钓鱼，宝匣判定成立，困难模式 | 242 |
| 3 | 传说 | 1/3 | 恶魔海螺 / Demon Conch (4819)、无底熔岩桶 / Bottomless Lava Bucket (4820)、熔岩吸收绵 / Lava Absorbant Sponge (4872)、黑曜石剑鱼 / Obsidian Swordfish (2331) | 鱼池含熔岩，可熔岩钓鱼，困难模式 | 243 |
| 4 | 传说 | 1/3 | 恶魔海螺 / Demon Conch (4819)、无底熔岩桶 / Bottomless Lava Bucket (4820)、熔岩吸收绵 / Lava Absorbant Sponge (4872) | 鱼池含熔岩，可熔岩钓鱼，肉前 | 244 |
| 5 | 极稀有 | 必定 | 闪鳍锦鲤 / Flarefin Koi (2312) | 鱼池含熔岩，可熔岩钓鱼 | 245 |
| 6 | 稀有 | 必定 | 黑曜石鱼 / Obsidifish (2315) | 鱼池含熔岩，可熔岩钓鱼 | 246 |
| 7 | 任意 | 必定 | （停止：无渔获） | 鱼池含熔岩 | 247 |

**蜂蜜**（HoneyDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 8 | 稀有 | 必定 | 蜂蜜鱼 / Honeyfin (2314) | 鱼池含蜂蜜 | 233 |
| 9 | 罕见 | 1/2 | 蜂蜜鱼 / Honeyfin (2314) | 鱼池含蜂蜜 | 234 |
| 10 | 罕见 | 必定 | 任务鱼 大黄蜂金枪鱼 / Bumblebee Tuna (2451) | 鱼池含蜂蜜，当前任务鱼 | 235 |
| 11 | 任意 | 必定 | （停止：无渔获） | 鱼池含蜂蜜 | 236 |

**垃圾**（JunkDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 12 | 任意 | 1/8 | Joja可乐 / Joja Cola (5275) | 垃圾判定成立 | 226 |
| 13 | 任意 | 必定 | 旧鞋 / Old Shoe (2337)、海草 / Seaweed (2338)、锡罐 / Tin Can (2339) | 垃圾判定成立 | 227 |
| 14 | 任意 | 必定 | （停止：无渔获） | 垃圾判定成立 | 228 |

**宝匣**（CrateDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 15 | 稀有 | 必定 | 地牢匣 / Dungeon Crate (3205) | 宝匣判定成立，地牢(且已杀骷髅王)，肉前 | 206 |
| 16 | 稀有 | 必定 | 围栏匣 / Stockade Crate (3984) | 宝匣判定成立，地牢(且已杀骷髅王)，困难模式 | 206 |
| 17 | 稀有 | 必定 | 海洋匣 / Ocean Crate (5002) | 宝匣判定成立，ZoneBeach 海洋，肉前 | 207 |
| 18 | 稀有 | 必定 | 海边匣 / Seaside Crate (5003) | 宝匣判定成立，ZoneBeach 海洋，困难模式 | 207 |
| 19 | 稀有 | 必定 | 腐化匣 / Corrupt Crate (3203) | 宝匣判定成立，腐化(判定后)，肉前 | 208 |
| 20 | 稀有 | 必定 | 污损匣 / Defiled Crate (3982) | 宝匣判定成立，腐化(判定后)，困难模式 | 208 |
| 21 | 稀有 | 必定 | 猩红匣 / Crimson Crate (3204) | 宝匣判定成立，猩红(判定后)，肉前 | 209 |
| 22 | 稀有 | 必定 | 血匣 / Hematic Crate (3983) | 宝匣判定成立，猩红(判定后)，困难模式 | 209 |
| 23 | 稀有 | 必定 | 神圣匣 / Hallowed Crate (3207) | 宝匣判定成立，ZoneHallow 神圣，肉前 | 210 |
| 24 | 稀有 | 必定 | 天赐匣 / Divine Crate (3986) | 宝匣判定成立，ZoneHallow 神圣，困难模式 | 210 |
| 25 | 稀有 | 必定 | 丛林匣 / Jungle Crate (3208) | 宝匣判定成立，丛林(判定后)，肉前 | 211 |
| 26 | 稀有 | 必定 | 荆棘匣 / Bramble Crate (3987) | 宝匣判定成立，丛林(判定后)，困难模式 | 211 |
| 27 | 稀有 | 必定 | 冰冻匣 / Frozen Crate (4405) | 宝匣判定成立，雪原(判定后)，肉前 | 212 |
| 28 | 稀有 | 必定 | 针叶木匣 / Boreal Crate (4406) | 宝匣判定成立，雪原(判定后)，困难模式 | 212 |
| 29 | 稀有 | 必定 | 绿洲匣 / Oasis Crate (4407) | 宝匣判定成立，ZoneDesert 沙漠(未过滤)，肉前 | 213 |
| 30 | 稀有 | 必定 | 幻象匣 / Mirage Crate (4408) | 宝匣判定成立，ZoneDesert 沙漠(未过滤)，困难模式 | 213 |
| 31 | 稀有 | 必定 | 天空匣 / Sky Crate (3206) | 宝匣判定成立，高度=太空，肉前 | 214 |
| 32 | 稀有 | 必定 | 天蓝匣 / Azure Crate (3985) | 宝匣判定成立，高度=太空，困难模式 | 214 |
| 33 | 稀有 | 必定 | 海洋匣 / Ocean Crate (5002) | 宝匣判定成立，颠倒世界种子，高度=地表，浮标Y>=rockLayer，肉前 | 215 |
| 34 | 稀有 | 必定 | 海边匣 / Seaside Crate (5003) | 宝匣判定成立，颠倒世界种子，高度=地表，浮标Y>=rockLayer，困难模式 | 215 |
| 35 | 传说 | 必定 | 金匣 / Golden Crate (2336) | 宝匣判定成立，肉前 | 216 |
| 36 | 传说 | 必定 | 钛金匣 / Titanium Crate (3981) | 宝匣判定成立，困难模式 | 216 |
| 37 | 极稀有 | 必定 | 金匣 / Golden Crate (2336) | 宝匣判定成立，肉前 | 217 |
| 38 | 极稀有 | 必定 | 钛金匣 / Titanium Crate (3981) | 宝匣判定成立，困难模式 | 217 |
| 39 | 稀有 | 必定 | 铁匣 / Iron Crate (2335) | 宝匣判定成立，肉前 | 218 |
| 40 | 稀有 | 必定 | 秘银匣 / Mythril Crate (3980) | 宝匣判定成立，困难模式 | 218 |
| 41 | 罕见 | 必定 | 铁匣 / Iron Crate (2335) | 宝匣判定成立，肉前 | 219 |
| 42 | 罕见 | 必定 | 秘银匣 / Mythril Crate (3980) | 宝匣判定成立，困难模式 | 219 |
| 43 | 任意 | 必定 | 木匣 / Wooden Crate (2334) | 宝匣判定成立，肉前 | 220 |
| 44 | 任意 | 必定 | 珍珠木匣 / Pearlwood Crate (3979) | 宝匣判定成立，困难模式 | 220 |
| 45 | 任意 | 必定 | （停止：无渔获） | 宝匣判定成立 | 221 |

**全局稀有**（RareDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 46 | 传说 | 1/2 | 先进战斗技术 / Advanced Combat Techniques (4382) | 血月，未使用过战斗书 | 196 |
| 47 | 传说 | 1/2 | 红海恐惧 / Dread of the Red Sea (5240) | 血月 | 197 |
| 48 | 传说 | 1/5 | 蛙腿 / Frog Leg (2423) | — | 198 |
| 49 | 传说 | 1/5 | 气球河豚鱼 / Balloon Pufferfish (3225) | — | 199 |
| 50 | 传说 | 1/10 | 和风鱼 / Zephyr Fish (2420) | — | 200 |
| 51 | 罕见且非极稀有非传说 | 1/5 | 炸弹鱼 / Bomb Fish (3196) | — | 201 |

**颠倒世界任务鱼**（RemixDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 52 | 罕见 | 必定 | 任务鱼 鸟妖鱼 / Harpyfish (2461) | 当前任务鱼 且颠倒世界 | 34 |
| 53 | 罕见 | 必定 | 任务鱼 坠落星鱼 / Fallen Starfish (2458) | 当前任务鱼 且颠倒世界 | 35 |
| 54 | 罕见 | 必定 | 任务鱼 克苏鲁鱼 / The Fish of Cthulhu (2459) | 当前任务鱼 且颠倒世界 | 36 |
| 55 | 罕见 | 必定 | 任务鱼 兔兔鱼 / Bunnyfish (2479) | 当前任务鱼 且颠倒世界 | 37 |
| 56 | 罕见 | 必定 | 任务鱼 雷管鱼 / Dynamite Fish (2456) | 当前任务鱼 且颠倒世界 | 38 |
| 57 | 罕见 | 必定 | 任务鱼 僵尸鱼 / Zombie Fish (2474) | 当前任务鱼 且颠倒世界 | 39 |
| 58 | 罕见 | 必定 | 任务鱼 骷髅鱼 / Bonefish (2478) | 当前任务鱼 且颠倒世界 | 40 |
| 59 | 罕见 | 必定 | 任务鱼 蝙蝠鱼 / Batfish (2450) | 当前任务鱼 且颠倒世界 | 41 |
| 60 | 罕见 | 必定 | 任务鱼 珠宝鱼 / Jewelfish (2464) | 当前任务鱼 且颠倒世界 | 42 |
| 61 | 罕见 | 必定 | 任务鱼 蜘蛛鱼 / Spiderfish (2469) | 当前任务鱼 且颠倒世界 | 43 |

**地牢**（DungeonDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 62 | 极稀有 | 1/12 | 炼药桌 / Alchemy Table (3000) | 地牢(且已杀骷髅王) | 190 |
| 63 | 极稀有 | 1/12 | 施法桌 / Bewitching Table (2999) | 地牢(且已杀骷髅王) | 191 |

**腐化**（CorruptionDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 64 | 传说 | 2/3 | 带鳞松露 / Scaly Truffle (2429) | ZoneSnow 雪原(未过滤)，腐化(判定后)，困难模式，高度=洞穴 | 179 |
| 65 | 传说 | 1/2 | 毒弹枪 / Toxikarp (3210) | 腐化(判定后)，困难模式 | 180 |
| 66 | 稀有 | 必定 | 紫挥棒鱼 / Purple Clubberfish (2330) | 腐化(判定后) | 181 |
| 67 | 罕见 | 必定 | 任务鱼 诅咒鱼 / Cursedfish (2454) | 腐化(判定后)，当前任务鱼 | 182 |
| 68 | 罕见 | 必定 | 任务鱼 染病鞘鱼 / Infected Scabbardfish (2485) | 腐化(判定后)，当前任务鱼 | 183 |
| 69 | 罕见 | 必定 | 任务鱼 浮游噬鱼 / Eater of Plankton (2457) | 腐化(判定后)，当前任务鱼 | 184 |
| 70 | 罕见 | 必定 | 黑檀锦鲤 / Ebonkoi (2318) | 腐化(判定后) | 185 |

**猩红**（CrimsonDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 71 | 传说 | 2/3 | 带鳞松露 / Scaly Truffle (2429) | ZoneSnow 雪原(未过滤)，猩红(判定后)，困难模式，高度=洞穴 | 168 |
| 72 | 传说 | 1/2 | 舌锋剑 / Bladetongue (3211) | 猩红(判定后)，困难模式 | 169 |
| 73 | 罕见 | 必定 | 任务鱼 血腥战神 / Bloody Manowar (2477) | 猩红(判定后)，当前任务鱼 | 170 |
| 74 | 罕见 | 必定 | 任务鱼 灵液鱼 / Ichorfish (2463) | 猩红(判定后)，当前任务鱼 | 171 |
| 75 | 罕见 | 必定 | 血腥食人鱼 / Hemopiranha (2319) | 猩红(判定后) | 172 |
| 76 | 普通 | 必定 | 猩红虎鱼 / Crimson Tigerfish (2305) | 猩红(判定后) | 173 |

**神圣（含神圣沙漠）**（HallowedDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 77 | 传说 | 必定 | 巴西天空 / Brasilian Skies (5490) | 神圣沙漠(50%判定) | 147 |
| 78 | 罕见 | 必定 | 任务鱼 甲虫鱼 / Scarab Fish (4393) | 神圣沙漠(50%判定)，当前任务鱼 | 148 |
| 79 | 罕见 | 必定 | 任务鱼 蝎子鱼 / Scorpio Fish (4394) | 神圣沙漠(50%判定)，当前任务鱼 | 149 |
| 80 | 罕见 | 必定 | 牡蛎 / Oyster (4410) | 神圣沙漠(50%判定) | 150 |
| 81 | 任意 | 1/3 | 岩石龙虾 / Rock Lobster (4402) | 神圣沙漠(50%判定) | 151 |
| 82 | 任意 | 必定 | 偏口鱼 / Flounder (4401) | 神圣沙漠(50%判定) | 152 |
| 83 | 传说 | 2/3 | 带鳞松露 / Scaly Truffle (2429) | ZoneSnow 雪原(未过滤)，ZoneHallow 神圣，困难模式，高度=洞穴 | 154 |
| 84 | 传说 | 1/2 | 水晶蛇 / Crystal Serpent (3209) | ZoneHallow 神圣，困难模式 | 155 |
| 85 | 传说 | 2/3 | 湖中女士 / Lady Of The Lake (5274) | ZoneHallow 神圣，困难模式 | 156 |
| 86 | 极稀有 | 必定 | 混沌鱼 / Chaos Fish (2317) | ZoneHallow 神圣，高度>地表(地下/洞穴/地狱) | 157 |
| 87 | 罕见 | 必定 | 任务鱼 幻象鱼 / Mirage Fish (2465) | ZoneHallow 神圣，高度>地表(地下/洞穴/地狱)，当前任务鱼 | 158 |
| 88 | 罕见 | 必定 | 任务鱼 妖精鱼 / Pixiefish (2468) | ZoneHallow 神圣，高度<地下(太空/地表)，当前任务鱼 | 159 |
| 89 | 稀有 | 必定 | 七彩矿鱼 / Prismite (2310) | ZoneHallow 神圣 | 160 |
| 90 | 罕见 | 必定 | 任务鱼 独角兽鱼 / Unicorn Fish (2471) | ZoneHallow 神圣，当前任务鱼 | 161 |
| 91 | 罕见 | 必定 | 公主鱼 / Princess Fish (2307) | ZoneHallow 神圣 | 162 |

**发光蘑菇**（GlowingMushroomsDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 92 | 罕见 | 必定 | 任务鱼 毒菌鱼 / Amanita Fungifin (2475) | ZoneGlowshroom 发光蘑菇，当前任务鱼 | 141 |

**雪原**（SnowDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 93 | 罕见 | 必定 | 任务鱼 企鹅鱼 / Pengfish (2467) | 雪原(判定后)，高度<地下(太空/地表)，当前任务鱼 | 128 |
| 94 | 罕见 | 必定 | 任务鱼 苔原鳟鱼 / Tundra Trout (2470) | 雪原(判定后)，高度=地表，当前任务鱼 | 129 |
| 95 | 罕见 | 必定 | 任务鱼 猪龙鱼 / Fishron (2484) | 雪原(判定后)，高度>地表(地下/洞穴/地狱)，当前任务鱼 | 130 |
| 96 | 罕见 | 必定 | 任务鱼 突变雪怪鱼 / Mutant Flinxfin (2466) | 雪原(判定后)，高度>地表(地下/洞穴/地狱)，当前任务鱼 | 131 |
| 97 | 普通 | 1/12 | 寒霜飞鱼 / Frost Daggerfish (3197) | 雪原(判定后) | 132 |
| 98 | 罕见 | 1/6 | 寒霜飞鱼 / Frost Daggerfish (3197) | 雪原(判定后) | 133 |
| 99 | 罕见 | 必定 | 寒霜鲦鱼 / Frost Minnow (2306) | 雪原(判定后) | 134 |
| 100 | 普通 | 必定 | 大西洋鳕鱼 / Atlantic Cod (2299) | 雪原(判定后) | 135 |
| 101 | 任意 | 1/3 | 镜面鱼 / Specular Fish (2309) | 雪原(判定后)，高度>地表(地下/洞穴/地狱) | 136 |

**丛林**（JungleDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 102 | 传说 | 2/3 | 藤壶法杖 / Barnacle Staff (5463) | 丛林(判定后)，困难模式 | 114 |
| 103 | 传说 | 2/3 | 邋遢怪来啦 / It's Scragglin' Time (5634) | 丛林(判定后) | 115 |
| 104 | 罕见 | 必定 | 任务鱼 猫鱼 / Catfish (2452) | 丛林(判定后)，高度=地表，当前任务鱼 | 116 |
| 105 | 罕见 | 必定 | 任务鱼 跳跳鱼 / Derpfish (2483) | 丛林(判定后)，高度=地表，当前任务鱼 | 117 |
| 106 | 罕见 | 必定 | 任务鱼 热带梭鱼 / Tropical Barracuda (2488) | 丛林(判定后)，高度=地表，当前任务鱼 | 118 |
| 107 | 罕见 | 必定 | 任务鱼 泥鱼 / Mudfish (2486) | 丛林(判定后)，高度>=地表(非太空)，当前任务鱼 | 119 |
| 108 | 罕见 | 必定 | 斑驳油鱼 / Variegated Lardfish (2311) | 丛林(判定后)，高度>地表(地下/洞穴/地狱) | 120 |
| 109 | 罕见 | 必定 | 双鳍鳕鱼 / Double Cod (2313) | 丛林(判定后) | 121 |
| 110 | 普通 | 必定 | 霓虹脂鲤 / Neon Tetra (2302) | 丛林(判定后) | 122 |

**海洋**（OceanDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 111 | 极稀有 | 1/2 | 掠夺鲨 / Reaver Shark (2341) | 海洋鱼池(高度≤地表、浮标距世界左右边缘<380格、水>1000格) | 98 |
| 112 | 极稀有 | 必定 | 锯齿鲨 / Sawtooth Shark (2342) | 海洋鱼池(高度≤地表、浮标距世界左右边缘<380格、水>1000格) | 99 |
| 113 | 稀有 | 1/5 | 粉水母 / Pink Jellyfish (2438) | 海洋鱼池(高度≤地表、浮标距世界左右边缘<380格、水>1000格) | 100 |
| 114 | 稀有 | 1/3 | 剑鱼 / Swordfish (2332) | 海洋鱼池(高度≤地表、浮标距世界左右边缘<380格、水>1000格) | 101 |
| 115 | 罕见 | 必定 | 任务鱼 金枪鱼须船长 / Cap'n Tunabeard (2480) | 海洋鱼池(高度≤地表、浮标距世界左右边缘<380格、水>1000格)，当前任务鱼 | 102 |
| 116 | 罕见 | 必定 | 任务鱼 小丑鱼 / Clownfish (2481) | 海洋鱼池(高度≤地表、浮标距世界左右边缘<380格、水>1000格)，当前任务鱼 | 103 |
| 117 | 罕见 | 必定 | 虾 / Shrimp (2316) | 海洋鱼池(高度≤地表、浮标距世界左右边缘<380格、水>1000格) | 104 |
| 118 | 普通 | 1/2 | 红鲷鱼 / Red Snapper (2301) | 海洋鱼池(高度≤地表、浮标距世界左右边缘<380格、水>1000格) | 105 |
| 119 | 普通 | 必定 | 金枪鱼 / Tuna (2300) | 海洋鱼池(高度≤地表、浮标距世界左右边缘<380格、水>1000格) | 106 |
| 120 | 任意 | 必定 | 鳟鱼 / Trout (2297) | 海洋鱼池(高度≤地表、浮标距世界左右边缘<380格、水>1000格) | 107 |
| 121 | 任意 | 必定 | （停止：无渔获） | 海洋鱼池(高度≤地表、浮标距世界左右边缘<380格、水>1000格) | 108 |

**沙漠**（DesertDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 122 | 传说 | 1/3 | 巴西天空 / Brasilian Skies (5490) | 沙漠(判定后) | 87 |
| 123 | 罕见 | 必定 | 任务鱼 甲虫鱼 / Scarab Fish (4393) | 沙漠(判定后)，当前任务鱼 | 88 |
| 124 | 罕见 | 必定 | 任务鱼 蝎子鱼 / Scorpio Fish (4394) | 沙漠(判定后)，当前任务鱼 | 89 |
| 125 | 罕见 | 必定 | 牡蛎 / Oyster (4410) | 沙漠(判定后) | 90 |
| 126 | 任意 | 1/3 | 岩石龙虾 / Rock Lobster (4402) | 沙漠(判定后) | 91 |
| 127 | 任意 | 必定 | 偏口鱼 / Flounder (4401) | 沙漠(判定后) | 92 |

**太空/浮岛**（FloatingIslandDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 128 | 罕见 | 必定 | 任务鱼 鸟妖鱼 / Harpyfish (2461) | 高度<地下(太空/地表)，当前任务鱼 | 75 |
| 129 | 罕见 | 必定 | 任务鱼 云鱼 / Cloudfish (2453) | 高度=太空，当前任务鱼 | 76 |
| 130 | 罕见 | 必定 | 任务鱼 飞龙尾 / Wyverntail (2473) | 高度=太空，当前任务鱼 | 77 |
| 131 | 罕见 | 必定 | 任务鱼 天使鱼 / Angelfish (2476) | 高度=太空，当前任务鱼 | 78 |
| 132 | 罕见 | 必定 | 任务鱼 坠落星鱼 / Fallen Starfish (2458) | 高度<地下(太空/地表)，当前任务鱼 | 79 |
| 133 | 罕见 | 必定 | 任务鱼 克苏鲁鱼 / The Fish of Cthulhu (2459) | 高度<地下(太空/地表)，当前任务鱼 | 80 |
| 134 | 罕见 | 必定 | 雀鲷 / Damselfish (2304) | 高度=太空 | 81 |

**地表/地下/洞穴（兜底）**（SurfaceDrops）

| # | 稀有度 | 概率 | 物品 | 条件 | 行 |
|---|---|---|---|---|---|
| 135 | 罕见 | 必定 | 任务鱼 土鱼 / Dirtfish (2455) | 高度=地表或地下，当前任务鱼 | 48 |
| 136 | 罕见 | 必定 | 任务鱼 兔兔鱼 / Bunnyfish (2479) | 高度=地表，当前任务鱼 | 49 |
| 137 | 罕见 | 必定 | 任务鱼 雷管鱼 / Dynamite Fish (2456) | 高度=地表，当前任务鱼 | 50 |
| 138 | 罕见 | 必定 | 任务鱼 僵尸鱼 / Zombie Fish (2474) | 高度=地表，当前任务鱼 | 51 |
| 139 | 稀有 | 1/10 | 绿水母 / Green Jellyfish (2437) | 高度>地表(地下/洞穴/地狱)，困难模式 | 52 |
| 140 | 稀有 | 1/9 | 蓝水母 / Blue Jellyfish (2436) | 高度>地表(地下/洞穴/地狱)，困难模式 | 53 |
| 141 | 稀有 | 1/5 | 蓝水母 / Blue Jellyfish (2436) | 高度>地表(地下/洞穴/地狱)，肉前 | 54 |
| 142 | 传说 | 2/3 | 金鲤鱼 / Golden Carp (2308) | 高度>地表(地下/洞穴/地狱) | 55 |
| 143 | 极稀有 | 1/2 | 岩鱼锤 / Rockfish (2320) | 高度>地表(地下/洞穴/地狱) | 56 |
| 144 | 稀有 | 必定 | 臭味鱼 / Stinkfish (2321) | 高度>地表(地下/洞穴/地狱) | 57 |
| 145 | 罕见 | 必定 | 任务鱼 骷髅鱼 / Bonefish (2478) | 高度>地表(地下/洞穴/地狱)，当前任务鱼 | 58 |
| 146 | 罕见 | 必定 | 任务鱼 蝙蝠鱼 / Batfish (2450) | 高度>地表(地下/洞穴/地狱)，当前任务鱼 | 59 |
| 147 | 罕见 | 必定 | 任务鱼 珠宝鱼 / Jewelfish (2464) | 高度>地表(地下/洞穴/地狱)，当前任务鱼 | 60 |
| 148 | 罕见 | 必定 | 任务鱼 蜘蛛鱼 / Spiderfish (2469) | 高度>地表(地下/洞穴/地狱)，当前任务鱼 | 61 |
| 149 | 罕见 | 必定 | 任务鱼 饿鬼鱼 / Hungerfish (2462) | 高度>地下(洞穴/地狱)，当前任务鱼 | 62 |
| 150 | 罕见 | 必定 | 任务鱼 恶魔地狱鱼 / Demonic Hellfish (2482) | 高度>地下(洞穴/地狱)，当前任务鱼 | 63 |
| 151 | 罕见 | 必定 | 任务鱼 向导巫毒鱼 / Guide Voodoo Fish (2472) | 高度>地下(洞穴/地狱)，当前任务鱼 | 64 |
| 152 | 罕见 | 必定 | 任务鱼 骷髅王鱼 / Fishotron (2460) | 高度>地下(洞穴/地狱)，当前任务鱼 | 65 |
| 153 | 罕见 | 3/4 | 装甲洞穴鱼 / Armored Cavefish (2303) | 高度>地表(地下/洞穴/地狱) | 66 |
| 154 | 罕见或普通 | 1/4 | 装甲洞穴鱼 / Armored Cavefish (2303)、镜面鱼 / Specular Fish (2309)、镜面鱼 / Specular Fish (2309)、镜面鱼 / Specular Fish (2309) | 高度>地表(地下/洞穴/地狱) | 67 |
| 155 | 罕见 | 必定 | 任务鱼 史莱姆鱼 / Slimefish (2487) | 当前任务鱼 | 68 |
| 156 | 普通 | 必定 | 三文鱼 / Salmon (2298) | 水>1000格 | 69 |
| 157 | 任意 | 必定 | 鲈鱼 / Bass (2290) | — | 70 |

## 4. 按环境速查（由规则表归纳，权威仍是第 3 节顺序）

| 环境 | 内容 |
|---|---|
| 任何地点（熔岩/蜂蜜/垃圾/宝匣分支之后、群系之前） | 血月：传说 1/2 先进战斗技术(4382)(需未用过) → 传说 1/2 红海恐惧(5240)；传说 1/5 蛙腿(2423)、1/5 气球河豚鱼(3225)、1/10 和风鱼(2420)；罕见且非极稀有非传说 1/5 炸弹鱼(3196)。 |
| 熔岩（需可熔岩钓鱼） | 宝匣 → 1/6 黑曜石匣(4877)/狱石匣(4878)；传说 1/3 恶魔海螺(4819)/无底熔岩桶(4820)/熔岩吸收绵(4872)（困难模式另加 黑曜石剑鱼(2331)）；极稀有 闪鳍锦鲤(2312)；稀有 黑曜石鱼(2315)；否则无。不能熔岩钓鱼时熔岩池什么都钓不到。 |
| 蜂蜜 | 稀有 蜂蜜鱼(2314)；罕见 1/2 蜂蜜鱼(2314)；罕见 任务鱼 大黄蜂金枪鱼(2451)；否则无（蜂蜜池不出宝匣/垃圾）。 |
| 垃圾 | 1/8 Joja可乐(5275)，否则 旧鞋(2337)/海草(2338)/锡罐(2339) 均匀。 |
| 海洋（浮标 X<380 或 >maxTilesX-380、高度≤地表、水>1000） | 极稀有 1/2 掠夺鲨(2341)，否则 锯齿鲨(2342)；稀有 1/5 粉水母(2438)、1/3 剑鱼(2332)；罕见 任务鱼 金枪鱼须船长(2480)/小丑鱼(2481)，否则 虾(2316)；普通 1/2 红鲷鱼(2301)，否则 金枪鱼(2300)；其余 鳟鱼(2297)；以停止规则收尾。宝匣用的是玩家 ZoneBeach。 |
| 腐化 | 困难模式+洞穴+雪原 传说 2/3 带鳞松露(2429)；困难模式 传说 1/2 毒弹枪(3210)；稀有 紫挥棒鱼(2330)；罕见 任务鱼，否则 黑檀锦鲤(2318)。 |
| 猩红 | 同上 带鳞松露(2429)；困难模式 传说 1/2 舌锋剑(3211)；罕见 任务鱼，否则 血腥食人鱼(2319)；普通 猩红虎鱼(2305)。 |
| 神圣 | 神圣沙漠（沙漠判定后再 50%）：传说 巴西天空(5490)(必定)、罕见 任务鱼/牡蛎(4410)、1/3 岩石龙虾(4402)、否则 偏口鱼(4401)。普通神圣：带鳞松露(2429)；困难模式 传说 1/2 水晶蛇(3209)、2/3 湖中女士(5274)；高度>地表 极稀有 混沌鱼(2317)；稀有 七彩矿鱼(2310)；罕见 任务鱼，否则 公主鱼(2307)。 |
| 发光蘑菇 | 只有任务鱼 毒菌鱼(2475)，其余落到后续规则。 |
| 雪原 | 普通 1/12 或罕见 1/6 寒霜飞鱼(3197)；罕见 任务鱼，否则 寒霜鲦鱼(2306)；普通 大西洋鳕鱼(2299)；高度>地表 1/3 镜面鱼(2309)。 |
| 丛林 | 困难模式 传说 2/3 藤壶法杖(5463)；传说 2/3 邋遢怪来啦(5634)；罕见 任务鱼；高度>地表 罕见 斑驳油鱼(2311)；罕见 双鳍鳕鱼(2313)；普通 霓虹脂鲤(2302)。 |
| 沙漠 | 传说 1/3 巴西天空(5490)；罕见 任务鱼 甲虫鱼(4393)/蝎子鱼(4394)，否则 牡蛎(4410)；1/3 岩石龙虾(4402)；否则 偏口鱼(4401)（沙漠必有渔获，不会落到鲈鱼）。 |
| 地牢（骷髅王后） | 极稀有 1/12 炼药桌(3000)、1/12 施法桌(2999)，其余落到后续规则。 |
| 太空（高度 0） | 罕见 任务鱼，否则 雀鲷(2304)；之后落到兜底。 |
| 高度>地表（地下/洞穴/地狱） | 稀有 困难模式 1/10 绿水母(2437)、1/9 蓝水母(2436)；肉前 1/5 蓝水母(2436)；传说 2/3 金鲤鱼(2308)；极稀有 1/2 岩鱼锤(2320)；稀有 臭味鱼(2321)；罕见 任务鱼；罕见 3/4 装甲洞穴鱼(2303)；罕见或普通 1/4 [装甲洞穴鱼(2303) 1/4、镜面鱼(2309) 3/4]。 |
| 兜底（所有到达末尾的情况） | 罕见 任务鱼 史莱姆鱼(2487)；普通 且 水>1000 三文鱼(2298)；否则 鲈鱼(2290)（必定）。 |

宝匣（宝匣判定 10%/25% 成立且不在熔岩/蜂蜜、未判出垃圾时必出宝匣）：优先级 地牢 > 海洋(ZoneBeach) > 腐化 > 猩红 > 神圣 > 丛林 > 雪原 > 沙漠(ZoneDesert) > 太空 > (颠倒世界海洋) 这些都要求 **稀有**；否则 极稀有/传说→金匣/钛金匣，稀有/罕见→铁匣/秘银匣，其它→木匣/珍珠木匣。困难模式出对应困难版（规则 15-45）。

## 5. 钓竿（每次 1 个浮标；useTime=useAnimation=8）

| ID | 名称 | 渔力 | shootSpeed | 浮标弹幕 | 获取 | 引用 |
|---|---|---|---|---|---|---|
| 2289 | 木钓竿 / Wood Fishing Pole | 5 | 9.0 | 360 BobberWooden | Craft: Wood x8 @ Work Bench (Recipe.cs:6780) | Terraria/Item.cs:23031-23036 |
| 2291 | 强化钓竿 / Reinforced Fishing Pole | 15 | 11.0 | 361 BobberReinforced | Craft: Iron Bar x8 (group IronBar = Iron/Lead) @ Anvil (Recipe.cs:9827) | Terraria/Item.cs:23037-23041 |
| 2293 | 灵魂钓手 / Fisher of Souls | 20 | 13.0 | 363 BobberFisherOfSouls | Craft: Demonite Bar x8 @ Anvil (Recipe.cs:10577) | Terraria/Item.cs:23042-23047 |
| 2421 | 捕肉手 / Fleshcatcher | 22 | 13.5 | 381 BobberFleshcatcher | Craft: Crimtane Bar x8 @ Anvil (Recipe.cs:10655) | Terraria/Item.cs:23084-23090 |
| 4325 | 鱼饵投掷者 / Chum Caster | 25 | 15.0 | 760 BobberBloody | Drop: Zombie Merman / Wandering Eye Fish 1/8 (ItemDropDatabase.cs:170). Special: blood-moon enemy fishing chance 1/3 instead of 1/6 (Projectile.cs:20934-20937) | Terraria/Item.cs:37054-37066 |
| 2292 | 玻璃钢钓竿 / Fiberglass Fishing Pole | 30 | 14.0 | 362 BobberFiberglass | Jungle shrine chest main item 1/15 (WorldGen.cs:10164-10167); Jungle/Bramble Crate primary (19/20 x 1/5) (Player.cs:8725-8732) | Terraria/Item.cs:23048-23053 |
| 4442 | 甲虫钓竿 / Scarab Fishing Rod | 30 | 15.0 | 775 BobberScarab | Oasis/Mirage Crate primary 1/8 (Player.cs:8750-8760) | Terraria/Item.cs:37547-37559 |
| 2295 | 机械师钓竿 / Mechanic's Rod | 35 | 15.0 | 365 BobberMechanics | Shop: Mechanic, only if an Angler NPC exists and Main.moonPhase is 1,3,5 or 7 (Chest.cs:2079-2083) | Terraria/Item.cs:23054-23059 |
| 2296 | 冤大头钓竿 / Sitting Duck's Fishing Pole | 40 | 16.0 | 366 BobbersittingDuck | Shop: Travelling Merchant (rarity slot 3, needs NPC.downedBoss3) (Chest.cs:1048-1051) | Terraria/Item.cs:23060-23065 |
| 2422 | 熔线钓钩 / Hotline Fishing Hook | 45 | 16.5 | 382 BobberHotline | Angler reward: Main.hardMode && questsDone>25 && 1/int(100*r) (Player.cs:57040-57044). ItemID.Sets.CanFishInLava (ItemID.cs:1078)；**可熔岩钓鱼** | Terraria/Item.cs:23091-23097 |
| 2294 | 金钓竿 / Golden Fishing Rod | 50 | 17.0 | 364 BobberGolden | Angler reward: guaranteed on the 30th quest; afterwards questsDone>75 && 1/int(250*r) (Player.cs:57007-57009, 57035-57039) | Terraria/Item.cs:23066-23071 |

## 6. 鱼饵（Item.bait）

消耗概率 = 1/(1+bait/6)，有钓具箱时 1/(2+bait/6)。

| ID | 名称 | 饵力 | 获取/备注 | 引用 |
|---|---|---|---|---|
| 1994 | 帝王蝶 / Monarch Butterfly | 5 | critter (bug net): butterfly, NPC 356 style 0 | Terraria/Item.cs:21346 |
| 1998 | 黄粉蝶 / Sulphur Butterfly | 10 | critter (bug net): butterfly style 4 | Terraria/Item.cs:21350 |
| 2006 | 蜗牛 / Snail | 10 | critter (bug net): Snail NPC 359 | Terraria/Item.cs:21409 |
| 2157 | 蝎子 / Scorpion | 10 | critter (bug net): Scorpion NPC 367 (desert) | Terraria/Item.cs:22079 |
| 2740 | 蚱蜢 / Grasshopper | 10 | critter (bug net): Grasshopper NPC 377 | Terraria/Item.cs:26167 |
| 5132 | 臭虫 / Stinkbug | 10 | critter (bug net): Stinkbug NPC 669 | Terraria/Item.cs:41101 |
| 2000 | 带凤蝶 / Zebra Swallowtail Butterfly | 15 | critter (bug net): butterfly style 6 | Terraria/Item.cs:21354 |
| 2007 | 发光蜗牛 / Glowing Snail | 15 | critter (bug net): Glowing Snail NPC 360 (glowing mushroom) | Terraria/Item.cs:21413 |
| 2156 | 黑蝎子 / Black Scorpion | 15 | critter (bug net): Black Scorpion NPC 366 | Terraria/Item.cs:22075 |
| 2674 | 学徒诱饵 / Apprentice Bait | 15 | Angler reward (bait roll); Wooden Crate; crates | Terraria/Item.cs:25895 |
| 3192 | 蛆虫 / Grubby | 15 | critter (bug net): Grubby NPC 485 (jungle/underground) | Terraria/Item.cs:29611 |
| 4845 | 地狱蝴蝶 / Hell Butterfly | 15 | critter (bug net): Hell Butterfly NPC 653. LAVA BAIT (ItemID.Sets.IsLavaBait) | Terraria/Item.cs:39575 |
| 4361 | 瓢虫 / Ladybug | 17 | critter (bug net): Ladybug NPC 604. Consuming it calls NPC.LadyBugKilled -> bad luck (Player.cs:53153-53156) | Terraria/Item.cs:37207 |
| 4418 | 水黾 / Water Strider | 17 | critter (bug net): Water Strider NPC 612 | Terraria/Item.cs:37422 |
| 1992 | 萤火虫 / Firefly | 20 | critter (bug net): Firefly NPC 355 | Terraria/Item.cs:21319 |
| 1997 | 翠凤蝶 / Ulysses Butterfly | 20 | critter (bug net): butterfly style 3 | Terraria/Item.cs:21358 |
| 2436 | 蓝水母 / Blue Jellyfish | 20 | fished (Blue Jellyfish), also usable as bait | Terraria/Item.cs:24121 |
| 2437 | 绿水母 / Green Jellyfish | 20 | fished (Green Jellyfish), also usable as bait | Terraria/Item.cs:24121 |
| 2438 | 粉水母 / Pink Jellyfish | 20 | fished (Pink Jellyfish), also usable as bait | Terraria/Item.cs:24121 |
| 4334 | 黑蜻蜓 / Black Dragonfly | 20 | critter (bug net): dragonfly NPC 595 | Terraria/Item.cs:37098 |
| 4335 | 蓝蜻蜓 / Blue Dragonfly | 20 | critter (bug net): dragonfly NPC 596 | Terraria/Item.cs:37098 |
| 4336 | 绿蜻蜓 / Green Dragonfly | 20 | critter (bug net): dragonfly NPC 597 | Terraria/Item.cs:37098 |
| 4337 | 橙蜻蜓 / Orange Dragonfly | 20 | critter (bug net): dragonfly NPC 598 | Terraria/Item.cs:37098 |
| 4338 | 红蜻蜓 / Red Dragonfly | 20 | critter (bug net): dragonfly NPC 599 | Terraria/Item.cs:37098 |
| 4339 | 黄蜻蜓 / Yellow Dragonfly | 20 | critter (bug net): dragonfly NPC 600 | Terraria/Item.cs:37098 |
| 4363 | 蝇蛆 / Maggot | 22 | critter (bug net): Maggot NPC 606 | Terraria/Item.cs:37222 |
| 2001 | 珠袖蝶 / Julia Butterfly | 25 | critter (bug net): butterfly style 7 | Terraria/Item.cs:21362 |
| 2002 | 蠕虫 / Worm | 25 | critter (bug net): Worm NPC 357; Can of Worms (Worm x5-8, 30% Enchanted Nightcrawler x1-2, 1/20 Gold Worm; Player.cs:9529-9541) | Terraria/Item.cs:21387 |
| 3193 | 鼻涕虫 / Sluggy | 25 | critter (bug net): Sluggy NPC 486 | Terraria/Item.cs:29615 |
| 4847 | 熔岩萤火虫 / Lavafly | 25 | critter (bug net): Lavafly NPC 654. LAVA BAIT | Terraria/Item.cs:39585 |
| 1996 | 红蛱蝶 / Red Admiral Butterfly | 30 | critter (bug net): butterfly style 2 | Terraria/Item.cs:21366 |
| 2675 | 熟手诱饵 / Journeyman Bait | 30 | Angler reward (bait roll); Wooden/Iron/biome crates | Terraria/Item.cs:25887 |
| 1995 | 紫蛱蝶 / Purple Emperor Butterfly | 35 | critter (bug net): butterfly style 1 | Terraria/Item.cs:21370 |
| 2004 | 荧光虫 / Lightning Bug | 35 | critter (bug net): Lightning Bug NPC 358 (hallow) | Terraria/Item.cs:21394 |
| 3191 | 附魔夜行者 / Enchanted Nightcrawler | 35 | critter (bug net): Enchanted Nightcrawler NPC 484; craft Worm + Fallen Star (Recipe.cs:1102) | Terraria/Item.cs:29607 |
| 4849 | 岩浆蜗牛 / Magma Snail | 35 | critter (bug net): Magma Snail NPC 655. LAVA BAIT | Terraria/Item.cs:39595 |
| 3194 | 蚜虫 / Buggy | 40 | critter (bug net): Buggy NPC 487 | Terraria/Item.cs:29619 |
| 1999 | 帛斑蝶 / Tree Nymph Butterfly | 50 | critter (bug net): butterfly style 5 | Terraria/Item.cs:21374 |
| 2676 | 大师诱饵 / Master Bait | 50 | Angler reward (bait roll); Iron/Golden/biome crates; Jungle Mimic 1/3 x2-4 (ItemDropDatabase.cs:996) | Terraria/Item.cs:25891 |
| 2891 | 金蝴蝶 / Gold Butterfly | 50 | gold critter (bug net): Gold Butterfly | Terraria/Item.cs:27417 |
| 2893 | 金蚱蜢 / Gold Grasshopper | 50 | gold critter (bug net): Gold Grasshopper | Terraria/Item.cs:27417 |
| 2895 | 金蠕虫 / Gold Worm | 50 | gold critter (bug net): Gold Worm. Consumption chance forced to 1/20 (Player.cs:53143-53146) | Terraria/Item.cs:27417 |
| 4340 | 金蜻蜓 / Gold Dragonfly | 50 | gold critter (bug net): Gold Dragonfly NPC 601 | Terraria/Item.cs:37106 |
| 4362 | 金瓢虫 / Gold Ladybug | 50 | gold critter (bug net): Gold Ladybug NPC 605. Consuming -> NPC.LadyBugKilled(gold) | Terraria/Item.cs:37215 |
| 4419 | 金水黾 / Gold Water Strider | 50 | gold critter (bug net): Gold Water Strider NPC 613 | Terraria/Item.cs:37430 |
| 2673 | 松露虫 / Truffle Worm | 666 | critter (bug net): Truffle Worm NPC 374/375 (glowing mushroom). NOT a normal bait: GetFishingConditions returns before computing fishing power (Player.cs:42503-42506); in an ocean (X<380 or X>maxTilesX-380) with >1000 liquid tiles and no Duke Fishron alive it 'bites' and reeling summons Duke Fishron (NPC 370); always consumed (Projectile.cs:20742-20753, Player.cs:53041-53052, 53147-53150) | Terraria/Item.cs:25876 |

## 7. 配饰、药水、盔甲与其它加成

| ID | 名称 | 效果 | 引用 | 获取 |
|---|---|---|---|---|
| 2373 | 优质钓鱼线 / High Test Fishing Line | accFishingLine=true: the line never breaks on reel-in (otherwise 1/7 break chance) | Player.cs:14756-14759, 53076 | Angler reward accessory pool |
| 2374 | 渔夫耳环 / Angler Earring | fishingSkill += 10 | Player.cs:14760-14763 | Angler reward accessory pool |
| 2375 | 钓具箱 / Tackle Box | accTackleBox=true: bait-consumption divisor +1 | Player.cs:14768-14771, 53121-53124 | Angler reward accessory pool |
| 3721 | 渔夫渔具袋 / Angler Tackle Bag | accFishingLine + accTackleBox + fishingSkill += 10 | Player.cs:14776-14781 | Craft: High Test Fishing Line + Tackle Box + Angler Earring @ Tinkerer's Workshop (tile 114) (Recipe.cs:14205) |
| 4881 | 防熔岩钓钩 / Lavaproof Fishing Hook | accLavaFishing=true: allows lava catches (CanFishInLava) | Player.cs:14772-14775 | Obsidian/Hellstone Crate primary (19/20 x 1/5) (Player.cs:8796-8803) |
| 5064 | 防熔岩渔具袋 / Lavaproof Tackle Bag | accFishingLine + accTackleBox + fishingSkill += 10 + accLavaFishing | Player.cs:14782-14788 | Craft: Angler Tackle Bag + Lavaproof Fishing Hook @ Tinkerer's Workshop (Recipe.cs:14211) |
| 5139 | 钓鱼浮标 / Fishing Bobber | accFishingBobber -> fishingSkill += 10 (Player.cs:13083-13086); bobber projectile replaced by 986 (Player.cs:36954-36983, 47586-47589) | Player.cs:14764-14767 | Angler reward accessory pool |
| 5140 | 发光钓鱼浮标 / Glowing Fishing Bobber | as Fishing Bobber, projectile 987 with glowMask + light=1 (Projectile.cs:9424-9437) | Player.cs:14764-14767 | Craft: Fishing Bobber + Fallen Star x5 @ Tinkerer's Workshop (Recipe.cs:14216) |
| 5141 | 熔岩苔藓钓鱼浮标 / Lava Moss Fishing Bobber | as Fishing Bobber, glowing projectile 988 | Player.cs:14764-14767 | Craft: Glowing Fishing Bobber + Lava Moss x5 @ Tinkerer's Workshop (Recipe.cs:14234) |
| 5142 | 氪苔藓钓鱼浮标 / Krypton Moss Fishing Bobber | as Fishing Bobber, glowing projectile 989 | Player.cs:14764-14767 | Craft: Glowing Fishing Bobber + Krypton Moss x5 (Recipe.cs:14228) |
| 5143 | 氙苔藓钓鱼浮标 / Xenon Moss Fishing Bobber | as Fishing Bobber, glowing projectile 990 | Player.cs:14764-14767 | Craft: Glowing Fishing Bobber + Xenon Moss x5 (Recipe.cs:14252) |
| 5144 | 氩苔藓钓鱼浮标 / Argon Moss Fishing Bobber | as Fishing Bobber, glowing projectile 991 | Player.cs:14764-14767 | Craft: Glowing Fishing Bobber + Argon Moss x5 (Recipe.cs:14222) |
| 5145 | 氖苔藓钓鱼浮标 / Neon Moss Fishing Bobber | as Fishing Bobber, glowing projectile 992 | Player.cs:14764-14767 | Craft: Glowing Fishing Bobber + Neon Moss x5 (Recipe.cs:14246) |
| 5146 | 氦苔藓钓鱼浮标 / Helium Moss Fishing Bobber | as Fishing Bobber, glowing projectile 993 | Player.cs:14764-14767 | Craft: Glowing Fishing Bobber + Helium Moss x5 (Recipe.cs:14240) |
| 3120 | 渔民袖珍宝典 / Fisherman's Pocket Guide | info accessory: accFishFinder -> shows player.displayedFishingInfo (fishing power / 'Not Enough Water!' / penalty %); also shared from nearby teammates | Player.cs:12856-12859, 12768-12770 | Angler reward accessory pool; component of Fish Finder (3036) -> PDA (3123) -> Cell Phone (3124) -> Shellphones (5358-5361), which all set accFishFinder |
| 4404 | 浮游圈 / Inner Tube | canFloatInWater (Player.cs:13363-13366): +5 fishing power while the player is wet (Player.cs:42516-42519) | Player.cs:13363 | Ocean chest main item, Ocean/Seaside crate primary (9/40) |

| ID | 药水 | Buff | 持续 | 效果 | 引用 | 获取 |
|---|---|---|---|---|---|---|
| 2354 | 钓鱼药水 / Fishing Potion | 121 钓鱼 | 28800 帧 = 8 分钟 | fishingSkill += 15 | Item.cs:23695-23708; Player.cs:10233-10236 | Craft: Bottled Water + Crispy Honey Block + Waterleaf @ Bottle/Alchemy Table (Recipe.cs:1823); Angler reward fallback |
| 2355 | 声呐药水 / Sonar Potion | 122 声呐 | 28800 帧 = 8 分钟 | sonarPotion: when a catch is rolled, pops the item name above the bobber (PopupText Sonar) + sound (Projectile.cs:20869-20875, 20885-20889) | Item.cs:23709-23722; Player.cs:10237-10240 | Craft: Bottled Water + Waterleaf + Coral (Recipe.cs:1858); Angler reward fallback |
| 2356 | 宝匣药水 / Crate Potion | 123 宝匣 | 14400 帧 = 4 分钟 | cratePotion: crate chance 10% -> 25% (Projectile.cs:21642-21646) | Item.cs:23723-23736; Player.cs:10241-10244 | Craft: Bottled Water + Amber + Moonglow + Shiverthorn + Waterleaf (Recipe.cs:1829); Angler reward fallback |

| ID | 盔甲 | 防御 | 渔力 | 获取 |
|---|---|---|---|---|
| 2367 | 渔夫帽 / Angler Hat | 1 | +5 | Angler reward: 10th quest (Player.cs:56995-56997) |
| 2368 | 渔夫背心 / Angler Vest | 2 | +5 | Angler reward: 15th quest (Player.cs:56998-57000) |
| 2369 | 渔夫裤 / Angler Pants | 1 | +5 | Angler reward: 20th quest (Player.cs:57001-57003) |
| 5591 | 船长帽 / Captain Hat | 10 | +5 | Craft: Angler Hat + Mythril/Orichalcum Bar x10 @ Mythril Anvil (Recipe.cs:11774-11777) |
| 5592 | 船长背心 / Captain Vest | 12 | +5 | Craft: Angler Vest + Mythril Bar x10 @ Mythril Anvil (Recipe.cs:11779-11782) |
| 5593 | 船长裤装 / Captain Pants | 11 | +5 | Craft: Angler Pants + Mythril Bar x10 @ Mythril Anvil (Recipe.cs:11784-11787) |

套装奖励（钓鱼套装/船长套装任意混搭，ArmorSetBonuses.cs:445）：`ArmorSetBonus.Angler` “敌人生成速度降低”：spawnRate×1.3、maxSpawns×0.7（ArmorSetBonuses.cs:149-152；NPC.cs:718-722）。

其它加成：
- Gummy Worm (5341), permanent consumable：usedGummyWorm -> fishingSkill += 3 forever（Player.cs:12952-12955, 45794-45797）；获取：Shimmer a Gold Worm critter (NPC 448 -> item 5341, NPCID.cs:4845)
- Tipsy buff (25) e.g. Ale/Sake：+5 fishing power (added in GetFishingConditions)（Player.cs:42512-42515）
- Floating in liquid: canFloatInWater && player.wet (Inner Tube 4404 / Flamingo mount buff 265)：+5 fishing power（Player.cs:42516-42519, 9982-9986, 13363-13366）
- Sitting (chair/bench etc., sitting.TryGetSittingBlock)：+5 fishing power（Player.cs:42520-42523）
- Angler armor pieces 2367/2368/2369 and Captain 5591/5592/5593：+5 fishingSkill per piece; full set (Angler or Captain, mixable) = ArmorSetBonus.Angler: anglerSetSpawnReduction -> spawnRate *1.3, maxSpawns *0.7（Player.cs:13546-13549; ArmorSetBonuses.cs:149-152, 445; NPC.cs:718-722）
- Chum Bucket (4608) -> throws Chum projectile 820：chums in the pond: >0 +11, >1 +6 more, >2 +3 more (max +20). Each chum lives 86400 ticks; each reel-in in that pond adds ai[0]++, at ai[0]>=10 it expires (timeLeft=60)（Projectile.cs:20760-20771, 21722, 54546-54597, 54535-54544, 8021-8031; ChumBucketProjectileHelper.cs）；获取：Drop: Zombie Merman/Wandering Eye Fish 1/2 x4-6; Hemogoblin Shark/Blood Eel/Dreadnautilus 1/2 x7-10 (ItemDropDatabase.cs:166-167)

## 8. 宝匣开箱（Player.OpenFishingCrate，Player.cs:8052-9336）

宝匣开箱仍是硬编码（不在 ItemDropDatabase）。语义：
- **循环段**：反复执行直到有一项“计入掉落”的条目成功；标“仅首掉前”的条目只在本轮之前还没掉过东西时才掷（C# `flag && Next(n)==0`）。
- “困难版”指**宝匣物品是困难模式变体**（ItemID.Sets.IsFishingCrateHardmode），不是世界状态。
- “否则”= 仅在上一条掷骰失败时才掷。
- `HM替换 p→[...]`：困难版宝匣以概率 p 把抽到的物品换成列表中之一（可能改堆叠）。

### 木匣 / Wooden Crate (2334) ｜ 困难版 珍珠木匣 / Pearlwood Crate (3979)

钓取条件：fallback crate (crate roll, no biome crate, tier common/none)。代码：Terraria/Player.cs:8058-8291。

循环段：
- 1/200：附魔日晷(3064) ×1（仅困难版，仅首掉前，计入掉落） `P:8064`
- 1/40：旗鱼靴(3200) ×1（仅首掉前，计入掉落） `P:8069`
- 1/40：海啸瓶(3201) ×1（仅首掉前，计入掉落） `P:8074`
- 1/25：锚(2424) ×1（仅困难版，仅首掉前，计入掉落） `P:8079`
- 1/20：均匀之一：鞋带束头(285)、攀爬爪(953)、梯凳(4341)、植物纤维绳索宝典(3068)、雷达(3084)、淬毒刺(6165) ×1（计入掉落） — NOT gated by nothing-yet: can drop together with the items above `P:8084`
- 1/50：提炼机(997) ×1（仅肉前版，仅首掉前，计入掉落） `P:8111`
- 1/7：金币(73) ×1-5〔1/3: Gold Coin x1-5〕；或 银币(72) ×20-90〔2/3: Silver Coin x20-90〕（计入掉落） — exactly one of the two gives: Next(3)==0 -> gold, else silver `P:8116`
- 1/7：均匀之一：铜矿(12)、锡矿(699)、铁矿(11)、铅矿(700) ×4-15；HM替换 1/2→钴矿(364)、钯金矿(1104)（计入掉落） `P:8133`
- 1/8：均匀之一：铜锭(20)、锡锭(703)、铁锭(22)、铅锭(704) ×2-5；HM替换 1/2→钴锭(381)、钯金锭(1184) ×2-3（否则，计入掉落） — switch has unreachable cases 4-7 (Silver/Tungsten/Gold/Platinum bars) because Next(4); HM override Next(2) makes cases 2-5 unreachable `P:8168`
- 1/7：均匀之一：黑曜石皮药水(288)、敏捷药水(290)、铁皮药水(292)、夜猫子药水(299)、光芒药水(298)、狩猎药水(304)、鱼鳃药水(291)、挖矿药水(2322)、拾心药水(2323)、危险感药水(2329) ×1-3（计入掉落） `P:8228`
循环后：
- 1/3：均匀之一：弱效治疗药水(28)、弱效魔力药水(110) ×5-15 `P:8269`
- 1/3：学徒诱饵(2674)[2/3]、熟手诱饵(2675)[1/3] ×1-4〔Next(3)!=0 -> Apprentice (2/3) else Journeyman (1/3)〕 `P:8284`

### 铁匣 / Iron Crate (2335) ｜ 困难版 秘银匣 / Mythril Crate (3980)

钓取条件：crate roll with uncommon or rare tier。代码：Terraria/Player.cs:8292-8478。

循环段：
- 1/60：附魔日晷(3064) ×1（仅困难版，仅首掉前，计入掉落） `P:8298`
- 1/25：姜须(2501) ×1（仅首掉前，计入掉落） `P:8303`
- 1/20：魅惑里拉琴(2587) ×1（仅首掉前，计入掉落） `P:8308`
- 1/15：猎鹰刃(2608) ×1（仅首掉前，计入掉落） `P:8313`
- 1/20：旗鱼靴(3200) ×1（仅首掉前，计入掉落） `P:8318`
- 1/20：海啸瓶(3201) ×1（仅首掉前，计入掉落） `P:8323`
- 1/4：金币(73) ×5-10（计入掉落） `P:8328`
- 1/6：均匀之一：铜矿(12)、锡矿(699)、铁矿(11)、铅矿(700)、银矿(14)、钨矿(701) ×12-21；HM替换 1/2→钴矿(364)、钯金矿(1104)、秘银矿(365)、山铜矿(1105)（计入掉落） `P:8335`
- 1/4：均匀之一：铜锭(20)、锡锭(703)、铁锭(22)、铅锭(704)、银锭(21)、钨锭(705) ×4-7；HM替换 2/3→钴锭(381)、钯金锭(1184)、秘银锭(382)、山铜锭(1191)（数量再减 0~1）（否则，计入掉落） `P:8382`
- 1/4：均匀之一：黑曜石皮药水(288)、洞穴探险药水(296)、狩猎药水(304)、重力药水(305)、挖矿药水(2322)、拾心药水(2323)、镇静药水(2324)、脚蹼药水(2327) ×2-4（计入掉落） `P:8430`
循环后：
- 1/2：均匀之一：治疗药水(188)、魔力药水(189) ×5-15 `P:8465`
- 1/2：熟手诱饵(2675)[2/3]、大师诱饵(2676)[1/3] ×2-4〔Next(3)!=0 -> Journeyman (2/3) else Master (1/3)〕 `P:8471`

### 金匣 / Golden Crate (2336) ｜ 困难版 钛金匣 / Titanium Crate (3981)

钓取条件：crate roll with veryrare or legendary tier。代码：Terraria/Player.cs:8479-8639。

循环段：
- 1/20：附魔日晷(3064) ×1（仅困难版，仅首掉前，计入掉落） `P:8485`
- 1/8：生命水晶(29) ×1（仅首掉前，计入掉落） `P:8490`
- 1/10：硬鞍(2491) ×1（仅首掉前，计入掉落） `P:8495`
- 1/3：金币(73) ×8-20（计入掉落） `P:8500`
- 1/5：均匀之一：银矿(14)、钨矿(701)、金矿(13)、铂金矿(702) ×25-34；HM替换 1/2→秘银矿(365)、山铜矿(1105)、精金矿(366)、钛金矿(1106)（计入掉落） `P:8507`
- 1/3：均匀之一：银锭(21)、金锭(19)、钨锭(705)、铂金锭(706) ×8-11；HM替换 2/3→秘银锭(382)、精金锭(391)、山铜锭(1191)、钛金锭(1198)（否则，计入掉落） — `if (Next(3)!=0) continue;` inside the else-branch; continue is harmless (last statement of loop) `P:8550`
循环后：
- 1/3：均匀之一：黑曜石皮药水(288)、洞穴探险药水(296)、重力药水(305)、挖矿药水(2322)、拾心药水(2323) ×2-5 `P:8594`
- 1/2：均匀之一：治疗药水(188)、魔力药水(189) ×5-20 `P:8618`
- 2/3：大师诱饵(2676) ×3-7 `P:8624`
- 1/30：附魔剑(989) ×1（仅肉前版） `P:8630`
- 1/15：附魔剑(989) ×1（仅困难版） `P:8634`

### 地牢匣 / Dungeon Crate (3205) ｜ 困难版 围栏匣 / Stockade Crate (3984)

钓取条件：Rare + Player.ZoneDungeon && NPC.downedBoss3。代码：Terraria/Player.cs:8641-9336。

循环段（本匣主奖 → 通用段）：
- 必定：金锁盒(3085) ×1（计入掉落） `P:8682`
- 1/2：书(149) ×5-15 `P:8687`
- 1/2：均匀之一：蓝砖(134)、绿砖(137)、粉砖(139) ×25-50 `P:8691`
- ＋ 通用循环段（见下）
循环后：通用循环后段（见下），然后：
- 1/2：均匀之一：浪里(1438)、扭曲的骨头(1375)、创造向导(1573)、被诅咒的人(1420)、毁灭者(1435)、鸟力驱动(1434)、最后一眼(1421)、虔诚的余孽(5234)、烈火荣耀(1374)、哥布林打扑克牌(1425)、血月升空(1372)、守卫凝视(1441)、上吊的人(1373)、碰撞(1433)、眼睛的永恒(1436)、树妖女(1426)、呐喊者(1424)、骷髅杰克(1419)、斯派基(2995)、恶魔注视(1422)、星空(1439)、超级英雄三剑客(1502)、苏醒的双子魔眼(1423)、独角兽穿越神圣之地(1437)、直面幕后大脑(1500) ×1 `P:9042`

### 海洋匣 / Ocean Crate (5002) ｜ 困难版 海边匣 / Seaside Crate (5003)

钓取条件：Rare + Player.ZoneBeach (or remix: surface-height & below rockLayer)。代码：Terraria/Player.cs:8641-9336。

循环段（本匣主奖 → 通用段）：
- 必定：水上漂靴(863)[4/40]、芦苇呼吸管(186)[9/40]、浮游圈(4404)[9/40]、三叉戟(277)[9/40]、脚蹼(187)[9/40] ×1〔Next(10)==0 -> Water Walking Boots (1/10), else Next(4): Breathing Reed/Inner Tube/Trident/Flipper (9/40 each)〕（计入掉落） `P:8645`
- ＋ 通用循环段（见下）
循环后：通用循环后段（见下），然后：
- 1/5：海与梦(5527) ×1 `P:9020`
- 1/3：贝壳堆(4090) ×20-50 `P:9025`
- 1/10：沙堡桶(4460) ×1 `P:9031`
- 1/10：鲨鱼鱼饵(4425) ×1 `P:9036`

### 腐化匣 / Corrupt Crate (3203) ｜ 困难版 污损匣 / Defiled Crate (3982)

钓取条件：Rare + RolledCorruption。代码：Terraria/Player.cs:8641-9336。

循环段（本匣主奖 → 通用段）：
- 必定：均匀之一：链球(162)、星力手环(111)、火枪(96)、暗影珠(115)、魔刺(64) ×1（计入掉落） `P:8658`
- ＋ 通用循环段（见下）
循环后：通用循环后段（见下），然后：
- [仅困难版] 1/2：暗影之魂(521) ×2-5 `P:9310`
- [仅困难版] 1/2：诅咒焰(522) ×2-5 `P:9320`

### 猩红匣 / Crimson Crate (3204) ｜ 困难版 血匣 / Hematic Crate (3983)

钓取条件：Rare + RolledCrimson。代码：Terraria/Player.cs:8641-9336。

循环段（本匣主奖 → 通用段）：
- 必定：均匀之一：夺命枪(800)、腐叉(802)、猩红魔杖(1256)、恐慌项链(1290)、猩红之心(3062) ×1（计入掉落） `P:8670`
- ＋ 通用循环段（见下）
循环后：通用循环后段（见下），然后：
- [仅困难版] 1/2：暗影之魂(521) ×2-5 `P:9310`
- [仅困难版] 1/2：灵液(1332) ×2-5 `P:9320`

### 神圣匣 / Hallowed Crate (3207) ｜ 困难版 天赐匣 / Divine Crate (3986)

钓取条件：Rare + Player.ZoneHallow。代码：Terraria/Player.cs:8641-9336。 No primary loot: the shared loop repeats until gold coins / ore / bars drop.

循环段（本匣主奖 → 通用段）：
- ＋ 通用循环段（见下）
循环后：通用循环后段（见下），然后：
- [仅困难版] 1/2：光明之魂(520) ×2-5 `P:9310`
- [仅困难版] 1/2：水晶碎块(502) ×4-10 `P:9320`

### 丛林匣 / Jungle Crate (3208) ｜ 困难版 荆棘匣 / Bramble Crate (3987)

钓取条件：Rare + RolledJungle。代码：Terraria/Player.cs:8641-9336。

循环段（本匣主奖 → 通用段）：
- 1/20：花靴(3017) ×1（计入掉落） `P:8716`
- 必定：均匀之一：疾风脚镯(212)、三发猎枪(964)、猛爪手套(211)、再生法杖(213)、玻璃钢钓竿(2292) ×1（否则，计入掉落） `P:8723`
- ＋ 通用循环段（见下）
循环后：通用循环后段（见下），然后：
- 1/3：竹子(4564) ×20-50 `P:9127`
- 1/20：海草(753) ×1 `P:9133`

### 冰冻匣 / Frozen Crate (4405) ｜ 困难版 针叶木匣 / Boreal Crate (4406)

钓取条件：Rare + RolledSnow。代码：Terraria/Player.cs:8641-9336。

循环段（本匣主奖 → 通用段）：
- 必定：均匀之一：冰雪回旋镖(670)、冰雪刃(724)、溜冰鞋(950)、雪球炮(1319)、暴雪瓶(987)、疾风雪靴(1579)、冰川之牙(6153) ×1〔Main.remixWorld: Snowball Cannon(1319) -> Ice Bow(725)〕（计入掉落） `P:8736`
- ＋ 通用循环段（见下）
循环后：通用循环后段（见下），然后：
- 1/20：鱼(669) ×1 `P:9139`

### 绿洲匣 / Oasis Crate (4407) ｜ 困难版 幻象匣 / Mirage Crate (4408)

钓取条件：Rare + Player.ZoneDesert (TrueDesert, unfiltered)。代码：Terraria/Player.cs:8641-9336。

循环段（本匣主奖 → 通用段）：
- 必定：均匀之一：远古凿子(4056)、甲虫钓竿(4442)、沙丘行者靴(4055)、风暴长矛(4061)、霹雳法杖(4062)、巴斯特雕像(4276)、耍蛇者长笛(4262)、魔法海螺(4263) ×1（计入掉落） `P:8748`
- 1/4：甲虫炸弹(4423) ×4-6（计入掉落） `P:8763`
- 1/2：坚固化石(3380) ×10-16（计入掉落） `P:8770`
- 1/35：沙暴瓶(857) ×1（计入掉落） `P:8777`
- ＋ 通用循环段（见下）
循环后：通用循环后段（见下），然后：
- 1/2：均匀之一：神眼(4639)、警惕的蚁狮(4627)、燃烧幽魂(4628)、沙上的生活(4632)、史莱姆之沙(4630)、绷带男孩(4638)、鬼门关(4629)、绿洲(4633)、史前留存(4634)、远古碑牌(4635)、乌鲁鲁巨岩(4636)、参观金字塔(4637)、蛇，我讨厌蛇(4631)、Andrew狮身人面像(4626) ×1 `P:9144`

### 天空匣 / Sky Crate (3206) ｜ 困难版 天蓝匣 / Azure Crate (3985)

钓取条件：Rare + heightLevel 0 (sky)。代码：Terraria/Player.cs:8641-9336。

循环段（本匣主奖 → 通用段）：
- 必定：均匀之一：幸运马掌(158)、星怒(65)、闪亮红气球(159)、天界磁石(2219) ×1（计入掉落） `P:8703`
- ＋ 通用循环段（见下）
循环后：通用循环后段（见下），然后：
- 1/2：均匀之一：高音(5226)、天堂的祝福(5254)、星座(5238)、看清世界的真谛(5258)、爱在垃圾桶里(5255)、太阳之眼(5388) ×1 `P:9196`
- 1/2：云(751) ×50-100 `P:9222`
- 1/40：雏翼(4978) ×1 `P:9228`
- 1/3：天磨(2197) ×1（WorldGen.Skyblock.lowTiles (skyblock-style seed only)） `P:9233`

### 黑曜石匣 / Obsidian Crate (4877) ｜ 困难版 狱石匣 / Hellstone Crate (4878)

钓取条件：in lava with CanFishInLava, crate roll, then 1/6。代码：Terraria/Player.cs:8641-9336。

循环段（本匣主奖 → 通用段）：
- 1/20：熔岩护身符(906) ×1（计入掉落） `P:8787`
- 必定：均匀之一：烈焰靴(4822)、过热的血(4828)、熔岩蛇缸(4880)、防熔岩钓钩(4881)、小火山(4868) ×1（否则，计入掉落） `P:8794`
- 1/4：吊挂盆(4858) ×2（计入掉落） `P:8806`
- 必定：黑曜石锁盒(4879) ×1（计入掉落） — always `P:8814`
- 1/3：湿炸弹(4824) ×7-10（计入掉落） `P:8816`
- 1/2：均匀之一：盆栽岩浆棕榈(4902)、盆栽硫磺灌木(4903)、盆栽火荆棘(4904)、盆栽熔岩球茎(4905)、盆栽余烬卷须(4906) ×1（计入掉落） `P:8823`
- ＋ 通用循环段（见下）
循环后：通用循环后段（见下），然后：
- 1/2：均匀之一：大地之手(1497)、黑暗(1475)、恶魔眼(1479)、岩浆漫流(1542)、暗魂死神(1476)、小鬼脸(1538)、火湖(1501)、受困鬼魂(1478)、凶兆(1539)、闪亮月亮(1540)、骨蛇之头(1499)、血肉之躯(1541) ×1 `P:9241`
- 1/20：地狱熔炉(221) ×1 `P:9285`
- 1/20：恶魔地狱矿车(4443) ×1 `P:9290`
- 1/20：华丽暗影钥匙(4737) ×1 `P:9295`
- 1/20：地狱蛋糕块(4551) ×1 `P:9300`

### 生物群系宝匣通用段（所有生物群系匣与黑曜石匣共用）

while(nothingYet){ <this crate's loop_primary entries, each gated by `nothingYet && Next(maxValue=1)==0` for the first one>; <biome_common.loop> } then <biome_common.after>, then <extras_after>, then <extras_after_hm_crate_only> (only 3982/3983/3986). Because maxValue=1 the primary always fires on pass 1, so the loop runs once for every biome crate except Hallowed/Divine (no primary).

通用循环段：
- 1/4：金币(73) ×5-12（计入掉落） `P:8848`
- 1/7：均匀之一：铜矿(12)、锡矿(699)、铁矿(11)、铅矿(700)、银矿(14)、钨矿(701)、金矿(13)、铂金矿(702) ×20-35；HM替换 1/2→钴矿(364)、钯金矿(1104)、秘银矿(365)、山铜矿(1105)、精金矿(366)、钛金矿(1106)（计入掉落） — Obsidian/Hellstone crate: picked ore is replaced by Hellstone (174) before the HM override (8885-8888) `P:8855`
- 1/4：均匀之一：铁锭(22)、银锭(21)、金锭(19)、铅锭(704)、钨锭(705)、铂金锭(706) ×6-16；HM替换 2/3→钴锭(381)、秘银锭(382)、精金锭(391)、钯金锭(1184)、山铜锭(1191)、钛金锭(1198)（数量再减 0~1）（计入掉落） — independent of the ore roll; Obsidian/Hellstone crate: bar replaced by Hellstone Bar (175) before HM override (8945-8948) `P:8918`
通用循环后段：
- 1/4：均匀之一：黑曜石皮药水(288)、洞穴探险药水(296)、狩猎药水(304)、重力药水(305)、挖矿药水(2322)、拾心药水(2323) ×2-4 `P:8978`
- 1/2：均匀之一：治疗药水(188)、魔力药水(189) ×5-17 `P:9006`
- 1/2：均匀之一：熟手诱饵(2675)、大师诱饵(2676) ×2-6〔Next(2)!=0 -> Journeyman else Master〕 `P:9012`

## 9. 渔夫 NPC

- NPC：渔夫 / Angler (369)；沉睡形态 沉睡渔夫 / Sleeping Angler (376)。
- Sleeping Angler (376) spawns via NPC.SpawnNPC when !NPC.savedAngler and none exists: on surface ocean beach sand (spawnTileX<beachDistance or >maxTilesX-beachDistance, Main.tileSand, Y<worldSurface) (NPC.cs:1928-1931), or above ocean water (NPC.cs:1798-1820); special no-surface seeds 1/20 in any water (NPC.cs:1778-1796).
- Talking to him (player.talkNPC) transforms 376 -> 369 (NPC.cs:19903-19906) and sets NPC.savedAngler=true (NPC.cs:53506-53508).
- Afterwards he moves in like a normal town NPC when savedAngler && no Angler exists (Main.cs:66855-66858, 67037-67040).
- 任务列表：Main.anglerQuestItemNetIDs (41 fish), Main.cs:1863-1870。
- 每日刷新：Main.AnglerQuestSwap() at every dawn (Main.UpdateTime_StartDay, Main.cs:66400) and at world generation end (WorldGen.cs:22687): clears anglerWhoFinishedToday, anglerQuestFinished=false, rerolls anglerQuest uniformly until no reroll condition applies (Main.cs:3936-4084). Not in multiplayer clients.
- 交付：Chat button 'Quest' (Lang.inter[64]) -> Main.NPCChatText_DoAnglerQuest (Main.cs:40969-41004): if !anglerQuestFinished and player not in anglerWhoFinishedToday and the player has the fish: remove 1, anglerQuestsFinished++, GetAnglerReward(...), anglerQuestFinished=true, add player name to anglerWhoFinishedToday. Each player can finish once per day in MP (server tracks names; MessageBuffer.cs:3157-3166).
- 可钓条件：A quest fish can only be caught if it is today's quest, the Angler exists, today's quest is not finished, and the player does not already carry one (Projectile.cs:21618-21633). Quest fish are rare=-11 questItem (Item.cs:27652-27658) and never consume bait.
- 商店：无。No Shop(369, ...) is registered in NPCInteractions.cs:500-525; the Angler has only the Quest button (NPCInteractions.cs:133-150).
- 对话：NPC.GetChat type 369 (NPC.cs:95904-95907): 1/5 (if anglerQuestsFinished>1) AnglerQuestChatter.*; else special event text; blood moon: 1/3 AnglerSpecialText.BloodMoonFishing, else LegacyDialog 350-352 (quest done) / 348-349; else quest not done: LegacyDialog 334-337; done: AnglerChatter.* (or LegacyDialog 347 1/5 if Dryad present). Sleeping Angler: LegacyDialog 353-355. Quest text: AnglerQuestText.Quest_<ItemInternalName>, TurnIn_*, NoQuest_* (Lang.cs:430-442).

### 9.1 任务鱼（41 条）

| ID | 名称 | 钓取条件（规则#：条件） | 刷任务时重掷条件 | 物品提示 |
|---|---|---|---|---|
| 2450 | 蝙蝠鱼 / Batfish | #146: 高度>地表(地下/洞穴/地狱)<br>颠倒世界另有 #59（任意地点） | — | “抓捕位置：地下和洞穴” / 'Caught in Underground & Caverns' |
| 2451 | 大黄蜂金枪鱼 / Bumblebee Tuna | #10: 鱼池含蜂蜜 | — | “抓捕位置：蜂蜜” / 'Caught in Honey' |
| 2452 | 猫鱼 / Catfish | #104: 丛林(判定后)、高度=地表 | !Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4021） | “抓捕位置：丛林地表” / 'Caught in Jungle Surface' |
| 2453 | 云鱼 / Cloudfish | #129: 高度=太空 | !(NPC.downedBoss1 || downedBoss2 || downedBoss3 || hardMode || downedSlimeKing || downedQueenBee)（Main.cs:4003）；!Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4025） | “抓捕位置：天湖” / 'Caught in Sky Lakes' |
| 2454 | 诅咒鱼 / Cursedfish | #67: 腐化(判定后) | (!hardMode || WorldGen.crimson)（Main.cs:3951） | “抓捕位置：腐化之地” / 'Caught in Corruption' |
| 2455 | 土鱼 / Dirtfish | #135: 高度=地表或地下 | — | “抓捕位置：地表和地下” / 'Caught in Surface & Underground' |
| 2456 | 雷管鱼 / Dynamite Fish | #137: 高度=地表<br>颠倒世界另有 #56（任意地点） | !Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4037） | “抓捕位置：地表” / 'Caught in Surface' |
| 2457 | 浮游噬鱼 / Eater of Plankton | #69: 腐化(判定后) | WorldGen.crimson（Main.cs:3955） | “抓捕位置：腐化之地” / 'Caught in Corruption' |
| 2458 | 坠落星鱼 / Fallen Starfish | #132: 高度<地下(太空/地表)<br>颠倒世界另有 #53（任意地点） | !Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4041） | “抓捕位置：天湖和地表” / 'Caught in Sky Lakes & Surface' |
| 2459 | 克苏鲁鱼 / The Fish of Cthulhu | #133: 高度<地下(太空/地表)<br>颠倒世界另有 #54（任意地点） | !Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4045） | “抓捕位置：天湖和地表” / 'Caught in Sky Lakes & Surface' |
| 2460 | 骷髅王鱼 / Fishotron | #152: 高度>地下(洞穴/地狱) | — | “抓捕位置：洞穴” / 'Caught in Caverns' |
| 2461 | 鸟妖鱼 / Harpyfish | #128: 高度<地下(太空/地表)<br>颠倒世界另有 #52（任意地点） | !Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4049） | “抓捕位置：天湖和地表” / 'Caught in Sky Lakes & Surface' |
| 2462 | 饿鬼鱼 / Hungerfish | #149: 高度>地下(洞穴/地狱) | !hardMode（Main.cs:3959） | “抓捕位置：洞穴” / 'Caught in Caverns' |
| 2463 | 灵液鱼 / Ichorfish | #74: 猩红(判定后) | (!hardMode || !WorldGen.crimson)（Main.cs:3963） | “抓捕位置：猩红之地” / 'Caught in Crimson' |
| 2464 | 珠宝鱼 / Jewelfish | #147: 高度>地表(地下/洞穴/地狱)<br>颠倒世界另有 #60（任意地点） | — | “抓捕位置：地下和洞穴” / 'Caught in Underground & Caverns' |
| 2465 | 幻象鱼 / Mirage Fish | #87: ZoneHallow 神圣、高度>地表(地下/洞穴/地狱) | !hardMode（Main.cs:3967） | “抓捕位置：地下神圣之地” / 'Caught in Underground Hallow' |
| 2466 | 突变雪怪鱼 / Mutant Flinxfin | #96: 雪原(判定后)、高度>地表(地下/洞穴/地狱) | — | “抓捕位置：地下苔原” / 'Caught in Underground Tundra' |
| 2467 | 企鹅鱼 / Pengfish | #93: 雪原(判定后)、高度<地下(太空/地表) | !Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4053） | “抓捕位置：地表苔原” / 'Caught in Surface Tundra' |
| 2468 | 妖精鱼 / Pixiefish | #88: ZoneHallow 神圣、高度<地下(太空/地表) | !hardMode（Main.cs:3971）；!Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4057） | “抓捕位置：地表神圣之地” / 'Caught in Surface Hallow' |
| 2469 | 蜘蛛鱼 / Spiderfish | #148: 高度>地表(地下/洞穴/地狱)<br>颠倒世界另有 #61（任意地点） | — | “抓捕位置：地下和洞穴” / 'Caught in Underground & Caverns' |
| 2470 | 苔原鳟鱼 / Tundra Trout | #94: 雪原(判定后)、高度=地表 | !Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4069） | “抓捕位置：地表苔原” / 'Caught in Surface Tundra' |
| 2471 | 独角兽鱼 / Unicorn Fish | #90: ZoneHallow 神圣 | !hardMode（Main.cs:3975） | “抓捕位置：神圣之地” / 'Caught in Hallow' |
| 2472 | 向导巫毒鱼 / Guide Voodoo Fish | #151: 高度>地下(洞穴/地狱) | — | “抓捕位置：洞穴” / 'Caught in Caverns' |
| 2473 | 飞龙尾 / Wyverntail | #130: 高度=太空 | !hardMode（Main.cs:3979）；!(NPC.downedBoss1 || downedBoss2 || downedBoss3 || hardMode || downedSlimeKing || downedQueenBee)（Main.cs:4003）；!Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4073） | “抓捕位置：天湖” / 'Caught in Sky Lakes' |
| 2474 | 僵尸鱼 / Zombie Fish | #138: 高度=地表<br>颠倒世界另有 #57（任意地点） | !Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4077） | “抓捕位置：地表” / 'Caught in Surface' |
| 2475 | 毒菌鱼 / Amanita Fungifin | #92: ZoneGlowshroom 发光蘑菇 | — | “抓捕位置：发光蘑菇地” / 'Caught in Glowing Mushroom Fields' |
| 2476 | 天使鱼 / Angelfish | #131: 高度=太空 | !(NPC.downedBoss1 || downedBoss2 || downedBoss3 || hardMode || downedSlimeKing || downedQueenBee)（Main.cs:4003）；!Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4009） | “抓捕位置：天湖” / 'Caught in Sky Lakes' |
| 2477 | 血腥战神 / Bloody Manowar | #73: 猩红(判定后) | !WorldGen.crimson（Main.cs:3983） | “抓捕位置：猩红之地” / 'Caught in Crimson' |
| 2478 | 骷髅鱼 / Bonefish | #145: 高度>地表(地下/洞穴/地狱)<br>颠倒世界另有 #58（任意地点） | — | “抓捕位置：地下和洞穴” / 'Caught in Underground & Caverns' |
| 2479 | 兔兔鱼 / Bunnyfish | #136: 高度=地表<br>颠倒世界另有 #55（任意地点） | !Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4013） | “抓捕位置：地表” / 'Caught in Surface' |
| 2480 | 金枪鱼须船长 / Cap'n Tunabeard | #115: 海洋鱼池(高度≤地表、浮标距世界左右边缘<380格、水>1000格) | !hardMode（Main.cs:3987）；!Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4017） | “抓捕位置：海洋” / 'Caught in Ocean' |
| 2481 | 小丑鱼 / Clownfish | #116: 海洋鱼池(高度≤地表、浮标距世界左右边缘<380格、水>1000格) | !Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4029） | “抓捕位置：海洋” / 'Caught in Ocean' |
| 2482 | 恶魔地狱鱼 / Demonic Hellfish | #150: 高度>地下(洞穴/地狱) | — | “抓捕位置：洞穴” / 'Caught in Caverns' |
| 2483 | 跳跳鱼 / Derpfish | #105: 丛林(判定后)、高度=地表 | !hardMode（Main.cs:3991）；!Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4033） | “抓捕位置：丛林地表” / 'Caught in Jungle Surface' |
| 2484 | 猪龙鱼 / Fishron | #95: 雪原(判定后)、高度>地表(地下/洞穴/地狱) | !hardMode（Main.cs:3995） | “抓捕位置：地下苔原” / 'Caught in Underground Tundra' |
| 2485 | 染病鞘鱼 / Infected Scabbardfish | #68: 腐化(判定后) | WorldGen.crimson（Main.cs:3999） | “抓捕位置：腐化之地” / 'Caught in Corruption' |
| 2486 | 泥鱼 / Mudfish | #107: 丛林(判定后)、高度>=地表(非太空) | — | “抓捕位置：丛林” / 'Caught in Jungle' |
| 2487 | 史莱姆鱼 / Slimefish | #155: 任意地点 | !Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4061） | “抓捕位置：地表森林” / 'Caught in Surface Forest' |
| 2488 | 热带梭鱼 / Tropical Barracuda | #106: 丛林(判定后)、高度=地表 | !Main.isThereAWorldSurface (special seed with no surface)（Main.cs:4065） | “抓捕位置：丛林地表” / 'Caught in Jungle Surface' |
| 4393 | 甲虫鱼 / Scarab Fish | #78: 神圣沙漠(50%判定)<br>#123: 沙漠(判定后) | — | “抓捕位置：沙漠” / 'Caught in Desert' |
| 4394 | 蝎子鱼 / Scorpio Fish | #79: 神圣沙漠(50%判定)<br>#124: 沙漠(判定后) | — | “抓捕位置：沙漠” / 'Caught in Desert' |

全部任务鱼都需要“罕见(uncommon)”标志成立，且规则在表中的位置决定优先级（例：在腐化区钓腐化任务鱼时，前面的腐化稀有鱼规则会先抢）。

### 9.2 任务奖励（Player.GetAnglerReward）

- 入口：Player.GetAnglerReward(angler, questItemType) Terraria/Player.cs:56967-56977; called from Main.NPCChatText_DoAnglerQuest after anglerQuestsFinished++ (Main.cs:40986-40987), so questsDone q >= 1 on the first turn-in。调用顺序：主奖励 → 装饰 → 金钱 → 鱼饵。
- 稀有度系数 r：`r0(q) = q<=50 ? 1-0.01q : q<=100 ? 0.5-0.005(q-50) : q>150 ? 0.15 : 0.25-0.002(q-100);  r = r0*0.9*(PriceAdjustment+1)/2`；town-NPC happiness shop multiplier, clamped [0.75,1.5] (ShopHelper.cs:182) -> factor [0.875,1.25]（Player.cs:56972, 56979-56984）。smaller r = rarer items become MORE likely (denominators are int(N*r)). UnifiedRandom.Next(0) returns 0 (UnifiedRandom.cs:135-142), so int(N*r)==0 means a guaranteed hit.
- 主奖励（按顺序，命中即停；q = 已完成任务数，含本次）：
  - `q == 5` → 给予：绒毛胡萝卜 / Fuzzy Carrot (2428)（Player.cs:56992-56994）
  - `q == 10` → 给予：渔夫帽 / Angler Hat (2367)（Player.cs:56995-56997）
  - `q == 15` → 给予：渔夫背心 / Angler Vest (2368)（Player.cs:56998-57000）
  - `q == 20` → 给予：渔夫裤 / Angler Pants (2369)（Player.cs:57001-57003）
  - `q == 25` → 给予：无底水桶 / Bottomless Water Bucket (3031)（Player.cs:57004-57006）
  - `q == 30` → 给予：金钓竿 / Golden Fishing Rod (2294)（Player.cs:57007-57009）
  - `quest fish was Bumblebee Tuna (2451) && Main.hardMode` → 均匀之一：蜂蜜吸收绵 / Honey Absorbant Sponge (5303)、无底蜂蜜桶 / Bottomless Honey Bucket (5302)（Player.cs:57012-57016）
  - `quest fish was Bumblebee Tuna && !hardMode && Next(2)==0` → 均匀之一：蜂蜜吸收绵 / Honey Absorbant Sponge (5303)、无底蜂蜜桶 / Bottomless Honey Bucket (5302)（Player.cs:57017-57021）
  - `q > 75 && Next(int(250r))==0` → 给予：金钓竿 / Golden Fishing Rod (2294)（Player.cs:57035-57039）
  - `hardMode && q > 25 && Next(int(100r))==0` → 给予：熔线钓钩 / Hotline Fishing Hook (2422)（Player.cs:57040-57044）
  - `hardMode && q > 10 && Next(int(70r))==0` → 给予：鳍翼 / Fin Wings (2494)（Player.cs:57045-57049）
  - `q > 10 && Next(int(70r))==0` → 给予：无底水桶 / Bottomless Water Bucket (3031)（Player.cs:57050-57054）
  - `q > 10 && Next(int(70r))==0` → 给予：超级吸收绵 / Super Absorbant Sponge (3032)（Player.cs:57055-57059）
  - `Next(int(80r))==0` → 给予：金虫网 / Golden Bug Net (3183)（Player.cs:57060-57064）
  - `Next(int(60r))==0` → 给予：鱼钩 / Fish Hook (2360)（Player.cs:57065-57069）
  - `Next(int(60r))==0` → 给予：鲤鱼矿车 / Minecarp (4067)（Player.cs:57070-57074）
  - `Next(int(80r))==0` → 给予：贝壳发夹 / Seashell Hairpin (2417)、美人鱼饰品 / Mermaid Adornment (2418)、美人鱼鱼尾裤 / Mermaid Tail (2419)（Player.cs:57075-57079, 57115-57123） hairpin + adornment + tail (3 items)
  - `Next(int(80r))==0` → 给予：鱼装面具 / Fish Costume Mask (2498)、鱼装衣 / Fish Costume Shirt (2499)、鱼装鳍裙 / Fish Costume Finskirt (2500)（Player.cs:57080-57084, 57124-57132） fish costume 3 pieces
  - `accessory pool (see below)` → 配饰池：优质钓鱼线 / High Test Fishing Line (2373)、渔夫耳环 / Angler Earring (2374)、钓具箱 / Tackle Box (2375)、渔民袖珍宝典 / Fisherman's Pocket Guide (3120)、天气收音机 / Weather Radio (3037)、六分仪 / Sextant (3096)、钓鱼浮标 / Fishing Bobber (5139)（Player.cs:57022-57034, 57085-57095, 57259-57357）
  - `otherwise` → 均匀之一：钓鱼药水 / Fishing Potion (2354)、声呐药水 / Sonar Potion (2355)、宝匣药水 / Crate Potion (2356) ×2-5（Player.cs:57096-57110）
- 配饰池：概率 `p = 0.8*(1 - (1-1/int(40r))^3 * (1-1/int(30r))^3 * (1-1/int(25r)))`。Remove from the pool every item the player owns (inventory, armor+accessory+vanity slots, Piggy Bank, Safe, Defender's Forge, Void Vault, all loadouts). Owning Angler Tackle Bag(3721)/Lavaproof Tackle Bag(5064) removes 2373,2375,2374; owning Fish Finder(3036)/PDA(3123)/Cell Phone(3124)/Shellphones(5358-5361) removes 3120,3037,3096; owning any glowing bobber (5140-5146) removes 5139. If the pool is non-empty: roll p once -> give a random missing item, else go to potions. If the pool is empty (player owns all): roll p -> give a random item from the FULL list (duplicates possible), else potions.（Player.cs:57085-57095, 57259-57357）
- 装饰：概率 `Lerp(1-r, 1, min(1, q/100))`，Next(19) 均匀：救生圈(2442)、舵轮(2443)、罗盘针(2444)、墙锚(2445)、海贼像素画(2497)、宝藏地图(2495)、金鱼纪念章(2446)、兔兔鱼纪念章(2447)、剑鱼纪念章(2448)、鲨牙纪念章(2449)、船舶瓶(2490)、海草花盆(2496)、并未如此迷失在乐园(5235)、甲壳摄影(5252)、獠牙(5256)、下面潜伏着什么(5259)、沙发笑话(5263)、沉默的鱼(5264)、公爵(5265)（Player.cs:57135-57168）。
- 金钱（必给）：`n=(q+50)/2 (int); n=int(n*Next(50,201)*0.015); n=int(n*1.5); if hardMode n*=2; if expertMode n*=2; if n>100: Gold Coin x clamp(n/100,1,10) else Silver Coin x clamp(n,1,99)`（Player.cs:57215-57257）。
- 鱼饵：概率 `Next(int(100r)) <= 50`；种类 `Next(int(15r))==0 -> Master Bait; else Next(int(5r))==0 -> Journeyman Bait; else Apprentice Bait`；数量 `1 + [Next(25)<=q] + [Next(50)<=q] + [Next(100)<=q] + [Next(150)<=q] + [Next(200)<=q] + [Next(250)<=q]`（Player.cs:57170-57213）。

### 9.3 对话键

- `AnglerQuestText.*`（51 条，Town.json）：`Quest_<物品内部名>`（每条任务鱼一条，内含“(Caught in …)”提示）、`TurnIn_1..`、`NoQuest_1..`。
- `AnglerQuestChatter.*`（2）、`AnglerChatter.*`（10）、`AnglerSpecialText.*`（11：Party、Graveyard1、Graveyard2、Windy1、Windy2、Storm1、Storm2、Rain1、Rain2、BloodMoonFishing1、BloodMoonFishing2）。
- `LegacyDialog.334-355`（Legacy.json）：334-337 有任务时闲聊，347 树妖相关，348-352 血月，353-355 沉睡渔夫。全文（中英）都在 `fishing.json` 的 `angler.dialogue`。
- 按钮文本 `Lang.inter[64]`（“任务”）；UI 字符串：`GameUI.FishingPower` = {0} Fishing Power / {0}渔力；`GameUI.FullFishingPower` = {0} ({1}%) Fishing Power / {0}（{1}%）渔力；`GameUI.NotEnoughWater` = Not Enough Water! / 水量不够！；`GameUI.FishingWarning` = Warning! / 警告！ (Truffle Worm equipped)；`GameUI.PrecentFishingPower` = {0}% fishing power / {0}%渔力 (pole tooltip)。

## 10. 其它与不确定点

- 牡蛎：Right-click Oyster: 1/4 -> (1/10 Pink Pearl 4414, else 1/3 Black Pearl 4413, else White Pearl 4412); always Shucked Oyster 4411 (Player.cs:9543-9561)。
- 血月钓鱼敌怪掉落：ItemDropDatabase.cs:164-184 (Chum Bucket, Chum Caster 1/8, Vampire Frog Staff 1/8, Blood Rain Bow 1/8, Money Trough 1/15, Bloody Tear etc.)。
- 较新的物品（旧版 wiki 可能没有，以本源码为准）：湖中女士(5274)、藤壶法杖(5463)、邋遢怪来啦(5634)、巴西天空(5490)、施法桌(2999)、船长帽(5591)、船长背心(5592)、船长裤装(5593)、淬毒刺(6165)、冰川之牙(6153)、高音(5226)、天堂的祝福(5254)、星座(5238)、看清世界的真谛(5258)、爱在垃圾桶里(5255)、太阳之眼(5388)、并未如此迷失在乐园(5235)、甲壳摄影(5252)、獠牙(5256)、下面潜伏着什么(5259)、沙发笑话(5263)、沉默的鱼(5264)、公爵(5265)、无底蜂蜜桶(5302)、蜂蜜吸收绵(5303)、海与梦(5527)。中文名全部取自 zh-Hans 本地化。
- 不确定：`localAI[1] == -1` 时强制消耗鱼饵（Player.cs:53129-53132）在 1.4.5 中没有找到会产生 -1 的路径（敌怪用的是 -NPC类型），疑为遗留。
- 不确定：神圣沙漠的巴西天空在 HallowedDrops 里是传说 必定（行 147），普通沙漠是 1/3（行 87）——源码如此，不是笔误推断。
- 地下兜底规则 #154 `UncommonOrCommon 1/4 [装甲洞穴鱼(2303), 镜面鱼(2309)×3]`（行 67）与旧代码语义不同（旧代码：罕见或普通时必中，否则 1/4），以新表为准。
- 高度层与海洋判定使用浮标坐标，群系使用玩家坐标；移植到 HK 时如果没有“世界边缘”，需要自行把某些场景标记为海洋。
- 种子专属逻辑（颠倒世界 remix、蜜蜂世界 notTheBees、空岛 Skyblock、无地表）已标注，移植可忽略。

## 11. 对拍用测试向量（fishing_reference.py 精确计算，单次 FishingCheck 的结果分布）

单次 FishingCheck = 一次咬钩计时走满 660。`(nothing)` 含门槛失败和停止规则。未列出的上下文字段取 `DEFAULT_CTX`（白天、无血月、无宝匣药水、无任务鱼）。

**地表森林池塘，FL=30，肉前，水=1000** `{'FL': 30}`

| 概率 | 结果 |
|---|---|
| 47.0000% | (nothing) |
| 46.6302% | 鲈鱼 / Bass (2290) |
| 4.5415% | 木匣 / Wooden Crate (2334) |
| 0.9350% | 炸弹鱼 / Bomb Fish (3196) |
| 0.6530% | 铁匣 / Iron Crate (2335) |
| 0.1055% | 金匣 / Golden Crate (2336) |
| 0.0636% | 蛙腿 / Frog Leg (2423) |
| 0.0509% | 气球河豚鱼 / Balloon Pufferfish (3225) |
| 0.0204% | 和风鱼 / Zephyr Fish (2420) |

**洞穴池塘，FL=100，困难模式** `{'FL': 100, 'heightLevel': 3, 'hardMode': True}`

| 概率 | 结果 |
|---|---|
| 43.3494% | 鲈鱼 / Bass (2290) |
| 15.4881% | 装甲洞穴鱼 / Armored Cavefish (2303) |
| 12.0000% | (nothing) |
| 5.6866% | 臭味鱼 / Stinkfish (2321) |
| 5.1366% | 镜面鱼 / Specular Fish (2309) |
| 4.9280% | 珍珠木匣 / Pearlwood Crate (3979) |
| 4.9280% | 炸弹鱼 / Bomb Fish (3196) |
| 3.2853% | 秘银匣 / Mythril Crate (3980) |
| 1.7323% | 岩鱼锤 / Rockfish (2320) |
| 0.7353% | 绿水母 / Green Jellyfish (2437) |
| 0.7353% | 蓝水母 / Blue Jellyfish (2436) |
| 0.6623% | 金鲤鱼 / Golden Carp (2308) |
| 0.5867% | 钛金匣 / Titanium Crate (3981) |
| 0.3520% | 蛙腿 / Frog Leg (2423) |
| 0.2816% | 气球河豚鱼 / Balloon Pufferfish (3225) |
| 0.1126% | 和风鱼 / Zephyr Fish (2420) |

**海洋（ZoneBeach + 海洋池），FL=60，水=2000** `{'FL': 60, 'oceanPond': True, 'zones': ['beach'], 'waterTiles': 2000}`

| 概率 | 结果 |
|---|---|
| 32.0000% | (nothing) |
| 23.0336% | 鳟鱼 / Trout (2297) |
| 11.5168% | 红鲷鱼 / Red Snapper (2301) |
| 11.5168% | 金枪鱼 / Tuna (2300) |
| 9.2312% | 虾 / Shrimp (2316) |
| 4.9152% | 木匣 / Wooden Crate (2334) |
| 2.3501% | 炸弹鱼 / Bomb Fish (3196) |
| 1.2288% | 铁匣 / Iron Crate (2335) |
| 0.8919% | 剑鱼 / Swordfish (2332) |
| 0.8224% | 掠夺鲨 / Reaver Shark (2341) |
| 0.8224% | 锯齿鲨 / Sawtooth Shark (2342) |
| 0.6689% | 粉水母 / Pink Jellyfish (2438) |
| 0.4000% | 海洋匣 / Ocean Crate (5002) |
| 0.2560% | 金匣 / Golden Crate (2336) |
| 0.1632% | 蛙腿 / Frog Leg (2423) |
| 0.1306% | 气球河豚鱼 / Balloon Pufferfish (3225) |
| 0.0522% | 和风鱼 / Zephyr Fish (2420) |

**地狱熔岩 + 熔线钓钩，FL=80，困难模式** `{'FL': 80, 'inLava': True, 'canFishInLava': True, 'heightLevel': 4, 'hardMode': True}`

| 概率 | 结果 |
|---|---|
| 89.8651% | (nothing) |
| 5.6554% | 黑曜石鱼 / Obsidifish (2315) |
| 2.7230% | 闪鳍锦鲤 / Flarefin Koi (2312) |
| 1.3000% | 狱石匣 / Hellstone Crate (4878) |
| 0.1141% | 恶魔海螺 / Demon Conch (4819) |
| 0.1141% | 无底熔岩桶 / Bottomless Lava Bucket (4820) |
| 0.1141% | 熔岩吸收绵 / Lava Absorbant Sponge (4872) |
| 0.1141% | 黑曜石剑鱼 / Obsidian Swordfish (2331) |
