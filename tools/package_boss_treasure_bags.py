"""Package original imagegen treasure-bag art and validate delivered bytes."""
import html
import json
import math
import shutil
import zipfile
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/boss-treasure-bags'
PACKAGE = ROOT / '空洞骑士大师宝藏袋素材包'
ARCHIVE = ROOT / 'artifacts/空洞骑士大师宝藏袋素材包.zip'


def main():
    rows = json.loads((OUT / 'manifest.json').read_text(encoding='utf-8'))
    records = sorted((OUT / 'results').glob('*.json')) + sorted((OUT / 'revisions').glob('*.json'))
    results = {}
    for path in records:
        entry = json.loads(path.read_text(encoding='utf-8'))
        results[entry['slug']] = entry
    PACKAGE.mkdir(exist_ok=True)
    complete, problems = [], []
    for row in rows:
        if row['slug'] in results:
            row.update(results[row['slug']])
        if not row.get('source'):
            continue
        dest = OUT / row['file']
        shutil.copyfile(row['source'], dest)
        with Image.open(dest) as im:
            im.load()
            if im.mode != 'RGBA':
                problems.append({'slug': row['slug'], 'problem': 'Missing RGBA alpha'})
                continue
            alpha = im.getchannel('A')
            hist = alpha.histogram()
            bbox = alpha.point(lambda a: 255 if a >= 128 else 0).getbbox()
            clear = hist[0] / (im.width * im.height)
            if clear < .05 or alpha.getextrema() != (0, 255):
                problems.append({'slug': row['slug'], 'problem': 'Insufficient transparency', 'clear': clear})
            if bbox and (bbox[0] == 0 or bbox[1] == 0 or bbox[2] == im.width or bbox[3] == im.height):
                problems.append({'slug': row['slug'], 'problem': 'Artwork touches canvas edge'})
            row.update(status='generated', width=im.width, height=im.height, transparent_ratio=round(clear, 4), bbox=bbox)
        shutil.copyfile(dest, PACKAGE / row['file'])
        complete.append(row)

    cols, cw, ch = 6, 220, 250
    contact = Image.new('RGB', (cols * cw, max(1, math.ceil(len(complete)/cols))*ch), '#17202c')
    draw = ImageDraw.Draw(contact)
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 16)
    small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 11)
    cards = []
    for i, row in enumerate(complete):
        with Image.open(OUT / row['file']) as im:
            thumb = im.copy()
        thumb.thumbnail((196, 190), Image.Resampling.NEAREST)
        x, y = (i % cols)*cw, (i // cols)*ch
        contact.paste(thumb, (x+(cw-thumb.width)//2, y+8+(190-thumb.height)//2), thumb)
        draw.text((x+10, y+204), row['name'], font=font, fill='#f2e5c9')
        draw.text((x+10, y+228), row['slug'], font=small, fill='#aab8cb')
        title = html.escape(row['name'])
        cards.append(f'<a class="card" href="{row["file"]}" target="_blank"><div class="picture"><img src="{row["file"]}" alt="{title}"></div><b>{title}</b><small>{html.escape(row["en"])}</small><small>{row["slug"]} · {row["width"]}×{row["height"]}</small></a>')
    contact.save(OUT / '总览.png')
    gallery = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>空洞骑士大师宝藏袋</title><style>body{margin:0;padding:30px;background:#17202c;color:#f2e5c9;font:16px/1.7 "Microsoft YaHei",sans-serif}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(210px,1fr));gap:16px}.card{padding:12px;background:#222e3e;color:inherit;text-decoration:none;border:1px solid #41526a}.picture{height:210px;display:flex;align-items:center;justify-content:center;background:repeating-conic-gradient(#dde0e5 0% 25%,#b6bdc8 0% 50%) 50%/20px 20px}.picture img{max-height:200px;max-width:100%;object-fit:contain;image-rendering:pixelated}b,small{display:block}small{font-size:12px;color:#b3c0d2}.card[hidden]{display:none}input{padding:12px;margin-bottom:24px;width:min(480px,85%);font:inherit}</style><h1>空洞骑士 · 大师宝藏袋</h1><p>独立透明 PNG 原图。点击查看原尺寸；下方棋盘仅用于展示透明背景。</p><input placeholder="搜索中文、英文或 ID" oninput="document.querySelectorAll('.card').forEach(a=>a.hidden=!a.textContent.toLowerCase().includes(this.value.toLowerCase()))"><div class="grid">'''+''.join(cards)+'</div></html>'
    (OUT / 'index.html').write_text(gallery, encoding='utf-8')
    (OUT / 'manifest.json').write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')
    (OUT / 'prompts.json').write_text(json.dumps([{'slug':r['slug'], 'name':r['name'], 'prompt':r['prompt']} for r in rows], ensure_ascii=False, indent=2), encoding='utf-8')
    (OUT / 'README.md').write_text(f'''# 空洞骑士大师宝藏袋素材包

当前已生成 {len(complete)} / {len(rows)} 款。使用内置 image_gen 逐款生成，参考泰拉瑞亚 Boss 宝藏袋的扎口袋结构和像素表现。

- 每款独立透明 PNG，保留生成器原始尺寸和透明通道；未强制压缩为 32×32 或 48×48。
- `总览.png` 为带名称的深色背景预览；`index.html` 用棋盘背景检查透明边缘，可搜索中文名、英文名或 ID。
- 覆盖项目 48 项 Boss／战斗组合，普通与强化形态分开；包括苍白潜伏者和左特两个额外条目。
- `manifest.json` 记录 ID、尺寸、边界和透明比例；`prompts.json` 保存逐款提示词。
- 本包为大师模式掉落物的美术素材；尚未接入物品注册、掉落条件、开袋奖励或联机逻辑。
- 游戏内显示尺寸及缩小后的可读性尚未验证，建议接入时按实际背包槽尺寸调整。
''', encoding='utf-8')
    report = {'requested':len(rows), 'generated':len(complete), 'pending':[r['slug'] for r in rows if r not in complete], 'problems':problems, 'generator':'built-in image_gen', 'runtime_integration_tested':False}
    (OUT / 'validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    extra = ['总览.png', 'index.html', 'manifest.json', 'prompts.json', 'README.md', 'validation.json']
    for name in extra:
        shutil.copyfile(OUT / name, PACKAGE / name)
    with zipfile.ZipFile(ARCHIVE, 'w', zipfile.ZIP_DEFLATED) as z:
        for row in complete:
            z.write(PACKAGE / row['file'], row['file'])
        for name in extra:
            z.write(PACKAGE / name, name)
    with zipfile.ZipFile(ARCHIVE) as z:
        report['zip_error'] = z.testzip()
        report['packaged_png_bytes_match'] = all(z.read(r['file']) == (OUT / r['file']).read_bytes() for r in complete)
        report['archive_png_count'] = sum(n.endswith('.png') and n != '总览.png' for n in z.namelist())
    (OUT / 'validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    shutil.copyfile(OUT / 'validation.json', PACKAGE / 'validation.json')
    # Rebuild with the completed report so the loose package and archive agree.
    with zipfile.ZipFile(ARCHIVE, 'w', zipfile.ZIP_DEFLATED) as z:
        for row in complete:
            z.write(PACKAGE / row['file'], row['file'])
        for name in extra:
            z.write(PACKAGE / name, name)
    with zipfile.ZipFile(ARCHIVE) as z:
        assert z.testzip() is None
        assert all(z.read(name) == (PACKAGE / name).read_bytes() for name in z.namelist())
    print(json.dumps(report, ensure_ascii=True))


if __name__ == '__main__':
    main()
