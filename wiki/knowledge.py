"""Current-source additions to the encyclopedia, split into bounded topic pages."""
from collections import defaultdict
from pathlib import Path
import html
import json
import re


def load(root, sections, entries):
    report = {'dialogue': 0, 'ai_profiles': 0, 'ai_profile_pages': 0, 'enemy_ai_pages': 0, 'native_shops': 0}
    path = root/'tools/encyclopedia_knowledge.json'
    if not path.exists():
        raise FileNotFoundError('Refresh the current isolated knowledge export first')
    data = json.loads(path.read_text('utf-8'))
    report['ai_profiles'] = len(data['profiles'])
    details = json.loads((root/'tools/encyclopedia_details.json').read_text('utf-8'))
    def source(rel, label='实现源码'):
        return f'[{label}]({rel})'
    def add(key, title, body):
        assert key not in sections, key
        sections[key] = dict(id=key, title=title, body=body)
    def cell(s):
        return html.escape(str(s)).replace('|', '&#124;').replace('\n', '<br>')
    def table(headers, rows):
        return '\n'.join(['| '+' | '.join(headers)+' |', '|'+ '|'.join('---' for _ in headers)+'|',
                          *['| '+' | '.join(cell(c) for c in row)+' |' for row in rows]])
    kind_names={'Lunge':'直线冲锋','Chase':'突进追击','Leap':'跳扑','Dive':'俯冲','Stalk':'等待落点并逼近','Blink':'传送','Shot':'发射弹体','Volley':'齐射','Burst':'爆发射击','Fan':'扇形散射','Rain':'落弹','Beam':'激光束','Wave':'冲击波','Pulse':'范围脉冲','Pillar':'地柱','Pool':'地面残留','Summon':'召唤','Own':'调用原生招式','Shell':'硬壳防御'}
    field_names={'Kind':'招式类型','Tier':'难度层级','Weight':'选招权重','Cooldown':'冷却秒数','Min':'最小距离','Max':'最大距离','Sight':'需要视线','Rel':'玩家相对位置','When':'玩家状态条件','Antic':'基础前摇秒数','Recover':'基础恢复秒数','Lead':'预判系数','Speed':'移动速度','Time':'运动时长','Height':'高度','Vertical':'允许垂直运动','Heavy':'击落坐骑','CutHook':'切断钩索','LandWave':'落地产生冲击波','LandWaveSpeed':'落地冲击波速度','Repeat':'重复次数','Count':'数量','Spread':'散射角度','Interval':'间隔秒数','Gravity':'弹体重力','Homing':'追踪强度','Life':'弹体存活秒数','Size':'弹体大小','ShotSpeed':'弹速','Aim':'瞄准方式','Look':'视觉主题','Land':'落地效果','Bounces':'反弹次数','Radius':'半径','Width':'宽度','Length':'长度','Duration':'持续秒数','Delay':'延迟秒数','Drift':'漂移','Sweep':'扫动','Masks':'伤害标记','Debuff':'减益 ID','DebuffTicks':'减益持续帧数','OwnState':'原生起始状态','OwnFsm':'原生 FSM','OwnFrom':'允许切入的原生状态','Sight':'视距或视线条件','Hear':'听觉范围','Memory':'记忆秒数','Flyer':'飞行单位','Alarm':'警报范围','AimLead':'预判秒数','Gap':'最短出招间隔','Reflect':'硬壳反弹'}
    def paged(prefix, title, rows, render_row, size=32):
        pages = []
        for start in range(0, len(rows), size):
            key = f'{prefix}-{start//size+1}'
            pages.append((key, f'{title} · {start+1}–{min(len(rows),start+size)}'))
        for i, (key, name) in enumerate(pages):
            nav = ' · '.join(f'[{label}](#{target})' for target,label in pages[max(0,i-1):i+2])
            add(key, name, f'[返回总览](#{prefix}) · {nav}\n\n' + '\n\n'.join(render_row(r) for r in rows[i*size:(i+1)*size]))
        add(prefix, title, f'共 {len(rows)} 条。每页最多 {size} 条。\n\n'+'\n'.join(f'- [{label}](#{key})' for key,label in pages))

    # Exported Language.Get includes identity replacements and inscriptions.
    sheets = defaultdict(list)
    for row in data['dialogue']: sheets[row['Sheet']].append(row)
    report['dialogue'] = len(data['dialogue'])
    links = []
    for sheet, rows in sorted(sheets.items()):
        prefix = 'text-'+re.sub(r'[^a-z0-9_-]', '-', sheet.lower())
        def dialogue_row(r):
            changed = ' · **新版改写**' if r['Changed'] else ''
            text = html.escape(r['Text']).replace('\n','<br>')
            en = ('\n\n英文：'+html.escape(r['English'])) if r.get('English') else ''
            return f'### {cell(r["Id"])}{changed}\n\n{text}{en}'
        paged(prefix, f'{sheet} 文本与台词', rows, dialogue_row)
        links.append((sheet,len(rows),f'[{sheet}](#{prefix})'))
    add('dialogue', '全部新版文字、台词与石碑',
        '以下是当前中文游戏通过 `Language.Get` 实际取得的完整文本，包含石碑、梦境、剧情、商店说明、确认框及界面。标记“新版改写”的条目使用泰拉人身份；英文仅列已撰写的新文本。\n\n'+
        table(['文本表','条数','入口'],links).replace('&#91;','[')+'\n\n'+source('src/TerraKnight/UI/TerrarianDialogue.cs'))
    # Preserve real links in generated tables (cell() only escapes HTML and pipes).
    mod_text = json.loads((root/'src/TerraKnight/Resources/lang.json').read_text('utf-8-sig'))
    paged('mod-text', 'Mod 全部文字与城镇台词', list(mod_text.items()),
          lambda row: '### '+cell(row[0])+'\n\n'+table(['语言','文本'], sorted(row[1].items()) if isinstance(row[1],dict) else [('文本',row[1])]),size=24)
    sections['dialogue']['body'] += '\n\n[Mod 全部文字与城镇台词](#mod-text) · [36 条新任务鱼台词](#quest-dialogue)'
    qsrc = (root/'src/TerraKnight/Items/QuestFishCatalog.cs').read_text('utf-8-sig')
    string = r'"((?:\\.|[^"\\])*)"'
    quest_rows=[]
    for m in re.finditer(r'new Entry\(ItemType\.(\w+), (\d+), '+', '.join([string]*7)+r'\)',qsrc):
        vals = [json.loads('"'+s+'"') for s in m.groups()[2:]]
        quest_rows.append((m[1],m[2],*vals))
    assert len(quest_rows)==36, len(quest_rows)
    add('quest-dialogue','渔夫的 36 条新任务鱼提示',
        '每天积累一个任务名额，上限三个；交鱼后抽取下一任务，候选多于一条时排除刚交过的鱼。任务仅从当前能力阶段允许的鱼中抽取。持有任务鱼、没有任务名额或地区不符时不能再钓同一任务鱼。\n\n'+
        '\n\n'.join(f'### {name} · 阶段 {stage} · {area}\n\n[物品详情](#item-{key.lower()})\n\n物品提示：{tip}\n\n渔夫：{chat}' for key,stage,area,name,en,tip,entip,chat,enchat in quest_rows)+
        '\n\n'+source('src/TerraKnight/Town/Angler.cs'))

    # Exact slots distribution comes from the live payout function, all 125 stop tuples.
    casino=data['casino']; slots={int(k):v for k,v in casino['slots'].items()}; stake=casino['stakeCopper']
    assert sum(slots.values())==125
    rtp=sum(k*v for k,v in slots.items())/125/stake
    add('casino','三台赌博机器：规则、概率与返还',
        '所有倍率均含本金；净收益为返还减去下注。赌场售价 5000 吉欧，由税收官出售；也可拆下机器重新摆放。筹码、飞行中的弹珠与奖金属于角色。\n\n'
        '## 老虎机\n\n三个轮轴各有五个等概率停点，共 125 种结果。结算中线、上下线及两条对角线；只有中线允许前两格相同获得对子返还。各线共享停点，不能当成五次独立抽奖。\n\n'+
        f'返还期望 **{rtp:.4%}**；净收益期望 **{rtp-1:.4%}**。获得任何返还 {sum(v for k,v in slots.items() if k>0)}/125；盈利 {sum(v for k,v in slots.items() if k>stake)}/125；持平 {slots.get(stake,0)}/125。\n\n'+
        table(['返还倍率','结果数 / 125','精确概率'],[(f'{k/stake:g}×',v,f'{v}/125 = {v/125:.2%}') for k,v in slots.items()])+
        '\n\n## 红黑绿转盘\n\n65 个等概率扇区：绿色 1 个，红色与黑色各 32 个。\n\n'+
        table(['押注','命中概率','返还倍率','返还期望'],[('红','32/65','1.97×',f'{32/65*1.97:.6%}'),('黑','32/65','1.97×',f'{32/65*1.97:.6%}'),('绿','1/65','63×',f'{63/65:.6%}')])+
        '\n\n## 弹珠机\n\n弹珠真实参与风车、挡板、钉子及球与球碰撞，发射力度、机器时钟、随机种子和同时在场的球都会影响落点。没有固定的十一槽概率。金杯支付 3×；彩虹杯生成两颗免费球，每个付费球家庭最多六颗免费球，整个家庭落完才结算。\n\n'+
        table(['槽位（从左到右）','返还倍率'],[(i+1,f'{n/10:g}×') for i,n in enumerate(casino['channelTenths'])])+
        '\n\n[弹珠物理统计及条件](#pinball-statistics)\n\n'+source('src/TerraKnight/Items/Casino.cs')+' · '+source('src/TerraKnight/Items/CasinoPinSim.cs'))
    pinbody='每种力度 2000 次独立单球家庭；固定种子序列，起始时钟均匀铺在 60 秒内，包含免费子球。**这些是指定协议的样本频率，不适用于连续自动发射或所有玩家。**\n\n'+casino['pinballProtocol']+'\n\n'
    for sample in casino['pinball']:
        counts={int(k):v for k,v in sample['PayoutTenths'].items()}; n=sample['Samples']
        mean=sum(k*v for k,v in counts.items() if k>=0)/10/n
        pinbody+=f'## 力度 {sample["Force"]} · {n} 个家庭\n\n观察返还均值 {mean:.4%}；退回发射器 {counts.get(-1,0)} 次。Replay 对未在 180 秒内完成的家庭会按 1× 回退，因此这不是无限时长的纯物理期望。\n\n'+table(['家庭返还倍率','次数','样本频率'],[(f'{k/10:g}×' if k>=0 else '退回',v,f'{v/n:.3%}') for k,v in counts.items()])+'\n\n'
    add('pinball-statistics','弹珠机物理统计',pinbody)

    # One AI companion per catalog record, and bounded native implementation excerpts.
    attack = (root/'docs/全怪物攻击方式与阶段彻查-2026-10-21.md').read_text('utf-8')
    native = (root/'docs/全怪物原生FSM逐项证据-2026-10-21.md').read_text('utf-8')
    native = native.replace('../artifacts/enemy-attack-audit/decompiled)', '#native-scripts)')
    scripts = sorted((root/'artifacts/enemy-attack-audit/decompiled').glob('*.cs'))
    add('native-scripts','原生敌人 C# 组件实现',
        '以下为本机游戏程序集反编译的组件；与原生 FSM 对象归属一起使用。\n\n'+'\n'.join('- '+source(p.relative_to(root).as_posix(),p.stem) for p in scripts))
    def split_records(text, level):
        matches=list(re.finditer(r'^'+level+r' \d+ (.+?) · ([\w@]+)\s*$',text,re.M))
        result = {}
        for i,m in enumerate(matches):
            body=text[m.end():matches[i+1].start() if i+1<len(matches) else len(text)].strip()
            body=re.split(r'^#{1,'+str(len(level)-1)+r'} ',body,maxsplit=1,flags=re.M)[0].strip()
            result[m[2]]=body
        return result
    old = split_records(attack,'####'); fsms=split_records(native,'##')
    kits = defaultdict(list)
    boss_notes={
        'Hornet':'投针可按前摇时的玩家位置定向，针抵达墙、地板或天花板再收回；空中突刺加入预判。二战继续利用刺球和高处丝线风暴压制空中玩家。',
        'FalseKnight':'原生锤击冲击波可以沿墙与天花板继续传播；仍使用原生岩刺、出招和受伤表现。',
        'BlackKnight':'弹跳滚动可预判落点；地面滚动延长，双体通过分工施压。',
        'DungDefender':'粪球按抛物线落向玩家或预判位置，落点有提示；地下停留时间变化。',
        'WhiteDefender':'继承防御者的投球与潜地改写；砸地时还会在玩家位置附近生成额外原生粪柱。',
        'FlukeMother':'幼体追击速度与加速度提高，孵化间隔缩短。',
        'BigFly':'保留冲撞和弹跳，缩短间歇；冲撞前显示锁定路线，高位玩家有专门应对。',
        'BigBuzzer':'俯冲低点按玩家运动预判，前摇显示路线；更高阶可接续第二次俯冲，并限制同时召出的幼体。',
        'Mawlek':'吐液按抛物线瞄准落点，落点有提示；保留原生飞液与落地溅射。',
        'MageKnight':'传送下劈可预判玩家位置；可在空中玩家所在高度出现并横向冲刺。',
        'MantisLord':'落刺锁定提示时的位置，之后不持续追踪；缩短原生招式间隔，并保留多体阶段组织。',
        'MageLord':'传送后可追加追踪灵魂球；震地落点加入预判与原生阶段衔接。',
        'MegaBeamMiner':'激光前摇跟踪玩家，在释放前才锁定方向，可针对房间内远处或高处玩家。',
        'MegaJellyfish':'连续电击按每次生成时玩家的位置落下，并保留足够的预警时间。',
        'MegaMossCharger':'出土不再只依赖原先局部警戒范围；跳跃更高，并向玩家位置组织突袭。',
        'JarCollector':'逐罐锁定玩家及其附近位置，落罐前显示落点；从顶部斜向落向玩家。',
        'MimicSpider':'顶部落下与滴液瞄准玩家，先显示落点；本体与有翼变体由场景和原生状态区分。',
        'InfectedKnight':'地面与空中冲刺在前摇时定向，部分使用预判；空中冲刺允许斜向。',
        'HiveKnight':'蜂群以玩家位置为中心形成有空隙的下落帘幕，预告位置；跳跃后追加蜂群。',
        'Oblobble':'飞行与弹幕节奏加快，每轮首发瞄准前摇时的位置，存活者继续使用狂暴路径。',
        'TraitorLord':'跳跃顶点显示俯冲路线；玩家在同高或更高处时先向上突进，再提示并俯冲。',
        'Grimm':'火蝠加入运动预判，俯冲与突进显示路线，地刺和上勾拳针对高处玩家。',
        'NightmareGrimm':'继承剧团长的强化规则，并改写梦魇火柱等专属招式。',
        'GreyPrince':'悬停后向锁定落点横移再砸下，并显示地面提示；跳跃可针对平台上的玩家。',
        'NailBros':'冲刺骨钉技可向预判位置延伸，蓄力时显示路线；双大师保持各自招式与协同。',
        'Nailsage':'冲刺斩可越过预判位置；跳跃和骨钉技针对高处玩家。',
        'Paintmaster':'跳斩、刺击、横扫与大斩的原生颜料中追加瞄准玩家的颜料，针对空中目标上抛。',
        'HollowKnight':'保留原生三段流程和原生画面；跳跃与下砸加入预判，缩短间歇。',
        'HollowKnightPrime':'聚集爆炸轮流在玩家位置附近生成，保留原生膨胀爆炸表现，并加强原生对空应对。',
        'FinalBoss':'原生攻击节奏加快；中后段在横扫间加入原生追踪球，普通辐光与无上辐光沿各自状态继续运行。'
    }
    for p in sorted((root/'src/TerraKnight/EnemyAi/Bosses/Kits').glob('*.cs')):
        text=p.read_text('utf-8-sig')
        # Each [Kit] decorated class is bounded by the next class.
        matches=list(re.finditer(r'((?:\s*\[Kit\("[^"]+"\)\])+)[\s\S]*?class (\w+)\s*:',text))
        for mi,m in enumerate(matches):
            keys=re.findall(r'Kit\("([^"]+)"\)',m[1])
            # Full source is linked; its authored summary explains attacks and hooks below show the concrete state edits.
            before=text[:m.start()]; summary=re.findall(r'/// <summary>(.*?)/// </summary>',before,re.S)
            notes=re.sub(r'\s*///\s?', ' ', summary[-1]).strip() if summary else m[2]
            class_end=matches[mi+1].start() if mi+1<len(matches) else len(text)
            hooks=re.findall(r'\b(Tempo|OnEnter|Branch|At|Every|After)\(([^\n]+)',text[m.end():class_end])
            for key in keys: kits[key].append((p.relative_to(root).as_posix(),notes,hooks))
    ai_links=[]
    for enemy in [e for e in entries if e['kind']=='怪物']:
        key=enemy['id'].removeprefix('enemy-'); record=next((r for r in old if r.lower().replace('@','-')==key),None)
        if record is None: record=next((r for r in data['profiles'] if r.lower()==key),key)
        base=record.split('@')[0]; page='ai-'+key
        body='[返回怪物数值与掉落](#'+enemy['id']+') · [AI 总览](#enemy-ai)\n\n## 原生 / 旧版攻击\n\n'+old.get(record,old.get(base,'此记录没有独立研究条目；见原生 FSM 与当前源码。'))
        profile=data['profiles'].get(record,data['profiles'].get(base)); kit=kits.get(base,[])
        if profile:
            report['ai_profile_pages']+=1
            body+='\n\n## 新版 AI\n\n距离单位为泰拉格，速度为格/秒，时间为秒。以下为基础参数；难度系数、视线、攻击令牌及冷却共同决定实际出招。\n\n'+table(['感知/行为参数','值'],[(field_names.get(k,k)+' · '+k,v) for k,v in profile.items() if k not in ('Moves','Key')])
            for i, move in enumerate(profile['Moves']):
                body+=f'\n\n### 新招 {i+1} · {kind_names.get(move["Kind"],move["Kind"])} · {move["Tier"]}\n\n'+table(['参数','值'],[(field_names.get(k,k)+' · '+k,v) for k,v in move.items()])
        elif kit:
            body+='\n\n## 新版 Boss 攻击与状态改写\n\n这些是当前实现的攻击说明与 FSM 挂接点；分支条件、弹体参数及清理逻辑可查看链接的完整源码。\n\n'
        else:
            body+='\n\n## 新版 AI\n\n没有匹配到独立普通怪 Profile 或 Boss Kit。保持原生行为，或由召唤物/特殊脚本路径控制；不能据此推断它没有攻击。\n\n'
        for rel,notes,hooks in kit:
            body+=boss_notes.get(base,'具体攻击按下列原生状态挂接点与实现说明执行。')+'\n\n<details><summary>实现中的原始说明</summary>\n\n'+html.escape(notes)+'\n\n</details>\n\n'+source(rel)+'\n\n'+table(['挂接类型','参数（源码）'],hooks)+'\n\n'
        evidence=fsms.get(record,fsms.get(base))
        if evidence:
            # Split by object/FSM heading and additionally cap long individual evidence units.
            units=re.split(r'(?=^### )',evidence,flags=re.M)
            chunks=[]; current=''
            for unit in units:
                for start in range(0,len(unit),12000):
                    piece=unit[start:start+12000]
                    if len(current)+len(piece)>16000 and current: chunks.append(current);current=''
                    current+=piece+'\n\n'
            if current:chunks.append(current)
            for i,chunk in enumerate(chunks):
                eid=f'fsm-{key}-{i+1}'
                add(eid,f'{enemy["title"]} · 原生 FSM 证据 {i+1}/{len(chunks)}',f'[返回攻击说明](#{page})\n\n资源初值和启用状态不等于实际战斗常量；跨 FSM 事件及动态写入需结合源码。\n\n'+chunk)
            body+='\n\n## 原生实现证据\n\n'+'\n'.join(f'- [FSM 证据 {i+1}/{len(chunks)}](#fsm-{key}-{i+1})' for i in range(len(chunks)))
        add(page, enemy['title']+' · 老版与新版 AI',body)
        enemy['body']+='\n\n## 攻击方式与 AI 实现\n\n[查看原生攻击、新版招式参数与 FSM 证据](#'+page+')'
        ai_links.append(f'- [{enemy["title"]}](#{page})')
        report['enemy_ai_pages']+=1
    add('enemy-ai','全部敌人的老版与新版 AI',
        '旧版以原生 PlayMaker FSM 为基础；新版由 Profile/Brain 与 Boss Kit 添加招式、感知和状态挂接。存档选择新版模式才启用；联机由权威端执行，远端傀儡同步结果。普通怪 All 招式用于全部难度，Expert 从专家起开放，Master 从大师起开放。\n\n'
        '## 难度共通系数\n\n'+table(['难度','预判权重','前摇系数','冷却系数','身体招式距离系数','同时攻击令牌'],[
            ('普通',.25,1.25,1,1,2),('困难',.45,1.1,1,1,2),('专家',.65,1,.85,1.25,3),('大师',.8,.9,.75,1.4,4),('空洞',.9,.85,.65,1.5,5)])+
        '\n\n前摇最短 0.3 秒；Boss 远程/锁定预警最短 0.4 秒。大师及空洞开放切钩、惩罚冲刺和击落坐骑等规则。\n\n'+source('src/TerraKnight/EnemyAi/EnemyAi.cs')+' · '+source('src/TerraKnight/EnemyAi/Brain.cs')+' · '+source('src/TerraKnight/EnemyAi/Bosses/BossAi.cs')+'\n\n'+'\n'.join(ai_links))

    # Probability rules and order, separate from finite-sample regional catch frequencies.
    fsrc=(root/'src/TerraKnight/Items/Fishing.cs').read_text('utf-8-sig')
    rule_src=(root/'src/TerraKnight/Items/FishingData.cs').read_text('utf-8-sig')
    conds={int(bit):name for name,bit in re.findall(r'(\w+)\s*=\s*1UL\s*<<\s*(\d+)',fsrc)}
    rules=[]
    for m in re.finditer(r'new Rule\((\d+), (true|false), (true|false), (\d+), (\d+), (\d+), 0x([0-9A-Fa-f]+)UL, ItemType\.(\w+), new\[\] \{([^}]+)\}',rule_src):
        if m[8]!='None':continue # retired vanilla quest rules
        flags=int(m[7],16); flags='、'.join(name for bit,name in conds.items() if flags & 1<<bit)
        rules.append((m[1],flags,m[4],f'{m[5]}/{m[6]}','、'.join(re.findall(r'ItemType\.(\w+)',m[9])),m[3]))
    add('fishing-rules','钓鱼精确判定、概率与规则顺序',
        '钓鱼存在多个层次：咬钩检查、稀有度独立掷骰、宝匣掷骰、地区/液体筛选、按顺序尝试物品规则、收线断线。单条规则的几率不是最终物品概率。\n\n'
        '设有效渔力为 F（整数，池塘与幸运修正后至少 1）：\n\n'+table(['事件','精确概率'],[
        ('普通稀有度','1 / max(2, floor(150/F))'),('不常见','1 / max(3, floor(300/F))'),('稀有','1 / max(4, floor(1050/F))'),('非常稀有','1 / max(5, floor(2250/F))'),('传奇','1 / max(6, floor(4500/F))'),('宝匣骰','15%；宝匣药水时 30%'),('通行骰','min(100, floor((F+75)/2)+1) / 100'),('无高强度钓鱼线的收线断线','1/7')])+
        '\n\n原生池塘不受面积惩罚；玩家倒水池少于 75 格无法钓鱼，75–299 格按格数/300 降渔力。阶段 3 开始使用困难模式鱼池。酸水需要酸钓装备或鱼饵；蜂巢水按蜂蜜规则。区域任务鱼优先于普通鱼：任务有效、地区相符、触发不常见骰、不是垃圾/宝匣/蜂蜜判定，酸水还要有装备。\n\n'
        '天气、时段、月相与幸运会改变 F，因此下方区域抽样只对应表中给定条件。鱼箱以 1/25（宝匣药水时 1/12）替换普通收获，不能替换任务鱼。稀有度编号：0 必中，1 普通，2 不常见，3 稀有，4 非常稀有，5 传奇，6 不常见且非非常稀有/传奇，其余为不常见或普通。酸水中原先必中的 lava 规则改为 1/2。地区宝匣另有优先替换路径；所有已废弃原版任务鱼规则均跳过。\n\n'+
        table(['顺序','条件位','稀有度','规则自身概率','候选物品内部 ID','终止规则'],rules)+
        '\n\n[分阶段分地区实测概率](#fishing) · [新任务鱼提示](#quest-dialogue)\n\n'+source('src/TerraKnight/Items/Fishing.cs')+' · '+source('src/TerraKnight/Items/FishingData.cs'))
    sections['fishing']['body']='[精确概率公式与全部捕获规则](#fishing-rules) · [36 条任务鱼提示](#quest-dialogue)\n\n'+sections['fishing']['body']
    # Each area/crate/chest is a topic. Do not render scores of tables on one page.
    fishing=sections['fishing']['body']; groups=list(re.finditer(r'^### (.+)$',fishing,re.M))
    overview=fishing[:groups[0].start()] if groups else fishing
    for gi,group in enumerate(groups):
        end=groups[gi+1].start() if gi+1<len(groups) else len(fishing)
        block=fishing[group.end():end]; topics=list(re.finditer(r'^#### (.+)$',block,re.M))
        overview+='\n\n### '+group[1]+'\n\n'+(block[:topics[0].start()] if topics else block)
        for i,topic in enumerate(topics):
            key=f'fishing-topic-{gi+1}-{i+1}'
            body=block[topic.end():topics[i+1].start() if i+1<len(topics) else len(block)]
            add(key,topic[1]+' · '+group[1],
                '[返回钓鱼与宝匣目录](#fishing) · [精确判定公式](#fishing-rules)\n\n'
                '以下为当前隔离导出的样本频率：钓鱼渔力 100、每组 3000 次模拟，宝匣每组 600 次开启，地区储物箱每组 300 次开启；不是所有渔力、天气、幸运及任务状态下的固定掉率。\n\n'+body)
            overview+=f'\n- [{topic[1]}](#{key})'
    sections['fishing']['body']=overview

    native_path=root/'tools/encyclopedia_native_shops.json'
    native_data=json.loads(native_path.read_text('utf-8'))
    assert not native_data['errors'], native_data['errors']
    by_scene=defaultdict(list)
    texts={r['Id']:r['Text'] for r in data['dialogue']}
    for row in native_data['items']:by_scene[row.get('scene',row['file'])].append(row)
    names={'Room_Charm_Shop':'萨鲁巴','Room_mapper':'伊塞尔达','Room_shop':'斯莱','Room_Sly_Storeroom':'斯莱地下室','Fungus2_26':'食腿者','Ruins1_05b':'古董商里姆'}
    shop_links=[]
    for scene,rows in sorted(by_scene.items()):
        key='native-shop-'+scene.lower(); rows_out=[]
        for row in rows:
            name=next((texts[sheet+'/'+row['nameConvo']] for sheet in ['UI','Shop','Map','Names'] if sheet+'/'+row['nameConvo'] in texts),row['nameConvo'])
            base=texts.get('Prices/'+row['priceConvo'],'')
            price=int(base)*(1 if row['relic'] else 5) if base.isdigit() else '价格键 '+row['priceConvo']
            cond=[]
            if row['requiredPlayerDataBool'] not in ('','Null'):cond.append('需要 '+row['requiredPlayerDataBool'])
            if row['playerDataBoolName'] not in ('','Null'):cond.append('未购买 '+row['playerDataBoolName'])
            if row['removalPlayerDataBool']:cond.append('不满足移除标记 '+row['removalPlayerDataBool'])
            if row['charmsRequired']:cond.append(f'护符数量至少 {row["charmsRequired"]}')
            if row['stockVariant']=='alternate':cond.append('替代库存：'+row['alternateFlag']+' 或 '+row['alternateFlag2'])
            elif row['alternateFlag']:cond.append('替代标记不满足时的基础库存')
            if row['dungDiscount']:cond.append('装备粪臭防御者纹章 8 折')
            if row['relic']:cond.append('古董回收收入；需持有对应 '+row['relicPDInt']+' 古董')
            rows_out.append((name,price,'；'.join(cond) or '无额外商品标记',row['nameConvo'],row['specialType']))
        add(key,names.get(scene,scene)+' · 原生商店',f'场景 `{scene}`。原生库存按剧情与购买标记控制，不统一绑定能力阶段。原生商品正价格 ×5，古董回收收入保持原价。\n\n'+table(['商品','吉欧价格','出现/购买条件','文本 ID','特殊类型'],rows_out)+'\n\n'+source('tools/encyclopedia_native_shops.json','完整原生字段')+' · '+source('src/TerraKnight/Items/HkShopPrices.cs'))
        shop_links.append(f'- [{names.get(scene,scene)} · {scene}](#{key})')
        report['native_shops']+=len(rows)
    add('native-shops','圣巢本地 NPC 商店与服务',
        '商品页包含基础/替代库存、剧情标记、购买移除条件、护符数量与折扣。原生剧情标记必须和路线条件一起看，不能用能力阶段代替。\n\n'+'\n'.join(shop_links)+
        '\n\n## 服务型交易\n\n钉子匠升级、柯尼法地图、特定长椅与门禁、鹿角站、食腿者修复、迪万护符升级及竞技场门票通过吉欧确认框结算，正数收费同样 ×5；银行存款不加倍。里姆按原价回收古董。图钉/地图的购买标记、钥匙与商店剧情是额外条件。\n\n[剧情与结局路线](#original) · [能力阶段与地区流程](#progress)\n\n'+source('src/TerraKnight/Items/HkShopPrices.cs'))
    service_names={'NAIL_UPGRADE_1':'钉子匠升级一（无苍白矿石）','NAIL_UPGRADE_2':'钉子匠升级二（1 苍白矿石）','NAIL_UPGRADE_3':'钉子匠升级三（2 苍白矿石）','NAIL_UPGRADE_4':'钉子匠升级四（3 苍白矿石）','NAILMASTER_ORO':'奥罗传授冲刺劈砍','UNBREAKABLE_GREED':'迪万：坚固贪婪','UNBREAKABLE_HEART':'迪万：坚固心脏','UNBREAKABLE_STRENGTH':'迪万：坚固力量','COLOSSEUM_BRONZE':'勇士的试炼','COLOSSEUM_SILVER':'征服者的试炼','COLOSSEUM_GOLD':'愚人的试炼'}
    service_rows=[]
    for row in data['dialogue']:
        key=row['Key']
        if row['Sheet']=='Prices' and row['Text'].isdigit() and (key in service_names or key.startswith(('STAGWAY_','TOLL_','MAP_'))):
            service_rows.append((service_names.get(key,key),int(row['Text'])*5,key))
    add('native-services','本地 NPC 服务、地图、车站与门禁价格',
        '下列为通过原生吉欧确认框收费时的当前价格；到达场景、剧情进度、物品与服务是否开放仍由原生脚本控制。各地区的地图/车站需要在当地找到对应 NPC 或开关。\n\n'+table(['服务','当前吉欧','原生价格键'],service_rows)+
        '\n\n迪万还要求吞入对应易碎护符，换成坚固版后不再作为普通重复购买；钉子匠升级按顺序消耗矿石。食腿者的护符及修复价格、购买移除条件与粪臭折扣见其商店。银行存款、古董回收和捐款不能直接按此表的五倍确认框规则泛化。\n\n'+source('src/TerraKnight/Items/HkShopPrices.cs'))
    sections['native-shops']['body']='[全部服务、地图、车站与门禁现价](#native-services)\n\n'+sections['native-shops']['body']
    sections['npcs']['body']='[圣巢本地 NPC 商店与服务](#native-shops) · [全部台词](#dialogue)\n\n'+sections['npcs']['body']
    npc_entries={e['title']:e for e in entries if e['kind']=='NPC'}
    progression=(root/'src/TerraKnight/Items/VanillaBossProgression.cs').read_text('utf-8-sig')
    def boss_link(record):
        eid='enemy-'+record.lower().replace('@','-')
        label=next((e['title'] for e in entries if e['id']==eid),record)
        return f'[{label}](#{eid})'
    equip_gates={}; material_gates={}
    for line in progression.splitlines():
        m=re.search(r'\["([\w@]+)"\]\s*=P\(',line)
        if m:
            head=re.split(r',M\(',line,maxsplit=1)[0]
            for t in re.findall(r'ItemType\.(\w+)',head):
                if t!='None':equip_gates[t]='击败 '+boss_link(m[1])
    allowed=progression[progression.index('internal static bool Allowed(ItemType t)'):]
    for m in re.finditer(r'((?:case ItemType\.\w+:\s*)+)return ([^;]+);',allowed):
        expr=m[2]
        boss=re.search(r'Has\("([^"]+)"\)',expr)
        step=re.search(r'Current>=Step\.(\w+)',expr)
        condition='击败 '+boss_link(boss[1]) if boss else ('世界进度 ≥ '+step[1] if step else expr)
        for t in re.findall(r'ItemType\.(\w+)',m[1]):material_gates[t]=condition
    gate_rows=[]
    step_names={
        'Evil':'邪恶首领进度（击败粪虫防御者或吸虫之母，或达到后续世界 Boss 进度）',
        'Hardmode':'困难模式进度', 'MechOne':'至少一位机械首领进度',
        'MechAll':'全部三位机械首领进度', 'Plantera':'叛徒领主进度',
        'Golem':'神之驯服者进度', 'Cultist':'空洞骑士进度',
    }
    item_entries={e['id']:e for e in entries if e['kind']=='物品'}
    for t in sorted(set(equip_gates)|set(material_gates)):
        condition='；'.join(dict.fromkeys(c for c in [material_gates.get(t),equip_gates.get(t)] if c))
        for step,label in step_names.items():
            condition=condition.replace('世界进度 ≥ '+step,'世界进度达到'+label)
        gate_rows.append((f'[{t}](#item-{t.lower()})',condition))
        entry=item_entries.get('item-'+t.lower())
        if entry:
            entry['body']+='\n\n## 额外获取条件\n\n'+condition+'。能力阶段与此条件都要满足；阶段掉落表按该阶段世界 Boss 已推进后的状态展示。固定 Boss 奖励以对应 Boss 的奖励规则为准。\n\n[完整进度条件](#equipment-gates) · [Boss 推进路线](#boss-progression)'
    add('equipment-gates','全部材料与装备的世界 Boss 进度条件',
        '此门槛同时影响商店、制作与获取，不能仅凭能力阶段跳过。表内直接整理当前 Allowed/AllowedEquipment 配置。未列出的普通装备：阶段 0–2 不额外要求世界进度，阶段 3–4 需要 Hardmode，阶段 5 需要 Plantera；法术奖励武器改由对应法术解锁。普通材料若无表内规则，则仍检查自己的物品阶段。\n\n'
        'Hardmode：进入困难模式 Boss 进度；Plantera：叛徒领主进度；Golem：神之驯服者进度；Cultist：空洞骑士进度；MechOne/MechAll：一个/全部机械 Boss 对应战斗进度。详细顺序见 [Boss 推进路线](#boss-progression)。\n\n'+table(['物品','额外进度条件'],gate_rows)+'\n\n'+source('src/TerraKnight/Items/VanillaBossProgression.cs'))
    for npc in details['npcs']:
        entry=npc_entries[npc['Zh']]
        functional=bool(npc['Stocks']) or npc['Kind']=='Visitor' or npc['Internal'] in ('Guide','Nurse','Angler')
        placement=('功能居民：达到阶段 '+str(npc['Stage'])+' 并满足自然解锁条件后，才允许入住或通过居民面板搬迁。' if functional else '装饰居民/宠物：可从居民面板自由放置；不强制套用功能商人的解锁门槛。')
        if npc['Kind']=='Angler':placement+=' 必须先在蓝湖救醒。'
        if npc['Kind']=='Santa':placement+=' 自然在场另受本机日期 12 月 15 日起的圣诞期间限制。'
        if npc['Kind']=='Visitor':placement+=' 旅商每天以 22.12% 的日概率安排上午到访（上午前 450 秒内），还要求镇上至少两名居民；白天 810 秒后或夜间，玩家不在附近才离开。每次带 4–6 种货物，并优先抽一件当前可供池最高阶段的战斗武器。'
        if npc['Kind']=='Princess':placement+=' 若走自然到访路径，需其他符合条件的常住居民先到齐。'
        entry['body']=placement+'\n\n'+entry['body']
        entry['body']+='\n\n## 当前游戏的完整库存条件\n\n普通商品还受物品阶段与世界 Boss 进度限制。法术奖励武器根据吸收对应法术解锁。以下价格来自当前购买函数，无优惠卡折扣。\n\n'
        rows=[]
        for stock in npc['Stocks']:
            conditions=[]
            if stock['Phases']:conditions.append('月相 '+','.join(map(str,stock['Phases'])))
            if stock['Spell']:conditions.append('已吸收 '+['','复仇之魂','嚎叫幽灵','暗影之魂','深渊尖啸'][stock['Spell']])
            if stock['Type'] in equip_gates:conditions.append(equip_gates[stock['Type']])
            if stock['Type'] in material_gates:conditions.append(material_gates[stock['Type']])
            rows.append((f'[{stock["Type"]}](#item-{stock["Type"].lower()})',max(npc['Stage'],stock['Stage']),'; '.join(conditions) or '不限月相',f'{stock["PriceCopper"]/100:g} 吉欧'))
        if rows:entry['body']+=table(['物品','最低能力阶段','额外条件','当前单价'],rows)
        if npc['Chat']:
            key='town-chat-'+npc['Internal'].lower()
            add(key,npc['Zh']+' · 全部城镇台词',
                '保留原始条件占位符：Day/Night 表示昼夜，此处台词的 Hardmode 条件从能力阶段 4 起满足；人名占位符在游戏中替换为当前居民名字。具体筛选与名字替换见下方实现。\n\n'+
                '\n\n'.join('### '+row['Key']+'\n\n'+html.escape(row['Text'] or '此键未加载') for row in npc['Chat'])+'\n\n'+source('src/TerraKnight/Town/TownNpcsServices.cs'))
            entry['body']+='\n\n[查看此 NPC 全部台词](#'+key+')'
        entry['body']+='\n\n[其余装备与材料的世界 Boss 门槛](#equipment-gates)\n\n'+source('src/TerraKnight/Town/TownNpcs.cs')+' · '+source('src/TerraKnight/Items/ProgressionShops.cs')
    report['town_dialogue']=sum(len(n['Chat']) for n in details['npcs'])
    boss_links=[]
    for record,rows in details['godhome'].items():
        enemy_id='enemy-'+record.lower().replace('@','-')
        enemy=next((e for e in entries if e['id']==enemy_id),None)
        if not enemy:continue
        key='godhome-'+record.lower().replace('@','-')
        add(key,enemy['title']+' · 神居全档位数值',
            f'[怪物详情](#{enemy_id})\n\n覆盖六个能力阶段、五种难度、调谐/飞升/辐辉及一至五门。阶段 5 再区分邪教徒前、击败空洞骑士、四柱进度、月亮领主进度。单位血池由当前函数直接导出；合计预算 = 单位血池 × 血池系数 × 个体平衡系数，具体原生可跳过部位和血条取整见怪物页。防御和伤害列为神居挑战的系数，并非裸接触伤害。\n\n'+
            table(['能力阶段','终局进度','难度','挑战','单位血池','血池系数','个体平衡系数','防御倍率','附加防御','伤害倍率'],[(r['Stage'],r['Progress'],r['Difficulty'],r['Challenge'],r['UnitLife'],r['PoolFactor'],r['LifeTuning'],r['Defense'],r['FlatDefense'],r['Damage']) for r in rows])+
            '\n\n'+source('src/TerraKnight/Items/BossStats.cs')+' · '+source('src/TerraKnight/Items/GodhomeBalance.cs')+' · '+source('src/TerraKnight/Items/BossHealthBudgets.cs'))
        enemy['body']+='\n\n[神居各阶段、进度、难度与挑战完整数值](#'+key+')'
        boss_links.append(f'- [{enemy["title"]}](#{key})')
    add('godhome-values','神居 Boss 全阶段与挑战数值','所有档位按页加载，每页最多 24 行。\n\n'+'\n'.join(boss_links))
    report['godhome_rows']=sum(len(r) for r in details['godhome'].values())
    progression=(root/'src/TerraKnight/Items/VanillaBossProgression.cs').read_text('utf-8-sig')
    progress_rows=[]
    for m in re.finditer(r'\["([\w@]+)"\]\s*=\s*P\((\d+),Step\.(\w+),"([^"]+)","([^"]+)"',progression):
        eid='enemy-'+m[1].lower().replace('@','-')
        label=next((e['title'] for e in entries if e['id']==eid),m[1])
        progress_rows.append((f'[{label}](#{eid})',m[2],m[3],m[4],m[5]))
    add('boss-progression','Boss 推进路线、阶段与装备解锁',
        '能力阶段与世界 Boss 进度同时影响获取。能力可以提前探索取得，强装备和材料仍应沿 Boss 推进制作；此表直接整理当前进度配置，具体掉落概率、配方与材料数量见对应怪物/物品页。\n\n'+
        table(['圣巢 Boss','能力阶段','世界进度','泰拉进度对应','下一步与装备制作'],progress_rows)+
        '\n\n四柱对应奥罗马托、斯莱、席奥和纯粹容器；这些神居战斗的阶段 5 预算随邪教徒、四柱、月亮领主进度改变。\n\n[能力获取与地区路线](#progress) · [全 Boss 神居数值](#godhome-values)\n\n'+source('src/TerraKnight/Items/VanillaBossProgression.cs'))
    sections['progress']['body']='[完整 Boss 推进与装备解锁路线](#boss-progression)\n\n'+sections['progress']['body']
    add('knowledge','新版完整资料导航',
        '\n'.join(f'- [{title}](#{key})' for key,title in [('progress','开局到终局的阶段路线'),('items','全部物品与材料'),('recipes','合成与上位替代材料'),('enemies','五难度敌人与 Boss 数值'),('godhome-values','神居全部进度与挑战数值'),('enemy-ai','全部敌人的原生与新版 AI'),('npcs','泰拉居民、访客与分阶段商店'),('native-shops','圣巢本地商店与服务'),('dialogue','全部新版文字、台词和石碑'),('casino','三台赌博机器与精确概率'),('fishing-rules','钓鱼精确公式和全部规则'),('fishing','分地区分阶段钓鱼与宝匣抽样'),('original','剧情、路线与结局')])+
        '\n\n本地工作区快照；不代表已发布或已安装。精确枚举、条件公式、有限次抽样、原生静态 FSM 与运行时导出在各页分别标明。')
    sections['audit']['body']=(
        '## 本轮 4.4.2 数据来源与覆盖\n\n'
        '物品、配方、掉落、商店和中文文本来自同一冻结源码构建的私人游戏副本；神居各进度/挑战数值及现价由同份生产源码补充导出。快照版本、DLL 和源码清单哈希可查看 '+source('wiki-source.json','本地快照记录')+'。\n\n'+
        source('tools/encyclopedia_knowledge.json','4091 条有效文本、110 套 AI 和赌博机原始数据')+' · '+source('tools/encyclopedia_details.json','完整现价、城镇台词和神居组合数值')+' · '+source('tools/encyclopedia_native_shops.json','原生商店对象与解锁字段')+'\n\n'
        '原生商店按全部 BuildSettings 场景扫描并沿库存引用解码，81 条序列化库存行包含基础/替代库存和模板；不把对象存在本身当成玩家此时能购买的证明。原生敌人攻击研究保留原来的证据状态，FSM 初始值、已启用动作与当前实战值分别说明。新版招式参数来自实际编译后配置，源码挂接点用于说明实现，不声称所有招式都完成了视觉或联机逐招实战验收。\n\n'
        '老虎机与转盘为精确枚举；弹珠机是 6000 个指定发射协议的家庭样本。钓鱼与开箱的样本表不能泛化成任意渔力、天气、幸运和任务状态下的固定概率；精确判定公式另列。完整性、链接和桌面/手机性能由本地浏览器核查。\n\n---\n\n'+sections['audit']['body'])
    return report
