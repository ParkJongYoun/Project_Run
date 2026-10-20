using System.Collections.Generic;
using UnityEngine;

namespace ProjectRun.Race
{
    /// <summary>Subdivides the approved Image and gently deforms its six painted subjects.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.UI.Image))]
    public sealed class UIItem_TitleArtworkMotion : UnityEngine.UI.BaseMeshEffect
    {
        [SerializeField, Range(32, 128)] private int Columns = 104;
        [SerializeField, Range(18, 72)] private int Rows = 60;
        [SerializeField, Range(0, 2)] private float MotionStrength = 1;

        private const float ArtworkWidth = 1672;
        private const float ArtworkHeight = 941;
        private const float FeatherPixels = 36;
        private readonly List<UIVertex> vertices = new List<UIVertex>();
        private readonly List<int> indices = new List<int>();
        private Vector3[] restPositions;
        private Vector2[] artworkPoints;
        private float[] weights;
        private Vector3 cachedMin;
        private Vector3 cachedMax;
        private Vector4 cachedUvMin;
        private Vector4 cachedUvMax;
        private int cachedColumns;
        private int cachedRows;
        private float elapsed;
        private readonly Vector4[] motion = new Vector4[6];

        private sealed class Region
        {
            public readonly Vector2 Pivot;
            public readonly float Period, Phase, Bob, Sway, Stretch, Tilt;
            public readonly Vector2[] Outline;

            public Region(Vector2 pivot, float period, float phase, float bob, float sway,
                float stretch, float tilt, params Vector2[] outline)
            {
                Pivot = pivot;
                Period = period;
                Phase = phase;
                Bob = bob;
                Sway = sway;
                Stretch = stretch;
                Tilt = tilt * Mathf.Deg2Rad;
                Outline = outline;
            }
        }

        // Front-to-back masks in original artwork pixels (top-left origin).
        // A narrow feather outside each silhouette keeps the single image continuous.
        private static readonly Region[] regions =
        {
            new Region(new Vector2(662, 739), 2.8f, .2f, 3.1f, 1.2f, .013f, .18f,
                new Vector2(520, 648), new Vector2(533, 593), new Vector2(575, 552),
                new Vector2(648, 531), new Vector2(668, 516), new Vector2(694, 515),
                new Vector2(709, 535), new Vector2(759, 565), new Vector2(791, 613),
                new Vector2(801, 670), new Vector2(778, 712), new Vector2(730, 738),
                new Vector2(645, 744), new Vector2(568, 726), new Vector2(533, 694)),
            new Region(new Vector2(1078, 762), 3.3f, 1.1f, 2.3f, 1.4f, .006f, .32f,
                new Vector2(912, 561), new Vector2(952, 553), new Vector2(968, 535),
                new Vector2(1024, 564), new Vector2(1082, 609), new Vector2(1117, 533),
                new Vector2(1126, 511), new Vector2(1148, 545), new Vector2(1165, 605),
                new Vector2(1158, 630), new Vector2(1196, 669), new Vector2(1209, 723),
                new Vector2(1175, 753), new Vector2(1118, 774), new Vector2(1042, 764),
                new Vector2(981, 731), new Vector2(951, 685), new Vector2(931, 615)),
            new Region(new Vector2(1424, 788), 3.0f, 2.2f, 2.8f, 1.3f, .008f, .3f,
                new Vector2(1300, 639), new Vector2(1329, 605), new Vector2(1382, 592),
                new Vector2(1384, 550), new Vector2(1414, 495), new Vector2(1451, 523),
                new Vector2(1490, 514), new Vector2(1538, 525), new Vector2(1554, 564),
                new Vector2(1570, 596), new Vector2(1546, 638), new Vector2(1507, 660),
                new Vector2(1519, 692), new Vector2(1486, 712), new Vector2(1463, 753),
                new Vector2(1423, 795), new Vector2(1382, 774), new Vector2(1363, 733),
                new Vector2(1312, 710), new Vector2(1287, 679)),
            new Region(new Vector2(336, 677), 3.5f, .7f, 2.4f, 1.1f, .007f, .23f,
                new Vector2(188, 414), new Vector2(194, 374), new Vector2(244, 343),
                new Vector2(278, 303), new Vector2(336, 277), new Vector2(322, 311),
                new Vector2(404, 309), new Vector2(431, 350), new Vector2(405, 397),
                new Vector2(423, 436), new Vector2(454, 385), new Vector2(463, 434),
                new Vector2(540, 422), new Vector2(565, 447), new Vector2(501, 491),
                new Vector2(449, 539), new Vector2(391, 584), new Vector2(344, 624),
                new Vector2(363, 666), new Vector2(326, 690), new Vector2(267, 670),
                new Vector2(264, 624), new Vector2(288, 582), new Vector2(218, 574),
                new Vector2(179, 556), new Vector2(213, 520), new Vector2(272, 496),
                new Vector2(247, 455), new Vector2(211, 463)),
            new Region(new Vector2(1256, 581), 3.9f, 2.7f, 1.9f, 1.0f, .006f, .17f,
                new Vector2(1090, 407), new Vector2(1143, 356), new Vector2(1204, 332),
                new Vector2(1244, 303), new Vector2(1280, 341), new Vector2(1308, 316),
                new Vector2(1331, 371), new Vector2(1365, 416), new Vector2(1373, 461),
                new Vector2(1356, 501), new Vector2(1369, 552), new Vector2(1341, 582),
                new Vector2(1294, 562), new Vector2(1239, 590), new Vector2(1187, 569),
                new Vector2(1165, 515), new Vector2(1122, 479)),
            new Region(new Vector2(791, 574), 4.2f, 1.8f, 1.5f, .8f, .005f, .12f,
                new Vector2(480, 358), new Vector2(500, 300), new Vector2(559, 256),
                new Vector2(634, 240), new Vector2(728, 251), new Vector2(791, 278),
                new Vector2(846, 294), new Vector2(925, 324), new Vector2(1006, 387),
                new Vector2(1030, 439), new Vector2(1067, 467), new Vector2(1077, 510),
                new Vector2(1054, 554), new Vector2(1011, 566), new Vector2(969, 532),
                new Vector2(916, 554), new Vector2(854, 589), new Vector2(783, 592),
                new Vector2(722, 553), new Vector2(645, 521), new Vector2(603, 477),
                new Vector2(585, 404), new Vector2(541, 381), new Vector2(513, 397))
        };

        protected override void OnEnable()
        {
            elapsed = 0;
            base.OnEnable();
        }

        private void Update()
        {
            if (!Application.isPlaying || !IsActive() || graphic == null || !graphic.isActiveAndEnabled)
                return;
            elapsed += Time.unscaledDeltaTime;
            if (MotionStrength > 0)
                graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(UnityEngine.UI.VertexHelper helper)
        {
            if (!IsActive() || helper.currentVertCount != 4)
                return;

            var bottomLeft = UIVertex.simpleVert;
            var topRight = UIVertex.simpleVert;
            helper.PopulateUIVertex(ref bottomLeft, 0);
            helper.PopulateUIVertex(ref topRight, 2);
            int columns = Mathf.Clamp(Columns, 32, 128);
            int rows = Mathf.Clamp(Rows, 18, 72);
            if (restPositions == null || columns != cachedColumns || rows != cachedRows ||
                cachedMin != bottomLeft.position || cachedMax != topRight.position ||
                cachedUvMin != bottomLeft.uv0 || cachedUvMax != topRight.uv0)
                BuildGrid(bottomLeft, topRight, columns, rows);

            float strength = Application.isPlaying
                ? MotionStrength * Mathf.SmoothStep(0, 1, elapsed / .8f) : 0;
            for (int r = 0; r < regions.Length; r++)
            {
                var region = regions[r];
                float phase = elapsed * (Mathf.PI * 2 / region.Period) + region.Phase;
                motion[r] = new Vector4(
                    Mathf.Sin(phase * .83f) * region.Sway,
                    -Mathf.Sin(phase) * region.Bob,
                    Mathf.Sin(phase + .5f) * region.Stretch,
                    Mathf.Sin(phase * .77f + .4f) * region.Tilt) * strength;
            }

            Vector2 scale = new Vector2(
                (cachedMax.x - cachedMin.x) / ArtworkWidth,
                -(cachedMax.y - cachedMin.y) / ArtworkHeight);
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 displacement = Vector2.zero;
                for (int r = 0; r < regions.Length; r++)
                {
                    float weight = weights[i * regions.Length + r];
                    if (weight <= 0) continue;
                    Vector2 relative = artworkPoints[i] - regions[r].Pivot;
                    Vector4 pose = motion[r];
                    displacement.x += weight * (pose.x - relative.y * pose.w - relative.x * pose.z * .45f);
                    displacement.y += weight * (pose.y + relative.x * pose.w + relative.y * pose.z);
                }
                var vertex = vertices[i];
                vertex.position = restPositions[i] + new Vector3(displacement.x * scale.x, displacement.y * scale.y);
                vertex.color = bottomLeft.color;
                vertices[i] = vertex;
            }
            helper.Clear();
            helper.AddUIVertexStream(vertices, indices);
        }

        private void BuildGrid(UIVertex bottomLeft, UIVertex topRight, int columns, int rows)
        {
            cachedColumns = columns;
            cachedRows = rows;
            cachedMin = bottomLeft.position;
            cachedMax = topRight.position;
            cachedUvMin = bottomLeft.uv0;
            cachedUvMax = topRight.uv0;
            vertices.Clear();
            indices.Clear();
            int count = (columns + 1) * (rows + 1);
            restPositions = new Vector3[count];
            artworkPoints = new Vector2[count];
            weights = new float[count * regions.Length];
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= columns; x++)
            {
                int index = y * (columns + 1) + x;
                float u = (float)x / columns;
                float v = (float)y / rows;
                var vertex = bottomLeft;
                vertex.position = new Vector3(Mathf.Lerp(cachedMin.x, cachedMax.x, u),
                    Mathf.Lerp(cachedMin.y, cachedMax.y, v), cachedMin.z);
                vertex.uv0 = new Vector4(Mathf.Lerp(cachedUvMin.x, cachedUvMax.x, u),
                    Mathf.Lerp(cachedUvMin.y, cachedUvMax.y, v), cachedUvMin.z, cachedUvMin.w);
                vertices.Add(vertex);
                restPositions[index] = vertex.position;
                Vector2 point = new Vector2(u * ArtworkWidth, (1 - v) * ArtworkHeight);
                artworkPoints[index] = point;
                float remaining = 1;
                for (int r = 0; r < regions.Length; r++)
                {
                    float mask = MaskWeight(point, regions[r].Outline);
                    weights[index * regions.Length + r] = mask * remaining;
                    remaining *= 1 - mask;
                }
                if (x == columns || y == rows) continue;
                int above = index + columns + 1;
                indices.Add(index); indices.Add(above); indices.Add(above + 1);
                indices.Add(above + 1); indices.Add(index + 1); indices.Add(index);
            }
        }

        private static float MaskWeight(Vector2 point, Vector2[] outline)
        {
            bool inside = false;
            float distanceSquared = float.MaxValue;
            for (int i = 0, previous = outline.Length - 1; i < outline.Length; previous = i++)
            {
                Vector2 a = outline[previous];
                Vector2 b = outline[i];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
                Vector2 edge = b - a;
                float along = Mathf.Clamp01(Vector2.Dot(point - a, edge) / Mathf.Max(edge.sqrMagnitude, .001f));
                distanceSquared = Mathf.Min(distanceSquared, (point - a - edge * along).sqrMagnitude);
            }
            return inside ? 1 : 1 - Mathf.SmoothStep(0, 1, Mathf.Sqrt(distanceSquared) / FeatherPixels);
        }
    }
}
