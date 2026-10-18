using UnityEngine;
using UnityEngine.Video;

namespace ProjectRun.Race
{
    // The second simulation skill event selects the movie. Playback never advances the race clock.
    public sealed class Sys_DemoSkillVideo : MonoBehaviour
    {
        private enum Phase { Idle, Waiting, Playing, Returning }
        private Sys_Race race;
        private UIPanel_DemoRace panel;
        private Sys_RacePresentation presentation;
        private VideoPlayer player;
        private RenderTexture target;
        private Phase phase;
        private bool failed,playRequested,pausedVideo;
        private int generation;
        private float age,fadeAge,stalledFor,returnOpacity;
        private double lastMediaTime;
        public bool IsActive=>phase!=Phase.Idle;
        public bool IsReady=>player!=null&&player.isPrepared;
        public bool HasFirstFrame {get;private set;}
        public string VideoPath {get;private set;}
        public double MediaTime=>player==null?0:player.time;

        public static string PathFor(string monsterId,bool sprite2D)
        {
            if(monsterId!="Poring"&&monsterId!="Minorous"&&monsterId!="Eddga")return null;
            return "Video/DemoSkills/"+monsterId+"_"+(sprite2D?"2D":"3D")+".mp4";
        }
        public void Configure(Sys_Race targetRace,UIPanel_DemoRace targetPanel,string monsterId,bool sprite2D)
        {
            race=targetRace;panel=targetPanel;VideoPath=PathFor(monsterId,sprite2D);
            presentation=FindFirstObjectByType<Sys_RacePresentation>();
            if(string.IsNullOrEmpty(VideoPath)){failed=true;return;}
            target=new RenderTexture(512,288,0,RenderTextureFormat.ARGB32){name="DemoSkillVideo",filterMode=FilterMode.Bilinear};
            target.Create();
            player=gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake=false;player.isLooping=false;player.source=VideoSource.Url;
            // URL playback also works from StreamingAssets in WebGL, unlike embedded VideoClips.
            player.url=Application.streamingAssetsPath.TrimEnd('/')+"/"+VideoPath;
            player.renderMode=VideoRenderMode.RenderTexture;player.targetTexture=target;
            player.audioOutputMode=VideoAudioOutputMode.None;
            player.waitForFirstFrame=true;player.sendFrameReadyEvents=true;
            player.timeUpdateMode=VideoTimeUpdateMode.UnscaledGameTime;
            player.prepareCompleted+=Prepared;player.frameReady+=FrameReady;
            player.loopPointReached+=Ended;player.errorReceived+=Failed;
            panel.BindVideo(this,target);player.Prepare();
        }
        private void OnEnable()=>Mgr_Event.Reset+=ResetVideo;
        public bool TryPlay()
        {
            if(failed||player==null||IsActive||!panel.CutscenesEnabled||race.IsSkipping||!race.AcquirePresentationHold(this))return false;
            generation=race.Generation;phase=Phase.Waiting;
            age=fadeAge=stalledFor=0;lastMediaTime=0;
            HasFirstFrame=playRequested=pausedVideo=false;
            if(presentation!=null)presentation.BeginSkillVideo(Mgr_Game.Instance.HeroRunner);
            if(player.isPrepared)PlayPrepared();else player.Prepare();
            return true;
        }
        private void Prepared(VideoPlayer source){if(phase==Phase.Waiting&&!race.Paused)PlayPrepared();}
        private void PlayPrepared(){if(playRequested)return;playRequested=true;player.Play();}
        private void FrameReady(VideoPlayer source,long frame)
        {
            if(phase!=Phase.Waiting)return;
            HasFirstFrame=true;phase=Phase.Playing;fadeAge=0;
        }
        private void Update()
        {
            if(!IsActive)return;
            if(race.Generation!=generation||race.IsSkipping||race.Simulation.Phase==E_RacePhase.Result||!panel.CutscenesEnabled)
            {Cancel();return;}
            if(race.Paused&&phase!=Phase.Returning)
            {
                if(player.isPlaying){player.Pause();pausedVideo=true;}return;
            }
            if(pausedVideo){pausedVideo=false;if(phase!=Phase.Returning)player.Play();}
            float delta=Time.unscaledDeltaTime;age+=delta;fadeAge+=delta;
            if(phase==Phase.Waiting)
            {
                if(player.isPrepared&&!playRequested)PlayPrepared();
                // Some browser backends update media time without a frameReady callback.
                if(player.isPlaying&&player.time>0.02)FrameReady(player,0);
                else if(age>5)Fail("First frame timed out.");
                return;
            }
            if(phase==Phase.Playing)
            {
                panel.SetVideoOpacity(Mathf.SmoothStep(0,1,fadeAge/.15f));
                stalledFor=player.time>lastMediaTime+.001?0:stalledFor+delta;lastMediaTime=player.time;
                if(stalledFor>4||age>14){Fail("Playback stalled.");return;}
                if(player.length>0&&player.time>=player.length-.04)BeginReturn();
            }
            else if(phase==Phase.Returning)
            {
                panel.SetVideoOpacity(returnOpacity*(1-Mathf.SmoothStep(0,1,fadeAge/.2f)));
                if(fadeAge>=.2f)Cancel();
            }
        }
        private void Ended(VideoPlayer source){if(IsActive)BeginReturn();}
        public void Skip(){if(IsActive)BeginReturn();}
        private void BeginReturn()
        {
            if(phase==Phase.Returning)return;
            returnOpacity=HasFirstFrame?Mathf.SmoothStep(0,1,fadeAge/.15f):0;
            phase=Phase.Returning;fadeAge=0;player.Pause();
        }
        public void Cancel()
        {
            bool wasActive=IsActive;phase=Phase.Idle;pausedVideo=playRequested=false;
            if(panel!=null)panel.SetVideoOpacity(0);
            if(race!=null)race.ReleasePresentationHold(this);
            if(wasActive&&presentation!=null)presentation.EndSkillVideo();
            if(player!=null&&wasActive)player.Stop();
        }
        private void Failed(VideoPlayer source,string message)=>Fail(message);
        private void Fail(string message)
        {
            bool fallback=IsActive;failed=true;Cancel();
            Debug.LogWarning("Demo skill video ("+VideoPath+"): "+message,this);
            if(fallback&&panel!=null)panel.PlayComicFallback();
        }
        private void ResetVideo()
        {
            Cancel();HasFirstFrame=false;failed=false;
            if(player!=null&&isActiveAndEnabled&&!race.IsSkipping)player.Prepare();
        }
        private void OnDisable(){Mgr_Event.Reset-=ResetVideo;Cancel();}
        private void OnDestroy()
        {
            if(player!=null)
            {
                player.prepareCompleted-=Prepared;player.frameReady-=FrameReady;
                player.loopPointReached-=Ended;player.errorReceived-=Failed;player.targetTexture=null;
            }
            if(target!=null){target.Release();Destroy(target);}
        }
    }
}
