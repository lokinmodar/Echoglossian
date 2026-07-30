// <copyright file="SelectionDialogHandlerContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;
using System.Reflection;

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
    ///     Ensures changing only the SelectIconString toggle invalidates addon
    ///     handler registration.
    /// </summary>
    [Fact]
    public void AddonHandlerRegistrationSignature_ChangesWhenSelectIconStringToggleChanges()
    {
        var disabled = new Config { TranslateSelectIconString = false };
        var enabled = new Config { TranslateSelectIconString = true };

        Assert.NotEqual(
            Echoglossian.ComputeAddonHandlerRegistrationSignature(disabled),
            Echoglossian.ComputeAddonHandlerRegistrationSignature(enabled));
    }

    /// <summary>
    ///     Ensures addon wiring registers SelectIconString under its dedicated
    ///     toggle rather than sharing SelectString settings.
    /// </summary>
    [Fact]
    public void AddonHandlerWiring_UsesDedicatedSelectIconStringToggleAndMode()
    {
        var root = FindRepositoryRoot();
        var wiringSource = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "AddonHandlerWiring.cs"));
        var handlerSource = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "SelectionDialogs",
            "SelectIconStringHandler.cs"));

        Assert.Contains(
            "this.configuration.TranslateSelectIconString",
            wiringSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "config.SelectIconStringTranslationDisplayMode",
            handlerSource,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the selection-dialog runtime now uses hover tooltips rather
    ///     than callback-owned overlay state.
    /// </summary>
    [Fact]
    public void SelectionDialogHandlerBase_UsesHoverTooltipManagerInsteadOfOverlayCallbacks()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "SelectionDialogs",
            "SelectionDialogHandlerBase.cs"));

        Assert.Contains("HoverTooltipManager", source, StringComparison.Ordinal);
        Assert.Contains("RemoveByPrefix", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SyncSelectionDialogOverlayBoundsDelegate",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Action<string, string, string> updateOverlay",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures generic selection dialogs are no longer registered as
    ///     overlay surfaces now that they use structured hover tooltips.
    /// </summary>
    [Fact]
    public void OverlayConfigs_DoesNotRegisterGenericSelectionDialogOverlays()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "UIOverlays",
            "TranslationOverlay",
            "OverlayConfigs.cs"));

        Assert.DoesNotContain("\"SelectYesno\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"SelectOk\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"SelectString\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"SelectIconString\"", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the frame-time overlay readiness gate no longer treats the
    ///     generic selection dialogs as ImGui overlay surfaces.
    /// </summary>
    [Fact]
    public void PluginRuntimeUi_DoesNotGateSelectionDialogsAsOverlayPresentation()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "PluginUI",
            "PluginRuntimeUi.cs"));

        Assert.DoesNotContain(
            "this.configuration.SelectYesNoTranslationDisplayMode",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "this.configuration.SelectOkTranslationDisplayMode",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "this.configuration.SelectStringTranslationDisplayMode",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "this.configuration.SelectIconStringTranslationDisplayMode",
            source,
            StringComparison.Ordinal);
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
