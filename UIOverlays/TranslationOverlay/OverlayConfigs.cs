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
    private readonly TranslationOverlay cutSceneSelectStringOverlay = new();
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
        PluginRuntimeLog.Debug("Registering overlays...");

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.talkOverlay,
                () => TranslationWindowConfig.FromConfigForTalk(this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateTalk &&
                    NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                        this.configuration.TalkTranslationDisplayMode,
                        this.configuration.OverlayOnlyLanguage),
                syncBeforeDraw: () =>
                    this.TrySyncOverlayToAddon("Talk", this.talkOverlay)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.battleTalkOverlay,
                () => TranslationWindowConfig.FromConfigForBattleTalk(
                    this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateBattleTalk &&
                    NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                        this.configuration.BattleTalkTranslationDisplayMode,
                        this.configuration.OverlayOnlyLanguage),
                syncBeforeDraw: () =>
                    this.TrySyncOverlayToAddon("_BattleTalk", this.battleTalkOverlay)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.talkSubtitleOverlay,
                () => TranslationWindowConfig.FromConfigTalkSubtitle(
                    this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateTalkSubtitle &&
                    NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                        this.configuration.TalkSubtitleTranslationDisplayMode,
                        this.configuration.OverlayOnlyLanguage),
                syncBeforeDraw: () =>
                    this.TrySyncOverlayToAddon("TalkSubtitle", this.talkSubtitleOverlay)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.cutSceneSelectStringOverlay,
                () => TranslationWindowConfig.FromConfigForCutSceneSelectString(
                    this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateCutSceneSelectString &&
                    NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                        this.configuration.CutSceneSelectStringTranslationDisplayMode,
                        this.configuration.OverlayOnlyLanguage),
                syncBeforeDraw: () =>
                    this.TrySyncOverlayToAddon(
                        "CutSceneSelectString",
                        this.cutSceneSelectStringOverlay)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.textGimmickHintOverlay,
                () => TranslationWindowConfig.FromConfigForTextGimmickHint(
                    this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateTextGimmickHint &&
                    NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                        this.configuration.TextGimmickHintTranslationDisplayMode,
                        this.configuration.OverlayOnlyLanguage),
                syncBeforeDraw: () =>
                    this.TrySyncToastOverlayToAddon(
                        "_TextGimmickHint",
                        this.textGimmickHintOverlay,
                        AddonTextNodeResolvers.ResolveFirstTextNode)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.toastOverlay,
                () => TranslationWindowConfig.FromConfigForWideTextToast(this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateToast &&
                    this.configuration.TranslateWideTextToast &&
                    NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                        this.configuration.WideTextToastTranslationDisplayMode,
                        this.configuration.OverlayOnlyLanguage),
                syncBeforeDraw: () =>
                    this.TrySyncToastOverlayToAddon(
                        "_WideText",
                        this.toastOverlay,
                        AddonTextNodeResolvers.ResolveWideTextNode)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.errorToastOverlay,
                () => TranslationWindowConfig.FromConfigForErrorToast(
                    this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateToast &&
                    this.configuration.TranslateErrorToast &&
                    NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                        this.configuration.ErrorToastTranslationDisplayMode,
                        this.configuration.OverlayOnlyLanguage),
                syncBeforeDraw: () =>
                    this.TrySyncToastOverlayToAddon(
                        "_TextError",
                        this.errorToastOverlay,
                        AddonTextNodeResolvers.ResolveFirstTextNode)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.areaToastOverlay,
                () => TranslationWindowConfig.FromConfigForAreaToast(
                    this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateToast &&
                    this.configuration.TranslateAreaToast &&
                    NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                        this.configuration.AreaToastTranslationDisplayMode,
                        this.configuration.OverlayOnlyLanguage),
                syncBeforeDraw: () =>
                    this.TrySyncToastOverlayToAddon(
                        "_AreaText",
                        this.areaToastOverlay,
                        AddonTextNodeResolvers.ResolveFirstTextNode)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.classChangeToastOverlay,
                () => TranslationWindowConfig.FromConfigForClassChangeToast(
                    this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateToast &&
                    this.configuration.TranslateClassChangeToast &&
                    NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                        this.configuration.ClassChangeToastTranslationDisplayMode,
                        this.configuration.OverlayOnlyLanguage),
                syncBeforeDraw: () =>
                    this.TrySyncToastOverlayToAddon(
                        "_TextClassChange",
                        this.classChangeToastOverlay,
                        AddonTextNodeResolvers.ResolveFirstTextNode)));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.questToastOverlay,
                () => TranslationWindowConfig.FromConfigForQuestToast(
                    this.configuration),
                isEnabled: () =>
                    this.configuration.TranslateToast &&
                    this.configuration.TranslateQuestToast &&
                    NativeUI.Helpers.TranslationDisplayModeHelper.UsesOverlayPresentation(
                        this.configuration.QuestToastTranslationDisplayMode,
                        this.configuration.OverlayOnlyLanguage),
                syncBeforeDraw: this.TrySyncQuestToastOverlayToViewport));

        this.registeredOverlays.Add(
            new OverlayRegistration(
                this.chatBubbleOverlay,
                () => TranslationWindowConfig.FromConfigForChatBubble(
                    this.configuration)));

        PluginRuntimeLog.Debug(
            $"Overlays registered: {this.registeredOverlays.Count} ");
    }
}


