const { chromium } = require('C:/Users/ParkJongYoun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
(async()=>{
  const root=path.resolve(__dirname,'..');
  const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
  const page=await browser.newPage({viewport:{width:1280,height:900},deviceScaleFactor:1});
  const results=[];
  for(const [view,name] of [['overview','UI_AB_Comparison.png'],['details','UI_AB_Details.png']]){
    await page.goto(pathToFileURL(path.join(root,'index.html')).href+'?export=1&view='+view);
    await page.evaluate(async()=>{await document.fonts.ready;await Promise.all([...document.images].map(img=>img.decode()));});
    await page.locator('.board').screenshot({path:path.join(root,name)});
    results.push(await page.evaluate(()=>({mode:document.body.className,overflow:document.documentElement.scrollWidth>innerWidth,images:[...document.images].map(img=>({src:img.getAttribute('src'),loaded:img.complete&&img.naturalWidth>0,width:img.naturalWidth,height:img.naturalHeight}))})));
  }
  console.log(JSON.stringify(results,null,2));
  await browser.close();
})();
