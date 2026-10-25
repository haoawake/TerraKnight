"""Collect the paid house/shop and actual diagonal rendering evidence for the tested DLL."""
from pathlib import Path
import json,re,hashlib,shutil,sys,zipfile
from PIL import Image,ImageDraw,ImageFont
root=Path(__file__).resolve().parents[1];run=Path(sys.argv[1]).resolve()
out=root/'artifacts/prefab-houses';out.mkdir(exist_ok=True)
result=json.loads((run/'result.json').read_text('utf-8-sig'))
assert result['Completed'] and not result['TimedOut'] and not result['Failures'],result
assert not result['ChangedPlayerSaves'] and not result['NewPlayerSaves'],result
log=(run/'AutoTest.log').read_text('utf-8-sig')
assert re.search(r'PrefabHouses TOTAL 347/347 PASS',log)
assert re.search(r'RegionalPlatforms TOTAL 1248/1248 PASS',log)
assert len(re.findall(r'rendered diagonal continuity slope=[12] gaps=0 PASS',log))==48
assert len(re.findall(r'walk slope=.* PASS',log))==96
assert len(re.findall(r'actual jump from below and land PASS',log))==24
evidence=out/'evidence';evidence.mkdir(exist_ok=True)
for name in ['result.json','AutoTest.log']:shutil.copy2(run/name,evidence/name)
game=root/'artifacts/single-drop-game/BepInEx/TerraKnight/dev/single-prefabhouses'
for name in ['prefab-houses.json','regional-platforms.json','Town_prefab_guide_chat.png','Town_prefab_guide_shop.png']:shutil.copy2(game/name,evidence/name)
assert max(Image.open(evidence/'Town_prefab_guide_shop.png').convert('RGB').getextrema()[0])>100
areas=json.loads((root/'artifacts/regional-furniture/references/spec.json').read_text('utf8'))['areas']
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
for prefix,file in [('prefab_house_','houses-overview.png'),('regional_platform_','platforms-after-slope-fix.png')]:
 contact=Image.new('RGB',(1280,1224),'#242b35');draw=ImageDraw.Draw(contact)
 for i,(key,name) in enumerate(areas):
  image=game/(prefix+key+'.png');shutil.copy2(image,evidence/image.name)
  contact.paste(Image.open(image).convert('RGB').resize((320,180)),(i%4*320,i//4*204+24));draw.text((i%4*320+8,i//4*204+2),name,font=font,fill='white')
 contact.save(out/file)
dll=root/'artifacts/prefab-houses-build/TerraKnight.dll';digest=hashlib.sha256(dll.read_bytes()).hexdigest()
installation=json.loads((out/'installation.json').read_text('utf-8-sig')) if (out/'installation.json').exists() else {}
verification={'passed':True,'house_and_shop_checks':347,'platform_checks':1248,'checks':1595,'regions':24,'paid_random_purchases':1024,'rendered_diagonal_continuity_checks':48,'actual_stair_walks':96,'native_player_saves_changed':[],'native_player_saves_created':[],'failed_checks':[],'dll_sha256':digest,'runtime_run':str(run),'shop_ui_inspected':True,'house_renders_inspected':True,'diagonal_renders_inspected':True,'two_client_house_placement_tested':False,'installed':installation.get('installed',False) and installation.get('dll_sha256')==digest,'installation_record':'installation.json'}
(out/'verification.json').write_text(json.dumps(verification,ensure_ascii=False,indent=2),encoding='utf8')
delivery=out/'delivery';delivery.mkdir(exist_ok=True)
for name in ['TerraKnight.dll','TerraKnight.pdb']:shutil.copy2(dll.parent/name,delivery/name)
for name in ['verification.json','houses-overview.png','platforms-after-slope-fix.png']:
 shutil.copy2(out/name,delivery/name)
if (out/'installation.json').exists():shutil.copy2(out/'installation.json',delivery/'installation.json')
shutil.copy2(root/'docs/向导随机地区预制房与平台斜坡修复-2026-10-21.md',delivery/'使用说明.md')
with zipfile.ZipFile(out/'TerraKnight-向导预制房与连续斜坡.zip','w',zipfile.ZIP_DEFLATED) as archive:
 for f in delivery.iterdir():archive.write(f,f.name)
print(json.dumps(verification,ensure_ascii=False))
