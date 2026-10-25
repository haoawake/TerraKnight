# 蜂巢骑士刺球与跳跳平移核查

## 结论

- 蜂巢骑士刺球原版支持受击位移。`Control` 状态机在 `Form 2` / `Antic` 接收 `TINK LEFT/RIGHT/UP/DOWN`，进入对应 `Tink` 状态，通过 `iTweenMoveBy` 移动后进入 `Burst`。泰拉武器、弹幕、召唤物通过 `PropHits` 触发同一原版机制。本次保留这一行为。
- 跳跳另有可复现的击退速度交接缺陷：击退开始保存旧横向速度，原版期间进入停止状态，击退结束仍恢复旧速度。已修正为记录原版 `SetVelocity2d` 最新的横向命令，交还控制时使用它。未发出新横向命令的持续行走敌人沿用原先速度；仅设置纵向速度的命令不会误记击退横向速度。

## 修复与测试入口

- `Items/TerraKnockback.cs`：`KnockFlight.RememberNativeVelocity`、`KnockFlightNativeVelocity`。后续飞行分支扩展和双端结果见 `enemy-slide-multiplayer-validation-2026-10-22.md`。
- `DevAutoTestEnemySlide.cs`、`enemyslide` 套件：真实游戏场景和原版对象验证。
- 构建：`dotnet build src/TerraKnight/TerraKnight.csproj -c Release --no-restore -p:SkipPluginInstall=true -o artifacts/enemy-slide-fixed-build`，0 个错误。构建包含当前工作区已有的新 AI 源码，测试世界明确使用原版 AI。

## 复现证据与范围

初始对照：`artifacts/single-copy-enemyslide-20261022-112746/AutoTest.log`。

对真实跳跳设置受击前横向速度 4，施加击退，在击退期间令原版 FSM 进入 `Sleep`。交接时 FSM 已回到等待状态 `Init`，但旧版恢复 `vx=4`，再过 0.2 秒继续移动 0.800。修复后相同受控路径交接时 `vx=0`，0.2 秒移动 0.000；随后唤醒原版跳跃状态，仍能正常起跳。此为受控交接缺陷复现，不能视为已复现用户完整自然战斗过程。

三颗原版刺球分别启动形成、待爆、自然爆炸流程，未攻击时横向偏移约 0.1（原版待爆抖动），未见持续漂移；分别调用武器使用的 `PropHits.Pulse` 从左右攻击，每颗均进入对应 `Tink` 状态并移动后爆炸。状态机位移参数为 ±3，实测世界坐标位移约 ±2.5；测试验证方向、有限位移、触发事件和爆炸状态，不将参数直接等同世界位移。

最初两轮套件曾因把状态机位移参数直接当作世界位移而将刺球检查判失败；这些失败保留在原始日志中。断言已按观察层次修正，没有修改刺球玩法。

不包含完整蜂巢骑士自然战斗、逐帧人工视觉核对、多人双客户端验证或所有怪物的全量回归。不能据此宣称所有平移现象均已修复。

## 最终验证

`artifacts/single-copy-enemyslide-20261022-113252/`：13/13 检查通过，`Completed=true`、`TimedOut=false`、失败 0，正式玩家存档未改变也未新增。包装脚本输出 `SharedConfigRestored=True`；内部 `result.json` 的共享设置字段仍为底层脚本自身的占位值，不代表包装脚本未恢复。

测试 DLL：`artifacts/enemy-slide-fixed-build/TerraKnight.dll`，SHA256 `BAA32CE66B226F9D93EC3315203BB78B7CBFF552243FE6E93A4451A11EF78445`。

已有连续击退回归 `artifacts/single-copy-fx_17knockjuggle-20261022-113404/` 同样通过：真实 Buzzer 单次击退与半秒十次连击均退后 1.99，挡住 4 次空中追加和 5 次站稳期间追加。无超时、失败、正式玩家存档变化；包装脚本确认共享设置恢复。

已备份并安装 DLL/PDB 至本机正式 `F:/SteamLibrary/steamapps/common/Hollow Knight/BepInEx/plugins/TerraKnight`，安装后 SHA256 与测试 DLL 一致。备份：`artifacts/before-enemy-slide-install-20261022-113506`。安装前确认正式游戏关闭且旧 DLL 哈希未发生并发变化。
