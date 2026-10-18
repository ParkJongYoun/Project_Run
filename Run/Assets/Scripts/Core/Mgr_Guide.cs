using System.Collections.Generic;
using UnityEngine;

namespace ProjectRun.Race
{
    // Guide choices belong to this running session, never to the user save.
    public static class Mgr_Guide
    {
        public static bool Enabled { get; private set; } = true;
        private static readonly HashSet<string> shownScreens = new HashSet<string>();
        public static bool HasShownAny => shownScreens.Count > 0;
        public static bool HasShown(string screen) => shownScreens.Contains(screen);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Enabled = true; shownScreens.Clear(); }

        // Screen hints and button prompts belong to the current demo.
        // A skip remains in effect until the next application run (or explicit settings toggle).
        public static void BeginSession() => shownScreens.Clear();
        public static bool TryShow(string screen) => Enabled && shownScreens.Add(screen);
        public static void SetEnabled(bool enabled) { Enabled = enabled; if (enabled) shownScreens.Clear(); }
    }
}
