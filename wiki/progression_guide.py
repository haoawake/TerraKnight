"""Player guides derived from the selected release's progression and pet tables."""
import json
import re


def table(headers, rows):
    return '\n'.join(['| '+' | '.join(headers)+' |', '|'+'|'.join('---' for _ in headers)+'|'] +
                     ['| '+' | '.join(str(c).replace('|', ' / ') for c in row)+' |' for row in rows])


def apply(root, sections, entries):
    src = root/'src/TerraKnight/Items'
    code = (src/'VanillaBossProgression.cs').read_text('utf-8-sig')
    pets = (src/'BossPetsData.cs').read_text('utf-8-sig')
    runtime = json.loads((root/'tools/encyclopedia_runtime.json').read_text('utf-8'))['data']
    item_data = runtime['items']
    by_id = {e['id']: e for e in entries}
    def item(t):
        assert 'item-'+t.lower() in by_id, t
        return f'[{item_data[t]["Name"]}](#item-{t.lower()})'
    def boss(k):
        key = 'enemy-'+k.lower().replace('@', '-')
        assert key in by_id, k
        name = by_id[key]['title'].split(' · ')[0]
        if k == 'Hornet@4': name = '大黄蜂（哨兵）'
        return f'[{name}](#{key})'
    def beat(*keys): return '击败'+('任意一位：' if len(keys)>1 else '：')+'、'.join(boss(k) for k in keys)
    later = '；达到更高世界战况也满足这一门槛'
    milestones = {
        'PreBoss': '初探圣巢', 'Slime': '初战告捷', 'Eye': '苍绿试炼已过',
        'Evil': '水道强敌已克', 'Dungeon': '深处试炼已过', 'Hardmode': '王国强敌已克',
        'MechOne': '三重试炼 · 其一', 'MechTwo': '三重试炼 · 其二', 'MechAll': '三重试炼已竟',
        'Plantera': '叛徒领主已败', 'Golem': '神之驯服者已败', 'Cultist': '空洞骑士已败',
        'Pillars': '神居试炼已进', 'MoonLord': '辐光已败',
    }
    mechs = ('Hornet@4', 'GhostMarkoth', 'InfectedKnight@5')
    conditions = {
        'Evil': beat('DungDefender', 'FlukeMother')+later,
        'Hardmode': beat('BlackKnight', 'JarCollector', 'MageLord@5', 'InfectedKnight')+later,
        'MechOne': beat(*mechs)+later,
        'MechAll': '全部击败：'+'、'.join(boss(k) for k in mechs)+later,
        'Plantera': beat('TraitorLord')+later,
        'Golem': beat('LobsterLancer')+later,
        'Cultist': beat('HollowKnight')+later,
    }
    # Every switch branch is consumed, including exact-boss gates that later victories cannot replace.
    allowed = code.split('internal static bool Allowed(ItemType t)', 1)[1].split('internal static void ApplyBands', 1)[0]
    gates = {}
    gate_rules = {}
    for m in re.finditer(r'((?:case ItemType\.\w+:\s*)+)return ([^;]+);', allowed):
        expression = m[2]
        exact = re.fullmatch(r'Has\("([^"]+)"\)', expression)
        step = re.fullmatch(r'Current>=Step\.(\w+)', expression)
        assert exact or step, expression
        condition = beat(exact[1])+'（须有该首领本人的胜利记录）' if exact else conditions[step[1]]
        for t in re.findall(r'ItemType\.(\w+)', m[1]):
            gates[t] = condition
            gate_rules[t] = ('boss', exact[1]) if exact else ('step', step[1])
    assert len(gates) == len(re.findall(r'case ItemType\.', allowed))
    profiles = []
    for line in code.splitlines():
        m = re.search(r'\["([\w@]+)"\]=P\((\d+),Step\.(\w+),"([^"]+)","([^"]+)",ItemType\.(\w+),(.+)\),?$', line.strip())
        if not m: continue
        key, band, step, role, upgrade, signature, tail = m.groups()
        gear = re.findall(r'ItemType\.(\w+)', tail.split(',M(', 1)[0])
        mats = re.findall(r'M\(ItemType\.(\w+),(\d+),(\d+)(?:,(\d+))?\)', tail)
        profiles.append(dict(key=key, band=int(band), step=step, role=role, upgrade=upgrade,
                             gear=gear, signature=signature, mats=mats))
    assert len(profiles) == len(re.findall(r'\]=P\(', code)), 'Incomplete boss profiles'
    stage_rows = [
        (0, '初入圣巢', '没有取得下列移动能力', '遗忘十字路', '铜、锡、铁、铅、银、钨、金、铂金；具体来源看物品页'),
        (1, '常规路线：蛾翼披风', '苍绿之径击败大黄蜂后取得', '苍绿之径、雾之峡谷', '魔矿、猩红矿与丛林原料；不等于已开放狱石'),
        (2, '常规路线：螳螂爪', '螳螂村拾取；不要求击败螳螂领主', '真菌荒地、泪水之城、皇家水道、水晶山峰', '水道强敌后开放普通狱石、陨石来源；骨头、蜂蜡等另需对应首领'),
        (3, '常规路线：水晶之心', '水晶山峰拾取', '深邃巢穴、古老盆地及中期探索区', '王国强敌后开放钴、钯、秘银、山铜、精金、钛金矿及相应锭和困难材料'),
        (4, '常规路线：帝王之翼', '古老盆地击败残破容器后取得', '王国边缘、蜂巢及三重试炼路线', '首位试炼首领后神圣锭；三位全部完成后叶绿矿、叶绿锭'),
        (5, '常规路线：暗影披风', '王国边缘击败大黄蜂取得王者之印，再进入深渊取得', '王后花园、终局与神居', '叛徒领主后灵气、幽灵/蘑菇矿加工；其后甲虫、天界碎片、夜明矿依次解锁'),
    ]
    intro = ('能力阶段与首领战况是两套同时生效的条件。阶段 0–5 实际按已取得的五种不同移动能力数量计算：'
             '蛾翼披风、螳螂爪、水晶之心、帝王之翼、暗影披风；取得顺序可以不同。下表是常规路线示例，'
             '不会因为先拿到某一种能力就直接跳到它在常规路线中的编号。矿物来源还受地区和具体敌人影响。\n\n')
    sections['progress']['body'] = intro+table(['能力阶段','常规节点','取得方式','常见探索区域（建议）','资源进展概要'], stage_rows)+\
        '\n\n[阶段内首领与材料解锁全表](#boss-progression) · [全部矿石与额外条件](#ore-unlocks) · [宠物完整掉率](#pet-drops) · [回退掉落阶段](#bestiary-progress)'
    boss_rows = []
    for p in profiles:
        materials = '、'.join(item(t)+' ×'+(lo if lo == hi else lo+'–'+hi)+('（同组任选）' if group else '') for t,lo,hi,group in p['mats']) or '无专属固定材料'
        gear = p['gear']+([p['signature']] if p['signature'] != 'None' else [])
        boss_rows.append((boss(p['key']), p['band'], milestones[p['step']], p['role'], materials,
                          '、'.join(item(t) for t in gear) or '见材料与额外掉落', p['upgrade']))
    body = intro+'## 每位首领的阶段、材料与装备\n\n推荐能力阶段不等于击杀硬性门槛。材料数量为普通难度基础值；难度和神居奖励会调整数量。装备列是奖励池，不表示整列每次同时掉落。\n\n'+table(
        ['圣巢首领','推荐能力阶段','奖励所属战况','泰拉进度对应','基础材料奖励','装备与标志奖励','制作与下一步'], boss_rows)
    body += ('\n\n## 哪些胜利会真正推进战况\n\n战况取当前已记录胜利中的最高档，不是必须依序击败所有前辈。'
             '某个首领奖励属于一个档次，不代表它的胜利会推进到那个档次；例如白色防御者不代替三重试炼，'
             '格林、灰色王子和梦魇之王不代替叛徒领主。\n\n')
    raises = re.findall(r'Raise\(Step\.(\w+),([^;]+?)\);', code)
    world_rows = [(milestones[step], '、'.join(boss(k) for k in re.findall(r'"([\w@]+)"', keys)), '任意一位即可') for step,keys in raises]
    world_rows[5:5] = [(milestones['MechOne'], '、'.join(boss(k) for k in mechs), '三位中的任意一位'),
                           (milestones['MechTwo'], '、'.join(boss(k) for k in mechs), '三位中的任意两位'),
                           (milestones['MechAll'], '、'.join(boss(k) for k in mechs), '三位全部完成')]
    body += table(['圣巢战况','会推进此档的胜利','要求'], world_rows)
    raise_keys = {step:set(re.findall(r'"([\w@]+)"', keys)) for step,keys in raises}
    for stage in range(6):
        rows = []
        for p in profiles:
            if p['band'] != stage: continue
            newly = [t for t,(kind,key) in gate_rules.items() if
                     (kind == 'boss' and key == p['key']) or
                     (kind == 'step' and p['key'] in raise_keys.get(key,set())) or
                     (kind == 'step' and key == 'MechOne' and p['key'] in mechs)]
            note = '、'.join(item(t) for t in newly) or '无新的通用矿石/材料许可；仍有本人的固定奖励与装备池'
            if p['key'] in mechs:
                note += '；若为三重试炼最后一位，另开放'+item('ChlorophyteOre')+'、'+item('ChlorophyteBar')
            rows.append((boss(p['key']),note,p['upgrade']))
        key = 'boss-stage-'+str(stage)
        sections[key] = dict(id=key,title=f'能力阶段 {stage}：各首领与材料解锁',body=
            '[返回全部阶段](#progress) · [首领奖励数量与装备全表](#boss-progression)\n\n'
            '同一能力阶段内部仍按首领战况继续推进。下列为这一阶段推荐挑战的首领；通用材料还需要它自己的物品阶段和来源。高档战况会满足较低的“战况达到”条件，但不代替表中指定首领本人的记录。\n\n'+
            table(['首领','这场胜利新增的普通获取许可','固定奖励用途与制作方向'],rows))
    stage_links = ' · '.join(f'[阶段 {s} 的逐首领解锁](#boss-stage-{s})' for s in range(6))
    sections['progress']['body'] += '\n\n'+stage_links
    body = stage_links+'\n\n'+body
    body += ('\n\n通常只有世界战胜利计入；普通神居复战不补世界战况。空洞骑士进度后，'
             '奥罗与马托、斯莱、席奥、纯粹容器这四场对应四柱的神居胜利可推进并分别登记。'
             '旧存档已有击杀记录会迁移保留。左特与苍白潜伏者没有独立世界战况档；战斗姐妹、有翼诺斯克、暴怒守卫与无上辐光沿用基础首领记录，不另设一档矿物许可。\n\n'
             '“前辈”并非独立的通用门槛：请按上表首领姓名判断。叛徒领主之前和之后，即使都在能力阶段 5，普通掉落和制作可用材料仍会变化。\n\n'
             '[全部矿石门槛](#ore-unlocks) · [逐项材料与装备条件](#equipment-gates) · [首领宠物](#pet-drops)')
    sections['boss-progression']['body'] = body
    ores = [t for t in item_data if t.endswith(('Ore','Bar')) or t in ('Hellstone','Meteorite','Obsidian','ChlorophyteOre','LunarOre')]
    ores = [t for t in ores if not t.startswith(('Ore','Health')) and 'item-'+t.lower() in by_id]
    ore_rows = [(item(t), item_data[t]['Stage'], gates.get(t, '无额外首领胜利门槛；仍需满足本物品阶段、地区与来源条件')) for t in sorted(ores, key=lambda t:(item_data[t]['Stage'], item_data[t]['Name']))]
    sections['ore-unlocks'] = dict(id='ore-unlocks', title='全部矿石、金属锭与解锁条件', body=
        intro+'下表是普通获取路径的条件，开放不等于所有怪物都会掉。点击物品查看对应敌人、地区、数量和配方。固定首领材料奖励按首领自己的奖励规则处理。\n\n'+
        table(['矿石 / 锭','物品所需能力阶段','额外战况条件'], ore_rows)+'\n\n'+
        '神圣锭不是矿石；灵气、断裂英雄剑、甲虫鞘翅与四柱碎片也各有独立条件，见[全部材料与装备门槛](#equipment-gates)。')
    # Replace the old generalized explanation on each gated item with exact release rules.
    for t,condition in gates.items():
        e = by_id['item-'+t.lower()]
        e['body'] = re.sub(r'\n\n## 额外获取条件\n\n.*?(?=\n\n## |\Z)', '', e['body'], flags=re.S)
        e['body'] += '\n\n## 额外获取条件\n\n'+condition+'。同时需要物品所需能力阶段与实际来源条件；固定首领奖励按其独立规则处理。\n\n[全部矿物门槛](#ore-unlocks) · [阶段内首领路线](#boss-progression)'
    sections['equipment-gates']['body'] = ('能力阶段和世界战况同时检查。时装免除装备的首领门槛；法术奖励装备按其法术解锁。'
        '其他非专属普通装备：阶段 0–2 无额外世界战况，阶段 3–4 需要王国强敌或更高战况，阶段 5 需要叛徒领主或更高战况。'
        '下表逐项列出额外材料和坐骑条件；专属装备本人条件见[首领奖励全表](#boss-progression)。\n\n'+
        table(['物品','所需能力阶段','额外条件'], [(item(t),item_data[t]['Stage'],c) for t,c in gates.items()]))
    pet_rows = []
    parsed_pets = []
    for line in pets.splitlines():
        m = re.search(r'new Row\(ItemType\.(\w+), "([^"]+)", "([^"]+)", (null|new\[\] \{ [^}]+ \}), Kind\.(\w+), \d+, \d+, \d+, (true|false), (\d+), "([^"]+)", "([^"]+)"\)', line)
        if not m: continue
        t,name,record,scenes,kind,light,stage,pet_name,item_name = m.groups()
        variant = {'SistersOfBattle':'战斗姐妹','WingedNosk':'有翼诺斯克','EnragedGuardian':'暴怒守卫','AbsoluteRadiance':'无上辐光'}.get(name)
        source = boss(record)+(f'（{variant}战斗变体）' if variant else '')
        scene_names = {'GG_Mantis_Lords_V':'神居·战斗姐妹','GG_Nosk_Hornet':'神居·有翼诺斯克',
                       'Mines_32':'水晶山峰·暴怒守卫','GG_Crystal_Guardian_2':'神居·暴怒守卫','GG_Radiance':'神居·无上辐光'}
        scene_text = '对应首领战；存在变体时以实际战斗区分' if scenes == 'null' else ' / '.join(scene_names[s] for s in re.findall(r'"([^"]+)"', scenes))
        pet_rows.append((item(t),pet_name,source,stage,'照明宠物' if light == 'true' else '观赏宠物',scene_text))
        parsed_pets.append((t,record,source,scene_text))
        e = by_id['item-'+t.lower()]
        e['body'] += '\n\n## 首领专属宠物掉率\n\n'+source+'的独立抽选，普通 1%、困难 1.1%、专家 1.2%、大师/空洞 1.3%，每次成功 ×1。大师品质不表示大师限定；不参与通用怪物 0.01% 宠物池。场景：'+scene_text+'。阶段数字是物品配置标签；专属宠物掉落行不再额外按该标签锁定。\n\n[全部宠物与概率口径](#pet-drops)'
    assert len(parsed_pets) == len(re.findall(r'new Row\(', pets)) == 48
    general = re.findall(r'\[ItemType\.(\w+)\]\s*=\s*new PetDef', (src/'PetsData.cs').read_text('utf-8-sig'))
    extras = code.split('Extras =',1)[1].split('internal static readonly Dictionary<string,(int Life',1)[0]
    boss_only = set()
    native_rows = []
    for record,values in re.findall(r'\["([^"]+)"\]=new\[\]\{([^}]+)\}', extras):
        for t,den in re.findall(r'\(ItemType\.(\w+),(\d+)\)', values):
            boss_only.add(t)
            if t in general:
                d = int(den)
                rates = ' / '.join(f'{min(100,100*s/d):.4g}%' for s in (1,1.1,1.2,1.3))
                native_rows.append((item(t),boss(record),rates))
    general = [t for t in general if t not in boss_only and t not in ('HkLumaflyLantern','HkGrimmchild')]
    # Match BossDropPlan.BossOnly, including authored gear and expert-exclusive items.
    reserved = {t for p in profiles for t in p['gear']+[p['signature']]}
    balance = (src/'VanillaProgressionMapping.cs').read_text('utf-8-sig')
    exclusive = balance.split('ExclusiveBossItems',1)[1].split('};',1)[0]
    reserved.update(re.findall(r'ItemType\.(\w+)', exclusive))
    general = [t for t in general if t not in reserved]
    pet_body = ('本页区分三种宠物来源，不能把首领专属宠物写成通用彩蛋掉率。\n\n'
        '## 48 种圣巢首领专属宠物\n\n每种只由对应首领或指定战斗变体提供；每次成功 ×1。'
        '基础概率 1/100，即普通 1%，困难 1.1%，专家 1.2%，大师和空洞 1.3%。'
        '品质标记为大师，但没有大师限定判定。阶段列为物品配置标签，专属宠物的抽选行本身不额外检查该标签。'
        '同一记录的变体按场景替换宠物，不会同时抽本体和变体两种。'
        '专家及以上对应首领奖励进入宝藏袋，宠物在开袋时抽选；非宝藏袋战斗直接随掉落结算。\n\n'
        '**5.1.0 的变体宝藏袋限制**：宝藏袋只保留基础首领记录，开袋时不恢复原战斗场景。因此战斗姐妹、有翼诺斯克、暴怒守卫、无上辐光的袋子目前会抽基础首领宠物，不能据此刷到对应变体宠物。下表场景变体对应直接掉落表；此限制需要游戏更新修复。\n\n'+
        table(['召唤物','宠物','来源首领','配置阶段','种类','战斗场景说明'], pet_rows)+
        '\n\n## 泰拉原生的首领专属宠物\n\n这些宠物也不进入通用池，按下表自己的概率抽取。概率顺序为普通 / 困难 / 专家 / 大师与空洞；神居低进度奖励替换仍受其首领奖励规则影响。\n\n'+
        table(['宠物召唤物','对应圣巢首领','各难度概率'], native_rows)+
        f'\n\n## 通用击杀彩蛋宠物（{len(general)} 种）\n\n正常结算且有掉落表的已登记怪物，包括首领，可触发一次共享抽选。'
        '每一种候选宠物为 1/10000（0.01%），不是整个池子合计 0.01%；一次最多获得一种。'
        '不乘普通掉落的难度倍率，也不按宠物配置阶段筛选。若抽到被禁止直接掉落的候选则本次不发放，不重新抽。\n\n'+
        table(['召唤物','单种每次有效击杀概率'], [(item(t),'0.01%（1/10000），×1') for t in general])+
        '\n\n## 宝匣与特殊宠物\n\n钓鱼宝匣另有 1/50（2%）触发的共享宠物抽选，成功后从通用宠物候选中等概率选一种，单种为 2% ÷ 候选种数；候选与最终发放仍受其规则检查。见各[宝匣词条](#fishing)，不能把 0.01% 击杀概率套到开箱。'
        '首领专属宠物被排除在普通宝匣池外。光蝇灯笼由斯莱出售，格林之子按其剧情/护符规则取得，均不在通用彩蛋池。')
    sections['pet-drops'] = dict(id='pet-drops',title='宠物来源、首领专属与完整掉率',body=pet_body)
    for t in general:
        by_id['item-'+t.lower()]['body'] += '\n\n[全部宠物来源与掉率说明](#pet-drops)'
    for record in {r for _,r,_,_ in parsed_pets}:
        by_id['enemy-'+record.lower().replace('@','-')]['body'] += '\n\n## 专属宠物\n\n'+\
            '、'.join(item(t) for t,r,_,_ in parsed_pets if r == record)+'。普通 1%、困难 1.1%、专家 1.2%、大师/空洞 1.3%；同记录变体按实际战斗场景区分。\n\n[全部宠物与变体条件](#pet-drops)'
    sections['bestiary-progress'] = dict(id='bestiary-progress', title='猎人手记、实际掉落阶段与首领战况', body=
        intro+'## 实际掉落与探索预览\n\n图鉴里的“实际掉落”可以选择“跟随进度”或已经到达过的较早能力阶段；不能选择尚未解锁的未来阶段。'
        '选择会改变真正击杀掉落与相应的神居额外装备来源，选择随存档保存。它不会降低角色或敌人的数值，也不会回退能力、首领胜利、商店或制作进度。'
        '较早阶段的掉落仍检查当前真实首领战况，不会撤销已经获得的材料许可。世界首领固定奖励至少按其自身奖励阶段处理，不能靠回退改成低阶首领包。'
        '联机时由房主选择，加入者跟随房主。\n\n“探索预览”只改变查阅条件，不改真实掉落。当前可得按发现记录与当前条件展示，全部资料可以查询尚未发现或未开放来源。'
        '能力阶段变化与有效世界首领胜利会在左下角以圣巢说法通知。\n\n'
        '[全部阶段与首领解锁](#boss-progression) · [矿石与金属锭门槛](#ore-unlocks) · [宠物掉率](#pet-drops)')
    sections['systems']['body'] = re.sub(r'\*\*宠物\*\*：.*?(?=\*\*鱼饵与苔藓\*\*)',
        '**宠物**：首领专属、通用彩蛋、宝匣与剧情来源规则不同，请查阅[全部宠物与掉率](#pet-drops)。\n\n', sections['systems']['body'], flags=re.S)
    sections['reading']['body'] = sections['reading']['body'].replace('开发版猎人手记与双重进度说明','猎人手记与阶段回退说明')
    for key in ('knowledge','systems'):
        sections[key]['body'] = '[宠物完整掉率](#pet-drops) · [全部矿石解锁](#ore-unlocks) · [阶段内首领推进](#boss-progression)\n\n'+sections[key]['body']
    return {'boss_profiles':len(profiles),'boss_pets':len(parsed_pets),'general_pets':len(general),'mineral_rows':len(ore_rows),'explicit_gates':len(gates)}
