using UnityEditor;

namespace ProjectRun.Editor
{
    public static class PlayModeWebBuild
    {
        [MenuItem("Tools/Project Run/Build Web - Game")]
        public static void Build() => ProjectRun.Race.Editor.DemoWebBuild.Queue();

        [MenuItem("Tools/Project Run/Build Web - Test Scenes")]
        public static void BuildTestScenes() => ProjectRun.Race.Editor.DemoWebBuild.QueueTests();
    }
}
