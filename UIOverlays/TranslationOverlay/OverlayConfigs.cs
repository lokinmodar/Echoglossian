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
    private readonly TranslationOverlay areaToastOverlay = new();
    private readonly TranslationOverlay battleTalkOverlay = new();
    private readonly TranslationOverlay classChangeToastOverlay = new();
    private readonly TranslationOverlay chatBubbleOverlay = new();
    private readonly TranslationOverlay errorToastOverlay = new();
    private readonly TranslationOverlay textGimmickHintOverlay = new();
    private readonly TranslationOverlay questToastOverlay = new();

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
    private unsafe void RegisterOverlays()
    {
        PluginLog.Debug("Registering overlays...");

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.talkOverlay,
                () => TranslationWindowConfig.FromConfigForTalk(this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateTalk &&
                    this.configuration.UseImGuiForTalk,
                syncBeforeDraw: () =>
                    this.TrySyncOverlayToAddon("Talk", this.talkOverlay)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.battleTalkOverlay,
                () => TranslationWindowConfig.FromConfigForBattleTalk(
                    this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateBattleTalk &&
                    this.configuration.UseImGuiForBattleTalk,
                syncBeforeDraw: () =>
                    this.TrySyncOverlayToAddon("_BattleTalk", this.battleTalkOverlay)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.talkSubtitleOverlay,
                () => TranslationWindowConfig.FromConfigTalkSubtitle(
                    this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateTalkSubtitle &&
                    this.configuration.UseImGuiForTalkSubtitle,
                syncBeforeDraw: () =>
                    this.TrySyncOverlayToAddon("TalkSubtitle", this.talkSubtitleOverlay)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.textGimmickHintOverlay,
                () => TranslationWindowConfig.FromConfigForTextGimmickHint(
                    this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateTextGimmickHint &&
                    this.configuration.UseImGuiForTextGimmickHint,
                syncBeforeDraw: () =>
                    this.TrySyncToastOverlayToAddon(
                        "_TextGimmickHint",
                        this.textGimmickHintOverlay,
                        ToastTextNodeResolvers.ResolveFirstTextNode)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.toastOverlay,
                () => TranslationWindowConfig.FromConfigForWideTextToast(this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateToast &&
                    this.configuration.TranslateWideTextToast &&
                    (this.configuration.OverlayOnlyLanguage ||
                     this.configuration.UseImGuiForWideTextToast),
                syncBeforeDraw: () =>
                    this.TrySyncToastOverlayToAddon(
                        "_WideText",
                        this.toastOverlay,
                        ToastTextNodeResolvers.ResolveWideTextNode)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.errorToastOverlay,
                () => TranslationWindowConfig.FromConfigForErrorToast(
                    this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateToast &&
                    this.configuration.TranslateErrorToast &&
                    (this.configuration.OverlayOnlyLanguage ||
                     this.configuration.UseImGuiForErrorToast),
                syncBeforeDraw: () =>
                    this.TrySyncToastOverlayToAddon(
                        "_TextError",
                        this.errorToastOverlay,
                        ToastTextNodeResolvers.ResolveFirstTextNode)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.areaToastOverlay,
                () => TranslationWindowConfig.FromConfigForAreaToast(
                    this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateToast &&
                    this.configuration.TranslateAreaToast &&
                    (this.configuration.OverlayOnlyLanguage ||
                     this.configuration.UseImGuiForAreaToast),
                syncBeforeDraw: () =>
                    this.TrySyncToastOverlayToAddon(
                        "_AreaText",
                        this.areaToastOverlay,
                        ToastTextNodeResolvers.ResolveFirstTextNode)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.classChangeToastOverlay,
                () => TranslationWindowConfig.FromConfigForClassChangeToast(
                    this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateToast &&
                    this.configuration.TranslateClassChangeToast &&
                    (this.configuration.OverlayOnlyLanguage ||
                     this.configuration.UseImGuiForClassChangeToast),
                syncBeforeDraw: () =>
                    this.TrySyncToastOverlayToAddon(
                        "_TextClassChange",
                        this.classChangeToastOverlay,
                        ToastTextNodeResolvers.ResolveFirstTextNode)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.questToastOverlay,
                () => TranslationWindowConfig.FromConfigForQuestToast(
                    this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateToast &&
                    this.configuration.TranslateQuestToast &&
                    (this.configuration.OverlayOnlyLanguage ||
                     this.configuration.UseImGuiForQuestToast),
                syncBeforeDraw: this.TrySyncQuestToastOverlayToViewport));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.chatBubbleOverlay,
                () => TranslationWindowConfig.FromConfigForChatBubble(
                    this.configuration)));

        PluginLog.Debug(
            $"Overlays registered: {this.registeredOverlays.Count} ");
    }
}
