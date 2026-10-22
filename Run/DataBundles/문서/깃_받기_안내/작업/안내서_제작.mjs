import fs from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { Presentation, PresentationFile } from 'file:///C:/Users/GRAVITY/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';

const workspaceDir = 'C:/Project_MR/Run/DataBundles/문서/깃_받기_안내';
const buildDir = path.join(workspaceDir, '작업');
const SKILL_DIR = 'C:/Users/GRAVITY/.codex/plugins/cache/openai-primary-runtime/presentations/26.1020.11348/skills/presentations';
const RUNTIME_PYTHON = 'C:/Users/GRAVITY/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe';
const FINAL_PPTX = path.join(workspaceDir, '확정/Project_MR_깃_받기_안내.pptx');
const fontPolicy = { basis: 'design', families: ['Malgun Gothic'] };
const presentation = Presentation.create({ slideSize: { width: 1600, height: 900 } });
const c = { ink:'#172532', muted:'#506575', blue:'#1867B2', bg:'#FFFFFF' };
const repo = 'http://172.31.205.175:3000/YT-Studio/project-mr';
const sourceTree = 'https://www.sourcetreeapp.com/';

function text(slide, value, x, y, w, h, size=30, bold=false, color=c.ink, name='') {
  const shape = slide.shapes.add({
    geometry:'textbox', name: name || value.slice(0,40),
    position:{left:x,top:y,width:w,height:h}, fill:'none',
    line:{fill:'none',width:0,style:'solid'}
  });
  shape.text = value;
  shape.text.style = { typeface:'Malgun Gothic',fontSize:size,bold,color,
    autoFit:'none',wrap:'square',insets:0,verticalAlignment:'top' };
  return shape;
}
function link(slide, value, uri, x, y, w, h, size=30) {
  const shape=text(slide,value,x,y,w,h,size,false,c.blue);
  shape.text.get(value).link={uri,isExternal:true};
  return shape;
}
function slide(title, n) {
  const s = presentation.slides.add(); s.background.fill = c.bg;
  text(s,title,64,48,1450,80,49,true);
  text(s,`${n} / 5`,1448,845,88,30,21,false,c.muted);
  return s;
}
async function screenshot(s, file, x, y, w, h, desc) {
  const bytes = await fs.readFile(path.join(buildDir,'원본이미지',file));
  s.images.add({blob:bytes,contentType:'image/png',alt:desc,fit:'contain',
    position:{left:x,top:y,width:w,height:h}});
}

// 1. Installation is the first action; the following pages reproduce the supplied UI.
{
  const s = slide('Project MR Git 받기',1);
  text(s,'01  Sourcetree 설치',64,207,1450,70,44,true,c.blue);
  text(s,'아래 사이트에서 Windows용 설치 파일을 내려받아 설치합니다.',64,319,1450,61,34);
  link(s,sourceTree,sourceTree,64,398,1450,61,34);
  text(s,'설치를 마치면 Sourcetree를 실행합니다.',64,521,1450,60,34);
  text(s,'다음 장부터 첨부 화면의 빨간 화살표를 따라 진행하면 됩니다.',64,672,1450,54,29,false,c.muted);
  text(s,'준비: 사내 Git 저장소에 접근할 수 있는 계정과 네트워크',64,783,1370,43,25,false,c.muted);
  s.speakerNotes.text='Sourcetree 공식 설치 사이트: https://www.sourcetreeapp.com/\n사내 저장소: '+repo+'\n사내 계정으로 해당 비공개 저장소에 접근할 수 있어야 합니다.';
}
// 2. Open the supplied repository page.
{
  const s=slide('02  저장소 사이트 접속',2);
  link(s,repo,repo,64,149,1472,51,30);
  text(s,'위 주소에 접속한 뒤 오른쪽의 [코드] 버튼을 클릭합니다.',64,213,1472,48,31);
  await screenshot(s,'01_저장소.png',64,287,1472,508,'사용자 첨부 1: 사내 project-mr 저장소와 코드 버튼의 빨간 화살표');
  text(s,'로그인 화면이 나오면 사내 Git 계정으로 로그인합니다.',64,830,1330,42,25,false,c.muted);
  s.speakerNotes.text='출처: 사용자 첨부 이미지 1 (저장소 메인 화면).\n저장소 사이트: '+repo;
}
// 3. Copy the URL from the HTTP tab rather than asking users to type it.
{
  const s=slide('03  클론 주소 복사',3);
  text(s,'[HTTP] 탭에서 주소 오른쪽의 복사 아이콘을 클릭합니다.',64,147,1472,51,32);
  await screenshot(s,'02_주소복사.png',64,225,1472,567,'사용자 첨부 2: 코드 메뉴의 HTTP 클론 주소 복사 버튼');
  text(s,'복사한 주소는 잠시 뒤 Sourcetree의 [소스 경로 / URL]에 붙여넣습니다.',64,830,1355,43,25,false,c.muted);
  s.speakerNotes.text='출처: 사용자 첨부 이미지 2 (코드 메뉴의 HTTP 탭과 복사 버튼).\n저장소: '+repo+'\n사이트의 복사 버튼을 사용해 정확한 클론 URL을 가져옵니다.';
}
// 4. Show the full original screenshot, with instructions to its left.
{
  const s=slide('04  Sourcetree에서 Clone 선택',4);
  text(s,'Sourcetree를 실행한 뒤',64,229,730,60,35);
  text(s,'상단의 [+]로 새 탭을 엽니다.',64,287,730,64,35);
  text(s,'[Clone]을 클릭합니다.',64,422,730,66,40,true,c.blue);
  text(s,'새 탭이 이미 열려 있다면\n바로 Clone을 선택하면 됩니다.',64,566,730,108,29,false,c.muted);
  await screenshot(s,'03_소스트리.png',881,162,654,661,'사용자 첨부 3: Sourcetree 새 탭과 Clone 메뉴');
  s.speakerNotes.text='출처: 사용자 첨부 이미지 3 (Sourcetree의 새 탭과 Clone 메뉴).\nSourcetree 공식 클론 안내: https://confluence.atlassian.com/get-started-with-sourcetree/clone-a-remote-repository-847359098.html';
}
// 5. Paste, choose local folder, and start cloning.
{
  const s=slide('05  주소 붙여넣기 후 클론',5);
  await screenshot(s,'04_클론.png',64,168,929,608,'사용자 첨부 4: Sourcetree 소스 URL, 목적지 경로, 이름과 클론 버튼');
  text(s,'소스 경로 / URL',1044,181,492,49,29,true,c.blue);
  text(s,'사이트에서 복사한 주소를\n붙여넣습니다. (Ctrl+V)',1044,239,492,92,29);
  text(s,'목적지 경로',1044,367,492,49,29,true,c.blue);
  text(s,'예: D:\\Project_MR',1044,423,492,49,29);
  text(s,'새 폴더 또는 빈 폴더를 선택합니다.',1044,476,492,67,24,false,c.muted);
  text(s,'이름',1044,566,492,49,29,true,c.blue);
  text(s,'Project_MR',1044,622,492,49,29);
  text(s,'아래 [클론] 버튼 클릭',1044,720,492,52,31,true,c.blue);
  text(s,'인증 창이 나오면 사내 Git 계정으로 로그인하고 다운로드가 끝날 때까지 기다립니다.',64,825,1355,51,25,false,c.muted);
  s.speakerNotes.text='출처: 사용자 첨부 이미지 4 (Sourcetree Clone 입력 화면).\n목적지 경로 D:\\Project_MR는 예시이며 사용 가능한 로컬 경로를 선택합니다.\n이름은 Sourcetree에서 저장소를 구분하는 표시 이름입니다.\n클론을 마치면 지정한 경로에서 프로젝트 파일을 확인합니다.\n이 프로젝트는 Git LFS로 대용량 에셋을 관리하므로 LFS 파일 다운로드까지 완료되어야 합니다. 근거: 프로젝트 루트 .gitattributes.\nSourcetree 공식 안내: https://support.atlassian.com/bitbucket-cloud/docs/clone-a-git-repository/';
}

await fs.mkdir(path.join(buildDir,'미리보기'),{recursive:true});
for (let i=0;i<presentation.slides.items.length;i++) {
  const s=presentation.slides.items[i];
  const stem=String(i+1).padStart(2,'0');
  const png=await presentation.export({slide:s,format:'png',scale:1});
  await fs.writeFile(path.join(buildDir,`미리보기/${stem}.png`),new Uint8Array(await png.arrayBuffer()));
  const layout=await s.export({format:'layout'});
  await fs.writeFile(path.join(buildDir,`미리보기/${stem}.json`),await layout.text());
}
const candidatePath=path.join(buildDir,'검수용.pptx');
await (await PresentationFile.exportPptx(presentation)).save(candidatePath);
const {finalizePresentation}=await import(pathToFileURL(path.join(SKILL_DIR,'container_tools/artifact_tool_utils.mjs')).href);
const result=await finalizePresentation({
  workspaceDir,candidatePath,finalPath:FINAL_PPTX,pythonExecutable:RUNTIME_PYTHON,
  integrityValidatorPath:path.join(SKILL_DIR,'container_tools/inspect_presentation_package_integrity.py'),
  layoutValidatorPath:path.join(SKILL_DIR,'container_tools/inspect_presentation_layout_geometry.py'),
  layoutArgs:['--expected-slide-size-emu','15240000,8572500','--validate-bullet-geometry','--validate-heading-fit'],
  requiredNativeTableOwnerSlides:[],requiredNativeChartOwnerSlides:[],fontPolicy,
  verifyArtifactToolImport:true,receiptPath:path.join(buildDir,'검증결과.json')
});
console.log(JSON.stringify({finalPath:FINAL_PPTX,slides:presentation.slides.items.length,result}));
