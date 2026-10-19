using UnityEngine;
using UnityEngine.Video;

namespace ProjectRun.Race
{
    // One selected movie is prepared at a time; the poster covers preparation and decoding.
    public sealed class UIItem_RanchTrainingVideo : MonoBehaviour
    {
        private VideoPlayer player;
        private RenderTexture texture;
        private UnityEngine.UI.RawImage image;
        private bool requested;
        private bool started;
        private float waitingSeconds;
        private float stalledSeconds;
        private float visibleSeconds;
        private double previousTime;

        public bool HasStarted => started;
        public bool IsComplete { get; private set; }
        public bool HasFailed { get; private set; }
        public bool IsPrepared => player != null && player.isPrepared;
        public double MediaTime => player == null ? 0 : player.time;
        public float Progress => IsComplete ? 1 : player == null || player.length <= 0
            ? 0 : Mathf.Clamp01((float)(player.time / player.length));
        public string VideoPath { get; private set; }

        public void Prepare(UnityEngine.UI.Image poster, string monsterId)
        {
            Shutdown();
            HasFailed = IsComplete = started = requested = false;
            waitingSeconds = stalledSeconds = visibleSeconds = 0;
            previousTime = 0;
            if (poster == null) { HasFailed = true; return; }
            if (image == null)
            {
                var surface = new GameObject("img_training_video", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
                surface.transform.SetParent(poster.transform.parent, false);
                image = surface.GetComponent<UnityEngine.UI.RawImage>();
                image.raycastTarget = false;
            }
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one;
            image.transform.SetAsLastSibling();
            image.color = new Color(1, 1, 1, 0);
            image.gameObject.SetActive(true);
            texture = new RenderTexture(1024, 576, 0, RenderTextureFormat.ARGB32)
            { name = "RanchTrainingVideo", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.Create();
            image.texture = texture;
            if (player == null)
            {
                player = gameObject.AddComponent<VideoPlayer>();
                player.playOnAwake = false;
                player.isLooping = false;
                player.source = VideoSource.Url;
                player.renderMode = VideoRenderMode.RenderTexture;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.waitForFirstFrame = true;
                player.sendFrameReadyEvents = true;
                player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                player.prepareCompleted += Prepared;
                player.frameReady += FrameReady;
                player.loopPointReached += Ended;
                player.errorReceived += Failed;
            }
            VideoPath = "Video/RanchTraining/" + monsterId + ".mp4";
            player.targetTexture = texture;
            player.url = Application.streamingAssetsPath.TrimEnd('/') + "/" + VideoPath;
            player.Prepare();
        }

        public void Play()
        {
            if (HasFailed || player == null || requested) return;
            requested = true;
            waitingSeconds = 0;
            if (player.isPrepared) player.Play();
        }

        private void Prepared(VideoPlayer source) { if (requested) source.Play(); }
        private void FrameReady(VideoPlayer source, long frame) { if (requested) started = true; }
        private void Ended(VideoPlayer source)
        {
            if (!requested || HasFailed) return;
            started = true;
            IsComplete = true;
            requested = false;
        }
        private void Failed(VideoPlayer source, string message)
        {
            HasFailed = true;
            requested = false;
            if (image != null) image.color = new Color(1, 1, 1, 0);
            if (player != null) player.Stop();
            Debug.LogWarning("Ranch training video (" + VideoPath + "): " + message, this);
        }

        private void Update()
        {
            if (image != null && texture != null)
            {
                // Letterbox within the existing masked preview without stretching the character.
                var parent = image.transform.parent as RectTransform;
                float width = parent.rect.width;
                float height = parent.rect.height;
                float scale = Mathf.Min(width / 1024f, height / 576f);
                image.rectTransform.offsetMin = new Vector2((width - 1024 * scale) * .5f, (height - 576 * scale) * .5f);
                image.rectTransform.offsetMax = -image.rectTransform.offsetMin;
            }
            if (!requested || player == null) return;
            float delta = Time.unscaledDeltaTime;
            waitingSeconds += delta;
            // Browser backends may advance media time without sending frameReady.
            if (!started && player.isPlaying && player.time > .02) started = true;
            if (!started)
            {
                if (waitingSeconds > 12) Failed(player, "First frame timed out.");
                return;
            }
            visibleSeconds += delta;
            image.color = new Color(1, 1, 1, Mathf.SmoothStep(0, 1, visibleSeconds / .2f));
            stalledSeconds = player.time > previousTime + .001 ? 0 : stalledSeconds + delta;
            previousTime = player.time;
            if (player.length > 0 && player.time >= player.length - .06) Ended(player);
            else if (stalledSeconds > 5 || waitingSeconds > 25) Failed(player, "Playback stalled.");
        }

        public void Shutdown()
        {
            requested = started = false;
            IsComplete = false;
            if (player != null) { player.Stop(); player.targetTexture = null; }
            if (image != null) { image.texture = null; image.gameObject.SetActive(false); }
            if (texture != null) { texture.Release(); Destroy(texture); texture = null; }
        }
        private void OnDisable() => Shutdown();
        private void OnDestroy()
        {
            Shutdown();
            if (player != null)
            {
                player.prepareCompleted -= Prepared;
                player.frameReady -= FrameReady;
                player.loopPointReached -= Ended;
                player.errorReceived -= Failed;
            }
            if (image != null) Destroy(image.gameObject);
        }
    }
}
