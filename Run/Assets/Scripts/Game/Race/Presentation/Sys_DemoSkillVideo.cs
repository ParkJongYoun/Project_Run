using UnityEngine;
using UnityEngine.Video;

namespace ProjectRun.Race
{
    // One simulation skill event selects the movie. Playback never advances the race clock.
    public sealed class Sys_DemoSkillVideo : MonoBehaviour
    {
        private enum Phase { Idle, Waiting, Playing, Returning }
        private Sys_Race race;
        private UIPanel_DemoRace panel;
        private Sys_RacePresentation presentation;
        private Sys_RaceCamera director;
        private CameraRig.Sys_RaceCinemachine cinemachine;
        private VideoPlayer player;
        private RenderTexture target;
        private Phase phase;
        private bool failed,playRequested,pausedVideo,cameraFocused;
        private int generation;
        private int playedGeneration = -1;
        private const float fadeInSeconds = .28f;
        private const float fadeOutSeconds = .38f;
        private float age,fadeAge,stalledFor,returnOpacity;
        private double lastMediaTime;
        public bool IsActive=>phase!=Phase.Idle;
        public bool IsReady=>player!=null&&player.isPrepared;
        public bool HasFailed=>failed;
        public bool HasFirstFrame {get;private set;}
        public string VideoPath {get;private set;}
        public double MediaTime=>player==null?0:player.time;

        public static string PathFor(string monsterId)
        {
            if(monsterId!="Poring"&&monsterId!="Minorous"&&monsterId!="Pecopeco")return null;
            return "Video/RaceSkills/"+monsterId+".mp4";
        }
        public void Configure(Sys_Race targetRace,UIPanel_DemoRace targetPanel,string monsterId)
        {
            race=targetRace;panel=targetPanel;VideoPath=PathFor(monsterId);
            presentation=FindFirstObjectByType<Sys_RacePresentation>();
            director=FindFirstObjectByType<Sys_RaceCamera>();
            cinemachine=FindFirstObjectByType<CameraRig.Sys_RaceCinemachine>();
            if(string.IsNullOrEmpty(VideoPath)){failed=true;return;}
            target=new RenderTexture(1920,1080,0,RenderTextureFormat.ARGB32)
                {name="RaceSkillVideo1080",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
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
            if(failed||player==null||IsActive||playedGeneration==race.Generation||!panel.CutscenesEnabled
                ||race.IsSkipping||!race.AcquirePresentationHold(this))return false;
            generation=playedGeneration=race.Generation;phase=Phase.Waiting;
            age=fadeAge=stalledFor=0;lastMediaTime=0;
            HasFirstFrame=playRequested=pausedVideo=cameraFocused=false;
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
                else if(age>12)Fail("First frame timed out.");
                return;
            }
            if(phase==Phase.Playing)
            {
                float opacity=Mathf.SmoothStep(0,1,fadeAge/fadeInSeconds);
                panel.SetVideoOpacity(opacity);
                // Hide the reframe beneath the fully opaque movie, then reveal the same runner.
                if(!cameraFocused&&opacity>=.999f)
                {
                    if(cinemachine!=null&&cinemachine.isActiveAndEnabled)cinemachine.BeginSkillVideo(Mgr_Game.Instance.HeroRunner);
                    else if(director!=null&&director.isActiveAndEnabled)director.BeginSkillVideo(Mgr_Game.Instance.HeroRunner);
                    cameraFocused=true;
                }
                stalledFor=player.time>lastMediaTime+.001?0:stalledFor+delta;lastMediaTime=player.time;
                if(stalledFor>5||age>25){Fail("Playback stalled.");return;}
                if(player.length>0&&player.time>=player.length-.04)BeginReturn();
            }
            else if(phase==Phase.Returning)
            {
                panel.SetVideoOpacity(returnOpacity*(1-Mathf.SmoothStep(0,1,fadeAge/fadeOutSeconds)));
                if(fadeAge>=fadeOutSeconds)Finish();
            }
        }
        private void Ended(VideoPlayer source){if(IsActive)BeginReturn();}
        public void Skip(){if(IsActive)BeginReturn();}
        private void BeginReturn()
        {
            if(phase==Phase.Returning)return;
            returnOpacity=HasFirstFrame?Mathf.SmoothStep(0,1,fadeAge/fadeInSeconds):0;
            phase=Phase.Returning;fadeAge=0;player.Pause();
            race.ReleasePresentationHold(this);
            if(cameraFocused&&director!=null)director.EndSkillVideo();
            if(cameraFocused&&cinemachine!=null)cinemachine.EndSkillVideo();
        }
        public void Cancel()
        {
            Finish();
            if(director!=null)director.CancelSkillVideo();
            if(cinemachine!=null)cinemachine.CancelSkillVideo();
            cameraFocused=false;
        }
        private void Finish()
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
            failed=true;Cancel();
            Debug.LogWarning("Demo skill video ("+VideoPath+"): "+message,this);
        }
        private void ResetVideo()
        {
            Cancel();HasFirstFrame=false;failed=false;playedGeneration=-1;
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
