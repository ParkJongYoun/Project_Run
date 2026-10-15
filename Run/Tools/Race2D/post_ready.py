"""Extract and key downloaded clips; never marks a clip visually reviewed."""
from pathlib import Path
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'DataBundles/영상/2D탈것_달리기_20261016'

def run(*args):
    subprocess.run([sys.executable,'-B',*map(str,args)],cwd=ROOT,check=True)

for key in sys.argv[1:]:
    job=WORK/'_post'/key
    videos=list((WORK/'originals'/key).glob('*.mp4'))
    if len(videos)!=1:raise ValueError(f'Expected one downloaded source: {key}')
    if not (job/'frames.json').exists():
        run('Tools/SpriteAnimation/prepare_video_animation.py','extract','--source',videos[0],'--job',job,'--motion','Run','--output-directory',WORK/'sprites'/key)
    if not (job/'matte_v1/source.png').exists():
        settings=['--settings','Tools/Race2D/matte_pink.json'] if key.startswith(('poring_','ghost_')) else []
        run('Tools/SpriteAnimation/prepare_video_animation.py','matte','--job',job,'--version','matte_v1',*settings)
    run('Tools/Race2D/inspect_run.py',key,'--clean')
