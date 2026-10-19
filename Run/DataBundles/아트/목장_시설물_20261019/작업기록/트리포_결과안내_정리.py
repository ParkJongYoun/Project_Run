import json
from pathlib import Path

base = Path(__file__).resolve().parent.parent
records = base / '작업기록' / '트리포_적용'
plan = json.loads((base / '작업기록' / '트리포_모델생성_계획.json').read_text(encoding='utf-8-sig'))
lines = [
    'Tripo 목장 시설물 생성 및 Unity 적용 결과 · 2026-10-19', '',
    '결과: 입력 원화 11장으로 11종 FBX 생성 성공. Scene_LobbyTest의 배치 프리팹과 외곽 환경에 적용.',
    '크레딧: 시작 1,000 / 사용 605(11종 × 55) / 잔여 395 / 예약 0.',
    '연결: 등록된 Tripo MCP의 tripo-cli 0.7.0 실행 파일과 기존 인증 사용. Meshy는 사용하지 않음.',
    '생성 옵션: v3.1-20260211, smart_low_poly=true(깔끔한 토폴로지), quad=true, texture=true, pbr=true, texture_quality=detailed.',
    '원본은 사각면 중심이며 삼각면도 포함한다. Unity에서는 삼각형으로 렌더링한다.',
    'face_limit는 요청 목표값이고 실제 결과가 이를 초과했다. 아래는 다운로드한 FBX의 실측값이다.', '',
    '이름 | ID | 요청 면 목표 | 원본 면 | 사각면 | 원본 정점 | Unity 삼각형',
]
for asset in plan['assets']:
    report = json.loads((records / (asset['id'] + '_원본토폴로지.json')).read_text(encoding='utf-8-sig'))
    name = Path(asset['source']).stem[3:].replace('_', ' ')
    vertices = sum(o['vertices'] for o in report['objects'])
    lines.append(f"{name} | {asset['id']} | {asset['face_limit']:,} | {report['total_faces']:,} | {report['total_quads']:,} | {vertices:,} | {report['total_render_triangles']:,}")
lines += [
    '', '위 삼각형 수는 모델 본체 기준이다. 축사·훈련장 바닥과 가로등 기둥 보조 메시가 각각 12개 삼각형을 추가한다.',
    'Unity 정점 수는 UV 경계와 노멀 분리로 원본 정점 수보다 많을 수 있으며, 작업기록/트리포_적용/<id>_Unity검증.json에 따로 기록했다.',
    '', '적용 및 보정',
    '- 원본 다운로드 FBX는 트리포_생성모델에 보존했다. Unity용 복사본만 보정했다.',
    '- 11종의 면 방향을 검사하고 뒤집힌 면이 있는 8종은 연결된 면 방향을 정리했다. 정점 수·면 수·UV를 유지했다.',
    '- 특히 훈련장의 뒤집힌 면 2,952개를 보정했다. 최종 재질은 일반 단면 렌더링을 사용한다.',
    '- 생성 모델의 열린 바닥은 축사/훈련장 보조 바닥으로 받쳤고, 가로등의 끊긴 부분에는 작은 목재 보조 메시를 추가했다.',
    '- AI 원본의 모든 구멍을 봉합한 밀폐형 메시가 아니며, 출입문 중앙 목재 끝처럼 작은 열린 부분은 남아 있다.',
    '- URP Lit 전용 재질과 알베도, 최대 2,048 텍스처와 밉맵을 연결했다. 원본 PBR 맵은 보존했다.',
    '- 바닥 중심 피벗, 루트 스케일 1, 배치 칸수 안의 크기와 지붕/수관 선택 Collider를 맞췄다.',
    '- 외곽 숲·바위·울타리도 새 모델로 교체했다. 원본 카툰 몬스터와 정식 Scene_Lobby는 변경하지 않았다.',
    '', '검증 범위',
    '- 11종 재질·UV·피벗·크기·Collider 연결 검사 통과.',
    '- 그리드/에셋 검사 63개 및 Play 배치/회전/이동/취소/회수 등 검사 18개 통과.',
    '- Unity Editor에서 전체 UI와 건설 미리보기, 주요 모델 확대 화면을 확인했다.',
    '- 전체 정적 메시 합계 967,392 삼각형: 화면 밖 숲 포함, 스킨 몬스터 제외. 실제 화면 렌더링량과 다르다.',
    '- WebGL 빌드와 모바일 실기기 성능 측정은 하지 않았다. 반복 나무의 LOD 등 최적화는 별도 검토가 필요하다.',
    '', '파일 위치',
    '원화: 트리포_입력원화/<시설명>.png',
    '다운로드 원본: 트리포_생성모델/<시설명>/model.fbx',
    '게임용 FBX와 재질: C:/Project_Run/Run/Assets/ArtBundles/Ranch/TripoModels/<id>/',
    '배치 프리팹: C:/Project_Run/Run/Assets/Resources/Prefabs/Ranch/P_Ranch_<id>.prefab',
    '대상 씬: C:/Project_Run/Run/Assets/Scenes/Scene_LobbyTest.unity',
    '면 방향 보정본과 검사 기록: 작업기록/트리포_적용/',
    '전체 화면: 검증/03_Tripo_모델적용_전체.png',
    '배치 화면: 검증/04_Tripo_모델적용_배치.png',
    '', '사용: Scene_LobbyTest를 열고 Play → 건설 → 시설 선택 → 드래그/회전 → 배치.',
    '이번 버전은 시설 배치 시제품이다. 확장 구역은 표시만 하며 해금/재화 소모/생산·성장 시스템은 연결하지 않았다.',
]
(base / '트리포_생성결과와_적용안내.txt').write_text('\n'.join(lines) + '\n', encoding='utf-8-sig')

guide_path = base / '시설물_목록과_사용안내.txt'
guide = guide_path.read_text(encoding='utf-8-sig')
guide = guide.replace('- 실제 모델을 받은 먹이 창고는 FBX와 외부 텍스처를 연결했다. 나머지 10종은 배치 검증을 위한 임시 3D 형상이며 아래 원화로 만든 최종 모델로 교체할 수 있다.',
    '- 2026-10-19 Tripo 생성 완료: 원화 11장 모두 새 FBX로 생성하여 배치 프리팹, 숲, 외곽 울타리에 적용했다. 605크레딧 사용, 395크레딧 잔여. 상세 수치와 보정 내용은 트리포_생성결과와_적용안내.txt 참조.')
guide = guide.replace('새 원화 + 임시 3D.', 'Tripo 생성 FBX 적용.')
guide = guide.replace('사용자가 제공한 원화와 Tripo FBX 적용.', '제공 원화로 새 Tripo FBX 생성 및 적용.')
guide = guide.replace('흙길, 확장 경계, 훈련장 바닥, 외곽 울타리는 씬 구성용 메쉬다.', '흙길과 확장 경계는 씬 구성용 메시다. 외곽 울타리와 숲은 새 Tripo 모델을 사용한다.')
guide = guide.replace('현재 먹이 창고의 영문 복사본과 재질만 수정했으며 D:/임시 원본은 보존했다.', '현재 11종의 영문 복사본과 전용 재질을 적용했다. 이전 먹이 창고와 D:/임시 원본, 새 Tripo 다운로드 원본은 보존했다.')
guide = guide.replace('전체 정적 메쉬 합계는 약 13만 삼각형이며', 'Tripo 적용 후 전체 정적 메시 합계는 967,392 삼각형(화면 밖 숲 포함, 스킨 몬스터 제외)이며')
guide = guide.replace('Tools > Project Run > Ranch > Build Lobby Test는 현재 테스트 씬의 목장 구성과 생성 프리팹을 다시 만든다. 수작업으로 추가한 목장 하위 요소나 교체 모델이 있으면 재생성 전에 별도 보존한다.',
    'Tools > Project Run > Ranch > Build Lobby Test는 현재 테스트 씬의 목장 구성을 다시 만든다. model_generated 자식이 있는 적용 프리팹은 유지한다. 별도로 수작업 수정한 씬 하위 요소는 재생성 전에 보존한다.\nTools > Project Run > Ranch > Refresh Generated Environment는 주변 숲과 외곽 울타리를 새 모델로 갱신한다.\nTools > Project Run > Ranch > Validate Generated Models는 11종의 재질/UV/배치 크기/피벗/Collider와 환경 연결을 검사한다.')
guide_path.write_text(guide, encoding='utf-8-sig')
print('Updated facility guide and generated detailed Tripo result report.')
