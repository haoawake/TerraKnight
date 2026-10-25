"""Collect generated relic PNGs, audit transparency, and build a review gallery."""
import html
import json
import math
import shutil
import zipfile
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/boss-relics-final'


def main():
    manifest = json.loads((OUT / 'manifest.json').read_text(encoding='utf-8'))
    by_id = {row['slug']: row for row in manifest}
    by_id['hornet'].update(name='大黄蜂·守护者', en='Hornet Protector')
    by_id['hornet-4'].update(name='大黄蜂·哨兵', en='Hornet Sentinel')
    by_id['falseknight'].setdefault('prompt', "Use case: stylized-concept. Create ONE finished transparent PNG game relic asset for Hollow Knight's FALSE KNIGHT, inspired by Terraria Master Mode boss relics. Image 1 is a STYLE reference: warm monochrome gold statuettes with pale yellow highlights, amber middle tones, dark ochre recesses. Image 2 is the exact BOSS IDENTITY reference: preserve the huge round layered armor, two tall curved helmet horns, tiny black eye slits, stocky legs, and long horizontal mace with a large round spiked head. Turn this specific boss into a miniature solid sculpted GOLD STATUE, mounted above a small ornate gold rectangular plinth, with a thin dark amber underside. Three-quarter/front game item view. Boss sculpture occupies the upper 75%, plinth lower 15%, generous transparent margin. High-resolution finished pixel-art-inspired game illustration with crisp deliberate contours and readable chunky forms; not restricted to 48x32 pixels. Match Terraria relic visual vocabulary rather than a realistic photograph. Only gold metal tones, no original purple, no colored gems. Entire isolated statuette and base visible, no scene, no ground plane, no cast shadow outside the object, no text, no border, no checkerboard. Genuine transparent background. Keep silhouette compact and recognizably False Knight, no extra horns or limbs.")
    files = (sorted(OUT.glob('batch-results-*.json'))
             + sorted((OUT / 'results').glob('*.json'))
             + sorted((OUT / 'revisions').glob('*.json')))
    for path in files:
        data = json.loads(path.read_text(encoding='utf-8'))
        for entry in data if isinstance(data, list) else [data]:
            if not entry.get('source'):
                continue
            row = by_id[entry['slug']]
            shutil.copyfile(entry['source'], OUT / row['file'])
            row.update({key: entry[key] for key in ('source', 'prompt') if key in entry})
            row['status'] = 'generated'
    complete = []
    problems = []
    for row in manifest:
        path = OUT / row['file']
        if not path.is_file():
            continue
        image = Image.open(path)
        if image.mode != 'RGBA':
            problems.append({'slug': row['slug'], 'problem': 'No alpha channel'})
            continue
        alpha = image.getchannel('A')
        histogram = alpha.histogram()
        transparent = histogram[0] / (image.width * image.height)
        if transparent < 0.05:
            problems.append({'slug': row['slug'], 'problem': 'Insufficient clear background', 'transparent_ratio': transparent})
        row.update(width=image.width, height=image.height, alpha_min=alpha.getextrema()[0],
                   alpha_max=alpha.getextrema()[1], transparent_ratio=round(transparent, 4))
        complete.append(row)
    (OUT / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    report = {'requested': len(manifest), 'generated': len(complete), 'pending': [r['slug'] for r in manifest if r not in complete], 'problems': problems}
    (OUT / 'validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    font_path = 'C:/Windows/Fonts/msyh.ttc'
    label_font = ImageFont.truetype(font_path, 17)
    small_font = ImageFont.truetype(font_path, 11)
    cols, cw, ch = 6, 256, 258
    contact = Image.new('RGB', (cols * cw, max(1, math.ceil(len(complete) / cols)) * ch), '#152b25')
    draw = ImageDraw.Draw(contact)
    cards = []
    for n, row in enumerate(complete):
        image = Image.open(OUT / row['file']).convert('RGBA')
        image.thumbnail((cw - 24, ch - 56), Image.Resampling.LANCZOS)
        x, y = (n % cols) * cw, (n // cols) * ch
        contact.paste(image, (x + (cw - image.width) // 2, y + 8 + (ch - 56 - image.height) // 2), image)
        draw.text((x + 12, y + ch - 43), row['name'], font=label_font, fill='#e5d89b')
        draw.text((x + 12, y + ch - 19), row['slug'], font=small_font, fill='#9da998')
        title = html.escape(row['name'])
        cards.append(f'<a class="card" href="{row["file"]}" target="_blank"><div class="picture"><img loading="lazy" src="{row["file"]}" alt="{title}"></div><strong>{title}</strong><small>{html.escape(row["en"])} · {row["width"]}×{row["height"]}</small><small>{html.escape(row["slug"])}</small></a>')
    if complete:
        contact.save(OUT / '总览.png')
    gallery = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>空洞骑士 · 大师圣物图集</title><style>body{margin:0;background:#14251f;color:#ecdfb4;font:16px/1.7 "Microsoft YaHei",sans-serif;padding:32px}h1{font-size:28px}p{color:#b7bfa6}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:18px}.card{display:block;text-decoration:none;color:inherit;border:1px solid #415345;padding:12px;background:#1e342b}.picture{height:210px;display:flex;align-items:center;justify-content:center;background:repeating-conic-gradient(#f0f0e8 0% 25%,#d4d8c9 0% 50%) 50%/20px 20px}.picture img{max-width:100%;max-height:100%;object-fit:contain}.card strong,.card small{display:block}.card small{color:#abb99e;font-size:12px}input{background:#f6f4e7;color:#173227;padding:12px;border:0;width:min(500px,85%);margin:0 0 24px;font:inherit}</style><h1>空洞骑士 · 大师圣物图集</h1><p>独立高分辨率透明 PNG。点击看原图；底座与雕像合为一张。多人首领采用组合圣物。</p><input type="search" placeholder="搜索中文名、英文名或首领 ID" oninput="document.querySelectorAll('.card').forEach(a=>a.hidden=!a.textContent.toLowerCase().includes(this.value.toLowerCase()))"><div class="grid">'''+''.join(cards)+'''</div><style>.card[hidden]{display:none}</style></html>'''
    (OUT / 'index.html').write_text(gallery, encoding='utf-8')
    prompts = [{'id': row['slug'], 'name': row['name'], 'prompt': row.get('prompt', ''), 'reference': Path(row['reference']).name} for row in complete]
    (OUT / 'prompts.json').write_text(json.dumps(prompts, ensure_ascii=False, indent=2), encoding='utf-8')
    (OUT / 'README.md').write_text(f'''# 空洞骑士大师圣物美术素材

当前生成 {len(complete)} / {len(manifest)} 张。使用内置 image_gen，参考本地泰拉瑞亚大师圣物与空洞骑士首领资源。

- 每个首领／战斗组合独立一张高分辨率透明 PNG，保留生成器原图，不压成 48×32。
- 查看 `index.html` 可在棋盘背景上检查透明边缘；`总览.png` 是带名称的预览，不作为透明贴图使用。
- 原版圣物的占格、上方雕像与底座分开绘制。本素材是合成的静态美术原图，尚未制作游戏放置帧、悬浮动画或掉落逻辑。
- 清单覆盖当前 Mod 的首领记录，并补暴怒守卫、战斗姐妹、有翼诺斯克、无上辐光；另保留苍白潜伏者与斗兽场左特这两个额外条目。
- 多人战如守望者骑士、螳螂领主、奥罗与马托使用组合圣物；普通与梦境强化形态分别提供图片。
- `manifest.json` 含中文名、英文名、ID、文件名与透明通道检查；`prompts.json` 保存提示词。
- 这是美术素材包，尚未接入游戏或 Wiki。
''', encoding='utf-8')
    archive_path = ROOT / 'artifacts/空洞骑士大师圣物素材包.zip'
    with zipfile.ZipFile(archive_path, 'w', zipfile.ZIP_DEFLATED) as archive:
        for row in complete:
            archive.write(OUT / row['file'], row['file'])
        for name in ('index.html', '总览.png', 'README.md', 'manifest.json', 'prompts.json', 'validation.json'):
            path = OUT / name
            if path.is_file():
                archive.write(path, name)
    print(json.dumps(report | {'archive_bytes': archive_path.stat().st_size}, ensure_ascii=True))


if __name__ == '__main__':
    main()
