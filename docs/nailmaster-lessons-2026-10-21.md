# 骨钉大师授课恢复

修复前的可见隔离运行 `artifacts/single-copy-nailteacherscan-20261020-235508` 记录了三位大师实际加载后的会话状态机。`Player/NailServices.cs` 的旧补丁把三处关键跳转改为：

- 初次对话结束：`Meet / CONVO_FINISH → Talk End`。
- 再次提议结束：`Reoffer / CONVO_FINISH → Talk End`。
- 没有学会时：`Convo Choice / REOFFER → Greet`。

因此，虽然剑技战斗代码已经恢复，普通新档仍被绕过学习提议。以前手动设置“已学会”标志的战斗测试不能证明正常授课可用。

此次移除整个旧的授课拦截，保留原版会话、是/否选择、收费、演出、学习标志和结束流程。初见与再次询问都恢复为原版授课路径；已经见过但未学会的玩家无需重开存档。获得提示继续由现有 `TutorialNotes` 和 `ObsoletePrompts` 适配为泰拉按键说明。

另外修复 `NailArts.TerrariaDash` 排除 `TerraMovement.HkDashing` 的问题：暗影披风冲刺也由自定义移动驱动，不会自行设置原版 `dashing` 标志，现在与盾冲一样可接冲刺斩。

## 验证方式

专用套件 `nailteachers` 从三种剑技均未学习、三位大师均未见过的隔离存档开始。角色走近并按实际交互键发起会话，选择框通过原版菜单导航事件选中选项、用实际确认输入提交，由原版付款与授课状态机处理；测试不直接把技能标志设为已学会。

检查首次提议、拒绝不学习/不扣钱、拒绝后再次询问、三位大师实际授课、原版扣款、结束后返回空闲、已学会后不重复收费、跨场景保留，以及学到的三种剑技释放。奥罗另测余额不足的拒绝与补足金额后学习。马托和席奥免费，奥罗800吉欧。

最终运行证据目录：`artifacts/single-copy-nailteachers-20261021-002136`。运行结束后的 `result.json`、`saves/nail-teachers-check.json` 和 `AutoTest.log` 为结果依据；本记录不将单机套件视为双客户端授课同步或完整通关的证明。

最终结果：34项检查全部通过，套件正常完成且未超时；正式玩家存档未修改、未新增。马托和席奥分别实际扣款0吉欧，奥罗实际扣款800吉欧；三种剑技由原版授课授予后均实际释放成功，包括暗影冲刺接冲刺斩。

测试截图由测试副本保存于 `F:/TK-Test2/Hollow Knight/BepInEx/TerraKnight/dev/single-nailteachers`。正式安装前备份旧DLL/PDB，并核对安装文件与通过测试的构建完全一致。此修复不修改玩家已有存档或主动补发剑技。
