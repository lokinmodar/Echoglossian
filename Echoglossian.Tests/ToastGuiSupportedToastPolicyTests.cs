// <copyright file="ToastGuiSupportedToastPolicyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Toasts;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the merged family-level semantics used by the alternate ToastGui
///     runtime path for supported toasts.
/// </summary>
public class ToastGuiSupportedToastPolicyTests
{
    /// <summary>
    ///     Ensures the supported normal-toast family becomes active whenever
    ///     the hidden runtime toggle is on, without depending on the legacy
    ///     addon-specific toast toggles.
    /// </summary>
    [Fact]
    public void UseSupportedNormalToastRuntime_ReturnsTrue_WhenHiddenRuntimeToggleIsEnabled()
    {
        var config = new Config
        {
            TranslateToast = true,
            UseToastGuiRuntimeForSupportedToasts = true,
        };

        Assert.True(ToastGuiSupportedToastPolicy.UseSupportedNormalToastRuntime(config));
    }

    /// <summary>
    ///     Ensures the unified normal-toast family reuses the canonical
    ///     wide-text toast display mode while the alternate ToastGui path is
    ///     enabled.
    /// </summary>
    [Fact]
    public void GetNormalToastDisplayMode_ReturnsWideTextToastDisplayMode()
    {
        var config = new Config
        {
            WideTextToastTranslationDisplayMode = JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            AreaToastTranslationDisplayMode = JournalTranslationDisplayMode.TooltipTranslation,
            ClassChangeToastTranslationDisplayMode = JournalTranslationDisplayMode.NativeUiTranslation,
        };

        Assert.Equal(
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            ToastGuiSupportedToastPolicy.GetNormalToastDisplayMode(config));
    }
}
