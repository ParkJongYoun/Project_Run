using UnityEditor;

namespace ProjectRun.Editor
{
    public static class PlayModeWebBuild
    {
        [MenuItem("Tools/Project Run/Build Web - Game")]
        public static void Build() => ProjectRun.Race.Editor.DemoWebBuild.Queue();

    }
}
