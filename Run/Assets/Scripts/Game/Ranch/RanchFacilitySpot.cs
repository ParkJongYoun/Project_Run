using UnityEngine;

namespace ProjectRun.Ranch
{
    public enum E_RanchActivity { Bath, Track, Training, Feeding, Rest }

    // A reservation lasts through the approach, activity and exit, preventing two visitors from entering together.
    public sealed class RanchFacilitySpot : MonoBehaviour
    {
        public E_RanchActivity Activity;
        public Vector3 Entry;
        public Vector3 ActionPoint;
        public Vector3[] EntryPoints;
        public Vector3 Facing = Vector3.forward;
        public Vector3[] TrackPoints;
        public float Duration = 6;
        public Sys_RanchResident Occupant { get; private set; }
        public bool TryReserve(Sys_RanchResident resident)
        {
            if (Occupant != null && Occupant != resident) return false;
            Occupant = resident; return true;
        }
        public void Release(Sys_RanchResident resident) { if (Occupant == resident) Occupant = null; }
        private void OnDisable() { Occupant = null; }
    }
}
