using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ProjectRun.Race.Editor
{
    public static class RaceSkillEffectsValidation
    {
        public static string Output { get; set; } = "DataBundles/작업기록/경기6종_스킬이펙트_20261021";
        private static IEnumerator<float> sequence;
        private static double next;
        private static double deadline;
        private static readonly List<string> notes = new List<string>();
        private static string reportFile = "실제경기_검증.txt";
        public static string Status { get; private set; }
        private static Sys_Race Race => Object.FindFirstObjectByType<Sys_Race>();
        private static Sys_RaceSkillEffects Effects => Object.FindFirstObjectByType<Sys_RaceSkillEffects>();
        private static Sys_DemoSkillVideo Video => Object.FindFirstObjectByType<Sys_DemoSkillVideo>();

        public static void BackupSave()
        {
            Directory.CreateDirectory(Output);
            string save = Path.Combine(Application.persistentDataPath,"project_run_demo_v1.json");
            if (File.Exists(save)) File.Copy(save, Output+"/검증전_로컬저장.json", true);
        }
        public static void RestoreSave()
        {
            if (!File.Exists(Output+"/검증전_로컬저장.json")) return;
            string text = File.ReadAllText(Output+"/검증전_로컬저장.json");
            if (Application.isPlaying) { JsonUtility.FromJsonOverwrite(text,Mgr_User.Data); Mgr_User.Save(); }
            else File.WriteAllText(Path.Combine(Application.persistentDataPath,"project_run_demo_v1.json"),text);
        }
        public static void Run()
        {
            if (!EditorApplication.isPlaying || sequence != null) throw new InvalidOperationException("Enter Play first.");
            reportFile="실제경기_검증.txt";
            notes.Clear(); next=0; deadline=EditorApplication.timeSinceStartup+240;
            sequence=Steps(); EditorApplication.update+=Tick; Write("RUNNING");
        }
        public static void RunMovieSkip()
        {
            if (!EditorApplication.isPlaying || sequence != null) throw new InvalidOperationException("Enter Play first.");
            reportFile="컷씬건너뛰기_검증.txt";
            notes.Clear(); next=0; deadline=EditorApplication.timeSinceStartup+100;
            sequence=MovieSkipSteps(); EditorApplication.update+=Tick; Write("RUNNING");
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup<next) return;
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup>deadline)
                    throw new Exception("Verification interrupted or timed out.");
                if (!sequence.MoveNext()) { Finish("PASSED"); return; }
                next=EditorApplication.timeSinceStartup+sequence.Current;
            }
            catch(Exception e) { notes.Add(e.ToString()); Finish("FAILED"); Debug.LogException(e); }
        }
        private static void Finish(string state)
        {
            EditorApplication.update-=Tick; sequence?.Dispose(); sequence=null;
            if(Race!=null) Race.Paused=true;
            var config=AssetDatabase.LoadAssetAtPath<Data_RaceSkillEffects>(RaceSkillEffectsBuilder.ConfigPath);
            config.Enabled=true; foreach(var entry in config.Runners) entry.Enabled=true;
            RestoreSave(); Write(state);
        }
        private static void Write(string status)
        {
            Status=status;
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Output+"/"+reportFile, new[]{status}.Concat(notes));
        }
        private static void Check(bool value,string message)
        {
            if(!value) throw new Exception(message);
            notes.Add("PASS "+message); Write("RUNNING");
        }
        private static Data_RaceSimulation Expected(Sys_Race race)
        {
            var expected=new Data_RaceSimulation(race.Entrants,race.Course,race.EnableLaneContests,race.LapCount);
            expected.Begin();
            for(int i=0;i<18000&&expected.Phase!=E_RacePhase.Result;i++) expected.Step();
            return expected;
        }
        private static IEnumerator<float> MovieSkipSteps()
        {
            Mgr_User.Data.SelectedMonster=0;
            Mgr_Game.Instance.EnterRace(Define_Scene.OvalToon);
            yield return .5f;
            while(Race==null||Effects==null||Mgr_Game.Instance.IsTransitioning) yield return .05f;
            var race=Race; var effects=Effects; var expected=Expected(race);
            race.Paused=false; race.PlaybackSpeed=3;
            while(Video==null||!Video.IsActive) yield return .025f;
            while(Video.MediaTime<.2)
            {
                if(effects.IsVisible(0)) throw new Exception("Effect visible before movie skip.");
                yield return .025f;
            }
            Check(GameObject.Find("eff_skill_arrival")==null,"legacy arrival particles suppressed before movie");
            Check(!effects.IsVisible(0),"hero effect hidden before movie skip");
            Video.Skip();
            while(Video.IsActive)
            {
                if(effects.IsVisible(0)) throw new Exception("Effect visible during return fade after skip.");
                yield return .025f;
            }
            yield return .1f;
            Check(effects.IsVisible(0)&&effects.StartCount(0)==1,"movie skip starts effect once after return fade");
            race.PlaybackSpeed=1;
            yield return .5f;
            race.Paused=true;
            ScreenCapture.CaptureScreenshot(Output+"/실제게임화면_컷씬복귀후.png");
            yield return .4f;
            race.SkipToResult();
            yield return .1f;
            Check(race.Simulation.Order.SequenceEqual(expected.Order)&&race.Simulation.Runners.Zip(expected.Runners,
                (a,b)=>a.FinishTime==b.FinishTime&&a.SkillActivationCount==b.SkillActivationCount).All(x=>x),
                "movie skip preserves exact simulation finish times and order");
        }
        private static IEnumerator<float> Steps()
        {
            for(int hero=0;hero<3;hero++)
            {
                Mgr_User.Data.SelectedMonster=hero;
                Mgr_Game.Instance.EnterRace(Define_Scene.OvalToon);
                yield return .5f;
                while(Race==null||Effects==null||Mgr_Game.Instance.IsTransitioning) yield return .05f;
                var race=Race; var effects=Effects;
                var config=effects.Configuration;
                while(config==null){yield return .05f;config=effects.Configuration;}
                var expected=Expected(race);
                bool[] seen=new bool[6]; bool sawVideo=false; bool sawWaiting=false;
                race.Paused=false; race.PlaybackSpeed=3;
                while(seen.Any(x=>!x))
                {
                    if(Video!=null&&Video.IsActive)
                    {
                        sawVideo=true;
                        if(effects.IsWaitingForVideo(hero)) sawWaiting=true;
                        if(effects.IsVisible(hero)) throw new Exception("Hero effect appeared before complete movie return.");
                    }
                    for(int i=0;i<6;i++)
                    {
                        if(seen[i]||!effects.IsVisible(i)||race.Simulation.Runners[i].SkillAge<.65f)continue;
                        if(i==hero && !sawVideo)throw new Exception("Hero video was never active.");
                        seen[i]=true;
                        if(hero==0||i==hero)
                        {
                            race.PlaybackSpeed=0;
                            yield return .15f;
                            CaptureRunner(i,"선택"+hero+"_주자"+i);
                            race.PlaybackSpeed=3;
                        }
                    }
                    if(race.Simulation.Phase==E_RacePhase.Result&&!seen.All(x=>x))throw new Exception("Missing runner effects.");
                    yield return .025f;
                }
                Check(sawVideo&&sawWaiting,"hero "+hero+" waits through movie and return, then plays");
                Check(Enumerable.Range(0,6).All(i=>effects.StartCount(i)==1),"hero "+hero+" all six effects start exactly once");
                if(hero==2)
                {
                    Check(race.Simulation.Runners[2].Lane>3,"Peco continues its burst on the outside lane");
                    race.Paused=true;
                    ScreenCapture.CaptureScreenshot(Output+"/실제게임화면_페코페코_후반외곽.png");
                    yield return .3f;
                }
                var active=effects.GetComponentsInChildren<ParticleSystem>().Where(p=>p.name.StartsWith("fx_")).ToArray();
                race.Paused=true;
                var times=active.Select(p=>p.time).ToArray();
                yield return .25f;
                Check(active.Select((p,i)=>Mathf.Abs(p.time-times[i])<.0001f).All(x=>x),"particle clock frozen on pause");
                config.Enabled=false;
                yield return .1f;
                Check(Enumerable.Range(0,6).All(i=>!effects.IsVisible(i)),"global disable hides added effects");
                Check(!effects.GetComponent<Sys_DemoSkillEffects>().SuppressGeneratedEffects,"global disable restores legacy hero effect");
                config.Enabled=true; race.Paused=false; race.PlaybackSpeed=0;
                yield return .1f;
                int activeId=Enumerable.Range(0,6).LastOrDefault(i=>effects.IsVisible(i));
                config.Find(activeId).Enabled=false;
                yield return .1f;
                Check(!effects.IsVisible(activeId),"individual runner disable");
                config.Find(activeId).Enabled=true;
                race.SkipToResult();
                yield return .1f;
                Check(Enumerable.Range(0,6).All(i=>!effects.IsVisible(i)),"skip clears all effect instances");
                Check(race.Simulation.Order.SequenceEqual(expected.Order)&&race.Simulation.Runners.Zip(expected.Runners,
                    (a,b)=>a.FinishTime==b.FinishTime&&a.SkillActivationCount==b.SkillActivationCount).All(x=>x),
                    "effects/video/toggles preserve exact simulation finish times and order");
                race.RestartRace(); race.PlaybackSpeed=0;
                yield return .1f;
                Check(Enumerable.Range(0,6).All(i=>!effects.IsVisible(i)&&effects.StartCount(i)==0),"restart clears effects after lane shuffle");
            }
            foreach(var guid in AssetDatabase.FindAssets("t:Shader",new[]{"Assets/ArtBundles/Race/SkillEffects"}))
            {
                var shader=AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                var errors=ShaderUtil.GetShaderMessages(shader).Where(x=>x.severity.ToString()=="Error").ToArray();
                Check(errors.Length==0,"shader "+shader.name);
            }
        }

        public static void CaptureRunner(int id,string name)
        {
            var presentation=Object.FindFirstObjectByType<Sys_RacePresentation>();
            var view=presentation.Views[id];
            Vector3 forward=Race.Simulation.Forward((float)Race.Simulation.Runners[id].Distance);
            var cameraObject=new GameObject("obj_skill_validation_camera");
            var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false; camera.fieldOfView=48; camera.nearClipPlane=.1f; camera.farClipPlane=1500;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.42f,.65f,.82f);
            camera.transform.position=view.transform.position-forward*9+Vector3.Cross(Vector3.up,forward)*9+Vector3.up*7;
            camera.transform.LookAt(view.transform.position+Vector3.up*.9f);
            var data=camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing=false; data.renderShadows=true;
            var rt=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=RenderTexture.active;
            bool async=ShaderUtil.allowAsyncCompilation;
            Texture2D image=null;
            try
            {
                ShaderUtil.allowAsyncCompilation=false;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt; image=new Texture2D(1280,720,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation=async; RenderTexture.active=previous;
                RenderTexture.ReleaseTemporary(rt); if(image!=null)Object.DestroyImmediate(image);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
