"""Per-frame flat-color key for reviewed footage; preserves all frame coordinates."""
from pathlib import Path
import json, sys
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt

root=Path(__file__).resolve().parents[2]/'DataBundles/영상/2D탈것_달리기_20261016'
key=sys.argv[1]
low=float(sys.argv[2]) if len(sys.argv)>2 else 80
high=float(sys.argv[3]) if len(sys.argv)>3 else 140
version=sys.argv[4] if len(sys.argv)>4 else 'matte_v2'
job=root/'_post'/key
info=json.loads((job/'frames.json').read_text(encoding='utf-8-sig'))
out=job/version
out.mkdir(exist_ok=False)
atlas=Image.new('RGBA',(6400,((info['frameCount']+7)//8)*800))
stats=[]
for i,f in enumerate(info['frames']):
    rgb=np.asarray(Image.open(job/f['file']).convert('RGB'),dtype=np.float32)
    corners=np.concatenate([rgb[:12,:12].reshape(-1,3),rgb[:12,-12:].reshape(-1,3),rgb[-12:,:12].reshape(-1,3),rgb[-12:,-12:].reshape(-1,3)])
    bg=np.median(corners,axis=0)
    difference=np.linalg.norm(rgb-bg,axis=2)
    alpha=np.clip((difference-low)/(high-low),0,1)
    interior=(alpha>.995)
    interior &= distance_transform_edt(interior)>3
    distances,nearest=distance_transform_edt(~interior,return_indices=True)
    edge=(alpha>0)&(distances<=6)&~interior
    result=rgb.copy()
    result[edge]=rgb[nearest[0][edge],nearest[1][edge]]
    result[alpha==0]=0
    atlas.paste(Image.fromarray(np.dstack([np.uint8(np.clip(result,0,255)),np.uint8(np.rint(alpha*255))])),(i%8*800,i//8*800))
    stats.append({'frame':i,'background':bg.tolist(),'alphaPixels':int((alpha>.1).sum())})
atlas.save(out/'source.png')
atlas.save(out/'source_clean.png')
(out/'key.json').write_text(json.dumps({'clear':low,'opaque':high,'scale':1,'despill':'nearest interior RGB at boundary only','frames':stats},indent=2),encoding='utf-8')
print(out)
