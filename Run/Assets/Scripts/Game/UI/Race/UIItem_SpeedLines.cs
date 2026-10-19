using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    /// <summary>
    /// Radial wind/speed lines around <see cref="Center"/>.
    /// Outward: streaks are born near the center and rush past the screen edge.
    /// Inward: streaks enter from beyond the screen edge and get sucked into the center.
    /// Each streak is a tapered triangle (thin + transparent toward the center, wide + opaque toward the edge).
    /// Make this RectTransform larger than the screen so streaks clearly start/end off-screen.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIItem_SpeedLines : MaskableGraphic
    {
        [Header("Shape")]
        [Range(4, 160)] public int LineCount = 56;
        [Tooltip("Clear radius around the center (fraction of the distance to the farthest rect corner).")]
        [Range(0f, 0.9f)] public float InnerRadius = 0.22f;
        [Tooltip("Streak length range (fraction of the distance to the farthest rect corner).")]
        public Vector2 LengthRange = new Vector2(0.18f, 0.45f);
        [Tooltip("Streak width at the outer end, in canvas units.")]
        public Vector2 WidthRange = new Vector2(1.5f, 5f);

        [Header("Motion")]
        [Tooltip("Checked: lines come in from beyond the screen edge toward the center. Unchecked: lines rush outward.")]
        public bool Inward = false;
        [Tooltip("Speed range (fractions of the corner distance per second).")]
        public Vector2 SpeedRange = new Vector2(1.6f, 3.2f);
        [Tooltip("Max alpha of a streak.")]
        [Range(0f, 1f)] public float MaxAlpha = 0.75f;

        [Header("Runtime")]
        [Range(0f, 1f)] public float Intensity;
        public Vector2 Center;

        private struct Streak { public float Angle, Head, Length, Width, Speed; }
        private Streak[] streaks;
        private bool streaksInward;
        private uint seed = 0x9E3779B9u;

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            EnsureStreaks();
        }

        private float Rand()
        {
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            return (seed & 0xFFFFFF) / (float)0x1000000;
        }

        private void EnsureStreaks()
        {
            if (streaks != null && streaks.Length == LineCount && streaksInward == Inward) return;
            streaksInward = Inward;
            streaks = new Streak[LineCount];
            for (int i = 0; i < streaks.Length; i++) Respawn(ref streaks[i], i, true);
        }

        private void Respawn(ref Streak s, int index, bool scatter)
        {
            // Even angular spread with jitter so lines never clump on one side.
            s.Angle = (index + Rand() * 0.9f) / Mathf.Max(1, LineCount) * Mathf.PI * 2f;
            s.Length = Mathf.Lerp(LengthRange.x, LengthRange.y, Rand());
            s.Width = Mathf.Lerp(WidthRange.x, WidthRange.y, Rand());
            s.Speed = Mathf.Lerp(SpeedRange.x, SpeedRange.y, Rand());
            float spread = InnerRadius + Rand() * (1f - InnerRadius + s.Length); // fills the whole rect (first fill)
            if (Inward)
                s.Head = scatter ? spread : 1f + Rand() * s.Length;              // spawn beyond the rect corner
            else
                s.Head = scatter ? spread : InnerRadius + Rand() * 0.1f;         // spawn just outside the clear zone
        }

        /// <summary>Advance the streaks (call every frame while visible).</summary>
        public void Tick(float deltaTime)
        {
            EnsureStreaks();
            if (Intensity <= 0f) return;
            for (int i = 0; i < streaks.Length; i++)
            {
                if (Inward)
                {
                    streaks[i].Head -= streaks[i].Speed * deltaTime;                       // toward the center
                    if (streaks[i].Head <= InnerRadius) Respawn(ref streaks[i], i, false); // fully swallowed by the clear zone
                }
                else
                {
                    streaks[i].Head += streaks[i].Speed * deltaTime;                       // toward the edge
                    if (streaks[i].Head - streaks[i].Length > 1.02f) Respawn(ref streaks[i], i, false); // tail left the rect
                }
            }
            SetVerticesDirty();
        }

        public void SetState(Vector2 center, float intensity)
        {
            bool changed = intensity != Intensity || (intensity > 0f && center != Center);
            Center = center; Intensity = Mathf.Clamp01(intensity);
            if (changed) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Intensity <= 0.001f || streaks == null) return;
            var r = rectTransform.rect;
            // Distance from the center to the farthest rect corner, so lines always reach every edge.
            float reach = 0f;
            reach = Mathf.Max(reach, Vector2.Distance(Center, new Vector2(r.xMin, r.yMin)));
            reach = Mathf.Max(reach, Vector2.Distance(Center, new Vector2(r.xMax, r.yMin)));
            reach = Mathf.Max(reach, Vector2.Distance(Center, new Vector2(r.xMin, r.yMax)));
            reach = Mathf.Max(reach, Vector2.Distance(Center, new Vector2(r.xMax, r.yMax)));
            float inner = InnerRadius * reach;

            var v = UIVertex.simpleVert;
            foreach (var s in streaks)
            {
                float head = s.Head * reach, tail = (s.Head - s.Length) * reach;
                tail = Mathf.Max(tail, inner);
                if (head <= tail + 1f) continue;
                var dir = new Vector2(Mathf.Cos(s.Angle), Mathf.Sin(s.Angle));
                var side = new Vector2(-dir.y, dir.x);
                float t = Mathf.InverseLerp(inner, reach, head);
                float w = s.Width * (0.5f + 0.5f * t);
                float alpha;
                float wideAt, pointAt; // wide/opaque end = leading end in the direction of motion
                if (Inward)
                {
                    // Outer end wide/opaque, inner end faint.
                    // Fade out while the streak is swallowed by the clear zone.
                    float visible = (head - tail) / Mathf.Max(1f, s.Length * reach);
                    alpha = MaxAlpha * Intensity * Mathf.SmoothStep(0f, 1f, visible * 2f);
                    wideAt = head; pointAt = tail;
                }
                else
                {
                    // Leading end is the outer end; transparent near the clear zone.
                    alpha = MaxAlpha * Intensity * Mathf.SmoothStep(0f, 1f, t * 5f);
                    wideAt = head; pointAt = tail;
                }
                if (alpha <= 0.002f) continue;
                Color32 wideColor = color; wideColor.a = (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * alpha * 255f), 0, 255);
                Color32 pointColor = color; pointColor.a = 0;

                int start = vh.currentVertCount;
                v.color = pointColor; v.position = Center + dir * pointAt; vh.AddVert(v);
                v.color = wideColor; v.position = Center + dir * wideAt + side * w; vh.AddVert(v);
                v.color = wideColor; v.position = Center + dir * wideAt - side * w; vh.AddVert(v);
                vh.AddTriangle(start, start + 1, start + 2);
            }
        }
    }
}
