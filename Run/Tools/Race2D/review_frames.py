from pathlib import Path
import sys
from PIL import Image

job=Path(__file__).resolve().parents[2]/'DataBundles/영상/2D탈것_달리기_20261016/_post'/sys.argv[1]
source=sorted(p for p in job.glob('matte_v*') if (p/'source_clean.png').exists())[-1]/'source_clean.png'
atlas=Image.open(source)
columns=atlas.width//800
for index in map(int,sys.argv[2:]):
    x,y=index%columns*800,index//columns*800
    atlas.crop((x,y,x+800,y+800)).save(job/'inspection'/f'review_{index:02}.png')
