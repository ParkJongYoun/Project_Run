using UnityEngine;

namespace ProjectRun.Race.CameraRig
{
    /// <summary>
    /// Physics probe that follows the protagonist (selected runner).
    /// Runners have no colliders, so this kinematic trigger body is what enters/leaves <see cref="RaceCameraZone"/> volumes.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class RaceCameraProbe : MonoBehaviour
    {
        [Tooltip("Transform the probe follows. Set at runtime by Sys_RaceCinemachine.")]
        public Transform Target;

        private Rigidbody body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            GetComponent<SphereCollider>().isTrigger = true;
        }

        public void SetTarget(Transform target)
        {
            Target = target;
            Snap();
        }

        /// <summary>Teleport to the target immediately (used on selection change / race restart).</summary>
        public void Snap()
        {
            if (Target == null) return;
            transform.position = Target.position;
            if (body != null) body.position = Target.position;
        }

        private void FixedUpdate()
        {
            if (Target != null) body.MovePosition(Target.position);
        }
    }
}
