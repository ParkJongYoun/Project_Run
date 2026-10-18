using UnityEngine;

namespace ProjectRun.Race
{
    /// <summary>Lightweight 3D skill marker driven entirely by the race skill clock.</summary>
    [DefaultExecutionOrder(80)]
    public sealed class RaceRunnerSkillAura : MonoBehaviour
    {
        [SerializeField] private Material EffectMaterial;
        [SerializeField, Min(.1f)] private float Radius = 2.4f;
        [SerializeField, Min(.1f)] private float Height = 1.8f;
        private Sys_Race race;
        private int runnerId;
        private Color tint;
        private readonly LineRenderer[] rings = new LineRenderer[2];
        private readonly LineRenderer[] wisps = new LineRenderer[6];
        private Mesh disc;
        private MeshRenderer discRenderer;
        private Color[] discColors;
        public bool IsVisible { get; private set; }
        public void Bind(Sys_Race target, int id, Color color) { race = target; runnerId = id; tint = Color.Lerp(color, Color.white, .35f); }
        private void Start()
        {
            if (EffectMaterial == null) { enabled = false; return; }
            for (int i = 0; i < rings.Length; i++) rings[i] = CreateLine("obj_aura_ring_" + i, 65);
            for (int i = 0; i < wisps.Length; i++) wisps[i] = CreateLine("obj_aura_wisp_" + i, 3);
            var ground = new GameObject("obj_aura_ground", typeof(MeshFilter), typeof(MeshRenderer));
            ground.transform.SetParent(transform, false);
            var vertices = new Vector3[66]; var triangles = new int[64 * 3]; discColors = new Color[vertices.Length];
            for (int i = 0; i <= 64; i++)
            {
                float angle = i * Mathf.PI * 2 / 64;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Radius;
                if (i < 64) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
            }
            disc = new Mesh { name = "SkillAuraGround" }; disc.MarkDynamic();
            disc.vertices = vertices; disc.triangles = triangles; disc.RecalculateBounds();
            ground.GetComponent<MeshFilter>().sharedMesh = disc;
            discRenderer = ground.GetComponent<MeshRenderer>(); discRenderer.sharedMaterial = EffectMaterial;
            discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; discRenderer.receiveShadows = false;
            Hide();
        }
        private LineRenderer CreateLine(string objectName, int count)
        {
            var root = new GameObject(objectName); root.transform.SetParent(transform, false);
            var line = root.AddComponent<LineRenderer>(); line.sharedMaterial = EffectMaterial;
            line.positionCount = count; line.useWorldSpace = false; line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            line.enabled = false; return line;
        }
        private void LateUpdate()
        {
            if (race == null || race.Simulation == null || disc == null) return;
            var sim = race.Simulation; var runner = sim.Runners[runnerId];
            float duration = race.Entrants[runnerId].SurgeDuration;
            float age = runner.SkillAge < 0 ? -1 : Mathf.Max(0, runner.SkillAge - (1 - Mathf.Clamp01(race.Interpolation)) * (float)Data_RaceSimulation.StepSeconds);
            if (race.IsSkipping || age < 0 || age >= duration || runner.Finished || sim.Phase == E_RacePhase.Result) { Hide(); return; }
            IsVisible = true;
            transform.localPosition = Vector3.up * .07f;
            float fade = Mathf.SmoothStep(0, 1, age / .22f) * Mathf.SmoothStep(0, 1, (duration - age) / .65f);
            float pulse = 1 + Mathf.Sin(age * 5) * .055f;
            discRenderer.enabled = true; discRenderer.transform.localScale = Vector3.one * pulse;
            for (int i = 0; i < discColors.Length; i++) discColors[i] = new Color(tint.r, tint.g, tint.b, (i == 0 ? .38f : .025f) * fade);
            disc.colors = discColors;
            for (int i = 0; i < rings.Length; i++)
            {
                var ring = rings[i]; ring.enabled = true;
                float phase = i == 0 ? 0 : Mathf.Repeat(age * .65f, 1);
                float radius = Radius * (i == 0 ? pulse : .8f + phase * .48f);
                for (int point = 0; point < 65; point++)
                {
                    float angle = point * Mathf.PI * 2 / 64;
                    ring.SetPosition(point, new Vector3(Mathf.Cos(angle) * radius, .025f, Mathf.Sin(angle) * radius));
                }
                ring.startWidth = ring.endWidth = i == 0 ? .10f : .065f;
                ring.startColor = ring.endColor = new Color(tint.r, tint.g, tint.b, fade * (i == 0 ? .9f : (1 - phase) * .55f));
            }
            for (int i = 0; i < wisps.Length; i++)
            {
                var wisp = wisps[i]; wisp.enabled = true;
                float phase = Mathf.Repeat(age * .8f + i / 6f, 1), angle = i * Mathf.PI / 3 + age * .6f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Radius * .82f;
                wisp.SetPosition(0, radial + Vector3.up * (phase * Height));
                wisp.SetPosition(1, radial * .96f + Vector3.up * (phase * Height + .25f));
                wisp.SetPosition(2, radial * .9f + Vector3.up * (phase * Height + .5f));
                wisp.startWidth = .12f; wisp.endWidth = .02f;
                wisp.startColor = new Color(tint.r, tint.g, tint.b, fade * (1 - phase) * .7f);
                wisp.endColor = new Color(1, 1, 1, 0);
            }
        }
        private void Hide()
        {
            IsVisible = false;
            if (discRenderer != null) discRenderer.enabled = false;
            foreach (var line in rings) if (line != null) line.enabled = false;
            foreach (var line in wisps) if (line != null) line.enabled = false;
        }
        private void OnDisable() => Hide();
        private void OnDestroy() { if (disc != null) Destroy(disc); }
    }
}
