"""Validate production images, topology, and append-only serialized IDs."""
from pathlib import Path
import json,re,subprocess,hashlib
from PIL import Image
root=Path(__file__).resolve().parents[1]
bundle=root/'空洞骑士地区家具素材包_v2_差异造型'
data=json.loads((root/'artifacts/regional-furniture-integration/assets.json').read_text('utf8'))
rows=json.loads((bundle/'单图清单.json').read_text('utf8'))
assert len(data['assets'])==648 and len(rows)==504
def enum(source,name):
 block=re.search(r'enum '+name+r'\b[^\{]*\{(.*?)\n    \}',source,re.S).group(1)
 block=re.sub(r'//[^\n]*|/\*.*?\*/','',block,flags=re.S)
 value=-1;result={}
 for part in block.split(','):
  part=part.strip()
  if not part:continue
  pair=part.split('=');key=pair[0].strip()
  value=int(pair[1].strip(),0) if len(pair)==2 else value+1
  result[key]=value
 return result
ids={}
for f,name in [('src/TerraKnight/Items/Items.cs','ItemType'),('src/TerraKnight/World/Stations.cs','StationKind')]:
 old=enum(subprocess.check_output(['git','show','HEAD:'+f],cwd=root).decode('utf8'),name)
 new=enum((root/f).read_text('utf8'),name)
 assert all(new[k]==v for k,v in old.items()),name+' existing IDs shifted'
 ids[name]={'previous_head_members':len(old),'current_members':len(new),'existing_values_unchanged':True}
original=Image.open(bundle/'原版参考/完整原版图集/Tiles_19.png').convert('RGBA')
platform_manifest=root/'artifacts/regional-platforms/assets.json'
platform_overrides={a['file']:a for a in json.loads(platform_manifest.read_text('utf8'))['assets']} if platform_manifest.exists() else {}
native=flames=platforms=0
for a in data['assets']:
 file=root/a['file'];raw=file.read_bytes();assert hashlib.sha256(raw).hexdigest()==a['sha256']
 image=Image.open(file).convert('RGBA');assert list(image.size)==a['size']
 assert all(c[3] in (0,255) for c in image.getdata())
 if file.stem.endswith('_Flame'):
  body=Image.open(file.with_name(file.name.replace('_Flame.png','.png'))).convert('RGBA')
  assert image.size==body.size and image.getbbox()
  assert all(c[3]==0 or c==b for c,b in zip(image.getdata(),body.getdata()));flames+=1
 elif 'HkFurniturePlatform_' in file.name:
  assert image.size==(original.width,18)
  if a['file'] in platform_overrides:
   # Frames 8..26 keep the native stair, corner and brace geometry.
   assert image.crop((144,0,486,18)).getchannel('A').tobytes()==original.crop((144,0,486,18)).getchannel('A').tobytes()
   assert hashlib.sha256(raw).hexdigest()==platform_overrides[a['file']]['sha256']
  else: assert image.getchannel('A').tobytes()==original.crop((0,0,original.width,18)).getchannel('A').tobytes()
  platforms+=1
 else:
  r=next(r for r in rows if file.stem=='hk_HkFurniture_'+r['region']+'_'+r['furniture'])
  if a['file'] in platform_overrides:
   assert image.size==(16,16) and hashlib.sha256(raw).hexdigest()==platform_overrides[a['file']]['sha256']
  else: assert image.tobytes()==Image.open(bundle/r['file']).convert('RGBA').tobytes()
  native+=1
assert (native,flames,platforms)==(504,120,24)
result={'passed':True,'native_sprites':native,'flame_layers':flames,'platform_connection_sheets':platforms,'native_pixels_equal_delivered_art':True,'flame_pixels_from_corresponding_art':True,'platform_stairs_match_vanilla_topology':True,'regional_platform_overrides':len(platform_overrides),'serialized_ids':ids}
(root/'artifacts/regional-furniture-integration/static-validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf8')
print(json.dumps(result,ensure_ascii=False))
