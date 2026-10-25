"""Decode serialized native shop inventories; require exact byte consumption."""
import argparse
import hashlib
import json
from pathlib import Path
import UnityPy
from UnityPy.classes import PPtr
from decode_enemy_fsm import Reader


def decode_item(obj):
    r = Reader(obj.get_raw_data()); r.pos = 28
    r.read('System.String')
    names = 'playerDataBoolName nameConvo descConvo priceConvo requiredPlayerDataBool removalPlayerDataBool'.split()
    d = {n: r.read('System.String') for n in names}
    for n in ['specialType', 'relicNumber', 'charmsRequired']:
        d[n] = r.read('System.Int32')
    for n in ['activeColour', 'inactiveColour']:
        d[n] = [r.number('f') for _ in range(4)]
    for n in ['dungDiscount', 'relic']:
        d[n] = r.read('System.Boolean')
    for n in ['relicPDInt', 'notchCostBool']:
        d[n] = r.read('System.String')
    for n in ['cost', 'itemNumber']:
        d[n] = r.read('System.Int32')
    d['canBuy'] = r.read('System.Boolean')
    assert r.pos == len(r.raw), (r.pos, len(r.raw))
    return d


def main():
    ap = argparse.ArgumentParser(); ap.add_argument('data', type=Path); ap.add_argument('output', type=Path)
    a = ap.parse_args(); rows = []; errors = []; menus = []; hashes = {}
    settings = UnityPy.load(str(a.data/'globalgamemanagers'))
    scenes = next(o.read_typetree()['scenes'] for o in settings.objects if o.type.name == 'BuildSettings')
    scene_names = {f'level{i}': Path(s).stem for i, s in enumerate(scenes)}
    for name in ['resources.assets', 'sharedassets0.assets', *scene_names]:
        path = a.data/name
        hashes[name] = hashlib.sha256(path.read_bytes()).hexdigest()
        env = UnityPy.load(str(path)); cache = {}
        for obj in env.objects:
            if obj.type.name != 'MonoBehaviour': continue
            d = obj.read_typetree(check_read=False); p = d['m_Script']
            key = (obj.assets_file.name, p['m_FileID'], p['m_PathID'])
            if key not in cache: cache[key] = PPtr(**p, assetsfile=obj.assets_file).read().m_ClassName
            cls = cache[key]
            if cls not in ('ShopItemStats', 'ShopMenuStock'): continue
            owner = PPtr(**d['m_GameObject'], assetsfile=obj.assets_file).read()
            try:
                if cls == 'ShopItemStats':
                    rows.append(dict(file=name, object=obj.path_id, owner=owner.m_Name, **decode_item(obj)))
                else:
                    # Public serialized fields in ShopMenuStock, verified from Assembly-CSharp.
                    r = Reader(obj.get_raw_data()); r.pos = 28; r.read('System.String')
                    def pointers():
                        n = r.number('i'); assert 0 <= n < 1000
                        return [dict(fileId=r.number('i'), pathId=r.number('q')) for _ in range(n)]
                    master = dict(fileId=r.number('i'), pathId=r.number('q'))
                    stock = pointers(); inventory = pointers(); alt = pointers(); flag = r.read('System.String')
                    flag_alt = r.read('System.String'); spacing = r.number('f'); item_count = r.number('i')
                    assert r.pos == len(r.raw), (r.pos, len(r.raw))
                    for variant, refs in [('normal', stock), ('alternate', alt)]:
                        for ref in refs:
                            go_obj = PPtr(m_FileID=ref['fileId'], m_PathID=ref['pathId'], assetsfile=obj.assets_file).deref()
                            go = go_obj.read()
                            for component in go.m_Component:
                                child = component.component.deref()
                                if child.type.name != 'MonoBehaviour': continue
                                cd = child.read_typetree(check_read=False)
                                if PPtr(**cd['m_Script'], assetsfile=child.assets_file).read().m_ClassName == 'ShopItemStats':
                                    rows.append(dict(file=name, scene=scene_names.get(name, name), object=child.path_id, owner=go.m_Name, menu=owner.m_Name,
                                        stockVariant=variant, alternateFlag=flag, alternateFlag2=flag_alt, **decode_item(child)))
                    menus.append(dict(file=name, object=obj.path_id, owner=owner.m_Name,
                                      stock=stock, stockAlt=alt, altPlayerDataBool=flag,
                                      remainderBytes=len(r.raw)-r.pos))
            except Exception as e:
                errors.append(dict(file=name, object=obj.path_id, owner=owner.m_Name, type=cls, error=str(e)))
        if menus and menus[-1]['file'] == name or name.startswith('level') and int(name[5:]) % 50 == 0:
            print(name, 'items', len(rows), 'menus', len(menus), 'errors', len(errors), flush=True)
    a.output.parent.mkdir(parents=True, exist_ok=True)
    a.output.write_text(json.dumps(dict(items=rows, menus=menus, errors=errors, source_hashes=hashes,
        scope='Serialized shop item fields with exact byte consumption; stock and alternate-stock references. Conditions are PlayerData flags, not ability stages.'),ensure_ascii=False,indent=2),'utf-8')
    if errors: raise SystemExit('Native shop decoding errors')


if __name__ == '__main__': main()
