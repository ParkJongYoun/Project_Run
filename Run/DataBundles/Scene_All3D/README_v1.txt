Scene_All3D — 첫 3D 경마장 배경 시안
작성/검증: 2026-10-15

씬: Assets/Scenes/Scene_All3D.unity
재사용 프리팹: Assets/Resources/Prefabs/Environment/P_Racecourse_All3D.prefab
메시·재질·표면 텍스처: Assets/ArtBundles/Racecourse/All3D
제작 도구: Assets/Scripts/Editor/Racecourse/All3DRacecourseBuilder.cs

구성
- 외곽 잔디 주로, 안쪽 더트 주로, 흰 난간과 녹색 기둥
- 12개 좌석 구획의 계단식 관람석, 캐노피, 클럽하우스
- 골 지점, 01~08 구역 표지, 전광판, 조명탑, 배너
- 중앙 연못 2곳, 산책로, 화단, 벤치, 나무와 바위, 원경 언덕
- Main Camera와 단일 Directional Light, URP 재질, 카메라 FXAA

작업 범위
배경 확인용 축소 레이아웃이다. 실제 도쿄 경마장이나 2,000m 코스의 정확한 재현은 아니다.
긴 직선은 각각 180 Unity unit, 잔디 주로 안쪽/바깥쪽 반경은 60/77 unit,
주로 폭은 17 unit, 중심선의 기하학적 길이는 약 790.4 unit이다.
01~08은 구역 식별 번호이며 실제 거리 표지가 아니다.
중앙 경사·실제 경기 거리·출발 위치·주행 경로·순위 계산은 아직 구현하지 않았다.
캐릭터, HUD, 경기 제어도 이번 배경 작업에 포함하지 않았다.

기존 리소스 활용
- Assets/OldAsset/Environment/worldboss/etc/wb_bg_iys/oak_tree.FBX
  및 barkOak001.png / BranchesOak001.tif: 입체 오크나무
- Assets/OldAsset/Environment/mjolnir/mj_rock001.FBX 및 mj_rock001.tga: 조경 바위
OldAsset 모델·텍스처·임포터는 수정하지 않고, 이번 맵용 URP 재질만 별도로 만들었다.
관람석, 주로, 난간, 원경 식재 등은 에디터에서 생성한 정적 3D 메시다.

카메라 확인
Unity 메뉴 Tools > Project Run > All 3D:
- Camera - overview: 전체 경마장
- Camera - home straight: 직선 주로와 관람석
- Camera - bend: 곡선 주로
메뉴는 에디터 카메라 구도와 구도에 맞는 원경 안개 거리를 바꾼다.
카메라 구도를 유지하려면 씬을 저장한다. 저장된 기본 구도는 overview다.

재생성
Scene_All3D를 Edit 모드에서 연 뒤 Build racecourse를 실행한다.
이 메뉴는 P_Racecourse_All3D 생성 루트와 해당 생성 에셋을 갱신한다.
이 루트 안의 수동 배치 수정은 재생성 시 초기화되므로, 변경을 유지하려면
제작 도구에 반영하거나 별도 루트/프리팹으로 보관한다. 다른 씬 루트는 보존한다.
프리팹 루트 위치/회전은 0, 스케일은 1이다.

검증 완료
- Unity 6000.3.23f1, 현재 Windows Editor / URP PC 프로파일에서 컴파일 및 Play 확인
- 저장한 씬을 다시 열어 프리팹 및 에셋 참조 확인
- 누락 스크립트 0, 누락 메시 0, 누락/지원되지 않는 재질 0
- 직선 2곳 및 곡선 2곳에서 주로 충돌면 레이캐스트 성공
- Play 모드 콘솔 오류/경고 0
- 메시와 스크립트 수동 점검: Renderer 126개, 메시 삼각형 합계 655,120개
  이는 전체 배치 합계이며 화면에 실제 그려지는 삼각형 수나 드로콜 측정값이 아니다.
- 720×1280 세로 전경/직선/코너와 가로 전경 렌더 확인

미검증
모바일 브라우저 WebGL 빌드, 기기별 메모리/FPS, 실제 캐릭터와 UI를 포함한 성능.
현 단계의 입체 오크나무는 LOD를 추가하지 않은 원본 메시다.

미리보기
Preview_Overview.png: 1440×900 전체 배치
Preview_Portrait.png: 720×1280 기본 전경
Preview_HomeStraight.png: 720×1280 직선
Preview_Bend.png: 720×1280 코너
Preview_PlayMode.png: Unity Play 모드 화면
