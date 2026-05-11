// <copyright file="MainCommandHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace Echoglossian.NativeUI.AddonHandlers.MainMenu;

/// <summary>
///     Handles DB-first translation for the main command menu.
/// </summary>
public unsafe class MainCommandHandler : DbFirstGameWindowAddonHandler
{
    private const int FirstMainCommandButtonNodeId = 2;
    private const int LastMainCommandButtonNodeId = 8;
    private const int FirstMainCommandPayloadIndex = 3;
    private const int LastMainCommandPayloadIndex = 9;

    private readonly Config config;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="MainCommandHandler" /> class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public MainCommandHandler(
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService)
        : base(
            addonName: "_MainCommand",
            config: config,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService,
            enabledSelector: static configuration =>
                configuration.TranslateGameMainMenu,
            useAtkValues: true,
            useTextNodes: false,
            displayModeSelector: static configuration =>
                configuration.GameMainMenuWindowTranslationDisplayMode)
    {
        this.config = config;
    }

    /// <inheritdoc />
    protected override bool ShouldReuseCompatiblePayloads()
    {
        return false;
    }

    /// <inheritdoc />
    protected override void OnLifecycleEvent(AddonEvent evt, AddonArgs args)
    {
        if (evt == AddonEvent.PreRefresh)
        {
            this.TryApplyHoverRefreshTranslation(args);
        }

        base.OnLifecycleEvent(evt, args);
    }

    /// <inheritdoc />
    private protected override bool TryRegisterCustomHoverTooltips(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload,
        JournalTranslationDisplayMode displayMode)
    {
        var buttonNodes = CollectMainCommandButtonNodes(addon);
        if (buttonNodes.Count == 0)
        {
            return false;
        }

        var payloadCount = Math.Min(
            buttonNodes.Count,
            LastMainCommandButtonNodeId - FirstMainCommandButtonNodeId + 1);
        var registeredAny = false;

        for (var offset = 0; offset < payloadCount; offset++)
        {
            var payloadIndex = FirstMainCommandPayloadIndex + offset;
            if (!originalPayload.AtkValues.TryGetValue(
                    payloadIndex,
                    out var originalText) ||
                !translatedPayload.AtkValues.TryGetValue(
                    payloadIndex,
                    out var translatedText))
            {
                continue;
            }

            var node = (AtkResNode*)buttonNodes[offset];
            var left = node->ScreenX - 8f;
            var top = node->ScreenY - 4f;
            var right = node->ScreenX + Math.Max(1f, node->Width) + 8f;
            var bottom = node->ScreenY + Math.Max(1f, node->Height) + 4f;

            this.RegisterTranslatedHoverTooltip(
                $"button-{payloadIndex}",
                new Vector2(left, top),
                new Vector2(right, bottom),
                originalText,
                translatedText,
                displayMode);
            registeredAny = true;
        }

        return registeredAny;
    }

    /// <summary>
    ///     Collects the visible main-command button components in left-to-right
    ///     order.
    /// </summary>
    /// <param name="addon">The live main-command addon.</param>
    /// <returns>The visible command button nodes.</returns>
    private static List<nint> CollectMainCommandButtonNodes(
        AtkUnitBase* addon)
    {
        var nodes = new List<nint>();
        if (addon == null ||
            addon->UldManager.NodeList == null ||
            addon->UldManager.NodeListCount <= 0)
        {
            return nodes;
        }

        for (var index = 0; index < addon->UldManager.NodeListCount; index++)
        {
            var node = addon->UldManager.NodeList[index];
            if (node == null ||
                !node->IsVisible() ||
                node->NodeId < FirstMainCommandButtonNodeId ||
                node->NodeId > LastMainCommandButtonNodeId ||
                (ushort)node->Type < 1000)
            {
                continue;
            }

            nodes.Add((nint)node);
        }

        nodes.Sort(
            static (left, right) =>
                ((AtkResNode*)left)->ScreenX.CompareTo(((AtkResNode*)right)->ScreenX));
        return nodes;
    }

    /// <summary>
    ///     Applies translated hover labels directly to the refresh payload when
    ///     the main command menu reprocesses its <c>AtkValue</c>s.
    /// </summary>
    /// <param name="args">The lifecycle arguments for the refresh event.</param>
    private void TryApplyHoverRefreshTranslation(AddonArgs args)
    {
        if (args.AddonName != "_MainCommand" ||
            args is not AddonRefreshArgs refreshArgs ||
            args.Addon.Address == IntPtr.Zero ||
            refreshArgs.AtkValues == IntPtr.Zero ||
            refreshArgs.AtkValueCount <= FirstMainCommandPayloadIndex)
        {
            return;
        }

        var atkValues = (AtkValue*)refreshArgs.AtkValues;
        if (atkValues == null)
        {
            return;
        }

        var originalPayload = CaptureRefreshPayload(
            atkValues,
            refreshArgs.AtkValueCount);
        if (originalPayload.IsEmpty ||
            !this.TryResolveExactPersistedGameWindowPayload(
                originalPayload,
                out var translatedPayload))
        {
            return;
        }

        var displayMode = TranslationDisplayModeHelper.GetEffectiveDisplayMode(
            this.config.GameMainMenuWindowTranslationDisplayMode,
            this.config.OverlayOnlyLanguage);

        if (TranslationDisplayModeHelper.WritesNativeTranslation(displayMode))
        {
            ApplyTranslatedRefreshPayload(
                atkValues,
                refreshArgs.AtkValueCount,
                translatedPayload);
        }

        if (!TranslationDisplayModeHelper.UsesHoverTooltips(displayMode))
        {
            return;
        }

        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null || !addon->IsVisible)
        {
            return;
        }

        this.TryRegisterCustomHoverTooltips(
            addon,
            originalPayload,
            translatedPayload,
            displayMode);
    }

    /// <summary>
    ///     Captures the hover-label portion of the main command refresh payload.
    /// </summary>
    /// <param name="atkValues">The refresh ATK value buffer.</param>
    /// <param name="atkValueCount">The number of values in the buffer.</param>
    /// <returns>The captured DB-first payload.</returns>
    private static DbFirstGameWindowPayload CaptureRefreshPayload(
        AtkValue* atkValues,
        uint atkValueCount)
    {
        var capturedAtkValues = new SortedDictionary<int, string>();

        for (var index = FirstMainCommandPayloadIndex;
             index <= LastMainCommandPayloadIndex && index < atkValueCount;
             index++)
        {
            ref var value = ref atkValues[index];
            if (value.Type is not
                (ValueType.String or
                 ValueType.String8 or
                 ValueType.ManagedString))
            {
                continue;
            }

            var text = MemoryHelper.ReadSeStringAsString(
                out _,
                (nint)value.String.Value);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            capturedAtkValues[(int)index] = text;
        }

        return new DbFirstGameWindowPayload(
            capturedAtkValues,
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Writes translated hover labels into the refresh ATK value buffer.
    /// </summary>
    /// <param name="atkValues">The refresh ATK value buffer.</param>
    /// <param name="atkValueCount">The number of values in the buffer.</param>
    /// <param name="translatedPayload">The translated payload to apply.</param>
    private static void ApplyTranslatedRefreshPayload(
        AtkValue* atkValues,
        uint atkValueCount,
        DbFirstGameWindowPayload translatedPayload)
    {
        foreach (var (index, translatedText) in translatedPayload.AtkValues)
        {
            if (index < FirstMainCommandPayloadIndex ||
                index > LastMainCommandPayloadIndex ||
                index >= atkValueCount)
            {
                continue;
            }

            ref var value = ref atkValues[index];
            if (value.Type is not
                (ValueType.String or
                 ValueType.String8 or
                 ValueType.ManagedString))
            {
                continue;
            }

            var currentText = MemoryHelper.ReadSeStringAsString(
                out _,
                (nint)value.String.Value);
            if (string.Equals(
                    currentText,
                    translatedText,
                    StringComparison.Ordinal))
            {
                continue;
            }

            value.SetManagedString(translatedText);
        }
    }
}
