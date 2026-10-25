"""Report post-restoration boss balance without passing estimates off as boss kills."""
import argparse
import hashlib
import json
import math
import statistics
from pathlib import Path

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('run',type=Path)
p.add_argument('--source-root',type=Path,default=Path('artifacts/boss-loadouts-source-20261016'))
p.add_argument('--endgame-run',type=Path)
p.add_argument('--endgame-source',type=Path,default=Path('artifacts/boss-loadouts-endgame-source-20261016'))
a=p.parse_args()
read=lambda f:json.loads(f.read_text(encoding='utf-8-sig'))
r=read(a.run/'result.json')
assert r['Completed'] and not r['TimedOut'] and not r['Failures']
assert not r['ChangedPlayerSaves'] and not r['NewPlayerSaves']
data=read(a.run/'saves/boss-loadouts.json')
assert data['Failures']==0 and len(data['Profiles'])==28
base=Path('artifacts/equipment-restore-source-20261016')
old=read(base/'build-source-hashes.json')
new=read(a.source_root/'build-source-hashes.json')
production=[k for k in old if not k.startswith('DevAutoTest')]
assert all(old[k]==new[k] for k in production), 'Production differs from the installed restoration'
assert all(hashlib.sha256((a.source_root/'src/TerraKnight'/k).read_bytes()).hexdigest()==v for k,v in new.items())
items=read(base/'tools/encyclopedia_runtime.json')['data']['items']
name=lambda t:items.get(t,{}).get('Name',t)
classes=['近战','远程','魔法','召唤']
profiles={(v['Stage'],v['Class'],v['Entry']):v for v in data['Profiles']}
endgame=None
if a.endgame_run:
    er=read(a.endgame_run/'result.json')
    assert er['Completed'] and not er['TimedOut'] and not er['Failures'] and not er['ChangedPlayerSaves'] and not er['NewPlayerSaves']
    endgame=read(a.endgame_run/'saves/boss-loadouts.json')
    assert endgame['Endgame'] and len(endgame['Profiles'])==4 and endgame['Failures']==0
    eh=read(a.endgame_source/'build-source-hashes.json')
    assert all(old[k]==eh[k] for k in production)

def ttk(hp,v):
    first=v['BurstDps'];last=v['SustainedDps']
    if hp<=first*8 and first>0: return hp/first
    return 8+(hp-first*8)/last if last>0 else math.inf

def number(v): return '无法维持输出' if not math.isfinite(v) else f'{v:.1f}'

estimates=[]
for b in data['Bosses']:
    if b['Stage']>5: continue
    entry=b['Record']=='InfectedKnight'
    for c in range(4):
        v=profiles[(b['Stage'],c,entry)]
        estimates.append({**b,'Class':c,'Entry':entry,'BurstDps':v['BurstDps'],'SustainedDps':v['SustainedDps'],
                          'ReferenceDefense':v['Defense'],'DefenseDifference':b['Defense']-v['Defense'],
                          'EstimatedContactSeconds':ttk(b['Life'],v)})
if endgame:
    ep={v['Class']:v for v in endgame['Profiles']}
    for b in endgame['Bosses']:
        if b.get('Location')!='GodhomeAttuned':continue
        for c in range(4):
            v=ep[c]
            estimates.append({**b,'Class':c,'Entry':False,'BurstDps':v['BurstDps'],'SustainedDps':v['SustainedDps'],
                              'ReferenceDefense':v['Defense'],'DefenseDifference':b['Defense']-v['Defense'],
                              'EstimatedContactSeconds':ttk(b['Life'],v)})

lines=['# 恢复原版装备后的 Boss 平衡回归', '',
       '本轮只测试和记录，未修改 Boss 血量、装备属性或主游戏安装文件。生产代码与已安装的原版装备恢复版本逐文件相同，只添加隔离审计代码。', '',
       '## 测试口径', '',
       '- 实际运行 28 组配装：六阶段 × 四职业，以及四阶段入口 Boss 使用三阶段装备的四职业配装。',
       '- 从当时可解锁的主线套装中比较静态输出能力，保留更强的早期套装；不因阶段更高而强行换上原版属性更弱的蜜蜂套。未穷举所有装备和武器组合。',
       '- 实际攻击固定单目标，通过 Boss 的真实防御和伤害换算扣血；原版增伤、暴击、攻速、仆从数量、鞭子、套装命中特效均走游戏代码。',
       '- 每组前 8 秒计爆发，后续计持续输出；近战/远程/召唤共测 16 秒，魔法测 24 秒。不给魔力续杯、不喝药水，弹药无限供应。',
       '- 统一最多 5 个饰品槽，不使用恶魔之心或大师额外槽，不使用毕业后解锁装备、重铸及药水增益。受击反击不计入本轮主动输出。',
       '- 下方击杀时间是按实测输出推算的有效接触时间：前 8 秒使用爆发数据，其后使用持续数据，避免把短暂魔力爆发无限外推。',
       '- 这不是逐个 Boss 的实际通关时间。目标被固定，未计入走位、无敌期、转阶段、弹道命中率和追击损失；Boss 防御与代表目标不同的记录单独标出，未假装进行精确防御修正。', '',
       f'隔离测试完成：{data["Checks"]} 项检查，零失败；28 组实测、{len(data["Bosses"])} 条 Boss/难度血量记录。玩家存档未变化。', '',
       '## 各阶段完整配装的实际输出', '',
       '|阶段|职业|武器 / 仆从|护甲|饰品|Boss 防御|仆从上限|前 8 秒 DPS|后续 DPS|魔力前→后|',
       '|---:|---|---|---|---|---:|---:|---:|---:|---|']
for v in data['Profiles']:
    title=str(v['Stage'])+(' 入口' if v['Entry'] else '')
    weapon=name(v['Weapon'])+(' + '+name(v['Summon']) if v['Summon'] else '')
    lines.append(f'|{title}|{classes[v["Class"]]}|{weapon}|'+ '、'.join(name(t) for t in v['Armor'])+'|'+
                 '、'.join(name(t) for t in v['Accessories'])+f'|{v["Defense"]}|{v["Minions"]}|{v["BurstDps"]:.1f}|{v["SustainedDps"]:.1f}|{v["ManaBefore"]}→{v["ManaAfter"]}|')
if endgame:
    lines+=['', '## 通关后毕业配装补测', '',
            '以下四组使用日耀/星旋/星云/星尘原版套装和通关后武器，仅用于神居重打推算，未混入主线阶段。星旋主动隐身已打开；其他口径仍为五饰品、无重铸、无药水。', '',
            '|职业|武器 / 仆从|前 8 秒 DPS|后续 DPS|魔力前→后|','|---|---|---:|---:|---|']
    for v in endgame['Profiles']:
        weapon=name(v['Weapon'])+(' + '+name(v['Summon']) if v['Summon'] else '')
        lines.append(f'|{classes[v["Class"]]}|{weapon}|{v["BurstDps"]:.1f}|{v["SustainedDps"]:.1f}|{v["ManaBefore"]}→{v["ManaAfter"]}|')

lines+=['', '## 全部 Boss 的普通/大师有效接触时间推算', '',
        '同一列的四个数依次为近战、远程、魔法、召唤，单位为秒。大师列沿用相同的五饰品配装，未加入第六槽额外收益。', '',
        '|阶段|Boss / ID|普通总血量|普通四职业秒数|大师总血量|大师四职业秒数|防御偏差范围|',
        '|---:|---|---:|---|---:|---|---|']
normal=[b for b in data['Bosses'] if b['Difficulty']=='Normal']
for b in sorted(normal,key=lambda b:(b['Stage'],b['Record'])):
    ns=[e for e in estimates if e['Record']==b['Record'] and e['Difficulty']=='Normal' and not e.get('Location')]
    ms=[e for e in estimates if e['Record']==b['Record'] and e['Difficulty']=='Master' and not e.get('Location')]
    if not ns or not ms: continue
    diff=[e['DefenseDifference'] for e in ns]
    lines.append(f'|{b["Stage"]}|{b["Name"]} / `{b["Record"]}`|{b["Life"]}|'+
                 ' / '.join(number(e['EstimatedContactSeconds']) for e in ns)+f'|{ms[0]["Life"]}|'+
                 ' / '.join(number(e['EstimatedContactSeconds']) for e in ms)+f'|{min(diff):+d}～{max(diff):+d}|')

if endgame:
    lines+=['', '## 神居调谐重打的有效接触时间', '',
            '四列均使用上面的毕业配装，单位为秒。这是调谐、单人血量，不代表飞升/辐辉或多人血量；前三职业或多仆从命中大体型时也可能不同于当前小目标。', '',
            '|Boss / ID|普通 / 大师总血量|普通四职业秒数|大师四职业秒数|','|---|---|---|---|']
    for b in [b for b in endgame['Bosses'] if b.get('Location')=='GodhomeAttuned' and b['Difficulty']=='Normal']:
        ns=[e for e in estimates if e['Record']==b['Record'] and e['Difficulty']=='Normal' and e.get('Location')]
        ms=[e for e in estimates if e['Record']==b['Record'] and e['Difficulty']=='Master' and e.get('Location')]
        lines.append(f'|{b["Name"]} / `{b["Record"]}`|{b["Life"]} / {ms[0]["Life"]}|'+
                     ' / '.join(number(e['EstimatedContactSeconds']) for e in ns)+'|'+
                     ' / '.join(number(e['EstimatedContactSeconds']) for e in ms)+'|')
short=[e for e in estimates if e['Difficulty']=='Normal' and e['EstimatedContactSeconds']<20 and not e.get('Location')]
lines+=['', '## 需要优先复核的短战斗', '',
        '20 秒仅作为本轮筛选标记，不是原版规则，也不是自动加血依据。普通难度、站桩理想命中下低于此值的记录如下；应先对照移动/无敌/阶段机制再决定是否增加血量。', '',
        '|Boss|阶段|职业|当前总血量|有效接触秒数|Boss / 代表目标防御|', '|---|---:|---|---:|---:|---|']
for e in sorted(short,key=lambda e:e['EstimatedContactSeconds']):
    lines.append(f'|{e["Name"]} / `{e["Record"]}`|{e["Stage"]}|{classes[e["Class"]]}|{e["Life"]}|{e["EstimatedContactSeconds"]:.1f}|{e["Defense"]} / {e["ReferenceDefense"]}|')
lines+=['', '## 证据', '',f'- 运行：`{a.run}`',f'- 数据：`{a.run}/saves/boss-loadouts.json`',
        f'- 测试源码快照：`{a.source_root}`',
        '- Boss 当前阶段血量系数为 0.65 / 0.65 / 0.60 / 0.55 / 0.50 / 0.40；套装恢复没有同步抬高这些系数。',
        '- 全部四难度、四职业的推算保存在 `artifacts/boss-post-restore-estimates.json`。']
if a.endgame_run:lines.append(f'- 毕业配装补测：`{a.endgame_run}/saves/boss-loadouts.json`。')
Path('Boss原版装备恢复后平衡回归.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
def finite(value):
    if isinstance(value,dict):return {k:finite(v) for k,v in value.items()}
    if isinstance(value,list):return [finite(v) for v in value]
    if isinstance(value,float) and not math.isfinite(value):return None
    return value
Path('artifacts/boss-post-restore-estimates.json').write_text(json.dumps(finite({'Run':str(a.run),'ProductionEquivalentFiles':len(production),'Profiles':data['Profiles'],'EndgameProfiles':endgame['Profiles'] if endgame else [],'Estimates':estimates}),ensure_ascii=False,indent=2,allow_nan=False),encoding='utf-8')
print(json.dumps({'profiles':len(data['Profiles']),'boss_difficulty_records':len(data['Bosses']),'checks':data['Checks'],'short_normal_class_cases':len(short),'report':'Boss原版装备恢复后平衡回归.md'},ensure_ascii=False))
