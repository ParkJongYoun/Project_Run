const sharp=require('C:/Users/ParkJongYoun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const {chromium}=require('C:/Users/ParkJongYoun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const path=require('node:path');
const fs=require('node:fs');
(async()=>{
 const root=path.resolve(__dirname,'..');
 const file=path.join(root,'A_Ragnarok_Fantasy_UI_Only.png');
 const meta=await sharp(file).metadata();
 const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let clear=0,semi=0,opaque=0;
 for(let i=3;i<data.length;i+=4){if(data[i]===0)clear++;else if(data[i]===255)opaque++;else semi++;}
 console.log(JSON.stringify({width:meta.width,height:meta.height,hasAlpha:meta.hasAlpha,clear,semi,opaque,centerAlpha:data[((800*info.width)+470)*4+3]}));
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 const page=await browser.newPage({viewport:{width:1000,height:920}});
 const image='data:image/png;base64,'+fs.readFileSync(file).toString('base64');
 await page.setContent('<style>body{margin:0;display:flex;background:#242b36}div{width:50%;background:#e8e8e8}div+div{background:#242b36}img{width:100%;display:block}p{font:14px sans-serif;margin:0;padding:12px;color:#8c8c8c}</style><div><p>Light background</p><img src="'+image+'"></div><div><p>Dark background</p><img src="'+image+'"></div>');
 await page.evaluate(async()=>await Promise.all([...document.images].map(i=>i.decode())));
 await page.screenshot({path:path.join(root,'sources','UI_Only_AlphaCheck.png'),fullPage:true});
 await browser.close();
})();
