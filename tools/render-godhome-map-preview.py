"""Compose exported, actual map textures at their shipped map positions."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
rooms = {r['Scene']: r for r in json.loads((root / 'artifacts/godhome-map-placement.json').read_text(encoding='utf-8'))}
images = root / 'artifacts/single-copy-godhomeverify-20261018-014330/saves'
selected = {'GG_Atrium': '神居中庭', 'GG_Atrium_Roof': '神居顶层', 'GG_Workshop': '众神堂',
            'GG_Blue_Room': '蓝色房间', 'GG_Land_of_Storms': '风暴之地'}
canvas = Image.new('RGB', (1220, 650), '#0b101a')
draw = ImageDraw.Draw(canvas)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 18)
draw.text((24, 12), '神居地图 · 原生地形图集预览（深层）', font=font, fill='#eed587')
labels = []
for name, label in selected.items():
    room = rooms[name]
    image = Image.open(images / f'{name}-inner.png').convert('RGBA')
    x, y = room['X'] - 3560, 1620 - room['Y'] - room['Height']
    canvas.paste(image, (x, y), image)
    labels.append((x + 6, max(44, y - 25), label))
draw = ImageDraw.Draw(canvas)
for x, y, label in labels:
    draw.text((x, y), label, font=font, fill='#ffe6a5', stroke_width=2, stroke_fill='#0b101a')
draw.text((24, 614), '此图使用游戏导出的实际地图像素；非可见窗口截图。', font=font, fill='#a9b3c4')
canvas.save(root / 'artifacts/godhome-map-preview.png')
