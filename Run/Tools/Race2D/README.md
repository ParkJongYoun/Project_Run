# 2D 레이스 제작·검수 도구

2026-10-22 정리: 아래 내용은 과거 작업의 참고 기록이다. 현재 정식 데모는 `Scene_Title → Scene_LobbyTest → Scene_OvalToon`이며, 이전 2D 경기 씬과 이 폴더의 일회성 Python 도구·임시 설정은 삭제했다. 아래 명령과 경로가 현재 프로젝트에 모두 남아 있는 것은 아니다.

2026-10-16 야간 작업에서 사용한 재현 자료다. Unity 게임 실행에는 이 Python 도구가 필요하지 않다.

## 원본과 결과

- 원화: `DataBundles/기획/시안/2D뷰세트_탈것6종_경기장전체_v1`
- 생성 작업 기록: `DataBundles/영상/2D탈것_달리기_20261016/jobs.json`
- 생성 워크플로: 같은 폴더의 `_work/<작업명>/workflow.json`
- 검수 통과 목록: 같은 폴더의 `view_status.json`
- 프레임 선택·피벗·재생 시간: `sprites/<작업명>/clip.json`
- 게임용 시트: `Assets/ArtBundles/Race2D/animations`
- Unity 검증 결과와 화면: `DataBundles/검증/2D레이스_20261016`

원화의 캐릭터 비율을 유지한다. 800×800 전체 캔버스의 프레임 좌표를 그대로 사용하며 프레임마다 크기를 맞추거나 중앙 정렬하지 않는다. SGL 전용 바닥 기준점 y=600은 이 프로젝트의 키 큰 탑승 이미지에 적용하면 머리가 잘리므로 원화의 실제 바닥 피벗을 유지한다.

## 영상 생성

로컬 ComfyUI의 MiniMax H3 fl2va UNet INT8, Qwen3VL NVFP4, 25 steps, turbo OFF, 24fps, 800×800을 사용했다. 2초 요청이 모델의 프레임 단위 제약으로 실제 56프레임·약 2.333초가 된다. 게임 루프는 그중 연결되는 완전한 동작 구간을 선택하므로 재생 길이가 더 짧을 수 있다.

`prepare_run_jobs.py`는 최초 입력과 프롬프트를 준비한다. 기존 작업의 `jobs.json`을 덮어쓰므로 진행 중인 작업 폴더에 재실행하지 않는다. 실제 생성은 저장된 워크플로를 ComfyUI MCP로 검증·제출한다. 큐와 prompt ID를 확인하여 중복 제출하지 않는다.

## 후처리

Python에는 Pillow, NumPy, SciPy, PyAV가 필요하다. `post_ready.py <작업명>`은 원본 시간 정보를 유지해 추출하고 마젠타 배경 매트와 루프 후보를 만든다. 실행 폴더는 Unity 프로젝트 루트 `Run`이다.

```text
python Tools/Race2D/post_ready.py fox_01_side
python Tools/Race2D/review_frames.py fox_01_side 16 24 32
python Tools/Race2D/export_run.py fox_01_side 16 32 "실제 검수 내용"
```

`export_run.py`의 마지막 프레임 번호는 포함하지 않는다. 자동 후보 점수만으로 통과 처리하지 않는다. 실제 프레임에서 카메라, 외형, 완전한 동작, 루프 연결, 투명 경계와 잘림을 확인해야 한다. 실패한 뷰에는 `clip.json`을 만들지 않아 원래 정지 이미지가 유지된다.

분홍 포링과 보라색 불꽃에는 기본 마젠타 제거값을 적용하면 몸이나 효과가 사라질 수 있다. 포링은 `matte_pink.json`을 사용한다. 배경 색상이 시간에 따라 변한 늑대 후방은 `connected_flat_matte.py`로 배경과 연결된 영역을 구분한다. 유령의 보라색 효과와 마젠타 배경이 섞이는 경우 녹색 배경으로 다시 생성한 뒤 `connected_flat_matte.py`와 `despill_green.py`를 사용한다. 이 녹색 제거는 원래 녹색 디자인이 없는 유령 전용이다.

`adaptive_matte.py`, `key_flat_video.py`, `cleanup_ghost_matte.py`는 실패 사례를 분석한 실험 도구다. 결과를 자동으로 완성 에셋으로 취급하지 않는다. 이미 보정한 작업에 `post_ready.py`를 다시 실행하면 최신 매트에 기본 경계 보정이 적용되므로 재실행하지 않는다.

## Unity 적용과 검증

Play 모드를 종료한 후 `ProjectRun.Editor.RaceRunClipImporter.ImportReviewed()`를 실행한다. 검수 통과 기록만 가져온다. 시트는 800px 셀·최대 5×5 페이지이며 축소하지 않는다. Sprite Editor 프레임 편집 기능이 이 프로젝트에서 제공되지 않아 텍스처 페이지와 메타데이터를 저장하고 런타임 `Sprite.Create`로 분리한다.

- `RaceSpriteValidation.Start()`: 실제 여우 영상·일시정지·복귀·건너뛰기·재시작·동일 경기 결과 검사.
- `RaceSpritePlaybackValidation.Start()`: 공통 HUD 조작을 통한 세 시점 프레임 재생·정지·배속·원본 캔버스 크기 검사.
- `RaceSpriteCameraCapture.Start()`: 네 구간 × 세 시점 화면 캡처. 숫자 순서는 구간별 사이드·후방·상공이다.
- `RaceSpriteWebGLBuild.Start()`: WebGL 플랫폼에서 검수용 일반 빌드. 첫 씬은 All2D이며 프로젝트의 씬 순서는 변경하지 않는다.

위 검증 도구는 수동 실행 시에만 동작한다. 생성된 임시 프레임·매트·빌드 출력은 게임에 들어가지 않는다. `Tools/SpriteAnimation`의 두 도구는 Project SGL의 후처리 도구를 이 프로젝트의 DataBundles 경로에 맞춰 복사한 것이다.

## 로컬 웹 미리보기

WebGL 플랫폼에서 Unity 메뉴 `Tools > Project Run > Race > Build WebGL review`를 실행하면 `Builds/WebGL_2DReview`에 검수용 빌드를 만든다. 빌드 출력은 Git에 포함하지 않는다. `webgl_build.json`의 `Status: Succeeded`를 확인한 뒤 프로젝트 루트 `Run`에서 실행한다.

```text
python Tools/Race2D/serve_webgl.py Builds/WebGL_2DReview 8768
```

브라우저에서 `http://127.0.0.1:8768`을 열면 All2D 경기로 진입한다. 서버는 이 컴퓨터의 루프백 주소에만 바인딩하며 Unity의 Brotli 압축 파일에 필요한 헤더를 제공한다. 이미 이 포트의 검수 서버가 실행 중이면 중복 실행하지 않는다.

일반 데스크톱에서는 중앙 세로 영역, 좁은 모바일 비율에서는 전체 높이를 사용한다. 브라우저 화면 크기 검사는 실제 Android/iOS 기기의 성능·메모리·터치 검사를 대신하지 않는다. 이 빌드는 세 씬과 공통 리소스를 포함한 기능 검수용이다.
