using HarmonyLib;
using System.Collections.Generic;

namespace TaskKillTracker;

// Tracks kills for the current round, keyed by the killer's PlayerId.
// Runs on every client (not host-only) since PlayerControl.MurderPlayer
// executes locally whenever the kill RPC is received/replayed, so a dead
// spectator's own client will still have accurate counts.
public static class KillTracker
{
    public static readonly Dictionary<byte, int> Kills = new();

    // Tracks which victims have already been counted this round. A
    // player can only die once per round, so if MurderPlayer somehow
    // fires more than once for the same victim (e.g. once locally and
    // again when the confirming RPC arrives -- this has been observed
    // causing kills to be over-counted), this stops the second call
    // from counting as an additional kill.
    private static readonly HashSet<byte> CountedVictims = new();

    public static int GetKills(byte playerId) => Kills.TryGetValue(playerId, out int n) ? n : 0;

    public static void ResetRound()
    {
        Kills.Clear();
        CountedVictims.Clear();
    }

    public static void RecordKill(byte killerId, byte victimId)
    {
        if (!CountedVictims.Add(victimId))
            return; // this victim's death was already counted -- skip.

        Kills.TryAdd(killerId, 0);
        Kills[killerId]++;
    }
}

// NOTE: MurderPlayer's exact signature has changed across Among Us
// versions -- some builds use MurderPlayer(PlayerControl target,
// MurderResultFlags resultFlags), others just MurderPlayer(PlayerControl
// target). If this patch fails to bind at Harmony.PatchAll() time, use
// Ctrl+Shift+O on PlayerControl in the decompiled assembly to confirm the
// current overload and adjust the HarmonyPatch attribute/argument list.
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
public static class MurderPlayer_TrackKillsPatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        try
        {
            if (__instance == null || target == null)
                return;

            // Don't count self-murder / exile-triggered calls as a kill.
            if (__instance.PlayerId == target.PlayerId)
                return;

            // Don't count a disconnect as a kill. Some builds internally
            // call MurderPlayer as part of disconnect cleanup (to mark
            // the leaving player dead), attributing it to an arbitrary
            // PlayerControl rather than a real killer -- this filters
            // those out by checking whether the "victim" has actually
            // left the game.
            if (target.Data == null || target.Data.Disconnected)
                return;

            KillTracker.RecordKill(__instance.PlayerId, target.PlayerId);
        }
        catch (System.Exception ex)
        {
            TKTLogger.Exception("[TaskKillTracker] MurderPlayer_TrackKillsPatch failed", ex);
        }
    }
}

// Clear kill counts at the start of each new round. Two separate hooks
// are used as a safety net against version mismatches: ShipStatus.Begin
// is the more "obvious" choice, but given this codebase has already hit
// multiple cases where a plausible-looking member doesn't exist at
// runtime in this specific game build, HudManager.CoShowIntro is also
// patched -- that's the confirmed-working hook AmongUsRevamped's real,
// tested source uses for exactly this purpose (resetting its own kill
// counter dictionary at the start of each round's intro). Calling
// ResetRound() twice back-to-back is harmless, so both are kept; if one
// of the two doesn't exist in this build, the resilient per-class
// patcher will just skip it and log a warning rather than breaking
// anything, and the other hook still covers the reset.
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Begin))]
public static class ClearKillsOnGameStartPatch
{
    public static void Postfix() => KillTracker.ResetRound();
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.CoShowIntro))]
public static class ClearKillsOnIntroPatch
{
    public static void Postfix() => KillTracker.ResetRound();
}