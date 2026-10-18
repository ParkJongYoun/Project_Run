"""Extract generated training video without changing artwork or frame timing."""
from pathlib import Path
import sys, json, hashlib
import av
from PIL import Image, ImageDraw

video = Path(sys.argv[1]).resolve()
target = video.parent.parent / '스프라이트'
target.mkdir(parents=True, exist_ok=True)
container = av.open(str(video))
stream = container.streams.video[0]
fps = float(stream.average_rate)
frames = [frame.to_image().convert('RGB') for frame in container.decode(stream)]
w,h = frames[0].size
cols, rows = 4096//w, 4096//h
perpage=cols*rows
manifest={'SourceVideoSha256':hashlib.sha256(video.read_bytes()).hexdigest(), 'IllustrationRect':[0,0,1672,941], 'CellSize':[w,h], 'Pages':[], 'Frames':[]}
for start in range(0,len(frames),perpage):
    subset=frames[start:start+perpage]; page=len(manifest['Pages'])
    ph=((len(subset)+cols-1)//cols)*h
    atlas=Image.new('RGB',(cols*w,ph))
    filename=f'atlas_{page:02}.png'
    for j,frame in enumerate(subset):
        x,y=(j%cols)*w,(j//cols)*h
        atlas.paste(frame,(x,y))
        manifest['Frames'].append({'Index':start+j,'Page':page,'X':x,'Y':y,'Duration':1/fps})
    atlas.save(target/filename)
    manifest['Pages'].append({'File':filename,'Width':cols*w,'Height':ph})
(target/'clip.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
sheet=Image.new('RGB',(960,612),'#173A2F'); draw=ImageDraw.Draw(sheet)
for i in range(9):
    index=round(i*(len(frames)-1)/8)
    tile=frames[index].copy();tile.thumbnail((320,180))
    x,y=i%3*320,i//3*204
    sheet.paste(tile,(x,y));draw.text((x+8,y+183),f'{index/fps:.2f}s / frame {index}',fill='white')
sheet.save(video.parent.parent/'프레임검수.jpg')
print(json.dumps({'video':str(video),'frames':len(frames),'fps':fps,'size':[w,h],'pages':len(manifest['Pages']),'target':str(target)},ensure_ascii=False))
