"""Link exact scene GameObject IDs to death records/overrides, not coincidental FSM strings."""
from collections import defaultdict
from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT/'artifacts/enemy-attack-audit'


def label(value):
    if isinstance(value,dict):
        if value.get('unresolved'):
            return '未展开'
        if 'variable' in value:
            return f"${value['variable']}（初值 {value.get('fsmInitialValue')}）"
        if 'name' in value and 'value' in value:
            return f"${value['name']}" if value['name'] else label(value['value'])
        if 'serializedValue' in value:
            return str(value['serializedValue'])
        if 'ownerOption' in value:
            return 'Self' if value['ownerOption']==0 else label(value['gameObject'])
        if 'pathId' in value:
            return f"PPtr({value.get('fileId',0)},{value['pathId']})"
        return json.dumps(value,ensure_ascii=False,separators=(',',':'))
    return str(value)


def main():
    coverage=json.loads((DEST/'coverage.json').read_text(encoding='utf-8'))
    components=json.loads((DEST/'enemy-owners.json').read_text(encoding='utf-8'))
    hover=(ROOT/'src/TerraKnight/UI/EnemyHover.cs').read_text(encoding='utf-8-sig')
    overrides=re.findall(r'\("([^"]+)", (null|"[^"]+")\)', re.search(r'nameToRecord =\s*\{(.*?)\n        \};',hover,re.S)[1])
    aliases=dict(re.findall(r'\{ "([^"]+)", "([^"]+)" \}',re.search(r'aliases = new Dictionary<string, string>\s*\{(.*?)\n        \};',hover,re.S)[1]))
    keys={e['key'].split('@')[0] for e in coverage['entries']}
    by_go=defaultdict(list)
    for c in components['components']:
        by_go[(c['scene'],c['ownerPathId'])].append(c)

    def record(owner,cs):
        for prefix,key in [('Flamebearer Small','FlameBearerSmall'),('Flamebearer Large','FlameBearerLarge')]:
            if owner==prefix or owner.startswith(prefix+' ('):
                return key,'supplemental exact prefab name; native death journal field is empty'
        for prefix,key in overrides:
            if owner.startswith(prefix):
                return (None if key=='null' else key.strip('"')), 'current EnemyHover name override'
        for c in cs:
            if c['type'].startswith('EnemyDeathEffects') and c['value'].get('playerDataName') in keys:
                return c['value']['playerDataName'], 'native EnemyDeathEffects.playerDataName'
        words=re.split('[ _]+',owner.split('(')[0].strip())
        for end in range(len(words),0,-1):
            prefix=' '.join(words[:end])
            if prefix in aliases:
                return aliases[prefix], 'current EnemyHover alias'
            candidate=''.join(words[:end])
            if candidate in keys:
                return candidate,'EnemyHover-style name prefix (roster membership, not live journal)'
        if len(words)>2 and words[0]+words[-1] in keys:
            return words[0]+words[-1], 'EnemyHover-style first-last name'
        return None,None

    idx=json.loads((DEST/'action-index.json').read_text(encoding='utf-8'))
    scenes={s['scene']:s for s in idx['scenes']}
    bindings, candidates, drivers = [],defaultdict(list),defaultdict(list)
    shared=[]
    if (DEST/'shared-index.json').exists():
        for s in json.loads((DEST/'shared-index.json').read_text(encoding='utf-8'))['assets']:
            data=json.loads((DEST/s['file']).read_text(encoding='utf-8'))
            surface='asset:'+s['asset']
            for c in data['components']:
                c['scene']=surface
                by_go[(surface,c['ownerPathId'])].append(c)
            shared.append((dict(scene=surface,file=s['file']),data))
    # Full bindings across all scenes; per-entry representative selection below.
    surfaces=[(s,None) for s in idx['scenes']]+shared
    for scene,data in surfaces:
        if data is None:data=json.loads((DEST/scene['file']).read_text(encoding='utf-8'))
        for script in data.get('scripts',[]):
            rec,basis=record(script['owner'],by_go.get((scene['scene'],script['ownerPathId']),[]))
            if rec:
                drivers[rec].append(dict(scene=scene['scene'],file=scene['file'],**script))
        for f in data['fsms']:
            cs=by_go.get((scene['scene'],f['ownerPathId']),[])
            rec,basis=record(f['owner'],cs)
            if not rec:
                continue
            binding=dict(scene=scene['scene'],owner=f['owner'],ownerPathId=f['ownerPathId'],fsm=f['name'],pathId=f['pathId'],record=rec,basis=basis,file=scene['file'],states=len(f['states']))
            bindings.append(binding)
            candidates[rec].append((binding,f))
    # Explicit object-name identities for supplemental variants; not a new drop mapping.
    extra_names=dict(EnragedGuardian=('Zombie Beam Miner Rematch',),SistersOfBattle=('Mantis Lord',),WingedNosk=('Hornet Nosk',),AbsoluteRadiance=('Absolute Radiance',),Shade=('Hollow Shade',),FlukeLarva=('fluke_baby',),Revek=('Revek',),ZotelingMighty=('Ordeal Zoteling',),HeavyZoteling=('Zote Crew Fat',),TurretZoteling=('Zote Turret',),LankyZoteling=('Zote Crew Tall',),HeadOfZote=('Zote Thwomp',),FlukeZoteling=('Zote Fluke',),ZotesCurse=('Zote Salubra',))
    evidence,missing=[],[]
    docs=['# 逐怪物原生 FSM 证据\n\n来自本机资源解码，GameObject pathId 将 FSM 与同对象死亡记录/当前名称映射连接。这里只连接同对象的 FSM，子对象、共享 prefab、外部代码和跨 FSM 事件仍需继续追踪。字段为原生单位；变量值只显示资源初值，禁止当作战斗中恒定值。禁用动作不列作已启用攻击。\n\n']
    cache={}
    direct=json.loads((DEST/'direct-script-owners.json').read_text(encoding='utf-8')) if (DEST/'direct-script-owners.json').exists() else []
    direct_notes={
        'Climber':'Cliffs_01 的同对象挂 Climber：本机反编译确认四向贴面/转角移动，身体 DamageHero 负责接触伤害；不需要一个同名攻击 FSM。',
        'FungCrawler':'Fungus2_04 同对象挂 Walker、FSMActivator，未挂 PlayMakerFSM。已核对 Walker 移动与 FSMActivator 启停组件代码；不能因 FSMActivator 名字含 FSM 就虚构一套喷弹状态机。',
        'AbyssCrawler':'Abyss_04 同对象挂 Crawler：按物体旋转选择地/墙/顶运动，射线查地形并改向，取消击退时恢复运动；不是 Climber 的绕角脚本。',
        'HealthScuttler':'sharedassets6.assets / Health Scuttler 的主控是 ScuttlerControl，而非 HealthManager+PlayMaker：代码处理逃跑、被击响应、生命血奖励和护符27的追随反向行为。',
        'EggSac':'Crossroads_50 同对象包含 HealthManager、EnemyDeathEffects、PersistentBoolItem 与表现组件，未挂 DamageHero 或攻击 FSM；固定资源单位。仅此组件集合结论，不冒充全局所有子节点绝无伤害。',
        'FlukeLarva':'Waterways_08 的 fluke_baby_*：HealthManager、EnemyDeathEffectsNoEffect、InfectedEnemyEffects、PlayFromRandomFrameMecanim，未挂 DamageHero/移动攻击 FSM；原先补充场景 Waterways_15 已更正。',
    }
    for i,e in enumerate(coverage['entries'],1):
        key=e['key'];base=key.split('@')[0]
        selected=[(b,f) for b,f in candidates.get(base,[]) if b['scene']==e['scene']]
        if not selected:
            selected=[(b,f) for b,f in candidates.get(base,[]) if b['scene'].startswith('asset:')]
        if not selected and candidates.get(base):
            first_scene=candidates[base][0][0]['scene']
            selected=[(b,f) for b,f in candidates[base] if b['scene']==first_scene]
        if e['extra']:
            s=scenes.get(e['scene'])
            selected=[]
            if s:
                if e['scene'] not in cache:
                    cache[e['scene']]=json.loads((DEST/s['file']).read_text(encoding='utf-8'))
                for f in cache[e['scene']]['fsms']:
                    if any(f['owner'].startswith(n) for n in extra_names.get(key,())):
                        b=dict(scene=e['scene'],owner=f['owner'],ownerPathId=f['ownerPathId'],fsm=f['name'],pathId=f['pathId'],record=key,basis='supplemental exact-name prefix candidate; not a drop record',file=s['file'],states=len(f['states']))
                        selected.append((b,f))
            if not selected:
                for surf,data in shared:
                    for f in data['fsms']:
                        if any(f['owner'].startswith(n) for n in extra_names.get(key,())):
                            b=dict(scene=surf['scene'],owner=f['owner'],ownerPathId=f['ownerPathId'],fsm=f['name'],pathId=f['pathId'],record=key,basis='supplemental shared prefab name candidate; not a drop record',file=surf['file'],states=len(f['states']))
                            selected.append((b,f))
        refs=[b for b,f in selected]
        own_components=[c for (scene,go),cs in by_go.items() if scene==e['scene'] and record(cs[0]['owner'],cs)[0]==base for c in cs]
        driver_refs=drivers.get(base,[])
        evidence.append(dict(key=key,representativeScene=e['scene'],bindings=refs,ownedComponents=own_components,sharedComponentScripts=driver_refs,directScriptEvidence=[r for r in direct if r['key']==key],nonFsmExplanation=direct_notes.get(key),otherSceneBindings=[b for b,f in candidates.get(base,[]) if b['scene']!=e['scene']]))
        docs.append(f"## {i:03d} {e['zh']} · {key}\n\n")
        if not selected:
            missing.append(key)
            docs.append((direct_notes[key] if key in direct_notes else '代表场景尚未连接到同对象 FSM；不代表没有 AI。需检查动态生成、子对象、prefab、组件脚本或补充对象名。')+'\n\n')
            if key in direct_notes:
                docs.append('[原生组件归属](../artifacts/enemy-attack-audit/direct-script-owners.json)；[本机反编译组件目录](../artifacts/enemy-attack-audit/decompiled)。\n\n')
            if own_components:
                docs.append('已连接的原生组件：'+'；'.join(f"{c['owner']} / {c['type']} / GameObject {c['ownerPathId']}" for c in own_components)+'。它们可能由 C# 脚本驱动，不能强行套 PlayMaker。\n\n')
            if driver_refs:
                docs.append('共享资源同种对象的组件类型：'+', '.join(sorted({r['type'] for r in driver_refs}))+'。组件存在不等于其全部方法已人工核查。\n\n')
            continue
        for b,f in selected:
            docs.append(f"### {b['owner']} / {f['name']}\n\n`{b['scene']}` · GameObject `{b['ownerPathId']}` · FSM `{b['pathId']}` · {b['basis']} · [全部动作字段](../artifacts/enemy-attack-audit/{b['file']})\n\n起点：`{f['startState']}`。状态数：{len(f['states'])}。\n\n")
            docs.append('状态目录：'+ ' → '.join('`'+s['name']+'`' for s in f['states'])+'。目录顺序是序列化顺序，不表示运行顺序。\n\n')
            for st in f['states']:
                selected_actions=[a for a in st['actions'] if a['enabled'] and re.search(r'(Spawn|Create|Fling|Fire|Shoot|SetVelocity|Set.*HP|Get.*HP|IntCompare|FloatCompare|HPCompare|Health|SetDamage|SendEventByName|SendEvent$)',a['type'])]
                if not selected_actions:
                    continue
                docs.append(f"- 状态 `{st['name']}`；转移："+ '；'.join(f"{t['event']} → {t['to']}" for t in st['transitions'])+'。\n')
                for a in selected_actions:
                    ps='; '.join(p['name']+'='+label(p['value']) for p in a['parameters'])
                    docs.append(f"  - `{a['type'].split('.')[-1]}`：{ps}\n")
            docs.append('\n')
    unresolved=[k for k in missing if k not in direct_notes]
    (DEST/'owner-links.json').write_text(json.dumps(dict(bindings=bindings,entries=evidence,unmatchedRepresentativeEntries=missing,unresolvedEntryIdentities=unresolved,nonFsmEntries=[k for k in missing if k in direct_notes],componentDecodeErrors=components['errors']),ensure_ascii=False,indent=2),encoding='utf-8')
    (ROOT/'docs/全怪物原生FSM逐项证据-2026-10-21.md').write_text(''.join(docs),encoding='utf-8')
    print(json.dumps(dict(bindings=len(bindings),matchedFsmEntries=len(evidence)-len(missing),nonFsmEntries=missing,unresolvedIdentities=unresolved),ensure_ascii=False,indent=2))


if __name__=='__main__':
    main()
