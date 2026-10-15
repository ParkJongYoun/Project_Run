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
        private AudioSource[] footsteps;
        private AudioSource[] accents;
        private int footIndex, accentIndex;
        private AudioSource ambience;
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            footsteps = CreateVoices(8);
            accents = CreateVoices(4);
            ambience = gameObject.AddComponent<AudioSource>(); ambience.playOnAwake = false;
            ambience.clip = Wind; ambience.loop = true; ambience.volume = 0;
            if (Wind != null) ambience.Play();
        }
        public static void PlayAccent(bool burst)
        {
            if (instance == null) return;
            var clip = burst ? instance.Burst : instance.Chime;
            instance.PlayVoice(instance.accents, ref instance.accentIndex, clip, burst ? 0.42f : 0.38f);
        }
        public static void PlayFoot(float volume)
        {
            if (instance != null) instance.PlayVoice(instance.footsteps, ref instance.footIndex, instance.Hoof, volume * 0.27f);
        }
        public static void SetWind(float volume)
        {
            if (instance != null) instance.ambience.volume = Mathf.Clamp01(volume) * 0.14f;
        }
        private AudioSource[] CreateVoices(int count)
        {
            var voices = new AudioSource[count];
            for (int i = 0; i < count; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }
            return voices;
        }
        private void PlayVoice(AudioSource[] voices, ref int index, AudioClip clip, float volume)
        {
            if (clip == null || volume <= 0) return;
            var voice = voices[index];
            index = (index + 1) % voices.Length;
            // Web audio can remain suspended until the first user gesture. Reuse a bounded
            // voice instead of accumulating PlayOneShot sources while its clock is stopped.
            voice.Stop();
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(volume);
            voice.Play();
        }
        private void OnDestroy() { if (instance == this) instance = null; }
    }
}
