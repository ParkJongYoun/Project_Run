"""Read-only source asset inventory; explicit plan/copy/verify stages for Run."""
import collections, concurrent.futures, csv, hashlib, json, os, re, shutil, sys, time
from pathlib import Path, PurePosixPath

WORK = Path(r'C:\Project_Run\AssetMigration')
SOURCE = Path(r'D:\svn_alldata\Branches\OBT_KOR_1_16\RS_Development')
DEST = Path(r'C:\Project_Run\Run\Assets\OldAsset')
GUID = re.compile(rb'guid:\s*([0-9a-fA-F]{32})')
MODEL = {'.fbx','.obj','.blend','.dae','.3ds','.max','.mb','.ma','.dxf','.c4d','.gltf','.glb','.skp','.mesh'}
ANIMATION = {'.anim','.controller','.overridecontroller','.mask'}
VISUAL = MODEL | ANIMATION | {'.prefab','.mat','.png','.tga','.jpg','.jpeg','.tif','.tiff','.psd','.bmp','.dds','.exr','.hdr','.asset','.shader','.cginc','.hlsl','.glslinc','.rendertexture','.cubemap','.terrainlayer','.flare','.shadervariants','.wav','.ogg','.mp3','.otf','.ttf'}
TEXT = {'.prefab','.mat','.anim','.controller','.overridecontroller','.mask','.asset','.shader','.cginc','.hlsl','.rendertexture','.terrainlayer','.flare','.shadervariants','.mesh'}
HEADER = re.compile(rb'^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n',re.M)

def save(name,obj):
    (WORK/name).write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')

def sanitize(data,ext):
    if ext not in {'.prefab','.controller','.overridecontroller'} or not data.startswith(b'%YAML'):
        return data, []
    matches=list(HEADER.finditer(data)); removed=[]; pieces=[data[:matches[0].start()]] if matches else [data]
    for i,m in enumerate(matches):
        block=data[m.start():matches[i+1].start() if i+1<len(matches) else len(data)]
        if m.group(1)==b'114':
            removed.append(m.group(2).decode());continue
        pieces.append(block)
    data=b''.join(pieces)
    for fileid in removed:
        fid=fileid.encode()
        data=re.sub(rb'^\s*- component: \{fileID: '+fid+rb'\}\r?\n',b'',data,flags=re.M)
        data=re.sub(rb'\{fileID: '+fid+rb'\}',b'{fileID: 0}',data)
    if ext=='.prefab':
        data=re.sub(rb'^(  m_Layer:) (?:[6-9]|[12][0-9]|3[01])\r?$',rb'\1 0',data,flags=re.M)
        data=re.sub(rb'^(  m_TagString:) (?!Untagged\r?$|Respawn\r?$|Finish\r?$|EditorOnly\r?$|MainCamera\r?$|Player\r?$|GameController\r?$)[^\r\n]+',rb'\1 Untagged',data,flags=re.M)
    return data,removed

def destination(p):
    parts=p.split('/'); variant='AndroidVariants' if parts[1]=='ArtBundles(and)' else None
    if '/Character/monster/' in p and 'fellow_falcon' in p:
        companion='fellow_falcon_armor' if 'fellow_falcon_armor' in p else 'fellow_falcon'
        ext=PurePosixPath(p).suffix.lower()
        section='Prefabs' if ext=='.prefab' else ('Animations' if '/animation/' in p else ('Models' if ext in MODEL else ('Materials' if ext=='.mat' else 'Textures')))
        return '/'.join(['Pets','Companions',companion,section]+([variant] if variant else [])+[PurePosixPath(p).name])
    if len(parts)>3 and parts[1] in {'ArtBundles','ArtBundles(and)'}:
        area=parts[2];tail=parts[3:]
        if area=='Character':
            group=tail[0]; rest=tail[1:]
            buckets={'rides':'Mounts','player':'Characters/Players','npc':'Characters/NPCs','monster':'Monsters','items':'Equipment','object':'Props/Interactive','event':'Characters/Events','dummy':'Characters/Dummies','costume_idle_anim':'Characters/CostumeAnimations','res_character':'Shared/Character','[Exclude Updates]':'Extras/ArchivedCharacters'}
            base=buckets.get(group,'Characters/'+group)
        elif area=='Background':
            base='Props/Housing' if tail[0]=='housing' else 'Environment'
            rest=tail[1:] if tail[0]=='housing' else tail
        elif area=='Effect':base='Effects';rest=tail
        elif area=='Shaders':base='Shared/Shaders';rest=tail
        elif area=='Timeline':base='Animations/Cinematics';rest=tail
        elif area=='UI':base='Animations/UI';rest=tail
        else:base='Extras/'+area;rest=tail
        return '/'.join([base]+([variant] if variant else [])+rest)
    if p.startswith('Assets/ArtBuildRes/'):
        return 'Extras/ArtBuildRes/'+p.split('/',2)[2]
    return 'Shared/Dependencies/'+p.removeprefix('Assets/')

def plan():
    rows=json.loads((WORK/'source_inventory.json').read_text('utf-8'))
    inventory={r['path']:r for r in rows}
    metas=[r['path'] for r in rows if r['ext']=='.meta']
    cache=WORK/'guid_index.json'
    if cache.exists(): index=json.loads(cache.read_text('utf-8'))
    else:
        def readmeta(p):
            b=(SOURCE/p).read_bytes();m=GUID.search(b)
            return (m.group(1).decode().lower(),p[:-5]) if m else None
        index={};duplicates=[]
        with concurrent.futures.ThreadPoolExecutor(max_workers=12) as pool:
            for n,result in enumerate(pool.map(readmeta,metas),1):
                if result:
                    g,p=result
                    if g in index and index[g]!=p:duplicates.append([g,index[g],p])
                    else:index[g]=p
                if n%10000==0:print('Indexed metadata',n,'/',len(metas),flush=True)
        save('guid_index.json',index);save('duplicate_source_guids.json',duplicates)
    seeds=set()
    for p,r in inventory.items():
        ext=r['ext']
        if ext in MODEL|ANIMATION:seeds.add(p)
        art=p.startswith(('Assets/ArtBundles/Character/','Assets/ArtBundles/Background/','Assets/ArtBundles(and)/Character/','Assets/ArtBundles(and)/Background/'))
        if art and ext in VISUAL and ext not in {'.exr','.hdr','.wav','.ogg','.mp3'}:
            if '/bake_light/' not in p and '/bake_attrib_grid/' not in p:seeds.add(p)
    selected=set();pending=list(seeds); refs={}; missing=[];excluded=[];stripped={};headers={};shader_includes=[]
    while pending:
        p=pending.pop()
        if p in selected:continue
        selected.add(p);ext=inventory[p]['ext'];data=b''
        if ext in TEXT:
            data=(SOURCE/p).read_bytes()
            if data.startswith(b'%YAML'):
                ids=[m.group(1).decode() for m in HEADER.finditer(data)];headers[p]=sorted(set(ids))
                if ext=='.asset' and set(ids)=={'114'}:
                    selected.remove(p);excluded.append({'source':p,'reason':'legacy ScriptableObject'});continue
            data,removed=sanitize(data,ext)
            if removed:stripped[p]=removed
        meta=SOURCE/(p+'.meta')
        if p+'.meta' in inventory:data+=b'\n'+meta.read_bytes()
        deps=set(x.decode().lower() for x in GUID.findall(data))
        own=[g for g in deps if index.get(g)==p]
        deps.difference_update(own);refs[p]=sorted(deps)
        for g in deps:
            if g.startswith('0000000000000000'):continue
            q=index.get(g)
            if q is None:missing.append({'source':p,'guid':g});continue
            if q not in inventory:continue
            if inventory[q]['ext'] not in VISUAL:
                excluded.append({'source':p,'reference':q,'guid':g,'reason':'nonvisual dependency'});continue
            if q not in selected:pending.append(q)
        if ext in {'.shader','.cginc','.hlsl','.glslinc'}:
            for inc in re.findall(rb'#include\s+["<]([^">]+)[">]',data):
                name=inc.decode('utf-8',errors='replace').replace('\\','/')
                options=[name,str(PurePosixPath(p).parent/name)]
                for q in options:
                    q=os.path.normpath(q).replace('\\','/')
                    if q in inventory:
                        if q not in selected:pending.append(q)
                        shader_includes.append({'source':p,'include':name,'target':q});break
        if len(selected)%5000==0:print('Resolved assets',len(selected),'pending',len(pending),flush=True)
    mapping={p:destination(p) for p in sorted(selected)}
    lower=[d.lower() for d in mapping.values()]
    if len(lower)!=len(set(lower)):raise RuntimeError('Destination collision')
    summary={'assets':len(mapping),'gib':sum(inventory[p]['size'] for p in mapping)/2**30,'extensions':dict(collections.Counter(inventory[p]['ext'] for p in mapping)),'categories':dict(collections.Counter(d.split('/')[0] for d in mapping.values())),'source_models':sum(r['ext'] in MODEL for r in rows),'source_animations':sum(r['ext'] in ANIMATION for r in rows),'removed_script_components':sum(len(v) for v in stripped.values()),'prefabs_cleaned':len(stripped),'missing_reference_count':len(missing),'excluded_dependency_count':len(excluded)}
    save('plan.json',{'source':str(SOURCE),'destination':str(DEST),'mapping':mapping,'references':refs,'missing':missing,'excluded':excluded,'stripped':stripped,'shader_includes':shader_includes,'summary':summary})
    save('serialized_asset_classes.json',headers);print(json.dumps(summary,indent=2),flush=True)

def copy():
    plan=json.loads((WORK/'plan.json').read_text('utf-8'));mapping=plan['mapping']
    if any((DEST/d).exists() for d in mapping.values()):raise RuntimeError('Destination files already exist; refusing overwrite')
    existing={}
    for p in (DEST.parent).rglob('*.meta'):
        m=GUID.search(p.read_bytes())
        if m:existing[m.group(1).decode().lower()]=str(p)
    index=json.loads((WORK/'guid_index.json').read_text('utf-8'))
    collisions=[(g,p,existing[g]) for g,p in index.items() if p in mapping and g in existing]
    if collisions:save('destination_guid_collisions.json',collisions);raise RuntimeError('GUID collision')
    modified=[];manifest=[];start=time.time()
    # Folder metadata is generated by Unity; file GUIDs and subasset IDs are preserved.
    for n,(src,dst) in enumerate(mapping.items(),1):
        a=SOURCE/src;b=DEST/dst;b.parent.mkdir(parents=True,exist_ok=True);ext=a.suffix.lower()
        data=a.read_bytes();original_hash=hashlib.sha256(data).hexdigest();data,removed=sanitize(data,ext)
        changes=[]
        if hashlib.sha256(data).hexdigest()!=original_hash:changes.append('remove_legacy_behaviours_and_normalize_tags_layers');modified.append(src)
        if ext in {'.shader','.cginc','.hlsl','.glslinc'}:
            before_shader=data
            for inc in plan['shader_includes']:
                if inc['source']==src and inc['target'] in mapping:
                    relative=os.path.relpath(DEST/mapping[inc['target']],b.parent).replace('\\','/')
                    data=data.replace(('"'+inc['include']+'"').encode(),('"'+relative+'"').encode())
            data=re.sub(rb'^\s*CustomEditor\s+"[^"]+"',b'',data,flags=re.M)
            if data!=before_shader:changes.append('repair_shader_include_paths_and_remove_legacy_custom_inspector')
        b.write_bytes(data)
        meta=Path(str(a)+'.meta')
        if meta.exists():
            md=meta.read_bytes();md=re.sub(rb'^(\s*assetBundleName:)[^\r\n]*',rb'\1 ',md,flags=re.M);md=re.sub(rb'^(\s*assetBundleVariant:)[^\r\n]*',rb'\1 ',md,flags=re.M)
            Path(str(b)+'.meta').write_bytes(md)
        manifest.append({'source':src,'destination':'Assets/OldAsset/'+dst,'bytes':len(data),'source_sha256':original_hash,'destination_sha256':hashlib.sha256(data).hexdigest(),'changes':';'.join(changes)})
        if n%2000==0:print('Copied',n,'/',len(mapping),'elapsed',round(time.time()-start),'seconds',flush=True)
    with (WORK/'manifest.csv').open('w',encoding='utf-8-sig',newline='') as f:
        w=csv.DictWriter(f,fieldnames=list(manifest[0]));w.writeheader();w.writerows(manifest)
    save('copy_result.json',{'copied':len(manifest),'modified_prefabs':len(modified),'elapsed_seconds':time.time()-start})
    print('Copy completed',len(manifest),flush=True)

if __name__=='__main__':
    {'plan':plan,'copy':copy}[sys.argv[1]]()
