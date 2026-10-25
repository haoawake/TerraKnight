"""Build the Chinese balance report from a completed isolated combatupgrade run."""
import argparse
import json
import math
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("run", type=Path)
    parser.add_argument("--godhome-run", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    run = args.run.resolve()
    read = lambda name: json.loads((run / name).read_text(encoding="utf-8-sig"))
    result = read("result.json")
    combat = read("saves/combat-upgrade.json")
    loot = read("saves/loot-balance-runtime.json")
    weapons = read("saves/weapon-balance.json")
    craft = read("saves/craft-progression-audit.json")
    vanilla = read("vanilla-recipe-check.json")
    assert result["Completed"] and not result["TimedOut"] and not result["Failures"]
    assert not result["ChangedPlayerSaves"] and not result["NewPlayerSaves"] and result["RestoredSettings"]
    assert combat["Failures"] == weapons["Failures"] == craft["Failures"] == 0
    assert not loot["summary"]["Failures"]
    assert not vanilla["failures"]
    items = {d["Type"]: d for d in loot["items"]}
    name = lambda t: items.get(t, {}).get("Name", t)
    lines = [
        "# 锻造、重铸与旗帜加入后的战斗和掉落调整", "",
        "证据来自本次隔离存档运行：`" + str(run.relative_to(root)) + "`。本报告描述本地实现及固定靶验证，未发布新版本。", "",
        "## 新旧阶段掉落", "",
        "普通怪和普通精英会累积保留从其原生阶段到当前阶段的旧候选；不会升阶后清空旧装备。",
        "同一装备池有补回的历史候选时，当前构建的候选共享70%权重，补回候选合计共享30%；每池仍只进行一次抽取。这里的70%/30%是池内份额，不是每杀一只怪的出货率。",
        "矿石和材料原本就保留的旧候选也参与倾斜：同一资源池内有当期及旧期材料时，原权重分别乘1.15和0.85，再归一化。总体抽取概率和数量预算不因此膨胀。没有当期材料的旧资源池继续正常产出。",
        "Boss和具名精英保留各自固定奖励阶段；治疗、永久升级、工作站等独立奖励沿用各自规则。地区来源限制仍生效。", "",
        "普通难度示例（每次击杀的边际概率，已计入池内权重）：", "",
        "|阶段|怪物记录|物品|每杀概率|", "|---|---|---|---:|",
    ]
    for stage, record, wanted in [(3, "BeamMiner", {"CobaltOre", "PalladiumOre", "MythrilOre"}),
                                  (4, "MossmanRunner", {"JungleSpores", "Vine", "ChlorophyteOre"}),
                                  (5, "MossmanRunner", {"JungleSpores", "Vine", "ChlorophyteOre"})]:
        rows = loot["stage" + str(stage)][record]
        for t in sorted(wanted):
            for d in rows:
                if d["Type"] not in (t, items[t]["TypeId"]):
                    continue
                group = d["Group"]
                chance = d.get("Chance", max(1, d["Num"]) / max(1, d["Den"]))
                if group > 0:
                    options = [o for o in rows if o["Group"] == group]
                    weight = lambda o: o["Weight"] if o["Weight"] > 0 else 1
                    first = options[0]
                    chance = first.get("Chance", max(1, first["Num"]) / max(1, first["Den"])) * weight(d) / sum(map(weight, options))
                lines.append(f"|{stage}|{record}|{name(t)}|{chance:.3%}|")
    lines += ["", "## 同期材料与制作链", "",
        "合成不检查玩家阶段或成品阶段，也不要求阶段内首杀标志。只要持有原材料并满足原工作站及环境条件，就可制作。阶段仅控制材料、装备和工作站的获取；原配方材料、数量及工作站保持不变。表中的阶段是预计正常获取阶段，不是合成门槛。", "",
        "|阶段|调整后的链条|", "|---|---|",
        "|3|钴/钯矿→锭；秘银/山铜→秘银砧，可在泪水之城、水晶山峰等当地来源取得|",
        "|4|精金熔炉＋叶绿矿→叶绿锭→武器/护甲；松露人同阶段出售自动锤炼机，可继续制作蘑菇矿锭；皇家水道可出灵气|",
        "|5|碎片Boss同时提供远古操纵机（首杀必出，重复25%）；夜明矿加入同期Boss资源池，碎片/夜明锭的武器和护甲同阶段可做|", "",
        "下表直接读取运行时配方；替代配方逐行列出。", "",
        "|成品|预计获取阶段|产量|原材料|工作站|", "|---|---:|---:|---|---|",
    ]
    targets = {"CobaltBar", "PalladiumBar", "MythrilAnvil", "ChlorophyteBar", "ChlorophyteSaber", "ChlorophytePlateMail",
               "ShroomiteBar", "LunarBar", "VortexBeater", "Phantasm", "VortexHelmet", "VortexBreastplate", "VortexLeggings"}
    for r in loot["recipes"]:
        if r["Result"] in targets:
            ingredients = "、".join(f'{name(i["Type"])}×{i["Count"]}' for i in r["Ingredients"])
            lines.append(f'|{name(r["Result"])}|{r["StationStage"]}|{r["Count"]}|{ingredients}|{r["Station"]}|')
    lines += ["", "## 固定生命预算", "",
        "以普通难度为表中比较口径；Boss维持原生FSM血量与转阶段逻辑，通过战斗生命换算调整。血量不随玩家是否携带旗帜、是否强化装备实时变化。", "",
        "|阶段|小怪/普通精英相对上一版|地图Boss相对上一版|常规锻造|锻造上限|",
        "|---|---:|---:|---:|---:|",
    ]
    for s, (mob, boss) in enumerate(zip([5, 10, 18, 25, 32, 40], [10, 16, 25, 35, 45, 60])):
        lines.append(f"|{s}|+{mob}%|+{boss}%|+{2*(s+1)}（伤害+{10*(s+1)}%）|+{5*(s+1)}（伤害+{25*(s+1)}%）|")
    lines += ["", "这是阶段固定预算：前期提升较轻，后期提高较多，为可累积的锻造保留空间。普通怪的追赶成长仍单独生效；上表比较的是相同阶段条件下的新旧基础曲线。",
        "神居沿用现有重战预算和档位，其变化取决于原有生命下限，不能直接套用表中的地图倍率。",
        "旗帜针对匹配怪物：普通难度造成伤害×1.5、受到伤害×0.75；专家/大师造成×2、受到×0.5。未用旗帜倍率等量抵消血量，因此收集旗帜仍能明显降低重复刷怪压力。", "",
        "普通难度各地图Boss的整场生命预算（包括代码中的原生阶段换算；实际战斗仍受无敌、转场等机制影响）：", "",
        "|记录|阶段|整场生命预算|", "|---|---:|---:|",
    ]
    for s in combat["Stages"]:
        for b in s["Bosses"]:
            lines.append(f'|{b["Record"]}|{s["Stage"]}|{math.ceil(b["WholeLife"])}|')
    lines += ["",
        "## 骷髅魔法书", "",
        "骷髅最长180帧（正常60Hz为3秒），或累计飞行640泰拉像素后消失；两者先到者生效。240像素内寻找目标，超过480像素清除追踪。仍可穿透，最多3次命中，同目标局部命中冷却20帧。",
        "三次命中计入同一次施法的输出预算，追踪与穿透额外计入0.8效用系数。时间/路程到限后先销毁，再处理伤害；额外验证了追踪边界内保留、边界外释放。", "",
        "|实测距离（游戏单位）|单发伤害|施法次数|命中次数|时长|实测DPS|", "|---|---:|---:|---:|---:|---:|",
    ]
    for r in weapons["Live"]:
        if r["Type"] == "BookofSkulls":
            lines.append(f'|{r["Distance"]:g}|{r["Damage"]}|{r["Casts"]}|{r["Hits"]}|{r["Seconds"]:.2f}s|{r["Dps"]:.1f}|')
    lines += ["", "## 各阶段四职业固定靶输出", "",
        "以下为游戏内攻击路径实测。固定目标、起始满蓝、阶段护甲和饰品由现有掉落回归夹具设定；测试期间没有每帧补满魔力，缺蓝影响也计入结果。它用于比较命中和输出，不代表移动Boss的完整战斗时间。", "",
        "|阶段|职业|武器|实测DPS|", "|---|---|---|---:|",
    ]
    for r in loot["combat"]:
        lines.append(f'|{r["Stage"]}|{["近战", "远程", "魔法", "召唤"][r["Class"]]}|{name(r["Weapon"])}|{r["Dps"]:.1f}|')
    lines += ["", "## 验证范围", "",
        f'- 成长与掉落结构：{combat["Checks"]}项，0失败；包括逐阶段旧掉落集合保留、无越阶、组概率一致、70/30份额及同期配方链。',
        f'- 掉落回归：{loot["summary"]["Checks"]}项，0失败；包含实际抽取、数量采样、四职业权重以及各阶段固定靶。',
        f'- 制作链回归：{craft["Checks"]}项，0失败。',
        f'- 武器回归：{weapons["Checks"]}项，0失败；包含全武器预算和重点武器固定靶。',
        '- 原版配方的实际制作、材料扣除及环境限制结果见本次 `AutoTest.log`。',
        f'- 与泰拉瑞亚原版数据独立比对：{vanilla["checked"]}条原版物品配方，0差异；另有{len(vanilla["custom"])}条自定义物品配方。',
        '- 叶绿、蘑菇矿、夜明锭、星璇武器与护甲额外在阶段0持有材料时实际合成并扣料；同时验证缺材料或缺工作站不能制作。',
        '- 隔离运行完成、无超时；正式玩家存档无变化，设置已恢复。',
        '- 本次游戏运行插件 SHA-256：`' + loot["pluginSha256"] + '`。',
        '- 未覆盖：所有真实Boss的整场通关、移动靶命中率、全部锻造/重铸/旗帜组合的实际战斗。下表强化输出属于模型估算。', "",
        "## 全武器阶段与强化输出", "",
        "模型按该阶段标准防御计算；不包含整套护甲、饰品、药水、弹药和移动命中率的完整叠加。重铸取可用前缀中的最高模型输出；常规锻造取每阶段2级，满锻造取每阶段5级，均先重铸再锻造。旗帜倍率只在匹配目标时另行应用。", "",
        "|阶段|武器|基础伤害|裸装DPS|最佳重铸|重铸＋常规锻造|重铸＋满锻造|", "|---|---|---:|---:|---:|---:|---:|",
    ]
    if args.godhome_run:
        godrun = args.godhome_run.resolve()
        godresult = json.loads((godrun / "result.json").read_text(encoding="utf-8-sig"))
        god = json.loads((godrun / "saves/godhome-balance.json").read_text(encoding="utf-8-sig"))
        assert godresult["Completed"] and not godresult["Failures"] and not godresult["TimedOut"]
        assert godresult["RestoredSettings"] and not godresult["ChangedPlayerSaves"] and not godresult["NewPlayerSaves"]
        assert god["Failures"] == 0 and len(god["ActualScene"]) > 0
        index = lines.index("## 验证范围") + 2
        lines.insert(index, f'- 神居数值及真实格林场景接线：{god["Checks"]}项，0失败；等待入场激活后核对生命、防御、伤害及原生HP。证据：`{godrun.relative_to(root)}`。本次仅修正该测试的入场等待，未调整神居档位配置。')
    for s in combat["Stages"]:
        for r in s["Weapons"]:
            lines.append(f'|{r["Stage"]}|{r["Name"]}|{r["Damage"]}|{r["Bare"]:.1f}|{r["Reforged"]:.1f}|{r["Routine"]:.1f}|{r["MaxForge"]:.1f}|')
    path = root / "docs/战斗与掉落平衡调整-2026-10-17.md"
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(path)


if __name__ == "__main__":
    main()
