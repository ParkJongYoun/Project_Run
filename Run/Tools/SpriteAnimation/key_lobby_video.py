"""Re-key a flat-magenta video using a local foreground color estimate and a soft trimap."""
import argparse
import json
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import binary_erosion, distance_transform_edt, gaussian_filter


def key_frame(image, settings):
    rgb = np.asarray(image.convert('RGB'), dtype=np.float32)
    h,w = rgb.shape[:2]
    border = np.concatenate((rgb[:8].reshape(-1,3),rgb[-8:].reshape(-1,3),rgb[:,:8].reshape(-1,3),rgb[:,-8:].reshape(-1,3)))
    background = np.median(border,axis=0)
    excess = np.minimum(rgb[:,:,0],rgb[:,:,2])-rgb[:,:,1]
    region = np.zeros((h,w),bool)
    x0,y0,x1,y1 = settings['pinkSearchRect']
    region[y0:y1,x0:x1] = True
    pink = region & (rgb[:,:,0]-rgb[:,:,2]>40) & (rgb[:,:,1]>35)
    foreground = (excess < 5) | pink
    core = binary_erosion(foreground,iterations=settings.get('foregroundColorReferenceErosionPixels',2))
    # Narrow gaps between an arm and the body also contain the key color. A broad
    # interior threshold would seal those gaps and preserve their magenta pixels.
    interior = distance_transform_edt((excess < settings.get('interiorMaxMagentaExcess',40)) | pink) > settings.get('sourceInteriorPreservedBeyondPixels',4)
    distance,nearest = distance_transform_edt(~core,return_indices=True)
    estimate = rgb[nearest[0],nearest[1]]
    vector = estimate-background
    alpha = np.clip(np.sum((rgb-background)*vector,axis=2)/np.maximum(np.sum(vector*vector,axis=2),1),0,1)
    alpha[core] = 1
    alpha[(distance>8)&(alpha<.3)] = 0
    # Remove low-amplitude chroma-block haze without an all-silhouette hard erosion.
    low,high = settings.get('alphaThresholdRemap',[.075,.99])
    alpha = np.clip((alpha-low)/(high-low),0,1)
    smoothed = gaussian_filter(alpha,settings.get('alphaGaussianSigma',.5))
    weight = settings.get('alphaGaussianWeight',.45)
    alpha = np.where(core|interior,1,alpha*(1-weight)+smoothed*weight)
    alpha[alpha<.015] = 0
    corrected = rgb.copy()
    edge = (~core)&(~interior)&(alpha>0)
    # Recover straight-alpha RGB from the color equation, then stabilize chroma with
    # the nearest uncontaminated foreground. Keep original interior RGB untouched.
    unmix = (rgb-(1-alpha[:,:,None])*background)/np.maximum(alpha[:,:,None],.08)
    luma_weights = np.array([.2126,.7152,.0722],np.float32)
    recovered_luma = np.sum(unmix*luma_weights,axis=2)
    reference_luma = np.maximum(np.sum(estimate*luma_weights,axis=2),1)
    gain = np.clip(recovered_luma/reference_luma,.85,1.15)
    stable = estimate*gain[:,:,None]
    repaired = stable
    corrected[edge] = repaired[edge]
    border_distance = distance_transform_edt(alpha > .95)
    spill_edge = (border_distance < settings.get('edgeSpillRepairBandPixels',5)) & (alpha > 0) & (excess > 5) & ~pink
    corrected[spill_edge] = stable[spill_edge]
    rgba = np.dstack((np.clip(corrected,0,255),alpha*255)).round().astype('uint8')
    rgba[rgba[:,:,3]==0,:3]=0
    return Image.fromarray(rgba),{'background':background.tolist(),'corePixels':int(core.sum()),'partialPixels':int(((alpha>0)&(alpha<1)).sum()),'pinkPixels':int(pink.sum())}


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--settings',type=Path,required=True)
    parser.add_argument('--frames',default='all')
    args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    files=sorted(args.source.glob('frame_*.png'))
    if args.frames!='all':
        selected={int(n) for n in args.frames.split(',')}
        files=[p for p in files if int(p.stem.split('_')[-1]) in selected]
    settings=json.loads(args.settings.read_text(encoding='utf-8-sig'))
    records=[]
    for path in files:
        destination=args.output/path.name
        if destination.exists(): raise FileExistsError(destination)
        rgba,stats=key_frame(Image.open(path),settings)
        rgba.save(destination)
        stats.update(frame=path.name,bounds=rgba.getbbox())
        records.append(stats)
    (args.output/'inspection.json').write_text(json.dumps({'algorithm':'local-foreground-projection-v2','settings':settings,'frames':records},indent=2),encoding='utf-8')
    print(json.dumps({'frames':len(records),'output':str(args.output)}))
