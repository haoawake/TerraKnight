"""Assemble three revised sculptures with the unchanged Terraria pedestal."""
from pathlib import Path
import collections, hashlib, html, json, shutil, zipfile
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from package_relic_native import native_statue

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / 'artifacts/relic-revision-20261019'
OLD = ROOT / '空洞骑士大师圣物素材包-原版规格重制候选-20261019'
OUT = ROOT / '空洞骑士大师圣物素材包-三件构图修订版-20261019'
SLUGS = ['mantislord', 'sistersofbattle', 'ghostgalien']

def main():
    rows = json.loads((OLD / 'manifest.json').read_text(encoding='utf-8'))
    base = Image.open(OLD / 'native-reference/native-pedestal-48x64.png').convert('RGBA')
    counts = collections.Counter(Image.open(OLD / 'native-reference/Extra_198.png').convert('RGBA').getdata())
    palette = sorted([c[:3] for c, n in counts.items() if c[3] == 255 and n >= 100], key=sum)
    for folder in ['statues', 'complete', 'classic-2px', 'previous-candidate', 'source-reference', 'generation-metadata', 'comparisons']:
        (OUT / folder).mkdir(parents=True, exist_ok=True)
    base.save(OUT / '原版底座.png')
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 20)
    small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 16)
    sheet = Image.new('RGB', (1200, 450), '#25303b')
    sd = ImageDraw.Draw(sheet)
    records, cards = [], []
    for i, slug in enumerate(SLUGS):
        row = next(r for r in rows if r['slug'] == slug)
        name = row['texture'] + '.png'
        raw = WORK / 'generated' / (slug + '.png')
        sculpture = native_statue(raw, palette)
        sculpture.save(OUT / 'statues' / name)
        placed = Image.new('RGBA', (50, 68))
        placed.alpha_composite(sculpture, (0, 0))
        placed.alpha_composite(base, (1, 3))
        placed.save(OUT / 'complete' / name)
        coarse = Image.new('RGBA', (50, 68))
        coarse.alpha_composite(native_statue(raw, palette, coarse=True), (0, 0))
        coarse.alpha_composite(base, (1, 3))
        coarse.save(OUT / 'classic-2px' / name)
        for im in [placed, coarse]:
            a = np.array(im)[3:67, 1:49]; b = np.array(base); mask = b[:, :, 3] > 0
            assert np.array_equal(a[mask], b[mask]), slug
            assert set(np.unique(np.array(im)[:, :, 3])) == {0, 255}, slug
        assert sculpture.size == (50, 50)
        shutil.copy2(OLD / row['placed_file'], OUT / 'previous-candidate' / name)
        shutil.copy2(OLD / row['reference_file'], OUT / 'source-reference' / name)
        shutil.copy2(WORK / 'generated' / (slug + '.json'), OUT / 'generation-metadata' / (slug + '.json'))
        refs = ['candidates/mantislord/000.png', 'candidates/mantislord/015.png'] if slug == 'mantislord' else ['candidates/sistersofbattle/000.png', 'candidates/sistersofbattle/015.png'] if slug == 'sistersofbattle' else ['selected-components/ghostgalien-weapon.png']
        for n, ref in enumerate(refs):
            shutil.copy2(ROOT / 'artifacts/relic-remake-native' / ref, OUT / 'source-reference' / f'{slug}-{n}.png')
        panel = Image.new('RGB', (900, 430), '#25303b'); d = ImageDraw.Draw(panel)
        for x, title, im in [(0, '上一版候选', Image.open(OLD / row['placed_file'])), (300, '修订版 · 完整圣物', placed), (600, '修订版 · 粗像素', coarse)]:
            d.text((x+20, 15), title, font=font, fill='#ffe99b')
            im = im.resize((200, 272), Image.Resampling.NEAREST)
            panel.paste(im, (x+50, 65), im)
        d.text((20, 380), row['name'], font=font, fill='white')
        panel.save(OUT / 'comparisons' / name)
        big = placed.resize((250, 340), Image.Resampling.NEAREST)
        sheet.paste(big, (i*400+75, 35), big)
        sd.text((i*400+30, 390), row['name'], font=font, fill='#ffe99b')
        desc = '三人紧凑构图，共同落脚基线。' if slug != 'ghostgalien' else '原版弯刃武器改为手持，握点连接本体。'
        records.append(dict(slug=slug, name=row['name'], texture=row['texture'], revision=desc, sculpture_size=[50, 50], complete_preview_size=[50, 68], footprint_tiles=[3, 4], pedestal_pixels_identical=True, sha256=hashlib.sha256((OUT/'complete'/name).read_bytes()).hexdigest()))
        cards.append(f'<article><h2>{html.escape(row["name"])}</h2><p>{desc}</p><img class="compare" src="comparisons/{name}"><p>原生大小：<img src="complete/{name}"> <img src="classic-2px/{name}"></p><a href="complete/{name}">完整透明 PNG</a></article>')
    sheet.save(OUT / '三件完整圣物.png')
    (OUT / 'manifest.json').write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf-8')
    (OUT / 'validation.json').write_text(json.dumps(dict(count=3, status='passed', pedestal_pixel_match=True, native_sculpture=[50,50], preview=[50,68], alpha=[0,255], integrated=False), indent=2), encoding='utf-8')
    (OUT / 'index.html').write_text('<!doctype html><meta charset="utf-8"><title>三件完整圣物修订对比</title><style>body{background:#15202b;color:#eee;font:18px system-ui;margin:24px}article{padding:20px;margin:20px 0;background:#25303b;border-radius:12px}.compare{width:min(900px,100%);image-rendering:pixelated}a{color:#ffe99b}p img{vertical-align:middle;image-rendering:pixelated}</style><h1>三件完整圣物 · 构图修订版</h1><p>左：上一版；中：修订细节版；右：修订粗像素版。保留现有名称，底座复用原版像素。未替换游戏资源。</p>'+''.join(cards), encoding='utf-8')
    (OUT / 'README.md').write_text('# 三件完整圣物构图修订版\n\n打开 index.html 查看新旧对比。complete/ 为带原版底座的完整透明 PNG；statues/ 是供后续接入的独立 50×50 雕像；classic-2px/ 为粗像素完整预览。\n\n占地依照原版 3×4 格，50×68 是包含边缘留白的静态完整预览，不是占地尺寸。原版底座逐像素复用。名称与资源名保留，未覆盖首版候选或游戏资源。\n\n螳螂两件改为紧凑的三人组合，加利安按用户要求从悬空武器改为手持原版武器。构图经过重排，不代表游戏中存在完全相同的动画帧。尚未接入游戏。\n', encoding='utf-8')
    archive = ROOT / 'artifacts' / (OUT.name + '.zip')
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
        for p in OUT.rglob('*'):
            if p.is_file(): z.write(p, p.relative_to(OUT.parent))
    with zipfile.ZipFile(archive) as z: assert z.testzip() is None
    print(json.dumps(dict(count=3, output=str(OUT), zip=str(archive)), ensure_ascii=False))

if __name__ == '__main__': main()
