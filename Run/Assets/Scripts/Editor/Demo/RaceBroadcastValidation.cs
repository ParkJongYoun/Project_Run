namespace ProjectRun.Race.Editor
{
    // Keep the existing verification entry point aligned with the current race rules.
    public static class RaceBroadcastValidation
    {
        public static string Run(int count = 36, bool trained = false) => RaceTacticsValidation.Run(count, trained);
    }
}
