namespace ProjectRun.Race
{
    /// <summary>Race scenes are named by course, with Toon identifying the updated six-monster assets.</summary>
    public static class Define_Scene
    {
        public const string Title = "Scene_Title";
        public const string Oval = "Scene_Oval";
        public const string Prontera = "Scene_Prontera";
        public const string OvalToon = "Scene_OvalToon";
        public const string PronteraToon = "Scene_PronteraToon";
        public const string Lobby = "Scene_LobbyTest";
        public const string Sprite2D = "Scene_2D";
        public static string Path(string name) => "Assets/Scenes/" + name + ".unity";
    }
}
