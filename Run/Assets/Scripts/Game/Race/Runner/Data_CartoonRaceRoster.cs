using System;
using UnityEngine;

namespace ProjectRun.Race
{
    public sealed class Data_CartoonRaceRoster : ScriptableObject
    {
        public Data_RunnerSetup[] Playable;
        public Data_RunnerSetup[] Opponents;

        public Data_RunnerSetup[] CreateEntrants()
        {
            if (Playable == null || Playable.Length != 3 || Opponents == null || Opponents.Length < 3)
                throw new InvalidOperationException("Cartoon race requires three playable runners and at least three opponents.");
            var entries = new Data_RunnerSetup[6];
            for (int i = 0; i < 3; i++) entries[i] = Playable[i].Copy();
            var indices = new int[Opponents.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            for (int i = 0; i < 3; i++)
            {
                int pick = UnityEngine.Random.Range(i, indices.Length);
                int swap = indices[i]; indices[i] = indices[pick]; indices[pick] = swap;
                entries[i + 3] = Opponents[indices[i]].Copy();
            }
            return entries;
        }
    }
}
