"""영상 원본 프레임/시간 보존, 마젠타 매트 초안, 검수된 시트 조립 진입점."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import statistics

import numpy as np
from PIL import Image

from optimize_sheets import process

PROJECT = Path(__file__).resolve().parents[2]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path, value):
    with path.open("w", encoding="utf-8-sig", newline="") as stream:
        stream.write(json.dumps(value, ensure_ascii=False, indent=2).replace("\n", "\r\n") + "\r\n")


def data_path(path):
    # 제작 파일은 현재 체크아웃의 DataBundles 안으로 한정한다.
    result = path.resolve()
    if not result.is_relative_to((PROJECT / "DataBundles").resolve()):
        raise ValueError("Production paths must be inside this project's DataBundles")
    return result


def relative(path):
    return path.resolve().relative_to(PROJECT).as_posix()


def file_part(name):
    if not name or name in (".", "..") or any(c in name for c in '<>:"/\\|?*'):
        raise ValueError("Expected a single filename component")
    return name


def fresh_directory(path):
    data_path(path)
    if path.exists():
        raise FileExistsError(f"Use a new work/version directory: {path}")
    path.mkdir(parents=True)


def frame_durations(times, last_duration=None):
    if not times or any(not math.isfinite(t) for t in times):
        raise ValueError("Missing or invalid presentation timestamps")
    durations = [b - a for a, b in zip(times, times[1:])]
    if any(t <= 0 for t in durations):
        raise ValueError("Presentation timestamps must increase")
    inferred = last_duration is None or not math.isfinite(last_duration) or last_duration <= 0
    if inferred:
        if not durations:
            raise ValueError("Cannot infer duration of a single frame")
        last_duration = statistics.median(durations)
    return durations + [last_duration], inferred


def extract(source, job, motion, output_directory):
    import av

    source, job = data_path(source), data_path(job)
    output_directory = data_path(output_directory)
    file_part(motion)
    source_hash = digest(source)
    with av.open(str(source)) as container:
        if not container.streams.video:
            raise ValueError("Input has no video stream")
        video = container.streams.video[0]
        fresh_directory(job)
        raw = job / "raw"
        raw.mkdir()
        records, times, size = [], [], None
        last_duration = None
        for index, frame in enumerate(container.decode(video=0)):
            if frame.pts is None or frame.time_base is None:
                raise ValueError("Frame has no presentation timestamp")
            timestamp = float(frame.pts * frame.time_base)
            picture = frame.to_image().convert("RGB")
            if size is not None and picture.size != size:
                raise ValueError("Frame dimensions changed within the video")
            size = picture.size
            path = raw / f"frame_{index:05d}.png"
            picture.save(path)
            duration = getattr(frame, "duration", None)
            last_duration = float(duration * frame.time_base) if duration else None
            records.append({"index": index, "file": path.relative_to(job).as_posix(),
                            "sha256": digest(path), "pts": frame.pts,
                            "timeBase": str(frame.time_base), "sourceTime": timestamp})
            times.append(timestamp)
        durations, inferred = frame_durations(times, last_duration)
        for record, duration in zip(records, durations):
            record.update(time=record["sourceTime"] - times[0], duration=duration)
        if digest(source) != source_hash:
            raise ValueError("Source video changed during extraction")
        metadata = {"version": 1, "source": relative(source), "sourceSha256": source_hash,
                    "motion": motion, "sourceSize": list(size), "frameCount": len(records),
                    "nominalFps": float(video.average_rate) if video.average_rate else None,
                    "duration": sum(durations), "lastDurationInferred": inferred,
                    "outputDirectory": relative(output_directory), "frames": records}
        write_json(job / "frames.json", metadata)
        print(json.dumps({"motion": motion, "size": size, "frames": len(records),
                          "duration": sum(durations), "lastDurationInferred": inferred}))
        return metadata


def magenta_matte(picture, settings, protection=None):
    # 배경과 같은 색인 장비는 색만으로 구별할 수 없어 검수한 보호 마스크를 우선한다.
    keep, clear = settings.get("keepExcess", 30), settings.get("clearExcess", 210)
    strength = settings.get("unmix", 1.0)
    if not (0 <= keep < clear <= 255 and 0 <= strength <= 1):
        raise ValueError("Invalid magenta thresholds/unmix strength")
    rgb = np.asarray(picture.convert("RGB"), dtype=np.float32)
    excess = np.minimum(rgb[:, :, 0], rgb[:, :, 2]) - rgb[:, :, 1]
    alpha = np.clip((clear - excess) / (clear - keep), 0, 1)
    if protection is not None:
        if protection.size != picture.size:
            raise ValueError("Protection mask must match source dimensions")
        alpha = np.maximum(alpha, np.asarray(protection.convert("L"), dtype=np.float32) / 255)
    # 키색 역합성은 낮은 알파에서 녹색 과보정을 만들 수 있다.
    # 주변 불투명 그림의 RGB를 경계로만 전파하고, 떨어진 경계는 과잉 마젠타만 줄인다.
    recovered = rgb.copy()
    known = alpha >= 1
    partial = (alpha > 0) & (alpha < 1)
    height, width = alpha.shape
    for _ in range(3):
        totals = np.zeros_like(rgb)
        counts = np.zeros_like(alpha)
        for dy, dx in ((-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)):
            y0, y1 = max(0, -dy), min(height, height - dy)
            x0, x1 = max(0, -dx), min(width, width - dx)
            weight = known[y0 + dy:y1 + dy, x0 + dx:x1 + dx]
            totals[y0:y1, x0:x1] += recovered[y0 + dy:y1 + dy, x0 + dx:x1 + dx] * weight[:, :, None]
            counts[y0:y1, x0:x1] += weight
        fill = partial & ~known & (counts > 0)
        if not np.any(fill):
            break
        recovered[fill] = totals[fill] / counts[fill, None]
        known[fill] = True
    # 주변 기준색이 없는 얇은 선은 G를 증폭하지 않고 공통 R/B 과잉분만 제거한다.
    unresolved = partial & ~known
    spill = np.maximum(np.minimum(rgb[:, :, 0], rgb[:, :, 2]) - rgb[:, :, 1], 0)
    for channel in (0, 2):
        recovered[:, :, channel][unresolved] -= spill[unresolved]
    corrected = rgb + strength * (recovered - rgb)
    rgba = np.dstack((np.rint(corrected).astype(np.uint8), np.rint(alpha * 255).astype(np.uint8)))
    rgba[rgba[:, :, 3] == 0, :3] = 0
    return Image.fromarray(rgba)


def create_matte(job, version, settings_path=None):
    job = data_path(job)
    info = read_json(job / "frames.json")
    source = data_path(PROJECT / info["source"])
    if digest(source) != info["sourceSha256"]:
        raise ValueError("Source video hash mismatch")
    settings = read_json(settings_path) if settings_path else {}
    unknown = set(settings) - {"keepExcess", "clearExcess", "unmix", "protectionMasks"}
    if unknown:
        raise ValueError(f"Unknown matte settings: {sorted(unknown)}")
    masks = settings.get("protectionMasks", {})
    valid_indices = {str(f["index"]) for f in info["frames"]}
    if not isinstance(masks, dict) or set(masks) - valid_indices:
        raise ValueError("Protection masks must use valid source-frame indices")
    # 입력 해시와 마스크 크기부터 검사한다. 마스크는 작업 폴더 상대 경로다.
    for record in info["frames"]:
        raw = (job / record["file"]).resolve()
        if not raw.is_relative_to(job) or digest(raw) != record["sha256"]:
            raise ValueError("Raw frame path/hash mismatch")
        if str(record["index"]) in masks:
            mask_path = (job / masks[str(record["index"]) ]).resolve()
            if not mask_path.is_relative_to(job):
                raise ValueError("Protection mask escapes job directory")
            with Image.open(mask_path) as mask:
                if list(mask.size) != info["sourceSize"]:
                    raise ValueError("Protection mask size mismatch")
    destination = job / file_part(version)
    fresh_directory(destination)
    cell = max(info["sourceSize"])
    count = len(info["frames"])
    columns = min(8, count)
    # 비정사각 영상도 좌상단 원본 좌표를 유지하여 정사각 셀에 투명 패딩만 추가한다.
    atlas = Image.new("RGBA", (columns * cell, math.ceil(count / columns) * cell))
    metrics, mask_hashes = [], {}
    for record in info["frames"]:
        index = record["index"]
        with Image.open(job / record["file"]) as original:
            mask = None
            if str(index) in masks:
                mask_path = job / masks[str(index)]
                with Image.open(mask_path) as opened:
                    mask = opened.convert("L")
                mask_hashes[str(index)] = digest(mask_path)
            keyed = magenta_matte(original, settings, mask)
            alpha = np.asarray(keyed)[:, :, 3]
            metrics.append({"sourceFrame": index, "alphaBounds": keyed.getchannel("A").getbbox(),
                            "transparentFraction": float(np.mean(alpha == 0)),
                            "opaqueFraction": float(np.mean(alpha == 255)),
                            "touchesBorder": bool(np.any(alpha[0]) or np.any(alpha[-1]) or
                                                  np.any(alpha[:, 0]) or np.any(alpha[:, -1]))})
            atlas.paste(keyed, (index % columns * cell, index // columns * cell))
    atlas_path = destination / "source.png"
    atlas.save(atlas_path)
    clip = {"name": info["motion"], "source": "source.png", "sourceSha256": digest(atlas_path),
            "sourceCellSize": cell, "outputCellSize": 800,
            "sourceFrameCount": count, "sourceFrames": list(range(count)),
            "durations": [r["duration"] for r in info["frames"]], "duration": info["duration"],
            "frameCount": count, "columns": min(5, count), "file": info["motion"] + "_v1.png",
            "loop": None, "pivotX": .5, "pivotY": .25, "impactFrame": -1,
            "transform": {"scale": 1.0, "sourceAnchor": None, "outputAnchor": [400, 600]}}
    manifest = {"pipeline": "sgl-video-animation-v1", "reviewStatus": "draft",
                "review": {key: "pending" for key in ("alpha", "appearance", "motion", "alignment")},
                "cellSize": 800, "pixelsPerUnit": None, "maxTextureSize": 4096,
                "sourceDirectory": relative(destination), "outputDirectory": info["outputDirectory"],
                "clips": [clip], "video": {"framesMetadata": relative(job / "frames.json"),
                    "sourceSha256": info["sourceSha256"], "matteSettings": settings,
                    "protectionMaskHashes": mask_hashes,
                    "matteAlgorithm": "magenta-local-color-v1", "toolSha256": digest(Path(__file__))}}
    write_json(destination / "manifest.draft.json", manifest)
    write_json(destination / "matte-inspection.json", {"reviewStatus": "draft", "frames": metrics})
    print(json.dumps({"frames": count, "sourceCellSize": cell, "atlasSize": atlas.size,
                      "draft": relative(destination / "manifest.draft.json")}))
    return manifest


def validate_reviewed(config):
    if config.get("pipeline") != "sgl-video-animation-v1" or config.get("reviewStatus") != "reviewed":
        raise ValueError("A reviewed video-animation manifest is required")
    if any(config.get("review", {}).get(k) != "passed" for k in ("alpha", "appearance", "motion", "alignment")):
        raise ValueError("Complete alpha, appearance, motion and alignment review first")
    ppu = config.get("pixelsPerUnit")
    if not isinstance(ppu, (float, int)) or not math.isfinite(ppu) or ppu <= 0:
        raise ValueError("Reviewed pixelsPerUnit is required")
    data_path(PROJECT / config["sourceDirectory"])
    data_path(PROJECT / config["outputDirectory"])
    limit = config["maxTextureSize"]
    if not isinstance(limit, int) or limit <= 0 or not config.get("clips"):
        raise ValueError("Invalid texture limit or empty clips")
    for clip in config["clips"]:
        frames, durations = clip["sourceFrames"], clip["durations"]
        if not frames or len(frames) != clip["frameCount"] or len(durations) != len(frames):
            raise ValueError("Frame/duration count mismatch")
        if any(not isinstance(f, int) or not 0 <= f < clip["sourceFrameCount"] for f in frames):
            raise ValueError("Selected source frame exceeds valid frames")
        if any(b <= a for a, b in zip(frames, frames[1:])):
            raise ValueError("Select forward motion frames; do not duplicate/reverse poses for loops")
        if any(not math.isfinite(t) or t <= 0 for t in durations):
            raise ValueError("Frame durations must be finite and positive")
        if not math.isfinite(clip["duration"]) or not math.isclose(sum(durations), clip["duration"], abs_tol=1e-6):
            raise ValueError("Total duration must match selected frame holds")
        if not isinstance(clip["loop"], bool):
            raise ValueError("Decide loop behavior")
        if any(not isinstance(clip[k], (float, int)) or not math.isfinite(clip[k]) or
               not 0 <= clip[k] <= 1 for k in ("pivotX", "pivotY")):
            raise ValueError("Reviewed pivot required")
        transform = clip.get("transform") or {}
        scale = transform.get("scale")
        if not isinstance(scale, (float, int)) or not math.isfinite(scale) or scale <= 0:
            raise ValueError("Reviewed fixed scale required")
        for key in ("sourceAnchor", "outputAnchor"):
            value = transform.get(key)
            if not isinstance(value, list) or len(value) != 2 or any(not math.isfinite(v) for v in value):
                raise ValueError("Reviewed anchor required")
        cell, columns = clip["outputCellSize"], clip["columns"]
        if any(not isinstance(v, int) or v <= 0 for v in (cell, columns)):
            raise ValueError("Invalid output cell/columns")
        expected = [cell * clip["pivotX"], cell * (1 - clip["pivotY"])]
        if any(abs(a - b) > 1e-4 for a, b in zip(expected, transform["outputAnchor"])):
            raise ValueError("Output anchor and Unity pivot disagree")
        if max(columns * cell, math.ceil(len(frames) / columns) * cell) > limit:
            raise ValueError("Sheet exceeds maxTextureSize; revise columns/cell/frame selection")


def build(manifest_path, check_only=False):
    config = read_json(manifest_path)
    validate_reviewed(config)
    # 기존 처리기의 해시, 잘림 거부, 단일 리샘플링, 덮어쓰기 방지를 재사용한다.
    process(manifest_path, check_only)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    decode = commands.add_parser("extract", help="Keep every original frame and presentation time")
    decode.add_argument("--source", type=Path, required=True)
    decode.add_argument("--job", type=Path, required=True)
    decode.add_argument("--motion", required=True)
    decode.add_argument("--output-directory", type=Path, required=True)
    matte = commands.add_parser("matte", help="Create a REVIEW-REQUIRED magenta matte atlas")
    matte.add_argument("--job", type=Path, required=True)
    matte.add_argument("--version", required=True)
    matte.add_argument("--settings", type=Path)
    assemble = commands.add_parser("build", help="Validate and assemble a reviewed manifest")
    assemble.add_argument("manifest", type=Path)
    assemble.add_argument("--check", action="store_true")
    args = parser.parse_args()
    if args.command == "extract":
        extract(args.source, args.job, args.motion, args.output_directory)
    elif args.command == "matte":
        create_matte(args.job, args.version, args.settings)
    else:
        build(args.manifest.resolve(), args.check)


if __name__ == "__main__":
    main()
