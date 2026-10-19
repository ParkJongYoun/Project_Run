from pathlib import Path
import json, sys, zipfile, xml.etree.ElementTree as ET
import av
from PIL import Image, ImageDraw, ImageFont

sys.stdout.reconfigure(encoding='utf-8')
root = Path(__file__).parent
font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 18)
records = []
for path in sorted(Path('D:/Project_Run/Run/DataBundles/참고데이터/우마무스메').glob('필살기연출*.mp4')):
    with av.open(str(path)) as container:
        stream = container.streams.video[0]
        duration = float(stream.duration * stream.time_base) if stream.duration else container.duration / 1000000
        times = [duration * i / 12 for i in range(12)]
        sheet = Image.new('RGB', (320*4, 206*3), '#15202b')
        draw = ImageDraw.Draw(sheet)
        i = 0
        for frame in container.decode(stream):
            t = float(frame.time or 0)
            if i < len(times) and t >= times[i]:
                img = frame.to_image().convert('RGB')
                img.thumbnail((320,180))
                x,y = (i%4)*320,(i//4)*206
                sheet.paste(img,(x+(320-img.width)//2,y))
                draw.text((x+8,y+182), f'{path.stem} / {t:.2f}s',font=font,fill='white')
                i += 1
            if i == len(times): break
        sheet.save(root / f'{path.stem}_프레임검수.jpg',quality=90)
        records.append(dict(name=path.name,width=stream.width,height=stream.height,duration=duration,fps=str(stream.average_rate)))

ns = {'a':'http://schemas.openxmlformats.org/drawingml/2006/main'}
with zipfile.ZipFile('C:/Users/ParkJongYoun/Desktop/임시/심사데모_플레이콘티.pptx') as z:
    slide = ET.fromstring(z.read('ppt/slides/slide50.xml'))
    text = '\n'.join(t.text or '' for t in slide.findall('.//a:t',ns))
    (root/'콘티_미노_발췌.txt').write_text(text,encoding='utf-8')
    rels=ET.fromstring(z.read('ppt/slides/_rels/slide50.xml.rels'))
    for rel in rels:
        if rel.attrib['Type'].endswith('/image'):
            target=rel.attrib['Target']
            member=target.lstrip('/') if target.startswith('/') else 'ppt/'+target.replace('../','')
            out=root/('콘티50_'+Path(member).name)
            out.write_bytes(z.read(member))
            print('slide image',out)
(root/'참고영상_정보.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(records,ensure_ascii=False,indent=2))
