using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    public static class RanchTrainingVideoVerification
    {
        private const string Folder = "DataBundles/UI/목장/훈련영상_20261020/작업기록/Unity검증";
        private static readonly StringBuilder report = new StringBuilder();
        public static string Status { get; private set; }
        public static void Run(int first = 0, int count = 3)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter lobby Play mode first.");
            Directory.CreateDirectory(Folder);
            report.Clear();
            Status = "RUNNING";
            Mgr_Game.Instance.StartCoroutine(Verify(first, count));
        }
        private static IEnumerator Verify(int first, int count)
        {
            string previous = JsonUtility.ToJson(Mgr_User.Data);
            var ranch = UnityEngine.Object.FindFirstObjectByType<UIPanel_Ranch>();
            var training = UnityEngine.Object.FindObjectsByType<UIPanel_RanchTraining>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var show = ranch.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "btn_training");
            try
            {
                Mgr_User.Data.Monsters = new[] { new Data_MonsterProgress(), new Data_MonsterProgress(), new Data_MonsterProgress() };
                Mgr_User.Data.SelectedMonster = -1;
                for (int i = first; i < Mathf.Min(first + count, 3); i++)
                {
                    string id = Mgr_Table.Demo.Monsters[i].Id;
                    ranch.CloseCollection();
                    show.onClick.Invoke();
                    training.SelectMonster(i);
                    training.BeginTraining();
                    float deadline = Time.realtimeSinceStartup + 15;
                    while (!training.TrainingVideo.HasStarted && !training.TrainingVideo.HasFailed && Time.realtimeSinceStartup < deadline) yield return null;
                    Check(training.TrainingVideo.HasStarted && !training.TrainingVideo.HasFailed, id + " 실제 영상 시작");
                    Check(training.TrainingVideo.VideoPath == "Video/RanchTraining/" + id + ".mp4", id + " 해당 캐릭터 MP4 선택");
                    yield return new WaitForSecondsRealtime(.4f);
                    Check(training.TrainingProgress.fillAmount > 0 && training.IsTraining, id + " 미디어 시간으로 진행도 표시");
                    training.SelectMonster((i + 1) % 3);
                    Check(training.SelectedId == i && !training.RosterScrollbar.interactable, id + " 재생 중 대상 변경 차단");
                    training.ReturnToRanch();
                    yield return null;
                    Check(!training.IsTraining && !Mgr_User.Data.Monsters[i].Trained, id + " 취소 시 성장 미지급");
                    Check(!training.TrainingVideo.HasStarted, id + " 패널 닫기 시 재생 정리");
                    show.onClick.Invoke();
                    training.SelectMonster(i);
                    training.BeginTraining();
                    deadline = Time.realtimeSinceStartup + 22;
                    bool captured = false;
                    while (training.IsTraining && Time.realtimeSinceStartup < deadline)
                    {
                        if (!captured && training.TrainingVideo.MediaTime > 2.5)
                        {
                            captured = true;
                            yield return new WaitForEndOfFrame();
                            Capture(id + "_훈련재생.png");
                            var image = training.GetComponentsInChildren<UnityEngine.UI.RawImage>().Single(x => x.name == "img_training_video");
                            Check(Mathf.Abs(image.rectTransform.rect.width / image.rectTransform.rect.height - 16f / 9f) < .002f, id + " 영상 비율 유지");
                        }
                        yield return null;
                    }
                    var progress = Mgr_User.Data.Monsters[i];
                    Check(captured && !training.IsTraining && progress.Trained && progress.Level == 5 && progress.SpeedBonus == 30 && progress.EndurancePassive, id + " 영상 완료 후 성장 지급");
                    int experience = progress.Experience;
                    training.BeginTraining();
                    Check(!training.IsTraining && progress.Experience == experience && !training.TrainButton.interactable, id + " 중복 성장 차단");
                    Check(Mgr_User.Data.SelectedMonster == -1, id + " 출전 선택 독립");
                    yield return new WaitForEndOfFrame();
                    Capture(id + "_훈련완료.png");
                }
                // A missing network asset must not award growth; the same button must recover.
                Mgr_User.Data.Monsters[first] = new Data_MonsterProgress();
                training.SelectMonster(first);
                training.BeginTraining();
                var decoder = training.GetComponent<UnityEngine.Video.VideoPlayer>();
                decoder.Stop();
                decoder.url = Application.streamingAssetsPath + "/Video/RanchTraining/VerificationMissing.mp4";
                decoder.Prepare();
                decoder.Play();
                float failureDeadline = Time.realtimeSinceStartup + 15;
                while (training.IsTraining && Time.realtimeSinceStartup < failureDeadline) yield return null;
                Check(!training.IsTraining && !Mgr_User.Data.Monsters[first].Trained && training.TrainButton.interactable
                    && training.TrainingStatus.text.Contains("다시"), "영상 로드 실패 시 성장 미지급·재시도 버튼 복구");
                training.BeginTraining();
                failureDeadline = Time.realtimeSinceStartup + 22;
                while (training.IsTraining && Time.realtimeSinceStartup < failureDeadline) yield return null;
                Check(!training.IsTraining && Mgr_User.Data.Monsters[first].Trained, "영상 오류 후 실제 영상 재시도·완료 성장");
                Status = "PASSED";
            }
            finally
            {
                ranch.CloseCollection();
                JsonUtility.FromJsonOverwrite(previous, Mgr_User.Data);
                Mgr_User.Save();
                if (Status == "RUNNING") Status = "FAILED";
                report.AppendLine("결과: " + Status);
                File.WriteAllText(Folder + "/훈련영상_동작검증.txt", report.ToString());
            }
        }
        private static void Check(bool success, string message)
        {
            report.AppendLine((success ? "PASS " : "FAIL ") + message);
            if (!success) throw new InvalidOperationException(message);
        }
        private static void Capture(string name)
        {
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Folder + "/" + name, image.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(image); }
        }
    }
}
