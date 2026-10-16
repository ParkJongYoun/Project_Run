from pathlib import Path
import subprocess
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent
frames = root / 'VideoStudy'
frames.mkdir(exist_ok=True)
ffmpeg = r'C:\Users\ParkJongYoun\AppData\Local\Temp\run-video-tools\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe'
video = root.parent / '참고데이터' / '우마무스메_경기영상.mp4'
times = [77, 84, 96, 108, 124, 138, 167, 185, 207, 220, 230, 239]
sheet = Image.new('RGB', (1440, 1320), '#132b35')
draw = ImageDraw.Draw(sheet)
for index, seconds in enumerate(times):
    out = frames / f'frame_{seconds:03d}.png'
    subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-ss', str(seconds), '-i', str(video), '-frames:v', '1', '-y', str(out)], check=True)
    im = Image.open(out).convert('RGB')
    # Inspect the middle race scene rather than repeated phone UI; the originals remain intact.
    im = im.crop((0, 180, 590, 1010))
    im.thumbnail((345, 395))
    x, y = index % 4 * 360, index // 4 * 440
    sheet.paste(im, (x + (360-im.width)//2, y+30))
    draw.text((x+12, y+8), f'{seconds//60}:{seconds%60:02d}', fill='white')
sheet.save(frames / 'ContactSheet.jpg', quality=94)
print(frames / 'ContactSheet.jpg')
