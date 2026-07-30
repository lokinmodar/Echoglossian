// <copyright file="TooltipAddonHandlerContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the dedicated Tooltip addon runtime wiring contract.
/// </summary>
public sealed class TooltipAddonHandlerContractTests
{
    /// <summary>
    ///     Ensures changing only the Tooltip addon toggle invalidates addon
    ///     handler registration.
    /// </summary>
    [Fact]
    public void AddonHandlerRegistrationSignature_ChangesWhenTooltipAddonToggleChanges()
    {
        var disabled = new Config { TranslateTooltipAddon = false };
        var enabled = new Config { TranslateTooltipAddon = true };

        Assert.NotEqual(
            Echoglossian.ComputeAddonHandlerRegistrationSignature(disabled),
            Echoglossian.ComputeAddonHandlerRegistrationSignature(enabled));
    }

    /// <summary>
    ///     Ensures addon wiring registers the dedicated Tooltip handler.
    /// </summary>
    [Fact]
    public void AddonHandlerWiring_RegistersTooltipHandler()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "AddonHandlerWiring.cs"));

        Assert.Contains("(AddonName: \"Tooltip\"", source, StringComparison.Ordinal);
        Assert.Contains("new TooltipHandler(", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the dedicated Tooltip handler uses its own config and
    ///     text-node DB-first runtime.
    /// </summary>
    [Fact]
    public void TooltipHandler_UsesDedicatedConfigAndTextNodeRuntime()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Common",
            "TooltipHandler.cs"));

        Assert.Contains("addonName: \"Tooltip\"", source, StringComparison.Ordinal);
        Assert.Contains("configuration.TranslateTooltipAddon", source, StringComparison.Ordinal);
        Assert.Contains("configuration.TooltipAddonTranslationDisplayMode", source, StringComparison.Ordinal);
        Assert.Contains("useAtkValues: false", source, StringComparison.Ordinal);
        Assert.Contains("useTextNodes: true", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Finds the repository root from the test output directory.
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
