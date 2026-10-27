"""Transfer AI-authored color decisions to lossless original indexed animation geometry.

This deliberately does NOT claim new silhouettes: every output alpha byte, frame,
and source-color equivalence class is preserved. AI images are color proposals;
their geometry is never used as final equipment geometry.
"""
from pathlib import Path
import json, hashlib, argparse, shutil
from collections import defaultdict
import numpy as np
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'output/regional-vanity-native'
REFS=OUT/'references/vanilla'

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def finalize(index,proposal):
 manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
 spec=manifest['sets'][index-1]
 folder=OUT/f"{index:02d}-{spec['key']}"
 proposal=Path(proposal)
 if proposal.resolve()!=(folder/'generated-original.png').resolve(): shutil.copy2(proposal,folder/'generated-original.png')
 generated=Image.open(proposal).convert('RGBA')
 native=generated.resize((512,768),Image.Resampling.NEAREST)
 observed=np.array(native)
 reference=np.array(Image.open(folder/'template-native.png').convert('RGBA'))
 union=(reference[:,:,3]>0)&(observed[:,:,3]>160)
 overlap=float(union.sum()/max(1,(reference[:,:,3]>0).sum()))
 # Generators sometimes add margins, resize individual sprites or leave an
 # opaque matte despite the alpha request. Those pixels must NEVER become
 # equipment geometry or be sampled as clothing colors.
 alpha=observed[:,:,3]
 if np.any(alpha<128):
  foreground=alpha>160
 else:
  rgb=observed[:,:,:3].astype(np.int16)
  edges=np.zeros(alpha.shape,dtype=bool)
  edges[1:,:]|=np.max(np.abs(rgb[1:,:]-rgb[:-1,:]),axis=2)>28
  edges[:,1:]|=np.max(np.abs(rgb[:,1:]-rgb[:,:-1]),axis=2)>28
  padded=np.pad(edges,1)
  foreground=np.zeros_like(edges)
  for oy in range(3):
   for ox in range(3): foreground|=padded[oy:oy+768,ox:ox+512]
 seen=np.zeros_like(foreground)
 components=[]
 for yy,xx in np.argwhere(foreground):
  if seen[yy,xx]: continue
  stack=[(int(yy),int(xx))];seen[yy,xx]=True; xs=[];ys=[]
  while stack:
   y,x=stack.pop();xs.append(x);ys.append(y)
   for ny,nx in [(y-1,x),(y+1,x),(y,x-1),(y,x+1)]:
    if 0<=ny<768 and 0<=nx<512 and foreground[ny,nx] and not seen[ny,nx]:
     seen[ny,nx]=True;stack.append((ny,nx))
  x0,y0,x1,y1=min(xs),min(ys),max(xs)+1,max(ys)+1
  if 3<=x1-x0<=90 and 3<=y1-y0<=90 and len(xs)>=8: components.append((x0,y0,x1,y1))
 if not components: raise ValueError('No usable generated garment components')
 report=dict(index=index,key=spec['key'],name=spec['name'],mode='AI-color-proposal-original-indexed-geometry',generated_size=list(generated.size),template_overlap=overlap,files=[])
 assetdir=folder/'native'; assetdir.mkdir(exist_ok=True)
 pack=folder/'resource-pack'; (pack/'Content/Images/Armor').mkdir(parents=True,exist_ok=True)
 body_mapping=None; part_mappings={}
 for e in spec['entries']:
  src=Image.open(REFS/(e['source']+'.png')).convert('RGBA'); a=np.array(src)
  samples=defaultdict(list)
  for m in spec['atlas_map']:
   if m['source']!=e['source']: continue
   sx,sy,w,h=m['src']; dx,dy,_,_=m['dst']
   ss=a[sy:sy+h,sx:sx+w]
   bounds=Image.fromarray(ss).getbbox()
   if bounds is None: continue
   x0,y0,x1,y1=bounds; cx=dx+(x0+x1)/2; cy=dy+(y0+y1)/2
   candidates=[b for b in components if abs((b[0]+b[2])/2-cx)<56 and abs((b[1]+b[3])/2-cy)<56]
   if not candidates: continue
   # Register only COLOR to the source's exact indexed contour. A nearest
   # neighboring pose of the same garment is also valid as a color sample.
   box=min(candidates,key=lambda b: ((b[0]+b[2])/2-cx)**2+((b[1]+b[3])/2-cy)**2)
   patch=Image.fromarray(observed).crop(box).resize((x1-x0,y1-y0),Image.Resampling.NEAREST)
   gg=np.array(patch); cut=ss[y0:y1,x0:x1]
   # The generated matte can retain colored RGB under alpha=0. Sampling it
   # would incorrectly turn ivory cloth or rose petals into muddy brown.
   valid=(cut[:,:,3]>0)&(gg[:,:,3]>160)
   for rgb,gen in zip(cut[:,:,:3][valid],gg[:,:,:3][valid]): samples[tuple(int(v) for v in rgb)].append(gen)
  if e['kind'].endswith('Item'):
   mapping=dict(part_mappings[e['kind'][:-4]])
  elif e['kind'].startswith('Cape'):
   if body_mapping is None: raise ValueError('Missing parent body color proposal')
   mapping=dict(body_mapping)
  else:
   if not samples: raise ValueError(f'No generated samples for {e["source"]}')
   mapping={rgb:np.rint(np.median(vals,axis=0)).astype(np.uint8) for rgb,vals in samples.items()}
  keys=np.array(list(mapping),dtype=float)
  result=a.copy(); visible=a[:,:,3]>0
  original_colors=np.unique(a[:,:,:3][visible],axis=0)
  for rgb in original_colors:
   key=tuple(int(v) for v in rgb)
   # Rare edge/glow pixels omitted by the model inherit the nearest sampled
   # SOURCE palette entry, so no unvalidated model geometry enters the asset.
   if key not in mapping:
    near=int(np.argmin(np.sum((keys-rgb)**2,axis=1)))
    mapping[key]=mapping[tuple(int(v) for v in keys[near])]
   pick=visible&np.all(a[:,:,:3]==rgb,axis=2)
   result[:,:,:3][pick]=mapping[key]
  result[~visible,:3]=0
  if e['kind']=='Body': body_mapping=dict(mapping)
  if e['kind'] in ['Head','Body','Legs']: part_mappings[e['kind']]=dict(mapping)
  out=Image.fromarray(result)
  filename=f"{spec['key']}_{e['kind']}.png"
  path=assetdir/filename; out.save(path)
  replacement=pack/'Content/Images'/(e['source']+'.png'); replacement.parent.mkdir(parents=True,exist_ok=True); out.save(replacement)
  check=np.array(Image.open(path).convert('RGBA'))
  same_alpha=bool(np.array_equal(a[:,:,3],check[:,:,3]))
  same_size=out.size==src.size
  # Across-frame source-color identity is an invariant of the transfer.
  stable=all(len(np.unique(check[:,:,:3][visible&np.all(a[:,:,:3]==rgb,axis=2)],axis=0))==1 for rgb in original_colors)
  assert same_alpha and same_size and stable
  report['files'].append(dict(kind=e['kind'],path=str(path.relative_to(OUT)).replace('\\','/'),replacement=e['source']+'.png',size=list(out.size),same_alpha_as_source=same_alpha,frame_palette_consistent=stable,palette_colors=len(np.unique(check[:,:,:3][visible],axis=0)),sha256=sha(path),source_sha256=e['sha256']))
  # Readable contact sheet of EVERY native cell; never used as a final asset.
  if e['kind'] in ['Head','Legs']:
   board=Image.new('RGBA',(200,224))
   for f in range((out.height+55)//56):
    cell=out.crop((0,f*56,40,min((f+1)*56,out.height)))
    board.paste(cell,(f%5*40,f//5*56))
  elif e['kind']=='Body': board=out
  else: board=None
  if board is not None:
   bg=Image.new('RGBA',board.size,(48,52,64,255)); bg.alpha_composite(board)
   bg.resize((bg.width*4,bg.height*4),Image.Resampling.NEAREST).convert('RGB').save(folder/(e['kind']+'-all-frames-review.png'))
 packmeta=dict(Name=f"TerraKnight {spec['name']} 时装",Author='TerraKnight',Description=f"{spec['name']}地区配色，保留原版{', '.join(e['item'] for e in spec['entries'] if not e['kind'].endswith('Item'))}的全部像素结构与原版动画。",Version=dict(major=1,minor=0))
 (pack/'pack.json').write_text(json.dumps(packmeta,ensure_ascii=False,indent=2),encoding='utf-8')
 report['static_native_checks_passed']=True
 report['runtime_equipment_verified']=False
 report['new_silhouette']=False
 report['color_registration']='connected generated garment components registered to source indexed palette; generated geometry and matte discarded'
 (folder/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
 # Palette source/target review is explicit, so color variants cannot be
 # mistaken for newly authored shape designs.
 print(json.dumps(dict(index=index,key=spec['key'],files=len(report['files']),overlap=round(overlap,3),static_pass=True),ensure_ascii=False))
 return report

if __name__=='__main__':
 p=argparse.ArgumentParser(); p.add_argument('index',type=int); p.add_argument('proposal'); a=p.parse_args(); finalize(a.index,a.proposal)
