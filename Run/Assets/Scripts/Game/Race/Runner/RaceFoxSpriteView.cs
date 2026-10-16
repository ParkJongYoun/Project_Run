using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRun.Race
{
    /// <summary>Two illustrated poses for each authored camera shot. Simulation and camera targets stay on the original runner.</summary>
    [DefaultExecutionOrder(300), DisallowMultipleComponent]
    public sealed class RaceFoxSpriteView : MonoBehaviour
    {
        [Header("Camera-specific fox experiment")]
        public bool UseSprites = true;
        public Data_FoxSpriteSet SpriteSet;
        public RaceRunnerView Runner;
        public Sys_Race Race;
        public Sys_RaceCamera Director;
        [Tooltip("Keep the original model's shadow while hiding its visible surfaces.")]
        public bool KeepModelShadows = true;
        public E_RaceShot DisplayedShot { get; private set; }
        public int DisplayedFrame { get; private set; }
        public bool IsShowingSprites { get; private set; }
        public SpriteRenderer Display => display;
        private SpriteRenderer display;
        private SkinnedMeshRenderer[] originals;
        private ShadowCastingMode[] shadowModes;
        private bool[] enabledStates;
        private bool warned;

        private void Awake()
        {
            if (Runner == null) Runner = GetComponent<RaceRunnerView>();
            originals = Runner.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            shadowModes = new ShadowCastingMode[originals.Length]; enabledStates = new bool[originals.Length];
            for (int i = 0; i < originals.Length; i++) { shadowModes[i] = originals[i].shadowCastingMode; enabledStates[i] = originals[i].enabled; }
            var child = new GameObject("obj_fox_camera_sprite"); child.transform.SetParent(transform, false);
            display = child.AddComponent<SpriteRenderer>(); display.enabled = false;
            display.shadowCastingMode = ShadowCastingMode.Off; display.receiveShadows = false;
            if (SpriteSet != null) display.sharedMaterial = SpriteSet.SpriteMaterial;
        }

        private void LateUpdate()
        {
            if (Runner == null || !Runner.HasPose || Director == null || Race == null || display == null) return;
            var frames = SpriteSet == null ? null : SpriteSet.Find(Director.CurrentShot);
            bool valid = frames != null && SpriteSet.SpriteMaterial != null;
            if (UseSprites && !valid && !warned)
            {
                Debug.LogError("Missing fox sprite pair/material for " + Director.CurrentShot + "; showing original model.", this); warned = true;
            }
            SetVisibility(UseSprites && valid);
            if (!IsShowingSprites) return;
            DisplayedShot = frames.Shot;
            // Nearest captured phase: stable across pause, playback speed, shot changes and restart.
            float phase = Runner.GaitPhase;
            float left = Mathf.Abs(Mathf.DeltaAngle(phase * 360, frames.LeftPhase * 360));
            float right = Mathf.Abs(Mathf.DeltaAngle(phase * 360, frames.RightPhase * 360));
            if (Race.Countdown > 0 || Race.Simulation.Phase == E_RacePhase.Result) DisplayedFrame = 1;
            else DisplayedFrame = left <= right ? 0 : 1;
            display.sprite = DisplayedFrame == 0 ? frames.Left : frames.Right;
            display.transform.SetPositionAndRotation(transform.position + Vector3.up * frames.AnchorHeight, Director.RaceCamera.transform.rotation);
            display.transform.localScale = Vector3.one;
        }

        private void SetVisibility(bool show)
        {
            if (display != null) display.enabled = show;
            if (originals == null) return;
            for (int i = 0; i < originals.Length; i++)
            {
                if (originals[i] == null) continue;
                originals[i].enabled = show ? KeepModelShadows && enabledStates[i] : enabledStates[i];
                originals[i].shadowCastingMode = show ? ShadowCastingMode.ShadowsOnly : shadowModes[i];
            }
            IsShowingSprites = show;
        }

        [ContextMenu("Toggle 2D / 3D comparison")]
        public void ToggleComparison() { UseSprites = !UseSprites; }
        private void OnDisable() => SetVisibility(false);
    }
}
