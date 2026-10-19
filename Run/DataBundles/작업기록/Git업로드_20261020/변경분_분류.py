import collections, json, pathlib, subprocess
ROOT = pathlib.Path('D:/Project_Run')
OUT = ROOT / 'Run/DataBundles/작업기록/Git업로드_20261020'
def git(*args):
    return subprocess.check_output(['git', '-c', 'core.quotePath=false', *args], cwd=ROOT)
raw = git('status', '--porcelain=v1', '-z', '--untracked-files=all').decode('utf-8')
parts = iter(raw.split('\0'))
rows=[]
for item in parts:
    if not item: continue
    status, path = item[:2], item[3:]
    row={'status': status, 'path': path}
    if status[0] in 'RC' or status[1] in 'RC': row['old_path']=next(parts)
    p=ROOT/path
    if p.is_file(): row.update(bytes=p.stat().st_size, mtime=p.stat().st_mtime)
    rows.append(row)
(OUT/'변경분_목록.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
counts=collections.Counter((r['status'], '/'.join(r['path'].split('/')[:4])) for r in rows)
print(json.dumps([{'status':s,'root':p,'count':n} for (s,p),n in sorted(counts.items(),key=lambda x:x[0][1])],ensure_ascii=False,indent=2))
print('LARGE',json.dumps([{'path':r['path'],'bytes':r['bytes']} for r in rows if r.get('bytes',0)>90_000_000],ensure_ascii=False))
