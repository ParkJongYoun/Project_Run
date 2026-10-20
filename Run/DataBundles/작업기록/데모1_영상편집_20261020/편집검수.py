from pathlib import Path
import subprocess
import re
import io
import json
import hashlib
import numpy as np
from PIL import Image, ImageDraw, ImageFont

work = Path(__file__).resolve().parent
ffmpeg = next((work / "도구").rglob("ffmpeg*.exe"))
encoded = work / "highlight.mp4"
final = work.parents[1] / "영상" / "데모1_하이라이트" / "데모1_하이라이트_19초.mp4"

decode = subprocess.run([
    str(ffmpeg), "-hide_banner", "-i", "highlight.mp4",
    "-map", "0:v:0", "-map", "0:a:0", "-f", "null", "-"
], cwd=work, capture_output=True)
log = decode.stderr.decode("utf-8", errors="replace")
(work / "전체디코딩검증.txt").write_text(log, encoding="utf-8")
assert decode.returncode == 0, log[-2000:]
print(log[-1400:])

times = [
    0, 1, 1.6, 1.9, 2.2, 3.8, 5.4, 5.7,
    6.0, 7.5, 9.2, 9.5, 9.8, 11, 12.6, 12.9,
    13.2, 14.5, 16.5, 17.6, 18.2, 18.6, 18.9
]
font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 19)
sheet = Image.new("RGB", (4 * 384, ((len(times) + 3) // 4) * 242), "#151c24")
draw = ImageDraw.Draw(sheet)
for i, time in enumerate(times):
    result = subprocess.run([
        str(ffmpeg), "-hide_banner", "-loglevel", "error", "-ss", str(time),
        "-i", "highlight.mp4", "-frames:v", "1", "-vf", "scale=384:216",
        "-f", "image2pipe", "-vcodec", "mjpeg", "-"
    ], cwd=work, capture_output=True, check=True)
    frame = Image.open(io.BytesIO(result.stdout))
    x, y = i % 4 * 384, i // 4 * 242
    sheet.paste(frame, (x, y))
    draw.text((x + 7, y + 218), f"OUTPUT {time:05.2f}s", fill="white", font=font)
sheet.save(work / "편집본_장면전환검수.jpg", quality=91)

motion_result = subprocess.run([
    str(ffmpeg), "-hide_banner", "-loglevel", "error", "-i", "highlight.mp4",
    "-vf", "scale=160:90,format=gray", "-f", "rawvideo", "-pix_fmt", "gray", "-"
], cwd=work, capture_output=True, check=True)
frames = np.frombuffer(motion_result.stdout, dtype=np.uint8).reshape(-1, 90, 160)
diffs = np.abs(frames[1:].astype(np.float32) - frames[:-1]).mean(axis=(1, 2))
luma = frames.mean(axis=(1, 2))
longest_freeze = current = 0
for diff in diffs:
    current = current + 1 if diff < 0.05 else 0
    longest_freeze = max(longest_freeze, current)

record = {
    "full_decode_passed": True,
    "decoded_video_frames": len(frames),
    "container_duration_seconds": 19.0,
    "video_duration_seconds": len(frames) / 30,
    "longest_near_identical_run_seconds": longest_freeze / 30,
    "black_frames_before_ending_fade": int((luma[:int(18.6 * 30)] < 4).sum()),
    "file_bytes": final.stat().st_size,
    "output_matches_verified_encode": hashlib.sha256(final.read_bytes()).digest()
                                      == hashlib.sha256(encoded.read_bytes()).digest(),
    "sha256": hashlib.sha256(final.read_bytes()).hexdigest(),
}
(work / "검증결과.json").write_text(
    json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8"
)
print(json.dumps(record, indent=2))
