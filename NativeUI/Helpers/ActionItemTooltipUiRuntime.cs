// <copyright file="ActionItemTooltipUiRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Toasts;
using Echoglossian.NativeUI.Helpers;
using Echoglossian.UIOverlays.TranslationOverlay;

namespace Echoglossian;

/// <summary>
///     Provides the live DB-first apply/runtime path for action and item tooltips.
/// </summary>
public unsafe partial class Echoglossian
{
    private readonly TranslationOverlay actionTooltipOverlay = new();
    private readonly TranslationOverlay itemTooltipOverlay = new();

    private StructuredTooltipNativeState? currentActionTooltipState;
    private StructuredTooltipNativeState? currentItemTooltipState;

    /// <summary>
    ///     Updates live action/item tooltip state before tooltip overlays are drawn.
    /// </summary>
    private void UpdateStructuredTooltipUiRuntime()
    {
        this.UpdateActionTooltipUiRuntime();
        this.UpdateItemTooltipUiRuntime();
    }

    /// <summary>
    ///     Restores native tooltip state and clears action/item tooltip overlays.
    /// </summary>
    private void ResetStructuredTooltipUiRuntime()
    {
        if (this.TryResolveTooltipAddon(out var actionAddon, "ActionDetail", "_ActionDetail"))
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentActionTooltipState, actionAddon);
        }
        else
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentActionTooltipState, null);
        }

        if (this.TryResolveTooltipAddon(out var itemAddon, "ItemDetail", "_ItemDetail"))
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentItemTooltipState, itemAddon);
        }
        else
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentItemTooltipState, null);
        }

        this.ClearOverlay(this.actionTooltipOverlay, clearText: true);
        this.ClearOverlay(this.itemTooltipOverlay, clearText: true);
    }

    /// <summary>
    ///     Draws the active action/item tooltip overlays, if any.
    /// </summary>
    private void DrawStructuredTooltipOverlays()
    {
        this.DrawStructuredTooltipOverlay(
            this.actionTooltipOverlay,
            this.BuildTooltipOverlayConfig("Action tooltip translation"));
        this.DrawStructuredTooltipOverlay(
            this.itemTooltipOverlay,
            this.BuildTooltipOverlayConfig("Item tooltip translation"));
    }

    /// <summary>
    ///     Updates the live action-tooltip runtime.
    /// </summary>
    private void UpdateActionTooltipUiRuntime()
    {
        if (!this.ShouldRunStructuredTooltipUiRuntime())
        {
            this.ResetStructuredTooltipUiRuntime();
            return;
        }

        var hoveredAction = GameGuiInterface.HoveredAction;
        var hoveredActionId = hoveredAction?.ActionID ?? 0u;
        if (hoveredActionId == 0 ||
            !TryGetCurrentClassJobInfo(
                out var currentClassJobId,
                out _))
        {
            if (this.TryResolveTooltipAddon(out var restoreAddon, "ActionDetail", "_ActionDetail"))
            {
                this.RestoreStructuredTooltipOriginals(ref this.currentActionTooltipState, restoreAddon);
            }
            else
            {
                this.RestoreStructuredTooltipOriginals(ref this.currentActionTooltipState, null);
            }

            this.ClearOverlay(this.actionTooltipOverlay, clearText: true);
            return;
        }

        if (!this.TryResolveTooltipAddon(
                out var addon,
                "ActionDetail",
                "_ActionDetail"))
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentActionTooltipState, null);
            this.ClearOverlay(this.actionTooltipOverlay, clearText: true);
            return;
        }

        if (!TryBuildActionTooltipCanonicalPayload(
                hoveredActionId,
                currentClassJobId,
                out var originalPayload) ||
            !this.TryFindTranslatedActionTooltipPayload(
                originalPayload,
                out var translatedPayload))
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentActionTooltipState, addon);
            this.ClearOverlay(this.actionTooltipOverlay, clearText: true);
            return;
        }

        var displayMode = TranslationDisplayModeHelper.GetEffectiveDisplayMode(
            this.configuration.TooltipTranslationDisplayMode,
            this.configuration.OverlayOnlyLanguage);
        var useOverlayOnly =
            !TranslationDisplayModeHelper.WritesNativeTranslation(displayMode);
        var useSwapOverlay =
            TranslationDisplayModeHelper.ShowsOriginalTooltips(displayMode);

        if (useOverlayOnly)
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentActionTooltipState, addon);
        }
        else
        {
            this.ApplyStructuredActionTooltipNative(
                addon,
                originalPayload,
                translatedPayload);
        }

        if (useOverlayOnly || useSwapOverlay)
        {
            var overlayText = useOverlayOnly
                ? translatedPayload.BuildTranslatedTooltipText()
                : originalPayload.BuildOriginalTooltipText();
            this.UpdateStructuredTooltipOverlay(
                this.actionTooltipOverlay,
                addon,
                overlayText);
        }
        else
        {
            this.ClearOverlay(this.actionTooltipOverlay, clearText: true);
        }
    }

    /// <summary>
    ///     Updates the live item-tooltip runtime.
    /// </summary>
    private void UpdateItemTooltipUiRuntime()
    {
        if (!this.ShouldRunStructuredTooltipUiRuntime())
        {
            this.ResetStructuredTooltipUiRuntime();
            return;
        }

        var hoveredItemId = NormalizeHoveredItemId((uint)GameGuiInterface.HoveredItem);
        if (hoveredItemId == 0)
        {
            if (this.TryResolveTooltipAddon(out var restoreAddon, "ItemDetail", "_ItemDetail"))
            {
                this.RestoreStructuredTooltipOriginals(ref this.currentItemTooltipState, restoreAddon);
            }
            else
            {
                this.RestoreStructuredTooltipOriginals(ref this.currentItemTooltipState, null);
            }

            this.ClearOverlay(this.itemTooltipOverlay, clearText: true);
            return;
        }

        if (!this.TryResolveTooltipAddon(
                out var addon,
                "ItemDetail",
                "_ItemDetail"))
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentItemTooltipState, null);
            this.ClearOverlay(this.itemTooltipOverlay, clearText: true);
            return;
        }

        if (!TryBuildItemTooltipCanonicalPayload(
                hoveredItemId,
                out var originalPayload) ||
            !this.TryFindTranslatedItemTooltipPayload(
                originalPayload,
                out var translatedPayload))
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentItemTooltipState, addon);
            this.ClearOverlay(this.itemTooltipOverlay, clearText: true);
            return;
        }

        var displayMode = TranslationDisplayModeHelper.GetEffectiveDisplayMode(
            this.configuration.TooltipTranslationDisplayMode,
            this.configuration.OverlayOnlyLanguage);
        var useOverlayOnly =
            !TranslationDisplayModeHelper.WritesNativeTranslation(displayMode);
        var useSwapOverlay =
            TranslationDisplayModeHelper.ShowsOriginalTooltips(displayMode);

        if (useOverlayOnly)
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentItemTooltipState, addon);
        }
        else
        {
            this.ApplyStructuredItemTooltipNative(
                addon,
                originalPayload,
                translatedPayload);
        }

        if (useOverlayOnly || useSwapOverlay)
        {
            var overlayText = useOverlayOnly
                ? translatedPayload.BuildTranslatedTooltipText()
                : originalPayload.BuildOriginalTooltipText();
            this.UpdateStructuredTooltipOverlay(
                this.itemTooltipOverlay,
                addon,
                overlayText);
        }
        else
        {
            this.ClearOverlay(this.itemTooltipOverlay, clearText: true);
        }
    }

    /// <summary>
    ///     Gets whether the DB-first tooltip runtime should execute.
    /// </summary>
    /// <returns><see langword="true" /> when the runtime should execute.</returns>
    private bool ShouldRunStructuredTooltipUiRuntime()
    {
        return this.configuration.Translate &&
               this.configuration.TranslateTooltips &&
               !GameGuiInterface.GameUiHidden &&
               ClientStateInterface.IsLoggedIn;
    }

    /// <summary>
    ///     Tries to resolve one visible tooltip addon by name.
    /// </summary>
    /// <param name="addon">The resolved addon, if any.</param>
    /// <param name="addonNames">The candidate addon names.</param>
    /// <returns><see langword="true" /> when a visible addon was resolved.</returns>
    private bool TryResolveTooltipAddon(
        out AtkUnitBase* addon,
        params string[] addonNames)
    {
        addon = null;

        foreach (var addonName in addonNames)
        {
            var addonPtr = GameGuiInterface.GetAddonByName(addonName, 1);
            if (addonPtr.Address == IntPtr.Zero)
            {
                continue;
            }

            var resolvedAddon = (AtkUnitBase*)addonPtr.Address;
            if (resolvedAddon == null ||
                !resolvedAddon->IsVisible ||
                resolvedAddon->RootNode == null)
            {
                continue;
            }

            addon = resolvedAddon;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Tries to resolve one translated action-tooltip payload from canonical storage.
    /// </summary>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translatedPayload">The translated payload, if any.</param>
    /// <returns><see langword="true" /> when a complete translation is available.</returns>
    private bool TryFindTranslatedActionTooltipPayload(
        ActionTooltipCanonicalPayload originalPayload,
        out ActionTooltipCanonicalPayload translatedPayload)
    {
        translatedPayload = new ActionTooltipCanonicalPayload();

        var probe = ActionTooltipPersistenceHelper.CreateCanonicalRow(
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code,
            this.configuration.ChosenTransEngine,
            GetGameVersion(),
            originalPayload);
        var row = this.FindActionTooltip(probe);
        var resolvedPayload = ActionTooltipCanonicalPayload.Deserialize(
            row?.CanonicalPayloadAsText);
        if (resolvedPayload?.HasCompleteTranslation != true)
        {
            return false;
        }

        translatedPayload = resolvedPayload;
        return true;
    }

    /// <summary>
    ///     Tries to resolve one translated item-tooltip payload from canonical storage.
    /// </summary>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translatedPayload">The translated payload, if any.</param>
    /// <returns><see langword="true" /> when a complete translation is available.</returns>
    private bool TryFindTranslatedItemTooltipPayload(
        ItemTooltipCanonicalPayload originalPayload,
        out ItemTooltipCanonicalPayload translatedPayload)
    {
        translatedPayload = new ItemTooltipCanonicalPayload();

        var probe = ItemTooltipPersistenceHelper.CreateCanonicalRow(
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code,
            this.configuration.ChosenTransEngine,
            GetGameVersion(),
            originalPayload);
        var row = this.FindItemTooltip(probe);
        var resolvedPayload = ItemTooltipCanonicalPayload.Deserialize(
            row?.CanonicalPayloadAsText);
        if (resolvedPayload?.HasCompleteTranslation != true)
        {
            return false;
        }

        translatedPayload = resolvedPayload;
        return true;
    }

    /// <summary>
    ///     Applies translated native text for the action tooltip when safe to do so.
    /// </summary>
    /// <param name="addon">The visible tooltip addon.</param>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translatedPayload">The translated canonical payload.</param>
    private void ApplyStructuredActionTooltipNative(
        AtkUnitBase* addon,
        ActionTooltipCanonicalPayload originalPayload,
        ActionTooltipCanonicalPayload translatedPayload)
    {
        this.ApplyStructuredTooltipNative(
            addon,
            originalPayload.ActionId,
            originalPayload.Name,
            originalPayload.Description,
            translatedPayload.TranslatedName ?? originalPayload.Name,
            translatedPayload.TranslatedDescription ?? originalPayload.Description,
            ref this.currentActionTooltipState);
    }

    /// <summary>
    ///     Applies translated native text for the item tooltip when safe to do so.
    /// </summary>
    /// <param name="addon">The visible tooltip addon.</param>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translatedPayload">The translated canonical payload.</param>
    private void ApplyStructuredItemTooltipNative(
        AtkUnitBase* addon,
        ItemTooltipCanonicalPayload originalPayload,
        ItemTooltipCanonicalPayload translatedPayload)
    {
        this.ApplyStructuredTooltipNative(
            addon,
            originalPayload.ItemId,
            originalPayload.Name,
            originalPayload.Description,
            translatedPayload.TranslatedName ?? originalPayload.Name,
            translatedPayload.TranslatedDescription ?? originalPayload.Description,
            ref this.currentItemTooltipState);
    }

    /// <summary>
    ///     Applies translated native text to the name and description nodes of a tooltip.
    /// </summary>
    /// <param name="addon">The visible tooltip addon.</param>
    /// <param name="contentId">The logical content identifier.</param>
    /// <param name="originalName">The original name text.</param>
    /// <param name="originalDescription">The original description text.</param>
    /// <param name="translatedName">The translated name text.</param>
    /// <param name="translatedDescription">The translated description text.</param>
    /// <param name="runtimeState">The active native-runtime state.</param>
    private void ApplyStructuredTooltipNative(
        AtkUnitBase* addon,
        uint contentId,
        string originalName,
        string originalDescription,
        string translatedName,
        string translatedDescription,
        ref StructuredTooltipNativeState? runtimeState)
    {
        if (addon == null || contentId == 0)
        {
            this.RestoreStructuredTooltipOriginals(ref runtimeState, addon);
            return;
        }

        if (runtimeState != null &&
            ((nint)addon != runtimeState.AddonAddress ||
             runtimeState.ContentId != contentId))
        {
            this.RestoreStructuredTooltipOriginals(ref runtimeState, addon);
        }

        StructuredTooltipNativeState? resolvedState = null;
        if (runtimeState == null)
        {
            if (!this.TryResolveTooltipNodeAddresses(
                    addon,
                    contentId,
                    originalName,
                    originalDescription,
                    out var resolvedRuntimeState))
            {
                return;
            }

            resolvedState = resolvedRuntimeState;
        }

        runtimeState ??= resolvedState;

        if (runtimeState.NameNodeAddress != 0)
        {
            var nameNode = (AtkTextNode*)runtimeState.NameNodeAddress;
            if (nameNode != null &&
                this.ReadTooltipTextNode(nameNode) != translatedName)
            {
                nameNode->SetText(translatedName);
            }
        }

        if (runtimeState.DescriptionNodeAddress != 0 &&
            !string.IsNullOrWhiteSpace(originalDescription))
        {
            var descriptionNode = (AtkTextNode*)runtimeState.DescriptionNodeAddress;
            if (descriptionNode != null &&
                this.ReadTooltipTextNode(descriptionNode) != translatedDescription)
            {
                descriptionNode->SetText(translatedDescription);
            }
        }
    }

    /// <summary>
    ///     Restores original tooltip text when the runtime previously mutated native nodes.
    /// </summary>
    /// <param name="runtimeState">The runtime state to restore.</param>
    /// <param name="addon">The current visible tooltip addon, if any.</param>
    private void RestoreStructuredTooltipOriginals(
        ref StructuredTooltipNativeState? runtimeState,
        AtkUnitBase* addon)
    {
        if (runtimeState == null)
        {
            return;
        }

        if (addon == null || (nint)addon != runtimeState.AddonAddress)
        {
            runtimeState = null;
            return;
        }

        if (runtimeState.NameNodeAddress != 0)
        {
            var nameNode = (AtkTextNode*)runtimeState.NameNodeAddress;
            if (nameNode != null &&
                this.ReadTooltipTextNode(nameNode) != runtimeState.OriginalNameText)
            {
                nameNode->SetText(runtimeState.OriginalNameText);
            }
        }

        if (runtimeState.DescriptionNodeAddress != 0)
        {
            var descriptionNode = (AtkTextNode*)runtimeState.DescriptionNodeAddress;
            if (descriptionNode != null &&
                this.ReadTooltipTextNode(descriptionNode) != runtimeState.OriginalDescriptionText)
            {
                descriptionNode->SetText(runtimeState.OriginalDescriptionText);
            }
        }

        runtimeState = null;
    }

    /// <summary>
    ///     Tries to resolve tooltip text-node addresses that match the canonical name and description.
    /// </summary>
    /// <param name="addon">The visible tooltip addon.</param>
    /// <param name="contentId">The logical content identifier.</param>
    /// <param name="originalName">The canonical original name.</param>
    /// <param name="originalDescription">The canonical original description.</param>
    /// <param name="runtimeState">The resolved node-address map.</param>
    /// <returns><see langword="true" /> when at least one relevant node was resolved.</returns>
    private bool TryResolveTooltipNodeAddresses(
        AtkUnitBase* addon,
        uint contentId,
        string originalName,
        string originalDescription,
        out StructuredTooltipNativeState runtimeState)
    {
        runtimeState = new StructuredTooltipNativeState(
            (nint)addon,
            contentId,
            0,
            originalName,
            0,
            originalDescription);

        if (addon == null || string.IsNullOrWhiteSpace(originalName))
        {
            return false;
        }

        var textNodeAddresses = AddonTextNodeResolvers.ResolveMiniTalkBubbleTextNodes(addon);
        foreach (var nodeAddress in textNodeAddresses)
        {
            var textNode = (AtkTextNode*)nodeAddress;
            var visibleText = this.ReadTooltipTextNode(textNode);
            if (runtimeState.NameNodeAddress == 0 &&
                string.Equals(visibleText, originalName, StringComparison.Ordinal))
            {
                runtimeState = runtimeState with
                {
                    NameNodeAddress = nodeAddress,
                    OriginalNameText = visibleText,
                };
                continue;
            }

            if (runtimeState.DescriptionNodeAddress == 0 &&
                !string.IsNullOrWhiteSpace(originalDescription) &&
                string.Equals(
                    visibleText,
                    originalDescription,
                    StringComparison.Ordinal))
            {
                runtimeState = runtimeState with
                {
                    DescriptionNodeAddress = nodeAddress,
                    OriginalDescriptionText = visibleText,
                };
            }
        }

        return runtimeState.NameNodeAddress != 0 ||
               runtimeState.DescriptionNodeAddress != 0;
    }

    /// <summary>
    ///     Reads one tooltip text node using the same fallback order used elsewhere in the repo.
    /// </summary>
    /// <param name="textNode">The text node to read.</param>
    /// <returns>The resolved text, or <see cref="string.Empty" />.</returns>
    private string ReadTooltipTextNode(AtkTextNode* textNode)
    {
        if (textNode == null)
        {
            return string.Empty;
        }

        try
        {
            var directText = textNode->NodeText.ToString();
            if (!string.IsNullOrWhiteSpace(directText))
            {
                return directText;
            }
        }
        catch
        {
            // Fall through to the legacy buffer read.
        }

        try
        {
            return MemoryHelper.ReadSeStringAsString(
                       out _,
                       (nint)textNode->NodeText.StringPtr.Value) ??
                   string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    ///     Updates one tooltip overlay from the currently visible tooltip addon.
    /// </summary>
    /// <param name="overlay">The overlay instance to update.</param>
    /// <param name="addon">The visible tooltip addon.</param>
    /// <param name="text">The tooltip text to render.</param>
    private void UpdateStructuredTooltipOverlay(
        TranslationOverlay overlay,
        AtkUnitBase* addon,
        string? text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            addon == null)
        {
            this.ClearOverlay(overlay, clearText: true);
            return;
        }

        this.UpdateOverlayContent(
            overlay,
            string.Empty,
            text,
            string.Empty);
        this.UpdateOverlayBounds(overlay, addon);
    }

    /// <summary>
    ///     Draws one structured tooltip overlay when it has published content.
    /// </summary>
    /// <param name="overlay">The overlay instance to draw.</param>
    /// <param name="config">The overlay configuration.</param>
    private void DrawStructuredTooltipOverlay(
        TranslationOverlay overlay,
        TranslationWindowConfig config)
    {
        overlay.Semaphore.Wait();
        var shouldDraw = overlay.Display;
        overlay.Semaphore.Release();

        if (!shouldDraw)
        {
            return;
        }

        this.DrawTranslationWindow(overlay, config);
    }

    /// <summary>
    ///     Builds the shared overlay configuration used by structured tooltips.
    /// </summary>
    /// <param name="defaultTitle">The default overlay title.</param>
    /// <returns>The overlay configuration.</returns>
    private TranslationWindowConfig BuildTooltipOverlayConfig(string defaultTitle)
    {
        return new TranslationWindowConfig(
            DefaultTitle: defaultTitle,
            FontScale: 1.0f,
            WidthMultiplier: 1.0f,
            HeightMultiplier: 1.0f,
            TextColor: new Vector4(1f, 1f, 1f, 1f),
            PosCorrection: Vector2.Zero,
            ForceShowTitle: false,
            BackgroundOpacity: 0.95f,
            NoBackground: false,
            UseFixedWindowSize: false,
            CenterOnAddon: false,
            AutoSizeToTextWithMaxWidth: true,
            ExpandWidthToFitText: true,
            MaxAutoExpandedWidthMultiplier: 1.35f,
            MinWidthViewportFraction: 0.18f,
            MaxWidthViewportFraction: 0.36f);
    }

    /// <summary>
    ///     Normalizes hovered item ids so HQ items resolve against the base Item row.
    /// </summary>
    /// <param name="hoveredItemId">The raw hovered item identifier.</param>
    /// <returns>The normalized base item row identifier.</returns>
    private static uint NormalizeHoveredItemId(uint hoveredItemId)
    {
        return hoveredItemId > 1_000_000u
            ? hoveredItemId - 1_000_000u
            : hoveredItemId;
    }

    /// <summary>
    ///     Captures the minimal mutable state for one structured tooltip instance.
    /// </summary>
    /// <param name="AddonAddress">The visible tooltip addon address.</param>
    /// <param name="ContentId">The logical item/action identifier.</param>
    /// <param name="NameNodeAddress">The resolved name-node address.</param>
    /// <param name="OriginalNameText">The original name-node text.</param>
    /// <param name="DescriptionNodeAddress">The resolved description-node address.</param>
    /// <param name="OriginalDescriptionText">The original description-node text.</param>
    private sealed record StructuredTooltipNativeState(
        nint AddonAddress,
        uint ContentId,
        nint NameNodeAddress,
        string OriginalNameText,
        nint DescriptionNodeAddress,
        string OriginalDescriptionText);
}
