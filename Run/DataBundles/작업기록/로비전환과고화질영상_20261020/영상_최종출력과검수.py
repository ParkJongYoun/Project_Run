from pathlib import Path
from fractions import Fraction
import argparse, hashlib, json, sys
import av
import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.stdout.reconfigure(encoding='utf-8')
parser=argparse.ArgumentParser()
parser.add_argument('source',type=Path)
parser.add_argument('output',type=Path)
parser.add_argument('--width',type=int,default=1024)
parser.add_argument('--height',type=int,default=576)
parser.add_argument('--seconds',type=float,default=7)
parser.add_argument('--crf',default='20')
args=parser.parse_args()
args.output.parent.mkdir(parents=True,exist_ok=True)
record=args.output.parent/'작업기록'
record.mkdir(exist_ok=True)
with av.open(str(args.source)) as container:
    stream=container.streams.video[0]
    fps=float(stream.average_rate)
    frames=[frame for frame in container.decode(stream)]
source_info={'width':stream.width,'height':stream.height,'fps':fps,'frames':len(frames),'seconds':len(frames)/fps}
target_count=round(args.seconds*24)
with av.open(str(args.output),'w',options={'movflags':'+faststart'}) as output:
    stream=output.add_stream('libx264',rate=24)
    stream.width=args.width
    stream.height=args.height
    stream.pix_fmt='yuv420p'
    stream.codec_context.color_primaries=1
    stream.codec_context.color_trc=1
    stream.codec_context.colorspace=1
    stream.codec_context.color_range=1
    stream.codec_context.thread_count=2
    stream.options={'crf':args.crf,'preset':'slow','profile':'high','level':'4.0','maxrate':'6000k' if args.height>=1080 else '3500k','bufsize':'12000k' if args.height>=1080 else '7000k'}
    for i in range(target_count):
        index=round(i*(len(frames)-1)/(target_count-1))
        rgb=av.VideoFrame.from_ndarray(frames[index].to_ndarray(format='rgb24'),format='rgb24')
        frame=rgb.reformat(width=args.width,height=args.height,format='yuv420p',dst_colorspace='ITU709')
        frame.pts=i
        frame.time_base=Fraction(1,24)
        for packet in stream.encode(frame): output.mux(packet)
    for packet in stream.encode(): output.mux(packet)
del frames
sample_indices=set(np.linspace(0,target_count-1,16,dtype=int).tolist())
sheet=Image.new('RGB',(1280,4*205),'#17212b')
draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',14)
means=[]
motion=[]
prior=None
sample=0
with av.open(str(args.output)) as container:
    stream=container.streams.video[0]
    count=0
    for i,frame in enumerate(container.decode(stream)):
        small=frame.reformat(width=64,height=36,format='rgb24').to_ndarray().astype(np.float32)
        means.append(float(small.mean()))
        if prior is not None: motion.append(float(np.abs(small-prior).mean()))
        prior=small
        if i in sample_indices:
            image=frame.to_image().convert('RGB'); image.thumbnail((320,180))
            x,y=(sample%4)*320,(sample//4)*205
            sheet.paste(image,(x,y));draw.text((x+5,y+181),f'{i/24:.2f}s / {i}',font=font,fill='white');sample+=1
        count+=1
    info={'file':str(args.output),'width':stream.width,'height':stream.height,'fps':float(stream.average_rate),'frames':count,'seconds':count/float(stream.average_rate),'codec':stream.codec_context.name,'color_primaries':stream.codec_context.color_primaries,'color_trc':stream.codec_context.color_trc,'colorspace':stream.codec_context.colorspace,'color_range':stream.codec_context.color_range,'audio':len(container.streams.audio)>0,'bytes':args.output.stat().st_size}
info.update(source=str(args.source),source_info=source_info,sha256=hashlib.sha256(args.output.read_bytes()).hexdigest(),black_frames=sum(x<2 for x in means),white_frames=sum(x>253 for x in means),mean_adjacent_change=float(np.mean(motion)),generation_note='원본 생성 후 시간축을 정확히 7초로 조정하여 24fps 출력. 스킬 FHD 출력은 원본 해상도에서 리샘플링한 파일이며 네이티브 FHD 생성으로 표기하지 않음.')
assert info['width']==args.width and info['height']==args.height and info['frames']==target_count
assert info['black_frames']==0 and info['white_frames']==0
assert info['color_primaries']==1 and info['color_trc']==1 and info['colorspace']==1
sheet.save(record/(args.output.stem+'_전체구간_검수.jpg'),quality=93)
(record/(args.output.stem+'_파일검증.json')).write_text(json.dumps(info,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(info,ensure_ascii=False,indent=2))
