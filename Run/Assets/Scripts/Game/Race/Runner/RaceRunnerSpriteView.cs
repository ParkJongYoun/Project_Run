using UnityEngine;

namespace ProjectRun.Race
{
    // Only presentation is changed. RaceRunnerView still moves on the shared course.
    [DefaultExecutionOrder(130)]
    public sealed class RaceRunnerSpriteView : MonoBehaviour
    {
        public RaceRunnerView Runner;
        public Sys_Race Race;
        public Sys_RaceSpritePresentation Director;
        public Data_RaceSpriteSet Art;
        public SpriteRenderer Image;
        private double animationClock;
        private double previousClock;
        private int generation = -1;
        private int lastFoot = -1;

        private void LateUpdate()
        {
            if (Race == null || Race.Simulation == null || Art == null || Director == null) return;
            var state = Race.Simulation.Runners[Runner.RunnerId];
            if (generation != Race.Generation)
            {
                generation = Race.Generation;
                previousClock = 0;
                animationClock = Runner.RunnerId * .137;
                lastFoot = -1;
            }
            double clock = System.Math.Max(0, Race.Simulation.Time - Data_RaceSimulation.StepSeconds
                + Mathf.Clamp01(Race.Interpolation) * Data_RaceSimulation.StepSeconds);
            double delta = System.Math.Max(0, clock - previousClock);
            previousClock = clock;
            animationClock += delta * Mathf.Clamp(state.Speed / 13f, 0, 1.8f);
            Image.sprite = Art.GetFrame(Director.Angle, animationClock, state.Speed > .5f);
            if (Image.sprite == null) return;
            var camera = Director.Camera;
            bool flat = Director.Mode == E_RaceArtMode.AllSprites;
            float height = Art.WorldHeight;
            Vector3 ground;
            if (flat)
            {
                Director.GetFlatPlacement(Runner.RunnerId, out var viewport, out float fraction);
                ground = camera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, 20));
                height = camera.orthographicSize * 2 * fraction;
                Image.sortingOrder = 1000 - Mathf.RoundToInt(viewport.y * 1000);
            }
            else
            {
                ground = Runner.transform.position;
                Image.sortingOrder = 0; // World distance sorting preserves near/far runner overlap.
            }
            Image.transform.SetPositionAndRotation(ground, camera.transform.rotation);
            float scale = height / Mathf.Max(.01f, Art.StillViews[(int)Director.Angle].bounds.size.y);
            Image.transform.localScale = Vector3.one * scale;
            Runner.RiderHead.position = ground + camera.transform.up * height * .86f;
            Runner.FaceTarget.position = Runner.RiderHead.position;
            Runner.CameraTarget.position = ground + Vector3.up * height * .52f;
            Runner.MonsterFaceTarget.position = ground + camera.transform.up * height * .38f;
            if (Runner.Shadow != null)
            {
                Runner.Shadow.gameObject.SetActive(!flat);
                if (!flat) Runner.Shadow.position = Runner.transform.position + Vector3.up * .025f;
            }
            int foot = (int)(animationClock * 4);
            if (Runner.HasFootfalls && state.Speed > 2 && !Race.Paused && !Race.PresentationHeld && foot != lastFoot)
            {
                lastFoot = foot;
                Mgr_Sound.PlayFoot(flat ? .18f : Mathf.Clamp01(1 - Vector3.Distance(camera.transform.position, ground) / 50) * .4f);
            }
        }
    }
}

