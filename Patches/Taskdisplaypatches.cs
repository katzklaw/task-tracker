using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;

namespace TaskKillTracker;

public static class TaskUtils
{
    // NetworkedPlayerInfo.Tasks holds NetworkedPlayerInfo.TaskInfo entries
    // with a `.Complete` bool. This reads directly off the synced
    // NetworkedPlayerInfo rather than tracking our own dictionary, so it
    // works correctly for every client (not just the host) -- task
    // completion state is part of the networked GameData every client
    // already receives.
    public static (int completed, int total) GetTaskCounts(NetworkedPlayerInfo info)
    {
        if (info == null || info.Tasks == null)
            return (0, 0);

        int total = info.Tasks.Count;
        int completed = info.Tasks.ToArray().Count(t => t != null && t.Complete);

        return (completed, total);
    }
}

// Captures the active MeetingHud the moment one starts, so
// MeetingRevealPatch doesn't need an unconfirmed static .Instance.
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
public static class CaptureActiveMeetingPatch
{
    public static void Postfix(MeetingHud __instance)
    {
        MeetingRevealPatch.SetActive(__instance);

        int rowCount = __instance.playerStates?.Count ?? -1;
        bool localDead = PlayerControl.LocalPlayer?.Data?.IsDead ?? false;
        TKTLogger.Info($"[TaskKillTracker] Meeting started. playerStates count={rowCount}, LocalPlayer.IsDead={localDead}");
    }
}

// While you are dead, reveals everyone's task completion and each
// impostor's kill count on the meeting/vote screen, by cloning a new
// TextMeshPro label as a child of each row's existing name text rather
// than overwriting it (overwriting it gets silently reverted by vanilla
// for any still-connected player).
//
// This runs every frame via PlayerControl.FixedUpdate (confirmed
// reliable -- it's what's already driving the working own-nametag
// counter) instead of doing the work once at MeetingHud.Start, because
// Start appears to fire before each row's PlayerVoteArea/NameText is
// actually populated (they're filled in slightly later by the meeting's
// intro/"beam in" animation). Polling every frame and only creating the
// label once it's actually available -- and only once per row -- avoids
// that timing problem entirely.
public static class MeetingRevealPatch
{
    private const string LabelName = "TaskKillTracker_MeetingText";

    private static MeetingHud _active;
    private static bool _loggedThisMeeting;

    public static void SetActive(MeetingHud hud)
    {
        _active = hud;
        _loggedThisMeeting = false;
    }

    public static void Tick()
    {
        try
        {
            // _active correctly evaluates as null once the underlying
            // IL2CPP object is destroyed (same pattern used elsewhere in
            // this codebase for despawned objects), so no extra cleanup
            // is needed when a meeting ends.
            if (_active == null)
                return;

            if (!TKTOptions.ShowAllTasksWhenDead && !TKTOptions.ShowKillCountsWhenDead)
                return;

            if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null)
                return;

            // Only reveal this info once you're dead -- matches the
            // request ("on the vote screen when I am dead").
            if (!PlayerControl.LocalPlayer.Data.IsDead)
                return;

            if (_active.playerStates == null)
                return;

            if (!_loggedThisMeeting)
            {
                _loggedThisMeeting = true;
                TKTLogger.Info($"[TaskKillTracker] Tick running with local player dead. playerStates count={_active.playerStates.Count}");
            }

            int added = 0;
            int skippedNoTemplate = 0;
            int skippedNoPlayer = 0;
            int skippedNoProgressText = 0;

            foreach (var playerState in _active.playerStates)
            {
                if (playerState == null)
                    continue;

                var textTemplate = playerState.NameText;
                if (textTemplate == null)
                {
                    skippedNoTemplate++;
                    continue;
                }

                if (HasLabelAlready(textTemplate.transform))
                    continue;

                // Match this row to a player by the plain name vanilla
                // already put in NameText, rather than TargetPlayerId
                // (confirmed missing at runtime in this build -- see the
                // now-removed CaptureTargetPlayerIdPatch/get_TargetPlayerId
                // MissingMethodException). We haven't modified NameText's
                // own text anywhere in this mod, only added a child label,
                // so it should still hold vanilla's plain name here.
                string rowName = textTemplate.text;

                PlayerControl player = PlayerControl.AllPlayerControls
                    .ToArray()
                    .FirstOrDefault(pc => pc != null && pc.Data != null && pc.Data.PlayerName == rowName);

                if (player == null || player.Data == null)
                {
                    skippedNoPlayer++;
                    continue;
                }

                string progressText = GetProgressText(player);
                if (string.IsNullOrEmpty(progressText))
                {
                    skippedNoProgressText++;
                    continue;
                }

                var extraText = UnityEngine.Object.Instantiate(textTemplate);
                extraText.transform.SetParent(textTemplate.transform);
                extraText.transform.localPosition = new Vector3(0f, 0.18f, 0f);
                extraText.fontSize = 1.6f;
                extraText.text = progressText;
                extraText.gameObject.name = LabelName;
                extraText.enableWordWrapping = false;
                extraText.enabled = true;
                added++;
            }

            if (added > 0 || skippedNoTemplate > 0 || skippedNoPlayer > 0 || skippedNoProgressText > 0)
            {
                TKTLogger.Info($"[TaskKillTracker] Tick pass: added={added}, skippedNoTemplate={skippedNoTemplate}, skippedNoPlayer={skippedNoPlayer}, skippedNoProgressText={skippedNoProgressText}");
            }
        }
        catch (Exception ex)
        {
            TKTLogger.Exception("[TaskKillTracker] MeetingRevealPatch.Tick failed", ex);
        }
    }

    private static bool HasLabelAlready(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            if (parent.GetChild(i).name == LabelName)
                return true;
        }

        return false;
    }

    private static string GetProgressText(PlayerControl player)
    {
        bool isImpostor = player.Data.Role != null && player.Data.Role.IsImpostor;

        if (isImpostor)
        {
            if (!TKTOptions.ShowKillCountsWhenDead)
                return null;

            int kills = KillTracker.GetKills(player.Data.PlayerId);
            return $"<color=red>({kills}\u2020)</color>"; // dagger symbol
        }
        else
        {
            if (!TKTOptions.ShowAllTasksWhenDead)
                return null;

            var (completed, total) = TaskUtils.GetTaskCounts(player.Data);
            if (total == 0)
                return null;

            return $"<color=green>({completed}/{total})</color>";
        }
    }
}

// Appends "(completed/total)" to your own in-world nametag, and drives
// MeetingRevealPatch.Tick() every frame (see that class for why it's
// polled here rather than done once at MeetingHud.Start). Nametag
// changes only affect what renders locally on your own client -- other
// players' clients render your nametag from the networked PlayerName,
// not from this TextMeshPro object, so it's not visible to anyone else.
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class OwnNameTaskCounterPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == null || !__instance.AmOwner)
            return;

        try
        {
            MeetingRevealPatch.Tick();
        }
        catch (Exception ex)
        {
            TKTLogger.Exception("[TaskKillTracker] MeetingRevealPatch.Tick call failed", ex);
        }

        try
        {
            if (!TKTOptions.ShowOwnTaskCounter)
                return;

            if (__instance.Data == null || AmongUsClient.Instance == null || !AmongUsClient.Instance.IsGameStarted)
                return;

            // Impostors' "tasks" are fake and never complete, so skip
            // showing a counter for them -- it'd always read (0/N).
            if (__instance.Data.Role != null && __instance.Data.Role.IsImpostor)
                return;

            var nameText = __instance.cosmetics?.nameText;
            if (nameText == null)
                return;

            var (completed, total) = TaskUtils.GetTaskCounts(__instance.Data);
            if (total == 0)
                return;

            string suffixed = $"{__instance.Data.PlayerName} ({completed}/{total})";

            if (nameText.text != suffixed)
                nameText.text = suffixed;
        }
        catch (Exception ex)
        {
            TKTLogger.Exception("[TaskKillTracker] OwnNameTaskCounterPatch failed", ex);
        }
    }
}