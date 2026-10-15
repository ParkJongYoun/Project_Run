"""Select sprite frames, with optional explicit scale/anchor and alpha cleanup."""
import argparse
import hashlib
import json
import math
from pathlib import Path
from PIL import Image

PROJECT = Path(__file__).resolve().parents[2]


def transform_frame(frame, clip, selected, output_cell):
    """Deterministic source-space cleanup, then ONE clip-wide affine transform.

    Cleanup rectangles are manually reviewed effect-only regions. No automatic
    per-frame bounding-box normalization or generation of missing art occurs.
    """
    for rule in clip.get("cleanup", []):
        if selected not in rule["frames"]:
            continue
        x0, y0, x1, y1 = rule["rect"]
        if not (0 <= x0 < x1 <= frame.width and 0 <= y0 < y1 <= frame.height):
            raise ValueError("Cleanup rectangle outside source cell")
        mode = rule["mode"]
        if mode not in ("erase", "fade_right", "fade_bottom"):
            raise ValueError("Unknown cleanup mode")
        pixels = frame.load()
        for y in range(y0, y1):
            for x in range(x0, x1):
                factor = 0 if mode == "erase" else ((x1-1-x)/max(1,x1-1-x0) if mode == "fade_right" else (y1-1-y)/max(1,y1-1-y0))
                # Smoothstep avoids an abrupt start to the effect fade.
                factor = factor * factor * (3 - 2 * factor)
                r, g, b, a = pixels[x, y]
                alpha = round(a * factor)
                pixels[x, y] = (r, g, b, alpha) if alpha else (0, 0, 0, 0)
    transform = clip.get("transform")
    if transform is None:
        if frame.size != (output_cell, output_cell):
            raise ValueError("Different output cell requires explicit transform")
        return frame
    scale = transform["scale"]
    if not math.isfinite(scale) or scale <= 0:
        raise ValueError("Scale must be finite and positive")
    sx, sy = transform["sourceAnchor"]
    tx, ty = transform["outputAnchor"]
    # Explicit reviewed drift corrections, in output pixels. Never infer these
    # from a frame's bounding box: deliberate jumps and falls must survive.
    offset = clip.get("frameOffsets", {}).get(str(selected), [0, 0])
    if len(offset) != 2 or any(not math.isfinite(v) for v in offset):
        raise ValueError("Frame offset must contain two finite coordinates")
    tx, ty = tx + offset[0], ty + offset[1]
    bbox = frame.getchannel("A").getbbox()
    if bbox:
        mapped = ((bbox[0]-sx)*scale+tx, (bbox[1]-sy)*scale+ty,
                  (bbox[2]-sx)*scale+tx, (bbox[3]-sy)*scale+ty)
        if mapped[0] < 0 or mapped[1] < 0 or mapped[2] > output_cell or mapped[3] > output_cell:
            raise ValueError("Transform would clip visible pixels; enlarge the output cell")
    # Integer translation at native scale must preserve even partial-alpha RGB.
    dx, dy = tx - sx, ty - sy
    if scale == 1 and float(dx).is_integer() and float(dy).is_integer():
        result = Image.new("RGBA", (output_cell, output_cell))
        result.paste(frame, (int(dx), int(dy)))
        return result
    # Premultiplied RGBa prevents hidden transparent RGB from creating halos.
    return frame.convert("RGBa").transform(
        (output_cell, output_cell), Image.Transform.AFFINE,
        (1 / scale, 0, sx - tx / scale, 0, 1 / scale, sy - ty / scale),
        resample=Image.Resampling.BICUBIC).convert("RGBA")


def confined(base, relative):
    path = (base / relative).resolve()
    if not path.is_relative_to(base.resolve()):
        raise ValueError(f"Path escapes configured directory: {relative}")
    return path


def process(manifest_path, check_only=False):
    config = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    if config.get("reviewStatus") == "draft":
        raise ValueError("Atlas draft requires visual review, PPU, pivot, loop and timing decisions before processing")
    source_dir = confined(PROJECT, config["sourceDirectory"])
    output_dir = confined(PROJECT, config["outputDirectory"])
    cell = config["cellSize"]
    if not isinstance(cell, int) or cell <= 0:
        raise ValueError("cellSize must be a positive integer")
    outputs = set()
    prepared = []
    for clip in config["clips"]:
        source_cell = clip.get("sourceCellSize", cell)
        output_cell = clip.get("outputCellSize", cell)
        if any(not isinstance(s, int) or s <= 0 for s in (source_cell, output_cell)):
            raise ValueError("Cell sizes must be positive integers")
        source = confined(source_dir, clip["source"])
        output = confined(output_dir, clip["file"])
        if output == source or output in outputs:
            raise ValueError("Output must be unique and different from source")
        outputs.add(output)
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        if digest != clip["sourceSha256"]:
            raise ValueError(f"Source changed: {source}")
        frames, holds = clip["sourceFrames"], clip["durations"]
        if not frames or len(frames) != clip["frameCount"] or len(holds) != len(frames):
            raise ValueError(f"Frame count mismatch: {clip['name']}")
        if any(t <= 0 for t in holds) or not math.isclose(sum(holds), clip["duration"], abs_tol=1e-6):
            raise ValueError("Invalid frame durations")
        if not -1 <= clip["impactFrame"] < len(frames):
            raise ValueError("Invalid impact frame")
        for axis in ("pivotX", "pivotY"):
            if not 0 <= clip[axis] <= 1:
                raise ValueError("Pivot must be normalized")
        with Image.open(source) as opened:
            original = opened.convert("RGBA")
        if original.width % source_cell or original.height % source_cell:
            raise ValueError("Source dimensions must be multiples of cellSize")
        source_columns = original.width // source_cell
        capacity = source_columns * (original.height // source_cell)
        columns = clip["columns"]
        if not isinstance(columns, int) or columns <= 0:
            raise ValueError("Invalid output columns")
        sheet = Image.new("RGBA", (columns * output_cell, math.ceil(len(frames) / columns) * output_cell))
        for index, selected in enumerate(frames):
            if not isinstance(selected, int) or not 0 <= selected < capacity:
                raise ValueError("Source frame index outside sheet")
            x, y = selected % source_columns * source_cell, selected // source_columns * source_cell
            frame = original.crop((x, y, x + source_cell, y + source_cell))
            if not frame.getchannel("A").getbbox():
                raise ValueError(f"Selected empty frame: {selected}")
            frame = transform_frame(frame, clip, selected, output_cell)
            if not frame.getchannel("A").getbbox():
                raise ValueError("Cleanup/transform removed the entire frame")
            sheet.paste(frame, (index % columns * output_cell, index // columns * output_cell))
        # Existing versions are immutable; new selections need a new filename/version.
        if output.exists():
            with Image.open(output) as opened:
                existing = opened.convert("RGBA")
            if existing.size != sheet.size or existing.tobytes() != sheet.tobytes():
                raise ValueError(f"Existing output differs; use a new version: {output}")
        elif check_only:
            raise FileNotFoundError(output)
        prepared.append((output, sheet, clip["name"]))
    # Validate every clip before writing any new output.
    for output, sheet, name in prepared:
        if not check_only and not output.exists():
            output.parent.mkdir(parents=True, exist_ok=True)
            sheet.save(output)
        print(f"PASS {name}: {output.name} (deterministic expected RGBA equality)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--check", action="store_true", help="Verify existing outputs without writing")
    args = parser.parse_args()
    process(args.manifest.resolve(), args.check)
