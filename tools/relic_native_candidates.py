"""Extract original tk2d animation frames for the separate relic art review package."""
from pathlib import Path
import argparse, collections, gc, json, re, sys

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/relic-remake-native'
ATLASES = {}
COLLECTIONS = {}
DATA = Path('F:/SteamLibrary/steamapps/common/Hollow Knight/hollow_knight_Data')
HELPERS = Path('D:/ClaudeTemp/claude/e------------github-----projects---/e332c8d1-bad4-4402-a7e3-1d6b4d5e6526/scratchpad/bossphases')

def save(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')

def inventory():
    import UnityPy
    e = UnityPy.load(str(DATA/'globalgamemanagers'))
    paths = next(o.read_typetree()['scenes'] for o in e.objects if o.type.name == 'BuildSettings')
    levels = {Path(p).stem: 'level'+str(i) for i,p in enumerate(paths)}
    text = (ROOT/'src/TerraKnight/Items/RelicsGenerated.cs').read_text(encoding='utf-8-sig')
    arenas = (ROOT/'src/TerraKnight/Items/BossArena.cs').read_text(encoding='utf-8-sig')
    arena = dict(re.findall(r'\["([^"]+)"\]\s*=\s*(?:Set\()?\s*[WDG]\("([^"]+)"', arenas))
    extras = {'AbsoluteRadiance':'GG_Radiance', 'EnragedGuardian':'GG_Crystal_Guardian_2',
              'SistersOfBattle':'GG_Mantis_Lords_V', 'WingedNosk':'GG_Nosk_Hornet',
              'Zote':'Room_Colosseum_Bronze', 'MageKnight':'Ruins1_23', 'PaleLurker':'GG_Lurker'}
    source = json.loads((ROOT/'空洞骑士大师圣物素材包-原图重制版/manifest.json').read_text(encoding='utf-8'))
    by_slug = {row['slug']:row for row in source}
    roster_scene = {row['key']:row['scene'] for row in json.loads((OUT.parent/'relic-references/boss-roster.json').read_text(encoding='utf-8'))}
    rows=[]
    for item, name, texture in re.findall(r'list.Add\(Relic\(ItemType\.(\w+), "([^"]+)", "(relic_[^"]+)"',text):
        slug=texture.removeprefix('relic_'); key=by_slug[slug]['key']
        scene=extras.get(key,arena.get(key,roster_scene.get(key)))
        assert scene, (key,'missing canonical scene')
        scene=scene+'_boss' if scene+'_boss' in levels else scene
        assert scene in levels,(key,scene)
        rows.append(dict(item=item,name=name,slug=slug,key=key,texture=texture,scene=scene,level=levels[scene]))
    assert len(rows)==48 and len({r['texture'] for r in rows})==48
    save(OUT/'inventory.json',rows)
    return rows

def scan():
    sys.path.insert(0,str(HELPERS)); import hk
    rows=inventory(); result={}
    for scene, group in __import__('itertools').groupby(sorted(rows,key=lambda x:x['scene']),key=lambda x:x['scene']):
        group=list(group); s=hk.Scene(group[0]['level']); libs={}
        for go,obj in s.of('tk2dSpriteAnimator'):
            try:
                animator=obj.read(); ptr=animator.library
                if not ptr.m_PathID: continue
                reader=ptr.deref(); ident=(Path(reader.assets_file.name).name,reader.path_id)
                if ident in libs: libs[ident]['paths'].append(s.path(go)); continue
                lib=ptr.read(); collection_names=set()
                for clip in lib.clips:
                    if clip.frames:
                        f=clip.frames[0]
                        if f.spriteCollection.m_PathID:
                            collection_names.add(f.spriteCollection.read().spriteCollectionName)
                libs[ident]={'file':ident[0],'pid':ident[1],'paths':[s.path(go)],'collections':sorted(collection_names),
                             'clips':[[c.name,len(c.frames)] for c in lib.clips]}
            except Exception as error:
                print('SKIP',scene,s.path(go),type(error).__name__,str(error)[:80],flush=True)
        result[scene]=list(libs.values());save(OUT/'animation-libraries.json',result)
        print(scene,[(r['collections'],r['paths'][0]) for r in result[scene]],flush=True)
        del s;gc.collect()

def frame_image(frame):
    import numpy as np
    from PIL import Image,ImageDraw,ImageChops
    cr=frame.spriteCollection.deref(); ck=(cr.assets_file.name,cr.path_id)
    if ck not in COLLECTIONS: COLLECTIONS[ck]=frame.spriteCollection.read()
    col=COLLECTIONS[ck]; d=col.spriteDefinitions[frame.spriteId]
    tr=col.textures[d.materialId].deref();tk=(tr.assets_file.name,tr.path_id)
    if tk not in ATLASES:
        tex=tr.read();ATLASES[tk]=(tex.image.convert('RGBA'),tex.m_Name)
    atlas,texture_name=ATLASES[tk]
    positions=np.array([(p.x,p.y) for p in d.positions],dtype=float)
    uv=np.array([(u.x*atlas.width,(1-u.y)*atlas.height) for u in d.uvs],dtype=float)
    lo=positions.min(axis=0); hi=positions.max(axis=0)
    # TK2D uses the source's 64 pixels per Unity unit. Geometry restores rotations and trim.
    width,height=max(1,round((hi[0]-lo[0])*64)),max(1,round((hi[1]-lo[1])*64))
    target=np.array([((x-lo[0])*64,(hi[1]-y)*64) for x,y in positions])
    matrix=np.column_stack([target,np.ones(len(target))])
    coeff=np.linalg.lstsq(matrix,uv,rcond=None)[0].T.flatten()
    image=atlas.transform((width,height),Image.Transform.AFFINE,coeff,Image.Resampling.NEAREST)
    mask=Image.new('L',image.size); draw=ImageDraw.Draw(mask)
    for i in range(0,len(d.indices),3): draw.polygon([tuple(target[v]) for v in d.indices[i:i+3]],fill=255)
    image.putalpha(ImageChops.multiply(image.getchannel('A'),mask))
    box=image.getbbox();assert box,(d.name,'empty extracted mesh')
    image=image.crop(box)
    rgba=np.array(image);rgba[rgba[:,:,3]==0,:3]=0
    return Image.fromarray(rgba),d.name,col.spriteCollectionName,texture_name

def candidates(config):
    sys.path.insert(0,str(HELPERS));import hk
    from PIL import Image,ImageDraw,ImageFont
    rows=inventory(); choices=json.loads(Path(config).read_text(encoding='utf-8'))
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',16)
    for row in rows:
        if row['slug'] not in choices: continue
        setting=choices[row['slug']]; directory=OUT/'candidates'/row['slug']
        if (directory/'frames.json').exists(): continue
        directory.mkdir(parents=True,exist_ok=True)
        s=hk.Scene(setting['file']);lib=s.lf.objects[setting['pid']].read();selected=[]
        clip_names=setting.get('clips')
        for ci,clip in enumerate(lib.clips):
            if clip_names and clip.name not in clip_names:continue
            if not clip.frames: continue
            if not clip_names and re.search(r'death|stun|hurt|corpse|effect|fx|splat|burst|impact|trail|flash',clip.name,re.I):continue
            for index in sorted(set([0,len(clip.frames)//2,len(clip.frames)-1])):
                f=clip.frames[index]
                if not f.spriteCollection.m_PathID:continue
                try:
                    image,definition,collection,atlas=frame_image(f)
                except AssertionError:
                    continue
                if image.width<15 or image.height<15:continue
                filename=f'{len(selected):03d}.png';image.save(directory/filename)
                selected.append(dict(file=filename,clip=clip.name,clip_index=ci,frame=index,sprite_id=f.spriteId,
                                     definition=definition,collection=collection,atlas=atlas,width=image.width,height=image.height))
        assert selected,row['slug']
        cols=6;cellw=250;cellh=230
        sheet=Image.new('RGB',(cols*cellw,((len(selected)+cols-1)//cols)*cellh),'#26323d');draw=ImageDraw.Draw(sheet)
        for i,entry in enumerate(selected):
            im=Image.open(directory/entry['file']);im.thumbnail((230,180),Image.Resampling.LANCZOS)
            x=(i%cols)*cellw;y=(i//cols)*cellh
            sheet.paste(im,(x+(cellw-im.width)//2,y+5+(180-im.height)//2),im)
            draw.text((x+5,y+185),f'{i:03d} {entry["clip"]}',font=font,fill='white')
            draw.text((x+5,y+207),f'frame {entry["frame"]}: {entry["definition"][:24]}',font=font,fill='#aebfce')
        sheet.save(directory/'contact.png');save(directory/'frames.json',selected)
        print(row['slug'],len(selected),flush=True);ATLASES.clear();COLLECTIONS.clear();del s;gc.collect()

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('mode',choices=['inventory','scan','candidates']);parser.add_argument('--config')
    args=parser.parse_args()
    if args.mode=='inventory':print(len(inventory()))
    elif args.mode=='scan':scan()
    else:candidates(args.config)
