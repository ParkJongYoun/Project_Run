const fs=require('fs'),path=require('path'),vm=require('vm');const root=path.resolve(__dirname,'..');
const html=fs.readFileSync(path.join(root,'preview.html'),'utf8');
const script=html.match(/<script>([\s\S]*?)<\/script>/)[1];new vm.Script(script);
const manifest=JSON.parse(fs.readFileSync(path.join(root,'asset_manifest.json'),'utf8'));
const prompts=JSON.parse(fs.readFileSync(path.join(root,'generation_prompts.json'),'utf8'));
const selected=new Map(prompts.jobs.map(j=>[j.dest,j]));
for(const a of manifest.assets){if(!fs.existsSync(path.join(root,a.file)))throw Error(a.file);}
if(manifest.assets.length!==30||selected.size!==27)throw Error('Unexpected counts');
for(const j of selected.values())if(!fs.existsSync(j.dest))throw Error('Missing selected output '+j.dest);
if(!manifest.assets.filter(a=>a.mount==='tiger').every(a=>a.originalHashMatches))throw Error('Tiger changed');
console.log(JSON.stringify({previewScript:'syntax valid',imageFiles:30,newFinalImages:selected.size,generationRecords:prompts.jobs.length,tigerOriginals:'3 hashes match',totalImageMB:Number((manifest.assets.reduce((n,a)=>n+a.bytes,0)/1048576).toFixed(1))}));

