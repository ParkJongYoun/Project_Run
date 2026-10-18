using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ProjectRun.Race
{
    /// <summary>Isolated material preview. Does not run or modify the race simulation.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class Sys_MinorousLookDev : MonoBehaviour
    {
        [Header("Appearance comparison")]
        [SerializeField] private Renderer Surface;
        [SerializeField] private Material OriginalMaterial;
        [SerializeField] private Material LookDevMaterial;
        [SerializeField] private Renderer Outline;
        [SerializeField] private Material OriginalOutline;
        [SerializeField] private Material LookDevOutline;
        [SerializeField] private bool UseUpdatedLook = true;

        [Header("Preview animation (Play Mode)")]
        [Tooltip("Disable in a race scene: RaceRunnerView owns the running animation there.")]
        [SerializeField] private bool PlayIdleAnimation = true;
        [SerializeField] private Animator TargetAnimator;
        [SerializeField] private AnimationClip IdleClip;
        [SerializeField, Range(0, 2)] private float PlaybackSpeed = 1;

        private PlayableGraph graph;
        private AnimationClipPlayable idle;

        private void OnEnable()
        {
            ApplyAppearance();
            if (!Application.isPlaying || !PlayIdleAnimation || TargetAnimator == null || IdleClip == null) return;
            TargetAnimator.applyRootMotion = false;
            TargetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Minorous look preview");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            idle = AnimationClipPlayable.Create(graph, IdleClip);
            idle.SetApplyFootIK(false);
            idle.SetApplyPlayableIK(false);
            var output = AnimationPlayableOutput.Create(graph, "Idle", TargetAnimator);
            output.SetSourcePlayable(idle);
            graph.Play();
        }

        private void Update()
        {
            ApplyAppearance();
            if (!Application.isPlaying || !graph.IsValid()) return;
            idle.SetSpeed(PlaybackSpeed);
            graph.Evaluate(Time.deltaTime);
        }

        private void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        [ContextMenu("Show original material")]
        public void ShowOriginal() { UseUpdatedLook = false; ApplyAppearance(); }

        [ContextMenu("Show new material")]
        public void ShowUpdated() { UseUpdatedLook = true; ApplyAppearance(); }

        private void ApplyAppearance()
        {
            var material = UseUpdatedLook ? LookDevMaterial : OriginalMaterial;
            if (Surface != null && material != null && Surface.sharedMaterial != material)
                Surface.sharedMaterial = material;
            var outlineMaterial = UseUpdatedLook ? LookDevOutline : OriginalOutline;
            if (Outline != null && outlineMaterial != null && Outline.sharedMaterial != outlineMaterial)
                Outline.sharedMaterial = outlineMaterial;
        }
    }
}
