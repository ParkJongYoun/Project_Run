"""사용자 요청: 기존 원화 픽셀을 자르고 여백을 줄인다. 영상 생성/Unity 적용 없음."""
from pathlib import Path
import hashlib, json
from PIL import Image
import numpy as np

OUT = Path(__file__).resolve().parent
BASE = OUT.parent / "화풍통일_v1"
OLD = BASE / "후처리_v1"
for name in ("캐릭터", "환경패치", "접촉검수"):
    (OUT / name).mkdir(exist_ok=True)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

source_hashes = {}
records = []
def remember(path):
    source_hashes[str(path)] = digest(path)

def key_image(rgba):
    key = Image.new("RGBA", rgba.size, (255,0,255,255))
    key.alpha_composite(rgba)
    return key.convert("RGB")

# 화면 배치를 저장하는 큰 캔버스와 영상용 로컬 영역을 분리한다.
# 작은 후경 주자의 이동은 향후 합성 위치로 처리하고 긴 마젠타 이동 통로를 만들지 않는다.
char_specs = [
    ("우로스", "우로스", None, (352,-64,1376,960), "도끼를 올리는 위쪽 여백을 유지한 입력 후보"),
    ("와일드로즈", "와일드로즈", None, (352,-64,1632,960), "집사·메이드·고양이·소품 한 묶음"),
    ("마타", "마타", None, (672,224,1632,992), "호흡·귀·하품 동작 여백을 둔 입력 후보"),
    ("후경주자_두마리", "마타후경주자", None, (-24,336,296,592), "두 주자의 원래 상대 위치를 보존한 작은 묶음"),
    ("후경우로스_개별", "마타후경주자", (0,330,150,600), (-32,336,160,592), "원본 왼쪽에서 잘린 자세. 배치 확인용이며 온전한 시작 자세 보완 전 영상 생성 보류"),
    ("후경포링_개별", "마타후경주자", (150,400,320,580), (168,400,296,560), "작은 점프를 위한 위쪽 여백. 우로스와 독립 반복 가능"),
]
for label, name, scope, window, note in char_specs:
    path = OLD / (name + "_투명_v1.png")
    remember(path)
    full = Image.open(path).convert("RGBA")
    if scope is not None:
        isolated = Image.new("RGBA", full.size)
        isolated.paste(full.crop(scope), scope[:2])
        full = isolated
    bbox = full.getchannel("A").getbbox()
    assert bbox
    # 밀착 크롭은 검사/원본 관리용. 움직임 입력은 동작여백 파일을 사용한다.
    tight_box = (bbox[0]-8,bbox[1]-8,bbox[2]+8,bbox[3]+8)
    tight = full.crop(tight_box)
    local = full.crop(window)
    assert bbox[0] >= window[0] and bbox[1] >= window[1] and bbox[2] <= window[2] and bbox[3] <= window[3], (label,bbox,window)
    tight_name = "캐릭터/" + label + "_밀착크롭_투명.png"
    alpha_name = "캐릭터/" + label + "_동작여백_투명.png"
    key_name = "캐릭터/" + label + "_동작여백_마젠타.png"
    tight.save(OUT / tight_name)
    local.save(OUT / alpha_name)
    key_image(local).save(OUT / key_name)
    # 저장한 로컬 이미지를 원위치에 놓으면 해당 그룹의 원본 RGBA와 완전히 같아야 한다.
    restored = Image.new("RGBA", full.size)
    restored.paste(local, window[:2])
    assert np.array_equal(np.asarray(restored),np.asarray(full)), label
    records.append({
        "id":label,"kind":"character_crop","source":str(path),"sourceSize":list(full.size),
        "sourceGroupBounds":bbox,"tightCropBox":tight_box,"tightSize":list(tight.size),
        "cropBox":window,"workingSize":list(local.size),"finalVideoResolution":None,
        "sourcePixelScale":1.0,"pixelRedraw":False,
        "files":{"tightAlpha":tight_name,"motionAlpha":alpha_name,"motionMagenta":key_name},
        "sourceFrameRestoration":"exact RGBA match",
        "note":note,
        "sourceClippedAtLeft":label in ("후경주자_두마리","후경우로스_개별"),
        "readyForVideoGeneration":False,
        "generationPendingReason":"원본 왼쪽 잘림 보완 필요" if label in ("후경주자_두마리","후경우로스_개별") else "최종 출력 크기와 동작 여백 테스트 전"
    })

env_specs = [
    ("우로스_상단나뭇잎", "우로스_빈배경_v1.png", (128,0,512,224), "잎 끝만 약하게 흔들기. 가지·성·카메라 고정"),
    ("우로스_좌측풀", "우로스_빈배경_v1.png", (232,640,552,896), "꽃과 풀 끝의 작은 바람. 뒤쪽 울타리·땅 고정"),
    ("와일드로즈_커튼끝", "와일드로즈_빈배경_v1.png", (832,160,1088,608), "커튼의 늘어진 끝만 미세하게. 금색 침대 기둥·창문 고정"),
    ("마타_상단나뭇잎", "마타_빈배경_v1.png", (1120,0,1440,256), "큰 잎 끝만 미세하게. 나무·기둥·지붕 고정"),
    ("마타_좌측풀", "마타_빈배경_v1.png", (368,672,752,928), "풀과 들꽃 끝만 낮은 강도로. 땅·울타리 고정"),
]
for label, filename, box, note in env_specs:
    path = BASE / filename
    remember(path)
    full = Image.open(path).convert("RGB")
    patch = full.crop(box)
    name = "환경패치/" + label + "_배경포함.png"
    patch.save(OUT / name)
    w,h = patch.size
    yy,xx = np.indices((h,w))
    distance = np.minimum.reduce((xx, yy, w-1-xx, h-1-yy)).astype(np.float32)
    mask = np.clip(distance / 16,0,1)
    mask_name = "환경패치/" + label + "_합성경계마스크.png"
    Image.fromarray(np.rint(mask*255).astype(np.uint8)).save(OUT / mask_name)
    assert np.array_equal(np.asarray(Image.open(OUT/name)), np.asarray(full)[box[1]:box[3],box[0]:box[2]])
    records.append({
        "id":label,"kind":"opaque_environment_patch","source":str(path),"sourceSize":list(full.size),
        "cropBox":box,"workingSize":list(patch.size),"finalVideoResolution":None,"sourcePixelScale":1.0,
        "files":{"rgbPatch":name,"compositeEdgeMask":mask_name},
        "keying":"환경 패치는 주변 배경을 포함한다. 마젠타 키 제거 대상 아님.",
        "edgeMaskMeaning":"합성용 16px 경계 마스크. 영상 모델의 움직임을 자동 제어하는 마스크가 아님.",
        "motionNote":note,"pixelCropValidation":"exact pixel match",
        "readyForVideoGeneration":False,"generationPendingReason":"패치 바깥 테두리 고정·루프·출력 크기 테스트 전"
    })

contact_specs = [
    ("우로스_발밑과받침", "우로스_전체장면_v1.png", (400,768,1296,941), "발굽·받침의 접촉 그림자 참고"),
    ("와일드로즈_침대접점", "와일드로즈_화풍기준_원본.jpeg", (864,912,1840,1064), "고양이 발·배와 침구의 접촉 그림자 참고"),
    ("와일드로즈_집사가림", "와일드로즈_화풍기준_원본.jpeg", (496,752,992,912), "집사 하체를 침대가 가리는 경계 참고"),
    ("마타_몸과풀접점", "마타_전체장면_v1.png", (704,768,1600,941), "몸·발·꼬리와 풀밭의 접촉·가림 참고"),
]
for label,filename,box,note in contact_specs:
    path=BASE/filename
    remember(path)
    full=Image.open(path).convert("RGB")
    patch=full.crop(box)
    name="접촉검수/"+label+"_참고전용.png"
    patch.save(OUT/name)
    assert np.array_equal(np.asarray(Image.open(OUT/name)),np.asarray(full)[box[1]:box[3],box[0]:box[2]])
    records.append({"id":label,"kind":"contact_reference_only","source":str(path),"sourceSize":list(full.size),
        "cropBox":box,"workingSize":list(patch.size),"files":{"reference":name},
        "sourcePixelScale":1.0,"pixelCropValidation":"exact pixel match",
        "note":note+"; 캐릭터 일부가 포함된 참고 그림이므로 독립 영상/중복 합성에 사용하지 않는다.",
        "readyForVideoGeneration":False})
for path,sha in source_hashes.items():
    assert digest(Path(path))==sha, "기존 파일 변경 감지: "+path

manifest={
    "version":2,"date":"2026-10-17","status":"crops_prepared_only",
    "generationRun":False,"unityApplied":False,"originalFilesModified":False,
    "finalVideoResolution":None,"scalePolicy":"기존 픽셀 그대로 크롭. 업스케일·다운스케일 없음. 출력 크기는 테스트 후 별도 결정.",
    "placementRule":"full_scene_xy = cropped_xy + cropBox.xy. 생성 결과를 다른 크기로 받으면 크롭 좌표계로 환산 후 배치.",
    "runnerMotionRule":"후경 주자는 작은 캔버스에서 제자리 달리기·점프를 만든 후 배치 좌표로 이동. 전체 마젠타 화면을 이동 경로로 사용하지 않는다.",
    "mainCatRule":"집사·메이드·고양이·소품은 하나의 묶음 유지.",
    "environmentRule":"환경 패치는 캐릭터 아래 레이어에 둔다. 고정된 패치 경계와 원래 카메라·빛을 유지하는지 영상에서 확인.",
    "existingV1Scope":"기존 v1 파일과 Unity 리소스·씬·프리팹은 변경하지 않음",
    "sourceHashes":source_hashes,"crops":records
}
(OUT/"크롭기록.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps({"crops":[{"id":r["id"],"size":r["workingSize"],"kind":r["kind"]} for r in records],"sourceFilesUnchanged":True,"exactPixelValidation":True},ensure_ascii=False,indent=2))

