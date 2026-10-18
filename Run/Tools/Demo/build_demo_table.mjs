import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {createRequire} from 'node:module';
const bundledRequire=createRequire('C:/Users/ParkJongYoun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/package.json');
const {Workbook,SpreadsheetFile}=await import(pathToFileURL(bundledRequire.resolve('@oai/artifact-tool')).href);
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const data=JSON.parse(await fs.readFile(path.join(root,'Assets/Resources/Tables/Table_Demo.json'),'utf8'));
const report=path.join(root,'DataBundles/검증/심사데모_구현검증_20261018');
await fs.mkdir(report,{recursive:true});
const wb=Workbook.create();
const stats=wb.worksheets.add('몬스터');
const descriptions=wb.worksheets.add('스킬과설정');
for(const sheet of [stats,descriptions]){
  sheet.showGridLines=false;sheet.tabColor='#235B45';
  sheet.getRange(sheet===stats?'A1:L10':'A1:E23').format.font={name:'Arial',size:11,color:'#243B31'};
  sheet.getRange(sheet===stats?'A1:L10':'A1:E23').format.verticalAlignment='center';
  sheet.getRange('A2').values=[[sheet===stats?'심사 데모 · 몬스터 능력치':'심사 데모 · 스킬 설명과 공통 설정']];
  sheet.getRange('A2').format.font={size:16,bold:true};
  sheet.getRange('A3').values=[['출처: 심사데모_플레이콘티.pptx · 2026-10-18 사용자 요청 / 에드가 대체 및 데모용 수치 보완']];
  sheet.getRange('A3').format.font={size:10,color:'#68776E'};
}
stats.getRange('A5:L8').values=[['Id','Name','Strength','Tactic','TacticIndex','RunnerIndex','Speed','Stamina','Power','Intelligence','Spirit','훈련 후 Speed'],...data.Monsters.map(m=>[m.Id,m.Name,m.Strength,m.Tactic,m.TacticIndex,m.RunnerIndex,...m.Stats,null])];
stats.getRange('L6').formulas=[["=G6+'스킬과설정'!$B$16"]];stats.getRange('L6:L8').fillDown();
stats.getRange('A5:L8').format.rowHeight=34;
stats.getRange('A5:C8').format.columnWidth=22;
stats.getRange('D5:F8').format.columnWidth=15;
stats.getRange('G5:L8').format.columnWidth=16;
stats.getRange('E6:L8').setNumberFormat('0');
stats.getRange('L6:L8').format.fill='#E5F0E6';
stats.getRange('A10').values=[['TacticIndex: 0 도주 / 1 선행 / 2 선입 / 3 추입. RunnerIndex는 기존 경기 주자 슬롯입니다.']];
const wrap=value=>value.split(' ').reduce((lines,word)=>{if(lines.at(-1).length+word.length+1>25)lines.push(word);else lines[lines.length-1]+=(lines.at(-1)?' ':'')+word;return lines;},['']).join('\n');
descriptions.getRange('A5:E8').values=[['Id','Skill','SkillCondition','SkillDescription','Story'],...data.Monsters.map(m=>[m.Id,m.Skill,wrap(m.SkillCondition),wrap(m.SkillDescription),wrap(m.Story)])];
descriptions.getRange('A5:A22').format.columnWidth=27;
descriptions.getRange('B5:B22').format.columnWidth=25;
descriptions.getRange('C5:E22').format.columnWidth=50;
descriptions.getRange('A6:E8').format.rowHeight=95;
descriptions.getRange('B6:E8').format.wrapText=true;
const keys=['RequiredLevel','RequiredSpeed','TrainingSpeed','TrainingExperience','TrainingSeconds','LobbyMinimumSeconds','LobbyMaximumSeconds','DissolveSeconds'];
const labels=['출전 최소 레벨','출전 최소 스피드','훈련 스피드 증가','훈련 경험치 증가','훈련 표시 시간(초)','로비 교체 최소 시간(초)','로비 교체 최대 시간(초)','크로스 디졸브 시간(초)'];
descriptions.getRange('A13:C21').values=[['Key','Value','설명'],...keys.map((key,i)=>[key,data[key],labels[i]])];
descriptions.getRange('A13:C21').format.rowHeight=28;
descriptions.getRange('B14:B21').setNumberFormat('0.0');
descriptions.getRange('B14:B21').format.fill='#FFF2CF';
for(const [sheet,range] of [[stats,'A5:L5'],[descriptions,'A5:E5'],[descriptions,'A13:C13']]){
  sheet.getRange(range).format={fill:'#235B45',font:{bold:true,color:'#FFFFFF'},rowHeight:30,horizontalAlignment:'center'};
}
stats.getRange('A6:K8').format.fill='#FFF9E9';
descriptions.getRange('A6:E8').format.fill='#FFF9E9';
// JSON is the runtime table. This workbook remains the editable development source.
wb.recalculate();
console.log((await wb.inspect({kind:'table',range:'몬스터!G5:L8',include:'values,formulas',tableMaxRows:4,tableMaxCols:6,maxChars:1600})).ndjson);
console.log((await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A',options:{useRegex:true,maxResults:10},maxChars:1000})).ndjson);
for(const [sheet,range] of [[stats,'A1:L10'],[descriptions,'A1:E22']]){
  const preview=await wb.render({sheetName:sheet.name,range,scale:1,format:'png'});
  await fs.writeFile(path.join(report,`테이블검수_${sheet.name}.png`),new Uint8Array(await preview.arrayBuffer()));
}
await fs.mkdir(path.join(root,'DataBundles/테이블'),{recursive:true});
await (await SpreadsheetFile.exportXlsx(wb)).save(path.join(root,'DataBundles/테이블/심사데모_몬스터와훈련.xlsx'));
console.log('DEMO_WORKBOOK_EXPORTED');
