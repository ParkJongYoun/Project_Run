"""QA-only montage of unchanged capture files; never used as game artwork."""
from pathlib import Path
import sys
from PIL import Image,ImageDraw

folder=Path(sys.argv[1])
files=sorted(folder.glob(sys.argv[2]))
width=240
height=round(width*16/9)
columns=4
out=Image.new('RGB',(columns*width,((len(files)+columns-1)//columns)*(height+24)),(30,30,35))
draw=ImageDraw.Draw(out)
for i,path in enumerate(files):
    img=Image.open(path).convert('RGB')
    img.thumbnail((width,height))
    x,y=i%columns*width,i//columns*(height+24)
    out.paste(img,(x,y+24))
    draw.text((x+5,y+5),path.stem,fill=(255,255,255))
out.save(sys.argv[3])
