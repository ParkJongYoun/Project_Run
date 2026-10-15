"""Frame diagnostics only: loop candidates require visual review before export."""
from pathlib import Path
import json
import sys
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'DataBundles/영상/2D탈것_달리기_20261016'

def inspect(key):
    job = WORK / '_post' / key
    info = json.loads((job/'frames.json').read_text(encoding='utf-8-sig'))
    matte = sorted(p for p in job.glob('matte_v*') if (p/'source.png').exists())[-1]
    source_path = matte/'source_clean.png'
    if not source_path.exists():
        source_path = matte/'source.png'
    atlas = Image.open(source_path).convert('RGBA')
    size = 800
    cols = atlas.width // size
    frames = [atlas.crop(((i%cols)*size,(i//cols)*size,(i%cols+1)*size,(i//cols+1)*size)) for i in range(info['frameCount'])]
    inspection = job/'inspection'
    inspection.mkdir(exist_ok=True)
    for i in (0, 7, 14, 21, 28, 35, 42, 49, 55):
        frames[i].save(inspection/f'rgba_{i:02}.png')
    samples=[]
    for frame in frames:
        a=np.asarray(frame.resize((100,100)),dtype=np.float32)/255
        a[:,:,:3]*=a[:,:,3:4]
        samples.append(a)
    pairs=[]
    for start in range(5,len(frames)-13):
        for end in range(start+12,min(start+31,len(frames)-1)):
            d=np.abs(samples[start]-samples[end])
            velocity=np.mean(np.abs((samples[start+1]-samples[start])-(samples[end+1]-samples[end])))
            score=float(np.mean(d)+velocity*.35)
            pairs.append({'start':start,'endExclusive':end,'frames':end-start,'seconds':(end-start)/24,'score':round(score,5)})
    pairs.sort(key=lambda p:p['score'])
    selected=[]
    for p in pairs:
        if all(abs(p['start']-q['start'])>2 or abs(p['endExclusive']-q['endExclusive'])>2 for q in selected): selected.append(p)
        if len(selected)==12:break
    stats=[]
    for i,f in enumerate(frames):
        a=np.asarray(f)
        opaque=a[:,:,3]>30
        stats.append({'frame':i,'bounds':f.getbbox(),'opaquePixels':int(opaque.sum()),'edgePixels':int(opaque[0,:].sum()+opaque[-1,:].sum()+opaque[:,0].sum()+opaque[:,-1].sum())})
    report={'key':key,'sourceSize':info['sourceSize'],'duration':info['duration'],'loopCandidates':selected,'stats':stats,'reviewStatus':'pending'}
    best=selected[0]
    for i in sorted(set([best['start'],best['endExclusive']] + list(range(best['start'],best['endExclusive'],4)))):
        frames[i].save(inspection/f'loop_{i:02}.png')
    (inspection/'diagnostics.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'key':key,'candidates':selected[:6],'edgeFrames':[s['frame'] for s in stats if s['edgePixels']>0]}))

def clean_edges(key):
    job=WORK/'_post'/key
    info=json.loads((job/'frames.json').read_text(encoding='utf-8-sig'))
    matte=sorted(p for p in job.glob('matte_v*') if (p/'source.png').exists())[-1]
    source=Image.open(matte/'source.png').convert('RGBA')
    columns=source.width//800
    result=Image.new('RGBA',source.size)
    altered=0
    for i in range(info['frameCount']):
        x,y=(i%columns)*800,(i//columns)*800
        f=source.crop((x,y,x+800,y+800))
        a=np.asarray(f).astype(np.int16)
        near_clear=np.asarray(f.getchannel('A').filter(ImageFilter.MinFilter(5)))<245
        excess=np.maximum(0,np.minimum(a[:,:,0],a[:,:,2])-a[:,:,1])
        mask=near_clear & (a[:,:,3]>0) & (excess>8)
        # Remove only the common red/blue spill at the outer two pixels.
        # Interior colors, alpha, geometry and all frame coordinates stay untouched.
        a[:,:,0][mask]-=excess[mask]
        a[:,:,2][mask]-=excess[mask]
        altered+=int(mask.sum())
        result.paste(Image.fromarray(np.uint8(a)),(x,y))
    result.save(matte/'source_clean.png')
    (matte/'edge_cleanup.json').write_text(json.dumps({'method':'two-pixel boundary magenta despill; RGB only','changedPixels':altered,'alphaChanged':False,'resized':False},indent=2),encoding='utf-8')

if __name__=='__main__':
    if len(sys.argv)>2 and sys.argv[2]=='--clean':clean_edges(sys.argv[1])
    inspect(sys.argv[1])
