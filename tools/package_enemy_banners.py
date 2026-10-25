"""Export generated enemy-banner artwork to native pixel assets and an auditable gallery."""
import argparse
import collections
import hashlib
import html
import json
import math
import shutil
import zipfile
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from rename_enemy_banners import write_mapping

ROOT=Path(__file__).resolve().parents[1]
WORK=ROOT/'artifacts/hk-banners'
PACKAGE=ROOT/'空洞骑士怪物旗帜素材包'


def main(final=False):
    rows=json.loads((WORK/'manifest.json').read_text(encoding='utf-8'))
    for folder in ['旗帜_16x48','预览_8倍','绘制原图','怪物参考']:
        (PACKAGE/folder).mkdir(parents=True,exist_ok=True)
    complete=[]
    for row in rows:
        record=WORK/'results'/f"{row['id'].replace('@','-')}.json"
        if not record.is_file():
            continue
        result=json.loads(record.read_text(encoding='utf-8'))
        filename=row['id']+'.png'
        original=PACKAGE/'绘制原图'/filename
        shutil.copyfile(result['source'],original)
        shutil.copyfile(WORK/row['reference'],PACKAGE/'怪物参考'/filename)
        with Image.open(original) as image:
            image=image.convert('RGBA')
            alpha=image.getchannel('A')
            assert alpha.getextrema()[0]==0, f"No transparent background: {row['id']}"
            bbox=alpha.point(lambda x:255 if x>=128 else 0).getbbox()
            assert bbox, f"Blank sprite: {row['id']}"
            cropped=image.crop(bbox)
            factor=min(16/cropped.width,48/cropped.height)
            size=(max(1,round(cropped.width*factor)),max(1,round(cropped.height*factor)))
            scaled=cropped.resize(size,Image.Resampling.NEAREST)
            # Export conversion only: fit to the game's grid, retain sharp pixels and binary alpha.
            native=Image.new('RGBA',(16,48))
            native.paste(scaled,((16-size[0])//2,(48-size[1])//2))
            native.putalpha(native.getchannel('A').point(lambda x:255 if x>=128 else 0))
            opaque=native.getchannel('A')
            native=native.convert('RGB').quantize(colors=20,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE).convert('RGBA')
            native.putalpha(opaque)
            pixels=list(native.getdata())
            native.putdata([pixel if pixel[3] else (0,0,0,0) for pixel in pixels])
            assert native.getbbox() and len(native.getcolors(1024))<=21
            native.save(PACKAGE/'旗帜_16x48'/filename)
            native.resize((128,384),Image.Resampling.NEAREST).save(PACKAGE/'预览_8倍'/filename)
        row.update(status='generated',file='旗帜_16x48/'+filename,preview='预览_8倍/'+filename,
                   original='绘制原图/'+filename,reference_file='怪物参考/'+filename,
                   width=16,height=48,source_bbox=list(bbox),native_bbox=list(native.getbbox()),
                   sha256=hashlib.sha256((PACKAGE/'旗帜_16x48'/filename).read_bytes()).hexdigest(),
                   prompt=result['prompt'],method='built-in image_gen; native-grid export')
        complete.append(row)
    assert len({r['sha256'] for r in complete})==len(complete),'Duplicate native banners require review'
    if final:
        assert len(complete)==len(rows), f"Incomplete: {len(complete)}/{len(rows)}"
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',14)
    small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',11)
    columns,cw,ch=8,160,256
    overview=Image.new('RGB',(columns*cw,max(1,math.ceil(len(complete)/columns))*ch+72),'#202a38')
    draw=ImageDraw.Draw(overview)
    draw.text((20,12),'空洞骑士 × 泰拉瑞亚 · 怪物旗帜',font=font,fill='#f8edd7')
    draw.text((20,38),f"已绘制 {len(complete)} / {len(rows)} 条记录 · 原生 16×48 · 以下按整数倍展示",font=font,fill='#a8bdcb')
    for i,row in enumerate(complete):
        x=i%columns*cw;y=i//columns*ch+72
        image=Image.open(PACKAGE/row['file']).resize((64,192),Image.Resampling.NEAREST)
        overview.paste(image,(x+48,y+3),image)
        draw.text((x+7,y+202),f"{row['index']:03} {row['name']}",font=font,fill='#f7ecd7')
        draw.text((x+7,y+226),row['id'],font=small,fill='#a7bac9')
    overview.save(PACKAGE/'总览.png')
    compare=[]
    for page in range(math.ceil(len(complete)/16)):
        panel=Image.new('RGB',(1200,1040),'#25313e');d=ImageDraw.Draw(panel)
        for i,row in enumerate(complete[page*16:(page+1)*16]):
            x=i%4*300;y=i//4*260
            source=Image.open(PACKAGE/row['reference_file']).convert('RGBA');panel.paste(source,(x+8,y+40),source)
            sprite=Image.open(PACKAGE/row['file']).resize((64,192),Image.Resampling.NEAREST);panel.paste(sprite,(x+185,y+8),sprite)
            d.text((x+8,y+211),f"{row['index']:03} {row['name']}",font=font,fill='#fff0d9')
            d.text((x+8,y+235),row['id'],font=small,fill='#a7bac9')
        filename=f'对照_{page+1:02}.png';panel.save(PACKAGE/filename);compare.append(filename)
    (PACKAGE/'manifest.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
    write_mapping(PACKAGE, rows)
    prompts=[{'id':r['id'],'prompt':r['prompt']} for r in complete]
    (PACKAGE/'prompts.json').write_text(json.dumps(prompts,ensure_ascii=False,indent=2),encoding='utf-8')
    cards=''.join(f'<article><h2>{r["index"]:03} {html.escape(r["name"])}</h2><div class="pictures"><img class="monster" src="{html.escape(r["reference_file"],quote=True)}" alt="原怪物"><a href="{html.escape(r["original"],quote=True)}"><img class="flag" src="{html.escape(r["file"],quote=True)}" alt="旗帜"></a></div><p>{html.escape(r["id"])} · {r["category"]}</p><a href="{html.escape(r["file"],quote=True)}" download>下载 16×48 PNG</a></article>' for r in complete)
    (PACKAGE/'预览.html').write_text(f'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>空洞怪物旗帜</title><style>body{{background:#202a38;color:#eee7d8;font:14px system-ui;margin:24px}}main{{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:16px}}article{{padding:18px;background:#2b3948;border:1px solid #496072}}h2{{font-size:17px}}.pictures{{display:flex;align-items:center;justify-content:space-around;height:210px}}.monster{{width:128px;height:128px;object-fit:contain}}.flag{{width:64px;height:192px;image-rendering:pixelated}}a{{color:#9ed7c9}}p{{color:#adbfc9}}</style><h1>空洞骑士怪物旗帜</h1><p>{len(complete)} / {len(rows)} 条记录 · 左侧原怪物，右侧原生像素旗帜。点击旗帜查看绘制原图。</p><main>{cards}</main></html>''',encoding='utf-8')
    summary={'expected':len(rows),'generated':len(complete),'pending':[r['id'] for r in rows if r['status']!='generated'],
             'dimensions':[16,48],'categories':dict(collections.Counter(r['category'] for r in complete)),
             'native_unique_hashes':len({r['sha256'] for r in complete}),'comparison_sheets':compare,'method':'built-in image_gen',
             'integrated_into_game':False}
    (PACKAGE/'validation.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    (PACKAGE/'README.md').write_text(f'''# 空洞骑士怪物旗帜素材包

当前完成 {len(complete)} / {len(rows)} 条图鉴记录。范围为本项目普通怪、精英，以及 Kind=miniboss 的三种小首领；不含正式 Boss。无敌环境生物、被动生物、召唤幼体同样保留。Husk Miner 的不同进度记录按内部 ID 分列，名称相同不表示漏图。

`旗帜_16x48/` 为可用作游戏贴图的逐怪透明 PNG，固定 16×48，使用二值透明度和最多 20 个不透明颜色。`预览_8倍/` 为 128×384 最近邻放大版。`绘制原图/` 保留 imagegen 逐张绘制的高分辨率原稿；原生版通过裁边、等比适配和像素规格导出获得。原稿没有被覆盖。

参照本机 Terraria 的 Tiles_91 旗帜贴图和空洞骑士猎人日志肖像绘制。`总览.png` 按编号展示旗帜，`对照_*.png` 与 `预览.html` 可对照怪物。`manifest.json` 含内部 ID、中文名、分类、文件及 SHA-256；`prompts.json` 保留逐张生成提示词。

本包交付美术素材，不包含掉落、放置或附近增益逻辑。采用内置 image_gen 工具逐张生成，没有调用外部图像 API。
''',encoding='utf-8')
    if final:
        archive=ROOT/'artifacts/空洞骑士怪物旗帜素材包.zip'
        with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as bundle:
            for p in sorted(PACKAGE.rglob('*')):
                if p.is_file():bundle.write(p,p.relative_to(PACKAGE.parent))
        with zipfile.ZipFile(archive) as bundle:
            assert bundle.testzip() is None
            assert len([n for n in bundle.namelist() if '/旗帜_16x48/' in n and n.endswith('.png')])==len(rows)
        summary['zip']=str(archive)
    print(json.dumps(summary,ensure_ascii=False,indent=2))


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--final',action='store_true');args=parser.parse_args();main(args.final)
