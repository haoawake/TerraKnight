"""Read native enemy record ownership and component values with exact-length checks."""
from pathlib import Path
import json
import UnityPy
from UnityPy.classes import PPtr
from decode_enemy_fsm import Reader, SCHEMA, DEST


def main():
    root = Path(r'F:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data')
    index = json.loads((DEST/'native-index.json').read_text(encoding='utf-8'))
    rows, errors = [], []
    for s in index['scenes']:
        if not s['healthManagers']:
            continue
        env = UnityPy.load(str(root/f"level{s['level']}"))
        classes = {}
        for obj in env.objects:
            if obj.type.name != 'MonoBehaviour':
                continue
            d = obj.read_typetree(check_read=False)
            p = d['m_Script']
            key = (obj.assets_file.name,p['m_FileID'],p['m_PathID'])
            if key not in classes:
                classes[key] = PPtr(**p, assetsfile=obj.assets_file).read().m_ClassName
            cls = classes[key]
            if not (cls.startswith('EnemyDeathEffects') or cls in ('HealthManager','DamageHero')):
                continue
            go = PPtr(**d['m_GameObject'],assetsfile=obj.assets_file).read()
            try:
                raw = obj.get_raw_data()
                reader = Reader(raw)
                reader.pos = 28
                reader.read('System.String','m_Name')
                value = reader.read(cls,cls)
                reader.align()
                if reader.pos != len(raw):
                    raise ValueError(f'Consumed {reader.pos}, expected {len(raw)}')
                rows.append(dict(scene=s['scene'],level=s['level'],owner=go.m_Name,ownerPathId=d['m_GameObject']['m_PathID'],pathId=obj.path_id,type=cls,value=value))
            except Exception as exc:
                errors.append(dict(scene=s['scene'],owner=go.m_Name,type=cls,error=str(exc)))
        if s['level'] % 25 == 0:
            print(s['level'], 'components',len(rows),'errors',len(errors),flush=True)
    (DEST/'enemy-owners.json').write_text(json.dumps(dict(components=rows,errors=errors),ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    print('Components',len(rows),'errors',len(errors),flush=True)


if __name__=='__main__':
    main()
