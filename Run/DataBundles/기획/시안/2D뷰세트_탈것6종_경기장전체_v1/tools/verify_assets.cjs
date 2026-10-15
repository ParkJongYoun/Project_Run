const fs = require('fs');
const path = require('path');
const zlib = require('zlib');
const crypto = require('crypto');
const root = path.resolve(__dirname, '..');
const mounts = ['tiger','poring','fox','ghost','wolf','boar'];
const sections = ['01_home_straight','02_first_corner','03_back_straight','04_final_corner'];
const views = ['01_side','02_rear','03_top'];
const sha = b => crypto.createHash('sha256').update(b).digest('hex');
function png(file, alpha) {
  const b=fs.readFileSync(file);
  if (b.subarray(0,8).toString('hex') !== '89504e470d0a1a0a') throw Error('Not PNG: '+file);
  const w=b.readUInt32BE(16),h=b.readUInt32BE(20),depth=b[24],type=b[25],interlace=b[28];
  const result={width:w,height:h,bitDepth:depth,colorType:type,bytes:b.length,sha256:sha(b)};
  if (!alpha) return result;
  if(depth!==8||type!==6||interlace!==0) throw Error('Expected RGBA8 noninterlaced: '+file);
  const parts=[];
  for(let p=8;p<b.length;){const len=b.readUInt32BE(p),t=b.toString('ascii',p+4,p+8);if(t==='IDAT')parts.push(b.subarray(p+8,p+8+len));p+=len+12;}
  const raw=zlib.inflateSync(Buffer.concat(parts)),stride=w*4;
  let prev=Buffer.alloc(stride),cur=Buffer.alloc(stride),offset=0,minX=w,minY=h,maxX=-1,maxY=-1,clear=0;
  for(let y=0;y<h;y++){
    const filter=raw[offset++];
    for(let x=0;x<stride;x++){
      const a=x>=4?cur[x-4]:0,bb=prev[x],c=x>=4?prev[x-4]:0;
      let predict=0;
      if(filter===1)predict=a;
      else if(filter===2)predict=bb;
      else if(filter===3)predict=Math.floor((a+bb)/2);
      else if(filter===4){const p=a+bb-c,pa=Math.abs(p-a),pb=Math.abs(p-bb),pc=Math.abs(p-c);predict=pa<=pb&&pa<=pc?a:pb<=pc?bb:c;}
      else if(filter!==0)throw Error('Invalid filter');
      cur[x]=(raw[offset++]+predict)&255;
    }
    for(let x=0;x<w;x++){const a=cur[x*4+3];if(a===0)clear++;if(a>16){minX=Math.min(minX,x);maxX=Math.max(maxX,x);minY=Math.min(minY,y);maxY=Math.max(maxY,y);}}
    [prev,cur]=[cur,prev];
  }
  result.transparentPixels=clear; result.clearFraction=Number((clear/(w*h)).toFixed(4));
  result.bounds={left:minX,top:minY,right:maxX,bottom:maxY};
  result.edgeMargins={left:minX,top:minY,right:w-1-maxX,bottom:h-1-maxY};
  result.touchesEdge=Object.values(result.edgeMargins).some(n=>n===0);
  if(clear===0)throw Error('Missing transparent pixels: '+file);
  return result;
}
const assets=[];
for(const m of mounts)for(const v of views){const file='characters/'+m+'/'+v+'.png';assets.push({file,kind:'character',mount:m,view:v.slice(3),...png(path.join(root,file),true)});}
for(const s of sections)for(const v of views){const file='backgrounds/'+s+'/'+v+'.png';assets.push({file,kind:'background',section:s,view:v.slice(3),...png(path.join(root,file),false)});}
for(let i=0;i<views.length;i++){
 const old=path.resolve(root,'../2D뷰테스트_호랑이_v1/characters/'+views[i].slice(0,3)+'tiger_rider_'+views[i].slice(3)+'.png');
 const item=assets.find(a=>a.mount==='tiger'&&a.view===views[i].slice(3));
 item.originalHashMatches=sha(fs.readFileSync(old))===item.sha256;
 if(!item.originalHashMatches)throw Error('Original tiger mismatch');
}
const warnings=assets.filter(a=>a.touchesEdge).map(a=>a.file+' has content touching edge');
const report={created:new Date().toISOString(),characters:18,backgrounds:12,allFilesPresent:true,originalTigerPreserved:true,warnings,assets};
fs.writeFileSync(path.join(root,'asset_manifest.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({characters:18,backgrounds:12,warnings,characterChecks:assets.filter(a=>a.kind==='character').map(a=>({file:a.file,size:a.width+'x'+a.height,clearFraction:a.clearFraction,margins:a.edgeMargins})),backgroundSizes:[...new Set(assets.filter(a=>a.kind==='background').map(a=>a.width+'x'+a.height))]},null,2));

