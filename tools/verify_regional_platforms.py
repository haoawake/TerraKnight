"""Validate native platform frames and render an inspection contact sheet (not a game asset)."""
from pathlib import Path
import json,hashlib
from PIL import Image,ImageDraw,ImageFont
root=Path(__file__).resolve().parents[1]
out=root/'artifacts/regional-platforms'
manifest=json.loads((out/'assets.json').read_text('utf8'))
designs=json.loads((out/'generated.json').read_text('utf8'))
reference=Image.open(root/'空洞骑士地区家具素材包_v2_差异造型/原版参考/完整原版图集/Tiles_19.png').convert('RGBA')
assert len(manifest['assets'])==48 and len(designs)==24
hashes=[];silhouettes=[]
contact=Image.new('RGB',(1510,1248),'#242b35');draw=ImageDraw.Draw(contact)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',14)
for index,d in enumerate(designs):
 item=Image.open(out/'native'/f"HkFurniture_{d['key']}_Platform.png").convert('RGBA')
 sheet=Image.open(out/'native'/f"HkFurniturePlatform_{d['key']}.png").convert('RGBA')
 assert item.size==(16,16) and sheet.size==(486,18)
 assert item.getchannel('A').crop((0,0,16,2)).getextrema()==(255,255)
 assert item.getchannel('A').crop((0,8,16,16)).getbbox() is None
 hashes.append(hashlib.sha256(sheet.tobytes()).hexdigest());silhouettes.append(hashlib.sha256(item.getchannel('A').tobytes()).hexdigest())
 assert set(item.getchannel('A').getdata())<={0,255}
 assert set(sheet.getchannel('A').getdata())<={0,255}
 draw.text((4,index*52+14),d['name'],font=font,fill='white')
 for f in range(27):
  frame=sheet.crop((f*18,0,f*18+16,16))
  assert frame.getbbox() is not None,(d['key'],f)
  if f>=8: assert frame.getchannel('A').tobytes()==reference.crop((f*18,0,f*18+16,16)).getchannel('A').tobytes(),(d['key'],f)
  assert sheet.getchannel('A').crop((f*18+16,0,f*18+18,18)).getbbox() is None
  contact.paste(frame.resize((48,48),Image.Resampling.NEAREST),(200+f*48,index*52),frame.resize((48,48),Image.Resampling.NEAREST))
assert len(set(hashes))==24
for a in manifest['assets']:
 assert hashlib.sha256((root/a['file']).read_bytes()).hexdigest()==a['sha256'],a['file']
contact.save(out/'all-connection-frames.png')
result={'passed':True,'regions':24,'native_items':24,'connection_sheets':24,'frames':648,'unique_sheets':len(set(hashes)),'unique_item_silhouettes':len(set(silhouettes)),'native_stair_corner_brace_masks_preserved':True,'binary_alpha':True,'frame_padding_transparent':True,'runtime_verified':False}
(out/'static-validation.json').write_text(json.dumps(result,indent=2),encoding='utf8')
print(json.dumps(result))
