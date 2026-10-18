"""Verify deterministic sprite preparation and source preservation."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from PIL import Image

parser=argparse.ArgumentParser()
parser.add_argument('--credential-file')
args=parser.parse_args()
root=Path(__file__).resolve().parents[2]
source_root=root/'DataBundles/Art/Race2D/AutoSprite'
output_root=root/'Assets/ArtBundles/Race2D/AutoSprite'
report=[]
prompts=[]
for character in ['Minorous','GiantPoring','Smokie','Savage','Lunatic','Edgar']:
    source=source_root/character
    generation=json.loads((source/'generation.json').read_text(encoding='utf-8-sig'))
    config=json.loads((source/'runtime_selection.json').read_text())
    spec=json.loads((output_root/character/'Import.json').read_text())
    actual=Image.open(output_root/character/'Run.png').convert('RGBA')
    expected=Image.new('RGBA',actual.size)
    selections=config.get('directionFrames',{d:config.get('sourceFrames',[]) for d in config['directions']})
    assert hashlib.sha256((source/'original.png').read_bytes()).hexdigest()==generation['sourceSha256']
    assert generation['uploadUnchanged']
    for di,direction in enumerate(config['directions']):
        path=source/'source'/(direction+'.png')
        assert hashlib.sha256(path.read_bytes()).hexdigest()==config['sourceSha256'][di]
        image=Image.open(path).convert('RGBA')
        atlas=json.loads(path.with_suffix('.json').read_text())
        indices=selections[direction]
        assert len(indices)==spec['DirectionFrameCounts'][di]
        assert indices==list(range(indices[0],indices[-1]+1))
        for fi,index in enumerate(indices+[indices[-1]]*(spec['FrameCount']-len(indices))):
            f=atlas['frames'][str(index)]
            frame=image.crop((f['x'],f['y'],f['x']+f['w'],f['y']+f['h']))
            frame=frame.convert('RGBa').resize((224,224),Image.Resampling.LANCZOS).convert('RGBA')
            expected.paste(frame,((fi%8)*256+16,(di*spec['RowsPerDirection']+fi//8)*256+16))
    assert np.array_equal(np.asarray(actual),np.asarray(expected)),character+' pixels differ from documented transform'
    report.append({'character':character,'uploadMatchesOriginal':True,'sourceHashesMatch':True,'deterministicPixelMatch':True,'directions':8,'frameCounts':spec['DirectionFrameCounts'],'validFrames':sum(spec['DirectionFrameCounts']),'duration':spec['Duration'],'sourceEdgeContactFrames':len(config['sourceEdgeContacts']),'newClipping':False,'textureSize':actual.size})
    prompts.append({'character':character,'imageMode':'built-in image_gen','artPrompt':(source/'art_prompt.txt').read_text(),'motionPrompt':generation['motionPrompt'],'motionSummaryKo':generation['motionSummaryKo']})
if args.credential_file:
    key=Path(args.credential_file).read_text(encoding='utf-8-sig').strip()
    copies=0
    for folder in [source_root,output_root,root/'Tools/AutoSprite']:
        for path in folder.rglob('*'):
            if path.is_file() and path.suffix in ['.py','.json','.md','.txt']:
                copies+=key in path.read_text(encoding='utf-8-sig',errors='ignore')
    assert copies==0,'Credential unexpectedly copied to a generated artifact'
else:
    copies=None
out=root/'DataBundles/검증/Race2D_AutoSprite_20261017'
(out/'assets.json').write_text(json.dumps({'characters':report,'credentialCopies':copies,'paidPacks':7,'creditsUsed':105,'freeReextractions':1,'sourceLimitations':'Minor original edge contacts remain on a few horn/ear/paw-tip frames. Padding introduces no new clipping; it does not reconstruct missing source pixels. Edgar long-tail clipping was resolved by the compact-tail revision.','webglBuildTested':False},ensure_ascii=False,indent=2),encoding='utf-8')
(source_root/'prompts.json').write_text(json.dumps(prompts,ensure_ascii=False,indent=2),encoding='utf-8')
print('Validated',len(report),'characters,',sum(r['validFrames'] for r in report),'active frames; deterministic RGBA match; credential copies:',copies)
