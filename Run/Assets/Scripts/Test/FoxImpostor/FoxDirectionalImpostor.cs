using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRun.Race
{
    /// <summary>A single quad; all visible shape comes from captured color and depth, never the reference mesh.</summary>
    [ExecuteAlways, DefaultExecutionOrder(500)]
    public sealed class FoxDirectionalImpostor : MonoBehaviour
    {
        public Data_FoxDirectionalImpostor Data;
        public Camera ViewCamera;
        public MeshRenderer Display;
        public bool UseDepth = true;
        public Vector4 ViewWeights { get; private set; }
        public Vector4 ViewIndices { get; private set; }
        public bool IsOutsideCaptureRange { get; private set; }
        private MaterialPropertyBlock properties;
        private readonly Vector4[] rights = new Vector4[4];
        private readonly Vector4[] ups = new Vector4[4];
        private readonly Vector4[] directions = new Vector4[4];
        private readonly Vector4[] tiles = new Vector4[4];

        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (Data == null || Display == null || ViewCamera == null || Data.Radius <= 0 || Data.ElevationCount < 2 || Data.MaximumElevation <= Data.MinimumElevation) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            Vector3 center = transform.TransformPoint(Data.Center);
            var direction = transform.InverseTransformDirection(ViewCamera.transform.position - center).normalized;
            float x = Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 360) / 360 * Data.AzimuthCount;
            float elevation = Mathf.Asin(Mathf.Clamp(direction.y, -1, 1)) * Mathf.Rad2Deg;
            IsOutsideCaptureRange = elevation < Data.MinimumElevation - .01f || elevation > Data.MaximumElevation + .01f;
            // Outside the captured hemisphere, use the nearest available row without inventing underside views.
            float y = Mathf.InverseLerp(Data.MinimumElevation, Data.MaximumElevation, elevation) * (Data.ElevationCount - 1);
            int x0 = Mathf.FloorToInt(x) % Data.AzimuthCount;
            int y0 = Mathf.Min(Mathf.FloorToInt(y), Data.ElevationCount - 2);
            float fx = x - Mathf.Floor(x), fy = y - y0;
            ViewWeights = new Vector4((1-fx)*(1-fy), fx*(1-fy), (1-fx)*fy, fx*fy);
            var indices = Vector4.zero;
            for (int i = 0; i < 4; i++)
            {
                int column = (x0 + i % 2) % Data.AzimuthCount, row = y0 + i / 2;
                Data_FoxDirectionalImpostor.Basis(column * 360f / Data.AzimuthCount, Mathf.Lerp(Data.MinimumElevation, Data.MaximumElevation, (float)row / (Data.ElevationCount - 1)), out var d, out var r, out var u);
                directions[i] = d; rights[i] = r; ups[i] = u;
                tiles[i] = new Vector4(column, row, 1f / Data.AzimuthCount, 1f / Data.ElevationCount);
                indices[i] = row * Data.AzimuthCount + column;
            }
            ViewIndices = indices;
            Display.transform.SetPositionAndRotation(center, ViewCamera.transform.rotation);
            float distance=Vector3.Distance(ViewCamera.transform.position,center);
            float coverage=1/Mathf.Sqrt(Mathf.Max(.25f,1-Data.Radius*Data.Radius/(distance*distance)));
            Display.transform.localScale = Vector3.one * (Data.Radius * 2*coverage);
            Display.sharedMaterial = Data.Material;
            Display.shadowCastingMode = ShadowCastingMode.Off;
            properties.SetVectorArray("_ViewRight", rights);
            properties.SetVectorArray("_ViewUp", ups);
            properties.SetVectorArray("_ViewDirection", directions);
            properties.SetVectorArray("_Tiles", tiles);
            properties.SetVector("_Weights", ViewWeights);
            properties.SetVector("_CaptureCenter", Data.Center);
            properties.SetFloat("_Radius", Data.Radius);
            properties.SetFloat("_TileSize", Data.TileSize);
            properties.SetFloat("_UseDepth", UseDepth ? 1 : 0);
            // Root transform, not the camera-facing quad's transform.
            properties.SetMatrix("_WorldToCapture", transform.worldToLocalMatrix);
            properties.SetMatrix("_CaptureToWorld", transform.localToWorldMatrix);
            Display.SetPropertyBlock(properties);
        }
    }
}
