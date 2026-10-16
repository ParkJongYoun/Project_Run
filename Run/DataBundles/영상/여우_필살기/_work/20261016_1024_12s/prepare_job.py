import importlib.util, json
from pathlib import Path
work=Path(__file__).resolve().parent
skill=Path(r'D:\Project_SGL\ProjectSupport\Skills\sgl-video-generation')
spec=importlib.util.spec_from_file_location('sgl_prepare_video',skill/'scripts/prepare_video.py')
module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
packet=module.prepare(image=work.parents[1]/'여우_필살기_입력이미지_1024.png',uploaded_name='여우_필살기_입력이미지_1024.png',work_dir=work,prefix='video/project_run_fox_ultimate_1024_20261016/fox_somersault_fireworks_1024_12s',prompt=(work/'prompt_en.txt').read_text(encoding='utf-8'),duration=12.0,seed=2026101602)
for slot in packet['overrides']:
    if slot['address']=='115.megapixels':slot['value']=1.0
packet['record'].update({'output_size':[1024,1024],'steps':25,'turbo':False,'subject':'Fox mount with seated rider','action':'One forward somersault, four-paw landing, low crouch, explosive launch, sustained gallop','background':'Moonlit fantasy racecourse, castle, foxfire and fireworks','source_workflow':str(skill/'assets/h3-i2v.json'),'generation_purpose':'Final skill cutscene with Unity integration','requested_count':1})
(work/'slot-packet.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'workflow_path':packet['workflow_path'],'expected_frames':packet['record']['expected_frames'],'expected_seconds':packet['record']['expected_seconds']},ensure_ascii=True))
