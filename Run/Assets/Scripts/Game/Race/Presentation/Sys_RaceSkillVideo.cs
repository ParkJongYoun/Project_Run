using UnityEngine;
using UnityEngine.Video;

namespace ProjectRun.Race
{
    // This component owns presentation time only; the simulation's skill and result rules are unchanged.
    [DefaultExecutionOrder(-90)]
    public sealed class Sys_RaceSkillVideo : MonoBehaviour
    {
        public Sys_Race Race;
        public Sys_RacePresentation Presentation;
        public Sys_RaceCamera Director;
        public UIPanel_RaceSkillVideo Panel;
        public UIPanel_RaceHUD HUD;
        public VideoPlayer Player;
        public int RunnerId = 2;
        public string VideoPath = "Video/FoxUltimate1024.mp4";
        public float FadeInSeconds = .4f;
        public float FadeOutSeconds = .85f;
        public bool IsActive => phase != Phase.Idle;
        public bool IsReady => Player != null && Player.isPrepared;
        public bool HasFirstFrame { get; private set; }
        private enum Phase { Idle, WaitingForFrame, Playing, Returning }
        private Phase phase;
        private RenderTexture target;
        private int playedGeneration = -1;
        private float age, fadeAge, returnDuration, returnOpacity, stalledFor;
        private double lastMediaTime;
        private bool cameraFocused, pausedVideo, ended, failed;

        private void Awake()
        {
            target = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32) { name = "FoxSkillVideo", filterMode = FilterMode.Bilinear };
            target.Create();
            Player.playOnAwake = false; Player.isLooping = false;
            Player.source = VideoSource.Url;
            // Web builds require a streaming URL, not an embedded VideoClip in Resources.
            Player.url = Application.streamingAssetsPath.TrimEnd('/') + "/" + VideoPath;
            Player.renderMode = VideoRenderMode.RenderTexture;
            Player.targetTexture = target;
            Player.audioOutputMode = VideoAudioOutputMode.None;
            Player.waitForFirstFrame = true;
            Player.sendFrameReadyEvents = true;
            Player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            Panel.VideoImage.texture = target;
            Panel.SetOpacity(0);
        }
        private void OnEnable()
        {
            Mgr_Event.RaceChanged += Handle;
            Mgr_Event.Reset += ResetVideo;
            Player.frameReady += FrameReady;
            Player.loopPointReached += EndReached;
            Player.errorReceived += VideoError;
            Panel.SkipRequested += SkipVideo;
            if (HUD != null) Panel.MenuRequested += HUD.ToggleMenu;
        }
        private void Start() => Prepare();
        private void Prepare()
        {
            if (!failed && !Player.isPrepared && !IsActive) Player.Prepare();
        }
        private void Handle(Data_RaceEvent value)
        {
            if (value.Kind != E_RaceEvent.Skill || value.RunnerId != RunnerId || Race.IsSkipping
                || !Presentation.Cutscenes || IsActive || playedGeneration == Race.Generation || failed) return;
            // A network/preparation failure falls back to the existing live skill camera without holding the race.
            if (!IsReady || !Race.AcquirePresentationHold(this)) return;
            playedGeneration = Race.Generation;
            phase = Phase.WaitingForFrame;
            age = fadeAge = stalledFor = 0;
            lastMediaTime = 0; ended = pausedVideo = cameraFocused = HasFirstFrame = false;
            Presentation.BeginSkillVideo(RunnerId);
            Player.Play();
        }
        private void FrameReady(VideoPlayer player, long frame)
        {
            if (phase != Phase.WaitingForFrame) return;
            HasFirstFrame = true;
            phase = Phase.Playing; fadeAge = 0;
        }
        private void Update()
        {
            if (!IsActive) return;
            if (Race.Generation != playedGeneration || Race.IsSkipping || Race.Simulation.Phase == E_RacePhase.Result)
            { Abort(); return; }
            if (!Presentation.Cutscenes && phase != Phase.Returning) BeginReturn(true);
            if (Race.Paused)
            {
                if (Player.isPlaying) { Player.Pause(); pausedVideo = true; }
                return;
            }
            if (pausedVideo) { pausedVideo = false; if (!ended) Player.Play(); }
            float delta = Time.unscaledDeltaTime;
            age += delta; fadeAge += delta;
            if (phase == Phase.WaitingForFrame)
            {
                if (age > 5) Fail("First video frame did not arrive within five seconds.");
                return;
            }
            if (phase == Phase.Playing)
            {
                float alpha = Mathf.SmoothStep(0, 1, fadeAge / Mathf.Max(.05f, FadeInSeconds));
                Panel.SetOpacity(alpha);
                // Reframe the live fox only once the movie fully covers the camera change.
                if (!cameraFocused && alpha >= .999f) { Director.BeginSkillVideo(RunnerId); cameraFocused = true; }
                stalledFor = Player.time > lastMediaTime + .001 ? 0 : stalledFor + delta;
                lastMediaTime = Player.time;
                if (stalledFor > 6 || age > 35) { Fail("Video playback stalled; returning to the race."); return; }
                if (Player.length > 0 && Player.time >= Player.length - FadeOutSeconds) BeginReturn(false);
            }
            else if (phase == Phase.Returning)
            {
                Panel.SetOpacity(returnOpacity * (1 - Mathf.SmoothStep(0, 1, fadeAge / returnDuration)));
                if (fadeAge >= returnDuration) Finish();
            }
        }
        private void EndReached(VideoPlayer player)
        {
            ended = true;
            if (phase == Phase.Playing) BeginReturn(false);
        }
        public void SkipVideo() { if (IsActive && phase != Phase.Returning) BeginReturn(true); }
        private void BeginReturn(bool skipped)
        {
            if (!IsActive || phase == Phase.Returning) return;
            returnOpacity = Panel.Opacity.alpha;
            returnDuration = skipped ? .25f : Mathf.Max(.05f, FadeOutSeconds);
            fadeAge = 0; phase = Phase.Returning;
            if (skipped) { Player.Pause(); ended = true; }
            if (!cameraFocused && returnOpacity > .5f) { Director.BeginSkillVideo(RunnerId); cameraFocused = true; }
            // Resume all racers together during the dissolve; their existing gait clocks continue unchanged.
            Race.ReleasePresentationHold(this);
            if (cameraFocused) Director.EndSkillVideo();
        }
        private void Finish()
        {
            phase = Phase.Idle;
            Panel.SetOpacity(0);
            Presentation.EndSkillVideo();
            Race.ReleasePresentationHold(this);
            Player.Stop();
            Prepare();
        }
        private void VideoError(VideoPlayer player, string message) => Fail(message);
        private void Fail(string message)
        {
            failed = true;
            Debug.LogWarning("Fox skill video: " + message, this);
            Abort();
        }
        private void Abort()
        {
            phase = Phase.Idle; pausedVideo = false;
            if (Panel != null) Panel.SetOpacity(0);
            if (Race != null) Race.ReleasePresentationHold(this);
            if (Presentation != null) Presentation.EndSkillVideo();
            if (Director != null) Director.CancelSkillVideo();
            if (Player != null) Player.Stop();
        }
        private void ResetVideo()
        {
            Abort(); playedGeneration = -1; HasFirstFrame = false; failed = false;
            if (isActiveAndEnabled && !Race.IsSkipping) Prepare();
        }
        private void OnDisable()
        {
            Mgr_Event.RaceChanged -= Handle; Mgr_Event.Reset -= ResetVideo;
            if (Player != null) { Player.frameReady -= FrameReady; Player.loopPointReached -= EndReached; Player.errorReceived -= VideoError; }
            if (Panel != null) Panel.SkipRequested -= SkipVideo;
            if (Panel != null && HUD != null) Panel.MenuRequested -= HUD.ToggleMenu;
            Abort();
        }
        private void OnDestroy()
        {
            if (Player != null) Player.targetTexture = null;
            if (Panel != null && Panel.VideoImage != null) Panel.VideoImage.texture = null;
            if (target != null) { target.Release(); Destroy(target); }
        }
    }
}
