using System;
using UnityEngine;

namespace ProjectRun.Race
{
    /// <summary>Closed, arc-length sampled course shared by simulation and presentation.</summary>
    [CreateAssetMenu(menuName = "Project Run/Race course")]
    public sealed class Data_RaceCourse : ScriptableObject
    {
        public string CourseName = "프론테라 월드 서킷";
        public float RoadWidth = 18;
        public Vector3[] Points = Array.Empty<Vector3>();
        public float[] Distances = Array.Empty<float>();
        public float Length => Distances != null && Distances.Length > 1 ? Distances[Distances.Length - 1] : 0;
        public bool IsValid => Points != null && Points.Length >= 4 && Distances != null && Distances.Length == Points.Length && Length > 1;

        public Vector3 Center(float distance)
        {
            if (!IsValid) return Vector3.zero;
            float d = Mathf.Repeat(distance, Length);
            int low = 0, high = Distances.Length - 1;
            while (high - low > 1)
            {
                int mid = (low + high) / 2;
                if (Distances[mid] <= d) low = mid; else high = mid;
            }
            return Vector3.Lerp(Points[low], Points[high], (d - Distances[low]) / Mathf.Max(.0001f, Distances[high] - Distances[low]));
        }

        public Vector3 Forward(float distance) => (Center(distance + .5f) - Center(distance - .5f)).normalized;
        public Vector3 Point(float distance, float lane = 0)
        {
            Vector3 right = Vector3.Cross(Vector3.up, Forward(distance)).normalized;
            return Center(distance) + right * lane;
        }

        public float Metric(float distance, float lane)
        {
            // Offset lanes travel different distances on bends, including reverse bends.
            return Mathf.Max(.2f, Vector3.Distance(Point(distance - .5f, lane), Point(distance + .5f, lane)));
        }

        public string Region(float distance)
        {
            Vector3 p = Center(distance);
            if (p.x < -85 && p.z > -110) return "페이욘 숲길";
            if (p.z > 40 && Mathf.Abs(p.x) < 86) return "프론테라 분수광장";
            if (p.x > 90 && p.z > -115) return "소그라트 오아시스";
            return "프론테라 초원";
        }
    }
}
