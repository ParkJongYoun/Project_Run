import pathlib,re,subprocess,json,collections
root=pathlib.Path('D:/Project_Run')
path=root/'Run/Assets/Resources/Prefabs/UI/Panel/UIPanel_DemoRace.prefab'
text=path.read_text('utf-8-sig')
pattern=r'<<<<<<< HEAD\n(.*?)=======\n(.*?)>>>>>>> origin/main\n'
assert len(re.findall(pattern,text,re.S))==3
text=re.sub(pattern,lambda m:m.group(2),text,flags=re.S)
remote=subprocess.check_output(['git','show','origin/main:'+path.relative_to(root).as_posix()],cwd=root).decode('utf-8-sig')
def blocks(s):
    return {m.group(2):(m.group(1),m.group(0)) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)\n.*?(?=^--- !u!|\Z)',s,re.S|re.M)}
rb=blocks(remote)
transforms={'993839869779339951'}
while True:
    new={fid for fid,(kind,b) in rb.items() if kind in {'4','224'} and (m:=re.search(r'm_Father: \{fileID: (-?\d+)\}',b)) and m.group(1) in transforms}
    if new<=transforms:break
    transforms |= new
removed=set(transforms)
names=[]
for fid in transforms:
    go=re.search(r'm_GameObject: \{fileID: (-?\d+)\}',rb[fid][1]).group(1)
    removed.add(go)
    body=rb[go][1]
    names.append(re.search(r'm_Name: (.*)',body).group(1))
    removed.update(re.findall(r'- component: \{fileID: (-?\d+)\}',body))
for fid,(_,body) in blocks(text).items():
    if fid in removed:text=text.replace(body,'')
for fid in transforms:
    text=re.sub(r'^  - \{fileID: '+fid+r'\}\n','',text,flags=re.M)
assert 'group_comic' not in text and '<<<<<<<' not in text
assert re.search(r'm_Name: (?:group_standings|row_1)',text)
ids=re.findall(r'^--- !u!\d+ &(-?\d+)',text,re.M)
assert len(ids)==len(set(ids)), 'Duplicate YAML ids'
missing=[]
for line in text.splitlines():
    if 'guid:' in line:continue
    for fid in re.findall(r'\{fileID: (-?\d+)\}',line):
        if fid!='0' and fid not in ids:missing.append(line)
assert not missing, 'Dangling local references: '+repr(missing[:8])
path.write_text(text,encoding='utf-8')
report={'conflicts_resolved':3,'preserved':'원격 순위표·거리 표시·경기 UI 및 로컬 영상 연결','removed_comic_objects':sorted(names),'duplicate_ids':0,'dangling_local_references':0}
(pathlib.Path(__file__).parent/'경기UI_병합검증.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
