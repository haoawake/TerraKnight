"""Offline layer contact sheets and animations, NOT in-game screenshots.

Uses TerraPlayerSprite's native composite cell lookup. Source and authored
colorways are composited with identical native player layers, without resampling.
"""
from pathlib import Path
import json, argparse
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]; OUT=ROOT/'output/regional-vanity-native'; REF=OUT/'references/vanilla'
ARM=[(2,0),(3,0),(4,0),(5,0),(6,0),(2,1),(3,1),(4,1),(4,1),(4,1),(4,1),(3,1),(3,1),(3,1),(5,1),(6,1),(6,1),(5,1),(3,1),(3,1)]
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',16)

def cell(im,x,y): return im.crop((x*40,y*56,x*40+40,y*56+56))
def tint(im,rgb):
 a=np.array(im); a[:,:,:3]=(a[:,:,:3].astype(np.uint16)*np.array(rgb,dtype=np.uint16)//255).astype(np.uint8); return Image.fromarray(a)

def render(spec,f,target=True,female=False,mirror=False):
 folder=OUT/f"{spec['index']:02d}-{spec['key']}"
 parts={e['kind']:Image.open(folder/'native'/f"{spec['key']}_{e['kind']}.png" if target else REF/(e['source']+'.png')).convert('RGBA') for e in spec['entries'] if not e['kind'].endswith('Item')}
 players={i:Image.open(REF/f'Player_0_{i}.png').convert('RGBA') for i in [0,1,2,3,5,7,10]}
 im=Image.new('RGBA',(40,56)); bob=-2 if f in [7,8,9,14,15,16] else 0
 fem=2 if female else 0; torso=(1 if f==5 else 0,fem); arm=ARM[f]; back=(arm[0],arm[1]+2)
 def put(tile,dy=0): im.alpha_composite(tile,(0,dy))
 skin=(240,174,131)
 for i in [7,5]: put(tint(cell(players[i],*back),skin),bob)
 put(cell(parts['Body'],*back),bob)
 put(cell(parts['Legs'],0,f))
 put(tint(cell(players[3],*torso),skin),bob)
 put(cell(parts['Body'],1,1+fem),bob)
 put(cell(parts['Body'],*torso),bob)
 for i,color in [(0,skin),(1,(255,255,255)),(2,(71,55,46))]: put(tint(cell(players[i],0,f),color))
 put(cell(parts['Head'],0,f))
 for i in [7,5]: put(tint(cell(players[i],*arm),skin),bob)
 put(cell(parts['Body'],*arm),bob)
 put(cell(parts['Body'],0,1+fem),bob)
 if mirror: im=im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
 return im

def review(index):
 spec=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))['sets'][index-1]
 folder=OUT/f"{index:02d}-{spec['key']}"
 board=Image.new('RGB',(20*40*2,5*56*2+48),(46,50,61)); d=ImageDraw.Draw(board)
 d.text((8,7),f"{index:02d} {spec['name']} | top: vanilla; rows 2-5: regional male/female, right/left | offline",font=FONT,fill='white')
 for row,(target,female,mirror) in enumerate([(False,False,False),(True,False,False),(True,False,True),(True,True,False),(True,True,True)]):
  for f in range(20):
   tile=render(spec,f,target,female,mirror).resize((80,112),Image.Resampling.NEAREST)
   board.paste(tile,(f*80,48+row*112),tile)
 board.save(folder/'all-poses-comparison.png')
 frames=[]
 for f in [0,0,5,5,*range(6,20)]:
  frame=Image.new('RGB',(400,256),(46,50,61)); dr=ImageDraw.Draw(frame)
  dr.text((8,5),f"{spec['name']} / frame {f} / offline",font=FONT,fill='white')
  for i,(target,female,mirror) in enumerate([(False,False,False),(True,False,False),(True,True,False),(True,False,True)]):
   tile=render(spec,f,target,female,mirror).resize((160,224),Image.Resampling.NEAREST)
   # Overlapping transparent frame margins leave 100 px per actual figure.
   frame.paste(tile,(i*100-30,32),tile)
  frames.append(frame)
 frames[0].save(folder/'animation-review.gif',save_all=True,append_images=frames[1:],duration=100,loop=0,disposal=2)
 print(json.dumps(dict(index=index,key=spec['key'],offline_pose_combinations=80,runtime_verified=False)))

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('index',type=int);a=p.parse_args();review(a.index)
