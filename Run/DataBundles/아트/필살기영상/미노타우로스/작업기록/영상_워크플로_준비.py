import json
from pathlib import Path

root=Path(__file__).parent
base=Path('D:/Project_Run/Run/DataBundles/기획/심사데모_제작자료/훈련영상_제작/Minorous_workflow.json')
prompt='''A spectacular 6.6-second ultimate-skill animation for a fantasy racing game. Preserve the exact minotaur in the first image: stocky muscular orange-furred body, auburn mane, large upright ivory horns, amber eyes, cream chest, teal waist sash, brown shorts, white wrist wraps and dark cloven hooves. Keep his cute but powerful proportions, two horns, two arms and two legs, with no weapons or rider. Polished hand-drawn 2D anime animation with expressive clean outlines and painted scenery. The skill is Formation Breaker, explosive physical acceleration.
0.0-1.2 seconds: the low camera pushes in slightly as the braced minotaur exhales visible steam, tightens both fists and compresses his powerful legs. Grass bends outward and small stones tremble. The grand stone stadium arches, colorful pennants, grass racing track and distant green mountains remain clearly visible. Golden sparks coil around his wrists and hooves.
1.2-2.1 seconds: sharp cinematic cut to a fierce eye-and-horns close-up. His eyes catch a brilliant amber glint, mane and teal sash snap backward as pressure rises. The stadium is still identifiable behind him.
2.1-2.8 seconds: dramatic ground-level close-up of one cloven hoof stamping into the track. A single enormous golden circular shockwave blasts grass, dust and pebbles outward, with a brief crisp impact flash and strong camera jolt. The flash clears immediately.
2.8-5.8 seconds: hard cut to a wide low three-quarter tracking shot. He launches into an extremely fast heavy sprint along the track, leaning forward, arms driving and hooves hammering, each stride throwing up dust. A huge translucent golden bull-shaped energy silhouette surges behind him for a moment, then stretches into brilliant golden ribbons. The stadium arches sweep past in parallax. Clear readable body movement, spectacular sparks, sunlight and debris. Energy curls around his silhouette without hiding his face or costume. Accelerate visibly and forcefully.
5.8-6.6 seconds: the camera swings toward his front for a powerful charging hero shot, fierce confident gaze, one fist driving forward, the golden wake splits outward to both sides, with the stone arches and sky visible behind. Finish on continued forward momentum for return to racing. Fluid character animation, changing poses and genuine locomotion, bold dynamic camera cuts, high contrast golden effects against blue sky and green track. No static slideshow, no panel borders, no captions, no logos, no interface, no extra limbs, no character redesign, no prolonged whiteout, no background replaced by abstract speed lines.'''
workflow=json.loads(base.read_text(encoding='utf-8-sig'))
workflow['1']['inputs']['image']='2D_시작장면.png'
workflow['5']['inputs'].update(prompt=prompt,width=512,height=288,length=158)
workflow['5']['inputs'].pop('last_frame',None)
workflow['6']['inputs']['noise_seed']=1810202601
workflow['14']={'class_type':'LoraLoaderModelOnly','inputs':{'model':['2',0],'lora_name':'minimax_h3_fl2v_turbo_8step_v1.0_comfyui_bf16.safetensors','strength_model':1.0}}
workflow['8']['inputs'].update(model=['14',0],steps=8)
workflow['9']['inputs']['model']=['14',0]
workflow['13']['inputs']['filename_prefix']='ProjectRunUltimate/Minorous_2D_512_v01'
(root/'2D_생성프롬프트.txt').write_text(prompt,encoding='utf-8')
(root/'2D_영상워크플로.json').write_text(json.dumps(workflow,ensure_ascii=False,indent=2),encoding='utf-8')
print(root/'2D_영상워크플로.json')
prompt3=prompt.replace('stocky muscular orange-furred body, auburn mane, large upright ivory horns, amber eyes, cream chest, teal waist sash, brown shorts, white wrist wraps and dark cloven hooves','huge ochre-brown muscular upper body, angular bull head, upright dark brown mohawk, two ivory horns curling downwards, glowing red eyes, one gold nose ring, X-shaped shoulder scars, dark maroon leather chest harness with silver trim and rivets, large maroon bracers and huge gloved fists, short maroon waist armor over dark furry legs, dark cloven hooves').replace('cute but powerful proportions','broad-shouldered powerful 3D game-model proportions').replace('Polished hand-drawn 2D anime animation with expressive clean outlines and painted scenery','Polished three-dimensional cel-shaded game cinematic, stylized angular polygonal forms, strong toon shadows, glossy metal rim accents, cinematic lighting and rich three-dimensional stadium scenery. Maintain clearly 3D game character rendering throughout, not a hand-drawn illustration').replace('mane and teal sash snap backward','mohawk and leather straps flutter backward').replace('brilliant amber glint','brilliant red-gold glint')
workflow['1']['inputs']['image']='3D_시작장면.png'
workflow['5']['inputs']['prompt']=prompt3
workflow['6']['inputs']['noise_seed']=1810202602
workflow['13']['inputs']['filename_prefix']='ProjectRunUltimate/Minorous_3D_512_v01'
(root/'3D_생성프롬프트.txt').write_text(prompt3,encoding='utf-8')
(root/'3D_영상워크플로.json').write_text(json.dumps(workflow,ensure_ascii=False,indent=2),encoding='utf-8')
