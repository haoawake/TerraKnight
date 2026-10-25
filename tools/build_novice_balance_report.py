"""Summarize bounded acquisition cohorts and real AI attempts without inventing a playthrough."""
import argparse
import collections
import json
import math
from pathlib import Path
import statistics

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('run', type=Path)
p.add_argument('--audit', type=Path)
p.add_argument('--acquisition', type=Path)
p.add_argument('--before-audit', type=Path)
p.add_argument('--retake', type=Path, action='append', default=[])
p.add_argument('--output', type=Path, default=Path('docs/新玩家成长平衡测试-2026-10-17.md'))
a = p.parse_args()
read = lambda f: json.loads(f.read_text(encoding='utf-8-sig'))
status = read(a.run / 'result.json')
assert status['Completed'] and not status['TimedOut'] and not status['Failures'], 'suite did not finish cleanly'
assert status['RestoredSettings'] and not status['ChangedPlayerSaves'] and not status['NewPlayerSaves']
acquisition_run = a.acquisition or a.run
if a.acquisition:
    ac_status=read(a.acquisition/'result.json')
    assert ac_status['Completed'] and not ac_status['TimedOut'] and not ac_status['Failures'] and ac_status['RestoredSettings']
    assert not ac_status['ChangedPlayerSaves'] and not ac_status['NewPlayerSaves']
data = read(acquisition_run / 'saves/novice-acquisition.json')
assert data['Failures'] == 0 and len(data['Rows']) == 768
battles = read(a.run / 'saves/novice-bosses.json')
assert battles['RealAI'] and not battles['Immortal'] and not battles['DirectDamage']
for retake in a.retake:
    rs = read(retake / 'result.json')
    assert rs['Completed'] and not rs['TimedOut'] and not rs['Failures'] and rs['RestoredSettings']
    assert not rs['ChangedPlayerSaves'] and not rs['NewPlayerSaves']
    replacement = read(retake / 'saves/novice-bosses.json')
    assert replacement['RealAI'] and not replacement['Immortal'] and not replacement['DirectDamage']
    key = lambda r: (r['Difficulty'], r['Class'], r['GearStage'], r['Boss'])
    updated = {key(r):r for r in replacement['Results']}
    battles['Results'] = [updated.pop(key(r),r) for r in battles['Results']] + list(updated.values())
assert len(battles['Results']) == 48
assert len({(r['Difficulty'],r['Class'],r['GearStage']) for r in battles['Results']}) == 48
rows = data['Rows']
catalogue = read(a.audit/'saves/novice-audit.json')['Items'] if a.audit else []
by_id={str(d['Id']):d['Name'] for d in catalogue}
by_type={d['Type']:d['Name'] for d in catalogue}
label=lambda value: '无' if str(value) in ('0','None') else by_id.get(str(value),by_type.get(str(value),str(value)))
buff_names = {87:'温馨之火',89:'心灯',146:'快乐',158:'星星瓶',215:'巴斯特防御',74:'烈火灌注',71:'毒液灌注',73:'诅咒焰灌注',76:'灵液灌注',79:'毒药灌注'}
for item in catalogue:
    if item['BuffType'] > 0:
        buff_names.setdefault(item['BuffType'], item['Name'])
buff_names.update({29:'灵视',93:'弹药箱',150:'着魔',159:'锋利',192:'糖果冲刺',348:'战略家'})
buff_label=lambda t:buff_names.get(t, str(t))
labels=lambda values: '、'.join(label(v) for v in values if str(v) not in ('0','None')) or '无'
boss_names={'Hornet':'大黄蜂','MegaMossCharger':'大型苔藓冲锋者','MageLord':'灵魂大师','InfectedKnight':'残破容器','Hornet@4':'大黄蜂（守望者）','TraitorLord':'叛徒领主'}
boss_label=lambda value:boss_names.get(value,value)
phase_names={'Mage Lord':'灵魂大师第一阶段','Mage Lord Phase2':'灵魂大师第二阶段','Mantis Traitor Lord':'叛徒领主','Hornet Boss 1':'大黄蜂','Hornet Boss 2':'大黄蜂（守望者）','Mega Moss Charger':'大型苔藓冲锋者','Infected Knight':'残破容器'}
combat_seen=lambda r:bool(r['PhasesSeen']) if 'PhasesSeen' in r else r['Seen']
classes = ['近战', '远程', '魔法', '召唤']
stages = ['冲刺前', '冲刺后', '螳螂爪后', '水晶之心后', '帝王之翼后', '暗影披风后']

def median(v): return statistics.median(v)
def percentile(v, q):
    v = sorted(v)
    return v[min(len(v)-1, math.ceil(len(v)*q)-1)]
def common(rs):
    counts = collections.Counter(r['WeaponName'] for r in rs)
    return '、'.join(f'{name}（{n}/16）' for name,n in counts.most_common(3))

text = [
    '# 新玩家成长平衡测试（专家、大师）', '',
    '本轮将随机采集、实际合成和真实 Boss AI 战斗分别记录。阶段 1–5 的采集以“已取得对应能力”为前提，不能据此声称角色从零连续通关。', '',
    f'采集核验 {data["Checks"]} 项，失败 0；专家与大师各四职业、每职业 16 个种子、六阶段，共 768 个样本。每阶段追加 180 次普通怪/精英掉落抽样，跨阶段保留库存与现金。', '',
    '## 测试边界', '',
    '- 使用正式运行时的掉落彩票、难度倍率、原版合成数量、工作站兼容、NPC 当日实际货架价格、锻造随机结果和扣款。不会把所有可能掉落当成已经持有。',
    '- 起步只有游戏实际发放的铜短剑、铜镐、木弓、100 支箭、火把、钩爪、攀爬爪和云朵瓶。没有起始金币、整套护甲或预先选好的词缀。',
    '- 四职业分别比较整套护甲与散件、可负担饰品，遵守饰品栏位和翅膀冲突；同一资源账本还支付职业药水、食物、灌注和增益家具。药水与食物有两份准备上限，按实际持续时间补充。篝火、心灯、星星瓶等按项目背包生效规则携带，交互家具按实际交互给 Buff。',
    '- 采集阶段的击杀是掉落抽样，未实际执行这些小怪战斗；不含杀怪时耗费的箭、药水、死亡损失、矿物开采和赶路时间。城镇仓储容量抽象为无限，不模拟稀有旅商到访。同样不计出售、税收和主动采矿收入，因此这是特定收支假设下的样本，不能称为预算上界。',
    '- 每 30 杀才重新选择武器；保留生活预算，升级必须逐项支付。武器候选按攻击周期、多弹幕、穿透次数、仆从频率等现有输出模型排序，该排序本身不是实战结果。远程候选必须同时取得 30 秒射击所需的弹药，召唤分别准备法杖和鞭。',
    '- 16 个种子的掉落不含自然词缀；锻造结果随机，包括阶段0。实战另对主武器及最多两个饰品执行预算内的付费重铸，接受随机词缀。 备用武器按普通库存物品重建，尚未完整跟踪旧备用武器的历史锻造/词缀状态；其切换片段不能代表玩家保留全部旧强化的最佳组合。',
    '- 六阶段采集不赠送 Boss 奖励；因此不包含击败 Boss 后奖励带来的真实成长。不能拿这组采集数据证明 Boss 奖励的完整循环已经平衡。',
    '- 种子 0 的实际配方路径另通过游戏合成栏核对产物、数量与扣料。材料仅从账本中已经获得的库存转入；仅摆放已经获得的工作站，世界自带祭坛单独处理。特殊液体、墓地环境配方未纳入自动规划。', '',
    '## 首次本职业武器掉落', '',
    '以下为随机掉落本身的等待击杀数，不把起始武器、制作或购买算作掉落。180 杀未掉落的种子另列，分位数仅统计已掉落者。', '',
    '| 难度 | 职业 | 中位击杀 | P90 击杀 | 180 杀未掉落 |',
    '|---|---|---:|---:|---:|',
]
for diff in ('Expert','Master'):
    for c in range(4):
        rs = [r for r in rows if r['Stage']==0 and r['Difficulty']==diff and r['Class']==c]
        waits = [r['FirstClassDrop'] for r in rs if r['FirstClassDrop']>=0]
        text.append(f'| {diff} | {classes[c]} | {median(waits) if waits else "—"} | {percentile(waits,.9) if waits else "—"} | {16-len(waits)}/16 |')
start_rows=[r for r in rows if r['Stage']==0]
iron_start=sum('IronskinPotion' in r['BuffItems'] for r in start_rows)
text += ['', '## 增益获取链的实际发现', '',
    f'修复前的受限路线缺少沙/玻璃，无法建立原版放置瓶子工作站；商人已有瓶装水仍不足以解决。此次给商人增加阶段0空瓶，不改制药配方。修复后的128份起步样本中，{iron_start}份实际准备出铁皮药水，全部按原材料与价格付费。', '',
    '蛋糕块与各职业增益家具按货架阶段购买。战争桌也列入哨兵准备候选，未取得时不凭空补上。药水不能假定无限续用；凑不齐两份时退为一份。', '',
    '| 阶段 | 职业 | 铁皮准备成功 | 再生准备成功 | 本职业药水准备成功 |', '|---|---|---:|---:|---|']
for stage in range(6):
    for cls in range(4):
        subset=[r for r in rows if r['Stage']==stage and r['Class']==cls]
        counts=collections.Counter(t for r in subset for t in r['BuffItems'])
        desired=([],['ArcheryPotion','AmmoReservationPotion'],['MagicPowerPotion','ManaRegenerationPotion'],['SummoningPotion'])[cls]
        support='、'.join(f'{label(t)} {counts[t]}/32' for t in desired) or '以通用药水与灌注为主'
        text.append(f'| {stage} | {classes[cls]} | {counts["IronskinPotion"]}/32 | {counts["RegenerationPotion"]}/32 | {support} |')
for diff in ('Expert','Master'):
    text += ['',f'## {diff} 条件采集路线', '', '| 阶段 | 职业 | 常用武器 | 锻造等级中位数 | 余款中位数（吉欧） |', '|---|---|---|---:|---:|']
    for s in range(6):
        for c in range(4):
            rs = [r for r in rows if r['Stage']==s and r['Difficulty']==diff and r['Class']==c]
            text.append(f'| {s} {stages[s]} | {classes[c]} | {common(rs)} | {median([r["WeaponInfo"]["Forge"] for r in rs]):g} | {median([r["Cash"]/100 for r in rs]):.1f} |')
text += ['', '## 同期套装、饰品与 Buff（种子0）', '',
    '这是实际付出材料和预算后准备成功的清单，不是该阶段的全部理论最优物品。空缺表示本条路线未取得，不能自动补全。', '',
    '| 难度/阶段 | 职业 | 护甲 | 饰品 | 药水、食物与灌注 | 携带/交互增益家具 |', '|---|---|---|---|---|---|']
for r in rows:
    if r['Seed']!=0:continue
    text.append(f'| {r["Difficulty"]}/{r["Stage"]} | {classes[r["Class"]]} | {labels(r["Armor"])} | {labels(r["Accessories"])} | {labels(r["BuffItems"])} | {labels(r["CarriedBuffItems"]+r["BuffStations"])} |')
text += ['', '## 召唤路线可替换方案（种子0）', '',
    '仆从、哨兵和鞭分别准备；可替换仆从按已持有物品列出。这里只准备最多四类可负担仆从 AI 作为主动试用方案，随机掉落留下的其他法杖也保留；不会因为某法杖纸面分数低而直接丢弃。', '',
    '| 难度/阶段 | 主法杖 | 已拥有的替换法杖 | 哨兵 | 鞭 |', '|---|---|---|---|---|']
for r in rows:
    if r['Seed']!=0 or r['Class']!=3:continue
    text.append(f'| {r["Difficulty"]}/{r["Stage"]} | {r["WeaponName"]} | {labels(r["SummonAlternatives"])} | {label(r["Sentry"])} | {label(r["Whip"])} |')
text += ['', '## 真实 Boss AI 战斗', '',
    '每场使用对应阶段种子 0 的已获得装备与消耗品；叛徒领主补测包含真实前置波次，前置战败不会伪记为击败/见到主 Boss。生命水晶来自本存档实际生成、挖取并捡到的早期水晶。跨场重建相同条件用于对照，不继承前一场死亡损失或 Boss 奖励。战前赶路与竞技场定位有辅助，进场调用模组原有召唤启动流程并跳过对白；叛徒领主在开战前按真实前置敌人的地面校正起点，灵魂大师战内正常走向假下砸拾取物触发转阶段，允许最多180秒；战内不改主角/Boss 位置、不关 AI、不直接扣 Boss 血、不锁血、不补弹药或蓝量。', '',
    '控制器只会按距离走位、地面起跳、瞄准攻击和使用已持有药水，没有实现玩家完整的冲刺、爬墙、钩爪和读招能力。失败不能直接归因于游戏数值；未见目标或未完成进场的记录属于测试器覆盖缺口。四职业均在实际持有的替换武器间尝试，召唤师另更换仆从 AI，以18秒区间的实扣生命和鞭配合表现作换装参考；记录的是该时段整套配装的总伤害，包含仆从、鞭、荆棘、灌注等；阶段变化和生存时间会干扰对照，不能将其当成严格的单武器/单仆从排名。', '',
    '| 难度 | 职业 | 阶段/Boss | 武器 | 胜负 | 秒 | 生命上限/剩余 | 受击次数 | 使用次数 |',
    '|---|---|---|---|---|---:|---:|---:|---:|']
for r in battles['Results']:
    outcome = '胜' if r['Won'] else '前置战败' if r.get('WaveSeen') and not combat_seen(r) and r['Life']<=0 else '前置超时' if r.get('WaveSeen') and not combat_seen(r) else '未进场' if not combat_seen(r) else '战败' if r['Life']<=0 else '超时/流程未完成'
    text.append(f'| {r["Difficulty"]} | {classes[r["Class"]]} | {r["GearStage"]}/{boss_label(r["Boss"])} | {label(r["Weapon"])} +{r["Forge"]} | {outcome} | {r["Seconds"]:.1f} | {r["LifeMax"]}/{r["Life"]} | {r["HitsTaken"]} | {r["Casts"]} |')
wins=sum(r['Won'] for r in battles['Results'])
unready=sum(not r['Awake'] or not combat_seen(r) and not r.get('WaveSeen') for r in battles['Results'])
wave_only=sum(bool(r.get('WaveSeen')) and not combat_seen(r) for r in battles['Results'])
timeouts=[r for r in battles['Results'] if not r['Won'] and r['Life']>0 and r['Seconds']>=179]
text += ['',f'48 个阶段节点的最终记录中确认击杀 {wins} 场；未完成进场或未见目标（不含已进入前置波次）{unready} 场；仅进入前置波次 {wave_only} 场。其余战败/超时按表保留，不能归为平衡性通过。', '']
if timeouts:
    text += ['以下记录存活至时限而未完成战斗：'+ '、'.join(f'{r["Difficulty"]}/{classes[r["Class"]]}/{boss_label(r["Boss"])}（剩余生命{r["Life"]}/{r["LifeMax"]}）' for r in timeouts)+'。灵魂大师补测日志中存在第二阶段停在 Init、未完成原生转场的情况；尚未确定是测试夹具残留状态还是其他流程条件。没有强制发送转阶段事件绕过，不能把这些超时判作武器输出不足，也不能算击杀。', '']
text += ['', '## 战斗参数与审计资料' , '',
    'JSON 中逐阶段保留武器攻击间隔、动画间隔、弹速、弹幕种类、持续攻击标记、耗蓝和弹药种类；保留每一步制作、购买、换装、锻造成功/失败、现金和材料余量。实战记录重铸费用、词缀、生命/魔力上限、受击、使用次数和剩余弹药。', '',
    '骷髅魔法书已有限制的行为沿用本轮前的实现：最多三次命中、三秒或累计 640 像素消失；本轮补充商人起步阶段空瓶来源，保持原版配方；另修正连续短剑按动画误计攻击频率的问题，以及灰木、珍珠木剑弓的低成本建材武器预算倒挂，未改 Boss 生命。具体改动见下方对照，不能将其描述为微调。', '',
    '## 尚不能下的结论', '',
    '同期指材料、商店与可访问区域实际可达，而不是武器成品标签。规划已移除成品阶段筛选；特殊环境配方、种植养成、钓鱼和 Boss 奖励未完整模拟，清单不能代表全阶段全部可获得的增强物。', '',
    '各阶段只采用最多6颗实挖早期水晶作为共同整备起点；后期继续收集水晶、生命果以及必经Boss奖励进入后续库存尚未模拟。后期战败不能直接推导为充分成长角色的数值结论。\n\n本报告未证明从零到终局的连续真人式流程已经平衡。需要继续覆盖按真实击杀解锁区域、Boss 奖励及宝藏袋进入后续库存、采矿、死亡和补给成本，以及完整玩家动作控制器。不能用固定靶输出或条件配装替代这些证据。', '',
    f'证据使用固定源码快照 `artifacts/novice-validated-source` 与 DLL `artifacts/novice-validated-build`；哈希见该构建目录。其他任务并行修改的生产文件未混入本次测试，不能视为当前工作区全部改动已通过。证据目录：`{a.run.as_posix()}`。测试器正常结束、未超时、正式存档无新增/改动、设置恢复通过。', '',
]
if a.audit:
    ar=read(a.audit/'result.json')
    assert ar['Completed'] and not ar['Failures'] and not ar['TimedOut'] and ar['RestoredSettings'] and not ar['ChangedPlayerSaves'] and not ar['NewPlayerSaves']
    after=read(a.audit/'saves/weapon-balance-after.json')
    before=read(a.audit/'saves/weapon-balance-timber-before.json')
    assert after['Failures']==0 and before['Failures']==0 and before['CounterfactualTimberBaseline']
    text += ['## 建材武器调整的实射对照', '',
        '旧值通过同一测试版本在内存中仅还原四件武器的原始伤害进行受控对照；不是旧版完整客户端回放。均使用同一固定靶、射程和基础弹药条件，包含实际攻击间隔及弹幕命中；旧伤害组只核对发射/命中，不接受其已知超预算值为平衡通过。木桩 DPS 不代表 Boss 击杀时间。', '',
        '| 武器 | 原伤害→现伤害 | 原实射 DPS→现实射 DPS |', '|---|---:|---:|']
    new_live={r['Type']:r for r in after['Live']}
    for old in before['Live']:
        new=new_live[old['Type']]
        text.append(f'| {label(old["Type"])} | {old["Damage"]}→{new["Damage"]} | {old["Dps"]:.1f}→{new["Dps"]:.1f} |')
    text += ['', f'现版本实射 {len(after["Live"])} 组，旧伤害受控对照 {len(before["Live"])} 组；两组运行时检查均无失败。', '']
    audit=read(a.audit/'saves/novice-audit.json')
    assert audit['Failures']==0
    text += ['## 普通、困难小怪与 NPC 采购补充核验', '',
        f'数值与实际商店扣款核验 {audit["Checks"]} 项、失败 0。六阶段按 EnemyStats 实际路径核对小怪生命、防御、原始伤害：普通/困难/专家/大师生命与伤害倍率为0.25/0.5/0.75/1，防御不重复乘难度倍率。', '',
        f'从阶段样本剩余预算出资，经实际 NPC 商店槽交付与扣款成功 {sum(r["Bought"] for r in audit["Purchases"])} 次。无对应采购、无货或 NPC 不在场的阶段保留空记录，不虚构采购。此核验不替代 NPC 住房/解救流程。', '',
        f'补充证据：`{a.audit.as_posix()}`。', '']
if a.before_audit and a.audit:
    earlier=read(a.before_audit/'saves/weapon-balance-after.json')
    old_items={r['Type']:r for r in earlier['Weapons']}
    old_live={r['Type']:r for r in earlier['Live']}
    new_items={r['Type']:r for r in after['Weapons']}
    new_live={r['Type']:r for r in after['Live']}
    text += ['## 连续刺击频率错误的修复', '',
        '前次实射回归因叶绿军刀约359 DPS超出旧模型预算而失败。查明罗马短剑、标尺和叶绿军刀会在一次动画内按UseTime重复刺击；本轮同时修正实际和原版对照的频率预算，并纳入叶绿军刀特效已有的0.7伤害倍率，保留原攻击间隔、动画、射程与材料。不是通过放宽失败阈值处理。', '',
        '| 武器 | 原伤害→现伤害 | 原实射 DPS→现实射 DPS |', '|---|---:|---:|']
    for t in ('Gladius','Ruler','ChlorophyteSaber'):
        prior=f'{old_live[t]["Dps"]:.1f}' if t in old_live else '未实射'
        text.append(f'| {label(t)} | {old_items[t]["Damage"]}→{new_items[t]["Damage"]} | {prior}→{new_live[t]["Dps"]:.1f} |')
    text += ['', f'失败基线保留于`{a.before_audit.as_posix()}`，未将该次审计记为通过。', '']
text += ['', '## 多阶段与前置战斗覆盖', '',
    '| 难度/职业 | Boss | 实际进入的本体 | 前置击杀 | 实际额外跳跃 | 正常流程导航 |', '|---|---|---|---:|---:|---|']
for r in battles['Results']:
    if r['GearStage'] not in (2,5):continue
    def nav_label(value):
        if value.startswith('air-jump/'):return '空中再次按跳跃'
        if value.startswith('jump-to-collect:'):return '跳跃触碰 '+value.split(':',1)[1]
        if value.startswith('walk-to:'):return '接近 '+value.split(':',1)[1]
        return value
    navigation='；'.join(nav_label(v) for v in r.get('EncounterNavigation',[])) or '无' 
    text.append(f'| {r["Difficulty"]}/{classes[r["Class"]]} | {boss_label(r["Boss"])} | {"、".join(phase_names.get(v,v) for v in r.get("PhasesSeen",[])) or "未确认"} | {r.get("WaveKills",0)} | {r.get("ExtraJumps","—")} | {navigation} |')
text += ['', '## 实战换装片段' , '',
    '片段记录观测到的生命减少折算值，包含整套装备、增益和前置敌人。伤害与受击数按逐帧生命差分采样，治疗同时发生或目标被销毁时可能低估；Boss阶段与存活时长也不同，不是严格单武器/单仆从排名。', '',
    '| 难度/阶段/职业 | 实际武器与伤害/持续秒数 | 最终使用 |', '|---|---|---|']
for r in battles['Results']:
    trials='；'.join(f'{label(t["Weapon"])}：{t["Damage"]:.1f}/{t["Seconds"]:.1f}s' for t in r['WeaponTrials']) or '无'
    text.append(f'| {r["Difficulty"]}/{r["GearStage"]}/{classes[r["Class"]]} | {trials} | {label(r["FinalWeapon"])} |')
text += ['## 饰品与增益的实战记录' , '',
    '下表列出结果采样时的防御/仆从上限、开战 Buff 名称及实际消耗。防御与仆从上限可能随药效、动态饰品和死亡变化，不能视为全程固定值。物品在采集清单中出现，只表示已获得；实际生效以这里和原始 JSON 为准。', '',
    '| 难度/阶段/职业 | 防御 | 仆从上限 | 开战 Buff | 实际消耗 |', '|---|---:|---:|---|---|']
for r in battles['Results']:
    used='、'.join(f'{label(v["Type"])}×{v["Count"]}' for v in r['ConsumablesUsed']) or '无'
    text.append(f'| {r["Difficulty"]}/{r["GearStage"]}/{classes[r["Class"]]} | {r["Defense"]} | {r["MaxMinions"]} | {"、".join(buff_label(t) for t in r["StartBuffIds"]) or "无"} | {used} |')
if a.acquisition:
    text += ['', '## 版本与证据复用', '',
        'Boss表合并主批次与补测，不能称为48场均在同一DLL重跑。最终版重新生成全部768份账本，并逐字段确认576份非近战账本与主批次一致；全部近战节点及灵魂大师、叛徒领主节点使用最终版补测，相关记录覆盖旧结果。审计DLL与补测控制器DLL分别保留，后者仅修改测试控制器；生产代码与资源逐文件一致，哈希和核验结果见validated-evidence.json。配装、库存、资金与饰品增益的一致性由validate_novice_evidence.py检查。', '',
        '主批次在输出“完成”后、退出游戏时出现一次原版CameraController.ReleaseLock空引用；最后一组灵魂大师补测也在输出“完成”后出现原版MusicRegion.FadeOut空引用。测试场内未见同类异常，正式存档和设置核验通过。保留这些退场异常，不据此声称日志完全无异常。', '']
if a.audit and (a.audit/'vanilla-recipe-comparison.json').exists():
    recipes=read(a.audit/'vanilla-recipe-comparison.json')
    assert recipes['checked']>0 and not recipes['failures']
    text += ['## 原版配方独立对照', '', f'当前运行时导出的 {recipes["checked"]} 条原版物品配方与原版数据核对，材料、数量、工作站和环境条件不匹配数为0；另有{len(recipes["custom"])}条模组自定义配方单列。物品导出格式由字典改为列表后，核验脚本增加列表读取和重复类型校验，未修改配方匹配标准。', '']
text += ['', f'最终配装账本目录：`{acquisition_run.as_posix()}`。', '', '补测目录：'+('、'.join(str(r) for r in a.retake) or '无')+'。', '']
a.output.parent.mkdir(parents=True,exist_ok=True)
a.output.write_text('\n'.join(text).replace('Expert','专家').replace('Master','大师'),encoding='utf-8')
print(a.output)
