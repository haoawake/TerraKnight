"""Draw native arena geometry from the atlas and measured actors/gates for landing review."""
import argparse
import gzip
import io
import json
import struct
from pathlib import Path
from PIL import Image, ImageDraw


def read_atlas(path):
    stream = io.BytesIO(gzip.decompress(path.read_bytes()))
    def integer():
        return struct.unpack('<i', stream.read(4))[0]
    def string():
        count = shift = 0
        while True:
            value = stream.read(1)[0]
            count |= (value & 127) << shift
            if value < 128:
                break
            shift += 7
        return stream.read(count).decode('utf-8')
    def rle():
        size = integer()
        data = bytearray()
        while len(data) < size:
            count, value = struct.unpack('<HB', stream.read(3))
            if count == 0:
                raise ValueError('Zero-length atlas run')
            data.extend(bytes([value]) * count)
        if len(data) != size:
            raise ValueError('Atlas run exceeds declared length')
        return data
    assert string() == 'TKATLAS'
    assert integer() == 1
    result = {}
    for _ in range(integer()):
        name, w, h = string(), integer(), integer()
        tiles, wets = rle(), rle()
        for _ in range(integer()):
            stream.read(1)
            string()
            stream.read(16)
        assert len(tiles) == w * h
        result[name] = (w, h, tiles)
    assert stream.read() == b''
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('survey', type=Path)
    parser.add_argument('--atlas', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    atlas = read_atlas(args.atlas)
    args.out.mkdir(parents=True, exist_ok=True)
    panels = []
    for row in json.loads(args.survey.read_text(encoding='utf-8-sig')):
        if row['Room'] not in atlas:
            continue
        w, h, tiles = atlas[row['Room']]
        scale = min(3, 540 / w, 370 / h)
        panel = Image.new('RGB', (600, 460), '#111722')
        draw = ImageDraw.Draw(panel)
        draw.text((10, 8), row['Key'] + ' / ' + row['Room'], fill='white')
        def point(x, y):
            return (20 + x * 2 * scale, 420 - y * 2 * scale)
        for y in range(h):
            for x in range(w):
                tile = tiles[y*w+x]
                if tile in (2, 3, 5, 6, 7, 8, 9):
                    px, py = 20+x*scale, 420-y*scale
                    draw.rectangle((px, py-scale, px+scale, py), fill='#6a7487')
        for lock in row['Locks']:
            if ' B' not in lock['name'] and 'CamLock B' not in lock['name']:
                continue
            a = point(max(0, lock['cameraXMin']-14.6), lock['cameraYMax']+8.3)
            b = point(lock['cameraXMax']+14.6, max(0, lock['cameraYMin']-8.3))
            if b[0] >= a[0] and b[1] >= a[1]:
                draw.rectangle((*a, *b), outline='#67b3f4')
        for item in row['Landmarks']:
            x, y = point(item['X'], item['Y'])
            draw.rectangle((x-2,y-4,x+2,y+4), fill='#f0bc44')
        for item in row['Bodies']:
            x, y = point(item['X'], item['Y'])
            draw.ellipse((x-4,y-4,x+4,y+4), fill='#ef6b6b')
            draw.text((x+5,y), f"{item['X']:.1f},{item['Y']:.1f}", fill='#ff9999')
        x, y = point(row['HeroX'], row['HeroY'])
        draw.ellipse((x-4,y-4,x+4,y+4), fill='#5de5a0')
        draw.text((10,440), 'red: boss; green: entry; yellow: battle gates; blue: battle camera', fill='white')
        panel.save(args.out / (row['Key'].replace('@','_')+'.png'))
        panels.append(panel)
    for start in range(0,len(panels),6):
        sheet=Image.new('RGB',(1800,920),'#111722')
        for i,panel in enumerate(panels[start:start+6]):
            sheet.paste(panel,((i%3)*600,(i//3)*460))
        sheet.save(args.out / f'review-{start//6+1:02d}.png')
    print(f'Rendered {len(panels)} arenas from source atlas and live survey')


if __name__ == '__main__':
    main()
