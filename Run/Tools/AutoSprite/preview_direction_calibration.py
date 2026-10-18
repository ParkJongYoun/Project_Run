"""Inspect fixed Unity PPU/pivot calibration without changing any production pixels."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'DataBundles/검증/Race2D_DirectionAudit_20261018'
IDS = ['Minorous', 'GiantPoring', 'Smokie', 'Savage', 'Lunatic', 'Edgar']
DIRS = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW']

def read(ident):
    folder = ROOT / 'Assets/ArtBundles/Race2D/AutoSprite' / ident
    cfg = json.loads((folder/'Import.json').read_text(encoding='utf-8-sig'))
    path = folder/'DirectionCalibration.json'
    calibration = json.loads(path.read_text(encoding='utf-8-sig')) if path.exists() else None
    return cfg, calibration, Image.open(folder/'Run.png').convert('RGBA')

def cell(sheet, cfg, direction, frame):
    x = frame % cfg['Columns'] * cfg['CellSize']
    y = (direction * cfg['RowsPerDirection'] + frame // cfg['Columns']) * cfg['CellSize']
    return sheet.crop((x, y, x+cfg['CellSize'], y+cfg['CellSize']))

def draw_pose(target, pose, cfg, calibration, direction, anchor):
    scale = calibration['Scales'][direction] if calibration else 1
    p = calibration['GroundAnchors'][direction] if calibration else {
        'x':cfg['PivotX'] * cfg['CellSize'], 'y':(1-cfg['PivotY']) * cfg['CellSize']}
    # Preview only: mirror the fixed PPU and pivot used by Sprite.Create.
    size = round(cfg['CellSize'] * scale)
    preview = pose.resize((size, size), Image.Resampling.LANCZOS)
    target.paste(preview,(round(anchor[0]-p['x']*scale),round(anchor[1]-p['y']*scale)),preview)

manifest = {}
for ident in IDS:
    cfg, calibration, sheet = read(ident)
    out = Image.new('RGB',(8*320,2*304),'#d7e0d5'); draw=ImageDraw.Draw(out)
    for direction in range(8):
        frame=cfg['DirectionFrameCounts'][direction]//2
        for row, cal in enumerate([None,calibration]):
            anchor=(direction*320+160,row*304+225)
            draw.ellipse((anchor[0]-51,anchor[1]-15,anchor[0]+51,anchor[1]+15),fill='#adb7a7')
            draw_pose(out,cell(sheet,cfg,direction,frame),cfg,cal,direction,anchor)
            draw.line((anchor[0]-5,anchor[1],anchor[0]+5,anchor[1]),fill='#bd3340',width=2)
            draw.text((direction*320+8,row*304+6),f'{ident} {DIRS[direction]} '+('before' if row==0 else 'after'),fill='#20392a')
    out.save(OUT/(ident+'_calibrated_comparison.png'))
    paths=[ROOT/'Assets/ArtBundles/Race2D/AutoSprite'/ident/'Run.png']
    paths+=list((ROOT/'DataBundles/Art/Race2D/AutoSprite'/ident/'source').glob('*.png'))
    manifest[ident]={'frameCounts':cfg['DirectionFrameCounts'],'duration':cfg['Duration'],
        'productionPngSha256':{str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}}
(OUT/'preserved_assets.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('Inspection previews and source/runtime hashes recorded for all six characters.')

if '--video' in __import__('sys').argv:
    import subprocess
    ffmpeg = next((ROOT/'Temp/media-tools/imageio_ffmpeg/binaries').glob('ffmpeg*.exe'))
    data = [read(ident) for ident in IDS]
    video = OUT/'all_six_direction_comparison.mp4'
    process = subprocess.Popen([str(ffmpeg), '-y', '-loglevel', 'error', '-f', 'rawvideo',
        '-pix_fmt', 'rgb24', '-s', '1920x640', '-r', '24', '-i', '-', '-an',
        '-c:v', 'libx264', '-crf', '20', '-pix_fmt', 'yuv420p', '-movflags', '+faststart',str(video)],stdin=subprocess.PIPE)
    try:
        for speed, seconds in [(1,8),(.4,20)]:
            for tick in range(seconds*24):
                clock=tick/24*speed; direction=int(clock)%8
                out=Image.new('RGB',(1920,640),'#d7e0d5'); draw=ImageDraw.Draw(out)
                for col,(cfg,calibration,sheet) in enumerate(data):
                    count=cfg['DirectionFrameCounts'][direction]
                    frame=int(clock/cfg['Duration']*count)%count
                    for row,cal in enumerate([None,calibration]):
                        anchor=(col*320+160,row*320+225)
                        draw.ellipse((anchor[0]-51,anchor[1]-15,anchor[0]+51,anchor[1]+15),fill='#adb7a7')
                        draw_pose(out,cell(sheet,cfg,direction,frame),cfg,cal,direction,anchor)
                        draw.text((col*320+10,row*320+12),IDS[col]+' '+DIRS[direction]+' '+('BEFORE' if row==0 else 'AFTER'),fill='#20392a')
                        draw.text((col*320+10,row*320+285),f'{speed:g}x / frame {frame:02} / fixed pivot + PPU',fill='#20392a')
                process.stdin.write(out.tobytes())
    finally:
        process.stdin.close()
        result=process.wait()
    if result: raise RuntimeError('Preview video encoding failed')
    print(video)
