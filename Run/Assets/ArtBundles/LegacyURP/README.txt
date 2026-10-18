레거시 캐릭터 · 탈것 URP 라이브러리

바로 사용하기
1. Assets/ArtBundles/LegacyURP/ReadyToDrag 폴더를 연다.
2. 원하는 M_ 프리팹을 Scene 또는 Hierarchy로 드래그한다.
3. Assets/Scenes/Scene_LegacyURP_Preview.unity에서 대표 모델 8개를 확인할 수 있다.

ReadyToDrag 구성 (124개)
- Mounts: 한글 이름으로 정리한 탈것 62종.
- Pets: 매, 갑옷매 2종.
- Characters: 몸 · 얼굴 · 머리를 조립한 기본 남녀 캐릭터 2종.
- Characters/Classes: 직업별 몸에 기본 얼굴 · 머리를 연결한 남녀 캐릭터 58개.
직업별 이름은 원본 영문 식별자를 유지한다. 탈것 한글 이름은 원본 이름과 외형에서 추론한 개발용 명칭이다.

그 외 모델
- Characters / Mounts / Pets / Monsters / Equipment의 Prefabs: 기존 프리팹을 변환한 모델.
- 각 분류의 Models: 원본 FBX를 감싼 URP 모델 프리팹. @ 애니메이션 전용 FBX는 이 목록에서 제외했다.
- 각 분류의 Materials: 변환된 재질.
- 얼굴, 머리, 장비 같은 부품은 조립용이다. 완성 캐릭터가 필요하면 ReadyToDrag를 사용한다.

표시 방식
ProjectRun/CharacterSimple 셰이더를 사용한다. 기본 설정은 텍스처 색을 그대로 표시하며,
Directional Light, 환경광, 라이트맵, GI, Reflection Probe, 후처리를 요구하지 않는다.
원본 재질의 주 텍스처, 색상, UV, 양면, 투명 / 알파 컷아웃 / 가산 설정을 옮겼다.
Self shading은 선택 사항이며 기본값은 꺼짐이다. 켜도 씬 조명에는 의존하지 않는다.
실시간 그림자, 기존 특수 조명, 굴절, 왜곡, 환경 상호작용은 재현하지 않는다.
기존 Animator와 애니메이션 참조는 유지한다. 레이스 상태 제어와 탑승자 부착은 별도 게임 기능이다.
프리팹 루트는 위치 0, 회전 0, 크기 1이며 바닥 / 중심 보정은 자식에서 처리했다.
미리보기 씬은 비교를 위해 배치 크기를 별도로 조절했다.

원본과 사용 범위
Assets/OldAsset는 원본 보관 폴더다. 이 폴더의 원본 FBX를 직접 드래그하면 기존 재질을 사용할 수 있으므로,
URP 표시에는 이 라이브러리의 M_ 프리팹을 사용한다.
변환 프리팹은 OldAsset의 메시, 텍스처, 애니메이션을 참조한다. OldAsset를 삭제하거나 이동하지 않는다.
실제 게임에서 쓰는 모델을 선정한 뒤 필요한 게임용 프리팹만 Assets/Resources에 등록한다.
환경 / 지형 / 소품 전체의 URP 변환은 이번 캐릭터 · 탈것 작업 범위에 포함하지 않는다.

원본 누락과 검증
원본 메시가 없는 헤어 32번 프리팹 16개는 사용 목록에서 제외했다.
재질이 없는 일부 기존 파티클은 표시를 껐다. 일부 캐릭터 / 장비 부품은 정지 자세를 복구했으며
(복구 기록 70건), 움직이려면 해당 부품의 원래 본 계층에 연결해야 한다.
전체 프리팹 참조 검사, 대표 6종 달리기 변형 검사, 미리보기 Play 모드 확인 결과는
프로젝트 루트 DataBundles/기존에셋_URP변환/ConversionReport.txt 및 Validation.json에 기록했다.
에디터 검증 완료와 모바일 브라우저 검증은 별개다. WebGL 모듈 미설치로 웹 빌드는 실행하지 않았다.

변환 도구: Tools > Project Run > Legacy URP
Build missing converted assets는 없는 변환 파일을 생성한다. 기존 변환 재질을 강제로 덮어쓰지 않는다.
ReadyToDrag 이름 / 조립 목록은 DataBundles/기존에셋_URP변환/QuickAccessCatalog.tsv를 참고한다.
