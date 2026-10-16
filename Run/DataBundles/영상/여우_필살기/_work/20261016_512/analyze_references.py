import json, re, subprocess
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
FFMPEG = Path(r'C:\Users\ParkJongYoun\AppData\Local\Temp\run-video-tools\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe')
SOURCE = Path(r'D:\Project_Run\Run\DataBundles\참고데이터')
records = []
for video in sorted(SOURCE.glob('183*.mp4')):
    info = subprocess.run([str(FFMPEG), '-hide_banner', '-i', str(video)], capture_output=True, text=True, encoding='utf-8', errors='replace').stderr
    match = re.search(r'Duration: (\d+):(\d+):([\d.]+)', info)
    seconds = int(match[1])*3600 + int(match[2])*60 + float(match[3])
    folder = ROOT / 'reference_frames' / video.stem
    folder.mkdir(parents=True, exist_ok=True)
    sheet = Image.new('RGB', (1280, 720), '#111827')
    draw = ImageDraw.Draw(sheet)
    times = [seconds * f for f in [0.02, .14, .27, .40, .53, .66, .80, .94]]
    for i, time in enumerate(times):
        frame = folder / f'{i:02d}.png'
        subprocess.run([str(FFMPEG), '-hide_banner', '-loglevel', 'error', '-ss', str(time), '-i', str(video), '-frames:v', '1', '-y', str(frame)], check=True)
        im = Image.open(frame).convert('RGB')
        im.thumbnail((316, 322))
        x, y = i%4*320, i//4*360
        sheet.paste(im, (x+(320-im.width)//2, y+30))
        draw.text((x+8,y+8), f'{time:.2f}s', fill='white')
    out = folder / 'contact.jpg'
    sheet.save(out, quality=95)
    records.append({'source':str(video),'seconds':seconds,'metadata':info,'sample_times':times,'contact':str(out)})
(ROOT/'references.json').write_text(json.dumps(records,ensure_ascii=False,indent=2), encoding='utf-8')
print(json.dumps([{'name':Path(r['source']).name,'seconds':r['seconds'],'contact':r['contact']} for r in records], ensure_ascii=False))
