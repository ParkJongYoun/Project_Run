import hashlib, json
from datetime import datetime, timezone
from pathlib import Path

work = Path(__file__).resolve().parent
root = work.parents[1]
project = root.parents[2]
game = project/'Assets/StreamingAssets/Video/FoxUltimate1024.mp4'
validation = work/'unity_validation_final'
assert (validation/'completion.txt').read_text().strip() == 'PASS'
record = {
    'recorded_at_utc': datetime.now(timezone.utc).isoformat(),
    'scene': 'Assets/Scenes/Scene_All3D.unity',
    'video': str(game),
    'bytes': game.stat().st_size,
    'sha256': hashlib.sha256(game.read_bytes()).hexdigest(),
    'compressed_video_packet_sha256': '541f4331a90807ab1e0c3bebaefaea751ebcfdd46071b2535b33b45fee787e5b',
    'original_and_game_video_packet_hashes_match': True,
    'game_copy_processing': 'Video stream copied without re-encoding; audio omitted; MP4 faststart enabled.',
    'runner_id': 2, 'skill_race_seconds': 40,
    'fade_in_seconds': .4, 'fade_out_seconds': .85, 'skip_fade_seconds': .25,
    'validation': 'Unity Editor 6000.3.23f1, 720x1280, actual VideoPlayer playback',
    'validation_report': (validation/'report.txt').read_text(encoding='utf-8'),
    'web_build_tested': False
}
(work/'integration.json').write_text(json.dumps(record, ensure_ascii=False, indent=2),encoding='utf-8')
note = work/'제작기록.txt'
text = note.read_text(encoding='utf-8')
text += '''

영상 외형 확인
0~1.8초: 현재 여우와 탑승자를 유지한 채 달빛 경기장·등불·청색 여우불이 등장한다.
약 2~5초: 도약 및 공중 회전, 성·달·청금색 폭죽. 공중에서 잠시 머무르는 슬로모션에 가까운 회전이다.
약 5.3~7.3초: 착지 후 낮은 자세, 발밑 마법진과 압축되는 여우불.
약 7.5초 이후: 발진과 지속 질주, 양옆 폭죽과 속도선. 끝까지 달리는 동작을 유지한다.
0.5초 간격 25개 프레임과 Unity 실제 재생을 확인했다. 확인 이미지: review_contact.jpg, unity_validation_final/.
프롬프트상의 시간과 실제 장면 전환은 완전히 같지는 않다. 배경과 이펙트는 생성된 영상에 포함되어 있으며 경기장 3D 모델을 수정한 것은 아니다.

Unity 적용 완료
Scene_All3D의 [Systems]/Sys_RaceSkillVideo에서 여우(RunnerId 2)의 경기 시간 40초 스킬 이벤트에 연결했다.
패널: Assets/Resources/Prefabs/UI/Panel/UIPanel_RaceSkillVideo.prefab.
영상: Assets/StreamingAssets/Video/FoxUltimate1024.mp4.
원본 영상과 게임용 파일의 압축된 비디오 스트림 SHA-256이 같다. 재압축 없이 오디오 트랙 제거와 faststart만 적용했다.
게임용 영상은 무음이며 기존 게임 효과음 처리를 유지한다. 원본 납품 MP4의 생성 오디오는 보존했다.
페이드인 0.4초 / 페이드아웃 0.85초 / 연출 생략 0.25초.
최초 영상 프레임 이후 표시하며, 종료 전 실제 달리기를 재개한다. 애니메이션 시계를 초기화하지 않는다.
여우 복귀 구도에서 일반 중계 카메라로 약 0.9초 동안 보간한다.
컴파일 오류 없음. Unity Editor 실제 720×1280 화면에서 아래 항목 통과.
최초 실행에서는 프리팹의 메뉴 Canvas overrideSorting이 초기화되는 문제를 발견했다. 메뉴 활성화 직후 Popup 순서를 적용하도록 수정한 뒤 재검증했다. 첫 실행 로그는 unity_validation_first/에 보존했다.
'''
text += record['validation_report']
text += '\n브라우저 WebGL 빌드 및 실제 모바일 기기 재생은 이번 작업에서 실행하지 않았다.\n'
text += '\n생성 서버 로그의 실행시간은 01:09:05 형식의 초 단위 표시를 4,145초로 환산했다.\n'
note.write_text(text,encoding='utf-8')
friendly = root/'제작기록_천공여우불_1024_20261016.txt'
friendly.write_text('상세 작업 폴더: '+str(work)+'\n\n'+text,encoding='utf-8')
print(json.dumps({'game_copy':str(game),'record':str(friendly),'validation':'PASS'},ensure_ascii=True))
