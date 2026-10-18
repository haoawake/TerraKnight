/* Regression: later rooms' rock must not obscure earlier passages or facilities. */
const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const rooms = ['early','later'].map(id => ({id,x:0,y:0,w:1,h:1,layer:'world',objects:[],wet:false}));
rooms[0].objects.push({kind:0,x:0,y:0,w:1,h:1});
const nodes = new Map();
let pending = [], pixel, draws = [];
const ctx = {setTransform(){},clearRect(){pixel=null;draws=[];},drawImage(img){draws.push(img.id);if(img.color)pixel=img.color;},fillRect(){pixel=this.fillStyle;}};
const canvas = {getContext:()=>ctx,getBoundingClientRect:()=>({width:300,height:300}),dataset:{},addEventListener(){}};
nodes.set('world-map',canvas);
for (const id of ['map-layer','map-search','map-objects','map-labels','map-bosses','map-position','map-source','map-loading','map-count','map-boss-list','map-detail','map-zoom-in','map-zoom-out','map-reset','map-fullscreen','map-exit-fullscreen'])
  nodes.set(id,{value:id==='map-layer'?'world':'',checked:false,replaceChildren(){}});
class Image {
  set src(url){assert.equal(new URL(url).searchParams.get('v'),'regression');this.id=new URL(url).pathname.split('/').pop().replace('.png','');this.color=({'early':'rock-early','later':'rock-later','early-open':'passage-early'})[this.id];queueMicrotask(()=>this.onload());}
}
vm.runInNewContext(fs.readFileSync(__dirname+'/assets/world-map.js','utf8'),{
  document:{getElementById:id=>nodes.get(id),currentScript:{src:'https://example.test/assets/world-map.js?v=regression'}},
  window:{devicePixelRatio:1},URL,URLSearchParams,Image,
  location:{hash:''},fetch:async url=>{assert.equal(url.searchParams.get('v'),'regression');return {ok:true,json:async()=>({render_mode:'solid-open',rooms,bosses:[],source:{rooms:2,cells:2},note:''})};},
  requestAnimationFrame:fn=>{pending.push(fn);return pending.length;},ResizeObserver:class{observe(){}},
});
setImmediate(()=>{
  for(const draw of pending.splice(0))draw();
  assert.equal(canvas.dataset.ready,'true');
  assert.deepEqual(draws,['early','later','early-open','later-open']);
  assert.equal(pixel,'passage-early','later rock covered earlier passage');
  nodes.get('map-objects').checked=true;
  nodes.get('map-objects').onchange();
  for(const draw of pending.splice(0))draw();
  assert.equal(pixel,'#e8b25c','terrain covered the earlier room facility');
  console.log('PASS: all solids precede all passages; facilities render above every room');
});
