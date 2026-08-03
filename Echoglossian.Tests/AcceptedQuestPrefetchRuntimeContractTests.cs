// <copyright file="AcceptedQuestPrefetchRuntimeContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Guards the accepted-quest prefetch runtime against running the full
/// canonical prefetch pipeline inline on the plugin tick.
/// </summary>
public sealed class AcceptedQuestPrefetchRuntimeContractTests
{
    /// <summary>
    /// Ensures the plugin tick schedules accepted-quest prefetch work instead
    /// of invoking the heavy prefetch routine inline.
    /// </summary>
    [Fact]
    public void TickAcceptedQuestPrefetch_QueuesBackgroundWorkInsteadOfCallingPrefetchInline()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "AcceptedQuestPrefetchRuntime.cs"));

        Assert.Contains(
            "this.ScheduleAcceptedQuestPrefetch(",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "this.PrefetchAcceptedQuest(",
            source,
            StringComparison.Ordinal);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
