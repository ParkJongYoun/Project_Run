using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectRun.Race.Editor
{
    public static class LobbyModularVerification
    {
        private static EditorApplication.CallbackFunction captureTick;

        [Serializable] private class PlaybackEvidence
        {
            public bool Passed;
            public string Error;
            public int StartFrame;
            public int EndFrame;
            public bool AllLayersAdvanced;
            public bool PauseAllPassed;
            public bool CheerEntered;
            public bool ReturnedToIdle;
            public string[] Layers;
            public int[] CheerCounts;
        }

        // Observes real Play-mode Updates. Does not advance clocks manually.
        public static string BeginPlaybackVerification()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play first.");
            if (captureTick != null) throw new InvalidOperationException("Verification is already running.");
            var players = UnityEngine.Object.FindObjectsByType<UIItem_LobbyAnimation>(FindObjectsSortMode.None)
                .Where(p => p.isActiveAndEnabled).OrderBy(p => p.name).ToArray();
            Require(players.Length == 3, "Expected three active animation layers; the butler is a still image.");
            var main = players.Single(p => p.name == "img_maid_cat");
            var button = UnityEngine.Object.FindFirstObjectByType<UIPanel_Lobby>().transform
                .Find("obj_safe_area/group_caption/btn_pause").GetComponent<UnityEngine.UI.Button>();
            var evidence = new PlaybackEvidence { StartFrame = Time.frameCount, Layers = players.Select(p => p.name).ToArray() };
            var initial = players.Select(p => p.Elapsed).ToArray();
            float[] paused = null;
            bool[] advanced = new bool[players.Length];
            int initialCheerCount = main.CheerCount;
            double start = EditorApplication.timeSinceStartup;
            double pauseTime = -1;
            string output = Path.Combine(Path.GetDirectoryName(Application.dataPath),
                "DataBundles/LobbyAnimation/WildRose_Modular_v01/_work/unity_playback.json");
            captureTick = () =>
            {
                try
                {
                    if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play stopped before verification completed.");
                    double elapsed = EditorApplication.timeSinceStartup - start;
                    for (int i = 0; i < players.Length; i++) advanced[i] |= Mathf.Abs(players[i].Elapsed - initial[i]) > .02f;
                    if (elapsed > 2 && pauseTime < 0)
                    {
                        button.onClick.Invoke();
                        Require(players.All(p => !p.IsPlaying), "Pause button did not pause every layer.");
                        paused = players.Select(p => p.Elapsed).ToArray(); pauseTime = elapsed;
                    }
                    if (pauseTime >= 0 && !evidence.PauseAllPassed && elapsed - pauseTime > .5)
                    {
                        Require(players.Select((p, i) => p.Elapsed == paused[i]).All(v => v), "A paused layer advanced.");
                        button.onClick.Invoke();
                        Require(players.All(p => p.IsPlaying), "Resume button did not resume every layer.");
                        evidence.PauseAllPassed = true;
                    }
                    evidence.CheerEntered |= main.CheerCount > initialCheerCount && main.IsCheering;
                    evidence.ReturnedToIdle |= evidence.CheerEntered && !main.IsCheering;
                    evidence.AllLayersAdvanced = advanced.All(v => v);
                    if (evidence.ReturnedToIdle && evidence.PauseAllPassed && evidence.AllLayersAdvanced)
                    {
                        evidence.Passed = true;
                        evidence.EndFrame = Time.frameCount;
                        evidence.CheerCounts = players.Select(p => p.CheerCount).ToArray();
                        File.WriteAllText(output, JsonUtility.ToJson(evidence, true));
                        EditorApplication.update -= captureTick; captureTick = null;
                        Debug.Log("Lobby playback verification passed.");
                    }
                    else if (elapsed > 55) throw new TimeoutException("Lobby playback did not complete one idle/cheer/idle cycle within 55 seconds.");
                }
                catch (Exception error)
                {
                    evidence.Error = error.Message; evidence.EndFrame = Time.frameCount;
                    File.WriteAllText(output, JsonUtility.ToJson(evidence, true));
                    EditorApplication.update -= captureTick; captureTick = null;
                    Debug.LogError("Lobby playback verification: " + error.Message);
                }
            };
            EditorApplication.update += captureTick;
            return "Observing real frame advancement, pause/resume, one cheer and idle return.";
        }

        public static string VerifyStateMachine()
        {
            const string folder = "Assets/ArtBundles/Lobby/Demo/WildRose/";
            var idle = AssetDatabase.LoadAssetAtPath<Data_LobbyAnimation>(folder + "maid_cat_idle.asset");
            var cheer = AssetDatabase.LoadAssetAtPath<Data_LobbyAnimation>(folder + "maid_cat_cheer.asset");
            var ambient = AssetDatabase.LoadAssetAtPath<Data_LobbyAnimation>(folder + "curtain_left_idle.asset");
            Require(idle != null && cheer != null && ambient != null, "Clips must exist.");
            var objects = new List<GameObject>();
            try
            {
                var main = Create(objects); main.ConfigureStates(idle, cheer, 3, 3);
                var other = Create(objects); other.ConfigureStates(ambient, null, 3, 3, .11f);
                var reference = Create(objects); reference.ConfigureStates(ambient, null, 3, 3, .11f);
                bool entered = false, returned = false;
                float step = 1f / 120f;
                int frames = Mathf.CeilToInt((idle.Duration * 3 + cheer.Duration + idle.Duration) / step);
                for (int i = 0; i < frames; i++)
                {
                    bool wasCheering = main.IsCheering;
                    main.Advance(step); other.Advance(step); reference.Advance(step);
                    Require(other.CurrentFrame == reference.CurrentFrame && Mathf.Abs(other.Elapsed - reference.Elapsed) < .0001f,
                        "Special action reset an ambient clock.");
                    if (!wasCheering && main.IsCheering)
                    {
                        Require(main.CompletedIdleLoops == 3, "Cheer started before the idle boundary.");
                        entered = true;
                    }
                    if (wasCheering && !main.IsCheering) returned = true;
                    Require(main.CurrentFrame >= 0 && main.CurrentFrame < main.Clip.Frames.Length, "Invalid frame index.");
                }
                Require(entered && returned && main.CheerCount == 1, "Expected one cheer and return to idle.");
                foreach (var player in new[] { main, other })
                {
                    float before = player.Elapsed; int frame = player.CurrentFrame;
                    player.SetPlaying(false);
                    for (int i = 0; i < 120; i++) player.Advance(step);
                    Require(player.Elapsed == before && player.CurrentFrame == frame, "Pause changed the frame.");
                    player.SetPlaying(true); player.Advance(step);
                    Require(player.Elapsed != before, "Resume failed.");
                }
                var randomState = UnityEngine.Random.state;
                try
                {
                    for (int trial = 0; trial < 12; trial++)
                    {
                        main.ConfigureStates(idle, cheer, 3, 6);
                        int limit = Mathf.CeilToInt(idle.Duration * 6 / .1f) + 3;
                        for (int i = 0; i < limit && !main.IsCheering; i++) main.Advance(.1f);
                        Require(main.IsCheering && main.CompletedIdleLoops >= 3 && main.CompletedIdleLoops <= 6,
                            "Random idle interval is outside the configured bounds.");
                    }
                }
                finally { UnityEngine.Random.state = randomState; }
                return "PASS: idle boundary, one-shot cheer, idle return, independent ambient clock, pause/resume, random 3-6 cycles.";
            }
            finally { foreach (var item in objects) UnityEngine.Object.DestroyImmediate(item); }
        }

        private static UIItem_LobbyAnimation Create(List<GameObject> objects)
        {
            var go = new GameObject("lobby_verification", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.hideFlags = HideFlags.HideAndDontSave; objects.Add(go);
            return go.AddComponent<UIItem_LobbyAnimation>();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
