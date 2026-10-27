"""Extract installed Terraria references and create per-set, reversible generation atlases.

The atlas is transport only. Its cell map reconstructs the exact source dimensions.
"""
from pathlib import Path
import json, re, subprocess, hashlib
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/regional-vanity-native'
GAME = Path('F:/SteamLibrary/steamapps/common/Terraria')
PY = 'C:/Users/luo/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
ROWS = [
 ('TimelessTravelerHood','TimelessTravelerRobe','TimelessTravelerBottom','weathered limestone gray hood, slate travel robe, charcoal wrapped trousers; warm gray cloth highlights, desaturated brown belt'),
 ('GangstaHat','ClothierJacket','ClothierPants','Dirtmouth townsfolk: warm umber hat, ochre brown jacket with cream shirt, dark chocolate trousers, brass buttons'),
 ('ParkaHood','ParkaCoat','ParkaPants','Howling Cliffs: blue-gray windproof parka, ivory fur edging, slate boots, cold pale gray highlights'),
 ('SailorHat','SailorShirt','SailorPants','Forgotten Crossroads: navy work cap and work shirt, dusty cream trim, charcoal trousers, tarnished brass details'),
 ('HerosHat','HerosShirt','HerosPants','Greenpath: moss green pointed cloth cap and layered tunic, olive highlights, deep forest shadows, bark-brown boots and belt'),
 ('MushroomHat','MushroomVest','MushroomPants','Fungal Wastes: golden ochre mushroom cap with cream spots, muted plum vest, warm amber trousers, brown boots'),
 ('FloretProtectorHelmet','FloretProtectorChestplate','FloretProtectorLegs','Fog Canyon: pale lavender protective clothing, violet shadows, restrained coral pink trim, pale blue glass highlights; preserve plant and glass structure'),
 ('FlowerBoyHat','FlowerBoyShirt','FlowerBoyPants','Queens Gardens: deep emerald greenery and rose pink flowers, ivory collar, dark forest shadows, jade cloth highlights'),
 ('TaxCollectorHat','TaxCollectorSuit','TaxCollectorPants','City of Tears: midnight navy top hat and formal coat, silver buttons, pale gray cravat, blue-gray trousers'),
 ('PlumbersHat','PlumbersShirt','PlumbersPants','Royal Waterways: muted olive work cap, teal shirt, moss-brown overalls, tarnished brass buckles, dark brown rubber boots'),
 ('EngineeringHelmet','CyborgShirt','CyborgPants','Crystal Peak: dusty violet mining helmet and work suit, bright pink crystal-colored accents, dark gray boots and straps, pale rose highlights'),
 ('FuneralHat','FuneralCoat','FuneralPants','Resting Grounds: muted indigo mourning formalwear, pale blue-gray hat edging, ivory scarf and sash details, slate trousers'),
 ('PlaguebringerHelmet','PlaguebringerChestplate','PlaguebringerGreaves','Deepnest: near-black indigo silk hood and layered clothing, violet woven trim, pale gray thread highlights, no neon green'),
 ('WolfMask','WolfShirt','WolfPants','Beasts Den: umber and dark brown fur costume, muted tan fur highlights, black cloth shadows; retain original wolf costume outline'),
 ('CowboyHat','CowboyJacket','CowboyPants','Kingdoms Edge: ash-white broad-brim hat, pale cream dust jacket, slate-gray trousers, brown leather boots and straps'),
 ('BeeHat','BeeShirt','BeePants','Hive: amber and honey-gold padded bee costume, dark warm brown stripes, pale wax highlights, orange-brown boots'),
 ('AncientArmorHat','AncientArmorShirt','AncientArmorPants','Ancient Basin: weathered ash-gray ancient explorer clothing, umber straps, faded ivory highlights, charcoal trousers'),
 ('SuperHeroMask','SuperHeroCostume','SuperHeroTights','Abyss: near-black navy mask and fitted clothing, restrained desaturated blue seam highlights, deep charcoal boots; no bright primary colors'),
 ('GoldCrown','PrinceUniform','PrincePants','White Palace: ivory and white formal royal uniform, warm gold crown and fine gold edging, pale gray shadows, ivory boots'),
 ('ReaperHood','FallenTuxedoShirt','FallenTuxedoPants','Black Egg Temple: midnight blue hood and ceremonial formalwear, tiny pale gray buttons, restrained cold silver trim, almost black trousers'),
 ('FoodBarbarianHelm','FoodBarbarianArmor','FoodBarbarianGreaves','Colosseum of Fools: bronze helmet and warm rusty red gladiator clothing, brown leather, muted cream cloth, brass highlights'),
 ('VampireMask','VampireShirt','VampirePants','Grimm Troupe: black and crimson theatrical formalwear, deep red collar and lining, dark charcoal trousers, small muted gold buttons; preserve face colors'),
 ('PedguinHat','PedguinShirt','PedguinPants','Dream: soft lilac and muted lavender sleepwear costume, pale ivory accents, subdued gold small details, violet shadows'),
 ('RoyalTiara','RoyalDressTop','RoyalDressBottom','Godhome: rich honey-gold ceremonial dress and crown, ivory cloth panels, muted bronze shadows, pale gold highlights'),
]

def main():
 OUT.mkdir(parents=True,exist_ok=True)
 refs=OUT/'references/vanilla'
 (refs/'Armor').mkdir(parents=True,exist_ok=True)
 current=OUT/'references/current-vanity-runtime.json'
 data=json.loads((current if current.exists() else ROOT/'tools/vanilla-data/vanity/vanity-runtime.json').read_text(encoding='utf-8-sig'))
 items={x['name']:x for x in data['items']}
 areas=re.findall(r'new Area\("([^"]+)", "([^"]+)"',(ROOT/'src/TerraKnight/World/AreaBlocks.cs').read_text(encoding='utf-8'))
 sets=[]; names=set()
 for n,(area,row) in enumerate(zip(areas,ROWS),1):
  key,zh=area; h,b,l,design=row
  entries=[]
  for kind,itemname,slot,prefix in [('Head',h,'headSlot','Armor_Head_'),('Body',b,'bodySlot','Armor/Armor_'),('Legs',l,'legSlot','Armor_Legs_')]:
   it=items[itemname]; tex=prefix+str(it[slot]); icon='Item_'+str(it['id'])
   entries.extend([dict(kind=kind,source=tex,item=itemname,item_id=it['id'],slot=it[slot]),dict(kind=kind+'Item',source=icon,item=itemname,item_id=it['id'],slot=it[slot])])
   names.update([tex,icon])
  body=items[b]['bodySlot']; supplements={}
  for field,prefix in [('IncludedCapeBack','Acc_Back_'),('IncludedCapeBackFemale','Acc_Back_'),('IncludedCapeFront','Acc_Front_')]:
   table=data['armor']['Body'].get(field,{})
   value=table.get('values',{}).get(str(body),table.get('default',-1))
   if value>=0: supplements[prefix+str(value)]=dict(kind=('CapeBack' if prefix=='Acc_Back_' else 'CapeFront')+str(value),source=prefix+str(value),item=b,item_id=items[b]['id'],slot=value,included_by=field,color_source='Body')
  entries.extend(supplements.values()); names.update(supplements)
  sets.append(dict(index=n,key=key,name=zh,design=design,entries=entries))
 dump=ROOT/'tools/XnbDump/bin/Release/net10.0/XnbDump.exe'
 run=subprocess.run([str(dump),str(GAME/'Content/Images'),str(refs),*sorted(names)],capture_output=True,text=True,encoding='utf-8',errors='replace')
 if run.returncode: raise RuntimeError(run.stdout+run.stderr)
 for s in sets:
  folder=OUT/f"{s['index']:02d}-{s['key']}"; folder.mkdir(exist_ok=True)
  atlas=Image.new('RGBA',(512,768)); mappings=[]
  for e in s['entries']:
   im=Image.open(refs/(e['source']+'.png')).convert('RGBA')
   e['size']=list(im.size); e['sha256']=hashlib.sha256((refs/(e['source']+'.png')).read_bytes()).hexdigest()
   if e['kind'].startswith('Cape'): continue
   if e['kind'] in ['Head','Legs']:
    bx=0 if e['kind']=='Head' else 200
    for f in range((im.height+55)//56):
     h=min(56,im.height-f*56); dx=bx+(f%5)*40; dy=(f//5)*56
     atlas.paste(im.crop((0,f*56,40,f*56+h)),(dx,dy))
     mappings.append(dict(source=e['source'],src=[0,f*56,40,h],dst=[dx,dy,40,h]))
   else:
    if e['kind']=='Body': x,y=0,224
    else: x,y=416,{'HeadItem':16,'BodyItem':80,'LegsItem':144}[e['kind']]
    assert x+im.width<=512 and y+im.height<=768,(e['source'],im.size)
    atlas.paste(im,(x,y)); mappings.append(dict(source=e['source'],src=[0,0,im.width,im.height],dst=[x,y,im.width,im.height]))
  s['atlas_map']=mappings
  atlas.save(folder/'template-native.png')
  atlas.resize((1024,1536),Image.Resampling.NEAREST).save(folder/'template-2x.png')
  (folder/'source.json').write_text(json.dumps(s,ensure_ascii=False,indent=2),encoding='utf-8')
 (OUT/'manifest.json').write_text(json.dumps(dict(mode='vanilla-geometry-regional-colorways',sets=sets),ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps(dict(sets=len(sets),source_textures=len(names),output=str(OUT)),ensure_ascii=False))

if __name__=='__main__': main()
