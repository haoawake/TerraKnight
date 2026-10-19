# 联机 Boss 显示、仇恨和受伤同步修复

## 问题与实现

- 原敌人同步只复制根对象位置、动画和血量。非控制端关闭 AI 后，Boss 出场状态机负责激活的父容器、子精灵仍可能关闭。现在同步子部件状态，激活有效快照需要的父容器。
- 远端对象路径匹配失败时，旧逻辑仍把快照视为完整，可能误隐藏其他本地对象。现在保留“不完整”状态，禁止据此清理本地 Boss。
- 原实现明确不支持敌人生成的独立攻击同步，AI 也只识别控制端主角。现在由场景控制端发送伤害形状、启停状态、独立攻击精灵和伤害系数；每位玩家通过本地正常受伤路径结算自身碰撞，保留无敌帧和伤害减免。
- 普通敌人的 PlayMaker 目标改为每敌人独立的坐标代理：优先追踪最近命中者，随后选择附近存活玩家；同步警戒范围与视线检测。代理不移动或控制真实本地主角。
- 攻击缓存支持场景对象销毁后重新绑定；完成每轮快照后删除已经停止的攻击。所有权变化和离开会话时清理代理。
- 联机协议从 net4 升为 net5，双方必须更新；服务器只转发当前场景控制端的敌人及攻击快照。

## 验证

构建命令：

```powershell
dotnet build src/TerraKnight/TerraKnight.csproj -c Release --no-restore -p:SkipPluginInstall=true -o artifacts/net-combat-build
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-net-world.ps1 -BuildDir artifacts/net-combat-build -Suite all
```

双端用例覆盖远端命中玩家获得仇恨、关闭父容器和子精灵的恢复、无法匹配 Boss 时不误隐藏本地对象、独立攻击使客机实际扣血、攻击对象销毁后重建、双方进入真实黄蜂战场时客机 Boss 可见，以及原有敌人伤害/死亡、PvP、弹幕、挖掘同步。

测试使用隔离存档。脚本备份并恢复游戏配置和注册表，并将网络消息处理异常计入失败。共享世界回归还覆盖箱锁、家具、掉落物、水晶、跨场景交互与远端 Boss 不移动未参与玩家。

这不是所有 Boss、所有 AI 类型的完整覆盖：尚未遍历每个 Boss 的切阶段、特殊脚本攻击、断线重连和高延迟。普通敌人的目标适配针对 PlayMaker AI，直接缓存原版 HeroController 的自定义 C# AI 仍需逐类实战验证。攻击形状精确支持 Box、Circle、Polygon；其他 Collider2D 使用近似包围盒。独立攻击精灵需要对应 tk2d 图集已在本地加载。

## 交付

- 插件：`artifacts/net-combat-build/TerraKnight.dll`
- 插件 SHA256：`D0B99874AAB6FBA38E4DA6A3FCAC255EF39268DA9986460125FEDF870B6FE2EE`
- 更新包：`artifacts/TerraKnight-4.1.0-multiplayer-fix.zip`
- 全部玩家退出游戏后，将更新包中的 BepInEx 合并到现有游戏目录。

共享世界结果：`artifacts/networld-20261018-210910/result.json`，52 条断言通过，双方完成、无失败、无超时、个人存档无变化、配置恢复成功。该轮使用加入攻击缓存重建保护前的构建；最终构建的战斗回归单独记录。

战斗结果：`artifacts/net2-20261018-211024/result.json`，30 条断言通过，包括真实黄蜂客机显示、远端仇恨、独立攻击客机扣血和无法匹配时保留本地对象；双方完成、无失败、无超时、个人存档无变化、配置恢复成功，日志无网络消息处理异常。测试实际安装的 DLL SHA256 是 `B8F8C7D64484D70968F87CE896441356084C567B427A42CC69E099BA9BC5605A`。

重要验证边界：最后一次构建在该轮测试启动后完成。交付 DLL（D0B998...）额外包含销毁对象缓存重建保护、对应销毁重建用例和攻击对象的 TerraOwned 标识，已编译但尚未完成最终双端复测。两次尝试执行最终复测均被自动审批服务的“Selected model is at capacity”错误阻止，动作未执行；这不是不安全判定。交付文件不能标记为完整运行验收通过。
