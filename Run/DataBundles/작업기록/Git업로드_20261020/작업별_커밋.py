import json, pathlib, subprocess, sys, tempfile
ROOT=pathlib.Path('D:/Project_Run')
OUT=ROOT/'Run/DataBundles/작업기록/Git업로드_20261020'
plan=json.loads((OUT/'커밋계획.json').read_text('utf-8'))
messages={
'01':('사용하지 않는 이전 아트와 작업 부산물 정리','이전 로비 애니메이션과 참고 원본 정리','과거 시안과 검증 부산물 삭제 반영'),
'02':('목장 시설의 입구와 환경 표현 개선','시설 방문 위치와 이동 동작 보정','물 효과와 조경 및 시설 배치 개선'),
'03':('마타 독립 리깅 모델과 달리기 연결 추가','원본과 독립된 모델 및 카툰 프리팹 추가','리깅 비교 원본과 검증 자료 보존'),
'04':('카툰 몬스터 전시와 추가 캐릭터 에셋 구성','몬스터와 탈것의 독립 카툰 리소스 추가','비교 전시와 크기 조정 도구 연결'),
'05':('에드가와 미노타우르스의 팔 달리기 동작 보정','카툰 전용 달리기 클립과 프리팹 갱신','동작 생성 도구와 확인 자료 추가'),
'06':('카툰 경기와 목장의 출전자 구성을 통일','플레이어블 세 종과 무작위 세 종 구성','전시 비율을 경기 전용 프리팹에 반영'),
'07':('사용하지 않는 테스트 씬과 전용 빌드 도구 제거','테스트 전용 코드와 프리팹 정리','게임 빌드 메뉴와 참조 검증 기록 갱신'),
'08':('목장 몬스터와 훈련 화면에 개별 훈련 영상 연결','우드 스타일 UI와 캐릭터별 훈련 화면 구성','훈련 영상 완료 후 성장 지급과 오류 처리 적용'),
'09':('목장 경기장 화면과 출전 준비 UI 구성','경기장 수첩과 주자 미리보기 연결','출전 선택 화면과 관련 제작 자료 추가'),
'10':('정식 진입을 새 목장 로비와 카툰 경기로 연결','암전 중 목장 준비 후 화면 전환','기존 로비 삭제와 진입 흐름 검증 반영'),
'11':('필살기를 한 번의 고화질 영상 연출로 통일','만화 연출 제거와 캐릭터별 7초 영상 연결','첫 프레임 페이드와 질주 카메라 복귀 보정'),
'12':('최신 목장과 경기 및 영상 개발 기준 반영','로비와 플레이어블 구성 기준 갱신','훈련 보상과 단일 스킬 영상 규칙 명시'),
'13':('야간 미션의 웹 빌드와 검증 결과 보존','정식 세 씬의 검증된 WebGL 빌드 갱신','영상 파일 검증과 실제 브라우저 확인 기록 추가'),
}
def run(*args,check=True):
    p=subprocess.run(['git','-c','core.quotePath=false',*args],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    if check and p.returncode: raise RuntimeError(p.stderr.decode('utf-8',errors='replace')[-5000:]+p.stdout.decode('utf-8',errors='replace')[-3000:])
    return p
for number in sys.argv[1:]:
    entry=next(e for e in plan if e['group'].startswith(number+'_'))
    temp=pathlib.Path(tempfile.gettempdir())/'project-run-git-20261020'
    temp.mkdir(exist_ok=True)
    pathspec=temp/(number+'.paths')
    pathspec.write_bytes(b'\0'.join(p.encode('utf-8') for p in entry['paths'])+b'\0')
    existing=set(filter(None,run('diff','--cached','--name-only','-z','--no-renames').stdout.decode('utf-8').split('\0')))
    if existing-set(entry['paths']): raise RuntimeError('Index contains another task before '+number)
    run('add','--pathspec-from-file='+str(pathspec),'--pathspec-file-nul')
    check=run('diff','--cached','--check',check=False)
    (OUT/(entry['group']+'_공백검사.txt')).write_bytes(check.stdout+check.stderr)
    code_check=run('diff','--cached','--check','--','*.cs','*.shader','*.py','*.ps1',check=False)
    if code_check.returncode: raise RuntimeError('Code whitespace check failed for '+number+': '+code_check.stdout.decode('utf-8',errors='replace')[-3000:])
    staged=run('diff','--cached','--name-only','-z','--no-renames').stdout.decode('utf-8').split('\0')
    unexpected=set(filter(None,staged))-set(entry['paths'])
    if unexpected: raise RuntimeError('Unexpected staged paths: '+repr(unexpected))
    title,a,b=messages[number]
    msg=pathlib.Path(tempfile.gettempdir())/'project-run-git-20261020'/('message-'+number+'.txt')
    msg.write_text(title+'\n\n- '+a+'\n- '+b+'\n',encoding='utf-8')
    result=run('commit','-F',str(msg))
    sha=run('rev-parse','HEAD').stdout.decode().strip()
    with (OUT/'작업별_커밋결과.jsonl').open('a',encoding='utf-8') as stream:
        stream.write(json.dumps({'group':entry['group'],'commit':sha,'title':title,'files':len(list(filter(None,staged)))},ensure_ascii=False)+'\n')
    print(sha[:10]+' '+title+' ('+str(len(list(filter(None,staged))))+' files)',flush=True)
