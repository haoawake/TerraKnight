# Boss 40% 霸体与房间建造开放

## 最终行为

- 普通、困难、专家、大师：原版有僵直状态机的 Boss，剩余血量高于 40% 时照原版累计僵直；降到 40% 及以下时不再累计僵直。跨过阈值的那次攻击也受保护。
- 空洞难度：保留 Boss 全程霸体。
- 判定基于当前受击身体/阶段的原版血量与最大血量，保留原有阶段与部件生命预算。不增加原版没有的僵直状态机，不阻止原版必需的转阶段和终结剧情。Boss 的位移击退规则未改。
- 整间已知 Boss 房的原生地形，包括战斗区域之外的位置，不能挖掘、炸毁或被模组下砸破坏；普通房间里临时出现的 Boss 仍保护其战斗区域。
- 允许在 Boss 房放置物块、平台、绳索、火把、墙、家具、箱子和能容纳的预制房屋；可以按正常支撑、工具、占用等规则拆回自己的建造物。
- 移除晶塔针对 Boss 战斗范围的放置限制，仍保留梦境、神居、白宫、终局场景等原有独立限制。液体使用限制和自然生成物分布没有调整。

## 实现

`BossBalance.SuperArmour` 使用整数比较处理 40% 边界，避免浮点阈值误差。本地攻击和联机控制端的伤害入口都使用伤害结算后的预期血量；`BossStunArmour` 同时保护原生 `HealthManager.TakeDamage` 路径，并在调用结束（含异常路径）恢复状态机引用。精英原有规则保留。

建造方面移除了物块、墙、家具、箱子、预制房屋、晶塔和冰魔杖的 Boss 区放置拦截。`TerrainSystem` 不再关闭整间原生 Boss 房的网格根对象；整间保留和局部保留两种房间，都只跳过原生地形的网格碰撞，玩家放下的块仍显示并生成碰撞。整间房保留原版渲染网格，避免重复的原生黑色填充。

原生保护统一由 `BossRooms.ProtectsNativeArea/ProtectsTerrain` 判定。除普通挖掘入口外，`TerraMod.Mine` 的底层操作也重新检查可挖掘性，防止直接调用绕过保护；爆炸沿用 `CanDig`。自然背景墙、尖刺和特殊下砸地板也保留原生保护。

中英文功能说明已更新。

## 最终实测

最终证据：`artifacts/single-copy-bossrules-20261022-042713`。`verification-summary.json` 记录 **81 项检查全部通过**：

| 类别 | 检查数 | 结果 |
| --- | ---: | --- |
| 五档难度的 100%、41%、40%、39%、1% 及 41%→39% 跨线判定 | 30 | 通过 |
| 大师/空洞下，大黄蜂 80%/40% 的原生僵直表现 | 4 | 通过 |
| 同四种情况下直接走原生受击入口 | 4 | 通过 |
| 大黄蜂房、三螳螂房的建造与原生保护 | 43 | 通过 |

大师 80% 时实际出现 `Stun Start`、`Stun Air`；40% 时僵直计数与僵直状态切换均为 0。空洞 80%/40% 均为 0。原生伤害入口在大师 80% 时不拦截，在大师 40% 和空洞两档下各拦下 23 次僵直通知；伤害正常结算。

房间测试分别覆盖 `Fungus1_04` 的局部原生地形模式与 `Fungus2_15` 的整间原生地形模式。检查了原生地形反复挖掘、爆炸保护，战斗区域之外的原生保护判定，以及真实物品放置、活动精灵、物理碰撞、角色落在新增方块上站稳、方块回收、墙的放置/回收、工作台放置/拆除、箱子放置。

`result.json`：Completed=true、TimedOut=false、Failures=[]，玩家存档无改动或新增。外围脚本 `artifacts/run-bloom-guard.ps1` 另外备份恢复公共配置和注册表，输出 `SharedConfigRestored=True`；内部通用脚本的结果仍保留其自身未执行公共配置恢复的说明。

本轮是单进程真实游戏验证，未完成新一轮双客户端回归、逐 Boss 遍历、保存重载、预制房屋/晶塔的逐类实放或人工画面验收。

## 构建与本机安装

工作区同期正在写入菜单、外观和新 AI。最终主工程全量构建曾因新 AI 缺少 `Profiles` 等未完成依赖失败。没有修改或补桩那些模块。

本次使用独立项目 `artifacts/boss-rules-scoped-project/TerraKnight.csproj` 编译现有源码，明确排除尚未完成的 `EnemyAi/**/*.cs`，保留此前正式安装的原有 AI。构建 0 个错误。该包没有交付或验证正在开发的新 AI。

```powershell
dotnet build artifacts/boss-rules-scoped-project/TerraKnight.csproj -c Release --no-restore -p:SkipPluginInstall=true -o artifacts/boss-rules-final-build
powershell -NoProfile -ExecutionPolicy Bypass -File artifacts/run-bloom-guard.ps1 -Suite bossrules -BuildDir artifacts/boss-rules-final-build -TimeoutSec 600
```

已备份并替换本机正式安装的 DLL、PDB，安装 DLL 与最终实测构建一致：

`985A618073ADDB00ADE4F928079109A41587C798E5D44F4E94A8A593AC00EAA5`

备份：`artifacts/before-boss-rules-final-install-20261022-042953`。

早期 `single-copy-bossrules-20261022-040701` 的失败记录保留：测试暴露并修复了底层挖掘缺少保护的问题，也修正了测试中同帧拆块后未等待碰撞刷新、低伤害不足以稳定触发原生眩晕的条件。`041043` 为中间版本 76 项通过；`041503` 在构建失败后误启动了旧构建，已主动终止，不作为最终证据。
