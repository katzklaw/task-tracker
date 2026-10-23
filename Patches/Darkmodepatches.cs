using HarmonyLib;
using System;
using UnityEngine;

namespace TaskKillTracker;

// Chat-box dark mode: dark gray background, configurable text color.
// Colors/technique adapted from VanillaEnhancements' DarkModePatches.cs
// (confirmed working against a real installed game), scoped down to
// just the chat pieces here rather than the whole UI.
//
// Controlled entirely via the config file (TKTOptions.DarkModeChat /
// ChatTextColorHex) -- no in-game toggle, per request.
public static class DarkModePatches
{
    private static Color TextColor
    {
        get
        {
            if (ColorUtility.TryParseHtmlString(TKTOptions.ChatTextColorHex, out var color))
                return color;

            // Falls back to white if the config has an invalid hex code
            // (e.g. missing the leading '#', or a typo).
            return Color.white;
        }
    }

    private static readonly Color DarkBackground = new(0.1f, 0.1f, 0.1f);
    private static readonly Color DarkerBackground = new(0.04f, 0.04f, 0.04f);

    // The chat panel's own background (the box itself, not individual
    // messages).
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
    public static class ChatControllerPatch
    {
        public static void Postfix(ChatController __instance)
        {
            try
            {
                if (!TKTOptions.DarkModeChat)
                    return;

                if (__instance.backgroundImage != null)
                    __instance.backgroundImage.color = DarkBackground;
            }
            catch (Exception ex)
            {
                TKTLogger.Exception("[TaskKillTracker] DarkMode ChatControllerPatch failed", ex);
            }
        }
    }

    // The text input box you type into at the bottom of the chat panel.
    [HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.Awake))]
    public static class FreeChatInputFieldPatch
    {
        public static void Postfix(FreeChatInputField __instance)
        {
            try
            {
                if (!TKTOptions.DarkModeChat)
                    return;

                if (__instance.background != null)
                    __instance.background.color = DarkBackground;

                var rollover = __instance.background?.GetComponent<ButtonRolloverHandler>();
                if (rollover != null)
                {
                    rollover.OutColor = DarkerBackground;
                    rollover.OverColor = new Color(0.25f, 0.25f, 0.25f);
                    rollover.UnselectedColor = DarkerBackground;
                }

                var textComp = __instance.textArea?.gameObject.GetComponent<TMPro.TextMeshPro>();
                if (textComp != null)
                    textComp.color = TextColor;
            }
            catch (Exception ex)
            {
                TKTLogger.Exception("[TaskKillTracker] DarkMode FreeChatInputFieldPatch failed", ex);
            }
        }
    }

    // Individual chat message bubbles (the classic chat bubble UI).
    [HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetCosmetics))]
    public static class ChatBubblePatch
    {
        public static void Postfix(ChatBubble __instance)
        {
            try
            {
                if (!TKTOptions.DarkModeChat)
                    return;

                if (__instance.Background != null)
                    __instance.Background.color = DarkBackground;

                if (__instance.MaskArea != null)
                    __instance.MaskArea.color = DarkerBackground;

                if (__instance.TextArea != null)
                {
                    __instance.TextArea.color = TextColor;

                    if (__instance.NameText != null)
                        __instance.TextArea.outlineWidth = __instance.NameText.outlineWidth * 0.75f;
                }
            }
            catch (Exception ex)
            {
                TKTLogger.Exception("[TaskKillTracker] DarkMode ChatBubblePatch failed", ex);
            }
        }
    }

    // The small popup notification that appears when a new chat message
    // arrives while the chat panel is closed.
    [HarmonyPatch(typeof(ChatNotification), nameof(ChatNotification.Awake))]
    public static class ChatNotificationPatch
    {
        public static void Postfix(ChatNotification __instance)
        {
            try
            {
                if (!TKTOptions.DarkModeChat)
                    return;

                if (__instance.background != null)
                    __instance.background.color = DarkBackground;

                if (__instance.chatText != null)
                    __instance.chatText.color = TextColor;
            }
            catch (Exception ex)
            {
                TKTLogger.Exception("[TaskKillTracker] DarkMode ChatNotificationPatch failed", ex);
            }
        }
    }
}