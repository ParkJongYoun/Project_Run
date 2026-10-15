"""Background-connected flat-color segmentation; never keys isolated body colors."""
from pathlib import Path
import json,sys
import numpy as np
from PIL import Image
from scipy.ndimage import binary_propagation,distance_transform_edt,label

root=Path(__file__).resolve().parents[2]/'DataBundles/영상/2D탈것_달리기_20261016'
job=root/'_post'/sys.argv[1]
threshold=float(sys.argv[2])
out=job/sys.argv[3]
out.mkdir(exist_ok=False)
info=json.loads((job/'frames.json').read_text(encoding='utf-8-sig'))
atlas=Image.new('RGBA',(6400,((info['frameCount']+7)//8)*800))
stats=[]
for i,f in enumerate(info['frames']):
    rgb=np.asarray(Image.open(job/f['file']).convert('RGB'),dtype=np.float32)
    corners=np.concatenate([rgb[:12,:12].reshape(-1,3),rgb[:12,-12:].reshape(-1,3),rgb[-12:,:12].reshape(-1,3),rgb[-12:,-12:].reshape(-1,3)])
    bg=np.median(corners,axis=0)
    difference=np.linalg.norm(rgb-bg,axis=2)
    candidate=difference<threshold
    seed=np.zeros(candidate.shape,bool)
    seed[[0,-1],:]=True
    seed[:,[0,-1]]=True
    clear=binary_propagation(seed&candidate,mask=candidate)
    # High-confidence exact background also clears enclosed spaces between limbs.
    clear=binary_propagation(clear | (difference<20),mask=candidate)
    fg=~clear
    components,_=label(fg)
    sizes=np.bincount(components.ravel())
    fg &= sizes[components]>=16
    distance=distance_transform_edt(fg)
    alpha=np.clip((distance-.75)/1.5,0,1)
    interior=distance>3.5
    _,nearest=distance_transform_edt(~interior,return_indices=True)
    edge=(alpha>0)&~interior
    result=rgb.copy()
    result[edge]=rgb[nearest[0][edge],nearest[1][edge]]
    result[alpha==0]=0
    atlas.paste(Image.fromarray(np.dstack([np.uint8(np.clip(result,0,255)),np.uint8(np.rint(alpha*255))])),(i%8*800,i//8*800))
    stats.append({'frame':i,'background':bg.tolist(),'opaquePixels':int((alpha>.1).sum())})
atlas.save(out/'source.png');atlas.save(out/'source_clean.png')
(out/'connected-key.json').write_text(json.dumps({'method':'border-connected background key, 0.75px inset, 1.5px antialias, boundary despill only','threshold':threshold,'scale':1,'frames':stats},indent=2),encoding='utf-8')
print(out)
