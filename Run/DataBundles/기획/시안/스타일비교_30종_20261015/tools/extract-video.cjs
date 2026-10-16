const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');
const { chromium } = require('C:/Users/ParkJongYoun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async () => {
  const root = path.resolve(__dirname, '..');
  const out = path.join(root, 'reference_frames');
  fs.mkdirSync(out, {recursive:true});
  const video = 'D:/Project_Run/Run/DataBundles/참고데이터/우마무스메_경기영상.mp4';
  const html = path.join(out, 'video-reader.html');
  fs.writeFileSync(html, '<video id="source" muted preload="auto" src="' + pathToFileURL(video).href + '"></video>');
  const browser = await chromium.launch({headless:true, executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', args:['--allow-file-access-from-files']});
  const page = await browser.newPage();
  await page.goto(pathToFileURL(html).href);
  await page.waitForFunction(() => document.querySelector('video').readyState >= 2);
  const info = await page.evaluate(() => {const v=document.querySelector('video');return {duration:v.duration,width:v.videoWidth,height:v.videoHeight};});
  console.log(JSON.stringify(info));
  const times = [0.12,0.3,0.47,0.63,0.78,0.9].map(x => Math.round(info.duration*x));
  for (const t of times) {
    const data = await page.evaluate(async t => {
      const v=document.querySelector('video');
      await new Promise(resolve => {v.addEventListener('seeked',resolve,{once:true});v.currentTime=t;});
      const c=document.createElement('canvas');c.width=v.videoWidth;c.height=v.videoHeight;
      c.getContext('2d').drawImage(v,0,0);return c.toDataURL('image/png').split(',')[1];
    },t);
    fs.writeFileSync(path.join(out,'frame_'+String(t).padStart(3,'0')+'s.png'), Buffer.from(data,'base64'));
  }
  fs.writeFileSync(path.join(out,'video-metadata.json'),JSON.stringify({...info,times},null,2));
  await browser.close();
})().catch(e=>{console.error(e);process.exitCode=1;});
