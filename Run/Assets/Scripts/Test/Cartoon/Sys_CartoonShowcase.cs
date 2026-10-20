using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ProjectRun.Race
{
    [Serializable]
    public sealed class Data_CartoonExhibit
    {
        public string Name;
        public Transform Root;
        public Animator Animator;
        public AnimationClip Idle;
        public AnimationClip Run;
        public AnimationClip Walk;
        public AnimationClip Clap;
        public bool KeepAppearance;
        public Vector3 Focus;
        public float Radius;
        public Transform CartoonRoot;
        public GameObject OriginalPrefab;
        public AnimationClip OriginalIdle;
        public AnimationClip OriginalRun;
        public AnimationClip OriginalWalk;
        public AnimationClip OriginalClap;
        public Vector3 OriginalPosition;
        public Quaternion OriginalRotation = Quaternion.identity;
        public Vector3 OriginalScale = Vector3.one;
        [NonSerialized] public GameObject OriginalInstance;
        [NonSerialized] public Animator OriginalAnimator;
    }

    /// <summary>Presentation-only animation and framing for the cartoon test scene.</summary>
    public sealed class Sys_CartoonShowcase : MonoBehaviour
    {
        public Data_CartoonExhibit[] Exhibits = Array.Empty<Data_CartoonExhibit>();
        public Camera PreviewCamera;
        public RectTransform SafeArea;
        public UnityEngine.UI.Text Status;
        public GameObject[] NameLabels = Array.Empty<GameObject>();
        public Vector3 OverviewPosition;
        public Vector3 OverviewEuler;
        public float OverviewSize = 11;
        private PlayableGraph[] graphs;
        private int selected = -1;
        private enum PreviewMotion { Idle, Run, Walk, Clap }
        private PreviewMotion motion;
        private bool showOriginal;
        private bool comparingMinotaurus;
        private double animationTime;
        public bool ShowingOriginal => showOriginal;

        private void OnEnable() { if (Application.isPlaying) PlayIdle(); }
        private void OnDisable() { StopGraphs(); }

        private void Update()
        {
            if (SafeArea != null && Screen.width > 0 && Screen.height > 0)
            {
                Rect safe = Screen.safeArea;
                SafeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
                SafeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
                SafeArea.offsetMin = SafeArea.offsetMax = Vector2.zero;
            }
            if (graphs == null) return;
            animationTime += Time.unscaledDeltaTime;
            foreach (var graph in graphs) if (graph.IsValid()) graph.Evaluate(Time.unscaledDeltaTime);
        }

        public void PlayIdle() { Play(PreviewMotion.Idle); }
        public void PlayRun() { Play(PreviewMotion.Run); }
        public void PlayWalk() { Play(PreviewMotion.Walk); }
        public void PlayClap() { Play(PreviewMotion.Clap); }

        private void Play(PreviewMotion value)
        {
            motion = value; animationTime = 0;
            RebuildAnimation();
        }

        private void RebuildAnimation()
        {
            StopGraphs();
            graphs = new PlayableGraph[Exhibits.Length];
            for (int i = 0; i < Exhibits.Length; i++)
            {
                var entry = Exhibits[i];
                bool original = showOriginal && !entry.KeepAppearance;
                var animator = original ? entry.OriginalAnimator : entry.Animator;
                var clip = original ? (motion == PreviewMotion.Run ? entry.OriginalRun
                    : motion == PreviewMotion.Walk ? (entry.OriginalWalk != null ? entry.OriginalWalk : entry.OriginalIdle)
                    : motion == PreviewMotion.Clap ? (entry.OriginalClap != null ? entry.OriginalClap : entry.OriginalIdle) : entry.OriginalIdle)
                    : motion == PreviewMotion.Run ? entry.Run
                    : motion == PreviewMotion.Walk ? (entry.Walk != null ? entry.Walk : entry.Idle)
                    : motion == PreviewMotion.Clap ? (entry.Clap != null ? entry.Clap : entry.Idle) : entry.Idle;
                if (animator == null || clip == null) continue;
                // This preview owns the pose. A prefab's Idle controller would otherwise
                // overwrite the manually evaluated clip later in the same frame.
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                // Legacy sound callbacks belong to the original game, not this visual preview.
                animator.fireEvents = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var graph = PlayableGraph.Create("Cartoon_" + entry.Name);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
                playable.SetTime(animationTime);
                var output = AnimationPlayableOutput.Create(graph, "Preview", animator);
                output.SetSourcePlayable(playable);
                graph.Play(); graph.Evaluate(0); graphs[i] = graph;
            }
            UpdateStatus();
        }

        public void ToggleAppearance()
        {
            if (!showOriginal)
            {
                foreach (var entry in Exhibits)
                    if (!entry.KeepAppearance)
                    if (entry.OriginalPrefab == null || entry.CartoonRoot == null || entry.OriginalIdle == null || entry.OriginalRun == null)
                    { Debug.LogError("Missing original comparison source: " + entry.Name, this); return; }
                foreach (var entry in Exhibits) if (!entry.KeepAppearance) PrepareOriginal(entry);
            }
            StopGraphs();
            showOriginal = !showOriginal;
            foreach (var entry in Exhibits)
            {
                if (entry.KeepAppearance) continue;
                entry.CartoonRoot.gameObject.SetActive(!showOriginal);
                if (entry.OriginalInstance != null) entry.OriginalInstance.SetActive(showOriginal);
            }
            RebuildAnimation();
        }

        private static void PrepareOriginal(Data_CartoonExhibit entry)
        {
            if (entry.OriginalInstance != null) return;
            var container = new GameObject("obj_original");
            container.SetActive(false);
            container.transform.SetParent(entry.Root, false);
            var model = Instantiate(entry.OriginalPrefab, container.transform, false);
            model.name = "obj_original_model";
            model.transform.SetLocalPositionAndRotation(entry.OriginalPosition, entry.OriginalRotation);
            model.transform.localScale = entry.OriginalScale;
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                if (renderer.name == "shadow") renderer.gameObject.SetActive(false);
            var animator = model.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.fireEvents = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            entry.OriginalInstance = container;
            entry.OriginalAnimator = animator;
        }

        private void StopGraphs()
        {
            if (graphs == null) return;
            foreach (var graph in graphs) if (graph.IsValid()) graph.Destroy();
            graphs = null;
        }

        public void ShowAll()
        {
            comparingMinotaurus = false;
            selected = -1;
            foreach (var entry in Exhibits) entry.Root.gameObject.SetActive(true);
            foreach (var label in NameLabels) label.SetActive(true);
            PreviewCamera.transform.SetPositionAndRotation(OverviewPosition, Quaternion.Euler(OverviewEuler));
            PreviewCamera.orthographicSize = OverviewSize;
            UpdateStatus();
        }
        public void CompareMinotaurus()
        {
            Data_CartoonExhibit old = null, added = null;
            foreach (var entry in Exhibits)
            {
                if (entry.Root.name == "V_Cartoon_Minorous") old = entry;
                if (entry.Root.name == "V_Cartoon_NewMinotaurus") added = entry;
            }
            if (old == null || added == null) return;
            selected = -1;
            comparingMinotaurus = true;
            foreach (var entry in Exhibits) entry.Root.gameObject.SetActive(entry == old || entry == added);
            foreach (var label in NameLabels)
                label.SetActive(label.name == "txt_name_Minorous" || label.name == "txt_name_NewMinotaurus");
            Vector3 center = (old.Focus + added.Focus) * .5f;
            PreviewCamera.transform.position = center + new Vector3(0, 1.5f, -10);
            PreviewCamera.transform.LookAt(center);
            PreviewCamera.orthographicSize = Mathf.Max(3.6f, Mathf.Max(old.Radius, added.Radius) * 1.4f);
            UpdateStatus();
        }
        public void Next() { Focus((selected + 1) % Exhibits.Length); }
        public void Previous() { Focus(selected < 0 ? Exhibits.Length - 1 : (selected + Exhibits.Length - 1) % Exhibits.Length); }
        public void Rotate()
        {
            if (selected < 0) return;
            Exhibits[selected].Root.Rotate(0, 45, 0, Space.World);
        }
        public void Focus(int index)
        {
            if (index < 0 || index >= Exhibits.Length) return;
            selected = index;
            comparingMinotaurus = false;
            for (int i = 0; i < Exhibits.Length; i++) Exhibits[i].Root.gameObject.SetActive(i == index);
            foreach (var label in NameLabels) label.SetActive(false);
            var entry = Exhibits[index];
            Vector3 center = entry.Focus;
            PreviewCamera.transform.position = center + new Vector3(0, 2, -8);
            PreviewCamera.transform.LookAt(center);
            PreviewCamera.orthographicSize = Mathf.Max(2.25f, entry.Radius * 1.65f);
            UpdateStatus();
        }
        private void UpdateStatus()
        {
            if (Status != null) Status.text = (comparingMinotaurus ? "미노타우루스 · 기존 / 신규 비교"
                : selected < 0 ? "전체 " + Exhibits.Length + "종" : Exhibits[selected].Name)
                + "  ·  " + (showOriginal ? "원본" : "카툰") + "  ·  "
                + (motion == PreviewMotion.Run ? "달리기" : motion == PreviewMotion.Walk ? "걷기 (신규 미노)"
                    : motion == PreviewMotion.Clap ? "박수 (신규 미노)" : "대기");
        }
        public void ReturnToTitle() { Mgr_Scene.Load(Define_Scene.Title); }
    }
}
