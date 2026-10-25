"""Document the reviewed loadout routes using actual staged collection/crafting evidence."""
import argparse
import json
import re
from pathlib import Path


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('run', type=Path)
    ap.add_argument('assets', type=Path)
    ap.add_argument('--source', type=Path, required=True)
    ap.add_argument('--output', type=Path, required=True)
    args = ap.parse_args()
    result = json.loads((args.run/'result.json').read_text(encoding='utf-8-sig'))
    assert result['Completed'] and not result['Failures'] and not result['TimedOut']
    evidence = json.loads((args.run/'saves/craft-progression-audit.json').read_text(encoding='utf-8-sig'))
    assert evidence['Failures'] == 0
    actual = json.loads((args.assets/'saves/wiki-assets/runtime-data.json').read_text(encoding='utf-8-sig'))
    progression = json.loads((args.run/'saves/progression-audit.json').read_text(encoding='utf-8-sig'))
    items = actual['items']
    stages = {s['Stage']:s for s in evidence['Routes'] if 'Targets' in s}
    def name(t):
        return f"{items[t]['Name']}（`{t}`）"
    def sources(t, stage):
        source = progression['stages'][stage]
        rows = [rec for rec, drops in source['Drops'].items() if any(d['Type']==t for d in drops)]
        shops = [s['Npc'] for s in source['Shops'] if s['Type']==t]
        return '、'.join([f'`{k}`' for k in rows[:5]] + [f'{k} 商店' for k in shops]) or '由下列原版配方加工'
    lines = ['# 阶段制作链核验', '', '当前固定构建的实际掉落抽取、可达源闭包及游戏内合成结果。配方的材料、数量、制作站和环境保持泰拉瑞亚原版，修改的是模组的获取阶段与来源。', '',
             '- 阶段 2：水晶山峰的结晶躯壳提供狱石、黑曜石及地狱熔炉；商人也提供同期地狱熔炉。熔炼 17 个狱石锭后，可在砧制作小鬼法杖。',
             '- 阶段 3：矿区/城市提供秘银和山铜矿，深巢提供蜘蛛牙；先造秘银砧或山铜砧，再按原版配方制作蜘蛛杖、蜘蛛女王法杖及蜘蛛套。',
             '- 同期修正附魔回旋镖、恶魔弓、黄蜂法杖、鹰身女妖护符、强力手套、熔火之怒、凤凰爆破枪和黑曜石套的前置来源。',
             '- 炽焰巨剑位于阶段 2，升级成永夜刃位于阶段 3，避免升级成品早于前置武器。',
             '- 工匠作坊阶段 1、地狱熔炉阶段 2、秘银/山铜砧阶段 3；精金/钛金及叶绿等阶段 4 高级材料仍保留对应首领门槛。',
             '- 饰品的原有效果和数值保持不变；饰品总掉率及单件数量保护保留。', '', '## 收集与实际合成', '',
             '| 路线 | 阶段 | 收集轮数 | 平均击杀数 | 95% 所需击杀数 | 最大值 |', '|---|---:|---:|---:|---:|---:|']
    for r in evidence['Routes']:
        if 'Route' in r:
            label = '小鬼法杖：51 狱石＋17 黑曜石＋1 地狱熔炉' if r['Route']=='Imp' else '蜘蛛整套＋蜘蛛杖＋蜘蛛女王法杖：76 蜘蛛牙＋40 秘银矿（含制作秘银砧）'
            lines.append(f"| {label} | {r['Stage']} | {r['Trials']} | {r['MeanKills']:.1f} | {r['P95']} | {r['Maximum']} |")
    lines += ['', '使用普通难度、同期小怪的真实掉落规则随机抽取；未通过后期 Boss、钓鱼匣或测试赠送补充材料。击杀数为模拟抽取的收集成本，不包含跑图时间；实际合成使用摆在角色附近的实物制作站。', '', '## 关键材料与制作站', '',
              '| 物品 | 阶段 | 同期来源 |', '|---|---:|---|']
    for t in ['WoodenBoomerang','BeeWax','DemoniteOre','CrimtaneOre','Amber','TinkerersWorkshop','Hellstone','Obsidian','Hellforge','TitanGlove','MythrilOre','OrichalcumOre','MythrilAnvil','OrichalcumAnvil','SpiderFang']:
        stage = items[t]['Stage']
        lines.append(f'| {name(t)} | {stage} | {sources(t,stage)} |')
    for stage in range(6):
        stock = set(stages[stage]['Reachable'])
        lines += ['', f'## 阶段 {stage} 成长装备', '', '| 装备 | 物品阶段 | 最早配方阶段 | 原版制作站 | 配方材料 | 同期完整链 |', '|---|---:|---:|---|---|---|']
        for t in stages[stage]['Targets']:
            assert t in stock and items[t]['Stage']<=stage, (stage,t)
            recipes = [r for r in actual['recipes'] if r['Result']==t]
            recipe = min(recipes, key=lambda r:r['StationStage']) if recipes else None
            if recipe and items[t]['Source']=='Craft':
                assert recipe['StationStage']<=stage, (stage,t,recipe)
            ingredients = '＋'.join(f"{name(i['Type'])} ×{i['Count']}" for i in recipe['Ingredients']) if recipe else '直接掉落／商店；非制作物品'
            lines.append(f"| {name(t)} | {items[t]['Stage']} | {recipe['StationStage'] if recipe else '—'} | {recipe['Station'] if recipe else '—'} | {ingredients} | 通过 |")
        lines += ['', '直接掉落物品的可选回收或转换配方可能较晚，不会延迟其本体的获取；制作型成长装备则必须在本阶段完成材料与制作站闭包。']
    lines += ['', '## 验证证据', '', f"- 游戏内阶段检查：{evidence['Checks']}/{evidence['Checks']} 通过。", f'- 运行目录：`{args.run.as_posix()}`。',
              '- 同一套件另逐条执行原版配方制作、材料扣除与环境限制检查；全部结果见该目录 `AutoTest.log`。',
              '- 本报告的可达检查针对六阶段四职业成长装备；收集实测针对上述两条反馈路线，不冒充逐格跑通整个地图。', '']
    args.output.write_text('\n'.join(lines), encoding='utf-8')
    print(f'Wrote {args.output}; all six staged loadout routes verified.')


if __name__ == '__main__':
    main()
