using System;
using System.Collections.Generic;
using System.IO;

namespace TaskKillTracker;

// Simple text-file config, same pattern as BanListMod's Options.cs.
// No in-game settings UI in v1 -- just edit the file and rejoin/restart
// if you want to change these. Ask if you'd like a settings menu added
// later (kept out for now to avoid the GUILayout.TextField IL2CPP crash
// you've already hit in BanListMod).
public static class TKTOptions
{
    private const string ConfigPath = "./TASKKILL_DATA/SETTINGS/TaskKillTracker_config.txt";

    // Appends "(completed/total)" to your own in-world nametag.
    public static bool ShowOwnTaskCounter = true;

    // While you are dead, reveals every player's task completion on the
    // meeting/vote screen (calls PlayerVoteArea.SetTasks for everyone,
    // not just yourself).
    public static bool ShowAllTasksWhenDead = true;

    // While you are dead, appends each impostor's kill count for the
    // current round to their name on the meeting/vote screen.
    public static bool ShowKillCountsWhenDead = true;

    // Turns the chat box background dark gray. Chat text color is
    // controlled separately by ChatTextColorHex below.
    public static bool DarkModeChat = false;

    // Hex color (e.g. "#FFFFFF") for chat text when DarkModeChat is on.
    // Falls back to white if this isn't a valid hex code.
    public static string ChatTextColorHex = "#FFFFFF";

    public static void Load()
    {
        try
        {
            Directory.CreateDirectory("./TASKKILL_DATA/SETTINGS");

            if (!File.Exists(ConfigPath))
            {
                Save();
                return;
            }

            var map = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(ConfigPath))
            {
                int idx = line.IndexOf('=');
                if (idx <= 0) continue;
                map[line[..idx].Trim()] = line[(idx + 1)..].Trim();
            }

            bool GetB(string key, bool def) => map.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : def;
            string GetS(string key, string def) => map.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : def;

            ShowOwnTaskCounter = GetB(nameof(ShowOwnTaskCounter), ShowOwnTaskCounter);
            ShowAllTasksWhenDead = GetB(nameof(ShowAllTasksWhenDead), ShowAllTasksWhenDead);
            ShowKillCountsWhenDead = GetB(nameof(ShowKillCountsWhenDead), ShowKillCountsWhenDead);
            DarkModeChat = GetB(nameof(DarkModeChat), DarkModeChat);
            ChatTextColorHex = GetS(nameof(ChatTextColorHex), ChatTextColorHex);

            // Re-save so an older config file (from before a setting was
            // added) picks up any new keys instead of silently missing
            // them forever -- Save() only ever runs once, at first-ever
            // creation, otherwise.
            Save();
        }
        catch (Exception ex)
        {
            TKTLogger.Exception("[TaskKillTracker] Options.Load failed", ex);
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory("./TASKKILL_DATA/SETTINGS");

            var lines = new List<string>
            {
                $"{nameof(ShowOwnTaskCounter)}={ShowOwnTaskCounter}",
                $"{nameof(ShowAllTasksWhenDead)}={ShowAllTasksWhenDead}",
                $"{nameof(ShowKillCountsWhenDead)}={ShowKillCountsWhenDead}",
                $"{nameof(DarkModeChat)}={DarkModeChat}",
                $"{nameof(ChatTextColorHex)}={ChatTextColorHex}",
            };

            File.WriteAllLines(ConfigPath, lines);
        }
        catch (Exception ex)
        {
            TKTLogger.Exception("[TaskKillTracker] Options.Save failed", ex);
        }
    }
}