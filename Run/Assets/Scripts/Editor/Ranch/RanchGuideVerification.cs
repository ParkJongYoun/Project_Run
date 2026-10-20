using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectRun.Race;
using ProjectRun.Ranch;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectRun.Editor
{
    // Exercises real button raycasts, video completion and the title/lobby/race round trip.
    public static class RanchGuideVerification
    {
        public const string Folder = "DataBundles/UI/목장/메뉴설명_간단튜토리얼_20261020/검증";
        public static string Status { get; private set; }
        private static readonly List<string> notes = new List<string>();
        private static Sys_RanchUI Lobby => UnityEngine.Object.FindFirstObjectByType<Sys_RanchUI>();
        public static void Run(bool overviewOnly = false)
        {
            if (!Application.isPlaying || SceneManager.GetActiveScene().name != Define_Scene.Title)
                throw new InvalidOperationException("Run from title Play mode.");
            notes.Clear(); Directory.CreateDirectory(Folder); Status = "RUNNING";
            UnityEditor.SessionState.SetString("RanchGuideVerification.UserSave", JsonUtility.ToJson(Mgr_User.Data));
            Mgr_Game.Instance.StartCoroutine(Verify(overviewOnly));
        }
        private static IEnumerator Verify(bool overviewOnly)
        {
            string saved = JsonUtility.ToJson(Mgr_User.Data);
            float volume = Mgr_User.Data.Volume;
            try
            {
                Click(GameObject.Find("btn_game_start").GetComponent<Button>());
                yield return WaitScene(Define_Scene.Lobby); yield return null;
                Check(Lobby.IsPresentationReady && Lobby.GuidePanel.CurrentKey == "ranch_welcome", "타이틀→목장 준비 완료 후 첫 인사");
                Check(Lobby.Panel.Placement.InputBlocked, "대화 중 목장 조작 차단");
                Check(Lobby.GuidePanel.CharacterSprite.texture.width >= 1024, "안내 캐릭터 고해상도 스프라이트 연결");
                Check(Mgr_User.Data.SelectedMonster == -1 && Mgr_User.Data.Monsters.All(m => !m.Trained), "새 체험 초기화");
                yield return new WaitForSecondsRealtime(2.5f); yield return new WaitForEndOfFrame(); Capture("03_타이틀진입_첫인사.png");
                yield return VerifyMenuTour();
                if (overviewOnly)
                {
                    yield return VerifyMenuVisits();
                    Mgr_Game.Instance.StartDemo(); yield return WaitScene(Define_Scene.Lobby); yield return null;
                    Check(Lobby.GuidePanel.CurrentKey == "ranch_welcome", "새 체험 첫 인사 재시작");
                    yield return Dismiss(); yield return null;
                    Check(!Lobby.Panel.Placement.InputBlocked, "새 체험 설명 완료 후 입력 해제");
                    Status = "PASSED"; yield break;
                }
                Click(Home("btn_monsters")); yield return null;
                Check(!Lobby.GuidePanel.IsDialogueOpen, "몬스터 첫 방문 추가 안내 없음");
                Lobby.MonsterPanel.SelectMonster(1);
                Check(Lobby.MonsterPanel.Character.sprite == Resources.Load<Data_CartoonRaceRoster>("Tables/Data_CartoonRaceRoster").Playable[1].Portrait, "신규 미노 정보 이미지 연결");
                Check(Mgr_User.Data.SelectedMonster == -1, "정보 조회가 출전 선택을 바꾸지 않음");
                yield return new WaitForEndOfFrame(); Capture("04_신규미노_몬스터정보.png");
                Click(Lobby.MonsterPanel.BackButton); yield return null; yield return null;
                Check(!Lobby.GuidePanel.IsDialogueOpen, "목장 복귀 후 추가 안내 없음");
                Click(Home("btn_training")); yield return null;
                Check(!Lobby.GuidePanel.IsDialogueOpen, "훈련 첫 방문 추가 안내 없음");
                Lobby.TrainingPanel.SelectMonster(1);
                Check(Mgr_User.Data.SelectedMonster == -1, "훈련 선택이 출전 선택을 바꾸지 않음");
                Click(Lobby.TrainingPanel.TrainButton); yield return new WaitForSecondsRealtime(1);
                Click(Lobby.TrainingPanel.BackButton); yield return null;
                Check(!Mgr_User.Data.Monsters[1].Trained, "훈련 중 취소는 성장 미지급");
                Click(Home("btn_training")); yield return null;
                Check(!Lobby.GuidePanel.IsDialogueOpen, "훈련 재방문에 안내 반복 없음");
                Click(Lobby.TrainingPanel.TrainButton);
                float deadline = Time.realtimeSinceStartup + 30;
                while (Lobby.TrainingPanel.IsTraining && Time.realtimeSinceStartup < deadline) yield return null;
                Check(Mgr_User.Data.Monsters[1].Trained && Mgr_User.CanEnter(1), "실제 훈련 영상 완료 후 성장 지급");
                Check(!Lobby.GuidePanel.IsDialogueOpen, "훈련 완료 추가 안내 없음");
                yield return new WaitForEndOfFrame(); Capture("05_훈련완료.png");
                yield return Dismiss(); Click(Lobby.TrainingPanel.BackButton); yield return null; yield return null;
                Check(!Lobby.GuidePanel.IsDialogueOpen, "출전 준비 추가 안내 없음");
                Click(Home("btn_arena")); yield return null;
                Check(!Lobby.GuidePanel.IsDialogueOpen, "경기장 첫 방문 추가 안내 없음");
                Click(Lobby.ArenaPanel.NotebookButton); yield return null;
                Check(Lobby.ArenaPanel.NotebookPopup.activeSelf, "수첩 실제 클릭 가능");
                Click(Lobby.ArenaPanel.NotebookClose); Click(Lobby.ArenaPanel.EntryButton); yield return null;
                Check(!Lobby.GuidePanel.IsDialogueOpen, "출전 선택 추가 안내 없음");
                Click(Lobby.ArenaPanel.EntrantButtons[1]);
                Check(Mgr_User.Data.SelectedMonster == -1, "출전 확정 전 저장 선택 유지");
                yield return new WaitForEndOfFrame(); Capture("06_신규미노_출전준비.png");
                Click(Lobby.ArenaPanel.ConfirmEntry); yield return WaitScene(Define_Scene.OvalToon);
                var race = UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
                var view = UnityEngine.Object.FindObjectsByType<RaceRunnerView>(FindObjectsSortMode.None).Single(v => v.RunnerId == Mgr_Game.Instance.HeroRunner);
                Check(race.Entrants.Length == 6 && race.Entrants.Select(e => e.Name).Distinct().Count() == 6, "경기 6종 각 한 마리 유지");
                Check(view.RunClip.name == "NewMinotaurus_Run" && view.IdleClip.name == "NewMinotaurus_Idle", "선택 미노 신규 모델·대기·달리기 연결");
                Check(view.MountAnimator.avatar.isValid && view.MonsterFaceTarget != null && view.HasPose, "신규 미노 리그·카메라 기준점·실시간 포즈 정상");
                Check(Mgr_User.Data.SelectedMonster == 1, "선택 미노 출전·카메라 대상 연결 유지");
                yield return new WaitForSecondsRealtime(12);
                Check(view.CurrentAnimationRate > 0, "실제 경기 달리기 애니메이션 재생");
                yield return new WaitForEndOfFrame(); Capture("07_신규미노_레이스.png");
                var racePanel = UnityEngine.Object.FindFirstObjectByType<UIPanel_DemoRace>();
                Click(GameObject.Find("btn_lobby").GetComponent<Button>()); yield return WaitScene(Define_Scene.Lobby); yield return null;
                Check(Mgr_User.Data.SelectedMonster == 1 && Mgr_User.Data.Monsters[1].Trained, "경기 후 목장 복귀 시 성장·선택 보존");
                Check(!Lobby.GuidePanel.IsDialogueOpen, "일반 재진입에 첫 인사 반복 없음");
                Check(UnityEngine.Object.FindObjectsByType<Sys_RanchResident>(FindObjectsSortMode.None).Length == 6, "재진입 목장 6종 유지");
                Click(Home("btn_arena")); yield return null;
                Click(Lobby.ArenaPanel.EntryButton); yield return null;
                Click(Lobby.ArenaPanel.ConfirmEntry); yield return WaitScene(Define_Scene.OvalToon);
                race = UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
                racePanel = UnityEngine.Object.FindFirstObjectByType<UIPanel_DemoRace>();
                racePanel.CutscenesEnabled = false; race.SkipToResult(); yield return null; yield return null;
                Check(race.Simulation.Runners[Mgr_Game.Instance.HeroRunner].SkillActivationCount == 1, "선택 미노 필살기 1회 유지");
                deadline = Time.realtimeSinceStartup + 15;
                while (GameObject.Find("btn_new_session") == null && Time.realtimeSinceStartup < deadline) yield return null;
                Click(GameObject.Find("btn_new_session").GetComponent<Button>()); yield return WaitScene(Define_Scene.Lobby); yield return null;
                Check(Lobby.GuidePanel.CurrentKey == "ranch_welcome", "새 체험에서 첫 인사 재시작");
                yield return Dismiss(); yield return null;
                Check(!Lobby.GuidePanel.IsDialogueOpen && !Lobby.Panel.Placement.InputBlocked, "설명 완료 시 안내·입력 차단 해제");
                Click(Home("btn_monsters")); yield return null;
                Check(!Lobby.GuidePanel.IsDialogueOpen, "설명 완료 뒤 메뉴 안내 없음");
                Click(Lobby.MonsterPanel.BackButton); yield return null;
                Check(Mgr_User.Data.Volume == volume, "소리 설정 보존");
                Status = "PASSED";
            }
            finally
            {
                JsonUtility.FromJsonOverwrite(saved, Mgr_User.Data); Mgr_User.Save();
                if (Status == "RUNNING") Status = "FAILED";
                notes.Add("결과: " + Status); File.WriteAllLines(Folder + "/통합흐름_검증.txt", notes);
            }
        }
        private static Button Home(string name) => Lobby.Panel.Navigation.Find(name).GetComponent<Button>();
        private static IEnumerator Dismiss()
        {
            for (int i = 0; i < 8 && Lobby.GuidePanel.IsDialogueOpen; i++)
            {
                Click(Lobby.GuidePanel.IsTourOpen ? Lobby.GuidePanel.NextButton : Lobby.GuidePanel.AdvanceButton);
                yield return null;
            }
            Check(!Lobby.GuidePanel.IsDialogueOpen, "대화 진행 버튼 클릭 완료");
        }
        private static IEnumerator VerifyMenuTour()
        {
            var guide = Lobby.GuidePanel;
            Check(!guide.GetComponentsInChildren<Transform>(true).Any(t => t.name == "btn_skip_all"), "전체 스킵 버튼 제거");
            string before = JsonUtility.ToJson(Mgr_User.Data);
            Click(guide.AdvanceButton); yield return null;
            string[] targets = { "btn_monsters", "btn_build", "btn_training", "btn_arena" };
            string[] captures = { "01_몬스터_설명.png", "02_시설_설명.png", "03_훈련_설명.png", "04_경기장_설명.png" };
            for (int i = 0; i < targets.Length; i++)
            {
                yield return new WaitForEndOfFrame(); Capture(captures[i]);
                Check(guide.IsTourOpen && guide.CurrentTarget.name == targets[i], "설명 순서: " + targets[i]);
                Check(!guide.Character.gameObject.activeInHierarchy, "작은 설명창에서는 대형 안내인 숨김");
                Check(!Lobby.MonsterPanel.gameObject.activeInHierarchy && !Lobby.TrainingPanel.gameObject.activeInHierarchy
                    && !Lobby.ArenaPanel.gameObject.activeInHierarchy && !Lobby.Panel.Placement.IsBuilding, "실제 기능 실행 없이 설명");
                Check(Lobby.Panel.Placement.InputBlocked, "설명 중 월드 입력 차단");
                var targetHit = TopHit(guide.CurrentTarget);
                Check(targetHit.transform.IsChildOf(guide.Tour), "밝은 메뉴 영역에서도 입력 통과 방지 (" + targetHit.name + ")");
                Check(guide.TooltipMessage.preferredHeight <= guide.TooltipMessage.rectTransform.rect.height, "설명 글자 잘림 없음");
                Check(EventSystem.current.currentSelectedGameObject == guide.NextButton.gameObject, "키보드 진행 대상은 설명창");
                Check(guide.PaperSprite.texture != guide.FrameSprite.texture, "백판·프레임 원본 분리");
                var cornerPoints = new Vector3[4]; guide.Tooltip.GetWorldCorners(cornerPoints);
                Check(cornerPoints.All(p => p.x >= 0 && p.x <= Screen.width && p.y >= 0 && p.y <= Screen.height), "설명창이 화면 안에 배치");
                Click(guide.NextButton); yield return null;
            }
            Check(!guide.IsDialogueOpen && !Lobby.Panel.Placement.InputBlocked, "네 설명 완료 후 딤드·입력 차단 해제");
            Check(JsonUtility.ToJson(Mgr_User.Data) == before, "튜토리얼에서 선택·훈련·경기 데이터 변경 없음");
            Check(TopHit(Home("btn_monsters").transform as RectTransform).GetComponentInParent<Button>() == Home("btn_monsters"), "완료 후 실제 메뉴 입력 복구");
        }
        private static IEnumerator VerifyMenuVisits()
        {
            Click(Home("btn_monsters")); yield return null;
            Check(!Lobby.GuidePanel.IsDialogueOpen, "몬스터 방문에 추가 안내 없음");
            Click(Lobby.MonsterPanel.BackButton); yield return null; yield return null;
            Click(Home("btn_build")); yield return null;
            Check(Lobby.Panel.Placement.IsBuilding && !Lobby.GuidePanel.IsDialogueOpen, "시설 메뉴 자유 진입");
            Click(Lobby.Panel.GetComponentsInChildren<Button>(true).Single(b => b.name == "btn_close_build")); yield return null;
            Click(Home("btn_training")); yield return null;
            Check(!Lobby.GuidePanel.IsDialogueOpen, "훈련 방문에 추가 안내 없음");
            Click(Lobby.TrainingPanel.BackButton); yield return null; yield return null;
            Click(Home("btn_arena")); yield return null;
            Check(!Lobby.GuidePanel.IsDialogueOpen, "경기장 방문에 추가 안내 없음");
            Click(Lobby.ArenaPanel.NotebookButton); yield return null;
            Check(!Lobby.GuidePanel.IsDialogueOpen && Lobby.ArenaPanel.NotebookPopup.activeSelf, "수첩 안내·클릭 유도 없음");
            Click(Lobby.ArenaPanel.NotebookClose);
            Click(Lobby.ArenaPanel.EntryButton); yield return null;
            Check(!Lobby.GuidePanel.IsDialogueOpen, "출전 준비에 추가 안내 없음");
            Click(Lobby.ArenaPanel.EntryClose); Click(Lobby.ArenaPanel.BackButton); yield return null; yield return null;
            Check(!Lobby.GuidePanel.IsDialogueOpen, "같은 체험의 로비 재진입에 반복 안내 없음");
        }
        private static GameObject TopHit(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0, "레이캐스트 결과 존재: " + rect.name);
            return hits[0].gameObject;
        }
        private static void Click(Button button)
        {
            Check(button != null && button.isActiveAndEnabled && button.interactable, "버튼 활성: " + (button == null ? "missing" : button.name));
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
                "레이캐스트 통과: " + button.name + (hits.Count > 0 ? " (" + hits[0].gameObject.name + ")" : " (no hit)"));
            button.onClick.Invoke();
        }
        private static IEnumerator WaitScene(string name)
        {
            float deadline = Time.realtimeSinceStartup + 45; yield return null;
            while ((SceneManager.GetActiveScene().name != name || Mgr_Game.Instance.IsTransitioning) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(SceneManager.GetActiveScene().name == name && !Mgr_Game.Instance.IsTransitioning, "전환 완료: " + name);
            // Transition clears its flag when scheduling the black canvas for destruction.
            // Finish that rendered frame before testing pointer input in the new scene.
            yield return new WaitForEndOfFrame(); yield return null;
        }
        private static void Check(bool condition, string text)
        {
            notes.Add((condition ? "PASS " : "FAIL ") + text);
            File.WriteAllLines(Folder + "/통합흐름_검증.txt", notes);
            if (!condition) throw new InvalidOperationException(text);
        }
        private static void Capture(string name)
        {
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Folder + "/" + name, texture.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(texture); }
        }
    }
}
