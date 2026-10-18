using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    [RequireComponent(typeof(Image))]
    public sealed class UIItem_LobbyCurtain : MonoBehaviour
    {
        [SerializeField] private UIItem_LobbyAnimation Animation;
        [SerializeField] private float Period = 5.4f;
        private static readonly int PhaseId = Shader.PropertyToID("_Phase");
        private Image targetImage;
        private Material originalMaterial;
        private Material instanceMaterial;
        private float phase;

        public void Configure(UIItem_LobbyAnimation animation) => Animation = animation;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            targetImage = GetComponent<Image>();
            originalMaterial = targetImage.material;
            instanceMaterial = new Material(originalMaterial);
            targetImage.material = instanceMaterial;
        }

        private void Update()
        {
            if (instanceMaterial == null || (Animation != null && !Animation.IsPlaying)) return;
            phase = Mathf.Repeat(phase + Time.unscaledDeltaTime * Mathf.PI * 2 / Mathf.Max(Period, .1f), Mathf.PI * 2);
            instanceMaterial.SetFloat(PhaseId, phase);
        }

        private void OnDisable()
        {
            if (instanceMaterial == null) return;
            if (targetImage != null) targetImage.material = originalMaterial;
            Destroy(instanceMaterial);
            instanceMaterial = null;
        }
    }
}
