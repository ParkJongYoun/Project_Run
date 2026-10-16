import importlib.util, json
from pathlib import Path

work = Path(__file__).resolve().parent
skill = Path(r'D:\Project_SGL\ProjectSupport\Skills\sgl-video-generation')
spec = importlib.util.spec_from_file_location('sgl_prepare_video', skill/'scripts/prepare_video.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
packet = module.prepare(
    image=work.parents[1]/'여우_필살기_입력이미지_512.png',
    uploaded_name='여우_필살기_입력이미지_512.png',
    work_dir=work,
    prefix='video/project_run_fox_ultimate_20261016/fox_ultimate_512_int8_25steps',
    prompt=(work/'prompt_en.txt').read_text(encoding='utf-8'),
    duration=6.0,
    seed=2026101601,
)
for slot in packet['overrides']:
    if slot['address'] == '115.megapixels': slot['value'] = 0.25
packet['record'].update({
    'output_size': [512,512],
    'background': 'opaque navy Unity snapshot; cinematic cyan/gold effects, no chroma-key requirement',
    'resolution_reason': 'User explicitly requested lightweight 512 generation.',
    'source_scene': 'Assets/Scenes/Scene_All3D.unity',
    'source_runner': 'P_Runner_03 / RunnerId 2 / fox_001',
    'subject_priority': 'Fox mount primary, seated rider secondary; user correction applied.',
    'source_workflow': str(skill/'assets/h3-i2v.json'),
    'unet': 'minimax_h3_fl2va_pruned_int8_convrot.safetensors',
    'text_encoder': 'qwen3vl_32b_minimax_h3_nvfp4_awq.safetensors',
    'video_vae': 'minimax_h3_video_vae_int8_convrot.safetensors',
    'audio_vae': 'minimax_h3_audio_vae_fp32.safetensors',
    'turbo': False, 'steps': 25, 'sampler': 'res_multistep', 'scheduler': 'simple',
    'prompt_file': str(work/'prompt_en.txt'), 'requested_count': 1,
})
(work/'slot-packet.json').write_text(json.dumps(packet,ensure_ascii=False,indent=2), encoding='utf-8')
print(json.dumps({'workflow_path':packet['workflow_path'], 'record':packet['record']},ensure_ascii=True))
