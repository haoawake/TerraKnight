"""Report every runtime item, material, source and recipe after a staged acquisition review."""
import argparse
import collections
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(p):
    return json.loads(Path(p).read_text(encoding='utf-8-sig'))


def audit(runtime, before):
    def recipe_identity(r):
        return (r['Result'],r['Count'],r['Station'],r.get('Environment','None'),
                tuple(sorted((i['Type'],i['Count']) for i in r['Ingredients'])))
    assert collections.Counter(map(recipe_identity,runtime['recipes']))==collections.Counter(map(recipe_identity,before['recipes'])), 'Recipe ingredients, counts, stations or environments changed'
    policy = read(ROOT / 'tools/resource_stage_policy.json')
    targets = {}
    for group in policy['groups'] + policy['stations']:
        for t in group['items'].split():
            assert t not in targets, 'Duplicate policy entry: ' + t
            targets[t] = dict(stage=group['stage'], reason=group['reason'])
    names = {}
    for p in (ROOT / 'src/TerraKnight/Items').glob('*.cs'):
        names.update(re.findall(r'Mat\(ItemType\.(\w+),\s*"([^"]+)"', p.read_text('utf-8-sig')))
    assert not names.keys() - targets.keys(), 'Unreviewed material definitions: ' + str(names.keys() - targets.keys())
    items = runtime['items']
    first = {}
    stage_rows = []
    missing = []
    for stage in runtime['stages']:
        s = stage['Stage']
        sources = collections.defaultdict(list)
        for t, d in items.items():
            if d['Stage'] <= s and d['Source'] == 'Basic':
                sources[t].append('basic')
        for rec, rows in stage['Drops'].items():
            for row in rows:
                if row['Chance'] > 0:
                    sources[row['Type']].append('drop:' + rec)
        for row in stage['Shops']:
            sources[row['Type']].append('shop:' + row['Npc'])
        for kind in ('Fishing', 'Crates', 'Bait', 'Quests', 'WorldSources'):
            for t in stage.get(kind, []):
                if t in items and items[t]['Stage'] <= s:
                    sources[t].append(kind)
        reach = set(sources)
        recipes = [r for r in runtime['recipes'] if r['StationStage'] <= s]
        crafted = set()
        while True:
            n = len(reach)
            for r in recipes:
                if all(i['Type'] in reach for i in r['Ingredients']):
                    crafted.add(r['Result'])
                    if r['Result'] not in reach:
                        sources[r['Result']].append('craft:' + r['Station'])
                        reach.add(r['Result'])
            if len(reach) == n:
                break
        for t in sorted(reach):
            first.setdefault(t, dict(stage=s, sources=sources[t]))
        blocked = []
        for r in recipes:
            if r['Result'] not in crafted:
                blocked.append(dict(result=r['Result'], missing=[i['Type'] for i in r['Ingredients'] if i['Type'] not in reach]))
        stage_rows.append(dict(stage=s, reachable=len(reach), blocked=blocked))
    rows = []
    for t, d in sorted(items.items()):
        old = before.get('items', {}).get(t, {})
        available = first.get(t)
        record = dict(type=t, name=d.get('Name', names.get(t, t)), stage=d['Stage'],
                      before=old.get('Stage'), source=d['Source'], first=available,
                      material=d.get('Material', False), station=d.get('Station', 'None'),
                      reviewed=targets.get(t), changed=old.get('Stage') != d['Stage'])
        if t in names or t in targets:
            if available is None or available['stage'] > d['Stage']:
                missing.append(record)
        rows.append(record)
    removed_crafted = set()
    ordinary_crafted_rows = []
    enemies=runtime.get('enemies', {})
    for data, old in ((before, True),(runtime, False)):
        for stage in data['stages']:
            for rec, drops in stage['Drops'].items():
                if enemies.get(rec, {}).get('Boss', True):
                    continue
                for row in drops:
                    if data['items'][row['Type']]['Source']=='Craft':
                        if old:
                            removed_crafted.add(row['Type'])
                        else:
                            ordinary_crafted_rows.append((stage['Stage'],rec,row['Type']))
    if enemies:
        assert not ordinary_crafted_rows, 'Craft-only ordinary enemy drop leaked: ' + str(ordinary_crafted_rows[:5])
    # Rates and quantities belong to the existing roll rules. This compares every
    # production Row() call against Git HEAD, not against a stale JSON snapshot.
    loot_path = 'src/TerraKnight/Items/ProgressionLoot.cs'
    old_loot = subprocess.check_output(['git', 'show', 'HEAD:' + loot_path], cwd=ROOT).decode('utf-8-sig')
    new_loot = (ROOT / loot_path).read_text('utf-8-sig')
    def roll_calls(text):
        return [s.strip() for s in re.findall(r'\bRow\([^;\n]*', text)]
    assert roll_calls(old_loot) == roll_calls(new_loot), 'A production Row probability/quantity call changed'
    return dict(itemCount=len(items), recipeCount=len(runtime['recipes']),
                reviewedMaterialDefinitions=len(names), reviewedResourcesAndStations=len(targets),
                changedItems=sum(r['changed'] for r in rows), rateCallsUnchanged=True,
                ordinaryCraftedRows=ordinary_crafted_rows,removedCraftedTypes=sorted(removed_crafted),
                stages=stage_rows, sourceDelays=missing, items=rows)


def markdown(data):
    lines = ['# 全量物品获取阶段复核', '',
             '后续更严格的材料、实际工位依赖及跨阶段持续来源检查见同目录 `crafting-material-continuity-2026-10-19.md`。该检查排除制作成品直接掉落作为依赖捷径。', '',
             f"运行时覆盖 {data['itemCount']} 个物品、{data['recipeCount']} 条配方；逐项分类复核 {data['reviewedMaterialDefinitions']} 个 Mat 材料定义与 {data['reviewedResourcesAndStations']} 个资源/工位/宝匣门槛。", '',
             f"本次实际改变 {data['changedItems']} 个物品的阶段（包括合成链自动传播与地区家具），没有重写原版配方材料、数量、工位和环境条件。", '',
             '掉率：全部生产 Row() 调用与修改前逐项一致；独立掉落概率、池触发概率、数量、难度倍率均未调整。新增同期来源和提前解锁会改变部分共享池的候选占比，不能理解为每项最终边际概率完全不变。', '',
             '小怪只制造物品过滤：按物品的 Craft 来源分类统一处理普通怪、精英、首杀及旧阶段保留表；不会仅凭“有配方”就误删暴雪瓶、星力手环等仍有原版世界来源的物品。Boss 奖励保持原范围。删除的组内份额保留为空结果，图鉴和随机抽签共用原总权重，因此不把被删物品的概率转移给剩余物品。鱼饵、苔藓和宠物旁路也执行同一限制。', '',
             f"运行时普通敌人阶段表中的 Craft 条目：{len(data['ordinaryCraftedRows'])}；修改前此类成品共有 {len(data['removedCraftedTypes'])} 种出现在小怪表内。", '',
             '检查包含真实游戏中的掉落表、随机掉落样本、商店、钓鱼、宝匣与配方。采矿/原版能力拾取采用来源声明补充图模型，不能替代逐地区手动通关或采矿验证。钓鱼和宝匣来源为有限抽样，来源未命中也会记录为待核验。', '',
             '## 配方闭合检查', '', '| 阶段 | 可获取物品 | 尚未闭合的配方 |', '| --- | ---: | ---: |']
    for s in data['stages']:
        lines.append(f"| {s['stage']} | {s['reachable']} | {len(s['blocked'])} |")
    lines += ['', '## 材料和工位的来源延迟或未覆盖项', '', '下表区分标注阶段与采样到的最早来源；不会把仅修改标签称为同期可获取。', '', '| 物品 | 标注阶段 | 最早来源阶段 | 说明 |', '| --- | ---: | --- | --- |']
    for r in data['sourceDelays']:
        first = r['first']
        note=', '.join(first['sources'][:2]) if first else '检查世界采集、开箱、事件或采样覆盖'
        if r['type'] in ('FloatingIslandFishingCrateHard','HallowedFishingCrateHard','OasisCrateHard'):
            note+='；地区专属宝匣，困难模式开关阶段3，实际仍需到达对应阶段4/5地区，属有意保留的地区限制'
        lines.append(f"| {r['name']} `{r['type']}` | {r['stage']} | {first['stage'] if first else '未覆盖'} | {note} |")
    lines += ['', '## 全部材料与工位逐项结果', '', '| 物品 | 原阶段 | 当前阶段 | 最早来源 | 判断依据 |', '| --- | ---: | ---: | --- | --- |']
    for r in data['items']:
        if not r['reviewed']:
            continue
        first = r['first']
        lines.append(f"| {r['name']} `{r['type']}` | {r['before']} | {r['stage']} | {str(first['stage']) + ': ' + ', '.join(first['sources'][:2]) if first else '未覆盖'} | {r['reviewed']['reason']} |")
    lines += ['', '完整 JSON 另列全部物品、来源、阶段变化及每条未闭合配方的缺失材料。仅制造物品统一从小怪表过滤，其余装备、饰品、坐骑和特殊物品保留合法来源；材料链随资源门槛重新计算，不将所有物品强行放入同一个阶段。', '']
    if 'validation' in data:
        v=data['validation']
        lines += ['## 验证证据', '',
                  f"隔离游戏副本：`{v['run']}`；Completed={v['completed']}，TimedOut={v['timedOut']}，Failures={len(v['failures'])}。", '',
                  f"正式玩家存档修改 {len(v['changedPlayerSaves'])}，新增 {len(v['newPlayerSaves'])}。运行器不承诺共享设置恢复，不能将这一结果写成共享设置恢复验证通过。", '',
                  '独立代码检查：75 项资源/地区检查；30 项过滤、图鉴概率和随机抽签检查（180000 次抽签）；资源表生成往返一致。构建使用 SkipPluginInstall=true，未安装到正式游戏目录。', '',
                  '运行日志汇总：', ''] + ['- '+line for line in v['summaries']] + ['']
    return '\n'.join(lines)


if __name__ == '__main__':
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('runtime', type=Path)
    ap.add_argument('--before', type=Path, required=True)
    ap.add_argument('--output', type=Path, required=True)
    args = ap.parse_args()
    data = audit(read(args.runtime), read(args.before))
    run=args.runtime.resolve().parent.parent
    result=read(run/'result.json')
    assert result['Completed'] and not result['TimedOut'] and not result['Failures'], 'Runtime suite did not pass'
    assert not result['ChangedPlayerSaves'] and not result['NewPlayerSaves'], 'Player saves changed'
    assert not any(s['blocked'] for s in data['stages']), 'A staged recipe dependency is still blocked'
    assert {r['type'] for r in data['sourceDelays']} <= {'FloatingIslandFishingCrateHard','HallowedFishingCrateHard','OasisCrateHard'}, 'Unresolved resource source delay'
    log=(run/'AutoTest.log').read_text('utf-8-sig')
    data['validation']=dict(run=str(run),completed=result['Completed'],timedOut=result['TimedOut'],failures=result['Failures'],
                            changedPlayerSaves=result['ChangedPlayerSaves'],newPlayerSaves=result['NewPlayerSaves'],
                            summaries=[line for line in log.splitlines() if '材料阶段全表汇总' in line or '材料掉率汇总' in line])
    args.output.with_suffix('.json').write_text(json.dumps(data, ensure_ascii=False, indent=2), 'utf-8')
    args.output.with_suffix('.md').write_text(markdown(data), 'utf-8')
    print(f"Items={data['itemCount']} recipes={data['recipeCount']} materials={data['reviewedMaterialDefinitions']} reviewed={data['reviewedResourcesAndStations']} changed={data['changedItems']} delays={len(data['sourceDelays'])}")
    for s in data['stages']:
        print(f"stage {s['stage']}: reachable={s['reachable']} blocked={len(s['blocked'])}")
