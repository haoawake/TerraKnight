# 三螳螂联机故障复核（2026-10-22）

反馈：进入三螳螂后少一只，一只死亡后尸体悬空且持续叫，随后首领卡住，玩家互相看不到人物。

## 结论

当前工作区源码（加载版本 4.4.1）在本机两个独立游戏进程的三轮测试中未复现这组连锁故障。两轮主机控制首领，一轮客机控制首领。三轮双方均记录到 `Mantis Lord`、`Mantis Lord S1`、`Mantis Lord S2` 活动，控制端发出三次死亡，另一端收到三次死亡，最终击败记录各增加一次，没有存活首领，战后可操作、人物状态持续更新且对方可见。记录中的首领及其子尸体音源没有循环播放。

这是当前源码在指定路径的运行证据，不能推出所有联机路径都正常，也不能确定旧版本的确切根因。第一阶段一只、第二阶段另外两只属于原版战斗顺序。

## 验证方式与证据

编译：`dotnet build src/TerraKnight/TerraKnight.csproj -c Release --no-restore -p:SkipPluginInstall=true -o artifacts/net-mantis-audit-build -v quiet`，0 个错误。

测试：`powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-net-boss-copy.ps1`；交换控制端加 `-ClientOwns`。

测试 DLL SHA256：`193BC4D3DEBA30F9D6A8A74883CE3A733F101E48A741C3C3570A4014B49BA569`。

| 证据目录（artifacts 下） | 控制首领的游戏 | 结果 |
| --- | --- | --- |
| netboss-copy-20261022-024244 | 主机 | 双方完成，战斗与网络检查通过；初版汇总脚本的编码错误使 result.json 未生成，后续分析在 reviewed-result.json |
| netboss-copy-20261022-024445 | 客机 | 双方完成，战斗与网络检查通过；原 result.json 将启动阶段全部 Unity Error 混入失败，原记录保留，复核见 reviewed-result.json |
| netboss-copy-20261022-024727 | 主机 | 修正汇总脚本后完整通过；Completed=true、TimedOut=false、Failures=[]、玩家存档无改动或新增、公共配置恢复成功 |

每轮 `host-saves/netboss.jsonl` 和 `client-saves/netboss.jsonl` 保存约每 0.2 秒的状态；`hostAutoTest.log`、`clientAutoTest.log` 均包含 `[NET_DONE]`，无 `[NET_FAIL]`、网络消息处理异常或战斗取样异常。`reviewed-result.json` 保存双方首领名称、结算、末次人物可见状态和音源检查。

新增测试记录包含 FSM、音源播放/循环标志、对方人物可见标志及人物消息年龄；新增断言检查战斗结算、无存活首领、无战斗异常、战后对方仍可见且人物消息新鲜。此次没有修改正式联机战斗逻辑。

## 边界

- 通过召唤物进入原生三螳螂战，自动近战命中并在击败后继续观测 12 秒；未单独覆盖自然走入房间后手动接受挑战。
- 两进程使用相同构建、隔离存档及独立插件目录；本机 TCP，没有注入延迟、丢包、断线重连，也没有覆盖战中玩家死亡或控制端退出。
- 可见性结论来自运行状态和双方消息记录；截图捕获失败，没有人工看图或听音验收，不能将音源标志当作实际听感证明。
- 客机先进入时，在主机抵达期间存在短暂不可见样本，随后恢复；没有出现战后持续互相不可见。
- 测试未替换正式游戏安装的 DLL。正式安装 DLL 与测试 DLL 哈希不同；本次测试构建加入了诊断字段，未对正式安装包作逐项源码等同性证明。
- 旧版本的原始日志、版本号与玩家存档未提供，不能宣称已经复现并修复了旧版本根因。
