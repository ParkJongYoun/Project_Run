OldAsset - Ragnarok legacy visual asset library
원본: D:\svn_alldata\Branches\OBT_KOR_1_16\RS_Development
대상: C:\Project_Run\Run\Assets\OldAsset

FBX 11,585개(모델 및 FBX 내장 애니메이션), .anim 1,975개,
Animator/Override Controller 2,375개, 프리팹 8,899개.
총 37,163개 에셋 / 10.69 GiB (메타데이터 제외).

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
_Catalog/LegacyTimelines.zip: 기존 맵/게임 코드에 의존하는 원본 Timeline 시퀀스 119개와 메타데이터 보관본.

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

최종 Unity 확인
- Unity에서 모델 11,585개, 프리팹 8,899개 등록을 확인했습니다.
- 페코페코 001 / 늑대 001 / 말 001 / 알파카 프리팹과 달리기 클립이 로드됩니다.
- 위 네 프리팹의 missing script 및 빈 재질 슬롯은 0개입니다.
- 기본 페코페코 달리기 0.6초, 늑대 0.8초, 말 1.067초, 알파카 0.6초 (모두 30fps).
- _Catalog/UnityValidation.json: Unity 실제 로드 검사 결과.
- _Catalog/UnityImportWarnings.csv: 임포트 경고/오류가 있는 원본 FBX 203개 목록.
- _Catalog/KnownImportErrors.json: 비정상 키 시간으로 일부 곡선이 제외된 애니메이션 10개 상세.
  (아몬라 6개, 페코페코 004 4개). 원본 파일은 수정하지 않고 보관했습니다.
- 별도로 인퀴지터 공격 애니메이션 1개에 Avatar 리그 불일치 진단이 있습니다.
- 늑대 001 모델의 NaN 정점 4개와 다크클라우드의 미할당 본 가중치 등 원본 품질 경고도 있습니다.
- 파일 이관/참조 보존과 리소스 등록은 완료됐으며, 전체 자산의 외형/모션 품질 보정 및 URP 셰이더 이식은 별도 작업입니다.
