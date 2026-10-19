using UnityEngine;

namespace ProjectRun.Race.CameraRig
{
    /// <summary>
    /// Trigger volume on the track. While the protagonist probe is inside, the linked virtual camera gets
    /// <see cref="ActivePriority"/>; otherwise it falls back to <see cref="InactivePriority"/>.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RaceCameraZone : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("Virtual camera shown while the protagonist is inside this zone. Accepts CinemachineCamera or camera groups (Sequencer / ClearShot).")]
        public Unity.Cinemachine.CinemachineVirtualCameraBase ZoneCamera;
        [Tooltip("HUD shot label shown while this zone is live.")]
        public string ShotLabel = "중계";

        [Header("Priority")]
        public int ActivePriority = 20;
        public int InactivePriority = 0;

        public bool IsLive => inside > 0;
        private int inside;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            Apply();
        }

        private void OnDisable() { inside = 0; Apply(); }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<RaceCameraProbe>() == null) return;
            inside++; Apply();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<RaceCameraProbe>() == null) return;
            inside = Mathf.Max(0, inside - 1); Apply();
        }

        private void Apply()
        {
            if (ZoneCamera != null) ZoneCamera.Priority = IsLive ? ActivePriority : InactivePriority;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = IsLive ? new Color(1f, .55f, .1f, .25f) : new Color(.1f, .8f, 1f, .12f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(.1f, .8f, 1f, .8f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
#endif
    }
}
