"""Independent file/frame/alpha verification and standalone vanilla resource-pack packaging."""
from pathlib import Path
import json, hashlib, zipfile, shutil, subprocess
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from review_regional_native import render, review

ROOT=Path(__file__).resolve().parents[2]; OUT=ROOT/'output/regional-vanity-native'; REF=OUT/'references/vanilla'
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
 live=json.loads((OUT/'references/current-vanity-runtime.json').read_text(encoding='utf-8'))
 actual={x['id']:x for x in live['items']}
 checks=[]; paths=set(); replacements=[]
 combined=OUT/'HallownestRegionalVanity';(combined/'Content/Images').mkdir(parents=True,exist_ok=True)
 font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
 tiny=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',13)
 board=Image.new('RGB',(960,1740),(38,43,54)); draw=ImageDraw.Draw(board)
 draw.text((12,8),'24地区 · 原版结构地区配色 / 离线预览',font=font,fill='white')
 draw.text((12,34),'每套独立生成配色；最终透明轮廓、帧位和尺寸逐像素沿用原版。非游戏内截图。',font=tiny,fill=(190,197,211))
 for spec in manifest['sets']:
  index=spec['index'];folder=OUT/f"{index:02d}-{spec['key']}"
  validation=json.loads((folder/'validation.json').read_text(encoding='utf-8'))
  assert validation['static_native_checks_passed'] and len(validation['files'])==len(spec['entries'])
  garment_colors={}
  for entry in spec['entries']:
   frame_checks=0
   filename=f"{spec['key']}_{entry['kind']}.png"
   native=folder/'native'/filename; original=REF/(entry['source']+'.png')
   with Image.open(native) as ni,Image.open(original) as oi:
    assert ni.mode=='RGBA' and ni.size==oi.size,(index,filename,'mode/size')
    a=np.array(ni);b=np.array(oi.convert('RGBA'))
   assert np.array_equal(a[:,:,3],b[:,:,3]),(index,filename,'alpha')
   assert digest(original)==entry['sha256'],(index,filename,'source changed')
   visible=b[:,:,3]>0
   assert np.all(a[~visible,:3]==0),(index,filename,'hidden pixels')
   # All native cells retain original nonempty occupancy, including special
   # shorter last head frames, gender rows, shoulder/arm and glow cells.
   for y in range(0,a.shape[0],56):
    for x in range(0,a.shape[1],40):
     assert np.array_equal(a[y:y+56,x:x+40,3],b[y:y+56,x:x+40,3])
     frame_checks+=1
   colors=np.unique(a[:,:,:3][visible],axis=0)
   assert len(colors)>=2,(index,filename,'collapsed palette')
   color_set={tuple(int(v) for v in rgb) for rgb in colors}
   if entry['kind'] in ['Head','Body','Legs']:garment_colors[entry['kind']]=color_set
   if entry['kind'].endswith('Item'):assert color_set<=garment_colors[entry['kind'][:-4]],(index,filename,'inventory palette mismatch')
   replacement=entry['source']+'.png'
   assert replacement not in paths,('resource pack collision',replacement)
   paths.add(replacement)
   dest=combined/'Content/Images'/replacement;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(native,dest)
   assert digest(dest)==digest(native)
   checks.append(dict(region=spec['key'],kind=entry['kind'],size=[a.shape[1],a.shape[0]],source_alpha_exact=True,native_frame_cells_checked=frame_checks,sha256=digest(native)))
   if entry['kind'] in ['Head','Body','Legs']:
    field={'Head':'headSlot','Body':'bodySlot','Legs':'legSlot'}[entry['kind']]
    assert actual[entry['item_id']][field]==entry['slot']
    replacements.append(f"|{index:02d}|{spec['name']}|{entry['kind']}|{entry['item']}|{entry['item_id']}|{replacement}|{' × '.join(map(str,entry['size']))}|")
  review(index)
  x=(index-1)%4*240;y=(index-1)//4*280+60
  draw.rectangle((x+4,y+2,x+235,y+275),outline=(77,87,105))
  draw.text((x+12,y+8),f"{index:02d} {spec['name']}",font=font,fill='white')
  tile=render(spec,0).resize((160,224),Image.Resampling.NEAREST)
  board.paste(tile,(x+6,y+42),tile)
  for i,kind in enumerate(['HeadItem','BodyItem','LegsItem']):
   icon=Image.open(folder/'native'/f"{spec['key']}_{kind}.png").convert('RGBA')
   icon=icon.resize((icon.width*2,icon.height*2),Image.Resampling.NEAREST)
   board.paste(icon,(x+194-icon.width//2,y+60+i*70),icon)
 board.save(OUT/'all24-native-preview.png')
 pack=dict(Name='圣巢24地区时装 · 原版结构配色版',Author='TerraKnight',Description='替换24套已有时装的地区配色。原版像素轮廓、动画、男女体型及原时装效果保留。不增加物品ID。基于Terraria 1.4.5.8。',Version=dict(major=1,minor=0))
 (combined/'pack.json').write_text(json.dumps(pack,ensure_ascii=False,indent=2),encoding='utf-8')
 summary=dict(sets=24,png_files=len(checks),native_frame_cells_checked=sum(x['native_frame_cells_checked'] for x in checks),all_source_alpha_exact=True,all_png_dimensions_exact=True,item_slots_checked_live=True,source_game_version='1.4.5.8',runtime_equipment_verified=False,new_silhouettes=False,files=checks)
 (OUT/'validation-summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
 (OUT/'原版替换对照.md').write_text('# 原版替换对照\n\n这些贴图替换已有原版时装，不增加新的物品。物品编号已从本机Terraria 1.4.5.8重新导出核对。\n\n|序号|地区|部位|原版物品名|物品ID|资源包路径 Content/Images/ 下|原尺寸|\n|---|---|---|---|---|---|---|\n'+'\n'.join(replacements)+'\n',encoding='utf-8')
 readme='''# 圣巢24地区时装：原版结构地区配色版

## 本次交付的准确范围

24套逐套调用内置 image_gen 生成配色提案；最终为保留原版既有服装剪影、所有动画帧、复合手臂结构和透明轮廓的地区配色变体。不是前一轮设计板中新款服装剪影的完整原创动画重绘，也不是新增24套游戏物品。生成图的背景和几何不作为最终贴图使用；颜色经部件配准回填到原版索引色后，每个原版颜色在所有帧采用同一个目标色，避免生成帧闪烁。

基于本机 Terraria **1.4.5.8** 导出原始PNG和装备编号。所有最终PNG都是原尺寸RGBA，包含头部、身体、腿部、三个背包图标，以及需要的自动披风层。原版时装固有的露发、遮脸、披风、光效、坐姿等行为仍由对应原版物品决定；本资源包不改变这些行为。

## 使用完整原版资源包

将 `HallownestRegionalVanity-resource-pack.zip` 放进 Terraria 的资源包目录，然后在游戏的资源包菜单启用。也可以将 `HallownestRegionalVanity` 文件夹放入该目录。它会替换对照表中的现有时装，其他物品不变。更高优先级资源包如果也替换相同路径，会覆盖本包。

本次没有写入你的正式游戏目录、启用资源包或修改角色/世界存档。

## 使用单套或接入项目

每个 `序号-地区` 目录：

- `native/`：真正用于装载的独立原尺寸PNG，地区名命名。
- `resource-pack/`：单套原版替换资源包，已经按原版路径命名。
- `source.json`：原版物品ID、装备槽、原图尺寸和哈希。
- `validation.json`：独立的尺寸、透明轮廓和帧配色一致性检查。
- `all-poses-comparison.png`、`animation-review.gif`：离线拼层预览；不是游戏内截图。
- `generation-prompt.txt`、`generated-original.png`：实际提示词和保留的生成原稿。生成原稿不是可直接装载的贴图。

如果将这些文件作为模组内新增物品，需要在模组代码里注册新的装备槽，并继承对应原版时装的遮发、手臂、披风等绘制规则。仅重命名图片不会自动增加物品。本次提供的原版资源包采用已有物品和已有绘制规则，因此无需新增物品代码。

## 验证

已完成：从当前游戏重新导出物品槽位；逐张与原版核对尺寸和每个alpha字节；检查所有原版帧单元；验证资源包无路径冲突；重开所有PNG；输出每套20种动作、男女体型、左右朝向的离线预览。

另已调用本机 Terraria 的实际 ResourcePack / GetContentSource 接口，读取每个单套资源包，以及完整文件夹和ZIP资源包中的所有PNG流，逐一比较SHA256。对应 `game-loader-check*.json` 是原版资源包读取证据，不是GPU纹理解码或实际装备截图。

尚未完成：在运行中的 Terraria 或 TerraKnight 内实际启用本包、逐套试穿和观察染料/配饰叠加。离线预览不完整模拟原版所有发型、绘制标志、披风和粒子光效，不能当作这些路径的运行证据。

根目录 `原版替换对照.md` 与 `validation-summary.json` 提供完整清单和验证边界。`all24-native-preview.png` 只供查看，不能当作装备图集使用。
'''
 (OUT/'使用说明.md').write_text(readme,encoding='utf-8')
 with zipfile.ZipFile(OUT/'HallownestRegionalVanity-resource-pack.zip','w',zipfile.ZIP_DEFLATED) as z:
  for p in sorted(combined.rglob('*')):
   if p.is_file():z.write(p,p.relative_to(combined))
 loader=OUT/'references/CheckResourcePack.exe'
 for source,report,expected in [(OUT/f"{s['index']:02d}-{s['key']}"/'resource-pack',OUT/f"{s['index']:02d}-{s['key']}"/'game-loader-check.json',{digest(OUT/f"{s['index']:02d}-{s['key']}"/'native'/f"{s['key']}_{e['kind']}.png") for e in s['entries']}) for s in manifest['sets']]+[(combined,OUT/'game-loader-check-folder.json',{x['sha256'] for x in checks}),(OUT/'HallownestRegionalVanity-resource-pack.zip',OUT/'game-loader-check-zip.json',{x['sha256'] for x in checks})]:
  run=subprocess.run([str(loader),'F:/SteamLibrary/steamapps/common/Terraria/Terraria.exe',str(source),str(report)],capture_output=True,text=True,encoding='utf-8',errors='replace')
  if run.returncode:raise RuntimeError(run.stdout+run.stderr)
  loaded=json.loads(report.read_text(encoding='utf-8-sig'))
  assert {x['sha256'] for x in loaded['assets']}==expected,('game content stream mismatch',source)
  print(run.stdout.strip(),flush=True)
 summary['game_resource_pack_content_loader_verified']=True
 summary['single_packs_loaded']=24
 summary['combined_folder_and_zip_loaded']=True
 (OUT/'validation-summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
 with zipfile.ZipFile(OUT/'圣巢24地区时装-原尺寸贴图与验证.zip','w',zipfile.ZIP_DEFLATED) as z:
  for p in sorted(OUT.rglob('*')):
   if not p.is_file() or p.suffix.lower()=='.zip' or 'references' in p.relative_to(OUT).parts or combined in p.parents:continue
   z.write(p,p.relative_to(OUT))
 print(json.dumps({k:v for k,v in summary.items() if k!='files'},ensure_ascii=False))

if __name__=='__main__':main()
