using UnityEngine;
namespace ProjectRun.Race
{
    public sealed class Mgr_Sound : MonoBehaviour
    {
        public AudioClip Hoof;
        public AudioClip Burst;
        public AudioClip Chime;
        public AudioClip Wind;
        // The entire experience is silent. Keep serialized source references and call
        // signatures for existing scenes/builders, but never create or play audio voices.
        public static void PlayAccent(bool burst) { }
        public static void PlayFoot(float volume) { }
        public static void SetWind(float volume) { }
    }
}
