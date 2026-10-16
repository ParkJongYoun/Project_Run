const { chromium } = require(process.env.CODEX_NODE_MODULES ? process.env.CODEX_NODE_MODULES + '/playwright' : 'C:/Users/ParkJongYoun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const {pathToFileURL}=require('url');
const path=require('path');
const fs=require('fs');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 const page=await browser.newPage({viewport:{width:720,height:1280},deviceScaleFactor:1});
 const errors=[],layouts={referenceResolution:[720,1280],coordinateOrigin:'top-left',screens:{}};page.on('pageerror',e=>errors.push(e.message));
 const base=pathToFileURL(path.join(__dirname,'index.html')).href;
 for(const name of ['prepare','race','result']){
   await page.goto(base+'?capture=1&screen='+name);await page.waitForFunction(()=>document.querySelectorAll('svg').length>20);await page.evaluate(()=>document.fonts.ready);
   await page.screenshot({path:path.join(__dirname,'exports',name+'-720x1280.png')});
   const selectors={prepare:['.prepare-header','.race-ticket','.rider-label','.rider-sheet','.prepare-actions'],race:['.race-header','.race-progress','.skill-notice','.focus-tag','.commentary','.runner-status','.race-controls'],result:['.result-header','.victory-title','.result-sheet','.result-actions']};
   layouts.screens[name]=await page.evaluate(selectors=>selectors.map(selector=>{const r=document.querySelector(selector).getBoundingClientRect(),bottom=/sheet|actions|commentary|runner-status|race-controls/.test(selector);return {selector,x:Math.round(r.x),y:Math.round(r.y),width:Math.round(r.width),height:Math.round(r.height),unity:{anchorMin:bottom?[0,0]:[0,1],anchorMax:bottom?[1,0]:[1,1],pivot:bottom?[0.5,0]:[0.5,1],offsetFromEdge:Math.round(bottom?1280-r.bottom:r.top),note:selector==='.focus-tag'?'Use world tracking and face avoidance; static coordinates are for this art only.':'Group fixed HUD under Safe Area.'}};}),selectors[name]);
   await page.goto(base+'?capture=1&overlay=1&screen='+name);await page.screenshot({path:path.join(__dirname,'exports',name+'-overlay-720x1280.png'),omitBackground:true});
 }
 await page.setViewportSize({width:720,height:1600});await page.goto(base+'?capture=1&screen=race&height=1600&safe=1');await page.screenshot({path:path.join(__dirname,'exports','race-720x1600-safe.png')});
 await page.setViewportSize({width:1440,height:1100});await page.goto(base+'?screen=race');await page.screenshot({path:path.join(__dirname,'exports','interactive-preview.png')});
 const checks=[];
 for(const [width,height] of [[720,1280],[360,640],[390,844],[320,568]]){
  await page.setViewportSize({width,height});await page.goto(base+'?screen=race');
  checks.push(await page.evaluate(()=>({viewport:[innerWidth,innerHeight],overflow:document.documentElement.scrollWidth>innerWidth,controls:[...document.querySelectorAll('.race-controls button')].map(el=>{const r=el.getBoundingClientRect();return {width:Math.round(r.width),height:Math.round(r.height)};})})));
 }
 await page.setViewportSize({width:1440,height:1100});await page.goto(base+'?screen=race');
 await page.locator('[data-action="speed"]').click();checks.push({speed:await page.locator('#speed-label').innerText()});
 await page.locator('[data-action="camera"]').click();checks.push({camera:await page.locator('#camera-label').innerText()});
 await page.locator('[data-action="menu"]').click();await page.locator('#skill-toggle').uncheck();await page.locator('[data-action="save-settings"]').click();checks.push({skillHidden:await page.locator('.skill-notice').isHidden()});
 await page.locator('[data-action="skip"]').first().click();await page.locator('#modal-actions [data-screen="result"]').click();checks.push({skipToResult:await page.locator('#result').isVisible()});
 await page.locator('[data-action="results"]').click();checks.push({resultRows:await page.locator('#modal-content .ranking-row').count()});await page.keyboard.press('Escape');
 await page.locator('#result [data-screen="prepare"]').click();checks.push({confirmToPrepare:await page.locator('#prepare').isVisible()});
 await page.locator('#prepare [data-screen="race"]').click();checks.push({startToRace:await page.locator('#race').isVisible()});
 fs.writeFileSync(path.join(__dirname,'handoff','layout-720x1280.json'),JSON.stringify(layouts,null,2));
 fs.writeFileSync(path.join(__dirname,'handoff','verification.json'),JSON.stringify({errors,checks},null,2));
 console.log(JSON.stringify({errors,checks},null,2));await browser.close();
})().catch(e=>{console.error(e);process.exitCode=1;});
