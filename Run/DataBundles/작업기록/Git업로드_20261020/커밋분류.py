import json, pathlib, re, collections, tempfile
ROOT=pathlib.Path('D:/Project_Run')
OUT=ROOT/'Run/DataBundles/작업기록/Git업로드_20261020'
rows=json.loads((OUT/'변경분_목록.json').read_text('utf-8'))
groups=collections.defaultdict(list)
def group(p,s):
    if '/작업기록/Git업로드_20261020/' in p: return '14_업로드기록'
    if s==' D' and p.startswith('Run/DataBundles/'):
        return '07_테스트씬정리' if '/테스트씬_WebGL/' in p or '테스트씬_빌드' in p else '01_이전자료정리'
    if p=='Run/Assets/Screenshots.meta': return '제외_빈스크린샷폴더메타'
    if p.startswith('Run/Assets/ArtBundles/Ranch/') or p.startswith('Run/Assets/Resources/Prefabs/Ranch/') or re.search(r'/(RanchVillageSetup|Data_RanchCatalog|RanchFacilitySpot|Sys_RanchResident)\.cs',p): return '02_목장시설'
    if 'MatyrIndependent' in p or '/IndependentMonsters' in p or '/마타_' in p: return '03_마타독립리깅'
    if '/카툰_에드가_미노_팔달리기_' in p or 'CartoonRunArmBuilder' in p or re.search(r'CartoonMonsters/(Edgar|Minorous)/',p) or re.search(r'P_Runner_Cartoon_(Edgar|Minorous)\.prefab',p): return '05_팔달리기'
    if '/ArtBundles/Characters/' in p or '/ArtBundles/CartoonTest' in p or '/Prefabs/Race/CartoonMonsters/' in p or 'Scene_CartoonTest' in p or re.search(r'(CartoonOriginalComparisonBuilder|CartoonShowcaseBuilder|Sys_CartoonShowcase)',p) or '/Scripts/Test/Cartoon.meta' in p or '/카툰몬스터_' in p or '/카툰탈것' in p or '/카툰테스트_' in p or p.startswith('ProjectSupport/'): return '04_카툰전시'
    if 'CartoonRaceLineup' in p or 'CartoonRaceRoster' in p or 'RanchPopulation' in p or '/카툰경기_출전자구성_' in p or p.endswith('Table_Demo.json') or re.search(r'/(RaceRunnerView|Sys_RacePresentation)\.cs',p) or 'Scene_OvalToon.unity' in p or 'UIPanel_DemoLobby.Arena.cs' in p: return '06_경기출전자'
    if '/Scenes/Test' in p or '/Prefabs/Environment/P_Racecourse_Prontera' in p or '/Prefabs/Race/DemoMonsters' in p or '/Prefabs/UI/Panel/Test' in p or '/Prefabs/UI/Panel/UIPanel_RaceHUD' in p or re.search(r'/(TestSceneMenuBuilder|PlayModeMenuBuilder|PlayModeLandscapeLayout|PlayModeWebBuild|CartoonMonsterPrefabBuilder|MinorousLookDevBuilder|MonsterRaceLookDevBuilder|PronteraAssetSceneryBuilder|PronteraWorldRaceBuilder|PronteraWorldRaceScenery|PronteraWorldRaceValidation|RaceSceneSetBuilder)\.cs',p) or '/Tools/Demo/' in p or '/테스트씬_제거_' in p: return '07_테스트씬정리'
    if 'RanchArena' in p or 'UIItem_ArenaExhibition' in p or '/목장/경기장_' in p or '/RanchCollection/arena_' in p: return '09_목장경기장UI'
    if 'RanchCollection' in p or 'RanchMonsters' in p or 'RanchTraining' in p or '/DataBundles/UI/목장/' in p or '/목장UI_시안점검_' in p: return '08_목장몬스터훈련UI'
    if re.search(r'/(Define_Scene|Mgr_Game|Sys_RanchUI|UIPanel_Ranch|UIPanel_Base|DemoBuilder|DemoPlaythrough|DemoWebBuild|RanchFlowVerification)\.cs',p) or 'Scene_Lobby' in p or p.endswith('/EditorBuildSettings.asset') or p.endswith('/ProjectSettings.asset'): return '10_로비흐름'
    if '/Scripts/Cinemachine/' in p or re.search(r'/(Sys_RaceCamera|Sys_DemoRace|Sys_DemoSkillVideo|UIPanel_DemoRace|DemoRacePrefabBuilder|DemoSkillVideoValidation|HeroComicValidation|MinorousComicValidation|UIItem_ComicBurst|UIItem_ComicPanel|UIItem_DemoHeroComic|UIItem_MinorousComic)(\.|/)',p) or 'UIPanel_DemoRace.prefab' in p or 'Data_DemoArt.asset' in p or '/ArtBundles/UI/Demo/' in p or '/Resources/UI/Demo/' in p or '/StreamingAssets/Video/' in p or '/스킬영상_고화질_' in p or '/로비전환과고화질영상_20261020/이전' in p: return '11_스킬영상'
    if p=='AGENTS.md' or p.endswith('/Project_Run_개발_규칙.md'): return '12_개발기준'
    if '/DataBundles/빌드/' in p or '/로비전환과고화질영상_20261020/' in p: return '13_야간검증웹빌드'
    return '미분류'
for r in rows: groups[group(r['path'],r['status'])].append(r['path'])
plan=[]
temp=pathlib.Path(tempfile.gettempdir())/'project-run-git-20261020'
temp.mkdir(exist_ok=True)
for name,paths in sorted(groups.items()):
    (temp/(name+'.paths')).write_bytes(b'\0'.join(p.encode('utf-8') for p in paths)+b'\0')
    plan.append({'group':name,'count':len(paths),'pathspec':str(temp/(name+'.paths')),'paths':paths})
(OUT/'커밋계획.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps([{'group':x['group'],'count':x['count']} for x in plan],ensure_ascii=False))
print('UNASSIGNED',json.dumps(groups['미분류'],ensure_ascii=False))
patterns={'token':re.compile(r'(?:sk-(?:proj-)?[A-Za-z0-9_-]{24,}|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|AKIA[0-9A-Z]{16})'),'private_key':re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----')}
hits=[]
for r in rows:
    p=ROOT/r['path']
    if p.suffix.lower() not in {'.cs','.py','.ps1','.cmd','.json','.md','.txt','.toml','.yaml','.yml','.env'} or not p.is_file(): continue
    text=p.read_text('utf-8-sig',errors='ignore')
    for kind,pattern in patterns.items():
        for m in pattern.finditer(text): hits.append({'path':r['path'],'kind':kind,'line':text.count('\n',0,m.start())+1})
(OUT/'비밀문자열_점검.json').write_text(json.dumps(hits,ensure_ascii=False,indent=2),encoding='utf-8')
print('SECRET_CANDIDATES',json.dumps(hits,ensure_ascii=False))
