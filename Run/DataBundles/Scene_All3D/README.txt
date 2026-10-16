Scene_All3D — 경기 영상 참고 배경 개선 V2
작성/검증: 2026-10-15

씬: Assets/Scenes/Scene_All3D.unity
재사용 프리팹: Assets/Resources/Prefabs/Environment/P_Racecourse_All3D.prefab
메시·재질·표면 텍스처·셰이더: Assets/ArtBundles/Racecourse/All3D
제작 도구: Assets/Scripts/Editor/Racecourse/All3DRacecourseBuilder.cs
개선 구성: Assets/Scripts/Editor/Racecourse/All3DRacecourseBuilder.Refinement.cs

참고 자료
DataBundles/참고데이터/우마무스메_경기영상.mp4
- 01:17~01:24: 출발 게이트, 번호판, 주로를 바라보는 구도
- 01:48 / 03:40: 지붕 있는 관람석과 관중의 밀도
- 02:18 / 03:05: 흰색·녹색 난간과 사선 지지대, 주로 주변 식재
- 02:47: 잔디 주로와 더트의 색·질감 구분
추출한 참고 프레임: VideoStudy/ContactSheet.jpg 및 frame_*.png
영상의 경기 규칙이나 UI를 구현한 작업이 아니라 배경 디자인 참고다.

V2 변경
- 2층 관람석, 곡면 캐노피, 지붕 트러스, 계단 통로, 유리 VIP 구역과 양끝 파빌리온
- 좌석과 정적 3D 관중을 직접 생성해 관람석의 밀도와 색 변화 추가
- 10칸 출발 게이트, 번호판·바퀴·칸막이, 원형 결승 조형물과 결승선
- 프레임이 있는 전광판, 조명탑, 곡면 배너, 읽을 수 있는 메시 표지판
- 굽은 산책로, 석재 호안, 작은 섬, 난간 있는 목교, 분수, 카페·퍼골라
- 파라솔·테이블·의자, 줄기와 꽃잎을 모델링한 화단
- 기존 오크의 잎 메시를 줄여 만든 수관과 수풀, 나무의 크기·방향 변화
- 새 잔디 알베도, 구름 하늘, 잔물결 수면, 색 보정과 ACES 톤매핑
- 카메라 SMAA High, 관람석을 보는 showcase를 기본 구도로 저장

기존 리소스 활용
- Assets/OldAsset/Environment/worldboss/etc/wb_bg_iys/oak_tree.FBX
  및 barkOak001.png / BranchesOak001.tif: 오크나무와 잎 메시·텍스처
- Assets/OldAsset/Environment/mjolnir/mj_rock001.FBX 및 mj_rock001.tga: 조경 바위
OldAsset의 원본 모델·텍스처·임포터는 수정하지 않았다.
파생 수관 메시는 이번 맵 폴더에 별도로 저장했다.
관람석·소품·관중은 에디터에서 생성하고 저장한 정적 3D 메시다.

생성 이미지
imagegen 새 이미지 생성 모드로 잔디 표면 알베도 1장을 제작했다.
프로젝트 파일: Assets/ArtBundles/Racecourse/All3D/Textures/Tex_Turf_Art.png
Unity 임포트: 최대 1024, Repeat, Mipmap, Aniso 8.
전체 프롬프트와 생성 원본 경로는 TexturePrompt.txt에 기록했다.

카메라 확인
Unity 메뉴 Tools > Project Run > All 3D:
- Camera - showcase: 관람석과 결승 지점 (기본 저장 구도)
- Camera - overview: 전체 경마장
- Camera - home straight: 직선 주로
- Camera - bend: 곡선 주로
- Camera - garden: 중앙 정원
메뉴는 Main Camera와 구도에 맞는 원경 안개 거리를 바꾼다.
카메라 구도를 유지하려면 씬을 저장한다.

재생성
Scene_All3D를 Edit 모드에서 연 뒤 Build racecourse를 실행한다.
P_Racecourse_All3D 생성 루트, 생성 에셋, 배경 조명과 카메라를 갱신한다.
생성 루트 안의 수동 배치 수정은 초기화된다. 유지할 수정은 제작 도구에 반영하거나
별도 루트/프리팹으로 보관한다. 다른 사용자 씬 루트는 보존한다.
프리팹 루트 위치/회전은 0, 스케일은 1이다.
PC_RPAsset의 Shadow Distance를 50에서 180으로 늘렸다. 이 프로파일을 쓰는 다른 씬에도 적용된다.

작업 범위
배경 확인용 축소 레이아웃이며 실제 경마장이나 2,000m 코스의 정확한 재현은 아니다.
직선은 각각 180 Unity unit, 잔디 주로 안쪽/바깥쪽 반경은 60/77 unit,
주로 폭은 17 unit, 중심선의 기하학적 길이는 약 790.4 unit이다.
01~08은 구역 식별 번호이며 실제 거리 표지가 아니다.
실제 코스 경사·경기 거리·주행 경로·순위 계산·캐릭터·HUD·경기 제어는 이번 배경 작업 범위 밖이다.

검증 완료
- Unity 6000.3.23f1, Windows Editor / URP PC 프로파일에서 컴파일 및 Play 확인
- 저장된 씬을 다시 열어 메시·재질·프리팹 참조와 표지판 표시 확인
- 누락 스크립트 0, 누락 메시 0, 누락/지원되지 않는 재질 0
- 직선 2곳 및 곡선 2곳의 주로 충돌면 확인
- Play 모드 콘솔 오류/경고 0, 종료 후 저장된 Edit 모드로 복귀
- 배경 루트 Renderer 134개, 배치 메시 삼각형 합계 1,367,854개
  이는 전체 배치 합계이며 실제 화면의 삼각형 수나 드로콜/FPS 측정값이 아니다.
- 720×1280 세로 관람석/직선/코너/정원, 1440×900 가로 전경 렌더 확인

미검증
모바일 브라우저 WebGL 빌드, 실기기 메모리/FPS, 캐릭터·UI를 포함한 성능.
관중은 정적이며 애니메이션은 없다. LOD와 실기기 성능 조정은 후속 작업이다.

미리보기
Preview_v2_Hero.png: 1440×900 관람석·주로
Preview_v2_Overview.png: 1440×900 전체 배치
Preview_v2_Showcase.png: 720×1280 기본 구도
Preview_v2_HomeStraight.png: 720×1280 직선
Preview_v2_Bend.png: 720×1280 코너
Preview_v2_Garden.png: 720×1280 정원
Preview_v2_PlayMode.png: Unity Play 모드 카메라 캡처
README_v1.txt 및 Preview_v1_Overview.png: 첫 시안 기록
