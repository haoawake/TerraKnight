"""Export the game's TKATLAS without resampling or inventing room geometry."""
from __future__ import annotations
import argparse
import gzip
import hashlib
import io
import json
import re
import struct
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'src/TerraKnight'
OUT = ROOT / 'artifacts/wiki-world-map'


def source(path):
    return (SRC / path).read_text(encoding='utf-8-sig')


def prepare(atlas_path):
    layout_text = source('UI/WorldMapLayout.cs')
    layout = {name: (int(x), int(y)) for name, x, y in re.findall(
        r'\["([^"]+)"\] = new Vector2Int\((-?\d+), (-?\d+)\)', layout_text)}
    # Godhome's rooms, placed by their own generated table (UI/WorldMapGodhome.cs: layout["GG_..."] = ...).
    if (SRC / 'UI/WorldMapGodhome.cs').exists():
        for name, x, y in re.findall(r'layout\["([^"]+)"\] = new Vector2Int\((-?\d+), (-?\d+)\)', source('UI/WorldMapGodhome.cs')):
            layout[name] = (int(x), int(y))
    # Each room's stretch on the map (UI/WorldMapStretch.cs): drawn from its origin that much wider and taller.
    stretch = {name: (float(sx), float(sy)) for name, sx, sy in re.findall(
        r'\["([^"]+)"\] = new Vector2\((-?[\d.]+)f, (-?[\d.]+)f\)', layout_text)}
    deep = set(re.findall(r'"([^"]+)"', source('UI/WorldMapLayers.cs').split('new HashSet<string>')[1]))
    # WorldMap.IsDeep: Godhome is the deep layer's, but for the three parts of Hallownest that carry its prefix.
    deep |= {name for name in layout if name.startswith('GG_') and not name.startswith(('GG_Pipeway', 'GG_Waterways', 'GG_Lurker'))}
    # Parse the ordered AreaName rules and exact AreaColor constants used by the game.
    area_text = source('World/Areas.cs').split('public static string AreaName(string scene)')[1].split('public static string BestiaryBackground')[0]
    rules = [(re.findall(r'"([^"]+)"', prefixes), area) for prefixes, area in re.findall(
        r'if \(Starts\(scene, (.*?)\)\) return "([^"]+)";', area_text)]
    fog_text = source('World/Areas.cs').split('static bool IsFogCanyon')[1].split(';')[0]
    fog = re.findall(r'"([^"]+)"', fog_text)
    def area(name):
        for prefixes, label in rules:
            if name.startswith(tuple(prefixes)):
                return label
        if name.startswith('Fungus3'):
            return '雾之峡谷' if name.startswith(tuple(fog)) else '王后花园'
        return '圣巢'
    hues = {}
    colors_text = source('Player/OpeningPortal.cs').split('internal static Color AreaColor')[1].split('static void SetPalette')[0]
    for labels, rgb in re.findall(r'((?:case "[^"]+":\s*)+)return new Color\(([^)]+)\)', colors_text):
        color = [float(v.strip().rstrip('f')) * 255 for v in rgb.split(',')]
        for label in re.findall(r'case "([^"]+)"', labels):
            hues[label] = color
    tiles = {0: (0, 0, 0, 0), 4: (210, 160, 120, 255), 5: (151, 107, 75, 255),
             6: (128, 128, 128, 255), 7: (191, 142, 111, 255), 8: (186, 168, 84, 255),
             9: (144, 195, 232, 255), 10: (253, 221, 3, 255), 11: (137, 120, 67, 255),
             12: (66, 52, 42, 255), 13: (255, 112, 40, 255)}
    data = io.BytesIO(gzip.decompress(atlas_path.read_bytes()))
    def integer():
        return struct.unpack('<i', data.read(4))[0]
    def string():
        length = shift = 0
        while True:
            c = data.read(1)[0]
            length |= (c & 127) << shift
            shift += 7
            if c < 128:
                return data.read(length).decode('utf-8')
    def rle(expected):
        size = integer()
        if size != expected:
            raise ValueError(f'Unexpected grid length {size}, expected {expected}')
        result = bytearray()
        while len(result) < size:
            run, value = struct.unpack('<HB', data.read(3))
            if not run or len(result) + run > size:
                raise ValueError('Corrupt RLE run')
            result.extend(bytes([value]) * run)
        return bytes(result)
    assert string() == 'TKATLAS'
    assert integer() == 1, 'Unsupported atlas version'
    count = integer()
    OUT.mkdir(parents=True, exist_ok=True)
    rooms = []
    for _ in range(count):
        name, w, h = string(), integer(), integer()
        terrain, wet = rle(w * h), rle(w * h)
        things = []
        for _ in range(integer()):
            kind = data.read(1)[0]
            title = string()
            x, y, tw, th = struct.unpack('<iiii', data.read(16))
            things.append(dict(kind=kind, name=title, x=x, y=h-y-th, w=tw, h=th))
        assert name in layout, f'Missing layout: {name}'
        label = area(name)
        hue = hues.get(label, [.72*255, .38*255, 255])
        palette = dict(tiles)
        for tile, base, mix in [(1, (8, 8, 12), .1), (2, (156, 156, 166), .3), (3, (192, 178, 152), .2)]:
            palette[tile] = tuple(round(a*(1-mix)+b*mix) for a, b in zip(base, hue)) + (255,)
        # One exported pixel per cell, with the same bottom-up to top-down conversion as Unity.
        # Match WorldMap.Build/Draw: every room's solids precede every room's
        # open cells. A later room's rock must never erase an earlier passage.
        solid = {2, 3, 5, 6, 7, 8, 9}
        clear = bytes((0, 0, 0, 0))
        for suffix, is_solid in [('', True), ('-open', False)]:
            pixels = b''.join(bytes(palette[t]) if (t in solid) == is_solid else clear for t in terrain)
            Image.frombytes('RGBA', (w, h), pixels).transpose(Image.Transpose.FLIP_TOP_BOTTOM).save(OUT / f'{name}{suffix}.png', optimize=True)
        wet_palette = [(0, 0, 0, 0), (9, 61, 191, 255), (196, 64, 64, 255), (96, 200, 60, 255), (253, 32, 3, 255)]
        if any(wet):
            # WorldMap.Rebuild gives solid terrain priority over hazards/liquids.
            wet_pixels = b''.join(bytes(wet_palette[v] if t not in solid else wet_palette[0]) for t, v in zip(terrain, wet))
            Image.frombytes('RGBA', (w, h), wet_pixels).transpose(Image.Transpose.FLIP_TOP_BOTTOM).save(OUT / f'{name}-wet.png')
        x, y = layout[name]
        sx, sy = stretch.get(name, (1.0, 1.0))
        dw, dh = round(w * sx, 3), round(h * sy, 3)
        rooms.append(dict(id=name, x=x, y=round(-y-dh, 3), w=w, h=h, dw=dw, dh=dh, area=label,
                          layer='deep' if name in deep else 'world', objects=things, wet=any(wet)))
    assert data.read() == b'', 'Unexpected trailing atlas bytes'
    assert {r['id'] for r in rooms} == set(layout), 'Atlas must cover the entire current map layout'
    by_id = {r['id']: r for r in rooms}
    arena_text = source('Items/BossArena.cs').split('static readonly Dictionary<string, Arena> arenas')[1].split('static readonly Dictionary<string, (string Scene, string Gate)> frontDoors')[0]
    arenas = dict(re.findall(r'\["([^"]+)"\] = (?:Set\()?\w\("([^"]+)"', arena_text))
    seals = dict(re.findall(r'S\(ItemType\.(BossSigil\w+), "([^"]+)"', source('Items/BossSummons.cs')))
    seals = {key: item.lower() for item, key in seals.items()}
    bosses = []
    for line in source('Items/EnemyDropData.cs').splitlines():
        if 'Boss = true' not in line:
            continue
        key = re.search(r'\["([^"]+)"\]', line)[1]
        def field(name):
            return re.search(name + r' = "([^"]+)"', line)[1]
        scene = arenas.get(key, field('Scene')).removesuffix('_boss')
        # The Colosseum encounters have world arenas as well as their Godhome rematches.
        if key in ('Oblobble', 'LobsterLancer'):
            scene = {'Oblobble': 'Room_Colosseum_Silver', 'LobsterLancer': 'Room_Colosseum_Gold'}[key]
        bosses.append(dict(id=key, name=field('Zh'), en=field('En'), scene=scene,
                           stage=int(re.search(r'Stage = (\d+)', line)[1]),
                           url='enemy-' + key.lower().replace('@', '-') + '.html',
                           summon=('item-' + seals[key] + '.html') if key in seals else None))
    # These four variants are explicitly distinguished by the game's relic/treasure bag rules.
    for key, name, scene, parent in [
        ('EnragedGuardian', '暴怒守卫', 'Mines_32', 'MegaBeamMiner'),
        ('AbsoluteRadiance', '无上辐光', 'GG_Radiance', 'FinalBoss'),
        ('SistersOfBattle', '战斗姐妹', 'GG_Mantis_Lords_V', 'MantisLord'),
        ('WingedNosk', '有翼诺斯克', 'GG_Nosk_Hornet', 'MimicSpider')]:
        bosses.append(dict(id=key, name=name, en=key, scene=scene, stage=2 if key == 'EnragedGuardian' else 5,
                           url='item-bag' + key.lower() + '.html', summon=None))
    for boss in bosses:
        scene = boss['scene']
        # Gate survey: Spectate/right1 -> GG_Lurker/left1; Shortcut/left1 -> GG_Waterways/door1.
        entrances = {'Room_Final_Boss_Core': 'Room_temple', 'Dream_Final_Boss': 'Room_temple',
                     'GG_Lurker': 'Room_Colosseum_Spectate',
                     **{s: 'Room_GG_Shortcut' for s in ['GG_Hollow_Knight', 'GG_Nailmasters', 'GG_Sly',
                         'GG_Painter', 'GG_Radiance', 'GG_Mantis_Lords_V', 'GG_Nosk_Hornet']}}
        anchor = scene if scene in by_id else entrances[scene]
        room = by_id[anchor]
        boss.update(anchor=anchor, x=room['x']+room['dw']/2, y=room['y']+room['dh']/2,
                    layer=room['layer'], area=area(scene) if scene != anchor else room['area'],
                    precision='room' if anchor == scene else 'entrance')
        if boss['id'] in ('Hornet', 'Hornet@4'):
            boss['name'] += '（守护者）' if boss['id'] == 'Hornet' else '（哨兵）'
    result = dict(schema=2, render_mode='solid-open', rooms=rooms, bosses=bosses,
                  source=dict(atlas_sha256=hashlib.sha256(atlas_path.read_bytes()).hexdigest(),
                              layout_sha256=hashlib.sha256(layout_text.encode()).hexdigest(),
                              rooms=len(rooms), cells=sum(r['w']*r['h'] for r in rooms)),
                  note='按游戏内世界地图的逐格地形、坐标与配色导出。标记定位到战斗房间；图集未收录的竞技场标记其入口。地形为原始图集，不包含玩家存档中的挖掘、建造和陨石改动。')
    (OUT / 'map.json').write_text(json.dumps(result, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
    print(json.dumps(dict(rooms=len(rooms), bosses=len(bosses), entrance_markers=sum(b['precision']=='entrance' for b in bosses), cells=result['source']['cells']), ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('atlas', type=Path)
    prepare(parser.parse_args().atlas)
