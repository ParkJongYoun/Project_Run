using UnityEngine;

namespace ProjectRun.Race
{
    // Maps the same race state to the three authored illustration cameras.
    public sealed class Sys_RaceSpritePresentation : MonoBehaviour
    {
        public E_RaceArtMode Mode;
        public Sys_Race Race;
        public Sys_RacePresentation Presentation;
        public Camera Camera;
        public Sprite[] Backgrounds = new Sprite[12]; // section * 3 + angle
        public SpriteRenderer Background;
        public SpriteRenderer PreviousBackground;
        public E_RaceSpriteAngle Angle { get; private set; }
        public int Section { get; private set; }
        public int FocusRunner { get; private set; }
        public string ShotLabel { get; private set; }
        public E_RaceShot Shot { get; private set; }
        private int generation = -1;
        private int backgroundIndex = -1;
        private float transitionAge = 1;
        private float focusDistance;
        private float centerDistance;
        private int videoRunner = -1;
        private bool videoPlaying;
        private float returnUntil;
        private E_RaceSpriteAngle previousAngle = (E_RaceSpriteAngle)(-1);
        private Vector3 localOffset;
        private float motionDistance;
        private static readonly string[] sectionNames = { "관중석 앞 직선", "첫 번째 코너", "맞은편 직선", "마지막 코너" };

        public void BeginVideo(int runnerId) { videoRunner = runnerId; videoPlaying = true; }
        public void EndVideo() { videoPlaying = false; returnUntil = Time.unscaledTime + 1.6f; }
        public void CancelVideo() { videoRunner = -1; videoPlaying = false; returnUntil = 0; }

        public void RenderCamera(int manualShot)
        {
            if (Race == null || Race.Simulation == null) return;
            var sim = Race.Simulation;
            if (generation != Race.Generation)
            {
                generation = Race.Generation; backgroundIndex = -1; previousAngle = (E_RaceSpriteAngle)(-1);
                CancelVideo();
            }
            if (Race.Paused && videoRunner >= 0 && !videoPlaying) returnUntil += Time.unscaledDeltaTime;
            if (videoRunner >= 0 && !videoPlaying && Time.unscaledTime >= returnUntil) videoRunner = -1;
            FocusRunner = videoRunner >= 0 ? videoRunner : Presentation.InCutscene && Presentation.SkillRunner >= 0 ? Presentation.SkillRunner : sim.Order[0];
            focusDistance = Distance(FocusRunner);
            centerDistance = 0;
            foreach (var runner in sim.Runners) centerDistance += Distance(runner.Id);
            centerDistance /= sim.Runners.Length;
            Section = GetSection(focusDistance);
            float time = (float)sim.Time;
            if (Race.Countdown > 0) Angle = E_RaceSpriteAngle.Top;
            else if (Presentation.InCutscene || videoRunner >= 0 || Presentation.Remaining < 45) Angle = E_RaceSpriteAngle.Side;
            else
            {
                int cycle = Mathf.FloorToInt(time / 6.5f) % 3;
                Angle = cycle == 0 ? E_RaceSpriteAngle.Rear : cycle == 1 ? E_RaceSpriteAngle.Side : E_RaceSpriteAngle.Top;
            }
            if (manualShot >= 0) Angle = (E_RaceSpriteAngle)(manualShot % 3);
            if (videoRunner >= 0) Angle = E_RaceSpriteAngle.Side;
            Shot = videoRunner >= 0 ? E_RaceShot.SkillVideo : Angle == E_RaceSpriteAngle.Top ? E_RaceShot.Pack
                : Angle == E_RaceSpriteAngle.Rear ? E_RaceShot.Front : E_RaceShot.Tracking;
            ShotLabel = sectionNames[Section] + " · " + (Angle == E_RaceSpriteAngle.Side ? "측면 추적" : Angle == E_RaceSpriteAngle.Rear ? "후방 추적" : "상공 조망");
            transitionAge += Time.unscaledDeltaTime;
            motionDistance = centerDistance;
            if (Mode == E_RaceArtMode.AllSprites) RenderFlat();
            else RenderWorld();
            previousAngle = Angle;
        }

        public static int GetSection(float distance)
        {
            float point = Mathf.Repeat(distance + 139, Data_RaceSimulation.Length);
            if (point < 180) return 0;
            if (point < 180 + Mathf.PI * Data_RaceSimulation.Radius) return 1;
            if (point < 360 + Mathf.PI * Data_RaceSimulation.Radius) return 2;
            return 3;
        }

        private static float SectionProgress(float distance)
        {
            float point = Mathf.Repeat(distance + 139, Data_RaceSimulation.Length);
            float bend = Mathf.PI * Data_RaceSimulation.Radius;
            if (point < 180) return point / 180;
            if (point < 180 + bend) return (point - 180) / bend;
            if (point < 360 + bend) return (point - 180 - bend) / 180;
            return (point - 360 - bend) / bend;
        }

        private float Distance(int id)
        {
            var state = Race.Simulation.Runners[id];
            return Race.Simulation.Phase == E_RacePhase.Result ? (float)state.Distance
                : Mathf.Lerp((float)state.PreviousDistance, (float)state.Distance, Mathf.Clamp01(Race.Interpolation));
        }

        private void RenderWorld()
        {
            Camera.orthographic = false;
            var forward = Data_RaceSimulation.CourseForward(centerDistance);
            var right = Vector3.Cross(Vector3.up, forward);
            Vector3 center = Data_RaceSimulation.CoursePoint(centerDistance) + Vector3.up * 2;
            bool closeup = videoRunner >= 0 || Presentation.InCutscene;
            if (closeup) center = Data_RaceSimulation.CoursePoint(focusDistance, Race.Simulation.Runners[FocusRunner].Lane) + Vector3.up * 2;
            Vector3 requested;
            if (Angle == E_RaceSpriteAngle.Side) requested = new Vector3(24, 8, 4);
            else if (Angle == E_RaceSpriteAngle.Rear) requested = new Vector3(0, 11, -37);
            else requested = new Vector3(25, 47, 25);
            if (closeup) requested = new Vector3(18, 3.2f, 3);
            // Tall screens need enough horizontal room for all six lanes.
            float fit = Mathf.Clamp(.62f / Mathf.Max(.42f, Camera.aspect), 1, 1.4f);
            requested *= fit;
            // The home grandstand begins 26m outside the course, with end towers.
            // Stay in front of it even on tall screens instead of entering its walls.
            if (Angle == E_RaceSpriteAngle.Side && !closeup) requested.x = Mathf.Min(requested.x, 26);
            bool changed = previousAngle != Angle;
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 5);
            localOffset = changed ? requested : Vector3.Lerp(localOffset, requested, blend);
            Camera.transform.position = center + right * localOffset.x + Vector3.up * localOffset.y + forward * localOffset.z;
            Camera.transform.rotation = Quaternion.LookRotation(center - Camera.transform.position, Vector3.up);
            Camera.fieldOfView = Angle == E_RaceSpriteAngle.Side && !closeup ? 52 : 43;
        }

        private void RenderFlat()
        {
            Camera.orthographic = true;
            Camera.orthographicSize = 10;
            Camera.transform.SetPositionAndRotation(new Vector3(0, 1000, -20), Quaternion.identity);
            int index = Section * 3 + (int)Angle;
            if (index != backgroundIndex)
            {
                PreviousBackground.sprite = Background.sprite;
                PreviousBackground.transform.SetPositionAndRotation(Background.transform.position, Background.transform.rotation);
                PreviousBackground.transform.localScale = Background.transform.localScale;
                backgroundIndex = index; Background.sprite = Backgrounds[index]; transitionAge = 0;
            }
            if (Background.sprite == null) return;
            float height = Camera.orthographicSize * 2;
            float width = height * Camera.aspect;
            float scale = Mathf.Max(width / Background.sprite.bounds.size.x, height / Background.sprite.bounds.size.y) * 1.08f;
            Background.transform.localScale = Vector3.one * scale;
            float scroll = Mathf.Sin(motionDistance * .018f);
            // Use the full wide illustration for continuous forward travel. At the
            // section boundary the next illustration dissolves in and starts its pan.
            float panSpace = Mathf.Max(0, Background.sprite.bounds.size.x * scale - width) * .47f;
            float sidePan = Mathf.Lerp(panSpace, -panSpace, SectionProgress(motionDistance));
            Background.transform.position = new Vector3(Angle == E_RaceSpriteAngle.Side ? sidePan : scroll * .18f,
                1000 + (Angle == E_RaceSpriteAngle.Side ? 1.1f : Mathf.Sin(motionDistance * .014f) * .2f), 10);
            Background.transform.rotation = Quaternion.identity;
            Background.color = Color.white;
            PreviousBackground.color = new Color(1, 1, 1, 1 - Mathf.Clamp01(transitionAge / .45f));
            PreviousBackground.enabled = PreviousBackground.sprite != null && transitionAge < .45f;
        }

        public void GetFlatPlacement(int runnerId, out Vector2 viewport, out float heightFraction)
        {
            var state = Race.Simulation.Runners[runnerId];
            float relative = Distance(runnerId) - centerDistance;
            float lane = state.Lane;
            int laneIndex = 0;
            foreach (var other in Race.Simulation.Runners) if (other.Lane < lane) laneIndex++;
            if (Angle == E_RaceSpriteAngle.Side)
            {
                // Stagger the lanes in depth so six illustrated silhouettes remain readable
                // in a narrow portrait viewport. Their progress still comes from the race.
                int row = laneIndex / 3;
                viewport = new Vector2(Mathf.Clamp(.19f + laneIndex % 3 * .31f
                    + (float)System.Math.Tanh(relative * .035f) * .16f, .15f, .85f), .30f + row * .12f);
                heightFraction = .15f - row * .012f;
            }
            else if (Angle == E_RaceSpriteAngle.Rear)
            {
                float depth = (float)System.Math.Tanh(relative * .035f);
                viewport = new Vector2(.5f - lane * .043f * (1 - depth * .28f) - depth * .06f, .40f + depth * .23f);
                heightFraction = .19f - depth * .075f;
            }
            else
            {
                float depth = (float)System.Math.Tanh(relative * .035f);
                float trackCenter = Section == 1 ? .69f : Section == 3 ? .72f : .63f;
                int row = laneIndex / 2;
                viewport = new Vector2(Mathf.Clamp(trackCenter + .07f - row * .07f
                    + (laneIndex % 2 == 0 ? -.09f : .09f) + depth * .13f, .22f, .87f),
                    .34f + row * .105f - depth * .13f);
                heightFraction = .115f;
            }
            if (Race.Simulation.Phase == E_RacePhase.Result && runnerId == Race.Simulation.Order[0]) heightFraction *= 1.1f;
        }
    }
}

