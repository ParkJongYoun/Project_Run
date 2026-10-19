using UnityEngine;

namespace ProjectRun.Race
{
    [DefaultExecutionOrder(-200)]
    public sealed class Sys_Race : MonoBehaviour
    {
        public Data_RunnerSetup[] Entrants = new Data_RunnerSetup[6];
        public Data_RaceCourse Course;
        [Min(1)] public int LapCount = 1;
        public float CountdownSeconds = 4;
        public float PlaybackSpeed = 1;
        public bool Paused;
        [Tooltip("Enable deterministic blocking, gap selection and overtaking for this scene.")]
        public bool EnableLaneContests;
        [Tooltip("Built-in oval only (Course empty): meters added to each end of both straights. 0 = original 180m straights.")]
        [Min(0)] public float StraightExtension;
        [Tooltip("After the result, runners keep running past the line and slow to a stop (3D runner views).")]
        public bool CoolDownAfterFinish = false;
        [Tooltip("Deceleration (m/s²) of the post-result cool-down run. Lower = runs longer.")]
        [Min(0.1f)] public float CoolDownDeceleration = 2.5f;
        [Tooltip("Speed (m/s) runners already have the instant the race begins. 0 = standing start (original).")]
        [Min(0)] public float StartLaunchSpeed = 0f;
        [Tooltip("Acceleration (m/s²) during the first 3 race seconds. 6 = original.")]
        [Min(0.1f)] public float StartAcceleration = 6f;
        public Data_RaceSimulation Simulation { get; private set; }
        public float Countdown { get; private set; }
        public int Generation { get; private set; }
        public bool IsSkipping { get; private set; }
        public bool PresentationHeld => presentationOwner != null;
        public float Interpolation => (float)(accumulator / Data_RaceSimulation.StepSeconds);
        private Object presentationOwner;
        private double accumulator;
        private bool firstFrame = true;

        private void Awake() { Application.runInBackground = true; Data_RaceSimulation.StraightExtension = StraightExtension; Sys_DemoRace.ConfigureRace(this); RestartRace(); }
        public void RestartRace()
        {
            Data_RaceSimulation.StraightExtension = StraightExtension;
            Data_RaceSimulation.StartLaunchSpeed = StartLaunchSpeed;
            Data_RaceSimulation.StartAcceleration = StartAcceleration;
            if (Simulation != null) Simulation.Changed -= Publish;
            Simulation = new Data_RaceSimulation(Entrants, Course, EnableLaneContests, LapCount);
            Simulation.Changed += Publish;
            Countdown = CountdownSeconds;
            accumulator = 0;
            Paused = false;
            presentationOwner = null;
            Generation++;
            Mgr_Event.RaceReset();
        }
        private void Publish(Data_RaceEvent value) => Mgr_Event.Publish(value);
        private void Update()
        {
            // Editor/domain initialization can report a very large first unscaled delta.
            if (firstFrame) { firstFrame = false; return; }
            if (Paused || PresentationHeld) return;
            double delta = Time.unscaledDeltaTime * PlaybackSpeed;
            if (Countdown > 0)
            {
                Countdown -= (float)delta;
                if (Countdown > 0) return;
                delta = -Countdown;
                Countdown = 0;
                Simulation.Begin();
            }
            accumulator += delta;
            int steps = 0;
            // Keep the remaining accumulator on a slow frame; never drop simulation time.
            while (accumulator >= Data_RaceSimulation.StepSeconds && steps++ < 240 && !PresentationHeld)
            {
                if (Simulation.Phase == E_RacePhase.Result)
                {
                    if (!CoolDownAfterFinish || !Simulation.CoolingDown) { accumulator = 0; break; }
                    Simulation.CoolDownStep(CoolDownDeceleration);
                }
                else Simulation.Step();
                accumulator -= Data_RaceSimulation.StepSeconds;
            }
        }
        public void SkipToResult()
        {
            IsSkipping = true;
            try
            {
                Mgr_Event.RaceReset();
                presentationOwner = null;
                Countdown = 0;
                Simulation.Begin();
                for (int i = 0; i < 18000 && Simulation.Phase != E_RacePhase.Result; i++) Simulation.Step();
                accumulator = 0;
            }
            finally { IsSkipping = false; Mgr_Event.RaceReset(); }
        }
        public bool AcquirePresentationHold(Object owner)
        {
            if (owner == null || IsSkipping || (presentationOwner != null && presentationOwner != owner)) return false;
            presentationOwner = owner;
            return true;
        }
        public void ReleasePresentationHold(Object owner)
        {
            if (presentationOwner == owner) presentationOwner = null;
        }
        private void OnDestroy() { if (Simulation != null) Simulation.Changed -= Publish; }
    }
}
