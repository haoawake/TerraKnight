"""Decode reflected PlayMaker layouts, requiring exact serialized object consumption."""
from pathlib import Path
import json
import struct
import UnityPy
from UnityPy.classes import PPtr

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'artifacts/enemy-attack-audit'
SCHEMA = json.loads((DEST / 'fsm-schema.json').read_text(encoding='utf-8'))


class Reader:
    def __init__(self, raw):
        self.raw, self.pos = raw, 0

    def align(self):
        self.pos = (self.pos + 3) & ~3

    def number(self, fmt):
        n = struct.unpack_from('<' + fmt, self.raw, self.pos)[0]
        self.pos += struct.calcsize(fmt)
        return n

    def read(self, typ, trace=''):
        if typ == 'UnityEngine.Rect':
            return dict(zip(('x', 'y', 'width', 'height'), [self.number('f') for _ in range(4)]))
        if typ == 'UnityEngine.AnimationCurve':
            n = self.number('i')
            if not 0 <= n <= 10000:
                raise ValueError(f'Curve {trace} size {n}')
            return dict(keys=[dict(time=self.number('f'), value=self.number('f'), inSlope=self.number('f'), outSlope=self.number('f'), weightedMode=self.number('i'), inWeight=self.number('f'), outWeight=self.number('f')) for _ in range(n)], pre=self.number('i'), post=self.number('i'), order=self.number('i'))
        layout = SCHEMA['layouts'][typ]
        kind = layout['kind']
        if kind == 'enum':
            return self.read(layout['element'], trace)
        if kind == 'pointer':
            return dict(fileId=self.number('i'), pathId=self.number('q'))
        if kind == 'primitive':
            if typ == 'System.String':
                n = self.number('i')
                if not 0 <= n <= len(self.raw) - self.pos:
                    raise ValueError(f'String {trace} offset {self.pos-4} size {n}')
                value = self.raw[self.pos:self.pos+n].decode('utf-8')
                self.pos += n
                self.align()
                return value
            fmt = {'System.Boolean': '?', 'System.Byte': 'B', 'System.SByte': 'b', 'System.Int16': 'h', 'System.UInt16': 'H', 'System.Int32': 'i', 'System.UInt32': 'I', 'System.Int64': 'q', 'System.UInt64': 'Q', 'System.Single': 'f', 'System.Double': 'd'}[typ]
            value = self.number(fmt)
            if struct.calcsize(fmt) < 4:
                self.align()
            return value
        if kind == 'array':
            n = self.number('i')
            if not 0 <= n <= 1000000:
                raise ValueError(f'Array {trace} offset {self.pos-4} size {n}')
            if layout['element'] in ('System.Byte', 'System.Boolean'):
                value = [self.number('B') for _ in range(n)]
            else:
                value = [self.read(layout['element'], f'{trace}[{i}]') for i in range(n)]
            self.align()
            return value
        return {f['name']: self.read(f['type'], trace + '.' + f['name']) for f in layout['fields']}


def decode(raw):
    reader = Reader(raw)
    # MonoBehaviour native header: GameObject pointer, enabled+padding, script pointer, name.
    reader.pos = 28
    reader.read('System.String', 'm_Name')
    value = {f['name']: reader.read(f['type'], f['name']) for f in SCHEMA['fields']}
    reader.align()
    if reader.pos != len(raw):
        raise ValueError(f'Consumed {reader.pos}, expected {len(raw)}')
    return value


def main():
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--level', type=int, default=448)
    parser.add_argument('--all', action='store_true')
    args = parser.parse_args()
    data = Path(r'F:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data')
    if args.all:
        index = json.loads((DEST / 'native-index.json').read_text(encoding='utf-8'))
        decoded_dir = DEST / 'decoded'
        decoded_dir.mkdir(exist_ok=True)
        totals = []
        for s in index['scenes']:
            out = decode_scene(data, s['level'])
            path = f"decoded/{s['level']:03d}-{s['scene']}.json"
            (DEST / path).write_text(json.dumps(out, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
            totals.append(dict(scene=s['scene'], level=s['level'], file=path, decoded=len(out['decoded']), errors=out['errors']))
            if s['level'] % 25 == 0:
                print('Decoded scenes', len(totals), 'errors', sum(len(t['errors']) for t in totals), flush=True)
        (DEST / 'decoded-index.json').write_text(json.dumps(dict(scenes=totals, decoded=sum(t['decoded'] for t in totals), errors=sum(len(t['errors']) for t in totals), validation='Reflected serialized field order; exact object byte consumption required. Does not prove reachability/runtime behavior.'), ensure_ascii=False, indent=2), encoding='utf-8')
        return
    out = decode_scene(data, args.level)
    (DEST / f'decoded-{args.level}.json').write_text(json.dumps(out, indent=2, ensure_ascii=False), encoding='utf-8')
    print('Decoded', len(out['decoded']), 'Errors', len(out['errors']))
    print(json.dumps(out['errors'][:3], ensure_ascii=False))


def decode_scene(data, level):
    env = UnityPy.load(str(data / f'level{level}'))
    rows, errors = [], []
    for obj in env.objects:
        if obj.type.name != 'MonoBehaviour':
            continue
        d = obj.read_typetree(check_read=False)
        if PPtr(**d['m_Script'], assetsfile=obj.assets_file).read().m_ClassName != 'PlayMakerFSM':
            continue
        owner = PPtr(**d['m_GameObject'], assetsfile=obj.assets_file).read().m_Name
        try:
            result = decode(obj.get_raw_data())
            rows.append(dict(owner=owner, ownerPathId=d['m_GameObject']['m_PathID'], pathId=obj.path_id, value=result))
        except Exception as exc:
            errors.append(dict(owner=owner, pathId=obj.path_id, error=str(exc)))
    return dict(level=level, decoded=rows, errors=errors)


if __name__ == '__main__':
    main()
