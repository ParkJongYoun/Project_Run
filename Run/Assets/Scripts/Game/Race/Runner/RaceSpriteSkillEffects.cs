using UnityEngine;

namespace ProjectRun.Race
{
    /// <summary>Ground-projected aura and short acceleration streaks, driven by simulation time.</summary>
    [DefaultExecutionOrder(70)]
    public sealed class RaceSpriteSkillEffects : MonoBehaviour
    {
        public RaceDirectionalSpriteView View;
        public Sprite Swirl;
        public Material SpriteMaterial;
        private Transform groundPlane;
        private SpriteRenderer aura;
        private readonly SpriteRenderer[] streaks = new SpriteRenderer[7];
        public bool IsActive { get; private set; }

        private SpriteRenderer Create(string name, Transform parent, Sprite sprite)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent,false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = SpriteMaterial; renderer.sprite = sprite;
            renderer.enabled = false;
            return renderer;
        }
        private void Start()
        {
            groundPlane = new GameObject("obj_skill_ground_plane").transform;
            groundPlane.SetParent(transform,false);
            aura = Create("img_skill_aura",groundPlane,Swirl);
            var disc = View.GroundShadow.GetComponent<SpriteRenderer>().sprite;
            for (int i=0;i<streaks.Length;i++) streaks[i] = Create("img_skill_streak_"+i,transform,disc);
        }
        private void LateUpdate()
        {
            if (View == null || View.Race == null || View.Race.Simulation == null || aura == null) return;
            var race = View.Race; var sim = race.Simulation;
            var runner = sim.Runners[View.RunnerId]; var setup = race.Entrants[View.RunnerId];
            IsActive = race.Countdown <= 0 && sim.Phase != E_RacePhase.Result && !runner.Finished
                && runner.SkillAge >= 0 && runner.SkillAge < setup.SurgeDuration;
            if (!IsActive) { Hide(); return; }
            float clock = (float)System.Math.Max(0,sim.Time-Data_RaceSimulation.StepSeconds
                + Mathf.Clamp01(race.Interpolation)*Data_RaceSimulation.StepSeconds);
            float age = Mathf.Max(0,clock-runner.SkillStarted);
            float fade = Mathf.Min(Mathf.Clamp01(age/.16f),Mathf.Clamp01((setup.SurgeDuration-age)/.45f));
            // Rotate in the circular ground plane BEFORE projection. The ellipse never tips upright.
            float pulse = .67f * (1 + .025f*Mathf.Sin(age*9));
            groundPlane.localPosition = new Vector3(0,-.08f,0);
            groundPlane.localRotation = Quaternion.identity;
            groundPlane.localScale = new Vector3(pulse,pulse*Mathf.Sin(Data_Race2DProjection.Pitch*Mathf.Deg2Rad),1);
            aura.enabled = true;
            aura.transform.localRotation = Quaternion.Euler(0,0,age*38);
            aura.color = new Color(1,1,1,fade*.48f);
            aura.sortingOrder = View.Display.sortingOrder-3;
            Vector3 forward = Data_Race2DProjection.Project(View.Heading).normalized;
            Vector3 side = Data_Race2DProjection.Project(Vector3.Cross(Vector3.up,View.Heading));
            float angle = Mathf.Atan2(forward.y,forward.x)*Mathf.Rad2Deg;
            var tint = Color.Lerp(setup.Color,Color.white,.78f);
            for (int i=0;i<streaks.Length;i++)
            {
                float phase = Mathf.Repeat(age*2.7f+i*.381966f,1);
                float spread = (i%2==0 ? 1 : -1) * (.6f+(i%3)*.19f);
                var streak = streaks[i]; streak.enabled = true;
                streak.transform.localPosition = -forward*(.35f+phase*1.7f) + side*spread
                    + Vector3.up*(.22f+(i%3)*.16f);
                streak.transform.localRotation = Quaternion.Euler(0,0,angle);
                float length = .4f+phase*.65f;
                streak.transform.localScale = new Vector3(length/streak.sprite.bounds.size.x,.045f/streak.sprite.bounds.size.y,1);
                tint.a = fade * Mathf.Sin(phase*Mathf.PI) * .66f; streak.color = tint;
                streak.sortingOrder = View.Display.sortingOrder-1;
            }
        }
        private void Hide()
        {
            if (aura != null) aura.enabled = false;
            foreach (var streak in streaks) if (streak != null) streak.enabled = false;
        }
        private void OnDisable() { IsActive = false; Hide(); }
    }
}
