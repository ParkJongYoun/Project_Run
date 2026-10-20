using UnityEngine;

namespace ProjectRun.Ranch
{
    // The ranch always contains the playable trio and the same three fixed race opponents.
    [DefaultExecutionOrder(-300)]
    public sealed class Sys_RanchPopulation : MonoBehaviour
    {
        public Sys_RanchResident[] PlayableResidents;
        public Sys_RanchResident[] GuestResidents;
        public Vector3[] Positions;

        private void Awake()
        {
            if (PlayableResidents == null || PlayableResidents.Length != 3 ||
                GuestResidents == null || GuestResidents.Length != 3 || Positions == null || Positions.Length != 6)
            {
                Debug.LogError("Ranch population requires three playable residents, three fixed guests and six positions.", this);
                return;
            }
            for (int i = 0; i < PlayableResidents.Length; i++) Place(PlayableResidents[i], i);
            for (int i = 0; i < GuestResidents.Length; i++) Place(GuestResidents[i], i + 3);
        }

        private void Place(Sys_RanchResident resident, int slot)
        {
            resident.transform.localPosition = Positions[slot];
            resident.gameObject.SetActive(true);
        }
    }
}
