"""Read the Excel editing source; emit or verify the runtime skill table."""
from pathlib import Path
import json
import sys
import openpyxl

root = Path(__file__).resolve().parents[2]
book = openpyxl.load_workbook(root / 'DataBundles/테이블/경기스킬_6종.xlsx', data_only=True)
sheet = book['경기스킬']
keys = ['RunnerId', 'MonsterId', 'Name', 'Normal', 'Rare', 'Unique',
        'NormalProgress', 'RareProgress', 'UniqueProgress', 'UniquePower', 'UniqueDuration']
rows = [dict(zip(keys, row)) for row in sheet.iter_rows(min_row=2, max_row=7, max_col=11, values_only=True)]
assert [row['RunnerId'] for row in rows] == list(range(6))
for row in rows:
    assert all(row[key] for key in ['MonsterId', 'Name', 'Normal', 'Rare', 'Unique'])
    assert 0 < row['NormalProgress'] < row['RareProgress'] < row['UniqueProgress'] < .8
    assert 0 < row['UniquePower'] < 1 and row['UniqueDuration'] > 0
data = dict(ComebackChance=sheet['B11'].value, Runners=rows)
assert 0 <= data['ComebackChance'] <= 1
target = root / 'Assets/Resources/Tables/Table_RaceSkills.json'
if '--verify' in sys.argv:
    assert data == json.loads(target.read_text(encoding='utf-8'))
    print('RACE_SKILLS_EXCEL_JSON_IDENTICAL')
else:
    target.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('EXPORTED Table_RaceSkills.json')
