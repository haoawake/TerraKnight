import UnityPy,struct,json,re
from pathlib import Path
from UnityPy.classes import PPtr
root=Path(r'F:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data');e=UnityPy.load(str(root/'globalgamemanagers')); scenes=next(o.read_typetree()['scenes'] for o in e.objects if o.type.name=='BuildSettings')
source=Path('src/TerraKnight/Items/BossArena.cs').read_text(encoding='utf-8-sig');wanted=set(re.findall(r'W\("([^"]+)"',source));wanted|={s+'_boss' for s in list(wanted)};rows=[];cache={}
def ss(raw):
 result=[]
 for k in range(32,len(raw)-4,4):
  n=struct.unpack_from('<i',raw,k)[0]
  if 1<=n<=160 and k+4+n<=len(raw) and all(32<=b<127 for b in raw[k+4:k+4+n]):result.append(raw[k+4:k+4+n].decode())
 return result
def string(raw,pos):
 n=struct.unpack_from('<i',raw,pos)[0];assert 0<=n<1024;pos+=4;return raw[pos:pos+n].decode(),(pos+n+3)//4*4
for i,s in enumerate(scenes):
 scene=Path(s).stem
 if scene not in wanted:continue
 e=UnityPy.load(str(root/f'level{i}'));r={'scene':scene,'persistent':[],'loaders':[],'fsms':[]}
 for o in e.objects:
  if o.type.name!='MonoBehaviour':continue
  d=o.read_typetree(check_read=False);p=d['m_Script'];key=(o.assets_file.name,p['m_FileID'],p['m_PathID'])
  if key not in cache:cache[key]=PPtr(**p,assetsfile=o.assets_file).read().m_ClassName
  cl=cache[key]
  if cl not in ['PersistentBoolItem','SceneAdditiveLoadConditional','PlayMakerFSM']:continue
  go=PPtr(**d['m_GameObject'],assetsfile=o.assets_file).read().m_Name;raw=o.get_raw_data()
  if cl=='PersistentBoolItem':
   ident,pos=string(raw,36);sn,pos=string(raw,pos);r['persistent'].append({'go':go,'id':ident or go,'scene':sn or scene.removesuffix('_boss')})
  elif cl=='SceneAdditiveLoadConditional':r['loaders'].append({'go':go,'strings':ss(raw)})
  elif cl=='PlayMakerFSM':r['fsms'].append({'go':go,'strings':ss(raw)})
 rows.append(r)
Path('artifacts/native-encounter-reset-audit.json').write_text(json.dumps(rows,indent=2),encoding='utf8')
for r in rows:print(r['scene'],r['persistent'],[(l['go'],l['strings'][:12]) for l in r['loaders']])
