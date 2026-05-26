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
    ///     Ensures the default supported normal-toast route remains on the
    ///     legacy addon-handler path when neither hidden ToastGui toggle is
    ///     enabled.
    /// </summary>
    [Fact]
    public void GetSupportedNormalToastRouteState_ReturnsLegacyAddonHandlers_ByDefault()
    {
        var config = new Config
        {
            TranslateToast = true,
        };

        Assert.Equal(
            ToastGuiRouteState.LegacyAddonHandlers,
            ToastGuiSupportedToastPolicy.GetSupportedNormalToastRouteState(config));
    }

    /// <summary>
    ///     Ensures the supported normal-toast route reports the capture-only
    ///     prefetch path when the legacy ToastGui capture toggle is enabled.
    /// </summary>
    [Fact]
    public void GetSupportedNormalToastRouteState_ReturnsToastGuiCapturePrefetch_WhenLegacyCaptureIsEnabled()
    {
        var config = new Config
        {
            TranslateToast = true,
            TranslateWideTextToast = true,
            UseToastGuiCaptureForSupportedToasts = true,
        };

        Assert.Equal(
            ToastGuiRouteState.ToastGuiCapturePrefetch,
            ToastGuiSupportedToastPolicy.GetSupportedNormalToastRouteState(config));
    }

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
        Assert.Equal(
            ToastGuiRouteState.ToastGuiFullRuntime,
            ToastGuiSupportedToastPolicy.GetSupportedNormalToastRouteState(config));
    }

    /// <summary>
    ///     Ensures the default supported error-toast route remains on the
    ///     legacy addon-handler path when the hidden full-runtime toggle is
    ///     disabled.
    /// </summary>
    [Fact]
    public void GetSupportedErrorToastRouteState_ReturnsLegacyAddonHandlers_ByDefault()
    {
        var config = new Config
        {
            TranslateToast = true,
            TranslateErrorToast = true,
        };

        Assert.Equal(
            ToastGuiRouteState.LegacyAddonHandlers,
            ToastGuiSupportedToastPolicy.GetSupportedErrorToastRouteState(config));
    }

    /// <summary>
    ///     Ensures the supported error-toast route reports the capture-only
    ///     prefetch path when the legacy ToastGui capture toggle is enabled.
    /// </summary>
    [Fact]
    public void GetSupportedErrorToastRouteState_ReturnsToastGuiCapturePrefetch_WhenLegacyCaptureIsEnabled()
    {
        var config = new Config
        {
            TranslateToast = true,
            TranslateErrorToast = true,
            UseToastGuiCaptureForSupportedToasts = true,
        };

        Assert.Equal(
            ToastGuiRouteState.ToastGuiCapturePrefetch,
            ToastGuiSupportedToastPolicy.GetSupportedErrorToastRouteState(config));
    }

    /// <summary>
    ///     Ensures the supported error-toast route reports the full callback
    ///     runtime when the hidden full ToastGui path is enabled.
    /// </summary>
    [Fact]
    public void GetSupportedErrorToastRouteState_ReturnsToastGuiFullRuntime_WhenFullRuntimeIsEnabled()
    {
        var config = new Config
        {
            TranslateToast = true,
            TranslateErrorToast = true,
            UseToastGuiRuntimeForSupportedToasts = true,
        };

        Assert.Equal(
            ToastGuiRouteState.ToastGuiFullRuntime,
            ToastGuiSupportedToastPolicy.GetSupportedErrorToastRouteState(config));
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
