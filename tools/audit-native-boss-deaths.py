"""Read-only native death-owner audit; no game process or saves are opened."""
import UnityPy,struct,json,re
from pathlib import Path
from UnityPy.classes import PPtr
root=Path(r'F:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data')
e=UnityPy.load(str(root/'globalgamemanagers')); scenes=next(o.read_typetree()['scenes'] for o in e.objects if o.type.name=='BuildSettings')
source=Path('src/TerraKnight/Items/BossArena.cs').read_text(encoding='utf-8-sig')
wanted=set(re.findall(r'(?:W|D|G)\("([^"]+)"',source))
for line in Path('src/TerraKnight/Items/EnemyDropData.cs').read_text(encoding='utf-8-sig').splitlines():
 if 'Boss = true' in line:
  m=re.search(r'Scene = "([^"]+)"',line)
  if m:wanted.add(m[1])
wanted|={Path(s).stem for s in scenes if Path(s).stem.startswith(('GG_','Dream_'))}
wanted|={Path(s).stem for s in scenes if any(Path(s).stem==w+'_boss' for w in wanted)}
rows=[];cache={}
def strings(o):
 raw=o.get_raw_data();ss=[]
 for k in range(32,len(raw)-4,4):
  n=struct.unpack_from('<i',raw,k)[0]
  if 1<=n<=180 and k+4+n<=len(raw) and all(32<=x<127 for x in raw[k+4:k+4+n]):ss.append(raw[k+4:k+4+n].decode())
 return ss
def script(o,d):
 p=d['m_Script'];key=(o.assets_file.name,p['m_FileID'],p['m_PathID'])
 if key not in cache:cache[key]=PPtr(**p,assetsfile=o.assets_file).read().m_ClassName
 return cache[key]
for i,scene in enumerate(scenes):
 scene=Path(scene).stem
 if scene not in wanted:continue
 e=UnityPy.load(str(root/f'level{i}'));row={'scene':scene,'effects':[],'deathFsms':[]}
 for o in e.objects:
  if o.type.name!='MonoBehaviour':continue
  d=o.read_typetree(check_read=False);cl=script(o,d)
  if not cl.startswith('EnemyDeathEffects') and cl!='PlayMakerFSM':continue
  go=PPtr(**d['m_GameObject'],assetsfile=o.assets_file).read().m_Name
  if cl.startswith('EnemyDeathEffects'):
   raw=o.get_raw_data();fid,pid=struct.unpack_from('<iq',raw,32);pref=None;fs=[]
   if pid:
    p=PPtr(m_FileID=fid,m_PathID=pid,assetsfile=o.assets_file);pref=p.read().m_Name
    for c in p.read().m_Component:
     co=c.component.deref()
     if co.type.name=='MonoBehaviour':
      cd=co.read_typetree(check_read=False)
      if script(co,cd)=='PlayMakerFSM':fs.append(strings(co))
   row['effects'].append({'body':go,'type':cl,'prefab':pref,'corpseFsms':fs})
  else:
   ss=strings(o)
   if any(re.search(r'(Death|Dying|Blow|Explode|Burst|Splat)',s) and '.' not in s for s in ss):row['deathFsms'].append({'owner':go,'strings':ss})
 rows.append(row)
Path('artifacts/native-boss-death-audit.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8')
print('Scenes',len(rows),'effects',sum(len(r['effects']) for r in rows),'FSMs',sum(len(r['deathFsms']) for r in rows))
