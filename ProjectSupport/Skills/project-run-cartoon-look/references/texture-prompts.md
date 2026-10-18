# 원본 UV를 유지하는 카툰 알베도 편집

원본 atlas를 이미지 편집 입력으로 전달한다. 프롬프트의 보존 요청만으로 UV 정확도가 보장되지는 않으므로 결과는 실제 모델에서 확인한다. 여러 캐릭터를 한 이미지에 합쳐 생성하지 않는다.

## 입력과 실행

1. 원본 파일을 열어 UV 배치, 배경 여백, 실제 크기, 알파 유무를 확인한다.
2. 아래 `{...}` 항목을 대상 정보로 채운다. 1024 정사각은 기존 6종의 예시이며 다른 비율의 원본을 정사각으로 강제하지 않는다.
3. 로컬 원본은 `referenced_image_paths`로 전달하고, 모든 편집 대상 경로가 있으면 최근 이미지 개수 방식과 섞지 않는다. 현재 도구 설명에 맞춰 호출한다.
4. `transparent_background`는 원본 알파 보존 요구에 맞춘다. 기존 6종 atlas는 불투명이다. 새 대상에 알파가 있으면 불투명 프롬프트를 복사하지 않는다.
5. 출력 파일을 프로젝트에 저장하고 원본과 같은 정규화 좌표의 눈·입·UV 섬이 유지되는지 실제 모델로 점검한다. 사용자 요청 없이 별도 Python 이미지 수정으로 결과를 바꾸지 않는다.

## 기본 영문 프롬프트

```text
Use case: style-transfer. Asset type: production 3D character UV albedo texture atlas.
Edit the supplied source atlas IN PLACE. Output one atlas with the same aspect ratio and canvas framing as the source; target dimensions: {dimensions}.
Character and palette: {identity, main local colors, distinguishing facial features, costume and material regions}.

Match the approved Project Run cartoon character look: clean harmonious anime-game local colors, restrained baked shading, two or three broad gentle value groups, crisp important facial features, and soft controlled color transitions. Reduce painted directional highlights, deep baked shadows, tiny fur strokes, noisy muscle shading and scratches. The real-time shader will provide form and lighting. Preserve the original character identity and palette. Avoid plastic shine.

CRITICAL: this image is a UV texture atlas for an unchanged mesh, NOT a character illustration. Keep every UV island outline, position, normalized scale, rotation, orientation, spacing and padding exactly aligned with the source. Preserve eyes, mouth, teeth, nostrils, markings, seams and ornaments at the exact same normalized coordinates. Do not rearrange, enlarge, crop, add, remove or invent islands. Paint over the existing image. Do not generate a posed character or a background scene.

Preserve the existing empty background color and padding. {alpha requirement}.
Do not add text, labels, grids, watermarks, lighting glare or cast shadows. Deliver a clean albedo atlas for the same cel-shaded lighting as the approved Minorous look.
```

불투명 원본의 `{alpha requirement}`: `Opaque PNG; preserve the source's opaque alpha, with no transparency change.`

알파가 있는 원본은 대신 `Preserve the source alpha mask and all cutout boundaries exactly; do not fill transparent regions or create new holes.`를 사용한다. 프롬프트와 도구의 투명 배경 설정이 모순되지 않게 한다.

## 캐릭터별 색·질감 기준

| 대상 | 유지할 특징과 정리 방향 |
|---|---|
| 미노 | 따뜻한 캐러멜 피부, 붉은 눈, 버건디 가죽, 차분한 은색 갑옷, 아이보리 뿔, 큰 덩어리의 짙은 갈색 갈기. 근육에 그려진 깊은 명암을 특히 줄임 |
| 루나틱 | 아이보리 털과 옅은 라벤더 그림자. 얼굴 특징을 보존하고 흰 털의 과노출을 억제 |
| 마타 | 따뜻한 엄버 털, 부드러운 자주색 그림자, 구리색 갈색 털 덩어리, 붉고 노란 눈. 눈·이빨·어두운 코와 귀 위치 유지 |
| 스모키 | 금빛 갈색 털, 크림 배색, 어두운 눈 주변 무늬, 초록 잎. 잔털을 넓은 색면으로 정리 |
| 새비지 | 갈색 몸통과 크림빛 갈기·엄니. 코·눈·털 경계를 유지하며 거친 명암을 정리 |
| 오크 | 올리브 녹색 피부, 버건디 의상, 갈색 나무·가죽, 은색 갑옷, 아이보리 이빨. 근육 명암과 금속 광택을 절제 |
| 자이언트 포링 | 딸기빛 분홍 젤리와 작은 검은 눈·미소, 초콜릿색 가죽 가방·끈, 은색 버클. 분홍색의 강한 baked 명암과 가죽 광택을 줄이고 얼굴 좌표를 보존 |
| 호랑이 에드가 | 꿀빛 주황 털, 아이보리 주둥이·목털, 짙은 갈색 줄무늬, 붉은 천과 금색 장식. 얼룩진 털 명암을 넓은 색면으로 정리하고 줄무늬·발바닥·얼굴 좌표를 보존 |

## 미노에서 사용한 강한 명암 정리 문구

아래는 기존 미노의 매우 강한 baked shading을 줄일 때 사용한 표현이다. 80%는 그 이미지에 대한 스타일 지시이며 측정된 수치나 모든 모델의 의무 기준이 아니다.

```text
Make a DRAMATIC change to the paint style: clean flat Japanese anime game base color / diffuse albedo. Remove 80 percent of all painted directional lighting, dark shadows, bright muscle highlights and brush texture from the skin. The cheek, forehead, neck, chest and arms must be mostly even warm caramel tan midtones with only small crisp dark creases marking form. The shader will supply the shape and lighting. Do not make the muscles sculpted with paint. No painterly brush strokes or faceted painted highlights. Use broad flat calm color shapes. Flatten the body while preserving anatomical line placement. Face red eye and nostril remain precisely in original location. Brown fur becomes clean large dark brown clumps with just a few lighter tips, not thousands of tiny noisy strokes. Leather is clean burgundy with seams; silver armor is calm cool grey with fine edging and no noisy scratches; horn is ivory with very faint gradient.
```

## 재현 기록

실제 사용한 전체 프롬프트와 원본/출력 경로는 다음에서 읽는다. 예시 문구를 이미 실행한 프롬프트라고 기록하지 않는다.

- 미노: `Run/DataBundles/LookDev/미노타우로스_카툰외형_20261017/`의 프롬프트 버전과 `작업기록.txt`.
- 나머지 5종: `Run/DataBundles/LookDev/몬스터6종_카툰외형_20261017/texture_generation.json`.
- 포링·에드가 교체: `Run/DataBundles/LookDev/자이언트포링_에드가_카툰교체_20261017/texture_generation.json`. 두 원본은 `Assets/OldAsset/Mounts/giantporing_001/Materials/giantporing_001.png`, `Assets/OldAsset/Mounts/vehicle_edgar_001/material/vehicle_edgar_001.png`이며 기존 재질을 덮어쓰지 않는다.

다음 작업에서도 대상별 원본 경로, 최종 프롬프트, 사용 도구, 출력 경로, 실제 이미지 크기와 Unity 임포트 크기, 결과 해시를 작업 폴더에 남긴다. 같은 프롬프트도 동일한 픽셀을 보장하지 않으므로 승인 결과 파일을 우선 재사용한다.
