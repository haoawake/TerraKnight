"""Decode FSMs and component identities in shared assets, including spawn prefabs."""
from pathlib import Path
from collections import Counter
import json
import UnityPy
from UnityPy.classes import PPtr
from decode_enemy_fsm import Reader, SCHEMA, decode, DEST
from summarize_enemy_fsm import actions, simplify


def main():
    root=Path(r'F:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data')
    outdir=DEST/'shared'
    outdir.mkdir(exist_ok=True)
    index=[]
    files=sorted(root.glob('*.assets'))
    for no,path in enumerate(files):
        env=UnityPy.load(str(path))
        fsms,components,scripts,errors=[],[],[],[]
        classes={}
        for obj in env.objects:
            if obj.type.name!='MonoBehaviour':continue
            d=obj.read_typetree(check_read=False);p=d['m_Script'];k=(obj.assets_file.name,p['m_FileID'],p['m_PathID'])
            if not d['m_GameObject']['m_PathID']:continue
            if not p['m_PathID']:continue
            if k not in classes:
                classes[k]=PPtr(**p,assetsfile=obj.assets_file).read().m_ClassName
            cls=classes[k]
            go=PPtr(**d['m_GameObject'],assetsfile=obj.assets_file).read()
            identity=dict(owner=go.m_Name,ownerPathId=d['m_GameObject']['m_PathID'],pathId=obj.path_id,asset=obj.assets_file.name)
            scripts.append(dict(identity,type=cls))
            try:
                if cls=='PlayMakerFSM':
                    value=decode(obj.get_raw_data());f=value['fsm']
                    fsms.append(dict(identity,name=f['name'],startState=f['startState'],variables=simplify(f['variables']),globalTransitions=[dict(event=t['fsmEvent']['name'],to=t['toState']) for t in f['globalTransitions']],states=[dict(name=s['name'],transitions=[dict(event=t['fsmEvent']['name'],to=t['toState']) for t in s['transitions']],actions=actions(f,s)) for s in f['states']]))
                elif cls.startswith('EnemyDeathEffects') or cls in ('HealthManager','DamageHero'):
                    raw=obj.get_raw_data();r=Reader(raw);r.pos=28;r.read('System.String');value=r.read(cls);r.align()
                    if r.pos!=len(raw):raise ValueError(f'Consumed {r.pos}, expected {len(raw)}')
                    components.append(dict(identity,type=cls,value=value))
            except Exception as exc:
                errors.append(dict(identity,type=cls,error=str(exc)))
        out=dict(asset=path.name,fsms=fsms,components=components,scripts=scripts,errors=errors)
        filename='shared/'+path.stem+'.json'
        (DEST/filename).write_text(json.dumps(out,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
        index.append(dict(asset=path.name,file=filename,fsms=len(fsms),components=len(components),scripts=len(scripts),errors=errors))
        if no%25==0:print(f'{no+1}/{len(files)} assets; FSMs {sum(s["fsms"] for s in index)}; errors {sum(len(s["errors"]) for s in index)}',flush=True)
    (DEST/'shared-index.json').write_text(json.dumps(dict(assets=index,fsms=sum(s['fsms'] for s in index),errors=sum(len(s['errors']) for s in index)),ensure_ascii=False,indent=2),encoding='utf-8')
    print('Complete',len(files),'assets',sum(s['fsms'] for s in index),'FSMs',flush=True)


if __name__=='__main__':main()
