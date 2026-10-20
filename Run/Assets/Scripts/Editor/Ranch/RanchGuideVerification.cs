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
        public const string Folder = "DataBundles/UI/목장/신규안내인_튜토리얼_20261020/검증";
        public static string Status { get; private set; }
        private static readonly List<string> notes = new List<string>();
        private static Sys_RanchUI Lobby => UnityEngine.Object.FindFirstObjectByType<Sys_RanchUI>();
        public static void Run()
        {
            if (!Application.isPlaying || SceneManager.GetActiveScene().name != Define_Scene.Title)
                throw new InvalidOperationException("Run from title Play mode.");
            notes.Clear(); Directory.CreateDirectory(Folder); Status = "RUNNING";
            Mgr_Game.Instance.StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
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
                Dismiss(); yield return null;
                Check(!Lobby.Panel.Placement.InputBlocked && Lobby.GuidePanel.Highlight.gameObject.activeSelf, "대화 종료 후 조작 해제·몬스터 강조");
                Click(Home("btn_monsters")); yield return null;
                Check(Lobby.GuidePanel.CurrentKey == "ranch_monsters", "몬스터 첫 방문 안내"); Dismiss();
                Lobby.MonsterPanel.SelectMonster(1);
                Check(Lobby.MonsterPanel.Character.sprite == Resources.Load<Data_CartoonRaceRoster>("Tables/Data_CartoonRaceRoster").Playable[1].Portrait, "신규 미노 정보 이미지 연결");
                Check(Mgr_User.Data.SelectedMonster == -1, "정보 조회가 출전 선택을 바꾸지 않음");
                yield return new WaitForEndOfFrame(); Capture("04_신규미노_몬스터정보.png");
                Click(Lobby.MonsterPanel.BackButton); yield return null; yield return null;
                Check(Lobby.GuidePanel.CurrentKey == "ranch_open_training", "목장 복귀 후 훈련 안내"); Dismiss();
                Click(Home("btn_training")); yield return null;
                Check(Lobby.GuidePanel.CurrentKey == "ranch_training", "훈련 첫 방문 안내"); Dismiss();
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
                Check(Lobby.GuidePanel.CurrentKey == "ranch_training_complete", "실제 완료 때 성장 축하 안내");
                yield return new WaitForSecondsRealtime(2.5f); yield return new WaitForEndOfFrame(); Capture("05_훈련완료_대화.png");
                Dismiss(); Click(Lobby.TrainingPanel.BackButton); yield return null; yield return null;
                Check(Lobby.GuidePanel.CurrentKey == "ranch_ready", "출전 준비 안내"); Dismiss();
                Click(Home("btn_arena")); yield return null;
                Check(Lobby.GuidePanel.CurrentKey == "ranch_arena", "경기장 첫 방문 안내"); Dismiss();
                Check(Lobby.GuidePanel.Highlight.gameObject.activeSelf, "경기장 수첩 강조");
                Click(Lobby.ArenaPanel.NotebookButton); yield return null;
                Check(Lobby.ArenaPanel.NotebookPopup.activeSelf, "강조 위에서도 수첩 실제 클릭 가능");
                Click(Lobby.ArenaPanel.NotebookClose); Click(Lobby.ArenaPanel.EntryButton); yield return null;
                Check(Lobby.GuidePanel.CurrentKey == "ranch_entry", "출전 선택 안내"); Dismiss();
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
                Click(Lobby.GuidePanel.SkipButton); yield return null;
                Check(!Mgr_Guide.Enabled && !Lobby.GuidePanel.IsDialogueOpen && !Lobby.Panel.Placement.InputBlocked, "전부 스킵 시 안내·입력 차단 해제");
                Click(Home("btn_monsters")); yield return null;
                Check(!Lobby.GuidePanel.IsDialogueOpen, "스킵 뒤 메뉴 안내 억제");
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
        private static void Dismiss()
        {
            for (int i = 0; i < 8 && Lobby.GuidePanel.IsDialogueOpen; i++) Click(Lobby.GuidePanel.AdvanceButton);
            Check(!Lobby.GuidePanel.IsDialogueOpen, "대화 진행 버튼 클릭 완료");
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
