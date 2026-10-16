using UnityEngine;

namespace ProjectRun.Race
{
    [CreateAssetMenu(menuName = "Project Run/Fox directional impostor")]
    public sealed class Data_FoxDirectionalImpostor : ScriptableObject
    {
        public Texture2D ColorAtlas;
        public Texture2D DepthAtlas;
        public Material Material;
        public Vector3 Center;
        public float Radius;
        public int AzimuthCount = 16;
        public int ElevationCount = 9;
        public float MinimumElevation = -90;
        public float MaximumElevation = 90;
        public int TileSize = 384;
        public string PoseDescription;

        public static void Basis(float yaw, float elevation, out Vector3 direction, out Vector3 right, out Vector3 up)
        {
            float a = yaw * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
            direction = new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
            right = new Vector3(-Mathf.Cos(a), 0, Mathf.Sin(a));
            up = Vector3.Cross(-direction, right);
        }
    }
}
