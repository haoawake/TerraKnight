"""Package metadata and the offline comparison viewer; image conversion uses the PS exporter."""
import base64
import json
import re
import shutil
from pathlib import Path

project=Path(__file__).resolve().parents[1]
root=project/'角色特殊动画素材包'
reference=project/'artifacts/resume-heroanimexport-20261020-211758/saves/hero-animation-reference'
refout=root/'原版参考'
shutil.copytree(reference,refout,dirs_exist_ok=True)
shutil.copy2(project/'artifacts/hero-absorb-native-contact.png',refout/'魔法吸收动作对照.png')
native=json.loads((reference/'native-clips.json').read_text(encoding='utf-8-sig'))
source=(project/'src/TerraKnight/Player/HeroSpecialAnimations.cs').read_text(encoding='utf-8-sig')
jobs=json.loads((root/'生成清单.json').read_text(encoding='utf-8-sig'))
names=['魔法吸收与倒下','地面苏醒与起身','聚集与恢复','普通能力获取','展开与阅读地图','吼叫震慑与恢复','梦境传送蓄势']
mapping={name:dict(kind=int(kind),first=int(first),last=int(last)) for name,kind,first,last in re.findall(r'\["([^"]+)"\]=new Range\((\d+),(\d+),(\d+)\)',source)}
rows=[]
for c in native:
    label='沿用现有泰拉人物渲染、移动、战斗或坐姿流程；本轮未新增逐帧'
    if not c['frames']: label='原版空槽'
    elif re.search(r'Effect|Fx|Trail|Wings|Recharge|Crys|Hit Crack',c['name']): label='独立特效或附属效果，本轮不替换为人体'
    if c['name'] in mapping: label='本轮新增特殊人物动画'
    rows.append(dict(nativeClip=c['name'],fps=c['fps'],nativeFrameCount=len(c['frames']),classification=label,replacement=mapping.get(c['name'])))
(root/'原版动画审计.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
config=dict(generator='built-in imagegen',frameSize=[96,96],feetAnchor=[48,82],sampling='Point/Nearest',alpha='0 or 255',
    sheets=[dict(id=j['id'],name=names[i],texture='动画图集/'+j['id']+'.png',frames=j['frames']) for i,j in enumerate(jobs)],
    clipMappings=mapping,nativeTiming='Original FPS, frame count, wrap mode and FSM events are unchanged; native frame progress selects a replacement drawing.',
    visualPolicy='Use default unarmored Terraria appearance for these special actions, restore worn equipment afterward.',
    network='Protocol net9. Signed-byte Body codes -2 through -85 carry special kind/frame; all participants need matching builds.')
config['standaloneActors']=dict(driver='Player/HeroCutsceneDouble.cs',rules=['Knight Get * descendants','Knight Dummy'],
    vengefulSpirit='_Props/Knight Get Fireball/Knight Cutscene Animator',desolateDive='Quake Real Parent/Knight Get Quake/Knight Cutscene Animator',
    monarchWings='higher_being/Dish Plat/Knight Dummy',behavior='Hide original renderer with forceRenderingOff, retain its enabled flag, animator/FSM/events, follow position/facing, replace only character body, forward visible pose when the main hero is hidden.')
(root/'导入配置.json').write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
data=[]
for i,j in enumerate(jobs):
    data.append(dict(id=j['id'],name=names[i],count=j['frames'],png='data:image/png;base64,'+base64.b64encode((root/'动画图集'/f"{j['id']}.png").read_bytes()).decode()))
# The original absorption segment and its exact native frame-to-drawing mapping.
original=[]
for name in ['Collect Magical 1','Collect Magical 2','Collect Magical 3','Collect Magical Fall','Collect Magical Land']:
    c=next(c for c in native if c['name']==name)
    m=mapping[name]
    for f in range(len(c['frames'])):
        path=refout/'native-frames'/name.replace(' ','_')/f'{f:03}.png'
        original.append(dict(clip=name,frame=f,replacement=round(m['first']+(m['last']-m['first'])*(f/max(1,len(c['frames'])-1))),png='data:image/png;base64,'+base64.b64encode(path.read_bytes()).decode()))
html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>泰拉人物特殊动画 · 原版动作对照</title>
<style>*{box-sizing:border-box}body{background:#171d28;color:#eef2f8;font:16px system-ui,"Microsoft YaHei";margin:28px auto;max-width:1150px;padding:0 20px}p{color:#b9c7d8;line-height:1.8}h1{font-size:27px}button,select,input{font:inherit;padding:8px;background:#324156;color:white;border:1px solid #708099;border-radius:6px}.bar{display:flex;gap:14px;align-items:center;flex-wrap:wrap;padding:14px;background:#273448;border-radius:9px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:18px;margin:20px 0}section{background:#253041;padding:16px;border-radius:12px}canvas{width:100%;height:auto;background:#3b4555;image-rendering:pixelated}img{image-rendering:pixelated}.cards{display:grid;grid-template-columns:repeat(4,1fr);gap:12px}.card{padding:12px;background:#273448;border-radius:8px}.card canvas{height:190px;object-fit:contain}small{color:#b9c7d8}@media(max-width:700px){.grid{grid-template-columns:1fr}.cards{grid-template-columns:repeat(2,1fr)}}</style>
<h1>泰拉人物特殊动画 · 原版动作对照</h1><p>以项目当前初始人物重绘，7套64帧。魔法吸收对照使用本机原版导出的动作帧；新角色保留红棕头发、浅蓝袖口、灰米色上衣、米色裤子与棕鞋。按实机默认人物约30×48像素校准，96×96透明画布保留伸展空间。吸收保持静止；预览单次播放，按“重新播放”重看。展示动画不含粒子与光效。</p>
<div class="bar"><label>动作 <select id="kind"></select></label><button id="pause">暂停</button><button id="replay">重新播放</button><label>帧 <input id="frame" type="range" min="0" max="11" value="0"></label><label><input id="flip" type="checkbox">向左</label><label><input id="light" type="checkbox">浅色背景</label><span id="status"></span></div>
<div class="grid"><section><h2>原版小骑士 · 魔法吸收</h2><canvas id="native" width="480" height="320"></canvas><p id="nativeLabel"></p></section><section><h2>初始泰拉人物 · 替换动画</h2><canvas id="new" width="480" height="320"></canvas><p>固定96×96画布、脚底锚点(48,82)，左右镜像；原版状态机继续控制时序。</p></section></div>
<section><h2>同一比例下的人物尺寸</h2><img src="人物尺寸校准对照.png" width="500" style="max-width:100%"><p>左侧为游戏内默认人物，右侧为吸收起始姿势；两者以相同像素比例显示。</p></section><div class="cards" id="cards"></div><p>接入代码：Player/HeroSpecialAnimations.cs、Player/TerraPlayerSprite.cs。完整清单见 <a href="导入配置.json">导入配置</a> 和 <a href="原版动画审计.json">214条目审计</a>。本轮覆盖原版英雄主动画库中的特殊动作；已有移动、武器战斗、坐姿及独立特效继续使用各自流程，未把全部214条目重复生成人体贴图。</p>
<script>const data=__DATA__,original=__ORIGINAL__;const ims=data.map(d=>{let i=new Image;i.src=d.png;return i}),old=original.map(d=>{let i=new Image;i.src=d.png;return i});
const select=document.querySelector('#kind'),slider=document.querySelector('#frame');select.innerHTML=data.map((d,i)=>`<option value="${i}">${d.name}</option>`).join('');document.querySelector('#cards').innerHTML=data.map((d,i)=>`<article class="card"><canvas width="144" height="192" id="c${i}"></canvas><b>${d.name}</b><p>${d.count}帧 · 96×96</p></article>`).join('');let paused=false,time=0,last=performance.now();window.animState={};document.querySelector('#pause').onclick=e=>{paused=!paused;e.target.textContent=paused?'继续':'暂停'};slider.oninput=()=>{paused=true;document.querySelector('#pause').textContent='继续'};document.querySelector("#replay").onclick=()=>{time=0;paused=false;slider.value=0;document.querySelector("#pause").textContent="暂停"};select.onchange=()=>{time=0;paused=false;document.querySelector("#pause").textContent="暂停";slider.max=data[+select.value].count-1;slider.value=0};
function frame(c,i,f,scale,flip,light){c.imageSmoothingEnabled=false;c.fillStyle=light?'#dae2ea':'#253041';c.fillRect(0,0,c.canvas.width,c.canvas.height);c.save();c.translate(c.canvas.width/2,c.canvas.height*.86);c.scale(flip?-1:1,1);c.drawImage(i,0,f*96,96,96,-48*scale,-82*scale,96*scale,96*scale);c.restore()}
function draw(now){const dt=Math.min(.06,(now-last)/1000);last=now;if(!paused)time+=dt;let k=+select.value,light=document.querySelector('#light').checked,flip=document.querySelector('#flip').checked;let step=Math.floor(time*12),oi=Math.min(step,original.length-1),f=paused?+slider.value:k===0?original[oi].replacement:Math.min(step,data[k].count-1);if(!paused){slider.value=f;if(step>=(k===0?original.length-1:data[k].count-1)){paused=true;document.querySelector("#pause").textContent="继续";}}frame(document.querySelector('#new').getContext('2d'),ims[k],f,3,flip,light);let c=document.querySelector('#native').getContext('2d');c.fillStyle=light?'#dae2ea':'#253041';c.fillRect(0,0,480,320);if(old[oi].complete)c.drawImage(old[oi],80,0,320,320);document.querySelector('#nativeLabel').textContent=original[oi].clip+' · 原版帧 '+original[oi].frame;document.querySelector('#status').textContent=(f+1)+' / '+data[k].count;for(let n=0;n<data.length;n++)if(ims[n].complete)frame(document.querySelector('#c'+n).getContext('2d'),ims[n],Math.min(Math.floor(time*8),data[n].count-1),2,false,light);window.animState={kind:k,frame:f,paused,flip,loaded:ims.every(i=>i.complete&&i.naturalWidth)};requestAnimationFrame(draw)}requestAnimationFrame(draw);
</script></html>'''
html=html.replace('__DATA__',json.dumps(data,ensure_ascii=False)).replace('__ORIGINAL__',json.dumps(original,ensure_ascii=False))
html=html.replace("frame(document.querySelector('#new').getContext('2d'),ims[k],f,3,flip,light);", "if(ims[k].complete&&ims[k].naturalWidth)frame(document.querySelector('#new').getContext('2d'),ims[k],f,3,flip,light);")
html=html.replace("if(old[oi].complete)c.drawImage(old[oi],80,0,320,320);", "if(k===0&&old[oi].complete)c.drawImage(old[oi],80,0,320,320);else if(k!==0){c.fillStyle=light?'#253041':'#d7e1ee';c.font='18px system-ui';c.fillText('此窗口用于魔法吸收原版对照',105,150);c.fillText('其他动作原帧见原版参考目录',105,182);}")
html=html.replace("original[oi].clip+' · 原版帧 '+original[oi].frame", "k===0?original[oi].clip+' · 原版帧 '+original[oi].frame:'其他动作独立预览'")
(root/'动画与原版对照预览.html').write_text(html,encoding='utf-8')
(root/'README.md').write_text(f'''# 泰拉人物特殊动画替换

使用内置 imagegen，参考当前初始人物和本机原版动作，新绘7套64帧。人物保持红棕头发、灰米上衣、浅蓝袖口、米色裤子、棕鞋；固定使用初始外观进行特殊演出，结束后恢复装备外观。

每帧96×96，脚底锚点(48,82)，竖排图集，Point/Nearest采样，27色以内、二值透明。`动画单帧/`含64张PNG，`动画图集/`含7张PNG；`生成清单.json`记录提示词和生成来源，`生成原稿/`保存原图。原版帧与214条目清单在`原版参考/`。

## 新绘动作

|动作|帧数|文件|
|---|---:|---|
'''+''.join(f"|{names[i]}|{j['frames']}|动画图集/{j['id']}.png|\n" for i,j in enumerate(jobs))+f'''
## 原版时序与范围

魔法吸收对应 Collect Magical 1/2/3（含3b）、Fall、Land：抬头、悬浮展开、失力倒下、落地。使用原版各段进度选择新图，原版FPS、事件和FSM均保留；吸收等待阶段固定保持同一姿势，不随原版循环重复展开身体。另覆盖普通能力、水晶之心、酸泳、暗影披风、黑吼、面具碎片获取，以及地面苏醒、聚集、地图、震慑与梦境传送，共{len(mapping)}个原版动画名称。

已有泰拉人物走跑跳、武器战斗、坐姿和死亡流程继续使用现有实现；坐着或走动读图仍沿用现有分层姿态。完整分类在`原版动画审计.json`，不能把214条目理解成214套新画的人物动画。

## 独立获取演出对象

复仇之魂真正显示的是 `_Props/Knight Get Fireball/Knight Cutscene Animator`，原版会隐藏主角并启用此替身；仅改主角库会漏掉它。`Player/HeroCutsceneDouble.cs`通过动画对象启用钩子接管 `Knight Get *` 的人物渲染，保留原动画、状态机、事件、粒子及授予能力流程。已定位的同类对象还包括下砸的 `Knight Get Quake/Knight Cutscene Animator`、帝王之翼的 `Knight Dummy`；后者的Fall/Emerge/Shake/Rise等动作复用相应新姿态。角色隐藏时独立替身姿态可写入联机角色记录。

能力获取场景另做9场景扫描，结果见验证材料。独立特效不换成人体；结局专用对象、完整剧情和所有独立梦境角色仍未逐场证明全部覆盖。

## 代码与联机

代码为 src/TerraKnight/Player/HeroSpecialAnimations.cs、HeroCutsceneDouble.cs、TerraPlayerSprite.cs、TerraMod.cs、Net/NetPlayers.cs。新图集嵌入Resources/heroanim_*.png。使用现有Body字节的负数范围携带动作与帧，网络协议升级为net9，联机双方须使用相同新版。

本轮运行检查的范围与结果另见验证文件；单进程远端角色渲染、数据往返与两台客户端联机是不同证据，不互相替代。运行 `导出素材.ps1` 可重导出并复制资源，运行 tools/build_hero_animation_pack.py 可重建清单与离线预览。
''',encoding='utf-8')
print(f'Packaged 7 sheets, 64 frames, {len(mapping)} native clip mappings and {len(native)} audited entries.')
