// <copyright file="NamePlateDistanceAwareOverlayContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the repository-level contract for the distance-aware NamePlate
///     overlay fallback.
/// </summary>
public sealed class NamePlateDistanceAwareOverlayContractTests
{
    /// <summary>
    ///     Verifies that the overlay registry retains NamePlate presentation
    ///     state for the distance-aware fallback.
    /// </summary>
    [Fact]
    public void OverlayConfigs_registers_nameplate_overlay_state()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "UIOverlays",
            "TranslationOverlay",
            "OverlayConfigs.cs"));

        Assert.Contains(
            "private readonly TranslationOverlay namePlateOverlay = new();",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "this.namePlateOverlay",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that NamePlate runtime registration supplies projection and
    ///     overlay dependencies.
    /// </summary>
    [Fact]
    public void NamePlateRegistration_passes_game_gui_to_runtime()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "NamePlateTranslationRuntimeRegistration.cs"));

        Assert.Contains("GameGuiInterface", source, StringComparison.Ordinal);
        Assert.Contains("this.namePlateOverlay", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that the NamePlate runtime projects its game object and
    ///     resolves shared distance-aware presentation state.
    /// </summary>
    [Fact]
    public void NamePlateRuntime_uses_game_object_projection_and_distance_presentation()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "NamePlates",
            "NamePlateTranslationRuntime.cs"));

        Assert.Contains("handler.GameObject", source, StringComparison.Ordinal);
        Assert.Contains("WorldToScreen(", source, StringComparison.Ordinal);
        Assert.Contains("DistanceAwareOverlayPresentation.Resolve(", source, StringComparison.Ordinal);
        Assert.Contains("TrySyncDistanceAwareOverlayFrame(", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that retained NamePlate overlay candidates use the
    ///     game's object identifier for re-resolution instead of the unstable
    ///     event-object entity identifier path.
    /// </summary>
    [Fact]
    public void NamePlateRuntime_re_resolves_retained_candidates_by_game_object_id()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "NamePlates",
            "NamePlateTranslationRuntime.cs"));

        Assert.Contains("handler.GameObjectId", source, StringComparison.Ordinal);
        Assert.Contains("SearchById(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchByEntityId(", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that plugin shutdown releases the NamePlate overlay owned by
    ///     the distance-aware fallback.
    /// </summary>
    [Fact]
    public void Plugin_disposes_name_plate_overlay()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "Echoglossian.cs"));

        Assert.Contains("this.namePlateOverlay.Dispose();", source, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Unable to locate the repository root.");
    }
}
