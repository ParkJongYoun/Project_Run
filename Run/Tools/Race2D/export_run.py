"""Export a visually reviewed forward-playing loop without resizing any frame."""
from pathlib import Path
import hashlib
import json
import sys
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'DataBundles/영상/2D탈것_달리기_20261016'

def export(key,start,end,notes):
    job=WORK/'_post'/key
    info=json.loads((job/'frames.json').read_text(encoding='utf-8-sig'))
    source=sorted(p for p in job.glob('matte_v*') if (p/'source_clean.png').exists())[-1]/'source_clean.png'
    atlas=Image.open(source).convert('RGBA')
    columns=atlas.width//800
    if not 0<=start<end<=info['frameCount']:raise ValueError('invalid reviewed range')
    parts=key.split('_')
    mount,view=parts[0],'_'.join(parts[1:3])
    source_manifest=json.loads((ROOT/'DataBundles/기획/시안/2D뷰세트_탈것6종_경기장전체_v1/asset_manifest.json').read_text(encoding='utf-8-sig'))
    original=next(a for a in source_manifest['assets'] if a['file']==f'characters/{mount}/{view}.png')
    out=WORK/'sprites'/key
    out.mkdir(parents=True,exist_ok=True)
    pages=[]
    for page_start in range(start,end,25):
        count=min(25,end-page_start)
        page=Image.new('RGBA',(min(5,count)*800,((count+4)//5)*800))
        for i,index in enumerate(range(page_start,page_start+count)):
            frame=atlas.crop(((index%columns)*800,(index//columns)*800,(index%columns+1)*800,(index//columns+1)*800))
            page.paste(frame,((i%5)*800,(i//5)*800))
        name=f'run_{key}_{len(pages):02}.png'
        page.save(out/name)
        pages.append({'file':name,'width':page.width,'height':page.height,'sha256':hashlib.sha256((out/name).read_bytes()).hexdigest()})
    durations=[info['frames'][i]['duration'] for i in range(start,end)]
    record={'key':key,'mount':mount,'view':view,'reviewStatus':'reviewed','review':{'appearance':'passed','camera':'passed','motion':'passed','alpha':'passed','alignment':'passed'},'notes':notes,'sourceVideo':info['source'],'sourceSha256':info['sourceSha256'],'matteSourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'sourceFrames':list(range(start,end)),'frameCount':end-start,'durations':durations,'duration':sum(durations),'cellSize':800,'columns':5,'framesPerPage':25,'pixelsPerUnit':250*800/1254,'pivot':[.5,(original['height']-1-original['bounds']['bottom'])/original['height']],'pages':pages,'transform':'identity; no resampling or per-frame recentering','loop':True,'sglAdaptation':'Preserve the original full 800px canvas and original ground pivot; moving its tall artwork to the SGL-specific y=600 anchor would crop the head.'}
    (out/'clip.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'key':key,'frames':end-start,'duration':sum(durations),'pages':len(pages)}))

if __name__=='__main__':export(sys.argv[1],int(sys.argv[2]),int(sys.argv[3]),sys.argv[4])
