import collections,csv,hashlib,json,os
from pathlib import Path
from migrate_assets import WORK,DEST,destination,save

p=json.loads((WORK/'plan.json').read_text('utf-8'))
with (WORK/'manifest.csv').open(encoding='utf-8-sig',newline='') as f:rows=list(csv.DictReader(f))
by_source={r['source']:r for r in rows};moves=[];root=DEST.resolve()
for src,old in p['mapping'].items():
    if '/Character/monster/' not in src or 'fellow_falcon' not in src:continue
    new=destination(src)
    if old==new:continue
    a=(DEST/old).resolve();b=(DEST/new).resolve()
    assert a.is_relative_to(root) and b.is_relative_to(root),'Move must stay in OldAsset'
    assert a.is_file() and not b.exists(),'Unexpected file state'
    assert hashlib.sha256(a.read_bytes()).hexdigest()==by_source[src]['destination_sha256'],'Asset content changed before move'
    moves.append((src,old,new,a,b))
for src,old,new,a,b in moves:
    b.parent.mkdir(parents=True,exist_ok=True)
    os.rename(a,b)
    am=Path(str(a)+'.meta');bm=Path(str(b)+'.meta')
    assert am.is_file() and not bm.exists()
    os.rename(am,bm)
    assert hashlib.sha256(b.read_bytes()).hexdigest()==by_source[src]['destination_sha256']
    by_source[src]['destination']='Assets/OldAsset/'+new
    p['mapping'][src]=new
with (WORK/'manifest.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
p['summary']['categories']=dict(collections.Counter(d.split('/')[0] for d in p['mapping'].values()))
save('plan.json',p);save('companion_moves.json',[{'source':s,'old_destination':a,'destination':b} for s,a,b,_,_ in moves])
print('Moved and hash-verified',len(moves),'companion assets with their original GUIDs.')
