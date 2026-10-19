from pathlib import Path
from PIL import Image, ImageDraw
import sys, math

folder = Path(sys.argv[1])
frames = sorted(folder.glob('[0-9][0-9][0-9].png'))
sheet = Image.new('RGB', (1280, math.ceil(len(frames) / 4) * 202), '#18202a')
draw = ImageDraw.Draw(sheet)
for i, file in enumerate(frames):
    with Image.open(file) as source:
        frame = source.convert('RGB').resize((320, 180))
    x, y = (i % 4) * 320, (i // 4) * 202
    sheet.paste(frame, (x, y))
    draw.text((x + 5, y + 184), file.stem, fill='white')
out = folder.parent / (folder.name + '_검수.jpg')
sheet.save(out, quality=92)
print(out)
