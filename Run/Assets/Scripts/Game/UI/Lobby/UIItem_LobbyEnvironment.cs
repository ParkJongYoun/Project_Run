using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    [RequireComponent(typeof(Image))]
    public sealed class UIItem_LobbyEnvironment : MonoBehaviour
    {
        [SerializeField] private UIItem_LobbyAnimation Animation;
        [Header("Movement in the original illustration's pixels")]
        [SerializeField, Range(0, 40)] private float CurtainPixels = 20;
        [SerializeField, Range(0, 20)] private float FlowerPixels = 8;
        [SerializeField, Range(0, 30)] private float LeafPixels = 14;
        [SerializeField, Range(.1f, 3)] private float Speed = 1;
        private static readonly int ClockId = Shader.PropertyToID("_Clock");
        private Image targetImage;
        private Material sourceMaterial;
        private Material runtimeMaterial;
        private float elapsed;

        public float Elapsed => elapsed;
        public void Configure(UIItem_LobbyAnimation animation) => Animation = animation;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            targetImage = GetComponent<Image>();
            sourceMaterial = targetImage.material;
            runtimeMaterial = new Material(sourceMaterial);
            targetImage.material = runtimeMaterial;
            ApplyParameters();
        }

        private void Update()
        {
            if (runtimeMaterial == null || (Animation != null && !Animation.IsPlaying)) return;
            elapsed += Time.unscaledDeltaTime * Speed;
            ApplyParameters();
        }

        private void ApplyParameters()
        {
            runtimeMaterial.SetFloat(ClockId, elapsed);
            runtimeMaterial.SetVector("_MovementPixels", new Vector4(CurtainPixels, FlowerPixels, LeafPixels, 0));
        }

        private void OnDisable()
        {
            if (runtimeMaterial == null) return;
            if (targetImage != null) targetImage.material = sourceMaterial;
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }
    }
}
