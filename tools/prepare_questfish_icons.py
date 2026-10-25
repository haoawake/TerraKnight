"""Slice the four generated sheets using the task document's magenta-key workflow."""
from pathlib import Path
import json
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'output/questfish-icons'
RES = ROOT / 'src/TerraKnight/Resources'


def main():
    report = []
    preview = Image.new('RGB', (6 * 160, 6 * 180), '#292c35')
    draw = ImageDraw.Draw(preview)
    for batch in range(4):
        first = batch * 9 + 1
        sheet = Image.open(OUT / f'Q{first:02}-Q{first+8:02}.png').convert('RGBA')
        w, h = sheet.size
        for cell in range(9):
            n = first + cell
            x, y = cell % 3, cell // 3
            # The generated sheets have 10-14 px separators. All subjects have clear margins.
            tile = sheet.crop((x*w//3+14, y*h//3+14, (x+1)*w//3-14, (y+1)*h//3-14))
            pixels = []
            for r, g, b, a in tile.getdata():
                key = (min(r,b)-g > 75 and r > 160 and b > 160) or (min(r,b)-g > 55 and r > 1.7*g and b > 1.7*g)
                pixels.append((0, 0, 0, 0) if key else (r, g, b, 255))
            tile.putdata(pixels)
            tile = tile.resize((32, 32), Image.Resampling.NEAREST)
            mask = tile.getchannel('A')
            tile = tile.convert('RGB').quantize(colors=24, dither=Image.Dither.NONE).convert('RGBA')
            tile.putalpha(mask)
            icon = Image.new('RGBA', (34, 34))
            icon.paste(tile, (1, 1))
            target = RES / f'hk_QuestFish_Q{n:02}.png'
            icon.save(target)
            icon.save(OUT / f'Q{n:02}.png')
            colors = {p[:3] for p in icon.getdata() if p[3]}
            assert 1 < len(colors) <= 24
            assert not any(min(r,b)-g > 75 and r > 160 and b > 160 for r,g,b in colors)
            report.append({'id': f'Q{n:02}', 'size': list(icon.size), 'colors': len(colors), 'opaque_pixels': sum(p[3]>0 for p in icon.getdata())})
            px, py = ((n-1)%6)*160+12, ((n-1)//6)*180+8
            preview.paste(icon.resize((136,136), Image.Resampling.NEAREST), (px,py), icon.resize((136,136), Image.Resampling.NEAREST))
            draw.text((px,py+141), f'Q{n:02}', fill='white')
    preview.save(OUT / 'preview.png')
    (OUT / 'icon-validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(f'Prepared and verified {len(report)} transparent 34x34 icons')


if __name__ == '__main__':
    main()
