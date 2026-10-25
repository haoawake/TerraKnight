"""Summarize sequential AI probes without promoting fixture gaps into passes."""
import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("log", type=Path)
parser.add_argument("--visual-log", type=Path)
parser.add_argument("--boss-log", type=Path, action="append", default=[])
parser.add_argument("--visual-reviewed", action="store_true")
parser.add_argument("--current-baseline", action="store_true")
args = parser.parse_args()
text = args.log.read_text(encoding="utf-8-sig")
visual = args.visual_log.read_text(encoding="utf-8-sig") if args.visual_log else ""
boss_log = "\n".join(p.read_text(encoding="utf-8-sig") for p in args.boss_log)
build_log = ROOT / "artifacts/new-ai-audit-current-build.log"
build_text = build_log.read_text(encoding="utf-8-sig") if build_log.exists() else ""
current_build_passed = "0 个错误" in build_text and "error CS" not in build_text
names = dict(re.findall(r'\["([^"\n]+)"\].*?Zh = "([^"\n]+)"', (ROOT / "src/TerraKnight/Items/EnemyDropData.cs").read_text(encoding="utf-8-sig")))
rows = []
for key, ordinal, count in re.findall(r"NewAI profile (\w+)：#(\d+)/110 moves=(\d+)", text):
    unavailable = re.findall(r"\[NEWAI_UNAVAILABLE\] " + re.escape(key) + r": ([^\n]+)", text)
    attached = "NewAI real prefab attached " + key + "：" in text
    updated = "NewAI real prefab update " + key + "：" in text
    forced = re.findall(r"NewAI forced lifecycle " + re.escape(key) + r"/(\d+)：([^\n]+)", text)
    rows.append(dict(number=int(ordinal), key=key, name=names.get(key, key), moves=int(count), attached=attached, short_update=updated, forced_entries=len(forced), unavailable=unavailable))
checks = [line for line in text.splitlines() if "结果 NewAI " in line]
failures = [line for line in checks if "不通过" in line]
visual_failures = [line for line in visual.splitlines() if "结果 NewAI " in line and "不通过" in line]
bosses = []
for path in sorted((ROOT / "src/TerraKnight/EnemyAi/Bosses/Kits").glob("*.cs")):
    for key in re.findall(r'\[Kit\("([^"\n]+)"\)\]', path.read_text(encoding="utf-8-sig")):
        bosses.append((key, path.name))
summary = dict(completed="[NEWAI_DONE]" in text, checks=len(checks), failed=len(failures), profiles=len(rows), attached=sum(r["attached"] for r in rows), short_updates=sum(r["short_update"] for r in rows), forced_entries=sum(r["forced_entries"] for r in rows), unavailable=[r["key"] for r in rows if r["unavailable"]], failures=failures, visual_failures=visual_failures, visual_completed="[NEWAI_DONE]" in visual, visual_checks=sum("结果 NewAI " in line for line in visual.splitlines()), current_full_source_build_passed=current_build_passed, test_build="110-profile baseline uses prior casino implementation; final core/visual regression uses full current source build", rows=rows)
summary["visual_reviewed"] = args.visual_reviewed
if args.current_baseline:
    summary["test_build"] = "Each suite used its full source build; 110-profile scan precedes final Mantis landing-state fix; latest core/visual and Mantis retest cover that fix"
summary["boss_checks"] = [line for line in boss_log.splitlines() if "结果 NewAI " in line]
summary["boss_failures"] = [line for line in summary["boss_checks"] if "不通过" in line]
summary["boss_completed"] = "[NEWAI_DONE]" in boss_log
summary["boss_observed"] = re.findall(r"\[NEWAI_BOSS_END\] (\S+):", boss_log)
dest = ROOT / "artifacts/new-ai-audit-result.json"
dest.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
lines = ["# 新版敌人 AI 逐项检查：第一轮", "", "2026-10-23。按配置顺序逐只生成，逐条记录。保留原版和招式设计中有意的传送，排除非预期跳位。", "", "## 已修复", "",
"1. 目标死亡、消失或变为友好后，接管身体的招式及晶壳姿态未及时结束，可能卡住。现在释放身体和状态机。",
"2. 连招后续段未重新申请攻击名额，且缩短前摇后可能低于最短值。现在重新申请名额，遵守受伤宽限和 0.3 秒最短前摇。",
"3. 跳扑附带地波时把跳高覆盖成 2 格，波速参数未生效。现在两组参数独立。",
"4. 激光扫射方向在创建攻击区之后修改，实际方向无效。现在先确定方向再创建，并保留原配置。",
"5. 隐藏结束时把原先关闭的所有 Renderer 都打开。现在逐个恢复原状态。",
"6. 停用或回退时遗留瞄准替身、接管状态或隐藏状态。现在相应清理；原版模式和停用 Brain 不再附加新版触碰减益。",
"7. 水晶爬虫结束晶壳姿态时回到起手坐标。现在保持当前身体中心，只恢复朝向和防御。",
"8. 落体招式超时后瞬间回到天花板。现在连续沿原路返回，遇到新建实心表面则停止，结束后恢复物理。",
"9. 圆形、方形、柱形和地波主要依赖粒子，粒子预算耗尽时范围不可见。现在每帧绘制范围，不依赖粒子额度。",
"10. 格林亲族小型/大型预制体没有原生死亡图鉴字段，物体名大小写也与配置不同。补充显式身份映射；生成器同时查找已加载的原生预制体。",
"11. 死亡鲁多的生成来源指向白银试炼，改为愚人试炼。",
"12. 没有专属减益的身体招式会覆盖种族原有触碰减益。现在保留种族减益，只有明确配置的招式减益才优先覆盖。",
"13. 首领新增状态的协程中断后没有释放迭代器，嵌套招式也未统一受生命状态约束。现在状态退出、死亡和组件停用都会释放已进入的各层迭代器，执行 finally 收尾。",
"14. 首领和普通敌人的防卡逻辑直接搬回安全点，可能造成意外瞬移。现在暂停身体控制后以每秒 8 单位连续脱困，单步最多 0.4 单位，中断恢复原物理类型、重力及脚本开关。首领不再因重复脱困而永久停止防卡；普通敌人不在联机傀儡端自行纠位。",
"15. 假骑士越过已消失的左门时原先直接移回门内。现在纠位有速度和单步上限。",
"16. 大黄蜂的升空、诺斯克的俯冲、螳螂领主的两种斜冲、灵魂战士的空中冲刺，缺少中断收尾。现在相应恢复重力、停止残速、恢复朝向并销毁伴随攻击区；搬运弹体的协程也在 finally 中销毁弹体。",
"17. 螳螂领主的两种斜冲在物理受阻或运行时间到期后直接贴到预定终点，可能穿墙瞬移。现在按物理步推进到实际可到达的位置，受阻及时结束。天花板斜落接原生 Dstab Land 时也临时保留实际高度，避免原生 SetPosition 再次跳到 Land Y，状态退出后恢复原变量引用。",
"18. 躯壳矿工从 Crossroads_45 复制到了尚未敌对、HealthManager 停用的米拉。改为从 Mines_04 取得真正的战斗矿工，已补测挂载、短时更新及 3 个招式入口。",
"19. 临时借用场景的 PlayMakerUnity2d.Awake 会覆盖全局事件转发器；卸载后指向已销毁对象，导致后续碰撞/触发代理停用。复制敌人启动前及借用流程结束时恢复当前场景原有转发器，逐只检查可用性。", "", "## 运行证据与边界", "",
f"- 名单 {len(rows)}/110；配置及回归检查 {len(checks)} 项，失败 {len(failures)}；完成标记：{'存在' if summary['completed'] else '尚未出现'}。",
f"- 真实预制体挂载 {summary['attached']} 种；1.8 秒自然更新 {summary['short_updates']} 种；强制招式入口/清理 {summary['forced_entries']} 项。",
"- 强制入口测试不证明招式自然选择、完整轨迹、全部阶段或完整战斗合理；未完成双客户端实战。",
"- 借用场景复制预制体期间出现 PlayMakerUnity2DProxy 缺少本场景 Unity 2D Prefab 的警告，不能据此宣称完整原生场景行为已经通过。",
("- 各轮均使用当轮完整源码重建的 DLL、独立游戏和存档目录。110 种复扫后最后修改了螳螂原生落地衔接，由最新核心/视觉回归及螳螂复测补验。未安装到主游戏。" if args.current_baseline else "- 测试使用隔离游戏和存档。最初 110 种扫描期间采用先前可编译赌场实现隔离同时变更的模块；最终核心/视觉回归使用完整当前源码重建的 DLL。未安装到主游戏。"),
f"- 当前完整源码重建：{'通过，0 编译错误' if current_build_passed else '未通过'}，日志 `{build_log.relative_to(ROOT)}`。",
f"- 最终核心/视觉回归：{summary['visual_checks']} 项，失败 {len(visual_failures)}；完成标记：{'存在' if summary['visual_completed'] else '尚未出现'}。",
("- 最新视觉回归仍有失败，尚未放行。用户已授权短暂显示隔离测试窗口。" if visual_failures else "- 已短暂显示隔离窗口；相机图中 18 类弹体、6 类攻击区的预警及生效阶段可见。" if args.visual_reviewed else "- 相机图尚需人工复核；纹理加载和绘制计数不等同于可见证明。"),
f"- 首领原生场景短时观察：{', '.join(summary['boss_observed']) or '未完成'}；检查 {len(summary['boss_checks'])} 项，失败 {len(summary['boss_failures'])}。每只仅观察 45 秒；不替代完整阶段、击杀、所有特效或联机验证。",
f"- 未充分覆盖：{', '.join(summary['unavailable']) or '本轮未记录生成缺口'}。", "", "## 普通敌人和精英逐项表", "", "| 序号 | 敌人 | 新增招式数 | 真实挂载 | 短时更新 | 强制入口数 | 尚需处理 |", "|---:|---|---:|---|---|---:|---|"]
for r in rows:
    gap = "；".join(r["unavailable"]).replace("|", "/") or "完整战斗、自然选招、原生场景和联机仍待核验"
    lines.append(f"| {r['number']} | {r['name']} `{r['key']}` | {r['moves']} | {'通过' if r['attached'] else '未覆盖'} | {'通过' if r['short_update'] else '未覆盖'} | {r['forced_entries']} | {gap} |")
lines += ["", "## 首领后续逐项清单", "", "以下仅建立源码清单，不能标记为已逐只实战通过。普通/精英的测试不替代首领、梦境和神居测试。", "", "| 首领配置 | 文件 | 状态 |", "|---|---|---|"]
for key, file in bosses:
    observed = [k for k in summary["boss_observed"] if k.split('@')[0] == key]
    status = f"已观察 {', '.join(observed)} 每只 45 秒；完整阶段、击杀、其他变体和联机待核验" if observed else "待原生场景逐只实战及变体核验"
    lines.append(f"| {names.get(key, key)} `{key}` | `{file}` | {status} |")
lines += ["", "## 文件", "", f"- 原始运行日志：`{args.log}`。", f"- 最终核心/视觉日志：`{args.visual_log}`。", f"- 结构化逐项结果：`{dest.relative_to(ROOT)}`。", "- 源码回归入口：`DevAutoTestNewAiAudit.cs`，套件 `newaiaudit` / `newaivis`。", "- 实际相机渲染：`artifacts/new-ai-runtime/BepInEx/TerraKnight/dev/single-newaivis/newai_effects_camera.png` 和 `newai_zones_camera.png`。", ""]
(ROOT / "docs/新版敌人AI逐项检查-2026-10-23.md").write_text("\n".join(lines), encoding="utf-8")
print(json.dumps({k: v for k, v in summary.items() if k != "rows"}, ensure_ascii=False, indent=2))
