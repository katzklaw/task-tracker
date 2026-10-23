using BepInEx.Logging;

namespace TaskKillTracker;

public static class TKTLogger
{
    public static ManualLogSource Log;

    public static void Init(ManualLogSource logSource)
    {
        Log = logSource;
    }

    public static void Info(string msg) => Log?.LogInfo(msg);
    public static void Warn(string msg) => Log?.LogWarning(msg);
    public static void Error(string msg) => Log?.LogError(msg);
    public static void Exception(string msg, System.Exception ex = null) => Log?.LogError(ex != null ? $"{msg}: {ex}" : msg);
}