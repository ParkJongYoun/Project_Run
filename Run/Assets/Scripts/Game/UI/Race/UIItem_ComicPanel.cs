using UnityEngine;

namespace ProjectRun.Race
{
    // A star-shaped comic cut, sampled directly from the page. Frames remain separate graphics.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIItem_ComicPanel : UnityEngine.UI.MaskableGraphic
    {
        private Texture source;
        private Vector2[] points;
        private Vector2 center;
        private bool frame;
        private Material ownedMaterial;
        private Rect sourceRegion;
        private bool useSourceRegion;
        public override Texture mainTexture => source != null ? source : Texture2D.whiteTexture;

        public void Configure(Texture texture, Vector2[] corners, Material template, bool drawFrame)
        {
            source = texture;
            useSourceRegion = false;
            points = corners;
            frame = drawFrame;
            Vector2 minimum = corners[0], maximum = corners[0];
            foreach (var point in corners) { minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point); }
            center = (minimum + maximum) * .5f;
            rectTransform.sizeDelta = maximum - minimum;
            rectTransform.anchoredPosition = new Vector2(center.x - 836, 495 - center.y);
            raycastTarget = false;
            if (template != null)
            {
                ownedMaterial = new Material(template);
                material = ownedMaterial;
            }
            SetAllDirty();
        }

        // Normalized top-left source region, independent of the panel's page layout.
        public void ConfigureRegion(Texture texture, Vector2[] shape, Rect region, Material template, bool drawFrame)
        {
            Configure(texture, shape, template, drawFrame);
            sourceRegion = region;
            useSourceRegion = true;
            SetVerticesDirty();
        }

        public void SetLight(float saturation, float brightness)
        {
            if (ownedMaterial != null) ownedMaterial.SetFloat("_Saturation", saturation);
            color = new Color(brightness, brightness, brightness, 1);
        }

        public void SetTear(float side)
        {
            SetTear(side, Vector2.right);
        }

        public void SetTear(float side, Vector2 axis)
        {
            if (ownedMaterial == null) return;
            ownedMaterial.SetFloat("_TearSide", side);
            ownedMaterial.SetVector("_TearAxis", new Vector4(axis.x, axis.y, 0, 0));
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            if (points == null) return;
            for (int i = 0; i < points.Length; i++) AddVertex(mesh, points[i], false);
            if (frame)
            {
                for (int i = 0; i < points.Length; i++) AddVertex(mesh, points[i], true);
                for (int i = 0; i < points.Length; i++)
                {
                    int next = (i + 1) % points.Length;
                    mesh.AddTriangle(i, next, points.Length + next);
                    mesh.AddTriangle(i, points.Length + next, points.Length + i);
                }
            }
            else
            {
                // A center fan also supports the inward notches of a torn paper contour.
                AddVertex(mesh, center, false);
                for (int i = 0; i < points.Length; i++) mesh.AddTriangle(points.Length, i, (i + 1) % points.Length);
            }
        }

        private void AddVertex(UnityEngine.UI.VertexHelper mesh, Vector2 point, bool inset)
        {
            var local = point - center;
            if (inset) local -= local.normalized * 6;
            var vertex = UIVertex.simpleVert;
            vertex.position = new Vector3(local.x, -local.y, 0);
            vertex.color = color;
            vertex.uv0 = source == null ? Vector2.zero : new Vector2(point.x / source.width, 1 - point.y / source.height);
            if (source != null && useSourceRegion)
            {
                var uv = (point - center) / rectTransform.sizeDelta + Vector2.one * .5f;
                vertex.uv0 = new Vector2(sourceRegion.x + uv.x * sourceRegion.width,
                    1 - sourceRegion.y - uv.y * sourceRegion.height);
            }
            mesh.AddVert(vertex);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (ownedMaterial == null) return;
            if (Application.isPlaying) Destroy(ownedMaterial); else DestroyImmediate(ownedMaterial);
        }
    }
}
