// <copyright file="OverlayConfigs.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Manages the registration and configuration of translation overlays.
/// </summary>
public partial class Echoglossian
{
    private readonly TranslationOverlay battleTalkOverlay = new();
    private readonly TranslationOverlay chatBubbleOverlay = new();
    private readonly TranslationOverlay errorToastOverlay = new();

    // List of registered overlays
    private readonly List<OverlayRegistration> registeredOverlays = new();

    // Overlays
    private readonly TranslationOverlay talkOverlay = new();
    private readonly TranslationOverlay talkSubtitleOverlay = new();
    private readonly TranslationOverlay toastOverlay = new();

    /// <summary>
    ///     Registers the overlays with their respective configurations using current
    ///     plugin config values.
    /// </summary>
    private void RegisterOverlays()
    {
        PluginLog.Debug("Registering overlays...");

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.talkOverlay,
                TranslationWindowConfig.FromConfigForTalk(this.configuration)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.battleTalkOverlay,
                TranslationWindowConfig.FromConfigForBattleTalk(
                    this.configuration)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.talkSubtitleOverlay,
                TranslationWindowConfig.FromConfigTalkSubtitle(
                    this.configuration)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.toastOverlay,
                TranslationWindowConfig.FromConfigForToast(
                    this.configuration)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.errorToastOverlay,
                TranslationWindowConfig.FromConfigForErrorToast(
                    this.configuration)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.chatBubbleOverlay,
                TranslationWindowConfig.FromConfigForChatBubble(
                    this.configuration)));

        PluginLog.Debug(
            $"Overlays registered: {this.registeredOverlays.Count} ");
    }
}