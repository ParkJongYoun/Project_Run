from pathlib import Path
import collections,json,re
p=Path(r'C:\Users\GRAVITY\AppData\Local\Unity\Editor\Editor.log')
s=p.read_text('utf-8',errors='replace');i=s.find('Start importing Assets/OldAsset/');s=s[i:] if i>=0 else s
lines=s.splitlines()
records=list(re.finditer(r'Start importing (Assets/OldAsset/.+?) using Guid\([^)]+\) \((\w+Importer)\)(.*?)(?=Start importing |\Z)',s,re.S))
done=[m for m in records if 'artifact id:' in m.group(3)]
problems=[x for x in lines if re.search(r'error |error:|failed|Exception|Could not|Cannot ',x,re.I) and not any(y in x for y in ['MCP','ping','WebSocket','SetException','TrySetException'])]
print(json.dumps({'completed_import_entries':len(done),'importers':dict(collections.Counter(m.group(2) for m in done)),'problem_lines':len(problems),'problem_sample':list(dict.fromkeys(problems))[:10],'recent':lines[-3:]},ensure_ascii=False,indent=2))
