// Convert generated platform art into native item sprites and Terraria's 27 connection frames.
// Usage: node tools/pack_regional_platforms.cjs artifacts/regional-platforms/generated.json
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const {PNG}=require('C:/Users/luo/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/pngjs');
const root=path.resolve(__dirname,'..'),dir=path.join(root,'artifacts/regional-platforms');
const read=f=>PNG.sync.read(fs.readFileSync(f));
const manifest=JSON.parse(fs.readFileSync(process.argv[2]||path.join(dir,'generated.json')));
const reference=read(path.join(root,'空洞骑士地区家具素材包_v2_差异造型/原版参考/完整原版图集/Tiles_19.png'));
const digest=b=>crypto.createHash('sha256').update(b).digest('hex');
fs.mkdirSync(path.join(dir,'native'),{recursive:true});
fs.mkdirSync(path.join(dir,'originals'),{recursive:true});
const assets=[];
for(const entry of manifest){
 const source=read(entry.file);fs.copyFileSync(entry.file,path.join(dir,'originals',entry.key+'.png'));
 // Ignore isolated almost-transparent edge noise when finding the art bounds.
 let left=source.width,top=source.height,right=0,bottom=0;
 for(let y=0;y<source.height;y++)for(let x=0;x<source.width;x++){
  if(source.data[(y*source.width+x)*4+3]<192)continue;
  left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);
 }
 if(left>right)throw Error('Empty generation '+entry.key);
 const tile=new PNG({width:16,height:16});
 for(let y=0;y<8;y++)for(let x=0;x<16;x++){
  const sx=Math.min(right,Math.floor(left+(x+.5)*(right-left+1)/16));
  const sy=Math.min(bottom,Math.floor(top+(y+.5)*(bottom-top+1)/8));
  const si=(sy*source.width+sx)*4,di=(y*16+x)*4;
  source.data.copy(tile.data,di,si,si+3);tile.data[di+3]=source.data[si+3]>=128?255:0;
 }
 // Continuous deck: align all flat connection edges with the collision surface.
 for(let x=0;x<16;x++)for(let y=0;y<2;y++){
  const di=(y*16+x)*4;if(tile.data[di+3])continue;
  let closest=-1,distance=1e9;
  for(let yy=0;yy<8;yy++)for(let xx=0;xx<16;xx++){
   const si=(yy*16+xx)*4,d=(xx-x)**2+(yy-y)**2;
   if(tile.data[si+3]&&d<distance){closest=si;distance=d;}
  }
  if(closest<0)throw Error('No native pixels '+entry.key);
  tile.data.copy(tile.data,di,closest,closest+4);
 }
 const sheet=new PNG({width:486,height:18});
 for(let f=0;f<27;f++)for(let x=0;x<16;x++){
  let start=-1;for(let y=0;y<16;y++)if(reference.data[(y*486+f*18+x)*4+3]){start=y;break;}
  for(let y=0;y<16;y++){
   const di=(y*486+f*18+x)*4;if(!reference.data[di+3])continue;
   const depth=Math.min(7,y-start),si=(depth*16+x)*4;
   // Flat row frames use the regional underside silhouette; stair/corner/brace masks stay native.
   if(f<=7&&!tile.data[(y*16+x)*4+3])continue;
   let sample=si;
   if(!tile.data[sample+3]){
    let best=1e9;
    for(let yy=0;yy<8;yy++)for(let xx=0;xx<16;xx++){
     const candidate=(yy*16+xx)*4,distance=(xx-x)**2+(yy-depth)**2;
     if(tile.data[candidate+3]&&distance<best){sample=candidate;best=distance;}
    }
   }
   tile.data.copy(sheet.data,di,sample,sample+3);sheet.data[di+3]=255;
  }
 }
 for(const [name,png]of [['HkFurniture_'+entry.key+'_Platform',tile],['HkFurniturePlatform_'+entry.key,sheet]]){
  const bytes=PNG.sync.write(png),target=path.join(root,'src/TerraKnight/Resources/hk_'+name+'.png');
  fs.writeFileSync(target,bytes);fs.writeFileSync(path.join(dir,'native',name+'.png'),bytes);
  assets.push({region:entry.key,file:path.relative(root,target).replaceAll('\\','/'),size:[png.width,png.height],sha256:digest(bytes)});
 }
}
if(manifest.length!==24)throw Error('Expected 24 regions');
fs.writeFileSync(path.join(dir,'assets.json'),JSON.stringify({regions:24,assets},null,2));
console.log(JSON.stringify({regions:24,items:24,sheets:24,frames:648}));
