// <copyright file="SelectionDialogHandlerContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the runtime contract for the generic selection-dialog handlers.
/// </summary>
public sealed class SelectionDialogHandlerContractTests
{
    /// <summary>
    ///     Ensures addon wiring declares the three generic selection-dialog
    ///     handlers with the expected addon names.
    /// </summary>
    /// <param name="addonName">The native addon name.</param>
    /// <param name="handlerTypeName">The runtime handler type name.</param>
    [Theory]
    [InlineData("SelectYesno", "SelectYesNoHandler")]
    [InlineData("SelectOk", "SelectOkHandler")]
    [InlineData("SelectString", "SelectStringHandler")]
    [InlineData("SelectIconString", "SelectIconStringHandler")]
    public void AddonHandlerWiring_RegistersSelectionDialogHandlers(
        string addonName,
        string handlerTypeName)
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "AddonHandlerWiring.cs"));

        Assert.Contains(addonName, source, StringComparison.Ordinal);
        Assert.Contains(handlerTypeName, source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the handler source files exist so runtime wiring can target
    ///     concrete implementations rather than placeholder names.
    /// </summary>
    /// <param name="relativePath">The handler file relative path.</param>
    [Theory]
    [InlineData("NativeUI\\AddonHandlers\\SelectionDialogs\\SelectYesNoHandler.cs")]
    [InlineData("NativeUI\\AddonHandlers\\SelectionDialogs\\SelectOkHandler.cs")]
    [InlineData("NativeUI\\AddonHandlers\\SelectionDialogs\\SelectStringHandler.cs")]
    [InlineData("NativeUI\\AddonHandlers\\SelectionDialogs\\SelectIconStringHandler.cs")]
    public void HandlerSourceFiles_ExistForSelectionDialogs(string relativePath)
    {
        var root = FindRepositoryRoot();
        Assert.True(
            File.Exists(Path.Combine(root.FullName, relativePath)),
            $"Expected selection-dialog handler source file '{relativePath}' to exist.");
    }

    /// <summary>
    ///     Ensures the icon-bearing selection dialog has dedicated config and
    ///     no longer reuses the plain SelectString toggle and display mode.
    /// </summary>
    [Fact]
    public void SelectIconStringHandler_UsesDedicatedToggleAndDisplayMode()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "SelectionDialogs",
            "SelectIconStringHandler.cs"));

        Assert.Contains(
            "config.TranslateSelectIconString",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "config.SelectIconStringTranslationDisplayMode",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "config.TranslateSelectString",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the shared selection-dialog runtime switches to the shared
    ///     hover-tooltip pipeline instead of the dedicated overlay callbacks.
    /// </summary>
    [Fact]
    public void SelectionDialogHandlerBase_UsesHoverTooltipRuntimeInsteadOfOverlayCallbacks()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "SelectionDialogs",
            "SelectionDialogHandlerBase.cs"));

        Assert.Contains(
            "HoverTooltipManager",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RemoveByPrefix",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Action<string, string, string> updateOverlay",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "private void PublishOverlay()",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the overlay sync path uses the real native addon name for
    ///     the yes/no dialog.
    /// </summary>
    [Fact]
    public void OverlayConfigs_UsesNativeSelectYesnoAddonName()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "UIOverlays",
            "TranslationOverlay",
            "OverlayConfigs.cs"));

        Assert.Contains("\"SelectYesno\"", source, StringComparison.Ordinal);
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
