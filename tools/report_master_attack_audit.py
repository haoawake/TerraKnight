"""Summarize isolated Master attack measurements; ideal budget time is not a clear time."""
import argparse
import csv
import json
from pathlib import Path

CLASSES = ["近战", "远程", "魔法", "召唤"]
BOSSES = {"FalseKnight": "假骑士", "Hornet": "一战大黄蜂", "MantisLord": "螳螂领主",
          "BlackKnight": "守望者骑士", "MegaBeamMiner": "水晶守卫", "InfectedKnight": "残破容器",
          "Hornet@4": "二战大黄蜂", "FinalBoss": "普通辐光"}


def load(path, forged):
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    assert data["MasterAudit"] and data["FixedTarget"] and not data["RefilledMana"]
    assert bool(data["Forging"]) == forged and not data["Failures"]
    rows = data["Profiles"]
    assert rows and all(r["Difficulty"] == "Master" for r in rows)
    assert all(r["BurstDps"] > 0 and r["WholeBossLife"] > 0 for r in rows)
    return data, rows


def num(value):
    return "无持续输出" if value is None else f"{value:.1f}"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("baseline", type=Path)
    parser.add_argument("forged", type=Path)
    parser.add_argument("out", type=Path)
    parser.add_argument("--variants", type=Path)
    args = parser.parse_args()
    base_data, base = load(args.baseline, False)
    forge_data, forged = load(args.forged, True)
    variants = load(args.variants, False)[1] if args.variants else []
    assert len(base) == 44 and sum(r["Entry"] for r in base) == 20
    keys = lambda r: (r["Stage"], r["Class"], r["Entry"])
    by_key = {keys(r): r for r in base}
    assert len(by_key) == 44
    args.out.mkdir(parents=True, exist_ok=True)
    flat = []
    for rows, label in [(base, "无锻造"), (variants, "无锻造替代搭配"), (forged, "满锻造抽测")]:
        for row in rows:
            flat.append({"测试": label, "阶段": row["Stage"], "装备阶段": row["GearStage"],
                         "职业": CLASSES[row["Class"]], "跨阶段": row["Entry"],
                         "Boss": BOSSES.get(row["Representative"], row["Representative"]),
                         "血量": row["WholeBossLife"], "防御": row["Defense"], "武器": row["Weapon"],
                         "召唤杖": row.get("Summon"), "爆发DPS": row["BurstDps"],
                         "持续DPS": row["SustainedDps"], "理想爆发血池秒": row["IdealBurstBudgetSeconds"],
                         "理想持续血池秒": row["IdealSustainedBudgetSeconds"],
                         "魔力开始": row["ManaBefore"], "魔力结束": row["ManaAfter"],
                         "武器锻造": row.get("WeaponForge", 0), "召唤杖锻造": row.get("SummonForge", 0)})
    with (args.out / "大师输出明细.csv").open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=flat[0].keys())
        writer.writeheader()
        writer.writerows(flat)
    text = ["# 大师各阶段攻击力测试", "", "## 测试口径", "",
            "使用固定原生敌人作为命中载体，接入对应Boss的大师防御与血量计算。每组记录前6秒爆发及其后持续输出，魔法总计20秒，其他职业12秒。",
            "无药水、无词缀、五饰品栏；魔力不回填、弹药实际消耗。无锻造基线覆盖6阶段×4职业，以及5组跨阶段门槛×4职业，共44组。",
            "同阶段成型配装可包含该阶段奖励；跨阶段组使用上一阶段装备并排除目标Boss自身奖励。装备来源受阶段筛选，未模拟逐件刷取过程。",
            "满锻造只抽测基线中各阶段较强组合和可疑门槛；它是强化上限压力测试，不代表玩家一定负担得起满级锻造。",
            "固定目标的血池/DPS仅反映持续命中压力，不包含Boss移动、无敌、阶段切换和躲避损失，不能当成实际击杀时间。此次没有穷尽所有武器、词缀、锻造和增益组合。", "",
            "## 同阶段成型配装：已测组合的最高持续DPS", "",
            "| 阶段 | 目标 | 血量 | 近战 | 远程 | 魔法 | 召唤 |", "| --- | --- | ---: | ---: | ---: | ---: | ---: |"]
    for stage in range(6):
        group = [max([r for r in base + variants if r["Stage"] == stage and r["Class"] == cls and not r["Entry"]], key=lambda r: r["SustainedDps"]) for cls in range(4)]
        assert len(group) == 4
        text.append(f"| {stage} | {BOSSES[group[0]['Representative']]} | {group[0]['WholeBossLife']} | " +
                    " | ".join(num(r["SustainedDps"]) for r in group) + " |")
    text += ["", "## 跨阶段守门战：理想持续命中血池秒数", "",
             "数值越小，持续命中下打空血池越快；这不是实际战斗时长。", "",
             "| 进入阶段 | 装备阶段 | 目标 | 近战 | 远程 | 魔法 | 召唤 |", "| --- | --- | --- | ---: | ---: | ---: | ---: |"]
    for stage in range(1, 6):
        group = sorted([r for r in base if r["Stage"] == stage and r["Entry"]], key=lambda r: r["Class"])
        assert len(group) == 4
        text.append(f"| {stage} | {group[0]['GearStage']} | {BOSSES[group[0]['Representative']]} | " +
                    " | ".join(num(r["IdealSustainedBudgetSeconds"]) for r in group) + " |")
    if variants:
        text += ["", "## 替代武器及鞭子搭配复核", "", "同样的防御和配装，只更换武器或鞭子。小目标对群攻落点分散武器不利，因此补测追踪／集中法术；召唤补测旧爆竹鞭与后期仆从。", "",
                 "| 阶段 | 职业 | 武器 | 仆从杖 | 持续DPS | 理想持续血池秒 |", "| --- | --- | --- | --- | ---: | ---: |"]
        for row in variants:
            text.append(f"| {row['Stage']} | {CLASSES[row['Class']]} | {row['Weapon']} | {row.get('Summon') or '—'} | {row['SustainedDps']:.1f} | {num(row['IdealSustainedBudgetSeconds'])} |")
    text += ["", "## 满锻造抽测", "", "| 阶段／门槛 | 职业 | 武器 | 锻造等级 | 持续DPS：基线→满锻造 | 理想持续血池秒 |",
             "| --- | --- | --- | --- | ---: | ---: |"]
    for row in forged:
        matching = [r for r in base + variants if keys(r) == keys(row) and r['Weapon'] == row['Weapon'] and r.get('Summon') == row.get('Summon')]
        assert matching, f"Missing matching un-forged baseline: {row}"
        old = matching[0]
        level = f"{row.get('WeaponForge', 0)}"
        if row.get("Summon"):
            level += f"；杖{row.get('SummonForge', 0)}"
        text.append(f"| {row['Stage']}{'（跨阶段）' if row['Entry'] else ''} | {CLASSES[row['Class']]} | {row['Weapon']} | {level} | "
                    f"{old['SustainedDps']:.1f} → {row['SustainedDps']:.1f} | {num(row['IdealSustainedBudgetSeconds'])} |")
    risks = [r for r in flat if (r["理想爆发血池秒"] or 1e9) < 15 or (r["理想持续血池秒"] or 1e9) < 30]
    text += ["", "## 筛查标记", "",
             "仅作人工复核触发线：爆发血池不足15秒或持续血池不足30秒，标记为输出偏快候选，不直接判定实战失衡。", ""]
    if risks:
        for row in risks:
            text.append(f"- {row['测试']} 阶段{row['阶段']} {row['职业']} {'跨阶段' if row['跨阶段'] else '同阶段'}："
                        f"爆发血池{num(row['理想爆发血池秒'])}秒、持续血池{num(row['理想持续血池秒'])}秒。")
    else:
        text.append("本次测到的组合均未触发这两条偏快复核线。")
    text += ["", "## 原始证据", "", f"- 基线：`{args.baseline}`", f"- 替代搭配：`{args.variants}`", f"- 满锻造：`{args.forged}`",
             "- 同目录保存CSV明细；运行结果中的存档恢复状态另行核对。", ""]
    (args.out / "大师输出测试报告.md").write_text("\n".join(text), encoding="utf-8")
    summary = {"BaselineProfiles": len(base), "ForgedProfiles": len(forged), "FastCandidates": risks}
    (args.out / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False))


if __name__ == "__main__":
    main()
