from pathlib import Path
import copy, json, shutil, sys
from PIL import Image, ImageDraw, ImageFont, ImageOps
import relic_native_candidates as native

OUT=native.OUT
SELECTED={
 'absoluteradiance':4,'bigbuzzer':13,'bigfly':16,'blackknight':7,'dungdefender':7,'enragedguardian':12,
 'falseknight':10,'falseknight-5':12,'finalboss':7,'flukemother':10,'ghostaladar':7,'ghostgalien':12,
 'ghosthu':4,'ghostmarkoth':4,'ghostmarmu':4,'ghostnoeyes':4,'ghostxero':4,'greyprince':1,
 'grimm':6,'hiveknight':8,'hollowknight':13,'hollowknightprime':13,'hornet':4,'hornet-4':16,
 'infectedknight':0,'infectedknight-5':4,'jarcollector':14,'lobsterlancer':1,'mageknight':13,
 'magelord':13,'magelord-5':4,'mantislord':9,'mawlek':7,'megabeamminer':4,'megajellyfish':4,
 'megamosscharger':11,'mimicspider':16,'nailbros':1,'nailsage':15,'nightmaregrimm':11,
 'oblobble':1,'paintmaster':1,'palelurker':13,'sistersofbattle':12,'traitorlord':13,
 'whitedefender':10,'wingednosk':1,'zote':7,
}

def frame(slug,index):
    folder=OUT/'candidates'/slug
    meta=json.loads((folder/'frames.json').read_text(encoding='utf-8'))[index]
    return Image.open(folder/meta['file']).convert('RGBA'),meta

def panel(parts,size=(720,720)):
    result=Image.new('RGBA',size)
    for image,box,flip in parts:
        image=ImageOps.mirror(image) if flip else image.copy()
        image.thumbnail((box[2]-box[0],box[3]-box[1]),Image.Resampling.LANCZOS)
        result.alpha_composite(image,(box[0]+(box[2]-box[0]-image.width)//2,box[1]+(box[3]-box[1]-image.height)//2))
    return result.crop(result.getbbox())

def main():
    sys.path.insert(0,str(native.HELPERS));import hk
    rows=native.inventory();selected=[];(OUT/'selected-frames').mkdir(exist_ok=True)
    for row in rows:
        slug=row['slug'];image,meta=frame(slug,SELECTED[slug]);members=2 if slug in ['nailbros','oblobble','lobsterlancer'] else 3 if slug in ['mantislord','sistersofbattle'] else 1
        components=[dict(meta,mirror=False)];note='原版完整、清晰的战斗蓄势或施法帧；避开受伤、死亡和过度模糊特效。'
        if slug in ['mantislord','sistersofbattle']:
            ids=[14,9,14] if slug=='mantislord' else [9,12,9]
            parts=[];components=[]
            for i,index in enumerate(ids):
                im,part=frame(slug,index);parts.append((im,(i*230,40 if i!=1 else 0,i*230+260,700),i==2));components.append(dict(part,mirror=i==2))
            image=panel(parts);note='三位领主：使用三张真实动画帧并列，右侧沿用游戏水平翻转；不创造新姿态。'
        elif slug=='nailbros':
            other,part=frame(slug,8);image=panel([(image,(0,140,390,700),False),(other,(340,0,720,700),False)])
            components.append(dict(part,mirror=False));note='奥罗与马托双人构图；两位均来自实际动画库的完整持钉帧。'
        elif slug=='oblobble':
            other,part=frame(slug,7);image=panel([(image,(0,50,380,650),False),(other,(340,50,720,650),True)])
            components.append(dict(part,mirror=True));note='双奥波路波：实际飞行与射击帧，右侧按游戏朝向水平翻转。'
        elif slug in ['ghostgalien','ghostxero']:
            file,pid=('sharedassets298.assets',61) if slug=='ghostgalien' else ('sharedassets238.assets',37)
            scene=hk.Scene(file);lib=scene.lf.objects[pid].read()
            if slug=='ghostgalien':
                clip=next(c for c in lib.clips if c.name=='Weapon Idle');f=clip.frames[0]
            else:
                clip=lib.clips[0];f=copy.copy(clip.frames[0]);f.spriteId=0
            weapon,definition,collection,atlas=native.frame_image(f)
            (OUT/'selected-components').mkdir(exist_ok=True);weapon.save(OUT/'selected-components'/f'{slug}-weapon.png')
            components.append(dict(clip='Weapon Idle' if slug=='ghostgalien' else 'static projectile sprite',frame=0,sprite_id=f.spriteId,definition=definition,collection=collection,atlas=atlas))
            if slug=='ghostgalien':image=panel([(image,(0,210,600,720),False),(weapon,(380,0,720,360),False)])
            else:
                weapon=weapon.rotate(90,expand=True)
                components[-1]['rotation_degrees']=90
                image=panel([(image,(100,60,620,700),False),(weapon,(0,100,140,630),False),(weapon,(580,100,720,630),True)])
            note='本体选定动画帧，另附同一原版图集的实际武器精灵；仅做构图排布，不设计新武器。'
            native.ATLASES.clear();native.COLLECTIONS.clear()
        elif slug=='ghostmarkoth':
            shield,part=frame(slug,10);image=panel([(image,(0,0,570,720),False),(shield,(500,180,720,580),False)])
            components.append(dict(part,mirror=False));note='披风展开攻击帧与原版单面梦之盾精灵组合。'
        import numpy as np
        rgba=np.array(image);rgba[rgba[:,:,3]==0,:3]=0;image=Image.fromarray(rgba)
        target=OUT/'selected-frames'/f'{slug}.png';image.save(target)
        selected.append(dict(row,selection=meta,components=components,members=members,reference=str(target),reason=note))
    native.save(OUT/'selections.json',selected)
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',16)
    for page in range(4):
        sheet=Image.new('RGB',(1200,1200),'#25303b');draw=ImageDraw.Draw(sheet)
        for i,row in enumerate(selected[page*12:(page+1)*12]):
            im=Image.open(row['reference']);im.thumbnail((280,240),Image.Resampling.LANCZOS);x=(i%4)*300;y=(i//4)*400
            sheet.paste(im,(x+(300-im.width)//2,y+20+(260-im.height)//2),im)
            draw.text((x+8,y+285),row['name'],font=font,fill='#ffe99b');draw.text((x+8,y+312),row['slug'],font=font,fill='white')
            draw.text((x+8,y+340),row['selection']['clip']+' / '+str(row['selection']['frame']),font=font,fill='#aebfce')
        sheet.save(OUT/f'selected-review-{page+1}.png')
    print('Prepared',len(selected),'selected references')

if __name__=='__main__':main()
