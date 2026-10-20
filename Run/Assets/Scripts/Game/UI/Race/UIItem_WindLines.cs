using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    /// <summary>
    /// Horizontal wind streaks that sweep across the rect (default right → left).
    /// Each streak is a straight bar of uniform thickness and alpha.
    /// Streaks spawn just beyond one edge and leave past the other, so they never pop in on screen.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIItem_WindLines : MaskableGraphic
    {
        [Header("Shape")]
        [Range(2, 120)] public int LineCount = 10;
        [Tooltip("Streak length range (fraction of the rect width).")]
        public Vector2 LengthRange = new Vector2(0.15f, 0.4f);
        [Tooltip("Streak thickness, in canvas units.")]
        public Vector2 ThicknessRange = new Vector2(6f, 12f);
        [Tooltip("Vertical band streaks are spread over (0 = bottom, 1 = top).")]
        public Vector2 VerticalBand = new Vector2(0.05f, 0.95f);

        [Header("Motion")]
        [Tooltip("Checked: right → left. Unchecked: left → right.")]
        public bool RightToLeft = true;
        [Tooltip("Speed range (rect widths per second).")]
        public Vector2 SpeedRange = new Vector2(1.4f, 2.6f);
        [Range(0f, 1f)] public float MaxAlpha = 0.3f;

        [Header("Runtime")]
        [Range(0f, 1f)] public float Intensity;

        private struct Streak { public float Y, Head, Length, Thickness, Speed; }
        private Streak[] streaks;
        private uint seed = 0x2545F491u;

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
            if (streaks != null && streaks.Length == LineCount) return;
            streaks = new Streak[LineCount];
            for (int i = 0; i < streaks.Length; i++) Respawn(ref streaks[i], i, true);
        }

        // Head is measured in "progress" space: 0 = entry edge, 1 = exit edge.
        private void Respawn(ref Streak s, int index, bool scatter)
        {
            // Even vertical spread with jitter so lines don't clump.
            float slot = (index + Rand() * 0.9f) / Mathf.Max(1, LineCount);
            s.Y = Mathf.Lerp(VerticalBand.x, VerticalBand.y, slot);
            s.Length = Mathf.Lerp(LengthRange.x, LengthRange.y, Rand());
            s.Thickness = Mathf.Lerp(ThicknessRange.x, ThicknessRange.y, Rand());
            s.Speed = Mathf.Lerp(SpeedRange.x, SpeedRange.y, Rand());
            s.Head = scatter ? Rand() * (1f + s.Length) : -Rand() * 0.5f; // start beyond the entry edge
        }

        /// <summary>Advance the streaks (call every frame while visible).</summary>
        public void Tick(float deltaTime)
        {
            EnsureStreaks();
            if (Intensity <= 0f) return;
            for (int i = 0; i < streaks.Length; i++)
            {
                streaks[i].Head += streaks[i].Speed * deltaTime;
                if (streaks[i].Head - streaks[i].Length > 1.02f) Respawn(ref streaks[i], i, false); // tail left the rect
            }
            SetVerticesDirty();
        }

        public void SetIntensity(float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            if (intensity == Intensity) return;
            Intensity = intensity;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Intensity <= 0.001f || streaks == null) return;
            var r = rectTransform.rect;
            float alpha = MaxAlpha * Intensity;
            Color32 c = color; c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * alpha * 255f), 0, 255);
            var v = UIVertex.simpleVert;
            v.color = c;

            foreach (var s in streaks)
            {
                float head = s.Head, tail = s.Head - s.Length;
                if (head < 0f || tail > 1f) continue;
                float xHead = RightToLeft ? Mathf.Lerp(r.xMax, r.xMin, head) : Mathf.Lerp(r.xMin, r.xMax, head);
                float xTail = RightToLeft ? Mathf.Lerp(r.xMax, r.xMin, tail) : Mathf.Lerp(r.xMin, r.xMax, tail);
                float y = Mathf.Lerp(r.yMin, r.yMax, s.Y);
                float h = s.Thickness * 0.5f;

                int start = vh.currentVertCount;
                v.position = new Vector3(xTail, y - h); vh.AddVert(v);
                v.position = new Vector3(xTail, y + h); vh.AddVert(v);
                v.position = new Vector3(xHead, y + h); vh.AddVert(v);
                v.position = new Vector3(xHead, y - h); vh.AddVert(v);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start + 2, start + 3, start);
            }
        }
    }
}
