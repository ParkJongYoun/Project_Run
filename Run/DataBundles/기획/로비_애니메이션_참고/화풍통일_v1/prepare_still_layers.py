"""로비 마젠타 정지 원화의 후처리 검수. 영상 생성/프레임 매트 완료를 뜻하지 않음."""
from pathlib import Path
import sys, json, hashlib, zipfile
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[3]
sys.path.insert(0, str(PROJECT / "Tools" / "SpriteAnimation"))
from prepare_video_animation import magenta_matte

OUT = ROOT / "후처리_v1"
OUT.mkdir(exist_ok=True)
SETTINGS = {"keepExcess": 30, "clearExcess": 210, "unmix": 1.0}
# 원화 좌표계에서 한 번 정한 작업용 영역이다. 영상의 매 프레임을 자동 재정렬하지 않는다.
CROPS = {
    "우로스": (320, -96, 1376, 960),
    "와일드로즈": (320, -128, 1696, 960),
    "마타": (608, 128, 1664, 960),
    "마타후경주자": (-64, 288, 384, 608),
}
results, layers = [], {}

def remove_boundary_spill(picture, protected_region):
    """마젠타 배경과 맞닿는 최대 3px 경계의 RGB만 내부 색으로 보정한다."""
    rgba = np.array(picture)
    rgb = rgba[:, :, :3].astype(np.float32)
    alpha = rgba[:, :, 3]
    solid = alpha > 0
    inside = np.asarray(Image.fromarray(solid.astype(np.uint8) * 255).filter(ImageFilter.MinFilter(7))) > 0
    excess = np.minimum(rgb[:, :, 0], rgb[:, :, 2]) - rgb[:, :, 1]
    target = solid & ~inside & (excess > 5) & ~protected_region
    known = solid & ~target & (alpha >= 250)
    height, width = alpha.shape
    count = 0
    for step in range(3):
        totals, counts = np.zeros_like(rgb), np.zeros_like(alpha, dtype=np.float32)
        for dy, dx in ((-1,-1),(-1,0),(-1,1),(0,-1),(0,1),(1,-1),(1,0),(1,1)):
            y0, y1 = max(0,-dy), min(height,height-dy)
            x0, x1 = max(0,-dx), min(width,width-dx)
            weight = known[y0+dy:y1+dy,x0+dx:x1+dx]
            totals[y0:y1,x0:x1] += rgb[y0+dy:y1+dy,x0+dx:x1+dx] * weight[:,:,None]
            counts[y0:y1,x0:x1] += weight
        fill = target & ~known & (counts > 0)
        rgb[fill] = totals[fill] / counts[fill,None]
        known[fill] = True
        count += int(fill.sum())
    unresolved = target & ~known
    spill = np.maximum(np.minimum(rgb[:,:,0],rgb[:,:,2])-rgb[:,:,1],0)
    for channel in (0,2):
        rgb[:,:,channel][unresolved] -= spill[unresolved]
    rgba[:,:,:3] = np.clip(np.rint(rgb),0,255).astype(np.uint8)
    return Image.fromarray(rgba), count

for name, crop in CROPS.items():
    source = ROOT / (name + "_마젠타_생성원본_v1.png")
    rgb = Image.open(source).convert("RGB")
    values = np.asarray(rgb, dtype=np.int16)
    protection = Image.new("L", rgb.size)
    region = np.zeros((rgb.height, rgb.width), dtype=bool)
    if name in ("와일드로즈", "마타후경주자"):
        if name == "와일드로즈":
            region[520:850, 950:1210] = True
        else:
            region[440:540, 175:280] = True
        # 원화에서 확인한 분홍 리본/포링만 보호. 순수 키색에는 적용하지 않는다.
        pink = region & (values[:, :, 0] - values[:, :, 2] > 30) & (values[:, :, 0] - values[:, :, 1] > 35) & (values[:, :, 1] > 25)
        protection = Image.fromarray((pink * 255).astype(np.uint8))
        protection.save(OUT / (name + "_분홍보호마스크.png"))
    rgba = magenta_matte(rgb, SETTINGS, protection)
    # 생성 원본 배경의 미세한 색 편차로 생긴 희미한 고립 잔여물을 제거한다.
    # 불투명 그림에서 3px 이내의 안티앨리어싱은 그대로 둔다.
    matte = np.array(rgba)
    near_opaque = np.asarray(Image.fromarray((matte[:,:,3] >= 128).astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(7))) > 0
    noise = (matte[:,:,3] > 0) & (matte[:,:,3] < 24) & ~near_opaque
    removed_noise_pixels = int(noise.sum())
    matte[noise] = 0
    rgba = Image.fromarray(matte)
    rgba, corrected_boundary_pixels = remove_boundary_spill(rgba, region)
    rgba.save(OUT / (name + "_투명_v1.png"))
    layers[name] = rgba
    key = Image.new("RGBA", rgb.size, (255, 0, 255, 255))
    key.alpha_composite(rgba)
    key.convert("RGB").save(OUT / (name + "_마젠타정리_v1.png"))
    crop_alpha = rgba.crop(crop)
    crop_alpha.save(OUT / (name + "_영역분리_투명_v1.png"))
    crop_key = Image.new("RGBA", crop_alpha.size, (255, 0, 255, 255))
    crop_key.alpha_composite(crop_alpha)
    crop_key.convert("RGB").save(OUT / (name + "_영역분리_마젠타_v1.png"))
    # 검은색/흰색 배경에서 동일한 매트를 검수한다. 크기 변형은 하지 않는다.
    for label, color in (("검정", (0, 0, 0, 255)), ("흰색", (255, 255, 255, 255))):
        preview = Image.new("RGBA", rgb.size, color)
        preview.alpha_composite(rgba)
        preview.convert("RGB").save(OUT / (name + "_" + label + "검수.png"))
    alpha = np.asarray(rgba)[:, :, 3]
    protection_pixels = np.asarray(protection) > 0
    confident = Image.fromarray((alpha >= 128).astype(np.uint8) * 255).getbbox()
    results.append({
        "name": name, "source": source.name,
        "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
        "sourceSize": list(rgb.size), "foregroundBounds": confident,
        "cropBoxSourcePixels": crop, "cropSize": list(crop_alpha.size),
        "cropCoordinateRule": "source_xy = cropped_xy + cropBoxSourcePixels.xy; outside source is magenta/transparent padding",
        "cropIsFinalVideoResolution": False,
        "protectedPixels": int(protection_pixels.sum()),
        "additionalBoundaryCorrectedPixels": corrected_boundary_pixels,
        "backgroundNoisePixelsRemoved": removed_noise_pixels,
        "protectedPixelsAllOpaque": bool(np.all(alpha[protection_pixels] == 255)),
        "transparentPixels": int((alpha == 0).sum()),
        "partialPixels": int(((alpha > 0) & (alpha < 255)).sum()),
        "opaquePixels": int((alpha == 255).sum()),
        "notes": "후경 주자는 원본 왼쪽 경계에서 이미 잘려 있으며 영상 입력 확정 전 별도 동작 설계 필요" if name == "마타후경주자" else "동작 여백은 정지 원화 기준 초안; 영상 결과로 잘림 재검수",
    })
for name in ("우로스", "와일드로즈", "마타"):
    background = Image.open(ROOT / (name + "_빈배경_v1.png")).convert("RGBA")
    assert background.size == layers[name].size
    if name == "마타":
        background.alpha_composite(layers["마타후경주자"])
    background.alpha_composite(layers[name])
    background.convert("RGB").save(OUT / (name + "_합성검수_v1.png"))

# 제공 문서의 GIF는 수정 없이 복사하고 전 프레임 시간과 출처를 기록한다.
refs = ROOT / "동작참고"
refs.mkdir(exist_ok=True)
deck = Path("C:/Users/ParkJongYoun/Desktop/임시/RO_데모_플레이콘티_로비시안_v2.pptx")
gif_info = []
with zipfile.ZipFile(deck) as z:
    for part, name, page in (("ppt/media/image7.gif", "우로스_동작참고.gif", 9), ("ppt/media/image9.gif", "와일드로즈_동작참고.gif", 10)):
        path = refs / name
        path.write_bytes(z.read(part))
        im = Image.open(path)
        durations = []
        for i in range(im.n_frames):
            im.seek(i)
            durations.append(im.info.get("duration", 0))
            if i in (0, im.n_frames // 2, im.n_frames - 1):
                im.convert("RGB").save(refs / (path.stem + "_프레임" + str(i) + ".png"))
        gif_info.append({"sourceDeck": str(deck), "slide": page, "part": part, "file": "동작참고/" + name, "size": list(im.size), "frames": im.n_frames, "frameDurationsMs": durations, "totalDurationMs": sum(durations), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
manifest = {
    "date": "2026-10-17",
    "status": "still_art_and_static_matte_draft",
    "imageGenerationMode": "built_in_imagegen",
    "matteFunction": "Run/Tools/SpriteAnimation/prepare_video_animation.py:magenta_matte",
    "matteSettings": SETTINGS, "edgePropagationMaxPixels": 3,
    "backgroundNoiseRemoval": {"alphaBelow":24,"minimumOpaqueReferenceAlpha":128,"retainWithinPixels":3},
    "stillAlphaProcessingRun": True, "videoGenerationRun": False,
    "videoAlphaProcessingRun": False, "unityApplied": False,
    "finalGenerationResolution": None, "finalSpriteCellSize": None,
    "layers": results, "motionReferences": gif_info,
    "limitations": [
        "Generated layer extraction is a redraw, not a pixel-identical segmentation of the reference.",
        "Contact shadows and bed occlusion must be checked with actual motion.",
        "Pink protection masks are still-image masks; video requires per-frame tracked masks.",
        "No mobile browser or runtime memory measurement has been performed."
    ],
}
(OUT / "후처리검수.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"layers": results, "motionReferences": gif_info}, ensure_ascii=False, indent=2))

