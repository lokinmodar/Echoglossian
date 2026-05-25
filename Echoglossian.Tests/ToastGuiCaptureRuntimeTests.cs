// <copyright file="ToastGuiCaptureRuntimeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Echoglossian.NativeUI.AddonHandlers.Toasts;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers gating behavior for the legacy ToastGui capture runtime.
/// </summary>
public class ToastGuiCaptureRuntimeTests
{
    /// <summary>
    ///     Ensures the legacy capture-only path shuts itself off when the new
    ///     full callback-owned ToastGui route is enabled for supported toasts.
    /// </summary>
    [Fact]
    public void HandleNormalToast_DoesNotPrefetch_WhenFullToastGuiRuntimeIsEnabled()
    {
        var config = new Config
        {
            TranslateToast = true,
            TranslateWideTextToast = true,
            UseToastGuiCaptureForSupportedToasts = true,
            UseToastGuiRuntimeForSupportedToasts = true,
        };
        var lookupCalls = 0;
        var runtime = new ToastGuiCaptureRuntime(
            config,
            null!,
            _ =>
            {
                lookupCalls++;
                return null;
            },
            _ => throw new InvalidOperationException("Insert should not run."));
        SeString message = string.Empty;
        ToastOptions options = new();
        var isHandled = false;

        runtime.HandleNormalToast(ref message, ref options, ref isHandled);

        Assert.Equal(0, lookupCalls);
    }
}
