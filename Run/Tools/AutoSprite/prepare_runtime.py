"""Deterministic frame selection. No frame-by-frame recentering or rescaling."""
import hashlib
import json
import sys
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
character = sys.argv[1]
source = root / 'DataBundles/Art/Race2D/AutoSprite' / character
config = json.loads((source / 'runtime_selection.json').read_text())
destination = root / 'Assets/ArtBundles/Race2D/AutoSprite' / character
destination.mkdir(parents=True, exist_ok=True)
directions = ['up','northeast','right','southeast','down','southwest','left','northwest']
selections = config.get('directionFrames', {d:config.get('sourceFrames',[]) for d in directions})
frame_count = max(len(indices) for indices in selections.values())
cell, inner, columns = 256, 224, 8
rows = (frame_count+columns-1)//columns
output = Image.new('RGBA',(columns*cell,len(directions)*rows*cell))
hashes = []
edge_frames = []
for di, direction in enumerate(directions):
    path = source / 'source' / (direction+'.png')
    sheet = Image.open(path).convert('RGBA')
    atlas = json.loads(path.with_suffix('.json').read_text())
    hashes.append(hashlib.sha256(path.read_bytes()).hexdigest())
    indices = selections[direction]
    padded_indices = indices + [indices[-1]] * (frame_count-len(indices))
    for oi, index in enumerate(padded_indices):
        f = atlas['frames'][str(index)]
        frame = sheet.crop((f['x'],f['y'],f['x']+f['w'],f['y']+f['h']))
        box = frame.getchannel('A').point(lambda a:255 if a>64 else 0).getbbox()
        if box is None:
            raise ValueError('Empty frame: '+direction+' '+str(index))
        if box[0] == 0 or box[1] == 0 or box[2] == f['w'] or box[3] == f['h']:
            edge_frames.append({'direction':direction,'frame':index,'bounds':box})
        # Premultiplied filtering prevents colored transparent RGB from bleeding into outlines.
        resized = frame.convert('RGBa').resize((inner,inner),Image.Resampling.LANCZOS).convert('RGBA')
        offset = (cell-inner)//2
        output.paste(resized,((oi%columns)*cell+offset,(di*rows+oi//columns)*cell+offset))
    duration = config.get('clipDuration', atlas['meta']['duration_s'] * sum(len(v) for v in selections.values()) / 8 / len(atlas['frames']))
output.save(destination / 'Run.png')
manifest = {'CharacterId':character,'DirectionCount':8,'FrameCount':frame_count,'DirectionFrameCounts':[len(selections[d]) for d in directions],'Columns':columns,'RowsPerDirection':rows,'CellSize':cell,'Duration':duration,'WorldSize':config['worldSize'],'PivotX':config.get('pivotX',.5),'PivotY':config.get('pivotY',.11)}
(destination / 'Import.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
config.update({'directions':directions,'sourceSha256':hashes,'outputSha256':hashlib.sha256((destination/'Run.png').read_bytes()).hexdigest(),'sourceCell':512,'outputCell':cell,'innerSize':inner,'uniformScale':inner/512,'padding':16,'durationSeconds':duration,'sourceEdgeContacts':edge_frames,'frameTransform':'Same fixed scale and translation for every frame and direction; no anatomy edits.'})
(source / 'runtime_selection.json').write_text(json.dumps(config,indent=2),encoding='utf-8')
print(character,'runtime',output.size,'counts',manifest['DirectionFrameCounts'],'duration',duration,'source edge contact count',len(edge_frames))
