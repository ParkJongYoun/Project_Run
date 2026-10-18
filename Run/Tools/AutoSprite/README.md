# Project Run 2D 몬스터 생성

SGL의 AutoSprite 사용 절차만 참고해 Run 전용으로 작성한 도구다. SGL의 이미지·스크립트·API 키를 복사하지 않는다. 키는 호출 시 `--credential-file`로 지정한 외부 파일에서 메모리로만 읽으며, 인증은 AutoSprite 공식 API에만 보낸다. 다운로드에는 인증 헤더를 붙이지 않는다.

## 원화와 모션

- 원화: 기본 제공 `image_gen`으로 제작. 40도 상부 사선 시점, 투명 배경, 기수 없는 6종 몬스터. 캐릭터별 `art_prompt.txt`에 영어 원화 프롬프트를 보관한다.
- `DataBundles/Art/Race2D/AutoSprite/{ID}/generation.json`: 캐릭터 ID, 작업 ID, 원본 해시, 영어 동작 프롬프트와 한글 요약, 사용 크레딧.
- 기존 3D 캡처 원본은 보존한다. 이 도구가 생성한 새 스프라이트는 `Scene_2D` 전용이다.
- AutoSprite 현재 8방향 팩을 사용한다. `generate_isometric_pack`, `action: custom`, `name: Race Run`, `loop: true`, `frameCount: 0`, `frameSize: 512`. 한 팩은 15크레딧이며 중복 요청 방지를 위한 idempotencyKey를 먼저 저장한다.
- 원화 업로드는 무료다. 원격 등록 이미지 SHA-256과 로컬 원화의 SHA-256을 대조한다. Auto Padding 호출은 하지 않는다.
- 작업 상태는 30초 이상 간격으로 확인한다. 작업이 접수됐으면 다시 생성하지 않고 기존 jobId를 조회한다.

## 도구 순서

1. `race_generation.py account`로 잔액을 한 번 확인한다.
2. 캐릭터 폴더의 `original.png`, `art_prompt.txt`, `generation.json`을 준비한다.
3. `race_generation.py upload`, `generate`, `poll`, `download`를 순서대로 사용한다. 각 명령에는 `--directory`와 `--credential-file`이 필요하다.
4. `inspect_motion.py {ID}`는 모든 방향의 주기·경계와 임시 콘택트 시트를 확인하는 보조 도구다. 수치만으로 프레임을 결정하지 않는다.
5. `runtime_selection.json`에서 실제 한 주기의 연속 프레임을 지정한다. 방향마다 프레임 수가 달라도 전체 캐릭터는 공통 주기로 재생해 회전 시 위상을 유지한다. 준비 자세, 불완전한 마지막 주기, 단순 일정 간격 샘플링은 사용하지 않는다.
6. `prepare_runtime.py {ID}`로 256px 셀에 고정 배율과 16px 여백을 적용한다. 모든 프레임에 동일한 변환을 적용하며 개별 재중앙 정렬은 하지 않는다. Premultiplied-alpha Lanczos 필터를 사용한다.
7. Unity의 `Tools/Project Run/2D race/Import AutoSprite monsters`와 `Apply AutoSprite monsters to Scene_2D`를 실행한다. 씬에 적용하기 전 Play Mode를 종료한다.

화면 기준 방향 순서는 N, NE, E, SE, S, SW, W, NW이다. AutoSprite 응답의 `direction` 필드는 팩에서도 `right`로 표시될 수 있으므로 `kind`의 마지막 방향 이름으로 연결한다.

런타임은 SpriteRenderer만 사용하며, 경기 계산은 기존 Sys_Race를 공유한다. 16방향 3D 캡처용 데이터는 이전 방향 규칙으로 계속 동작한다. 텍스처는 Bilinear, 밉맵 없음, 최대 4096, PC/WebGL DXT5로 가져온다. 출력 PNG와 Import.json은 Assets/ArtBundles/Race2D/AutoSprite에, Sprite 배열은 Assets/Resources/Race2D/AutoSprite에 저장한다.

## 2026-10-17 생성 이력

6종 첫 생성 90크레딧. 에드가 첫 영상의 꼬리 잘림을 원본 크기로 무료 재추출하여 확인했으나 원본 결손이라 복원되지 않았다. 꼬리를 짧게 말린 새 원화로 에드가만 15크레딧 추가 생성했다. 합계 105크레딧. 에드가 이전 결과와 원화는 Revision1에 보존한다. 최종 검증 여부와 수치는 DataBundles/검증/Race2D_AutoSprite_20261017에 기록한다.

## 2026-10-18 방향·크기 검사

6종 601개 유효 프레임의 N→NW 연결, 몸 크기, 접지 위치를 검사했다. 8방향 이름은 지면 방위 기준이다. 화면에서 대각선이 보이는 각도는 원화의 40도 내려다보기 각도로 투영한 약 57.3/122.7/237.3/302.7도다. 임포터가 `DirectionAngles`를 저장하고 런타임은 가장 가까운 각도를 선택한다. 경계에는 2도 여유를 둔다. 각도 배열이 없는 기존 16방향 캡처는 기존 등간격과 4도 여유를 유지한다.

스모키는 측면의 머리·몸통이 작고 대각선이 커지는 원본 차이가 있어 `Assets/ArtBundles/Race2D/AutoSprite/Smokie/DirectionCalibration.json`으로 방향별 고정 등방 배율과 지면 앵커를 적용했다. 기준은 대표 프레임 및 전체 루프의 머리/몸통/앞뒤 발이며, 꼬리까지 포함한 알파 박스 자동 정규화는 사용하지 않는다. N/NE/E/SE/S/SW/W/NW 배율은 0.94/0.86/1.20/0.77/0.90/0.77/1.20/0.86이다. 머리 대 몸 비율 자체가 다른 부분까지 복원하는 처리는 아니다.

앵커는 256px 셀 좌상단 기준이며 Unity 피벗은 `(x/256, 1-y/256)`, PPU는 `256/(WorldSize*Scale)`다. 같은 방향의 모든 프레임에 동일한 값을 적용해 발구름과 점프를 보존한다. `prepare_runtime.py`는 이 별도 보정 파일을 덮어쓰지 않는다. PNG, 선택 프레임 수, 재생 시간, Display Transform 배율은 변경하지 않았다. 재임포트 시 기존 Sprite 참조를 갱신한다.

미노타우로스·새비지·루나틱·에드가에서는 스모키 같은 큰 배율 불연속이 발견되지 않아 개별 배율은 유지했다. 포링은 크기가 일정하지만 NE/E 및 NW/W 원화의 얼굴 노출 차이가 작아 방향 구분이 약하다. 스모키의 기존 원본 경계 접촉 3프레임은 재생성하지 않았으며, PPU 보정으로 결손이 복원되었다고 보지 않는다.

`audit_directions.py`는 전체 프레임 콘택트 시트와 알파 통계를 만든다. `preview_direction_calibration.py --video`는 원본 PNG를 바꾸지 않고 보정 전후 이미지와 1배속·0.4배속 비교 영상을 만든다. 검증 결과는 `DataBundles/검증/Race2D_DirectionAudit_20261018`에 있다. 실제 한 경기에서 6종 모두 8방향을 통과했고, 계산한 진행 방향과 실제 이동 벡터의 최대 차이는 0.983도였다. 카메라 배율 15 유지, 601프레임 고정 PPU/피벗, 원본 및 런타임 PNG의 기존 SHA-256 일치를 확인했다.
