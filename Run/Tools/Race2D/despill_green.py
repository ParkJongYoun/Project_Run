"""Remove green key spill only, for the purple/white/gold ghost artwork."""
from pathlib import Path
import json,sys
import numpy as np
from PIL import Image

job=Path(__file__).resolve().parents[2]/'DataBundles/영상/2D탈것_달리기_20261016/_post'/sys.argv[1]
source=job/'matte_v2/source_clean.png'
out=job/'matte_v3'
out.mkdir(exist_ok=False)
atlas=Image.open(source).convert('RGBA')
result=Image.new('RGBA',atlas.size)
info=json.loads((job/'frames.json').read_text(encoding='utf-8-sig'))
for i in range(info['frameCount']):
    x,y=i%8*800,i//8*800
    rgba=np.asarray(atlas.crop((x,y,x+800,y+800)),dtype=np.float32).copy()
    limit=np.maximum(rgba[:,:,0],rgba[:,:,2])
    excess=rgba[:,:,1]-limit
    rgba[:,:,3]*=np.clip((35-excess)/25,0,1)
    rgba[:,:,1]=np.minimum(rgba[:,:,1],limit)
    rgba[:,:,:3][rgba[:,:,3]==0]=0
    result.paste(Image.fromarray(np.uint8(np.clip(np.rint(rgba),0,255))),(x,y))
result.save(out/'source.png');result.save(out/'source_clean.png')
(out/'green_despill.json').write_text(json.dumps({'source':str(source),'method':'green excess alpha 10..35; green channel limited to max(red,blue); full canvas unchanged','scale':1},indent=2),encoding='utf-8')
print(out)
