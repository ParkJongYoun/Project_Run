using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectRun.Race
{
    public sealed class UIItem_LobbyDissolve : MonoBehaviour
    {
        public int Current { get; private set; } = -1;
        public bool IsTransitioning { get; private set; }
        public bool IsReady { get; private set; }
        public bool HasPreparationFailed { get; private set; }
        private CanvasGroup currentLayer;
        private float nextChange;
        private bool awaitingPresentation;
        private readonly CanvasGroup[] layers = new CanvasGroup[3];
        private readonly UIItem_LobbyAnimation[][] animations = new UIItem_LobbyAnimation[3][];
        private static System.Random random = new System.Random(System.Guid.NewGuid().GetHashCode());
        private static int lastShown = -1;
        private readonly int[] shuffle = { 0, 1, 2 };
        private int shuffleIndex = 3;
        private readonly string[] paths = { "Prefabs/Lobby/P_Lobby_WildRose", "Prefabs/Lobby/P_Lobby_Minorous", "Prefabs/Lobby/P_Lobby_Matyr" };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            random = new System.Random(System.Guid.NewGuid().GetHashCode());
            lastShown = -1;
        }
        private void OnEnable()
        {
            if (!IsReady) StartCoroutine(Prepare());
            else
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    layers[i].alpha = i == Current ? 1 : 0;
                    SetPlaying(i, i == Current);
                }
                ScheduleNext();
            }
        }
        private void OnDisable() { StopAllCoroutines(); IsTransitioning = false; }
        private void Update()
        {
            if (!IsReady || (Mgr_Game.Instance != null && Mgr_Game.Instance.IsTransitioning)) return;
            if (awaitingPresentation)
            {
                awaitingPresentation = false;
                SetPlaying(Current, true);
                ScheduleNext();
            }
            if (!IsTransitioning && Time.unscaledTime >= nextChange && Mgr_User.Data.MotionEnabled) Next();
        }
        public void Next()
        {
            if (IsReady && !IsTransitioning && !awaitingPresentation) StartCoroutine(Change(PickNext()));
        }
        private IEnumerator Prepare()
        {
            IsTransitioning = true;
            HasPreparationFailed = false;
            // A serialized lobby panel replaces its illustration root in OnOpen.
            // Defer work until that obsolete root has been removed.
            yield return null;
            var textures = new HashSet<Texture>();
            var sprites = new HashSet<Sprite>();
            var clips = new HashSet<Data_LobbyAnimation>();
            var target = RenderTexture.GetTemporary(1, 1, 0);
            bool prepared = false;
            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    if (layers[i] == null)
                    {
                        var request = Resources.LoadAsync<GameObject>(paths[i]);
                        yield return request;
                        var prefab = request.asset as GameObject;
                        if (prefab == null || prefab.GetComponent<CanvasGroup>() == null)
                        {
                            Debug.LogError("Missing lobby composition: " + paths[i]);
                            HasPreparationFailed = true;
                            IsTransitioning = false;
                            yield break;
                        }
                        layers[i] = Instantiate(prefab, transform).GetComponent<CanvasGroup>();
                    }
                    var layer = layers[i];
                    layer.alpha = 0;
                    layer.interactable = false;
                    layer.blocksRaycasts = false;
                    animations[i] = layer.GetComponentsInChildren<UIItem_LobbyAnimation>(true);
                    SetPlaying(i, false);
                    foreach (var animation in animations[i])
                    {
                        if (animation.IdleClip != null) clips.Add(animation.IdleClip);
                        if (animation.CheerClip != null) clips.Add(animation.CheerClip);
                    }
                    foreach (var image in layer.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                        WarmSprite(image.sprite, sprites, textures, target);
                    yield return null;
                }
                // Loading the prefab only resolves its initial images. Later animation frames
                // can still trigger synchronous file reads unless every referenced sprite is touched.
                int batch = 0;
                foreach (var clip in clips)
                {
                    if (clip.Frames == null) continue;
                    foreach (var sprite in clip.Frames)
                    {
                        WarmSprite(sprite, sprites, textures, target);
                        if (++batch % 24 == 0) yield return null;
                    }
                }
                var art = DemoUI.Art;
                if (art != null && art.Images != null)
                    foreach (var entry in art.Images)
                    {
                        WarmSprite(entry.Sprite, sprites, textures, target);
                        if (++batch % 24 == 0) yield return null;
                    }
                // Prime each composition's actual UI materials and geometry behind the scene cover.
                bool covered = Mgr_Game.Instance != null && Mgr_Game.Instance.IsTransitioning;
                if (covered)
                    foreach (var layer in layers)
                    {
                        layer.alpha = 1;
                        Canvas.ForceUpdateCanvases();
                        yield return null;
                        yield return null;
                        layer.alpha = 0;
                    }
                prepared = true;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);
                if (!prepared) { HasPreparationFailed = true; IsTransitioning = false; }
            }
            int first = PickNext();
            currentLayer = layers[first];
            currentLayer.transform.SetAsLastSibling();
            currentLayer.alpha = 1;
            Current = first;
            lastShown = first;
            awaitingPresentation = true;
            IsReady = true;
            IsTransitioning = false;
        }
        private static void WarmSprite(Sprite sprite, HashSet<Sprite> sprites, HashSet<Texture> textures, RenderTexture target)
        {
            if (sprite == null || !sprites.Add(sprite)) return;
            var rect = sprite.rect;
            var texture = sprite.texture;
            if (rect.width <= 0 || texture == null || !textures.Add(texture)) return;
            // Issue a GPU use while black, without making a CPU-readable texture copy.
            var previous = RenderTexture.active;
            try { Graphics.Blit(texture, target); }
            finally { RenderTexture.active = previous; }
        }
        private void SetPlaying(int index, bool playing)
        {
            if (animations[index] == null) return;
            foreach (var animation in animations[index]) animation.SetPlaying(playing);
        }
        private void ScheduleNext()
        {
            nextChange = Time.unscaledTime + Mathf.Lerp(Mgr_Table.Demo.LobbyMinimumSeconds, Mgr_Table.Demo.LobbyMaximumSeconds, (float)random.NextDouble());
        }
        private int PickNext()
        {
            if (shuffleIndex >= shuffle.Length)
            {
                for (int i = shuffle.Length - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    int value = shuffle[i]; shuffle[i] = shuffle[j]; shuffle[j] = value;
                }
                int previous = Current >= 0 ? Current : lastShown;
                if (shuffle[0] == previous)
                {
                    int j = random.Next(1, shuffle.Length);
                    int value = shuffle[0]; shuffle[0] = shuffle[j]; shuffle[j] = value;
                }
                shuffleIndex = 0;
            }
            return shuffle[shuffleIndex++];
        }
        private IEnumerator Change(int id)
        {
            IsTransitioning = true;
            var next = layers[id];
            next.transform.SetAsLastSibling();
            SetPlaying(id, true);
            next.alpha = currentLayer == null ? 1 : 0;
            next.interactable = false; next.blocksRaycasts = false;
            if (currentLayer != null)
            {
                float duration = Mgr_Table.Demo.DissolveSeconds;
                for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
                {
                    float a = Mathf.SmoothStep(0, 1, t / duration);
                    // Both complete compositions remain visible throughout the dissolve.
                    next.alpha = a; currentLayer.alpha = 1 - a;
                    yield return null;
                }
                currentLayer.alpha = 0;
                SetPlaying(Current, false);
            }
            next.alpha = 1; currentLayer = next; Current = id; lastShown = id;
            ScheduleNext();
            IsTransitioning = false;
        }
    }
}
