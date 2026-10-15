"""Verify copied bytes/GUIDs and write browsable catalogs without opening a spreadsheet."""
import collections,concurrent.futures,csv,hashlib,json,re,shutil,zipfile
from pathlib import Path
from migrate_assets import WORK,SOURCE,DEST,GUID,MODEL,ANIMATION,save

plan=json.loads((WORK/'plan.json').read_text('utf-8'))
mapping=plan['mapping'];guid_index=json.loads((WORK/'guid_index.json').read_text('utf-8'))
inverse={p:g for g,p in guid_index.items()}
with (WORK/'manifest.csv').open(encoding='utf-8-sig',newline='') as f:manifest=list(csv.DictReader(f))
def verify(row):
    p=DEST.parent.parent/row['destination'];errors=[]
    if not p.exists():return [row['destination']+': missing file']
    if hashlib.sha256(p.read_bytes()).hexdigest()!=row['destination_sha256']:errors.append(row['destination']+': content mismatch')
    mp=Path(str(p)+'.meta')
    if row['source'] in inverse:
        m=GUID.search(mp.read_bytes()) if mp.exists() else None
        if not m or m.group(1).decode().lower()!=inverse[row['source']]:errors.append(row['destination']+': GUID mismatch')
    return errors
errors=[]
with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
    for n,e in enumerate(pool.map(verify,manifest),1):
        errors.extend(e)
        if n%10000==0:print('Verified',n,'/',len(manifest),flush=True)
allrows=json.loads((WORK/'source_inventory.json').read_text('utf-8'))
missing_models=[r['path'] for r in allrows if r['ext'] in MODEL|ANIMATION and r['path'] not in mapping]
save('verification.json',{'files_checked':len(manifest),'errors':errors,'missing_model_animation_files':missing_models,'guid_preservation':'all matched' if not errors else 'see errors'})
if errors or missing_models:raise RuntimeError('Verification failed; see verification.json')

catalog=DEST/'_Catalog';catalog.mkdir(exist_ok=True)
timelines=[r['path'] for r in allrows if r['ext']=='.playable']
with zipfile.ZipFile(catalog/'LegacyTimelines.zip','w',compression=zipfile.ZIP_DEFLATED) as z:
    for src in timelines:
        z.write(SOURCE/src,src)
        if (SOURCE/(src+'.meta')).exists():z.write(SOURCE/(src+'.meta'),src+'.meta')
shutil.copyfile(WORK/'manifest.csv',catalog/'AssetManifest.csv')
shutil.copyfile(WORK/'verification.json',catalog/'FileVerification.json')
def writecsv(name,rows,fields):
    with (catalog/name).open('w',encoding='utf-8-sig',newline='') as f:
        w=csv.DictWriter(f,fieldnames=fields);w.writeheader();w.writerows(rows)
mounts=[]
groups=sorted(set(d.split('/')[1] for d in mapping.values() if d.startswith('Mounts/') and len(d.split('/'))>=3 and d.split('/')[1] not in {'prefabs','AndroidVariants','riding_poses'}))
for group in groups:
    pairs=[(s,d) for s,d in mapping.items() if d.startswith('Mounts/'+group+'/')]
    models=[d for s,d in pairs if Path(s).suffix.lower() in MODEL and '@' not in Path(s).stem]
    motions=[d for s,d in pairs if Path(s).suffix.lower() in MODEL|{'.anim'} and ('@' in Path(s).stem or '/animation/' in d.lower())]
    model_names={Path(d).stem for d in models}|{group}
    prefabs=[d for d in mapping.values() if d.startswith('Mounts/prefabs/') and Path(d).stem.replace('_pawn','') in model_names]
    mounts.append({'mount':group,'models':' | '.join(models),'prefabs':' | '.join(prefabs),'run_animations':' | '.join(d for d in motions if 'run' in Path(d).stem.lower()),'all_animations':' | '.join(motions),'asset_count':len(pairs)})
writecsv('MountCatalog.csv',mounts,['mount','models','prefabs','run_animations','all_animations','asset_count'])
animations=[]
for src,dst in mapping.items():
    ext=Path(src).suffix.lower()
    if ext in ANIMATION or (ext in MODEL and '@' in Path(src).name):
        animations.append({'category':dst.split('/')[0],'type':ext,'name':Path(src).stem,'path':'Assets/OldAsset/'+dst})
writecsv('AnimationCatalog.csv',animations,['category','type','name','path'])
groups=collections.defaultdict(lambda:collections.Counter())
for s,d in mapping.items():groups[d.split('/')[0]][Path(s).suffix.lower()]+=1
summary={'source':str(SOURCE),'destination':str(DEST),'asset_count':len(mapping),'bytes':sum(int(r['bytes']) for r in manifest),'categories':dict(groups),'mount_groups':len(mounts),'mount_groups_with_run_animation':sum(bool(m['run_animations']) for m in mounts),'standalone_animation_files':sum(Path(s).suffix.lower()=='.anim' for s in mapping),'fbx_files':sum(Path(s).suffix.lower()=='.fbx' for s in mapping),'animator_controllers':sum(Path(s).suffix.lower() in {'.controller','.overridecontroller'} for s in mapping),'prefabs':sum(Path(s).suffix.lower()=='.prefab' for s in mapping),'archived_timeline_sequences':len(timelines)}
save('catalog_summary.json',summary)
(catalog/'Summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
issues={'missing_in_source':plan['missing'],'excluded_nonvisual_dependencies':plan['excluded'],'named_shader_dependencies':plan.get('named_shader_dependencies',[]),'built_in_or_unresolved_named_shaders':plan.get('unresolved_named_shaders',[]),'stripped_script_components':plan['stripped']}
(catalog/'SourceDependencyNotes.json').write_text(json.dumps(issues,ensure_ascii=False,indent=2),encoding='utf-8')
readme=f'''OldAsset - Ragnarok legacy visual asset library
원본: {SOURCE}
대상: {DEST}

FBX {summary['fbx_files']:,}개(모델 및 FBX 내장 애니메이션), .anim {summary['standalone_animation_files']:,}개,
Animator/Override Controller {summary['animator_controllers']:,}개, 프리팹 {summary['prefabs']:,}개.
총 {len(mapping):,}개 에셋 / {summary['bytes']/2**30:.2f} GiB (메타데이터 제외).

폴더 안내
Mounts: 탈것별 모델/재질/텍스처/애니메이션, prefabs에 조립된 프리팹, riding_poses에 탑승 자세.
Characters: Players, NPCs, Events, Dummies, CostumeAnimations.
Monsters: 몬스터 리소스와 프리팹. 원본에 별도 Pet 폴더가 없어 펫 후보도 이곳에서 선택.
Pets/Companions: 원본에서 동료로 확인된 팔콘/장갑 팔콘의 모델, 프리팹, 재질, 텍스처, 애니메이션.
Environment: 지역별 지형, 건물, 식생, 배경 소품. 원본의 지역/세트 내부 배치를 유지.
Props: Housing(하우징 소품), Interactive(상호작용 오브젝트).
Equipment: 무기, 의상, 장신구 등 캐릭터 부속.
Animations: Cinematics 및 UI 등 기타 애니메이션.
Effects: 모델/애니메이션 및 가져온 프리팹에 연결된 이펙트 리소스.
Shared: 공용 재질/셰이더/텍스처 및 참조 리소스.
Extras: 테스트/보관용 모델과 애니메이션. AndroidVariants는 원본 Android 변형본.

찾아보기
_Catalog/MountCatalog.csv: 탈것별 모델, 조립 프리팹, 달리기 및 전체 애니메이션 경로.
_Catalog/AnimationCatalog.csv: .anim, 컨트롤러, @ 이름 FBX 애니메이션 목록.
_Catalog/AssetManifest.csv: 원본/대상 경로, SHA-256, 변경 내역을 담은 전체 파일 목록.
_Catalog/SourceDependencyNotes.json: 원본부터 없었던 참조, 제외한 코드 의존성, 정리한 컴포넌트.
_Catalog/LegacyTimelines.zip: 기존 맵/게임 코드에 의존하는 원본 Timeline 시퀀스 {len(timelines)}개와 메타데이터 보관본.

이관 정책
- 원본 프로젝트는 수정하지 않았습니다.
- 모든 FBX/독립 애니메이션/컨트롤러 파일을 가져왔습니다. FBX 안에 든 클립은 FBX에 유지됩니다.
- 파일 GUID와 importer 설정을 유지하여 모델/재질/텍스처/애니메이션 연결을 보존했습니다.
- 복사본 프리팹/컨트롤러의 기존 MonoBehaviour 게임 코드는 제거했습니다.
- 복사본의 사용자 정의 태그와 6~31 레이어는 기본값으로 정리했습니다.
- 이전 AssetBundle 이름/variant는 복사본에서 해제했습니다.
- 스크립트, DLL, 게임 데이터 테이블, 기존 맵 씬과 라이트베이크 자료는 이관 대상이 아닙니다.
- 셰이더 include 경로를 새 폴더 구조에 맞췄고, 기존 전용 머티리얼 inspector 지정은 제거했습니다.

호환성
원본은 Unity 2022.3 Built-in, 현재는 Unity 6000.3 URP입니다.
원본 셰이더/재질 설정을 보존한 자산 라이브러리이므로 URP에서 원본처럼 보이려면 전용 셰이더를
이식하거나 URP 재질로 변환해야 할 수 있습니다. 일괄 재질 변환과 H5 용량 최적화는 수행하지 않았습니다.
원본 애니메이션 이벤트와 리그 설정은 유지되어 이전 게임 함수 이벤트는 사용 시 정리가 필요할 수 있습니다.
'''
(DEST/'README.txt').write_text(readme,encoding='utf-8-sig')
print(json.dumps(summary,ensure_ascii=False,indent=2),flush=True)
