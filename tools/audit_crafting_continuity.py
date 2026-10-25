"""Prove recipe input/workstation closure from real source exports, without seeding crafted boss rewards."""
import argparse
import collections
import json
from pathlib import Path


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sources_at(data, stage, kills_only=False):
    result=collections.defaultdict(list)
    items=data['items']
    def add(t, route):
        d=items.get(t)
        if d and not d.get('Placeholder',False) and d['Source']!='Craft' and d['Stage']<=stage['Stage']:
            result[t].append(route)
    for record, rows in stage['Drops'].items():
        for row in rows:
            if row['Chance']>0:
                add(row['Type'],dict(kind='drop',record=record,chance=row['Chance']))
    for t in stage.get('Bait',[]):
        add(t,dict(kind='kill-bait-or-moss'))
    if not kills_only:
        # Explicit production starter items are real seeds, including equipment
        # that is otherwise craft-only. Never seed arbitrary Craft definitions.
        for t in data.get('starterSources', []):
            if t in items and not items[t].get('Placeholder', False) and items[t]['Stage'] <= stage['Stage']:
                result[t].append(dict(kind='starter-kit'))
        for t in data['basicSources']:
            add(t,dict(kind='basic-stock'))
        for row in stage['Shops']:
            add(row['Type'],dict(kind='shop',npc=row['Npc']))
        for kind in ('Fishing','Crates','Quests','WorldSources'):
            for t in stage.get(kind,[]):
                add(t,dict(kind=kind))
    return result


def factory(data, stage, kills_only=False):
    direct=sources_at(data,stage,kills_only)
    stock=set(direct)
    witnesses={t:dict(source=routes) for t,routes in direct.items()}
    stations=data['stationSources']
    world_stations=data['worldStationStages']
    def ready_station(recipe):
        name=recipe['Station']
        if name in ('','徒手'):
            return 'by-hand'
        if not kills_only and world_stations.get(name,99)<=stage['Stage']:
            return 'fixed-world:'+name
        # Altars remain real world fixtures even in the diagnostic kill-only
        # comparison; "kills only" refers to item inputs, not dismantling an altar.
        if world_stations.get(name,99)<=stage['Stage']:
            return 'fixed-world:'+name
        return next((t for t in stations.get(name,[]) if t in stock),None)
    def ready_environment(recipe):
        env=recipe.get('Environment','None')
        if env=='Graveyard':
            return stage['Stage']>=2
        if env=='Honey':
            return stage['Stage']>=4 or 'HoneyDispenser' in stock
        # Water/acid are native environmental conditions (CraftEnvironments),
        # not inventory ingredients. Their rooms are declared in the world data;
        # this graph does not claim to navigate every pond in a real save.
        return True
    recipes=[(i,r) for i,r in enumerate(data['recipes']) if r['StationStage']<=stage['Stage']]
    made=set()
    while True:
        count=len(stock)
        for index,r in recipes:
            station=ready_station(r)
            if station and ready_environment(r) and all(i['Type'] in stock for i in r['Ingredients']):
                made.add(index)
                if r['Result'] not in stock:
                    witnesses[r['Result']]=dict(recipe=index,station=station,ingredients=r['Ingredients'])
                    stock.add(r['Result'])
        if len(stock)==count:
            break
    blocked=[]
    for index,r in recipes:
        if index not in made:
            blocked.append(dict(recipe=index,result=r['Result'],missing=[i['Type'] for i in r['Ingredients'] if i['Type'] not in stock],
                                station=r['Station'] if not ready_station(r) else None,
                                environment=r.get('Environment') if not ready_environment(r) else None))
    return dict(stage=stage['Stage'],directSourceCount=len(direct),reachable=len(stock),recipeCount=len(recipes),
                craftableRecipes=len(made),blocked=blocked,witnesses=witnesses)


def continuity(data):
    inputs={i['Type'] for r in data['recipes'] for i in r['Ingredients']}
    losses=[]
    count=0
    for record,meta in data['enemies'].items():
        if meta['Boss']:
            continue
        seen=set()
        for stage in data['stages']:
            if record not in stage['Drops']:
                continue
            now={r['Type'] for r in stage['Drops'][record] if r['Chance']>0 and r['Type'] in inputs}
            count+=len(seen)
            losses.extend(dict(record=record,stage=stage['Stage'],type=t) for t in sorted(seen-now))
            seen|=now
    return dict(comparisons=count,losses=losses)


def report(data,before,run):
    full=[factory(data,s) for s in data['stages']]
    kills=[factory(data,s,True) for s in data['stages']]
    return dict(itemCount=len(data['items']),recipeCount=len(data['recipes']),
                inputCount=len({i['Type'] for r in data['recipes'] for i in r['Ingredients']}),
                continuity=continuity(data),beforeContinuity=continuity(before),
                runtimeContinuity=read(run/'saves/material-continuity.json'),
                validation=read(run/'result.json'),stages=full,killsOnly=kills)


def markdown(result):
    old=result['beforeContinuity'];new=result['continuity'];v=result['runtimeContinuity']
    lines=['# 制作材料与跨阶段持续来源验证','',
           '执行边界：保留原版商店、钓鱼、采矿和 Boss 专属材料来源；没有新增“转阶段后统一卖给商人”的兜底方案。', '',
           f"覆盖 {result['itemCount']} 个物品、{result['recipeCount']} 条配方、{result['inputCount']} 种配方输入。",'',
           f"修复前，有 {len(old['losses'])} 处小怪原有材料来源在升阶段后消失；修复后，同一怪物的回归检查中消失项为 {len(new['losses'])}。",'',
           '根因：旧材料回溯从 NativeTier 开始，但苍绿之径等实际可达阶段低于怪物档位；同时排除了具名精英。现在从实际场景与档位中较早的阶段回溯，具名精英也保留旧材料。沿用现有新旧池 70%/30% 分配，不把旧材料清空；制作专用成品仍不掉落。', '',
           '本次证明不把 Boss、宝匣或商店直接给出的 Craft 成品当成原材料捷径，也不把未实现占位物品当作免费库存。每一步必须有材料来源、实际工位兼容表中的工位或德特茅斯固定祭坛，循环配方不能凭空生成第一份材料。', '',
           '严格检查补出的遗漏：干火箭改回既有机器侠商店来源，打破“干火箭↔液体火箭”虚假起点；蜂巢水域补上已有蜂蜜钓鱼规则需要的 InHoney 标记，蜂蜜鱼恢复可钓；金蠕虫、金蝴蝶的阶段与深巢唯一来源对齐，不提高原有稀有掉率。', '',
           '## 保留原版获取途径的制作链', '',
           '| 阶段 | 应检查配方 | 材料与工位均可达 | 未闭合 |','| --- | ---: | ---: | ---: |']
    for s in result['stages']:
        lines.append(f"| {s['stage']} | {s['recipeCount']} | {s['craftableRecipes']} | {len(s['blocked'])} |")
    lines+=['','## 只靠怪物材料的对照检查','',
            '下表刻意排除商店、钓鱼、宝匣、采矿和原版能力拾取，因此不能用它冒充保留原版途径的结论。缺口会在完整 JSON 中逐条列明。', '',
            '| 阶段 | 只用怪物材料可制造 | 需要其他获取途径的配方 |','| --- | ---: | ---: |']
    for s in result['killsOnly']:
        lines.append(f"| {s['stage']} | {s['craftableRecipes']} | {len(s['blocked'])} |")
    lines+=['','## 实际运行证据','',
            f"原场景及额外场景覆盖：{v['Transitions']} 次阶段持续来源比较，{v['Checks']} 项检查，失败 {v['Failures']}。",'',
            '另在阶段 1、5 实际调用击杀掉落流程，验证苍绿之径旧材料能继续刷到：','',
            '| 阶段 | 材料 | 掉率 | 模拟击杀 | 实际获得数量 |','| --- | --- | ---: | ---: | ---: |']
    for s in v['Samples']:
        lines.append(f"| {s['Stage']} | {s['Type']} | {s['Chance']:.6%} | {s['Trials']} | {s['Count']} |")
    lines+=['','## 验证范围','',
            '这些结果证明当前物品表、掉落表及工位表中的依赖路径。采矿、能力拾取和水/酸环境使用原有世界来源声明；没有自动走遍每个房间或逐个点击所有配方，因此不将它表述为每个存档、每条地图路线都已手动通关验证。原版环境条件和配方材料、数量、工位保持不变。','',
            '完整 JSON 保留每个可获取物品的原料与工位路径，以及只刷怪模式下每一条缺失项。','']
    return '\n'.join(lines)


if __name__=='__main__':
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('runtime',type=Path)
    ap.add_argument('--before',type=Path,required=True)
    ap.add_argument('--output',type=Path,required=True)
    args=ap.parse_args()
    run=args.runtime.resolve().parent.parent
    result=report(read(args.runtime),read(args.before),run)
    args.output.with_suffix('.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf-8')
    args.output.with_suffix('.md').write_text(markdown(result),'utf-8')
    print('Recipe rows:',result['recipeCount'],'inputs:',result['inputCount'])
    print('Lost repeat sources before/after:',len(result['beforeContinuity']['losses']),len(result['continuity']['losses']))
    for s,k in zip(result['stages'],result['killsOnly']):
        print(f"Stage {s['stage']}: full={s['craftableRecipes']}/{s['recipeCount']}; kills-only={k['craftableRecipes']}/{k['recipeCount']}")
    v=result['validation']
    assert v['Completed'] and not v['TimedOut'] and not v['Failures'] and not v['ChangedPlayerSaves'] and not v['NewPlayerSaves']
    assert not result['continuity']['losses'] and result['runtimeContinuity']['Failures']==0, 'An older material source still disappears'
    assert not any(s['blocked'] for s in result['stages']), 'A recipe still lacks material/station paths'
