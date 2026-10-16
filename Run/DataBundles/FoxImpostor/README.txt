Project Run — All3D 여우 카메라별 2D 실험 (2026-10-16)

적용
- Assets/Scenes/Scene_All3D.unity의 P_Runner_03(아리 / 구미호)에 RaceFoxSpriteView를 추가했다.
- 씬을 Play하면 기본적으로 여우와 탑승자가 새로 그린 2D 이미지로 표시된다.
- 다른 주자, 경기장, 경기 계산과 실제 카메라 연출은 기존 것을 사용한다.
- Project Run > Fox 2D > Toggle original 3D comparison 메뉴 또는 해당 컴포넌트의 Use Sprites로 원본과 비교한다.
- 컴포넌트를 끄면 원래 3D 렌더러 상태를 복원한다. 원본 프리팹은 변경하지 않았다.

제작물
- Captures: Play Mode의 6초 경기 상태에서 실제 Sys_RaceCamera의 12개 컷을 재현한 원본 화면, 투명 여우 캡처, 두 발 포즈 및 투영 기준 JSON.
- Assets/ArtBundles/FoxImpostor/Textures: 각 구도를 별도로 새로 그린 투명 2프레임 시트 12장. Tracking을 공통 외형 참조로 사용했다.
- Assets/ArtBundles/FoxImpostor/Frames: 원본 시트의 알파를 보존해 분리한 Left / Right PNG 24장.
- Assets/Resources/FoxImpostor/FoxSpriteSet.asset: 카메라 컷과 두 Sprite의 대응, 캡처한 걸음 위상 및 기준 높이.
- Prompts/imagegen-prompts.json: built-in image_gen에 사용한 12개 프롬프트. CLI/API 폴백은 사용하지 않았다.
- Review/Comparisons: 실제 경기장에 배치한 컷별 좌/우 프레임과 원본 3D(왼쪽) / 2D(오른쪽) 비교 화면.
- Review/validation.txt: 실제 Play Mode 검증 결과.

표현 방식
- Opening, Pack, Tracking, Low, Front, Duel, Rider, Charge, Burst, Finish, Winner, SkillVideo의 12개 컷별로 별도 이미지 두 장을 선택한다.
- 기존 주자의 GaitPhase를 읽으므로 일시정지와 배속, 재시작에 맞춰 두 발을 교차한다. 대기/결과는 한 프레임으로 멈춘다.
- 공통 프레임 크기와 피벗을 사용한다. 두 발을 따로 중앙 정렬하거나 따로 크기 조절하지 않는다.
- SpriteRenderer는 실제 3D 위치에 놓고 현재 카메라를 향하게 하며 깊이 테스트로 난간/다른 주자와 가려진다.
- 기존 스킨은 ShadowsOnly로 화면에서 숨기고 그림자와 카메라 추적용 뼈대는 유지했다. Keep Model Shadows를 끄면 스킨 렌더러도 비활성화한다.
- PC / PC_RPAsset 품질 기준을 유지했다.

검증 완료
- 12개 컷, 24개 서로 다른 Sprite 및 두 발 선택.
- 비교 스위치와 컴포넌트 비활성화 시 3D 복원.
- 일시정지에서 경기/걸음 위상 고정, 1배속 및 2배속 교차 재생.
- 건너뛰기 후 원래 시뮬레이션과 동일한 모든 순위와 완주 시간.
- 재시작 후 Opening 컷 복원. Unity 컴파일/콘솔 오류 없음.

실험의 한계
- 컷마다 대표 카메라 시점 1개를 그렸다. 같은 컷 중 순위/코너/카메라 보간에 따라 변하는 모든 연속 시점이나 자유 회전용 360도 이미지 세트는 아니다.
- 원본 달리기는 몸이 크게 숙여지고 펴지는 모션이다. 두 장만 교차하므로 근접 화면에서 프레임 전환이 드러날 수 있다.
- 새 그림의 털/얼굴/장식은 공통 참조로 맞췄지만 원본 모델의 픽셀 단위 재현은 아니며, 구도 간 세부 묘사 차이는 남는다.
- 뼈대/카메라 기준점과 원본 그림자가 남아 있으므로 이번 작업은 시각 검증용이다. 스키닝 제거에 따른 성능 최적화를 검증한 것은 아니다.
- WebGL 빌드 및 실제 모바일 브라우저 성능은 이번 범위에서 측정하지 않았다.

도구
- FoxSpriteCapture.Prepare / Capture: 실제 플레이어 포즈와 카메라를 캡처.
- FoxSpriteInstaller.Install: 시트에서 Sprite를 분리하고 피벗/월드 크기를 등록, All3D 씬에 연결.
- FoxSpriteValidation.Begin: Play Mode에서 비교 화면과 재생/경기 불변성 검증을 재실행.
