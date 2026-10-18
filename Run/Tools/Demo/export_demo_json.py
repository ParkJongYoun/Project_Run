"""Export the approved Excel editing source to Unity JSON; no runtime Excel dependency."""
from pathlib import Path
import zipfile, xml.etree.ElementTree as ET, json, sys
root=Path(__file__).resolve().parents[2]
workbook=root/'DataBundles/테이블/심사데모_몬스터와훈련.xlsx'
ns={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
with zipfile.ZipFile(workbook) as z:
    shared=[]
    if 'xl/sharedStrings.xml' in z.namelist():
        shared=[''.join(t.text or '' for t in si.findall('.//s:t',ns)) for si in ET.fromstring(z.read('xl/sharedStrings.xml')).findall('s:si',ns)]
    def sheet(number):
        cells={}
        for c in ET.fromstring(z.read(f'xl/worksheets/sheet{number}.xml')).findall('.//s:c',ns):
            kind=c.get('t');v=c.find('s:v',ns);value=v.text if v is not None else ''
            if kind=='s':value=shared[int(value)]
            elif kind=='inlineStr':value=''.join(t.text or '' for t in c.findall('.//s:t',ns))
            elif value and kind not in ['str','e']:value=float(value);value=int(value) if value.is_integer() else value
            cells[c.get('r')]=value
        return cells
    stats,skills=sheet(1),sheet(2)
config={skills[f'A{r}']:skills[f'B{r}'] for r in range(14,22)}
monsters=[]
for r in range(6,9):
    assert stats[f'A{r}']==skills[f'A{r}'],'Monster identifiers differ between worksheets'
    monsters.append(dict(zip(['Id','Name','Strength','Tactic','TacticIndex','RunnerIndex'],[stats[f'{c}{r}'] for c in 'ABCDEF'])))
    monsters[-1]['Stats']=[stats[f'{c}{r}'] for c in 'GHIJK']
    for key,col in [('Skill','B'),('SkillCondition','C'),('SkillDescription','D'),('Story','E')]:monsters[-1][key]=skills[f'{col}{r}'].replace('\n',' ')
assert len({m['Id'] for m in monsters})==3
assert 0<config['TrainingSeconds']<60 and 0<config['DissolveSeconds']<10
assert config['LobbyMaximumSeconds']>=config['LobbyMinimumSeconds']>0
config['Monsters']=monsters
target=root/'Assets/Resources/Tables/Table_Demo.json'
if '--verify' in sys.argv:
    assert config==json.loads(target.read_text(encoding='utf-8')),'Excel and JSON differ'
    print('EXCEL_JSON_IDENTICAL')
else:
    target.write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('EXPORTED Table_Demo.json')
