using UnityEngine;

namespace ProjectRun.Race
{
    /// <summary>A soft, shadowless portrait fill for racers. The sun still supplies the world light and shadows.</summary>
    [ExecuteAlways, DefaultExecutionOrder(250)]
    public sealed class Sys_RaceLighting : MonoBehaviour
    {
        public Camera ViewCamera;
        public Light CharacterFill;
        public Vector2 CameraAngleOffset = new Vector2(15, 15);

        private void LateUpdate() => AlignToCamera();

        public void AlignToCamera()
        {
            if (ViewCamera == null || CharacterFill == null) return;
            var rotation = ViewCamera.transform.rotation * Quaternion.Euler(CameraAngleOffset.x, CameraAngleOffset.y, 0);
            if (Quaternion.Angle(CharacterFill.transform.rotation, rotation) > 0.01f)
                CharacterFill.transform.rotation = rotation;
        }
    }
}
