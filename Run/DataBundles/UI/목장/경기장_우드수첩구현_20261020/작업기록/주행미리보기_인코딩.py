from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
files = sorted((root / '검증' / '주행프레임').glob('*.jpg'))
if len(files) != 72:
    raise RuntimeError(f'Expected 72 captured frames, got {len(files)}')
durations = [max(16, min(1000, round((b.stat().st_mtime - a.stat().st_mtime) * 1000)))
             for a, b in zip(files, files[1:])]
durations.append(durations[-1])
frames = [Image.open(path).convert('RGB') for path in files]
output = root / '결과' / '경기장_스프라이트주행.webp'
frames[0].save(output, save_all=True, append_images=frames[1:], duration=durations,
               loop=0, quality=82, method=4)
with Image.open(output) as result:
    print(f'{output}\n{result.size}, {result.n_frames} frames, {sum(durations)} ms, {output.stat().st_size} bytes')
for frame in frames:
    frame.close()
