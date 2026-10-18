using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    [RequireComponent(typeof(Image))]
    public sealed class UIItem_LobbyAnimation : UIItem_Base<Data_LobbyAnimation>
    {
        [SerializeField] private Data_LobbyAnimation Animation;
        [SerializeField] private Data_LobbyAnimation CheerAnimation;
        [SerializeField, Min(1)] private int MinimumIdleLoops = 3;
        [SerializeField, Min(1)] private int MaximumIdleLoops = 6;
        [SerializeField, Range(0, 1)] private float InitialNormalizedTime;
        [SerializeField] private bool Playing = true;
        public bool UseIllustrationLayout;
        private Image targetImage;
        private Data_LobbyAnimation activeClip;
        private float elapsed;
        private float duration;
        private int currentFrame = -1;
        private int idleLoopsRemaining;
        private bool isCheering;
        private readonly System.Random random = new System.Random();

        public int CurrentFrame => currentFrame;
        public bool IsPlaying => Playing;
        public Data_LobbyAnimation Clip => activeClip != null ? activeClip : Animation;
        public Data_LobbyAnimation IdleClip => Animation;
        public Data_LobbyAnimation CheerClip => CheerAnimation;
        public bool IsCheering => isCheering;
        public int CheerCount { get; private set; }
        public int CompletedIdleLoops { get; private set; }
        public float Elapsed => elapsed;

        private void Awake() => Bind(Animation);

        public override void Bind(Data_LobbyAnimation data)
        {
            Animation = data;
            targetImage = GetComponent<Image>();
            Restart();
        }

        public void ConfigureStates(Data_LobbyAnimation idle, Data_LobbyAnimation cheer,
            int minimumIdleLoops = 3, int maximumIdleLoops = 6, float initialNormalizedTime = 0)
        {
            CheerAnimation = cheer;
            MinimumIdleLoops = Mathf.Max(1, minimumIdleLoops);
            MaximumIdleLoops = Mathf.Max(MinimumIdleLoops, maximumIdleLoops);
            InitialNormalizedTime = Mathf.Clamp01(initialNormalizedTime);
            Bind(idle);
        }

        public void SetPlaying(bool playing) => Playing = playing;

        public void Restart()
        {
            CheerCount = 0;
            CompletedIdleLoops = 0;
            isCheering = false;
            ScheduleCheer();
            SetClip(Animation);
            elapsed = duration > 0 ? Mathf.Repeat(InitialNormalizedTime * duration, duration) : 0;
            ShowCurrentFrame();
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        // The clock belongs to this layer. Another layer's action never resets it.
        public void Advance(float deltaTime)
        {
            if (!Playing || !IsValid(activeClip) || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            // Discard long background-tab gaps instead of replaying missed special actions.
            elapsed += Mathf.Min(deltaTime, .25f);
            if (elapsed >= duration)
            {
                float remainder = elapsed - duration;
                if (isCheering)
                {
                    isCheering = false;
                    ScheduleCheer();
                    SetClip(Animation);
                }
                else
                {
                    CompletedIdleLoops++;
                    if (IsValid(CheerAnimation) && --idleLoopsRemaining <= 0)
                    {
                        isCheering = true;
                        CheerCount++;
                        SetClip(CheerAnimation);
                    }
                }
                elapsed = duration > 0 ? Mathf.Repeat(remainder, duration) : 0;
            }
            ShowCurrentFrame();
        }

        private static bool IsValid(Data_LobbyAnimation clip) => clip != null && clip.Frames != null &&
            clip.Durations != null && clip.Frames.Length > 0 && clip.Frames.Length == clip.Durations.Length && clip.Duration > 0;

        private void ScheduleCheer()
        {
            int minimum = Mathf.Max(1, MinimumIdleLoops);
            idleLoopsRemaining = random.Next(minimum, Mathf.Max(minimum, MaximumIdleLoops) + 1);
        }

        private void SetClip(Data_LobbyAnimation clip)
        {
            activeClip = clip;
            duration = IsValid(clip) ? clip.Duration : 0;
            currentFrame = -1;
            elapsed = 0;
            if(UseIllustrationLayout && clip!=null)
            {
                var rect=(RectTransform)transform;var r=clip.IllustrationRect;var size=clip.IllustrationSize;
                rect.anchorMin=new Vector2(r.xMin/size.x,1-r.yMax/size.y);
                rect.anchorMax=new Vector2(r.xMax/size.x,1-r.yMin/size.y);
                rect.offsetMin=rect.offsetMax=Vector2.zero;
            }
        }

        private void ShowCurrentFrame()
        {
            if (!IsValid(activeClip)) return;
            float end = 0;
            for (int i = 0; i < activeClip.Frames.Length; i++)
            {
                end += Mathf.Max(activeClip.Durations[i], .001f);
                if (elapsed < end) { ShowFrame(i); break; }
            }
        }

        private void ShowFrame(int index)
        {
            if (targetImage == null || !IsValid(activeClip) ||
                index >= activeClip.Frames.Length || currentFrame == index) return;
            currentFrame = index;
            targetImage.sprite = activeClip.Frames[index];
        }
    }
}
