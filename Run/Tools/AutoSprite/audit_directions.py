"""Read-only inspection of all runtime cells; originals are never changed."""
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'DataBundles/검증/Race2D_DirectionAudit_20261018'
IDS = ['Minorous', 'GiantPoring', 'Smokie', 'Savage', 'Lunatic', 'Edgar']
DIRS = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW']
OUT.mkdir(parents=True, exist_ok=True)
overview = Image.new('RGB', (8*192, 6*222), '#d7e0d5')
draw = ImageDraw.Draw(overview)
report = {}
for row, ident in enumerate(IDS):
    folder = ROOT / 'Assets/ArtBundles/Race2D/AutoSprite' / ident
    settings = json.loads((folder/'Import.json').read_text(encoding='utf-8-sig'))
    sheet = Image.open(folder/'Run.png').convert('RGBA')
    counts = settings['DirectionFrameCounts']
    details = Image.new('RGB', (max(counts)*128, 8*154), '#d7e0d5')
    dd = ImageDraw.Draw(details)
    metrics = []
    for direction in range(8):
        data=[]
        for frame in range(counts[direction]):
            x=frame%settings['Columns']*256
            y=(direction*settings['RowsPerDirection']+frame//settings['Columns'])*256
            cell=sheet.crop((x,y,x+256,y+256))
            alpha=np.asarray(cell)[:,:,3]
            ys,xs=np.where(alpha>64)
            data.append({'frame':frame,'box':[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)],'area':int((alpha>64).sum())})
            preview=cell.resize((128,128))
            details.paste(preview,(frame*128,direction*154+24),preview)
            dd.text((frame*128+3,direction*154+3),f'{DIRS[direction]} {frame:02}',fill='#21352b')
            if frame==counts[direction]//2:
                preview=cell.resize((192,192))
                overview.paste(preview,(direction*192,row*222+28),preview)
        draw.text((direction*192+4,row*222+4),ident+' '+DIRS[direction],fill='#21352b')
        metrics.append({'direction':DIRS[direction],'frames':data,'medianWidth':float(np.median([f['box'][2]-f['box'][0] for f in data])), 'medianHeight':float(np.median([f['box'][3]-f['box'][1] for f in data])),'medianArea':float(np.median([f['area'] for f in data]))})
    details.save(OUT/(ident+'_all_frames.png'))
    report[ident]=metrics
overview.save(OUT/'directions_before.png')
(OUT/'alpha_metrics_before.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
for ident,metrics in report.items():
    print(ident,[(d['direction'],int(d['medianWidth']),int(d['medianHeight']),int(d['medianArea'])) for d in metrics])
