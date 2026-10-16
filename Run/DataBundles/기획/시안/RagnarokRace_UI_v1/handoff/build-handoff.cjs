const fs=require('fs'),path=require('path'),{pathToFileURL}=require('url');
const root=path.resolve(__dirname,'..'), packageRoot=process.env.CODEX_NODE_MODULES||'C:/Users/ParkJongYoun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules';
const {chromium}=require(packageRoot+'/playwright');
const spriteDir=path.join(root,'assets','ui');fs.mkdirSync(spriteDir,{recursive:true});
const shapes=[
 {name:'UI_Button_Primary',top:'#347c68',bottom:'#205e50',stroke:'#c8d6a5',radius:18,border:24},
 {name:'UI_Button_Secondary',top:'#fffcf1',bottom:'#e8e6d7',stroke:'#fffbeb',radius:18,border:24},
 {name:'UI_Panel_Ivory',top:'#fffdf5',bottom:'#f1eddd',stroke:'#f8eac2',radius:24,border:30},
 {name:'UI_Panel_Glass',top:'#153d43',bottom:'#245452',stroke:'#b7c4a6',radius:20,border:26,opacity:.91},
 {name:'UI_Skill_Notification',top:'#174e45',bottom:'#2a6852',stroke:'#ddd096',radius:14,border:20},
 {name:'UI_Progress_Track',top:'#6c8c80',bottom:'#6c8c80',stroke:'#a4b7a3',radius:6,border:8},
 {name:'UI_Progress_Fill',top:'#86ad8d',bottom:'#e3cf82',stroke:'#cbdbad',radius:6,border:8}
];
const manifest={referenceResolution:[720,1280],textureType:'Sprite (2D and UI)',spriteMode:'Single',filterMode:'Bilinear',wrapMode:'Clamp',mipMaps:false,alphaIsTransparency:true,pixelsPerUnit:100,canvasReferencePixelsPerUnit:100,note:'These are reusable shape sprites. Keep labels and changing values in TextMeshPro. Screenshots and overlay PNGs are visual reference only.',sprites:[]};
for(const s of shapes){let svg=`<svg xmlns="http://www.w3.org/2000/svg" width="96" height="96" viewBox="0 0 96 96"><defs><linearGradient id="g" x2="0" y2="1"><stop stop-color="${s.top}"/><stop offset="1" stop-color="${s.bottom}"/></linearGradient></defs><rect x="1" y="1" width="94" height="94" rx="${s.radius}" fill="url(#g)" stroke="${s.stroke}" stroke-width="2" opacity="${s.opacity||1}"/><path d="M${s.radius+3} 4H${93-s.radius}" stroke="#fff" opacity=".22"/></svg>`;fs.writeFileSync(path.join(spriteDir,s.name+'.svg'),svg);manifest.sprites.push({file:'assets/ui/'+s.name+'.png',source:'assets/ui/'+s.name+'.svg',border:[s.border,s.border,s.border,s.border],imageType:'Sliced',size:[96,96]});}
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});const page=await browser.newPage({viewport:{width:96,height:96},deviceScaleFactor:1});
 for(const s of shapes){await page.goto(pathToFileURL(path.join(spriteDir,s.name+'.svg')).href);await page.screenshot({path:path.join(spriteDir,s.name+'.png'),omitBackground:true});}
 await page.goto(pathToFileURL(path.join(root,'index.html')).href+'?capture=1');
 const iconData=await page.evaluate(()=>Object.fromEntries(Object.entries(iconPaths).map(([name,def])=>[name,def.split('|').map(p=>{if(p.startsWith('circle:')){const [cx,cy,r]=p.slice(7).split(',');return `<circle cx="${cx}" cy="${cy}" r="${r}"/>`;}return `<path d="${p}"/>`;}).join('')])));
 await page.setViewportSize({width:64,height:64});
 for(const [name,content] of Object.entries(iconData)){const filename='UI_Icon_'+name;const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="#ffffff" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${content}</svg>`;fs.writeFileSync(path.join(spriteDir,filename+'.svg'),svg);await page.goto(pathToFileURL(path.join(spriteDir,filename+'.svg')).href);await page.screenshot({path:path.join(spriteDir,filename+'.png'),omitBackground:true});manifest.sprites.push({file:'assets/ui/'+filename+'.png',source:'assets/ui/'+filename+'.svg',border:[0,0,0,0],imageType:'Simple',size:[64,64],tintable:true});}
 fs.writeFileSync(path.join(__dirname,'UI_SpriteManifest.json'),JSON.stringify(manifest,null,2));
 await page.setViewportSize({width:1680,height:1220});await page.goto(pathToFileURL(path.join(root,'board.html')).href);await page.screenshot({path:path.join(root,'exports','UI_Overview.png')});
 await browser.close();console.log('Exported '+manifest.sprites.length+' reusable sprites and overview.');
})().catch(e=>{console.error(e);process.exitCode=1;});
