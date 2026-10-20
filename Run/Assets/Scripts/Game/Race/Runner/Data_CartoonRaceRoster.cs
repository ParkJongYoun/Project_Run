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
            if (Playable == null || Playable.Length != 3 || Opponents == null || Opponents.Length != 3)
                throw new InvalidOperationException("Cartoon race requires three playable runners and three fixed opponents.");
            var entries = new Data_RunnerSetup[6];
            // Keep runner identities stable for player selection, skills and camera tracking.
            for (int i = 0; i < 3; i++)
            {
                entries[i] = Playable[i].Copy();
                entries[i + 3] = Opponents[i].Copy();
            }
            return entries;
        }
    }
}
