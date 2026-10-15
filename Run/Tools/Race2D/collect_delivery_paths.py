"""Exact source/QA handoff path list; excludes raw frames, failed mattes and logs."""
from pathlib import Path
import json

root=Path(__file__).resolve().parents[2]
repo=root.parent
work=root/'DataBundles/영상/2D탈것_달리기_20261016'
qa=root/'DataBundles/검증/2D레이스_20261016'
paths=set()
for folder in ('Tools/Race2D','Tools/SpriteAnimation'):
    paths.update(p for p in (root/folder).rglob('*') if p.is_file() and p.suffix in ('.py','.json','.md'))
paths.update(p for p in (work/'_work').rglob('*') if p.is_file() and p.suffix in ('.json','.txt'))
paths.update(work/p for p in ('jobs.json','view_status.json'))
for manifest in (work/'sprites').glob('*/clip.json'):
    clip=json.loads(manifest.read_text(encoding='utf-8-sig'))
    if clip.get('reviewStatus')!='reviewed':continue
    paths.add(manifest)
    paths.update(manifest.parent/page['file'] for page in clip['pages'])
    paths.update((work/'originals'/clip['key']).glob('*.mp4'))
    post=work/'_post'/clip['key']
    paths.add(post/'frames.json')
    paths.add(post/'inspection/diagnostics.json')
    # Preserve the exact key settings/history that produced the selected matte.
    paths.update(p for p in post.glob('matte_v*/*.json') if p.name not in ('manifest.draft.json','matte-inspection.json'))
for pattern in ('Scene_*_validation.json','Scene_*_sprite_playback.json','Scene_*_animated_*.png','Scene_*_movie_paused.png','Scene_*_result.png','*camera_matrix.jpg','sprite_alpha_shader.json','unity_dependencies.json','webgl_build.json','webgl_browser*.json','webgl_browser*.png','webgl_browser*.jpg'):
    paths.update(qa.glob(pattern))
paths.update((qa/'camera_matrix').glob('Scene_*.png'))
paths.add(root/'DataBundles/기획/작업기록/2D레이스_야간_20261016.json')
paths.update((root/'DataBundles/기획/작업기록').glob('2D레이스_완료보고_20261016.*'))
missing=[str(p) for p in paths if not p.is_file()]
if missing:raise ValueError(missing)
paths=sorted(paths)
(qa/'delivery_commit_paths.nul').write_bytes(b''.join(p.relative_to(repo).as_posix().encode('utf-8')+b'\0' for p in paths))
print(json.dumps({'files':len(paths),'bytes':sum(p.stat().st_size for p in paths)},ensure_ascii=False))
