"""SGL matte + fixed rectangular lobby atlas packing. No resizing or per-frame recentering."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import shutil

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from prepare_video_animation import magenta_matte


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def boundary_cleanup(picture, protected):
    rgba = np.array(picture)
    rgb = rgba[:, :, :3].astype(np.float32)
    alpha = rgba[:, :, 3]
    inside = np.asarray(Image.fromarray((alpha >= 250).astype('uint8') * 255).filter(ImageFilter.MinFilter(7))) > 0
    excess = np.minimum(rgb[:, :, 0], rgb[:, :, 2]) - rgb[:, :, 1]
    target = (alpha > 0) & ~inside & (excess > 5) & ~protected
    known = (alpha >= 250) & ~target
    height, width = alpha.shape
    for step in range(5):
        totals, counts = np.zeros_like(rgb), np.zeros_like(alpha, dtype=np.float32)
        for dy, dx in ((-1,-1),(-1,0),(-1,1),(0,-1),(0,1),(1,-1),(1,0),(1,1)):
            y0, y1 = max(0,-dy), min(height,height-dy)
            x0, x1 = max(0,-dx), min(width,width-dx)
            weight = known[y0+dy:y1+dy, x0+dx:x1+dx]
            totals[y0:y1,x0:x1] += rgb[y0+dy:y1+dy,x0+dx:x1+dx] * weight[:,:,None]
            counts[y0:y1,x0:x1] += weight
        fill = target & ~known & (counts > 0)
        rgb[fill] = totals[fill] / counts[fill,None]
        known[fill] = True
    unresolved = target & ~known
    spill = np.maximum(np.minimum(rgb[:,:,0],rgb[:,:,2])-rgb[:,:,1],0)
    for channel in (0,2):
        rgb[:,:,channel][unresolved] -= spill[unresolved]
    rgba[:,:,:3] = np.clip(np.rint(rgb),0,255).astype('uint8')
    near_opaque = np.asarray(Image.fromarray((alpha >= 128).astype('uint8')*255).filter(ImageFilter.MaxFilter(7))) > 0
    noise = (alpha > 0) & (alpha < 24) & ~near_opaque
    rgba[noise] = 0
    return Image.fromarray(rgba), int(target.sum()), int(noise.sum())


def matte(job, version):
    meta = read(job/'extracted/frames.json')
    config = read(job/'matte_settings.json')
    out = job/version
    out.mkdir(exist_ok=False)
    (out/'masks').mkdir()
    records = []
    for record in meta['frames']:
        image = Image.open(job/'extracted'/record['file']).convert('RGB')
        rgb = np.asarray(image, dtype=np.float32)
        x0,y0,x1,y1 = config['pinkSearchRect']
        region = np.zeros((image.height,image.width), dtype=bool)
        region[y0:y1,x0:x1] = True
        # Shape follows each frame's warm-pink pixels; the search rectangle is not an opaque mask.
        protect = np.clip((rgb[:,:,0]-rgb[:,:,2]-15)/25,0,1)*np.clip((rgb[:,:,1]-15)/20,0,1)*region
        mask = Image.fromarray(np.rint(protect*255).astype('uint8'))
        name = f"frame_{record['index']:05d}.png"
        mask.save(out/'masks'/name)
        rgba = magenta_matte(image, config, mask)
        rgba, edge_count, noise_count = boundary_cleanup(rgba, protect > .5)
        rgba.save(out/name)
        alpha = np.asarray(rgba)[:,:,3]
        bounds = Image.fromarray((alpha > 3).astype('uint8')*255).getbbox()
        records.append({'index':record['index'],'file':name,'bounds':bounds,
                        'protectedPixels':int((protect >= 1).sum()),'edgeCorrectedPixels':edge_count,
                        'noisePixelsRemoved':noise_count,'sha256':digest(out/name),
                        'edgeTouches':bool(np.any(alpha[0,:]) or np.any(alpha[-1,:]) or np.any(alpha[:,0]) or np.any(alpha[:,-1]))})
    write(out/'inspection.json',{'reviewStatus':'draft','settings':config,'frames':records})
    print(json.dumps({'frames':len(records),'edgeTouchFrames':[r['index'] for r in records if r['edgeTouches']]}))


def pack(job, version):
    placement = read(job/'input_record.json')
    meta = read(job / placement.get('sourceFrameMetadata', 'extracted/frames.json'))
    review = read(job/'selection.json')
    if review['reviewStatus'] != 'reviewed':
        raise ValueError('Review the motion, matte and loop before packing.')
    selected = review['sourceFrames']
    if len(selected) < 2 or selected != sorted(set(selected)):
        raise ValueError('Expected unique forward-ordered source frame indices.')
    images = [Image.open(job/version/f'frame_{i:05d}.png').convert('RGBA') for i in selected]
    bounds = [im.getbbox() for im in images]
    width,height = images[0].size
    union = [max(0,min(b[0] for b in bounds)-4), max(0,min(b[1] for b in bounds)-4),
             min(width,max(b[2] for b in bounds)+4), min(height,max(b[3] for b in bounds)+4)]
    fixed_crop = review.get('fixedCrop')
    if fixed_crop is not None:
        if len(fixed_crop) != 4 or any(not isinstance(v, int) for v in fixed_crop):
            raise ValueError('fixedCrop must contain four integer source coordinates.')
        x0,y0,x1,y1 = fixed_crop
        if not (0 <= x0 < x1 <= width and 0 <= y0 < y1 <= height):
            raise ValueError('fixedCrop exceeds the source canvas.')
        if any(b[0] < x0 or b[1] < y0 or b[2] > x1 or b[3] > y1 for b in bounds):
            raise ValueError('fixedCrop would cut off animation pixels.')
        union = fixed_crop
    cw,ch = union[2]-union[0],union[3]-union[1]
    gutter = 2
    cols,rows = 4096//(cw+gutter*2),4096//(ch+gutter*2)
    capacity = cols*rows
    if capacity < 1: raise ValueError('A frame exceeds the atlas limit.')
    out = job/'final'
    out.mkdir(exist_ok=False)
    pages,frames = [],[]
    for start in range(0,len(images),capacity):
        count = min(capacity,len(images)-start)
        page_cols = min(cols,count)
        page_rows = math.ceil(count/page_cols)
        atlas = Image.new('RGBA',(page_cols*(cw+2*gutter),page_rows*(ch+2*gutter)))
        page_index = len(pages)
        for offset in range(count):
            index = start+offset
            x,y=(offset%page_cols)*(cw+2*gutter)+gutter,(offset//page_cols)*(ch+2*gutter)+gutter
            cropped=images[index].crop(union)
            atlas.paste(cropped,(x,y))
            # Timing is explicitly selected; all-frame path keeps each original PTS interval.
            duration = review['durations'][index]
            frames.append({'Index':index,'SourceFrame':selected[index],'Page':page_index,'X':x,'Y':y,'Duration':duration})
        name=f'wildrose_idle_{page_index:02d}.png'
        atlas.save(out/name)
        # Verify stored cells against the matte pixels, not the pre-key RGB source.
        saved=Image.open(out/name).convert('RGBA')
        for frame in frames[start:start+count]:
            actual=np.asarray(saved.crop((frame['X'],frame['Y'],frame['X']+cw,frame['Y']+ch)))
            expected=np.asarray(images[frame['Index']].crop(union))
            assert np.array_equal(actual,expected)
        pages.append({'File':name,'Width':atlas.width,'Height':atlas.height,'Sha256':digest(out/name)})
    scale=placement['sourcePixelsPerVideoPixel']
    origin=placement['sceneSourceCropOrigin']
    scene_rect=[origin[0]+union[0]*scale,origin[1]+union[1]*scale,cw*scale,ch*scale]
    manifest={'SourceVideoSha256':meta['sourceSha256'],'IllustrationRect':scene_rect,'CellSize':[cw,ch],
              'Pages':pages,'Frames':frames,'SourceSize':meta['sourceSize'],'FixedCrop':union,
              'Duration':sum(f['Duration'] for f in frames),'OriginalFrameCount':meta['frameCount'],
              'OriginalDuration':meta['duration'],'TextureRGBABytes':sum(p['Width']*p['Height']*4 for p in pages),
              'LoopReview':review['loopReview'],'PixelCopyVerified':True,'Review':review}
    write(out/'clip.json',manifest)
    shutil.copyfile(job/'background.png',out/'background.png')
    if placement.get('makeContactShadow', True):
        make_shadow(images[0],placement,out/'contact_shadow.png')
    print(json.dumps({k:manifest[k] for k in ['CellSize','Duration','TextureRGBABytes','PixelCopyVerified']},ensure_ascii=False))


def make_shadow(frame, placement, path):
    alpha=np.asarray(frame)[:,:,3]
    mask=Image.new('L',frame.size)
    draw=ImageDraw.Draw(mask)
    # Only the reclining cat's underside, excluding the raised tail and both people.
    for x in range(round(frame.width*.35),round(frame.width*.82)):
        ys=np.where(alpha[round(frame.height*.72):,x]>180)[0]
        if len(ys):
            y=int(ys[-1])+round(frame.height*.72)
            draw.line((x,y-1,x,y+5),fill=90,width=1)
    mask=mask.filter(ImageFilter.GaussianBlur(5))
    shadow=Image.new('RGBA',frame.size,(65,39,32,0));shadow.putalpha(mask)
    scale=placement['sourcePixelsPerVideoPixel']
    shadow=shadow.resize((round(frame.width*scale),round(frame.height*scale)),Image.Resampling.LANCZOS)
    canvas=Image.new('RGBA',tuple(placement['sceneSourceSize']))
    canvas.alpha_composite(shadow,tuple(placement['sceneSourceCropOrigin']))
    canvas.save(path)


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action',choices=['matte','pack'])
    parser.add_argument('--job',type=Path,required=True)
    parser.add_argument('--version',default='matte_v1')
    args=parser.parse_args()
    {'matte':matte,'pack':pack}[args.action](args.job,args.version)
