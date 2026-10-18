using UnityEngine;

namespace ProjectRun.Race
{
    /// <summary>Compares authored materials without changing race or animation state.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class Sys_RaceLookDev : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Surface
        {
            public Renderer Renderer;
            public Material[] Original;
            public Material[] Updated;
        }

        [Header("All monsters — appearance comparison")]
        [SerializeField] private bool UseUpdatedLook = true;
        [SerializeField] private Surface[] Surfaces = new Surface[0];
        private bool applied;
        private bool previousLook;

        public void Configure(Surface[] surfaces)
        {
            Surfaces = surfaces;
            applied = false;
            ApplyAppearance();
        }

        private void OnEnable() { applied = false; ApplyAppearance(); }
        private void OnValidate() => applied = false;
        private void Update()
        {
            if (!applied || previousLook != UseUpdatedLook) ApplyAppearance();
        }

        [ContextMenu("Show original materials")]
        public void ShowOriginal() { UseUpdatedLook = false; ApplyAppearance(); }

        [ContextMenu("Show updated materials")]
        public void ShowUpdated() { UseUpdatedLook = true; ApplyAppearance(); }

        private void ApplyAppearance()
        {
            foreach (var surface in Surfaces)
            {
                if (surface == null || surface.Renderer == null) continue;
                var materials = UseUpdatedLook ? surface.Updated : surface.Original;
                if (materials != null && materials.Length > 0) surface.Renderer.sharedMaterials = materials;
            }
            previousLook = UseUpdatedLook;
            applied = true;
        }
    }
}
