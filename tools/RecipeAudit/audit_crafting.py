"""Compare expanded vanilla recipes with a fresh isolated TerraKnight runtime export."""
import argparse
import collections
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BAR_NAMES = 'CopperBar TinBar IronBar LeadBar SilverBar TungstenBar GoldBar PlatinumBar DemoniteBar CrimtaneBar MeteoriteBar HellstoneBar CobaltBar PalladiumBar MythrilBar OrichalcumBar AdamantiteBar TitaniumBar HallowedBar ChlorophyteBar SpectreBar ShroomiteBar LunarBar'.split()
TILES = {'徒手': -1, '': -1, '工作台': 18, '砧': 16, '熔炉': 17, '地狱熔炉': 77, '秘银砧': 134, '精金熔炉': 133, '远古操纵机': 412, '织布机': 86, '工匠作坊': 114, '水晶球': 125, '书架': 101, '自动锤炼机': 247, '固化机': 220, '锯木机': 106, '玻璃瓶': 13, '灌注站': 243, '恶魔祭坛 / 猩红祭坛': 26, '染缸': 228, '骨头焊机': 300}


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def kind(item):
    if item['vanity'] or item.get("_type", "").startswith("GolfBall"):
        return None
    if item['accessory']:
        return '饰品'
    if any(item[k] >= 0 for k in ('headSlot', 'bodySlot', 'legSlot')):
        return '盔甲'
    if any(item[k] > 0 for k in ('pick', 'axe', 'hammer', 'fishingPole')):
        return '工具与钓竿'
    if item['damage'] > 0 and item['ammo'] == 0 and item['createTile'] < 0 and item['createWall'] <= 0:
        return '武器'
    return None


def expanded_ingredients(recipe, groups):
    choices = []
    for item, count in recipe['ingredients']:
        alternatives = {item}
        for group in recipe['groups']:
            allowed = set(groups[str(group)])
            if item in allowed:
                alternatives |= allowed
        choices.append((alternatives, count))
    return choices


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--mod-run', type=Path, required=True)
    ap.add_argument('--vanilla-source', type=Path, required=True)
    args = ap.parse_args()
    import sys
    sys.stdout.reconfigure(encoding="utf-8")
    result = read(args.mod_run / 'result.json')
    assert result['Completed'] and not result['Failures'] and not result['TimedOut']
    assert result['RestoredSettings'] and not result['ChangedPlayerSaves'] and not result['NewPlayerSaves']
    vanilla = read(ROOT / 'tools/vanilla-data/crafting/vanilla-runtime.json')
    assert vanilla['setupComplete'] and vanilla['recipeCount'] == len(vanilla['recipes'])
    mod = read(args.mod_run / 'saves/wiki-assets/runtime-data.json')
    ids = {int(v): k for k, v in re.findall(r'public const short (\w+) = (\d+);', (args.vanilla_source / 'Terraria.ID/ItemID.cs').read_text(encoding='utf-8-sig'))}
    text = (args.vanilla_source / 'Terraria.Localization.Content.zh-Hans.Items.json').read_text(encoding='utf-8-sig')
    zh = json.loads(re.sub(r',(\s*[}\]])', r'\1', text))['ItemName']
    def name(item):
        return zh.get(ids.get(item, ''), ids.get(item, str(item)))
    def label(item):
        return f'{name(item)} `{ids[item]}`（{item}）'

    stats = {int(i): dict(d, _type=ids.get(int(i), "")) for i, d in vanilla['items'].items()}
    by_result = collections.defaultdict(list)
    for r in vanilla['recipes']:
        by_result[r['result']].append(r)
    mod_by_id = collections.defaultdict(list)
    type_id = {}
    for t, d in mod['items'].items():
        match = re.fullmatch(r'Item_(\d+)', d['Texture'] or '')
        if match:
            item = int(match[1])
            mod_by_id[item].append(t)
            type_id[t] = item
    mod_recipes = collections.defaultdict(list)
    for r in mod['recipes']:
        if r['Result'] in type_id:
            mod_recipes[type_id[r['Result']]].append(r)
    expected = {i for i in by_result if kind(stats[i])}
    status = {i: '缺物品定义' if i not in mod_by_id else '有物品但缺制作配方' if not mod_recipes[i] else '有物品和制作配方' for i in expected}

    def matches(vr, mr):
        if vr['count'] != mr['Count']:
            return False
        vt = 13 if vr['tile'] == 355 else vr['tile']
        if TILES.get(mr['Station']) != vt:
            return False
        actual = collections.Counter()
        for ing in mr['Ingredients']:
            if ing['Type'] not in type_id:
                return False
            actual[type_id[ing['Type']]] += ing['Count']
        def assign(slots, remaining):
            if not slots:
                return not any(remaining.values())
            choices, count = slots[0]
            for item in choices:
                if remaining[item] < count:
                    continue
                rest = remaining.copy()
                rest[item] -= count
                if assign(slots[1:], rest):
                    return True
            return False
        return assign(expanded_ingredients(vr, vanilla['groups']), actual)

    compatible = {i: any(matches(vr, mr) for vr in by_result[i] for mr in mod_recipes[i]) for i in expected if mod_recipes[i]}
    dependencies = [(r['result'], set().union(*(x for x, n in expanded_ingredients(r, vanilla['groups']))) if r['ingredients'] else set()) for r in vanilla['recipes']]
    def descendants(start):
        reached = set(start)
        while True:
            before = len(reached)
            for target, ingredients in dependencies:
                if ingredients & reached:
                    reached.add(target)
            if len(reached) == before:
                return reached
    bars = {i: n for i, n in ids.items() if n in BAR_NAMES}
    bar_products = expected & descendants(bars)
    gaps = [i for i in sorted(expected) if status[i] != '有物品和制作配方']
    mismatched = [i for i in sorted(compatible) if not compatible[i]]
    records = []
    for i in sorted(expected):
        records.append(dict(id=i, type=ids[i], name=name(i), kind=kind(stats[i]), status=status[i], bar_related=i in bar_products,
                            mod_types=mod_by_id[i], vanilla_recipes=by_result[i], mod_recipes=mod_recipes[i], vanilla_ingredient_station_match=compatible.get(i)))
    output = ROOT / 'tools/vanilla-data/crafting'
    output.mkdir(exist_ok=True)
    data = dict(vanilla_items=vanilla['itemCount'], vanilla_recipes=vanilla['recipeCount'], mod_items=len(mod['items']), mod_recipes=len(mod['recipes']),
                mod_run=str(args.mod_run), records=records, bars={ids[i]: dict(id=i, products=sorted(expected & descendants({i}))) for i in bars})
    (output / 'audit.json').write_text(json.dumps(data, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    (output / 'item-names.json').write_text(json.dumps({i: dict(type=n, zh=name(i)) for i, n in ids.items()}, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')

    lines = ['# 金属锭与可制作装备完整性审计', '', ('核对日期：2026-10-15。' + ('结论：本次范围内的物品定义和制作入口已补齐。' if not gaps else '结论：仍有制作入口缺口。')), '',
             f'原版运行时读取 {vanilla["itemCount"]} 个物品和 {vanilla["recipeCount"]} 条展开后配方（含配方组和运行时生成配方），`SetupRecipes` 完整执行成功；模组从隔离运行 `{args.mod_run.name}` 导出 {len(mod["items"])} 个物品、{len(mod["recipes"])} 条配方。', '',
             '范围：非时装盔甲、原版 `accessory` 标记的饰品、非弹药/非放置物的伤害物品，以及镐/斧/锤/钓竿。原版虽把 14 种彩色高尔夫球标为 accessory，本表将它们排除，不把高尔夫用品算成饰品缺口。没有把家具、建筑材料、弹药、纯时装、宠物、坐骑全算进装备。', '',
             '“有物品和配方”仅表示当前运行时有真实物品映射和制作入口，不代表每项特殊效果已逐一实测，也不保证所有材料/NPC/任务途径可获取。配方匹配另比较材料、数量、制作站和原版配方组；未将环境条件或阶段完全相同作为已验证事实。', '',
             '## 总览', '', '| 类别 | 原版可制作成品 | 有物品和配方 | 缺物品 | 有物品但缺配方 |', '|---|---:|---:|---:|---:|']
    for category in ('盔甲', '武器', '饰品', '工具与钓竿'):
        pool = {i for i in expected if kind(stats[i]) == category}
        c = collections.Counter(status[i] for i in pool)
        lines.append(f'| {category} | {len(pool)} | {c["有物品和制作配方"]} | {c["缺物品定义"]} | {c["有物品但缺制作配方"]} |')
    c = collections.Counter(status[i] for i in expected)
    lines += [f'| 合计 | {len(expected)} | {c["有物品和制作配方"]} | {c["缺物品定义"]} | {c["有物品但缺制作配方"]} |', '', '## 23 种锭的直接和后续制作链', '',
              '追踪配方材料的后继关系，包括把低级武器/饰品继续合成的链；同一成品可能属于多种锭，不能把逐锭行相加。此处只追踪制作，不把召唤首领后掉落、微光转换等算成制作配方。', '',
              f'去重后共有 {len(bar_products)} 件锭相关装备；' + '、'.join(f'{s} {n}' for s, n in collections.Counter(status[i] for i in bar_products).items()) + '。', '',
              '| 锭 | 原版相关成品 | 有物品和配方 | 缺口 |', '|---|---:|---:|---|']
    for bar_name in BAR_NAMES:
        bar = next(i for i, n in bars.items() if n == bar_name)
        pool = expected & descendants({bar})
        missing = [i for i in sorted(pool) if status[i] != '有物品和制作配方']
        lines.append(f'| {name(bar)} | {len(pool)} | {len(pool)-len(missing)} | ' + ('；'.join(name(i) + '：' + status[i] for i in missing) or '无物品/制作入口缺口') + ' |')
    lines += ['', '## 全部物品/制作入口缺口', '', '| 原版物品 | 类别 | 状态 | 属于锭制作链 |', '|---|---|---|---|']
    for i in gaps:
        lines.append(f'| {label(i)} | {kind(stats[i])} | {status[i]} | {"是" if i in bar_products else "否"} |')
    lines += ['', '## 已有配方但未匹配到相同材料/数量/制作站', '',
              f'已有制作入口的 {c["有物品和制作配方"]} 件中，{sum(compatible.values())} 件至少有一条原版兼容配方；其余 {len(mismatched)} 件如下。这里的差异可能是有意适配、遗漏或材料分组处理差异，不等于武器功能缺失。', '',
              '| 物品 | 模组配方 | 原版配方 |', '|---|---|---|']
    def vanilla_recipe(r):
        return ' + '.join(f'{name(i)}×{n}' for i, n in r['ingredients']) + f' → ×{r["count"]} @tile {r["tile"]}'
    def mod_recipe(r):
        return ' + '.join(f'{ing["Type"]}×{ing["Count"]}' for ing in r['Ingredients']) + f' → ×{r["Count"]} @{r["Station"]}'
    for i in mismatched:
        lines.append(f'| {label(i)} | ' + '；'.join(mod_recipe(r) for r in mod_recipes[i]) + ' | ' + '；'.join(vanilla_recipe(r) for r in by_result[i]) + ' |')
    lines += ['', '## 证据与限制', '', f'- 隔离导出状态：`{result}`。',
              '- 原版机器数据：`tools/vanilla-data/crafting/vanilla-runtime.json`；对比明细：同目录 `audit.json`（含每个装备的原版/模组配方）。',
              '- 原版读取工具：`tools/RecipeAudit/DumpCrafting.cs`；对比脚本：`tools/RecipeAudit/audit_crafting.py`。',
              '- 此次没有验证每件装备的伤害、弹幕、套装效果、特殊能力，也未做普通新档的全部材料获取闭环；不能将当前结果说成“所有装备功能都已完成”。', '']
    report = ROOT / '金属锭与可制作装备完整性审计-2026-10-15.md'
    lines += ['墙物品来源已在生成器中显式设为 Craft，不再作为默认 Basic 商品出售；没有配方的墙仍未开放普通制作。', '', '说明：骨头虽有伤害且可投掷，主要仍是掉落材料；其“缺配方”指原版建筑材料的反向拆料入口缺失，不代表模组拿不到骨头。沙暴瓶、暴雪瓶、星力手环等也可能已有掉落来源，这里仅指出未接原版制作入口。', '']
    report.write_text('\n'.join(lines), encoding='utf-8')
    print(json.dumps(dict(all=len(expected), status=c, bars=len(bar_products), bar_status=collections.Counter(status[i] for i in bar_products), recipe_mismatches=len(mismatched)), ensure_ascii=False))
    print('Bar-related gaps:', [(name(i), ids[i], status[i]) for i in gaps if i in bar_products])
    print('Report:', report)


if __name__ == '__main__':
    main()
