from pathlib import Path
import json

root = Path('D:/Project_Run/Run')
out = root / 'DataBundles/UI/목장/훈련영상_20261020/작업기록'
out.mkdir(parents=True, exist_ok=True)
base = json.loads((root / 'DataBundles/UI/필살기영상/미노타우로스/작업기록/3D_영상워크플로.json').read_text(encoding='utf-8-sig'))
common = '''Animate the supplied image as a polished fantasy ranch training movie, 6.6 seconds. Preserve the exact character identity, face, costume, bright warm anime illustration, clean outlines, colorful flowers, wood fences, blue-roof cottage and fountain. Genuine fluid character animation with changing poses, not camera motion on a still painting. Use a smooth side-tracking camera that keeps the complete subject and training equipment visible with generous margins, no cuts or close-ups. Fountain water flows, small leaves sway gently, warm sunlight stays stable. Maintain crisp details and grounded contact shadows. One character only, no new characters, no text, logos, interface, extra limbs, body transformations or abrupt changes in style. End with a readable successful training movement and a calm settling pose. '''
items = [
('Poring','포링','포링_탄성점프_훈련장면.png',common + '''The single pink translucent jelly Poring has two big red-brown eyes, a smiling mouth, rosy cheeks and one rounded jelly crest, with no limbs. It completes three distinct elastic hurdle jumps: finish the initial jump with a soft squash on landing, stretch upward and spring over the next low red-white wooden hurdle, then compress and launch a bigger final hop. Keep its face coherent during elastic deformation. Tiny dust puffs at contact, a few sparkling droplets in sunlight, lively joyful movement. The hurdles remain intact and stationary in the ground. The camera follows gently along the course. The final landing gives a proud cheerful jelly wobble.'''),
('Minorous','미노타우르스','미노타우르스_저항질주_훈련장면.png',common + '''The muscular golden-brown minotaur has curled ivory horns, dark mane, a gold nose ring, leather bracers, leather waist armor and dark cloven hooves. He pulls the existing wooden sled loaded with three logs using taut ropes attached at his waist and hands as depicted. A strenuous resistance sprint with heavy alternating hoof contacts, clear pumping shoulder movement, flexing muscles, powerful forward lean and swinging tail. The heavy sled follows continuously behind him and slides on the dirt, throwing small dust trails and shallow grooves. Ropes stay connected and under tension. Keep the whole minotaur AND sled visible. Slow heavy first strides accelerate into a confident steady rhythm; end with two deliberate slowing strides and the sled settling naturally, a determined proud expression.'''),
('Pecopeco','페코페코','페코페코_허들질주_훈련장면.png',common + '''The single golden-yellow Pecopeco bird has a white chest, orange beak, blue face marking, round red-orange side ornaments, fluffy golden crest and tail, two small wings and exactly two dark clawed legs. Athletic agility training: complete the initial hurdle leap, land lightly on alternating feet, perform quick nimble running strides and clear two more red-white low hurdles with a compact tuck of both legs. Wings flick outward for balance, tail feathers bounce naturally. Keep the bird running and jumping near the ground, never flying. Small sunlit dust puffs and a few loose golden feathers emphasize speed. Track the bird smoothly so its head, feet, tail and the next hurdle stay visible. End with a light landing and energetic proud wing flick.'''),
]
for i,(key,ko,img,prompt) in enumerate(items):
    graph=json.loads(json.dumps(base))
    graph['1']['inputs']['image']=img
    graph['5']['inputs'].update(prompt=prompt,width=1024,height=576,length=158)
    graph['6']['inputs']['noise_seed']=20261020010+i
    graph.pop('14',None)
    graph['8']['inputs'].update(model=['2',0],steps=25)
    graph['9']['inputs']['model']=['2',0]
    graph['13']['inputs']['filename_prefix']='ProjectRunTraining20261020/'+key+'_1024_v01'
    (out/(ko+'_워크플로.json')).write_text(json.dumps(graph,ensure_ascii=False,indent=2),encoding='utf-8')
    (out/(ko+'_프롬프트.txt')).write_text(prompt,encoding='utf-8')
print(str(out))
