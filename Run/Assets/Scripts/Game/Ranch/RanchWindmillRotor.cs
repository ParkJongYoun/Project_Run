using UnityEngine;

namespace ProjectRun.Ranch
{
    public sealed class RanchWindmillRotor : MonoBehaviour
    {
        [SerializeField] private float DegreesPerSecond = 24;
        private void Update() { transform.Rotate(Vector3.forward, DegreesPerSecond * Time.deltaTime, Space.Self); }
    }
}
