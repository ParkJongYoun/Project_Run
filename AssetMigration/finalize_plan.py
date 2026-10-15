"""Resolve shader references encoded by name (UsePass/Fallback), not GUID."""
import collections,json,os,re
from pathlib import Path,PurePosixPath
from migrate_assets import WORK,SOURCE,DEST,GUID,TEXT,MODEL,ANIMATION,VISUAL,destination,save,sanitize

p=json.loads((WORK/'plan.json').read_text('utf-8'))
rows=json.loads((WORK/'source_inventory.json').read_text('utf-8'));inv={r['path']:r for r in rows}
idx=json.loads((WORK/'guid_index.json').read_text('utf-8'))
shaders={};cache={}
for r in rows:
    if r['ext']=='.shader':
        data=(SOURCE/r['path']).read_bytes();cache[r['path']]=data
        name=re.search(rb'^\s*Shader\s+"([^"]+)"',data,re.M)
        if name:shaders[name.group(1).decode()]=r['path']
pending=[s for s in p['mapping'] if Path(s).suffix.lower() in {'.shader','.cginc','.hlsl','.glslinc'}]
visited=set();named=[];unresolved=[]
while pending:
    src=pending.pop()
    if src in visited:continue
    visited.add(src);data=cache.get(src)
    if data is None:data=(SOURCE/src).read_bytes()
    for raw in re.findall(rb'UsePass\s+"([^"]+)"',data)+re.findall(rb'Fallback\s+"([^"]+)"',data,re.I):
        name=raw.decode();name=name.rsplit('/',1)[0] if re.search(rb'UsePass\s+"'+re.escape(raw)+rb'"',data) else name
        dep=shaders.get(name)
        if dep:
            p['mapping'].setdefault(dep,destination(dep));pending.append(dep);named.append({'source':src,'shader_name':name,'target':dep})
        else:unresolved.append({'source':src,'name':name})
    for raw in re.findall(rb'#include(?:_with_pragmas)?\s+["<]([^">]+)[">]',data):
        name=raw.decode().replace('\\','/')
        for q in [name,str(PurePosixPath(src).parent/name)]:
            q=os.path.normpath(q).replace('\\','/')
            if q in inv:
                p['mapping'].setdefault(q,destination(q));pending.append(q)
                item={'source':src,'include':name,'target':q}
                if item not in p['shader_includes']:p['shader_includes'].append(item)
                break
    deps=set(x.decode().lower() for x in GUID.findall(data))
    p['references'][src]=sorted(deps)
    for g in deps:
        q=idx.get(g)
        if q in inv and inv[q]['ext'] in VISUAL and q not in p['mapping']:
            p['mapping'][q]=destination(q);pending.append(q)
p['mapping']={s:d.replace('/Resources/','/ResourceAssets/') for s,d in sorted(p['mapping'].items())}
p['named_shader_dependencies']=named;p['unresolved_named_shaders']=unresolved
p['summary']['assets']=len(p['mapping']);p['summary']['gib']=sum(inv[s]['size'] for s in p['mapping'])/2**30
p['summary']['extensions']=dict(collections.Counter(inv[s]['ext'] for s in p['mapping']))
p['summary']['categories']=dict(collections.Counter(d.split('/')[0] for d in p['mapping'].values()))
save('plan.json',p)
print(json.dumps({'summary':p['summary'],'named_shader_dependencies':len(named),'built_in_or_unresolved_shader_names':unresolved,'missing_reference_sample':p['missing'][:15],'excluded_sample':p['excluded'][:10]},indent=2),flush=True)
