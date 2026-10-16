using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRun.Race
{
    public static class Mgr_Scene
    {
        public static bool IsLoading { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => IsLoading = false;

        public static bool Load(string sceneName)
        {
            if (IsLoading) return false;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("Scene is missing from build settings: " + sceneName);
                return false;
            }
            IsLoading = true;
            try
            {
                var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (operation == null) { IsLoading = false; return false; }
                operation.completed += completed => IsLoading = false;
                return true;
            }
            catch (System.Exception exception)
            {
                IsLoading = false;
                Debug.LogException(exception);
                return false;
            }
        }
    }
}
