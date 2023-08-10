namespace MoreInjuriesPatch.Logging;

internal static class Logger
{
    public static void Log(string message)
    {
        if (MoreInjuriesPatchMod.Settings.enableLogging)
        {
            Verse.Log.Message($"[{nameof(MoreInjuriesPatch)}] {message}");
        }
    }

    public static void LogVerbose(string message)
    {
        if (MoreInjuriesPatchMod.Settings.enableVerboseLogging)
        {
            Log(message);
        }
    }

    public static void Error(string message) => 
        Verse.Log.Error($"[{nameof(MoreInjuriesPatch)}] {message}");

    public static void LogAlways(string message) =>
        Verse.Log.Message($"[{nameof(MoreInjuriesPatch)}] {message}");
}
