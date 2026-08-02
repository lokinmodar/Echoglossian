// <copyright file="RuntimeConfigurationRefreshContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Verifies runtime configuration refresh sequencing for translation
///     signature changes.
/// </summary>
public sealed class RuntimeConfigurationRefreshContractTests
{
    /// <summary>
    ///     Ensures visible addon-owned presentation is restored before runtime
    ///     reset and translator rebuild happen.
    /// </summary>
    [Fact]
    public void Translation_refresh_restores_visible_addons_before_resetting_runtime_state()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "GeneralHelpers",
            "RuntimeConfigurationRefresh.cs"));

        var restoreCall = source.IndexOf(
            "this.RestoreVisibleAddonPresentationStateBeforeRuntimeReset();",
            StringComparison.Ordinal);
        var resetCall = source.IndexOf(
            "this.ResetRuntimeTranslationPresentationState();",
            StringComparison.Ordinal);
        var rebuildCall = source.IndexOf(
            "this.RebuildTranslationServiceSafely();",
            StringComparison.Ordinal);

        Assert.True(restoreCall >= 0);
        Assert.True(resetCall > restoreCall);
        Assert.True(rebuildCall > resetCall);
        Assert.Contains(
            "translationRefreshRestoreApplied",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Finds the repository root from the current test directory.
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
