"""Export the complete staged loot routes from the isolated runtime export."""
import argparse
import hashlib
import json
import re
from collections import defaultdict
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('runtime', type=Path)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    data = json.loads(args.runtime.read_text(encoding='utf-8-sig'))
    items, stages = data['items'], data['drop_stages']
    route = (args.source/'src/TerraKnight/Items/BestiaryRouteData.cs').read_text(encoding='utf-8-sig')
    names = re.findall(r'"([^"]+)"', route.split('areaNames =', 1)[1].split('};', 1)[0])
    groups = re.findall(r'new\[\] \{([^}]+)\}', route.split('areaRecords =', 1)[1])
    regions = {record: area for area, group in zip(names, groups) for record in re.findall(r'"([^"]+)"', group)}
    main_areas = {'国王山道', '遗忘十字路', '苍绿之径', '真菌荒地', '泪水之城', '水晶山峰', '远古盆地', '王国边缘', '深巢', '深渊', '黑卵神殿'}
    loot = (args.source/'src/TerraKnight/Items/ProgressionLoot.cs').read_text(encoding='utf-8-sig')
    balance = (args.source/'src/TerraKnight/Items/ProgressionBalanceData.cs').read_text(encoding='utf-8-sig')
    bosses = set(re.findall(r'\["([^"]+)"\] = new BossReward', balance)) - {'Zote', 'BigBuzzer', 'MageKnight', 'PaleLurker'}
    elite_names = set(re.findall(r'"([^"]+)"', loot.split('Elites = new HashSet<string>', 2)[-1].split('};', 1)[0])) | {'BigBuzzer', 'MageKnight', 'PaleLurker'}
    lines = ['# 全阶段掉落与主支线路线', '', '数据来自最终 DLL 的隔离游戏运行导出。区域按图鉴初遇路线归属；材料仍遵守实际场景的区域来源。', '',
             '- 普通怪饰品整池基础概率 2%，精英 5%；先判定，再等概率 N 选 1。候选数不会增加总概率。',
             '- Boss 首杀饰品整池 100%，重复击杀 20%，一次最多一件。难度奖励倍率另算。',
             '- 旧区域随进度升级装备；高阶区域不掉低阶装备。旧装备合成前置持续保留在低阶主线来源。',
             '- 主线承担四职业常规成长和合成前置；支线提供特殊变体，蜂巢、深巢、水晶、丛林等主题装备优先归属对应区域。',
             '- 魂、矿石、Boss 召唤材料保留区域用途，不把仍参与合成的材料当作过时装备删除。', '']
    checks = []
    for stage, records in stages.items():
        lines += [f'## 阶段 {stage}', '', '| 区域 | 路线 | 怪物 | 饰品整池概率 | 装备与饰品 | 材料及其他掉落 |', '|---|---|---|---|---|---|']
        for record, rows in sorted(records.items(), key=lambda pair: (names.index(regions[pair[0]]) if regions.get(pair[0]) in names else 999, pair[0])):
            if not rows:
                continue
            accessory = [r for r in rows if items[r['Type']]['Accessory'] != 'None']
            if accessory:
                expected = 1 if record in bosses else .05 if record in elite_names else .02
                total = sum(r['Probability'] for r in accessory)
                passed = all(r['Group'] == 103 and r['Min'] == r['Max'] == 1 for r in accessory) and abs(total-expected) < .00001
                checks.append({'Stage': int(stage), 'Record': record, 'Candidates': len(accessory), 'PoolProbability': total, 'Pass': passed})
            columns = [[], []]
            for row in rows:
                item = items[row['Type']]
                equipment = item['Accessory'] != 'None' or item['Defense'] > 0 or item['Damage'] > 0 and item['MaxStack'] == 1
                count = str(row['Min']) if row['Min'] == row['Max'] else f"{row['Min']}–{row['Max']}"
                name = item['Name'].replace('|', '/')
                columns[0 if equipment else 1].append(f"{name}（{row['Type']}）×{count} / {row['Probability']:.3%}" + (' / 大师' if row['Master'] else ''))
            area = regions.get(record, '其他')
            rate = sum(r['Probability'] for r in accessory)
            lines.append(f"| {area} | {'主线' if area in main_areas else '支线'} | {record} | {rate:.2%} / {len(accessory)} 选 1 | {'<br>'.join(columns[0]) or '—'} | {'<br>'.join(columns[1]) or '—'} |")
        lines.append('')
    args.output.write_text('\n'.join(lines), encoding='utf-8')
    result = {'RuntimeSHA256': hashlib.sha256(args.runtime.read_bytes()).hexdigest(), 'Checks': len(checks), 'Failures': [c for c in checks if not c['Pass']], 'Pools': checks}
    args.output.with_suffix('.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f"Exported all {len(stages)} stages; {len(checks)} accessory pools checked; failures={len(result['Failures'])}")
    raise SystemExit(bool(result['Failures']))


if __name__ == '__main__':
    main()
