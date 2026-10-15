"""Collect manually reviewed clips and verify that Unity has the identical pages."""
from pathlib import Path
import hashlib,json

root=Path(__file__).resolve().parents[2]
work=root/'DataBundles/영상/2D탈것_달리기_20261016'
jobs=json.loads((work/'jobs.json').read_text(encoding='utf-8-sig'))
accepted={}
for path in sorted((work/'sprites').glob('*/clip.json')):
    clip=json.loads(path.read_text(encoding='utf-8-sig'))
    if clip.get('reviewStatus')!='reviewed':continue
    canonical=clip['mount']+'_'+clip['view']
    if canonical in accepted:raise ValueError('More than one reviewed clip for '+canonical)
    for page in clip['pages']:
        target=root/'Assets/ArtBundles/Race2D/animations'/clip['mount']/clip['view']/page['file']
        if hashlib.sha256(target.read_bytes()).hexdigest()!=page['sha256']:
            raise ValueError('Unity page differs from reviewed source: '+str(target))
    diagnostics=json.loads((work/'_post'/clip['key']/'inspection/diagnostics.json').read_text(encoding='utf-8-sig'))
    clipped=[s['frame'] for s in diagnostics['stats'] if s['frame'] in clip['sourceFrames'] and s['edgePixels']>0]
    if clipped:raise ValueError('Reviewed range touches frame boundary: '+clip['key']+str(clipped))
    accepted[canonical]=clip
records=[]
for job in jobs:
    clip=accepted.get(job['key'])
    if clip:
        job['status']='reviewed_applied'
        job['acceptedKey']=clip['key']
        job['sourceFrames']=clip['sourceFrames']
        reason=clip['notes']
    else:
        job['status']='still_fallback'
        reason='여러 생성에서 발 또는 꼬리가 프레임 경계에서 잘려 완전한 루프를 확보하지 못함. 원본 정지 이미지 유지.'
    records.append({'mount':job['mount'],'view':job['view'],'status':job['status'],'clip':clip['key'] if clip else None,'frameCount':clip['frameCount'] if clip else 0,'seconds':clip['duration'] if clip else 0,'notes':reason})
result={'requestedViews':18,'appliedViews':len(accepted),'fallbackViews':18-len(accepted),'sourceVideo':'800x800 / 24fps / 56frames / ~2.333sec','runtime':'reviewed forward loops; original full-canvas scale and pivot preserved','views':records}
(work/'jobs.json').write_text(json.dumps(jobs,ensure_ascii=False,indent=2),encoding='utf-8')
(work/'view_status.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='views'},ensure_ascii=False))
