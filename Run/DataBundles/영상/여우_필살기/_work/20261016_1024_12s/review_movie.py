import json, subprocess
from pathlib import Path
from PIL import Image, ImageDraw
work=Path(__file__).resolve().parent
video=work.parents[1]/'여우_필살기_천공여우불_1024.mp4'
frames=work/'review_frames'; frames.mkdir(exist_ok=True)
ffmpeg=r'C:\Users\ParkJongYoun\AppData\Local\Temp\run-video-tools\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe'
sheet=Image.new('RGB',(1600,1600),'#111827'); draw=ImageDraw.Draw(sheet)
for index in range(25):
    seconds=index*.5
    out=frames/f'{index:02d}.png'
    subprocess.run([ffmpeg,'-hide_banner','-loglevel','error','-ss',str(seconds),'-i',str(video),'-frames:v','1','-y',str(out)],check=True)
    im=Image.open(out).convert('RGB');im.thumbnail((312,288))
    x,y=index%5*320,index//5*320
    sheet.paste(im,(x+(320-im.width)//2,y+27));draw.text((x+9,y+8),f'{seconds:.1f}s',fill='white')
sheet.save(work/'review_contact.jpg',quality=95)
print(str(work/'review_contact.jpg'))
