const fs=require('fs'),path=require('path'),crypto=require('crypto');
const {PNG}=require('C:/Users/luo/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/pngjs');
const root=path.resolve(__dirname,'..'),bundle=path.join(root,'空洞骑士地区家具素材包_v2_差异造型'),out=path.join(root,'src/TerraKnight/Resources');
const rows=JSON.parse(fs.readFileSync(bundle+'/单图清单.json')),spec=JSON.parse(fs.readFileSync(bundle+'/原版参考/spec.json'));
const lights=new Set(['Candle','Candelabra','Chandelier','Lantern','Lamp']),emissions=[],assets=[];
const read=f=>PNG.sync.read(fs.readFileSync(f)),lum=(r,g,b)=>r*.2126+g*.7152+b*.0722;
function write(name,p){const f=out+'/hk_'+name+'.png';fs.writeFileSync(f,PNG.sync.write(p));assets.push({file:path.relative(root,f).replaceAll('\\','/'),size:[p.width,p.height],sha256:crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex')});}
for(const r of rows){const override=root+'/artifacts/regional-platforms/native/HkFurniture_'+r.region+'_Platform.png';const p=read(r.furniture==='Platform'&&fs.existsSync(override)?override:bundle+'/'+r.file),name='HkFurniture_'+r.region+'_'+r.furniture;write(name,p);
if(lights.has(r.furniture)){
const flame=new PNG({width:p.width,height:p.height}),scores=[];
for(let y=0;y<p.height;y++)for(let x=0;x<p.width;x++){const i=(y*p.width+x)*4;if(!p.data[i+3])continue;if((r.furniture==='Candle'&&y>p.height*.6)||(r.furniture==='Candelabra'&&y>p.height*.6)||(r.furniture==='Chandelier'&&y<p.height*.28))continue;scores.push({i,v:lum(...p.data.subarray(i,i+3))});}
scores.sort((a,b)=>b.v-a.v);const top=scores.slice(0,Math.max(4,Math.ceil(scores.length*.25)));let red=0,green=0,blue=0;
for(const {i} of top){p.data.copy(flame.data,i,i,i+4);red+=p.data[i];green+=p.data[i+1];blue+=p.data[i+2];}
if(!top.length)throw Error('No flame '+name);write(name+'_Flame',flame);
if(r.furniture==='Candle'){const m=Math.max(red,green,blue);emissions.push({region:r.region,rgb:[red/m,green/m,blue/m].map(v=>+v.toFixed(4))});}
}}
const original=read(bundle+'/原版参考/完整原版图集/Tiles_19.png');
for(const [key] of spec.areas){const row=rows.find(r=>r.region===key&&r.furniture==='Platform'),p=read(bundle+'/'+row.file),paint=[];
const override=root+'/artifacts/regional-platforms/native/HkFurniturePlatform_'+key+'.png';if(fs.existsSync(override)){write('HkFurniturePlatform_'+key,read(override));continue;}
for(let i=0;i<p.data.length;i+=4)if(p.data[i+3])paint.push(i);if(!paint.length)throw Error('Empty platform');
const tile=new PNG({width:original.width,height:18});
for(let y=0;y<18;y++)for(let x=0;x<tile.width;x++){const i=(y*tile.width+x)*4;if(!original.data[i+3])continue;const xx=x%18,yy=y;let si=(Math.min(15,yy)*16+Math.min(15,xx))*4;
if(!p.data[si+3]){let best=1e9;for(const pi of paint){const px=pi/4%16,py=Math.floor(pi/4/16),dist=(px-xx)**2+(py-yy)**2;if(dist<best){si=pi;best=dist;}}}
p.data.copy(tile.data,i,si,si+3);tile.data[i+3]=original.data[i+3];}
write('HkFurniturePlatform_'+key,tile);}
const meta={native_sprites:rows.length,flame_layers:120,platform_sheets:24,total:assets.length,emissions,assets};
const artifact=root+'/artifacts/regional-furniture-integration';fs.mkdirSync(artifact,{recursive:true});fs.writeFileSync(artifact+'/assets.json',JSON.stringify(meta,null,2));
let source=fs.readFileSync(root+'/src/TerraKnight/World/RegionalFurniture.cs','utf8');
const marker='        static readonly Dictionary<StationKind, Sprite> sprites';
const colors='        static readonly Color[] lightColors = {\n'+emissions.map(e=>'            new Color('+e.rgb.map(v=>v.toFixed(4)+'f').join(',')+'), // '+e.region).join('\n')+'\n        };\n        public static Color LightColor(int area) => lightColors[area];\n';
if(!source.includes('static readonly Color[] lightColors')){source=source.replace(marker,colors+marker);fs.writeFileSync(root+'/src/TerraKnight/World/RegionalFurniture.cs',source);}
console.log(JSON.stringify({native_sprites:rows.length,flame_layers:120,platform_sheets:24,total:assets.length}));
