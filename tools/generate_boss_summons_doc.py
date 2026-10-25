"""Generate the complete current seal/material/source table from BossSummons.cs."""
import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROFILE = re.compile(
    r'S\(ItemType\.(\w+), "([^"]+)", "([^"]+)", "([^"]+)", (\d), '
    r'ItemType\.(\w+), (\d+), "([^"]+)", ItemType\.(\w+), (\d+), "([^"]+)", "([^"]+)"(, true)?\)')
NAMES = {
    'IronOre': '铁矿石', 'SilverOre': '银矿石', 'Gel': '凝胶', 'Lens': '晶状体',
    'Daybloom': '太阳花', 'Deathweed': '死亡草', 'Vine': '藤蔓', 'Cobweb': '蛛网',
    'JungleSpores': '丛林孢子', 'Moonglow': '月光草', 'FlinxFur': '小雪怪皮毛',
    'Bone': '骨头', 'Obsidian': '黑曜石', 'Stinger': '毒刺', 'Book': '书',
    'Coral': '珊瑚', 'Waterleaf': '幌菊', 'Diamond': '钻石', 'Ruby': '红玉',
    'SturdyFossil': '坚固化石', 'DemoniteOre': '魔矿', 'BeeWax': '蜂蜡',
    'TissueSample': '组织样本', 'SpiderFang': '蜘蛛牙', 'Fireblossom': '火焰花',
    'Ichor': '灵液', 'PixieDust': '妖精尘', 'SoulofFlight': '飞翔之魂',
    'CursedFlame': '诅咒焰', 'ShadowScale': '暗影鳞片', 'SoulofNight': '暗影之魂',
    'CobaltOre': '钴矿', 'PalladiumOre': '钯金矿',
}


def profiles():
    source = (ROOT / 'src/TerraKnight/Items/BossSummons.cs').read_text(encoding='utf-8-sig')
    roster_source = (ROOT / 'src/TerraKnight/Items/EnemyDropData.cs').read_text(encoding='utf-8-sig')
    roster = dict(re.findall(r'\["([^"]+)"\] = new Entry \{ En = "[^"]+", Zh = "([^"]+)"', roster_source))
    rows = []
    for item, key, zh, en, stage, mat, amount, mob, support, count, other, area, dream in PROFILE.findall(source):
        assert mat in NAMES and support in NAMES
        assert key in roster and mob in roster and other in roster
        rows.append(dict(item=item, key=key, name=zh, en=en, boss=roster[key], stage=int(stage),
                         dream=bool(dream), locality=area,
                         materials=[dict(type=mat, name=NAMES[mat], count=int(amount), source=mob, source_name=roster[mob]),
                                    dict(type=support, name=NAMES[support], count=int(count), source=other, source_name=roster[other])]))
    assert len(rows) == 40 and len({r['item'] for r in rows}) == 40
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--audit', type=Path, help='Optional successful isolated boss-recipe-audit.json')
    args = parser.parse_args()
    rows = profiles()
    audit = None
    if args.audit:
        audit = json.loads(args.audit.read_text(encoding='utf-8-sig'))
        assert audit['Failures'] == 0 and len(audit['Seals']) == 40
        for row in rows:
            observed = next(r for r in audit['Seals'] if r['Key'] == row['key'])
            assert observed['Stage'] == row['stage']
            assert {(p['Material'], p['Count'], p['Source']) for p in observed['Proofs']} == {
                (m['type'], m['count'], m['source']) for m in row['materials']}
            assert all(p['Available'] and p['Repeatable'] for p in observed['Proofs'])
    text = [
        '# Boss 召唤物：当地材料与制作清单\n\n',
        '2026-10-16。以下 40 张配方直接取自当前 `BossSummons.cs`，每份在砧制作 1 个。阶段沿用现有 0–5 级探索进度。\n\n',
        '全部取消旧的统一玻璃、石块底料，改为两种附近小怪实际掉落的材料；不使用 Boss 专属魂、神圣锭或天界碎片作为底料。材料可由常规敌人、可重复前置试炼取得，不依赖已退场的格林眷族或 Boss 召出的临时小怪。\n\n',
        '蜂蜡的材料解锁从阶段 4 对齐到蜂巢骑士所在的阶段 3，使蜂巢士兵能在该阶段提供配方所需蜂蜡。其他原版物品配方的材料与数量没有改动。\n\n',
        '梦境变体采用现世本体周围的敌人；钉师采用住所附近敌人。神居纯粹容器没有常驻小怪，采用神居入口垃圾坑的皇家水道敌人；德特茅斯的剧团、灰王与斯莱采用镇外悬崖或十字路井口。奥罗与马托分别对应王国边缘与呼啸悬崖。\n\n',
        '原有使用规则保留：先在旅途中击败对应 Boss 一次，再用召唤物发起重战；梦境型仍需梦之钉；成功进入战斗消耗 1 个，无效使用不消耗。此表只重做材料，不改变战斗入口。\n\n',
        '| 序号 | Boss／召唤物 | 阶段 | 材料一及来源 | 材料二及来源 | 对应周边区域 |\n',
        '|---:|---|---:|---|---|---|\n',
    ]
    for index, row in enumerate(rows, 1):
        a, b = row['materials']
        text.append(f"| {index} | {row['boss']}／{row['name']} | {row['stage']} | {a['name']} ×{a['count']} ← {a['source_name']} | {b['name']} ×{b['count']} ← {b['source_name']} | {row['locality']} |\n")
    if audit:
        text.append(f"\n隔离游戏验证 {audit['Checks']}/{audit['Checks']} 项通过。每张配方逐项验证解锁阶段至阶段 5 的重复击杀掉落，并用普通难度实际掉落逻辑模拟 200 次收集。下表是每份材料的击杀数，未计算走路、怪物刷新和战斗耗时，也不等于每次都能在该击杀数内收齐。\n\n")
        text.append('| Boss | 平均击杀数 | 95% 样本所需击杀数 |\n|---|---:|---:|\n')
        for row in rows:
            observed = next(r for r in audit['Seals'] if r['Key'] == row['key'])
            cost = observed['Collection']
            text.append(f"| {row['boss']}（{row['name']}） | {cost['AverageKills']:.1f} | {cost['P95Kills']} |\n")
        text.append(f'\n验证数据：`{args.audit.resolve().relative_to(ROOT).as_posix()}`。\n')
    else:
        text.append('\n此表为当前配方与作者指定来源；未附运行时验证数据。使用 `--audit` 关联成功的隔离测试。\n')
    (ROOT / 'Boss召唤物机制与制作清单.md').write_text(''.join(text), encoding='utf-8')
    out = ROOT / 'artifacts/boss-local-recipes'
    out.mkdir(parents=True, exist_ok=True)
    (out / 'recipes.json').write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'Generated {len(rows)} complete local-material recipes and sources.')


if __name__ == '__main__':
    main()
