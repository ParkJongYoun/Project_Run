# 현재 독립 카툰 6종

2026-10-17 사용자 추가 요청: 복사본에 남은 이전 외형 재질 등을 정리하고 카툰형 6종을 별도 캐릭터 에셋으로 취급한다.

경로는 Unity 루트 `D:/Project_Run/Run` 기준이다.

## 수정할 에셋

- 프리팹: `Assets/Resources/Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_{ID}.prefab`.
- 캐릭터별 리소스: `Assets/ArtBundles/Characters/CartoonMonsters/{ID}/`.
- 하위 폴더: `Meshes`, `Rig`, `Animations`, `Textures`, `Materials`, `Portraits`.
- 카툰 6종이 공용으로 쓰는 셰이더·그림자·먼지·스킬 이펙트: `Assets/ArtBundles/Characters/CartoonMonsters/Shared/`.
- 현재 출전 ID: `Minorous`, `GiantPoring`, `Smokie`, `Savage`, `Lunatic`, `Edgar`.
- 적용 씬: `Assets/Scenes/Scene_OvalToon.unity`, `Assets/Scenes/Scene_PronteraToon.unity`. 타이틀의 카툰 캐릭터 두 조합에 연결한다.
- 2026-10-18 외곽선·음영 강화 요청으로 현재 6종의 전용 재질 12개를 보정했다. 최신 수치는 [승인된 외형 기준](approved-look.md)의 2026-10-18 항목, 전후 화면과 원복본은 `DataBundles/LookDev/카툰_음영외곽선강화_20261018/`를 따른다. 텍스처·셰이더 코드·프리팹 구조는 유지했다.
- 2026-10-17 추가 요청으로 마타(슬롯 1)는 자이언트 포링, 오크(슬롯 5)는 호랑이 에드가로 교체했다. 기존 `Matyr`·`Orc` 에셋은 보관하되 현재 두 씬에서는 출전하지 않는다. 원본 캐릭터 조합인 `Scene_Oval`·`Scene_Prontera`는 이 교체 대상이 아니다.
- 이후 같은 날 요청으로 `Minorous`, `GiantPoring`, `Edgar`의 표시 크기를 직전 크기의 0.7배로 조정했다. 독립 프리팹의 루트 스케일 1을 유지하고 자식 모델 루트의 위치·스케일과 그림자·이펙트 위치·카메라 추적 높이를 맞췄다. 자식 모델 루트 스케일은 미노 `0.896`(직전 1.28), 포링·에드가 `0.7`(직전 1)이다. 두 카툰 경기장에 공통 반영하며 다른 캐릭터에 자동 적용하지 않는다. 변경 전 프리팹과 수치는 `DataBundles/LookDev/카툰몬스터_크기보정_20261017/`에 보관한다.

프리팹은 원본을 상속하는 Variant가 아닌 Regular 프리팹이다. 메시·아바타·애니메이션·마스크를 독립 에셋으로 저장했고 텍스처·재질·초상화와 셰이더도 전용 폴더로 복제했다. Unity 기본 리소스, 패키지, 공용 경기 스크립트는 계속 사용한다. 모델을 새로 조형하거나 애니메이션을 새로 제작했다는 뜻은 아니다.

새 캐릭터도 이 독립 구조를 따른다. `GiantPoring`과 `Edgar`는 기존 `P_Runner_02`·`P_Runner_01`의 탈것 모델과 동작을 복제했지만 원본 프리팹·FBX·재질·텍스처 의존성을 남기지 않는다. 캐릭터별 새 Anime/AnimeOutline 재질과 편집한 알베도를 사용하고, 카툰 전용 Shared 셰이더·이펙트만 공유한다. RiderRoot/RiderAnimator/RidingClip은 비우고 얼굴 타깃은 몬스터에 연결한다.

## 제거한 연결

- 각 카툰 주자의 `Sys_MonsterLook`과 Original/Toon/Outlined 재질 배열.
- 새 경기장의 `Sys_RaceLookDev`, `Sys_RaceRenderStyle`, 비활성 `group_render_style` 선택 UI.
- 원본 DemoMonsters 프리팹과 FBX·아바타·원본 텍스처·이전 재질을 향한 6종 프리팹의 의존성.
- 경기 참가자 초상화는 카툰 전용 파일로 연결했다.

독립 프리팹에서 `Use Updated Look`을 다시 붙이거나 원본 프리팹의 재질을 가져오지 않는다. 스타일 비교는 기존 All3D와 카툰 경기장을 나누어 실행한다. 기존 All3D, 원본 리소스, 미노 단독 비교/주행 씬과 과거 LookDev 제작 이력은 남아 있다.

## 제작 도구와 검증

- `Assets/Scripts/Editor/Racecourse/CartoonMonsterPrefabBuilder.cs`는 이번 6종 분리용 일회성 마이그레이션이다. 폴더가 이미 있으면 실행을 거부한다. 새 캐릭터를 추가할 때 기존 에셋을 지우고 재실행하지 않는다.
- `CartoonMonsterPrefabBuilder.Validate()`는 Regular 프리팹, 전용 폴더 밖의 캐릭터 에셋 참조, 원본 외형 제어기, 필수 리깅과 재질을 검사한다.
- `CartoonMountRunnerBuilder.Build()`는 포링·에드가 전용 복제·재질·초상화·두 씬 교체 도구다. 다른 캐릭터의 범용 변환기로 사용하지 않는다. `Validate()`는 두 프리팹 의존성, 기수 제거, 씬 슬롯과 초상화 연결을 확인한다. 기록은 `DataBundles/LookDev/자이언트포링_에드가_카툰교체_20261017/`에 있다.
- `MonsterRaceLookDevBuilder.RefreshPortraits()`는 독립 프리팹 이름도 처리하며 전용 Portraits 폴더의 이미지를 갱신한다.
- 작업 기록: `DataBundles/LookDev/카툰몬스터_독립에셋_20261017/`. `asset_manifest.json`에 원본/독립 에셋 매핑, `asset_validation.json`에 프리팹 의존성, `protected_hashes.json`에 원본 해시, `pose_validation.json`에 대기·달리기 샘플 포즈 비교가 있다.
- 6종의 대기·달리기 클립을 각 길이의 37% 시점에서 샘플링했을 때 원본 대비 스킨 메시 정점 위치 차이는 모두 0이었다. 이는 해당 샘플의 검증이며 모든 프레임의 수학적 동일성을 뜻하지 않는다.

외형·애니메이션을 다듬을 때 이 전용 에셋을 수정하고 실제 레이스에서 확인한다. 새로운 스타일·동작 변경은 사용자의 최신 요청을 따른다.
