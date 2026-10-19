from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw

root = Path(__file__).parent
project = root.parents[2]
for name in ['windmill','waterfall','hot_spring','race_track']:
    v = np.loadtxt(root / f'{name}_정점.txt')
    t = np.loadtxt(root / f'{name}_삼각형.txt', dtype=int).reshape(-1,3)
    image_path = next((project / 'Assets/ArtBundles/Ranch/TripoModels' / name / 'Textures').glob('BaseColor.*'))
    texture = np.asarray(Image.open(image_path).convert('RGB'))
    uv = v[t,3:].mean(axis=1)
    colors = texture[np.clip(((1-uv[:,1])*(texture.shape[0]-1)).astype(int),0,texture.shape[0]-1), np.clip((uv[:,0]*(texture.shape[1]-1)).astype(int),0,texture.shape[1]-1)]
    centers = v[t,:3].mean(axis=1)
    if name == 'windmill':
        x,y,z = centers.T
        plane = .03623*x + .11589*y - .82633
        cross = (abs(abs(x)-abs(y-1.85)) < .59) | (x*x+(y-1.85)**2 < .32**2)
        rotor = (z < plane+.09) & (y > .67) & cross
        print('rotor triangles',sum(rotor),'of',len(t))
        (root / '풍차_날개삼각형.json').write_text(json.dumps({'Indices':np.flatnonzero(rotor).tolist()}))
    canvas = Image.new('RGB',(1500,750),'white')
    draw = ImageDraw.Draw(canvas)
    for view in range(3):
        if view == 0: ax,ay,depth=0,1,2
        elif view == 1: ax,ay,depth=2,1,0
        else: ax,ay,depth=0,2,1
        low=v[:,:3].min(axis=0);high=v[:,:3].max(axis=0)
        scale=min(440/(high[ax]-low[ax]),640/(high[ay]-low[ay]))
        order=np.argsort(centers[:,depth])[::-1] if view<2 else np.argsort(centers[:,depth])
        for i in order:
            p=v[t[i],:3]
            points=[(view*500+30+(a[ax]-low[ax])*scale,700-(a[ay]-low[ay])*scale) for a in p]
            col=tuple(map(int,colors[i]))
            if name=='windmill' and rotor[i]: col=(220,70,40)
            draw.polygon(points,fill=col)
    canvas.save(root / f'{name}_구조확인.png')
    if name in ['waterfall','hot_spring']:
        c=colors.astype(float)/255
        water=(c[:,1]>c[:,0]*1.2)&(c[:,2]>c[:,0]*1.2)&(c[:,1]>.45)
        for lo,hi in zip(np.arange(0,2.1,.2),np.arange(.2,2.3,.2)):
            pts=centers[water&(centers[:,1]>=lo)&(centers[:,1]<hi)]
            if len(pts): print(name,round(lo,1),'n=',len(pts),'center=',np.round(pts.mean(axis=0),2),'min=',np.round(pts.min(axis=0),2),'max=',np.round(pts.max(axis=0),2))
