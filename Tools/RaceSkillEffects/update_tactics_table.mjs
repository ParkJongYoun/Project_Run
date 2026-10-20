import fs from "node:fs/promises";
import { createRequire } from "node:module";
import { pathToFileURL } from "node:url";
const runtimeRequire = createRequire("C:/Users/ParkJongYoun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/package.json");
const { FileBlob, SpreadsheetFile } = await import(pathToFileURL(runtimeRequire.resolve("@oai/artifact-tool")).href);
const root = "D:/Project_Run/Run";
const record = root + "/DataBundles/작업기록/경기스킬분산과_추월AI_20261021";
const path = root + "/DataBundles/테이블/경기스킬_6종.xlsx";
await fs.mkdir(record,{recursive:true});
const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(path));
const sheet = workbook.worksheets.getItemAt(0);
if (process.argv.includes("--inspect")) {
  console.log((await workbook.inspect({kind:"region",sheetId:sheet.name,range:"A1:K17",maxChars:3500})).ndjson);
  const image = await workbook.render({sheetName:sheet.name,range:"A1:K17",scale:1});
  await fs.writeFile(record+"/스킬표_변경전.png",new Uint8Array(await image.arrayBuffer()));
} else {
  try { await fs.copyFile(path,record+"/변경전_경기스킬_6종.xlsx",fs.constants.COPYFILE_EXCL); } catch(e) { if(e.code!=="EEXIST")throw e; }
  const progress=[.65,.40,.76,.14,.52,.27];
  const power=[.11,.15,.34,.18,.20,.16];
  const duration=[9,8,13.5,8,7,9];
  const intent=["중앙","중앙","외곽","외곽","외곽","중앙"];
  sheet.getRange("G1:H1").values=[["노말 횟수","레어 최소횟수"]];
  sheet.getRange("G2:H7").values=Array.from({length:6},()=>[2,1]);
  sheet.getRange("I2:K7").values=progress.map((p,i)=>[p,power[i],duration[i]]);
  for(const col of ["L","M","N","O","P","Q"])sheet.getRange(`${col}1:${col}7`).copyFrom(sheet.getRange("K1:K7"),"all");
  sheet.getRange("L1:Q1").values=[["레어 최대횟수","노말 속도율","노말 지속(초)","레어 속도율","레어 지속(초)","레어 진로"]];
  sheet.getRange("L2:Q7").values=intent.map(x=>[2,.012,2,.025,3.2,x]);
  sheet.getRange("G2:H7").setNumberFormat("0");
  sheet.getRange("L2:L7").setNumberFormat("0");
  sheet.getRange("M2:M7").setNumberFormat("0.0%");
  sheet.getRange("O2:O7").setNumberFormat("0.0%");
  sheet.getRange("N2:N7").setNumberFormat("0.0");
  sheet.getRange("P2:P7").setNumberFormat("0.0");
  sheet.getRange("G1:H7").format.columnWidth=18;
  sheet.getRange("L1:Q7").format.columnWidth=18;
  sheet.getRange("A14").values=[["노말 2회, 레어 1~2회 랜덤. 경기 시작 때 순서를 추첨하고 전체 발동 사이 1.6초 이상 간격을 둔다."]];
  sheet.getRange("A15").values=[["유니크는 종별 초·중·후반에 1회. 선택 주자 영상도 종별 시점에 재생하며 이펙트는 영상 복귀 후 시작한다."]];
  sheet.getRange("A16").values=[["노말은 미세 가속, 레어는 미세 가속과 진로 확보. 페코페코는 후반 외곽 추월과 강하고 긴 스퍼트를 사용한다."]];
  workbook.recalculate();
  console.log((await workbook.inspect({kind:"match",searchTerm:"#REF!|#DIV/0!|#VALUE!|#NAME\\?",options:{useRegex:true,maxResults:20},maxChars:1000})).ndjson);
  const preview = await workbook.render({sheetName:sheet.name,range:"G1:Q7",scale:1.5});
  await fs.writeFile(record+"/스킬표_변경후.png",new Uint8Array(await preview.arrayBuffer()));
  const exported=await SpreadsheetFile.exportXlsx(workbook);
  await exported.save(path);
  const rows=sheet.getRange("A2:Q7").values;
  const data={ComebackChance:Number(sheet.getRange("B11").values[0][0]),Runners:rows.map(r=>({
    RunnerId:Number(r[0]),MonsterId:r[1],Name:r[2],Normal:r[3],Rare:r[4],Unique:r[5],
    NormalCount:r[6],RareCountMin:r[7],RareCountMax:r[11],
    NormalPower:r[12],NormalDuration:r[13],RarePower:r[14],RareDuration:r[15],
    RareLaneIntent:r[16]==="외곽"?2:1,UniqueProgress:r[8],UniquePower:r[9],UniqueDuration:r[10]
  }))};
  await fs.writeFile(root+"/Assets/Resources/Tables/Table_RaceSkills.json",JSON.stringify(data,null,2)+"\n");
  console.log(JSON.stringify({updated:path,runners:data.Runners.length}));
}
