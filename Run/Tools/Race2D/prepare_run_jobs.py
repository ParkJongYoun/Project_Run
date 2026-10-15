"""Prepare reproducible run-video jobs without submitting diffusion work."""
from pathlib import Path
import importlib.util
import json
import shutil

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'DataBundles/기획/시안/2D뷰세트_탈것6종_경기장전체_v1/characters'
WORK = ROOT / 'DataBundles/영상/2D탈것_달리기_20261016'
SKILL = Path('D:/Project_SGL/ProjectSupport/Skills/sgl-video-generation')

MOUNTS = {
    'tiger': ('the upright bipedal golden tiger mount carrying its original seated rider', 'The tiger RUNS ON ITS TWO HIND LEGS exactly as its bipedal reference anatomy: alternate left and right hind feet with distinct push-off and lifted-knee phases. Its short forearms swing slightly. Keep the seated rider attached to the saddle; the rider does not run independently.'),
    'poring': ('the pink rounded jelly mount with its blonde rider and leather saddle', 'The legless pink jelly mount performs a brisk low bouncing travel cycle IN PLACE: small soft squash on contact, elastic recovery and a brief low airborne phase. Keep its round mass, face and saddle intact. Do not invent legs or arms. The rider bobs subtly while staying seated.'),
    'fox': ('the white three-tailed fox mount with red ritual ropes and its green-clothed teal-haired rider', 'The four-legged fox gallops rhythmically IN PLACE. Alternate gathered legs under the body and a compact forward-reaching stride, with clear front and hind paw push-offs. Its three tails sway gently without changing their count. The rider stays seated and hands keep holding the reins.'),
    'ghost': ('the white floating ghost mount carrying a red-and-gold throne and its purple witch rider, with the original three little attendant ghosts and purple flames', 'The ghost travels by a smooth hovering run-equivalent cycle IN PLACE, with a small rhythmic rise and fall and a gentle trailing-cloth undulation. Keep the same ghost body, throne, witch rider, three little attendants and original flames. Do not invent legs, extra ghosts or extra effects. The witch stays seated with subtle arm and hair motion.'),
    'wolf': ('the black armored wolf mount with red armor spikes and its dark-haired olive-clothed rider', 'The four-legged wolf gallops rhythmically IN PLACE. Alternate gathered legs beneath the body with compact reaching strides and clearly separated paw push-off phases. Keep the tail and all armor pieces attached. The rider remains seated and holds the reins with subtle elbow motion.'),
    'boar': ('the dark brown armored boar mount with red-and-gold saddle and its copper-braided goggle-wearing rider', 'The four-legged boar runs briskly IN PLACE with short powerful strides, alternating hoof placement and a mild body bounce. Preserve its tusks, snout, saddle and armor. The rider stays seated, with small reactive arm and braid movement.'),
}
VIEWS = {
    '01_side': 'Keep the exact original SIDE camera and right-facing orientation throughout. The running direction is screen right, but the character does not translate. Never rotate toward the viewer or show the back.',
    '02_rear': 'Keep the exact original REAR FOLLOW camera throughout. We see the rider and mount FROM BEHIND, facing directly away. Feet cycle away from the viewer while the whole character stays in place. Never reveal a front face, side profile or orbit the camera.',
    '03_top': 'Keep the exact elevated oblique TOP VIEW of the reference throughout, looking down at the same high angle. Preserve the visible top surfaces, foreshortening and original diagonal heading toward lower right. Never lower the camera into a side view, flatten the perspective or rotate the character.',
}

def main():
    WORK.mkdir(parents=True, exist_ok=True)
    jobs = []
    for mount, (subject, action) in MOUNTS.items():
        for view, angle in VIEWS.items():
            key = mount + '_' + view
            job = WORK / '_work' / key
            job.mkdir(parents=True, exist_ok=True)
            source = SOURCE / mount / (view + '.png')
            upload = WORK / '_inputs' / ('run_' + key + '_20261016.png')
            upload.parent.mkdir(parents=True, exist_ok=True)
            if not upload.exists(): shutil.copy2(source, upload)
            prompt = f'''Animate {subject} from the single reference image as a seamless repeating running-in-place game animation.\n\n{angle}\n\n{action}\n\nSustain a steady complete repeated motion from the start through the end, with matching stride phase at the loop boundary. Keep the head and torso proportions constant. The mount's original body and rider must stay recognizable in every frame. Do not merely slide or shake a static picture.\n\nAnchor the entire character group at the exact original horizontal center and ground level. Preserve the original character size relative to the square canvas and all empty margins. Use only small natural gait motion; do not enlarge, recenter, shrink or crop the subject. Keep every ear, foot, tail, saddle, rider, hair and accessory within the frame. Fixed camera: no zoom, pan, tracking, orbit, cuts, shake or reframing. Preserve the exact rider identity, costume, face, hairstyle, colors, mount anatomy, equipment and illustrated 2D rendering style.\n\nKeep a perfectly flat uniform solid magenta background, #FF00FF, for every frame. No scenery, floor, cast shadow, text, added characters, new equipment, motion blur, dust, smoke, trails, flashes or new magic effects. Preserve only effects that already belong to the reference. No speech or music.'''
            (job / 'prompt.txt').write_text(prompt, encoding='utf-8')
            jobs.append({'key': key, 'mount': mount, 'view': view, 'source': str(source), 'upload': str(upload), 'work': str(job), 'seed': 1610202600 + len(jobs), 'status': 'prepared'})
    (WORK / 'jobs.json').write_text(json.dumps(jobs, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'jobs':len(jobs),'directory':str(WORK)}, ensure_ascii=False))

if __name__ == '__main__': main()
