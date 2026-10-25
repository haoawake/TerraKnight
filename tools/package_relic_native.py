"""Package separately generated artwork at verified Terraria-native relic dimensions."""
from pathlib import Path
import collections, hashlib, html, json, shutil, zipfile
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[1]
WORK=ROOT/'artifacts/relic-remake-native'
PACKAGE=ROOT/'空洞骑士大师圣物素材包-原版规格重制候选-20261019'

def digest(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def native_statue(source,palette,coarse=False):
    raw=Image.open(source).convert('RGBA')
    assert raw.getchannel('A').getextrema()[0]==0,('No real transparency',source)
    box=raw.getchannel('A').point(lambda a:255 if a>=64 else 0).getbbox()
    assert box,source
    size,border=(25,1) if coarse else (50,2)
    art=raw.crop(box);art.thumbnail((size-2*border,size-2*border),Image.Resampling.LANCZOS)
    pal=Image.new('P',(1,1));rgb=[v for c in palette for v in c]
    pal.putpalette(rgb+list(palette[-1])*(256-len(palette)))
    alpha=art.getchannel('A').point(lambda a:255 if a>=96 else 0)
    art=art.convert('RGB').quantize(palette=pal,dither=Image.Dither.NONE).convert('RGBA');art.putalpha(alpha)
    logical=Image.new('RGBA',(size,size));logical.alpha_composite(art,((size-art.width)//2,(size-art.height)//2))
    return logical.resize((50,50),Image.Resampling.NEAREST)

def main():
    rows=json.loads((WORK/'selections.json').read_text(encoding='utf-8'))
    PACKAGE.mkdir(exist_ok=True)
    for name in ['statues','placed-preview','references','previous','comparisons','native-reference','generation-metadata']:
        (PACKAGE/name).mkdir(exist_ok=True)
    for name in ['statues','placed-preview']:(PACKAGE/'classic-2px'/name).mkdir(parents=True,exist_ok=True)
    base=Image.open(WORK/'reference/native-pedestal-48x64.png').convert('RGBA')
    native_art=Image.open(WORK/'reference/Extra_198.png').convert('RGBA')
    counts=collections.Counter(native_art.getdata())
    palette=sorted([rgb[:3] for rgb,count in counts.items() if rgb[3]==255 and count>=100],key=lambda c:sum(c))
    assert len(palette)>=8 and base.size==(48,64)
    for name in ['Tiles_617.png','Extra_198.png','native-pedestal-48x64.png']:
        shutil.copy2(WORK/'reference'/name,PACKAGE/'native-reference'/name)
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
    manifest=[];errors=[];cards=[];complete=[]
    for row in rows:
        slug=row['slug'];filename=row['texture']+'.png';raw=WORK/'generated'/f'{slug}.png'
        ref=PACKAGE/'references'/filename;shutil.copy2(row['reference'],ref)
        old=PACKAGE/'previous'/filename;shutil.copy2(ROOT/'src/TerraKnight/Resources'/filename,old)
        row=dict(row,reference_file='references/'+filename,previous_file='previous/'+filename,status='pending')
        meta=WORK/'generation-metadata'/f'{slug}.json'
        if meta.exists():shutil.copy2(meta,PACKAGE/'generation-metadata'/meta.name)
        if not raw.exists():manifest.append(row);continue
        statue=native_statue(raw,palette);statue_path=PACKAGE/'statues'/filename;statue.save(statue_path)
        coarse=native_statue(raw,palette,coarse=True);coarse.save(PACKAGE/'classic-2px/statues'/filename)
        # Vanilla: center (24,24), 50x50 sculpture. DrawYOffset=2 for the 48x64 tile pedestal.
        # One horizontal pixel margin and one top margin avoid cropping the separate native art.
        placed=Image.new('RGBA',(50,68));placed.alpha_composite(statue,(0,0));placed.alpha_composite(base,(1,3))
        placed_path=PACKAGE/'placed-preview'/filename;placed.save(placed_path)
        coarse_placed=Image.new('RGBA',(50,68));coarse_placed.alpha_composite(coarse,(0,0));coarse_placed.alpha_composite(base,(1,3))
        coarse_placed.save(PACKAGE/'classic-2px/placed-preview'/filename)
        arr=np.array(statue);pixels=set(map(tuple,arr[arr[:,:,3]>0,:3]));a=set(np.unique(arr[:,:,3]))
        if pixels-set(palette):errors.append([slug,'non-native palette'])
        if a!={0,255}:errors.append([slug,'alpha must be binary'])
        if statue.size!=(50,50) or placed.size!=(50,68):errors.append([slug,'wrong dimensions'])
        ca=np.array(coarse)
        if not np.array_equal(ca[::2,::2],ca[1::2,1::2]) or not np.array_equal(ca[::2,::2],ca[::2,1::2]):errors.append([slug,'classic alternate not 2x2 pixel clusters'])
        p=np.array(placed)[3:67,1:49];b=np.array(base);mask=b[:,:,3]>0
        if not np.array_equal(p[mask],b[mask]):errors.append([slug,'native pedestal differs'])
        panel=Image.new('RGB',(1000,430),'#25303b');draw=ImageDraw.Draw(panel)
        for x,title in [(0,'选定原版帧'),(330,'现有圣物原图'),(660,'新候选 · 原版规格')]:draw.text((x+12,12),title,font=font,fill='#ffe99b')
        source=Image.open(ref);source.thumbnail((300,300),Image.Resampling.LANCZOS);panel.paste(source,(12+(300-source.width)//2,55+(300-source.height)//2),source)
        previous=Image.open(old).convert('RGBA');previous.thumbnail((280,300),Image.Resampling.NEAREST);panel.paste(previous,(340+(300-previous.width)//2,55+(300-previous.height)//2),previous)
        large=placed.resize((200,272),Image.Resampling.NEAREST);panel.paste(large,(720,65),large)
        draw.text((12,380),row['name']+' | '+row['selection']['clip']+' / frame '+str(row['selection']['frame']),font=font,fill='white')
        panel.save(PACKAGE/'comparisons'/filename)
        row.update(status='generated',statue_file='statues/'+filename,placed_file='placed-preview/'+filename,
                   comparison_file='comparisons/'+filename,native_statue_size=[50,50],placed_preview_size=[50,68],
                   footprint_tiles=[3,4],footprint_pixels=[48,64],palette_colors=len(palette),
                   raw_generated=str(raw),statue_sha256=digest(statue_path),placed_sha256=digest(placed_path))
        manifest.append(row);complete.append(row)
        esc=html.escape
        cards.append(f'<article data-name="{esc(row["name"]+" "+slug)}"><h2>{esc(row["name"])}</h2><small>{slug} · {esc(row["selection"]["clip"])} / frame {row["selection"]["frame"]}</small><div class="images"><figure><img src="{row["reference_file"]}"><figcaption>选定原版帧</figcaption></figure><figure><img class="pixel" src="{row["previous_file"]}"><figcaption>现有圣物原图</figcaption></figure><figure><img class="pixel" src="{row["placed_file"]}"><figcaption>新候选·细节版</figcaption></figure><figure><img class="pixel" src="classic-2px/{row["placed_file"]}"><figcaption>同尺寸·粗像素版</figcaption></figure></div><p class="native">原生尺寸：<img src="{row["placed_file"]}"> <img src="classic-2px/{row["placed_file"]}"></p><p>{esc(row["reason"])}</p><a href="{row["comparison_file"]}">打开对比图</a> · <a href="{row["statue_file"]}">50×50 雕像</a></article>')
    sheet=Image.new('RGB',(1200,max(1,(len(complete)+5)//6)*340),'#25303b');draw=ImageDraw.Draw(sheet)
    for i,row in enumerate(complete):
        im=Image.open(PACKAGE/row['placed_file']).resize((200,272),Image.Resampling.NEAREST);x=i%6*200;y=i//6*340
        sheet.paste(im,(x,y),im);draw.text((x+4,y+280),row['name'],font=font,fill='#ffe99b');draw.text((x+4,y+310),row['slug'],font=font,fill='white')
    sheet.save(PACKAGE/'总览.png')
    spec=dict(version='native-terraria-20261019',count_required=48,native_sculpture_frame=[50,50],detail_grid=[50,50],classic_logical_grid=[25,25],classic_pixel_cluster=[2,2],
              footprint_tiles=[3,4],tile_pixels=16,footprint_pixels=[48,64],pedestal_source='Tiles_617.png',
              pedestal_extraction='First 54x72 style, 3 columns x 4 rows, each 16x16 tile with 2-pixel atlas padding removed',
              sculpture_source='Extra_198.png: 50x1400, 28 independent 50x50 frames',
              pedestal_draw_y_offset=2,sculpture_center_in_footprint=[24,24],placed_preview_canvas=[50,68],
              placed_preview_note='50x68 is a padded render preview, not the 3x4-tile collision footprint or a replacement statue-frame size.',
              background='RGBA transparent, alpha 0 or 255',palette=palette,pedestal_rgba_sha256=hashlib.sha256(base.tobytes()).hexdigest(),
              invariants=['Existing names and resource basenames retained','Existing game assets remain untouched','Separate original pedestal pixels reused verbatim','No smooth resizing in delivered native sprite','No new boss anatomy or weapons requested'])
    (PACKAGE/'specification.json').write_text(json.dumps(spec,ensure_ascii=False,indent=2),encoding='utf-8')
    (PACKAGE/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    validation=dict(required=48,generated=len(complete),pending=[r['slug'] for r in manifest if r['status']!='generated'],errors=errors,
                    technical_status='passed' if len(complete)==48 and not errors else 'partial',native_pedestal_pixel_match=not any('pedestal' in str(x) for x in errors),
                    limitations=['No integration or in-game visual test: this is a separate comparison candidate pack.','Generated art is visually reviewed against the selected frames; it is a stylized material conversion, not a claim of identical pixels to the original boss.'])
    (PACKAGE/'validation.json').write_text(json.dumps(validation,ensure_ascii=False,indent=2),encoding='utf-8')
    head='''<!doctype html><meta charset="utf-8"><title>圣物重制候选对比</title><meta name="viewport" content="width=device-width,initial-scale=1"><style>body{background:#15202b;color:#e6edf3;font:16px system-ui;margin:24px}h1{color:#ffe99b}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,620px),1fr));gap:20px}article{border:1px solid #46515e;border-radius:12px;padding:18px;background:#25303b}h2{font-size:21px;margin:0 0 8px}small{color:#aebfce}.images{display:flex;gap:8px;align-items:center}figure{width:33%;margin:16px 0;text-align:center}figure img{max-width:100%;height:220px;object-fit:contain}.pixel{image-rendering:pixelated}.native img{width:50px;height:68px;vertical-align:middle}.native{min-height:75px}p{line-height:1.5}a{color:#ffe99b}input{padding:12px;width:min(90%,520px);margin:12px 0 24px}figcaption{color:#c8d2dd}</style>'''
    page=head+f'<h1>圣物重制候选 · {len(complete)} / 48</h1><p>名称保持现有配置。每件展示：选定原版帧、旧圣物、新候选。底座直接复用泰拉瑞亚原版像素；独立雕像 50×50，占地 3×4 格。50×68 是含边界与底座偏移的摆放预览。</p><p>本包供自行对比选择，尚未替换游戏资源。旧图按自身画布展示，放大对比不代表游戏中的实际渲染尺寸。</p><input id="q" placeholder="搜索圣物名称或 ID"><main>'+''.join(cards)+'</main><script>q.oninput=()=>document.querySelectorAll("article").forEach(e=>e.hidden=!e.dataset.name.toLowerCase().includes(q.value.toLowerCase()))</script>'
    (PACKAGE/'index.html').write_text(page,encoding='utf-8')
    readme=f'''# 原版规格圣物重制候选

已生成 {len(complete)} / 48。打开 index.html 对比；名称与现有圣物配置逐字一致。

- statues/：独立雕像，50×50 RGBA，与原版 Extra_198 帧尺寸相同；优先保留小尺寸面部和武器细节。
- classic-2px/：同样 50×50 的粗像素替代版，25×25 逻辑像素放大两倍；两版底座和占地完全一致。
- native-reference/：实际 Terraria Tiles_617、Extra_198 和去除图集间隔后的 48×64 底座。所有候选的底座像素相同。
- placed-preview/：50×68 静态摆放预览，含原版 DrawYOffset=2 与边缘留白。占地仍是原版 3×4 格（48×64），预览画布不是占地。
- references/：选定的真实 Boss 动画帧。manifest.json 记录动画名、帧号、Sprite ID、图集、多人组合和武器来源。
- previous/：当前游戏资源中的旧图，原样复制；comparisons/ 与总览.png 供对比。
- specification.json：统一尺寸、颜色、底座和布局规格；validation.json：技术验收；generation-metadata/：逐件提示词与生成来源。

使用内置 image_gen 逐件按原版帧生成金质候选，再按原版像素规格缩制、限定原版金色色板并装配实际底座。原生图使用最近邻像素块；不要用双线性插值放大。

本包保留已有名称和资源文件名，不覆盖原图、不安装进游戏。它是供选择的候选包；尚未进行游戏内显示验收。若选择采用，需分别接入独立雕像与原版底座，不能直接将预览图覆盖当前 64×96 合并贴图。
'''
    (PACKAGE/'README.md').write_text(readme,encoding='utf-8')
    if len(complete)==48 and not errors:
        archive=ROOT/'artifacts'/f'{PACKAGE.name}.zip'
        with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
            for p in PACKAGE.rglob('*'):
                if p.is_file():z.write(p,p.relative_to(PACKAGE.parent))
        (WORK/'package-result.json').write_text(json.dumps(dict(path=str(archive),sha256=digest(archive),generated=48),ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(validation,ensure_ascii=False))

if __name__=='__main__':main()
