using UnityEngine;

namespace ProjectRun.Ranch
{
    // Keep the playable trio in the ranch and choose three distinct guests per scene entry.
    [DefaultExecutionOrder(-300)]
    public sealed class Sys_RanchPopulation : MonoBehaviour
    {
        public Sys_RanchResident[] PlayableResidents;
        public Sys_RanchResident[] GuestResidents;
        public Vector3[] Positions;

        private void Awake()
        {
            if (PlayableResidents == null || PlayableResidents.Length != 3 ||
                GuestResidents == null || GuestResidents.Length < 3 || Positions == null || Positions.Length != 6)
            {
                Debug.LogError("Ranch population requires three playable residents, at least three guests and six positions.", this);
                return;
            }
            foreach (var guest in GuestResidents) guest.gameObject.SetActive(false);
            for (int i = 0; i < PlayableResidents.Length; i++) Place(PlayableResidents[i], i);
            var indices = new int[GuestResidents.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            for (int i = 0; i < 3; i++)
            {
                int pick = Random.Range(i, indices.Length);
                int swap = indices[i]; indices[i] = indices[pick]; indices[pick] = swap;
                Place(GuestResidents[indices[i]], i + 3);
            }
        }

        private void Place(Sys_RanchResident resident, int slot)
        {
            resident.transform.localPosition = Positions[slot];
            resident.gameObject.SetActive(true);
        }
    }
}
