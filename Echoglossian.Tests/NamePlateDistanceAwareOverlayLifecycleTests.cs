// <copyright file="NamePlateDistanceAwareOverlayLifecycleTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Numerics;

using Echoglossian.NativeUI.AddonHandlers.NamePlates;
using Echoglossian.PluginUI.Tabs;
using Echoglossian.UIOverlays.TranslationOverlay;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the retained-candidate lifecycle used by the single shared
///     distance-aware NamePlate overlay.
/// </summary>
public sealed class NamePlateDistanceAwareOverlayLifecycleTests
{
    /// <summary>
    ///     Verifies that a retained callback candidate is projected again on
    ///     every UI frame even when no new callback occurs.
    /// </summary>
    [Fact]
    public void TrySync_recomputes_live_frame_without_another_callback()
    {
        using var overlay = new TranslationOverlay();
        var lifecycle = new NamePlateDistanceAwareOverlayLifecycle();
        lifecycle.BeginNamePlateUpdate(isFullUpdate: false, activeNamePlateCount: 1);
        lifecycle.UpsertCandidate(new NamePlateDistanceAwareOverlayCandidate(
            100u,
            "Original",
            "Translated"));

        var liveFrame = new NamePlateDistanceAwareOverlayFrame(
            new Vector2(100f, 50f),
            6f,
            1f,
            1f);

        Assert.True(lifecycle.TrySync(
            overlay,
            new Vector2(1000f, 600f),
            _ => liveFrame));
        Assert.Equal(new Vector2(10f, 38f), overlay.Position);
        Assert.Equal(1f, overlay.RenderScale);
        Assert.Equal(1f, overlay.RenderAlpha);

        liveFrame = new NamePlateDistanceAwareOverlayFrame(
            new Vector2(240f, 140f),
            22f,
            0.72f,
            0.5f);

        Assert.True(lifecycle.TrySync(
            overlay,
            new Vector2(1000f, 600f),
            _ => liveFrame));
        Assert.Equal(new Vector2(150f, 128f), overlay.Position);
        Assert.Equal(0.72f, overlay.RenderScale);
        Assert.Equal(0.5f, overlay.RenderAlpha);
    }

    /// <summary>
    ///     Verifies that callback insertion order cannot choose a farther
    ///     candidate for the single shared overlay.
    /// </summary>
    [Fact]
    public void TrySync_selects_nearest_candidate_independent_of_callback_order()
    {
        using var overlay = new TranslationOverlay();
        var lifecycle = new NamePlateDistanceAwareOverlayLifecycle();
        lifecycle.BeginNamePlateUpdate(isFullUpdate: true, activeNamePlateCount: 2);
        lifecycle.UpsertCandidate(new NamePlateDistanceAwareOverlayCandidate(
            20u,
            "Near Original",
            "Near Translation"));
        lifecycle.UpsertCandidate(new NamePlateDistanceAwareOverlayCandidate(
            10u,
            "Far Original",
            "Far Translation"));

        Assert.True(lifecycle.TrySync(
            overlay,
            new Vector2(1000f, 600f),
            candidate => candidate.EntityId == 20u
                ? new NamePlateDistanceAwareOverlayFrame(
                    new Vector2(200f, 100f),
                    5f,
                    1f,
                    1f)
                : new NamePlateDistanceAwareOverlayFrame(
                    new Vector2(400f, 200f),
                    15f,
                    0.8f,
                    1f)));

        Assert.Equal("Near Translation", overlay.CurrentText);
        Assert.Equal("Near Original", overlay.OriginalName);
        Assert.Equal(new Vector2(110f, 88f), overlay.Position);
    }

    /// <summary>
    ///     Verifies that an invalid candidate cannot clear another candidate
    ///     that still resolves to a visible live frame.
    /// </summary>
    [Fact]
    public void TrySync_ignores_invalid_candidate_when_another_candidate_is_valid()
    {
        using var overlay = new TranslationOverlay();
        var lifecycle = new NamePlateDistanceAwareOverlayLifecycle();
        lifecycle.BeginNamePlateUpdate(isFullUpdate: false, activeNamePlateCount: 2);
        lifecycle.UpsertCandidate(new NamePlateDistanceAwareOverlayCandidate(
            1u,
            "Visible Original",
            "Visible Translation"));
        lifecycle.UpsertCandidate(new NamePlateDistanceAwareOverlayCandidate(
            2u,
            "Invalid Original",
            "Invalid Translation"));

        Assert.True(lifecycle.TrySync(
            overlay,
            new Vector2(1000f, 600f),
            candidate => candidate.EntityId == 1u
                ? new NamePlateDistanceAwareOverlayFrame(
                    new Vector2(200f, 100f),
                    8f,
                    1f,
                    1f)
                : null));

        Assert.True(overlay.Display);
        Assert.Equal("Visible Translation", overlay.CurrentText);
    }

    /// <summary>
    ///     Verifies that projection failure, despawn, or any other failed live
    ///     resolution clears a previously visible overlay when no candidate
    ///     survives.
    /// </summary>
    [Fact]
    public void TrySync_clears_overlay_when_no_candidate_resolves_live()
    {
        using var overlay = new TranslationOverlay();
        var lifecycle = new NamePlateDistanceAwareOverlayLifecycle();
        lifecycle.BeginNamePlateUpdate(isFullUpdate: false, activeNamePlateCount: 1);
        lifecycle.UpsertCandidate(new NamePlateDistanceAwareOverlayCandidate(
            100u,
            "Original",
            "Translated"));
        Assert.True(lifecycle.TrySync(
            overlay,
            new Vector2(1000f, 600f),
            _ => new NamePlateDistanceAwareOverlayFrame(
                new Vector2(100f, 50f),
                6f,
                1f,
                1f)));

        Assert.False(lifecycle.TrySync(
            overlay,
            new Vector2(1000f, 600f),
            _ => null));

        Assert.False(overlay.Display);
        Assert.Equal(string.Empty, overlay.CurrentText);
        Assert.Equal(string.Empty, overlay.OriginalName);
        Assert.Equal(1f, overlay.RenderScale);
        Assert.Equal(1f, overlay.RenderAlpha);
    }

    /// <summary>
    ///     Verifies that a zero-active-nameplate transition clears retained
    ///     candidates and the previously visible shared overlay.
    /// </summary>
    [Fact]
    public void Zero_active_nameplates_clear_candidates_and_overlay()
    {
        using var overlay = new TranslationOverlay();
        var lifecycle = new NamePlateDistanceAwareOverlayLifecycle();
        lifecycle.BeginNamePlateUpdate(isFullUpdate: false, activeNamePlateCount: 1);
        lifecycle.UpsertCandidate(new NamePlateDistanceAwareOverlayCandidate(
            100u,
            "Original",
            "Translated"));
        Assert.True(lifecycle.TrySync(
            overlay,
            new Vector2(1000f, 600f),
            _ => new NamePlateDistanceAwareOverlayFrame(
                new Vector2(100f, 50f),
                6f,
                1f,
                1f)));

        lifecycle.BeginNamePlateUpdate(
            isFullUpdate: false,
            activeNamePlateCount: 0);

        Assert.False(lifecycle.TrySync(
            overlay,
            new Vector2(1000f, 600f),
            _ => throw new InvalidOperationException("No candidate should remain.")));
        Assert.False(overlay.Display);
        Assert.Equal(string.Empty, overlay.CurrentText);
    }

    /// <summary>
    ///     Verifies that the settings UI persists the same ordered distance
    ///     policy that the presentation helper applies at runtime.
    /// </summary>
    [Fact]
    public void NormalizeDistanceAwareOverlayOrdering_matches_runtime_policy()
    {
        var config = new Config
        {
            DistanceAwareOverlayFullScaleDistance = 30f,
            DistanceAwareOverlayFadeStartDistance = 10f,
            DistanceAwareOverlayMaxDistance = 20f,
        };

        Assert.True(OverlayTab.NormalizeDistanceAwareOverlayOrdering(config));
        Assert.Equal(30f, config.DistanceAwareOverlayFullScaleDistance);
        Assert.Equal(30f, config.DistanceAwareOverlayFadeStartDistance);
        Assert.Equal(30.01f, config.DistanceAwareOverlayMaxDistance, 2);
    }
}
