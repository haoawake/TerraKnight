"""Player-facing presentation. Keep raw exports private; publish names and gameplay rules."""
import html
import json
import math
import re
import struct

DIFFICULTY={'Normal':'普通','Hard':'困难','Expert':'专家','Master':'大师','Hollow':'空洞'}
PROGRESS={'PreCultist':'击败空洞骑士前','PostCultist':'已击败空洞骑士','PostPillars':'四柱挑战后','PostMoonLord':'月亮领主进度','Hardmode':'困难模式','Plantera':'已击败叛徒领主','Golem':'已击败神之驯服者','Cultist':'已击败空洞骑士','MechOne':'至少击败一位机械首领对应 Boss','MechAll':'击败全部机械首领对应 Boss','Slime':'史莱姆王进度','Eye':'克苏鲁之眼进度','Evil':'邪恶首领进度','Dungeon':'地牢进度','Pillars':'四柱进度','MoonLord':'月亮领主进度','Any':'不限','All':'全部难度','Above':'玩家在上方','Below':'玩家在下方','Ground':'地面','Air':'空中','True':'是','False':'否','Day':'白天','Night':'夜晚'}
MOVES={'Lunge':'直线冲锋','Chase':'突进追击','Leap':'跳扑','Dive':'俯冲','Stalk':'等待落点并逼近','Blink':'传送','Shot':'发射弹体','Volley':'齐射','Burst':'连续射击','Fan':'扇形散射','Rain':'落弹','Beam':'激光束','Wave':'冲击波','Pulse':'范围攻击','Pillar':'地柱','Pool':'地面残留','Summon':'召唤','Own':'原有招式','Shell':'硬壳防御'}
TECH=re.compile(r'源码|代码(?![12]球)|内部\s*ID|HealthManager|PlayMaker|\bFSM\b|反编译|\bDLL\b|C#|\bProfile\b|\bBrain\b|Boss Kit|挂接|序列化|源文件|原始字段|字段名|函数|快照哈希|source_hash|单元测试|实现证据|资源初值|原生可杀敌')

def table(headers, rows):
    return '\n'.join(['| '+' | '.join(headers)+' |','|'+'|'.join('---' for _ in headers)+'|']+['| '+' | '.join(str(v).replace('|',' / ') for v in row)+' |' for row in rows])

def player_release_notes(text):
    text=re.sub(r'```.*?```','',text,flags=re.S)
    text=re.sub(r'`([^`]+)`',r'\1',text)
    text='\n'.join(line for line in text.splitlines() if not TECH.search(line))
    return re.sub(r'\n{3,}','\n\n',text)

def apply(root,sections,entries):
    names={e['id']:e['title'].split(' · ')[0] for e in entries}
    items={e['id'][5:]:e['title'] for e in entries if e['id'].startswith('item-')}
    runtime=json.loads((root/'tools/encyclopedia_runtime.json').read_text('utf-8'))['data']
    mapping={k:v.get('Name',k) for k,v in runtime['items'].items()}
    for e in entries:
        armor=re.search(r'\*\*盔甲套装\*\*：`([^`]+)`；套装判定 `([^`]+)`',e['body'])
        if armor:
            mapping.setdefault(armor[1],e['title']+'搭配套装')
            mapping.setdefault(armor[2],e['title']+'搭配套装')
            e['body']=re.sub(r'\*\*盔甲套装\*\*：`[^`]+`；套装判定 `[^`]+`。','**完整套装奖励**：',e['body'])
    health=json.loads((root/'tools/encyclopedia_enemy_health.json').read_text('utf-8'))['records']
    for k in health:mapping[k]=names.get('enemy-'+k.lower().replace('@','-'),k)
    mapping.update(DIFFICULTY);mapping.update(PROGRESS)
    words=re.compile(r'(?<![A-Za-z0-9_])('+'|'.join(re.escape(k) for k in sorted(mapping,key=len,reverse=True))+r')(?![A-Za-z0-9_])')
    hidden={k for k in sections if k.startswith('fsm-') or k=='native-scripts'}
    for k in hidden:sections.pop(k)
    def clean(text,translate=True):
        text=text.replace('| 套装判定 ID |','| 套装搭配 |')
        text=re.sub(r'^同族不同头部/胸甲[^\n]+','同一套装的不同头部或胸甲可能提供不同奖励；下表按搭配装备查询。',text,flags=re.M)
        text=re.sub(r'```.*?```','',text,flags=re.S)
        text=re.sub(r'<details>.*?</details>','',text,flags=re.S)
        text=re.sub(r'\[([^\]]+)\]\(([^)]+)\)',lambda m:'' if m[2].lstrip('#') in hidden or re.search(r'(?:^|/)(?:src|tools|docs|artifacts)/|\.(?:cs|json|ps1|py|md)(?:#.*)?$',m[2]) else m[0],text)
        text=re.sub(r'`([^`]+)`',lambda m:mapping.get(m[1],m[1] if re.search(r'[\u4e00-\u9fff]',m[1]) or re.fullmatch(r'[\d\s.%×+*/<>=~–—−:,-]+',m[1]) else ''),text)
        # Remove developer-only columns as a whole, retaining the gameplay cells.
        def player_table(m):
            lines=m[0].strip().splitlines()
            cells=lambda line:[c.strip() for c in line.strip().strip('|').split('|')]
            headers=cells(lines[0])
            keep=[i for i,h in enumerate(headers) if not re.search(r'内部|文本 ID|特殊类型|价格键|源码|原始|字段|判定 ID|标记 ID',h)]
            if not keep:return ''
            return '\n'.join('| '+' | '.join(cells(line)[i] if i<len(cells(line)) else '' for i in keep)+' |' for line in lines)+'\n'
        text=re.sub(r'^\|[^\n]+\n\|[ :|-]+\|\s*\n(?:\|[^\n]*(?:\n|$))*',player_table,text,flags=re.M)
        kept=[]
        for line in text.splitlines():
            if TECH.search(line):continue
            if re.search(r'分类：.*代表场景|\*?\*?证据\*?\*?\s*：|本轮无实战|原生逐动作复核',line):continue
            if re.match(r'^- (记录|常见地点|图鉴分类|定义来源|实际伤害)',line):continue
            if translate:line=words.sub(lambda m:mapping[m[1]],line)
            line=line.replace('战斗阶段','推荐阶段').replace('配置阶段','所需阶段').replace('配置标签阶段','所需阶段').replace('当前工作区','当前版本').replace('运行时导出','游戏数据').replace('快照','资料').replace('精确枚举','精确概率').replace('怪物数值与掉落','怪物资料与掉落').replace('攻击方式与 AI 实现','攻击方式').replace('老版与新版 AI','旧版与新版打法').replace('原生 / 旧版攻击','旧版攻击方式').replace('弹速参数','弹速')
            line=re.sub(r'[^；。]*字段[^；。]*[；。]?','',line)
            line=re.sub(r'\s*·\s*(?:Kind|Tier|Cooldown|Antic|Recover|Sight|Hear|Key|Gap|Speed|Time|Height|Count|Weight)\b','',line)
            kept.append(line)
        return re.sub(r'\n{3,}','\n\n','\n'.join(kept)).strip()
    # Rebuild each monster's main numbers as a compact player table, with no implementation factors.
    for e in entries:
        if e['kind']!='怪物':continue
        record=next((k for k in health if 'enemy-'+k.lower().replace('@','-')==e['id']),None)
        if not record:continue
        h=health[record];old=e['body'];rows=[]
        for d in h['difficulties']:
            stat=next((s for s in d['stage_stats'] if s['stage']==h['stage']),d['stage_stats'][0])
            rows.append((DIFFICULTY[d['difficulty']],f"{stat['life']:,}",stat['damage'],stat['defense']))
        body=f"推荐阶段：{h['stage']}。\n\n"+table(['难度','整场总血量' if e['category']=='首领' else '生命','接触伤害参考','防御'],rows)
        body+='\n\n接触伤害为减伤前的参考值；弹幕和特殊攻击可能不同。神居请查下方的独立数值表。'
        if any(len({(s['life'],s['damage'],s['defense']) for s in d['stage_stats']})>1 for d in h['difficulties']):
            stage_rows=[(s['stage'],DIFFICULTY[d['difficulty']],f"{s['life']:,}",s['damage'],s['defense']) for d in h['difficulties'] for s in d['stage_stats']]
            body+='\n\n## 随成长阶段变化的数值\n\n上表为推荐阶段的参考值；成长阶段改变后，请以下表对应阶段为准。\n\n'+table(['能力阶段','难度','生命','接触伤害参考','防御'],stage_rows)
        phase=re.search(r'(\| 部位 / 阶段[^\n]*\n(?:\|[^\n]*\n)+)',old)
        if phase:body+='\n\n## 各阶段血条\n\n多阶段和多部位会分别显示血条，当前血条不等于整场总血量。\n\n'+phase[1]
        for line in old.splitlines():
            if line.startswith(('- 对应召唤物','- 宝藏袋')):body+='\n\n'+line
        drops=re.search(r'(\| 掉落物[^\n]*\n(?:\|[^\n]*(?:\n|$))+)',old)
        if drops:body+='\n\n## 掉落物\n\n'+drops[1]
        body+='\n\n[攻击方式与应对](#ai-'+e['id'][6:]+')'
        if 'godhome-'+e['id'][6:] in sections:body+=' · [神居血量](#godhome-'+e['id'][6:]+')'
        body+='\n\n## 游戏内图鉴搜索条件\n\n游戏内按已发现的怪物名称、已击败怪物当前已开放的掉落名称搜索，并受所选掉落阶段、地区和世界 Boss 进度影响。Wiki 展示全部资料；阶段掉落表按该阶段 Boss 已推进后的状态导出，不能只凭阶段编号判断当前存档已经解锁。商店、制作、宝匣等来源也不会单独成为怪物搜索结果。'
        e['body']=body
    # The attack pages describe moves and useful timings rather than source fields.
    profiles=json.loads((root/'tools/encyclopedia_knowledge.json').read_text('utf-8'))['profiles']
    for key,s in sections.items():
        if not key.startswith('ai-'):continue
        record=next((k for k in health if k.lower().replace('@','-')==key[3:]),key[3:]);base=record.split('@')[0]
        p=profiles.get(record,profiles.get(base));raw=s['body']
        before=raw.split('## 新版')[0]
        body=clean(before)
        if p:
            body+='\n\n## 新版攻击方式\n\n新版会更积极地接近玩家，也可能应对高处、冲刺和钩爪。以下列出基础动作和时间；战斗时还会受距离与难度影响。\n\n'
            rows=[]
            for move in p['Moves']:
                tier={'All':'全部难度','Expert':'专家及以上','Master':'大师及空洞'}.get(move['Tier'],'全部难度')
                extra=[]
                if move.get('CutHook'):extra.append('会打断钩爪')
                if move.get('Heavy'):extra.append('可击落坐骑')
                if move.get('LandWave'):extra.append('落地后有冲击波')
                rows.append((MOVES.get(move['Kind'],'特殊攻击'),tier,f"{move['Antic']:g} 秒",f"{move['Cooldown']:g} 秒",'、'.join(extra) or '注意动作前摇'))
            body+=table(['动作','开放难度','基础前摇','基础间隔','应对提示'],rows)
        else:
            paragraphs=[]
            raw=re.sub(r'<details>.*?</details>','',raw,flags=re.S)
            raw=raw.split('## 新版',1)[-1]
            for para in raw.split('\n\n'):
                if not para.startswith(('#','|','[')) and re.search(r'[\u4e00-\u9fff]',para) and not TECH.search(para):
                    c=clean(para)
                    if c and c not in paragraphs:paragraphs.append(c)
            body+='\n\n## 新版攻击方式\n\n'+('\n\n'.join(paragraphs) or '保留原有攻击动作；战斗时留意前摇、落点与弹幕空隙。')
        s['body']=body;s['title']=names.get('enemy-'+key[3:],s['title'].split(' · ')[0])+' · 攻击方式与应对'
    details=json.loads((root/'tools/encyclopedia_details.json').read_text('utf-8'))
    f32=lambda x:struct.unpack('f',struct.pack('f',x))[0]
    for record,rows in details['godhome'].items():
        key='godhome-'+record.lower().replace('@','-')
        if key not in sections:continue
        out=[]
        for r in rows:
            factor=r['PoolFactor']
            if record=='FalseKnight':factor=f32(factor*f32(f32(10/13)*1.5+f32(3/13)))
            whole=math.ceil(f32(f32(r['UnitLife']*r['LifeTuning'])*factor))
            out.append((r['Stage'],PROGRESS.get(r['Progress'],r['Progress']),DIFFICULTY[r['Difficulty']],r['Challenge'],f'{whole:,}',f"×{r['Defense']:g}",r['FlatDefense'],f"×{r['Damage']:g}"))
        sections[key]['body']='[返回首领资料](#enemy-'+record.lower().replace('@','-')+')\n\n神居血量随能力阶段、世界进度、难度和挑战等级变化。下表直接列出单人整场总血量，无需自己乘系数。分阶段首领仍会分开显示各阶段血条。最后三列为挑战带来的防御和伤害修正；接触伤害基础参考值见首领资料。\n\n'+table(['能力阶段','世界进度','难度','挑战','整场总血量','挑战防御倍率','挑战额外防御','挑战伤害倍率'],out)
    sections['audit']['body']='本百科面向玩家，列出当前版本的物品、配方、商店、敌人和玩法资料。\n\n## 如何读数值\n\n- 怪物页默认显示世界战，神居另列。\n- 整场总血量适合比较战斗规模；多阶段首领会分开显示血条。\n- 接触伤害是减伤前参考值，弹幕和特殊攻击可能不同。\n- 钓鱼、开箱及弹珠机的抽样频率带有条件，不代表每位玩家都会得到同样结果。\n- 数据已从游戏重新读取，但没有把全部 Boss 的逐场通关当作已完成。'
    sections['reading']['body']='按物品、怪物、NPC 或玩法名称搜索即可。\n\n首领页先看世界战总血量，再看阶段血条与神居表。物品页先看获取方式、合成配方和所需阶段；同类替代材料会列在配方中。概率表请同时阅读适用条件。'
    sections['enemies']['body']='按小怪、精英、首领与成长阶段查找敌人。\n\n血量表分普通、困难、专家、大师和空洞五种难度；世界战与神居分别列出。多阶段首领先看整场总血量，再看各阶段血条。接触伤害为减伤前参考值，特殊攻击可能不同。'
    sections['enemy-ai']['body']='旧版保留原有战斗方式，新版会更积极地预判移动、应对空中玩家，并在较高难度加入对应招式。空洞使用大师的敌人动作和速度，额外难度来自角色限制。\n\n'+ '\n'.join(f'- [{e["title"]}](#ai-{e["id"][6:]})' for e in entries if e['kind']=='怪物')
    sections['boss-updates']={'id':'boss-updates','title':'最新 Boss 血量与挑战规则','body':'## 梦境首领的世界战血量\n\n以下为单人大师难度的整场总血量；失败冠军的盔甲和头部、灵魂暴君的两个阶段分别显示血条。\n\n'+table(['首领','调整前','现在'],[('[失败冠军](#enemy-falseknight-5)','12,000','约 18,000'),('[灵魂暴君](#enemy-magelord-5)','19,000','约 30,000'),('[失落近亲](#enemy-infectedknight-5)','50,000','75,000')])+'\n\n白色防御者约 105,000、灰色王子约 95,000、梦魇之王 120,000。神居对应变体也已提高，请在各首领页查看具体阶段和挑战。\n\n## 空洞与四锁\n\n空洞生命上限为 85%，治疗药水冷却延长 20%，材料与吉欧额外增加 40%。有效环境伤害为当前生命上限的一半，奇数向上取整；免疫和护盾仍有效。\n\n武器锁使最终伤害为 85%；生命锁使生命上限为 70%，与空洞同时开启时仍取 70%；饰品锁关闭饰品属性与技能，保留冲刺、护甲和套装；恢复锁关闭战斗自然恢复和吸血，药水治疗为 70%，休息时解除。全四锁通关材料 ×2.5，空洞全四锁 ×3.5。'}
    sections['knowledge']['body']='按需要选择资料：\n\n- [最新 Boss 血量与挑战规则](#boss-updates)\n\n'+sections['knowledge']['body'].split('\n\n本地工作区')[0]
    knowledge=json.loads((root/'tools/encyclopedia_knowledge.json').read_text('utf-8'))
    texts={r['Id']:r['Text'] for r in knowledge['dialogue']}
    shop_names={'Room_Charm_Shop':'萨鲁巴','Room_mapper':'伊塞尔达','Room_shop':'斯莱','Room_Sly_Storeroom':'斯莱地下室','Fungus2_26':'食腿者','Ruins1_05b':'古董商里姆'}
    flags={'hasDash':'已获得蛾翼披风','hasDreamNail':'已获得梦之钉','hasTramPass':'已获得电车通行证','metStag':'已见过老鹿角虫','salubraNotch4':'已购买前四个护符槽','visitedRestingGrounds':'已到过安息之地', 'brokenCharm_23':'易碎心脏已损坏','brokenCharm_24':'易碎贪婪已损坏','brokenCharm_25':'易碎力量已损坏', 'slyShellFrag1':'已购买第一块面具碎片','slyShellFrag2':'已购买第二块面具碎片','slyShellFrag3':'已购买第三块面具碎片','slyVesselFrag1':'已购买第一块容器碎片'}
    regions={'abyss':'深渊','city':'泪水之城','cliffs':'呼啸悬崖','crossroads':'遗忘十字路','deepnest':'深邃巢穴','fogCanyon':'雾之峡谷','fungalWastes':'真菌荒地','greenpath':'苍绿之径','mines':'水晶山峰','outskirts':'王国边缘','royalGardens':'王后花园','waterways':'皇家水道'}
    for region,name in regions.items():flags['corn_'+region+'Left']='柯尼法已离开'+name
    native=json.loads((root/'tools/encyclopedia_native_shops.json').read_text('utf-8'))
    for scene,name in shop_names.items():
        rows=[]
        for r in native['items']:
            if r.get('scene',r['file'])!=scene:continue
            product=next((texts[s+'/'+r['nameConvo']] for s in ['UI','Shop','Map','Names'] if s+'/'+r['nameConvo'] in texts),'剧情商品')
            base=texts.get('Prices/'+r['priceConvo'],'')
            price=f"{int(base)*(1 if r['relic'] else 5):,}" if base.isdigit() else '对话时显示'
            conditions=[]
            required=r['requiredPlayerDataBool']
            if required not in ('','Null'):
                assert required in flags,required
                conditions.append(flags[required])
            if r['playerDataBoolName'] not in ('','Null') and not r['relic']:conditions.append('尚未购买该商品')
            if r['removalPlayerDataBool']:conditions.append('未把对应易碎护符交给迪万')
            if r['charmsRequired']:conditions.append(f"拥有至少 {r['charmsRequired']} 枚护符")
            if r['stockVariant']=='alternate':conditions.append('已取得或交还店主的钥匙')
            elif r['alternateFlag']:conditions.append('取得店主的钥匙前')
            if r['dungDiscount']:conditions.append('佩戴防御者纹章可享八折')
            if r['relic']:conditions.append('回收收入，需持有对应古董')
            rows.append((product,price,'；'.join(conditions) or '商店开放后可购买'))
        sections['native-shop-'+scene.lower()]['body']='本地商店随探索和剧情开放，请同时查看每件商品的条件。价格单位为吉欧。\n\n'+table(['商品','价格 / 回收收入','购买条件'],rows)
    sections['native-shops']['body']='[服务、地图、车站与门禁价格](#native-services)\n\n圣巢本地商店按剧情和购买进度开放。\n\n'+'\n'.join(f'- [{name}](#native-shop-{scene.lower()})' for scene,name in shop_names.items())
    service_regions={'ABYSS':'远古盆地','CITY':'泪水之城','CLIFFS':'呼啸悬崖','CROSSROADS':'遗忘十字路','DEEPNEST':'深邃巢穴','FOG_CANYON':'雾之峡谷','FUNGAL_WASTES':'真菌荒地','GREENPATH':'苍绿之径','MINES':'水晶山峰','OUTSKIRTS':'王国边缘','RESTING_GROUNDS':'安息之地','ROYAL_GARDENS':'王后花园','ROYALGARDENS':'王后花园','WATERWAYS':'皇家水道','FUNGUS_STATION':'王后驿站','HIDDEN':'隐藏鹿角站','RUINS_1':'城市仓库','RUINS_2':'国王驿站'}
    services=sections['native-services']['body']
    for code,name in sorted(service_regions.items(),key=lambda kv:-len(kv[0])):
        for prefix,label in [('MAP_','地图：'),('STAGWAY_','鹿角站：'),('TOLL_BENCH_','付费长椅：'),('TOLL_GATE_','付费通道：')]:services=services.replace(prefix+code,label+name)
    sections['native-services']['body']=services.replace('原生脚本控制','剧情和探索进度决定').replace('通过原生吉欧确认框收费时的当前价格','当前服务价格')
    sheet_names={'Achievements':'成就','Backer Messages':'旅行者留言','Banker':'银行家','CP2':'剧团与梦境','CP3':'神居与挑战','Charm Slug':'萨鲁巴','Cornifer':'柯尼法','Credits List':'制作名单','Dream Witch':'先知','Dreamers':'守梦人','Elderbug':'德特茅斯长者','Enemy Dreams':'敌人梦语','General':'通用文字','Ghosts':'幽灵','Hornet':'大黄蜂','Hunter':'猎人','Iselda':'伊塞尔达','Jiji':'吉吉','Journal':'猎人日志','Lore Tablets':'石碑与铭文','MainMenu':'主菜单','Map Zones':'地图区域','Minor NPC':'其他居民','Nailmasters':'骨钉大师','Nailsmith':'钉子匠','Prices':'服务价格文字','Prompts':'操作提示','Quirrel':'奎若','Relic Dealer':'古董商里姆','Shaman':'蜗牛萨满','Sly':'斯莱','Stag':'老鹿角虫','StagMenu':'鹿角站菜单','Titles':'标题','UI':'界面','Zote':'左特'}
    for sheet,label in sheet_names.items():
        prefix='text-'+re.sub(r'[^a-z0-9_-]','-',sheet.lower())
        for key,section in sections.items():
            if key==prefix or key.startswith(prefix+'-'):
                section['title']=section['title'].replace(sheet+' 文本与台词',label+' · 台词与文字')
                section['body']=section['body'].replace(sheet+' 文本与台词',label+' · 台词与文字')
        sections['dialogue']['body']=sections['dialogue']['body'].replace('['+sheet+']','['+label+']').replace('| '+sheet+' |','| '+label+' |')
    # Explain probability rolls with examples and named conditions, keeping every catch rule.
    fishing=sections['fishing-rules']['body']
    condition_names={'HardMode':'阶段 3 起','EarlyMode':'阶段 0–2','InLava':'酸水','InHoney':'蜂蜜','Junk':'垃圾判定','CrateRoll':'已抽中宝匣','CanFishInLava':'装备允许酸水钓鱼','Dungeon':'地牢地区','Beach':'海滩地区','Hallow':'神圣地区','Glowshroom':'发光蘑菇地区','TrueDesert':'沙漠地区','TrueSnow':'雪地区域','Height0':'天空层','Height1':'地表层','Height1And2':'地表或地下层','HeightAbove1':'地下及更深处','HeightAboveAnd1':'地表及更深处','HeightUnder2':'天空或地表层','HeightAbove2':'最深层','Height3':'最深层','Corruption':'腐化地区','Crimson':'猩红地区','Jungle':'丛林地区','Snow':'雪地鱼池','Desert':'沙漠鱼池','RolledHallowDesert':'神圣沙漠额外判定通过','Ocean':'海洋大水池','DidNotUseCombatBook':'尚未使用战斗书','Water1000':'水量大于 1000 格'}
    rarity={'0':'不限','1':'普通','2':'不常见','3':'稀有','4':'非常稀有','5':'传奇','6':'不常见，排除非常稀有和传奇','7':'不常见或普通'}
    fish_rows=[]
    for line in fishing.splitlines():
        cells=[c.strip() for c in line.strip('|').split('|')]
        if len(cells)!=6 or not cells[0].isdigit():continue
        conditions='、'.join(condition_names.get(c,c) for c in cells[1].split('、') if c) or '一般鱼池'
        fish_rows.append((conditions,rarity.get(cells[2],'不常见或普通'),cells[3],cells[4],'本次没有鱼获，停止后续尝试' if cells[5]=='true' else '成功选中可用物品后获得鱼获'))
    sections['fishing-rules']['title']='钓鱼概率与捕获条件'
    sections['fishing-rules']['body']='鱼获取决于渔力、地区、液体、成长阶段和任务状态。下表是逐步尝试的捕获条件，单条规则的概率并不等于最终钓到该物品的概率；同一行的候选物品还会再次选择。\n\n## 渔力与稀有度\n\n渔力越高，越容易通过稀有度判定。各级稀有度分别抽取；这里以有效渔力 100 为例：\n\n'+table(['判定','渔力 100 时的概率'],[('普通','50%'),('不常见','33.33%'),('稀有','10%'),('非常稀有','约 4.55%'),('传奇','约 2.22%'),('宝匣','15%；宝匣药水期间 30%'),('继续尝试鱼获','88%'),('没有高强度钓鱼线时断线','1/7')])+'\n\n有效渔力还会受到天气、时段、月相、幸运和水池大小影响。天然池塘不受面积惩罚；自己灌水的池塘至少需要 75 格，75–299 格会按水量比例降低渔力，300 格起没有面积惩罚。阶段 3 起使用困难模式鱼获。酸水需要对应钓具或鱼饵；蜂巢水按蜂蜜处理。\n\n任务鱼优先于普通鱼，需任务有效、地区吻合并通过不常见判定；垃圾、宝匣、蜂蜜不会触发任务鱼，酸水还需对应装备。普通鱼获有 1/25 几率变成鱼箱，使用宝匣药水时为 1/12；任务鱼不会被替换。酸水中原先必中的液体规则改为 1/2。\n\n## 全部捕获规则\n\n按下表顺序尝试；遇到适用的终止条件后，本次鱼获不会继续尝试后续规则。高度分类按所在地区计算。\n\n'+table(['适用条件','需要的稀有度','该规则自身概率','候选鱼获','命中后的处理'],fish_rows)+'\n\n[分地区鱼获与宝匣概率](#fishing) · [任务鱼提示](#quest-dialogue)'
    sections['dialogue']['body']='这里收录游戏中的对白、石碑、任务提示与界面文字。“新版改写”表示采用泰拉人身份的新文字。\n\n'+sections['dialogue']['body'].split('\n\n',1)[1]
    sections['bestiary-progress']={
        'id':'bestiary-progress', 'title':'猎人手记与双重进度（开发版）',
        'body':'本页说明正在开发和验证的新版游戏内图鉴；不表示本地 Wiki 的整套物品数值已更新为开发版，也不表示线上版本已发布。\n\n'
        '## 为什么同一阶段的掉落会变化\n\n探索阶段按已取得的五种不同移动能力计算，范围 0–5；世界首领胜利还会进一步开放材料、装备与制作条件。取得更多能力，不会替代尚未完成的首领战。\n\n'
        +table(['条件','影响'],[
            ('探索阶段 2，并推进水道强敌或后续战况','普通来源的狱石、陨石链开放；固定首领奖励按各自规则处理'),
            ('完成全部三重试炼','叶绿矿与叶绿锭开放'),
            ('击败叛徒领主','灵气、幽灵锭、蘑菇矿锭、自动锤炼机、断裂英雄剑等进度条件满足'),
            ('击败空洞骑士','远古操纵机进度与后续神居奖励路线推进；一次性奖励仍按存档领取记录处理'),
        ])
        +'\n\n## 两种查询模式\n\n- **当前可得**：依据当前存档、已击败的怪物、所选探索阶段与地区，显示实际掉落概率。\n- **全部资料**：检索未发现的怪物和未开放的潜在掉落来源，逐项列出缺少的能力阶段、首领或难度；未开放来源不显示假设概率，也不改变存档的发现记录。\n\n'
        '探索阶段预览只改变查阅的能力阶段，首领战况仍沿用当前存档。切换地区会重新计算该地区来源。物品卡概率表示单次击杀至少获得一次的概率，具体抽选与数量可悬停查看。\n\n'
        '## 战况与提示\n\n“战况一览”逐项显示本存档有效的首领胜利。成长阶段变化、有效首领胜利都会在左下角消息区提示，并使用圣巢称呼；重新读档不重复通知。\n\n'
        '[全部材料与装备进度条件](#equipment-gates) · [Boss 推进路线](#boss-progression)'
    }
    sections['reading']['body']+='\n\n[开发版猎人手记与双重进度说明](#bestiary-progress)'
    # Final sweep applies to every ordinary page, including imported historical notes.
    for e in entries:e['body']=clean(e['body'])
    for key,s in sections.items():
        is_dialogue=key.startswith(('text-','town-chat-','mod-text-'))
        s['body']=clean(s['body'],translate=not is_dialogue)
        s['title']=s['title'].replace('原生 FSM','攻击').replace('AI 实现','攻击方式').replace('原生商店','本地商店')
        s['body']=s['body'].replace('AI 总览','攻击方式总览').replace('全部敌人的原生与新版 AI','全部敌人的旧版与新版攻击方式').replace('钓鱼精确公式和全部规则','钓鱼概率与捕获条件').replace('Mod 全部文字','模组全部文字')
        if is_dialogue:
            s['body']=re.sub(r'^[ \t]{4,}','',s['body'],flags=re.M)
            s['title']=re.sub(r'^[A-Za-z0-9 _-]+ 文本与台词','游戏台词与文字',s['title'])
            n=[0]
            def heading(m):n[0]+=1;return '### 文字 '+str(n[0])+(' · 新版改写' if '新版' in m[0] else '')
            s['body']=re.sub(r'^### [A-Za-z0-9_ /.-]+.*$',heading,s['body'],flags=re.M)
            s['body']=re.sub(r'&lt;(?:br\s*/?|page)&gt;','\n\n',s['body'],flags=re.I)
            s['body']=re.sub(r'^[ \t]{4,}','',s['body'],flags=re.M)
            s['body']=re.sub(r'&lt;/?(?:color|b|i|size)(?:=[^&]*)?&gt;','',s['body'],flags=re.I)
            s['body']=re.sub(r'^[-* ]{3,}$',lambda m:'<p>'+m[0]+'</p>',s['body'],flags=re.M)
        if key.startswith('town-chat-'):
            s['body']='以下为该居民的全部台词。对白会随昼夜、进度和城镇居民变化，人名在游戏中自动替换。\n\n'+'\n\n'.join(s['body'].split('\n\n')[1:])
            for npc in details['npcs']:s['body']=s['body'].replace('{'+npc['Internal']+'}','（'+npc['Zh']+'的名字）')
            s['body']=s['body'].replace('{PlayerName}','（玩家名）')
            s['body']=s['body'].replace('{Armsdealer}','（军火商的名字）')
            def chat_condition(m):
                positive={'Day':'白天','Night':'夜晚','Hardmode':'阶段 4 起'}
                negative={'Day':'夜晚','Night':'白天','Hardmode':'阶段 0–3'}
                return '【'+(negative if m[1] else positive).get(m[2],'当前未启用的保留台词' if not m[1] else '无额外限制')+'】'
            s['body']=re.sub(r'\{\?(!?)(\w+)\}',chat_condition,s['body'])
    return {'presentation':'player','hidden_technical_pages':len(hidden)}
