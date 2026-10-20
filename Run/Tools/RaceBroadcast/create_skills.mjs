import fs from 'node:fs/promises';
import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';
const require = createRequire('C:/Users/GRAVITY/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/');
const { Workbook, SpreadsheetFile } = await import(pathToFileURL(require.resolve('@oai/artifact-tool')).href);

const root = 'C:/Project_Run/Run';
await fs.mkdir(`${root}/DataBundles/테이블`,{recursive:true});
const workbook = Workbook.create();
const sheet = workbook.worksheets.add('경기스킬');
const rows = [
  [0,'Poring','포링','통통 스텝','힐','울지 마, 포링!',.10,.25,.60,.11,9],
  [1,'Minorous','미노타우르스','육중한 발걸음','파워 업','대열 돌파',.12,.28,.63,.15,8],
  [2,'Pecopeco','페코페코','깃털 스텝','바람 가르기','질주 본능',.14,.31,.65,.17,7],
  [3,'Lunatic','루나틱','토끼뜀','달빛 가속','만월의 도약',.16,.34,.67,.18,8],
  [4,'BaphometJr','바포메트 주니어','그림자 걸음','다크 스트라이크','작은 마왕의 진격',.18,.37,.69,.20,7],
  [5,'Alpaca','알파카','폭신한 보폭','고원의 숨결','구름길의 기적',.20,.40,.71,.16,9],
];
sheet.getRange('A1:K7').values = [
  ['RunnerId','MonsterId','이름','노말','레어','유니크','노말 시작률','레어 시작률','유니크 시작률','유니크 속도비','지속(초)'],
  ...rows
];
sheet.getRange('A1:K7').format.font = {name:'Arial',size:11,color:'#18304A'};
sheet.getRange('A1:K1').format = {fill:'#133659',font:{bold:true,color:'#FFFFFF'},rowHeight:30};
sheet.getRange('A2:K7').format.rowHeight=28;
sheet.getRange('A1:K7').format.columnWidth=18;
sheet.getRange('C1:F7').format.columnWidth=24;
sheet.getRange('D2:D7').format.fill='#E2F1FC';
sheet.getRange('E2:E7').format.fill='#EEE3FC';
sheet.getRange('F2:F7').format.fill='#FFF0BE';
sheet.getRange('G2:J7').setNumberFormat('0%');
sheet.getRange('A10:B11').values=[['설정','값'],['역전 보정 추첨 확률',.82]];
sheet.getRange('B11').setNumberFormat('0%');
sheet.getRange('A14:K14').merge();
sheet.getRange('A14').values=[['노말 3회(시작률 + 0.28씩), 레어 2회(시작률 + 0.36), 유니크 1회. 노말·레어는 UI만 표시.']];
sheet.getRange('A15:K15').merge();
sheet.getRange('A15').values=[['선택 캐릭터의 유니크는 60% 이후 1회 영상. 역전 보정은 유니크 발동 이후 후반 가속에만 적용.']];
sheet.getRange('A16:K16').merge();
sheet.getRange('A16').values=[['힐·파워 업·다크 스트라이크는 라그나로크 스킬명 참고. 종별 배정·나머지 이름·효과는 데모용 창작.']];
sheet.getRange('A17:K17').merge();
sheet.getRange('A17').values=[['출처: https://ro.gnjoy.com/guide/runemidgarts/popup/monsterview.asp?monsterID=BAPHOMET']];
sheet.showGridLines=false;
workbook.recalculate();
console.log((await workbook.inspect({kind:'table',range:'경기스킬!A1:K7',include:'values',tableMaxRows:7,tableMaxCols:11})).ndjson);
const preview=await workbook.render({sheetName:'경기스킬',range:'A1:K17',scale:1,format:'png'});
await fs.writeFile(`${root}/DataBundles/검증/경기스킬과HUD_20261020/스킬표_미리보기.png`,new Uint8Array(await preview.arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(workbook)).save(`${root}/DataBundles/테이블/경기스킬_6종.xlsx`);
