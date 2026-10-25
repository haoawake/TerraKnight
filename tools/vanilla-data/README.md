# 原版调查交接资料

这些文件从 Claude 交接记录中指向的本机 scratchpad 复制而来，保留其原始内容，防止临时目录清理后丢失。调查报告内的“当前 mod 行号”和“尚未实现”是调查时的快照，不代表续作后的代码状态。

- `hammer/`：锤子循环、墙物品映射、斜坡碰撞及单向墙调查；Python 模拟器是原版参考实现。
- `fishing/`：钓竿、鱼饵、渔获规则、任务鱼、宝匣、参考实现和未完成的数据生成工作。
- `petsnpcs/`：城镇 NPC 条件、商店、宠物、照明宠物和矿车资料。

钓鱼生成器仍依赖原 scratchpad 的 `gen2/dropdata.py`、`port/`、反编译目录和液体普查结果；复制到这里不代表已经独立可运行或接入游戏。使用前应补齐可重现的数据输入并核对输出。项目内可直接重跑的墙生成器是 `tools/generate_walls.py`。

原交接目录：`D:/ClaudeTemp/claude/e------------github-----projects---/e332c8d1-bad4-4402-a7e3-1d6b4d5e6526/scratchpad/`。

## 墙数据校验更正

原始 `hammer/walls_all.json` 的 239 条静态抽取包含 3 条前景材料误报、26 条表达式编号错误，并漏了 56 种墙。经本机 Terraria 的 `Item.SetDefaults` 逐项读取，实际可放置背景墙为 292 种、292 个不同墙片编号。生成器现在只读取 `hammer/walls-verified.json`；`wall-item-stats.tsv` 保存实际属性，`DumpWallItems.cs` 保存读取工具。旧调查表保留作出处，不能直接用于生成。
