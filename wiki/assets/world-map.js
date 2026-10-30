/* The game's cell coordinates, drawn at native resolution with nearest-neighbour zoom. */
(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const canvas = $('world-map');
  if (!canvas) return;
  const ctx = canvas.getContext('2d'), base = new URL('world-map/', document.currentScript.src);
  const version = new URL(document.currentScript.src).searchParams.get('v');
  const asset = name => {const url=new URL(name,base);if(version)url.searchParams.set('v',version);return url;};
  let data, width = 1, height = 1, scale = 1, tx = 0, ty = 0, selected = null;
  let hits = [], frame = 0;
  const images = new Map(), pointers = new Map();
  const objectColors = ['#e8b25c','#fddd03','#a87846','#bebebe','#9696af','#e9cf5e','#c8a0ff','#d2d278','#78e178','#5a96d7','#ebebff','#fff','#a0735a','#78c8eb','#ff96cd','#64ebcd','#ae815c','#ae1845'];
  const visibleRooms = () => data.rooms.filter(r => $('map-layer').value === 'all' || r.layer === $('map-layer').value);
  const visibleBosses = () => data.bosses.filter(b => ($('map-layer').value === 'all' || b.layer === $('map-layer').value) && `${b.name} ${b.en} ${b.scene} ${b.area}`.toLowerCase().includes($('map-search').value.trim().toLowerCase()));
  function schedule() { if (!frame) frame = requestAnimationFrame(draw); }
  function bounds(rooms) {
    return [Math.min(...rooms.map(r=>r.x)), Math.min(...rooms.map(r=>r.y)), Math.max(...rooms.map(r=>r.x+(r.dw||r.w))), Math.max(...rooms.map(r=>r.y+(r.dh||r.h)))];
  }
  function fit() {
    if (!data) return;
    const [x,y,x2,y2] = bounds(visibleRooms());
    scale = Math.min((width-64)/(x2-x),(height-64)/(y2-y));
    tx = width/2-(x+x2)/2*scale; ty = height/2-(y+y2)/2*scale;
    schedule();
  }
  function resize() {
    const box = canvas.getBoundingClientRect(); width = box.width; height = box.height;
    const ratio = window.devicePixelRatio || 1;
    canvas.width = Math.round(width*ratio); canvas.height = Math.round(height*ratio);
    ctx.setTransform(ratio,0,0,ratio,0,0); schedule();
  }
  function zoom(factor,x=width/2,y=height/2) {
    const next = Math.max(.025,Math.min(24,scale*factor));
    tx = x-(x-tx)*next/scale; ty = y-(y-ty)*next/scale; scale = next; schedule();
  }
  function draw() {
    frame = 0; if (!data) return;
    ctx.clearRect(0,0,width,height); ctx.imageSmoothingEnabled = false;
    const rooms = visibleRooms();
    const visible = [];
    for (const room of rooms) {
      // Drawn as the game draws it, stretched (dw, dh); its cells and things scale with it.
      const x=tx+room.x*scale,y=ty+room.y*scale,w=(room.dw||room.w)*scale,h=(room.dh||room.h)*scale;
      if (x+w<0 || y+h<0 || x>width || y>height) continue;
      visible.push({room,x,y,w,h});
      const image=images.get(room.id); if (image) ctx.drawImage(image,x,y,w,h);
    }
    // As in the game, draw ALL rock first, then ALL passages and hazards.
    for (const {room,x,y,w,h} of visible) {
      ctx.drawImage(images.get(room.id+'-open'),x,y,w,h);
      if(room.wet && images.has(room.id+'-wet')) ctx.drawImage(images.get(room.id+'-wet'),x,y,w,h);
    }
    // Facilities and selection stay above terrain from every room.
    for (const {room,x,y,w,h} of visible) {
      if ($('map-objects').checked) for(const o of room.objects) {
        ctx.fillStyle=objectColors[o.kind] || '#f0f';
        const kx=w/room.w,ky=h/room.h;
        ctx.fillRect(x+o.x*kx,y+o.y*ky,Math.max(1,o.w*kx),Math.max(1,o.h*ky));
      }
      if(selected && selected.anchor===room.id) {ctx.strokeStyle='#fff3a6';ctx.lineWidth=2;ctx.strokeRect(x,y,w,h);}
    }
    if ($('map-labels').checked) {
      const groups = new Map(); for(const r of rooms) {if(!groups.has(r.area))groups.set(r.area,[]);groups.get(r.area).push(r);}
      ctx.font='600 13px system-ui';ctx.textAlign='center';ctx.lineWidth=4;
      for(const [name,rs] of groups) {
        const [x,y,x2,y2]=bounds(rs),px=tx+(x+x2)/2*scale,py=ty+(y+y2)/2*scale;
        ctx.strokeStyle='#081016';ctx.strokeText(name,px,py);ctx.fillStyle='#d5e1d5';ctx.fillText(name,px,py);
      }
    }
    hits=[];
    if ($('map-bosses').checked) {
      const shared=new Map();
      for(const b of visibleBosses()) {
        const key=b.anchor,n=shared.get(key)||0; shared.set(key,n+1);
        // Shared-room markers are spread visually; all still resolve to the same room.
        const x=tx+b.x*scale+(n%4)*19,y=ty+b.y*scale-Math.floor(n/4)*19;
        if(x < -20 || y < -20 || x > width+20 || y > height+20)continue;
        ctx.beginPath();ctx.fillStyle=selected===b?'#fff4ab':'#d8a36c';ctx.strokeStyle='#181b20';ctx.lineWidth=2;
        if(b.precision==='entrance'){ctx.moveTo(x,y-8);ctx.lineTo(x+8,y);ctx.lineTo(x,y+8);ctx.lineTo(x-8,y);ctx.closePath();}
        else ctx.arc(x,y,7,0,Math.PI*2);
        ctx.fill();ctx.stroke();hits.push({x,y,b});
        if(scale>.9 || selected===b){ctx.font='12px system-ui';ctx.textAlign='left';ctx.lineWidth=3;ctx.strokeStyle='#0b1418';ctx.strokeText(b.name,x+11,y+4);ctx.fillStyle='#f0e9cf';ctx.fillText(b.name,x+11,y+4);}
      }
    }
    $('map-position').textContent=`${rooms.length} 个房间 · ${Math.round(scale*100)}% · 1 格 = ${scale.toFixed(2)} px`;
  }
  function choose(b, focus=true) {
    selected=b;
    if($('map-layer').value!=='all' && $('map-layer').value!==b.layer) $('map-layer').value=b.layer;
    if(focus) {scale=Math.max(.9,Math.min(2.5,(width-80)/(r=>r.dw||r.w)(data.rooms.find(r=>r.id===b.anchor))));tx=width/2-b.x*scale;ty=height/2-b.y*scale;}
    const detail=$('map-detail');detail.replaceChildren();detail.hidden=false;
    const close=document.createElement('button');close.className='close';close.textContent='×';close.setAttribute('aria-label','关闭 Boss 详情');close.onclick=()=>{selected=null;detail.hidden=true;list();schedule();};detail.append(close);
    const heading=document.createElement('h2');heading.textContent=b.name;detail.append(heading);
    for(const text of [`${b.area} · 阶段 ${b.stage}`,`竞技场：${b.scene}`,b.precision==='entrance'?`入口标记：${b.anchor}。竞技场地形未收录；此处为通往该战斗的入口区域。`:'房间标记：已定位战斗所在房间。']) {const p=document.createElement('p');p.textContent=text;detail.append(p);}
    for(const [href,label] of [[b.url,'查看 Wiki 词条 →'],[b.summon,'查看对应召唤物 →']])if(href){const a=document.createElement('a');a.href=new URL('../../'+href,base);a.textContent=label;detail.append(a);}
    history.replaceState(null,'','#boss='+encodeURIComponent(b.id));list();schedule();
  }
  function list() {
    const items=visibleBosses(),container=$('map-boss-list');container.replaceChildren();
    $('map-count').textContent=`当前图层 ${items.length} / 全部 ${data.bosses.length} 个 Boss`;
    for(const b of items){const button=document.createElement('button');button.type='button';button.dataset.boss=b.id;button.setAttribute('aria-pressed',String(selected===b));button.textContent=b.name;const small=document.createElement('small');small.textContent=b.area+(b.precision==='entrance'?' · 入口':'');button.append(small);button.onclick=()=>choose(b);container.append(button);}
    if(!items.length)container.textContent='未找到匹配的 Boss，可切换为全部图层或修改搜索词。';
  }
  $('map-layer').onchange=()=>{selected=null;$('map-detail').hidden=true;list();fit();};
  $('map-search').oninput=()=>{list();schedule();};
  for(const id of ['map-bosses','map-labels','map-objects'])$(id).onchange=schedule;
  $('map-zoom-in').onclick=()=>zoom(1.4);$('map-zoom-out').onclick=()=>zoom(1/1.4);$('map-reset').onclick=fit;
  $('map-fullscreen').onclick=async()=>{try{if(document.fullscreenElement)await document.exitFullscreen();else await $('atlas-app').requestFullscreen();}catch{$('map-fullscreen').textContent='浏览器暂不支持全屏';}};
  $('map-exit-fullscreen').onclick=()=>document.exitFullscreen();
  canvas.addEventListener('wheel',e=>{e.preventDefault();const r=canvas.getBoundingClientRect();zoom(Math.exp(-e.deltaY*.0015),e.clientX-r.left,e.clientY-r.top);},{passive:false});
  let moved=0;
  canvas.onpointerdown=e=>{canvas.setPointerCapture(e.pointerId);pointers.set(e.pointerId,{x:e.clientX,y:e.clientY});moved=0;};
  canvas.onpointermove=e=>{
    if(!pointers.has(e.pointerId))return;
    const old=pointers.get(e.pointerId),dx=e.clientX-old.x,dy=e.clientY-old.y;moved+=Math.abs(dx)+Math.abs(dy);
    if(pointers.size===2){const other=[...pointers.entries()].find(([id])=>id!==e.pointerId)[1];const before=Math.hypot(old.x-other.x,old.y-other.y),after=Math.hypot(e.clientX-other.x,e.clientY-other.y);const r=canvas.getBoundingClientRect();if(before>1)zoom(after/before,(old.x+other.x)/2-r.left,(old.y+other.y)/2-r.top);tx+=dx/2;ty+=dy/2;}else{tx+=dx;ty+=dy;}
    pointers.set(e.pointerId,{x:e.clientX,y:e.clientY});schedule();
  };
  canvas.onpointerup=e=>{pointers.delete(e.pointerId);if(moved<5){const r=canvas.getBoundingClientRect(),x=e.clientX-r.left,y=e.clientY-r.top;const hit=hits.findLast(h=>Math.hypot(h.x-x,h.y-y)<13);if(hit)choose(hit.b,false);}};
  canvas.onpointercancel=e=>pointers.delete(e.pointerId);
  canvas.onkeydown=e=>{const moves={ArrowLeft:[60,0],ArrowRight:[-60,0],ArrowUp:[0,60],ArrowDown:[0,-60]};if(moves[e.key]){e.preventDefault();tx+=moves[e.key][0];ty+=moves[e.key][1];schedule();}else if(['+','=','-','0'].includes(e.key)){e.preventDefault();if(e.key==='0')fit();else zoom(e.key==='-'?1/1.4:1.4);}};
  new ResizeObserver(resize).observe(canvas);
  async function start() {
    try {
      const response=await fetch(asset('map.json'));if(!response.ok)throw Error('地图数据请求失败');data=await response.json();
      if(data.render_mode!=='solid-open')throw Error('地图版本已更新，请刷新页面');
      await Promise.all(data.rooms.flatMap(r=>[r.id,r.id+'-open',...(r.wet?[r.id+'-wet']:[])]).map(id=>new Promise((resolve,reject)=>{const img=new Image();img.onload=()=>{images.set(id,img);resolve();};img.onerror=()=>reject(Error('房间地形加载失败：'+id));img.src=asset(id+'.png');} )));
      $('map-source').textContent=`${data.source.rooms} 个房间 · ${data.source.cells.toLocaleString()} 个格子。${data.note}`;
      $('map-loading').hidden=true;resize();list();fit();
      const id=new URLSearchParams(location.hash.slice(1)).get('boss');const boss=data.bosses.find(b=>b.id===id);if(boss)choose(boss);
      canvas.dataset.ready='true';
    }catch(error){$('map-loading').textContent='地图载入失败，请刷新重试。'+error.message;canvas.dataset.ready='error';}
  }
  start();
})();
