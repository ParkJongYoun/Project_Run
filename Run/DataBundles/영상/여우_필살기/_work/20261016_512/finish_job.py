import argparse, hashlib, json, re, shutil, subprocess
from datetime import datetime, timezone
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--source', required=True)
p.add_argument('--server-original', required=True)
p.add_argument('--server-seconds', required=True, type=float)
args = p.parse_args()
work = Path(__file__).resolve().parent
root = work.parents[1]
source = Path(args.source)
original = Path(args.server_original)
output = root/'여우_필살기_여우불질주_512_테스트.mp4'
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
if source.resolve() != output.resolve(): shutil.copy2(source,output)
assert sha(original) == sha(source) == sha(output), 'Original MP4 hash mismatch'
ffmpeg = r'C:\Users\ParkJongYoun\AppData\Local\Temp\run-video-tools\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe'
# Decode only to the null sink to count frames. The delivered MP4 is never re-encoded.
probe = subprocess.run([ffmpeg,'-hide_banner','-i',str(output),'-map','0:v:0','-f','null','-'],capture_output=True,text=True,encoding='utf-8',errors='replace')
if probe.returncode: raise RuntimeError(probe.stderr)
metadata = probe.stderr
(work/'output_metadata.txt').write_text(metadata,encoding='utf-8')
frames = int(re.findall(r'frame=\s*(\d+)',metadata)[-1])
duration = re.search(r'Duration: (\d+):(\d+):([\d.]+)',metadata)
stream = next(line for line in metadata.splitlines() if 'Stream #0:0' in line and 'Video:' in line)
resolution = re.search(r'\b(\d{2,5})x(\d{2,5})\b',stream)
fps = float(re.search(r'([\d.]+) fps',stream)[1])
submission = json.loads((work/'submission.json').read_text(encoding='utf-8'))
now = datetime.now(timezone.utc)
start = datetime.fromisoformat(submission['submitted_at_utc'].replace('Z','+00:00'))
record = {
    'prompt_id':submission['prompt_id'],'status':'completed',
    'submitted_at_utc':submission['submitted_at_utc'],'completion_observed_at_utc':now.isoformat(),
    'server_execution_seconds':args.server_seconds,
    'observed_total_seconds_including_queue_and_polling':round((now-start).total_seconds(),3),
    'original_comfy_output':str(original),'delivered_output':str(output),
    'sha256':sha(output),'original_bytes_preserved':True,'bytes':output.stat().st_size,
    'width':int(resolution[1]),'height':int(resolution[2]),'fps':fps,'frames':frames,
    'video_seconds_from_frames':frames/fps,
    'container_duration_seconds':int(duration[1])*3600+int(duration[2])*60+float(duration[3]),
    'video_stream':stream.strip(),'generated_count':1,
    'delivery_processing':'Original MP4 copied without trimming, re-encoding, chroma removal or compositing.'
}
(work/'result.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
note=root/'제작기록_20261016.txt'
text=note.read_text(encoding='utf-8').replace('상태: 생성 중. 완료 정보는 아래에 추가한다.','상태: 생성 완료. 원본 MP4를 복사해 납품.')
text += '\n\n완료 기록\n'
text += f'영상: {output.name}\n실제 규격: {record["width"]}×{record["height"]}, {fps:g}fps, {frames}프레임, {frames/fps:.3f}초\n'
text += f'서버 실행시간: {args.server_seconds:.2f}초\n대기·완료 폴링·가져오기를 포함한 관측 경과시간: {record["observed_total_seconds_including_queue_and_polling"]:.2f}초\n'
text += f'원본: {original}\nSHA-256: {record["sha256"]}\n원본과 납품본 해시 일치. 재인코딩·트리밍·배경 제거 없음.\n'
text += '생성 수량 1개. 추가 시드·프롬프트 시험 없음. 영상 메타데이터 및 끝까지 디코딩 확인. 컷 타이밍·외형 보존 수준은 재생 검토 대상.\n'
note.write_text(text,encoding='utf-8')
print(json.dumps(record,ensure_ascii=True))
