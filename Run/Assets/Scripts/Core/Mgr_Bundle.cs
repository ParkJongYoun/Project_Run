using System.Collections.Generic;
using UnityEngine;
namespace ProjectRun.Race
{
    public static class Mgr_Bundle
    {
        private static readonly Dictionary<string, Object> cache = new Dictionary<string, Object>();
        public static T Load<T>(string path) where T : Object
        {
            if (cache.TryGetValue(path, out var asset)) return asset as T;
            var result = Resources.Load<T>(path);
            if (result == null) Debug.LogError("Missing resource: " + path);
            else cache[path] = result;
            return result;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize() => cache.Clear();
    }
}
