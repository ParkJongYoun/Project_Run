from pathlib import Path
import json,sys
import av,numpy as np
from PIL import Image,ImageDraw,ImageFont

sys.stdout.reconfigure(encoding='utf-8')
path=Path(sys.argv[1])
root=path.parent.parent
label=sys.argv[2]
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',17)
frames=[]
with av.open(str(path)) as c:
    s=c.streams.video[0]
    for f in c.decode(s): frames.append(f.to_image().convert('RGB'))
    fps=float(s.average_rate)
    info={'파일':str(path),'가로':s.width,'세로':s.height,'프레임수':len(frames),'fps':fps,'길이초':len(frames)/fps,'오디오':len(c.streams.audio)>0,'코덱':s.codec_context.name}
sheet=Image.new('RGB',(512*4,314*4),'#14212b')
draw=ImageDraw.Draw(sheet)
indices=np.linspace(0,len(frames)-1,16,dtype=int)
for i,j in enumerate(indices):
    x,y=(i%4)*512,(i//4)*314
    im=frames[j].copy()
    im.thumbnail((512,288))
    sheet.paste(im,(x,y))
    draw.text((x+8,y+291),f'{label} / {j/fps:.2f}s / frame {j}',font=font,fill='white')
sheet.save(root/f'{label}_프레임검수.jpg',quality=94)
arrays=np.stack([np.asarray(f.resize((64,36)),dtype=np.float32) for f in frames])
info['평균_인접프레임변화']=float(np.abs(arrays[1:]-arrays[:-1]).mean())
info['검은프레임수']=int((arrays.mean(axis=(1,2,3))<2).sum())
info['흰프레임수']=int((arrays.mean(axis=(1,2,3))>253).sum())
assert info['가로']==512 and info['세로']==288
assert 0<info['길이초']<=7
assert info['프레임수']==158
(root/f'{label}_파일검증.json').write_text(json.dumps(info,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(info,ensure_ascii=False,indent=2))
