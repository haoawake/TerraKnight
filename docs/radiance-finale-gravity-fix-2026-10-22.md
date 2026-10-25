# 普通辐光终结动画重力冲突修复

反馈范围：普通辐光、单人。最后一击进入动画后，玩家似乎被强制拉下来，随后卡死。

## 已确认与未确认

修复前的真实游戏回归捕捉到重力覆盖：在原生 `Final Impact` 接管角色后，`Knight Pull` 的重力为 0，随后 `Ball Tween`、`First Idle`、`Knight Idle` 等阶段变为 0.79。复现条件是在进入终结动画前留有未结束的模组暗影冲刺。

模组的冲刺收尾和超时兜底会恢复保存的重力；这一写入发生在游戏接管角色之后，覆盖了原生悬停动画选择的重力。世界边界保护也没有完整避开游戏接管/替身演出，存在干涉原生摆位的路径。

该轮修复前回归仍进入了结局，没有复现玩家所说的完整“强制下落后卡死”。所以这里确认并修复的是相关的重力所有权冲突，不能把它直接当作原存档卡死的唯一根因。

## 改动

- `Player/TerraMovement.cs`：原生接管控制前，先收尾仍由模组持有的冲刺重力并清理冲刺状态。游戏接管角色后，移动后处理和冲刺兜底只清理残留状态，不再覆盖原生动画的重力或速度。前置处理在其他补丁允许执行原生接管时才运行，避免越过联机战斗隔离的拦截。
- `World/WorldBorder.cs`：玩家不具备操作权或原生主角被替身演出隐藏时，不钳制位置或触发模组越界回退；同时清空旧的深坑计时。
- `DevAutoTestRadianceBlow.cs` / `DevAutoTestFix1016.cs`：新增 `fx_radiancefall` 路径，覆盖普通冲刺自然收尾、携带未结束冲刺进入动画、等待终结攻击、鼠标终结输入、击杀后的完整演出与结局转场。

## 运行验证

构建 `artifacts/radiance-fall-build`：0 个错误。

修复前证据：`artifacts/single-copy-fx_radiancefall-20261022-034605`。其 `saves/radiance-fall.json` 中等待终结输入阶段的重力为 0.79；该轮旧的两项断言只覆盖输入和位置，不能当作重力正确的证明。

修复后证据：`artifacts/single-copy-fx_radiancefall-20261022-035003`。

5/5 检查通过：

1. 普通冲刺结束仍恢复重力 0.79，没有将正常移动永久悬空。
2. 左键能驱动原生终结攻击，进入 `Kill Hit`。
3. 等待攻击期间最大下落约 0.000002 单位，无异常下落。
4. 等待攻击期间重力保持 0。570 个约每 0.05 秒记录的样本中，从 `Knight Pull` 到 `Final Black` 的已采样演出状态均保持 0，世界边界干预计数不变。
5. 原生流程经过 `Tendrils 3`、`Yank Down`、`Drag`、`Final Black`、`Ending Scene`，真实进入 `Cinematic_Ending_C` 并创建结局视频播放器。转场时原生恢复重力属于正常收尾。

`result.json`：Completed=true、TimedOut=false、Failures=[]，玩家存档无改动或新增。外围测试脚本另行备份并恢复公共配置和注册表，输出 `SharedConfigRestored=True`。

这是一条进入真实场景后从原生 `Final Impact` 启动的终结动画回归，不是完整从头击败辐光的通关测试；未覆盖原玩家存档、装备组合、自然剧情入口、无上辐光或多人终结动画，也没有人工画面验收。

## 本机安装

游戏关闭时已备份并替换正式安装 DLL、PDB。安装 DLL 与测试构建一致：

`321EC2D330358E03AD510025C6D1C04D80FF16875C5C4D4C2E3B27FDDA3251F1`

备份：`artifacts/before-radiance-fall-install-20261022-035219`。
