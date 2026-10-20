# 蓝湖沉睡渔夫不可见

沉睡渔夫确实生成在蓝湖左岸，实测脚底约为 (15.1, 23.0)。原实现把他的绘制顺序设为主角原版 MeshRenderer 的顺序减一，实际为 `Default/-1`，导致他排在 `Default/0` 的蓝湖背景与雾层后面。对象和右键交互存在，不代表玩家能看见。

修复：使用普通城镇 NPC 的 `Default/0` 和 `FxBatch.TownZ=0.00395`，通过深度保持在玩家后面，而不降低整层绘制顺序。原位置及救援条件保持不变。

隔离专项 `fishingangler` 已验证生成、正确绘制层、右键救醒、救醒后入住四项。相同相机、位置和画面只切换渔夫顺序，对比确认旧顺序不可见、修复顺序可见。截图为诊断相机的世界层画面，排除了 HUD 和全屏暗幕；不作为完整游戏 UI 截图。

证据：`artifacts/single-copy-fishingangler-20261019-222917/`，含 `result.json`、`AutoTest.log` 与 `screenshots/angler-fixed-world.png`、`screenshots/angler-old-order-world.png`。完成、无超时、无失败，正式玩家存档未新增或改变；测试器未承诺恢复共享设置。

完整钓鱼套件另跑一次，结果为 28/29；唯一失败是旧的鱼饵掉落数量阈值，渔夫生成、救醒、入住及任务交付均通过。此次未调整任何掉率。

百科生成器已补充蓝湖救援条件和每日钓鱼任务说明，去掉渔夫仅提供普通对话的错误描述；本次没有重导过期资源快照或更新线上 Wiki。

修复构建：`artifacts/angler-build/TerraKnight.dll`，SHA-256 `5DC014D97EBB9E8FA0831C5B71E1F55C26BE21ABE936635DEB8D7FBDD10EB39F`。正式游戏安装未替换，临时 R: 别名已移除。
