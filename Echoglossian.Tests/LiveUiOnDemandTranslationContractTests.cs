// <copyright file="LiveUiOnDemandTranslationContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Verifies that live UI callbacks only apply cached translations and do not
/// start new translation work from transient game UI handlers.
/// </summary>
public class LiveUiOnDemandTranslationContractTests
{
    /// <summary>
    /// Ensures ActionDetail live hover processing does not trigger canonical
    /// action-tooltip prefetch. Background batch prefetch owns that work.
    /// </summary>
    [Fact]
    public void ActionDetailRuntime_does_not_request_on_demand_prefetch()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "ActionItemDetailUiRuntime.cs"));

        Assert.DoesNotContain(
            "TryRequestActionDetailOnDemandPrefetch(",
            source);
    }

    /// <summary>
    /// Ensures NamePlateGui callbacks do not enqueue translation work from
    /// one-frame handler state. Only cached translations may be applied live.
    /// </summary>
    [Fact]
    public void NamePlateRuntime_does_not_queue_live_callback_translations()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "NamePlates",
            "NamePlateTranslationRuntime.cs"));

        Assert.DoesNotContain(
            "QueueTranslationIfNeeded(",
            source);
        Assert.DoesNotContain(
            "ResolveTranslationAsync(",
            source);
    }

    /// <summary>
    /// Finds the repository root from the test output directory.
    /// </summary>
    /// <returns>The repository root directory.</returns>
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
