"""Read-only frame analysis and temporary contact sheets for visual inspection."""
import json
import sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[2]
folder = root / 'DataBundles/Art/Race2D/AutoSprite' / sys.argv[1] / 'source'
temporary = root / 'Temp/AutoSprite'
directions = ['up','northeast','right','southeast','down','southwest','left','northwest']
overview = Image.new('RGBA', (1536, 768), (70,95,92,255))
for di, direction in enumerate(directions):
    sheet = Image.open(folder / (direction+'.png')).convert('RGBA')
    atlas = json.loads((folder / (direction+'.json')).read_text())
    frames = [sheet.crop((f['x'],f['y'],f['x']+f['w'],f['y']+f['h'])) for f in atlas['frames'].values()]
    masks = np.asarray([np.asarray(f.getchannel('A').resize((64,64)),dtype=np.float32) / 255 for f in frames])
    bounds = [f.getchannel('A').point(lambda a:255 if a>64 else 0).getbbox() for f in frames]
    print(direction, 'bounds', [min(b[i] for b in bounds) if i<2 else max(b[i] for b in bounds) for i in range(4)],
          'cycle distances', [(p,round(float(abs(masks[20:-p]-masks[20+p:]).mean()),3)) for p in range(10,23)])
    preview = frames[24].resize((384,384))
    overview.alpha_composite(preview, ((di%4)*384,(di//4)*384))
    ImageDraw.Draw(overview).text(((di%4)*384+8,(di//4)*384+8),direction,fill='white')
    if direction == 'right':
        contact = Image.new('RGBA',(1536,1344),(70,95,92,255))
        draw = ImageDraw.Draw(contact)
        for i,f in enumerate(frames):
            contact.alpha_composite(f.resize((192,192)),((i%8)*192,(i//8)*192))
            draw.text(((i%8)*192+3,(i//8)*192+3),str(i),fill='white')
        contact.save(temporary / (sys.argv[1]+'_motion.png'))
overview.save(temporary / (sys.argv[1]+'_directions.png'))
