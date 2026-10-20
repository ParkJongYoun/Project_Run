from pathlib import Path
import subprocess
import re
import json
import io
import shutil
from PIL import Image, ImageDraw, ImageFont

work = Path(__file__).resolve().parent
source = Path("D:/임시/데모원본.mp4")
ffmpeg = next((work / "도구").rglob("ffmpeg*.exe"))
# This Windows FFmpeg build requires an ASCII relative media path.
local_source = work / "source.mp4"
if not local_source.exists():
    shutil.copyfile(source, local_source)
result = subprocess.run([str(ffmpeg), "-hide_banner", "-i", "source.mp4"],
                        capture_output=True, cwd=work)
info = result.stderr.decode("utf-8", errors="replace")
(work / "원본정보.txt").write_text(info, encoding="utf-8")
print(info)
duration_match = re.search(r"Duration: (\d+):(\d+):([\d.]+)", info)
if not duration_match:
    raise RuntimeError("Could not read source duration")
duration = sum(float(x) * y for x, y in zip(duration_match.groups(), [3600, 60, 1]))
times = list(range(0, int(duration), 5))
font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 20)
for page in range((len(times) + 23) // 24):
    group = times[page * 24:(page + 1) * 24]
    sheet = Image.new("RGB", (4 * 384, ((len(group) + 3) // 4) * 242), "#171c23")
    draw = ImageDraw.Draw(sheet)
    for idx, t in enumerate(group):
        frame = subprocess.run([
            str(ffmpeg), "-hide_banner", "-loglevel", "error", "-ss", str(t),
            "-i", "source.mp4", "-frames:v", "1",
            "-vf", "scale=384:216:force_original_aspect_ratio=decrease,pad=384:216:(ow-iw)/2:(oh-ih)/2",
            "-f", "image2pipe", "-vcodec", "mjpeg", "pipe:1"
        ], capture_output=True, check=True, cwd=work).stdout
        img = Image.open(io.BytesIO(frame))
        x, y = idx % 4 * 384, idx // 4 * 242
        sheet.paste(img, (x, y))
        draw.text((x + 8, y + 218), f"{t // 60:02d}:{t % 60:02d}", fill="white", font=font)
    path = work / f"원본장면표_{page + 1:02d}.jpg"
    sheet.save(path, quality=90)
    print(str(path))
