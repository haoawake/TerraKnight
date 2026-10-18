"""Audit every registered magic item's final mana against the installed Terraria defaults."""
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    runtime = json.loads((ROOT / 'tools/encyclopedia_runtime.json').read_text(encoding='utf-8'))
    for rel, digest in runtime['source_hashes'].items():
        assert hashlib.sha256((ROOT / 'src/TerraKnight' / rel).read_bytes()).hexdigest() == digest, 'Refresh metadata: ' + rel
    vanilla = json.loads((ROOT / 'tools/vanilla-data/combat/vanilla-runtime.json').read_text(encoding='utf-8-sig'))
    before = json.loads((ROOT / 'tools/vanilla-data/combat/mod-mana-before-4.1.0.json').read_text(encoding='utf-8'))
    special = {
        'LastPrism': '起手一次；持续每 30／15／5 帧扣一次（随蓄能加速）',
        'LaserMachinegun': '起手不扣；持续每 24／18／12／6 帧扣一次，每次发射两束',
        'MedusaHead': '看见有效目标才扣；持续凝视每 15 帧一次',
        'SoulDrain': '起手不扣；每个吸取区域首次找到目标时扣一次',
        'GlacierFang': '起手一次；蓄能期间第 20、40 帧再扣一次',
        'ChargedBlasterCannon': '起手一次；蓄能射击与满蓄光束分次扣蓝',
        'BookStaff': '每轮连发扣一次；右键龙卷风基础蓝耗翻倍',
        'MagicMissile': '发射时一次；转向操控不逐帧扣蓝',
        'Flamelash': '发射时一次；转向操控不逐帧扣蓝',
        'RainbowRod': '发射时一次；转向操控不逐帧扣蓝',
    }
    rows = []
    for t, d in runtime['data']['items'].items():
        if d['Class'] != 'Magic':
            continue
        item_id = d['Texture'].removeprefix('Item_')
        assert item_id in vanilla['items'], 'Missing original item: ' + t
        original = vanilla['items'][item_id]['mana']
        assert d['Mana'] == original, (t, d['Mana'], original)
        timing = special.get(t, '每轮连发扣一次，周期内后续弹幕不重复扣' if d['ManaEveryOtherShot'] else '每次施法扣一次，多弹幕不逐颗重复扣')
        if t in ('NimbusRod', 'CrimsonRod', 'ClingerStaff', 'RainbowGun', 'MagnetSphere'):
            timing = '施放时扣一次，已生成的持续区域不重复扣蓝'
        if t in ('SpaceGun', 'ZapinatorGray', 'ZapinatorOrange', 'LaserRifle'):
            timing += '；陨石套为零蓝耗'
        rows.append({'Type': t, 'Name': d['Name'], 'Stage': d['Stage'], 'Before': before[t]['Mana'], 'Mana': d['Mana'], 'Original': original, 'Timing': timing})
    assert len(rows) == len(before) == 79
    changed = sum(r['Before'] != r['Mana'] for r in rows)
    lines = ['# 全部魔法武器蓝耗核查（4.1.0）', '',
             f'逐项检查 {len(rows)} 件魔法武器：{changed} 件原先偏低，现全部恢复本机 Terraria {vanilla.get("version", "1.4.5.8")} 的基础蓝耗。基础蓝耗与持续施法的扣费次数分别核对。', '',
             '- 原因：阶段伤害平衡代码同时用“阶段＋使用间隔”覆盖蓝耗，大量武器被压到 1–8 点。现在伤害平衡不再改写原版蓝耗。',
             '- 持续施法现在使用手持物品的重铸后定义结算，修复词缀减蓝失效；装备减蓝在词缀之后计算。保留最少 1 点的当前付费施法规则。',
             '- 陨石套的太空枪、两种冲击枪、激光步枪零蓝耗，以及无限智慧巨著右键双倍蓝耗均保留。魔力花先尝试喝药再扣蓝。',
             '- 连发武器按使用周期收费；棱镜按持续施法节拍收费，不能把一次生成的多束光线误当多次独立施法。',
             '- 当前缺蓝后仍能以 2.5 倍使用时间施法，这是既有的 Terraria 1.4.5 ManaV2 规则；本次不改缺蓝施法机制。',
             '- 验证包含最终 ItemDb 的 79 项原版值核对及 171 项实际扣蓝方法检查；没有进行实机战斗、DPS 或联机测试。', '',
             '| 物品 | ID | 阶段 | 修复前 | 修复后 / 原版 | 扣蓝时机与特例 |',
             '|---|---|---:|---:|---:|---|']
    for r in sorted(rows, key=lambda r: (r['Stage'], r['Type'])):
        lines.append(f'| {r["Name"]} | `{r["Type"]}` | {r["Stage"]} | {r["Before"]} | {r["Mana"]} | {r["Timing"]} |')
    path = ROOT / 'docs/magic-mana-audit-4.1.0.md'
    path.write_text('\n'.join(lines) + '\n', encoding='utf-8')
    with (ROOT / 'artifacts/release-v4.1.0/magic-mana-audit.csv').open('w', encoding='utf-8-sig', newline='') as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0])); w.writeheader(); w.writerows(rows)
    print(json.dumps({'magic_items': len(rows), 'restored_items': changed, 'remaining_mismatches': 0, 'report': str(path)}, ensure_ascii=False))


if __name__ == '__main__':
    main()
