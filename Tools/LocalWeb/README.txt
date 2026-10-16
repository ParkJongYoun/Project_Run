Project Run - Web 빌드 / 로컬 실행

1. Unity Hub > Installs > 6000.3.23f1 > Add modules > Web Build Support 설치.
   해당 Unity 버전에 Web Build Support가 설치되어 있어야 한다.
2. Unity File > Build Profiles > Web > Switch Platform.
3. Tools > Project Run > Build Web - Game 실행.
   Scene_Title, Scene_All3D, Scene_MonsterRenderCompare를 포함한다.
   타이틀에서 몬스터 레이스와 렌더링 비교 화면으로 진입한다.
4. Run/Builds/WebGL_PlayModes/Play.cmd를 더블클릭.
   이 폴더 전체를 복사하면 Windows PowerShell이 있는 다른 Windows PC에서도 실행 가능하다.
   index.html만 복사하거나 더블클릭하는 방식은 지원하지 않는다.
5. 기본 브라우저에서 http://localhost:18765/가 열린다.
   검은 콘솔 창을 유지하고, 종료할 때 창을 닫는다.

외부 서버, 계정 서버, Python, Node 설치는 필요 없다.
대신 내 PC에서만 접속되는 작은 HTTP 서버가 실행된다.
서버는 127.0.0.1에만 바인딩하며 다른 기기에서 접속할 수 없다.
빌드 메뉴는 압축을 Disabled로 설정한다. 이 실행기는 압축 없는 Web 빌드용이다.
포트가 사용 중이면 PowerShell에서 다음과 같이 다른 포트로 실행한다:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Serve.ps1 -Port 18767
브라우저 저장소는 주소/포트별로 구분되므로 이어하기 검사에는 같은 포트를 유지한다.

초기 메뉴 작업 당시 검증 기록 (현재 씬 구성과 구분):
- Unity Editor Play 모드에서 세 타이틀 버튼의 onClick을 각각 호출해 실제 씬 로드 확인.
- 세 씬 모두 Sys_Race가 Running 상태가 되고 주자 거리가 증가함을 확인.
- 각 씬의 타이틀 버튼으로 복귀 확인. 빠른 연속 클릭으로 중복 로드하지 않음.
- 타이틀 및 경기 복귀 버튼 표시 확인. 씬 진입 후 EventSystem 1개 확인.
- 로컬 서버의 HTTP 200, WASM MIME, HTTP 206 바이트 범위 응답 확인.

미완료:
- 초기 메뉴 검증 당시에는 Web 모듈 부재로 Web 빌드를 수행하지 못했다. 이후 2026-10-16 17:25 KST WebGL 빌드 성공은 Run/DataBundles/검증/몬스터렌더링_20261016/three_monster_validation.txt에 기록되어 있다. 브라우저 동작·모바일 성능 검증과는 구분한다.
- Editor 버튼 이벤트 검증은 브라우저의 실제 마우스/터치 입력 검증과 구분함.

공식 참고:
https://docs.unity.com/ko-kr/engine/6000.3/manual/platform-specific/webgl/intro/gettingstarted
