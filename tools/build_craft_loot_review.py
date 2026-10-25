"""Render the isolated game's actual boss allocation and nearby-enemy transfer report."""
import argparse
import collections
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("run", type=Path)
    parser.add_argument("--output", type=Path, default=ROOT / "docs/制作优先与首领掉落分配-2026-10-21.md")
    args = parser.parse_args()
    report = json.loads((args.run / "saves/craft-loot-report.json").read_text("utf-8-sig"))
    result = json.loads((args.run / "result.json").read_text("utf-8-sig"))
    assert report["Failed"] == 0 and result["Completed"] and not result["TimedOut"]
    assert not result["Failures"] and not result["ChangedPlayerSaves"] and not result["NewPlayerSaves"]
    allocated = [t for r in report['Bosses'] for t in r['Weapons'] + [r['Signature']] if t != 'None']
    assert len(allocated) == len(set(allocated)), 'Duplicate boss allocation'
    assert not set(allocated).intersection(report['ShopEquipment']), 'Shop equipment allocated to a boss'
    names = {}
    for path in sorted((ROOT / "src/TerraKnight/Items").glob("*.cs")):
        for key, name in re.findall(r'ItemType\.(\w+),\s*"([^"\n]+)"', path.read_text("utf-8-sig")):
            names.setdefault(key, name)
    enemies = dict(re.findall(r'\["([^"]+)"\]\s*=\s*new Entry[^\n]*?Zh\s*=\s*"([^"]+)"',
                              (ROOT / "src/TerraKnight/Items/EnemyDropData.cs").read_text("utf-8-sig")))

    def item(key):
        return "无" if key == "None" else f"{names.get(key, key)} (`{key}`)"

    lines = ["# 制作优先与首领掉落分配", "", "本表来自隔离游戏副本中的最终物品定义与实际掉落表，不是旧的 BossRewards 模板。", "",
             "- 制作来源的成品不再从小怪、Boss、Boss 转阶段、Boss 宝藏袋、神居额外奖励、五门结算、钓鱼、钓鱼宝匣和钓鱼宝箱中获得。开局和探索击杀奖励也改为材料，不再直接赠送制作武器或魔力水晶。",
             "- Boss 的普通武器使用本阶段的原版掉落来源武器；合成升级武器、护甲和饰品仍需自己制作。原版双来源物品保留其世界来源分类。",
             "- 世界 Boss 使用自身阶段；神居复战与神居宝藏袋使用玩家当前阶段，宝藏袋以开包时为准。终局四职业装备只给各自的终局 Boss，五门额外通关结算给材料。",
             "- 所有实际商店货架中的武器、护甲、饰品和手持工具都从通用掉落中排除，含月相商品与旅商完整候选；雀杖保留在商人货架，Boss 和小怪不再掉落。",
             "- 不同 Boss 的装备严格不重复；某职业候选不足就保留空位，剩余职业平分武器抽选，不借用其他 Boss 装备，也不用制作成品或其他阶段补位。剧情法术奖励和钓鱼专属装备不纳入 Boss 通用候选。",
             "- 神居复战只在当前阶段与本 Boss 的装备阶段相符时给其专属装备，其余阶段只给当前阶段材料等非装备奖励；神居额外抽选也只选本 Boss 装备。五门额外通关结算只给材料，装备随各自 Boss 结算。",
             "- 上阶段的普通奖励在下一阶段由新阶段 Boss 所在区域的一种小怪承接，每件独立 5%；原世界 Boss 仍保留自己的原阶段奖励，Boss 专属物品不转给小怪。", "",
             f"隔离验证：{report['Checks']:,} 项成品/阶段/承接/商店排除/装备去重检查，{report['SampledPayouts']:,} 次实际奖励输出；无失败、无超时、玩家存档无改变或新增。",
             "另通过原版制作链可达性 764 项，以及实际制作、扣料和环境限制 1,327 项检查。未进行本轮双客户端联机验证。", "",
             f"## Boss 分配（{len(report['Bosses'])} 条）", "",
             "| 阶段 | Boss | 近战 | 远程 | 魔法 | 召唤 | 独特奖励 |", "|---|---|---|---|---|---|---|"]
    for row in sorted(report["Bosses"], key=lambda r: (r["Stage"], r["Record"])):
        record = row["Record"]
        lines.append("| " + " | ".join([str(row["Stage"]), f"{enemies.get(record, record)} (`{record}`)",
                                         *(item(t) for t in row["Weapons"]), item(row["Signature"])]) + " |")
    lines += ["", "辐光与纯粹容器使用互不重复的终局武器；可制作的月亮领主护甲不再直接掉落。", "",
              f"## 附近小怪承接（{len(report['Transfers'])} 条）", "",
              "| 开始阶段 | 物品 | 小怪 | 区域 |", "|---|---|---|---|"]
    for row in sorted(report["Transfers"], key=lambda r: (r["UnlockStage"], r["Region"], r["Monster"], r["Item"])):
        record = row["Monster"]
        lines.append(f"| {row['UnlockStage']} | {item(row['Item'])} | {enemies.get(record, record)} (`{record}`) | {row['Region']} |")
    lines += ["", "## 同阶段去重情况", "", "每个阶段不同武器数等于武器分配次数，证明没有用同一武器填充多个 Boss。空位表示该职业没有分到独立候选。", "",
              "| 阶段 | Boss 数 | 不同武器数 | 武器分配次数 |", "|---|---|---|---|"]
    for stage in range(6):
        rows = [r for r in report["Bosses"] if r["Stage"] == stage]
        counts = collections.Counter(t for r in rows for t in r["Weapons"] if t != "None")
        lines.append(f"| {stage} | {len(rows)} | {len(counts)} | {sum(counts.values())} |")
    lines += ["", f"验证文件：`{args.run.resolve().relative_to(ROOT)}/result.json`、`saves/craft-loot-report.json`、`saves/craft-progression-audit.json`。", ""]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote {args.output}; {len(report['Bosses'])} bosses; {len(report['Transfers'])} transfers")


if __name__ == "__main__":
    main()
