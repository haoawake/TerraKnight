"""Read-only installed-game evidence inventory. Strings are NOT decoded FSM transitions."""
from pathlib import Path
import argparse
import hashlib
import json
import re
import struct
import time
import UnityPy
from UnityPy.classes import PPtr

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'artifacts/enemy-attack-audit'


def aligned_strings(raw):
    result = []
    for pos in range(28, len(raw) - 4, 4):
        n = struct.unpack_from('<i', raw, pos)[0]
        if 1 <= n <= 200 and pos + 4 + n <= len(raw):
            value = raw[pos + 4:pos + 4 + n]
            if all(32 <= x < 127 for x in value):
                result.append(value.decode('ascii'))
    return list(dict.fromkeys(result))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--data', type=Path, default=Path(r'F:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data'))
    args = parser.parse_args()
    DEST.mkdir(parents=True, exist_ok=True)
    (DEST / 'scenes').mkdir(exist_ok=True)
    settings = UnityPy.load(str(args.data / 'globalgamemanagers'))
    scenes = next(o.read_typetree()['scenes'] for o in settings.objects if o.type.name == 'BuildSettings')
    rows, errors = [], []
    for index, scene_path in enumerate(scenes):
        scene = Path(scene_path).stem
        path = args.data / f'level{index}'
        try:
            env = UnityPy.load(str(path))
            classes, objects, fsms, health, damage, scripts = {}, {}, [], [], [], set()

            def classname(obj, data):
                ref = data['m_Script']
                key = (obj.assets_file.name, ref['m_FileID'], ref['m_PathID'])
                if key not in classes:
                    classes[key] = PPtr(**ref, assetsfile=obj.assets_file).read().m_ClassName
                return classes[key]

            def owner(obj, data):
                ref = data['m_GameObject']
                ptr = PPtr(**ref, assetsfile=obj.assets_file)
                key = (ptr.assetsfile.name, ptr.m_FileID, ptr.m_PathID)
                if key not in objects:
                    objects[key] = ptr.read().m_Name
                return objects[key], ref['m_PathID']

            for obj in env.objects:
                if obj.type.name != 'MonoBehaviour':
                    continue
                data = obj.read_typetree(check_read=False)
                cls = classname(obj, data)
                scripts.add(cls)
                if cls not in ('PlayMakerFSM', 'HealthManager', 'DamageHero'):
                    continue
                go, go_id = owner(obj, data)
                entry = dict(owner=go, ownerPathId=go_id, pathId=obj.path_id, asset=obj.assets_file.name)
                if cls == 'PlayMakerFSM':
                    raw = obj.get_raw_data()
                    entry.update(strings=aligned_strings(raw), sha256=hashlib.sha256(raw).hexdigest())
                    fsms.append(entry)
                elif cls == 'HealthManager':
                    health.append(entry)
                else:
                    damage.append(entry)
            out = dict(scene=scene, level=index, fsms=fsms, healthManagers=health, damageHero=damage, scriptClasses=sorted(scripts))
            filename = f'scenes/{index:03d}-{scene}.json'
            (DEST / filename).write_text(json.dumps(out, ensure_ascii=False, indent=2), encoding='utf-8')
            rows.append(dict(scene=scene, level=index, file=filename, fsms=len(fsms), healthManagers=len(health), damageHero=len(damage), sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        except Exception as exc:
            errors.append(dict(scene=scene, level=index, error=str(exc)))
        if index % 25 == 0:
            print(f'{index + 1}/{len(scenes)} scenes; errors={len(errors)}', flush=True)
    result = dict(scope='All BuildSettings scenes in installed Hollow Knight; serialized ASCII tokens and component inventory only; no action field/transition decoding or runtime verification.', gameData=str(args.data), unityPy=UnityPy.__version__, scenesExpected=len(scenes), scenesRead=len(rows), errors=errors, scenes=rows)
    (DEST / 'native-index.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'scenes'}, ensure_ascii=False), flush=True)
    if errors:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
