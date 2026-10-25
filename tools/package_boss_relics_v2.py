"""Assemble faithful gold cutouts onto one front-elevation base, at exact 2x3-cell sizes."""
import argparse
import hashlib
import html
import json
import math
import shutil
import zipfile
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/boss-relics-v2'
PACKAGE = ROOT / '空洞骑士大师圣物素材包-原图重制版'
ARCHIVE = ROOT / 'artifacts/空洞骑士大师圣物素材包-原图重制版.zip'


def write_json(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')


def crop_art(path):
    with Image.open(path) as im:
        im = im.convert('RGBA')
        box = im.getchannel('A').point(lambda a:255 if a>=16 else 0).getbbox()
        if box is None:
            raise ValueError(f'Empty image: {path}')
        return im.crop(box)


def fit_art(image, width, height):
    image = image.copy()
    image.thumbnail((width, height), Image.Resampling.LANCZOS)
    # Resampling adds nearly transparent fringe rows. Anchor the visible edge,
    # rather than the canvas edge, directly against the pedestal.
    box = image.getchannel('A').point(lambda a:255 if a>=128 else 0).getbbox()
    if box is None:
        raise ValueError('Resized statue has no visible pixels')
    return image.crop(box)


def touches_pedestal(image, top):
    alpha = image.getchannel('A')
    return any(alpha.getpixel((x,top-1))>=128 and alpha.getpixel((x,top))>=64
               for x in range(image.width))


def paste_preview(canvas, path, box, nearest=False):
    with Image.open(path) as im:
        im = im.convert('RGBA')
        im.thumbnail((box[2]-box[0],box[3]-box[1]),Image.Resampling.NEAREST if nearest else Image.Resampling.LANCZOS)
        canvas.paste(im,(box[0]+(box[2]-box[0]-im.width)//2,box[1]+(box[3]-box[1]-im.height)//2),im)


def main(package=False, install=False):
    rows=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
    for row in rows:
        row.pop('revision_history',None)
    for path in sorted((OUT/'results').glob('*.json'))+sorted((OUT/'revisions').glob('*.json')):
        entry=json.loads(path.read_text(encoding='utf-8'))
        row=next(r for r in rows if r['slug']==entry['slug'])
        row.update(entry)
        if entry.get('revision_prompt'):
            row.setdefault('revision_history',[]).append({'record':path.name,'prompt':entry['revision_prompt'],'source':entry['source']})
    base_info=json.loads((OUT/'pedestal.json').read_text(encoding='utf-8'))
    base_source=crop_art(base_info['source'])
    base=base_source.resize((64,16),Image.Resampling.LANCZOS)
    base_hd=base_source.resize((768,192),Image.Resampling.LANCZOS)
    # Canonical transparent RGB matches the same base alpha-composited into a relic.
    clear_base=Image.new('RGBA',base.size)
    clear_base.alpha_composite(base)
    base=clear_base
    clear_hd=Image.new('RGBA',base_hd.size)
    clear_hd.alpha_composite(base_hd)
    base_hd=clear_hd
    base.save(OUT/'pedestal-64x16.png')
    base_hd.save(OUT/'pedestal-768x192.png')
    base_hash=hashlib.sha256(base.tobytes()).hexdigest()
    (OUT/'statues').mkdir(exist_ok=True)
    (OUT/'hd').mkdir(exist_ok=True)
    complete,problems=[],[]
    for row in rows:
        if not row.get('source'):
            continue
        row['statue_file']='statues/'+row['file']
        row['hd_file']='hd/'+row['file']
        shutil.copyfile(row['source'],OUT/row['statue_file'])
        with Image.open(row['source']) as raw:
            if raw.mode!='RGBA' or raw.getchannel('A').getextrema()[0]!=0:
                problems.append({'slug':row['slug'],'problem':'Raw cutout lacks clear alpha'})
        statue=crop_art(row['source'])
        icon=Image.new('RGBA',(64,96))
        small=fit_art(statue,60,76)
        sx,sy=(64-small.width)//2,80-small.height
        icon.alpha_composite(small,(sx,sy))
        icon.alpha_composite(base,(0,80))
        icon.save(OUT/row['file'])
        hd=Image.new('RGBA',(768,1152))
        large=fit_art(statue,720,912)
        hd.alpha_composite(large,((768-large.width)//2,960-large.height))
        hd.alpha_composite(base_hd,(0,960))
        hd.save(OUT/row['hd_file'])
        alpha=icon.getchannel('A')
        clear=alpha.histogram()[0]/(64*96)
        if icon.size!=(64,96) or hd.size!=(768,1152) or clear<.05:
            problems.append({'slug':row['slug'],'problem':'Invalid final dimensions or transparency'})
        if icon.crop((0,80,64,96)).tobytes()!=base.tobytes():
            problems.append({'slug':row['slug'],'problem':'Pedestal differs from common front-elevation base'})
        if hd.crop((0,960,768,1152)).tobytes()!=base_hd.tobytes():
            problems.append({'slug':row['slug'],'problem':'HD pedestal differs from common front-elevation base'})
        contact=touches_pedestal(icon,80) and touches_pedestal(hd,960)
        if not contact:
            problems.append({'slug':row['slug'],'problem':'Visible statue does not touch pedestal'})
        local_ref=OUT/'references'/Path(row['reference']).name
        if Path(row['reference']).resolve()!=local_ref.resolve():
            shutil.copyfile(row['reference'],local_ref)
        row.update(status='generated',width=64,height=96,hd_width=768,hd_height=1152,tiles_w=2,tiles_h=3,
                   transparent_ratio=round(clear,4),statue_box=[sx,sy,sx+small.width,sy+small.height],
                   pedestal_box=[0,80,64,96],pedestal_sha256=base_hash,reference_file='references/'+local_ref.name)
        row['statue_touches_pedestal']=contact
        complete.append(row)

    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',16)
    small_font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',12)
    cols,cw,ch=6,220,340
    sheet=Image.new('RGB',(cols*cw,max(1,math.ceil(len(complete)/cols))*ch),'#203229')
    d=ImageDraw.Draw(sheet)
    for i,row in enumerate(complete):
        x,y=(i%cols)*cw,(i//cols)*ch
        with Image.open(OUT/row['file']) as im:
            im=im.resize((192,288),Image.Resampling.NEAREST)
            sheet.paste(im,(x+14,y+3),im)
        d.text((x+8,y+295),row['name'],font=font,fill='#f0dfa1')
        d.text((x+8,y+321),row['slug'],font=small_font,fill='#acbaa9')
    sheet.save(OUT/'总览.png')
    comparison_files=[]
    for part in range(math.ceil(len(complete)/12)):
        panel=Image.new('RGB',(1320,1400),'#203229')
        d=ImageDraw.Draw(panel)
        for i,row in enumerate(complete[part*12:(part+1)*12]):
            x,y=(i%3)*440,(i//3)*350
            d.rectangle((x+3,y+3,x+435,y+345),outline='#526047')
            paste_preview(panel,OUT/row['reference_file'],(x+10,y+30,x+210,y+285))
            with Image.open(OUT/row['file']) as im:
                im=im.resize((192,288),Image.Resampling.NEAREST)
                panel.paste(im,(x+230,y+12),im)
            # Grid is only a review overlay, never baked into the delivered texture.
            for line in range(3):
                d.line((x+230+line*96,y+12,x+230+line*96,y+300),fill='#556345')
            for line in range(4):
                d.line((x+230,y+12+line*96,x+422,y+12+line*96),fill='#556345')
            d.text((x+10,y+9),'原版 Boss 外观',font=small_font,fill='#aab8a5')
            d.text((x+10,y+301),row['name'],font=font,fill='#f0dfa1')
            d.text((x+10,y+327),'2 格宽 × 3 格高 · 64×96 · 同一正视底座',font=small_font,fill='#aab8a5')
        name=f'原图对照-{part+1:02}.png'
        panel.save(OUT/name)
        comparison_files.append(name)

    cards=[]
    for r in complete:
        cards.append(f'''<article class="card"><h2>{html.escape(r['name'])}</h2><div class="pair"><figure><img class="ref" src="{r['reference_file']}" alt="原版外观参考"><figcaption>原版外观参考</figcaption></figure><figure><a class="sprite" href="{r['hd_file']}" target="_blank"><img src="{r['file']}" alt="{html.escape(r['name'])}圣物"></a><figcaption>2×3 格 · 点击高清版</figcaption></figure></div><small>{r['slug']} · 64×96 / 768×1152</small><details><summary>参考与尺寸</summary>{html.escape(r['reference_note'])}<p>保留原比例等比缩放，宽体 Boss 的画布上方留白。底座为同一张正视图，位于最下方 64×16 区域。</p></details></article>''')
    page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>圣物原图重制版 · 2×3 格</title><style>*{box-sizing:border-box}body{margin:0;padding:28px;background:#203229;color:#f0dfa1;font:16px/1.6 "Microsoft YaHei",sans-serif}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(410px,1fr));gap:18px}.card{border:1px solid #526047;padding:16px;background:#293d30}.card[hidden]{display:none}h1{font-size:25px}h2{font-size:18px;margin:0 0 12px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:10px}figure{margin:0;height:330px;display:flex;align-items:center;justify-content:center;flex-direction:column}.ref{max-width:100%;max-height:288px;object-fit:contain}.sprite{display:block;width:192px;height:288px;background:repeating-conic-gradient(#bac4b8 0% 25%,#8c9c89 0% 50%) 50%/24px 24px;position:relative}.sprite img{width:192px;height:288px;image-rendering:pixelated}.showgrid .sprite:after{content:"";position:absolute;inset:0;pointer-events:none;background:repeating-linear-gradient(90deg,transparent 0 95px,#91a47c 95px 96px),repeating-linear-gradient(0deg,transparent 0 95px,#91a47c 95px 96px)}figcaption,small,details{font-size:12px;color:#b4c2aa}input[type=search]{padding:12px;width:min(500px,90%);margin:0 12px 24px 0;font:inherit}a{color:inherit}@media(max-width:480px){body{padding:10px}.grid{grid-template-columns:1fr}.pair{grid-template-columns:1fr}figure{height:auto;min-height:220px}}</style><h1>空洞骑士大师圣物 · 原图重制版</h1><p>以原版 Boss 外观为准转换金质雕像，统一 2 格宽 × 3 格高，共用完全正视的底座。强化形态与普通形态原本相同的部分保留相同外观，不添加虚构装饰。</p><input type="search" placeholder="搜索中文、英文或 ID" oninput="document.querySelectorAll('.card').forEach(a=>a.hidden=!a.textContent.toLowerCase().includes(this.value.toLowerCase()))"><label><input type="checkbox" onchange="document.body.classList.toggle('showgrid',this.checked)"> 显示占格</label><div class="grid">'''+''.join(cards)+'</div></html>'
    (OUT/'index.html').write_text(page,encoding='utf-8')
    write_json(OUT/'manifest.json',rows)
    write_json(OUT/'prompts.json',{'pedestal':base_info,'statues':[{k:r.get(k) for k in ('slug','name','prompt','revision_prompt','revision_history','reference','reference_note')} for r in rows]})
    (OUT/'README.md').write_text(f'''# 空洞骑士大师圣物 · 原图重制版

已生成 {len(complete)} / {len(rows)} 款。使用内置 image_gen 逐款参考原版 Boss 外观转换金质立像，再进行统一尺寸排版。

- 根目录每款 PNG 为 **64×96 像素，项目内两格宽、三格高**（每格 32 像素）。
- `hd/` 为统一 768×1152 高清版；未拉伸 Boss 身体比例，宽体 Boss 会留出画布上方空白。
- 所有款式的底部 64×16 像素完全相同，来自同一张完全正视底座，不露顶面或侧面。
- `index.html` 与 `原图对照-*.png` 供逐款对照；对照图中的占格线不在实际贴图内。
- `manifest.json` 记录尺寸、雕像区域、底座区域和底座像素哈希；`prompts.json` 保存提示词。
- 一些强化形态和普通形态使用同一原版外观，保留原有结构，不添加虚构光环或装备。
- 原始生成立像保存在工作目录 `artifacts/boss-relics-v2/statues/`，旧贴图和代码备份保存在 `backup-before-install/`。
- `validation.json` 记录透明通道、统一尺寸、统一底座和压缩包校验；游戏验证结果另存运行报告。
''',encoding='utf-8')
    report={'requested':len(rows),'generated':len(complete),'pending':[r['slug'] for r in rows if r not in complete],
            'problems':problems,'native_size':[64,96],'hd_size':[768,1152],'tiles':[2,3],
            'identical_front_pedestal':not any(p['problem']=='Pedestal differs from common front-elevation base' for p in problems),
            'pedestal_sha256':base_hash,'generator':'built-in image_gen'}
    report['statues_touching_pedestal']=sum(r['statue_touches_pedestal'] for r in complete)
    write_json(OUT/'validation.json',report)
    if install:
        if len(complete)!=48 or problems:
            raise RuntimeError('Refusing to install incomplete or invalid relic set')
        target_dir=ROOT/'src/TerraKnight/Resources'
        for r in complete:
            target=target_dir/('relic_'+r['file'])
            backup=OUT/'backup-before-install/src/TerraKnight/Resources'/target.name
            backup.parent.mkdir(parents=True,exist_ok=True)
            if not backup.exists():
                shutil.copyfile(target,backup)
            shutil.copyfile(OUT/r['file'],target)
        report['installed_resources']=48
        report['installed_bytes_match']=all((target_dir/('relic_'+r['file'])).read_bytes()==(OUT/r['file']).read_bytes() for r in complete)
    else:
        target_dir=ROOT/'src/TerraKnight/Resources'
        matches=sum((target_dir/('relic_'+r['file'])).is_file() and (target_dir/('relic_'+r['file'])).read_bytes()==(OUT/r['file']).read_bytes() for r in complete)
        report['installed_resources']=matches
        report['installed_bytes_match']=len(complete)==48 and matches==48
    if package:
        PACKAGE.mkdir(exist_ok=True)
        files=[r['file'] for r in complete]+[r['hd_file'] for r in complete]
        files+=sorted(set(r['reference_file'] for r in complete))
        files+=['总览.png','index.html','README.md','manifest.json','prompts.json','validation.json','pedestal-64x16.png','pedestal-768x192.png']+comparison_files
        for name in ('visual-review.json','runtime-validation.json','sources.md','游戏内验证.png'):
            if (OUT/name).exists():
                files.append(name)
        def copy_metadata():
            write_json(OUT/'validation.json',report)
            for name in files:
                target=PACKAGE/name
                target.parent.mkdir(parents=True,exist_ok=True)
                shutil.copyfile(OUT/name,target)
        def build_zip():
            with zipfile.ZipFile(ARCHIVE,'w',zipfile.ZIP_DEFLATED) as z:
                for name in files:
                    z.write(PACKAGE/name,name)
        copy_metadata()
        build_zip()
        with zipfile.ZipFile(ARCHIVE) as z:
            report.update(zip_error=z.testzip(),packaged_png_bytes_match=all(z.read(r['file'])==(OUT/r['file']).read_bytes() for r in complete),archive_relic_count=len(complete))
        copy_metadata()
        build_zip()
        with zipfile.ZipFile(ARCHIVE) as z:
            assert z.testzip() is None
            assert all(z.read(name)==(PACKAGE/name).read_bytes() for name in z.namelist())
    write_json(OUT/'validation.json',report)
    print(json.dumps(report,ensure_ascii=True))


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--package',action='store_true')
    parser.add_argument('--install',action='store_true')
    args=parser.parse_args()
    main(args.package,args.install)
