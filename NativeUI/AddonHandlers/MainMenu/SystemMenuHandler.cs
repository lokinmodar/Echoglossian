// <copyright file="SystemMenuHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.AddonHandlers.Toasts;
using Echoglossian.NativeUI.Helpers;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Echoglossian.NativeUI.AddonHandlers.MainMenu;

/// <summary>
///     Handles DB-first translation for the SystemMenu addon.
/// </summary>
public unsafe class SystemMenuHandler : DbFirstGameWindowAddonHandler
{
    private const uint MenuEntryTextNodeId = 2;
    private const uint MenuTitleTextNodeId = 3;

    private readonly Config config;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SystemMenuHandler" />
    ///     class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public SystemMenuHandler(
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService)
        : base(
            addonName: "SystemMenu",
            config: config,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService,
            enabledSelector: static configuration =>
                configuration.TranslateGameMainMenu,
            useAtkValues: false,
            useTextNodes: true,
            displayModeSelector: static configuration =>
                configuration.GameMainMenuWindowTranslationDisplayMode)
    {
        this.config = config;
    }

    /// <summary>
    ///     The SystemMenu addon reuses the same text-node surface for multiple
    ///     menu states, so broad compatible-payload reuse is not stable enough
    ///     to keep enabled.
    /// </summary>
    /// <returns>Always <see langword="false" />.</returns>
    protected override bool ShouldReuseCompatiblePayloads()
    {
        return false;
    }

    /// <summary>
    ///     The SystemMenu addon repaints the same visible text-node slots
    ///     across menu states, so stale translated values must be restored
    ///     before capturing the next original-facing payload.
    /// </summary>
    /// <returns>Always <see langword="true" />.</returns>
    protected override bool ShouldRestoreStaleTranslatedTextNodesOnPayloadChange()
    {
        return true;
    }

    /// <inheritdoc />
    protected override bool ShouldCaptureTextNode(
        AtkTextNode* textNode,
        string visibleText)
    {
        return textNode != null &&
               textNode->AtkResNode.NodeId is
                   MenuEntryTextNodeId or MenuTitleTextNodeId;
    }

    /// <inheritdoc />
    protected override List<nint> ResolveTextNodeAddresses(AtkUnitBase* addon)
    {
        return AddonTextNodeResolvers.ResolveReadableTextNodes(addon);
    }

    /// <inheritdoc />
    private protected override bool TryResolveSupplementalTranslatedPayload(
        SourceClientLanguage sourceLanguage,
        DbFirstGameWindowPayload originalPayload,
        out DbFirstGameWindowPayload translatedPayload)
    {
        translatedPayload = DbFirstGameWindowPayload.Empty;
        var scope = new TranslationReuseScope(
            sourceLanguage.PersistenceCode,
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.config.Lang),
            this.GetOperationTranslationEngineId(),
            this.config.TranslateAlreadyTranslatedTexts);

        if (!MainCommandCanonicalTextResolver.TryResolveTranslatedTextMap(
                originalPayload.TextNodes,
                scope,
                GetGameVersion(),
                out var translatedTextNodes))
        {
            return false;
        }

        translatedPayload = new DbFirstGameWindowPayload(
            [],
            [],
            translatedTextNodes);
        return true;
    }

    /// <inheritdoc />
    private protected override bool TryResolveSupplementalOriginalPayload(
        SourceClientLanguage sourceLanguage,
        DbFirstGameWindowPayload livePayload,
        out DbFirstGameWindowPayload originalPayload)
    {
        originalPayload = DbFirstGameWindowPayload.Empty;
        var scope = new TranslationReuseScope(
            sourceLanguage.PersistenceCode,
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.config.Lang),
            this.GetOperationTranslationEngineId(),
            this.config.TranslateAlreadyTranslatedTexts);

        if (!MainCommandCanonicalTextResolver.TryResolveOriginalTextMap(
                livePayload.TextNodes,
                scope,
                GetGameVersion(),
                out var originalTextNodes))
        {
            return false;
        }

        originalPayload = new DbFirstGameWindowPayload(
            [],
            [],
            originalTextNodes);
        return true;
    }
}
