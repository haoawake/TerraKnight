"""Package reference-driven boss bag redesigns with side-by-side visual review sheets."""
import argparse
import html
import json
import math
import shutil
import textwrap
import zipfile
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/boss-treasure-bags-v2'
PACKAGE = ROOT / '空洞骑士大师宝藏袋素材包-结构重制版'
ARCHIVE = ROOT / 'artifacts/空洞骑士大师宝藏袋素材包-结构重制版.zip'


def write_json(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')


def paste_fit(canvas, source, box, nearest=False):
    with Image.open(source) as image:
        image = image.convert('RGBA')
        image.thumbnail((box[2]-box[0], box[3]-box[1]), Image.Resampling.NEAREST if nearest else Image.Resampling.LANCZOS)
        canvas.paste(image, (box[0]+(box[2]-box[0]-image.width)//2, box[1]+(box[3]-box[1]-image.height)//2), image)


def main(package=False):
    rows = json.loads((OUT / 'manifest.json').read_text(encoding='utf-8'))
    for path in sorted((OUT / 'results').glob('*.json')) + sorted((OUT / 'revisions').glob('*.json')):
        entry = json.loads(path.read_text(encoding='utf-8'))
        next(r for r in rows if r['slug'] == entry['slug']).update(entry)
    complete, problems = [], []
    for row in rows:
        if not row.get('source'):
            continue
        target = OUT / row['file']
        shutil.copyfile(row['source'], target)
        with Image.open(target) as im:
            im.load()
            if im.mode != 'RGBA':
                problems.append({'slug':row['slug'], 'problem':'No RGBA alpha channel'})
                continue
            alpha = im.getchannel('A')
            bbox = alpha.point(lambda a:255 if a >= 128 else 0).getbbox()
            clear = alpha.histogram()[0] / (im.width*im.height)
            alpha_min, alpha_max = alpha.getextrema()
            # A generated interior at 254/255 is valid, nearly opaque artwork.
            if clear < .05 or alpha_min != 0 or alpha_max < 240:
                problems.append({'slug':row['slug'], 'problem':'Insufficient transparency'})
            if bbox and (bbox[0] == 0 or bbox[1] == 0 or bbox[2] == im.width or bbox[3] == im.height):
                problems.append({'slug':row['slug'], 'problem':'Visible art touches canvas edge'})
            row.update(status='generated', width=im.width, height=im.height, transparent_ratio=round(clear,4), alpha_min=alpha_min, alpha_max=alpha_max, bbox=bbox)
        local_ref = OUT / 'references' / Path(row['reference']).name
        if Path(row['reference']).resolve() != local_ref.resolve():
            shutil.copyfile(row['reference'], local_ref)
        row['reference_file'] = 'references/' + local_ref.name
        complete.append(row)

    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 16)
    small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 12)
    cols, cw, ch = 6, 240, 260
    sheet = Image.new('RGB', (cols*cw, max(1, math.ceil(len(complete)/cols))*ch), '#202a38')
    d = ImageDraw.Draw(sheet)
    for i, row in enumerate(complete):
        x, y = (i%cols)*cw, (i//cols)*ch
        paste_fit(sheet, OUT/row['file'], (x+10,y+8,x+cw-10,y+208), True)
        d.text((x+10,y+218), row['name'], font=font, fill='#f1ead9')
        d.text((x+10,y+244), row['slug'], font=small, fill='#acbccd')
    sheet.save(OUT/'总览.png')

    comparison_files = []
    for part in range(math.ceil(len(complete)/12)):
        group = complete[part*12:(part+1)*12]
        panel = Image.new('RGB', (1380,1120), '#202a38')
        d = ImageDraw.Draw(panel)
        for i, row in enumerate(group):
            x,y = (i%3)*460, (i//3)*280
            d.rectangle((x+4,y+3,x+454,y+274), outline='#465267')
            paste_fit(panel, OUT/row['reference_file'], (x+10,y+20,x+185,y+204))
            paste_fit(panel, OUT/row['file'], (x+212,y+12,x+450,y+204), True)
            d.text((x+10,y+5), 'Boss 原图', font=small, fill='#95a8bf')
            d.text((x+212,y+5), '结构重制宝藏袋', font=small, fill='#95a8bf')
            d.text((x+10,y+209), row['name'], font=font, fill='#f1ead9')
            for line_n, line in enumerate(textwrap.wrap(row['features_cn'], width=31)):
                d.text((x+10,y+235+line_n*18), line, font=small, fill='#acbccd')
        name = f'逐款对照-{part+1:02}.png'
        panel.save(OUT/name)
        comparison_files.append(name)

    cards=[]
    for row in complete:
        cards.append(f'''<article class="card"><h2>{html.escape(row['name'])}</h2><div class="pair"><figure><img src="{row['reference_file']}" alt="Boss 外观参考"><figcaption>Boss 外观参考</figcaption></figure><figure class="checker"><a href="{row['file']}" target="_blank"><img class="pixel" src="{row['file']}" alt="{html.escape(row['name'])}宝藏袋"></a><figcaption>透明 PNG · 点击原图</figcaption></figure></div><p>{html.escape(row['features_cn'])}</p><small>{html.escape(row['en'])} · {row['slug']}</small><details><summary>参考范围</summary>{html.escape(row['reference_notes'])}</details></article>''')
    page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>空洞骑士宝藏袋 · 结构重制版</title><style>*{box-sizing:border-box}body{margin:0;padding:28px;background:#202a38;color:#f1ead9;font:16px/1.6 "Microsoft YaHei",sans-serif}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(400px,1fr));gap:18px}.card{background:#283545;padding:16px;border:1px solid #465267}.card[hidden]{display:none}h1{font-size:26px}h2{font-size:19px;margin:0 0 12px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:10px}figure{margin:0;height:230px;display:flex;align-items:center;justify-content:center;flex-direction:column}figure img{max-width:100%;height:192px;object-fit:contain}.pixel{image-rendering:pixelated}.checker{background:repeating-conic-gradient(#c3c9d2 0% 25%,#9da7b4 0% 50%) 50%/20px 20px;color:#192433}figcaption{font-size:12px}p{color:#d0dae4}small,details{font-size:12px;color:#a9b9cc}input{padding:12px;width:min(500px,90%);margin-bottom:25px;font:inherit}@media(max-width:480px){body{padding:12px}.grid{grid-template-columns:1fr}figure img{height:150px}figure{height:190px}}</style><h1>空洞骑士宝藏袋 · 结构重制版</h1><p>把 Boss 的轮廓、身体材质与装备做成袋子本身。原图并排对照；不使用脸部徽章。强化形态部分采用同族外观图，具体参考范围在各卡片说明中。</p><input type="search" placeholder="搜索 Boss 名称、特征或 ID" oninput="document.querySelectorAll('.card').forEach(a=>a.hidden=!a.textContent.toLowerCase().includes(this.value.toLowerCase()))"><div class="grid">'''+''.join(cards)+'</div></html>'
    (OUT/'index.html').write_text(page,encoding='utf-8')
    design_md = '# 逐款结构设计与验收点\n\n主原则：袋子必须仍有束口与封闭袋底；用外形、材质、分节、翅翼、披风、武器和战斗结构识别 Boss，不贴脸部徽章。\n\n| ID | Boss | 需要能看见的特征 |\n|---|---|---|\n'+''.join(f"| {r['slug']} | {r['name']} | {r['features_cn']} |\n" for r in rows)
    (OUT/'逐款设计说明.md').write_text(design_md, encoding='utf-8')
    (OUT/'README.md').write_text(f'''# 空洞骑士大师宝藏袋 · 结构重制版

已生成 {len(complete)} / {len(rows)} 款，使用内置 image_gen，逐款传入 Boss 外观图和本机泰拉瑞亚原版宝藏袋风格参考。

- 独立透明 PNG；保留生成原图尺寸与透明通道，尚未制作固定尺寸的游戏精灵导出。
- `总览.png` 展示全套；`逐款对照-*.png` 及 `index.html` 将 Boss 外观与袋子并排对照。
- `逐款设计说明.md` 列出每款的中文结构识别点；`prompts.json` 保留全部提示词和参考范围。
- 参考图只作外观对照，位于 `references`；不作为可用掉落图标。
- 原版与强化形态的实际外观基本相同者保留同一核心外观，以梦境绳、灵魂球、裂损、陷阱等结构区分。无上辐光、暴怒守卫、战斗姐妹用同族外观参考；有翼诺斯克以诺斯克感染腹部和大黄蜂分叉诱饵作部分参考，膜翼由设计明确指定。
- `validation.json` 记录文件和透明通道检查；`visual-review.json` 如存在，则记录人工逐款视觉复核结果。
- 当前仅交付美术素材；未接入大师掉落、开袋奖励或联机逻辑，未验证游戏内缩小显示效果。
''',encoding='utf-8')
    write_json(OUT/'manifest.json', rows)
    write_json(OUT/'prompts.json', [{k:r.get(k) for k in ('slug','name','traits','design','acceptance','features_cn','prompt','revision_prompt','reference','reference_notes')} for r in rows])
    report={'requested':len(rows),'generated':len(complete),'pending':[r['slug'] for r in rows if r not in complete],'problems':problems,'generator':'built-in image_gen','runtime_integration_tested':False}
    write_json(OUT/'validation.json',report)

    if package:
        PACKAGE.mkdir(exist_ok=True)
        files=[r['file'] for r in complete]+['总览.png','index.html','逐款设计说明.md','README.md','manifest.json','prompts.json','validation.json']+comparison_files
        files += sorted(set(r['reference_file'] for r in complete))
        files += ['references/terraria-treasure-bags.png']
        if (OUT/'visual-review.json').exists():
            files.append('visual-review.json')
        for name in files:
            target=PACKAGE/name
            target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(OUT/name,target)
        def build_archive():
            with zipfile.ZipFile(ARCHIVE,'w',zipfile.ZIP_DEFLATED) as z:
                for name in files:
                    z.write(PACKAGE/name,name)
        build_archive()
        with zipfile.ZipFile(ARCHIVE) as z:
            report.update(zip_error=z.testzip(),packaged_png_bytes_match=all(z.read(r['file'])==(OUT/r['file']).read_bytes() for r in complete),archive_icon_count=sum(n.endswith('.png') and '/' not in n and n!='总览.png' and not n.startswith('逐款对照-') for n in z.namelist()))
        write_json(OUT/'validation.json',report)
        shutil.copyfile(OUT/'validation.json',PACKAGE/'validation.json')
        build_archive()
        with zipfile.ZipFile(ARCHIVE) as z:
            assert z.testzip() is None
            assert all(z.read(name)==(PACKAGE/name).read_bytes() for name in z.namelist())
    print(json.dumps(report,ensure_ascii=True))


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--package',action='store_true')
    main(parser.parse_args().package)
