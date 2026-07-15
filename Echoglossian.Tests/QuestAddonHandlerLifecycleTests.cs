// <copyright file="QuestAddonHandlerLifecycleTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Dalamud.Game.Addon.Lifecycle;

using Echoglossian.NativeUI.AddonHandlers.Quest;
using Echoglossian.NativeUI.Handlers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers lifecycle expectations for standalone quest addon handlers.
/// </summary>
public class QuestAddonHandlerLifecycleTests
{
    /// <summary>
    /// Ensures Journal participates in plugin-unload resets so visible quest
    /// text can be restored when the translation runtime changes.
    /// </summary>
    [Fact]
    public void JournalHandler_ImplementsPluginUnloadAwareContract()
    {
        Assert.True(
            typeof(IPluginUnloadAwareAddonHandler).IsAssignableFrom(
                typeof(JournalHandler)));
        Assert.Equal(
            typeof(JournalHandler),
            typeof(JournalHandler).GetMethod(
                nameof(IPluginUnloadAwareAddonHandler.OnPluginUnload),
                BindingFlags.Instance | BindingFlags.Public)?.DeclaringType);
    }

    /// <summary>
    /// Ensures JournalDetail participates in plugin-unload resets so visible
    /// detail text can be restored when the translation runtime changes.
    /// </summary>
    [Fact]
    public void JournalDetailHandler_ImplementsPluginUnloadAwareContract()
    {
        Assert.True(
            typeof(IPluginUnloadAwareAddonHandler).IsAssignableFrom(
                typeof(JournalDetailHandler)));
        Assert.Equal(
            typeof(JournalDetailHandler),
            typeof(JournalDetailHandler).GetMethod(
                nameof(IPluginUnloadAwareAddonHandler.OnPluginUnload),
                BindingFlags.Instance | BindingFlags.Public)?.DeclaringType);
    }

    /// <summary>
    /// Ensures Journal registers a pre-draw retry path so delayed quest
    /// translations can still surface without a new addon update event.
    /// </summary>
    [Fact]
    public void JournalHandler_RegistersPreDrawRefresh()
    {
        var handler = new JournalHandler(CreateDependencies());

        Assert.Contains(AddonEvent.PreDraw, handler.GetEventHandlers().Keys);
    }

    /// <summary>
    /// Ensures JournalDetail registers a pre-draw retry path so delayed quest
    /// translations can still surface without a new addon update event.
    /// </summary>
    [Fact]
    public void JournalDetailHandler_RegistersPreDrawRefresh()
    {
        var handler = new JournalDetailHandler(CreateDependencies());

        Assert.Contains(AddonEvent.PreDraw, handler.GetEventHandlers().Keys);
    }

    /// <summary>
    ///     Ensures tooltip-only JournalDetail rendering remains read-only unless
    ///     it must restore native text previously written by this handler.
    /// </summary>
    /// <param name="writesNativeTranslation">
    ///     Whether the active display mode writes native translations.
    /// </param>
    /// <param name="ownsNativeMutation">
    ///     Whether this handler previously wrote the visible native state.
    /// </param>
    /// <param name="expectedAction">The required native mutation action.</param>
    [Theory]
    [InlineData(
        true,
        false,
        nameof(JournalDetailNativeMutationAction.ApplyTranslation))]
    [InlineData(
        false,
        true,
        nameof(JournalDetailNativeMutationAction.RestoreOriginal))]
    [InlineData(false, false, nameof(JournalDetailNativeMutationAction.None))]
    public void ResolveNativeMutationAction_PreservesTooltipOnlyNativeState(
        bool writesNativeTranslation,
        bool ownsNativeMutation,
        string expectedAction)
    {
        Assert.Equal(
            Enum.Parse<JournalDetailNativeMutationAction>(expectedAction),
            JournalDetailHandler.ResolveNativeMutationAction(
                writesNativeTranslation,
                ownsNativeMutation));
    }

    /// <summary>
    /// Ensures the resolved display mode never selects native JournalDetail
    /// mutation work while tooltip-only rendering is active.
    /// </summary>
    /// <param name="displayMode">The configured JournalDetail display mode.</param>
    /// <param name="ownsNativeMutation">
    /// Whether this handler already owns the visible native state.
    /// </param>
    /// <param name="expectedAction">The expected native mutation action.</param>
    [Theory]
    [InlineData(
        JournalTranslationDisplayMode.TooltipTranslation,
        false,
        nameof(JournalDetailNativeMutationAction.None))]
    [InlineData(
        JournalTranslationDisplayMode.TooltipTranslation,
        true,
        nameof(JournalDetailNativeMutationAction.RestoreOriginal))]
    [InlineData(
        JournalTranslationDisplayMode.NativeUiTranslation,
        false,
        nameof(JournalDetailNativeMutationAction.ApplyTranslation))]
    [InlineData(
        JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
        false,
        nameof(JournalDetailNativeMutationAction.ApplyTranslation))]
    public void JournalDetailDisplayMode_MapsToExpectedNativeMutationAction(
        JournalTranslationDisplayMode displayMode,
        bool ownsNativeMutation,
        string expectedAction)
    {
        Assert.Equal(
            Enum.Parse<JournalDetailNativeMutationAction>(expectedAction),
            JournalDetailHandler.ResolveNativeMutationAction(
                QuestAddonModeHelpers.WritesNativeTranslation(displayMode),
                ownsNativeMutation));
    }

    /// <summary>
    /// Ensures the JournalDetail native layout cache key is stable for an
    /// unchanged scope and invalidates on scope or payload changes.
    /// </summary>
    [Fact]
    public void BuildJournalDetailNativeLayoutKey_IsStableAndInvalidatesOnChanges()
    {
        var stableKey = JournalDetailHandler.BuildJournalDetailNativeLayoutKey(
            "quest-a",
            ["title", "body", "objective"]);
        var sameKey = JournalDetailHandler.BuildJournalDetailNativeLayoutKey(
            "quest-a",
            ["title", "body", "objective"]);
        var differentScopeKey = JournalDetailHandler.BuildJournalDetailNativeLayoutKey(
            "quest-b",
            ["title", "body", "objective"]);
        var differentPayloadKey = JournalDetailHandler.BuildJournalDetailNativeLayoutKey(
            "quest-a",
            ["title", "body changed", "objective"]);

        Assert.Equal(stableKey, sameKey);
        Assert.NotEqual(stableKey, differentScopeKey);
        Assert.NotEqual(stableKey, differentPayloadKey);
    }

    /// <summary>
    /// Creates the minimal dependency bundle required to construct quest
    /// handlers for lifecycle tests.
    /// </summary>
    /// <returns>The quest-handler dependencies.</returns>
    private static QuestAddonHandlerDependencies CreateDependencies()
    {
        return new QuestAddonHandlerDependencies
        {
            Config = new Config(),
            TranslationService = null!,
            FindQuestPlate = _ => null,
            FindQuestPlateByName = _ => null,
            InsertQuestPlate = _ => string.Empty,
            UpdateQuestPlate = _ => string.Empty,
            UpdateQuestPlateGameVersion = (_, _) => { },
            NormalizeText = text => text,
            DisableTranslationAccordingToState = static () => false,
            TryGetQueuedTranslation = static (string _, out string translatedText) =>
            {
                translatedText = string.Empty;
                return false;
            },
            QueueTranslation = static (_, _, _) => false,
            QueueTranslationBatch = static (_, _, _, _) => false,
            RemoveHoverTooltipByPrefix = static _ => { },
            RegisterTranslatedHoverTooltipAddon = null!,
            RegisterTranslatedHoverTooltipTextNode = null!,
            RegisterTranslatedHoverTooltipResNode = null!,
            RegisterTranslatedHoverTooltipBounds = static (
                _,
                _,
                _,
                _,
                _,
                _,
                _,
                _) => { },
        };
    }
}
