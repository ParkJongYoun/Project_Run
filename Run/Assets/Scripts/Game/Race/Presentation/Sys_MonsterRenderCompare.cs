using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ProjectRun.Race
{
    /// <summary>Presentation-only comparison: one clock, three identical monsters, no race scoring.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class Sys_MonsterRenderCompare : MonoBehaviour
    {
        public RaceRunnerView[] Views;
        public Camera ViewCamera;
        public float LaneSpacing = 2.8f;
        public float RunSpeed = 10;
        public float StartDistance = 80;
        public bool Paused { get; private set; }
        public bool SlowMotion { get; private set; }
        public bool Angled { get; private set; }
        public double Elapsed { get; private set; }
        private PlayableGraph[] graphs;
        private AnimationClipPlayable[] clips;

        private void Start()
        {
            graphs = new PlayableGraph[Views.Length];
            clips = new AnimationClipPlayable[Views.Length];
            for (int i = 0; i < Views.Length; i++)
            {
                var view = Views[i];
                view.enabled = false;
                view.MountAnimator.applyRootMotion = false;
                view.MountAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graphs[i] = PlayableGraph.Create("MonsterCompare_" + i);
                graphs[i].SetTimeUpdateMode(DirectorUpdateMode.Manual);
                clips[i] = AnimationClipPlayable.Create(graphs[i], view.RunClip);
                var output = AnimationPlayableOutput.Create(graphs[i], "mount", view.MountAnimator);
                output.SetSourcePlayable(clips[i]);
                graphs[i].Play();
                view.GetComponent<Sys_MonsterLook>().Apply((E_MonsterRenderStyle)i);
            }
            ApplyPose();
        }

        private void LateUpdate()
        {
            if (!Paused) Elapsed += Time.unscaledDeltaTime * (SlowMotion ? .5 : 1);
            ApplyPose();
        }

        public void TogglePause() => Paused = !Paused;
        public void ToggleSpeed() => SlowMotion = !SlowMotion;
        public void ToggleAngle() { Angled = !Angled; ApplyLayout(); }
        public void ResetRun() { Elapsed = 0; ApplyPose(); }

        private void ApplyPose()
        {
            if (graphs == null) return;
            double clipTime = .2 + Elapsed * RunSpeed / Mathf.Max(.1f, Views[0].RunReferenceSpeed);
            for (int i = 0; i < Views.Length; i++)
            {
                clips[i].SetTime(clipTime);
                graphs[i].Evaluate(0);
                Views[i].MountRoot.localPosition = Vector3.zero;
                Views[i].MountRoot.localRotation = Quaternion.identity;
            }
            ApplyLayout();
        }

        public void ApplyLayout()
        {
            if (Views == null || Views.Length != 3 || ViewCamera == null) return;
            float distance = StartDistance + (float)(Elapsed * RunSpeed);
            Vector3 center = Data_RaceSimulation.CoursePoint(distance);
            Vector3 forward = Data_RaceSimulation.CourseForward(distance);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < Views.Length; i++)
                Views[i].transform.SetPositionAndRotation(center + right * ((1 - i) * LaneSpacing), Quaternion.LookRotation(forward));
            // Preserve the same world width on narrow portrait screens and on desktop.
            float halfWidth = LaneSpacing + 1.8f;
            float cameraDistance = halfWidth / (Mathf.Tan(ViewCamera.fieldOfView * .5f * Mathf.Deg2Rad) * Mathf.Min(.8f, ViewCamera.aspect));
            Vector3 focus = center + Vector3.up * 1.6f;
            Vector3 direction = Quaternion.AngleAxis(Angled ? 22 : 0, Vector3.up) * forward;
            ViewCamera.transform.position = focus + direction * cameraDistance + Vector3.up * (cameraDistance * .2f);
            ViewCamera.transform.LookAt(focus);
        }

        private void OnDestroy()
        {
            if (graphs == null) return;
            foreach (var graph in graphs) if (graph.IsValid()) graph.Destroy();
        }
    }
}
