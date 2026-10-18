using UnityEngine;

namespace ProjectRun.Race
{
    /// <summary>Only sprites are rendered. No hidden animated model runs behind the billboard.</summary>
    [DefaultExecutionOrder(50)]
    public sealed class RaceDirectionalSpriteView : MonoBehaviour
    {
        public Sys_Race Race;
        public int RunnerId;
        public Data_DirectionalRunSprites SpriteSet;
        public SpriteRenderer Display;
        public Camera RaceCamera;
        public Transform GroundShadow;
        public bool FlatScene;
        public SpriteRenderer IdentityMarker;
        public int DisplayedDirection { get; private set; }
        public int DisplayedFrame { get; private set; }
        public Vector3 Heading { get; private set; }
        private double animationClock;
        private double previousClock;
        private int generation = -1;

        private void LateUpdate()
        {
            if (Race == null || Race.Simulation == null || SpriteSet == null) return;
            var simulation = Race.Simulation;
            var runner = simulation.Runners[RunnerId];
            float interpolation = Mathf.Clamp01(Race.Interpolation);
            float distance = Mathf.Lerp((float)runner.PreviousDistance, (float)runner.Distance, interpolation);
            if (simulation.Phase == E_RacePhase.Result) distance = (float)runner.Distance;
            double clock = System.Math.Max(0, simulation.Time - Data_RaceSimulation.StepSeconds
                + interpolation * Data_RaceSimulation.StepSeconds);
            bool reset = generation != Race.Generation;
            if (reset)
            {
                generation = Race.Generation;
                animationClock = RunnerId * .137;
                previousClock = clock;
            }
            double delta = System.Math.Max(0, clock - previousClock);
            previousClock = clock;
            animationClock += delta * Mathf.Clamp(runner.Speed / Race.Entrants[RunnerId].BaseSpeed, 0, 1.8f);
            float lane = Mathf.Lerp(runner.PreviousLane,runner.Lane,interpolation);
            Vector3 point = simulation.Point(distance, lane);
            transform.position = FlatScene ? Data_Race2DProjection.Project(point) : point;
            Heading = simulation.Forward(distance);
            if (simulation.UsesLaneContests && runner.Speed > .5f)
                Heading = (Heading * runner.Speed + Vector3.Cross(Vector3.up,Heading) * runner.LaneVelocity).normalized;
            float heading;
            if (SpriteSet.ScreenSpaceDirections)
            {
                Vector3 screenHeading = FlatScene ? Data_Race2DProjection.Project(Heading)
                    : RaceCamera != null ? RaceCamera.transform.InverseTransformDirection(Heading) : Heading;
                heading = Mathf.Atan2(screenHeading.x, screenHeading.y) * Mathf.Rad2Deg;
            }
            else
            {
                float cameraYaw = !FlatScene && RaceCamera != null ? RaceCamera.transform.eulerAngles.y : SpriteSet.CaptureYaw;
                heading = Mathf.Atan2(Heading.x, Heading.z) * Mathf.Rad2Deg - cameraYaw + SpriteSet.CaptureYaw;
            }
            DisplayedDirection = SpriteSet.SelectDirection(heading, DisplayedDirection, reset);
            DisplayedFrame = Race.Countdown > 0 || simulation.Phase == E_RacePhase.Result ? 0
                : (int)(animationClock / SpriteSet.ClipDuration * SpriteSet.FrameCount(DisplayedDirection)) % SpriteSet.FrameCount(DisplayedDirection);
            Display.sprite = SpriteSet.Frame(DisplayedDirection, DisplayedFrame);
            if (RaceCamera != null) Display.transform.rotation = RaceCamera.transform.rotation;
            if (GroundShadow != null)
                GroundShadow.localScale = (FlatScene ? SpriteSet.ScreenSpaceDirections ? new Vector3(2.7f, 1f, 1)
                    : new Vector3(3.4f, 1.25f, 1) : new Vector3(2.5f, 1.65f, 1))
                    * (1 + .035f * Mathf.Sin(DisplayedFrame * Mathf.PI * 2 / SpriteSet.FrameCount(DisplayedDirection)));
            if (FlatScene)
            {
                int order = Data_Race2DProjection.SortOrder(transform.position);
                Display.sortingOrder = order;
                if (IdentityMarker != null) IdentityMarker.sortingOrder = order - 1;
                if (GroundShadow != null) GroundShadow.GetComponent<SpriteRenderer>().sortingOrder = order - 2;
            }
        }
    }
}
