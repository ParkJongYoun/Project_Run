"""Key a flat background whose hue changes; retain full-size, ordered video frames."""
from pathlib import Path
import json
import sys
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt

ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'DataBundles/영상/2D탈것_달리기_20261016'

def main(key):
    job=WORK/'_post'/key
    info=json.loads((job/'frames.json').read_text(encoding='utf-8-sig'))
    out=job/'matte_v2'
    out.mkdir(exist_ok=False)
    atlas=Image.new('RGBA',(6400,((info['frameCount']+7)//8)*800))
    report=[]
    for i,frame in enumerate(info['frames']):
        rgb=np.asarray(Image.open(job/frame['file']).convert('RGB'),dtype=np.float32)
        corners=np.concatenate([rgb[:12,:12].reshape(-1,3),rgb[:12,-12:].reshape(-1,3),rgb[-12:,:12].reshape(-1,3),rgb[-12:,-12:].reshape(-1,3)])
        bg=np.median(corners,axis=0)
        distance=np.sqrt(np.sum((rgb-bg)**2,axis=2))
        alpha=np.clip((distance-15)/65,0,1)
        solid=alpha>.995
        distances,nearest=distance_transform_edt(~solid,return_indices=True)
        edge=(alpha>0)&(alpha<.995)&(distances<=5)
        result=rgb.copy()
        result[edge]=rgb[nearest[0][edge],nearest[1][edge]]
        result[alpha==0]=0
        rgba=np.dstack([np.uint8(np.clip(result,0,255)),np.uint8(np.rint(alpha*255))])
        atlas.paste(Image.fromarray(rgba),(i%8*800,i//8*800))
        report.append({'frame':i,'background':bg.tolist(),'alphaPixels':int((alpha>.1).sum()),'borderPixels':int((alpha[0,:]>.1).sum()+(alpha[-1,:]>.1).sum()+(alpha[:,0]>.1).sum()+(alpha[:,-1]>.1).sum())})
    atlas.save(out/'source.png')
    atlas.save(out/'source_clean.png')
    (out/'adaptive-key.json').write_text(json.dumps({'method':'frame corner-median RGB key; 15 clear / 80 opaque; nearest interior color within 5px only for partial alpha','scale':1,'frames':report},indent=2),encoding='utf-8')
    print(json.dumps({'frames':len(report),'edgeFrames':[r['frame'] for r in report if r['borderPixels']]}))

if __name__=='__main__':main(sys.argv[1])
