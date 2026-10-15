"""Remove boundary spill from an already-keyed ghost clip, preserving its flames.

The two source flame regions are explicitly protected; interior character alpha
and colors are not normalized or repainted. Output remains review-required.
"""
from pathlib import Path
import json
import sys
import numpy as np
from PIL import Image, ImageFilter
from scipy.ndimage import label

ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'DataBundles/영상/2D탈것_달리기_20261016'

def main(key,version):
    job=WORK/'_post'/key
    info=json.loads((job/'frames.json').read_text(encoding='utf-8-sig'))
    source=Image.open(job/'matte_v2/source.png').convert('RGBA')
    out=job/version;out.mkdir(exist_ok=False)
    result=Image.new('RGBA',source.size)
    yy,xx=np.mgrid[:800,:800]
    # Fixed side-view source flame regions and the existing purple hanging charm.
    protect=((xx>345)&(xx<435)&(yy>235)&(yy<360))|((xx>520)&(xx<640)&(yy>155)&(yy<305))|((xx>300)&(xx<345)&(yy>555)&(yy<620))
    report=[]
    for i in range(info['frameCount']):
        x,y=i%8*800,i//8*800
        frame=source.crop((x,y,x+800,y+800));a=np.asarray(frame).astype(np.int16)
        near=np.asarray(frame.getchannel('A').filter(ImageFilter.MinFilter(7)))<220
        excess=np.maximum(0,np.minimum(a[:,:,0],a[:,:,2])-a[:,:,1])
        spill=near & ~protect & (a[:,:,3]>0)
        remove=spill & (excess>105)
        # True flames are blue-violet; pink/red background blocks can survive
        # inside their protected rectangles when the generated key hue changes.
        remove |= protect & (a[:,:,0]>=a[:,:,2]-12) & (a[:,:,0]>180) & (excess>75)
        a[:,:,3][remove]=0
        a[:,:,0][spill]-=excess[spill];a[:,:,2][spill]-=excess[spill]
        components,count=label(a[:,:,3]>10)
        sizes=np.bincount(components.ravel())
        noise=(components>0)&(sizes[components]<16)&~protect
        a[:,:,3][noise]=0
        result.paste(Image.fromarray(np.uint8(a)),(x,y))
        report.append({'frame':i,'spillPixelsRemoved':int(remove.sum()),'isolatedNoisePixels':int(noise.sum())})
    result.save(out/'source.png');result.save(out/'source_clean.png')
    (out/'cleanup.json').write_text(json.dumps({'reviewStatus':'pending','method':'outer three-pixel selective magenta spill removal; flame/charm regions protected; interior unchanged','frames':report},indent=2),encoding='utf-8')
    print(json.dumps({'key':key,'frames':len(report)}))

if __name__=='__main__':main(sys.argv[1],sys.argv[2] if len(sys.argv)>2 else 'matte_v3')
