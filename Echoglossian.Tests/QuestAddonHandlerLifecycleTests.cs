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
            QueueTranslationBatch = static (_, _, _) => false,
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
