"""Render the imported frames at their runtime timing, without modifying assets."""
import json
import subprocess
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[2]
out = root / 'DataBundles/검증/Race2D_AutoSprite_20261017'
ffmpeg = root / 'Temp/media-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
capture = root / 'Temp/AutoSprite/capture'
times = [float(s) for s in (capture/'times.txt').read_text().splitlines()]
lines = []
for i in range(len(times)-1):
    lines.extend(["file 'frame_%04d.png'" % i, 'duration %.6f' % (times[i+1]-times[i])])
lines.append("file 'frame_%04d.png'" % (len(times)-1))
(capture/'frames.ffconcat').write_text('\n'.join(lines), encoding='utf-8')
subprocess.run([str(ffmpeg),'-y','-hide_banner','-loglevel','error','-f','concat','-safe','0','-i',str(capture/'frames.ffconcat'),'-vf','fps=30','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',str(out/'gameplay.mp4')],check=True)

font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',18)
bold = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',28)
small = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',15)
ids = ['Minorous','GiantPoring','Smokie','Savage','Lunatic','Edgar']
names = ['미노타우로스','자이언트 포링','스모키','새비지','루나틱','호랑이 에드가']
directions = ['북쪽 · N','북동쪽 · NE','동쪽 · E','남동쪽 · SE','남쪽 · S','남서쪽 · SW','서쪽 · W','북서쪽 · NW']
colors = ['#e1ad4e','#e48cad','#a58bc5','#d5915b','#70b2ca','#a1bd61']
frames = []
settings = []
for character in ids:
    folder = root/'Assets/ArtBundles/Race2D/AutoSprite'/character
    spec = json.loads((folder/'Import.json').read_text())
    atlas = Image.open(folder/'Run.png').convert('RGBA')
    views = []
    for direction in range(8):
        row = direction*spec['RowsPerDirection']
        views.append([atlas.crop(((f%8)*256,(row+f//8)*256,(f%8+1)*256,(row+f//8+1)*256)) for f in range(spec['DirectionFrameCounts'][direction])])
    frames.append(views)
    settings.append(spec)
process = subprocess.Popen([str(ffmpeg),'-y','-hide_banner','-loglevel','error','-f','rawvideo','-pixel_format','rgba','-video_size','1280x720','-framerate','30','-i','pipe:0','-an','-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(out/'monsters_8directions.mp4')],stdin=subprocess.PIPE)
for frame_index in range(480):
    time = frame_index/30
    direction = min(7,int(time/2))
    canvas = Image.new('RGBA',(1280,720),'#edf1e7')
    draw = ImageDraw.Draw(canvas)
    draw.text((32,20),'8방향 몬스터 달리기',font=bold,fill='#254735')
    draw.text((34,61),'새 2D 원화 · AutoSprite · 실제 적용 프레임과 재생 시간',font=small,fill='#637463')
    draw.rounded_rectangle((1040,24,1248,76),radius=15,fill='#254735')
    draw.text((1063,39),directions[direction],font=font,fill='#f7f1d8')
    for i in range(6):
        x,y=32+(i%3)*416,99+(i//3)*291
        draw.rounded_rectangle((x,y,x+384,y+273),radius=18,fill='#fafbf6',outline='#ced9c7',width=2)
        draw.ellipse((x+122,y+202,x+262,y+227),fill='#dce5d3')
        count = settings[i]['DirectionFrameCounts'][direction]
        f = int(time/settings[i]['Duration']*count)%count
        sprite = frames[i][direction][f]
        canvas.alpha_composite(sprite,(x+64,y-1))
        draw.rounded_rectangle((x+14,y+242,x+21,y+258),radius=3,fill=colors[i])
        draw.text((x+30,y+238),names[i],font=font,fill='#254735')
    draw.text((34,691),'PROJECT RUN  /  Scene_2D',font=small,fill='#637463')
    draw.text((1027,691),'8 directions · 6 monsters',font=small,fill='#637463')
    if frame_index==195:
        canvas.save(out/'monsters_preview.png')
    process.stdin.write(canvas.tobytes())
process.stdin.close()
if process.wait()!=0:
    raise RuntimeError('Preview encoding failed')
print('Saved gameplay.mp4, monsters_8directions.mp4 and monsters_preview.png')
