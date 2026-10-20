from pathlib import Path
import subprocess
import io
from PIL import Image, ImageDraw, ImageFont

work = Path(__file__).resolve().parent
ffmpeg = next((work / "도구").rglob("ffmpeg*.exe"))
font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 20)
groups = {
    "목장후반장면표": [16 + x * 0.5 for x in range(32)],
}
for name, times in groups.items():
    sheet = Image.new("RGB", (4 * 384, ((len(times) + 3) // 4) * 242), "#171c23")
    draw = ImageDraw.Draw(sheet)
    for idx, t in enumerate(times):
        result = subprocess.run([
            str(ffmpeg), "-hide_banner", "-loglevel", "error", "-ss", str(t),
            "-i", "source.mp4", "-frames:v", "1", "-vf", "scale=384:216",
            "-f", "image2pipe", "-vcodec", "mjpeg", "pipe:1"
        ], cwd=work, capture_output=True, check=True)
        img = Image.open(io.BytesIO(result.stdout))
        x, y = idx % 4 * 384, idx // 4 * 242
        sheet.paste(img, (x, y))
        draw.text((x + 8, y + 218), f"{t:.2f}s", fill="white", font=font)
    sheet.save(work / f"{name}.jpg", quality=91)
    print(name.encode("ascii", errors="backslashreplace").decode())
