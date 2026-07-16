// <copyright file="PreviewCanvasTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.UI;
using Echoglossian.UIOverlays.TranslationOverlay;

using System.Numerics;
using System.Threading.Tasks;

using Xunit;

namespace Echoglossian.Previewer.Tests.UI;

/// <summary>
/// Covers deterministic preview canvas layout calculations.
/// </summary>
public sealed class PreviewCanvasTests
{
    /// <summary>
    /// Ensures the logical viewport is uniformly scaled and centered in the host space.
    /// </summary>
    [Fact]
    public void CalculateScaledViewport_UniformlyScalesAndCentersLogicalViewport()
    {
        var layout = PreviewCanvas.CalculateScaledViewport(
            availableWidth: 1000f,
            availableHeight: 700f,
            logicalWidth: 1920,
            logicalHeight: 1080);

        Assert.Equal(1000f, layout.Size.X, precision: 3);
        Assert.Equal(562.5f, layout.Size.Y, precision: 3);
        Assert.Equal(0f, layout.Offset.X, precision: 3);
        Assert.Equal(68.75f, layout.Offset.Y, precision: 3);
        Assert.Equal(1000f / 1920f, layout.Scale, precision: 6);
    }

    /// <summary>
    /// Ensures invalid logical viewport dimensions fail fast.
    /// </summary>
    /// <param name="logicalWidth">The invalid logical width.</param>
    /// <param name="logicalHeight">The invalid logical height.</param>
    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    [InlineData(-1, 1080)]
    [InlineData(1920, -1)]
    public void CalculateScaledViewport_InvalidLogicalDimensions_Throws(
        int logicalWidth,
        int logicalHeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PreviewCanvas.CalculateScaledViewport(
                availableWidth: 1000f,
                availableHeight: 700f,
                logicalWidth,
                logicalHeight));
    }

    /// <summary>
    /// Ensures preview title updates respect the shared name semaphore contract.
    /// </summary>
    [Fact]
    public async Task ApplyOverlayState_WaitsForNameSemaphoreBeforeUpdatingTitle()
    {
        using var overlay = new TranslationOverlay();
        var state = new PreviewShellState
        {
            Visible = true,
            Title = "Duty Finder",
            BodyText = "Preview body",
        };
        (string LastBodyText, string LastTitle)? updateResult = null;

        await overlay.NameSemaphore.WaitAsync();
        var updateTask = Task.Run(
            () => updateResult = PreviewCanvas.ApplyOverlayState(
                overlay,
                state,
                string.Empty,
                string.Empty));
        try
        {
            await Task.Delay(100);
            Assert.False(updateTask.IsCompleted);
            Assert.Equal(string.Empty, overlay.CurrentName);
            Assert.Equal(string.Empty, overlay.OriginalName);
        }
        finally
        {
            overlay.NameSemaphore.Release();
        }

        await updateTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal("Duty Finder", overlay.CurrentName);
        Assert.Equal("Duty Finder", overlay.OriginalName);
        Assert.Equal("Preview body", overlay.CurrentText);
        Assert.Equal(("Preview body", "Duty Finder"), updateResult);
    }

    /// <summary>
    /// Ensures preview body updates respect the shared text semaphore contract.
    /// </summary>
    [Fact]
    public async Task ApplyOverlayState_WaitsForTextSemaphoreBeforeUpdatingBody()
    {
        using var overlay = new TranslationOverlay();
        var state = new PreviewShellState
        {
            Visible = true,
            Title = "Quest Tracker",
            BodyText = "Translated objective",
        };
        (string LastBodyText, string LastTitle)? updateResult = null;

        await overlay.Semaphore.WaitAsync();
        var updateTask = Task.Run(
            () => updateResult = PreviewCanvas.ApplyOverlayState(
                overlay,
                state,
                string.Empty,
                string.Empty));
        try
        {
            await Task.Delay(100);
            Assert.False(updateTask.IsCompleted);
            Assert.Equal("Quest Tracker", overlay.CurrentName);
            Assert.Equal("Quest Tracker", overlay.OriginalName);
            Assert.Equal(string.Empty, overlay.CurrentText);
        }
        finally
        {
            overlay.Semaphore.Release();
        }

        await updateTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal("Translated objective", overlay.CurrentText);
        Assert.True(overlay.Display);
        Assert.Equal(("Translated objective", "Quest Tracker"), updateResult);
    }
}
