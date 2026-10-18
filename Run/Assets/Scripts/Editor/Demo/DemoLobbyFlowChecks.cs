using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectRun.Race.Editor
{
    // Exercises the independent menu roles through the same buttons and raycast layers as Play.
    public static class DemoLobbyFlowChecks
    {
        private const string Folder = "DataBundles/검증/로비_안내와경기장_보정_20261018";
        private static readonly List<string> notes = new List<string>();
        private static IEnumerator<float> sequence;
        private static double next;
        private static double deadline;
        private static UIPanel_DemoLobby Lobby => UnityEngine.Object.FindFirstObjectByType<UIPanel_DemoLobby>();

        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play first.");
            Directory.CreateDirectory(Folder);
            notes.Clear(); notes.Add("RUNNING");
            sequence = Steps(); next = 0; deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Save();
        }
        private static void Save() => File.WriteAllLines(Folder + "/메뉴흐름_검증.txt", notes);
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException("Play stopped or menu checks timed out.");
                if (!sequence.MoveNext())
                {
                    EditorApplication.update -= Tick; notes.Add("COMPLETED " + DateTime.Now.ToString("O")); Save(); return;
                }
                next = EditorApplication.timeSinceStartup + sequence.Current;
            }
            catch (Exception exception)
            {
                EditorApplication.update -= Tick; notes.Add("FAILED " + exception); Save(); Debug.LogException(exception);
            }
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            notes.Add("PASS " + message); Save();
        }
        private static void Click(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) throw new InvalidOperationException("Missing " + name);
            var button = go.GetComponent<Button>();
            if (button == null || !button.interactable) throw new InvalidOperationException("Disabled " + name);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)go.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<Button>() != button)
                throw new InvalidOperationException("Raycast blocked " + name + " by " + (hits.Count == 0 ? "nothing" : hits[0].gameObject.name));
            button.onClick.Invoke();
        }
        private static IEnumerable<float> OpenMenu(string name)
        {
            if(GameObject.Find(name)==null)
            {
                for(int guard=0;guard<8&&Lobby.CurrentPage!="home";guard++)
                {
                    if(HasGuide){Lobby.DismissGuide();yield return .2f;}
                    if(GameObject.Find("btn_page_back")!=null)Click("btn_page_back");
                    else if(GameObject.Find("btn_skill_back")!=null)Click("btn_skill_back");
                    else if(GameObject.Find("btn_result_confirm")!=null)Click("btn_result_confirm");
                    else Click("btn_lobby");
                    yield return .2f;
                }
                if(HasGuide){Lobby.DismissGuide();yield return .2f;}
            }
            Click(name);yield return .2f;
        }
        private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "/" + name + ".png"));
        private static bool HasGuide => GameObject.Find("btn_dialogue") != null;
        private static void CheckCover(bool expected)
        {
            var cover = Lobby.transform.Find("group_page_background");
            Check(cover.gameObject.activeSelf == expected, "Page cover " + Lobby.CurrentPage);
            if (!expected) return;
            Check(!Lobby.transform.Find("obj_safe_area/group_lobby_navigation").gameObject.activeSelf,"Full page hides bottom lobby navigation");
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)cover;
            var root = (RectTransform)Lobby.transform;
            Check(rect.parent == root && rect.rect.size == root.rect.size, "Cover fills canvas outside Safe Area");
            Check(cover.Find("bg_paper").GetComponent<Image>().sprite != cover.Find("img_frame").GetComponent<Image>().sprite, "Separate backplate and frame assets");
        }
        private static IEnumerator<float> Steps()
        {
            Click("btn_game_start"); yield return 2;
            while (Lobby == null || Mgr_Game.Instance.IsTransitioning) yield return .25f;
            Check(HasGuide && GameObject.Find("btn_skip_all") != null, "Greeting and skip on fresh start");
            Capture("01_첫인사"); yield return .3f; Click("btn_dialogue"); yield return .2f;
            CheckCover(false);
            Check(GameObject.Find("obj_menu_highlight")!=null,"Monster menu click prompt follows greeting");
            Capture("11_몬스터버튼_안내");yield return .3f;
            foreach(float delay in OpenMenu("btn_monsters"))yield return delay;yield return .2f;Click("btn_dialogue");yield return .2f;
            Click("btn_page_back");yield return .2f;
            Check(HasGuide,"Training menu explanation on first return home");
            Click("btn_dialogue");yield return .2f;
            Check(GameObject.Find("obj_menu_highlight")!=null,"Training menu click prompt follows explanation");
            Capture("12_훈련버튼_안내");yield return .3f;
            foreach(float delay in OpenMenu("btn_training"))yield return delay;yield return .2f;Click("btn_dialogue");yield return .2f;
            Click("btn_page_back");yield return .2f;
            Check(!HasGuide&&GameObject.Find("obj_menu_highlight")==null,"Both menu prompts complete once");
            // Arena can be opened first; unmet conditions never trap the player.
            foreach(float delay in OpenMenu("btn_arena"))yield return delay; yield return .2f; Click("btn_dialogue"); yield return .2f;
            Check(GameObject.Find("btn_arena")==null,"Arena navigation hidden on arena page");
            Check(GameObject.Find("obj_menu_highlight")!=null,"First arena visit highlights notebook");
            var preparation=(RectTransform)GameObject.Find("btn_select_arena").transform;
            Check(preparation.anchoredPosition==new Vector2(475,-293),"Preparation replaces arena navigation position");
            Click("btn_notebook");yield return .2f;Click("btn_close_notebook");yield return .2f;
            Check(GameObject.Find("obj_menu_highlight")==null,"Notebook prompt ends after opening notebook");
            Click("btn_select_arena"); yield return .2f; Click("btn_dialogue"); yield return .2f;
            for (int id = 0; id < 3; id++) Check(!GameObject.Find("btn_card_" + id).GetComponent<Button>().interactable, "Untrained entry blocked " + id);
            Check(!GameObject.Find("btn_confirm").GetComponent<Button>().interactable, "Entry needs a selection");
            Capture("02_출전조건_미달"); yield return .3f;
            Click("btn_entry_training"); yield return .2f; if(HasGuide)Click("btn_dialogue"); yield return .2f;
            Check(Lobby.CurrentPage == "training_select" && Mgr_User.Data.SelectedMonster == -1, "Training opens without prior monster selection");
            CheckCover(true); Capture("03_훈련대상_목록"); yield return .3f;
            for (int id = 0; id < 3; id++)
            {
                foreach(float delay in OpenMenu("btn_monsters"))yield return delay; yield return .2f; if (HasGuide) Click("btn_dialogue"); yield return .2f;
                CheckCover(true); if (id == 0) Capture("04_보유몬스터_전체백판"); yield return .3f;
                Check(GameObject.Find("btn_confirm") == null, "Information has no selection confirmation");
                Click("btn_card_" + ((id + 1) % 3)); yield return .2f;
                Check(Lobby.CurrentPage == "details" && Mgr_User.Data.SelectedMonster == -1, "Browsing another monster leaves entry unset " + id);
                Click("btn_unique"); yield return .2f; Click("btn_skill_back"); yield return .2f;
                Check(Lobby.CurrentPage == "details", "Unique returns to same information screen");
                Click("btn_passive"); yield return .2f; Click("btn_skill_back"); yield return .2f;
                Check(Lobby.CurrentPage == "details", "Passive returns without tutorial jump");
                foreach(float delay in OpenMenu("btn_training"))yield return delay; yield return .2f;
                Check(!HasGuide, "Repeated training visit has no repeated hint");
                Click("btn_card_" + id); yield return .2f;
                if (id == 0) Capture("05_훈련상세_전체백판"); yield return .3f;
                Click("btn_train"); yield return .7f;
                while (Lobby.IsTraining) yield return .25f;
                Check(Mgr_User.Data.Monsters[id].Level == 5 && Mgr_User.CanEnter(id) && Mgr_User.Data.Monsters[id].EndurancePassive, "Chosen training target grows " + id);
                Check(Mgr_User.Data.SelectedMonster == -1, "Training leaves entrant unset " + id);
                for (int other = id + 1; other < 3; other++) Check(!Mgr_User.Data.Monsters[other].Trained, "Other monster unchanged " + other);
                if (id == 0) Capture("06_훈련결과"); yield return .3f;
                Click("btn_result_confirm"); yield return .2f;
                Click("btn_card_" + id); yield return .2f;
                Check(!GameObject.Find("btn_train").GetComponent<Button>().interactable && !Mgr_User.Train(id), "Duplicate training blocked " + id);
            }
            foreach(float delay in OpenMenu("btn_arena"))yield return delay; yield return .2f; Check(!HasGuide, "Arena hint is once per session");
            Check(GameObject.Find("obj_menu_highlight")==null,"Notebook prompt does not repeat on arena return");
            var notebook=(RectTransform)GameObject.Find("btn_notebook").transform;
            var settings=(RectTransform)GameObject.Find("btn_settings").transform;
            Check(notebook.anchoredPosition.y==settings.anchoredPosition.y&&notebook.anchoredPosition.x<settings.anchoredPosition.x,"Notebook sits left of settings");
            Click("btn_notebook"); yield return .2f; Click("btn_close_notebook"); yield return .2f;
            Check(!HasGuide, "Notebook closes without forced tutorial");
            Click("btn_select_arena"); yield return .2f;
            for (int id = 0; id < 3; id++) Check(GameObject.Find("btn_card_" + id).GetComponent<Button>().interactable, "Every trained monster eligible " + id);
            Click("btn_card_1"); yield return .2f;
            Check(Mgr_User.Data.SelectedMonster == -1, "Card highlight alone does not commit entrant");
            Click("btn_confirm"); yield return .2f; Click("btn_dialogue"); yield return .2f;
            Check(Mgr_User.Data.SelectedMonster == 1, "Only arena confirmation commits entrant");
            foreach(float delay in OpenMenu("btn_monsters"))yield return delay; yield return .2f; Click("btn_card_0"); yield return .2f;
            Check(Mgr_User.Data.SelectedMonster == 1, "Browsing preserves confirmed entrant");
            Capture("07_몬스터상세"); yield return .3f;
            foreach(float delay in OpenMenu("btn_training"))yield return delay; yield return .2f; Click("btn_card_2"); yield return .2f;
            Check(Mgr_User.Data.SelectedMonster == 1, "Training target differs from confirmed entrant");
            foreach(float delay in OpenMenu("btn_arena"))yield return delay; yield return .2f; Click("btn_select_arena"); yield return .2f;
            Click("btn_card_2"); yield return .2f; Click("btn_page_back"); yield return .2f;
            Check(Mgr_User.Data.SelectedMonster == 1, "Cancelling new entry preserves prior confirmation");
            Click("btn_select_arena"); yield return .2f; Click("btn_card_2"); yield return .2f;
            Capture("08_출전_1마리_선택"); yield return .3f;
            Click("btn_confirm"); yield return .2f;
            Check(!HasGuide && Mgr_User.Data.SelectedMonster == 2, "Entrant can change without repeating tactic guide");
            Click("btn_tactic_" + Mgr_Table.Demo.Monsters[2].TacticIndex); yield return .2f;
            Click("btn_tactic_confirm"); yield return .2f; Capture("09_경기모드"); yield return .3f;
            // Both race modes must receive the arena choice, regardless of the last browsed monster.
            foreach (string mode in new[] { Define_Scene.Sprite2D, Define_Scene.OvalToon })
            {
                Click(mode == Define_Scene.Sprite2D ? "btn_enter_2d" : "btn_enter_oval"); yield return 2;
                while (Mgr_Game.Instance.IsTransitioning) yield return .25f;
                var race = UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
                Check(race != null && Mgr_Game.Instance.HeroRunner == Mgr_Table.Demo.Monsters[2].RunnerIndex, "Confirmed entrant reaches " + mode);
                race.SkipToResult(); yield return .7f;
                Mgr_Game.Instance.Go(Define_Scene.Lobby); yield return 2;
                while (Lobby == null || Mgr_Game.Instance.IsTransitioning) yield return .25f;
                Check(!HasGuide && Mgr_User.Data.SelectedMonster == 2 && Mgr_User.CanEnter(2), "Ordinary lobby return retains growth and visited hints");
                foreach(float delay in OpenMenu("btn_arena"))yield return delay; yield return .2f; Click("btn_select_arena"); yield return .2f;
                Click("btn_card_2"); yield return .2f; Click("btn_confirm"); yield return .2f;
                Click("btn_tactic_" + Mgr_Table.Demo.Monsters[2].TacticIndex); yield return .2f;
                Click("btn_tactic_confirm"); yield return .2f;
            }
            Mgr_Game.Instance.StartDemo(); yield return 2;
            while (Mgr_Game.Instance.IsTransitioning) yield return .25f;
            Check(HasGuide && Mgr_User.Data.SelectedMonster == -1 && !Mgr_User.CanEnter(2), "New demo resets progress and greeting");
            Click("btn_skip_all"); yield return .2f;
            foreach(float delay in OpenMenu("btn_training"))yield return delay; yield return .2f;
            Check(!HasGuide && !Mgr_Guide.Enabled, "Skip suppresses screen hints");
            Mgr_Game.Instance.StartDemo(); yield return 2;
            while (Mgr_Game.Instance.IsTransitioning) yield return .25f;
            Check(!HasGuide && !Mgr_Guide.Enabled, "Skip remains for current application run");
            Mgr_User.Load();
            string json = JsonUtility.ToJson(Mgr_User.Data);
            Check(!json.Contains("Guide") && !json.Contains("Tutorial"), "Guide state absent from save data");
            Mgr_Game.Instance.Go(Define_Scene.Title); yield return 2;
            while (Mgr_Game.Instance.IsTransitioning) yield return .25f;
        }
    }
}
