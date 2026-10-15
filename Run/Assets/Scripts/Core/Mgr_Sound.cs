using UnityEngine;
namespace ProjectRun.Race
{
    public sealed class Mgr_Sound : MonoBehaviour
    {
        public AudioClip Hoof;
        public AudioClip Burst;
        public AudioClip Chime;
        public AudioClip Wind;
        private static Mgr_Sound instance;
        private AudioSource effects;
        private AudioSource ambience;
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            effects = gameObject.AddComponent<AudioSource>(); effects.playOnAwake = false;
            ambience = gameObject.AddComponent<AudioSource>(); ambience.playOnAwake = false;
            ambience.clip = Wind; ambience.loop = true; ambience.volume = 0;
            if (Wind != null) ambience.Play();
        }
        public static void PlayAccent(bool burst)
        {
            if (instance == null) return;
            var clip = burst ? instance.Burst : instance.Chime;
            if (clip != null) instance.effects.PlayOneShot(clip, burst ? 0.42f : 0.38f);
        }
        public static void PlayFoot(float volume)
        {
            if (instance != null && instance.Hoof != null) instance.effects.PlayOneShot(instance.Hoof, volume * 0.27f);
        }
        public static void SetWind(float volume)
        {
            if (instance != null) instance.ambience.volume = Mathf.Clamp01(volume) * 0.14f;
        }
        private void OnDestroy() { if (instance == this) instance = null; }
    }
}
