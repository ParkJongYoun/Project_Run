from pathlib import Path
import collections,csv,json,re
from migrate_assets import WORK,DEST,save

log=Path(r'C:\Users\GRAVITY\AppData\Local\Unity\Editor\Editor.log').read_text('utf-8',errors='replace')
start=log.find('Start importing Assets/OldAsset/');log=log[start:] if start>=0 else log
pattern=r'Start importing (Assets/OldAsset/.+?) using Guid\([^)]+\) \((\w+Importer)\)(.*?)(?=Start importing |\Z)'
records=list(re.finditer(pattern,log,re.S));warnings=[];completed=collections.defaultdict(set)
for m in records:
    path,importer,body=m.groups()
    if 'artifact id:' in body:completed[importer].add(path)
    for warning in re.finditer(r'ImportFBX (Warnings|Errors):\s*\n(.*?)(?=\nUnityEngine\.|\n ->|\Z)',body,re.S):
        warnings.append({'asset':path,'importer':importer,'kind':warning.group(1),'message':warning.group(2).strip()[:12000]})
    for line in body.splitlines():
        if re.match(r'^(Rig Error:|Shader error in|Error while|Failed to import|Could not import|Cannot import|Importer\(.+?\) generated inconsistent)',line):
            warnings.append({'asset':path,'importer':importer,'kind':'Diagnostic','message':line})
unique={(r['asset'],r['kind'],r['message']):r for r in warnings};warnings=list(unique.values())
with (DEST/'_Catalog'/'UnityImportWarnings.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.DictWriter(f,fieldnames=['asset','importer','kind','message']);w.writeheader();w.writerows(warnings)
summary={'completed_unique_paths_by_importer':{k:len(v) for k,v in completed.items()},'warning_assets':len(set(r['asset'] for r in warnings)),'warning_records':len(warnings),'mount_warning_assets':sorted(set(r['asset'] for r in warnings if '/Mounts/' in r['asset'])),'notes':['Source FBX bytes preserved; Unity may substitute default-pose values for invalid source animation scales.','Import records indicate importer completion, not visual parity with the source Built-in rendering pipeline.']}
save('unity_import_summary.json',summary)
(DEST/'_Catalog'/'UnityImportSummary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(summary,ensure_ascii=False,indent=2))
