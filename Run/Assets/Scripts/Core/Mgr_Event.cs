using System;
namespace ProjectRun.Race
{
    public static class Mgr_Event
    {
        public static event Action<Data_RaceEvent> RaceChanged;
        public static event Action Reset;
        public static void Publish(Data_RaceEvent value) => RaceChanged?.Invoke(value);
        public static void RaceReset() => Reset?.Invoke();
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize() { RaceChanged = null; Reset = null; }
    }
}
