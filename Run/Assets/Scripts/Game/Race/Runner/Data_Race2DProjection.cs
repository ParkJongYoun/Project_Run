using UnityEngine;

namespace ProjectRun.Race
{
    /// <summary>Presentation-only projection. The shared race simulation remains in course metres.</summary>
    public static class Data_Race2DProjection
    {
        public const float Pitch = 38;
        public const float Yaw = 35;
        private static readonly Quaternion inverse = Quaternion.Inverse(Quaternion.Euler(Pitch, Yaw, 0));

        public static Vector3 Project(Vector3 coursePoint)
        {
            Vector3 point = inverse * coursePoint;
            return new Vector3(point.x, point.y, 0);
        }

        public static Vector3 Ground(Vector2 screenPoint)
        {
            float yaw = Yaw * Mathf.Deg2Rad;
            float depth = screenPoint.y / Mathf.Sin(Pitch * Mathf.Deg2Rad);
            return new Vector3(Mathf.Cos(yaw) * screenPoint.x + Mathf.Sin(yaw) * depth,
                0, -Mathf.Sin(yaw) * screenPoint.x + Mathf.Cos(yaw) * depth);
        }

        public static int SortOrder(Vector3 groundPoint) => -Mathf.RoundToInt(groundPoint.y * 100);
    }
}
