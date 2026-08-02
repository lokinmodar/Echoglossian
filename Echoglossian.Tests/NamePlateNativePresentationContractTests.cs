// <copyright file="NamePlateNativePresentationContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers the repository-level contract for the native NamePlate presentation
/// path.
/// </summary>
public sealed class NamePlateNativePresentationContractTests
{
    /// <summary>
    /// Verifies that the NamePlate runtime uses the native title line for
    /// overlay and swap presentation semantics.
    /// </summary>
    [Fact]
    public void NamePlateRuntime_uses_native_title_field_for_overlay_and_swap()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "NamePlates",
            "NamePlateTranslationRuntime.cs"));

        Assert.Contains(
            "NamePlateNativePresentationPlan.Create(",
            source);
        Assert.Contains(
            "NamePlateStringField.Title",
            source);
        Assert.Contains(
            "handler.DisplayTitle",
            source);
        Assert.Contains(
            "handler.IsPrefixTitle",
            source);
    }

    /// <summary>
    /// Verifies that the plugin draw loop no longer renders NamePlate ImGui
    /// overlays explicitly.
    /// </summary>
    [Fact]
    public void PluginRuntimeUi_no_longer_draws_nameplate_imgui_overlays()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "PluginUI",
            "PluginRuntimeUi.cs"));

        Assert.DoesNotContain(
            "namePlateTranslationRuntime.DrawOverlays()",
            source);
    }

    /// <summary>
    /// Verifies that the NamePlate overlay settings tab no longer exposes ImGui
    /// style controls for the native NamePlate presentation path.
    /// </summary>
    [Fact]
    public void OverlayTab_hides_nameplate_imgui_style_controls_when_nameplate_modes_are_native()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "PluginUI",
            "Tabs",
            "OverlayTab.cs"));

        Assert.DoesNotContain(
            "ref config.NamePlateFontScale",
            source);
        Assert.Contains(
            "Resources.NamePlateNativeReplacementHelpText",
            source);
    }

    /// <summary>
    /// Verifies that runtime config refresh tracks NamePlate presentation
    /// state separately and explicitly requests a NamePlate redraw when that
    /// state changes.
    /// </summary>
    [Fact]
    public void RuntimeConfigurationRefresh_requests_nameplate_redraw_when_presentation_signature_changes()
    {
        var root = FindRepositoryRoot();
        var refreshSource = File.ReadAllText(Path.Combine(
            root.FullName,
            "GeneralHelpers",
            "RuntimeConfigurationRefresh.cs"));
        var pluginSource = File.ReadAllText(Path.Combine(
            root.FullName,
            "Echoglossian.cs"));

        Assert.Contains(
            "ComputeNamePlatePresentationSignature()",
            refreshSource);
        Assert.Contains(
            "NamePlateGuiInterface.RequestRedraw();",
            refreshSource);
        Assert.Contains(
            "namePlatePresentationSignature",
            pluginSource);
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
