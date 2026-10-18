"""Pack in-game exports into two small, indexed wiki sprite sheets."""
import argparse
import collections
import hashlib
import json
import math
import sys
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
from generate_encyclopedia import build as read_catalog


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path, help='wiki-assets directory from the isolated wikiassets suite')
    parser.add_argument('--hollow-knight-data', type=Path, required=True, help='Local hollow_knight_Data directory; journal sprites use their exact packed mesh')
    args = parser.parse_args()
    source = args.source
    raw = json.loads((source / 'manifest.json').read_text(encoding='utf-8'))
    items, enemies = read_catalog(assets_only=True)
    # A place left in the item enum with nothing made of it keeps its enum name and gets no page (nor picture).
    items = {t: d for t, d in items.items() if d['name'] != t}
    raw = {key: data for key, data in raw.items() if data['kind'] != 'item' or data['item'] in items}
    expected = {'item-' + t.lower() for t in items}
    expected.update('enemy-' + e['key'].lower().replace('@', '-') for e in enemies)
    assert set(raw) == expected, {'missing': sorted(expected - set(raw)), 'extra': sorted(set(raw) - expected)}
    for key, item in items.items():
        assert raw['item-' + key.lower()]['texture'] == item['texture'], (key, item['texture'])
    out = ROOT / 'artifacts/wiki-illustrations'
    out.mkdir(parents=True, exist_ok=True)
    packed = {}
    counts = collections.Counter()
    import UnityPy
    environment = UnityPy.load(str(args.hollow_knight_data / 'resources.assets'))
    sprites = collections.defaultdict(list)
    required = {data['sprite'] for data in raw.values() if data['kind'] == 'enemy'}
    for obj in environment.objects:
        if obj.type.name == 'Sprite':
            sprite = obj.read()
            if sprite.m_Name in required:
                sprites[sprite.m_Name].append(sprite)
    for kind, cell, columns in [('item', 64, 32), ('enemy', 128, 16)]:
        filename = 'items.webp' if kind == 'item' else 'enemies.webp'
        keys = sorted(key for key, data in raw.items() if data['kind'] == kind)
        width, height = columns * cell, math.ceil(len(keys) / columns) * cell
        sheet = Image.new('RGBA', (width, height))
        for number, key in enumerate(keys):
            data = raw[key]
            path = source / (key + '.png')
            if kind == 'enemy':
                candidates = sprites[data['sprite']]
                assert len(candidates) == 1, (key, data['sprite'], len(candidates))
                # Unity uses tightly packed polygon sprites. A rectangular atlas crop
                # includes neighbours; Sprite.image reconstructs only the real mesh.
                picture = candidates[0].image.convert('RGBA')
            else:
                picture = Image.open(path).convert('RGBA')
            bbox = picture.getbbox()
            assert bbox, 'Blank image: ' + key
            picture = picture.crop(bbox)
            scale = (cell - 12) / max(picture.size)
            size = tuple(max(1, round(side * scale)) for side in picture.size)
            picture = picture.resize(size, Image.Resampling.NEAREST if kind == 'item' else Image.Resampling.LANCZOS)
            x, y = number % columns * cell, number // columns * cell
            sheet.alpha_composite(picture, (x + (cell - size[0]) // 2, y + (cell - size[1]) // 2))
            packed[key] = dict(data, sheet=filename, x=x, y=y, cell=cell,
                               sheet_width=width, sheet_height=height,
                               crop='sprite-mesh' if kind == 'enemy' else 'item-animation-first-frame',
                               source_sha256=hashlib.sha256(path.read_bytes()).hexdigest())
            counts[kind] += 1
        sheet.save(out / filename, lossless=kind == 'item', quality=72, alpha_quality=80, method=6)
    (out / 'manifest.json').write_text(json.dumps(packed, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
    report = {'counts': dict(counts), 'missing': [], 'files': {p.name: p.stat().st_size for p in out.iterdir() if p.is_file()}}
    (ROOT / 'artifacts/wiki-illustrations-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
