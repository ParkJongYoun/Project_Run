Project MR - 데모 웹 빌드 / 로컬 실행

1. Unity 6000.3.23f1에 Web Build Support를 설치하고 C:/Project_MR/Run을 연다.
2. Unity File > Build Profiles에서 Web 플랫폼으로 전환한다.
3. Tools > Project Run > Build Web - Game을 실행한다.
   빠른 무압축 빌드는 Build Web - Game (Fast)를 사용한다.
   정식 씬: Scene_Title → Scene_LobbyTest → Scene_OvalToon.
4. 생성된 Run/DataBundles/빌드/심사데모_WebGL/Play.cmd를 실행한다.
   배포 시 Play.cmd, Game, Launcher가 들어 있는 폴더 전체를 전달한다.
   Game/index.html만 복사하거나 직접 더블클릭하지 않는다.
5. 기본 브라우저에서 게임이 열린다. 게임 중 검은 명령창을 유지하며,
   종료할 때 게임 탭과 명령창을 닫는다.

실행기 원본:
- Run/Tools/Demo/Play.cmd
- Run/Tools/Demo/StartDemo.ps1
- Tools/LocalWeb/Serve.ps1

WebLauncherPostprocessor가 빌드 결과를 Game으로 정리하고,
PowerShell 스크립트 두 개를 Launcher에 배치한다.
StartDemo.ps1은 Game을 서버 루트로 사용하고 18767~18776 중 사용 가능한 포트를 선택한다.
서버는 127.0.0.1에서만 접속되며 Python이나 Node 설치가 필요 없다.
정식 메뉴는 Gzip 압축, Fast 메뉴는 무압축이며 Serve.ps1은 두 형식을 지원한다.

2026-10-22 업로드 전 정리에서 생성된 빌드 결과물을 모두 삭제했다.
빌드 결과물은 .gitignore로 제외되므로 필요한 PC에서 다시 빌드한다.
이번 정리에서는 컴파일과 씬 참조만 확인했으며 게임 Play와 재빌드는 수행하지 않았다.
