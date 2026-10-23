using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;

namespace TaskKillTracker;

[BepInPlugin(PluginGuid, "TaskKillTracker", PluginVersion)]
[BepInProcess("Among Us.exe")]
public class TaskKillTrackerPlugin : BasePlugin
{
    public const string PluginGuid = "com.katzklaw.taskkilltracker";
    public const string PluginVersion = "1.0.0";

    public Harmony Harmony { get; } = new(PluginGuid);
    public static TaskKillTrackerPlugin Instance;

    public override void Load()
    {
        Instance = this;
        TKTLogger.Init(Log);

        try
        {
            TKTOptions.Load();

            PatchAllResilient();

            TKTLogger.Info($"TaskKillTracker {PluginVersion} loaded.");
        }
        catch (Exception ex)
        {
            TKTLogger.Error("TaskKillTracker failed to load: " + ex);
        }
    }

    // Applies each [HarmonyPatch]-decorated class independently, instead
    // of one Harmony.PatchAll() call for the whole assembly. PatchAll
    // aborts entirely the moment any single class fails to bind (e.g. a
    // method that exists in the AmongUs.GameLibs.Steam reference package
    // we compile against but is missing from this specific installed
    // game build's runtime assembly -- this has already happened once
    // with PlayerVoteArea.SetTargetPlayerId, which silently took down
    // every other patch in the mod, including unrelated working ones).
    //
    // Patching class-by-class means a single broken/mismatched member
    // (for example, if a future game update renames something in
    // DarkModePatches) only disables that one feature and logs a
    // warning, instead of breaking the entire mod.
    private void PatchAllResilient()
    {
        var patchClasses = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<HarmonyPatch>() != null)
            .ToList();

        int succeeded = 0;
        int failed = 0;

        foreach (var type in patchClasses)
        {
            try
            {
                Harmony.CreateClassProcessor(type).Patch();
                succeeded++;
            }
            catch (Exception ex)
            {
                failed++;
                TKTLogger.Warn($"[TaskKillTracker] Skipped patch '{type.FullName}' (not compatible with this game build): {ex.Message}");
            }
        }

        TKTLogger.Info($"[TaskKillTracker] Patching complete: {succeeded} succeeded, {failed} skipped.");
    }
}