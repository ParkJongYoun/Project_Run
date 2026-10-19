# 승인된 카툰 외형 기준

기준일: 2026-10-17. 미노 외형은 사용자가 선호한다고 명시한 기준이며, 나머지 5종은 그 방향으로 확장해 적용한 결과다. 아래 숫자는 해당 씬의 재현 기준이다. 새로운 모델·카메라의 최종값을 일괄 확정하는 규칙은 아니다.

이 문서의 `Assets/...`와 `DataBundles/...`는 Unity 루트 `D:/Project_Run/Run` 기준이다.

**현재 적용 위치:** 이후 사용자 요청으로 6종을 [독립 프리팹·리소스](independent-assets.md)로 분리했다. 아래 LookDev 폴더는 스타일의 제작 원본·이력이며, 현재 6종 수정은 `Assets/ArtBundles/Characters/CartoonMonsters`에서 한다. 수치와 외형은 유지했다. 6종 경기장의 `Use Updated Look` 및 이전 스타일 선택 기능은 제거됐다.

현재 출전 구성은 미노타우로스·자이언트 포링·스모키·새비지·루나틱·호랑이 에드가다. 마타·오크는 2026-10-17 추가 요청으로 카툰 두 씬에서 교체됐다. 아래 마타·오크 텍스처 항목은 최초 제작 이력이다. 포링·에드가의 현재 리소스는 각각 `CartoonMonsters/GiantPoring`, `CartoonMonsters/Edgar`이며 전용 새 재질과 알베도를 사용한다.

## 결과 씬과 에셋

| 경로 | 용도 |
|---|---|
| `Assets/Scenes/Test/Scene_CartoonTest.unity` | 2026-10-19 미노 단독 전시를 이름 변경·확장한 카툰 비교 전시. 현재 몬스터·탈것 17종 |
| `Assets/Scenes/Scene_MinorousRaceTest.unity` | 미노 한 마리 실제 주행·HUD |
| `Assets/Scenes/Scene_OvalToon.unity` | 기존 경기장·카툰 6종 실제 주행·HUD |
| `Assets/Scenes/Scene_PronteraToon.unity` | 신규 경기장·카툰 6종 실제 주행·HUD |
| `Assets/Scenes/Scene_Oval.unity`, `Assets/Scenes/Scene_Prontera.unity` | 기존 외형 6종 비교용 경기장 |
| `Assets/ArtBundles/RaceShowcase/MinorousLookDev/` | 승인된 미노 텍스처·재질·셰이더·환경 |
| `Assets/ArtBundles/RaceShowcase/MonsterLookDev/` | 나머지 5종 텍스처, 6종 윤곽선·초상화, 공용 셰이더 |

- 미노: `MinorousLookDev/Textures/Minorous_Albedo.png`, `Materials/Minorous_Anime.mat`, `Shaders/MinorousAnime.shader`.
- 나머지 5종: `MonsterLookDev/Textures/{ID}_AnimeAlbedo.png`, `Materials/{ID}_Anime.mat`, `Shaders/MonsterAnime.shader`.
- 6종 윤곽선: `MonsterLookDev/Materials/{ID}_AnimeOutline.mat`.
- 6종 얼굴: `MonsterLookDev/Portraits/FacePortrait_{ID}.png` (256×256, 투명).
- 원본 주자: `Assets/Resources/Prefabs/Race/DemoMonsters/P_Runner_{ID}.prefab`.
- `{ID}`: `Minorous`, `Matyr`, `Smokie`, `Savage`, `Lunatic`, `Orc`.

원본 텍스처 공통 루트: `Assets/OldAsset/Monsters/resource/material_001/`.

| ID / 이름 | 원본 텍스처의 상대 경로 |
|---|---|
| Minorous / 미노타우로스 | `morocc_mob/minorous/Materials/minoruros_001.png` |
| Matyr / 마타 | `morocc_mob/matyr/Materials/matyr_001.png` |
| Smokie / 스모키 | `geffen_mob/smokie_001/smokie_001.png` |
| Savage / 새비지 | `mjolnir_mob/savage/Materials/savage_001.png` |
| Lunatic / 루나틱 | `prontera_mob/lunatic/Materials/lunatic_001.png` |
| Orc / 오크 | `unused/orc_axe_001/Materials/orc_axe_001.png` |

미노 원본 파일의 실제 철자는 `minoruros`다. 이름을 고치기 위해 기존 파일이나 GUID를 바꾸지 않는다.

## 텍스처와 재질

### 2026-10-18 현재 6종 명암·외곽선 보정

사용자가 `Scene_OvalToon` 캐릭터의 외곽선과 음영을 더 진한 카툰풍으로 요청하여, 독립 카툰 6종의 Anime/AnimeOutline 재질 12개를 조정했다. 같은 재질을 사용하는 `Scene_PronteraToon`에도 반영된다. 아래 표는 현재 6종의 최종값이며, 뒤의 2026-10-17 수치 표는 최초 제작 이력이다. 보관 중인 마타·오크와 미노 단독 비교 씬은 변경하지 않았다.

| 프로퍼티 | 현재 6종 값 |
|---|---|
| `_ShadeColor` | 기본 (.45, .47, .60, 1), 포링 (.57, .40, .56, 1), 루나틱 (.50, .49, .66, 1) |
| `_Threshold` | .22 |
| `_Softness` | .035 |
| `_AmbientStrength` | .10 |
| `_FillStrength` | .20 |
| `_RimStrength` | 미노 .045, 나머지 .025 |
| `_OutlineWidth` | 기본 1.45픽셀, 루나틱 1.10픽셀 |
| `_OutlineColor` | 기본 (.055, .035, .060, 1), 루나틱 (.11, .075, .15, 1) |

기존 텍스처·셰이더 코드·알파/컬링·광택·베이스 색·조명·Volume·PC 품질은 유지했다. 선명한 명암 경계와 낮춘 보조광 응답으로 음영을 강화하며, 밝은 캐릭터는 전용 그림자색으로 조정한다. 새 캐릭터에 무조건 복사하는 규격은 아니다.

전후 화면, 재질 원복본과 검증 기록: `DataBundles/LookDev/카툰_음영외곽선강화_20261018/`. 실제 검증 범위는 해당 폴더의 `작업기록.txt`를 따른다.

색면을 넓고 차분하게 묶고, 텍스처에 그려진 진한 근육·털 그림자와 밝은 하이라이트를 줄인다. 눈·입·의상 경계는 유지한다. 표면의 입체감은 부드러운 toon 명암과 실제 조명으로 만든다.

나머지 5종의 현재 임포트는 Default, sRGB=true, Mipmaps=true, Max Size=1024, Uncompressed, Repeat, Trilinear, Aniso=4다. 생성 요청은 1024였지만 실제 생성 파일은 1254×1254이며 Unity 임포트는 1024×1024다. 새 대상은 원본의 비율·알파·wrap 용도와 최종 화면 크기를 확인해 선택한다.

`ProjectRun/MonsterAnime`는 승인 미노 셰이더를 복제하여 공용 이름을 붙인 것이다. URP 조명·그림자·깊이 패스·안개와 Forward+ 추가 광원 처리를 유지한다. 스타일 조정은 먼저 재질 값으로 처리한다.

| 프로퍼티 | 미노 기준 | 나머지 5종 |
|---|---|---|
| `_BaseColor` | (1, 1, 1, 1) | 동일, 루나틱은 (.88, .88, .90, 1) |
| `_ShadeColor` | (.78, .80, .88, 1) | 동일, 루나틱은 (.76, .77, .88, 1) |
| `_Threshold` | .05 | 동일 |
| `_Softness` | .13 | 동일 |
| `_Specular` | .045 | .015, 오크는 .04 |
| `_Gloss` | 48 | 동일 |
| `_RimStrength` | .09 | .055 |
| `_RimPower` | 3.5 | 동일 |
| `_RimColor` | (.80, .89, 1, 1) | 동일 |
| `_AmbientStrength` | .2 | 동일 |
| `_FillStrength` | .45 | 동일 |

`_Cull`, `_AlphaClip`, `_Cutoff`는 원래 재질에서 가져왔다. 예를 들어 루나틱은 Cull=0이며 전 모델을 Cull=2로 통일하지 않는다. 새 모델이 반투명 표면이나 다른 재질 기능을 사용하면 현재 셰이더와의 호환성을 먼저 확인한다.

윤곽선 셰이더는 `ProjectRun/MonsterOutline`, 파일은 `Assets/ArtBundles/Shaders/Character/MonsterOutline.shader`다. 일반 Width=.65, Color=(.14, .085, .10, 1), 루나틱은 Width=.45, Color=(.28, .22, .32, 1)이다. 실제 outline renderer와 메시를 함께 사용하고 표면·윤곽선의 stencil 설정을 유지한다. Width를 월드 공간 거리로 해석해 모델 크기에 곱하지 않는다.

## 조명·환경·카메라

아래 방향 벡터는 Euler 각도가 아니다. 기존 빌더는 `Quaternion.LookRotation(-direction.normalized)`로 회전을 만든다. 모든 광원은 Directional이다.

| 광원 | direction | RGB | intensity | 그림자 |
|---|---|---|---|---|
| Key | (5, 7, -4) | (1, .965, .91) | 1.1 | Soft, Bias=.005, Normal Bias=.06 |
| Fill | (3, 2, 7) | (.68, .81, 1) | .45 | 없음 |
| Rim | (-4, 5, 2) | (1, .87, .67) | .7 | 없음 |

- Ambient Trilight: Sky=(.60,.70,.85), Equator=(.46,.49,.57), Ground=(.30,.25,.22).
- Linear Fog: RGB=(.72,.80,.87), Start=55, End=180.
- 잔디 복제 재질: Dark=(.40,.53,.29), Light=(.45,.58,.32), BaseMap은 흰색. 원본 경기장 재질과 분리한다.
- Global Volume: Neutral tonemapping, Post Exposure=+.1, Contrast=3, Saturation=-3.
- 카메라: HDR 및 Post Processing 활성, SMAA High, depth 사용, Volume의 레이어가 카메라 Volume Mask에 포함되는지 확인.

| 독립 Volume 프로필 | Gaussian DOF Start / End | Max Radius |
|---|---|---|
| `MinorousLookDev/Minorous_LookDevVolume.asset` | 13 / 25 | 1.2 |
| `MinorousLookDev/Minorous_RaceVolume.asset` | 24 / 50 | 1.2 |
| `MonsterLookDev/Monster_RaceVolume.asset` | 70 / 120 | 1.2 |

프로필 경로의 공통 앞부분은 `Assets/ArtBundles/RaceShowcase/`다. 단독 주행의 24/50을 넓은 단체 구도에 그대로 복사하면 주자까지 흐려졌다. 대상 카메라의 실제 거리로 초점을 확인해 조정한다. 장면 전체를 흐리게 만들어 카툰 느낌을 내지 않는다.

## 기존 코드의 역할과 재사용 경계

| Unity 루트 기준 코드 | 역할 / 주의점 |
|---|---|
| `Assets/Scripts/Game/Race/Presentation/Sys_RaceLookDev.cs` | Renderer별 원본/수정 재질 배열 전환. 경기 계산과 애니메이션을 수정하지 않음 |
| `Assets/Scripts/Game/Race/Presentation/Sys_MinorousLookDev.cs` | 미노 단독 비교 및 선택적 Idle 재생. 실제 레이스에서는 PlayIdleAnimation=false |
| `Assets/Scripts/Game/Race/Runner/Sys_MonsterLook.cs` | 기존 스타일의 표면·윤곽선 참조. 다른 스타일 제어의 재질 덮어쓰기 확인 |
| `Assets/Scripts/Editor/Racecourse/MinorousLookDevBuilder.cs` | 첫 미노 전시 씬 생성용. 기존 씬을 덮어쓰지 않음 |
| `Assets/Scripts/Editor/Racecourse/MonsterRaceLookDevBuilder.cs` | 현재 6종·단일 표면 슬롯에 맞춘 생성, 환경 복사, 초상화, 타이틀 연결 코드 |

`MonsterRaceLookDevBuilder.Build()`는 기존 대상 씬이 있거나 6종/슬롯 구성이 다르면 실패한다. 범용 변환기나 매번 실행하는 초기화 명령이 아니다. `RefreshPortraits()`와 `ConnectTitle()`도 지정된 씬·레이아웃에 묶여 있으므로 코드와 현재 상태를 먼저 읽는다.

과거 비교 구성의 `Use Updated Look`은 연결된 캐릭터 재질과 윤곽선을 바꾸는 기능이었다. 현재 독립 6종 경기장에는 없다. 미노 단독 비교 씬은 2026-10-19 사용자 요청으로 `Assets/Scenes/Test/Scene_CartoonTest.unity`로 이름을 바꾸고 기존 카툰 8종과 신규 6종의 전시로 확장했다. 이전 씬 파일은 해당 작업의 `작업기록/변경전_미노외형씬.unity.bak`에 보존한다.

2026-10-19 신규 비교 전시 대상은 일반 포링·페코페코·데비루치·바포메트·바포메트 주니어·탈것 여우다. 원본 UV를 기준으로 built-in image_gen에서 새 알베도를 생성하고 독립 Regular 프리팹·메시·아바타·대기/달리기 클립·재질로 복제했다. 무낙 원본은 찾지 못했으며 여우를 구미호 몬스터로 확정하지 않았다. 기존 경기 씬의 출전 구성은 이 작업으로 바꾸지 않았다. 생성 프롬프트·원본·파일 크기·검증 기록은 `DataBundles/아트/카툰몬스터_비교전시_20261019/작업기록`에 둔다.

같은 날 추가 요청으로 전시에서 마타·오크·바포메트를 제외하고 알파카·청룡·말 001·집사 위스퍼·불꽃새·짐꾼 강아지 6종을 추가했다. 현재 전시는 기존 11종과 신규 탈것 6종, 총 17종이다. 제외한 에셋과 실제 경기 씬은 보존한다. 신규 ID는 `Alpaca`, `BlueDragon`, `Horse`, `ButlerWhisper`, `FlameBird`, `PorterPuppy`이며 원본 UV를 유지한 새 image_gen 알베도, 독립 Regular 프리팹과 전용 메시·아바타·클립·재질을 사용한다. 알파카의 원본 동작 이름은 `_stand`·`_run`으로 끝나므로 `_stand_01`·`_run_01`만 가정하지 않는다. 작업·검증 기록은 `DataBundles/아트/카툰탈것6종_전시확장_20261019`에 있다.

이후 명시적인 비교 요청으로 `Scene_CartoonTest` 오른쪽 위에 `원본 ↔ 카툰` 버튼을 추가했다. 전시 씬에서만 LegacyURP의 원본 17종을 첫 전환 시 생성·재사용하며 카툰과 별도의 모델 묶음으로 표시한다. 원본의 메시·텍스처와 URP 호환 CharacterSimple 재질을 사용하고, 확대/회전/카메라/대기·달리기 재생 시점을 유지한다. 카툰 Regular 프리팹의 재질·텍스처·독립성 및 실제 경기 씬에는 영향을 주지 않는다. 기록: `DataBundles/검증/카툰테스트_원본전환_20261019`.

## 기록과 검증된 범위

- `DataBundles/LookDev/미노타우로스_카툰외형_20261017/작업기록.txt`: 미노 편집 이력, 프롬프트 버전, 전후 화면.
- `DataBundles/LookDev/미노타우로스_주행검증_20261017/사용법_검증.txt`: 단독 주행 연결 기록.
- `DataBundles/LookDev/몬스터6종_카툰외형_20261017/사용법_검증.txt`: 6종 적용과 전체 경기 확인.
- 같은 6종 폴더의 `texture_generation.json`: 최종 5종 영문 프롬프트와 원본·생성·프로젝트 출력 경로.
- 같은 폴더의 `original_hashes.json`: 원본 보존 확인 기준.
- 같은 폴더의 `six_monsters_final.png`, `smokie_closeup_final.png`, `title_final.png`, `result_final.png`: 실제 완료 화면.

2026-10-17 Editor Play Mode에서 타이틀 양쪽 경기장 진입, 6종 활성/기수 없음, 주행, 스킬, 자연 완주, 건너뛰기 결과 일치, 재시작, 원본/수정 외형 전환을 확인했다. 최종 콘솔 오류·경고는 0이었다. 원래 All3D·미노 단독 경기·원본 텍스처·6종 주자 프리팹은 해당 작업 시작 시점 대비 해시가 같았다.

이는 당시 결과의 검증 기록이다. 새 대상의 검증을 대신하지 않으며 **실제 WebGL 빌드·브라우저 주행·모바일 성능은 당시 검증하지 않았다**.
