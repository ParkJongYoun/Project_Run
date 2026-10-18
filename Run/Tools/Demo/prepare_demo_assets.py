"""Map approved source illustrations to English Unity resource names, without editing pixels."""
from pathlib import Path
import json, shutil

root = Path(__file__).resolve().parents[2]
art = root / 'DataBundles/아트'
records = []
def add(key, folder, filename):
    source = art / folder / filename
    if not source.exists(): raise FileNotFoundError(source)
    target = root / 'Assets/ArtBundles/UI/Demo' / (key + '.png')
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
    records.append({'Key': key, 'Path': target.relative_to(root).as_posix(), 'Source': source.relative_to(root).as_posix()})

for key, filename in {'title':'심사데모_타이틀/프론테라_아침풍경.png','nav_lobby':'로비/로비.png','nav_monsters':'로비/몬스터.png','nav_training':'로비/훈련.png','nav_arena':'로비/경기장.png','nav_background':'로비/배경변경.png','nav_settings':'로비/설정.png','nav_objective':'로비/목표안내.png'}.items():
    folder,name=filename.rsplit('/',1);add(key,folder,name)
for key,name in {'dialog_bg':'06_대화창_배경','dialog_frame':'06_대화창_프레임','kafra_guide':'카프라_왼손안내','kafra_success':'카프라_완료축하'}.items():add(key,'듀토리얼',name+'.png')
for key,name in {'panel_bg':'09_전체화면_백판','panel_frame':'09_전체화면_프레임','card_bg':'09_카드_배경','card_frame':'09_카드_프레임_기본','card_selected':'09_카드_프레임_선택','button_bg':'09_선택버튼_배경','button_frame':'09_선택버튼_프레임','portrait_bg':'14_전신캐릭터_배경','portrait_frame':'14_전신캐릭터_프레임','gauge_bg':'14_게이지_배경','gauge_frame':'14_게이지_프레임'}.items():add(key,'몬스터와훈련',name+'.png')
for i,stat in enumerate(['스피드','체력','근력','지력','정신력']):add('gauge_'+str(i),'몬스터와훈련','14_게이지_채움_'+stat+'.png')
for key,korean in [('Poring','포링'),('Minorous','마이너우로스'),('Eddga','에드가')]:
    add(key+'_face','몬스터와훈련','09_페이스_'+korean+'.png')
    add(key+'_body','몬스터와훈련','14_전신_'+korean+'.png')
    add(key+'_run','훈련용','15_훈련_'+korean+'_01_'+('탄성점프' if key=='Poring' else '달리기')+'.png')
    add(key+'_jump','훈련용','15_훈련_'+korean+'_02_장애물점프.png')
for key,name in {'arena':'32_카툰원형경기장_전경','notebook':'36_모험가수첩_아이콘','book_bg':'37_책_종이배경','book_frame':'37_책_외곽테두리','book_binding':'37_책_중앙제본','memo_bg':'37_관찰메모_배경','memo_frame':'37_관찰메모_프레임','photo_frame':'37_전경사진_프레임','book_course':'37_아이콘_코스특성','book_stats':'37_아이콘_추천능력','book_skill':'37_아이콘_추천스킬','book_heading_bg':'37_항목제목_배경','book_heading_frame':'37_항목제목_프레임'}.items():add(key,'경기장',name+'.png')
add('training_bg','훈련용','15_훈련장_배경.png');add('training_frame','훈련용','15_훈련연출창_사각프레임.png')
add('mode_2d','심사데모_경기미리보기','초원경기장.png')
add('mode_3d','심사데모_경기미리보기','카툰경기장.png')
output=root/'DataBundles/기획/심사데모_제작자료/아트연결목록.json'
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps({'Images':records},ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Prepared {len(records)} artwork references')
