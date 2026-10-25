# 锤子、墙、斜坡/半砖与“单向墙”——Terraria 1.4.5 源码对照与 TerraKnight 实现方案

源码：`terraria-src\Terraria\*.cs`（下文 `Player.cs:46357` 即该文件行号）；mod：`src\TerraKnight\...`（只读）。
本目录附带：`sim.py` / `sim2.py`（Collision.TileCollision / SlopeCollision / WalkDownSlope / StepDown / StepUp 与玩家每 tick 顺序的逐行 Python 移植，用来数值验证第 3、4 节结论），`walls_all.txt` / `walls_all.json`（全部 239 个放墙物品：ItemID→WallID、配方、拆墙掉落），`wall_drops.txt`（每个 WallID 的掉落），`walltable.md`（第 2 节表格的来源）。

## 0. 结论速览

- 锤子塑形**不计血量**：每次敲击 `hitTile.AddDamage(id, 100)`，一下就变形（`Player.cs:46376-46385`）；只有祭坛/暗影珠（`Main.tileHammer`：26、31、695、696，`Main.cs:7385-7388`）和墙才累计伤害。塑形**每按一次只变一次**（`poundRelease`，`Player.cs:46022`、`46698`），按住不放后续挥动去敲身后的墙。
- 实心块循环（默认）：整块 → 半砖 → 1 → 2 → 3 → 4 → 整块；右侧贴墙则 1/2、3/4 互换；“上实下空”则先下斜坡（3/4）再上斜坡（1/2）。平台：平 → A → B → 半砖 → 平（A/B 默认 1/2，见 1.3）。
- 平台斜坡只从上面、且不在上升时接住脚；平台**没有侧面碰撞**（`Collision.cs:2406/2426/2446` 三个分支都要求 `!tileSolidTop`）。从楼梯顶走上平排，靠 SlopeCollision 的“越过高端就放到顶上”分支（`Collision.cs:1968-1992`）在**同一 tick 移动之后**把脚放到平台顶，下一 tick 平台的“旧脚底 ≤ 顶”判定（`Collision.cs:2388`）成立，站住。
- “单向墙”：一列同向斜坡平台，**顺着下坡方向**走过去完全无阻（最低一块被“恰在底线且向下坡走”排除 `Collision.cs:1861`，其余块不在脚的高度范围 `Collision.cs:1857`）；**逆着上坡方向**走，脚被斜坡抬起；若头顶空间不够抬（3 格高通道：48−42=6 px），`Collision.cs:2003-2019` 把被挡住的抬升量换成**水平反推**并清零速度——过不去。开阔处只会被顶高 16–32 px 然后翻过去，不是墙。按住↓两个方向都能穿过。数值模拟 16 个起始相位 × 6 种速度全部一致（4.3 节）。
- mod 的“楼梯走到平台掉下去”：发生在**沿楼梯往上走到顶端那排平台**时。`StairCollision` 在物理步**之前**按当前位置算坡面（`StairCollision.cs:88-96`），而 Terraria 在移动**之后**算；于是越过楼梯高端的那一物理步里，主角的盒子在脚低于平台顶 `e`（0～约 3.6 px）的情况下从**侧面**进入平排平台那一格的整格高 one-way 盒子（`ChunkColliders.cs:121-135`，`PlatformEffector2D` `surfaceArc=160`，`ChunkColliders.cs:181-185`），这个接触被 one-way 判为“从侧面来”而禁用；随后托板（ledge）在主角右缘离开楼梯格时撤掉（`StairCollision.cs:82`、`100-105`、`139-144`），主角一直贴着平台没有分离过，接触仍是禁用状态，于是穿下去。修法：用**这一物理步结束时**的上坡角位置算坡面（与 Terraria 的顺序一致），见 5.4。

---

## 1. 锤子

### 1.1 调用链（`Player.ItemCheck_UseMiningTools`，`Player.cs:45969-46029`）

1. `toolTime == 0 && itemAnimation > 0 && controlUseItem` 时，若目标格无物块或不是锤子目标（`IsAHammerTarget`，`Player.cs:45921-45928`），`poundRelease = false`（`45993-46000`）。
2. 在射程内（`IsTargetTileInItemRange` = `tileBoost`，`Player.cs:46701-46704`）才继续；`ItemCheck_UseMiningTools_ActuallyUseMiningTool`（`46031-46167`）。
3. `if (releaseUseItem) poundRelease = true;`（`46020-46023`）——松开按键才允许下一次塑形。
4. `canHitWalls` 仍为真时找墙并敲墙（`46024-46028`，见 1.6）。

`ActuallyUseMiningTool`：
- `num = hitTile.HitObject(x, y, 1)`（`46045`）。
- `Main.tileHammer[type]`（祭坛/暗影珠/复制品）：`num2 += sItem.hammer`，累计到 100 才 `KillTile`；祭坛（26）在 `hammer < 80 || !Main.hardMode` 时伤害归 0 并扣玩家一半血（`46050-46087`）。这是唯一“锤子计血量”的物块。
- 镐（`sItem.pick > 0`）走 `PickTile`（`46158-46162`）。
- 最后总是调用 `ItemCheck_UseMiningTools_TryPoundingTile(sItem, num, ref canHitWalls, x, y)`（`46167`）。

### 1.2 `TryPoundingTile`（`Player.cs:46357-46699`）

- `if (!poundRelease) return;`（`46359`）。锤力 0 或无物块：`poundRelease=false` 返回（`46364-46367`）。
- `IsTilePoundable`（`Player.cs:45930-45940`）：`tileHammer` 的不行；必须 `Main.tileSolid[type]`（平台 19 也是 `tileSolid`，`Main.cs:8073-8074`），或特例 314 矿车轨道、424、442、351、470、`IsLivingFire`。桌子/工作台这类非实心家具不能敲，锤子转而打墙。
- `hitWall = false; ApplyItemTime(sItem);`，`damageAmount = 100`，上下方是上锁的门则为 0（`46375-46380`）。`AddDamage < 100` → 只出效果+声音返回（`46381-46386`）。即：**塑形一下完成，不累计**。
- 然后按类型分支（`46392-46696`），最后 `poundRelease = false`（`46698`）。
- 所有塑形最终走 `WorldGen.SlopeTile` / `WorldGen.PoundTile`：
  - `CanPoundTile`（`WorldGen.cs:81980-82028`）：门 10、48 尖刺、137 机关、232、380 种植盒、387/388 机关门、476、484、巨石（`TileID.Sets.Boulders`）不行；**上方**物块是箱子/祭坛/工作台等（`ForbidsSloping`：21、26、77、88、235、237、441、467、468、470、475、488、597，`WorldGen.cs:82030-82052`）不行；最后要 `CanKillTile`。没有 `tileFrameImportant` 检查。
  - `SlopeTile(i,j,slope)`（`WorldGen.cs:82053-82088`）：`halfBrick(false)`、`slope(slope)`；`KillTile(fail:true, effectOnly:true)`（3 粒尘，`KillTile_GetTileDustAmount` `WorldGen.cs:67127-67136`：失败 3、破坏 10）、`SoundEngine.PlaySound(0)`（= Dig）、`SquareTileFrame`；若结果为整块（slope 0），把与该格重叠的玩家顶到格子上面（`position.Y = rect.Y - height`，`gfxOffY` 平滑）。
  - `PoundTile`（`WorldGen.cs:82090-82123`）：切换 `halfBrick`，同样的尘/声音/重框；变回整块时同样把玩家顶上去。

### 1.3 循环顺序

**实心块**（`Player.cs:46627-46689`；第一次敲整块时不满足 `halfBrick || slope != 0`，走最后的 `else PoundTile` → 半砖，`46690-46696`）：

```
num11 = 1, num12 = 1, num13 = 2
if ((SolidTile(x+1,y) || tile[x+1,y].slope()==1 || ==3) && !SolidTile(x-1,y)) { num12 = 2; num13 = 1; }   // 46632-46636 右侧有墙
if (SolidTile(x, y-1) && !SolidTile(x, y+1)) num11 = -1;                                                // 46637-46640 上实下空
num11 == 1 : 0(半砖)→num12 → num13 → num12+2 → num13+2 → 其他→0
num11 == -1: 0(半砖)→num12+2 → num13+2 → num12 → num13 → 其他→0
```
`WorldGen.SolidTile` = 有物块、`tileSolid`、非 `tileSolidTop`、非半砖、slope 0、非虚化（`WorldGen.cs:70650-70671`）。由此：

| 情形 | 敲击序列 |
|---|---|
| 默认 | 整块 → 半砖 → 1 → 2 → 3 → 4 → 整块 |
| 右侧贴墙、左侧空 | 整块 → 半砖 → 2 → 1 → 4 → 3 → 整块 |
| 上实下空 | 整块 → 半砖 → 3 → 4 → 1 → 2 → 整块 |
| 上实下空 + 右侧贴墙 | 整块 → 半砖 → 4 → 3 → 2 → 1 → 整块 |

邻居每次重新判断；若中途邻居变化导致当前坡度不在序列里，落到“其他→0”。点击位置、光标在格内的位置**不影响**结果。

**平台**（`TileID.Sets.Platforms`：19、427、435-439，`Player.cs:46396-46440`；`WorldGen.PoundPlatform` `82125` 同逻辑）：
```
半砖 → PoundTile → 平
否则 num = 1, slope = 2;
     if (Platforms[tile[x+1,y-1]] || Platforms[tile[x-1,y+1]] || (SolidTile(x+1,y) && !SolidTile(x-1,y))) { num = 2; slope = 1; }
     slope 0 → SlopeTile(num);  slope == num → SlopeTile(slope);  否则(CanPoundTile) slope(0)+PoundTile → 半砖
```
即 平 → A → B → 半砖 → 平；右上 `(x+1,y-1)` 或左下 `(x-1,y+1)` 有平台、或右实左空时 A=2“/”。平台不会有 3/4。

**其他**：314 轨道换形（`Minecart.FrameTrack(pound:true)`），137 机关、424、442 改 frameX，470 换姿势；`tileSolidTop` 的非平台实心物（如 387/388 被 CanPoundTile 排除）只在整块/半砖间切换（`46627` 的条件含 `!tileSolidTop`）。

**y 方向换算**：Terraria `(x, y-1)` 是上方；mod 网格 y 向上，即 `(x, y+1)`。Terraria 右上 `(x+1,y-1)` = mod `(x+1,y+1)`，左下 `(x-1,y+1)` = mod `(x-1,y-1)`。

### 1.4 几何（`Tile.cs:44-54`、`202-213`、`290-328`；绘制 `TileDrawing.cs:1571-1601` 证实）

| slope | blockType 常量 | 实心部分 | 上表面 | `topSlope/bottomSlope/leftSlope/rightSlope` | 实心面（`Framing.cs:92-98` blockStyle） |
|---|---|---|---|---|---|
| 0 | 0 Solid | 整格 | 平 | — | 上下左右 |
| 半砖 | 1 Halfbrick | 下半 8 px | 低 8 px 的平面 | — | 下左右 |
| 1 | 2 SlopeDownRight | 左下三角 | “\”左高右低 | top, right | 左、下 |
| 2 | 3 SlopeDownLeft | 右下三角 | “/”右高左低 | top, left | 右、下 |
| 3 | 4 SlopeUpRight | 左上三角 | 平顶；底面斜 | bottom, right | 左、上 |
| 4 | 5 SlopeUpLeft | 右上三角 | 平顶；底面斜 | bottom, left | 右、上 |

`slope` 存于 `sTileHeader` 位 12-14，`halfBrick` 位 10（`Tile.cs:674-716`）。`SlopeTile` 总是清半砖；半砖与斜坡互斥。

### 1.5 声音、尘、手感

- 塑形：`SoundID.Dig`（PlaySound 0）+ 3 粒该物块的尘；`ApplyItemTime(sItem)`（等于 useTime）。
- 智能光标 + 锤子：`SmartCursorHelper.Step_Hammers`（`Terraria.GameContent\SmartCursorHelper.cs:1546-1730`）只找**墙**（沿玩家→鼠标的线 `Collision.HitLineWall` / `HitWallSubstep` 找可敲墙，否则范围内离鼠标最近的可敲墙），并把 `player.poundRelease = false`（`1684`、`1726`）：**智能光标下锤子不塑形，只拆墙**。
- 锤子数据（`Item.cs`）：木锤 196 锤力 25（useTime 25，tileBoost −1，`3799`），铜锤 3505：35（tileBoost −1），锡锤 3499：38，铁锤 7：40（`1479`），铅锤 3493：43，银锤 3511：45，钨锤 3487：50，金锤 3517：55，铂金锤 3481：59，Pwnhammer 367：80（`5669`）。

### 1.6 敲墙

- `TryFindingWallToHammer`（`Player.cs:46219-46293`）：目标格是 house 墙、或其 3×3 里有别的墙（处在边缘）、或目标格**有物块** → 就选目标格；否则（天然墙内部的空格，或目标无墙且无物块）按鼠标在格内偏向哪个角，在那个 2×2 里找第一个 house 墙或边缘墙格（`46249-46292`）。
- `TryHittingWall`（`46295-46303`）：`wall > 0 && (目标无物块 || 墙格≠目标 || (!tileHammer && !poundRelease))` 且 `CanPlayerSmashWall` → `PickWall(x, y, (int)(hammer * 1.5f))`，`itemTime = useTime / 2`。所以按住锤子对着有墙的方块：第一下塑形，之后每下打身后的墙。
- `CanPlayerSmashWall`（`46305-46328`）：墙 350 不可；`Main.wallHouse[wall]`（玩家可放的墙，`Main.cs:9922` 起的大表；1、4、5、6、10-12、16-27、29-39、41-47 … 都在）随时可敲；**天然墙**只有 3×3 内有空墙(0)或 house 墙时才能敲（只能从边缘往里拆）。
- `PickWall`（`46330-46355`）：`hitTile.HitObject(x,y,2)` 累计，≥100 → `WorldGen.KillWall`，否则 `KillWall(fail:true)`。例：木锤 37/下 → 3 下；铜锤 52 → 2 下；铁锤 60 → 2 下；Pwnhammer 120 → 1 下。
- `WorldGen.KillWall`（`WorldGen.cs:60867-60907`）：`KillWall_CheckFailure`（地牢墙 `wallDungeon` 在骷髅王前、丛林蜥蜴墙 87 在石巨人前恒失败，`60909-60920`）；声音（`60922-60940`：玻璃类 13、草/花类 6、其他 Dig）；尘 10（失败 3）；`KillWall_DropItems` → `KillWall_GetItemDrops`（`60951-61842`），墙→物品表（如 1→26 石墙、4→93、5→130、16→30 土墙、27→479、21→392、106→1447、88..93→1267+wall−88、138..141→2210+wall−138）；带 `Unsafe` 的天然墙（2、3、15、40、48-59、61-65 …）返回 0 不掉落（全表见 `wall_drops.txt`，范围型分支 `>=…<=` 未被脚本展开的少数墙标成了 NO DROP）。之后 `wall = 0`、清墙漆、`SquareWallFrame`，`FramesOnKillWall` 的物块重框。

### 1.7 镐子与斜坡

`PickTile`（`Player.cs:54401-54471`）对斜坡/半砖无特殊处理，伤害照常；`KillTile` 清 `halfBrick`（`WorldGen.cs:64407`），不清 slope 位，但 `PlaceTile` 放新物块时 `tile.Clear(Tile|TilePaint|Slope)`（`WorldGen.cs:59972-59975`），所以新放的块总是整块。另外：放**实心块**时，若正下方是顶斜/半砖（非平台）或正上方是底斜，会把它们 `SlopeTile(...,0)` 变回整块（`PlaceThing_Tiles_PlaceIt_UnslopeForSolids`，`Player.cs:40844-40870`）；火把放在斜坡/半砖上同样把支撑块扶正（`Player.cs:41426-41460`）。

---

## 2. 墙

### 2.1 可加入的常用墙（ItemID→WallID，配方，拆墙掉落）

数据由脚本从 `Item.cs`（`createWall` / `DefaultToPlaceableWall`，`Item.cs:48405-48416`）、`Recipe.cs`、`KillWall_GetItemDrops` 抽取，名字取 1.4.5 自带 zh-Hans/en-US 本地化。贴图名 `Wall_{WallID}`。所有放墙物品：`useStyle 1, useAnimation 15, useTime 7, autoReuse, consumable`。几乎每个“4 墙 ← 1 材料 @工作台”配方都有自动生成的反向配方“1 材料 ← 4 墙 @同站台”（`Recipe.CreateReverseWallRecipes`，`Recipe.cs:16744-16766`，调用于 `15148`；条件：成品是墙、只有一种材料且材料不是墙）。

| ItemID | 物品 (zh / en) | WallID → 贴图 | 配方（产出 ← 材料 @ 站台, Recipe.cs 行） | 拆墙掉落 |
|---|---|---|---|---|
| 30 | 土墙 / Dirt Wall | 16 → Wall_16 | x4 ← 1×土块#2 @工作台18 (L4521) | 自身 |
| 26 | 石墙 / Stone Wall | 1 → Wall_1 | x4 ← 1×石块#3 @工作台18 (L4568) | 自身 |
| 93 | 木墙 / Wood Wall | 4 → Wall_4 | x4 ← 1×木材#9 @工作台18 (L4619) | 自身 |
| 1447 | 木栅栏 / Wooden Fence | 106 → Wall_106 | x4 ← 1×木材#9 @工作台18 (L6451) | 自身 |
| 479 | 板条墙 / Planked Wall | 27 → Wall_27 | x4 ← 1×石块#3 + 1×木材#9(任意木) @锯木机106 (L6599) | 自身 |
| 130 | 灰砖墙 / Gray Brick Wall | 5 → Wall_5 | x4 ← 1×灰砖#129 @工作台18 (L3456) | 自身 |
| 132 | 红砖墙 / Red Brick Wall | 6 → Wall_6 | x4 ← 1×红砖#131 @工作台18 (L3466) | 自身 |
| 146 | 铜砖墙 / Copper Brick Wall | 12 → Wall_12 | x4 ← 1×铜砖#145 @工作台18 (L3479) | 自身 |
| 720 | 锡砖墙 / Tin Brick Wall | 45 → Wall_45 | x4 ← 1×锡砖#717 @工作台18 (L3544) | 自身 |
| 144 | 银砖墙 / Silver Brick Wall | 11 → Wall_11 | x4 ← 1×银砖#143 @工作台18 (L3510) | 自身 |
| 721 | 钨砖墙 / Tungsten Brick Wall | 46 → Wall_46 | x4 ← 1×钨砖#718 @工作台18 (L3549) | 自身 |
| 142 | 金砖墙 / Gold Brick Wall | 10 → Wall_10 | x4 ← 1×金砖#141 @工作台18 (L3523) | 自身 |
| 722 | 铂金砖墙 / Platinum Brick Wall | 47 → Wall_47 | x4 ← 1×铂金砖#719 @工作台18 (L3562) | 自身 |
| 135 | 蓝砖墙 / Blue Brick Wall | 17 → Wall_17 | x4 ← 1×蓝砖#134 @重型工作台283 (L3336) | 自身 |
| 138 | 绿砖墙 / Green Brick Wall | 18 → Wall_18 | x4 ← 1×绿砖#137 @重型工作台283 (L3351) | 自身 |
| 140 | 粉砖墙 / Pink Brick Wall | 19 → Wall_19 | x4 ← 1×粉砖#139 @重型工作台283 (L3366) | 自身 |
| 392 | 玻璃墙 / Glass Wall | 21 → Wall_21 | x4 ← 1×玻璃#170 @工作台18 (L3120) | 自身 |
| 2505 | 针叶木墙 / Boreal Wood Wall | 149 → Wall_149 | x4 ← 1×针叶木#2503 @工作台18 (L4644) | 自身 |
| 2507 | 针叶木栅栏 / Boreal Wood Fence | 150 → Wall_150 | x4 ← 1×针叶木#2503 @工作台18 (L6481) | 自身 |
| 2506 | 棕榈木墙 / Palm Wood Wall | 151 → Wall_151 | x4 ← 1×棕榈木#2504 @工作台18 (L4649) | 自身 |
| 2508 | 棕榈木栅栏 / Palm Wood Fence | 152 → Wall_152 | x4 ← 1×棕榈木#2504 @工作台18 (L6487) | 自身 |
| 622 | 乌木墙 / Ebonwood Wall | 41 → Wall_41 | x4 ← 1×乌木#619 @工作台18 (L4629) | 自身 |
| 2210 | 乌木栅栏 / Ebonwood Fence | 138 → Wall_138 | x4 ← 1×乌木#619 @工作台18 (L6457) | 自身 |
| 623 | 红木墙 / Rich Mahogany Wall | 42 → Wall_42 | x4 ← 1×红木#620 @工作台18 (L4624) | 自身 |
| 2211 | 红木栅栏 / Rich Mahogany Fence | 139 → Wall_139 | x4 ← 1×红木#620 @工作台18 (L6463) | 自身 |
| 927 | 暗影木墙 / Shadewood Wall | 85 → Wall_85 | x4 ← 1×暗影木#911 @工作台18 (L4634) | 自身 |
| 2213 | 暗影木栅栏 / Shadewood Fence | 141 → Wall_141 | x4 ← 1×暗影木#911 @工作台18 (L6475) | 自身 |
| 624 | 珍珠木墙 / Pearlwood Wall | 43 → Wall_43 | x4 ← 1×珍珠木#621 @工作台18 (L4639) | 自身 |
| 2212 | 珍珠木栅栏 / Pearlwood Fence | 140 → Wall_140 | x4 ← 1×珍珠木#621 @工作台18 (L6469) | 自身 |
| 5216 | 灰烬木墙 / Ash Wood Wall | 316 → Wall_316 | x4 ← 1×灰烬木#5215 @工作台18 (L4654) | 自身 |
| 5217 | 灰烬木栅栏 / Ash Wood Fence | 317 → Wall_317 | x4 ← 1×灰烬木#5215 @工作台18 (L6493) | 自身 |
| 1723 | 生命木墙 / Living Wood Wall | 78 → Wall_78 | x4 ← 1×木材#9 @生命木织机304 (L4609) | 自身 |
| 3584 | 生命树叶墙 / Living Leaf Wall | 60 → Wall_60 | x4 ← 1×木材#9 @生命木织机304 (L4614) | 自身 |
| 1448 | 铅栅栏 / Lead Fence | 107 → Wall_107 | x4 ← 1×铅锭#704 @铁砧16 (L6499) | 自身 |
| 2333 | 铁栅栏 / Iron Fence | 145 → Wall_145 | x4 ← 1×铁锭#22 @铁砧16 (L6505) | 自身 |
| 608 | 沙岩砖墙 / Sandstone Brick Wall | 34 → Wall_34 | x4 ← 1×沙岩砖#607 @工作台18 (L3682) | 自身 |
| 595 | 雪砖墙 / Snow Brick Wall | 31 → Wall_31 | x4 ← 1×雪砖#594 @工作台18 (L3641) | 自身 |
| 884 | 冰雪砖墙 / Ice Brick Wall | 84 → Wall_84 | x4 ← 1×冰雪砖#883 @工作台18 (L3663) | 自身 |
| 3082 | 大理石墙 / Marble Wall | 183 → Wall_183 | x4 ← 1×大理石块#3081 @工作台18 (L5892) | 自身 |
| 3088 | 花岗岩墙 / Granite Wall | 184 → Wall_184 | x4 ← 1×花岗岩块#3086 @工作台18 (L6031) | 自身 |
| 750 | 仙人掌墙 / Cactus Wall | 72 → Wall_72 | x4 ← 1×仙人掌#276 @工作台18 (L8096) | 自身 |
| 764 | 蘑菇墙 / Mushroom Wall | 74 → Wall_74 | x4 ← 1×发光蘑菇#183 @工作台18 (L4659) | 自身 |
| 768 | 骨块墙 / Bone Block Wall | 75 → Wall_75 | x4 ← 1×骨头#154 @骨头焊机300 (L8645) | 自身 |
| 769 | 史莱姆块墙 / Slime Block Wall | 76 → Wall_76 | x4 ← 1×史莱姆块#762 @工作台18 (L8626) | 自身 |
| 1126 | 蜂巢墙 / Hive Wall | 108 → Wall_108 | x4 ← 1×蜂巢#1124 @工作台18 (L8640) | 自身 |
| 663 | 彩虹砖墙 / Rainbow Brick Wall | 44 → Wall_44 | x4 ← 1×彩虹砖#662 @工作台18 (L3015) | 自身 |
| 1267-1271 | 紫/黄/蓝/绿/红花窗玻璃 | 88-92 → Wall_88..92 | x20 ← 20×玻璃墙#392 + 1 颗对应宝石（紫晶181/黄玉180/蓝玉177/翡翠179/红玉178）@工作台18 (L3125-3153)，无反向 | 自身 |
| 1272 | 五彩花窗玻璃 / Multicolored Stained Glass | 93 → Wall_93 | x50 ← 50×玻璃墙 + 五种宝石各 1 @工作台18 (L3167) | 自身 |

（栅栏 2210-2213 的 `createWall = 138 + type - 2210`，`Item.cs:22370`；铜护板/石板墙 2432-2434 也是 `146 + type - 2432` 这种偏移，`walls_all.txt` 里这几行的 WallID 需按偏移读。天然地牢墙 7/8/9 敲下也掉 135/138/140。）

### 2.2 放墙规则（`Player.PlaceThing_Walls`，`Player.cs:39909-39943`）

- 射程：`tileBoost + blockRange`（与放方块相同）。
- 条件：上下左右四邻至少一个**有物块或有墙**（`39916`）；目标格的墙不等于手上的墙；`WorldGen.PlaceWall` 只在 `wall == 0` 时放（`WorldGen.cs:75735-75754`，边界 2 格内不放，放后 `SquareWallFrame` + Dig 声）。启用“替换”时走 `PlaceThing_TryReplacingWalls`。墙可以放在物块/平台后面。
- 放成功：`ApplyItemTime(item, wallSpeed)`；`PlaceThing_Walls_FillEmptySpace`（`39945-`）：对四邻里**四周都是同种墙**的单格空洞自动补上（消耗物品）。

### 2.3 墙的帧（`Framing.WallFrame`，`Framing.cs:326-408`；查表 `Framing.cs:111-139`）

- 邻接位：上 1、左 2、右 4、下 8；邻格 `wall > 0`（任何种类的墙都算）或邻格物块是 `TileID.Sets.TruncatesWalls` 即算有（`345-376`）。
- 变体 `wallFrameNumber`：放墙时 `resetFrame` 随机 0..2（玻璃墙 21 一半概率取 2，`388-396`），之后保存在 tile 上；`wallLargeFrames == 1/2` 的墙按 `(j%4, i%3)` / `(i%2, j%2)` 固定表取 0..3（`378-387`）。
- 四邻全有（num == 15）时再加 `centerWallFrameLookup[i%3][j%3]`（`{2,0,0},{0,1,4},{0,3,0}`），即用查表 15..19（`401-404`）。
- 帧尺寸 `wallFrameSize = (36, 36)`：每帧在贴图上占 36×36 网格，画 32×32（`WallDrawing.cs:76` 源矩形 32×32）。查表（格坐标 ×36）：

```
0:(9,3)(10,3)(11,3)   1:(6,3)(7,3)(8,3)    2:(12,0)(12,1)(12,2)  3:(1,4)(3,4)(5,4)
4:(9,0)(9,1)(9,2)     5:(0,4)(2,4)(4,4)    6:(6,4)(7,4)(8,4)     7:(1,2)(2,2)(3,2)
8:(6,0)(7,0)(8,0)     9:(5,0)(5,1)(5,2)    10:(1,3)(3,3)(5,3)    11:(4,0)(4,1)(4,2)
12:(0,3)(2,3)(4,3)    13:(0,0)(0,1)(0,2)   14:(1,0)(2,0)(3,0)    15:(1,1)(2,1)(3,1)
16:(6,1)(7,1)(8,1)    17:(6,2)(7,2)(8,2)   18:(10,0)(10,1)(10,2) 19:(11,0)(11,1)(11,2)
（第 4 个变体只给 wallLargeFrames：0:(6,6) 1:(4,6) 2:(12,5) 3:(3,6) 4:(9,5) 5:(2,6) 6:(5,6) 7:(3,5) 8:(6,5) 9:(5,5) 10:(1,6) 11:(4,5) 12:(0,6) 13:(0,5) 14:(1,5) 15:(2,5) 16:(7,5) 17:(8,5) 18:(10,5) 19:(11,5)）
```
  普通墙贴图 13 列 × 5 行（468×180），动画墙每帧下移 `Main.wallFrame[wall] * 180`（`WallDrawing.cs:120`）。mod 的 `Terraria\TileFraming.cs:42-75` 已有 0..15 三变体的表，缺 16..19（“四周都有墙”的中心变化）。

### 2.4 画墙（`WallDrawing.DrawWalls`，`WallDrawing.cs:54-221`）

- 画在物块之前（墙层在物块层后面）；位置 `(i*16 - 8, j*16 - 8)`（`151`/`170`）：32×32 帧向四周各溢出 8 px，邻墙互相盖住，边缘圆角由帧本身提供。
- `FullTile(x,y)`（`243-`）：左右邻都是整块、自己是不透明整块等情况下不画（纯优化）；斜坡/半砖格及其左右邻会画墙，墙从斜坡缺口露出来。
- 光照：`Lighting.GetCornerColors` 四角插值（非复古光照、墙非 `wallLight`）；亮度够时，与 `Main.wallBlend` 不同的邻墙交界处画 2 px 的 `WallOutline`（`180-199`）。

### 2.5 玩法影响（只记录）

- 房屋：`WorldGen.StartRoomCheck` 需要背景墙，且要 `Main.wallHouse`（玩家可放的墙）；天然墙不算。
- 刷怪：`NPC.SpawnNPC` 中候选格若 `Main.wallHouse[wall]` 则不刷（`NPC.cs:977`），house 墙后面不刷敌人；也影响 `noWorms`（`NPC.cs:411`）。
- 天光：地表有墙处挡住天光（Lighting）。墙不参与任何碰撞。

---

## 3. 斜坡与半砖的碰撞

玩家 20×42 px（`Player.cs:56699-56700`），重力 0.4、最大下落 10 px/tick（`Player.cs:2493-2501`）。每 tick 顺序（`Player.Update`，`Player.cs:28254-28277`、`28294`、`28380-28440`、`28461`）：

1. `SlopeDownMovement` → `Collision.WalkDownSlope`（`Player.cs:23602-23618`）。
2. `velocity.Y == gravity` → `Collision.StepDown`；`velocity.Y >= gravity && !controlDown` → `Collision.StepUp(..., holdsMatching: controlUp)`。
3. `fallThrough = controlDown`；`DryCollision`（`Player.cs:23684-23779`，速度 >16 时按 16 px 分段，每段重复 1、2、4、5）：`velocity = TileCollision(...)`；`position += velocity`。
4. `SlopingCollision`（`Player.cs:23866-23890`）→ `Collision.SlopeCollision`。
5. `Collision.up` → `velocity.Y = 0.01`（`28461`）。

关键：**先移动，后做 SlopeCollision**——坡面总是在本 tick 移动后的位置上算。

### 3.1 `Collision.TileCollision`（`Collision.cs:2299-2460`）

对与目标位置相交的每格（半砖：`vector4.Y += 8`、高 8，`2350-2354`）：
- `flag3` 整格跳过（`2361-2387`）：slope 3：旧头顶 + |vx| ≥ 格顶 且 旧左缘 ≥ 格左；slope 4：同上且旧右缘 ≤ 格右；slope 1：旧脚 − |vx| ≤ 格底 且 旧左缘 ≥ 格左；slope 2：同上且旧右缘 ≤ 格右。也就是人在斜面那一侧时，TileCollision 不管这格，交给 SlopeCollision。
- `flag2` = 顶斜（1/2）：不会在 TileCollision 里落地（`2399` `if (num3 != num && !flag2)`）。
- 落地：`旧脚底 ≤ 格顶`（`2388`）且 `!(tileSolidTop && fallThrough) || !(vy <= 1 || fall2)`（`2391`）→ `result.Y = 格顶 − 旧脚底`。
- 左墙 / 右墙 / 天花板（`2406`/`2426`/`2446`）都要求 `!Main.tileSolidTop`——**平台只有顶面**。
- `hoik`（`2412`、`2432`，玩家宽高都 >16 时为真，`Player.cs:23781-23790`）：撞格 i 的左面时，若 `(i−1, j)` 是 slope 2 或 4（顶着你往上的斜面），忽略这面墙；右面对称看 `(i+1, j)` 的 slope 1/3。走“/”坡冲进上一排时不被卡住。

### 3.2 `Collision.SlopeCollision`（`Collision.cs:1796-2036`）

对与当前位置相交的每格：
- 平台额外门槛（`1851-1866`）：上升中（`vy < 0`）忽略；脚底必须在 `[格顶, 格底 + 1 + |vx|]`；`(slope 1 且 vx ≥ 0 或 slope 2 且 vx ≤ 0) 且 (脚底/16 − 1 == j)`——脚**恰好在这块的底线**且朝下坡走（或静止）时忽略。`fall`（stairFall）时平台不抬，只报告 `Collision.stairFall`（`1871`、`1946-1950`、`1970-1973`、`1981-1985`）。
- 底斜 3/4（`1883-1922`）：`num4` = 头部角到实心侧的距离；头顶在斜面以下则把人往下压（`vector2.Y` 取最大），`vy` 至少 0.0101（停止上升）。
- 顶斜 1/2（`1923-1999`）：`num4` = 上坡角（slope 1 用左缘 `Position.X − 格左`，slope 2 用右缘 `格右 − 右缘`）离高端的水平距离。
  - `num4 ≥ 0`：坡面高度 `格顶 + num4`；脚底 ≥ 坡面才抬，只往上抬、取最高（`y` 记最小值），`vy > 0` 则置 0；平台时 `Collision.stair = true`。
  - `num4 < 0`（上坡角已越过高端）：平台只在脚底 − 4 − |vx| ≤ 格顶 时（`1968`）、实心块无条件，把脚放到格顶（`1976-1992`）。
- 最后用 `TileCollision(position, 修正量)` 检查这次竖直修正（`2003`）：
  - 抬升被挡（`vector5.Y > velocity.Y`，头顶有实心块）：只抬允许的量，并 `bits[1]`（slope 1）`X = Position.X − num9`、`bits[2]`（slope 2）`X = Position.X + num9`，`num9 = 期望 − 允许 < 0`：**没抬上去的量变成向下坡一侧的水平推回**；速度清零，`Collision.up = false`（`2004-2019`）。
  - 下压被地面挡（底斜）：同理水平推（`2020-2034`）。
- `SlopingCollision`（`Player.cs:23866-23890`）：`ignorePlats || controlDown || 抓钩 || 倒重力` → `stairFall = true`；SlopeCollision 报告 stairFall 就保持；否则只在没按↓时清掉——所以按↓穿过楼梯后会一直穿到没有楼梯能接住为止。在斜坡平台上（`Collision.stair`）竖直修正 > 8 + |vx| 时用 `gfxOffY` 平滑画面（`stepSpeed = 4`，`Player.cs:23881`）。

### 3.3 `WalkDownSlope`、`StepDown`、`StepUp`

- `WalkDownSlope`（`Collision.cs:1691-1794`）：只在 `vy == gravity`（站地）时。在脚下 `(脚底+4)/16` 起两行里取最高的实心/平台顶（同高时优先斜坡、再优先与行走方向一致的斜坡）；若它是朝行走方向下降的顶斜（slope 1 & vx>0、slope 2 & vx<0）且脚在上坡角坡面或以下，`vy += |vx|`（`1778`、`1787`），45° 贴着坡走下去，不会飞出去。
- `StepDown`（`3577-3639`）：站地时脚下 7 < 落差 < 17 px 直接贴下去（下半砖台阶、一格台阶）。
- `StepUp`（`3641-3790`）：前方脚部那一行的格子是实心（台阶），其上方 `ceil(42/16)=3` 格无实心 → 抬到台阶顶，抬升 ≤ 16.1 px 才执行；台阶格是半砖时目标顶 +8（只抬 8 px），台阶格上方是半砖时目标顶 −8，于是半砖与整块交替可以 8 px 一级连续走上去；顶斜块只在玩家从它的实心高侧走来时（slope 1：中心在格左边以左；slope 2：中心在格右边以右）才当台阶；`holdsMatching`（按住↑）时**平台**也能当台阶踩上去（`flag4` 里的 `holdsMatching && (... Platforms ...)`）。
- 半砖：TileCollision 里就是下半格的盒子；配合 StepUp/StepDown 形成 8 px 一级的台阶。
- 站着不滑：Terraria 没有坡面摩擦/下滑，站在斜坡上每 tick 重力 0.4 → SlopeCollision 抬回坡面 → `vy = 0`。

### 3.4 斜坡平台

- 从哪边是“实”的：只在**不上升**且脚底落在该块高度范围内时，由 SlopeCollision 把脚抬到坡面；TileCollision 对斜坡平台既不落地（`flag2`）也无侧面/底面。从下方跳上来直接穿过，落下时被坡面接住；侧面永远可穿。
- 从斜坡走上平平台（往上坡走到楼梯顶那排）：本 tick 移动后左缘（“\”）已进入平排那一格，脚低于平台顶 e px；TileCollision 不管（平台要求旧脚底 ≤ 顶、且无侧面）；SlopeCollision 对楼梯顶块走 `num4 < 0` 分支（e ≤ 4+|vx|）把脚放到格顶 = 平排的顶；下一 tick TileCollision 的落地分支 `2388` 成立，站在平排上。模拟（`sim2.py` “stair walk up”）：脚 402 → 400，从此停在 400。
- 往下坡走到楼梯脚下那排：最下一块在脚到达其底线时被 `1861` 排除；`WalkDownSlope` 的搜索行从 `(脚+4)/16` 开始只看到下方平排，不再加速下坡；人以重力落下最后几个像素（模拟：476→476.4→477.2→478.4→480，4 tick），由 TileCollision 落地。
- 楼梯与平排的几何：“\” 块 `(i,j)` 的高端接同一行左侧 `(i−1, j)` 的平平台，低端接右下 `(i+1, j+1)` 的平平台（mod 坐标：高端 `(x−1, y)`，低端 `(x+1, y−1)`）。
- 按↓：`fallThrough` 让平平台不接（`vy ≤ 1` 时），`stairFall` 让斜坡平台不抬；斜坡平台需要按着↓或 stairFall 粘滞才能穿。注意 `vy > 1`（快速下落）时即使按↓也会在平平台上停一 tick（`fall2` 只给倒重力/坐骑/抓钩等）。

---

## 4. “单向墙”

### 4.1 机制

一列竖直叠放、同为 slope 1（“\”，左高右低）的平台：

- **向右走（顺下坡方向）可通过**：人站在地面，脚底恰是最下一块的底线（地面顶 = 格底，整数 16 倍，浮点精确相等）；向右走 `vx ≥ 0`，`1861` 的条件 `slope 1 && Velocity.X >= 0 && 脚底/16 − 1 == j` 成立 → 这块被忽略；上面各块的范围门槛 `1857`（脚底 − (1+|vx|) > 格底）不满足；平台没有侧面（`2406/2426/2446`）。整列等于不存在。
- **向左走（逆上坡方向）**：`vx < 0`，`1861` 不成立；左缘进入列时 `num4 = 左缘 − 格左 ≈ 16`，坡面在脚下 → 被抬（`1935-1966`），越往左抬越高（45°）。抬到这块的顶附近（`num4` 仍 ≥ 0 而脚已进入上一块的范围 `[顶, 底+1+|vx|]`）时上一块再接着抬，所以开阔处会被顶高 16～32 px 后从高端走出去（相位相关）。
- **有天花板时变成墙**：SlopeCollision 最后的 TileCollision 检查（`2003`）发现头顶的实心块挡住了抬升，就只抬允许的量，并把差额 `num9` 变成 `X = Position.X − num9`（slope 1 时向右，`2008-2011`）的推回，速度清零（`2016-2018`）。每 tick：TileCollision 左移 |vx| → SlopeCollision 想抬 |vx| 抬不动 → 推回 |vx|，原地不动。3 格高通道里玩家头顶只有 48 − 42 = 6 px 余量，而第一块就要抬 16 px，所以必然被挡。
- slope 2（“/”）完全镜像：向左通过，向右被挡。按住↓（`stairFall`）两边都能穿过（SlopeCollision 不抬，TileCollision 本来就不管）。
- 一列不是必要条件：3 格高通道里**地面上一块**斜坡平台就已单向（模拟 rows=1）。叠满整列的作用是：门洞里无论脚在什么高度（跳起后落下、从高处进门）都有一块接住脚——上升时平台被忽略，但 3 格高的门洞里跳起只有 6 px，落下时又被接住。

### 4.2 原版最小复现

1. 用石块造一条水平通道：地面一排、顶上一排，中间**正好 3 格高**的空。
2. 在通道中某一列放 3 个木平台，从地面往上叠满门洞（只放地面那 1 个也行）。
3. 每个平台用任意锤子敲一下：左右和对角都没有平台/实心块时，第一下得到 slope 1（“\”，`Player.cs:46408-46418`）。
4. 从左往右走：直接穿过。从右往左走：贴在平台上被推回，过不去。按住↓：两边都能过。再敲一下变 slope 2，方向反过来；第三下变半砖（失效），第四下复原。
5. 对照：同样的列放在开阔处（无天花板），从右往左走只会被顶起 1–2 格然后翻过去，不成墙。

### 4.3 数值验证（`sim2.py`，逐行移植的碰撞 + 玩家 tick 顺序；恒速奔跑，每种 16 个起始相位）

```
3 格高门洞、叠 3 块：
 slope 1 ->: v1.0:16/16 v2.0:16/16 v3.0:16/16 v4.5:16/16 v6.0:16/16 v9.0:16/16   （通过）
 slope 1 <-: 0/16（全部速度）                                                   （被挡）
 slope 2 ->: 0/16（全部速度）
 slope 2 <-: 16/16（全部速度）
按住↓：四种情况 16/16 通过
开阔处、叠 6 块：四种情况 16/16 通过（逆坡方向被顶高 16–32 px）
4 格高（余量 22 px）：slope 1 逆坡 16 px 抬升可通过，slope 2 逆坡要抬 32 px 被挡——相位/速度相关，只有 3 格高是确定的单向。
```

---

## 5. 对照 mod

### 5.1 现状

- **数据**：`CellGrid.slopes`（`World\CellGrid.cs:51-52`）只给平台用，0 平、1 “\”、2 “/”；`Restore`/`ApplyNetwork` 对非平台强制 0（`118-124`、`135-142`）；`SetSlope` 只对平台（`152-159`）；`Set` 换种类时清 0（`173-183`，对应 `PlaceTile` 清 slope）。没有半砖、3/4、墙层；“墙”只有 `dug` 标志（挖空的原地形格画所在区域的墙 `Areas.WallFor`，`CellVisuals.cs:97-107`）。
- **存档/联机**：`WorldEdits.Pack` 一个字节：种类 0-3 位、平台坡度 4-5 位、dug 第 7 位（`World\WorldEdits.cs:36-48`）；存档每格 `index << 8 | value`（`Save\ModSave.cs:71-72`、`252-256`、`441-445`）；联机 `Net\TileSync.cs:33-38`、`79` 同一字节。
- **碰撞**：方块走 greedy 矩形合成 Outline 的 `CompositeCollider2D`（`ChunkColliders.cs:99`、`112-120`）；只有**平的**平台进 one-way 合成体（`IsPlatform` 要求 `GetSlope == 0`，`101-102`），每段水平连续平台一个**整格高**的盒子（`121-135`），`PlatformEffector2D useOneWay, surfaceArc 160`（`170-189`）。斜坡平台没有碰撞体，由 `Player\StairCollision.cs` 处理。
- **楼梯行走**（`StairCollision.AfterFixedUpdate`，`HeroController.FixedUpdate` 的 postfix，即物理步之前）：上升中不管（`57-62`）；在 20 px 宽的盒子下找斜坡平台，脚在 `[底 − (1+|vx|)px, 顶 (+17 px 站立时)]`（`84-85`）、底线排除（`87`，对应 `Collision.cs:1861`）、上坡角（“\”左缘、“/”右缘）坡面 `顶 − e`、越过高端时在 4+|vx| 内取顶（`88-96`）；直接改 `transform.position` 到坡面、`vy<0` 清零，并在脚下放一块 20 px 宽、0.25 高的静态“托板”（`112-117`、`121-137`）；找不到就 `Leave()` 关托板（`100-105`、`139-144`）。按↓/钩/绳 → `stairFall`（`56`）。
- **穿平台**：`TerraMovement.PlatformsAndRope`（`Player\TerraMovement.cs:921-955`）按↓且脚下是平台非实心 → 把相关 chunk 的整个平台对象停用 0.3 s（`SuspendPlatforms`，`ChunkColliders.cs:80-94`，整 chunk 的平台对所有物体都消失）并给 −2 px/tick 下落速度。`StepUp`（`TerraMovement.cs:301-322`）只处理整块台阶。

### 5.2 楼梯→平排掉落：根因

以“\”楼梯从右下往左上走、顶端接同行左侧平排 `(x−1, y)` 为例（“/”向右走镜像）：

1. 主角在楼梯顶块 `(x, y)` 上，左缘 L 在格内，`e = L − 格左 ≥ 0`，`StairCollision` 把脚放在 `顶 − e`（`StairCollision.cs:88-90`、`114`）。
2. 接下来的物理步把主角左移 `vx·dt`（3 px/tick 奔跑 = 5.625 u/s，50 Hz 时 3.6 px）。左缘越过格左、进入平排那一格时，脚仍低于平台顶 e px（0 ≤ e < vx·dt），而平排的碰撞盒是整格高（`ChunkColliders.cs:132-133`）：盒子从**右侧面**接触平台盒。
3. one-way 效果器把这个“不是从上方来”的接触禁用（`surfaceArc = 160`，`ChunkColliders.cs:183`）。下一次 postfix 里 `e < 0`，走“越过高端”分支把脚抬到顶并铺托板（`91-96`）——但接触已经建立并被判为穿越，主角此后一直贴着平台顶（距离 0，在接触偏移内），接触没有结束过。
4. 主角右缘离开楼梯格（`R <= left`，`82`）后没有任何楼梯块接他，`Leave()` 关掉托板（`100-105`），此时只有那个被禁用的平台接触在下面 → 穿下去。

Terraria 不出问题的原因：平台没有侧面（`Collision.cs:2406/2426/2446`），落地判定只看“上一 tick 脚底 ≤ 顶”（`2388`），且坡面在移动**之后**算（`Player.cs:28406` → `28435`），同一 tick 就把脚放到顶上。

（往下坡走到楼梯脚下的平排、从平排走上楼梯、从顶上平排走下楼梯，这三个方向主角的盒子都是从上方或已在上方进入平台格，不触发。依赖 Unity one-way 接触“建立时判定、持续期间保持”的行为；实测可在 autotest 里于 `Leave()` 那一帧记录 `heroCollider.IsTouching(platformComposite)` 与 `Physics2D.GetContacts` 的结果确认。）

### 5.3 推荐的忠实实现

**(a) 每格形状数据**
- 把 `slopes` 换成 `shapes`（byte），沿用 Terraria 编号：0 整/平，1-4 坡度，另加半砖。对 Dirt/Stone/Wood/Sand/Platform 生效（平台只允许 0、1、2、半砖）。`Original`（场景原地形，由场景美术画）建议视为不可塑形（与 `CanPoundTile` 拒绝的格同等处理），或单独决定“塑形即转成该区域的 Terraria 方块”。`MagicIce` 不存档，可不支持。
- `Set` 换种类清形状（`PlaceTile`）；放实心块时扶正下方顶斜/半砖与上方底斜（`Player.cs:40844-40870`）；挖掉清半砖。
- 新增 `IsFullSolid(x,y)`（= `WorldGen.SolidTile`：实心、非半砖、slope 0），供锤子邻居判断、StepUp、帧计算使用；`Cells.Blocks` 仍表示“是方块”。
- 打包（向后兼容）：位 4-6 存形状码 `0 整/平, 1-4 坡度, 5 半砖`；旧档位 6 恒为 0、位 4-5 只有 0/1/2，含义不变。`WorldEdits.Pack/Unpack`、`TileSync` 自动带上。

**(b) 碰撞体生成**（`ChunkColliders.Rebuild`）
- greedy 掩码只取 `IsFullSolid` 的方块。
- 半砖：下半格盒子（可单独跑一次 greedy，高度 c/2）并进实心合成体。
- 底斜 3/4：精确三角形 `PolygonCollider2D`（`compositeOperation = Merge`）进实心合成体——平顶可站，实心侧是墙，斜底面在跳起时把人往下/往旁边推，与 `Collision.cs:1883-1922`、`2020-2034` 一致。
- 顶斜 1/2：三角形但斜边**整体下移约 1 px**进实心合成体（挡住实心侧与底面、防高速穿透），表面行走交给推广后的 `StairCollision`（托板顶在坡面上，主角不碰斜边，不触发 HK 的贴墙/滑坡状态）。
- 平平台：保持 one-way 盒子；建议改成只占格顶 4 px 的薄盒（对应 Terraria 只有顶线，`1968` 的 4+|vx| 容差），进一步避免侧面接触。半砖平台：薄盒放在半格高处（顶 = 格底 + c/2）。斜坡平台：无碰撞体。

**(c) `StairCollision` 推广为 SlopeCollision**
- 处理所有顶斜（平台与实心）：平台用现有门槛（上升忽略、范围、底线排除、4+|vx| 高端容差、↓/stairFall 穿过）；实心顶斜不受这些门槛限制（上升时也抬、无 4+|vx| 限制、↓不穿），与 `Collision.cs:1851-1866`、`1968` 的平台条件对应。
- **5.2 的修复**：坡面用本物理步**结束时**的上坡角位置计算，只在朝上坡方向移动时前瞻：
  ```csharp
  // Terraria 在移动之后做 SlopeCollision（Player.cs:28406 → 28435）：上坡时按这一步走完的位置量坡面，
  // 越过楼梯高端的那一步脚已在平排顶上，盒子从上方进入平台格。
  float lead = v.x * Time.fixedDeltaTime;
  float e = s == 1 ? (L + Mathf.Min(lead, 0f)) - left : right - (R + Mathf.Max(lead, 0f));
  ```
  （`StairCollision.cs:88`；`82` 的重叠判断同样用扫过的 L/R。）可再加一道保险：`Leave()` 时若脚正落在平平台顶上，对主角与该平台合成体 `Physics2D.IgnoreCollision(true)` 一个物理步再恢复，强制重建接触。
- **单向墙所需**：抬升前检查头顶——主角各列在 `头顶 .. 头顶 + d` 间有 `IsSolid` 格（或用 `Physics2D.OverlapBox` 查地形层）时，只抬允许量 `a`，并把 `d − a` 作为水平推回（slope 1 推向 +x、slope 2 推向 −x），水平速度清零（`Collision.cs:2003-2019`）。现有代码直接 `transform.position += best − feet`（`114`），会把头塞进天花板。加上这一步后，3 格高门洞里的斜坡平台列自动成为单向墙；顺坡方向靠现有 `84-87` 的门槛已经穿过。
- 底斜不需要脚本：精确三角形即可。

**(d) 锤子**（`TerraMod.Update` 的 `FindTarget`/`Use`，`TerraMod.cs:338-372`、`584-616`、`696-`）
- 目标：鼠标格（射程同镐，`tileBoost`）；智能光标开时只找墙（`Step_Hammers`）并禁止塑形。
- 左键按下边沿置 `poundRelease = true`；每次挥动（useTime 冷却）：有可塑形方块且 `poundRelease` → 按 1.3 的循环 `SetShape`、Dig 声、3 粒尘（`TerraMod.MineDust(cell, kind, 3)`）、`poundRelease = false`；否则若该格（或 1.6 的 2×2 规则选出的格）有可敲的墙 → 墙伤害 `HammerPower * 1.5` 累计到 100 拆墙，冷却 `useTime / 2`。塑形后若变回整块/去掉半砖，把重叠的主角顶到格上（`WorldGen.cs:82070-82081`、`82109-82120`）。
- `ItemDef.HammerPower` 已有（`Items\Items.cs:917-918`，现只用于提示）；锤子物品已在列表（`Items.cs:134-135`）。

**(e) 墙层**
- 数据：`CellGrid` 加 `ushort[] walls`（0 无），`GetWall/SetWall` 触发 `CellChanged`；`dug` 保持为“天然背景墙”。“有墙”= `walls != 0 || dug`（放方块/火把/绳子的依附规则已把 dug 当墙，`TerraMod.cs` 中 CanPlace 注释）。
- 放置：四邻有物块或有墙；目标 `walls == 0`；1.4 自动补洞；useTime 7、autoReuse。拆：`CanPlayerSmashWall`（放置的墙随时可敲；dug 背景墙若允许拆，只能从边缘拆、无掉落）；掉落为放它的物品。
- 渲染：`CellVisuals.Refresh` 的墙层（`CellVisuals.cs:97-107`，`WallZ`）：有放置墙时用 `Wall_{id}`，邻接位按“邻格有放置墙或 dug”计；补上中心查表 16..19（`TileFraming.cs` 的 `wallTable`），sprite 32×32 居中（已是 8 px 外溢）。墙在方块后面，斜坡/半砖缺口露出墙。
- 存档：`ModSave` 新增 `Walls: Dictionary<string, List<long>>`（`index << 16 | wallId`），旧档缺省即无墙；联机新增一条墙消息（对应 Terraria TileManipulation 的 3 放墙 / 2 拆墙），不改现有字节。

**(f) 斜坡/半砖的绘制**
- 实心斜坡：按 `TileDrawing.cs:1571-1601` 逐 2 px 列平移，不是简单遮罩——slope 1：第 i 列（x=2i）画帧的 `[0, 14−2i)` 行到 `y = 2i` 处；slope 2：x = 14−2i，同上；slope 3：x=2i，画帧的 `[2i, 16)` 行到 `y = 0`；slope 4：x = 14−2i，同 3；slope 1/2 另画帧底 2 行（y 14-15）、3/4 另画帧顶 2 行（y 0-1），使帧的边框沿斜面走。可按 (贴图, fx, fy, slope) 预烘成 16×16 精灵缓存。
- 半砖：帧的上 8 行画在格的下半（`TileDrawing.cs:1016-1022`、`6145-6147`）；下方不是实心或也是半砖时（非平台）画上 4 行 + `(144, 66)` 处 4 px 底边（`1655-1665`）。整块旁边有半砖时的补边（`1606-1648`）可后做。
- 帧的邻接（`WorldGen.cs:82724-82988`）：左邻 slope 1/3、右邻 2/4、上邻 3/4、下邻 1/2 视为不相连；自身 slope 1 断上右、2 断上左、3 断下右、4 断下左；半砖只与同种半砖左右相连、不连上方。`CellVisuals.Solid()` 需按这些规则改。
- 斜坡平台已有（`PlatformStairs.FrameX`、`SupportFrameX`）；半砖平台：帧上 8 行画在下半格。

### 5.4 建议的验证

- 修复 5.2：在 `DevAutoTestPlatforms` 的“往上走”后加断言：主角走到顶端平排后再走 1 s，脚高保持 = 平排顶、`OnStair=false`、未下落；并加“\”“/”两种朝向。
- 单向墙：3 格高石通道 + 3 块斜坡平台，两个方向各走 2 s，记录是否越过该列；按住↓再测。
- 锤子循环：对开阔、右贴墙、上实下空三种情形逐次敲 7 下，记录形状序列与 1.3 表对照；平台：开阔与右上有平台两种。
