from pathlib import Path
import subprocess
import json
import shutil
from datetime import datetime

work = Path(__file__).resolve().parent
ffmpeg = next((work / "도구").rglob("ffmpeg*.exe"))
deliver = work.parents[1] / "영상" / "데모1_하이라이트"
deliver.mkdir(parents=True, exist_ok=True)
final = deliver / "데모1_하이라이트_19초.mp4"

# Source and output timing use 30 fps frame boundaries. No speed changes.
clips = [
    {"name": "목장 시설 건설", "category": "lobby", "start_frame": 546, "frames": 66},
    {"name": "페코페코 훈련", "category": "lobby", "start_frame": 352, "frames": 132},
    {"name": "레이스 출발과 질주", "category": "race", "start_frame": 1269, "frames": 132},
    {"name": "페코페코 필살기", "category": "race", "start_frame": 2697, "frames": 120},
    {"name": "막판 추월과 결승선 통과", "category": "race", "start_frame": 3435, "frames": 192},
]
fps = 30
transition = 0.6
command = [str(ffmpeg), "-hide_banner", "-y", "-filter_complex_threads", "4"]
for clip in clips:
    command += ["-ss", f"{clip['start_frame'] / fps:.9f}",
                "-t", f"{clip['frames'] / fps:.9f}", "-i", "source.mp4"]

filters = []
for i, clip in enumerate(clips):
    filters.append(
        f"[{i}:v]trim=end_frame={clip['frames']},"
        f"setpts=PTS-STARTPTS,setsar=1,format=yuv420p,fps=30,settb=1/30[v{i}]"
    )
    filters.append(
        f"[{i}:a]atrim=duration={clip['frames'] / fps:.9f},"
        f"asetpts=PTS-STARTPTS,aresample=48000[a{i}]"
    )

duration = clips[0]["frames"] / fps
video, audio = "v0", "a0"
timeline = [{**clips[0], "output_start": 0.0, "output_end": duration}]
for i in range(1, len(clips)):
    offset = round(duration - transition, 9)
    filters.append(
        f"[{video}][v{i}]xfade=transition=fade:duration={transition}:offset={offset},"
        f"fps=30,settb=1/30[x{i}]"
    )
    filters.append(
        f"[{audio}][a{i}]acrossfade=d={transition}:c1=tri:c2=tri[ax{i}]"
    )
    video, audio = f"x{i}", f"ax{i}"
    duration = round(offset + clips[i]["frames"] / fps, 9)
    timeline.append({**clips[i], "output_start": offset, "output_end": duration})

filters += [
    f"[{video}]tpad=stop_mode=clone:stop_duration=0.1,trim=end_frame=570,"
    f"fade=t=out:st=18.6:d=0.4,format=yuv420p[vout]",
    f"[{audio}]afade=t=in:d=0.05,afade=t=out:st=18.6:d=0.4[aout]",
]
graph = ";\n".join(filters)
(work / "편집필터.txt").write_text(graph, encoding="utf-8")
command += [
    "-filter_complex", graph, "-map", "[vout]", "-map", "[aout]",
    "-t", "19", "-c:v", "libx264", "-preset", "slow", "-crf", "18",
    "-profile:v", "high", "-pix_fmt", "yuv420p", "-r", "30",
    "-c:a", "aac", "-b:a", "192k", "-ar", "48000",
    "-movflags", "+faststart", "-map_metadata", "-1",
    "-metadata", "comment=19s highlight; 30% lobby / 70% race; 0.6s cross-dissolves",
    "-threads", "6", "-progress", "encode_progress.txt", "highlight.mp4",
]
with (work / "인코딩로그.txt").open("w", encoding="utf-8") as log:
    result = subprocess.run(command, cwd=work, stdout=log, stderr=log)
if result.returncode:
    raise RuntimeError("FFmpeg encode failed; see log")
shutil.copyfile(work / "highlight.mp4", final)

record = {
    "original_requested_path": "D:/임시/데모1.mp4",
    "original_current_path": "D:/임시/데모원본.mp4",
    "original_bytes": (work / "source.mp4").stat().st_size,
    "duration_seconds": duration,
    "resolution": [1470, 828],
    "fps": fps,
    "playback_speed": 1.0,
    "cross_dissolve_seconds": transition,
    "lobby_seconds": 5.7,
    "race_seconds": 13.3,
    "ratio_method": "Count each cross-dissolve equally between its neighboring clips.",
    "audio": "Original audio is silent (-91 dBFS measured); no music added.",
    "timeline": timeline,
    "final_path": str(final),
}
(work / "편집구성.json").write_text(
    json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8"
)
print("ENCODE_COMPLETE", duration, final.stat().st_size)
