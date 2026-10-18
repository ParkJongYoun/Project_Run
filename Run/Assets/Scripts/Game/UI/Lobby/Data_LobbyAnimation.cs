using UnityEngine;

namespace ProjectRun.Race
{
    [CreateAssetMenu(menuName = "Project Run/Lobby Animation")]
    public sealed class Data_LobbyAnimation : ScriptableObject
    {
        public Sprite[] Frames;
        public float[] Durations;
        [Tooltip("Original illustration coordinates; all frames share this rectangle.")]
        public Rect IllustrationRect;
        public Vector2 IllustrationSize = new Vector2(1672, 941);
        public string SourceVideoSha256;

        public float Duration
        {
            get
            {
                float duration = 0;
                if (Durations != null)
                    foreach (float value in Durations) duration += Mathf.Max(value, .001f);
                return duration;
            }
        }
    }
}
