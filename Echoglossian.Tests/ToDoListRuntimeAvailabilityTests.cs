// <copyright file="ToDoListRuntimeAvailabilityTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Quest;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers runtime availability decisions for the ToDoList addon.
/// </summary>
public class ToDoListRuntimeAvailabilityTests
{
    /// <summary>
    /// Ensures mixed ready and pending quest rows remain renderable instead of
    /// forcing the entire list back to the original language.
    /// </summary>
    [Fact]
    public void FromCounts_ResolvedEntriesAndPendingQuests_RemainsRenderable()
    {
        var availability = ToDoListRuntimeAvailability.FromCounts(
            resolvedEntryCount: 3,
            blockingQuestCount: 1);

        Assert.True(availability.HasRenderableEntries);
        Assert.True(availability.HasPendingTranslations);
    }

    /// <summary>
    /// Ensures a fully unresolved list stays non-renderable until at least one
    /// runtime entry is available.
    /// </summary>
    [Fact]
    public void FromCounts_NoResolvedEntries_RemainsNonRenderable()
    {
        var availability = ToDoListRuntimeAvailability.FromCounts(
            resolvedEntryCount: 0,
            blockingQuestCount: 2);

        Assert.False(availability.HasRenderableEntries);
        Assert.True(availability.HasPendingTranslations);
    }
}
